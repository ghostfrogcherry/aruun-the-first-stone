using System;
using System.Collections.Generic;
using Aruun.Core.Content;
using Aruun.Core.Mining;

namespace Aruun.Core.Items
{
    /// <summary>One kind of item, read from content/items/items.json. Every breakable solid block is an item too.</summary>
    public sealed class ItemDef
    {
        public string Key = "";
        public string Name = "";
        /// <summary>block, material, food, seed, tool, weapon, gear or structure.</summary>
        public string Kind = "";
        public int Stack = 1;
        /// <summary>Uses before it breaks, or 0 for items that never wear.</summary>
        public int Durability;
        /// <summary>For kind "block": the block it places.</summary>
        public string Block = "";
        public string Color = "#888888";
        /// <summary>Every field from the JSON, for rules not ported yet (use, slam, gear...).</summary>
        public Dictionary<string, object> Raw = new Dictionary<string, object>();

        public bool Wears { get { return Durability > 0; } }
    }

    /// <summary>One crafting recipe.</summary>
    public sealed class Recipe
    {
        public string Id = "";
        public string Out = "";
        public int Count = 1;
        /// <summary>Item key to amount, in the order the file lists them.</summary>
        public List<KeyValuePair<string, int>> In = new List<KeyValuePair<string, int>>();
        /// <summary>A station that must be near, such as "fire" or "tether", or "".</summary>
        public string Station = "";
        /// <summary>Only someone who knows a word of Avarra can make it.</summary>
        public bool NeedsWord;
    }

    /// <summary>A placeable structure (campfire, tether) and what it does.</summary>
    public sealed class StructureDef
    {
        public string Key = "";
        public string Name = "";
        public string Station = "";
        public double HeatM;
        public Dictionary<string, object> Raw = new Dictionary<string, object>();
    }

    /// <summary>What digging one voxel can give.</summary>
    public struct Drop
    {
        public string Item;
        public int Count;
        public double Chance;
    }

    /// <summary>All items, recipes, structures and block drops. Immutable after loading.</summary>
    public sealed class ItemRegistry
    {
        readonly Dictionary<string, ItemDef> _items = new Dictionary<string, ItemDef>();
        readonly Dictionary<string, Recipe> _recipes = new Dictionary<string, Recipe>();
        readonly List<Recipe> _recipeOrder = new List<Recipe>();
        readonly Dictionary<string, StructureDef> _structures = new Dictionary<string, StructureDef>();
        readonly Dictionary<string, List<Drop>> _drops = new Dictionary<string, List<Drop>>();

        public ToolSet Tools { get; private set; }
        public BlockRegistry Blocks { get; private set; }
        public IReadOnlyList<Recipe> Recipes { get { return _recipeOrder; } }

        public bool Has(string key) { return key != null && _items.ContainsKey(key); }

        public ItemDef Get(string key)
        {
            ItemDef d;
            if (key == null || !_items.TryGetValue(key, out d)) throw new KeyNotFoundException("No item called '" + key + "'");
            return d;
        }

        public bool TryGet(string key, out ItemDef def)
        {
            def = null;
            return key != null && _items.TryGetValue(key, out def);
        }

        public Recipe Recipe(string id)
        {
            Recipe r;
            return id != null && _recipes.TryGetValue(id, out r) ? r : null;
        }

        public StructureDef Structure(string key)
        {
            StructureDef s;
            return key != null && _structures.TryGetValue(key, out s) ? s : null;
        }

        /// <summary>The structure that provides a crafting station, or null.</summary>
        public StructureDef StationStructure(string station)
        {
            foreach (var s in _structures.Values)
                if (s.Station == station) return s;
            return null;
        }

        /// <summary>
        /// What digging one voxel of this block gives. `roll` returns numbers in [0, 1) and
        /// decides the chance drops; pass the host's seeded random so every machine agrees.
        /// Blocks with no entry give themselves when their hardness is 1 or more.
        /// </summary>
        public List<KeyValuePair<string, int>> RollDrops(BlockDef block, Func<double> roll)
        {
            var output = new List<KeyValuePair<string, int>>();
            List<Drop> list;
            if (!_drops.TryGetValue(block.Key, out list))
            {
                if (block.Hardness >= 1 && Has(block.Key)) output.Add(new KeyValuePair<string, int>(block.Key, 1));
                return output;
            }
            foreach (var d in list)
                if (roll() < d.Chance) output.Add(new KeyValuePair<string, int>(d.Item, d.Count));
            return output;
        }

