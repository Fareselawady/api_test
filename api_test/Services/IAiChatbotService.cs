using api_test.Models;

namespace api_test.Services;

public interface IAiChatbotService
{
    Task<ChatbotServiceResult<ChatbotMessageResponse>> SendMessageAsync(
        int userId,
        ChatbotMessageRequest request,
        CancellationToken cancellationToken = default);

    Task<ChatbotHealthResponse> CheckHealthAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChatConversationSummaryDto>> GetConversationsAsync(
        int userId,
        CancellationToken cancellationToken = default);

    Task<ChatbotServiceResult<IReadOnlyList<ChatHistoryMessageDto>>> GetMessagesAsync(
        int userId,
        string conversationId,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteConversationAsync(
        int userId,
        string conversationId,
        CancellationToken cancellationToken = default);
}

public sealed class ChatbotServiceResult<T>
{
    public bool Success { get; private init; }
    public T? Value { get; private init; }
    public int StatusCode { get; private init; }
    public string ErrorCode { get; private init; } = string.Empty;
    public string Message { get; private init; } = string.Empty;

    public static ChatbotServiceResult<T> Ok(T value) => new()
    {
        Success = true,
        Value = value,
        StatusCode = StatusCodes.Status200OK
    };

    public static ChatbotServiceResult<T> Fail(int statusCode, string errorCode, string message) => new()
    {
        StatusCode = statusCode,
        ErrorCode = errorCode,
        Message = message
    };
}
