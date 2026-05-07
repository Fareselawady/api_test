namespace api_test.Models
{
    public class CreateUserMedicationDto
    {
        public string MedicationName { get; set; } = null!;
        public string? Dosage { get; set; }
        public string? Notes { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public TimeSpan? FirstDoseTime { get; set; }
        public int? CurrentPillCount { get; set; }
        public int? InitialPillCount { get; set; }
        public int? LowStockThreshold { get; set; }
        public int? DosesPerPeriod { get; set; }
        public string? PeriodUnit { get; set; }
        public int? PeriodValue { get; set; }
        public int? IntervalHours { get; set; }
        public bool NotificationActive { get; set; } = true;

        // ── NEW ────────────────────────────────────────────────────────────────
        /// <summary>
        /// Optional schedule type hint. Accepted values: "CustomTimes", "Interval", "Period".
        /// If omitted the backend infers the type from the other fields (backward compatible).
        /// </summary>
        public string? ScheduleType { get; set; }

        /// <summary>
        /// Exact daily dose times, e.g. ["08:00:00", "14:00:00", "22:00:00"].
        /// When provided (and non-empty) these take priority over IntervalHours
        /// and DosesPerPeriod-based scheduling.
        /// </summary>
        public List<string>? DoseTimes { get; set; }
    }
}