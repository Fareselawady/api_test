using api_test.Entities;

namespace api_test.Models
{
    public class CreateMedicationDto
    {
        public string TradeName { get; set; } = null!;
        public string? Description { get; set; }
        public string? DosageForm { get; set; }
        public string? ImageUrl { get; set; }
    }
}
