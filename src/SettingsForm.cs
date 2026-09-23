using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace Picky
{
    public class SettingsForm : Form
    {
        List<Target> _targets;
        AppConfig _cfg;

        RowList _ruleList;
        TextBox _pattern;
        ComboBox _target;
        FlatButton _defaultBtn;
        DarkCheck _showPrivate;
        DarkCheck _showLabels;
        DarkCheck _layoutRow;
        DarkCheck _showHost;
        DarkCheck _showBrowser;
        Label _dragHint;
        Label _sizeLabel;
        FlatButton[] _sizeBtns;

        /// <summary>The sizes the picker can be drawn at, smallest first.</summary>
        static readonly double[] SizeSteps =
            new double[] { 0.8d, 1.0d, 1.25d, 1.5d, 1.8d };
        static readonly string[] SizeNames =
            new string[] { "S", "M", "L", "XL", "XXL" };
        PickerPreview _preview;
        FlatButton _updateBtn;
        FlatButton _checkBtn;

        ReleaseInfo _update;
        string _updateText;
        bool _updating;
        bool _checked;
        bool _checking;
        bool _reportCheck;   // the check in flight was asked for, so say how it went
        Timer _checkNote;    // puts the button back to its plain label

        const string CheckLabel = "Check for updates";

        Card _acard, _rcard, _pbox;
        FlatButton _add, _del, _save;

        bool _isDefault;
        bool _registered;
        Icon _appIcon;

        Font _fTitle, _fSub, _fSection, _fBody;

        List<FileSystemWatcher> _watchers = new List<FileSystemWatcher>();
        Timer _rescan;   // debounces Local State churn
        Timer _poll;     // notices the default handler changing outside this app

        const int Pad = 26;
        const int CardW = 668;

        int _cardW = CardW;
        int _yFooter;
        int _yUpdate;
        int _contentH;

        public SettingsForm()
        {
            _cfg = AppConfig.Load();
            _targets = _cfg.Arrange(BrowserScanner.Scan());
            _isDefault = Registration.IsDefaultBrowser();
            _registered = Registration.IsRegistered();

            _fTitle = Theme.Font(15f, FontStyle.Regular);
            _fSub = Theme.Font(9f, FontStyle.Regular);
            _fSection = Theme.Font(8.25f, FontStyle.Bold);
            _fBody = Theme.Font(9f, FontStyle.Regular);

            try { _appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { }

            Text = "Picky";
            if (_appIcon != null) Icon = _appIcon;
            ClientSize = new Size(CardW + Pad * 2, 700);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Back;
            ForeColor = Theme.Text;
            Font = _fBody;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new Size(520, 520);
            DoubleBuffered = true;
            AutoScroll = true;

            BuildUi();

            // A life-size preview can push the window past a short screen, so take
            // the height the content asks for and let scrolling cover the rest.
            int maxH = Screen.PrimaryScreen.WorkingArea.Height - 80;
            bool scrolls = _contentH > maxH;
            ClientSize = new Size(
                CardW + Pad * 2 + (scrolls ? SystemInformation.VerticalScrollBarWidth : 0),
                Math.Min(_contentH, maxH));

            StartWatching();
        }

        int _yStatus, _yPicker, _yRules;

        /// <summary>
        /// Creates the controls. Where each one lands is decided by LayoutUi, so
        /// the window can be resized without every bound frozen at construction.
        /// </summary>
        void BuildUi()
        {
            // ---- status pill (painted) with its action button ----
            _defaultBtn = new FlatButton();
            _defaultBtn.Backdrop = Theme.Back;
            _defaultBtn.Ghost = true;
            _defaultBtn.Font = _fBody;
            _defaultBtn.Click += delegate
            {
                // Registering used to have its own button. This is now the only way
                // back if the association was ever undone outside the installer.
                if (!_registered) { Registration.Register(); SyncStatus(true); return; }
                Registration.OpenDefaultAppsSettings();
            };
            Controls.Add(_defaultBtn);

            // ---- update pill (painted), hidden until there is one ----
            _updateBtn = new FlatButton();
            _updateBtn.Backdrop = Theme.Card;
            _updateBtn.Primary = true;
            _updateBtn.Text = "Update";
            _updateBtn.Font = _fBody;
            _updateBtn.Visible = false;
            _updateBtn.Click += delegate { StartUpdate(); };
            Controls.Add(_updateBtn);

            // ---- manual update check, beside the status word ----
            // The bar above only exists once a release is found, which left no
            // way to ask on a build that was already current.
            _checkBtn = new FlatButton();
            _checkBtn.Backdrop = Theme.Back;
            _checkBtn.Ghost = true;
            _checkBtn.Font = _fBody;
            _checkBtn.Text = CheckLabel;
            _checkBtn.Click += delegate { CheckForUpdates(true); };
            Controls.Add(_checkBtn);

            _checkNote = new Timer();
            _checkNote.Interval = 4000;
            _checkNote.Tick += delegate
            {
                _checkNote.Stop();
                if (!_checking) _checkBtn.Text = CheckLabel;
                _checkBtn.Invalidate();
            };

            // ---- browsers ----
            // Lives on the picker card with the other switches: what it really
            // decides is whether the menu offers a private window.
            _showPrivate = new DarkCheck();
            _showPrivate.Backdrop = Theme.Card;
            _showPrivate.Text = "Show private and incognito";
            _showPrivate.Font = _fBody;
            _showPrivate.Checked = _cfg.ShowPrivate;
            _showPrivate.CheckedChanged += delegate
            {
                _cfg.ShowPrivate = _showPrivate.Checked;
                _cfg.Save();
                _targets = _cfg.Arrange(BrowserScanner.Scan());
                LoadTargetsIntoUi();
                RefreshRules();
                LayoutUi();
            };

            // ---- picker appearance ----
            _acard = new Card();
            Controls.Add(_acard);

            _layoutRow = new DarkCheck();
            _layoutRow.Backdrop = Theme.Card;
            _layoutRow.Text = "Lay the logos out in a row";
            _layoutRow.Font = _fBody;
            _layoutRow.Checked = _cfg.IsRowLayout();
            _layoutRow.CheckedChanged += delegate
            {
                _cfg.PickerLayout = _layoutRow.Checked
                    ? AppConfig.LayoutRow : AppConfig.LayoutColumn;
                _cfg.Save();
                _preview.Bind(_targets, _cfg);
                LayoutUi();
            };
            _acard.Controls.Add(_layoutRow);

            _showLabels = new DarkCheck();
            _showLabels.Backdrop = Theme.Card;
            _showLabels.Text = "Show profile names";
            _showLabels.Font = _fBody;
            _showLabels.Checked = _cfg.ShowLabels;
            _showLabels.CheckedChanged += delegate
            {
                _cfg.ShowLabels = _showLabels.Checked;
                _cfg.Save();
                _preview.Bind(_targets, _cfg);
                LayoutUi();
            };
            _acard.Controls.Add(_showLabels);

            _showHost = new DarkCheck();
            _showHost.Backdrop = Theme.Card;
            _showHost.Text = "Show the link address";
            _showHost.Font = _fBody;
            _showHost.Checked = _cfg.ShowHost;
            _showHost.CheckedChanged += delegate
            {
                _cfg.ShowHost = _showHost.Checked;
                _cfg.Save();
                _preview.Bind(_targets, _cfg);
                LayoutUi();
            };
            _acard.Controls.Add(_showHost);
            _acard.Controls.Add(_showPrivate);

            _showBrowser = new DarkCheck();
            _showBrowser.Backdrop = Theme.Card;
            _showBrowser.Text = "Show browser names";
            _showBrowser.Font = _fBody;
            _showBrowser.Checked = _cfg.ShowBrowserName;
            _showBrowser.CheckedChanged += delegate
            {
                _cfg.ShowBrowserName = _showBrowser.Checked;
                _cfg.Save();
                _preview.Bind(_targets, _cfg);
                LayoutUi();
            };
            _acard.Controls.Add(_showBrowser);

            _dragHint = new Label();
            _dragHint.Text = "Drag a logo to put the menu in the order you want";
            _dragHint.Font = _fSub;
            _dragHint.BackColor = Theme.Card;
            _dragHint.ForeColor = Theme.Dimmer;
            _dragHint.TextAlign = ContentAlignment.MiddleCenter;
            _acard.Controls.Add(_dragHint);

            _sizeLabel = new Label();
            _sizeLabel.Text = "Size";
            _sizeLabel.Font = _fBody;
            _sizeLabel.BackColor = Theme.Card;
            _sizeLabel.ForeColor = Theme.Dim;
            _acard.Controls.Add(_sizeLabel);

            _sizeBtns = new FlatButton[SizeSteps.Length];
            for (int i = 0; i < SizeSteps.Length; i++)
            {
                FlatButton b = new FlatButton();
                b.Backdrop = Theme.Card;
                b.Radius = 6;
                b.Text = SizeNames[i];
                b.Font = _fBody;
                b.Tag = i;
                b.Click += SizeClicked;
                _sizeBtns[i] = b;
                _acard.Controls.Add(b);
            }
            SyncSizeButtons();

            // The real picker, rendered live - so the two toggles above are never
            // a guess about what a link will actually pop up.
            _preview = new PickerPreview();
            _preview.Font = _fBody;
            _preview.Reordered += delegate { SaveOrder(); };
            _acard.Controls.Add(_preview);

            // ---- rules ----
            _rcard = new Card();
            Controls.Add(_rcard);

            _ruleList = new RowList();
            _rcard.Controls.Add(_ruleList);

            // ---- add-rule row ----
            _pbox = new Card();
            _pbox.Radius = 8;
            Controls.Add(_pbox);

            _pattern = new TextBox();
            _pattern.BorderStyle = BorderStyle.None;
            _pattern.BackColor = Theme.Card;
            _pattern.ForeColor = Theme.Text;
            _pattern.Font = _fBody;
            _pattern.SetBounds(12, 9, 186, 18);
            _pattern.Text = "*.example.com";
            _pbox.Controls.Add(_pattern);

            _target = new ComboBox();
            _target.DropDownStyle = ComboBoxStyle.DropDownList;
            _target.FlatStyle = FlatStyle.Flat;
            _target.BackColor = Theme.Card;
            _target.ForeColor = Theme.Text;
            _target.Font = _fBody;
            _target.DrawMode = DrawMode.OwnerDrawFixed;
            _target.ItemHeight = 22;
            _target.DrawItem += ComboDrawItem;
            Controls.Add(_target);

            _add = Btn("Add rule", true);
            _add.Click += delegate { AddRule(); };
            Controls.Add(_add);

            _del = Btn("Remove", false);
            _del.Click += delegate { RemoveRule(); };
            Controls.Add(_del);

            // ---- actions ----
            _save = Btn("Save", false);
            _save.Click += delegate
            {
                if (_cfg.Save()) Toast("Saved to " + AppConfig.FilePath);
                else Toast("Could not write " + AppConfig.FilePath);
            };
            Controls.Add(_save);

            LoadTargetsIntoUi();
            RefreshRules();
            SyncStatus(true);
            LayoutUi();
        }

        /// <summary>
        /// Places everything for the current window size: the cards follow the
        /// width, and the preview card is given exactly the height the menu needs
        /// so it can be shown at life size.
        /// </summary>
        void LayoutUi()
        {
            if (_save == null) return;   // still building

            int w = ClientSize.Width - Pad * 2;
            if (w < 420) w = 420;
            _cardW = w;

            int previewH = _preview.PickerSize.Height;
            if (previewH < 120) previewH = 120;
            if (previewH > 460) previewH = 460;   // a very long list stops growing

            // The status shares the title row rather than sitting under it.
            int y = 22;

            _yStatus = y;
            _defaultBtn.SetBounds(Pad + w - 96, _yStatus + 7, 96, 32);
            // Once a release is found the bar below carries the action instead.
            _checkBtn.SetBounds(Pad + w - 96 - 134, _yStatus + 7, 134, 32);
            _checkBtn.Visible = _update == null;
            y += 46 + 18;

            if (_update != null)
            {
                _yUpdate = y;
                _updateBtn.SetBounds(Pad + w - 136, _yUpdate + 7, 120, 32);
                y += 46 + 18;
            }

            _yPicker = y;
            y += 22;
            int acardH = 112 + previewH + 24;
            _acard.SetBounds(Pad, y, w, acardH);
            _layoutRow.SetBounds(14, 14, 210, 22);
            _showLabels.SetBounds(238, 14, 210, 22);
            _showBrowser.SetBounds(462, 14, 200, 22);
            _showHost.SetBounds(14, 44, 210, 22);
            _showPrivate.SetBounds(238, 44, 210, 22);

            _sizeLabel.SetBounds(14, 77, 30, 22);
            for (int i = 0; i < _sizeBtns.Length; i++)
                _sizeBtns[i].SetBounds(48 + i * 40, 76, 36, 24);

            _preview.SetBounds(14, 112, w - 28, previewH);
            _dragHint.SetBounds(14, 112 + previewH + 2, w - 28, 18);
            y += acardH + 20;

            _yRules = y;
            y += 22;
            _rcard.SetBounds(Pad, y, w, 160);
            _ruleList.SetBounds(6, 6, w - 12, 148);
            y += 160 + 14;

            _pbox.SetBounds(Pad, y, 210, 34);
            _target.SetBounds(Pad + 220, y + 3, 268, 28);
            _del.SetBounds(Pad + w - 78, y, 78, 34);
            _add.SetBounds(Pad + w - 170, y, 84, 34);
            y += 34 + 46;

            _save.SetBounds(Pad + w - 100, y, 100, 34);
            // Room under the last row, so the buttons are not flush with the frame.
            // The hint rides the Save line: alone underneath it read as a stray
            // caption belonging to nothing.
            _yFooter = y + 8;
            y += 34 + 18;

            _contentH = y;
            Invalidate();
        }

        protected override void OnClientSizeChanged(EventArgs e)
        {
            base.OnClientSizeChanged(e);
            LayoutUi();
        }

        FlatButton Btn(string text, bool primary)
        {
            FlatButton b = new FlatButton();
            b.Text = text;
            b.Primary = primary;
            b.Font = _fBody;
            b.Size = new Size(100, 34);
            return b;
        }

        // ---------- ordering ----------

        void SizeClicked(object sender, EventArgs e)
        {
            FlatButton b = sender as FlatButton;
            if (b == null || !(b.Tag is int)) return;

            _cfg.PickerScale = SizeSteps[(int)b.Tag];
            _cfg.Save();
            SyncSizeButtons();
            _preview.Bind(_targets, _cfg);
            LayoutUi();
        }

        void SyncSizeButtons()
        {
            // Always exactly one lit: whatever is in the config, including a value
            // left behind by an older set of steps, belongs to its nearest button.
            int near = 0;
            for (int i = 1; i < SizeSteps.Length; i++)
            {
                if (Math.Abs(_cfg.PickerScale - SizeSteps[i])
                    < Math.Abs(_cfg.PickerScale - SizeSteps[near])) near = i;
            }

            // And the lit button has to be the truth: a leftover value between two
            // steps would light one while the menu drew as another.
            if (Math.Abs(_cfg.PickerScale - SizeSteps[near]) > 0.001d)
            {
                _cfg.PickerScale = SizeSteps[near];
                _cfg.Save();
            }

            for (int i = 0; i < _sizeBtns.Length; i++)
            {
                _sizeBtns[i].Primary = (i == near);
                _sizeBtns[i].Invalidate();
            }
        }

        /// <summary>Persists the order after a logo has been dragged in the preview.</summary>
        void SaveOrder()
        {
            _cfg.Order = new List<string>();
            foreach (Target t in _targets) _cfg.Order.Add(t.Id);
            _cfg.Save();

            LoadComboOnly();
        }

        // ---------- live updates ----------

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

        void Rescan()
        {
            List<Target> found;
            try { found = _cfg.Arrange(BrowserScanner.Scan()); }
            catch { return; }

            if (SameTargets(_targets, found)) return;   // don't churn the UI for nothing

            _targets = found;
            LoadTargetsIntoUi();
            RefreshRules();
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

        void LoadTargetsIntoUi()
        {
            if (_preview != null) { _preview.Bind(_targets, _cfg); LayoutUi(); }
            LoadComboOnly();
        }

        void LoadComboOnly()
        {
            string keepId = null;
            Target cur = _target.SelectedItem as Target;
            if (cur != null) keepId = cur.Id;

            _target.BeginUpdate();
            _target.Items.Clear();
            foreach (Target t in _targets) _target.Items.Add(t);
            _target.EndUpdate();

            int pick = 0;
            if (keepId != null)
            {
                for (int i = 0; i < _targets.Count; i++)
                    if (_targets[i].Id == keepId) { pick = i; break; }
            }
            if (_target.Items.Count > 0) _target.SelectedIndex = pick;
        }

        void SyncStatus(bool force)
        {
            bool isDef = Registration.IsDefaultBrowser();
            bool reg = Registration.IsRegistered();
            if (!force && isDef == _isDefault && reg == _registered) return;

            _isDefault = isDef;
            _registered = reg;

            // One word: the state is the verb, and there is no room for a sentence.
            _defaultBtn.Text = _isDefault
                ? "Change" : (_registered ? "Default" : "Setup");
            _defaultBtn.Invalidate();
            Invalidate();
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            SyncStatus(false);   // catches a default set in Windows Settings while we were away
        }

        // ---------- updates ----------

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            // Only once per window, and only after there is a handle to marshal back to.
            if (_checked) return;
            _checked = true;
            CheckForUpdates(false);
        }

        void CheckForUpdates(bool asked)
        {
            if (_updating || _update != null) return;
            if (asked)
            {
                // A click during the check made on opening joins that one.
                _reportCheck = true;
                _checkNote.Stop();
                _checkBtn.Text = "Checking…";
                _checkBtn.Invalidate();
            }
            if (_checking) return;

            _checking = true;
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
            bool report = _reportCheck;
            _reportCheck = false;
            _checkBtn.Text = CheckLabel;

            if (r != null)
            {
                _update = r;
                _updateText = "Picky " + r.Tag + " is available.";
                _updateBtn.Visible = true;
                LayoutUi();
                return;
            }

            // The check made on opening stays quiet; one that was asked for answers.
            if (report)
            {
                _checkBtn.Text = err == null
                    ? "Up to date · " + Updater.CurrentLabel
                    : "Could not check";
                _checkNote.Start();
            }
            _checkBtn.Invalidate();
        }

        void StartUpdate()
        {
            if (_update == null || _updating) return;

            _updating = true;
            _updateBtn.Enabled = false;
            _updateText = "Downloading Picky " + _update.Tag + "...";
            Invalidate();

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
            _updateBtn.Enabled = true;
            _updateText = "Update failed: " + (err == null ? "no download" : err);
            Invalidate();
        }

        // ---------- painting ----------

        void ComboDrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            Graphics g = e.Graphics;
            bool sel = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            using (SolidBrush b = new SolidBrush(sel ? Theme.CardHi : Theme.Card))
                g.FillRectangle(b, e.Bounds);

            Target t = _target.Items[e.Index] as Target;
            string s = t == null ? "" : t.ProfileLabel + "  \u2014  " + t.BrowserName;
            TextRenderer.DrawText(g, s, _fBody,
                new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 12, e.Bounds.Height),
                Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            // Painted chrome has to travel with the controls when the form scrolls.
            g.TranslateTransform(AutoScrollPosition.X, AutoScrollPosition.Y);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            if (_appIcon != null)
            {
                try
                {
                    using (Bitmap bmp = _appIcon.ToBitmap())
                        g.DrawImage(bmp, new Rectangle(Pad, _yStatus + 5, 36, 36));
                }
                catch { }
            }

            // Title on the left, bare words of action on the right. The status
            // used to be a sentence in a filled pill, which was a lot of furniture
            // for something the button already says.
            int textW = _cardW - 48 - 110 - (_checkBtn.Visible ? 134 : 0);

            TextRenderer.DrawText(g, "Picky", _fTitle,
                new Rectangle(Pad + 48, _yStatus + 1, textW, 24), Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, "Choose which browser opens each link", _fSub,
                new Rectangle(Pad + 48, _yStatus + 24, textW, 20), Theme.Dim,
                TextFormatFlags.Left | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

            if (_update != null)
            {
                RectangleF bar = new RectangleF(Pad, _yUpdate, _cardW, 46);
                using (GraphicsPath p = Theme.Rounded(bar, 10f))
                using (SolidBrush b = new SolidBrush(Theme.Card))
                    g.FillPath(b, p);

                using (SolidBrush b = new SolidBrush(Theme.Accent))
                    g.FillEllipse(b, Pad + 18, _yUpdate + 19, 9, 9);

                TextRenderer.DrawText(g, _updateText, _fBody,
                    new Rectangle(Pad + 38, _yUpdate, _cardW - 176, 46), Theme.Text,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter
                    | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }

            Section(g, "PICKER  \u00B7  HOW A LINK OPENS", _yPicker);
            Section(g, "RULES  \u00B7  HOST PATTERN TO TARGET, FIRST MATCH WINS", _yRules);

            TextRenderer.DrawText(g, "Hold Shift while clicking a link to force the picker.", _fSub,
                new Rectangle(Pad, _yFooter, _cardW - 120, 20), Theme.Dimmer,
                TextFormatFlags.Left | TextFormatFlags.NoPrefix);
        }

        void Section(Graphics g, string caption, int y)
        {
            TextRenderer.DrawText(g, caption, _fSection,
                new Rectangle(Pad + 2, y, _cardW, 18), Theme.Dimmer,
                TextFormatFlags.Left | TextFormatFlags.NoPrefix);
        }

        void Toast(string msg)
        {
            MessageBox.Show(this, msg, "Picky", MessageBoxButtons.OK, MessageBoxIcon.None);
        }

        // ---------- rules ----------

        void RefreshRules()
        {
            _ruleList.BeginUpdate();
            _ruleList.Items.Clear();
            foreach (Rule r in _cfg.Rules)
            {
                Target t = _targets.Find(delegate(Target x) { return x.Id == r.TargetId; });
                RuleRow row = new RuleRow();
                row.Pattern = r.Pattern;
                if (t != null)
                {
                    row.TargetLabel = t.ProfileLabel + "  \u2014  " + t.BrowserName;
                    row.Image = t.Image;
                }
                else
                {
                    row.TargetLabel = r.TargetId + "   (no longer installed)";
                }
                _ruleList.Items.Add(row);
            }
            _ruleList.EndUpdate();
        }

        void AddRule()
        {
            string p = _pattern.Text.Trim();
            if (p.Length == 0) return;
            Target t = _target.SelectedItem as Target;
            if (t == null) return;

            Rule r = new Rule();
            r.Pattern = p;
            r.TargetId = t.Id;
            _cfg.Rules.Add(r);
            _cfg.Save();
            RefreshRules();
        }

        void RemoveRule()
        {
            int i = _ruleList.SelectedIndex;
            if (i < 0 || i >= _cfg.Rules.Count) return;
            _cfg.Rules.RemoveAt(i);
            _cfg.Save();
            RefreshRules();
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
                if (_rescan != null) _rescan.Dispose();
                if (_poll != null) _poll.Dispose();
                if (_checkNote != null) _checkNote.Dispose();
                if (_fTitle != null) _fTitle.Dispose();
                if (_fSub != null) _fSub.Dispose();
                if (_fSection != null) _fSection.Dispose();
                if (_fBody != null) _fBody.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
