using System.Collections.Generic;
using System.IO;
using Aruun.Core;
using Aruun.Core.Edits;
using Aruun.Core.Physics;
using Aruun.Core.Voxels;
using Aruun.Core.Worldgen;
using UnityEngine;

namespace Aruun.Game
{
    /// <summary>
    /// The player while the real one is being built: walks at Aruun's 0.38 g or flies, digs and
    /// builds, and has every dev tool on a key that always works, with no menu to open.
    /// Edits go through the same ops and journal the full game will use, so what you change is
    /// still there next time you start with the same seed.
    ///
    /// WASD move, mouse look, Space jump (or up while flying), Ctrl down, Shift run,
    /// LMB dig, RMB place, 1-9 block, F fly, V third person, M map (click it to teleport),
    /// T next place (Shift+T back), H home, B big dig, = and - fly speed, F1 keys, F2 screenshot,
    /// F3 overlay, F5 save, Esc free the mouse.
    /// </summary>
    public sealed class DevPlayer : MonoBehaviour
    {
        public WorldView View;
        public WorldMap Map;
        public int Seed;
        public float FlySpeed = 8f;          // metres per second
        public float WalkSpeed = 4.5f, RunSpeed = 8f;
        public float JumpMetres = 1.2f;      // about 1.2 m at 0.38 g: a Mars hop
        public float EyeHeight = 1.6f;
        public int Reach = 60;               // voxels; generous on purpose for building
        public int DigRadius10 = 24;         // 2.4 voxels: a 0.6 m bite

        const float V = WorldConfig.VoxelSize;
        float _yaw, _pitch;
        bool _overlay = true, _help, _fly, _thirdPerson, _bigDig;
        int _slot, _place = -1;
        string _journalPath;
        StreamWriter _journal;
        int _opsThisSession, _opsLoaded, _opsSkipped;
        float _fps = 60f;
        string _lastAction = "";
        float _lastActionTime;
        Mover _mover;
        Body _body;
        GameObject _avatar;
        MapScreen _mapScreen;

        public bool Flying { get { return _fly; } }

        /// <summary>The player's feet in metres.</summary>
        public Vector3 Feet { get { return new Vector3((float)_body.X * V, (float)_body.Y * V, (float)_body.Z * V); } }

        public void Init(WorldView view, WorldMap map, int seed, bool fly)
        {
            View = view;
            Map = map;
            Seed = seed;
            _fly = fly;
            _mover = new Mover(view.World);
            string dir = Path.Combine(Application.persistentDataPath, "worlds", "dev-" + seed);
            Directory.CreateDirectory(dir);
            _journalPath = Path.Combine(dir, "journal.txt");
            LoadJournal();
            _journal = new StreamWriter(_journalPath, true);
            if (new FileInfo(_journalPath).Length == 0) _journal.WriteLine(Journal.Header(seed));
            _journal.Flush();
            Vector3 e = transform.eulerAngles;
            _yaw = e.y; _pitch = e.x > 180f ? e.x - 360f : e.x;
            // Feet below the starting camera.
            Vector3 p = transform.position;
            _body = new Body { X = p.x / V, Y = (p.y - EyeHeight) / V, Z = p.z / V };
            if (!_fly) SettleOnGround();
            _avatar = MakeAvatar();
            _mapScreen = gameObject.AddComponent<MapScreen>();
            _mapScreen.Init(this, map);
            Cursor.lockState = CursorLockMode.Locked;
        }

        static GameObject MakeAvatar()
        {
            // A plain stand-in body for third person, until the player model arrives.
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Player body";
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.localScale = new Vector3(0.6f, 0.875f, 0.6f);
            go.GetComponent<Renderer>().material.color = new Color(0.86f, 0.62f, 0.3f);
            go.SetActive(false);
            return go;
        }

