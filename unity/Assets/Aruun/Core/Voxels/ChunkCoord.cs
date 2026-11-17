using System;

namespace Aruun.Core.Voxels
{
    /// <summary>Position of a chunk in chunk units. Voxel = chunk * 32 + local.</summary>
    public readonly struct ChunkCoord : IEquatable<ChunkCoord>
    {
        public readonly int X, Y, Z;

        public ChunkCoord(int x, int y, int z) { X = x; Y = y; Z = z; }

        /// <summary>The chunk that holds a voxel. Arithmetic shift floors negatives correctly.</summary>
        public static ChunkCoord FromVoxel(int x, int y, int z)
        {
            return new ChunkCoord(x >> WorldConfig.ChunkShift, y >> WorldConfig.ChunkShift, z >> WorldConfig.ChunkShift);
        }

        public int OriginX { get { return X << WorldConfig.ChunkShift; } }
        public int OriginY { get { return Y << WorldConfig.ChunkShift; } }
        public int OriginZ { get { return Z << WorldConfig.ChunkShift; } }

        public ChunkCoord Offset(int dx, int dy, int dz) { return new ChunkCoord(X + dx, Y + dy, Z + dz); }

        public bool Equals(ChunkCoord o) { return X == o.X && Y == o.Y && Z == o.Z; }
        public override bool Equals(object obj) { return obj is ChunkCoord && Equals((ChunkCoord)obj); }
        public override int GetHashCode() { unchecked { return (X * 73856093) ^ (Y * 19349663) ^ (Z * 83492791); } }
        public override string ToString() { return "(" + X + ", " + Y + ", " + Z + ")"; }
    }
}
