using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BuddyAI;

public sealed partial class AIQ
{
    private static readonly Regex HeadingRegex = new(@"^(#{1,6})\s+(.+)$", RegexOptions.Compiled);
    private static readonly Regex UnorderedBulletRegex = new(@"^[-*•]\s+", RegexOptions.Compiled);
    private static readonly Regex OrderedBulletRegex = new(@"^\d+\.\s+", RegexOptions.Compiled);
    private static readonly Regex CodeBlockFenceRegex = new("```(?:[^\\r\\n]*)\\r?\\n(?<code>[\\s\\S]*?)```", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex InlineBoldAsteriskRegex = new(@"\*\*(.+?)\*\*", RegexOptions.Compiled);
    private static readonly Regex InlineBoldUnderscoreRegex = new(@"__(.+?)__", RegexOptions.Compiled);
    private static readonly Regex InlineItalicAsteriskRegex = new(@"(?<!\*)\*(?!\s)(.+?)(?<!\s)\*(?!\*)", RegexOptions.Compiled);
    private static readonly Regex InlineItalicUnderscoreRegex = new(@"(?<!_)_(?!\s)(.+?)(?<!\s)_(?!_)", RegexOptions.Compiled);
    private static readonly Regex InlineCodeRegex = new(@"`([^`]+)`", RegexOptions.Compiled);
    private static readonly Regex TableSeparatorRegex = new(@"^:?-{3,}:?$", RegexOptions.Compiled);
    private static readonly Regex MermaidFirstLineRegex = new(
        @"^(graph|flowchart|sequenceDiagram|classDiagram|stateDiagram(?:-v2)?|erDiagram|journey|gantt|pie|mindmap|timeline|quadrantChart|requirementDiagram|gitGraph|c4context|c4container|c4component|c4dynamic|c4deployment|xychart-beta|sankey-beta|block-beta|architecture-beta|packet-beta)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static ResponseAnalysis AnalyzeResponse(string rawResponse)
    {
        string text = (rawResponse ?? string.Empty).Replace("\r\n", "\n").Trim();
        if (string.IsNullOrWhiteSpace(text))
            return ResponseAnalysis.Empty;

        string[] lines = text.Split('\n');

        List<string> bullets = new();
        List<string> paragraphs = new();
        StringBuilder paragraphBuilder = new();

        bool inCodeBlock = false;

        foreach (string originalLine in lines)
        {
            string line = originalLine ?? string.Empty;
            string trimmed = line.Trim();

            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                inCodeBlock = !inCodeBlock;
                FlushParagraph(paragraphBuilder, paragraphs);
                continue;
            }

            if (inCodeBlock)
                continue;

            if (string.IsNullOrWhiteSpace(trimmed))
            {
                FlushParagraph(paragraphBuilder, paragraphs);
                continue;
            }

            Match headingMatch = HeadingRegex.Match(trimmed);
            if (headingMatch.Success)
            {
                FlushParagraph(paragraphBuilder, paragraphs);
                string headingText = headingMatch.Groups[2].Value.Trim();
                if (!string.IsNullOrWhiteSpace(headingText))
                    paragraphs.Add(headingText);
                continue;
            }

            if (UnorderedBulletRegex.IsMatch(trimmed))
            {
                FlushParagraph(paragraphBuilder, paragraphs);
                bullets.Add(UnorderedBulletRegex.Replace(trimmed, "").Trim());
                continue;
            }

            if (OrderedBulletRegex.IsMatch(trimmed))
            {
                FlushParagraph(paragraphBuilder, paragraphs);
                bullets.Add(OrderedBulletRegex.Replace(trimmed, "").Trim());
                continue;
            }

            if (paragraphBuilder.Length > 0)
                paragraphBuilder.Append(' ');

            paragraphBuilder.Append(trimmed);
        }

        FlushParagraph(paragraphBuilder, paragraphs);

        string summary =
            paragraphs.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p))
            ?? bullets.FirstOrDefault()
            ?? text;

        List<string> highConfidence = new();
        List<string> lowConfidence = new();
        List<string> recommendations = new();
        List<string> warnings = new();

        foreach (string item in bullets)
        {
            if (ContainsUncertainty(item))
                lowConfidence.Add(item);
            else
                highConfidence.Add(item);

            if (LooksLikeRecommendation(item))
                recommendations.Add(item);
        }

        foreach (string paragraph in paragraphs.Skip(1))
        {
            if (LooksLikeRecommendation(paragraph))
                recommendations.Add(paragraph);

            if (ContainsUncertainty(paragraph))
                warnings.Add(paragraph);
        }

        if (ContainsUncertainty(summary))
            warnings.Insert(0, summary);

        return new ResponseAnalysis(
            summary.Trim(),
            highConfidence.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            lowConfidence.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            recommendations.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
    }

    private static void FlushParagraph(StringBuilder builder, List<string> paragraphs)
    {
        if (builder.Length == 0)
            return;

        string paragraph = builder.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(paragraph))
            paragraphs.Add(paragraph);

