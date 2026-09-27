using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

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
        const int ASFW_ANY = -1;

        public static void Launch(Target t, string url)
        {
            try
            {
                // A running browser hands the link to its own main process, which
                // then has to raise its window. Started from the running copy of
                // Picky, nothing in that chain holds the foreground and the page
                // opens behind - so pass on the right the click handed over.
                Handoff.AllowSetForegroundWindow(ASFW_ANY);

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
            // Browsers registered with Windows are whatever this machine happens
            // to have installed - a dev build, a work tool - so a published
            // screenshot keeps to the ones every reader knows.
            real.RemoveAll(delegate(Target t) { return t.Id.StartsWith("registered|", StringComparison.Ordinal); });

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
            try { ScanRegistered(list); }
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
                    Bitmap profileIco = TryProfileIcon(b.UserDataDir, p.Dir);
                    outList.Add(new Target
                    {
                        Id = key + "|" + p.Dir,
                        BrowserName = b.Name,
                        ProfileLabel = p.Label,
                        Subtitle = p.Email,
                        ExePath = exe,
                        // Always the directory key, never the display label.
                        ArgsTemplate = "--profile-directory=\"" + p.Dir + "\" {url}",
                        Image = profileIco != null ? profileIco : ico
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

        const string ClientsKey = @"SOFTWARE\Clients\StartMenuInternet";

        // Every other browser that registered itself with Windows (Forge Deck,
        // Opera, Arc...). A client name is read once: HKCU wins over HKLM, and
        // the 64-bit view over the 32-bit one.
        static void ScanRegistered(List<Target> outList)
        {
            RegistryKey[] roots = new RegistryKey[] {
                Registry.CurrentUser,
                RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64),
                RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32)
            };
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (RegistryKey root in roots)
            {
                string[] names;
                using (RegistryKey clients = root.OpenSubKey(ClientsKey))
                {
                    if (clients == null) continue;
                    names = clients.GetSubKeyNames();
                }

                foreach (string name in names)
                {
                    if (!seen.Add(name)) continue;
                    if (string.Equals(name, "Picky", StringComparison.OrdinalIgnoreCase)) continue;
                    if (string.Equals(name, "IEXPLORE.EXE", StringComparison.OrdinalIgnoreCase)) continue;

                    try
                    {
                        Target t = ReadRegistered(roots, root, name, outList);
                        if (t != null) outList.Add(t);
                    }
                    catch { }
                }
            }
        }

        static Target ReadRegistered(RegistryKey[] roots, RegistryKey root, string name, List<Target> outList)
        {
            string display = null;
            string progId = null;
            string iconSpec = null;
            string fallbackName = null;

            using (RegistryKey client = root.OpenSubKey(ClientsKey + @"\" + name))
            {
                if (client == null) return null;
                fallbackName = client.GetValue(null) as string;

                using (RegistryKey caps = client.OpenSubKey("Capabilities"))
                {
                    if (caps != null) display = caps.GetValue("ApplicationName") as string;
                }
                using (RegistryKey urls = client.OpenSubKey(@"Capabilities\URLAssociations"))
                {
                    if (urls != null)
                        progId = FirstNonEmpty(urls.GetValue("https") as string, urls.GetValue("http") as string);
                }
                using (RegistryKey icon = client.OpenSubKey("DefaultIcon"))
                {
                    if (icon != null) iconSpec = icon.GetValue(null) as string;
                }
            }

            // A browser that cannot take a link is no use here.
            if (string.IsNullOrEmpty(progId)) return null;
            string command = ProgIdCommand(roots, progId);
            if (string.IsNullOrEmpty(command)) return null;

            string exe, args;
            SplitCommand(command, out exe, out args);
            if (string.IsNullOrEmpty(exe) || !File.Exists(exe)) return null;

            // Edge, Chrome, Firefox and the rest register too; the scans above
            // already list them with their profiles.
            string full = Path.GetFullPath(exe);
            foreach (Target t in outList)
            {
                if (string.Equals(Path.GetFullPath(t.ExePath), full, StringComparison.OrdinalIgnoreCase))
                    return null;
            }

            if (display != null && display.StartsWith("@")) display = LoadIndirect(display);
            display = FirstNonEmpty(display, fallbackName, name);

            Bitmap ico = TryDefaultIcon(iconSpec);
            if (ico == null) ico = TryIcon(exe);

            return new Target
            {
                Id = "registered|" + name.ToLowerInvariant(),
                BrowserName = display,
                ProfileLabel = display,
                Subtitle = "",
                ExePath = exe,
                ArgsTemplate = args,
                Image = ico
            };
        }

        static string ProgIdCommand(RegistryKey[] roots, string progId)
        {
            string path = @"SOFTWARE\Classes\" + progId + @"\shell\open\command";
            foreach (RegistryKey root in roots)
            {
                using (RegistryKey k = root.OpenSubKey(path))
                {
                    if (k == null) continue;
                    string cmd = k.GetValue(null) as string;
                    if (!string.IsNullOrEmpty(cmd)) return cmd;
                }
            }
            return null;
        }

        // "C:\app.exe" --flag "%1"  ->  C:\app.exe  +  --flag {url}
        static void SplitCommand(string command, out string exe, out string args)
        {
            string text = command.Trim();
            string rest;
            if (text.StartsWith("\""))
            {
                int end = text.IndexOf('"', 1);
                if (end < 0) { exe = text.Substring(1); rest = ""; }
                else { exe = text.Substring(1, end - 1); rest = text.Substring(end + 1); }
            }
            else
            {
                int space = text.IndexOf(' ');
                if (space < 0) { exe = text; rest = ""; }
                else { exe = text.Substring(0, space); rest = text.Substring(space + 1); }
            }
            exe = Environment.ExpandEnvironmentVariables(exe.Trim());

            args = rest
                .Replace("\"%1\"", "{url}").Replace("%1", "{url}")
                .Replace("\"%L\"", "{url}").Replace("%L", "{url}")
                .Replace("\"%l\"", "{url}").Replace("%l", "{url}");
            if (args.IndexOf("{url}", StringComparison.Ordinal) < 0) args = args + " {url}";
            args = args.Trim();
        }

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        static extern int SHLoadIndirectString(string source, StringBuilder outBuf, int outBufSize, IntPtr reserved);

        // "@C:\app.exe,-123" style names point at a string resource.
        static string LoadIndirect(string source)
        {
            StringBuilder sb = new StringBuilder(512);
            if (SHLoadIndirectString(source, sb, sb.Capacity, IntPtr.Zero) != 0) return null;
            return sb.ToString();
        }

        // DefaultIcon is "path,index": the path may be quoted or hold %vars%, and
        // a negative index is a resource id rather than a position.
        static Bitmap TryDefaultIcon(string spec)
        {
            if (string.IsNullOrEmpty(spec)) return null;
            string path = spec.Trim();
            int index = 0;
            int comma = path.LastIndexOf(',');
            if (comma >= 0)
            {
                int n;
                if (int.TryParse(path.Substring(comma + 1).Trim(), out n))
                {
                    index = n;
                    path = path.Substring(0, comma);
                }
            }
            path = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
            if (!File.Exists(path)) return null;
            return TryIconAt(path, index);
        }

        static List<ProfileInfo> ReadProfiles(string userDataDir)
        {
            List<ProfileInfo> res = new List<ProfileInfo>();
            try
            {
                string ls = Path.Combine(userDataDir, "Local State");
                if (!File.Exists(ls)) return res;

                string json = File.ReadAllText(ls, Encoding.UTF8);
                Dictionary<string, object> root = Json.Obj(Json.Parse(json));
                if (root == null) return res;

                object profObj;
                if (!root.TryGetValue("profile", out profObj)) return res;
                Dictionary<string, object> prof = Json.Obj(profObj);
                if (prof == null) return res;

                object cacheObj;
                if (!prof.TryGetValue("info_cache", out cacheObj)) return res;
                Dictionary<string, object> cache = Json.Obj(cacheObj);
                if (cache == null) return res;

                foreach (KeyValuePair<string, object> kv in cache)
                {
                    Dictionary<string, object> v = Json.Obj(kv.Value);
                    string label = null;
                    string email = "";
                    if (v != null)
                    {
                        // shortcut_name carries the real label (Personal, Work). The name
                        // field is often a meaningless "Profile N" that disagrees with the
                        // directory it lives in, so it is the last resort before the key.
                        label = FirstNonEmpty(Json.Str(v, "shortcut_name"), Json.Str(v, "gaia_name"), Json.Str(v, "name"));
                        email = Json.Str(v, "user_name");
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

        // Chromium writes a badged icon (logo + profile avatar) into the profile dir.
        static Bitmap TryProfileIcon(string userDataDir, string profileDir)
        {
            try
            {
                if (string.IsNullOrEmpty(userDataDir) || string.IsNullOrEmpty(profileDir))
                    return null;

                string dir = Path.Combine(userDataDir, profileDir);
                if (!Directory.Exists(dir))
                    return null;

                string[] files = Directory.GetFiles(dir, "*Profile.ico");
                if (files.Length == 0)
                    return null;

                Array.Sort(files);
                string path = files[0];

                using (Icon ic = new Icon(path, 64, 64))
                using (Bitmap tmp = ic.ToBitmap())
                    return new Bitmap(tmp);
            }
            catch { }
            return null;
        }

        static Bitmap TryIcon(string exe)
        {
            Bitmap best = TryIconAt(exe, 0);
            if (best != null) return best;

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

        static Bitmap TryIconAt(string file, int index)
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
                    int n = PrivateExtractIcons(file, index, want, want, handles, ids, 1, 0);
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
            return null;
        }
    }
}
