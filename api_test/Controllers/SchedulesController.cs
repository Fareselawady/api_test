using api_test.Models;
using api_test.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace api_test.Controllers
{
    /// <summary>
    /// Provides schedule and alert query endpoints:
    ///   GET /api/medications/{userMedId}/schedules     → all schedules for a medication
    ///   GET /api/users/{userId}/alerts                 → pending alerts for a user
    ///   GET /api/users/{userId}/today-schedules        → today's schedules for a user
    ///   PATCH /api/schedules/{scheduleId}/status       → mark Taken / Missed / Pending
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
        /// <summary>
        /// Returns every schedule entry for the specified UserMedication.
        /// Only the authenticated user's own medications are accessible.
        /// </summary>
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
        /// <summary>
        /// Returns all unread (pending) alerts for a user.
        /// Users can only access their own alerts.
        /// </summary>
        [HttpGet("api/users/{userId:int}/alerts")]
        public async Task<ActionResult<List<AlertDto>>> GetUserAlerts(int userId)
        {
            if (!CallerOwnsResource(userId))
                return Forbid();

            var alerts = await _scheduleService.GetPendingAlertsAsync(userId);
            return Ok(alerts);   // empty list is fine — 200 with []
        }

        // ── GET /api/users/{userId}/today-schedules ───────────────────────────
        /// <summary>
        /// Returns all schedules whose ScheduledAt falls on today (UTC) for a user.
        /// </summary>
        [HttpGet("api/users/{userId:int}/today-schedules")]
        public async Task<ActionResult<List<MedicationScheduleDto>>> GetTodaySchedules(int userId)
        {
            if (!CallerOwnsResource(userId))
                return Forbid();

            var schedules = await _scheduleService.GetTodaySchedulesAsync(userId);
            return Ok(schedules);
        }

        // ── PATCH /api/schedules/{scheduleId}/status ──────────────────────────
        /// <summary>
        /// Updates the status of a schedule entry.
        /// Accepted values: "Taken", "Missed", "Pending"
        /// </summary>
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

        // ── Helper ────────────────────────────────────────────────────────────
        private int GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)
                ?? throw new UnauthorizedAccessException("NameIdentifier claim missing.");
            return int.Parse(claim.Value);
        }

        /// <summary>
        /// Ensures the authenticated user is the same as the userId in the route.
        /// Admins bypass this check (they can see any user's data).
        /// </summary>
        private bool CallerOwnsResource(int userId)
        {
            var callerId = GetUserId();
            var isAdmin = User.IsInRole("Admin");
            return isAdmin || callerId == userId;
        }
    }
}