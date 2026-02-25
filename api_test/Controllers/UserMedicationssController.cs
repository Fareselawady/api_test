using api_test.Data;
using api_test.Entities;
using api_test.Models;
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

        public UserMedicationsController(AppDbContext context)
        {
            _context = context;
        }

        // ================= CREATE =================
        [HttpPost]
        public async Task<ActionResult> AddUserMedication(CreateUserMedicationDto dto)
        {
            var userId = GetUserId();

            var userMed = new UserMedication
            {
                UserId = userId,
                MedId = dto.MedId,
                Dosage = dto.Dosage,
                Notes = dto.Notes,
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
                ExpiryDate = dto.ExpiryDate,
                CurrentPillCount = dto.CurrentPillCount,
                InitialPillCount = dto.InitialPillCount,
                LowStockThreshold = dto.LowStockThreshold,
                DosesPerPeriod = dto.DosesPerPeriod,
                PeriodUnit = dto.PeriodUnit,
                PeriodValue = dto.PeriodValue,
                FirstDoseTime = dto.FirstDoseTime,
                IntervalHours = dto.IntervalHours,
                NotificationActive = dto.NotificationActive
            };

            _context.UserMedications.Add(userMed);
            await _context.SaveChangesAsync();

            return Ok("UserMedication added successfully");
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
                    StartDate = um.StartDate,
                    EndDate = um.EndDate,
                    ExpiryDate = um.ExpiryDate,
                    CurrentPillCount = um.CurrentPillCount,
                    InitialPillCount = um.InitialPillCount,
                    LowStockThreshold = um.LowStockThreshold,
                    DosesPerPeriod = um.DosesPerPeriod,
                    PeriodUnit = um.PeriodUnit,
                    PeriodValue = um.PeriodValue,
                    FirstDoseTime = um.FirstDoseTime,
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

            if (userMed == null) return NotFound("UserMedication not found");

            userMed.MedId = dto.MedId;
            userMed.Dosage = dto.Dosage;
            userMed.Notes = dto.Notes;
            userMed.StartDate = dto.StartDate;
            userMed.EndDate = dto.EndDate;
            userMed.ExpiryDate = dto.ExpiryDate;
            userMed.CurrentPillCount = dto.CurrentPillCount;
            userMed.InitialPillCount = dto.InitialPillCount;
            userMed.LowStockThreshold = dto.LowStockThreshold;
            userMed.DosesPerPeriod = dto.DosesPerPeriod;
            userMed.PeriodUnit = dto.PeriodUnit;
            userMed.PeriodValue = dto.PeriodValue;
            userMed.FirstDoseTime = dto.FirstDoseTime;
            userMed.IntervalHours = dto.IntervalHours;
            userMed.NotificationActive = dto.NotificationActive;

            await _context.SaveChangesAsync();
            return Ok("UserMedication updated successfully");
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
    }

    
}