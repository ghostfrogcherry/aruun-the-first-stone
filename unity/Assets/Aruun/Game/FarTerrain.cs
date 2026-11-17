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
            _mesh = new Mesh { name = "Far terrain", indexFormat = IndexFormat.UInt32 };
            gameObject.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var r = gameObject.AddComponent<MeshRenderer>();
            r.sharedMaterial = _material;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        public void SetHole(float holeRadius) { if (_material != null) _material.SetFloat("_HoleRadius", holeRadius); }

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
            _job = Task.Run(() => Build(field, blocks, at, spacing, radius));
        }

        static Data Build(HeightField field, BlockRegistry blocks, Vector2 centre, float spacing, float radius)
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
            return d;
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