        builder.Clear();
    }

    private static bool ContainsUncertainty(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string s = value.ToLowerInvariant();

        string[] patterns =
        {
            "appears",
            "likely",
            "possibly",
            "maybe",
            "unclear",
            "partially unclear",
            "too blurry",
            "blurry",
            "unreadable",
            "not fully readable",
            "seems",
            "partially",
            "probably",
            "could be"
        };

        return patterns.Any(s.Contains);
    }

    private static bool LooksLikeRecommendation(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string s = value.ToLowerInvariant();

        string[] patterns =
        {
            "upload a clearer",
            "clearer photo",
            "closer photo",
            "good light",
            "full label",
            "in focus",
            "next step",
            "recommend",
            "should",
            "please upload",
            "try again",
            "better photo",
            "respond to the customer",
            "tell the customer"
        };

        return patterns.Any(s.Contains);
    }


    private static string BuildResponseHtml(string rawResponse, ResponseAnalysis analysis)
        => BuildResponseHtml(rawResponse, analysis, null);

    private static string BuildResponseHtml(string rawResponse, ResponseAnalysis analysis, string? viewerStateJson = null)
    {
        string summaryHtml = WrapParagraphs(analysis.Summary);
        string highConfidenceHtml = BuildBadgeList(analysis.HighConfidenceItems, "high");
        string lowConfidenceHtml = BuildBadgeList(analysis.LowConfidenceItems, "low");
        string recommendationsHtml = BuildBadgeList(analysis.Recommendations, "next");
        string warningsHtml = BuildWarningList(analysis.WarningNotes);
        string markdownHtml = ConvertMarkdownishToHtml(rawResponse);
        string responseInteractionScript = BuildResponseInteractionScript();
        string bootstrapViewerState = NormalizeViewerStateJson(viewerStateJson);

        string summaryCardHtml = BuildCopyableCard("Summary", summaryHtml, "content");
        string confidenceSnapshotCardHtml = BuildCopyableCard(
            "Confidence Snapshot",
            "High-confidence extracted items and lower-confidence observations are split automatically.",
            "content muted");
        string extractedDetailsCardHtml = BuildCopyableCard("Extracted Details", highConfidenceHtml, string.Empty);
        string lowConfidenceCardHtml = BuildCopyableCard("Low-Confidence / Unclear", lowConfidenceHtml, string.Empty);
        string recommendationsCardHtml = BuildCopyableCard("Suggested Next Step", recommendationsHtml, string.Empty);
        string warningsCardHtml = BuildCopyableCard("Confidence Notes", warningsHtml, string.Empty);

        return @"<!DOCTYPE html>
<html>
<head>
<meta charset=""utf-8"" />
<meta http-equiv=""X-UA-Compatible"" content=""IE=edge"" />
<meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
<style>
:root {
    --bg: #f6f8fb;
    --card: #ffffff;
    --text: #15202b;
    --muted: #5f6b7a;
    --line: #d9e2ec;
    --accent: #2563eb;
    --accent-soft: #e8f0ff;
    --good: #0f766e;
    --good-soft: #e7f8f5;
    --warn: #b45309;
    --warn-soft: #fff4e5;
    --danger: #9a3412;
    --danger-soft: #fff1ec;
    --code-bg: #0f172a;
    --code-text: #e2e8f0;
    --shadow: 0 10px 30px rgba(15, 23, 42, 0.07);
    --radius: 16px;
}
body[data-theme=""dark""] {
    --bg: #0b1220;
    --card: #111827;
    --text: #e5edf8;
    --muted: #9fb0c6;
    --line: #263244;
    --accent: #7aa2ff;
    --accent-soft: #172554;
    --good: #8ce2d2;
    --good-soft: #0f2f2c;
    --warn: #ffd28c;
    --warn-soft: #3c2b12;
    --danger: #ffb49a;
    --danger-soft: #3b1c16;
    --code-bg: #030712;
    --code-text: #dbe7f5;
    --shadow: 0 10px 30px rgba(2, 6, 23, 0.42);
}
* { box-sizing: border-box; }
html { scroll-behavior: smooth; }
body {
    margin: 0;
    min-height: 100vh;
    padding: 18px;
    background: var(--bg);
    color: var(--text);
    font-family: 'Segoe UI', Arial, sans-serif;
    line-height: 1.55;
}
body[data-density=""compact""] {
    font-size: 13px;
}
body[data-density=""compact""] .card,
body[data-density=""compact""] .detail,
body[data-density=""compact""] .response-block {
    border-radius: 12px;
}
.viewer-progress-track {
    position: fixed;
    top: 0;
    left: 0;
    right: 0;
    height: 3px;
    z-index: 1500;
    background: transparent;
}
.viewer-progress-bar {
    width: 0;
    height: 100%;
    background: linear-gradient(90deg, var(--accent), #7c3aed);
    box-shadow: 0 0 18px rgba(37, 99, 235, 0.35);
    transition: width 120ms ease-out;
}
.viewer-toast {
    position: fixed;
    right: 20px;
    bottom: 20px;
    max-width: min(420px, calc(100vw - 40px));
    padding: 12px 14px;
    border-radius: 12px;
    background: rgba(15, 23, 42, 0.92);
    color: #fff;
    box-shadow: var(--shadow);
    transform: translateY(10px);
    opacity: 0;
    pointer-events: none;
    z-index: 1400;
    transition: opacity 140ms ease, transform 140ms ease;
}
.viewer-toast.is-visible {
    opacity: 1;
    transform: translateY(0);
}
.viewer-toast.is-failure {
    background: rgba(153, 27, 27, 0.95);
}
.viewer-shell {
    display: grid;
    gap: 14px;
}
.viewer-toolbar {
    position: sticky;
    top: 12px;
    z-index: 1000;
    display: grid;
    gap: 12px;
    padding: 14px;
    background: color-mix(in srgb, var(--card) 92%, transparent);
    border: 1px solid var(--line);
    border-radius: var(--radius);
    box-shadow: var(--shadow);
    backdrop-filter: blur(18px);
}
.viewer-toolbar-main {
    display: grid;
    gap: 12px;
}
.viewer-brand {
    display: grid;
    gap: 4px;
}
.viewer-title {
    font-size: 18px;
    font-weight: 700;
    line-height: 1.2;
}
.viewer-subtitle {
    color: var(--muted);
    font-size: 13px;
}
.viewer-toolbar-actions {
    display: grid;
    gap: 10px;
}
.viewer-search-row {
    display: flex;
    align-items: center;
    gap: 8px;
    flex-wrap: wrap;
}
.viewer-search {
    display: flex;
    align-items: center;
    gap: 8px;
    min-width: min(380px, 100%);
    flex: 1 1 360px;
}
.viewer-search input,
.table-filter-input {
    width: 100%;
    min-width: 0;
    appearance: none;
    border: 1px solid var(--line);
    border-radius: 12px;
    background: var(--card);
    color: var(--text);
    padding: 10px 12px;
    font: inherit;
    outline: none;
    transition: border-color 120ms ease, box-shadow 120ms ease;
}
.viewer-search input:focus,
.table-filter-input:focus {
    border-color: var(--accent);
    box-shadow: 0 0 0 3px rgba(37, 99, 235, 0.16);
}
.toolbar-status {
    color: var(--muted);
    font-size: 12px;
    white-space: nowrap;
}
.viewer-button-row {
    display: flex;
    flex-wrap: wrap;
    gap: 8px;
}
.viewer-stats {
    display: flex;
    flex-wrap: wrap;
    gap: 8px;
}
.viewer-stat {
    display: inline-flex;
    align-items: center;
    gap: 8px;
    padding: 8px 10px;
    border: 1px solid var(--line);
    border-radius: 999px;
    background: color-mix(in srgb, var(--card) 85%, var,--bg);
    font-size: 12px;
}
.viewer-stat strong {
    font-weight: 700;
}
.viewer-layout {
   display: grid;
    grid-template-columns: 1fr;
    gap: 14px;
    align-items: start;
}
.viewer-outline {
    position: sticky;
    top: 148px;
    max-height: calc(100vh - 170px);
    overflow: auto;
    padding: 14px;
    background: var(--card);
    border: 1px solid var(--line);
    border-radius: var(--radius);
    box-shadow: var(--shadow);
    display: none;
}
.viewer-outline.is-open {
    display: block;
}
.outline-header {
    margin-bottom: 12px;
}
.outline-header h2 {
    margin: 0 0 6px 0;
    font-size: 16px;
}
.outline-header p {
    margin: 0;
    color: var(--muted);
    font-size: 12px;
}
.outline-group {
    display: grid;
    gap: 6px;
    margin-bottom: 12px;
}
.outline-group:last-child {
    margin-bottom: 0;
}
.outline-group-title {
    font-size: 11px;
    font-weight: 700;
    letter-spacing: 0.08em;
    text-transform: uppercase;
    color: var(--muted);
}
.outline-item {
    appearance: none;
    border: 1px solid transparent;
    background: transparent;
    color: var(--text);
    text-align: left;
    border-radius: 10px;
    padding: 8px 10px;
    cursor: pointer;
    font: inherit;
    transition: background 120ms ease, border-color 120ms ease, transform 120ms ease;
}
.outline-item:hover,
.outline-item:focus-visible {
    background: color-mix(in srgb, var(--accent-soft) 60%, var(--card));
    border-color: color-mix(in srgb, var(--accent) 28%, var,--line);
    outline: none;
}
.outline-item.level-2 { padding-left: 18px; }
.outline-item.level-3 { padding-left: 28px; }
.outline-empty {
    color: var(--muted);
    font-style: italic;
}
.viewer-main {
    min-width: 0;
}
.page {
    display: grid;
    grid-template-columns: 1fr;
    gap: 14px;
}
.grid {
    display: grid;
    grid-template-columns: repeat(2, minmax(280px, 1fr));
    gap: 14px;
}
.card,
.detail,
.response-block {
    background: var(--card);
    border: 1px solid var(--line);
    border-radius: var(--radius);
    box-shadow: var(--shadow);
}
.card,
.detail {
    padding: 16px;
}
.card h2,
.detail h2 {
    margin: 0;
    font-size: 17px;
    font-weight: 700;
}
.card p,
.detail p {
    margin: 0;
}
.card-header-row {
    display: flex;
    align-items: flex-start;
    justify-content: space-between;
    gap: 12px;
    margin: 0 0 12px 0;
}
.card-header-row h2 {
    margin: 0;
    flex: 1 1 auto;
}
.card-actions,
.response-block-actions {
    display: inline-flex;
    flex-wrap: wrap;
    gap: 8px;
    justify-content: flex-end;
    align-items: flex-start;
}
.card-copy-body > :first-child,
.response-block-body > :first-child,
.card-copy-body > .content > :first-child {
    margin-top: 0;
}
.card-copy-body > :last-child,
.response-block-body > :last-child,
.card-copy-body > .content > :last-child {
    margin-bottom: 0;
}
.muted { color: var(--muted); }
.pill-list {
    display: flex;
    flex-wrap: wrap;
    gap: 8px;
}
.pill,
.meta-chip {
    display: inline-flex;
    align-items: center;
    gap: 6px;
    padding: 7px 10px;
    border-radius: 999px;
    font-size: 12px;
    line-height: 1.35;
    border: 1px solid transparent;
}
.pill.high {
    background: var(--good-soft);
    color: var(--good);
    border-color: color-mix(in srgb, var(--good) 20%, var,--line);
}
.pill.low {
    background: var(--warn-soft);
    color: var(--warn);
    border-color: color-mix(in srgb, var(--warn) 26%, var,--line);
}
.pill.next,
.meta-chip-accent {
    background: var(--accent-soft);
    color: var(--accent);
    border-color: color-mix(in srgb, var(--accent) 28%, var,--line);
}
.warning-box {
    background: var(--danger-soft);
    color: var(--danger);
    border: 1px solid color-mix(in srgb, var(--danger) 28%, var,--line);
    border-radius: 12px;
    padding: 12px 14px;
}
.warning-box ul {
    margin: 0;
    padding-left: 18px;
}
.detail {
    padding: 18px;
}
.content h1 {
    margin: 0 0 12px 0;
    font-size: 26px;
    font-weight: 800;
    line-height: 1.2;
}
.content h2 {
    margin: 18px 0 10px 0;
    font-size: 21px;
    font-weight: 700;
    line-height: 1.3;
}
.content h3 {
    margin: 16px 0 8px 0;
    font-size: 17px;
    font-weight: 700;
    line-height: 1.3;
}
.content h4,
.content h5,
.content h6 {
    margin: 14px 0 8px 0;
    font-size: 15px;
    font-weight: 700;
    line-height: 1.3;
}
.content p,
.content blockquote,
.content pre,
.content .table-scroll,
.content ul,
.content ol {
    margin: 0 0 12px 0;
}
.content ul,
.content ol {
    padding-left: 22px;
}
.content li {
    margin: 0 0 6px 0;
}
.content strong { font-weight: 700; }
.content em { font-style: italic; }
.content a.response-link,
.content a[href] {
    color: var(--accent);
    text-decoration: none;
    border-bottom: 1px solid color-mix(in srgb, var(--accent) 45%, transparent);
}
.content a.response-link:hover,
.content a[href]:hover {
    border-bottom-color: currentColor;
}
.content code {
    background: color-mix(in srgb, var(--card) 55%, var(--bg));
    border: 1px solid var(--line);
    border-radius: 6px;
    padding: 1px 5px;
    font-family: Consolas, 'Cascadia Code', monospace;
    font-size: 12px;
}
.content pre {
    background: var(--code-bg);
    color: var(--code-text);
    padding: 14px;
    border-radius: 12px;
    overflow: auto;
    border: 1px solid color-mix(in srgb, var(--code-bg) 70%, var,--line);
    font-family: Consolas, 'Cascadia Code', monospace;
    font-size: 12px;
    white-space: pre;
}
.content pre.mermaid {
    background: color-mix(in srgb, var(--card) 90%, var(--bg));
    color: var(--text);
    border-color: var(--line);
    white-space: pre;
}
.content pre.mermaid.mermaid-source-error {
    background: var(--warn-soft);
    border-color: color-mix(in srgb, var(--warn) 35%, var,--line);
}
.content blockquote {
    padding: 10px 14px;
    border-left: 4px solid var(--accent);
    background: color-mix(in srgb, var(--accent-soft) 40%, var(--card));
    border-radius: 0 12px 12px 0;
}
.content hr {
    border: 0;
    border-top: 1px solid var(--line);
    margin: 16px 0;
}
.empty {
    color: var(--muted);
    font-style: italic;
}
.mermaid-status,
.mermaid-error {
    display: none;
    margin: 0 0 12px 0;
    padding: 10px 12px;
    border-radius: 10px;
    font-size: 12px;
}
.mermaid-status.visible {
    display: block;
    border: 1px solid color-mix(in srgb, var(--warn) 35%, var,--line);
    background: var(--warn-soft);
    color: var(--warn);
}
.mermaid-error {
    display: block;
    border: 1px solid color-mix(in srgb, var(--danger) 30%, var,--line);
    background: var(--danger-soft);
    color: var(--danger);
}
.mermaid-diagram {
    padding: 12px;
    border: 1px solid var(--line);
    border-radius: 12px;
    background: var(--card);
    overflow: auto;
}
.mermaid-diagram svg {
    display: block;
    max-width: 100%;
    height: auto;
    margin: 0 auto;
    transition: transform 120ms ease;
}
.content .table-scroll {
    overflow-x: auto;
    border: 1px solid var(--line);
    border-radius: 12px;
    background: color-mix(in srgb, var(--card) 94%, var(--bg));
}
.content table.response-table {
    width: 100%;
    min-width: max-content;
    border-collapse: separate;
    border-spacing: 0;
    font-size: 13px;
}
.content table.response-table th,
.content table.response-table td {
    padding: 10px 12px;
    border-right: 1px solid color-mix(in srgb, var(--line) 85%, transparent);
    border-bottom: 1px solid color-mix(in srgb, var(--line) 85%, transparent);
    vertical-align: top;
    text-align: left;
    white-space: normal;
    word-break: break-word;
}
.content table.response-table th:last-child,
.content table.response-table td:last-child { border-right: none; }
.content table.response-table thead th {
    position: sticky;
    top: 0;
    background: color-mix(in srgb, var(--card) 80%, var(--bg));
    color: var(--text);
    font-weight: 700;
    z-index: 1;
}
.content table.response-table thead th.table-sortable {
    cursor: pointer;
    user-select: none;
}
.content table.response-table thead th.table-sortable::after {
    content: '↕';
    margin-left: 8px;
    color: var(--muted);
    font-size: 11px;
}
.content table.response-table thead th[data-sort-direction=""asc""]::after { content: '↑'; }
.content table.response-table thead th[data-sort-direction=""desc""]::after { content: '↓'; }
.content table.response-table tbody tr:nth-child(even) td {
    background: color-mix(in srgb, var(--card) 96%, var(--bg));
}
.content table.response-table tbody tr:hover td {
    background: color-mix(in srgb, var(--accent-soft) 35%, var,--card);
}
.content table.response-table tbody tr:last-child td { border-bottom: none; }
.content table.response-table .table-align-left { text-align: left; }
.content table.response-table .table-align-center { text-align: center; }
.content table.response-table .table-align-right { text-align: right; }
.response-block {
    overflow: hidden;
}
.response-block-toolbar {
    display: flex;
    align-items: flex-start;
    justify-content: space-between;
    gap: 12px;
    padding: 14px 16px 0 16px;
}
.response-block-toolbar-left {
    display: grid;
    gap: 8px;
    min-width: 0;
}
.response-block-kind {
    font-size: 14px;
    font-weight: 700;
    letter-spacing: 0.01em;
}
.response-block-meta {
    display: flex;
    flex-wrap: wrap;
    gap: 8px;
}
.response-block-body {
    padding: 14px 16px 16px 16px;
    min-width: 0;
}
.response-block.is-collapsed .response-block-body,
.response-block.is-collapsed .response-source-panel,
.copyable-card.is-collapsed .card-copy-body,
.copyable-card.is-collapsed #mermaid-status {
    display: none;
}
.response-source-panel {
    margin: 0 16px 16px 16px;
    border: 1px solid var(--line);
    border-radius: 12px;
    overflow: auto;
    background: color-mix(in srgb, var(--card) 94%, var,--bg);
}
.response-source-panel pre {
    margin: 0;
    white-space: pre-wrap;
    background: transparent;
    color: var(--text);
    border: 0;
    border-radius: 0;
    box-shadow: none;
}
.table-tools {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 8px;
    padding: 0 16px;
}
.table-filter-count {
    color: var(--muted);
    font-size: 12px;
    white-space: nowrap;
}
.response-action-button {
    appearance: none;
    border: 1px solid var(--line);
    border-radius: 999px;
    background: color-mix(in srgb, var(--card) 92%, var,--bg);
    color: var(--text);
    padding: 5px 9px;
    font: 600 11px/1 'Segoe UI', Arial, sans-serif;
    cursor: pointer;
    transition: background 120ms ease, border-color 120ms ease, color 120ms ease, box-shadow 120ms ease, transform 120ms ease;
}
.response-action-button:hover {
    background: color-mix(in srgb, var(--accent-soft) 38%, var(--card));
    border-color: color-mix(in srgb, var(--accent) 26%, var,--line);
    transform: translateY(-1px);
}
.response-action-button:focus-visible {
    outline: none;
    box-shadow: 0 0 0 3px rgba(37, 99, 235, 0.16);
    border-color: var(--accent);
}
.response-action-button.is-active {
    background: var(--accent-soft);
    border-color: color-mix(in srgb, var(--accent) 32%, var,--line);
    color: var(--accent);
}
.response-action-button.is-success {
    background: color-mix(in srgb, var(--good-soft) 85%, var,--card);
    border-color: color-mix(in srgb, var(--good) 26%, var,--line);
    color: var(--good);
}
.response-action-button.is-failure {
    background: color-mix(in srgb, var(--danger-soft) 88%, var,--card);
    border-color: color-mix(in srgb, var(--danger) 32%, var,--line);
    color: var(--danger);
}
.response-action-button.is-image {
    background: color-mix(in srgb, var(--accent-soft) 72%, var,--card);
    border-color: color-mix(in srgb, var(--accent) 30%, var,--line);
    color: var(--accent);
}
.capture-image-mode .card-actions,
.capture-image-mode .response-block-actions,
.capture-image-mode .viewer-toolbar,
.capture-image-mode .viewer-outline,
.capture-image-mode .response-source-panel {
    display: none !important;
}
.search-hit {
    background: #fff3a3;
    color: #111827;
    border-radius: 4px;
    padding: 0 1px;
    box-shadow: inset 0 -1px 0 rgba(0, 0, 0, 0.08);
}
.search-hit.is-active {
    background: #fbbf24;
}
body[data-theme=""dark""] .search-hit {
    background: #854d0e;
    color: #fff7ed;
}
body[data-theme=""dark""] .search-hit.is-active {
    background: #ca8a04;
}
.code-line {
    display: grid;
    grid-template-columns: auto minmax(0, 1fr);
    gap: 12px;
}
.code-line-number {
    min-width: 2.4em;
    text-align: right;
    color: rgba(148, 163, 184, 0.9);
    user-select: none;
}
.code-line-content {
    min-width: 0;
    white-space: pre;
}
body[data-wrap-code=""1""] .code-line-content {
    white-space: pre-wrap;
    word-break: break-word;
}
 
@media (max-width: 1150px) {
    .viewer-layout {
        grid-template-columns: 1fr;
    }
    .viewer-outline {
        position: static;
        max-height: none;
    }
}
@media (max-width: 900px) {
    body {
        padding: 12px;
    }
    .grid {
        grid-template-columns: 1fr;
    }
    .viewer-toolbar {
        top: 8px;
    }
    .viewer-search {
        min-width: 0;
        flex-basis: 100%;
    }
}

</style>
</head>
<body data-theme=""light"" data-density=""comfortable"" data-wrap-code=""0"">
<div class=""viewer-progress-track""><div id=""viewer-progress-bar"" class=""viewer-progress-bar""></div></div>
<div id=""viewer-toast"" class=""viewer-toast"" aria-live=""polite"" aria-atomic=""true""></div>
<script>window.__AIQ_BOOTSTRAP = { viewerState: " + bootstrapViewerState + @" };</script>
<div class=""viewer-shell"">
    <header class=""viewer-toolbar"">
        <div class=""viewer-toolbar-main"">
            <div class=""viewer-brand"">
                <div class=""viewer-title"">Response Workspace</div>
                <div class=""viewer-subtitle"">Copy, focus, collapse, inspect source, and tune readability.</div>
            </div>
            <div class=""viewer-toolbar-actions"">
                <!--div class=""viewer-search-row"">
                    <div class=""viewer-search"">
                        <input id=""viewer-search"" type=""search"" placeholder=""Search this response (/)"" spellcheck=""false"" />
                        <span id=""viewer-search-count"" class=""toolbar-status"">0 results</span>
                    </div>
                    <button id=""viewer-search-prev"" type=""button"" class=""response-action-button"" title=""Previous result (Shift+F3)"">Previous</button>
                    <button id=""viewer-search-next"" type=""button"" class=""response-action-button"" title=""Next result (F3)"">Next</button>
                </div--!>
                <div class=""viewer-button-row"">
                    <button id=""viewer-copy-page"" type=""button"" class=""response-action-button"">Copy Page</button>
                    <!--button id=""viewer-toggle-outline"" type=""button"" class=""response-action-button"">Outline</button-->
                    <button id=""viewer-toggle-theme"" type=""button"" class=""response-action-button"">Light</button>
                    <button id=""viewer-toggle-density"" type=""button"" class=""response-action-button"">Comfortable</button>
                    <!--button id=""viewer-toggle-wrap"" type=""button"" class=""response-action-button"">Wrap</button-->
                    <button id=""viewer-toggle-sources"" type=""button"" class=""response-action-button"">Sources</button>
                    <button id=""viewer-collapse-all"" type=""button"" class=""response-action-button"">Collapse All</button>
                    <button id=""viewer-expand-all"" type=""button"" class=""response-action-button"">Expand All</button>
                    <!--button id=""viewer-exit-focus"" type=""button"" class=""response-action-button"" hidden>Exit Focus</button--> <div id=""viewer-stats"" class=""viewer-stats"" aria-live=""polite""></div>
                </div>
            </div>
        </div>
       
    </header>

    <div class=""viewer-layout"">
        <aside id=""viewer-outline"" class=""viewer-outline"" aria-label=""Response outline"">
            <div class=""outline-header"">
                <h2>Outline</h2>
                <p>Jump to cards, headings, tables, diagrams, and code blocks.</p>
            </div>
            <div id=""viewer-outline-content""></div>
        </aside>

        <main class=""viewer-main"">
            <div class=""page"" id=""viewer-page"">
                <section class=""grid"">
                    " + summaryCardHtml + @"

                    " + confidenceSnapshotCardHtml + @"

                    " + extractedDetailsCardHtml + @"

                    " + lowConfidenceCardHtml + @"
                </section>

                <section class=""grid"">
                    " + recommendationsCardHtml + @"

                    " + warningsCardHtml + @"
                </section>

                <section class=""detail copyable-card"" data-copy-title=""Full Response"">
                    <div class=""card-header-row"">
                        <h2>Full Response</h2>
                        <div class=""card-actions"">
                            <button type=""button"" class=""response-action-button card-copy-button"">Copy</button>
                            <button type=""button"" class=""response-action-button card-copy-image-button is-image"">Copy Image</button>
                            <div style=""position:relative; display:inline-block;"" class=""card-share-container"">
                                <button type=""button"" class=""response-action-button card-share-dropdown-button"">Share ▼</button>
                                <div class=""card-share-menu"" style=""display:none; position:absolute; right:0; top:100%; margin-top:4px; background:var(--card); border:1px solid var(--line); border-radius:8px; box-shadow:var(--shadow); z-index:100; min-width:120px; overflow:hidden;"">
                                    <button type=""button"" class=""response-action-button card-share-action"" data-target=""email"" style=""display:block; width:100%; text-align:left; border:none; border-radius:0; border-bottom:1px solid var(--line);"">Email</button>
                                    <button type=""button"" class=""response-action-button card-share-action"" data-target=""teams"" style=""display:block; width:100%; text-align:left; border:none; border-radius:0;"">Teams</button>
                                </div>
                            </div>
                        </div>
                    </div>
                    <div id=""mermaid-status"" class=""mermaid-status""></div>
                    <div id=""response-content"" class=""content card-copy-body"">
                        " + markdownHtml + @"
                    </div>
                </section>
            </div>
        </main>
    </div>
</div>
" + responseInteractionScript + @"
</body>
</html>";
    }

    private static string NormalizeViewerStateJson(string? viewerStateJson)
    {
        if (string.IsNullOrWhiteSpace(viewerStateJson))
            return "{}";

        try
        {
            using JsonDocument document = JsonDocument.Parse(viewerStateJson);
            return document.RootElement.ValueKind == JsonValueKind.Object
                ? document.RootElement.GetRawText()
                : "{}";
        }
        catch
        {
            return "{}";
        }
    }


    private static string BuildEmptyResponseHtml()
    {
        return @"<!DOCTYPE html>
<html>
<head>
<meta charset=""utf-8"" />
<style>
body {
    margin: 0;
    padding: 24px;
    font-family: 'Segoe UI', Arial, sans-serif;
    background: #f6f8fb;
    color: #334155;
}
.box {
    border: 1px solid #d9e2ec;
    border-radius: 16px;
    background: #ffffff;
    padding: 20px;
    box-shadow: 0 10px 30px rgba(15, 23, 42, 0.06);
}
h1 {
    margin: 0 0 10px 0;
    font-size: 20px;
}
p {
    margin: 0;
    color: #64748b;
    line-height: 1.55;
}
code {
    background: #eef2f7;
    border: 1px solid #d7e0ea;
    border-radius: 6px;
    padding: 1px 5px;
    font-family: Consolas, monospace;
}
</style>
</head>
<body>
    <div class=""box"">
        <h1>No response yet</h1>
        <p>Submit an image question to render the formatted response here. The viewer now supports search, outline navigation, source toggles, focus mode, table filtering, code line numbers, diagram zoom, and smarter copy actions as soon as a response is available.</p>
    </div>
</body>
</html>";
    }


    private static string BuildCopyableCard(string title, string bodyHtml, string bodyCssClass)
    {
        string encodedTitle = WebUtility.HtmlEncode(title ?? string.Empty);
        string resolvedBodyCssClass = "card-copy-body";
        if (!string.IsNullOrWhiteSpace(bodyCssClass))
            resolvedBodyCssClass += " " + bodyCssClass.Trim();

        return "<div class=\"card copyable-card\" data-copy-title=\"" + encodedTitle + "\">"
            + "<div class=\"card-header-row\">"
            + "<h2>" + encodedTitle + "</h2>"
            + "<div class=\"card-actions\">"
            + "<button type=\"button\" class=\"response-action-button card-copy-button\">Copy</button>"
            + "<button type=\"button\" class=\"response-action-button card-copy-image-button is-image\">Copy Image</button>"
            + "</div>"
            + "</div>"
            + "<div class=\"" + resolvedBodyCssClass + "\">" + (bodyHtml ?? string.Empty) + "</div>"
            + "</div>";
    }

    private static string BuildBadgeList(IEnumerable<string> items, string cssClass)
    {
        List<string> list = items?.Where(x => !string.IsNullOrWhiteSpace(x)).ToList() ?? new List<string>();
        if (list.Count == 0)
            return "<div class=\"empty\">None detected.</div>";

        StringBuilder sb = new();
        sb.Append("<div class=\"pill-list\">");

        foreach (string item in list)
            sb.Append("<span class=\"pill " + cssClass + "\">" + FormatInline(item) + "</span>");

        sb.Append("</div>");
        return sb.ToString();
    }

    private static string BuildWarningList(IEnumerable<string> items)
    {
        List<string> list = items?.Where(x => !string.IsNullOrWhiteSpace(x)).ToList() ?? new List<string>();
        if (list.Count == 0)
            return "<div class=\"empty\">No special confidence notes.</div>";

        StringBuilder sb = new();
        sb.Append("<div class=\"warning-box\"><ul>");

        foreach (string item in list)
            sb.Append("<li>" + FormatInline(item) + "</li>");

        sb.Append("</ul></div>");
        return sb.ToString();
    }

    private static string WrapParagraphs(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "<div class=\"empty\">No summary available.</div>";

        return "<p>" + FormatInline(text.Trim()) + "</p>";
    }


    private static string ConvertMarkdownishToHtml(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return "<div class=\"empty\">No response text available.</div>";

        string normalized = rawText.Replace("\r\n", "\n");
        string[] lines = normalized.Split('\n');

        StringBuilder sb = new();
        List<string> paragraphBuffer = new();
        List<string> codeBuffer = new();

        bool inCodeBlock = false;
        bool inUnorderedList = false;
        bool inOrderedList = false;
        string currentCodeFenceLanguage = null;

        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index] ?? string.Empty;
            string trimmed = line.Trim();

            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                FlushParagraph();
                FlushLists();

                if (!inCodeBlock)
                {
                    inCodeBlock = true;
                    currentCodeFenceLanguage = GetCodeFenceLanguage(trimmed);
                    codeBuffer.Clear();
                }
                else
                {
                    AppendCodeBlockHtml(sb, codeBuffer, currentCodeFenceLanguage);
                    inCodeBlock = false;
                    currentCodeFenceLanguage = null;
                    codeBuffer.Clear();
                }

                continue;
            }

            if (inCodeBlock)
            {
                codeBuffer.Add(line);
                continue;
            }

            if (string.IsNullOrWhiteSpace(trimmed))
            {
                FlushParagraph();
                FlushLists();
                continue;
            }

            if (TryBuildMarkdownTableHtml(lines, ref index, out string tableHtml))
            {
                FlushParagraph();
                FlushLists();
                sb.Append(tableHtml);
                continue;
            }

            Match headingMatch = HeadingRegex.Match(trimmed);
            if (headingMatch.Success)
            {
                FlushParagraph();
                FlushLists();

                int level = Math.Min(6, headingMatch.Groups[1].Value.Length);
                string headingText = headingMatch.Groups[2].Value.Trim();

                sb.Append("<h" + level + ">" + FormatInline(headingText) + "</h" + level + ">");
                continue;
            }

            if (UnorderedBulletRegex.IsMatch(trimmed))
            {
                FlushParagraph();

                if (inOrderedList)
                {
                    sb.Append("</ol>");
                    inOrderedList = false;
                }

                if (!inUnorderedList)
                {
                    sb.Append("<ul>");
                    inUnorderedList = true;
                }

                string item = UnorderedBulletRegex.Replace(trimmed, string.Empty);
                sb.Append("<li>" + FormatInline(item) + "</li>");
                continue;
            }

            if (OrderedBulletRegex.IsMatch(trimmed))
            {
                FlushParagraph();

                if (inUnorderedList)
                {
                    sb.Append("</ul>");
                    inUnorderedList = false;
                }

                if (!inOrderedList)
                {
                    sb.Append("<ol>");
                    inOrderedList = true;
                }

                string item = OrderedBulletRegex.Replace(trimmed, string.Empty);
                sb.Append("<li>" + FormatInline(item) + "</li>");
                continue;
            }

            FlushLists();
            paragraphBuffer.Add(trimmed);
        }

        FlushParagraph();
        FlushLists();

        if (inCodeBlock && codeBuffer.Count > 0)
            AppendCodeBlockHtml(sb, codeBuffer, currentCodeFenceLanguage);

        return sb.ToString();

        void FlushParagraph()
        {
            if (paragraphBuffer.Count == 0)
                return;

            string paragraph = string.Join(" ", paragraphBuffer).Trim();
            sb.Append("<p>" + FormatInline(paragraph) + "</p>");
            paragraphBuffer.Clear();
        }

        void FlushLists()
        {
            if (inUnorderedList)
            {
                sb.Append("</ul>");
                inUnorderedList = false;
            }

            if (inOrderedList)
            {
                sb.Append("</ol>");
                inOrderedList = false;
            }
        }
    }

    private static bool TryBuildMarkdownTableHtml(string[] lines, ref int index, out string html)
    {
        html = string.Empty;

        if (lines == null || index < 0 || index >= lines.Length)
            return false;

        string currentLine = lines[index] ?? string.Empty;

        // Headerless / malformed GPT table:
        // |---|---|
        // | key | value |
        if (TryParseMarkdownTableDelimiter(currentLine, out List<string> headerlessAlignments))
        {
            int expectedColumnCount = headerlessAlignments.Count;
            List<IReadOnlyList<string>> headerlessRows = new();
            int lastConsumedIndex = index;

            for (int rowIndex = index + 1; rowIndex < lines.Length; rowIndex++)
            {
                string rowLine = lines[rowIndex] ?? string.Empty;
                if (!LooksLikeMarkdownTableRow(rowLine, expectedColumnCount))
                    break;

                if (TryParseMarkdownTableDelimiter(rowLine, out _))
                    break;

                List<string> rowCells = ParseMarkdownTableCells(rowLine);
                if (rowCells.Count == 0)
                    break;

                headerlessRows.Add(NormalizeMarkdownTableCells(rowCells, expectedColumnCount));
                lastConsumedIndex = rowIndex;
            }

            if (headerlessRows.Count > 0)
            {
                html = BuildMarkdownTableHtml(null, headerlessAlignments, headerlessRows);
                index = lastConsumedIndex;
                return true;
            }

            return false;
        }

        // Normal markdown table:
        // | Header A | Header B |
        // |----------|----------|
        // | value    | value    |
        if (index + 1 >= lines.Length)
            return false;

        string headerLine = lines[index] ?? string.Empty;
        string delimiterLine = lines[index + 1] ?? string.Empty;

        if (!LooksLikeMarkdownTableHeader(headerLine))
            return false;

        List<string> headers = ParseMarkdownTableCells(headerLine);
        if (headers.Count == 0)
            return false;

        if (!TryParseMarkdownTableDelimiter(delimiterLine, out List<string> alignments))
            return false;

        if (alignments.Count != headers.Count)
            return false;

        List<IReadOnlyList<string>> rows = new();
        int lastConsumedNormalIndex = index + 1;

        for (int rowIndex = index + 2; rowIndex < lines.Length; rowIndex++)
        {
            string rowLine = lines[rowIndex] ?? string.Empty;
            if (!LooksLikeMarkdownTableRow(rowLine, headers.Count))
                break;

            if (TryParseMarkdownTableDelimiter(rowLine, out _))
                break;

            List<string> rowCells = ParseMarkdownTableCells(rowLine);
            if (rowCells.Count == 0)
                break;

            rows.Add(NormalizeMarkdownTableCells(rowCells, headers.Count));
            lastConsumedNormalIndex = rowIndex;
        }

        html = BuildMarkdownTableHtml(headers, alignments, rows);
        index = lastConsumedNormalIndex;
        return true;
    }

    private static bool LooksLikeMarkdownTableHeader(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || !line.Contains("|"))
            return false;

        string trimmed = line.Trim();
        if (trimmed.StartsWith(">", StringComparison.Ordinal))
            return false;

        if (TryParseMarkdownTableDelimiter(trimmed, out _))
            return false;

        List<string> cells = ParseMarkdownTableCells(trimmed);
        return cells.Count >= 2 && cells.Any(cell => !string.IsNullOrWhiteSpace(cell));
    }

    private static bool TryParseMarkdownTableDelimiter(string line, out List<string> alignments)
    {
        alignments = new List<string>();

        if (string.IsNullOrWhiteSpace(line) || !line.Contains("|"))
            return false;

        List<string> cells = ParseMarkdownTableCells(line);
        if (cells.Count == 0)
            return false;

        foreach (string cell in cells)
        {
            string token = cell.Trim();
            if (!TableSeparatorRegex.IsMatch(token))
                return false;

            if (token.StartsWith(":", StringComparison.Ordinal) && token.EndsWith(":", StringComparison.Ordinal))
                alignments.Add("center");
            else if (token.EndsWith(":", StringComparison.Ordinal))
                alignments.Add("right");
            else
                alignments.Add("left");
        }

        return true;
    }

    private static bool LooksLikeMarkdownTableRow(string line, int expectedColumnCount)
    {
        if (string.IsNullOrWhiteSpace(line) || !line.Contains("|"))
            return false;

        string trimmed = line.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
            return false;

        List<string> cells = ParseMarkdownTableCells(trimmed);
        return cells.Count >= 2 && cells.Count <= expectedColumnCount + 1;
    }

    private static List<string> ParseMarkdownTableCells(string line)
    {
        List<string> cells = new();
        if (string.IsNullOrWhiteSpace(line))
            return cells;

        string working = line.Trim();
        if (working.StartsWith("|", StringComparison.Ordinal))
            working = working[1..];

        if (working.EndsWith("|", StringComparison.Ordinal))
            working = working[..^1];

        StringBuilder cell = new();
        bool isEscaped = false;
        bool inCodeSpan = false;

        foreach (char ch in working)
        {
            if (isEscaped)
            {
                cell.Append(ch);
                isEscaped = false;
                continue;
            }

            if (ch == '\\')
            {
                isEscaped = true;
                continue;
            }

            if (ch == '`')
            {
                inCodeSpan = !inCodeSpan;
                cell.Append(ch);
                continue;
            }

            if (ch == '|' && !inCodeSpan)
            {
                cells.Add(cell.ToString().Trim());
                cell.Clear();
                continue;
            }

            cell.Append(ch);
        }

        if (isEscaped)
            cell.Append('\\');

        cells.Add(cell.ToString().Trim());
        return cells;
    }

    private static IReadOnlyList<string> NormalizeMarkdownTableCells(IReadOnlyList<string> cells, int targetCount)
    {
        List<string> normalized = cells.ToList();

        if (normalized.Count > targetCount)
        {
            string overflow = string.Join(" | ", normalized.Skip(targetCount - 1));
            normalized = normalized.Take(targetCount - 1).Concat(new[] { overflow }).ToList();
        }

        while (normalized.Count < targetCount)
            normalized.Add(string.Empty);

        return normalized;
    }

    private static string BuildMarkdownTableHtml(
        IReadOnlyList<string> headers,
        IReadOnlyList<string> alignments,
        IReadOnlyList<IReadOnlyList<string>> rows)
    {
        StringBuilder sb = new();
        sb.Append("<div class=\"table-scroll\"><table class=\"response-table\">");

        bool hasHeaders = headers != null && headers.Count > 0;
        int columnCount = hasHeaders
            ? headers.Count
            : (rows.Count > 0 ? rows[0].Count : alignments.Count);

        if (columnCount <= 0)
            return string.Empty;

        if (hasHeaders)
        {
            sb.Append("<thead><tr>");

            for (int columnIndex = 0; columnIndex < columnCount; columnIndex++)
            {
                string alignmentClass = ResolveMarkdownTableAlignmentClass(alignments, columnIndex);
                sb.Append("<th class=\"");
                sb.Append(alignmentClass);
                sb.Append("\">");
                sb.Append(FormatInline(headers[columnIndex]));
                sb.Append("</th>");
            }

            sb.Append("</tr></thead>");
        }

        if (rows.Count > 0)
        {
            sb.Append("<tbody>");
            foreach (IReadOnlyList<string> row in rows)
            {
                sb.Append("<tr>");
                for (int columnIndex = 0; columnIndex < columnCount; columnIndex++)
                {
                    string alignmentClass = ResolveMarkdownTableAlignmentClass(alignments, columnIndex);
                    sb.Append("<td class=\"");
                    sb.Append(alignmentClass);
                    sb.Append("\">");
                    sb.Append(FormatInline(columnIndex < row.Count ? row[columnIndex] : string.Empty));
                    sb.Append("</td>");
                }

                sb.Append("</tr>");
            }

            sb.Append("</tbody>");
        }

        sb.Append("</table></div>");
        return sb.ToString();
    }

    private static string ResolveMarkdownTableAlignmentClass(IReadOnlyList<string> alignments, int columnIndex)
    {
        string value = columnIndex >= 0 && columnIndex < alignments.Count
            ? alignments[columnIndex]
            : "left";

        return value switch
        {
            "center" => "table-align-center",
            "right" => "table-align-right",
            _ => "table-align-left"
        };
    }



    private static string BuildResponseInteractionScript()
    {
        return @"<script type=""module"">
window.addEventListener('load', async () => {
    const body = document.body;
    const responseContent = document.getElementById('response-content');
    const page = document.getElementById('viewer-page');
    if (!responseContent || !page)
        return;

    const outline = document.getElementById('viewer-outline');
    const outlineContent = document.getElementById('viewer-outline-content');
    const status = document.getElementById('mermaid-status');
    const toast = document.getElementById('viewer-toast');
    const statsBar = document.getElementById('viewer-stats');
    const progressBar = document.getElementById('viewer-progress-bar');
    const searchInput = document.getElementById('viewer-search');
    const searchCount = document.getElementById('viewer-search-count');
    const btnSearchPrev = document.getElementById('viewer-search-prev');
    const btnSearchNext = document.getElementById('viewer-search-next');
    const btnCopyPage = document.getElementById('viewer-copy-page');
    const btnToggleOutline = document.getElementById('viewer-toggle-outline');
    const btnToggleTheme = document.getElementById('viewer-toggle-theme');
    const btnToggleDensity = document.getElementById('viewer-toggle-density');
    const btnToggleWrap = document.getElementById('viewer-toggle-wrap');
    const btnToggleSources = document.getElementById('viewer-toggle-sources');
    const btnCollapseAll = document.getElementById('viewer-collapse-all');
    const btnExpandAll = document.getElementById('viewer-expand-all');
    const btnExitFocus = document.getElementById('viewer-exit-focus');

    const hostBridge = window.chrome && window.chrome.webview && typeof window.chrome.webview.postMessage === 'function'
        ? window.chrome.webview
        : null;

    const boot = window.__AIQ_BOOTSTRAP && typeof window.__AIQ_BOOTSTRAP === 'object'
        ? window.__AIQ_BOOTSTRAP
        : {};

    const viewerState = Object.assign({
        theme: 'light',
        density: 'comfortable',
        wrapCode: false,
        outlineOpen: false,
        showSources: false
    }, boot.viewerState && typeof boot.viewerState === 'object' ? boot.viewerState : {});

    let mermaidApi = null;
    let requestSequence = 0;
    let persistTimer = 0;
    let focusedWrapper = null;
    let searchHits = [];
    let activeSearchIndex = -1;
    const pendingHostRequests = new Map();
    const runWhenIdle = typeof window.requestIdleCallback === 'function'
        ? (callback) => window.requestIdleCallback(callback, { timeout: 450 })
        : (callback) => window.setTimeout(() => callback({ didTimeout: false, timeRemaining: () => 0 }), 32);

    const escapeHtml = (value) => {
        const span = document.createElement('span');
        span.textContent = value || '';
        return span.innerHTML;
    };

    const debounce = (action, wait) => {
        let timer = 0;
        return (...args) => {
            window.clearTimeout(timer);
            timer = window.setTimeout(() => action(...args), wait);
        };
    };

    const hashText = (value) => {
        let hash = 0;
        const input = String(value || '');
        for (let index = 0; index < input.length; index++)
            hash = ((hash << 5) - hash) + input.charCodeAt(index);
        return Math.abs(hash).toString(36);
    };

    const showToast = (message, isFailure = false) => {
        if (!toast)
            return;

        toast.textContent = message || '';
        toast.classList.toggle('is-visible', !!message);
        toast.classList.toggle('is-failure', !!isFailure);

        if (!message)
            return;

        window.clearTimeout(showToast._timerId || 0);
        showToast._timerId = window.setTimeout(() => {
            toast.classList.remove('is-visible', 'is-failure');
        }, 1800);
    };

    const pulseButton = (button, message, isFailure = false) => {
        if (!button)
            return;

        const originalText = button.dataset.originalText || button.textContent || '';
        button.dataset.originalText = originalText;
        button.textContent = message;
        button.classList.remove('is-success', 'is-failure');
        button.classList.add(isFailure ? 'is-failure' : 'is-success');

        window.setTimeout(() => {
            button.textContent = originalText;
            button.classList.remove('is-success', 'is-failure');
        }, 1400);
    };

    const createActionButton = (text, cssClass, title) => {
        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'response-action-button' + (cssClass ? ' ' + cssClass : '');
        button.textContent = text;
        button.dataset.originalText = text;
        if (title)
            button.title = title;
        return button;
    };

    const safePostHostMessage = (payload) => {
        if (!hostBridge)
            return false;

        try {
            hostBridge.postMessage(JSON.stringify(payload));
            return true;
        }
        catch {
            return false;
        }
    };

    const schedulePersistViewerState = () => {
        window.clearTimeout(persistTimer);
        persistTimer = window.setTimeout(() => {
            safePostHostMessage({ type: 'persist-viewer-state', state: viewerState });
        }, 180);
    };

    const fallbackCopyText = async (text) => {
        const value = String(text || '').trim();
        if (!value)
            throw new Error('Nothing to copy.');

        try {
            if (navigator.clipboard && typeof navigator.clipboard.writeText === 'function') {
                await navigator.clipboard.writeText(value);
                return;
            }
        }
        catch {
            // fall through to execCommand
        }

        const textarea = document.createElement('textarea');
        textarea.value = value;
        textarea.setAttribute('readonly', 'readonly');
        textarea.style.position = 'fixed';
        textarea.style.opacity = '0';
        textarea.style.pointerEvents = 'none';
        document.body.appendChild(textarea);
        textarea.focus();
        textarea.select();
        const copied = typeof document.execCommand === 'function' && document.execCommand('copy');
        textarea.remove();
        if (!copied)
            throw new Error('Clipboard copy is unavailable.');
    };

    const fallbackCopyRich = async (payload) => {
        const text = payload && payload.text ? payload.text : '';
        const html = payload && payload.html ? payload.html : '';

        try {
            if (navigator.clipboard && window.ClipboardItem && html) {
                const items = {
                    'text/plain': new Blob([text || ''], { type: 'text/plain' }),
                    'text/html': new Blob([html], { type: 'text/html' })
                };
                await navigator.clipboard.write([new ClipboardItem(items)]);
                return;
            }
        }
        catch {
            // fall back to text only
        }

        await fallbackCopyText(text || html.replace(/<[^>]+>/g, ' '));
    };

    const fallbackCopyImage = async (dataUrl) => {
        if (!dataUrl)
            throw new Error('Image data is unavailable.');

        if (navigator.clipboard && window.ClipboardItem && typeof fetch === 'function') {
            const response = await fetch(dataUrl);
            const blob = await response.blob();
            await navigator.clipboard.write([new ClipboardItem({ [blob.type || 'image/png']: blob })]);
            return;
        }

        throw new Error('Copy image requires the host bridge in this view.');
    };

    const fallbackHostAction = async (payload) => {
        if (!payload || !payload.type)
            throw new Error('Unsupported action.');

        switch (payload.type) {
            case 'copy-text':
                await fallbackCopyText(payload.text || '');
                return { success: true };
            case 'copy-rich':
                await fallbackCopyRich(payload);
                return { success: true };
            case 'copy-image':
                await fallbackCopyImage(payload.dataUrl || '');
                return { success: true };
            default:
                throw new Error('Action requires the host bridge.');
        }
    };

    const onHostMessage = (event) => {
        let payload = event ? event.data : null;
        if (typeof payload === 'string') {
            try {
                payload = JSON.parse(payload);
            }
            catch {
                return;
            }
        }

        if (!payload || payload.type !== 'copy-result' || !payload.requestId)
            return;

        const pending = pendingHostRequests.get(payload.requestId);
        if (!pending)
            return;

        pendingHostRequests.delete(payload.requestId);
        window.clearTimeout(pending.timerId);

        if (payload.success)
            pending.resolve(payload);
        else
            pending.reject(new Error(payload.message || 'Copy failed.'));
    };

    if (hostBridge && typeof hostBridge.addEventListener === 'function')
        hostBridge.addEventListener('message', onHostMessage);

    const requestHostAction = (payload) => {
        if (!hostBridge)
            return fallbackHostAction(payload);

        return new Promise((resolve, reject) => {
            const requestId = 'copy-' + Date.now() + '-' + (++requestSequence);
            const timerId = window.setTimeout(() => {
                pendingHostRequests.delete(requestId);
                reject(new Error('Copy timed out.'));
            }, 6000);

            pendingHostRequests.set(requestId, { resolve, reject, timerId });

            try {
                hostBridge.postMessage(JSON.stringify({ ...payload, requestId }));
            }
            catch (error) {
                window.clearTimeout(timerId);
                pendingHostRequests.delete(requestId);
                reject(error instanceof Error ? error : new Error(String(error)));
            }
        });
    };

    const humanizeKind = (kind) => {
        switch (kind) {
            case 'text': return 'Narrative';
            case 'code': return 'Code';
            case 'table': return 'Table';
            case 'mermaid': return 'Diagram';
            default: return 'Block';
        }
    };

    const getElementClipBounds = (element, padding = 0) => {
        if (!(element instanceof HTMLElement))
            throw new Error('Target element is unavailable.');

        const rect = element.getBoundingClientRect();
        const safePadding = Math.max(0, padding || 0);
        return {
            x: Math.max(0, rect.left + window.scrollX - safePadding),
            y: Math.max(0, rect.top + window.scrollY - safePadding),
            width: Math.max(1, Math.ceil(rect.width + safePadding * 2)),
            height: Math.max(1, Math.ceil(rect.height + safePadding * 2)),
            scale: Math.max(1, window.devicePixelRatio || 1)
        };
    };

    const withTemporaryClass = async (element, className, action) => {
        if (!(element instanceof HTMLElement))
            return await action();

        element.classList.add(className);
        try {
            await new Promise(resolve => window.requestAnimationFrame(resolve));
            return await action();
        }
        finally {
            element.classList.remove(className);
        }
    };

    const ensureCardBody = (card) => card.querySelector('.card-copy-body');

    const getCardCopyPayload = (card) => {
        const title = (card.dataset.copyTitle || '').trim();
        const bodyElement = ensureCardBody(card);
        const parts = [];
        if (title)
            parts.push(title);
        if (bodyElement) {
            const text = (bodyElement.innerText || bodyElement.textContent || '').trim();
            if (text)
                parts.push(text);
        }

        let html = '<div class=""copied-card"">';
        if (title)
            html += '<h2>' + escapeHtml(title) + '</h2>';
        if (bodyElement)
            html += bodyElement.outerHTML;
        html += '</div>';

        return { text: parts.join('\n\n').trim(), html };
    };

    const getPlainCopyText = (wrapper) => {
        const explicit = wrapper.dataset.copyText;
        if (typeof explicit === 'string' && explicit.trim())
            return explicit.trim();

        const bodyElement = wrapper.querySelector(':scope > .response-block-body');
        return bodyElement ? (bodyElement.innerText || bodyElement.textContent || '').trim() : '';
    };

    const getRichCopyPayload = (wrapper) => {
        const bodyElement = wrapper.querySelector(':scope > .response-block-body');
        return {
            text: bodyElement ? (bodyElement.innerText || bodyElement.textContent || '').trim() : '',
            html: bodyElement ? (bodyElement.innerHTML || '') : ''
        };
    };

    const escapeCsvCell = (value) => {
        const text = String(value || '').replace(/\r?\n/g, ' ').trim();
        if (!/["",\n]/.test(text))
            return text;
        return '""' + text.replace(/""/g, '""""') + '""';
    };

    const tableToDelimitedText = (table, delimiter = '\t') => {
        if (!(table instanceof HTMLTableElement))
            return '';

        return Array.from(table.rows)
            .map((row) => Array.from(row.cells)
                .map((cell) => {
                    const text = (cell.innerText || cell.textContent || '').replace(/\s+/g, ' ').trim();
                    return delimiter === ',' ? escapeCsvCell(text) : text;
                })
                .join(delimiter))
            .filter((line) => line.trim().length > 0)
            .join('\n')
            .trim();
    };

    const tableToMarkdown = (table) => {
        if (!(table instanceof HTMLTableElement))
            return '';

        const rows = Array.from(table.rows).map((row) => Array.from(row.cells)
            .map((cell) => (cell.innerText || cell.textContent || '').replace(/\|/g, '\\|').replace(/\s+/g, ' ').trim()));

        if (!rows.length)
            return '';

        const header = rows[0];
        const divider = header.map(() => '---');
        const lines = ['| ' + header.join(' | ') + ' |', '| ' + divider.join(' | ') + ' |'];
        for (const row of rows.slice(1))
            lines.push('| ' + row.join(' | ') + ' |');
        return lines.join('\n');
    };

    const createResponseBlock = (kind, copyText) => {
        const wrapper = document.createElement('section');
        wrapper.className = 'response-block';
        wrapper.dataset.blockType = kind;
        if (typeof copyText === 'string' && copyText.trim())
            wrapper.dataset.copyText = copyText.trim();

        const toolbar = document.createElement('div');
        toolbar.className = 'response-block-toolbar';

        const toolbarLeft = document.createElement('div');
        toolbarLeft.className = 'response-block-toolbar-left';

        const title = document.createElement('span');
        title.className = 'response-block-kind';
        title.textContent = humanizeKind(kind);

        const meta = document.createElement('div');
        meta.className = 'response-block-meta';

        const actions = document.createElement('div');
        actions.className = 'response-block-actions';

        toolbarLeft.appendChild(title);
        toolbarLeft.appendChild(meta);
        toolbar.appendChild(toolbarLeft);
        toolbar.appendChild(actions);
        wrapper.appendChild(toolbar);

        const bodyElement = document.createElement('div');
        bodyElement.className = 'response-block-body';
        wrapper.appendChild(bodyElement);

        return wrapper;
    };

    const ensureActionBar = (wrapper) => wrapper.querySelector(':scope > .response-block-toolbar > .response-block-actions');
    const ensureBlockMeta = (wrapper) => wrapper.querySelector(':scope > .response-block-toolbar > .response-block-toolbar-left > .response-block-meta');
    const ensureBlockBody = (wrapper) => wrapper.querySelector(':scope > .response-block-body');

    const addBlockBadge = (wrapper, text, cssClass = '') => {
        const meta = ensureBlockMeta(wrapper);
        if (!meta)
            return;

        const badge = document.createElement('span');
        badge.className = 'meta-chip' + (cssClass ? ' ' + cssClass : '');
        badge.textContent = text;
        meta.appendChild(badge);
    };

    const ensureUniqueElementId = (element, prefix) => {
        if (!(element instanceof HTMLElement))
            return '';

        let base = element.id || (prefix + '-' + hashText(element.textContent || prefix));
        let candidate = base;
        let suffix = 1;
        while (true) {
            const existing = document.getElementById(candidate);
            if (!existing || existing === element)
                break;
            candidate = base + '-' + suffix++;
        }

        element.id = candidate;
        return candidate;
    };

    const ensureSourcePanel = (wrapper) => {
        let panel = wrapper.querySelector(':scope > .response-source-panel');
        if (panel)
            return panel;

        panel = document.createElement('div');
        panel.className = 'response-source-panel';
        panel.hidden = true;
        const pre = document.createElement('pre');
        panel.appendChild(pre);
        wrapper.appendChild(panel);
        return panel;
    };

    const getWrapperSourceText = (wrapper) => (wrapper.dataset.sourceText || '').trim();

    const setWrapperSourceText = (wrapper, text) => {
        const value = String(text || '').replace(/\r\n/g, '\n').trimEnd();
        if (!value) {
            delete wrapper.dataset.sourceText;
            return;
        }

        wrapper.dataset.sourceText = value;
        const panel = ensureSourcePanel(wrapper);
        const pre = panel.querySelector('pre');
        if (pre)
            pre.textContent = value;
    };

    const isSourceVisible = (wrapper) => wrapper.dataset.sourceVisible === '1';

    const applySourceVisibility = (wrapper, visible) => {
        const panel = wrapper.querySelector(':scope > .response-source-panel');
        if (!panel)
            return;

        const next = !!visible;
        wrapper.dataset.sourceVisible = next ? '1' : '0';
        wrapper.classList.toggle('is-source-visible', next);
        panel.hidden = !next;

        const buttons = wrapper.querySelectorAll('.response-action-button[data-copy-kind=""source-toggle""]');
        buttons.forEach((button) => {
            button.textContent = next ? 'Hide Source' : 'Source';
            button.dataset.originalText = button.textContent;
        });
    };

    const toggleWrapperSource = (wrapper, force) => {
        if (!getWrapperSourceText(wrapper))
            return;
        applySourceVisibility(wrapper, typeof force === 'boolean' ? force : !isSourceVisible(wrapper));
    };

    const isCollapsed = (wrapper) => wrapper.dataset.collapsed === '1';

    const applyCollapsedState = (wrapper, collapsed) => {
        const next = !!collapsed;
        wrapper.dataset.collapsed = next ? '1' : '0';
        wrapper.classList.toggle('is-collapsed', next);
        const buttons = wrapper.querySelectorAll('.response-action-button[data-copy-kind=""collapse""]');
        buttons.forEach((button) => {
            button.textContent = next ? 'Expand' : 'Collapse';
            button.dataset.originalText = button.textContent;
        });
    };

    const toggleWrapperCollapsed = (wrapper, force) => {
        applyCollapsedState(wrapper, typeof force === 'boolean' ? force : !isCollapsed(wrapper));
    };

    const applyCardCollapsedState = (card, collapsed) => {
        const next = !!collapsed;
        card.dataset.collapsed = next ? '1' : '0';
        card.classList.toggle('is-collapsed', next);
        const buttons = card.querySelectorAll('.response-action-button[data-copy-kind=""card-collapse""]');
        buttons.forEach((button) => {
            button.textContent = next ? 'Expand' : 'Collapse';
            button.dataset.originalText = button.textContent;
        });
    };

    const setFocusedWrapper = (wrapper) => {
        if (focusedWrapper === wrapper)
            wrapper = null;

        if (focusedWrapper instanceof HTMLElement)
            focusedWrapper.classList.remove('is-focused');

        focusedWrapper = wrapper instanceof HTMLElement ? wrapper : null;
        body.classList.toggle('focus-mode', !!focusedWrapper);

        document.querySelectorAll('.response-block .response-action-button[data-copy-kind=""focus""]').forEach((button) => {
            const block = button.closest('.response-block');
            const active = !!focusedWrapper && block === focusedWrapper;
            button.textContent = active ? 'Unfocus' : 'Focus';
            button.dataset.originalText = button.textContent;
            button.classList.toggle('is-active', active);
        });

        if (btnExitFocus) {
            btnExitFocus.hidden = !focusedWrapper;
            btnExitFocus.classList.toggle('is-active', !!focusedWrapper);
        }

        if (focusedWrapper)
            focusedWrapper.scrollIntoView({ behavior: 'smooth', block: 'center' });
    };

    const ensureAncestorsExpanded = (element) => {
        let current = element instanceof HTMLElement ? element : null;
        while (current) {
            const wrapper = current.closest('.response-block');
            if (wrapper)
                applyCollapsedState(wrapper, false);

            const card = current.closest('.copyable-card');
            if (card)
                applyCardCollapsedState(card, false);

            current = current.parentElement;
        }
    };

    const addPlainTextCopyButton = (wrapper) => {
        if (wrapper.querySelector('.response-action-button[data-copy-kind=""text""]'))
            return;

        const button = createActionButton('Copy', 'is-text', 'Copy plain text');
        button.dataset.copyKind = 'text';
        button.addEventListener('click', async () => {
            const text = getPlainCopyText(wrapper);
            if (!text)
                return;

            try {
                await requestHostAction({ type: 'copy-text', text });
                pulseButton(button, 'Copied');
            }
            catch (error) {
                pulseButton(button, 'Copy failed', true);
                showToast(error && error.message ? error.message : 'Copy failed.', true);
            }
        });

        ensureActionBar(wrapper).appendChild(button);
    };

    const addRichCopyButton = (wrapper) => {
        if (wrapper.querySelector('.response-action-button[data-copy-kind=""rich""]'))
            return;

        const button = createActionButton('Copy', 'is-text', 'Copy rich content');
        button.dataset.copyKind = 'rich';
        button.addEventListener('click', async () => {
            const payload = getRichCopyPayload(wrapper);
            if (!payload.text && !payload.html)
                return;

            try {
                await requestHostAction({ type: 'copy-rich', text: payload.text, html: payload.html });
                pulseButton(button, 'Copied');
            }
            catch (error) {
                pulseButton(button, 'Copy failed', true);
                showToast(error && error.message ? error.message : 'Copy failed.', true);
            }
        });

        ensureActionBar(wrapper).appendChild(button);
    };

    const addTableCopyButton = (wrapper) => {
        if (wrapper.querySelector('.response-action-button[data-copy-kind=""table""]'))
            return;

        const button = createActionButton('Copy Table', 'is-text', 'Copy the visible table as rich text');
        button.dataset.copyKind = 'table';
        button.addEventListener('click', async () => {
            const table = wrapper.querySelector('table');
            if (!(table instanceof HTMLTableElement))
                return;

            const payload = {
                text: tableToDelimitedText(table, '\t'),
                html: table.outerHTML
            };

            try {
                await requestHostAction({ type: 'copy-rich', text: payload.text, html: payload.html });
                pulseButton(button, 'Copied');
            }
            catch (error) {
                pulseButton(button, 'Copy failed', true);
                showToast(error && error.message ? error.message : 'Copy failed.', true);
            }
        });

        ensureActionBar(wrapper).appendChild(button);
    };

    const addCsvCopyButton = (wrapper) => {
        if (wrapper.querySelector('.response-action-button[data-copy-kind=""csv""]'))
            return;

        const button = createActionButton('Copy CSV', 'is-text', 'Copy the table as CSV');
        button.dataset.copyKind = 'csv';
        button.addEventListener('click', async () => {
            const table = wrapper.querySelector('table');
            if (!(table instanceof HTMLTableElement))
                return;

            try {
                await requestHostAction({ type: 'copy-text', text: tableToDelimitedText(table, ',') });
                pulseButton(button, 'Copied');
            }
            catch (error) {
                pulseButton(button, 'Copy failed', true);
                showToast(error && error.message ? error.message : 'Copy failed.', true);
            }
        });

        ensureActionBar(wrapper).appendChild(button);
    };

    const addBlockImageButton = (wrapper) => {
        if (wrapper.querySelector('.response-action-button[data-copy-kind=""block-image""]'))
            return;

        const button = createActionButton('Copy Image', 'is-image', 'Copy this block as an image');
        button.dataset.copyKind = 'block-image';
        button.addEventListener('click', async () => {
            try {
                await withTemporaryClass(wrapper, 'capture-image-mode', async () => {
                    const bounds = getElementClipBounds(wrapper, 2);
                    await requestHostAction({ type: 'copy-image', bounds });
                });
                pulseButton(button, 'Copied');
            }
            catch (error) {
                pulseButton(button, 'Copy failed', true);
                showToast(error && error.message ? error.message : 'Copy failed.', true);
            }
        });

        ensureActionBar(wrapper).appendChild(button);
    };

    const addShareButtons = (wrapper) => {
        const button = createActionButton('Share', 'is-text', 'Share this response');
        button.dataset.copyKind = 'share';
        button.addEventListener('click', () => {
            const url = window.location.href;
            const text = 'Check out this response: ' + url;
            if (navigator.share) {
                navigator.share({
                    title: 'AI Response',
                    text: text,
                    url: url
                })
                .then(() => console.log('Share successful'))
                .catch((error) => console.log('Error sharing:', error));
            } else {
                // Fallback for browsers that don't support the Web Share API
                showToast('Share not supported. Please copy the link manually.', true);
            }
        });

        ensureActionBar(wrapper).appendChild(button);
    };

    const addSourceToggleButton = (wrapper) => {
        if (!getWrapperSourceText(wrapper) || wrapper.querySelector('.response-action-button[data-copy-kind=""source-toggle""]'))
            return;

        const button = createActionButton('Source', 'is-text', 'Show or hide the original source for this block');
        button.dataset.copyKind = 'source-toggle';
        button.addEventListener('click', () => toggleWrapperSource(wrapper));
        ensureActionBar(wrapper).appendChild(button);
    };

    const addFocusButton = (wrapper) => {
return;
        if (wrapper.querySelector('.response-action-button[data-copy-kind=""focus""]'))
            return;

        const button = createActionButton('Focus', 'is-text', 'Spotlight this block');
        button.dataset.copyKind = 'focus';
        button.addEventListener('click', () => setFocusedWrapper(wrapper));
        ensureActionBar(wrapper).appendChild(button);
    };

    const addCollapseButton = (wrapper) => {
        if (wrapper.querySelector('.response-action-button[data-copy-kind=""collapse""]'))
            return;

        const button = createActionButton('Collapse', 'is-text', 'Collapse or expand this block');
        button.dataset.copyKind = 'collapse';
        button.addEventListener('click', () => toggleWrapperCollapsed(wrapper));
        ensureActionBar(wrapper).appendChild(button);
    };

    const addCardCollapseButton = (card) => {
        if (!(card instanceof HTMLElement) || card.querySelector('.response-action-button[data-copy-kind=""card-collapse""]'))
            return;

        const header = card.querySelector(':scope > .card-header-row');
        const actions = card.querySelector(':scope > .card-header-row > .card-actions');
        if (!header || !actions)
            return;

        const button = createActionButton('Collapse', 'is-text', 'Collapse or expand this card');
        button.dataset.copyKind = 'card-collapse';
        button.addEventListener('click', () => applyCardCollapsedState(card, card.dataset.collapsed !== '1'));
        actions.appendChild(button);
    };

    const renderCodeLines = (pre) => {
        if (!(pre instanceof HTMLElement) || pre.dataset.linesEnhanced === '1')
            return 0;

        const source = (pre.textContent || '').replace(/\r\n/g, '\n');
        const lines = source.split('\n');
        pre.textContent = '';

        const fragment = document.createDocumentFragment();
        lines.forEach((line, index) => {
            const row = document.createElement('span');
            row.className = 'code-line';

            const number = document.createElement('span');
            number.className = 'code-line-number';
            number.textContent = String(index + 1);

            const content = document.createElement('span');
            content.className = 'code-line-content';
            content.textContent = line.length ? line : ' ';

            row.appendChild(number);
            row.appendChild(content);
            fragment.appendChild(row);
        });

        pre.appendChild(fragment);
        pre.dataset.linesEnhanced = '1';
        return lines.length;
    };

    const enhanceCodeWrapper = (wrapper) => {
        const pre = wrapper.querySelector('pre');
        if (!(pre instanceof HTMLElement))
            return;

        const source = (pre.textContent || '').replace(/\r\n/g, '\n').trimEnd();
        if (source)
            wrapper.dataset.copyText = source;

        const language = (pre.dataset.codeLanguage || '').trim();
        const lineCount = renderCodeLines(pre);
        if (language)
            addBlockBadge(wrapper, language, 'meta-chip-accent');
        addBlockBadge(wrapper, lineCount + ' line' + (lineCount === 1 ? '' : 's'));
        setWrapperSourceText(wrapper, source);
        addPlainTextCopyButton(wrapper);
        addShareButtons(wrapper);
        addSourceToggleButton(wrapper);
        addBlockImageButton(wrapper);
        addFocusButton(wrapper);
        addCollapseButton(wrapper);
    };

    const guessTableSortType = (values) => values.every((value) => {
        const normalized = value.replace(/[,$%]/g, '').trim();
        return normalized === '' || !Number.isNaN(Number(normalized));
    }) ? 'number' : 'text';

    const sortTableByColumn = (table, columnIndex, direction) => {
        const bodyElement = table.tBodies[0];
        if (!bodyElement)
            return;

        const rows = Array.from(bodyElement.rows);
        const values = rows.map((row) => ((row.cells[columnIndex]?.innerText || row.cells[columnIndex]?.textContent || '')).trim());
        const type = guessTableSortType(values);
        const multiplier = direction === 'desc' ? -1 : 1;

        rows.sort((left, right) => {
            const leftText = ((left.cells[columnIndex]?.innerText || left.cells[columnIndex]?.textContent || '')).trim();
            const rightText = ((right.cells[columnIndex]?.innerText || right.cells[columnIndex]?.textContent || '')).trim();
            if (type === 'number')
                return (Number(leftText.replace(/[,$%]/g, '')) - Number(rightText.replace(/[,$%]/g, ''))) * multiplier;
            return leftText.localeCompare(rightText, undefined, { numeric: true, sensitivity: 'base' }) * multiplier;
        });

        rows.forEach((row) => bodyElement.appendChild(row));
    };

    const updateTableFilterSummary = (wrapper) => {
        const table = wrapper.querySelector('table');
        if (!(table instanceof HTMLTableElement))
            return;

        const bodyElement = table.tBodies[0];
        const rows = bodyElement ? Array.from(bodyElement.rows) : [];
        const visibleCount = rows.filter((row) => row.style.display !== 'none').length;
        const counter = wrapper.querySelector('.table-filter-count');
        if (counter)
            counter.textContent = visibleCount + ' / ' + rows.length + ' rows';
    };

    const enhanceTableWrapper = (wrapper) => {
        const table = wrapper.querySelector('table');
        if (!(table instanceof HTMLTableElement))
            return;

        const headerCount = table.tHead && table.tHead.rows.length ? table.tHead.rows[0].cells.length : (table.rows[0] ? table.rows[0].cells.length : 0);
        const bodyCount = table.tBodies[0] ? table.tBodies[0].rows.length : Math.max(0, table.rows.length - 1);
        addBlockBadge(wrapper, bodyCount + ' row' + (bodyCount === 1 ? '' : 's'));
        addBlockBadge(wrapper, headerCount + ' col' + (headerCount === 1 ? '' : 's'));
        setWrapperSourceText(wrapper, tableToMarkdown(table));

        const tools = document.createElement('div');
        tools.className = 'table-tools';

        const filterInput = document.createElement('input');
        filterInput.type = 'search';
        filterInput.className = 'table-filter-input';
        filterInput.placeholder = 'Filter rows';

        const filterCount = document.createElement('span');
        filterCount.className = 'table-filter-count';

        tools.appendChild(filterInput);
        tools.appendChild(filterCount);
        wrapper.insertBefore(tools, ensureBlockBody(wrapper));

        const applyFilter = debounce(() => {
            const query = filterInput.value.trim().toLowerCase();
            const bodyElement = table.tBodies[0];
            if (!bodyElement)
                return;

            Array.from(bodyElement.rows).forEach((row) => {
                const haystack = (row.innerText || row.textContent || '').toLowerCase();
                row.style.display = !query || haystack.includes(query) ? '' : 'none';
            });

            updateTableFilterSummary(wrapper);
        }, 60);

        filterInput.addEventListener('input', applyFilter);
        updateTableFilterSummary(wrapper);

        const headers = Array.from(table.querySelectorAll('thead th'));
        headers.forEach((header, columnIndex) => {
            header.tabIndex = 0;
            header.classList.add('table-sortable');
            header.addEventListener('click', () => {
                const current = header.dataset.sortDirection === 'asc' ? 'desc' : 'asc';
                headers.forEach((other) => {
                    if (other !== header)
                        delete other.dataset.sortDirection;
                });
                header.dataset.sortDirection = current;
                sortTableByColumn(table, columnIndex, current);
                updateTableFilterSummary(wrapper);
            });
            header.addEventListener('keydown', (event) => {
                if (event.key === 'Enter' || event.key === ' ') {
                    event.preventDefault();
                    header.click();
                }
            });
        });

        addTableCopyButton(wrapper);
        addCsvCopyButton(wrapper);
        addSourceToggleButton(wrapper);
        addBlockImageButton(wrapper);
        addFocusButton(wrapper);
        addCollapseButton(wrapper);
    };

    const enhanceTextWrapper = (wrapper) => {
        const bodyElement = ensureBlockBody(wrapper);
        const paragraphCount = bodyElement ? bodyElement.querySelectorAll('p, ul, ol, blockquote').length : 0;
        const wordCount = bodyElement ? ((bodyElement.innerText || bodyElement.textContent || '').trim().split(/\s+/).filter(Boolean).length) : 0;
        addBlockBadge(wrapper, paragraphCount + ' item' + (paragraphCount === 1 ? '' : 's'));
        addBlockBadge(wrapper, wordCount + ' words');
        addRichCopyButton(wrapper);
        addBlockImageButton(wrapper);
        addFocusButton(wrapper);
        addCollapseButton(wrapper);
    };

    const addMermaidZoomButtons = (wrapper) => {
        const actionBar = ensureActionBar(wrapper);
        if (!actionBar || wrapper.querySelector('.response-action-button[data-copy-kind=""zoom-in""]'))
            return;

        const setZoom = (delta) => {
            const diagram = wrapper.querySelector('.mermaid-diagram');
            const svg = diagram ? diagram.querySelector('svg') : null;
            if (!(svg instanceof SVGElement))
                return;

            const current = Number(wrapper.dataset.mermaidZoom || '1');
            const next = Math.max(0.4, Math.min(3, delta === 0 ? 1 : current + delta));
            wrapper.dataset.mermaidZoom = String(next);
            svg.style.transformOrigin = 'top left';
            svg.style.transform = 'scale(' + next.toFixed(2) + ')';
        };

        const zoomIn = createActionButton('+', 'is-text', 'Zoom in');
        zoomIn.dataset.copyKind = 'zoom-in';
        zoomIn.addEventListener('click', () => setZoom(0.15));

        const zoomOut = createActionButton('−', 'is-text', 'Zoom out');
        zoomOut.dataset.copyKind = 'zoom-out';
        zoomOut.addEventListener('click', () => setZoom(-0.15));

        const zoomReset = createActionButton('100%', 'is-text', 'Reset zoom');
        zoomReset.dataset.copyKind = 'zoom-reset';
        zoomReset.addEventListener('click', () => setZoom(0));

        actionBar.appendChild(zoomOut);
        actionBar.appendChild(zoomIn);
        actionBar.appendChild(zoomReset);
    };

    const createSvgElementFromMarkup = (svgMarkup) => {
        const template = document.createElement('template');
        template.innerHTML = svgMarkup.trim();
        const svg = template.content.querySelector('svg');
        if (!(svg instanceof SVGElement))
            throw new Error('Rendered Mermaid output did not contain an SVG element.');
        return svg;
    };

    const getSvgDimensions = (svgElement) => {
        const viewBox = svgElement.viewBox && svgElement.viewBox.baseVal ? svgElement.viewBox.baseVal : null;
        const rect = svgElement.getBoundingClientRect();
        return {
            width: Math.max(1, Math.ceil(viewBox && viewBox.width ? viewBox.width : rect.width || svgElement.clientWidth || 1)),
            height: Math.max(1, Math.ceil(viewBox && viewBox.height ? viewBox.height : rect.height || svgElement.clientHeight || 1))
        };
    };

    const svgToPngDataUrl = async (svgSource) => {
        const svgElement = typeof svgSource === 'string' ? createSvgElementFromMarkup(svgSource) : svgSource;
        const dimensions = getSvgDimensions(svgElement);
        const clone = svgElement.cloneNode(true);
        clone.setAttribute('xmlns', 'http://www.w3.org/2000/svg');
        clone.setAttribute('xmlns:xlink', 'http://www.w3.org/1999/xlink');
        clone.setAttribute('width', String(dimensions.width));
        clone.setAttribute('height', String(dimensions.height));
        if (!clone.getAttribute('viewBox'))
            clone.setAttribute('viewBox', '0 0 ' + dimensions.width + ' ' + dimensions.height);

        const serializer = new XMLSerializer();
        const markup = serializer.serializeToString(clone);
        const blob = new Blob([markup], { type: 'image/svg+xml;charset=utf-8' });
        const objectUrl = URL.createObjectURL(blob);
        try {
            const image = await new Promise((resolve, reject) => {
                const img = new Image();
                img.onload = () => resolve(img);
                img.onerror = () => reject(new Error('Unable to load Mermaid SVG into an image.'));
                img.src = objectUrl;
            });
            const scale = Math.max(1, window.devicePixelRatio || 1);
            const canvas = document.createElement('canvas');
            canvas.width = Math.max(1, Math.round(dimensions.width * scale));
            canvas.height = Math.max(1, Math.round(dimensions.height * scale));
            const context = canvas.getContext('2d');
            if (!context)
                throw new Error('Canvas 2D context is unavailable.');
            context.setTransform(scale, 0, 0, scale, 0, 0);
            context.clearRect(0, 0, dimensions.width, dimensions.height);
            context.drawImage(image, 0, 0, dimensions.width, dimensions.height);
            return canvas.toDataURL('image/png');
        }
        finally {
            URL.revokeObjectURL(objectUrl);
        }
    };

    const renderMermaidExportSvg = async (definition) => {
        if (!definition || !mermaidApi)
            throw new Error('Mermaid export is unavailable.');

        mermaidApi.initialize({
            startOnLoad: false,
            securityLevel: 'loose',
            theme: 'default',
            htmlLabels: false,
            flowchart: { useMaxWidth: true, htmlLabels: false }
        });

        try {
            await mermaidApi.parse(definition, { suppressErrors: false });
            const renderResult = await mermaidApi.render('mermaid-export-' + Date.now() + '-' + Math.random().toString(36).slice(2), definition);
            if (!renderResult || !renderResult.svg)
                throw new Error('Mermaid export did not produce SVG output.');
            return renderResult.svg;
        }
        finally {
            mermaidApi.initialize({
                startOnLoad: false,
                securityLevel: 'loose',
                theme: 'default',
                htmlLabels: false,
                flowchart: { useMaxWidth: true, htmlLabels: false }
            });
        }
    };

    const getMermaidImageDataUrl = async (wrapper) => {
        const renderedSvg = wrapper.querySelector('.mermaid-diagram svg');
        if (renderedSvg instanceof SVGElement) {
            try {
                return await svgToPngDataUrl(renderedSvg);
            }
            catch {
                // fallback below
            }
        }

        const definition = (wrapper.dataset.mermaidDefinition || getPlainCopyText(wrapper) || '').trim();
        if (!definition)
            throw new Error('No rendered Mermaid diagram was available to copy.');

        const exportSvgMarkup = await renderMermaidExportSvg(definition);
        return await svgToPngDataUrl(exportSvgMarkup);
    };

    const addMermaidImageCopyButton = (wrapper) => {
        if (wrapper.querySelector('.response-action-button[data-copy-kind=""image""]'))
            return;

        const button = createActionButton('Copy Image', 'is-image', 'Copy the Mermaid diagram as PNG');
        button.dataset.copyKind = 'image';
        button.addEventListener('click', async () => {
            try {
                const dataUrl = await getMermaidImageDataUrl(wrapper);
                await requestHostAction({ type: 'copy-image', dataUrl });
                pulseButton(button, 'Copied');
            }
            catch (error) {
                pulseButton(button, 'Copy failed', true);
                showToast(error && error.message ? error.message : 'Copy failed.', true);
            }
        });
        ensureActionBar(wrapper).appendChild(button);
    };

    const enhanceMermaidWrapper = (wrapper) => {
        const definition = getPlainCopyText(wrapper);
        if (definition)
            setWrapperSourceText(wrapper, definition);
        addBlockBadge(wrapper, 'Mermaid', 'meta-chip-accent');
        addPlainTextCopyButton(wrapper);
        addMermaidImageCopyButton(wrapper);
        addShareButtons(wrapper);
        addSourceToggleButton(wrapper);
        addBlockImageButton(wrapper);
        addFocusButton(wrapper);
        addCollapseButton(wrapper);
        addMermaidZoomButtons(wrapper);
    };

    const finalizeWrapper = (wrapper) => {
        const seed = wrapper.dataset.copyText || wrapper.dataset.sourceText || wrapper.innerText || wrapper.textContent || wrapper.dataset.blockType || 'block';
        ensureUniqueElementId(wrapper, 'block-' + (wrapper.dataset.blockType || 'item') + '-' + hashText(seed));

        switch (wrapper.dataset.blockType) {
            case 'code':
                enhanceCodeWrapper(wrapper);
                break;
            case 'table':
                enhanceTableWrapper(wrapper);
                break;
            case 'mermaid':
                enhanceMermaidWrapper(wrapper);
                break;
            default:
                enhanceTextWrapper(wrapper);
                break;
        }

        if (viewerState.showSources)
            applySourceVisibility(wrapper, !!getWrapperSourceText(wrapper));
    };

    const decorateResponseBlocks = () => {
        const originalChildren = Array.from(responseContent.children).filter((node) => node instanceof HTMLElement);
        responseContent.innerHTML = '';

        let currentTextWrapper = null;
        for (const node of originalChildren) {
            if (!(node instanceof HTMLElement))
                continue;

            if (node.classList.contains('response-block') || node.classList.contains('empty')) {
                currentTextWrapper = null;
                responseContent.appendChild(node);
                continue;
            }

            if (node.matches('pre.mermaid')) {
                currentTextWrapper = null;
                const definition = (node.textContent || '').trim();
                const wrapper = createResponseBlock('mermaid', definition);
                ensureBlockBody(wrapper).appendChild(node);
                responseContent.appendChild(wrapper);
                continue;
            }

            if (node.matches('pre')) {
                currentTextWrapper = null;
                const source = (node.textContent || '').replace(/\r\n/g, '\n').trimEnd();
                const wrapper = createResponseBlock('code', source);
                ensureBlockBody(wrapper).appendChild(node);
                responseContent.appendChild(wrapper);
                continue;
            }

            if (node.matches('.table-scroll')) {
                currentTextWrapper = null;
                const wrapper = createResponseBlock('table', '');
                ensureBlockBody(wrapper).appendChild(node);
                responseContent.appendChild(wrapper);
                continue;
            }

            if (!currentTextWrapper) {
                currentTextWrapper = createResponseBlock('text', '');
                responseContent.appendChild(currentTextWrapper);
            }

            ensureBlockBody(currentTextWrapper).appendChild(node);
        }

        Array.from(responseContent.querySelectorAll('.response-block')).forEach((wrapper) => finalizeWrapper(wrapper));
    };

    const decorateCards = () => {
        document.querySelectorAll('.copyable-card').forEach((card) => addCardCollapseButton(card));
    };

    const wireCopyableCards = () => {
        document.querySelectorAll('.copyable-card').forEach((card) => {
            if (!(card instanceof HTMLElement))
                return;

            const copyButton = card.querySelector('.card-copy-button');
            if (copyButton instanceof HTMLButtonElement && copyButton.dataset.bound !== '1') {
                copyButton.dataset.bound = '1';
                copyButton.addEventListener('click', async () => {
                    const payload = getCardCopyPayload(card);
                    if (!payload.text && !payload.html)
                        return;
                    try {
                        await requestHostAction({ type: 'copy-rich', text: payload.text, html: payload.html });
                        pulseButton(copyButton, 'Copied');
                    }
                    catch (error) {
                        pulseButton(copyButton, 'Copy failed', true);
                        showToast(error && error.message ? error.message : 'Copy failed.', true);
                    }
                });
            }

            const imageButton = card.querySelector('.card-copy-image-button');
            if (imageButton instanceof HTMLButtonElement && imageButton.dataset.bound !== '1') {
                imageButton.dataset.bound = '1';
                imageButton.addEventListener('click', async () => {
                    try {
                        await withTemporaryClass(card, 'capture-image-mode', async () => {
                            const bounds = getElementClipBounds(card, 2);
                            await requestHostAction({ type: 'copy-image', bounds });
                        });
                        pulseButton(imageButton, 'Copied');
                    }
                    catch (error) {
                        pulseButton(imageButton, 'Copy failed', true);
                        showToast(error && error.message ? error.message : 'Copy failed.', true);
                    }
                });
            }

            const shareDropdownButton = card.querySelector('.card-share-dropdown-button');
            const shareMenu = card.querySelector('.card-share-menu');
            if (shareDropdownButton instanceof HTMLButtonElement && shareMenu instanceof HTMLElement && shareDropdownButton.dataset.bound !== '1') {
                shareDropdownButton.dataset.bound = '1';
                shareDropdownButton.addEventListener('click', (e) => {
                    e.stopPropagation();
                    const isVisible = shareMenu.style.display === 'block';
                    document.querySelectorAll('.card-share-menu').forEach(m => m.style.display = 'none');
                    shareMenu.style.display = isVisible ? 'none' : 'block';
                });

                card.querySelectorAll('.card-share-action').forEach(btn => {
                    btn.addEventListener('click', async (e) => {
                        e.stopPropagation();
                        shareMenu.style.display = 'none';
                        const target = btn.dataset.target;
                        const payload = getCardCopyPayload(card);
                        if (!payload.text && !payload.html) return;
                        try {
                            await requestHostAction({ type: 'share-' + target, text: payload.text, html: payload.html });
                        } catch (err) {
                            showToast(err && err.message ? err.message : 'Share failed.', true);
                        }
                    });
                });
            }
        });

        document.addEventListener('click', (e) => {
            document.querySelectorAll('.card-share-menu').forEach(m => m.style.display = 'none');
        });
    };

    const isSkippableLinkContainer = (element) => {
        if (!(element instanceof HTMLElement))
            return false;
        return !!element.closest('a, pre, code, textarea, .response-source-panel');
    };

    const buildLinkNode = (value) => {
        const trimmed = String(value || '');
        if (!trimmed)
            return null;

        const anchor = document.createElement('a');
        const isEmail = /^[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}$/i.test(trimmed);
        anchor.href = isEmail ? 'mailto:' + trimmed : trimmed;
        anchor.className = 'response-link';
        anchor.dataset.externalLink = '1';
        anchor.target = '_blank';
        anchor.rel = 'noopener noreferrer';
        anchor.textContent = trimmed;
        return anchor;
    };

    const splitTrailingPunctuation = (value) => {
        const match = String(value || '').match(/^(.*?)([).,!?:;]+)?$/);
        return {
            core: match ? (match[1] || '') : value,
            trailing: match ? (match[2] || '') : ''
        };
    };

    const linkifyResponseContent = () => {
        const walker = document.createTreeWalker(responseContent, NodeFilter.SHOW_TEXT, {
            acceptNode(node) {
                const parent = node.parentElement;
                if (!node.nodeValue || !node.nodeValue.trim() || isSkippableLinkContainer(parent))
                    return NodeFilter.FILTER_REJECT;
                return NodeFilter.FILTER_ACCEPT;
            }
        });

        const candidates = [];
        while (walker.nextNode())
            candidates.push(walker.currentNode);

        const pattern = /(https?:\/\/[^\s<]+|[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,})/ig;
        candidates.forEach((node) => {
            const source = node.nodeValue || '';
            pattern.lastIndex = 0;
            if (!pattern.test(source))
                return;

            pattern.lastIndex = 0;
            const fragment = document.createDocumentFragment();
            let lastIndex = 0;
            let match = null;
            while ((match = pattern.exec(source)) !== null) {
                const raw = match[0] || '';
                const split = splitTrailingPunctuation(raw);
                const startIndex = match.index;
                const endIndex = startIndex + split.core.length;

                if (startIndex > lastIndex)
                    fragment.appendChild(document.createTextNode(source.slice(lastIndex, startIndex)));

                const anchor = buildLinkNode(split.core);
                if (anchor)
                    fragment.appendChild(anchor);
                else
                    fragment.appendChild(document.createTextNode(split.core));

                if (split.trailing)
                    fragment.appendChild(document.createTextNode(split.trailing));

                lastIndex = startIndex + raw.length;
            }

            if (lastIndex < source.length)
                fragment.appendChild(document.createTextNode(source.slice(lastIndex)));

            node.parentNode.replaceChild(fragment, node);
        });
    };

    const clearSearchHighlights = () => {
        document.querySelectorAll('mark.search-hit').forEach((mark) => {
            const text = document.createTextNode(mark.textContent || '');
            mark.replaceWith(text);
        });
        responseContent.normalize();
        searchHits = [];
        activeSearchIndex = -1;
    };

    const highlightSearch = (query) => {
        clearSearchHighlights();
        const value = String(query || '').trim();
        if (!value) {
            if (searchCount)
                searchCount.textContent = '0 results';
            return;
        }

        const escaped = value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
        const matcher = new RegExp(escaped, 'ig');
        const walker = document.createTreeWalker(page, NodeFilter.SHOW_TEXT, {
            acceptNode(node) {
                const parent = node.parentElement;
                if (!node.nodeValue || !node.nodeValue.trim())
                    return NodeFilter.FILTER_REJECT;
                if (!parent)
                    return NodeFilter.FILTER_REJECT;
                if (parent.closest('.viewer-toolbar, .viewer-outline, .response-source-panel[hidden], script, style, textarea'))
                    return NodeFilter.FILTER_REJECT;
                return NodeFilter.FILTER_ACCEPT;
            }
        });

        const textNodes = [];
        while (walker.nextNode())
            textNodes.push(walker.currentNode);

        textNodes.forEach((node) => {
            const text = node.nodeValue || '';
            matcher.lastIndex = 0;
            if (!matcher.test(text))
                return;

            matcher.lastIndex = 0;
            const fragment = document.createDocumentFragment();
            let lastIndex = 0;
            let match = null;
            while ((match = matcher.exec(text)) !== null) {
                const startIndex = match.index;
                const endIndex = startIndex + match[0].length;
                if (startIndex > lastIndex)
                    fragment.appendChild(document.createTextNode(text.slice(lastIndex, startIndex)));
                const mark = document.createElement('mark');
                mark.className = 'search-hit';
                mark.textContent = text.slice(startIndex, endIndex);
                fragment.appendChild(mark);
                searchHits.push(mark);
                lastIndex = endIndex;
            }
            if (lastIndex < text.length)
                fragment.appendChild(document.createTextNode(text.slice(lastIndex)));
            node.parentNode.replaceChild(fragment, node);
        });

        if (searchCount)
            searchCount.textContent = searchHits.length + ' result' + (searchHits.length === 1 ? '' : 's');

        if (searchHits.length)
            activateSearchHit(0);
    };

    const activateSearchHit = (index) => {
        if (!searchHits.length)
            return;

        activeSearchIndex = (index + searchHits.length) % searchHits.length;
        searchHits.forEach((hit, hitIndex) => hit.classList.toggle('is-active', hitIndex === activeSearchIndex));

        const target = searchHits[activeSearchIndex];
        ensureAncestorsExpanded(target);
        target.scrollIntoView({ behavior: 'smooth', block: 'center' });
        if (searchCount)
            searchCount.textContent = (activeSearchIndex + 1) + ' / ' + searchHits.length + ' results';
    };

    const buildOutline = () => {
        if (!outlineContent)
            return;

        outlineContent.innerHTML = '';
        const groups = [];

        const cardTargets = Array.from(document.querySelectorAll('.page > section .copyable-card > .card-header-row h2'));
        if (cardTargets.length) {
            groups.push({
                title: 'Cards',
                items: cardTargets.map((heading) => ({
                    label: (heading.textContent || '').trim(),
                    target: heading.closest('.copyable-card') || heading
                }))
            });
        }

        const headingTargets = Array.from(document.querySelectorAll('#response-content h1, #response-content h2, #response-content h3'));
        if (headingTargets.length) {
            groups.push({
                title: 'Headings',
                items: headingTargets.map((heading) => ({
                    label: (heading.textContent || '').trim(),
                    target: heading,
                    level: Number(heading.tagName.substring(1))
                }))
            });
        }

        const blockTargets = Array.from(document.querySelectorAll('#response-content .response-block[data-block-type=""code""], #response-content .response-block[data-block-type=""table""], #response-content .response-block[data-block-type=""mermaid""]'));
        if (blockTargets.length) {
            groups.push({
                title: 'Blocks',
                items: blockTargets.map((wrapper, index) => ({
                    label: humanizeKind(wrapper.dataset.blockType) + ' ' + (index + 1),
                    target: wrapper
                }))
            });
        }

        if (!groups.length) {
            const empty = document.createElement('div');
            empty.className = 'outline-empty';
            empty.textContent = 'No sections detected yet.';
            outlineContent.appendChild(empty);
            return;
        }

        groups.forEach((group) => {
            const section = document.createElement('section');
            section.className = 'outline-group';

            const title = document.createElement('div');
            title.className = 'outline-group-title';
            title.textContent = group.title;
            section.appendChild(title);

            group.items.forEach((item) => {
                const button = document.createElement('button');
                button.type = 'button';
                button.className = 'outline-item' + (item.level ? ' level-' + item.level : '');
                button.textContent = item.label;
                button.addEventListener('click', () => {
                    const target = item.target;
                    ensureUniqueElementId(target, 'outline-target');
                    ensureAncestorsExpanded(target);
                    target.scrollIntoView({ behavior: 'smooth', block: 'start' });
                });
                section.appendChild(button);
            });

            outlineContent.appendChild(section);
        });
    };

    const updateStats = () => {
        if (!statsBar)
            return;

        const plainText = (page.innerText || page.textContent || '').trim();
        const wordCount = plainText ? plainText.split(/\s+/).filter(Boolean).length : 0;
        const charCount = plainText.length;
        const blockCount = document.querySelectorAll('#response-content .response-block').length;
        const codeCount = document.querySelectorAll('#response-content .response-block[data-block-type=""code""]').length;
        const tableCount = document.querySelectorAll('#response-content .response-block[data-block-type=""table""]').length;
        const mermaidCount = document.querySelectorAll('#response-content .response-block[data-block-type=""mermaid""]').length;
        const readingTimeMinutes = Math.max(1, Math.ceil(wordCount / 220));
        const stats = [
            ['Words', wordCount],
            ['Chars', charCount],
            ['Blocks', blockCount],
            ['Code', codeCount],
            ['Tables', tableCount],
            ['Diagrams', mermaidCount],
            ['Read', readingTimeMinutes + ' min']
        ];
        statsBar.innerHTML = stats.map(([label, value]) => '<span class=""viewer-stat""><strong>' + escapeHtml(String(value)) + '</strong><span>' + escapeHtml(String(label)) + '</span></span>').join('');
    };

    const updateProgress = () => {
        if (!progressBar)
            return;

        const scrollTop = window.scrollY || document.documentElement.scrollTop || 0;
        const scrollHeight = Math.max(1, document.documentElement.scrollHeight - window.innerHeight);
        const percent = Math.max(0, Math.min(100, (scrollTop / scrollHeight) * 100));
        progressBar.style.width = percent.toFixed(2) + '%';
    };

    const updateToolbarState = () => {
        body.dataset.theme = viewerState.theme === 'dark' ? 'dark' : 'light';
        body.dataset.density = viewerState.density === 'compact' ? 'compact' : 'comfortable';
        body.dataset.wrapCode = viewerState.wrapCode ? '1' : '0';
        if (outline)
            outline.classList.toggle('is-open', !!viewerState.outlineOpen);

        document.querySelectorAll('.response-block').forEach((wrapper) => {
            if (viewerState.showSources && getWrapperSourceText(wrapper))
                applySourceVisibility(wrapper, true);
            else if (!viewerState.showSources)
                applySourceVisibility(wrapper, false);
        });

        const setButtonState = (button, active, activeText, inactiveText) => {
            if (!(button instanceof HTMLButtonElement))
                return;
            button.classList.toggle('is-active', !!active);
            button.textContent = active ? activeText : inactiveText;
            button.dataset.originalText = button.textContent;
        };

        setButtonState(btnToggleOutline, !!viewerState.outlineOpen, 'Outline On', 'Outline');
        setButtonState(btnToggleTheme, viewerState.theme === 'dark', 'Dark', 'Light');
        setButtonState(btnToggleDensity, viewerState.density === 'compact', 'Compact', 'Comfortable');
        setButtonState(btnToggleWrap, !!viewerState.wrapCode, 'Wrap On', 'Wrap');
        setButtonState(btnToggleSources, !!viewerState.showSources, 'Sources On', 'Sources');
        schedulePersistViewerState();
    };

    const collapseAll = (collapsed) => {
        document.querySelectorAll('.response-block').forEach((wrapper) => applyCollapsedState(wrapper, collapsed));
        document.querySelectorAll('.copyable-card').forEach((card) => applyCardCollapsedState(card, collapsed));
    };

    const toggleOutline = (force) => {
        viewerState.outlineOpen = typeof force === 'boolean' ? force : !viewerState.outlineOpen;
        updateToolbarState();
    };

    const toggleTheme = () => {
        viewerState.theme = viewerState.theme === 'dark' ? 'light' : 'dark';
        updateToolbarState();
    };

    const toggleDensity = () => {
        viewerState.density = viewerState.density === 'compact' ? 'comfortable' : 'compact';
        updateToolbarState();
    };

    const toggleWrap = () => {
        viewerState.wrapCode = !viewerState.wrapCode;
        updateToolbarState();
    };

    const toggleSources = () => {
        viewerState.showSources = !viewerState.showSources;
        updateToolbarState();
    };

    const copyWholePage = async () => {
        const payload = {
            text: (page.innerText || page.textContent || '').trim(),
            html: page.innerHTML || ''
        };

        try {
            await requestHostAction({ type: 'copy-rich', text: payload.text, html: payload.html });
            showToast('Page copied.');
        }
        catch (error) {
            showToast(error && error.message ? error.message : 'Copy failed.', true);
        }
    };

    if (searchInput instanceof HTMLInputElement)
        searchInput.addEventListener('input', debounce(() => highlightSearch(searchInput.value), 90));
    if (btnSearchPrev instanceof HTMLButtonElement)
        btnSearchPrev.addEventListener('click', () => activateSearchHit(activeSearchIndex - 1));
    if (btnSearchNext instanceof HTMLButtonElement)
        btnSearchNext.addEventListener('click', () => activateSearchHit(activeSearchIndex + 1));
    if (btnCopyPage instanceof HTMLButtonElement)
        btnCopyPage.addEventListener('click', copyWholePage);
    if (btnToggleOutline instanceof HTMLButtonElement)
        btnToggleOutline.addEventListener('click', () => toggleOutline());
    if (btnToggleTheme instanceof HTMLButtonElement)
        btnToggleTheme.addEventListener('click', toggleTheme);
    if (btnToggleDensity instanceof HTMLButtonElement)
        btnToggleDensity.addEventListener('click', toggleDensity);
    if (btnToggleWrap instanceof HTMLButtonElement)
        btnToggleWrap.addEventListener('click', toggleWrap);
    if (btnToggleSources instanceof HTMLButtonElement)
        btnToggleSources.addEventListener('click', toggleSources);
    if (btnCollapseAll instanceof HTMLButtonElement)
        btnCollapseAll.addEventListener('click', () => collapseAll(true));
    if (btnExpandAll instanceof HTMLButtonElement)
        btnExpandAll.addEventListener('click', () => collapseAll(false));
    if (btnExitFocus instanceof HTMLButtonElement)
        btnExitFocus.addEventListener('click', () => setFocusedWrapper(null));

    document.addEventListener('click', (event) => {
        const target = event.target instanceof Element ? event.target : null;
        const anchor = target ? target.closest('a.response-link, a[data-external-link=""1""]') : null;
        if (!(anchor instanceof HTMLAnchorElement))
            return;

        const href = anchor.getAttribute('href') || '';
        if (!href)
            return;

        event.preventDefault();
        if (!safePostHostMessage({ type: 'launch-url', url: href }))
            window.open(href, '_blank', 'noopener,noreferrer');
    });

    document.addEventListener('keydown', (event) => {
        const target = event.target;
        const typing = target instanceof HTMLInputElement || target instanceof HTMLTextAreaElement || target instanceof HTMLSelectElement;

        if (event.key === '/' && !typing && searchInput instanceof HTMLInputElement) {
            event.preventDefault();
            searchInput.focus();
            searchInput.select();
            return;
        }

        if (event.key === 'Escape') {
            if (focusedWrapper) {
                event.preventDefault();
                setFocusedWrapper(null);
                return;
            }
            if (searchInput instanceof HTMLInputElement && searchInput === document.activeElement && searchInput.value) {
                event.preventDefault();
                searchInput.value = '';
                highlightSearch('');
                return;
            }
        }

        if (typing)
            return;

        if (event.key === 'F3') {
            event.preventDefault();
            activateSearchHit(activeSearchIndex + (event.shiftKey ? -1 : 1));
            return;
        }

        if (event.altKey && !event.shiftKey && !event.ctrlKey) {
            switch (event.key.toLowerCase()) {
                case 'd':
                    event.preventDefault();
                    toggleTheme();
                    break;
                case 'w':
                    event.preventDefault();
                    toggleWrap();
                    break;
                case 'o':
                    event.preventDefault();
                    toggleOutline();
                    break;
                case 's':
                    event.preventDefault();
                    toggleSources();
                    break;
            }
        }
    });

    decorateResponseBlocks();
    decorateCards();
    wireCopyableCards();
    updateToolbarState();
    updateProgress();

    runWhenIdle(() => {
        linkifyResponseContent();
        buildOutline();
        updateStats();
    });

    window.addEventListener('scroll', updateProgress, { passive: true });
    window.addEventListener('resize', debounce(() => {
        updateProgress();
        updateStats();
    }, 50));

    const mermaidBlocks = Array.from(document.querySelectorAll('pre.mermaid'));
    if (mermaidBlocks.length) {
        const showStatus = (message) => {
            if (!status)
                return;
            status.textContent = message;
            status.classList.add('visible');
        };

        try {
            const mermaidModule = await import('https://cdn.jsdelivr.net/npm/mermaid@11/dist/mermaid.esm.min.mjs');
            mermaidApi = mermaidModule.default;
            mermaidApi.initialize({
                startOnLoad: false,
                securityLevel: 'loose',
                theme: 'default',
                htmlLabels: false,
                flowchart: { useMaxWidth: true, htmlLabels: false }
            });

            for (let index = 0; index < mermaidBlocks.length; index++) {
                const block = mermaidBlocks[index];
                const definition = (block.textContent || '').trim();
                if (!definition)
                    continue;

                try {
                    await mermaidApi.parse(definition, { suppressErrors: false });
                    const container = document.createElement('div');
                    container.className = 'mermaid-diagram';
                    const renderResult = await mermaidApi.render('mermaid-diagram-' + index + '-' + Date.now(), definition);
                    container.innerHTML = renderResult.svg;
                    block.replaceWith(container);
                    const wrapper = container.closest('.response-block');
                    if (wrapper) {
                        wrapper.dataset.copyText = definition;
                        wrapper.dataset.mermaidDefinition = definition;
                        enhanceMermaidWrapper(wrapper);
                    }
                    if (renderResult.bindFunctions)
                        renderResult.bindFunctions(container);
                }
                catch (diagramError) {
                    block.classList.add('mermaid-source-error');
                    const errorBox = document.createElement('div');
                    errorBox.className = 'mermaid-error';
                    errorBox.textContent = 'Mermaid render error: ' + (diagramError && diagramError.message ? diagramError.message : String(diagramError));
                    block.parentElement?.insertBefore(errorBox, block);
                }
            }
        }
        catch (loadError) {
            showStatus('Mermaid diagrams were detected, but the library could not be loaded. Diagram source is shown instead. ' + (loadError && loadError.message ? loadError.message : String(loadError)));
            mermaidBlocks.forEach((block) => block.classList.add('mermaid-source-error'));
        }
        finally {
            runWhenIdle(() => {
                buildOutline();
                updateStats();
            });
        }
    }
});

