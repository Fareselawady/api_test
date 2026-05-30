namespace api_test.Models
{
    public class UserMedicationDetailsDto
    {
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
        /// Must be greater than 0 if provided.
        /// </summary>
        public int? PillsPerDose { get; set; }

        public decimal? InitialQuantity { get; set; }
        public decimal? CurrentQuantity { get; set; }
        public decimal? DoseQuantity { get; set; }
        public string? QuantityUnit { get; set; }
        public string? DosageForm { get; set; }

        public int? DosesPerPeriod { get; set; }
        public string? PeriodUnit { get; set; }
        public int? PeriodValue { get; set; }
        public int? IntervalHours { get; set; }
        public bool NotificationActive { get; set; } = true;

        // ── Schedule ───────────────────────────────────────────────────────────
        /// <summary>
        /// Optional schedule type hint. Accepted values: "CustomTimes", "Interval".
        /// If omitted the backend infers the type from the other fields (backward compatible).
        /// </summary>
        public string? ScheduleType { get; set; }

        /// <summary>
        /// Exact daily dose times, e.g. ["08:00:00", "14:00:00", "22:00:00"].
        /// When provided (and non-empty after cleaning) these define a CustomTimes schedule.
        /// Send an empty array [] for Interval schedules.
        /// </summary>
        public List<string>? DoseTimes { get; set; }
    }
}
