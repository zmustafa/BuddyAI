using System.Drawing.Drawing2D;

namespace BuddyAI.Helpers;

/// <summary>
/// Renders a curated subset of Tabler Icons (https://github.com/tabler/tabler-icons)
/// as GDI+ bitmaps at any size and color. Icons are defined as SVG-style path segments
/// and drawn using <see cref="GraphicsPath"/> so they scale cleanly across DPI settings.
///
/// Tabler Icons are licensed under the MIT License.
/// Copyright (c) 2020-2024 Pawe? Kuna
/// </summary>
internal static class TablerIconRenderer
{
    // ??????????????????????????????????????????????????????????????
    //  Public API
    // ??????????????????????????????????????????????????????????????

    /// <summary>
    /// Renders the requested icon as a bitmap of the given size and color.
    /// A small inset is applied so strokes are not clipped at bitmap edges.
    /// </summary>
    public static Bitmap Render(TablerIcon icon, int size, Color color)
    {
        Bitmap bitmap = new(size, size);
        using Graphics g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);

        int inset = Math.Max(1, size / 12);
        Rectangle inner = new(inset, inset, size - inset * 2, size - inset * 2);
        Draw(g, icon, inner, color);
        return bitmap;
    }

    /// <summary>
    /// Draws the requested icon into the given rectangle on an existing Graphics surface.
    /// </summary>
    public static void Draw(Graphics g, TablerIcon icon, Rectangle bounds, Color color)
    {
        Action<Graphics, RectangleF, Color> painter = GetPainter(icon);
        painter(g, bounds, color);
    }

    /// <summary>
    /// Creates an <see cref="Image"/> suitable for assigning to a Button.Image,
    /// ToolStripButton.Image, or similar control property.
    /// </summary>
    public static Image CreateImage(TablerIcon icon, int size, Color color)
        => Render(icon, size, color);

    // ??????????????????????????????????????????????????????????????
    //  Icon definitions – each painter draws inside a 0..1 unit square
    //  that is mapped to the target rectangle by the caller.
    //
    //  The shapes below are hand-translated from the official Tabler
    //  SVG icon source (24×24 viewBox) to normalised GDI+ primitives.
    // ??????????????????????????????????????????????????????????????

    private static Action<Graphics, RectangleF, Color> GetPainter(TablerIcon icon) => icon switch
    {
        TablerIcon.Plus => PaintPlus,
        TablerIcon.Photo => PaintPhoto,
        TablerIcon.Screenshot => PaintScreenshot,
        TablerIcon.Eraser => PaintEraser,
        TablerIcon.Send => PaintSend,
        TablerIcon.ArrowForwardUp => PaintArrowForwardUp,
        TablerIcon.X => PaintX,
        TablerIcon.Keyboard => PaintKeyboard,
        TablerIcon.AlertTriangle => PaintAlertTriangle,
        TablerIcon.Loader => PaintLoader,
        TablerIcon.FileText => PaintFileText,
        TablerIcon.Message => PaintMessage,
        TablerIcon.Pin => PaintPin,
        TablerIcon.StarFilled => PaintStarFilled,
        TablerIcon.Copy => PaintCopy,
        TablerIcon.ClipboardText => PaintClipboardText,
        TablerIcon.Trash => PaintTrash,
        TablerIcon.Refresh => PaintRefresh,
        TablerIcon.FileExport => PaintFileExport,
        TablerIcon.ExternalLink => PaintExternalLink,
        TablerIcon.Search => PaintSearch,
        TablerIcon.ClearAll => PaintClearAll,
        TablerIcon.Edit => PaintEdit,
        TablerIcon.LayoutDashboard => PaintLayoutDashboard,
        TablerIcon.Settings => PaintSettings,
        TablerIcon.DeviceFloppy => PaintDeviceFloppy,
        TablerIcon.FolderOpen => PaintFolderOpen,
        TablerIcon.Download => PaintDownload,
        TablerIcon.Upload => PaintUpload,
        TablerIcon.PlayerPlay => PaintPlayerPlay,
        TablerIcon.InfoCircle => PaintInfoCircle,
        TablerIcon.Bug => PaintBug,
        TablerIcon.Wand => PaintWand,
        TablerIcon.ListDetails => PaintListDetails,
        TablerIcon.History => PaintHistory,
        TablerIcon.Bulb => PaintBulb,
        TablerIcon.Tools => PaintTools,
        TablerIcon.BrandOpenai => PaintBrandOpenai,
        TablerIcon.ChevronRight => PaintChevronRight,
        TablerIcon.Clipboard => PaintClipboard,
        TablerIcon.SquareRoundedPlus => PaintSquareRoundedPlus,
        TablerIcon.WindowMaximize => PaintWindowMaximize,
        TablerIcon.ReportAnalytics => PaintReportAnalytics,
        TablerIcon.ArrowsMaximize => PaintArrowsMaximize,
        TablerIcon.ArrowsMinimize => PaintArrowsMinimize,
        TablerIcon.Cut => PaintCut,
        TablerIcon.ArrowBackUp => PaintArrowBackUp,
        TablerIcon.ArrowForwardUpDouble => PaintArrowForwardUpDouble,
        TablerIcon.Pencil => PaintPencil,
        TablerIcon.Highlight => PaintHighlight,
        TablerIcon.ColorPicker => PaintColorPicker,
        TablerIcon.LayersIntersect => PaintLayersIntersect,
        TablerIcon.ArrowsMove => PaintArrowsMove,
        TablerIcon.Clock => PaintClock,
        TablerIcon.Line => PaintLine,
        TablerIcon.Square => PaintSquare,
        TablerIcon.Circle => PaintCircle,
        TablerIcon.ArrowNarrowRight => PaintArrowNarrowRight,
        TablerIcon.LetterA => PaintLetterA,
        TablerIcon.PaintBucket => PaintPaintBucket,
        TablerIcon.ColorSwatch => PaintColorSwatch,
        TablerIcon.SelectionDrag => PaintSelectionDrag,
        TablerIcon.CropIcon => PaintCropIcon,
        TablerIcon.ZoomIn => PaintZoomIn,
        TablerIcon.ZoomOut => PaintZoomOut,
        TablerIcon.Diameter => PaintDiameter,
        _ => PaintPlus
    };

    // ?? Helpers ??????????????????????????????????????????????????

    /// <summary>Maps normalised 0..24 coords into the target rectangle.</summary>
    private static PointF Map(RectangleF r, float x24, float y24)
    {
        float nx = x24 / 24f;
        float ny = y24 / 24f;
        return new PointF(r.X + nx * r.Width, r.Y + ny * r.Height);
    }

    private static float Scale(RectangleF r, float v24) => (v24 / 24f) * Math.Min(r.Width, r.Height);

    private static Pen MakePen(Color c, RectangleF r, float strokeWidth = 2.5f)
    {
        return new Pen(c, Scale(r, strokeWidth))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
    }

    // ?? Icon painters ???????????????????????????????????????????

    // tabler:plus
    private static void PaintPlus(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        g.DrawLine(pen, Map(r, 12, 5), Map(r, 12, 19));
        g.DrawLine(pen, Map(r, 5, 12), Map(r, 19, 12));
    }

    // tabler:photo
    private static void PaintPhoto(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        // frame
        DrawRoundedRect(g, pen, r, 4, 4, 20, 20, 2);
        // mountain line
        g.DrawLine(pen, Map(r, 4, 15), Map(r, 8, 11));
        g.DrawLine(pen, Map(r, 8, 11), Map(r, 13, 16));
        g.DrawLine(pen, Map(r, 13, 16), Map(r, 16, 13));
        g.DrawLine(pen, Map(r, 16, 13), Map(r, 20, 17));
        // sun
        float sunR = Scale(r, 1.2f);
        PointF sun = Map(r, 14.5f, 8.5f);
        using SolidBrush fill = new(c);
        g.FillEllipse(fill, sun.X - sunR, sun.Y - sunR, sunR * 2, sunR * 2);
    }

    // tabler:screenshot
    private static void PaintScreenshot(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        // four corner brackets
        g.DrawLine(pen, Map(r, 7, 4), Map(r, 4, 4));
        g.DrawLine(pen, Map(r, 4, 4), Map(r, 4, 7));

        g.DrawLine(pen, Map(r, 17, 4), Map(r, 20, 4));
        g.DrawLine(pen, Map(r, 20, 4), Map(r, 20, 7));

        g.DrawLine(pen, Map(r, 7, 20), Map(r, 4, 20));
        g.DrawLine(pen, Map(r, 4, 20), Map(r, 4, 17));

        g.DrawLine(pen, Map(r, 17, 20), Map(r, 20, 20));
        g.DrawLine(pen, Map(r, 20, 20), Map(r, 20, 17));

        // center circle
        float cr = Scale(r, 3f);
        PointF center = Map(r, 12, 12);
        g.DrawEllipse(pen, center.X - cr, center.Y - cr, cr * 2, cr * 2);
    }

    // tabler:eraser
    private static void PaintEraser(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        // simplified eraser shape
        g.DrawLine(pen, Map(r, 19, 20), Map(r, 5, 20));
        g.DrawLine(pen, Map(r, 5, 20), Map(r, 5, 16));
        g.DrawLine(pen, Map(r, 5, 16), Map(r, 14, 7));
        g.DrawLine(pen, Map(r, 14, 7), Map(r, 19, 12));
        g.DrawLine(pen, Map(r, 19, 12), Map(r, 10, 20));
        // handle line
        g.DrawLine(pen, Map(r, 9, 16), Map(r, 14, 11));
    }

    // tabler:send
    private static void PaintSend(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        // paper plane
        g.DrawLine(pen, Map(r, 10, 14), Map(r, 21, 3));
        g.DrawLine(pen, Map(r, 21, 3), Map(r, 3, 11));
        g.DrawLine(pen, Map(r, 3, 11), Map(r, 10, 14));
        g.DrawLine(pen, Map(r, 10, 14), Map(r, 10, 21));
        g.DrawLine(pen, Map(r, 10, 21), Map(r, 14, 17));
        g.DrawLine(pen, Map(r, 21, 3), Map(r, 14, 17));
    }

    // tabler:arrow-forward-up
    private static void PaintArrowForwardUp(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        g.DrawLine(pen, Map(r, 15, 13), Map(r, 19, 9));
        g.DrawLine(pen, Map(r, 19, 9), Map(r, 15, 5));
        // curved path
        g.DrawLine(pen, Map(r, 19, 9), Map(r, 11, 9));
        float arcR = Scale(r, 4);
        PointF arcCenter = Map(r, 11, 13);
        g.DrawArc(pen, arcCenter.X - arcR, arcCenter.Y - arcR, arcR * 2, arcR * 2, 270, -90);
        g.DrawLine(pen, Map(r, 7, 13), Map(r, 7, 20));
    }

    // tabler:x
    private static void PaintX(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        g.DrawLine(pen, Map(r, 6, 6), Map(r, 18, 18));
        g.DrawLine(pen, Map(r, 6, 18), Map(r, 18, 6));
    }

    // tabler:keyboard
    private static void PaintKeyboard(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        DrawRoundedRect(g, pen, r, 3, 6, 21, 18, 2);
        // key dots
        float dot = Scale(r, 0.8f);
        using SolidBrush fill = new(c);
        void Dot(float x, float y) { PointF p = Map(r, x, y); g.FillEllipse(fill, p.X - dot, p.Y - dot, dot * 2, dot * 2); }
        Dot(7, 10); Dot(10, 10); Dot(14, 10); Dot(17, 10);
        Dot(7, 13); Dot(10, 13); Dot(14, 13); Dot(17, 13);
        // space bar
        g.DrawLine(pen, Map(r, 9, 16), Map(r, 15, 16));
    }

    // tabler:alert-triangle
    private static void PaintAlertTriangle(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        // triangle
        g.DrawLine(pen, Map(r, 12, 4), Map(r, 21, 20));
        g.DrawLine(pen, Map(r, 21, 20), Map(r, 3, 20));
        g.DrawLine(pen, Map(r, 3, 20), Map(r, 12, 4));
        // exclamation mark
        g.DrawLine(pen, Map(r, 12, 10), Map(r, 12, 14));
        float dotR = Scale(r, 0.9f);
        PointF dotP = Map(r, 12, 17);
        using SolidBrush fill = new(c);
        g.FillEllipse(fill, dotP.X - dotR, dotP.Y - dotR, dotR * 2, dotR * 2);
    }

    // tabler:loader (simplified spinner)
    private static void PaintLoader(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        PointF center = Map(r, 12, 12);
        float outerR = Scale(r, 7);
        // draw arc segments
        g.DrawArc(pen, center.X - outerR, center.Y - outerR, outerR * 2, outerR * 2, 0, 60);
        g.DrawArc(pen, center.X - outerR, center.Y - outerR, outerR * 2, outerR * 2, 120, 60);
        g.DrawArc(pen, center.X - outerR, center.Y - outerR, outerR * 2, outerR * 2, 240, 60);
    }

    // tabler:file-text
    private static void PaintFileText(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        // file outline with folded corner
        g.DrawLine(pen, Map(r, 6, 4), Map(r, 14, 4));
        g.DrawLine(pen, Map(r, 14, 4), Map(r, 18, 8));
        g.DrawLine(pen, Map(r, 18, 8), Map(r, 18, 20));
        g.DrawLine(pen, Map(r, 18, 20), Map(r, 6, 20));
        g.DrawLine(pen, Map(r, 6, 20), Map(r, 6, 4));
        // text lines
        g.DrawLine(pen, Map(r, 9, 12), Map(r, 15, 12));
        g.DrawLine(pen, Map(r, 9, 15), Map(r, 15, 15));
    }

    // tabler:message
    private static void PaintMessage(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        // chat bubble
        g.DrawLine(pen, Map(r, 4, 5), Map(r, 20, 5));
        g.DrawLine(pen, Map(r, 20, 5), Map(r, 20, 15));
        g.DrawLine(pen, Map(r, 20, 15), Map(r, 13, 15));
        g.DrawLine(pen, Map(r, 13, 15), Map(r, 8, 20));
        g.DrawLine(pen, Map(r, 8, 20), Map(r, 8, 15));
        g.DrawLine(pen, Map(r, 8, 15), Map(r, 4, 15));
        g.DrawLine(pen, Map(r, 4, 15), Map(r, 4, 5));
    }

    // tabler:pin (pushpin / thumbtack)
    private static void PaintPin(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        g.DrawLine(pen, Map(r, 9, 4), Map(r, 15, 4));
        g.DrawLine(pen, Map(r, 15, 4), Map(r, 15, 10));
        g.DrawLine(pen, Map(r, 15, 10), Map(r, 18, 13));
        g.DrawLine(pen, Map(r, 18, 13), Map(r, 6, 13));
        g.DrawLine(pen, Map(r, 6, 13), Map(r, 9, 10));
        g.DrawLine(pen, Map(r, 9, 10), Map(r, 9, 4));
        // pin needle
        g.DrawLine(pen, Map(r, 12, 13), Map(r, 12, 21));
    }

    // tabler:star-filled
    private static void PaintStarFilled(Graphics g, RectangleF r, Color c)
    {
        // 5-point star
        PointF[] star = MakeStarPoints(Map(r, 12, 12), Scale(r, 8), Scale(r, 3.5f), 5);
        using SolidBrush fill = new(c);
        g.FillPolygon(fill, star);
    }

    // tabler:copy
    private static void PaintCopy(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        DrawRoundedRect(g, pen, r, 8, 4, 20, 16, 1.5f);
        // back page
        g.DrawLine(pen, Map(r, 16, 16), Map(r, 16, 20));
        g.DrawLine(pen, Map(r, 16, 20), Map(r, 4, 20));
        g.DrawLine(pen, Map(r, 4, 20), Map(r, 4, 8));
        g.DrawLine(pen, Map(r, 4, 8), Map(r, 8, 8));
    }

    // tabler:clipboard-text
    private static void PaintClipboardText(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        DrawRoundedRect(g, pen, r, 5, 4, 19, 20, 2);
        // clip
        g.DrawLine(pen, Map(r, 10, 4), Map(r, 10, 6));
        g.DrawLine(pen, Map(r, 10, 6), Map(r, 14, 6));
        g.DrawLine(pen, Map(r, 14, 6), Map(r, 14, 4));
        // text
        g.DrawLine(pen, Map(r, 9, 10), Map(r, 15, 10));
        g.DrawLine(pen, Map(r, 9, 13), Map(r, 15, 13));
        g.DrawLine(pen, Map(r, 9, 16), Map(r, 13, 16));
    }

    // tabler:trash
    private static void PaintTrash(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        g.DrawLine(pen, Map(r, 4, 7), Map(r, 20, 7));
        g.DrawLine(pen, Map(r, 6, 7), Map(r, 7, 20));
        g.DrawLine(pen, Map(r, 7, 20), Map(r, 17, 20));
        g.DrawLine(pen, Map(r, 17, 20), Map(r, 18, 7));
        // lid
        g.DrawLine(pen, Map(r, 9, 4), Map(r, 15, 4));
        g.DrawLine(pen, Map(r, 9, 4), Map(r, 9, 7));
        g.DrawLine(pen, Map(r, 15, 4), Map(r, 15, 7));
        // lines inside
        g.DrawLine(pen, Map(r, 10, 11), Map(r, 10, 17));
        g.DrawLine(pen, Map(r, 14, 11), Map(r, 14, 17));
    }

    // tabler:refresh
    private static void PaintRefresh(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        PointF center = Map(r, 12, 12);
        float rad = Scale(r, 6);
        g.DrawArc(pen, center.X - rad, center.Y - rad, rad * 2, rad * 2, 200, 260);
        // arrow head
        g.DrawLine(pen, Map(r, 19, 7), Map(r, 19, 3));
        g.DrawLine(pen, Map(r, 19, 7), Map(r, 15, 7));
    }

    // tabler:file-export
    private static void PaintFileExport(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        g.DrawLine(pen, Map(r, 6, 4), Map(r, 14, 4));
        g.DrawLine(pen, Map(r, 14, 4), Map(r, 18, 8));
        g.DrawLine(pen, Map(r, 18, 8), Map(r, 18, 14));
        g.DrawLine(pen, Map(r, 6, 20), Map(r, 6, 4));
        g.DrawLine(pen, Map(r, 6, 20), Map(r, 18, 20));
        // arrow out
        g.DrawLine(pen, Map(r, 14, 17), Map(r, 22, 17));
        g.DrawLine(pen, Map(r, 20, 15), Map(r, 22, 17));
        g.DrawLine(pen, Map(r, 20, 19), Map(r, 22, 17));
    }

    // tabler:external-link
    private static void PaintExternalLink(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        g.DrawLine(pen, Map(r, 12, 6), Map(r, 19, 6));
        g.DrawLine(pen, Map(r, 19, 6), Map(r, 19, 13));
        g.DrawLine(pen, Map(r, 19, 6), Map(r, 11, 14));
        // rounded rect
        g.DrawLine(pen, Map(r, 16, 16), Map(r, 16, 18));
        g.DrawLine(pen, Map(r, 16, 18), Map(r, 6, 18));
        g.DrawLine(pen, Map(r, 6, 18), Map(r, 6, 8));
        g.DrawLine(pen, Map(r, 6, 8), Map(r, 8, 8));
    }

    // tabler:search
    private static void PaintSearch(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        PointF center = Map(r, 11, 11);
        float rad = Scale(r, 5);
        g.DrawEllipse(pen, center.X - rad, center.Y - rad, rad * 2, rad * 2);
        g.DrawLine(pen, Map(r, 15, 15), Map(r, 20, 20));
    }

    // tabler:clear-all
    private static void PaintClearAll(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        g.DrawLine(pen, Map(r, 8, 6), Map(r, 20, 6));
        g.DrawLine(pen, Map(r, 6, 10), Map(r, 20, 10));
        g.DrawLine(pen, Map(r, 4, 14), Map(r, 20, 14));
        g.DrawLine(pen, Map(r, 4, 18), Map(r, 14, 18));
        // X on bottom-right
        g.DrawLine(pen, Map(r, 16, 16), Map(r, 20, 20));
        g.DrawLine(pen, Map(r, 20, 16), Map(r, 16, 20));
    }

    // tabler:edit (pencil)
    private static void PaintEdit(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        // pencil body
        g.DrawLine(pen, Map(r, 7, 20), Map(r, 4, 20));
        g.DrawLine(pen, Map(r, 4, 20), Map(r, 4, 17));
        g.DrawLine(pen, Map(r, 4, 17), Map(r, 16, 5));
        g.DrawLine(pen, Map(r, 16, 5), Map(r, 19, 8));
        g.DrawLine(pen, Map(r, 19, 8), Map(r, 7, 20));
        // crossbar
        g.DrawLine(pen, Map(r, 14, 7), Map(r, 17, 10));
    }

    // tabler:layout-dashboard
    private static void PaintLayoutDashboard(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        // left column
        DrawRoundedRect(g, pen, r, 4, 4, 10, 20, 1.5f);
        // top right
        DrawRoundedRect(g, pen, r, 14, 4, 20, 11, 1.5f);
        // bottom right
        DrawRoundedRect(g, pen, r, 14, 14, 20, 20, 1.5f);
    }

    // tabler:settings (gear)
    private static void PaintSettings(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        PointF center = Map(r, 12, 12);
        float inner = Scale(r, 3);
        float outer = Scale(r, 6.5f);
        g.DrawEllipse(pen, center.X - inner, center.Y - inner, inner * 2, inner * 2);
        // gear teeth (simplified as 6 ticks)
        for (int i = 0; i < 6; i++)
        {
            double angle = i * Math.PI / 3;
            PointF a = new(center.X + (float)(Math.Cos(angle) * (inner + Scale(r, 1))), center.Y + (float)(Math.Sin(angle) * (inner + Scale(r, 1))));
            PointF b = new(center.X + (float)(Math.Cos(angle) * outer), center.Y + (float)(Math.Sin(angle) * outer));
            g.DrawLine(pen, a, b);
        }
    }

    // tabler:device-floppy
    private static void PaintDeviceFloppy(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        // outer
        g.DrawLine(pen, Map(r, 5, 4), Map(r, 16, 4));
        g.DrawLine(pen, Map(r, 16, 4), Map(r, 19, 7));
        g.DrawLine(pen, Map(r, 19, 7), Map(r, 19, 20));
        g.DrawLine(pen, Map(r, 19, 20), Map(r, 5, 20));
        g.DrawLine(pen, Map(r, 5, 20), Map(r, 5, 4));
        // top slot
        g.DrawLine(pen, Map(r, 8, 4), Map(r, 8, 8));
        g.DrawLine(pen, Map(r, 8, 8), Map(r, 15, 8));
        g.DrawLine(pen, Map(r, 15, 8), Map(r, 15, 4));
        // bottom slot
        g.DrawLine(pen, Map(r, 8, 14), Map(r, 16, 14));
    }

    // tabler:folder-open
    private static void PaintFolderOpen(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        g.DrawLine(pen, Map(r, 4, 7), Map(r, 10, 7));
        g.DrawLine(pen, Map(r, 10, 7), Map(r, 12, 5));
        g.DrawLine(pen, Map(r, 12, 5), Map(r, 20, 5));
        g.DrawLine(pen, Map(r, 20, 5), Map(r, 20, 10));
        g.DrawLine(pen, Map(r, 20, 10), Map(r, 22, 10));
        g.DrawLine(pen, Map(r, 22, 10), Map(r, 18, 19));
        g.DrawLine(pen, Map(r, 18, 19), Map(r, 4, 19));
        g.DrawLine(pen, Map(r, 4, 19), Map(r, 4, 7));
    }

    // tabler:download
    private static void PaintDownload(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        g.DrawLine(pen, Map(r, 4, 17), Map(r, 4, 19));
        g.DrawLine(pen, Map(r, 4, 19), Map(r, 20, 19));
        g.DrawLine(pen, Map(r, 20, 19), Map(r, 20, 17));
        g.DrawLine(pen, Map(r, 12, 4), Map(r, 12, 15));
        g.DrawLine(pen, Map(r, 8, 11), Map(r, 12, 15));
        g.DrawLine(pen, Map(r, 16, 11), Map(r, 12, 15));
    }

    // tabler:upload
    private static void PaintUpload(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        g.DrawLine(pen, Map(r, 4, 17), Map(r, 4, 19));
        g.DrawLine(pen, Map(r, 4, 19), Map(r, 20, 19));
        g.DrawLine(pen, Map(r, 20, 19), Map(r, 20, 17));
        g.DrawLine(pen, Map(r, 12, 15), Map(r, 12, 4));
        g.DrawLine(pen, Map(r, 8, 8), Map(r, 12, 4));
        g.DrawLine(pen, Map(r, 16, 8), Map(r, 12, 4));
    }

    // tabler:player-play
    private static void PaintPlayerPlay(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        PointF[] triangle = { Map(r, 7, 5), Map(r, 19, 12), Map(r, 7, 19) };
        g.DrawPolygon(pen, triangle);
    }

    // tabler:info-circle
    private static void PaintInfoCircle(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        PointF center = Map(r, 12, 12);
        float rad = Scale(r, 7);
        g.DrawEllipse(pen, center.X - rad, center.Y - rad, rad * 2, rad * 2);
        g.DrawLine(pen, Map(r, 12, 11), Map(r, 12, 16));
        float dotR = Scale(r, 0.8f);
        PointF dotP = Map(r, 12, 8.5f);
        using SolidBrush fill = new(c);
        g.FillEllipse(fill, dotP.X - dotR, dotP.Y - dotR, dotR * 2, dotR * 2);
    }

    // tabler:bug
    private static void PaintBug(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        // body
        PointF center = Map(r, 12, 14);
        float rx = Scale(r, 4);
        float ry = Scale(r, 5);
        g.DrawEllipse(pen, center.X - rx, center.Y - ry, rx * 2, ry * 2);
        g.DrawLine(pen, Map(r, 12, 9), Map(r, 12, 19));
        // antennae
        g.DrawLine(pen, Map(r, 10, 10), Map(r, 7, 5));
        g.DrawLine(pen, Map(r, 14, 10), Map(r, 17, 5));
        // legs
        g.DrawLine(pen, Map(r, 8, 12), Map(r, 4, 11));
        g.DrawLine(pen, Map(r, 16, 12), Map(r, 20, 11));
        g.DrawLine(pen, Map(r, 8, 17), Map(r, 4, 18));
        g.DrawLine(pen, Map(r, 16, 17), Map(r, 20, 18));
    }

    // tabler:wand
    private static void PaintWand(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        g.DrawLine(pen, Map(r, 5, 19), Map(r, 14, 10));
        g.DrawLine(pen, Map(r, 14, 10), Map(r, 16, 12));
        g.DrawLine(pen, Map(r, 16, 12), Map(r, 7, 21));
        g.DrawLine(pen, Map(r, 7, 21), Map(r, 5, 19));
        // sparkles
        float sR = Scale(r, 0.7f);
        using SolidBrush fill = new(c);
        PointF s1 = Map(r, 16, 5);
        PointF s2 = Map(r, 20, 4);
        PointF s3 = Map(r, 19, 8);
        g.FillEllipse(fill, s1.X - sR, s1.Y - sR, sR * 2, sR * 2);
        g.FillEllipse(fill, s2.X - sR, s2.Y - sR, sR * 2, sR * 2);
        g.FillEllipse(fill, s3.X - sR, s3.Y - sR, sR * 2, sR * 2);
    }

    // tabler:list-details
    private static void PaintListDetails(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        g.DrawLine(pen, Map(r, 10, 6), Map(r, 20, 6));
        g.DrawLine(pen, Map(r, 10, 10), Map(r, 20, 10));
        g.DrawLine(pen, Map(r, 10, 14), Map(r, 20, 14));
        g.DrawLine(pen, Map(r, 10, 18), Map(r, 20, 18));
        // bullets
        float bR = Scale(r, 1f);
        using SolidBrush fill = new(c);
        foreach (float y in new[] { 6f, 10f, 14f, 18f })
        {
            PointF p = Map(r, 5.5f, y);
            g.FillEllipse(fill, p.X - bR, p.Y - bR, bR * 2, bR * 2);
        }
    }

    // tabler:history
    private static void PaintHistory(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        PointF center = Map(r, 13, 12);
        float rad = Scale(r, 7);
        g.DrawArc(pen, center.X - rad, center.Y - rad, rad * 2, rad * 2, 90, 300);
        // clock hands
        g.DrawLine(pen, center, Map(r, 13, 8));
        g.DrawLine(pen, center, Map(r, 16, 14));
        // arrow
        g.DrawLine(pen, Map(r, 4, 13), Map(r, 6, 16));
        g.DrawLine(pen, Map(r, 4, 13), Map(r, 7, 11));
    }

    // tabler:bulb
    private static void PaintBulb(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        PointF center = Map(r, 12, 10);
        float rad = Scale(r, 5);
        g.DrawArc(pen, center.X - rad, center.Y - rad, rad * 2, rad * 2, 180, 240);
        g.DrawLine(pen, Map(r, 9, 15), Map(r, 9, 18));
        g.DrawLine(pen, Map(r, 15, 15), Map(r, 15, 18));
        g.DrawLine(pen, Map(r, 9, 18), Map(r, 15, 18));
        g.DrawLine(pen, Map(r, 10, 21), Map(r, 14, 21));
    }

    // tabler:tools
    private static void PaintTools(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        // wrench
        g.DrawLine(pen, Map(r, 7, 10), Map(r, 14, 17));
        g.DrawLine(pen, Map(r, 14, 17), Map(r, 17, 14));
        g.DrawLine(pen, Map(r, 17, 14), Map(r, 10, 7));
        // screwdriver
        g.DrawLine(pen, Map(r, 14, 7), Map(r, 20, 13));
        g.DrawLine(pen, Map(r, 20, 13), Map(r, 18, 15));
        g.DrawLine(pen, Map(r, 4, 17), Map(r, 7, 20));
    }

    // tabler:brand-openai (simplified brain/circle)
    private static void PaintBrandOpenai(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        PointF center = Map(r, 12, 12);
        float rad = Scale(r, 7);
        g.DrawEllipse(pen, center.X - rad, center.Y - rad, rad * 2, rad * 2);
        // simplified AI lines inside
        g.DrawLine(pen, Map(r, 9, 9), Map(r, 12, 6));
        g.DrawLine(pen, Map(r, 12, 6), Map(r, 15, 9));
        g.DrawLine(pen, Map(r, 9, 9), Map(r, 9, 14));
        g.DrawLine(pen, Map(r, 15, 9), Map(r, 15, 14));
        g.DrawLine(pen, Map(r, 9, 14), Map(r, 12, 18));
        g.DrawLine(pen, Map(r, 15, 14), Map(r, 12, 18));
    }

    // tabler:chevron-right
    private static void PaintChevronRight(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        g.DrawLine(pen, Map(r, 9, 5), Map(r, 16, 12));
        g.DrawLine(pen, Map(r, 16, 12), Map(r, 9, 19));
    }

    // tabler:clipboard
    private static void PaintClipboard(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        DrawRoundedRect(g, pen, r, 5, 5, 19, 20, 2);
        // clip top
        g.DrawLine(pen, Map(r, 9, 4), Map(r, 9, 7));
        g.DrawLine(pen, Map(r, 9, 7), Map(r, 15, 7));
        g.DrawLine(pen, Map(r, 15, 7), Map(r, 15, 4));
        g.DrawLine(pen, Map(r, 9, 4), Map(r, 15, 4));
    }

    // tabler:square-rounded-plus
    private static void PaintSquareRoundedPlus(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        DrawRoundedRect(g, pen, r, 4, 4, 20, 20, 3);
        g.DrawLine(pen, Map(r, 12, 8), Map(r, 12, 16));
        g.DrawLine(pen, Map(r, 8, 12), Map(r, 16, 12));
    }

    // tabler:app-window
    private static void PaintWindowMaximize(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        DrawRoundedRect(g, pen, r, 4, 4, 20, 20, 2);
        g.DrawLine(pen, Map(r, 4, 9), Map(r, 20, 9));
        // dot indicators in title bar
        float dotR = Scale(r, 0.7f);
        using SolidBrush fill = new(c);
        PointF d1 = Map(r, 7, 6.5f);
        PointF d2 = Map(r, 10, 6.5f);
        g.FillEllipse(fill, d1.X - dotR, d1.Y - dotR, dotR * 2, dotR * 2);
        g.FillEllipse(fill, d2.X - dotR, d2.Y - dotR, dotR * 2, dotR * 2);
    }

    // tabler:report-analytics
    private static void PaintReportAnalytics(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        DrawRoundedRect(g, pen, r, 5, 4, 19, 20, 1.5f);
        // bars
        g.DrawLine(pen, Map(r, 9, 16), Map(r, 9, 12));
        g.DrawLine(pen, Map(r, 12, 16), Map(r, 12, 9));
        g.DrawLine(pen, Map(r, 15, 16), Map(r, 15, 13));
    }

    // tabler:arrows-maximize (expand outward arrows)
    private static void PaintArrowsMaximize(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r, 2f);
        // top-left arrow
        g.DrawLine(pen, Map(r, 4, 4), Map(r, 10, 4));
        g.DrawLine(pen, Map(r, 4, 4), Map(r, 4, 10));
        // top-right arrow
        g.DrawLine(pen, Map(r, 20, 4), Map(r, 14, 4));
        g.DrawLine(pen, Map(r, 20, 4), Map(r, 20, 10));
        // bottom-left arrow
        g.DrawLine(pen, Map(r, 4, 20), Map(r, 10, 20));
        g.DrawLine(pen, Map(r, 4, 20), Map(r, 4, 14));
        // bottom-right arrow
        g.DrawLine(pen, Map(r, 20, 20), Map(r, 14, 20));
        g.DrawLine(pen, Map(r, 20, 20), Map(r, 20, 14));
    }

    // tabler:arrows-minimize (collapse inward arrows)
    private static void PaintArrowsMinimize(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r, 2f);
        // top-left arrow pointing inward
        g.DrawLine(pen, Map(r, 10, 10), Map(r, 4, 10));
        g.DrawLine(pen, Map(r, 10, 10), Map(r, 10, 4));
        // top-right arrow pointing inward
        g.DrawLine(pen, Map(r, 14, 10), Map(r, 20, 10));
        g.DrawLine(pen, Map(r, 14, 10), Map(r, 14, 4));
        // bottom-left arrow pointing inward
        g.DrawLine(pen, Map(r, 10, 14), Map(r, 4, 14));
        g.DrawLine(pen, Map(r, 10, 14), Map(r, 10, 20));
        // bottom-right arrow pointing inward
        g.DrawLine(pen, Map(r, 14, 14), Map(r, 20, 14));
        g.DrawLine(pen, Map(r, 14, 14), Map(r, 14, 20));
    }

    // tabler:cut (scissors)
    private static void PaintCut(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r, 2f);
        float circleR = Scale(r, 3f);
        // bottom-left circle
        PointF bl = Map(r, 7, 17);
        g.DrawEllipse(pen, bl.X - circleR, bl.Y - circleR, circleR * 2, circleR * 2);
        // bottom-right circle
        PointF br = Map(r, 17, 17);
        g.DrawEllipse(pen, br.X - circleR, br.Y - circleR, circleR * 2, circleR * 2);
        // crossing lines from circles to top
        g.DrawLine(pen, Map(r, 9, 15), Map(r, 18, 5));
        g.DrawLine(pen, Map(r, 15, 15), Map(r, 6, 5));
    }

    // tabler:arrow-back-up (undo)
    private static void PaintArrowBackUp(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r, 2f);
        // arrow tip pointing left
        g.DrawLine(pen, Map(r, 9, 9), Map(r, 5, 13));
        g.DrawLine(pen, Map(r, 5, 13), Map(r, 9, 17));
        // curved path from arrow back and around
        g.DrawLine(pen, Map(r, 5, 13), Map(r, 13, 13));
        float arcR = Scale(r, 4);
        PointF arcCenter = Map(r, 13, 9);
        g.DrawArc(pen, arcCenter.X - arcR, arcCenter.Y - arcR, arcR * 2, arcR * 2, 90, -180);
        g.DrawLine(pen, Map(r, 17, 9), Map(r, 17, 17));
    }

    // tabler:arrow-forward-up (redo — mirrored undo)
    private static void PaintArrowForwardUpDouble(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r, 2f);
        // arrow tip pointing right
        g.DrawLine(pen, Map(r, 15, 9), Map(r, 19, 13));
        g.DrawLine(pen, Map(r, 19, 13), Map(r, 15, 17));
        // curved path from arrow back and around
        g.DrawLine(pen, Map(r, 19, 13), Map(r, 11, 13));
        float arcR = Scale(r, 4);
        PointF arcCenter = Map(r, 11, 9);
        g.DrawArc(pen, arcCenter.X - arcR, arcCenter.Y - arcR, arcR * 2, arcR * 2, 90, 180);
        g.DrawLine(pen, Map(r, 7, 9), Map(r, 7, 17));
    }

    // tabler:pencil
    private static void PaintPencil(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        g.DrawLine(pen, Map(r, 4, 20), Map(r, 8, 16));
        g.DrawLine(pen, Map(r, 8, 16), Map(r, 16, 8));
        g.DrawLine(pen, Map(r, 16, 8), Map(r, 18, 6));
        g.DrawLine(pen, Map(r, 18, 6), Map(r, 20, 8));
        g.DrawLine(pen, Map(r, 20, 8), Map(r, 8, 20));
        g.DrawLine(pen, Map(r, 8, 20), Map(r, 4, 20));
        g.DrawLine(pen, Map(r, 14, 10), Map(r, 17, 13));
    }

    // tabler:highlight
    private static void PaintHighlight(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        g.DrawLine(pen, Map(r, 3, 19), Map(r, 3, 21));
        g.DrawLine(pen, Map(r, 3, 21), Map(r, 9, 21));
        g.DrawLine(pen, Map(r, 9, 21), Map(r, 9, 19));
        g.DrawLine(pen, Map(r, 9, 19), Map(r, 3, 19));
        g.DrawLine(pen, Map(r, 6, 19), Map(r, 11, 14));
        g.DrawLine(pen, Map(r, 11, 14), Map(r, 17, 8));
        g.DrawLine(pen, Map(r, 17, 8), Map(r, 20, 5));
        g.DrawLine(pen, Map(r, 20, 5), Map(r, 21, 6));
        g.DrawLine(pen, Map(r, 21, 6), Map(r, 14, 13));
    }

    // tabler:color-picker
    private static void PaintColorPicker(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        PointF center = Map(r, 12, 14);
        float rx = Scale(r, 7);
        float ry = Scale(r, 7);
        g.DrawEllipse(pen, center.X - rx, center.Y - ry, rx * 2, ry * 2);
        g.DrawLine(pen, Map(r, 12, 7), Map(r, 12, 3));
        g.DrawLine(pen, Map(r, 12, 3), Map(r, 15, 3));
        g.DrawLine(pen, Map(r, 15, 3), Map(r, 15, 6));
    }

    // tabler:layers-intersect
    private static void PaintLayersIntersect(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        DrawRoundedRect(g, pen, r, 4, 4, 15, 15, 2f);
        DrawRoundedRect(g, pen, r, 9, 9, 20, 20, 2f);
    }

    // tabler:arrows-move
    private static void PaintArrowsMove(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        // vertical
        g.DrawLine(pen, Map(r, 12, 4), Map(r, 12, 20));
        g.DrawLine(pen, Map(r, 12, 4), Map(r, 9, 7));
        g.DrawLine(pen, Map(r, 12, 4), Map(r, 15, 7));
        g.DrawLine(pen, Map(r, 12, 20), Map(r, 9, 17));
        g.DrawLine(pen, Map(r, 12, 20), Map(r, 15, 17));
        // horizontal
        g.DrawLine(pen, Map(r, 4, 12), Map(r, 20, 12));
        g.DrawLine(pen, Map(r, 4, 12), Map(r, 7, 9));
        g.DrawLine(pen, Map(r, 4, 12), Map(r, 7, 15));
        g.DrawLine(pen, Map(r, 20, 12), Map(r, 17, 9));
        g.DrawLine(pen, Map(r, 20, 12), Map(r, 17, 15));
    }

    // tabler:clock
    private static void PaintClock(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        PointF center = Map(r, 12, 12);
        float radius = Scale(r, 8);
        g.DrawEllipse(pen, center.X - radius, center.Y - radius, radius * 2, radius * 2);
        g.DrawLine(pen, Map(r, 12, 12), Map(r, 12, 8));
        g.DrawLine(pen, Map(r, 12, 12), Map(r, 16, 14));
    }

    // tabler:line
    private static void PaintLine(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        g.DrawLine(pen, Map(r, 4, 20), Map(r, 20, 4));
    }

    // tabler:square
    private static void PaintSquare(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        DrawRoundedRect(g, pen, r, 5, 5, 19, 19, 1.5f);
    }

    // tabler:circle
    private static void PaintCircle(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        PointF center = Map(r, 12, 12);
        float radius = Scale(r, 8);
        g.DrawEllipse(pen, center.X - radius, center.Y - radius, radius * 2, radius * 2);
    }

    // tabler:arrow-narrow-right
    private static void PaintArrowNarrowRight(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        g.DrawLine(pen, Map(r, 4, 12), Map(r, 20, 12));
        g.DrawLine(pen, Map(r, 16, 8), Map(r, 20, 12));
        g.DrawLine(pen, Map(r, 16, 16), Map(r, 20, 12));
    }

    // tabler:letter-a
    private static void PaintLetterA(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        g.DrawLine(pen, Map(r, 7, 20), Map(r, 12, 4));
        g.DrawLine(pen, Map(r, 12, 4), Map(r, 17, 20));
        g.DrawLine(pen, Map(r, 9, 14), Map(r, 15, 14));
    }

    // tabler:paint (bucket)
    private static void PaintPaintBucket(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        // bucket body
        DrawRoundedRect(g, pen, r, 4, 8, 16, 20, 2f);
        // handle
        g.DrawLine(pen, Map(r, 10, 8), Map(r, 10, 4));
        g.DrawLine(pen, Map(r, 10, 4), Map(r, 14, 4));
        // drip
        g.DrawLine(pen, Map(r, 19, 12), Map(r, 19, 16));
        using SolidBrush brush = new(c);
        PointF drip = Map(r, 19, 17);
        float ds = Scale(r, 2);
        g.FillEllipse(brush, drip.X - ds, drip.Y - ds, ds * 2, ds * 2);
    }

    // tabler:color-swatch
    private static void PaintColorSwatch(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        DrawRoundedRect(g, pen, r, 4, 4, 20, 20, 2f);
        g.DrawLine(pen, Map(r, 4, 12), Map(r, 20, 12));
        g.DrawLine(pen, Map(r, 12, 4), Map(r, 12, 20));
    }

    // selection rectangle (dashed)
    private static void PaintSelectionDrag(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r, 2f);
        pen.DashStyle = DashStyle.Dash;
        DrawRoundedRect(g, pen, r, 5, 5, 19, 19, 0.5f);
        // corner dots
        using SolidBrush b = new(c);
        float ds = Scale(r, 1.5f);
        foreach (var (x, y) in new[] { (5f, 5f), (19f, 5f), (5f, 19f), (19f, 19f) })
        {
            PointF p = Map(r, x, y);
            g.FillRectangle(b, p.X - ds, p.Y - ds, ds * 2, ds * 2);
        }
    }

    // crop icon
    private static void PaintCropIcon(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        g.DrawLine(pen, Map(r, 7, 4), Map(r, 7, 17));
        g.DrawLine(pen, Map(r, 7, 17), Map(r, 20, 17));
        g.DrawLine(pen, Map(r, 17, 7), Map(r, 17, 20));
        g.DrawLine(pen, Map(r, 4, 7), Map(r, 17, 7));
    }

    // zoom-in
    private static void PaintZoomIn(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        PointF center = Map(r, 10, 10);
        float rad = Scale(r, 6);
        g.DrawEllipse(pen, center.X - rad, center.Y - rad, rad * 2, rad * 2);
        g.DrawLine(pen, Map(r, 15, 15), Map(r, 20, 20));
        g.DrawLine(pen, Map(r, 8, 10), Map(r, 12, 10));
        g.DrawLine(pen, Map(r, 10, 8), Map(r, 10, 12));
    }

    // zoom-out
    private static void PaintZoomOut(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        PointF center = Map(r, 10, 10);
        float rad = Scale(r, 6);
        g.DrawEllipse(pen, center.X - rad, center.Y - rad, rad * 2, rad * 2);
        g.DrawLine(pen, Map(r, 15, 15), Map(r, 20, 20));
        g.DrawLine(pen, Map(r, 8, 10), Map(r, 12, 10));
    }

    // diameter / brush size
    private static void PaintDiameter(Graphics g, RectangleF r, Color c)
    {
        using Pen pen = MakePen(c, r);
        PointF center = Map(r, 12, 12);
        float rad = Scale(r, 7);
        g.DrawEllipse(pen, center.X - rad, center.Y - rad, rad * 2, rad * 2);
        g.DrawLine(pen, Map(r, 6, 12), Map(r, 18, 12));
    }

    // —— Geometry helpers ————————————————————————————————

    private static void DrawRoundedRect(Graphics g, Pen pen, RectangleF r,
        float x1_24, float y1_24, float x2_24, float y2_24, float radius24)
    {
        PointF tl = Map(r, x1_24, y1_24);
        PointF br = Map(r, x2_24, y2_24);
        float w = br.X - tl.X;
        float h = br.Y - tl.Y;
        float rad = Scale(r, radius24);
        float d = rad * 2;

        if (d > w) d = w;
        if (d > h) d = h;
        rad = d / 2;

        using GraphicsPath path = new();
        path.AddArc(tl.X, tl.Y, d, d, 180, 90);
        path.AddArc(tl.X + w - d, tl.Y, d, d, 270, 90);
        path.AddArc(tl.X + w - d, tl.Y + h - d, d, d, 0, 90);
        path.AddArc(tl.X, tl.Y + h - d, d, d, 90, 90);
        path.CloseFigure();
        g.DrawPath(pen, path);
    }

    private static PointF[] MakeStarPoints(PointF center, float outerRadius, float innerRadius, int points)
    {
        PointF[] result = new PointF[points * 2];
        double offset = -Math.PI / 2;
        for (int i = 0; i < points * 2; i++)
        {
            double angle = offset + i * Math.PI / points;
            float radius = i % 2 == 0 ? outerRadius : innerRadius;
            result[i] = new PointF(
                center.X + (float)(Math.Cos(angle) * radius),
                center.Y + (float)(Math.Sin(angle) * radius));
        }
        return result;
    }
}

