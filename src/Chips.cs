using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Picky
{
    /// <summary>
    /// A switch, the Windows 11 way: an outlined track with a small knob when
    /// off, a filled accent track with a white knob when on. With Text it is
    /// the label and the switch together, and the whole width is clickable.
    /// </summary>
    public class ToggleChip : Drawn
    {
        public event EventHandler CheckedChanged;
        bool _checked, _hover, _down;

        public ToggleChip()
        {
            BackColor = Theme.Back;
            Cursor = Cursors.Hand;
            Height = 26;
        }

        public bool Checked
        {
            get { return _checked; }
            set
            {
                if (value == _checked) return;
                _checked = value;
                Invalidate();
                if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
            }
        }

        /// <summary>Set the state without raising CheckedChanged.</summary>
        public void SetQuiet(bool value)
        {
            if (value == _checked) return;
            _checked = value;
            Invalidate();
        }

        public int PreferredWidth()
        {
            if (string.IsNullOrEmpty(Text)) return 40;
            return TextRenderer.MeasureText(Text, Font).Width + 12 + 40;
        }

        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }
        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            bool click = _down && ClientRectangle.Contains(e.Location);
            _down = false;
            if (click) Checked = !Checked;
            else Invalidate();
            base.OnMouseUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            const float tw = 40f, th = 20f;
            float tx = Width - tw - 0.5f, ty = (Height - th) / 2f;

            if (!string.IsNullOrEmpty(Text))
                TextRenderer.DrawText(g, Text, Font,
                    new Rectangle(0, 0, (int)tx - 10, Height),
                    _checked || _hover ? Theme.Text : Theme.Dim,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

            RectangleF track = new RectangleF(tx, ty, tw, th);
            using (GraphicsPath p = Theme.Rounded(track, th / 2f))
            {
                if (_checked)
                    using (SolidBrush b = new SolidBrush(_hover ? Theme.AccentHi : Theme.Accent))
                        g.FillPath(b, p);
                else
                {
                    using (SolidBrush b = new SolidBrush(_hover ? Theme.Hover : Theme.Field))
                        g.FillPath(b, p);
                    using (Pen pen = new Pen(Theme.Dim, 1f)) g.DrawPath(pen, p);
                }
            }

            // The knob grows under the pointer and stretches while pressed.
            float kd = _hover ? 14f : 12f;
            float kw = _down ? kd + 3f : kd;
            float kx = _checked ? tx + tw - (th - kd) / 2f - kw : tx + (th - kd) / 2f;
            RectangleF knob = new RectangleF(kx, ty + (th - kd) / 2f, kw, kd);
            using (GraphicsPath p = Theme.Rounded(knob, kd / 2f))
            using (SolidBrush b = new SolidBrush(_checked ? Color.White : Theme.Text))
                g.FillPath(b, p);
        }
    }

    /// <summary>
    /// Menus in the family style: a dark popover with a hairline edge, rounded
    /// by the desktop manager on Windows 11, and an accent pill under the item
    /// the pointer is on.
    /// </summary>
    public static class Menus
    {
        public static readonly Color Back = ColorTranslator.FromHtml("#232329");

        public static ContextMenuStrip Create(Font font)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Renderer = new DarkMenuRenderer();
            menu.BackColor = Back;
            menu.ForeColor = Theme.Text;
            menu.Font = font;
            menu.ShowImageMargin = false;
            menu.ShowCheckMargin = false;
            menu.Padding = new Padding(0, 4, 0, 4);
            menu.Opened += delegate { Round(menu); };
            return menu;
        }

        public static ToolStripMenuItem Item(string text, Color ink)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text);
            item.ForeColor = ink;
            item.Padding = new Padding(8, 6, 16, 6);
            return item;
        }

        static void Round(ToolStripDropDown menu)
        {
            try
            {
                int round = 3;   // DWMWCP_ROUNDSMALL, what Windows uses for its own menus
                DwmSetWindowAttribute(menu.Handle, 33, ref round, 4);
            }
            catch { }
        }

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    }

    class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkMenuColors()) { RoundedEdges = false; }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            e.Graphics.Clear(Menus.Back);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using (Pen pen = new Pen(Theme.Border, 1f))
                e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected || !e.Item.Enabled) return;
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            RectangleF r = new RectangleF(4, 1, e.Item.Width - 8, e.Item.Height - 2);
            using (GraphicsPath p = Theme.Rounded(r, 5f))
            using (SolidBrush b = new SolidBrush(Theme.Accent))
                g.FillPath(b, p);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            if (e.Item.Selected && e.Item.Enabled) e.TextColor = Color.White;
            else if (!e.Item.Enabled) e.TextColor = Theme.Dimmer;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            int y = e.Item.Height / 2;
            using (Pen pen = new Pen(Theme.Border, 1f))
                e.Graphics.DrawLine(pen, 10, y, e.Item.Width - 10, y);
        }
    }

    /// <summary>Dark menu colours, for whatever the renderer does not draw itself.</summary>
    class DarkMenuColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return Menus.Back; } }
        public override Color MenuBorder { get { return Theme.Border; } }
        public override Color MenuItemBorder { get { return Theme.Accent; } }
        public override Color MenuItemSelected { get { return Theme.Accent; } }
        public override Color MenuItemSelectedGradientBegin { get { return Theme.Accent; } }
        public override Color MenuItemSelectedGradientEnd { get { return Theme.Accent; } }
        public override Color ImageMarginGradientBegin { get { return Menus.Back; } }
        public override Color ImageMarginGradientMiddle { get { return Menus.Back; } }
        public override Color ImageMarginGradientEnd { get { return Menus.Back; } }
        public override Color SeparatorDark { get { return Theme.Border; } }
        public override Color SeparatorLight { get { return Theme.Border; } }
    }

    /// <summary>
    /// A dropdown field. Windows' own ComboBox cannot be styled to match a
    /// dark window - it keeps a light border and a system arrow, and it clips
    /// its text - so this draws the closed state itself and uses a themed
    /// menu for the list.
    /// </summary>
    public class DropChip : Drawn
    {
        public List<string> Items = new List<string>();
        /// <summary>Optional icon for each item, in the same order; drawn in the chip and the list.</summary>
        public List<Image> Images = new List<Image>();
        public event EventHandler SelectionChanged;

        int _index = -1;
        bool _hover;
        ContextMenuStrip _menu;
        int _closedAt;          // tick when the menu last closed

        public DropChip()
        {
            BackColor = Theme.Card;
            Height = 32;
            Cursor = Cursors.Hand;
        }

        public int SelectedIndex
        {
            get { return _index; }
            set
            {
                if (value == _index || value < 0 || value >= Items.Count) return;
                _index = value;
                Invalidate();
                if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
            }
        }

        /// <summary>Set the selection without raising SelectionChanged.</summary>
        public void SetIndexQuiet(int value)
        {
            if (value < 0 || value >= Items.Count) return;
            _index = value;
            Invalidate();
        }

        /// <summary>Empty the list and the selection, ready to be filled again.</summary>
        public void Clear()
        {
            Items.Clear();
            Images.Clear();
            _index = -1;
            Invalidate();
        }

        public string SelectedText
        {
            get { return _index >= 0 && _index < Items.Count ? Items[_index] : ""; }
        }

        bool Open { get { return _menu != null && _menu.Visible; } }

        Image ImageAt(int i)
        {
            return i >= 0 && i < Images.Count ? Images[i] : null;
        }

        bool HasImages
        {
            get
            {
                foreach (Image im in Images) if (im != null) return true;
                return false;
            }
        }

        /// <summary>Width that fits the longest entry, so nothing is clipped.</summary>
        public int PreferredWidth()
        {
            int w = 0;
            foreach (string s in Items)
                w = Math.Max(w, TextRenderer.MeasureText(s, Font).Width);
            return w + 12 + 34 + (HasImages ? 24 : 0);     // text + padding + chevron + icon
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            ShowMenu(this, new Point(0, Height + 4));
        }

        /// <summary>
        /// A click on the chip while the list is open should shut it. The menu
        /// has already closed itself by then (click-away), so without this the
        /// same click immediately reopens it and it never appears to close.
        /// </summary>
        bool JustClosed()
        {
            if (Open) { _menu.Close(); return true; }
            return unchecked(Environment.TickCount - _closedAt) < 250;
        }

        /// <summary>Open the list anywhere.</summary>
        public void ShowMenu(Control anchor, Point at)
        {
            if (Items.Count == 0) return;
            if (JustClosed()) return;

            if (_menu != null) _menu.Dispose();
            _menu = Menus.Create(Font);
            if (anchor == this) _menu.MinimumSize = new Size(Width, 0);
            if (HasImages) _menu.ShowImageMargin = true;

            for (int i = 0; i < Items.Count; i++)
            {
                ToolStripMenuItem item = Menus.Item(Items[i], i == _index ? Theme.AccentHi : Theme.Text);
                item.Image = ImageAt(i);
                int captured = i;
                item.Click += delegate { SelectedIndex = captured; };
                _menu.Items.Add(item);
            }
            _menu.Closed += delegate { _closedAt = Environment.TickCount; Invalidate(); };
            _menu.Show(anchor, at);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (GraphicsPath p = Theme.Rounded(r, 6f))
            {
                using (SolidBrush b = new SolidBrush(Theme.Field)) g.FillPath(b, p);
                Color edge = Open ? Theme.Accent : (_hover ? Theme.BorderHi : Theme.Border);
                using (Pen pen = new Pen(edge, 1f)) g.DrawPath(pen, p);
            }

            int tx = 11;
            Image im = ImageAt(_index);
            if (im != null)
            {
                try
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawImage(im, new Rectangle(10, (Height - 16) / 2, 16, 16));
                }
                catch { }
                tx = 34;
            }

            TextRenderer.DrawText(g, SelectedText, Font,
                new Rectangle(tx, 0, Width - tx - 28, Height), Theme.Text, Theme.Left);

            // The family's chevron: a stroked V, not a filled triangle.
            float cx = Width - 16, cy = Height / 2f;
            using (Pen pen = new Pen(Theme.Dim, 1.4f))
            {
                pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round; pen.LineJoin = LineJoin.Round;
                g.DrawLines(pen, new PointF[] {
                    new PointF(cx - 4, cy - 2), new PointF(cx, cy + 2), new PointF(cx + 4, cy - 2) });
            }
        }
    }

    /// <summary>
    /// A few equal choices side by side with one lit, for a setting whose
    /// steps are worth seeing all at once - the picker's size.
    /// </summary>
    public class Segmented : Drawn
    {
        public readonly List<string> Items = new List<string>();
        public event EventHandler SelectedIndexChanged;

        /// <summary>Width each choice gets from PreferredWidth.</summary>
        public const int SegmentW = 44;
        const int Inset = 2;

        int _index = -1;
        int _hover = -1;

        public Segmented()
        {
            BackColor = Theme.Card;
            Height = 30;
            Cursor = Cursors.Hand;
        }

        public int SelectedIndex
        {
            get { return _index; }
            set
            {
                if (value == _index || value < 0 || value >= Items.Count) return;
                _index = value;
                Invalidate();
                if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty);
            }
        }

        /// <summary>Set the selection without raising SelectedIndexChanged.</summary>
        public void SetIndexQuiet(int value)
        {
            if (value < 0 || value >= Items.Count) return;
            _index = value;
            Invalidate();
        }

        public int PreferredWidth()
        {
            return Items.Count * SegmentW + Inset * 2;
        }

        RectangleF SegmentRect(int i)
        {
            float w = (Width - Inset * 2) / (float)Math.Max(1, Items.Count);
            return new RectangleF(Inset + i * w, Inset, w, Height - Inset * 2 - 0.5f);
        }

        int SegmentAt(Point p)
        {
            for (int i = 0; i < Items.Count; i++)
                if (SegmentRect(i).Contains(p)) return i;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int i = SegmentAt(e.Location);
            if (i != _hover) { _hover = i; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = -1;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            int i = SegmentAt(e.Location);
            if (i >= 0) SelectedIndex = i;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (GraphicsPath p = Theme.Rounded(r, 6f))
            {
                using (SolidBrush b = new SolidBrush(Theme.Field)) g.FillPath(b, p);
                using (Pen pen = new Pen(Theme.Border, 1f)) g.DrawPath(pen, p);
            }

            for (int i = 0; i < Items.Count; i++)
            {
                RectangleF s = SegmentRect(i);
                bool on = i == _index;
                if (on || i == _hover)
                    using (GraphicsPath p = Theme.Rounded(s, 4f))
                    using (SolidBrush b = new SolidBrush(on ? Theme.Accent : Color.FromArgb(20, 255, 255, 255)))
                        g.FillPath(b, p);
                TextRenderer.DrawText(g, Items[i], Font, Rectangle.Round(s),
                    on ? Theme.OnAccent : (i == _hover ? Theme.Text : Theme.Dim), Theme.Center);
            }
        }
    }

    /// <summary>
    /// A one-line text input in the family style: a field-coloured box with a
    /// hairline edge that turns accent while it has the caret, and muted
    /// placeholder text while it is empty. The native edit control does the
    /// typing; this only draws around it.
    /// </summary>
    public class TextField : Drawn
    {
        public readonly TextBox Box;
        readonly HintBox _hintBox;
        bool _hover;

        public TextField()
        {
            // The box before anything that raises a resize, which places it.
            _hintBox = new HintBox();
            Box = _hintBox;

            BackColor = Theme.Card;
            Height = 32;
            Cursor = Cursors.IBeam;

            Box.BorderStyle = BorderStyle.None;
            Box.BackColor = Theme.Field;
            Box.ForeColor = Theme.Text;
            Box.GotFocus += delegate { Invalidate(); };
            Box.LostFocus += delegate { Invalidate(); };
            Box.MouseEnter += delegate { _hover = true; Invalidate(); };
            Box.MouseLeave += delegate
            {
                _hover = ClientRectangle.Contains(PointToClient(Cursor.Position));
                Invalidate();
            };
            Controls.Add(Box);
        }

        /// <summary>Muted text shown while the field is empty.</summary>
        public string Placeholder
        {
            get { return _hintBox.Placeholder; }
            set { _hintBox.Placeholder = value ?? ""; Box.Invalidate(); }
        }

        void PlaceBox()
        {
            if (Box == null) return;
            Box.SetBounds(10, (Height - Box.Height) / 2, Math.Max(1, Width - 20), Box.Height);
        }

        protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); Box.Font = Font; PlaceBox(); }
        protected override void OnResize(EventArgs e) { base.OnResize(e); PlaceBox(); }
        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = ClientRectangle.Contains(PointToClient(Cursor.Position));
            Invalidate();
            base.OnMouseLeave(e);
        }

        /// <summary>The padding round the edit control is part of the field.</summary>
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Box.Focus();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);
            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (GraphicsPath p = Theme.Rounded(r, 6f))
            {
                using (SolidBrush b = new SolidBrush(Theme.Field)) g.FillPath(b, p);
                Color edge = Box.Focused ? Theme.Accent : (_hover ? Theme.BorderHi : Theme.Border);
                using (Pen pen = new Pen(edge, 1f)) g.DrawPath(pen, p);
            }
        }

        /// <summary>
        /// The edit control paints its own text, so the placeholder is drawn
        /// straight after it, at the same margin the typed text starts from.
        /// </summary>
        class HintBox : TextBox
        {
            public string Placeholder = "";

            const int WM_PAINT = 0x0F;
            const int EM_GETMARGINS = 0xD4;

            [DllImport("user32.dll")]
            static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

            protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

            protected override void WndProc(ref Message m)
            {
                base.WndProc(ref m);
                if (m.Msg != WM_PAINT || TextLength > 0 || Placeholder.Length == 0) return;

                int left = (int)SendMessage(Handle, EM_GETMARGINS, IntPtr.Zero, IntPtr.Zero) & 0xFFFF;
                using (Graphics g = Graphics.FromHwnd(Handle))
                    TextRenderer.DrawText(g, Placeholder, Font,
                        new Rectangle(left, 0, Math.Max(1, ClientSize.Width - left), ClientSize.Height),
                        Theme.Dimmer, TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPadding
                        | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            }
        }
    }
}
