using api_test.Models;

namespace api_test.Entities
{
    public class User
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
        public DateTime? BirthDate { get; set; }
        public string Gender { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;

        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;

        // ================= EMAIL VERIFICATION =================
        public bool IsEmailVerified { get; set; } = false;
        public string? EmailOtp { get; set; }
        public DateTime? OtpExpiry { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int RoleId { get; set; }
        public virtual Role Role { get; set; } = null!;
        public ICollection<UserMedication> UserMedications { get; set; } = new List<UserMedication>();
        public ICollection<Alert> Alerts { get; set; } = new List<Alert>();
        public ICollection<ChatConversation> ChatConversations { get; set; } = new List<ChatConversation>();

        // ================= PREMIUM =================
        public bool IsPremium { get; set; } = false;
        public DateTime? PremiumStartDate { get; set; }
        public DateTime? PremiumEndDate { get; set; }
    }
}
