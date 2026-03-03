namespace api_test.Models
{
    public class CreateUserMedicationDto
    {
        public int MedId { get; set; }
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
    }
}
