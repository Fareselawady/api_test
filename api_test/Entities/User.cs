using api_test.Models;

namespace api_test.Entities
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;

        public int RoleId { get; set; } // لازم يكون موجود عشان الـ ForeignKey
        public virtual Role Role { get; set; }
        public ICollection<Drug> Drugs { get; set; } = new List<Drug>();
    }
}
