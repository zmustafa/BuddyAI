using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BuddyAI.Docking;
using BuddyAI.Forms;
using BuddyAI.Helpers;
using BuddyAI.Models;
using BuddyAI.Services;
using Timer = System.Windows.Forms.Timer;
using ThemeService = BuddyAI.Services.ThemeService;

namespace BuddyAI;

public sealed partial class AIQ : Form
{
    private readonly AiProviderClient _client = new();
    private readonly PersonaService _personaService = new();
    private readonly PromptService _promptService = new();
    private readonly SnippetService _snippetService = new();
    private readonly SuggestionService _suggestionService = new();
    private readonly UsageMetricsService _usageMetricsService = new();
    private readonly DiagnosticsService _diagnostics = new();
    private readonly WorkspaceSettingsService _workspaceSettingsService = new();
    private readonly SessionStateService _sessionStateService = new();
    private readonly AiProviderService _providerService = new();
    private readonly TablerIconCache _iconCache;

    private readonly PromptAssistanceHelper _promptAssistanceHelper;
    private ConversationManager _conversationManager = null!;

    private readonly List<AiProviderDefinition> _providers = new();
    private WorkspaceSettings _workspaceSettings = new();
    private ThemeService.ThemeProfile _activeTheme = ThemeService.VisualStudioDark;

    private readonly TableLayoutPanel _root = new();
    private readonly MenuStrip _menuStrip = new();
    private readonly ToolStrip _quickAccessStrip = new();

    private readonly DockContainerHost _dockHost = new();

    private readonly DockablePanel _dockPersonaExplorer = new() { PanelTitle = "Persona Explorer" };
    private readonly DockablePanel _dockAiTools = new() { PanelTitle = "AI Tools" };
    private readonly DockablePanel _dockComposer = new() { PanelTitle = "Composer" };
    private readonly DockablePanel _dockConversations = new() { PanelTitle = "Conversations" };
    private readonly DockablePanel _dockDiagnostics = new() { PanelTitle = "Diagnostics" };

    // Keep old split containers for backward compat in CaptureWorkspaceSettingsFromShell
    private readonly SplitContainer _leftMainSplit = new();
    private readonly SplitContainer _centerToolsSplit = new();
    private readonly SplitContainer _workspaceDiagnosticsSplit = new();

    private readonly Panel _personaPanel = new();
    private readonly TextBox _txtPersonaExplorerSearch = new();
    private readonly TreeView _personaTree = new();

    private readonly TableLayoutPanel _workspaceLayout = new();
    private readonly Panel _composerPanel = new();
    private readonly TabControl _conversationTabs = new();

    private readonly ComboBox _cmbProfile = new();
    private readonly ComboBox _cmbProvider = new();
    private readonly ComboBox _cmbModel = new();
    private readonly ComboBox _cmbTemperature = new();
    private readonly ComboBox _cmbPersona = new();
    private readonly ComboBox _cmbPrefilledQuestion = new();
    private readonly TextBox _txtSystemPrompt = new();
    private readonly TextBox _txtQuestion = new();
    private readonly TextBox _txtConversationSearch = new();
    private readonly Button _btnConversationSearch = new();
    private readonly Button _btnConversationSearchClear = new();
    private readonly Button _btnBrowseImage = new();
    private readonly Button _btnSnipScreen = new();
    private readonly CheckBox _chkSnipAuto = new();
    private readonly NumericUpDown _numSnipDelay = new();
    private readonly Button _btnClearImage = new();
    private readonly Button _btnAsk = new();
    private readonly Button _btnFollowUp = new();
    private readonly Button _btnCancel = new();
    private readonly Button _btnNewConversation = new();
    private readonly PictureBox _picPreview = new();
    private readonly Label _lblImageInfo = new();

    private readonly Panel _toolsPanel = new();
    private readonly TabControl _toolTabs = new();
    private readonly ListBox _lstTemplates = new();
    private readonly ListBox _lstSnippets = new();
    private readonly ListBox _lstPromptHistory = new();
    private readonly ListBox _lstSearchResults = new();
    private readonly ListBox _lstSuggestions = new();
    private readonly TextBox _txtDiagnostics = new();
    private readonly TextBox _txtGlobalSearch = new();
    private readonly Button _btnGlobalSearch = new();
    private readonly Button _btnGlobalSearchClear = new();
    private readonly Button _btnAnalyzeClipboard = new();
    private readonly Button _btnExpandPrompt = new();
    private readonly Button _btnPromptCopy = new();
    private readonly Button _btnPromptCut = new();
    private readonly Button _btnPromptPaste = new();
    private readonly Button _btnPromptUndo = new();
    private readonly Button _btnPromptRedo = new();


    private readonly StatusStrip _statusStrip = new();
    private readonly ToolStripStatusLabel _statusState = new();
    private readonly ToolStripStatusLabel _statusModel = new();
    private readonly ToolStripStatusLabel _statusPersona = new();
    private readonly ToolStripStatusLabel _statusProfile = new();
    private readonly ToolStripStatusLabel _statusProvider = new();
    private readonly ToolStripStatusLabel _statusTokens = new();
    private readonly ToolStripStatusLabel _statusCost = new();
    private readonly ToolStripStatusLabel _statusLatency = new();
    private readonly ToolStripStatusLabel _statusConversations = new();

    private readonly ContextMenuStrip _conversationTabMenu = new();
    private readonly ContextMenuStrip _personaMenu = new();
    private readonly ContextMenuStrip _promptMenu = new();
    private readonly ContextMenuStrip _responseMenu = new();

    private readonly ToolStripMenuItem _mnuViewPersonaPanel = new("Persona Explorer") { Checked = true, CheckOnClick = true };
    private readonly ToolStripMenuItem _mnuViewToolsPanel = new("AI Tools") { Checked = true, CheckOnClick = true };
    private readonly ToolStripMenuItem _mnuViewDiagnostics = new("Diagnostics") { Checked = false, CheckOnClick = true };
    private readonly ToolStripMenuItem _mnuViewComposer = new("Composer") { Checked = true, CheckOnClick = true };
    private readonly ToolStripMenuItem _mnuViewConversations = new("Conversations") { Checked = true, CheckOnClick = true };
    private readonly ToolStripMenuItem _mnuLockDockedWindows = new("Lock Window &Layout") { Checked = false, CheckOnClick = true };

    private readonly Timer _clipboardMonitorTimer = new();
    private readonly Timer _suggestionsDebounceTimer = new() { Interval = 300 };
    private readonly Timer _autoSaveTimer = new();
    private readonly AutoCompleteStringCollection _promptAutoComplete = new();

    private CancellationTokenSource? _requestCts;
    private bool _isBusy;
    private bool _isLoadingProviders;
    private bool _isLoadingPersonas;
    private bool _suppressQuestionTextChanged;
    private bool _clipboardSuggestionAvailable;
    private bool _isPromptExpanded;
    private string _lastClipboardSignature = string.Empty;
    private string _lastStatusText = "Ready";
    private string _pendingPersonaContextName = string.Empty;

    private string? _imagePath;
    private byte[]? _imageBytes;
    private string? _imageMimeType;
    private Image? _previewImage;

    private SplitContainer? _promptSplit;
    private Form? _promptExpandWindow;

    private readonly List<PersonaRecord> _personaRecords = new();
    private readonly List<PromptItem> _templates = new();
    private readonly List<SnippetItem> _snippets = new();
    private readonly List<string> _promptHistory = new();
    private readonly List<SuggestionItem> _managedSuggestions = new();
    private readonly Dictionary<TabPage, ConversationTabState> _conversationStates = new();
    private readonly Dictionary<string, WorkspaceProfile> _profiles = new(StringComparer.OrdinalIgnoreCase);

    private const string DefaultTemperature = "1";
    private const int ConversationTabCloseButtonWidth = 18;

    private static readonly string[] SupportedTemperatures =
    {
        "0",
        ".1",
        ".2",
        ".3",
        ".4",
        ".5",
        ".6",
        ".7",
        ".8",
        ".9",
        "1",
        "1.1",
        "2",
    };

    public AIQ()
    {
        _promptAssistanceHelper = new PromptAssistanceHelper(_managedSuggestions, _templates);
        _workspaceSettings = _workspaceSettingsService.Load();
        
        _activeTheme = ThemeService.Resolve(_workspaceSettings.Theme);
        _iconCache = new TablerIconCache(_activeTheme.Text);

        BuildProfiles();

        Text = "BuddyAI Desktop";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1440, 920);
        Size = new Size(_workspaceSettings.WindowWidth, _workspaceSettings.WindowHeight);
        KeyPreview = true;
        AutoScaleMode = AutoScaleMode.Dpi;

        BuildUi();
        _conversationManager = new ConversationManager(_conversationTabs, _conversationStates, _responseMenu, _diagnostics);
        WireEvents();
        LoadProviders();

        LoadPersonas();
        RestoreWorkspace();
        ApplyTheme(_workspaceSettings.Theme);
        SetStatus("Ready");
        UpdateUiState();
        RefreshUsageStatus();
        UpdateAutoComplete();
        RestoreSessionFromDisk();

