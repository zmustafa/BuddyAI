using Microsoft.Web.WebView2.WinForms;
using BuddyAI.Models;

namespace BuddyAI;

internal sealed class ConversationTabState
{
    public const string DefaultTemperatureValue = "1";

    public TabPage TabPage { get; init; } = null!;
    public SplitContainer Split { get; init; } = null!;
    public PictureBox Picture { get; init; } = null!;
    public Label ImageInfoLabel { get; init; } = null!;
    public ToolStrip Tool { get; init; } = null!;
    public ToolStripButton CopyButton { get; init; } = null!;
    public ToolStripButton CopySummaryButton { get; init; } = null!;
    public ToolStripButton ClearButton { get; init; } = null!;
    public ToolStripButton ReRenderButton { get; init; } = null!;
    public ToolStripButton ExportButton { get; init; } = null!;
    public ToolStripButton WindowButton { get; init; } = null!;
    public ToolStripLabel MetaLabel { get; init; } = null!;
    public TabControl ResponseTabs { get; init; } = null!;
    public WebView2 WebView { get; init; } = null!;
    public TextBox RawTextBox { get; init; } = null!;
    public bool ResponseViewInitialized { get; set; }
    public bool ResponseViewInitializationAttempted { get; set; }
    public string RawResponseText { get; set; } = string.Empty;
    public string ResponseViewerStateJson { get; set; } = string.Empty;
    public ResponseAnalysis? LastAnalysis { get; set; }
    public string PreviousResponseId { get; set; } = string.Empty;
    public string? ImageMimeType { get; set; }
    public string? ImageName { get; set; }
    public byte[]? ImageBytes { get; set; }
    public string Title { get; set; } = "Conversation";
    public string Persona { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Temperature { get; set; } = DefaultTemperatureValue;
    public string ProviderId { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string SystemPrompt { get; set; } = string.Empty;
    public string LastPrompt { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime LastUpdatedAt { get; set; }
    public double LatencySeconds { get; set; }
    public int EstimatedTokens { get; set; }
    public decimal EstimatedCostUsd { get; set; }
    public bool IsPinned { get; set; }
    public bool HasError { get; set; }
    public bool IsPending { get; set; }
    public List<string> PromptHistory { get; } = new();
}

/// <summary>
/// Holds the analysis results produced from a raw AI response.
/// Extracted from AIQ.Rendering.cs to allow sharing across the codebase.
/// </summary>
internal sealed class ResponseAnalysis
{
    public string Summary { get; }
    public IReadOnlyList<string> HighConfidenceItems { get; }
    public IReadOnlyList<string> LowConfidenceItems { get; }
    public IReadOnlyList<string> Recommendations { get; }
    public IReadOnlyList<string> WarningNotes { get; }

    public ResponseAnalysis(
        string summary,
        IReadOnlyList<string> highConfidenceItems,
        IReadOnlyList<string> lowConfidenceItems,
        IReadOnlyList<string> recommendations,
        IReadOnlyList<string> warningNotes)
    {
        Summary = summary;
        HighConfidenceItems = highConfidenceItems;
        LowConfidenceItems = lowConfidenceItems;
        Recommendations = recommendations;
        WarningNotes = warningNotes;
    }

    public static ResponseAnalysis Empty { get; } = new(
        string.Empty,
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>());
}

/// <summary>
/// Represents an option in the prefilled question combo box.
/// </summary>
internal sealed class PrefilledQuestionOption
{
    public string Question { get; }
    public string SystemPrompt { get; }

    public PrefilledQuestionOption(string question, string systemPrompt)
    {
        Question = question;
        SystemPrompt = systemPrompt;
    }

    public override string ToString() => Question;
}

/// <summary>
/// Display wrapper for a prompt template in a ListBox.
/// </summary>
internal sealed class TemplateListEntry
{
    public PromptItem Item { get; }
    public TemplateListEntry(PromptItem item) => Item = item;
    public override string ToString() => $"[{Item.Category}] {Item.Name}";
}

/// <summary>
/// Display wrapper for a snippet in a ListBox.
/// </summary>
internal sealed class SnippetListEntry
{
    public SnippetItem Item { get; }
    public SnippetListEntry(SnippetItem item) => Item = item;
    public override string ToString() => $"[{Item.Category}] {Item.Name}";
}

/// <summary>
/// Represents a search match across conversations.
/// </summary>
internal sealed class SearchResultItem
{
    public ConversationTabState State { get; }
    public string Query { get; }

    public SearchResultItem(ConversationTabState state, string query)
    {
        State = state;
        Query = query;
    }

    public override string ToString() => State.Title + " — " + State.Persona;
}

/// <summary>
/// Tag attached to persona tree nodes.
/// </summary>
internal sealed class PersonaTreeTag
{
    public string? PersonaName { get; }
    public string Category { get; }

    public PersonaTreeTag(string? personaName, string category)
    {
        PersonaName = personaName;
        Category = category;
    }
}

/// <summary>
/// Defines a named workspace profile with persona, model, and theme defaults.
/// </summary>
internal sealed class WorkspaceProfile
{
    public string Name { get; }
    public string DefaultPersona { get; }
    public string DefaultModel { get; }
    public string PreferredTheme { get; }

    public WorkspaceProfile(string name, string defaultPersona, string defaultModel, string preferredTheme)
    {
        Name = name;
        DefaultPersona = defaultPersona;
        DefaultModel = defaultModel;
        PreferredTheme = preferredTheme;
    }

    public override string ToString() => Name;
}
