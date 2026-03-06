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
        private readonly IScheduleService _scheduleService; // ← أضيف ده

        public UserMedicationsController(AppDbContext context , IScheduleService scheduleService)
        {
            _context = context;
            _scheduleService = scheduleService; // ← أضيف ده
        }

        // ================= CREATE =================
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

            return Ok(new { Message = $"'{dto.MedicationName}' added to your medications successfully." });
        }
        // ================= READ =================
        [HttpGet("myusermeds")]
        public async Task<ActionResult> GetMyUserMedications()
        {
            var userId = GetUserId();

            var meds = await _context.UserMedications
                .Include(um => um.Medication)
                .Where(um => um.UserId == userId)
                .Select(um => new UserMedicationDto
                {
                    Id = um.Id,
                    MedId = um.MedId,
                    MedName = um.Medication.Trade_name,
                    Dosage = um.Dosage,
                    Notes = um.Notes,
                    StartDate = um.StartDate.HasValue ? um.StartDate.Value.ToDateTime(TimeOnly.MinValue) : null,
                    EndDate = um.EndDate.HasValue ? um.EndDate.Value.ToDateTime(TimeOnly.MinValue) : null,
                    ExpiryDate = um.ExpiryDate.HasValue ? um.ExpiryDate.Value.ToDateTime(TimeOnly.MinValue) : null,
                    CurrentPillCount = um.CurrentPillCount,
                    InitialPillCount = um.InitialPillCount,
                    LowStockThreshold = um.LowStockThreshold,
                    DosesPerPeriod = um.DosesPerPeriod,
                    PeriodUnit = um.PeriodUnit,
                    PeriodValue = um.PeriodValue,
                    FirstDoseTime = um.FirstDoseTime.HasValue ? um.FirstDoseTime.Value.ToTimeSpan() : null,
                    IntervalHours = um.IntervalHours,
                    NotificationActive = um.NotificationActive
                })
                .ToListAsync();

            return Ok(meds);
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

            // جيب الدواء بالاسم
            var medication = await _context.Medications
                .FirstOrDefaultAsync(m => m.Trade_name == dto.MedicationName);

            if (medication == null)
                return NotFound(new { Message = $"Medication '{dto.MedicationName}' not found." });


            userMed.MedId = medication.ID;  // ← بدل dto.MedId
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
            return Ok(new { Message = $"UserMedication updated successfully." });
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

        // ================= HELPERS =================
        private int GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim == null) throw new UnauthorizedAccessException();
            return int.Parse(claim.Value);
        }


        //==============================add by OCR or with 2 steps ============================

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

            return Ok(new
            {
                Message = "Medication selected successfully.",
                UserMedicationId = userMed.Id,
                MedicationName = medication.Trade_name
            });
        }

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


    }


}