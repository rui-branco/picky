using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Picky
{
    public class PickerForm : Form
    {
        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("gdi32.dll")]
        static extern IntPtr CreateRoundRectRgn(int l, int t, int r, int b, int w, int h);

        [DllImport("gdi32.dll")]
        static extern bool DeleteObject(IntPtr hObject);

        const int CS_DROPSHADOW = 0x00020000;

        readonly List<Target> _targets;
        readonly string _url;
        int _sel;
        int _hover = -1;
        bool _launched;
        bool _everActivated;

        public static bool AutoClose = true;

        const int Corner = 12;
        const int RowH = 52;
        const int HeaderH = 46;
        const int PadX = 14;
        const int IconSize = 24;
        const int ChipW = 20;
        const int RowInset = 7;

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

        Font _fLabel;
        Font _fSub;
        Font _fHost;
        Font _fChip;

        public PickerForm(List<Target> targets, string url)
        {
            _targets = targets;
            _url = url;
            _sel = 0;

            _fLabel = MakeFont(10.5f, FontStyle.Regular);
            _fSub = MakeFont(8.25f, FontStyle.Regular);
            _fHost = MakeFont(9.75f, FontStyle.Regular);
            _fChip = MakeFont(8f, FontStyle.Regular);

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = CBack;
            DoubleBuffered = true;
            KeyPreview = true;
            Text = "Picky";

            Width = 420;
            Height = HeaderH + _targets.Count * RowH + 8;

            PositionAtCursor();
        }

        static Font MakeFont(float size, FontStyle style)
        {
            // Segoe UI Variable is the Windows 11 face; fall back cleanly on 10.
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
            return new Font(FontFamily.GenericSansSerif, size, style);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= CS_DROPSHADOW;
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyRoundedRegion();
        }

        void ApplyRoundedRegion()
        {
            IntPtr rgn = CreateRoundRectRgn(0, 0, Width + 1, Height + 1, Corner * 2, Corner * 2);
            if (rgn == IntPtr.Zero) return;
            try { Region = Region.FromHrgn(rgn); }
            finally { DeleteObject(rgn); }
        }

        void PositionAtCursor()
        {
            Point c = Cursor.Position;
            Rectangle wa = Screen.FromPoint(c).WorkingArea;

            int x = c.X - 24;
            int y = c.Y - 14;

            if (x + Width > wa.Right) x = wa.Right - Width;
            if (y + Height > wa.Bottom) y = wa.Bottom - Height;
            if (x < wa.Left) x = wa.Left;
            if (y < wa.Top) y = wa.Top;

            Location = new Point(x, y);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            // A protocol-launched process does not reliably get the foreground,
            // which would swallow every keystroke. Claim it explicitly.
            Activate();
            BringToFront();
            try { SetForegroundWindow(Handle); }
            catch { }
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            _everActivated = true;
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            // Deactivate also fires at startup when the window never won the
            // foreground. Closing on that would make the picker vanish before
            // it could be seen, so only dismiss once it has really been focused.
            if (_everActivated && AutoClose) Close();
        }

        Rectangle RowRect(int i)
        {
            return new Rectangle(0, HeaderH + i * RowH, Width, RowH);
        }

        int IndexAt(Point p)
        {
            if (p.Y < HeaderH) return -1;
            int i = (p.Y - HeaderH) / RowH;
            if (i < 0 || i >= _targets.Count) return -1;
            return i;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int i = IndexAt(e.Location);
            if (i != _hover)
            {
                _hover = i;
                if (i >= 0) _sel = i;
                Invalidate();
            }
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
            int i = IndexAt(e.Location);
            if (i >= 0) LaunchIndex(i);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.KeyCode == Keys.Escape) { Close(); return; }

            if (e.KeyCode == Keys.Down)
            {
                _sel = (_sel + 1) % _targets.Count;
                Invalidate(); e.Handled = true; return;
            }
            if (e.KeyCode == Keys.Up)
            {
                _sel = (_sel - 1 + _targets.Count) % _targets.Count;
                Invalidate(); e.Handled = true; return;
            }
            if (e.KeyCode == Keys.Enter) { LaunchIndex(_sel); return; }

            int digit = -1;
            if (e.KeyCode >= Keys.D1 && e.KeyCode <= Keys.D9) digit = e.KeyCode - Keys.D1;
            else if (e.KeyCode >= Keys.NumPad1 && e.KeyCode <= Keys.NumPad9) digit = e.KeyCode - Keys.NumPad1;

            if (digit >= 0 && digit < _targets.Count) LaunchIndex(digit);
        }

        void LaunchIndex(int i)
        {
            if (_launched) return;
            if (i < 0 || i >= _targets.Count) return;
            _launched = true;
            Launcher.Launch(_targets[i], _url);
            Close();
        }

        static GraphicsPath RoundedPath(RectangleF r, float radius)
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

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(CBack);

            DrawHeader(g);

            for (int i = 0; i < _targets.Count; i++)
                DrawRow(g, i);

            // Hairline border traced along the rounded edge.
            using (GraphicsPath p = RoundedPath(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), Corner))
            using (Pen bp = new Pen(CBorder, 1f))
                g.DrawPath(bp, p);
        }

        void DrawHeader(Graphics g)
        {
            string host = AppConfig.GetHost(_url);
            if (string.IsNullOrEmpty(host)) host = _url;

            TextFormatFlags f = TextFormatFlags.Left | TextFormatFlags.VerticalCenter
                              | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;

            Rectangle r = new Rectangle(PadX + 6, 0, Width - (PadX + 6) * 2, HeaderH);
            TextRenderer.DrawText(g, host, _fHost, r, CAccent, f);

            using (Pen p = new Pen(CRule, 1f))
                g.DrawLine(p, PadX, HeaderH - 1, Width - PadX, HeaderH - 1);
        }

        void DrawRow(Graphics g, int i)
        {
            Target t = _targets[i];
            Rectangle r = RowRect(i);

            bool selected = (i == _sel);
            if (selected || i == _hover)
            {
                RectangleF fill = new RectangleF(
                    RowInset, r.Y + 2, Width - RowInset * 2, RowH - 4);
                using (GraphicsPath p = RoundedPath(fill, 9f))
                using (SolidBrush b = new SolidBrush(selected ? CSel : CHover))
                    g.FillPath(b, p);
            }

            int x = PadX + 6;

            if (t.Image != null)
            {
                try
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawImage(t.Image, new Rectangle(x, r.Y + (RowH - IconSize) / 2, IconSize, IconSize));
                }
                catch { }
            }
            x += IconSize + 12;

            int chipSpace = ChipW + 10;
            int textW = Width - x - PadX - chipSpace;
            bool hasSub = !string.IsNullOrEmpty(t.Subtitle);

            TextFormatFlags f = TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;

            if (hasSub)
            {
                Rectangle r1 = new Rectangle(x, r.Y + 8, textW, 19);
                Rectangle r2 = new Rectangle(x, r.Y + 27, textW, 17);
                TextRenderer.DrawText(g, t.ProfileLabel, _fLabel, r1, CText, f);
                TextRenderer.DrawText(g, t.BrowserName + "  \u00B7  " + t.Subtitle, _fSub, r2, CDim, f);
            }
            else
            {
                Rectangle r1 = new Rectangle(x, r.Y + 9, textW, 19);
                Rectangle r2 = new Rectangle(x, r.Y + 28, textW, 17);
                TextRenderer.DrawText(g, t.ProfileLabel, _fLabel, r1, CText, f);
                TextRenderer.DrawText(g, t.BrowserName, _fSub, r2, CDim, f);
            }

            if (i < 9)
            {
                RectangleF chip = new RectangleF(
                    Width - PadX - ChipW - 4, r.Y + (RowH - ChipW) / 2f, ChipW, ChipW);
                if (selected)
                {
                    using (GraphicsPath p = RoundedPath(chip, 6f))
                    using (SolidBrush b = new SolidBrush(CChip))
                        g.FillPath(b, p);
                }
                TextFormatFlags fn = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                                   | TextFormatFlags.NoPrefix;
                TextRenderer.DrawText(g, (i + 1).ToString(), _fChip,
                    Rectangle.Round(chip), selected ? CDim : CDimmer, fn);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_fLabel != null) _fLabel.Dispose();
                if (_fSub != null) _fSub.Dispose();
                if (_fHost != null) _fHost.Dispose();
                if (_fChip != null) _fChip.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
