namespace BuddyAI.Models;

public sealed class AppSessionState
{
    public string ActiveProfile { get; set; } = string.Empty;
    public string ActivePersona { get; set; } = string.Empty;
    public string ActiveProvider { get; set; } = string.Empty;
    public string ActiveProviderId { get; set; } = string.Empty;
    public string ActiveModel { get; set; } = string.Empty;
    public string ActiveTemperature { get; set; } = "1";
    public string CurrentSystemPrompt { get; set; } = string.Empty;
    public string CurrentPrompt { get; set; } = string.Empty;
    public string? CurrentImageMimeType { get; set; }
    public string? CurrentImagePath { get; set; }
    public string? CurrentImageBase64 { get; set; }
    public int SelectedConversationIndex { get; set; }
    public bool IsWindowMaximized { get; set; }
    public List<string> PromptHistory { get; set; } = new();
    public List<ConversationSessionState> Conversations { get; set; } = new();
}
