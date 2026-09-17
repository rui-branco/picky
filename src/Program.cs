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

        static void LogError(Exception ex)
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

            // --keep pins the picker open instead of dismissing it on focus loss,
            // so it can be inspected without a real link click.
            List<string> argv = new List<string>();
            foreach (string a in args)
            {
                if (string.Equals(a, "--keep", StringComparison.OrdinalIgnoreCase))
                    PickerForm.AutoClose = false;
                else if (string.Equals(a, "--demo", StringComparison.OrdinalIgnoreCase))
                {
                    // Placeholder profiles and sample rules, for documentation shots.
                    BrowserScanner.DemoMode = true;
                    AppConfig.DemoMode = true;
                }
                else
                    argv.Add(a);
            }

            if (argv.Count == 0)
            {
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

            HandleUrl(a0);
        }

        static bool IsAllowedUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("file:///", StringComparison.OrdinalIgnoreCase);
        }

        static void HandleUrl(string url)
        {
            AppConfig cfg = AppConfig.Load();
            List<Target> targets = cfg.Arrange(BrowserScanner.Scan());
            if (targets.Count == 0)
            {
                MessageBox.Show("Picky found no installed browsers.", "Picky");
                return;
            }

            bool forcePicker = (Control.ModifierKeys & Keys.Shift) == Keys.Shift;

            if (!forcePicker)
            {
                string targetId = cfg.MatchRule(url);
                if (!string.IsNullOrEmpty(targetId))
                {
                    Target match = targets.Find(delegate(Target x) { return x.Id == targetId; });
                    if (match != null)
                    {
                        Launcher.Launch(match, url);
                        return;
                    }
                    // A rule pointing at a browser/profile that no longer exists
                    // falls through to the picker rather than silently doing nothing.
                }
            }

            Application.Run(new PickerForm(targets, url));
        }
    }
}
