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
    [ApiController]
    [Authorize]
    public class SchedulesController : ControllerBase
    {
        private readonly IScheduleService _scheduleService;
        private readonly AppDbContext _context;

        public SchedulesController(IScheduleService scheduleService, AppDbContext context)
        {
            _scheduleService = scheduleService;
            _context = context;
        }

        // ── GET /api/medications/{userMedId}/schedules ────────────────────────
        [HttpGet("api/medications/{userMedId:int}/schedules")]
        public async Task<ActionResult<List<MedicationScheduleDto>>> GetSchedulesForMedication(
            int userMedId,
            [FromQuery] string lang = "en")
        {
            var userId = GetUserId();
            var schedules = await _scheduleService.GetSchedulesForMedicationAsync(userMedId, userId, lang);

            if (schedules.Count == 0)
                return NotFound(new { message = $"No schedules found for medication {userMedId}, or access denied." });

            return Ok(schedules);
        }

        // ── GET /api/users/me/today-schedules (User) ──────────────────────────
        [HttpGet("api/users/me/today-schedules")]
        public async Task<ActionResult<List<MedicationScheduleDto>>> GetMyTodaySchedules(
            [FromQuery] string lang = "en")
        {
            var userId = GetUserId();
            return Ok(await _scheduleService.GetTodaySchedulesAsync(userId, lang));
        }

        // ── GET /api/users/{userId}/today-schedules (Admin) ───────────────────
        [HttpGet("api/users/{userId:int}/today-schedules")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<List<MedicationScheduleDto>>> GetTodaySchedulesForUser(
            int userId,
            [FromQuery] string lang = "en")
        {
            return Ok(await _scheduleService.GetTodaySchedulesAsync(userId, lang));
        }

        // ── GET /api/users/me/schedules-by-date (User) ────────────────────────
        [HttpGet("api/users/me/schedules-by-date")]
        public async Task<ActionResult<List<MedicationScheduleDto>>> GetMySchedulesByDate(
            [FromQuery] string? date,
            [FromQuery] string lang = "en")
        {
            var userId = GetUserId();

            if (string.IsNullOrWhiteSpace(date))
                return BadRequest(new { message = "The 'date' query parameter is required. Example: ?date=2026-03-14" });

            if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var parsedDate))
                return BadRequest(new { message = $"Invalid date format '{date}'. Use yyyy-MM-dd." });

            return Ok(await _scheduleService.GetSchedulesByDateAsync(userId, parsedDate, lang));
        }

        // ── GET /api/users/{userId}/schedules-by-date (Admin) ─────────────────
        [HttpGet("api/users/{userId:int}/schedules-by-date")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<List<MedicationScheduleDto>>> GetSchedulesByDateForUser(
            int userId,
            [FromQuery] string? date,
            [FromQuery] string lang = "en")
        {
            if (string.IsNullOrWhiteSpace(date))
                return BadRequest(new { message = "The 'date' query parameter is required. Example: ?date=2026-03-14" });

            if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var parsedDate))
                return BadRequest(new { message = $"Invalid date format '{date}'. Use yyyy-MM-dd." });

            return Ok(await _scheduleService.GetSchedulesByDateAsync(userId, parsedDate, lang));
        }

        // ── POST /api/schedules/{scheduleId}/take ─────────────────────────────
        [HttpPost("api/schedules/{scheduleId:int}/take")]
        public async Task<ActionResult<TakeDoseResult>> TakeDose(int scheduleId)
        {
            var userId = GetUserId();
            var result = await _scheduleService.TakeDoseAsync(scheduleId, userId);

            if (!result.Succeeded)
                return result.Error!.Contains("not found")
                    ? NotFound(new { message = result.Error })
                    : BadRequest(new { message = result.Error });

            return Ok(result);
        }

        // ── POST /api/schedules/{scheduleId}/snooze ───────────────────────────
        [HttpPost("api/schedules/{scheduleId:int}/snooze")]
        public async Task<ActionResult<SnoozeResult>> SnoozeDose(int scheduleId, [FromQuery] int minutes = 15)
        {
            if (minutes <= 0)
            {
                return BadRequest(new { message = "Snooze duration must be a positive number of minutes." });
            }

            var userId = GetUserId();
            var result = await _scheduleService.SnoozeAsync(scheduleId, userId, minutes);

            if (!result.Succeeded)
                return result.Error!.Contains("not found")
                    ? NotFound(new { message = result.Error })
                    : BadRequest(new { message = result.Error });

            return Ok(result);
        }

        // ── POST /api/schedules/{scheduleId}/skip ─────────────────────────────
        [HttpPost("api/schedules/{scheduleId:int}/skip")]
        public async Task<ActionResult<SkipDoseResult>> SkipDose(
            int scheduleId,
            [FromBody] SkipDoseRequestDto dto)
        {
            var userId = GetUserId();
            var result = await _scheduleService.SkipDoseAsync(scheduleId, userId, dto.Reason, dto.Note);

            if (!result.Succeeded)
                return result.Error!.Contains("not found")
                    ? NotFound(new { message = result.Error })
                    : BadRequest(new { message = result.Error });

            return Ok(result);
        }

        // ── PATCH /api/schedules/{scheduleId}/status ──────────────────────────
        [HttpPatch("api/schedules/{scheduleId:int}/status")]
        public async Task<ActionResult> UpdateScheduleStatus(
            int scheduleId, [FromBody] UpdateScheduleStatusDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Status))
                return BadRequest(new { message = "Status is required." });

            var userId = GetUserId();

            bool updated;
            try
            {
                updated = await _scheduleService.UpdateScheduleStatusAsync(scheduleId, dto.Status, userId);
            }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }

            if (!updated)
                return NotFound(new { message = $"Schedule {scheduleId} not found or access denied." });

            return Ok(new { message = $"Schedule {scheduleId} marked as {dto.Status}." });
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        [HttpGet("api/users/me/adherence-summary")]
        public async Task<ActionResult<AdherenceSummaryDto>> GetMyAdherenceSummary(
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to)
        {
            var userId = GetUserId();
            var range = ResolveRange(from, to);

            var userMeds = await _context.UserMedications
                .Include(um => um.Medication)
                .Where(um => um.UserId == userId)
                .OrderBy(um => um.MedicationName)
                .ToListAsync();

            var userMedIds = userMeds.Select(um => um.Id).ToList();
            var schedulesQuery = _context.MedicationSchedules
                .Include(s => s.UserMedication)
                    .ThenInclude(um => um.Medication)
                .Where(s => userMedIds.Contains(s.UserMedicationId)
                    && s.UserMedication!.MedicationUseType != "AsNeeded");

            var schedules = await WhereDoseActionInRange(schedulesQuery, range.From, range.To)
                .ToListAsync();

            schedules = schedules
                .OrderBy(s => GetDoseEventAt(s) ?? s.ScheduledAt)
                .ToList();

            var summary = BuildAdherenceSummary(schedules, range.From, range.To, null);
            summary.Medications = userMeds
                .Select(um => BuildMedicationAdherenceSummary(
                    um,
                    schedules.Where(s => s.UserMedicationId == um.Id).ToList(),
                    range.From,
                    range.To))
                .ToList();

            return Ok(summary);
        }

        [HttpGet("api/medications/{userMedId:int}/adherence")]
        public async Task<ActionResult<AdherenceSummaryDto>> GetMedicationAdherence(
            int userMedId,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to)
        {
            var userId = GetUserId();
            var range = ResolveRange(from, to);

            var medExists = await _context.UserMedications
                .AnyAsync(um => um.Id == userMedId && um.UserId == userId);
            if (!medExists)
                return NotFound(new { message = "Medication not found or access denied." });

            var schedulesQuery = _context.MedicationSchedules
                .Include(s => s.UserMedication)
                    .ThenInclude(um => um.Medication)
                .Where(s => s.UserMedicationId == userMedId
                    && s.UserMedication!.UserId == userId
                    && s.UserMedication.MedicationUseType != "AsNeeded");

            var schedules = await WhereDoseActionInRange(schedulesQuery, range.From, range.To)
                .ToListAsync();

            schedules = schedules
                .OrderBy(s => GetDoseEventAt(s) ?? s.ScheduledAt)
                .ToList();

            return Ok(BuildAdherenceSummary(schedules, range.From, range.To, userMedId));
        }

        [HttpGet("api/users/me/dose-history")]
        public async Task<ActionResult<List<DoseHistoryDto>>> GetMyDoseHistory(
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to)
        {
            var userId = GetUserId();
            var range = ResolveRange(from, to);

            var schedulesQuery = _context.MedicationSchedules
                .Include(s => s.UserMedication)
                    .ThenInclude(um => um.Medication)
                .Where(s => s.UserMedication!.UserId == userId
                    && s.UserMedication.MedicationUseType != "AsNeeded");

            var schedules = await WhereDoseActionInRange(schedulesQuery, range.From, range.To)
                .ToListAsync();

            var intakeLogs = await _context.MedicationIntakeLogs
                .Include(l => l.UserMedication)
                    .ThenInclude(um => um.Medication)
                .Where(l => l.UserMedication!.UserId == userId
                    && l.UserMedication.MedicationUseType == "AsNeeded"
                    && l.TakenAt >= range.From
                    && l.TakenAt <= range.To)
                .ToListAsync();

            return Ok(schedules
                .OrderByDescending(s => GetDoseEventAt(s) ?? s.ScheduledAt)
                .Select(ToDoseHistoryDto)
                .Concat(intakeLogs
                    .OrderByDescending(l => l.TakenAt)
                    .Select(ToAsNeededDoseHistoryDto))
                .OrderByDescending(h => h.ActionAt ?? h.ScheduledAt)
                .ToList());
        }

        private int GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)
                ?? throw new UnauthorizedAccessException("NameIdentifier claim missing.");
            return int.Parse(claim.Value);
        }

        private static (DateTime From, DateTime To) ResolveRange(DateTime? from, DateTime? to)
        {
            var resolvedTo = to ?? DateTime.UtcNow;
            var resolvedFrom = from ?? resolvedTo.AddDays(-30);

            if (resolvedFrom > resolvedTo)
                (resolvedFrom, resolvedTo) = (resolvedTo, resolvedFrom);

            return (DateTime.SpecifyKind(resolvedFrom, DateTimeKind.Utc),
                DateTime.SpecifyKind(resolvedTo, DateTimeKind.Utc));
        }

        private static IQueryable<MedicationSchedule> WhereDoseActionInRange(
            IQueryable<MedicationSchedule> query,
            DateTime from,
            DateTime to)
        {
            return query.Where(s =>
                (s.Status == MedicationStatus.Taken &&
                    ((s.TakenAt.HasValue && s.TakenAt.Value >= from && s.TakenAt.Value <= to) ||
                     (!s.TakenAt.HasValue && s.ScheduledAt >= from && s.ScheduledAt <= to))) ||
                (s.Status == MedicationStatus.Skipped &&
                    ((s.SkippedAt.HasValue && s.SkippedAt.Value >= from && s.SkippedAt.Value <= to) ||
                     (!s.SkippedAt.HasValue && s.ScheduledAt >= from && s.ScheduledAt <= to))) ||
                (s.Status == MedicationStatus.Missed &&
                    ((s.MissedAt.HasValue && s.MissedAt.Value >= from && s.MissedAt.Value <= to) ||
                     (!s.MissedAt.HasValue && s.ScheduledAt >= from && s.ScheduledAt <= to))));
        }

        private static DateTime? GetDoseEventAt(MedicationSchedule schedule)
        {
            return schedule.Status switch
            {
                MedicationStatus.Taken => schedule.TakenAt ?? schedule.ScheduledAt,
                MedicationStatus.Skipped => schedule.SkippedAt ?? schedule.ScheduledAt,
                MedicationStatus.Missed => schedule.MissedAt ?? schedule.ScheduledAt,
                _ => null
            };
        }

        private static AdherenceSummaryDto BuildAdherenceSummary(
            List<MedicationSchedule> schedules,
            DateTime from,
            DateTime to,
            int? userMedicationId)
        {
            var total = schedules.Count;
            var taken = schedules.Count(s => s.Status == MedicationStatus.Taken);
            var missed = schedules.Count(s => s.Status == MedicationStatus.Missed);
            var skipped = schedules.Count(s => s.Status == MedicationStatus.Skipped);
            var late = schedules.Count(IsLateDose);

            var dayStats = schedules
                .GroupBy(s => (GetDoseEventAt(s) ?? s.ScheduledAt).Date)
                .Select(g => new
                {
                    Day = g.Key,
                    Rate = g.Count() == 0 ? 0 : (decimal)g.Count(s => s.Status == MedicationStatus.Taken) / g.Count()
                })
                .ToList();

            var medicationName = schedules.FirstOrDefault()?.UserMedication is { } userMed
                ? UserMedicationFeatureHelper.GetDisplayName(userMed)
                : null;

            return new AdherenceSummaryDto
            {
                UserMedicationId = userMedicationId,
                MedicationName = medicationName,
                From = from,
                To = to,
                TotalDoses = total,
                TakenDoses = taken,
                MissedDoses = missed,
                SkippedDoses = skipped,
                LateDoses = late,
                AdherenceRate = total == 0 ? 0 : Math.Round((decimal)taken / total * 100, 2),
                CurrentStreak = CalculateCurrentStreak(schedules),
                BestDay = dayStats.Count == 0
                    ? null
                    : dayStats.OrderByDescending(d => d.Rate).ThenBy(d => d.Day).First().Day.ToString("yyyy-MM-dd"),
                WorstDay = dayStats.Count == 0
                    ? null
                    : dayStats.OrderBy(d => d.Rate).ThenBy(d => d.Day).First().Day.ToString("yyyy-MM-dd")
            };
        }

        private static MedicationAdherenceSummaryDto BuildMedicationAdherenceSummary(
            UserMedication userMedication,
            List<MedicationSchedule> schedules,
            DateTime from,
            DateTime to)
        {
            var total = schedules.Count;
            var taken = schedules.Count(s => s.Status == MedicationStatus.Taken);
            var missed = schedules.Count(s => s.Status == MedicationStatus.Missed);
            var skipped = schedules.Count(s => s.Status == MedicationStatus.Skipped);
            var late = schedules.Count(IsLateDose);

            var dayStats = schedules
                .GroupBy(s => (GetDoseEventAt(s) ?? s.ScheduledAt).Date)
                .Select(g => new
                {
                    Day = g.Key,
                    Rate = g.Count() == 0 ? 0 : (decimal)g.Count(s => s.Status == MedicationStatus.Taken) / g.Count()
                })
                .ToList();

            return new MedicationAdherenceSummaryDto
            {
                UserMedicationId = userMedication.Id,
                MedicationName = UserMedicationFeatureHelper.GetDisplayName(userMedication),
                From = from,
                To = to,
                TotalDoses = total,
                TakenDoses = taken,
                MissedDoses = missed,
                SkippedDoses = skipped,
                LateDoses = late,
                AdherenceRate = total == 0 ? 0 : Math.Round((decimal)taken / total * 100, 2),
                CurrentStreak = CalculateCurrentStreak(schedules),
                BestDay = dayStats.Count == 0
                    ? null
                    : dayStats.OrderByDescending(d => d.Rate).ThenBy(d => d.Day).First().Day.ToString("yyyy-MM-dd"),
                WorstDay = dayStats.Count == 0
                    ? null
                    : dayStats.OrderBy(d => d.Rate).ThenBy(d => d.Day).First().Day.ToString("yyyy-MM-dd")
            };
        }

        private static DoseHistoryDto ToDoseHistoryDto(MedicationSchedule schedule)
        {
            return new DoseHistoryDto
            {
                ScheduleId = schedule.Id,
                UserMedicationId = schedule.UserMedicationId,
                MedicationName = UserMedicationFeatureHelper.GetDisplayName(schedule.UserMedication),
                ScheduledAt = FormatUtc(schedule.ScheduledAt),
                Status = schedule.Status.ToString(),
                TakenAt = FormatUtc(schedule.TakenAt),
                SkippedAt = FormatUtc(schedule.SkippedAt),
                MissedAt = FormatUtc(schedule.MissedAt),
                ActionAt = FormatUtc(GetDoseEventAt(schedule)),
                IsAsNeeded = false,
                IsLate = IsLateDose(schedule),
                MissedReason = schedule.MissedReason,
                ActionNote = schedule.ActionNote,
                Notes = schedule.Notes
            };
        }

        private static DoseHistoryDto ToAsNeededDoseHistoryDto(MedicationIntakeLog log)
        {
            return new DoseHistoryDto
            {
                ScheduleId = log.Id,
                UserMedicationId = log.UserMedicationId,
                MedicationName = UserMedicationFeatureHelper.GetDisplayName(log.UserMedication),
                ScheduledAt = FormatUtc(log.TakenAt),
                Status = "TakenNow",
                TakenAt = FormatUtc(log.TakenAt),
                ActionAt = FormatUtc(log.TakenAt),
                IsAsNeeded = true,
                IsLate = false,
                MissedReason = log.Reason,
                ActionNote = log.Notes,
                Notes = log.Notes
            };
        }

        private static bool IsLateDose(MedicationSchedule schedule)
            => schedule.Status == MedicationStatus.Taken
               && schedule.TakenAt.HasValue
               && schedule.TakenAt.Value > schedule.ScheduledAt.AddMinutes(15);

        private static string? FormatUtc(DateTime? value)
            => value.HasValue ? FormatUtc(value.Value) : null;

        private static string FormatUtc(DateTime value)
        {
            var utc = value.Kind == DateTimeKind.Utc
                ? value
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);
            return utc.ToString("yyyy-MM-ddTHH:mm:ssZ");
        }

        private static int CalculateCurrentStreak(List<MedicationSchedule> schedules)
        {
            var completed = schedules
                .Where(s => s.Status is MedicationStatus.Taken or MedicationStatus.Missed or MedicationStatus.Skipped)
                .OrderByDescending(s => GetDoseEventAt(s) ?? s.ScheduledAt)
                .ToList();

            var streak = 0;
            foreach (var schedule in completed)
            {
                if (schedule.Status != MedicationStatus.Taken)
                    break;
                streak++;
            }

            return streak;
        }
    }
}
