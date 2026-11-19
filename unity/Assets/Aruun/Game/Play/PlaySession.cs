using System;
using System.Collections.Generic;
using System.IO;
using Aruun.Core.Combat;
using Aruun.Core.Creatures;
using Aruun.Core.Edits;
using Aruun.Core.Farming;
using Aruun.Core.Inscription;
using Aruun.Core.Items;
using Aruun.Core.Language;
using Aruun.Core.Net;
using Aruun.Core.People;
using Aruun.Core.Play;
using Aruun.Core.Skills;
using Aruun.Core.Ruins;
using Aruun.Core.Survival;
using Aruun.Game.Art;
using UnityEngine;

namespace Aruun.Game
{
    /// <summary>
    /// Runs the game's rules (Core.Play.Host) inside Unity: loads the content, ticks the host,
    /// hands its world edits to the view and the journal, keeps the character saved, and draws
    /// the creatures, plants and structures it holds. The local player is player 1, or the id
    /// the host gave when joining a friend's world (see Coop): then the Host here is a mirror
    /// of the friend's, and hosting streams this one to friends.
    ///
    /// Launch flags: --survival (survival-only mode, the hardcore ruleset), --ruleset=id,
    /// --new-character (start a fresh castaway), --no-creatures.
    /// </summary>
    public sealed class PlaySession : MonoBehaviour
    {
        /// <summary>This machine's player: 1, or the id a friend's host gave us.</summary>
        public static int LocalId { get; private set; } = 1;
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

        string _charPath, _worldPath;
        /// <summary>A --smoke run: a fresh character, and nothing it does is saved.</summary>
        bool _smoke;
        float _saveTimer = 30f;
        readonly Dictionary<int, CreatureView> _creatures = new Dictionary<int, CreatureView>();
        readonly Dictionary<int, GameObject> _plants = new Dictionary<int, GameObject>();
        readonly Dictionary<int, string> _plantModel = new Dictionary<int, string>();
        readonly Dictionary<int, GameObject> _structures = new Dictionary<int, GameObject>();
        Transform _entityRoot;
        CoopServer _server;
        CoopClient _client;
        bool _lostSaid;
        readonly Dictionary<int, CreatureView> _others = new Dictionary<int, CreatureView>();

        /// <summary>Hosting: how many friends are in. Joining: -1.</summary>
        public int Friends { get { return _server != null ? _server.GuestCount : -1; } }
        public bool Hosting { get { return _server != null; } }
        public bool Joined { get { return _client != null; } }
        public int HostPort { get { return _server != null ? _server.Port : 0; } }

        /// <summary>The name over another player's head.</summary>
        public string NameOf(int id)
        {
            string n;
            if (_client != null && _client.Names.TryGetValue(id, out n)) return n;
            if (_server != null) return _server.NameOf(id);
            return "";
        }

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
            _smoke = Array.IndexOf(args, "--smoke") >= 0;
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
            try { Host.Lexicon = Lexicon.FromJson(GameContent.ReadText("language", "lexicon.json")); }
            catch (Exception e) { Debug.LogError("Could not load the Avarra words: " + e.Message); }
            try
            {
                Host.Inscriptions = InscriptionRules.FromJson(GameContent.ReadText("language", "inscriptions.json"));
                foreach (string problem in Host.Inscriptions.Validate(Host.Lexicon, items)) Debug.LogWarning("inscriptions.json: " + problem);
            }
            catch (Exception e) { Debug.LogError("Could not load the inscription rules: " + e.Message); }
            try
            {
                Host.Tree = SkillTree.FromJson(GameContent.ReadText("skills", "graviturge.json"));
                foreach (string problem in Host.Tree.Validate()) Debug.LogWarning("graviturge.json: " + problem);
            }
            catch (Exception e) { Debug.LogError("Could not load the skill tree: " + e.Message); }
            Host.SpawnCreatures = Array.IndexOf(args, "--no-creatures") < 0 && DevInput.Enabled;

            string dir = Path.Combine(Application.persistentDataPath, "worlds", (SurvivalOnly ? "survival-" : "dev-") + seed);
            Directory.CreateDirectory(dir);
            _client = Coop.Joining ? Coop.Client : null;
            LocalId = _client != null ? Coop.Welcome.Id : 1;
            // In a friend's world we bring our travelling character; the world is theirs.
            _charPath = _client != null ? Coop.CharacterPath : Path.Combine(dir, "character.txt");
            _worldPath = Path.Combine(dir, "world.txt");
            if (_client != null)
            {
                Host.SpawnCreatures = false;
                foreach (EditOp op in Coop.Welcome.Edits) Host.ApplyRemote(op);
                spawnFeet = ToUnity(Coop.Welcome.Spawn);
            }
            else if (!_smoke && File.Exists(_worldPath))
                foreach (string line in File.ReadAllLines(_worldPath))
                    if (line.StartsWith("flag ", StringComparison.Ordinal)) Host.WorldFlags.Add(line.Substring(5).Trim());
            var c = new CharacterState(items, rules);
            bool fresh = _smoke || Array.IndexOf(args, "--new-character") >= 0 || !File.Exists(_charPath);
            if (!fresh)
            {
                try { c.FromText(File.ReadAllText(_charPath)); }
                catch (Exception e)
                {
                    // The last good save is kept beside it (SafeFile): try that before starting over.
                    string bak = _charPath + ".bak";
                    try
                    {
                        c = new CharacterState(items, rules);
                        c.FromText(File.ReadAllText(bak));
                        Debug.LogWarning("Could not read the character (" + e.Message + "), loaded the backup");
                    }
                    catch (Exception)
                    {
                        Debug.LogWarning("Could not read the character (" + e.Message + "), starting fresh");
                        c = new CharacterState(items, rules);
                        fresh = true;
                    }
                }
            }
            PlayerRec joined = Host.Join(LocalId, c, ToVec(spawnFeet), fresh && _client == null);
            if (_client != null) joined.RespawnAt = ToVec(spawnFeet);
            if (_client == null) Say(fresh ? "You wake on Aruun with little more than the clothes you came in. Something old hums to the south. (Tab: bag and crafting)" : "Welcome back.");

