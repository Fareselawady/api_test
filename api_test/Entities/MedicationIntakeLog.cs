namespace api_test.Entities
{
    public class MedicationIntakeLog
    {
        public int Id { get; set; }
        public int UserMedicationId { get; set; }
        public UserMedication UserMedication { get; set; } = null!;
        public DateTime TakenAt { get; set; } = DateTime.UtcNow;
        public decimal QuantityTaken { get; set; } = 1;
        public string? Reason { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
