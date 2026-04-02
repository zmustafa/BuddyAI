using System.Text;
using System.Text.Json;
using BuddyAI.Models;

namespace BuddyAI.Services;

public sealed class PersonaService
{
    private readonly string _folderPath;
    private readonly string _jsonPath;

    public PersonaService()
    {
        _folderPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BuddyAIDesktop");

        _jsonPath = Path.Combine(_folderPath, "personas.json");
    }

    public string GetStoragePath() => _jsonPath;

    public void BackupPersonasFile()
    {
        if (!File.Exists(_jsonPath))
            return;

        string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        string backupPath = Path.Combine(_folderPath, $"personas-{timestamp}.json");
        File.Copy(_jsonPath, backupPath, overwrite: true);
    }

    public void EnsureFileExists()
    {
        Directory.CreateDirectory(_folderPath);

        if (File.Exists(_jsonPath))
            return;

        Save(GetSeedRecords());
    }

    public List<PersonaRecord> LoadOrSeed()
    {
        EnsureFileExists();
        return LoadFromFile(_jsonPath, fallbackToSeed: true);
    }

    public List<PersonaRecord> LoadFromFile(string path, bool fallbackToSeed = false)
    {
        if (!File.Exists(path))
            return fallbackToSeed ? GetSeedRecords() : new List<PersonaRecord>();

        string json = File.ReadAllText(path);
        List<PersonaRecord> items = DeserializeRecords(json);

        if (items.Count == 0 && fallbackToSeed)
        {
            items = GetSeedRecords();
            Save(items);
        }

        return items;
    }

    public void Save(IEnumerable<PersonaRecord> records)
    {
        Directory.CreateDirectory(_folderPath);

        List<PersonaRecord> normalized = records
            .Select(Normalize)
            .Where(x => !string.IsNullOrWhiteSpace(x.PersonaName)
                     && !string.IsNullOrWhiteSpace(x.SystemPrompt))
            .ToList();

        string json = JsonSerializer.Serialize(normalized, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(_jsonPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public void Export(string path, IEnumerable<PersonaRecord> records)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Export path is required.", nameof(path));

        string directory = Path.GetDirectoryName(path) ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        List<PersonaRecord> normalized = records.Select(Normalize).ToList();
        string json = JsonSerializer.Serialize(normalized, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json, new UTF8Encoding(false));
    }

    public List<PersonaRecord> Import(string path, bool mergeWithExisting)
    {
        List<PersonaRecord> imported = LoadFromFile(path, fallbackToSeed: false)
            .Select(Normalize)
            .ToList();

        if (!mergeWithExisting)
            return imported;

        List<PersonaRecord> existing = LoadOrSeed();
        foreach (PersonaRecord candidate in imported)
        {
            bool exists = existing.Any(x =>
                string.Equals(x.PersonaName, candidate.PersonaName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.MessageTemplate, candidate.MessageTemplate, StringComparison.OrdinalIgnoreCase));

            if (!exists)
                existing.Add(candidate);
        }

        return existing;
    }

    private static PersonaRecord Normalize(PersonaRecord record)
    {
        return new PersonaRecord
        {
            Id = string.IsNullOrWhiteSpace(record.Id) ? Guid.NewGuid().ToString("n") : record.Id.Trim(),
            Category = string.IsNullOrWhiteSpace(record.Category) ? "General" : record.Category.Trim(),
            PersonaName = record.PersonaName?.Trim() ?? string.Empty,
            SystemPrompt = record.SystemPrompt?.Trim() ?? string.Empty,
            MessageTemplate = record.MessageTemplate?.Trim() ?? string.Empty,
            Icon = string.IsNullOrWhiteSpace(record.Icon) ? "🧠" : record.Icon.Trim(),
            AccentHex = record.AccentHex?.Trim() ?? string.Empty,
            DefaultModel = record.DefaultModel?.Trim() ?? string.Empty,
            Favorite = record.Favorite
        };
    }

    private static List<PersonaRecord> DeserializeRecords(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<PersonaRecord>();

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return new List<PersonaRecord>();

            List<PersonaRecord> records = new();

            foreach (JsonElement element in document.RootElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object)
                    continue;

                string id = GetString(element, "Id") ?? GetString(element, "id") ?? Guid.NewGuid().ToString("n");
                string category = GetString(element, "Category") ?? GetString(element, "category") ?? "General";
                string personaName = GetString(element, "PersonaName") ?? GetString(element, "personaName") ?? string.Empty;
                string systemPrompt = GetString(element, "SystemPrompt") ?? GetString(element, "systemPrompt") ?? GetString(element, "Description") ?? GetString(element, "description") ?? string.Empty;
                string messageTemplate = GetString(element, "MessageTemplate") ?? GetString(element, "messageTemplate") ?? GetString(element, "PrefilledQuestion") ?? GetString(element, "prefilledQuestion") ?? string.Empty;
                string icon = GetString(element, "Icon") ?? GetString(element, "icon") ?? "🧠";
                string accentHex = GetString(element, "AccentHex") ?? GetString(element, "accentHex") ?? string.Empty;
                string defaultModel = GetString(element, "DefaultModel") ?? GetString(element, "defaultModel") ?? string.Empty;
                bool favorite = GetBool(element, "Favorite") ?? GetBool(element, "favorite") ?? false;

                records.Add(Normalize(new PersonaRecord
                {
                    Id = id,
                    Category = category,
                    PersonaName = personaName,
                    SystemPrompt = systemPrompt,
                    MessageTemplate = messageTemplate,
                    Icon = icon,
                    AccentHex = accentHex,
                    DefaultModel = defaultModel,
                    Favorite = favorite
                }));
            }

            return records;
        }
        catch
        {
            return new List<PersonaRecord>();
        }
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String)
            return value.GetString();

