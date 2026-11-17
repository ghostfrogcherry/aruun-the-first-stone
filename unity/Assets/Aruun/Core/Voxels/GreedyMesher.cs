using Aruun.Core.Content;

namespace Aruun.Core.Voxels
{
    /// <summary>
    /// Turns a chunk into a mesh, merging neighbouring faces of the same block into large
    /// quads (greedy meshing). At 0.25 m a flat meadow would otherwise be thousands of tiny
    /// squares. Small plants ("cross" blocks) become two crossed quads.
    ///
    /// Input is a padded 34x34x34 array: the chunk plus one voxel of each neighbour, so faces
    /// on the chunk border are hidden correctly. One mesher per thread; it keeps scratch buffers.
    /// </summary>
    public sealed class GreedyMesher
    {
        public const int Padded = WorldConfig.ChunkSize + 2;
        public const int PaddedVolume = Padded * Padded * Padded;
        const int N = WorldConfig.ChunkSize;

        readonly BlockRegistry _blocks;
        readonly bool[] _cube, _opaque, _cross;
        readonly ushort[] _mask = new ushort[N * N];

        public GreedyMesher(BlockRegistry blocks)
        {
            _blocks = blocks;
            _cube = new bool[blocks.Count];
            _opaque = new bool[blocks.Count];
            _cross = new bool[blocks.Count];
            for (int i = 0; i < blocks.Count; i++)
            {
                BlockDef b = blocks.Get(i);
                _cube[i] = b.IsCube;
                _opaque[i] = b.IsOpaqueCube;
                _cross[i] = b.Id != 0 && b.Shape == "cross";
            }
        }

        public static int PaddedIndex(int px, int py, int pz) { return px + pz * Padded + py * Padded * Padded; }

        public void Build(ushort[] padded, MeshData mesh)
        {
            mesh.Clear();
            int[] pos = new int[3];
            int[] step = new int[3];
            for (int d = 0; d < 3; d++)
            {
                int u = (d + 1) % 3, v = (d + 2) % 3;
                for (int dir = -1; dir <= 1; dir += 2)
                {
                    step[0] = step[1] = step[2] = 0;
                    step[d] = dir;
                    for (int slice = 0; slice < N; slice++)
                    {
                        // 1. Which faces of this slice can be seen?
                        bool any = false;
                        pos[d] = slice;
                        for (int j = 0; j < N; j++)
                        {
                            pos[v] = j;
                            for (int i = 0; i < N; i++)
                            {
                                pos[u] = i;
                                ushort a = padded[PaddedIndex(pos[0] + 1, pos[1] + 1, pos[2] + 1)];
                                ushort face = 0;
                                if (a < _cube.Length && _cube[a])
                                {
                                    ushort b = padded[PaddedIndex(pos[0] + 1 + step[0], pos[1] + 1 + step[1], pos[2] + 1 + step[2])];
                                    bool hidden = (b < _opaque.Length && _opaque[b]) || (!_opaque[a] && b == a);
                                    if (!hidden) { face = a; any = true; }
                                }
                                _mask[i + j * N] = face;
                            }
                        }
                        if (!any) continue;

                        // 2. Merge equal neighbours into rectangles.
                        for (int j = 0; j < N; j++)
                        {
                            for (int i = 0; i < N;)
                            {
                                ushort id = _mask[i + j * N];
                                if (id == 0) { i++; continue; }
                                int w = 1;
                                while (i + w < N && _mask[i + w + j * N] == id) w++;
                                int h = 1;
                                bool grow = true;
                                while (j + h < N && grow)
                                {
                                    for (int k = 0; k < w; k++)
                                        if (_mask[i + k + (j + h) * N] != id) { grow = false; break; }
                                    if (grow) h++;
                                }
                                for (int jj = 0; jj < h; jj++)
                                    for (int ii = 0; ii < w; ii++)
                                        _mask[i + ii + (j + jj) * N] = 0;

                                pos[u] = i; pos[v] = j; pos[d] = dir > 0 ? slice + 1 : slice;
                                EmitQuad(mesh, pos, u, v, d, w, h, dir, id);
                                i += w;
                            }
                        }
                    }
                }
            }
            EmitCrosses(padded, mesh);
        }

        void EmitQuad(MeshData mesh, int[] origin, int u, int v, int d, int w, int h, int dir, ushort id)
        {
            BlockDef b = _blocks.Get(id);
            int first = mesh.VertexCount;
            for (int corner = 0; corner < 4; corner++)
            {
                float[] p = { origin[0], origin[1], origin[2] };
                if (corner == 1 || corner == 2) p[u] += w;
                if (corner == 2 || corner == 3) p[v] += h;
                mesh.Positions.Add(p[0]); mesh.Positions.Add(p[1]); mesh.Positions.Add(p[2]);
                mesh.Normals.Add(d == 0 ? dir : 0); mesh.Normals.Add(d == 1 ? dir : 0); mesh.Normals.Add(d == 2 ? dir : 0);
                mesh.Colors.Add(b.R); mesh.Colors.Add(b.G); mesh.Colors.Add(b.B); mesh.Colors.Add(b.A);
            }
            var indices = b.Transparent ? mesh.Transparent : mesh.Opaque;
            // Corners run origin, +u, +u+v, +v. That order faces +d; flip it for the -d side.
            if (dir > 0)
            {
                indices.Add(first); indices.Add(first + 1); indices.Add(first + 2);
                indices.Add(first); indices.Add(first + 2); indices.Add(first + 3);
            }
            else
            {
                indices.Add(first); indices.Add(first + 2); indices.Add(first + 1);
                indices.Add(first); indices.Add(first + 3); indices.Add(first + 2);
            }
        }

        /// <summary>Small plants: two upright quads crossed on the diagonals, seen from both sides.</summary>
        void EmitCrosses(ushort[] padded, MeshData mesh)
        {
            for (int y = 0; y < N; y++)
            for (int z = 0; z < N; z++)
            for (int x = 0; x < N; x++)
            {
                ushort id = padded[PaddedIndex(x + 1, y + 1, z + 1)];
                if (id >= _cross.Length || !_cross[id]) continue;
                BlockDef b = _blocks.Get(id);
                for (int diag = 0; diag < 2; diag++)
                {
                    int first = mesh.VertexCount;
                    float x0 = x, x1 = x + 1;
                    float z0 = diag == 0 ? z : z + 1, z1 = diag == 0 ? z + 1 : z;
                    AddCrossVertex(mesh, b, x0, y, z0);
                    AddCrossVertex(mesh, b, x1, y, z1);
                    AddCrossVertex(mesh, b, x1, y + 1, z1);
                    AddCrossVertex(mesh, b, x0, y + 1, z0);
                    int[] order = { 0, 1, 2, 0, 2, 3, 0, 2, 1, 0, 3, 2 };
                    foreach (int k in order) mesh.Opaque.Add(first + k);
                }
            }
        }

        static void AddCrossVertex(MeshData mesh, BlockDef b, float x, float y, float z)
        {
            mesh.Positions.Add(x); mesh.Positions.Add(y); mesh.Positions.Add(z);
            mesh.Normals.Add(0); mesh.Normals.Add(1); mesh.Normals.Add(0); // lit like the ground it grows on
            mesh.Colors.Add(b.R); mesh.Colors.Add(b.G); mesh.Colors.Add(b.B); mesh.Colors.Add(255);
        }
    }
}
