using System;
using System.Collections.Generic;

namespace Aruun.Core.Destruction
{
    /// <summary>
    /// The simple, exact fall of a loose piece: straight down until it lands. The host uses it
    /// to know where a piece will come to rest and how hard it hits, so collapses are the same
    /// for every player and are saved with the world. Tumbling physics bodies are drawn on top
    /// of this by the view layer.
    /// </summary>
    public static class Collapse
    {
        public const int MaxDrop = 96;

        /// <summary>
        /// How many voxels the piece can fall before any cell would enter something solid.
        /// isFree must return true for air, water and plants. Returns -1 if it does not land
        /// within maxDrop.
        /// </summary>
        public static int SettleDrop(IList<PieceCell> cells, Func<int, int, int, bool> isFree, int maxDrop = MaxDrop)
        {
            var own = new HashSet<long>();
            foreach (PieceCell c in cells) own.Add(Key(c.X, c.Y, c.Z));
            // Only the underside can hit anything: every other cell follows a cell below it.
            var bottoms = new List<PieceCell>();
            foreach (PieceCell c in cells)
                if (!own.Contains(Key(c.X, c.Y - 1, c.Z))) bottoms.Add(c);
            for (int d = 1; d <= maxDrop; d++)
                foreach (PieceCell c in bottoms)
                    if (!isFree(c.X, c.Y - d, c.Z)) return d - 1;
            return -1;
        }

        /// <summary>Seconds to fall `drop` voxels under `gravity` in metres per second squared.</summary>
        public static double FallTime(int drop, double gravity)
        {
            return Math.Sqrt(2.0 * Math.Max(drop, 0) * WorldConfig.VoxelSize / gravity);
        }

        /// <summary>
        /// Damage to anything under a landing piece. Pebbles sting, a pillar kills. It scales
        /// with landing speed, so the same fall hurts less in Aruun's low gravity than under
        /// Earth gravity inside an Occlusion site.
        /// </summary>
        public static double ImpactDamage(int cellCount, int drop, double gravity = WorldConfig.EarthGravity)
        {
            if (drop < 3) return 0;
            return Math.Min(Math.Sqrt(cellCount) * Math.Sqrt(drop * gravity / WorldConfig.EarthGravity) * 1.6, 400.0);
        }

        static long Key(int x, int y, int z)
        {
            return ((long)(x & 0x1FFFFF) << 42) | ((long)(y & 0x1FFFFF) << 21) | (long)(z & 0x1FFFFF);
        }
    }
}
