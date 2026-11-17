using System;
using System.Collections.Generic;
using System.Text;
using Aruun.Core;
using Aruun.Core.Worldgen;
using UnityEngine;
using UnityEngine.Profiling;

namespace Aruun.Game
{
    /// <summary>
    /// Every dev tool on a key that always works, with no menu to open. F1 lists them all.
    /// Other systems add their own with DevTools.Register (spawn creature, give items...), so
    /// they never need to edit this file.
    /// </summary>
    public sealed class DevTools : MonoBehaviour
    {
        sealed class Entry
        {
            public KeyCode Key;
            public bool Shift;
            public string Label;
            public Action Action;
        }

        static readonly List<Entry> Entries = new List<Entry>();

        /// <summary>True while god mode is on: systems that hurt, starve or tire the player should skip them.</summary>
        public static bool God { get; private set; }

        /// <summary>
        /// Adds a dev key. Shift = true means Shift+key. A later registration of the same key
        /// replaces the earlier one, so reloading a system never doubles its keys.
        /// </summary>
        public static void Register(KeyCode key, string label, Action action, bool shift = false)
        {
            Entries.RemoveAll(e => e.Key == key && e.Shift == shift);
            Entries.Add(new Entry { Key = key, Shift = shift, Label = label, Action = action });
        }

        DevPlayer _player;
        WorldView _view;
        DayNight _day;
        bool _overlay = true, _help, _stats, _wireframe;
        float _fps = 60f, _worstMs, _worstShown, _statsTimer;
        string _statsText = "";
        GUIStyle _mono;

        public void Init(DevPlayer player, WorldView view, DayNight day)
        {
            _player = player;
            _view = view;
            _day = day;

            Register(KeyCode.F1, "Show or hide this list", () => _help = !_help);
            Register(KeyCode.F2, "Screenshot (saved in the screenshots folder)", () => _player.TakeScreenshot());
            Register(KeyCode.F3, "Info overlay", () => _overlay = !_overlay);
            Register(KeyCode.F4, "Performance stats", () => _stats = !_stats);
            Register(KeyCode.F5, "Save edits now", () => _player.SaveEdits());
            Register(KeyCode.F6, "Reload all chunks", () => { _view.ReloadAll(); _player.Say("Reloading chunks"); });
            Register(KeyCode.F7, "God mode (no damage, hunger or fatigue)", () => { God = !God; _player.Say(God ? "God mode on" : "God mode off"); });
            Register(KeyCode.F8, "Time: back one hour", () => { _day.Shift(-1f); _player.Say("Time " + _day.Clock); });
            Register(KeyCode.F9, "Time: forward one hour", () => { _day.Shift(1f); _player.Say("Time " + _day.Clock); });
            Register(KeyCode.F8, "Time: jump to dawn", () => { _day.Hour = 6f; _day.Apply(); _player.Say("Time " + _day.Clock); }, true);
            Register(KeyCode.F9, "Time: jump to noon", () => { _day.Hour = 12f; _day.Apply(); _player.Say("Time " + _day.Clock); }, true);
            Register(KeyCode.F10, "Pause or resume the day", () => { _day.Paused = !_day.Paused; _player.Say("Time " + _day.Clock); });
            Register(KeyCode.F11, "Wireframe", () => _wireframe = !_wireframe);
            Register(KeyCode.F, "Fly (no collisions) or walk", () => _player.SetFly(!_player.Flying));
            Register(KeyCode.V, "Third person", () => _player.ThirdPerson = !_player.ThirdPerson);
            Register(KeyCode.M, "World map; click it to teleport", () => _player.MapScreen.Toggle());
            Register(KeyCode.T, "Teleport to the next named place", () => _player.CyclePlace(1));
            Register(KeyCode.T, "Teleport to the previous named place", () => _player.CyclePlace(-1), true);
            Register(KeyCode.G, "Teleport to where you are looking", () => _player.TeleportToLookTarget());
            Register(KeyCode.H, "Home: the Teshari Confluence", () => _player.TeleportToVoxel(0, 0, "Home"));
            Register(KeyCode.B, "Big dig (2 m) or small dig (0.6 m)", () => { _player.BigDig = !_player.BigDig; _player.Say(_player.BigDig ? "Big dig: 2 m" : "Small dig: 0.6 m"); });
            Register(KeyCode.Equals, "Fly faster", () => { _player.FlySpeed = Mathf.Min(_player.FlySpeed * 2f, 512f); _player.Say("Fly speed " + _player.FlySpeed + " m/s"); });
            Register(KeyCode.Minus, "Fly slower", () => { _player.FlySpeed = Mathf.Max(_player.FlySpeed / 2f, 1f); _player.Say("Fly speed " + _player.FlySpeed + " m/s"); });
            Register(KeyCode.Equals, "View distance +32 m", () => ChangeView(32), true);
            Register(KeyCode.Minus, "View distance -32 m", () => ChangeView(-32), true);
            Register(KeyCode.Escape, "Free or capture the mouse", () =>
            {
                if (_player.MapScreen.Open) return;
                Cursor.lockState = Cursor.lockState == CursorLockMode.Locked ? CursorLockMode.None : CursorLockMode.Locked;
            });
        }

