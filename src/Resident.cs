using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace Picky
{
    /// <summary>
    /// What a click runs first. Nothing in here may touch WinForms or GDI+: the
    /// point is to reach a running Picky before either of them has loaded.
    /// </summary>
    static class Handoff
    {
        internal const string WindowTitle = "Picky.Resident";
        internal const string MutexName = @"Local\Picky.Resident";
        internal static readonly IntPtr MessageOnly = new IntPtr(-3);

        internal const int WM_CLOSE = 0x0010;
        internal const int WM_COPYDATA = 0x004A;
        const int SMTO_ABORTIFHUNG = 0x0002;
        const int VK_SHIFT = 0x10;

        // Tags a message as a link from Picky, not stray WM_COPYDATA traffic.
        static readonly IntPtr Magic = new IntPtr(0x50434B59);
        const int MaxChars = 32 * 1024;

        [StructLayout(LayoutKind.Sequential)]
        struct COPYDATASTRUCT
        {
            public IntPtr dwData;
            public int cbData;
            public IntPtr lpData;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string title);

        [DllImport("user32.dll")]
        static extern int GetWindowThreadProcessId(IntPtr hWnd, out int pid);

        [DllImport("user32.dll")]
        internal static extern bool AllowSetForegroundWindow(int pid);

        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int vk);

        [DllImport("user32.dll")]
        static extern IntPtr SendMessageTimeout(IntPtr hWnd, int msg, IntPtr wParam,
            ref COPYDATASTRUCT lParam, int flags, int timeout, out IntPtr result);

        [DllImport("user32.dll")]
        internal static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        internal static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll")]
        internal static extern bool SetProcessWorkingSetSize(IntPtr proc, IntPtr min, IntPtr max);

        static IntPtr Find()
        {
            return FindWindowEx(MessageOnly, IntPtr.Zero, null, WindowTitle);
        }

        public static bool IsRunning()
        {
            try { return Find() != IntPtr.Zero; }
            catch { return false; }
        }

        /// <summary>Passes the link to a running Picky. False means there is none
        /// to take it, and this process has to show the picker itself.</summary>
        public static bool Send(string url)
        {
            try
            {
                IntPtr h = Find();
                if (h == IntPtr.Zero) return false;

                // This process was started by the click and may take the
                // foreground; the running one was not, and without the right to
                // it the picker would open behind the window the link was in.
                int pid;
                GetWindowThreadProcessId(h, out pid);
                if (pid != 0) AllowSetForegroundWindow(pid);

                // Shift is read here, at the click: by the time the running copy
                // gets the message, its own view of the keyboard is stale.
                bool shift = (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0;
                string payload = (shift ? "1" : "0") + url;

                IntPtr buf = Marshal.StringToHGlobalUni(payload);
                try
                {
                    COPYDATASTRUCT cds;
                    cds.dwData = Magic;
                    cds.cbData = payload.Length * 2;
                    cds.lpData = buf;

                    IntPtr result;
                    IntPtr ok = SendMessageTimeout(h, WM_COPYDATA, IntPtr.Zero, ref cds,
                        SMTO_ABORTIFHUNG, 3000, out result);
                    return ok != IntPtr.Zero && result == (IntPtr)1;
                }
                finally { Marshal.FreeHGlobal(buf); }
            }
            catch { return false; }
        }

        /// <summary>The text a WM_COPYDATA carried, or null when it is not ours.</summary>
        internal static string Read(IntPtr lParam)
        {
            if (lParam == IntPtr.Zero) return null;
            COPYDATASTRUCT cds = (COPYDATASTRUCT)Marshal.PtrToStructure(lParam, typeof(COPYDATASTRUCT));
            if (cds.dwData != Magic || cds.lpData == IntPtr.Zero) return null;
            if (cds.cbData < 4 || cds.cbData > MaxChars * 2 || cds.cbData % 2 != 0) return null;
            return Marshal.PtrToStringUni(cds.lpData, cds.cbData / 2);
        }

        /// <summary>Asks a running Picky to exit, if there is one.</summary>
        public static void Stop()
        {
            try
            {
                IntPtr h = Find();
                if (h != IntPtr.Zero) PostMessage(h, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            }
            catch { }
        }
    }

    /// <summary>
    /// One Picky kept running between clicks. A fresh .NET process per link spent
    /// most of its time loading and compiling before it could draw anything;
    /// kept alive, that is paid once and a click costs a few milliseconds. Idle,
    /// it runs no timers and hands its memory back.
    /// </summary>
    static class Resident
    {
        const int WM_APP_OPEN = 0x8000 + 1;

        // Owned for the life of the process: holding it is what makes this the
        // running copy, and Windows releases it if the process dies.
#pragma warning disable 414
        static Mutex _claim;
#pragma warning restore 414
        static Listener _listener;
        static PickerForm _open;
        static System.Windows.Forms.Timer _trim;
        static readonly Queue<string> _pending = new Queue<string>();

        /// <summary>Makes this process the running copy. False when another
        /// already is, or is starting up.</summary>
        public static bool Claim()
        {
            Mutex m;
            bool created;
            try { m = new Mutex(true, Handoff.MutexName, out created); }
            catch { return false; }
            if (!created) { m.Close(); return false; }
            _claim = m;

            // A process that lives all session should not pin whatever folder
            // the click happened to start it in.
            try { Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory; }
            catch { }

            _trim = new System.Windows.Forms.Timer();
            _trim.Interval = 2000;
            _trim.Tick += delegate
            {
                _trim.Stop();
                if (_open == null) Trim();
            };

            _listener = new Listener();
            return true;
        }

        public static void Run()
        {
            Application.Run(new ApplicationContext());
        }

        /// <summary>
        /// Starts the running copy in a process of its own when there is none, so
        /// it outlives whichever window asked for it.
        /// </summary>
        public static void EnsureRunning()
        {
            if (Handoff.IsRunning()) return;
            try { System.Diagnostics.Process.Start(Application.ExecutablePath, "--background"); }
            catch { }
        }

        /// <summary>
        /// Runs the picker path once with nobody waiting on it, so a start at
        /// sign-in pays for compiling it instead of the first click.
        /// </summary>
        public static void Warm()
        {
            try
            {
                const string sample = "https://example.com/";
                AppConfig cfg = AppConfig.Load();
                List<Target> targets = cfg.Arrange(BrowserScanner.Scan());
                cfg.MatchRule(sample);
                using (PickerForm f = new PickerForm(targets, sample, cfg))
                using (Bitmap b = new Bitmap(Math.Max(1, f.Width), Math.Max(1, f.Height)))
                    f.DrawToBitmap(b, new Rectangle(0, 0, b.Width, b.Height));
            }
            catch { }
            Trim();
        }

        public static void Open(string url, bool forcePicker)
        {
            // A second link while the picker is up replaces it rather than stacking.
            if (_open != null)
            {
                PickerForm old = _open;
                _open = null;
                old.Close();
            }

            PickerForm f = null;
            try { f = Program.Route(url, forcePicker); }
            catch (Exception ex) { Program.LogError(ex); }

            if (f == null) { _trim.Stop(); _trim.Start(); return; }

            _open = f;
            f.FormClosed += delegate
            {
                if (_open == f) _open = null;
                _trim.Stop();
                _trim.Start();
            };
            f.Show();
        }

        /// <summary>
        /// Nothing runs again until the next click, so give the pages back rather
        /// than sit on them. They are still in memory and fault back in far
        /// faster than a new process could start.
        /// </summary>
        static void Trim()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            Handoff.SetProcessWorkingSetSize(Handoff.GetCurrentProcess(), (IntPtr)(-1), (IntPtr)(-1));
        }

        /// <summary>A message-only window: invisible, and found by title alone.</summary>
        sealed class Listener : NativeWindow
        {
            public Listener()
            {
                CreateParams cp = new CreateParams();
                cp.Caption = Handoff.WindowTitle;
                cp.Parent = Handoff.MessageOnly;
                CreateHandle(cp);
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == Handoff.WM_COPYDATA)
                {
                    string req = Handoff.Read(m.LParam);
                    bool ok = req != null && Program.IsAllowedUrl(req.Substring(1));
                    if (ok)
                    {
                        // Answer at once and open on the next pass of the loop:
                        // the sender is blocked until this returns, and a dialog
                        // shown in here would hold it past its timeout.
                        _pending.Enqueue(req);
                        Handoff.PostMessage(Handle, WM_APP_OPEN, IntPtr.Zero, IntPtr.Zero);
                    }
                    m.Result = ok ? (IntPtr)1 : IntPtr.Zero;
                    return;
                }
                if (m.Msg == WM_APP_OPEN)
                {
                    while (_pending.Count > 0)
                    {
                        string req = _pending.Dequeue();
                        Open(req.Substring(1), req[0] == '1');
                    }
                    return;
                }
                if (m.Msg == Handoff.WM_CLOSE)
                {
                    Application.ExitThread();
                    return;
                }
                base.WndProc(ref m);
            }
        }
    }
}
