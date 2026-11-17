using System.Collections.Generic;

namespace Aruun.Core.Voxels
{
    /// <summary>
    /// Plain mesh arrays the view layer copies into an engine mesh. Positions are in voxels,
    /// relative to the chunk origin. Solid and see-through faces have separate index lists
    /// so they can use different materials.
    /// </summary>
    public sealed class MeshData
    {
        public readonly List<float> Positions = new List<float>();   // x, y, z per vertex
        public readonly List<float> Normals = new List<float>();     // x, y, z per vertex
        public readonly List<byte> Colors = new List<byte>();        // r, g, b, a per vertex
        public readonly List<int> Opaque = new List<int>();
        public readonly List<int> Transparent = new List<int>();

        public int VertexCount { get { return Positions.Count / 3; } }
        public bool IsEmpty { get { return Opaque.Count == 0 && Transparent.Count == 0; } }

        public void Clear()
        {
            Positions.Clear(); Normals.Clear(); Colors.Clear(); Opaque.Clear(); Transparent.Clear();
        }
    }
}
