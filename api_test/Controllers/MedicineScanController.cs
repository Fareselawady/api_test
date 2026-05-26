using api_test.Data;
using api_test.Entities;
using api_test.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace api_test.Controllers
{
    [Route("api/medicine-scan")]
    [ApiController]
    [Authorize]
    public class MedicineScanController : ControllerBase
    {
        private readonly IAIMedicineRecognitionService _aiService;
        private readonly IConfiguration _configuration;
        private readonly AppDbContext _db;

        public MedicineScanController(
            IAIMedicineRecognitionService aiService,
            IConfiguration configuration,
            AppDbContext db)
        {
            _aiService = aiService;
            _configuration = configuration;
            _db = db;
        }

        // ── POST /api/medicine-scan/image ────────────────────────────────────
        [HttpPost("image")]
        public async Task<IActionResult> ScanMedicineImage([FromForm] IFormFile? file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new
                {
                    success = false,
                    medicationName = (string?)null,
                    message = "No image file was provided. Send multipart/form-data with field name 'file'."
                });

            if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new
                {
                    success = false,
                    medicationName = (string?)null,
                    message = $"Invalid file type '{file.ContentType}'. Only image files are accepted."
                });

            var result = await _aiService.ScanMedicineImageAsync(file);

            if (result.Success)
            {
                await SaveScanHistory(file, result, StatusCodes.Status200OK);
                return Ok(result);
            }

            bool isGatewayError =
                result.Message.Contains("AI service returned an error", StringComparison.OrdinalIgnoreCase) ||
                result.Message.Contains("Could not reach", StringComparison.OrdinalIgnoreCase) ||
                result.Message.Contains("timed out", StringComparison.OrdinalIgnoreCase) ||
                result.Message.Contains("not configured", StringComparison.OrdinalIgnoreCase);

            if (isGatewayError)
            {
                await SaveScanHistory(file, result, StatusCodes.Status502BadGateway);
                return StatusCode(502, result);
            }

            await SaveScanHistory(file, result, StatusCodes.Status400BadRequest);
            return BadRequest(result);
        }

        // ── GET /api/medicine-scan/test-webhook ──────────────────────────────
        /// <summary>
        /// Development only. Call this to verify the AI webhook is reachable.
        /// Example: GET /api/medicine-scan/test-webhook?value=hello
        /// Delete before production.
        /// </summary>
        [HttpGet("test-webhook")]
        [AllowAnonymous]
        public async Task<IActionResult> TestWebhook([FromQuery] string value = "test")
        {
            var webhookUrl = _configuration["AIService:WebhookUrl"];
            if (string.IsNullOrWhiteSpace(webhookUrl))
                return StatusCode(500, new { message = "AIService:WebhookUrl is not configured." });

            try
            {
                using var httpClient = new HttpClient();
                using var content = new MultipartFormDataContent();

                var textBytes = System.Text.Encoding.UTF8.GetBytes(value);
                var byteContent = new ByteArrayContent(textBytes);
                byteContent.Headers.ContentType =
                    new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");

                content.Add(byteContent, "file", "test.txt");

                var response = await httpClient.PostAsync(webhookUrl, content);
                var rawBody = await response.Content.ReadAsStringAsync();

                return Ok(new
                {
                    webhookStatusCode = (int)response.StatusCode,
                    webhookReached = response.IsSuccessStatusCode,
                    rawAiResponse = rawBody
                });
            }
            catch (Exception ex)
            {
                return StatusCode(502, new
                {
                    webhookReached = false,
                    error = ex.Message
                });
            }
        }

        private async Task SaveScanHistory(IFormFile file, Models.MedicineScanResponseDto result, int statusCode)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            _db.MedicineScanHistories.Add(new MedicineScanHistory
            {
                UserId = userId,
                FileName = file.FileName,
                ContentType = file.ContentType,
                FileSize = file.Length,
                Success = result.Success,
                MedicationName = result.MedicationName,
                Message = result.Message,
                HttpStatusCode = statusCode,
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
        }
    }
}
