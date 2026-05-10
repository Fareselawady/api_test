namespace api_test.Entities
{
    public enum SupportCategory
    {
        BugReport,
        TechnicalIssue,
        PaymentProblem,
        PremiumSubscription,
        Suggestion,
        Other
    }

    public enum SupportStatus
    {
        Open,
        InProgress,
        Closed
    }

    public class SupportTicket
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public SupportCategory Category { get; set; }

        public string Message { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public SupportStatus Status { get; set; } = SupportStatus.Open;

        public string? AdminReply { get; set; }

        public DateTime? RepliedAt { get; set; }
    }
}