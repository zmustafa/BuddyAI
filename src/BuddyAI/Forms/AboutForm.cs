using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Reflection;

namespace BuddyAI.Forms;

public sealed class AboutForm : Form
{
    private const string LicenseUrl = "https://mit-license.org/";

    public AboutForm()
    {
        Assembly asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

        string appName = GetAttribute<AssemblyProductAttribute>(asm)?.Product ?? "BuddyAI Desktop";
        string description = GetAttribute<AssemblyDescriptionAttribute>(asm)?.Description ?? string.Empty;
        string copyright = GetAttribute<AssemblyCopyrightAttribute>(asm)?.Copyright ?? string.Empty;
        string projectUrl = GetMetadataAttribute(asm, "RepositoryUrl") ?? "https://github.com/zmustafa/BuddyAI";
        string version = GetAppVersion(asm);

        Text = $"About {appName}";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Size = new Size(520, 520);
        ShowInTaskbar = false;

        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(24, 20, 24, 16)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));   // header
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));  // details
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));   // project link
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));   // license link
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));   // close button
        Controls.Add(root);

        // --- Header with icon and title ---
        FlowLayoutPanel header = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0)
        };

        PictureBox icon = new()
        {
            Width = 64,
            Height = 64,
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = CreateAppIcon(),
            Margin = new Padding(0, 0, 16, 0)
        };
        header.Controls.Add(icon);

        Panel titleBlock = new() { Width = 360, Height = 64 };
        Label lblName = new()
        {
            Text = appName,
            Font = new Font("Segoe UI", 18F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(0, 2)
        };
        titleBlock.Controls.Add(lblName);

        Label lblVersion = new()
        {
            Text = $"Version {version}",
            Font = new Font("Segoe UI", 9.5F),
            AutoSize = true,
            Location = new Point(2, 38)
        };
        titleBlock.Controls.Add(lblVersion);
        header.Controls.Add(titleBlock);
        root.Controls.Add(header, 0, 0);

        // --- Details ---
        TextBox txtDetails = new()
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 9.5F),
            Text = string.Join(Environment.NewLine,
            [
                description,
                "",
                "Features:",
                "  • Multi-provider AI conversations (OpenAI, Claude, Grok)",
                "  • Persona-driven system prompts",
                "  • Session restore and conversation tabs",
                "  • Screenshot capture and image analysis",
                "  • Clipboard monitoring and diagnostics",
                "  • Workspace profiles and theming",
                "",
                copyright,
                "",
                $"Runtime: .NET {Environment.Version}",
                $"OS: {Environment.OSVersion}",
                ""
            ])
        };
        root.Controls.Add(txtDetails, 0, 1);

        // --- Project link ---
        LinkLabel lnkProject = new()
        {
            Dock = DockStyle.Fill,
            Text = projectUrl,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9.5F)
        };
        lnkProject.LinkClicked += (_, _) => OpenUrl(projectUrl);
        root.Controls.Add(lnkProject, 0, 2);

        // --- License link ---
        LinkLabel lnkLicense = new()
        {
            Dock = DockStyle.Fill,
            Text = $"License: MIT ({LicenseUrl})",
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9.5F)
        };
        lnkLicense.LinkClicked += (_, _) => OpenUrl(LicenseUrl);
        root.Controls.Add(lnkLicense, 0, 3);

        // --- Close button ---
        FlowLayoutPanel buttonPanel = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        Button btnClose = new()
        {
            Text = "Close",
            Width = 100,
            Height = 32,
            DialogResult = DialogResult.OK
        };
        buttonPanel.Controls.Add(btnClose);
        root.Controls.Add(buttonPanel, 0, 4);

        AcceptButton = btnClose;
        CancelButton = btnClose;
    }

    private static string GetAppVersion(Assembly asm)
    {
        // Prefer informational version (supports semver/pre-release tags)
        string? informational = GetAttribute<AssemblyInformationalVersionAttribute>(asm)?.InformationalVersion;
        if (!string.IsNullOrEmpty(informational))
        {
            // Strip source-link commit hash appended after '+'
            int plusIndex = informational.IndexOf('+');
            return plusIndex > 0 ? informational[..plusIndex] : informational;
        }

        Version? version = asm.GetName().Version;
        return version is not null
            ? $"{version.Major}.{version.Minor}.{version.Build}"
            : "1.0.0";
    }

    private static T? GetAttribute<T>(Assembly asm) where T : Attribute
    {
        return asm.GetCustomAttribute<T>();
    }

    private static string? GetMetadataAttribute(Assembly asm, string key)
    {
        return asm.GetCustomAttributes<AssemblyMetadataAttribute>()
                  .FirstOrDefault(a => string.Equals(a.Key, key, StringComparison.OrdinalIgnoreCase))
                  ?.Value;
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // Silently handle environments where the browser cannot launch
        }
    }

    private static Bitmap CreateAppIcon()
    {
        Bitmap bitmap = new(64, 64);
        using Graphics g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        using LinearGradientBrush gradientBrush = new(
            new Rectangle(0, 0, 64, 64),
            Color.FromArgb(0, 122, 204),
            Color.FromArgb(0, 88, 160),
            45f);
        g.FillEllipse(gradientBrush, 2, 2, 60, 60);

        using Font font = new("Segoe UI", 22F, FontStyle.Bold);
        TextRenderer.DrawText(g, "AI", font,
            new Rectangle(0, 0, 64, 64), Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

        return bitmap;
    }
}
