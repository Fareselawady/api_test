namespace api_test.Models
{
    // ── Schedule response DTO ─────────────────────────────────────────────────
    public class MedicationScheduleDto
    {
        public int Id { get; set; }
        public int UserMedId { get; set; }
        public int MedId { get; set; }                        // ← مضاف عشان نعمل interaction check
        public string MedName { get; set; } = string.Empty;
        public string ScheduledAt { get; set; } = string.Empty;
        public string NotificationTime { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool ReminderSent { get; set; }
        public int SnoozeCount { get; set; }

        // ── Interactions ──────────────────────────────────────────────────────
        /// <summary>
        /// true if this medication has at least one interaction with any
        /// other medication the user currently has (not just today's doses).
        /// </summary>
        public bool HasInteractions { get; set; } = false;

        /// <summary>
        /// List of interactions — only populated for today-schedules endpoint.
        /// Each entry contains the conflicting med name and the interaction type.
        /// </summary>
        public List<MedicationInteractionDto> Interactions { get; set; } = new();
    }

    // ── Alert response DTO ────────────────────────────────────────────────────
    public class AlertDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int? UserMedicationId { get; set; }
        public int? MedicationScheduleId { get; set; }
        public string? Type { get; set; }       // LowStock | ExpiryWarning | DoseReminder
        public string? Title { get; set; }
        public string? Message { get; set; }
        public bool IsRead { get; set; }
        public string CreatedAt { get; set; } = string.Empty;   // ISO 8601
        public string ScheduledAt { get; set; } = string.Empty; // ISO 8601
    }

    // ── Mark-as-taken / update status request ─────────────────────────────────
    public class UpdateScheduleStatusDto
    {
        /// <summary>"Pending" | "Taken" | "Missed"</summary>
        public string Status { get; set; } = string.Empty;
    }
}