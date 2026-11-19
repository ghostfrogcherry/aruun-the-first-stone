using System;
using System.Collections.Generic;
using Aruun.Core.Content;
using Aruun.Core.Items;
using Aruun.Core.Language;

namespace Aruun.Core.Inscription
{
    /// <summary>
    /// How an inscription works. Not every word is a permanent upgrade:
    /// passive words are always on, activated ones cost stamina when you use them,
    /// conditional ones work by themselves when something holds, and compositional
    /// ones do nothing alone and only work together with another word.
    /// </summary>
    public enum InscriptionType { Passive, Activated, Conditional, Compositional }

    /// <summary>One way words act on an item: a single word's rule, or a composition of several words.</summary>
    public sealed class InscriptionRule
    {
        /// <summary>The word id for a word's rule, or the composition id.</summary>
        public string Id = "";
        /// <summary>The name shown for a composition ("Resonant strike"); "" for a single word.</summary>
        public string Name = "";
        /// <summary>The words it needs: one for a word's rule, two or more for a composition.</summary>
        public string[] Words = new string[0];
        public InscriptionType Type;
        /// <summary>Item classes it works on (items.json inscribe.class).</summary>
        public string[] Fits = new string[0];
        /// <summary>The effect code it runs, or "" for none (a compositional word alone).</summary>
        public string Effect = "";
        /// <summary>For a conditional rule: when it works ("resting", "always").</summary>
        public string When = "";
        public double Stamina, CooldownS, DurationS;
        public Dictionary<string, object> Params = new Dictionary<string, object>();
        public string Text = "";

        public bool IsComposition { get { return Words.Length > 1; } }
        public bool FitsClass(string cls) { return Array.IndexOf(Fits, cls) >= 0; }
        public double Num(string key, double fallback = 0) { return MiniJson.Num(Params, key, fallback); }
    }

    /// <summary>
    /// Changing one item into another at the table (tuning a ringing pick with singing quartz).
    /// The item keeps its words and wear.
    /// </summary>
    public sealed class Tuning
    {
        public string From = "", Into = "", NeedsWord = "", Text = "";
        public Dictionary<string, int> In = new Dictionary<string, int>();
    }

    /// <summary>
    /// Every inscription rule, read from content/language/inscriptions.json. Immutable after
    /// loading. Effects are a fixed list of code paths with hard limits: content picks numbers
    /// inside those limits, it never names code that does not exist.
    /// </summary>
    public sealed class InscriptionRules
    {
        /// <summary>Effects the host knows how to run.</summary>
        public static readonly string[] Handlers = { "resonant_mining", "fortify", "reclaim", "shockwave" };
        /// <summary>Conditions a conditional rule may name.</summary>
        public static readonly string[] Conditions = { "resting", "always" };

        readonly Dictionary<string, InscriptionRule> _words = new Dictionary<string, InscriptionRule>();
        readonly List<InscriptionRule> _compositions = new List<InscriptionRule>();
        readonly Dictionary<string, Dictionary<string, object>> _limits = new Dictionary<string, Dictionary<string, object>>();
        readonly List<Dictionary<string, int>> _slotCosts = new List<Dictionary<string, int>>();
        readonly List<Tuning> _tunings = new List<Tuning>();

        /// <summary>The tuning that changes this item, or null.</summary>
        public Tuning TuningFrom(string itemKey)
        {
            foreach (var t in _tunings) if (t.From == itemKey) return t;
            return null;
        }

        public IEnumerable<InscriptionRule> WordRules { get { return _words.Values; } }
        public IReadOnlyList<InscriptionRule> Compositions { get { return _compositions; } }

        /// <summary>The rule for one word alone, or null when the word cannot be inscribed by itself.</summary>
        public InscriptionRule Word(string word) { InscriptionRule r; return _words.TryGetValue(word, out r) ? r : null; }

        public InscriptionRule Composition(string id)
        {
            foreach (var c in _compositions) if (c.Id == id) return c;
            return null;
        }

        /// <summary>What it costs to cut a word into slot `index` (0 is the first word), from the bag.</summary>
        public Dictionary<string, int> SlotCost(int index)
        {
            if (_slotCosts.Count == 0) return new Dictionary<string, int>();
            return _slotCosts[Math.Min(index, _slotCosts.Count - 1)];
        }

        /// <summary>Does this word take part in inscriptions at all (alone or in a composition)?</summary>
        public bool Knows(string word)
        {
            if (_words.ContainsKey(word)) return true;
            foreach (var c in _compositions) if (Array.IndexOf(c.Words, word) >= 0) return true;
            return false;
        }

