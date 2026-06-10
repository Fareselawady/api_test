namespace api_test.Entities
{
    public class Medication
    {
        public int ID { get; set; }
        public string? Trade_name { get; set; }
        public string? Description { get; set; }
        public string? Dosage_Form { get; set; }
        public string? image_url { get; set; }
        public int? DefaultAfterOpeningValue { get; set; }
        public string? DefaultAfterOpeningUnit { get; set; }
        public bool RequiresOpeningTracking { get; set; }
        public string? AfterOpeningNote { get; set; }

        public ICollection<MedIngredientLink>? Ingredients { get; set; }
        public ICollection<UserMedication>? UserMedications { get; set; } = new List<UserMedication>();

    }
}
