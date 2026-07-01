namespace api_test.Entities;

public sealed class ChatMessage
{
    public long Id { get; set; }
    public int ChatConversationId { get; set; }
    public ChatConversation ChatConversation { get; set; } = null!;
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? DetectedLanguage { get; set; }
    public string? Intent { get; set; }
    public string? MatchedDrugsJson { get; set; }
    public string? SafetyFlagsJson { get; set; }
}
