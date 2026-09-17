using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;

namespace Picky
{
    public class Target
    {
        public string Id;
        public string BrowserName;
        public string ProfileLabel;
        public string Subtitle;
        public string ExePath;
        public string ArgsTemplate;
        public Bitmap Image;
    }

    public static class Launcher
    {
        public static void Launch(Target t, string url)
        {
            try
            {
                // Strip quotes so a hostile URL cannot break out of its argument.
                string safe = (url == null ? "" : url).Replace("\"", "");
                string args = t.ArgsTemplate.Replace("{url}", "\"" + safe + "\"");
                ProcessStartInfo psi = new ProcessStartInfo(t.ExePath, args);
                psi.UseShellExecute = false;
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show(
                    "Could not launch " + t.BrowserName + ":\r\n" + ex.Message,
                    "Picky");
            }
        }
    }

    class ProfileInfo
    {
        public string Dir;
        public string Label;
        public string Email;
    }

    class ChromiumBrowser
    {
        public string Name;
        public string[] ExeCandidates;
        public string UserDataDir;
        public string PrivateFlag;
        public string PrivateLabel;
    }

    public static class BrowserScanner
    {
        static string LocalAppData
        {
            get { return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData); }
        }
        static string PF
        {
            get { return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles); }
        }
        static string PF86
        {
            get { return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86); }
        }

        /// <summary>User Data folders whose "Local State" defines the profile list.</summary>
        public static List<string> UserDataDirs()
        {
            List<string> dirs = new List<string>();
            foreach (ChromiumBrowser b in ChromiumBrowsers())
            {
                if (Directory.Exists(b.UserDataDir)) dirs.Add(b.UserDataDir);
            }
            return dirs;
        }

        /// <summary>
        /// Replaces real profile names and account emails with placeholders.
        /// Used to produce documentation screenshots without leaking personal data.
        /// </summary>
        public static bool DemoMode = false;

        public static List<Target> Scan()
        {
            List<Target> found = ScanReal();
            return DemoMode ? Anonymise(found) : found;
        }

        static List<Target> Anonymise(List<Target> real)
        {
            // A distinct label set per browser, so the demo never shows two
            // identically named profiles.
            string[][] labelSets = new string[][] {
                new string[] { "Personal", "Work", "Testing" },
                new string[] { "Default", "Design" },
                new string[] { "Profile A", "Profile B" }
            };
            string[] mails = new string[] { "you@example.com", "you@company.com", "qa@example.com" };

            Dictionary<string, int> browserOrdinal = new Dictionary<string, int>();
            Dictionary<string, int> profileIndex = new Dictionary<string, int>();

            foreach (Target t in real)
            {
                if (!browserOrdinal.ContainsKey(t.BrowserName))
                    browserOrdinal[t.BrowserName] = browserOrdinal.Count;

                if (t.Id.EndsWith("|__private")) continue;   // InPrivate / Incognito keep their names

                int n;
                profileIndex.TryGetValue(t.BrowserName, out n);
                profileIndex[t.BrowserName] = n + 1;

                string[] set = labelSets[browserOrdinal[t.BrowserName] % labelSets.Length];
                t.ProfileLabel = set[n % set.Length];
                if (!string.IsNullOrEmpty(t.Subtitle)) t.Subtitle = mails[n % mails.Length];
            }
            return real;
        }

        static List<Target> ScanReal()
        {
            List<Target> list = new List<Target>();
            foreach (ChromiumBrowser b in ChromiumBrowsers())
            {
                try { ScanChromium(b, list); }
                catch { }
            }
            try { ScanFirefox(list); }
            catch { }
            return list;
        }

        static List<ChromiumBrowser> ChromiumBrowsers()
        {
            List<ChromiumBrowser> l = new List<ChromiumBrowser>();

            l.Add(new ChromiumBrowser
            {
                Name = "Microsoft Edge",
                ExeCandidates = new string[] {
                    Path.Combine(PF86, @"Microsoft\Edge\Application\msedge.exe"),
                    Path.Combine(PF, @"Microsoft\Edge\Application\msedge.exe")
                },
                UserDataDir = Path.Combine(LocalAppData, @"Microsoft\Edge\User Data"),
                PrivateFlag = "--inprivate",
                PrivateLabel = "InPrivate"
            });

            l.Add(new ChromiumBrowser
            {
                Name = "Google Chrome",
                ExeCandidates = new string[] {
                    Path.Combine(PF, @"Google\Chrome\Application\chrome.exe"),
                    Path.Combine(PF86, @"Google\Chrome\Application\chrome.exe")
                },
                UserDataDir = Path.Combine(LocalAppData, @"Google\Chrome\User Data"),
                PrivateFlag = "--incognito",
                PrivateLabel = "Incognito"
            });

            l.Add(new ChromiumBrowser
            {
                Name = "Brave",
                ExeCandidates = new string[] {
                    Path.Combine(PF, @"BraveSoftware\Brave-Browser\Application\brave.exe"),
                    Path.Combine(LocalAppData, @"BraveSoftware\Brave-Browser\Application\brave.exe")
                },
                UserDataDir = Path.Combine(LocalAppData, @"BraveSoftware\Brave-Browser\User Data"),
                PrivateFlag = "--incognito",
                PrivateLabel = "Incognito"
            });

            l.Add(new ChromiumBrowser
            {
                Name = "Vivaldi",
                ExeCandidates = new string[] {
                    Path.Combine(LocalAppData, @"Vivaldi\Application\vivaldi.exe"),
                    Path.Combine(PF, @"Vivaldi\Application\vivaldi.exe")
                },
                UserDataDir = Path.Combine(LocalAppData, @"Vivaldi\User Data"),
                PrivateFlag = "--incognito",
                PrivateLabel = "Incognito"
            });

            return l;
        }

        static void ScanChromium(ChromiumBrowser b, List<Target> outList)
        {
            string exe = null;
            foreach (string c in b.ExeCandidates)
            {
                if (File.Exists(c)) { exe = c; break; }
            }
            if (exe == null) return;

            Bitmap ico = TryIcon(exe);
            string key = Slug(b.Name);

            List<ProfileInfo> profiles = ReadProfiles(b.UserDataDir);

            if (profiles.Count == 0)
            {
                outList.Add(new Target
                {
                    Id = key,
                    BrowserName = b.Name,
                    ProfileLabel = b.Name,
                    Subtitle = "",
                    ExePath = exe,
                    ArgsTemplate = "{url}",
                    Image = ico
                });
            }
            else
            {
                foreach (ProfileInfo p in profiles)
                {
                    outList.Add(new Target
                    {
                        Id = key + "|" + p.Dir,
                        BrowserName = b.Name,
                        ProfileLabel = p.Label,
                        Subtitle = p.Email,
                        ExePath = exe,
                        // Always the directory key, never the display label.
                        ArgsTemplate = "--profile-directory=\"" + p.Dir + "\" {url}",
                        Image = ico
                    });
                }
            }

            outList.Add(new Target
            {
                Id = key + "|__private",
                BrowserName = b.Name,
                ProfileLabel = b.PrivateLabel,
                Subtitle = "",
                ExePath = exe,
                ArgsTemplate = b.PrivateFlag + " {url}",
                Image = ico
            });
        }

        static void ScanFirefox(List<Target> outList)
        {
            string[] cands = new string[] {
                Path.Combine(PF, @"Mozilla Firefox\firefox.exe"),
                Path.Combine(PF86, @"Mozilla Firefox\firefox.exe")
            };
            string exe = null;
            foreach (string c in cands)
            {
                if (File.Exists(c)) { exe = c; break; }
            }
            if (exe == null) return;

            Bitmap ico = TryIcon(exe);
            outList.Add(new Target
            {
                Id = "firefox",
                BrowserName = "Firefox",
                ProfileLabel = "Firefox",
                Subtitle = "",
                ExePath = exe,
                ArgsTemplate = "{url}",
                Image = ico
            });
            outList.Add(new Target
            {
                Id = "firefox|__private",
                BrowserName = "Firefox",
                ProfileLabel = "Private",
                Subtitle = "",
                ExePath = exe,
                ArgsTemplate = "-private-window {url}",
                Image = ico
            });
        }

        static List<ProfileInfo> ReadProfiles(string userDataDir)
        {
            List<ProfileInfo> res = new List<ProfileInfo>();
            try
            {
                string ls = Path.Combine(userDataDir, "Local State");
                if (!File.Exists(ls)) return res;

                string json = File.ReadAllText(ls, Encoding.UTF8);
                JavaScriptSerializer ser = new JavaScriptSerializer();
                ser.MaxJsonLength = int.MaxValue;
                Dictionary<string, object> root = ser.Deserialize<Dictionary<string, object>>(json);
                if (root == null) return res;

                object profObj;
                if (!root.TryGetValue("profile", out profObj)) return res;
                Dictionary<string, object> prof = profObj as Dictionary<string, object>;
                if (prof == null) return res;

                object cacheObj;
                if (!prof.TryGetValue("info_cache", out cacheObj)) return res;
                Dictionary<string, object> cache = cacheObj as Dictionary<string, object>;
                if (cache == null) return res;

                foreach (KeyValuePair<string, object> kv in cache)
                {
                    Dictionary<string, object> v = kv.Value as Dictionary<string, object>;
                    string label = null;
                    string email = "";
                    if (v != null)
                    {
                        // shortcut_name carries the real label (Personal, Work). The name
                        // field is often a meaningless "Profile N" that disagrees with the
                        // directory it lives in, so it is the last resort before the key.
                        label = FirstNonEmpty(Str(v, "shortcut_name"), Str(v, "gaia_name"), Str(v, "name"));
                        email = Str(v, "user_name");
                        if (email == null) email = "";
                    }
                    if (string.IsNullOrEmpty(label)) label = kv.Key;
                    res.Add(new ProfileInfo { Dir = kv.Key, Label = label, Email = email });
                }
                res.Sort(CompareProfiles);
            }
            catch { }
            return res;
        }

        static int CompareProfiles(ProfileInfo a, ProfileInfo b)
        {
            int ra = Rank(a.Dir), rb = Rank(b.Dir);
            if (ra != rb) return ra.CompareTo(rb);
            return string.Compare(a.Dir, b.Dir, StringComparison.OrdinalIgnoreCase);
        }

        static int Rank(string dir)
        {
            if (string.Equals(dir, "Default", StringComparison.OrdinalIgnoreCase)) return -1;
            if (dir != null && dir.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase))
            {
                int n;
                if (int.TryParse(dir.Substring(8), out n)) return n;
            }
            return int.MaxValue;
        }

        static string Str(Dictionary<string, object> d, string key)
        {
            object v;
            if (d.TryGetValue(key, out v) && v != null)
            {
                string s = v.ToString();
                if (!string.IsNullOrEmpty(s)) return s;
            }
            return null;
        }

        static string FirstNonEmpty(params string[] vals)
        {
            foreach (string v in vals)
            {
                if (!string.IsNullOrEmpty(v)) return v;
            }
            return null;
        }

        static string Slug(string s)
        {
            return s.ToLowerInvariant().Replace(" ", "");
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int PrivateExtractIcons(string file, int index, int cx, int cy,
            IntPtr[] phicon, int[] piconid, int nIcons, int flags);

        [DllImport("user32.dll")]
        static extern bool DestroyIcon(IntPtr handle);

        static Bitmap TryIcon(string exe)
        {
            // ExtractAssociatedIcon only ever yields 32x32, which looks mushy once
            // scaled. PrivateExtractIcons picks the best matching image out of the
            // exe's icon group, so ask for a large one and downscale cleanly.
            foreach (int want in new int[] { 128, 64, 48 })
            {
                IntPtr[] handles = new IntPtr[1];
                int[] ids = new int[1];
                try
                {
                    int n = PrivateExtractIcons(exe, 0, want, want, handles, ids, 1, 0);
                    if (n > 0 && handles[0] != IntPtr.Zero)
                    {
                        try
                        {
                            using (Icon ic = Icon.FromHandle(handles[0]))
                            using (Bitmap tmp = ic.ToBitmap())
                                return new Bitmap(tmp);   // copy; handle is freed below
                        }
                        finally { DestroyIcon(handles[0]); }
                    }
                }
                catch { }
            }

            try
            {
                using (Icon ic = Icon.ExtractAssociatedIcon(exe))
                {
                    if (ic != null)
                        using (Bitmap tmp = ic.ToBitmap())
                            return new Bitmap(tmp);
                }
            }
            catch { }
            return null;
        }
    }
}
