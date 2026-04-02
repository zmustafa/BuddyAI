using System.Diagnostics;
using BuddyAI.Models;
using BuddyAI.Services;

namespace BuddyAI.Forms;

public sealed class TemplateEditorForm : Form
{
    private static readonly string[] HardcodedCategories =
    [
        "General",
        "GCP",
        "Azure",
        "AWS",
        "OCI",
        "DevOps",
        "Security",
        "Networking",
        "Database",
        "Scripting",
        "PowerShell",
        "Automation",
        "Architecture",
        "Compliance",
        "Monitoring"
    ];

    private readonly PromptService _promptService;
    private readonly TreeView _treeTemplates = new();
    private readonly TextBox _txtSearch = new();
    private readonly ComboBox _cmbCategoryFilter = new();
    private readonly Label _lblStats = new();
    private readonly TextBox _txtId = new();
    private readonly ComboBox _cmbCategory = new();
    private readonly TextBox _txtName = new();
    private readonly TextBox _txtTemplateText = new();
    private readonly Button _btnNew = new();
    private readonly Button _btnClone = new();
    private readonly Button _btnDelete = new();
    private readonly Button _btnMoveUp = new();
    private readonly Button _btnMoveDown = new();
    private readonly Button _btnReload = new();
    private readonly Button _btnOpenJson = new();
    private readonly Button _btnImport = new();
    private readonly Button _btnExport = new();
    private readonly Button _btnSave = new();
    private readonly Button _btnClose = new();
    private readonly Label _lblPath = new();
    private readonly SplitContainer _split = new();

    private readonly List<PromptItem> _records = new();
    private readonly List<PromptItem> _filteredRecords = new();
    private bool _isLoading;
    private bool _isDirty;

