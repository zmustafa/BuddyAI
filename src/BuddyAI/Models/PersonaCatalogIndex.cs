namespace BuddyAI.Models;

public sealed class PersonaCatalogIndex
{
    public string StoreVersion { get; set; } = string.Empty;
    public string GeneratedUtc { get; set; } = string.Empty;
    public int PersonaCount { get; set; }
    public List<string> Categories { get; set; } = new();
    public List<CatalogPersonaEntry> Personas { get; set; } = new();
}

public sealed class CatalogPersonaEntry
{
    public string Id { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public List<string> Roles { get; set; } = new();
    public string Icon { get; set; } = "??";
    public string AccentHex { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string MinAppVersion { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
}
