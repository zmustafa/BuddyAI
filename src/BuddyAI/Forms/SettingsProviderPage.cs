using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using BuddyAI.Models;
using BuddyAI.Services;

namespace BuddyAI.Forms;

public sealed class SettingsProviderPage : UserControl
{
    private static readonly JsonSerializerOptions ExportJsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly AiProviderService _providerService;
    private readonly TreeView _treeProviders = new();
    private readonly TextBox _txtName = new();
    private readonly TextBox _txtId = new();
    private readonly ComboBox _cmbProviderType = new();
    private readonly TextBox _txtBaseUrl = new();
    private readonly TextBox _txtEndpointPath = new();
    private readonly TextBox _txtApiKey = new();
    private readonly DataGridView _gridModels = new();
    private readonly Button _btnAddModel = new();
    private readonly Button _btnCloneModel = new();
    private readonly Button _btnDeleteModel = new();
    private readonly ToolStripButton _btnNew = new("New");
    private readonly ToolStripButton _btnClone = new("Clone");
    private readonly ToolStripButton _btnDelete = new("Delete");
    private readonly ToolStripButton _btnReload = new("Reload");
    private readonly ToolStripButton _btnOpenJson = new("Open JSON");
    private readonly ToolStripDropDownButton _btnExportImport = new("Export / Import");

    private readonly ToolStripMenuItem _btnExportWithKeys = new("Export Providers (with API keys)");
    private readonly ToolStripMenuItem _btnExportWithoutKeys = new("Export Providers (without API keys)");
    private readonly ToolStripMenuItem _btnImportOpenAi = new("Import OpenAI");
    private readonly ToolStripMenuItem _btnImportGrok = new("Import GROK");
    private readonly ToolStripMenuItem _btnImportClaude = new("Import Claude");
    private readonly ToolStripMenuItem _btnImportGemini = new("Import Gemini");
    private readonly ToolStripMenuItem _btnImportMistral = new("Import Mistral");
    private readonly ToolStripMenuItem _btnImportOpenAIOAuth = new("Import ChatGPT OAuth");
    private readonly ToolStripMenuItem _btnImportClaudeOAuth = new("Import Claude OAuth");
    private readonly ToolStripMenuItem _btnAddDefaultProviders = new("Add Default Providers");
    private readonly ToolStripMenuItem _btnImportWizard = new("Import Provider Wizard...");

    private readonly Button _btnSave = new();
    private readonly Button _btnReauthenticate = new() { Text = "Re-Authenticate" };
    private readonly Label _lblPath = new();
    private readonly SplitContainer _split = new();
    private readonly ImageList _treeImageList = new();

    private const int ProviderImageIndex = 0;
    private const int ModelImageIndex = 1;

    private readonly List<AiProviderDefinition> _records = new();
    private BindingList<ModelGridRow> _modelRows = new();
    private string? _loadedRecordId;
    private bool _isLoading;

    public bool IsDirty { get; private set; }

    public event EventHandler? DirtyChanged;

