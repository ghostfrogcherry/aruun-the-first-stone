using System.Threading.Tasks;
using Aruun.Core;
using Aruun.Core.Content;
using Aruun.Core.Voxels;
using Aruun.Core.Worldgen;
using UnityEngine;
using UnityEngine.Rendering;

namespace Aruun.Game
{
    /// <summary>
    /// The land past the voxel chunks, out to the horizon: one coarse height-field mesh around
    /// the viewer, sampled from the same worldgen, coloured by the surface block and darkened
    /// where forests stand. It has a hole where the real chunks are drawn, and is rebuilt on a
    /// worker thread whenever the viewer has moved a good way.
    /// </summary>
    public sealed class FarTerrain : MonoBehaviour
    {
        public float Spacing = 4f;        // metres between samples
        public float Radius = 1024f;      // metres from the viewer to the edge
        public float RebuildAfter = 96f;  // metres moved before the mesh is rebuilt
        public float TreesTo = 0f;        // metres: trees as simple blocks out to here (0 = off; they read as boxes)
        float _hole;

        Transform _viewer;
        HeightField _field;
        BlockRegistry _blocks;
        Mesh _mesh;
        Material _material;
        Vector2 _centre = new Vector2(float.MaxValue, 0);
        Task<Data> _job;

        sealed class Data
        {
            public Vector2 Centre;
            public Vector3[] Positions;
            public Vector3[] Normals;
            public Color32[] Colors;
            public int[] Indices;
        }

