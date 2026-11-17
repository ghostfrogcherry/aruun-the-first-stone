using System;
using System.Collections.Generic;
using Aruun.Core.Content;

namespace Aruun.Core.Survival
{
    /// <summary>One drift rule: per_min while every 'when' flag is set and no 'unless' flag is.</summary>
    public sealed class Rate
    {
        public double PerMin;
        public string[] When = new string[0];
        public string[] Unless = new string[0];

        public bool On(ICollection<string> flags)
        {
            foreach (var f in When) if (!flags.Contains(f)) return false;
            foreach (var f in Unless) if (flags.Contains(f)) return false;
            return true;
        }
    }

    /// <summary>A range [From, To) of a condition with its effects.</summary>
    public sealed class Band
    {
        public double From, To;
        public string Name = "";
        public bool Good;
        public double DamagePerMin;
        public Dictionary<string, double> Mods = new Dictionary<string, double>();
    }

    public sealed class ConditionDef
    {
        public string Id = "";
        public string Name = "";
        public double Start;
        /// <summary>Part of the magick system (gravity debt), not the body.</summary>
        public bool Magick;
        public List<Rate> Rates = new List<Rate>();
        public List<Band> Bands = new List<Band>();
    }

    public sealed class BuffDef
    {
        public string Id = "";
        public string Name = "";
        public double Duration;
        public Dictionary<string, double> Mods = new Dictionary<string, double>();
        /// <summary>Extra drift per minute on conditions, by condition id.</summary>
        public Dictionary<string, double> Rates = new Dictionary<string, double>();
        public string[] Flags = new string[0];
    }

    /// <summary>
    /// A survival preset from content/survival/rulesets.json, with its "extends" chain merged in
    /// (hardcore is light plus overrides). Every number lives in the file; this only reads it.
    /// </summary>
    public sealed class Ruleset
    {
        public string Id = "";
        public string Name = "";
        public double DayLength = 1200;
        public bool Durability;
        public bool Pvp;
        /// <summary>The merged JSON, for sections read elsewhere (vitals, death, fall, creatures...).</summary>
        public Dictionary<string, object> Raw = new Dictionary<string, object>();
        public List<ConditionDef> Conditions = new List<ConditionDef>();
        public Dictionary<string, BuffDef> Buffs = new Dictionary<string, BuffDef>();
        public List<KeyValuePair<string, int>> StartingItems = new List<KeyValuePair<string, int>>();
        public List<string> StartingWords = new List<string>();

        /// <summary>A number from a section, such as Num("vitals", "hp").</summary>
        public double Num(string section, string key, double fallback = 0)
        {
            return MiniJson.Num(Section(section), key, fallback);
        }

        public bool Flag(string section, string key, bool fallback = false)
        {
            return MiniJson.Bool(Section(section), key, fallback);
        }

        public string Str(string section, string key, string fallback = "")
        {
            return MiniJson.Str(Section(section), key, fallback);
        }

        public Dictionary<string, object> Section(string name)
        {
            object v;
            return Raw.TryGetValue(name, out v) && v is Dictionary<string, object> ? (Dictionary<string, object>)v : new Dictionary<string, object>();
        }

        public ConditionDef Condition(string id)
        {
            foreach (var c in Conditions) if (c.Id == id) return c;
            return null;
        }

        /// <summary>The ids of every ruleset in the file, and the default one.</summary>
        public static List<string> Ids(string json, out string defaultId)
        {
            var root = MiniJson.ParseObject(json);
            defaultId = MiniJson.Str(root, "default", "light");
            return new List<string>(Rulesets(root).Keys);
        }

        /// <summary>Loads one ruleset. An unknown id falls back to the file's default.</summary>
        public static Ruleset Load(string json, string id)
        {
            var root = MiniJson.ParseObject(json);
            var all = Rulesets(root);
            if (id == null || !all.ContainsKey(id)) id = MiniJson.Str(root, "default", "light");
            var raw = Resolve(all, id, 0);
            var r = new Ruleset
            {
                Id = id,
                Raw = raw,
                Name = MiniJson.Str(raw, "name", id),
                DayLength = MiniJson.Num(raw, "day_length_s", 1200),
                Durability = MiniJson.Bool(raw, "durability"),
                Pvp = MiniJson.Bool(raw, "pvp"),
            };
            foreach (var kv in r.Section("conditions"))
                r.Conditions.Add(ReadCondition(kv.Key, (Dictionary<string, object>)kv.Value));
            foreach (var kv in r.Section("starting_items"))
                r.StartingItems.Add(new KeyValuePair<string, int>(kv.Key, (int)(double)kv.Value));
            foreach (var w in MiniJson.List(raw, "starting_words")) r.StartingWords.Add((string)w);
            object buffs;
            if (root.TryGetValue("buffs", out buffs) && buffs is Dictionary<string, object>)
                foreach (var kv in (Dictionary<string, object>)buffs)
                {
                    var o = kv.Value as Dictionary<string, object>;
                    if (o == null) continue;
                    r.Buffs[kv.Key] = new BuffDef
                    {
                        Id = kv.Key, Name = MiniJson.Str(o, "name", kv.Key), Duration = MiniJson.Num(o, "duration_s", 10),
                        Mods = Numbers(o, "mods"), Rates = Numbers(o, "rates"), Flags = Strings(o, "flags"),
                    };
                }
            return r;
        }

