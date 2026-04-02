using BuddyAI.Models;
using BuddyAI.Services;

namespace BuddyAI.Forms;

public sealed class PersonaCatalogBrowserForm : Form
{
    private readonly PersonaCatalogService _catalogService;
    private readonly PersonaService _personaService;

    private readonly ListView _listViewPersonas = new();
    private readonly ComboBox _cmbCategoryFilter = new();
    private readonly TextBox _txtSearch = new();
    private readonly Label _lblStatus = new();
    private readonly Button _btnDownload = new();
    private readonly Button _btnClose = new();
    private readonly Button _btnSelectAll = new();
    private readonly Button _btnDeselectAll = new();
    private readonly ProgressBar _progressBar = new();
    private readonly CheckBox _chkUpdateExisting = new();

    private PersonaCatalogIndex? _catalogIndex;
    private List<CatalogPersonaEntry> _allEntries = new();
    private List<CatalogPersonaEntry> _filteredEntries = new();
    private HashSet<string> _existingIds = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _cts;

    public List<PersonaRecord> DownloadedPersonas { get; } = new();

    public PersonaCatalogBrowserForm(PersonaCatalogService catalogService, PersonaService personaService)
    {
        _catalogService = catalogService;
        _personaService = personaService;

        Text = "Persona Catalog — Download from GitHub";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(820, 520);
        Size = new Size(960, 640);
        AutoScaleMode = AutoScaleMode.Dpi;
        KeyPreview = true;

        BuildUi();
        WireEvents();
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        await LoadCatalogAsync();
    }

