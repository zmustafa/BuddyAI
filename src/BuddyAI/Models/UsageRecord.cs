namespace BuddyAI.Models;

public sealed class UsageRecord
{
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string Model { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public int PromptCharacters { get; set; }
    public int ResponseCharacters { get; set; }
    public int EstimatedPromptTokens { get; set; }
    public int EstimatedResponseTokens { get; set; }
    public decimal EstimatedCostUsd { get; set; }
    public double LatencyMs { get; set; }
}
