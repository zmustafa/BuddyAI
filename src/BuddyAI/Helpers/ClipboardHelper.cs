using System.Globalization;
using System.Text;

namespace BuddyAI.Helpers;

/// <summary>
/// Encapsulates clipboard read/write operations, rich HTML clipboard formatting,
/// and diagnostic text detection for the BuddyAI shell.
/// </summary>
internal static class ClipboardHelper
{
    private const string ClipboardRichTextCss = @"
body {
    margin: 0;
    color: #15202b;
    font-family: 'Segoe UI', Arial, sans-serif;
    font-size: 12pt;
    line-height: 1.55;
}
.copied-response-block {
    color: #15202b;
}
.copied-response-block h1 {
    margin: 0 0 12px 0;
    font-size: 24px;
    font-weight: 700;
    line-height: 1.25;
}
.copied-response-block h2 {
    margin: 18px 0 10px 0;
    font-size: 20px;
    font-weight: 700;
    line-height: 1.3;
}
.copied-response-block h3 {
    margin: 16px 0 8px 0;
    font-size: 17px;
    font-weight: 700;
    line-height: 1.3;
}
.copied-response-block h4,
.copied-response-block h5,
.copied-response-block h6 {
    margin: 14px 0 8px 0;
    font-size: 15px;
    font-weight: 700;
    line-height: 1.3;
}
.copied-response-block p {
    margin: 0 0 12px 0;
}
.copied-response-block ul,
.copied-response-block ol {
    margin: 0 0 12px 0;
    padding-left: 22px;
}
.copied-response-block li {
    margin: 0 0 6px 0;
}
.copied-response-block strong {
    font-weight: 700;
}
.copied-response-block em {
    font-style: italic;
}
.copied-response-block code {
    background: #eef2f7;
    border: 1px solid #d7e0ea;
    border-radius: 6px;
    padding: 1px 5px;
    font-family: Consolas, monospace;
    font-size: 10pt;
}
.copied-response-block pre {
    margin: 0 0 12px 0;
    background: #0f172a;
    color: #e2e8f0;
    padding: 12px;
    border-radius: 10px;
    border: 1px solid #1e293b;
    font-family: Consolas, monospace;
    font-size: 10pt;
    white-space: pre-wrap;
}
.copied-response-block .muted {
    color: #5f6b7a;
}
.copied-response-block .empty {
    color: #5f6b7a;
    font-style: italic;
}
.copied-response-block .copied-card {
    border: 1px solid #d9e2ec;
    border-radius: 14px;
    background: #ffffff;
    padding: 16px;
}
.copied-response-block .copied-card > h2 {
    margin: 0 0 10px 0;
    font-size: 16px;
    font-weight: 700;
    line-height: 1.3;
}
.copied-response-block .card-copy-body > :first-child {
    margin-top: 0;
}
.copied-response-block .card-copy-body > :last-child {
    margin-bottom: 0;
}
.copied-response-block .pill-list {
    display: block;
    margin: 0;
}
.copied-response-block .pill {
    display: inline-block;
    margin: 0 8px 8px 0;
    padding: 7px 10px;
    border-radius: 999px;
    border: 1px solid transparent;
    font-size: 10pt;
    font-weight: 700;
    line-height: 1.35;
}
.copied-response-block .pill.high {
    background: #e7f8f5;
    color: #0f766e;
    border-color: #b6ece2;
}
.copied-response-block .pill.low {
    background: #fff4e5;
    color: #b45309;
    border-color: #f2d7a4;
}
.copied-response-block .pill.next {
    background: #e8f0ff;
    color: #2563eb;
    border-color: #c6d8ff;
}
.copied-response-block .warning-box {
    background: #fff1ec;
    color: #9a3412;
    border: 1px solid #f2c0ad;
    border-radius: 12px;
    padding: 12px 14px;
}
.copied-response-block .warning-box ul {
    margin: 0;
    padding-left: 18px;
}
";

    public static bool TryCopyTextToClipboard(string text)
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

    public static bool TryCopyRichHtmlToClipboard(string plainText, string htmlFragment)
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

    public static bool TryCopyPngDataUrlToClipboard(string? dataUrl)
    {
        const string prefix = "data:image/png;base64,";
        if (string.IsNullOrWhiteSpace(dataUrl) || !dataUrl.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        return TryCopyPngBase64ToClipboard(dataUrl[prefix.Length..]);
    }

    public static bool TryCopyPngBase64ToClipboard(string? base64)
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

    public static bool LooksLikeDiagnosticText(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length < 8)
            return false;

        string lower = text.ToLowerInvariant();
        return lower.Contains("error") || lower.Contains("exception") || lower.Contains("stack") || lower.Contains("failed") || lower.Contains("timeout");
    }

    public static string BuildClipboardHtml(string htmlFragment)
    {
        if (string.IsNullOrWhiteSpace(htmlFragment))
            return string.Empty;

        string htmlDocument = "<!DOCTYPE html><html><head><meta charset=\"utf-8\" /><style>" + ClipboardRichTextCss + "</style></head><body><!--StartFragment-->"
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

    public static void SetClipboardDataObject(object data)
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
}
