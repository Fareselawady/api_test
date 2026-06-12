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

        private readonly TimeSpan _interval = TimeSpan.FromSeconds(1);
        private DateOnly? _lastExpiryCheckDate;
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

            if (_lastExpiryCheckDate == null || today != _lastExpiryCheckDate.Value)
            {
                await CheckExpiryWarningsAsync(db, now, today);
                _lastExpiryCheckDate = today;
            }

            var schedules = await db.MedicationSchedules
                .Include(s => s.UserMedication).ThenInclude(um => um.Medication)
                .Where(s => s.UserMedication.NotificationActive
                    && (
                        (s.Status == MedicationStatus.Pending && (
                            (!s.DueReminderSent && s.ScheduledAt <= now) ||
                            (s.UserMedication.AdvanceReminderMinutes != null && !s.AdvanceReminderSent && s.ScheduledAt.AddMinutes(-s.UserMedication.AdvanceReminderMinutes.Value) <= now)
                        )) ||
                        (s.Status == MedicationStatus.Snoozed && s.SnoozedUntil != null && s.SnoozedUntil <= now) ||
                        ((s.Status == MedicationStatus.Pending || s.Status == MedicationStatus.Snoozed) && (s.SnoozedUntil ?? s.ScheduledAt).AddHours(1) <= now)
                    ))
                .ToListAsync();

            await CheckUpcomingRemindersAsync(db, schedules, now);
            await CheckSnoozeRemindersAsync(db, schedules, now);
            await CheckAutoMissedAsync(db, schedules, now);

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
                .Where(um => um.NotificationActive
                    && (um.EffectiveExpiryDate != null || um.ExpiryDate != null))
                .ToListAsync();

            foreach (var um in meds)
            {
                var medName = UserMedicationFeatureHelper.GetDisplayName(um);
                var effectiveExpiry = MedicationExpiryHelper.GetEffectiveExpiryDate(um);
                if (effectiveExpiry == null) continue;

                var effectiveExpiryDate = DateOnly.FromDateTime(effectiveExpiry.Value);
                int daysLeft = effectiveExpiryDate.DayNumber - today.DayNumber;
                if (daysLeft < 0 || daysLeft > ExpiryWarningDays) continue;

                bool alreadySent = await db.Alerts.AnyAsync(a =>
                    a.UserMedicationId == um.Id &&
                    a.Type == "ExpiryWarning" &&
                    a.CreatedAt.Date == now.Date);
                if (alreadySent) continue;

                string title = daysLeft == 0 ? "Medication expires today!" : "Medication expiring soon";
                string message = daysLeft == 0
                    ? $"Your medication \"{medName}\" expires today ({effectiveExpiryDate:dd/MM/yyyy})."
                    : $"Your medication \"{medName}\" will expire in {daysLeft} day(s) on {effectiveExpiryDate:dd/MM/yyyy}.";

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
        // 2. UPCOMING REMINDER — normal flow, including Advance Reminder
        // =====================================================================
        private async Task CheckUpcomingRemindersAsync(AppDbContext db, List<MedicationSchedule> schedules, DateTime now)
        {
            var upcoming = schedules
                .Where(s => s.Status == MedicationStatus.Pending)
                .ToList();

            foreach (var schedule in upcoming)
            {
                var um = schedule.UserMedication;
                var medName = UserMedicationFeatureHelper.GetDisplayName(um);

                // A. Advance Reminder Alert
                if (um.AdvanceReminderMinutes.HasValue && um.AdvanceReminderMinutes.Value > 0)
                {
                    var advanceTime = schedule.ScheduledAt.AddMinutes(-um.AdvanceReminderMinutes.Value);
                    if (now >= advanceTime && !schedule.AdvanceReminderSent)
                    {
                        string advanceMsg = $"Reminder: your dose of \"{medName}\" is due in {um.AdvanceReminderMinutes.Value} minute(s) - {um.Dosage}";
                        db.Alerts.Add(new Alert
                        {
                            UserId = um.UserId,
                            UserMedicationId = um.Id,
                            MedicationScheduleId = schedule.Id,
                            Type = "DoseReminder",
                            Title = "Advance Reminder",
                            Message = advanceMsg,
                            IsRead = false,
                            ScheduledAt = advanceTime,
                            CreatedAt = now
                        });

                        schedule.AdvanceReminderSent = true;
                        schedule.ReminderSent = true;
                        _logger.LogInformation("AdvanceReminder: Schedule={Id}", schedule.Id);
                    }
                }

                // B. Medication Due Alert
                if (now >= schedule.ScheduledAt && !schedule.DueReminderSent)
                {
                    string dueMsg = $"It's time to take your dose of \"{medName}\" - {um.Dosage}";
                    db.Alerts.Add(new Alert
                    {
                        UserId = um.UserId,
                        UserMedicationId = um.Id,
                        MedicationScheduleId = schedule.Id,
                        Type = "DoseReminder",
                        Title = "Dose Reminder Due Now",
                        Message = dueMsg,
                        IsRead = false,
                        ScheduledAt = schedule.ScheduledAt,
                        CreatedAt = now
                    });

                    schedule.DueReminderSent = true;
                    schedule.ReminderSent = true;
                    schedule.NotificationTime = now;
                    _logger.LogInformation("DoseReminderDueNow: Schedule={Id}", schedule.Id);
                }
            }
        }

        // =====================================================================
        // 3. SNOOZE REMINDER — SnoozedUntil has arrived
        // =====================================================================
        private async Task CheckSnoozeRemindersAsync(AppDbContext db, List<MedicationSchedule> schedules, DateTime now)
        {
            _logger.LogInformation("CheckSnoozeReminders: Total loaded schedules={Count}", schedules.Count);
            var snoozedAll = schedules.Where(s => s.Status == MedicationStatus.Snoozed).ToList();
            _logger.LogInformation("CheckSnoozeReminders: Total Snoozed status schedules={Count}", snoozedAll.Count);
            foreach (var s in snoozedAll)
            {
                _logger.LogInformation("Snoozed schedule Id={Id}, SnoozedUntil={SnoozedUntil}, now={Now}, DiffSec={Diff}",
                    s.Id, s.SnoozedUntil, now, s.SnoozedUntil.HasValue ? (now - s.SnoozedUntil.Value).TotalSeconds : 0);
            }

            var snoozed = schedules
                .Where(s => s.Status == MedicationStatus.Snoozed && s.SnoozedUntil.HasValue && s.SnoozedUntil.Value <= now)
                .ToList();

            foreach (var schedule in snoozed)
            {
                var um = schedule.UserMedication;
                var medName = UserMedicationFeatureHelper.GetDisplayName(um);

                db.Alerts.Add(new Alert
                {
                    UserId = um.UserId,
                    UserMedicationId = um.Id,
                    MedicationScheduleId = schedule.Id,
                    Type = "SnoozeReminder",
                    Title = $"Dose Reminder (snooze {schedule.SnoozeCount}/2)",
                    Message = $"Reminder: take your dose of \"{medName}\" - {um.Dosage}",
                    IsRead = false,
                    ScheduledAt = schedule.SnoozedUntil.Value,
                    CreatedAt = now
                });

                schedule.Status = MedicationStatus.Pending;
                // keep SnoozedUntil populated for auto-miss tracking
                schedule.AdvanceReminderSent = true;
                schedule.DueReminderSent = true;
                schedule.ReminderSent = true;

                _logger.LogInformation("SnoozeReminder triggered: Schedule={Id}, Count={Count}",
                    schedule.Id, schedule.SnoozeCount);
            }
        }

        // =====================================================================
        // 4. AUTO MISSED — 1 hour after effective due time
        // =====================================================================
        private async Task CheckAutoMissedAsync(AppDbContext db, List<MedicationSchedule> schedules, DateTime now)
        {
            var autoMissed = schedules
                .Where(s => (s.Status == MedicationStatus.Pending || s.Status == MedicationStatus.Snoozed)
                    && (s.SnoozedUntil ?? s.ScheduledAt).AddHours(1) <= now)
                .ToList();

            foreach (var schedule in autoMissed)
            {
                var um = schedule.UserMedication;
                var medName = UserMedicationFeatureHelper.GetDisplayName(um);

                schedule.Status = MedicationStatus.Missed;
                schedule.MissedAt = now;
                schedule.SnoozedUntil = null;
                schedule.ReminderSent = true;

                db.Alerts.Add(new Alert
                {
                    UserId = um.UserId,
                    UserMedicationId = um.Id,
                    MedicationScheduleId = schedule.Id,
                    Type = "MissedDose",
                    Title = "Missed Dose",
                    Message = $"You missed your dose of \"{medName}\". Please consult your schedule.",
                    IsRead = false,
                    ScheduledAt = schedule.ScheduledAt,
                    CreatedAt = now
                });

                _logger.LogInformation("Auto-missed: Schedule={Id}", schedule.Id);
            }
        }
    }
}