        void LoadJournal()
        {
            if (!File.Exists(_journalPath) || new FileInfo(_journalPath).Length == 0) return;
            try
            {
                int seed, gen;
                List<EditOp> ops;
                using (var reader = new StreamReader(_journalPath)) ops = Journal.Read(reader, out seed, out gen, out _opsSkipped);
                if (seed != Seed || gen != WorldConfig.GenerationVersion)
                {
                    // Edits made on a different world would land in the wrong places: keep them aside.
                    string old = _journalPath + ".gen" + gen;
                    File.Delete(old);
                    File.Move(_journalPath, old);
                    Debug.LogWarning("Journal was for seed " + seed + " world version " + gen + "; moved it to " + old);
                    return;
                }
                foreach (EditOp op in ops) View.World.Apply(op);
                _opsLoaded = ops.Count;
            }
            catch (System.FormatException e)
            {
                // Keep the unreadable file for inspection and start a clean one.
                string aside = _journalPath + ".unreadable";
                File.Delete(aside);
                File.Move(_journalPath, aside);
                Debug.LogWarning("Could not read the journal (" + e.Message + "). Moved it to " + aside);
            }
        }

        void Say(string text) { _lastAction = text; _lastActionTime = Time.unscaledTime; }

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            _fps = Mathf.Lerp(_fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 1e-4f), 0.05f);
            bool shift = DevInput.Holding(DevKey.Shift);
            if (DevInput.EscapeDown && !_mapScreen.Open) Cursor.lockState = Cursor.lockState == CursorLockMode.Locked ? CursorLockMode.None : CursorLockMode.Locked;
            if (DevInput.OverlayDown) _overlay = !_overlay;
            if (DevInput.SaveDown) { _journal.Flush(); Say("Saved"); }
            if (DevInput.Down(DevKey.Help)) _help = !_help;
            if (DevInput.Down(DevKey.Fly)) { _fly = !_fly; _body.VX = _body.VY = _body.VZ = 0; Say(_fly ? "Flying (no collisions)" : "Walking"); }
            if (DevInput.Down(DevKey.ThirdPerson)) _thirdPerson = !_thirdPerson;
            if (DevInput.Down(DevKey.BigDig)) { _bigDig = !_bigDig; Say(_bigDig ? "Big dig: 2 m" : "Small dig: 0.6 m"); }
            if (DevInput.Down(DevKey.Faster)) { FlySpeed = Mathf.Min(FlySpeed * 2f, 256f); Say("Fly speed " + FlySpeed + " m/s"); }
            if (DevInput.Down(DevKey.Slower)) { FlySpeed = Mathf.Max(FlySpeed / 2f, 1f); Say("Fly speed " + FlySpeed + " m/s"); }
            if (DevInput.Down(DevKey.Home)) TeleportToVoxel(0, 0, "Home");
            if (DevInput.Down(DevKey.Place) && Map != null && Map.Places.Count > 0)
            {
                int n = Map.Places.Count;
                _place = ((_place + (shift ? -1 : 1)) % n + n) % n;
                MapPlace p = Map.Places[_place];
                TeleportToVoxel(p.X + 24, p.Z - 24, p.Name);
            }
            if (DevInput.Down(DevKey.Screenshot)) TakeScreenshot();
            if (DevInput.Down(DevKey.Map)) _mapScreen.Toggle();
            int number = DevInput.NumberDown;
            if (number >= 0 && number < View.World.Blocks.Placeable.Count) _slot = number;

