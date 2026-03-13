using api_test.Data;
using api_test.Models;
using Microsoft.EntityFrameworkCore;

namespace api_test.Services
{
    public class AlertService : IAlertService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<AlertService> _logger;

        public AlertService(AppDbContext db, ILogger<AlertService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<List<AlertDto>> GetAllAlertsAsync(int userId)
        {
            return await _db.Alerts
                .Where(a => a.UserId == userId)
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
                    CreatedAt = a.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    ScheduledAt = a.ScheduledAt.ToString("yyyy-MM-ddTHH:mm:ssZ")
                })
                .ToListAsync();
        }

        public async Task<List<AlertDto>> GetUnreadAlertsAsync(int userId)
        {
            return await _db.Alerts
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
                    CreatedAt = a.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    ScheduledAt = a.ScheduledAt.ToString("yyyy-MM-ddTHH:mm:ssZ")
                })
                .ToListAsync();
        }

        public async Task<int> GetUnreadCountAsync(int userId)
        {
            return await _db.Alerts
                .CountAsync(a => a.UserId == userId && !a.IsRead);
        }

        public async Task<bool> MarkAsReadAsync(int alertId, int userId)
        {
            var alert = await _db.Alerts
                .FirstOrDefaultAsync(a => a.Id == alertId && a.UserId == userId);

            if (alert is null) return false;
            if (alert.IsRead) return true;

            alert.IsRead = true;
            await _db.SaveChangesAsync();
            _logger.LogInformation("Alert {AlertId} marked as read for User {UserId}", alertId, userId);
            return true;
        }

        public async Task<int> MarkAllAsReadAsync(int userId)
        {
            var unread = await _db.Alerts
                .Where(a => a.UserId == userId && !a.IsRead)
                .ToListAsync();

            if (unread.Count == 0) return 0;

            unread.ForEach(a => a.IsRead = true);
            await _db.SaveChangesAsync();
            _logger.LogInformation("{Count} alerts marked as read for User {UserId}", unread.Count, userId);
            return unread.Count;
        }

        public async Task<bool> DeleteAlertAsync(int alertId, int userId)
        {
            var alert = await _db.Alerts
                .FirstOrDefaultAsync(a => a.Id == alertId && a.UserId == userId);

            if (alert is null) return false;

            _db.Alerts.Remove(alert);
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<int> DeleteOldReadAlertsAsync(int daysOld = 30)
        {
            var cutoff = DateTime.UtcNow.AddDays(-daysOld);
            var old = await _db.Alerts
                .Where(a => a.IsRead && a.CreatedAt < cutoff)
                .ToListAsync();

            if (old.Count == 0) return 0;

            _db.Alerts.RemoveRange(old);
            await _db.SaveChangesAsync();
            _logger.LogInformation("Deleted {Count} old alerts (older than {Days} days)", old.Count, daysOld);
            return old.Count;
        }
    }
}