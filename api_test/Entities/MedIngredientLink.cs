namespace api_test.Entities
{
    public class MedIngredientLink
    {
        public int ID { get; set; }

        public int Med_id { get; set; }
        public Medication Medication { get; set; }

        public int Ingredient_id { get; set; }
        public Ingredient Ingredient { get; set; }

        public int? Strength_value { get; set; }
        public string? Strength_unit { get; set; }
    }
}
