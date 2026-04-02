using BuddyAI.Docking;

namespace BuddyAI.Models;

public sealed class WorkspaceSettings
{
    public string Theme { get; set; } = "Visual Studio Dark";
    public string ActiveProfile { get; set; } = "Cloud Architect";
    public bool ShowPersonaPanel { get; set; } = true;
    public bool ShowToolsPanel { get; set; } = false;
    public bool ShowDiagnosticsPanel { get; set; } = true;
    public bool ClipboardSuggestionsEnabled { get; set; } = true;
    public int LeftSplitterDistance { get; set; } = 280;
    public int RightSplitterDistance { get; set; } = 940;
    public int BottomSplitterDistance { get; set; } = 630;
    public int WindowWidth { get; set; } = 1600;
    public int WindowHeight { get; set; } = 980;
    public bool AutoAskAfterSnipEnabled { get; set; }
    public bool GlobalShortcutSnipAskEnabled { get; set; }
    public int QuickResultWindowWidth { get; set; } = 760;
    public int QuickResultWindowHeight { get; set; } = 560;
    public double QuickResultWindowZoomFactor { get; set; } = 1d;
    public bool LockDockedWindows { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public DockLayoutState? DockLayout { get; set; }

    // Advanced settings
    public int MaxPromptHistory { get; set; } = 100;
    public int MaxConversationTabs { get; set; } = 50;
    public float EditorFontSize { get; set; } = 10F;
    public bool ConfirmBeforeClose { get; set; } = true;
    public int AutoSaveIntervalSeconds { get; set; } = 300;
    public bool ResponseWordWrap { get; set; }
    public string DefaultExportFormat { get; set; } = "Markdown";

    // Lens settings
    public bool LensEnabled { get; set; } = true;
    public int LensCaptureTimeout { get; set; } = 2;
    public bool LensAutoFocusTarget { get; set; } = true;
    public bool LensShowDiagnostics { get; set; }
    public bool LensClipboardFallback { get; set; } = true;

    // QuickInsight settings
    public bool QuickInsightEnabled { get; set; } = true;
    public bool QuickInsightTopMost { get; set; } = true;
    public bool QuickInsightAutoAsk { get; set; } = true;
    public int QuickInsightMaxTokens { get; set; } = 2048;
    public bool QuickInsightShowInTaskbar { get; set; }
}
