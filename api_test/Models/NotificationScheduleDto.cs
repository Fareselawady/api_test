namespace api_test.Models
{
    /// <summary>
    /// DTO returned by GET /api/users/me/notification-schedules.
    /// Designed for Flutter local notifications, Firebase push, and wearable reminders.
    /// </summary>
    public class NotificationScheduleDto
    {
        public int ScheduleId { get; set; }
        public int UserMedId { get; set; }
        public int MedId { get; set; }
        public string MedName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;

        /// <summary>UTC time when the dose should be taken.</summary>
        public DateTime ScheduledAt { get; set; }

        /// <summary>UTC time to fire the local notification (ScheduledAt - 15 min).</summary>
        public DateTime NotificationTime { get; set; }

        public string Status { get; set; } = "Pending";

        public int? PillsPerDose { get; set; }
        public int? CurrentPillCount { get; set; }
        public int? LowStockThreshold { get; set; }
        public decimal? DoseQuantity { get; set; }
        public decimal? CurrentQuantity { get; set; }
        public string? QuantityUnit { get; set; }
        public string? DosageForm { get; set; }

        /// <summary>True if this medication has at least one known drug interaction.</summary>
        public bool HasInteractions { get; set; }
    }
}
