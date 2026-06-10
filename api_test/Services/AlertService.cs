using api_test.Data;
using api_test.Entities;
using api_test.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace api_test.Services
{
    public class AlertService : IAlertService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<AlertService> _logger;
        private readonly ITranslationService _translation;

        public AlertService(
            AppDbContext db,
            ILogger<AlertService> logger,
            ITranslationService translation)
        {
            _db = db;
            _logger = logger;
            _translation = translation;
        }

        public async Task<List<AlertDto>> GetAllAlertsAsync(int userId, string lang = "en")
        {
            var alerts = await GetAlertQuery(userId)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            return alerts.Select(a => ToDto(a, lang)).ToList();
        }

        public async Task<List<AlertDto>> GetUnreadAlertsAsync(int userId, string lang = "en")
        {
            var alerts = await GetAlertQuery(userId)
                .Where(a => !a.IsRead)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            return alerts.Select(a => ToDto(a, lang)).ToList();
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

        private IQueryable<Alert> GetAlertQuery(int userId)
        {
            return _db.Alerts
                .AsNoTracking()
                .Include(a => a.UserMedication)
                    .ThenInclude(um => um!.Medication)
                .Include(a => a.MedicationSchedule)
                .Where(a => a.UserId == userId);
        }

        private AlertDto ToDto(Alert alert, string lang)
        {
            return new AlertDto
            {
                Id = alert.Id,
                UserId = alert.UserId,
                UserMedicationId = alert.UserMedicationId,
                MedicationScheduleId = alert.MedicationScheduleId,
                Type = alert.Type,
                Title = LocalizeTitle(alert, lang),
                Message = LocalizeMessage(alert, lang),
                IsRead = alert.IsRead,
                CreatedAt = alert.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                ScheduledAt = alert.ScheduledAt.ToString("yyyy-MM-ddTHH:mm:ssZ")
            };
        }

        private string? LocalizeTitle(Alert alert, string lang)
        {
            var fallback = alert.Title ?? alert.Type ?? string.Empty;
            var type = alert.Type ?? string.Empty;

            if (type == "DoseReminder")
            {
                var retry = MatchCount(alert.Title, "retry");
                if (retry is not null)
                    return _translation.GetNotificationText(
                        "DoseReminderRetry", lang, fallback, retry.Value.current, retry.Value.max);

                var snooze = MatchCount(alert.Title, "snooze");
                if (snooze is not null)
                    return _translation.GetNotificationText(
                        "DoseReminderSnooze", lang, fallback, snooze.Value.current, snooze.Value.max);
            }

            var key = type switch
            {
                "ExpiryWarning" when alert.Title?.Contains("today", StringComparison.OrdinalIgnoreCase) == true
                    => "ExpiryWarningToday",
                "ExpiryWarning" => "ExpiryWarningSoon",
                "MissedDose" => "MissedDose",
                "LowStock" => "LowStock",
                "AdminReply" => "AdminReply",
                "NewSurvey" => "NewSurvey",
                "AdminMessage" => GetAdminMessageKey(alert.Title),
                _ => type
            };

            if (type == "NewSurvey")
            {
                var surveyTitle = MatchNewSurveyTitle(alert.Title);
                if (surveyTitle is not null)
                    return _translation.GetNotificationText(
                        key,
                        lang,
                        fallback,
                        TranslateSurveyType(surveyTitle.Value.type, lang),
                        surveyTitle.Value.userName);
            }

            return _translation.GetNotificationText(key, lang, fallback);
        }

        private string? LocalizeMessage(Alert alert, string lang)
        {
            var fallback = alert.Message ?? string.Empty;
            var type = alert.Type ?? string.Empty;
            var medName = GetMedicationName(alert, lang);
            var dosage = alert.UserMedication?.Dosage ?? string.Empty;

            switch (type)
            {
                case "DoseReminder":
                    var dueSoon = Regex.Match(
                        fallback,
                        @"due in (?<minutes>\d+) minute",
                        RegexOptions.IgnoreCase);

                    if (dueSoon.Success)
                        return _translation.GetNotificationText(
                            "DoseReminderDueSoonMessage",
                            lang,
                            fallback,
                            medName,
                            dueSoon.Groups["minutes"].Value,
                            dosage);

                    if (fallback.Contains("It's time", StringComparison.OrdinalIgnoreCase))
                        return _translation.GetNotificationText(
                            "DoseReminderDueNowMessage", lang, fallback, medName, dosage);

                    if (alert.Title?.Contains("snooze", StringComparison.OrdinalIgnoreCase) == true)
                        return _translation.GetNotificationText(
                            "DoseReminderSnoozeMessage", lang, fallback, medName, dosage);

                    if (alert.Title?.Contains("retry", StringComparison.OrdinalIgnoreCase) == true)
                        return _translation.GetNotificationText(
                            "DoseReminderRetryMessage", lang, fallback, medName, dosage);

                    return _translation.GetNotificationText(
                        "DoseReminderMessage", lang, fallback, medName, dosage);

                case "MissedDose":
                    return _translation.GetNotificationText(
                        "MissedDoseMessage", lang, fallback, medName);

                case "ExpiryWarning":
                    var effectiveExpiry = alert.UserMedication == null
                        ? null
                        : MedicationExpiryHelper.GetEffectiveExpiryDate(alert.UserMedication);
                    var expiryDate = effectiveExpiry?.ToString("dd/MM/yyyy") ?? string.Empty;
                    var daysLeft = MatchFirstNumber(fallback);
                    var expiryKey = alert.Title?.Contains("today", StringComparison.OrdinalIgnoreCase) == true
                        ? "ExpiryWarningTodayMessage"
                        : "ExpiryWarningSoonMessage";
                    return _translation.GetNotificationText(
                        expiryKey, lang, fallback, medName, daysLeft, expiryDate);

                case "LowStock":
                    var lowStock = MatchLowStock(fallback);
                    return _translation.GetNotificationText(
                        "LowStockMessage",
                        lang,
                        fallback,
                        medName,
                        lowStock.remaining,
                        lowStock.threshold);

                case "AdminMessage":
                    var key = GetAdminMessageKey(alert.Title);
                    if (key == "PremiumActivated")
                        return _translation.GetNotificationText(
                            "PremiumActivatedMessage", lang, fallback, MatchIsoDate(fallback));

                    return _translation.GetNotificationText($"{key}Message", lang, fallback);

                case "NewSurvey":
                    var survey = MatchNewSurveyMessage(fallback);
                    if (survey is not null)
                        return _translation.GetNotificationText(
                            "NewSurveyMessage",
                            lang,
                            fallback,
                            survey.Value.userName,
                            survey.Value.userId,
                            TranslateSurveyType(survey.Value.type, lang),
                            survey.Value.message);
                    return fallback;

                case "AdminReply":
                    return fallback;

                default:
                    return _translation.GetNotificationText($"{type}Message", lang, fallback, medName, dosage);
            }
        }

        private string GetMedicationName(Alert alert, string lang)
        {
            var med = alert.UserMedication?.Medication;
            if (med is null) return string.Empty;

            var translated = _translation.GetMedName(med.ID, lang);
            return string.IsNullOrWhiteSpace(translated)
                ? med.Trade_name ?? string.Empty
                : translated;
        }

        private static string GetAdminMessageKey(string? title)
        {
            return title switch
            {
                "Premium Activated" => "PremiumActivated",
                "Premium Cancelled" => "PremiumCancelled",
                "Support Request Submitted" => "SupportRequestSubmitted",
                "Admin Replied to Your Support Request" => "SupportReply",
                _ => "AdminMessage"
            };
        }

        private string TranslateSurveyType(string type, string lang)
            => _translation.GetNotificationText($"SurveyType_{type}", lang, type);

        private static (string current, string max)? MatchCount(string? text, string word)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            var match = Regex.Match(
                text,
                $@"{word}\s+(?<current>\d+)/(?<max>\d+)",
                RegexOptions.IgnoreCase);

            return match.Success
                ? (match.Groups["current"].Value, match.Groups["max"].Value)
                : null;
        }

        private static string MatchFirstNumber(string text)
        {
            var match = Regex.Match(text, @"\d+");
            return match.Success ? match.Value : string.Empty;
        }

        private static string MatchIsoDate(string text)
        {
            var match = Regex.Match(text, @"\d{4}-\d{2}-\d{2}");
            return match.Success ? match.Value : string.Empty;
        }

        private static (string remaining, string threshold) MatchLowStock(string text)
        {
            var match = Regex.Match(
                text,
                @"Remaining:\s*(?<remaining>\d+).*threshold:\s*(?<threshold>\d+)",
                RegexOptions.IgnoreCase);

            return match.Success
                ? (match.Groups["remaining"].Value, match.Groups["threshold"].Value)
                : (string.Empty, string.Empty);
        }

        private static (string type, string userName)? MatchNewSurveyTitle(string? title)
        {
            if (string.IsNullOrWhiteSpace(title)) return null;

            var match = Regex.Match(title, @"^New (?<type>.+) from (?<user>.+)$");
            return match.Success
                ? (match.Groups["type"].Value, match.Groups["user"].Value)
                : null;
        }

        private static (string userName, string userId, string type, string message)? MatchNewSurveyMessage(string text)
        {
            var match = Regex.Match(
                text,
                "^User \"(?<user>.*)\" \\(ID: (?<id>\\d+)\\) submitted a (?<type>[^:]+): (?<message>.*)$");

            return match.Success
                ? (
                    match.Groups["user"].Value,
                    match.Groups["id"].Value,
                    match.Groups["type"].Value,
                    match.Groups["message"].Value)
                : null;
        }
    }
}
