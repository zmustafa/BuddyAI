namespace BuddyAI.Models;

public static class AiProviderTypes
{
    public const string AzureOpenAI = "Azure OpenAI";
    public const string OpenAI = "OpenAI";
    public const string Grok = "GROK";
    public const string Claude = "Claude";
    public const string GoogleGemini = "Google Gemini";
    public const string Mistral = "Mistral";
    public const string Ollama = "Ollama";
    public const string LMStudio = "LM Studio";
    public const string ChatGPTOAuth = "ChatGPT OAuth";
    public const string ClaudeOAuth = "Claude OAuth";

    public static readonly string[] All =
    {
        AzureOpenAI,
        OpenAI,
        Grok,
        Claude,
        GoogleGemini,
        Mistral,
        Ollama,
        LMStudio,
        ChatGPTOAuth,
        ClaudeOAuth
    };
}

public sealed class AiProviderModelDefinition
{
    public string Name { get; set; } = string.Empty;
    public bool SupportsImages { get; set; } = true;
    public bool SupportsTemperature { get; set; } = true;

    public AiProviderModelDefinition Clone()
    {
        return new AiProviderModelDefinition
        {
            Name = Name,
            SupportsImages = SupportsImages,
            SupportsTemperature = SupportsTemperature
        };
    }
}

public sealed class AiProviderDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public string Name { get; set; } = string.Empty;
    public string ProviderType { get; set; } = AiProviderTypes.OpenAI;
    public string BaseUrl { get; set; } = string.Empty;
    public string EndpointPath { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public List<AiProviderModelDefinition> Models { get; set; } = new();

    public override string ToString() => Name;

    public AiProviderDefinition Clone()
    {
        return new AiProviderDefinition
        {
            Id = Id,
            Name = Name,
            ProviderType = ProviderType,
            BaseUrl = BaseUrl,
            EndpointPath = EndpointPath,
            ApiKey = ApiKey,
            Models = Models?.Select(x => x.Clone()).ToList() ?? new List<AiProviderModelDefinition>()
        };
    }
}
