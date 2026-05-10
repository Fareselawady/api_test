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
    [Authorize]
    public class SupportController : ControllerBase
    {
        private readonly AppDbContext _db;

        public SupportController(AppDbContext db)
        {
            _db = db;
        }

        // ── POST /api/support ─────────────────────────────────────────────────
        [HttpPost("api/support")]
        public async Task<IActionResult> SubmitTicket([FromBody] CreateSupportTicketDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Message))
                return BadRequest(new { message = "Message is required." });

            if (!Enum.TryParse<SupportCategory>(dto.Category, ignoreCase: true, out var category))
                return BadRequest(new
                {
                    message = $"Invalid category '{dto.Category}'. Valid values: " +
                              string.Join(", ", Enum.GetNames<SupportCategory>())
                });

            var userId = GetUserId();
            var now = DateTime.UtcNow;

            var ticket = new SupportTicket
            {
                UserId = userId,
                Category = category,
                Message = dto.Message,
                Status = SupportStatus.Open,
                CreatedAt = now
            };

            _db.SupportTickets.Add(ticket);
            await _db.SaveChangesAsync();

            // Create internal alert for the user (reusing existing Alert system)
            _db.Alerts.Add(new Alert
            {
                UserId = userId,
                Type = "AdminMessage",
                Title = "Support Request Submitted",
                Message = "Your support request was submitted successfully.",
                IsRead = false,
                ScheduledAt = now,
                CreatedAt = now
            });

            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "Support request submitted successfully.",
                ticketId = ticket.Id,
                status = ticket.Status.ToString()
            });
        }

        // ── GET /api/support/my-tickets ───────────────────────────────────────
        [HttpGet("api/support/my-tickets")]
        public async Task<ActionResult<List<SupportTicketDto>>> GetMyTickets()
        {
            var userId = GetUserId();

            var tickets = await _db.SupportTickets
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new SupportTicketDto
                {
                    Id = t.Id,
                    UserId = t.UserId,
                    Category = t.Category.ToString(),
                    Message = t.Message,
                    Status = t.Status.ToString(),
                    AdminReply = t.AdminReply,
                    CreatedAt = t.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    RepliedAt = t.RepliedAt.HasValue
                        ? t.RepliedAt.Value.ToString("yyyy-MM-ddTHH:mm:ssZ")
                        : null
                })
                .ToListAsync();

            return Ok(tickets);
        }

        // ── GET /api/admin/support ────────────────────────────────────────────
        [HttpGet("api/admin/support")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<List<SupportTicketDto>>> GetAllTickets(
            [FromQuery] string? category = null,
            [FromQuery] string? status = null)
        {
            var query = _db.SupportTickets
                .Include(t => t.User)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(category))
            {
                if (!Enum.TryParse<SupportCategory>(category, ignoreCase: true, out var cat))
                    return BadRequest(new { message = $"Invalid category '{category}'." });
                query = query.Where(t => t.Category == cat);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                if (!Enum.TryParse<SupportStatus>(status, ignoreCase: true, out var st))
                    return BadRequest(new { message = $"Invalid status '{status}'." });
                query = query.Where(t => t.Status == st);
            }

            var tickets = await query
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new SupportTicketDto
                {
                    Id = t.Id,
                    UserId = t.UserId,
                    UserName = t.User.Name,
                    UserEmail = t.User.Email,
                    Category = t.Category.ToString(),
                    Message = t.Message,
                    Status = t.Status.ToString(),
                    AdminReply = t.AdminReply,
                    CreatedAt = t.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    RepliedAt = t.RepliedAt.HasValue
                        ? t.RepliedAt.Value.ToString("yyyy-MM-ddTHH:mm:ssZ")
                        : null
                })
                .ToListAsync();

            return Ok(tickets);
        }

        // ── POST /api/admin/support/{id}/reply ────────────────────────────────
        [HttpPost("api/admin/support/{id:int}/reply")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ReplyToTicket(int id, [FromBody] AdminSupportReplyDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Reply))
                return BadRequest(new { message = "Reply is required." });

            var ticket = await _db.SupportTickets.FindAsync(id);
            if (ticket == null)
                return NotFound(new { message = $"Ticket {id} not found." });

            var now = DateTime.UtcNow;
            ticket.AdminReply = dto.Reply;
            ticket.RepliedAt = now;

            // Automatically move to InProgress if still Open
            if (ticket.Status == SupportStatus.Open)
                ticket.Status = SupportStatus.InProgress;

            // Create internal alert for the user
            _db.Alerts.Add(new Alert
            {
                UserId = ticket.UserId,
                Type = "AdminMessage",
                Title = "Admin Replied to Your Support Request",
                Message = "Admin replied to your support request.",
                IsRead = false,
                ScheduledAt = now,
                CreatedAt = now
            });

            await _db.SaveChangesAsync();

            return Ok(new { message = "Reply sent successfully." });
        }

        // ── PATCH /api/admin/support/{id}/status ──────────────────────────────
        [HttpPatch("api/admin/support/{id:int}/status")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateTicketStatus(int id, [FromBody] UpdateSupportStatusDto dto)
        {
            if (!Enum.TryParse<SupportStatus>(dto.Status, ignoreCase: true, out var newStatus))
                return BadRequest(new
                {
                    message = $"Invalid status '{dto.Status}'. Valid values: " +
                              string.Join(", ", Enum.GetNames<SupportStatus>())
                });

            var ticket = await _db.SupportTickets.FindAsync(id);
            if (ticket == null)
                return NotFound(new { message = $"Ticket {id} not found." });

            ticket.Status = newStatus;
            await _db.SaveChangesAsync();

            return Ok(new { message = $"Ticket {id} status updated to {newStatus}." });
        }

        private int GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)
                ?? throw new UnauthorizedAccessException("NameIdentifier claim missing.");
            return int.Parse(claim.Value);
        }
    }
}