using System.Text;
using System.Text.Json;
using BuddyAI.Models;

namespace BuddyAI.Services;

public sealed class PromptService
{
    private readonly string _folderPath;
    private readonly string _path;

    public PromptService()
    {
        // Keep the existing AppData folder for backwards compatibility with the
        // user's already working installation and data.
        _folderPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BuddyAIDesktop");

        _path = Path.Combine(_folderPath, "prompts.json");
    }

    public string GetStoragePath() => _path;

    public void EnsureFileExists()
    {
        Directory.CreateDirectory(_folderPath);

        if (File.Exists(_path))
            return;

        Save(GetSeedPrompts());
    }

    public List<PromptItem> LoadOrSeed()
    {
        EnsureFileExists();
        return LoadFromFile(_path, fallbackToSeed: true);
    }

    public List<PromptItem> LoadFromFile(string path, bool fallbackToSeed = false)
    {
        if (!File.Exists(path))
            return fallbackToSeed ? GetSeedPrompts() : new List<PromptItem>();

        string json = File.ReadAllText(path);
        List<PromptItem> items = DeserializeItems(json);

        if (items.Count == 0 && fallbackToSeed)
        {
            items = GetSeedPrompts();
            Save(items);
        }

        return items;
    }

    public void Save(IEnumerable<PromptItem> prompts)
    {
        Directory.CreateDirectory(_folderPath);

        List<PromptItem> normalized = prompts
            .Select(Normalize)
            .Where(x => !string.IsNullOrWhiteSpace(x.Name) && !string.IsNullOrWhiteSpace(x.Text))
            .ToList();

        string json = JsonSerializer.Serialize(normalized, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(_path, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public void Export(string path, IEnumerable<PromptItem> prompts)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Export path is required.", nameof(path));

        string directory = Path.GetDirectoryName(path) ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        List<PromptItem> normalized = prompts.Select(Normalize).ToList();
        string json = JsonSerializer.Serialize(normalized, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json, new UTF8Encoding(false));
    }

    public List<PromptItem> Import(string path, bool mergeWithExisting)
    {
        List<PromptItem> imported = LoadFromFile(path, fallbackToSeed: false)
            .Select(Normalize)
            .ToList();

        if (!mergeWithExisting)
            return imported;

        List<PromptItem> existing = LoadOrSeed();
        foreach (PromptItem candidate in imported)
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

    private static PromptItem Normalize(PromptItem item)
    {
        return new PromptItem
        {
            Id = string.IsNullOrWhiteSpace(item.Id) ? Guid.NewGuid().ToString("n") : item.Id.Trim(),
            Category = string.IsNullOrWhiteSpace(item.Category) ? "General" : item.Category.Trim(),
            Name = item.Name?.Trim() ?? string.Empty,
            Text = item.Text?.Trim() ?? string.Empty
        };
    }

    private static List<PromptItem> DeserializeItems(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<PromptItem>();

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return new List<PromptItem>();

            List<PromptItem> items = new();

            foreach (JsonElement element in document.RootElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object)
                    continue;

                string id = GetString(element, "Id") ?? GetString(element, "id") ?? Guid.NewGuid().ToString("n");
                string category = GetString(element, "Category") ?? GetString(element, "category") ?? "General";
                string name = GetString(element, "Name") ?? GetString(element, "name") ?? "";
                string text = GetString(element, "Text") ?? GetString(element, "text") ?? GetString(element, "Prompt") ?? GetString(element, "prompt") ?? GetString(element, "Template") ?? GetString(element, "template") ?? "";

                items.Add(Normalize(new PromptItem
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
            return new List<PromptItem>();
        }
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String)
            return value.GetString();

        return null;
    }

    private static List<PromptItem> GetSeedPrompts()
    {
        return new List<PromptItem>
        {
            new PromptItem
            {
                Category = "GCP",
                Name = "List VMs (first 5 projects)",
                Text = "Write a gcloud script that lists Compute Engine VMs in the first 5 projects in the current organization."
            },
            new PromptItem
            {
                Category = "Azure",
                Name = "List Storage Accounts",
                Text = "Write an Azure CLI script that lists all storage accounts in the current subscription."
            },
            new PromptItem
            {
                Category = "AWS",
                Name = "List EC2 Instances",
                Text = "Write an AWS CLI script that lists EC2 instances in all regions, output as JSON."
            },
            new PromptItem
            {
                Category = "OCI",
                Name = "List Compute Instances",
                Text = "Write an OCI CLI script that lists compute instances in a compartment, output as JSON."
            }
        };
    }
}
