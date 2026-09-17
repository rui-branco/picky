using System;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Win32;

namespace Picky
{
    public static class Registration
    {
        public const string AppName = "Picky";
        public const string ProgId = "PickyURL";

        const string ClientKey = @"SOFTWARE\Clients\StartMenuInternet\Picky";
        const string ClassKey = @"SOFTWARE\Classes\PickyURL";
        const string RegAppsKey = @"SOFTWARE\RegisteredApplications";

        public static string ExePath
        {
            get { return Assembly.GetEntryAssembly().Location; }
        }

        public static void Register()
        {
            string exe = ExePath;

            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(ClassKey))
            {
                k.SetValue(null, "Picky Document");
                k.SetValue("URL Protocol", "");
            }
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(ClassKey + @"\DefaultIcon"))
                k.SetValue(null, exe + ",0");
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(ClassKey + @"\shell\open\command"))
                k.SetValue(null, "\"" + exe + "\" \"%1\"");

            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(ClientKey))
                k.SetValue(null, AppName);
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(ClientKey + @"\DefaultIcon"))
                k.SetValue(null, exe + ",0");
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(ClientKey + @"\shell\open\command"))
                k.SetValue(null, exe);

            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(ClientKey + @"\Capabilities"))
            {
                k.SetValue("ApplicationName", AppName);
                k.SetValue("ApplicationDescription", "Choose which browser opens each link.");
                k.SetValue("ApplicationIcon", exe + ",0");
            }
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(ClientKey + @"\Capabilities\URLAssociations"))
            {
                k.SetValue("http", ProgId);
                k.SetValue("https", ProgId);
            }
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(ClientKey + @"\Capabilities\FileAssociations"))
            {
                k.SetValue(".htm", ProgId);
                k.SetValue(".html", ProgId);
            }

            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RegAppsKey))
                k.SetValue(AppName, @"Software\Clients\StartMenuInternet\Picky\Capabilities");
        }

        public static void Unregister()
        {
            TryDeleteTree(ClassKey);
            TryDeleteTree(ClientKey);
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RegAppsKey, true))
                {
                    if (k != null) k.DeleteValue(AppName, false);
                }
            }
            catch { }
        }

        static void TryDeleteTree(string path)
        {
            try { Registry.CurrentUser.DeleteSubKeyTree(path, false); }
            catch { }
        }

        public static bool IsRegistered()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(ClientKey + @"\Capabilities"))
                    return k != null;
            }
            catch { return false; }
        }

        // UserChoice is hash-protected by Windows; we can only read it, never set it.
        public static bool IsDefaultBrowser()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\http\UserChoice"))
                {
                    if (k == null) return false;
                    object v = k.GetValue("ProgId");
                    return v != null && string.Equals(v.ToString(), ProgId, StringComparison.OrdinalIgnoreCase);
                }
            }
            catch { return false; }
        }

        public static void OpenDefaultAppsSettings()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(
                    "ms-settings:defaultapps?registeredAppUser=" + AppName);
                psi.UseShellExecute = true;
                Process.Start(psi);
            }
            catch { }
        }
    }
}
