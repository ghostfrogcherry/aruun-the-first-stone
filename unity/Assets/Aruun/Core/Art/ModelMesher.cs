using System.Collections.Generic;

namespace Aruun.Core.Art
{
    /// <summary>
    /// Mesh arrays for a voxel model. Positions are in model voxels; the view scales them.
    /// Colours are sRGB bytes. Glowing colours have their own index list for an emissive material.
    /// </summary>
    public sealed class ModelMeshData
    {
        public readonly List<float> Positions = new List<float>();
        public readonly List<float> Normals = new List<float>();
        public readonly List<byte> Colors = new List<byte>();
        public readonly List<int> Solid = new List<int>();
        public readonly List<int> Glow = new List<int>();
        public int VertexCount { get { return Positions.Count / 3; } }
        public bool IsEmpty { get { return Solid.Count == 0 && Glow.Count == 0; } }
        public float MinX = float.MaxValue, MinY = float.MaxValue, MinZ = float.MaxValue;
        public float MaxX = float.MinValue, MaxY = float.MinValue, MaxZ = float.MinValue;
    }

    /// <summary>
    /// Greedy mesher for .vox models: hidden faces are dropped and touching faces of the same
    /// colour merge into larger quads. Add each part with an offset (in voxels), then read Data.
    /// </summary>
    public sealed class ModelMesher
    {
        public readonly ModelMeshData Data = new ModelMeshData();

        public void Add(VoxModel m, uint[] palette, bool[] glow, float offX, float offY, float offZ)
        {
            int[] dims = { m.SizeX, m.SizeY, m.SizeZ };
            int[] pos = new int[3];
            for (int d = 0; d < 3; d++)
            {
                int u = (d + 1) % 3, v = (d + 2) % 3;
                int nu = dims[u], nv = dims[v];
                var mask = new byte[nu * nv];
                for (int dir = -1; dir <= 1; dir += 2)
                {
                    for (int slice = 0; slice < dims[d]; slice++)
                    {
                        bool any = false;
                        pos[d] = slice;
                        for (int j = 0; j < nv; j++)
                        {
                            pos[v] = j;
                            for (int i = 0; i < nu; i++)
                            {
                                pos[u] = i;
                                byte c = m.Get(pos[0], pos[1], pos[2]);
                                byte face = 0;
                                if (c != 0)
                                {
                                    pos[d] += dir;
                                    if (m.Get(pos[0], pos[1], pos[2]) == 0) { face = c; any = true; }
                                    pos[d] -= dir;
                                }
                                mask[i + j * nu] = face;
                            }
                        }
                        if (!any) continue;
                        for (int j = 0; j < nv; j++)
                            for (int i = 0; i < nu;)
                            {
                                byte c = mask[i + j * nu];
                                if (c == 0) { i++; continue; }
                                int w = 1;
                                while (i + w < nu && mask[i + w + j * nu] == c) w++;
                                int h = 1;
                                bool grow = true;
                                while (j + h < nv && grow)
                                {
                                    for (int k = 0; k < w; k++)
                                        if (mask[i + k + (j + h) * nu] != c) { grow = false; break; }
                                    if (grow) h++;
                                }
                                for (int jj = 0; jj < h; jj++)
                                    for (int ii = 0; ii < w; ii++) mask[i + ii + (j + jj) * nu] = 0;
                                var o = new float[3];
                                o[u] = i; o[v] = j; o[d] = dir > 0 ? slice + 1 : slice;
                                o[0] += m.OriginX + offX; o[1] += m.OriginY + offY; o[2] += m.OriginZ + offZ;
                                Quad(o, u, v, d, w, h, dir, palette[c], glow != null && glow[c]);
                                i += w;
                            }
                    }
                }
            }
        }

        void Quad(float[] o, int u, int v, int d, int w, int h, int dir, uint rgb, bool glows)
        {
            var g = Data;
            int first = g.VertexCount;
            for (int corner = 0; corner < 4; corner++)
            {
                float x = o[0], y = o[1], z = o[2];
                float du = (corner == 1 || corner == 2) ? w : 0, dv = (corner == 2 || corner == 3) ? h : 0;
                if (u == 0) x += du; else if (u == 1) y += du; else z += du;
                if (v == 0) x += dv; else if (v == 1) y += dv; else z += dv;
                g.Positions.Add(x); g.Positions.Add(y); g.Positions.Add(z);
                if (x < g.MinX) g.MinX = x; if (y < g.MinY) g.MinY = y; if (z < g.MinZ) g.MinZ = z;
                if (x > g.MaxX) g.MaxX = x; if (y > g.MaxY) g.MaxY = y; if (z > g.MaxZ) g.MaxZ = z;
                g.Normals.Add(d == 0 ? dir : 0); g.Normals.Add(d == 1 ? dir : 0); g.Normals.Add(d == 2 ? dir : 0);
                g.Colors.Add((byte)(rgb >> 16)); g.Colors.Add((byte)(rgb >> 8)); g.Colors.Add((byte)rgb); g.Colors.Add(255);
            }
            var idx = glows ? g.Glow : g.Solid;
            // Corners run origin, +u, +u+v, +v, which faces +d (same as the terrain mesher).
            if (dir > 0) { idx.Add(first); idx.Add(first + 1); idx.Add(first + 2); idx.Add(first); idx.Add(first + 2); idx.Add(first + 3); }
            else { idx.Add(first); idx.Add(first + 2); idx.Add(first + 1); idx.Add(first); idx.Add(first + 3); idx.Add(first + 2); }
        }
    }
}
