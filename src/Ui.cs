using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Picky
{
    public static class Theme
    {
        // The same surface ladder as Keycap, Shotwin and Playbar, darkest to
        // lightest, so the apps read as one family: rail, page, raised card,
        // field, chip. Three ink levels only - primary, secondary, tertiary.
        public static readonly Color Rail = ColorTranslator.FromHtml("#161619");
        public static readonly Color Back = ColorTranslator.FromHtml("#1B1B1F");
        public static readonly Color Card = ColorTranslator.FromHtml("#212126");
        public static readonly Color Field = ColorTranslator.FromHtml("#26262B");
        public static readonly Color CardHi = ColorTranslator.FromHtml("#2E2E35");
        public static readonly Color Hover = ColorTranslator.FromHtml("#3A3A44");
        public static readonly Color NavOn = ColorTranslator.FromHtml("#2E2E38");
        public static readonly Color NavHover = ColorTranslator.FromHtml("#222228");
        public static readonly Color Border = ColorTranslator.FromHtml("#31313A");
        public static readonly Color BorderHi = ColorTranslator.FromHtml("#4A4A56");
        public static readonly Color Text = ColorTranslator.FromHtml("#E7E7EA");
        public static readonly Color Dim = ColorTranslator.FromHtml("#8A8A93");
        public static readonly Color Dimmer = ColorTranslator.FromHtml("#62626C");
        public static readonly Color Accent = ColorTranslator.FromHtml("#3D8BFD");
        public static readonly Color AccentHi = ColorTranslator.FromHtml("#5AA0FF");
        public static readonly Color AccentDark = ColorTranslator.FromHtml("#2F66C0");
        public static readonly Color AccentSoft = ColorTranslator.FromHtml("#1E3255");
        public static readonly Color OnAccent = Color.White;
        public static readonly Color Good = ColorTranslator.FromHtml("#4CC38A");
        public static readonly Color Warn = ColorTranslator.FromHtml("#FF9F0A");
        public static readonly Color Bad = ColorTranslator.FromHtml("#FF6B5E");
        public static readonly Color CloseRed = ColorTranslator.FromHtml("#E81123");

        // The picker popover floats over whatever app the link came from, so
        // it is the raised surface with a white hairline edge that holds it
        // against any backdrop. Rows light up with washes laid over that
        // surface: white for the pointer, accent for the selection.
        public static readonly Color Popover = Card;
        public static readonly Color PopoverEdge = Blend(Card, Color.White, 0.18f);
        public static readonly Color Line = Border;
        public static readonly Color HoverWash = Color.FromArgb(20, 255, 255, 255);   // white 8%
        public static readonly Color AccentWash = Color.FromArgb(48, Accent);           // accent 19%

        /// <summary>Body text. The Variable "Text" cut is drawn for these sizes.</summary>
        public static Font Font(float size, FontStyle style)
        {
            return Pick(new string[] { "Segoe UI Variable Text", "Segoe UI" },
                        size, style, FontFamily.GenericSansSerif);
        }

        /// <summary>
        /// Semibold, the weight every heading in the family uses. GDI has no
        /// semibold style bit, so it is its own family name.
        /// </summary>
        public static Font Semi(float size)
        {
            return Pick(new string[] { "Segoe UI Variable Text Semibold", "Segoe UI Semibold" },
                        size, FontStyle.Regular, FontFamily.GenericSansSerif);
        }

        /// <summary>
        /// Windows' own icon font - the glyphs its title bars are drawn with.
        /// Segoe Fluent Icons on 11, the same code points in MDL2 on 10.
        /// </summary>
        public static Font Icons(float size)
        {
            return Pick(new string[] { "Segoe Fluent Icons", "Segoe MDL2 Assets" },
                        size, FontStyle.Regular, FontFamily.GenericSansSerif);
        }

        static Font Pick(string[] prefs, float size, FontStyle style, FontFamily fallback)
        {
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
            return new System.Drawing.Font(fallback, size, style);
        }

        /// <summary>Mix two colours; t = 0 is a, t = 1 is b.</summary>
        public static Color Blend(Color a, Color b, float t)
        {
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        [System.Runtime.InteropServices.DllImport("uxtheme.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        static extern int SetWindowTheme(IntPtr hwnd, string app, string idList);

        /// <summary>
        /// Ask the desktop manager for a dark frame, rounded corners and a
        /// border that matches the page. Without this a dark window still gets
        /// a white title bar and a pale outline, which is the single thing that
        /// makes a themed app look unfinished.
        /// </summary>
        public static void DarkTitleBar(Form form)
        {
            try
            {
                int on = 1;
                // 20 on current builds, 19 on early Windows 10 - try both.
                if (DwmSetWindowAttribute(form.Handle, 20, ref on, 4) != 0)
                    DwmSetWindowAttribute(form.Handle, 19, ref on, 4);

                // Windows 11 only; older builds reject these and nothing changes.
                int round = 2;                                  // DWMWCP_ROUND
                DwmSetWindowAttribute(form.Handle, 33, ref round, 4);
                int border = ColorTranslator.ToWin32(Border);   // COLORREF
                DwmSetWindowAttribute(form.Handle, 34, ref border, 4);
            }
            catch { }
        }

        /// <summary>
        /// Dark scrollbars and edit chrome for a native control. The only way to
        /// theme a Win32 scrollbar at all; a no-op before Windows 10 1809.
        /// </summary>
        public static void DarkControl(Control c)
        {
            try { SetWindowTheme(c.Handle, "DarkMode_Explorer", null); }
            catch { }
        }

        /// <summary>
        /// A tooltip that matches the window. The stock one is a pale yellow
        /// box with dark text - invisible against everything else here.
        /// </summary>
        public static ToolTip Tip()
        {
            ToolTip t = new ToolTip();
            t.OwnerDraw = true;
            Font f = Font(8.75f, FontStyle.Regular);
            t.Popup += delegate(object s, PopupEventArgs e)
            {
                Size sz = TextRenderer.MeasureText(t.GetToolTip(e.AssociatedControl), f);
                e.ToolTipSize = new Size(sz.Width + 20, sz.Height + 12);
            };
            t.Draw += delegate(object s, DrawToolTipEventArgs e)
            {
                e.Graphics.Clear(ColorTranslator.FromHtml("#2A2A31"));
                using (Pen pen = new Pen(ColorTranslator.FromHtml("#3C3C47")))
                    e.Graphics.DrawRectangle(pen, 0, 0, e.Bounds.Width - 1, e.Bounds.Height - 1);
                TextRenderer.DrawText(e.Graphics, e.ToolTipText, f, e.Bounds, Text, Center);
            };
            return t;
        }

        public static GraphicsPath Rounded(RectangleF r, float radius)
        {
            GraphicsPath p = new GraphicsPath();
            float d = radius * 2;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            if (d <= 0) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public const TextFormatFlags Left = TextFormatFlags.Left | TextFormatFlags.VerticalCenter
            | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis;
        public const TextFormatFlags Center = TextFormatFlags.HorizontalCenter
            | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix;
        public const TextFormatFlags Wrap = TextFormatFlags.Left | TextFormatFlags.Top
            | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix;
    }

    /// <summary>Base for every owner-drawn control: double buffered, no flicker.</summary>
    public class Drawn : Control
    {
        public Drawn()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                   | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }
    }

    /// <summary>Rounded surface used to group controls.</summary>
    public class Card : Panel
    {
        public int Radius = 10;
        public bool Outline = false;

        Color _fill = Theme.Card;

        /// <summary>
        /// The card's surface. Setting it also sets BackColor, so controls
        /// placed on the card clear to the card rather than to the page - that
        /// mismatch is what made the background bleed through their corners.
        /// </summary>
        public Color Fill
        {
            get { return _fill; }
            set { _fill = value; BackColor = value; Invalidate(); }
        }

        /// <summary>What sits behind this card - shows at the rounded corners.</summary>
        public Color Page = Theme.Back;

        public Card()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                   | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = _fill;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            // Clear to the page, so the rounded corners blend with what is
            // behind the card rather than showing a square of card colour.
            g.Clear(Page);
            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (GraphicsPath p = Theme.Rounded(r, Radius))
            {
                using (SolidBrush b = new SolidBrush(Fill)) g.FillPath(b, p);
                if (Outline)
                    using (Pen pen = new Pen(Theme.Border, 1f)) g.DrawPath(pen, p);
            }
            base.OnPaint(e);
        }
    }

    /// <summary>
    /// Settings rows grouped on one card and divided by hairlines: what the
    /// setting is, what it does in a sentence under it, and its control at the
    /// right. A one-word label is never left to carry the meaning alone.
    /// </summary>
    public class RowCard : Card
    {
        class Row { public string Title, Detail; public Control Action; public int Top, Height, TextW; }
        readonly System.Collections.Generic.List<Row> _rows = new System.Collections.Generic.List<Row>();
        const int PadX = 18, PadY = 13;
        readonly Font _title = Theme.Font(9.75f, FontStyle.Regular);
        readonly Font _detail = Theme.Font(8.75f, FontStyle.Regular);

        /// <summary>Add a row. The action is optional, and keeps the size it has.</summary>
        public void Add(string title, string detail, Control action)
        {
            Row r = new Row();
            r.Title = title;
            r.Detail = detail ?? "";
            r.Action = action;
            if (action != null)
            {
                action.BackColor = Fill;
                Controls.Add(action);
            }
            _rows.Add(r);
        }

        /// <summary>Change a row's text after the fact - a status that moves on.</summary>
        public void SetText(int row, string title, string detail)
        {
            _rows[row].Title = title;
            _rows[row].Detail = detail ?? "";
            if (Width > 0) Arrange(Width);
            Invalidate();
        }

        /// <summary>Lay the rows out at this width, and return the height the card needs.</summary>
        public int Arrange(int width)
        {
            Width = width;
            int y = 0;
            foreach (Row r in _rows)
            {
                // Not Action.Visible: that reads false while the page is hidden,
                // which laid the text out under the button.
                int aw = r.Action != null ? r.Action.Width : 0;
                r.TextW = Math.Max(80, width - PadX * 2 - (aw > 0 ? aw + 28 : 0));
                int th = TextRenderer.MeasureText("Ag", _title).Height;
                int dh = r.Detail.Length == 0 ? 0 : TextRenderer.MeasureText(r.Detail, _detail,
                    new Size(r.TextW, 10000), Theme.Wrap).Height;
                int h = PadY * 2 + th + (dh > 0 ? 2 + dh : 0);
                if (r.Action != null) h = Math.Max(h, r.Action.Height + PadY * 2);
                r.Top = y;
                r.Height = h;
                if (r.Action != null)
                    r.Action.Location = new Point(width - PadX - r.Action.Width, y + (h - r.Action.Height) / 2);
                y += h;
            }
            Height = y;
            Invalidate();
            return y;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            int th = TextRenderer.MeasureText("Ag", _title).Height;
            for (int i = 0; i < _rows.Count; i++)
            {
                Row r = _rows[i];
                if (i > 0)
                    using (SolidBrush b = new SolidBrush(Theme.Border))
                        g.FillRectangle(b, PadX, r.Top, Width - PadX * 2, 1);

                int dh = r.Detail.Length == 0 ? 0 : TextRenderer.MeasureText(r.Detail, _detail,
                    new Size(r.TextW, 10000), Theme.Wrap).Height;
                int block = th + (dh > 0 ? 2 + dh : 0);
                int y = r.Top + (r.Height - block) / 2;
                TextRenderer.DrawText(g, r.Title, _title, new Rectangle(PadX, y, r.TextW, th),
                    Theme.Text, Theme.Left);
                if (dh > 0)
                    TextRenderer.DrawText(g, r.Detail, _detail, new Rectangle(PadX, y + th + 2, r.TextW, dh),
                        Theme.Dim, Theme.Wrap);
            }
        }
    }

    /// <summary>
    /// The family's buttons. A chip by default - a flat, borderless fill -
    /// Primary for the one action a view is for, Ghost for a text link.
    /// </summary>
    public class FlatButton : Button
    {
        public bool Primary = false;
        public bool Ghost = false;
        public int Radius = 7;
        public Color Backdrop = Theme.Back;
        /// <summary>A shortcut drawn in its own translucent key, after the label.</summary>
        public string Hint = "";
        bool _hover, _down;

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

        /// <summary>Width that fits the label with the family's padding.</summary>
        public int PreferredWidth()
        {
            // A link is its text alone, so it lines up with the text above it.
            if (Ghost) return TextRenderer.MeasureText(Text, Font).Width + 4;
            int w = TextRenderer.MeasureText(Text, Font).Width + 30;
            if (Hint.Length > 0)
                using (Font f = Theme.Semi(Font.Size * 0.86f))
                    w += TextRenderer.MeasureText(Hint, f).Width + 16;
            return Math.Max(Ghost ? 0 : 72, w);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override bool ShowFocusCues { get { return false; } }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Backdrop);

            if (Ghost)
            {
                Color ink = !Enabled ? Theme.Dimmer : (_hover ? Theme.AccentHi : Theme.Accent);
                TextRenderer.DrawText(g, Text, Font, ClientRectangle, ink, Theme.Left);
                return;
            }

            Color fill, fg;
            if (Primary)
            {
                fill = _down ? Theme.AccentDark : (_hover ? Theme.AccentHi : Theme.Accent);
                fg = Theme.OnAccent;
            }
            else
            {
                fill = _down ? Theme.Border : (_hover ? Theme.Hover : Theme.CardHi);
                fg = Theme.Text;
            }
            if (!Enabled)
            {
                // The family greys a disabled control by fading it into what is
                // behind it, not by swapping in a separate colour.
                fill = Theme.Blend(Backdrop, fill, 0.45f);
                fg = Theme.Blend(Backdrop, fg, 0.4f);
            }

            RectangleF r = new RectangleF(0, 0, Width - 0.5f, Height - 0.5f);
            using (GraphicsPath p = Theme.Rounded(r, Radius))
            using (SolidBrush b = new SolidBrush(fill))
                g.FillPath(b, p);

            if (Hint.Length == 0)
            {
                TextRenderer.DrawText(g, Text, Font, ClientRectangle, fg, Theme.Center);
                return;
            }

            // Label and key centred together as one group.
            using (Font fk = Theme.Semi(Font.Size * 0.86f))
            {
                Size ts = TextRenderer.MeasureText(Text, Font);
                Size ks = TextRenderer.MeasureText(Hint, fk);
                int kw = ks.Width + 8, gap = 8;
                int x = (Width - ts.Width - gap - kw) / 2;
                TextRenderer.DrawText(g, Text, Font, new Rectangle(x, 0, ts.Width, Height), fg, Theme.Center);
                RectangleF kr = new RectangleF(x + ts.Width + gap, (Height - 20) / 2f, kw, 20);
                using (GraphicsPath p = Theme.Rounded(kr, 4f))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(Enabled ? 0x2B : 0x14, 255, 255, 255)))
                    g.FillPath(b, p);
                TextRenderer.DrawText(g, Hint, fk, Rectangle.Round(kr),
                    Color.FromArgb(Enabled ? 0xD8 : 0x60, fg), Theme.Center);
            }
        }
    }

    /// <summary>
    /// A label that draws itself, so it takes its background from the parent
    /// instead of painting a rectangle of its own colour.
    /// </summary>
    public class TextLine : Drawn
    {
        public Color Ink = Theme.Text;
        public bool Wrap = false;
        public bool AlignRight = false;

        public TextLine()
        {
            BackColor = Theme.Back;
            Height = 18;
        }

        /// <summary>Height the text needs at the current width.</summary>
        public int Measure()
        {
            if (!Wrap) return TextRenderer.MeasureText(Text, Font).Height;
            return TextRenderer.MeasureText(Text, Font, new Size(Math.Max(1, Width), 10000),
                Theme.Wrap).Height;
        }

        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            TextFormatFlags flags = Wrap ? Theme.Wrap : Theme.Left;
            if (AlignRight) flags = (flags & ~TextFormatFlags.Left) | TextFormatFlags.Right;
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, Ink, flags);
        }
    }

    /// <summary>
    /// A vertically scrolling area with the family's thin overlay bar: no
    /// arrows, no track, a 6px thumb riding the edge, and only while there is
    /// more to see. Children go in Content, whose height the owner sets.
    /// </summary>
    public class ScrollPage : Panel
    {
        public readonly Panel Content;
        readonly ScrollThumb _thumb;
        int _offset;

        public ScrollPage()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                   | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = Theme.Back;

            Content = new BufferedPanel();
            Content.BackColor = Theme.Back;
            Controls.Add(Content);

            _thumb = new ScrollThumb(this);
            Controls.Add(_thumb);
            _thumb.BringToFront();
        }

        public int Offset { get { return _offset; } }
        public int MaxOffset { get { return Math.Max(0, Content.Height - Height); } }

        public void ScrollTo(int y)
        {
            int next = Math.Max(0, Math.Min(MaxOffset, y));
            _offset = next;
            Content.Top = -next;
            _thumb.Visible = MaxOffset > 0;
            _thumb.Invalidate();
        }

        /// <summary>Call after changing Content's height.</summary>
        public void Sync()
        {
            Content.Width = Width;
            ScrollTo(_offset);
            _thumb.SetBounds(Width - 10, 0, 10, Height);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Sync();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            // Three lines at a time, the way the system scrolls a list.
            ScrollTo(_offset - Math.Sign(e.Delta) * 60);
            // Wheel messages land on whatever is under the pointer - usually a
            // row deep inside. What a child leaves unhandled travels up to its
            // parent, which is how it arrives here.
            HandledMouseEventArgs h = e as HandledMouseEventArgs;
            if (h != null) h.Handled = true;
        }

        class BufferedPanel : Panel
        {
            public BufferedPanel()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                       | ControlStyles.OptimizedDoubleBuffer, true);
            }
        }

        class ScrollThumb : Drawn
        {
            readonly ScrollPage _owner;
            bool _hover, _drag;
            int _grabY, _grabOffset;

            public ScrollThumb(ScrollPage owner)
            {
                _owner = owner;
                BackColor = Theme.Back;
                Visible = false;
            }

            RectangleF Thumb()
            {
                float track = Height - 8;
                int content = Math.Max(1, _owner.Content.Height);
                float h = Math.Max(32f, track * _owner.Height / content);
                float y = 4 + (_owner.MaxOffset == 0 ? 0 : (track - h) * _owner.Offset / _owner.MaxOffset);
                return new RectangleF(Width - 8, y, 6, h);
            }

            protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                RectangleF t = Thumb();
                if (e.Y >= t.Top && e.Y <= t.Bottom)
                {
                    _drag = true; _grabY = e.Y; _grabOffset = _owner.Offset;
                    Capture = true;
                }
                else
                {
                    // A click on the empty track pages towards it.
                    _owner.ScrollTo(_owner.Offset + (e.Y < t.Top ? -1 : 1) * _owner.Height * 9 / 10);
                }
                base.OnMouseDown(e);
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                if (_drag)
                {
                    RectangleF t = Thumb();
                    float track = Height - 8 - t.Height;
                    if (track > 0)
                        _owner.ScrollTo(_grabOffset + (int)((e.Y - _grabY) * _owner.MaxOffset / track));
                }
                base.OnMouseMove(e);
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                _drag = false; Capture = false; Invalidate();
                base.OnMouseUp(e);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(BackColor);
                if (_owner.MaxOffset == 0) return;
                using (GraphicsPath p = Theme.Rounded(Thumb(), 3f))
                using (SolidBrush b = new SolidBrush(_hover || _drag
                           ? ColorTranslator.FromHtml("#5C5C69")
                           : ColorTranslator.FromHtml("#43434E")))
                    g.FillPath(b, p);
            }
        }
    }

    /// <summary>
    /// A confirmation that floats over the bottom of a page for a few seconds,
    /// rather than holding a row of its own that leaves a gap when it goes.
    /// </summary>
    public class Toast : Drawn
    {
        public Color Ink = Theme.Dim;
        readonly Timer _timer = new Timer();

        public Toast()
        {
            BackColor = Theme.Back;
            Font = Theme.Font(8.75f, FontStyle.Regular);
            Visible = false;
            Height = 32;
            _timer.Tick += delegate { _timer.Stop(); Visible = false; };
        }

        public void Say(string text, Color ink)
        {
            Text = text;
            Ink = ink;
            Width = TextRenderer.MeasureText(text, Font).Width + 34;
            if (Parent != null)
                Left = CenterLeft + (Parent.ClientSize.Width - CenterLeft - Width) / 2;
            Visible = true;
            BringToFront();
            Invalidate();
            _timer.Stop();
            _timer.Interval = Math.Max(2600, 900 + text.Length * 45);
            _timer.Start();
        }

        /// <summary>Where the area it centres in starts - past the rail, in the window.</summary>
        public int CenterLeft;

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);
            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (GraphicsPath p = Theme.Rounded(r, 6f))
            {
                using (SolidBrush b = new SolidBrush(Theme.Card)) g.FillPath(b, p);
                using (Pen pen = new Pen(Theme.Border, 1f)) g.DrawPath(pen, p);
            }
            float d = 6;
            using (SolidBrush b = new SolidBrush(Ink))
                g.FillEllipse(b, 13, (Height - d) / 2f, d, d);
            TextRenderer.DrawText(g, Text, Font, new Rectangle(25, 0, Width - 31, Height),
                Ink == Theme.Dim ? Theme.Dim : Theme.Text, Theme.Left);
        }
    }

    /// <summary>
    /// Owner-drawn list of rules or targets on the card it sits on: an icon, a
    /// label and muted secondary text. The owner sizes it to show every row,
    /// so it never needs a scrollbar of its own; the page scrolls instead.
    /// </summary>
    public class RowList : ListBox
    {
        /// <summary>Enables press-and-drag row reordering.</summary>
        public bool AllowReorder = false;
        public event EventHandler Reordered;

        /// <summary>Draws a remove button on the row under the pointer.</summary>
        public bool ShowRemove = false;
        /// <summary>Raised with the row's index when its remove button is clicked.</summary>
        public event Action<int> RemoveClicked;

        int _pressIndex = -1;
        int _dragIndex = -1;
        int _dropIndex = -1;
        Point _pressPt;
        bool _dragging;

        int _hover = -1;
        bool _hoverX;
        int _pressX = -1;

        readonly Font _f1 = Theme.Font(9.75f, FontStyle.Regular);
        readonly Font _f2 = Theme.Font(8.75f, FontStyle.Regular);

        const int WM_ERASEBKGND = 0x14;
        const int WM_MOUSEWHEEL = 0x20A;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

        public RowList()
        {
            DrawMode = DrawMode.OwnerDrawFixed;
            BorderStyle = BorderStyle.None;
            BackColor = Theme.Card;
            ForeColor = Theme.Text;
            IntegralHeight = false;
            Font = Theme.Font(9.75f, FontStyle.Regular);
            ItemHeight = 44;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.DarkControl(this);
        }

        protected override void WndProc(ref Message m)
        {
            // Sized to its rows, so the wheel belongs to the page around it.
            if (m.Msg == WM_MOUSEWHEEL && Parent != null)
            {
                SendMessage(Parent.Handle, m.Msg, m.WParam, m.LParam);
                return;
            }
            // The rows cover the whole control; erasing first only makes a
            // row flash every time the pointer crosses it.
            if (m.Msg == WM_ERASEBKGND && Items.Count * ItemHeight >= ClientSize.Height)
            {
                m.Result = (IntPtr)1;
                return;
            }
            base.WndProc(ref m);
        }

        /// <summary>The row under a point, or -1 where there is none.</summary>
        int RowAt(Point p)
        {
            int i = IndexFromPoint(p);
            if (i < 0 || i >= Items.Count || !GetItemRectangle(i).Contains(p)) return -1;
            return i;
        }

        Rectangle RemoveRect(int i)
        {
            Rectangle b = GetItemRectangle(i);
            return new Rectangle(b.Right - 36, b.Y + (b.Height - 26) / 2, 26, 26);
        }

        void InvalidateRow(int i)
        {
            if (i >= 0 && i < Items.Count) Invalidate(GetItemRectangle(i));
        }

        void TrackHover(Point p)
        {
            int i = RowAt(p);
            bool onX = ShowRemove && i >= 0 && RemoveRect(i).Contains(p);
            if (i == _hover && onX == _hoverX) return;
            InvalidateRow(_hover);
            _hover = i;
            _hoverX = onX;
            InvalidateRow(_hover);
            if (!_dragging) Cursor = onX ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;

            int i = RowAt(e.Location);
            if (ShowRemove && i >= 0 && RemoveRect(i).Contains(e.Location))
            {
                // A press on the remove button is never the start of a drag.
                _pressX = i;
                return;
            }

            if (!AllowReorder) return;
            _pressIndex = IndexFromPoint(e.Location);
            _pressPt = e.Location;
            _dragging = false;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            TrackHover(e.Location);
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

            if (_pressX >= 0)
            {
                int i = _pressX;
                _pressX = -1;
                if (i < Items.Count && RemoveRect(i).Contains(e.Location) && RemoveClicked != null)
                    RemoveClicked(i);
                return;
            }

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
            if (_dragging) return;
            Cursor = Cursors.Default;
            InvalidateRow(_hover);
            _hover = -1;
            _hoverX = false;
        }

        /// <summary>Where the target column starts: after the widest pattern, at most half way.</summary>
        int PatternColumn(int x, int right)
        {
            int widest = 0;
            foreach (object o in Items)
            {
                RuleRow rr = o as RuleRow;
                if (rr != null && rr.Pattern != null)
                    widest = Math.Max(widest, TextRenderer.MeasureText(rr.Pattern, _f1).Width);
            }
            return x + Math.Min(widest + 28, Math.Max(80, (right - x) / 2));
        }

        static void DrawIcon(Graphics g, Image img, Rectangle r)
        {
            if (img == null) return;
            try
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(img, r);
            }
            catch { }
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= Items.Count) return;

            Graphics g = e.Graphics;
            Rectangle b = e.Bounds;

            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            bool hovered = e.Index == _hover && !_dragging;

            // Filled before antialiasing is on: antialiased, a square fill only
            // half covers its edge pixels, and the list is never erased under
            // it, so every row showed a hairline along its top and left.
            using (SolidBrush bg = new SolidBrush(BackColor))
                g.FillRectangle(bg, b);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (selected || hovered)
            {
                RectangleF r = new RectangleF(b.X, b.Y + 1, b.Width - 0.5f, b.Height - 2);
                using (GraphicsPath p = Theme.Rounded(r, 6f))
                {
                    if (selected)
                        using (SolidBrush fill = new SolidBrush(Theme.AccentSoft)) g.FillPath(fill, p);
                    if (hovered)
                        using (SolidBrush fill = new SolidBrush(Color.FromArgb(20, 255, 255, 255))) g.FillPath(fill, p);
                }
            }

            int x = b.X + 12;
            if (AllowReorder)
            {
                // Grip dots say the row can be dragged.
                using (SolidBrush gb = new SolidBrush(selected || hovered ? Theme.Dim : Theme.Dimmer))
                {
                    for (int row = 0; row < 3; row++)
                        for (int col = 0; col < 2; col++)
                            g.FillEllipse(gb, x + col * 5, b.Y + b.Height / 2 - 6 + row * 5, 2.4f, 2.4f);
                }
                x += 20;
            }
            int right = b.Right - 14 - (ShowRemove ? 30 : 0);

            object item = Items[e.Index];
            RuleRow rr = item as RuleRow;
            Target t = item as Target;
            if (rr != null)
            {
                // One line: the pattern, then where it goes in muted text.
                int col = PatternColumn(x, right);
                TextRenderer.DrawText(g, rr.Pattern, _f1, new Rectangle(x, b.Y, col - x - 12, b.Height),
                    Theme.Text, Theme.Left);
                int tx = col;
                if (rr.Image != null)
                {
                    DrawIcon(g, rr.Image, new Rectangle(tx, b.Y + (b.Height - 16) / 2, 16, 16));
                    tx += 24;
                }
                TextRenderer.DrawText(g, rr.TargetLabel, _f2, new Rectangle(tx, b.Y, Math.Max(1, right - tx), b.Height),
                    rr.Image != null ? Theme.Dim : Theme.Dimmer, Theme.Left);
            }
            else if (t != null)
            {
                DrawIcon(g, t.Image, new Rectangle(x, b.Y + (b.Height - 20) / 2, 20, 20));
                x += 30;
                // A single-profile browser goes by its own name, said once.
                bool single = string.Equals(t.ProfileLabel, t.BrowserName, StringComparison.OrdinalIgnoreCase);
                string line2 = single ? "" : t.BrowserName;
                if (!string.IsNullOrEmpty(t.Subtitle)) line2 += (line2.Length > 0 ? "  ·  " : "") + t.Subtitle;
                if (line2.Length == 0)
                {
                    TextRenderer.DrawText(g, t.ProfileLabel, _f1, new Rectangle(x, b.Y, right - x, b.Height),
                        Theme.Text, Theme.Left);
                }
                else
                {
                    TextRenderer.DrawText(g, t.ProfileLabel, _f1, new Rectangle(x, b.Y + 5, right - x, 18),
                        Theme.Text, Theme.Left);
                    TextRenderer.DrawText(g, line2, _f2, new Rectangle(x, b.Y + 23, right - x, 16),
                        Theme.Dim, Theme.Left);
                }
            }
            else
            {
                TextRenderer.DrawText(g, item.ToString(), _f1, new Rectangle(x, b.Y, right - x, b.Height),
                    Theme.Text, Theme.Left);
            }

            if (ShowRemove && hovered)
            {
                Rectangle xr = RemoveRect(e.Index);
                float cx = xr.X + xr.Width / 2f, cy = xr.Y + xr.Height / 2f, k = 4.5f;
                using (Pen pen = new Pen(_hoverX ? Theme.Bad : Theme.Dim, 1.5f))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    g.DrawLine(pen, cx - k, cy - k, cx + k, cy + k);
                    g.DrawLine(pen, cx - k, cy + k, cx + k, cy - k);
                }
            }

            if (_dragging && e.Index == _dropIndex)
            {
                // Insertion line on the edge the row will land against.
                int yline = (_dropIndex >= _dragIndex) ? b.Bottom - 2 : b.Top + 1;
                using (Pen p = new Pen(Theme.Accent, 2f))
                    g.DrawLine(p, b.X + 10, yline, b.Right - 10, yline);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _f1.Dispose();
                _f2.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    public class RuleRow
    {
        public Rule Rule;
        public string Pattern;
        public string TargetLabel;
        public Bitmap Image;
    }
}