        /// <summary>Could this word ever do something on an item of this class (alone, or with another word)?</summary>
        public bool Fits(string word, string cls)
        {
            InscriptionRule r = Word(word);
            if (r != null && r.FitsClass(cls)) return true;
            foreach (var c in _compositions)
                if (Array.IndexOf(c.Words, word) >= 0 && c.FitsClass(cls)) return true;
            return false;
        }

        public static InscriptionRules FromJson(string json)
        {
            var root = MiniJson.ParseObject(json);
            var rules = new InscriptionRules();
            foreach (var x in MiniJson.List(root, "slot_costs"))
            {
                var cost = new Dictionary<string, int>();
                var o = x as Dictionary<string, object>;
                if (o != null) foreach (var kv in o) cost[kv.Key] = (int)(double)kv.Value;
                rules._slotCosts.Add(cost);
            }
            foreach (var x in MiniJson.List(root, "tunings"))
            {
                var o = x as Dictionary<string, object>;
                if (o == null) continue;
                var t = new Tuning { From = MiniJson.Str(o, "from"), Into = MiniJson.Str(o, "into"), NeedsWord = MiniJson.Str(o, "needs_word"), Text = MiniJson.Str(o, "text") };
                var inn = MiniJson.Obj(o, "in");
                if (inn != null) foreach (var kv in inn) t.In[kv.Key] = (int)(double)kv.Value;
                rules._tunings.Add(t);
            }
            foreach (var x in MiniJson.List(root, "effects"))
            {
                var o = x as Dictionary<string, object>;
                if (o != null) rules._limits[MiniJson.Str(o, "id")] = o;
            }
            foreach (var x in MiniJson.List(root, "words"))
            {
                var o = x as Dictionary<string, object>;
                if (o == null) continue;
                var r = Read(o);
                r.Id = MiniJson.Str(o, "word");
                r.Words = new[] { r.Id };
                if (rules._words.ContainsKey(r.Id)) throw new FormatException("inscriptions.json: word '" + r.Id + "' has two rules");
                rules._words[r.Id] = r;
            }
            foreach (var x in MiniJson.List(root, "compositions"))
            {
                var o = x as Dictionary<string, object>;
                if (o == null) continue;
                var r = Read(o);
                r.Id = MiniJson.Str(o, "id");
                r.Name = MiniJson.Str(o, "name", r.Id);
                r.Words = Strings(o, "words");
                rules._compositions.Add(r);
            }
            return rules;
        }

        static InscriptionRule Read(Dictionary<string, object> o)
        {
            var r = new InscriptionRule
            {
                Type = ParseType(MiniJson.Str(o, "type")),
                Fits = Strings(o, "fits"),
                Effect = MiniJson.Str(o, "effect"),
                When = MiniJson.Str(o, "when"),
                Stamina = MiniJson.Num(o, "stamina"),
                CooldownS = MiniJson.Num(o, "cooldown_s"),
                DurationS = MiniJson.Num(o, "duration_s"),
                Text = MiniJson.Str(o, "text"),
            };
            object p;
            if (o.TryGetValue("params", out p) && p is Dictionary<string, object>) r.Params = (Dictionary<string, object>)p;
            return r;
        }

        public static InscriptionType ParseType(string s)
        {
            switch (s)
            {
                case "passive": return InscriptionType.Passive;
                case "activated": return InscriptionType.Activated;
                case "conditional": return InscriptionType.Conditional;
                case "compositional": return InscriptionType.Compositional;
                default: throw new FormatException("inscriptions.json: unknown type '" + s + "'");
            }
        }

        static string[] Strings(Dictionary<string, object> o, string key)
        {
            var list = new List<string>();
            foreach (var x in MiniJson.List(o, key)) if (x is string) list.Add((string)x);
            return list.ToArray();
        }

        double Limit(string effect, string key, double fallback)
        {
            Dictionary<string, object> l;
            return _limits.TryGetValue(effect, out l) ? MiniJson.Num(l, key, fallback) : fallback;
        }

