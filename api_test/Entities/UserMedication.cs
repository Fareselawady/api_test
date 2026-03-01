namespace api_test.Entities
{
    public class UserMedication
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public int MedId { get; set; }
        public Medication Medication { get; set; } = null!; // FK على MedId

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

        public bool NotificationActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<MedicationSchedule> MedicationSchedules { get; set; } = new List<MedicationSchedule>();
        public ICollection<Alert> Alerts { get; set; } = new List<Alert>();
    }
}
