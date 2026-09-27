using System;
using System.Drawing;
using System.Windows.Forms;

namespace Picky
{
    /// <summary>
    /// Where Picky stands with Windows: not set up, set up but not the default,
    /// or handling every link. The row says which, and its button is the next
    /// step from there.
    /// </summary>
    public class GeneralPage : Page
    {
        readonly SettingsForm _form;
        ScrollPage _scroll;
        TextLine _hDefault, _hint;
        RowCard _default;
        FlatButton _button;
        bool _registered;

        const int MaxColumn = 860;

        public GeneralPage(SettingsForm form)
        {
            _form = form;
            Title = "General";
            Subtitle = "Choose which browser opens each link.";

            _scroll = new ScrollPage();
            Controls.Add(_scroll);
            Panel c = _scroll.Content;

            _hDefault = Section("Default browser", Theme.Back);
            c.Controls.Add(_hDefault);

            _button = new FlatButton();
            _button.Font = Theme.Font(9f, FontStyle.Regular);
            _button.Height = 32;
            _button.Backdrop = Theme.Card;
            _button.Click += delegate
            {
                // The only way back if the association was ever undone outside
                // the installer, so it stays here rather than only in setup.
                if (!_registered) { Registration.Register(); _form.SyncStatus(true); return; }
                Registration.OpenDefaultAppsSettings();
            };

            _default = new RowCard();
            _default.Fill = Theme.Card;
            _default.Page = Theme.Back;
            _default.Add("", "", _button);
            c.Controls.Add(_default);

            _hint = Hint("Hold Shift while clicking a link to always show the picker, even when a rule matches.",
                Theme.Back);
            c.Controls.Add(_hint);
        }

        /// <summary>Put the registration state into the row and its button.</summary>
        public void ShowStatus(bool registered, bool isDefault)
        {
            _registered = registered;
            string title, detail;
            if (!registered)
            {
                title = "Picky is not set up";
                detail = "Set it up so Windows can offer it as a browser.";
                _button.Text = "Set up";
                _button.Primary = true;
            }
            else if (!isDefault)
            {
                title = "Picky is not your default browser";
                detail = "Choose Picky for HTTP and HTTPS in Windows Settings, and every link opens here first.";
                _button.Text = "Open Settings";
                _button.Primary = true;
            }
            else
            {
                title = "Picky is your default browser";
                detail = "Every link you click opens here first, then in the browser you pick.";
                _button.Text = "Change";
                _button.Primary = false;
            }
            _button.Width = _button.PreferredWidth();
            _button.Invalidate();
            _default.SetText(0, title, detail);
            Arrange();
        }

        public override void Arrange()
        {
            if (_hint == null) return;
            _scroll.SetBounds(Inset, ContentTop, Width - Inset, Height - ContentTop);
            int w = Math.Max(1, Math.Min(_scroll.Width - Inset, MaxColumn));

            int y = 4;
            _hDefault.SetBounds(2, y, w, 18);
            y += 26;
            _default.Location = new Point(0, y);
            y += _default.Arrange(w) + 12;

            _hint.SetBounds(2, y, w - 2, 1);
            _hint.Height = _hint.Measure();
            y += _hint.Height;

            _scroll.Content.Height = y + 24;
            _scroll.Sync();
        }
    }
}
