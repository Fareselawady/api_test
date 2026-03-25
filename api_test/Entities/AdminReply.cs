namespace api_test.Entities
{
    public class AdminReply
    {
        public int Id { get; set; }

        // ── FK — الرد على اليوزر مش على السيرفاي ─────────────────────────────
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        // ── DATA ──────────────────────────────────────────────────────────────
        public string Message { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
