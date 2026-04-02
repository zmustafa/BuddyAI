using System.Diagnostics;
using BuddyAI.Models;
using BuddyAI.Services;

namespace BuddyAI.Forms;

public sealed class SnippetEditorForm : Form
{
    private readonly SnippetService _service;
    private readonly ListBox _lstItems = new();
    private readonly TextBox _txtSearch = new();
    private readonly TextBox _txtId = new();
    private readonly TextBox _txtCategory = new();
    private readonly TextBox _txtName = new();
    private readonly TextBox _txtText = new();
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

    private readonly List<SnippetItem> _records = new();
    private bool _isLoading;
    private bool _isDirty;

    public SnippetEditorForm(SnippetService snippetService)
    {
        _service = snippetService;

        Text = "BuddyAI Snippet Editor";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1180, 740);
        Size = new Size(1400, 860);
        AutoScaleMode = AutoScaleMode.Dpi;
        this.Resize += Form1_Resize;
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
        // Example: keep Panel1 at 30% of total width
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

        Label lblList = new() { Text = "Snippets", Dock = DockStyle.Top, Height = 24 };
        left.Controls.Add(lblList);

        _txtSearch.Dock = DockStyle.Top;
        _txtSearch.PlaceholderText = "Search snippets";
        _txtSearch.Margin = new Padding(0, 0, 0, 8);
        left.Controls.Add(_txtSearch);
        _txtSearch.BringToFront();

        _lstItems.Dock = DockStyle.Fill;
        _lstItems.IntegralHeight = false;
        left.Controls.Add(_lstItems);

        FlowLayoutPanel leftButtons = new()
        {
            Dock = DockStyle.Bottom,
            Height = 112,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        left.Controls.Add(leftButtons);

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
        AddRowLabel(form, "Snippet", 3);

        _txtName.Dock = DockStyle.Fill;
        _txtCategory.Dock = DockStyle.Fill;
        _txtId.Dock = DockStyle.Fill;
        _txtId.ReadOnly = true;
        _txtId.BackColor = SystemColors.ControlLight;

        ConfigureEditorTextBox(_txtText);

        form.Controls.Add(_txtName, 1, 0);
        form.Controls.Add(_txtCategory, 1, 1);
        form.Controls.Add(_txtId, 1, 2);
        form.Controls.Add(_txtText, 1, 3);

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
        _txtSearch.TextChanged += (_, __) => BindList(GetSelectedRecord()?.Id);
        _lstItems.SelectedIndexChanged += (_, __) => LoadSelectedRecordIntoEditors();
        _btnNew.Click += (_, __) => CreateNewRecord();
        _btnClone.Click += (_, __) => CloneSelectedRecord();
        _btnDelete.Click += (_, __) => DeleteSelectedRecord();
        _btnMoveUp.Click += (_, __) => MoveSelectedRecord(-1);
        _btnMoveDown.Click += (_, __) => MoveSelectedRecord(1);
        _btnReload.Click += (_, __) => ReloadWithPrompt();
        _btnOpenJson.Click += (_, __) => OpenJsonFile();
        _btnImport.Click += (_, __) => ImportItems();
        _btnExport.Click += (_, __) => ExportItems();
        _btnSave.Click += (_, __) => SaveAll();
        _btnClose.Click += (_, __) => Close();

        _txtName.TextChanged += Editor_TextChanged;
        _txtCategory.TextChanged += Editor_TextChanged;
        _txtText.TextChanged += Editor_TextChanged;

        FormClosing += EditorForm_FormClosing;
    }

    private void Editor_TextChanged(object? sender, EventArgs e)
    {
        ApplyEditorChanges();
    }

