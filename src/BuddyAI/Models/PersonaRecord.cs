namespace BuddyAI.Models;

public sealed class PersonaRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public string Category { get; set; } = "General";
    public string PersonaName { get; set; } = string.Empty;
    public string SystemPrompt { get; set; } = string.Empty;
    public string MessageTemplate { get; set; } = string.Empty;
    public string Icon { get; set; } = "🧠";
    public string AccentHex { get; set; } = string.Empty;
    public string DefaultModel { get; set; } = string.Empty;
    public bool Favorite { get; set; }

    public PersonaRecord Clone()
    {
        return new PersonaRecord
        {
            Id = Id,
            Category = Category,
            PersonaName = PersonaName,
            SystemPrompt = SystemPrompt,
            MessageTemplate = MessageTemplate,
            Icon = Icon,
            AccentHex = AccentHex,
            DefaultModel = DefaultModel,
            Favorite = Favorite
        };
    }
}