            try
            {
                Host.LoadPeople(PersonDef.FromJson(GameContent.ReadText("people", "people.json")),
                    QuestDef.FromJson(GameContent.ReadText("quests", "quests.json")));
            }
            catch (Exception e) { Debug.LogError("Could not load the people and quests: " + e.Message); }

            _entityRoot = new GameObject("Entities").transform;
            gameObject.AddComponent<InscriptionView>();
            RegisterDevKeys();
            // Co-op keeps running when the window is in the background: a host that alt-tabs
            // would otherwise freeze every friend and time out anyone joining.
            if (_client != null || Coop.Hosting) Application.runInBackground = true;
            if (_client != null)
            {
                _client.Attach(Host, Coop.Welcome);
                Say("You joined the world at " + Coop.JoinedAddress + " as " + Coop.Name + ".");
            }
            else if (Coop.Hosting && !_smoke) StartHosting(seed);
            if (_smoke)
            {
                Debug.Log("[smoke] scheduled; nothing from this run is saved");
                StartCoroutine(Smoke());
            }
        }

        /// <summary>
        /// --smoke: a scripted play-through for checking a build without hands on the keys. It
        /// gives the kit, digs, crafts, eats, spawns a creature and fights it, and writes what
        /// happened to smoke.txt in the game's data folder (and the log).
        /// </summary>
        System.Collections.IEnumerator Smoke()
        {
            var lines = new List<string>();
            Action<string> note = t => { lines.Add(t); Debug.Log("[smoke] " + t); };
            note("waiting for the world to settle");
            yield return new WaitForSecondsRealtime(6f);
            PlayerRec me = Me;
            note("start: hp " + me.C.Hp + ", bag empty " + me.C.Inv.IsEmpty + ", plants nearby " + Host.PlantsInWorld.Count + ", creatures " + Host.Creatures.Count);
            GiveKit();
            note("kit: flint_pick " + me.C.Inv.Count("flint_pick") + ", cord " + me.C.Inv.Count("cord"));
            // Dig the ground under our feet with the pick.
            for (int i = 0; i < 32; i++) if (me.C.Inv.KeyAt(i) == "flint_pick") me.C.Selected = i;
            // The ground surface 1.5 m in front: the top solid voxel of that column.
            Vec3 ahead = me.Pos + me.Facing.Flat.Normalized * 1.5;
            int fx = (int)Math.Floor(ahead.X * 4), fz = (int)Math.Floor(ahead.Z * 4);
            int fy = (int)Math.Round(Host.GroundHeight(ahead.X, ahead.Z) * 4) - 1;
            note("dig target " + fx + "," + fy + "," + fz + ": " + Host.World.Blocks.Get(Host.World.Get(fx, fy, fz)).Name);
            int bagBefore = 0;
            for (int i = 0; i < 32; i++) { var st = me.C.Inv[i]; if (st.HasValue) bagBefore += st.Value.Count; }
            DigTick t = default;
            for (int i = 0; i < 200 && !t.Broken && !t.Refused; i++) { t = Host.Dig(LocalId, fx, fy, fz, 0.05); Host.Time += 0.05; yield return null; }
            int bagAfter = 0;
            for (int i = 0; i < 32; i++) { var st = me.C.Inv[i]; if (st.HasValue) bagAfter += st.Value.Count; }
            note("dig with pick: broken " + t.Broken + " refused " + t.Refused + " '" + t.Message + "', bag " + bagBefore + " -> " + bagAfter);
            note("craft cord: '" + Host.Craft(LocalId, "cord") + "' (empty means it worked)");
            for (int i = 0; i < 32; i++) if (me.C.Inv.KeyAt(i) == "roast_meat") me.C.Selected = i;
            me.C.Cond["hunger"] = 60;
            Host.UseHeld(LocalId, false, 0, 0, 0, 0, 0, 0);
            note("eat roast meat: hunger 60 -> " + me.C.Cond["hunger"].ToString("0"));
            for (int i = 0; i < 32; i++) if (me.C.Inv.KeyAt(i) == "reed_spear") me.C.Selected = i;
            Vec3 fwd = me.Facing.Flat.Normalized;
            Creature c = Host.SpawnCreature("redstrider", new Vec3(me.Pos.X + fwd.X * 1.5, Host.GroundHeight(me.Pos.X + fwd.X * 1.5, me.Pos.Z + fwd.Z * 1.5), me.Pos.Z + fwd.Z * 1.5), 777);
            yield return new WaitForSecondsRealtime(1f);
            int swings = 0;
            for (; swings < 40 && c.State != "dead"; swings++)
            {
                c.Pos = me.Pos + fwd * 1.5;
                me.C.Stamina = me.C.StaminaMax;
                Host.Attack(LocalId, false);
                yield return new WaitForSecondsRealtime(0.6f);
            }
            note("fight redstrider: state " + c.State + " after " + swings + " swings, raw_meat " + me.C.Inv.Count("raw_meat") + ", hide " + me.C.Inv.Count("hide"));
            SmokeElder(note, "before the ruin");
            yield return SmokeRuin(note);
            yield return SmokeInscription(note);
            yield return SmokeInscriptionMore(note);
            yield return new WaitForSecondsRealtime(1f);
            SmokeElder(note, "after the ruin");
            yield return SmokeM4(note);
            note("end: hp " + me.C.Hp.ToString("0") + ", day " + Host.Day + " " + (Host.TimeOfDay * 24).ToString("0.0") + "h, creatures " + Host.Creatures.Count + ", plants " + Host.PlantsInWorld.Count + ", structures " + Host.Structures.Count);
            File.WriteAllLines(Path.Combine(Application.persistentDataPath, "smoke.txt"), lines.ToArray());
        }

