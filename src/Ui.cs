using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Picky
{
    public static class Theme
    {
        public static readonly Color Back = ColorTranslator.FromHtml("#1C1D20");
        public static readonly Color Card = ColorTranslator.FromHtml("#232529");
        public static readonly Color CardHi = ColorTranslator.FromHtml("#2E3034");
        public static readonly Color Hover = ColorTranslator.FromHtml("#292B2F");
        public static readonly Color Text = ColorTranslator.FromHtml("#F1F3F4");
        public static readonly Color Dim = ColorTranslator.FromHtml("#9AA0A6");
        public static readonly Color Dimmer = ColorTranslator.FromHtml("#6E7378");
        public static readonly Color Accent = ColorTranslator.FromHtml("#8AB4F8");
        public static readonly Color AccentDark = ColorTranslator.FromHtml("#4D7FD1");
        public static readonly Color Border = ColorTranslator.FromHtml("#34363B");
        public static readonly Color Good = ColorTranslator.FromHtml("#81C995");
        public static readonly Color Warn = ColorTranslator.FromHtml("#FDD663");
        public static readonly Color OnAccent = ColorTranslator.FromHtml("#16212E");

        public static Font Font(float size, FontStyle style)
        {
            string[] prefs = new string[] { "Segoe UI Variable Display", "Segoe UI" };
            foreach (string name in prefs)
            {
                try
                {
                    Font f = new Font(name, size, style);
                    if (string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase)) return f;
                    f.Dispose();
                }
                catch { }
            }
            return new System.Drawing.Font(FontFamily.GenericSansSerif, size, style);
        }

        public static GraphicsPath Rounded(RectangleF r, float radius)
        {
            GraphicsPath p = new GraphicsPath();
            float d = radius * 2;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    /// <summary>Rounded surface used to group controls.</summary>
    public class Card : Panel
    {
        public int Radius = 10;
        public Color Fill = Theme.Card;
        public bool Outline = false;

        public Card()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                   | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Back;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Back);
            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (GraphicsPath p = Theme.Rounded(r, Radius))
            {
                using (SolidBrush b = new SolidBrush(Fill)) g.FillPath(b, p);
                if (Outline)
                    using (Pen pen = new Pen(Theme.Border, 1f)) g.DrawPath(pen, p);
            }
        }
    }

    public class FlatButton : Button
    {
        public bool Primary = false;
        /// <summary>Text only - no fill, no border - for a bare inline action.</summary>
        public bool Ghost = false;
        public int Radius = 8;
        /// <summary>Colour behind the rounded corners - set to the card colour when placed on one.</summary>
        public Color Backdrop = Theme.Back;
        bool _hover;

        public FlatButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                   | ControlStyles.OptimizedDoubleBuffer, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = Theme.Back;
            Cursor = Cursors.Hand;
            Height = 34;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Backdrop);

            if (Ghost)
            {
                TextRenderer.DrawText(g, Text, Font, ClientRectangle,
                    _hover ? Theme.Text : Theme.Accent,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                    | TextFormatFlags.NoPrefix);
                return;
            }

            Color fill, fg;
            if (Primary)
            {
                fill = _hover ? Theme.Accent : Theme.AccentDark;
                fg = _hover ? Theme.OnAccent : Theme.Text;
            }
            else
            {
                fill = _hover ? Theme.CardHi : Theme.Card;
                fg = Theme.Text;
            }

            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (GraphicsPath p = Theme.Rounded(r, Radius))
            {
                using (SolidBrush b = new SolidBrush(fill)) g.FillPath(b, p);
                if (!Primary)
                    using (Pen pen = new Pen(Theme.Border, 1f)) g.DrawPath(pen, p);
            }

            TextRenderer.DrawText(g, Text, Font, ClientRectangle, fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }

    /// <summary>Owner-drawn list with an icon, a label and a dim second line.</summary>
    public class RowList : ListBox
    {
        /// <summary>Enables press-and-drag row reordering.</summary>
        public bool AllowReorder = false;
        public event EventHandler Reordered;

        int _pressIndex = -1;
        int _dragIndex = -1;
        int _dropIndex = -1;
        Point _pressPt;
        bool _dragging;

        public RowList()
        {
            DrawMode = DrawMode.OwnerDrawFixed;
            ItemHeight = 46;
            BorderStyle = BorderStyle.None;
            BackColor = Theme.Card;
            ForeColor = Theme.Text;
            IntegralHeight = false;
            Font = Theme.Font(9f, FontStyle.Regular);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!AllowReorder || e.Button != MouseButtons.Left) return;
            _pressIndex = IndexFromPoint(e.Location);
            _pressPt = e.Location;
            _dragging = false;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!AllowReorder || _pressIndex < 0 || e.Button != MouseButtons.Left) return;

            if (!_dragging)
            {
                // Only treat it as a drag once past the system threshold, so a
                // plain click still selects normally.
                if (Math.Abs(e.Y - _pressPt.Y) < SystemInformation.DragSize.Height &&
                    Math.Abs(e.X - _pressPt.X) < SystemInformation.DragSize.Width) return;
                _dragging = true;
                _dragIndex = _pressIndex;
                Cursor = Cursors.SizeNS;
            }

            int idx = IndexFromPoint(e.Location);
            if (idx == ListBox.NoMatches)
                idx = (e.Y < 0) ? 0 : Items.Count - 1;

            if (idx != _dropIndex)
            {
                _dropIndex = idx;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!AllowReorder) return;

            if (_dragging && _dragIndex >= 0 && _dropIndex >= 0 && _dragIndex != _dropIndex)
            {
                object item = Items[_dragIndex];
                BeginUpdate();
                Items.RemoveAt(_dragIndex);
                Items.Insert(_dropIndex, item);
                EndUpdate();
                SelectedIndex = _dropIndex;
                if (Reordered != null) Reordered(this, EventArgs.Empty);
            }

            _dragging = false;
            _pressIndex = -1;
            _dragIndex = -1;
            _dropIndex = -1;
            Cursor = Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (!_dragging) Cursor = Cursors.Default;
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= Items.Count) return;

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

            using (SolidBrush bg = new SolidBrush(Theme.Card))
                g.FillRectangle(bg, e.Bounds);

            if (selected)
            {
                RectangleF r = new RectangleF(e.Bounds.X + 5, e.Bounds.Y + 3,
                                              e.Bounds.Width - 10, e.Bounds.Height - 6);
                using (GraphicsPath p = Theme.Rounded(r, 8f))
                using (SolidBrush b = new SolidBrush(Theme.CardHi))
                    g.FillPath(b, p);
            }

            object item = Items[e.Index];
            Bitmap ico = null;
            string line1 = "", line2 = "";

            Target t = item as Target;
            if (t != null)
            {
                ico = t.Image;
                line1 = t.ProfileLabel;
                line2 = t.BrowserName;
                if (!string.IsNullOrEmpty(t.Subtitle)) line2 += "  \u00B7  " + t.Subtitle;
            }
            else
            {
                RuleRow rr = item as RuleRow;
                if (rr != null)
                {
                    ico = rr.Image;
                    line1 = rr.Pattern;
                    line2 = rr.TargetLabel;
                }
                else
                {
                    line1 = item.ToString();
                }
            }

            int x = e.Bounds.X + 16;
            if (ico != null)
            {
                try
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawImage(ico, new Rectangle(x, e.Bounds.Y + (e.Bounds.Height - 20) / 2, 20, 20));
                }
                catch { }
            }
            x += 30;

            int w = e.Bounds.Right - x - 16;
            TextFormatFlags f = TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;

            using (Font f1 = Theme.Font(9.75f, FontStyle.Regular))
            using (Font f2 = Theme.Font(8.25f, FontStyle.Regular))
            {
                TextRenderer.DrawText(g, line1, f1,
                    new Rectangle(x, e.Bounds.Y + 6, w, 18), Theme.Text, f);
                TextRenderer.DrawText(g, line2, f2,
                    new Rectangle(x, e.Bounds.Y + 24, w, 16), Theme.Dim, f);
            }

            if (AllowReorder)
            {
                // Grip dots hint that the row can be dragged.
                int gx = e.Bounds.Right - 18;
                int gy = e.Bounds.Y + e.Bounds.Height / 2 - 7;
                using (SolidBrush b = new SolidBrush(selected ? Theme.Dim : Theme.Dimmer))
                {
                    for (int row = 0; row < 3; row++)
                        for (int col = 0; col < 2; col++)
                            g.FillEllipse(b, gx + col * 5, gy + row * 6, 2.6f, 2.6f);
                }
            }

            if (_dragging && e.Index == _dropIndex)
            {
                // Insertion line on the edge the row will land against.
                int yline = (_dropIndex >= _dragIndex) ? e.Bounds.Bottom - 2 : e.Bounds.Top + 1;
                using (Pen p = new Pen(Theme.Accent, 2f))
                    g.DrawLine(p, e.Bounds.X + 10, yline, e.Bounds.Right - 10, yline);
            }
        }
    }

    /// <summary>Checkbox drawn to match the dark theme.</summary>
    public class DarkCheck : CheckBox
    {
        public Color Backdrop = Theme.Back;
        bool _hover;

        public DarkCheck()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                   | ControlStyles.OptimizedDoubleBuffer, true);
            FlatStyle = FlatStyle.Flat;
            Cursor = Cursors.Hand;
            Height = 22;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnCheckedChanged(EventArgs e) { Invalidate(); base.OnCheckedChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Backdrop);

            const int box = 15;
            int top = (Height - box) / 2;
            RectangleF r = new RectangleF(0.5f, top + 0.5f, box, box);

            using (GraphicsPath p = Theme.Rounded(r, 4f))
            {
                using (SolidBrush b = new SolidBrush(Checked ? Theme.Accent : Theme.Card))
                    g.FillPath(b, p);
                using (Pen pen = new Pen(Checked ? Theme.Accent : (_hover ? Theme.Dim : Theme.Border), 1f))
                    g.DrawPath(pen, p);
            }

            if (Checked)
            {
                using (Pen tick = new Pen(Theme.OnAccent, 1.9f))
                {
                    tick.StartCap = LineCap.Round;
                    tick.EndCap = LineCap.Round;
                    tick.LineJoin = LineJoin.Round;
                    g.DrawLines(tick, new PointF[] {
                        new PointF(4f,  top + 7.5f),
                        new PointF(6.6f, top + 10.4f),
                        new PointF(11.2f, top + 4.6f)
                    });
                }
            }

            TextRenderer.DrawText(g, Text, Font,
                new Rectangle(box + 8, 0, Width - box - 8, Height),
                _hover ? Theme.Text : Theme.Dim,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }

    public class RuleRow
    {
        public string Pattern;
        public string TargetLabel;
        public Bitmap Image;
    }
}