    public SettingsProviderPage(AiProviderService providerService)
    {
        _providerService = providerService;
        BuildUi();
        WireEvents();
        LoadRecords(selectId: null);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _treeImageList.Dispose();

        base.Dispose(disposing);
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

        ToolStrip leftToolbar = new()
        {
            GripStyle = ToolStripGripStyle.Hidden,
            RenderMode = ToolStripRenderMode.System,
            Padding = new Padding(0, 0, 0, 5)
        };

        _btnExportImport.DropDownItems.AddRange(new ToolStripItem[]
        {
            _btnImportWizard,
            new ToolStripSeparator(),
            _btnExportWithKeys,
            _btnExportWithoutKeys,
            new ToolStripSeparator(),
            _btnImportOpenAi,
            _btnImportGrok,
            _btnImportClaude,
            _btnImportGemini,
            _btnImportMistral,
            _btnImportOpenAIOAuth,
            _btnImportClaudeOAuth,
            new ToolStripSeparator(),
            _btnAddDefaultProviders
        });

        leftToolbar.Items.AddRange(new ToolStripItem[]
            { _btnNew, _btnClone, _btnDelete, new ToolStripSeparator(), _btnReload, _btnOpenJson, _btnExportImport });

        _treeProviders.Dock = DockStyle.Fill;
        _treeProviders.HideSelection = false;
        _treeProviders.FullRowSelect = true;
        _treeProviders.ShowLines = true;
        _treeProviders.ShowPlusMinus = true;
        _treeProviders.ShowRootLines = true;

        InitializeTreeImageList();
        _treeProviders.ImageList = _treeImageList;

        left.Controls.Add(_treeProviders);
        left.Controls.Add(leftToolbar);

        TableLayoutPanel form = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 8,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        form.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        right.Controls.Add(form);

        AddRowLabel(form, "Provider Name", 0);
        AddRowLabel(form, "Id", 1);
        AddRowLabel(form, "Provider Type", 2);
        AddRowLabel(form, "Base URL", 3);
        AddRowLabel(form, "Endpoint Path", 4);
        AddRowLabel(form, "API Key", 5);
        AddRowLabel(form, "Models", 6);

        _txtName.Dock = DockStyle.Fill;
        _txtId.Dock = DockStyle.Fill;
        _txtId.ReadOnly = true;
        _txtId.BackColor = SystemColors.ControlLight;
        _cmbProviderType.Dock = DockStyle.Fill;
        _cmbProviderType.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbProviderType.Items.AddRange(AiProviderTypes.All.Cast<object>().ToArray());
        _txtBaseUrl.Dock = DockStyle.Fill;
        _txtEndpointPath.Dock = DockStyle.Fill;

        form.Controls.Add(_txtName, 1, 0);
        form.Controls.Add(_txtId, 1, 1);
        form.Controls.Add(_cmbProviderType, 1, 2);
        form.Controls.Add(_txtBaseUrl, 1, 3);
        form.Controls.Add(_txtEndpointPath, 1, 4);

        TableLayoutPanel apiKeyPanel = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        apiKeyPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        apiKeyPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        apiKeyPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        apiKeyPanel.Controls.Add(_txtApiKey, 0, 0);
        _txtApiKey.Dock = DockStyle.Fill;
        _txtApiKey.PasswordChar = 'X';

        _btnReauthenticate.Height = 28;
        _btnReauthenticate.AutoSize = true;
        _btnReauthenticate.Margin = new Padding(5, 0, 0, 0);
        _btnReauthenticate.Visible = false;
        apiKeyPanel.Controls.Add(_btnReauthenticate, 1, 0);

        form.Controls.Add(apiKeyPanel, 1, 5);

        TableLayoutPanel modelsPanel = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        modelsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        modelsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        form.Controls.Add(modelsPanel, 1, 6);

        FlowLayoutPanel modelButtons = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        ConfigureButton(_btnAddModel, "Add Model", 110);
        ConfigureButton(_btnCloneModel, "Clone Model", 110);
        ConfigureButton(_btnDeleteModel, "Delete Model", 110);
        modelButtons.Controls.AddRange(new Control[] { _btnAddModel, _btnCloneModel, _btnDeleteModel });
        modelsPanel.Controls.Add(modelButtons, 0, 0);

        ConfigureModelGrid();
        modelsPanel.Controls.Add(_gridModels, 0, 1);

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

        ConfigureButton(_btnSave, "Save Providers", 140);
        footer.Controls.Add(_btnSave, 1, 0);

        ResizePanels();
        Resize += (_, _) => ResizePanels();
    }

