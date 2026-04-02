using BuddyAI.Docking;
using BuddyAI.Forms;
using BuddyAI.Helpers;
using BuddyAI.Models;
using BuddyAI.Services;
using ThemeService = BuddyAI.Services.ThemeService;

namespace BuddyAI;

public sealed partial class AIQ
{
    private void BuildProfiles()
    {
        _profiles["Support Engineer"] = new WorkspaceProfile("Support Engineer", "Cloud Support Engineer", "gpt-4.1-mini-637176", "Visual Studio Dark");
        _profiles["Cloud Architect"] = new WorkspaceProfile("Cloud Architect", "Architecture Reviewer", "gpt-4.1-mini-637176", "Azure Blue");
        _profiles["Security Analyst"] = new WorkspaceProfile("Security Analyst", "Security Analyst", "gpt-4.1-260074", "Dark");
        _profiles["Developer"] = new WorkspaceProfile("Developer", "Software Engineer", "gpt-5.3-codex", "Visual Studio Dark");
    }

    private void BuildUi()
    {
        SuspendLayout();

        _root.Dock = DockStyle.Fill;
        _root.ColumnCount = 1;
        _root.RowCount = 3;
        _root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // menu
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); // main content
        _root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // status strip
        Controls.Add(_root);

        BuildMenu();
        BuildShell();
        BuildStatusBar();

        _root.Controls.Add(_menuStrip, 0, 0);
        _root.Controls.Add(_dockHost, 0, 1);
        _root.Controls.Add(_statusStrip, 0, 2);

        MainMenuStrip = _menuStrip;

