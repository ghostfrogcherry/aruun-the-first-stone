using System;
using System.Collections.Generic;
using Aruun.Core.Edits;

namespace Aruun.Core.Building
{
    /// <summary>The building shapes, in the order the shape key cycles through them.</summary>
    public enum Shape { Single, Line, Wall, Floor, Box, Room }

    /// <summary>
    /// Shape building: two corners and a shape become world edits. Building ops only fill empty
    /// space (air, water, plants), so a shape never eats what is already there, and you pay one
    /// block for each voxel actually filled. Pure logic, shared by the host and the preview.
    /// </summary>
    public static class Shapes
    {
        /// <summary>Longest side of any shape, in voxels (8 m).</summary>
        public const int MaxSide = 32;

        public static string Name(Shape s)
        {
            switch (s)
            {
                case Shape.Single: return "Single block";
                case Shape.Line: return "Line";
                case Shape.Wall: return "Wall";
                case Shape.Floor: return "Floor";
                case Shape.Box: return "Solid box";
                case Shape.Room: return "Room (hollow box)";
            }
            return s.ToString();
        }

        public static Shape Next(Shape s) { return (Shape)(((int)s + 1) % Enum.GetValues(typeof(Shape)).Length); }

        /// <summary>
        /// Where the second corner really lands. A wall keeps to one vertical plane: the
        /// horizontal axis the corners differ most along. A floor keeps the first corner's height.
        /// </summary>
        public static void Snap(Shape s, int ax, int ay, int az, ref int bx, ref int by, ref int bz)
        {
            if (s == Shape.Wall)
            {
                if (Math.Abs(bx - ax) >= Math.Abs(bz - az)) bz = az; else bx = ax;
            }
            else if (s == Shape.Floor) by = ay;
            else if (s == Shape.Single) { bx = ax; by = ay; bz = az; }
            // Clamp every side to MaxSide, keeping the direction from the first corner.
            bx = ax + Math.Max(-(MaxSide - 1), Math.Min(MaxSide - 1, bx - ax));
            by = ay + Math.Max(-(MaxSide - 1), Math.Min(MaxSide - 1, by - ay));
            bz = az + Math.Max(-(MaxSide - 1), Math.Min(MaxSide - 1, bz - az));
        }

        /// <summary>The edits that build the shape from corner a to corner b (after Snap) in `block`.</summary>
        public static List<EditOp> Ops(Shape s, int ax, int ay, int az, int bx, int by, int bz, ushort block)
        {
            Snap(s, ax, ay, az, ref bx, ref by, ref bz);
            int x0 = Math.Min(ax, bx), x1 = Math.Max(ax, bx);
            int y0 = Math.Min(ay, by), y1 = Math.Max(ay, by);
            int z0 = Math.Min(az, bz), z1 = Math.Max(az, bz);
            var ops = new List<EditOp>();
            switch (s)
            {
                case Shape.Line:
                    foreach (var c in LineCells(ax, ay, az, bx, by, bz)) ops.Add(EditOp.Set(c[0], c[1], c[2], block));
                    break;
                case Shape.Room:
                    if (x1 - x0 < 2 || y1 - y0 < 2 || z1 - z0 < 2) { ops.Add(EditOp.Box(x0, y0, z0, x1, y1, z1, block)); break; }
                    ops.Add(EditOp.Box(x0, y0, z0, x1, y0, z1, block));                 // floor
                    ops.Add(EditOp.Box(x0, y1, z0, x1, y1, z1, block));                 // roof
                    ops.Add(EditOp.Box(x0, y0 + 1, z0, x1, y1 - 1, z0, block));         // four walls
                    ops.Add(EditOp.Box(x0, y0 + 1, z1, x1, y1 - 1, z1, block));
                    ops.Add(EditOp.Box(x0, y0 + 1, z0 + 1, x0, y1 - 1, z1 - 1, block));
                    ops.Add(EditOp.Box(x1, y0 + 1, z0 + 1, x1, y1 - 1, z1 - 1, block));
                    break;
                default:
                    ops.Add(EditOp.Box(x0, y0, z0, x1, y1, z1, block));
                    break;
            }
            return ops;
        }

        /// <summary>Every cell the ops cover, once each (shells and lines can touch themselves).</summary>
        public static HashSet<long> Cells(IEnumerable<EditOp> ops)
        {
            var cells = new HashSet<long>();
            foreach (var op in ops)
            {
                if (op.Kind == EditKind.Set) { cells.Add(Key(op.X, op.Y, op.Z)); continue; }
                for (int x = op.X; x <= op.X2; x++)
                    for (int y = op.Y; y <= op.Y2; y++)
                        for (int z = op.Z; z <= op.Z2; z++) cells.Add(Key(x, y, z));
            }
            return cells;
        }

        public static long Key(int x, int y, int z) { return ((long)(x & 0x1FFFFF) << 42) | ((long)(y & 0x1FFFFF) << 21) | (long)(z & 0x1FFFFF); }

        public static void Unkey(long k, out int x, out int y, out int z)
        {
            x = Sign21((int)((k >> 42) & 0x1FFFFF));
            y = Sign21((int)((k >> 21) & 0x1FFFFF));
            z = Sign21((int)(k & 0x1FFFFF));
        }

        static int Sign21(int v) { return (v & 0x100000) != 0 ? v - 0x200000 : v; }

        /// <summary>A 3D line of cells, each touching the last by a face or an edge.</summary>
        public static List<int[]> LineCells(int ax, int ay, int az, int bx, int by, int bz)
        {
            var cells = new List<int[]>();
            int n = Math.Max(Math.Abs(bx - ax), Math.Max(Math.Abs(by - ay), Math.Abs(bz - az)));
            for (int i = 0; i <= n; i++)
            {
                double t = n == 0 ? 0 : (double)i / n;
                cells.Add(new[] { (int)Math.Round(ax + (bx - ax) * t), (int)Math.Round(ay + (by - ay) * t), (int)Math.Round(az + (bz - az) * t) });
            }
            return cells;
        }
    }
}
