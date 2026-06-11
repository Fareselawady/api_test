using api_test.Data;
using api_test.Entities;
using api_test.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace api_test.Controllers
{
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly AppDbContext _db;

        public AdminController(AppDbContext db)
        {
            _db = db;
        }

        [HttpGet("api/admin/premium/subscriptions")]
        public async Task<IActionResult> GetPremiumSubscriptions()
        {
            var now = DateTime.UtcNow;

            var users = await _db.Users
                .AsNoTracking()
                .Include(u => u.Role)
                .OrderByDescending(u => u.IsPremium)
                .ThenBy(u => u.PremiumEndDate)
                .Select(u => new
                {
                    u.Id,
                    u.Name,
                    u.Username,
                    u.Email,
                    u.Phone,
                    u.IsPremium,
                    u.PremiumStartDate,
                    u.PremiumEndDate,
                    Role = new { u.Role.RoleId, u.Role.RoleName }
                })
                .ToListAsync();

            return Ok(users.Select(u => new
            {
                u.Id,
                u.Name,
                u.Username,
                u.Email,
                u.Phone,
                u.IsPremium,
                u.PremiumStartDate,
                u.PremiumEndDate,
                IsActive = u.IsPremium && u.PremiumEndDate.HasValue && u.PremiumEndDate.Value > now,
                RemainingDays = u.IsPremium && u.PremiumEndDate.HasValue && u.PremiumEndDate.Value > now
                    ? (int?)Math.Ceiling((u.PremiumEndDate.Value - now).TotalDays)
                    : null,
                u.Role
            }));
        }

        [HttpPost("api/admin/users/{userId:int}/premium/activate")]
        public async Task<IActionResult> ActivatePremiumForUser(int userId, [FromBody] ActivatePremiumDto dto)
        {
            if (!Enum.TryParse<PremiumPlan>(dto.Plan, ignoreCase: true, out var plan))
                return BadRequest(new { message = "Invalid plan. Valid values: Month, ThreeMonths, Year." });

            var user = await _db.Users.FindAsync(userId);
            if (user == null)
                return NotFound(new { message = "User not found." });

            var now = DateTime.UtcNow;
            var startFrom = HasActivePremium(user, now) ? user.PremiumEndDate!.Value : now;

            var newEndDate = plan switch
            {
                PremiumPlan.Month => startFrom.AddMonths(1),
                PremiumPlan.ThreeMonths => startFrom.AddMonths(3),
                PremiumPlan.Year => startFrom.AddYears(1),
                _ => startFrom.AddMonths(1)
            };

            if (!HasActivePremium(user, now))
                user.PremiumStartDate = now;

            user.IsPremium = true;
            user.PremiumEndDate = newEndDate;

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
                userId,
                premiumStartDate = user.PremiumStartDate,
                premiumEndDate = user.PremiumEndDate,
                isPremium = true
            });
        }

        [HttpPost("api/admin/users/{userId:int}/premium/cancel")]
        public async Task<IActionResult> CancelPremiumForUser(int userId)
        {
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

            return Ok(new { message = "Premium subscription cancelled.", userId });
        }

        [HttpGet("api/admin/alerts")]
        public async Task<IActionResult> GetAlerts(
            [FromQuery] string? type = null,
            [FromQuery] bool? isRead = null,
            [FromQuery] int? userId = null)
        {
            var query = _db.Alerts
                .AsNoTracking()
                .Include(a => a.User)
                .Include(a => a.UserMedication)
                    .ThenInclude(um => um!.Medication)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(type))
                query = query.Where(a => a.Type == type);

            if (isRead.HasValue)
                query = query.Where(a => a.IsRead == isRead.Value);

            if (userId.HasValue)
                query = query.Where(a => a.UserId == userId.Value);

            var alerts = await query
                .OrderByDescending(a => a.CreatedAt)
                .Take(500)
                .Select(a => new
                {
                    a.Id,
                    a.UserId,
                    UserName = a.User.Name,
                    UserEmail = a.User.Email,
                    a.UserMedicationId,
                    a.MedicationScheduleId,
                    MedicationName = a.UserMedication == null
                        ? null
                        : a.UserMedication.MedicationName != string.Empty
                            ? a.UserMedication.MedicationName
                            : a.UserMedication.Medication != null
                                ? a.UserMedication.Medication.Trade_name
                                : null,
                    a.Type,
                    a.Title,
                    a.Message,
                    a.IsRead,
                    a.CreatedAt,
                    a.ScheduledAt
                })
                .ToListAsync();

            return Ok(alerts);
        }

        [HttpGet("api/admin/medicine-scans")]
        public async Task<IActionResult> GetMedicineScans(
            [FromQuery] bool? success = null,
            [FromQuery] int? userId = null)
        {
            var query = _db.MedicineScanHistories
                .AsNoTracking()
                .Include(s => s.User)
                .AsQueryable();

            if (success.HasValue)
                query = query.Where(s => s.Success == success.Value);

            if (userId.HasValue)
                query = query.Where(s => s.UserId == userId.Value);

            var scans = await query
                .OrderByDescending(s => s.CreatedAt)
                .Take(500)
                .Select(s => new
                {
                    s.Id,
                    s.UserId,
                    UserName = s.User.Name,
                    UserEmail = s.User.Email,
                    s.FileName,
                    s.ContentType,
                    s.FileSize,
                    s.Success,
                    s.MedicationName,
                    s.Message,
                    s.HttpStatusCode,
                    s.CreatedAt
                })
                .ToListAsync();

            return Ok(scans);
        }

        [HttpGet("api/admin/chatbot/history")]
        public IActionResult GetChatbotHistory()
        {
            return Ok(new
            {
                configured = false,
                message = "No chatbot persistence exists in the current backend project.",
                items = Array.Empty<object>()
            });
        }

        private static bool HasActivePremium(User user, DateTime now)
            => user.IsPremium
            && user.PremiumEndDate.HasValue
            && user.PremiumEndDate.Value > now;
    }
}
