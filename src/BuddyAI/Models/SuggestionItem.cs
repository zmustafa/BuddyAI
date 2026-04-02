namespace BuddyAI.Models;

public sealed class SuggestionItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public string Category { get; set; } = "General";
    public string Name { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}
