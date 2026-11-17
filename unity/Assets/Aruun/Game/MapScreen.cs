using System.IO;
using Aruun.Core.Worldgen;
using UnityEngine;

namespace Aruun.Game
{
    /// <summary>
    /// The painted world map on screen (M), with the player and every named place marked.
    /// Click anywhere on it to teleport there. The map art and the land come from the same
    /// content/world files, so where you click is where you land.
    /// </summary>
    public sealed class MapScreen : MonoBehaviour
    {
        DevPlayer _player;
        WorldMap _map;
        Texture2D _art;
        bool _open;
        CursorLockMode _lockBefore;
        GUIStyle _label;

        public bool Open { get { return _open; } }

        public void Init(DevPlayer player, WorldMap map)
        {
            _player = player;
            _map = map;
            _art = LoadArt();
        }

        public static Texture2D LoadArt()
        {
            string path = Path.Combine(GameContent.Root, "world", "aruun_map_art.png");
            if (!File.Exists(path)) return null;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(path));
            tex.wrapMode = TextureWrapMode.Clamp;
            return tex;
        }

        public void Toggle()
        {
            if (_map == null) return;
            _open = !_open;
            if (_open) { _lockBefore = Cursor.lockState; Cursor.lockState = CursorLockMode.None; }
            else Cursor.lockState = _lockBefore == CursorLockMode.None ? CursorLockMode.Locked : _lockBefore;
        }

        /// <summary>Where the map is drawn, fitted to the screen.</summary>
        Rect Frame()
        {
            float w = _art != null ? _art.width : _map.Width, h = _art != null ? _art.height : _map.Height;
            float s = Mathf.Min(Screen.width * 0.94f / w, Screen.height * 0.9f / h);
            return new Rect((Screen.width - w * s) / 2f, (Screen.height - h * s) / 2f, w * s, h * s);
        }

        /// <summary>Screen point on the map for voxel column (x, z).</summary>
        Vector2 ToScreen(Rect f, double x, double z)
        {
            float cx = (float)(_map.SpawnPx + 0.5 + x / _map.Cell), cy = (float)(_map.SpawnPy + 0.5 - z / _map.Cell);
            return new Vector2(f.x + cx / _map.Width * f.width, f.y + cy / _map.Height * f.height);
        }

        void OnGUI()
        {
            if (!_open || _map == null) return;
            if (_label == null)
            {
                _label = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
                _label.normal.textColor = Color.white;
            }
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            Rect f = Frame();
            if (_art != null) GUI.DrawTexture(f, _art, ScaleMode.StretchToFill);

            foreach (MapPlace p in _map.Places)
            {
                Vector2 s = ToScreen(f, p.X, p.Z);
                GUI.color = new Color(1f, 0.85f, 0.3f);
                GUI.DrawTexture(new Rect(s.x - 3, s.y - 3, 6, 6), Texture2D.whiteTexture);
            }
            Vector3 feet = _player.Feet / Aruun.Core.WorldConfig.VoxelSize;
            Vector2 me = ToScreen(f, feet.x, feet.z);
            GUI.color = new Color(1f, 0.15f, 0.1f);
            GUI.DrawTexture(new Rect(me.x - 5, me.y - 5, 10, 10), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Name of the place under the mouse, if near one.
            Vector2 mouse = Event.current.mousePosition;
            foreach (MapPlace p in _map.Places)
            {
                Vector2 s = ToScreen(f, p.X, p.Z);
                if ((s - mouse).sqrMagnitude < 100f) GUI.Label(new Rect(s.x + 8, s.y - 10, 400, 22), p.Name, _label);
            }
            GUI.Label(new Rect(f.x + 8, f.yMax - 26, 600, 22), "Click to teleport.  M or Esc closes.  Red is you, gold are places.", _label);

            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && f.Contains(e.mousePosition))
            {
                float cx = (e.mousePosition.x - f.x) / f.width * _map.Width, cy = (e.mousePosition.y - f.y) / f.height * _map.Height;
                int x = Mathf.RoundToInt((cx - _map.SpawnPx - 0.5f) * _map.Cell);
                int z = Mathf.RoundToInt((_map.SpawnPy + 0.5f - cy) * _map.Cell);
                _player.TeleportToVoxel(x, z, _map.BiomeAt(x, z).Name);
                e.Use();
                Toggle();
            }
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { e.Use(); Toggle(); }
        }
    }
}
