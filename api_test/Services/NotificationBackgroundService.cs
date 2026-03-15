using api_test.Data;
using api_test.Entities;
using Microsoft.EntityFrameworkCore;

namespace api_test.Services
{
    /// <summary>
    /// Runs every minute and handles:
    /// 1. ExpiryWarning   — ExpiryDate within 7 days (once per day)
    /// 2. DoseReminder    — 15 min before ScheduledAt
    /// 3. RetryReminder   — dose passed with no action → retry every hour, max 2 retries → Missed
    /// 4. SnoozeReminder  — SnoozedUntil has arrived → send reminder, max 2 snoozes → Missed
    /// </summary>
    public class NotificationBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<NotificationBackgroundService> _logger;

        private readonly TimeSpan _interval = TimeSpan.FromMinutes(1);
        private const int ExpiryWarningDays = 7;
        private const int ReminderMinutesBefore = 15;
        private const int RetryIntervalHours = 1;
        private const int MaxRetries = 2;  // after initial reminder

        public NotificationBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<NotificationBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("NotificationBackgroundService started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try { await RunChecksAsync(); }
                catch (Exception ex) { _logger.LogError(ex, "Notification check failed."); }
                await Task.Delay(_interval, stoppingToken);
            }
        }

        private async Task RunChecksAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var now = DateTime.UtcNow;
            var today = DateOnly.FromDateTime(now);

            await CheckExpiryWarningsAsync(db, now, today);
            await CheckUpcomingRemindersAsync(db, now);
            await CheckSnoozeRemindersAsync(db, now);
            await CheckRetryOrMissedAsync(db, now);

            await db.SaveChangesAsync();
        }

        // =====================================================================
        // 1. EXPIRY WARNING — once per day
        // =====================================================================
        private async Task CheckExpiryWarningsAsync(
            AppDbContext db, DateTime now, DateOnly today)
        {
            var meds = await db.UserMedications
                .Include(um => um.Medication)
                .Where(um => um.NotificationActive && um.ExpiryDate != null)
                .ToListAsync();

            foreach (var um in meds)
            {
                int daysLeft = um.ExpiryDate!.Value.DayNumber - today.DayNumber;
                if (daysLeft < 0 || daysLeft > ExpiryWarningDays) continue;

                bool alreadySent = await db.Alerts.AnyAsync(a =>
                    a.UserMedicationId == um.Id &&
                    a.Type == "ExpiryWarning" &&
                    a.CreatedAt.Date == now.Date);
                if (alreadySent) continue;

                string title = daysLeft == 0 ? "Medication expires today!" : "Medication expiring soon";
                string message = daysLeft == 0
                    ? $"Your medication \"{um.Medication.Trade_name}\" expires today ({um.ExpiryDate.Value:dd/MM/yyyy})."
                    : $"Your medication \"{um.Medication.Trade_name}\" will expire in {daysLeft} day(s) on {um.ExpiryDate.Value:dd/MM/yyyy}.";

                db.Alerts.Add(new Alert
                {
                    UserId = um.UserId,
                    UserMedicationId = um.Id,
                    Type = "ExpiryWarning",
                    Title = title,
                    Message = message,
                    IsRead = false,
                    ScheduledAt = now,
                    CreatedAt = now
                });

                _logger.LogInformation("ExpiryWarning: UserMed={Id}, DaysLeft={Days}", um.Id, daysLeft);
            }
        }

        // =====================================================================
        // 2. UPCOMING REMINDER — 15 min before dose (normal flow, no snooze)
        // =====================================================================
        private async Task CheckUpcomingRemindersAsync(AppDbContext db, DateTime now)
        {
            var upcoming = await db.MedicationSchedules
                .Include(s => s.UserMedication).ThenInclude(um => um.Medication)
                .Where(s =>
                    s.UserMedication.NotificationActive &&
                    !s.ReminderSent &&
                    s.Status == "Pending" &&
                    s.SnoozedUntil == null &&           // not snoozed
                    s.SnoozeCount == 0 &&               // not retried yet
                    s.ScheduledAt >= now &&
                    s.ScheduledAt <= now.AddMinutes(ReminderMinutesBefore))
                .ToListAsync();

            foreach (var schedule in upcoming)
            {
                var um = schedule.UserMedication;
                int minsLeft = (int)(schedule.ScheduledAt - now).TotalMinutes;

                string message = minsLeft <= 1
                    ? $"It's time to take your dose of \"{um.Medication.Trade_name}\" - {um.Dosage}"
                    : $"Reminder: your dose of \"{um.Medication.Trade_name}\" is due in {minsLeft} minute(s) - {um.Dosage}";

                db.Alerts.Add(new Alert
                {
                    UserId = um.UserId,
                    UserMedicationId = um.Id,
                    MedicationScheduleId = schedule.Id,
                    Type = "DoseReminder",
                    Title = "Dose Reminder",
                    Message = message,
                    IsRead = false,
                    ScheduledAt = schedule.ScheduledAt,
                    CreatedAt = now
                });

                schedule.ReminderSent = true;
                schedule.NotificationTime = now;

                _logger.LogInformation("DoseReminder: Schedule={Id}", schedule.Id);
            }
        }

        // =====================================================================
        // 3. SNOOZE REMINDER — SnoozedUntil has arrived
        // =====================================================================
        private async Task CheckSnoozeRemindersAsync(AppDbContext db, DateTime now)
        {
            var snoozed = await db.MedicationSchedules
                .Include(s => s.UserMedication).ThenInclude(um => um.Medication)
                .Where(s =>
                    s.UserMedication.NotificationActive &&
                    !s.ReminderSent &&
                    s.Status == "Pending" &&
                    s.SnoozedUntil != null &&
                    s.SnoozedUntil <= now)
                .ToListAsync();

            foreach (var schedule in snoozed)
            {
                var um = schedule.UserMedication;

                // lو وصل للحد الأقصى → Missed
                if (schedule.SnoozeCount > MaxRetries)
                {
                    schedule.Status = "Missed";
                    schedule.ReminderSent = true;
                    schedule.SnoozedUntil = null;

                    db.Alerts.Add(new Alert
                    {
                        UserId = um.UserId,
                        UserMedicationId = um.Id,
                        MedicationScheduleId = schedule.Id,
                        Type = "MissedDose",
                        Title = "Missed Dose",
                        Message = $"You missed your dose of \"{um.Medication.Trade_name}\". " +
                                               $"Please consult your schedule.",
                        IsRead = false,
                        ScheduledAt = schedule.ScheduledAt,
                        CreatedAt = now
                    });

                    _logger.LogInformation("MissedDose (snooze limit): Schedule={Id}", schedule.Id);
                    continue;
                }

                // بعت تذكير عادي
                db.Alerts.Add(new Alert
                {
                    UserId = um.UserId,
                    UserMedicationId = um.Id,
                    MedicationScheduleId = schedule.Id,
                    Type = "DoseReminder",
                    Title = $"Dose Reminder (snooze {schedule.SnoozeCount}/{MaxRetries})",
                    Message = $"Reminder: take your dose of \"{um.Medication.Trade_name}\" - {um.Dosage}",
                    IsRead = false,
                    ScheduledAt = schedule.SnoozedUntil!.Value,
                    CreatedAt = now
                });

                schedule.ReminderSent = true;
                schedule.SnoozedUntil = null;

                _logger.LogInformation("SnoozeReminder: Schedule={Id}, Count={Count}",
                    schedule.Id, schedule.SnoozeCount);
            }
        }

        // =====================================================================
        // 4. RETRY OR MISSED — dose passed with no action (Flow 1)
        //    - SnoozeCount tracks retries: 0=initial, 1=retry1, 2=retry2 → Missed
        // =====================================================================
        private async Task CheckRetryOrMissedAsync(AppDbContext db, DateTime now)
        {
            // schedules that:
            // - passed their scheduled time
            // - still Pending
            // - not snoozed
            // - ReminderSent = true (initial reminder was sent)
            var overdue = await db.MedicationSchedules
                .Include(s => s.UserMedication).ThenInclude(um => um.Medication)
                .Where(s =>
                    s.UserMedication.NotificationActive &&
                    s.ReminderSent &&
                    s.Status == "Pending" &&
                    s.SnoozedUntil == null &&
                    s.ScheduledAt < now)
                .ToListAsync();

            foreach (var schedule in overdue)
            {
                var um = schedule.UserMedication;

                // Check if enough time has passed since last reminder (1 hour)
                var lastReminderTime = schedule.NotificationTime ?? schedule.ScheduledAt;
                if ((now - lastReminderTime).TotalHours < RetryIntervalHours)
                    continue;

                // Max retries reached → Missed
                if (schedule.SnoozeCount >= MaxRetries)
                {
                    schedule.Status = "Missed";
                    schedule.ReminderSent = true;

                    db.Alerts.Add(new Alert
                    {
                        UserId = um.UserId,
                        UserMedicationId = um.Id,
                        MedicationScheduleId = schedule.Id,
                        Type = "MissedDose",
                        Title = "Missed Dose",
                        Message = $"You missed your dose of \"{um.Medication.Trade_name}\". " +
                                               $"Please consult your schedule.",
                        IsRead = false,
                        ScheduledAt = schedule.ScheduledAt,
                        CreatedAt = now
                    });

                    _logger.LogInformation("MissedDose (retry limit): Schedule={Id}", schedule.Id);
                    continue;
                }

                // Send retry reminder
                schedule.SnoozeCount++;
                schedule.NotificationTime = now;

                db.Alerts.Add(new Alert
                {
                    UserId = um.UserId,
                    UserMedicationId = um.Id,
                    MedicationScheduleId = schedule.Id,
                    Type = "DoseReminder",
                    Title = $"Dose Reminder (retry {schedule.SnoozeCount}/{MaxRetries})",
                    Message = $"Don't forget your dose of \"{um.Medication.Trade_name}\" - {um.Dosage}",
                    IsRead = false,
                    ScheduledAt = now,
                    CreatedAt = now
                });

                _logger.LogInformation("RetryReminder: Schedule={Id}, Retry={Count}",
                    schedule.Id, schedule.SnoozeCount);
            }
        }
    }
}