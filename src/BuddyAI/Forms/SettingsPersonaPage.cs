using System.Diagnostics;
using BuddyAI.Models;
using BuddyAI.Services;

namespace BuddyAI.Forms;

public sealed class SettingsPersonaPage : UserControl
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
        "??",
        "??",
        "???",
        "???",
        "??",
        "??",
        "??",
        "??",
        "??",
        "??"
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
    private readonly Label _lblPath = new();
    private readonly SplitContainer _split = new();

    private readonly TextBox _txtSearch = new();
    private readonly ComboBox _cmbCategoryFilter = new();
    private readonly CheckBox _chkFavoritesOnly = new();
    private readonly Panel _pnlAccentPreview = new();
    private readonly Label _lblStats = new();
    private readonly ToolStrip _toolStrip1 = new();
    private readonly ToolStrip _toolStrip2 = new();
    private ToolStripButton _tsbClone = null!;
    private ToolStripButton _tsbDelete = null!;

    private readonly List<PersonaRecord> _records = new();
    private readonly List<PersonaRecord> _filteredRecords = new();
    private bool _isLoading;

    public bool IsDirty { get; private set; }

    public event EventHandler? DirtyChanged;

    public SettingsPersonaPage(PersonaService personaService, AiProviderService providerService)
    {
        _personaService = personaService;
        _providerService = providerService;

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
        _txtSearch.PlaceholderText = "?? Search personas...";
        filterBar.Controls.Add(_txtSearch, 0, 0);

        _cmbCategoryFilter.Dock = DockStyle.Fill;
        _cmbCategoryFilter.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbCategoryFilter.Items.Add("All Categories");
        foreach (string cat in HardcodedCategories)
            _cmbCategoryFilter.Items.Add(cat);
        _cmbCategoryFilter.SelectedIndex = 0;
        filterBar.Controls.Add(_cmbCategoryFilter, 1, 0);

        _chkFavoritesOnly.Dock = DockStyle.Fill;
        _chkFavoritesOnly.Text = "? Only";
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
            CreateToolStripButton("?", () => MoveSelectedRecord(-1)),
            CreateToolStripButton("?", () => MoveSelectedRecord(1)),
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

        left.Controls.Add(_treePersonas);
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
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        form.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
        form.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
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
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        form.Controls.Add(footer, 0, 7);
        form.SetColumnSpan(footer, 2);

        _lblPath.Dock = DockStyle.Fill;
        _lblPath.TextAlign = ContentAlignment.MiddleLeft;
        footer.Controls.Add(_lblPath, 0, 0);

        ConfigureButton(_btnSave, "Save Personas", 140);
        footer.Controls.Add(_btnSave, 1, 0);

        ResizePanels();
        Resize += (_, _) => ResizePanels();
    }

    private static void AddRowLabel(TableLayoutPanel form, string text, int row)
    {
        form.Controls.Add(
            new Label { Text = text, TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 0, row);
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
        _btnSave.Click += (_, _) => ExecuteGuarded(SaveAll, "Save personas");

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

        _txtSearch.TextChanged += (_, _) => ApplyFilter();
        _cmbCategoryFilter.SelectedIndexChanged += (_, _) => ApplyFilter();
        _chkFavoritesOnly.CheckedChanged += (_, _) => ApplyFilter();

        _pnlAccentPreview.Click += AccentPreview_Click;
    }

    private void ResizePanels()
    {
        if (_split.Width > 0)
            _split.SplitterDistance = (int)(_split.Width * 0.25);
    }

    private void Editor_FocusLost(object? sender, EventArgs e) => ApplyEditorChanges();

    private void Editor_TextChanged(object? sender, EventArgs e)
    {
        if (_isLoading)
            return;

        MarkDirty();
    }

    private void AccentHex_TextChanged(object? sender, EventArgs e)
    {
        if (_isLoading)
            return;

        MarkDirty();
        UpdateAccentPreview();
    }

    private void UpdateAccentPreview()
    {
        string hex = _txtAccentHex.Text.Trim();
        _pnlAccentPreview.BackColor = TryParseHexColor(hex, out Color color)
            ? color
            : SystemColors.Control;
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

        if (dialog.ShowDialog(FindForm()) == DialogResult.OK)
            _txtAccentHex.Text = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
    }

    private void MarkDirty()
    {
        if (_isLoading)
            return;

        if (!IsDirty)
        {
            IsDirty = true;
            DirtyChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool TrySave()
    {
        try
        {
            SaveAll();
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(FindForm(), ex.Message, "Validation Error", MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }
    }

    public bool PromptSaveIfDirty()
    {
        if (!IsDirty)
            return true;

        DialogResult result = MessageBox.Show(
            FindForm(),
            "You have unsaved persona changes. Save before continuing?",
            "Personas",
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Question);

        if (result == DialogResult.Cancel)
            return false;

        if (result == DialogResult.Yes)
            return TrySave();

        return true;
    }

    private void LoadRecords(string? selectId)
    {
        _isLoading = true;
        try
        {
            _records.Clear();
            _records.AddRange(_personaService.LoadOrSeed().Select(x => x.Clone()));
            ApplyFilter(selectId);
            _lblPath.Text = "JSON: " + _personaService.GetStoragePath();
            IsDirty = false;
            DirtyChanged?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private static string NormalizeCategory(string? category) =>
        string.IsNullOrWhiteSpace(category) ? "General" : category;

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
            ? $"{total} personas � {favorites} ? � {categories} categories"
            : $"{shown} of {total} shown � {favorites} ? � {categories} categories";
    }

    private void RebuildTree(string? selectId)
    {
        _treePersonas.BeginUpdate();
        _treePersonas.Nodes.Clear();

        var groups = _filteredRecords
            .GroupBy(r => NormalizeCategory(r.Category), StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        TreeNode? nodeToSelect = null;

        foreach (var group in groups)
        {
            TreeNode categoryNode = new($"?? {group.Key} ({group.Count()})");
            categoryNode.Tag = null;

            foreach (PersonaRecord record in group)
            {
                string favorite = record.Favorite ? "? " : string.Empty;
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

            TreeNode? selected = _treePersonas.SelectedNode;
            selected?.Parent?.Expand();
        }

        _treePersonas.EndUpdate();
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
        record.Icon = _cmbIcon.SelectedItem as string ?? "??";
        record.AccentHex = _txtAccentHex.Text.Trim();
        record.DefaultModel = (_cmbDefaultModel.SelectedItem as ModelDropdownItem)?.ModelName ?? "";
        record.Favorite = _chkFavorite.Checked;
        record.SystemPrompt = _txtSystemPrompt.Text.Trim();
        record.MessageTemplate = _txtMessage.Text.Trim();

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

        string favorite = record.Favorite ? "? " : string.Empty;
        node.Text = $"{favorite}{record.Icon} {record.PersonaName}";

        string currentCategory = NormalizeCategory(record.Category);
        if (node.Parent != null)
        {
            string parentLabel = node.Parent.Text;
            string expectedPrefix = $"?? {currentCategory}";
            if (!parentLabel.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
            {
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
            Icon = "??",
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

        if (MessageBox.Show(FindForm(), $"Delete '{selected.PersonaName}'?", "Delete Persona",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
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
        if (IsDirty)
        {
            DialogResult result = MessageBox.Show(FindForm(),
                "Discard unsaved changes and reload personas from disk?",
                "Reload Personas", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result != DialogResult.Yes)
                return;
        }

        LoadRecords(GetSelectedRecord()?.Id);
    }

    private void OpenJsonFile()
    {
        _personaService.EnsureFileExists();
        Process.Start(new ProcessStartInfo
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

        if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
            return;

        DialogResult merge = MessageBox.Show(FindForm(),
            "Merge imported personas with existing personas? Click No to replace current working list.",
            "Import Personas", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
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

        if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
            return;

        _personaService.Export(dialog.FileName, _records);
        MessageBox.Show(FindForm(), "Personas exported successfully.", "Export Personas",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void SaveAll()
    {
        ApplyEditorChanges();
        ValidateBeforeSave();
        _personaService.Save(_records);
        IsDirty = false;
        DirtyChanged?.Invoke(this, EventArgs.Empty);
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

    private PersonaRecord? GetSelectedRecord() =>
        _treePersonas.SelectedNode?.Tag as PersonaRecord;

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

    private void ExecuteGuarded(Action action, string operationName)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            MessageBox.Show(FindForm(),
                $"{operationName} failed.{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                "Personas", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
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

        public override string ToString() => $"{ProviderName} ? {ModelName}";
    }
}
