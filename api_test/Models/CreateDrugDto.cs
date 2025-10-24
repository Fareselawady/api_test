namespace api_test.Models
{
    public class CreateDrugDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public DateOnly ExpirationDate { get; set; }
        public DateOnly ProductDate { get; set; }
    }
}
