using System.Drawing.Drawing2D;

namespace BuddyAI.Services;

public static class ThemeService
{
    public sealed class ThemeProfile
    {
        public string Name { get; init; } = "Dark";
        public Color Background { get; init; } = Color.FromArgb(30, 30, 30);
        public Color Surface { get; init; } = Color.FromArgb(45, 45, 48);
        public Color SurfaceAlt { get; init; } = Color.FromArgb(37, 37, 38);
        public Color InputBackground { get; init; } = Color.FromArgb(30, 30, 30);
        public Color Text { get; init; } = Color.WhiteSmoke;
        public Color MutedText { get; init; } = Color.Gainsboro;
        public Color Accent { get; init; } = Color.FromArgb(0, 122, 204);
        public Color AccentForeground { get; init; } = Color.White;
        public Color Border { get; init; } = Color.FromArgb(63, 63, 70);
    }

    public static readonly ThemeProfile Light = new()
    {
        Name = "Light",
        Background = Color.FromArgb(245, 245, 245),
        Surface = Color.White,
        SurfaceAlt = Color.FromArgb(236, 236, 236),
        InputBackground = Color.White,
        Text = Color.FromArgb(30, 30, 30),
        MutedText = Color.FromArgb(60, 60, 60),
        Accent = Color.FromArgb(0, 120, 215),
        AccentForeground = Color.White,
        Border = Color.FromArgb(210, 210, 210)
    };

    public static readonly ThemeProfile Dark = new()
    {
        Name = "Dark",
        Background = Color.FromArgb(37, 37, 38),
        Surface = Color.FromArgb(45, 45, 48),
        SurfaceAlt = Color.FromArgb(30, 30, 30),
        InputBackground = Color.FromArgb(30, 30, 30),
        Text = Color.WhiteSmoke,
        MutedText = Color.Gainsboro,
        Accent = Color.FromArgb(255, 140, 0),
        AccentForeground = Color.White,
        Border = Color.FromArgb(63, 63, 70)
    };

    public static readonly ThemeProfile VisualStudioDark = new()
    {
        Name = "Visual Studio Dark",
        Background = Color.FromArgb(37, 37, 38),
        Surface = Color.FromArgb(45, 45, 48),
        SurfaceAlt = Color.FromArgb(30, 30, 30),
        InputBackground = Color.FromArgb(30, 30, 30),
        Text = Color.WhiteSmoke,
        MutedText = Color.Gainsboro,
        Accent = Color.FromArgb(0, 122, 204),
        AccentForeground = Color.White,
        Border = Color.FromArgb(63, 63, 70)
    };

    public static readonly ThemeProfile AzureBlue = new()
    {
        Name = "Azure Blue",
        Background = Color.FromArgb(239, 246, 255),
        Surface = Color.White,
        SurfaceAlt = Color.FromArgb(219, 234, 254),
        InputBackground = Color.White,
        Text = Color.FromArgb(17, 24, 39),
        MutedText = Color.FromArgb(55, 65, 81),
        Accent = Color.FromArgb(2, 132, 199),
        AccentForeground = Color.White,
        Border = Color.FromArgb(147, 197, 253)
    };

    public static readonly ThemeProfile HighContrast = new()
    {
        Name = "High Contrast",
        Background = Color.Black,
        Surface = Color.Black,
        SurfaceAlt = Color.FromArgb(18, 18, 18),
        InputBackground = Color.Black,
        Text = Color.White,
        MutedText = Color.White,
        Accent = Color.Yellow,
        AccentForeground = Color.Black,
        Border = Color.White
    };

    public static ThemeProfile Resolve(string? name)
    {
        return (name ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "light" => Light,
            "dark" => Dark,
            "azure blue" => AzureBlue,
            "high contrast" => HighContrast,
            _ => VisualStudioDark
        };
    }

    public static void ApplyTheme(Control root, ThemeProfile theme)
    {
        ApplyRecursive(root, theme);
    }

    public static void StylePrimaryButton(Button button, ThemeProfile theme)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = theme.Border;
        button.BackColor = theme.Background;
        button.ForeColor = theme.Text;
    }

    public static void StyleSecondaryButton(Button button, ThemeProfile theme)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = theme.Border;
        button.BackColor = theme.Surface;
        button.ForeColor = theme.Text;
    }

    public static Bitmap CreateGlyph(Color accent, string text)
    {
        Bitmap bitmap = new(16, 16);
        using Graphics g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);
        using SolidBrush brush = new(accent);
        g.FillEllipse(brush, 1, 1, 14, 14);
        TextRenderer.DrawText(g, text, new Font("Segoe UI", 7.5f, FontStyle.Bold), new Rectangle(0, 0, 16, 16), Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        return bitmap;
    }

    private static void ApplyRecursive(Control control, ThemeProfile theme)
    {
        if (control is Form form)
        {
            form.BackColor = theme.Background;
            form.ForeColor = theme.Text;
        }
        else if (control is TextBox textBox)
        {
            textBox.BackColor = theme.InputBackground;
            textBox.ForeColor = theme.Text;
            textBox.BorderStyle = BorderStyle.FixedSingle;
        }
        else if (control is RichTextBox richTextBox)
        {
            richTextBox.BackColor = theme.InputBackground;
            richTextBox.ForeColor = theme.Text;
            richTextBox.BorderStyle = BorderStyle.FixedSingle;
        }
        else if (control is ListBox listBox)
        {
            listBox.BackColor = theme.InputBackground;
            listBox.ForeColor = theme.Text;
            listBox.BorderStyle = BorderStyle.FixedSingle;
        }
        else if (control is TreeView treeView)
        {
            treeView.BackColor = theme.InputBackground;
            treeView.ForeColor = theme.Text;
            treeView.BorderStyle = BorderStyle.FixedSingle;
            treeView.LineColor = theme.Border;
        }
        else if (control is ListView listView)
        {
            listView.BackColor = theme.InputBackground;
            listView.ForeColor = theme.Text;
        }
        else if (control is ComboBox comboBox)
        {
            comboBox.BackColor = theme.InputBackground;
            comboBox.ForeColor = theme.Text;
        }
        else if (control is TabControl tabControl)
        {
            tabControl.BackColor = theme.SurfaceAlt;
            tabControl.ForeColor = theme.Text;
        }
        else if (control is Panel or GroupBox or SplitContainer or TableLayoutPanel or FlowLayoutPanel)
        {
            control.BackColor = theme.Surface;
            control.ForeColor = theme.Text;
        }
        else if (control is PictureBox)
        {
            control.BackColor = theme.InputBackground;
        }
        else if (control is Button button)
        {
            StyleSecondaryButton(button, theme);
        }
        else
        {
            control.BackColor = theme.Surface;
            control.ForeColor = theme.Text;
        }

        if (control is ToolStrip toolStrip)
        {
            toolStrip.RenderMode = ToolStripRenderMode.System;
            toolStrip.BackColor = theme.SurfaceAlt;
            toolStrip.ForeColor = theme.Text;
            foreach (ToolStripItem item in toolStrip.Items)
            {
                item.BackColor = theme.SurfaceAlt;
                item.ForeColor = theme.Text;
            }
        }

        foreach (Control child in control.Controls)
            ApplyRecursive(child, theme);
    }
}