        void ChangeView(int metres)
        {
            int chunks = Mathf.Clamp(_view.ViewChunks + metres / 8, 4, 64);
            _view.ViewChunks = chunks;
            float m = chunks * WorldConfig.ChunkSize * WorldConfig.VoxelSize;
            Vector4 fog = Shader.GetGlobalVector("_AruunFog");
            Shader.SetGlobalVector("_AruunFog", new Vector4(m * 0.35f, fog.y, fog.z, 0f));
            var far = FindFirstObjectByType<FarTerrain>();
            if (far != null) far.SetHole(m - 3f);
            QualitySettings.shadowDistance = Mathf.Min(m, 160f);
            _view.ReloadAll();
            _player.Say("View distance " + m + " m");
        }

        void Update()
        {
            float ms = Time.unscaledDeltaTime * 1000f;
            _fps = Mathf.Lerp(_fps, 1000f / Mathf.Max(ms, 0.01f), 0.05f);
            _worstMs = Mathf.Max(_worstMs, ms);
            _statsTimer += Time.unscaledDeltaTime;
            if (_statsTimer >= 1f) { _worstShown = _worstMs; _worstMs = 0f; _statsTimer = 0f; if (_stats) BuildStats(); }

            bool shift = DevInput.ShiftHeld;
            // Most specific first: a Shift+key entry wins over the plain key while Shift is held.
            foreach (Entry e in Entries.ToArray())
            {
                if (!DevInput.KeyDown(e.Key)) continue;
                if (e.Shift != shift && (e.Shift || HasShiftVersion(e.Key))) continue;
                try { e.Action(); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
        }

        static bool HasShiftVersion(KeyCode key)
        {
            foreach (Entry e in Entries) if (e.Key == key && e.Shift) return true;
            return false;
        }

        void BuildStats()
        {
            long verts, tris;
            _view.CountGeometry(out verts, out tris);
            var sb = new StringBuilder();
            sb.Append(Mathf.RoundToInt(_fps)).Append(" fps, worst frame ").Append(_worstShown.ToString("0.0")).Append(" ms this second\n");
            sb.Append("Chunks: ").Append(_view.ShownChunks).Append(" shown, ").Append(_view.PendingChunks).Append(" building on ")
              .Append(_view.WorkerThreads).Append(" threads, ").Append(_view.World.LoadedChunks).Append(" in memory\n");
            sb.Append("Geometry: ").Append((verts / 1000)).Append("k vertices, ").Append((tris / 1000)).Append("k triangles\n");
            sb.Append("Memory: ").Append(Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024)).Append(" MB used, GC heap ")
              .Append(GC.GetTotalMemory(false) / (1024 * 1024)).Append(" MB\n");
            sb.Append("View ").Append(_view.ViewChunks * WorldConfig.ChunkSize * WorldConfig.VoxelSize).Append(" m, time ").Append(_day.Clock)
              .Append(", ").Append(SystemInfo.graphicsDeviceName);
            _statsText = sb.ToString();
        }

        void OnPreRender() { if (_wireframe) GL.wireframe = true; }
        void OnPostRender() { GL.wireframe = false; }

        string NearestPlace()
        {
            WorldMap map = _player.Map;
            if (map == null) return "";
            Vector3 feet = _player.Feet / WorldConfig.VoxelSize;
            MapPlace best = null;
            double bestD = double.MaxValue;
            foreach (MapPlace p in map.Places)
            {
                double dx = p.X - feet.x, dz = p.Z - feet.z, d = dx * dx + dz * dz;
                if (d < bestD) { bestD = d; best = p; }
            }
            if (best == null) return "";
            double m = Math.Sqrt(bestD) * WorldConfig.VoxelSize;
            string biome = map.Contains((int)feet.x, (int)feet.z) ? map.BiomeAt((int)feet.x, (int)feet.z).Name : "Wild lands beyond the map";
            return biome + ", " + (m < 30 ? "at " + best.Name : Mathf.RoundToInt((float)m) + " m from " + best.Name);
        }

        void Box(float y, float w, float h, string text)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.Box(new Rect(8, y, w, h), GUIContent.none);
            GUI.color = Color.white;
            GUI.Label(new Rect(16, y + 4, w - 12, h - 6), text, _mono);
        }

