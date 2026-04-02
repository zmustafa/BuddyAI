using System.Runtime.InteropServices;
using System.Text;
using Timer = System.Windows.Forms.Timer;

namespace SelectionCaptureApp;

public sealed class MainForm : Form
{
    private const int HotkeyId = 0xB001;
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;
    private const int VK_Q = 0x51;

    private NotifyIcon? _notifyIcon;
    private ContextMenuStrip? _menu;
    private bool _initialized;

    public MainForm()
    {
        Text = "Selection Capture App";
        ShowInTaskbar = false;
        WindowState = FormWindowState.Minimized;
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        Opacity = 0;
        Load += OnLoad;
        FormClosed += OnClosed;
    }

    private void OnLoad(object? sender, EventArgs e)
    {
        if (_initialized) return;
        _initialized = true;

        BuildTrayIcon();

        var timer = new Timer { Interval = 200 };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            timer.Dispose();
            RegisterGlobalHotkey();
            Hide();
        };
        timer.Start();
    }

    private void OnClosed(object? sender, FormClosedEventArgs e)
    {
        try { NativeMethods.UnregisterHotKey(Handle, HotkeyId); } catch { }
        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
        _menu?.Dispose();
        _menu = null;
    }

    private void BuildTrayIcon()
    {
        _menu = new ContextMenuStrip();
        _menu.Items.Add("Test Capture", null, (_, _) => ShowHotkeyResult(HandleHotkey()));
        _menu.Items.Add("Exit", null, (_, _) => Close());

        _notifyIcon = new NotifyIcon
        {
            Text = "Selection Capture App (Win+Shift+Q)",
            Visible = true,
            Icon = SystemIcons.Information,
            ContextMenuStrip = _menu
        };

        _notifyIcon.DoubleClick += (_, _) => ShowHotkeyResult(HandleHotkey());
        _notifyIcon.ShowBalloonTip(2000, "Selection Capture App", "Listening on Win+Shift+Q", ToolTipIcon.Info);
    }

    private void RegisterGlobalHotkey()
    {
        if (!NativeMethods.RegisterHotKey(Handle, HotkeyId, MOD_WIN | MOD_SHIFT, VK_Q))
        {
            ShowHotkeyResult(new HotkeyResult
            {
                Status = HotkeyStatus.Error,
                Title = "Hotkey Registration Failed",
                Message = $"Failed to register Win+Shift+Q. Win32 error: {Marshal.GetLastWin32Error()}"
            });
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY && m.WParam == (IntPtr)HotkeyId)
        {
            ShowHotkeyResult(HandleHotkey());
            return;
        }

        base.WndProc(ref m);
    }

    internal static HotkeyResult HandleHotkey()
    {
        CaptureResult result;
        try
        {
            result = SelectionCaptureEngine.TryCapture();
        }
        catch (Exception ex)
        {
            return new HotkeyResult
            {
                Status = HotkeyStatus.Error,
                Title = "Capture Error",
                Message = ex.ToString()
            };
        }

        if (result.Success && !string.IsNullOrWhiteSpace(result.SelectedText))
        {
            return new HotkeyResult
            {
                Status = HotkeyStatus.Captured,
                Title = $"Selected text captured via {result.Method}",
                Message = result.SelectedText,
                CapturedText = result.SelectedText,
                CaptureResult = result
            };
        }

        if (result.TextBoxFound)
        {
            var sb = new StringBuilder();
            sb.Append("Textbox-like input was found, but no selected text could be read.");
            if (!string.IsNullOrWhiteSpace(result.Method))
            {
                sb.AppendLine();
                sb.AppendLine();
                sb.Append("Detection path: ").Append(result.Method);
            }
            if (!string.IsNullOrWhiteSpace(result.ClassName))
            {
                sb.AppendLine();
                sb.Append("Class: ").Append(result.ClassName);
            }
            if (!string.IsNullOrWhiteSpace(result.WindowTitle))
            {
                sb.AppendLine();
                sb.Append("Window: ").Append(result.WindowTitle);
            }
            if (!string.IsNullOrWhiteSpace(result.Diagnostics))
            {
                sb.AppendLine();
                sb.AppendLine();
                sb.Append(result.Diagnostics);
            }

            return new HotkeyResult
            {
                Status = HotkeyStatus.TextBoxFoundButEmpty,
                Title = "Textbox Found",
                Message = sb.ToString(),
                CaptureResult = result
            };
        }

        return new HotkeyResult
        {
            Status = HotkeyStatus.NothingFound,
            Title = "Nothing Found",
            Message = "No textbox or readable text selection was detected in the currently focused window.",
            CaptureResult = result
        };
    }

    private static void ShowHotkeyResult(HotkeyResult result)
    {
        var icon = result.Status switch
        {
            HotkeyStatus.Captured => MessageBoxIcon.Information,
            HotkeyStatus.TextBoxFoundButEmpty => MessageBoxIcon.Warning,
            HotkeyStatus.NothingFound => MessageBoxIcon.Exclamation,
            HotkeyStatus.Error => MessageBoxIcon.Error,
            _ => MessageBoxIcon.None
        };

        if (result.CaptureResult != null && result.CaptureResult.Success)
        {

            SelectionCaptureEngine.ReplaceSourceText(result.CaptureResult, "XXX");

        }

       // MessageBox.Show(result.Message, result.Title, MessageBoxButtons.OK, icon);

        //if (result.Status == HotkeyStatus.Captured && result.CaptureResult is { Success: true, FocusInfo: not null })
        //{
        //    var answer = MessageBox.Show(
        //        "Would you like to replace the source text with 'XXX'?",
        //        "Replace Source Text",
        //        MessageBoxButtons.YesNo,
        //        MessageBoxIcon.Question);

        //    if (answer == DialogResult.Yes)
        //    {
        //        try
        //        {
        //            SelectionCaptureEngine.ReplaceSourceText(result.CaptureResult, "XXX");
        //        }
        //        catch (Exception ex)
        //        {
        //            MessageBox.Show($"Failed to replace text: {ex.Message}", "Replace Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        //        }
        //    }
        //}
    }
}

internal enum HotkeyStatus
{
    Captured,
    TextBoxFoundButEmpty,
    NothingFound,
    Error
}

internal readonly record struct HotkeyResult
{
    public required HotkeyStatus Status { get; init; }
    public required string Title { get; init; }
    public required string Message { get; init; }
    public string CapturedText { get; init; }
    public CaptureResult? CaptureResult { get; init; }
}
