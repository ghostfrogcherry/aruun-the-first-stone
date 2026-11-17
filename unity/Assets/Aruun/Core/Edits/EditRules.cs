namespace Aruun.Core.Edits
{
    /// <summary>The rules the host uses to judge an edit request. Pure functions, easy to test.</summary>
    public static class EditRules
    {
        /// <summary>Can a tool of this dig power break a block of this hardness? 99 and above never breaks.</summary>
        public static bool Breaks(int hardness, int power)
        {
            return hardness <= power && hardness < 99;
        }

        /// <summary>Is the target cell within reach of a character standing at (px, py, pz), in voxels?</summary>
        public static bool InReach(int px, int py, int pz, int tx, int ty, int tz, int reach)
        {
            long dx = tx - px, dy = ty - py, dz = tz - pz;
            return dx * dx + dy * dy + dz * dz <= (long)reach * reach;
        }
    }
}
