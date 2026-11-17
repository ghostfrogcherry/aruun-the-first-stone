using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Aruun.Core.Art
{
    /// <summary>
    /// One object from a .vox file, already in Unity axes (x right, y up, z forward).
    /// Cells are dense: index = x + SizeX * (y + SizeY * z), value = palette index (0 = empty).
    /// Origin is where the lowest corner sits, in voxels, so parts saved as separate objects
    /// come out assembled.
    /// </summary>
    public sealed class VoxModel
    {
        public string Name = "";
        public int SizeX, SizeY, SizeZ;
        public int OriginX, OriginY, OriginZ;
        public byte[] Cells = new byte[0];

        public byte Get(int x, int y, int z)
        {
            if (x < 0 || y < 0 || z < 0 || x >= SizeX || y >= SizeY || z >= SizeZ) return 0;
            return Cells[x + SizeX * (y + SizeY * z)];
        }

        public int Count
        {
            get { int n = 0; foreach (byte c in Cells) if (c != 0) n++; return n; }
        }
    }

    /// <summary>
    /// Reads MagicaVoxel .vox files: sizes, voxels, palette and the scene graph (object names
    /// and positions). Rotated objects are read unrotated.
    ///
    /// Axes match the Godot reader mirrored on z, so rig pivots written for Godot
    /// (content/models/models.json) work by negating their z. Models face +z, Unity's forward.
    /// </summary>
    public sealed class VoxFile
    {
        /// <summary>256 colours as 0xRRGGBB. Index 0 is unused.</summary>
        public readonly uint[] Palette = new uint[256];
        public readonly List<VoxModel> Models = new List<VoxModel>();
        public bool HasPalette;

        public VoxModel Find(string name)
        {
            foreach (var m in Models) if (m.Name == name) return m;
            return null;
        }

        public static VoxFile Load(string path) { return Parse(File.ReadAllBytes(path)); }

        public static VoxFile Parse(byte[] bytes)
        {
            if (bytes.Length < 20 || Encoding.ASCII.GetString(bytes, 0, 4) != "VOX ")
                throw new FormatException("not a .vox file");
            var f = new VoxFile();
            for (int i = 0; i < 256; i++) f.Palette[i] = 0x808080;
            var sizes = new List<int[]>();
            var raw = new List<byte[]>();
            var transforms = new Dictionary<int, Transform>();
            var groups = new Dictionary<int, List<int>>();
            var shapes = new Dictionary<int, List<int>>();

            var r = new Reader(bytes) { Pos = 20 }; // "VOX " version "MAIN" content children
            while (r.Pos + 12 <= bytes.Length)
            {
                string id = Encoding.ASCII.GetString(bytes, r.Pos, 4);
                r.Pos += 4;
                int content = r.Int(), children = r.Int();
                int next = r.Pos + content + children;
                switch (id)
                {
                    case "SIZE":
                        sizes.Add(new[] { r.Int(), r.Int(), r.Int() });
                        break;
                    case "XYZI":
                    {
                        int count = r.Int();
                        var cells = new byte[count * 4];
                        Array.Copy(bytes, r.Pos, cells, 0, cells.Length);
                        raw.Add(cells);
                        break;
                    }
                    case "RGBA":
                        for (int i = 0; i < 255; i++)
                        {
                            int p = r.Pos + i * 4;
                            f.Palette[i + 1] = (uint)(bytes[p] << 16 | bytes[p + 1] << 8 | bytes[p + 2]);
                        }
                        f.HasPalette = true;
                        break;
                    case "nTRN":
                    {
                        int node = r.Int();
                        var attrs = r.Dict();
                        int child = r.Int();
                        r.Int(); r.Int(); // reserved, layer
                        int frames = r.Int();
                        var t = new int[3];
                        for (int k = 0; k < frames; k++)
                        {
                            var frame = r.Dict();
                            string tt;
                            if (k == 0 && frame.TryGetValue("_t", out tt))
                            {
                                var parts = tt.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                if (parts.Length == 3)
                                    for (int a = 0; a < 3; a++) t[a] = int.Parse(parts[a], System.Globalization.CultureInfo.InvariantCulture);
                            }
                        }
                        string name;
                        attrs.TryGetValue("_name", out name);
                        transforms[node] = new Transform { Name = name ?? "", Child = child, T = t };
                        break;
                    }
                    case "nGRP":
                    {
                        int node = r.Int();
                        r.Dict();
                        int n = r.Int();
                        var kids = new List<int>();
                        for (int k = 0; k < n; k++) kids.Add(r.Int());
                        groups[node] = kids;
                        break;
                    }
                    case "nSHP":
                    {
                        int node = r.Int();
                        r.Dict();
                        int n = r.Int();
                        var ids = new List<int>();
                        for (int k = 0; k < n; k++) { ids.Add(r.Int()); r.Dict(); }
                        shapes[node] = ids;
                        break;
                    }
                }
                r.Pos = next;
            }
            if (sizes.Count == 0 || sizes.Count != raw.Count) throw new FormatException("no models found");

            if (transforms.ContainsKey(0))
                f.Walk(0, new int[3], "", transforms, groups, shapes, sizes, raw, 0);
            if (f.Models.Count == 0) // old files without a scene graph: stand each model on the ground
                for (int i = 0; i < sizes.Count; i++)
                    f.Models.Add(Convert("model_" + i, sizes[i], raw[i], new[] { 0, 0, sizes[i][2] / 2 }));
            return f;
        }

        void Walk(int node, int[] offset, string name, Dictionary<int, Transform> transforms, Dictionary<int, List<int>> groups,
            Dictionary<int, List<int>> shapes, List<int[]> sizes, List<byte[]> raw, int depth)
        {
            if (depth > 64) return;
            Transform t;
            List<int> list;
            if (transforms.TryGetValue(node, out t))
            {
                var o = new[] { offset[0] + t.T[0], offset[1] + t.T[1], offset[2] + t.T[2] };
                Walk(t.Child, o, t.Name != "" ? t.Name : name, transforms, groups, shapes, sizes, raw, depth + 1);
            }
            else if (groups.TryGetValue(node, out list))
            {
                foreach (int kid in list) Walk(kid, offset, name, transforms, groups, shapes, sizes, raw, depth + 1);
            }
            else if (shapes.TryGetValue(node, out list))
            {
                foreach (int m in list)
                    if (m >= 0 && m < sizes.Count)
                        Models.Add(Convert(name != "" ? name : "model_" + m, sizes[m], raw[m], offset));
            }
        }

        /// <summary>vs = size in MagicaVoxel axes (z up); t = where MagicaVoxel puts the model's centre.</summary>
        static VoxModel Convert(string name, int[] vs, byte[] cells, int[] t)
        {
            var m = new VoxModel
            {
                Name = name, SizeX = vs[0], SizeY = vs[2], SizeZ = vs[1],
                OriginX = t[0] - vs[0] / 2,
                OriginY = t[2] - vs[2] / 2,
                OriginZ = t[1] - vs[1] / 2 - 1,
            };
            m.Cells = new byte[m.SizeX * m.SizeY * m.SizeZ];
            for (int i = 0; i + 3 < cells.Length; i += 4)
            {
                int x = cells[i], y = cells[i + 2], z = cells[i + 1];
                if (x < m.SizeX && y < m.SizeY && z < m.SizeZ)
                    m.Cells[x + m.SizeX * (y + m.SizeY * z)] = cells[i + 3];
            }
            return m;
        }

        sealed class Transform { public string Name; public int Child; public int[] T; }

        sealed class Reader
        {
            readonly byte[] _b;
            public int Pos;
            public Reader(byte[] b) { _b = b; }
            public int Int() { int v = BitConverter.ToInt32(_b, Pos); Pos += 4; return v; }
            public string Str() { int n = Int(); string s = Encoding.UTF8.GetString(_b, Pos, n); Pos += n; return s; }
            public Dictionary<string, string> Dict()
            {
                var d = new Dictionary<string, string>();
                int n = Int();
                for (int i = 0; i < n; i++) { string k = Str(); d[k] = Str(); }
                return d;
            }
        }
    }
}
