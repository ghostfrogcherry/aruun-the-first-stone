using UnityEngine;

namespace Aruun.Game
{
    /// <summary>
    /// The first screen: continue the last world, start one from a seed (a number or any word),
    /// host it for friends, or join a friend's world. Drawn with Unity's immediate GUI so it
    /// needs no scene or prefab.
    /// </summary>
    public sealed class TitleScreen : MonoBehaviour
    {
        const string LastSeedKey = "aruun.lastSeed";
        LaunchOptions _options;
        string _seedText = "";
        Texture2D _art;
        GUIStyle _title, _sub, _button, _field, _small;
        bool _hasLast;
        int _lastSeed;
        string _name = "", _address = "", _error;

        public void Init(LaunchOptions options, string error = null)
        {
            _options = options;
            _error = error;
            _name = Coop.Name;
            _address = PlayerPrefs.GetString("aruun.lastJoin", options.JoinAddress ?? "");
            _art = MapScreen.LoadArt();
            _hasLast = LastWorld.TryRead(out _lastSeed);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Camera cam = Camera.main;
            if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.06f, 0.08f, 0.12f); }
        }

        void Styles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 72, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _title.normal.textColor = new Color(0.98f, 0.88f, 0.62f);
            _sub = new GUIStyle(_title) { fontSize = 22, fontStyle = FontStyle.Italic };
            _sub.normal.textColor = new Color(0.85f, 0.92f, 0.95f);
            _button = new GUIStyle(GUI.skin.button) { fontSize = 20 };
            _field = new GUIStyle(GUI.skin.textField) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
            _small.normal.textColor = new Color(0.8f, 0.85f, 0.9f);
        }

        void OnGUI()
        {
            Styles();
            // Laid out for a 1080-pixel-tall screen and scaled to the real one, so the menu is the
            // same size at 1440p or 4K. IMGUI maps the mouse through GUI.matrix, so clicks still hit.
            float scale = Mathf.Clamp(Screen.height / 1080f, 0.6f, 4f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float sw = Screen.width / scale, sh = Screen.height / scale;
            if (_art != null)
            {
                GUI.DrawTexture(new Rect(0, 0, sw, sh), _art, ScaleMode.ScaleAndCrop);
                GUI.color = new Color(0f, 0f, 0f, 0.55f);
                GUI.DrawTexture(new Rect(0, 0, sw, sh), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
            float cx = sw / 2f, y = Mathf.Max(8f, Mathf.Min(sh * 0.15f, sh - 720f));
            GUI.Label(new Rect(0, y, sw, 90), "ARUUN", _title);
            GUI.Label(new Rect(0, y + 84, sw, 34), "The First Stone", _sub);
            y += 150;
            const float w = 340;
            GUI.Label(new Rect(cx - w / 2, y, w, 22), "Your name (shown to friends in co-op)", _small);
            y += 22;
            _name = GUI.TextField(new Rect(cx - w / 2, y, w, 36), _name, 20, _field);
            y += 48;
            Coop.Hosting = GUI.Toggle(new Rect(cx - w / 2, y, w, 26), Coop.Hosting,
                "  Open to friends (co-op host, port " + Coop.Port + ")");
            y += 34;
            if (_hasLast)
            {
                if (GUI.Button(new Rect(cx - w / 2, y, w, 48), "Continue  (seed " + _lastSeed + ")", _button)) Begin(_lastSeed);
                y += 60;
            }
            GUI.Label(new Rect(cx - w / 2, y, w, 22), "Seed: a number or any word. Empty picks one.", _small);
            y += 24;
            GUI.SetNextControlName("seed");
            _seedText = GUI.TextField(new Rect(cx - w / 2, y, w, 40), _seedText, 40, _field);
            y += 50;
            Event e = Event.current;
            bool enter = e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                         && GUI.GetNameOfFocusedControl() != "join";
            if (GUI.Button(new Rect(cx - w / 2, y, w, 48), "New world", _button) || enter) Begin(SeedFromText());
            y += 64;
            GUI.Label(new Rect(cx - w / 2, y, w, 22), "Join a friend: their address (and :port if not " + Coop.Port + ")", _small);
            y += 24;
            GUI.SetNextControlName("join");
            _address = GUI.TextField(new Rect(cx - w / 2, y, w * 0.68f, 40), _address, 60, _field);
            if (GUI.Button(new Rect(cx - w / 2 + w * 0.7f, y, w * 0.3f, 40), "Join", _button)) Join();
            y += 50;
            if (!string.IsNullOrEmpty(_error))
            {
                var err = new GUIStyle(_small) { wordWrap = true };
                err.normal.textColor = new Color(1f, 0.6f, 0.5f);
                GUI.Label(new Rect(cx - w / 2 - 60, y, w + 120, 40), _error, err);
                y += 44;
            }
            if (GUI.Button(new Rect(cx - w / 2, y, w, 40), "Quit", _button)) Application.Quit();
            GUI.Label(new Rect(0, sh - 34, sw, 24),
                "In the world: F1 shows every dev key. They always work, no menu needed.", _small);
        }

        int SeedFromText()
        {
            string t = _seedText.Trim();
            if (t.Length == 0) return System.Environment.TickCount & 0x7fffffff;
            int v;
            if (int.TryParse(t, out v)) return v;
            return LaunchOptions.StableHash(t);
        }

        void Join()
        {
            Coop.SaveName(_name);
            PlayerPrefs.SetString("aruun.lastJoin", _address.Trim());
            PlayerPrefs.Save();
            _error = Coop.Connect(_address, false);
            if (_error != null) return;
            Coop.Hosting = false;
            _options.Seed = Coop.Welcome.Seed;
            Destroy(gameObject);
            Bootstrap.StartWorld(_options);
        }

        void Begin(int seed)
        {
            Coop.SaveName(_name);
            PlayerPrefs.SetInt(LastSeedKey, seed);
            PlayerPrefs.Save();
            LastWorld.Write(seed);
            _options.Seed = seed;
            Destroy(gameObject);
            Bootstrap.StartWorld(_options);
        }
    }
}
