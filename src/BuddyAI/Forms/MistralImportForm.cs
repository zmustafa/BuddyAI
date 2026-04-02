using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;

namespace BuddyAI.Forms;

public partial class MistralImportForm : Form
{
    private readonly WebView2 _webView = new();
    private readonly TextBox _txtApiKey = new();
    private readonly Button _btnImport = new();

    public string? ImportedApiKey { get; private set; }

    public MistralImportForm()
    {
        Text = "Import Mistral API Key";
        Size = new Size(1100, 800);
        StartPosition = FormStartPosition.CenterParent;

        var topPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 50,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(10)
        };
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));

        var lblInstructions = new Label
        {
            Text = "1. Log in & Create new API key.  2. Paste here:",
            TextAlign = ContentAlignment.MiddleLeft,
            AutoSize = true,
            Dock = DockStyle.Fill
        };
        topPanel.Controls.Add(lblInstructions, 0, 0);

        _txtApiKey.Dock = DockStyle.Fill;
        _txtApiKey.PasswordChar = '*';
        _txtApiKey.Margin = new Padding(10, 5, 10, 5);
        topPanel.Controls.Add(_txtApiKey, 1, 0);

        _btnImport.Text = "Import";
        _btnImport.Dock = DockStyle.Fill;
        _btnImport.Click += BtnImport_Click;
        topPanel.Controls.Add(_btnImport, 2, 0);

        Controls.Add(topPanel);

        _webView.Dock = DockStyle.Fill;
        Controls.Add(_webView);

        Load += async (s, e) =>
        {
            try
            {
                await _webView.EnsureCoreWebView2Async();
                _webView.CoreWebView2.Navigate("https://console.mistral.ai/api-keys/");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize WebView2: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
    }

    private void BtnImport_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_txtApiKey.Text))
        {
            MessageBox.Show(this, "Please paste the API key first.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        ImportedApiKey = _txtApiKey.Text.Trim();
        DialogResult = DialogResult.OK;
        Close();
    }
}