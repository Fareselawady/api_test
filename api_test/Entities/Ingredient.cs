using System.ComponentModel.DataAnnotations.Schema;

namespace api_test.Entities
{
    public class Ingredient
    {
        public int Id { get; set; }

        [Column("Ingredient")]
        public string? IngredientName { get; set; }

        public ICollection<MedIngredientLink>? MedLinks { get; set; }
    }
}
