using System;
using System.Collections.Generic;
using System.Globalization;
using Aruun.Core.Mining;

namespace Aruun.Core.Content
{
    /// <summary>One kind of block, read from content/blocks/blocks.json.</summary>
    public sealed class BlockDef
    {
        public ushort Id;
        public string Key = "";
        public string Name = "";
        public bool Solid;
        /// <summary>False for things you walk through (water, grass tufts).</summary>
        public bool Collision;
        public bool Transparent;
        public bool Fluid;
        public bool Emissive;
        /// <summary>"cube" or "cross" (a small plant drawn as crossed quads).</summary>
        public string Shape = "cube";
        /// <summary>Dig power needed to break it. 99 and above cannot be broken.</summary>
        public int Hardness;
        /// <summary>What kind of tool breaks it: Hand (anything), Axe (wood) or Pick (stone and metal).</summary>
        public ToolClass Tool = ToolClass.Hand;
        /// <summary>How much digging one bite takes. A fitting tool wears off its dig speed per second.</summary>
        public double Health;
        public byte R, G, B, A = 255;
        /// <summary>
        /// Another colour that means this block in .vox files ("paint" in blocks.json), so art
        /// painted with an older palette entry keeps working after the block's colour changes. -1 for none.
        /// </summary>
        public int Paint = -1;

        public bool IsCube { get { return Id != 0 && Shape == "cube"; } }
        /// <summary>Hides the faces of neighbours behind it.</summary>
        public bool IsOpaqueCube { get { return IsCube && !Transparent; } }
        public bool Unbreakable { get { return Hardness >= 99; } }
    }

    /// <summary>
    /// All block kinds by id and by key. Ids are stable and index 0 is air.
    /// Immutable after loading, so worker threads can read it freely.
    /// </summary>
    public sealed class BlockRegistry
    {
        readonly BlockDef[] _byId;
        readonly Dictionary<string, BlockDef> _byKey = new Dictionary<string, BlockDef>();

        public int Count { get { return _byId.Length; } }
        public IReadOnlyList<string> Placeable { get; private set; }

        BlockRegistry(BlockDef[] byId, List<string> placeable)
        {
            _byId = byId;
            Placeable = placeable;
            foreach (var b in byId) _byKey[b.Key] = b;
        }

        public BlockDef Get(int id) { return id >= 0 && id < _byId.Length ? _byId[id] : _byId[0]; }

        public BlockDef Get(string key)
        {
            BlockDef b;
            if (!_byKey.TryGetValue(key, out b)) throw new KeyNotFoundException("No block called '" + key + "'");
            return b;
        }

        public ushort Id(string key) { return Get(key).Id; }
        public bool Has(string key) { return _byKey.ContainsKey(key); }

        public static BlockRegistry FromJson(string json)
        {
            var root = MiniJson.ParseObject(json);
            var list = MiniJson.List(root, "blocks");
            var defs = new BlockDef[list.Count];
            foreach (var item in list)
            {
                var o = item as Dictionary<string, object>;
                if (o == null) throw new FormatException("blocks.json: every block must be an object");
                var b = new BlockDef();
                int id = MiniJson.Int(o, "id", -1);
                if (id < 0 || id >= defs.Length) throw new FormatException("blocks.json: id " + id + " is out of order; ids must run 0.." + (defs.Length - 1));
                if (defs[id] != null) throw new FormatException("blocks.json: id " + id + " is used twice");
                b.Id = (ushort)id;
                b.Key = MiniJson.Str(o, "key");
                b.Name = MiniJson.Str(o, "name", b.Key);
                b.Solid = MiniJson.Bool(o, "solid");
                b.Collision = MiniJson.Bool(o, "collision", b.Solid);
                b.Transparent = MiniJson.Bool(o, "transparent");
                b.Fluid = MiniJson.Bool(o, "fluid");
                b.Emissive = MiniJson.Bool(o, "emissive");
                b.Shape = MiniJson.Str(o, "shape", "cube");
                b.Hardness = MiniJson.Int(o, "hardness");
                b.Tool = ToolClasses.Parse(MiniJson.Str(o, "tool"), ToolClass.Hand);
                if (b.Tool == ToolClass.Any) throw new FormatException("blocks.json: " + b.Key + " cannot need tool 'any'");
                b.Health = MiniJson.Num(o, "health", 2 + 4 * Math.Min(b.Hardness, 10));
                string hex = MiniJson.Str(o, "color", "#ff00ff");
                if (hex.Length != 7 || hex[0] != '#') throw new FormatException("blocks.json: bad color '" + hex + "' on " + b.Key);
                b.R = byte.Parse(hex.Substring(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                b.G = byte.Parse(hex.Substring(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                b.B = byte.Parse(hex.Substring(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                b.A = (byte)Math.Round(MiniJson.Num(o, "alpha", 1.0) * 255.0);
                string paint = MiniJson.Str(o, "paint");
                if (paint.Length == 7 && paint[0] == '#') b.Paint = int.Parse(paint.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                defs[id] = b;
            }
            for (int i = 0; i < defs.Length; i++)
                if (defs[i] == null) throw new FormatException("blocks.json: id " + i + " is missing");
            if (defs.Length == 0 || defs[0].Key != "air") throw new FormatException("blocks.json: id 0 must be air");
            var placeable = new List<string>();
            foreach (var p in MiniJson.List(root, "placeable")) placeable.Add(p as string ?? "");
            return new BlockRegistry(defs, placeable);
        }
    }
}