        return null;
    }

    private static bool? GetBool(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement value))
            return null;

        if (value.ValueKind == JsonValueKind.True)
            return true;

        if (value.ValueKind == JsonValueKind.False)
            return false;

        if (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out bool parsed))
            return parsed;

        return null;
    }

    private static List<PersonaRecord> GetSeedRecords()
    {
        return new List<PersonaRecord>
        {
            new() { Id = "484f757daa2149e69945a75e438158b1", Category = "DevOps", PersonaName = "Automation Engineer", SystemPrompt = "Act as an automation engineer focused on efficiency and reliability.\r\nProduce robust automation scripts and workflows.", MessageTemplate = "Write an automation script for this task\n do it in this format.", Icon = "🧠", AccentHex = "", DefaultModel = "gpt-5.3-codex", Favorite = false },
            new() { Id = "8cb41ed4ba474f0ca87098895398f83b", Category = "Security", PersonaName = "Compliance Analyst", SystemPrompt = "Act as a compliance analyst.\r\nIdentify regulatory or policy issues and explain required actions.", MessageTemplate = "Review this configuration and identify compliance concerns", Icon = "🧠", AccentHex = "", DefaultModel = "gpt-5.3-codex", Favorite = false },
            new() { Id = "cf52f1cd2cc64e30bab7253f6f4496a1", Category = "Architecture", PersonaName = "Cloud Solutions Architect", SystemPrompt = "Act as a helpful cloud soultions architect.", MessageTemplate = "What do you see? give me bullet points summary.", Icon = "🧠", AccentHex = "", DefaultModel = "gpt-5.3-codex", Favorite = false },
            new() { Id = "fbeb4989ca1e4e7b872527cf5e6a5545", Category = "Architecture", PersonaName = "Cloud Solutions Architect", SystemPrompt = "Act as a cloud solutions architect.\r\nDiagnose issues quickly and provide practical troubleshooting steps.", MessageTemplate = "Check this image and explain the Azure error", Icon = "🧠", AccentHex = "", DefaultModel = "gpt-5.3-codex", Favorite = false },
            new() { Id = "a783d9dedc1f45cfa0aeab0a8b7ae73d", Category = "Architecture", PersonaName = "Cloud Solutions Architect", SystemPrompt = "Act as a cloud solutions architect.\r\nDiagnose issues quickly and provide practical troubleshooting steps.", MessageTemplate = "Check this image and tell me the next action to fix it.\r\nCreate a mermaid diagram to help me understand the problem.\r\nCreate mermaid diagram with the proposed solution.\r\nAlso create a customer ready response.", Icon = "🧠", AccentHex = "#FF8040", DefaultModel = "gpt-5.3-codex", Favorite = false },
            new() { Id = "948be40cc3da44ef8270625b62afbfdd", Category = "Architecture", PersonaName = "Cloud Solutions Architect", SystemPrompt = "Act as a cloud solutions architect.\r\nDiagnose issues quickly and provide practical troubleshooting steps.", MessageTemplate = "Explain the likely cause of this cloud error", Icon = "🧠", AccentHex = "", DefaultModel = "gpt-5.3-codex", Favorite = false },
            new() { Id = "c27a5f17148c4682a84928bf3d93fd35", Category = "Education", PersonaName = "Data Formatter", SystemPrompt = "Act as a data formatting specialist.\r\nConvert and structure data precisely into the requested format.", MessageTemplate = "Clean the JSON faf w", Icon = "🧠", AccentHex = "", DefaultModel = "gpt-5.3-codex", Favorite = false },
            new() { Id = "ee8009f432614ecd855fb45d611b3e42", Category = "Education", PersonaName = "Data Formatter", SystemPrompt = "Act as a data formatting specialist.\r\nConvert and structure data precisely into the requested format.", MessageTemplate = "Convert this CSV into one line per value", Icon = "🧠", AccentHex = "", DefaultModel = "gpt-5.3-codex", Favorite = false },
            new() { Id = "8cc485fbe69d41f7aaf86b422b6d7b80", Category = "Education", PersonaName = "Data Formatter", SystemPrompt = "Act as a data formatting specialist.\r\nConvert and structure data precisely into the requested format.", MessageTemplate = "Convert this data into structured JSON", Icon = "🧠", AccentHex = "", DefaultModel = "gpt-5.3-codex", Favorite = false },
            new() { Id = "abac3de9ea454bbaae94fdb3061c8b36", Category = "Education", PersonaName = "Data Formatter", SystemPrompt = "Act as a data formatting specialist.\r\nConvert and structure data precisely into the requested format.", MessageTemplate = "Convert this to CSV", Icon = "🧠", AccentHex = "", DefaultModel = "gpt-5.3-codex", Favorite = false },
            new() { Id = "5605fe7a222040eb85e046fdba11f413", Category = "Education", PersonaName = "Data Formatter", SystemPrompt = "Act as a data formatting specialist.\r\nConvert and structure data precisely into the requested format.", MessageTemplate = "Convert to and from Base64", Icon = "🧠", AccentHex = "", DefaultModel = "gpt-5.3-codex", Favorite = false },
            new() { Id = "b5b9b9669166464a9afd2fffe0df56a4", Category = "Education", PersonaName = "Math Teacher", SystemPrompt = "Act as a helpful enterprise assistant.", MessageTemplate = "What is the correct answer(s)?", Icon = "🧠", AccentHex = "", DefaultModel = "gpt-5.3-codex", Favorite = false },
            new() { Id = "24fb9dcaa2244bc1ba2d02a7380892a8", Category = "Education", PersonaName = "Math Teacher", SystemPrompt = "Act as a smart mathematician and respond precisely and accurately.", MessageTemplate = "Add these numbers", Icon = "🧠", AccentHex = "#008000", DefaultModel = "gpt-5.3-codex", Favorite = false },
            new() { Id = "62cd6098e73f450b8d1fd88de000a253", Category = "Education", PersonaName = "Math Teacher", SystemPrompt = "Act as a smart mathematician and respond precisely and accurately.", MessageTemplate = "Explain this algebra problem", Icon = "🧠", AccentHex = "", DefaultModel = "gpt-5.3-codex", Favorite = false },
            new() { Id = "17ccda15549144a39d391ef0bb63e29d", Category = "DevOps", PersonaName = "PowerShell Automation Engineer", SystemPrompt = "Act as a senior PowerShell automation engineer.\r\nProduce production-ready scripts with clear structure.", MessageTemplate = "Write a PowerShell script to accomplish this task", Icon = "🧠", AccentHex = "", DefaultModel = "gpt-5.3-codex", Favorite = false },
            new() { Id = "abee08393edc472188275eb9aaea9957", Category = "Support", PersonaName = "Regex Engineer", SystemPrompt = "Act as a regex engineer.\r\nDesign precise regular expressions to match or transform text patterns.", MessageTemplate = "Write a regex pattern for this requirement", Icon = "🧠", AccentHex = "", DefaultModel = "gpt-5.3-codex", Favorite = false },
            new() { Id = "16d05b252f25442496d1eaab3a43014b", Category = "Education", PersonaName = "Scientific Research Assistant", SystemPrompt = "Act as a scientific research assistant.\r\nSummarize research findings clearly and explain complex scientific concepts simply.", MessageTemplate = "Summarize the key findings of this research paper", Icon = "🧠", AccentHex = "", DefaultModel = "gpt-5.3-codex", Favorite = false },
            new() { Id = "38571febf53449c099da4f8159d771e5", Category = "Coder", PersonaName = "Coder: Explain Code", SystemPrompt = "Act as a senior software engineer.\r\n\r\nExplain Code", MessageTemplate = "", Icon = "🧠", AccentHex = "", DefaultModel = "gpt-5.3-codex", Favorite = true },
            new() { Id = "4e72c0ad7cae4f488d82e1fbe7ede919", Category = "Coder", PersonaName = "Coder: Complete Code", SystemPrompt = "Act as a senior software engineer.\r\n\r\nComplete code.\r\n\r\nOnly return code with optional inline comments.", MessageTemplate = "", Icon = "🧠", AccentHex = "#008080", DefaultModel = "gpt-5.3-codex", Favorite = true },
            new() { Id = "ecdcb9a3d4df47da9c0237d1e372e6ac", Category = "Coder", PersonaName = "Coder: Find Bug", SystemPrompt = "Act as a senior software engineer.\r\n\r\nFind bug in the code.\r\n\r\nOnly return code with optional inline comments.", MessageTemplate = "", Icon = "🧠", AccentHex = "", DefaultModel = "gpt-5.3-codex", Favorite = true },
            new() { Id = "f3c221a252b2473fb4279dee7f0e252e", Category = "Coder", PersonaName = "Coder: Optimize Code", SystemPrompt = "Act as a senior software engineer.\r\n\r\nOptimize Code\r\n\r\nOnly return code with optional inline comments.", MessageTemplate = "", Icon = "🧠", AccentHex = "", DefaultModel = "gpt-5.3-codex", Favorite = true },
            new() { Id = "13b090e06a2c4dacb3896df5f165e7bf", Category = "Coder", PersonaName = "Coder: Add Comment", SystemPrompt = "Act as a senior software engineer.\r\n\r\nAdd Comments\r\n\r\nOnly return code with inline comments.", MessageTemplate = "", Icon = "🧠", AccentHex = "", DefaultModel = "gpt-5.3-codex", Favorite = true },
            new() { Id = "5a04607d2d634421a0e2583f51f5d71f", Category = "Coder", PersonaName = "Coder: Generate Unit Test", SystemPrompt = "Act as a senior software engineer.\r\n\r\nGenerate Unit Test\r\n\r\nOnly return code with optional inline comments.", MessageTemplate = "", Icon = "🧠", AccentHex = "", DefaultModel = "gpt-5.3-codex", Favorite = true },
            new() { Id = "6640eadaabc54ef79f9c422ea68137f6", Category = "Engineering", PersonaName = "SE: Explain", SystemPrompt = "Act as a senior software engineer.\r\nProvide precise, production-quality code and explain architecture decisions clearly.", MessageTemplate = "Write code to implement this functionality", Icon = "🧠", AccentHex = "", DefaultModel = "gpt-5.3-codex", Favorite = false },
            new() { Id = "5eae67f9568d4a068fff2f189714bf98", Category = "Support", PersonaName = "Support Engineer", SystemPrompt = "Act as a senior support engineer.\r\nRead the screenshot carefully, infer the likely issue, explain it clearly, and suggest the best next troubleshooting steps.", MessageTemplate = "Check this image and tell me the likely next troubleshooting steps", Icon = "🧠", AccentHex = "", DefaultModel = "gpt-5.3-codex", Favorite = false },
            new() { Id = "a0c6991a7f334fa8abf6e2c3b407127e", Category = "Support", PersonaName = "Support Engineer", SystemPrompt = "Act as a senior support engineer.\r\nRead the screenshot carefully, infer the likely issue, explain it clearly, and suggest the best next response to the customer.", MessageTemplate = "Check this image and tell me what to respond to the customer", Icon = "🧠", AccentHex = "", DefaultModel = "gpt-5.3-codex", Favorite = false },
            new() { Id = "e15575655dbb483a92cbfc077feeffc7", Category = "Support", PersonaName = "Technical Writer", SystemPrompt = "Act as a technical writer.\r\nProduce clear and well-structured documentation.", MessageTemplate = "Write documentation for this system", Icon = "🧠", AccentHex = "", DefaultModel = "gpt-5.3-codex", Favorite = false },
            new() { Id = "8647f91e241746f68b32815133eb5695", Category = "Security", PersonaName = "Threat Hunter", SystemPrompt = "Act as a cybersecurity threat hunter.\r\nIdentify suspicious behavior patterns and explain potential threats.", MessageTemplate = "Analyze this activity and identify possible threats", Icon = "🧠", AccentHex = "", DefaultModel = "gpt-5.3-codex", Favorite = false },
            new() { Id = "9ccfc0f82d8d43cabd4177ff119ec3ff", Category = "General", PersonaName = "Restaurant", SystemPrompt = "Act as a chef.", MessageTemplate = "create a mermaid diagram to bake a pizza. also include detailed explanation", Icon = "🧠", AccentHex = "", DefaultModel = "gpt-5.3-codex", Favorite = false }
        };
    }
}
