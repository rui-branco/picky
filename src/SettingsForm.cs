using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Picky
{
    /// <summary>
    /// The settings window: a rail on the left and one page at a time on the
    /// right, the same shell as Keycap. Every change is written the moment it
    /// is made, so there is no Save button to forget.
    /// </summary>
    public class SettingsForm : ChromeForm
    {
        List<Target> _targets;
        AppConfig _cfg;

        Rail _rail;
        NavItem _navGeneral, _navPicker, _navRules, _navAbout;
        GeneralPage _general;
        PickerPage _picker;
        RulesPage _rules;
        AboutPage _about;
        readonly List<Page> _pages = new List<Page>();
        Page _current;

        TextLine _version;
        FlatButton _updateBtn;
        // Tracked here, not read back from Visible, which is false for every
        // control until the window is shown.
        bool _showUpdate;
        Toast _toast;

        Icon _appIcon;
        Bitmap _logo;

        ReleaseInfo _update;
        string _updateError;   // why the last download or swap failed
        string _checkError;    // why the last check did not get through
        bool _updating;
        bool _checking;
        bool _everChecked;     // a check has finished at least once
        bool _checked;         // the check made on opening has been started

        bool _isDefault;
        bool _registered;

        List<FileSystemWatcher> _watchers = new List<FileSystemWatcher>();
        RegistryWatch _registry;   // browsers registering or leaving under ...\Clients
        Timer _rescan;   // debounces Local State churn
        Timer _poll;     // notices the default handler changing outside this app

        /// <param name="page">general, picker, rules or about; anything else opens General.</param>
        public SettingsForm(string page)
        {
            _cfg = AppConfig.Load();
            _targets = _cfg.Arrange(BrowserScanner.Scan());
            _isDefault = Registration.IsDefaultBrowser();
            _registered = Registration.IsRegistered();

            try { _appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { }
            if (_appIcon != null)
            {
                Icon = _appIcon;
                try { _logo = _appIcon.ToBitmap(); }
                catch { }
            }

            Text = "Picky";
            BackColor = Theme.Back;
            ForeColor = Theme.Text;
            Font = Theme.Font(9f, FontStyle.Regular);
            StartPosition = FormStartPosition.CenterScreen;

            // Window sizes include the invisible resize frame: 8px each side
            // and 8 along the bottom, none on top now the caption is gone.
            MinimumSize = new Size(820, 560);
            Rectangle work = Screen.PrimaryScreen.WorkingArea;
            Size = new Size(Math.Max(820, Math.Min(980, work.Width - 40)),
                            Math.Max(560, Math.Min(680, work.Height - 40)));

            // The rail first: it owns the toast a page may need while it is built.
            BuildRail();
            BuildPages();

            ShowPage(PageNamed(page));
            LayoutShell();
            SyncStatus(true);
            DrawUpdate();

            StartWatching();
        }

        // ---- shared with the pages -------------------------------------------

        internal AppConfig Config { get { return _cfg; } }

        /// <summary>The browsers and profiles, in the picker's order.</summary>
        internal List<Target> Targets { get { return _targets; } }

        /// <summary>Write the config. Every control calls this as it changes.</summary>
        internal void SaveConfig()
        {
            if (!_cfg.Save()) Toast("Could not write " + AppConfig.FilePath, Theme.Bad);
        }

        /// <summary>Scan again under the current options - the private-window switch changes what exists.</summary>
        internal void ReloadTargets()
        {
            _targets = _cfg.Arrange(BrowserScanner.Scan());
            TargetsChanged();
        }

        /// <summary>Persists the order after a logo has been dragged in the preview.</summary>
        internal void SaveOrder()
        {
            _cfg.Order = new List<string>();
            foreach (Target t in _targets) _cfg.Order.Add(t.Id);
            SaveConfig();

            _rules.LoadTargets();
        }

        /// <summary>The browser list changed: the preview, the targets and the rules all follow it.</summary>
        void TargetsChanged()
        {
            _picker.Bind();
            _rules.LoadTargets();
            _rules.RefreshRules();
        }

        void Toast(string msg, Color c)
        {
            _toast.Say(msg, c);
        }

        // ---- rail and pages --------------------------------------------------

        void BuildRail()
        {
            _rail = new Rail();
            _rail.Logo = _logo;
            Controls.Add(_rail);

            _navGeneral = _rail.Add("General", NavGlyph.General);
            _navPicker = _rail.Add("Picker", NavGlyph.Picker);
            _navRules = _rail.Add("Rules", NavGlyph.Rules);
            _navAbout = _rail.Add("About", NavGlyph.About);
            _rail.Navigate += delegate(object s, NavItem n) { ShowPage(PageFor(n)); };

            // The version sits here quietly, and is the same spot an update
            // offers itself from: a found release adds a button over it rather
            // than interrupting anything.
            _version = new TextLine();
            _version.Font = Theme.Font(8.25f, FontStyle.Regular);
            _version.Ink = Theme.Dimmer;
            _version.BackColor = Theme.Rail;
            _version.Text = "Picky " + Updater.CurrentLabel;
            _rail.Controls.Add(_version);

            _updateBtn = new FlatButton();
            _updateBtn.Primary = true;
            _updateBtn.Text = "Update";
            _updateBtn.Font = Theme.Font(8.25f, FontStyle.Regular);
            _updateBtn.Backdrop = Theme.Rail;
            _updateBtn.Height = 28;
            _updateBtn.Visible = false;
            _updateBtn.Click += delegate
            {
                if (_update == null) return;
                Toast("Downloading Picky " + _update.Tag + "…", Theme.Dim);
                StartUpdate();
            };
            _rail.Controls.Add(_updateBtn);

            _toast = new Toast();
            Controls.Add(_toast);
        }

        void BuildPages()
        {
            _general = new GeneralPage(this);
            _picker = new PickerPage(this);
            _rules = new RulesPage(this);
            _about = new AboutPage(this);
            _pages.AddRange(new Page[] { _general, _picker, _rules, _about });

            foreach (Page p in _pages)
            {
                p.Visible = false;
                p.Say = Toast;
                Controls.Add(p);
            }
        }

        /// <summary>The page --page asks for by name.</summary>
        Page PageNamed(string name)
        {
            if (string.Equals(name, "picker", StringComparison.OrdinalIgnoreCase)) return _picker;
            if (string.Equals(name, "rules", StringComparison.OrdinalIgnoreCase)) return _rules;
            if (string.Equals(name, "about", StringComparison.OrdinalIgnoreCase)) return _about;
            return _general;
        }

        Page PageFor(NavItem n)
        {
            if (n == _navPicker) return _picker;
            if (n == _navRules) return _rules;
            if (n == _navAbout) return _about;
            return _general;
        }

        NavItem NavFor(Page p)
        {
            if (p == _picker) return _navPicker;
            if (p == _rules) return _navRules;
            if (p == _about) return _navAbout;
            return _navGeneral;
        }

        void ShowPage(Page page)
        {
            if (page == null) return;
            NavItem n = NavFor(page);
            if (!n.Selected) { _rail.Select(n); return; }   // Select comes back here

            if (_current == page) return;
            Page was = _current;
            _current = page;
            page.Visible = true;
            page.BringToFront();
            if (was != null) was.Visible = false;
            _toast.BringToFront();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutShell();
        }

        void LayoutShell()
        {
            if (_rail == null || _pages.Count == 0) return;
            int w = ClientSize.Width, h = ClientSize.Height;

            _rail.SetBounds(0, 0, Rail.W, h);
            foreach (Page p in _pages)
                p.SetBounds(Rail.W, CaptionHeight, w - Rail.W, h - CaptionHeight);

            // Rail footer, packed up from the bottom edge: the version, and the
            // update button over it once there is one.
            int y = h - 16 - 18;
            _version.SetBounds(17, y, Rail.W - 32, 18);
            if (_showUpdate)
            {
                _updateBtn.Width = _updateBtn.PreferredWidth();
                y -= 8 + _updateBtn.Height;
                _updateBtn.Location = new Point(16, y);
            }

            _toast.CenterLeft = Rail.W;
            _toast.Top = h - _toast.Height - 22;
            if (_toast.Visible) _toast.Left = Rail.W + (w - Rail.W - _toast.Width) / 2;
        }

        // ---- live updates ----------------------------------------------------

        void StartWatching()
        {
            _rescan = new Timer();
            _rescan.Interval = 900;               // browsers rewrite Local State often
            _rescan.Tick += delegate { _rescan.Stop(); Rescan(); };

            _poll = new Timer();
            _poll.Interval = 2000;
            _poll.Tick += delegate { SyncStatus(false); };
            _poll.Start();

            foreach (string dir in BrowserScanner.UserDataDirs())
            {
                try
                {
                    FileSystemWatcher w = new FileSystemWatcher(dir, "Local State");
                    w.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName;
                    w.Changed += OnProfilesChanged;
                    w.Created += OnProfilesChanged;
                    w.Renamed += OnProfilesChanged;
                    w.EnableRaisingEvents = true;
                    _watchers.Add(w);
                }
                catch { }
            }

            _registry = new RegistryWatch();
            _registry.Changed += OnRegistryChanged;
        }

        void OnProfilesChanged(object sender, FileSystemEventArgs e)
        {
            // Raised on a watcher thread - hop to the UI thread, then debounce.
            try
            {
                if (!IsHandleCreated || IsDisposed) return;
                BeginInvoke((MethodInvoker)delegate
                {
                    _rescan.Stop();
                    _rescan.Start();
                });
            }
            catch { }
        }

        void OnRegistryChanged(object sender, EventArgs e)
        {
            // Raised on the watch thread - the same hop and debounce as Local State.
            try
            {
                if (!IsHandleCreated || IsDisposed || Disposing) return;
                BeginInvoke((MethodInvoker)delegate
                {
                    _rescan.Stop();
                    _rescan.Start();
                });
            }
            catch { }
        }

        void Rescan()
        {
            List<Target> found;
            try { found = _cfg.Arrange(BrowserScanner.Scan()); }
            catch { return; }

            if (SameTargets(_targets, found)) return;   // don't churn the UI for nothing

            _targets = found;
            TargetsChanged();
        }

        static bool SameTargets(List<Target> a, List<Target> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i].Id != b[i].Id) return false;
                if (a[i].ProfileLabel != b[i].ProfileLabel) return false;
                if (a[i].Subtitle != b[i].Subtitle) return false;
            }
            return true;
        }

        /// <summary>Read the registration back from Windows and show it on the General page.</summary>
        internal void SyncStatus(bool force)
        {
            bool isDef = Registration.IsDefaultBrowser();
            bool reg = Registration.IsRegistered();
            if (!force && isDef == _isDefault && reg == _registered) return;

            _isDefault = isDef;
            _registered = reg;
            _general.ShowStatus(_registered, _isDefault);
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            SyncStatus(false);   // catches a default set in Windows Settings while we were away

            // And whatever the registry watch cannot see, such as a browser's
            // command changing under Classes. Rescan only redraws on a difference.
            _rescan.Stop();
            _rescan.Start();
        }

        // ---- updates ---------------------------------------------------------

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            // Only once per window, and only after there is a handle to marshal back to.
            if (_checked) return;
            _checked = true;
            CheckForUpdates();
        }

        /// <summary>The About page's button: install what was found, or look again.</summary>
        internal void UpdateClicked()
        {
            if (_update != null) StartUpdate();
            else CheckForUpdates();
        }

        void CheckForUpdates()
        {
            if (_updating || _update != null || _checking) return;

            _checking = true;
            _checkError = null;
            DrawUpdate();
            Updater.CheckAsync(delegate(ReleaseInfo r, string err)
            {
                try
                {
                    if (!IsHandleCreated || IsDisposed) return;
                    BeginInvoke((MethodInvoker)delegate { CheckDone(r, err); });
                }
                catch { }
            });
        }

        void CheckDone(ReleaseInfo r, string err)
        {
            _checking = false;
            _everChecked = true;
            if (r != null) _update = r;
            else _checkError = err;
            DrawUpdate();
        }

        void StartUpdate()
        {
            if (_update == null || _updating) return;

            _updating = true;
            _updateError = null;
            DrawUpdate();

            Updater.DownloadAsync(_update, delegate(string path, string err)
            {
                try
                {
                    if (!IsHandleCreated || IsDisposed) return;
                    BeginInvoke((MethodInvoker)delegate { FinishUpdate(path, err); });
                }
                catch { }
            });
        }

        void FinishUpdate(string path, string err)
        {
            if (err == null && path != null)
            {
                // Apply restarts into the new build, so nothing below this runs.
                try { Updater.Apply(path); return; }
                catch (Exception ex) { err = ex.Message; }
            }

            _updating = false;
            _updateError = err == null ? "no download" : err;
            DrawUpdate();
            if (_current != _about) Toast("Update failed: " + _updateError, Theme.Bad);
        }

        /// <summary>Put the update state into the About page and the rail.</summary>
        void DrawUpdate()
        {
            string detail;
            string button = "Check for updates";
            bool primary = false, enabled = true;

            if (_updating)
            {
                detail = "Downloading Picky " + _update.Tag + "…";
                button = "Update";
                primary = true;
                enabled = false;
            }
            else if (_update != null)
            {
                detail = _updateError != null
                    ? "Update failed: " + _updateError
                    : "Picky " + _update.Tag + " is available.";
                button = "Update";
                primary = true;
            }
            else if (_checking || !_everChecked)
            {
                detail = "Checking…";
                enabled = false;
            }
            else if (_checkError != null)
                detail = "Could not check for updates.";
            else
                detail = "You have the latest version.";

            _about.ShowUpdate(detail, button, primary, enabled);

            _updateBtn.Enabled = !_updating;
            bool show = _update != null;
            if (_showUpdate != show)
            {
                _showUpdate = show;
                _updateBtn.Visible = show;
                LayoutShell();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (FileSystemWatcher w in _watchers)
                {
                    try { w.EnableRaisingEvents = false; w.Dispose(); }
                    catch { }
                }
                _watchers.Clear();
                if (_registry != null) _registry.Dispose();
                if (_rescan != null) _rescan.Dispose();
                if (_poll != null) _poll.Dispose();
                if (_logo != null) _logo.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
