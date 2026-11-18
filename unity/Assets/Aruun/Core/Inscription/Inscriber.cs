using System;
using System.Collections.Generic;
using Aruun.Core.Content;
using Aruun.Core.Items;
using Aruun.Core.Language;

namespace Aruun.Core.Inscription
{
    /// <summary>What an item's inscription does right now: the rules that are working, and the words that wait.</summary>
    public sealed class Resolved
    {
        /// <summary>Rules in effect, compositions first, in content order.</summary>
        public readonly List<InscriptionRule> Active = new List<InscriptionRule>();
        /// <summary>Words on the item that do nothing yet (a compositional word with no partner, or a word that does not suit this item).</summary>
        public readonly List<string> Waiting = new List<string>();

        public InscriptionRule First(InscriptionType type)
        {
            foreach (var r in Active) if (r.Type == type) return r;
            return null;
        }

        public InscriptionRule WithEffect(string effect)
        {
            foreach (var r in Active) if (r.Effect == effect) return r;
            return null;
        }

        public bool Any { get { return Active.Count > 0; } }
    }

    /// <summary>
    /// The grammar of inscriptions, as pure functions: which words can go on which item, and
    /// what a set of words does. The host calls these; nothing here changes the world. A word
    /// is only ever a stable id looked up in the rules, so no string on an item can do anything
    /// the rules do not list.
    /// </summary>
    public static class Inscriber
    {
        /// <summary>Words inscribed on one item can never be more than this, whatever content says.</summary>
        public const int MaxSlots = 4;

        /// <summary>How many words this item can carry, and what it is to the words. 0 slots: it cannot be inscribed.</summary>
        public static int Slots(ItemDef def, out string cls)
        {
            cls = "";
            if (def == null || def.Stack != 1) return 0;
            var o = MiniJson.Obj(def.Raw, "inscribe");
            if (o == null) return 0;
            cls = MiniJson.Str(o, "class");
            return Math.Max(0, Math.Min(MaxSlots, MiniJson.Int(o, "slots")));
        }

        public static string ClassOf(ItemDef def) { string c; Slots(def, out c); return c; }

        /// <summary>
        /// "" when `word` may be cut into an item of this kind that already carries `current`,
        /// for a character with this knowledge; otherwise why not, in plain words. Costs and the
        /// table are the host's business.
        /// </summary>
        public static string WhyNot(InscriptionRules rules, Lexicon lexicon, ItemDef item, IList<string> current, string word, WordKnowledge k)
        {
            string cls;
            int slots = Slots(item, out cls);
            if (slots == 0) return (item != null ? item.Name : "That") + " cannot carry words";
            if (lexicon == null || !lexicon.Has(word)) return "that is not a word anyone remembers";
            if (k != null)
            {
                Familiarity f = k.Of(word);
                if (f == Familiarity.Unseen) return "you have never seen that word";
                if (f == Familiarity.Seen) return "you have seen it only once: find it written in a second place";
            }
            if (!rules.Knows(word)) return "that word does not take to things";
            if (current != null && current.Contains(word)) return "it already carries that word";
            if (current != null && current.Count >= slots) return "it has no room for another word";
            if (!rules.Fits(word, cls)) return "that word does nothing for a " + cls;
            return "";
        }

        /// <summary>
        /// What these words do on an item of class `cls`. Compositions are matched first, in
        /// content order, and use up their words; the words left act alone if their own rule
        /// suits the item. Ids the rules do not know are ignored. The same words always give the
        /// same answer.
        /// </summary>
        public static Resolved Resolve(InscriptionRules rules, IList<string> words, string cls)
        {
            var res = new Resolved();
            if (rules == null || words == null || words.Count == 0) return res;
            var left = new List<string>();
            foreach (string w in words) if (!left.Contains(w)) left.Add(w);
            foreach (var c in rules.Compositions)
            {
                if (!c.FitsClass(cls)) continue;
                bool all = true;
                foreach (string w in c.Words) all &= left.Contains(w);
                if (!all) continue;
                res.Active.Add(c);
                foreach (string w in c.Words) left.Remove(w);
            }
            foreach (string w in left)
            {
                InscriptionRule r = rules.Word(w);
                if (r != null && r.Type != InscriptionType.Compositional && r.FitsClass(cls)) res.Active.Add(r);
                else res.Waiting.Add(w);
            }
            return res;
        }

        public static Resolved Resolve(InscriptionRules rules, ItemRegistry items, ItemStack? stack)
        {
            if (!stack.HasValue || !stack.Value.Inscribed) return new Resolved();
            ItemDef def;
            if (!items.TryGet(stack.Value.Key, out def)) return new Resolved();
            return Resolve(rules, stack.Value.WordList, ClassOf(def));
        }

        /// <summary>The composition that adding `word` to `current` would newly form on this item class, or null.</summary>
        public static InscriptionRule Forms(InscriptionRules rules, IList<string> current, string word, string cls)
        {
            var before = Resolve(rules, current, cls);
            var after = new List<string>(current) { word };
            foreach (var r in Resolve(rules, after, cls).Active)
                if (r.IsComposition && !before.Active.Contains(r)) return r;
            return null;
        }
    }
}
