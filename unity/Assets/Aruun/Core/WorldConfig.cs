namespace Aruun.Core
{
    /// <summary>
    /// World-wide constants. Voxel coordinates are integers; one voxel is 0.25 m.
    /// In Unity one unit is one metre, so the view layer scales voxel positions by VoxelSize.
    /// </summary>
    public static class WorldConfig
    {
        public const float VoxelSize = 0.25f;
        public const int VoxelsPerMeter = 4;

        /// <summary>Chunk edge length in voxels (8 m).</summary>
        public const int ChunkSize = 32;
        public const int ChunkShift = 5;
        public const int ChunkMask = ChunkSize - 1;
        public const int ChunkVolume = ChunkSize * ChunkSize * ChunkSize;

        /// <summary>Aruun's surface gravity is 0.38 g (canon). Metres per second squared.</summary>
        public const float Gravity = 9.81f * 0.38f;
        /// <summary>Earth gravity, used inside Occlusion sites.</summary>
        public const float EarthGravity = 9.81f;

        /// <summary>Bump when worldgen output changes, so old saves can be detected.</summary>
        public const int GenerationVersion = 1;

        /// <summary>How far a character can reach to dig or place, in voxels (6 m).</summary>
        public const int ReachVoxels = 24;
        /// <summary>Extra reach the host allows for latency when validating a request.</summary>
        public const int ServerReachSlack = 8;
    }
}
