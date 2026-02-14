namespace api_test.Entities
{
    public class Medication
    {
        public int ID { get; set; }
        public string? Trade_name { get; set; }
        public string? Description { get; set; }
        public string? Dosage_Form { get; set; }
        public string? image_url { get; set; }

        public ICollection<MedIngredientLink>? Ingredients { get; set; }
    }
}
