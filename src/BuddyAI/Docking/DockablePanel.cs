using BuddyAI.Services;

namespace BuddyAI.Docking;

/// <summary>
/// A VS-style tool-window panel with a title bar that supports drag-to-float,
/// dock, pin, and close operations.  Title-bar buttons are owner-drawn with
/// GDI+ primitives so they render reliably on every font / DPI configuration.
/// </summary>
public sealed class DockablePanel : Panel
{
    private readonly TitleBarControl _titleBar;
    private readonly Panel _contentHost;

    private bool _isPinned = true;
    private bool _isAutoHide;
    private DockZone _currentZone = DockZone.None;
    private bool _isLocked;

    public string PanelTitle
    {
        get => _titleBar.TitleText;
        set => _titleBar.TitleText = value;
    }

    public DockZone CurrentZone
    {
        get => _currentZone;
        set => _currentZone = value;
    }

    public bool IsPinned
    {
        get => _isPinned;
        set
        {
            _isPinned = value;
            _titleBar.IsPinned = value;
            _titleBar.Invalidate();
        }
    }

    public bool IsAutoHide
    {
        get => _isAutoHide;
        set => _isAutoHide = value;
    }

    public bool IsLocked
    {
        get => _isLocked;
        set
        {
            _isLocked = value;
            _titleBar.IsLocked = value;
        }
    }

    public Panel ContentHost => _contentHost;

    public event EventHandler<DockablePanelDragEventArgs>? DragStarted;
    public event EventHandler<DockablePanelDragEventArgs>? DragMoved;
    public event EventHandler<DockablePanelDragEventArgs>? DragEnded;
    public event EventHandler? PanelClosed;
    public event EventHandler? FloatRequested;
    public event EventHandler? PinToggled;

    public DockablePanel()
    {
        Dock = DockStyle.Fill;
        Padding = Padding.Empty;
        Margin = Padding.Empty;

        _titleBar = new TitleBarControl
        {
            Dock = DockStyle.Top,
            Height = 26
        };

        _contentHost = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = Padding.Empty
        };

        Controls.Add(_contentHost);
        Controls.Add(_titleBar);

