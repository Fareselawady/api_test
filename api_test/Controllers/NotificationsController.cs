using api_test.Data;
using api_test.Entities;
using api_test.Models;
using api_test.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
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

            // Load all future pending or snoozed schedules for this user
            var schedules = await _db.MedicationSchedules
                .Include(s => s.UserMedication)
                    .ThenInclude(um => um.Medication)
                .Where(s =>
                    s.UserMedication!.UserId == userId &&
                    s.UserMedication.NotificationActive &&
                    (s.Status == MedicationStatus.Pending || s.Status == MedicationStatus.Snoozed) &&
                    (s.Status == MedicationStatus.Snoozed ? s.SnoozedUntil : s.ScheduledAt) > nowUtc)
                .OrderBy(s => s.Status == MedicationStatus.Snoozed ? s.SnoozedUntil : s.ScheduledAt)
                .ToListAsync();

            // Build a map of medicationId → hasInteractions to avoid N+1 per schedule.
            // Custom medications have no ingredient records, so they are skipped.
            var medicationIds = schedules
                .Where(s => UserMedicationFeatureHelper.SupportsInteractions(s.UserMedication))
                .Select(s => s.UserMedication!.MedicationId!.Value)
                .Distinct()
                .ToList();

            var interactionMap = new Dictionary<int, bool>();
            foreach (var medicationId in medicationIds)
            {
                var interactions = await _interactionService
                    .GetInteractionsForUserMedication(userId, medicationId);
                interactionMap[medicationId] = interactions.Any();
            }

            var result = schedules.Select(s =>
            {
                var um = s.UserMedication!;
                var medName = UserMedicationFeatureHelper.GetDisplayName(um, _translation, lang);
                var supportsInteractions = UserMedicationFeatureHelper.SupportsInteractions(um);

                string title;
                string message;
                DateTime notifTime;

                if (s.Status == MedicationStatus.Snoozed)
                {
                    notifTime = s.SnoozedUntil ?? s.ScheduledAt;
                    title = _translation.GetNotificationText(
                        "DoseReminderSnooze",
                        lang,
                        $"Dose Reminder (snooze {s.SnoozeCount}/2)",
                        s.SnoozeCount.ToString(),
                        "2");
                    message = _translation.GetNotificationText(
                        "DoseReminderSnoozeMessage",
                        lang,
                        $"Reminder: take your dose of \"{medName}\" - {um.Dosage}",
                        medName,
                        um.Dosage ?? string.Empty);
                }
                else if (um.AdvanceReminderMinutes.HasValue && um.AdvanceReminderMinutes.Value > 0)
                {
                    var mins = um.AdvanceReminderMinutes.Value;
                    notifTime = s.ScheduledAt.AddMinutes(-mins);
                    title = _translation.GetNotificationText(
                        "AdvanceReminder",
                        lang,
                        "Advance Reminder");
                    message = _translation.GetNotificationText(
                        "AdvanceDoseReminderMessage",
                        lang,
                        $"Reminder: your dose of \"{medName}\" is due in {mins} minute(s) - {um.Dosage}",
                        medName,
                        mins.ToString(),
                        um.Dosage ?? string.Empty);
                }
                else
                {
                    notifTime = s.ScheduledAt;
                    title = _translation.GetNotificationText(
                        "DoseReminderDueNow",
                        lang,
                        "Dose Reminder Due Now");
                    message = _translation.GetNotificationText(
                        "DoseReminderDueNowMessage",
                        lang,
                        $"It's time to take your dose of \"{medName}\" - {um.Dosage}",
                        medName,
                        um.Dosage ?? string.Empty);
                }

                return new NotificationScheduleDto
                {
                    ScheduleId = s.Id,
                    UserMedId = um.Id,
                    MedicationId = um.MedicationId,
                    MedName = medName,
                    MedicationName = medName,
                    IsCustomMedication = um.IsCustomMedication,
                    Title = title,
                    Message = message,
                    ScheduledAt = s.ScheduledAt,
                    NotificationTime = notifTime,
                    Status = s.Status.ToString(),
                    PillsPerDose = um.PillsPerDose,
                    CurrentPillCount = um.CurrentPillCount,
                    LowStockThreshold = um.LowStockThreshold,
                    DosageForm = UserMedicationFeatureHelper.GetDosageForm(um),
                    QuantityUnit = UserMedicationFeatureHelper.GetQuantityUnit(um),
                    DoseQuantity = MedicationQuantityHelper.ResolveQuantity(um.DoseQuantity, um.PillsPerDose),
                    CurrentQuantity = MedicationQuantityHelper.ResolveQuantity(um.CurrentQuantity, um.CurrentPillCount),
                    HasInteractions = supportsInteractions
                        && um.MedicationId.HasValue
                        && interactionMap.GetValueOrDefault(um.MedicationId.Value, false),
                    SupportsInteractions = supportsInteractions,
                    SupportsIngredientWarnings = UserMedicationFeatureHelper.SupportsIngredientWarnings(um),
                    CustomMedicationWarning = UserMedicationFeatureHelper.GetCustomMedicationWarning(um)
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
