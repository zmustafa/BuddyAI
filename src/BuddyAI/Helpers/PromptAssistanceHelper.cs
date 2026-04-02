using System.Globalization;
using System.Text.RegularExpressions;
using BuddyAI.Models;
using BuddyAI.Services;

namespace BuddyAI.Helpers;

/// <summary>
/// Manages smart suggestions generation, template/snippet insertion logic,
/// global/conversation search, and prompt auto-complete state.
/// Extracted from AIQ to isolate prompt-assistance concerns.
/// </summary>
internal sealed class PromptAssistanceHelper
{
    private readonly List<SuggestionItem> _managedSuggestions;
    private readonly List<PromptItem> _templates;

    public PromptAssistanceHelper(List<SuggestionItem> managedSuggestions, List<PromptItem> templates)
    {
        _managedSuggestions = managedSuggestions;
        _templates = templates;
    }

    /// <summary>
    /// Generates context-aware prompt suggestions based on the current user input
    /// and the managed suggestion/template libraries.
    /// </summary>
    public IEnumerable<string> GetSmartSuggestions(string prompt)
    {
        List<string> suggestions = new();
        if (string.IsNullOrWhiteSpace(prompt))
        {
            suggestions.AddRange(_managedSuggestions.Select(x => x.Text).Take(6));
            suggestions.Add("Explain this error");
            suggestions.Add("Analyze this log");
            suggestions.Add("Generate a PowerShell script");
            suggestions.Add("Review this architecture");
            return suggestions.Distinct(StringComparer.OrdinalIgnoreCase).Take(10).ToList();
        }

        string lower = prompt.ToLowerInvariant();
        if (lower.Contains("error") || lower.Contains("exception") || lower.Contains("failed"))
            suggestions.Add("Analyze this error and propose the top 3 likely root causes.");
        if (lower.Contains("json") || lower.Contains("csv") || lower.Contains("xml"))
            suggestions.Add("Convert this content into clean structured JSON with validation notes.");
        if (lower.Contains("azure") || lower.Contains("aws") || lower.Contains("gcp"))
            suggestions.Add("Provide cloud-specific troubleshooting steps and the next verification commands.");
        if (lower.Contains("script") || lower.Contains("powershell") || lower.Contains("terraform"))
            suggestions.Add("Generate a production-minded script with comments and safe defaults.");
        if (lower.Contains("diagram") || lower.Contains("architecture"))
            suggestions.Add("Review this architecture for risks, bottlenecks, and next improvements.");

        foreach (SuggestionItem item in _managedSuggestions.Where(x =>
                     x.Name.Contains(prompt, StringComparison.OrdinalIgnoreCase) ||
                     x.Category.Contains(prompt, StringComparison.OrdinalIgnoreCase) ||
                     x.Text.Contains(prompt, StringComparison.OrdinalIgnoreCase)).Take(5))
        {
            suggestions.Add(item.Text);
        }

        foreach (SuggestionItem item in _managedSuggestions.Take(3))
            suggestions.Add(item.Text);

        foreach (PromptItem item in _templates.Take(3))
            suggestions.Add(item.Text);

        return suggestions.Distinct(StringComparer.OrdinalIgnoreCase).Take(10).ToList();
    }

    /// <summary>
    /// Rebuilds the auto-complete string collection from prompt history and templates.
    /// </summary>
    public void UpdateAutoComplete(AutoCompleteStringCollection target, IReadOnlyList<string> promptHistory)
    {
        target.Clear();
        foreach (string item in promptHistory.Distinct(StringComparer.OrdinalIgnoreCase).Take(30))
            target.Add(item);
        foreach (PromptItem item in _templates.Take(50))
            if (!target.Contains(item.Text))
                target.Add(item.Text);
    }

    /// <summary>
    /// Utility for normalizing a temperature string to a canonical format
    /// suitable for display and storage.
    /// </summary>
    public static string NormalizeTemperatureValue(string? value, string defaultTemperature = "1")
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
        {
            parsed = Math.Round(parsed, 1, MidpointRounding.AwayFromZero);
            parsed = Math.Max(0d, Math.Min(2d, parsed));

            if (Math.Abs(parsed - Math.Round(parsed)) < 0.0001d)
                return ((int)Math.Round(parsed)).ToString(CultureInfo.InvariantCulture);

            string formatted = parsed.ToString("0.0", CultureInfo.InvariantCulture);
            return formatted.StartsWith("0.", StringComparison.Ordinal) ? formatted.Substring(1) : formatted;
        }

        return defaultTemperature;
    }

    /// <summary>
    /// Finds the index of a value in a ComboBox (case-insensitive).
    /// </summary>
    public static int FindComboIndex(ComboBox combo, string value)
    {
        for (int i = 0; i < combo.Items.Count; i++)
        {
            if (string.Equals(Convert.ToString(combo.Items[i]), value, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    /// <summary>
    /// Gets the currently selected text from a ComboBox, handling both
    /// bound-selection and free-text scenarios.
    /// </summary>
    public static string GetComboSelectionText(ComboBox combo)
    {
        return combo.SelectedItem == null
            ? combo.Text.Trim()
            : Convert.ToString(combo.SelectedItem)?.Trim() ?? string.Empty;
    }

    /// <summary>
    /// Infers a provider name from a model identifier using known naming conventions.
    /// Falls back to the provider service's model registry first.
    /// </summary>
    public static string InferProvider(IReadOnlyList<AiProviderDefinition> providers, AiProviderService providerService, string? model)
    {
        AiProviderDefinition? configured = providerService.FindByModel(providers, model);
        if (configured != null)
            return configured.Name;

        string value = (model ?? string.Empty).Trim().ToLowerInvariant();
        if (value.StartsWith("claude"))
            return AiProviderTypes.Claude;
        if (value.StartsWith("grok"))
            return AiProviderTypes.Grok;
        if (value.StartsWith("gpt-") || value.StartsWith("o1") || value.StartsWith("o3") || value.StartsWith("o4"))
            return "OpenAI/Azure";
        return string.Empty;
    }
}
