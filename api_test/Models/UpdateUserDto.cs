namespace api_test.Models
{
    public class UpdateUserDto
    {
        public string? Name { get; set; }
        public string? Username { get; set; }
        public string? Phone { get; set; }
        public DateTime? BirthDate { get; set; }
        public string? Gender { get; set; }
        public string? Password { get; set; }

        // فقط للادمن
        public int? RoleId { get; set; }
    }
}
