using System.Collections.Generic;
using Aruun.Core.Content;

namespace Aruun.Core.Art
{
    /// <summary>One block of a structure, relative to its floor centre.</summary>
    public struct StampVoxel
    {
        public int X, Y, Z;
        public ushort Block;
    }

    /// <summary>
    /// Turns a structure .vox (content/structures, 0.25 m voxels, painted only in block colours)
    /// into blocks. y = 0 is the floor layer. Colours that are not blocks are reported, not guessed.
    /// </summary>
    public sealed class StructureStamp
    {
        public readonly List<StampVoxel> Voxels = new List<StampVoxel>();
        public readonly List<string> UnknownColours = new List<string>();

        public static StructureStamp FromVox(VoxFile file, BlockRegistry blocks)
        {
            var byColour = new Dictionary<uint, ushort>();
            for (int i = 1; i < blocks.Count; i++)
            {
                var b = blocks.Get(i);
                uint rgb = (uint)(b.R << 16 | b.G << 8 | b.B);
                if (b.Solid && !byColour.ContainsKey(rgb)) byColour[rgb] = b.Id;
            }
            for (int i = 1; i < blocks.Count; i++)
            {
                var b = blocks.Get(i);
                if (b.Solid && b.Paint >= 0 && !byColour.ContainsKey((uint)b.Paint)) byColour[(uint)b.Paint] = b.Id;
            }
            var stamp = new StructureStamp();
            var lookup = new int[256];
            for (int i = 1; i < 256; i++)
            {
                ushort id;
                lookup[i] = byColour.TryGetValue(file.Palette[i], out id) ? id : -1;
            }
            var missing = new HashSet<uint>();
            int minY = int.MaxValue;
            foreach (var m in file.Models) if (m.Count > 0 && m.OriginY < minY) minY = m.OriginY;
            foreach (var m in file.Models)
                for (int z = 0; z < m.SizeZ; z++)
                    for (int y = 0; y < m.SizeY; y++)
                        for (int x = 0; x < m.SizeX; x++)
                        {
                            byte c = m.Cells[x + m.SizeX * (y + m.SizeY * z)];
                            if (c == 0) continue;
                            if (lookup[c] < 0) { missing.Add(file.Palette[c]); continue; }
                            stamp.Voxels.Add(new StampVoxel { X = m.OriginX + x, Y = m.OriginY - minY + y, Z = m.OriginZ + z, Block = (ushort)lookup[c] });
                        }
            foreach (uint rgb in missing) stamp.UnknownColours.Add("#" + rgb.ToString("x6"));
            return stamp;
        }
    }
}
