using api_test.Models;
using api_test.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace api_test.Controllers
{
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
        public async Task<ActionResult<SkipDoseResult>> SkipDose(int scheduleId)
        {
            var userId = GetUserId();
            var result = await _scheduleService.SkipDoseAsync(scheduleId, userId);

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
        private int GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)
                ?? throw new UnauthorizedAccessException("NameIdentifier claim missing.");
            return int.Parse(claim.Value);
        }
    }
}