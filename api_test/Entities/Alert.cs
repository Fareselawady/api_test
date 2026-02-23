namespace api_test.Entities
{
    public class Alert
    {
        public int Id { get; set; }

        // ================= USER =================
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        // ================= OPTIONAL LINKS =================
        public int? UserMedicationId { get; set; }
        public UserMedication? UserMedication { get; set; }

        public int? MedicationScheduleId { get; set; }
        public MedicationSchedule? MedicationSchedule { get; set; }

        // ================= ALERT DATA =================
        public string? Type { get; set; }
        public string? Title { get; set; }
        public string? Message { get; set; }

        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
