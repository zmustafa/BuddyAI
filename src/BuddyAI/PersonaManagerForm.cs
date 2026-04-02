using System.Windows.Forms;
using BuddyAI.Forms;
using BuddyAI.Models;
using BuddyAI.Services;

namespace BuddyAI;

public sealed class PersonaManagerForm : Form
{
    private static readonly string[] HardcodedCategories =
    [
        "Coder",
        "General",
        "Engineering",
        "Security",
        "DevOps",
        "Architecture",
        "Data & Analytics",
        "Compliance",
        "Finance",
        "Marketing",
        "HR & People",
        "Legal",
        "Support",
        "Education",
        "Creative",
        "Research"
    ];

    private static readonly string[] HardcodedIcons =
    [
        "🧠",
        "💻",
        "🛡️",
        "🏗️",
        "☁️",
        "⚙️",
        "📊",
        "📚",
        "🎨",
        "🔍"
    ];

    private readonly PersonaService _personaService;
    private readonly AiProviderService _providerService;
    private readonly TreeView _treePersonas = new();
    private readonly TextBox _txtPersonaName = new();
    private readonly ComboBox _cmbCategory = new();
    private readonly TextBox _txtId = new();
    private readonly ComboBox _cmbIcon = new();
    private readonly TextBox _txtAccentHex = new();
    private readonly ComboBox _cmbDefaultModel = new();
    private readonly CheckBox _chkFavorite = new();
    private readonly TextBox _txtSystemPrompt = new();
    private readonly TextBox _txtMessage = new();
    private readonly Button _btnSave = new();
    private readonly Button _btnClose = new();
    private readonly Label _lblPath = new();
    private readonly SplitContainer _split = new();

    private readonly TextBox _txtSearch = new();
    private readonly ComboBox _cmbCategoryFilter = new();
    private readonly CheckBox _chkFavoritesOnly = new();
    private readonly Panel _pnlAccentPreview = new();
    private readonly Label _lblStats = new();
    private readonly ToolStrip _toolStrip1 = new();
    private readonly ToolStrip _toolStrip2 = new();
    private readonly ToolStrip _toolStrip3 = new();
    private ToolStripButton _tsbClone = null!;
    private ToolStripButton _tsbDelete = null!;

    private readonly List<PersonaRecord> _records = new();
    private readonly List<PersonaRecord> _filteredRecords = new();
    private bool _isLoading;
    private bool _isDirty;

    public PersonaManagerForm(PersonaService personaService, AiProviderService providerService)
    {
        _personaService = personaService;
        _providerService = providerService;

        Text = "BuddyAI Persona Manager";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(960, 580);
        Size = new Size(1100, 680);
        AutoScaleMode = AutoScaleMode.Dpi;
        KeyPreview = true;
        Font = new Font("Segoe UI Emoji", 9F);

        Resize += Form1_Resize;

        BuildUi();
        WireEvents();
        LoadRecords(selectId: null);
    }

