using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace Picky
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // With Picky already running, a click only has to pass the link on.
            // That path is mscorlib and user32 alone - loading WinForms and GDI+
            // was most of what a click used to wait for.
            if (args.Length == 1 && IsAllowedUrl(args[0]) && Handoff.Send(args[0])) return;

            try
            {
                Run(args);
            }
            catch (Exception ex)
            {
                LogError(ex);
                throw;
            }
        }

        internal static void LogError(Exception ex)
        {
            try
            {
                if (!System.IO.Directory.Exists(AppConfig.Dir))
                    System.IO.Directory.CreateDirectory(AppConfig.Dir);
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(AppConfig.Dir, "error.log"),
                    DateTime.Now.ToString("s") + "  " + ex.ToString() + Environment.NewLine);
            }
            catch { }
        }

        static void Run(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // An update leaves the previous build renamed beside this one.
            Updater.CleanupOldBuild();

            // --keep pins the picker open instead of dismissing it on focus loss,
            // so it can be inspected without a real link click.
            bool background = false;
            List<string> argv = new List<string>();
            foreach (string a in args)
            {
                if (string.Equals(a, "--keep", StringComparison.OrdinalIgnoreCase))
                    PickerForm.AutoClose = false;
                else if (string.Equals(a, "--background", StringComparison.OrdinalIgnoreCase))
                    background = true;
                else if (string.Equals(a, "--demo", StringComparison.OrdinalIgnoreCase))
                {
                    // Placeholder profiles and sample rules, for documentation shots.
                    BrowserScanner.DemoMode = true;
                    AppConfig.DemoMode = true;
                }
                else
                    argv.Add(a);
            }

            // Started with Windows: become the running copy, with nothing to show yet.
            if (background)
            {
                if (Resident.Claim())
                {
                    Resident.Warm();
                    Resident.Run();
                }
                return;
            }

            if (argv.Count == 0)
            {
                // Opening Picky gets the next link ready too: an install that
                // predates the startup entry, or a copy that was stopped, would
                // otherwise leave the first click to start from cold.
                if (!AppConfig.DemoMode)
                {
                    Registration.EnsureStartup();
                    Resident.EnsureRunning();
                }
                Application.Run(new SettingsForm());
                return;
            }

            string a0 = argv[0];

            if (string.Equals(a0, "--register", StringComparison.OrdinalIgnoreCase))
            {
                Registration.Register();
                Registration.OpenDefaultAppsSettings();
                return;
            }

            if (string.Equals(a0, "--unregister", StringComparison.OrdinalIgnoreCase))
            {
                Registration.Unregister();
                return;
            }

            if (!IsAllowedUrl(a0)) return;

            bool forcePicker = (Control.ModifierKeys & Keys.Shift) == Keys.Shift;

            // The first click stays on as the running copy, so the next one is a
            // hand-off. --keep and --demo inspect a single picker and must not
            // leave a stray instance behind to serve real clicks.
            bool oneOff = !PickerForm.AutoClose || AppConfig.DemoMode;
            if (!oneOff && Resident.Claim())
            {
                Resident.Open(a0, forcePicker);
                Resident.Run();
                return;
            }

            PickerForm picker = Route(a0, forcePicker);
            if (picker != null) Application.Run(picker);
        }

        internal static bool IsAllowedUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("file:///", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Opens the link straight away when a rule covers it. Otherwise returns
        /// the picker to show, or null when there is nothing to show.
        /// </summary>
        internal static PickerForm Route(string url, bool forcePicker)
        {
            // Read fresh on every link: the running copy outlives any number of
            // settings changes and new browser profiles.
            AppConfig cfg = AppConfig.Load();
            List<Target> targets = cfg.Arrange(BrowserScanner.Scan());
            if (targets.Count == 0)
            {
                MessageBox.Show("Picky found no installed browsers.", "Picky");
                return null;
            }

            if (!forcePicker)
            {
                string targetId = cfg.MatchRule(url);
                if (!string.IsNullOrEmpty(targetId))
                {
                    Target match = targets.Find(delegate(Target x) { return x.Id == targetId; });
                    if (match != null)
                    {
                        Launcher.Launch(match, url);
                        return null;
                    }
                    // A rule pointing at a browser/profile that no longer exists
                    // falls through to the picker rather than silently doing nothing.
                }
            }

            return new PickerForm(targets, url, cfg);
        }
    }
}
