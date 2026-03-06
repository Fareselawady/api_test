namespace api_test.Models
{
    public class AddMedIngredientDto
    {
        public string MedicationName { get; set; } = null!;
        public string IngredientName { get; set; } = null!;
        public int? StrengthValue { get; set; }
        public string? StrengthUnit { get; set; }
    }
}
