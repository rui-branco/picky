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

        RowList _targetList;
        RowList _ruleList;
        TextBox _pattern;
        ComboBox _target;
        FlatButton _defaultBtn;
        DarkCheck _showPrivate;

        bool _isDefault;
        bool _registered;
        Icon _appIcon;

        Font _fTitle, _fSub, _fSection, _fBody;

        List<FileSystemWatcher> _watchers = new List<FileSystemWatcher>();
        Timer _rescan;   // debounces Local State churn
        Timer _poll;     // notices the default handler changing outside this app

        const int Pad = 26;
        const int CardW = 668;
        const int RowsVisible = 5;
        const int BrowsersCardH = RowsVisible * 46 + 12;

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
            ClientSize = new Size(CardW + Pad * 2, 712 + (BrowsersCardH - 194));
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Back;
            ForeColor = Theme.Text;
            Font = _fBody;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            DoubleBuffered = true;

            BuildUi();
            StartWatching();
        }

        int _yStatus, _yBrowsers, _yRules;

        void BuildUi()
        {
            int y = 96;

            // ---- status pill (painted) with its action button ----
            _yStatus = y;

            _defaultBtn = new FlatButton();
            _defaultBtn.Backdrop = Theme.Card;
            _defaultBtn.Font = _fBody;
            _defaultBtn.SetBounds(Pad + CardW - 152, _yStatus + 7, 136, 32);
            _defaultBtn.Click += delegate { Registration.OpenDefaultAppsSettings(); };
            Controls.Add(_defaultBtn);

            y += 46 + 18;

            // ---- browsers ----
            _yBrowsers = y;
            y += 22;

            _showPrivate = new DarkCheck();
            _showPrivate.Text = "Show private windows";
            _showPrivate.Font = _fBody;
            _showPrivate.Checked = _cfg.ShowPrivate;
            _showPrivate.SetBounds(Pad + CardW - 168, _yBrowsers - 3, 168, 22);
            _showPrivate.CheckedChanged += delegate
            {
                _cfg.ShowPrivate = _showPrivate.Checked;
                _cfg.Save();
                _targets = _cfg.Arrange(BrowserScanner.Scan());
                LoadTargetsIntoUi();
                RefreshRules();
            };
            Controls.Add(_showPrivate);

            Card bcard = new Card();
            bcard.SetBounds(Pad, y, CardW, BrowsersCardH);
            Controls.Add(bcard);

            _targetList = new RowList();
            _targetList.AllowReorder = true;
            _targetList.SetBounds(6, 6, CardW - 12, BrowsersCardH - 12);
            _targetList.Reordered += delegate { SaveOrderFromList(); };
            bcard.Controls.Add(_targetList);
            y += BrowsersCardH + 20;

            // ---- rules ----
            _yRules = y;
            y += 22;

            Card rcard = new Card();
            rcard.SetBounds(Pad, y, CardW, 160);
            Controls.Add(rcard);

            _ruleList = new RowList();
            _ruleList.SetBounds(6, 6, CardW - 12, 148);
            rcard.Controls.Add(_ruleList);
            y += 160 + 14;

            // ---- add-rule row ----
            Card pbox = new Card();
            pbox.Radius = 8;
            pbox.SetBounds(Pad, y, 210, 34);
            Controls.Add(pbox);

            _pattern = new TextBox();
            _pattern.BorderStyle = BorderStyle.None;
            _pattern.BackColor = Theme.Card;
            _pattern.ForeColor = Theme.Text;
            _pattern.Font = _fBody;
            _pattern.SetBounds(12, 9, 186, 18);
            _pattern.Text = "*.example.com";
            pbox.Controls.Add(_pattern);

            _target = new ComboBox();
            _target.DropDownStyle = ComboBoxStyle.DropDownList;
            _target.FlatStyle = FlatStyle.Flat;
            _target.BackColor = Theme.Card;
            _target.ForeColor = Theme.Text;
            _target.Font = _fBody;
            _target.DrawMode = DrawMode.OwnerDrawFixed;
            _target.ItemHeight = 22;
            _target.SetBounds(Pad + 220, y + 3, 268, 28);
            _target.DrawItem += ComboDrawItem;
            Controls.Add(_target);

            FlatButton add = Btn("Add rule", Pad + 498, y, 84, true);
            add.Click += delegate { AddRule(); };
            Controls.Add(add);

            FlatButton del = Btn("Remove", Pad + 590, y, 78, false);
            del.Click += delegate { RemoveRule(); };
            Controls.Add(del);
            y += 34 + 22;

            // ---- actions ----
            FlatButton reg = Btn("Register", Pad, y, 100, false);
            reg.Click += delegate { Registration.Register(); SyncStatus(true); };
            Controls.Add(reg);

            FlatButton unreg = Btn("Unregister", Pad + 108, y, 100, false);
            unreg.Click += delegate { Registration.Unregister(); SyncStatus(true); };
            Controls.Add(unreg);

            FlatButton save = Btn("Save", Pad + CardW - 100, y, 100, false);
            save.Click += delegate
            {
                if (_cfg.Save()) Toast("Saved to " + AppConfig.FilePath);
                else Toast("Could not write " + AppConfig.FilePath);
            };
            Controls.Add(save);

            LoadTargetsIntoUi();
            RefreshRules();
            SyncStatus(true);
        }

        FlatButton Btn(string text, int x, int y, int w, bool primary)
        {
            FlatButton b = new FlatButton();
            b.Text = text;
            b.Primary = primary;
            b.Font = _fBody;
            b.SetBounds(x, y, w, 34);
            return b;
        }

        // ---------- ordering ----------

        void SaveOrderFromList()
        {
            List<Target> ordered = new List<Target>();
            foreach (object o in _targetList.Items)
            {
                Target t = o as Target;
                if (t != null) ordered.Add(t);
            }
            _targets = ordered;

            _cfg.Order = new List<string>();
            foreach (Target t in ordered) _cfg.Order.Add(t.Id);
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
            _targetList.BeginUpdate();
            _targetList.Items.Clear();
            foreach (Target t in _targets) _targetList.Items.Add(t);
            _targetList.EndUpdate();
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

            _defaultBtn.Text = _isDefault ? "Change" : "Set as default";
            _defaultBtn.Primary = !_isDefault;
            _defaultBtn.Visible = _registered;
            _defaultBtn.Invalidate();
            Invalidate();
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            SyncStatus(false);   // catches a default set in Windows Settings while we were away
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
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            if (_appIcon != null)
            {
                try
                {
                    using (Bitmap bmp = _appIcon.ToBitmap())
                        g.DrawImage(bmp, new Rectangle(Pad, 24, 36, 36));
                }
                catch { }
            }
            TextRenderer.DrawText(g, "Picky", _fTitle,
                new Rectangle(Pad + 48, 22, 400, 24), Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(g, "Choose which browser opens each link", _fSub,
                new Rectangle(Pad + 48, 45, 500, 20), Theme.Dim,
                TextFormatFlags.Left | TextFormatFlags.NoPrefix);

            // ---- status pill ----
            RectangleF pill = new RectangleF(Pad, _yStatus, CardW, 46);
            using (GraphicsPath p = Theme.Rounded(pill, 10f))
            using (SolidBrush b = new SolidBrush(Theme.Card))
                g.FillPath(b, p);

            Color dot = _isDefault ? Theme.Good : (_registered ? Theme.Warn : Theme.Dimmer);
            using (SolidBrush b = new SolidBrush(dot))
                g.FillEllipse(b, Pad + 18, _yStatus + 19, 9, 9);

            string status;
            if (_isDefault) status = "Picky is catching your links.";
            else if (_registered) status = "Picky isn't catching your links yet.";
            else status = "Picky isn't set up yet - click Register below.";

            int reserve = _defaultBtn.Visible ? 176 : 46;
            TextRenderer.DrawText(g, status, _fBody,
                new Rectangle(Pad + 38, _yStatus, CardW - reserve, 46),
                _isDefault ? Theme.Text : Theme.Dim,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter
                | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            Section(g, "BROWSERS AND PROFILES  \u00B7  DRAG TO REORDER", _yBrowsers);
            Section(g, "RULES  \u00B7  HOST PATTERN TO TARGET, FIRST MATCH WINS", _yRules);

            TextRenderer.DrawText(g, "Hold Shift while clicking a link to force the picker.", _fSub,
                new Rectangle(Pad, ClientSize.Height - 30, CardW, 20), Theme.Dimmer,
                TextFormatFlags.Left | TextFormatFlags.NoPrefix);
        }

        void Section(Graphics g, string caption, int y)
        {
            TextRenderer.DrawText(g, caption, _fSection,
                new Rectangle(Pad + 2, y, CardW, 18), Theme.Dimmer,
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
                if (_fTitle != null) _fTitle.Dispose();
                if (_fSub != null) _fSub.Dispose();
                if (_fSection != null) _fSection.Dispose();
                if (_fBody != null) _fBody.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