        /// <summary>M4: skill points and the tree, a Fallhammer slam, the steading claimed and mended with a shape.</summary>
        System.Collections.IEnumerator SmokeM4(Action<string> note)
        {
            PlayerRec me = Me;
            if (Host.Tree == null) { note("m4: no skill tree loaded"); yield break; }
            note("m4 skills: " + me.C.SkillPoints + " points after the ruin, " + me.C.Words.Count + " words");
            int pts = me.C.SkillPoints;
            foreach (string id in new[] { "sense_of_down", "heavy_hand", "wide_fall" })
                note("m4 learn " + id + ": '" + Host.LearnSkill(LocalId, id) + "'");
            yield return null;   // the new modifiers apply on the next tick
            yield return null;
            note("m4 skills: " + me.C.SkillPoints + " left of " + pts + ", debt_cost_mult " + me.C.Mods["debt_cost_mult"].ToString("0.00"));

            // A heavy Fallhammer blow beside a redstrider that is outside the swing's arc.
            for (int i = 0; i < 32; i++) if (me.C.Inv.KeyAt(i) == "fallhammer") me.C.Selected = i;
            Vec3 fwd = me.Facing.Flat.Normalized, side = new Vec3(fwd.Z, 0, -fwd.X);
            Vec3 spot = me.Pos + fwd * 1.6 + side * 2.4;
            Creature c = Host.SpawnCreature("redstrider", new Vec3(spot.X, Host.GroundHeight(spot.X, spot.Z), spot.Z), 778);
            double hp = c.Hp;
            me.C.Stamina = me.C.StaminaMax;
            note("m4 slam: attack " + Host.Attack(LocalId, true));
            // Hold it in place until the blow lands; a redstrider would rather run.
            for (float t0 = Time.realtimeSinceStartup; Time.realtimeSinceStartup - t0 < 1.2f;) { c.Pos = new Vec3(spot.X, c.Pos.Y, spot.Z); yield return null; }
            note("m4 slam: redstrider hp " + hp.ToString("0") + " -> " + c.Hp.ToString("0") + " (" + c.State + ")");

            // The steading: plant a Tether on the plinth, then mend the east wall with a shape.
            if (Host.Village == null) { note("m4 steading: no village"); yield break; }
            Vec3 plinth = Host.Village.SteadingPlinth;
            int gx = (int)Math.Floor(plinth.X * 4), gz = (int)Math.Floor(plinth.Z * 4), gy = (int)Math.Round(plinth.Y * 4) - 1;
            me.Pos = plinth + new Vec3(0, -0.5, -1.5);
            me.RespawnAt = me.Pos;
            yield return new WaitForSecondsRealtime(2f);   // let the chunks there load
            me.C.Inv.Add("tether_stone", 1);
            for (int i = 0; i < 32; i++) if (me.C.Inv.KeyAt(i) == "tether_stone") me.C.Selected = i;
            note("m4 steading: plinth is " + Host.World.Blocks.Get(Host.World.Get(gx, gy, gz)).Name + ", set tether " + Host.UseHeld(LocalId, true, gx, gy, gz, gx, gy + 1, gz) +
                 ", claimed " + Host.WorldFlags.Contains(Host.SteadingFlag));
            me.C.Inv.Add("stone_brick", 300);
            for (int i = 0; i < 32; i++) if (me.C.Inv.KeyAt(i) == "stone_brick") me.C.Selected = i;
            int x1 = Aruun.Core.Town.Village.SteadingX + 11, z0 = Aruun.Core.Town.Village.SteadingZ - 8, z1 = Aruun.Core.Town.Village.SteadingZ + 9;
            int fy = gy - 1;   // the plinth stands one course above the yard floor
            me.Pos = new Vec3((x1 - 6) / 4.0, (fy + 1) / 4.0, (z0 + z1) / 8.0);
            me.RespawnAt = me.Pos;
            string why = Host.BuildShape(LocalId, Aruun.Core.Building.Shape.Wall, x1, fy + 1, z0 + 1, x1, fy + 10, z1 - 1);
            note("m4 build east wall: '" + why + "', stone_brick left " + me.C.Inv.Count("stone_brick"));
            // Stand in the yard looking at the mended cottage, for the screenshot.
            // Midday, bag closed, standing in the yard south-west of the plinth, looking at plinth and cottage.
            PlayHud.SetBag(false);
            double tod = Host.TimeOfDay;
            Host.Time += ((0.5 - tod) % 1 + 1) % 1 * Host.Rules.DayLength;
            Vec3 stand = plinth + new Vec3(-2.5, -0.5, -4.5);
            me.Pos = stand;
            me.RespawnAt = stand;   // the player's body follows a respawn point, not a bare position
            Vec3 lookAt = new Vec3(Aruun.Core.Town.Village.SteadingX / 4.0, plinth.Y, (Aruun.Core.Town.Village.SteadingZ - 4) / 4.0);
            me.ArriveYaw = Math.Atan2(lookAt.X - stand.X, lookAt.Z - stand.Z);
        }

        /// <summary>Talks to the elder and takes the first answer, as a player would.</summary>
        void SmokeElder(Action<string> note, string when)
        {
            PlayerRec me = Me;
            PersonRec elder = Host.Person("elder");
            if (elder == null) { note("village: no elder (people " + Host.People.Count + ", village " + (Host.Village != null) + ")"); return; }
            me.Pos = elder.Pos + new Vec3(0, 0, 1.5);
            me.RespawnAt = me.Pos;
            Conversation c = Host.StartTalk(me, elder);
            note("village " + when + ": " + c.Name + " says \"" + (c.Say.Length > 60 ? c.Say.Substring(0, 60) + "..." : c.Say) + "\"");
            Host.Choose(LocalId, 0);
            Host.EndTalk(LocalId);
            note("village " + when + ": main quest stage " + me.C.Quests.Stage("main_remember") + " (1000 = done), objective: " + Host.ObjectiveFor(LocalId).Text);
        }

