namespace BuddyAI.Models;

public class PromptItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public string Category { get; set; } = "GCP";
    public string Name { get; set; } = "";
    public string Text { get; set; } = "";
}