    private void BuildUi()
    {
        _split.Dock = DockStyle.Fill;
        _split.FixedPanel = FixedPanel.Panel1;
        _split.BorderStyle = BorderStyle.Fixed3D;
        Controls.Add(_split);

        Panel left = new() { Dock = DockStyle.Fill, Padding = new Padding(10) };
        Panel right = new() { Dock = DockStyle.Fill, Padding = new Padding(10) };
        _split.Panel1.Controls.Add(left);
        _split.Panel2.Controls.Add(right);

        // --- Search / filter bar at top of left panel ---
        TableLayoutPanel filterBar = new()
        {
            Dock = DockStyle.Top,
            Height = 72,
            ColumnCount = 3,
            RowCount = 2,
            Padding = new Padding(0, 0, 0, 4),
            Margin = new Padding(0)
        };
        filterBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        filterBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        filterBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        filterBar.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        filterBar.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

        _txtSearch.Dock = DockStyle.Fill;
        _txtSearch.PlaceholderText = "🔍 Search personas... (Ctrl+F)";
        filterBar.Controls.Add(_txtSearch, 0, 0);

        _cmbCategoryFilter.Dock = DockStyle.Fill;
        _cmbCategoryFilter.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbCategoryFilter.Items.Add("All Categories");
        foreach (string cat in HardcodedCategories)
            _cmbCategoryFilter.Items.Add(cat);
        _cmbCategoryFilter.SelectedIndex = 0;
        filterBar.Controls.Add(_cmbCategoryFilter, 1, 0);

        _chkFavoritesOnly.Dock = DockStyle.Fill;
        _chkFavoritesOnly.Text = "★ Only";
        _chkFavoritesOnly.TextAlign = ContentAlignment.MiddleCenter;
        filterBar.Controls.Add(_chkFavoritesOnly, 2, 0);

        _lblStats.Dock = DockStyle.Fill;
        _lblStats.TextAlign = ContentAlignment.MiddleLeft;
        _lblStats.Text = "0 personas";
        filterBar.Controls.Add(_lblStats, 0, 1);
        filterBar.SetColumnSpan(_lblStats, 3);

        _treePersonas.Dock = DockStyle.Fill;
        _treePersonas.HideSelection = false;
        _treePersonas.FullRowSelect = true;
        _treePersonas.ShowLines = true;
        _treePersonas.ShowPlusMinus = true;
        _treePersonas.ShowRootLines = true;

        // --- Toolbars for persona actions (two rows) ---
        _tsbClone = CreateToolStripButton("Clone", CloneSelectedRecord);
        _tsbDelete = CreateToolStripButton("Delete", DeleteSelectedRecord);

        _toolStrip1.GripStyle = ToolStripGripStyle.Hidden;
        _toolStrip1.Dock = DockStyle.Top;
        _toolStrip1.Items.AddRange(
        [
            CreateToolStripButton("New", CreateNewRecord),
            _tsbClone,
            _tsbDelete,
            new ToolStripSeparator(),
            CreateToolStripButton("▲", () => MoveSelectedRecord(-1)),
            CreateToolStripButton("▼", () => MoveSelectedRecord(1)),
            CreateToolStripButton("Expand All", ToggleExpandAll)
        ]);

        _toolStrip2.GripStyle = ToolStripGripStyle.Hidden;
        _toolStrip2.Dock = DockStyle.Top;
        _toolStrip2.Items.AddRange(
        [
            CreateToolStripButton("Reload", ReloadWithPrompt),
            CreateToolStripButton("Open JSON", OpenJsonFile),
            new ToolStripSeparator(),
            CreateToolStripButton("Import", ImportPersonas),
            CreateToolStripButton("Export", ExportPersonas)
        ]);

        _toolStrip3.GripStyle = ToolStripGripStyle.Hidden;
        _toolStrip3.Dock = DockStyle.Top;
        _toolStrip3.Items.AddRange(
        [
            CreateToolStripButton("⬇ Download from Catalog", DownloadFromCatalog)
        ]);

        // WinForms dock z-order: last added gets highest layout priority.
        // Add Fill first, then Top items — so Top claims space before Fill.
        left.Controls.Add(_treePersonas);
        left.Controls.Add(_toolStrip3);
        left.Controls.Add(_toolStrip2);
        left.Controls.Add(_toolStrip1);
        left.Controls.Add(filterBar);

        TableLayoutPanel form = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 8,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));  // 0: Persona
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));  // 1: Category
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));  // 2: Id
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));  // 3: Icon/Accent
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));  // 4: Model/Lense
        form.RowStyles.Add(new RowStyle(SizeType.Percent, 40));   // 5: System Prompt
        form.RowStyles.Add(new RowStyle(SizeType.Percent, 60));   // 6: User Prompt
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));  // 7: Footer
        right.Controls.Add(form);

        AddRowLabel(form, "Persona", 0);
        AddRowLabel(form, "Category", 1);
        AddRowLabel(form, "Id", 2);
        AddRowLabel(form, "Options", 3);
        AddRowLabel(form, "Model", 4);
        AddRowLabel(form, "System Prompt", 5);
        AddRowLabel(form, "User Prompt", 6);

        _txtPersonaName.Dock = DockStyle.Fill;

        _cmbCategory.Dock = DockStyle.Fill;
        _cmbCategory.DropDownStyle = ComboBoxStyle.DropDownList;
        foreach (string cat in HardcodedCategories)
            _cmbCategory.Items.Add(cat);
        _cmbCategory.SelectedIndex = 0;

        _cmbIcon.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbIcon.Width = 60;
        foreach (string icon in HardcodedIcons)
            _cmbIcon.Items.Add(icon);
        _cmbIcon.SelectedIndex = 0;

        _cmbDefaultModel.Width = 260;
        _cmbDefaultModel.DropDownStyle = ComboBoxStyle.DropDownList;
        PopulateModelDropdown();

        _chkFavorite.Text = "Buddy Lense";
        _chkFavorite.AutoSize = true;

        _txtId.Dock = DockStyle.Fill;
        _txtId.ReadOnly = true;
        _txtId.BackColor = SystemColors.ControlLight;

        // Accent hex field with live color swatch
        Panel accentRow = new() { Margin = new Padding(0), AutoSize = true };
        _txtAccentHex.Width = 80;
        _pnlAccentPreview.Width = 24;
        _pnlAccentPreview.Height = 24;
        _pnlAccentPreview.BorderStyle = BorderStyle.FixedSingle;
        _pnlAccentPreview.BackColor = SystemColors.Control;
        _pnlAccentPreview.Cursor = Cursors.Hand;
        accentRow.Controls.Add(_pnlAccentPreview);
        accentRow.Controls.Add(_txtAccentHex);
        _txtAccentHex.Dock = DockStyle.Left;
        _pnlAccentPreview.Dock = DockStyle.Left;

        // Combined options row: Icon | Accent
        FlowLayoutPanel optionsRow = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };

        Label lblIcon = new() { Text = "Icon", AutoSize = true, Margin = new Padding(0, 6, 4, 0) };
        Label lblAccent = new() { Text = "Accent", AutoSize = true, Margin = new Padding(8, 6, 4, 0) };
        _chkFavorite.Margin = new Padding(8, 4, 0, 0);

        optionsRow.Controls.Add(lblIcon);
        optionsRow.Controls.Add(_cmbIcon);
        optionsRow.Controls.Add(lblAccent);
        optionsRow.Controls.Add(_txtAccentHex);
        optionsRow.Controls.Add(_pnlAccentPreview);

        // Model + Buddy Lense row
        FlowLayoutPanel modelRow = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };

        modelRow.Controls.Add(_cmbDefaultModel);
        modelRow.Controls.Add(_chkFavorite);

        ConfigureEditorTextBox(_txtSystemPrompt);
        ConfigureEditorTextBox(_txtMessage);

        form.Controls.Add(_txtPersonaName, 1, 0);
        form.Controls.Add(_cmbCategory, 1, 1);
        form.Controls.Add(_txtId, 1, 2);
        form.Controls.Add(optionsRow, 1, 3);
        form.Controls.Add(modelRow, 1, 4);
        form.Controls.Add(_txtSystemPrompt, 1, 5);
        form.Controls.Add(_txtMessage, 1, 6);

        TableLayoutPanel footer = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        form.Controls.Add(footer, 0, 7);
        form.SetColumnSpan(footer, 2);

        _lblPath.Dock = DockStyle.Fill;
        _lblPath.TextAlign = ContentAlignment.MiddleLeft;
        footer.Controls.Add(_lblPath, 0, 0);

        FlowLayoutPanel footerButtons = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        footer.Controls.Add(footerButtons, 0, 1);

        ConfigureButton(_btnClose, "Close", 90);
        ConfigureButton(_btnSave, "Save", 110);
        footerButtons.Controls.Add(_btnClose);
        footerButtons.Controls.Add(_btnSave);
        Form1_Resize(this, null);
    }

    private static void AddRowLabel(TableLayoutPanel form, string text, int row)
    {
        form.Controls.Add(new Label { Text = text, TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 0, row);
    }

    private static void ConfigureButton(Button button, string text, int width)
    {
        button.Text = text;
        button.Width = width;
        button.Height = 32;
        button.Margin = new Padding(0, 0, 8, 8);
    }

    private static void ConfigureEditorTextBox(TextBox textBox)
    {
        textBox.Dock = DockStyle.Fill;
        textBox.Multiline = true;
        textBox.AcceptsReturn = true;
        textBox.AcceptsTab = true;
        textBox.ScrollBars = ScrollBars.Both;
        textBox.WordWrap = false;
        textBox.Font = new Font("Segoe UI", 10F);
    }

    private static ToolStripButton CreateToolStripButton(string text, Action onClick)
    {
        ToolStripButton button = new(text);
        button.Click += (_, _) => onClick();
        return button;
    }

    private void WireEvents()
    {
        _treePersonas.BeforeSelect += (_, _) => ApplyEditorChanges();
        _treePersonas.AfterSelect += (_, _) => LoadSelectedRecordIntoEditors();
        _btnSave.Click += (_, _) => SaveAll();
        _btnClose.Click += (_, _) => Close();

        _txtPersonaName.TextChanged += Editor_TextChanged;
        _cmbCategory.SelectedIndexChanged += Editor_TextChanged;
        _cmbIcon.SelectedIndexChanged += Editor_TextChanged;
        _txtAccentHex.TextChanged += AccentHex_TextChanged;
        _cmbDefaultModel.SelectedIndexChanged += Editor_TextChanged;
        _txtSystemPrompt.TextChanged += Editor_TextChanged;
        _txtMessage.TextChanged += Editor_TextChanged;
        _chkFavorite.CheckedChanged += Editor_TextChanged;

        _txtPersonaName.Leave += Editor_FocusLost;
        _cmbCategory.Leave += Editor_FocusLost;
        _cmbIcon.Leave += Editor_FocusLost;
        _txtAccentHex.Leave += Editor_FocusLost;
        _cmbDefaultModel.Leave += Editor_FocusLost;
        _txtSystemPrompt.Leave += Editor_FocusLost;
        _txtMessage.Leave += Editor_FocusLost;

        // Search & filter events
        _txtSearch.TextChanged += (_, _) => ApplyFilter();
        _cmbCategoryFilter.SelectedIndexChanged += (_, _) => ApplyFilter();
        _chkFavoritesOnly.CheckedChanged += (_, _) => ApplyFilter();

        // Accent color swatch click opens color picker
        _pnlAccentPreview.Click += AccentPreview_Click;

        // Keyboard shortcuts
        KeyDown += PersonaManagerForm_KeyDown;

        FormClosing += PersonaManagerForm_FormClosing;
    }

    private void PersonaManagerForm_KeyDown(object? sender, KeyEventArgs e)
    {
        switch (e)
        {
            case { Control: true, KeyCode: Keys.F }:
                _txtSearch.Focus();
                _txtSearch.SelectAll();
                e.Handled = true;
                e.SuppressKeyPress = true;
                break;

            case { Control: true, KeyCode: Keys.S }:
                SaveAll();
                e.Handled = true;
                e.SuppressKeyPress = true;
                break;

            case { Control: true, KeyCode: Keys.N }:
                CreateNewRecord();
                e.Handled = true;
                e.SuppressKeyPress = true;
                break;

            case { Control: true, KeyCode: Keys.D }:
                CloneSelectedRecord();
                e.Handled = true;
                e.SuppressKeyPress = true;
                break;

            case { KeyCode: Keys.Escape } when _txtSearch.Focused && !string.IsNullOrEmpty(_txtSearch.Text):
                _txtSearch.Clear();
                e.Handled = true;
                e.SuppressKeyPress = true;
                break;

            case { KeyCode: Keys.Escape }:
                Close();
                e.Handled = true;
                e.SuppressKeyPress = true;
                break;

            case { KeyCode: Keys.Delete, Control: true }:
                DeleteSelectedRecord();
                e.Handled = true;
                e.SuppressKeyPress = true;
                break;
        }
    }

    private void AccentHex_TextChanged(object? sender, EventArgs e)
    {
        if (_isLoading)
            return;

        _isDirty = true;
        UpdateAccentPreview();
    }

    private void UpdateAccentPreview()
    {
        string hex = _txtAccentHex.Text.Trim();
        if (TryParseHexColor(hex, out Color color))
        {
            _pnlAccentPreview.BackColor = color;
        }
        else
        {
            _pnlAccentPreview.BackColor = SystemColors.Control;
        }
    }

    private static bool TryParseHexColor(string hex, out Color color)
    {
        color = Color.Empty;

        if (string.IsNullOrWhiteSpace(hex))
            return false;

        try
        {
            if (hex.StartsWith('#'))
                hex = hex[1..];

            if (hex.Length is not (3 or 6))
                return false;

            if (hex.Length == 3)
                hex = string.Create(6, hex, static (span, h) =>
                {
                    span[0] = span[1] = h[0];
                    span[2] = span[3] = h[1];
                    span[4] = span[5] = h[2];
                });

            int rgb = Convert.ToInt32(hex, 16);
            color = Color.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void AccentPreview_Click(object? sender, EventArgs e)
    {
        using ColorDialog dialog = new();

        if (TryParseHexColor(_txtAccentHex.Text.Trim(), out Color current))
            dialog.Color = current;

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _txtAccentHex.Text = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
        }
    }

    private void Form1_Resize(object? sender, EventArgs e)
    {
        ResizePanel1();
    }

    private void Editor_FocusLost(object? sender, EventArgs e)
    {
        ApplyEditorChanges();
    }

    private void Editor_TextChanged(object? sender, EventArgs e)
    {
        if (_isLoading)
            return;

        _isDirty = true;
    }

    private void ResizePanel1()
    {
        _split.SplitterDistance = (int)(_split.Width * 0.25);
    }

    private void PersonaManagerForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!_isDirty)
            return;

        DialogResult result = MessageBox.Show(
            this,
            "You have unsaved persona changes. Save before closing?",
            "Persona Manager",
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Question);

        if (result == DialogResult.Cancel)
        {
            e.Cancel = true;
            return;
        }

        if (result == DialogResult.Yes)
        {
            try
            {
                SaveAll();
            }
            catch
            {
                e.Cancel = true;
            }
        }
    }

    private void LoadRecords(string? selectId)
    {
        _isLoading = true;
        try
        {
            _records.Clear();
            _records.AddRange(_personaService.LoadOrSeed().Select(x => x.Clone()));
            ApplyFilter(selectId);
            _lblPath.Text = "JSON file: " + _personaService.GetStoragePath();
            _isDirty = false;
            UpdateWindowTitle();
        }
        finally
        {
            _isLoading = false;
        }
    }

    private static string NormalizeCategory(string? category)
    {
        return string.IsNullOrWhiteSpace(category) ? "General" : category;
    }

    private void ApplyFilter(string? selectId = null)
    {
        string searchText = _txtSearch.Text.Trim();
        string categoryFilter = _cmbCategoryFilter.SelectedItem as string ?? "All Categories";
        bool favoritesOnly = _chkFavoritesOnly.Checked;

        _filteredRecords.Clear();

        foreach (PersonaRecord record in _records)
        {
            if (favoritesOnly && !record.Favorite)
                continue;

            if (categoryFilter != "All Categories")
            {
                string recordCategory = NormalizeCategory(record.Category);
                if (!string.Equals(recordCategory, categoryFilter, StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            if (!string.IsNullOrEmpty(searchText))
            {
                bool matches =
                    record.PersonaName.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    record.Category.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    record.SystemPrompt.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    record.MessageTemplate.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    record.Icon.Contains(searchText, StringComparison.OrdinalIgnoreCase);

                if (!matches)
                    continue;
            }

            _filteredRecords.Add(record);
        }

        RebuildTree(selectId ?? GetSelectedRecord()?.Id);
        UpdateStats();
    }

    private void UpdateStats()
    {
        int total = _records.Count;
        int shown = _filteredRecords.Count;
        int favorites = _records.Count(r => r.Favorite);
        int categories = _records
            .Select(r => NormalizeCategory(r.Category))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        _lblStats.Text = shown == total
            ? $"{total} personas · {favorites} ★ · {categories} categories"
            : $"{shown} of {total} shown · {favorites} ★ · {categories} categories";
    }

    private void RebuildTree(string? selectId)
    {
        _treePersonas.BeginUpdate();
        _treePersonas.Nodes.Clear();

        // Group filtered records by category
        var groups = _filteredRecords
            .GroupBy(r => NormalizeCategory(r.Category), StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        TreeNode? nodeToSelect = null;

        foreach (var group in groups)
        {
            TreeNode categoryNode = new($"{group.Key} ({group.Count()})");
            categoryNode.Tag = null;

            foreach (PersonaRecord record in group)
            {
                string favorite = record.Favorite ? "★ " : string.Empty;
                string label = $"{favorite}{record.Icon} {record.PersonaName}";
                TreeNode personaNode = new(label) { Tag = record };
                categoryNode.Nodes.Add(personaNode);

                if (!string.IsNullOrWhiteSpace(selectId) &&
                    string.Equals(record.Id, selectId, StringComparison.OrdinalIgnoreCase))
                {
                    nodeToSelect = personaNode;
                }
            }

            _treePersonas.Nodes.Add(categoryNode);
        }

        _treePersonas.EndUpdate();

        if (nodeToSelect != null)
        {
            nodeToSelect.Parent?.Expand();
            _treePersonas.SelectedNode = nodeToSelect;
        }
        else if (_treePersonas.Nodes.Count > 0 && _treePersonas.Nodes[0].Nodes.Count > 0)
        {
            _treePersonas.Nodes[0].Expand();
            _treePersonas.SelectedNode = _treePersonas.Nodes[0].Nodes[0];
        }
        else
        {
            UpdateToolbarState(false);
            ClearEditors();
        }
    }

    private void ToggleExpandAll()
    {
        if (_treePersonas.Nodes.Count == 0)
            return;

        bool anyCollapsed = false;
        foreach (TreeNode node in _treePersonas.Nodes)
        {
            if (!node.IsExpanded)
            {
                anyCollapsed = true;
                break;
            }
        }

        ToolStripItem? expandButton = _toolStrip1.Items.Cast<ToolStripItem>()
            .FirstOrDefault(i => i.Text is "Expand All" or "Collapse All");

        _treePersonas.BeginUpdate();
        if (anyCollapsed)
        {
            _treePersonas.ExpandAll();
            if (expandButton != null) expandButton.Text = "Collapse All";
        }
        else
        {
            _treePersonas.CollapseAll();
            if (expandButton != null) expandButton.Text = "Expand All";

            // Re-expand and select the current node's parent so it stays visible
            TreeNode? selected = _treePersonas.SelectedNode;
            selected?.Parent?.Expand();
        }
        _treePersonas.EndUpdate();

        // Ensure selected node is visible
        _treePersonas.SelectedNode?.EnsureVisible();
    }

    private void UpdateToolbarState(bool hasSelection)
    {
        _tsbClone.Enabled = hasSelection;
        _tsbDelete.Enabled = hasSelection;
    }

    private void LoadSelectedRecordIntoEditors()
    {
        if (_isLoading)
            return;

        _isLoading = true;
        try
        {
            PersonaRecord? record = GetSelectedRecord();
            UpdateToolbarState(record != null);
            if (record == null)
            {
                ClearEditors();
                return;
            }

            _txtPersonaName.Text = record.PersonaName;
            SelectCategory(record.Category);
            _txtId.Text = record.Id;
            SelectIcon(record.Icon);
            _txtAccentHex.Text = record.AccentHex;
            SelectModel(record.DefaultModel);
            _chkFavorite.Checked = record.Favorite;
            _txtSystemPrompt.Text = record.SystemPrompt;
            _txtMessage.Text = record.MessageTemplate;
            UpdateAccentPreview();
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void SelectIcon(string? icon)
    {
        if (string.IsNullOrWhiteSpace(icon))
        {
            _cmbIcon.SelectedIndex = 0;
            return;
        }

        int idx = _cmbIcon.FindStringExact(icon);
        _cmbIcon.SelectedIndex = idx >= 0 ? idx : 0;
    }

    private void SelectModel(string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            _cmbDefaultModel.SelectedIndex = _cmbDefaultModel.Items.Count > 0 ? 0 : -1;
            return;
        }

        for (int i = 0; i < _cmbDefaultModel.Items.Count; i++)
        {
            if (_cmbDefaultModel.Items[i] is ModelDropdownItem item &&
                string.Equals(item.ModelName, model, StringComparison.OrdinalIgnoreCase))
            {
                _cmbDefaultModel.SelectedIndex = i;
                return;
            }
        }

        // Model not found in any provider — add a "Not Set" entry so it's still visible
        ModelDropdownItem notSet = new("Not Set", model);
        _cmbDefaultModel.Items.Add(notSet);
        _cmbDefaultModel.SelectedItem = notSet;
    }

    private void PopulateModelDropdown()
    {
        _cmbDefaultModel.Items.Clear();

        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        List<AiProviderDefinition> providers = _providerService.LoadOrSeed();

        foreach (AiProviderDefinition provider in providers)
        {
            foreach (string modelName in AiProviderService.GetModelNames(provider))
            {
                if (seen.Add(modelName))
                    _cmbDefaultModel.Items.Add(new ModelDropdownItem(provider.Name, modelName));
            }
        }

        if (_cmbDefaultModel.Items.Count > 0)
            _cmbDefaultModel.SelectedIndex = 0;
    }

    private void SelectCategory(string? category)
    {
        string normalized = NormalizeCategory(category);
        int idx = _cmbCategory.FindStringExact(normalized);
        _cmbCategory.SelectedIndex = idx >= 0 ? idx : 0;
    }

    private void ApplyEditorChanges()
    {
        if (_isLoading)
            return;

        PersonaRecord? record = GetSelectedRecord();
        if (record == null)
            return;

        record.PersonaName = _txtPersonaName.Text.Trim();
        record.Category = _cmbCategory.SelectedItem as string ?? "General";
        record.Icon = _cmbIcon.SelectedItem as string ?? "🧠";
        record.AccentHex = _txtAccentHex.Text.Trim();
        record.DefaultModel = (_cmbDefaultModel.SelectedItem as ModelDropdownItem)?.ModelName ?? "";
        record.Favorite = _chkFavorite.Checked;
        record.SystemPrompt = _txtSystemPrompt.Text.Trim();
        record.MessageTemplate = _txtMessage.Text.Trim();

        // Refresh tree display without losing selection — guard against re-entrancy
        _isLoading = true;
        try
        {
            RefreshSelectedTreeNode();
        }
        finally
        {
            _isLoading = false;
        }

        MarkDirty();
    }

    private void RefreshSelectedTreeNode()
    {
        TreeNode? node = _treePersonas.SelectedNode;
        if (node?.Tag is not PersonaRecord record)
            return;

        string favorite = record.Favorite ? "★ " : string.Empty;
        node.Text = $"{favorite}{record.Icon} {record.PersonaName}";

        // Update the parent category node label if needed
        string currentCategory = NormalizeCategory(record.Category);
        if (node.Parent != null)
        {
            string parentLabel = node.Parent.Text;
            string expectedPrefix = $"{currentCategory}";
            if (!parentLabel.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                // Category changed — full rebuild needed
                _isLoading = false;
                ApplyFilter(record.Id);
                _isLoading = true;
            }
        }
    }

    private void CreateNewRecord()
    {
        PersonaRecord record = new()
        {
            PersonaName = "New Persona",
            Category = "General",
            Icon = "🧠",
            DefaultModel = "gpt-4.1-mini-637176",
            MessageTemplate = "Describe the task you want me to help with.",
            SystemPrompt = "Act as a helpful enterprise assistant."
        };

        _records.Add(record);
        ApplyFilter(record.Id);
        MarkDirty();
    }

    private void CloneSelectedRecord()
    {
        PersonaRecord? selected = GetSelectedRecord();
        if (selected == null)
            return;

        PersonaRecord clone = selected.Clone();
        clone.Id = Guid.NewGuid().ToString("n");
        clone.PersonaName = selected.PersonaName + " Copy";

        int masterIndex = _records.IndexOf(selected);
        _records.Insert(masterIndex + 1, clone);
        ApplyFilter(clone.Id);
        MarkDirty();
    }

    private void DeleteSelectedRecord()
    {
        PersonaRecord? selected = GetSelectedRecord();
        if (selected == null)
            return;

        if (MessageBox.Show(this, $"Delete '{selected.PersonaName}'?", "Delete Persona", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        int filteredIndex = _filteredRecords.IndexOf(selected);
        _records.Remove(selected);
        _filteredRecords.Remove(selected);

        string? nextId = _filteredRecords.ElementAtOrDefault(Math.Max(0, filteredIndex - 1))?.Id;
        ApplyFilter(nextId);
        MarkDirty();
    }

    private void MoveSelectedRecord(int direction)
    {
        PersonaRecord? selected = GetSelectedRecord();
        if (selected == null)
            return;

        int current = _records.IndexOf(selected);
        if (current < 0)
            return;

        int target = current + direction;
        if (target < 0 || target >= _records.Count)
            return;

        _records.RemoveAt(current);
        _records.Insert(target, selected);
        ApplyFilter(selected.Id);
        MarkDirty();
    }

    private void ReloadWithPrompt()
    {
        if (_isDirty)
        {
            DialogResult result = MessageBox.Show(this, "Discard unsaved changes and reload personas from disk?", "Reload Personas", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result != DialogResult.Yes)
                return;
        }

        LoadRecords(GetSelectedRecord()?.Id);
    }

    private void OpenJsonFile()
    {
        _personaService.EnsureFileExists();
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = _personaService.GetStoragePath(),
            UseShellExecute = true
        });
    }

    private void ImportPersonas()
    {
        using OpenFileDialog dialog = new()
        {
            Filter = "JSON Files|*.json|All Files|*.*",
            Title = "Import Personas"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        DialogResult merge = MessageBox.Show(this, "Merge imported personas with existing personas? Click No to replace current working list.", "Import Personas", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        if (merge == DialogResult.Cancel)
            return;

        List<PersonaRecord> imported = _personaService.Import(dialog.FileName, merge == DialogResult.Yes);
        _records.Clear();
        _records.AddRange(imported.Select(x => x.Clone()));
        ApplyFilter(_records.FirstOrDefault()?.Id);
        MarkDirty();
    }

    private void ExportPersonas()
    {
        using SaveFileDialog dialog = new()
        {
            Filter = "JSON Files|*.json",
            FileName = "BuddyAI.personas.export.json",
            Title = "Export Personas"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        _personaService.Export(dialog.FileName, _records);
        MessageBox.Show(this, "Personas exported successfully.", "Export Personas", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void DownloadFromCatalog()
    {
        PersonaCatalogService catalogService = new();
        using PersonaCatalogBrowserForm browser = new(catalogService, _personaService);

        browser.ShowDialog(this);

        if (browser.DownloadedPersonas.Count == 0)
            return;

        ApplyEditorChanges();
        _personaService.BackupPersonasFile();

        List<PersonaRecord> merged = PersonaCatalogService.MergeWithExisting(
            _records, browser.DownloadedPersonas);

        _records.Clear();
        _records.AddRange(merged);
        ApplyFilter(_records.FirstOrDefault()?.Id);
        MarkDirty();
    }

    private void SaveAll()
    {
        try
        {
            ApplyEditorChanges();
            ValidateBeforeSave();
            _personaService.Save(_records);
            _isDirty = false;
            UpdateWindowTitle();
            DialogResult = DialogResult.OK;
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(this, ex.Message, "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ValidateBeforeSave()
    {
        foreach (PersonaRecord record in _records)
        {
            if (string.IsNullOrWhiteSpace(record.PersonaName))
                throw new InvalidOperationException("Persona name is required.");

            if (string.IsNullOrWhiteSpace(record.SystemPrompt))
                throw new InvalidOperationException($"System prompt is required for '{record.PersonaName}'.");
        }
    }

    private PersonaRecord? GetSelectedRecord()
    {
        return _treePersonas.SelectedNode?.Tag as PersonaRecord;
    }

    private void ClearEditors()
    {
        _txtPersonaName.Clear();
        _cmbCategory.SelectedIndex = 0;
        _txtId.Clear();
        _cmbIcon.SelectedIndex = 0;
        _txtAccentHex.Clear();
        _cmbDefaultModel.SelectedIndex = -1;
        _chkFavorite.Checked = false;
        _txtSystemPrompt.Clear();
        _txtMessage.Clear();
        _pnlAccentPreview.BackColor = SystemColors.Control;
    }

    private void MarkDirty()
    {
        _isDirty = true;
        UpdateWindowTitle();
    }

    private void UpdateWindowTitle()
    {
        Text = _isDirty ? "BuddyAI Persona Manager *" : "BuddyAI Persona Manager";
    }

    private sealed class ModelDropdownItem
    {
        public string ProviderName { get; }
        public string ModelName { get; }

        public ModelDropdownItem(string providerName, string modelName)
        {
            ProviderName = providerName;
            ModelName = modelName;
        }

        public override string ToString() => $"{ProviderName} → {ModelName}";
    }
}
