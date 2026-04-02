using System.Text;
using System.Text.Json;

namespace BuddyAI.Services;

public sealed class M365CopilotOAuthTokenData
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; } = DateTime.MinValue;
}

public sealed class M365CopilotOAuthTokenManager
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BuddyAIDesktop",
        "m365_copilot_oauth_tokens.json");

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private static Dictionary<string, M365CopilotOAuthTokenData> Load()
    {
        if (!File.Exists(FilePath))
            return new Dictionary<string, M365CopilotOAuthTokenData>();

        try
        {
            string json = File.ReadAllText(FilePath, Encoding.UTF8);
            return JsonSerializer.Deserialize<Dictionary<string, M365CopilotOAuthTokenData>>(json) ?? new Dictionary<string, M365CopilotOAuthTokenData>();
        }
        catch
        {
            return new Dictionary<string, M365CopilotOAuthTokenData>();
        }
    }

    private static void Save(Dictionary<string, M365CopilotOAuthTokenData> data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        string json = JsonSerializer.Serialize(data, Options);
        File.WriteAllText(FilePath, json, new UTF8Encoding(false));
    }

    public static void SaveToken(string providerId, string accessToken, string refreshToken, int expiresInSeconds)
    {
        var data = Load();
        data[providerId] = new M365CopilotOAuthTokenData
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddSeconds(expiresInSeconds - 60) // 1 min buffer
        };
        Save(data);
    }

    public static async Task<string> GetAccessTokenAsync(string providerId, CancellationToken cancellationToken)
    {
        var data = Load();
        if (!data.TryGetValue(providerId, out var tokenData))
            return string.Empty;

        if (DateTime.UtcNow < tokenData.ExpiresAt)
            return tokenData.AccessToken;

        // Needs refresh
        if (string.IsNullOrWhiteSpace(tokenData.RefreshToken))
            return string.Empty;

        using HttpClient client = new();
        var request = new HttpRequestMessage(HttpMethod.Post, "https://login.microsoftonline.com/common/oauth2/v2.0/token");
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "refresh_token" },
            { "refresh_token", tokenData.RefreshToken },
            { "client_id", "d3590ed6-52b3-4102-aeff-aad2292ab01c" } // Placeholder generic client id
        });

        try
        {
            var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return tokenData.AccessToken; // fallback to expired, maybe it still works

            string json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            
            string newAccessToken = doc.RootElement.GetProperty("access_token").GetString() ?? "";
            string newRefreshToken = doc.RootElement.TryGetProperty("refresh_token", out var rte) ? rte.GetString() ?? tokenData.RefreshToken : tokenData.RefreshToken;
            int expiresIn = doc.RootElement.TryGetProperty("expires_in", out var ex) ? ex.GetInt32() : 3600;

            SaveToken(providerId, newAccessToken, newRefreshToken, expiresIn);
            return newAccessToken;
        }
        catch
        {
            return tokenData.AccessToken;
        }
    }
}
