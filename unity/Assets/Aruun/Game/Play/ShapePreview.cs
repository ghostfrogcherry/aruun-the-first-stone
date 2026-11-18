using Aruun.Core;
using Aruun.Game.Art;
using UnityEngine;

namespace Aruun.Game
{
    /// <summary>
    /// The outline of the shape being built: twelve thin edges around the cells it will cover.
    /// Plain, earth-toned and unlit by design; it marks space, it is not magic.
    /// </summary>
    public sealed class ShapePreview : MonoBehaviour
    {
        static readonly Color Edge = new Color32(0xd5, 0xb7, 0x7c, 0xff);   // Warm Sand
        Transform[] _edges;

        void Awake()
        {
            Mesh cube = InscriptionView.Bit(Edge);
            _edges = new Transform[12];
            for (int i = 0; i < 12; i++)
            {
                var go = new GameObject("edge");
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = cube;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = ModelLibrary.SolidMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                _edges[i] = go.transform;
            }
            Hide();
        }

        public void Hide() { gameObject.SetActive(false); }

        /// <summary>Shows the box covering voxel cells (x0..x1, y0..y1, z0..z1), inclusive.</summary>
        public void Show(int x0, int y0, int z0, int x1, int y1, int z1)
        {
            gameObject.SetActive(true);
            float v = (float)WorldConfig.VoxelSize, t = 0.03f;
            Vector3 lo = new Vector3(Mathf.Min(x0, x1), Mathf.Min(y0, y1), Mathf.Min(z0, z1)) * v - Vector3.one * 0.01f;
            Vector3 hi = new Vector3(Mathf.Max(x0, x1) + 1, Mathf.Max(y0, y1) + 1, Mathf.Max(z0, z1) + 1) * v + Vector3.one * 0.01f;
            Vector3 size = hi - lo, mid = (lo + hi) * 0.5f;
            int i = 0;
            for (int a = 0; a < 2; a++)
                for (int b = 0; b < 2; b++)
                {
                    // Edges along x, y and z, at each pair of the other two sides.
                    Place(i++, new Vector3(mid.x, a == 0 ? lo.y : hi.y, b == 0 ? lo.z : hi.z), new Vector3(size.x + t, t, t));
                    Place(i++, new Vector3(a == 0 ? lo.x : hi.x, mid.y, b == 0 ? lo.z : hi.z), new Vector3(t, size.y + t, t));
                    Place(i++, new Vector3(a == 0 ? lo.x : hi.x, b == 0 ? lo.y : hi.y, mid.z), new Vector3(t, t, size.z + t));
                }
        }

        void Place(int i, Vector3 centre, Vector3 scale)
        {
            // The Bit cube is 0.86 x 0.86 x 1 units: undo that so scale is the real size.
            _edges[i].position = centre;
            _edges[i].localScale = new Vector3(scale.x / 0.86f, scale.y / 0.86f, scale.z);
        }
    }
}