        ResumeLayout(true);
    }

    private void BuildMenu()
    {
        // —— File —__
        ToolStripMenuItem file = new("&File");
        file.DropDownItems.Add(CreateMenuItem("&New Chat", Keys.Control | Keys.T, (_, __) => CreateDraftConversation()));
        file.DropDownItems.Add(CreateMenuItem("New &Fresh Session", Keys.Control | Keys.Shift | Keys.N, (_, __) => NewFreshSession()));
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add(CreateMenuItem("&Save Session", Keys.Control | Keys.Shift | Keys.S, (_, __) => SaveSessionNow()));
        file.DropDownItems.Add(new ToolStripSeparator());
        
        ToolStripMenuItem importExport = new("&Import / Export");
        importExport.DropDownItems.Add(CreateMenuItem("Import &Personas...", (_, __) => ImportPersonas()));
        importExport.DropDownItems.Add(CreateMenuItem("Export P&ersonas...", (_, __) => ExportPersonas()));
        importExport.DropDownItems.Add(new ToolStripSeparator());
        importExport.DropDownItems.Add(CreateMenuItem("Import all settings...", (_, __) => ImportAllSettings()));
        importExport.DropDownItems.Add(CreateMenuItem("Export all settings...", (_, __) => ExportAllSettings()));
        importExport.DropDownItems.Add(new ToolStripSeparator());
        importExport.DropDownItems.Add(CreateMenuItem("Export &Conversation...", Keys.Control | Keys.E, (_, __) => ExportActiveConversation()));
        file.DropDownItems.Add(importExport);
        
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add(CreateMenuItem("E&xit", "Alt+F4", (_, __) => Close()));

        // —— Edit —__
        ToolStripMenuItem edit = new("&Edit");
        edit.DropDownItems.Add(CreateMenuItem("&Send Prompt", "Ctrl+Enter", async (_, __) => await AskAsync(null, false)));
        edit.DropDownItems.Add(CreateMenuItem("Send &Follow Up", "Ctrl+Shift+Enter", async (_, __) => await AskAsync(GetActiveConversation()?.PreviousResponseId, true)));
        edit.DropDownItems.Add(CreateMenuItem("&Cancel Request", "Esc", (_, __) => CancelActiveRequest()));
        edit.DropDownItems.Add(new ToolStripSeparator());
        edit.DropDownItems.Add(CreateMenuItem("Copy &Response", Keys.Control | Keys.Shift | Keys.C, (_, __) => CopyFullResponse()));
        edit.DropDownItems.Add(CreateMenuItem("Copy Su&mmary", (_, __) => CopySummary()));
        edit.DropDownItems.Add(CreateMenuItem("Copy Code &Block", (_, __) => CopyFirstCodeBlock()));
        edit.DropDownItems.Add(CreateMenuItem("C&lear Response", Keys.Control | Keys.L, (_, __) => ClearResponse()));
        edit.DropDownItems.Add(new ToolStripSeparator());
        edit.DropDownItems.Add(CreateMenuItem("&Paste Image from Clipboard", "Ctrl+Shift+V", (_, __) => PasteImageFromClipboard()));
        edit.DropDownItems.Add(CreateMenuItem("Anal&yze Clipboard", (_, __) => AnalyzeClipboardIntoPrompt()));

        // —— View —__
        ToolStripMenuItem view = new("&View");
        ToolStripMenuItem panelsMenu = new("&Panels");
        _mnuViewPersonaPanel.CheckedChanged += (_, __) => TogglePersonaPanel(_mnuViewPersonaPanel.Checked);
        _mnuViewDiagnostics.CheckedChanged += (_, __) => ToggleDiagnosticsPanel(_mnuViewDiagnostics.Checked);
        _mnuViewComposer.CheckedChanged += (_, __) => ToggleComposerPanel(_mnuViewComposer.Checked);
        _mnuViewConversations.CheckedChanged += (_, __) => ToggleConversationsPanel(_mnuViewConversations.Checked);
        panelsMenu.DropDownItems.Add(_mnuViewPersonaPanel);
        panelsMenu.DropDownItems.Add(_mnuViewDiagnostics);
        panelsMenu.DropDownItems.Add(_mnuViewComposer);
        panelsMenu.DropDownItems.Add(_mnuViewConversations);
        view.DropDownItems.Add(panelsMenu);
        view.DropDownItems.Add(new ToolStripSeparator());

        ToolStripMenuItem layoutPresets = new("&Layout");
        layoutPresets.DropDownItems.Add(CreateMenuItem("&Standard Layout", (_, __) => ApplyLayoutPreset("standard")));
        layoutPresets.DropDownItems.Add(CreateMenuItem("&Analyzer Layout", (_, __) => ApplyLayoutPreset("analyzer")));
        layoutPresets.DropDownItems.Add(CreateMenuItem("&Focus Layout", (_, __) => ApplyLayoutPreset("focus")));
        layoutPresets.DropDownItems.Add(new ToolStripSeparator());
        layoutPresets.DropDownItems.Add(CreateMenuItem("&Reset Layout", (_, __) => ApplyLayoutPreset("standard")));
        layoutPresets.DropDownItems.Add(new ToolStripSeparator());
        layoutPresets.DropDownItems.Add(CreateMenuItem("Save &Layout", (_, __) => SaveWorkspace()));
        layoutPresets.DropDownItems.Add(CreateMenuItem("&Restore Layout", (_, __) => RestoreWorkspace()));
        
        view.DropDownItems.Add(layoutPresets);
        view.DropDownItems.Add(new ToolStripSeparator());
        
        _mnuLockDockedWindows.Checked = _workspaceSettings.LockDockedWindows;
        _mnuLockDockedWindows.CheckedChanged += (_, __) => ToggleDockLock(_mnuLockDockedWindows.Checked);
        view.DropDownItems.Add(_mnuLockDockedWindows);
        view.DropDownItems.Add(new ToolStripSeparator());

        ToolStripMenuItem themeMenu = new("&Themes");
        themeMenu.DropDownItems.Add(CreateMenuItem("&Light", (_, __) => ApplyTheme("Light")));
        themeMenu.DropDownItems.Add(CreateMenuItem("&Dark", (_, __) => ApplyTheme("Dark")));
        themeMenu.DropDownItems.Add(CreateMenuItem("&Visual Studio Dark", (_, __) => ApplyTheme("Visual Studio Dark")));
        themeMenu.DropDownItems.Add(CreateMenuItem("&Azure Blue", (_, __) => ApplyTheme("Azure Blue")));
        themeMenu.DropDownItems.Add(CreateMenuItem("&High Contrast", (_, __) => ApplyTheme("High Contrast")));
        view.DropDownItems.Add(themeMenu);

        // —— Conversation —__
        ToolStripMenuItem conversation = new("&Conversation");
        conversation.DropDownItems.Add(CreateMenuItem("&Rename Tab...", Keys.F2, (_, __) => RenameActiveConversation()));
        conversation.DropDownItems.Add(CreateMenuItem("&Duplicate Tab", Keys.Control | Keys.D, (_, __) => DuplicateActiveConversation()));
        conversation.DropDownItems.Add(CreateMenuItem("&Pin / Unpin Tab", Keys.Control | Keys.P, (_, __) => PinActiveConversation()));
        conversation.DropDownItems.Add(CreateMenuItem("Open in &Window...", Keys.F11, (_, __) => OpenActiveConversationWindow()));
        conversation.DropDownItems.Add(new ToolStripSeparator());
        
        ToolStripMenuItem automationSubMenu = new("&Transform Response");
        automationSubMenu.DropDownItems.Add(CreateMenuItem("Convert ? &JSON", (_, __) => PrepareResponseTransformation("Convert the following content into valid JSON.")));
        automationSubMenu.DropDownItems.Add(CreateMenuItem("Convert ? &PowerShell", (_, __) => PrepareResponseTransformation("Convert the following response into a PowerShell-oriented implementation or script.")));
        conversation.DropDownItems.Add(automationSubMenu);
        conversation.DropDownItems.Add(new ToolStripSeparator());

        conversation.DropDownItems.Add(CreateMenuItem("Close &Other Tabs", (_, __) => CloseOtherConversations()));
        conversation.DropDownItems.Add(CreateMenuItem("Close &All Tabs", (_, __) => CloseAllConversations()));
        conversation.DropDownItems.Add(CreateMenuItem("&Close Tab", Keys.Control | Keys.W, (_, __) =>
        {
            ConversationTabState? state = GetActiveConversation();
            if (state != null) DisposeConversation(state);
        }));

        // —— Tools —__
        ToolStripMenuItem tools = new("&Tools");

        ToolStripMenuItem captureSubMenu = new("Screen &Capture");
        captureSubMenu.DropDownItems.Add(CreateMenuItem("&Screenshot ? AI", "Ctrl+Shift+A", async (_, __) => await SnipScreenAsync()));
        captureSubMenu.DropDownItems.Add(CreateMenuItem("&Browse Image...", (_, __) => BrowseImage()));
        captureSubMenu.DropDownItems.Add(CreateMenuItem("C&lear Image", (_, __) => ClearSelectedImage()));
        tools.DropDownItems.Add(captureSubMenu);
        tools.DropDownItems.Add(new ToolStripSeparator());

        tools.DropDownItems.Add(CreateMenuItem("&Settings...", (_, __) => OpenSettings()));
        tools.DropDownItems.Add(CreateMenuItem("&AI Connection Wizard...", (_, __) => OpenImportProviderWizard()));
        tools.DropDownItems.Add(new ToolStripSeparator());

        ToolStripMenuItem settingsSubMenu = new("&Quick Access");
        settingsSubMenu.DropDownItems.Add(CreateMenuItem("Manage &Personas...", (_, __) => OpenSettings("Personas")));
        settingsSubMenu.DropDownItems.Add(CreateMenuItem("AI &Provider Manager...", (_, __) => OpenSettings("Providers")));
        tools.DropDownItems.Add(settingsSubMenu);

        ToolStripMenuItem dataFilesSubMenu = new("&Data Files");
        dataFilesSubMenu.DropDownItems.Add(CreateMenuItem("Open &Persona JSON", (_, __) => OpenPersonaFile()));
        tools.DropDownItems.Add(dataFilesSubMenu);

        // —— Profiles —__
        ToolStripMenuItem profiles = new("P&rofiles");
        foreach (string profileName in _profiles.Keys)
        {
            string name = profileName;
            profiles.DropDownItems.Add(CreateMenuItem(name, (_, __) =>
            {
                _cmbProfile.SelectedItem = name;
                ApplyProfile(name);
            }));
        }

        // —— Enterprise (Metrics & Diagnostics) —__
        ToolStripMenuItem enterprise = new("E&nterprise");
        enterprise.DropDownItems.Add(CreateMenuItem("&Usage Dashboard...", (_, __) => OpenUsageDashboard()));
        enterprise.DropDownItems.Add(CreateMenuItem("&Refresh Metrics", (_, __) => RefreshUsageStatus()));
        enterprise.DropDownItems.Add(CreateMenuItem("&Purge Usage Statistics", (_, __) => PurgeUsageStatistics()));
        enterprise.DropDownItems.Add(new ToolStripSeparator());
        enterprise.DropDownItems.Add(CreateMenuItem("&Diagnostics Panel", (_, __) => ToggleDiagnosticsPanel(true)));
        enterprise.DropDownItems.Add(CreateMenuItem("E&xport Diagnostics...", (_, __) => ExportDiagnostics()));

        // —— Help —__
        ToolStripMenuItem help = new("&Help");
        help.DropDownItems.Add(CreateMenuItem("&Keyboard Shortcuts...", (_, __) => ShowKeyboardShortcuts()));
        help.DropDownItems.Add(new ToolStripSeparator());
        help.DropDownItems.Add(CreateMenuItem("&About BuddyAI...", (_, __) => ShowAbout()));

        _menuStrip.Items.AddRange(new ToolStripItem[] { file, edit, view, conversation, tools, profiles, enterprise, help });
    }

    private void BuildShell()
    {
        _workspaceLayout.Dock = DockStyle.Fill;

        BuildPersonaPanel();
        BuildComposerPanel();
        BuildConversationTabs();

        // Place content into dockable panel hosts
        _personaPanel.Dock = DockStyle.Fill;
        _dockPersonaExplorer.ContentHost.Controls.Add(_personaPanel);

        _composerPanel.Dock = DockStyle.Fill;
        _dockComposer.ContentHost.Controls.Add(_composerPanel);

        _conversationTabs.Dock = DockStyle.Fill;
        _dockConversations.ContentHost.Controls.Add(_conversationTabs);

        // Dock panels into their default zones
        _dockHost.DockPanel(_dockPersonaExplorer, DockZone.Left);
        _dockHost.DockPanel(_dockComposer, DockZone.Top);
        _dockHost.DockPanel(_dockConversations, DockZone.Center);
        _dockHost.DockPanel(_dockDiagnostics, DockZone.Bottom);

        _dockHost.SetZoneSize(DockZone.Left, 280);
        _dockHost.SetZoneSize(DockZone.Top, 210);
        _dockHost.SetZoneSize(DockZone.Bottom, 180);
    }

    private void BuildPersonaPanel()
    {
        _personaPanel.Dock = DockStyle.Fill;
        _personaPanel.Padding = new Padding(6);

        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        _personaPanel.Controls.Add(layout);

        _txtPersonaExplorerSearch.Dock = DockStyle.Fill;
        _txtPersonaExplorerSearch.PlaceholderText = "Search personas";
        layout.Controls.Add(_txtPersonaExplorerSearch, 0, 0);

        Image treeIconSample = _iconCache.Get(TablerIcon.BrandOpenai, TablerIconCache.TreeIconSize);
        ImageList treeImages = new() { ImageSize = treeIconSample.Size, ColorDepth = ColorDepth.Depth32Bit };
        treeImages.Images.Add("persona", treeIconSample);
        treeImages.Images.Add("star", _iconCache.Get(TablerIcon.StarFilled, TablerIconCache.TreeIconSize));
        treeImages.Images.Add("folder", _iconCache.Get(TablerIcon.FolderOpen, TablerIconCache.TreeIconSize));
        _personaTree.ImageList = treeImages;

        _personaTree.Dock = DockStyle.Fill;
        _personaTree.HideSelection = false;
        layout.Controls.Add(_personaTree, 0, 1);
    }

    private void BuildComposerPanel()
    {
        _composerPanel.Dock = DockStyle.Fill;
        _composerPanel.Padding = new Padding(1);
        _composerPanel.Font = new Font("Segoe UI", 10.5F); // 20% larger base font for labels, etc.

        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 5,
            //CellBorderStyle = TableLayoutPanelCellBorderStyle.Inset

        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 85));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 85)); // Increased to match larger text
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 0)); // Formerly 50% / unused
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); // Give combo boxes enough height space
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
        _composerPanel.Controls.Add(layout);

        _cmbProfile.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbProfile.Font = new Font("Segoe UI", 10.5F);
        foreach (string profile in _profiles.Keys)
            _cmbProfile.Items.Add(profile);

        _cmbProvider.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbProvider.Width = 180;
        _cmbProvider.Font = new Font("Segoe UI", 10.5F);

        _cmbModel.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbModel.Width = 200;
        _cmbModel.Font = new Font("Segoe UI", 10.5F);

        _cmbTemperature.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbTemperature.Items.AddRange(SupportedTemperatures);
        _cmbTemperature.Width = 40;
        _cmbTemperature.SelectedItem = DefaultTemperature;
        _cmbTemperature.Font = new Font("Segoe UI", 10.5F);

        _cmbPersona.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbPersona.Width = 180;
        _cmbPersona.Font = new Font("Segoe UI", 10.5F);
        _cmbPrefilledQuestion.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbPrefilledQuestion.Width = 350;
        _cmbPrefilledQuestion.Font = new Font("Segoe UI", 10.5F);

        _txtConversationSearch.PlaceholderText = "Find in active conversation";
        _txtConversationSearch.Font = new Font("Segoe UI", 10.5F);
        _btnConversationSearch.Text = "Find";
        _btnConversationSearch.Font = new Font("Segoe UI", 10.5F);
        _btnConversationSearchClear.Text = "Clear";
        _btnConversationSearchClear.Font = new Font("Segoe UI", 10.5F);
        _btnConversationSearch.Width = 72;
        _btnConversationSearchClear.Width = 72;

        _txtSystemPrompt.Multiline = true;
        _txtSystemPrompt.AcceptsReturn = true;
        _txtSystemPrompt.AcceptsTab = true;
        _txtSystemPrompt.ScrollBars = ScrollBars.Vertical;
        _txtSystemPrompt.Dock = DockStyle.Fill;
        _txtSystemPrompt.Font = new Font("Segoe UI", 10F); // 20% larger than 10F

        _txtQuestion.Multiline = true;
        _txtQuestion.AcceptsReturn = true;
        _txtQuestion.AcceptsTab = true;
        _txtQuestion.ScrollBars = ScrollBars.Vertical;
        _txtQuestion.Dock = DockStyle.Fill;
        _txtQuestion.Font = new Font("Segoe UI", 10F); // 20% larger than 10F
        _txtQuestion.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
        _txtQuestion.AutoCompleteSource = AutoCompleteSource.CustomSource;
        _txtQuestion.AutoCompleteCustomSource = _promptAutoComplete;

        _picPreview.Dock = DockStyle.Fill;
        _picPreview.BorderStyle = BorderStyle.FixedSingle;
        _picPreview.SizeMode = PictureBoxSizeMode.Zoom;
        _picPreview.Cursor = Cursors.Hand;

        _lblImageInfo.Dock = DockStyle.Fill;
        _lblImageInfo.Text = "No image selected.";
        _lblImageInfo.TextAlign = ContentAlignment.MiddleLeft;
        _lblImageInfo.Width = 300;
        _lblImageInfo.BorderStyle = BorderStyle.FixedSingle;

        ConfigureActionButton(_btnNewConversation, "New", TablerIcon.SquareRoundedPlus, primary: false);
        ConfigureActionButton(_btnBrowseImage, "Image", TablerIcon.Photo, primary: false);
        ConfigureActionButton(_btnSnipScreen, "Snip", TablerIcon.Screenshot, primary: false);
        ConfigureActionButton(_btnClearImage, "Clear", TablerIcon.Eraser, primary: false);
        ConfigureActionButton(_btnAsk, "Ask", TablerIcon.Send, primary: true);
        ConfigureActionButton(_btnFollowUp, "Follow", TablerIcon.ArrowForwardUp, primary: true);
        ConfigureActionButton(_btnCancel, "Cancel", TablerIcon.X, primary: false);
        _chkSnipAuto.Text = "Auto Send";
        _chkSnipAuto.AutoSize = true;

        _chkSnipShortcut.Text = "Hot Key";
        _chkSnipShortcut.AutoSize = true;
        _chkSnipShortcut.Margin = new Padding(0, 4, 8, 0);
        _chkSnipShortcut.Padding = new Padding(0);
        _chkSnipShortcut.CheckAlign = ContentAlignment.MiddleLeft;
        _chkSnipShortcut.TextAlign = ContentAlignment.MiddleLeft;
        _chkSnipShortcut.AccessibleName = "Enable Win+Shift+Q global snip ask shortcut";
        _toolTip.SetToolTip(_chkSnipShortcut, "Enable the global Win+Shift+Q snip ? ask shortcut.");

        _numSnipDelay.Minimum = 0;
        _numSnipDelay.Maximum = 30;
        _numSnipDelay.Value = 0;
        _numSnipDelay.Width = 60;

        FlowLayoutPanel searchPanel = new()
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false,
            Margin = new Padding(0)
        };
        searchPanel.Controls.Add(_txtConversationSearch);
        searchPanel.Controls.Add(_btnConversationSearch);
        searchPanel.Controls.Add(_btnConversationSearchClear);
        _txtConversationSearch.Width = 220;

        FlowLayoutPanel modelTemperaturePanel = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        // Use Anchors to center vertically in flow layout
        _cmbProvider.Anchor = AnchorStyles.Left;
        _cmbProvider.Margin = new Padding(0, 3, 0, 0);

        modelTemperaturePanel.Controls.Add(_cmbProvider);
        modelTemperaturePanel.Controls.Add(new Label
        {
            Text = "Model",
            TextAlign = ContentAlignment.MiddleLeft,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(8, 0, 4, 0)
        });

        _cmbModel.Anchor = AnchorStyles.Left;
        _cmbModel.Margin = new Padding(0, 3, 0, 0);
        modelTemperaturePanel.Controls.Add(_cmbModel);

        modelTemperaturePanel.Controls.Add(new Label
        {
            Text = "Temp.",
            TextAlign = ContentAlignment.MiddleLeft,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(8, 0, 0, 0)
        });

        _cmbTemperature.Anchor = AnchorStyles.Left;
        _cmbTemperature.Margin = new Padding(0, 3, 0, 0);
        modelTemperaturePanel.Controls.Add(_cmbTemperature);

        layout.Controls.Add(
            new Label { Text = "Profile", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 0, 0);
        
        _cmbProfile.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _cmbProfile.Margin = new Padding(3, 4, 3, 0);
        layout.Controls.Add(_cmbProfile, 1, 0);

        layout.Controls.Add(
            new Label { Text = "Provider", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill, Margin = new Padding(0, 2, 4, 0) }, 2, 0);
        
        layout.Controls.Add(modelTemperaturePanel, 3, 0);
        // layout.Controls.Add(searchPanel, 4, 0);

        FlowLayoutPanel personaAndQuestionPanel = new()
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false,
            Padding = new Padding(0), Margin = new Padding(0)
        };

        modelTemperaturePanel.Controls.Add(new Label
        {
            Text = "Persona", TextAlign = ContentAlignment.MiddleLeft, AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(8, 0, 0, 0)
        });

        _cmbPersona.Anchor = AnchorStyles.Left;
        _cmbPersona.Margin = new Padding(0, 3, 0, 0);
        modelTemperaturePanel.Controls.Add(_cmbPersona);

        modelTemperaturePanel.Controls.Add(new Label
        {
            Text = "Template", TextAlign = ContentAlignment.MiddleLeft, AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(8, 0, 0, 0)
        });

        _cmbPrefilledQuestion.Anchor = AnchorStyles.Left;
        _cmbPrefilledQuestion.Margin = new Padding(0, 3, 0, 0);
        modelTemperaturePanel.Controls.Add(_cmbPrefilledQuestion);

        //layout.Controls.Add(personaAndQuestionPanel, 4, 0);
        layout.SetColumnSpan(modelTemperaturePanel, 3);

        layout.Controls.Add(
            new Label { Text = "System Prompt", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 0, 1);

        SplitContainer promptSplit = new()
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 6,
            FixedPanel = FixedPanel.None
        };
        _promptSplit = promptSplit;
        promptSplit.Panel1.Controls.Add(_txtSystemPrompt);
        promptSplit.Panel1.Padding = new Padding(0, 0, 4, 0);

        // Wrap _txtQuestion in a panel so the expand button can overlay it
        Panel questionHost = new() { Dock = DockStyle.Fill };
        _txtQuestion.Dock = DockStyle.Fill;

        // Configure prompt toolbar buttons (Copy, Cut, Paste, Expand)
        int toolBtnSize = 28;
        void ConfigurePromptToolButton(Button btn, TablerIcon icon, string tooltip)
        {
            btn.Size = new Size(toolBtnSize, toolBtnSize);
            btn.Text = string.Empty;
            btn.Image = _iconCache.Get(icon, 16);
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.BackColor = Color.FromArgb(60, 60, 60);
            btn.Cursor = Cursors.Hand;
            _toolTip.SetToolTip(btn, tooltip);
        }

        ConfigurePromptToolButton(_btnPromptCopy, TablerIcon.Copy, "Copy");
        ConfigurePromptToolButton(_btnPromptCut, TablerIcon.Cut, "Cut");
        ConfigurePromptToolButton(_btnPromptPaste, TablerIcon.Clipboard, "Paste");
        ConfigurePromptToolButton(_btnExpandPrompt, TablerIcon.ArrowsMaximize, "Expand prompt editor");

        questionHost.Controls.Add(_btnPromptCopy);
        questionHost.Controls.Add(_btnPromptCut);
        questionHost.Controls.Add(_btnPromptPaste);
        questionHost.Controls.Add(_btnExpandPrompt);
        questionHost.Controls.Add(_txtQuestion);
        _btnPromptCopy.BringToFront();
        _btnPromptCut.BringToFront();
        _btnPromptPaste.BringToFront();
        _btnExpandPrompt.BringToFront();

        // Position the prompt toolbar buttons at top-right of the question host
        questionHost.Layout += (_, __) =>
        {
            LayoutPromptToolButtons(questionHost);
        };

        promptSplit.Panel2.Controls.Add(questionHost);
        promptSplit.Panel2.Padding = new Padding(4, 0, 0, 0);

        layout.Controls.Add(promptSplit, 1, 1);
        layout.SetColumnSpan(promptSplit, 4);

        layout.Controls.Add(_picPreview, 5, 1);
        layout.SetRowSpan(_picPreview, 2);

        FlowLayoutPanel buttonPanel = new()
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false,
            Margin = new Padding(0)
        };
        buttonPanel.Controls.Add(_btnNewConversation);
        buttonPanel.Controls.Add(_btnBrowseImage);
        buttonPanel.Controls.Add(_btnSnipScreen);
        buttonPanel.Controls.Add(_btnClearImage);
        buttonPanel.Controls.Add(_chkSnipAuto);
        _chkSnipAuto.Padding = new Padding(2);
        buttonPanel.Controls.Add(_chkSnipShortcut);
        _chkSnipShortcut.Padding = new Padding(2);
        buttonPanel.Controls.Add(new Label { Text = "Delay", AutoSize = true, Padding = new Padding(8, 9, 4, 0) });
        buttonPanel.Controls.Add(_numSnipDelay);
        buttonPanel.Controls.Add(_btnAsk);
        buttonPanel.Controls.Add(_btnFollowUp);
        buttonPanel.Controls.Add(_btnCancel);
        buttonPanel.Controls.Add(_lblImageInfo);
        layout.Controls.Add(buttonPanel, 0, 2);
        layout.SetColumnSpan(buttonPanel, 6);
    }

    private void BuildConversationTabs()
    {
        _conversationTabs.Dock = DockStyle.Fill;
        _conversationTabs.DrawMode = TabDrawMode.OwnerDrawFixed;
        _conversationTabs.Padding = new Point(18, 6);
        _conversationTabs.DrawItem += ConversationTabs_DrawItem;
        _conversationTabs.MouseDown += ConversationTabs_MouseDown;
        _conversationTabs.SelectedIndexChanged += (_, __) =>
        {
            SyncComposerWithActiveConversation();
            EnsureActiveConversationRendered();
            UpdateUiState();
            UpdateStatusBar();
        };
    }

    private void BuildToolsPanel()
    {
        _toolsPanel.Dock = DockStyle.Fill;
        _toolsPanel.Padding = new Padding(4);

        _toolTabs.Dock = DockStyle.Fill;
        _toolsPanel.Controls.Add(_toolTabs);

        _toolTabs.TabPages.Add(BuildDiagnosticsPage());
    }

    private TabPage BuildTemplatesPage()
    {

        TabPage page = new("Templates");
        TableLayoutPanel layout = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(8) };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        page.Controls.Add(layout);

        _lstTemplates.Dock = DockStyle.Fill;
        layout.Controls.Add(_lstTemplates, 0, 0);

        FlowLayoutPanel buttons = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = true };
        Button insert = new() { Text = "Insert", Width = 80 };
        Button apply = new() { Text = "Apply", Width = 80 };
        Button manage = new() { Text = "Manage", Width = 90 };
        Button reload = new() { Text = "Reload", Width = 90 };
        insert.Click += (_, __) => InsertSelectedTemplate();
        apply.Click += (_, __) => ApplySelectedTemplate();
        manage.Click += (_, __) => ManageTemplates();
        reload.Click += (_, __) => LoadTemplates();
        buttons.Controls.Add(insert);
        buttons.Controls.Add(apply);
        buttons.Controls.Add(manage);
        buttons.Controls.Add(reload);
        layout.Controls.Add(buttons, 0, 1);
        return page;
    }

    private TabPage BuildSnippetsPage()
    {
        TabPage page = new("Snippets");
        TableLayoutPanel layout = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(8) };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        page.Controls.Add(layout);

        _lstSnippets.Dock = DockStyle.Fill;
        layout.Controls.Add(_lstSnippets, 0, 0);

        FlowLayoutPanel buttons = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = true };
        Button insert = new() { Text = "Insert", Width = 80 };
        Button manage = new() { Text = "Manage", Width = 90 };
        Button reload = new() { Text = "Reload", Width = 90 };
        insert.Click += (_, __) => InsertSelectedSnippet();
        manage.Click += (_, __) => ManageSnippets();
        reload.Click += (_, __) => LoadSnippets();
        buttons.Controls.Add(insert);
        buttons.Controls.Add(manage);
        buttons.Controls.Add(reload);
        layout.Controls.Add(buttons, 0, 1);
        return page;
    }

    private TabPage BuildDiagnosticsPage()
    {
        TabPage page = new("Diagnostics");
        TableLayoutPanel layout = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(3) };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        page.Controls.Add(layout);

        FlowLayoutPanel buttons = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        Button export = new() { Text = "Export", Width = 90 };
        export.Click += (_, __) => ExportDiagnostics();
        Button clear = new() { Text = "Clear", Width = 90 };
        clear.Click += (_, __) =>
        {
            _txtDiagnostics.Clear();
            _diagnostics.Info("Diagnostics window cleared by user.");
        };
        buttons.Controls.Add(export);
        buttons.Controls.Add(clear);
        layout.Controls.Add(buttons, 0, 0);

        _txtDiagnostics.Dock = DockStyle.Fill;
        _txtDiagnostics.Multiline = true;
        _txtDiagnostics.ScrollBars = ScrollBars.Both;
        _txtDiagnostics.WordWrap = false;
        _txtDiagnostics.ReadOnly = true;
        _txtDiagnostics.Font = new Font("Consolas", 10F);
        layout.Controls.Add(_txtDiagnostics, 0, 1);
        return page;
    }

    private void BuildDiagnosticsPanel()
    {
        TableLayoutPanel layout = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(6) };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        _dockDiagnostics.ContentHost.Controls.Add(layout);

        FlowLayoutPanel bar = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        Button export = new() { Text = "Export", Width = 80 };
        export.Click += (_, __) => ExportDiagnostics();
        
        Button clear = new() { Text = "Clear", Width = 80 };

        bar.Controls.Add(export);
        bar.Controls.Add(clear);
        layout.Controls.Add(bar, 0, 0);

        TabControl diagnosticsTabs = new() { Dock = DockStyle.Fill };
        TabPage logsTab = new("System Logs");
        TabPage httpTab = new("HTTP Dump");

        RichTextBox diagnosticsBox = new()
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            Font = new Font("Consolas", 9F),
            WordWrap = false
        };
        diagnosticsBox.Text = string.Empty;
        logsTab.Controls.Add(diagnosticsBox);

        RichTextBox httpDumpBox = new()
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            Font = new Font("Consolas", 9F),
            WordWrap = true
        };
        httpDumpBox.Text = string.Empty;
        httpTab.Controls.Add(httpDumpBox);

        diagnosticsTabs.TabPages.Add(logsTab);
        diagnosticsTabs.TabPages.Add(httpTab);
        layout.Controls.Add(diagnosticsTabs, 0, 1);

        clear.Click += (_, __) =>
        {
            _txtDiagnostics.Clear();
            diagnosticsBox.Clear();
            httpDumpBox.Clear();
            _diagnostics.Info("Diagnostics window cleared by user.");
        };

        _diagnostics.EntryAdded += (_, entry) =>
        {
            if (!IsDisposed)
            {
                BeginInvoke(new Action(() =>
                {
                    diagnosticsBox.AppendText(entry + Environment.NewLine);
                    _txtDiagnostics.AppendText(entry + Environment.NewLine);
                }));
            }
        };

        void AppendHttpLog(string log)
        {
            if (!IsDisposed)
            {
                BeginInvoke(new Action(() =>
                {
                    httpDumpBox.AppendText(log + Environment.NewLine + Environment.NewLine);
                    if (httpDumpBox.Lines.Length > 1000)
                    {
                        string[] lines = httpDumpBox.Lines;
                        httpDumpBox.Lines = lines.Skip(lines.Length - 1000).ToArray();
                        httpDumpBox.SelectionStart = httpDumpBox.Text.Length;
                        httpDumpBox.ScrollToCaret();
                    }
                }));
            }
        }

        _client.OnHttpRequestStarted += (url, payload) =>
        {
            AppendHttpLog($"--- OUTGOING HTTP REQUEST ---\nURL: {url}\nPayload:\n{payload}");
        };

        _client.OnHttpResponseReceived += (url, response) =>
        {
            AppendHttpLog($"--- INCOMING HTTP RESPONSE ---\nURL: {url}\n{response}");
        };
    }

    private void BuildStatusBar()
    {
        _statusState.Spring = true;
        _statusState.TextAlign = ContentAlignment.MiddleLeft;
        _statusStrip.Items.AddRange(new ToolStripItem[]
        {
            _statusState,
            _statusModel,
            _statusPersona,
            _statusProfile,
            _statusProvider,
            _statusTokens,
            _statusCost,
            _statusLatency,
            _statusConversations
        });
    }

    private void WireEvents()
    {
        _cmbProfile.SelectedIndexChanged += (_, __) =>
        {
            if (_cmbProfile.SelectedItem is string profileName)
                ApplyProfile(profileName);
        };

        _cmbPersona.SelectedIndexChanged += (_, __) => OnPersonaChanged();
        _cmbPrefilledQuestion.SelectedIndexChanged += (_, __) => OnPrefilledQuestionChanged();
        _cmbProvider.SelectedIndexChanged += async (_, __) =>
        {
            if (_isLoadingProviders)
                return;

            PopulateModelsForSelectedProvider();
            UpdateImageInfo();
            UpdateUiState();
            UpdateStatusBar();

            // Fetch dynamic models if local provider
            if (GetSelectedProvider() is AiProviderDefinition provider &&
                (provider.ProviderType == AiProviderTypes.Ollama || provider.ProviderType == AiProviderTypes.LMStudio))
            {
                await TryFetchDynamicModelsAsync(provider);
            }
        };
        _cmbModel.SelectedIndexChanged += (_, __) =>
        {
            UpdateImageInfo();
            UpdateUiState();
            UpdateStatusBar();
        };
        _cmbTemperature.SelectedIndexChanged += (_, __) =>
        {
            ConversationTabState? activeConversation = GetActiveConversation();
            if (activeConversation != null)
                activeConversation.Temperature = GetSelectedTemperatureText();

            UpdateStatusBar();
        };

        _txtQuestion.TextChanged += (_, __) =>
        {
            if (!_suppressQuestionTextChanged)
            {
                UpdateUiState();
                var tb = _txtQuestion;

                string normalized = tb.Text
                    .Replace("\r\n", "\n")
                    .Replace("\r", "\n")
                    .Replace("\n", "\r\n");

                if (tb.Text != normalized)
                {
                    int pos = tb.SelectionStart;

                    tb.Text = normalized;

                    tb.SelectionStart = Math.Min(pos, tb.Text.Length);
                }
            }
        };

        _txtSystemPrompt.TextChanged += (_, __) => UpdateUiState();
        _txtPersonaExplorerSearch.TextChanged += (_, __) => RefreshPersonaExplorer();

        _btnBrowseImage.Click += (_, __) => BrowseImage();
        _btnSnipScreen.Click += async (_, __) => await SnipScreenAsync();
        _btnClearImage.Click += (_, __) => ClearSelectedImage();
        _btnExpandPrompt.Click += (_, __) => TogglePromptExpand();
        _btnPromptCopy.Click += (_, __) => PromptCopy();
        _btnPromptCut.Click += (_, __) => PromptCut();
        _btnPromptPaste.Click += (_, __) => PromptPaste();
        _chkSnipShortcut.CheckedChanged += OnSnipShortcutToggleChanged;
        _btnAsk.Click += async (_, __) => await AskAsync(null, false);
        _btnFollowUp.Click += async (_, __) => await AskAsync(GetActiveConversation()?.PreviousResponseId, true);
        _btnCancel.Click += (_, __) => CancelActiveRequest();
        _btnNewConversation.Click += (_, __) => CreateDraftConversation();

        _picPreview.Click += (_, __) =>
        {
            if (_picPreview.Image != null)
            {
                using ImagePreviewForm previewForm = new(_picPreview.Image);
                if (previewForm.ShowDialog(this) == DialogResult.OK && previewForm.EditedImage != null)
                {
                    _picPreview.Image = previewForm.EditedImage;
                    UpdateImageInfo();
                }
            }
        };

        _personaTree.NodeMouseClick += PersonaTree_NodeMouseClick;

        _promptMenu.Items.Add("Paste Image", null, (_, __) => PasteImageFromClipboard());
        _promptMenu.Items.Add("Clear", null, (_, __) => _txtQuestion.Clear());
        _txtQuestion.ContextMenuStrip = _promptMenu;

        _responseMenu.Items.Add("Copy", null, (_, __) => CopyFullResponse());
        _responseMenu.Items.Add("Copy Code Block", null, (_, __) => CopyFirstCodeBlock());
        _responseMenu.Items.Add("Explain Code", null, (_, __) => PrepareResponseTransformation("Explain the following code or technical response in simpler terms, preserving the key behavior."));
        _responseMenu.Items.Add("Convert to PowerShell", null, (_, __) => PrepareResponseTransformation("Convert the following response into a PowerShell-oriented output."));
        _responseMenu.Items.Add("Convert to JSON", null, (_, __) => PrepareResponseTransformation("Convert the following response into valid JSON output."));
        _responseMenu.Items.Add("Export", null, (_, __) => ExportActiveConversation());

        _personaMenu.Items.Add("Set Persona", null, (_, __) => SelectPendingPersonaContext());
        _personaMenu.Items.Add("Edit Personas", null, (_, __) => ManagePersonas());
        _personaMenu.Items.Add("Export Personas", null, (_, __) => ExportPersonas());

        _conversationTabMenu.Items.Add("Rename Tab", null, (_, __) => RenameActiveConversation());
        _conversationTabMenu.Items.Add("Duplicate Tab", null, (_, __) => DuplicateActiveConversation());
        _conversationTabMenu.Items.Add("Pin Tab", null, (_, __) => PinActiveConversation());
        _conversationTabMenu.Items.Add("Export Conversation", null, (_, __) => ExportActiveConversation());
        _conversationTabMenu.Items.Add("Open in Window", null, (_, __) => OpenActiveConversationWindow());
        _conversationTabMenu.Items.Add(new ToolStripSeparator());
        _conversationTabMenu.Items.Add("Close Others", null, (_, __) => CloseOtherConversations());
        _conversationTabMenu.Items.Add("Close All", null, (_, __) => CloseAllConversations());

        _clipboardMonitorTimer.Interval = 1500;
        _clipboardMonitorTimer.Tick += (_, __) => PollClipboard();
        _clipboardMonitorTimer.Start();

        _autoSaveTimer.Interval = Math.Max(60, _workspaceSettings.AutoSaveIntervalSeconds) * 1000;
        _autoSaveTimer.Tick += (_, __) =>
        {
            try { _sessionStateService.Save(BuildSessionSnapshot()); }
            catch { }
        };
        _autoSaveTimer.Start();

        _dockHost.LayoutChanged += (_, __) => SyncMenuChecksWithDockState();

        HandleCreated += (_, __) =>
        {
            if (_chkSnipShortcut.Checked)
                UpdateGlobalSnipShortcutRegistration(showFailureUi: false, announceSuccess: false);
            RegisterTextCaptureHotKey();
        };
        HandleDestroyed += (_, __) =>
        {
            ReleaseGlobalSnipShortcutRegistration();
            ReleaseTextCaptureHotKey();
        };
        FormClosing += OnFormClosing;
    }

    private ToolStripMenuItem CreateMenuItem(string text, EventHandler onClick)
    {
        ToolStripMenuItem item = new(text);
        item.Click += onClick;
        return item;
    }

    private ToolStripMenuItem CreateMenuItem(string text, Keys shortcutKeys, EventHandler onClick)
    {
        ToolStripMenuItem item = new(text) { ShortcutKeys = shortcutKeys, ShowShortcutKeys = true };
        item.Click += onClick;
        return item;
    }

    private ToolStripMenuItem CreateMenuItem(string text, string shortcutDisplay, EventHandler onClick)
    {
        ToolStripMenuItem item = new(text) { ShortcutKeyDisplayString = shortcutDisplay, ShowShortcutKeys = true };
        item.Click += onClick;
        return item;
    }

    private void ConfigureActionButton(Button button, string text, TablerIcon icon, bool primary)
    {
        button.Text = " " + text;
        button.Image = _iconCache.Get(icon, TablerIconCache.ButtonIconSize);
        button.Font = new Font("Segoe UI", 10.5F, primary ? FontStyle.Bold : FontStyle.Regular);
        button.TextImageRelation = TextImageRelation.ImageBeforeText;
        button.ImageAlign = ContentAlignment.MiddleLeft;
        button.TextAlign = ContentAlignment.MiddleCenter;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.AutoSize = true;
        button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        button.Padding = new Padding(4, 2, 6, 2);
        button.Margin = new Padding(2);

        if (primary)
        {
            button.BackColor = _activeTheme.Accent;
            button.ForeColor = _activeTheme.AccentForeground;
            button.FlatAppearance.BorderColor = _activeTheme.Accent;
        }
        else
        {
            button.BackColor = _activeTheme.SurfaceAlt;
            button.ForeColor = _activeTheme.Text;
            button.FlatAppearance.BorderColor = _activeTheme.SurfaceAlt;
        }
    }
}
