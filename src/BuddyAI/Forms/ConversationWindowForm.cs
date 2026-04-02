using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace BuddyAI.Forms;

public sealed class ConversationWindowForm : Form
{
    private const float BaseRawFontSize = 10f;
    private const double MinZoomFactor = 0.75d;
    private const double MaxZoomFactor = 2.50d;

    private readonly ConversationWindowFormOptions _options;
    private readonly string _html;
    private readonly WebView2 _web = new() { Dock = DockStyle.Fill, DefaultBackgroundColor = Color.White };
    private readonly ToolStripLabel _zoomLabel = new() { Alignment = ToolStripItemAlignment.Right };
    private Font? _rawZoomFont;
    private double _zoomFactor;

    public ConversationWindowForm(string title, string html, string raw)
        : this(title, html, raw, null)
    {
    }

    public ConversationWindowForm(string title, string html, string raw, ConversationWindowFormOptions? options)
    {
        _options = options ?? new ConversationWindowFormOptions();
        _html = html ?? string.Empty;
        _zoomFactor = Math.Clamp(_options.InitialZoomFactor <= 0d ? 1d : _options.InitialZoomFactor, MinZoomFactor, MaxZoomFactor);

        Text = string.IsNullOrWhiteSpace(_options.WindowTitlePrefix)
            ? title
            : _options.WindowTitlePrefix + " — " + title;

        StartPosition = _options.IsPopupWindow ? FormStartPosition.Manual : FormStartPosition.CenterParent;
        Size = _options.InitialSize;
        MinimumSize = new Size(520, 360);
        ShowIcon = false;
        ShowInTaskbar = _options.ShowInTaskbar;
        TopMost = _options.TopMost;
        ControlBox = true;
        MinimizeBox = !_options.IsPopupWindow;
        MaximizeBox = !_options.IsPopupWindow;
        FormBorderStyle = _options.IsPopupWindow ? FormBorderStyle.SizableToolWindow : FormBorderStyle.Sizable;

        ToolStrip toolbar = BuildToolbar();
        TabControl tabs = new() { Dock = DockStyle.Fill };
        TabPage formattedTab = new("Formatted");
       // TabPage rawTab = new("RawX");

        //_rawText = new TextBox
        //{
        //    Dock = DockStyle.Fill,
        //    Multiline = true,
        //    ScrollBars = ScrollBars.Both,
        //    WordWrap = false,
        //    ReadOnly = true,
        //    Text = raw ?? string.Empty
        //};

        formattedTab.Controls.Add(_web);
       // rawTab.Controls.Add(_rawText);
        tabs.TabPages.Add(formattedTab);
       // tabs.TabPages.Add(rawTab);

        Controls.Add((_web));
        Controls.Add(toolbar);

        Shown += async (_, __) =>
        {
            if (_options.IsPopupWindow)
                PositionBottomRight();

            await LoadHtmlAsync();
            ApplyZoom();
        };

        ResizeEnd += (_, __) => PersistWindowSize();
        FormClosing += (_, __) => PersistWindowSize();
    }

    private ToolStrip BuildToolbar()
    {
        ToolStrip toolbar = new()
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden,
            RenderMode = ToolStripRenderMode.System
        };

        ToolStripButton btnZoomOut = new("A-") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        ToolStripButton btnZoomReset = new("100%") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        ToolStripButton btnZoomIn = new("A+") { DisplayStyle = ToolStripItemDisplayStyle.Text };

        btnZoomOut.Click += (_, __) => ChangeZoom(-0.10d);
        btnZoomReset.Click += (_, __) => SetZoom(1d);
        btnZoomIn.Click += (_, __) => ChangeZoom(0.10d);

