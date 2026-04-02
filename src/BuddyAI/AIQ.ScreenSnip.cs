using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace BuddyAI;

public sealed partial class AIQ
{
    private sealed class ScreenSnipOverlayForm : Form
    {
        private Bitmap? _desktopBitmap;
        private Bitmap? _dimmedBitmap;
        private Rectangle _virtualBounds;
        private Rectangle _selection;
        private bool _isDragging;
        private Point _dragStart;

        public Bitmap? CapturedImage { get; private set; }

        public ScreenSnipOverlayForm()
        {
            AutoScaleMode = AutoScaleMode.None;
            DoubleBuffered = true;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            KeyPreview = true;
            Cursor = Cursors.Cross;

            BuildDesktopSnapshot();

            Bounds = _virtualBounds;
            BackColor = Color.Black;
            Opacity = 1.0;

            MouseDown += OnOverlayMouseDown;
            MouseMove += OnOverlayMouseMove;
            MouseUp += OnOverlayMouseUp;
            KeyDown += OnOverlayKeyDown;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Focus();
            Activate();
        }

        private void BuildDesktopSnapshot()
        {
            _virtualBounds = GetVirtualScreenBounds();

            _desktopBitmap = new Bitmap(_virtualBounds.Width, _virtualBounds.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(_desktopBitmap))
            {
                g.CopyFromScreen(_virtualBounds.Location, Point.Empty, _virtualBounds.Size);
            }

            _dimmedBitmap = new Bitmap(_desktopBitmap.Width, _desktopBitmap.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(_dimmedBitmap))
            {
                g.DrawImageUnscaled(_desktopBitmap, 0, 0);
                using SolidBrush brush = new(Color.FromArgb(110, Color.SkyBlue));
                g.FillRectangle(brush, 0, 0, _dimmedBitmap.Width, _dimmedBitmap.Height);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (_dimmedBitmap != null)
                e.Graphics.DrawImageUnscaled(_dimmedBitmap, 0, 0);

            if (_selection.Width > 0 && _selection.Height > 0 && _desktopBitmap != null)
            {
                e.Graphics.SetClip(_selection);
                e.Graphics.DrawImageUnscaled(_desktopBitmap, 0, 0);
                e.Graphics.ResetClip();

                using Pen borderPen = new(Color.Red, 3f);
                e.Graphics.DrawRectangle(borderPen, _selection);

                using SolidBrush infoBrush = new(Color.FromArgb(220, 30, 30, 30));
                string label = _selection.Width + " x " + _selection.Height;
                Size labelSize = TextRenderer.MeasureText(label, Font);
                Rectangle labelRect = new(_selection.X, Math.Max(0, _selection.Y - labelSize.Height - 6), labelSize.Width + 10, labelSize.Height + 4);
                e.Graphics.FillRectangle(infoBrush, labelRect);
                TextRenderer.DrawText(e.Graphics, label, Font, labelRect, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            string hint = "Drag to capture a region. Press Esc to cancel.";
            Rectangle hintRect = new(20, 20, 420, 28);
            using SolidBrush hintBrush = new(Color.FromArgb(180, 20, 20, 20));
            e.Graphics.FillRectangle(hintBrush, hintRect);
            TextRenderer.DrawText(e.Graphics, hint, Font, hintRect, Color.White, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.LeftAndRightPadding);
        }

        private void OnOverlayMouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;

            _isDragging = true;
            _dragStart = e.Location;
            _selection = new Rectangle(e.Location, Size.Empty);
            Invalidate();
        }

        private void OnOverlayMouseMove(object? sender, MouseEventArgs e)
        {
            if (!_isDragging)
                return;

            _selection = NormalizeRectangle(_dragStart, e.Location);
            Invalidate();
        }

        private void OnOverlayMouseUp(object? sender, MouseEventArgs e)
        {
            if (!_isDragging)
                return;

            _isDragging = false;
            _selection = NormalizeRectangle(_dragStart, e.Location);
            if (_selection.Width < 2 || _selection.Height < 2)
            {
                _selection = Rectangle.Empty;
                Invalidate();
                return;
            }

            CaptureSelectionAndClose();
        }

        private void OnOverlayKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();
            }
        }

        private void CaptureSelectionAndClose()
        {
            if (_desktopBitmap == null || _selection.Width <= 0 || _selection.Height <= 0)
            {
                DialogResult = DialogResult.Cancel;
                Close();
                return;
            }

            Bitmap captured = new(_selection.Width, _selection.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(captured))
            {
                g.DrawImage(_desktopBitmap, new Rectangle(0, 0, captured.Width, captured.Height), _selection, GraphicsUnit.Pixel);
            }

            CapturedImage = captured;
            DialogResult = DialogResult.OK;
            Close();
        }

        private static Rectangle GetVirtualScreenBounds()
        {
            return SystemInformation.VirtualScreen;
        }

        private static Rectangle NormalizeRectangle(Point p1, Point p2)
        {
            int x = Math.Min(p1.X, p2.X);
            int y = Math.Min(p1.Y, p2.Y);
            int width = Math.Abs(p2.X - p1.X);
            int height = Math.Abs(p2.Y - p1.Y);
            return new Rectangle(x, y, width, height);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _desktopBitmap?.Dispose();
                _desktopBitmap = null;
                _dimmedBitmap?.Dispose();
                _dimmedBitmap = null;
            }

            base.Dispose(disposing);
        }
    }
}
