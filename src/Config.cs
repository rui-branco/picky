using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace Picky
{
    public class Rule
    {
        public string Pattern { get; set; }
        public string TargetId { get; set; }
    }

    public class AppConfig
    {
        public List<Rule> Rules { get; set; }
        public string FallbackTargetId { get; set; }
        /// <summary>Target ids in the order the picker should list them.</summary>
        public List<string> Order { get; set; }
        /// <summary>Whether InPrivate / Incognito entries appear in the picker.</summary>
        public bool ShowPrivate { get; set; }

        public AppConfig()
        {
            Rules = new List<Rule>();
            Order = new List<string>();
            // Deserialising a config written before this option existed leaves the
            // constructor value in place, so old configs keep showing private entries.
            ShowPrivate = true;
        }

        /// <summary>Applies the saved order and the private-window preference.</summary>
        public List<Target> Arrange(List<Target> targets)
        {
            List<Target> ordered = ApplyOrder(targets);
            if (ShowPrivate) return ordered;

            List<Target> visible = new List<Target>();
            foreach (Target t in ordered)
            {
                if (t.Id != null && t.Id.EndsWith("|__private")) continue;
                visible.Add(t);
            }
            return visible;
        }

        /// <summary>
        /// Reorders a freshly scanned list to match the saved order. Targets with no
        /// saved position keep their natural order and go last, so a newly created
        /// profile shows up rather than disappearing.
        /// </summary>
        public List<Target> ApplyOrder(List<Target> targets)
        {
            if (Order == null || Order.Count == 0) return targets;

            List<Target> ordered = new List<Target>();
            foreach (string id in Order)
            {
                Target t = targets.Find(delegate(Target x) { return x.Id == id; });
                if (t != null && !ordered.Contains(t)) ordered.Add(t);
            }
            foreach (Target t in targets)
            {
                if (!ordered.Contains(t)) ordered.Add(t);
            }
            return ordered;
        }

        public static string Dir
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Picky");
            }
        }

        public static string FilePath
        {
            get { return Path.Combine(Dir, "config.json"); }
        }

        /// <summary>
        /// Documentation mode: Load returns sample rules and Save is a no-op, so
        /// rendering screenshots can never overwrite a real config.
        /// </summary>
        public static bool DemoMode = false;

        static AppConfig DemoConfig()
        {
            AppConfig c = new AppConfig();
            c.Rules.Add(MakeRule("*.company.com", "microsoftedge|Profile 1"));
            c.Rules.Add(MakeRule("github.com", "googlechrome|Default"));
            c.Rules.Add(MakeRule("*.internal.dev", "microsoftedge|Profile 1"));
            return c;
        }

        static Rule MakeRule(string pattern, string targetId)
        {
            Rule r = new Rule();
            r.Pattern = pattern;
            r.TargetId = targetId;
            return r;
        }

        public static AppConfig Load()
        {
            if (DemoMode) return DemoConfig();

            try
            {
                if (!File.Exists(FilePath)) return new AppConfig();
                string json = File.ReadAllText(FilePath, Encoding.UTF8);
                JavaScriptSerializer ser = new JavaScriptSerializer();
                AppConfig cfg = ser.Deserialize<AppConfig>(json);
                if (cfg == null) return new AppConfig();
                if (cfg.Rules == null) cfg.Rules = new List<Rule>();
                return cfg;
            }
            catch
            {
                // A corrupt config must never stop the picker appearing.
                return new AppConfig();
            }
        }

        public bool Save()
        {
            if (DemoMode) return true;   // never touch a real config while demoing
            try
            {
                if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);
                JavaScriptSerializer ser = new JavaScriptSerializer();
                File.WriteAllText(FilePath, ser.Serialize(this), new UTF8Encoding(false));
                return true;
            }
            catch
            {
                return false;
            }
        }

        public string MatchRule(string url)
        {
            string host = GetHost(url);
            if (host == null) return null;
            foreach (Rule r in Rules)
            {
                if (r == null || string.IsNullOrEmpty(r.Pattern)) continue;
                if (GlobMatches(r.Pattern, host)) return r.TargetId;
            }
            return null;
        }

        public static string GetHost(string url)
        {
            try { return new Uri(url).Host; }
            catch { return null; }
        }

        public static bool GlobMatches(string pattern, string host)
        {
            string rx = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
            try { return Regex.IsMatch(host, rx, RegexOptions.IgnoreCase); }
            catch { return false; }
        }
    }
}
