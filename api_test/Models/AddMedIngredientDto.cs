namespace api_test.Models
{
    public class AddMedIngredientDto
    {
        public string MedicationName { get; set; } = null!;
        public List<IngredientItemDto> Ingredients { get; set; } = new();
    }
}