        public static ItemRegistry FromJson(string itemsJson, BlockRegistry blocks)
        {
            var root = MiniJson.ParseObject(itemsJson);
            var reg = new ItemRegistry { Blocks = blocks, Tools = ToolSet.FromJson(itemsJson) };
            int blockStack = MiniJson.Int(root, "block_stack", 999);
            for (int i = 1; i < blocks.Count; i++)
            {
                BlockDef b = blocks.Get(i);
                if (!b.Solid || b.Fluid || b.Unbreakable) continue;
                reg._items[b.Key] = new ItemDef
                {
                    Key = b.Key, Name = b.Name, Kind = "block", Stack = blockStack, Block = b.Key,
                    Color = string.Format("#{0:x2}{1:x2}{2:x2}", b.R, b.G, b.B),
                };
            }
            foreach (var item in MiniJson.List(root, "items"))
            {
                var o = item as Dictionary<string, object>;
                if (o == null) throw new FormatException("items.json: every item must be an object");
                var d = new ItemDef
                {
                    Key = MiniJson.Str(o, "key"),
                    Kind = MiniJson.Str(o, "kind"),
                    Stack = Math.Max(1, MiniJson.Int(o, "stack", 1)),
                    Durability = MiniJson.Int(o, "durability"),
                    Color = MiniJson.Str(o, "color", "#888888"),
                    Raw = o,
                };
                d.Name = MiniJson.Str(o, "name", d.Key);
                if (d.Key.Length == 0) throw new FormatException("items.json: an item has no key");
                if (d.Wears && d.Stack != 1) throw new FormatException("items.json: " + d.Key + " wears out, so its stack must be 1");
                reg._items[d.Key] = d;
            }
            object structs;
            if (root.TryGetValue("structures", out structs) && structs is Dictionary<string, object>)
            {
                foreach (var kv in (Dictionary<string, object>)structs)
                {
                    var o = kv.Value as Dictionary<string, object>;
                    if (o == null) continue;
                    reg._structures[kv.Key] = new StructureDef
                    {
                        Key = kv.Key, Name = MiniJson.Str(o, "name", kv.Key), Station = MiniJson.Str(o, "station"),
                        HeatM = MiniJson.Num(o, "heat_m"), Raw = o,
                    };
                }
            }
            object drops;
            if (root.TryGetValue("block_drops", out drops) && drops is Dictionary<string, object>)
            {
                foreach (var kv in (Dictionary<string, object>)drops)
                {
                    var list = kv.Value as List<object>;
                    if (list == null) continue;   // the _comment
                    var outList = new List<Drop>();
                    foreach (var x in list)
                    {
                        var o = (Dictionary<string, object>)x;
                        outList.Add(new Drop { Item = MiniJson.Str(o, "item"), Count = MiniJson.Int(o, "n", 1), Chance = MiniJson.Num(o, "chance", 1.0) });
                    }
                    reg._drops[kv.Key] = outList;
                }
            }
            foreach (var x in MiniJson.List(root, "recipes"))
            {
                var o = (Dictionary<string, object>)x;
                var r = new Recipe
                {
                    Id = MiniJson.Str(o, "id"), Out = MiniJson.Str(o, "out"), Count = MiniJson.Int(o, "n", 1),
                    Station = MiniJson.Str(o, "station"), NeedsWord = MiniJson.Bool(o, "needs_word"),
                };
                object ins;
                if (o.TryGetValue("in", out ins) && ins is Dictionary<string, object>)
                    foreach (var kv in (Dictionary<string, object>)ins)
                        r.In.Add(new KeyValuePair<string, int>(kv.Key, (int)(double)kv.Value));
                reg._recipes[r.Id] = r;
                reg._recipeOrder.Add(r);
            }
            reg.Check();
            return reg;
        }

        /// <summary>Every name a recipe or drop uses must be a real item, so typos fail at load, not in play.</summary>
        void Check()
        {
            foreach (var r in _recipeOrder)
            {
                if (!Has(r.Out)) throw new FormatException("items.json: recipe " + r.Id + " makes unknown item '" + r.Out + "'");
                foreach (var kv in r.In)
                    if (!Has(kv.Key)) throw new FormatException("items.json: recipe " + r.Id + " needs unknown item '" + kv.Key + "'");
                if (r.Station.Length > 0 && StationStructure(r.Station) == null)
                    throw new FormatException("items.json: recipe " + r.Id + " needs unknown station '" + r.Station + "'");
            }
            foreach (var kv in _drops)
                foreach (var d in kv.Value)
                    if (!Has(d.Item)) throw new FormatException("items.json: block " + kv.Key + " drops unknown item '" + d.Item + "'");
        }
    }
}