/// <summary>
/// Identifies a specific Tabler Icon for rendering.
/// See https://github.com/tabler/tabler-icons for the full catalog.
/// </summary>
internal enum TablerIcon
{
    Plus,
    Photo,
    Screenshot,
    Eraser,
    Send,
    ArrowForwardUp,
    X,
    Keyboard,
    AlertTriangle,
    Loader,
    FileText,
    Message,
    Pin,
    StarFilled,
    Copy,
    ClipboardText,
    Trash,
    Refresh,
    FileExport,
    ExternalLink,
    Search,
    ClearAll,
    Edit,
    LayoutDashboard,
    Settings,
    DeviceFloppy,
    FolderOpen,
    Download,
    Upload,
    PlayerPlay,
    InfoCircle,
    Bug,
    Wand,
    ListDetails,
    History,
    Bulb,
    Tools,
    BrandOpenai,
    ChevronRight,
    Clipboard,
    SquareRoundedPlus,
    WindowMaximize,
    ReportAnalytics,
    ArrowsMaximize,
    ArrowsMinimize,
    Cut,
    ArrowBackUp,
    ArrowForwardUpDouble,
    Pencil,
    Highlight,
    ColorPicker,
    LayersIntersect,
    ArrowsMove,
    Clock,
    Line,
    Square,
    Circle,
    ArrowNarrowRight,
    LetterA,
    PaintBucket,
    ColorSwatch,
    SelectionDrag,
    CropIcon,
    ZoomIn,
    ZoomOut,
    Diameter
}
