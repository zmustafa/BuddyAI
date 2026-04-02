using System.Text.RegularExpressions;
using Microsoft.Web.WebView2.WinForms;
using BuddyAI.Models;
using BuddyAI.Services;
using ThemeService = BuddyAI.Services.ThemeService;

namespace BuddyAI.Helpers;

/// <summary>
/// Manages conversation tab lifecycle: creation, disposal, duplication,
/// pinning, renaming, tab drawing, and session state serialization.
/// Extracted from AIQ to isolate conversation management concerns.
/// </summary>
internal sealed class ConversationManager
{
    private readonly TabControl _conversationTabs;
    private readonly Dictionary<TabPage, ConversationTabState> _conversationStates;
    private readonly ContextMenuStrip _responseMenu;
    private readonly DiagnosticsService _diagnostics;

    private const int ConversationTabCloseButtonWidth = 18;

    public ConversationManager(
        TabControl conversationTabs,
        Dictionary<TabPage, ConversationTabState> conversationStates,
        ContextMenuStrip responseMenu,
        DiagnosticsService diagnostics)
    {
        _conversationTabs = conversationTabs;
        _conversationStates = conversationStates;
        _responseMenu = responseMenu;
        _diagnostics = diagnostics;
    }

    public ConversationTabState CreateConversationTab(
        string title,
        string persona,
        string model,
        string temperature,
        string provider,
        string systemPrompt,
        string lastPrompt,
        byte[]? imageBytes,
        string? imageMimeType,
        string? imagePath)
    {
        TabPage page = new();
        page.Padding = new Padding(0);

        SplitContainer split = new()
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 250,
            FixedPanel = FixedPanel.Panel1,
            Panel1MinSize = 180
        };

        Panel left = new() { Dock = DockStyle.Fill, Padding = new Padding(8) };
        PictureBox picture = new() { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, SizeMode = PictureBoxSizeMode.Zoom };
        Label imageInfo = new() { Dock = DockStyle.Bottom, Height = 32, BorderStyle = BorderStyle.FixedSingle, TextAlign = ContentAlignment.MiddleLeft, Text = "No image" };
        left.Controls.Add(picture);
        left.Controls.Add(imageInfo);
        split.Panel1.Controls.Add(left);

        Panel right = new() { Dock = DockStyle.Fill, Padding = new Padding(6) };
        ToolStrip tool = new() { Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden };
        ToolStripButton btnCopy = new("Copy") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        ToolStripButton btnCopySummary = new("Summary") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        ToolStripButton btnClear = new("Clear") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        ToolStripButton btnReRender = new("Re-Render") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        ToolStripButton btnExport = new("Export") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        ToolStripButton btnWindow = new("Window") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        ToolStripLabel meta = new("No response") { Alignment = ToolStripItemAlignment.Right };
        tool.Items.AddRange(new ToolStripItem[] { btnCopy, btnCopySummary, btnClear, btnReRender, btnExport, btnWindow, new ToolStripSeparator(), meta });

        TabControl responseTabs = new() { Dock = DockStyle.Fill };
        TabPage formattedTab = new("Formatted");
        TabPage rawTab = new("Raw");
        WebView2 webView = new() { Dock = DockStyle.Fill, DefaultBackgroundColor = Color.White };
        TextBox rawText = new() { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Both, WordWrap = false, ReadOnly = true, ContextMenuStrip = _responseMenu, Font = new Font("Consolas", 10F) };
        formattedTab.Controls.Add(webView);
        rawTab.Controls.Add(rawText);
        responseTabs.TabPages.Add(formattedTab);
        responseTabs.TabPages.Add(rawTab);
        right.Controls.Add(responseTabs);
        right.Controls.Add(tool);
        split.Panel2.Controls.Add(right);