            bool locked = Cursor.lockState == CursorLockMode.Locked;
            if (locked)
            {
                Vector2 look = DevInput.Look;
                _yaw += look.x * 2f;
                _pitch = Mathf.Clamp(_pitch - look.y * 2f, -89f, 89f);
            }
            Vector2 move = DevInput.Move;
            Quaternion facing = Quaternion.Euler(0f, _yaw, 0f);
            if (_fly)
            {
                Quaternion look = Quaternion.Euler(_pitch, _yaw, 0f);
                Vector3 dir = look * Vector3.forward * move.y + look * Vector3.right * move.x + Vector3.up * DevInput.Rise;
                Vector3 d = dir * (FlySpeed * (shift ? 6f : 1f) * dt) / V;
                _body.X += d.x; _body.Y += d.y; _body.Z += d.z;
            }
            else
            {
                Vector3 wish = facing * new Vector3(move.x, 0f, move.y);
                if (wish.sqrMagnitude > 1f) wish.Normalize();
                float speed = (shift ? RunSpeed : WalkSpeed) / V;
                // Quick to start and stop on the ground, floaty but steerable in the air.
                float grip = _body.Grounded ? 12f : 2.5f;
                float k = 1f - Mathf.Exp(-grip * dt);
                _body.VX += (wish.x * speed - _body.VX) * k;
                _body.VZ += (wish.z * speed - _body.VZ) * k;
                if (_body.Grounded && DevInput.Holding(DevKey.Jump)) _body.VY = _mover.JumpSpeed(JumpMetres);
                _mover.Step(ref _body, dt);
                if (_body.Y < -2000) TeleportToVoxel((int)_body.X, (int)_body.Z, "Fell out of the world");
            }
            PlaceCamera();

