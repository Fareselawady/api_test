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
        private readonly AppDbContext _context;
        private readonly IScheduleService _scheduleService;
        private readonly IInteractionService _interactionService;

        public UserMedicationsController(
            AppDbContext context,
            IScheduleService scheduleService,
            IInteractionService interactionService)
        {
            _context = context;
            _scheduleService = scheduleService;
            _interactionService = interactionService;
        }

        // ================= CREATE (single-step) =================
        [HttpPost]
        public async Task<ActionResult> AddUserMedication(CreateUserMedicationDto dto)
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

            // ── Resolve and validate custom dose times ────────────────────────
            List<TimeOnly>? resolvedDoseTimes = null;

            bool hasCustomTimes = dto.DoseTimes != null && dto.DoseTimes.Count > 0;

            if (hasCustomTimes)
            {
                // Validate format
                var parsed = new List<TimeOnly>();
                foreach (var raw in dto.DoseTimes!)
                {
                    if (!TimeOnly.TryParse(raw, out var t))
                        return BadRequest(new { Message = $"Invalid dose time format: '{raw}'. Use HH:mm:ss or HH:mm." });
                    parsed.Add(t);
                }

                // Deduplicate and sort
                resolvedDoseTimes = parsed.Distinct().OrderBy(t => t).ToList();

                if (resolvedDoseTimes.Count == 0)
                    return BadRequest(new { Message = "doseTimes must contain at least one valid time." });

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
            var medication = await _context.Medications
                .FirstOrDefaultAsync(m => m.Trade_name == dto.MedicationName);

            if (medication == null)
                return NotFound(new { Message = $"Medication '{dto.MedicationName}' not found." });

            var alreadyExists = await _context.UserMedications
                .AnyAsync(um => um.UserId == userId && um.MedId == medication.ID);

            if (alreadyExists)
                return BadRequest(new { Message = $"You already have '{dto.MedicationName}' in your medications." });

            // ── Expiry adjustment ─────────────────────────────────────────────
            var originalExpiry = dto.ExpiryDate.HasValue
                ? DateOnly.FromDateTime(dto.ExpiryDate.Value) : (DateOnly?)null;

            var adjustedExpiry = AdjustExpiryForLiquid(originalExpiry, medication.Dosage_Form);

            // ── Build entity ──────────────────────────────────────────────────
            var userMed = new UserMedication
            {
                UserId = userId,
                MedId = medication.ID,
                Dosage = dto.Dosage,
                Notes = dto.Notes,
                StartDate = dto.StartDate.HasValue ? DateOnly.FromDateTime(dto.StartDate.Value) : null,
                EndDate = dto.EndDate.HasValue ? DateOnly.FromDateTime(dto.EndDate.Value) : null,
                ExpiryDate = adjustedExpiry,
                CurrentPillCount = dto.CurrentPillCount,
                InitialPillCount = dto.InitialPillCount,
                LowStockThreshold = dto.LowStockThreshold,
                PillsPerDose = dto.PillsPerDose,
                DosesPerPeriod = dto.DosesPerPeriod,
                PeriodUnit = dto.PeriodUnit,
                PeriodValue = dto.PeriodValue,
                FirstDoseTime = dto.FirstDoseTime.HasValue ? TimeOnly.FromTimeSpan(dto.FirstDoseTime.Value) : null,
                IntervalHours = hasCustomTimes ? null : dto.IntervalHours,
                NotificationActive = dto.NotificationActive
            };

            _context.UserMedications.Add(userMed);
            await _context.SaveChangesAsync();

            if (hasCustomTimes && resolvedDoseTimes != null)
                await _scheduleService.GenerateScheduleWithDoseTimesAsync(userMed, resolvedDoseTimes);
            else
                await _scheduleService.GenerateScheduleAsync(userMed);

            var warnings = await _interactionService
                .CheckInteractionsForNewMedAsync(userId, dto.MedicationName);

            bool expiryWasAdjusted = adjustedExpiry != originalExpiry;

            return Ok(new
            {
                Message = $"'{dto.MedicationName}' added to your medications successfully.",
                ExpiryDate = adjustedExpiry,
                ExpiryAdjusted = expiryWasAdjusted,
                ExpiryAdjustedNote = expiryWasAdjusted
                    ? (string?)$"Expiry date adjusted from {originalExpiry:dd/MM/yyyy} to {adjustedExpiry:dd/MM/yyyy} because this is a liquid medication (opened shelf life = 3 months)."
                    : null,
                InteractionWarnings = warnings.Count > 0 ? warnings : null
            });
        }

        // ================= READ =================
        [HttpGet("myusermeds")]
        public async Task<ActionResult> GetMyUserMedications()
        {
            var userId = GetUserId();

            var userMeds = await _context.UserMedications
                .Include(um => um.Medication)
                .Where(um => um.UserId == userId)
                .ToListAsync();

            // Load all future pending schedules for this user's meds in one query
            var userMedIds = userMeds.Select(um => um.Id).ToList();
            var nowUtc = DateTime.UtcNow;

            // Fetch raw (UserMedicationId, ScheduledAt) rows from DB — no grouping/Distinct in SQL
            // then group and deduplicate in memory to avoid EF Core translation limitations.
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
                var interactions = await _interactionService
                    .GetInteractionsForUserMedication(userId, um.MedId);

                // ── Infer scheduleType ────────────────────────────────────────
                string? scheduleType = InferScheduleType(um);

                // ── Build doseTimes list ──────────────────────────────────────
                List<string> doseTimes;
                if (scheduleType == "CustomTimes" && scheduleTimeMap.TryGetValue(um.Id, out var times))
                {
                    // Convert UTC times back to Cairo local and format as HH:mm:ss
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
                    MedicationName = um.Medication.Trade_name,
                    Dosage = um.Dosage,
                    Notes = um.Notes,
                    StartDate = um.StartDate?.ToDateTime(TimeOnly.MinValue),
                    EndDate = um.EndDate?.ToDateTime(TimeOnly.MinValue),
                    ExpiryDate = um.ExpiryDate?.ToDateTime(TimeOnly.MinValue),
                    CurrentPillCount = um.CurrentPillCount,
                    InitialPillCount = um.InitialPillCount,
                    LowStockThreshold = um.LowStockThreshold,
                    PillsPerDose = um.PillsPerDose,
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

            // ── Validate basic fields ─────────────────────────────────────────
            if (dto.CurrentPillCount.HasValue && dto.CurrentPillCount < 0)
                return BadRequest(new { Message = "currentPillCount must not be negative." });
            if (dto.InitialPillCount.HasValue && dto.InitialPillCount < 0)
                return BadRequest(new { Message = "initialPillCount must not be negative." });
            if (dto.LowStockThreshold.HasValue && dto.LowStockThreshold < 0)
                return BadRequest(new { Message = "lowStockThreshold must not be negative." });
            if (dto.PillsPerDose.HasValue && dto.PillsPerDose <= 0)
                return BadRequest(new { Message = "pillsPerDose must be greater than 0." });

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

            // PillsPerDose: update only when explicitly provided; preserve existing value when null
            if (dto.PillsPerDose.HasValue) userMed.PillsPerDose = dto.PillsPerDose;

            // ── Apply schedule fields ─────────────────────────────────────────
            bool scheduleChanged = false;

            if (effectiveScheduleType == "CustomTimes" && hasCustomTimes)
            {
                // Derive scheduling fields from doseTimes
                userMed.FirstDoseTime = resolvedDoseTimes![0];
                userMed.DosesPerPeriod = resolvedDoseTimes.Count;
                userMed.PeriodUnit = "Day";
                userMed.PeriodValue = 1;
                userMed.IntervalHours = null;
                scheduleChanged = true;
            }
            else if (effectiveScheduleType == "Interval")
            {
                if (dto.IntervalHours.HasValue)
                {
                    userMed.IntervalHours = dto.IntervalHours;
                    userMed.DosesPerPeriod = null;
                    userMed.PeriodUnit = null;
                    userMed.PeriodValue = null;
                    scheduleChanged = true;
                }
                if (dto.FirstDoseTime.HasValue)
                {
                    userMed.FirstDoseTime = TimeOnly.FromTimeSpan(dto.FirstDoseTime.Value);
                    scheduleChanged = true;
                }
            }
            else
            {
                // Backward-compatible: no scheduleType hint, apply fields as before
                if (dto.IntervalHours.HasValue)
                {
                    userMed.IntervalHours = dto.IntervalHours;
                    userMed.DosesPerPeriod = null;
                    userMed.PeriodUnit = null;
                    userMed.PeriodValue = null;
                    scheduleChanged = true;
                }
                else if (dto.DosesPerPeriod.HasValue || dto.PeriodUnit != null || dto.PeriodValue.HasValue)
                {
                    if (dto.DosesPerPeriod.HasValue) userMed.DosesPerPeriod = dto.DosesPerPeriod;
                    if (dto.PeriodUnit != null) userMed.PeriodUnit = dto.PeriodUnit;
                    if (dto.PeriodValue.HasValue) userMed.PeriodValue = dto.PeriodValue;
                    userMed.IntervalHours = null;
                    scheduleChanged = true;
                }

                if (dto.FirstDoseTime.HasValue)
                {
                    userMed.FirstDoseTime = TimeOnly.FromTimeSpan(dto.FirstDoseTime.Value);
                    scheduleChanged = true;
                }
            }

            if (dto.StartDate.HasValue || dto.EndDate.HasValue)
                scheduleChanged = true;

            // ── Expiry ────────────────────────────────────────────────────────
            DateOnly? adjustedExpiry = null;
            DateOnly? originalExpiry = null;
            if (dto.ExpiryDate.HasValue)
            {
                originalExpiry = DateOnly.FromDateTime(dto.ExpiryDate.Value);
                adjustedExpiry = AdjustExpiryForLiquid(originalExpiry, userMed.Medication?.Dosage_Form);
                userMed.ExpiryDate = adjustedExpiry;
            }

            await _context.SaveChangesAsync();

            // ── Regenerate schedules ──────────────────────────────────────────
            if (scheduleChanged)
            {
                if (effectiveScheduleType == "CustomTimes" && hasCustomTimes)
                    await _scheduleService.RegenerateScheduleWithDoseTimesAsync(userMed, resolvedDoseTimes!);
                else
                    await _scheduleService.RegenerateScheduleAsync(userMed);
            }

            bool expiryWasAdjusted = adjustedExpiry != null && adjustedExpiry != originalExpiry;

            return Ok(new
            {
                Message = "UserMedication updated successfully.",
                ExpiryDate = userMed.ExpiryDate,
                ExpiryAdjusted = expiryWasAdjusted,
                ExpiryAdjustedNote = expiryWasAdjusted
                    ? (string?)$"Expiry date adjusted from {originalExpiry:dd/MM/yyyy} to {adjustedExpiry:dd/MM/yyyy} because this is a liquid medication (opened shelf life = 3 months)."
                    : null,
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
        public async Task<ActionResult> InitUserMedication(InitUserMedicationDto dto)
        {
            var userId = GetUserId();

            var medication = await _context.Medications
                .FirstOrDefaultAsync(m => m.Trade_name == dto.MedicationName);

            if (medication == null)
                return NotFound(new { Message = $"Medication '{dto.MedicationName}' not found." });

            var alreadyExists = await _context.UserMedications
                .AnyAsync(um => um.UserId == userId && um.MedId == medication.ID);

            if (alreadyExists)
                return BadRequest(new { Message = $"You already have '{dto.MedicationName}' in your medications." });

            var userMed = new UserMedication
            {
                UserId = userId,
                MedId = medication.ID
            };

            _context.UserMedications.Add(userMed);
            await _context.SaveChangesAsync();

            var warnings = await _interactionService
                .CheckInteractionsForNewMedAsync(userId, dto.MedicationName);

            return Ok(new
            {
                Message = "Medication selected successfully.",
                UserMedicationId = userMed.Id,
                MedicationName = medication.Trade_name,
                DosageForm = medication.Dosage_Form,
                InteractionWarnings = warnings.Count > 0 ? warnings : null
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

            // ── Validate basic fields ─────────────────────────────────────────
            if (dto.CurrentPillCount.HasValue && dto.CurrentPillCount < 0)
                return BadRequest(new { Message = "currentPillCount must not be negative." });
            if (dto.InitialPillCount.HasValue && dto.InitialPillCount < 0)
                return BadRequest(new { Message = "initialPillCount must not be negative." });
            if (dto.LowStockThreshold.HasValue && dto.LowStockThreshold < 0)
                return BadRequest(new { Message = "lowStockThreshold must not be negative." });
            if (dto.PillsPerDose.HasValue && dto.PillsPerDose <= 0)
                return BadRequest(new { Message = "pillsPerDose must be greater than 0." });

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

            // ── Expiry ────────────────────────────────────────────────────────
            var originalExpiry = dto.ExpiryDate.HasValue
                ? DateOnly.FromDateTime(dto.ExpiryDate.Value) : (DateOnly?)null;

            var adjustedExpiry = AdjustExpiryForLiquid(originalExpiry, userMed.Medication?.Dosage_Form);

            // ── Apply all fields ──────────────────────────────────────────────
            userMed.Dosage = dto.Dosage;
            userMed.Notes = dto.Notes;
            userMed.StartDate = dto.StartDate.HasValue ? DateOnly.FromDateTime(dto.StartDate.Value) : null;
            userMed.EndDate = dto.EndDate.HasValue ? DateOnly.FromDateTime(dto.EndDate.Value) : null;
            userMed.ExpiryDate = adjustedExpiry;
            userMed.CurrentPillCount = dto.CurrentPillCount;
            userMed.InitialPillCount = dto.InitialPillCount;
            userMed.LowStockThreshold = dto.LowStockThreshold;
            userMed.NotificationActive = dto.NotificationActive;

            // PillsPerDose: save provided value (null means clear/unset for the details endpoint
            // which is a full-replace style endpoint like the original fields above)
            userMed.PillsPerDose = dto.PillsPerDose;

            // ── Apply schedule fields ─────────────────────────────────────────
            if (effectiveScheduleType == "CustomTimes" && hasCustomTimes)
            {
                userMed.FirstDoseTime = resolvedDoseTimes![0];
                userMed.DosesPerPeriod = resolvedDoseTimes.Count;
                userMed.PeriodUnit = "Day";
                userMed.PeriodValue = 1;
                userMed.IntervalHours = null;
            }
            else if (effectiveScheduleType == "Interval")
            {
                userMed.IntervalHours = dto.IntervalHours;
                userMed.FirstDoseTime = dto.FirstDoseTime.HasValue ? TimeOnly.FromTimeSpan(dto.FirstDoseTime.Value) : userMed.FirstDoseTime;
                userMed.DosesPerPeriod = null;
                userMed.PeriodUnit = null;
                userMed.PeriodValue = null;
            }
            else
            {
                // Backward-compatible path: apply fields directly
                userMed.DosesPerPeriod = dto.DosesPerPeriod;
                userMed.PeriodUnit = dto.PeriodUnit;
                userMed.PeriodValue = dto.PeriodValue;
                userMed.FirstDoseTime = dto.FirstDoseTime.HasValue ? TimeOnly.FromTimeSpan(dto.FirstDoseTime.Value) : null;
                userMed.IntervalHours = dto.IntervalHours;
            }

            await _context.SaveChangesAsync();

            // ── Generate/regenerate schedules ─────────────────────────────────
            if (effectiveScheduleType == "CustomTimes" && hasCustomTimes)
                await _scheduleService.RegenerateScheduleWithDoseTimesAsync(userMed, resolvedDoseTimes!);
            else
                await _scheduleService.GenerateScheduleAsync(userMed);

            bool expiryWasAdjusted = adjustedExpiry != originalExpiry;

            return Ok(new
            {
                Message = "Medication details added successfully.",
                ExpiryDate = adjustedExpiry,
                ExpiryAdjusted = expiryWasAdjusted,
                ExpiryAdjustedNote = expiryWasAdjusted
                    ? (string?)$"Expiry date adjusted from {originalExpiry:dd/MM/yyyy} to {adjustedExpiry:dd/MM/yyyy} because this is a liquid medication (opened shelf life = 3 months)."
                    : null
            });
        }

        // ================= HELPERS =================
        private int GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim == null) throw new UnauthorizedAccessException();
            return int.Parse(claim.Value);
        }

        private static DateOnly? AdjustExpiryForLiquid(DateOnly? originalExpiry, string? dosageForm)
        {
            if (string.IsNullOrWhiteSpace(dosageForm) || originalExpiry == null)
                return originalExpiry;

            var liquidForms = new[]
            {
                "Syrup", "Suspension", "Oral_Solution", "Oral_Drop",
                "Oral Drops", "Ampoule", "Vial_powder", "Ophthalmic Solution",
                "Emulgel", "Gel"
            };

            bool isLiquid = liquidForms.Any(f =>
                dosageForm.Equals(f, StringComparison.OrdinalIgnoreCase));

            if (!isLiquid) return originalExpiry;

            return originalExpiry.Value.AddMonths(-3);
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