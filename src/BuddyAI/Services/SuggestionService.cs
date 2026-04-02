using System.Text;
using System.Text.Json;
using BuddyAI.Models;

namespace BuddyAI.Services;

public sealed class SuggestionService
{
    private readonly string _folderPath;
    private readonly string _path;

    public SuggestionService()
    {
        // Keep storage alongside the rest of the app data so backup/restore is simple.
        _folderPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BuddyAIDesktop");

        _path = Path.Combine(_folderPath, "suggestions.json");
    }

    public string GetStoragePath() => _path;

    public void EnsureFileExists()
    {
        Directory.CreateDirectory(_folderPath);

        if (File.Exists(_path))
            return;

        Save(GetSeedItems());
    }

    public List<SuggestionItem> LoadOrSeed()
    {
        EnsureFileExists();
        return LoadFromFile(_path, fallbackToSeed: true);
    }

    public List<SuggestionItem> LoadFromFile(string path, bool fallbackToSeed = false)
    {
        if (!File.Exists(path))
            return fallbackToSeed ? GetSeedItems() : new List<SuggestionItem>();

        string json = File.ReadAllText(path);
        List<SuggestionItem> items = DeserializeItems(json);

        if (items.Count == 0 && fallbackToSeed)
        {
            items = GetSeedItems();
            Save(items);
        }

        return items;
    }

    public void Save(IEnumerable<SuggestionItem> items)
    {
        Directory.CreateDirectory(_folderPath);

        List<SuggestionItem> normalized = items
            .Select(Normalize)
            .Where(x => !string.IsNullOrWhiteSpace(x.Name) && !string.IsNullOrWhiteSpace(x.Text))
            .ToList();

        string json = JsonSerializer.Serialize(normalized, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(_path, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public void Export(string path, IEnumerable<SuggestionItem> items)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Export path is required.", nameof(path));

        string directory = Path.GetDirectoryName(path) ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        List<SuggestionItem> normalized = items.Select(Normalize).ToList();
        string json = JsonSerializer.Serialize(normalized, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json, new UTF8Encoding(false));
    }

    public List<SuggestionItem> Import(string path, bool mergeWithExisting)
    {
        List<SuggestionItem> imported = LoadFromFile(path, fallbackToSeed: false)
            .Select(Normalize)
            .ToList();

        if (!mergeWithExisting)
            return imported;

        List<SuggestionItem> existing = LoadOrSeed();
        foreach (SuggestionItem candidate in imported)
        {
            bool exists = existing.Any(x =>
                string.Equals(x.Category, candidate.Category, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.Name, candidate.Name, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.Text, candidate.Text, StringComparison.OrdinalIgnoreCase));

            if (!exists)
                existing.Add(candidate);
        }

        return existing;
    }

    private static SuggestionItem Normalize(SuggestionItem item)
    {
        return new SuggestionItem
        {
            Id = string.IsNullOrWhiteSpace(item.Id) ? Guid.NewGuid().ToString("n") : item.Id.Trim(),
            Category = string.IsNullOrWhiteSpace(item.Category) ? "General" : item.Category.Trim(),
            Name = item.Name?.Trim() ?? string.Empty,
            Text = item.Text?.Trim() ?? string.Empty
        };
    }

    private static List<SuggestionItem> DeserializeItems(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<SuggestionItem>();

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return new List<SuggestionItem>();

            List<SuggestionItem> items = new();
            foreach (JsonElement element in document.RootElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object)
                    continue;

                string id = GetString(element, "Id") ?? GetString(element, "id") ?? Guid.NewGuid().ToString("n");
                string category = GetString(element, "Category") ?? GetString(element, "category") ?? "General";
                string name = GetString(element, "Name") ?? GetString(element, "name") ?? "";
                string text = GetString(element, "Text") ?? GetString(element, "text") ?? GetString(element, "Suggestion") ?? GetString(element, "suggestion") ?? "";

                items.Add(Normalize(new SuggestionItem
                {
                    Id = id,
                    Category = category,
                    Name = name,
                    Text = text
                }));
            }

            return items;
        }
        catch
        {
            return new List<SuggestionItem>();
        }
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String)
            return value.GetString();

        return null;
    }

    private static List<SuggestionItem> GetSeedItems()
    {
        return new List<SuggestionItem>
        {
            new() { Category = "Errors", Name = "Root Cause Analysis", Text = "Analyze this error and propose the top 3 likely root causes, along with the next best verification steps." },
            new() { Category = "Cloud", Name = "Cloud Troubleshooting", Text = "Provide cloud-specific troubleshooting steps and the next commands I should run to verify the fix." },
            new() { Category = "Automation", Name = "Production Script", Text = "Generate a production-minded script with comments, safe defaults, and clear prerequisites." },
            new() { Category = "Data", Name = "Convert to JSON", Text = "Convert this content into clean structured JSON and call out any data-quality issues." },
            new() { Category = "Architecture", Name = "Architecture Review", Text = "Review this architecture for risks, bottlenecks, tradeoffs, and the highest-value next improvements." }
        };
    }
}
