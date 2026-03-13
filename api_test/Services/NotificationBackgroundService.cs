using api_test.Data;
using api_test.Entities;
using Microsoft.EntityFrameworkCore;

namespace api_test.Services
{
    /// <summary>
    /// Background Service that runs every minute and checks:
    /// 1. ExpiryWarning  — ExpiryDate within 7 days
    /// 2. DoseReminder   — ScheduledAt within 15 minutes and ReminderSent = false
    ///
    /// LowStock is triggered immediately in ScheduleService when the user marks a dose as Taken
    /// </summary>
    public class NotificationBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<NotificationBackgroundService> _logger;

        private readonly TimeSpan _interval = TimeSpan.FromMinutes(1);
        private const int ExpiryWarningDays = 7;
        private const int ReminderMinutesBefore = 15;

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
                try
                {
                    await RunChecksAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Notification check failed.");
                }

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
            await CheckDoseRemindersAsync(db, now);

            await db.SaveChangesAsync();
        }

        // =====================================================================
        // 1. EXPIRY WARNING
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

                // Skip if already expired or outside warning window
                if (daysLeft < 0 || daysLeft > ExpiryWarningDays) continue;

                // Skip if already sent today
                bool alreadySent = await db.Alerts.AnyAsync(a =>
                    a.UserMedicationId == um.Id &&
                    a.Type == "ExpiryWarning" &&
                    a.CreatedAt.Date == now.Date);

                if (alreadySent) continue;

                string title = daysLeft == 0
                    ? "Medication expires today!"
                    : "Medication expiring soon";

                string message = daysLeft == 0
                    ? $"Your medication \"{um.Medication.Trade_name}\" expires today ({um.ExpiryDate.Value:dd/MM/yyyy}). Please renew your prescription."
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

                _logger.LogInformation(
                    "ExpiryWarning created: UserMed={Id}, DaysLeft={Days}", um.Id, daysLeft);
            }
        }

        // =====================================================================
        // 2. DOSE REMINDER
        // =====================================================================
        private async Task CheckDoseRemindersAsync(AppDbContext db, DateTime now)
        {
            var upcoming = await db.MedicationSchedules
                .Include(s => s.UserMedication)
                    .ThenInclude(um => um.Medication)
                .Where(s =>
                    s.UserMedication.NotificationActive &&
                    !s.ReminderSent &&
                    s.Status == "Pending" &&
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

                // Mark as sent so it does not trigger again
                schedule.ReminderSent = true;
                schedule.NotificationTime = now;

                _logger.LogInformation(
                    "DoseReminder created: Schedule={Id}, User={UserId}", schedule.Id, um.UserId);
            }
        }
    }
}