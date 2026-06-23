using api_test.Data;
using api_test.Entities;
using api_test.Models;
using api_test.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace api_test.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UserMedicationsController : ControllerBase
    {
        private readonly ITranslationService _translationService;
        private readonly AppDbContext _context;
        private readonly IScheduleService _scheduleService;
        private readonly IInteractionService _interactionService;

        public UserMedicationsController(
            AppDbContext context,
            IScheduleService scheduleService,
            IInteractionService interactionService,
            ITranslationService translationService)
        {
            _context = context;
            _scheduleService = scheduleService;
            _interactionService = interactionService;
            _translationService = translationService;
        }
        // ================= CREATE (single-step) =================
        [HttpPost]
        public async Task<ActionResult> AddUserMedication(CreateUserMedicationDto dto, [FromQuery] string lang = "en")
        {
            var userId = GetUserId();

            // ── Basic validation ──────────────────────────────────────────────
            if (dto.CurrentPillCount.HasValue && dto.CurrentPillCount < 0)
                return BadRequest(new { Message = "currentPillCount must not be negative." });

            if (dto.InitialPillCount.HasValue && dto.InitialPillCount < 0)
                return BadRequest(new { Message = "initialPillCount must not be negative." });

            if (dto.LowStockThreshold.HasValue && dto.LowStockThreshold < 0)
                return BadRequest(new { Message = "lowStockThreshold must not be negative." });

            if (dto.IntervalHours.HasValue && dto.IntervalHours <= 0)
                return BadRequest(new { Message = "intervalHours must be greater than 0." });

            if (dto.DosesPerPeriod.HasValue && dto.DosesPerPeriod <= 0)
                return BadRequest(new { Message = "dosesPerPeriod must be greater than 0." });

            if (dto.PillsPerDose.HasValue && dto.PillsPerDose <= 0)
                return BadRequest(new { Message = "pillsPerDose must be greater than 0." });

            if (MedicationQuantityHelper.HasInvalidQuantity(dto.InitialQuantity))
                return BadRequest(new { Message = "initialQuantity must not be negative." });

            if (MedicationQuantityHelper.HasInvalidQuantity(dto.CurrentQuantity))
                return BadRequest(new { Message = "currentQuantity must not be negative." });

            if (MedicationQuantityHelper.HasInvalidDoseQuantity(dto.DoseQuantity))
                return BadRequest(new { Message = "doseQuantity must be greater than 0." });

            var featureError = ValidateMedicationFeatureInputs(
                dto.MedicationUseType,
                dto.MaxDosesPerDay,
                dto.MinimumHoursBetweenDoses,
                dto.RefillReminderDaysBefore);
            if (featureError != null)
                return BadRequest(new { Message = featureError });

            // ── Resolve and validate custom dose times ────────────────────────
            var resolvedDoseTimes = ResolveDoseTimes(dto.DoseTimes, out string? doseTimeError);
            if (doseTimeError != null)
                return BadRequest(new { Message = doseTimeError });

            bool hasCustomTimes = resolvedDoseTimes != null && resolvedDoseTimes.Count > 0;
            if (hasCustomTimes)
            {
                if (dto.IntervalHours.HasValue)
                    return BadRequest(new { Message = "intervalHours must not be used together with doseTimes." });

                // Compatibility: auto-fill firstDoseTime / dosesPerPeriod
                if (!dto.FirstDoseTime.HasValue)
                    dto.FirstDoseTime = resolvedDoseTimes[0].ToTimeSpan();

                if (!dto.DosesPerPeriod.HasValue)
                    dto.DosesPerPeriod = resolvedDoseTimes.Count;

                if (string.IsNullOrWhiteSpace(dto.PeriodUnit))
                    dto.PeriodUnit = "Day";

                if (!dto.PeriodValue.HasValue)
                    dto.PeriodValue = 1;
            }

            // ── Resolve database/custom medication ───────────────────────────
            var resolvedMedication = await ResolveMedicationAsync(
                dto.MedicationId,
                dto.MedicationName,
                dto.IsCustomMedication);

            if (resolvedMedication.Error != null)
                return BadRequest(new { Message = resolvedMedication.Error });

            var medication = resolvedMedication.Medication;
            var medicationName = resolvedMedication.MedicationName;
            var isCustomMedication = resolvedMedication.IsCustomMedication;

            var alreadyExists = await UserMedicationAlreadyExistsAsync(
                userId,
                medication?.ID,
                medicationName,
                ignoreUserMedicationId: null);

            if (alreadyExists)
                return BadRequest(new { Message = $"You already have '{medicationName}' in your medications." });

            var dosageForm = isCustomMedication
                ? dto.DosageForm
                : (string.IsNullOrWhiteSpace(medication?.Dosage_Form) ? dto.DosageForm : medication.Dosage_Form);

            var unitError = MedicationQuantityHelper.ValidateUnit(dosageForm, dto.QuantityUnit);
            if (unitError != null)
                return BadRequest(new { Message = unitError });

            var quantityUnit = MedicationQuantityHelper.ResolveUnit(dosageForm, dto.QuantityUnit);
            var initialQuantity = MedicationQuantityHelper.ResolveQuantity(dto.InitialQuantity, dto.InitialPillCount);
            var currentQuantity = MedicationQuantityHelper.ResolveQuantity(dto.CurrentQuantity, dto.CurrentPillCount);
            var doseQuantity = MedicationQuantityHelper.ResolveQuantity(dto.DoseQuantity, dto.PillsPerDose);

            var now = DateTime.UtcNow;
            var afterOpeningError = MedicationExpiryHelper.ValidateAfterOpeningInput(
                dto.IsOpened,
                dto.OpenedDate,
                dto.AfterOpeningDurationValue,
                dto.AfterOpeningDurationUnit,
                now);
            if (afterOpeningError != null)
                return BadRequest(new { Message = afterOpeningError });

            if (dto.AdvanceReminderMinutes.HasValue)
            {
                var val = dto.AdvanceReminderMinutes.Value;
                if (val < 0 || val > 1440)
                {
                    return BadRequest(new { Message = "Advance reminder minutes must be between 1 and 1440 (or 0 to disable)." });
                }
            }

            var packageExpiry = dto.ExpiryDate.HasValue
                ? DateOnly.FromDateTime(dto.ExpiryDate.Value) : (DateOnly?)null;

            // ── Build entity ──────────────────────────────────────────────────
            var userMed = new UserMedication
            {
                UserId = userId,
                MedicationId = medication?.ID,
                MedicationName = medicationName,
                IsCustomMedication = isCustomMedication,
                Dosage = dto.Dosage,
                Notes = dto.Notes,
                StartDate = dto.StartDate.HasValue ? DateOnly.FromDateTime(dto.StartDate.Value) : null,
                EndDate = dto.EndDate.HasValue ? DateOnly.FromDateTime(dto.EndDate.Value) : null,
                ExpiryDate = packageExpiry,
                IsOpened = dto.IsOpened,
                OpenedDate = dto.OpenedDate?.Date,
                AfterOpeningDurationValue = dto.AfterOpeningDurationValue,
                AfterOpeningDurationUnit = dto.AfterOpeningDurationUnit,
                CurrentPillCount = MedicationQuantityHelper.ResolveLegacyCount(dto.CurrentPillCount, currentQuantity),
                InitialPillCount = MedicationQuantityHelper.ResolveLegacyCount(dto.InitialPillCount, initialQuantity),
                LowStockThreshold = dto.LowStockThreshold,
                PillsPerDose = MedicationQuantityHelper.ResolveLegacyCount(dto.PillsPerDose, doseQuantity),
                InitialQuantity = initialQuantity,
                CurrentQuantity = currentQuantity,
                DoseQuantity = doseQuantity,
                QuantityUnit = quantityUnit,
                DosageForm = dosageForm,
                DosesPerPeriod = dto.DosesPerPeriod,
                PeriodUnit = dto.PeriodUnit,
                PeriodValue = dto.PeriodValue,
                FirstDoseTime = dto.FirstDoseTime.HasValue ? TimeOnly.FromTimeSpan(dto.FirstDoseTime.Value) : null,
                IntervalHours = hasCustomTimes ? null : dto.IntervalHours,
                MedicationUseType = NormalizeMedicationUseType(dto.MedicationUseType),
                MaxDosesPerDay = dto.MaxDosesPerDay,
                MinimumHoursBetweenDoses = dto.MinimumHoursBetweenDoses,
                RefillReminderDaysBefore = dto.RefillReminderDaysBefore,
                NotificationActive = dto.NotificationActive,
                AdvanceReminderMinutes = dto.AdvanceReminderMinutes == 0 ? null : dto.AdvanceReminderMinutes
            };

            MedicationExpiryHelper.Apply(userMed, medication, now);

            _context.UserMedications.Add(userMed);
            await _context.SaveChangesAsync();

            if (hasCustomTimes && resolvedDoseTimes != null)
                await _scheduleService.GenerateScheduleWithDoseTimesAsync(userMed, resolvedDoseTimes);
            else
                await _scheduleService.GenerateScheduleAsync(userMed);

            var warnings = UserMedicationFeatureHelper.SupportsInteractions(userMed)
                ? await _interactionService.CheckInteractionsForNewMedWithLangAsync(userId, medicationName, lang)
                : new List<InteractionWarningDto>();

            return Ok(new
            {
                Message = $"'{medicationName}' added to your medications successfully.",
                userMed.Id,
                userMed.MedicationId,
                userMed.MedicationName,
                userMed.IsCustomMedication,
                SupportsInteractions = UserMedicationFeatureHelper.SupportsInteractions(userMed),
                SupportsIngredientWarnings = UserMedicationFeatureHelper.SupportsIngredientWarnings(userMed),
                CustomMedicationWarning = UserMedicationFeatureHelper.GetCustomMedicationWarning(userMed),
                ExpiryDate = userMed.ExpiryDate,
                userMed.IsOpened,
                userMed.OpenedDate,
                userMed.AfterOpeningDurationValue,
                userMed.AfterOpeningDurationUnit,
                userMed.AfterOpeningExpiryDate,
                userMed.EffectiveExpiryDate,
                userMed.ExpiryReason,
                userMed.AfterOpeningSource,
                AfterOpeningWarning = MedicationExpiryHelper.GetWarning(userMed),
                DosageForm = userMed.DosageForm,
                QuantityUnit = userMed.QuantityUnit,
                InitialQuantity = userMed.InitialQuantity,
                CurrentQuantity = userMed.CurrentQuantity,
                DoseQuantity = userMed.DoseQuantity,
                LowStockThreshold = userMed.LowStockThreshold,
                userMed.MedicationUseType,
                userMed.MaxDosesPerDay,
                userMed.MinimumHoursBetweenDoses,
                userMed.RefillReminderDaysBefore,
                InteractionWarnings = warnings.Count > 0
    ? warnings.Select(w => w.Reason).ToList()
    : null
            });
        }

        // ================= READ =================
        [HttpGet("myusermeds")]
        public async Task<ActionResult> GetMyUserMedications([FromQuery] string lang = "en")
        {
            var userId = GetUserId();

            var userMeds = await _context.UserMedications
                .Include(um => um.Medication)
                .Where(um => um.UserId == userId)
                .ToListAsync();

            var userMedIds = userMeds.Select(um => um.Id).ToList();
            var nowUtc = DateTime.UtcNow;

            var rawScheduleRows = await _context.MedicationSchedules
                .Where(s => userMedIds.Contains(s.UserMedicationId)
                         && s.Status == MedicationStatus.Pending
                         && s.ScheduledAt > nowUtc)
                .Select(s => new { s.UserMedicationId, s.ScheduledAt })
                .ToListAsync();

            var scheduleTimeMap = rawScheduleRows
                .GroupBy(s => s.UserMedicationId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(s => TimeZoneInfo.ConvertTimeFromUtc(s.ScheduledAt, CairoZone).ToString("HH:mm:ss"))
                          .Distinct()
                          .ToList()
                );

            var result = new List<UserMedicationDto>();

            foreach (var um in userMeds)
            {
                var rawInteractions = UserMedicationFeatureHelper.SupportsInteractions(um)
                    ? await _interactionService.GetInteractionsForUserMedication(userId, um.MedicationId!.Value)
                    : new List<MedicationInteractionDto>();

                var interactions = rawInteractions.Select(i =>
                {
                    string translatedMedication =
    string.IsNullOrWhiteSpace(i.WithMedication)
        ? string.Empty
        : (
            _context.Medications
                .Where(m => m.Trade_name == i.WithMedication)
                .Select(m => new { m.ID, m.Trade_name })
                .FirstOrDefault() is { } med
            ? (_translationService.GetMedName(med.ID, lang) is { Length: > 0 } tr
                ? tr
                : med.Trade_name ?? i.WithMedication)
            : i.WithMedication
          );

                    return new MedicationInteractionDto
                    {
                        WithMedication = translatedMedication,

                        Reason = _translationService.GetInteractionReason(
                            i.Reason ?? string.Empty,
                            lang)
                    };
                }).ToList();

                string finalName = UserMedicationFeatureHelper.GetDisplayName(um, _translationService, lang);

                string? scheduleType = InferScheduleType(um);
                var forecast = MedicationRefillForecastHelper.BuildForecast(um, DateTime.UtcNow);

                List<string> doseTimes;
                if (scheduleType == "CustomTimes" && scheduleTimeMap.TryGetValue(um.Id, out var times))
                {
                    doseTimes = times
                        .OrderBy(t => t)
                        .ToList();
                }
                else
                {
                    doseTimes = new List<string>();
                }

                result.Add(new UserMedicationDto
                {
                    Id = um.Id,
                    MedicationId = um.MedicationId,
                    MedicationName = finalName,
                    IsCustomMedication = um.IsCustomMedication,
                    SupportsInteractions = UserMedicationFeatureHelper.SupportsInteractions(um),
                    SupportsIngredientWarnings = UserMedicationFeatureHelper.SupportsIngredientWarnings(um),
                    CustomMedicationWarning = UserMedicationFeatureHelper.GetCustomMedicationWarning(um),
                    Dosage = um.Dosage,
                    Notes = um.Notes,
                    StartDate = um.StartDate?.ToDateTime(TimeOnly.MinValue),
                    EndDate = um.EndDate?.ToDateTime(TimeOnly.MinValue),
                    ExpiryDate = um.ExpiryDate?.ToDateTime(TimeOnly.MinValue),
                    IsOpened = um.IsOpened,
                    OpenedDate = um.OpenedDate,
                    AfterOpeningDurationValue = um.AfterOpeningDurationValue,
                    AfterOpeningDurationUnit = um.AfterOpeningDurationUnit,
                    AfterOpeningExpiryDate = um.AfterOpeningExpiryDate,
                    EffectiveExpiryDate = um.EffectiveExpiryDate,
                    ExpiryReason = um.ExpiryReason,
                    AfterOpeningSource = um.AfterOpeningSource,
                    AfterOpeningWarning = MedicationExpiryHelper.GetWarning(um),
                    CurrentPillCount = um.CurrentPillCount,
                    InitialPillCount = um.InitialPillCount,
                    LowStockThreshold = um.LowStockThreshold,
                    PillsPerDose = um.PillsPerDose,
                    DosageForm = UserMedicationFeatureHelper.GetDosageForm(um),
                    QuantityUnit = UserMedicationFeatureHelper.GetQuantityUnit(um),
                    InitialQuantity = MedicationQuantityHelper.ResolveQuantity(um.InitialQuantity, um.InitialPillCount),
                    CurrentQuantity = MedicationQuantityHelper.ResolveQuantity(um.CurrentQuantity, um.CurrentPillCount),
                    DoseQuantity = MedicationQuantityHelper.ResolveQuantity(um.DoseQuantity, um.PillsPerDose),
                    DosesPerPeriod = um.DosesPerPeriod,
                    PeriodUnit = um.PeriodUnit,
                    PeriodValue = um.PeriodValue,
                    FirstDoseTime = um.FirstDoseTime?.ToTimeSpan(),
                    IntervalHours = um.IntervalHours,
                    MedicationUseType = NormalizeMedicationUseType(um.MedicationUseType),
                    MaxDosesPerDay = um.MaxDosesPerDay,
                    MinimumHoursBetweenDoses = um.MinimumHoursBetweenDoses,
                    RefillReminderDaysBefore = um.RefillReminderDaysBefore,
                    LastRefillDate = um.LastRefillDate,
                    LastRefillQuantity = um.LastRefillQuantity,
                    EstimatedRunOutDate = forecast.EstimatedRunOutDate,
                    DaysUntilEmpty = forecast.DaysUntilEmpty,
                    DosesRemaining = forecast.DosesRemaining,
                    RefillWarning = forecast.RefillWarning,
                    NotificationActive = um.NotificationActive,
                    AdvanceReminderMinutes = um.AdvanceReminderMinutes,
                    ScheduleType = scheduleType,
                    DoseTimes = doseTimes,
                    Interactions = interactions
                });
            }

            return Ok(result);
        }

        // ================= UPDATE =================
        [HttpPut("{id}")]
        public async Task<ActionResult> UpdateUserMedication(int id, UpdateUserMedicationDto dto)
        {
            var userId = GetUserId();

            var userMed = await _context.UserMedications
                .Include(um => um.Medication)
                .FirstOrDefaultAsync(um => um.Id == id && um.UserId == userId);

            if (userMed == null)
                return NotFound(new { Message = "UserMedication not found." });

            var now = DateTime.UtcNow;

            // ── Validate basic fields ─────────────────────────────────────────
            if (dto.CurrentPillCount.HasValue && dto.CurrentPillCount < 0)
                return BadRequest(new { Message = "currentPillCount must not be negative." });
            if (dto.InitialPillCount.HasValue && dto.InitialPillCount < 0)
                return BadRequest(new { Message = "initialPillCount must not be negative." });
            if (dto.LowStockThreshold.HasValue && dto.LowStockThreshold < 0)
                return BadRequest(new { Message = "lowStockThreshold must not be negative." });
            if (dto.PillsPerDose.HasValue && dto.PillsPerDose <= 0)
                return BadRequest(new { Message = "pillsPerDose must be greater than 0." });
            if (MedicationQuantityHelper.HasInvalidQuantity(dto.InitialQuantity))
                return BadRequest(new { Message = "initialQuantity must not be negative." });
            if (MedicationQuantityHelper.HasInvalidQuantity(dto.CurrentQuantity))
                return BadRequest(new { Message = "currentQuantity must not be negative." });
            if (MedicationQuantityHelper.HasInvalidDoseQuantity(dto.DoseQuantity))
                return BadRequest(new { Message = "doseQuantity must be greater than 0." });

            var featureError = ValidateMedicationFeatureInputs(
                dto.MedicationUseType,
                dto.MaxDosesPerDay,
                dto.MinimumHoursBetweenDoses,
                dto.RefillReminderDaysBefore);
            if (featureError != null)
                return BadRequest(new { Message = featureError });

            var nextIsOpened = dto.IsOpened ?? userMed.IsOpened;
            var afterOpeningError = MedicationExpiryHelper.ValidateAfterOpeningInput(
                nextIsOpened,
                dto.OpenedDate,
                dto.AfterOpeningDurationValue,
                dto.AfterOpeningDurationUnit,
                now);
            if (afterOpeningError != null)
                return BadRequest(new { Message = afterOpeningError });

            if (dto.MedicationId.HasValue || dto.MedicationName != null || dto.IsCustomMedication.HasValue)
            {
                var resolvedMedication = await ResolveMedicationAsync(
                    dto.MedicationId,
                    dto.MedicationName ?? userMed.MedicationName,
                    dto.IsCustomMedication ?? userMed.IsCustomMedication);

                if (resolvedMedication.Error != null)
                    return BadRequest(new { Message = resolvedMedication.Error });

                var alreadyExists = await UserMedicationAlreadyExistsAsync(
                    userId,
                    resolvedMedication.Medication?.ID,
                    resolvedMedication.MedicationName,
                    id);

                if (alreadyExists)
                    return BadRequest(new { Message = $"You already have '{resolvedMedication.MedicationName}' in your medications." });

                userMed.MedicationId = resolvedMedication.Medication?.ID;
                userMed.Medication = resolvedMedication.Medication;
                userMed.MedicationName = resolvedMedication.MedicationName;
                userMed.IsCustomMedication = resolvedMedication.IsCustomMedication;
            }

            var effectiveDosageForm = userMed.IsCustomMedication
                ? (dto.DosageForm ?? userMed.DosageForm)
                : (string.IsNullOrWhiteSpace(userMed.Medication?.Dosage_Form)
                    ? (dto.DosageForm ?? userMed.DosageForm)
                    : userMed.Medication.Dosage_Form);
            var unitError = MedicationQuantityHelper.ValidateUnit(effectiveDosageForm, dto.QuantityUnit);
            if (unitError != null)
                return BadRequest(new { Message = unitError });
            // ── Resolve and validate doseTimes ────────────────────────────────
            var resolvedDoseTimes = ResolveDoseTimes(dto.DoseTimes, out string? doseTimeError);
            if (doseTimeError != null)
                return BadRequest(new { Message = doseTimeError });

            // ── Determine effective schedule type ─────────────────────────────
            bool hasCustomTimes = resolvedDoseTimes != null && resolvedDoseTimes.Count > 0;

            // Infer from scheduleType hint or field presence
            string? effectiveScheduleType = dto.ScheduleType;
            if (string.IsNullOrWhiteSpace(effectiveScheduleType))
            {
                if (hasCustomTimes)
                    effectiveScheduleType = "CustomTimes";
                else if (dto.IntervalHours.HasValue)
                    effectiveScheduleType = "Interval";
            }

            // ── Validate per schedule type ────────────────────────────────────
            if (effectiveScheduleType == "Interval")
            {
                if (dto.IntervalHours.HasValue && dto.IntervalHours <= 0)
                    return BadRequest(new { Message = "intervalHours must be greater than 0." });
            }
            else if (effectiveScheduleType == "CustomTimes")
            {
                if (!hasCustomTimes)
                    return BadRequest(new { Message = "doseTimes must contain at least one valid time for CustomTimes schedule." });
                if (dto.IntervalHours.HasValue)
                    return BadRequest(new { Message = "intervalHours must not be used together with doseTimes." });
            }

            // ── Apply non-schedule fields ─────────────────────────────────────
            if (dto.Dosage != null) userMed.Dosage = dto.Dosage;
            if (dto.Notes != null) userMed.Notes = dto.Notes;
            if (dto.StartDate.HasValue) userMed.StartDate = DateOnly.FromDateTime(dto.StartDate.Value);
            if (dto.EndDate.HasValue) userMed.EndDate = DateOnly.FromDateTime(dto.EndDate.Value);
            if (dto.CurrentPillCount.HasValue) userMed.CurrentPillCount = dto.CurrentPillCount;
            if (dto.InitialPillCount.HasValue) userMed.InitialPillCount = dto.InitialPillCount;
            if (dto.LowStockThreshold.HasValue) userMed.LowStockThreshold = dto.LowStockThreshold;
            if (dto.AdvanceReminderMinutes.HasValue)
            {
                var val = dto.AdvanceReminderMinutes.Value;
                if (val < 0 || val > 1440)
                {
                    return BadRequest(new { Message = "Advance reminder minutes must be between 1 and 1440 (or 0 to disable)." });
                }
            }

            if (dto.NotificationActive.HasValue) userMed.NotificationActive = dto.NotificationActive.Value;
            if (dto.AdvanceReminderMinutes.HasValue)
            {
                userMed.AdvanceReminderMinutes = dto.AdvanceReminderMinutes.Value == 0 ? null : dto.AdvanceReminderMinutes;
            }
            if (dto.IsOpened.HasValue) userMed.IsOpened = dto.IsOpened.Value;
            if (dto.OpenedDate.HasValue) userMed.OpenedDate = dto.OpenedDate.Value.Date;
            if (dto.AfterOpeningDurationValue.HasValue)
            {
                userMed.AfterOpeningDurationValue = dto.AfterOpeningDurationValue;
                userMed.AfterOpeningSource = null;
            }
            if (dto.AfterOpeningDurationUnit != null)
            {
                userMed.AfterOpeningDurationUnit = dto.AfterOpeningDurationUnit;
                userMed.AfterOpeningSource = null;
            }

            // PillsPerDose: update only when explicitly provided; preserve existing value when null
            if (dto.PillsPerDose.HasValue) userMed.PillsPerDose = dto.PillsPerDose;
            if (dto.InitialQuantity.HasValue) userMed.InitialQuantity = dto.InitialQuantity;
            if (dto.CurrentQuantity.HasValue) userMed.CurrentQuantity = dto.CurrentQuantity;
            if (dto.DoseQuantity.HasValue) userMed.DoseQuantity = dto.DoseQuantity;
            if (dto.QuantityUnit != null) userMed.QuantityUnit = MedicationQuantityHelper.ResolveUnit(effectiveDosageForm, dto.QuantityUnit);
            if (dto.MedicationUseType != null) userMed.MedicationUseType = NormalizeMedicationUseType(dto.MedicationUseType);
            if (dto.MaxDosesPerDay.HasValue) userMed.MaxDosesPerDay = dto.MaxDosesPerDay;
            if (dto.MinimumHoursBetweenDoses.HasValue) userMed.MinimumHoursBetweenDoses = dto.MinimumHoursBetweenDoses;
            if (dto.RefillReminderDaysBefore.HasValue) userMed.RefillReminderDaysBefore = dto.RefillReminderDaysBefore;

            if (dto.DosageForm != null || userMed.IsCustomMedication || string.IsNullOrWhiteSpace(userMed.DosageForm))
                userMed.DosageForm = effectiveDosageForm;
            if (string.IsNullOrWhiteSpace(userMed.QuantityUnit))
                userMed.QuantityUnit = MedicationQuantityHelper.ResolveUnit(effectiveDosageForm, null);
            userMed.InitialQuantity = MedicationQuantityHelper.ResolveQuantity(userMed.InitialQuantity, userMed.InitialPillCount);
            userMed.CurrentQuantity = MedicationQuantityHelper.ResolveQuantity(userMed.CurrentQuantity, userMed.CurrentPillCount);
            userMed.DoseQuantity = MedicationQuantityHelper.ResolveQuantity(userMed.DoseQuantity, userMed.PillsPerDose);
            userMed.InitialPillCount = MedicationQuantityHelper.ResolveLegacyCount(userMed.InitialPillCount, userMed.InitialQuantity);
            userMed.CurrentPillCount = MedicationQuantityHelper.ResolveLegacyCount(userMed.CurrentPillCount, userMed.CurrentQuantity);
            userMed.PillsPerDose = MedicationQuantityHelper.ResolveLegacyCount(userMed.PillsPerDose, userMed.DoseQuantity);

            // ── Apply schedule fields ─────────────────────────────────────────
            bool scheduleChanged = false;

            // Compare StartDate / EndDate against stored values — only flag changed if the value actually differs.
            if (dto.StartDate.HasValue)
            {
                var newStart = DateOnly.FromDateTime(dto.StartDate.Value);
                if (userMed.StartDate != newStart)
                    scheduleChanged = true;
            }
            if (dto.EndDate.HasValue)
            {
                var newEnd = DateOnly.FromDateTime(dto.EndDate.Value);
                if (userMed.EndDate != newEnd)
                    scheduleChanged = true;
            }

            if (effectiveScheduleType == "CustomTimes" && hasCustomTimes)
            {
                // Compare incoming doseTimes with the times currently stored in the entity.
                // The entity stores FirstDoseTime; we compare sorted resolved list vs existing schedules
                // by checking DosesPerPeriod count and FirstDoseTime as a lightweight proxy.
                // For a precise comparison we compare the sorted TimeOnly list to what was in the DB.
                var newFirst = resolvedDoseTimes![0];
                var newCount = resolvedDoseTimes.Count;
                if (userMed.FirstDoseTime != newFirst
                    || userMed.DosesPerPeriod != newCount
                    || userMed.PeriodUnit != "Day"
                    || userMed.PeriodValue != 1
                    || userMed.IntervalHours.HasValue)
                {
                    scheduleChanged = true;
                }

                // Derive scheduling fields from doseTimes
                userMed.FirstDoseTime = newFirst;
                userMed.DosesPerPeriod = newCount;
                userMed.PeriodUnit = "Day";
                userMed.PeriodValue = 1;
                userMed.IntervalHours = null;
            }
            else if (effectiveScheduleType == "Interval")
            {
                if (dto.IntervalHours.HasValue)
                {
                    if (userMed.IntervalHours != dto.IntervalHours)
                        scheduleChanged = true;
                    userMed.IntervalHours = dto.IntervalHours;
                    userMed.DosesPerPeriod = null;
                    userMed.PeriodUnit = null;
                    userMed.PeriodValue = null;
                }
                if (dto.FirstDoseTime.HasValue)
                {
                    var newTime = TimeOnly.FromTimeSpan(dto.FirstDoseTime.Value);
                    if (userMed.FirstDoseTime != newTime)
                        scheduleChanged = true;
                    userMed.FirstDoseTime = newTime;
                }
            }
            else
            {
                // Backward-compatible: no scheduleType hint, apply fields as before
                if (dto.IntervalHours.HasValue)
                {
                    if (userMed.IntervalHours != dto.IntervalHours)
                        scheduleChanged = true;
                    userMed.IntervalHours = dto.IntervalHours;
                    userMed.DosesPerPeriod = null;
                    userMed.PeriodUnit = null;
                    userMed.PeriodValue = null;
                }
                else if (dto.DosesPerPeriod.HasValue || dto.PeriodUnit != null || dto.PeriodValue.HasValue)
                {
                    if ((dto.DosesPerPeriod.HasValue && userMed.DosesPerPeriod != dto.DosesPerPeriod)
                        || (dto.PeriodUnit != null && userMed.PeriodUnit != dto.PeriodUnit)
                        || (dto.PeriodValue.HasValue && userMed.PeriodValue != dto.PeriodValue))
                    {
                        scheduleChanged = true;
                    }
                    if (dto.DosesPerPeriod.HasValue) userMed.DosesPerPeriod = dto.DosesPerPeriod;
                    if (dto.PeriodUnit != null) userMed.PeriodUnit = dto.PeriodUnit;
                    if (dto.PeriodValue.HasValue) userMed.PeriodValue = dto.PeriodValue;
                    userMed.IntervalHours = null;
                }

                if (dto.FirstDoseTime.HasValue)
                {
                    var newTime = TimeOnly.FromTimeSpan(dto.FirstDoseTime.Value);
                    if (userMed.FirstDoseTime != newTime)
                        scheduleChanged = true;
                    userMed.FirstDoseTime = newTime;
                }
            }

            if (dto.ExpiryDate.HasValue)
            {
                userMed.ExpiryDate = DateOnly.FromDateTime(dto.ExpiryDate.Value);
            }

            MedicationExpiryHelper.Apply(userMed, userMed.Medication, now);

            await _context.SaveChangesAsync();

            // ── Regenerate schedules ──────────────────────────────────────────
            if (scheduleChanged)
            {
                if (effectiveScheduleType == "CustomTimes" && hasCustomTimes)
                    await _scheduleService.RegenerateScheduleWithDoseTimesAsync(userMed, resolvedDoseTimes!);
                else
                    await _scheduleService.RegenerateScheduleAsync(userMed);
            }

            return Ok(new
            {
                Message = "UserMedication updated successfully.",
                ExpiryDate = userMed.ExpiryDate,
                userMed.IsOpened,
                userMed.OpenedDate,
                userMed.AfterOpeningDurationValue,
                userMed.AfterOpeningDurationUnit,
                userMed.AfterOpeningExpiryDate,
                userMed.EffectiveExpiryDate,
                userMed.ExpiryReason,
                userMed.AfterOpeningSource,
                AfterOpeningWarning = MedicationExpiryHelper.GetWarning(userMed),
                userMed.MedicationId,
                MedicationName = UserMedicationFeatureHelper.GetDisplayName(userMed, _translationService),
                userMed.IsCustomMedication,
                SupportsInteractions = UserMedicationFeatureHelper.SupportsInteractions(userMed),
                SupportsIngredientWarnings = UserMedicationFeatureHelper.SupportsIngredientWarnings(userMed),
                CustomMedicationWarning = UserMedicationFeatureHelper.GetCustomMedicationWarning(userMed),
                DosageForm = userMed.DosageForm,
                QuantityUnit = userMed.QuantityUnit,
                InitialQuantity = userMed.InitialQuantity,
                CurrentQuantity = userMed.CurrentQuantity,
                DoseQuantity = userMed.DoseQuantity,
                LowStockThreshold = userMed.LowStockThreshold,
                userMed.MedicationUseType,
                userMed.MaxDosesPerDay,
                userMed.MinimumHoursBetweenDoses,
                userMed.RefillReminderDaysBefore,
                ScheduleRegenerated = scheduleChanged
            });
        }

        // ================= REQUEST DATABASE ADDITION =================
        [HttpPost("{id}/request-add-to-database")]
        public async Task<ActionResult> RequestMedicationAddToDatabase(int id)
        {
            var userId = GetUserId();

            var userMed = await _context.UserMedications
                .Include(um => um.Medication)
                .FirstOrDefaultAsync(um => um.Id == id && um.UserId == userId);

            if (userMed == null)
                return NotFound(new { Message = "UserMedication not found." });

            if (UserMedicationFeatureHelper.SupportsDatabaseFeatures(userMed))
                return BadRequest(new { Message = "This medication already exists in the database." });

            var medicationName = UserMedicationFeatureHelper.GetDisplayName(userMed);
            if (string.IsNullOrWhiteSpace(medicationName))
                return BadRequest(new { Message = "medicationName is required." });

            var dosageForm = UserMedicationFeatureHelper.GetDosageForm(userMed);
            var quantityUnit = UserMedicationFeatureHelper.GetQuantityUnit(userMed);
            var now = DateTime.UtcNow;
            var message = BuildMissingMedicationRequestMessage(medicationName, dosageForm, quantityUnit);

            var existingOpenTicket = await _context.SupportTickets.AnyAsync(t =>
                t.UserId == userId
                && t.Category == SupportCategory.MissingMedicationRequest
                && t.Status != SupportStatus.Closed
                && t.Message.Contains($"Medication name: {medicationName}"));

            if (existingOpenTicket)
                return Ok(new { Message = "A request for this medication is already open." });

            var ticket = new SupportTicket
            {
                UserId = userId,
                Category = SupportCategory.MissingMedicationRequest,
                Message = message,
                Status = SupportStatus.Open,
                CreatedAt = now
            };

            _context.SupportTickets.Add(ticket);
            _context.Alerts.Add(new Alert
            {
                UserId = userId,
                Type = "AdminMessage",
                Title = "Support Request Submitted",
                Message = "Your missing medication request was submitted successfully.",
                IsRead = false,
                ScheduledAt = now,
                CreatedAt = now
            });

            await _context.SaveChangesAsync();

            return Ok(new
            {
                Message = "Missing medication request submitted successfully.",
                TicketId = ticket.Id,
                Status = ticket.Status.ToString()
            });
        }

        [HttpPost("/api/user-medications/{id:int}/refill")]
        public async Task<ActionResult> RefillMedication(int id, RefillMedicationDto dto)
        {
            var userId = GetUserId();
            if (dto.Quantity <= 0)
                return BadRequest(new { Message = "quantity must be greater than 0." });
            if (dto.RefillReminderDaysBefore.HasValue && dto.RefillReminderDaysBefore.Value < 0)
                return BadRequest(new { Message = "refillReminderDaysBefore must not be negative." });

            var userMed = await _context.UserMedications
                .Include(um => um.Medication)
                .FirstOrDefaultAsync(um => um.Id == id && um.UserId == userId);
            if (userMed == null)
                return NotFound(new { Message = "UserMedication not found." });

            var now = DateTime.UtcNow;
            var refillDate = dto.RefillDate ?? now;
            var currentQuantity = MedicationQuantityHelper.ResolveQuantity(userMed.CurrentQuantity, userMed.CurrentPillCount) ?? 0;
            var initialQuantity = MedicationQuantityHelper.ResolveQuantity(userMed.InitialQuantity, userMed.InitialPillCount);

            userMed.CurrentQuantity = currentQuantity + dto.Quantity;
            userMed.CurrentPillCount = MedicationQuantityHelper.ResolveLegacyCount(null, userMed.CurrentQuantity);
            if (!initialQuantity.HasValue || userMed.CurrentQuantity > initialQuantity)
            {
                userMed.InitialQuantity = userMed.CurrentQuantity;
                userMed.InitialPillCount = MedicationQuantityHelper.ResolveLegacyCount(null, userMed.InitialQuantity);
            }
            userMed.LastRefillDate = refillDate;
            userMed.LastRefillQuantity = dto.Quantity;
            if (dto.RefillReminderDaysBefore.HasValue)
                userMed.RefillReminderDaysBefore = dto.RefillReminderDaysBefore;

            await _context.SaveChangesAsync();

            var forecast = MedicationRefillForecastHelper.BuildForecast(userMed, now);
            return Ok(new
            {
                Message = "Medication refill recorded successfully.",
                userMed.Id,
                userMed.CurrentQuantity,
                userMed.QuantityUnit,
                userMed.LastRefillDate,
                userMed.LastRefillQuantity,
                Forecast = forecast
            });
        }

        [HttpPost("/api/user-medications/{id:int}/take-now")]
        public async Task<ActionResult> TakeMedicationNow(int id, [FromBody] TakeNowDto dto)
        {
            var userId = GetUserId();
            var userMed = await _context.UserMedications
                .Include(um => um.Medication)
                .FirstOrDefaultAsync(um => um.Id == id && um.UserId == userId);
            if (userMed == null)
                return NotFound(new { Message = "UserMedication not found." });

            var now = DateTime.UtcNow;
            var takenAt = dto.TakenAt ?? now;
            var quantityTaken = dto.QuantityTaken ?? MedicationRefillForecastHelper.ResolveDoseQuantity(userMed);
            if (quantityTaken <= 0)
                return BadRequest(new { Message = "quantityTaken must be greater than 0." });

            if (userMed.MinimumHoursBetweenDoses.HasValue && userMed.MinimumHoursBetweenDoses.Value > 0)
            {
                var previous = await _context.MedicationIntakeLogs
                    .Where(l => l.UserMedicationId == id && l.TakenAt <= takenAt)
                    .OrderByDescending(l => l.TakenAt)
                    .FirstOrDefaultAsync();

                if (previous != null)
                {
                    var hoursSince = (decimal)(takenAt - previous.TakenAt).TotalHours;
                    if (hoursSince < userMed.MinimumHoursBetweenDoses.Value)
                    {
                        return BadRequest(new
                        {
                            Message = $"Minimum time between doses is {userMed.MinimumHoursBetweenDoses:0.##} hour(s).",
                            PreviousTakenAt = previous.TakenAt
                        });
                    }
                }
            }

            if (userMed.MaxDosesPerDay.HasValue && userMed.MaxDosesPerDay.Value > 0)
            {
                var dayStart = takenAt.Date;
                var dayEnd = dayStart.AddDays(1);
                var takenToday = await _context.MedicationIntakeLogs
                    .CountAsync(l => l.UserMedicationId == id && l.TakenAt >= dayStart && l.TakenAt < dayEnd);
                if (takenToday >= userMed.MaxDosesPerDay.Value)
                {
                    return BadRequest(new
                    {
                        Message = $"Maximum doses per day is {userMed.MaxDosesPerDay.Value}.",
                        DosesAlreadyTakenToday = takenToday
                    });
                }
            }

            userMed.CurrentQuantity = MedicationQuantityHelper.ResolveQuantity(userMed.CurrentQuantity, userMed.CurrentPillCount);
            if (userMed.CurrentQuantity.HasValue)
            {
                userMed.CurrentQuantity = Math.Max(0, userMed.CurrentQuantity.Value - quantityTaken);
                userMed.CurrentPillCount = MedicationQuantityHelper.ResolveLegacyCount(null, userMed.CurrentQuantity);
            }

            var log = new MedicationIntakeLog
            {
                UserMedicationId = userMed.Id,
                TakenAt = takenAt,
                QuantityTaken = quantityTaken,
                Reason = CleanText(dto.Reason),
                Notes = CleanText(dto.Notes),
                CreatedAt = now
            };
            _context.MedicationIntakeLogs.Add(log);

            await CreateRefillWarningIfNeededAsync(userMed, now);
            await _context.SaveChangesAsync();

            var forecast = MedicationRefillForecastHelper.BuildForecast(userMed, now);
            return Ok(new
            {
                Message = "Medication intake logged successfully.",
                Intake = ToIntakeLogDto(log, userMed),
                userMed.CurrentQuantity,
                userMed.CurrentPillCount,
                Forecast = forecast
            });
        }

        [HttpGet("/api/user-medications/{id:int}/intake-history")]
        public async Task<ActionResult<List<MedicationIntakeLogDto>>> GetMedicationIntakeHistory(int id)
        {
            var userId = GetUserId();
            var userMed = await _context.UserMedications
                .Include(um => um.Medication)
                .FirstOrDefaultAsync(um => um.Id == id && um.UserId == userId);
            if (userMed == null)
                return NotFound(new { Message = "UserMedication not found." });

            var logs = await _context.MedicationIntakeLogs
                .Where(l => l.UserMedicationId == id)
                .OrderByDescending(l => l.TakenAt)
                .ToListAsync();

            return Ok(logs.Select(log => ToIntakeLogDto(log, userMed)).ToList());
        }

        [HttpGet("/api/users/me/cabinet-health")]
        public async Task<ActionResult<CabinetHealthDto>> GetCabinetHealth()
        {
            var userId = GetUserId();
            var now = DateTime.UtcNow;
            var meds = await _context.UserMedications
                .Include(um => um.Medication)
                .Where(um => um.UserId == userId)
                .ToListAsync();

            var dashboard = new CabinetHealthDto();
            foreach (var med in meds)
            {
                var forecast = MedicationRefillForecastHelper.BuildForecast(med, now);
                var effectiveExpiry = MedicationExpiryHelper.GetEffectiveExpiryDate(med);
                var currentQuantity = MedicationQuantityHelper.ResolveQuantity(med.CurrentQuantity, med.CurrentPillCount);
                var lowStock = currentQuantity.HasValue
                    && med.LowStockThreshold.HasValue
                    && currentQuantity.Value <= med.LowStockThreshold.Value;
                var outOfStock = currentQuantity.HasValue && currentQuantity.Value <= 0;
                var expired = effectiveExpiry.HasValue && effectiveExpiry.Value.Date < now.Date;
                var expiringSoon = effectiveExpiry.HasValue
                    && effectiveExpiry.Value.Date >= now.Date
                    && effectiveExpiry.Value.Date <= now.Date.AddDays(7);
                var afterOpeningSoon = med.AfterOpeningExpiryDate.HasValue
                    && med.AfterOpeningExpiryDate.Value.Date >= now.Date
                    && med.AfterOpeningExpiryDate.Value.Date <= now.Date.AddDays(7);

                if (expired)
                    dashboard.Expired.Add(BuildCabinetItem(med, forecast, "Critical", "Medication is expired."));
                if (expiringSoon)
                    dashboard.ExpiringSoon.Add(BuildCabinetItem(med, forecast, "Warning", "Medication expires within 7 days."));
                if (afterOpeningSoon)
                    dashboard.AfterOpeningExpiringSoon.Add(BuildCabinetItem(med, forecast, "Warning", "Opened medication expires soon."));
                if (outOfStock)
                    dashboard.OutOfStock.Add(BuildCabinetItem(med, forecast, "Critical", "Medication is out of stock."));
                else if (lowStock)
                    dashboard.LowStock.Add(BuildCabinetItem(med, forecast, "Warning", "Medication stock is low."));

                if (!expired && !expiringSoon && !afterOpeningSoon && !outOfStock && !lowStock)
                    dashboard.Healthy.Add(BuildCabinetItem(med, forecast, "Info", "Medication looks healthy."));
            }

            return Ok(dashboard);
        }

        // ================= DELETE =================
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteUserMedication(int id)
        {
            var userId = GetUserId();

            var userMed = await _context.UserMedications
                .FirstOrDefaultAsync(um => um.Id == id && um.UserId == userId);

            if (userMed == null) return NotFound("UserMedication not found");

            _context.UserMedications.Remove(userMed);
            await _context.SaveChangesAsync();

            return Ok("UserMedication deleted successfully");
        }

        // ================= 2-STEP: INIT =================
        [HttpPost("init")]
        public async Task<ActionResult> InitUserMedication(InitUserMedicationDto dto , [FromQuery] string lang = "en")
        {
            var userId = GetUserId();

            var resolvedMedication = await ResolveMedicationAsync(
                dto.MedicationId,
                dto.MedicationName,
                dto.IsCustomMedication);

            if (resolvedMedication.Error != null)
                return BadRequest(new { Message = resolvedMedication.Error });

            var medication = resolvedMedication.Medication;
            var medicationName = resolvedMedication.MedicationName;
            var isCustomMedication = resolvedMedication.IsCustomMedication;

            var alreadyExists = await UserMedicationAlreadyExistsAsync(
                userId,
                medication?.ID,
                medicationName,
                ignoreUserMedicationId: null);

            if (alreadyExists)
                return BadRequest(new { Message = $"You already have '{medicationName}' in your medications." });

            var dosageForm = isCustomMedication
                ? dto.DosageForm
                : (string.IsNullOrWhiteSpace(medication?.Dosage_Form) ? dto.DosageForm : medication.Dosage_Form);
            var unitError = MedicationQuantityHelper.ValidateUnit(dosageForm, dto.QuantityUnit);
            if (unitError != null)
                return BadRequest(new { Message = unitError });

            var userMed = new UserMedication
            {
                UserId = userId,
                MedicationId = medication?.ID,
                MedicationName = medicationName,
                IsCustomMedication = isCustomMedication,
                DosageForm = dosageForm,
                QuantityUnit = MedicationQuantityHelper.ResolveUnit(dosageForm, dto.QuantityUnit)
            };

            MedicationExpiryHelper.Apply(userMed, medication, DateTime.UtcNow);

            _context.UserMedications.Add(userMed);
            await _context.SaveChangesAsync();
            var warnings = UserMedicationFeatureHelper.SupportsInteractions(userMed)
                ? await _interactionService.CheckInteractionsForNewMedWithLangAsync(userId, medicationName, lang)
                : new List<InteractionWarningDto>();

            return Ok(new
            {
                Message = "Medication selected successfully.",
                UserMedicationId = userMed.Id,
                userMed.MedicationId,
                MedicationName = medicationName,
                userMed.IsCustomMedication,
                SupportsInteractions = UserMedicationFeatureHelper.SupportsInteractions(userMed),
                SupportsIngredientWarnings = UserMedicationFeatureHelper.SupportsIngredientWarnings(userMed),
                CustomMedicationWarning = UserMedicationFeatureHelper.GetCustomMedicationWarning(userMed),
                DosageForm = userMed.DosageForm,
                QuantityUnit = userMed.QuantityUnit,
                InteractionWarnings = warnings.Count > 0
    ? warnings.Select(w => w.Reason).ToList()
    : null
            });
        }

        // ================= 2-STEP: DETAILS =================
        [HttpPut("{id}/details")]
        public async Task<ActionResult> AddUserMedicationDetails(int id, UserMedicationDetailsDto dto)
        {
            var userId = GetUserId();

            var userMed = await _context.UserMedications
                .Include(um => um.Medication)
                .FirstOrDefaultAsync(um => um.Id == id && um.UserId == userId);

            if (userMed == null)
                return NotFound(new { Message = "UserMedication not found." });

            var now = DateTime.UtcNow;

            // ── Validate basic fields ─────────────────────────────────────────
            if (dto.CurrentPillCount.HasValue && dto.CurrentPillCount < 0)
                return BadRequest(new { Message = "currentPillCount must not be negative." });
            if (dto.InitialPillCount.HasValue && dto.InitialPillCount < 0)
                return BadRequest(new { Message = "initialPillCount must not be negative." });
            if (dto.LowStockThreshold.HasValue && dto.LowStockThreshold < 0)
                return BadRequest(new { Message = "lowStockThreshold must not be negative." });
            if (dto.PillsPerDose.HasValue && dto.PillsPerDose <= 0)
                return BadRequest(new { Message = "pillsPerDose must be greater than 0." });
            if (MedicationQuantityHelper.HasInvalidQuantity(dto.InitialQuantity))
                return BadRequest(new { Message = "initialQuantity must not be negative." });
            if (MedicationQuantityHelper.HasInvalidQuantity(dto.CurrentQuantity))
                return BadRequest(new { Message = "currentQuantity must not be negative." });
            if (MedicationQuantityHelper.HasInvalidDoseQuantity(dto.DoseQuantity))
                return BadRequest(new { Message = "doseQuantity must be greater than 0." });

            var featureError = ValidateMedicationFeatureInputs(
                dto.MedicationUseType,
                dto.MaxDosesPerDay,
                dto.MinimumHoursBetweenDoses,
                dto.RefillReminderDaysBefore);
            if (featureError != null)
                return BadRequest(new { Message = featureError });

            var afterOpeningError = MedicationExpiryHelper.ValidateAfterOpeningInput(
                dto.IsOpened,
                dto.OpenedDate,
                dto.AfterOpeningDurationValue,
                dto.AfterOpeningDurationUnit,
                now);
            if (afterOpeningError != null)
                return BadRequest(new { Message = afterOpeningError });

            if (dto.IsCustomMedication == true && !userMed.IsCustomMedication)
            {
                userMed.MedicationId = null;
                userMed.Medication = null;
                userMed.IsCustomMedication = true;
                userMed.MedicationName = string.IsNullOrWhiteSpace(dto.MedicationName)
                    ? UserMedicationFeatureHelper.GetDisplayName(userMed)
                    : dto.MedicationName.Trim();
            }
            else if (dto.MedicationName != null && userMed.IsCustomMedication)
            {
                if (string.IsNullOrWhiteSpace(dto.MedicationName))
                    return BadRequest(new { Message = "medicationName is required for custom medications." });
                userMed.MedicationName = dto.MedicationName.Trim();
            }

            var effectiveDosageForm = userMed.IsCustomMedication
                ? (dto.DosageForm ?? userMed.DosageForm)
                : (string.IsNullOrWhiteSpace(userMed.Medication?.Dosage_Form)
                    ? (dto.DosageForm ?? userMed.DosageForm)
                    : userMed.Medication.Dosage_Form);
            var unitError = MedicationQuantityHelper.ValidateUnit(effectiveDosageForm, dto.QuantityUnit);
            if (unitError != null)
                return BadRequest(new { Message = unitError });

            // ── Resolve and validate doseTimes ────────────────────────────────
            var resolvedDoseTimes = ResolveDoseTimes(dto.DoseTimes, out string? doseTimeError);
            if (doseTimeError != null)
                return BadRequest(new { Message = doseTimeError });

            bool hasCustomTimes = resolvedDoseTimes != null && resolvedDoseTimes.Count > 0;

            // ── Infer effective schedule type ─────────────────────────────────
            string? effectiveScheduleType = dto.ScheduleType;
            if (string.IsNullOrWhiteSpace(effectiveScheduleType))
            {
                if (hasCustomTimes)
                    effectiveScheduleType = "CustomTimes";
                else if (dto.IntervalHours.HasValue)
                    effectiveScheduleType = "Interval";
            }

            // ── Validate per schedule type ────────────────────────────────────
            if (effectiveScheduleType == "Interval" && dto.IntervalHours.HasValue && dto.IntervalHours <= 0)
                return BadRequest(new { Message = "intervalHours must be greater than 0." });

            if (effectiveScheduleType == "CustomTimes" && !hasCustomTimes)
                return BadRequest(new { Message = "doseTimes must contain at least one valid time for CustomTimes schedule." });

            if (effectiveScheduleType == "CustomTimes" && dto.IntervalHours.HasValue)
                return BadRequest(new { Message = "intervalHours must not be used together with doseTimes." });

            if (dto.AdvanceReminderMinutes.HasValue)
            {
                var val = dto.AdvanceReminderMinutes.Value;
                if (val < 0 || val > 1440)
                {
                    return BadRequest(new { Message = "Advance reminder minutes must be between 1 and 1440 (or 0 to disable)." });
                }
            }

            var packageExpiry = dto.ExpiryDate.HasValue
                ? DateOnly.FromDateTime(dto.ExpiryDate.Value) : (DateOnly?)null;

            // ── Apply all fields ──────────────────────────────────────────────
            userMed.Dosage = dto.Dosage;
            userMed.Notes = dto.Notes;
            userMed.ExpiryDate = packageExpiry;
            userMed.IsOpened = dto.IsOpened;
            userMed.OpenedDate = dto.OpenedDate?.Date;
            userMed.AfterOpeningDurationValue = dto.AfterOpeningDurationValue;
            userMed.AfterOpeningDurationUnit = dto.AfterOpeningDurationUnit;
            userMed.AfterOpeningSource = null;
            userMed.CurrentPillCount = dto.CurrentPillCount;
            userMed.InitialPillCount = dto.InitialPillCount;
            userMed.LowStockThreshold = dto.LowStockThreshold;
            userMed.NotificationActive = dto.NotificationActive;
            userMed.AdvanceReminderMinutes = dto.AdvanceReminderMinutes == 0 ? null : dto.AdvanceReminderMinutes;
            userMed.MedicationUseType = NormalizeMedicationUseType(dto.MedicationUseType);
            userMed.MaxDosesPerDay = dto.MaxDosesPerDay;
            userMed.MinimumHoursBetweenDoses = dto.MinimumHoursBetweenDoses;
            userMed.RefillReminderDaysBefore = dto.RefillReminderDaysBefore;

            // PillsPerDose: save provided value (null means clear/unset for the details endpoint
            // which is a full-replace style endpoint like the original fields above)
            userMed.InitialQuantity = MedicationQuantityHelper.ResolveQuantity(dto.InitialQuantity, dto.InitialPillCount);
            userMed.CurrentQuantity = MedicationQuantityHelper.ResolveQuantity(dto.CurrentQuantity, dto.CurrentPillCount);
            userMed.DoseQuantity = MedicationQuantityHelper.ResolveQuantity(dto.DoseQuantity, dto.PillsPerDose);
            userMed.QuantityUnit = MedicationQuantityHelper.ResolveUnit(effectiveDosageForm, dto.QuantityUnit);
            userMed.DosageForm = effectiveDosageForm;
            userMed.PillsPerDose = MedicationQuantityHelper.ResolveLegacyCount(dto.PillsPerDose, userMed.DoseQuantity);
            userMed.InitialPillCount = MedicationQuantityHelper.ResolveLegacyCount(dto.InitialPillCount, userMed.InitialQuantity);
            userMed.CurrentPillCount = MedicationQuantityHelper.ResolveLegacyCount(dto.CurrentPillCount, userMed.CurrentQuantity);

            // ── Detect schedule-related changes ───────────────────────────────
            bool scheduleChanged = false;

            if (dto.StartDate.HasValue)
            {
                var newStart = DateOnly.FromDateTime(dto.StartDate.Value);
                if (userMed.StartDate != newStart)
                    scheduleChanged = true;
                userMed.StartDate = newStart;
            }
            else
            {
                userMed.StartDate = null;
            }

            if (dto.EndDate.HasValue)
            {
                var newEnd = DateOnly.FromDateTime(dto.EndDate.Value);
                if (userMed.EndDate != newEnd)
                    scheduleChanged = true;
                userMed.EndDate = newEnd;
            }
            else
            {
                userMed.EndDate = null;
            }

            // ── Apply schedule fields ─────────────────────────────────────────
            if (effectiveScheduleType == "CustomTimes" && hasCustomTimes)
            {
                var newFirst = resolvedDoseTimes![0];
                var newCount = resolvedDoseTimes.Count;
                if (userMed.FirstDoseTime != newFirst
                    || userMed.DosesPerPeriod != newCount
                    || userMed.PeriodUnit != "Day"
                    || userMed.PeriodValue != 1
                    || userMed.IntervalHours.HasValue)
                {
                    scheduleChanged = true;
                }
                userMed.FirstDoseTime = newFirst;
                userMed.DosesPerPeriod = newCount;
                userMed.PeriodUnit = "Day";
                userMed.PeriodValue = 1;
                userMed.IntervalHours = null;
            }
            else if (effectiveScheduleType == "Interval")
            {
                if (dto.IntervalHours.HasValue && userMed.IntervalHours != dto.IntervalHours)
                    scheduleChanged = true;
                if (dto.FirstDoseTime.HasValue)
                {
                    var newTime = TimeOnly.FromTimeSpan(dto.FirstDoseTime.Value);
                    if (userMed.FirstDoseTime != newTime)
                        scheduleChanged = true;
                    userMed.FirstDoseTime = newTime;
                }
                userMed.IntervalHours = dto.IntervalHours;
                userMed.DosesPerPeriod = null;
                userMed.PeriodUnit = null;
                userMed.PeriodValue = null;
            }
            else
            {
                // Backward-compatible path: apply fields directly
                if (dto.DosesPerPeriod.HasValue && userMed.DosesPerPeriod != dto.DosesPerPeriod)
                    scheduleChanged = true;
                if (dto.PeriodUnit != null && userMed.PeriodUnit != dto.PeriodUnit)
                    scheduleChanged = true;
                if (dto.PeriodValue.HasValue && userMed.PeriodValue != dto.PeriodValue)
                    scheduleChanged = true;
                if (dto.IntervalHours.HasValue && userMed.IntervalHours != dto.IntervalHours)
                    scheduleChanged = true;
                if (dto.FirstDoseTime.HasValue)
                {
                    var newTime = TimeOnly.FromTimeSpan(dto.FirstDoseTime.Value);
                    if (userMed.FirstDoseTime != newTime)
                        scheduleChanged = true;
                    userMed.FirstDoseTime = newTime;
                }
                else
                {
                    userMed.FirstDoseTime = null;
                }
                userMed.DosesPerPeriod = dto.DosesPerPeriod;
                userMed.PeriodUnit = dto.PeriodUnit;
                userMed.PeriodValue = dto.PeriodValue;
                userMed.IntervalHours = dto.IntervalHours;
            }

            MedicationExpiryHelper.Apply(userMed, userMed.Medication, now);

            await _context.SaveChangesAsync();

            // ── Generate/regenerate schedules only when schedule fields changed ─
            if (scheduleChanged)
            {
                if (effectiveScheduleType == "CustomTimes" && hasCustomTimes)
                    await _scheduleService.RegenerateScheduleWithDoseTimesAsync(userMed, resolvedDoseTimes!);
                else
                    await _scheduleService.RegenerateScheduleAsync(userMed);
            }

            return Ok(new
            {
                Message = "Medication details added successfully.",
                ExpiryDate = userMed.ExpiryDate,
                userMed.IsOpened,
                userMed.OpenedDate,
                userMed.AfterOpeningDurationValue,
                userMed.AfterOpeningDurationUnit,
                userMed.AfterOpeningExpiryDate,
                userMed.EffectiveExpiryDate,
                userMed.ExpiryReason,
                userMed.AfterOpeningSource,
                AfterOpeningWarning = MedicationExpiryHelper.GetWarning(userMed),
                userMed.MedicationId,
                MedicationName = UserMedicationFeatureHelper.GetDisplayName(userMed, _translationService),
                userMed.IsCustomMedication,
                SupportsInteractions = UserMedicationFeatureHelper.SupportsInteractions(userMed),
                SupportsIngredientWarnings = UserMedicationFeatureHelper.SupportsIngredientWarnings(userMed),
                CustomMedicationWarning = UserMedicationFeatureHelper.GetCustomMedicationWarning(userMed),
                DosageForm = userMed.DosageForm,
                QuantityUnit = userMed.QuantityUnit,
                InitialQuantity = userMed.InitialQuantity,
                CurrentQuantity = userMed.CurrentQuantity,
                DoseQuantity = userMed.DoseQuantity,
                LowStockThreshold = userMed.LowStockThreshold,
                userMed.MedicationUseType,
                userMed.MaxDosesPerDay,
                userMed.MinimumHoursBetweenDoses,
                userMed.RefillReminderDaysBefore
            });
        }

        // ================= HELPERS =================
        private sealed class ResolvedMedication
        {
            public Medication? Medication { get; init; }
            public string MedicationName { get; init; } = string.Empty;
            public bool IsCustomMedication { get; init; }
            public string? Error { get; init; }
        }

        private async Task<ResolvedMedication> ResolveMedicationAsync(
            int? medicationId,
            string? medicationName,
            bool? isCustomMedication)
        {
            if (medicationId.HasValue)
            {
                var medication = await _context.Medications
                    .FirstOrDefaultAsync(m => m.ID == medicationId.Value);

                if (medication == null)
                    return new ResolvedMedication { Error = $"Medication id '{medicationId.Value}' not found." };

                return new ResolvedMedication
                {
                    Medication = medication,
                    MedicationName = medication.Trade_name ?? medicationName?.Trim() ?? string.Empty,
                    IsCustomMedication = false
                };
            }

            if (string.IsNullOrWhiteSpace(medicationName))
                return new ResolvedMedication
                {
                    Error = "Either medicationId or medicationName is required."
                };

            var normalizedName = medicationName.Trim();
            if (isCustomMedication == true)
            {
                return new ResolvedMedication
                {
                    MedicationName = normalizedName,
                    IsCustomMedication = true
                };
            }

            var translatedMedId = _translationService.FindMedIdByName(normalizedName);
            var medicationByName = translatedMedId.HasValue
                ? await _context.Medications.FirstOrDefaultAsync(m => m.ID == translatedMedId.Value)
                : await _context.Medications.FirstOrDefaultAsync(m => m.Trade_name == normalizedName);

            if (medicationByName != null)
            {
                return new ResolvedMedication
                {
                    Medication = medicationByName,
                    MedicationName = medicationByName.Trade_name ?? normalizedName,
                    IsCustomMedication = false
                };
            }

            return new ResolvedMedication
            {
                MedicationName = normalizedName,
                IsCustomMedication = true
            };
        }

        private async Task<bool> UserMedicationAlreadyExistsAsync(
            int userId,
            int? medicationId,
            string medicationName,
            int? ignoreUserMedicationId)
        {
            var query = _context.UserMedications
                .Where(um => um.UserId == userId);

            if (ignoreUserMedicationId.HasValue)
                query = query.Where(um => um.Id != ignoreUserMedicationId.Value);

            if (medicationId.HasValue)
            {
                return await query.AnyAsync(um =>
                    um.MedicationId == medicationId.Value
                    || um.MedicationName == medicationName);
            }

            return await query.AnyAsync(um => um.MedicationName == medicationName);
        }

        private static string BuildMissingMedicationRequestMessage(
            string medicationName,
            string? dosageForm,
            string? quantityUnit)
        {
            return
                "A user tried to add a medication that is not available in the database.\n\n" +
                $"Medication name: {medicationName}\n" +
                $"Dosage form: {dosageForm ?? "Not provided"}\n" +
                $"Quantity unit: {quantityUnit ?? "unit"}\n\n" +
                "Please review this medication and consider adding it to the medication database.";
        }

        private async Task CreateRefillWarningIfNeededAsync(UserMedication userMed, DateTime now)
        {
            var forecast = MedicationRefillForecastHelper.BuildForecast(userMed, now);
            if (!forecast.RefillWarning || !userMed.NotificationActive)
                return;

            var alreadySentToday = await _context.Alerts.AnyAsync(a =>
                a.UserMedicationId == userMed.Id
                && a.Type == "RefillWarning"
                && a.CreatedAt.Date == now.Date);
            if (alreadySentToday)
                return;

            var medName = UserMedicationFeatureHelper.GetDisplayName(userMed);
            var runOutText = forecast.EstimatedRunOutDate.HasValue
                ? $"Estimated run-out date: {forecast.EstimatedRunOutDate.Value:dd/MM/yyyy}."
                : "Run-out date is not available.";

            _context.Alerts.Add(new Alert
            {
                UserId = userMed.UserId,
                UserMedicationId = userMed.Id,
                Type = "RefillWarning",
                Title = "Refill needed soon",
                Message = $"\"{medName}\" may run out soon. {runOutText}",
                IsRead = false,
                ScheduledAt = now,
                CreatedAt = now
            });
        }

        private static MedicationIntakeLogDto ToIntakeLogDto(MedicationIntakeLog log, UserMedication userMed)
        {
            return new MedicationIntakeLogDto
            {
                Id = log.Id,
                UserMedicationId = log.UserMedicationId,
                MedicationName = UserMedicationFeatureHelper.GetDisplayName(userMed),
                TakenAt = log.TakenAt,
                QuantityTaken = log.QuantityTaken,
                QuantityUnit = UserMedicationFeatureHelper.GetQuantityUnit(userMed),
                Reason = log.Reason,
                Notes = log.Notes
            };
        }

        private static CabinetMedicationDto BuildCabinetItem(
            UserMedication userMed,
            RefillForecastDto forecast,
            string severity,
            string reason)
        {
            return new CabinetMedicationDto
            {
                UserMedicationId = userMed.Id,
                MedicationName = UserMedicationFeatureHelper.GetDisplayName(userMed),
                Severity = severity,
                EffectiveExpiryDate = MedicationExpiryHelper.GetEffectiveExpiryDate(userMed),
                AfterOpeningExpiryDate = userMed.AfterOpeningExpiryDate,
                CurrentQuantity = MedicationQuantityHelper.ResolveQuantity(userMed.CurrentQuantity, userMed.CurrentPillCount),
                DoseQuantity = MedicationQuantityHelper.ResolveQuantity(userMed.DoseQuantity, userMed.PillsPerDose),
                QuantityUnit = UserMedicationFeatureHelper.GetQuantityUnit(userMed),
                DaysUntilEmpty = forecast.DaysUntilEmpty,
                EstimatedRunOutDate = forecast.EstimatedRunOutDate,
                Reason = reason
            };
        }

        private static string NormalizeMedicationUseType(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Scheduled";

            return value.Trim().Equals("AsNeeded", StringComparison.OrdinalIgnoreCase)
                || value.Trim().Equals("PRN", StringComparison.OrdinalIgnoreCase)
                || value.Trim().Equals("TakeAsNeeded", StringComparison.OrdinalIgnoreCase)
                    ? "AsNeeded"
                    : "Scheduled";
        }

        private static string? ValidateMedicationFeatureInputs(
            string? medicationUseType,
            int? maxDosesPerDay,
            decimal? minimumHoursBetweenDoses,
            int? refillReminderDaysBefore)
        {
            if (!string.IsNullOrWhiteSpace(medicationUseType))
            {
                var raw = medicationUseType.Trim();
                if (!raw.Equals("Scheduled", StringComparison.OrdinalIgnoreCase)
                    && !raw.Equals("AsNeeded", StringComparison.OrdinalIgnoreCase)
                    && !raw.Equals("PRN", StringComparison.OrdinalIgnoreCase)
                    && !raw.Equals("TakeAsNeeded", StringComparison.OrdinalIgnoreCase))
                {
                    return "medicationUseType must be Scheduled or AsNeeded.";
                }
            }

            if (maxDosesPerDay.HasValue && maxDosesPerDay.Value <= 0)
                return "maxDosesPerDay must be greater than 0.";
            if (minimumHoursBetweenDoses.HasValue && minimumHoursBetweenDoses.Value < 0)
                return "minimumHoursBetweenDoses must not be negative.";
            if (refillReminderDaysBefore.HasValue && refillReminderDaysBefore.Value < 0)
                return "refillReminderDaysBefore must not be negative.";

            return null;
        }

        private static string? CleanText(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private int GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim == null) throw new UnauthorizedAccessException();
            return int.Parse(claim.Value);
        }

        /// <summary>
        /// Infers the schedule type from the UserMedication entity fields.
        /// </summary>
        private static string? InferScheduleType(UserMedication um)
        {
            if (um.IntervalHours.HasValue && um.IntervalHours > 0)
                return "Interval";

            if (um.DosesPerPeriod.HasValue && um.DosesPerPeriod > 0)
                return "CustomTimes";

            return null;
        }

        /// <summary>
        /// Cleans and parses a raw DoseTimes list from a DTO.
        /// Returns null if the input was null or empty after cleaning.
        /// Sets doseTimeError if any string has an invalid format.
        /// </summary>
        private static List<TimeOnly>? ResolveDoseTimes(List<string>? raw, out string? error)
        {
            error = null;

            if (raw == null || raw.Count == 0)
                return null;

            // Strip empty/whitespace entries (treat [""] as empty)
            var nonEmpty = raw.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            if (nonEmpty.Count == 0)
                return null;

            var parsed = new List<TimeOnly>();
            foreach (var s in nonEmpty)
            {
                if (!TimeOnly.TryParse(s, out var t))
                {
                    error = $"Invalid dose time format: '{s}'. Use HH:mm:ss or HH:mm.";
                    return null;
                }
                parsed.Add(t);
            }

            // Deduplicate and sort
            return parsed.Distinct().OrderBy(t => t).ToList();
        }

        private static readonly TimeZoneInfo CairoZone =
            TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time");

        /// <summary>
        /// Converts a UTC TimeOfDay (TimeSpan) back to Cairo local time and formats as HH:mm:ss.
        /// Uses today's date as reference for the conversion.
        /// </summary>
        private static string ConvertUtcTimeOfDayToCairoString(TimeSpan utcTimeOfDay)
        {
            // Use an arbitrary reference date; DST differences are absorbed here.
            var utcDt = DateTime.UtcNow.Date.Add(utcTimeOfDay);
            var localDt = TimeZoneInfo.ConvertTimeFromUtc(utcDt, CairoZone);
            return localDt.ToString("HH:mm:ss");
        }
    }
}
