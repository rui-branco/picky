using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

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
        /// <summary>"column" for a vertical list, "row" for a horizontal strip.</summary>
        public string PickerLayout { get; set; }
        /// <summary>Whether the picker prints names beside the logos, or shows logos alone.</summary>
        public bool ShowLabels { get; set; }
        /// <summary>Whether each entry names the browser under the profile.</summary>
        public bool ShowBrowserName { get; set; }
        /// <summary>Whether the picker prints the link address above the list.</summary>
        public bool ShowHost { get; set; }
        /// <summary>How large the picker draws, 1.0 being the designed size.</summary>
        public double PickerScale { get; set; }

        public const string LayoutColumn = "column";
        public const string LayoutRow = "row";

        public AppConfig()
        {
            Rules = new List<Rule>();
            Order = new List<string>();
            // Deserialising a config written before this option existed leaves the
            // constructor value in place, so old configs keep showing private entries.
            ShowPrivate = true;
            PickerLayout = LayoutColumn;
            ShowLabels = true;
            ShowHost = true;
            ShowBrowserName = true;
            PickerScale = 1.0;
        }

        /// <summary>A method, not a property: a getter-only property would be serialised.</summary>
        public bool IsRowLayout()
        {
            return string.Equals(PickerLayout, LayoutRow, StringComparison.OrdinalIgnoreCase);
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
                Dictionary<string, object> root = Json.Obj(Json.Parse(json));
                if (root == null) return new AppConfig();

                AppConfig cfg = new AppConfig();

                // Rules
                List<object> rulesArr = Json.Arr(root.ContainsKey("Rules") ? root["Rules"] : null);
                if (rulesArr != null)
                {
                    cfg.Rules = new List<Rule>();
                    foreach (object item in rulesArr)
                    {
                        Dictionary<string, object> ruleObj = Json.Obj(item);
                        if (ruleObj != null)
                        {
                            Rule r = new Rule();
                            r.Pattern = Json.Str(ruleObj, "Pattern");
                            r.TargetId = Json.Str(ruleObj, "TargetId");
                            cfg.Rules.Add(r);
                        }
                    }
                }

                // FallbackTargetId
                cfg.FallbackTargetId = Json.Str(root, "FallbackTargetId");

                // Order
                List<object> orderArr = Json.Arr(root.ContainsKey("Order") ? root["Order"] : null);
                if (orderArr != null)
                {
                    cfg.Order = new List<string>();
                    foreach (object item in orderArr)
                    {
                        if (item != null) cfg.Order.Add(item.ToString());
                    }
                }

                // ShowPrivate (default true)
                cfg.ShowPrivate = Json.Bool(root, "ShowPrivate", true);

                // PickerLayout (default column)
                string layout = Json.Str(root, "PickerLayout");
                cfg.PickerLayout = string.IsNullOrEmpty(layout) ? LayoutColumn : layout;

                // ShowLabels (default true)
                cfg.ShowLabels = Json.Bool(root, "ShowLabels", true);

                // ShowHost (default true)
                cfg.ShowHost = Json.Bool(root, "ShowHost", true);

                // ShowBrowserName (default true)
                cfg.ShowBrowserName = Json.Bool(root, "ShowBrowserName", true);

                // PickerScale (default 1.0, clamped to 0.5-2.0)
                double scale = 1.0;
                if (root.ContainsKey("PickerScale"))
                {
                    object val = root["PickerScale"];
                    if (val is double) scale = (double)val;
                    else if (val is int) scale = (int)val;
                    else if (val is long) scale = (long)val;
                }
                if (scale < 0.5) scale = 0.5;
                if (scale > 2.0) scale = 2.0;
                cfg.PickerScale = scale;

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

                Dictionary<string, object> root = new Dictionary<string, object>();

                // Rules
                List<object> rulesArr = new List<object>();
                foreach (Rule r in Rules)
                {
                    Dictionary<string, object> ruleObj = new Dictionary<string, object>();
                    ruleObj["Pattern"] = r.Pattern;
                    ruleObj["TargetId"] = r.TargetId;
                    rulesArr.Add(ruleObj);
                }
                root["Rules"] = rulesArr;

                // FallbackTargetId
                root["FallbackTargetId"] = FallbackTargetId;

                // Order
                List<object> orderArr = new List<object>();
                foreach (string s in Order)
                {
                    orderArr.Add(s);
                }
                root["Order"] = orderArr;

                // Booleans and layout
                root["ShowPrivate"] = ShowPrivate;
                root["PickerLayout"] = PickerLayout;
                root["ShowLabels"] = ShowLabels;
                root["ShowHost"] = ShowHost;
                root["ShowBrowserName"] = ShowBrowserName;
                root["PickerScale"] = PickerScale;

                File.WriteAllText(FilePath, Json.Write(root), new UTF8Encoding(false));
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
