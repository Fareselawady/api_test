namespace api_test.Entities
{
    public class MedicineScanHistory
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public string? FileName { get; set; }
        public string? ContentType { get; set; }
        public long FileSize { get; set; }

        public bool Success { get; set; }
        public string? MedicationName { get; set; }
        public string Message { get; set; } = string.Empty;
        public int HttpStatusCode { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
