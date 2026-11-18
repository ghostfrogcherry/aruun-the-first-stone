using System;
using System.Collections.Generic;
using Aruun.Core.Content;
using Aruun.Core.Voxels;

namespace Aruun.Core.Town
{
    /// <summary>
    /// A hand-made build written as code: boxes of blocks in world voxels, applied in the order
    /// they were added (later ones win), plus helpers for the shapes the town is made of: walls,
    /// gabled and pyramid roofs, discs and rings. Stamping is pure and thread safe, so it runs
    /// inside world generation. Boxes are indexed by 64-voxel columns so a chunk only looks at
    /// the few that touch it.
    /// </summary>
    public sealed class Blueprint
    {
        struct Part
        {
            public int X0, Y0, Z0, X1, Y1, Z1;
            public ushort Block;
            /// <summary>Swap some of it for this block (weathering), 0 for none.</summary>
            public ushort Wear;
            /// <summary>Only fill cells that are empty (air, water, plants).</summary>
            public bool FillOnly;
        }

        const int Cell = 64;
        readonly List<Part> _boxes = new List<Part>();
        readonly Dictionary<long, List<int>> _index = new Dictionary<long, List<int>>();
        readonly BlockRegistry _blocks;
        public int MinX = int.MaxValue, MaxX = int.MinValue, MinY = int.MaxValue, MaxY = int.MinValue, MinZ = int.MaxValue, MaxZ = int.MinValue;

        public Blueprint(BlockRegistry blocks) { _blocks = blocks; }

        public ushort Id(string key) { return _blocks.Has(key) ? _blocks.Id(key) : _blocks.Id("ancient_stone"); }

        static long Key(int cx, int cz) { return ((long)cx << 32) ^ (uint)cz; }

        /// <summary>Any box, corners in any order, inclusive.</summary>
        public void Box(int x0, int y0, int z0, int x1, int y1, int z1, ushort block, ushort wear = 0, bool fillOnly = false)
        {
            var b = new Part
            {
                X0 = Math.Min(x0, x1), X1 = Math.Max(x0, x1),
                Y0 = Math.Min(y0, y1), Y1 = Math.Max(y0, y1),
                Z0 = Math.Min(z0, z1), Z1 = Math.Max(z0, z1),
                Block = block, Wear = wear, FillOnly = fillOnly,
            };
            int i = _boxes.Count;
            _boxes.Add(b);
            for (int cz = Floor(b.Z0, Cell); cz <= Floor(b.Z1, Cell); cz++)
                for (int cx = Floor(b.X0, Cell); cx <= Floor(b.X1, Cell); cx++)
                {
                    List<int> list;
                    long k = Key(cx, cz);
                    if (!_index.TryGetValue(k, out list)) _index[k] = list = new List<int>();
                    list.Add(i);
                }
            MinX = Math.Min(MinX, b.X0); MaxX = Math.Max(MaxX, b.X1);
            MinY = Math.Min(MinY, b.Y0); MaxY = Math.Max(MaxY, b.Y1);
            MinZ = Math.Min(MinZ, b.Z0); MaxZ = Math.Max(MaxZ, b.Z1);
        }

        public void Set(int x, int y, int z, ushort block) { Box(x, y, z, x, y, z, block); }

        /// <summary>Four walls of a box, `t` thick, without floor or ceiling.</summary>
        public void Walls(int x0, int y0, int z0, int x1, int y1, int z1, int t, ushort block, ushort wear = 0)
        {
            Box(x0, y0, z0, x1, y1, z0 + t - 1, block, wear);
            Box(x0, y0, z1 - t + 1, x1, y1, z1, block, wear);
            Box(x0, y0, z0, x0 + t - 1, y1, z1, block, wear);
            Box(x1 - t + 1, y0, z0, x1, y1, z1, block, wear);
        }

        /// <summary>
        /// A gabled roof over a footprint, starting at height y: each layer one voxel higher and
        /// one voxel in from the long sides, until the ridge. The two gable ends are filled with
        /// `gable`; the ridge gets `ridge`.
        /// </summary>
        public int Gable(int x0, int z0, int x1, int z1, int y, ushort roof, ushort ridge, ushort gable, int overhang = 2)
        {
            bool alongX = (x1 - x0) >= (z1 - z0);
            int lx0 = x0 - overhang, lx1 = x1 + overhang, lz0 = z0 - overhang, lz1 = z1 + overhang;
            int half = alongX ? (lz1 - lz0) / 2 : (lx1 - lx0) / 2;
            int top = y;
            for (int k = 0; k <= half; k++)
            {
                int yy = y + k;
                top = yy;
                if (alongX)
                {
                    int a = lz0 + k, b = lz1 - k;
                    if (a > b) break;
                    bool last = b - a <= 1;
                    Box(lx0, yy, a, lx1, yy, a, last ? ridge : roof);
                    Box(lx0, yy, b, lx1, yy, b, last ? ridge : roof);
                    if (a + 1 <= b - 1)
                    {
                        // The gable ends, inside the walls' line.
                        Box(x0, yy, a + 1, x0, yy, b - 1, gable);
                        Box(x1, yy, a + 1, x1, yy, b - 1, gable);
                    }
                }
                else
                {
                    int a = lx0 + k, b = lx1 - k;
                    if (a > b) break;
                    bool last = b - a <= 1;
                    Box(a, yy, lz0, a, yy, lz1, last ? ridge : roof);
                    Box(b, yy, lz0, b, yy, lz1, last ? ridge : roof);
                    if (a + 1 <= b - 1)
                    {
                        Box(a + 1, yy, z0, b - 1, yy, z0, gable);
                        Box(a + 1, yy, z1, b - 1, yy, z1, gable);
                    }
                }
            }
            return top;
        }

