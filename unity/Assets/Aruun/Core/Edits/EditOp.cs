using System;
using System.Globalization;
using Aruun.Core.Mining;

namespace Aruun.Core.Edits
{
    public enum EditKind { Set, Box, Sphere }

    /// <summary>
    /// One change to the world. Players never change voxels directly: they send a request,
    /// the host checks it and turns it into ops, and every machine applies the same ops in the
    /// same order. Ops are also what the save journal stores, one per line.
    ///
    /// Block 0 (air) removes. A removing op only breaks blocks whose hardness is at most Power,
    /// so a weak tool takes the soil and leaves the rock. Any other block builds: it fills empty
    /// space (air, water, plants) and never overwrites something solid.
    ///
    /// Tool says what kind of tool a removing op was dug with. Any (the default) ignores tool
    /// kinds; Hand, Axe or Pick only break blocks that kind of tool fits (see DigRules), so an
    /// axe bite takes the trunk and leaves the stone beside it. The journal line only carries a
    /// tool when it is not Any, so old saves read the same.
    /// </summary>
    public struct EditOp
    {
        public EditKind Kind;
        /// <summary>The cell (Set), the low corner (Box) or the centre cell (Sphere).</summary>
        public int X, Y, Z;
        /// <summary>The high corner of a Box, inclusive.</summary>
        public int X2, Y2, Z2;
        /// <summary>Sphere radius in tenths of a voxel.</summary>
        public int Radius10;
        public ushort Block;
        public int Power;
        public ToolClass Tool;

        public const int MaxExtent = 256;

        public static EditOp Set(int x, int y, int z, ushort block, int power = 98)
        {
            return new EditOp { Kind = EditKind.Set, X = x, Y = y, Z = z, Block = block, Power = power };
        }

        public static EditOp Box(int x, int y, int z, int x2, int y2, int z2, ushort block, int power = 98)
        {
            return new EditOp
            {
                Kind = EditKind.Box,
                X = Math.Min(x, x2), Y = Math.Min(y, y2), Z = Math.Min(z, z2),
                X2 = Math.Max(x, x2), Y2 = Math.Max(y, y2), Z2 = Math.Max(z, z2),
                Block = block, Power = power,
            };
        }

        public static EditOp Sphere(int x, int y, int z, int radius10, ushort block, int power = 98)
        {
            return new EditOp { Kind = EditKind.Sphere, X = x, Y = y, Z = z, Radius10 = radius10, Block = block, Power = power };
        }

        /// <summary>False for ops that are malformed or too large to be honest requests.</summary>
        public bool IsValid()
        {
            switch (Kind)
            {
                case EditKind.Set: return true;
                case EditKind.Box:
                    return X2 >= X && Y2 >= Y && Z2 >= Z &&
                           X2 - X < MaxExtent && Y2 - Y < MaxExtent && Z2 - Z < MaxExtent;
                case EditKind.Sphere: return Radius10 > 0 && Radius10 <= MaxExtent * 5;
                default: return false;
            }
        }

        /// <summary>Calls visit for every cell the op covers, in a fixed order.</summary>
        public void ForEachCell(Action<int, int, int> visit)
        {
            switch (Kind)
            {
                case EditKind.Set:
                    visit(X, Y, Z);
                    break;
                case EditKind.Box:
                    for (int y = Y; y <= Y2; y++)
                        for (int z = Z; z <= Z2; z++)
                            for (int x = X; x <= X2; x++)
                                visit(x, y, z);
                    break;
                case EditKind.Sphere:
                    int r = (Radius10 + 9) / 10;
                    long limit = (long)Radius10 * Radius10;
                    for (int dy = -r; dy <= r; dy++)
                        for (int dz = -r; dz <= r; dz++)
                            for (int dx = -r; dx <= r; dx++)
                                if ((long)(dx * dx + dy * dy + dz * dz) * 100 <= limit)
                                    visit(X + dx, Y + dy, Z + dz);
                    break;
            }
        }

        /// <summary>The same op, dug with a tool of this kind.</summary>
        public EditOp WithTool(ToolClass tool)
        {
            var op = this;
            op.Tool = tool;
            return op;
        }

        public string ToLine()
        {
            string line = BaseLine();
            return Tool == ToolClass.Any ? line : line + " " + ToolClasses.Name(Tool);
        }

        string BaseLine()
        {
            var c = CultureInfo.InvariantCulture;
            switch (Kind)
            {
                case EditKind.Set:
                    return string.Format(c, "set {0} {1} {2} {3} {4}", X, Y, Z, Block, Power);
                case EditKind.Box:
                    return string.Format(c, "box {0} {1} {2} {3} {4} {5} {6} {7}", X, Y, Z, X2, Y2, Z2, Block, Power);
                default:
                    return string.Format(c, "sphere {0} {1} {2} {3} {4} {5}", X, Y, Z, Radius10, Block, Power);
            }
        }

        public static bool TryParse(string line, out EditOp op)
        {
            op = new EditOp();
            if (string.IsNullOrWhiteSpace(line)) return false;
            string[] p = line.Trim().Split(' ');
            int need = p[0] == "set" ? 6 : p[0] == "box" ? 9 : p[0] == "sphere" ? 7 : -1;
            if (need < 0 || (p.Length != need && p.Length != need + 1)) return false;
            var tool = ToolClass.Any;
            if (p.Length == need + 1)
            {
                try { tool = ToolClasses.Parse(p[need], ToolClass.Any); }
                catch (FormatException) { return false; }
            }
            int[] n = new int[need - 1];
            for (int i = 1; i < need; i++)
                if (!int.TryParse(p[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out n[i - 1])) return false;
            int block = n[need - 3];
            if (block < 0 || block > ushort.MaxValue) return false;
            switch (p[0])
            {
                case "set": op = Set(n[0], n[1], n[2], (ushort)block, n[4]); break;
                case "box": op = Box(n[0], n[1], n[2], n[3], n[4], n[5], (ushort)block, n[7]); break;
                default: op = Sphere(n[0], n[1], n[2], n[3], (ushort)block, n[5]); break;
            }
            op.Tool = tool;
            return op.IsValid();
        }
    }
}
