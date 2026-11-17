using System;
using System.Collections.Generic;
using System.Globalization;
using Aruun.Core.Content;

namespace Aruun.Core.Art
{
    /// <summary>One rig part: which part it hangs from and where it pivots (model voxels, Unity axes).</summary>
    public sealed class RigPart
    {
        public string Name = "";
        public string Parent = "";
        public float PivotX, PivotY, PivotZ;
    }

    /// <summary>
    /// One channel of a procedural clip: swings a part around one axis with a sine wave, or bobs it.
    /// Angles are in degrees as authored for Godot; RigPose converts them for Unity.
    /// </summary>
    public sealed class RigChannel
    {
        public string Part = "";
        public char Axis = 'x';
        public float Amp, Freq, Phase, Offset;
        /// <summary>When set, the channel lifts the part this many voxels instead of rotating it.</summary>
        public float? Bob;
    }

    public sealed class ModelInfo
    {
        public string Id = "";
        public string File = "";
        public string Category = "";
        public string Note = "";
        public float VoxelMeters = 0.0625f;
        public float HeightMeters;
        /// <summary>Parents before children.</summary>
        public readonly List<RigPart> Parts = new List<RigPart>();
        public readonly Dictionary<string, List<RigChannel>> Clips = new Dictionary<string, List<RigChannel>>();
        public bool IsRigged { get { return Parts.Count > 0; } }
    }

    /// <summary>
    /// content/models/models.json plus the glowing colours from content/art/palette.json.
    /// Plain data, no engine types, so it is tested without Unity.
    /// </summary>
    public sealed class ModelCatalog
    {
        readonly Dictionary<string, ModelInfo> _models = new Dictionary<string, ModelInfo>();
        readonly HashSet<uint> _glow = new HashSet<uint>();

        public IEnumerable<string> Ids { get { return _models.Keys; } }
        public int Count { get { return _models.Count; } }
        public bool Has(string id) { return _models.ContainsKey(id); }

        public ModelInfo Get(string id)
        {
            ModelInfo m;
            if (!_models.TryGetValue(id, out m)) throw new KeyNotFoundException("No model called '" + id + "'");
            return m;
        }

        public List<string> InCategory(string category)
        {
            var list = new List<string>();
            foreach (var kv in _models) if (category == "" || kv.Value.Category == category) list.Add(kv.Key);
            list.Sort(StringComparer.Ordinal);
            return list;
        }

        /// <summary>Glow is decided by colour, not palette slot, so files re-saved by other editors still glow.</summary>
        public bool Glows(uint rgb) { return _glow.Contains(rgb); }

        public bool[] GlowFlags(VoxFile file)
        {
            var flags = new bool[256];
            for (int i = 1; i < 256; i++) flags[i] = Glows(file.Palette[i]);
            return flags;
        }

        public static ModelCatalog FromJson(string modelsJson, string paletteJson)
        {
            var cat = new ModelCatalog();
            if (paletteJson != null)
                foreach (var item in MiniJson.List(MiniJson.ParseObject(paletteJson), "colors"))
                {
                    var c = item as Dictionary<string, object>;
                    if (c != null && MiniJson.Bool(c, "emissive")) cat._glow.Add(ParseHex(MiniJson.Str(c, "hex")));
                }

            var root = MiniJson.ParseObject(modelsJson);
            object modelsObj;
            var models = root.TryGetValue("models", out modelsObj) ? modelsObj as Dictionary<string, object> : null;
            if (models == null) throw new FormatException("models.json: no 'models' object");
            foreach (var kv in models)
            {
                var o = kv.Value as Dictionary<string, object>;
                if (o == null) continue;
                var m = new ModelInfo
                {
                    Id = kv.Key,
                    File = MiniJson.Str(o, "file"),
                    Category = MiniJson.Str(o, "category"),
                    Note = MiniJson.Str(o, "note"),
                    VoxelMeters = (float)MiniJson.Num(o, "voxel_m", 0.0625),
                    HeightMeters = (float)MiniJson.Num(o, "height_m", 0),
                };
                object rigObj;
                var rig = o.TryGetValue("rig", out rigObj) ? rigObj as Dictionary<string, object> : null;
                if (rig != null) ReadRig(m, rig);
                cat._models[m.Id] = m;
            }
            return cat;
        }

        static void ReadRig(ModelInfo m, Dictionary<string, object> rig)
        {
            object partsObj, clipsObj;
            var parts = rig.TryGetValue("parts", out partsObj) ? partsObj as Dictionary<string, object> : null;
            if (parts != null)
            {
                var pending = new List<RigPart>();
                foreach (var kv in parts)
                {
                    var p = kv.Value as Dictionary<string, object>;
                    if (p == null) continue;
                    var pivot = MiniJson.List(p, "pivot");
                    pending.Add(new RigPart
                    {
                        Name = kv.Key,
                        Parent = MiniJson.Str(p, "parent"),
                        PivotX = Num(pivot, 0), PivotY = Num(pivot, 1), PivotZ = -Num(pivot, 2), // Godot z points the other way
                    });
                }
                var placed = new HashSet<string>();
                for (int guard = 0; pending.Count > 0 && guard < 64; guard++) // parents first
                    for (int i = 0; i < pending.Count; i++)
                    {
                        var p = pending[i];
                        if (p.Parent != "" && !placed.Contains(p.Parent)) continue;
                        m.Parts.Add(p);
                        placed.Add(p.Name);
                        pending.RemoveAt(i--);
                    }
                if (pending.Count > 0) throw new FormatException("models.json: '" + m.Id + "' has rig parts with missing parents");
            }
            var clips = rig.TryGetValue("clips", out clipsObj) ? clipsObj as Dictionary<string, object> : null;
            if (clips == null) return;
            foreach (var kv in clips)
            {
                var list = new List<RigChannel>();
                var arr = kv.Value as List<object>;
                if (arr == null) continue;
                foreach (var item in arr)
                {
                    var c = item as Dictionary<string, object>;
                    if (c == null) continue;
                    var ch = new RigChannel
                    {
                        Part = MiniJson.Str(c, "part"),
                        Amp = (float)MiniJson.Num(c, "amp"),
                        Freq = (float)MiniJson.Num(c, "freq"),
                        Phase = (float)MiniJson.Num(c, "phase"),
                        Offset = (float)MiniJson.Num(c, "offset"),
                    };
                    string axis = MiniJson.Str(c, "axis", "x");
                    ch.Axis = axis.Length > 0 ? axis[0] : 'x';
                    if (c.ContainsKey("bob")) ch.Bob = (float)MiniJson.Num(c, "bob");
                    list.Add(ch);
                }
                m.Clips[kv.Key] = list;
            }
        }

        static float Num(List<object> list, int i)
        {
            if (i >= list.Count) return 0;
            return (float)System.Convert.ToDouble(list[i], CultureInfo.InvariantCulture);
        }

        public static uint ParseHex(string hex)
        {
            hex = hex.TrimStart('#');
            return hex.Length == 6 ? uint.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture) : 0;
        }
    }
}