        _titleBar.CloseClicked += (_, __) => PanelClosed?.Invoke(this, EventArgs.Empty);
        _titleBar.FloatClicked += (_, __) => FloatRequested?.Invoke(this, EventArgs.Empty);
        _titleBar.PinClicked += (_, __) =>
        {
            IsPinned = !IsPinned;
            PinToggled?.Invoke(this, EventArgs.Empty);
        };
        _titleBar.TitleDragStarted += (_, e) => DragStarted?.Invoke(this, new DockablePanelDragEventArgs(e));
        _titleBar.TitleDragMoved += (_, e) => DragMoved?.Invoke(this, new DockablePanelDragEventArgs(e));
        _titleBar.TitleDragEnded += (_, e) =>
        {
            DragEnded?.Invoke(this, new DockablePanelDragEventArgs(e));
        };
    }

    public void ApplyTheme(ThemeService.ThemeProfile theme)
    {
        _titleBar.AccentColor = theme.Accent;
        _titleBar.TitleForeColor = theme.AccentForeground;
        _titleBar.BackColor = theme.Accent;
        _titleBar.Invalidate();

        _contentHost.BackColor = theme.Surface;
        _contentHost.ForeColor = theme.Text;
        BackColor = theme.Surface;
        ForeColor = theme.Text;
    }

    // ???????????????????????????????????????????????????????????????
    //  Owner-drawn title bar with GDI+ glyph buttons
    // ???????????????????????????????????????????????????????????????
    private sealed class TitleBarControl : Control
    {
        private const int ButtonSize = 20;
        private const int ButtonMargin = 2;
        private const int ButtonPadding = 4; // inner glyph inset

        private bool _isDragging;
        private Point _dragStart;
        private int _hoveredButton = -1; // 0=close, 1=pin, 2=float, -1=none
        private bool _isLocked;

        public string TitleText { get; set; } = string.Empty;
        public bool IsPinned { get; set; } = true;
        
        public bool IsLocked 
        { 
            get => _isLocked; 
            set 
            { 
                _isLocked = value; 
                Cursor = value ? Cursors.Default : Cursors.SizeAll;
                Invalidate();
            } 
        }

        public Color AccentColor { get; set; } = Color.FromArgb(0, 122, 204);
        public Color TitleForeColor { get; set; } = Color.White;

        public event EventHandler? CloseClicked;
        public event EventHandler? PinClicked;
        public event EventHandler? FloatClicked;
        public event EventHandler<Point>? TitleDragStarted;
        public event EventHandler<Point>? TitleDragMoved;
        public event EventHandler<Point>? TitleDragEnded;

        public TitleBarControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint
                | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw, true);
            Cursor = _isLocked ? Cursors.Default : Cursors.SizeAll;
        }

        private Rectangle GetButtonRect(int index)
        {
            // Buttons are laid out right-to-left: Close(0), Pin(1), Float(2)
            int x = Width - (ButtonSize + ButtonMargin) * (index + 1);
            int y = (Height - ButtonSize) / 2;
            return new Rectangle(x, y, ButtonSize, ButtonSize);
        }

        private int HitTestButton(Point pt)
        {
            for (int i = 0; i < 3; i++)
            {
                if (GetButtonRect(i).Contains(pt))
                    return i;
            }
            return -1;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // Background
            using SolidBrush bgBrush = new(AccentColor);
            g.FillRectangle(bgBrush, ClientRectangle);

            // Title text
            int textRight = Width - (ButtonSize + ButtonMargin) * 3 - 4;
            Rectangle textRect = new(6, 0, Math.Max(1, textRight - 6), Height);
            TextRenderer.DrawText(g, TitleText, new Font("Segoe UI", 9f, FontStyle.Bold),
                textRect, TitleForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            // Buttons
            DrawCloseButton(g, GetButtonRect(0), _hoveredButton == 0);
            DrawPinButton(g, GetButtonRect(1), _hoveredButton == 1, IsPinned);
            
            if (!IsLocked)
                DrawFloatButton(g, GetButtonRect(2), _hoveredButton == 2);
        }

        private void DrawCloseButton(Graphics g, Rectangle rect, bool hovered)
        {
            if (hovered)
            {
                using SolidBrush hoverBg = new(Color.FromArgb(232, 17, 35));
                g.FillRectangle(hoverBg, rect);
            }

            Rectangle inner = Inflate(rect, -ButtonPadding);
            using Pen pen = new(hovered ? Color.White : TitleForeColor, 1.6f);
            g.DrawLine(pen, inner.Left, inner.Top, inner.Right, inner.Bottom);
            g.DrawLine(pen, inner.Right, inner.Top, inner.Left, inner.Bottom);
        }

        private void DrawPinButton(Graphics g, Rectangle rect, bool hovered, bool pinned)
        {
            if (hovered)
            {
                using SolidBrush hoverBg = new(Color.FromArgb(60, TitleForeColor));
                g.FillRectangle(hoverBg, rect);
            }

            Rectangle inner = Inflate(rect, -ButtonPadding);
            using Pen pen = new(TitleForeColor, 1.4f);

            if (pinned)
            {
                // Vertical pin: small rect + line down
                int pinW = inner.Width / 2;
                int pinH = inner.Height * 2 / 3;
                int px = inner.X + (inner.Width - pinW) / 2;
                int py = inner.Y;
                g.DrawRectangle(pen, px, py, pinW, pinH);
                int cx = inner.X + inner.Width / 2;
                g.DrawLine(pen, cx, py + pinH, cx, inner.Bottom);
            }
            else
            {
                // Horizontal pin (rotated): small rect + line right
                int pinW = inner.Width * 2 / 3;
                int pinH = inner.Height / 2;
                int px = inner.X;
                int py = inner.Y + (inner.Height - pinH) / 2;
                g.DrawRectangle(pen, px, py, pinW, pinH);
                int cy = inner.Y + inner.Height / 2;
                g.DrawLine(pen, px + pinW, cy, inner.Right, cy);
            }
        }

        private void DrawFloatButton(Graphics g, Rectangle rect, bool hovered)
        {
            if (hovered)
            {
                using SolidBrush hoverBg = new(Color.FromArgb(60, TitleForeColor));
                g.FillRectangle(hoverBg, rect);
            }

            Rectangle inner = Inflate(rect, -ButtonPadding);
            using Pen pen = new(TitleForeColor, 1.4f);

            // Two overlapping rectangles (window restore glyph)
            int offset = 3;
            Rectangle back = new(inner.X + offset, inner.Y, inner.Width - offset, inner.Height - offset);
            Rectangle front = new(inner.X, inner.Y + offset, inner.Width - offset, inner.Height - offset);
            g.DrawRectangle(pen, back);
            using SolidBrush fillBrush = new(AccentColor);
            g.FillRectangle(fillBrush, front);
            g.DrawRectangle(pen, front);
        }

        private static Rectangle Inflate(Rectangle rect, int amount)
        {
            return new Rectangle(rect.X - amount, rect.Y - amount,
                rect.Width + amount * 2, rect.Height + amount * 2);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_isDragging)
            {
                TitleDragMoved?.Invoke(this, PointToScreen(e.Location));
                return;
            }

            if (IsLocked)
            {
                int btn = HitTestButton(e.Location);
                // In locked mode, allow Pin (1) and Close (0), disable Float (2) and Drag
                if (btn == 2 || btn == -1) 
                    btn = -1;

                if (btn != _hoveredButton)
                {
                    _hoveredButton = btn;
                    Cursor = btn >= 0 ? Cursors.Hand : Cursors.Default;
                    Invalidate();
                }
                return;
            }

            int btnFast = HitTestButton(e.Location);
            if (btnFast != _hoveredButton)
            {
                _hoveredButton = btnFast;
                Cursor = btnFast >= 0 ? Cursors.Hand : Cursors.SizeAll;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoveredButton != -1)
            {
                _hoveredButton = -1;
                Cursor = IsLocked ? Cursors.Default : Cursors.SizeAll;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;

            int btn = HitTestButton(e.Location);
            if (btn >= 0)
            {
                if (IsLocked && btn == 2) btn = -1; // float disabled
                if (btn >= 0) return; // handled on MouseUp
            }

            if (IsLocked) return;

            _isDragging = true;
            _dragStart = e.Location;
            Capture = true;
            TitleDragStarted?.Invoke(this, PointToScreen(e.Location));
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;

            if (_isDragging)
            {
                _isDragging = false;
                Capture = false;
                TitleDragEnded?.Invoke(this, PointToScreen(e.Location));
                return;
            }

            int btn = HitTestButton(e.Location);
            if (IsLocked && btn == 2) btn = -1; // float disabled

            switch (btn)
            {
                case 0: CloseClicked?.Invoke(this, EventArgs.Empty); break;
                case 1: PinClicked?.Invoke(this, EventArgs.Empty); break;
                case 2: FloatClicked?.Invoke(this, EventArgs.Empty); break;
            }
        }
    }
}

public sealed class DockablePanelDragEventArgs : EventArgs
{
    public Point ScreenLocation { get; }
    public DockablePanelDragEventArgs(Point screenLocation)
    {
        ScreenLocation = screenLocation;
    }
}
