using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace api_test.Models;

public sealed class ChatbotMessageRequest
{
    [Required]
    [StringLength(4000, MinimumLength = 1)]
    public string Message { get; set; } = string.Empty;

    [StringLength(200)]
    public string? ConversationId { get; set; }
}

public sealed class ChatbotMessageResponse
{
    public string Answer { get; set; } = string.Empty;

    [JsonPropertyName("conversation_id")]
    public string ConversationId { get; set; } = string.Empty;

    [JsonPropertyName("detected_language")]
    public string DetectedLanguage { get; set; } = string.Empty;

    public string Intent { get; set; } = string.Empty;

    [JsonPropertyName("matched_drugs")]
    public List<string> MatchedDrugs { get; set; } = [];

    [JsonPropertyName("safety_flags")]
    public List<string> SafetyFlags { get; set; } = [];
}

public sealed class AiChatRequest
{
    [JsonPropertyName("conversation_id")]
    public string ConversationId { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "patient";

    [JsonPropertyName("patient_context")]
    public AiPatientContext PatientContext { get; set; } = new();

    [JsonPropertyName("session_metadata")]
    public AiSessionMetadata SessionMetadata { get; set; } = new();
}

public sealed class AiPatientContext
{
    [JsonPropertyName("patient_id")]
    public string PatientId { get; set; } = "unknown";

    [JsonPropertyName("age")]
    public int Age { get; set; }

    [JsonPropertyName("gender")]
    public string Gender { get; set; } = "male";

    [JsonPropertyName("weight_kg")]
    public decimal WeightKg { get; set; }

    [JsonPropertyName("current_medications")]
    public List<AiCurrentMedication> CurrentMedications { get; set; } = [];

    [JsonPropertyName("allergies")]
    public List<AiAllergy> Allergies { get; set; } = [];

    [JsonPropertyName("conditions")]
    public List<string> Conditions { get; set; } = [];

    [JsonPropertyName("kidney_function")]
    public string KidneyFunction { get; set; } = "normal";

    [JsonPropertyName("liver_function")]
    public string LiverFunction { get; set; } = "normal";

    [JsonPropertyName("is_pregnant")]
    public bool IsPregnant { get; set; }

    [JsonPropertyName("is_breastfeeding")]
    public bool IsBreastfeeding { get; set; }
}

public sealed class AiCurrentMedication
{
    [JsonPropertyName("drug_name")]
    public string DrugName { get; set; } = string.Empty;

    [JsonPropertyName("dose")]
    public string Dose { get; set; } = string.Empty;

    [JsonPropertyName("frequency")]
    public string Frequency { get; set; } = string.Empty;
}

public sealed class AiAllergy
{
    [JsonPropertyName("allergen")]
    public string Allergen { get; set; } = string.Empty;

    [JsonPropertyName("reaction")]
    public string Reaction { get; set; } = string.Empty;
}

public sealed class AiSessionMetadata
{
    [JsonPropertyName("language_preference")]
    public string LanguagePreference { get; set; } = "auto";

    [JsonPropertyName("app_version")]
    public string AppVersion { get; set; } = "1.0.0";

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = string.Empty;
}

public sealed class AiChatResponse
{
    [JsonPropertyName("answer")]
    public string Answer { get; set; } = string.Empty;

    [JsonPropertyName("conversation_id")]
    public string ConversationId { get; set; } = string.Empty;

    [JsonPropertyName("detected_language")]
    public string DetectedLanguage { get; set; } = string.Empty;

    [JsonPropertyName("intent")]
    public string Intent { get; set; } = string.Empty;

    [JsonPropertyName("matched_drugs")]
    public List<string>? MatchedDrugs { get; set; }

    [JsonPropertyName("safety_flags")]
    public List<string>? SafetyFlags { get; set; }
}

public sealed class AiValidationErrorResponse
{
    [JsonPropertyName("detail")]
    public List<AiValidationErrorDetail> Detail { get; set; } = [];
}

public sealed class AiValidationErrorDetail
{
    [JsonPropertyName("loc")]
    public List<JsonElement> Location { get; set; } = [];

    [JsonPropertyName("msg")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("input")]
    public JsonElement? Input { get; set; }

    [JsonPropertyName("ctx")]
    public JsonElement? Context { get; set; }
}

public sealed class ChatbotHealthResponse
{
    public bool Reachable { get; set; }
    public int? StatusCode { get; set; }
    public string Message { get; set; } = string.Empty;
}
