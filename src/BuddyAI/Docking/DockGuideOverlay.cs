using BuddyAI.Services;

namespace BuddyAI.Docking;

/// <summary>
/// A translucent overlay form that shows dock-guide indicators (a cross-shaped
/// compass with zone previews) while the user is dragging a <see cref="DockablePanel"/>.
/// Mimics the Visual Studio dock-guide UX.  All glyphs are GDI+ drawn so they
/// render correctly on every font / DPI configuration.
/// </summary>
internal sealed class DockGuideOverlay : Form
{
    private DockZone _highlightedZone = DockZone.None;
    private Rectangle _previewRect;
    private readonly Rectangle[] _guideRects = new Rectangle[5]; // Left, Right, Top, Bottom, Center
    private readonly Rectangle[] _guideIconRects = new Rectangle[5];
    private ThemeService.ThemeProfile _theme = ThemeService.VisualStudioDark;

    private const int GuideSize = 36;
    private const int GuideGap = 4;
    private const int IconSize = 28;

    public DockZone HighlightedZone => _highlightedZone;
    public Rectangle PreviewRect => _previewRect;

    public DockGuideOverlay()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.Magenta;
        TransparencyKey = Color.Magenta;
        DoubleBuffered = true;
        Opacity = 0.85;
    }

    public void ShowOverlay(Rectangle ownerBounds)
    {
        Bounds = ownerBounds;
        RecalculateGuides();
        if (!Visible) Show();
        Invalidate();
    }

    public void HideOverlay()
    {
        _highlightedZone = DockZone.None;
        _previewRect = Rectangle.Empty;
        Hide();
    }

    public DockZone HitTest(Point screenPoint)
    {
        Point local = PointToClient(screenPoint);

        DockZone[] zones = { DockZone.Left, DockZone.Right, DockZone.Top, DockZone.Bottom, DockZone.Center };
        for (int i = 0; i < zones.Length; i++)
        {
            if (_guideRects[i].Contains(local))
            {
                if (_highlightedZone != zones[i])
                {
                    _highlightedZone = zones[i];
                    _previewRect = ComputePreviewRect(zones[i]);
                    Invalidate();
                }
                return zones[i];
            }
        }

        if (_highlightedZone != DockZone.None)
        {
            _highlightedZone = DockZone.None;
            _previewRect = Rectangle.Empty;
            Invalidate();
        }

        return DockZone.None;
    }

    public void ApplyTheme(ThemeService.ThemeProfile theme)
    {
        _theme = theme;
        Invalidate();
    }

    private void RecalculateGuides()
    {
        int cx = ClientSize.Width / 2;
        int cy = ClientSize.Height / 2;

        // Center
        _guideRects[4] = new Rectangle(cx - GuideSize / 2, cy - GuideSize / 2, GuideSize, GuideSize);
        _guideIconRects[4] = Deflate(_guideRects[4], (GuideSize - IconSize) / 2);

        // Left
        _guideRects[0] = new Rectangle(cx - GuideSize / 2 - GuideSize - GuideGap, cy - GuideSize / 2, GuideSize, GuideSize);
        _guideIconRects[0] = Deflate(_guideRects[0], (GuideSize - IconSize) / 2);

        // Right
        _guideRects[1] = new Rectangle(cx + GuideSize / 2 + GuideGap, cy - GuideSize / 2, GuideSize, GuideSize);
        _guideIconRects[1] = Deflate(_guideRects[1], (GuideSize - IconSize) / 2);

        // Top
        _guideRects[2] = new Rectangle(cx - GuideSize / 2, cy - GuideSize / 2 - GuideSize - GuideGap, GuideSize, GuideSize);
        _guideIconRects[2] = Deflate(_guideRects[2], (GuideSize - IconSize) / 2);

        // Bottom
        _guideRects[3] = new Rectangle(cx - GuideSize / 2, cy + GuideSize / 2 + GuideGap, GuideSize, GuideSize);
        _guideIconRects[3] = Deflate(_guideRects[3], (GuideSize - IconSize) / 2);
    }

    private Rectangle ComputePreviewRect(DockZone zone)
    {
        int w = ClientSize.Width;
        int h = ClientSize.Height;
        int third = w / 3;
        int thirdH = h / 3;

        return zone switch
        {
            DockZone.Left => new Rectangle(0, 0, third, h),
            DockZone.Right => new Rectangle(w - third, 0, third, h),
            DockZone.Top => new Rectangle(0, 0, w, thirdH),
            DockZone.Bottom => new Rectangle(0, h - thirdH, w, thirdH),
            DockZone.Center => new Rectangle(0, 0, w, h),
            _ => Rectangle.Empty
        };
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        // Draw preview highlight
        if (_highlightedZone != DockZone.None && !_previewRect.IsEmpty)
        {
            using SolidBrush previewBrush = new(Color.FromArgb(60, _theme.Accent));
            using Pen previewPen = new(Color.FromArgb(140, _theme.Accent), 2f);
            g.FillRectangle(previewBrush, _previewRect);
            g.DrawRectangle(previewPen, _previewRect);
        }

        // Draw the compass background
        Rectangle compassBounds = GetCompassBounds();
        using SolidBrush compassBg = new(Color.FromArgb(220, 240, 240, 240));
        using Pen compassBorder = new(Color.FromArgb(180, 180, 180), 1f);
        DrawRoundedRect(g, compassBg, compassBorder, compassBounds, 6);

        // Draw guide buttons
        DockZone[] zones = { DockZone.Left, DockZone.Right, DockZone.Top, DockZone.Bottom, DockZone.Center };
        for (int i = 0; i < zones.Length; i++)
        {
            bool isHighlighted = _highlightedZone == zones[i];
            Color bgColor = isHighlighted ? _theme.Accent : Color.FromArgb(230, 255, 255, 255);
            Color fgColor = isHighlighted ? Color.White : Color.FromArgb(80, 80, 80);
            Color borderColor = isHighlighted ? _theme.Accent : Color.FromArgb(160, 160, 160);

            using SolidBrush bg = new(bgColor);
            using Pen border = new(borderColor, 1f);
            DrawRoundedRect(g, bg, border, _guideRects[i], 4);

            // Mini panel preview
            DrawPanelPreviewIcon(g, _guideIconRects[i], zones[i], isHighlighted, fgColor, borderColor);

            // Arrow glyph (GDI+ drawn)
            DrawArrowGlyph(g, _guideRects[i], zones[i], fgColor);
        }
    }

    private static void DrawArrowGlyph(Graphics g, Rectangle rect, DockZone zone, Color color)
    {
        using Pen pen = new(color, 2f);
        int cx = rect.X + rect.Width / 2;
        int cy = rect.Y + rect.Height / 2;
        int arrowLen = 6;
        int headLen = 4;

        switch (zone)
        {
            case DockZone.Left:
                g.DrawLine(pen, cx + arrowLen, cy, cx - arrowLen, cy);
                g.DrawLine(pen, cx - arrowLen, cy, cx - arrowLen + headLen, cy - headLen);
                g.DrawLine(pen, cx - arrowLen, cy, cx - arrowLen + headLen, cy + headLen);
                break;
            case DockZone.Right:
                g.DrawLine(pen, cx - arrowLen, cy, cx + arrowLen, cy);
                g.DrawLine(pen, cx + arrowLen, cy, cx + arrowLen - headLen, cy - headLen);
                g.DrawLine(pen, cx + arrowLen, cy, cx + arrowLen - headLen, cy + headLen);
                break;
            case DockZone.Top:
                g.DrawLine(pen, cx, cy + arrowLen, cx, cy - arrowLen);
                g.DrawLine(pen, cx, cy - arrowLen, cx - headLen, cy - arrowLen + headLen);
                g.DrawLine(pen, cx, cy - arrowLen, cx + headLen, cy - arrowLen + headLen);
                break;
            case DockZone.Bottom:
                g.DrawLine(pen, cx, cy - arrowLen, cx, cy + arrowLen);
                g.DrawLine(pen, cx, cy + arrowLen, cx - headLen, cy + arrowLen - headLen);
                g.DrawLine(pen, cx, cy + arrowLen, cx + headLen, cy + arrowLen - headLen);
                break;
            case DockZone.Center:
                // Plus / cross-hair glyph
                g.DrawLine(pen, cx - arrowLen, cy, cx + arrowLen, cy);
                g.DrawLine(pen, cx, cy - arrowLen, cx, cy + arrowLen);
                break;
        }
    }

    private void DrawPanelPreviewIcon(Graphics g, Rectangle rect, DockZone zone, bool highlighted, Color fg, Color border)
    {
        Color fill = highlighted ? Color.FromArgb(60, Color.White) : Color.FromArgb(40, 100, 149, 237);
        using SolidBrush fillBrush = new(fill);
        using Pen borderPen = new(Color.FromArgb(120, border), 1f);

        Rectangle inner = new(rect.X + 2, rect.Y + 2, rect.Width - 4, rect.Height - 4);
        g.DrawRectangle(borderPen, inner);

        Rectangle highlight = zone switch
        {
            DockZone.Left => new Rectangle(inner.X, inner.Y, inner.Width / 3, inner.Height),
            DockZone.Right => new Rectangle(inner.Right - inner.Width / 3, inner.Y, inner.Width / 3, inner.Height),
            DockZone.Top => new Rectangle(inner.X, inner.Y, inner.Width, inner.Height / 3),
            DockZone.Bottom => new Rectangle(inner.X, inner.Bottom - inner.Height / 3, inner.Width, inner.Height / 3),
            DockZone.Center => inner,
            _ => Rectangle.Empty
        };

        if (!highlight.IsEmpty)
            g.FillRectangle(fillBrush, highlight);
    }

    private Rectangle GetCompassBounds()
    {
        int cx = ClientSize.Width / 2;
        int cy = ClientSize.Height / 2;
        int extent = GuideSize + GuideGap + GuideSize / 2 + 6;
        return new Rectangle(cx - extent, cy - extent, extent * 2, extent * 2);
    }

    private static void DrawRoundedRect(Graphics g, Brush fill, Pen border, Rectangle rect, int radius)
    {
        using System.Drawing.Drawing2D.GraphicsPath path = new();
        int d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        g.FillPath(fill, path);
        g.DrawPath(border, path);
    }

    private static Rectangle Deflate(Rectangle rect, int amount)
    {
        return new Rectangle(rect.X + amount, rect.Y + amount,
            Math.Max(1, rect.Width - amount * 2),
            Math.Max(1, rect.Height - amount * 2));
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= 0x00000020; // WS_EX_TRANSPARENT for click-through on magenta areas
            return cp;
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        // Prevent the overlay itself from capturing focus
    }
}
