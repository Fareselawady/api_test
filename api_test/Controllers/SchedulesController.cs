using api_test.Models;
using api_test.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace api_test.Controllers
{
    /// <summary>
    /// Provides schedule and alert query endpoints:
    ///   GET  /api/medications/{userMedId}/schedules        → all schedules for a medication
    ///   GET  /api/users/{userId}/alerts                    → pending alerts for a user
    ///   GET  /api/users/{userId}/today-schedules           → today's schedules for a user
    ///   GET  /api/users/{userId}/schedules-by-date         → schedules for a specific date  ← NEW
    ///   PATCH /api/schedules/{scheduleId}/status           → mark Taken / Missed / Pending
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

        // ── GET /api/users/{userId}/alerts ────────────────────────────────────
        [HttpGet("api/users/{userId:int}/alerts")]
        public async Task<ActionResult<List<AlertDto>>> GetUserAlerts(int userId)
        {
            if (!CallerOwnsResource(userId))
                return Forbid();

            var alerts = await _scheduleService.GetPendingAlertsAsync(userId);
            return Ok(alerts);
        }

        // ── GET /api/users/{userId}/today-schedules ───────────────────────────
        [HttpGet("api/users/{userId:int}/today-schedules")]
        public async Task<ActionResult<List<MedicationScheduleDto>>> GetTodaySchedules(int userId)
        {
            if (!CallerOwnsResource(userId))
                return Forbid();

            var schedules = await _scheduleService.GetTodaySchedulesAsync(userId);
            return Ok(schedules);
        }

        // ── GET /api/users/{userId}/schedules-by-date ─────────────────────────
        /// <summary>
        /// Returns all medication schedules for a user on a specific date.
        /// Used by the mobile home screen week view when the user taps a day.
        ///
        /// Example request:
        ///   GET /api/users/1/schedules-by-date?date=2026-03-12
        ///
        /// Rules:
        ///   • The caller must be the owner of the resource (or an Admin).
        ///   • date must be supplied in ISO 8601 format: yyyy-MM-dd
        ///   • Returns 200 OK with [] when no schedules exist for that day.
        ///   • Returns 400 Bad Request when date is missing or cannot be parsed.
        /// </summary>
        [HttpGet("api/users/{userId:int}/schedules-by-date")]
        public async Task<ActionResult<List<MedicationScheduleDto>>> GetSchedulesByDate(
            int userId,
            [FromQuery] string? date)   // received as string so we control the error message
        {
            // 1. Ownership check — same pattern as GetUserAlerts / GetTodaySchedules
            if (!CallerOwnsResource(userId))
                return Forbid();

            // 2. Validate the date query parameter
            if (string.IsNullOrWhiteSpace(date))
                return BadRequest(new { message = "The 'date' query parameter is required. Example: ?date=2026-03-12" });

            if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var parsedDate))
                return BadRequest(new { message = $"Invalid date format '{date}'. Use yyyy-MM-dd, e.g. 2026-03-12" });

            // 3. Delegate to the service layer
            var schedules = await _scheduleService.GetSchedulesByDateAsync(userId, parsedDate);

            // 4. Always return 200 — empty list is a valid result (no doses that day)
            return Ok(schedules);
        }

        // ── PATCH /api/schedules/{scheduleId}/status ──────────────────────────
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

        /// <summary>
        /// Ensures the authenticated user matches the userId in the route.
        /// Admins bypass this check and can access any user's data.
        /// </summary>
        private bool CallerOwnsResource(int userId)
        {
            var callerId = GetUserId();
            var isAdmin = User.IsInRole("Admin");
            return isAdmin || callerId == userId;
        }
    }
}