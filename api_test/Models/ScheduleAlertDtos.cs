namespace api_test.Models
{
    // ── Schedule response DTO ─────────────────────────────────────────────────
    public class MedicationScheduleDto
    {
        public int Id { get; set; }
        public int UserMedId { get; set; }
        public string MedName { get; set; } = string.Empty;  // joined from Medication
        public string ScheduledAt { get; set; } = string.Empty;  // ISO-8601 string
        public string NotificationTime { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool ReminderSent { get; set; }
        public int SnoozeCount { get; set; }
    }

    // ── Alert response DTO ────────────────────────────────────────────────────
    public class AlertDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int? UserMedicationId { get; set; }
        public int? MedicationScheduleId { get; set; }
        public string? Type { get; set; }
        public string? Title { get; set; }
        public string? Message { get; set; }
        public bool IsRead { get; set; }
        public string CreatedAt { get; set; } = string.Empty;
    }

    // ── Mark-as-taken / update status request ─────────────────────────────────
    public class UpdateScheduleStatusDto
    {
        /// <summary>"Pending" | "Taken" | "Missed"</summary>
        public string Status { get; set; } = string.Empty;
    }
}