using System.Collections.Generic;
using Aruun.Core.Content;
using Aruun.Core.Voxels;

namespace Aruun.Core.Destruction
{
    /// <summary>A world cell and the block that was in it.</summary>
    public struct PieceCell
    {
        public int X, Y, Z;
        public ushort Block;
        public PieceCell(int x, int y, int z, ushort block) { X = x; Y = y; Z = z; Block = block; }
    }

    /// <summary>
    /// Finds solid voxels that lost their connection to the rest of the world after a carve.
    ///
    /// A voxel counts as anchored if it connects (through its six faces) to any solid voxel on
    /// the boundary of the searched box: we cannot see past the box, so we assume the outside
    /// holds it up. Everything else is a loose piece, which the host turns into a falling body.
    /// </summary>
    public static class LoosePieces
    {
        /// <summary>
        /// The core search on a flat grid (index = x + z*sx + y*sx*sz, true = solid).
        /// Returns one list of grid indices per loose piece. Pieces larger than maxPiece are
        /// left where they are: something that big is treated as still held up.
        /// </summary>
        public static List<List<int>> Find(bool[] solid, int sx, int sy, int sz, int maxPiece)
        {
            int layer = sx * sz, count = solid.Length;
            var mark = new byte[count]; // 0 unvisited, 1 anchored, 2 loose
            var queue = new List<int>();
            for (int i = 0; i < count; i++)
            {
                if (!solid[i]) continue;
                int x = i % sx, z = (i / sx) % sz, y = i / layer;
                if (x == 0 || z == 0 || y == 0 || x == sx - 1 || z == sz - 1 || y == sy - 1)
                {
                    mark[i] = 1;
                    queue.Add(i);
                }
            }
            Flood(solid, sx, sy, sz, mark, queue, 1);

            var pieces = new List<List<int>>();
            for (int i = 0; i < count; i++)
            {
                if (!solid[i] || mark[i] != 0) continue;
                // Flood the whole piece even when it is too big, so its other voxels are
                // marked and never picked up as separate small pieces.
                var piece = new List<int> { i };
                mark[i] = 2;
                Flood(solid, sx, sy, sz, mark, piece, 2);
                if (piece.Count <= maxPiece) pieces.Add(piece);
            }
            return pieces;
        }

        /// <summary>
        /// Searches a box of the world (inclusive corners) for loose pieces. Water and small
        /// plants do not hold anything up; leaves and everything else do, so a felled tree
        /// comes down whole.
        /// </summary>
        public static List<List<PieceCell>> FindInWorld(VoxelWorld world, int x0, int y0, int z0, int x1, int y1, int z1, int maxPiece)
        {
            int sx = x1 - x0 + 1, sy = y1 - y0 + 1, sz = z1 - z0 + 1;
            var solid = new bool[sx * sy * sz];
            var ids = new ushort[solid.Length];
            for (int y = 0; y < sy; y++)
                for (int z = 0; z < sz; z++)
                    for (int x = 0; x < sx; x++)
                    {
                        int i = x + z * sx + y * sx * sz;
                        ushort id = world.Get(x0 + x, y0 + y, z0 + z);
                        ids[i] = id;
                        solid[i] = IsStructural(world.Blocks.Get(id));
                    }
            var result = new List<List<PieceCell>>();
            foreach (List<int> piece in Find(solid, sx, sy, sz, maxPiece))
            {
                var cells = new List<PieceCell>(piece.Count);
                foreach (int i in piece)
                    cells.Add(new PieceCell(x0 + i % sx, y0 + i / (sx * sz), z0 + (i / sx) % sz, ids[i]));
                result.Add(cells);
            }
            return result;
        }

        /// <summary>
        /// The pieces a carve set free: loose pieces that touch the carved box (inclusive
        /// corners), searched out to `margin` voxels around it. Pieces that were already
        /// floating somewhere nearby, like stray leaves at the edge of a canopy, are left alone.
        /// Call it after the carve has been applied.
        /// </summary>
        public static List<List<PieceCell>> FreedBy(VoxelWorld world, int x0, int y0, int z0, int x1, int y1, int z1, int margin, int maxPiece)
        {
            var freed = new List<List<PieceCell>>();
            foreach (List<PieceCell> piece in FindInWorld(world, x0 - margin, y0 - margin, z0 - margin, x1 + margin, y1 + margin, z1 + margin, maxPiece))
            {
                foreach (PieceCell c in piece)
                {
                    if (c.X >= x0 - 1 && c.X <= x1 + 1 && c.Y >= y0 - 1 && c.Y <= y1 + 1 && c.Z >= z0 - 1 && c.Z <= z1 + 1)
                    {
                        freed.Add(piece);
                        break;
                    }
                }
            }
            return freed;
        }

        public static bool IsStructural(BlockDef b)
        {
            return b.Id != 0 && !b.Fluid && b.Shape == "cube";
        }

        /// <summary>Breadth-first flood. `queue` doubles as the output list.</summary>
        static void Flood(bool[] solid, int sx, int sy, int sz, byte[] mark, List<int> queue, byte value)
        {
            int layer = sx * sz;
            for (int head = 0; head < queue.Count; head++)
            {
                int i = queue[head];
                int x = i % sx, z = (i / sx) % sz, y = i / layer;
                if (x > 0) Visit(i - 1, solid, mark, queue, value);
                if (x < sx - 1) Visit(i + 1, solid, mark, queue, value);
                if (z > 0) Visit(i - sx, solid, mark, queue, value);
                if (z < sz - 1) Visit(i + sx, solid, mark, queue, value);
                if (y > 0) Visit(i - layer, solid, mark, queue, value);
                if (y < sy - 1) Visit(i + layer, solid, mark, queue, value);
            }
        }

        static void Visit(int n, bool[] solid, byte[] mark, List<int> queue, byte value)
        {
            if (solid[n] && mark[n] == 0) { mark[n] = value; queue.Add(n); }
        }
    }
}
