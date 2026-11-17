using System.Collections.Generic;
using System.IO;
using Aruun.Core;
using Aruun.Core.Edits;
using Aruun.Core.Physics;
using Aruun.Core.Play;
using Aruun.Core.Voxels;
using Aruun.Core.Worldgen;
using Aruun.Game.Art;
using UnityEngine;

namespace Aruun.Game
{
    /// <summary>
    /// The player while the real one is being built: walks at Aruun's 0.38 g or flies, digs and
    /// builds. Edits go through the same ops and journal the full game will use, so what you
    /// change is still there next time you start with the same seed. Dev keys live in DevTools.
    ///
    /// WASD move, mouse look, Space jump (or up while flying), Ctrl down, Shift run,
    /// 1-9 or the scroll wheel choose the held item. Hold LMB to dig with it (or swing at a
    /// creature in front of you), R heavy swing, hold Q to guard, Left Alt to dodge, RMB to
    /// place, plant, set down or eat what you hold, E to gather or use, Tab for the bag.
    /// Big dig (a dev key) digs anything with no tool, as before.
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
        bool _fly, _thirdPerson, _bigDig;
        int _slot, _place = -1;
        string _journalPath;
        StreamWriter _journal;
        int _opsThisSession, _opsLoaded, _opsSkipped;
        string _lastAction = "";
        float _lastActionTime;
        Mover _mover;
        Body _body;
        GameObject _avatar;
        MapScreen _mapScreen;
        VoxelRig _avatarRig;
        bool _wasGrounded = true;
        float _fallSpeed;
        float _swingTimer, _animTimer;
        bool _digging;

