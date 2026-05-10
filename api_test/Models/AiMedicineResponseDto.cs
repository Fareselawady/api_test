using System.Text.Json.Serialization;

namespace api_test.Models
{
    /// <summary>
    /// Maps the AI webhook response which returns { "medicine_name": "..." }.
    /// Internally mapped to MedicationName before returning to Flutter.
    /// </summary>
    public class AiMedicineResponseDto
    {
        [JsonPropertyName("medicine_name")]
        public string? Medicine_Name { get; set; }
    }
}