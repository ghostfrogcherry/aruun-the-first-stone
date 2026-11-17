using System;
using System.Collections.Generic;
using System.IO;
using Aruun.Core.Art;
using Aruun.Core.Combat;
using Aruun.Core.Content;
using Aruun.Core.Ruins;
using Aruun.Core.Voxels;
using Aruun.Core.Worldgen;

namespace Aruun.Core.Town
{
    /// <summary>One building of the village, placed: a structure stamp standing on a levelled pad.</summary>
    public sealed class VillageBuilding
    {
        public string Key = "";
        /// <summary>Centre of the footprint and the floor layer, in world voxels.</summary>
        public int X, Z, Floor;
        public int MinX, MaxX, MinZ, MaxZ, Height;
        internal StructureStamp Stamp;
        internal int OffX, OffZ;
    }

    /// <summary>
    /// The village at the Teshari Confluence, the demo's home town: a paved plaza around the
    /// Confluence stone, Vaari dome houses, reed huts and a watch tower, and a paved road south
    /// to the echo ruin. Like the ruin it is part of world generation: pure, the same for the
    /// same seed, stamped into each chunk it touches. Buildings come from content/structures.
    /// Set it on the generator (TerrainGenerator.Village) before any chunk is generated.
    /// </summary>
    public sealed class Village
    {
        /// <summary>The plaza's radius around the Confluence stone, in voxels (10 m).</summary>
        public const int PlazaRadius = 40;
        /// <summary>The road's half width, in voxels.</summary>
        public const int RoadHalf = 6;
        /// <summary>How deep a building's pad reaches down to meet sloping ground.</summary>
        const int PadDepth = 10;

        public readonly List<VillageBuilding> Buildings = new List<VillageBuilding>();
        /// <summary>Named spots (where people live), in metres; the height is filled in by the host.</summary>
        public readonly Dictionary<string, Vec3> Spots = new Dictionary<string, Vec3>();

        readonly HeightField _field;
        readonly ushort _paving, _sandstone, _air;
        readonly int _roadX0, _roadZ0, _roadX1, _roadZ1;
        int _minX = int.MaxValue, _maxX = int.MinValue, _minZ = int.MaxValue, _maxZ = int.MinValue;

        /// <summary>Loads the village's buildings from content and lays it out.</summary>
        public static Village Load(string contentRoot, HeightField field, BlockRegistry blocks, EchoRuin ruin)
        {
            var stamps = new Dictionary<string, StructureStamp>();
            foreach (string key in new[] { "vaari_dome_house", "reed_hut", "watch_tower" })
            {
                string path = Path.Combine(contentRoot, "structures", key + ".vox");
                if (File.Exists(path)) stamps[key] = StructureStamp.FromVox(VoxFile.Load(path), blocks);
            }
            return new Village(field, blocks, stamps, ruin);
        }

        public Village(HeightField field, BlockRegistry blocks, Dictionary<string, StructureStamp> stamps, EchoRuin ruin)
        {
            _field = field;
            _paving = blocks.Id("paving");
            _sandstone = blocks.Id("sandstone");
            _air = 0;
            // The road runs from the plaza's south edge to the ruin's arch.
            _roadX0 = 0; _roadZ0 = -PlazaRadius + 4;
            _roadX1 = ruin != null ? ruin.Ox : 0; _roadZ1 = ruin != null ? ruin.Oz + 3 : -PlazaRadius;
            Grow(-PlazaRadius, -PlazaRadius); Grow(PlazaRadius, PlazaRadius);
            Grow(Math.Min(_roadX0, _roadX1) - RoadHalf, Math.Min(_roadZ0, _roadZ1)); Grow(Math.Max(_roadX0, _roadX1) + RoadHalf, Math.Max(_roadZ0, _roadZ1));

            // North is +Z. The road to the ruin leaves the plaza to the south, so the south stays open.
            Place(stamps, "watch_tower", 0, 104);
            Place(stamps, "vaari_dome_house", -76, 52);
            Place(stamps, "vaari_dome_house", 76, 52);
            Place(stamps, "vaari_dome_house", -92, -24);
            Place(stamps, "vaari_dome_house", 92, -24);
            Place(stamps, "reed_hut", -56, -84);
            Place(stamps, "reed_hut", 56, -84);

            Spots["elder"] = M(0, 18);
            Spots["cook"] = M(-60, 22);
            Spots["watcher"] = M(10, 84);
        }

        static Vec3 M(int x, int z) { return new Vec3((x + 0.5) / WorldConfig.VoxelsPerMeter, 0, (z + 0.5) / WorldConfig.VoxelsPerMeter); }

        void Grow(int x, int z)
        {
            _minX = Math.Min(_minX, x); _maxX = Math.Max(_maxX, x);
            _minZ = Math.Min(_minZ, z); _maxZ = Math.Max(_maxZ, z);
        }

