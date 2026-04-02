using System.Text;
using System.Text.Json;

namespace BuddyAI.Services;

public sealed class ClaudeOAuthTokenData
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string OrganizationId { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; } = DateTime.MinValue;
}

public sealed class ClaudeOAuthTokenManager
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BuddyAIDesktop",
        "claude_oauth_tokens.json");

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private static Dictionary<string, ClaudeOAuthTokenData> Load()
    {
        if (!File.Exists(FilePath))
            return new Dictionary<string, ClaudeOAuthTokenData>();

        try
        {
            string json = File.ReadAllText(FilePath, Encoding.UTF8);
            return JsonSerializer.Deserialize<Dictionary<string, ClaudeOAuthTokenData>>(json) ?? new Dictionary<string, ClaudeOAuthTokenData>();
        }
        catch
        {
            return new Dictionary<string, ClaudeOAuthTokenData>();
        }
    }

    private static void Save(Dictionary<string, ClaudeOAuthTokenData> data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        string json = JsonSerializer.Serialize(data, Options);
        File.WriteAllText(FilePath, json, new UTF8Encoding(false));
    }

    public static void SaveToken(string providerId, string accessToken, string refreshToken, int expiresInSeconds, string organizationId = "")
    {
        var data = Load();
        data[providerId] = new ClaudeOAuthTokenData
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            OrganizationId = organizationId ?? string.Empty,
            ExpiresAt = DateTime.UtcNow.AddSeconds(expiresInSeconds - 60) // 1 min buffer
        };
        Save(data);
    }

    public static Task<string> GetAccessTokenAsync(string providerId, CancellationToken cancellationToken)
    {
        var data = Load();
        if (!data.TryGetValue(providerId, out var tokenData))
            return Task.FromResult(string.Empty);

        return Task.FromResult(tokenData.AccessToken);
    }

    public static string GetOrganizationId(string providerId)
    {
        var data = Load();
        if (!data.TryGetValue(providerId, out var tokenData))
            return string.Empty;

        return tokenData.OrganizationId ?? string.Empty;
    }
}
