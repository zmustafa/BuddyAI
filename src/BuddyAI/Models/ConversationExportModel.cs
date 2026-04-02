namespace BuddyAI.Models;

public sealed class ConversationExportModel
{
    public string Title { get; set; } = string.Empty;
    public string Persona { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string SystemPrompt { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public string PreviousResponseId { get; set; } = string.Empty;
    public string ImageName { get; set; } = string.Empty;
    public string ImageMimeType { get; set; } = string.Empty;
    public int EstimatedTokens { get; set; }
    public decimal EstimatedCostUsd { get; set; }
    public double LatencySeconds { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime LastUpdatedAt { get; set; } = DateTime.Now;
    public List<string> PromptHistory { get; set; } = new();
}