        void Place(Dictionary<string, StructureStamp> stamps, string key, int x, int z)
        {
            StructureStamp s;
            if (!stamps.TryGetValue(key, out s) || s.Voxels.Count == 0) return;
            int lx = int.MaxValue, hx = int.MinValue, lz = int.MaxValue, hz = int.MinValue, hy = 0;
            foreach (var v in s.Voxels)
            {
                lx = Math.Min(lx, v.X); hx = Math.Max(hx, v.X);
                lz = Math.Min(lz, v.Z); hz = Math.Max(hz, v.Z);
                hy = Math.Max(hy, v.Y);
            }
            var b = new VillageBuilding { Key = key, X = x, Z = z, Stamp = s, Height = hy + 1 };
            b.OffX = x - (lx + hx) / 2;
            b.OffZ = z - (lz + hz) / 2;
            b.MinX = lx + b.OffX - 2; b.MaxX = hx + b.OffX + 2;
            b.MinZ = lz + b.OffZ - 2; b.MaxZ = hz + b.OffZ + 2;
            // Stand it on the ground at its middle, never below the water.
            b.Floor = Math.Max(_field.HeightAt(x, z), HeightField.SeaLevel + 1);
            Buildings.Add(b);
            Grow(b.MinX, b.MinZ); Grow(b.MaxX, b.MaxZ);
        }

        /// <summary>Is this column paved: the plaza or the road south?</summary>
        public bool Paved(int x, int z)
        {
            if (x * x + z * z <= PlazaRadius * PlazaRadius) return true;
            // Distance from the road's centre line, in voxels.
            double dx = _roadX1 - _roadX0, dz = _roadZ1 - _roadZ0;
            double len2 = dx * dx + dz * dz;
            if (len2 < 1) return false;
            double t = ((x - _roadX0) * dx + (z - _roadZ0) * dz) / len2;
            if (t < 0 || t > 1) return false;
            double px = _roadX0 + t * dx - x, pz = _roadZ0 + t * dz - z;
            return px * px + pz * pz <= RoadHalf * RoadHalf;
        }

        /// <summary>Is this point (metres) inside a building's footprint?</summary>
        public bool InBuilding(Vec3 p)
        {
            int x = (int)Math.Floor(p.X * WorldConfig.VoxelsPerMeter), z = (int)Math.Floor(p.Z * WorldConfig.VoxelsPerMeter);
            foreach (var b in Buildings) if (x >= b.MinX && x <= b.MaxX && z >= b.MinZ && z <= b.MaxZ) return true;
            return false;
        }

        /// <summary>Writes the village into a freshly generated chunk: paving, then each building on its pad.</summary>
        public void Stamp(VoxelChunk chunk, BlockRegistry blocks)
        {
            const int n = WorldConfig.ChunkSize;
            int ox = chunk.Coord.OriginX, oy = chunk.Coord.OriginY, oz = chunk.Coord.OriginZ;
            if (ox > _maxX || ox + n <= _minX || oz > _maxZ || oz + n <= _minZ) return;

            // Paving follows the ground: the top layer of each paved column, above the water.
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                {
                    int wx = ox + x, wz = oz + z;
                    if (!Paved(wx, wz)) continue;
                    int h = _field.HeightAt(wx, wz);
                    if (h <= HeightField.SeaLevel) continue;
                    int y = h - oy;
                    if (y >= 0 && y < n) chunk.Set(x, y, z, _paving);
                    // Clear plants and low trees off the road.
                    for (int dy = 1; dy <= 24; dy++)
                    {
                        int yy = y + dy;
                        if (yy >= 0 && yy < n) chunk.Set(x, yy, z, _air);
                    }
                }

            foreach (var b in Buildings)
            {
                if (ox > b.MaxX || ox + n <= b.MinX || oz > b.MaxZ || oz + n <= b.MinZ) continue;
                if (oy > b.Floor + b.Height + 8 || oy + n <= b.Floor - PadDepth) continue;
                int x0 = Math.Max(b.MinX, ox), x1 = Math.Min(b.MaxX, ox + n - 1);
                int z0 = Math.Max(b.MinZ, oz), z1 = Math.Min(b.MaxZ, oz + n - 1);
                // The pad: solid sandstone under the floor, open air above it (no hill, no tree).
                for (int wz = z0; wz <= z1; wz++)
                    for (int wx = x0; wx <= x1; wx++)
                        for (int wy = Math.Max(b.Floor - PadDepth, oy); wy <= Math.Min(b.Floor + b.Height + 8, oy + n - 1); wy++)
                            chunk.Set(wx - ox, wy - oy, wz - oz, wy < b.Floor ? _sandstone : _air);
                foreach (var v in b.Stamp.Voxels)
                {
                    int wx = v.X + b.OffX, wy = v.Y + b.Floor, wz = v.Z + b.OffZ;
                    if (wx < ox || wx >= ox + n || wy < oy || wy >= oy + n || wz < oz || wz >= oz + n) continue;
                    chunk.Set(wx - ox, wy - oy, wz - oz, v.Block);
                }
                // Fill the floor layer where the building has none, so the pad has a top.
                for (int wz = z0; wz <= z1; wz++)
                    for (int wx = x0; wx <= x1; wx++)
                    {
                        int wy = b.Floor;
                        if (wy < oy || wy >= oy + n) continue;
                        if (chunk.Get(wx - ox, wy - oy, wz - oz) == 0) chunk.Set(wx - ox, wy - oy, wz - oz, _paving);
                    }
            }
        }
    }
}
