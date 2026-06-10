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

            // ── Find medication ───────────────────────────────────────────────
            var medId_add = _translationService.FindMedIdByName(dto.MedicationName);
            var medication = medId_add.HasValue
                ? await _context.Medications.FirstOrDefaultAsync(m => m.ID == medId_add.Value)
                : await _context.Medications.FirstOrDefaultAsync(m => m.Trade_name == dto.MedicationName);

            if (medication == null)
                return NotFound(new { Message = $"Medication '{dto.MedicationName}' not found." });

            var alreadyExists = await _context.UserMedications
                .AnyAsync(um => um.UserId == userId && um.MedId == medication.ID);

            if (alreadyExists)
                return BadRequest(new { Message = $"You already have '{dto.MedicationName}' in your medications." });

            var unitError = MedicationQuantityHelper.ValidateUnit(medication.Dosage_Form, dto.QuantityUnit);
            if (unitError != null)
                return BadRequest(new { Message = unitError });

            var dosageForm = string.IsNullOrWhiteSpace(medication.Dosage_Form)
                ? null
                : medication.Dosage_Form;
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

            var packageExpiry = dto.ExpiryDate.HasValue
                ? DateOnly.FromDateTime(dto.ExpiryDate.Value) : (DateOnly?)null;

            // ── Build entity ──────────────────────────────────────────────────
            var userMed = new UserMedication
            {
                UserId = userId,
                MedId = medication.ID,
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
                NotificationActive = dto.NotificationActive
            };

            MedicationExpiryHelper.Apply(userMed, medication, now);

            _context.UserMedications.Add(userMed);
            await _context.SaveChangesAsync();

            if (hasCustomTimes && resolvedDoseTimes != null)
                await _scheduleService.GenerateScheduleWithDoseTimesAsync(userMed, resolvedDoseTimes);
            else
                await _scheduleService.GenerateScheduleAsync(userMed);

            var warnings = await _interactionService
    .CheckInteractionsForNewMedWithLangAsync(userId, medication.Trade_name!, lang);

            return Ok(new
            {
                Message = $"'{dto.MedicationName}' added to your medications successfully.",
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
                MedicationName = medication.Trade_name,
                DosageForm = userMed.DosageForm,
                QuantityUnit = userMed.QuantityUnit,
                InitialQuantity = userMed.InitialQuantity,
                CurrentQuantity = userMed.CurrentQuantity,
                DoseQuantity = userMed.DoseQuantity,
                LowStockThreshold = userMed.LowStockThreshold,
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
                         && s.Status == "Pending"
                         && s.ScheduledAt > nowUtc)
                .Select(s => new { s.UserMedicationId, s.ScheduledAt })
                .ToListAsync();

            var scheduleTimeMap = rawScheduleRows
                .GroupBy(s => s.UserMedicationId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(s => s.ScheduledAt.TimeOfDay).Distinct().ToList()
                );

            var result = new List<UserMedicationDto>();

            foreach (var um in userMeds)
            {
                var rawInteractions = await _interactionService
     .GetInteractionsForUserMedication(userId, um.MedId);

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

                string translatedName = _translationService.GetMedName(um.MedId, lang);

                string finalName = string.IsNullOrWhiteSpace(translatedName)
     ? um.Medication.Trade_name ?? string.Empty
     : translatedName;

                string? scheduleType = InferScheduleType(um);

                List<string> doseTimes;
                if (scheduleType == "CustomTimes" && scheduleTimeMap.TryGetValue(um.Id, out var times))
                {
                    doseTimes = times
                        .OrderBy(t => t)
                        .Select(t => ConvertUtcTimeOfDayToCairoString(t))
                        .Distinct()
                        .ToList();
                }
                else
                {
                    doseTimes = new List<string>();
                }

                result.Add(new UserMedicationDto
                {
                    Id = um.Id,
                    MedId = um.MedId,
                    MedicationName = finalName,
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
                    DosageForm = string.IsNullOrWhiteSpace(um.DosageForm)
                        ? um.Medication?.Dosage_Form
                        : um.DosageForm,
                    QuantityUnit = string.IsNullOrWhiteSpace(um.QuantityUnit)
                        ? MedicationQuantityHelper.GetSuggestedUnit(um.DosageForm ?? um.Medication?.Dosage_Form)
                        : um.QuantityUnit,
                    InitialQuantity = MedicationQuantityHelper.ResolveQuantity(um.InitialQuantity, um.InitialPillCount),
                    CurrentQuantity = MedicationQuantityHelper.ResolveQuantity(um.CurrentQuantity, um.CurrentPillCount),
                    DoseQuantity = MedicationQuantityHelper.ResolveQuantity(um.DoseQuantity, um.PillsPerDose),
                    DosesPerPeriod = um.DosesPerPeriod,
                    PeriodUnit = um.PeriodUnit,
                    PeriodValue = um.PeriodValue,
                    FirstDoseTime = um.FirstDoseTime?.ToTimeSpan(),
                    IntervalHours = um.IntervalHours,
                    NotificationActive = um.NotificationActive,
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

            var nextIsOpened = dto.IsOpened ?? userMed.IsOpened;
            var afterOpeningError = MedicationExpiryHelper.ValidateAfterOpeningInput(
                nextIsOpened,
                dto.OpenedDate,
                dto.AfterOpeningDurationValue,
                dto.AfterOpeningDurationUnit,
                now);
            if (afterOpeningError != null)
                return BadRequest(new { Message = afterOpeningError });

            var effectiveDosageForm = string.IsNullOrWhiteSpace(userMed.Medication?.Dosage_Form)
                ? userMed.DosageForm
                : userMed.Medication.Dosage_Form;
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
            if (dto.NotificationActive.HasValue) userMed.NotificationActive = dto.NotificationActive.Value;
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

            if (string.IsNullOrWhiteSpace(userMed.DosageForm))
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
                MedicationName = userMed.Medication?.Trade_name,
                DosageForm = userMed.DosageForm,
                QuantityUnit = userMed.QuantityUnit,
                InitialQuantity = userMed.InitialQuantity,
                CurrentQuantity = userMed.CurrentQuantity,
                DoseQuantity = userMed.DoseQuantity,
                LowStockThreshold = userMed.LowStockThreshold,
                ScheduleRegenerated = scheduleChanged
            });
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

            var medId_init = _translationService.FindMedIdByName(dto.MedicationName);
            var medication = medId_init.HasValue
                ? await _context.Medications.FirstOrDefaultAsync(m => m.ID == medId_init.Value)
                : await _context.Medications.FirstOrDefaultAsync(m => m.Trade_name == dto.MedicationName);

            if (medication == null)
                return NotFound(new { Message = $"Medication '{dto.MedicationName}' not found." });

            var alreadyExists = await _context.UserMedications
                .AnyAsync(um => um.UserId == userId && um.MedId == medication.ID);

            if (alreadyExists)
                return BadRequest(new { Message = $"You already have '{dto.MedicationName}' in your medications." });

            var userMed = new UserMedication
            {
                UserId = userId,
                MedId = medication.ID,
                DosageForm = medication.Dosage_Form,
                QuantityUnit = MedicationQuantityHelper.GetSuggestedUnit(medication.Dosage_Form)
            };

            MedicationExpiryHelper.Apply(userMed, medication, DateTime.UtcNow);

            _context.UserMedications.Add(userMed);
            await _context.SaveChangesAsync();
            var warnings = await _interactionService
                .CheckInteractionsForNewMedWithLangAsync(userId, medication.Trade_name!, lang);

            return Ok(new
            {
                Message = "Medication selected successfully.",
                UserMedicationId = userMed.Id,
                MedicationName = medication.Trade_name,
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

            var afterOpeningError = MedicationExpiryHelper.ValidateAfterOpeningInput(
                dto.IsOpened,
                dto.OpenedDate,
                dto.AfterOpeningDurationValue,
                dto.AfterOpeningDurationUnit,
                now);
            if (afterOpeningError != null)
                return BadRequest(new { Message = afterOpeningError });

            var effectiveDosageForm = string.IsNullOrWhiteSpace(userMed.Medication?.Dosage_Form)
                ? userMed.DosageForm
                : userMed.Medication.Dosage_Form;
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
                MedicationName = userMed.Medication?.Trade_name,
                DosageForm = userMed.DosageForm,
                QuantityUnit = userMed.QuantityUnit,
                InitialQuantity = userMed.InitialQuantity,
                CurrentQuantity = userMed.CurrentQuantity,
                DoseQuantity = userMed.DoseQuantity,
                LowStockThreshold = userMed.LowStockThreshold
            });
        }

        // ================= HELPERS =================
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
