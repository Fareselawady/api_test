using api_test.Data;
using api_test.Entities;
using api_test.Models;
using Microsoft.EntityFrameworkCore;

namespace api_test.Services
{
    public class ScheduleService : IScheduleService
    {
        private readonly AppDbContext _context;
        private readonly IInteractionService _interactionService;
        private readonly ITranslationService _translation;

        private static readonly TimeZoneInfo CairoZone =
            TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time");

        private const int MaxSnoozeCount = 2;

        public ScheduleService(
            AppDbContext context,
            IInteractionService interactionService,
            ITranslationService translation)
        {
            _context = context;
            _interactionService = interactionService;
            _translation = translation;
        }

        // =====================================================================
        // GENERATION  (unchanged)
        // =====================================================================

        public async Task RegenerateScheduleAsync(UserMedication userMed)
        {
            var nowUtc = DateTime.UtcNow;

            var futurePending = await _context.MedicationSchedules
                .Where(s => s.UserMedicationId == userMed.Id
                         && s.ScheduledAt > nowUtc
                         && s.Status == "Pending")
                .ToListAsync();

            if (futurePending.Any())
            {
                _context.MedicationSchedules.RemoveRange(futurePending);
                await _context.SaveChangesAsync();
            }

            await GenerateScheduleAsync(userMed, fromNow: true);
        }

        public async Task RegenerateScheduleWithDoseTimesAsync(UserMedication userMed, List<TimeOnly> doseTimes)
        {
            var nowUtc = DateTime.UtcNow;

            var futurePending = await _context.MedicationSchedules
                .Where(s => s.UserMedicationId == userMed.Id
                         && s.ScheduledAt > nowUtc
                         && s.Status == "Pending")
                .ToListAsync();

            if (futurePending.Any())
            {
                _context.MedicationSchedules.RemoveRange(futurePending);
                await _context.SaveChangesAsync();
            }

            await GenerateCustomTimesScheduleAsync(userMed, doseTimes, fromNow: true);
        }

        public async Task GenerateScheduleAsync(UserMedication userMed)
            => await GenerateScheduleAsync(userMed, fromNow: false);

        public async Task GenerateScheduleWithDoseTimesAsync(UserMedication userMed, List<TimeOnly> doseTimes)
            => await GenerateCustomTimesScheduleAsync(userMed, doseTimes, fromNow: false);

        private async Task GenerateScheduleAsync(UserMedication userMed, bool fromNow)
        {
            if (!userMed.StartDate.HasValue || !userMed.FirstDoseTime.HasValue)
                return;

            var schedules = new List<MedicationSchedule>();

            DateTime localStart = userMed.StartDate.Value.ToDateTime(userMed.FirstDoseTime.Value);
            DateTime start = TimeZoneInfo.ConvertTimeToUtc(localStart, CairoZone);
            start = DateTime.SpecifyKind(start, DateTimeKind.Utc);

            if (fromNow && start < DateTime.UtcNow)
                start = DateTime.UtcNow;

            DateTime end = userMed.EndDate.HasValue
                ? userMed.EndDate.Value.ToDateTime(TimeOnly.MinValue).AddDays(1)
                : start.AddYears(1);

            if (end <= start) end = start.AddDays(1);

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

        private async Task GenerateCustomTimesScheduleAsync(
            UserMedication userMed, List<TimeOnly> doseTimes, bool fromNow)
        {
            if (!userMed.StartDate.HasValue) return;

            var schedules = new List<MedicationSchedule>();

            DateOnly startDate = userMed.StartDate.Value;
            DateOnly endDate = userMed.EndDate.HasValue
                ? userMed.EndDate.Value
                : startDate.AddYears(1);

            var nowUtc = DateTime.UtcNow;

            HashSet<DateTime> handledSlots = new();
            if (fromNow)
            {
                var existing = await _context.MedicationSchedules
                    .Where(s => s.UserMedicationId == userMed.Id
                             && (s.Status == "Taken" || s.Status == "Missed"))
                    .Select(s => s.ScheduledAt)
                    .ToListAsync();
                handledSlots = existing.ToHashSet();
            }

            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                foreach (var time in doseTimes)
                {
                    DateTime localDt = date.ToDateTime(time);
                    DateTime utcDt = TimeZoneInfo.ConvertTimeToUtc(localDt, CairoZone);
                    utcDt = DateTime.SpecifyKind(utcDt, DateTimeKind.Utc);

                    if (fromNow && utcDt <= nowUtc) continue;
                    if (handledSlots.Contains(utcDt)) continue;

                    schedules.Add(BuildEntry(userMed.Id, utcDt));
                }
            }

            if (schedules.Count > 0)
            {
                await _context.MedicationSchedules.AddRangeAsync(schedules);
                await _context.SaveChangesAsync();
            }
        }