        toolbar.Items.Add(btnZoomOut);
        toolbar.Items.Add(btnZoomReset);
        toolbar.Items.Add(btnZoomIn);
        toolbar.Items.Add(new ToolStripSeparator());
        toolbar.Items.Add(_zoomLabel);
        return toolbar;
    }

    private async Task LoadHtmlAsync()
    {
        try
        {
            await _web.EnsureCoreWebView2Async();
            if (_web.CoreWebView2 != null)
            {
                _web.CoreWebView2.Settings.IsWebMessageEnabled = true;
                _web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                _web.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
            }
            ApplyZoom();
            _web.NavigateToString(_html);
        }
        catch
        {
        }
    }

    private void ChangeZoom(double delta)
    {
        SetZoom(_zoomFactor + delta);
    }

    private void SetZoom(double zoomFactor)
    {
        _zoomFactor = Math.Clamp(zoomFactor, MinZoomFactor, MaxZoomFactor);
        ApplyZoom();
        _options.ZoomFactorChanged?.Invoke(_zoomFactor);
    }

    private void ApplyZoom()
    {
        Font? previousFont = _rawZoomFont;
        _rawZoomFont = new Font("Consolas", BaseRawFontSize * (float)_zoomFactor);
       // _rawText.Font = _rawZoomFont;
        previousFont?.Dispose();

        if (_web.CoreWebView2 != null)
            _web.ZoomFactor = _zoomFactor;

        _zoomLabel.Text = $"Zoom {Math.Round(_zoomFactor * 100d):0}%";
    }

    private void PersistWindowSize()
    {
        if (WindowState == FormWindowState.Normal)
            _options.WindowSizeChanged?.Invoke(Size);
    }

    private void PositionBottomRight()
    {
        Rectangle area = _options.WorkingAreaProvider?.Invoke()
            ?? Screen.FromPoint(Cursor.Position).WorkingArea;

        int x = Math.Max(area.Left + 12, area.Right - Width - 16);
        int y = Math.Max(area.Top + 12, area.Bottom - Height - 16);
        Location = new Point(x, y);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.Add)
            || keyData == (Keys.Control | Keys.Oemplus)
            || keyData == (Keys.Control | Keys.Shift | Keys.Oemplus))
        {
            ChangeZoom(0.10d);
            return true;
        }

        if (keyData == (Keys.Control | Keys.Subtract)
            || keyData == (Keys.Control | Keys.OemMinus))
        {
            ChangeZoom(-0.10d);
            return true;
        }

        if (keyData == (Keys.Control | Keys.D0)
            || keyData == (Keys.Control | Keys.NumPad0))
        {
            SetZoom(1d);
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        string requestId = string.Empty;

        try
        {
            string message = args.TryGetWebMessageAsString();
            if (string.IsNullOrWhiteSpace(message))
                return;

            using JsonDocument document = JsonDocument.Parse(message);
            JsonElement root = document.RootElement;

            string type = root.TryGetProperty("type", out JsonElement typeElement)
                ? typeElement.GetString() ?? string.Empty
                : string.Empty;

            requestId = root.TryGetProperty("requestId", out JsonElement requestIdElement)
                ? requestIdElement.GetString() ?? string.Empty
                : string.Empty;

            if (string.Equals(type, "persist-viewer-state", StringComparison.Ordinal))
                return;

            if (string.Equals(type, "launch-url", StringComparison.Ordinal))
            {
                string url = root.TryGetProperty("url", out JsonElement urlElement)
                    ? urlElement.GetString() ?? string.Empty
                    : string.Empty;
                TryLaunchExternalUrl(url);
                return;
            }

            bool success = false;
            string statusMessage = "Copy failed.";

            switch (type)
            {
                case "copy-text":
                    string text = root.TryGetProperty("text", out JsonElement textElement)
                        ? textElement.GetString() ?? string.Empty
                        : string.Empty;
                    success = TryCopyTextToClipboard(text);
                    statusMessage = success ? "Block copied." : "Copy failed.";
                    break;

                case "copy-rich":
                    string richText = root.TryGetProperty("text", out JsonElement richTextElement)
                        ? richTextElement.GetString() ?? string.Empty
                        : string.Empty;
                    string htmlFragment = root.TryGetProperty("html", out JsonElement htmlElement)
                        ? htmlElement.GetString() ?? string.Empty
                        : string.Empty;
                    success = TryCopyRichHtmlToClipboard(richText, htmlFragment);
                    statusMessage = success ? "Block copied." : "Copy failed.";
                    break;

                case "copy-image":
                    string dataUrl = root.TryGetProperty("dataUrl", out JsonElement dataUrlElement)
                        ? dataUrlElement.GetString() ?? string.Empty
                        : string.Empty;
                    success = TryCopyPngDataUrlToClipboard(dataUrl);
                    if (!success)
                        success = await TryCopyWebViewClipToClipboardAsync(root);
                    statusMessage = success ? "Image copied." : "Copy failed.";
                    break;

                default:
                    return;
            }

            if (!string.IsNullOrWhiteSpace(requestId))
                PostCopyResult(requestId, success, statusMessage);
        }
        catch
        {
            if (!string.IsNullOrWhiteSpace(requestId))
                PostCopyResult(requestId, false, "Copy failed.");
        }
    }

    private void PostCopyResult(string requestId, bool success, string message)
    {
        if (_web.CoreWebView2 == null)
            return;

        string payload = JsonSerializer.Serialize(new
        {
            type = "copy-result",
            requestId,
            success,
            message
        });

        _web.CoreWebView2.PostWebMessageAsString(payload);
    }

    private static bool TryCopyTextToClipboard(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        try
        {
            DataObject data = new();
            data.SetData(DataFormats.UnicodeText, true, text);
            data.SetData(DataFormats.Text, true, text);
            SetClipboardDataObject(data);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryCopyRichHtmlToClipboard(string plainText, string htmlFragment)
    {
        if (string.IsNullOrWhiteSpace(plainText) && string.IsNullOrWhiteSpace(htmlFragment))
            return false;

        try
        {
            DataObject data = new();

            if (!string.IsNullOrWhiteSpace(plainText))
            {
                data.SetData(DataFormats.UnicodeText, true, plainText);
                data.SetData(DataFormats.Text, true, plainText);
            }

            if (!string.IsNullOrWhiteSpace(htmlFragment))
            {
                string wrappedFragment = "<div class=\"copied-response-block\">" + htmlFragment + "</div>";
                data.SetData(DataFormats.Html, true, BuildClipboardHtml(wrappedFragment));
            }

            SetClipboardDataObject(data);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryCopyPngDataUrlToClipboard(string? dataUrl)
    {
        const string prefix = "data:image/png;base64,";
        if (string.IsNullOrWhiteSpace(dataUrl) || !dataUrl.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        return TryCopyPngBase64ToClipboard(dataUrl[prefix.Length..]);
    }

    private static bool TryCopyPngBase64ToClipboard(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64))
            return false;

        try
        {
            byte[] bytes = Convert.FromBase64String(base64);
            using MemoryStream stream = new(bytes);
            using Image image = Image.FromStream(stream);
            using Bitmap bitmap = new(image);
            SetClipboardDataObject(bitmap);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> TryCopyWebViewClipToClipboardAsync(JsonElement root)
    {
        if (_web.CoreWebView2 == null)
            return false;

        if (!root.TryGetProperty("bounds", out JsonElement boundsElement) || boundsElement.ValueKind != JsonValueKind.Object)
            return false;

        double x = ReadJsonDouble(boundsElement, "x");
        double y = ReadJsonDouble(boundsElement, "y");
        double width = ReadJsonDouble(boundsElement, "width");
        double height = ReadJsonDouble(boundsElement, "height");
        double scale = ReadJsonDouble(boundsElement, "scale", 1d);

        if (width <= 0 || height <= 0)
            return false;

        object parameters = new
        {
            format = "png",
            captureBeyondViewport = true,
            clip = new
            {
                x = Math.Max(0d, x),
                y = Math.Max(0d, y),
                width = Math.Max(1d, width),
                height = Math.Max(1d, height),
                scale = Math.Clamp(scale, 1d, 4d)
            }
        };

        string protocolResult = await _web.CoreWebView2.CallDevToolsProtocolMethodAsync(
            "Page.captureScreenshot",
            JsonSerializer.Serialize(parameters));

        using JsonDocument resultDocument = JsonDocument.Parse(protocolResult);
        string base64 = resultDocument.RootElement.TryGetProperty("data", out JsonElement dataElement)
            ? dataElement.GetString() ?? string.Empty
            : string.Empty;

        return TryCopyPngBase64ToClipboard(base64);
    }

    private static double ReadJsonDouble(JsonElement element, string propertyName, double defaultValue = 0d)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property))
            return defaultValue;

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetDouble(out double number) => number,
            JsonValueKind.String when double.TryParse(property.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) => parsed,
            _ => defaultValue
        };
    }

    private static bool TryLaunchExternalUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
            return false;

        bool allowedScheme = string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Scheme, Uri.UriSchemeMailto, StringComparison.OrdinalIgnoreCase);

        if (!allowedScheme)
            return false;

        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string BuildClipboardHtml(string htmlFragment)
    {
        if (string.IsNullOrWhiteSpace(htmlFragment))
            return string.Empty;

        string htmlDocument = "<!DOCTYPE html><html><head><meta charset=\"utf-8\" /></head><body><!--StartFragment-->"
            + htmlFragment
            + "<!--EndFragment--></body></html>";

        const string startFragmentMarker = "<!--StartFragment-->";
        const string endFragmentMarker = "<!--EndFragment-->";
        const string headerTemplate = "Version:1.0\r\nStartHTML:{0:D10}\r\nEndHTML:{1:D10}\r\nStartFragment:{2:D10}\r\nEndFragment:{3:D10}\r\n";

        string header = string.Format(CultureInfo.InvariantCulture, headerTemplate, 0, 0, 0, 0);

        int startFragmentIndex = htmlDocument.IndexOf(startFragmentMarker, StringComparison.Ordinal);
        int endFragmentIndex = htmlDocument.IndexOf(endFragmentMarker, StringComparison.Ordinal);
        if (startFragmentIndex < 0 || endFragmentIndex < 0 || endFragmentIndex < startFragmentIndex)
            return htmlDocument;

        int fragmentStartInHtml = startFragmentIndex + startFragmentMarker.Length;
        int fragmentEndInHtml = endFragmentIndex;

        int startHtml = Encoding.UTF8.GetByteCount(header);
        int startFragment = startHtml + Encoding.UTF8.GetByteCount(htmlDocument[..fragmentStartInHtml]);
        int endFragment = startHtml + Encoding.UTF8.GetByteCount(htmlDocument[..fragmentEndInHtml]);
        int endHtml = startHtml + Encoding.UTF8.GetByteCount(htmlDocument);

        header = string.Format(CultureInfo.InvariantCulture, headerTemplate, startHtml, endHtml, startFragment, endFragment);
        return header + htmlDocument;
    }

    private static void SetClipboardDataObject(object data)
    {
        const int maxAttempts = 5;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                Clipboard.SetDataObject(data, true);
                return;
            }
            catch when (attempt < maxAttempts - 1)
            {
                System.Threading.Thread.Sleep(50);
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _web.Dispose();
            _rawZoomFont?.Dispose();
            _rawZoomFont = null;
        }

        base.Dispose(disposing);
    }
}

public sealed class ConversationWindowFormOptions
{
    public bool IsPopupWindow { get; set; }
    public bool TopMost { get; set; }
    public bool ShowInTaskbar { get; set; } = true;
    public string? WindowTitlePrefix { get; set; }
    public Size InitialSize { get; set; } = new(1100, 760);
    public double InitialZoomFactor { get; set; } = 1d;
    public Func<Rectangle>? WorkingAreaProvider { get; set; }
    public Action<double>? ZoomFactorChanged { get; set; }
    public Action<Size>? WindowSizeChanged { get; set; }
}
