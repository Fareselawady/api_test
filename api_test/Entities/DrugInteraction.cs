namespace api_test.Entities
{
    public class DrugInteraction
    {
        public int ID { get; set; }

        public int? Ingredient_1_id { get; set; }
        public int? Ingredient_2_id { get; set; }

        public string? Interaction_type { get; set; }
        public string? Drug_1_name { get; set; }
        public string? Drug_2_name { get; set; }

        public Ingredient? Ingredient1 { get; set; }
        public Ingredient? Ingredient2 { get; set; }
    }
}
