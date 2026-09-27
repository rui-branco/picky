using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Picky
{
    /// <summary>
    /// Host patterns that skip the picker. The list reads top to bottom in the
    /// order they are tried, and a row can be dragged to change that order or
    /// removed from the button its hover shows.
    /// </summary>
    public class RulesPage : Page
    {
        readonly SettingsForm _form;
        ScrollPage _scroll;
        Card _listCard, _addCard;
        RowList _list;
        TextLine _empty, _hAdd;
        TextField _pattern;
        DropChip _target;
        FlatButton _add;

        /// <summary>What the dropdown lists, in its order. A copy: dragging in the
        /// preview reorders the window's list in place.</summary>
        List<Target> _choices = new List<Target>();

        const int MaxColumn = 860;
        const int ListPad = 6;
        const int EmptyH = 52;
        const int AddPadX = 18, AddPadY = 14;

        public RulesPage(SettingsForm form)
        {
            _form = form;
            Title = "Rules";
            Subtitle = "A link whose host matches a pattern skips the picker and opens straight in its browser. "
                     + "The first match wins.";

            _scroll = new ScrollPage();
            Controls.Add(_scroll);
            Panel c = _scroll.Content;

            _listCard = new Card();
            _listCard.Fill = Theme.Card;
            _listCard.Page = Theme.Back;
            c.Controls.Add(_listCard);

            _list = new RowList();
            _list.AllowReorder = true;
            _list.ShowRemove = true;
            _list.Reordered += delegate { SaveRuleOrder(); };
            _list.RemoveClicked += delegate(int i) { RemoveRule(i); };
            _listCard.Controls.Add(_list);

            _empty = new TextLine();
            _empty.Text = "No rules yet. Add one below.";
            _empty.Font = Theme.Font(9f, FontStyle.Regular);
            _empty.Ink = Theme.Dim;
            _empty.BackColor = Theme.Card;
            _listCard.Controls.Add(_empty);

            _hAdd = Section("Add a rule", Theme.Back);
            c.Controls.Add(_hAdd);

            _addCard = new Card();
            _addCard.Fill = Theme.Card;
            _addCard.Page = Theme.Back;
            c.Controls.Add(_addCard);

            _pattern = new TextField();
            _pattern.Font = Theme.Font(9f, FontStyle.Regular);
            _pattern.Placeholder = "*.example.com";
            _pattern.Box.TextChanged += delegate { SyncAdd(); };
            _pattern.Box.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode != Keys.Enter) return;
                e.Handled = true;
                e.SuppressKeyPress = true;   // no ding from a single-line box
                AddRule();
            };
            _addCard.Controls.Add(_pattern);

            _target = new DropChip();
            _target.Font = Theme.Font(9f, FontStyle.Regular);
            _target.SelectionChanged += delegate { SyncAdd(); };
            _addCard.Controls.Add(_target);

            _add = new FlatButton();
            _add.Text = "Add rule";
            _add.Primary = true;
            _add.Font = Theme.Font(9f, FontStyle.Regular);
            _add.Height = 32;
            _add.Backdrop = Theme.Card;
            _add.Click += delegate { AddRule(); };
            _addCard.Controls.Add(_add);

            LoadTargets();
            RefreshRules();
        }

        /// <summary>What a target is called here and in the rules list. A
        /// browser with a single profile (Firefox, or one registered with
        /// Windows) goes by its own name, said once.</summary>
        static string Label(Target t)
        {
            if (string.Equals(t.ProfileLabel, t.BrowserName, StringComparison.OrdinalIgnoreCase)) return t.BrowserName;
            return t.ProfileLabel + " · " + t.BrowserName;
        }

        /// <summary>Fill the dropdown from the window's browsers, keeping the choice where it still exists.</summary>
        public void LoadTargets()
        {
            string keepId = null;
            int cur = _target.SelectedIndex;
            if (cur >= 0 && cur < _choices.Count) keepId = _choices[cur].Id;

            _choices = new List<Target>(_form.Targets);
            _target.Clear();
            foreach (Target t in _choices)
            {
                _target.Items.Add(Label(t));
                _target.Images.Add(t.Image);
            }

            int pick = 0;
            if (keepId != null)
            {
                for (int i = 0; i < _choices.Count; i++)
                    if (_choices[i].Id == keepId) { pick = i; break; }
            }
            if (_choices.Count > 0) _target.SetIndexQuiet(pick);
            SyncAdd();
            Arrange();
        }

        /// <summary>Rebuild the list from the config, in the order the rules are tried.</summary>
        public void RefreshRules()
        {
            List<Target> targets = _form.Targets;
            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (Rule r in _form.Config.Rules)
            {
                Target t = targets.Find(delegate(Target x) { return x.Id == r.TargetId; });
                RuleRow row = new RuleRow();
                row.Rule = r;
                row.Pattern = r.Pattern;
                if (t != null)
                {
                    row.TargetLabel = Label(t);
                    row.Image = t.Image;
                }
                else
                {
                    row.TargetLabel = r.TargetId + "  (no longer installed)";
                }
                _list.Items.Add(row);
            }
            _list.EndUpdate();
            Arrange();
        }

        void SyncAdd()
        {
            int i = _target.SelectedIndex;
            _add.Enabled = _pattern.Box.Text.Trim().Length > 0 && i >= 0 && i < _choices.Count;
        }

        void AddRule()
        {
            string p = _pattern.Box.Text.Trim();
            if (p.Length == 0) return;
            int i = _target.SelectedIndex;
            if (i < 0 || i >= _choices.Count) return;

            Rule r = new Rule();
            r.Pattern = p;
            r.TargetId = _choices[i].Id;
            _form.Config.Rules.Add(r);
            _form.SaveConfig();

            _pattern.Box.Text = "";
            RefreshRules();
        }

        void RemoveRule(int index)
        {
            if (index < 0 || index >= _list.Items.Count) return;
            RuleRow row = _list.Items[index] as RuleRow;
            if (row == null || row.Rule == null) return;
            _form.Config.Rules.Remove(row.Rule);
            _form.SaveConfig();
            RefreshRules();
        }

        /// <summary>A row was dragged: the config takes the list's new order.</summary>
        void SaveRuleOrder()
        {
            List<Rule> ordered = new List<Rule>();
            foreach (object o in _list.Items)
            {
                RuleRow row = o as RuleRow;
                if (row != null && row.Rule != null) ordered.Add(row.Rule);
            }
            _form.Config.Rules = ordered;
            _form.SaveConfig();
        }

        public override void Arrange()
        {
            if (_add == null) return;
            _scroll.SetBounds(Inset, ContentTop, Width - Inset, Height - ContentTop);
            int w = Math.Max(1, Math.Min(_scroll.Width - Inset, MaxColumn));

            int y = 0;
            bool any = _list.Items.Count > 0;
            _list.Visible = any;
            _empty.Visible = !any;
            if (any)
            {
                // Tall enough for every row: the page scrolls, never the list.
                int listH = _list.Items.Count * _list.ItemHeight;
                _listCard.SetBounds(0, y, w, listH + ListPad * 2);
                _list.SetBounds(ListPad, ListPad, Math.Max(1, w - ListPad * 2), listH);
            }
            else
            {
                _listCard.SetBounds(0, y, w, EmptyH);
                _empty.SetBounds(AddPadX, 0, Math.Max(1, w - AddPadX * 2), EmptyH);
            }
            y += _listCard.Height;

            y += 22;
            _hAdd.SetBounds(2, y, w, 18);
            y += 26;

            int inner = w - AddPadX * 2;
            int aw = _add.PreferredWidth();
            int avail = Math.Max(1, inner - aw - 20);
            int dw = Math.Min(_target.PreferredWidth(), avail * 55 / 100);
            int pw = Math.Max(1, avail - dw);
            _addCard.SetBounds(0, y, w, 32 + AddPadY * 2);
            _pattern.SetBounds(AddPadX, AddPadY, pw, 32);
            _target.SetBounds(AddPadX + pw + 10, AddPadY, dw, 32);
            _add.SetBounds(w - AddPadX - aw, AddPadY, aw, 32);
            y += _addCard.Height;

            _scroll.Content.Height = y + 24;
            _scroll.Sync();
        }
    }
}
