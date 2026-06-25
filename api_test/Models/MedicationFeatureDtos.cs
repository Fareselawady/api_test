namespace api_test.Models
{
    public class SkipDoseRequestDto
    {
        public string? Reason { get; set; }
        public string? Note { get; set; }
    }

    public class RefillMedicationDto
    {
        public decimal Quantity { get; set; }
        public DateTime? RefillDate { get; set; }
        public int? RefillReminderDaysBefore { get; set; }
    }

    public class TakeNowDto
    {
        public decimal? QuantityTaken { get; set; }
        public string? Reason { get; set; }
        public string? Notes { get; set; }
        public DateTime? TakenAt { get; set; }
    }

    public class MedicationIntakeLogDto
    {
        public int Id { get; set; }
        public int UserMedicationId { get; set; }
        public string MedicationName { get; set; } = string.Empty;
        public DateTime TakenAt { get; set; }
        public decimal QuantityTaken { get; set; }
        public string? QuantityUnit { get; set; }
        public string? Reason { get; set; }
        public string? Notes { get; set; }
    }

    public class RefillForecastDto
    {
        public decimal? DosesRemaining { get; set; }
        public DateTime? EstimatedRunOutDate { get; set; }
        public int? DaysUntilEmpty { get; set; }
        public bool RefillWarning { get; set; }
        public int? RefillReminderDaysBefore { get; set; }
    }

    public class AdherenceSummaryDto
    {
        public int? UserMedicationId { get; set; }
        public string? MedicationName { get; set; }
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public int TotalDoses { get; set; }
        public int TakenDoses { get; set; }
        public int MissedDoses { get; set; }
        public int SkippedDoses { get; set; }
        public int LateDoses { get; set; }
        public decimal AdherenceRate { get; set; }
        public int CurrentStreak { get; set; }
        public string? BestDay { get; set; }
        public string? WorstDay { get; set; }
        public List<MedicationAdherenceSummaryDto> Medications { get; set; } = new();
    }

    public class MedicationAdherenceSummaryDto
    {
        public int UserMedicationId { get; set; }
        public string MedicationName { get; set; } = string.Empty;
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public int TotalDoses { get; set; }
        public int TakenDoses { get; set; }
        public int MissedDoses { get; set; }
        public int SkippedDoses { get; set; }
        public int LateDoses { get; set; }
        public decimal AdherenceRate { get; set; }
        public int CurrentStreak { get; set; }
        public string? BestDay { get; set; }
        public string? WorstDay { get; set; }
    }

    public class DoseHistoryDto
    {
        public int ScheduleId { get; set; }
        public int UserMedicationId { get; set; }
        public string MedicationName { get; set; } = string.Empty;
        public string ScheduledAt { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? TakenAt { get; set; }
        public string? SkippedAt { get; set; }
        public string? MissedAt { get; set; }
        public string? ActionAt { get; set; }
        public bool IsAsNeeded { get; set; }
        public bool IsLate { get; set; }
        public string? MissedReason { get; set; }
        public string? ActionNote { get; set; }
        public string? Notes { get; set; }
    }

    public class CabinetHealthDto
    {
        public List<CabinetMedicationDto> Expired { get; set; } = new();
        public List<CabinetMedicationDto> ExpiringSoon { get; set; } = new();
        public List<CabinetMedicationDto> AfterOpeningExpiringSoon { get; set; } = new();
        public List<CabinetMedicationDto> LowStock { get; set; } = new();
        public List<CabinetMedicationDto> OutOfStock { get; set; } = new();
        public List<CabinetMedicationDto> Healthy { get; set; } = new();
    }

    public class CabinetMedicationDto
    {
        public int UserMedicationId { get; set; }
        public string MedicationName { get; set; } = string.Empty;
        public string Severity { get; set; } = "Info";
        public DateTime? EffectiveExpiryDate { get; set; }
        public DateTime? AfterOpeningExpiryDate { get; set; }
        public decimal? CurrentQuantity { get; set; }
        public decimal? DoseQuantity { get; set; }
        public string? QuantityUnit { get; set; }
        public int? DaysUntilEmpty { get; set; }
        public DateTime? EstimatedRunOutDate { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}
