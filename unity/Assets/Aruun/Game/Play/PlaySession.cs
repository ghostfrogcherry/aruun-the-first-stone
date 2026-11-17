using System;
using System.Collections.Generic;
using System.IO;
using Aruun.Core.Combat;
using Aruun.Core.Creatures;
using Aruun.Core.Edits;
using Aruun.Core.Farming;
using Aruun.Core.Items;
using Aruun.Core.Play;
using Aruun.Core.Survival;
using UnityEngine;

namespace Aruun.Game
{
    /// <summary>
    /// Runs the game's rules (Core.Play.Host) inside Unity: loads the content, ticks the host,
    /// hands its world edits to the view and the journal, keeps the character saved, and draws
    /// the creatures, plants and structures it holds. The local player is player 1.
    ///
    /// Launch flags: --survival (survival-only mode, the hardcore ruleset), --ruleset=id,
    /// --new-character (start a fresh castaway), --no-creatures.
    /// </summary>
    public sealed class PlaySession : MonoBehaviour
    {
        public const int LocalId = 1;
        public static PlaySession Instance { get; private set; }

        public Host Host { get; private set; }
        public PlayerRec Me { get { PlayerRec p; return Host != null && Host.Players.TryGetValue(LocalId, out p) ? p : null; } }
        public WorldView View { get; private set; }
        public bool SurvivalOnly { get; private set; }
        /// <summary>Called with every edit the host made, so the player's journal keeps it.</summary>
        public Action<EditOp> Journal;
        /// <summary>Recent lines of feedback for the HUD: text and the time it arrived.</summary>
        public readonly List<KeyValuePair<string, float>> Log = new List<KeyValuePair<string, float>>();
        /// <summary>Things that just happened to the local player, for the camera and HUD (hurt, blocked, swing).</summary>
        public readonly List<HostEvent> MyEvents = new List<HostEvent>();

        string _charPath;
        float _saveTimer = 30f;
        readonly Dictionary<int, CreatureView> _creatures = new Dictionary<int, CreatureView>();
        readonly Dictionary<int, GameObject> _plants = new Dictionary<int, GameObject>();
        readonly Dictionary<int, string> _plantModel = new Dictionary<int, string>();
        readonly Dictionary<int, GameObject> _structures = new Dictionary<int, GameObject>();
        Transform _entityRoot;

        sealed class CreatureView
        {
            public GameObject Go;
            public VoxelRig Rig;
            public float Punch;
            public float DeadFor;
        }

        public void Init(WorldView view, int seed, Vector3 spawnFeet)
        {
            Instance = this;
            View = view;
            string[] args = Environment.GetCommandLineArgs();
            SurvivalOnly = Array.IndexOf(args, "--survival") >= 0;
            string rulesetId = SurvivalOnly ? "hardcore" : "light";
            foreach (string a in args) if (a.StartsWith("--ruleset=", StringComparison.Ordinal)) rulesetId = a.Substring(10);

            var blocks = view.World.Blocks;
            var items = ItemRegistry.FromJson(GameContent.ReadText("items", "items.json"), blocks);
            Ruleset rules;
            try { rules = Ruleset.Load(GameContent.ReadText("survival", "rulesets.json"), rulesetId); }
            catch (Exception e)
            {
                Debug.LogError("Ruleset '" + rulesetId + "' failed (" + e.Message + "), using light");
                rules = Ruleset.Load(GameContent.ReadText("survival", "rulesets.json"), "light");
            }
            var plants = Plants.FromJson(GameContent.ReadText("farming", "plants.json"));
            var creatures = CreatureDef.FromJson(GameContent.ReadText("creatures", "creatures.json"));
            Host = new Host(view.World, items, rules, plants, creatures, seed);
            Host.SpawnPoint = ToVec(spawnFeet);
            Host.SpawnCreatures = Array.IndexOf(args, "--no-creatures") < 0 && DevInput.Enabled;

            string dir = Path.Combine(Application.persistentDataPath, "worlds", (SurvivalOnly ? "survival-" : "dev-") + seed);
            Directory.CreateDirectory(dir);
            _charPath = Path.Combine(dir, "character.txt");
            var c = new CharacterState(items, rules);
            bool fresh = Array.IndexOf(args, "--new-character") >= 0 || !File.Exists(_charPath);
            if (!fresh)
            {
                try { c.FromText(File.ReadAllText(_charPath)); }
                catch (Exception e) { Debug.LogWarning("Could not read the character (" + e.Message + "), starting fresh"); fresh = true; }
            }
            Host.Join(LocalId, c, ToVec(spawnFeet), fresh);
            Say(fresh ? "You wake on Aruun with little more than the clothes you came in. (Tab: bag and crafting)" : "Welcome back.");

            _entityRoot = new GameObject("Entities").transform;
        }