            if (locked && !_mapScreen.Open && (DevInput.DigDown || DevInput.PlaceDown)) Act(DevInput.DigDown);
        }

        void PlaceCamera()
        {
            Vector3 eye = Feet + Vector3.up * EyeHeight;
            Quaternion look = Quaternion.Euler(_pitch, _yaw, 0f);
            _avatar.SetActive(_thirdPerson);
            if (_thirdPerson)
            {
                _avatar.transform.SetPositionAndRotation(Feet + Vector3.up * 0.875f, Quaternion.Euler(0f, _yaw, 0f));
                // Pull back from the eye, stopping short of walls.
                Vector3 back = look * Vector3.back;
                float dist = 4f;
                Vector3 ev = eye / V, bv = back;
                VoxelHit hit = VoxelRaycast.Cast(View.World, ev.x, ev.y, ev.z, bv.x, bv.y, bv.z, (int)(dist / V) + 1);
                if (hit.Hit) dist = Mathf.Max(0.3f, Vector3.Distance(ev, new Vector3(hit.X + 0.5f, hit.Y + 0.5f, hit.Z + 0.5f)) * V - 0.4f);
                transform.SetPositionAndRotation(eye + back * dist, look);
            }
            else transform.SetPositionAndRotation(eye, look);
        }

        /// <summary>Moves the player to column (x, z) in voxels, standing on the ground there.</summary>
        public void TeleportToVoxel(int x, int z, string label)
        {
            int ground = View.World.Generator.Field.HeightAt(x, z);
            if (ground < HeightField.SeaLevel) ground = HeightField.SeaLevel;
            _body = new Body { X = x + 0.5, Y = ground + 1.0001, Z = z + 0.5 };
            SettleOnGround();
            if (_fly) _body.Y += 8 / V;   // look around from a little above
            PlaceCamera();
            Say("Teleported: " + label);
        }

        void SettleOnGround()
        {
            // Rise out of trees and anything else until the body fits.
            for (int i = 0; i < 400 && _mover.Overlaps(_body.X, _body.Y, _body.Z); i++) _body.Y += 1;
            _body.VX = _body.VY = _body.VZ = 0;
        }

        void TakeScreenshot()
        {
            string dir = Path.Combine(Application.persistentDataPath, "screenshots");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "aruun-" + System.DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".png");
            ScreenCapture.CaptureScreenshot(path);
            Say("Screenshot: " + path);
        }

        void Act(bool dig)
        {
            Vector3 o = View.ViewerVoxel;
            if (_thirdPerson) o = (Feet + Vector3.up * EyeHeight) / V;
            Vector3 d = transform.forward;
            VoxelHit hit = VoxelRaycast.Cast(View.World, o.x, o.y, o.z, d.x, d.y, d.z, Reach);
            if (!hit.Hit) { Say("Nothing in reach"); return; }
            EditOp op;
            if (dig) op = EditOp.Sphere(hit.X, hit.Y, hit.Z, _bigDig ? 80 : DigRadius10, 0);
            else
            {
                string key = View.World.Blocks.Placeable[_slot];
                op = EditOp.Set(hit.PrevX, hit.PrevY, hit.PrevZ, View.World.Blocks.Id(key));
            }
            EditResult result = View.World.Apply(op);
            if (result.Changed == 0) { Say(dig ? "Too hard to break" : "No room"); return; }
            Journal.Append(_journal, op);
            _opsThisSession++;
            View.MarkDirty(result.Dirty);
            Say((dig ? "Dug " : "Placed ") + result.Changed + " voxels");
        }

        void OnDestroy()
        {
            if (_journal != null) { _journal.Flush(); _journal.Dispose(); }
        }

        void OnApplicationQuit()
        {
            if (_journal != null) _journal.Flush();
        }

        /// <summary>Name of the map place nearest the player, and how far it is.</summary>
        string NearestPlace()
        {
            if (Map == null) return "";
            MapPlace best = null;
            double bestD = double.MaxValue;
            foreach (MapPlace p in Map.Places)
            {
                double dx = p.X - _body.X, dz = p.Z - _body.Z, d = dx * dx + dz * dz;
                if (d < bestD) { bestD = d; best = p; }
            }
            if (best == null) return "";
            double m = System.Math.Sqrt(bestD) * V;
            string biome = Map.BiomeAt((int)_body.X, (int)_body.Z).Name;
            return biome + ", " + (m < 30 ? "at " + best.Name : Mathf.RoundToInt((float)m) + " m from " + best.Name);
        }

        void OnGUI()
        {
            if (!_thirdPerson) GUI.Label(new Rect(Screen.width / 2 - 5, Screen.height / 2 - 11, 20, 22), "+");
            if (View == null || View.World == null) return;
            if (!_overlay) return;
            Vector3 m = Feet;
            var blocks = View.World.Blocks;
            string action = Time.unscaledTime - _lastActionTime < 6f ? _lastAction : "";
            string text =
                "Aruun dev build   seed " + Seed + "   " + Mathf.RoundToInt(_fps) + " fps   " + (_fly ? "flying " + FlySpeed + " m/s" : "walking") + (_bigDig ? "   big dig" : "") + "\n" +
                "Position " + m.x.ToString("0.0") + ", " + m.y.ToString("0.0") + ", " + m.z.ToString("0.0") + " m   " + NearestPlace() + "\n" +
                "Chunks shown " + View.ShownChunks + ", building " + View.PendingChunks + ", in memory " + View.World.LoadedChunks +
                "   edits " + _opsLoaded + " loaded" + (_opsSkipped > 0 ? " (" + _opsSkipped + " unreadable)" : "") + ", " + _opsThisSession + " new\n" +
                "Block [" + (_slot + 1) + "] " + blocks.Get(blocks.Placeable[_slot]).Name + "   " + action + "\n" +
                "F1 all keys   F fly   M map   T places   H home   F3 hide";
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.Box(new Rect(8, 8, 620, 96), GUIContent.none);
            GUI.color = Color.white;
            GUI.Label(new Rect(16, 12, 608, 92), text);
            if (_help)
            {
                const string keys =
                    "WASD move, mouse look, Shift run (fast while flying)\n" +
                    "Space jump (up while flying), Ctrl down while flying\n" +
                    "Left click dig, right click place, 1-9 choose block\n" +
                    "F  fly / walk (flying ignores walls)\n" +
                    "V  third person\n" +
                    "M  world map; click on it to teleport there\n" +
                    "T  teleport to the next place (Shift+T: previous)\n" +
                    "H  home: Teshari Confluence\n" +
                    "B  big dig (2 m) / small dig (0.6 m)\n" +
                    "=  and  -  fly speed\n" +
                    "F2 screenshot, F3 overlay, F5 save edits, Esc free the mouse";
                GUI.color = new Color(0f, 0f, 0f, 0.7f);
                GUI.Box(new Rect(8, 110, 460, 186), GUIContent.none);
                GUI.color = Color.white;
                GUI.Label(new Rect(16, 114, 448, 180), keys);
            }
        }
    }
}