    public TemplateEditorForm(PromptService promptService)
    {
        _promptService = promptService;

        Text = "BuddyAI Template Editor";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1180, 740);
        Size = new Size(1400, 860);
        AutoScaleMode = AutoScaleMode.Dpi;
        KeyPreview = true;
        Resize += Form1_Resize;
        BuildUi();
        WireEvents();
        LoadRecords(selectId: null);
        Form1_Resize(this, null);
    }

    private void Form1_Resize(object? sender, EventArgs e)
    {
        ResizePanel1();
    }

    private void ResizePanel1()
    {
        _split.SplitterDistance = (int)(_split.Width * 0.30);
    }

    private void BuildUi()
    {
        _split.Dock = DockStyle.Fill;
        _split.FixedPanel = FixedPanel.Panel1;
        _split.SplitterDistance = 390;
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
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(0, 0, 0, 4),
            Margin = new Padding(0)
        };
        filterBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        filterBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        filterBar.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        filterBar.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

        _txtSearch.Dock = DockStyle.Fill;
        _txtSearch.PlaceholderText = "🔍 Search templates... (Ctrl+F)";
        filterBar.Controls.Add(_txtSearch, 0, 0);

        _cmbCategoryFilter.Dock = DockStyle.Fill;
        _cmbCategoryFilter.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbCategoryFilter.Items.Add("All Categories");
        foreach (string cat in HardcodedCategories)
            _cmbCategoryFilter.Items.Add(cat);
        _cmbCategoryFilter.SelectedIndex = 0;
        filterBar.Controls.Add(_cmbCategoryFilter, 1, 0);

        _lblStats.Dock = DockStyle.Fill;
        _lblStats.TextAlign = ContentAlignment.MiddleLeft;
        _lblStats.Text = "0 templates";
        filterBar.Controls.Add(_lblStats, 0, 1);
        filterBar.SetColumnSpan(_lblStats, 2);

        _treeTemplates.Dock = DockStyle.Fill;
        _treeTemplates.HideSelection = false;
        _treeTemplates.FullRowSelect = true;
        _treeTemplates.ShowLines = true;
        _treeTemplates.ShowPlusMinus = true;
        _treeTemplates.ShowRootLines = true;

        FlowLayoutPanel leftButtons = new()
        {
            Dock = DockStyle.Bottom,
            Height = 112,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };

        // WinForms dock z-order: last added gets highest layout priority.
        left.Controls.Add(_treeTemplates);
        left.Controls.Add(leftButtons);
        left.Controls.Add(filterBar);

        ConfigureButton(_btnNew, "New", 90);
        ConfigureButton(_btnClone, "Clone", 90);
        ConfigureButton(_btnDelete, "Delete", 90);
        ConfigureButton(_btnMoveUp, "Move Up", 90);
        ConfigureButton(_btnMoveDown, "Move Down", 100);
        ConfigureButton(_btnReload, "Reload", 90);
        ConfigureButton(_btnOpenJson, "Open JSON", 110);
        ConfigureButton(_btnImport, "Import", 90);
        ConfigureButton(_btnExport, "Export", 90);

        leftButtons.Controls.AddRange(new Control[]
        {
            _btnNew, _btnClone, _btnDelete, _btnMoveUp, _btnMoveDown,
            _btnReload, _btnOpenJson, _btnImport, _btnExport
        });

        TableLayoutPanel form = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 6,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        form.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        right.Controls.Add(form);

        AddRowLabel(form, "Name", 0);
        AddRowLabel(form, "Category", 1);
        AddRowLabel(form, "Id", 2);
        AddRowLabel(form, "Template", 3);

        _txtName.Dock = DockStyle.Fill;

        _cmbCategory.Dock = DockStyle.Fill;
        _cmbCategory.DropDownStyle = ComboBoxStyle.DropDownList;
        foreach (string cat in HardcodedCategories)
            _cmbCategory.Items.Add(cat);
        _cmbCategory.SelectedIndex = 0;

        _txtId.Dock = DockStyle.Fill;
        _txtId.ReadOnly = true;
        _txtId.BackColor = SystemColors.ControlLight;

        ConfigureEditorTextBox(_txtTemplateText);

        form.Controls.Add(_txtName, 1, 0);
        form.Controls.Add(_cmbCategory, 1, 1);
        form.Controls.Add(_txtId, 1, 2);
        form.Controls.Add(_txtTemplateText, 1, 3);

        _lblPath.Dock = DockStyle.Fill;
        _lblPath.TextAlign = ContentAlignment.MiddleLeft;
        form.Controls.Add(_lblPath, 0, 4);
        form.SetColumnSpan(_lblPath, 2);

        FlowLayoutPanel footerButtons = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        form.Controls.Add(footerButtons, 0, 5);
        form.SetColumnSpan(footerButtons, 2);

        ConfigureButton(_btnClose, "Close", 90);
        ConfigureButton(_btnSave, "Save", 110);
        footerButtons.Controls.Add(_btnClose);
        footerButtons.Controls.Add(_btnSave);
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

    private void WireEvents()
    {
        _txtSearch.TextChanged += (_, _) => ApplyFilter();
        _cmbCategoryFilter.SelectedIndexChanged += (_, _) => ApplyFilter();
        _treeTemplates.AfterSelect += (_, _) => LoadSelectedRecordIntoEditors();
        _btnNew.Click += (_, _) => CreateNewRecord();
        _btnClone.Click += (_, _) => CloneSelectedRecord();
        _btnDelete.Click += (_, _) => DeleteSelectedRecord();
        _btnMoveUp.Click += (_, _) => MoveSelectedRecord(-1);
        _btnMoveDown.Click += (_, _) => MoveSelectedRecord(1);
        _btnReload.Click += (_, _) => ReloadWithPrompt();
        _btnOpenJson.Click += (_, _) => OpenJsonFile();
        _btnImport.Click += (_, _) => ImportTemplates();
        _btnExport.Click += (_, _) => ExportTemplates();
        _btnSave.Click += (_, _) => SaveAll();
        _btnClose.Click += (_, _) => Close();

        _txtName.TextChanged += Editor_TextChanged;
        _cmbCategory.SelectedIndexChanged += Editor_TextChanged;
        _txtTemplateText.TextChanged += Editor_TextChanged;

        _txtName.Leave += Editor_FocusLost;
        _cmbCategory.Leave += Editor_FocusLost;
        _txtTemplateText.Leave += Editor_FocusLost;

        KeyDown += TemplateEditorForm_KeyDown;
        FormClosing += TemplateEditorForm_FormClosing;
    }

    private void TemplateEditorForm_KeyDown(object? sender, KeyEventArgs e)
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

            case { KeyCode: Keys.Delete, Control: true }:
                DeleteSelectedRecord();
                e.Handled = true;
                e.SuppressKeyPress = true;
                break;
        }
    }

    private void Editor_TextChanged(object? sender, EventArgs e)
    {
        if (_isLoading)
            return;

        _isDirty = true;
    }

    private void Editor_FocusLost(object? sender, EventArgs e)
    {
        ApplyEditorChanges();
    }

    private void TemplateEditorForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!_isDirty)
            return;

        DialogResult result = MessageBox.Show(
            this,
            "You have unsaved template changes. Save before closing?",
            "BuddyAI Template Editor",
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
            _records.AddRange(_promptService.LoadOrSeed().Select(CloneTemplate));
            ApplyFilter(selectId);
            _lblPath.Text = "JSON file: " + _promptService.GetStoragePath();
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

        _filteredRecords.Clear();

        foreach (PromptItem record in _records)
        {
            if (categoryFilter != "All Categories")
            {
                string recordCategory = NormalizeCategory(record.Category);
                if (!string.Equals(recordCategory, categoryFilter, StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            if (!string.IsNullOrEmpty(searchText))
            {
                bool matches =
                    record.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    record.Category.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    record.Text.Contains(searchText, StringComparison.OrdinalIgnoreCase);

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
        int categories = _records
            .Select(r => NormalizeCategory(r.Category))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        _lblStats.Text = shown == total
            ? $"{total} templates · {categories} categories"
            : $"{shown} of {total} shown · {categories} categories";
    }

    private void RebuildTree(string? selectId)
    {
        _treeTemplates.BeginUpdate();
        _treeTemplates.Nodes.Clear();

        var groups = _filteredRecords
            .GroupBy(r => NormalizeCategory(r.Category), StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        TreeNode? nodeToSelect = null;

        foreach (var group in groups)
        {
            TreeNode categoryNode = new($"📁 {group.Key} ({group.Count()})");
            categoryNode.Tag = null;

            foreach (PromptItem record in group)
            {
                string label = $"📄 {record.Name}";
                TreeNode templateNode = new(label) { Tag = record };
                categoryNode.Nodes.Add(templateNode);

                if (!string.IsNullOrWhiteSpace(selectId) &&
                    string.Equals(record.Id, selectId, StringComparison.OrdinalIgnoreCase))
                {
                    nodeToSelect = templateNode;
                }
            }

            _treeTemplates.Nodes.Add(categoryNode);
        }

        _treeTemplates.EndUpdate();

        if (nodeToSelect != null)
        {
            nodeToSelect.Parent?.Expand();
            _treeTemplates.SelectedNode = nodeToSelect;
        }
        else if (_treeTemplates.Nodes.Count > 0 && _treeTemplates.Nodes[0].Nodes.Count > 0)
        {
            _treeTemplates.Nodes[0].Expand();
            _treeTemplates.SelectedNode = _treeTemplates.Nodes[0].Nodes[0];
        }
        else
        {
            ClearEditors();
        }
    }

    private void LoadSelectedRecordIntoEditors()
    {
        if (_isLoading)
            return;

        _isLoading = true;
        try
        {
            PromptItem? record = GetSelectedRecord();
            if (record == null)
            {
                ClearEditors();
                return;
            }

            _txtName.Text = record.Name;
            SelectCategory(record.Category);
            _txtId.Text = record.Id;
            _txtTemplateText.Text = record.Text;
        }
        finally
        {
            _isLoading = false;
        }
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

        PromptItem? record = GetSelectedRecord();
        if (record == null)
            return;

        record.Name = _txtName.Text.Trim();
        record.Category = _cmbCategory.SelectedItem as string ?? "General";
        record.Text = _txtTemplateText.Text.Trim();

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
        TreeNode? node = _treeTemplates.SelectedNode;
        if (node?.Tag is not PromptItem record)
            return;

        node.Text = $"📄 {record.Name}";

        string currentCategory = NormalizeCategory(record.Category);
        if (node.Parent != null)
        {
            string expectedPrefix = $"📁 {currentCategory}";
            if (!node.Parent.Text.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                _isLoading = false;
                ApplyFilter(record.Id);
                _isLoading = true;
            }
        }
    }

    private void CreateNewRecord()
    {
        PromptItem record = new()
        {
            Name = "New Template",
            Category = "General",
            Text = "Describe the task you want me to help with."
        };

        _records.Add(record);
        ApplyFilter(record.Id);
        MarkDirty();
    }

    private void CloneSelectedRecord()
    {
        PromptItem? selected = GetSelectedRecord();
        if (selected == null)
            return;

        PromptItem clone = CloneTemplate(selected);
        clone.Id = Guid.NewGuid().ToString("n");
        clone.Name = selected.Name + " Copy";

        int sourceIndex = _records.FindIndex(x => string.Equals(x.Id, selected.Id, StringComparison.OrdinalIgnoreCase));
        int insertIndex = sourceIndex >= 0 ? sourceIndex + 1 : _records.Count;
        _records.Insert(insertIndex, clone);
        ApplyFilter(clone.Id);
        MarkDirty();
    }

    private void DeleteSelectedRecord()
    {
        PromptItem? selected = GetSelectedRecord();
        if (selected == null)
            return;

        if (MessageBox.Show(this, $"Delete '{selected.Name}'?", "Delete Template", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        int filteredIndex = _filteredRecords.IndexOf(selected);
        _records.RemoveAll(x => string.Equals(x.Id, selected.Id, StringComparison.OrdinalIgnoreCase));
        _filteredRecords.Remove(selected);

        string? nextId = _filteredRecords.ElementAtOrDefault(Math.Max(0, filteredIndex - 1))?.Id;
        ApplyFilter(nextId);
        MarkDirty();
    }

    private void MoveSelectedRecord(int direction)
    {
        PromptItem? selected = GetSelectedRecord();
        if (selected == null)
            return;

        int current = _records.FindIndex(x => string.Equals(x.Id, selected.Id, StringComparison.OrdinalIgnoreCase));
        if (current < 0)
            return;

        int target = current + direction;
        if (target < 0 || target >= _records.Count)
            return;

        PromptItem item = _records[current];
        _records.RemoveAt(current);
        _records.Insert(target, item);
        ApplyFilter(item.Id);
        MarkDirty();
    }

    private void ReloadWithPrompt()
    {
        if (_isDirty)
        {
            DialogResult result = MessageBox.Show(this, "Discard unsaved changes and reload templates from disk?", "Reload Templates", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result != DialogResult.Yes)
                return;
        }

        LoadRecords(GetSelectedRecord()?.Id);
    }

    private void OpenJsonFile()
    {
        _promptService.EnsureFileExists();
        Process.Start(new ProcessStartInfo
        {
            FileName = _promptService.GetStoragePath(),
            UseShellExecute = true
        });
    }

    private void ImportTemplates()
    {
        using OpenFileDialog dialog = new()
        {
            Filter = "JSON Files|*.json|All Files|*.*",
            Title = "Import Templates"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        DialogResult merge = MessageBox.Show(this, "Merge imported templates with the current working list? Click No to replace the current list.", "Import Templates", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        if (merge == DialogResult.Cancel)
            return;

        List<PromptItem> imported = _promptService.Import(dialog.FileName, merge == DialogResult.Yes);
        _records.Clear();
        _records.AddRange(imported.Select(CloneTemplate));
        ApplyFilter(_records.FirstOrDefault()?.Id);
        MarkDirty();
    }

    private void ExportTemplates()
    {
        using SaveFileDialog dialog = new()
        {
            Filter = "JSON Files|*.json",
            FileName = "BuddyAI.templates.export.json",
            Title = "Export Templates"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        _promptService.Export(dialog.FileName, _records);
        MessageBox.Show(this, "Templates exported successfully.", "Export Templates", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void SaveAll()
    {
        ApplyEditorChanges();
        ValidateBeforeSave();
        _promptService.Save(_records);
        _isDirty = false;
        UpdateWindowTitle();
        DialogResult = DialogResult.OK;
        Close();
    }

    private void ValidateBeforeSave()
    {
        foreach (PromptItem record in _records)
        {
            if (string.IsNullOrWhiteSpace(record.Name))
                throw new InvalidOperationException("Template name is required.");

            if (string.IsNullOrWhiteSpace(record.Text))
                throw new InvalidOperationException($"Template text is required for '{record.Name}'.");
        }
    }

    private PromptItem? GetSelectedRecord()
    {
        return _treeTemplates.SelectedNode?.Tag as PromptItem;
    }

    private void ClearEditors()
    {
        _txtName.Clear();
        _cmbCategory.SelectedIndex = 0;
        _txtId.Clear();
        _txtTemplateText.Clear();
    }

    private void MarkDirty()
    {
        _isDirty = true;
        UpdateWindowTitle();
    }

    private void UpdateWindowTitle()
    {
        Text = _isDirty ? "BuddyAI Template Editor *" : "BuddyAI Template Editor";
    }

    private static PromptItem CloneTemplate(PromptItem item)
    {
        return new PromptItem
        {
            Id = item.Id,
            Category = item.Category,
            Name = item.Name,
            Text = item.Text
        };
    }
}
