using System.Collections.Generic;
using System.IO;
using Aruun.Core.Art;
using UnityEngine;
using UnityEngine.Rendering;

namespace Aruun.Game.Art
{
    /// <summary>
    /// The voxel models in content/models, ready to put in the world.
    ///
    ///   GameObject jar = ModelLibrary.Instantiate("clay_jar");     // feet at its origin, facing +z
    ///   GameObject strider = ModelLibrary.Instantiate("redstrider");
    ///   strider.GetComponent&lt;VoxelRig&gt;().Play("walk");
    ///
    /// Sizes are real: 1 Unity unit = 1 m and each model voxel is voxel_m (usually 1/16 m).
    /// Meshes are built once per model and shared.
    /// </summary>
    public static class ModelLibrary
    {
        static ModelCatalog _catalog;
        static readonly Dictionary<string, VoxFile> _files = new Dictionary<string, VoxFile>();
        static readonly Dictionary<string, Mesh> _meshes = new Dictionary<string, Mesh>();
        static Material _solid, _glow;

        public static ModelCatalog Catalog
        {
            get
            {
                if (_catalog == null)
                {
                    string models = GameContent.ReadText("models", "models.json");
                    string palettePath = Path.Combine(GameContent.Root, "art", "palette.json");
                    _catalog = ModelCatalog.FromJson(models, System.IO.File.Exists(palettePath) ? System.IO.File.ReadAllText(palettePath) : null);
                }
                return _catalog;
            }
        }

        public static bool Has(string id) { return Catalog.Has(id); }
        public static ModelInfo Info(string id) { return Catalog.Get(id); }

        /// <summary>The whole model as one mesh in its rest pose, in metres. Origin = ground centre.</summary>
        public static Mesh GetMesh(string id)
        {
            Mesh mesh;
            if (_meshes.TryGetValue(id, out mesh)) return mesh;
            VoxFile file = VoxOf(id);
            var mesher = new ModelMesher();
            bool[] glow = Catalog.GlowFlags(file);
            foreach (VoxModel m in file.Models) mesher.Add(m, file.Palette, glow, 0, 0, 0);
            mesh = ToMesh(mesher.Data, Info(id).VoxelMeters, id);
            _meshes[id] = mesh;
            return mesh;
        }

        /// <summary>One part of a rigged model with its pivot at the mesh origin.</summary>
        public static Mesh GetPartMesh(string id, RigPart part)
        {
            string key = id + "/" + part.Name;
            Mesh mesh;
            if (_meshes.TryGetValue(key, out mesh)) return mesh;
            VoxFile file = VoxOf(id);
            VoxModel m = file.Find(part.Name);
            var mesher = new ModelMesher();
            if (m != null) mesher.Add(m, file.Palette, Catalog.GlowFlags(file), -part.PivotX, -part.PivotY, -part.PivotZ);
            mesh = ToMesh(mesher.Data, Info(id).VoxelMeters, key);
            _meshes[key] = mesh;
            return mesh;
        }

        /// <summary>
        /// A new object showing the model. Rigged models get a VoxelRig with one child per part
        /// and start playing "idle" if they have it.
        /// </summary>
        public static GameObject Instantiate(string id, Transform parent = null)
        {
            ModelInfo info = Info(id);
            var go = new GameObject(id);
            if (parent != null) go.transform.SetParent(parent, false);
            if (info.IsRigged)
                go.AddComponent<VoxelRig>().Setup(id);
            else
                AddRenderer(go, GetMesh(id));
            return go;
        }

        public static void AddRenderer(GameObject go, Mesh mesh)
        {
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = mesh.subMeshCount > 1 ? new[] { SolidMaterial, GlowMaterial } : new[] { SolidMaterial };
            r.shadowCastingMode = ShadowCastingMode.On;
        }

        public static Material SolidMaterial
        {
            get { return _solid != null ? _solid : (_solid = new Material(Resources.Load<Shader>("AruunVoxel")) { name = "Aruun model" }); }
        }

        /// <summary>Magick glow (teal and violet), lamps and embers: unlit and bright.</summary>
        public static Material GlowMaterial
        {
            get { return _glow != null ? _glow : (_glow = new Material(Resources.Load<Shader>("AruunVoxelGlow")) { name = "Aruun glow" }); }
        }

        /// <summary>The .vox file behind a model, read once.</summary>
        public static VoxFile VoxOf(string id)
        {
            VoxFile f;
            if (!_files.TryGetValue(id, out f))
            {
                f = VoxFile.Load(Path.Combine(GameContent.Root, "models", Info(id).File));
                _files[id] = f;
            }
            return f;
        }

        static readonly List<Vector3> _v = new List<Vector3>();
        static readonly List<Vector3> _n = new List<Vector3>();
        static readonly List<Color32> _c = new List<Color32>();

        public static Mesh ToMesh(ModelMeshData d, float scale, string name)
        {
            _v.Clear(); _n.Clear(); _c.Clear();
            for (int i = 0; i < d.VertexCount; i++)
            {
                _v.Add(new Vector3(d.Positions[i * 3], d.Positions[i * 3 + 1], d.Positions[i * 3 + 2]) * scale);
                _n.Add(new Vector3(d.Normals[i * 3], d.Normals[i * 3 + 1], d.Normals[i * 3 + 2]));
                _c.Add(new Color32(d.Colors[i * 4], d.Colors[i * 4 + 1], d.Colors[i * 4 + 2], d.Colors[i * 4 + 3]));
            }
            var mesh = new Mesh { name = name };
            if (_v.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(_v);
            mesh.SetNormals(_n);
            mesh.SetColors(_c);
            mesh.subMeshCount = d.Glow.Count > 0 ? 2 : 1;
            mesh.SetTriangles(d.Solid, 0);
            if (d.Glow.Count > 0) mesh.SetTriangles(d.Glow, 1);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
