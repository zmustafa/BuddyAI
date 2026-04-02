using System.Text;
using System.Text.Json;

namespace BuddyAI.Services;

public sealed class ChatGPTOAuthTokenData
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; } = DateTime.MinValue;
}

public sealed class ChatGPTOAuthTokenManager
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BuddyAIDesktop",
        "chatgpt_oauth_tokens.json");

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private static Dictionary<string, ChatGPTOAuthTokenData> Load()
    {
        if (!File.Exists(FilePath))
            return new Dictionary<string, ChatGPTOAuthTokenData>();

        try
        {
            string json = File.ReadAllText(FilePath, Encoding.UTF8);
            return JsonSerializer.Deserialize<Dictionary<string, ChatGPTOAuthTokenData>>(json) ?? new Dictionary<string, ChatGPTOAuthTokenData>();
        }
        catch
        {
            return new Dictionary<string, ChatGPTOAuthTokenData>();
        }
    }

    private static void Save(Dictionary<string, ChatGPTOAuthTokenData> data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        string json = JsonSerializer.Serialize(data, Options);
        File.WriteAllText(FilePath, json, new UTF8Encoding(false));
    }

    public static void SaveToken(string providerId, string accessToken, string refreshToken, int expiresInSeconds)
    {
        var data = Load();
        data[providerId] = new ChatGPTOAuthTokenData
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
        var request = new HttpRequestMessage(HttpMethod.Post, "https://auth.openai.com/oauth/token");
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "refresh_token" },
            { "refresh_token", tokenData.RefreshToken },
            { "client_id", "app_EMoamEEZ73f0CkXaXp7hrann" }
        });

        try
        {
            var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return tokenData.AccessToken; // fallback to expired, maybe it still works

            string json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            
            string newAccessToken = doc.RootElement.GetProperty("access_token").GetString() ?? "";
            string newRefreshToken = doc.RootElement.GetProperty("refresh_token").GetString() ?? tokenData.RefreshToken;
            int expiresIn = doc.RootElement.GetProperty("expires_in").GetInt32();

            SaveToken(providerId, newAccessToken, newRefreshToken, expiresIn);
            return newAccessToken;
        }
        catch
        {
            return tokenData.AccessToken;
        }
    }
}