        public void Init(VoxelWorld world, Transform viewer, float holeRadius)
        {
            _viewer = viewer;
            _field = world.Generator.Field;
            _blocks = world.Blocks;
            _material = new Material(Resources.Load<Shader>("AruunVoxel")) { name = "Aruun far terrain" };
            _material.SetFloat("_HoleRadius", holeRadius);
            _hole = holeRadius;
            _mesh = new Mesh { name = "Far terrain", indexFormat = IndexFormat.UInt32 };
            gameObject.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var r = gameObject.AddComponent<MeshRenderer>();
            r.sharedMaterial = _material;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        public void SetHole(float holeRadius)
        {
            _hole = holeRadius;
            if (_material != null) _material.SetFloat("_HoleRadius", holeRadius);
            _centre = new Vector2(float.MaxValue, 0);   // rebuild, so trees fill the new ring
        }

        void Update()
        {
            if (_viewer == null) return;
            if (_job != null)
            {
                if (!_job.IsCompleted) return;
                if (_job.Status == TaskStatus.RanToCompletion) Apply(_job.Result);
                else if (_job.Exception != null) Debug.LogException(_job.Exception);
                _job = null;
            }
            Vector2 at = new Vector2(_viewer.position.x, _viewer.position.z);
            if ((at - _centre).magnitude < RebuildAfter) return;
            // Snap to the sample grid so the land does not swim as you move.
            at = new Vector2(Mathf.Round(at.x / Spacing) * Spacing, Mathf.Round(at.y / Spacing) * Spacing);
            _centre = at;
            float spacing = Spacing, radius = Radius;
            HeightField field = _field;
            BlockRegistry blocks = _blocks;
            float treesFrom = _hole + 40f, treesTo = TreesTo;   // a band of forest-coloured ground first, so the change is gradual
            _job = Task.Run(() => Build(field, blocks, at, spacing, radius, treesFrom, treesTo));
        }

        static Data Build(HeightField field, BlockRegistry blocks, Vector2 centre, float spacing, float radius, float treesFrom, float treesTo)
        {
            int n = Mathf.CeilToInt(radius * 2 / spacing) + 1;
            float v = WorldConfig.VoxelSize;
            var heights = new float[n * n];
            var colors = new Color32[n * n];
            BlockDef water = blocks.Get("water");
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    float mx = centre.x - radius + i * spacing, mz = centre.y - radius + j * spacing;
                    int x = Mathf.FloorToInt(mx / v), z = Mathf.FloorToInt(mz / v);
                    int h = field.HeightAt(x, z);
                    BlockDef b;
                    if (h < HeightField.SeaLevel)
                    {
                        b = field.Frozen(x, z) ? blocks.Get("ice") : water;
                        heights[i + j * n] = (HeightField.SeaLevel + 1) * v;
                    }
                    else
                    {
                        b = blocks.Get(field.SurfaceKey(x, z, h));
                        heights[i + j * n] = (h + 1) * v;       // the top of the voxel ground
                    }
                    Color c = new Color32(b.R, b.G, b.B, 255);
                    if (h >= HeightField.SeaLevel && b.Key == "grass")
                    {
                        // Trees from afar: forest colour where a tree stands (raising the land
                        // there made spikes), so woods still read on the horizon.
                        TreeSpot tree = field.TreeInCell(FloorDiv(x, HeightField.TreeCell), FloorDiv(z, HeightField.TreeCell));
                        if (tree.Exists)
                        {
                            BlockDef leaf = blocks.Get(tree.Crimson ? "crimson_leaves" : "leaves");
                            c = Color.Lerp(c, new Color32(leaf.R, leaf.G, leaf.B, 255), 0.75f) * 0.85f;
                            c.a = 1f;
                        }
                        else
                        {
                            float lush = field.Lushness(x, z) / 65536f;
                            c = Color.Lerp(c, new Color(0.24f, 0.45f, 0.2f), Mathf.Clamp01(lush - 0.3f) * 0.5f);
                        }
                    }
                    colors[i + j * n] = c;
                }
            var d = new Data { Centre = centre, Positions = new Vector3[n * n], Normals = new Vector3[n * n], Colors = colors };
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    int k = i + j * n;
                    d.Positions[k] = new Vector3(-radius + i * spacing, heights[k], -radius + j * spacing);
                    float hl = heights[Mathf.Max(i - 1, 0) + j * n], hr = heights[Mathf.Min(i + 1, n - 1) + j * n];
                    float hd = heights[i + Mathf.Max(j - 1, 0) * n], hu = heights[i + Mathf.Min(j + 1, n - 1) * n];
                    d.Normals[k] = new Vector3(hl - hr, 2 * spacing, hd - hu).normalized;
                }
            d.Indices = new int[(n - 1) * (n - 1) * 6];
            int t = 0;
            for (int j = 0; j < n - 1; j++)
                for (int i = 0; i < n - 1; i++)
                {
                    int a = i + j * n;
                    d.Indices[t++] = a; d.Indices[t++] = a + n; d.Indices[t++] = a + 1;
                    d.Indices[t++] = a + 1; d.Indices[t++] = a + n; d.Indices[t++] = a + n + 1;
                }
            if (treesTo > treesFrom) AddTrees(d, field, blocks, centre, treesFrom, treesTo);
            return d;
        }

