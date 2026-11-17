using System.Collections.Generic;
using System.IO;
using Aruun.Core;
using Aruun.Core.Edits;
using Aruun.Core.Voxels;
using UnityEngine;

namespace Aruun.Game
{
    /// <summary>
    /// A flying camera that can dig and build, for looking at the world while the real player
    /// is being ported. Edits go through the same ops and journal the full game will use, so
    /// what you change is still there next time you start with the same seed.
    ///
    /// WASD move, Space and Ctrl up and down, Shift fast, mouse look, left click dig,
    /// right click place, 1-9 choose a block, F3 overlay, F5 save, Esc free the mouse.
    /// </summary>
    public sealed class DevPlayer : MonoBehaviour
    {
        public WorldView View;
        public int Seed;
        public float Speed = 8f;             // metres per second
        public int Reach = 60;               // voxels; generous on purpose for building
        public int DigRadius10 = 24;         // 2.4 voxels: a 0.6 m bite

        float _yaw, _pitch;
        bool _overlay = true;
        int _slot;
        string _journalPath;
        StreamWriter _journal;
        int _opsThisSession, _opsLoaded, _opsSkipped;
        float _fps = 60f;
        string _lastAction = "";

        public void Init(WorldView view, int seed)
        {
            View = view;
            Seed = seed;
            string dir = Path.Combine(Application.persistentDataPath, "worlds", "dev-" + seed);
            Directory.CreateDirectory(dir);
            _journalPath = Path.Combine(dir, "journal.txt");
            LoadJournal();
            _journal = new StreamWriter(_journalPath, true);
            if (new FileInfo(_journalPath).Length == 0) _journal.WriteLine(Journal.Header(seed));
            _journal.Flush();
            Vector3 e = transform.eulerAngles;
            _yaw = e.y; _pitch = e.x;
            Cursor.lockState = CursorLockMode.Locked;
        }

        void LoadJournal()
        {
            if (!File.Exists(_journalPath) || new FileInfo(_journalPath).Length == 0) return;
            try
            {
                int seed, gen;
                List<EditOp> ops;
                using (var reader = new StreamReader(_journalPath)) ops = Journal.Read(reader, out seed, out gen, out _opsSkipped);
                if (seed != Seed) { Debug.LogWarning("Journal at " + _journalPath + " is for seed " + seed + "; ignoring it."); return; }
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

        void Update()
        {
            _fps = Mathf.Lerp(_fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 1e-4f), 0.05f);
            if (DevInput.EscapeDown) Cursor.lockState = Cursor.lockState == CursorLockMode.Locked ? CursorLockMode.None : CursorLockMode.Locked;
            if (DevInput.OverlayDown) _overlay = !_overlay;
            if (DevInput.SaveDown) { _journal.Flush(); _lastAction = "Saved"; }
            int number = DevInput.NumberDown;
            if (number >= 0 && number < View.World.Blocks.Placeable.Count) _slot = number;

            bool locked = Cursor.lockState == CursorLockMode.Locked;
            if (locked)
            {
                Vector2 look = DevInput.Look;
                _yaw += look.x * 2f;
                _pitch = Mathf.Clamp(_pitch - look.y * 2f, -89f, 89f);
                transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            }
            Vector2 move = DevInput.Move;
            Vector3 dir = transform.forward * move.y + transform.right * move.x + Vector3.up * DevInput.Rise;
            transform.position += dir * (Speed * (DevInput.Fast ? 6f : 1f) * Time.unscaledDeltaTime);

            if (locked && (DevInput.DigDown || DevInput.PlaceDown)) Act(DevInput.DigDown);
        }

        void Act(bool dig)
        {
            Vector3 o = View.ViewerVoxel;
            Vector3 d = transform.forward;
            VoxelHit hit = VoxelRaycast.Cast(View.World, o.x, o.y, o.z, d.x, d.y, d.z, Reach);
            if (!hit.Hit) { _lastAction = "Nothing in reach"; return; }
            EditOp op;
            if (dig) op = EditOp.Sphere(hit.X, hit.Y, hit.Z, DigRadius10, 0);
            else
            {
                string key = View.World.Blocks.Placeable[_slot];
                op = EditOp.Set(hit.PrevX, hit.PrevY, hit.PrevZ, View.World.Blocks.Id(key));
            }
            EditResult result = View.World.Apply(op);
            if (result.Changed == 0) { _lastAction = dig ? "Too hard to break" : "No room"; return; }
            Journal.Append(_journal, op);
            _opsThisSession++;
            View.MarkDirty(result.Dirty);
            _lastAction = (dig ? "Dug " : "Placed ") + result.Changed + " voxels";
        }

        void OnDestroy()
        {
            if (_journal != null) { _journal.Flush(); _journal.Dispose(); }
        }

        void OnApplicationQuit()
        {
            if (_journal != null) _journal.Flush();
        }

        void OnGUI()
        {
            // Crosshair.
            GUI.Label(new Rect(Screen.width / 2 - 5, Screen.height / 2 - 11, 20, 22), "+");
            if (!_overlay || View == null || View.World == null) return;
            Vector3 m = transform.position;
            var blocks = View.World.Blocks;
            string text =
                "Aruun dev build   seed " + Seed + "   " + Mathf.RoundToInt(_fps) + " fps\n" +
                "Position " + m.x.ToString("0.0") + ", " + m.y.ToString("0.0") + ", " + m.z.ToString("0.0") + " m\n" +
                "Chunks shown " + View.ShownChunks + ", building " + View.PendingChunks + ", in memory " + View.World.LoadedChunks + "\n" +
                "Edits: " + _opsLoaded + " loaded" + (_opsSkipped > 0 ? " (" + _opsSkipped + " unreadable)" : "") + ", " + _opsThisSession + " this session\n" +
                "Block [" + (_slot + 1) + "] " + blocks.Get(blocks.Placeable[_slot]).Name + "   " + _lastAction + "\n" +
                "WASD fly, Space/Ctrl up/down, Shift fast, LMB dig, RMB place, 1-" + Mathf.Min(9, blocks.Placeable.Count) + " block, F3 hide, F5 save, Esc mouse";
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.Box(new Rect(8, 8, 560, 112), GUIContent.none);
            GUI.color = Color.white;
            GUI.Label(new Rect(16, 12, 548, 108), text);
        }
    }
}
