using BuddyAI.Models;
using BuddyAI.Services;

namespace BuddyAI.Forms;

public sealed class SettingsForm : Form
{
    private readonly TreeView _navTree = new();
    private readonly Panel _contentPanel = new();
    private readonly SplitContainer _split = new();
    private readonly Label _pageTitle = new();
    private readonly Button _btnClose = new();

    private readonly Panel _generalPage;
    private readonly Panel _advancedPage;
    private readonly SettingsProviderPage _providerPage;
    private readonly SettingsPersonaPage _personaPage;

    private readonly WorkspaceSettingsService _workspaceSettingsService;
    private readonly WorkspaceSettings _workspaceSettings;
    private readonly Action<string> _applyThemeCallback;

    private readonly ComboBox _cmbTheme = new();
    private readonly CheckBox _chkClipboardSuggestions = new();
    private readonly CheckBox _chkStartWithWindows = new();

    // Advanced page controls
    private readonly NumericUpDown _nudMaxPromptHistory = new();
    private readonly NumericUpDown _nudMaxConversationTabs = new();
    private readonly NumericUpDown _nudEditorFontSize = new();
    private readonly CheckBox _chkConfirmBeforeClose = new();
    private readonly NumericUpDown _nudAutoSaveInterval = new();
    private readonly CheckBox _chkResponseWordWrap = new();
    private readonly ComboBox _cmbDefaultExportFormat = new();

    // Lens page controls
    private readonly CheckBox _chkLensEnabled = new();
    private readonly NumericUpDown _nudLensCaptureTimeout = new();
    private readonly CheckBox _chkLensAutoFocusTarget = new();
    private readonly CheckBox _chkLensShowDiagnostics = new();
    private readonly CheckBox _chkLensClipboardFallback = new();

    // QuickInsight page controls
    private readonly CheckBox _chkQuickInsightEnabled = new();
    private readonly CheckBox _chkQuickInsightTopMost = new();
    private readonly CheckBox _chkQuickInsightAutoAsk = new();
    private readonly NumericUpDown _nudQuickInsightMaxTokens = new();
    private readonly CheckBox _chkQuickInsightShowInTaskbar = new();

    private string _selectedPageKey = "General";

    public bool ProvidersChanged => _providerPage.IsDirty is false && _providerPageEverSaved;
    public bool PersonasChanged => _personaPage.IsDirty is false && _personaPageEverSaved;

    private bool _providerPageEverSaved;
    private bool _personaPageEverSaved;

    public SettingsForm(
        AiProviderService providerService,
        PersonaService personaService,
        WorkspaceSettingsService workspaceSettingsService,
        WorkspaceSettings workspaceSettings,
        Action<string> applyThemeCallback,
        string? initialPage = null)
    {
        _workspaceSettingsService = workspaceSettingsService;
        _workspaceSettings = workspaceSettings;
        _applyThemeCallback = applyThemeCallback;

        Text = "Settings";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1280, 780);
        Size = new Size(1500, 940);
        AutoScaleMode = AutoScaleMode.Dpi;
        KeyPreview = true;
        ShowInTaskbar = false;

        _providerPage = new SettingsProviderPage(providerService);
        _personaPage = new SettingsPersonaPage(personaService, providerService);
        _generalPage = BuildGeneralPage();
        _advancedPage = BuildAdvancedPage();

        _providerPage.DirtyChanged += (_, _) =>
        {
            if (!_providerPage.IsDirty)
                _providerPageEverSaved = true;
        };

        _personaPage.DirtyChanged += (_, _) =>
        {
            if (!_personaPage.IsDirty)
                _personaPageEverSaved = true;
        };

        BuildUi();
        WireEvents();

