using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
    /// Shows a VoxelWorld: keeps chunk meshes built around a viewer and rebuilds them when the
    /// world is edited. Generation and meshing run on worker threads; only the final copy into
    /// a Unity mesh happens on the main thread.
    ///
    /// This object is scaled by the voxel size, so its children work in voxel coordinates
    /// while the rest of Unity works in metres.
    /// </summary>
    public sealed class WorldView : MonoBehaviour
    {
        public VoxelWorld World { get; private set; }
        public Transform Viewer;
        public int ViewChunks = 16;          // horizontal radius in chunks (8 m each)
        /// <summary>Chunks kept around the viewer's own height, for building in the air or digging down.</summary>
        public int ViewChunksDown = 2, ViewChunksUp = 2;
        /// <summary>How far out (chunks) the band around the viewer reaches when they are far above the ground.</summary>
        public int AloftChunks = 5;
        public int MeshesPerFrame = 6;

        public int ShownChunks { get { return _shown.Count; } }
        public int PendingChunks { get { return _inFlight; } }

        sealed class Built { public ChunkCoord Coord; public int Version; public MeshData Mesh; public GreedyMesher Mesher; public ushort[] Padded; }

        readonly Dictionary<ChunkCoord, GameObject> _shown = new Dictionary<ChunkCoord, GameObject>();
        readonly Dictionary<ChunkCoord, int> _version = new Dictionary<ChunkCoord, int>();
        readonly HashSet<ChunkCoord> _empty = new HashSet<ChunkCoord>();
        readonly HashSet<ChunkCoord> _requested = new HashSet<ChunkCoord>();
        readonly ConcurrentQueue<Built> _done = new ConcurrentQueue<Built>();
        readonly ConcurrentBag<Built> _pool = new ConcurrentBag<Built>();
        readonly List<ChunkCoord> _wanted = new List<ChunkCoord>();
        readonly Dictionary<Vector2Int, Vector2Int> _columns = new Dictionary<Vector2Int, Vector2Int>();
        // Terrain spans never change for a seed, so each column is sampled once, not every time
        // the viewer crosses a chunk border (that was a visible hitch).
        readonly Dictionary<Vector2Int, Vector2Int> _spanCache = new Dictionary<Vector2Int, Vector2Int>();
        readonly List<Vector3> _positions = new List<Vector3>(), _normals = new List<Vector3>();
        readonly List<Color32> _colors = new List<Color32>();
        Material _opaque, _transparent;
        // Lamps per chunk (world metres, w = voxel count), and the nearest ones handed to the shader.
        readonly Dictionary<ChunkCoord, Vector4[]> _lights = new Dictionary<ChunkCoord, Vector4[]>();
        readonly List<Vector4> _near = new List<Vector4>();
        readonly Vector4[] _lampArray = new Vector4[MaxLamps];
        public const int MaxLamps = 16;
        /// <summary>How far a lamp's light reaches, in metres.</summary>
        public float LampRadius = 10f;
        static readonly int LampsId = Shader.PropertyToID("_AruunLamps");
        static readonly int LampCountId = Shader.PropertyToID("_AruunLampCount");
        /// <summary>Lamps lighting the scene now (for the stats overlay).</summary>
        public int LitLamps { get; private set; }
        public int KnownLamps { get { int n = 0; foreach (var l in _lights.Values) n += l.Length; return n; } }
        ChunkCoord _lastCentre = new ChunkCoord(int.MinValue, 0, 0);
        int _inFlight, _maxInFlight;
        BlockRegistry _blocks;

        public void Init(VoxelWorld world, Transform viewer)
        {
            World = world;
            Viewer = viewer;
            _blocks = world.Blocks;
            transform.localScale = Vector3.one * WorldConfig.VoxelSize;
            _maxInFlight = Mathf.Max(2, SystemInfo.processorCount - 2);

            Shader shader = Resources.Load<Shader>("AruunVoxel");
            _opaque = new Material(shader) { name = "Aruun voxels" };
            _transparent = new Material(shader) { name = "Aruun water", renderQueue = (int)RenderQueue.Transparent };
            _transparent.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            _transparent.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            _transparent.SetFloat("_ZWrite", 0f);
            _transparent.SetShaderPassEnabled("ShadowCaster", false); // water casts no shadow
        }

        /// <summary>Viewer position in voxel coordinates.</summary>
        public Vector3 ViewerVoxel { get { return transform.InverseTransformPoint(Viewer.position); } }

        /// <summary>Throws away every chunk and mesh and builds them again (dev tool).</summary>
        public void ReloadAll()
        {
            foreach (var pair in _shown)
            {
                Destroy(pair.Value.GetComponent<MeshFilter>().sharedMesh);
                Destroy(pair.Value);
                World.Unload(pair.Key);
            }
            _shown.Clear(); _version.Clear(); _requested.Clear(); _empty.Clear(); _wanted.Clear(); _lights.Clear();
            _lastCentre = new ChunkCoord(int.MinValue, 0, 0);
        }

        /// <summary>Vertices and triangles of every chunk on screen (for the stats overlay).</summary>
        public void CountGeometry(out long vertices, out long triangles)
        {
            vertices = 0; triangles = 0;
            foreach (var go in _shown.Values)
            {
                Mesh m = go.GetComponent<MeshFilter>().sharedMesh;
                vertices += m.vertexCount;
                for (int i = 0; i < m.subMeshCount; i++) triangles += (long)m.GetIndexCount(i) / 3;
            }
        }

        public int WorkerThreads { get { return _maxInFlight; } }

        /// <summary>Rebuild these chunks soon: the world changed under them.</summary>
        public void MarkDirty(IEnumerable<ChunkCoord> coords)
        {
            foreach (ChunkCoord c in coords)
            {
                _empty.Remove(c);
                if (_shown.ContainsKey(c) || InRange(c, _lastCentre)) Request(c);
            }
        }

        void Update()
        {
            if (World == null || Viewer == null) return;
            Vector3 v = ViewerVoxel;
            ChunkCoord centre = ChunkCoord.FromVoxel(Mathf.FloorToInt(v.x), Mathf.FloorToInt(v.y), Mathf.FloorToInt(v.z));
            if (!centre.Equals(_lastCentre))
            {
                _lastCentre = centre;
                PlanAround(centre);
            }
            // Hand out work nearest first, a few chunks in flight at a time.
            while (_inFlight < _maxInFlight && _wanted.Count > 0)
            {
                ChunkCoord next = _wanted[_wanted.Count - 1];
                _wanted.RemoveAt(_wanted.Count - 1);
                if (!_shown.ContainsKey(next) && !_empty.Contains(next) && !_requested.Contains(next) && InRange(next, centre)) Request(next);
            }
            for (int i = 0; i < MeshesPerFrame; i++)
            {
                Built b;
                if (!_done.TryDequeue(out b)) break;
                _inFlight--;
                Show(b);
                _pool.Add(b);
            }
            UpdateLamps();
        }

        /// <summary>
        /// Hands the lamps nearest the camera to the voxel shader, which adds their warm light
        /// per pixel. No extra passes, so many lamps cost little; they cast no shadows.
        /// </summary>
        void UpdateLamps()
        {
            Camera cam = Camera.main;
            Vector3 eye = cam != null ? cam.transform.position : Viewer.position;
            float reach = 64f, reach2 = reach * reach;
            _near.Clear();
            foreach (var arr in _lights.Values)
                foreach (Vector4 l in arr)
                {
                    float dx = l.x - eye.x, dy = l.y - eye.y, dz = l.z - eye.z;
                    if (dx * dx + dy * dy + dz * dz < reach2) _near.Add(l);
                }
            if (_near.Count > MaxLamps)
                _near.Sort((a, b) => Dist2(a, eye).CompareTo(Dist2(b, eye)));
            int n = Mathf.Min(_near.Count, MaxLamps);
            for (int i = 0; i < MaxLamps; i++)
            {
                if (i < n)
                {
                    Vector4 l = _near[i];
                    // Bigger clusters (a lamp column) reach a little farther.
                    _lampArray[i] = new Vector4(l.x, l.y, l.z, LampRadius * Mathf.Min(1.6f, 0.9f + 0.1f * l.w));
                }
                else _lampArray[i] = Vector4.zero;
            }
            Shader.SetGlobalVectorArray(LampsId, _lampArray);
            Shader.SetGlobalFloat(LampCountId, n);
            LitLamps = n;
        }

        static float Dist2(Vector4 l, Vector3 p)
        {
            float dx = l.x - p.x, dy = l.y - p.y, dz = l.z - p.z;
            return dx * dx + dy * dy + dz * dz;
        }

        void SetLights(ChunkCoord c, MeshData m)
        {
            if (m == null || m.Lights.Count == 0) { _lights.Remove(c); return; }
            var arr = new Vector4[m.Lights.Count / 4];
            float s = WorldConfig.VoxelSize;
            for (int i = 0; i < arr.Length; i++)
                arr[i] = new Vector4((c.OriginX + m.Lights[i * 4]) * s, (c.OriginY + m.Lights[i * 4 + 1]) * s,
                                     (c.OriginZ + m.Lights[i * 4 + 2]) * s, m.Lights[i * 4 + 3]);
            _lights[c] = arr;
        }

        void PlanAround(ChunkCoord centre)
        {
            _wanted.Clear();
            _columns.Clear();
            int clo, chi;
            TerrainSpan(centre.X, centre.Z, out clo, out chi);
            bool aloft = centre.Y > chi + 4;
            for (int dz = -ViewChunks - 2; dz <= ViewChunks + 2; dz++)
                for (int dx = -ViewChunks - 2; dx <= ViewChunks + 2; dx++)
                {
                    if (dx * dx + dz * dz > (ViewChunks + 2) * (ViewChunks + 2)) continue;
                    int cx = centre.X + dx, cz = centre.Z + dz;
                    int lo, hi;
                    TerrainSpan(cx, cz, out lo, out hi);
                    // The terrain's own span everywhere, so the ground shows from any height;
                    // near the viewer also a band around their height, for building in the air.
                    // Far above the ground (the college library is a hall in the sky) the band
                    // reaches out to the edge of view, so the whole hall shows.
                    if (dx * dx + dz * dz <= 4 || (aloft && dx * dx + dz * dz <= AloftChunks * AloftChunks))
                    {
                        lo = Math.Min(lo, centre.Y - ViewChunksDown);
                        hi = Math.Max(hi, centre.Y + ViewChunksUp);
                    }
                    _columns[new Vector2Int(cx, cz)] = new Vector2Int(lo, hi);
                    if (dx * dx + dz * dz > ViewChunks * ViewChunks) continue;
                    for (int cy = lo; cy <= hi; cy++)
                    {
                        var c = new ChunkCoord(cx, cy, cz);
                        if (!_shown.ContainsKey(c) && !_empty.Contains(c)) _wanted.Add(c);
                    }
                }
            // Farthest first, because work is taken from the end of the list.
            _wanted.Sort((a, b) => Dist2(b, centre).CompareTo(Dist2(a, centre)));

            // Let go of chunks that are now well out of view.
            var drop = new List<ChunkCoord>();
            foreach (var pair in _shown) if (!InRange(pair.Key, centre, 2)) drop.Add(pair.Key);
            foreach (ChunkCoord c in drop)
            {
                Mesh mesh = _shown[c].GetComponent<MeshFilter>().sharedMesh;
                Destroy(_shown[c]);
                Destroy(mesh);
                _shown.Remove(c);
                _version.Remove(c);
                _requested.Remove(c);
                _lights.Remove(c);
                World.Unload(c);
            }
            _empty.RemoveWhere(c => !InRange(c, centre, 2));
        }

        /// <summary>
        /// The chunk heights a column of chunks can have terrain in: sampled ground heights,
        /// down to a little below (cliff faces, lake beds) and up past the tallest tree.
        /// </summary>
        void TerrainSpan(int cx, int cz, out int lo, out int hi)
        {
            var key = new Vector2Int(cx, cz);
            Vector2Int cached;
            if (_spanCache.TryGetValue(key, out cached)) { lo = cached.x; hi = cached.y; return; }
            if (_spanCache.Count > 40000) _spanCache.Clear();
            SampleSpan(cx, cz, out lo, out hi);
            _spanCache[key] = new Vector2Int(lo, hi);
        }

        void SampleSpan(int cx, int cz, out int lo, out int hi)
        {
            var field = World.Generator.Field;
            int min = int.MaxValue, max = int.MinValue;
            const int n = WorldConfig.ChunkSize;
            for (int sz = 0; sz <= n; sz += n / 4)
                for (int sx = 0; sx <= n; sx += n / 4)
                {
                    int h = field.HeightAt(cx * n + sx, cz * n + sz);
                    if (h < min) min = h;
                    if (h > max) max = h;
                }
            if (max < HeightField.SeaLevel) max = HeightField.SeaLevel;
            lo = (min - 12) >> WorldConfig.ChunkShift;
            hi = (max + 52) >> WorldConfig.ChunkShift; // trees reach about 48 voxels above the ground
        }

        bool InRange(ChunkCoord c, ChunkCoord centre, int slack = 0)
        {
            int r = ViewChunks + slack;
            int dx = c.X - centre.X, dz = c.Z - centre.Z;
            if (dx * dx + dz * dz > r * r) return false;
            Vector2Int span;
            if (!_columns.TryGetValue(new Vector2Int(c.X, c.Z), out span)) return false;
            return c.Y >= span.x - slack && c.Y <= span.y + slack;
        }

        static int Dist2(ChunkCoord a, ChunkCoord b)
        {
            int dx = a.X - b.X, dy = (a.Y - b.Y) * 2, dz = a.Z - b.Z;
            return dx * dx + dy * dy + dz * dz;
        }

        void Request(ChunkCoord coord)
        {
            _requested.Add(coord);
            int version;
            _version.TryGetValue(coord, out version);
            _version[coord] = ++version;
            _inFlight++;
            Built b;
            if (!_pool.TryTake(out b))
                b = new Built { Mesh = new MeshData(), Mesher = new GreedyMesher(_blocks), Padded = new ushort[GreedyMesher.PaddedVolume] };
            b.Coord = coord;
            b.Version = version;
            VoxelWorld world = World;
            Task.Run(() =>
            {
                try
                {
                    world.FillPadded(b.Coord, b.Padded);
                    b.Mesher.Build(b.Padded, b.Mesh);
                }
                catch (Exception e)
                {
                    b.Mesh.Clear();
                    Debug.LogException(e);
                }
                _done.Enqueue(b);
            });
        }

        void Show(Built b)
        {
            int latest;
            if (!_version.TryGetValue(b.Coord, out latest) || latest != b.Version) return; // a newer build is on its way
            _requested.Remove(b.Coord);
            SetLights(b.Coord, b.Mesh);
            GameObject go;
            _shown.TryGetValue(b.Coord, out go);
            if (b.Mesh.IsEmpty)
            {
                if (go != null) { Destroy(go.GetComponent<MeshFilter>().sharedMesh); Destroy(go); _shown.Remove(b.Coord); }
                _empty.Add(b.Coord);
                return;
            }
            Mesh mesh;
            if (go == null)
            {
                go = new GameObject("Chunk " + b.Coord);
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(b.Coord.OriginX, b.Coord.OriginY, b.Coord.OriginZ);
                mesh = new Mesh { name = go.name };
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterials = new[] { _opaque, _transparent };
                _shown[b.Coord] = go;
            }
            else
            {
                mesh = go.GetComponent<MeshFilter>().sharedMesh;
                mesh.Clear();
            }

            MeshData m = b.Mesh;
            _positions.Clear(); _normals.Clear(); _colors.Clear();
            for (int i = 0; i < m.Positions.Count; i += 3)
            {
                _positions.Add(new Vector3(m.Positions[i], m.Positions[i + 1], m.Positions[i + 2]));
                _normals.Add(new Vector3(m.Normals[i], m.Normals[i + 1], m.Normals[i + 2]));
            }
            for (int i = 0; i < m.Colors.Count; i += 4)
                _colors.Add(new Color32(m.Colors[i], m.Colors[i + 1], m.Colors[i + 2], m.Colors[i + 3]));
            mesh.indexFormat = _positions.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(_positions);
            mesh.SetNormals(_normals);
            mesh.SetColors(_colors);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(m.Opaque, 0, false);
            mesh.SetTriangles(m.Transparent, 1, false);
            mesh.bounds = new Bounds(Vector3.one * (WorldConfig.ChunkSize * 0.5f), Vector3.one * WorldConfig.ChunkSize);
        }
    }
}