        /// <summary>The echo ruin, start to finish, through the host as a player would do it.</summary>
        System.Collections.IEnumerator SmokeRuin(Action<string> note)
        {
            PlayerRec me = Me;
            EchoRuin r = Host.Ruin;
            if (r == null) { note("ruin: none in this world"); yield break; }
            note("ruin: arch at " + r.Ox + "," + r.Ground + "," + r.Oz + " (" + Vec3.Distance(me.Pos, r.EntranceM).ToString("0") + " m away)");
            Func<Vec3, Vec3, string, bool> speak = (feet, at, word) =>
            {
                me.Pos = feet;
                me.RespawnAt = feet;
                Host.Time += 5;
                Vec3 eye = feet + new Vec3(0, 1.6, 0);
                return Host.Speak(LocalId, new List<string> { word }, eye, at - eye);
            };
            Func<string, RuinInscription> ins = place => r.Inscriptions.Find(i => i.Place == place);
            GoToRuin(false);
            yield return new WaitForSecondsRealtime(2f);
            note("ruin objective: " + Host.ObjectiveFor(LocalId).Text);
            Host.Read(me, ins("ruin_arch"));
            Host.Read(me, ins("ruin_antechamber_left"));
            note("ruin: Vuh is " + me.C.Lang.Of("word_push"));
            note("ruin gate: " + speak(r.GateM, r.Mechanism("gate").Point, "word_push") + ", open " + EchoRuin.Done(Host.World, r.Mechanism("gate")));
            yield return new WaitForSecondsRealtime(1.5f);
            Host.Read(me, ins("ruin_antechamber_right"));
            Host.Read(me, ins("ruin_step_hall"));
            note("ruin stair: " + speak(r.StepM, r.Mechanism("stair").Point, "word_rise") + ", raised " + EchoRuin.Done(Host.World, r.Mechanism("stair")));
            yield return new WaitForSecondsRealtime(1.5f);
            GoToRuin(true);
            yield return new WaitForSecondsRealtime(2f);
            Creature s = Host.Sentinel;
            note("ruin sentinel: " + (s != null ? s.State : "missing"));
            Host.Read(me, ins("ruin_court_west"));
            Host.Read(me, ins("ruin_court_east"));
            Vec3 below = r.CourtM + new Vec3(0, 0, -6);
            note("ruin heart before: " + speak(below, r.Mechanism("heart").Point, "word_pull"));
            if (s != null)
            {
                s.State = "exposed";
                s.T = 3;
                Host.HitCreature(s, 100000, 0, Vec3.Zero, LocalId);
            }
            yield return new WaitForSecondsRealtime(1f);
            note("ruin heart after: " + speak(below, r.Mechanism("heart").Point, "word_pull") + ", remembered " + Host.WorldFlags.Contains(EchoRuin.FlagRemembered));
            note("ruin words known: push " + me.C.Lang.Knows("word_push") + ", rise " + me.C.Lang.Knows("word_rise") + ", pull " + me.C.Lang.Knows("word_pull") + ", weigh " + me.C.Lang.Knows("word_weigh"));
            yield return new WaitForSecondsRealtime(1f);
            me.Pos = r.ExitM;
            me.RespawnAt = r.ExitM;
            yield return new WaitForSecondsRealtime(2f);
            note("ruin exit objective: " + Host.ObjectiveFor(LocalId).Text + " (done " + Host.ObjectiveFor(LocalId).Done + ")");
        }

        /// <summary>The inscription slice: find Ulun, cut it into a pick at the table, make it ring, mine with it.</summary>
        System.Collections.IEnumerator SmokeInscription(Action<string> note)
        {
            PlayerRec me = Me;
            EchoRuin r = Host.Ruin;
            if (r == null || Host.Inscriptions == null) { note("inscription: no ruin or no rules"); yield break; }
            Host.Read(me, r.Inscriptions.Find(i => i.Place == "ruin_step_hall_east"));
            Host.Read(me, r.Inscriptions.Find(i => i.Place == "ruin_table"));
            note("inscription: Ulun is " + me.C.Lang.Of("word_sonic"));
            Vec3 feet = new Vec3(r.TableM.X - 1.6, r.CourtM.Y, r.TableM.Z);
            me.Pos = feet;
            me.RespawnAt = feet;
            yield return new WaitForSecondsRealtime(1.5f);
            Vec3 eye = me.Pos + new Vec3(0, 1.6, 0);
            note("inscription table: opened " + Host.Interact(LocalId, eye, r.TableM - new Vec3(0, 0.1, 0) - eye) + ", at table " + Host.AtTable(LocalId));
            int pick = -1;
            for (int i = 0; i < 32; i++) if (me.C.Inv.KeyAt(i) == "flint_pick") pick = i;
            if (pick < 0) { note("inscription: no pick in the bag"); yield break; }
            me.C.Selected = pick;
            note("inscribe Ulun on the pick: '" + Host.Inscribe(LocalId, pick, "word_sonic") + "' (empty means it worked), words " + me.C.Inv[pick].Value.Words + ", Ulun known " + me.C.Lang.Knows("word_sonic"));
            yield return new WaitForSecondsRealtime(1.5f);
            PlayHud.SetBag(false);
            me.C.Stamina = me.C.StaminaMax;
            note("ring the pick: " + Host.UseHeld(LocalId, false, 0, 0, 0, 0, 0, 0) + ", resonating " + Host.Resonating(me));
            // A rock to mine, in the court in front of the table.
            int x = (int)Math.Floor((feet.X - 3) * 4), y = (int)Math.Floor(feet.Y * 4), z = (int)Math.Floor(feet.Z * 4);
            ushort rock = Host.World.Blocks.Id("rust_rock");
            View.MarkDirty(Host.World.Apply(Core.Edits.EditOp.Box(x - 3, y, z - 3, x + 3, y + 6, z + 3, rock)).Dirty);
            yield return new WaitForSecondsRealtime(0.5f);
            int before = me.C.Inv.Count("rust_rock");
            DigTick t = default;
            int ticks = 0;
            for (; ticks < 200 && !t.Broken && !t.Refused; ticks++) { t = Host.Dig(LocalId, x + 3, y + 3, z, 0.05); Host.Time += 0.05; yield return null; }
            note("resonant dig: broken " + t.Broken + " in " + (ticks * 0.05).ToString("0.00") + " s, rust rock in bag " + before + " -> " + me.C.Inv.Count("rust_rock"));
            yield return new WaitForSecondsRealtime(2f);
        }

