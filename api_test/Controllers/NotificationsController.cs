using api_test.Data;
using api_test.Models;
using api_test.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace api_test.Controllers
{
    [ApiController]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IInteractionService _interactionService;
        private readonly ITranslationService _translation;

        public NotificationsController(
            AppDbContext db,
            IInteractionService interactionService,
            ITranslationService translation)
        {
            _db = db;
            _interactionService = interactionService;
            _translation = translation;
        }

        /// <summary>
        /// GET /api/users/me/notification-schedules
        /// Returns upcoming Pending schedules with all info Flutter needs
        /// to schedule local (or push) notifications.
        /// notificationTime = scheduledAt - 15 minutes.
        /// </summary>
        [HttpGet("api/users/me/notification-schedules")]
        public async Task<ActionResult<List<NotificationScheduleDto>>> GetNotificationSchedules(
            [FromQuery] string lang = "en")
        {
            var userId = GetUserId();
            var nowUtc = DateTime.UtcNow;

            // Load all future pending schedules for this user
            var schedules = await _db.MedicationSchedules
                .Include(s => s.UserMedication)
                    .ThenInclude(um => um.Medication)
                .Where(s =>
                    s.UserMedication!.UserId == userId &&
                    s.Status == "Pending" &&
                    s.ScheduledAt > nowUtc)
                .OrderBy(s => s.ScheduledAt)
                .ToListAsync();

            // Build a map of medId → hasInteractions to avoid N+1 per schedule
            var userMedIds = schedules
                .Select(s => s.UserMedication!.MedId)
                .Distinct()
                .ToList();

            var interactionMap = new Dictionary<int, bool>();
            foreach (var medId in userMedIds)
            {
                var interactions = await _interactionService
                    .GetInteractionsForUserMedication(userId, medId);
                interactionMap[medId] = interactions.Count > 0;
            }

            var result = schedules.Select(s =>
            {
                var um = s.UserMedication!;

                // Reuse stored NotificationTime if correct; otherwise calculate dynamically
                DateTime notifTime = (s.NotificationTime.HasValue &&
                    s.NotificationTime.Value == s.ScheduledAt.AddMinutes(-15))
                    ? s.NotificationTime.Value
                    : s.ScheduledAt.AddMinutes(-15);

                var translatedName = _translation.GetMedName(um.MedId, lang);
                var medName = string.IsNullOrWhiteSpace(translatedName)
                    ? um.Medication?.Trade_name ?? string.Empty
                    : translatedName;

                return new NotificationScheduleDto
                {
                    ScheduleId = s.Id,
                    UserMedId = um.Id,
                    MedId = um.MedId,
                    MedName = medName,
                    Title = _translation.GetNotificationText(
                        "DoseReminder",
                        lang,
                        "Dose Reminder"),
                    Message = _translation.GetNotificationText(
                        "DoseReminderDueSoonMessage",
                        lang,
                        $"Reminder: your dose of \"{medName}\" is due in 15 minute(s) - {um.Dosage}",
                        medName,
                        "15",
                        um.Dosage ?? string.Empty),
                    ScheduledAt = s.ScheduledAt,
                    NotificationTime = notifTime,
                    Status = s.Status ?? "Pending",
                    PillsPerDose = um.PillsPerDose,
                    CurrentPillCount = um.CurrentPillCount,
                    LowStockThreshold = um.LowStockThreshold,
                    DosageForm = string.IsNullOrWhiteSpace(um.DosageForm)
                        ? um.Medication?.Dosage_Form
                        : um.DosageForm,
                    QuantityUnit = string.IsNullOrWhiteSpace(um.QuantityUnit)
                        ? MedicationQuantityHelper.GetSuggestedUnit(um.DosageForm ?? um.Medication?.Dosage_Form)
                        : um.QuantityUnit,
                    DoseQuantity = MedicationQuantityHelper.ResolveQuantity(um.DoseQuantity, um.PillsPerDose),
                    CurrentQuantity = MedicationQuantityHelper.ResolveQuantity(um.CurrentQuantity, um.CurrentPillCount),
                    HasInteractions = interactionMap.GetValueOrDefault(um.MedId, false)
                };
            }).ToList();

            return Ok(result);
        }

        private int GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)
                ?? throw new UnauthorizedAccessException("NameIdentifier claim missing.");
            return int.Parse(claim.Value);
        }
    }
}
