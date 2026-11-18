using System;
using System.Collections.Generic;
using Aruun.Core.Content;

namespace Aruun.Core.Language
{
    /// <summary>What a word does in a phrase.</summary>
    public enum WordRole
    {
        /// <summary>The verb: what happens (rise, push, bind, awaken). Every phrase has exactly one.</summary>
        Action,
        /// <summary>What it happens to (stone, beast, water). Optional; it narrows the target.</summary>
        Thing,
        /// <summary>How (greatly, briefly, far). Scales the action within its effect's limits.</summary>
        Modifier,
    }

    /// <summary>One Avarra word, read from content/language/lexicon.json.</summary>
    public sealed class WordDef
    {
        public string Id = "";
        public string Display = "";
        public string Meaning = "";
        public string Discipline = "";
        public WordRole Role = WordRole.Action;
        /// <summary>For an action: the effect it runs. For a thing: the target kind it names.</summary>
        public string Effect = "";
        public Dictionary<string, object> Params = new Dictionary<string, object>();
        public int RangeVoxels;
        public double CooldownSeconds;
        /// <summary>Gravitational debt the word costs (0 to 100 scale).</summary>
        public double Debt;
        /// <summary>For a modifier: how much it multiplies the action's strength and its debt.</summary>
        public double Scale = 1.0;
        public double DebtScale = 1.0;
        /// <summary>
        /// The word's symbol: rows of '#' (cut) and '.' (bare stone). The same glyph is carved in
        /// the ruins and cut into inscribed equipment, so the language looks the same everywhere.
        /// </summary>
        public string[] Glyph = new string[0];
        /// <summary>False for words that are only cut into things (inscriptions), never spoken.</summary>
        public bool Speakable = true;

        public double Num(string key, double fallback = 0) { return MiniJson.Num(Params, key, fallback); }
        public string Str(string key, string fallback = "") { return MiniJson.Str(Params, key, fallback); }
    }

    /// <summary>The hard limits of one effect. Words and modifiers can never push past them.</summary>
    public sealed class EffectDef
    {
        public string Id = "";
        /// <summary>What it can act on: solid_voxel, creature, creatures.</summary>
        public string Targets = "";
        public Dictionary<string, object> Limits = new Dictionary<string, object>();

        public double Max(string key, double fallback = 0) { return MiniJson.Num(Limits, key, fallback); }
    }

    /// <summary>All words and effects. Immutable after loading.</summary>
    public sealed class Lexicon
    {
        /// <summary>Effects the interpreter knows how to run. A word naming any other effect is a content error.</summary>
        public static readonly string[] Handlers = { "raise_column", "force_push", "force_pull", "force_weigh" };
        /// <summary>Glyphs are this many cells square.</summary>
        public const int GlyphSize = 5;

        readonly Dictionary<string, WordDef> _words = new Dictionary<string, WordDef>();
        readonly Dictionary<string, EffectDef> _effects = new Dictionary<string, EffectDef>();

        public IEnumerable<WordDef> Words { get { return _words.Values; } }
        public bool Has(string id) { return _words.ContainsKey(id); }
        public WordDef Word(string id) { WordDef w; return _words.TryGetValue(id, out w) ? w : null; }
        public EffectDef Effect(string id) { EffectDef e; return _effects.TryGetValue(id, out e) ? e : null; }

