namespace api_test.Models
{
    public class IngredientItemDto
    {
        public string IngredientName { get; set; } = null!;
        public int? StrengthValue { get; set; }
        public string? StrengthUnit { get; set; }
    }
}