        Shown += (_, __) =>
        {
            ApplyLayoutPreset("standard");
            TryAutoSelectInitialPersona();
            RefreshPersonaExplorer();
            UpdateStatusBar();
            _diagnostics.Info("BuddyAI shell initialized.");
            RestoreConversationViewsAfterShow();
        };
        BuildDiagnosticsPanel();
    }

    private void LoadProviders(string? preferredProviderName = null, string? preferredModel = null)
    {
        string currentProvider = string.IsNullOrWhiteSpace(preferredProviderName)
            ? (_cmbProvider.SelectedItem as AiProviderDefinition)?.Id ?? GetComboSelectionText(_cmbProvider)
            : preferredProviderName.Trim();
            
        string currentModel = string.IsNullOrWhiteSpace(preferredModel)
            ? GetComboSelectionText(_cmbModel)
            : preferredModel.Trim();

        _providers.Clear();
        _providers.AddRange(_providerService.LoadOrSeed());

        _isLoadingProviders = true;
        try
        {
            _cmbProvider.BeginUpdate();
            _cmbProvider.Items.Clear();
            _cmbProvider.SelectedIndex = -1;
            _cmbProvider.ResetText();

            if (_providers.Count > 0)
                _cmbProvider.Items.AddRange(_providers.Cast<object>().ToArray());
        }
        finally
        {
            _cmbProvider.EndUpdate();
            _isLoadingProviders = false;
        }

        if (_providers.Count == 0)
        {
            _cmbModel.BeginUpdate();
            try
            {
                _cmbModel.Items.Clear();
                _cmbModel.SelectedIndex = -1;
                _cmbModel.ResetText();
            }
            finally
            {
                _cmbModel.EndUpdate();
            }

            _cmbProvider.SelectedIndex = -1;
            _cmbProvider.ResetText();
            UpdateTemperatureControlState();
            UpdateImageInfo();
            return;
        }

        if (!TrySelectProviderForModel(currentModel, currentProvider, allowMissingModel: true))
            TrySelectProviderForModel(null, _providers[0].Name, allowMissingModel: true);

        UpdateStatusBar();
        UpdateUiState();
    }

    private AiProviderDefinition? GetSelectedProvider()
    {
        if (_cmbProvider.SelectedItem is AiProviderDefinition provider)
            return provider;

        return FindProviderByName(_cmbProvider.Text);
    }

    private AiProviderDefinition? FindProviderByName(string? providerName)
    {
        return _providerService.FindByName(_providers, providerName);
    }

    private AiProviderDefinition? FindProviderByModel(string? model)
    {
        return _providerService.FindByModel(_providers, model);
    }

    private AiProviderModelDefinition? GetSelectedModelDefinition(bool includeHeuristicFallback = true)
    {
        return AiProviderService.FindModel(GetSelectedProvider(), _cmbModel.Text, includeHeuristicFallback);
    }

    private bool SelectedModelSupportsImages()
    {
        return GetSelectedModelDefinition()?.SupportsImages ?? true;
    }

    private bool SelectedModelSupportsTemperature()
    {
        return GetSelectedModelDefinition()?.SupportsTemperature ?? true;
    }

    private bool TrySelectProviderForModel(string? model, string? preferredProviderName = null, bool allowMissingModel = false)
    {
        if (_providers.Count == 0)
            return false;

        AiProviderDefinition? provider = null;
        
        if (!string.IsNullOrWhiteSpace(preferredProviderName))
            provider = _providers.FirstOrDefault(x => string.Equals(x.Id, preferredProviderName, StringComparison.OrdinalIgnoreCase));
            
        if (provider == null)
            provider = FindProviderByName(preferredProviderName) ?? FindProviderByModel(model) ?? _providers.FirstOrDefault();

        if (provider == null)
            return false;

        _isLoadingProviders = true;
        try
        {
            int idx = _cmbProvider.Items.IndexOf(provider);
            if (idx >= 0)
                _cmbProvider.SelectedIndex = idx;
            else
            {
                int providerIndex = FindComboIndex(_cmbProvider, provider.Name);
                _cmbProvider.SelectedIndex = providerIndex >= 0 ? providerIndex : 0;
            }
        }
        finally
        {
            _isLoadingProviders = false;
        }

        PopulateModelsForSelectedProvider(model, allowMissingModel);
        return true;
    }

    private void PopulateModelsForSelectedProvider(string? preferredModel = null, bool allowMissingModel = false)
    {
        AiProviderDefinition? provider = GetSelectedProvider();
        string desiredModel = string.IsNullOrWhiteSpace(preferredModel)
            ? GetComboSelectionText(_cmbModel)
            : preferredModel.Trim();

        List<string> models = provider == null
            ? new List<string>()
            : AiProviderService.GetModelNames(provider);

        if (allowMissingModel
            && !string.IsNullOrWhiteSpace(desiredModel)
            && !models.Any(x => string.Equals(x, desiredModel, StringComparison.OrdinalIgnoreCase)))
        {
            models.Add(desiredModel);
        }

        _isLoadingProviders = true;
        try
        {
            _cmbModel.BeginUpdate();
            _cmbModel.Items.Clear();
            _cmbModel.SelectedIndex = -1;
            _cmbModel.ResetText();

            if (models.Count > 0)
                _cmbModel.Items.AddRange(models.Cast<object>().ToArray());

            int targetIndex = string.IsNullOrWhiteSpace(desiredModel)
                ? -1
                : FindComboIndex(_cmbModel, desiredModel);

            _cmbModel.SelectedIndex = targetIndex >= 0
                ? targetIndex
                : _cmbModel.Items.Count > 0 ? 0 : -1;
        }
        finally
        {
            _cmbModel.EndUpdate();
            _isLoadingProviders = false;
        }

        UpdateTemperatureControlState();
    }

    private async Task TryFetchDynamicModelsAsync(AiProviderDefinition provider)
    {
        try
        {
            List<string> dynamicModels = await _client.GetModelsAsync(provider);
            if (dynamicModels.Count == 0) return;

            string currentModel = GetComboSelectionText(_cmbModel);

            _isLoadingProviders = true;
            try
            {
                _cmbModel.BeginUpdate();
                _cmbModel.Items.Clear();
                _cmbModel.SelectedIndex = -1;
                _cmbModel.ResetText();

                // Merge old configured models with dynamically fetched ones
                var mergedModels = AiProviderService.GetModelNames(provider)
                    .Concat(dynamicModels)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (mergedModels.Count > 0)
                    _cmbModel.Items.AddRange(mergedModels.Cast<object>().ToArray());

                int targetIndex = string.IsNullOrWhiteSpace(currentModel)
                    ? -1
                    : FindComboIndex(_cmbModel, currentModel);

                if (targetIndex >= 0)
                {
                    _cmbModel.SelectedIndex = targetIndex;
                }
                else if (dynamicModels.Count > 0)
                {
                    // Select first dynamic model if possible
                    int dynIndex = FindComboIndex(_cmbModel, dynamicModels[0]);
                    _cmbModel.SelectedIndex = dynIndex >= 0 ? dynIndex : 0;
                }
                else if (_cmbModel.Items.Count > 0)
                {
                    _cmbModel.SelectedIndex = 0;
                }
            }
            finally
            {
                _cmbModel.EndUpdate();
                _isLoadingProviders = false;
            }

            UpdateTemperatureControlState();
        }
        catch (Exception ex)
        {
            _diagnostics.Error($"Failed to fetch dynamic models for {provider.Name}: {ex.Message}");
        }
    }

    private void UpdateTemperatureControlState()
    {
        AiProviderDefinition? provider = GetSelectedProvider();
        bool hasSelection = provider != null && !string.IsNullOrWhiteSpace(_cmbModel.Text);
        bool supportsTemperature = hasSelection && SelectedModelSupportsTemperature();
        bool supportsImages = hasSelection && SelectedModelSupportsImages();

        _cmbTemperature.Enabled = !_isBusy && supportsTemperature;
        _btnBrowseImage.Enabled = !_isBusy && supportsImages;
        _btnSnipScreen.Enabled = !_isBusy && supportsImages;
    }

    private void BrowseImage()
    {
        if (!SelectedModelSupportsImages())
        {
            MessageBox.Show(this, "The selected model does not support image input.", "Image Input", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using OpenFileDialog dialog = new()
        {
            Title = "Select Image",
            Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp|All Files|*.*",
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        LoadImageFromFile(dialog.FileName);
    }

    private Task SnipScreenAsync()
        => SnipScreenAsync(new SnipCaptureOptions
        {
            AutoAskAfterCapture = _chkSnipAuto.Checked,
            HideMainWindowDuringCapture = true,
            RestoreMainWindowAfterCapture = true,
            OpenResultWindowOnSuccess = false,
            PreferMainWindowForDialogs = true
        });

    private async Task SnipScreenAsync(SnipCaptureOptions? options)
    {
        SnipCaptureOptions request = options ?? new SnipCaptureOptions
        {
            AutoAskAfterCapture = _chkSnipAuto.Checked,
            HideMainWindowDuringCapture = true,
            RestoreMainWindowAfterCapture = true,
            OpenResultWindowOnSuccess = false,
            PreferMainWindowForDialogs = true
        };

        if (_isBusy || _isSnipInProgress)
            return;

        if (!SelectedModelSupportsImages())
        {
            ShowAppMessage(
                "The selected model does not support image input.",
                "Image Input",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information,
                request.PreferMainWindowForDialogs);
            return;
        }

        bool shellWasVisible = Visible;
        FormWindowState shellWindowState = WindowState;
        bool shouldHideShell = request.HideMainWindowDuringCapture && shellWasVisible && shellWindowState != FormWindowState.Minimized;
        bool shouldRestoreShell = request.RestoreMainWindowAfterCapture && shellWasVisible && shellWindowState != FormWindowState.Minimized;

        void RestoreShell()
        {
            if (!shouldRestoreShell)
                return;

            if (!Visible)
                Show();

            WindowState = shellWindowState;
            Activate();
        }

        _isSnipInProgress = true;
        UpdateUiState();

        try
        {
            int delaySeconds = (int)_numSnipDelay.Value;

            await Task.Delay(150);
            for (int remaining = delaySeconds; remaining >= 1; remaining--)
            {
                SetStatus("Snipping starts in " + remaining + " second" + ( remaining == 1 ? string.Empty : "s") + "...");
                await Task.Delay(1000);
            }

            if (shouldHideShell)
                Hide();

            using ScreenSnipOverlayForm snipForm = new();
            DialogResult result = snipForm.ShowDialog();

            RestoreShell();

            if (result != DialogResult.OK || snipForm.CapturedImage == null)
            {
                SetStatus("Snip cancelled.");
                return;
            }

            ApplyImageFromBitmap(snipForm.CapturedImage, null, "image/png");
            SetStatus("Snip captured.");
            _diagnostics.Info("Screenshot captured.");

            if (request.AutoAskAfterCapture)
            {
                await AskAsync(
                    null,
                    false,
                    new AskExecutionOptions
                    {
                        OpenResultWindowOnSuccess = request.OpenResultWindowOnSuccess,
                        PreferMainWindowForDialogs = request.PreferMainWindowForDialogs
                    });
            }
        }
        catch (Exception ex)
        {
            RestoreShell();
            _diagnostics.Error("Snip failed: " + ex.Message);
            ShowAppMessage(
                ex.Message,
                "Snip Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error,
                request.PreferMainWindowForDialogs);
        }
        finally
        {
            _isSnipInProgress = false;
            UpdateUiState();
        }
    }

    private void LoadImageFromFile(string filePath)
    {
        string? extension = Path.GetExtension(filePath)?.ToLowerInvariant();
        if (!IsSupportedImageExtension(extension))
            throw new InvalidOperationException("Unsupported image type. Use PNG, JPG, JPEG, BMP, GIF, or WEBP.");

        using Bitmap bitmap = new(filePath);
        ApplyImageFromBitmap(bitmap, filePath, GetMimeTypeFromExtension(extension ?? ".png"));
        SetStatus("Image loaded.");
        _diagnostics.Info("Image loaded from file.");
    }

   private void ApplyImageFromBitmap(Bitmap bitmap, string? sourcePath, string mimeType)
    {
        using MemoryStream ms = new();
        bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        _imageBytes = ms.ToArray();
        _imagePath = sourcePath;
        _imageMimeType = mimeType;

        ReplacePreviewImage(bitmap);
        UpdateImageInfo();
        UpdateUiState();
    }

    private void ReplacePreviewImage(Image image)
    {
        Image? old = _previewImage;
        _previewImage = new Bitmap(image);
        _picPreview.Image = _previewImage;
        old?.Dispose();
    }

    private void ClearSelectedImage()
    {
        _imageBytes = null;
        _imagePath = null;
        _imageMimeType = null;
        Image? old = _previewImage;
        _previewImage = null;
        _picPreview.Image = null;
        old?.Dispose();
        UpdateImageInfo();
        UpdateUiState();
        SetStatus("Image cleared.");
    }

    private async Task AskAsync(string? previousResponseId, bool isFollowUp = false, AskExecutionOptions? executionOptions = null)
    {
        if (_isBusy)
            return;

        AiProviderDefinition? provider = GetSelectedProvider();
        string model = _cmbModel.Text.Trim();
        string temperatureText = GetSelectedTemperatureText();
        double temperature = GetSelectedTemperature();
        string userPrompt = _txtQuestion.Text.Trim();
        string systemPrompt = _txtSystemPrompt.Text.Trim();
        string persona = _cmbPersona.Text.Trim();

        if (provider == null)
        {
            ShowAppMessage("Select an AI provider.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning, executionOptions?.PreferMainWindowForDialogs ?? true);
            return;
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            ShowAppMessage("Select a model.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning, executionOptions?.PreferMainWindowForDialogs ?? true);
            return;
        }

        if ((_imageBytes?.Length ?? 0) > 0 && !SelectedModelSupportsImages())
        {
            ShowAppMessage("The selected model does not support image input. Clear the image or choose a different model.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning, executionOptions?.PreferMainWindowForDialogs ?? true);
            return;
        }

        if (string.IsNullOrWhiteSpace(userPrompt))
        {
            ShowAppMessage("Enter a prompt.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning, executionOptions?.PreferMainWindowForDialogs ?? true);
            return;
        }

        if (string.IsNullOrWhiteSpace(systemPrompt))
        {
            ShowAppMessage("Enter or select a system prompt.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning, executionOptions?.PreferMainWindowForDialogs ?? true);
            return;
        }

        ConversationTabState state;
        if (isFollowUp)
        {
            ConversationTabState? activeConversation = GetActiveConversation();
            if (activeConversation == null)
            {
                ShowAppMessage("Select a conversation first.", "Follow Up", MessageBoxButtons.OK, MessageBoxIcon.Warning, executionOptions?.PreferMainWindowForDialogs ?? true);
                return;
            }

            state = activeConversation;
        }
        else
        {
            state = CreateConversationTab(userPrompt);
        }

        bool canUsePreviousResponseId = isFollowUp
            && AiProviderClient.SupportsStatefulConversation(provider)
            && !string.IsNullOrWhiteSpace(previousResponseId)
            && string.Equals(state.Provider, provider.Name, StringComparison.OrdinalIgnoreCase);

        AiProviderClient.ConversationContext? conversationContext = null;
        if (isFollowUp && !canUsePreviousResponseId && !string.IsNullOrWhiteSpace(state.LastPrompt) && !string.IsNullOrWhiteSpace(state.RawResponseText))
        {
            conversationContext = new AiProviderClient.ConversationContext
            {
                PreviousUserPrompt = state.LastPrompt,
                PreviousAssistantResponse = state.RawResponseText
            };
        }

        _requestCts?.Dispose();
        _requestCts = new CancellationTokenSource();

        try
        {
            _isBusy = true;
            state.IsPending = true;
            UpdateConversationTabText(state);
            UpdateUiState();

            _conversationTabs.SelectedTab = state.TabPage;
            SetStatus(isFollowUp ? "Sending follow-up request..." : "Sending request...");
            _diagnostics.Info($"Request started for provider {provider.Name}, model {model}.");

            byte[] imageBytes = _imageBytes ?? Array.Empty<byte>();
            string imageMimeType = string.IsNullOrWhiteSpace(_imageMimeType) ? "image/png" : _imageMimeType!;

            Stopwatch stopwatch = Stopwatch.StartNew();
            AiProviderClient.AiResponse response = await _client.SendWithImageAndStateAsync(
                provider,
                model,
                temperature,
                userPrompt,
                systemPrompt,
                imageBytes,
                imageMimeType,
                canUsePreviousResponseId ? previousResponseId : null,
                conversationContext,
                _requestCts.Token);
            stopwatch.Stop();

            state.Title = NormalizeTabTitle(userPrompt);
            state.Persona = persona;
            state.Model = model;
            state.Temperature = temperatureText;
            state.Provider = provider.Name;
            state.ProviderId = GetSelectedProvider()?.Id ?? string.Empty;
            state.SystemPrompt = systemPrompt;
            state.LastPrompt = userPrompt;
            state.PreviousResponseId = response.ResponseId ?? string.Empty;
            state.CreatedAt = state.CreatedAt == default ? DateTime.Now : state.CreatedAt;
            state.LastUpdatedAt = DateTime.Now;
            state.LatencySeconds = stopwatch.Elapsed.TotalSeconds;
            state.IsPending = false;
            state.HasError = false;
            state.PromptHistory.Add(userPrompt);
            AddPromptHistory(userPrompt);

            if ((_imageBytes?.Length ?? 0) > 0)
                UpdateConversationImage(state, _imageBytes, _imageMimeType, _imagePath);

            UsageRecord usage = _usageMetricsService.Track(model, state.Provider, userPrompt + Environment.NewLine + systemPrompt, response.Text, stopwatch.Elapsed.TotalMilliseconds);
            state.EstimatedTokens = usage.EstimatedPromptTokens + usage.EstimatedResponseTokens;
            state.EstimatedCostUsd = usage.EstimatedCostUsd;

            await SetResponseAsync(state, response.Text);
            UpdateConversationTabText(state);
            RefreshUsageStatus();
            UpdateStatusBar();

            if (executionOptions?.OpenResultWindowOnSuccess == true)
                OpenQuickResultWindow(state);

            SetStatus($"Completed in {stopwatch.Elapsed.TotalSeconds:F1}s");
            UpdateAutoComplete();
        }
        catch (OperationCanceledException)
        {
            state.IsPending = false;
            state.HasError = true;
            await SetResponseAsync(state, "Request cancelled by user.");
            UpdateConversationTabText(state);
            SetStatus("Request cancelled.");
        }
        catch (Exception ex)
        {
            state.IsPending = false;
            state.HasError = true;
            await SetResponseAsync(state, ex.ToString());
            UpdateConversationTabText(state);
            _diagnostics.Error("Request failed: " + ex.Message);
            SetStatus("Request failed.");
            ShowAppMessage(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error, executionOptions?.PreferMainWindowForDialogs ?? true);
        }
        finally
        {
            _isBusy = false;
            UpdateUiState();
            UpdateStatusBar();
        }
    }

    private void AddPromptHistory(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            return;

        _promptHistory.Insert(0, prompt.Trim());
        int max = Math.Max(10, _workspaceSettings.MaxPromptHistory);
        while (_promptHistory.Count > max)
            _promptHistory.RemoveAt(_promptHistory.Count - 1);

        UpdateAutoComplete();
    }

    private void ReuseSelectedHistoryPrompt()
    {
        if (_lstPromptHistory.SelectedItem is not string prompt)
            return;

        _txtQuestion.Text = prompt;
        _txtQuestion.Focus();
    }

    private void CancelActiveRequest()
    {
        if (!_isBusy)
            return;

        _requestCts?.Cancel();
        SetStatus("Cancelling request...");
    }

    private ConversationTabState CreateConversationTab(string title)
    {
        // Enforce max conversation tabs by closing the oldest non-pinned tab
        int maxTabs = Math.Max(1, _workspaceSettings.MaxConversationTabs);
        while (_conversationStates.Count >= maxTabs)
        {
            ConversationTabState? oldest = _conversationStates.Values
                .Where(s => !s.IsPinned)
                .OrderBy(s => s.CreatedAt)
                .FirstOrDefault();

            if (oldest == null)
                break;

            DisposeConversation(oldest);
        }

        TabPage page = new();
        page.Padding = new Padding(0);

        Panel right = new() { Dock = DockStyle.Fill, Padding = new Padding(6) };
        Image toolStripIconSample = _iconCache.Get(TablerIcon.Copy, TablerIconCache.ToolStripIconSize);
        ToolStrip tool = new() { Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden, ImageScalingSize = toolStripIconSample.Size };
        ToolStripButton btnCopy = new("Copy") { DisplayStyle = ToolStripItemDisplayStyle.ImageAndText, Image = toolStripIconSample };
        ToolStripButton btnCopySummary = new("Summary") { DisplayStyle = ToolStripItemDisplayStyle.ImageAndText, Image = _iconCache.Get(TablerIcon.ClipboardText, TablerIconCache.ToolStripIconSize) };
        ToolStripButton btnClear = new("Clear") { DisplayStyle = ToolStripItemDisplayStyle.ImageAndText, Image = _iconCache.Get(TablerIcon.Trash, TablerIconCache.ToolStripIconSize) };
        ToolStripButton btnReRender = new("Re-Render") { DisplayStyle = ToolStripItemDisplayStyle.ImageAndText, Image = _iconCache.Get(TablerIcon.Refresh, TablerIconCache.ToolStripIconSize) };
        ToolStripButton btnExport = new("Export") { DisplayStyle = ToolStripItemDisplayStyle.ImageAndText, Image = _iconCache.Get(TablerIcon.FileExport, TablerIconCache.ToolStripIconSize) };
        ToolStripButton btnWindow = new("Window") { DisplayStyle = ToolStripItemDisplayStyle.ImageAndText, Image = _iconCache.Get(TablerIcon.WindowMaximize, TablerIconCache.ToolStripIconSize) };
        ToolStripLabel meta = new("No response") { Alignment = ToolStripItemAlignment.Right };
        tool.Items.AddRange(new ToolStripItem[] { btnCopy, btnCopySummary, btnClear, btnReRender, btnExport, btnWindow, new ToolStripSeparator(), meta });

        TabControl responseTabs = new() { Dock = DockStyle.Fill };
        TabPage formattedTab = new("Formatted");
        TabPage rawTab = new("Raw");
        WebView2 webView = new() { Dock = DockStyle.Fill, DefaultBackgroundColor = Color.White };
        float fontSize = Math.Clamp(_workspaceSettings.EditorFontSize, 8F, 24F);
        TextBox rawText = new() { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Both, WordWrap = _workspaceSettings.ResponseWordWrap, ReadOnly = true, ContextMenuStrip = _responseMenu, Font = new Font("Consolas", fontSize) };
        // Wait, does ContextMenuStrip exist? Yes, we used it above and the only error was type conversion.
        formattedTab.Controls.Add(webView);
        rawTab.Controls.Add(rawText);
        responseTabs.TabPages.Add(formattedTab);
        responseTabs.TabPages.Add(rawTab);
        right.Controls.Add(responseTabs);
        right.Controls.Add(tool);

        page.Controls.Add(right);
        _conversationTabs.TabPages.Add(page);
        
        SplitContainer split = new();
        PictureBox picture = new();
        Label imageInfo = new();

        ConversationTabState state = new()
        {
            TabPage = page,
            Split = split,
            Picture = picture,
            ImageInfoLabel = imageInfo,
            Tool = tool,
            CopyButton = btnCopy,
            CopySummaryButton = btnCopySummary,
            ClearButton = btnClear,
            ReRenderButton = btnReRender,
            ExportButton = btnExport,
            WindowButton = btnWindow,
            MetaLabel = meta,
            ResponseTabs = responseTabs,
            WebView = webView,
            RawTextBox = rawText,
            Title = NormalizeTabTitle(title),
            Persona = _cmbPersona.Text,
            Model = _cmbModel.Text,
            Temperature = GetSelectedTemperatureText(),
            Provider = _cmbProvider.Text.Trim(),
            ProviderId = GetSelectedProvider()?.Id ?? string.Empty,
            SystemPrompt = _txtSystemPrompt.Text.Trim(),
            LastPrompt = _txtQuestion.Text.Trim(),
            CreatedAt = DateTime.Now,
            LastUpdatedAt = DateTime.Now
        };

        btnCopy.Click += (_, __) => CopyFullResponse(state);
        btnCopySummary.Click += (_, __) => CopySummary(state);
        btnClear.Click += (_, __) => ClearResponse(state);
        btnReRender.Click += async (_, __) => await RenderResponseAsync(state, state.RawResponseText);
        btnExport.Click += (_, __) => ExportConversation(state);
        btnWindow.Click += (_, __) => OpenConversationWindow(state);

        _conversationStates[page] = state;
        _conversationTabs.SelectedTab = page;
        UpdateConversationImage(state, _imageBytes, _imageMimeType, _imagePath);
        ClearResponse(state);
        UpdateConversationTabText(state);
        return state;
    }

    private void UpdateConversationTabText(ConversationTabState state)
    {
        string title = NormalizeTabTitle(state.Title);
        state.TabPage.Text = "    " + title;
    }

    private string NormalizeTabTitle(string? title)
    {
        string value = string.IsNullOrWhiteSpace(title) ? "Conversation" : title.Trim();
        value = Regex.Replace(value, "\\s+", " ");
        return value.Length > 28 ? value.Substring(0, 28) + "‥" : value;
    }

    private ConversationTabState? GetActiveConversation()
    {
        if (_conversationTabs.SelectedTab == null)
            return null;

        return _conversationStates.TryGetValue(_conversationTabs.SelectedTab, out ConversationTabState? state) ? state : null;
    }

    private async Task EnsureResponseViewerAsync(ConversationTabState state)
    {
        if (state.ResponseViewInitialized || state.ResponseViewInitializationAttempted)
            return;

        state.ResponseViewInitializationAttempted = true;

        try
        {
            await state.WebView.EnsureCoreWebView2Async();
            if (state.WebView.CoreWebView2 != null)
            {
                state.WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                state.WebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                state.WebView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = true;
                state.WebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                state.WebView.CoreWebView2.Settings.IsWebMessageEnabled = true;
                state.WebView.CoreWebView2.WebMessageReceived += async (_, args) => await HandleResponseWebMessageAsync(state, args);
            }
            state.ResponseViewInitialized = true;
            state.WebView.NavigateToString(BuildEmptyResponseHtml());
        }
        catch (Exception ex)
        {
            state.ResponseViewInitialized = false;
            state.RawTextBox.Text = "WebView2 initialization failed." + Environment.NewLine + Environment.NewLine + ex;
            _diagnostics.Warn("WebView2 initialization failed. Raw response tab remains available.");
        }
    }

    private async Task SetResponseAsync(ConversationTabState state, string response)
    {
        state.RawResponseText = response ?? string.Empty;
        state.LastAnalysis = AnalyzeResponse(state.RawResponseText);
        UpdateResponseMeta(state);
        UpdateUiState();
        await RenderResponseAsync(state, state.RawResponseText);
    }

    private async Task RenderResponseAsync(ConversationTabState state, string rawResponse)
    {
        state.RawTextBox.Text = rawResponse ?? string.Empty;
        if (!state.ResponseViewInitialized)
        {
            await EnsureResponseViewerAsync(state);
            if (!state.ResponseViewInitialized)
                return;
        }

        ResponseAnalysis analysis = state.LastAnalysis ?? AnalyzeResponse(rawResponse ?? string.Empty);
        string html = BuildResponseHtml(rawResponse ?? string.Empty, analysis, state.ResponseViewerStateJson);
        state.WebView.NavigateToString(html);
    }

    private void UpdateResponseMeta(ConversationTabState state)
    {
        if (string.IsNullOrWhiteSpace(state.RawResponseText))
        {
            state.MetaLabel.Text = "No response";
            return;
        }

        int lineCount = state.RawResponseText.Replace("\r\n", "\n").Split('\n').Length;
        int charCount = state.RawResponseText.Length;
        int high = state.LastAnalysis?.HighConfidenceItems.Count ?? 0;
        int low = state.LastAnalysis?.LowConfidenceItems.Count ?? 0;
        state.MetaLabel.Text = $"{lineCount} lines | {charCount} chars | high {high} | low {low}";
    }

    private void CopyFullResponse()
    {
        ConversationTabState? state = GetActiveConversation();
        if (state != null)
            CopyFullResponse(state);
    }

    private void CopyFullResponse(ConversationTabState state)
    {
        if (string.IsNullOrWhiteSpace(state.RawResponseText))
            return;

        Clipboard.SetText(state.RawResponseText);
        SetStatus("Response copied.");
    }

    private void CopySummary()
    {
        ConversationTabState? state = GetActiveConversation();
        if (state != null)
            CopySummary(state);
    }

    private void CopySummary(ConversationTabState state)
    {
        string summary = state.LastAnalysis?.Summary?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(summary))
            return;

        Clipboard.SetText(summary);
        SetStatus("Summary copied.");
    }

    private void CopyFirstCodeBlock()
    {
        ConversationTabState? state = GetActiveConversation();
        if (state == null)
            return;

        Match match = CodeBlockFenceRegex.Match(state.RawResponseText ?? string.Empty);
        if (!match.Success)
        {
            MessageBox.Show(this, "No fenced code block was found in the active response.", "Copy Code Block", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Clipboard.SetText(match.Groups["code"].Value.Trim());
        SetStatus("Code block copied.");
    }


    private async Task HandleResponseWebMessageAsync(ConversationTabState state, CoreWebView2WebMessageReceivedEventArgs args)
    {
        string requestId = string.Empty;

        try
        {
            string message = args.TryGetWebMessageAsString();
            if (string.IsNullOrWhiteSpace(message))
                return;

            using JsonDocument document = JsonDocument.Parse(message);
            JsonElement root = document.RootElement;

            string type = root.TryGetProperty("type", out JsonElement typeElement)
                ? typeElement.GetString() ?? string.Empty
                : string.Empty;

            requestId = root.TryGetProperty("requestId", out JsonElement requestIdElement)
                ? requestIdElement.GetString() ?? string.Empty
                : string.Empty;

            if (string.Equals(type, "persist-viewer-state", StringComparison.Ordinal))
            {
                if (root.TryGetProperty("state", out JsonElement stateElement))
                    state.ResponseViewerStateJson = stateElement.GetRawText();

                return;
            }

            if (string.Equals(type, "launch-url", StringComparison.Ordinal))
            {
                string url = root.TryGetProperty("url", out JsonElement urlElement)
                    ? urlElement.GetString() ?? string.Empty
                    : string.Empty;

                if (TryLaunchExternalUrl(url))
                    SetStatus("Link opened.");

                return;
            }

            bool success = false;
            string statusMessage = "Copy failed.";

            switch (type)
            {
                case "copy-text":
                    string text = root.TryGetProperty("text", out JsonElement textElement)
                        ? textElement.GetString() ?? string.Empty
                        : string.Empty;

                    success = TryCopyTextToClipboard(text);
                    statusMessage = success ? "Block copied." : "Copy failed.";
                    break;

                case "copy-rich":
                    string richText = root.TryGetProperty("text", out JsonElement richTextElement)
                        ? richTextElement.GetString() ?? string.Empty
                        : string.Empty;

                    string htmlFragment = root.TryGetProperty("html", out JsonElement htmlElement)
                        ? htmlElement.GetString() ?? string.Empty
                        : string.Empty;

                    success = TryCopyRichHtmlToClipboard(richText, htmlFragment);
                    statusMessage = success ? "Block copied." : "Copy failed.";
                    break;

                case "copy-image":
                    string dataUrl = root.TryGetProperty("dataUrl", out JsonElement dataUrlElement)
                        ? dataUrlElement.GetString() ?? string.Empty
                        : string.Empty;

                    success = TryCopyPngDataUrlToClipboard(dataUrl);
                    if (!success)
                        success = await TryCopyWebViewClipToClipboardAsync(state, root);

                    statusMessage = success ? "Image copied." : "Copy failed.";
                    break;

                case "share-email":
                    string emailText = root.TryGetProperty("text", out JsonElement emailTextElement)
                        ? emailTextElement.GetString() ?? string.Empty
                        : string.Empty;
                    success = TryShareToEmail(emailText);
                    statusMessage = success ? "Drafting email..." : "Email share failed.";
                    break;

                case "share-teams":
                    string teamsText = root.TryGetProperty("text", out JsonElement teamsTextElement)
                        ? teamsTextElement.GetString() ?? string.Empty
                        : string.Empty;
                    success = TryShareToTeams(teamsText);
                    statusMessage = success ? "Drafting Teams message..." : "Teams share failed.";
                    break;

                default:
                    return;
            }

            if (!string.IsNullOrWhiteSpace(requestId))
                PostCopyResult(state, requestId, success, statusMessage);

            if (success)
                SetStatus(statusMessage);
        }
        catch (Exception ex)
        {
            _diagnostics.Warn("WebView clipboard bridge failed: " + ex.Message);

            if (!string.IsNullOrWhiteSpace(requestId))
                PostCopyResult(state, requestId, false, ex.Message);
        }
    }

    private static void PostCopyResult(ConversationTabState state, string requestId, bool success, string message)
    {
        if (state.WebView.CoreWebView2 == null)
            return;

        string payload = JsonSerializer.Serialize(new
        {
            type = "copy-result",
            requestId,
            success,
            message
        });

        state.WebView.CoreWebView2.PostWebMessageAsString(payload);
    }

    private static bool TryCopyTextToClipboard(string text)
        => ClipboardHelper.TryCopyTextToClipboard(text);

    private static bool TryCopyRichHtmlToClipboard(string plainText, string htmlFragment)
        => ClipboardHelper.TryCopyRichHtmlToClipboard(plainText, htmlFragment);

    private async Task<bool> TryCopyWebViewClipToClipboardAsync(ConversationTabState state, JsonElement root)
    {
        try
        {
            if (!root.TryGetProperty("bounds", out JsonElement boundsElement) || boundsElement.ValueKind != JsonValueKind.Object)
                return false;

            double x = ReadJsonDouble(boundsElement, "x");
            double y = ReadJsonDouble(boundsElement, "y");
            double width = ReadJsonDouble(boundsElement, "width");
            double height = ReadJsonDouble(boundsElement, "height");
            double scale = ReadJsonDouble(boundsElement, "scale", 1d);

            if (width <= 0 || height <= 0)
                return false;

            object parameters = new
            {
                format = "png",
                captureBeyondViewport = true,
                clip = new
                {
                    x = Math.Max(0d, x),
                    y = Math.Max(0d, y),
                    width = Math.Max(1d, width),
                    height = Math.Max(1d, height),
                    scale = Math.Clamp(scale, 1d, 4d)
                }
            };

            string protocolResult = await state.WebView.CoreWebView2.CallDevToolsProtocolMethodAsync(
                "Page.captureScreenshot",
                JsonSerializer.Serialize(parameters));

            using JsonDocument resultDocument = JsonDocument.Parse(protocolResult);
            string base64 = resultDocument.RootElement.TryGetProperty("data", out JsonElement dataElement)
                ? dataElement.GetString() ?? string.Empty
                : string.Empty;

            return TryCopyPngBase64ToClipboard(base64);
        }
        catch
        {
            return false;
        }
    }

    private static double ReadJsonDouble(JsonElement element, string propertyName, double defaultValue = 0d)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property))
            return defaultValue;

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetDouble(out double number) => number,
            JsonValueKind.String when double.TryParse(property.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) => parsed,
            _ => defaultValue
        };
    }

    private static bool TryCopyPngDataUrlToClipboard(string? dataUrl)
        => ClipboardHelper.TryCopyPngDataUrlToClipboard(dataUrl);

    private static bool TryCopyPngBase64ToClipboard(string? base64)
        => ClipboardHelper.TryCopyPngBase64ToClipboard(base64);

    private async Task<bool> TryCopyClipboardImageToPngAsync(ConversationTabState state)
    {
        try
        {
            if (!Clipboard.ContainsImage())
                return false;

            using Image image = Clipboard.GetImage();
            using Bitmap bitmap = new(image);
            SetClipboardDataObject(bitmap);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryLaunchExternalUrl(string? url)
        => UrlHelper.TryLaunchExternalUrl(url);

    private static void SetClipboardDataObject(object data)
        => ClipboardHelper.SetClipboardDataObject(data);

    private bool TryShareToEmail(string text)
    {
        try
        {
            string escaped = Uri.EscapeDataString(text);
            string url = $"mailto:?body={escaped}";
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            return true;
        }
        catch (Exception ex)
        {
            _diagnostics.Warn("TryShareToEmail failed: " + ex.Message);
            return false;
        }
    }

    private bool TryShareToTeams(string text)
    {
        try
        {
            string escaped = Uri.EscapeDataString(text);
            string url = $"msteams://teams.microsoft.com/l/chat/0/0?users=&message={escaped}";
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            return true;
        }
        catch (Exception ex)
        {
            _diagnostics.Warn("TryShareToTeams failed: " + ex.Message);
            return false;
        }
    }

    private void ClearResponse()
    {
        ConversationTabState? state = GetActiveConversation();
        if (state != null)
            ClearResponse(state);
    }

    private void ClearResponse(ConversationTabState state)
    {
        state.RawResponseText = string.Empty;
        state.LastAnalysis = null;
        state.HasError = false;
        state.IsPending = false;
        state.RawTextBox.Clear();
        state.MetaLabel.Text = "No response";
        if (state.ResponseViewInitialized)
            state.WebView.NavigateToString(BuildEmptyResponseHtml());
        UpdateConversationTabText(state);
        UpdateUiState();
    }

    private void UpdateConversationImage(ConversationTabState state, byte[]? imageBytes, string? imageMimeType, string? imagePath)
    {
        state.ImageBytes = imageBytes?.ToArray();
        state.ImageMimeType = imageMimeType;
        state.ImageName = string.IsNullOrWhiteSpace(imagePath) ? "(captured image)" : Path.GetFileName(imagePath);

        Image? old = state.Picture.Image;
        state.Picture.Image = null;
        old?.Dispose();

        if (imageBytes == null || imageBytes.Length == 0)
        {
            state.ImageInfoLabel.Text = "No image";
            return;
        }

        using MemoryStream ms = new(imageBytes);
        using Image temp = Image.FromStream(ms);
        state.Picture.Image = new Bitmap(temp);
        double kb = imageBytes.Length / 1024d;
        state.ImageInfoLabel.Text = "Image: " + state.ImageName + " | MIME: " + (imageMimeType ?? "unknown") + " | Size: " + kb.ToString("F1") + " KB";
    }

    private void SyncComposerWithActiveConversation()
    {
        ConversationTabState? state = GetActiveConversation();
        if (state == null)
            return;

        if (!string.IsNullOrWhiteSpace(state.Persona))
            SelectPersona(state.Persona);
            
        if (!string.IsNullOrWhiteSpace(state.ProviderId) || !string.IsNullOrWhiteSpace(state.Provider) || !string.IsNullOrWhiteSpace(state.Model))
        {
            string providerToSelect = !string.IsNullOrWhiteSpace(state.ProviderId) ? state.ProviderId : state.Provider;
            TrySelectProviderForModel(state.Model, providerToSelect, allowMissingModel: true);
        }

        SelectTemperature(state.Temperature);

        if (!string.IsNullOrWhiteSpace(state.SystemPrompt))
            _txtSystemPrompt.Text = state.SystemPrompt;
    }

    private void EnsureActiveConversationRendered()
    {
        ConversationTabState? state = GetActiveConversation();
        if (state == null || string.IsNullOrWhiteSpace(state.RawResponseText))
            return;

        _ = RenderResponseAsync(state, state.RawResponseText);
    }

    private void ToggleDockLock(bool locked)
    {
        _workspaceSettings.LockDockedWindows = locked;
        _dockHost.IsLocked = locked;
        if (_mnuLockDockedWindows.Checked != locked)
            _mnuLockDockedWindows.Checked = locked;
        SetStatus(locked ? "Window layout locked." : "Window layout unlocked.");
    }

    private void UpdateUiState()
    {
        bool hasPrompt = !string.IsNullOrWhiteSpace(_txtQuestion.Text);
        bool hasSystemPrompt = !string.IsNullOrWhiteSpace(_txtSystemPrompt.Text);
        bool hasImage = _imageBytes != null && _imageBytes.Length > 0;
        bool hasProvider = GetSelectedProvider() != null;
        bool hasModel = !string.IsNullOrWhiteSpace(_cmbModel.Text);
        ConversationTabState? active = GetActiveConversation();
        bool hasResponse = !string.IsNullOrWhiteSpace(active?.RawResponseText);

        _cmbProfile.Enabled = !_isBusy;
        _cmbProvider.Enabled = !_isBusy;
        _cmbModel.Enabled = !_isBusy;
        _cmbPersona.Enabled = !_isBusy;
        _cmbPrefilledQuestion.Enabled = !_isBusy;
        _txtSystemPrompt.Enabled = !_isBusy;
        _txtQuestion.Enabled = !_isBusy;
        _btnBrowseImage.Enabled = !_isBusy && !_isSnipInProgress;
        _btnSnipScreen.Enabled = !_isBusy && !_isSnipInProgress;
        _btnClearImage.Enabled = !_isBusy && hasImage;
        _chkSnipAuto.Enabled = !_isBusy && !_isSnipInProgress;
        _chkSnipShortcut.Enabled = !_isBusy && !_isSnipInProgress;
        _numSnipDelay.Enabled = !_isBusy && !_isSnipInProgress;
        bool canSubmitWithCurrentImageState = !hasImage || SelectedModelSupportsImages();

        _btnAsk.Enabled = !_isBusy && hasProvider && hasModel && hasPrompt && hasSystemPrompt && canSubmitWithCurrentImageState;
        _btnFollowUp.Enabled = !_isBusy && hasProvider && hasModel && hasPrompt && hasSystemPrompt && canSubmitWithCurrentImageState && active != null && hasResponse;
        _btnCancel.Enabled = _isBusy;
        _btnAnalyzeClipboard.Enabled = _clipboardSuggestionAvailable;

        if (active != null)
        {
            active.CopyButton.Enabled = hasResponse;
            active.CopySummaryButton.Enabled = hasResponse && !string.IsNullOrWhiteSpace(active.LastAnalysis?.Summary);
            active.ClearButton.Enabled = hasResponse;
            active.ReRenderButton.Enabled = hasResponse;
            active.ExportButton.Enabled = hasResponse;
            active.WindowButton.Enabled = hasResponse;
        }

        UpdateTemperatureControlState();
    }

    private void UpdateImageInfo()
    {
        bool hasSelection = GetSelectedProvider() != null && !string.IsNullOrWhiteSpace(_cmbModel.Text);
        bool supportsImages = !hasSelection || SelectedModelSupportsImages();

        if (_imageBytes == null || string.IsNullOrWhiteSpace(_imageMimeType))
        {
            _lblImageInfo.Text = supportsImages
                ? "No image selected."
                : "Image input is not supported by the selected model.";
            return;
        }

        string fileName = string.IsNullOrWhiteSpace(_imagePath) ? "(captured image)" : Path.GetFileName(_imagePath);
        double kb = _imageBytes.Length / 1024d;
        _lblImageInfo.Text = "Image: " + fileName + " | MIME: " + _imageMimeType + " | Size: " + kb.ToString("F1") + " KB";

        if (!supportsImages)
            _lblImageInfo.Text += " | Selected model does not support image input.";
    }

    private void SetStatus(string text)
    {
        _lastStatusText = text;
        UpdateStatusBar();
    }

    private void UpdateStatusBar()
    {
        ConversationTabState? active = GetActiveConversation();
        string resolvedProvider = !string.IsNullOrWhiteSpace(active?.Provider)
            ? active.Provider
            : !string.IsNullOrWhiteSpace(_cmbProvider.Text)
                ? _cmbProvider.Text.Trim()
                : InferProvider(_cmbModel.Text);

        _statusState.Text = _lastStatusText;
        _statusModel.Text = "Model: " + (_cmbModel.Text.Trim().Length == 0 ? "n/a" : _cmbModel.Text.Trim());
        _statusPersona.Text = "Persona: " + (_cmbPersona.Text.Trim().Length == 0 ? "n/a" : _cmbPersona.Text.Trim());
        _statusProfile.Text = "Profile: " + (_cmbProfile.Text.Trim().Length == 0 ? "n/a" : _cmbProfile.Text.Trim());
        _statusProvider.Text = "Provider: " + (string.IsNullOrWhiteSpace(resolvedProvider) ? "n/a" : resolvedProvider);
        _statusTokens.Text = "Tokens: " + ((active?.EstimatedTokens ?? 0).ToString("N0"));
        _statusCost.Text = "Cost: $" + (active?.EstimatedCostUsd ?? 0m).ToString("F4");
        _statusLatency.Text = "Latency: " + ((active?.LatencySeconds ?? 0d).ToString("F1")) + "s";
        _statusConversations.Text = "Tabs: " + _conversationStates.Count;
    }

    private void RefreshUsageStatus()
    {
        UsageMetricsService.UsageSummary summary = _usageMetricsService.GetSummary();
        _diagnostics.Info($"Usage summary refreshed. Today requests: {summary.Today.Requests}, estimated cost: ${summary.Today.EstimatedCostUsd:F4}.");
    }

    private void RefreshSuggestions()
    {
        _lstSuggestions.BeginUpdate();
        _lstSuggestions.Items.Clear();
        foreach (string suggestion in GetSmartSuggestions(_txtQuestion.Text.Trim()))
            _lstSuggestions.Items.Add(suggestion);
        _lstSuggestions.EndUpdate();
    }

    private IEnumerable<string> GetSmartSuggestions(string prompt)
        => _promptAssistanceHelper.GetSmartSuggestions(prompt);

    private void ApplySelectedSuggestion()
    {
        if (_lstSuggestions.SelectedItem is not string suggestion)
            return;

        _txtQuestion.Text = suggestion;
        _txtQuestion.Focus();
    }

    private void InsertSelectedTemplate()
    {
        if (_lstTemplates.SelectedItem is not TemplateListEntry entry)
            return;

        InsertTextIntoPrompt(entry.Item.Text);
    }

    private void ApplySelectedTemplate()
    {
        if (_lstTemplates.SelectedItem is not TemplateListEntry entry)
            return;

        _txtQuestion.Text = entry.Item.Text;
        _txtQuestion.Focus();
    }

    private void InsertSelectedSnippet()
    {
        if (_lstSnippets.SelectedItem is not SnippetListEntry entry)
            return;

        InsertTextIntoPrompt(entry.Item.Text);
    }

    private void InsertTextIntoPrompt(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        if (_txtQuestion.TextLength > 0 && !_txtQuestion.Text.EndsWith(Environment.NewLine))
            _txtQuestion.AppendText(Environment.NewLine + Environment.NewLine);

        _txtQuestion.AppendText(text);
        _txtQuestion.Focus();
    }

    private void SearchAcrossConversations()
    {
    }

    private void OpenSelectedSearchResult()
    {
    }

    private void FindInActiveConversation()
    {
    }

    private void FindInActiveConversation(string? query)
    {
    }

    private void PrepareResponseTransformation(string instruction)
    {
        ConversationTabState? state = GetActiveConversation();
        if (state == null || string.IsNullOrWhiteSpace(state.RawResponseText))
            return;

        _txtQuestion.Text = instruction + Environment.NewLine + Environment.NewLine + state.RawResponseText;
        _txtQuestion.Focus();
        SetStatus("Transformation prompt prepared.");
    }

    private void PollClipboard()
    {
        if (!_workspaceSettings.ClipboardSuggestionsEnabled || !Visible)
            return;

        try
        {
            if (!Clipboard.ContainsText())
            {
                _clipboardSuggestionAvailable = false;
                return;
            }

            string text = Clipboard.GetText();
            string signature = text.Length > 300 ? text.Substring(0, 300) : text;
            if (signature == _lastClipboardSignature)
                return;

            _lastClipboardSignature = signature;
            _clipboardSuggestionAvailable = LooksLikeDiagnosticText(text);
            if (_clipboardSuggestionAvailable)
            {
                SetStatus("Clipboard contains diagnostic text. Use Analyze Clipboard.");
                _diagnostics.Info("Clipboard suggestion available.");
            }
            UpdateUiState();
        }
        catch
        {
        }
    }

    private static bool LooksLikeDiagnosticText(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length < 8)
            return false;

        string lower = text.ToLowerInvariant();
        return lower.Contains("error") || lower.Contains("exception") || lower.Contains("stack") || lower.Contains("failed") || lower.Contains("timeout");
    }

    private void AnalyzeClipboardIntoPrompt()
    {
        try
        {
            if (!Clipboard.ContainsText())
            {
                MessageBox.Show(this, "Clipboard does not currently contain text.", "Analyze Clipboard", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string text = Clipboard.GetText();
            _txtQuestion.Text = "Analyze the following clipboard content. Explain the likely issue, confidence level, and the next best steps." + Environment.NewLine + Environment.NewLine + text;
            _txtQuestion.Focus();
            _toolTabs.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Clipboard", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void PasteImageFromClipboard()
    {
        try
        {
            if (!SelectedModelSupportsImages())
            {
                MessageBox.Show(this, "The selected model does not support image input.", "Image Input", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!Clipboard.ContainsImage())
                return;

            using Image image = Clipboard.GetImage();
            using Bitmap bitmap = new(image);
            ApplyImageFromBitmap(bitmap, null, "image/png");
        }
        catch
        {
        }
    }

    private void TogglePromptExpand()
    {
        if (_isPromptExpanded)
            CollapsePrompt();
        else
            ExpandPrompt();
    }

    private void LayoutPromptToolButtons(Control host)
    {
        const int spacing = 2;
        int y = host.ClientSize.Height - _btnPromptCopy.Height - 4;
        int x = 4;

        // Left-to-right at bottom-left: Copy, Cut, Paste, Expand
        _btnPromptCopy.Location = new Point(x, y);
        x += _btnPromptCopy.Width + spacing;

        _btnPromptCut.Location = new Point(x, y);
        x += _btnPromptCut.Width + spacing;

        _btnPromptPaste.Location = new Point(x, y);
        x += _btnPromptPaste.Width + spacing;

        _btnExpandPrompt.Location = new Point(x, y);
    }

    private void PromptCopy()
    {
        string selected = _txtQuestion.SelectedText;
        if (!string.IsNullOrEmpty(selected))
        {
            Clipboard.SetText(selected);
            SetStatus("Copied selected text.");
        }
        else if (!string.IsNullOrWhiteSpace(_txtQuestion.Text))
        {
            Clipboard.SetText(_txtQuestion.Text);
            SetStatus("Copied all prompt text.");
        }
    }

    private void PromptCut()
    {
        if (!string.IsNullOrEmpty(_txtQuestion.SelectedText))
        {
            Clipboard.SetText(_txtQuestion.SelectedText);
            int start = _txtQuestion.SelectionStart;
            _txtQuestion.Text = _txtQuestion.Text.Remove(start, _txtQuestion.SelectionLength);
            _txtQuestion.SelectionStart = start;
            SetStatus("Cut selected text.");
        }
        else if (!string.IsNullOrWhiteSpace(_txtQuestion.Text))
        {
            Clipboard.SetText(_txtQuestion.Text);
            _txtQuestion.Clear();
            SetStatus("Cut all prompt text.");
        }
    }

    private void PromptPaste()
    {
        if (Clipboard.ContainsText())
        {
            string clip = Clipboard.GetText();
            int start = _txtQuestion.SelectionStart;
            int length = _txtQuestion.SelectionLength;
            string current = _txtQuestion.Text;
            _txtQuestion.Text = current.Remove(start, length).Insert(start, clip);
            _txtQuestion.SelectionStart = start + clip.Length;
            _txtQuestion.Focus();
            SetStatus("Pasted from clipboard.");
        }
    }

    private void AddPromptToolButtonsTo(Control host)
    {
        host.Controls.Add(_btnPromptCopy);
        host.Controls.Add(_btnPromptCut);
        host.Controls.Add(_btnPromptPaste);
        host.Controls.Add(_btnExpandPrompt);
        _btnPromptCopy.BringToFront();
        _btnPromptCut.BringToFront();
        _btnPromptPaste.BringToFront();
        _btnExpandPrompt.BringToFront();
    }

    private void RemovePromptToolButtonsFrom(Control? host)
    {
        host?.Controls.Remove(_btnPromptCopy);
        host?.Controls.Remove(_btnPromptCut);
        host?.Controls.Remove(_btnPromptPaste);
        host?.Controls.Remove(_btnExpandPrompt);
    }

    private void ExpandPrompt()
    {
        if (_isPromptExpanded || _promptSplit == null)
            return;

        _isPromptExpanded = true;

        // Remove _txtQuestion and buttons from the current host inside promptSplit.Panel2
        Control? questionHost = _txtQuestion.Parent;
        questionHost?.Controls.Remove(_txtQuestion);
        RemovePromptToolButtonsFrom(questionHost);

        // Create the popup window sized to half the main window
        int popupWidth = Math.Max(480, Width / 2);
        int popupHeight = Math.Max(360, Height / 2);

        bool allowClose = false;
        _promptExpandWindow = new Form
        {
            Text = "Prompt Editor",
            StartPosition = FormStartPosition.CenterScreen,
            Size = new Size(popupWidth, popupHeight),
            MinimumSize = new Size(400, 300),
            ShowIcon = false,
            ShowInTaskbar = false,
            FormBorderStyle = FormBorderStyle.Sizable,
            KeyPreview = true,
            BackColor = _activeTheme.Background
        };

        Form window = _promptExpandWindow;

        // Prevent the X button from closing the app — just collapse back
        window.FormClosing += (_, args) =>
        {
            if (!allowClose)
            {
                args.Cancel = true;
                CollapsePrompt();
            }
        };

        // Escape key collapses
        window.KeyDown += (_, args) =>
        {
            if (args.KeyCode == Keys.Escape)
                CollapsePrompt();
        };

        // Place the textbox and buttons inside the popup
        Panel innerHost = new() { Dock = DockStyle.Fill, Padding = new Padding(8) };
        _txtQuestion.Dock = DockStyle.Fill;
        AddPromptToolButtonsTo(innerHost);
        innerHost.Controls.Add(_txtQuestion);

        innerHost.Layout += (_, __) =>
        {
            if (_isPromptExpanded)
                LayoutPromptToolButtons(innerHost);
        };

        window.Controls.Add(innerHost);

        // Store a way to force-close the window without triggering collapse
        window.Tag = new Action(() => { allowClose = true; });

        // Update button icon to collapse
        _btnExpandPrompt.Image = _iconCache.Get(TablerIcon.ArrowsMinimize, 16);
        _toolTip.SetToolTip(_btnExpandPrompt, "Collapse prompt editor");

        window.Show(this);
        _txtQuestion.Focus();
        SetStatus("Prompt editor expanded.");
    }

    private void CollapsePrompt()
    {
        if (!_isPromptExpanded || _promptSplit == null)
            return;

        _isPromptExpanded = false;

        // Remove controls from the popup window and close it
        if (_promptExpandWindow != null)
        {
            Form window = _promptExpandWindow;
            _promptExpandWindow = null;

            Panel? innerHost = window.Controls.Count > 0 ? window.Controls[0] as Panel : null;
            innerHost?.Controls.Remove(_txtQuestion);
            RemovePromptToolButtonsFrom(innerHost);

            // Set the allowClose flag via the stored action so FormClosing doesn't re-enter
            if (window.Tag is Action enableClose)
                enableClose();

            try { window.Close(); } catch { }
            try { window.Dispose(); } catch { }
        }

        // Find the questionHost panel in promptSplit.Panel2 or recreate placement
        Panel questionHost;
        if (_promptSplit.Panel2.Controls.Count > 0 && _promptSplit.Panel2.Controls[0] is Panel existingHost)
        {
            questionHost = existingHost;
        }
        else
        {
            questionHost = new Panel { Dock = DockStyle.Fill };
            _promptSplit.Panel2.Controls.Add(questionHost);
        }

        _txtQuestion.Dock = DockStyle.Fill;
        AddPromptToolButtonsTo(questionHost);
        questionHost.Controls.Add(_txtQuestion);

        // Reattach the layout handler for button positioning
        questionHost.Layout += (_, __) =>
        {
            if (!_isPromptExpanded)
                LayoutPromptToolButtons(questionHost);
        };

        // Update button icon to expand
        _btnExpandPrompt.Image = _iconCache.Get(TablerIcon.ArrowsMaximize, 16);
        _toolTip.SetToolTip(_btnExpandPrompt, "Expand prompt editor");

        _txtQuestion.Focus();
        SetStatus("Prompt editor collapsed.");
    }

    private void UpdateExpandPromptButtonTheme()
    {
        void ApplyToolBtnTheme(Button btn)
        {
            btn.BackColor = _activeTheme.SurfaceAlt;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
        }

        ApplyToolBtnTheme(_btnExpandPrompt);
        ApplyToolBtnTheme(_btnPromptCopy);
        ApplyToolBtnTheme(_btnPromptCut);
        ApplyToolBtnTheme(_btnPromptPaste);

        _btnExpandPrompt.Image = _iconCache.Get(
            _isPromptExpanded ? TablerIcon.ArrowsMinimize : TablerIcon.ArrowsMaximize, 16);
        _btnPromptCopy.Image = _iconCache.Get(TablerIcon.Copy, 16);
        _btnPromptCut.Image = _iconCache.Get(TablerIcon.Cut, 16);
        _btnPromptPaste.Image = _iconCache.Get(TablerIcon.Clipboard, 16);

        if (_isPromptExpanded && _promptExpandWindow != null)
            _promptExpandWindow.BackColor = _activeTheme.Background;
    }

    private void ApplyTheme(string themeName)
    {
        _workspaceSettings.Theme = themeName;
        _activeTheme = ThemeService.Resolve(themeName);
        _iconCache.UpdateTheme(_activeTheme);

        ThemeService.ApplyTheme(this, _activeTheme);
        _dockHost.ApplyTheme(_activeTheme);
        ConfigureActionButton(_btnNewConversation, "New", TablerIcon.SquareRoundedPlus, primary: false);
        ConfigureActionButton(_btnBrowseImage, "Image", TablerIcon.Photo, primary: false);
        ConfigureActionButton(_btnSnipScreen, "Snip", TablerIcon.Screenshot, primary: false);
        ConfigureActionButton(_btnClearImage, "Clear", TablerIcon.Eraser, primary: false);
        ConfigureActionButton(_btnAsk, "Ask", TablerIcon.Send, primary: true);
        ConfigureActionButton(_btnFollowUp, "Follow", TablerIcon.ArrowForwardUp, primary: true);
        ConfigureActionButton(_btnCancel, "Cancel", TablerIcon.X, primary: false);
        UpdateExpandPromptButtonTheme();
        RefreshConversationToolbarIcons();
        SynchronizeQuickAccessCombos();
        Invalidate(true);
        _diagnostics.Info("Theme applied: " + themeName + ".");
    }

    private void TogglePersonaPanel(bool visible)
    {
        _workspaceSettings.ShowPersonaPanel = visible;
        if (visible)
            _dockHost.ShowPanel(_dockPersonaExplorer, _dockPersonaExplorer.CurrentZone is DockZone.None or DockZone.Float ? DockZone.TopLeft : _dockPersonaExplorer.CurrentZone);
        else
            _dockHost.HidePanel(_dockPersonaExplorer);
        if (_mnuViewPersonaPanel.Checked != visible)
            _mnuViewPersonaPanel.Checked = visible;
    }

    private void ToggleToolsPanel(bool visible)
    {
        _workspaceSettings.ShowToolsPanel = visible;
        if (visible)
            _dockHost.ShowPanel(_dockAiTools, _dockAiTools.CurrentZone is DockZone.None or DockZone.Float ? DockZone.Right : _dockAiTools.CurrentZone);
        else
            _dockHost.HidePanel(_dockAiTools);
        if (_mnuViewToolsPanel.Checked != visible)
            _mnuViewToolsPanel.Checked = visible;
    }

    private void ToggleDiagnosticsPanel(bool visible)
    {
        _workspaceSettings.ShowDiagnosticsPanel = visible;
        if (visible)
            _dockHost.ShowPanel(_dockDiagnostics, _dockDiagnostics.CurrentZone is DockZone.None or DockZone.Float ? DockZone.Bottom : _dockDiagnostics.CurrentZone);
        else
            _dockHost.HidePanel(_dockDiagnostics);
        if (_mnuViewDiagnostics.Checked != visible)
            _mnuViewDiagnostics.Checked = visible;
    }

    private void ToggleComposerPanel(bool visible)
    {
        if (visible)
            _dockHost.ShowPanel(_dockComposer, _dockComposer.CurrentZone is DockZone.None or DockZone.Float ? DockZone.Top : _dockComposer.CurrentZone);
        else
            _dockHost.HidePanel(_dockComposer);
        if (_mnuViewComposer.Checked != visible)
            _mnuViewComposer.Checked = visible;
    }

    private void ToggleConversationsPanel(bool visible)
    {
        if (visible)
            _dockHost.ShowPanel(_dockConversations, _dockConversations.CurrentZone is DockZone.None or DockZone.Float ? DockZone.Center : _dockConversations.CurrentZone);
        else
            _dockHost.HidePanel(_dockConversations);
        if (_mnuViewConversations.Checked != visible)
            _mnuViewConversations.Checked = visible;
    }

    private void ApplyLayoutPreset(string preset)
    {
        switch (preset.ToLowerInvariant())
        {
            case "analyzer":
                TogglePersonaPanel(true);
                ToggleDiagnosticsPanel(true);
                ToggleComposerPanel(true);
                ToggleConversationsPanel(true);
                ToggleDockLock(false);
                _dockHost.SetZoneSize(DockZone.Left, Math.Min(320, Width / 4));
                _dockHost.SetZoneSize(DockZone.Bottom, 220);
                break;
            case "focus":
                TogglePersonaPanel(false);
                ToggleDiagnosticsPanel(false);
                ToggleComposerPanel(true);
                ToggleConversationsPanel(true);
                ToggleDockLock(true);
                break;
            default:
                TogglePersonaPanel(false);
                ToggleDiagnosticsPanel(false);
                ToggleComposerPanel(false);
                ToggleConversationsPanel(false);
                _dockHost.ShowPanel(_dockPersonaExplorer, DockZone.TopLeft);
                _dockHost.ShowPanel(_dockComposer, DockZone.Top);
                _dockHost.ShowPanel(_dockConversations, DockZone.Center);
                _dockHost.SetZoneSize(DockZone.TopLeft, 280);
                _dockHost.SetZoneSize(DockZone.Top, 210);
                _mnuViewPersonaPanel.Checked = true;
                _mnuViewComposer.Checked = true;
                _mnuViewConversations.Checked = true;
                _mnuViewDiagnostics.Checked = false;
                ToggleDockLock(true);
                break;
        }

        SetStatus("Layout preset applied: " + preset + ".");
    }

    private DockLayoutState BuildDockLayoutState()
    {
        DockLayoutState state = new()
        {
            LeftZoneWidth = _dockHost.GetZoneSize(DockZone.Left),
            RightZoneWidth = _dockHost.GetZoneSize(DockZone.Right),
            TopZoneHeight = _dockHost.GetZoneSize(DockZone.Top),
            TopLeftZoneWidth = _dockHost.GetZoneSize(DockZone.TopLeft),
            BottomZoneHeight = _dockHost.GetZoneSize(DockZone.Bottom)
        };

        void Add(DockablePanel panel, string id)
        {
            state.Panels.Add(new DockPanelState
            {
                PanelId = id,
                Zone = _dockHost.GetPanelZone(panel).ToString(),
                IsVisible = _dockHost.IsPanelVisible(panel)
            });
        }

        Add(_dockPersonaExplorer, "PersonaExplorer");
        Add(_dockComposer, "Composer");
        Add(_dockConversations, "Conversations");
        Add(_dockDiagnostics, "Diagnostics");

        return state;
    }

    private void RestoreDockLayout(DockLayoutState? layout)
    {
        if (layout == null)
        {
            ApplyLayoutPreset("standard");
            return;
        }

        _dockHost.SetZoneSize(DockZone.Left, layout.LeftZoneWidth);
        _dockHost.SetZoneSize(DockZone.Right, layout.RightZoneWidth);
        _dockHost.SetZoneSize(DockZone.Top, layout.TopZoneHeight);
        _dockHost.SetZoneSize(DockZone.Bottom, layout.BottomZoneHeight);

        foreach (DockPanelState panelState in layout.Panels)
        {
            DockablePanel? panel = panelState.PanelId switch
            {
                "PersonaExplorer" => _dockPersonaExplorer,
                "Composer" => _dockComposer,
                "Conversations" => _dockConversations,
                "Diagnostics" => _dockDiagnostics,
                _ => null
            };

            if (panel == null)
                continue;

            DockZone zone = Enum.TryParse<DockZone>(panelState.Zone, out DockZone parsed) ? parsed : DockZone.Right;
            if (zone == DockZone.Float)
                zone = DockZone.Right;

            if (panelState.IsVisible)
                _dockHost.ShowPanel(panel, zone);
            else
                _dockHost.HidePanel(panel);
        }

        _dockHost.SetZoneSize(DockZone.TopLeft, layout.TopLeftZoneWidth);

        SyncMenuChecksWithDockState();
    }

    private void SyncMenuChecksWithDockState()
    {
        _mnuViewPersonaPanel.Checked = _dockHost.IsPanelVisible(_dockPersonaExplorer);
        _mnuViewDiagnostics.Checked = _dockHost.IsPanelVisible(_dockDiagnostics);
        _mnuViewComposer.Checked = _dockHost.IsPanelVisible(_dockComposer);
        _mnuViewConversations.Checked = _dockHost.IsPanelVisible(_dockConversations);
        _mnuLockDockedWindows.Checked = _workspaceSettings.LockDockedWindows;
    }

    private void SaveWorkspace()
    {
        CaptureWorkspaceSettingsFromShell();
        _workspaceSettings.DockLayout = BuildDockLayoutState();
        _workspaceSettingsService.Save(_workspaceSettings);
        SetStatus("Workspace saved.");
    }

    private void RestoreWorkspace()
    {
        WorkspaceSettings settings = _workspaceSettingsService.Load();
        _workspaceSettings = settings;

        RestoreDockLayout(settings.DockLayout);
        ToggleDockLock(settings.LockDockedWindows);

        _chkSnipAuto.Checked = settings.AutoAskAfterSnipEnabled;
        _suppressSnipShortcutToggle = true;
        try
        {
            _chkSnipShortcut.Checked = settings.GlobalShortcutSnipAskEnabled;
        }
        finally
        {
            _suppressSnipShortcutToggle = false;
        }

        _cmbProfile.SelectedItem = settings.ActiveProfile;
        SynchronizeQuickAccessCombos();
        SetStatus("Workspace restored.");
        _dockHost.PerformLayout();
    }

    private void SaveSessionNow()
    {
        try
        {
            _sessionStateService.Save(BuildSessionSnapshot());
            SetStatus("Session saved.");
            _diagnostics.Info("Session saved to disk.");
        }
        catch (Exception ex)
        {
            _diagnostics.Error("Session save failed: " + ex.Message);
            MessageBox.Show(this, ex.Message, "Session Save", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private AppSessionState BuildSessionSnapshot()
    {
        AppSessionState session = new()
        {
            ActiveProfile = _cmbProfile.Text.Trim(),
            ActivePersona = _cmbPersona.Text.Trim(),
            ActiveProvider = _cmbProvider.Text.Trim(),
            ActiveProviderId = GetSelectedProvider()?.Id ?? string.Empty,
            ActiveModel = _cmbModel.Text.Trim(),
            ActiveTemperature = GetSelectedTemperatureText(),
            CurrentSystemPrompt = _txtSystemPrompt.Text,
            CurrentPrompt = _txtQuestion.Text,
            CurrentImageMimeType = _imageMimeType,
            CurrentImagePath = _imagePath,
            CurrentImageBase64 = _imageBytes != null && _imageBytes.Length > 0 ? Convert.ToBase64String(_imageBytes) : null,
            SelectedConversationIndex = Math.Max(0, _conversationTabs.SelectedIndex),
            IsWindowMaximized = WindowState == FormWindowState.Maximized,
            PromptHistory = _promptHistory.ToList()
        };

        foreach (TabPage page in _conversationTabs.TabPages.OfType<TabPage>())
        {
            if (!_conversationStates.TryGetValue(page, out ConversationTabState? state))
                continue;

            session.Conversations.Add(new ConversationSessionState
            {
                Title = state.Title,
                Persona = state.Persona,
                Model = state.Model,
                Temperature = state.Temperature,
                Provider = state.Provider,
                ProviderId = state.ProviderId,
                SystemPrompt = state.SystemPrompt,
                LastPrompt = state.LastPrompt,
                RawResponseText = state.RawResponseText,
                PreviousResponseId = state.PreviousResponseId,
                ImageMimeType = state.ImageMimeType,
                ImageName = state.ImageName,
                ImageBase64 = state.ImageBytes != null && state.ImageBytes.Length > 0 ? Convert.ToBase64String(state.ImageBytes) : null,
                CreatedAt = state.CreatedAt,
                LastUpdatedAt = state.LastUpdatedAt,
                LatencySeconds = state.LatencySeconds,
                EstimatedTokens = state.EstimatedTokens,
                EstimatedCostUsd = state.EstimatedCostUsd,
                IsPinned = state.IsPinned,
                HasError = state.HasError,
                IsPending = state.IsPending,
                PromptHistory = state.PromptHistory.ToList()
            });
        }

        return session;
    }

    private void RestoreSessionFromDisk()
    {
        AppSessionState session = _sessionStateService.Load();

        if (!string.IsNullOrWhiteSpace(session.ActiveProfile))
            _cmbProfile.SelectedItem = session.ActiveProfile;
        if (!string.IsNullOrWhiteSpace(session.ActivePersona))
            SelectPersona(session.ActivePersona);

        string restoredProvider = !string.IsNullOrWhiteSpace(session.ActiveProviderId) ? session.ActiveProviderId : session.ActiveProvider;
        if (!TrySelectProviderForModel(session.ActiveModel, restoredProvider, allowMissingModel: true))
            LoadProviders(restoredProvider, session.ActiveModel);

        SelectTemperature(session.ActiveTemperature);
        _txtSystemPrompt.Text = session.CurrentSystemPrompt ?? string.Empty;
        _txtQuestion.Text = session.CurrentPrompt ?? string.Empty;

        _promptHistory.Clear();
        _promptHistory.AddRange(session.PromptHistory ?? new List<string>());
        RebuildPromptHistoryList();
        UpdateAutoComplete();

        CloseAllConversations();
        foreach (ConversationSessionState item in session.Conversations ?? new List<ConversationSessionState>())
            RestoreConversationSession(item);

        if (_conversationTabs.TabPages.Count > 0)
        {
            int targetIndex = Math.Max(0, Math.Min(_conversationTabs.TabPages.Count - 1, session.SelectedConversationIndex));
            _conversationTabs.SelectedIndex = targetIndex;
        }

        WindowState = session.IsWindowMaximized ? FormWindowState.Maximized : FormWindowState.Normal;

        RestoreComposerImageFromSession(session);
        UpdateStatusBar();
        UpdateUiState();
    }

    private void RestoreConversationSession(ConversationSessionState item)
    {
        ConversationTabState state = CreateConversationTab(item.Title + " Copy");
        state.Title = string.IsNullOrWhiteSpace(item.Title) ? "Conversation" : item.Title;
        state.Persona = item.Persona ?? string.Empty;
        state.Model = item.Model ?? string.Empty;
        state.Temperature = item.Temperature;
        state.Provider = string.IsNullOrWhiteSpace(item.Provider) ? InferProvider(item.Model) : item.Provider ?? string.Empty;
        state.ProviderId = item.ProviderId ?? string.Empty;
        state.SystemPrompt = item.SystemPrompt ?? string.Empty;
        state.LastPrompt = item.LastPrompt ?? string.Empty;
        state.RawResponseText = item.RawResponseText ?? string.Empty;
        state.PreviousResponseId = item.PreviousResponseId ?? string.Empty;
        state.CreatedAt = item.CreatedAt == default ? DateTime.Now : item.CreatedAt;
        state.LastUpdatedAt = item.LastUpdatedAt == default ? state.CreatedAt : item.LastUpdatedAt;
        state.LatencySeconds = item.LatencySeconds;
        state.IsPending = false;
        state.HasError = false;
        state.PromptHistory.Clear();
        state.PromptHistory.AddRange(item.PromptHistory ?? new List<string>());
        state.LastAnalysis = AnalyzeResponse(state.RawResponseText);

        string pendingText = state.RawResponseText;
        if (state.RawTextBox.IsHandleCreated)
            state.RawTextBox.Text = pendingText;
        else
            state.RawTextBox.HandleCreated += (_, __) => state.RawTextBox.Text = pendingText;

        UpdateResponseMeta(state);

        byte[]? imageBytes = null;
        if (!string.IsNullOrWhiteSpace(item.ImageBase64))
        {
            try { imageBytes = Convert.FromBase64String(item.ImageBase64); }
            catch { imageBytes = null; }
        }

        UpdateConversationImage(state, imageBytes, item.ImageMimeType, item.ImageName);
        UpdateConversationTabText(state);
    }

    private void RestoreComposerImageFromSession(AppSessionState session)
    {
        if (string.IsNullOrWhiteSpace(session.CurrentImageBase64))
        {
            _imageBytes = null;
            _imageMimeType = null;
            _imagePath = null;
            Image? old = _previewImage;
            _previewImage = null;
            _picPreview.Image = null;
            old?.Dispose();
            UpdateImageInfo();
            return;
        }

        try
        {
            _imageBytes = Convert.FromBase64String(session.CurrentImageBase64);
            _imageMimeType = string.IsNullOrWhiteSpace(session.CurrentImageMimeType) ? "image/png" : session.CurrentImageMimeType!;
            _imagePath = session.CurrentImagePath;
            using MemoryStream ms = new(_imageBytes);
            using Image temp = Image.FromStream(ms);
            ReplacePreviewImage(temp);
            UpdateImageInfo();
        }
        catch
        {
            _imageBytes = null;
            _imageMimeType = null;
            _imagePath = null;
            Image? old = _previewImage;
            _previewImage = null;
            _picPreview.Image = null;
            old?.Dispose();
            UpdateImageInfo();
        }
    }

    private void RebuildPromptHistoryList()
    {
    }

    private void RestoreConversationViewsAfterShow()
    {
        ConversationTabState? active = GetActiveConversation();
        if (active != null && !string.IsNullOrWhiteSpace(active.RawResponseText))
            _ = RenderResponseAsync(active, active.RawResponseText);
    }

    private void NewFreshSession()
    {
        if (MessageBox.Show(this, "Start a new fresh session and clear the current restored conversation history?", "Fresh Session", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        CloseAllConversations();
        _promptHistory.Clear();
        RebuildPromptHistoryList();
        _txtQuestion.Clear();
        _txtSystemPrompt.Clear();
        ClearSelectedImage();
        CreateDraftConversation();
        _sessionStateService.Clear();
        SaveSessionNow();
        SetStatus("Started a fresh session.");
    }

    private void ShowKeyboardShortcuts()
    {
        string text = string.Join(Environment.NewLine, new[]
        {
            "Ctrl+Enter  Send prompt",
            "Ctrl+T      New draft tab",
            "Ctrl+W      Close active tab",
            "Ctrl+Shift+Q Screenshot analyze",
            "Ctrl+Shift+N Fresh session",
            "Ctrl+Shift+S Save session now",
            "Ctrl+P      Focus persona selector",
            "Ctrl+L      Clear response",
            "Ctrl+1..9   Switch tabs"
        });

        MessageBox.Show(this, text, "Keyboard Shortcuts", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ShowAbout()
    {
        using AboutForm dialog = new();
        dialog.ShowDialog(this);
    }

    private TreeNode? FindNodeByPersonaName(TreeNodeCollection nodes, string personaName)
    {
        foreach (TreeNode node in nodes)
        {
            if (node.Tag is PersonaTreeTag tag && tag.PersonaName == personaName)
                return node;

            TreeNode? childResult = FindNodeByPersonaName(node.Nodes, personaName);
            if (childResult != null)
                return childResult;
        }
        return null;
    }

    private void PersonaTree_NodeMouseClick(object? sender, TreeNodeMouseClickEventArgs e)
    {
        _personaTree.SelectedNode = e.Node;
        if (e.Button == MouseButtons.Right && e.Node.Tag is PersonaTreeTag tagR && !string.IsNullOrWhiteSpace(tagR.PersonaName))
        {
            _pendingPersonaContextName = tagR.PersonaName;
            _personaMenu.Show(_personaTree, e.Location);
        }
        else if (e.Button == MouseButtons.Left)
        {
            ApplyPersonaTreeSelection(e.Node);
        }
    }

    private void ApplyPersonaTreeSelection(TreeNode node)
    {
        if (node.Tag is not PersonaTreeTag tag || string.IsNullOrWhiteSpace(tag.PersonaName))
        {
            if (node.Nodes.Count > 0)
            {
                if (node.IsExpanded)
                    node.Collapse();
                else
                    node.Expand();
            }
            return;
        }

        SelectPersona(tag.PersonaName);
        SetStatus("Persona selected from explorer.");
    }

    private void SelectPendingPersonaContext()
    {
        if (!string.IsNullOrWhiteSpace(_pendingPersonaContextName))
            SelectPersona(_pendingPersonaContextName);
    }

    private void ConversationTabs_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _conversationTabs.TabPages.Count)
            return;

        TabPage tab = _conversationTabs.TabPages[e.Index];
        Rectangle rect = _conversationTabs.GetTabRect(e.Index);
        bool isSelected = _conversationTabs.SelectedIndex == e.Index;

        Color backColor = isSelected ? _activeTheme.Accent : _activeTheme.SurfaceAlt;
        Color textColor = isSelected ? _activeTheme.AccentForeground : _activeTheme.Text;

        using SolidBrush backBrush = new(backColor);
        e.Graphics.FillRectangle(backBrush, rect);

        ConversationTabState? state = _conversationStates.TryGetValue(tab, out ConversationTabState? s) ? s : null;
        TablerIcon tabIcon = state switch
        {
            { HasError: true } => TablerIcon.AlertTriangle,
            { IsPending: true } => TablerIcon.Loader,
            _ when state != null && string.IsNullOrWhiteSpace(state.RawResponseText) => TablerIcon.FileText,
            _ => TablerIcon.Message
        };

        int iconSize = Math.Min(rect.Height - 4, 16);
        int iconX = rect.X + 6;
        int iconY = rect.Y + (rect.Height - iconSize) / 2;

        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        int textStartX = iconX;
        if (state is { IsPinned: true })
        {
            TablerIconRenderer.Draw(e.Graphics, TablerIcon.Pin, new Rectangle(iconX, iconY, iconSize, iconSize), textColor);
            textStartX += iconSize + 2;
        }

        Color iconColor = state is { HasError: true } ? Color.FromArgb(255, 200, 50) : textColor;
        TablerIconRenderer.Draw(e.Graphics, tabIcon, new Rectangle(textStartX, iconY, iconSize, iconSize), iconColor);
        textStartX += iconSize + 4;

        Rectangle textRect = new(textStartX, rect.Y, Math.Max(0, rect.Right - textStartX - ConversationTabCloseButtonWidth - 8), rect.Height);
        string title = state != null ? NormalizeTabTitle(state.Title) : tab.Text.Trim();
        TextRenderer.DrawText(e.Graphics, title, _conversationTabs.Font, textRect, textColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        Rectangle closeRect = GetConversationTabCloseRect(e.Index);
        using SolidBrush closeBrush = new(isSelected ? Color.FromArgb(220, 20, 60) : Color.FromArgb(160, 160, 160));
        e.Graphics.FillEllipse(closeBrush, closeRect);
        int closeIconSize = closeRect.Width - 4;
        TablerIconRenderer.Draw(e.Graphics, TablerIcon.X, new Rectangle(closeRect.X + 2, closeRect.Y + 2, closeIconSize, closeIconSize), Color.White);
    }

    private Rectangle GetConversationTabCloseRect(int tabIndex)
    {
        Rectangle tabRect = _conversationTabs.GetTabRect(tabIndex);
        return new Rectangle(tabRect.Right - ConversationTabCloseButtonWidth - 6, tabRect.Top + (tabRect.Height - 16) / 2, 16, 16);
    }

    private void ConversationTabs_MouseDown(object? sender, MouseEventArgs e)
    {
        for (int i = 0; i < _conversationTabs.TabPages.Count; i++)
        {
            Rectangle tabRect = _conversationTabs.GetTabRect(i);
            if (!tabRect.Contains(e.Location))
                continue;

            _conversationTabs.SelectedIndex = i;
            if (e.Button == MouseButtons.Right)
            {
                _conversationTabMenu.Show(_conversationTabs, e.Location);
                return;
            }

            if (GetConversationTabCloseRect(i).Contains(e.Location))
            {
                if (_conversationStates.TryGetValue(_conversationTabs.TabPages[i], out ConversationTabState? state))
                    DisposeConversation(state);
                UpdateUiState();
                break;
            }
        }
    }

    private void UpdateAutoComplete()
        => _promptAssistanceHelper.UpdateAutoComplete(_promptAutoComplete, _promptHistory);

    private static bool IsSupportedImageExtension(string? extension)
        => ImageHelper.IsSupportedImageExtension(extension);

    private static string GetMimeTypeFromExtension(string extension)
        => ImageHelper.GetMimeTypeFromExtension(extension);

    private string InferProvider(string? model)
        => PromptAssistanceHelper.InferProvider(_providers, _providerService, model);

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape && _isPromptExpanded)
        {
            CollapsePrompt();
            return true;
        }

        if (keyData == (Keys.Control | Keys.Enter))
        {
            _ = AskAsync(null, false);
            return true;
        }

        if (keyData == (Keys.Control | Keys.Shift | Keys.A))
        {
            _ = SnipScreenAsync();
            return true;
        }

        if (keyData == (Keys.Control | Keys.P))
        {
            _cmbPersona.Focus();
            return true;
        }

        for (int digit = 1; digit <= 9; digit++)
        {
            Keys combo = Keys.Control | (Keys)((int)Keys.D1 + digit - 1);
            if (keyData == combo)
            {
                int index = digit - 1;
                if (index < _conversationTabs.TabPages.Count)
                    _conversationTabs.SelectedIndex = index;
                return true;
            }
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        // Confirm before closing if the setting is enabled and there are active conversations
        if (_workspaceSettings.ConfirmBeforeClose
            && _conversationStates.Count > 0
            && e.CloseReason == CloseReason.UserClosing)
        {
            DialogResult result = MessageBox.Show(
                this,
                "Are you sure you want to close BuddyAI? Your session will be saved automatically.",
                "Confirm Close",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
            {
                e.Cancel = true;
                return;
            }
        }

        // Collapse prompt if expanded so the textbox is properly parented for session save
        if (_isPromptExpanded)
            CollapsePrompt();

        SaveWorkspace();
        try
        {
            _sessionStateService.Save(BuildSessionSnapshot());
        }
        catch (Exception ex)
        {
            _diagnostics.Error("Session save failed during shutdown: " + ex.Message);
        }

        _requestCts?.Cancel();
        _requestCts?.Dispose();
        _clipboardMonitorTimer.Stop();
        _clipboardMonitorTimer.Dispose();
        _suggestionsDebounceTimer.Stop();
        _suggestionsDebounceTimer.Dispose();
        _autoSaveTimer.Stop();
        _autoSaveTimer.Dispose();
        ReleaseGlobalSnipShortcutRegistration();

        _dockHost.DisposeFloatingWindows();

        if (_activeQuickResultWindow != null && !_activeQuickResultWindow.IsDisposed)
        {
            _activeQuickResultWindow.Close();
            _activeQuickResultWindow.Dispose();
            _activeQuickResultWindow = null;
        }

        foreach (ConversationTabState state in _conversationStates.Values.ToList())
        {
            state.Picture.Image?.Dispose();
            state.WebView.Dispose();
        }

        _client.Dispose();
        _iconCache.Dispose();
        _previewImage?.Dispose();
        _previewImage = null;
        _toolTip.Dispose();
        _conversationTabMenu.Dispose();
        _personaMenu.Dispose();
        _promptMenu.Dispose();
        _responseMenu.Dispose();
    }

    private void RefreshConversationToolbarIcons()
    {
        Image sampleIcon = _iconCache.Get(TablerIcon.Copy, TablerIconCache.ToolStripIconSize);
        foreach (ConversationTabState state in _conversationStates.Values)
        {
            state.Tool.ImageScalingSize = sampleIcon.Size;
            state.CopyButton.Image = sampleIcon;
            state.CopySummaryButton.Image = _iconCache.Get(TablerIcon.ClipboardText, TablerIconCache.ToolStripIconSize);
            state.ClearButton.Image = _iconCache.Get(TablerIcon.Trash, TablerIconCache.ToolStripIconSize);
            state.ReRenderButton.Image = _iconCache.Get(TablerIcon.Refresh, TablerIconCache.ToolStripIconSize);
            state.ExportButton.Image = _iconCache.Get(TablerIcon.FileExport, TablerIconCache.ToolStripIconSize);
            state.WindowButton.Image = _iconCache.Get(TablerIcon.WindowMaximize, TablerIconCache.ToolStripIconSize);
        }
    }

    private void DisposeConversation(ConversationTabState state)
    {
        state.Picture.Image?.Dispose();
        state.WebView.Dispose();
        _conversationStates.Remove(state.TabPage);
        _conversationTabs.TabPages.Remove(state.TabPage);
        state.TabPage.Dispose();
    }

    private void CloseAllConversations()
    {
        foreach (ConversationTabState state in _conversationStates.Values.ToList())
            DisposeConversation(state);
    }

    private void CloseOtherConversations()
    {
        ConversationTabState? active = GetActiveConversation();
        if (active == null)
            return;

        foreach (ConversationTabState state in _conversationStates.Values.ToList())
        {
            if (state == active)
                continue;
            DisposeConversation(state);
        }

        _conversationTabs.SelectedTab = active.TabPage;
    }

    private void CreateDraftConversation()
    {
        CreateConversationTab("New Chat");
        UpdateUiState();
        SetStatus("New conversation tab created.");
    }

    private void ExportConversation(ConversationTabState state)
    {
        if (string.IsNullOrWhiteSpace(state.RawResponseText))
        {
            MessageBox.Show(this, "No response to export.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using SaveFileDialog dialog = new()
        {
            Title = "Export Conversation",
            Filter = "JSON|*.json|Markdown|*.md|HTML|*.html|Text|*.txt",
            FileName = NormalizeTabTitle(state.Title)
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        ConversationExportModel model = _conversationManager.CreateExportModel(state);
        ConversationExportService.Save(model, dialog.FileName);
        SetStatus("Conversation exported.");
        _diagnostics.Info("Conversation exported to " + dialog.FileName);
    }

    private void ExportActiveConversation()
    {
        ConversationTabState? state = GetActiveConversation();
        if (state != null)
            ExportConversation(state);
    }

    private void RenameActiveConversation()
    {
        ConversationTabState? state = GetActiveConversation();
        if (state == null)
            return;

        using TextInputDialog dialog = new("Rename Tab", "Tab title:", state.Title);
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        string newTitle = dialog.InputText;
        if (!string.IsNullOrWhiteSpace(newTitle))
        {
            state.Title = NormalizeTabTitle(newTitle);
            UpdateConversationTabText(state);
            SetStatus("Tab renamed.");
        }
    }

    private void DuplicateActiveConversation()
    {
        ConversationTabState? source = GetActiveConversation();
        if (source == null)
            return;

        ConversationTabState dup = CreateConversationTab(source.Title + " Copy");
        dup.Title = NormalizeTabTitle(source.Title + " Copy");
        dup.Persona = source.Persona;
        dup.Model = source.Model;
        dup.Temperature = source.Temperature;
        dup.Provider = source.Provider;
        dup.ProviderId = source.ProviderId;
        dup.SystemPrompt = source.SystemPrompt;
        dup.LastPrompt = source.LastPrompt;
        dup.RawResponseText = source.RawResponseText;
        dup.PreviousResponseId = source.PreviousResponseId;
        dup.LastAnalysis = source.LastAnalysis;
        dup.EstimatedTokens = source.EstimatedTokens;
        dup.EstimatedCostUsd = source.EstimatedCostUsd;
        dup.LatencySeconds = source.LatencySeconds;
        dup.PromptHistory.Clear();
        dup.PromptHistory.AddRange(source.PromptHistory);

        dup.RawTextBox.Text = source.RawResponseText ?? string.Empty;
        UpdateResponseMeta(dup);
        UpdateConversationImage(dup, source.ImageBytes, source.ImageMimeType, source.ImageName);
        UpdateConversationTabText(dup);

        _ = RenderResponseAsync(dup, dup.RawResponseText);
        SetStatus("Tab duplicated.");
    }

    private void PinActiveConversation()
    {
        ConversationTabState? state = GetActiveConversation();
        if (state == null)
            return;

        state.IsPinned = !state.IsPinned;
        UpdateConversationTabText(state);
        SetStatus(state.IsPinned ? "Tab pinned." : "Tab unpinned.");
    }

    private void OpenConversationWindow(ConversationTabState state)
    {
        if (string.IsNullOrWhiteSpace(state.RawResponseText))
            return;

        string html = BuildResponseHtml(
            state.RawResponseText,
            state.LastAnalysis ?? AnalyzeResponse(state.RawResponseText),
            state.ResponseViewerStateJson);

        ConversationWindowForm window = new(state.Title, html, state.RawResponseText);
        window.Show(this);
    }

    private void OpenActiveConversationWindow()
    {
        ConversationTabState? state = GetActiveConversation();
        if (state != null)
            OpenConversationWindow(state);
    }

    private void OpenUsageDashboard()
    {
        UsageMetricsService.UsageSummary summary = _usageMetricsService.GetSummary();
        using UsageDashboardForm dialog = new(summary);
        dialog.ShowDialog(this);
    }

    private void PurgeUsageStatistics()
    {
        DialogResult result = MessageBox.Show(
            this,
            "Are you sure you want to purge all usage statistics? This cannot be undone.",
            "Purge Usage Statistics",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (result != DialogResult.Yes)
            return;

        _usageMetricsService.Purge();
        RefreshUsageStatus();
        SetStatus("Usage statistics purged.");
        _diagnostics.Info("Usage statistics purged by user.");
    }

    private void ExportDiagnostics()
    {
        using SaveFileDialog dialog = new()
        {
            Title = "Export Diagnostics",
            Filter = "Text Files|*.txt|All Files|*.*",
            FileName = "BuddyAI.diagnostics.txt"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        File.WriteAllText(dialog.FileName, _diagnostics.GetFullText());
        SetStatus("Diagnostics exported.");
        _diagnostics.Info("Diagnostics exported to " + dialog.FileName);
    }
}
