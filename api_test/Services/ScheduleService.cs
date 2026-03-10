using api_test.Data;
using api_test.Entities;
using api_test.Models;
using Microsoft.EntityFrameworkCore;

namespace api_test.Services
{
    public class ScheduleService : IScheduleService
    {
        private readonly AppDbContext _context;

        public ScheduleService(AppDbContext context)
        {
            _context = context;
        }

        // =====================================================================
        // GENERATION
        // =====================================================================

        public async Task GenerateScheduleAsync(UserMedication userMed)
        {
            if (!userMed.StartDate.HasValue || !userMed.FirstDoseTime.HasValue)
                return;

            var schedules = new List<MedicationSchedule>();

            DateTime start = userMed.StartDate.Value.ToDateTime(userMed.FirstDoseTime.Value);

            DateTime end = userMed.EndDate.HasValue
                ? userMed.EndDate.Value.ToDateTime(TimeOnly.MinValue).AddDays(1)
                : start.AddYears(1);

            if (end <= start)
                end = start.AddDays(1);

            if (userMed.IntervalHours.HasValue && userMed.IntervalHours > 0)
            {
                var current = start;
                while (current < end)
                {
                    schedules.Add(BuildEntry(userMed.Id, current));
                    current = current.AddHours(userMed.IntervalHours.Value);
                }
            }
            else if (userMed.DosesPerPeriod.HasValue && userMed.DosesPerPeriod > 0
                  && userMed.PeriodValue.HasValue && userMed.PeriodValue > 0
                  && !string.IsNullOrWhiteSpace(userMed.PeriodUnit))
            {
                double periodHours = GetPeriodHours(userMed.PeriodUnit, userMed.PeriodValue.Value);
                double intervalBetweenDoses = periodHours / userMed.DosesPerPeriod.Value;

                var current = start;
                while (current < end)
                {
                    for (int i = 0; i < userMed.DosesPerPeriod.Value; i++)
                    {
                        var doseTime = current.AddHours(intervalBetweenDoses * i);
                        if (doseTime >= end) break;
                        schedules.Add(BuildEntry(userMed.Id, doseTime));
                    }
                    current = current.AddHours(periodHours);
                }
            }
            else
            {
                schedules.Add(BuildEntry(userMed.Id, start));
            }

            await _context.MedicationSchedules.AddRangeAsync(schedules);
            await _context.SaveChangesAsync();
        }

        // =====================================================================
        // QUERIES
        // =====================================================================

        public async Task<List<MedicationScheduleDto>> GetSchedulesForMedicationAsync(
            int userMedId, int requestingUserId)
        {
            var medExists = await _context.UserMedications
                .AnyAsync(um => um.Id == userMedId && um.UserId == requestingUserId);

            if (!medExists)
                return new List<MedicationScheduleDto>();

            return await _context.MedicationSchedules
                .Include(s => s.UserMedication)
                    .ThenInclude(um => um!.Medication)
                .Where(s => s.UserMedicationId == userMedId)
                .OrderBy(s => s.ScheduledAt)
                .Select(s => ToScheduleDto(s))
                .ToListAsync();
        }

        public async Task<List<AlertDto>> GetPendingAlertsAsync(int userId)
        {
            return await _context.Alerts
                .Where(a => a.UserId == userId && !a.IsRead)
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new AlertDto
                {
                    Id = a.Id,
                    UserId = a.UserId,
                    UserMedicationId = a.UserMedicationId,
                    MedicationScheduleId = a.MedicationScheduleId,
                    Type = a.Type,
                    Title = a.Title,
                    Message = a.Message,
                    IsRead = a.IsRead,
                    CreatedAt = a.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ")
                })
                .ToListAsync();
        }

        public async Task<List<MedicationScheduleDto>> GetTodaySchedulesAsync(int userId)
        {
            var todayStart = DateTime.UtcNow.Date;
            var todayEnd = todayStart.AddDays(1);

            return await _context.MedicationSchedules
                .Include(s => s.UserMedication)
                    .ThenInclude(um => um!.Medication)
                .Where(s => s.UserMedication!.UserId == userId
                         && s.ScheduledAt >= todayStart
                         && s.ScheduledAt < todayEnd)
                .OrderBy(s => s.ScheduledAt)
                .Select(s => ToScheduleDto(s))
                .ToListAsync();
        }

