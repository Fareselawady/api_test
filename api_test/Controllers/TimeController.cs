using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using api_test.Data;
using api_test.Entities;
using Microsoft.EntityFrameworkCore;

namespace api_test.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TimeController : ControllerBase
    {
        private readonly AppDbContext _db;

        public TimeController(AppDbContext db)
        {
            _db = db;
        }

        [HttpGet("api/debug/time")]
        [AllowAnonymous]
        public ActionResult GetTime()
        {
            return Ok(new
            {
                UtcNow = DateTime.UtcNow,
                LocalNow = DateTime.Now
            });
        }

        [HttpGet("api/debug/db")]
        [AllowAnonymous]
        public async Task<ActionResult> GetDebugDb()
        {
            var schedules = await _db.MedicationSchedules
                .Include(s => s.UserMedication)
                .Where(s => s.Status == MedicationStatus.Snoozed || s.SnoozedUntil != null)
                .Select(s => new
                {
                    s.Id,
                    s.UserMedicationId,
                    NotificationActive = s.UserMedication != null ? s.UserMedication.NotificationActive : false,
                    s.ScheduledAt,
                    s.SnoozedUntil,
                    Status = s.Status.ToString(),
                    s.SnoozeCount,
                    s.DueReminderSent,
                    s.ReminderSent
                })
                .ToListAsync();

            var alerts = await _db.Alerts
                .OrderByDescending(a => a.CreatedAt)
                .Take(40)
                .Select(a => new
                {
                    a.Id,
                    a.UserId,
                    a.UserMedicationId,
                    a.MedicationScheduleId,
                    a.Type,
                    a.Title,
                    a.Message,
                    a.IsRead,
                    a.CreatedAt,
                    a.ScheduledAt
                })
                .ToListAsync();

            return Ok(new { schedules, alerts });
        }

        [HttpPost("api/debug/createsnoozed")]
        [AllowAnonymous]
        public async Task<ActionResult> CreateSnoozed()
        {
            var um = await _db.UserMedications.FirstOrDefaultAsync(u => u.NotificationActive);
            if (um == null)
            {
                return BadRequest("No active UserMedication found to link schedule to.");
            }

            var schedule = new MedicationSchedule
            {
                UserMedicationId = um.Id,
                ScheduledAt = DateTime.UtcNow,
                NotificationTime = DateTime.UtcNow,
                SnoozedUntil = DateTime.UtcNow.AddSeconds(10),
                Status = MedicationStatus.Snoozed,
                SnoozeCount = 1,
                ReminderSent = false,
                AdvanceReminderSent = false,
                DueReminderSent = false,
                CreatedAt = DateTime.UtcNow
            };

            _db.MedicationSchedules.Add(schedule);
            await _db.SaveChangesAsync();

            return Ok(new { Message = "Snoozed schedule created.", ScheduleId = schedule.Id, SnoozedUntil = schedule.SnoozedUntil });
        }
    }
}