        PlaySession Play { get { return PlaySession.Instance; } }

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
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--third") >= 0) _thirdPerson = true;
            _mapScreen = gameObject.AddComponent<MapScreen>();
            _mapScreen.Init(this, map);
            Cursor.lockState = CursorLockMode.Locked;
        }

        GameObject MakeAvatar()
        {
            if (ModelLibrary.Has("human_castaway"))
            {
                var model = ModelLibrary.Instantiate("human_castaway");
                model.name = "Player body";
                _avatarRig = model.GetComponent<VoxelRig>();
                model.SetActive(false);
                return model;
            }
            // A plain stand-in body for third person, when the model is missing.
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

        public void Say(string text) { _lastAction = text; _lastActionTime = Time.unscaledTime; }

        public bool ThirdPerson { get { return _thirdPerson; } set { _thirdPerson = value; } }
        public bool BigDig { get { return _bigDig; } set { _bigDig = value; } }
        public string LastAction { get { return Time.unscaledTime - _lastActionTime < 6f ? _lastAction : ""; } }
        public int Slot { get { return _slot; } }
        public int EditsLoaded { get { return _opsLoaded; } }
        public int EditsSkipped { get { return _opsSkipped; } }
        public int EditsNew { get { return _opsThisSession; } }
        public MapScreen MapScreen { get { return _mapScreen; } }

        public void SetFly(bool fly)
        {
            _fly = fly;
            _body.VX = _body.VY = _body.VZ = 0;
            if (!fly) SettleOnGround();
            Say(_fly ? "Flying (no collisions)" : "Walking");
        }

        public void SaveEdits() { _journal.Flush(); if (Play != null) Play.Save(); Say("Saved"); }

        /// <summary>Writes an edit someone else made (the rules host) to this world's journal.</summary>
        public void RecordEdit(EditOp op)
        {
            Journal.Append(_journal, op);
            _opsThisSession++;
        }

        /// <summary>Teleports to the next (or previous) named place on the map.</summary>
        public void CyclePlace(int step)
        {
            if (Map == null || Map.Places.Count == 0) return;
            int n = Map.Places.Count;
            _place = ((_place + step) % n + n) % n;
            MapPlace p = Map.Places[_place];
            TeleportToVoxel(p.X + 24, p.Z - 24, p.Name);
        }

        /// <summary>Teleports to the ground where the camera is looking, up to 500 m away.</summary>
        public void TeleportToLookTarget()
        {
            Vector3 o = transform.position / V, d = transform.forward;
            VoxelHit hit = VoxelRaycast.Cast(View.World, o.x, o.y, o.z, d.x, d.y, d.z, 2000);
            if (!hit.Hit) { Say("Nothing there to stand on"); return; }
            TeleportToVoxel(hit.X, hit.Z, "where you looked");
        }

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            bool shift = DevInput.Holding(DevKey.Shift);
            PlayerRec me = Play != null ? Play.Me : null;
            bool dead = me != null && me.C.Dead;
            int number = DevInput.NumberDown;
            if (me != null)
            {
                // Talking to someone: numbers pick an answer.
                if (me.Talk != null)
                {
                    if (number >= 0) Play.Host.Choose(PlaySession.LocalId, number);
                }
                // Speaking Avarra: X opens the panel, numbers pick words, Enter or X speaks.
                else if (PlayInput.Down(PlayKey.Speak) && !PlayHud.BagOpen && !dead)
                {
                    if (PlayHud.Speaking) SpeakNow(me);
                    else PlayHud.OpenSpeak();
                }
                else if (PlayHud.Speaking)
                {
                    if (number >= 0) PlayHud.PickWord(number, Play.Host, me);
                    if (PlayInput.Down(PlayKey.Confirm)) SpeakNow(me);
                    if (PlayInput.Down(PlayKey.Back)) PlayHud.DropWord();
                }
                else
                {
                    if (number >= 0) me.C.Selected = number;
                    float scroll = PlayInput.Scroll;
                    if (scroll != 0 && !PlayHud.BagOpen) me.C.Selected = ((me.C.Selected - (int)scroll) % 9 + 9) % 9;
                    // The middle mouse button says the last phrase again.
                    if (PlayInput.MiddleDown && !dead && PlayHud.LastPhrase.Count > 0) SpeakNow(me, PlayHud.LastPhrase);
                }
                if (PlayInput.Down(PlayKey.Bag)) { PlayHud.CloseSpeak(); PlayHud.SetBag(!PlayHud.BagOpen); }
                if (PlayInput.Down(PlayKey.Journal)) PlayHud.JournalOpen = !PlayHud.JournalOpen;
            }
            else if (number >= 0 && number < View.World.Blocks.Placeable.Count) _slot = number;

            bool locked = Cursor.lockState == CursorLockMode.Locked && !PlayHud.BagOpen;
            if (locked)
            {
                Vector2 look = DevInput.Look;
                _yaw += look.x * 2f;
                _pitch = Mathf.Clamp(_pitch - look.y * 2f, -89f, 89f);
            }
            Vector2 move = dead || PlayHud.BagOpen ? Vector2.zero : DevInput.Move;
            Quaternion facing = Quaternion.Euler(0f, _yaw, 0f);
            if (me != null)
            {
                if (me.RespawnAt.HasValue)
                {
                    Vector3 at = PlaySession.ToUnity(me.RespawnAt.Value) / V;
                    _body = new Body { X = at.x, Y = at.y, Z = at.z };
                    SettleOnGround();
                    me.RespawnAt = null;
                }
                if (me.Shove.HasValue && !_fly)
                {
                    _body.VX += me.Shove.Value.X / V; _body.VY += me.Shove.Value.Y / V; _body.VZ += me.Shove.Value.Z / V;
                    me.Shove = null;
                }
            }
            if (_fly)
            {
                Quaternion look = Quaternion.Euler(_pitch, _yaw, 0f);
                Vector3 dir = look * Vector3.forward * move.y + look * Vector3.right * move.x + Vector3.up * DevInput.Rise;
                Vector3 d = dir * (FlySpeed * (shift ? 6f : 1f) * dt) / V;
                _body.X += d.x; _body.Y += d.y; _body.Z += d.z;
                _wasGrounded = true;
            }
            else
            {
                Vector3 wish = facing * new Vector3(move.x, 0f, move.y);
                if (wish.sqrMagnitude > 1f) wish.Normalize();
                bool tired = me != null && me.C.Stamina <= 0.5;
                float speed = (shift && !tired ? RunSpeed : WalkSpeed) / V;
                if (me != null && me.Guard) speed *= 0.5f;
                // Quick to start and stop on the ground, floaty but steerable in the air.
                float grip = _body.Grounded ? 12f : 2.5f;
                float k = 1f - Mathf.Exp(-grip * dt);
                _body.VX += (wish.x * speed - _body.VX) * k;
                _body.VZ += (wish.z * speed - _body.VZ) * k;
                if (_body.Grounded && !dead && !PlayHud.BagOpen && DevInput.Holding(DevKey.Jump)) _body.VY = _mover.JumpSpeed(JumpMetres);
                if (me != null && !dead && PlayInput.Down(PlayKey.Dodge) && Play.Host.Dodge(PlaySession.LocalId))
                {
                    // A quick hop the way you are moving, or backwards when standing still.
                    Vector3 away = wish.sqrMagnitude > 0.01f ? wish.normalized : facing * Vector3.back;
                    _body.VX = away.x * 9f / V; _body.VZ = away.z * 9f / V;
                    if (_body.Grounded) _body.VY = _mover.JumpSpeed(0.25f);
                }
                if (!_body.Grounded) _fallSpeed = Mathf.Max(_fallSpeed, (float)-_body.VY * V);
                _mover.Step(ref _body, dt);
                if (_body.Grounded && !_wasGrounded && me != null && _fallSpeed > 0) Play.Host.ReportFall(PlaySession.LocalId, _fallSpeed);
                if (_body.Grounded) _fallSpeed = 0;
                _wasGrounded = _body.Grounded;
                if (_body.Y < -2000) TeleportToVoxel((int)_body.X, (int)_body.Z, "Fell out of the world");
            }
            PlaceCamera();
            if (me != null)
            {
                Play.Host.SetPose(PlaySession.LocalId, PlaySession.ToVec(Feet), PlaySession.ToVec(transform.forward), dt);
                Play.Host.SetGuard(PlaySession.LocalId, locked && PlayInput.Holding(PlayKey.Guard));
            }
            AnimateAvatar(dt, move);

            if (!locked || _mapScreen.Open || dead) { StopDig(); return; }
            if (me == null || _bigDig)
            {
                if (DevInput.DigDown || DevInput.PlaceDown) Act(DevInput.DigDown);
                return;
            }
            Survive(me);
        }

        /// <summary>The survival controls: dig with what you hold, swing, place, eat, gather.</summary>
        void Survive(PlayerRec me)
        {
            Host host = Play.Host;
            const int id = PlaySession.LocalId;
            Vector3 eye = Feet + Vector3.up * EyeHeight;
            Vector3 d = transform.forward;
            Vector3 o = eye / V;
            VoxelHit hit = VoxelRaycast.Cast(View.World, o.x, o.y, o.z, d.x, d.y, d.z, (int)(Host.ReachM / V));
            PlayHud.LookingAt = hit.Hit ? View.World.Blocks.Get(View.World.Get(hit.X, hit.Y, hit.Z)).Name : "";

            if (PlayInput.Down(PlayKey.Heavy)) Swing(host.Attack(id, true));
            if (PlayInput.LeftDown && host.CreatureInReach(id, 3.0)) { Swing(host.Attack(id, false)); StopDig(); return; }
            if (PlayInput.LeftHeld && Time.unscaledTime > _swingTimer && hit.Hit)
            {
                _digging = true;
                DigTick t = host.Dig(id, hit.X, hit.Y, hit.Z, Time.deltaTime);
                PlayHud.DigFraction = t.Broken ? 0 : (float)t.Fraction;
                if (t.Refused && PlayInput.LeftDown && !string.IsNullOrEmpty(t.Message)) Play.Say(t.Message);
                if (t.Broken || t.Fraction > 0) _animTimer = Time.unscaledTime + 0.3f;
            }
            else if (PlayInput.LeftDown && !hit.Hit) Swing(host.Attack(id, false));
            else StopDig();

            if (PlayInput.RightDown)
                host.UseHeld(id, hit.Hit, hit.X, hit.Y, hit.Z, hit.PrevX, hit.PrevY, hit.PrevZ);
            if (PlayInput.Down(PlayKey.Interact) && !host.Interact(id, PlaySession.ToVec(eye), PlaySession.ToVec(d)))
                Play.Say("Nothing to gather there");
        }

        /// <summary>Says the phrase on the speak panel (or `phrase`) toward the crosshair.</summary>
        void SpeakNow(PlayerRec me, List<string> phrase = null)
        {
            if (Play == null || Play.Host == null) return;
            var words = new List<string>(phrase ?? PlayHud.Phrase);
            if (words.Count == 0) words.AddRange(PlayHud.LastPhrase);
            if (words.Count == 0) { Play.Say("Pick a word with the number keys first"); return; }
            Vector3 eye = Feet + Vector3.up * EyeHeight;
            bool ok = Play.Host.Speak(PlaySession.LocalId, words, PlaySession.ToVec(eye), PlaySession.ToVec(transform.forward));
            PlayHud.LastPhrase.Clear();
            PlayHud.LastPhrase.AddRange(words);
            if (ok) { PlayHud.CloseSpeak(); Swing(true, 0.2f); }
        }

        void StopDig()
        {
            PlayHud.DigFraction = 0;
            // Only when a dig was going: the host may be digging for this player by other means (--smoke).
            if (!_digging) return;
            _digging = false;
            if (Play != null && Play.Host != null) Play.Host.StopDigging(PlaySession.LocalId);
        }

        void Swing(bool happened, float hold = 0.35f)
        {
            if (!happened) return;
            _animTimer = Time.unscaledTime + 0.35f;
            _swingTimer = Mathf.Max(_swingTimer, Time.unscaledTime + hold);
        }

        void AnimateAvatar(float dt, Vector2 move)
        {
            if (_avatarRig == null || !_thirdPerson) return;
            string clip = "idle";
            float speed = new Vector2((float)_body.VX, (float)_body.VZ).magnitude * V;
            if (Time.unscaledTime < _animTimer) clip = "swing";
            else if (speed > WalkSpeed * 1.2f) clip = "run";
            else if (speed > 0.4f) clip = "walk";
            if (_avatarRig.HasClip(clip) && _avatarRig.CurrentClip != clip) _avatarRig.Play(clip);
        }

        void PlaceCamera()
        {
            Vector3 eye = Feet + Vector3.up * EyeHeight;
            Quaternion look = Quaternion.Euler(_pitch, _yaw, 0f);
            _avatar.SetActive(_thirdPerson);
            if (_thirdPerson)
            {
                _avatar.transform.SetPositionAndRotation(Feet + Vector3.up * (_avatarRig != null ? 0f : 0.875f), Quaternion.Euler(0f, _yaw, 0f));
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

        public void TakeScreenshot()
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

        void OnGUI()
        {
            if (!_thirdPerson && Cursor.lockState == CursorLockMode.Locked)
                GUI.Label(new Rect(Screen.width / 2 - 5, Screen.height / 2 - 11, 20, 22), "+");
        }
    }
}
