namespace api_test.Entities
{
    public class UserMedication
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public int? MedicationId { get; set; }
        public Medication? Medication { get; set; }
        public string MedicationName { get; set; } = string.Empty;
        public bool IsCustomMedication { get; set; } = false;

        public string? Dosage { get; set; }
        public string? Notes { get; set; }

        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public DateOnly? ExpiryDate { get; set; }
        public bool IsOpened { get; set; } = false;
        public DateTime? OpenedDate { get; set; }
        public int? AfterOpeningDurationValue { get; set; }
        public string? AfterOpeningDurationUnit { get; set; } = "days";
        public DateTime? AfterOpeningExpiryDate { get; set; }
        public DateTime? EffectiveExpiryDate { get; set; }
        public string? ExpiryReason { get; set; }
        public string? AfterOpeningSource { get; set; }

        public int? CurrentPillCount { get; set; }
        public int? InitialPillCount { get; set; }
        public int? LowStockThreshold { get; set; }

        /// <summary>
        /// Number of pills/tablets taken per scheduled dose.
        /// If null, TakeDose defaults to deducting 1 pill.
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

        public TimeOnly? FirstDoseTime { get; set; }
        public int? IntervalHours { get; set; }

        public string MedicationUseType { get; set; } = "Scheduled";
        public int? MaxDosesPerDay { get; set; }
        public decimal? MinimumHoursBetweenDoses { get; set; }
        public int? RefillReminderDaysBefore { get; set; }
        public DateTime? LastRefillDate { get; set; }
        public decimal? LastRefillQuantity { get; set; }

        public bool NotificationActive { get; set; } = true;
        public int? AdvanceReminderMinutes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<MedicationSchedule> MedicationSchedules { get; set; } = new List<MedicationSchedule>();
        public ICollection<MedicationIntakeLog> IntakeLogs { get; set; } = new List<MedicationIntakeLog>();
        public ICollection<Alert> Alerts { get; set; } = new List<Alert>();
    }
}
