namespace api_test.Entities
{
    public class MedicationSchedule
    {
        public int Id { get; set; }

        // ================= FK =================
        public int UserMedicationId { get; set; }
        public UserMedication UserMedication { get; set; } = null!;

        // ================= SCHEDULE =================
        public DateTime ScheduledAt { get; set; }
        public DateTime? NotificationTime { get; set; }

        public MedicationStatus Status { get; set; } = MedicationStatus.Pending;

        public DateTime? TakenAt { get; set; }
        public DateTime? SkippedAt { get; set; }
        public DateTime? MissedAt { get; set; }
        public string? MissedReason { get; set; }
        public string? ActionNote { get; set; }

        public bool ReminderSent { get; set; } = false;
        public bool AdvanceReminderSent { get; set; } = false;
        public bool DueReminderSent { get; set; } = false;

        public DateTime? SnoozedUntil { get; set; }
        public int SnoozeCount { get; set; } = 0;

        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // ================= NAV =================
        public ICollection<Alert>? Alerts { get; set; }
    }
}
