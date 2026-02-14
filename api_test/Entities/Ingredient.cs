namespace api_test.Entities
{
    public class Ingredient
    {
        public int Id { get; set; }
        public string? IngredientName { get; set; }

        public ICollection<MedIngredientLink>? MedLinks { get; set; }
    }
}