        /// <summary>Content mistakes, in plain words. Empty means the rules are sound. Pass the lexicon and items to check names against them.</summary>
        public List<string> Validate(Lexicon lexicon, ItemRegistry items)
        {
            var problems = new List<string>();
            var all = new List<InscriptionRule>(_words.Values);
            all.AddRange(_compositions);
            foreach (var r in all)
            {
                string what = (r.IsComposition ? "composition '" : "word rule '") + r.Id + "'";
                foreach (string w in r.Words)
                    if (lexicon != null && !lexicon.Has(w)) problems.Add(what + " uses unknown word '" + w + "'");
                if (r.Fits.Length == 0) problems.Add(what + " fits nothing");
                if (r.IsComposition)
                {
                    if (r.Type == InscriptionType.Compositional) problems.Add(what + " must say what the words make (passive, activated or conditional)");
                    var distinct = new HashSet<string>(r.Words);
                    if (distinct.Count != r.Words.Length) problems.Add(what + " repeats a word");
                }
                if (r.Type == InscriptionType.Compositional)
                {
                    if (r.Effect.Length > 0) problems.Add(what + ": a compositional word has no effect by itself");
                    continue;
                }
                if (Array.IndexOf(Handlers, r.Effect) < 0) { problems.Add(what + " uses unknown effect '" + r.Effect + "'"); continue; }
                if (r.Type == InscriptionType.Activated && (r.Stamina <= 0 || r.CooldownS <= 0)) problems.Add(what + ": an activated inscription needs a stamina cost and a cooldown");
                if (r.Type == InscriptionType.Conditional && Array.IndexOf(Conditions, r.When) < 0) problems.Add(what + " has unknown condition '" + r.When + "'");
                switch (r.Effect)
                {
                    case "resonant_mining":
                        if (r.Type != InscriptionType.Activated) problems.Add(what + ": resonant mining must be activated");
                        if (r.Num("dig_mult") < 1 || r.Num("dig_mult") > Limit(r.Effect, "max_dig_mult", 4)) problems.Add(what + ": dig_mult out of bounds");
                        if (r.Num("vein_max") < 0 || r.Num("vein_max") > Limit(r.Effect, "max_vein_max", 64)) problems.Add(what + ": vein_max out of bounds");
                        if (r.Num("vein_radius_m") < 0 || r.Num("vein_radius_m") > Limit(r.Effect, "max_vein_radius_m", 3)) problems.Add(what + ": vein_radius_m out of bounds");
                        if (r.DurationS <= 0 || r.DurationS > Limit(r.Effect, "max_duration_s", 30)) problems.Add(what + ": duration out of bounds");
                        if (r.Num("power_bonus") < 0 || r.Num("power_bonus") > Limit(r.Effect, "max_power_bonus", 2)) problems.Add(what + ": power_bonus out of bounds");
                        break;
                    case "fortify":
                        if (r.Num("wear_mult", -1) < Limit(r.Effect, "min_wear_mult", 0.2) || r.Num("wear_mult", -1) > 1) problems.Add(what + ": wear_mult out of bounds");
                        break;
                    case "reclaim":
                        if (r.Num("mend_per_min") <= 0 || r.Num("mend_per_min") > Limit(r.Effect, "max_mend_per_min", 60)) problems.Add(what + ": mend_per_min out of bounds");
                        break;
                    case "shockwave":
                        if (r.Type != InscriptionType.Activated) problems.Add(what + ": a shockwave must be activated");
                        if (r.Num("radius_m") <= 0 || r.Num("radius_m") > Limit(r.Effect, "max_radius_m", 8)) problems.Add(what + ": radius out of bounds");
                        if (r.Num("impulse_mps") < 0 || r.Num("impulse_mps") > Limit(r.Effect, "max_impulse_mps", 20)) problems.Add(what + ": impulse out of bounds");
                        if (r.Num("damage") < 0 || r.Num("damage") > Limit(r.Effect, "max_damage", 40)) problems.Add(what + ": damage out of bounds");
                        break;
                }
            }
            if (items != null)
                foreach (var kv in _slotCosts)
                    foreach (var c in kv)
                        if (!items.Has(c.Key)) problems.Add("slot cost uses unknown item '" + c.Key + "'");
            foreach (var t in _tunings)
            {
                string what = "tuning of '" + t.From + "'";
                if (items != null && (!items.Has(t.From) || !items.Has(t.Into))) problems.Add(what + " names an unknown item");
                if (lexicon != null && t.NeedsWord.Length > 0 && !lexicon.Has(t.NeedsWord)) problems.Add(what + " needs unknown word '" + t.NeedsWord + "'");
                if (items != null) foreach (var c in t.In) if (!items.Has(c.Key)) problems.Add(what + " uses unknown item '" + c.Key + "'");
            }
            return problems;
        }
    }
}
