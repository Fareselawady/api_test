using System.ComponentModel.DataAnnotations;

namespace api_test.Models
{
    public class Student
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; }

        [Required, EmailAddress]
        public string Email { get; set; }

        public int Age { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