    private void BuildUi()
    {
        TableLayoutPanel main = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(10)
        };
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));   // filter bar
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // list
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));   // progress
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));   // footer
        Controls.Add(main);

        // --- Filter row ---
        FlowLayoutPanel filterRow = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };

        _txtSearch.Width = 220;
        _txtSearch.PlaceholderText = "?? Search personas...";
        filterRow.Controls.Add(_txtSearch);

        _cmbCategoryFilter.Width = 160;
        _cmbCategoryFilter.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbCategoryFilter.Margin = new Padding(8, 0, 0, 0);
        filterRow.Controls.Add(_cmbCategoryFilter);

        _btnSelectAll.Text = "Select All";
        _btnSelectAll.AutoSize = true;
        _btnSelectAll.Margin = new Padding(16, 0, 0, 0);
        filterRow.Controls.Add(_btnSelectAll);

        _btnDeselectAll.Text = "Deselect All";
        _btnDeselectAll.AutoSize = true;
        _btnDeselectAll.Margin = new Padding(4, 0, 0, 0);
        filterRow.Controls.Add(_btnDeselectAll);

        _chkUpdateExisting.Text = "Update existing";
        _chkUpdateExisting.AutoSize = true;
        _chkUpdateExisting.Checked = true;
        _chkUpdateExisting.Margin = new Padding(16, 4, 0, 0);
        filterRow.Controls.Add(_chkUpdateExisting);

        main.Controls.Add(filterRow, 0, 0);

        // --- ListView ---
        _listViewPersonas.Dock = DockStyle.Fill;
        _listViewPersonas.View = View.Details;
        _listViewPersonas.CheckBoxes = true;
        _listViewPersonas.FullRowSelect = true;
        _listViewPersonas.GridLines = true;
        _listViewPersonas.Columns.Add("", 30);
        _listViewPersonas.Columns.Add("Name", 200);
        _listViewPersonas.Columns.Add("Category", 110);
        _listViewPersonas.Columns.Add("Summary", 320);
        _listViewPersonas.Columns.Add("Tags", 150);
        _listViewPersonas.Columns.Add("Status", 90);
        main.Controls.Add(_listViewPersonas, 0, 1);

        // --- Progress ---
        FlowLayoutPanel progressRow = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };

        _progressBar.Width = 300;
        _progressBar.Height = 22;
        _progressBar.Style = ProgressBarStyle.Continuous;
        _progressBar.Visible = false;
        progressRow.Controls.Add(_progressBar);

        _lblStatus.AutoSize = true;
        _lblStatus.Text = "Loading catalog...";
        _lblStatus.Margin = new Padding(8, 4, 0, 0);
        progressRow.Controls.Add(_lblStatus);

        main.Controls.Add(progressRow, 0, 2);

        // --- Footer ---
        FlowLayoutPanel footer = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };

        _btnClose.Text = "Close";
        _btnClose.Width = 90;
        _btnClose.Height = 32;
        footer.Controls.Add(_btnClose);

        _btnDownload.Text = "Download Selected";
        _btnDownload.Width = 150;
        _btnDownload.Height = 32;
        _btnDownload.Enabled = false;
        _btnDownload.Margin = new Padding(0, 0, 8, 0);
        footer.Controls.Add(_btnDownload);

        main.Controls.Add(footer, 0, 3);
    }

    private void WireEvents()
    {
        _txtSearch.TextChanged += (_, _) => ApplyFilter();
        _cmbCategoryFilter.SelectedIndexChanged += (_, _) => ApplyFilter();
        _btnSelectAll.Click += (_, _) => SetAllChecked(true);
        _btnDeselectAll.Click += (_, _) => SetAllChecked(false);
        _btnDownload.Click += async (_, _) => await DownloadSelectedAsync();
        _btnClose.Click += (_, _) => Close();
        _listViewPersonas.ItemChecked += (_, _) => UpdateDownloadButtonState();

        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
                Close();
        };
    }

    private async Task LoadCatalogAsync()
    {
        try
        {
            SetUiBusy(true, "Fetching persona catalog from GitHub...");

            List<PersonaRecord> existing = _personaService.LoadOrSeed();
            _existingIds = new HashSet<string>(
                existing.Select(p => p.Id),
                StringComparer.OrdinalIgnoreCase);

            _catalogIndex = await _catalogService.FetchCatalogIndexAsync();
            _allEntries = _catalogIndex.Personas;

            _cmbCategoryFilter.Items.Clear();
            _cmbCategoryFilter.Items.Add("All Categories");
            foreach (string category in _catalogIndex.Categories.OrderBy(c => c))
                _cmbCategoryFilter.Items.Add(category);
            _cmbCategoryFilter.SelectedIndex = 0;

            ApplyFilter();

            _lblStatus.Text = $"{_allEntries.Count} personas available · " +
                              $"{_existingIds.Count} already installed";

            SetUiBusy(false);
        }
        catch (Exception ex)
        {
            SetUiBusy(false);
            _lblStatus.Text = "Failed to load catalog.";
            MessageBox.Show(
                this,
                $"Could not fetch the persona catalog:\n\n{ex.Message}",
                "Catalog Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void ApplyFilter()
    {
        string search = _txtSearch.Text.Trim();
        string category = _cmbCategoryFilter.SelectedItem as string ?? "All Categories";

        _filteredEntries = _allEntries.Where(entry =>
        {
            if (category != "All Categories" &&
                !string.Equals(entry.Category, category, StringComparison.OrdinalIgnoreCase))
                return false;

            if (!string.IsNullOrEmpty(search))
            {
                bool matches =
                    entry.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    entry.Category.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    entry.Summary.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    entry.Tags.Any(t => t.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                    entry.Roles.Any(r => r.Contains(search, StringComparison.OrdinalIgnoreCase));

                if (!matches)
                    return false;
            }

            return true;
        }).ToList();

        RebuildListView();
    }

    private void RebuildListView()
    {
        _listViewPersonas.BeginUpdate();
        _listViewPersonas.Items.Clear();

        foreach (CatalogPersonaEntry entry in _filteredEntries)
        {
            bool alreadyInstalled = _existingIds.Contains(entry.Id);
            string status = alreadyInstalled ? "Installed" : "New";
            string tags = string.Join(", ", entry.Tags);

            ListViewItem item = new()
            {
                Text = "",
                Tag = entry,
                Checked = !alreadyInstalled
            };

            item.SubItems.Add($"{entry.Icon} {entry.Name}");
            item.SubItems.Add(entry.Category);
            item.SubItems.Add(entry.Summary);
            item.SubItems.Add(tags);
            item.SubItems.Add(status);

            if (alreadyInstalled)
                item.ForeColor = SystemColors.GrayText;

            _listViewPersonas.Items.Add(item);
        }

        _listViewPersonas.EndUpdate();
        UpdateDownloadButtonState();
    }

    private void SetAllChecked(bool isChecked)
    {
        _listViewPersonas.BeginUpdate();
        foreach (ListViewItem item in _listViewPersonas.Items)
            item.Checked = isChecked;
        _listViewPersonas.EndUpdate();
    }

    private void UpdateDownloadButtonState()
    {
        int checkedCount = _listViewPersonas.CheckedItems.Count;
        _btnDownload.Enabled = checkedCount > 0 && _catalogIndex != null;
        _btnDownload.Text = checkedCount > 0
            ? $"Download Selected ({checkedCount})"
            : "Download Selected";
    }

    private async Task DownloadSelectedAsync()
    {
        List<CatalogPersonaEntry> selected = _listViewPersonas.CheckedItems
            .Cast<ListViewItem>()
            .Select(item => (CatalogPersonaEntry)item.Tag!)
            .ToList();

        if (selected.Count == 0)
            return;

        bool updateExisting = _chkUpdateExisting.Checked;

        if (!updateExisting)
        {
            selected = selected
                .Where(e => !_existingIds.Contains(e.Id))
                .ToList();

            if (selected.Count == 0)
            {
                MessageBox.Show(
                    this,
                    "All selected personas are already installed and 'Update existing' is unchecked.",
                    "Nothing to Download",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }
        }

        _cts = new CancellationTokenSource();

        try
        {
            SetUiBusy(true, $"Downloading 0 of {selected.Count}...");
            _progressBar.Visible = true;
            _progressBar.Minimum = 0;
            _progressBar.Maximum = selected.Count;
            _progressBar.Value = 0;

            Progress<(int completed, int total, string name)> progress = new(p =>
            {
                _progressBar.Value = p.completed;
                _lblStatus.Text = $"Downloading {p.completed} of {p.total}: {p.name}";
            });

            List<PersonaRecord> downloaded = await _catalogService.DownloadPersonasAsync(
                selected, progress, _cts.Token);

            DownloadedPersonas.AddRange(downloaded);

            foreach (PersonaRecord p in downloaded)
                _existingIds.Add(p.Id);

            RebuildListView();

            _lblStatus.Text = $"Downloaded {downloaded.Count} persona(s) successfully.";
            _progressBar.Visible = false;

            MessageBox.Show(
                this,
                $"Successfully downloaded {downloaded.Count} persona(s).\n" +
                "They have been merged into your working list.\n\n" +
                "Click Save in the Persona Manager to persist changes.",
                "Download Complete",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (OperationCanceledException)
        {
            _lblStatus.Text = "Download cancelled.";
        }
        catch (Exception ex)
        {
            _lblStatus.Text = "Download failed.";
            MessageBox.Show(
                this,
                $"Error downloading personas:\n\n{ex.Message}",
                "Download Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            _progressBar.Visible = false;
            SetUiBusy(false);
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void SetUiBusy(bool busy, string? statusText = null)
    {
        _btnDownload.Enabled = !busy && _listViewPersonas.CheckedItems.Count > 0;
        _btnSelectAll.Enabled = !busy;
        _btnDeselectAll.Enabled = !busy;
        _txtSearch.Enabled = !busy;
        _cmbCategoryFilter.Enabled = !busy;
        _chkUpdateExisting.Enabled = !busy;
        Cursor = busy ? Cursors.WaitCursor : Cursors.Default;

        if (statusText != null)
            _lblStatus.Text = statusText;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _cts?.Cancel();
        base.OnFormClosing(e);
    }
}
