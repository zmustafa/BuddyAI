using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using BuddyAI.Helpers;

namespace BuddyAI.Forms;

public sealed class ImagePreviewForm : Form
{
    private readonly TablerIconCache _iconCache;

    // Canvas state
    private Bitmap _canvas;
    private readonly List<DrawingStroke> _strokes = new();
    private DrawingStroke? _currentStroke;

    // Undo / Redo
    private readonly Stack<Bitmap> _undoStack = new();
    private readonly Stack<Bitmap> _redoStack = new();
    private const int MaxUndoLevels = 30;

    // Paste-layer state
    private Bitmap? _pastedLayer;
    private Point _layerOffset;
    private Size _layerSize;
    private bool _isDraggingLayer;
    private Point _layerDragStart;
    private bool _layerPlacementActive;
    private ResizeHandle _activeResizeHandle = ResizeHandle.None;
    private bool _isResizingLayer;
    private Point _resizeDragStart;
    private Rectangle _resizeStartRect;

    private const int ResizeHandleSize = 8;

    // Canvas-edge resize state
    private bool _isResizingCanvas;
    private ResizeHandle _canvasResizeHandle = ResizeHandle.None;
    private Point _canvasResizeDragStart;
    private Size _canvasResizeStartSize;
    private Bitmap? _canvasResizeSnapshot;

    // Drawing tool state
    private DrawingTool _activeTool = DrawingTool.None;
    private Color _drawColor = Color.Red;
    private Color _fillColor = Color.Transparent;
    private float _penWidth = 3f;
    private float _markerWidth = 14f;
    private float _eraserWidth = 20f;

    // Shape tool state
    private Point _shapeStart;
    private Point _shapeEnd;
    private bool _isDrawingShape;

    // Selection state
    private Rectangle _selection = Rectangle.Empty;
    private bool _isSelecting;
    private Point _selectionStart;

    // Zoom / pan state
    private float _zoomLevel = 1.0f;
    private PointF _scrollOffset = PointF.Empty;
    private bool _isPanning;
    private Point _panStart;
    private PointF _panScrollStart;
    private const float ZoomMin = 0.1f;
    private const float ZoomMax = 16f;

    // Toolbar buttons that need checked-state management
    private readonly ToolStripButton _btnPencil;
    private readonly ToolStripButton _btnMarker;
    private readonly ToolStripButton _btnEraser;
    private readonly ToolStripButton _btnLine;
    private readonly ToolStripButton _btnRect;
    private readonly ToolStripButton _btnEllipse;
    private readonly ToolStripButton _btnArrow;
    private readonly ToolStripButton _btnText;
    private readonly ToolStripButton _btnFill;
    private readonly ToolStripButton _btnEyedropper;
    private readonly ToolStripButton _btnSelect;
    private readonly ToolStripButton _btnCrop;
    private readonly ToolStripButton _btnUndo;
    private readonly ToolStripButton _btnRedo;
    private readonly ToolStripLabel _lblZoom;
    private readonly ToolStripButton _btnBrushSize;

    // Snip delay
    private readonly NumericUpDown _numDelay;

    // The picture surface
    private readonly PictureBox _preview;

    /// <summary>
    /// The edited image if the user pressed Save, otherwise null.
    /// </summary>
    public Image? EditedImage { get; private set; }

    public ImagePreviewForm(Image image)
    {
        _iconCache = new TablerIconCache(Color.WhiteSmoke);
        _canvas = new Bitmap(image);

        Text = "Image Preview";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(900, 680);
        MinimumSize = new Size(420, 320);
        KeyPreview = true;
        ShowInTaskbar = false;

        // --- Toolbar ---
        ToolStrip toolbar = new()
        {
            GripStyle = ToolStripGripStyle.Hidden,
            RenderMode = ToolStripRenderMode.System,
            ImageScalingSize = new Size(18, 18),
            Padding = new Padding(4, 2, 4, 2),
            LayoutStyle = ToolStripLayoutStyle.Flow
        };

        toolbar.Items.Add(MakeButton("Snip", TablerIcon.Screenshot, OnSnip));

        _numDelay = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 30,
            Value = 3,
            Width = 48,
            Font = new Font("Segoe UI", 9F)
        };
        ToolStripControlHost delayHost = new(_numDelay) { ToolTipText = "Snip delay (seconds)" };
        toolbar.Items.Add(delayHost);
        toolbar.Items.Add(MakeButton("Snip in…", TablerIcon.Clock, OnSnipDelayed));

        toolbar.Items.Add(MakeSep());
        toolbar.Items.Add(MakeButton("Copy", TablerIcon.Copy, OnCopy));
        toolbar.Items.Add(MakeButton("Paste Replace", TablerIcon.Clipboard, OnPasteReplace));
        toolbar.Items.Add(MakeButton("Paste Layer", TablerIcon.LayersIntersect, OnPasteLayer));
        toolbar.Items.Add(MakeButton("Resize Canvas", TablerIcon.ArrowsMaximize, OnResizeCanvas));
        toolbar.Items.Add(MakeButton("Move Image", TablerIcon.ArrowsMove, OnMoveImage));

        toolbar.Items.Add(MakeSep());
        _btnSelect = MakeToggleButton("Select", TablerIcon.SelectionDrag, OnSelectToggle);
        _btnCrop = MakeToggleButton("Crop", TablerIcon.CropIcon, OnCropToggle);
        toolbar.Items.Add(_btnSelect);
        toolbar.Items.Add(_btnCrop);
        toolbar.Items.Add(MakeButton("Crop Sel.", TablerIcon.Cut, OnCropSelection));

        toolbar.Items.Add(MakeSep());
        _btnPencil = MakeToggleButton("Pencil", TablerIcon.Pencil, OnPencilToggle);
        _btnMarker = MakeToggleButton("Marker", TablerIcon.Highlight, OnMarkerToggle);
        _btnEraser = MakeToggleButton("Eraser", TablerIcon.Eraser, OnEraserToggle);
        toolbar.Items.Add(_btnPencil);
        toolbar.Items.Add(_btnMarker);
        toolbar.Items.Add(_btnEraser);

        toolbar.Items.Add(MakeSep());
        _btnLine = MakeToggleButton("Line", TablerIcon.Line, OnLineToggle);
        _btnRect = MakeToggleButton("Rect", TablerIcon.Square, OnRectToggle);
        _btnEllipse = MakeToggleButton("Ellipse", TablerIcon.Circle, OnEllipseToggle);
        _btnArrow = MakeToggleButton("Arrow", TablerIcon.ArrowNarrowRight, OnArrowToggle);
        _btnText = MakeToggleButton("Text", TablerIcon.LetterA, OnTextToggle);
        toolbar.Items.Add(_btnLine);
        toolbar.Items.Add(_btnRect);
        toolbar.Items.Add(_btnEllipse);
        toolbar.Items.Add(_btnArrow);
        toolbar.Items.Add(_btnText);

        toolbar.Items.Add(MakeSep());
        _btnFill = MakeToggleButton("Fill", TablerIcon.PaintBucket, OnFillToggle);
        _btnEyedropper = MakeToggleButton("Pick", TablerIcon.ColorSwatch, OnEyedropperToggle);
        toolbar.Items.Add(_btnFill);
        toolbar.Items.Add(_btnEyedropper);
        toolbar.Items.Add(MakeButton("Color", TablerIcon.ColorPicker, OnPickColor));
        _btnBrushSize = MakeButton("Size", TablerIcon.Diameter, OnBrushSize);
        toolbar.Items.Add(_btnBrushSize);

        toolbar.Items.Add(MakeSep());
        toolbar.Items.Add(MakeButton("Zoom+", TablerIcon.ZoomIn, OnZoomIn));
        toolbar.Items.Add(MakeButton("Zoom?", TablerIcon.ZoomOut, OnZoomOut));
        toolbar.Items.Add(MakeButton("Fit", TablerIcon.WindowMaximize, OnZoomFit));
        _lblZoom = new ToolStripLabel("100%");
        toolbar.Items.Add(_lblZoom);