        public static Lexicon FromJson(string json)
        {
            var root = MiniJson.ParseObject(json);
            var lex = new Lexicon();
            foreach (var item in MiniJson.List(root, "effects"))
            {
                var o = item as Dictionary<string, object>;
                if (o == null) continue;
                var e = new EffectDef { Id = MiniJson.Str(o, "id"), Targets = MiniJson.Str(o, "targets"), Limits = o };
                lex._effects[e.Id] = e;
            }
            foreach (var item in MiniJson.List(root, "words"))
            {
                var o = item as Dictionary<string, object>;
                if (o == null) continue;
                var w = new WordDef
                {
                    Id = MiniJson.Str(o, "id"),
                    Display = MiniJson.Str(o, "display"),
                    Meaning = MiniJson.Str(o, "meaning"),
                    Discipline = MiniJson.Str(o, "discipline"),
                    Effect = MiniJson.Str(o, "effect"),
                    RangeVoxels = MiniJson.Int(o, "range_voxels"),
                    CooldownSeconds = MiniJson.Num(o, "cooldown_s"),
                    Debt = MiniJson.Num(o, "debt"),
                    Scale = MiniJson.Num(o, "scale", 1.0),
                    DebtScale = MiniJson.Num(o, "debt_scale", 1.0),
                    Speakable = MiniJson.Bool(o, "speakable", true),
                };
                var rows = new List<string>();
                foreach (var g in MiniJson.List(o, "glyph")) if (g is string) rows.Add((string)g);
                w.Glyph = rows.ToArray();
                object p;
                if (o.TryGetValue("params", out p) && p is Dictionary<string, object>) w.Params = (Dictionary<string, object>)p;
                string role = MiniJson.Str(o, "role", "action");
                if (role == "action") w.Role = WordRole.Action;
                else if (role == "thing") w.Role = WordRole.Thing;
                else if (role == "modifier") w.Role = WordRole.Modifier;
                else throw new FormatException("lexicon.json: word '" + w.Id + "' has unknown role '" + role + "'");
                if (lex._words.ContainsKey(w.Id)) throw new FormatException("lexicon.json: word '" + w.Id + "' is defined twice");
                lex._words[w.Id] = w;
            }
            return lex;
        }

        /// <summary>Content mistakes, in plain words. Empty means the lexicon is sound.</summary>
        public List<string> Validate()
        {
            var problems = new List<string>();
            foreach (EffectDef e in _effects.Values)
                if (Array.IndexOf(Handlers, e.Id) < 0) problems.Add("effect '" + e.Id + "' has no handler");
            var glyphs = new Dictionary<string, string>();
            foreach (WordDef w in _words.Values)
            {
                if (w.Glyph.Length == 0) { }   // a word without a glyph yet (tests, drafts); the shipped lexicon test asks for one
                else if (w.Glyph.Length != GlyphSize) problems.Add("word '" + w.Id + "': its glyph needs " + GlyphSize + " rows");
                else
                {
                    foreach (string row in w.Glyph)
                        if (row.Length != GlyphSize || row.Replace("#", "").Replace(".", "").Length > 0) problems.Add("word '" + w.Id + "': glyph rows are " + GlyphSize + " of '#' or '.'");
                    string key = string.Join("/", w.Glyph), other;
                    if (glyphs.TryGetValue(key, out other)) problems.Add("words '" + other + "' and '" + w.Id + "' share a glyph");
                    else glyphs[key] = w.Id;
                }
                if (!w.Speakable) continue;
                if (w.Role == WordRole.Modifier)
                {
                    if (w.Scale <= 0 || w.Scale > 4) problems.Add("modifier '" + w.Id + "': scale must be above 0 and at most 4");
                    continue;
                }
                if (w.Role == WordRole.Thing) continue;
                EffectDef e = Effect(w.Effect);
                if (e == null) { problems.Add("word '" + w.Id + "' uses unknown effect '" + w.Effect + "'"); continue; }
                if (w.Effect == "raise_column")
                {
                    if (w.Num("height") < 1 || w.Num("height") > e.Max("max_height")) problems.Add("word '" + w.Id + "': height out of bounds");
                    if (w.Num("radius", -1) < 0 || w.Num("radius", -1) > e.Max("max_radius")) problems.Add("word '" + w.Id + "': radius out of bounds");
                    if (w.Str("block") == "") problems.Add("word '" + w.Id + "': no block to raise");
                }
                else
                {
                    if (w.Num("range_m") <= 0 || w.Num("range_m") > e.Max("max_range_m")) problems.Add("word '" + w.Id + "': range out of bounds");
                    if (w.Num("impulse_mps") <= 0 || w.Num("impulse_mps") > e.Max("max_impulse_mps")) problems.Add("word '" + w.Id + "': impulse out of bounds");
                    if (e.Limits.ContainsKey("max_duration_s") && w.Num("duration_s") > e.Max("max_duration_s")) problems.Add("word '" + w.Id + "': duration out of bounds");
                }
            }
            return problems;
        }
    }
}