</script>";
    }



    private static void AppendCodeBlockHtml(StringBuilder sb, IReadOnlyList<string> codeBuffer, string? language)
    {
        string codeText = string.Join("\n", codeBuffer);
        bool renderAsMermaid = IsMermaidCodeFenceLanguage(language) || LooksLikeMermaidDefinition(codeText);
        string safeLanguage = WebUtility.HtmlEncode((renderAsMermaid ? "mermaid" : language ?? string.Empty).Trim().ToLowerInvariant());

        sb.Append("<pre");
        if (renderAsMermaid)
            sb.Append(" class=\"mermaid\"");
        if (!string.IsNullOrWhiteSpace(safeLanguage))
            sb.Append(" data-code-language=\"").Append(safeLanguage).Append("\"");
        sb.Append(">");
        sb.Append(WebUtility.HtmlEncode(renderAsMermaid ? codeText.Trim() : codeText));
        sb.Append("</pre>");
    }


    private static string? GetCodeFenceLanguage(string fenceLine)
    {
        if (string.IsNullOrWhiteSpace(fenceLine))
            return null;

        string value = fenceLine.Length > 3 ? fenceLine[3..].Trim() : string.Empty;
        if (string.IsNullOrWhiteSpace(value))
            return null;

        string token = value
            .Split(new[] { ' ', '\t', '{', ':' }, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? string.Empty;

        token = token.Trim().Trim('`').ToLowerInvariant();
        return string.IsNullOrWhiteSpace(token) ? null : token;
    }

    private static bool IsMermaidCodeFenceLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return false;

        return string.Equals(language, "mermaid", StringComparison.OrdinalIgnoreCase)
            || string.Equals(language, "mmd", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeMermaidDefinition(string codeText)
    {
        if (string.IsNullOrWhiteSpace(codeText))
            return false;

        string firstLine = codeText
            .Replace("\r\n", "\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()?
            .Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(firstLine))
            return false;

        return MermaidFirstLineRegex.IsMatch(firstLine);
    }

    private static string FormatInline(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        string encoded = WebUtility.HtmlEncode(text);

        encoded = InlineBoldAsteriskRegex.Replace(encoded, "<strong>$1</strong>");
        encoded = InlineBoldUnderscoreRegex.Replace(encoded, "<strong>$1</strong>");
        encoded = InlineItalicAsteriskRegex.Replace(encoded, "<em>$1</em>");
        encoded = InlineItalicUnderscoreRegex.Replace(encoded, "<em>$1</em>");
        encoded = InlineCodeRegex.Replace(encoded, "<code>$1</code>");

        return encoded;
    }

}
