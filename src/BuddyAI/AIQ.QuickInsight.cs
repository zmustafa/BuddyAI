using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms.VisualStyles;
using BuddyAI.Docking;
using BuddyAI.Forms;

namespace BuddyAI;

public sealed partial class AIQ
{
    private readonly CheckBox _chkSnipShortcut = new();
    private readonly ToolTip _toolTip = new();
    private ConversationWindowForm? _activeQuickResultWindow;
    private bool _globalSnipShortcutRegistered;
    private bool _suppressSnipShortcutToggle;
    private bool _isSnipInProgress;

    private const int WmHotKey = 0x0312;
    private const int QuickSnipHotKeyId = 0x4241;
    private const double QuickResultMinZoomFactor = 0.5d;
    private const double QuickResultMaxZoomFactor = 3.50d;

    [Flags]
    private enum HotKeyModifiers : uint
    {
        None = 0,
        Alt = 0x0001,
        Control = 0x0002,
        Shift = 0x0004,
        Win = 0x0008,
        NoRepeat = 0x4000
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private sealed class SnipCaptureOptions
    {
        public bool AutoAskAfterCapture { get; init; }
        public bool HideMainWindowDuringCapture { get; init; } = true;
        public bool RestoreMainWindowAfterCapture { get; init; } = true;
        public bool OpenResultWindowOnSuccess { get; init; }
        public bool PreferMainWindowForDialogs { get; init; } = true;
    }

    private sealed class AskExecutionOptions
    {
        public bool OpenResultWindowOnSuccess { get; init; }
        public bool PreferMainWindowForDialogs { get; init; } = true;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmHotKey && m.WParam == (IntPtr)QuickSnipHotKeyId)
        {
            _ = BeginQuickSnipAskFromShortcutAsync();
            return;
        }

        if (m.Msg == WmHotKey && m.WParam == (IntPtr)TextCaptureHotKeyId)
        {
            ShowTextCaptureForm();
            return;
        }

        base.WndProc(ref m);
    }

    private async Task BeginQuickSnipAskFromShortcutAsync()
    {
        if (!_chkSnipShortcut.Checked)
            return;

        if (_isBusy || _isSnipInProgress)
        {
            SetStatus("BuddyAI is already processing another request.");
            return;
        }
        this.WindowState = FormWindowState.Minimized;

        bool shellVisible = Visible && WindowState != FormWindowState.Minimized;
        _diagnostics.Info("Global quick insight shortcut invoked.");

        await SnipScreenAsync(new SnipCaptureOptions
        {
            AutoAskAfterCapture = true,
            HideMainWindowDuringCapture = shellVisible,
            RestoreMainWindowAfterCapture = shellVisible,
            OpenResultWindowOnSuccess = true,
            PreferMainWindowForDialogs = shellVisible
        });
      
    }

    private void OnSnipShortcutToggleChanged(object? sender, EventArgs e)
    {
        if (_suppressSnipShortcutToggle)
            return;

        _workspaceSettings.GlobalShortcutSnipAskEnabled = _chkSnipShortcut.Checked;
        SaveWorkspaceSettingsQuietly();

        if (_chkSnipShortcut.Checked)
        {
           _chkSnipShortcut.AccessibleName= UpdateGlobalSnipShortcutRegistration(showFailureUi: true, announceSuccess: true);
        }
        else
        {
            ReleaseGlobalSnipShortcutRegistration();
            SetStatus("Global shortcut disabled.");
        }
    }

    private string UpdateGlobalSnipShortcutRegistration(bool showFailureUi, bool announceSuccess)
    {
        if (!IsHandleCreated)
            return "";

        ReleaseGlobalSnipShortcutRegistration();

        if (!_chkSnipShortcut.Checked)
            return "";

        try
        {
            // Hard-coded hotkey attempts (Win + Shift + ?)
            // Order: A (your original) → then safer alternatives like Q, Z, X, Y, etc.
            // These letters are rarely used by Windows or major apps in 2025–2026

            uint modifiers = (uint)(HotKeyModifiers.Win | HotKeyModifiers.Shift | HotKeyModifiers.NoRepeat);

            // List of keys to try (in order of preference)
            Keys[] fallbackKeys = new Keys[]
            {
              //  Keys.A,     // 0 - your original (likely conflicted)
                Keys.Q,     // 1 - very low conflict, often free
                //Keys.Z,     // 2 - safe alternative
                //Keys.X,     // 3 - safe
                //Keys.Y,     // 4 - safe
                //Keys.O,     // 5 - good mnemonic for "OCR" or "Operate"
                //Keys.D,     // 6 - for "Detect" / "Data"
                //Keys.NumPad1 // 7 - numeric pad keys almost never conflicted
            };

            bool registered = false;
            int successfulIndex = -1;

            for (int i = 0; i < fallbackKeys.Length; i++)
            {
                // Always try to unregister first (safe even if not registered)
                UnregisterHotKey(Handle, QuickSnipHotKeyId);

                uint vk = (uint)fallbackKeys[i];

                registered = RegisterHotKey(
                    Handle,
                    QuickSnipHotKeyId,
                    modifiers,
                    vk);

                if (registered)
                {
                    successfulIndex = i;
                    break;
                }
            }

            // After the loop, report what happened
            string keyName="";
            if (registered)
            {
                keyName = fallbackKeys[successfulIndex].ToString();

                // Optional: show message or log
           //     MessageBox.Show($"Hotkey registered successfully: Win + Shift + {keyName}");
                // You can store the successful key if you want to show it in UI later
            }
            else
            {
                MessageBox.Show("Failed to register ANY of the fallback hotkeys.\n" +
                                "Try restarting the PC, or close apps like PowerToys/ShareX/AutoHotkey.\n" +
                                "Error code from last attempt: " + Marshal.GetLastWin32Error());
            }

            if (registered)
            {
                _globalSnipShortcutRegistered = true;
                if (announceSuccess)
                    SetStatus("Global shortcut enabled: Win+Shift+Q.");
                return keyName;
            }
        }
        catch (Exception ex)
        {
            _diagnostics.Error("Global shortcut registration failed: " + ex.Message);
        }

        _workspaceSettings.GlobalShortcutSnipAskEnabled = false;
        _suppressSnipShortcutToggle = true;
        try
        {
            _chkSnipShortcut.Checked = false;
        }
        finally
        {
            _suppressSnipShortcutToggle = false;
        }

        SaveWorkspaceSettingsQuietly();
        SetStatus("Global shortcut unavailable.");

        if (showFailureUi)
        {
            ShowAppMessage(
                "BuddyAI could not register Win+Shift+Q. Another application may already be using that shortcut.",
                "Global Shortcut",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning,
                preferMainWindowForDialogs: true);
        }

        return "";
    }

