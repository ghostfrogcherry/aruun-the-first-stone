using System;
using System.Collections.Generic;
using Aruun.Core.Content;

namespace Aruun.Core.Farming
{
    public struct Yield
    {
        public string Item;
        public int Min, Max;
    }

    /// <summary>A plant or gatherable node from content/farming/plants.json.</summary>
    public sealed class PlantDef
    {
        public string Key = "";
        public string Name = "";
        public int Stages = 1;
        /// <summary>Seconds per growth stage. Ripe at the last stage.</summary>
        public double StageSeconds;
        /// <summary>The stage it drops back to after a harvest, or -1 when it is used up.</summary>
        public int RegrowStage = -1;
        /// <summary>A gatherable node (flint) rather than a crop.</summary>
        public bool Node;
        /// <summary>Block keys it can be planted on.</summary>
        public List<string> Ground = new List<string>();
        public List<Yield> Yields = new List<Yield>();
        /// <summary>Wild placement: average count per cell and the kind of place (shore, mesa, meadow, land).</summary>
        public double WildPerCell;
        public string WildWhere = "";
        public Dictionary<string, object> Raw = new Dictionary<string, object>();
    }

    /// <summary>
    /// Crop rules. A plant's stage is worked out from when it was planted, so nothing ticks per
    /// plant and an unloaded field still grows. Tether "growth_mult" speeds it up.
    /// </summary>
    public sealed class Plants
    {
        readonly Dictionary<string, PlantDef> _byKey = new Dictionary<string, PlantDef>();
        readonly List<PlantDef> _order = new List<PlantDef>();

        /// <summary>Wild nodes are placed per cell of this many voxels.</summary>
        public int CellVoxels = 64;
        /// <summary>Seconds before a harvested wild node comes back.</summary>
        public double WildRegrowSeconds = 900;

        public IReadOnlyList<PlantDef> All { get { return _order; } }

        public PlantDef Get(string key)
        {
            PlantDef p;
            return key != null && _byKey.TryGetValue(key, out p) ? p : null;
        }

        public static Plants FromJson(string json)
        {
            var root = MiniJson.ParseObject(json);
            var set = new Plants
            {
                CellVoxels = MiniJson.Int(root, "cell_voxels", 64),
                WildRegrowSeconds = MiniJson.Num(root, "wild_regrow_s", 900),
            };
            foreach (var x in MiniJson.List(root, "plants"))
            {
                var o = (Dictionary<string, object>)x;
                var p = new PlantDef
                {
                    Key = MiniJson.Str(o, "key"), Stages = Math.Max(1, MiniJson.Int(o, "stages", 1)),
                    StageSeconds = MiniJson.Num(o, "stage_s"), RegrowStage = MiniJson.Int(o, "regrow_stage", -1),
                    Node = MiniJson.Bool(o, "node"), Raw = o,
                };
                p.Name = MiniJson.Str(o, "name", p.Key);
                foreach (var g in MiniJson.List(o, "ground")) p.Ground.Add((string)g);
                foreach (var y in MiniJson.List(o, "yields"))
                {
                    var yo = (Dictionary<string, object>)y;
                    p.Yields.Add(new Yield { Item = MiniJson.Str(yo, "item"), Min = MiniJson.Int(yo, "min", 1), Max = MiniJson.Int(yo, "max", 1) });
                }
                object w;
                if (o.TryGetValue("wild", out w) && w is Dictionary<string, object>)
                {
                    var wo = (Dictionary<string, object>)w;
                    p.WildPerCell = MiniJson.Num(wo, "per_cell");
                    p.WildWhere = MiniJson.Str(wo, "where", "land");
                }
                set._byKey[p.Key] = p;
                set._order.Add(p);
            }
            return set;
        }

        /// <summary>Growth stage 0..Stages-1. mult above 1 grows faster.</summary>
        public static int Stage(PlantDef p, double plantedAt, double now, double mult = 1.0)
        {
            if (p.Stages <= 1 || p.StageSeconds <= 0) return p.Stages - 1;
            int s = (int)Math.Floor((now - plantedAt) * mult / p.StageSeconds);
            return Math.Max(0, Math.Min(p.Stages - 1, s));
        }

        public static bool IsRipe(PlantDef p, double plantedAt, double now, double mult = 1.0)
        {
            return Stage(p, plantedAt, now, mult) >= p.Stages - 1;
        }

        public static bool CanPlant(PlantDef p, string groundKey) { return p.Ground.Contains(groundKey); }

        /// <summary>Rolls a ripe plant's yield. `roll(min, max)` returns a whole number in [min, max].</summary>
        public static List<KeyValuePair<string, int>> Harvest(PlantDef p, Func<int, int, int> roll)
        {
            var output = new List<KeyValuePair<string, int>>();
            foreach (var y in p.Yields)
            {
                int n = roll(y.Min, y.Max);
                if (n > 0) output.Add(new KeyValuePair<string, int>(y.Item, n));
            }
            return output;
        }

        /// <summary>After a harvest: the planted time that puts the plant back at its regrow stage, or null when it is used up.</summary>
        public static double? RegrowTime(PlantDef p, double now, double mult = 1.0)
        {
            if (p.RegrowStage < 0) return null;
            return now - p.RegrowStage * p.StageSeconds / mult;
        }
    }
}