        void OnGUI()
        {
            if (_view == null || _view.World == null) return;
            if (_mono == null) { _mono = new GUIStyle(GUI.skin.label) { fontSize = 13 }; _mono.normal.textColor = Color.white; }
            float y = 8;
            if (_overlay)
            {
                Vector3 m = _player.Feet;
                var blocks = _view.World.Blocks;
                string text =
                    "Aruun dev build   seed " + _player.Seed + "   " + Mathf.RoundToInt(_fps) + " fps   " + _day.Clock + "   " +
                    (_player.Flying ? "flying " + _player.FlySpeed + " m/s" : "walking") + (_player.BigDig ? "   big dig" : "") + (God ? "   GOD" : "") + "\n" +
                    "Position " + m.x.ToString("0.0") + ", " + m.y.ToString("0.0") + ", " + m.z.ToString("0.0") + " m   " + NearestPlace() + "\n" +
                    "Edits " + _player.EditsLoaded + " loaded" + (_player.EditsSkipped > 0 ? " (" + _player.EditsSkipped + " unreadable)" : "") + ", " + _player.EditsNew + " new" +
                    "   Block [" + (_player.Slot + 1) + "] " + blocks.Get(blocks.Placeable[_player.Slot]).Name + "\n" +
                    _player.LastAction + "\n" +
                    "F1 all dev keys";
                Box(y, 640, 92, text);
                y += 98;
            }
            if (_stats) { Box(y, 640, 92, _statsText.Length > 0 ? _statsText : "Measuring..."); y += 98; }
            if (_help)
            {
                var sb = new StringBuilder();
                sb.Append("Move: WASD, mouse, Space jump (up when flying), Ctrl down, Shift run.  Dig LMB, place RMB, 1-9 block\n");
                var list = new List<Entry>(Entries);
                list.Sort((a, b) => string.CompareOrdinal(KeyName(a), KeyName(b)));
                foreach (Entry e in list) sb.Append(KeyName(e).PadRight(12)).Append(e.Label).Append('\n');
                float h = 26 + 17 * list.Count;
                Box(y, 520, h, sb.ToString());
            }
        }

        static string KeyName(Entry e)
        {
            string k = e.Key == KeyCode.Equals ? "=" : e.Key == KeyCode.Minus ? "-" : e.Key.ToString();
            return (e.Shift ? "Shift+" : "") + k;
        }
    }
}
