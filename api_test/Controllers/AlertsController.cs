using api_test.Models;
using api_test.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace api_test.Controllers
{
    /// <summary>
    /// GET    /api/users/{userId}/alerts              → كل الإشعارات
    /// GET    /api/users/{userId}/alerts/unread       → الغير مقروءة بس
    /// GET    /api/users/{userId}/alerts/unread-count → عدد الغير مقروءة (badge)
    /// PATCH  /api/alerts/{alertId}/read              → علّم واحد كمقروء
    /// PATCH  /api/users/{userId}/alerts/read-all     → علّم الكل كمقروء
    /// DELETE /api/alerts/{alertId}                   → امسح إشعار واحد
    /// DELETE /api/alerts/cleanup                     → امسح القديم (Admin)
    /// </summary>
    [ApiController]
    [Authorize]
    public class AlertsController : ControllerBase
    {
        private readonly IAlertService _alertService;

        public AlertsController(IAlertService alertService)
        {
            _alertService = alertService;
        }

        // ── GET /api/users/{userId}/alerts ────────────────────────────────────
        [HttpGet("api/users/{userId:int}/alerts")]
        public async Task<ActionResult<List<AlertDto>>> GetAll(int userId)
        {
            if (!CallerOwnsResource(userId)) return Forbid();

            var alerts = await _alertService.GetAllAlertsAsync(userId);
            return Ok(alerts);
        }

        // ── GET /api/users/{userId}/alerts/unread ─────────────────────────────
        [HttpGet("api/users/{userId:int}/alerts/unread")]
        public async Task<ActionResult<List<AlertDto>>> GetUnread(int userId)
        {
            if (!CallerOwnsResource(userId)) return Forbid();

            var alerts = await _alertService.GetUnreadAlertsAsync(userId);
            return Ok(alerts);
        }

        // ── GET /api/users/{userId}/alerts/unread-count ───────────────────────
        [HttpGet("api/users/{userId:int}/alerts/unread-count")]
        public async Task<ActionResult> GetUnreadCount(int userId)
        {
            if (!CallerOwnsResource(userId)) return Forbid();

            var count = await _alertService.GetUnreadCountAsync(userId);
            return Ok(new { UnreadCount = count });
        }

        // ── PATCH /api/alerts/{alertId}/read ─────────────────────────────────
        [HttpPatch("api/alerts/{alertId:int}/read")]
        public async Task<ActionResult> MarkAsRead(int alertId)
        {
            var userId = GetUserId();
            var success = await _alertService.MarkAsReadAsync(alertId, userId);

            if (!success)
                return NotFound(new { message = $"Alert {alertId} not found or access denied." });

            return Ok(new { message = "Alert marked as read." });
        }

        // ── PATCH /api/users/{userId}/alerts/read-all ─────────────────────────
        [HttpPatch("api/users/{userId:int}/alerts/read-all")]
        public async Task<ActionResult> MarkAllAsRead(int userId)
        {
            if (!CallerOwnsResource(userId)) return Forbid();

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

        // ── DELETE /api/alerts/cleanup ────────────────────────────────────────
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

        private bool CallerOwnsResource(int userId)
        {
            var callerId = GetUserId();
            return User.IsInRole("Admin") || callerId == userId;
        }
    }
}