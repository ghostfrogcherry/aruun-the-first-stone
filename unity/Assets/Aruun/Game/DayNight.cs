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
        static readonly int LampColorId = Shader.PropertyToID("_AruunLampColor");
        static readonly int NightId = Shader.PropertyToID("_AruunNight");

        /// <summary>Lamp light (sRGB): warm amber, the colour of places people live (visual guide).</summary>
        public static readonly Color LampColor = new Color(1f, 0.64f, 0.30f);
        /// <summary>Lamp strength at night and in full day (daylight drowns it out anyway).</summary>
        public float LampNight = 4f, LampDay = 1.2f;
        /// <summary>Moonlight (sRGB): cool and blue, bright enough to find your way by.</summary>
        public Color MoonColor = new Color(0.46f, 0.53f, 0.70f);
        public Color NightAmbient = new Color(0.30f, 0.35f, 0.48f);
        /// <summary>0 by day, 1 at full night (for the stats overlay and dev tools).</summary>
        public float Night { get; private set; }

        // Key colours through the day (sRGB): night, dawn/dusk, day.
        static readonly Color NightTop = new Color(0.03f, 0.05f, 0.13f), NightHorizon = new Color(0.12f, 0.15f, 0.26f);
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
            // After sunset the moon takes over the key light: it fades in as the sun sinks, from
            // the opposite side of the sky, so there is no moment of total darkness.
            float night = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.04f, -0.25f, height));
            Night = 1f - day;
            bool moonUp = height <= -0.017f;                         // the sun's own light is gone by here
            Color moonColor = MoonColor * night;
            Vector3 moonDir = new Vector3(-sunDir.x, -sunDir.y, sunDir.z);
            if (moonDir.y < 0.25f) moonDir.y = 0.25f;                // keep the moon up while it fades in
            moonDir.Normalize();

            // Sky light: day blue, a warm tint at dawn and dusk, moonlit blue at night.
            Color ambient = Color.Lerp(NightAmbient, new Color(0.46f, 0.55f, 0.66f), day);
            ambient = Color.Lerp(ambient, new Color(0.55f, 0.45f, 0.45f), glow * 0.3f);

            Vector3 keyDir = moonUp ? moonDir : sunDir;
            Shader.SetGlobalVector(SunDirId, keyDir);
            Shader.SetGlobalColor(SunColorId, moonUp ? moonColor : sunColor);
            Shader.SetGlobalColor(AmbientId, ambient);
            Shader.SetGlobalColor(SkyTopId, top);
            Shader.SetGlobalColor(SkyHorizonId, horizon);
            Shader.SetGlobalColor(FogColorId, horizon);
            Color lamp = LampColor;
            lamp.a = Mathf.Lerp(LampNight, LampDay, day);
            Shader.SetGlobalColor(LampColorId, lamp);
            Shader.SetGlobalFloat(NightId, Night);
            Camera cam = Camera.main;
            if (cam != null) cam.backgroundColor = horizon;
            if (Sun != null)
            {
                Sun.transform.rotation = Quaternion.LookRotation(-keyDir);
                Sun.color = moonUp ? moonColor : sunColor;
                Sun.intensity = 1f;
                Sun.shadowStrength = moonUp ? 0.75f : 1f;
            }
        }
    }
}