        /// <summary>The rest of the slice in play: Ulun + Vuh on the hammer (shockwave), Tahr and Oshen alone,
        /// then Tahr + Oshen together on the hammer, mending a worn hammer by itself.</summary>
        System.Collections.IEnumerator SmokeInscriptionMore(Action<string> note)
        {
            PlayerRec me = Me;
            EchoRuin r = Host.Ruin;
            if (r == null || Host.Inscriptions == null) yield break;
            foreach (string w in new[] { "word_push", "word_fortify", "word_reclaim" }) me.C.Learn(w);
            foreach (string k in new[] { "fallhammer", "flint_axe", "reed_spear" }) if (me.C.Inv.Count(k) == 0) me.C.Inv.Add(k, 1);
            me.C.Inv.Add("word_fragment", 2);
            Vec3 feet = new Vec3(r.TableM.X - 1.6, r.CourtM.Y, r.TableM.Z);
            me.Pos = feet;
            Vec3 eye = me.Pos + new Vec3(0, 1.6, 0);
            Host.Interact(LocalId, eye, r.TableM - new Vec3(0, 0.1, 0) - eye);
            Func<string, int> slotOf = key => { for (int i = 0; i < 32; i++) if (me.C.Inv.KeyAt(i) == key) return i; return -1; };
            int hammer = slotOf("fallhammer"), axe = slotOf("flint_axe"), spear = slotOf("reed_spear");
            Func<int, string> active = i => string.Join(", ", Host.InscriptionOf(me, i).Active.ConvertAll(x => x.Id).ToArray());
            note("hammer: Ulun '" + Host.Inscribe(LocalId, hammer, "word_sonic") + "', Vuh '" + Host.Inscribe(LocalId, hammer, "word_push") + "', active [" + active(hammer) + "], fragments left " + me.C.Inv.Count("word_fragment"));
            note("axe: Tahr '" + Host.Inscribe(LocalId, axe, "word_fortify") + "', active [" + active(axe) + "]; spear: Oshen '" + Host.Inscribe(LocalId, spear, "word_reclaim") + "', active [" + active(spear) + "]");
            PlayHud.SetBag(false);
            // A creature in front of the hammer, then the shockwave.
            me.C.Selected = hammer;
            Creature target = null;
            foreach (string key in new[] { "redstrider", "skitter" }) if (target == null && Host.CreatureDefs.ContainsKey(key)) target = Host.SpawnCreature(key, new Vec3(feet.X - 3, Host.GroundHeight(feet.X - 3, feet.Z) + 0.05, feet.Z), 9100);
            if (target == null) foreach (var k in Host.CreatureDefs.Keys) { target = Host.SpawnCreature(k, new Vec3(feet.X - 3, Host.GroundHeight(feet.X - 3, feet.Z) + 0.05, feet.Z), 9100); break; }
            yield return new WaitForSecondsRealtime(1f);
            me.C.Stamina = me.C.StaminaMax;
            double hpBefore = target != null ? target.Hp : 0;
            Vec3 tBefore = target != null ? target.Pos : default;
            note("shockwave: used " + Host.UseHeld(LocalId, false, 0, 0, 0, 0, 0, 0) + ", stamina " + me.C.Stamina.ToString("0") + "/" + me.C.StaminaMax.ToString("0") + (target != null ? ", " + target.Def.Key + " hp " + hpBefore.ToString("0.0") + " -> " + target.Hp.ToString("0.0") : ", no creature"));
            yield return new WaitForSecondsRealtime(0.8f);
            if (target != null) note("shockwave: creature moved " + (target.Pos - tBefore).Length.ToString("0.0") + " m");
            yield return new WaitForSecondsRealtime(1.5f);
            // Grind both words off, cut Tahr + Oshen, wear the hammer and let it mend by itself.
            me.Pos = feet;
            eye = me.Pos + new Vec3(0, 1.6, 0);
            Host.Interact(LocalId, eye, r.TableM - new Vec3(0, 0.1, 0) - eye);
            Host.Erase(LocalId, hammer); Host.Erase(LocalId, hammer);
            note("hammer: Tahr '" + Host.Inscribe(LocalId, hammer, "word_fortify") + "', Oshen '" + Host.Inscribe(LocalId, hammer, "word_reclaim") + "', active [" + active(hammer) + "], self-mending found " + me.C.Combos.Contains("self_mending"));
            PlayHud.SetBag(false);
            me.C.Inv.Wear(hammer, 5);
            int worn = me.C.Inv[hammer].Value.Durability;
            yield return new WaitForSecondsRealtime(12f);
            note("self-mending: hammer durability " + worn + " -> " + me.C.Inv[hammer].Value.Durability + " after 12 s (6 a minute expected)");
        }

        readonly Dictionary<int, CreatureView> _people = new Dictionary<int, CreatureView>();

        /// <summary>Draws the village's people: their model, walking or standing, turned the way they face.</summary>
        void SyncPeople(float dt)
        {
            foreach (PersonRec r in Host.People)
            {
                CreatureView v;
                if (!_people.TryGetValue(r.Id, out v))
                {
                    v = new CreatureView();
                    if (ModelLibrary.Has(r.Def.Model))
                    {
                        v.Go = ModelLibrary.Instantiate(r.Def.Model);
                        v.Rig = v.Go.GetComponent<VoxelRig>();
                    }
                    else
                    {
                        v.Go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                        Destroy(v.Go.GetComponent<Collider>());
                        v.Go.transform.localScale = new Vector3(0.6f, 0.9f, 0.6f);
                    }
                    v.Go.name = r.Def.ShortName;
                    v.Go.transform.SetParent(_entityRoot, false);
                    v.Go.transform.position = ToUnity(r.Pos);
                    _people[r.Id] = v;
                }
                Transform t = v.Go.transform;
                t.position = Vector3.Lerp(t.position, ToUnity(r.Pos), Mathf.Clamp01(dt * 18f));
                t.rotation = Quaternion.Slerp(t.rotation, Quaternion.Euler(0f, (float)(r.Yaw * Mathf.Rad2Deg), 0f), Mathf.Clamp01(dt * 8f));
                if (v.Rig != null)
                {
                    string clip = r.Walking ? "walk" : "idle";
                    if (clip != v.Rig.CurrentClip && v.Rig.HasClip(clip)) v.Rig.Play(clip);
                }
            }
        }