        // =====================================================================
        // QUERIES  ← lang added
        // =====================================================================

        public async Task<List<MedicationScheduleDto>> GetSchedulesForMedicationAsync(
            int userMedId, int requestingUserId, string lang = "en")
        {
            var medExists = await _context.UserMedications
                .AnyAsync(um => um.Id == userMedId && um.UserId == requestingUserId);

            if (!medExists) return new List<MedicationScheduleDto>();

            var schedules = await _context.MedicationSchedules
                .Include(s => s.UserMedication)
                    .ThenInclude(um => um!.Medication)
                .Where(s => s.UserMedicationId == userMedId)
                .OrderBy(s => s.ScheduledAt)
                .ToListAsync();

            // No interactions for this endpoint — just translate names
            return schedules.Select(s => ToScheduleDto(s, lang)).ToList();
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
                    CreatedAt = a.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    ScheduledAt = a.ScheduledAt.ToString("yyyy-MM-ddTHH:mm:ssZ")
                })
                .ToListAsync();
        }

        public async Task<List<MedicationScheduleDto>> GetTodaySchedulesAsync(
            int userId, string lang = "en")
        {
            var cairoNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, CairoZone);
            var todayStart = TimeZoneInfo.ConvertTimeToUtc(cairoNow.Date, CairoZone);
            var todayEnd = todayStart.AddDays(1);

            var schedules = await _context.MedicationSchedules
                .Include(s => s.UserMedication)
                    .ThenInclude(um => um!.Medication)
                .Where(s => s.UserMedication!.UserId == userId
                         && s.ScheduledAt >= todayStart
                         && s.ScheduledAt < todayEnd)
                .OrderBy(s => s.ScheduledAt)
                .ToListAsync();

            return await BuildDtosWithDayInteractionsAsync(userId, schedules, lang);
        }

        public async Task<List<MedicationScheduleDto>> GetSchedulesByDateAsync(
            int userId, DateOnly date, string lang = "en")
        {
            var localMidnight = date.ToDateTime(TimeOnly.MinValue);
            var utcStart = TimeZoneInfo.ConvertTimeToUtc(localMidnight, CairoZone);
            var utcEnd = utcStart.AddDays(1);

            var schedules = await _context.MedicationSchedules
                .Include(s => s.UserMedication)
                    .ThenInclude(um => um!.Medication)
                .Where(s => s.UserMedication!.UserId == userId
                         && s.ScheduledAt >= utcStart
                         && s.ScheduledAt < utcEnd)
                .OrderBy(s => s.ScheduledAt)
                .ToListAsync();

            return await BuildDtosWithDayInteractionsAsync(userId, schedules, lang);
        }

        // =====================================================================
        // UPDATE STATUS  (unchanged)
        // =====================================================================

        public async Task<bool> UpdateScheduleStatusAsync(
            int scheduleId, string newStatus, int requestingUserId)
        {
            var valid = new[] { "Pending", "Missed" };
            if (!valid.Contains(newStatus))
                throw new ArgumentException(
                    $"Invalid status '{newStatus}'. Use TakeDoseAsync for Taken.");

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
        // TAKE DOSE  (unchanged)
        // =====================================================================

        public async Task<TakeDoseResult> TakeDoseAsync(int scheduleId, int requestingUserId)
        {
            var schedule = await _context.MedicationSchedules
                .Include(s => s.UserMedication)
                    .ThenInclude(um => um.Medication)
                .FirstOrDefaultAsync(s => s.Id == scheduleId
                                       && s.UserMedication!.UserId == requestingUserId);

            if (schedule is null) return TakeDoseResult.NotFound();
            if (schedule.Status == "Taken") return TakeDoseResult.AlreadyTaken();

            var userMed = schedule.UserMedication;
            var now = DateTime.UtcNow;

            schedule.Status = "Taken";
            schedule.TakenAt = now;
            schedule.SnoozedUntil = null;

            var relatedAlerts = await _context.Alerts
                .Where(a => a.MedicationScheduleId == scheduleId && !a.IsRead)
                .ToListAsync();
            relatedAlerts.ForEach(a => a.IsRead = true);

            var dosageForm = string.IsNullOrWhiteSpace(userMed.DosageForm)
                ? userMed.Medication?.Dosage_Form
                : userMed.DosageForm;
            userMed.DosageForm = dosageForm;

            if (string.IsNullOrWhiteSpace(userMed.QuantityUnit))
                userMed.QuantityUnit = MedicationQuantityHelper.GetSuggestedUnit(dosageForm);

            userMed.CurrentQuantity = MedicationQuantityHelper.ResolveQuantity(
                userMed.CurrentQuantity,
                userMed.CurrentPillCount);
            userMed.InitialQuantity = MedicationQuantityHelper.ResolveQuantity(
                userMed.InitialQuantity,
                userMed.InitialPillCount);
            userMed.DoseQuantity = MedicationQuantityHelper.ResolveQuantity(
                userMed.DoseQuantity,
                userMed.PillsPerDose);

            int pillsDeducted = 0;
            decimal quantityDeducted = 0;
            if (userMed.CurrentQuantity.HasValue)
            {
                var quantityToDeduct = userMed.DoseQuantity ?? 1;
                if (quantityToDeduct <= 0) quantityToDeduct = 1;

                quantityDeducted = Math.Min(quantityToDeduct, userMed.CurrentQuantity.Value);
                userMed.CurrentQuantity = Math.Max(0, userMed.CurrentQuantity.Value - quantityToDeduct);
                userMed.CurrentPillCount = MedicationQuantityHelper.ResolveLegacyCount(null, userMed.CurrentQuantity);
                pillsDeducted = MedicationQuantityHelper.ResolveLegacyCount(null, quantityDeducted) ?? 0;
            }

            bool lowStockAlertCreated = false;
            if (userMed.CurrentQuantity.HasValue && userMed.LowStockThreshold.HasValue
                && userMed.CurrentQuantity <= userMed.LowStockThreshold)
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
                                           $"Remaining: {FormatQuantity(userMed.CurrentQuantity)} {userMed.QuantityUnit} " +
                                           $"(threshold: {userMed.LowStockThreshold} {userMed.QuantityUnit}).",
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
                quantityDeducted: quantityDeducted,
                remainingQuantity: userMed.CurrentQuantity,
                quantityUnit: userMed.QuantityUnit,
                lowStockAlert: lowStockAlertCreated
            );
        }

        // =====================================================================
        // SNOOZE  (unchanged)
        // =====================================================================

        public async Task<SnoozeResult> SnoozeAsync(int scheduleId, int requestingUserId)
        {
            var schedule = await _context.MedicationSchedules
                .Include(s => s.UserMedication)
                .FirstOrDefaultAsync(s => s.Id == scheduleId
                                       && s.UserMedication!.UserId == requestingUserId);

            if (schedule is null) return SnoozeResult.NotFound();
            if (schedule.Status == "Taken") return SnoozeResult.AlreadyTaken();
            if (schedule.Status == "Missed") return SnoozeResult.AlreadyMissed();
            if (schedule.Status != "Pending") return SnoozeResult.InvalidStatus(schedule.Status ?? "Unknown");

            int currentSnoozeCount = schedule.SnoozeCount;
            if (currentSnoozeCount >= MaxSnoozeCount)
                return SnoozeResult.LimitReached(scheduleId, currentSnoozeCount);

            DateTime oldScheduledAt = schedule.ScheduledAt;
            DateTime oldNotificationTime = schedule.NotificationTime ?? schedule.ScheduledAt.AddMinutes(-15);

            schedule.ScheduledAt = oldScheduledAt.AddHours(1);
            schedule.NotificationTime = oldNotificationTime.AddHours(1);
            schedule.SnoozeCount = currentSnoozeCount + 1;
            schedule.ReminderSent = false;

            await _context.SaveChangesAsync();

            return SnoozeResult.Success(
                scheduleId: scheduleId,
                snoozeCount: schedule.SnoozeCount,
                oldScheduledAt: oldScheduledAt,
                newScheduledAt: schedule.ScheduledAt,
                oldNotificationTime: oldNotificationTime,
                newNotificationTime: schedule.NotificationTime.Value
            );
        }

        // =====================================================================
        // SKIP  (unchanged)
        // =====================================================================

        public async Task<SkipDoseResult> SkipDoseAsync(int scheduleId, int requestingUserId)
        {
            var schedule = await _context.MedicationSchedules
                .Include(s => s.UserMedication)
                .FirstOrDefaultAsync(s => s.Id == scheduleId
                                       && s.UserMedication!.UserId == requestingUserId);

            if (schedule is null) return SkipDoseResult.NotFound();
            if (schedule.Status == "Taken") return SkipDoseResult.AlreadyTaken();
            if (schedule.Status == "Missed") return SkipDoseResult.AlreadyMissed();
            if (schedule.Status != "Pending") return SkipDoseResult.InvalidStatus(schedule.Status ?? "Unknown");

            schedule.Status = "Missed";
            schedule.ReminderSent = true;

            await _context.SaveChangesAsync();
            return SkipDoseResult.Success(scheduleId);
        }

        // =====================================================================
        // PRIVATE HELPERS
        // =====================================================================

        private async Task<List<MedicationScheduleDto>> BuildDtosWithDayInteractionsAsync(
            int userId,
            List<MedicationSchedule> schedules,
            string lang = "en")
        {
            var baseDtos = schedules.Select(s => ToScheduleDto(s, lang)).ToList();

            if (schedules.Count == 0) return baseDtos;

            try
            {
                var dayMedIds = schedules
                    .Where(s => s.UserMedication != null)
                    .Select(s => s.UserMedication!.MedId)
                    .Distinct()
                    .ToList();

                if (dayMedIds.Count == 0) return baseDtos;

                var dayIngredients = await _context.Med_Ingredients_Link
                    .Where(m => dayMedIds.Contains(m.Med_id))
                    .Select(m => new { m.Med_id, m.Ingredient_id })
                    .ToListAsync();

                var ingredientsByMed = dayIngredients
                    .GroupBy(x => x.Med_id)
                    .ToDictionary(g => g.Key, g => g.Select(x => x.Ingredient_id).ToList());

                var allDayIngredientIds = dayIngredients
                    .Select(x => x.Ingredient_id)
                    .Distinct()
                    .ToList();

                var allInteractions = await _context.Drug_Interactions
                    .Where(di =>
                        allDayIngredientIds.Contains(di.Ingredient_1_id!.Value) ||
                        allDayIngredientIds.Contains(di.Ingredient_2_id!.Value))
                    .Select(di => new
                    {
                        di.Ingredient_1_id,
                        di.Ingredient_2_id,
                        di.Interaction_type
                    })
                    .ToListAsync();

                // medId → translated name (or DB name as fallback)
                var medIdToName = schedules
                    .Where(s => s.UserMedication?.Medication != null)
                    .Select(s => new { s.UserMedication!.MedId, s.UserMedication.Medication.Trade_name })
                    .Distinct()
                    .ToDictionary(
                        x => x.MedId,
                        x =>
                        {
                            var translated = _translation.GetMedName(x.MedId, lang);
                            return string.IsNullOrEmpty(translated)
                                ? (x.Trade_name ?? string.Empty)
                                : translated;
                        });

                var interactionCache = new Dictionary<int, List<MedicationInteractionDto>>();

                foreach (var medId in dayMedIds)
                {
                    var myIngredients = ingredientsByMed.GetValueOrDefault(medId, new List<int>());
                    var result = new List<MedicationInteractionDto>();

                    foreach (var otherMedId in dayMedIds)
                    {
                        if (otherMedId == medId) continue;

                        var otherIngredients = ingredientsByMed.GetValueOrDefault(otherMedId, new List<int>());

                        var matched = allInteractions
                            .Where(di =>
                                (myIngredients.Contains(di.Ingredient_1_id!.Value) &&
                                 otherIngredients.Contains(di.Ingredient_2_id!.Value)) ||
                                (myIngredients.Contains(di.Ingredient_2_id!.Value) &&
                                 otherIngredients.Contains(di.Ingredient_1_id!.Value)))
                            .Select(di => di.Interaction_type)
                            .Where(t => !string.IsNullOrWhiteSpace(t))
                            .ToList();

                        if (matched.Count > 0)
                        {
                            // ↓ Translate reason
                            var rawReason = string.Join(", ", matched);
                            var translatedReason = _translation.GetInteractionReason(rawReason, lang);

                            result.Add(new MedicationInteractionDto
                            {
                                WithMedication = medIdToName.GetValueOrDefault(otherMedId, string.Empty),
                                Reason = translatedReason
                            });
                        }
                    }

                    interactionCache[medId] = result;
                }

                for (int i = 0; i < schedules.Count; i++)
                {
                    var medId = schedules[i].UserMedication?.MedId ?? 0;
                    if (medId != 0 && interactionCache.TryGetValue(medId, out var interactions))
                    {
                        baseDtos[i].Interactions = interactions;
                        baseDtos[i].HasInteractions = interactions.Count > 0;
                    }
                }
            }
            catch (Exception)
            {
                return baseDtos;
            }

            return baseDtos;
        }

        // ── ToScheduleDto  ← ترجمة MedName ───────────────────────────────────
        private MedicationScheduleDto ToScheduleDto(MedicationSchedule s, string lang = "en")
        {
            var dbName = s.UserMedication?.Medication?.Trade_name ?? string.Empty;
            var medId = s.UserMedication?.MedId ?? 0;

            var translated = medId > 0 ? _translation.GetMedName(medId, lang) : string.Empty;
            var finalName = string.IsNullOrEmpty(translated) ? dbName : translated;

            return new MedicationScheduleDto
            {
                Id = s.Id,
                UserMedId = s.UserMedicationId,
                MedId = medId,
                MedName = finalName,
                ScheduledAt = s.ScheduledAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                NotificationTime = s.NotificationTime.HasValue
                    ? s.NotificationTime.Value.ToString("yyyy-MM-ddTHH:mm:ssZ")
                    : string.Empty,
                Status = s.Status ?? string.Empty,
                ReminderSent = s.ReminderSent,
                SnoozeCount = s.SnoozeCount,
                DosageForm = string.IsNullOrWhiteSpace(s.UserMedication?.DosageForm)
                    ? s.UserMedication?.Medication?.Dosage_Form
                    : s.UserMedication?.DosageForm,
                QuantityUnit = string.IsNullOrWhiteSpace(s.UserMedication?.QuantityUnit)
                    ? MedicationQuantityHelper.GetSuggestedUnit(s.UserMedication?.DosageForm ?? s.UserMedication?.Medication?.Dosage_Form)
                    : s.UserMedication?.QuantityUnit,
                DoseQuantity = MedicationQuantityHelper.ResolveQuantity(s.UserMedication?.DoseQuantity, s.UserMedication?.PillsPerDose),
                CurrentQuantity = MedicationQuantityHelper.ResolveQuantity(s.UserMedication?.CurrentQuantity, s.UserMedication?.CurrentPillCount)
            };
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

        private static string FormatQuantity(decimal? quantity)
            => quantity.HasValue
                ? quantity.Value.ToString("0.##")
                : "0";

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
