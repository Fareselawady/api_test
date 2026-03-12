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
        private readonly IInteractionService _interactionService; // ← NEW

        public UserMedicationsController(
            AppDbContext context,
            IScheduleService scheduleService,
            IInteractionService interactionService) // ← NEW
        {
            _context = context;
            _scheduleService = scheduleService;
            _interactionService = interactionService; // ← NEW
        }

        // ================= CREATE (single-step) =================
        [HttpPost]
        public async Task<ActionResult> AddUserMedication(CreateUserMedicationDto dto)
        {
            var userId = GetUserId();

            // 1. Validate the medication exists
            var medication = await _context.Medications
                .FirstOrDefaultAsync(m => m.Trade_name == dto.MedicationName);

            if (medication == null)
                return NotFound(new { Message = $"Medication '{dto.MedicationName}' not found." });

            // 2. Prevent duplicates
            var alreadyExists = await _context.UserMedications
                .AnyAsync(um => um.UserId == userId && um.MedId == medication.ID);

            if (alreadyExists)
                return BadRequest(new { Message = $"You already have '{dto.MedicationName}' in your medications." });

            // 3. Save the new medication
            var userMed = new UserMedication
            {
                UserId = userId,
                MedId = medication.ID,
                Dosage = dto.Dosage,
                Notes = dto.Notes,
                StartDate = dto.StartDate.HasValue ? DateOnly.FromDateTime(dto.StartDate.Value) : null,
                EndDate = dto.EndDate.HasValue ? DateOnly.FromDateTime(dto.EndDate.Value) : null,
                ExpiryDate = dto.ExpiryDate.HasValue ? DateOnly.FromDateTime(dto.ExpiryDate.Value) : null,
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

            // 4. Check interactions AFTER saving — medication is always added regardless
            //    Pass userId so the service only scans THIS user's medications,
            //    and exclude the just-saved med (service skips same MedId internally).
            var warnings = await _interactionService
                .CheckInteractionsForNewMedAsync(userId, dto.MedicationName);

            // 5. Build response — same shape whether warnings exist or not
            if (warnings.Count == 0)
            {
                return Ok(new
                {
                    Message = $"'{dto.MedicationName}' added to your medications successfully."
                });
            }

            return Ok(new
            {
                Message = $"'{dto.MedicationName}' added to your medications successfully.",
                InteractionWarnings = warnings
                // Example when warnings exist:
                // "interactionWarnings": [
                //   "Warning: 'Ibuprofen' may interact with 'Aspirin' (Major).",
                //   "Warning: 'Ibuprofen' may interact with 'Warfarin' (Moderate)."
                // ]
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
        public async Task<ActionResult> UpdateUserMedication(int id, CreateUserMedicationDto dto)
        {
            var userId = GetUserId();

            var userMed = await _context.UserMedications
                .FirstOrDefaultAsync(um => um.Id == id && um.UserId == userId);

            if (userMed == null)
                return NotFound(new { Message = "UserMedication not found." });

            var medication = await _context.Medications
                .FirstOrDefaultAsync(m => m.Trade_name == dto.MedicationName);

            if (medication == null)
                return NotFound(new { Message = $"Medication '{dto.MedicationName}' not found." });

            userMed.MedId = medication.ID;
            userMed.Dosage = dto.Dosage;
            userMed.Notes = dto.Notes;
            userMed.StartDate = dto.StartDate.HasValue ? DateOnly.FromDateTime(dto.StartDate.Value) : null;
            userMed.EndDate = dto.EndDate.HasValue ? DateOnly.FromDateTime(dto.EndDate.Value) : null;
            userMed.ExpiryDate = dto.ExpiryDate.HasValue ? DateOnly.FromDateTime(dto.ExpiryDate.Value) : null;
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
            return Ok(new { Message = "UserMedication updated successfully." });
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

            // Check interactions for the 2-step flow too
            var warnings = await _interactionService
                .CheckInteractionsForNewMedAsync(userId, dto.MedicationName);

            var response = new
            {
                Message = "Medication selected successfully.",
                UserMedicationId = userMed.Id,
                MedicationName = medication.Trade_name,
                InteractionWarnings = warnings.Count > 0 ? warnings : null
            };

            return Ok(response);
        }

        // ================= 2-STEP: DETAILS =================
        [HttpPut("{id}/details")]
        public async Task<ActionResult> AddUserMedicationDetails(int id, UserMedicationDetailsDto dto)
        {
            var userId = GetUserId();

            var userMed = await _context.UserMedications
                .FirstOrDefaultAsync(um => um.Id == id && um.UserId == userId);

            if (userMed == null)
                return NotFound(new { Message = "UserMedication not found." });

            userMed.Dosage = dto.Dosage;
            userMed.Notes = dto.Notes;
            userMed.StartDate = dto.StartDate.HasValue ? DateOnly.FromDateTime(dto.StartDate.Value) : null;
            userMed.EndDate = dto.EndDate.HasValue ? DateOnly.FromDateTime(dto.EndDate.Value) : null;
            userMed.ExpiryDate = dto.ExpiryDate.HasValue ? DateOnly.FromDateTime(dto.ExpiryDate.Value) : null;
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

            return Ok(new { Message = "Medication details added successfully." });
        }

        // ================= HELPERS =================
        private int GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim == null) throw new UnauthorizedAccessException();
            return int.Parse(claim.Value);
        }
    }
}