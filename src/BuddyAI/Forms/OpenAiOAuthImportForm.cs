using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using Microsoft.Web.WebView2.WinForms;
using BuddyAI.Services;
using Microsoft.Web.WebView2.Core;
using System.Security.Cryptography;

namespace BuddyAI.Forms;

public partial class ChatGPTOAuthImportForm : Form
{
    private readonly WebView2 _webView = new();
    private readonly string _codeVerifier;
    private readonly string _state;
    public string ImportedProviderId { get; private set; } = Guid.NewGuid().ToString("n");

    public ChatGPTOAuthImportForm()
    {
        Text = "ChatGPT OAuth Login";
        Size = new Size(600, 800);
        StartPosition = FormStartPosition.CenterParent;

        _webView.Dock = DockStyle.Fill;
        Controls.Add(_webView);

        _codeVerifier = GenerateCodeVerifier();
        _state = Guid.NewGuid().ToString("n");

        Load += async (s, e) =>
        {
            try
            {
                await _webView.EnsureCoreWebView2Async();
                _webView.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
                var scope = "openid profile email offline_access";
                string codeChallenge = GenerateCodeChallenge(_codeVerifier);
                string url = "https://auth.openai.com/oauth/authorize?" +
                    "response_type=code" +
                    "&client_id=app_EMoamEEZ73f0CkXaXp7hrann" +
                    $"&redirect_uri={Uri.EscapeDataString("http://localhost:1455/auth/callback")}" +
                    "&scope=" + Uri.EscapeDataString(scope) +
                    $"&code_challenge={codeChallenge}" +
                    "&code_challenge_method=S256" +
                    $"&state={_state}" +
                    "&id_token_add_organizations=true" +
                    "&codex_cli_simplified_flow=true";

                _webView.CoreWebView2.Navigate(url);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize WebView2: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
    }

    private async void CoreWebView2_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (e.Uri.StartsWith("http://localhost:1455/auth/callback"))
        {
            e.Cancel = true;
            
            var uri = new Uri(e.Uri);
            var queryParams = uri.Query.TrimStart('?').Split('&')
                .Select(p => p.Split('='))
                .Where(p => p.Length == 2)
                .ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]));

            if (queryParams.TryGetValue("code", out string? code) && !string.IsNullOrEmpty(code))
            {
                await ExchangeCodeForTokenAsync(code);
            }
            else
            {
                string error = queryParams.TryGetValue("error", out string? err) ? err : "Unknown error";
                MessageBox.Show($"Authorization failed: {error}", "OAuth Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private async Task ExchangeCodeForTokenAsync(string code)
    {
        try
        {
            using var client = new HttpClient();
            var req = new HttpRequestMessage(HttpMethod.Post, "https://auth.openai.com/oauth/token");
            req.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "grant_type", "authorization_code" },
                { "code", code },
                { "redirect_uri", "http://localhost:1455/auth/callback" },
                { "client_id", "app_EMoamEEZ73f0CkXaXp7hrann" },
                { "code_verifier", _codeVerifier }
            });

            var res = await client.SendAsync(req);
            string json = await res.Content.ReadAsStringAsync();
            
            if (!res.IsSuccessStatusCode)
            {
                MessageBox.Show($"Token exchange failed: {json}", "OAuth Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            using var doc = JsonDocument.Parse(json);
            string accessToken = doc.RootElement.GetProperty("access_token").GetString() ?? "";
            string refreshToken = doc.RootElement.TryGetProperty("refresh_token", out var rte) ? rte.GetString() ?? "" : "";
            int expiresIn = doc.RootElement.TryGetProperty("expires_in", out var ex) ? ex.GetInt32() : 3600;

            ChatGPTOAuthTokenManager.SaveToken(ImportedProviderId, accessToken, refreshToken, expiresIn);

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"OAuth error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string GenerateCodeVerifier()
    {
        byte[] bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    private static string GenerateCodeChallenge(string verifier)
    {
        byte[] challengeBytes = SHA256.HashData(Encoding.UTF8.GetBytes(verifier));
        return Base64UrlEncode(challengeBytes);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }
}
