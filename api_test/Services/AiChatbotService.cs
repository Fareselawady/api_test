using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using api_test.Data;
using api_test.Entities;
using api_test.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace api_test.Services;

public sealed class AiChatbotService : IAiChatbotService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly AppDbContext _db;
    private readonly AiChatbotOptions _options;
    private readonly ILogger<AiChatbotService> _logger;

    public AiChatbotService(
        HttpClient httpClient,
        AppDbContext db,
        IOptions<AiChatbotOptions> options,
        ILogger<AiChatbotService> logger)
    {
        _httpClient = httpClient;
        _db = db;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ChatbotServiceResult<ChatbotMessageResponse>> SendMessageAsync(
        int userId,
        ChatbotMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        var aiRequest = await BuildAiRequestAsync(userId, request, cancellationToken);
        if (aiRequest == null)
        {
            return ChatbotServiceResult<ChatbotMessageResponse>.Fail(
                StatusCodes.Status401Unauthorized,
                "user_not_found",
                "The authenticated user could not be found.");
        }

        try
        {
            using var response = await _httpClient.PostAsJsonAsync("chat", aiRequest, JsonOptions, cancellationToken);
            var rawBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
            {
                var validationMessage = ParseValidationError(rawBody);
                _logger.LogWarning("AI chatbot rejected the generated request with status 422: {ValidationMessage}", validationMessage);
                return ChatbotServiceResult<ChatbotMessageResponse>.Fail(
                    StatusCodes.Status502BadGateway,
                    "ai_validation_error",
                    validationMessage);
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("AI chatbot returned HTTP {StatusCode}.", (int)response.StatusCode);
                return ChatbotServiceResult<ChatbotMessageResponse>.Fail(
                    StatusCodes.Status503ServiceUnavailable,
                    "ai_service_error",
                    "Mighty is temporarily unavailable. Please try again shortly.");
            }

            AiChatResponse? aiResponse;
            try
            {
                aiResponse = JsonSerializer.Deserialize<AiChatResponse>(rawBody, JsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "AI chatbot returned invalid JSON.");
                return InvalidResponse();
            }

            if (aiResponse == null || string.IsNullOrWhiteSpace(aiResponse.Answer))
                return InvalidResponse();

            return ChatbotServiceResult<ChatbotMessageResponse>.Ok(new ChatbotMessageResponse
            {
                Answer = aiResponse.Answer,
                ConversationId = string.IsNullOrWhiteSpace(aiResponse.ConversationId)
                    ? aiRequest.ConversationId
                    : aiResponse.ConversationId,
                DetectedLanguage = aiResponse.DetectedLanguage,
                Intent = aiResponse.Intent,
                MatchedDrugs = aiResponse.MatchedDrugs ?? [],
                SafetyFlags = aiResponse.SafetyFlags ?? []
            });
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "AI chatbot request timed out.");
            return ChatbotServiceResult<ChatbotMessageResponse>.Fail(
                StatusCodes.Status504GatewayTimeout,
                "ai_timeout",
                "Mighty took too long to respond. Please try again.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Could not reach the AI chatbot service.");
            return ChatbotServiceResult<ChatbotMessageResponse>.Fail(
                StatusCodes.Status503ServiceUnavailable,
                "ai_unavailable",
                "Mighty is temporarily unavailable. Please try again shortly.");
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Could not serialize the AI chatbot request.");
            return InvalidResponse();
        }
    }

    public async Task<ChatbotHealthResponse> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync("health", cancellationToken);
            return new ChatbotHealthResponse
            {
                Reachable = response.IsSuccessStatusCode,
                StatusCode = (int)response.StatusCode,
                Message = response.IsSuccessStatusCode
                    ? "AI chatbot service is reachable."
                    : "AI chatbot service returned an unhealthy status."
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ChatbotHealthResponse { Message = "AI chatbot health check timed out." };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "AI chatbot health check failed.");
            return new ChatbotHealthResponse { Message = "AI chatbot service is unreachable." };
        }
    }

    private async Task<AiChatRequest?> BuildAiRequestAsync(
        int userId,
        ChatbotMessageRequest request,
        CancellationToken cancellationToken)
    {
        var user = await _db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user == null)
            return null;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var medications = await _db.UserMedications
            .AsNoTracking()
            .Include(um => um.Medication)
            .Where(um => um.UserId == userId && (um.EndDate == null || um.EndDate >= today))
            .ToListAsync(cancellationToken);

        return new AiChatRequest
        {
            ConversationId = string.IsNullOrWhiteSpace(request.ConversationId)
                ? Guid.NewGuid().ToString("N")
                : request.ConversationId.Trim(),
            Message = request.Message.Trim(),
            Mode = "patient",
            PatientContext = new AiPatientContext
            {
                PatientId = user.Id.ToString(),
                Age = CalculateAge(user.BirthDate),
                Gender = NormalizeGender(user.Gender),
                WeightKg = 0,
                CurrentMedications = medications.Select(um => new AiCurrentMedication
                {
                    DrugName = ResolveMedicationName(um),
                    Dose = string.IsNullOrWhiteSpace(um.Dosage) ? "unknown" : um.Dosage.Trim(),
                    Frequency = BuildFrequency(um)
                }).ToList(),
                Allergies = [],
                Conditions = [],
                KidneyFunction = "normal",
                LiverFunction = "normal",
                IsPregnant = false,
                IsBreastfeeding = false
            },
            SessionMetadata = new AiSessionMetadata
            {
                LanguagePreference = "auto",
                AppVersion = string.IsNullOrWhiteSpace(_options.AppVersion) ? "1.0.0" : _options.AppVersion,
                Timestamp = DateTimeOffset.UtcNow.ToString("O")
            }
        };
    }

    private static int CalculateAge(DateTime? birthDate)
    {
        if (!birthDate.HasValue)
            return 0;

        var today = DateTime.UtcNow.Date;
        var date = birthDate.Value.Date;
        if (date > today)
            return 0;

        var age = today.Year - date.Year;
        if (date > today.AddYears(-age))
            age--;
        return Math.Max(age, 0);
    }

    private static string NormalizeGender(string? gender)
    {
        if (string.IsNullOrWhiteSpace(gender))
            return "male";

        return gender.Trim().ToLowerInvariant() switch
        {
            "m" or "male" or "man" or "ذكر" => "male",
            "f" or "female" or "woman" or "أنثى" => "female",
            _ => "male"
        };
    }

    private static string ResolveMedicationName(UserMedication medication)
    {
        if (!string.IsNullOrWhiteSpace(medication.MedicationName))
            return medication.MedicationName.Trim();
        if (!string.IsNullOrWhiteSpace(medication.Medication?.Trade_name))
            return medication.Medication.Trade_name.Trim();
        return "unknown";
    }

    private static string BuildFrequency(UserMedication medication)
    {
        if (medication.MedicationUseType.Equals("PRN", StringComparison.OrdinalIgnoreCase))
        {
            return medication.MaxDosesPerDay.HasValue
                ? $"As needed, up to {medication.MaxDosesPerDay.Value} times per day"
                : "As needed";
        }

        if (medication.IntervalHours.HasValue)
            return $"Every {medication.IntervalHours.Value} hours";

        if (medication.DosesPerPeriod.HasValue)
        {
            var periodValue = medication.PeriodValue.GetValueOrDefault(1);
            var periodUnit = string.IsNullOrWhiteSpace(medication.PeriodUnit) ? "day" : medication.PeriodUnit.Trim();
            return $"{medication.DosesPerPeriod.Value} times every {periodValue} {periodUnit}";
        }

        if (medication.FirstDoseTime.HasValue)
            return $"Daily at {medication.FirstDoseTime.Value:HH:mm}";

        return "unknown";
    }

    private static string ParseValidationError(string rawBody)
    {
        try
        {
            var error = JsonSerializer.Deserialize<AiValidationErrorResponse>(rawBody, JsonOptions);
            var details = error?.Detail
                .Select(item => item.Message?.Trim())
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .Distinct()
                .ToList();

            if (details is { Count: > 0 })
                return $"Mighty could not process the request: {string.Join("; ", details)}";
        }
        catch (JsonException)
        {
            // Fall through to the stable client-facing message below.
        }

        return "Mighty could not process the request. Please try again.";
    }

    private static ChatbotServiceResult<ChatbotMessageResponse> InvalidResponse() =>
        ChatbotServiceResult<ChatbotMessageResponse>.Fail(
            StatusCodes.Status502BadGateway,
            "ai_invalid_response",
            "Mighty returned an unexpected response. Please try again.");
}