        static Dictionary<string, object> Rulesets(Dictionary<string, object> root)
        {
            object v;
            if (!root.TryGetValue("rulesets", out v) || !(v is Dictionary<string, object>))
                throw new FormatException("rulesets.json: needs a 'rulesets' object");
            return (Dictionary<string, object>)v;
        }

        static Dictionary<string, object> Resolve(Dictionary<string, object> all, string id, int depth)
        {
            if (depth > 8) throw new FormatException("rulesets.json: 'extends' goes round in a circle at " + id);
            object v;
            if (!all.TryGetValue(id, out v) || !(v is Dictionary<string, object>))
                throw new FormatException("rulesets.json: no ruleset called '" + id + "'");
            var raw = (Dictionary<string, object>)v;
            string parent = MiniJson.Str(raw, "extends");
            if (parent.Length == 0) return (Dictionary<string, object>)DeepCopy(raw);
            var merged = Merge(Resolve(all, parent, depth + 1), raw);
            merged.Remove("extends");
            return merged;
        }

        /// <summary>
        /// Deep merge: objects merge key by key, everything else is replaced. A key "name_add"
        /// appends its array to the base array "name" (hardcore adds bands this way).
        /// </summary>
        public static Dictionary<string, object> Merge(Dictionary<string, object> baseObj, Dictionary<string, object> over)
        {
            var output = (Dictionary<string, object>)DeepCopy(baseObj);
            foreach (var kv in over)
            {
                object mine;
                output.TryGetValue(kv.Key, out mine);
                if (kv.Value is Dictionary<string, object> && mine is Dictionary<string, object>)
                    output[kv.Key] = Merge((Dictionary<string, object>)mine, (Dictionary<string, object>)kv.Value);
                else if (kv.Value is List<object> && kv.Key.EndsWith("_add", StringComparison.Ordinal))
                {
                    string name = kv.Key.Substring(0, kv.Key.Length - 4);
                    object baseList;
                    var list = output.TryGetValue(name, out baseList) && baseList is List<object>
                        ? (List<object>)DeepCopy(baseList) : new List<object>();
                    list.AddRange((List<object>)DeepCopy(kv.Value));
                    output[name] = list;
                }
                else output[kv.Key] = DeepCopy(kv.Value);
            }
            return output;
        }

        static object DeepCopy(object v)
        {
            var d = v as Dictionary<string, object>;
            if (d != null)
            {
                var c = new Dictionary<string, object>();
                foreach (var kv in d) c[kv.Key] = DeepCopy(kv.Value);
                return c;
            }
            var l = v as List<object>;
            if (l != null)
            {
                var c = new List<object>(l.Count);
                foreach (var x in l) c.Add(DeepCopy(x));
                return c;
            }
            return v;
        }

        static ConditionDef ReadCondition(string id, Dictionary<string, object> o)
        {
            var c = new ConditionDef
            {
                Id = id, Name = MiniJson.Str(o, "name", id), Start = MiniJson.Num(o, "start"), Magick = MiniJson.Bool(o, "magick"),
            };
            foreach (var x in MiniJson.List(o, "rates"))
            {
                var r = (Dictionary<string, object>)x;
                c.Rates.Add(new Rate { PerMin = MiniJson.Num(r, "per_min"), When = Strings(r, "when"), Unless = Strings(r, "unless") });
            }
            foreach (var x in MiniJson.List(o, "bands"))
            {
                var b = (Dictionary<string, object>)x;
                c.Bands.Add(new Band
                {
                    From = MiniJson.Num(b, "from"), To = MiniJson.Num(b, "to", 101), Name = MiniJson.Str(b, "name"),
                    Good = MiniJson.Bool(b, "good"), DamagePerMin = MiniJson.Num(b, "damage_per_min"), Mods = Numbers(b, "mods"),
                });
            }
            return c;
        }

        static Dictionary<string, double> Numbers(Dictionary<string, object> o, string key)
        {
            var output = new Dictionary<string, double>();
            object v;
            if (o.TryGetValue(key, out v) && v is Dictionary<string, object>)
                foreach (var kv in (Dictionary<string, object>)v)
                    if (kv.Value is double) output[kv.Key] = (double)kv.Value;
            return output;
        }

        static string[] Strings(Dictionary<string, object> o, string key)
        {
            var list = MiniJson.List(o, key);
            var output = new string[list.Count];
            for (int i = 0; i < list.Count; i++) output[i] = list[i] as string ?? "";
            return output;
        }
    }
}
