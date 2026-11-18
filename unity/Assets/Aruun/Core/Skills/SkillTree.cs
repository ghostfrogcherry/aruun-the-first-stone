using System;
using System.Collections.Generic;
using Aruun.Core.Content;
using Aruun.Core.Survival;

namespace Aruun.Core.Skills
{
    /// <summary>One node of a discipline's tree: what it costs, what it needs first and what it changes.</summary>
    public sealed class SkillNode
    {
        public string Id = "";
        public string Name = "";
        public string Text = "";
        public int Cost = 1;
        public List<string> Requires = new List<string>();
        /// <summary>Modifiers while the node is learned. Keys ending in _mult multiply, others add (see Mods).</summary>
        public Dictionary<string, double> Mods = new Dictionary<string, double>();
        /// <summary>A keystone is a big change with a price. Only one keystone per tree.</summary>
        public bool Keystone;
        /// <summary>Where the node sits in the tree view, in grid cells (0,0 is the root).</summary>
        public int X, Y;
    }

    /// <summary>
    /// A discipline's small, PoE-style skill tree (content/skills/*.json). Points come from
    /// learning words, finishing quests and felling guardians. Pure logic: the host checks every
    /// unlock and folds learned nodes into the character's modifiers each tick.
    /// </summary>
    public sealed class SkillTree
    {
        public string Discipline = "";
        public string Name = "";
        public int StartPoints = 1;
        public readonly Dictionary<string, int> Awards = new Dictionary<string, int>();
        public readonly List<SkillNode> Nodes = new List<SkillNode>();
        readonly Dictionary<string, SkillNode> _byId = new Dictionary<string, SkillNode>();

        public static SkillTree FromJson(string json)
        {
            var o = MiniJson.ParseObject(json);
            var t = new SkillTree
            {
                Discipline = MiniJson.Str(o, "discipline"),
                Name = MiniJson.Str(o, "name"),
                StartPoints = MiniJson.Int(o, "start_points", 1),
            };
            foreach (var kv in MiniJson.Obj(o, "awards"))
                if (kv.Value is double) t.Awards[kv.Key] = (int)(double)kv.Value;
            foreach (object item in MiniJson.List(o, "nodes"))
            {
                var n = item as Dictionary<string, object>;
                if (n == null) continue;
                var node = new SkillNode
                {
                    Id = MiniJson.Str(n, "id"),
                    Name = MiniJson.Str(n, "name"),
                    Text = MiniJson.Str(n, "text"),
                    Cost = Math.Max(1, MiniJson.Int(n, "cost", 1)),
                    Keystone = MiniJson.Bool(n, "keystone"),
                    X = MiniJson.Int(n, "x"),
                    Y = MiniJson.Int(n, "y"),
                };
                foreach (object r in MiniJson.List(n, "requires")) if (r is string) node.Requires.Add((string)r);
                foreach (var kv in MiniJson.Obj(n, "mods")) if (kv.Value is double) node.Mods[kv.Key] = (double)kv.Value;
                if (node.Id.Length == 0 || t._byId.ContainsKey(node.Id)) throw new FormatException("skill tree: missing or repeated node id '" + node.Id + "'");
                t.Nodes.Add(node);
                t._byId[node.Id] = node;
            }
            return t;
        }

        public bool Has(string id) { return _byId.ContainsKey(id); }
        public SkillNode Get(string id) { return _byId[id]; }

        /// <summary>Points a trigger ("word", "quest", "boss") gives, or 0.</summary>
        public int Award(string why)
        {
            int n;
            return Awards.TryGetValue(why, out n) ? n : 0;
        }

        /// <summary>Why this character can't learn the node now, or "" when they can.</summary>
        public string WhyNot(CharacterState c, string id)
        {
            SkillNode node;
            if (!_byId.TryGetValue(id, out node)) return "no such skill";
            if (c.Skills.Contains(id)) return "you already have " + node.Name;
            foreach (string r in node.Requires)
                if (!c.Skills.Contains(r)) return "needs " + (_byId.ContainsKey(r) ? _byId[r].Name : r) + " first";
            if (node.Keystone)
                foreach (string s in c.Skills)
                    if (_byId.ContainsKey(s) && _byId[s].Keystone) return "you already follow " + _byId[s].Name + "; a Graviturge holds one keystone";
            if (c.SkillPoints < node.Cost) return "needs " + node.Cost + (node.Cost == 1 ? " point" : " points") + " (you have " + c.SkillPoints + ")";
            return "";
        }

        /// <summary>Learns the node, paying its cost. Returns "" or the reason it failed.</summary>
        public string Learn(CharacterState c, string id)
        {
            string why = WhyNot(c, id);
            if (why.Length > 0) return why;
            c.SkillPoints -= _byId[id].Cost;
            c.Skills.Add(id);
            return "";
        }

        /// <summary>Folds every learned node's modifiers into `mods`.</summary>
        public void AddMods(CharacterState c, Mods mods)
        {
            foreach (string id in c.Skills)
            {
                SkillNode node;
                if (_byId.TryGetValue(id, out node)) mods.Add(node.Mods);
            }
        }

        /// <summary>Problems with the tree's own data: unknown requirements, cycles, nodes nothing can reach.</summary>
        public List<string> Validate()
        {
            var problems = new List<string>();
            foreach (var n in Nodes)
                foreach (string r in n.Requires)
                    if (!_byId.ContainsKey(r)) problems.Add(n.Id + " requires unknown node " + r);
            // Every node must be reachable from the roots by learning requirements in order.
            var learned = new HashSet<string>();
            bool grew = true;
            while (grew)
            {
                grew = false;
                foreach (var n in Nodes)
                    if (!learned.Contains(n.Id) && n.Requires.TrueForAll(learned.Contains)) { learned.Add(n.Id); grew = true; }
            }
            foreach (var n in Nodes) if (!learned.Contains(n.Id)) problems.Add(n.Id + " can never be learned (cycle or missing root)");
            return problems;
        }
    }
}
