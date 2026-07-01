namespace api_test.Services;

public sealed class AiChatbotOptions
{
    public const string SectionName = "AiChatbot";

    public string BaseUrl { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;
    public string AppVersion { get; set; } = "1.0.0";
}