    private void EditorForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!_isDirty)
            return;

        DialogResult result = MessageBox.Show(
            this,
            "You have unsaved changes. Save before closing?",
            "BuddyAI Snippet Editor",
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
            _records.AddRange(_service.LoadOrSeed().Select(CloneItem));
            BindList(selectId);
            _lblPath.Text = "JSON file: " + _service.GetStoragePath();
            _isDirty = false;
            UpdateWindowTitle();
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void BindList(string? selectId)
    {
        string filter = _txtSearch.Text.Trim();
        IEnumerable<SnippetItem> filtered = _records;

        if (!string.IsNullOrWhiteSpace(filter))
        {
            filtered = filtered.Where(x =>
                x.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                x.Category.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                x.Text.Contains(filter, StringComparison.OrdinalIgnoreCase));
        }

        List<SnippetItem> visibleRecords = filtered.ToList();

        _lstItems.BeginUpdate();
        _lstItems.Items.Clear();
        foreach (SnippetItem record in visibleRecords)
            _lstItems.Items.Add(new ItemListEntry(record));
        _lstItems.EndUpdate();

        if (_lstItems.Items.Count == 0)
        {
            ClearEditors();
            return;
        }

        int index = 0;
        if (!string.IsNullOrWhiteSpace(selectId))
        {
            for (int i = 0; i < _lstItems.Items.Count; i++)
            {
                if (_lstItems.Items[i] is ItemListEntry item && string.Equals(item.Record.Id, selectId, StringComparison.OrdinalIgnoreCase))
                {
                    index = i;
                    break;
                }
            }
        }

        _lstItems.SelectedIndex = index;
    }

    private void LoadSelectedRecordIntoEditors()
    {
        if (_isLoading)
            return;

        _isLoading = true;
        try
        {
            SnippetItem? record = GetSelectedRecord();
            if (record == null)
            {
                ClearEditors();
                return;
            }

            _txtName.Text = record.Name;
            _txtCategory.Text = record.Category;
            _txtId.Text = record.Id;
            _txtText.Text = record.Text;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void ApplyEditorChanges()
    {
        if (_isLoading)
            return;

        SnippetItem? record = GetSelectedRecord();
        if (record == null)
            return;

        record.Name = _txtName.Text.Trim();
        record.Category = _txtCategory.Text.Trim();
        record.Text = _txtText.Text.Trim();

        RefreshSelectedListEntry();
        MarkDirty();
    }

    private void RefreshSelectedListEntry()
    {
        string? selectedId = GetSelectedRecord()?.Id;
        BindList(selectedId);
    }

    private void CreateNewRecord()
    {
        SnippetItem record = new()
        {
            Name = "New Snippet",
            Category = "General",
            Text = "Describe the content you want to reuse."
        };

        _records.Add(record);
        BindList(record.Id);
        MarkDirty();
    }

    private void CloneSelectedRecord()
    {
        SnippetItem? selected = GetSelectedRecord();
        if (selected == null)
            return;

        SnippetItem clone = CloneItem(selected);
        clone.Id = Guid.NewGuid().ToString("n");
        clone.Name = selected.Name + " Copy";

        int sourceIndex = _records.FindIndex(x => string.Equals(x.Id, selected.Id, StringComparison.OrdinalIgnoreCase));
        int insertIndex = sourceIndex >= 0 ? sourceIndex + 1 : _records.Count;
        _records.Insert(insertIndex, clone);
        BindList(clone.Id);
        MarkDirty();
    }

    private void DeleteSelectedRecord()
    {
        SnippetItem? selected = GetSelectedRecord();
        if (selected == null)
            return;

        if (MessageBox.Show(this, $"Delete '{selected.Name}'?", "Delete Snippet", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        int selectedIndex = _records.FindIndex(x => string.Equals(x.Id, selected.Id, StringComparison.OrdinalIgnoreCase));
        _records.RemoveAll(x => string.Equals(x.Id, selected.Id, StringComparison.OrdinalIgnoreCase));

        string? nextId = null;
        if (_records.Count > 0)
        {
            int nextIndex = Math.Max(0, Math.Min(_records.Count - 1, selectedIndex - 1));
            nextId = _records[nextIndex].Id;
        }

        BindList(nextId);
        MarkDirty();
    }

    private void MoveSelectedRecord(int direction)
    {
        SnippetItem? selected = GetSelectedRecord();
        if (selected == null)
            return;

        int current = _records.FindIndex(x => string.Equals(x.Id, selected.Id, StringComparison.OrdinalIgnoreCase));
        if (current < 0)
            return;

        int target = current + direction;
        if (target < 0 || target >= _records.Count)
            return;

        SnippetItem item = _records[current];
        _records.RemoveAt(current);
        _records.Insert(target, item);
        BindList(item.Id);
        MarkDirty();
    }

    private void ReloadWithPrompt()
    {
        if (_isDirty)
        {
            DialogResult result = MessageBox.Show(this, "Discard unsaved changes and reload from disk?", "Reload", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result != DialogResult.Yes)
                return;
        }

        LoadRecords(GetSelectedRecord()?.Id);
    }

    private void OpenJsonFile()
    {
        _service.EnsureFileExists();
        Process.Start(new ProcessStartInfo
        {
            FileName = _service.GetStoragePath(),
            UseShellExecute = true
        });
    }

    private void ImportItems()
    {
        using OpenFileDialog dialog = new()
        {
            Filter = "JSON Files|*.json|All Files|*.*",
            Title = "Import Snippets"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        DialogResult merge = MessageBox.Show(this, "Merge imported items with the current working list? Click No to replace the current list.", "Import Snippets", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        if (merge == DialogResult.Cancel)
            return;

        List<SnippetItem> imported = _service.Import(dialog.FileName, merge == DialogResult.Yes);
        _records.Clear();
        _records.AddRange(imported.Select(CloneItem));
        BindList(_records.FirstOrDefault()?.Id);
        MarkDirty();
    }

    private void ExportItems()
    {
        using SaveFileDialog dialog = new()
        {
            Filter = "JSON Files|*.json",
            FileName = "BuddyAI.snippets.export.json",
            Title = "Export"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        _service.Export(dialog.FileName, _records);
        MessageBox.Show(this, "Items exported successfully.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void SaveAll()
    {
        ValidateBeforeSave();
        _service.Save(_records);
        _isDirty = false;
        UpdateWindowTitle();
        DialogResult = DialogResult.OK;
        Close();
    }

    private void ValidateBeforeSave()
    {
        foreach (SnippetItem record in _records)
        {
            if (string.IsNullOrWhiteSpace(record.Name))
                throw new InvalidOperationException("Name is required.");

            if (string.IsNullOrWhiteSpace(record.Text))
                throw new InvalidOperationException($"Text is required for '{record.Name}'.");
        }
    }

    private SnippetItem? GetSelectedRecord()
    {
        return (_lstItems.SelectedItem as ItemListEntry)?.Record;
    }

    private void ClearEditors()
    {
        _txtName.Clear();
        _txtCategory.Clear();
        _txtId.Clear();
        _txtText.Clear();
    }

    private void MarkDirty()
    {
        _isDirty = true;
        UpdateWindowTitle();
    }

    private void UpdateWindowTitle()
    {
        Text = _isDirty ? "BuddyAI Snippet Editor *" : "BuddyAI Snippet Editor";
    }

    private static SnippetItem CloneItem(SnippetItem item)
    {
        return new SnippetItem
        {
            Id = item.Id,
            Category = item.Category,
            Name = item.Name,
            Text = item.Text
        };
    }

    private sealed class ItemListEntry
    {
        public SnippetItem Record { get; }

        public ItemListEntry(SnippetItem record)
        {
            Record = record;
        }

        public override string ToString()
        {
            string category = string.IsNullOrWhiteSpace(Record.Category) ? "General" : Record.Category;
            return $"[{category}] {Record.Name} — {Record.Text}";
        }
    }
}
