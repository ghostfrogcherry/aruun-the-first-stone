using System.Collections;
using System.Collections.Generic;
using Aruun.Core.Art;
using UnityEngine;

namespace Aruun.Game.Art
{
    /// <summary>
    /// Shows every voxel model in rows in front of the camera, so artists can check their work
    /// in the real game. Start the game with:
    ///   --gallery                 everything
    ///   --gallery=creatures       one category (creatures, props, flora, crops, items)
    ///   --clip=walk               play this clip on every rigged model that has it
    /// Works with --shot for a picture from the terminal.
    /// </summary>
    public sealed class ArtGallery : MonoBehaviour
    {
        string _category = "";
        string _clip = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Launch()
        {
            string category = null, clip = "";
            foreach (string arg in System.Environment.GetCommandLineArgs())
            {
                if (arg == "--gallery") category = "";
                else if (arg.StartsWith("--gallery=")) category = arg.Substring(10);
                else if (arg.StartsWith("--clip=")) clip = arg.Substring(7);
            }
            if (category == null) return;
            var g = new GameObject("Art gallery").AddComponent<ArtGallery>();
            g._category = category;
            g._clip = clip;
        }

        IEnumerator BuildNextFrame()
        {
            yield return null; // let Bootstrap place the camera first
            Build();
        }

        void Awake() { StartCoroutine(BuildNextFrame()); }

        /// <summary>The terrain shader, tinted stone grey through vertex colours on the cube.</summary>
        static Material StandMaterial()
        {
            return ModelLibrary.SolidMaterial;
        }

        void Build()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            // Face the camera across flat ground in front of it.
            Vector3 fwd = cam.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Vector3 start = cam.transform.position + fwd * 8f + Vector3.down * 3f;
            transform.SetPositionAndRotation(start, Quaternion.LookRotation(-fwd));

            List<string> ids = ModelLibrary.Catalog.InCategory(_category);
            const float rowWidth = 24f;
            float x = 0f, z = 0f, rowDepth = 0f;
            var stand = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stand.name = "Stand";
            stand.GetComponent<Renderer>().sharedMaterial = StandMaterial(); // Unity's default shader is not in builds
            stand.transform.SetParent(transform, false);
            foreach (string id in ids)
            {
                Mesh m = ModelLibrary.GetMesh(id);
                Bounds b = m.bounds;
                float w = Mathf.Max(b.size.x, b.size.z) + 0.6f;
                if (x + w > rowWidth) { x = 0f; z += rowDepth + 0.8f; rowDepth = 0f; }
                GameObject go = ModelLibrary.Instantiate(id, transform);
                go.transform.localPosition = new Vector3(rowWidth * 0.5f - x - w * 0.5f, 0f, -z - b.size.z * 0.5f);
                go.transform.localRotation = Quaternion.identity; // models face +z, which is toward the camera here
                var rig = go.GetComponent<VoxelRig>();
                if (rig != null && _clip != "" && rig.HasClip(_clip)) rig.Play(_clip);
                x += w;
                rowDepth = Mathf.Max(rowDepth, b.size.z);
            }
            float depth = z + rowDepth + 1f;
            stand.transform.localPosition = new Vector3(0f, -0.25f, -depth * 0.5f + 0.5f);
            stand.transform.localScale = new Vector3(rowWidth + 1f, 0.5f, depth + 1f);
            Mesh cube = stand.GetComponent<MeshFilter>().mesh;   // the voxel shader colours by vertex
            var grey = new Color32[cube.vertexCount];
            for (int i = 0; i < grey.Length; i++) grey[i] = new Color32(0x8a, 0x84, 0x7c, 255);
            cube.colors32 = grey;
            Debug.Log("Art gallery: " + ids.Count + " models" + (_category != "" ? " in " + _category : ""));
        }
    }
}
