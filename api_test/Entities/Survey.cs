namespace api_test.Entities
{
    public class Survey
    {
        public int Id { get; set; }

        // ── FK ────────────────────────────────────────────────────────────────
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        // ── DATA ──────────────────────────────────────────────────────────────
        /// <summary>GeneralFeedback | Complaint | MedicationRequest</summary>
        public string Type { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