        public async Task<bool> UpdateScheduleStatusAsync(
            int scheduleId, string newStatus, int requestingUserId)
        {
            var valid = new[] { "Pending", "Taken", "Missed" };
            if (!valid.Contains(newStatus))
                throw new ArgumentException(
                    $"Invalid status '{newStatus}'. Must be Pending, Taken or Missed.");

            var schedule = await _context.MedicationSchedules
                .Include(s => s.UserMedication)
                .FirstOrDefaultAsync(s => s.Id == scheduleId
                                       && s.UserMedication!.UserId == requestingUserId);

            if (schedule is null) return false;

            schedule.Status = newStatus;
            await _context.SaveChangesAsync();
            return true;
        }

        // ── NEW: Schedules by specific date ───────────────────────────────────
        /// <summary>
        /// Returns all MedicationSchedule records for a user where ScheduledAt
        /// falls on the given date (UTC). Produces a >= / < range comparison so
        /// EF Core can push it down as a SQL BETWEEN-style filter — never use
        /// EF.Functions or .Date in a LINQ query because they may not translate.
        /// </summary>
        public async Task<List<MedicationScheduleDto>> GetSchedulesByDateAsync(
            int userId, DateOnly date)
        {
            // Convert DateOnly → two DateTime fence-posts so EF Core can
            // translate the comparison to SQL without any client-side evaluation.
            //   dayStart = 2026-03-12 00:00:00  (inclusive)
            //   dayEnd   = 2026-03-13 00:00:00  (exclusive)
            DateTime dayStart = date.ToDateTime(TimeOnly.MinValue);   // midnight
            DateTime dayEnd = dayStart.AddDays(1);                  // next midnight

            return await _context.MedicationSchedules
                .Include(s => s.UserMedication)
                    .ThenInclude(um => um!.Medication)
                .Where(s => s.UserMedication!.UserId == userId
                         && s.ScheduledAt >= dayStart                 // >= 2026-03-12 00:00
                         && s.ScheduledAt < dayEnd)                  // <  2026-03-13 00:00
                .OrderBy(s => s.ScheduledAt)
                .Select(s => ToScheduleDto(s))
                .ToListAsync();
        }

        // =====================================================================
        // PRIVATE HELPERS
        // =====================================================================

        private static MedicationSchedule BuildEntry(int userMedId, DateTime scheduledAt)
            => new MedicationSchedule
            {
                UserMedicationId = userMedId,
                ScheduledAt = scheduledAt,
                NotificationTime = scheduledAt.AddMinutes(-15),
                Status = "Pending",
                ReminderSent = false,
                SnoozeCount = 0,
                CreatedAt = DateTime.UtcNow
            };

        private static MedicationScheduleDto ToScheduleDto(MedicationSchedule s)
            => new MedicationScheduleDto
            {
                Id = s.Id,
                UserMedId = s.UserMedicationId,
                MedName = s.UserMedication?.Medication?.Trade_name ?? string.Empty,
                ScheduledAt = s.ScheduledAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                NotificationTime = s.NotificationTime.HasValue
                    ? s.NotificationTime.Value.ToString("yyyy-MM-ddTHH:mm:ssZ")
                    : string.Empty,
                Status = s.Status,
                ReminderSent = s.ReminderSent,
                SnoozeCount = s.SnoozeCount
            };

        private static double GetPeriodHours(string periodUnit, int periodValue)
            => periodUnit.ToLower() switch
            {
                "hour" or "hours" => periodValue,
                "day" or "days" => periodValue * 24.0,
                "week" or "weeks" => periodValue * 24.0 * 7,
                "month" or "months" => periodValue * 24.0 * 30,
                _ => periodValue * 24.0
            };
    }
}