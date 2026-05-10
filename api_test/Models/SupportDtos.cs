namespace api_test.Models
{
    // ── POST /api/support ─────────────────────────────────────────────────────
    public class CreateSupportTicketDto
    {
        public string Category { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    // ── POST /api/admin/support/{id}/reply ───────────────────────────────────
    public class AdminSupportReplyDto
    {
        public string Reply { get; set; } = string.Empty;
    }

    // ── PATCH /api/admin/support/{id}/status ─────────────────────────────────
    public class UpdateSupportStatusDto
    {
        public string Status { get; set; } = string.Empty;
    }

    // ── Response DTOs ─────────────────────────────────────────────────────────
    public class SupportTicketDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string? UserName { get; set; }
        public string? UserEmail { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? AdminReply { get; set; }
        public string CreatedAt { get; set; } = string.Empty;
        public string? RepliedAt { get; set; }
    }
}