        /// <summary>
        /// Every tree in the ring between treesFrom and treesTo as one block of canopy, so
        /// distant woods have real height and outline instead of a flat green wash. The hole in
        /// the shader hides those that come inside the voxel chunks.
        /// </summary>
        static void AddTrees(Data d, HeightField field, BlockRegistry blocks, Vector2 centre, float treesFrom, float treesTo)
        {
            float v = WorldConfig.VoxelSize;
            int cell = HeightField.TreeCell;
            int cx0 = FloorDiv(Mathf.FloorToInt((centre.x - treesTo) / v), cell), cx1 = FloorDiv(Mathf.FloorToInt((centre.x + treesTo) / v), cell);
            int cz0 = FloorDiv(Mathf.FloorToInt((centre.y - treesTo) / v), cell), cz1 = FloorDiv(Mathf.FloorToInt((centre.y + treesTo) / v), cell);
            var pos = new System.Collections.Generic.List<Vector3>(d.Positions);
            var nrm = new System.Collections.Generic.List<Vector3>(d.Normals);
            var col = new System.Collections.Generic.List<Color32>(d.Colors);
            var idx = new System.Collections.Generic.List<int>(d.Indices);
            BlockDef green = blocks.Get("leaves"), red = blocks.Get("crimson_leaves");
            float from2 = treesFrom * treesFrom, to2 = treesTo * treesTo;
            for (int cz = cz0; cz <= cz1; cz++)
                for (int cx = cx0; cx <= cx1; cx++)
                {
                    float mx = (cx + 0.5f) * cell * v - centre.x, mz = (cz + 0.5f) * cell * v - centre.y;
                    float r2 = mx * mx + mz * mz;
                    if (r2 < from2 || r2 > to2) continue;
                    TreeSpot t = field.TreeInCell(cx, cz);
                    if (!t.Exists) continue;
                    float thick = t.Trunk < 28 ? 2 : 3;
                    float x = (t.X + thick * 0.5f) * v - centre.x, z = (t.Z + thick * 0.5f) * v - centre.y;
                    float top = (t.Base + t.Trunk) * v, half = t.Radius * 0.6f * v;
                    var lo = new Vector3(x - half, top - t.Radius * 0.6f * v, z - half);
                    var hi = new Vector3(x + half, top + t.Radius * 0.6f * v, z + half);
                    BlockDef leaf = t.Crimson ? red : green;
                    var c = new Color32((byte)(leaf.R * 0.8f), (byte)(leaf.G * 0.8f), (byte)(leaf.B * 0.8f), 255);
                    var side = new Color32((byte)(leaf.R * 0.6f), (byte)(leaf.G * 0.6f), (byte)(leaf.B * 0.6f), 255);
                    Quad(pos, nrm, col, idx, new Vector3(lo.x, hi.y, lo.z), new Vector3(lo.x, hi.y, hi.z), new Vector3(hi.x, hi.y, hi.z), new Vector3(hi.x, hi.y, lo.z), Vector3.up, c);
                    Quad(pos, nrm, col, idx, new Vector3(lo.x, lo.y, lo.z), new Vector3(lo.x, hi.y, lo.z), new Vector3(hi.x, hi.y, lo.z), new Vector3(hi.x, lo.y, lo.z), Vector3.back, side);
                    Quad(pos, nrm, col, idx, new Vector3(hi.x, lo.y, hi.z), new Vector3(hi.x, hi.y, hi.z), new Vector3(lo.x, hi.y, hi.z), new Vector3(lo.x, lo.y, hi.z), Vector3.forward, side);
                    Quad(pos, nrm, col, idx, new Vector3(lo.x, lo.y, hi.z), new Vector3(lo.x, hi.y, hi.z), new Vector3(lo.x, hi.y, lo.z), new Vector3(lo.x, lo.y, lo.z), Vector3.left, side);
                    Quad(pos, nrm, col, idx, new Vector3(hi.x, lo.y, lo.z), new Vector3(hi.x, hi.y, lo.z), new Vector3(hi.x, hi.y, hi.z), new Vector3(hi.x, lo.y, hi.z), Vector3.right, side);
                }
            d.Positions = pos.ToArray(); d.Normals = nrm.ToArray(); d.Colors = col.ToArray(); d.Indices = idx.ToArray();
        }

        /// <summary>A quad a-b-c-d, wound clockwise when seen from the side its normal faces.</summary>
        static void Quad(System.Collections.Generic.List<Vector3> pos, System.Collections.Generic.List<Vector3> nrm,
                         System.Collections.Generic.List<Color32> col, System.Collections.Generic.List<int> idx,
                         Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n, Color32 colour)
        {
            int f = pos.Count;
            pos.Add(a); pos.Add(b); pos.Add(c); pos.Add(d);
            for (int k = 0; k < 4; k++) { nrm.Add(n); col.Add(colour); }
            idx.Add(f); idx.Add(f + 1); idx.Add(f + 2);
            idx.Add(f); idx.Add(f + 2); idx.Add(f + 3);
        }

        static int FloorDiv(int a, int b) { int q = a / b; return (a % b != 0 && a < 0) ? q - 1 : q; }

        void Apply(Data d)
        {
            transform.position = new Vector3(d.Centre.x, 0f, d.Centre.y);
            _mesh.Clear();
            _mesh.vertices = d.Positions;
            _mesh.normals = d.Normals;
            _mesh.colors32 = d.Colors;
            _mesh.triangles = d.Indices;
            _mesh.RecalculateBounds();
        }
    }
}
