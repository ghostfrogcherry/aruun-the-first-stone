namespace Aruun.Core.Voxels
{
    /// <summary>A 32x32x32 block of voxel ids. Index order is x + z*32 + y*1024.</summary>
    public sealed class VoxelChunk
    {
        public readonly ChunkCoord Coord;
        public readonly ushort[] Ids = new ushort[WorldConfig.ChunkVolume];

        public VoxelChunk(ChunkCoord coord) { Coord = coord; }

        public static int Index(int lx, int ly, int lz)
        {
            return lx | (lz << WorldConfig.ChunkShift) | (ly << (2 * WorldConfig.ChunkShift));
        }

        public ushort Get(int lx, int ly, int lz) { return Ids[Index(lx, ly, lz)]; }
        public void Set(int lx, int ly, int lz, ushort id) { Ids[Index(lx, ly, lz)] = id; }
    }
}