        Light _heartLight;
        bool _ruinLit;
        float _ruinCheck;

        sealed class ResonancePulse { public Light Light; public float Life, Max, Peak; }
        readonly List<ResonancePulse> _pulses = new List<ResonancePulse>();

        void Pulse(Vec3 at, float seconds, float peak)
        {
            var l = new GameObject("resonance").AddComponent<Light>();
            l.transform.position = ToUnity(at);
            l.type = LightType.Point;
            l.range = 5f;
            l.color = new Color(0.384f, 0.722f, 0.694f); // Resonance #62B8B1
            l.intensity = peak;
            l.shadows = LightShadows.None;
            _pulses.Add(new ResonancePulse { Light = l, Life = seconds, Max = seconds, Peak = peak });
        }

        void FadePulses(float dt)
        {
            for (int i = _pulses.Count - 1; i >= 0; i--)
            {
                var p = _pulses[i];
                p.Life -= dt;
                if (p.Life <= 0 || p.Light == null) { if (p.Light != null) Destroy(p.Light.gameObject); _pulses.RemoveAt(i); continue; }
                p.Light.intensity = p.Peak * Mathf.Clamp01(p.Life / p.Max * 1.5f);
            }
        }

        bool _townLit;

        /// <summary>Lights the town's resonance crystals, once.</summary>
        void SyncTown()
        {
            if (_townLit || Host.Village == null) return;
            _townLit = true;
            var root = new GameObject("Town lights").transform;
            // Lamp blocks (lamp_glow) light themselves through the voxel shader; only the
            // resonance crystals get a Unity light, a faint teal one.
            foreach (Vec3 at in Host.Village.CrystalLights) TownLight(root, at, new Color(0.384f, 0.722f, 0.694f), 8f, 0.9f);
        }

        static void TownLight(Transform root, Vec3 at, Color color, float range, float intensity)
        {
            var l = new GameObject("lamp").AddComponent<Light>();
            l.transform.SetParent(root, false);
            l.transform.position = ToUnity(at);
            l.type = LightType.Point;
            l.range = range;
            l.intensity = intensity;
            l.color = color;
            l.shadows = LightShadows.None;
        }

        /// <summary>Lights the ruin's lamps (once) and the heart's glow until it falls.</summary>
        void SyncRuin()
        {
            EchoRuin r = Host.Ruin;
            if (r == null) return;
            if (!_ruinLit)
            {
                _ruinLit = true;
                var root = new GameObject("Echo ruin lights").transform;
                foreach (Vec3 at in r.Lamps)
                {
                    var l = new GameObject("lamp").AddComponent<Light>();
                    l.transform.SetParent(root, false);
                    l.transform.position = ToUnity(at);
                    l.type = LightType.Point;
                    l.range = 11f;
                    l.intensity = 1.6f;
                    l.color = new Color(1f, 0.78f, 0.5f);
                    l.shadows = LightShadows.None;
                }
                _heartLight = new GameObject("heart").AddComponent<Light>();
                _heartLight.transform.SetParent(root, false);
                _heartLight.transform.position = ToUnity(r.HeartM) + new Vector3(0, 0, 1.5f);
                _heartLight.type = LightType.Point;
                _heartLight.range = 16f;
                _heartLight.intensity = 2.2f;
                _heartLight.color = new Color(0.45f, 1f, 0.9f);
            }
            _ruinCheck -= Time.unscaledDeltaTime;
            if (_ruinCheck > 0 || _heartLight == null) return;
            _ruinCheck = 0.5f;
            bool awake = !EchoRuin.Done(Host.World, r.Mechanism("heart"));
            _heartLight.enabled = awake;
            if (awake) _heartLight.intensity = Host.WorldFlags.Contains(EchoRuin.FlagSentinel) ? 3.5f : 2.2f;
        }

        DevPlayer Player { get { return GetComponent<DevPlayer>(); } }

        void RegisterDevKeys()
        {
            DevTools.Register(KeyCode.K, "Give a full kit (tools, weapons, food, materials)", GiveKit);
            DevTools.Register(KeyCode.J, "Spawn the next kind of creature where you look", () => SpawnNextCreature(false));
            DevTools.Register(KeyCode.J, "Spawn a creature group around you", () => SpawnNextCreature(true), true);
            DevTools.Register(KeyCode.N, "Heal, feed and rest", HealAll);
            DevTools.Register(KeyCode.N, "Kill every creature within 30 m", KillNearby, true);
            DevTools.Register(KeyCode.B, "Dev dig: break anything, no tools (off: survival digging)", () =>
            {
                DevPlayer p = Player;
                if (p == null) return;
                p.BigDig = !p.BigDig;
                Say(p.BigDig ? "Dev dig on: breaks anything, 2 m" : "Dev dig off: survival digging with tools");
            });
            DevTools.Register(KeyCode.Y, "Go to the echo ruin's arch", () => GoToRuin(false));
            DevTools.Register(KeyCode.Y, "Go into the sentinel's court", () => GoToRuin(true), true);
            DevTools.Register(KeyCode.I, "Go to the inscription table in the echo ruin", () =>
            {
                PlayerRec me = Me;
                if (me == null || Host.Ruin == null) { Say("This world has no echo ruin"); return; }
                Vec3 at = new Vec3(Host.Ruin.TableM.X - 1.6, Host.Ruin.CourtM.Y, Host.Ruin.TableM.Z);
                me.Pos = at;
                me.RespawnAt = at;
                Say("The inscription table. Look at its worktop and press E.");
            });
            DevTools.Register(KeyCode.O, "Go to the old steading's yard (the first Tether base)", () =>
            {
                PlayerRec me = Me;
                if (me == null || Host.Village == null) { Say("This world has no steading"); return; }
                Vec3 at = Host.Village.SteadingPlinth + new Vec3(0, 0, -2);
                me.Pos = at;
                me.RespawnAt = at;
                me.ArriveYaw = 0;
                Say("The old steading. Set a Tether on the carved plinth to claim it.");
            });
            DevTools.Register(KeyCode.U, "Know every Avarra word", () =>
            {
                PlayerRec me = Me;
                if (me == null || Host.Lexicon == null) return;
                foreach (var w in Host.Lexicon.Words) me.C.Learn(w.Id);
                Say("You know every word now");
            });
            DevTools.Register(KeyCode.Escape, "Close the bag or the speak panel, or free / capture the mouse", () =>
            {
                if (PlayHud.BagOpen) { PlayHud.SetBag(false); return; }
                if (PlayHud.Speaking) { PlayHud.CloseSpeak(); return; }
                if (Me != null && Me.Talk != null) { Host.EndTalk(LocalId); return; }
                if (PlayHud.JournalOpen) { PlayHud.JournalOpen = false; return; }
                Cursor.lockState = Cursor.lockState == CursorLockMode.Locked ? CursorLockMode.None : CursorLockMode.Locked;
            });
        }

