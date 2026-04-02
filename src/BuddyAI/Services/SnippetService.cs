using System.Text;
using System.Text.Json;
using BuddyAI.Models;

namespace BuddyAI.Services;

public sealed class SnippetService
{
    private readonly string _folderPath;
    private readonly string _path;

    public SnippetService()
    {
        // Keep the existing storage root so current user data is not displaced.
        _folderPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BuddyAIDesktop");

        _path = Path.Combine(_folderPath, "snippets.json");
    }

    public string GetStoragePath() => _path;

    public void EnsureFileExists()
    {
        Directory.CreateDirectory(_folderPath);

        if (File.Exists(_path))
            return;

        Save(GetSeedItems());
    }

    public List<SnippetItem> LoadOrSeed()
    {
        EnsureFileExists();
        return LoadFromFile(_path, fallbackToSeed: true);
    }

    public List<SnippetItem> LoadFromFile(string path, bool fallbackToSeed = false)
    {
        if (!File.Exists(path))
            return fallbackToSeed ? GetSeedItems() : new List<SnippetItem>();

        string json = File.ReadAllText(path);
        List<SnippetItem> items = DeserializeItems(json);

        if (items.Count == 0 && fallbackToSeed)
        {
            items = GetSeedItems();
            Save(items);
        }

        return items;
    }

    public void Save(IEnumerable<SnippetItem> items)
    {
        Directory.CreateDirectory(_folderPath);

        List<SnippetItem> normalized = items
            .Select(Normalize)
            .Where(x => !string.IsNullOrWhiteSpace(x.Name) && !string.IsNullOrWhiteSpace(x.Text))
            .ToList();

        string json = JsonSerializer.Serialize(normalized, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(_path, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public void Export(string path, IEnumerable<SnippetItem> items)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Export path is required.", nameof(path));

        string directory = Path.GetDirectoryName(path) ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        List<SnippetItem> normalized = items.Select(Normalize).ToList();
        string json = JsonSerializer.Serialize(normalized, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json, new UTF8Encoding(false));
    }

    public List<SnippetItem> Import(string path, bool mergeWithExisting)
    {
        List<SnippetItem> imported = LoadFromFile(path, fallbackToSeed: false)
            .Select(Normalize)
            .ToList();

        if (!mergeWithExisting)
            return imported;

        List<SnippetItem> existing = LoadOrSeed();
        foreach (SnippetItem candidate in imported)
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

    private static SnippetItem Normalize(SnippetItem item)
    {
        return new SnippetItem
        {
            Id = string.IsNullOrWhiteSpace(item.Id) ? Guid.NewGuid().ToString("n") : item.Id.Trim(),
            Category = string.IsNullOrWhiteSpace(item.Category) ? "General" : item.Category.Trim(),
            Name = item.Name?.Trim() ?? string.Empty,
            Text = item.Text?.Trim() ?? string.Empty
        };
    }

    private static List<SnippetItem> DeserializeItems(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<SnippetItem>();

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return new List<SnippetItem>();

            List<SnippetItem> items = new();

            foreach (JsonElement element in document.RootElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object)
                    continue;

                string id = GetString(element, "Id") ?? GetString(element, "id") ?? Guid.NewGuid().ToString("n");
                string category = GetString(element, "Category") ?? GetString(element, "category") ?? "General";
                string name = GetString(element, "Name") ?? GetString(element, "name") ?? "";
                string text = GetString(element, "Text") ?? GetString(element, "text") ?? GetString(element, "Snippet") ?? GetString(element, "snippet") ?? "";

                items.Add(Normalize(new SnippetItem
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
            return new List<SnippetItem>();
        }
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String)
            return value.GetString();

        return null;
    }

    private static List<SnippetItem> GetSeedItems()
    {
        return new List<SnippetItem>
        {
            new() { Category = "PowerShell", Name = "Try/Catch Template", Text = "try\n{\n    # work here\n}\ncatch\n{\n    Write-Error $_\n    throw\n}" },
            new() { Category = "PowerShell", Name = "Advanced Function", Text = "function Invoke-Task\n{\n    [CmdletBinding()]\n    param(\n        [Parameter(Mandatory)]\n        [string]$Name\n    )\n\n    begin { }\n    process { }\n    end { }\n}" },
            new() { Category = "Terraform", Name = "Module Skeleton", Text = "module \"example\" {\n  source = \"./modules/example\"\n\n  name = var.name\n}" },
            new() { Category = "Bash", Name = "Strict Mode", Text = "#!/usr/bin/env bash\nset -euo pipefail\nIFS=$'\\n\\t'" },
            new() { Category = "JSON", Name = "JSON Envelope", Text = "{\n  \"status\": \"ok\",\n  \"data\": []\n}" },
            new() { Category = "KQL", Name = "Basic Filter", Text = "AppTraces\n| where TimeGenerated > ago(24h)\n| where Message contains \"error\"\n| project TimeGenerated, Message" }
        };
    }
}
