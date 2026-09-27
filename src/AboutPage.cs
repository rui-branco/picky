using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace Picky
{
    /// <summary>
    /// The version and whether a newer one is out, with the one action that
    /// matters about it; then what Picky is for, and where to look for more.
    /// </summary>
    public class AboutPage : Page
    {
        readonly SettingsForm _form;
        ScrollPage _scroll;
        RowCard _updateCard;
        FlatButton _updateButton;
        TextLine _whatHead, _whatText, _linksHead;
        Card _whatCard;
        FlatButton _repo;

        const int MaxColumn = 760;
        const string Repo = "rui-branco/picky";

        public AboutPage(SettingsForm form)
        {
            _form = form;
            Title = "About";
            Subtitle = "Picky " + Updater.CurrentLabel + "  ·  MIT licensed";

            _scroll = new ScrollPage();
            Controls.Add(_scroll);
            Panel content = _scroll.Content;

            _updateButton = new FlatButton();
            _updateButton.Height = 32;
            _updateButton.Font = Theme.Font(9f, FontStyle.Regular);
            _updateButton.Backdrop = Theme.Card;
            _updateButton.Text = "Check for updates";
            _updateButton.Width = _updateButton.PreferredWidth();
            _updateButton.Click += delegate { _form.UpdateClicked(); };

            _updateCard = new RowCard();
            _updateCard.Fill = Theme.Card;
            _updateCard.Page = Theme.Back;
            _updateCard.Add("Updates", "", _updateButton);
            content.Controls.Add(_updateCard);

            _whatHead = Section("What it does", Theme.Back);
            content.Controls.Add(_whatHead);

            _whatCard = new Card();
            _whatCard.Fill = Theme.Card;
            _whatCard.Page = Theme.Back;
            content.Controls.Add(_whatCard);

            _whatText = Hint("Windows lets you pick one default browser. Picky replaces that single choice with a "
                           + "decision made per link: it registers itself as a browser, so Windows hands it every "
                           + "link you click, and it either routes the link silently using a rule or shows a picker "
                           + "at your cursor. Click a work link, it opens in your work profile. Click anything else, "
                           + "you choose.", Theme.Card);
            _whatCard.Controls.Add(_whatText);

            _linksHead = Section("Links", Theme.Back);
            content.Controls.Add(_linksHead);

            _repo = new FlatButton();
            _repo.Ghost = true;
            _repo.Text = "github.com/" + Repo;
            _repo.Height = 26;
            _repo.BackColor = Theme.Back;
            _repo.Backdrop = Theme.Back;
            _repo.Width = _repo.PreferredWidth();
            _repo.Click += delegate
            {
                try { Process.Start("https://github.com/" + Repo); }
                catch { }
            };
            content.Controls.Add(_repo);
        }

        /// <summary>Put the update state into the row and its button.</summary>
        public void ShowUpdate(string detail, string button, bool primary, bool enabled)
        {
            _updateButton.Text = button;
            _updateButton.Primary = primary;
            _updateButton.Enabled = enabled;
            _updateButton.Width = _updateButton.PreferredWidth();
            _updateButton.Invalidate();
            _updateCard.SetText(0, "Updates", detail);
            Arrange();
        }

        public override void Arrange()
        {
            if (_repo == null) return;
            _scroll.SetBounds(Inset, ContentTop, Width - Inset, Height - ContentTop);
            int col = Math.Max(1, Math.Min(_scroll.Width - Inset, MaxColumn));
            int y = 0;

            _updateCard.Location = new Point(0, y);
            y += _updateCard.Arrange(col);

            y = PlaceSection(_whatHead, y, col);
            _whatText.SetBounds(18, 14, Math.Max(1, col - 36), 1);
            _whatText.Height = _whatText.Measure();
            _whatCard.SetBounds(0, y, col, _whatText.Height + 28);
            y += _whatCard.Height;

            y = PlaceSection(_linksHead, y, col);
            _repo.SetBounds(2, y, _repo.PreferredWidth(), 26);
            y += 26;

            _scroll.Content.Height = y + 24;
            _scroll.Sync();
        }

        /// <summary>A section heading under what came before; returns where its content starts.</summary>
        static int PlaceSection(TextLine head, int y, int col)
        {
            y += 22;
            head.SetBounds(2, y, col, 18);
            return y + 18 + 8;
        }
    }
}