        void GoToRuin(bool court)
        {
            PlayerRec me = Me;
            if (me == null || Host.Ruin == null) { Say("This world has no echo ruin"); return; }
            Vec3 at = court ? Host.Ruin.CourtM + new Vec3(0, 0, 10) : Host.Ruin.EntranceM + new Vec3(0, 0, 6);
            me.Pos = at;
            me.RespawnAt = at;
            Say(court ? "The sentinel's court" : "The echo ruin. Read the carved words with E.");
        }

        void GiveKit()
        {
            PlayerRec me = Me;
            if (me == null) return;
            foreach (string k in new[] { "flint_pick", "flint_axe", "reed_spear", "fallhammer", "roast_meat", "campfire_kit", "tether_stone" })
                if (Host.Items.Has(k)) me.C.Inv.Add(k, k == "roast_meat" ? 10 : 1);
            foreach (string k in new[] { "flint", "reed_stalk", "plant_fibre", "cord", "hide", "bark", "rust_rock", "timber" })
                if (Host.Items.Has(k)) me.C.Inv.Add(k, 20);
            Say("Kit given: tools, weapons, food, a campfire, a Tether and materials");
        }

        int _spawnIndex = -1;

        void SpawnNextCreature(bool group = false)
        {
            PlayerRec me = Me;
            if (me == null || Host.CreatureDefs.Count == 0) return;
            var keys = new List<string>(Host.CreatureDefs.Keys);
            _spawnIndex = (_spawnIndex + 1) % keys.Count;
            string key = keys[_spawnIndex];
            Vec3 fwd = me.Facing.Flat.Normalized;
            int n = group ? 4 : 1;
            for (int i = 0; i < n; i++)
            {
                double a = i * Math.PI * 2 / n;
                Vec3 at = group ? me.Pos + new Vec3(Math.Cos(a) * 8, 0, Math.Sin(a) * 8) : me.Pos + fwd * 6;
                at = new Vec3(at.X, Host.GroundHeight(at.X, at.Z) + (Host.CreatureDefs[key].Flies ? 2 : 0.05), at.Z);
                Host.SpawnCreature(key, at, 9000 + _spawnIndex);
            }
            Say("Spawned " + (group ? n + " x " : "") + Host.CreatureDefs[key].Name + " (J again for the next kind)");
        }

        void HealAll()
        {
            PlayerRec me = Me;
            if (me == null) return;
            me.C.Hp = me.C.HpMax;
            me.C.Stamina = me.C.StaminaMax;
            me.C.Cond = Conditions.StartValues(Host.Rules);
            foreach (var k in new List<string>(me.C.Cond.Keys)) me.C.Cond[k] = 0;
            Say("Healed, fed and rested");
        }

        void KillNearby()
        {
            PlayerRec me = Me;
            if (me == null) return;
            int n = 0;
            foreach (Creature c in Host.Creatures.ToArray())
                if (c.State != "dead" && Vec3.Distance(c.Pos, me.Pos) < 30) { Host.HitCreature(c, 99999, 999, Vec3.Zero, LocalId); n++; }
            Say("Killed " + n + " creatures");
        }

