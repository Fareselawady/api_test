namespace api_test.Models
{
    // ── POST /api/premium/activate ────────────────────────────────────────────
    public class ActivatePremiumDto
    {
        /// <summary>Month | ThreeMonths | Year</summary>
        public string Plan { get; set; } = string.Empty;
    }

    // ── GET /api/premium/me ───────────────────────────────────────────────────
    public class PremiumStatusDto
    {
        public bool IsPremium { get; set; }
        public DateTime? PremiumStartDate { get; set; }
        public DateTime? PremiumEndDate { get; set; }
        public int? RemainingDays { get; set; }
    }
}