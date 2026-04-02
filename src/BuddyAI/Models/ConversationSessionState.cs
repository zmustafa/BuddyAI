namespace BuddyAI.Models;

public sealed class ConversationSessionState
{
    public string Title { get; set; } = string.Empty;
    public string Persona { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Temperature { get; set; } = "1";
    public string Provider { get; set; } = string.Empty;
    public string ProviderId { get; set; } = string.Empty;
    public string SystemPrompt { get; set; } = string.Empty;
    public string LastPrompt { get; set; } = string.Empty;
    public string RawResponseText { get; set; } = string.Empty;
    public string PreviousResponseId { get; set; } = string.Empty;
    public string? ImageMimeType { get; set; }
    public string? ImageName { get; set; }
    public string? ImageBase64 { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastUpdatedAt { get; set; }
    public double LatencySeconds { get; set; }
    public int EstimatedTokens { get; set; }
    public decimal EstimatedCostUsd { get; set; }
    public bool IsPinned { get; set; }
    public bool HasError { get; set; }
    public bool IsPending { get; set; }
    public List<string> PromptHistory { get; set; } = new();
}
