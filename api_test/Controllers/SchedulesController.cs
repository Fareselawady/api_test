using api_test.Models;
using api_test.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace api_test.Controllers
{
    /// <summary>
    /// GET   /api/medications/{userMedId}/schedules   → all schedules for a medication
    /// GET   /api/users/{userId}/today-schedules      → today's schedules
    /// GET   /api/users/{userId}/schedules-by-date    → schedules for a specific date
    /// POST  /api/schedules/{scheduleId}/take         → mark as Taken + deduct pills
    /// PATCH /api/schedules/{scheduleId}/status       → mark as Pending / Missed
    /// </summary>
    [ApiController]
    [Authorize]
    public class SchedulesController : ControllerBase
    {
        private readonly IScheduleService _scheduleService;

        public SchedulesController(IScheduleService scheduleService)
        {
            _scheduleService = scheduleService;
        }

        // ── GET /api/medications/{userMedId}/schedules ────────────────────────
        [HttpGet("api/medications/{userMedId:int}/schedules")]
        public async Task<ActionResult<List<MedicationScheduleDto>>> GetSchedulesForMedication(
            int userMedId)
        {
            var userId = GetUserId();
            var schedules = await _scheduleService.GetSchedulesForMedicationAsync(userMedId, userId);

            if (schedules.Count == 0)
                return NotFound(new { message = $"No schedules found for medication {userMedId}, or access denied." });

            return Ok(schedules);
        }

        // ── GET /api/users/{userId}/today-schedules ───────────────────────────
        [HttpGet("api/users/{userId:int}/today-schedules")]
        public async Task<ActionResult<List<MedicationScheduleDto>>> GetTodaySchedules(int userId)
        {
            if (!CallerOwnsResource(userId)) return Forbid();

            var schedules = await _scheduleService.GetTodaySchedulesAsync(userId);
            return Ok(schedules);
        }

        // ── GET /api/users/{userId}/schedules-by-date ─────────────────────────
        [HttpGet("api/users/{userId:int}/schedules-by-date")]
        public async Task<ActionResult<List<MedicationScheduleDto>>> GetSchedulesByDate(
            int userId,
            [FromQuery] string? date)
        {
            if (!CallerOwnsResource(userId)) return Forbid();

            if (string.IsNullOrWhiteSpace(date))
                return BadRequest(new { message = "The 'date' query parameter is required. Example: ?date=2026-03-12" });

            if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var parsedDate))
                return BadRequest(new { message = $"Invalid date format '{date}'. Use yyyy-MM-dd, e.g. 2026-03-12" });

            var schedules = await _scheduleService.GetSchedulesByDateAsync(userId, parsedDate);
            return Ok(schedules);
        }

        // ── POST /api/schedules/{scheduleId}/take ─────────────────────────────
        /// <summary>
        /// Marks a dose as Taken, deducts pill count, and fires a LowStock alert
        /// immediately if CurrentPillCount drops to or below LowStockThreshold.
        /// </summary>
        [HttpPost("api/schedules/{scheduleId:int}/take")]
        public async Task<ActionResult<TakeDoseResult>> TakeDose(int scheduleId)
        {
            var userId = GetUserId();
            var result = await _scheduleService.TakeDoseAsync(scheduleId, userId);

            if (!result.Succeeded)
            {
                if (result.Error!.Contains("not found"))
                    return NotFound(new { message = result.Error });

                return BadRequest(new { message = result.Error });
            }

            return Ok(result);
        }

        // ── PATCH /api/schedules/{scheduleId}/status ──────────────────────────
        /// <summary>Update status to Pending or Missed. Use /take for Taken.</summary>
        [HttpPatch("api/schedules/{scheduleId:int}/status")]
        public async Task<ActionResult> UpdateScheduleStatus(
            int scheduleId,
            [FromBody] UpdateScheduleStatusDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Status))
                return BadRequest(new { message = "Status is required." });

            var userId = GetUserId();

            bool updated;
            try
            {
                updated = await _scheduleService.UpdateScheduleStatusAsync(scheduleId, dto.Status, userId);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }

            if (!updated)
                return NotFound(new { message = $"Schedule {scheduleId} not found or access denied." });

            return Ok(new { message = $"Schedule {scheduleId} marked as {dto.Status}." });
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private int GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)
                ?? throw new UnauthorizedAccessException("NameIdentifier claim missing.");
            return int.Parse(claim.Value);
        }

        private bool CallerOwnsResource(int userId)
        {
            var callerId = GetUserId();
            return User.IsInRole("Admin") || callerId == userId;
        }
    }
}