        public static Vec3 ToVec(Vector3 v) { return new Vec3(v.x, v.y, v.z); }
        public static Vector3 ToUnity(Vec3 v) { return new Vector3((float)v.X, (float)v.Y, (float)v.Z); }

        public void Say(string text)
        {
            Log.Add(new KeyValuePair<string, float>(text, Time.unscaledTime));
            if (Log.Count > 8) Log.RemoveAt(0);
        }

        public void Save()
        {
            PlayerRec me = Me;
            if (me == null) return;
            try { File.WriteAllText(_charPath, me.C.ToText()); }
            catch (Exception e) { Debug.LogWarning("Could not save the character: " + e.Message); }
        }

        void Update()
        {
            if (Host == null) return;
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            Host.Tick(dt);

            // World edits: redraw the chunks and journal them.
            foreach (HostEdit e in Host.Edits)
            {
                View.MarkDirty(e.Result.Dirty);
                if (Journal != null) Journal(e.Op);
            }
            Host.Edits.Clear();
            foreach (var m in Host.Messages) if (m.Key == LocalId) Say(m.Value);
            Host.Messages.Clear();
            MyEvents.Clear();
            foreach (HostEvent ev in Host.Events)
            {
                if (ev.Player == LocalId) MyEvents.Add(ev);
                CreatureView cv;
                if (ev.Creature != 0 && _creatures.TryGetValue(ev.Creature, out cv) && ev.Kind == "hit") cv.Punch = 0.18f;
            }
            Host.Events.Clear();

            SyncCreatures(dt);
            SyncPlants();
            SyncStructures();

            _saveTimer -= Time.unscaledDeltaTime;
            if (_saveTimer <= 0) { _saveTimer = 30f; Save(); }
        }

        void SyncCreatures(float dt)
        {
            var alive = new HashSet<int>();
            foreach (Creature c in Host.Creatures)
            {
                alive.Add(c.Id);
                CreatureView v;
                if (!_creatures.TryGetValue(c.Id, out v))
                {
                    v = new CreatureView();
                    if (ModelLibrary.Has(c.Def.Key))
                    {
                        v.Go = ModelLibrary.Instantiate(c.Def.Key);
                        v.Go.name = c.Def.Name;
                        v.Rig = v.Go.GetComponent<VoxelRig>();
                    }
                    else
                    {
                        v.Go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                        Destroy(v.Go.GetComponent<Collider>());
                        v.Go.name = c.Def.Name;
                    }
                    v.Go.transform.SetParent(_entityRoot, false);
                    _creatures[c.Id] = v;
                }
                Transform t = v.Go.transform;
                Vector3 target = ToUnity(c.Pos);
                // Smooth between the 20 Hz rule steps.
                t.position = Vector3.Distance(t.position, target) > 3f ? target : Vector3.Lerp(t.position, target, Mathf.Clamp01(dt * 18f));
                Quaternion rot = Quaternion.Euler(0f, (float)(c.Yaw * Mathf.Rad2Deg), 0f);
                if (c.State == "dead")
                {
                    v.DeadFor += dt;
                    rot *= Quaternion.Euler(0f, 0f, Mathf.Min(v.DeadFor * 300f, 90f));
                    t.position += Vector3.down * Mathf.Max(0, v.DeadFor - 1.2f) * 0.6f;
                }
                t.rotation = Quaternion.Slerp(t.rotation, rot, Mathf.Clamp01(dt * 14f));
                v.Punch = Mathf.Max(0, v.Punch - dt);
                t.localScale = Vector3.one * (1f + v.Punch * 0.8f);
                if (v.Rig != null)
                {
                    string clip = ClipFor(c, v.Rig);
                    if (clip != v.Rig.CurrentClip && v.Rig.HasClip(clip)) v.Rig.Play(clip);
                }
            }
            var gone = new List<int>();
            foreach (var kv in _creatures) if (!alive.Contains(kv.Key)) gone.Add(kv.Key);
            foreach (int id in gone) { Destroy(_creatures[id].Go); _creatures.Remove(id); }
        }

