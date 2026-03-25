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
    [Route("api/[controller]")]
    [Authorize]
    public class SurveysController : ControllerBase
    {
        private readonly AppDbContext _db;

        public SurveysController(AppDbContext db)
        {
            _db = db;
        }

        // ── POST /api/surveys ─────────────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> CreateSurvey([FromBody] CreateSurveyDto dto)
        {
            var validTypes = new[] { "GeneralFeedback", "Complaint", "MedicationRequest", "Other" };
            if (!validTypes.Contains(dto.Type))
                return BadRequest(new { message = $"Invalid type. Must be: {string.Join(", ", validTypes)}" });

            if (string.IsNullOrWhiteSpace(dto.Message))
                return BadRequest(new { message = "Message is required." });

            var userId = GetUserId();
            var now = DateTime.UtcNow;

            // 1. حفظ الـ survey
            var survey = new Survey
            {
                UserId = userId,
                Type = dto.Type,
                Message = dto.Message,
                CreatedAt = now
            };
            _db.Surveys.Add(survey);
            await _db.SaveChangesAsync();

            // 2. إشعار للأدمن — جيب كل الأدمنز
            var adminIds = await _db.Users
                .Where(u => u.Role.RoleName == "Admin")
                .Select(u => u.Id)
                .ToListAsync();

            var user = await _db.Users.FindAsync(userId);

            foreach (var adminId in adminIds)
            {
                _db.Alerts.Add(new Alert
                {
                    UserId = adminId,
                    SurveyId = survey.Id,
                    Type = "NewSurvey",
                    Title = $"New {dto.Type} from {user?.Name ?? "User"}",
                    Message = $"User \"{user?.Name}\" (ID: {userId}) submitted a {dto.Type}: {dto.Message}",
                    IsRead = false,
                    ScheduledAt = now,
                    CreatedAt = now
                });
            }

            await _db.SaveChangesAsync();

            return Ok(new { message = "Survey submitted successfully.", surveyId = survey.Id });
        }

        // ── GET /api/surveys/my ───────────────────────────────────────────────
        [HttpGet("my")]
        public async Task<IActionResult> GetMySurveys()
        {
            var userId = GetUserId();

            var surveys = await _db.Surveys
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.CreatedAt)
                .Select(s => new
                {
                    s.Id,
                    s.Type,
                    s.Message,
                    CreatedAt = s.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ")
                })
                .ToListAsync();

            var replies = await _db.AdminReplies
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new
                {
                    r.Id,
                    r.Message,
                    CreatedAt = r.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ")
                })
                .ToListAsync();

            return Ok(new { surveys, adminReplies = replies });
        }

        // ── GET /api/surveys ──────────────────────────────────────────────────
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllSurveys([FromQuery] string? type = null)
        {
            var query = _db.Surveys
                .Include(s => s.User)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(type))
                query = query.Where(s => s.Type == type);

            var surveys = await query
                .OrderByDescending(s => s.CreatedAt)
                .Select(s => new
                {
                    s.Id,
                    s.UserId,
                    UserName = s.User.Name,
                    UserEmail = s.User.Email,
                    s.Type,
                    s.Message,
                    CreatedAt = s.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ")
                })
                .ToListAsync();

            return Ok(surveys);
        }

        // ── GET /api/surveys/user/{userId} ────────────────────────────────────
        [HttpGet("user/{userId:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetSurveysByUser(int userId)
        {
            var surveys = await _db.Surveys
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.CreatedAt)
                .Select(s => new
                {
                    s.Id,
                    s.Type,
                    s.Message,
                    CreatedAt = s.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ")
                })
                .ToListAsync();

            var replies = await _db.AdminReplies
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new
                {
                    r.Id,
                    r.Message,
                    CreatedAt = r.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ")
                })
                .ToListAsync();

            return Ok(new { surveys, adminReplies = replies });
        }

        // ── POST /api/surveys/reply/{userId} ──────────────────────────────────
        [HttpPost("reply/{userId:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ReplyToUser(int userId, [FromBody] AdminReplyDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Message))
                return BadRequest(new { message = "Message is required." });

            var userExists = await _db.Users.AnyAsync(u => u.Id == userId);
            if (!userExists)
                return NotFound(new { message = "User not found." });

            var now = DateTime.UtcNow;

            // 1. حفظ الرد
            var reply = new AdminReply
            {
                UserId = userId,
                Message = dto.Message,
                CreatedAt = now
            };
            _db.AdminReplies.Add(reply);
            await _db.SaveChangesAsync();

            // 2. إشعار لليوزر
            _db.Alerts.Add(new Alert
            {
                UserId = userId,
                AdminReplyId = reply.Id,
                Type = "AdminReply",
                Title = "New message from Admin",
                Message = dto.Message,
                IsRead = false,
                ScheduledAt = now,
                CreatedAt = now
            });

            await _db.SaveChangesAsync();

            return Ok(new { message = "Reply sent successfully.", replyId = reply.Id });
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

  
