namespace api_test.Models
{
    public class InitUserMedicationDto
    {
        public int? MedicationId { get; set; }
        public string? MedicationName { get; set; }
        public bool? IsCustomMedication { get; set; }
        public string? DosageForm { get; set; }
        public string? QuantityUnit { get; set; }
    }
}