        /// <summary>A pyramid roof: rings stepping in one voxel per layer, with a tip block on top.</summary>
        public int Pyramid(int x0, int z0, int x1, int z1, int y, ushort roof, ushort tip, int overhang = 2, int rise = 1)
        {
            int k = 0;
            int ax0 = x0 - overhang, ax1 = x1 + overhang, az0 = z0 - overhang, az1 = z1 + overhang;
            int yy = y;
            while (ax0 <= ax1 && az0 <= az1)
            {
                for (int r = 0; r < rise; r++, yy++)
                {
                    if (ax1 - ax0 <= 1 || az1 - az0 <= 1) Box(ax0, yy, az0, ax1, yy, az1, tip);
                    else Walls(ax0, yy, az0, ax1, yy, az1, 1, roof);
                }
                ax0++; ax1--; az0++; az1--; k++;
            }
            int cx = (x0 + x1) / 2, cz = (z0 + z1) / 2;
            Box(cx, yy, cz, cx, yy + 2, cz, tip);
            return yy + 2;
        }

        /// <summary>A flat disc (or ring, when inner > 0) of radius r around (cx, cz), one layer.</summary>
        public void Disc(int cx, int y, int cz, int r, int inner, ushort block, int height = 1)
        {
            for (int dz = -r; dz <= r; dz++)
            {
                // Row by row: one box per run of cells inside the ring.
                int start = int.MinValue;
                for (int dx = -r; dx <= r + 1; dx++)
                {
                    int d2 = dx * dx + dz * dz;
                    bool inside = dx <= r && d2 <= r * r + r && d2 >= (inner > 0 ? inner * inner + inner : -1);
                    if (inside && start == int.MinValue) start = dx;
                    if (!inside && start != int.MinValue)
                    {
                        Box(cx + start, y, cz + dz, cx + dx - 1, y + height - 1, cz + dz, block);
                        start = int.MinValue;
                    }
                }
            }
        }

        /// <summary>Crenellations along the top of a wall run: merlons two wide with gaps between.</summary>
        public void Merlons(int x0, int z0, int x1, int z1, int y, ushort block, int h = 3)
        {
            bool alongX = Math.Abs(x1 - x0) >= Math.Abs(z1 - z0);
            int a0 = alongX ? Math.Min(x0, x1) : Math.Min(z0, z1), a1 = alongX ? Math.Max(x0, x1) : Math.Max(z0, z1);
            for (int a = a0; a <= a1; a += 4)
            {
                int b = Math.Min(a + 1, a1);
                if (alongX) Box(a, y, z0, b, y + h - 1, z1, block);
                else Box(x0, y, a, x1, y + h - 1, b, block);
            }
        }

        /// <summary>Writes the blueprint into a freshly generated chunk.</summary>
        public void Stamp(VoxelChunk chunk)
        {
            const int n = WorldConfig.ChunkSize;
            int ox = chunk.Coord.OriginX, oy = chunk.Coord.OriginY, oz = chunk.Coord.OriginZ;
            if (ox > MaxX || ox + n <= MinX || oy > MaxY || oy + n <= MinY || oz > MaxZ || oz + n <= MinZ) return;
            // Every box that touches this chunk, in the order they were added.
            var touching = new SortedSet<int>();
            for (int cz = Floor(oz, Cell); cz <= Floor(oz + n - 1, Cell); cz++)
                for (int cx = Floor(ox, Cell); cx <= Floor(ox + n - 1, Cell); cx++)
                {
                    List<int> list;
                    if (_index.TryGetValue(Key(cx, cz), out list)) foreach (int i in list) touching.Add(i);
                }
            foreach (int i in touching)
            {
                Part b = _boxes[i];
                int x0 = Math.Max(b.X0, ox), x1 = Math.Min(b.X1, ox + n - 1);
                int y0 = Math.Max(b.Y0, oy), y1 = Math.Min(b.Y1, oy + n - 1);
                int z0 = Math.Max(b.Z0, oz), z1 = Math.Min(b.Z1, oz + n - 1);
                if (x0 > x1 || y0 > y1 || z0 > z1) continue;
                for (int y = y0; y <= y1; y++)
                    for (int z = z0; z <= z1; z++)
                        for (int x = x0; x <= x1; x++)
                        {
                            if (b.FillOnly && _blocks.Get(chunk.Get(x - ox, y - oy, z - oz)).Collision) continue;
                            ushort id = b.Block;
                            if (b.Wear != 0 && Hash(x, y, z) % 5 == 0) id = b.Wear;
                            chunk.Set(x - ox, y - oy, z - oz, id);
                        }
            }
        }

        /// <summary>A small fixed hash of a cell, for weathering.</summary>
        public static int Hash(int x, int y, int z)
        {
            unchecked
            {
                int h = x * 73856093 ^ y * 19349663 ^ z * 83492791;
                h ^= h >> 13;
                h *= 0x5bd1e995;
                return (h ^ (h >> 15)) & 0x7fffffff;
            }
        }

        static int Floor(int v, int d) { return v >= 0 ? v / d : -((-v + d - 1) / d); }
    }
}
