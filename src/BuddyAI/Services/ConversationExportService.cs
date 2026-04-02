using System.Text;
using System.Text.Json;
using BuddyAI.Models;

namespace BuddyAI.Services;

public static class ConversationExportService
{
    public static void Save(ConversationExportModel model, string path)
    {
        string extension = Path.GetExtension(path).ToLowerInvariant();
        string content = extension switch
        {
            ".json" => JsonSerializer.Serialize(model, new JsonSerializerOptions { WriteIndented = true }),
            ".html" => BuildHtml(model),
            ".md" => BuildMarkdown(model),
            _ => BuildText(model)
        };

        File.WriteAllText(path, content, Encoding.UTF8);
    }

    public static string BuildMarkdown(ConversationExportModel model)
    {
        StringBuilder sb = new();
        sb.AppendLine($"# {model.Title}");
        sb.AppendLine();
        sb.AppendLine($"- Persona: {model.Persona}");
        sb.AppendLine($"- Model: {model.Model}");
        sb.AppendLine($"- Provider: {model.Provider}");
        sb.AppendLine($"- Estimated Tokens: {model.EstimatedTokens}");
        sb.AppendLine($"- Estimated Cost: ${model.EstimatedCostUsd:F4}");
        sb.AppendLine($"- Latency: {model.LatencySeconds:F2}s");
        sb.AppendLine($"- Created: {model.CreatedAt:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine("## System Prompt");
        sb.AppendLine();
        sb.AppendLine(model.SystemPrompt);
        sb.AppendLine();
        sb.AppendLine("## Prompt");
        sb.AppendLine();
        sb.AppendLine(model.Prompt);
        sb.AppendLine();
        sb.AppendLine("## Response");
        sb.AppendLine();
        sb.AppendLine(model.Response);
        return sb.ToString();
    }

    public static string BuildText(ConversationExportModel model)
    {
        StringBuilder sb = new();
        sb.AppendLine(model.Title);
        sb.AppendLine(new string('=', model.Title.Length));
        sb.AppendLine($"Persona: {model.Persona}");
        sb.AppendLine($"Model: {model.Model}");
        sb.AppendLine($"Provider: {model.Provider}");
        sb.AppendLine($"Estimated Tokens: {model.EstimatedTokens}");
        sb.AppendLine($"Estimated Cost: ${model.EstimatedCostUsd:F4}");
        sb.AppendLine($"Latency: {model.LatencySeconds:F2}s");
        sb.AppendLine();
        sb.AppendLine("SYSTEM PROMPT");
        sb.AppendLine(model.SystemPrompt);
        sb.AppendLine();
        sb.AppendLine("PROMPT");
        sb.AppendLine(model.Prompt);
        sb.AppendLine();
        sb.AppendLine("RESPONSE");
        sb.AppendLine(model.Response);
        return sb.ToString();
    }

    public static string BuildHtml(ConversationExportModel model)
    {
        string markdown = BuildMarkdown(model)
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");

        return $@"<!DOCTYPE html>
<html>
<head>
<meta charset=""utf-8"" />
<title>{System.Net.WebUtility.HtmlEncode(model.Title)}</title>
<style>
body {{ font-family: Segoe UI, Arial, sans-serif; margin: 24px; background: #f6f8fb; color: #15202b; }}
pre {{ white-space: pre-wrap; background: white; border: 1px solid #d9e2ec; border-radius: 10px; padding: 18px; }}
</style>
</head>
<body>
<pre>{markdown}</pre>
</body>
</html>";
    }
}
