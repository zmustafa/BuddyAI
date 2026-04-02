using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text.Json;
using BuddyAI.Models;
using BuddyAI.Services;
using SelectionCaptureApp;
using Microsoft.Web.WebView2.WinForms;
using Markdig;

namespace BuddyAI.Forms;

public sealed class TextCaptureForm : Form
{
    #region Win32 Interop

    [DllImport("dwmapi.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void DwmSetWindowAttribute(
        IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(
        IntPtr hwnd, ref MARGINS margins);

    [StructLayout(LayoutKind.Sequential)]
    private struct MARGINS
    {
        public int Left, Right, Top, Bottom;
    }

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
    private const int DWMWCP_ROUND = 2;
    private const int DWMSBT_TRANSIENTWINDOW = 3;

    // Resize hit-test constants
    private const int WM_NCHITTEST = 0x84;
    private const int HTLEFT = 10;
    private const int HTRIGHT = 11;
    private const int HTTOP = 12;
    private const int HTTOPLEFT = 13;
    private const int HTTOPRIGHT = 14;
    private const int HTBOTTOM = 15;
    private const int HTBOTTOMLEFT = 16;
    private const int HTBOTTOMRIGHT = 17;
    private const int ResizeGripSize = 6;

    #endregion

    // Theme palette
    private readonly Color _bgColor = Color.FromArgb(255, 80, 80, 80);
    private readonly Color _surfaceColor = Color.FromArgb(255, 95, 95, 95);
    private readonly Color _inputBg = Color.FromArgb(255, 65, 65, 65);
    private readonly Color _panelBg = Color.FromArgb(255, 88, 88, 88);
    private readonly Color _textColor = Color.WhiteSmoke;
    private readonly Color _mutedText = Color.FromArgb(255, 220, 220, 220);
    private readonly Color _dimText = Color.FromArgb(255, 190, 190, 190);
    private readonly Color _accent = Color.FromArgb(255, 30, 144, 255);
    private readonly Color _accentHover = Color.FromArgb(255, 60, 160, 255);
    private readonly Color _accentPress = Color.FromArgb(255, 0, 120, 215);
    private readonly Color _border = Color.FromArgb(255, 115, 115, 115);
    private readonly Color _subtleBorder = Color.FromArgb(255, 100, 100, 100);
    private readonly Color _selectionBg = Color.FromArgb(255, 110, 110, 110);
    private readonly Color _hoverBg = Color.FromArgb(255, 120, 120, 120);
    private readonly Color _warningText = Color.FromArgb(255, 230, 160, 60);
    private readonly Color _successText = Color.FromArgb(255, 100, 200, 120);
    private readonly Color _closeBtnHover = Color.FromArgb(255, 200, 60, 60);

    // Controls — Title bar
    private readonly Button _btnCloseX = new();

    // Controls — Top region
    private readonly Label _lblContextHeader = new();
    private readonly TextBox _txtContext = new();

    // Controls — Middle region: left pane
    private readonly ListBox _lstCategories = new();
    private readonly ListBox _lstPersonas = new();
    private readonly TextBox _txtSearch = new();

    // Controls — Middle region: right pane
    private readonly Label _lblPersonaName = new();
    private readonly TextBox _txtSystemPrompt = new();
    private readonly TextBox _txtMessageTemplate = new();
    private readonly Label _lblMessageTemplateHint = new();
    private readonly TextBox _txtCustomAsk = new();
    private readonly Label _lblCustomAskHint = new();

    // Controls — Image snip
    private readonly Button _btnSnipNow = new();
    private readonly Button _btnSnipDelay = new();
    private readonly Button _btnClearImage = new();
    private readonly PictureBox _picSnipPreview = new();
    private readonly Label _lblImageStatus = new();
    private readonly ContextMenuStrip _snipDelayMenu = new();

    // Controls — Bottom region
    private readonly Button _btnRun = new();
    private readonly Button _btnCancel = new();

    // Controls — Result region (shown after AI response)
    private readonly Panel _resultPanel = new();
    private readonly Panel _pnlResultHeader = new();
    private readonly Label _lblResultHeader = new();
    private readonly FlowLayoutPanel _pnlTabs = new();
    private readonly Button _btnTabRendered = new();
    private readonly Button _btnTabRaw = new();
    private readonly Panel _resultContentPanel = new();
    private readonly TextBox _txtResult = new();
    private readonly Panel _pnlRawCodeHeader = new();
    private readonly Label _lblRawCodeLanguage = new();
    private readonly Button _btnRawCodeMaximize = new();
    private readonly WebView2 _webViewResult = new();
    private readonly Button _btnReplace = new();
    private readonly Button _btnCopyResult = new();
    private readonly Button _btnClose = new();

    // Root layout reference for dynamic row manipulation
    private TableLayoutPanel _rootLayout = null!;

    // Data
    private readonly PersonaService _personaService = new();
    private List<PersonaRecord> _allPersonas = [];
    private List<string> _allCategories = [];
    private List<PersonaRecord> _filteredPersonas = [];
    private static List<string> _recentPersonaIds = [];
    private static bool _recentsLoaded;

    private const string RecentCategoryLabel = "⏱ Recent";
    private const string FavoritesCategoryLabel = "★ Buddy Lense";
    private const string RecentsFileName = "recent_personas.json";
    private const int MaxRecentCount = 10;

    // Window drag state
    private bool _isDragging;
    private Point _dragStart;

    // WebView state
    private string? _pendingHtmlNavigation;

    // Image state
    private byte[]? _snipImageBytes;
    private string? _snipImageMimeType;
    private Image? _snipPreviewImage;

    // DPI scale factor — set in OnLoad, used throughout layout
    private float _dpi = 1f;

    // Buffered state set before layout is built
    private string? _pendingContextText;

    // Public results
    public string CapturedText => _txtContext.Text.Trim();
    public PersonaRecord? SelectedPersona { get; private set; }
    public string CustomAsk => _txtCustomAsk.Text.Trim();
    public string EditableSystemPrompt => _txtSystemPrompt.Text.Trim();
    public string EditableMessageTemplate => _txtMessageTemplate.Text.Trim();
    public CaptureResult? SourceCaptureResult { get; private set; }
    public byte[]? SnipImageBytes => _snipImageBytes;
    public string? SnipImageMimeType => _snipImageMimeType;
    public string AiResultText { get; private set; } = string.Empty;

    private bool _isRawCodeMode;

    // Callback for AI execution — set by the caller (AIQ)
    public Func<TextCaptureForm, Task>? OnRunRequested { get; set; }

    // Model image support — set by the caller (AIQ)
    public Func<bool>? CheckModelSupportsImages { get; set; }

    // Navigation state
    private enum FocusPane { Search, Categories, Personas, CustomAsk }
    private FocusPane _currentPane = FocusPane.Categories;

    private bool _isProcessing;
    private bool _resultShown;
    private bool _isRawMaximized;

    public TextCaptureForm()
    {
        Text = string.Empty;
        ShowInTaskbar = false;
        TopMost = true;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        KeyPreview = true;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = _bgColor;
        ForeColor = _textColor;
        Padding = new Padding(1);

        SetStyle(ControlStyles.OptimizedDoubleBuffer
               | ControlStyles.AllPaintingInWmPaint
               | ControlStyles.UserPaint, true);

        LoadRecentIds();

        Load += OnLoad;
        Shown += OnShown;
    }

    private void OnLoad(object? sender, EventArgs e)
    {
        _dpi = DeviceDpi / 96f;

        Screen screen = Screen.FromPoint(Cursor.Position);
        int w = (int)(580 * _dpi);
        int h = (int)(420 * _dpi);
        Size = new Size(w, h);
        MinimumSize = new Size((int)(440 * _dpi), (int)(340 * _dpi));

        BuildLayout();
        _ = InitializeWebViewAsync();

        // Apply any buffered state that was set before layout existed
        if (_pendingContextText != null)
        {
            _txtContext.Text = _pendingContextText;
            _pendingContextText = null;
        }

        CenterOnMonitor(screen);
        LoadPersonas();
    }

    private int Dpi(int value) => (int)(value * _dpi);
    private float DpiF(float value) => value * _dpi;

    private void CenterOnMonitor(Screen screen)
    {
        Rectangle work = screen.WorkingArea;
        
        int existingCount = Application.OpenForms.OfType<TextCaptureForm>().Count(f => f != this && f.Visible);
        int offset = existingCount * Dpi(28);

        int x = work.Left + (work.Width - Width) / 2 + offset;
        int y = work.Top + (work.Height - Height) / 2 + offset;

        x = Math.Max(work.Left, Math.Min(x, work.Right - Width));
        y = Math.Max(work.Top, Math.Min(y, work.Bottom - Height));

        Location = new Point(x, y);
    }

    private void BuildLayout()
    {
        SuspendLayout();

        _rootLayout = new TableLayoutPanel()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(Dpi(8), Dpi(7), Dpi(8), Dpi(6))
        };
        _rootLayout.SuspendLayout();
        _rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        _rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, Dpi(22)));   // title bar
        _rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, Dpi(118)));  // context
        _rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));       // persona + details
        _rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, Dpi(48)));   // image bar
        _rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, Dpi(30)));   // action bar
        _rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));         // result (hidden)
        Controls.Add(_rootLayout);

        // ═══════════════════════════════════════════════════════════
        //  TITLE BAR — [X] close button
        // ═══════════════════════════════════════════════════════════
        Panel titleBar = new()
        {
            Dock = DockStyle.Fill
        };

        _btnCloseX.Text = "✕";
        _btnCloseX.Size = new Size(Dpi(28), Dpi(20));
        _btnCloseX.Dock = DockStyle.Right;
        _btnCloseX.FlatStyle = FlatStyle.Flat;
        _btnCloseX.FlatAppearance.BorderSize = 0;
        _btnCloseX.FlatAppearance.MouseOverBackColor = _closeBtnHover;
        _btnCloseX.FlatAppearance.MouseDownBackColor = Color.FromArgb(180, 30, 30);
        _btnCloseX.BackColor = _bgColor;
        _btnCloseX.ForeColor = _mutedText;
        _btnCloseX.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        _btnCloseX.UseCompatibleTextRendering = true;
        _btnCloseX.TextAlign = ContentAlignment.MiddleCenter;
        _btnCloseX.Padding = new Padding(0);
        _btnCloseX.Cursor = Cursors.Hand;
        _btnCloseX.TabStop = false;
        _btnCloseX.Click += (_, _) => Close();

        titleBar.Controls.Add(_btnCloseX);
        _rootLayout.Controls.Add(titleBar, 0, 0);

        // ═══════════════════════════════════════════════════════════
        //  CONTEXT — Editable text
        // ═══════════════════════════════════════════════════════════
        Panel topPanel = new()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0)
        };

        _lblContextHeader.Text = "Selected Context";
        _lblContextHeader.Dock = DockStyle.Top;
        _lblContextHeader.AutoSize = true;
        _lblContextHeader.Font = new Font("Segoe UI", 7f, FontStyle.Regular);
        _lblContextHeader.ForeColor = _dimText;
        _lblContextHeader.BackColor = _bgColor;
        _lblContextHeader.Margin = new Padding(0, 0, 0, Dpi(2));

        _txtContext.Dock = DockStyle.Fill;
        _txtContext.Multiline = true;
        _txtContext.ReadOnly = false;
        _txtContext.AcceptsReturn = true;
        _txtContext.AcceptsTab = false;
        _txtContext.ScrollBars = ScrollBars.Vertical;
        _txtContext.WordWrap = true;
        _txtContext.Font = new Font("Cascadia Code", 7.5f);
        _txtContext.BackColor = _panelBg;
        _txtContext.ForeColor = _mutedText;
        _txtContext.BorderStyle = BorderStyle.FixedSingle;
        _txtContext.TabStop = false;
        _txtContext.TextChanged += (_, _) => AdjustContextHeight();

        topPanel.Controls.Add(_txtContext);
        topPanel.Controls.Add(_lblContextHeader);
        _rootLayout.Controls.Add(topPanel, 0, 1);

        // ═══════════════════════════════════════════════════════════
        //  MIDDLE REGION — Persona Selection + Details
        // ═══════════════════════════════════════════════════════════
        TableLayoutPanel middleLayout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, Dpi(4), 0, Dpi(4))
        };
        middleLayout.SuspendLayout();
        middleLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55f));
        middleLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45f));
        middleLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        _rootLayout.Controls.Add(middleLayout, 0, 2);

        // ─── LEFT PANEL ───
        Panel leftPane = new()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 0, Dpi(4), 0)
        };

        _txtSearch.Dock = DockStyle.Top;
        _txtSearch.Height = Dpi(20);
        _txtSearch.Font = new Font("Segoe UI", 8.5f);
        _txtSearch.BackColor = _inputBg;
        _txtSearch.ForeColor = _textColor;
        _txtSearch.BorderStyle = BorderStyle.FixedSingle;
        _txtSearch.PlaceholderText = "🔍 Type to search personas...";
        _txtSearch.TextChanged += OnSearchTextChanged;
        _txtSearch.KeyDown += OnSearchKeyDown;
        _txtSearch.GotFocus += (_, _) => SetActivePane(FocusPane.Search);

        SplitContainer navSplit = new()
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = Dpi(2),
            BackColor = _border,
            FixedPanel = FixedPanel.Panel1,
            Panel1MinSize = 0,
            Panel2MinSize = 0
        };

        _lstCategories.Dock = DockStyle.Fill;
        _lstCategories.Font = new Font("Segoe UI", 8.5f);
        _lstCategories.BackColor = _panelBg;
        _lstCategories.ForeColor = _textColor;
        _lstCategories.BorderStyle = BorderStyle.None;
        _lstCategories.IntegralHeight = false;
        _lstCategories.DrawMode = DrawMode.OwnerDrawFixed;
        _lstCategories.ItemHeight = Dpi(19);
        _lstCategories.DrawItem += OnCategoryDrawItem;
        _lstCategories.SelectedIndexChanged += OnCategorySelected;
        _lstCategories.KeyDown += OnCategoryKeyDown;
        _lstCategories.GotFocus += (_, _) => SetActivePane(FocusPane.Categories);

        _lstPersonas.Dock = DockStyle.Fill;
        _lstPersonas.Font = new Font("Segoe UI", 8.5f);
        _lstPersonas.BackColor = _panelBg;
        _lstPersonas.ForeColor = _textColor;
        _lstPersonas.BorderStyle = BorderStyle.None;
        _lstPersonas.IntegralHeight = false;
        _lstPersonas.DrawMode = DrawMode.OwnerDrawFixed;
        _lstPersonas.ItemHeight = Dpi(20);
        _lstPersonas.DrawItem += OnPersonaDrawItem;
        _lstPersonas.SelectedIndexChanged += OnPersonaSelected;
        _lstPersonas.KeyDown += OnPersonaKeyDown;
        _lstPersonas.DoubleClick += (_, _) => ExecuteRun();
        _lstPersonas.GotFocus += (_, _) => SetActivePane(FocusPane.Personas);

        navSplit.Panel1.Controls.Add(_lstCategories);
        navSplit.Panel2.Controls.Add(_lstPersonas);

        leftPane.Controls.Add(navSplit);
        leftPane.Controls.Add(_txtSearch);
        middleLayout.Controls.Add(leftPane, 0, 0);

        // ─── RIGHT PANEL ───
        TableLayoutPanel rightPane = new()
        {
            Dock = DockStyle.Fill,
            BackColor = _panelBg,
            Padding = new Padding(Dpi(8), Dpi(5), Dpi(8), Dpi(5)),
            ColumnCount = 1,
            RowCount = 7
        };
        rightPane.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // persona name
        rightPane.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));   // system prompt
        rightPane.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // message template hint
        rightPane.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));   // message template text
        rightPane.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));    // spacer
        rightPane.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // custom ask hint
        rightPane.RowStyles.Add(new RowStyle(SizeType.Absolute, Dpi(44))); // custom ask textbox
        rightPane.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        _lblPersonaName.Dock = DockStyle.Fill;
        _lblPersonaName.AutoSize = true;
        _lblPersonaName.Font = new Font("Segoe UI Semibold", 9.5f);
        _lblPersonaName.ForeColor = _textColor;
        _lblPersonaName.BackColor = _panelBg;
        _lblPersonaName.Text = "Select a Persona";
        _lblPersonaName.Margin = new Padding(0, 0, 0, Dpi(2));

        _txtSystemPrompt.Dock = DockStyle.Fill;
        _txtSystemPrompt.Multiline = true;
        _txtSystemPrompt.ScrollBars = ScrollBars.Vertical;
        _txtSystemPrompt.Font = new Font("Segoe UI", 8f);
        _txtSystemPrompt.BackColor = _inputBg;
        _txtSystemPrompt.ForeColor = _textColor;
        _txtSystemPrompt.BorderStyle = BorderStyle.FixedSingle;
        _txtSystemPrompt.Margin = new Padding(0, 0, 0, Dpi(4));

        _lblMessageTemplateHint.Text = "Prompt Template";
        _lblMessageTemplateHint.Dock = DockStyle.Fill;
        _lblMessageTemplateHint.AutoSize = true;
        _lblMessageTemplateHint.Font = new Font("Segoe UI", 7.5f);
        _lblMessageTemplateHint.ForeColor = _dimText;
        _lblMessageTemplateHint.BackColor = _panelBg;
        _lblMessageTemplateHint.Margin = new Padding(0, 0, 0, Dpi(1));

        _txtMessageTemplate.Dock = DockStyle.Fill;
        _txtMessageTemplate.Multiline = true;
        _txtMessageTemplate.ScrollBars = ScrollBars.Vertical;
        _txtMessageTemplate.Font = new Font("Segoe UI", 8f);
        _txtMessageTemplate.BackColor = _inputBg;
        _txtMessageTemplate.ForeColor = _textColor;
        _txtMessageTemplate.BorderStyle = BorderStyle.FixedSingle;
        _txtMessageTemplate.Margin = new Padding(0, 0, 0, Dpi(4));

        _lblCustomAskHint.Text = "Custom Ask (optional)";
        _lblCustomAskHint.Dock = DockStyle.Fill;
        _lblCustomAskHint.AutoSize = true;
        _lblCustomAskHint.Font = new Font("Segoe UI", 7.5f);
        _lblCustomAskHint.ForeColor = _dimText;
        _lblCustomAskHint.BackColor = _panelBg;
        _lblCustomAskHint.Margin = new Padding(0, 0, 0, Dpi(1));

        _txtCustomAsk.Dock = DockStyle.Fill;
        _txtCustomAsk.Multiline = true;
        _txtCustomAsk.ScrollBars = ScrollBars.Vertical;
        _txtCustomAsk.Font = new Font("Segoe UI", 8f);
        _txtCustomAsk.BackColor = _inputBg;
        _txtCustomAsk.ForeColor = _textColor;
        _txtCustomAsk.BorderStyle = BorderStyle.FixedSingle;
        _txtCustomAsk.PlaceholderText = "Refine your request...";
        _txtCustomAsk.AcceptsReturn = false;
        _txtCustomAsk.KeyDown += OnCustomAskKeyDown;
        _txtCustomAsk.GotFocus += (_, _) => SetActivePane(FocusPane.CustomAsk);

        rightPane.Controls.Add(_lblPersonaName, 0, 0);
        rightPane.Controls.Add(_txtSystemPrompt, 0, 1);
        rightPane.Controls.Add(_lblMessageTemplateHint, 0, 2);
        rightPane.Controls.Add(_txtMessageTemplate, 0, 3);
        rightPane.Controls.Add(_lblCustomAskHint, 0, 5);
        rightPane.Controls.Add(_txtCustomAsk, 0, 6);
        middleLayout.Controls.Add(rightPane, 1, 0);

        // ═══════════════════════════════════════════════════════════
        //  IMAGE BAR — Snip controls + preview
        // ═══════════════════════════════════════════════════════════
        FlowLayoutPanel imageBar = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, Dpi(2), 0, Dpi(2))
        };
        imageBar.SuspendLayout();

        StyleButton(_btnSnipNow, "📷 Snip Now", isAccent: false);
        _btnSnipNow.Width = Dpi(80);
        _btnSnipNow.Click += async (_, _) => await SnipScreenAsync(0);

        // Build delay context menu
        foreach (int sec in new[] { 2, 4, 6, 10 })
        {
            int delay = sec;
            _snipDelayMenu.Items.Add($"Snip after {sec}s", null, async (_, _) => await SnipScreenAsync(delay));
        }
        _snipDelayMenu.BackColor = _surfaceColor;
        _snipDelayMenu.ForeColor = _textColor;
        _snipDelayMenu.Font = new Font("Segoe UI", 8f);
        _snipDelayMenu.Renderer = new ToolStripProfessionalRenderer(new DarkMenuColorTable());

        StyleButton(_btnSnipDelay, "⏱ Snip ▾", isAccent: false);
        _btnSnipDelay.Width = Dpi(68);
        _btnSnipDelay.Click += (_, _) =>
        {
            _snipDelayMenu.Show(_btnSnipDelay, new Point(0, _btnSnipDelay.Height));
        };

        StyleButton(_btnClearImage, "✕ Clear", isAccent: false);
        _btnClearImage.Width = Dpi(60);
        _btnClearImage.Visible = false;
        _btnClearImage.Click += (_, _) => ClearSnipImage();

        _picSnipPreview.Size = new Size(Dpi(38), Dpi(38));
        _picSnipPreview.SizeMode = PictureBoxSizeMode.Zoom;
        _picSnipPreview.BorderStyle = BorderStyle.FixedSingle;
        _picSnipPreview.BackColor = _inputBg;
        _picSnipPreview.Margin = new Padding(Dpi(4), Dpi(2), Dpi(4), Dpi(2));
        _picSnipPreview.Visible = false;
        _picSnipPreview.Cursor = Cursors.Hand;
        _picSnipPreview.Click += (_, _) => ShowFullPreview();

        _lblImageStatus.Text = string.Empty;
        _lblImageStatus.AutoSize = true;
        _lblImageStatus.Font = new Font("Segoe UI", 7.5f);
        _lblImageStatus.ForeColor = _dimText;
        _lblImageStatus.Margin = new Padding(Dpi(4), Dpi(10), 0, 0);

        imageBar.Controls.Add(_btnSnipNow);
        imageBar.Controls.Add(_btnSnipDelay);
        imageBar.Controls.Add(_btnClearImage);
        imageBar.Controls.Add(_picSnipPreview);
        imageBar.Controls.Add(_lblImageStatus);
        _rootLayout.Controls.Add(imageBar, 0, 3);

        // ═══════════════════════════════════════════════════════════
        //  ACTION BAR
        // ═══════════════════════════════════════════════════════════
        FlowLayoutPanel actionBar = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, Dpi(2), 0, 0)
        };
        actionBar.SuspendLayout();

        StyleButton(_btnRun, "Run", isAccent: true);
        _btnRun.Click += (_, _) => ExecuteRun();

        StyleButton(_btnCancel, "Cancel", isAccent: false);
        _btnCancel.Click += (_, _) => Close();

        actionBar.Controls.Add(_btnRun);
        actionBar.Controls.Add(_btnCancel);
        _rootLayout.Controls.Add(actionBar, 0, 4);

        // ═══════════════════════════════════════════════════════════
        //  RESULT PANEL — Initially hidden (row height = 0)
        // ═══════════════════════════════════════════════════════════
        _resultPanel.Dock = DockStyle.Fill;
        _resultPanel.Padding = new Padding(0, Dpi(4), 0, 0);
        _resultPanel.SuspendLayout();

        _pnlResultHeader.Dock = DockStyle.Top;
        _pnlResultHeader.Height = Dpi(24);
        _pnlResultHeader.SuspendLayout();

        _lblResultHeader.Dock = DockStyle.Left;
        _lblResultHeader.AutoSize = true;
        _lblResultHeader.Font = new Font("Segoe UI Semibold", 8f);
        _lblResultHeader.ForeColor = _accent;
        _lblResultHeader.BackColor = _bgColor;
        _lblResultHeader.Text = "AI Result";
        _lblResultHeader.TextAlign = ContentAlignment.MiddleLeft;

        _pnlTabs.Dock = DockStyle.Right;
        _pnlTabs.FlowDirection = FlowDirection.LeftToRight;
        _pnlTabs.WrapContents = false;
        _pnlTabs.AutoSize = true;
        _pnlTabs.Padding = new Padding(0, Dpi(2), 0, Dpi(2));

        InitTabButton(_btnTabRendered, "Preview");
        InitTabButton(_btnTabRaw, "Raw");
        _btnTabRendered.Click += (_, _) => SelectResultTab(true);
        _btnTabRaw.Click += (_, _) => SelectResultTab(false);
        _pnlTabs.Controls.Add(_btnTabRendered);
        _pnlTabs.Controls.Add(_btnTabRaw);

        _pnlResultHeader.Controls.Add(_lblResultHeader);
        _pnlResultHeader.Controls.Add(_pnlTabs);

        _resultContentPanel.Dock = DockStyle.Fill;
        _resultContentPanel.Padding = new Padding(0, Dpi(4), 0, Dpi(4));

        _txtResult.Dock = DockStyle.Fill;
        _txtResult.Multiline = true;
        _txtResult.ReadOnly = true;
        _txtResult.AcceptsReturn = true;
        _txtResult.ScrollBars = ScrollBars.Both;
        _txtResult.WordWrap = true;
        _txtResult.Font = new Font("Cascadia Code", 8.5f);
        _txtResult.BackColor = _inputBg;
        _txtResult.ForeColor = _textColor;
        _txtResult.BorderStyle = BorderStyle.FixedSingle;
        _txtResult.Visible = false;

        _pnlRawCodeHeader.Dock = DockStyle.Top;
        _pnlRawCodeHeader.Height = Dpi(24);
        _pnlRawCodeHeader.BackColor = _surfaceColor;
        _pnlRawCodeHeader.Visible = false;
        _pnlRawCodeHeader.Padding = new Padding(Dpi(4), 0, 0, 0);

        _lblRawCodeLanguage.Dock = DockStyle.Left;
        _lblRawCodeLanguage.AutoSize = true;
        _lblRawCodeLanguage.Font = new Font("Segoe UI Semibold", 8f);
        _lblRawCodeLanguage.ForeColor = _successText;
        _lblRawCodeLanguage.BackColor = _surfaceColor;
        _lblRawCodeLanguage.TextAlign = ContentAlignment.MiddleLeft;

        _btnRawCodeMaximize.Dock = DockStyle.Right;
        _btnRawCodeMaximize.Text = "⛶";
        _btnRawCodeMaximize.Size = new Size(Dpi(24), Dpi(24));
        _btnRawCodeMaximize.FlatStyle = FlatStyle.Flat;
        _btnRawCodeMaximize.FlatAppearance.BorderSize = 0;
        _btnRawCodeMaximize.FlatAppearance.MouseOverBackColor = _hoverBg;
        _btnRawCodeMaximize.FlatAppearance.MouseDownBackColor = _selectionBg;
        _btnRawCodeMaximize.BackColor = _surfaceColor;
        _btnRawCodeMaximize.ForeColor = _mutedText;
        _btnRawCodeMaximize.Font = new Font("Segoe UI", 9f);
        _btnRawCodeMaximize.UseCompatibleTextRendering = true;
        _btnRawCodeMaximize.TextAlign = ContentAlignment.MiddleCenter;
        _btnRawCodeMaximize.Padding = new Padding(0);
        _btnRawCodeMaximize.Cursor = Cursors.Hand;
        _btnRawCodeMaximize.TabStop = false;
        _btnRawCodeMaximize.Click += (_, _) => ToggleRawMaximize();

        _pnlRawCodeHeader.Controls.Add(_lblRawCodeLanguage);
        _pnlRawCodeHeader.Controls.Add(_btnRawCodeMaximize);

        _webViewResult.Dock = DockStyle.Fill;
        _webViewResult.DefaultBackgroundColor = _inputBg;
        _webViewResult.Visible = true;

        _resultContentPanel.Controls.Add(_webViewResult);
        _resultContentPanel.Controls.Add(_txtResult);
        _resultContentPanel.Controls.Add(_pnlRawCodeHeader);

        SelectResultTab(true); // Default to preview

        FlowLayoutPanel resultActions = new()
        {
            Dock = DockStyle.Bottom,
            Height = Dpi(28),
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, Dpi(2), 0, 0)
        };
        resultActions.SuspendLayout();

        StyleButton(_btnReplace, "Replace Source", isAccent: true);
        _btnReplace.Width = Dpi(90);
        _btnReplace.Click += (_, _) => ExecuteReplace();

        StyleButton(_btnCopyResult, "Copy", isAccent: false);
        _btnCopyResult.Click += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(_txtResult.Text))
                Clipboard.SetText(_txtResult.Text);
        };

        StyleButton(_btnClose, "Close", isAccent: false);
        _btnClose.Click += (_, _) => Close();

        resultActions.Controls.Add(_btnReplace);
        resultActions.Controls.Add(_btnCopyResult);
        resultActions.Controls.Add(_btnClose);

        _resultPanel.Controls.Add(_resultContentPanel);
        _resultPanel.Controls.Add(resultActions);
        _resultPanel.Controls.Add(_pnlResultHeader);
        _rootLayout.Controls.Add(_resultPanel, 0, 5);

        ResumeLayout(false);

        // Safely set the initial split distance without failing out of bounds bounds calculations.
        try { navSplit.SplitterDistance = Dpi(120); } catch { }
        this.Shown += (s, e) => { try { navSplit.SplitterDistance = Dpi(120); } catch { } };

        resultActions.ResumeLayout(false);
        _pnlResultHeader.ResumeLayout(false);
        _resultPanel.ResumeLayout(false);
        actionBar.ResumeLayout(false);
        imageBar.ResumeLayout(false);
        middleLayout.ResumeLayout(false);
        _rootLayout.ResumeLayout(false);
        ResumeLayout(false);

        AttachDragHandlers(this);
    }

    private void InitTabButton(Button btn, string text)
    {
        btn.Text = text;
        btn.Size = new Size(Dpi(60), Dpi(20));
        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderSize = 0;
        btn.Cursor = Cursors.Hand;
        btn.Margin = new Padding(Dpi(2), 0, 0, 0);
        btn.TabStop = false;
    }

    private void SelectResultTab(bool isRenderedActive)
    {
        _webViewResult.Visible = isRenderedActive;
        _txtResult.Visible = !isRenderedActive;
        _pnlRawCodeHeader.Visible = !isRenderedActive && _isRawCodeMode;

        _btnTabRendered.Font = new Font("Segoe UI", 8f, isRenderedActive ? FontStyle.Bold : FontStyle.Regular);
        _btnTabRendered.BackColor = isRenderedActive ? _selectionBg : _bgColor;
        _btnTabRendered.ForeColor = isRenderedActive ? _accent : _dimText;

        _btnTabRaw.Font = new Font("Segoe UI", 8f, !isRenderedActive ? FontStyle.Bold : FontStyle.Regular);
        _btnTabRaw.BackColor = !isRenderedActive ? _selectionBg : _bgColor;
        _btnTabRaw.ForeColor = !isRenderedActive ? _accent : _dimText;
        
        if (isRenderedActive && _isRawMaximized)
        {
            ToggleRawMaximize();
        }
    }

    private void ToggleRawMaximize()
    {
        if (_isRawMaximized)
        {
            // Restore to the original result panel structure
            _isRawMaximized = false;
            _btnRawCodeMaximize.Text = "⛶";
            _resultContentPanel.Controls.Add(_txtResult);
            _resultContentPanel.Controls.Add(_pnlRawCodeHeader);
            _txtResult.BringToFront();
        }
        else
        {
            // Maximize across the entire form boundary
            _isRawMaximized = true;
            _btnRawCodeMaximize.Text = "🗗";
            Controls.Add(_txtResult);
            Controls.Add(_pnlRawCodeHeader);
            _pnlRawCodeHeader.Dock = DockStyle.Top;
            _pnlRawCodeHeader.BringToFront();
            _txtResult.Dock = DockStyle.Fill;
            _txtResult.BringToFront();
        }
    }

    private void AdjustContextHeight()
    {
        if (_rootLayout == null || !IsHandleCreated) return;
        
        int contentHeight;
        if (string.IsNullOrEmpty(_txtContext.Text))
        {
            contentHeight = 0;
        }
        else
        {
            int width = Math.Max(100, _txtContext.Width > 0 ? _txtContext.Width - SystemInformation.VerticalScrollBarWidth - Dpi(10) : Dpi(500));
            using Graphics g = CreateGraphics();
            SizeF size = g.MeasureString(_txtContext.Text, _txtContext.Font, width);
            contentHeight = (int)Math.Ceiling(size.Height) + Dpi(12);
        }
        
        int headerHeight = _lblContextHeader.Height > 0 ? _lblContextHeader.Height : Dpi(16);
        int totalDesired = contentHeight + headerHeight + _lblContextHeader.Margin.Bottom;
        
        int maxHeight = Dpi(118); // ~28% of base height
        int minHeight = headerHeight + Dpi(28); // Enough for ~1 line
        
        int newHeight = Math.Max(minHeight, Math.Min(totalDesired, maxHeight));
        
        if (_rootLayout.RowStyles.Count > 1)
        {
            _rootLayout.SuspendLayout();
            _rootLayout.RowStyles[1].Height = newHeight;
            _rootLayout.ResumeLayout(true);
        }
    }

    private async Task InitializeWebViewAsync()
    {
        try
        {
            await _webViewResult.EnsureCoreWebView2Async(null);
            _webViewResult.CoreWebView2InitializationCompleted += (s, e) =>
            {
                if (e.IsSuccess && _pendingHtmlNavigation != null)
                {
                    _webViewResult.NavigateToString(_pendingHtmlNavigation);
                    _pendingHtmlNavigation = null;
                }
            };
        }
        catch { }
    }

    // ═══════════════════════════════════════════════════════════
    //  Dynamic layout: expand/collapse result row
    // ═══════════════════════════════════════════════════════════

    private void ExpandForResult()
    {
        if (_resultShown) return;
        _resultShown = true;

        _rootLayout.SuspendLayout();
        _rootLayout.RowStyles[2].SizeType = SizeType.Percent;
        _rootLayout.RowStyles[2].Height = 40f;
        _rootLayout.RowStyles[5].SizeType = SizeType.Percent;
        _rootLayout.RowStyles[5].Height = 32f;
        _rootLayout.ResumeLayout(true);

        int targetH = Dpi(640);
        if (Height < targetH)
        {
            Screen screen = Screen.FromControl(this);
            int maxH = screen.WorkingArea.Height - 40;
            Height = Math.Min(targetH, maxH);
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  Screen Snip
    // ═══════════════════════════════════════════════════════════

    private async Task SnipScreenAsync(int delaySeconds)
    {
        if (_isProcessing) return;

        bool supportsImages = CheckModelSupportsImages?.Invoke() ?? true;
        if (!supportsImages)
        {
            _lblImageStatus.ForeColor = _warningText;
            _lblImageStatus.Text = "⚠ Selected model does not support images.";
            return;
        }

        _btnSnipNow.Enabled = false;
        _btnSnipDelay.Enabled = false;

        try
        {
            if (delaySeconds > 0)
            {
                _lblImageStatus.ForeColor = _dimText;
                for (int i = delaySeconds; i >= 1; i--)
                {
                    _lblImageStatus.Text = $"Snipping in {i}s...";
                    await Task.Delay(1000);
                }
            }

            _lblImageStatus.Text = "Snipping...";
            Visible = false;
            await Task.Delay(200);

            using ScreenSnipOverlayForm snipForm = new();
            DialogResult result = snipForm.ShowDialog();

            Visible = true;
            Activate();

            if (result != DialogResult.OK || snipForm.CapturedImage == null)
            {
                _lblImageStatus.ForeColor = _dimText;
                _lblImageStatus.Text = "Snip cancelled.";
                return;
            }

            ApplySnipImage(snipForm.CapturedImage);
        }
        finally
        {
            _btnSnipNow.Enabled = true;
            _btnSnipDelay.Enabled = true;
        }
    }

    private void ApplySnipImage(Bitmap bitmap)
    {
        using MemoryStream ms = new();
        bitmap.Save(ms, ImageFormat.Png);
        _snipImageBytes = ms.ToArray();
        _snipImageMimeType = "image/png";

        Image? old = _snipPreviewImage;
        _snipPreviewImage = new Bitmap(bitmap);
        _picSnipPreview.Image = _snipPreviewImage;
        old?.Dispose();

        _picSnipPreview.Visible = true;
        _btnClearImage.Visible = true;

        double kb = _snipImageBytes.Length / 1024d;
        _lblImageStatus.ForeColor = _successText;
        _lblImageStatus.Text = $"📷 {bitmap.Width}×{bitmap.Height} ({kb:F0} KB)";
    }

    private void ClearSnipImage()
    {
        _snipImageBytes = null;
        _snipImageMimeType = null;

        Image? old = _snipPreviewImage;
        _snipPreviewImage = null;
        _picSnipPreview.Image = null;
        old?.Dispose();

        _picSnipPreview.Visible = false;
        _btnClearImage.Visible = false;
        _lblImageStatus.ForeColor = _dimText;
        _lblImageStatus.Text = "Image cleared.";
    }

    private void ShowFullPreview()
    {
        if (_snipPreviewImage == null) return;

        using Form previewForm = new()
        {
            Text = "Snip Preview",
            Size = new Size(520, 400),
            StartPosition = FormStartPosition.CenterParent,
            TopMost = true,
            ShowIcon = false,
            BackColor = _bgColor
        };

        PictureBox pic = new()
        {
            Dock = DockStyle.Fill,
            Image = _snipPreviewImage,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = _bgColor
        };
        previewForm.Controls.Add(pic);
        previewForm.ShowDialog(this);
    }

    // ═══════════════════════════════════════════════════════════
    //  AI Result Display
    // ═══════════════════════════════════════════════════════════

    private static string NormalizeLineEndings(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");
    }

    public void ShowProcessing(string personaName)
    {
        _isProcessing = true;
        _btnRun.Enabled = false;
        _btnSnipNow.Enabled = false;
        _btnSnipDelay.Enabled = false;
        _lblResultHeader.ForeColor = _dimText;
        _lblResultHeader.Text = $"⏳ Sending to {personaName}...";
        _txtResult.Text = string.Empty;
        UpdateWebViewHtml("⏳ *Processing...*");

        ExpandForResult();
    }

    public void ShowResult(string aiText, string personaName, bool canReplace)
    {
        _isProcessing = false;
        AiResultText = aiText;

        _lblResultHeader.ForeColor = _accent;
        _lblResultHeader.Text = $"✓ {personaName} — AI Result";

        // Parse format to see if it's strictly a codeblock
        string rawText = NormalizeLineEndings(aiText).Trim();
        bool isStrictCodeBlock = false;
        if (rawText.StartsWith("```") && rawText.EndsWith("```"))
        {
            var lines = rawText.Split('\n', 2);
            string firstLine = lines[0].Trim();
            string language = firstLine.Substring(3).Trim();

            if (string.IsNullOrWhiteSpace(language))
                language = "code";

            string codeContent = lines.Length > 1 ? lines[1] : string.Empty;
            if (codeContent.EndsWith("```"))
            {
                codeContent = codeContent.Substring(0, codeContent.Length - 3).TrimEnd();
            }

            _isRawCodeMode = true;
            _lblRawCodeLanguage.Text = language.ToUpperInvariant();
            _txtResult.Text = codeContent;
            isStrictCodeBlock = true;
        }
        else
        {
            _isRawCodeMode = false;
            _txtResult.Text = rawText;
        }

        UpdateWebViewHtml(aiText);

        if (isStrictCodeBlock)
        {
            SelectResultTab(false); // Automatically switch to Raw
        }
        else
        {
            SelectResultTab(true); // Automatically switch to Preview
        }

        _btnReplace.Visible = canReplace;
        _btnRun.Enabled = true;
        _btnSnipNow.Enabled = true;
        _btnSnipDelay.Enabled = true;
    }

    public void ShowError(string errorMessage)
    {
        _isProcessing = false;
        _lblResultHeader.ForeColor = _warningText;
        _lblResultHeader.Text = "✕ Request Failed";
        _txtResult.Text = NormalizeLineEndings(errorMessage);
        UpdateWebViewHtml($"**Error:**\n```\n{errorMessage}\n```");

        _btnRun.Enabled = true;
        _btnSnipNow.Enabled = true;
        _btnSnipDelay.Enabled = true;

        ExpandForResult();
    }

    private void UpdateWebViewHtml(string markdown)
    {
        var pipeline = new Markdig.MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        string htmlBody = Markdig.Markdown.ToHtml(markdown ?? "", pipeline);

        string fullHtml = $@"<!DOCTYPE html>
<html>
<head>
<meta charset=""utf-8"">
<style>
    body {{
        background-color: #{_inputBg.R:X2}{_inputBg.G:X2}{_inputBg.B:X2};
        color: #{_textColor.R:X2}{_textColor.G:X2}{_textColor.B:X2};
        font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
        font-size: 13px;
        line-height: 1.5;
        padding: 8px;
        margin: 0;
    }}
    pre {{
        background-color: #{_panelBg.R:X2}{_panelBg.G:X2}{_panelBg.B:X2};
        border: 1px solid #{_border.R:X2}{_border.G:X2}{_border.B:X2};
        border-radius: 4px;
        padding: 10px;
        overflow-x: auto;
    }}
    code {{
        font-family: 'Cascadia Code', Consolas, monospace;
        background-color: #{_panelBg.R:X2}{_panelBg.G:X2}{_panelBg.B:X2};
        padding: 2px 4px;
        border-radius: 3px;
    }}
    pre code {{
        background-color: transparent;
        padding: 0;
    }}
    a {{ color: #007acc; text-decoration: none; }}
    a:hover {{ text-decoration: underline; }}
    blockquote {{
        border-left: 4px solid #{_border.R:X2}{_border.G:X2}{_border.B:X2};
        margin: 0;
        padding-left: 1em;
        color: #{_dimText.R:X2}{_dimText.G:X2}{_dimText.B:X2};
    }}
    table {{ border-collapse: collapse; width: 100%; }}
    th, td {{ border: 1px solid #{_border.R:X2}{_border.G:X2}{_border.B:X2}; padding: 6px 12px; }}
    th {{ background-color: #{_surfaceColor.R:X2}{_surfaceColor.G:X2}{_surfaceColor.B:X2}; }}
    ul, ol {{ margin-top: 0; margin-bottom: 10px; }}
    p {{ margin-top: 0; margin-bottom: 10px; }}
</style>
</head>
<body>
{htmlBody}
</body>
</html>";

        if (_webViewResult.CoreWebView2 != null)
        {
            _webViewResult.NavigateToString(fullHtml);
        }
        else
        {
            _pendingHtmlNavigation = fullHtml;
        }
    }

    private void ExecuteReplace()
    {
        if (SourceCaptureResult is not { Success: true, FocusInfo: not null })
            return;

        string aiText = _txtResult.Text;
        if (string.IsNullOrWhiteSpace(aiText))
            return;

        try
        {
            SelectionCaptureEngine.ReplaceSourceText(SourceCaptureResult, aiText);
            _lblResultHeader.ForeColor = _successText;
            _lblResultHeader.Text = "✓ Source text replaced.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                $"Failed to replace text: {ex.Message}",
                "Replace Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  Recent Persona Persistence
    // ═══════════════════════════════════════════════════════════

    private static string GetRecentsFilePath()
    {
        string folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BuddyAIDesktop");
        return Path.Combine(folder, RecentsFileName);
    }

    private static void LoadRecentIds()
    {
        if (_recentsLoaded) return;
        _recentsLoaded = true;

        try
        {
            string path = GetRecentsFilePath();
            if (!File.Exists(path)) return;
            string json = File.ReadAllText(path);
            _recentPersonaIds = JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch
        {
            _recentPersonaIds = [];
        }
    }

    private static void SaveRecentIds()
    {
        try
        {
            string path = GetRecentsFilePath();
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            string json = JsonSerializer.Serialize(_recentPersonaIds);
            File.WriteAllText(path, json);
        }
        catch
        {
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  Data Loading
    // ═══════════════════════════════════════════════════════════

    private void LoadPersonas()
    {
        try
        {
            _allPersonas = _personaService.LoadOrSeed();
        }
        catch
        {
            _allPersonas = [];
        }

        _allCategories = _allPersonas
            .Select(p => p.Category)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();

        PopulateCategories();
    }

    private void PopulateCategories()
    {
        _lstCategories.BeginUpdate();
        _lstCategories.Items.Clear();

        string filter = _txtSearch.Text.Trim();
        bool isSearching = !string.IsNullOrEmpty(filter);

        if (isSearching)
        {
            _lstCategories.Items.Add("🔍 Results");
            _lstCategories.SelectedIndex = 0;
            _lstCategories.EndUpdate();
            PopulateFilteredPersonas();
            return;
        }

        bool hasRecent = _recentPersonaIds.Count > 0
            && _allPersonas.Any(p => _recentPersonaIds.Contains(p.Id));
        if (hasRecent)
            _lstCategories.Items.Add(RecentCategoryLabel);

        bool hasFavorites = _allPersonas.Any(p => p.Favorite);
        if (hasFavorites)
            _lstCategories.Items.Add(FavoritesCategoryLabel);

        foreach (string cat in _allCategories)
            _lstCategories.Items.Add(cat);

        if (_lstCategories.Items.Count > 0)
            _lstCategories.SelectedIndex = 0;

        _lstCategories.EndUpdate();
    }

    private void PopulateFilteredPersonas()
    {
        _lstPersonas.BeginUpdate();
        _lstPersonas.Items.Clear();

        string filter = _txtSearch.Text.Trim();
        bool isSearching = !string.IsNullOrEmpty(filter);

        if (isSearching)
        {
            _filteredPersonas = _allPersonas
                .Where(p =>
                    p.PersonaName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    p.Category.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    p.SystemPrompt.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        else
        {
            string? selectedCategory = _lstCategories.SelectedItem as string;
            if (selectedCategory == null)
            {
                _filteredPersonas = [];
            }
            else if (selectedCategory == RecentCategoryLabel)
            {
                _filteredPersonas = _recentPersonaIds
                    .Select(id => _allPersonas.FirstOrDefault(p => p.Id == id))
                    .Where(p => p != null)
                    .Cast<PersonaRecord>()
                    .ToList();
            }
            else if (selectedCategory == FavoritesCategoryLabel)
            {
                _filteredPersonas = _allPersonas
                    .Where(p => p.Favorite)
                    .ToList();
            }
            else
            {
                _filteredPersonas = _allPersonas
                    .Where(p => string.Equals(p.Category, selectedCategory, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
        }

        foreach (PersonaRecord p in _filteredPersonas)
        {
            string display = isSearching
                ? $"{p.Icon} {p.Category}: {p.PersonaName}"
                : $"{p.Icon} {p.PersonaName}";
            _lstPersonas.Items.Add(display);
        }

        if (_lstPersonas.Items.Count > 0)
            _lstPersonas.SelectedIndex = 0;

        _lstPersonas.EndUpdate();
        UpdatePersonaDetails();
    }

    private void UpdatePersonaDetails()
    {
        int idx = _lstPersonas.SelectedIndex;
        if (idx < 0 || idx >= _filteredPersonas.Count)
        {
            _lblPersonaName.Text = "Select a Persona";
            _txtSystemPrompt.Text = string.Empty;
            _txtMessageTemplate.Text = string.Empty;
            _lblMessageTemplateHint.Visible = false;
            _txtMessageTemplate.Visible = false;
            SelectedPersona = null;
            return;
        }

        PersonaRecord persona = _filteredPersonas[idx];
        SelectedPersona = persona;
        _lblPersonaName.Text = $"{persona.Icon} {persona.PersonaName}";
        _txtSystemPrompt.Text = persona.SystemPrompt;

        bool hasTemplate = !string.IsNullOrWhiteSpace(persona.MessageTemplate);
        _lblMessageTemplateHint.Visible = hasTemplate;
        _txtMessageTemplate.Visible = hasTemplate;
        _txtMessageTemplate.Text = hasTemplate ? persona.MessageTemplate : string.Empty;
    }

    private void ExecuteRun()
    {
        if (SelectedPersona == null || _isProcessing)
            return;

        if (_snipImageBytes is { Length: > 0 })
        {
            bool supportsImages = CheckModelSupportsImages?.Invoke() ?? true;
            if (!supportsImages)
            {
                _lblImageStatus.ForeColor = _warningText;
                _lblImageStatus.Text = "⚠ Selected model does not support images. Clear the image or change model.";
                return;
            }
        }

        _recentPersonaIds.Remove(SelectedPersona.Id);
        _recentPersonaIds.Insert(0, SelectedPersona.Id);
        if (_recentPersonaIds.Count > MaxRecentCount)
            _recentPersonaIds.RemoveRange(MaxRecentCount, _recentPersonaIds.Count - MaxRecentCount);
        SaveRecentIds();

        if (OnRunRequested != null)
        {
            _ = OnRunRequested(this);
        }
        else
        {
            Close();
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  Keyboard Handling
    // ═══════════════════════════════════════════════════════════

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Escape:
                if (_isProcessing) return true;
                Close();
                return true;

            case Keys.Enter:
            case Keys.Control | Keys.Enter:
                if (_txtCustomAsk.Focused || _lstPersonas.Focused)
                {
                    ExecuteRun();
                    return true;
                }
                break;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Down:
                e.Handled = true;
                if (_lstPersonas.Items.Count > 0)
                {
                    _lstPersonas.Focus();
                    if (_lstPersonas.SelectedIndex < 0)
                        _lstPersonas.SelectedIndex = 0;
                    _currentPane = FocusPane.Personas;
                }
                else if (_lstCategories.Items.Count > 0)
                {
                    _lstCategories.Focus();
                    if (_lstCategories.SelectedIndex < 0)
                        _lstCategories.SelectedIndex = 0;
                    _currentPane = FocusPane.Categories;
                }
                break;

            case Keys.Enter:
                e.Handled = true;
                e.SuppressKeyPress = true;
                if (_lstPersonas.Items.Count > 0)
                {
                    _lstPersonas.Focus();
                    _lstPersonas.SelectedIndex = 0;
                    _currentPane = FocusPane.Personas;
                }
                break;
        }
    }

    private void OnCategoryKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Right:
            case Keys.Enter:
                e.Handled = true;
                if (_lstPersonas.Items.Count > 0)
                {
                    _lstPersonas.Focus();
                    if (_lstPersonas.SelectedIndex < 0)
                        _lstPersonas.SelectedIndex = 0;
                    _currentPane = FocusPane.Personas;
                }
                break;

            case Keys.Tab:
                e.Handled = true;
                _txtSearch.Focus();
                _currentPane = FocusPane.Search;
                break;
        }

        if (e.KeyCode >= Keys.A && e.KeyCode <= Keys.Z && !e.Control && !e.Alt)
        {
            _txtSearch.Focus();
            _currentPane = FocusPane.Search;
        }
    }

    private void OnPersonaKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Left:
                e.Handled = true;
                _lstCategories.Focus();
                _currentPane = FocusPane.Categories;
                break;

            case Keys.Right:
            case Keys.Tab:
                e.Handled = true;
                _txtCustomAsk.Focus();
                _currentPane = FocusPane.CustomAsk;
                break;

            case Keys.Enter:
                e.Handled = true;
                e.SuppressKeyPress = true;
                ExecuteRun();
                break;
        }

        if (e.KeyCode >= Keys.A && e.KeyCode <= Keys.Z && !e.Control && !e.Alt)
        {
            _txtSearch.Focus();
            _currentPane = FocusPane.Search;
        }
    }

    private void OnCustomAskKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Enter when e.Control:
                e.Handled = true;
                e.SuppressKeyPress = true;
                ExecuteRun();
                break;

            case Keys.Left when _txtCustomAsk.SelectionStart == 0 && _txtCustomAsk.Text.Length == 0:
                e.Handled = true;
                _lstPersonas.Focus();
                _currentPane = FocusPane.Personas;
                break;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  Event Handlers
    // ═══════════════════════════════════════════════════════════

    private void SetActivePane(FocusPane pane)
    {
        if (_currentPane == pane) return;
        _currentPane = pane;
        _lstCategories.Invalidate();
        _lstPersonas.Invalidate();
    }

    private void OnShown(object? sender, EventArgs e)
    {
        LoadPersonas();
        _lstCategories.Focus();
        _currentPane = FocusPane.Categories;
    }

    private void OnSearchTextChanged(object? sender, EventArgs e) => PopulateCategories();
    private void OnCategorySelected(object? sender, EventArgs e) => PopulateFilteredPersonas();
    private void OnPersonaSelected(object? sender, EventArgs e) => UpdatePersonaDetails();

    // ═══════════════════════════════════════════════════════════
    //  Owner-Draw for Lists
    // ═══════════════════════════════════════════════════════════

    private void OnCategoryDrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;

        bool selected = (e.State & DrawItemState.Selected) != 0;
        bool isActivePane = _currentPane == FocusPane.Categories;
        Color bg = selected ? _selectionBg : _panelBg;
        Color fg = selected ? (isActivePane ? _accent : _dimText) : _textColor;

        using SolidBrush bgBrush = new(bg);
        e.Graphics.FillRectangle(bgBrush, e.Bounds);

        string text = _lstCategories.Items[e.Index]?.ToString() ?? string.Empty;
        TextRenderer.DrawText(e.Graphics, text, e.Font, e.Bounds,
            fg, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

        if (selected)
        {
            using Pen accentPen = new(isActivePane ? _accent : _border, isActivePane ? 2 : 1);
            e.Graphics.DrawLine(accentPen, e.Bounds.Left, e.Bounds.Top, e.Bounds.Left, e.Bounds.Bottom);

            using Pen bottomPen = new(_accent, isActivePane ? 2 : 1);
            int y = e.Bounds.Bottom - 1;
            e.Graphics.DrawLine(bottomPen, e.Bounds.Left + 2, y, e.Bounds.Right - 2, y);
        }
    }

    private void OnPersonaDrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;

        bool selected = (e.State & DrawItemState.Selected) != 0;
        bool isActivePane = _currentPane == FocusPane.Personas;
        Color bg = selected ? _selectionBg : _panelBg;
        Color fg = selected ? (isActivePane ? _textColor : _dimText) : _mutedText;

        using SolidBrush bgBrush = new(bg);
        e.Graphics.FillRectangle(bgBrush, e.Bounds);

        string text = _lstPersonas.Items[e.Index]?.ToString() ?? string.Empty;
        TextRenderer.DrawText(e.Graphics, text, e.Font,
            new Rectangle(e.Bounds.X + 4, e.Bounds.Y, e.Bounds.Width - 4, e.Bounds.Height),
            fg, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

        if (selected)
        {
            using Pen pen = new(isActivePane ? _accent : _border, isActivePane ? 2 : 1);
            e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Top, e.Bounds.Left, e.Bounds.Bottom);

            if (isActivePane)
            {
                int y = e.Bounds.Bottom - 1;
                e.Graphics.DrawLine(pen, e.Bounds.Left + 2, y, e.Bounds.Right - 2, y);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  Public API
    // ═══════════════════════════════════════════════════════════

    public void SetContextText(string text)
    {
        string normalized = NormalizeLineEndings(text ?? string.Empty);
        if (!IsHandleCreated)
        {
            // Layout not built yet — buffer for OnLoad
            _pendingContextText = normalized;
        }
        else
        {
            _txtContext.Text = normalized;
        }
    }

    public void SetCaptureResult(CaptureResult? result)
    {
        SourceCaptureResult = result;
    }

    // ═══════════════════════════════════════════════════════════
    //  Window Dragging + Visual Theme / DWM
    // ═══════════════════════════════════════════════════════════

    private void AttachDragHandlers(Control control)
    {
        if (control is not TextBox && control is not ListBox && control is not Button && control is not PictureBox && control is not WebView2)
        {
            control.MouseDown += Global_MouseDown;
            control.MouseMove += Global_MouseMove;
            control.MouseUp += Global_MouseUp;
        }

        foreach (Control child in control.Controls)
        {
            AttachDragHandlers(child);
        }
    }

    private void Global_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && GetResizeDirection(PointToClient(Cursor.Position)) == 0)
        {
            _isDragging = true;
            _dragStart = Cursor.Position;
            if (sender is Control c) c.Capture = true;
        }
    }

    private void Global_MouseMove(object? sender, MouseEventArgs e)
    {
        if (_isDragging)
        {
            Point current = Cursor.Position;
            Location = new Point(
                Location.X + current.X - _dragStart.X,
                Location.Y + current.Y - _dragStart.Y);
            _dragStart = current;
        }
    }

    private void Global_MouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _isDragging = false;
            if (sender is Control c) c.Capture = false;
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_NCHITTEST)
        {
            base.WndProc(ref m);
            int lParam = (int)m.LParam;
            Point cursor = PointToClient(new Point(lParam & 0xFFFF, (lParam >> 16) & 0xFFFF));
            int hit = GetResizeDirection(cursor);
            if (hit != 0)
            {
                m.Result = (IntPtr)hit;
                return;
            }
            return;
        }

        base.WndProc(ref m);
    }

    private int GetResizeDirection(Point clientPoint)
    {
        bool top = clientPoint.Y < ResizeGripSize;
        bool bottom = clientPoint.Y >= ClientSize.Height - ResizeGripSize;
        bool left = clientPoint.X < ResizeGripSize;
        bool right = clientPoint.X >= ClientSize.Width - ResizeGripSize;

        if (top && left) return HTTOPLEFT;
        if (top && right) return HTTOPRIGHT;
        if (bottom && left) return HTBOTTOMLEFT;
        if (bottom && right) return HTBOTTOMRIGHT;
        if (top) return HTTOP;
        if (bottom) return HTBOTTOM;
        if (left) return HTLEFT;
        if (right) return HTRIGHT;
        return 0;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyGlassEffect();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        using SolidBrush brush = new(_bgColor);
        e.Graphics.FillRectangle(brush, ClientRectangle);

        using Pen pen = new(_subtleBorder);
        e.Graphics.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        // Do not auto-close so the user can interact with AIQ main window
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _snipPreviewImage?.Dispose();
            _snipPreviewImage = null;
            _snipDelayMenu.Dispose();
        }
        base.Dispose(disposing);
    }

    private void ApplyGlassEffect()
    {
        // Completely bypass DWM acrylic/mica/glass transparency on this window 
        // to prevent fatal hardware acceleration conflicts with WebView2 under .NET 8.
    }

    private void StyleButton(Button btn, string text, bool isAccent)
    {
        btn.Text = text;
        btn.Width = Dpi(64);
        btn.Height = Dpi(24);
        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderSize = 1;
        btn.Font = new Font("Segoe UI", 8f, FontStyle.Regular);
        btn.Cursor = Cursors.Hand;
        btn.Margin = new Padding(Dpi(4), 0, 0, 0);
        btn.TabStop = false;

        if (isAccent)
        {
            btn.BackColor = _accent;
            btn.ForeColor = Color.White;
            btn.FlatAppearance.BorderColor = _accent;
            btn.FlatAppearance.MouseOverBackColor = _accentHover;
            btn.FlatAppearance.MouseDownBackColor = _accentPress;
        }
        else
        {
            btn.BackColor = _surfaceColor;
            btn.ForeColor = _textColor;
            btn.FlatAppearance.BorderColor = _border;
            btn.FlatAppearance.MouseOverBackColor = _hoverBg;
            btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(70, 70, 78);
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  Dark context menu color table
    // ═══════════════════════════════════════════════════════════

    private sealed class DarkMenuColorTable : ProfessionalColorTable
    {
        public override Color MenuItemSelected => Color.FromArgb(255, 120, 120, 120);
        public override Color MenuItemBorder => Color.FromArgb(255, 140, 140, 140);
        public override Color MenuBorder => Color.FromArgb(255, 115, 115, 115);
        public override Color ToolStripDropDownBackground => Color.FromArgb(255, 95, 95, 95);
        public override Color ImageMarginGradientBegin => Color.FromArgb(255, 95, 95, 95);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(255, 95, 95, 95);
        public override Color ImageMarginGradientEnd => Color.FromArgb(255, 95, 95, 95);
        public override Color SeparatorDark => Color.FromArgb(255, 115, 115, 115);
        public override Color SeparatorLight => Color.FromArgb(255, 115, 115, 115);
    }

    // ═══════════════════════════════════════════════════════════
    //  Embedded Screen Snip Overlay (same as AIQ.ScreenSnip)
    // ═══════════════════════════════════════════════════════════

    private sealed class ScreenSnipOverlayForm : Form
    {
        private Bitmap? _desktopBitmap;
        private Bitmap? _dimmedBitmap;
        private Rectangle _virtualBounds;
        private Rectangle _selection;
        private bool _isDragging;
        private Point _dragStart;

        public Bitmap? CapturedImage { get; private set; }

        public ScreenSnipOverlayForm()
        {
            DoubleBuffered = true;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            KeyPreview = true;
            Cursor = Cursors.Cross;

            BuildDesktopSnapshot();

            Bounds = _virtualBounds;
            BackColor = Color.Black;

            MouseDown += OnOverlayMouseDown;
            MouseMove += OnOverlayMouseMove;
            MouseUp += OnOverlayMouseUp;
            KeyDown += OnOverlayKeyDown;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Focus();
            Activate();
        }

        private void BuildDesktopSnapshot()
        {
            _virtualBounds = GetVirtualScreenBounds();

            _desktopBitmap = new Bitmap(_virtualBounds.Width, _virtualBounds.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(_desktopBitmap))
            {
                g.CopyFromScreen(_virtualBounds.Location, Point.Empty, _virtualBounds.Size);
            }

            _dimmedBitmap = new Bitmap(_desktopBitmap.Width, _desktopBitmap.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(_dimmedBitmap))
            {
                g.DrawImageUnscaled(_desktopBitmap, 0, 0);
                using SolidBrush brush = new(Color.FromArgb(110, Color.SkyBlue));
                g.FillRectangle(brush, 0, 0, _dimmedBitmap.Width, _dimmedBitmap.Height);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (_dimmedBitmap != null)
                e.Graphics.DrawImageUnscaled(_dimmedBitmap, 0, 0);

            if (_selection.Width > 0 && _selection.Height > 0 && _desktopBitmap != null)
            {
                e.Graphics.SetClip(_selection);
                e.Graphics.DrawImageUnscaled(_desktopBitmap, 0, 0);
                e.Graphics.ResetClip();

                using Pen borderPen = new(Color.Red, 3f);
                e.Graphics.DrawRectangle(borderPen, _selection);

                using SolidBrush infoBrush = new(Color.FromArgb(220, 30, 30, 30));
                string label = _selection.Width + " x " + _selection.Height;
                Size labelSize = TextRenderer.MeasureText(label, Font);
                Rectangle labelRect = new(_selection.X, Math.Max(0, _selection.Y - labelSize.Height - 6), labelSize.Width + 10, labelSize.Height + 4);
                e.Graphics.FillRectangle(infoBrush, labelRect);
                TextRenderer.DrawText(e.Graphics, label, Font, labelRect, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            string hint = "Drag to capture a region. Press Esc to cancel.";
            Rectangle hintRect = new(20, 20, 420, 28);
            using SolidBrush hintBrush = new(Color.FromArgb(180, 20, 20, 20));
            e.Graphics.FillRectangle(hintBrush, hintRect);
            TextRenderer.DrawText(e.Graphics, hint, Font, hintRect, Color.White, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.LeftAndRightPadding);
        }

        private void OnOverlayMouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            _isDragging = true;
            _dragStart = e.Location;
            _selection = new Rectangle(e.Location, Size.Empty);
            Invalidate();
        }

        private void OnOverlayMouseMove(object? sender, MouseEventArgs e)
        {
            if (!_isDragging) return;
            _selection = NormalizeRectangle(_dragStart, e.Location);
            Invalidate();
        }

        private void OnOverlayMouseUp(object? sender, MouseEventArgs e)
        {
            if (!_isDragging) return;
            _isDragging = false;
            _selection = NormalizeRectangle(_dragStart, e.Location);
            if (_selection.Width < 2 || _selection.Height < 2)
            {
                _selection = Rectangle.Empty;
                Invalidate();
                return;
            }
            CaptureSelectionAndClose();
        }

        private void OnOverlayKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();
            }
        }

        private void CaptureSelectionAndClose()
        {
            if (_desktopBitmap == null || _selection.Width <= 0 || _selection.Height <= 0)
            {
                DialogResult = DialogResult.Cancel;
                Close();
                return;
            }

            Bitmap captured = new(_selection.Width, _selection.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(captured))
            {
                g.DrawImage(_desktopBitmap, new Rectangle(0, 0, captured.Width, captured.Height), _selection, GraphicsUnit.Pixel);
            }

            CapturedImage = captured;
            DialogResult = DialogResult.OK;
            Close();
        }

        private static Rectangle GetVirtualScreenBounds()
        {
            int left = Screen.AllScreens.Min(s => s.Bounds.Left);
            int top = Screen.AllScreens.Min(s => s.Bounds.Top);
            int right = Screen.AllScreens.Max(s => s.Bounds.Right);
            int bottom = Screen.AllScreens.Max(s => s.Bounds.Bottom);
            return Rectangle.FromLTRB(left, top, right, bottom);
        }

        private static Rectangle NormalizeRectangle(Point p1, Point p2)
        {
            int x = Math.Min(p1.X, p2.X);
            int y = Math.Min(p1.Y, p2.Y);
            int width = Math.Abs(p2.X - p1.X);
            int height = Math.Abs(p2.Y - p1.Y);
            return new Rectangle(x, y, width, height);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _desktopBitmap?.Dispose();
                _desktopBitmap = null;
                _dimmedBitmap?.Dispose();
                _dimmedBitmap = null;
            }
            base.Dispose(disposing);
        }
    }
}
