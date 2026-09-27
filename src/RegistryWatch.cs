using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Threading;
using Microsoft.Win32;

namespace Picky
{
    /// <summary>
    /// Notices a browser registering with Windows or leaving while the settings
    /// window is open (a dev build of Forge Deck adds itself under
    /// StartMenuInternet when it starts and removes itself when it quits). The
    /// Clients keys are watched rather than StartMenuInternet, which may not
    /// exist yet. Changed is raised on a background thread.
    /// </summary>
    public sealed class RegistryWatch : IDisposable
    {
        const int REG_NOTIFY_CHANGE_NAME = 0x1;
        const int REG_NOTIFY_CHANGE_LAST_SET = 0x4;

        [DllImport("advapi32.dll")]
        static extern int RegNotifyChangeKeyValue(IntPtr hKey, bool watchSubtree, int notifyFilter,
            IntPtr hEvent, bool asynchronous);

        public event EventHandler Changed;

        readonly List<RegistryKey> _keys = new List<RegistryKey>();
        readonly List<AutoResetEvent> _events = new List<AutoResetEvent>();
        readonly ManualResetEvent _stop = new ManualResetEvent(false);
        Thread _thread;
        bool _disposed;

        public RegistryWatch()
        {
            Open(RegistryHive.CurrentUser, RegistryView.Default, @"Software\Clients");
            Open(RegistryHive.LocalMachine, RegistryView.Registry64, @"SOFTWARE\Clients");
            Open(RegistryHive.LocalMachine, RegistryView.Registry32, @"SOFTWARE\Clients");
            if (_keys.Count == 0) return;

            // Armed here so the watch is live the moment this returns. Should
            // this thread exit first, Windows signals the event once and the
            // watch thread re-arms it as its own.
            for (int i = 0; i < _keys.Count; i++) Arm(i);

            _thread = new Thread(Run);
            _thread.IsBackground = true;
            _thread.Name = "Picky registry watch";
            _thread.Start();
        }

        void Open(RegistryHive hive, RegistryView view, string path)
        {
            try
            {
                RegistryKey key;
                using (RegistryKey root = RegistryKey.OpenBaseKey(hive, view))
                    key = root.OpenSubKey(path, RegistryKeyPermissionCheck.Default, RegistryRights.Notify);
                if (key == null) return;
                _keys.Add(key);
                _events.Add(new AutoResetEvent(false));
            }
            catch { }
        }

        void Arm(int i)
        {
            RegNotifyChangeKeyValue(_keys[i].Handle.DangerousGetHandle(), true,
                REG_NOTIFY_CHANGE_NAME | REG_NOTIFY_CHANGE_LAST_SET,
                _events[i].SafeWaitHandle.DangerousGetHandle(), true);
        }

        void Run()
        {
            WaitHandle[] handles = new WaitHandle[_events.Count + 1];
            handles[0] = _stop;
            for (int i = 0; i < _events.Count; i++) handles[i + 1] = _events[i];

            try
            {
                while (true)
                {
                    int n = WaitHandle.WaitAny(handles);
                    if (n == 0) return;

                    // A notification fires once: re-arm before anyone rescans,
                    // so a change made during the rescan still gets through.
                    Arm(n - 1);

                    EventHandler h = Changed;
                    if (h != null)
                    {
                        try { h(this, EventArgs.Empty); }
                        catch { }
                    }
                }
            }
            catch { }   // a handle closed under us: Dispose gave up waiting
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _stop.Set();
            if (_thread != null) _thread.Join(1000);

            // Closing a key also cancels its pending notification.
            foreach (RegistryKey k in _keys) k.Dispose();
            foreach (AutoResetEvent e in _events) e.Dispose();
            _keys.Clear();
            _events.Clear();
            _stop.Dispose();
        }
    }
}