    private void ReleaseGlobalSnipShortcutRegistration()
    {
        if (!_globalSnipShortcutRegistered)
            return;

        try
        {
            if (IsHandleCreated)
                UnregisterHotKey(Handle, QuickSnipHotKeyId);
        }
        catch
        {
        }
        finally
        {
            _globalSnipShortcutRegistered = false;
        }
    }

    private DialogResult ShowAppMessage(
        string text,
        string caption,
        MessageBoxButtons buttons,
        MessageBoxIcon icon,
        bool preferMainWindowForDialogs)
    {
        if (preferMainWindowForDialogs && !IsDisposed && Visible && WindowState != FormWindowState.Minimized)
            return MessageBox.Show(this, text, caption, buttons, icon);

        return MessageBox.Show(text, caption, buttons, icon);
    }

    private void CaptureWorkspaceSettingsFromShell()
    {
        _workspaceSettings.ShowPersonaPanel = _dockHost.IsPanelVisible(_dockPersonaExplorer);
        _workspaceSettings.ShowToolsPanel = _dockHost.IsPanelVisible(_dockAiTools);
        _workspaceSettings.ShowDiagnosticsPanel = _dockHost.IsPanelVisible(_dockDiagnostics);
        _workspaceSettings.LeftSplitterDistance = _dockHost.GetZoneSize(DockZone.Left);
        _workspaceSettings.RightSplitterDistance = _dockHost.GetZoneSize(DockZone.Right);
        _workspaceSettings.BottomSplitterDistance = _dockHost.GetZoneSize(DockZone.Bottom);
        _workspaceSettings.WindowWidth = Width;
        _workspaceSettings.WindowHeight = Height;
        _workspaceSettings.AutoAskAfterSnipEnabled = _chkSnipAuto.Checked;
        _workspaceSettings.GlobalShortcutSnipAskEnabled = _chkSnipShortcut.Checked;
        _workspaceSettings.DockLayout = BuildDockLayoutState();
    }

    private void SaveWorkspaceSettingsQuietly()
    {
        try
        {
            CaptureWorkspaceSettingsFromShell();
            _workspaceSettingsService.Save(_workspaceSettings);
        }
        catch (Exception ex)
        {
            _diagnostics.Warn("Workspace settings save skipped: " + ex.Message);
        }
    }

    private void OpenQuickResultWindow(ConversationTabState state)
    {
        if (string.IsNullOrWhiteSpace(state.RawResponseText))
            return;

        if (_activeQuickResultWindow != null && !_activeQuickResultWindow.IsDisposed)
        {
            _activeQuickResultWindow.Close();
            _activeQuickResultWindow = null;
        }

        string html = BuildResponseHtml(
            state.RawResponseText,
            state.LastAnalysis ?? AnalyzeResponse(state.RawResponseText),
            state.ResponseViewerStateJson);

        ConversationWindowForm window = new(
            state.Title,
            html,
            state.RawResponseText,
            new ConversationWindowFormOptions
            {
                IsPopupWindow = true,
                TopMost = true,
                ShowInTaskbar = false,
                WindowTitlePrefix = "BuddyAI Quick Insight",
                InitialSize = new Size(
                    Math.Max(520, _workspaceSettings.QuickResultWindowWidth),
                    Math.Max(360, _workspaceSettings.QuickResultWindowHeight)),
                InitialZoomFactor = Math.Clamp(
                    _workspaceSettings.QuickResultWindowZoomFactor <= 0d ? 1d : _workspaceSettings.QuickResultWindowZoomFactor,
                    QuickResultMinZoomFactor,
                    QuickResultMaxZoomFactor),
                WorkingAreaProvider = GetQuickResultWorkingArea,
                ZoomFactorChanged = zoom =>
                {
                    _workspaceSettings.QuickResultWindowZoomFactor = zoom;
                    SaveWorkspaceSettingsQuietly();
                },
                WindowSizeChanged = size =>
                {
                    _workspaceSettings.QuickResultWindowWidth = size.Width;
                    _workspaceSettings.QuickResultWindowHeight = size.Height;
                    SaveWorkspaceSettingsQuietly();
                }
            });

        _activeQuickResultWindow = window;
        window.FormClosed += (_, __) =>
        {
            if (ReferenceEquals(_activeQuickResultWindow, window))
                _activeQuickResultWindow = null;
        };

        window.Show();
        window.Activate();
    }

    private static Rectangle GetQuickResultWorkingArea()
    {
        try
        {
            return Screen.FromPoint(Cursor.Position).WorkingArea;
        }
        catch
        {
            return Screen.PrimaryScreen?.WorkingArea
                ?? Screen.AllScreens.FirstOrDefault()?.WorkingArea
                ?? new Rectangle(0, 0, 1280, 720);
        }
    }
}
