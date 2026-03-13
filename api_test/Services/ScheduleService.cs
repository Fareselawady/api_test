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

            var cairoZone = TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time");
            DateTime localStart = userMed.StartDate.Value.ToDateTime(userMed.FirstDoseTime.Value);
            DateTime start = TimeZoneInfo.ConvertTimeToUtc(localStart, cairoZone);
            start = DateTime.SpecifyKind(start, DateTimeKind.Utc);

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

        public async Task<List<MedicationScheduleDto>> GetSchedulesByDateAsync(
            int userId, DateOnly date)
        {
            DateTime dayStart = date.ToDateTime(TimeOnly.MinValue);
            DateTime dayEnd = dayStart.AddDays(1);

            return await _context.MedicationSchedules
                .Include(s => s.UserMedication)
                    .ThenInclude(um => um!.Medication)
                .Where(s => s.UserMedication!.UserId == userId
                         && s.ScheduledAt >= dayStart
                         && s.ScheduledAt < dayEnd)
                .OrderBy(s => s.ScheduledAt)
                .Select(s => ToScheduleDto(s))
                .ToListAsync();
        }

        // =====================================================================
        // UPDATE STATUS — Pending / Missed only
        // =====================================================================

        public async Task<bool> UpdateScheduleStatusAsync(
            int scheduleId, string newStatus, int requestingUserId)
        {
            var valid = new[] { "Pending", "Missed" };
            if (!valid.Contains(newStatus))
                throw new ArgumentException(
                    $"Invalid status '{newStatus}'. Use TakeDoseAsync for Taken, or Pending/Missed here.");

            var schedule = await _context.MedicationSchedules
                .Include(s => s.UserMedication)
                .FirstOrDefaultAsync(s => s.Id == scheduleId
                                       && s.UserMedication!.UserId == requestingUserId);

            if (schedule is null) return false;

            if (schedule.Status == "Taken")
                throw new InvalidOperationException(
                    $"Schedule {scheduleId} is already Taken and cannot be changed.");

            schedule.Status = newStatus;
            await _context.SaveChangesAsync();
            return true;
        }

        // =====================================================================
        // TAKE DOSE — Taken + deduct pills + instant low stock check
        // =====================================================================

        public async Task<TakeDoseResult> TakeDoseAsync(int scheduleId, int requestingUserId)
        {
            var schedule = await _context.MedicationSchedules
                .Include(s => s.UserMedication)
                    .ThenInclude(um => um.Medication)
                .FirstOrDefaultAsync(s => s.Id == scheduleId
                                       && s.UserMedication!.UserId == requestingUserId);

            if (schedule is null)
                return TakeDoseResult.NotFound();

            if (schedule.Status == "Taken")
                return TakeDoseResult.AlreadyTaken();

            var userMed = schedule.UserMedication;
            var now = DateTime.UtcNow;

            // 1. Mark as Taken
            schedule.Status = "Taken";
            schedule.TakenAt = now;

            // 2. Deduct pill count
            int pillsDeducted = 0;
            if (userMed.CurrentPillCount.HasValue)
            {
                pillsDeducted = ParsePillsPerDose(userMed.Dosage);
                userMed.CurrentPillCount = Math.Max(0, userMed.CurrentPillCount.Value - pillsDeducted);
            }

            // 3. Instant low stock check
            bool lowStockAlertCreated = false;
            if (userMed.CurrentPillCount.HasValue && userMed.LowStockThreshold.HasValue
                && userMed.CurrentPillCount <= userMed.LowStockThreshold)
            {
                bool alreadySentToday = await _context.Alerts.AnyAsync(a =>
                    a.UserMedicationId == userMed.Id &&
                    a.Type == "LowStock" &&
                    a.CreatedAt.Date == now.Date);

                if (!alreadySentToday)
                {
                    _context.Alerts.Add(new Alert
                    {
                        UserId = userMed.UserId,
                        UserMedicationId = userMed.Id,
                        Type = "LowStock",
                        Title = "Low Medication Stock",
                        Message = $"\"{userMed.Medication.Trade_name}\" is running low. " +
                                           $"Remaining: {userMed.CurrentPillCount} pill(s) " +
                                           $"(threshold: {userMed.LowStockThreshold}).",
                        IsRead = false,
                        ScheduledAt = now,
                        CreatedAt = now
                    });
                    lowStockAlertCreated = true;
                }
            }

            await _context.SaveChangesAsync();

            return TakeDoseResult.Success(
                scheduleId: scheduleId,
                pillsDeducted: pillsDeducted,
                remainingPills: userMed.CurrentPillCount,
                lowStockAlert: lowStockAlertCreated
            );
        }

        // =====================================================================
        // PRIVATE HELPERS
        // =====================================================================

        /// <summary>
        /// Reads the leading integer from Dosage (e.g. "2 tablets" -> 2).
        /// Falls back to 1 if no number is found.
        /// </summary>
        private static int ParsePillsPerDose(string? dosage)
        {
            if (string.IsNullOrWhiteSpace(dosage)) return 1;
            var first = dosage.Trim().Split(' ')[0];
            return int.TryParse(first, out int n) && n > 0 ? n : 1;
        }

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