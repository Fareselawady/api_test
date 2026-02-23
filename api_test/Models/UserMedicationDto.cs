namespace api_test.Models
{
    public class UserMedicationDto
    {
        public int Id { get; set; }
        public int MedId { get; set; }
        public string? MedName { get; set; }
        public string? Dosage { get; set; }
        public string? Notes { get; set; }

        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public DateOnly? ExpiryDate { get; set; }

        public int? CurrentPillCount { get; set; }
        public int? InitialPillCount { get; set; }
        public int? LowStockThreshold { get; set; }

        public int? DosesPerPeriod { get; set; }
        public string? PeriodUnit { get; set; }
        public int? PeriodValue { get; set; }

        public TimeOnly? FirstDoseTime { get; set; }
        public int? IntervalHours { get; set; }

        public bool NotificationActive { get; set; }
    }
}