        static string ClipFor(Creature c, VoxelRig rig)
        {
            string want;
            if (c.AnimForce != Anim.Auto) want = c.AnimForce.ToString().ToLowerInvariant();
            else
            {
                double speed = c.Vel.FlatLength;
                if (c.State == "stagger") want = "stagger";
                else if (c.State == "windup") want = "windup";
                else if (speed > c.Def.WalkMps * 1.6) want = "run";
                else if (speed > 0.4) want = "walk";
                else want = "idle";
            }
            if (rig.HasClip(want)) return want;
            if ((want == "run" || want == "charge" || want == "hop") && rig.HasClip("walk")) return "walk";
            return "idle";
        }

        void SyncPlants()
        {
            var alive = new HashSet<int>();
            foreach (PlantRec r in Host.PlantsInWorld)
            {
                alive.Add(r.Id);
                string model = PlantModel(r);
                string had;
                GameObject go;
                if (_plants.TryGetValue(r.Id, out go) && _plantModel.TryGetValue(r.Id, out had) && had == model) continue;
                if (go != null) Destroy(go);
                go = ModelLibrary.Has(model) ? ModelLibrary.Instantiate(model, _entityRoot) : null;
                if (go == null)
                {
                    go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Destroy(go.GetComponent<Collider>());
                    go.transform.SetParent(_entityRoot, false);
                    go.transform.localScale = Vector3.one * 0.3f;
                }
                go.name = model;
                go.transform.position = ToUnity(r.Pos);
                // Each wild plant turned its own way, so a field does not look stamped.
                go.transform.rotation = Quaternion.Euler(0f, (r.X * 73 + r.Z * 151) % 360, 0f);
                _plants[r.Id] = go;
                _plantModel[r.Id] = model;
            }
            var gone = new List<int>();
            foreach (var kv in _plants) if (!alive.Contains(kv.Key)) gone.Add(kv.Key);
            foreach (int id in gone) { Destroy(_plants[id]); _plants.Remove(id); _plantModel.Remove(id); }
        }

        string PlantModel(PlantRec r)
        {
            PlantDef def = Host.Plants.Get(r.Plant);
            if (def != null && def.Node) return r.Plant;
            int stage = Host.StageOf(r) + 1;
            string id = r.Plant + "_s" + Mathf.Clamp(stage, 1, 4);
            return ModelLibrary.Has(id) ? id : r.Plant + "_s4";
        }

        void SyncStructures()
        {
            foreach (StructureRec s in Host.Structures)
            {
                if (_structures.ContainsKey(s.Id)) continue;
                string model = s.Key == "tether" ? "tether_stone" : s.Key;
                GameObject go = ModelLibrary.Has(model) ? ModelLibrary.Instantiate(model, _entityRoot) : GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = s.Key;
                go.transform.position = ToUnity(s.Pos);
                StructureDef def = Host.Items.Structure(s.Key);
                string light = def != null ? Core.Content.MiniJson.Str(def.Raw, "light") : "";
                Color col;
                if (light.Length > 0 && ColorUtility.TryParseHtmlString(light, out col))
                {
                    var l = new GameObject("light").AddComponent<Light>();
                    l.type = LightType.Point;
                    l.color = col;
                    l.range = 8f;
                    l.intensity = 1.6f;
                    l.transform.SetParent(go.transform, false);
                    l.transform.localPosition = Vector3.up * 0.8f;
                }
                _structures[s.Id] = go;
            }
        }

        void OnApplicationQuit() { Save(); }
        void OnDestroy() { Save(); if (Instance == this) Instance = null; }
    }
}
