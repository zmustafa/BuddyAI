using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using BuddyAI.Models;
using BuddyAI.Services;
using Microsoft.Web.WebView2.WinForms;

namespace BuddyAI.Forms;

public sealed class ProviderImportWizardForm : Form
{
    private readonly Panel _contentPanel = new();
    private readonly Panel _footerPanel = new();
    private readonly Panel _sidePanel = new();
    private readonly Button _btnBack = new();
    private readonly Button _btnNext = new();
    private readonly Button _btnCancel = new();
    private readonly Label _lblStepTitle = new();
    private readonly Label _lblStepDescription = new();

    private int _currentStep;
    private string _selectedCategory = "apikey"; // "oauth", "apikey", "local"
    private string? _selectedProviderType = AiProviderTypes.ChatGPTOAuth;
    private string? _importedApiKey;
    private string? _importedProviderId;
    private string? _importedProviderName;

    // Step 1 (Tip) controls
    private RadioButton? _rdoOAuth;
    private RadioButton? _rdoApiKey;
    private RadioButton? _rdoLocal;

    // Step 2 controls
    private readonly ListView _lstProviders = new();
    private readonly ImageList _providerImageList = new();

    // Step 3 controls (API key)
    private WebView2? _webView;
    private TextBox? _txtApiKey;
    private Button? _btnPasteKey;
    private Label? _lblKeyInstructions;
    private Panel? _apiKeyTopPanel;

    // Step 4 controls (summary)
    private Label? _lblSummary;
    private TextBox? _txtProviderName;

    private const int StepWelcome = 0;
    private const int StepTip = 1;
    private const int StepSelectProvider = 2;
    private const int StepApiKey = 3;
    private const int StepSummary = 4;
    private const int TotalSteps = 5;

    public AiProviderDefinition? ImportedProvider { get; private set; }