    private void ConfigureModelGrid()
    {
        _gridModels.Dock = DockStyle.Fill;
        _gridModels.AllowUserToAddRows = false;
        _gridModels.AllowUserToDeleteRows = false;
        _gridModels.AllowUserToResizeRows = false;
        _gridModels.MultiSelect = false;
        _gridModels.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _gridModels.AutoGenerateColumns = false;
        _gridModels.RowHeadersVisible = false;
        _gridModels.BackgroundColor = SystemColors.Window;

        _gridModels.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(ModelGridRow.Name),
            HeaderText = "Model Name",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            MinimumWidth = 240
        });
        _gridModels.Columns.Add(new DataGridViewCheckBoxColumn
        {
            DataPropertyName = nameof(ModelGridRow.SupportsImages),
            HeaderText = "Images",
            Width = 110
        });
        _gridModels.Columns.Add(new DataGridViewCheckBoxColumn
        {
            DataPropertyName = nameof(ModelGridRow.SupportsTemperature),
            HeaderText = "Temperature",
            Width = 120
        });

        _gridModels.DataSource = _modelRows;
    }

    private void InitializeTreeImageList()
    {
        const int size = 16;
        _treeImageList.ImageSize = new Size(size, size);
        _treeImageList.ColorDepth = ColorDepth.Depth32Bit;

        Bitmap providerBmp = new(size, size);
        using (Graphics g = Graphics.FromImage(providerBmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using Pen pen = new(Color.SteelBlue, 1.5f);
            using SolidBrush fill = new(Color.SteelBlue);
            g.FillRectangle(fill, 3, 2, 10, 4);
            g.FillRectangle(fill, 3, 7, 10, 4);
            g.DrawRectangle(pen, 3, 2, 10, 4);
            g.DrawRectangle(pen, 3, 7, 10, 4);
            using SolidBrush dot = new(Color.White);
            g.FillEllipse(dot, 9, 3, 2, 2);
            g.FillEllipse(dot, 9, 8, 2, 2);
        }
        _treeImageList.Images.Add("provider", providerBmp);

        Bitmap modelBmp = new(size, size);
        using (Graphics g = Graphics.FromImage(modelBmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using SolidBrush fill = new(Color.MediumSeaGreen);
            Point[] diamond = [new(8, 2), new(14, 8), new(8, 14), new(2, 8)];
            g.FillPolygon(fill, diamond);
            using SolidBrush center = new(Color.White);
            g.FillEllipse(center, 6, 6, 4, 4);
        }
        _treeImageList.Images.Add("model", modelBmp);
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

    private void WireEvents()
    {
        _treeProviders.AfterSelect += (_, _) => OnProviderSelectionChanged();
        _btnNew.Click += (_, _) => CreateNewRecord();
        _btnClone.Click += (_, _) => CloneSelectedRecord();
        _btnDelete.Click += (_, _) => DeleteSelectedRecord();
        _btnReload.Click += (_, _) => ExecuteGuarded(ReloadWithPrompt, "Reload providers");
        _btnOpenJson.Click += (_, _) => ExecuteGuarded(OpenJsonFile, "Open JSON file");

        _btnImportOpenAi.Click += (_, _) => ExecuteGuarded(ImportOpenAi, "Import OpenAI");
        _btnImportGrok.Click += (_, _) => ExecuteGuarded(ImportGrok, "Import GROK");
        _btnImportClaude.Click += (_, _) => ExecuteGuarded(ImportClaude, "Import Claude");
        _btnImportGemini.Click += (_, _) => ExecuteGuarded(ImportGemini, "Import Gemini");
        _btnImportMistral.Click += (_, _) => ExecuteGuarded(ImportMistral, "Import Mistral");
        _btnImportOpenAIOAuth.Click += (_, _) => ExecuteGuarded(ImportOpenAIOAuth, "Import ChatGPT OAuth");
        _btnImportClaudeOAuth.Click += (_, _) => ExecuteGuarded(ImportClaudeOAuth, "Import Claude OAuth");
        _btnAddDefaultProviders.Click += (_, _) => ExecuteGuarded(AddDefaultProviders, "Add Default Providers");
        _btnImportWizard.Click += (_, _) => ExecuteGuarded(ImportViaWizard, "Import Provider Wizard");
        _btnSave.Click += (_, _) => ExecuteGuarded(SaveAll, "Save providers");
        _btnReauthenticate.Click += (_, _) => ExecuteGuarded(ReauthenticateOAuth, "Re-Authenticate OAuth");

        _btnExportWithKeys.Click += (_, _) => ExecuteGuarded(() => ExportProviders(includeApiKeys: true), "Export providers");
        _btnExportWithoutKeys.Click +=
            (_, _) => ExecuteGuarded(() => ExportProviders(includeApiKeys: false), "Export providers");

        _btnAddModel.Click += (_, _) => AddModelRow();
        _btnCloneModel.Click += (_, _) => CloneSelectedModelRow();
        _btnDeleteModel.Click += (_, _) => DeleteSelectedModelRow();

        _txtName.TextChanged += Editor_TextChanged;
        _txtBaseUrl.TextChanged += Editor_TextChanged;
        _txtEndpointPath.TextChanged += Editor_TextChanged;
        _txtApiKey.TextChanged += Editor_TextChanged;
        _cmbProviderType.SelectedIndexChanged += ProviderType_SelectedIndexChanged;

        _txtName.Leave += (_, _) => CommitEditorsToLoadedRecord();
        _txtBaseUrl.Leave += (_, _) => CommitEditorsToLoadedRecord();
        _txtEndpointPath.Leave += (_, _) => CommitEditorsToLoadedRecord();
        _txtApiKey.Leave += (_, _) => CommitEditorsToLoadedRecord();
        _cmbProviderType.Leave += (_, _) => CommitEditorsToLoadedRecord();

        _gridModels.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_gridModels.IsCurrentCellDirty)
                _gridModels.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        _gridModels.CellEndEdit += (_, _) => OnModelRowsChanged();
        _gridModels.CellValueChanged += (_, _) => OnModelRowsChanged();
        _gridModels.RowsRemoved += (_, _) => OnModelRowsChanged();
        _gridModels.UserDeletingRow += (_, _) => MarkDirty();
        _gridModels.DataError += (_, e) => { e.ThrowException = false; };
    }

    private void ResizePanels()
    {
        if (_split.Width > 0)
            _split.SplitterDistance = Math.Max(280, (int)(_split.Width * 0.26));
    }

    private void ProviderType_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_isLoading)
            return;

        string providerType = _cmbProviderType.SelectedItem?.ToString() ?? _cmbProviderType.Text.Trim();
        bool isBrandNew = _txtName.Text.Trim() == "New Provider" || _modelRows.Count == 0;

        if (string.IsNullOrWhiteSpace(_txtBaseUrl.Text) || isBrandNew)
            _txtBaseUrl.Text = AiProviderService.GetDefaultBaseUrl(providerType);

        if (string.IsNullOrWhiteSpace(_txtEndpointPath.Text) || isBrandNew)
            _txtEndpointPath.Text = AiProviderService.GetDefaultEndpointPath(providerType);

        if (isBrandNew)
            SetModelRows(AiProviderService.GetDefaultModels(providerType));

        UpdateReauthenticateVisibility();
        MarkDirty();
        CommitEditorsToLoadedRecord();
    }

    private void Editor_TextChanged(object? sender, EventArgs e)
    {
        if (_isLoading)
            return;

        MarkDirty();
    }

    private void OnModelRowsChanged()
    {
        if (_isLoading)
            return;

        MarkDirty();
        _gridModels.Refresh();
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
            ShowOperationError("Save providers", ex);
            return false;
        }
    }

    public bool PromptSaveIfDirty()
    {
        if (!IsDirty)
            return true;

        DialogResult result = MessageBox.Show(
            FindForm(),
            "You have unsaved provider changes. Save before continuing?",
            "AI Providers",
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
            _records.AddRange(_providerService.LoadOrSeed().Select(x => x.Clone()));
            RebuildTree(selectId);
            _lblPath.Text = "JSON: " + _providerService.GetStoragePath();
        }
        finally
        {
            _isLoading = false;
        }

        IsDirty = false;
        DirtyChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RebuildTree(string? selectId)
    {
        _treeProviders.BeginUpdate();
        _treeProviders.Nodes.Clear();

        TreeNode? nodeToSelect = null;

        foreach (AiProviderDefinition record in _records)
        {
            int modelCount = AiProviderService.GetModelNames(record).Count;
            string providerLabel = string.IsNullOrWhiteSpace(record.Name)
                ? $"(unnamed provider) ({modelCount})"
                : $"{record.Name} ({modelCount})";

            TreeNode providerNode = new(providerLabel)
                { Tag = record, ImageIndex = ProviderImageIndex, SelectedImageIndex = ProviderImageIndex };

            foreach (AiProviderModelDefinition model in AiProviderService.NormalizeModels(record.Models,
                         record.ProviderType))
            {
                TreeNode modelNode = new(model.Name)
                    { Tag = model, ImageIndex = ModelImageIndex, SelectedImageIndex = ModelImageIndex };
                providerNode.Nodes.Add(modelNode);
            }

            _treeProviders.Nodes.Add(providerNode);

            if (!string.IsNullOrWhiteSpace(selectId) &&
                string.Equals(record.Id, selectId, StringComparison.OrdinalIgnoreCase))
            {
                nodeToSelect = providerNode;
            }
        }

        _treeProviders.EndUpdate();

        if (nodeToSelect != null)
        {
            nodeToSelect.Expand();
            _treeProviders.SelectedNode = nodeToSelect;
        }
        else if (_treeProviders.Nodes.Count > 0)
        {
            _treeProviders.Nodes[0].Expand();
            _treeProviders.SelectedNode = _treeProviders.Nodes[0];
        }
        else
        {
            ClearEditors();
        }

        LoadSelectedRecordIntoEditors();
    }

    private AiProviderDefinition? GetSelectedProvider()
    {
        TreeNode? node = _treeProviders.SelectedNode;
        if (node == null)
            return null;

        if (node.Tag is AiProviderDefinition provider)
            return provider;

        if (node.Parent?.Tag is AiProviderDefinition parentProvider)
            return parentProvider;

        return null;
    }

    private void OnProviderSelectionChanged()
    {
        if (_isLoading)
            return;

        AiProviderDefinition? selected = GetSelectedProvider();
        if (selected != null && string.Equals(selected.Id, _loadedRecordId, StringComparison.OrdinalIgnoreCase))
            return;

        CommitEditorsToLoadedRecord();
        LoadSelectedRecordIntoEditors();
    }

    private void LoadSelectedRecordIntoEditors()
    {
        AiProviderDefinition? record = GetSelectedProvider();
        if (record == null)
        {
            ClearEditors();
            return;
        }

        bool wasLoading = _isLoading;
        _isLoading = true;
        try
        {
            _loadedRecordId = record.Id;
            _txtName.Text = record.Name;
            _txtId.Text = record.Id;
            _cmbProviderType.SelectedItem = record.ProviderType;
            _txtBaseUrl.Text = record.BaseUrl;
            _txtEndpointPath.Text = record.EndpointPath;
            _txtApiKey.Text = record.ApiKey;
            SetModelRows(record.Models);
            UpdateReauthenticateVisibility();
        }
        finally
        {
            _isLoading = wasLoading;
        }
    }

    private void SetModelRows(IEnumerable<AiProviderModelDefinition> models)
    {
        string providerType = string.IsNullOrWhiteSpace(_cmbProviderType.Text)
            ? (_records.FirstOrDefault(x =>
                   string.Equals(x.Id, _loadedRecordId, StringComparison.OrdinalIgnoreCase))?.ProviderType ??
               AiProviderTypes.OpenAI)
            : _cmbProviderType.Text.Trim();

        _modelRows = new BindingList<ModelGridRow>(AiProviderService.NormalizeModels(models, providerType)
            .Select(ModelGridRow.FromModel)
            .ToList());
        _gridModels.DataSource = _modelRows;
    }

    private void CommitEditorsToLoadedRecord()
    {
        if (_isLoading || string.IsNullOrWhiteSpace(_loadedRecordId))
            return;

        AiProviderDefinition? record =
            _records.FirstOrDefault(x =>
                string.Equals(x.Id, _loadedRecordId, StringComparison.OrdinalIgnoreCase));
        if (record == null)
            return;

        _gridModels.EndEdit();
        if (_gridModels.IsCurrentCellDirty)
            _gridModels.CommitEdit(DataGridViewDataErrorContexts.Commit);

        record.Name = _txtName.Text.Trim();
        record.ProviderType = _cmbProviderType.Text.Trim();
        record.BaseUrl = _txtBaseUrl.Text.Trim();
        record.EndpointPath = _txtEndpointPath.Text.Trim();
        record.ApiKey = _txtApiKey.Text.Trim();
        record.Models = BuildModelsFromGrid(record.ProviderType);

        bool wasLoading = _isLoading;
        _isLoading = true;
        try
        {
            RefreshSelectedProviderNode(record);
        }
        finally
        {
            _isLoading = wasLoading;
        }
    }

    private void RefreshSelectedProviderNode(AiProviderDefinition record)
    {
        TreeNode? providerNode = FindProviderNode(record.Id);
        if (providerNode == null)
            return;

        int modelCount = AiProviderService.GetModelNames(record).Count;
        providerNode.Text = string.IsNullOrWhiteSpace(record.Name)
            ? $"(unnamed provider) ({modelCount})"
            : $"{record.Name} ({modelCount})";

        bool wasExpanded = providerNode.IsExpanded;

        providerNode.Nodes.Clear();
        foreach (AiProviderModelDefinition model in AiProviderService.NormalizeModels(record.Models,
                     record.ProviderType))
        {
            providerNode.Nodes.Add(new TreeNode(model.Name)
                { Tag = model, ImageIndex = ModelImageIndex, SelectedImageIndex = ModelImageIndex });
        }

        if (wasExpanded)
            providerNode.Expand();
    }

    private TreeNode? FindProviderNode(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        foreach (TreeNode node in _treeProviders.Nodes)
        {
            if (node.Tag is AiProviderDefinition provider &&
                string.Equals(provider.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return node;
            }
        }

        return null;
    }

    private List<AiProviderModelDefinition> BuildModelsFromGrid(string providerType)
    {
        List<AiProviderModelDefinition> raw = _modelRows
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .Select(x => new AiProviderModelDefinition
            {
                Name = x.Name.Trim(),
                SupportsImages = x.SupportsImages,
                SupportsTemperature = x.SupportsTemperature
            })
            .ToList();

        return AiProviderService.NormalizeModels(raw, providerType);
    }

    private void CreateNewRecord()
    {
        CommitEditorsToLoadedRecord();

        AiProviderDefinition record = new()
        {
            Id = Guid.NewGuid().ToString("n"),
            Name = "New Provider",
            ProviderType = string.Empty,
            BaseUrl = string.Empty,
            EndpointPath = string.Empty,
            ApiKey = string.Empty,
            Models = new List<AiProviderModelDefinition>()
        };

        _records.Add(record);
        _isLoading = true;
        try
        {
            RebuildTree(record.Id);
        }
        finally
        {
            _isLoading = false;
        }

        MarkDirty();
    }

    private void CloneSelectedRecord()
    {
        AiProviderDefinition? selected = GetSelectedProvider();
        if (selected == null)
            return;

        CommitEditorsToLoadedRecord();
        AiProviderDefinition copy = selected.Clone();
        copy.Id = Guid.NewGuid().ToString("n");
        copy.Name = string.IsNullOrWhiteSpace(copy.Name) ? "Provider Copy" : copy.Name + " Copy";

        int masterIndex = _records.IndexOf(selected);
        _records.Insert(masterIndex + 1, copy);

        _isLoading = true;
        try
        {
            RebuildTree(copy.Id);
        }
        finally
        {
            _isLoading = false;
        }

        MarkDirty();
    }

    private void DeleteSelectedRecord()
    {
        AiProviderDefinition? selected = GetSelectedProvider();
        if (selected == null)
            return;

        DialogResult result = MessageBox.Show(
            FindForm(),
            $"Delete provider '{selected.Name}'?",
            "AI Providers",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (result != DialogResult.Yes)
            return;

        int index = _records.IndexOf(selected);
        _records.Remove(selected);

        string? nextId = null;
        if (_records.Count > 0)
        {
            int nextIndex = Math.Max(0, Math.Min(_records.Count - 1, index - 1));
            nextId = _records[nextIndex].Id;
        }

        _isLoading = true;
        try
        {
            RebuildTree(nextId);
        }
        finally
        {
            _isLoading = false;
        }

        MarkDirty();
    }

    private void ImportViaWizard()
    {
        CommitEditorsToLoadedRecord();

        using ProviderImportWizardForm wizard = new();
        if (wizard.ShowDialog(FindForm()) == DialogResult.OK && wizard.ImportedProvider != null)
        {
            _records.Add(wizard.ImportedProvider);
            _isLoading = true;
            try
            {
                RebuildTree(wizard.ImportedProvider.Id);
            }
            finally
            {
                _isLoading = false;
            }

            MarkDirty();
        }
    }

    private void ImportOpenAi()
    {
        CommitEditorsToLoadedRecord();

        using OpenAiImportForm importForm = new();
        if (importForm.ShowDialog(FindForm()) == DialogResult.OK)
        {
            if (string.IsNullOrWhiteSpace(importForm.ImportedApiKey))
                return;

            AiProviderDefinition record = new()
            {
                Id = Guid.NewGuid().ToString("n"),
                Name = "OpenAI (Imported)",
                ProviderType = AiProviderTypes.OpenAI,
                BaseUrl = AiProviderService.GetDefaultBaseUrl(AiProviderTypes.OpenAI),
                EndpointPath = AiProviderService.GetDefaultEndpointPath(AiProviderTypes.OpenAI),
                ApiKey = importForm.ImportedApiKey,
                Models = AiProviderService.GetDefaultModels(AiProviderTypes.OpenAI)
            };

            AddImportedRecord(record);
        }
    }

    private void ImportGrok()
    {
        CommitEditorsToLoadedRecord();

        using GrokImportForm importForm = new();
        if (importForm.ShowDialog(FindForm()) == DialogResult.OK)
        {
            if (string.IsNullOrWhiteSpace(importForm.ImportedApiKey))
                return;

            AiProviderDefinition record = new()
            {
                Id = Guid.NewGuid().ToString("n"),
                Name = "GROK (Imported)",
                ProviderType = AiProviderTypes.Grok,
                BaseUrl = AiProviderService.GetDefaultBaseUrl(AiProviderTypes.Grok),
                EndpointPath = AiProviderService.GetDefaultEndpointPath(AiProviderTypes.Grok),
                ApiKey = importForm.ImportedApiKey,
                Models = AiProviderService.GetDefaultModels(AiProviderTypes.Grok)
            };

            AddImportedRecord(record);
        }
    }

    private void ImportClaude()
    {
        CommitEditorsToLoadedRecord();

        using ClaudeImportForm importForm = new();
        if (importForm.ShowDialog(FindForm()) == DialogResult.OK)
        {
            if (string.IsNullOrWhiteSpace(importForm.ImportedApiKey))
                return;

            AiProviderDefinition record = new()
            {
                Id = Guid.NewGuid().ToString("n"),
                Name = "Claude (Imported)",
                ProviderType = AiProviderTypes.Claude,
                BaseUrl = AiProviderService.GetDefaultBaseUrl(AiProviderTypes.Claude),
                EndpointPath = AiProviderService.GetDefaultEndpointPath(AiProviderTypes.Claude),
                ApiKey = importForm.ImportedApiKey,
                Models = AiProviderService.GetDefaultModels(AiProviderTypes.Claude)
            };

            AddImportedRecord(record);
        }
    }

    private void ImportGemini()
    {
        CommitEditorsToLoadedRecord();

        using GeminiImportForm importForm = new();
        if (importForm.ShowDialog(FindForm()) == DialogResult.OK)
        {
            if (string.IsNullOrWhiteSpace(importForm.ImportedApiKey))
                return;

            AiProviderDefinition record = new()
            {
                Id = Guid.NewGuid().ToString("n"),
                Name = "Google Gemini (Imported)",
                ProviderType = AiProviderTypes.GoogleGemini,
                BaseUrl = AiProviderService.GetDefaultBaseUrl(AiProviderTypes.GoogleGemini),
                EndpointPath = AiProviderService.GetDefaultEndpointPath(AiProviderTypes.GoogleGemini),
                ApiKey = importForm.ImportedApiKey,
                Models = AiProviderService.GetDefaultModels(AiProviderTypes.GoogleGemini)
            };

            AddImportedRecord(record);
        }
    }

    private void ImportMistral()
    {
        CommitEditorsToLoadedRecord();

        using MistralImportForm importForm = new();
        if (importForm.ShowDialog(FindForm()) == DialogResult.OK)
        {
            if (string.IsNullOrWhiteSpace(importForm.ImportedApiKey))
                return;

            AiProviderDefinition record = new()
            {
                Id = Guid.NewGuid().ToString("n"),
                Name = "Mistral (Imported)",
                ProviderType = AiProviderTypes.Mistral,
                BaseUrl = AiProviderService.GetDefaultBaseUrl(AiProviderTypes.Mistral),
                EndpointPath = AiProviderService.GetDefaultEndpointPath(AiProviderTypes.Mistral),
                ApiKey = importForm.ImportedApiKey,
                Models = AiProviderService.GetDefaultModels(AiProviderTypes.Mistral)
            };

            AddImportedRecord(record);
        }
    }

    private void ImportOpenAIOAuth()
    {
        CommitEditorsToLoadedRecord();

        using ChatGPTOAuthImportForm importForm = new();
        if (importForm.ShowDialog(FindForm()) == DialogResult.OK)
        {
            AiProviderDefinition record = new()
            {
                Id = importForm.ImportedProviderId,
                Name = "ChatGPT OAuth (Imported)",
                ProviderType = AiProviderTypes.ChatGPTOAuth,
                BaseUrl = AiProviderService.GetDefaultBaseUrl(AiProviderTypes.ChatGPTOAuth),
                EndpointPath = AiProviderService.GetDefaultEndpointPath(AiProviderTypes.ChatGPTOAuth),
                ApiKey = "OAuth2",
                Models = AiProviderService.GetDefaultModels(AiProviderTypes.ChatGPTOAuth)
            };

            AddImportedRecord(record);
        }
    }

    private void ImportClaudeOAuth()
    {
        CommitEditorsToLoadedRecord();

        using ClaudeOAuthImportForm importForm = new();
        if (importForm.ShowDialog(FindForm()) == DialogResult.OK)
        {
            AiProviderDefinition record = new()
            {
                Id = importForm.ImportedProviderId,
                Name = "Claude OAuth (Imported)",
                ProviderType = AiProviderTypes.ClaudeOAuth,
                BaseUrl = AiProviderService.GetDefaultBaseUrl(AiProviderTypes.ClaudeOAuth),
                EndpointPath = AiProviderService.GetDefaultEndpointPath(AiProviderTypes.ClaudeOAuth),
                ApiKey = "OAuth2",
                Models = AiProviderService.GetDefaultModels(AiProviderTypes.ClaudeOAuth)
            };

            AddImportedRecord(record);
        }
    }

    private void AddImportedRecord(AiProviderDefinition record)
    {
        _records.Add(record);
        _isLoading = true;
        try
        {
            RebuildTree(record.Id);
        }
        finally
        {
            _isLoading = false;
        }

        MarkDirty();
    }

    private void AddDefaultProviders()
    {
        CommitEditorsToLoadedRecord();

        List<AiProviderDefinition> defaultProviders = AiProviderService.GetSeedRecords();
        if (defaultProviders == null || defaultProviders.Count == 0)
            return;

        foreach (var provider in defaultProviders)
        {
            provider.Id = Guid.NewGuid().ToString("n");
            _records.Add(provider);
        }

        _isLoading = true;
        try
        {
            RebuildTree(defaultProviders.LastOrDefault()?.Id);
        }
        finally
        {
            _isLoading = false;
        }

        MarkDirty();
    }

    private void ReauthenticateOAuth()
    {
        string? providerType = _cmbProviderType.SelectedItem?.ToString();
        if (providerType == AiProviderTypes.ClaudeOAuth)
        {
            using ClaudeOAuthImportForm importForm = new();
            if (importForm.ShowDialog(FindForm()) == DialogResult.OK)
            {
                MessageBox.Show(FindForm(), "Re-authentication completed successfully.", "AI Providers",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        else
        {
            using ChatGPTOAuthImportForm importForm = new();
            if (importForm.ShowDialog(FindForm()) == DialogResult.OK)
            {
                MessageBox.Show(FindForm(), "Re-authentication completed successfully.", "AI Providers",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }

    private void UpdateReauthenticateVisibility()
    {
        string? providerType = _cmbProviderType.SelectedItem?.ToString();
        _btnReauthenticate.Visible = providerType == AiProviderTypes.ChatGPTOAuth || providerType == AiProviderTypes.ClaudeOAuth;
    }

    private void ReloadWithPrompt()
    {
        if (IsDirty)
        {
            DialogResult result = MessageBox.Show(
                FindForm(),
                "Discard unsaved provider changes and reload from disk?",
                "AI Providers",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
                return;
        }

        LoadRecords(selectId: _txtId.Text.Trim());
    }

    private void OpenJsonFile()
    {
        _providerService.EnsureFileExists();
        Process.Start(new ProcessStartInfo
        {
            FileName = _providerService.GetStoragePath(),
            UseShellExecute = true
        });
    }

    private void ExportProviders(bool includeApiKeys)
    {
        CommitEditorsToLoadedRecord();

        if (_records.Count == 0)
        {
            MessageBox.Show(FindForm(), "There are no providers to export.", "AI Providers",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (includeApiKeys)
        {
            DialogResult result = MessageBox.Show(FindForm(),
                "This export will include API keys in plain text. Continue?",
                "AI Providers", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (result != DialogResult.Yes)
                return;
        }

        string storagePath = _providerService.GetStoragePath();
        using SaveFileDialog dialog = new()
        {
            Title = includeApiKeys
                ? "Export Providers (with API keys)"
                : "Export Providers (without API keys)",
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = "json",
            AddExtension = true,
            OverwritePrompt = true,
            RestoreDirectory = true,
            InitialDirectory = GetExportDirectory(storagePath),
            FileName = BuildExportFileName(storagePath, includeApiKeys)
        };

        if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
            return;

        List<AiProviderDefinition> snapshot = CreateExportSnapshot(includeApiKeys);
        string json = JsonSerializer.Serialize(snapshot, ExportJsonOptions);

        string? exportDirectory = Path.GetDirectoryName(dialog.FileName);
        if (!string.IsNullOrWhiteSpace(exportDirectory))
            Directory.CreateDirectory(exportDirectory);

        File.WriteAllText(dialog.FileName, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        MessageBox.Show(FindForm(),
            $"Exported {snapshot.Count} provider(s) to:{Environment.NewLine}{dialog.FileName}",
            "AI Providers", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private List<AiProviderDefinition> CreateExportSnapshot(bool includeApiKeys)
    {
        return _records
            .Select(record =>
            {
                AiProviderDefinition copy = record.Clone();
                if (!includeApiKeys)
                    copy.ApiKey = string.Empty;
                return copy;
            })
            .ToList();
    }

    private static string GetExportDirectory(string storagePath)
    {
        string? storageDirectory = Path.GetDirectoryName(storagePath);
        if (!string.IsNullOrWhiteSpace(storageDirectory) && Directory.Exists(storageDirectory))
            return storageDirectory;

        string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return string.IsNullOrWhiteSpace(documentsPath)
            ? Environment.CurrentDirectory
            : documentsPath;
    }

    private static string BuildExportFileName(string storagePath, bool includeApiKeys)
    {
        string baseName = Path.GetFileNameWithoutExtension(storagePath);
        if (string.IsNullOrWhiteSpace(baseName))
            baseName = "ai-providers";

        string exportType = includeApiKeys ? "with-api-keys" : "without-api-keys";
        return $"{baseName}-export-{exportType}-{DateTime.Now:yyyyMMdd-HHmmss}.json";
    }

    private void SaveAll()
    {
        CommitEditorsToLoadedRecord();

        if (_records.Count == 0)
            throw new InvalidOperationException("At least one provider must be configured.");

        foreach (AiProviderDefinition record in _records)
        {
            if (string.IsNullOrWhiteSpace(record.Name))
                throw new InvalidOperationException("Every provider must have a name.");
            if (string.IsNullOrWhiteSpace(record.ProviderType))
                throw new InvalidOperationException($"Provider '{record.Name}' must have a provider type.");
            if (AiProviderService.GetModelNames(record).Count == 0)
                throw new InvalidOperationException($"Provider '{record.Name}' must have at least one model.");
        }

        _providerService.Save(_records);
        IsDirty = false;
        DirtyChanged?.Invoke(this, EventArgs.Empty);
    }

    private void AddModelRow()
    {
        string providerType = _cmbProviderType.Text.Trim();
        AiProviderModelDefinition model = AiProviderService.CreateModel(providerType, "new-model");
        _modelRows.Add(ModelGridRow.FromModel(model));
        if (_modelRows.Count > 0)
        {
            int rowIndex = _modelRows.Count - 1;
            _gridModels.ClearSelection();
            _gridModels.Rows[rowIndex].Selected = true;
            _gridModels.CurrentCell = _gridModels.Rows[rowIndex].Cells[0];
            _gridModels.BeginEdit(true);
        }

        MarkDirty();
    }

    private void CloneSelectedModelRow()
    {
        if (_gridModels.CurrentRow == null || _gridModels.CurrentRow.Index < 0 ||
            _gridModels.CurrentRow.Index >= _modelRows.Count)
            return;

        ModelGridRow source = _modelRows[_gridModels.CurrentRow.Index];
        ModelGridRow clone = new()
        {
            Name = string.IsNullOrWhiteSpace(source.Name) ? "model-copy" : source.Name + "-copy",
            SupportsImages = source.SupportsImages,
            SupportsTemperature = source.SupportsTemperature
        };
        int insertIndex = _gridModels.CurrentRow.Index + 1;
        _modelRows.Insert(insertIndex, clone);
        _gridModels.ClearSelection();
        _gridModels.Rows[insertIndex].Selected = true;
        _gridModels.CurrentCell = _gridModels.Rows[insertIndex].Cells[0];
        MarkDirty();
    }

    private void DeleteSelectedModelRow()
    {
        if (_gridModels.CurrentRow == null || _gridModels.CurrentRow.Index < 0 ||
            _gridModels.CurrentRow.Index >= _modelRows.Count)
            return;

        _modelRows.RemoveAt(_gridModels.CurrentRow.Index);
        MarkDirty();
    }

    private void ExecuteGuarded(Action action, string operationName)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            ShowOperationError(operationName, ex);
        }
    }

    private void ShowOperationError(string operationName, Exception ex)
    {
        MessageBox.Show(
            FindForm(),
            $"{operationName} failed.{Environment.NewLine}{Environment.NewLine}{ex.Message}",
            "AI Providers",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }

    private void ClearEditors()
    {
        bool wasLoading = _isLoading;
        _isLoading = true;
        try
        {
            _loadedRecordId = null;
            _txtName.Clear();
            _txtId.Clear();
            _cmbProviderType.SelectedIndex = -1;
            _txtBaseUrl.Clear();
            _txtEndpointPath.Clear();
            _txtApiKey.Clear();
            SetModelRows(Array.Empty<AiProviderModelDefinition>());
            UpdateReauthenticateVisibility();
        }
        finally
        {
            _isLoading = wasLoading;
        }
    }

    private sealed class ModelGridRow
    {
        public string Name { get; set; } = string.Empty;
        public bool SupportsImages { get; set; } = true;
        public bool SupportsTemperature { get; set; } = true;

        public static ModelGridRow FromModel(AiProviderModelDefinition model)
        {
            return new ModelGridRow
            {
                Name = model.Name,
                SupportsImages = model.SupportsImages,
                SupportsTemperature = model.SupportsTemperature
            };
        }
    }
}