        if (!string.IsNullOrWhiteSpace(initialPage))
            NavigateToPage(initialPage);
        else
            NavigateToPage("General");
    }

    private void BuildUi()
    {
        _split.Dock = DockStyle.Fill;
        _split.FixedPanel = FixedPanel.Panel1;
        _split.SplitterWidth = 1;

        // --- Left nav panel ---
        Panel navPanel = new() { Dock = DockStyle.Fill, Padding = new Padding(0) };

        Label navHeader = new()
        {
            Text = "Settings",
            Dock = DockStyle.Top,
            Height = 44,
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(14, 0, 0, 0)
        };

        _navTree.Dock = DockStyle.Fill;
        _navTree.HideSelection = false;
        _navTree.FullRowSelect = true;
        _navTree.ShowLines = false;
        _navTree.ShowPlusMinus = true;
        _navTree.ShowRootLines = false;
        _navTree.ItemHeight = 32;
        _navTree.Font = new Font("Segoe UI", 11F);
        _navTree.Indent = 24;
        _navTree.BorderStyle = BorderStyle.None;

        TreeNode generalNode = new("  General") { Tag = "General", ImageIndex = -1 };
        TreeNode environmentNode = new("  Environment") { Tag = "Environment" };
        TreeNode advancedNode = new("  Advanced") { Tag = "Advanced" };
        TreeNode providersNode = new("  AI Providers") { Tag = "Providers" };
        TreeNode personasNode = new("  Personas") { Tag = "Personas" };

        _navTree.Nodes.Add(generalNode);
        _navTree.Nodes.Add(environmentNode);
        _navTree.Nodes.Add(advancedNode);
        _navTree.Nodes.Add(providersNode);
        _navTree.Nodes.Add(personasNode);

        navPanel.Controls.Add(_navTree);
        navPanel.Controls.Add(navHeader);
        _split.Panel1.Controls.Add(navPanel);

        // --- Right content panel ---
        Panel rightPanel = new() { Dock = DockStyle.Fill, Padding = new Padding(0) };

        _pageTitle.Dock = DockStyle.Top;
        _pageTitle.Height = 44;
        _pageTitle.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
        _pageTitle.TextAlign = ContentAlignment.MiddleLeft;
        _pageTitle.Padding = new Padding(14, 0, 0, 0);

        Panel separator = new()
        {
            Dock = DockStyle.Top,
            Height = 1,
            BackColor = SystemColors.ControlDark
        };

        _contentPanel.Dock = DockStyle.Fill;
        _contentPanel.Padding = new Padding(0);

        Panel bottomBar = new()
        {
            Dock = DockStyle.Bottom,
            Height = 50,
            Padding = new Padding(0, 8, 14, 8)
        };

        _btnClose.Text = "Close";
        _btnClose.Width = 100;
        _btnClose.Height = 34;
        _btnClose.Dock = DockStyle.Right;

        bottomBar.Controls.Add(_btnClose);

        rightPanel.Controls.Add(_contentPanel);
        rightPanel.Controls.Add(separator);
        rightPanel.Controls.Add(_pageTitle);
        rightPanel.Controls.Add(bottomBar);
        _split.Panel2.Controls.Add(rightPanel);

        Controls.Add(_split);

        // Set initial splitter distance after layout
        Load += (_, _) =>
        {
            _split.SplitterDistance = 240;
            _navTree.SelectedNode = _navTree.Nodes[0];
        };
    }

    private Panel BuildGeneralPage()
    {
        Panel page = new() { Dock = DockStyle.Fill, Padding = new Padding(24, 16, 24, 16) };

        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 6,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // Section: Appearance
        Label lblAppearanceSection = new()
        {
            Text = "Appearance",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft,
            Padding = new Padding(0, 0, 0, 4)
        };
        layout.Controls.Add(lblAppearanceSection, 0, 0);
        layout.SetColumnSpan(lblAppearanceSection, 2);

        layout.Controls.Add(new Label
        {
            Text = "Color Theme",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 10.5F)
        }, 0, 1);

        _cmbTheme.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbTheme.Width = 260;
        _cmbTheme.Font = new Font("Segoe UI", 10.5F);
        _cmbTheme.Items.AddRange(new object[] { "Light", "Dark", "Visual Studio Dark", "Azure Blue", "High Contrast" });
        _cmbTheme.SelectedItem = _workspaceSettings.Theme;
        _cmbTheme.Anchor = AnchorStyles.Left;
        layout.Controls.Add(_cmbTheme, 1, 1);

        // Spacer row 2

        // Section: Behavior
        Label lblBehaviorSection = new()
        {
            Text = "Behavior",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft,
            Padding = new Padding(0, 0, 0, 4)
        };
        layout.Controls.Add(lblBehaviorSection, 0, 3);
        layout.SetColumnSpan(lblBehaviorSection, 2);

        _chkClipboardSuggestions.Text = "Enable clipboard monitoring for diagnostic suggestions";
        _chkClipboardSuggestions.AutoSize = true;
        _chkClipboardSuggestions.Checked = _workspaceSettings.ClipboardSuggestionsEnabled;
        _chkClipboardSuggestions.Font = new Font("Segoe UI", 10.5F);
        _chkClipboardSuggestions.Margin = new Padding(0, 4, 0, 0);
        layout.Controls.Add(_chkClipboardSuggestions, 0, 4);
        layout.SetColumnSpan(_chkClipboardSuggestions, 2);

        _chkStartWithWindows.Text = "Start BuddyAI with Windows";
        _chkStartWithWindows.AutoSize = true;
        _chkStartWithWindows.Checked = _workspaceSettings.StartWithWindows;
        _chkStartWithWindows.Font = new Font("Segoe UI", 10.5F);
        _chkStartWithWindows.Margin = new Padding(0, 4, 0, 0);
        layout.Controls.Add(_chkStartWithWindows, 0, 5);
        layout.SetColumnSpan(_chkStartWithWindows, 2);

        page.Controls.Add(layout);
        return page;
    }

    private Panel BuildAdvancedPage()
    {
        Panel page = new() { Dock = DockStyle.Fill, Padding = new Padding(24, 16, 24, 16), AutoScroll = true };

        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 26,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        // Row 0: Section header "Limits"
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        // Row 1: Max Prompt History
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        // Row 2: Max Conversation Tabs
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        // Row 3: Spacer
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 16));
        // Row 4: Section header "Editor"
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        // Row 5: Editor Font Size
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        // Row 6: Response Word Wrap
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        // Row 7: Default Export Format
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        // Row 8: Spacer
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 16));
        // Row 9: Section header "Session"
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        // Row 10: Confirm Before Close
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        // Row 11: Auto-Save Interval
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        // Row 12: Spacer
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 16));
        // Row 13: Section header "Lens"
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        // Row 14: Lens Enabled
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        // Row 15: Lens Capture Timeout
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        // Row 16: Lens Auto Focus Target
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        // Row 17: Lens Show Diagnostics
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        // Row 18: Lens Clipboard Fallback
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        // Row 19: Spacer
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 16));
        // Row 20: Section header "QuickInsight"
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        // Row 21: QuickInsight Enabled
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        // Row 22: QuickInsight TopMost
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        // Row 23: QuickInsight Auto Ask
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        // Row 24: QuickInsight Max Tokens
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        // Row 25: QuickInsight Show In Taskbar
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

        int row = 0;

        // Section: Limits
        Label lblLimitsSection = new()
        {
            Text = "Limits",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft,
            Padding = new Padding(0, 0, 0, 4)
        };
        layout.Controls.Add(lblLimitsSection, 0, row);
        layout.SetColumnSpan(lblLimitsSection, 2);
        row++;

        layout.Controls.Add(new Label
        {
            Text = "Max Prompt History",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 10.5F)
        }, 0, row);
        _nudMaxPromptHistory.Minimum = 10;
        _nudMaxPromptHistory.Maximum = 500;
        _nudMaxPromptHistory.Value = Math.Clamp(_workspaceSettings.MaxPromptHistory, 10, 500);
        _nudMaxPromptHistory.Width = 120;
        _nudMaxPromptHistory.Font = new Font("Segoe UI", 10.5F);
        layout.Controls.Add(_nudMaxPromptHistory, 1, row);
        row++;

        layout.Controls.Add(new Label
        {
            Text = "Max Conversation Tabs",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 10.5F)
        }, 0, row);
        _nudMaxConversationTabs.Minimum = 1;
        _nudMaxConversationTabs.Maximum = 100;
        _nudMaxConversationTabs.Value = Math.Clamp(_workspaceSettings.MaxConversationTabs, 1, 100);
        _nudMaxConversationTabs.Width = 120;
        _nudMaxConversationTabs.Font = new Font("Segoe UI", 10.5F);
        layout.Controls.Add(_nudMaxConversationTabs, 1, row);
        row++;

        // Spacer
        row++;

        // Section: Editor
        Label lblEditorSection = new()
        {
            Text = "Editor",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft,
            Padding = new Padding(0, 0, 0, 4)
        };
        layout.Controls.Add(lblEditorSection, 0, row);
        layout.SetColumnSpan(lblEditorSection, 2);
        row++;

        layout.Controls.Add(new Label
        {
            Text = "Editor Font Size",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 10.5F)
        }, 0, row);
        _nudEditorFontSize.Minimum = 8;
        _nudEditorFontSize.Maximum = 24;
        _nudEditorFontSize.Value = (decimal)Math.Clamp(_workspaceSettings.EditorFontSize, 8F, 24F);
        _nudEditorFontSize.Width = 120;
        _nudEditorFontSize.Font = new Font("Segoe UI", 10.5F);
        layout.Controls.Add(_nudEditorFontSize, 1, row);
        row++;

        _chkResponseWordWrap.Text = "Enable word wrap in raw response view";
        _chkResponseWordWrap.AutoSize = true;
        _chkResponseWordWrap.Checked = _workspaceSettings.ResponseWordWrap;
        _chkResponseWordWrap.Font = new Font("Segoe UI", 10.5F);
        _chkResponseWordWrap.Margin = new Padding(0, 4, 0, 0);
        layout.Controls.Add(_chkResponseWordWrap, 0, row);
        layout.SetColumnSpan(_chkResponseWordWrap, 2);
        row++;

        layout.Controls.Add(new Label
        {
            Text = "Default Export Format",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 10.5F)
        }, 0, row);
        _cmbDefaultExportFormat.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbDefaultExportFormat.Width = 260;
        _cmbDefaultExportFormat.Font = new Font("Segoe UI", 10.5F);
        _cmbDefaultExportFormat.Items.AddRange(new object[] { "Markdown", "HTML", "JSON", "Text" });
        _cmbDefaultExportFormat.SelectedItem = _workspaceSettings.DefaultExportFormat;
        if (_cmbDefaultExportFormat.SelectedIndex < 0)
            _cmbDefaultExportFormat.SelectedIndex = 0;
        _cmbDefaultExportFormat.Anchor = AnchorStyles.Left;
        layout.Controls.Add(_cmbDefaultExportFormat, 1, row);
        row++;

        // Spacer
        row++;

        // Section: Session
        Label lblSessionSection = new()
        {
            Text = "Session",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft,
            Padding = new Padding(0, 0, 0, 4)
        };
        layout.Controls.Add(lblSessionSection, 0, row);
        layout.SetColumnSpan(lblSessionSection, 2);
        row++;

        _chkConfirmBeforeClose.Text = "Confirm before closing the application";
        _chkConfirmBeforeClose.AutoSize = true;
        _chkConfirmBeforeClose.Checked = _workspaceSettings.ConfirmBeforeClose;
        _chkConfirmBeforeClose.Font = new Font("Segoe UI", 10.5F);
        _chkConfirmBeforeClose.Margin = new Padding(0, 4, 0, 0);
        layout.Controls.Add(_chkConfirmBeforeClose, 0, row);
        layout.SetColumnSpan(_chkConfirmBeforeClose, 2);
        row++;

        layout.Controls.Add(new Label
        {
            Text = "Auto-save Interval (minutes)",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 10.5F)
        }, 0, row);
        _nudAutoSaveInterval.Minimum = 1;
        _nudAutoSaveInterval.Maximum = 60;
        _nudAutoSaveInterval.Value = Math.Clamp(_workspaceSettings.AutoSaveIntervalSeconds / 60, 1, 60);
        _nudAutoSaveInterval.Width = 120;
        _nudAutoSaveInterval.Font = new Font("Segoe UI", 10.5F);
        layout.Controls.Add(_nudAutoSaveInterval, 1, row);
        row++;

        // Spacer
        row++;

        // Section: Lens
        Label lblLensSection = new()
        {
            Text = "Lens",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft,
            Padding = new Padding(0, 0, 0, 4)
        };
        layout.Controls.Add(lblLensSection, 0, row);
        layout.SetColumnSpan(lblLensSection, 2);
        row++;

        _chkLensEnabled.Text = "Enable Lens (experimental)";
        _chkLensEnabled.AutoSize = true;
        _chkLensEnabled.Checked = _workspaceSettings.LensEnabled;
        _chkLensEnabled.Font = new Font("Segoe UI", 10.5F);
        _chkLensEnabled.Margin = new Padding(0, 4, 0, 0);
        layout.Controls.Add(_chkLensEnabled, 0, row);
        layout.SetColumnSpan(_chkLensEnabled, 2);
        row++;

        layout.Controls.Add(new Label
        {
            Text = "Capture Timeout (seconds)",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 10.5F)
        }, 0, row);
        _nudLensCaptureTimeout.Minimum = 1;
        _nudLensCaptureTimeout.Maximum = 120;
        _nudLensCaptureTimeout.Value = Math.Clamp(_workspaceSettings.LensCaptureTimeout, 1, 120);
        _nudLensCaptureTimeout.Width = 120;
        _nudLensCaptureTimeout.Font = new Font("Segoe UI", 10.5F);
        layout.Controls.Add(_nudLensCaptureTimeout, 1, row);
        row++;

        _chkLensAutoFocusTarget.Text = "Auto-focus on main target";
        _chkLensAutoFocusTarget.AutoSize = true;
        _chkLensAutoFocusTarget.Checked = _workspaceSettings.LensAutoFocusTarget;
        _chkLensAutoFocusTarget.Font = new Font("Segoe UI", 10.5F);
        _chkLensAutoFocusTarget.Margin = new Padding(0, 4, 0, 0);
        layout.Controls.Add(_chkLensAutoFocusTarget, 0, row);
        layout.SetColumnSpan(_chkLensAutoFocusTarget, 2);
        row++;

        _chkLensShowDiagnostics.Text = "Show diagnostics in Lens";
        _chkLensShowDiagnostics.AutoSize = true;
        _chkLensShowDiagnostics.Checked = _workspaceSettings.LensShowDiagnostics;
        _chkLensShowDiagnostics.Font = new Font("Segoe UI", 10.5F);
        _chkLensShowDiagnostics.Margin = new Padding(0, 4, 0, 0);
        layout.Controls.Add(_chkLensShowDiagnostics, 0, row);
        layout.SetColumnSpan(_chkLensShowDiagnostics, 2);
        row++;

        _chkLensClipboardFallback.Text = "Use clipboard for Lens input (if supported)";
        _chkLensClipboardFallback.AutoSize = true;
        _chkLensClipboardFallback.Checked = _workspaceSettings.LensClipboardFallback;
        _chkLensClipboardFallback.Font = new Font("Segoe UI", 10.5F);
        _chkLensClipboardFallback.Margin = new Padding(0, 4, 0, 0);
        layout.Controls.Add(_chkLensClipboardFallback, 0, row);
        layout.SetColumnSpan(_chkLensClipboardFallback, 2);
        row++;

        // Spacer
        row++;

        // Section: QuickInsight
        Label lblQuickInsightSection = new()
        {
            Text = "QuickInsight",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft,
            Padding = new Padding(0, 0, 0, 4)
        };
        layout.Controls.Add(lblQuickInsightSection, 0, row);
        layout.SetColumnSpan(lblQuickInsightSection, 2);
        row++;

        _chkQuickInsightEnabled.Text = "Enable QuickInsight (experimental)";
        _chkQuickInsightEnabled.AutoSize = true;
        _chkQuickInsightEnabled.Checked = _workspaceSettings.QuickInsightEnabled;
        _chkQuickInsightEnabled.Font = new Font("Segoe UI", 10.5F);
        _chkQuickInsightEnabled.Margin = new Padding(0, 4, 0, 0);
        layout.Controls.Add(_chkQuickInsightEnabled, 0, row);
        layout.SetColumnSpan(_chkQuickInsightEnabled, 2);
        row++;

        _chkQuickInsightTopMost.Text = "Show QuickInsight window always on top";
        _chkQuickInsightTopMost.AutoSize = true;
        _chkQuickInsightTopMost.Checked = _workspaceSettings.QuickInsightTopMost;
        _chkQuickInsightTopMost.Font = new Font("Segoe UI", 10.5F);
        _chkQuickInsightTopMost.Margin = new Padding(0, 4, 0, 0);
        layout.Controls.Add(_chkQuickInsightTopMost, 0, row);
        layout.SetColumnSpan(_chkQuickInsightTopMost, 2);
        row++;

        _chkQuickInsightAutoAsk.Text = "Auto-ask for QuickInsight on new topics";
        _chkQuickInsightAutoAsk.AutoSize = true;
        _chkQuickInsightAutoAsk.Checked = _workspaceSettings.QuickInsightAutoAsk;
        _chkQuickInsightAutoAsk.Font = new Font("Segoe UI", 10.5F);
        _chkQuickInsightAutoAsk.Margin = new Padding(0, 4, 0, 0);
        layout.Controls.Add(_chkQuickInsightAutoAsk, 0, row);
        layout.SetColumnSpan(_chkQuickInsightAutoAsk, 2);
        row++;

        layout.Controls.Add(new Label
        {
            Text = "Max Tokens for QuickInsight",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 10.5F)
        }, 0, row);
        _nudQuickInsightMaxTokens.Minimum = 256;
        _nudQuickInsightMaxTokens.Maximum = 16384;
        _nudQuickInsightMaxTokens.Increment = 256;
        _nudQuickInsightMaxTokens.Value = Math.Clamp(_workspaceSettings.QuickInsightMaxTokens, 256, 16384);
        _nudQuickInsightMaxTokens.Width = 120;
        _nudQuickInsightMaxTokens.Font = new Font("Segoe UI", 10.5F);
        layout.Controls.Add(_nudQuickInsightMaxTokens, 1, row);
        row++;

        _chkQuickInsightShowInTaskbar.Text = "Show QuickInsight in Windows taskbar";
        _chkQuickInsightShowInTaskbar.AutoSize = true;
        _chkQuickInsightShowInTaskbar.Checked = _workspaceSettings.QuickInsightShowInTaskbar;
        _chkQuickInsightShowInTaskbar.Font = new Font("Segoe UI", 10.5F);
        _chkQuickInsightShowInTaskbar.Margin = new Padding(0, 4, 0, 0);
        layout.Controls.Add(_chkQuickInsightShowInTaskbar, 0, row);
        layout.SetColumnSpan(_chkQuickInsightShowInTaskbar, 2);
        row++;

        page.Controls.Add(layout);
        return page;
    }

    private void WireEvents()
    {
        _navTree.AfterSelect += (_, e) =>
        {
            if (e.Node?.Tag is string pageKey)
                NavigateToPage(pageKey);
        };

        _btnClose.Click += (_, _) => Close();

        _cmbTheme.SelectedIndexChanged += (_, _) =>
        {
            if (_cmbTheme.SelectedItem is string theme)
            {
                _workspaceSettings.Theme = theme;
                _workspaceSettingsService.Save(_workspaceSettings);
                _applyThemeCallback(theme);
            }
        };

        _chkClipboardSuggestions.CheckedChanged += (_, _) =>
        {
            _workspaceSettings.ClipboardSuggestionsEnabled = _chkClipboardSuggestions.Checked;
            _workspaceSettingsService.Save(_workspaceSettings);
        };

        _chkStartWithWindows.CheckedChanged += (_, _) =>
        {
            _workspaceSettings.StartWithWindows = _chkStartWithWindows.Checked;
            _workspaceSettingsService.Save(_workspaceSettings);
            Helpers.StartupHelper.SetStartWithWindows(_chkStartWithWindows.Checked);
        };

        // Advanced settings events
        _nudMaxPromptHistory.ValueChanged += (_, _) =>
        {
            _workspaceSettings.MaxPromptHistory = (int)_nudMaxPromptHistory.Value;
            _workspaceSettingsService.Save(_workspaceSettings);
        };

        _nudMaxConversationTabs.ValueChanged += (_, _) =>
        {
            _workspaceSettings.MaxConversationTabs = (int)_nudMaxConversationTabs.Value;
            _workspaceSettingsService.Save(_workspaceSettings);
        };

        _nudEditorFontSize.ValueChanged += (_, _) =>
        {
            _workspaceSettings.EditorFontSize = (float)_nudEditorFontSize.Value;
            _workspaceSettingsService.Save(_workspaceSettings);
        };

        _chkConfirmBeforeClose.CheckedChanged += (_, _) =>
        {
            _workspaceSettings.ConfirmBeforeClose = _chkConfirmBeforeClose.Checked;
            _workspaceSettingsService.Save(_workspaceSettings);
        };

        _nudAutoSaveInterval.ValueChanged += (_, _) =>
        {
            _workspaceSettings.AutoSaveIntervalSeconds = (int)_nudAutoSaveInterval.Value * 60;
            _workspaceSettingsService.Save(_workspaceSettings);
        };

        _chkResponseWordWrap.CheckedChanged += (_, _) =>
        {
            _workspaceSettings.ResponseWordWrap = _chkResponseWordWrap.Checked;
            _workspaceSettingsService.Save(_workspaceSettings);
        };

        _cmbDefaultExportFormat.SelectedIndexChanged += (_, _) =>
        {
            if (_cmbDefaultExportFormat.SelectedItem is string format)
            {
                _workspaceSettings.DefaultExportFormat = format;
                _workspaceSettingsService.Save(_workspaceSettings);
            }
        };

        // Lens settings events
        _chkLensEnabled.CheckedChanged += (_, _) =>
        {
            _workspaceSettings.LensEnabled = _chkLensEnabled.Checked;
            _workspaceSettingsService.Save(_workspaceSettings);
        };

        _nudLensCaptureTimeout.ValueChanged += (_, _) =>
        {
            _workspaceSettings.LensCaptureTimeout = (int)_nudLensCaptureTimeout.Value;
            _workspaceSettingsService.Save(_workspaceSettings);
        };

        _chkLensAutoFocusTarget.CheckedChanged += (_, _) =>
        {
            _workspaceSettings.LensAutoFocusTarget = _chkLensAutoFocusTarget.Checked;
            _workspaceSettingsService.Save(_workspaceSettings);
        };

        _chkLensShowDiagnostics.CheckedChanged += (_, _) =>
        {
            _workspaceSettings.LensShowDiagnostics = _chkLensShowDiagnostics.Checked;
            _workspaceSettingsService.Save(_workspaceSettings);
        };

        _chkLensClipboardFallback.CheckedChanged += (_, _) =>
        {
            _workspaceSettings.LensClipboardFallback = _chkLensClipboardFallback.Checked;
            _workspaceSettingsService.Save(_workspaceSettings);
        };

        // QuickInsight settings events
        _chkQuickInsightEnabled.CheckedChanged += (_, _) =>
        {
            _workspaceSettings.QuickInsightEnabled = _chkQuickInsightEnabled.Checked;
            _workspaceSettingsService.Save(_workspaceSettings);
        };

        _chkQuickInsightTopMost.CheckedChanged += (_, _) =>
        {
            _workspaceSettings.QuickInsightTopMost = _chkQuickInsightTopMost.Checked;
            _workspaceSettingsService.Save(_workspaceSettings);
        };

        _chkQuickInsightAutoAsk.CheckedChanged += (_, _) =>
        {
            _workspaceSettings.QuickInsightAutoAsk = _chkQuickInsightAutoAsk.Checked;
            _workspaceSettingsService.Save(_workspaceSettings);
        };

        _nudQuickInsightMaxTokens.ValueChanged += (_, _) =>
        {
            _workspaceSettings.QuickInsightMaxTokens = (int)_nudQuickInsightMaxTokens.Value;
            _workspaceSettingsService.Save(_workspaceSettings);
        };

        _chkQuickInsightShowInTaskbar.CheckedChanged += (_, _) =>
        {
            _workspaceSettings.QuickInsightShowInTaskbar = _chkQuickInsightShowInTaskbar.Checked;
            _workspaceSettingsService.Save(_workspaceSettings);
        };

        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        };

        FormClosing += OnFormClosing;
    }

    private void NavigateToPage(string pageKey)
    {
        _selectedPageKey = pageKey;
        _contentPanel.SuspendLayout();
        _contentPanel.Controls.Clear();

        switch (pageKey)
        {
            case "General":
            case "Environment":
                _pageTitle.Text = "General";
                _generalPage.Dock = DockStyle.Fill;
                _contentPanel.Controls.Add(_generalPage);
                break;

            case "Advanced":
                _pageTitle.Text = "Advanced";
                _advancedPage.Dock = DockStyle.Fill;
                _contentPanel.Controls.Add(_advancedPage);
                break;

            case "Providers":
                _pageTitle.Text = "AI Providers";
                _providerPage.Dock = DockStyle.Fill;
                _contentPanel.Controls.Add(_providerPage);
                break;

            case "Personas":
                _pageTitle.Text = "Personas";
                _personaPage.Dock = DockStyle.Fill;
                _contentPanel.Controls.Add(_personaPage);
                break;

            default:
                _pageTitle.Text = "General";
                _generalPage.Dock = DockStyle.Fill;
                _contentPanel.Controls.Add(_generalPage);
                break;
        }

        _contentPanel.ResumeLayout(true);

        // Sync tree selection
        TreeNode? target = FindNodeByTag(_navTree.Nodes, pageKey);
        if (target != null && _navTree.SelectedNode != target)
            _navTree.SelectedNode = target;
    }

    private static TreeNode? FindNodeByTag(TreeNodeCollection nodes, string tag)
    {
        foreach (TreeNode node in nodes)
        {
            if (node.Tag is string t && string.Equals(t, tag, StringComparison.Ordinal))
                return node;

            TreeNode? child = FindNodeByTag(node.Nodes, tag);
            if (child != null)
                return child;
        }

        return null;
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_providerPage.IsDirty)
        {
            if (!_providerPage.PromptSaveIfDirty())
            {
                e.Cancel = true;
                return;
            }
        }

        if (_personaPage.IsDirty)
        {
            if (!_personaPage.PromptSaveIfDirty())
            {
                e.Cancel = true;
                return;
            }
        }

        DialogResult = (_providerPageEverSaved || _personaPageEverSaved)
            ? DialogResult.OK
            : DialogResult.Cancel;
    }
}
