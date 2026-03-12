using api_test.Data;
using api_test.Entities;
using Microsoft.EntityFrameworkCore;

namespace api_test.Services
{
    public class AlertService
    {
        private readonly AppDbContext _context;

        public AlertService(AppDbContext context)
        {
            _context = context;
        }

        // Generate reminder alerts for a schedule
        public async Task GenerateScheduleReminderAsync(MedicationSchedule schedule)
        {
            if (schedule.ReminderSent) return;

            if (schedule.NotificationTime.HasValue && DateTime.UtcNow >= schedule.NotificationTime.Value)
            {
                var alert = new Alert
                {
                    UserId = schedule.UserMedication.UserId,
                    UserMedicationId = schedule.UserMedicationId,
                    MedicationScheduleId = schedule.Id,
                    Type = "Reminder",
                    Title = $"Time for {schedule.UserMedication.Medication.Trade_name}",
                    Message = $"Scheduled dose at {schedule.ScheduledAt:HH:mm}",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                };

                await _context.Alerts.AddAsync(alert);
                schedule.ReminderSent = true;
                await _context.SaveChangesAsync();
            }
        }

        // Check for low stock
        public async Task CheckLowStockAsync(UserMedication userMed)
        {
            if (userMed.CurrentPillCount.HasValue &&
                userMed.LowStockThreshold.HasValue &&
                userMed.CurrentPillCount <= userMed.LowStockThreshold)
            {
                var exists = await _context.Alerts.AnyAsync(a =>
                    a.UserMedicationId == userMed.Id && a.Type == "LowStock" && !a.IsRead);

                if (!exists)
                {
                    var alert = new Alert
                    {
                        UserId = userMed.UserId,
                        UserMedicationId = userMed.Id,
                        Type = "LowStock",
                        Title = $"{userMed.Medication.Trade_name} is running low",
                        Message = $"Remaining quantity: {userMed.CurrentPillCount}",
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow
                    };

                    await _context.Alerts.AddAsync(alert);
                    await _context.SaveChangesAsync();
                }
            }
        }

        // Check for upcoming expiry
        public async Task CheckExpiryAsync(UserMedication userMed)
        {
            if (userMed.ExpiryDate.HasValue)
            {
                var daysLeft = (userMed.ExpiryDate.Value.ToDateTime(TimeOnly.MinValue) - DateTime.UtcNow).TotalDays;
                if (daysLeft <= 7)
                {
                    var exists = await _context.Alerts.AnyAsync(a =>
                        a.UserMedicationId == userMed.Id && a.Type == "Expiry" && !a.IsRead);

                    if (!exists)
                    {
                        var alert = new Alert
                        {
                            UserId = userMed.UserId,
                            UserMedicationId = userMed.Id,
                            Type = "Expiry",
                            Title = $"{userMed.Medication.Trade_name} is about to expire",
                            Message = $"Expiry date: {userMed.ExpiryDate.Value:yyyy-MM-dd}",
                            IsRead = false,
                            CreatedAt = DateTime.UtcNow
                        };

                        await _context.Alerts.AddAsync(alert);
                        await _context.SaveChangesAsync();
                    }
                }
            }
        }

        // Mark alert as read
        public async Task MarkAlertAsReadAsync(int alertId)
        {
            var alert = await _context.Alerts.FindAsync(alertId);
            if (alert == null) return;

            alert.IsRead = true;
            await _context.SaveChangesAsync();
        }

        // Get pending alerts for a user
        public async Task<List<Alert>> GetPendingAlertsAsync(int userId)
        {
            return await _context.Alerts
                .Where(a => a.UserId == userId && !a.IsRead)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();
        }
    }
}