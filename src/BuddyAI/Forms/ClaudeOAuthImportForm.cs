using System.Text.Json;
using System.Text.Json;
using Microsoft.Web.WebView2.WinForms;
using BuddyAI.Services;
using Microsoft.Web.WebView2.Core;

namespace BuddyAI.Forms;

public partial class ClaudeOAuthImportForm : Form
{
    private readonly WebView2 _webView = new();
    private readonly Label _lblStatus = new();
    private readonly Button _btnCapture = new();
    public string ImportedProviderId { get; private set; } = Guid.NewGuid().ToString("n");

    public ClaudeOAuthImportForm()
    {
        Text = "Claude OAuth Login";
        Size = new Size(900, 800);
        StartPosition = FormStartPosition.CenterParent;

        Panel topPanel = new()
        {
            Dock = DockStyle.Top,
            Height = 50,
            Padding = new Padding(10, 8, 10, 8)
        };

        _lblStatus.Text = "Log in to your Anthropic account below, then click 'Capture Session'.";
        _lblStatus.Dock = DockStyle.Fill;
        _lblStatus.TextAlign = ContentAlignment.MiddleLeft;

        _btnCapture.Text = "Capture Session";
        _btnCapture.Width = 140;
        _btnCapture.Height = 32;
        _btnCapture.Dock = DockStyle.Right;
        _btnCapture.Click += BtnCapture_Click;

        topPanel.Controls.Add(_lblStatus);
        topPanel.Controls.Add(_btnCapture);
        Controls.Add(topPanel);

        _webView.Dock = DockStyle.Fill;
        Controls.Add(_webView);

        Load += async (s, e) =>
        {
            try
            {
                await _webView.EnsureCoreWebView2Async();
                _webView.CoreWebView2.Navigate("https://claude.ai/login");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize WebView2: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
    }

    private async void BtnCapture_Click(object? sender, EventArgs e)
    {
        try
        {
            _btnCapture.Enabled = false;
            _lblStatus.Text = "Capturing session...";

            var cookieManager = _webView.CoreWebView2.CookieManager;
            var cookies = await cookieManager.GetCookiesAsync("https://claude.ai");

            string? sessionKey = null;
            string? orgId = null;

            foreach (var cookie in cookies)
            {
                if (sessionKey == null &&
                    (cookie.Name == "sessionKey" || cookie.Name == "__session" || cookie.Name == "sk-ant-sid"))
                {
                    sessionKey = cookie.Value;
                }

                if (orgId == null && cookie.Name == "lastActiveOrg")
                {
                    orgId = cookie.Value;
                }

                if (sessionKey != null && orgId != null)
                    break;
            }

            if (string.IsNullOrWhiteSpace(sessionKey) || string.IsNullOrWhiteSpace(orgId))
            {
                // Try extracting via JavaScript as fallback
                string jsResult = await _webView.CoreWebView2.ExecuteScriptAsync("document.cookie");
                string cookieString = JsonSerializer.Deserialize<string>(jsResult) ?? "";

                foreach (string part in cookieString.Split(';'))
                {
                    string trimmed = part.Trim();
                    if (sessionKey == null &&
                        (trimmed.StartsWith("sessionKey=") || trimmed.StartsWith("__session=")))
                    {
                        sessionKey = trimmed.Substring(trimmed.IndexOf('=') + 1);
                    }

                    if (orgId == null && trimmed.StartsWith("lastActiveOrg="))
                    {
                        orgId = trimmed.Substring(trimmed.IndexOf('=') + 1);
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(sessionKey))
            {
                string apiKey = await TryExtractApiKeyFromPage();
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    ClaudeOAuthTokenManager.SaveToken(ImportedProviderId, apiKey, "", 86400 * 365, orgId ?? "");
                    // Reset the bridge so it picks up the fresh session
                    ClaudeWebViewBridge.Instance.Reset();
                    DialogResult = DialogResult.OK;
                    Close();
                    return;
                }

                _lblStatus.Text = "Could not capture session. Make sure you are logged in, then try again.";
                _btnCapture.Enabled = true;
                return;
            }

            if (string.IsNullOrWhiteSpace(orgId))
            {
                _lblStatus.Text = "Session captured but could not find organization ID (lastActiveOrg cookie). Please try again.";
                _btnCapture.Enabled = true;
                return;
            }

            ClaudeOAuthTokenManager.SaveToken(ImportedProviderId, sessionKey, "", 86400 * 30, orgId);

            // Reset the bridge so it picks up the fresh session cookies
            ClaudeWebViewBridge.Instance.Reset();

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Capture error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _lblStatus.Text = "Capture failed. Please try again.";
            _btnCapture.Enabled = true;
        }
    }

    private async Task<string> TryExtractApiKeyFromPage()
    {
        try
        {
            string url = await _webView.CoreWebView2.ExecuteScriptAsync("window.location.href");
            string currentUrl = JsonSerializer.Deserialize<string>(url) ?? "";

            if (!currentUrl.Contains("claude.ai"))
                return "";

            string result = await _webView.CoreWebView2.ExecuteScriptAsync(@"
                (function() {
                    var inputs = document.querySelectorAll('input, code, pre, span');
                    for (var i = 0; i < inputs.length; i++) {
                        var text = inputs[i].textContent || inputs[i].value || '';
                        if (text.startsWith('sk-ant-')) return text.trim();
                    }
                    return '';
                })()
            ");

            return JsonSerializer.Deserialize<string>(result) ?? "";
        }
        catch
        {
            return "";
        }
    }
}
