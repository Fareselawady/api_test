namespace api_test.Entities
{
    public class Drug
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public DateOnly ExpirationDate { get; set; }
        public DateOnly ProductDate { get; set; }

        public int UserId { get; set; }
        public User User { get; set; } = null!;
    }
}