        /// <summary>Opens this world to friends: they get its seed and every edit so far, then a live stream.</summary>
        void StartHosting(int seed)
        {
            var lines = new List<string>();
            try
            {
                var player = GetComponent<DevPlayer>();
                if (player != null) player.FlushJournal();
                string journal = Path.Combine(Application.persistentDataPath, "worlds", "dev-" + seed, "journal.txt");
                if (File.Exists(journal))
                {
                    int s, gen, skipped;
                    using (var reader = new StreamReader(new FileStream(journal, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
                        foreach (EditOp op in Aruun.Core.Edits.Journal.Read(reader, out s, out gen, out skipped)) lines.Add(op.ToLine());
                }
            }
            catch (Exception e) { Debug.LogWarning("Could not read the journal for co-op: " + e.Message); }
            try
            {
                _server = new CoopServer(Host, seed, lines) { HostName = Coop.Name };
                _server.Start(Coop.Port);
                Say("Open to friends on port " + _server.Port + ". Your address: " + LocalAddresses() + ".");
            }
            catch (Exception e)
            {
                _server = null;
                Say("Could not open the world to friends (" + e.Message + ").");
                Debug.LogWarning("Co-op host failed: " + e);
            }
        }

        static string LocalAddresses()
        {
            var found = new List<string>();
            try
            {
                foreach (var a in System.Net.Dns.GetHostAddresses(System.Net.Dns.GetHostName()))
                    if (a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !System.Net.IPAddress.IsLoopback(a)) found.Add(a.ToString());
            }
            catch { }
            return found.Count > 0 ? string.Join(" or ", found.ToArray()) : "see your network settings";
        }

        /// <summary>Draws the other players, with the castaway model.</summary>
        void SyncOthers(float dt)
        {
            var present = new HashSet<int>();
            foreach (PlayerRec p in Host.Players.Values)
            {
                if (p.Id == LocalId) continue;
                present.Add(p.Id);
                CreatureView v;
                if (!_others.TryGetValue(p.Id, out v))
                {
                    v = new CreatureView();
                    if (ModelLibrary.Has("human_castaway"))
                    {
                        v.Go = ModelLibrary.Instantiate("human_castaway");
                        v.Rig = v.Go.GetComponent<VoxelRig>();
                    }
                    else
                    {
                        v.Go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                        Destroy(v.Go.GetComponent<Collider>());
                        v.Go.transform.localScale = new Vector3(0.6f, 0.875f, 0.6f);
                    }
                    v.Go.name = "Player " + p.Id;
                    v.Go.transform.SetParent(_entityRoot, false);
                    v.Go.transform.position = ToUnity(p.Pos);
                    _others[p.Id] = v;
                }
                Transform t = v.Go.transform;
                Vector3 target = ToUnity(p.Pos) + (v.Rig != null ? Vector3.zero : Vector3.up * 0.875f);
                float moving = (target - t.position).magnitude / Mathf.Max(dt, 0.001f);
                t.position = Vector3.Lerp(t.position, target, Mathf.Clamp01(dt * 14f));
                Vector3 f = new Vector3((float)p.Facing.X, 0f, (float)p.Facing.Z);
                if (f.sqrMagnitude > 0.01f) t.rotation = Quaternion.Slerp(t.rotation, Quaternion.LookRotation(f), Mathf.Clamp01(dt * 10f));
                v.Go.SetActive(p.Alive);
                if (v.Rig != null)
                {
                    string clip = moving > 0.6f ? "walk" : "idle";
                    if (clip != v.Rig.CurrentClip && v.Rig.HasClip(clip)) v.Rig.Play(clip);
                }
            }
            var gone = new List<int>();
            foreach (var kv in _others) if (!present.Contains(kv.Key)) gone.Add(kv.Key);
            foreach (int id in gone) { Destroy(_others[id].Go); _others.Remove(id); }
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
            if (_smoke || Bench.Running) return;   // test runs never touch the saves
            PlayerRec me = Me;
            if (me == null) return;
            try
            {
                SafeFile.WriteAllText(_charPath, me.C.ToText());
                if (_client != null) return;   // the world and its flags are the host's to keep
                var flags = new List<string>();
                foreach (string f in Host.WorldFlags) flags.Add("flag " + f);
                SafeFile.WriteAllText(_worldPath, string.Join("\n", flags) + "\n");
            }
            catch (Exception e) { Debug.LogWarning("Could not save the character: " + e.Message); }
        }

        void Update()
        {
            if (Host == null) return;
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            PlayerRec me = Me;
            if (me != null) me.God = DevTools.God;
            SyncClock();
            if (_server != null) _server.Pump(dt);
            Host.Tick(dt);
            if (_server != null)
            {
                _server.AfterTick(dt);
                string line;
                while (_server.Log.TryDequeue(out line)) Debug.Log("[co-op] " + line);
            }
            if (_client != null)
            {
                _client.Pump(dt);
                if (_client.Lost != null && !_lostSaid)
                {
                    _lostSaid = true;
                    Save();
                    Say(_client.Lost + " Your character is saved; go back to the title (Esc, then quit) to play on.");
                }
            }

            // World edits: redraw the chunks and journal them (a joined world is the host's to keep).
            foreach (HostEdit e in Host.Edits)
            {
                View.MarkDirty(e.Result.Dirty);
                if (Journal != null && !_smoke && _client == null) Journal(e.Op);
            }
            Host.Edits.Clear();
            foreach (var m in Host.Messages) if (m.Key == LocalId) Say(m.Value);
            Host.Messages.Clear();
            MyEvents.Clear();
            foreach (HostEvent ev in Host.Events)
            {
                if (ev.Player == LocalId) MyEvents.Add(ev);
                if (ev.Player == LocalId && ev.Kind == "table") { PlayHud.CloseSpeak(); PlayHud.SetBag(true, true); }
                CreatureView cv;
                if (ev.Creature != 0 && _creatures.TryGetValue(ev.Creature, out cv) && ev.Kind == "hit") cv.Punch = 0.18f;
                // Carved words are dark stone; they light in Resonance only while active (read,
                // spoken to, answering), then fade. (Visual guide: lit only when active.)
                if (ev.Kind == "read" || ev.Kind == "glow") Pulse(ev.At, 2.5f, 1.4f);
                else if (ev.Kind == "answer") Pulse(ev.At, 5f, 2.4f);
            }
            Host.Events.Clear();

            SyncCreatures(dt);
            SyncPlants();
            SyncStructures();
            SyncRuin();
            SyncTown();
            FadePulses(Time.unscaledDeltaTime);
            SyncPeople(dt);
            SyncOthers(dt);

            _saveTimer -= Time.unscaledDeltaTime;
            if (_saveTimer <= 0) { _saveTimer = 30f; Save(); }
        }

        float _lastHour = -1;

        /// <summary>
        /// The host's clock is the real one (creatures and Conditions use it). The sky follows
        /// it, and the dev time keys, which move the sky, move the host's clock with them.
        /// </summary>
        void SyncClock()
        {
            DayNight day = DayNight.Instance;
            if (day == null) return;
            if (_lastHour >= 0 && Mathf.Abs(Mathf.DeltaAngle(day.Hour * 15f, _lastHour * 15f)) > 0.05f)
            {
                double shift = Mathf.DeltaAngle(_lastHour * 15f, day.Hour * 15f) / 15f;
                if (shift < 0) shift += 24;
                Host.Time += shift / 24.0 * Host.Rules.DayLength;
            }
            if (day.Paused) Host.Time -= Mathf.Min(Time.deltaTime, 0.1f);   // F10 stops the world's clock too
            day.Hour = (float)(Host.TimeOfDay * 24.0);
            _lastHour = day.Hour;
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
        void OnDestroy()
        {
            Save();
            if (_server != null) _server.Dispose();
            if (_client != null) _client.Dispose();
            if (Instance == this) Instance = null;
        }
    }
}