        page.Controls.Add(split);
        _conversationTabs.TabPages.Add(page);

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
            Persona = persona,
            Model = model,
            Temperature = temperature,
            Provider = provider,
            SystemPrompt = systemPrompt,
            LastPrompt = lastPrompt,
            CreatedAt = DateTime.Now,
            LastUpdatedAt = DateTime.Now
        };

        _conversationStates[page] = state;
        _conversationTabs.SelectedTab = page;
        UpdateConversationImage(state, imageBytes, imageMimeType, imagePath);

        return state;
    }

    public ConversationTabState? GetActiveConversation()
    {
        if (_conversationTabs.SelectedTab == null)
            return null;

        return _conversationStates.TryGetValue(_conversationTabs.SelectedTab, out ConversationTabState? state) ? state : null;
    }

    public void UpdateConversationTabText(ConversationTabState state)
    {
        string icon = state.HasError ? "?" : state.IsPending ? "?" : string.IsNullOrWhiteSpace(state.RawResponseText) ? "??" : "??";
        string pin = state.IsPinned ? "?? " : string.Empty;
        string title = NormalizeTabTitle(state.Title);
        state.TabPage.Text = pin + icon + " " + title;
    }

    public string NormalizeTabTitle(string? title)
    {
        string value = string.IsNullOrWhiteSpace(title) ? "Conversation" : title.Trim();
        value = Regex.Replace(value, "\\s+", " ");
        return value.Length > 28 ? value.Substring(0, 28) + "\u2026" : value;
    }

    public void UpdateConversationImage(ConversationTabState state, byte[]? imageBytes, string? imageMimeType, string? imagePath)
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

    public void UpdateResponseMeta(ConversationTabState state)
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

    public void ClearResponse(ConversationTabState state, string emptyHtml)
    {
        state.RawResponseText = string.Empty;
        state.LastAnalysis = null;
        state.HasError = false;
        state.IsPending = false;
        state.RawTextBox.Clear();
        state.MetaLabel.Text = "No response";
        if (state.ResponseViewInitialized)
            state.WebView.NavigateToString(emptyHtml);
        UpdateConversationTabText(state);
    }

    public void DisposeConversation(ConversationTabState state)
    {
        state.Picture.Image?.Dispose();
        state.WebView.Dispose();
        _conversationStates.Remove(state.TabPage);
        _conversationTabs.TabPages.Remove(state.TabPage);
        state.TabPage.Dispose();
    }

    public void CloseOtherConversations(ConversationTabState active)
    {
        foreach (ConversationTabState state in _conversationStates.Values.ToList())
        {
            if (state == active)
                continue;
            DisposeConversation(state);
        }
        _conversationTabs.SelectedTab = active.TabPage;
    }

    public void CloseAllConversations()
    {
        foreach (ConversationTabState state in _conversationStates.Values.ToList())
            DisposeConversation(state);
    }

    public void MovePinnedTabsToFront()
    {
        List<ConversationTabState> ordered = _conversationStates.Values.OrderByDescending(x => x.IsPinned).ThenBy(x => x.CreatedAt).ToList();
        _conversationTabs.TabPages.Clear();
        foreach (ConversationTabState state in ordered)
            _conversationTabs.TabPages.Add(state.TabPage);
    }

    public ConversationExportModel CreateExportModel(ConversationTabState state)
    {
        return new ConversationExportModel
        {
            Title = state.Title,
            Persona = state.Persona,
            Model = state.Model,
            Provider = state.Provider,
            SystemPrompt = state.SystemPrompt,
            Prompt = state.LastPrompt,
            Response = state.RawResponseText,
            PreviousResponseId = state.PreviousResponseId,
            ImageName = state.ImageName ?? string.Empty,
            ImageMimeType = state.ImageMimeType ?? string.Empty,
            EstimatedTokens = state.EstimatedTokens,
            EstimatedCostUsd = state.EstimatedCostUsd,
            LatencySeconds = state.LatencySeconds,
            CreatedAt = state.CreatedAt,
            LastUpdatedAt = state.LastUpdatedAt,
            PromptHistory = state.PromptHistory.ToList()
        };
    }

    public ConversationSessionState CreateSessionState(ConversationTabState state)
    {
        return new ConversationSessionState
        {
            Title = state.Title,
            Persona = state.Persona,
            Model = state.Model,
            Temperature = state.Temperature,
            Provider = state.Provider,
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
        };
    }

    public void RestoreConversationFromSession(
        ConversationTabState state,
        ConversationSessionState item,
        Func<string, string> inferProvider,
        Func<string?, string> normalizeTemperature,
        Func<string, ResponseAnalysis> analyzeResponse)
    {
        state.Title = string.IsNullOrWhiteSpace(item.Title) ? "Conversation" : item.Title;
        state.Persona = item.Persona ?? string.Empty;
        state.Model = item.Model ?? string.Empty;
        state.Temperature = normalizeTemperature(item.Temperature);
        state.Provider = string.IsNullOrWhiteSpace(item.Provider) ? inferProvider(item.Model ?? string.Empty) : item.Provider ?? string.Empty;
        state.SystemPrompt = item.SystemPrompt ?? string.Empty;
        state.LastPrompt = item.LastPrompt ?? string.Empty;
        state.RawResponseText = item.RawResponseText ?? string.Empty;
        state.PreviousResponseId = item.PreviousResponseId ?? string.Empty;
        state.CreatedAt = item.CreatedAt == default ? DateTime.Now : item.CreatedAt;
        state.LastUpdatedAt = item.LastUpdatedAt == default ? state.CreatedAt : item.LastUpdatedAt;
        state.LatencySeconds = item.LatencySeconds;
        state.EstimatedTokens = item.EstimatedTokens;
        state.EstimatedCostUsd = item.EstimatedCostUsd;
        state.IsPinned = item.IsPinned;
        state.HasError = item.HasError;
        state.IsPending = item.IsPending;
        state.PromptHistory.Clear();
        state.PromptHistory.AddRange(item.PromptHistory ?? new List<string>());
        state.LastAnalysis = analyzeResponse(state.RawResponseText);

        string pendingText = state.RawResponseText;
        if (state.RawTextBox.IsHandleCreated)
        {
            state.RawTextBox.Text = pendingText;
        }
        else
        {
            state.RawTextBox.HandleCreated += (_, __) => state.RawTextBox.Text = pendingText;
        }

        UpdateResponseMeta(state);

        byte[]? imageBytes = null;
        if (!string.IsNullOrWhiteSpace(item.ImageBase64))
        {
            try
            {
                imageBytes = Convert.FromBase64String(item.ImageBase64);
            }
            catch
            {
                imageBytes = null;
            }
        }

        UpdateConversationImage(state, imageBytes, item.ImageMimeType, item.ImageName);
        UpdateConversationTabText(state);
    }

    public void DrawConversationTab(DrawItemEventArgs e, ThemeService.ThemeProfile activeTheme)
    {
        if (e.Index < 0 || e.Index >= _conversationTabs.TabPages.Count)
            return;

        TabPage tab = _conversationTabs.TabPages[e.Index];
        Rectangle rect = _conversationTabs.GetTabRect(e.Index);
        bool isSelected = _conversationTabs.SelectedIndex == e.Index;

        Color backColor = isSelected ? activeTheme.Accent : activeTheme.SurfaceAlt;
        Color textColor = isSelected ? Color.White : activeTheme.Text;

        using SolidBrush backBrush = new(backColor);
        e.Graphics.FillRectangle(backBrush, rect);

        Rectangle textRect = new(rect.X + 6, rect.Y, Math.Max(0, rect.Width - ConversationTabCloseButtonWidth - 10), rect.Height);
        TextRenderer.DrawText(e.Graphics, tab.Text, _conversationTabs.Font, textRect, textColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        Rectangle closeRect = GetConversationTabCloseRect(e.Index);
        using SolidBrush closeBrush = new(isSelected ? Color.FromArgb(220, 20, 60) : Color.FromArgb(160, 160, 160));
        e.Graphics.FillEllipse(closeBrush, closeRect);
        TextRenderer.DrawText(e.Graphics, "\u00d7", _conversationTabs.Font, closeRect, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    public Rectangle GetConversationTabCloseRect(int tabIndex)
    {
        Rectangle tabRect = _conversationTabs.GetTabRect(tabIndex);
        return new Rectangle(tabRect.Right - ConversationTabCloseButtonWidth - 6, tabRect.Top + (tabRect.Height - 16) / 2, 16, 16);
    }

    public bool HandleTabMouseDown(MouseEventArgs e, ContextMenuStrip tabMenu, Action updateUiState)
    {
        for (int i = 0; i < _conversationTabs.TabPages.Count; i++)
        {
            Rectangle tabRect = _conversationTabs.GetTabRect(i);
            if (!tabRect.Contains(e.Location))
                continue;

            _conversationTabs.SelectedIndex = i;
            if (e.Button == MouseButtons.Right)
            {
                tabMenu.Show(_conversationTabs, e.Location);
                return true;
            }

            if (GetConversationTabCloseRect(i).Contains(e.Location))
            {
                if (_conversationStates.TryGetValue(_conversationTabs.TabPages[i], out ConversationTabState? state))
                    DisposeConversation(state);
                updateUiState();
                return true;
            }
        }

        return false;
    }
}
