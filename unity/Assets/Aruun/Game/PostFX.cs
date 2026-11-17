using UnityEngine;

namespace Aruun.Game
{
    /// <summary>
    /// The mood layer on the camera: bloom, light shafts from the sun, ACES tone mapping,
    /// colour grading and a vignette. Hand-written for the Built-in pipeline so it needs no
    /// package. F12 turns it off to compare.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class PostFX : MonoBehaviour
    {
        public bool On = true;
        public float Threshold = 1.0f, Knee = 0.5f, BloomIntensity = 0.55f;
        public float ShaftStrength = 0.45f;
        public float Exposure = 1.1f, Saturation = 1.12f, Contrast = 1.06f, Vignette = 0.28f;
        public Color Lift = new Color(0.015f, 0.02f, 0.045f), Gain = new Color(1.03f, 1.0f, 0.95f);

        static readonly int PostOnId = Shader.PropertyToID("_AruunPostOn");
        Material _mat;
        Camera _cam;
        readonly RenderTexture[] _levels = new RenderTexture[8];

        void OnEnable()
        {
            _cam = GetComponent<Camera>();
            _cam.allowHDR = true;
            _cam.depthTextureMode |= DepthTextureMode.Depth;
            Shader s = Resources.Load<Shader>("AruunPost");
            if (s == null || !s.isSupported) { Debug.LogWarning("Aruun post shader missing or unsupported; post-processing off."); enabled = false; return; }
            _mat = new Material(s) { hideFlags = HideFlags.HideAndDontSave };
        }

        void OnDisable() { Shader.SetGlobalFloat(PostOnId, 0f); }

        void Update() { Shader.SetGlobalFloat(PostOnId, On && _mat != null ? 1f : 0f); }

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (!On || _mat == null) { Graphics.Blit(src, dst); return; }
            RenderTextureFormat fmt = src.format;
            int w = src.width / 2, h = src.height / 2;

            // Bloom: a chain of half-size copies, then added back up.
            _mat.SetVector("_Threshold", new Vector4(Threshold, Knee, 30f, 0f));
            int count = 0;
            RenderTexture last = src;
            for (int i = 0; i < _levels.Length && w >= 8 && h >= 8; i++, w /= 2, h /= 2)
            {
                _levels[i] = RenderTexture.GetTemporary(w, h, 0, fmt);
                Graphics.Blit(last, _levels[i], _mat, i == 0 ? 0 : 1);
                last = _levels[i];
                count++;
            }
            for (int i = count - 2; i >= 0; i--) Graphics.Blit(_levels[i + 1], _levels[i], _mat, 2);

            // Light shafts toward the sun when it is in front of the camera.
            Vector4 sunDir = Shader.GetGlobalVector("_AruunSunDir");
            Vector3 sp = _cam.WorldToViewportPoint(transform.position + new Vector3(sunDir.x, sunDir.y, sunDir.z) * 1000f);
            float facing = Mathf.Clamp01(Vector3.Dot(transform.forward, ((Vector3)sunDir).normalized));
            float strength = sp.z > 0 && sunDir.y > -0.02f ? ShaftStrength * facing * facing : 0f;
            _mat.SetVector("_SunScreen", new Vector4(sp.x, sp.y, strength, 0f));
            Color sunCol = Shader.GetGlobalColor("_AruunSunColor").linear;
            _mat.SetColor("_ShaftColor", new Color(sunCol.r, sunCol.g * 0.85f, sunCol.b * 0.65f));
            RenderTexture mask = RenderTexture.GetTemporary(src.width / 4, src.height / 4, 0, RenderTextureFormat.R8);
            RenderTexture shafts = RenderTexture.GetTemporary(src.width / 4, src.height / 4, 0, RenderTextureFormat.R8);
            if (strength > 0f)
            {
                Graphics.Blit(src, mask, _mat, 3);
                Graphics.Blit(mask, shafts, _mat, 4);
                Graphics.Blit(shafts, mask, _mat, 4);   // twice for longer, smoother rays
                _mat.SetTexture("_ShaftTex", mask);
            }
            else _mat.SetTexture("_ShaftTex", Texture2D.blackTexture);

            _mat.SetTexture("_BloomTex", count > 0 ? (Texture)_levels[0] : Texture2D.blackTexture);
            _mat.SetFloat("_BloomIntensity", BloomIntensity);
            _mat.SetFloat("_Exposure", Exposure);
            _mat.SetFloat("_Saturation", Saturation);
            _mat.SetFloat("_Contrast", Contrast);
            _mat.SetFloat("_Vignette", Vignette);
            _mat.SetColor("_Lift", Lift);
            _mat.SetColor("_Gain", Gain);
            Graphics.Blit(src, dst, _mat, 5);

            for (int i = 0; i < count; i++) { RenderTexture.ReleaseTemporary(_levels[i]); _levels[i] = null; }
            RenderTexture.ReleaseTemporary(mask);
            RenderTexture.ReleaseTemporary(shafts);
        }
    }
}