    public ProviderImportWizardForm()
    {
        Text = "Setup AI Connection";
        Size = new Size(820, 620);
        MinimumSize = new Size(720, 520);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false;
        MinimizeBox = false;
        KeyPreview = true;

        BuildLayout();
        ShowStep(StepWelcome);

        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _providerImageList.Dispose();
            _webView?.Dispose();
        }
        base.Dispose(disposing);
    }

    private void BuildLayout()
    {
        // Side panel (step indicator)
        _sidePanel.Dock = DockStyle.Left;
        _sidePanel.Width = 170;
        _sidePanel.BackColor = Color.FromArgb(40, 55, 85);
        _sidePanel.Paint += SidePanel_Paint;

        // Footer panel with buttons
        _footerPanel.Dock = DockStyle.Bottom;
        _footerPanel.Height = 55;
        _footerPanel.Padding = new Padding(10, 8, 10, 8);

        // Separator line above footer
        Panel separator = new() { Dock = DockStyle.Bottom, Height = 1, BackColor = SystemColors.ControlDark };

        // Main content area
        Panel mainArea = new() { Dock = DockStyle.Fill, Padding = new Padding(20, 15, 20, 10) };

        // Step title
        _lblStepTitle.Dock = DockStyle.Top;
        _lblStepTitle.Height = 32;
        _lblStepTitle.Font = new Font(Font.FontFamily, 14f, FontStyle.Bold);
        _lblStepTitle.TextAlign = ContentAlignment.BottomLeft;

        // Step description
        _lblStepDescription.Dock = DockStyle.Top;
        _lblStepDescription.Height = 28;
        _lblStepDescription.ForeColor = SystemColors.GrayText;
        _lblStepDescription.TextAlign = ContentAlignment.TopLeft;
        _lblStepDescription.Padding = new Padding(0, 4, 0, 0);

        // Content panel
        _contentPanel.Dock = DockStyle.Fill;
        _contentPanel.Padding = new Padding(0, 10, 0, 0);

        // Add children to mainArea — Fill control must be added first (processed last by dock layout)
        mainArea.Controls.Add(_contentPanel);
        mainArea.Controls.Add(_lblStepDescription);
        mainArea.Controls.Add(_lblStepTitle);

        // Add to form — Fill control first, then edge-docked controls
        // WinForms processes dock in reverse order: last added docks first
        Controls.Add(mainArea);
        Controls.Add(separator);
        Controls.Add(_footerPanel);
        Controls.Add(_sidePanel);

        // Footer buttons
        _btnCancel.Text = "Cancel";
        _btnCancel.Width = 80;
        _btnCancel.Height = 32;
        _btnCancel.Dock = DockStyle.Right;
        _btnCancel.Click += (_, _) => Close();
        _footerPanel.Controls.Add(_btnCancel);

        Panel spacer1 = new() { Dock = DockStyle.Right, Width = 8 };
        _footerPanel.Controls.Add(spacer1);

        _btnBack.Text = "< Back";
        _btnBack.Width = 90;
        _btnBack.Height = 32;
        _btnBack.Dock = DockStyle.Right;
        _btnBack.Click += (_, _) => GoBack();
        _footerPanel.Controls.Add(_btnBack);
        
        Panel spacer2 = new() { Dock = DockStyle.Right, Width = 8 };
        _footerPanel.Controls.Add(spacer2);

        _btnNext.Text = "Next >";
        _btnNext.Width = 90;
        _btnNext.Height = 32;
        _btnNext.Dock = DockStyle.Right;
        _btnNext.Click += (_, _) => GoNext();
        _footerPanel.Controls.Add(_btnNext);

     
    }

    private void SidePanel_Paint(object? sender, PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        string[] stepNames = ["Welcome", "Type", "Provider", "Configure", "Finish"];

        int y = 40;
        using Font stepFont = new(Font.FontFamily, 9.5f, FontStyle.Regular);
        using Font activeFont = new(Font.FontFamily, 9.5f, FontStyle.Bold);

        for (int i = 0; i < stepNames.Length; i++)
        {
            bool isActive = i == _currentStep;
            bool isCompleted = i < _currentStep;

            // Circle
            int circleX = 20;
            int circleY = y + 2;
            int circleSize = 22;

            if (isCompleted)
            {
                using SolidBrush completedBrush = new(Color.FromArgb(80, 180, 80));
                g.FillEllipse(completedBrush, circleX, circleY, circleSize, circleSize);
                using Font checkFont = new("Segoe UI", 10f, FontStyle.Bold);
                TextRenderer.DrawText(g, "?", checkFont,
                    new Rectangle(circleX, circleY, circleSize, circleSize),
                    Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            else if (isActive)
            {
                using SolidBrush activeBrush = new(Color.FromArgb(80, 140, 220));
                g.FillEllipse(activeBrush, circleX, circleY, circleSize, circleSize);
                TextRenderer.DrawText(g, (i + 1).ToString(), stepFont,
                    new Rectangle(circleX, circleY, circleSize, circleSize),
                    Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            else
            {
                using Pen outlinePen = new(Color.FromArgb(120, 140, 170), 1.5f);
                g.DrawEllipse(outlinePen, circleX, circleY, circleSize, circleSize);
                TextRenderer.DrawText(g, (i + 1).ToString(), stepFont,
                    new Rectangle(circleX, circleY, circleSize, circleSize),
                    Color.FromArgb(120, 140, 170), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            // Connecting line to next step
            if (i < stepNames.Length - 1)
            {
                int lineX = circleX + circleSize / 2;
                int lineTop = circleY + circleSize + 2;
                int lineBottom = y + 52;
                using Pen linePen = new(isCompleted ? Color.FromArgb(80, 180, 80) : Color.FromArgb(80, 100, 130), 1.5f);
                g.DrawLine(linePen, lineX, lineTop, lineX, lineBottom);
            }

            // Step label
            Color textColor = isActive ? Color.White : Color.FromArgb(160, 175, 200);
            Font textFont = isActive ? activeFont : stepFont;
            TextRenderer.DrawText(g, stepNames[i], textFont,
                new Point(circleX + circleSize + 12, circleY + 2), textColor);

            y += 55;
        }

        // App title at bottom
        using Font titleFont = new(Font.FontFamily, 9f, FontStyle.Italic);
        TextRenderer.DrawText(g, "BuddyAI Setup",
            titleFont, new Point(15, _sidePanel.Height - 35),
            Color.FromArgb(100, 120, 155));
    }

    private void ShowStep(int step)
    {
        _currentStep = step;
        _contentPanel.Controls.Clear();
        _contentPanel.SuspendLayout();

        switch (step)
        {
            case StepWelcome:
                BuildWelcomePage();
                break;
            case StepTip:
                BuildTipPage();
                break;
            case StepSelectProvider:
                BuildSelectProviderPage();
                break;
            case StepApiKey:
                BuildApiKeyPage();
                break;
            case StepSummary:
                BuildSummaryPage();
                break;
        }

        _contentPanel.ResumeLayout(true);

        _btnBack.Enabled = step > StepWelcome;
        _btnNext.Text = step == StepSummary ? "Finish" : "Next >";
        _btnNext.Enabled = true;

        _sidePanel.Invalidate();
    }

    private void GoNext()
    {
        if (_currentStep == StepTip)
        {
            // Apply category selection from radio buttons before advancing
            if (_rdoOAuth?.Checked == true) _selectedCategory = "oauth";
            else if (_rdoLocal?.Checked == true) _selectedCategory = "local";
            else _selectedCategory = "apikey";

            // Reset provider selection when category changes
            _selectedProviderType = null;
        }

        if (_currentStep == StepSelectProvider && _selectedProviderType == null)
        {
            MessageBox.Show(this, "Please select a provider to import.", Text,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_currentStep == StepApiKey)
        {
            if (_selectedProviderType == AiProviderTypes.ChatGPTOAuth ||
                _selectedProviderType == AiProviderTypes.ClaudeOAuth)
            {
                // OAuth handled via sub-dialog; check we got an ID
                if (string.IsNullOrWhiteSpace(_importedProviderId))
                {
                    MessageBox.Show(this, "Please complete the OAuth authentication first.", Text,
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }
            else
            {
                string key = _txtApiKey?.Text.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(key))
                {
                    MessageBox.Show(this, "Please enter an API key to continue.", Text,
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                _importedApiKey = key;
            }
        }

        if (_currentStep == StepSummary)
        {
            FinishImport();
            return;
        }

        if (_currentStep < TotalSteps - 1)
            ShowStep(_currentStep + 1);
    }

    private void GoBack()
    {
        if (_currentStep > StepWelcome)
        {
            // Clean up WebView when leaving the API key step
            if (_currentStep == StepApiKey)
                DisposeWebView();

            ShowStep(_currentStep - 1);
        }
    }

    // ?? Step 1: Tip / Provider Category ????????????????????????????????????

    private void BuildTipPage()
    {
        _lblStepTitle.Text = "Choose type of connection";
        _lblStepDescription.Text = "Select the type of AI provider that best suits your needs.";

        // A single GroupBox (no border/text) acts as the mutual-exclusion container
        // for all three radio buttons so only one can be selected at a time.
        GroupBox radioGroup = new()
        {
            Dock = DockStyle.Fill,
            Text = string.Empty,
            FlatStyle = FlatStyle.Flat,
            Padding = new Padding(0)
        };
        // Remove the GroupBox border drawn by WinForms
        radioGroup.Paint += (_, e) => ControlPaint.DrawBorder(e.Graphics,
            radioGroup.ClientRectangle, Color.Transparent, ButtonBorderStyle.None);

        // Use a FlowLayoutPanel inside the group so cards stack with consistent gaps
        FlowLayoutPanel flow = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(0, 6, 0, 0)
        };

        // --- OAuth card ---
        _rdoOAuth = new RadioButton
        {
            Text = "Connect your ChatGPT or Claude Account (Quick Setup)",
            Font = new Font(Font.FontFamily, 10f, FontStyle.Bold),
            AutoSize = true,
            Height = 24,
            Checked = _selectedCategory == "oauth"
        };

        Label lblOAuthDesc = new()
        {
            Text = "Use your existing account to sign in\u2014no API keys required.\r\n" +
                   "  \u2022  Works with ChatGPT and Claude\r\n" +
                   "  \u2022  Secure browser-based authentication\r\n" +
                   "  \u2022  Best for quick setup and enterprise SSO",
            Padding = new Padding(22, 2, 0, 0),
            ForeColor = SystemColors.GrayText
        };

        // --- API Key card ---
        _rdoApiKey = new RadioButton
        {
            Text = "Using API Keys (Recommended for Control && Automation)",
            Font = new Font(Font.FontFamily, 10f, FontStyle.Bold),
            AutoSize = true,
            Height = 24,
            Checked = _selectedCategory == "apikey"
        };

        Label lblApiKeyDesc = new()
        {
            Text = "Use API keys to directly access frontier models.\r\n" +
                   "  \u2022  OpenAI  \u2022  Anthropic  \u2022  Google Gemini  \u2022  xAI Grok  \u2022  Mistral AI\r\n" +
                   "  \u2022  Full API control  \u2022  Cost tracking and governance",
            Padding = new Padding(22, 2, 0, 0),
            ForeColor = SystemColors.GrayText
        };

        // --- Local LLM card ---
        _rdoLocal = new RadioButton
        {
            Text = "Local LLM  (Recommended for Privacy && Offline Use)",
            Font = new Font(Font.FontFamily, 10f, FontStyle.Bold),
            AutoSize = true,
            Height = 24,
            Checked = _selectedCategory == "local"
        };

        Label lblLocalDesc = new()
        {
            Text = "Run models locally on your machine or network.\r\n" +
                   "  \u2022  Supports Ollama  && LM Studio\r\n" +
                   "  \u2022  No data leaves your environment\r\n" +
                   "  \u2022  Ideal for sensitive workloads and experimentation",
            Padding = new Padding(22, 2, 0, 0),
            ForeColor = SystemColors.GrayText
        };

        // Wire up click on description label to select the corresponding radio
        lblOAuthDesc.Click   += (_, _) => _rdoOAuth.Checked   = true;
        lblApiKeyDesc.Click  += (_, _) => _rdoApiKey.Checked  = true;
        lblLocalDesc.Click   += (_, _) => _rdoLocal.Checked   = true;

        // Each radio lives in its own Panel card, so WinForms cannot auto-group them.
        // Enforce mutual exclusion manually: checking one unchecks the other two.
        _rdoOAuth.CheckedChanged += (_, _) =>
        {
            if (_rdoOAuth.Checked) { _rdoApiKey.Checked = false; _rdoLocal.Checked = false; }
        };
        _rdoApiKey.CheckedChanged += (_, _) =>
        {
            if (_rdoApiKey.Checked) { _rdoOAuth.Checked = false; _rdoLocal.Checked = false; }
        };
        _rdoLocal.CheckedChanged += (_, _) =>
        {
            if (_rdoLocal.Checked) { _rdoOAuth.Checked = false; _rdoApiKey.Checked = false; }
        };

        flow.Controls.Add(BuildCategoryCard(_rdoOAuth,   lblOAuthDesc,   flow));
        flow.Controls.Add(BuildCategoryCard(_rdoApiKey,  lblApiKeyDesc,  flow));
        flow.Controls.Add(BuildCategoryCard(_rdoLocal,   lblLocalDesc,   flow));

        radioGroup.Controls.Add(flow);
        _contentPanel.Controls.Add(radioGroup);
        _rdoOAuth.Checked = true;
    }

    private static Panel BuildCategoryCard(RadioButton radio, Label description, FlowLayoutPanel owner)
    {
        const int cardPadH = 12;
        const int cardPadV = 10;
        const int cardGap  = 8;

        // Description: auto-size so the card grows to fit all text
        description.AutoSize = true;
        description.MaximumSize = new Size(1, 0); // width set below after layout

        Panel card = new()
        {
            Padding = new Padding(cardPadH, cardPadV, cardPadH, cardPadV),
            Margin  = new Padding(0, 0, 0, cardGap)
        };

        // Stack radio (top) then description
        radio.Dock = DockStyle.Top;
        description.Dock = DockStyle.Top;

        // Add in reverse dock order: description first so radio docks on top of it
        card.Controls.Add(description);
        card.Controls.Add(radio);

        // Highlight border when selected
        card.Paint += (_, e) =>
        {
            Rectangle rect = new(0, 0, card.Width - 1, card.Height - 1);
            using Pen border = new(radio.Checked
                ? Color.FromArgb(80, 140, 220)
                : Color.FromArgb(210, 215, 220), radio.Checked ? 2f : 1f);
            e.Graphics.DrawRectangle(border, rect);
        };
        radio.CheckedChanged += (_, _) => card.Invalidate();

        // Resize card whenever the FlowLayoutPanel (owner) changes width
        void SizeCard()
        {
            int w = owner.ClientSize.Width - owner.Padding.Horizontal - card.Margin.Horizontal;
            if (w < 100) return;
            card.Width = w;
            int labelW = w - cardPadH * 2 - 4;
            description.MaximumSize = new Size(labelW, 0);
            description.Width       = labelW;
            radio.Width             = labelW;
            // Height = top-padding + radio + description (auto-sized) + bottom-padding
            card.Height = cardPadV + radio.Height + description.PreferredSize.Height + cardPadV;
        }

        owner.SizeChanged   += (_, _) => SizeCard();
        card.VisibleChanged += (_, _) => SizeCard();
        owner.VisibleChanged += (_, _) => SizeCard();

        return card;
    }

    // ?? Step 0: Welcome ??????????????????????????????????????????

    private void BuildWelcomePage()
    {
        _lblStepTitle.Text = "Welcome";
        _lblStepDescription.Text = "AI Connection Setup Wizard";

        Label lblWelcome = new()
        {
            Dock = DockStyle.Fill,
            Text = "This wizard will guide you through adding a new AI connection to BuddyAI.\r\n\r\n" +
                   "You will be able to:\r\n\r\n" +
                   "  •  Choose from supported AI providers (OpenAI, Claude, Gemini, GROK, Mistral, and more)\r\n\r\n" +
                   "  •  Obtain or paste your API key\r\n\r\n" +
                   "  •  Review and confirm the provider configuration\r\n\r\n" +
                   "Click Next to get started.",
            TextAlign = ContentAlignment.TopLeft,
            Padding = new Padding(5, 15, 5, 5)
        };
        _contentPanel.Controls.Add(lblWelcome);
    }

    // ?? Step 1: Select Provider ??????????????????????????????????

    private void BuildSelectProviderPage()
    {
        _lblStepTitle.Text = "Select Provider";
        _lblStepDescription.Text = "Choose the AI provider you want to import.";

        InitializeProviderImageList();

        _lstProviders.View = View.LargeIcon;
        _lstProviders.Dock = DockStyle.Fill;
        _lstProviders.LargeImageList = _providerImageList;
        _lstProviders.MultiSelect = false;
        _lstProviders.HideSelection = false;

        _lstProviders.Items.Clear();
        if (_selectedCategory == "oauth")
        {
            AddProviderItem(AiProviderTypes.ChatGPTOAuth, "ChatGPT OAuth", "", 0);
            AddProviderItem(AiProviderTypes.ClaudeOAuth, "Claude OAuth", "", 1);
        }
        else if (_selectedCategory == "local")
        {
            AddProviderItem(AiProviderTypes.Ollama, "Ollama (Local)", "", 8);
            AddProviderItem(AiProviderTypes.LMStudio, "LM Studio (Local)", "", 9);
        }
        else // apikey
        {
            AddProviderItem(AiProviderTypes.OpenAI, "OpenAI API", "", 2);
            AddProviderItem(AiProviderTypes.Claude, "Claude API", "", 4);
            AddProviderItem(AiProviderTypes.GoogleGemini, "Gemini API", "", 5);
            AddProviderItem(AiProviderTypes.Grok, "GROK API", "", 3);
            AddProviderItem(AiProviderTypes.Mistral, "Mistral API", "", 6);
            AddProviderItem(AiProviderTypes.AzureOpenAI, "Azure OpenAI API", "", 7);
        }

        // Pre-select if already chosen
        if (_selectedProviderType != null)
        {
            foreach (ListViewItem item in _lstProviders.Items)
            {
                if (item.Tag is string tag && tag == _selectedProviderType)
                {
                    item.Selected = true;
                    item.Focused = true;
                    break;
                }
            }
        }

        _lstProviders.SelectedIndexChanged += (_, _) =>
        {
            if (_lstProviders.SelectedItems.Count > 0 && _lstProviders.SelectedItems[0].Tag is string providerType)
                _selectedProviderType = providerType;
        };

        _contentPanel.Controls.Add(_lstProviders);
    }

    private void AddProviderItem(string providerType, string displayName, string description, int imageIndex)
    {
        ListViewItem item = new(displayName + "\n" + description)
        {
            Tag = providerType,
            ImageIndex = imageIndex
        };
        _lstProviders.Items.Add(item);
    }

    private void InitializeProviderImageList()
    {
        _providerImageList.Images.Clear();
        _providerImageList.ImageSize = new Size(40, 40);
        _providerImageList.ColorDepth = ColorDepth.Depth32Bit;

        // Generate simple colored icons for each provider
        Color[] colors =
        [
            Color.FromArgb(16, 163, 127),   // OpenAI - green
            Color.FromArgb(30, 30, 30),      // GROK - dark
            Color.FromArgb(204, 122, 65),    // Claude - orange
            Color.FromArgb(66, 133, 244),    // Gemini - blue
            Color.FromArgb(255, 120, 0),     // Mistral - orange
            Color.FromArgb(0, 120, 212),     // Azure - MS blue
            Color.FromArgb(116, 170, 156),   // ChatGPT OAuth - teal
            Color.FromArgb(181, 101, 48),    // Claude OAuth - brown-orange
            Color.FromArgb(100, 100, 100),   // Ollama - gray
            Color.FromArgb(70, 70, 140)      // LM Studio - purple
        ];

        string[] initials = ["OA", "GK", "CL", "GE", "MI", "AZ", "GP", "CO", "OL", "LM"];

        for (int i = 0; i < colors.Length; i++)
        {
            Bitmap bmp = new(40, 40);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using SolidBrush fill = new(colors[i]);
                g.FillEllipse(fill, 2, 2, 36, 36);
                using Font font = new("Segoe UI", 12f, FontStyle.Bold);
                TextRenderer.DrawText(g, initials[i], font,
                    new Rectangle(2, 2, 36, 36), Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            _providerImageList.Images.Add(bmp);
        }
    }

    // ?? Step 2: API Key / OAuth ??????????????????????????????????

    private void BuildApiKeyPage()
    {
        string providerType = _selectedProviderType ?? AiProviderTypes.OpenAI;
        _importedProviderName = GetDefaultProviderName(providerType);

        if (providerType == AiProviderTypes.ChatGPTOAuth)
        {
            BuildOAuthPage();
            return;
        }

        if (providerType == AiProviderTypes.ClaudeOAuth)
        {
            BuildClaudeOAuthPage();
            return;
        }

        if (providerType == AiProviderTypes.Ollama || providerType == AiProviderTypes.LMStudio)
        {
            BuildLocalProviderPage(providerType);
            return;
        }

        _lblStepTitle.Text = "Enter API Key";
        _lblStepDescription.Text = $"Obtain your API key from {_importedProviderName} and paste it below.";

        // Top section: instructions + key input
        _apiKeyTopPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 90
        };

        _lblKeyInstructions = new Label
        {
            Text = GetKeyInstructions(providerType),
            Dock = DockStyle.Top,
            Height = 36,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(0, 4, 0, 4)
        };
        _apiKeyTopPanel.Controls.Add(_lblKeyInstructions);

        Panel keyRow = new() { Dock = DockStyle.Bottom, Height = 36 };

        _btnPasteKey = new Button { Text = "Paste", Width = 70, Height = 28, Dock = DockStyle.Right };
        _btnPasteKey.Click += (_, _) =>
        {
            if (Clipboard.ContainsText())
            {
                _txtApiKey!.Text = Clipboard.GetText().Trim();
                _txtApiKey.Focus();
            }
        };

        _txtApiKey = new TextBox
        {
            Dock = DockStyle.Fill,
            PasswordChar = '*',
            Font = new Font("Consolas", 10f),
            PlaceholderText = "Paste your API key here..."
        };

        // Restore previously entered key
        if (!string.IsNullOrWhiteSpace(_importedApiKey))
            _txtApiKey.Text = _importedApiKey;

        keyRow.Controls.Add(_btnPasteKey);
        Panel keySpacer = new() { Dock = DockStyle.Right, Width = 6 };
        keyRow.Controls.Add(keySpacer);
        keyRow.Controls.Add(_txtApiKey);
        _apiKeyTopPanel.Controls.Add(keyRow);

        // WebView for the provider's key management page
        string keyUrl = GetKeyManagementUrl(providerType);
        if (!string.IsNullOrWhiteSpace(keyUrl))
        {
            _webView = new WebView2 { Dock = DockStyle.Fill };
            // Add Fill control first, then Top control — WinForms docks in reverse add-order
            _contentPanel.Controls.Add(_webView);
            _contentPanel.Controls.Add(_apiKeyTopPanel);

            _ = InitializeWebViewAsync(keyUrl);
        }
        else
        {
            _contentPanel.Controls.Add(_apiKeyTopPanel);
        }
    }

    private async System.Threading.Tasks.Task InitializeWebViewAsync(string url)
    {
        try
        {
            await _webView!.EnsureCoreWebView2Async();
            _webView.CoreWebView2.Navigate(url);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Failed to initialize WebView2: {ex.Message}",
                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void BuildOAuthPage()
    {
        _lblStepTitle.Text = "ChatGPT OAuth Authentication";
        _lblStepDescription.Text = "Sign in with your OpenAI account.";

        Label lblOAuthInfo = new()
        {
            Dock = DockStyle.Top,
            Height = 60,
            Text = "ChatGPT OAuth uses your existing OpenAI account. Click the button below to open the " +
                   "sign-in window and authenticate with your credentials.",
            Padding = new Padding(0, 10, 0, 10)
        };

        Button btnStartOAuth = new()
        {
            Text = "Sign in to ChatGPT...",
            Width = 200,
            Height = 36,
            Margin = new Padding(0, 10, 0, 0)
        };

        Label lblStatus = new()
        {
            Text = string.IsNullOrWhiteSpace(_importedProviderId) ? "" : "Authentication completed.",
            ForeColor = Color.Green,
            AutoSize = true,
            Padding = new Padding(0, 10, 0, 0)
        };

        btnStartOAuth.Click += (_, _) =>
        {
            using ChatGPTOAuthImportForm importForm = new();
            if (importForm.ShowDialog(this) == DialogResult.OK)
            {
                _importedProviderId = importForm.ImportedProviderId;
                _importedApiKey = "OAuth2";
                lblStatus.Text = "Authentication completed successfully.";
                lblStatus.ForeColor = Color.Green;
            }
        };

        FlowLayoutPanel flow = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(0)
        };
        flow.Controls.Add(btnStartOAuth);
        flow.Controls.Add(lblStatus);

        // Add Fill control first, then Top control — WinForms docks in reverse add-order
        _contentPanel.Controls.Add(flow);
        _contentPanel.Controls.Add(lblOAuthInfo);
    }

    private void BuildClaudeOAuthPage()
    {
        _lblStepTitle.Text = "Claude OAuth Authentication";
        _lblStepDescription.Text = "Sign in with your Claude account.";

        Label lblOAuthInfo = new()
        {
            Dock = DockStyle.Top,
            Height = 60,
            Text = "Claude OAuth uses your existing Anthropic account. Click the button below to open the " +
                   "Claude sign-in window and authenticate with your credentials.",
            Padding = new Padding(0, 10, 0, 10)
        };

        Button btnStartOAuth = new()
        {
            Text = "Sign in to Claude...",
            Width = 200,
            Height = 36,
            Margin = new Padding(0, 10, 0, 0)
        };

        Label lblStatus = new()
        {
            Text = string.IsNullOrWhiteSpace(_importedProviderId) ? "" : "Authentication completed.",
            ForeColor = Color.Green,
            AutoSize = true,
            Padding = new Padding(0, 10, 0, 0)
        };

        btnStartOAuth.Click += (_, _) =>
        {
            using ClaudeOAuthImportForm importForm = new();
            if (importForm.ShowDialog(this) == DialogResult.OK)
            {
                _importedProviderId = importForm.ImportedProviderId;
                _importedApiKey = "OAuth2";
                lblStatus.Text = "Authentication completed successfully.";
                lblStatus.ForeColor = Color.Green;
            }
        };

        FlowLayoutPanel flow = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(0)
        };
        flow.Controls.Add(btnStartOAuth);
        flow.Controls.Add(lblStatus);

        // Add Fill control first, then Top control — WinForms docks in reverse add-order
        _contentPanel.Controls.Add(flow);
        _contentPanel.Controls.Add(lblOAuthInfo);
    }

    private void BuildLocalProviderPage(string providerType)
    {
        _lblStepTitle.Text = "Local Provider Setup";
        _lblStepDescription.Text = $"Configure {GetDefaultProviderName(providerType)}.";

        string defaultUrl = AiProviderService.GetDefaultBaseUrl(providerType);

        Label lblInfo = new()
        {
            Dock = DockStyle.Top,
            Height = 130,
            Text = $"{GetDefaultProviderName(providerType)} runs locally on your machine.\r\n\r\n" +
                   $"The default URL is: {defaultUrl}\r\n" +
                   $"You can change all the settings later.\r\n" +
                   "Most local providers do not require an API key. You can leave it empty or enter a placeholder.",
            Padding = new Padding(0, 10, 0, 10)
        };
        _contentPanel.Controls.Add(lblInfo);

        Panel keyRow = new() { Dock = DockStyle.Top, Height = 36, Padding = new Padding(0, 5, 0, 5) };
        Label lblKey = new() { Text = "API Key (optional):", Dock = DockStyle.Left, Width = 130, TextAlign = ContentAlignment.MiddleLeft };
        _txtApiKey = new TextBox { Dock = DockStyle.Fill, Text = "XXX", PlaceholderText = "Optional API key..." };
        keyRow.Controls.Add(_txtApiKey);
        keyRow.Controls.Add(lblKey);
        _contentPanel.Controls.Add(keyRow);

        _importedApiKey = "XXX";
    }

    // ?? Step 3: Summary ??????????????????????????????????????????

    private void BuildSummaryPage()
    {
        string providerType = _selectedProviderType ?? AiProviderTypes.OpenAI;
        string defaultName = GetDefaultProviderName(providerType);
        List<AiProviderModelDefinition> models = AiProviderService.GetDefaultModels(providerType);

        _lblStepTitle.Text = "Review & Finish";
        _lblStepDescription.Text = "Review the provider configuration and click Finish to import.";

        TableLayoutPanel table = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 5,
            Padding = new Padding(0, 10, 0, 0)
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        table.Controls.Add(new Label { Text = "Provider Name:", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 0, 0);
        _txtProviderName = new TextBox { Dock = DockStyle.Fill, Text = defaultName + " (Imported)" };
        table.Controls.Add(_txtProviderName, 1, 0);

        table.Controls.Add(new Label { Text = "Provider Type:", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 0, 1);
        table.Controls.Add(new Label { Text = providerType, TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 1, 1);

        table.Controls.Add(new Label { Text = "Base URL:", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 0, 2);
        table.Controls.Add(new Label { Text = AiProviderService.GetDefaultBaseUrl(providerType), TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 1, 2);

        table.Controls.Add(new Label { Text = "API Key:", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 0, 3);
        string keyDisplay = string.IsNullOrWhiteSpace(_importedApiKey) ? "(none)" :
            (_importedApiKey == "OAuth2" ? "OAuth2" : new string('*', Math.Min(_importedApiKey.Length, 20)));
        table.Controls.Add(new Label { Text = keyDisplay, TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 1, 3);

        // Models list
        table.Controls.Add(new Label { Text = "Default Models:", TextAlign = ContentAlignment.TopLeft, Dock = DockStyle.Fill, Padding = new Padding(0, 4, 0, 0) }, 0, 4);

        ListBox lstModels = new() { Dock = DockStyle.Fill };
        foreach (var model in models)
        {
            string flags = "";
            if (model.SupportsImages) flags += " -";
            if (model.SupportsTemperature) flags += " -";
            lstModels.Items.Add(model.Name + flags);
        }
        table.Controls.Add(lstModels, 1, 4);

        _contentPanel.Controls.Add(table);
    }

    // ?? Finish ????????????????????????????????????????????????????

    private void FinishImport()
    {
        string providerType = _selectedProviderType ?? AiProviderTypes.OpenAI;
        string name = _txtProviderName?.Text.Trim() ?? GetDefaultProviderName(providerType) + " (Imported)";

        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show(this, "Please enter a provider name.", Text,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string providerId = (providerType == AiProviderTypes.ChatGPTOAuth || providerType == AiProviderTypes.ClaudeOAuth) && !string.IsNullOrWhiteSpace(_importedProviderId)
            ? _importedProviderId
            : Guid.NewGuid().ToString("n");

        ImportedProvider = new AiProviderDefinition
        {
            Id = providerId,
            Name = name,
            ProviderType = providerType,
            BaseUrl = AiProviderService.GetDefaultBaseUrl(providerType),
            EndpointPath = AiProviderService.GetDefaultEndpointPath(providerType),
            ApiKey = _importedApiKey ?? string.Empty,
            Models = AiProviderService.GetDefaultModels(providerType)
        };

        DialogResult = DialogResult.OK;
        Close();
    }

    private void DisposeWebView()
    {
        if (_webView != null)
        {
            _contentPanel.Controls.Remove(_webView);
            _webView.Dispose();
            _webView = null;
        }
    }

    // ?? Helpers ??????????????????????????????????????????????????

    private static string GetDefaultProviderName(string providerType) => providerType switch
    {
        AiProviderTypes.OpenAI => "OpenAI",
        AiProviderTypes.Grok => "GROK",
        AiProviderTypes.Claude => "Claude",
        AiProviderTypes.GoogleGemini => "Google Gemini",
        AiProviderTypes.Mistral => "Mistral",
        AiProviderTypes.AzureOpenAI => "Azure OpenAI",
        AiProviderTypes.ChatGPTOAuth => "ChatGPT OAuth",
        AiProviderTypes.ClaudeOAuth => "Claude OAuth",
        AiProviderTypes.Ollama => "Ollama",
        AiProviderTypes.LMStudio => "LM Studio",
        _ => providerType
    };

    private static string GetKeyInstructions(string providerType) => providerType switch
    {
        AiProviderTypes.OpenAI => "1. Log in to OpenAI and create a new secret key.  2. Paste it below:",
        AiProviderTypes.Grok => "1. Log in to xAI console and create a new API key.  2. Paste it below:",
        AiProviderTypes.Claude => "1. Log in to Anthropic console and create a new API key.  2. Paste it below:",
        AiProviderTypes.GoogleGemini => "1. Log in to Google AI Studio and create a new API key.  2. Paste it below:",
        AiProviderTypes.Mistral => "1. Log in to Mistral console and create a new API key.  2. Paste it below:",
        AiProviderTypes.AzureOpenAI => "Enter your Azure OpenAI resource API key below:",
        _ => "Enter your API key below:"
    };

    private static string? GetKeyManagementUrl(string providerType) => providerType switch
    {
        AiProviderTypes.OpenAI => "https://platform.openai.com/api-keys",
        AiProviderTypes.Grok => "https://console.x.ai/",
        AiProviderTypes.Claude => "https://console.anthropic.com/settings/keys",
        AiProviderTypes.GoogleGemini => "https://aistudio.google.com/app/apikey",
        AiProviderTypes.Mistral => "https://console.mistral.ai/api-keys/",
        AiProviderTypes.AzureOpenAI => "https://portal.azure.com/#view/Microsoft_Azure_ProjectOxford/CognitiveServicesHub/~/OpenAI",
        _ => null
    };
}
