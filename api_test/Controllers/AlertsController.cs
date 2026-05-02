using api_test.Models;
using api_test.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace api_test.Controllers
{
    
    [ApiController]
    [Authorize]
    public class AlertsController : ControllerBase
    {
        private readonly IAlertService _alertService;

        public AlertsController(IAlertService alertService)
        {
            _alertService = alertService;
        }

        // ── GET /api/users/me/alerts (User) ───────────────────────────────────
        [HttpGet("api/users/me/alerts")]
        public async Task<ActionResult<List<AlertDto>>> GetMyAll()
        {
            var userId = GetUserId();
            return Ok(await _alertService.GetAllAlertsAsync(userId));
        }

        // ── GET /api/users/{userId}/alerts (Admin) ────────────────────────────
        [HttpGet("api/users/{userId:int}/alerts")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<List<AlertDto>>> GetAllForUser(int userId)
        {
            return Ok(await _alertService.GetAllAlertsAsync(userId));
        }

        // ── GET /api/users/me/alerts/unread (User) ────────────────────────────
        [HttpGet("api/users/me/alerts/unread")]
        public async Task<ActionResult<List<AlertDto>>> GetMyUnread()
        {
            var userId = GetUserId();
            return Ok(await _alertService.GetUnreadAlertsAsync(userId));
        }

        // ── GET /api/users/{userId}/alerts/unread (Admin) ─────────────────────
        [HttpGet("api/users/{userId:int}/alerts/unread")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<List<AlertDto>>> GetUnreadForUser(int userId)
        {
            return Ok(await _alertService.GetUnreadAlertsAsync(userId));
        }

        // ── GET /api/users/me/alerts/unread-count (User) ──────────────────────
        [HttpGet("api/users/me/alerts/unread-count")]
        public async Task<ActionResult> GetMyUnreadCount()
        {
            var userId = GetUserId();
            var count = await _alertService.GetUnreadCountAsync(userId);
            return Ok(new { UnreadCount = count });
        }

        // ── GET /api/users/{userId}/alerts/unread-count (Admin) ───────────────
        [HttpGet("api/users/{userId:int}/alerts/unread-count")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> GetUnreadCountForUser(int userId)
        {
            var count = await _alertService.GetUnreadCountAsync(userId);
            return Ok(new { UnreadCount = count });
        }

        // ── PATCH /api/alerts/{alertId}/read ──────────────────────────────────
        [HttpPatch("api/alerts/{alertId:int}/read")]
        public async Task<ActionResult> MarkAsRead(int alertId)
        {
            var userId = GetUserId();
            var success = await _alertService.MarkAsReadAsync(alertId, userId);

            if (!success)
                return NotFound(new { message = $"Alert {alertId} not found or access denied." });

            return Ok(new { message = "Alert marked as read." });
        }

        // ── PATCH /api/users/me/alerts/read-all (User) ────────────────────────
        [HttpPatch("api/users/me/alerts/read-all")]
        public async Task<ActionResult> MarkMyAllAsRead()
        {
            var userId = GetUserId();
            var count = await _alertService.MarkAllAsReadAsync(userId);
            return Ok(new { message = $"{count} alert(s) marked as read." });
        }

        // ── PATCH /api/users/{userId}/alerts/read-all (Admin) ─────────────────
        [HttpPatch("api/users/{userId:int}/alerts/read-all")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> MarkAllAsReadForUser(int userId)
        {
            var count = await _alertService.MarkAllAsReadAsync(userId);
            return Ok(new { message = $"{count} alert(s) marked as read." });
        }

        // ── DELETE /api/alerts/{alertId} ──────────────────────────────────────
        [HttpDelete("api/alerts/{alertId:int}")]
        public async Task<ActionResult> DeleteAlert(int alertId)
        {
            var userId = GetUserId();
            var success = await _alertService.DeleteAlertAsync(alertId, userId);

            if (!success)
                return NotFound(new { message = $"Alert {alertId} not found or access denied." });

            return Ok(new { message = "Alert deleted." });
        }

        // ── DELETE /api/alerts/cleanup (Admin) ────────────────────────────────
        [HttpDelete("api/alerts/cleanup")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> Cleanup([FromQuery] int daysOld = 30)
        {
            var count = await _alertService.DeleteOldReadAlertsAsync(daysOld);
            return Ok(new { message = $"Deleted {count} old alert(s) older than {daysOld} days." });
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