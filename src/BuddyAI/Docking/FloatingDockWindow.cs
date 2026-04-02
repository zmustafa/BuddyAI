using BuddyAI.Services;

namespace BuddyAI.Docking;

/// <summary>
/// A floating window that hosts a <see cref="DockablePanel"/> when it is undocked.
/// Uses FormBorderStyle.None so only the DockablePanel's own title bar is shown
/// (no duplicate OS chrome).  Resizable via WM_NCHITTEST edge detection.
/// </summary>
internal sealed class FloatingDockWindow : Form
{
    private DockablePanel? _hostedPanel;
    private readonly DockContainerHost _host;
    private readonly ContextMenuStrip _contextMenu;
    private DockZone _lastDockedZone = DockZone.Right;

    private const int ResizeBorderWidth = 6;

    public DockablePanel? HostedPanel => _hostedPanel;

    public FloatingDockWindow(DockContainerHost host)
    {
        _host = host;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        MinimumSize = new Size(200, 150);
        KeyPreview = true;
        DoubleBuffered = true;

        _contextMenu = new ContextMenuStrip();
        _contextMenu.Items.Add("Dock Back", null, (_, __) => RedockPanel());
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add("Close", null, (_, __) => Close());

        ContextMenuStrip = _contextMenu;

        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
                RedockPanel();
        };
    }

    public void AttachPanel(DockablePanel panel, Point screenLocation, Size size)
    {
        _hostedPanel = panel;
        Text = panel.PanelTitle;
        Size = size;
        Location = screenLocation;

        panel.Dock = DockStyle.Fill;
        Controls.Clear();
        Controls.Add(panel);
        _lastDockedZone = panel.CurrentZone is DockZone.Float or DockZone.None ? DockZone.Right : panel.CurrentZone;
        panel.CurrentZone = DockZone.Float;
        panel.ContextMenuStrip = _contextMenu;
    }

    public DockablePanel? DetachPanel()
    {
        DockablePanel? panel = _hostedPanel;
        _hostedPanel = null;
        if (panel != null)
        {
            panel.ContextMenuStrip = null;
            Controls.Remove(panel);
        }
        return panel;
    }

    public void RedockPanel()
    {
        if (_hostedPanel == null) return;
        DockablePanel panel = _hostedPanel;
        DockZone targetZone = _lastDockedZone is DockZone.None or DockZone.Float ? DockZone.Right : _lastDockedZone;
        DetachPanel();
        _host.DockPanel(panel, targetZone);
        Close();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing && _hostedPanel != null)
        {
            DockablePanel panel = _hostedPanel;
            DetachPanel();
            _host.HidePanel(panel);
        }
        base.OnFormClosing(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        // Draw a 1px border so the floating window has visible edges
        using Pen borderPen = new(Color.FromArgb(100, 100, 100), 1f);
        e.Graphics.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
    }

    // Allow resizing from edges even with FormBorderStyle.None
    protected override void WndProc(ref Message m)
    {
        const int WM_NCHITTEST = 0x0084;
        const int HTLEFT = 10;
        const int HTRIGHT = 11;
        const int HTTOP = 12;
        const int HTTOPLEFT = 13;
        const int HTTOPRIGHT = 14;
        const int HTBOTTOM = 15;
        const int HTBOTTOMLEFT = 16;
        const int HTBOTTOMRIGHT = 17;

        if (m.Msg == WM_NCHITTEST)
        {
            base.WndProc(ref m);
            Point pt = PointToClient(new Point(m.LParam.ToInt32()));
            int w = ClientSize.Width;
            int h = ClientSize.Height;

            bool left = pt.X < ResizeBorderWidth;
            bool right = pt.X >= w - ResizeBorderWidth;
            bool top = pt.Y < ResizeBorderWidth;
            bool bottom = pt.Y >= h - ResizeBorderWidth;

            if (top && left) m.Result = (IntPtr)HTTOPLEFT;
            else if (top && right) m.Result = (IntPtr)HTTOPRIGHT;
            else if (bottom && left) m.Result = (IntPtr)HTBOTTOMLEFT;
            else if (bottom && right) m.Result = (IntPtr)HTBOTTOMRIGHT;
            else if (left) m.Result = (IntPtr)HTLEFT;
            else if (right) m.Result = (IntPtr)HTRIGHT;
            else if (top) m.Result = (IntPtr)HTTOP;
            else if (bottom) m.Result = (IntPtr)HTBOTTOM;
            return;
        }

        base.WndProc(ref m);
    }

    public void ApplyTheme(ThemeService.ThemeProfile theme)
    {
        BackColor = theme.Surface;
        ForeColor = theme.Text;
        _hostedPanel?.ApplyTheme(theme);
    }
}
