using api_test.Data;
using api_test.Entities;
using api_test.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace api_test.Controllers
{
    [ApiController]
    [Route("api/premium")]
    [Authorize]
    public class PremiumController : ControllerBase
    {
        private readonly AppDbContext _db;

        public PremiumController(AppDbContext db)
        {
            _db = db;
        }

        // ── POST /api/premium/activate ────────────────────────────────────────
        [HttpPost("activate")]
        public async Task<IActionResult> Activate([FromBody] ActivatePremiumDto dto)
        {
            if (!Enum.TryParse<PremiumPlan>(dto.Plan, ignoreCase: true, out var plan))
                return BadRequest(new
                {
                    message = $"Invalid plan '{dto.Plan}'. Valid values: Month, ThreeMonths, Year."
                });

            var userId = GetUserId();
            var user = await _db.Users.FindAsync(userId);
            if (user == null)
                return NotFound(new { message = "User not found." });

            var now = DateTime.UtcNow;

            // Extend from existing end date if still active; otherwise start from now
            DateTime startFrom = HasActivePremium(user) ? user.PremiumEndDate!.Value : now;

            DateTime newEndDate = plan switch
            {
                PremiumPlan.Month => startFrom.AddMonths(1),
                PremiumPlan.ThreeMonths => startFrom.AddMonths(3),
                PremiumPlan.Year => startFrom.AddYears(1),
                _ => startFrom.AddMonths(1)
            };

            // Only update start date if not already premium (fresh activation)
            if (!HasActivePremium(user))
                user.PremiumStartDate = now;

            user.IsPremium = true;
            user.PremiumEndDate = newEndDate;

            // Internal alert
            _db.Alerts.Add(new Alert
            {
                UserId = userId,
                Type = "AdminMessage",
                Title = "Premium Activated",
                Message = $"Your Premium subscription is active until {newEndDate:yyyy-MM-dd}.",
                IsRead = false,
                ScheduledAt = now,
                CreatedAt = now
            });

            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "Premium activated successfully.",
                premiumStartDate = user.PremiumStartDate,
                premiumEndDate = user.PremiumEndDate,
                isPremium = true
            });
        }

        // ── POST /api/premium/cancel ──────────────────────────────────────────
        [HttpPost("cancel")]
        public async Task<IActionResult> Cancel()
        {
            var userId = GetUserId();
            var user = await _db.Users.FindAsync(userId);
            if (user == null)
                return NotFound(new { message = "User not found." });

            var now = DateTime.UtcNow;

            user.IsPremium = false;
            user.PremiumStartDate = null;
            user.PremiumEndDate = null;

            _db.Alerts.Add(new Alert
            {
                UserId = userId,
                Type = "AdminMessage",
                Title = "Premium Cancelled",
                Message = "Your Premium subscription has been cancelled.",
                IsRead = false,
                ScheduledAt = now,
                CreatedAt = now
            });

            await _db.SaveChangesAsync();

            return Ok(new { message = "Premium subscription cancelled." });
        }

        // ── GET /api/premium/me ───────────────────────────────────────────────
        [HttpGet("me")]
        public async Task<ActionResult<PremiumStatusDto>> GetPremiumStatus()
        {
            var userId = GetUserId();
            var user = await _db.Users.FindAsync(userId);
            if (user == null)
                return NotFound(new { message = "User not found." });

            bool isActive = HasActivePremium(user);

            int? remainingDays = null;
            if (isActive && user.PremiumEndDate.HasValue)
                remainingDays = (int)(user.PremiumEndDate.Value - DateTime.UtcNow).TotalDays;

            return Ok(new PremiumStatusDto
            {
                IsPremium = isActive,
                PremiumStartDate = isActive ? user.PremiumStartDate : null,
                PremiumEndDate = isActive ? user.PremiumEndDate : null,
                RemainingDays = remainingDays
            });
        }

        // ── Helper: expiry-aware premium check ────────────────────────────────
        public static bool HasActivePremium(User user)
            => user.IsPremium
            && user.PremiumEndDate.HasValue
            && user.PremiumEndDate.Value > DateTime.UtcNow;

        private int GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)
                ?? throw new UnauthorizedAccessException("NameIdentifier claim missing.");
            return int.Parse(claim.Value);
        }
    }
}