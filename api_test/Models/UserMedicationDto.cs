namespace api_test.Models
{
    public class UserMedicationDto
    {
        public int Id { get; set; }
        public int MedId { get; set; }
        public string? MedicationName { get; set; }
        public string? Dosage { get; set; }
        public string? Notes { get; set; }

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public TimeSpan? FirstDoseTime { get; set; }

        public int? CurrentPillCount { get; set; }
        public int? InitialPillCount { get; set; }
        public int? LowStockThreshold { get; set; }

        /// <summary>
        /// Number of pills/tablets taken per scheduled dose.
        /// </summary>
        public int? PillsPerDose { get; set; }

        public int? DosesPerPeriod { get; set; }
        public string? PeriodUnit { get; set; }
        public int? PeriodValue { get; set; }

        public int? IntervalHours { get; set; }

        public bool NotificationActive { get; set; }

        // ── Schedule ───────────────────────────────────────────────────────────
        /// <summary>
        /// Inferred or stored schedule type: "CustomTimes", "Interval", or null.
        /// </summary>
        public string? ScheduleType { get; set; }

        /// <summary>
        /// The exact daily dose times for CustomTimes schedules (e.g. ["08:00:00","14:00:00"]).
        /// Empty list for Interval schedules.
        /// </summary>
        public List<string> DoseTimes { get; set; } = new();

        public List<MedicationInteractionDto> Interactions { get; set; } = new();

        public bool HasInteractions => Interactions != null && Interactions.Any();
    }
}