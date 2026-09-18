using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Picky
{
    /// <summary>The picker window: activation and input.
    /// How it looks is the job of PickerView.</summary>
    public class PickerForm : Form
    {
        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("gdi32.dll")]
        static extern IntPtr CreateRoundRectRgn(int l, int t, int r, int b, int w, int h);

        [DllImport("gdi32.dll")]
        static extern bool DeleteObject(IntPtr hObject);

        const int CS_DROPSHADOW = 0x00020000;
        const int WM_GETMINMAXINFO = 0x0024;

        [StructLayout(LayoutKind.Sequential)]
        struct POINTS
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MINMAXINFO
        {
            public POINTS Reserved;
            public POINTS MaxSize;
            public POINTS MaxPosition;
            public POINTS MinTrackSize;
            public POINTS MaxTrackSize;
        }

        readonly List<Target> _targets;
        readonly string _url;
        readonly PickerView _view;
        int _sel;
        int _hover = -1;
        bool _launched;
        bool _everActivated;

        public static bool AutoClose = true;

        // There is deliberately no opening animation. The picker is launched as a
        // fresh process by a click, and the only two ways to animate that first
        // appearance both cost more than they give: holding the message loop to
        // drive the frames keeps the shell showing its launch spinner, and a
        // WM_TIMER is delivered only when the queue is idle, which during window
        // creation means the frames arrive in clumps. Appearing at once is both
        // faster and steadier than either.

        public PickerForm(List<Target> targets, string url, AppConfig cfg)
        {
            _targets = targets;
            _url = url;
            _view = new PickerView(targets, url, cfg);
            _sel = 0;

            // Without this the form scales itself against the system font when its
            // handle is created, growing the window while the menu keeps painting
            // at the size it measured - the content ends up in the top-left corner
            // of a larger, half-empty window.
            AutoScaleMode = AutoScaleMode.None;

            ControlBox = false;
            MinimizeBox = false;
            MaximizeBox = false;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = ColorTranslator.FromHtml("#1C1D20");
            DoubleBuffered = true;
            KeyPreview = true;
            Text = "Picky";

            Size s = _view.Measure(Screen.FromPoint(Cursor.Position).WorkingArea.Width);
            Width = s.Width;
            Height = s.Height;

            PositionAtCursor();
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

        /// <summary>
        /// Windows refuses to make any window smaller than MinWindowTrackSize,
        /// 136x39 by default. A dock of logos is narrower than that, and the menu
        /// was left painted in the corner of an over-wide window. This is the only
        /// place that limit can be lifted.
        /// </summary>
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_GETMINMAXINFO && m.LParam != IntPtr.Zero)
            {
                MINMAXINFO mmi = (MINMAXINFO)Marshal.PtrToStructure(m.LParam, typeof(MINMAXINFO));
                mmi.MinTrackSize.X = 1;
                mmi.MinTrackSize.Y = 1;
                Marshal.StructureToPtr(mmi, m.LParam, false);
                m.Result = IntPtr.Zero;
                return;
            }
            base.WndProc(ref m);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            // The rounded region is cut to a specific size; anything that resizes
            // the window has to have it cut again or the corners stop matching.
            if (IsHandleCreated) ApplyRoundedRegion();
        }

        void ApplyRoundedRegion()
        {
            int corner = _view.CornerRadius;
            IntPtr rgn = CreateRoundRectRgn(0, 0, Width + 1, Height + 1,
                corner * 2, corner * 2);
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

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int i = _view.IndexAt(e.Location);
            if (i != _hover)
            {
                _hover = i;
                if (i >= 0) _sel = i;
                // A hand over the rows, an arrow over the header and the padding:
                // the pointer should only promise a click where one does something.
                Cursor = i >= 0 ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = -1;
            Cursor = Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            int i = _view.IndexAt(e.Location);
            if (i >= 0) LaunchIndex(i);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.KeyCode == Keys.Escape) { Close(); return; }

            // Both axes step through the list: which one feels natural depends on
            // the layout, and neither key does anything else here.
            if (e.KeyCode == Keys.Down || e.KeyCode == Keys.Right)
            {
                _sel = (_sel + 1) % _targets.Count;
                Invalidate(); e.Handled = true; return;
            }
            if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Left)
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

        protected override void OnPaint(PaintEventArgs e)
        {
            _view.Paint(e.Graphics, _sel, _hover);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_view != null) _view.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
