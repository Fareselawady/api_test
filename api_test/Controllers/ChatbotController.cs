using System.Security.Claims;
using api_test.Models;
using api_test.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace api_test.Controllers;

[ApiController]
[Authorize]
[Route("api/chatbot")]
public sealed class ChatbotController : ControllerBase
{
    private readonly IAiChatbotService _chatbotService;

    public ChatbotController(IAiChatbotService chatbotService)
    {
        _chatbotService = chatbotService;
    }

    [HttpPost("message")]
    [ProducesResponseType<ChatbotMessageResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SendMessage(
        [FromBody] ChatbotMessageRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { code = "invalid_message", message = "Message cannot be empty." });

        if (!TryGetUserId(out var userId))
            return Unauthorized(new { code = "invalid_token", message = "The access token does not contain a valid user ID." });

        var result = await _chatbotService.SendMessageAsync(userId, request, cancellationToken);
        if (result.Success)
            return Ok(result.Value);

        return StatusCode(result.StatusCode, new { code = result.ErrorCode, message = result.Message });
    }

    [HttpGet("health")]
    [ProducesResponseType<ChatbotHealthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ChatbotHealthResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Health(CancellationToken cancellationToken)
    {
        var result = await _chatbotService.CheckHealthAsync(cancellationToken);
        return result.Reachable
            ? Ok(result)
            : StatusCode(StatusCodes.Status503ServiceUnavailable, result);
    }

    private bool TryGetUserId(out int userId)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out userId);
    }
}
