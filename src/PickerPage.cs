using System;
using System.Drawing;
using System.Windows.Forms;

namespace Picky
{
    /// <summary>
    /// How the menu looks. The real picker is drawn at the top, live, so no
    /// switch below it is a guess about what a link will actually pop up; its
    /// logos can be dragged to set the order the menu lists them in.
    /// </summary>
    public class PickerPage : Page
    {
        /// <summary>The sizes the picker can be drawn at, smallest first.</summary>
        static readonly double[] SizeSteps =
            new double[] { 0.8d, 1.0d, 1.25d, 1.5d, 1.8d };
        static readonly string[] SizeNames =
            new string[] { "S", "M", "L", "XL", "XXL" };

        readonly SettingsForm _form;
        ScrollPage _scroll;
        Card _previewCard;
        PickerPreview _preview;
        TextLine _hLayout, _hShow;
        RowCard _layout, _show;
        ToggleChip _row, _labels, _browser, _host, _private;
        Segmented _size;

        const int MaxColumn = 860;
        const int PreviewPad = 20;

        delegate void Setter(bool on);

        public PickerPage(SettingsForm form)
        {
            _form = form;
            Title = "Picker";
            Subtitle = "How the menu looks when a link opens. Drag a logo to change the order.";

            AppConfig cfg = _form.Config;

            _scroll = new ScrollPage();
            Controls.Add(_scroll);
            Panel c = _scroll.Content;

            _previewCard = new Card();
            _previewCard.Fill = Theme.Card;
            _previewCard.Page = Theme.Back;
            c.Controls.Add(_previewCard);

            _preview = new PickerPreview();
            _preview.Font = Theme.Font(9f, FontStyle.Regular);
            _preview.Reordered += delegate { _form.SaveOrder(); };
            _previewCard.Controls.Add(_preview);

            _hLayout = Section("Layout", Theme.Back);
            c.Controls.Add(_hLayout);
            _layout = NewCard(c);

            _row = Switch(cfg.IsRowLayout(), delegate(bool on)
            {
                cfg.PickerLayout = on ? AppConfig.LayoutRow : AppConfig.LayoutColumn;
                Changed();
            });
            _layout.Add("Lay the logos out in a row", "A strip of logos instead of a list.", _row);

            _size = new Segmented();
            _size.Font = Theme.Font(9f, FontStyle.Regular);
            _size.Items.AddRange(SizeNames);
            _size.Size = new Size(_size.PreferredWidth(), 30);
            _size.SelectedIndexChanged += delegate
            {
                cfg.PickerScale = SizeSteps[_size.SelectedIndex];
                Changed();
            };
            _layout.Add("Size", "How big the menu is.", _size);

            _hShow = Section("Show", Theme.Back);
            c.Controls.Add(_hShow);
            _show = NewCard(c);

            _labels = Switch(cfg.ShowLabels, delegate(bool on) { cfg.ShowLabels = on; Changed(); });
            _show.Add("Profile names", "", _labels);

            _browser = Switch(cfg.ShowBrowserName, delegate(bool on) { cfg.ShowBrowserName = on; Changed(); });
            _show.Add("Browser names", "", _browser);

            _host = Switch(cfg.ShowHost, delegate(bool on) { cfg.ShowHost = on; Changed(); });
            _show.Add("The link address", "The site the link goes to, above the choices.", _host);

            // What this really decides is which entries exist, so the browser
            // list is read again, and the rules and targets follow it.
            _private = Switch(cfg.ShowPrivate, delegate(bool on)
            {
                cfg.ShowPrivate = on;
                _form.SaveConfig();
                _form.ReloadTargets();
            });
            _show.Add("Private and incognito windows", "An InPrivate or Incognito choice for each browser.", _private);

            SyncSize();
            Bind();
        }

        RowCard NewCard(Panel parent)
        {
            RowCard r = new RowCard();
            r.Fill = Theme.Card;
            r.Page = Theme.Back;
            parent.Controls.Add(r);
            return r;
        }

        ToggleChip Switch(bool value, Setter apply)
        {
            ToggleChip t = new ToggleChip();
            t.Size = new Size(40, 24);
            t.SetQuiet(value);
            t.CheckedChanged += delegate { apply(t.Checked); };
            return t;
        }

        /// <summary>An option changed: write it, and redraw the preview from it.</summary>
        void Changed()
        {
            _form.SaveConfig();
            Bind();
        }

        /// <summary>
        /// Always exactly one size lit: whatever is in the config, including a
        /// value left behind by an older set of steps, belongs to its nearest
        /// step - and is saved as that step, so the lit choice is the truth.
        /// </summary>
        void SyncSize()
        {
            AppConfig cfg = _form.Config;
            int near = 0;
            for (int i = 1; i < SizeSteps.Length; i++)
            {
                if (Math.Abs(cfg.PickerScale - SizeSteps[i])
                    < Math.Abs(cfg.PickerScale - SizeSteps[near])) near = i;
            }

            if (Math.Abs(cfg.PickerScale - SizeSteps[near]) > 0.001d)
            {
                cfg.PickerScale = SizeSteps[near];
                _form.SaveConfig();
            }
            _size.SetIndexQuiet(near);
        }

        /// <summary>Redraw the preview from the current browsers and options, and fit its card to it.</summary>
        public void Bind()
        {
            _preview.Bind(_form.Targets, _form.Config);
            Arrange();
        }

        public override void Arrange()
        {
            if (_show == null) return;
            _scroll.SetBounds(Inset, ContentTop, Width - Inset, Height - ContentTop);
            int w = Math.Max(1, Math.Min(_scroll.Width - Inset, MaxColumn));

            // Life size whenever it fits; a very long list stops growing and is
            // shown smaller instead.
            int ph = _preview.PickerSize.Height;
            if (ph < 120) ph = 120;
            if (ph > 460) ph = 460;

            int y = 0;
            _previewCard.SetBounds(0, y, w, ph + PreviewPad * 2);
            _preview.SetBounds(PreviewPad, PreviewPad, Math.Max(1, w - PreviewPad * 2), ph);
            y += _previewCard.Height;

            foreach (object[] pair in new object[][] {
                         new object[] { _hLayout, _layout },
                         new object[] { _hShow, _show } })
            {
                TextLine head = (TextLine)pair[0];
                RowCard card = (RowCard)pair[1];
                y += 22;
                head.SetBounds(2, y, w, 18);
                y += 26;
                card.Location = new Point(0, y);
                y += card.Arrange(w);
            }

            _scroll.Content.Height = y + 24;
            _scroll.Sync();
        }
    }
}
