using api_test.Models;
using System.Text.Json;

namespace api_test.Services
{
    public class AIMedicineRecognitionService : IAIMedicineRecognitionService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AIMedicineRecognitionService> _logger;

        public AIMedicineRecognitionService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<AIMedicineRecognitionService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<MedicineScanResponseDto> ScanMedicineImageAsync(IFormFile file)
        {
            // ── 1. Validate file ──────────────────────────────────────────────
            if (file == null)
                return Fail("No file was provided.");

            if (file.Length == 0)
                return Fail("The uploaded file is empty.");

            if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                return Fail($"Invalid file type '{file.ContentType}'. Only image files are accepted.");

            // ── 2. Read configuration ─────────────────────────────────────────
            var webhookUrl = _configuration["AIService:WebhookUrl"];
            var fileFieldName = _configuration["AIService:FileFieldName"] ?? "file";

            if (string.IsNullOrWhiteSpace(webhookUrl))
            {
                _logger.LogError("AIService:WebhookUrl is not configured in appsettings.json.");
                return Fail("AI service is not configured. Please contact the administrator.");
            }

            // ── 3. Build multipart/form-data and POST ─────────────────────────
            try
            {
                using var content = new MultipartFormDataContent();
                using var fileStream = file.OpenReadStream();
                var streamContent = new StreamContent(fileStream);

                streamContent.Headers.ContentType =
                    new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);

                // Field name MUST be "file" — matches what the n8n webhook expects
                content.Add(streamContent, fileFieldName, file.FileName);

                var response = await _httpClient.PostAsync(webhookUrl, content);
                var rawBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("AI webhook returned {StatusCode}. Body: {Body}",
                        (int)response.StatusCode, rawBody);
                    return Fail($"AI service returned an error (HTTP {(int)response.StatusCode}). Please try again later.");
                }

                // ── 4. Parse JSON response ────────────────────────────────────
                if (string.IsNullOrWhiteSpace(rawBody))
                    return Fail("AI service returned an empty response.");

                AiMedicineResponseDto? aiResponse;
                try
                {
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    aiResponse = JsonSerializer.Deserialize<AiMedicineResponseDto>(rawBody, options);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "AI webhook returned invalid JSON: {Body}", rawBody);
                    return Fail("AI service returned an unexpected response format.");
                }

                if (aiResponse == null)
                    return Fail("AI service returned a null response.");

                // ── 5. Map medicine_name (AI field) → MedicationName (our field) ─
                // Treat null, empty, whitespace, or "unknown" all as detection failure
                var detectedName = aiResponse.Medicine_Name;

                if (string.IsNullOrWhiteSpace(detectedName) ||
                    detectedName.Equals("unknown", StringComparison.OrdinalIgnoreCase))
                {
                    return new MedicineScanResponseDto
                    {
                        Success = false,
                        MedicationName = null,
                        Message = "AI could not detect medicine name."
                    };
                }

                return new MedicineScanResponseDto
                {
                    Success = true,
                    MedicationName = detectedName.Trim(),
                    Message = "Medicine detected successfully."
                };
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error communicating with AI webhook.");
                return Fail("Could not reach the AI service. Please check your connection and try again.");
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "AI webhook request timed out.");
                return Fail("The AI service request timed out. Please try again.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in AIMedicineRecognitionService.");
                return Fail("An unexpected error occurred while scanning the image.");
            }
        }

        // ── Helper ────────────────────────────────────────────────────────────
        private static MedicineScanResponseDto Fail(string message) =>
            new MedicineScanResponseDto
            {
                Success = false,
                MedicationName = null,
                Message = message
            };
    }
}