        toolbar.Items.Add(MakeSep());
        _btnUndo = MakeButton("Undo", TablerIcon.ArrowBackUp, (_, __) => Undo());
        _btnRedo = MakeButton("Redo", TablerIcon.ArrowForwardUp, (_, __) => Redo());
        _btnUndo.Enabled = false;
        _btnRedo.Enabled = false;
        toolbar.Items.Add(_btnUndo);
        toolbar.Items.Add(_btnRedo);

        toolbar.Items.Add(MakeSep());
        toolbar.Items.Add(MakeButton("Save", TablerIcon.DeviceFloppy, OnSave));
        toolbar.Items.Add(MakeButton("Cancel", TablerIcon.X, OnCancel));

        Controls.Add(toolbar);

        // --- Preview surface ---
        _preview = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = _canvas
        };
        _preview.MouseDown += Preview_MouseDown;
        _preview.MouseMove += Preview_MouseMove;
        _preview.MouseUp += Preview_MouseUp;
        _preview.Paint += Preview_Paint;
        _preview.MouseWheel += (_, we) =>
        {
            float factor = we.Delta > 0 ? 1.15f : 1f / 1.15f;
            ApplyZoom(_zoomLevel * factor);
        };
        Controls.Add(_preview);

        // Ensure toolbar is on top
        toolbar.BringToFront();

        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                if (_layerPlacementActive)
                    CancelLayerPlacement();
                else if (_isDrawingShape) { _isDrawingShape = false; _preview.Invalidate(); }
                else if (_selection.Width > 0) { _selection = Rectangle.Empty; _preview.Invalidate(); }
                else
                    OnCancel(this, EventArgs.Empty);
            }
            else if (e.Control && e.KeyCode == Keys.Z) { Undo(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.Y) { Redo(); e.Handled = true; }
            else if (e.KeyCode == Keys.Oemplus || e.KeyCode == Keys.Add) { ApplyZoom(_zoomLevel * 1.25f); e.Handled = true; }
            else if (e.KeyCode == Keys.OemMinus || e.KeyCode == Keys.Subtract) { ApplyZoom(_zoomLevel / 1.25f); e.Handled = true; }
            else if (e.KeyCode == Keys.D0 || e.KeyCode == Keys.NumPad0) { ApplyZoom(1.0f); e.Handled = true; }
            else if (e.KeyCode == Keys.Delete && _selection.Width > 1)
            {
                PushUndo();
                using Graphics g = Graphics.FromImage(_canvas);
                using SolidBrush b = new(Color.White);
                g.FillRectangle(b, _selection);
                _selection = Rectangle.Empty;
                RefreshCanvas();
                e.Handled = true;
            }
        };
    }

    // ?? Toolbar helpers ??????????????????????????????????????????

    private ToolStripButton MakeButton(string text, TablerIcon icon, EventHandler click)
    {
        ToolStripButton btn = new(text, _iconCache.Get(icon, TablerIconCache.ToolStripIconSize))
        {
            DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
            TextImageRelation = TextImageRelation.ImageBeforeText
        };
        btn.Click += click;
        return btn;
    }

    private ToolStripButton MakeToggleButton(string text, TablerIcon icon, EventHandler click)
    {
        ToolStripButton btn = new(text, _iconCache.Get(icon, TablerIconCache.ToolStripIconSize))
        {
            DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
            TextImageRelation = TextImageRelation.ImageBeforeText,
            CheckOnClick = false
        };
        btn.Click += click;
        return btn;
    }

    private static ToolStripLabel MakeSep() => new(" ? ")
    {
        ForeColor = Color.Gray,
        Padding = new Padding(0),
        Margin = new Padding(0, 0, 0, 0)
    };

    // ?? Coordinate mapping (Zoom-aware) ??????????????????????????

    private Point ScreenToImage(Point screenPoint)
    {
        if (_preview.Image == null) return screenPoint;

        float imgW = _preview.Image.Width;
        float imgH = _preview.Image.Height;
        float ctlW = _preview.ClientSize.Width;
        float ctlH = _preview.ClientSize.Height;

        float scale = Math.Min(ctlW / imgW, ctlH / imgH);
        float dispW = imgW * scale;
        float dispH = imgH * scale;
        float offsetX = (ctlW - dispW) / 2f;
        float offsetY = (ctlH - dispH) / 2f;

        int ix = (int)((screenPoint.X - offsetX) / scale);
        int iy = (int)((screenPoint.Y - offsetY) / scale);
        ix = Math.Clamp(ix, 0, (int)imgW - 1);
        iy = Math.Clamp(iy, 0, (int)imgH - 1);
        return new Point(ix, iy);
    }

    private Point ScreenToImageUnclamped(Point screenPoint)
    {
        if (_preview.Image == null) return screenPoint;

        float imgW = _preview.Image.Width;
        float imgH = _preview.Image.Height;
        float ctlW = _preview.ClientSize.Width;
        float ctlH = _preview.ClientSize.Height;

        float scale = Math.Min(ctlW / imgW, ctlH / imgH);
        float offsetX = (ctlW - imgW * scale) / 2f;
        float offsetY = (ctlH - imgH * scale) / 2f;

        int ix = (int)((screenPoint.X - offsetX) / scale);
        int iy = (int)((screenPoint.Y - offsetY) / scale);
        return new Point(ix, iy);
    }

    private (float scale, float offsetX, float offsetY) GetImageDisplayTransform()
    {
        float imgW = _canvas.Width;
        float imgH = _canvas.Height;
        float ctlW = _preview.ClientSize.Width;
        float ctlH = _preview.ClientSize.Height;
        float scale = Math.Min(ctlW / imgW, ctlH / imgH);
        float offsetX = (ctlW - imgW * scale) / 2f;
        float offsetY = (ctlH - imgH * scale) / 2f;
        return (scale, offsetX, offsetY);
    }

    private static Rectangle NormalizeRect(Point a, Point b)
        => new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y));

    // ?? Drawing on canvas ????????????????????????????????????????

    private void Preview_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;

        // Canvas-edge resize (only when no layer active and no drawing tool)
        if (!_layerPlacementActive && _activeTool == DrawingTool.None)
        {
            ResizeHandle ch = HitTestCanvasEdge(e.Location);
            if (ch != ResizeHandle.None)
            {
                _isResizingCanvas = true;
                _canvasResizeHandle = ch;
                _canvasResizeDragStart = ScreenToImageUnclamped(e.Location);
                _canvasResizeStartSize = _canvas.Size;
                _canvasResizeSnapshot?.Dispose();
                _canvasResizeSnapshot = new Bitmap(_canvas);
                PushUndo();
                return;
            }
        }

        if (_layerPlacementActive && _pastedLayer != null)
        {
            Point imgPt = ScreenToImage(e.Location);

            // Check resize handles first
            ResizeHandle handle = HitTestResizeHandle(imgPt);
            if (handle != ResizeHandle.None)
            {
                _isResizingLayer = true;
                _activeResizeHandle = handle;
                _resizeDragStart = imgPt;
                _resizeStartRect = new Rectangle(_layerOffset, _layerSize);
                return;
            }

            if (IsPointInsideLayer(imgPt))
            {
                _isDraggingLayer = true;
                _layerDragStart = new Point(imgPt.X - _layerOffset.X, imgPt.Y - _layerOffset.Y);
                return;
            }
            else
            {
                CommitLayer();
                return;
            }
        }

        if (_activeTool == DrawingTool.None) return;

        Point p = ScreenToImage(e.Location);

        // Single-click tools
        if (_activeTool == DrawingTool.Fill) { FloodFill(p); return; }
        if (_activeTool == DrawingTool.Eyedropper) { EyedropperPick(p); return; }
        if (_activeTool == DrawingTool.Text) { CommitTextAtPoint(p); return; }

        // Selection / Crop drag
        if (_activeTool == DrawingTool.Select || _activeTool == DrawingTool.Crop)
        {
            _isSelecting = true;
            _selectionStart = p;
            _selection = new Rectangle(p, Size.Empty);
            return;
        }

        // Shape tools
        if (_activeTool is DrawingTool.Line or DrawingTool.Rectangle or DrawingTool.Ellipse or DrawingTool.Arrow)
        {
            _isDrawingShape = true;
            _shapeStart = p;
            _shapeEnd = p;
            return;
        }

        // Freehand tools (Pencil, Marker, Eraser)
        float width = _activeTool switch
        {
            DrawingTool.Marker => _markerWidth,
            DrawingTool.Eraser => _eraserWidth,
            _ => _penWidth
        };
        int alpha = _activeTool == DrawingTool.Marker ? 100 : 255;
        Color strokeColor = _activeTool == DrawingTool.Eraser ? Color.White : Color.FromArgb(alpha, _drawColor);

        _currentStroke = new DrawingStroke(strokeColor, width, _activeTool == DrawingTool.Marker);
        _currentStroke.Points.Add(p);
    }

    private void Preview_MouseMove(object? sender, MouseEventArgs e)
    {
        // Canvas-edge resize drag
        if (_isResizingCanvas)
        {
            Point imgPt = ScreenToImageUnclamped(e.Location);
            int dx = imgPt.X - _canvasResizeDragStart.X;
            int dy = imgPt.Y - _canvasResizeDragStart.Y;
            ApplyCanvasResize(dx, dy);
            return;
        }

        if (_isResizingLayer && _pastedLayer != null)
        {
            Point imgPt = ScreenToImage(e.Location);
            int dx = imgPt.X - _resizeDragStart.X;
            int dy = imgPt.Y - _resizeDragStart.Y;
            ApplyResize(dx, dy);
            _preview.Invalidate();
            return;
        }

        if (_isDraggingLayer && _pastedLayer != null)
        {
            Point imgPt = ScreenToImage(e.Location);
            _layerOffset = new Point(imgPt.X - _layerDragStart.X, imgPt.Y - _layerDragStart.Y);
            _preview.Invalidate();
            return;
        }

        // Update cursor for resize handles when hovering over the layer
        if (_layerPlacementActive && _pastedLayer != null && !_isDraggingLayer)
        {
            Point imgPt = ScreenToImage(e.Location);
            ResizeHandle handle = HitTestResizeHandle(imgPt);
            Cursor = handle switch
            {
                ResizeHandle.TopLeft or ResizeHandle.BottomRight => Cursors.SizeNWSE,
                ResizeHandle.TopRight or ResizeHandle.BottomLeft => Cursors.SizeNESW,
                ResizeHandle.Top or ResizeHandle.Bottom => Cursors.SizeNS,
                ResizeHandle.Left or ResizeHandle.Right => Cursors.SizeWE,
                _ => IsPointInsideLayer(imgPt) ? Cursors.SizeAll : Cursors.Default
            };
            return;
        }

        // Canvas-edge hover cursor (only when no layer and no tool)
        if (!_layerPlacementActive && _activeTool == DrawingTool.None)
        {
            ResizeHandle ch = HitTestCanvasEdge(e.Location);
            Cursor = ch switch
            {
                ResizeHandle.TopLeft or ResizeHandle.BottomRight => Cursors.SizeNWSE,
                ResizeHandle.TopRight or ResizeHandle.BottomLeft => Cursors.SizeNESW,
                ResizeHandle.Top or ResizeHandle.Bottom => Cursors.SizeNS,
                ResizeHandle.Left or ResizeHandle.Right => Cursors.SizeWE,
                _ => Cursors.Default
            };
        }

        // Shape drag
        if (_isDrawingShape)
        {
            _shapeEnd = ScreenToImage(e.Location);
            _preview.Invalidate();
            return;
        }

        // Selection drag
        if (_isSelecting)
        {
            Point cur = ScreenToImage(e.Location);
            _selection = NormalizeRect(_selectionStart, cur);
            _preview.Invalidate();
            return;
        }

        if (_currentStroke == null) return;
        Point p = ScreenToImage(e.Location);
        _currentStroke.Points.Add(p);
        _preview.Invalidate();
    }

    private void Preview_MouseUp(object? sender, MouseEventArgs e)
    {
        if (_isResizingCanvas)
        {
            _isResizingCanvas = false;
            _canvasResizeHandle = ResizeHandle.None;
            _canvasResizeSnapshot?.Dispose();
            _canvasResizeSnapshot = null;
            _preview.Invalidate();
            return;
        }

        if (_isResizingLayer)
        {
            _isResizingLayer = false;
            _activeResizeHandle = ResizeHandle.None;
            _preview.Invalidate();
            return;
        }

        if (_isDraggingLayer)
        {
            _isDraggingLayer = false;
            _preview.Invalidate();
            return;
        }

        if (_isDrawingShape)
        {
            _shapeEnd = ScreenToImage(e.Location);
            CommitShapeToCanvas();
            return;
        }

        if (_isSelecting)
        {
            Point cur = ScreenToImage(e.Location);
            _selection = NormalizeRect(_selectionStart, cur);
            _isSelecting = false;
            _preview.Invalidate();
            return;
        }

        if (_currentStroke == null) return;
        _strokes.Add(_currentStroke);
        PushUndo();
        BurnStroke(_currentStroke);
        _currentStroke = null;
        RefreshCanvas();
    }

    private void Preview_Paint(object? sender, PaintEventArgs e)
    {
        // Draw the in-progress stroke as a live overlay
        if (_currentStroke != null && _currentStroke.Points.Count > 1)
        {
            DrawStrokeOverlay(e.Graphics, _currentStroke);
        }

        // Draw floating paste layer
        if (_layerPlacementActive && _pastedLayer != null)
        {
            DrawLayerOverlay(e.Graphics);
        }

        // Draw canvas-edge resize handles when idle
        if (!_layerPlacementActive && _activeTool == DrawingTool.None)
        {
            DrawCanvasResizeHandles(e.Graphics);
        }

        // Draw in-progress shape overlay
        if (_isDrawingShape)
        {
            var (scale, offX, offY) = GetImageDisplayTransform();
            DrawShapeOnGraphics(e.Graphics, _shapeStart, _shapeEnd, scale, offX, offY);
        }

        // Draw selection rectangle
        if (_selection.Width > 1 && _selection.Height > 1)
        {
            var (scale, offX, offY) = GetImageDisplayTransform();
            float sx = _selection.X * scale + offX;
            float sy = _selection.Y * scale + offY;
            float sw = _selection.Width * scale;
            float sh = _selection.Height * scale;
            using Pen selPen = new(Color.FromArgb(180, 0, 120, 215), 1.5f) { DashStyle = DashStyle.Dash };
            e.Graphics.DrawRectangle(selPen, sx, sy, sw, sh);
            string selLabel = $"{_selection.Width}×{_selection.Height}";
            TextRenderer.DrawText(e.Graphics, selLabel, Font, new Point((int)sx, (int)(sy - 16)), Color.FromArgb(0, 120, 215));
        }
    }

    private void DrawStrokeOverlay(Graphics g, DrawingStroke stroke)
    {
        if (stroke.Points.Count < 2) return;

        // Map image coords to control coords for the overlay
        float imgW = _canvas.Width;
        float imgH = _canvas.Height;
        float ctlW = _preview.ClientSize.Width;
        float ctlH = _preview.ClientSize.Height;
        float scale = Math.Min(ctlW / imgW, ctlH / imgH);
        float offsetX = (ctlW - imgW * scale) / 2f;
        float offsetY = (ctlH - imgH * scale) / 2f;

        PointF[] pts = stroke.Points
            .Select(p => new PointF(p.X * scale + offsetX, p.Y * scale + offsetY))
            .ToArray();

        using Pen pen = new(stroke.Color, stroke.Width * scale)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };

        if (stroke.IsMarker)
        {
            using var oldMode = new CompositingModeScope(g, CompositingMode.SourceOver);
            g.DrawLines(pen, pts);
        }
        else
        {
            g.DrawLines(pen, pts);
        }
    }

    private void DrawLayerOverlay(Graphics g)
    {
        if (_pastedLayer == null) return;

        float imgW = _canvas.Width;
        float imgH = _canvas.Height;
        float ctlW = _preview.ClientSize.Width;
        float ctlH = _preview.ClientSize.Height;
        float scale = Math.Min(ctlW / imgW, ctlH / imgH);
        float offsetX = (ctlW - imgW * scale) / 2f;
        float offsetY = (ctlH - imgH * scale) / 2f;

        float dx = _layerOffset.X * scale + offsetX;
        float dy = _layerOffset.Y * scale + offsetY;
        float dw = _layerSize.Width * scale;
        float dh = _layerSize.Height * scale;

        g.DrawImage(_pastedLayer, dx, dy, dw, dh);

        using Pen border = new(Color.FromArgb(180, 0, 120, 215), 2f) { DashStyle = DashStyle.Dash };
        g.DrawRectangle(border, dx, dy, dw, dh);

        // Draw resize handles
        float hs = ResizeHandleSize;
        using SolidBrush handleBrush = new(Color.FromArgb(220, 0, 120, 215));
        using Pen handlePen = new(Color.White, 1f);

        RectangleF[] handles =
        {
            new(dx - hs / 2, dy - hs / 2, hs, hs),                          // TopLeft
            new(dx + dw / 2 - hs / 2, dy - hs / 2, hs, hs),                // Top
            new(dx + dw - hs / 2, dy - hs / 2, hs, hs),                     // TopRight
            new(dx + dw - hs / 2, dy + dh / 2 - hs / 2, hs, hs),           // Right
            new(dx + dw - hs / 2, dy + dh - hs / 2, hs, hs),               // BottomRight
            new(dx + dw / 2 - hs / 2, dy + dh - hs / 2, hs, hs),           // Bottom
            new(dx - hs / 2, dy + dh - hs / 2, hs, hs),                     // BottomLeft
            new(dx - hs / 2, dy + dh / 2 - hs / 2, hs, hs)                 // Left
        };

        foreach (RectangleF hr in handles)
        {
            g.FillRectangle(handleBrush, hr);
            g.DrawRectangle(handlePen, hr.X, hr.Y, hr.Width, hr.Height);
        }
    }

    private void BurnStroke(DrawingStroke stroke
    )
    {
        if (stroke.Points.Count < 2) return;

        using Graphics g = Graphics.FromImage(_canvas);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using Pen pen = new(stroke.Color, stroke.Width)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };

        PointF[] pts = stroke.Points.Select(p => new PointF(p.X, p.Y)).ToArray();
        g.DrawLines(pen, pts);
    }

    private void RefreshCanvas()
    {
        _preview.Image = _canvas;
        _preview.Invalidate();
    }

    // ?? Canvas-edge resize ???????????????????????????????????????

    private ResizeHandle HitTestCanvasEdge(Point screenPoint)
    {
        var (scale, offsetX, offsetY) = GetImageDisplayTransform();
        float iw = _canvas.Width * scale;
        float ih = _canvas.Height * scale;
        float hs = ResizeHandleSize + 2;

        float sx = screenPoint.X;
        float sy = screenPoint.Y;

        bool nearLeft   = Math.Abs(sx - offsetX) <= hs;
        bool nearRight  = Math.Abs(sx - (offsetX + iw)) <= hs;
        bool nearTop    = Math.Abs(sy - offsetY) <= hs;
        bool nearBottom = Math.Abs(sy - (offsetY + ih)) <= hs;
        bool inHorzBand = sy >= offsetY - hs && sy <= offsetY + ih + hs;
        bool inVertBand = sx >= offsetX - hs && sx <= offsetX + iw + hs;

        if (nearTop && nearLeft)    return ResizeHandle.TopLeft;
        if (nearTop && nearRight)   return ResizeHandle.TopRight;
        if (nearBottom && nearRight)  return ResizeHandle.BottomRight;
        if (nearBottom && nearLeft) return ResizeHandle.BottomLeft;
        if (nearTop && inVertBand)    return ResizeHandle.Top;
        if (nearBottom && inVertBand) return ResizeHandle.Bottom;
        if (nearLeft && inHorzBand)   return ResizeHandle.Left;
        if (nearRight && inHorzBand)  return ResizeHandle.Right;

        return ResizeHandle.None;
    }

    private void ApplyCanvasResize(int dx, int dy)
    {
        if (_canvasResizeSnapshot == null) return;

        const int minSize = 16;
        int w = _canvasResizeStartSize.Width;
        int h = _canvasResizeStartSize.Height;

        int newW = w;
        int newH = h;
        int imgX = 0;
        int imgY = 0;

        switch (_canvasResizeHandle)
        {
            case ResizeHandle.Right:
                newW = w + dx;
                break;
            case ResizeHandle.Bottom:
                newH = h + dy;
                break;
            case ResizeHandle.BottomRight:
                newW = w + dx; newH = h + dy;
                break;
            case ResizeHandle.Left:
                newW = w - dx; imgX = dx;
                break;
            case ResizeHandle.Top:
                newH = h - dy; imgY = dy;
                break;
            case ResizeHandle.TopLeft:
                newW = w - dx; newH = h - dy; imgX = dx; imgY = dy;
                break;
            case ResizeHandle.TopRight:
                newW = w + dx; newH = h - dy; imgY = dy;
                break;
            case ResizeHandle.BottomLeft:
                newW = w - dx; newH = h + dy; imgX = dx;
                break;
        }

        if (newW < minSize) { newW = minSize; if (imgX != 0) imgX = w - minSize; }
        if (newH < minSize) { newH = minSize; if (imgY != 0) imgY = h - minSize; }

        Bitmap newCanvas = new(newW, newH, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(newCanvas))
        {
            g.Clear(Color.White);
            g.DrawImageUnscaled(_canvasResizeSnapshot, imgX, imgY);
        }
        _canvas.Dispose();
        _canvas = newCanvas;
        RefreshCanvas();
    }

    private void DrawCanvasResizeHandles(Graphics g)
    {
        var (scale, offsetX, offsetY) = GetImageDisplayTransform();
        float iw = _canvas.Width * scale;
        float ih = _canvas.Height * scale;
        float hs = ResizeHandleSize;

        using SolidBrush brush = new(Color.FromArgb(180, 100, 100, 100));
        using Pen pen = new(Color.FromArgb(200, 200, 200), 1f);

        RectangleF[] handles =
        [
            new(offsetX - hs / 2,          offsetY - hs / 2,          hs, hs),  // TopLeft
            new(offsetX + iw / 2 - hs / 2, offsetY - hs / 2,          hs, hs),  // Top
            new(offsetX + iw - hs / 2,     offsetY - hs / 2,          hs, hs),  // TopRight
            new(offsetX + iw - hs / 2,     offsetY + ih / 2 - hs / 2, hs, hs),  // Right
            new(offsetX + iw - hs / 2,     offsetY + ih - hs / 2,     hs, hs),  // BottomRight
            new(offsetX + iw / 2 - hs / 2, offsetY + ih - hs / 2,     hs, hs),  // Bottom
            new(offsetX - hs / 2,          offsetY + ih - hs / 2,     hs, hs),  // BottomLeft
            new(offsetX - hs / 2,          offsetY + ih / 2 - hs / 2, hs, hs)   // Left
        ];

        foreach (RectangleF hr in handles)
        {
            g.FillRectangle(brush, hr);
            g.DrawRectangle(pen, hr.X, hr.Y, hr.Width, hr.Height);
        }
    }

    // ?? Undo / Redo ??????????????????????????????????????????????

    private void PushUndo()
    {
        if (_undoStack.Count >= MaxUndoLevels)
        {
            // Evict the oldest entry to stay within the limit.
            // Stack doesn't support removing from the bottom, so rebuild.
            var items = _undoStack.ToArray();          // top-first
            items[^1].Dispose();                        // dispose oldest
            _undoStack.Clear();
            for (int i = items.Length - 2; i >= 0; i--) // push back oldest-first
                _undoStack.Push(items[i]);
        }

        _undoStack.Push(new Bitmap(_canvas));

        // Any new mutation invalidates the redo history
        foreach (Bitmap bmp in _redoStack) bmp.Dispose();
        _redoStack.Clear();

        UpdateUndoRedoState();
    }

    private void Undo()
    {
        if (_undoStack.Count == 0) return;

        _redoStack.Push(new Bitmap(_canvas));
        _canvas.Dispose();
        _canvas = _undoStack.Pop();
        RefreshCanvas();
        UpdateUndoRedoState();
    }

    private void Redo()
    {
        if (_redoStack.Count == 0) return;

        _undoStack.Push(new Bitmap(_canvas));
        _canvas.Dispose();
        _canvas = _redoStack.Pop();
        RefreshCanvas();
        UpdateUndoRedoState();
    }

    private void UpdateUndoRedoState()
    {
        _btnUndo.Enabled = _undoStack.Count > 0;
        _btnRedo.Enabled = _redoStack.Count > 0;
    }

    // ?? Paste layer helpers ??????????????????????????????????????

    private bool IsPointInsideLayer(Point imgPt)
    {
        if (_pastedLayer == null) return false;
        Rectangle layerRect = new(_layerOffset.X, _layerOffset.Y, _layerSize.Width, _layerSize.Height);
        return layerRect.Contains(imgPt);
    }

    private void CommitLayer()
    {
        if (_pastedLayer == null) return;

        PushUndo();
        using Graphics g = Graphics.FromImage(_canvas);
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        g.DrawImage(_pastedLayer, _layerOffset.X, _layerOffset.Y, _layerSize.Width, _layerSize.Height);

        _pastedLayer.Dispose();
        _pastedLayer = null;
        _layerPlacementActive = false;
        _isDraggingLayer = false;
        _isResizingLayer = false;
        _activeResizeHandle = ResizeHandle.None;
        Cursor = _activeTool != DrawingTool.None ? Cursors.Cross : Cursors.Default;
        RefreshCanvas();
    }

    private void CancelLayerPlacement()
    {
        _pastedLayer?.Dispose();
        _pastedLayer = null;
        _layerPlacementActive = false;
        _isDraggingLayer = false;
        _isResizingLayer = false;
        _activeResizeHandle = ResizeHandle.None;
        Cursor = _activeTool != DrawingTool.None ? Cursors.Cross : Cursors.Default;
        _preview.Invalidate();
    }

    private ResizeHandle HitTestResizeHandle(Point imgPt)
    {
        if (_pastedLayer == null) return ResizeHandle.None;

        int x = _layerOffset.X;
        int y = _layerOffset.Y;
        int w = _layerSize.Width;
        int h = _layerSize.Height;
        int hs = ResizeHandleSize;

        Rectangle TopLeft()     => new(x - hs, y - hs, hs * 2, hs * 2);
        Rectangle Top()         => new(x + w / 2 - hs, y - hs, hs * 2, hs * 2);
        Rectangle TopRight()    => new(x + w - hs, y - hs, hs * 2, hs * 2);
        Rectangle Right()       => new(x + w - hs, y + h / 2 - hs, hs * 2, hs * 2);
        Rectangle BottomRight() => new(x + w - hs, y + h - hs, hs * 2, hs * 2);
        Rectangle Bottom()      => new(x + w / 2 - hs, y + h - hs, hs * 2, hs * 2);
        Rectangle BottomLeft()  => new(x - hs, y + h - hs, hs * 2, hs * 2);
        Rectangle Left()        => new(x - hs, y + h / 2 - hs, hs * 2, hs * 2);

        if (TopLeft().Contains(imgPt))     return ResizeHandle.TopLeft;
        if (TopRight().Contains(imgPt))    return ResizeHandle.TopRight;
        if (BottomRight().Contains(imgPt)) return ResizeHandle.BottomRight;
        if (BottomLeft().Contains(imgPt))  return ResizeHandle.BottomLeft;
        if (Top().Contains(imgPt))         return ResizeHandle.Top;
        if (Right().Contains(imgPt))       return ResizeHandle.Right;
        if (Bottom().Contains(imgPt))      return ResizeHandle.Bottom;
        if (Left().Contains(imgPt))        return ResizeHandle.Left;

        return ResizeHandle.None;
    }

    private void ApplyResize(int dx, int dy)
    {
        const int minSize = 16;
        int x = _resizeStartRect.X;
        int y = _resizeStartRect.Y;
        int w = _resizeStartRect.Width;
        int h = _resizeStartRect.Height;

        switch (_activeResizeHandle)
        {
            case ResizeHandle.TopLeft:     x += dx; y += dy; w -= dx; h -= dy; break;
            case ResizeHandle.Top:         y += dy; h -= dy; break;
            case ResizeHandle.TopRight:    y += dy; w += dx; h -= dy; break;
            case ResizeHandle.Right:       w += dx; break;
            case ResizeHandle.BottomRight: w += dx; h += dy; break;
            case ResizeHandle.Bottom:      h += dy; break;
            case ResizeHandle.BottomLeft:  x += dx; w -= dx; h += dy; break;
            case ResizeHandle.Left:        x += dx; w -= dx; break;
        }

        if (w < minSize) { if (x != _resizeStartRect.X) x = _resizeStartRect.X + _resizeStartRect.Width - minSize; w = minSize; }
        if (h < minSize) { if (y != _resizeStartRect.Y) y = _resizeStartRect.Y + _resizeStartRect.Height - minSize; h = minSize; }

        _layerOffset = new Point(x, y);
        _layerSize = new Size(w, h);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _pastedLayer?.Dispose();
            _canvasResizeSnapshot?.Dispose();
            foreach (Bitmap bmp in _undoStack) bmp.Dispose();
            _undoStack.Clear();
            foreach (Bitmap bmp in _redoStack) bmp.Dispose();
            _redoStack.Clear();
            _iconCache.Dispose();
        }
        base.Dispose(disposing);
    }

    // ?? Toolbar event handlers ???????????????????????????????????

    private void OnSnip(object? sender, EventArgs e) => DoSnip(0);

    private void OnSnipDelayed(object? sender, EventArgs e) => DoSnip((int)_numDelay.Value);

    private void DoSnip(int delaySeconds)
    {
        if (delaySeconds > 0)
        {
            Visible = false;
            System.Windows.Forms.Timer timer = new() { Interval = delaySeconds * 1000 };
            timer.Tick += (_, __) => { timer.Stop(); timer.Dispose(); PerformSnipCapture(); };
            timer.Start();
        }
        else
        {
            Visible = false;
            PerformSnipCapture();
        }
    }

    private void PerformSnipCapture()
    {
        using SnipOverlayForm overlay = new();
        if (overlay.ShowDialog() == DialogResult.OK && overlay.CapturedImage != null)
        {
            PushUndo();
            _canvas.Dispose();
            _canvas = new Bitmap(overlay.CapturedImage);
            RefreshCanvas();
        }
        Visible = true;
    }

    private void OnCopy(object? sender, EventArgs e) => Clipboard.SetImage(_canvas);

    private void OnPasteReplace(object? sender, EventArgs e)
    {
        if (!Clipboard.ContainsImage()) return;
        Image? img = Clipboard.GetImage();
        if (img == null) return;
        PushUndo();
        _canvas.Dispose();
        _canvas = new Bitmap(img);
        _strokes.Clear();
        RefreshCanvas();
    }

    private void OnPasteLayer(object? sender, EventArgs e)
    {
        if (!Clipboard.ContainsImage()) return;
        Image? img = Clipboard.GetImage();
        if (img == null) return;
        _pastedLayer?.Dispose();
        _pastedLayer = new Bitmap(img);
        _layerSize = _pastedLayer.Size;
        _layerOffset = new Point(
            Math.Max(0, (_canvas.Width - _layerSize.Width) / 2),
            Math.Max(0, (_canvas.Height - _layerSize.Height) / 2));
        _layerPlacementActive = true;
        Cursor = Cursors.SizeAll;
        _preview.Invalidate();
    }

    private void OnResizeCanvas(object? sender, EventArgs e)
    {
        using CanvasResizeDialog dlg = new(_canvas.Width, _canvas.Height);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        int newW = dlg.CanvasWidth;
        int newH = dlg.CanvasHeight;
        ContentAlignment anchor = dlg.Anchor;
        if (newW == _canvas.Width && newH == _canvas.Height) return;
        if (newW < 1 || newH < 1) return;
        int ox = anchor switch
        {
            ContentAlignment.TopLeft or ContentAlignment.MiddleLeft or ContentAlignment.BottomLeft => 0,
            ContentAlignment.TopCenter or ContentAlignment.MiddleCenter or ContentAlignment.BottomCenter => (newW - _canvas.Width) / 2,
            _ => newW - _canvas.Width
        };
        int oy = anchor switch
        {
            ContentAlignment.TopLeft or ContentAlignment.TopCenter or ContentAlignment.TopRight => 0,
            ContentAlignment.MiddleLeft or ContentAlignment.MiddleCenter or ContentAlignment.MiddleRight => (newH - _canvas.Height) / 2,
            _ => newH - _canvas.Height
        };
        PushUndo();
        Bitmap newCanvas = new(newW, newH, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(newCanvas))
        {
            g.Clear(Color.White);
            g.DrawImageUnscaled(_canvas, ox, oy);
        }
        _canvas.Dispose();
        _canvas = newCanvas;
        _strokes.Clear();
        RefreshCanvas();
    }

    private void OnMoveImage(object? sender, EventArgs e)
    {
        if (_layerPlacementActive) return;
        PushUndo();
        _pastedLayer?.Dispose();
        _pastedLayer = new Bitmap(_canvas);
        _layerSize = _pastedLayer.Size;
        _layerOffset = Point.Empty;
        _layerPlacementActive = true;
        using (Graphics g = Graphics.FromImage(_canvas))
            g.Clear(Color.White);
        Cursor = Cursors.SizeAll;
        RefreshCanvas();
    }

    private void SetActiveTool(DrawingTool tool)
    {
        _activeTool = _activeTool == tool ? DrawingTool.None : tool;
        _btnPencil.Checked = _activeTool == DrawingTool.Pencil;
        _btnMarker.Checked = _activeTool == DrawingTool.Marker;
        _btnEraser.Checked = _activeTool == DrawingTool.Eraser;
        _btnLine.Checked = _activeTool == DrawingTool.Line;
        _btnRect.Checked = _activeTool == DrawingTool.Rectangle;
        _btnEllipse.Checked = _activeTool == DrawingTool.Ellipse;
        _btnArrow.Checked = _activeTool == DrawingTool.Arrow;
        _btnText.Checked = _activeTool == DrawingTool.Text;
        _btnFill.Checked = _activeTool == DrawingTool.Fill;
        _btnEyedropper.Checked = _activeTool == DrawingTool.Eyedropper;
        _btnSelect.Checked = _activeTool == DrawingTool.Select;
        _btnCrop.Checked = _activeTool == DrawingTool.Crop;
        Cursor = _activeTool != DrawingTool.None ? Cursors.Cross : Cursors.Default;
        _selection = Rectangle.Empty;
        _preview.Invalidate();
    }

    private void OnPencilToggle(object? sender, EventArgs e) => SetActiveTool(DrawingTool.Pencil);
    private void OnMarkerToggle(object? sender, EventArgs e) => SetActiveTool(DrawingTool.Marker);
    private void OnEraserToggle(object? sender, EventArgs e) => SetActiveTool(DrawingTool.Eraser);
    private void OnLineToggle(object? sender, EventArgs e) => SetActiveTool(DrawingTool.Line);
    private void OnRectToggle(object? sender, EventArgs e) => SetActiveTool(DrawingTool.Rectangle);
    private void OnEllipseToggle(object? sender, EventArgs e) => SetActiveTool(DrawingTool.Ellipse);
    private void OnArrowToggle(object? sender, EventArgs e) => SetActiveTool(DrawingTool.Arrow);
    private void OnTextToggle(object? sender, EventArgs e) => SetActiveTool(DrawingTool.Text);
    private void OnFillToggle(object? sender, EventArgs e) => SetActiveTool(DrawingTool.Fill);
    private void OnEyedropperToggle(object? sender, EventArgs e) => SetActiveTool(DrawingTool.Eyedropper);
    private void OnSelectToggle(object? sender, EventArgs e) => SetActiveTool(DrawingTool.Select);
    private void OnCropToggle(object? sender, EventArgs e) => SetActiveTool(DrawingTool.Crop);

    private void OnPickColor(object? sender, EventArgs e)
    {
        using ColorDialog dlg = new() { Color = _drawColor, FullOpen = true };
        if (dlg.ShowDialog(this) == DialogResult.OK)
            _drawColor = dlg.Color;
    }

    private void OnBrushSize(object? sender, EventArgs e)
    {
        using TextInputDialog dlg = new("Brush Size", "Width (pixels):", _penWidth.ToString("0"));
        if (dlg.ShowDialog(this) == DialogResult.OK && float.TryParse(dlg.InputText, out float size) && size >= 1 && size <= 200)
        {
            _penWidth = size;
            _markerWidth = size * 4;
            _eraserWidth = size * 6;
        }
    }

    private void OnZoomIn(object? sender, EventArgs e) => ApplyZoom(_zoomLevel * 1.25f);
    private void OnZoomOut(object? sender, EventArgs e) => ApplyZoom(_zoomLevel / 1.25f);
    private void OnZoomFit(object? sender, EventArgs e) => ApplyZoom(1.0f);

    private void ApplyZoom(float newZoom)
    {
        _zoomLevel = Math.Clamp(newZoom, ZoomMin, ZoomMax);
        _lblZoom.Text = $"{(int)(_zoomLevel * 100)}%";
        _preview.Invalidate();
    }

    private void OnCropSelection(object? sender, EventArgs e)
    {
        if (_selection.Width < 2 || _selection.Height < 2) return;
        PushUndo();
        Bitmap cropped = new(_selection.Width, _selection.Height, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(cropped))
            g.DrawImage(_canvas, new Rectangle(0, 0, cropped.Width, cropped.Height), _selection, GraphicsUnit.Pixel);
        _canvas.Dispose();
        _canvas = cropped;
        _selection = Rectangle.Empty;
        _strokes.Clear();
        RefreshCanvas();
    }

    private void OnSave(object? sender, EventArgs e)
    {
        if (_layerPlacementActive) CommitLayer();
        EditedImage = new Bitmap(_canvas);
        DialogResult = DialogResult.OK;
        Close();
    }

    private void OnCancel(object? sender, EventArgs e)
    {
        EditedImage = null;
        DialogResult = DialogResult.Cancel;
        Close();
    }

    // ?? Shape / tool commit helpers ??????????????????????????????

    private void CommitShapeToCanvas()
    {
        if (!_isDrawingShape) return;
        PushUndo();
        using Graphics g = Graphics.FromImage(_canvas);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        DrawShapeOnGraphics(g, _shapeStart, _shapeEnd, 1f, 0, 0);
        _isDrawingShape = false;
        RefreshCanvas();
    }

    private void DrawShapeOnGraphics(Graphics g, Point p1, Point p2, float scale, float offX, float offY)
    {
        float x1 = p1.X * scale + offX, y1 = p1.Y * scale + offY;
        float x2 = p2.X * scale + offX, y2 = p2.Y * scale + offY;
        float rx = Math.Min(x1, x2), ry = Math.Min(y1, y2);
        float rw = Math.Abs(x2 - x1), rh = Math.Abs(y2 - y1);

        using Pen pen = new(_drawColor, _penWidth * scale) { StartCap = LineCap.Round, EndCap = LineCap.Round };

        switch (_activeTool)
        {
            case DrawingTool.Line:
                g.DrawLine(pen, x1, y1, x2, y2);
                break;
            case DrawingTool.Rectangle:
                g.DrawRectangle(pen, rx, ry, rw, rh);
                break;
            case DrawingTool.Ellipse:
                g.DrawEllipse(pen, rx, ry, rw, rh);
                break;
            case DrawingTool.Arrow:
                pen.EndCap = LineCap.ArrowAnchor;
                pen.CustomEndCap = new System.Drawing.Drawing2D.AdjustableArrowCap(5, 5);
                g.DrawLine(pen, x1, y1, x2, y2);
                break;
        }
    }

    private void CommitTextAtPoint(Point imgPt)
    {
        using TextInputDialog dlg = new("Add Text", "Text:", "");
        if (dlg.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(dlg.InputText)) return;
        PushUndo();
        using Graphics g = Graphics.FromImage(_canvas);
        using Font font = new("Segoe UI", _penWidth * 4, FontStyle.Regular);
        using SolidBrush brush = new(_drawColor);
        g.DrawString(dlg.InputText, font, brush, imgPt);
        RefreshCanvas();
    }

    private void FloodFill(Point pt)
    {
        if (pt.X < 0 || pt.Y < 0 || pt.X >= _canvas.Width || pt.Y >= _canvas.Height) return;
        PushUndo();
        Color target = _canvas.GetPixel(pt.X, pt.Y);
        if (target.ToArgb() == _drawColor.ToArgb()) return;
        Stack<Point> stack = new();
        stack.Push(pt);
        while (stack.Count > 0)
        {
            Point p = stack.Pop();
            if (p.X < 0 || p.Y < 0 || p.X >= _canvas.Width || p.Y >= _canvas.Height) continue;
            if (_canvas.GetPixel(p.X, p.Y).ToArgb() != target.ToArgb()) continue;
            _canvas.SetPixel(p.X, p.Y, _drawColor);
            stack.Push(new Point(p.X + 1, p.Y));
            stack.Push(new Point(p.X - 1, p.Y));
            stack.Push(new Point(p.X, p.Y + 1));
            stack.Push(new Point(p.X, p.Y - 1));
        }
        RefreshCanvas();
    }

    private void EyedropperPick(Point imgPt)
    {
        if (imgPt.X >= 0 && imgPt.Y >= 0 && imgPt.X < _canvas.Width && imgPt.Y < _canvas.Height)
            _drawColor = _canvas.GetPixel(imgPt.X, imgPt.Y);
        SetActiveTool(DrawingTool.None);
    }

    // ?? Inner types ??????????????????????????????????????????????

    private enum DrawingTool { None, Pencil, Marker, Eraser, Line, Rectangle, Ellipse, Arrow, Text, Fill, Eyedropper, Select, Crop }

    private enum ResizeHandle { None, TopLeft, Top, TopRight, Right, BottomRight, Bottom, BottomLeft, Left }

    private sealed class DrawingStroke
    {
        public Color Color { get; }
        public float Width { get; }
        public bool IsMarker { get; }
        public List<Point> Points { get; } = new();

        public DrawingStroke(Color color, float width, bool isMarker)
        {
            Color = color;
            Width = width;
            IsMarker = isMarker;
        }
    }

    private readonly struct CompositingModeScope : IDisposable
    {
        private readonly Graphics _g;
        private readonly CompositingMode _previous;

        public CompositingModeScope(Graphics g, CompositingMode mode)
        {
            _g = g;
            _previous = g.CompositingMode;
            g.CompositingMode = mode;
        }

        public void Dispose() => _g.CompositingMode = _previous;
    }

    // ?? Canvas resize dialog ?????????????????????????????????????

    private sealed class CanvasResizeDialog : Form
    {
        private readonly NumericUpDown _numWidth;
        private readonly NumericUpDown _numHeight;
        private readonly NumericUpDown _numPercent;
        private readonly RadioButton _rdPixels;
        private readonly RadioButton _rdPercent;
        private readonly Panel _anchorGrid;
        private readonly Button[] _anchorButtons = new Button[9];
        private readonly Panel _previewPanel;
        private readonly int _origW;
        private readonly int _origH;
        private bool _suppressSync;

        public int CanvasWidth => (int)_numWidth.Value;
        public int CanvasHeight => (int)_numHeight.Value;
        public ContentAlignment Anchor { get; private set; } = ContentAlignment.MiddleCenter;

        public CanvasResizeDialog(int currentWidth, int currentHeight)
        {
            _origW = currentWidth; _origH = currentHeight;
            Text = "Resize Canvas"; StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            ShowInTaskbar = false; Size = new Size(420, 400);
            TableLayoutPanel root = new() { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 7, Padding = new Padding(12) };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            Controls.Add(root);
            FlowLayoutPanel modePanel = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
            _rdPixels = new RadioButton { Text = "Pixels", Checked = true, AutoSize = true, Margin = new Padding(0, 2, 12, 0) };
            _rdPercent = new RadioButton { Text = "Percent", AutoSize = true, Margin = new Padding(0, 2, 0, 0) };
            modePanel.Controls.Add(_rdPixels); modePanel.Controls.Add(_rdPercent);
            root.Controls.Add(new Label { Text = "Mode:", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 0, 0);
            root.Controls.Add(modePanel, 1, 0);
            root.Controls.Add(new Label { Text = "Width:", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 0, 1);
            Panel widthRow = new() { Dock = DockStyle.Fill };
            _numWidth = new NumericUpDown { Minimum = 1, Maximum = 20000, Value = currentWidth, Width = 120, Font = new Font("Segoe UI", 10F), Location = new Point(0, 3) };
            _numPercent = new NumericUpDown { Minimum = 1, Maximum = 1000, Value = 100, DecimalPlaces = 0, Width = 120, Font = new Font("Segoe UI", 10F), Location = new Point(0, 3), Visible = false };
            Label lblPctSign = new() { Text = "%", AutoSize = true, Location = new Point(124, 7), Visible = false };
            widthRow.Controls.Add(_numWidth); widthRow.Controls.Add(_numPercent); widthRow.Controls.Add(lblPctSign);
            root.Controls.Add(widthRow, 1, 1);
            Label lblHeight = new() { Text = "Height:", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill };
            root.Controls.Add(lblHeight, 0, 2);
            _numHeight = new NumericUpDown { Minimum = 1, Maximum = 20000, Value = currentHeight, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10F) };
            root.Controls.Add(_numHeight, 1, 2);
            _rdPixels.CheckedChanged += (_, __) => { bool px = _rdPixels.Checked; _numWidth.Visible = px; _numPercent.Visible = !px; lblPctSign.Visible = !px; lblHeight.Visible = px; _numHeight.Visible = px; if (!px) { _suppressSync = true; _numPercent.Value = 100; _suppressSync = false; } else SyncPixelsFromPercent(); _previewPanel.Invalidate(); };
            _numPercent.ValueChanged += (_, __) => { if (_suppressSync) return; SyncPixelsFromPercent(); _previewPanel.Invalidate(); };
            _numWidth.ValueChanged += (_, __) => _previewPanel.Invalidate();
            _numHeight.ValueChanged += (_, __) => _previewPanel.Invalidate();
            Label lblAnchor = new() { Text = "Anchor:", TextAlign = ContentAlignment.BottomLeft, Dock = DockStyle.Fill, AutoSize = false };
            root.Controls.Add(lblAnchor, 0, 3); root.SetColumnSpan(lblAnchor, 2);
            FlowLayoutPanel anchorRow = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
            root.Controls.Add(anchorRow, 0, 4); root.SetColumnSpan(anchorRow, 2);
            _anchorGrid = new Panel { Width = 108, Height = 108, Margin = new Padding(0, 4, 12, 0) };
            BuildAnchorGrid(); anchorRow.Controls.Add(_anchorGrid);
            _previewPanel = new Panel { Width = 140, Height = 108, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(0, 4, 0, 0) };
            _previewPanel.Paint += PreviewPanel_Paint; anchorRow.Controls.Add(_previewPanel);
            Label lblInfo = new() { Text = $"Current: {currentWidth} × {currentHeight}", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill, ForeColor = SystemColors.GrayText, AutoSize = false };
            root.Controls.Add(lblInfo, 0, 5); root.SetColumnSpan(lblInfo, 2);
            FlowLayoutPanel buttons = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
            root.Controls.Add(buttons, 0, 6); root.SetColumnSpan(buttons, 2);
            Button btnCancel = new() { Text = "Cancel", Width = 90, DialogResult = DialogResult.Cancel };
            Button btnOk = new() { Text = "OK", Width = 90, DialogResult = DialogResult.OK };
            buttons.Controls.Add(btnCancel); buttons.Controls.Add(btnOk);
            AcceptButton = btnOk; CancelButton = btnCancel;
            SelectAnchorButton(4);
        }
        private void SyncPixelsFromPercent() { _suppressSync = true; decimal pct = _numPercent.Value / 100m; _numWidth.Value = Math.Clamp((int)Math.Round(_origW * pct), 1, 20000); _numHeight.Value = Math.Clamp((int)Math.Round(_origH * pct), 1, 20000); _suppressSync = false; }
        private void BuildAnchorGrid()
        {
            ContentAlignment[] anchors = [ContentAlignment.TopLeft, ContentAlignment.TopCenter, ContentAlignment.TopRight, ContentAlignment.MiddleLeft, ContentAlignment.MiddleCenter, ContentAlignment.MiddleRight, ContentAlignment.BottomLeft, ContentAlignment.BottomCenter, ContentAlignment.BottomRight];
            for (int i = 0; i < 9; i++) { int row = i / 3, col = i % 3, idx = i; Button btn = new() { Size = new Size(32, 32), Location = new Point(col * 34, row * 34), FlatStyle = FlatStyle.Flat, Text = "", Tag = anchors[i] }; btn.FlatAppearance.BorderSize = 1; btn.Click += (_, __) => SelectAnchorButton(idx); _anchorGrid.Controls.Add(btn); _anchorButtons[i] = btn; }
        }
        private void SelectAnchorButton(int index) { Anchor = (ContentAlignment)_anchorButtons[index].Tag!; for (int i = 0; i < 9; i++) { _anchorButtons[i].BackColor = i == index ? Color.FromArgb(0, 122, 204) : SystemColors.Control; _anchorButtons[i].ForeColor = i == index ? Color.White : SystemColors.ControlText; } _previewPanel.Invalidate(); }
        private void PreviewPanel_Paint(object? sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.Clear(Color.FromArgb(240, 240, 240));
            int newW = (int)_numWidth.Value, newH = (int)_numHeight.Value; if (newW < 1 || newH < 1) return;
            float pw = _previewPanel.ClientSize.Width - 8, ph = _previewPanel.ClientSize.Height - 8;
            int dispRefW = Math.Max(newW, _origW), dispRefH = Math.Max(newH, _origH);
            float scale = Math.Min(pw / dispRefW, ph / dispRefH);
            float canvasDispW = newW * scale, canvasDispH = newH * scale;
            float cx = (_previewPanel.ClientSize.Width - Math.Max(canvasDispW, _origW * scale)) / 2f;
            float cy = (_previewPanel.ClientSize.Height - Math.Max(canvasDispH, _origH * scale)) / 2f;
            int ox = Anchor switch { ContentAlignment.TopLeft or ContentAlignment.MiddleLeft or ContentAlignment.BottomLeft => 0, ContentAlignment.TopCenter or ContentAlignment.MiddleCenter or ContentAlignment.BottomCenter => (newW - _origW) / 2, _ => newW - _origW };
            int oy = Anchor switch { ContentAlignment.TopLeft or ContentAlignment.TopCenter or ContentAlignment.TopRight => 0, ContentAlignment.MiddleLeft or ContentAlignment.MiddleCenter or ContentAlignment.MiddleRight => (newH - _origH) / 2, _ => newH - _origH };
            float canvasX = cx + Math.Max(0, -ox * scale), canvasY = cy + Math.Max(0, -oy * scale);
            using SolidBrush cb = new(Color.White); g.FillRectangle(cb, canvasX, canvasY, canvasDispW, canvasDispH);
            using Pen cp = new(Color.Gray, 1f) { DashStyle = DashStyle.Dash }; g.DrawRectangle(cp, canvasX, canvasY, canvasDispW, canvasDispH);
            float imgDispX = canvasX + ox * scale, imgDispY = canvasY + oy * scale, imgDispW = _origW * scale, imgDispH = _origH * scale;
            using SolidBrush ib = new(Color.FromArgb(60, 0, 122, 204)); g.FillRectangle(ib, imgDispX, imgDispY, imgDispW, imgDispH);
            using Pen ip = new(Color.FromArgb(0, 122, 204), 1.5f); g.DrawRectangle(ip, imgDispX, imgDispY, imgDispW, imgDispH);
        }
    }

    // ?? Minimal snip overlay ?????????????????????????????????????

    private sealed class SnipOverlayForm : Form
    {
        private Bitmap? _desktopBitmap; private Bitmap? _dimmedBitmap; private Rectangle _virtualBounds;
        private Rectangle _selection; private bool _isDragging; private Point _dragStart;
        public Bitmap? CapturedImage { get; private set; }
        public SnipOverlayForm()
        {
            AutoScaleMode = AutoScaleMode.None; DoubleBuffered = true; FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false; StartPosition = FormStartPosition.Manual; TopMost = true; KeyPreview = true; Cursor = Cursors.Cross;
            _virtualBounds = SystemInformation.VirtualScreen;
            _desktopBitmap = new Bitmap(_virtualBounds.Width, _virtualBounds.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(_desktopBitmap)) g.CopyFromScreen(_virtualBounds.Location, Point.Empty, _virtualBounds.Size);
            _dimmedBitmap = new Bitmap(_desktopBitmap.Width, _desktopBitmap.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(_dimmedBitmap)) { g.DrawImageUnscaled(_desktopBitmap, 0, 0); using SolidBrush b = new(Color.FromArgb(110, Color.SkyBlue)); g.FillRectangle(b, 0, 0, _dimmedBitmap.Width, _dimmedBitmap.Height); }
            Bounds = _virtualBounds; BackColor = Color.Black;
            MouseDown += (_, me) => { if (me.Button == MouseButtons.Left) { _isDragging = true; _dragStart = me.Location; _selection = new Rectangle(me.Location, Size.Empty); Invalidate(); } };
            MouseMove += (_, me) => { if (_isDragging) { _selection = Normalize(_dragStart, me.Location); Invalidate(); } };
            MouseUp += (_, me) => { if (!_isDragging) return; _isDragging = false; _selection = Normalize(_dragStart, me.Location); if (_selection.Width < 2 || _selection.Height < 2) { _selection = Rectangle.Empty; Invalidate(); return; } CapturedImage = new Bitmap(_selection.Width, _selection.Height, PixelFormat.Format32bppArgb); using (Graphics g = Graphics.FromImage(CapturedImage)) g.DrawImage(_desktopBitmap!, new Rectangle(0, 0, CapturedImage.Width, CapturedImage.Height), _selection, GraphicsUnit.Pixel); DialogResult = DialogResult.OK; Close(); };
            KeyDown += (_, ke) => { if (ke.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); } };
        }
        protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e); if (_dimmedBitmap != null) e.Graphics.DrawImageUnscaled(_dimmedBitmap, 0, 0); if (_selection.Width > 0 && _selection.Height > 0 && _desktopBitmap != null) { e.Graphics.SetClip(_selection); e.Graphics.DrawImageUnscaled(_desktopBitmap, 0, 0); e.Graphics.ResetClip(); using Pen p = new(Color.Red, 3f); e.Graphics.DrawRectangle(p, _selection); } }
        protected override void Dispose(bool disposing) { if (disposing) { _desktopBitmap?.Dispose(); _dimmedBitmap?.Dispose(); } base.Dispose(disposing); }
        private static Rectangle Normalize(Point a, Point b) => new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y));
    }
}
