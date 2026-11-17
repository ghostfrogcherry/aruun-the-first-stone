using UnityEngine;

namespace Aruun.Game
{
    /// <summary>
    /// The sun's path and the colour of the sky through the day. Sets the shader globals the
    /// voxel, glow and sky shaders read (_AruunSunDir, _AruunSunColor, _AruunAmbient,
    /// _AruunSkyTop, _AruunSkyHorizon, _AruunFogColor), and turns the scene's sun light.
    /// Aruun's day is close to Earth's (24 h 37 min); in game a full day lasts DayMinutes.
    /// </summary>
    public sealed class DayNight : MonoBehaviour
    {
        public static DayNight Instance { get; private set; }

        /// <summary>Hour of the day, 0..24. Noon is 12.</summary>
        public float Hour = 9.5f;
        public float DayMinutes = 30f;
        public bool Paused;
        public Light Sun;

        static readonly int SunDirId = Shader.PropertyToID("_AruunSunDir");
        static readonly int SunColorId = Shader.PropertyToID("_AruunSunColor");
        static readonly int AmbientId = Shader.PropertyToID("_AruunAmbient");
        static readonly int SkyTopId = Shader.PropertyToID("_AruunSkyTop");
        static readonly int SkyHorizonId = Shader.PropertyToID("_AruunSkyHorizon");
        static readonly int FogColorId = Shader.PropertyToID("_AruunFogColor");

        // Key colours through the day (sRGB): night, dawn/dusk, day.
        static readonly Color NightTop = new Color(0.02f, 0.03f, 0.08f), NightHorizon = new Color(0.07f, 0.09f, 0.16f);
        static readonly Color DawnTop = new Color(0.25f, 0.33f, 0.55f), DawnHorizon = new Color(0.98f, 0.62f, 0.42f);
        static readonly Color DayTop = new Color(0.26f, 0.52f, 0.88f), DayHorizon = new Color(0.70f, 0.83f, 0.95f);

        float _pendingHour = -1f;
        int _pendingFrame;

        void Awake() { Instance = this; }

        /// <summary>
        /// Sets the hour a frame from now, so systems that keep their own clock (the play
        /// session's world time) see it as a change and follow it, instead of overwriting it.
        /// </summary>
        public void SetHourSoon(float hour) { _pendingHour = hour; _pendingFrame = Time.frameCount + 2; }

        public void Shift(float hours) { Hour = Mathf.Repeat(Hour + hours, 24f); Apply(); }

        public string Clock
        {
            get
            {
                int h = Mathf.FloorToInt(Hour), m = Mathf.FloorToInt((Hour - h) * 60f);
                return h.ToString("00") + ":" + m.ToString("00") + (Paused ? " (paused)" : "");
            }
        }

        void Update()
        {
            if (_pendingHour >= 0f && Time.frameCount >= _pendingFrame) { Hour = _pendingHour; _pendingHour = -1f; }
            else if (!Paused) Hour = Mathf.Repeat(Hour + Time.deltaTime * 24f / (DayMinutes * 60f), 24f);
            Apply();
        }

        public void Apply()
        {
            // The sun rises in the east (+X), peaks a little to the south, and sets in the west.
            float angle = (Hour - 6f) / 12f * Mathf.PI;               // 0 at 6:00, pi at 18:00
            Vector3 sunDir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), -0.35f).normalized;
            float height = sunDir.y;                                  // -1..1
            float day = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.12f, 0.35f, height));
            float glow = Mathf.Clamp01(1f - Mathf.Abs(height) / 0.3f) * Mathf.Clamp01((height + 0.2f) / 0.2f); // near the horizon

            Color top = Color.Lerp(NightTop, DayTop, day);
            Color horizon = Color.Lerp(NightHorizon, DayHorizon, day);
            top = Color.Lerp(top, DawnTop, glow * 0.6f);
            horizon = Color.Lerp(horizon, DawnHorizon, glow * 0.7f);

            Color sunColor = Color.Lerp(new Color(1f, 0.55f, 0.32f), new Color(1f, 0.96f, 0.88f), Mathf.Clamp01(height * 3f));
            sunColor *= Mathf.Clamp01(height * 6f + 0.1f) * 1.05f;
            // Moonlight keeps nights readable.
            Color ambient = Color.Lerp(new Color(0.10f, 0.12f, 0.20f), new Color(0.46f, 0.55f, 0.66f), day);
            ambient = Color.Lerp(ambient, new Color(0.55f, 0.45f, 0.45f), glow * 0.3f);

            Shader.SetGlobalVector(SunDirId, height > -0.05f ? sunDir : new Vector3(-sunDir.x, -sunDir.y, sunDir.z) * 1f);
            Shader.SetGlobalColor(SunColorId, height > -0.05f ? sunColor : new Color(0.12f, 0.14f, 0.22f));
            Shader.SetGlobalColor(AmbientId, ambient);
            Shader.SetGlobalColor(SkyTopId, top);
            Shader.SetGlobalColor(SkyHorizonId, horizon);
            Shader.SetGlobalColor(FogColorId, horizon);
            Camera cam = Camera.main;
            if (cam != null) cam.backgroundColor = horizon;
            if (Sun != null)
            {
                Vector3 lightFrom = height > -0.05f ? sunDir : new Vector3(-sunDir.x, -sunDir.y, sunDir.z);
                Sun.transform.rotation = Quaternion.LookRotation(-lightFrom);
                Sun.color = height > -0.05f ? sunColor : new Color(0.25f, 0.3f, 0.45f);
                Sun.intensity = height > -0.05f ? 1f : 0.25f;
            }
        }
    }
}
