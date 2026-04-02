using System.Text.Json;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace BuddyAI.Services;

/// <summary>
/// Maintains a hidden WebView2 instance navigated to claude.ai so that all
/// API requests are executed via JavaScript fetch() inside the browser context,
/// inheriting the full session, cookies, and Cloudflare clearance.
/// </summary>
public sealed class ClaudeWebViewBridge : IDisposable
{
    private static ClaudeWebViewBridge? _instance;
    private static readonly object Lock = new();

    private WebView2? _webView;
    private bool _isReady;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public static ClaudeWebViewBridge Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (Lock)
                {
                    _instance ??= new ClaudeWebViewBridge();
                }
            }
            return _instance;
        }
    }

    private ClaudeWebViewBridge() { }

    /// <summary>
    /// Ensures the hidden WebView2 is initialized and navigated to claude.ai,
    /// sharing the same user data folder as the login WebView2 so cookies persist.
    /// Must be called on the UI thread.
    /// </summary>
    public async Task EnsureInitializedAsync()
    {
        if (_isReady && _webView?.CoreWebView2 != null)
            return;

        await _initLock.WaitAsync();
        try
        {
            if (_isReady && _webView?.CoreWebView2 != null)
                return;

            _webView?.Dispose();
            _webView = new WebView2
            {
                Size = new System.Drawing.Size(1, 1),
                Visible = false
            };

            await _webView.EnsureCoreWebView2Async();

            // Register the NavigationCompleted handler BEFORE calling Navigate
            TaskCompletionSource<bool> navDone = new();
            void OnNavCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs args)
            {
                _webView.CoreWebView2.NavigationCompleted -= OnNavCompleted;
                navDone.TrySetResult(args.IsSuccess);
            }
            _webView.CoreWebView2.NavigationCompleted += OnNavCompleted;

            _webView.CoreWebView2.Navigate("https://claude.ai");

            bool success = await navDone.Task;
            if (!success)
                throw new InvalidOperationException("Failed to navigate hidden WebView2 to claude.ai. Please re-authenticate.");

            _isReady = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// Resets the bridge so the next call to EnsureInitializedAsync creates a fresh WebView2.
    /// Call this after re-authentication.
    /// </summary>
    public void Reset()
    {
        _isReady = false;
        _webView?.Dispose();
        _webView = null;
    }

    /// <summary>
    /// Executes a POST fetch() inside the WebView2 browser context and returns the response body.
    /// This automatically includes all cookies and passes Cloudflare.
    /// Must be called from the UI thread.
    /// </summary>
    public async Task<(int StatusCode, string Body)> FetchPostAsync(string url, string jsonBody, CancellationToken cancellationToken)
    {
        if (!_isReady || _webView?.CoreWebView2 == null)
            throw new InvalidOperationException("Claude WebView bridge is not initialized. Please re-authenticate.");

        // Build a script that passes the JSON body as a variable to avoid escaping issues.
        // The body is injected via JSON.parse() of a properly-escaped string literal.
        string escapedJsonForJs = JsonSerializer.Serialize(jsonBody);  // produces: "\"{ ... }\""
        string escapedUrlForJs = JsonSerializer.Serialize(url);        // produces: "\"https://...\""

        string script = $@"
            (async function() {{
                try {{
                    const resp = await fetch({escapedUrlForJs}, {{
                        method: 'POST',
                        headers: {{
                            'Content-Type': 'application/json',
                            'Accept': 'text/event-stream, application/json'
                        }},
                        body: {escapedJsonForJs}
                    }});
                    const text = await resp.text();
                    return JSON.stringify({{ status: resp.status, body: text }});
                }} catch (e) {{
                    return JSON.stringify({{ status: 0, body: e.message }});
                }}
            }})()";

        string resultJson = await EvalAsync(script);
        return ParseFetchResult(resultJson);
    }

    /// <summary>
    /// Executes a streaming POST fetch() inside the WebView2 browser context.
    /// Collects all SSE data events and returns the accumulated text from
    /// claude.ai's streaming completion format.
    /// Must be called from the UI thread.
    /// </summary>
    public async Task<(int StatusCode, string Body)> FetchPostStreamAsync(string url, string jsonBody, CancellationToken cancellationToken)
    {
        if (!_isReady || _webView?.CoreWebView2 == null)
            throw new InvalidOperationException("Claude WebView bridge is not initialized. Please re-authenticate.");

        string escapedJsonForJs = JsonSerializer.Serialize(jsonBody);
        string escapedUrlForJs = JsonSerializer.Serialize(url);

        string script = $@"
            (async function() {{
                try {{
                    const resp = await fetch({escapedUrlForJs}, {{
                        method: 'POST',
                        headers: {{
                            'Content-Type': 'application/json',
                            'Accept': 'text/event-stream'
                        }},
                        body: {escapedJsonForJs}
                    }});
                    if (!resp.ok) {{
                        const errText = await resp.text();
                        return JSON.stringify({{ status: resp.status, body: errText }});
                    }}
                    const reader = resp.body.getReader();
                    const decoder = new TextDecoder();
                    let fullText = '';
                    let buffer = '';
                    while (true) {{
                        const {{ done, value }} = await reader.read();
                        if (done) break;
                        buffer += decoder.decode(value, {{ stream: true }});
                        const lines = buffer.split('\n');
                        buffer = lines.pop() || '';
                        for (const line of lines) {{
                            if (!line.startsWith('data: ')) continue;
                            const data = line.substring(6).trim();
                            if (data === '[DONE]') continue;
                            try {{
                                const parsed = JSON.parse(data);
                                if (parsed.type === 'completion' && typeof parsed.completion === 'string') {{
                                    fullText += parsed.completion;
                                }}
                            }} catch (e) {{}}
                        }}
                    }}
                    return JSON.stringify({{ status: resp.status, body: fullText }});
                }} catch (e) {{
                    return JSON.stringify({{ status: 0, body: e.message }});
                }}
            }})()";

        string resultJson = await EvalAsync(script);
        return ParseFetchResult(resultJson);
    }

    /// <summary>
    /// Evaluates a JavaScript expression that returns a Promise and awaits the result
    /// using the DevTools Protocol, which correctly resolves async functions.
    /// </summary>
    private async Task<string> EvalAsync(string script)
    {
        if (_webView?.CoreWebView2 == null)
            return "";

        // Use Runtime.evaluate with awaitPromise so the Promise returned by the async IIFE
        // is resolved before the result is returned to C#.
        string paramsJson = JsonSerializer.Serialize(new
        {
            expression = script,
            awaitPromise = true,
            returnByValue = true
        });

        string cdpResult = await _webView.CoreWebView2.CallDevToolsProtocolMethodAsync("Runtime.evaluate", paramsJson);

        using JsonDocument doc = JsonDocument.Parse(cdpResult);
        JsonElement root = doc.RootElement;

        // Check for exceptions
        if (root.TryGetProperty("exceptionDetails", out _))
        {
            string desc = "";
            if (root.TryGetProperty("exceptionDetails", out JsonElement exDetails) &&
                exDetails.TryGetProperty("exception", out JsonElement exObj) &&
                exObj.TryGetProperty("description", out JsonElement exDesc))
            {
                desc = exDesc.GetString() ?? "";
            }
            throw new InvalidOperationException($"JavaScript error in Claude WebView bridge: {desc}");
        }

        // Extract the resolved value
        if (root.TryGetProperty("result", out JsonElement result) &&
            result.TryGetProperty("value", out JsonElement value))
        {
            return value.GetString() ?? "";
        }

        return "";
    }

    private static (int StatusCode, string Body) ParseFetchResult(string resultJson)
    {
        if (string.IsNullOrWhiteSpace(resultJson))
            return (0, "Empty response from Claude WebView bridge.");

        using JsonDocument doc = JsonDocument.Parse(resultJson);
        int statusCode = doc.RootElement.TryGetProperty("status", out JsonElement statusEl) ? statusEl.GetInt32() : 0;
        string body = doc.RootElement.TryGetProperty("body", out JsonElement bodyEl) ? bodyEl.GetString() ?? "" : "";
        return (statusCode, body);
    }

    /// <summary>
    /// Executes a GET fetch() inside the WebView2 browser context and returns the response body.
    /// This automatically includes all cookies and passes Cloudflare.
    /// Must be called from the UI thread.
    /// </summary>
    public async Task<(int StatusCode, string Body)> FetchGetAsync(string url, CancellationToken cancellationToken)
    {
        if (!_isReady || _webView?.CoreWebView2 == null)
            throw new InvalidOperationException("Claude WebView bridge is not initialized. Please re-authenticate.");

        string escapedUrlForJs = JsonSerializer.Serialize(url);

        string script = $@"
            (async function() {{
                try {{
                    const resp = await fetch({escapedUrlForJs}, {{
                        method: 'GET',
                        headers: {{
                            'Accept': 'application/json'
                        }}
                    }});
                    const text = await resp.text();
                    return JSON.stringify({{ status: resp.status, body: text }});
                }} catch (e) {{
                    return JSON.stringify({{ status: 0, body: e.message }});
                }}
            }})()";

        string resultJson = await EvalAsync(script);
        return ParseFetchResult(resultJson);
    }

    /// <summary>
    /// Uploads a file via multipart/form-data fetch() inside the WebView2 browser context.
    /// The file bytes are injected as a base64 string and converted to a Blob in JavaScript.
    /// Returns the status code and response body JSON.
    /// Must be called from the UI thread.
    /// </summary>
    public async Task<(int StatusCode, string Body)> FetchMultipartUploadAsync(
        string url,
        byte[] fileBytes,
        string fileName,
        string mimeType,
        CancellationToken cancellationToken)
    {
        if (!_isReady || _webView?.CoreWebView2 == null)
            throw new InvalidOperationException("Claude WebView bridge is not initialized. Please re-authenticate.");

        string base64Data = Convert.ToBase64String(fileBytes);
        string escapedUrlForJs = JsonSerializer.Serialize(url);
        string escapedFileNameForJs = JsonSerializer.Serialize(fileName);
        string escapedMimeTypeForJs = JsonSerializer.Serialize(mimeType);

        // We split the base64 into chunks injected as a JS array to avoid any single-string limits
        string script = $@"
            (async function() {{
                try {{
                    const b64 = '{base64Data}';
                    const binaryStr = atob(b64);
                    const bytes = new Uint8Array(binaryStr.length);
                    for (let i = 0; i < binaryStr.length; i++) {{
                        bytes[i] = binaryStr.charCodeAt(i);
                    }}
                    const blob = new Blob([bytes], {{ type: {escapedMimeTypeForJs} }});
                    const formData = new FormData();
                    formData.append('file', blob, {escapedFileNameForJs});
                    const resp = await fetch({escapedUrlForJs}, {{
                        method: 'POST',
                        body: formData
                    }});
                    const text = await resp.text();
                    return JSON.stringify({{ status: resp.status, body: text }});
                }} catch (e) {{
                    return JSON.stringify({{ status: 0, body: e.message }});
                }}
            }})()";

        string resultJson = await EvalAsync(script);
        return ParseFetchResult(resultJson);
    }

    public void Dispose()
    {
        _webView?.Dispose();
        _webView = null;
        _isReady = false;
        _initLock.Dispose();
    }
}
