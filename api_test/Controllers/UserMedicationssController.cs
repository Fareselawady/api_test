using api_test.Data;
using api_test.Entities;
using api_test.Models;
using api_test.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace api_test.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UserMedicationsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IScheduleService _scheduleService;
        private readonly IInteractionService _interactionService;

        public UserMedicationsController(
            AppDbContext context,
            IScheduleService scheduleService,
            IInteractionService interactionService)
        {
            _context = context;
            _scheduleService = scheduleService;
            _interactionService = interactionService;
        }

        // ================= CREATE (single-step) =================
        [HttpPost]
        public async Task<ActionResult> AddUserMedication(CreateUserMedicationDto dto)
        {
            var userId = GetUserId();

            var medication = await _context.Medications
                .FirstOrDefaultAsync(m => m.Trade_name == dto.MedicationName);

            if (medication == null)
                return NotFound(new { Message = $"Medication '{dto.MedicationName}' not found." });

            var alreadyExists = await _context.UserMedications
                .AnyAsync(um => um.UserId == userId && um.MedId == medication.ID);

            if (alreadyExists)
                return BadRequest(new { Message = $"You already have '{dto.MedicationName}' in your medications." });

            var originalExpiry = dto.ExpiryDate.HasValue
                ? DateOnly.FromDateTime(dto.ExpiryDate.Value) : (DateOnly?)null;

            var adjustedExpiry = AdjustExpiryForLiquid(originalExpiry, medication.Dosage_Form);

            var userMed = new UserMedication
            {
                UserId = userId,
                MedId = medication.ID,
                Dosage = dto.Dosage,
                Notes = dto.Notes,
                StartDate = dto.StartDate.HasValue ? DateOnly.FromDateTime(dto.StartDate.Value) : null,
                EndDate = dto.EndDate.HasValue ? DateOnly.FromDateTime(dto.EndDate.Value) : null,
                ExpiryDate = adjustedExpiry,
                CurrentPillCount = dto.CurrentPillCount,
                InitialPillCount = dto.InitialPillCount,
                LowStockThreshold = dto.LowStockThreshold,
                DosesPerPeriod = dto.DosesPerPeriod,
                PeriodUnit = dto.PeriodUnit,
                PeriodValue = dto.PeriodValue,
                FirstDoseTime = dto.FirstDoseTime.HasValue ? TimeOnly.FromTimeSpan(dto.FirstDoseTime.Value) : null,
                IntervalHours = dto.IntervalHours,
                NotificationActive = dto.NotificationActive
            };

            _context.UserMedications.Add(userMed);
            await _context.SaveChangesAsync();
            await _scheduleService.GenerateScheduleAsync(userMed);

            var warnings = await _interactionService
                .CheckInteractionsForNewMedAsync(userId, dto.MedicationName);

            bool expiryWasAdjusted = adjustedExpiry != originalExpiry;

            return Ok(new
            {
                Message = $"'{dto.MedicationName}' added to your medications successfully.",
                ExpiryDate = adjustedExpiry,
                ExpiryAdjusted = expiryWasAdjusted,
                ExpiryAdjustedNote = expiryWasAdjusted
                    ? (string?)$"Expiry date adjusted from {originalExpiry:dd/MM/yyyy} to {adjustedExpiry:dd/MM/yyyy} because this is a liquid medication (opened shelf life = 3 months)."
                    : null,
                InteractionWarnings = warnings.Count > 0 ? warnings : null
            });
        }

        // ================= READ =================
        [HttpGet("myusermeds")]
        public async Task<ActionResult> GetMyUserMedications()
        {
            var userId = GetUserId();

            var userMeds = await _context.UserMedications
                .Include(um => um.Medication)
                .Where(um => um.UserId == userId)
                .ToListAsync();

            var result = new List<UserMedicationDto>();

            foreach (var um in userMeds)
            {
                var interactions = await _interactionService
                    .GetInteractionsForUserMedication(userId, um.MedId);

                result.Add(new UserMedicationDto
                {
                    Id = um.Id,
                    MedId = um.MedId,
                    MedName = um.Medication.Trade_name,
                    Dosage = um.Dosage,
                    Notes = um.Notes,
                    StartDate = um.StartDate?.ToDateTime(TimeOnly.MinValue),
                    EndDate = um.EndDate?.ToDateTime(TimeOnly.MinValue),
                    ExpiryDate = um.ExpiryDate?.ToDateTime(TimeOnly.MinValue),
                    CurrentPillCount = um.CurrentPillCount,
                    InitialPillCount = um.InitialPillCount,
                    LowStockThreshold = um.LowStockThreshold,
                    DosesPerPeriod = um.DosesPerPeriod,
                    PeriodUnit = um.PeriodUnit,
                    PeriodValue = um.PeriodValue,
                    FirstDoseTime = um.FirstDoseTime?.ToTimeSpan(),
                    IntervalHours = um.IntervalHours,
                    NotificationActive = um.NotificationActive,
                    Interactions = interactions
                });
            }

            return Ok(result);
        }

        // ================= UPDATE =================
        [HttpPut("{id}")]
        public async Task<ActionResult> UpdateUserMedication(int id, UpdateUserMedicationDto dto)
        {
            var userId = GetUserId();

            var userMed = await _context.UserMedications
                .Include(um => um.Medication)
                .FirstOrDefaultAsync(um => um.Id == id && um.UserId == userId);

            if (userMed == null)
                return NotFound(new { Message = "UserMedication not found." });

            // الحقول العادية
            if (dto.Dosage != null) userMed.Dosage = dto.Dosage;
            if (dto.Notes != null) userMed.Notes = dto.Notes;
            if (dto.StartDate.HasValue) userMed.StartDate = DateOnly.FromDateTime(dto.StartDate.Value);
            if (dto.EndDate.HasValue) userMed.EndDate = DateOnly.FromDateTime(dto.EndDate.Value);
            if (dto.CurrentPillCount.HasValue) userMed.CurrentPillCount = dto.CurrentPillCount;
            if (dto.InitialPillCount.HasValue) userMed.InitialPillCount = dto.InitialPillCount;
            if (dto.LowStockThreshold.HasValue) userMed.LowStockThreshold = dto.LowStockThreshold;
            if (dto.FirstDoseTime.HasValue) userMed.FirstDoseTime = TimeOnly.FromTimeSpan(dto.FirstDoseTime.Value);
            if (dto.NotificationActive.HasValue) userMed.NotificationActive = dto.NotificationActive.Value;

            // Scheduling — لو بعت intervalHours امسح الـ period والعكس
            if (dto.IntervalHours.HasValue)
            {
                userMed.IntervalHours = dto.IntervalHours;
                userMed.DosesPerPeriod = null;
                userMed.PeriodUnit = null;
                userMed.PeriodValue = null;
            }
            else if (dto.DosesPerPeriod.HasValue || dto.PeriodUnit != null || dto.PeriodValue.HasValue)
            {
                if (dto.DosesPerPeriod.HasValue) userMed.DosesPerPeriod = dto.DosesPerPeriod;
                if (dto.PeriodUnit != null) userMed.PeriodUnit = dto.PeriodUnit;
                if (dto.PeriodValue.HasValue) userMed.PeriodValue = dto.PeriodValue;
                userMed.IntervalHours = null;
            }

            // ExpiryDate فيها logic خاص للسوائل
            DateOnly? adjustedExpiry = null;
            DateOnly? originalExpiry = null;
            if (dto.ExpiryDate.HasValue)
            {
                originalExpiry = DateOnly.FromDateTime(dto.ExpiryDate.Value);
                adjustedExpiry = AdjustExpiryForLiquid(originalExpiry, userMed.Medication?.Dosage_Form);
                userMed.ExpiryDate = adjustedExpiry;
            }

            await _context.SaveChangesAsync();

            // ✅ إعادة الجدولة لو في تغيير يأثر عليها
            bool scheduleChanged = dto.IntervalHours.HasValue
                || dto.DosesPerPeriod.HasValue
                || dto.PeriodUnit != null
                || dto.PeriodValue.HasValue
                || dto.FirstDoseTime.HasValue
                || dto.StartDate.HasValue
                || dto.EndDate.HasValue;

            if (scheduleChanged)
                await _scheduleService.RegenerateScheduleAsync(userMed);

            bool expiryWasAdjusted = adjustedExpiry != null && adjustedExpiry != originalExpiry;

            return Ok(new
            {
                Message = "UserMedication updated successfully.",
                ExpiryDate = userMed.ExpiryDate,
                ExpiryAdjusted = expiryWasAdjusted,
                ExpiryAdjustedNote = expiryWasAdjusted
                    ? (string?)$"Expiry date adjusted from {originalExpiry:dd/MM/yyyy} to {adjustedExpiry:dd/MM/yyyy} because this is a liquid medication (opened shelf life = 3 months)."
                    : null,
                ScheduleRegenerated = scheduleChanged
            });
        }
        // ================= DELETE =================
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteUserMedication(int id)
        {
            var userId = GetUserId();

            var userMed = await _context.UserMedications
                .FirstOrDefaultAsync(um => um.Id == id && um.UserId == userId);

            if (userMed == null) return NotFound("UserMedication not found");

            _context.UserMedications.Remove(userMed);
            await _context.SaveChangesAsync();

            return Ok("UserMedication deleted successfully");
        }

        // ================= 2-STEP: INIT =================
        [HttpPost("init")]
        public async Task<ActionResult> InitUserMedication(InitUserMedicationDto dto)
        {
            var userId = GetUserId();

            var medication = await _context.Medications
                .FirstOrDefaultAsync(m => m.Trade_name == dto.MedicationName);

            if (medication == null)
                return NotFound(new { Message = $"Medication '{dto.MedicationName}' not found." });

            var alreadyExists = await _context.UserMedications
                .AnyAsync(um => um.UserId == userId && um.MedId == medication.ID);

            if (alreadyExists)
                return BadRequest(new { Message = $"You already have '{dto.MedicationName}' in your medications." });

            var userMed = new UserMedication
            {
                UserId = userId,
                MedId = medication.ID
            };

            _context.UserMedications.Add(userMed);
            await _context.SaveChangesAsync();

            var warnings = await _interactionService
                .CheckInteractionsForNewMedAsync(userId, dto.MedicationName);

            return Ok(new
            {
                Message = "Medication selected successfully.",
                UserMedicationId = userMed.Id,
                MedicationName = medication.Trade_name,
                DosageForm = medication.Dosage_Form,
                InteractionWarnings = warnings.Count > 0 ? warnings : null
            });
        }

        // ================= 2-STEP: DETAILS =================
        [HttpPut("{id}/details")]
        public async Task<ActionResult> AddUserMedicationDetails(int id, UserMedicationDetailsDto dto)
        {
            var userId = GetUserId();

            var userMed = await _context.UserMedications
                .Include(um => um.Medication)
                .FirstOrDefaultAsync(um => um.Id == id && um.UserId == userId);

            if (userMed == null)
                return NotFound(new { Message = "UserMedication not found." });

            var originalExpiry = dto.ExpiryDate.HasValue
                ? DateOnly.FromDateTime(dto.ExpiryDate.Value) : (DateOnly?)null;

            var adjustedExpiry = AdjustExpiryForLiquid(originalExpiry, userMed.Medication?.Dosage_Form);

            userMed.Dosage = dto.Dosage;
            userMed.Notes = dto.Notes;
            userMed.StartDate = dto.StartDate.HasValue ? DateOnly.FromDateTime(dto.StartDate.Value) : null;
            userMed.EndDate = dto.EndDate.HasValue ? DateOnly.FromDateTime(dto.EndDate.Value) : null;
            userMed.ExpiryDate = adjustedExpiry;
            userMed.CurrentPillCount = dto.CurrentPillCount;
            userMed.InitialPillCount = dto.InitialPillCount;
            userMed.LowStockThreshold = dto.LowStockThreshold;
            userMed.DosesPerPeriod = dto.DosesPerPeriod;
            userMed.PeriodUnit = dto.PeriodUnit;
            userMed.PeriodValue = dto.PeriodValue;
            userMed.FirstDoseTime = dto.FirstDoseTime.HasValue ? TimeOnly.FromTimeSpan(dto.FirstDoseTime.Value) : null;
            userMed.IntervalHours = dto.IntervalHours;
            userMed.NotificationActive = dto.NotificationActive;

            await _context.SaveChangesAsync();
            await _scheduleService.GenerateScheduleAsync(userMed);

            bool expiryWasAdjusted = adjustedExpiry != originalExpiry;

            return Ok(new
            {
                Message = "Medication details added successfully.",
                ExpiryDate = adjustedExpiry,
                ExpiryAdjusted = expiryWasAdjusted,
                ExpiryAdjustedNote = expiryWasAdjusted
                    ? (string?)$"Expiry date adjusted from {originalExpiry:dd/MM/yyyy} to {adjustedExpiry:dd/MM/yyyy} because this is a liquid medication (opened shelf life = 3 months)."
                    : null
            });
        }

        // ================= HELPERS =================
        private int GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim == null) throw new UnauthorizedAccessException();
            return int.Parse(claim.Value);
        }

        /// <summary>
        /// لو الدواء سائل → ينقص 3 شهور من تاريخ الصلاحية
        /// عشان فتح العلبة بيقلل العمر الافتراضي
        /// </summary>
        private static DateOnly? AdjustExpiryForLiquid(DateOnly? originalExpiry, string? dosageForm)
        {
            if (string.IsNullOrWhiteSpace(dosageForm) || originalExpiry == null)
                return originalExpiry;

            var liquidForms = new[]
            {
                "Syrup", "Suspension", "Oral_Solution", "Oral_Drop",
                "Oral Drops", "Ampoule", "Vial_powder", "Ophthalmic Solution",
                "Emulgel", "Gel"
            };

            bool isLiquid = liquidForms.Any(f =>
                dosageForm.Equals(f, StringComparison.OrdinalIgnoreCase));

            if (!isLiquid) return originalExpiry;

            return originalExpiry.Value.AddMonths(-3);
        }
    }
}