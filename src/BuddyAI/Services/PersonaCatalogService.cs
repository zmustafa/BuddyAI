using System.Text;
using System.Text.Json;
using BuddyAI.Models;

namespace BuddyAI.Services;

public sealed class PersonaCatalogService
{
    private const string CatalogIndexUrl =
        "https://github.com/zmustafa/BuddyAI/raw/refs/heads/main/persona-catalog/index.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly HttpClient SharedClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    public async Task<PersonaCatalogIndex> FetchCatalogIndexAsync(CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await SharedClient.GetAsync(CatalogIndexUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<PersonaCatalogIndex>(json, JsonOptions)
               ?? throw new InvalidOperationException("Failed to parse persona catalog index.");
    }

    public async Task<PersonaRecord> DownloadPersonaAsync(
        CatalogPersonaEntry entry,
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await SharedClient.GetAsync(entry.DownloadUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParsePersonaJson(json);
    }

    public async Task<List<PersonaRecord>> DownloadPersonasAsync(
        IEnumerable<CatalogPersonaEntry> entries,
        IProgress<(int completed, int total, string name)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        List<CatalogPersonaEntry> entryList = entries.ToList();
        List<PersonaRecord> results = new();
        int completed = 0;

        foreach (CatalogPersonaEntry entry in entryList)
        {
            cancellationToken.ThrowIfCancellationRequested();

            PersonaRecord record = await DownloadPersonaAsync(entry, cancellationToken);
            results.Add(record);

            completed++;
            progress?.Report((completed, entryList.Count, entry.Name));
        }

        return results;
    }

    public static List<PersonaRecord> MergeWithExisting(
        List<PersonaRecord> existing,
        List<PersonaRecord> downloaded)
    {
        List<PersonaRecord> merged = new(existing);

        foreach (PersonaRecord candidate in downloaded)
        {
            bool exists = merged.Any(x =>
                string.Equals(x.Id, candidate.Id, StringComparison.OrdinalIgnoreCase));

            if (exists)
            {
                int index = merged.FindIndex(x =>
                    string.Equals(x.Id, candidate.Id, StringComparison.OrdinalIgnoreCase));
                merged[index] = candidate;
            }
            else
            {
                merged.Add(candidate);
            }
        }

        return merged;
    }

    private static PersonaRecord ParsePersonaJson(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        return new PersonaRecord
        {
            Id = GetString(root, "id") ?? Guid.NewGuid().ToString("n"),
            Category = GetString(root, "category") ?? "General",
            PersonaName = GetString(root, "personaName") ?? string.Empty,
            SystemPrompt = GetString(root, "systemPrompt") ?? string.Empty,
            MessageTemplate = GetString(root, "messageTemplate") ?? string.Empty,
            Icon = GetString(root, "icon") ?? "??",
            AccentHex = GetString(root, "accentHex") ?? string.Empty,
            DefaultModel = GetString(root, "defaultModel") ?? string.Empty,
            Favorite = GetBool(root, "favorite") ?? false
        };
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out JsonElement value) &&
            value.ValueKind == JsonValueKind.String)
            return value.GetString();

        return null;
    }

    private static bool? GetBool(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    }
