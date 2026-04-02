using System.Text;
using System.Text.Json;
using BuddyAI.Models;

namespace BuddyAI.Services;

public sealed class AiProviderService
{
    private readonly string _folderPath;
    private readonly string _jsonPath;

    public AiProviderService()
    {
        _folderPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BuddyAIDesktop");

        _jsonPath = Path.Combine(_folderPath, "providers.json");
    }

    public string GetStoragePath() => _jsonPath;

    public void EnsureFileExists()
    {
        Directory.CreateDirectory(_folderPath);

        if (File.Exists(_jsonPath))
            return;

        Save(new List<AiProviderDefinition>());
    }

    public List<AiProviderDefinition> LoadOrSeed()
    {
        EnsureFileExists();
        return LoadFromFile(_jsonPath, fallbackToSeed: false);
    }

    public List<AiProviderDefinition> LoadFromFile(string path, bool fallbackToSeed = false)
    {
        if (!File.Exists(path))
            return fallbackToSeed ? GetSeedRecords() : new List<AiProviderDefinition>();

        try
        {
            string json = File.ReadAllText(path, Encoding.UTF8);
            List<AiProviderDefinition> source = ParseProviders(json);

            List<AiProviderDefinition> normalized = source
                .Select(Normalize)
                .Where(IsUsable)
                .ToList();

            if (normalized.Count == 0 && fallbackToSeed)
            {
                normalized = GetSeedRecords();
                Save(normalized);
                return normalized;
            }

            string normalizedJson = SerializeProviders(normalized);
            if (!JsonDocumentsEquivalent(json, normalizedJson))
                File.WriteAllText(path, normalizedJson, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            return normalized;
        }
        catch
        {
            if (!fallbackToSeed)
                return new List<AiProviderDefinition>();

            List<AiProviderDefinition> seeded = GetSeedRecords();
            Save(seeded);
            return seeded;
        }
    }

    public void Save(IEnumerable<AiProviderDefinition> providers)
    {
        Directory.CreateDirectory(_folderPath);

        List<AiProviderDefinition> normalized = providers
            .Select(Normalize)
            .Where(IsUsable)
            .ToList();

        string json = SerializeProviders(normalized);
        File.WriteAllText(_jsonPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public AiProviderDefinition? FindByName(IEnumerable<AiProviderDefinition> providers, string? providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
            return null;

        string value = providerName.Trim();
        return providers.FirstOrDefault(x =>
            string.Equals(x.Name, value, StringComparison.OrdinalIgnoreCase));
    }

    public AiProviderDefinition? FindByModel(IEnumerable<AiProviderDefinition> providers, string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
            return null;

        string value = model.Trim();
        return providers.FirstOrDefault(x => GetModelNames(x)
            .Any(m => string.Equals(m, value, StringComparison.OrdinalIgnoreCase)));
    }

    public static AiProviderModelDefinition? FindModel(AiProviderDefinition? provider, string? modelName, bool includeHeuristicFallback = true)
    {
        if (provider == null || string.IsNullOrWhiteSpace(modelName))
            return null;

        string value = modelName.Trim();
        AiProviderModelDefinition? configured = NormalizeModels(provider.Models, provider.ProviderType)
            .FirstOrDefault(x => string.Equals(x.Name, value, StringComparison.OrdinalIgnoreCase));

        return configured?.Clone() ?? (includeHeuristicFallback ? CreateModel(provider.ProviderType, value) : null);
    }

    public static List<string> GetModelNames(AiProviderDefinition? provider)
    {
        if (provider == null)
            return new List<string>();

        return NormalizeModels(provider.Models, provider.ProviderType)
            .Select(x => x.Name)
            .ToList();
    }

    public static bool ModelSupportsImages(AiProviderDefinition? provider, string? modelName)
    {
        return FindModel(provider, modelName)?.SupportsImages ?? true;
    }

    public static bool ModelSupportsTemperature(AiProviderDefinition? provider, string? modelName)
    {
        return FindModel(provider, modelName)?.SupportsTemperature ?? true;
    }

    public static string GetDefaultBaseUrl(string providerType)
    {
        return providerType switch
        {
            AiProviderTypes.AzureOpenAI => "https://<DEPLOYMENT_NAME>.cognitiveservices.azure.com/openai",
            AiProviderTypes.OpenAI => "https://api.openai.com/v1",
            AiProviderTypes.ChatGPTOAuth => "https://chatgpt.com/backend-api",
            AiProviderTypes.Grok => "https://api.x.ai/v1",
            AiProviderTypes.Claude => "https://api.anthropic.com",
            AiProviderTypes.ClaudeOAuth => "https://claude.ai",
            AiProviderTypes.GoogleGemini => "https://generativelanguage.googleapis.com/v1beta/openai",
            AiProviderTypes.Mistral => "https://api.mistral.ai/v1",
            AiProviderTypes.Ollama => "http://localhost:11434",
            AiProviderTypes.LMStudio => "http://localhost:1234/v1",
            _ => "https://api.openai.com/v1"
        };
    }

    public static string GetDefaultEndpointPath(string providerType)
    {
        return providerType switch
        {
            AiProviderTypes.AzureOpenAI => "/responses?api-version=2025-04-01-preview",
            AiProviderTypes.OpenAI => "/responses",
            AiProviderTypes.ChatGPTOAuth => "/codex/responses",
            AiProviderTypes.Grok => "/responses",
            AiProviderTypes.Claude => "/v1/messages",
            AiProviderTypes.ClaudeOAuth => "/api/organizations",
            AiProviderTypes.GoogleGemini => "/chat/completions",
            AiProviderTypes.Mistral => "/chat/completions",
            AiProviderTypes.Ollama => "/api/chat",
            AiProviderTypes.LMStudio => "/chat/completions",
            _ => "/responses"
        };
    }

    public static List<AiProviderModelDefinition> GetDefaultModels(string providerType)
    {
        return providerType switch
        {
            AiProviderTypes.AzureOpenAI => new List<AiProviderModelDefinition>
            {
                CreateModel(providerType, "gpt-4.1-260074", supportsImages: true, supportsTemperature: true),
                CreateModel(providerType, "gpt-4.1-mini-637176", supportsImages: true, supportsTemperature: true),
                CreateModel(providerType, "gpt-5.3-codex", supportsImages: false, supportsTemperature: true),
                CreateModel(providerType, "gpt-5.4", supportsImages: true, supportsTemperature: true),
                CreateModel(providerType, "gpt-5-nano", supportsImages: true, supportsTemperature: false)
            },
            AiProviderTypes.OpenAI => new List<AiProviderModelDefinition>
            {
                CreateModel(providerType, "gpt-5.4", supportsImages: true, supportsTemperature: true),
                CreateModel(providerType, "gpt-5.3-codex", supportsImages: false, supportsTemperature: true),
                CreateModel(providerType, "gpt-5.1", supportsImages: true, supportsTemperature: true),
                CreateModel(providerType, "gpt-5-mini", supportsImages: true, supportsTemperature: true),
                CreateModel(providerType, "gpt-5-nano", supportsImages: false, supportsTemperature: false),
                CreateModel(providerType, "gpt-4.1", supportsImages: true, supportsTemperature: true),
                CreateModel(providerType, "gpt-4.1-mini", supportsImages: true, supportsTemperature: true),
                CreateModel(providerType, "gpt-4.1-nano", supportsImages: false, supportsTemperature: false),
                CreateModel(providerType, "gpt-4o", supportsImages: true, supportsTemperature: true),
                CreateModel(providerType, "gpt-4o-realtime-preview", supportsImages: true, supportsTemperature: true),
                CreateModel(providerType, "o3", supportsImages: true, supportsTemperature: false),
                CreateModel(providerType, "o4-mini", supportsImages: true, supportsTemperature: false)
            },
            AiProviderTypes.ChatGPTOAuth => new List<AiProviderModelDefinition>
            {
                CreateModel(providerType, "gpt-5.3-codex", supportsImages: true, supportsTemperature: false)
            },
            AiProviderTypes.Grok => new List<AiProviderModelDefinition>
            {
                CreateModel(providerType, "grok-4-0709", supportsImages: true, supportsTemperature: true),
                CreateModel(providerType, "grok-4.20-beta-0309-non-reasoning", supportsImages: true, supportsTemperature: true),
                CreateModel(providerType, "grok-4.20-beta-0309-reasoning", supportsImages: true, supportsTemperature: true),
                CreateModel(providerType, "grok-4.20-beta-latest-non-reasoning", supportsImages: true, supportsTemperature: true),
                CreateModel(providerType, "grok-4.20-multi-agent-beta-0309", supportsImages: true, supportsTemperature: true),
                CreateModel(providerType, "grok-code-fast-1", supportsImages: false, supportsTemperature: true),
                CreateModel(providerType, "grok-3", supportsImages: false, supportsTemperature: true),
                CreateModel(providerType, "grok-3-mini", supportsImages: false, supportsTemperature: true),
                CreateModel(providerType, "grok-3-mini-latest", supportsImages: false, supportsTemperature: true),
                CreateModel(providerType, "grok-3-fast", supportsImages: false, supportsTemperature: true),
                CreateModel(providerType, "grok-3-mini-fast", supportsImages: false, supportsTemperature: true)
            },
            AiProviderTypes.Claude => new List<AiProviderModelDefinition>
            {
                CreateModel(providerType, "claude-opus-4-6", supportsImages: true, supportsTemperature: true),
                CreateModel(providerType, "claude-sonnet-4-6", supportsImages: true, supportsTemperature: true),
                CreateModel(providerType, "claude-haiku-4-5", supportsImages: true, supportsTemperature: true)
            },
            AiProviderTypes.ClaudeOAuth => new List<AiProviderModelDefinition>
            {
                CreateModel(providerType, "claude-opus-4-6", supportsImages: true, supportsTemperature: true),
                CreateModel(providerType, "claude-sonnet-4-6", supportsImages: true, supportsTemperature: true),
                CreateModel(providerType, "claude-haiku-4-5", supportsImages: true, supportsTemperature: true)
            },
            AiProviderTypes.GoogleGemini => new List<AiProviderModelDefinition>
            {
                CreateModel(providerType, "gemini-2.5-flash-lite", supportsImages: true, supportsTemperature: true),
                CreateModel(providerType, "gemini-2.0-flash", supportsImages: true, supportsTemperature: true),
                CreateModel(providerType, "gemini-1.5-flash", supportsImages: true, supportsTemperature: true),
                CreateModel(providerType, "gemini-2.5-flash", supportsImages: true, supportsTemperature: true),
                CreateModel(providerType, "gemini-3.1-flash-lite", supportsImages: true, supportsTemperature: true),
                CreateModel(providerType, "gemini-2.5-pro", supportsImages: true, supportsTemperature: true),
                CreateModel(providerType, "gemini-2.0-flash-thinking-exp", supportsImages: true, supportsTemperature: true)
                
            },
            AiProviderTypes.Mistral => new List<AiProviderModelDefinition>
            {
                CreateModel(providerType, "ministral-3b-latest", supportsImages: false, supportsTemperature: true),
                CreateModel(providerType, "ministral-8b-latest", supportsImages: false, supportsTemperature: true),
                CreateModel(providerType, "pixtral-12b-2409", supportsImages: true, supportsTemperature: true),
                CreateModel(providerType, "mistral-small-latest", supportsImages: false, supportsTemperature: true),
                CreateModel(providerType, "mistral-7b-instruct-v0.3", supportsImages: false, supportsTemperature: true),
                CreateModel(providerType, "mixtral-8x7b-instruct-v0.1", supportsImages: false, supportsTemperature: true)
            },
            AiProviderTypes.Ollama => new List<AiProviderModelDefinition>
            {
                CreateModel(providerType, "nemotron-cascade-2:latest", supportsImages: false, supportsTemperature: true),
                CreateModel(providerType, "llama3.2-vision:11b", supportsImages: true, supportsTemperature: true),
                CreateModel(providerType, "llama3:latest", supportsImages: false, supportsTemperature: true),
                CreateModel(providerType, "qwen2.5-coder:latest", supportsImages: false, supportsTemperature: true)
            },
            AiProviderTypes.LMStudio => new List<AiProviderModelDefinition>
            {
                CreateModel(providerType, "local-model", supportsImages: true, supportsTemperature: true)
            },
            _ => new List<AiProviderModelDefinition>()
        };
    }

    public static AiProviderModelDefinition CreateModel(
        string providerType,
        string modelName,
        bool? supportsImages = null,
        bool? supportsTemperature = null)
    {
        string normalizedName = (modelName ?? string.Empty).Trim();
        return new AiProviderModelDefinition
        {
            Name = normalizedName,
            SupportsImages = supportsImages ?? InferDefaultModelSupportsImages(providerType, normalizedName),
            SupportsTemperature = supportsTemperature ?? InferDefaultModelSupportsTemperature(providerType, normalizedName)
        };
    }

    public static List<AiProviderModelDefinition> NormalizeModels(IEnumerable<AiProviderModelDefinition>? models, string providerType)
    {
        List<AiProviderModelDefinition> normalized = new();
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        if (models != null)
        {
            foreach (AiProviderModelDefinition? model in models)
            {
                if (model == null)
                    continue;

                foreach (string name in ExpandModelNames(model.Name))
                {
                    if (!seen.Add(name))
                        continue;

                    normalized.Add(CreateModel(
                        providerType,
                        name,
                        model.SupportsImages,
                        model.SupportsTemperature));
                }
            }
        }

        return normalized;
    }

    private static List<AiProviderDefinition> ParseProviders(string json)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return new List<AiProviderDefinition>();

        List<AiProviderDefinition> providers = new();
        foreach (JsonElement item in doc.RootElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;

            providers.Add(ParseProvider(item));
        }

        return providers;
    }

    private static AiProviderDefinition ParseProvider(JsonElement element)
    {
        string rawProviderType = GetString(element, "ProviderType") ?? GetString(element, "providerType") ?? AiProviderTypes.OpenAI;
        string providerType = NormalizeProviderType(rawProviderType);
        bool legacyTemperatureSupported = GetBool(element, "TemperatureSupported")
            ?? GetBool(element, "temperatureSupported")
            ?? true;

        List<AiProviderModelDefinition> models = ParseModels(element, providerType, legacyTemperatureSupported);
        if (models.Count == 0)
            models = GetDefaultModels(providerType);

        return new AiProviderDefinition
        {
            Id = GetString(element, "Id") ?? GetString(element, "id") ?? Guid.NewGuid().ToString("n"),
            Name = GetString(element, "Name") ?? GetString(element, "name") ?? providerType,
            ProviderType = providerType,
            BaseUrl = GetString(element, "BaseUrl") ?? GetString(element, "baseUrl") ?? string.Empty,
            EndpointPath = GetString(element, "EndpointPath") ?? GetString(element, "endpointPath") ?? string.Empty,
            ApiKey = GetString(element, "ApiKey") ?? GetString(element, "apiKey") ?? string.Empty,
            Models = models
        };
    }

    private static List<AiProviderModelDefinition> ParseModels(JsonElement providerElement, string providerType, bool legacyTemperatureSupported)
    {
        if (!TryGetProperty(providerElement, "Models", out JsonElement modelsElement) &&
            !TryGetProperty(providerElement, "models", out modelsElement))
        {
            return new List<AiProviderModelDefinition>();
        }

        List<AiProviderModelDefinition> models = new();

        switch (modelsElement.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (JsonElement item in modelsElement.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        string? value = item.GetString();
                        foreach (string name in ExpandModelNames(value))
                        {
                            models.Add(CreateModel(
                                providerType,
                                name,
                                supportsImages: InferDefaultModelSupportsImages(providerType, name),
                                supportsTemperature: legacyTemperatureSupported && InferDefaultModelSupportsTemperature(providerType, name)));
                        }
                    }
                    else if (item.ValueKind == JsonValueKind.Object)
                    {
                        string? rawName = GetString(item, "Name")
                            ?? GetString(item, "name")
                            ?? GetString(item, "Model")
                            ?? GetString(item, "model");
                        if (string.IsNullOrWhiteSpace(rawName))
                            continue;

                        bool? supportsImages = GetBool(item, "SupportsImages")
                            ?? GetBool(item, "supportsImages")
                            ?? GetBool(item, "ImageSupported")
                            ?? GetBool(item, "imageSupported")
                            ?? GetBool(item, "SupportsVision")
                            ?? GetBool(item, "supportsVision");

                        bool? supportsTemperature = GetBool(item, "SupportsTemperature")
                            ?? GetBool(item, "supportsTemperature")
                            ?? GetBool(item, "TemperatureSupported")
                            ?? GetBool(item, "temperatureSupported");

                        foreach (string name in ExpandModelNames(rawName))
                        {
                            models.Add(CreateModel(
                                providerType,
                                name,
                                supportsImages,
                                supportsTemperature ?? (legacyTemperatureSupported && InferDefaultModelSupportsTemperature(providerType, name))));
                        }
                    }
                }
                break;

            case JsonValueKind.String:
                foreach (string name in ExpandModelNames(modelsElement.GetString()))
                {
                    models.Add(CreateModel(
                        providerType,
                        name,
                        supportsImages: InferDefaultModelSupportsImages(providerType, name),
                        supportsTemperature: legacyTemperatureSupported && InferDefaultModelSupportsTemperature(providerType, name)));
                }
                break;
        }

        return NormalizeModels(models, providerType);
    }

    private static bool IsUsable(AiProviderDefinition provider)
    {
        return !string.IsNullOrWhiteSpace(provider.Name)
            && !string.IsNullOrWhiteSpace(provider.BaseUrl)
            && !string.IsNullOrWhiteSpace(provider.EndpointPath)
            && NormalizeModels(provider.Models, provider.ProviderType).Count > 0;
    }

    private static AiProviderDefinition Normalize(AiProviderDefinition provider)
    {
        string providerType = NormalizeProviderType(provider.ProviderType);
        List<AiProviderModelDefinition> models = NormalizeModels(provider.Models, providerType);
        if (models.Count == 0)
            models = GetDefaultModels(providerType);

        string baseUrl = (provider.BaseUrl ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
            baseUrl = GetDefaultBaseUrl(providerType);
        baseUrl = baseUrl.TrimEnd('/');

        string endpointPath = (provider.EndpointPath ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(endpointPath))
            endpointPath = GetDefaultEndpointPath(providerType);
        if (!endpointPath.StartsWith('/'))
            endpointPath = "/" + endpointPath;

        return new AiProviderDefinition
        {
            Id = string.IsNullOrWhiteSpace(provider.Id) ? Guid.NewGuid().ToString("n") : provider.Id.Trim(),
            Name = string.IsNullOrWhiteSpace(provider.Name) ? providerType : provider.Name.Trim(),
            ProviderType = providerType,
            BaseUrl = baseUrl,
            EndpointPath = endpointPath,
            ApiKey = provider.ApiKey?.Trim() ?? string.Empty,
            Models = models
        };
    }

    private static string NormalizeProviderType(string? providerType)
    {
        string value = (providerType ?? string.Empty).Trim();
        if (string.Equals(value, AiProviderTypes.AzureOpenAI, StringComparison.OrdinalIgnoreCase)) return AiProviderTypes.AzureOpenAI;
        if (string.Equals(value, AiProviderTypes.OpenAI, StringComparison.OrdinalIgnoreCase)) return AiProviderTypes.OpenAI;
        if (string.Equals(value, AiProviderTypes.ChatGPTOAuth, StringComparison.OrdinalIgnoreCase)) return AiProviderTypes.ChatGPTOAuth;
        if (string.Equals(value, AiProviderTypes.ClaudeOAuth, StringComparison.OrdinalIgnoreCase)) return AiProviderTypes.ClaudeOAuth;
        if (string.Equals(value, AiProviderTypes.Grok, StringComparison.OrdinalIgnoreCase)) return AiProviderTypes.Grok;
        if (string.Equals(value, AiProviderTypes.Claude, StringComparison.OrdinalIgnoreCase)) return AiProviderTypes.Claude;
        if (string.Equals(value, AiProviderTypes.GoogleGemini, StringComparison.OrdinalIgnoreCase)) return AiProviderTypes.GoogleGemini;
        if (string.Equals(value, AiProviderTypes.Mistral, StringComparison.OrdinalIgnoreCase)) return AiProviderTypes.Mistral;
        if (string.Equals(value, AiProviderTypes.Ollama, StringComparison.OrdinalIgnoreCase)) return AiProviderTypes.Ollama;
        if (string.Equals(value, AiProviderTypes.LMStudio, StringComparison.OrdinalIgnoreCase)) return AiProviderTypes.LMStudio;

        return AiProviderTypes.OpenAI;
    }

    // ✅ Fix - reuse a static instance (same pattern already used in AiProviderManagerForm)
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private static string SerializeProviders(List<AiProviderDefinition> providers)
    {
        return JsonSerializer.Serialize(providers, SerializerOptions);
    }

    private static bool JsonDocumentsEquivalent(string left, string right)
    {
        try
        {
            using JsonDocument leftDoc = JsonDocument.Parse(left);
            using JsonDocument rightDoc = JsonDocument.Parse(right);
            return leftDoc.RootElement.ToString() == rightDoc.RootElement.ToString();
        }
        catch
        {
            return false;
        }
    }

    private static IEnumerable<string> ExpandModelNames(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            yield break;

        string expanded = raw
            .Replace("\\r\\n", "\n", StringComparison.Ordinal)
            .Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

        foreach (string part in expanded.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string value = part.Trim();
            if (!string.IsNullOrWhiteSpace(value))
                yield return value;
        }
    }

    private static bool InferDefaultModelSupportsImages(string providerType, string modelName)
    {
        string value = (modelName ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(value))
            return true;

        if (value.Contains("codex", StringComparison.OrdinalIgnoreCase))
            return false;
        if (value.Contains("nano", StringComparison.OrdinalIgnoreCase))
            return false;
        if (value.Contains("vision", StringComparison.OrdinalIgnoreCase))
            return true;
        if (providerType == AiProviderTypes.Claude)
            return true;
        if (providerType == AiProviderTypes.ClaudeOAuth)
            return true;
        if (providerType == AiProviderTypes.GoogleGemini)
            return true;
        if (providerType == AiProviderTypes.Mistral)
            return value.Contains("pixtral");
        if (value.StartsWith("claude", StringComparison.OrdinalIgnoreCase))
            return true;
        if (value.StartsWith("gpt-4", StringComparison.OrdinalIgnoreCase))
            return true;
        if (value.StartsWith("gpt-5", StringComparison.OrdinalIgnoreCase))
            return true;
        if (value.StartsWith("grok-code", StringComparison.OrdinalIgnoreCase))
            return false;
        if (providerType == AiProviderTypes.Grok)
            return false;

        return true;
    }

    private static bool InferDefaultModelSupportsTemperature(string providerType, string modelName)
    {
        string value = (modelName ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(value))
            return true;

        if (value.Contains("nano", StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return TryGetProperty(element, propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static bool? GetBool(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out JsonElement value))
            return null;

        if (value.ValueKind == JsonValueKind.True)
            return true;
        if (value.ValueKind == JsonValueKind.False)
            return false;
        if (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out bool parsed))
            return parsed;

        return null;
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    public static List<AiProviderDefinition> GetSeedRecords()
    {
        return new List<AiProviderDefinition>
        {
            new()
            {
                Id = "aac9490b69f44e398a28224bfa3c8633",
                Name = "Azure OpenAI",
                ProviderType = AiProviderTypes.AzureOpenAI,
                BaseUrl = GetDefaultBaseUrl(AiProviderTypes.AzureOpenAI),
                EndpointPath = GetDefaultEndpointPath(AiProviderTypes.AzureOpenAI),
                ApiKey = "XXX",
                Models = GetDefaultModels(AiProviderTypes.AzureOpenAI)
            },
            new()
            {
                Id = "0864f9ae08ce4da78f94336d6e3d7f99",
                Name = "OpenAI",
                ProviderType = AiProviderTypes.OpenAI,
                BaseUrl = GetDefaultBaseUrl(AiProviderTypes.OpenAI),
                EndpointPath = GetDefaultEndpointPath(AiProviderTypes.OpenAI),
                ApiKey = "XXX",
                Models = GetDefaultModels(AiProviderTypes.OpenAI)
            },
            new()
            {
                Id = "fa5e23607bf147978f90e7c9dd320b0b",
                Name = "GROK",
                ProviderType = AiProviderTypes.Grok,
                BaseUrl = GetDefaultBaseUrl(AiProviderTypes.Grok),
                EndpointPath = GetDefaultEndpointPath(AiProviderTypes.Grok),
                ApiKey = "XXX",
                Models = GetDefaultModels(AiProviderTypes.Grok)
            },
            new()
            {
                Id = "3c0943b684884f1791ead0391068f20c",
                Name = "Claude",
                ProviderType = AiProviderTypes.Claude,
                BaseUrl = GetDefaultBaseUrl(AiProviderTypes.Claude),
                EndpointPath = GetDefaultEndpointPath(AiProviderTypes.Claude),
                ApiKey = "XXX",
                Models = GetDefaultModels(AiProviderTypes.Claude)
            },
            new()
            {
                Id = "9c2b43b684884f1791ead0391068f20d",
                Name = "Google Gemini",
                ProviderType = AiProviderTypes.GoogleGemini,
                BaseUrl = GetDefaultBaseUrl(AiProviderTypes.GoogleGemini),
                EndpointPath = GetDefaultEndpointPath(AiProviderTypes.GoogleGemini),
                ApiKey = "XXX",
                Models = GetDefaultModels(AiProviderTypes.GoogleGemini)
            },
            new()
            {
                Id = "e9074d7960af4546b1e30fc5da66be35",
                Name = "Ollama",
                ProviderType = AiProviderTypes.Ollama,
                BaseUrl = GetDefaultBaseUrl(AiProviderTypes.Ollama),
                EndpointPath = GetDefaultEndpointPath(AiProviderTypes.Ollama),
                ApiKey = "XXX",
                Models = GetDefaultModels(AiProviderTypes.Ollama)
            },
            new()
            {
                Id = "a123bc4567894f1791ead0391068f20e",
                Name = "LM Studio",
                ProviderType = AiProviderTypes.LMStudio,
                BaseUrl = GetDefaultBaseUrl(AiProviderTypes.LMStudio),
                EndpointPath = GetDefaultEndpointPath(AiProviderTypes.LMStudio),
                ApiKey = "XXX",
                Models = GetDefaultModels(AiProviderTypes.LMStudio)
            }
        };
    }
}
