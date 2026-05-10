namespace api_test.Models
{
    public class MedicineScanResponseDto
    {
        public bool Success { get; set; }
        public string? MedicationName { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}