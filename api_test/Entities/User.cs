using api_test.Models;

namespace api_test.Entities
{
    public class User
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
        public DateTime? BirthDate { get; set; }  // ممكن يكون null لو مش مطلوب
        public string Gender { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;

        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;


        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int RoleId { get; set; } // لازم يكون موجود عشان الـ ForeignKey
        public virtual Role Role { get; set; }
        public ICollection<UserMedication> UserMedications { get; set; } = new List<UserMedication>();
        public ICollection<Alert> Alerts { get; set; } = new List<Alert>();

    }
}
