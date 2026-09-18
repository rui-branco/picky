using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Picky
{
    /// <summary>
    /// Everything the picker looks like: metrics, layout and painting. The window
    /// and the settings preview both drive this one class, so what the preview
    /// shows cannot drift from what a link actually opens.
    /// </summary>
    public class PickerView : IDisposable
    {
        const int DesignCorner = 12;
        const int DesignRowH = 52;
        const int DesignHeaderH = 46;
        const int DesignPadX = 12;
        const int DesignIconSize = 24;
        const int DesignChipW = 20;
        const int DesignRowInset = 7;
        const int DesignEdgePad = 5;

        readonly double _scale;
        readonly int _corner;
        readonly int _rowH;
        readonly int _headerH;
        readonly int _padX;
        readonly int _iconSize;
        readonly int _chipW;
        readonly int _rowInset;
        readonly int _edgePad;
        int _pad;
        int _rowX;

        static readonly Color CBack = ColorTranslator.FromHtml("#1C1D20");
        static readonly Color CText = ColorTranslator.FromHtml("#F1F3F4");
        static readonly Color CDim = ColorTranslator.FromHtml("#9AA0A6");
        static readonly Color CDimmer = ColorTranslator.FromHtml("#6E7378");
        static readonly Color CSel = ColorTranslator.FromHtml("#2E3034");
        static readonly Color CHover = ColorTranslator.FromHtml("#26282C");
        static readonly Color CAccent = ColorTranslator.FromHtml("#8AB4F8");
        static readonly Color CBorder = ColorTranslator.FromHtml("#34363B");
        static readonly Color CRule = ColorTranslator.FromHtml("#26282C");
        static readonly Color CChip = ColorTranslator.FromHtml("#303236");
        static readonly Color CTile = ColorTranslator.FromHtml("#232529");

        readonly List<Target> _targets;
        readonly string _url;
        readonly AppConfig _cfg;

        Font _fLabel;
        Font _fSub;
        Font _fHost;
        Font _fChip;
        Font _fTile;

        bool _row;
        bool _labels;
        int _cellW;
        int _cellH;
        int _measuredHeaderH;
        int _w;
        int _h;

        /// <summary>Scaled corner radius for the window region.</summary>
        public int CornerRadius { get { return _corner; } }

        public PickerView(List<Target> targets, string url, AppConfig cfg)
        {
            _targets = targets == null ? new List<Target>() : targets;
            _url = url;
            _cfg = cfg == null ? new AppConfig() : cfg;

            // Clamp defensively in case config was tampered with.
            double s = _cfg.PickerScale;
            if (s < 0.5) s = 0.5;
            if (s > 2.0) s = 2.0;
            _scale = s;

            _corner = S(DesignCorner);
            _rowH = S(DesignRowH);
            _headerH = S(DesignHeaderH);
            _padX = S(DesignPadX);
            _iconSize = S(DesignIconSize);
            _chipW = S(DesignChipW);
            _rowInset = S(DesignRowInset);
            _edgePad = S(DesignEdgePad);

            _fLabel = MakeFont((float)(10.5 * _scale));
            _fSub = MakeFont((float)(8.25 * _scale));
            _fHost = MakeFont((float)(9.75 * _scale));
            _fChip = MakeFont((float)(8.0 * _scale));
            _fTile = MakeFont((float)(9.0 * _scale));
        }

        /// <summary>Scales a design-size pixel value by the configured scale.</summary>
        int S(int designPx)
        {
            return (int)Math.Round(designPx * _scale);
        }

        static Font MakeFont(float size)
        {
            // Segoe UI Variable is the Windows 11 face; fall back cleanly on 10.
            string[] prefs = new string[] { "Segoe UI Variable Display", "Segoe UI" };
            foreach (string name in prefs)
            {
                try
                {
                    Font f = new Font(name, size, FontStyle.Regular);
                    if (string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase)) return f;
                    f.Dispose();
                }
                catch { }
            }
            return new Font(FontFamily.GenericSansSerif, size, FontStyle.Regular);
        }

        public Size Size { get { return new Size(_w, _h); } }

        /// <summary>
        /// Picks the cell size for the configured layout and returns the size the
        /// picker needs. Every measurement lands here, so painting and hit-testing
        /// never need a special case for one of the four layout combinations.
        /// </summary>
        public Size Measure(int screenWidth)
        {
            _row = _cfg.IsRowLayout();
            _labels = _cfg.ShowLabels;

            int n = _targets.Count;
            if (n < 1) n = 1;

            SizeCells();
            int w = _row ? n * _cellW + _pad * 2 : (_labels ? _cellW : _cellW + _pad * 2);

            // A strip wider than the screen would be unusable, and clamping its
            // position only pushes the far end off the edge - so a machine that
            // cannot fit the row gets the list instead.
            if (_row && w > screenWidth - 48)
            {
                _row = false;
                SizeCells();
                w = _labels ? _cellW : _cellW + _pad * 2;
            }

            // Asked for the address, so make room for it rather than dropping it:
            // a dock of logos is never 200px wide on its own, which used to make
            // the setting look broken.
            if (_cfg.ShowHost && w < S(190)) w = S(190);

            _measuredHeaderH = _cfg.ShowHost ? (_labels && !_row ? _headerH : S(38)) : 0;

            _w = w;
            // Room made for the address is room around the strip, not after it.
            _rowX = _row ? (_w - n * _cellW) / 2 : (_labels ? 0 : (_w - _cellW) / 2);
            // The same gap above the cells as below them. Padding only the bottom
            // left the logos sitting visibly high in the window.
            _h = _measuredHeaderH + _pad * 2 + (_row ? _cellH : n * _cellH);
            return new Size(_w, _h);
        }

        void SizeCells()
        {
            // Each tile is already inset inside its cell, so the padding round the
            // outside has to match that inset or the first and last gap read wider
            // than the ones between.
            _pad = _labels ? _edgePad : S(2);

            if (_row)
            {
                _cellW = _labels ? S(94) : S(42);
                _cellH = _labels ? (_cfg.ShowBrowserName ? S(88) : S(72)) : S(42);
            }
            else
            {
                _cellW = _labels ? S(420) : S(42);
                _cellH = _labels ? _rowH : S(42);
            }
        }

        public Rectangle CellRect(int i)
        {
            int top = _measuredHeaderH + _pad;
            if (_row) return new Rectangle(_rowX + i * _cellW, top, _cellW, _cellH);
            if (_labels) return new Rectangle(0, top + i * _cellH, _w, _cellH);
            // The dock gets the same gap at its sides as above and below it.
            return new Rectangle(_rowX, top + i * _cellH, _cellW, _cellH);
        }

        public int IndexAt(Point p)
        {
            for (int i = 0; i < _targets.Count; i++)
            {
                if (CellRect(i).Contains(p)) return i;
            }
            return -1;
        }

        public static GraphicsPath RoundedPath(RectangleF r, float radius)
        {
            GraphicsPath p = new GraphicsPath();
            float d = radius * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public void Paint(Graphics g, int sel, int hover)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // Filling the rounded path rather than clearing the whole surface lets
            // the preview render onto a transparent bitmap with the same code the
            // window uses, where the region clips the corners away instead.
            using (GraphicsPath p = RoundedPath(new RectangleF(0, 0, _w, _h), _corner))
            using (SolidBrush b = new SolidBrush(CBack))
                g.FillPath(b, p);

            if (_measuredHeaderH > 0) DrawHeader(g);

            for (int i = 0; i < _targets.Count; i++)
                DrawCell(g, i, sel, hover);

            // Hairline border traced along the rounded edge.
            using (GraphicsPath p = RoundedPath(new RectangleF(0.5f, 0.5f, _w - 1f, _h - 1f), _corner))
            using (Pen bp = new Pen(CBorder, 1f))
                g.DrawPath(bp, p);
        }

        void DrawHeader(Graphics g)
        {
            string host = AppConfig.GetHost(_url);
            if (string.IsNullOrEmpty(host)) host = _url;

            TextFormatFlags f = TextFormatFlags.Left | TextFormatFlags.VerticalCenter
                              | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;

            Rectangle r = new Rectangle(_padX + S(6), 0, _w - (_padX + S(6)) * 2, _measuredHeaderH);
            TextRenderer.DrawText(g, host, _fHost, r, CAccent, f);

            using (Pen p = new Pen(CRule, 1f))
                g.DrawLine(p, _padX, _measuredHeaderH - 1, _w - _padX, _measuredHeaderH - 1);
        }

        void DrawCell(Graphics g, int i, int sel, int hover)
        {
            Rectangle r = CellRect(i);
            bool selected = (i == sel);
            bool hovered = (i == hover);

            // A logo with no text beside it needs a container of its own, or it
            // floats on the background with nothing to say where the click lands.
            bool always = !_labels;

            if (always || selected || hovered)
            {
                // Twice the inset, never S(4): each S() rounds on its own, so at
                // 0.8 the pair came out 2 and 3 and every tile sat a pixel low and
                // right of centre.
                int ins = S(2);
                RectangleF fill = (_row || !_labels)
                    ? new RectangleF(r.X + ins, r.Y + ins, r.Width - ins * 2, r.Height - ins * 2)
                    : new RectangleF(_rowInset, r.Y + ins, _w - _rowInset * 2, r.Height - ins * 2);
                Color shade = selected ? CSel : (hovered ? CHover : CTile);
                using (GraphicsPath p = RoundedPath(fill, S(9)))
                using (SolidBrush b = new SolidBrush(shade))
                    g.FillPath(b, p);
            }

            if (_row && _labels) DrawTile(g, i, r, selected);
            else if (_labels) DrawListRow(g, i, r, selected);
            else DrawIconOnly(g, i, r, selected);
        }

        void DrawIcon(Graphics g, Target t, Rectangle box)
        {
            if (t.Image == null) return;
            try
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(t.Image, box);
            }
            catch { }
        }

        /// <summary>Logo alone: a dock, as tight as the artwork allows.</summary>
        void DrawIconOnly(Graphics g, int i, Rectangle r, bool selected)
        {
            // No shortcut digit here. Printing one needs a band of its own under
            // every logo, and that band was most of the height of a mode whose
            // whole point is to be small. The number keys still launch.
            int size = S(28);
            DrawIcon(g, _targets[i], new Rectangle(
                r.X + (r.Width - size) / 2, r.Y + (r.Height - size) / 2, size, size));
        }

        /// <summary>Logo above a centred name - the horizontal strip with labels.</summary>
        void DrawTile(Graphics g, int i, Rectangle r, bool selected)
        {
            Target t = _targets[i];
            int size = S(34);

            DrawIcon(g, t, new Rectangle(r.X + (r.Width - size) / 2, r.Y + S(10), size, size));

            TextFormatFlags f = TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis
                              | TextFormatFlags.NoPrefix;

            int nameY = S(48);
            int m = S(6);
            TextRenderer.DrawText(g, t.ProfileLabel, _fTile,
                new Rectangle(r.X + m, r.Y + nameY, r.Width - m * 2, S(18)), CText, f);

            if (_cfg.ShowBrowserName)
                TextRenderer.DrawText(g, t.BrowserName, _fChip,
                    new Rectangle(r.X + m, r.Y + S(66), r.Width - m * 2, S(16)), CDim, f);

            if (i >= 9) return;
            TextRenderer.DrawText(g, (i + 1).ToString(), _fChip,
                new Rectangle(r.Right - S(20), r.Y + S(4), S(16), S(16)), selected ? CDim : CDimmer,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }

        /// <summary>Logo, name and account on one line - the vertical list.</summary>
        void DrawListRow(Graphics g, int i, Rectangle r, bool selected)
        {
            Target t = _targets[i];

            int x = _padX + S(6);
            DrawIcon(g, t, new Rectangle(x, r.Y + (r.Height - _iconSize) / 2, _iconSize, _iconSize));
            x += _iconSize + S(12);

            int chipSpace = _chipW + S(10);
            int textW = _w - x - _padX - chipSpace;

            TextFormatFlags f = TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;

            // The second line is whichever of the browser and the account is
            // wanted and present; with neither, the name takes the row alone.
            string second = _cfg.ShowBrowserName ? t.BrowserName : null;
            if (!string.IsNullOrEmpty(t.Subtitle))
            {
                second = string.IsNullOrEmpty(second)
                    ? t.Subtitle : second + "  \u00B7  " + t.Subtitle;
            }

            if (string.IsNullOrEmpty(second))
            {
                TextRenderer.DrawText(g, t.ProfileLabel, _fLabel,
                    new Rectangle(x, r.Y, textW, r.Height), CText,
                    f | TextFormatFlags.VerticalCenter);
            }
            else
            {
                TextRenderer.DrawText(g, t.ProfileLabel, _fLabel,
                    new Rectangle(x, r.Y + S(8), textW, S(19)), CText, f);
                TextRenderer.DrawText(g, second, _fSub,
                    new Rectangle(x, r.Y + S(27), textW, S(17)), CDim, f);
            }

            if (i < 9)
            {
                RectangleF chip = new RectangleF(
                    _w - _padX - _chipW - S(4), r.Y + (r.Height - _chipW) / 2f, _chipW, _chipW);
                if (selected)
                {
                    using (GraphicsPath p = RoundedPath(chip, S(6)))
                    using (SolidBrush b = new SolidBrush(CChip))
                        g.FillPath(b, p);
                }
                TextFormatFlags fn = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                                   | TextFormatFlags.NoPrefix;
                TextRenderer.DrawText(g, (i + 1).ToString(), _fChip,
                    Rectangle.Round(chip), selected ? CDim : CDimmer, fn);
            }
        }

        public void Dispose()
        {
            if (_fLabel != null) { _fLabel.Dispose(); _fLabel = null; }
            if (_fSub != null) { _fSub.Dispose(); _fSub = null; }
            if (_fHost != null) { _fHost.Dispose(); _fHost = null; }
            if (_fChip != null) { _fChip.Dispose(); _fChip = null; }
            if (_fTile != null) { _fTile.Dispose(); _fTile = null; }
        }
    }

    /// <summary>
    /// The settings preview: the real picker, rendered to a bitmap and scaled to
    /// the width on offer. Anything taller than the box is simply cut off, which
    /// keeps the logos at a readable size instead of shrinking a long list to dots.
    /// </summary>
    public class PickerPreview : Control
    {
        const string Sample = "https://example.com";

        List<Target> _targets;
        AppConfig _cfg;
        PickerView _view;
        Size _pickerSize;

        int _hover = -1;
        int _drag = -1;
        bool _dragging;
        Point _down;

        /// <summary>Raised once a drag has actually changed the order.</summary>
        public event EventHandler Reordered;

        /// <summary>What the menu measures right now, so the host can hand the
        /// preview exactly that much room and show it at life size.</summary>
        public Size PickerSize { get { return _pickerSize; } }

        public PickerPreview()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                   | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Card;
        }

        public void Bind(List<Target> targets, AppConfig cfg)
        {
            _targets = targets;
            _cfg = cfg;

            if (_view != null) { _view.Dispose(); _view = null; }
            _pickerSize = Size.Empty;

            if (_targets != null && _targets.Count > 0 && _cfg != null)
            {
                // Kept rather than rebuilt per paint: the same instance answers
                // both what to draw and which entry the pointer is over.
                _view = new PickerView(_targets, Sample, _cfg);
                _pickerSize = _view.Measure(Screen.PrimaryScreen.WorkingArea.Width);
            }
            Invalidate();
        }

        /// <summary>How much the menu is shrunk to fit the room on offer.</summary>
        float Scale()
        {
            if (_pickerSize.Width < 1 || _pickerSize.Height < 1) return 1f;
            return Math.Min(1f, Math.Min(
                ClientSize.Width / (float)_pickerSize.Width,
                ClientSize.Height / (float)_pickerSize.Height));
        }

        int OffsetX(float scale)
        {
            return (ClientSize.Width - (int)Math.Round(_pickerSize.Width * scale)) / 2;
        }

        /// <summary>
        /// Centred down the box as well as across it. Pinned to the top, a menu
        /// smaller than the space it was given piled all its slack underneath,
        /// which reads as uneven padding around the menu itself.
        /// </summary>
        int OffsetY(float scale)
        {
            return (ClientSize.Height - (int)Math.Round(_pickerSize.Height * scale)) / 2;
        }

        /// <summary>Control point to a point inside the menu it is showing.</summary>
        int IndexAt(Point p)
        {
            if (_view == null) return -1;
            float s = Scale();
            if (s <= 0f) return -1;
            return _view.IndexAt(new Point(
                (int)((p.X - OffsetX(s)) / s), (int)((p.Y - OffsetY(s)) / s)));
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            _drag = IndexAt(e.Location);
            _down = e.Location;
            _dragging = false;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int i = IndexAt(e.Location);

            if (_drag >= 0 && e.Button == MouseButtons.Left)
            {
                // A few pixels of slack, so a click that wobbles is not a drag.
                if (!_dragging
                    && Math.Abs(e.X - _down.X) < 4 && Math.Abs(e.Y - _down.Y) < 4) return;

                _dragging = true;
                Cursor = Cursors.SizeAll;

                if (i >= 0 && i != _drag && _targets != null && _drag < _targets.Count)
                {
                    Target moved = _targets[_drag];
                    _targets.RemoveAt(_drag);
                    _targets.Insert(i, moved);
                    _drag = i;
                    Invalidate();
                }
                return;
            }

            if (i != _hover)
            {
                _hover = i;
                Cursor = i >= 0 ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            bool moved = _dragging;

            _drag = -1;
            _dragging = false;
            Cursor = IndexAt(e.Location) >= 0 ? Cursors.Hand : Cursors.Default;

            if (moved && Reordered != null) Reordered(this, EventArgs.Empty);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_dragging) return;   // keep the held entry lit while dragging out
            _hover = -1;
            Cursor = Cursors.Default;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            using (SolidBrush b = new SolidBrush(Theme.Card))
                g.FillRectangle(b, ClientRectangle);

            if (_view == null || _targets == null || _targets.Count == 0)
            {
                TextRenderer.DrawText(g, "No browsers found", Font, ClientRectangle, Theme.Dimmer,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            Size s = _pickerSize;
            if (s.Width < 1 || s.Height < 1) return;

            using (Bitmap bmp = new Bitmap(s.Width, s.Height))
            {
                using (Graphics bg = Graphics.FromImage(bmp))
                {
                    bg.Clear(Color.Transparent);
                    _view.Paint(bg, _dragging ? _drag : 0, _hover);
                }

                // Life size whenever it fits, and only ever scaled down - a
                // preview that cropped the last entries answers the wrong question.
                float scale = Scale();
                int w = (int)Math.Round(s.Width * scale);
                int h = (int)Math.Round(s.Height * scale);

                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(bmp, new Rectangle(OffsetX(scale), OffsetY(scale), w, h));
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _view != null) { _view.Dispose(); _view = null; }
            base.Dispose(disposing);
        }
    }
}
