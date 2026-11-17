using System;
using System.Collections.Generic;
using Aruun.Core.Combat;
using Aruun.Core.Content;
using Aruun.Core.Creatures;
using Aruun.Core.Edits;
using Aruun.Core.Farming;
using Aruun.Core.Items;
using Aruun.Core.Mining;
using Aruun.Core.Physics;
using Aruun.Core.Survival;
using Aruun.Core.Voxels;
using Aruun.Core.Worldgen;

namespace Aruun.Core.Play
{
    /// <summary>
    /// The game's rules running together: the one place that owns the world's living state
    /// (players' characters, creatures, plants, structures, the clock) and judges every action.
    /// The Game layer only asks ("dig here", "swing", "eat this") and shows what comes back.
    ///
    /// It is written for co-op: every action names its player, and nothing here reads input or
    /// draws. In single player the Game layer is the only client. Positions are metres, feet
    /// at Pos; the voxel world is in voxels (4 per metre).
    /// </summary>
    public sealed class Host : ICreatureWorld
    {
        public const double M = WorldConfig.VoxelsPerMeter;
        public const double CreatureStep = 0.05;
        public const int MaxCreatures = 26;
        public const double SpawnMin = 34, SpawnMax = 54, DespawnDist = 110;
        public const int WildCellRadius = 3;
        public const double WalkSpeed = 4.5;
        /// <summary>How far a player can reach to harvest, use or place, in metres.</summary>
        public const double ReachM = 4.0;
        /// <summary>Radius of one dig bite, tenths of a voxel (1.6 voxels: a 0.4 m scoop).</summary>
        public const int BiteRadius10 = 16;

        public readonly VoxelWorld World;
        public readonly ItemRegistry Items;
        public readonly Ruleset Rules;
        public readonly Plants Plants;
        public readonly Dictionary<string, CreatureDef> CreatureDefs = new Dictionary<string, CreatureDef>();
        public readonly Dictionary<int, PlayerRec> Players = new Dictionary<int, PlayerRec>();
        public readonly List<Creature> Creatures = new List<Creature>();
        public readonly List<PlantRec> PlantsInWorld = new List<PlantRec>();
        public readonly List<StructureRec> Structures = new List<StructureRec>();
        /// <summary>Edits the host made since the Game layer last drained them.</summary>
        public readonly List<HostEdit> Edits = new List<HostEdit>();
        public readonly List<HostEvent> Events = new List<HostEvent>();
        /// <summary>Lines of feedback for one player ("Timber needs an axe"), oldest first.</summary>
        public readonly List<KeyValuePair<int, string>> Messages = new List<KeyValuePair<int, string>>();

        public double Time;
        /// <summary>Spawn point in metres: where players without a Tether wake.</summary>
        public Vec3 SpawnPoint;
        /// <summary>Turn off to stop wild creatures appearing (screenshots, tests).</summary>
        public bool SpawnCreatures = true;

        readonly DigProgress _dig = new DigProgress();
        readonly System.Random _rng;
        readonly Dictionary<string, Mover> _movers = new Dictionary<string, Mover>();
        readonly Dictionary<string, double> _harvested = new Dictionary<string, double>();
        readonly HashSet<long> _cells = new HashSet<long>();
        readonly Dictionary<int, Body> _bodies = new Dictionary<int, Body>();
        readonly List<Swing> _swings = new List<Swing>();
        int _nextId = 1;
        double _creatureAcc, _slowAcc, _spawnTimer = 2;

        struct Swing
        {
            public double At;
            public int Player;
            public AttackProfile Prof;
        }

        public Host(VoxelWorld world, ItemRegistry items, Ruleset rules, Plants plants, IEnumerable<CreatureDef> creatures, int seed)
        {
            World = world;
            Items = items;
            Rules = rules;
            Plants = plants;
            foreach (var d in creatures) CreatureDefs[d.Key] = d;
            _rng = new System.Random(seed ^ 0x5a17);
            // Start the first day mid-morning.
            Time = Rules.DayLength * 0.3;
        }

        HeightField Field { get { return World.Generator.Field; } }

        // ===================================================================== clock

        public double TimeOfDay { get { return DayClock.TimeOfDay(Time, Rules.DayLength); } }
        public int Day { get { return DayClock.DayNumber(Time, Rules.DayLength); } }
        public bool IsNight { get { return DayClock.IsNight(TimeOfDay); } }

        // ===================================================================== players

        /// <summary>Adds a player with a character (a fresh one is set up when `fresh`).</summary>
        public PlayerRec Join(int id, CharacterState c, Vec3 pos, bool fresh)
        {
            if (fresh) c.SetupNew();
            var p = new PlayerRec { Id = id, C = c, Pos = pos };
            Players[id] = p;
            return p;
        }

        public void Leave(int id)
        {
            Players.Remove(id);
            _dig.Stop(id);
        }

        /// <summary>The Game layer reports where the player is and looks, every frame.</summary>
        public void SetPose(int id, Vec3 feet, Vec3 facing, double dt)
        {
            PlayerRec p;
            if (!Players.TryGetValue(id, out p)) return;
            if (dt > 0)
            {
                double moved = (feet - p.Pos).FlatLength / dt;
                if (moved < 60) p.Speed += (moved - p.Speed) * Math.Min(1, dt * 8);
            }
            p.Pos = feet;
            if (facing.Length > 0.01) p.Facing = facing.Normalized;
        }

        void Say(int player, string text) { Messages.Add(new KeyValuePair<int, string>(player, text)); }

        void Fx(string kind, int player, int creature, double value, Vec3 at)
        {
            Events.Add(new HostEvent { Kind = kind, Player = player, Creature = creature, Value = value, At = at });
        }

        /// <summary>Changes the world and remembers the edit for the Game layer.</summary>
        EditResult Apply(EditOp op)
        {
            var r = World.Apply(op);
            if (r.Changed > 0) Edits.Add(new HostEdit { Op = op, Result = r });
            return r;
        }

        // ===================================================================== digging and building

        /// <summary>
        /// One tick of a player holding dig on voxel (x, y, z) with what they hold. The block wears
        /// down; when it breaks, a bite is taken out around it and the drops go in the bag.
        /// </summary>
        public DigTick Dig(int id, int x, int y, int z, double dt)
        {
            PlayerRec p;
            if (!Players.TryGetValue(id, out p) || !p.Alive) return new DigTick { Refused = true };
            BlockDef block = World.Blocks.Get(World.Get(x, y, z));
            ToolDef tool = p.C.HeldTool;
            var r = _dig.Hit(id, x, y, z, block, tool, dt, Time);
            if (r.Outcome == DigProgress.Outcome.Refused) return new DigTick { Refused = true, Message = r.Message };
            if (r.Outcome == DigProgress.Outcome.Working) return new DigTick { Fraction = r.Fraction };

            ToolClass cls = tool.Class == ToolClass.Hand ? ToolClass.Hand : tool.Class;
            EditOp op = EditOp.Sphere(x, y, z, BiteRadius10, 0, tool.Power).WithTool(cls);
            EditResult result = Apply(op);
            GiveDrops(p, result);
            if (Rules.Durability && p.C.Inv.Wear(p.C.Selected)) Say(id, "Your " + tool.Name.ToLowerInvariant() + " breaks");
            return new DigTick { Broken = true, Fraction = 1 };
        }

        public void StopDigging(int id) { _dig.Stop(id); }

        void GiveDrops(PlayerRec p, EditResult result)
        {
            var got = new Dictionary<string, int>();
            foreach (var kv in result.Removed)
            {
                BlockDef b = World.Blocks.Get(kv.Key);
                // One roll per few voxels, so a bite of soil gives a handful, not fifty.
                int rolls = Math.Max(1, (kv.Value + 4) / 8);
                for (int i = 0; i < rolls; i++)
                    foreach (var d in Items.RollDrops(b, _rng.NextDouble))
                    {
                        int n;
                        got.TryGetValue(d.Key, out n);
                        got[d.Key] = n + d.Value;
                    }
            }
            bool full = false;
            foreach (var kv in got)
                if (p.C.Inv.Add(kv.Key, kv.Value) > 0) full = true;
            if (full) Say(p.Id, "Your bag is full");
        }

        /// <summary>
        /// Right click on a voxel face: places the held block in the empty cell (px, py, pz)
        /// next to it, or plants a seed, sets down a structure, or eats what is held.
        /// (gx, gy, gz) is the voxel that was looked at. hasGround is false when nothing was in reach.
        /// </summary>
        public bool UseHeld(int id, bool hasGround, int gx, int gy, int gz, int px, int py, int pz)
        {
            PlayerRec p;
            if (!Players.TryGetValue(id, out p) || !p.Alive) return false;
            string key = p.C.HeldKey;
            ItemDef def;
            if (key.Length == 0 || !Items.TryGet(key, out def)) return false;
            switch (def.Kind)
            {
                case "food":
                    if (!p.C.Use(key)) return false;
                    p.C.Inv.TakeSlot(p.C.Selected, 1);
                    string verb = MiniJson.Str(MiniJson.Obj(def.Raw, "use"), "verb", "eat");
                    Say(id, "You " + verb + " the " + def.Name.ToLowerInvariant());
                    Fx("eat", id, 0, 0, p.Pos);
                    return true;
                case "block":
                    if (!hasGround) return false;
                    return PlaceBlock(p, def, px, py, pz);
                case "seed":
                case "structure":
                    if (!hasGround) { Say(id, "Look at the ground where it should go"); return false; }
                    string why = SetDown(p, def, gx, gy, gz);
                    if (why.Length > 0) { Say(id, "Can't: " + why); return false; }
                    p.C.Inv.TakeSlot(p.C.Selected, 1);
                    return true;
            }
            return false;
        }

        bool PlaceBlock(PlayerRec p, ItemDef def, int x, int y, int z)
        {
            if (World.Get(x, y, z) != 0 && World.Blocks.Get(World.Get(x, y, z)).Collision) return false;
            if (Vec3.Distance(p.Pos + new Vec3(0, 1.6, 0), CellCentre(x, y, z)) > ReachM + 1) return false;
            // Never build into a body.
            foreach (var other in Players.Values)
                if (other.Alive && InBody(other.Pos, 0.3, 1.75, x, y, z)) return false;
            ushort id = World.Blocks.Id(def.Block);
            if (id == 0) return false;
            var r = Apply(EditOp.Set(x, y, z, id));
            if (r.Changed == 0) return false;
            p.C.Inv.TakeSlot(p.C.Selected, 1);
            return true;
        }

        static bool InBody(Vec3 feet, double half, double height, int x, int y, int z)
        {
            double v = WorldConfig.VoxelSize;
            return x * v < feet.X + half && (x + 1) * v > feet.X - half &&
                   z * v < feet.Z + half && (z + 1) * v > feet.Z - half &&
                   y * v < feet.Y + height && (y + 1) * v > feet.Y;
        }

        static Vec3 CellCentre(int x, int y, int z)
        {
            double v = WorldConfig.VoxelSize;
            return new Vec3((x + 0.5) * v, (y + 0.5) * v, (z + 0.5) * v);
        }

        string SetDown(PlayerRec p, ItemDef def, int gx, int gy, int gz)
        {
            Vec3 at = CellCentre(gx, gy + 1, gz) - new Vec3(0, 0.125, 0);
            if (Vec3.Distance(p.Pos + new Vec3(0, 1.6, 0), at) > ReachM + 1) return "too far away";
            BlockDef ground = World.Blocks.Get(World.Get(gx, gy, gz));
            if (!ground.Collision) return "that is not solid ground";
            BlockDef above = World.Blocks.Get(World.Get(gx, gy + 1, gz));
            if (above.Collision || above.Fluid) return "there is no room above it";
            if (def.Kind == "seed")
            {
                PlantDef plant = Plants.Get(MiniJson.Str(def.Raw, "plant"));
                if (plant == null) return "it will not grow";
                if (!Plants.CanPlant(plant, ground.Key)) return plant.Name.ToLowerInvariant() + " will not grow in " + ground.Name.ToLowerInvariant();
                foreach (var other in PlantsInWorld)
                    if (Vec3.FlatDistance(other.Pos, at) < 0.6) return "too close to another plant";
                if (above.Id != 0) Apply(EditOp.Set(gx, gy + 1, gz, 0));
                double mult = NearStructure(at, "tether", 30) ? MiniJson.Num(Items.Structure("tether").Raw, "growth_mult", 1) : 1;
                AddPlant(plant.Key, gx, gy + 1, gz, Time, mult, "");
                Say(p.Id, "Planted " + plant.Name.ToLowerInvariant() + (mult > 1 ? " (the Tether quickens it)" : ""));
                return "";
            }
            string sKey = MiniJson.Str(def.Raw, "structure");
            StructureDef sdef = Items.Structure(sKey);
            if (sdef == null) return "nothing to build";
            if (above.Id != 0) Apply(EditOp.Set(gx, gy + 1, gz, 0));
            Structures.Add(new StructureRec { Id = _nextId++, Key = sKey, Pos = new Vec3(at.X, (gy + 1) * WorldConfig.VoxelSize, at.Z), Owner = p.Id });
            if (MiniJson.Bool(sdef.Raw, "respawn"))
            {
                p.C.Tether = new[] { gx, gy + 1, gz };
                Say(p.Id, "You plant a Tether. This is home now: you will wake here.");
            }
            else Say(p.Id, "You set down a " + sdef.Name.ToLowerInvariant());
            Fx("place", p.Id, 0, 0, at);
            return "";
        }

        // ===================================================================== plants

        PlantRec AddPlant(string plant, int x, int y, int z, double plantedAt, double mult, string wildId)
        {
            var r = new PlantRec { Id = _nextId++, Plant = plant, X = x, Y = y, Z = z, PlantedAt = plantedAt, Mult = mult, WildId = wildId };
            PlantsInWorld.Add(r);
            return r;
        }

        /// <summary>The growth stage a plant shows now, 0-based.</summary>
        public int StageOf(PlantRec r)
        {
            PlantDef def = Plants.Get(r.Plant);
            return def == null ? 0 : Plants.Stage(def, r.PlantedAt, Time, r.Mult);
        }

        public bool IsRipe(PlantRec r)
        {
            PlantDef def = Plants.Get(r.Plant);
            return def != null && Plants.IsRipe(def, r.PlantedAt, Time, r.Mult);
        }

        /// <summary>
        /// E: harvest the plant or use the structure nearest the line of sight from `eye` along
        /// `dir`, within reach. Returns false when there is nothing there.
        /// </summary>
        public bool Interact(int id, Vec3 eye, Vec3 dir)
        {
            PlayerRec p;
            if (!Players.TryGetValue(id, out p) || !p.Alive) return false;
            dir = dir.Normalized;
            PlantRec bestPlant = null;
            StructureRec bestStruct = null;
            double best = double.MaxValue;
            foreach (var r in PlantsInWorld)
            {
                double d = RayScore(eye, dir, r.Pos + new Vec3(0, 0.3, 0), 0.7);
                if (d < best) { best = d; bestPlant = r; bestStruct = null; }
            }
            foreach (var s in Structures)
            {
                double d = RayScore(eye, dir, s.Pos + new Vec3(0, 0.4, 0), 0.9);
                if (d < best) { best = d; bestStruct = s; bestPlant = null; }
            }
            if (bestPlant != null) { Harvest(p, bestPlant); return true; }
            if (bestStruct != null)
            {
                StructureDef def = Items.Structure(bestStruct.Key);
                if (def != null && MiniJson.Bool(def.Raw, "respawn"))
                {
                    p.C.Tether = new[] { (int)(bestStruct.Pos.X * M), (int)(bestStruct.Pos.Y * M), (int)(bestStruct.Pos.Z * M) };
                    Say(id, "This Tether is now your home. You will wake here.");
                }
                else Say(id, "The fire is warm. Stand by it to rest and cook.");
                return true;
            }
            return false;
        }

        /// <summary>Distance along the ray to a target near it, or infinity when it is off the ray or out of reach.</summary>
        static double RayScore(Vec3 eye, Vec3 dir, Vec3 target, double radius)
        {
            Vec3 to = target - eye;
            double along = Vec3.Dot(to, dir);
            if (along < 0 || along > ReachM + radius) return double.MaxValue;
            double off = (to - dir * along).Length;
            return off <= radius ? along : double.MaxValue;
        }

        void Harvest(PlayerRec p, PlantRec r)
        {
            PlantDef def = Plants.Get(r.Plant);
            if (def == null) return;
            if (!Plants.IsRipe(def, r.PlantedAt, Time, r.Mult)) { Say(p.Id, def.Name + " is not ripe yet"); return; }
            var got = Plants.Harvest(def, (lo, hi) => _rng.Next(lo, hi + 1));
            var parts = new List<string>();
            foreach (var g in got)
            {
                if (g.Value <= 0) continue;
                p.C.Inv.Add(g.Key, g.Value);
                ItemDef item;
                parts.Add(g.Value + " " + (Items.TryGet(g.Key, out item) ? item.Name.ToLowerInvariant() : g.Key));
            }
            if (r.WildId.Length > 0)
            {
                _harvested[r.WildId] = Time + Plants.WildRegrowSeconds;
                PlantsInWorld.Remove(r);
            }
            else
            {
                double? back = Plants.RegrowTime(def, Time, r.Mult);
                if (back == null) PlantsInWorld.Remove(r);
                else r.PlantedAt = back.Value;
            }
            Say(p.Id, parts.Count > 0 ? "You gather " + string.Join(", ", parts) : "Nothing worth taking");
            Fx("harvest", p.Id, 0, 0, r.Pos);
        }

        void UpdateWildCells()
        {
            int cell = Plants.CellVoxels;
            var want = new HashSet<long>();
            foreach (var p in Players.Values)
            {
                int cx = WildSpawns.CellOf((int)Math.Floor(p.Pos.X * M), cell);
                int cz = WildSpawns.CellOf((int)Math.Floor(p.Pos.Z * M), cell);
                for (int dz = -WildCellRadius; dz <= WildCellRadius; dz++)
                    for (int dx = -WildCellRadius; dx <= WildCellRadius; dx++)
                        want.Add(CellKey(cx + dx, cz + dz));
            }
            // Forget cells nobody is near; their wild plants come back from the seed later.
            var gone = new List<long>();
            foreach (long k in _cells) if (!want.Contains(k)) gone.Add(k);
            foreach (long k in gone)
            {
                _cells.Remove(k);
                int cx = (int)(k >> 32), cz = (int)(k & 0xffffffff);
                string prefix = "w:" + cx + ":" + cz + ":";
                PlantsInWorld.RemoveAll(r => r.WildId.StartsWith(prefix, StringComparison.Ordinal));
            }
            foreach (long k in want)
            {
                if (_cells.Contains(k)) continue;
                _cells.Add(k);
                int cx = (int)(k >> 32), cz = (int)(k & 0xffffffff);
                foreach (WildNode n in WildSpawns.InCell(Field, Plants, cx, cz))
                {
                    double back;
                    if (_harvested.TryGetValue(n.Id, out back))
                    {
                        if (Time < back) continue;
                        _harvested.Remove(n.Id);
                    }
                    // Wild plants stand on the ground as it is now: skip ones whose ground was dug away or built over.
                    if (!World.Blocks.Get(World.Get(n.X, n.Y - 1, n.Z)).Collision) continue;
                    if (World.Blocks.Get(World.Get(n.X, n.Y, n.Z)).Collision) continue;
                    AddPlant(n.Plant, n.X, n.Y, n.Z, -1e9, 1, n.Id);
                }
            }
        }

        static long CellKey(int cx, int cz) { return ((long)cx << 32) | (uint)cz; }

        // ===================================================================== crafting

        /// <summary>What the player stands near: "fire", "tether".</summary>
        public HashSet<string> StationsNear(Vec3 pos)
        {
            var set = new HashSet<string>();
            foreach (var s in Structures)
            {
                StructureDef def = Items.Structure(s.Key);
                if (def != null && def.Station.Length > 0 && Vec3.Distance(s.Pos, pos) <= 3.5) set.Add(def.Station);
            }
            return set;
        }

        /// <summary>"" when the player can craft it now, or why not.</summary>
        public string CanCraft(int id, string recipeId)
        {
            PlayerRec p;
            if (!Players.TryGetValue(id, out p)) return "no player";
            return Crafting.Check(Items, Items.Recipe(recipeId), p.C.Inv, StationsNear(p.Pos), p.C.Words.Count > 0);
        }

        public string Craft(int id, string recipeId)
        {
            PlayerRec p;
            if (!Players.TryGetValue(id, out p) || !p.Alive) return "no player";
            Recipe r = Items.Recipe(recipeId);
            string why = Crafting.Craft(Items, r, p.C.Inv, StationsNear(p.Pos), p.C.Words.Count > 0);
            if (why.Length > 0) { Say(id, "Can't craft: " + why); return why; }
            ItemDef def;
            Say(id, "Made " + (r.Count > 1 ? r.Count + " " : "") + (Items.TryGet(r.Out, out def) ? def.Name.ToLowerInvariant() : r.Out));
            Fx("craft", id, 0, 0, p.Pos);
            return "";
        }

        // ===================================================================== combat

        /// <summary>Starts a swing with what the player holds. It lands after the wind-up.</summary>
        public bool Attack(int id, bool heavy)
        {
            PlayerRec p;
            if (!Players.TryGetValue(id, out p) || !p.Alive) return false;
            if (Time < p.NextAttack || p.Stagger > 0) return false;
            AttackProfile prof = CombatRules.Profile(Items, p.C.HeldKey, heavy, p.C.Mods);
            if (!p.C.Spend(prof.Stamina)) { Say(id, "You are out of breath"); return false; }
            p.Guard = false;
            p.NextAttack = Time + prof.Cooldown;
            p.CombatAt = Time;
            _swings.Add(new Swing { At = Time + prof.Windup, Player = id, Prof = prof });
            Fx("swing", id, 0, heavy ? 1 : 0, p.Pos);
            return true;
        }

        /// <summary>Is there a creature in front of the player, within this reach? Lets left click choose between swing and dig.</summary>
        public bool CreatureInReach(int id, double reach)
        {
            PlayerRec p;
            if (!Players.TryGetValue(id, out p)) return false;
            foreach (var c in Creatures)
                if (c.State != "dead" && CombatRules.InArc(p.Chest, p.Facing, c.Pos + new Vec3(0, c.Def.HeightM * 0.5, 0), reach, 70, c.Def.RadiusM))
                    return true;
            return false;
        }

        public void SetGuard(int id, bool on)
        {
            PlayerRec p;
            if (Players.TryGetValue(id, out p)) p.Guard = on && p.Alive && p.C.Stamina > 1 && p.Stagger <= 0;
        }

        /// <summary>A quick sidestep with a moment of not being hit. Returns false when out of stamina.</summary>
        public bool Dodge(int id)
        {
            PlayerRec p;
            if (!Players.TryGetValue(id, out p) || !p.Alive) return false;
            if (!p.C.Spend(Rules.Num("vitals", "dodge_cost", 18))) return false;
            p.Iframes = Rules.Num("vitals", "dodge_iframes_s", 0.4);
            p.Guard = false;
            return true;
        }

        void RunSwings()
        {
            for (int i = _swings.Count - 1; i >= 0; i--)
            {
                Swing s = _swings[i];
                if (s.At > Time) continue;
                _swings.RemoveAt(i);
                PlayerRec p;
                if (!Players.TryGetValue(s.Player, out p) || !p.Alive) continue;
                bool hitAny = false;
                foreach (var c in Creatures.ToArray())
                {
                    if (c.State == "dead") continue;
                    Vec3 centre = c.Pos + new Vec3(0, c.Def.HeightM * 0.5, 0);
                    if (!CombatRules.InArc(p.Chest, p.Facing, centre, s.Prof.Reach, s.Prof.ArcDeg, c.Def.RadiusM)) continue;
                    HitCreature(c, s.Prof.Damage, s.Prof.Poise, CombatRules.Knockback(p.Chest, centre, s.Prof.Knockback, c.Def.Mass), p.Id);
                    hitAny = true;
                }
                if (hitAny)
                {
                    p.CombatAt = Time;
                    if (Rules.Durability && p.C.Inv.Wear(p.C.Selected)) Say(p.Id, "Your weapon breaks");
                }
            }
        }

        /// <summary>Damages a creature: armour, knockback, reaction, and loot when it dies.</summary>
        public void HitCreature(Creature c, double amount, double poise, Vec3 kick, int byPlayer)
        {
            if (c.State == "dead") return;
            double dmg = Brains.Armour(this, c, amount, byPlayer);
            if (dmg < 0) { Fx("immune", byPlayer, c.Id, 0, c.Pos); return; }
            c.Vel = c.Vel + kick;
            if (kick.Length > 4) c.Launched = 0.6;
            if (Brains.Hurt(this, c, dmg, poise, byPlayer)) Kill(c, byPlayer);
        }

        void Kill(Creature c, int byPlayer)
        {
            c.State = "dead";
            c.T = 2.5;
            Fx("creature_die", byPlayer, c.Id, 0, c.Pos);
            PlayerRec p;
            if (byPlayer == 0 || !Players.TryGetValue(byPlayer, out p)) return;
            var parts = new List<string>();
            foreach (object o in MiniJson.List(c.Def.Raw, "drops"))
            {
                var d = o as Dictionary<string, object>;
                if (d == null) continue;
                int n = _rng.Next(MiniJson.Int(d, "min", 1), MiniJson.Int(d, "max", 1) + 1);
                string item = MiniJson.Str(d, "item");
                if (n <= 0 || !Items.Has(item)) continue;
                p.C.Inv.Add(item, n);
                parts.Add(n + " " + Items.Get(item).Name.ToLowerInvariant());
            }
            Say(byPlayer, "The " + c.Def.Name.ToLowerInvariant() + " falls" + (parts.Count > 0 ? ". You take " + string.Join(", ", parts) : ""));
        }

        /// <summary>The Game layer reports a landing at `speed` m/s.</summary>
        public void ReportFall(int id, double speed)
        {
            PlayerRec p;
            if (!Players.TryGetValue(id, out p)) return;
            double debt;
            p.C.Cond.TryGetValue("gravity_debt", out debt);
            double dmg = CombatRules.FallDamage(speed, Rules, debt);
            if (dmg > 0) DamagePlayer(id, dmg, p.Pos, "fall");
        }

        // ===================================================================== tick

        /// <summary>Advances the world by dt seconds.</summary>
        public void Tick(double dt)
        {
            Time += dt;
            RunSwings();
            _creatureAcc += dt;
            int steps = 0;
            while (_creatureAcc >= CreatureStep && steps < 4)
            {
                _creatureAcc -= CreatureStep;
                StepCreatures(CreatureStep);
                steps++;
            }
            if (steps == 4) _creatureAcc = 0;
            foreach (var p in Players.Values) TickPlayer(p, dt);
            _slowAcc += dt;
            if (_slowAcc >= 0.5)
            {
                _slowAcc = 0;
                SlowTick();
            }
            _spawnTimer -= dt;
            if (_spawnTimer <= 0)
            {
                _spawnTimer = 4;
                if (SpawnCreatures) SpawnAround();
            }
        }

        void TickPlayer(PlayerRec p, double dt)
        {
            p.Iframes = Math.Max(0, p.Iframes - dt);
            p.Stagger = Math.Max(0, p.Stagger - dt);
            if (p.C.Dead)
            {
                p.RespawnIn -= dt;
                if (p.RespawnIn <= 0) Respawn(p);
                return;
            }
            p.FlagTimer -= dt;
            if (p.FlagTimer <= 0)
            {
                p.FlagTimer = 0.5;
                p.EnvFlags = EnvironmentFlags(p);
            }
            var flags = new HashSet<string>(p.EnvFlags);
            if (p.Speed > WalkSpeed * 1.25) flags.Add("sprinting");
            if (Time - p.CombatAt < 8) flags.Add("fighting");
            else if (p.Speed < 2 && (flags.Contains("near_heat") || flags.Contains("at_tether"))) flags.Add("resting");
            if (p.Guard && p.C.Stamina <= 0.5) p.Guard = false;
            p.Flags = flags;
            if (p.C.Tick(flags, dt)) Die(p);
        }

        HashSet<string> EnvironmentFlags(PlayerRec p)
        {
            var flags = new HashSet<string>();
            flags.Add(IsNight ? "night" : "day");
            int hx = (int)Math.Floor(p.Pos.X * M), hy = (int)Math.Floor((p.Pos.Y + 1.9) * M), hz = (int)Math.Floor(p.Pos.Z * M);
            bool roof = false;
            for (int y = 1; y < 40 && !roof; y++) roof = World.Blocks.Get(World.Get(hx, hy + y, hz)).Collision;
            flags.Add(roof ? "sheltered" : "exposed");
            foreach (var s in Structures)
            {
                StructureDef def = Items.Structure(s.Key);
                if (def == null) continue;
                double d = Vec3.Distance(s.Pos, p.Pos);
                if (def.HeatM > 0 && d <= def.HeatM) flags.Add("near_heat");
                if (def.Station == "tether" && d <= 6) flags.Add("at_tether");
            }
            int voxY = (int)(p.Pos.Y * M);
            if (voxY > Rules.Num("altitude", "thin_air_above", 110)) flags.Add("high_altitude");
            if (voxY > Rules.Num("altitude", "dusty_on_mesa_above", 70) && Field.IsMesa(hx, hz) && Math.Sin(Time * 0.07) > -0.2) flags.Add("dusty");
            return flags;
        }

        void Die(PlayerRec p)
        {
            p.C.Dead = true;
            p.C.Hp = 0;
            p.Guard = false;
            p.RespawnIn = Rules.Num("death", "respawn_delay_s", 4);
            _dig.Stop(p.Id);
            if (!Rules.Flag("death", "keep_inventory", true)) p.C.Inv.Clear();
            Fx("die", p.Id, 0, 0, p.Pos);
            Say(p.Id, "You fall. " + (Rules.Flag("death", "keep_inventory", true) ? "You will wake at your Tether." : "Your belongings are lost."));
        }

        void Respawn(PlayerRec p)
        {
            p.C.Dead = false;
            p.C.Hp = p.C.HpMax;
            p.C.Stamina = p.C.StaminaMax;
            if (Rules.Flag("death", "reset_conditions")) p.C.Cond = Conditions.StartValues(Rules);
            Vec3 at = SpawnPoint;
            if (p.C.Tether != null && p.C.Tether.Length == 3)
                at = new Vec3((p.C.Tether[0] + 1.5) / M, (p.C.Tether[1] + 1) / M, (p.C.Tether[2] + 0.5) / M);
            p.Pos = at;
            p.RespawnAt = at;
            Fx("wake", p.Id, 0, 0, at);
        }

        void SlowTick()
        {
            UpdateWildCells();
            // Plants whose ground was dug away fall out of the world.
            PlantsInWorld.RemoveAll(r =>
            {
                if (World.Blocks.Get(World.Get(r.X, r.Y - 1, r.Z)).Collision) return false;
                if (r.WildId.Length > 0) _harvested[r.WildId] = Time + Plants.WildRegrowSeconds;
                return true;
            });
            // Wild creatures nobody is near leave.
            Creatures.RemoveAll(c =>
            {
                if (c.State == "dead" || c.Def.Raw.ContainsKey("spawn") && MiniJson.Str(MiniJson.Obj(c.Def.Raw, "spawn"), "when") == "never") return false;
                foreach (var p in Players.Values) if (Vec3.Distance(p.Pos, c.Pos) < DespawnDist) return false;
                _bodies.Remove(c.Id);
                return true;
            });
        }

        // ===================================================================== creatures

        public Creature SpawnCreature(string key, Vec3 pos, int group)
        {
            CreatureDef def;
            if (!CreatureDefs.TryGetValue(key, out def)) return null;
            var c = new Creature(_nextId++, def, pos) { Group = group };
            c.Yaw = _rng.NextDouble() * Math.PI * 2;
            Creatures.Add(c);
            return c;
        }

        Mover MoverFor(CreatureDef def)
        {
            Mover m;
            if (_movers.TryGetValue(def.Key, out m)) return m;
            m = new Mover(World)
            {
                HalfWidth = Math.Max(0.6, def.RadiusM * M * 0.8),
                Height = Math.Max(1.5, def.HeightM * M * 0.9),
                StepUp = 2.05,
            };
            _movers[def.Key] = m;
            return m;
        }

        void StepCreatures(double dt)
        {
            for (int i = Creatures.Count - 1; i >= 0; i--)
            {
                Creature c = Creatures[i];
                if (c.State == "dead")
                {
                    c.T -= dt;
                    if (c.T <= 0) { Creatures.RemoveAt(i); _bodies.Remove(c.Id); }
                    continue;
                }
                Vec3 want = Brains.Think(this, c, dt);
                if (c.State == "dead") continue;
                if (c.Def.Flies) FlyStep(c, want, dt);
                else WalkStep(c, want, dt);
                Vec3 flat = c.Vel.Flat;
                bool steering = c.State != "windup" && c.State != "stagger" && c.Def.Behavior != "guardian" && c.Def.Behavior != "defender";
                if (flat.Length > 0.5 && steering)
                {
                    double to = Brains.YawTo(flat), d = to - c.Yaw;
                    while (d > Math.PI) d -= 2 * Math.PI;
                    while (d < -Math.PI) d += 2 * Math.PI;
                    c.Yaw += d * Math.Min(1, dt * 10);
                }
                if (c.Pos.Y < -50) Kill(c, 0);
            }
        }

        void WalkStep(Creature c, Vec3 want, double dt)
        {
            if (c.Launched <= 0 || (c.Grounded && c.Launched < 0.3))
            {
                double k = Math.Min(1, dt * (c.Grounded ? 10 : 2));
                c.Vel = new Vec3(c.Vel.X + (want.X - c.Vel.X) * k, c.Vel.Y, c.Vel.Z + (want.Z - c.Vel.Z) * k);
            }
            if (c.Grounded && c.Blocked > 0 && want.FlatLength > 0.1 && c.Launched <= 0)
                c.Vel = new Vec3(c.Vel.X, Math.Sqrt(2 * Gravity * c.Def.JumpM), c.Vel.Z);
            Body b;
            if (!_bodies.TryGetValue(c.Id, out b)) b = new Body();
            b.X = c.Pos.X * M; b.Y = c.Pos.Y * M; b.Z = c.Pos.Z * M;
            b.VX = c.Vel.X * M; b.VY = c.Vel.Y * M; b.VZ = c.Vel.Z * M;
            double wantX = b.VX, wantZ = b.VZ, fallSpeed = -c.Vel.Y;
            MoverFor(c.Def).Step(ref b, dt);
            bool landed = !c.Grounded && b.Grounded;
            c.Blocked = Math.Sqrt((wantX - b.VX) * (wantX - b.VX) + (wantZ - b.VZ) * (wantZ - b.VZ)) / M;
            c.Pos = new Vec3(b.X / M, b.Y / M, b.Z / M);
            c.Vel = new Vec3(b.VX / M, b.VY / M, b.VZ / M);
            c.Grounded = b.Grounded;
            _bodies[c.Id] = b;
            if (landed && fallSpeed > 12) HitCreature(c, (fallSpeed - 12) * 6, 20, Vec3.Zero, 0);
        }

        void FlyStep(Creature c, Vec3 want, double dt)
        {
            if (c.Launched <= 0)
            {
                double k = Math.Min(1, dt * 5);
                c.Vel = c.Vel + (want - c.Vel) * k;
            }
            Vec3 next = c.Pos + c.Vel * dt;
            double half = Math.Max(0.6, c.Def.RadiusM * M * 0.8), height = Math.Max(1, c.Def.HeightM * M * 0.8);
            if (!MoverFor(c.Def).Overlaps(next.X * M, next.Y * M, next.Z * M))
            {
                c.Pos = next;
                c.Blocked = 0;
            }
            else
            {
                c.Blocked = c.Vel.Length;
                c.Vel = new Vec3(0, 2, 0);   // bumped into something: rise over it
            }
            c.Grounded = false;
        }

        void SpawnAround()
        {
            int total = 0;
            foreach (var c in Creatures) if (c.State != "dead") total++;
            foreach (var p in Players.Values)
            {
                if (!p.Alive) continue;
                foreach (var def in CreatureDefs.Values)
                {
                    if (total >= MaxCreatures) return;
                    var spawn = MiniJson.Obj(def.Raw, "spawn");
                    string when = MiniJson.Str(spawn, "when", "day");
                    if (when == "never") continue;
                    bool hunter = def.Behavior == "hunter";
                    bool ok = (when == "night") == IsNight || (hunter && HuntersByDay);
                    if (!ok) continue;
                    int have = 0;
                    foreach (var c in Creatures)
                        if (c.Def.Behavior == def.Behavior && c.State != "dead" && Vec3.Distance(c.Pos, p.Pos) < 90) have++;
                    int cap = hunter ? (int)Rules.Num("creatures", "max_hunters_per_player", 2) : MiniJson.Int(def.Raw, "max_per_player", 3);
                    if (have >= cap || _rng.NextDouble() > 0.5) continue;
                    Vec3? spot = SpawnSpot(p.Pos, def, MiniJson.Str(spawn, "where", "land"));
                    if (spot == null) continue;
                    var range = MiniJson.List(def.Raw, "group");
                    int lo = range.Count > 0 ? Convert.ToInt32(range[0]) : 1, hi = range.Count > 1 ? Convert.ToInt32(range[1]) : lo;
                    int n = _rng.Next(lo, hi + 1);
                    int group = _nextId;
                    for (int i = 0; i < n && total < MaxCreatures; i++)
                    {
                        Vec3 at = spot.Value + (i == 0 ? Vec3.Zero : new Vec3(_rng.NextDouble() * 6 - 3, 0, _rng.NextDouble() * 6 - 3));
                        int gx = (int)Math.Floor(at.X * M), gz = (int)Math.Floor(at.Z * M);
                        double y = (Field.HeightAt(gx, gz) + 1) / M + (def.Flies ? 3 : 0.05);
                        SpawnCreature(def.Key, new Vec3(at.X, y, at.Z), group);
                        total++;
                    }
                }
            }
        }

        Vec3? SpawnSpot(Vec3 near, CreatureDef def, string where)
        {
            for (int attempt = 0; attempt < 6; attempt++)
            {
                double a = _rng.NextDouble() * Math.PI * 2, d = SpawnMin + _rng.NextDouble() * (SpawnMax - SpawnMin);
                double x = near.X + Math.Cos(a) * d, z = near.Z + Math.Sin(a) * d;
                int vx = (int)Math.Floor(x * M), vz = (int)Math.Floor(z * M);
                if (!WildSpawns.Fits(Field, where, vx, vz)) continue;
                var pos = new Vec3(x, (Field.HeightAt(vx, vz) + 1) / M, z);
                if (NearStructure(pos, "tether", 30)) continue;
                bool clear = true;
                foreach (var p in Players.Values) if (Vec3.Distance(p.Pos, pos) < SpawnMin * 0.8) clear = false;
                if (clear && !MoverFor(def).Overlaps(pos.X * M, pos.Y * M + 0.05, pos.Z * M)) return pos;
            }
            return null;
        }

        // ===================================================================== ICreatureWorld

        double ICreatureWorld.WorldTime { get { return Time; } }
        bool ICreatureWorld.IsNight { get { return IsNight; } }
        public double Gravity { get { return WorldConfig.Gravity; } }
        public double DamageMult { get { return Rules.Num("creatures", "damage_mult", 1); } }
        public bool HuntersByDay { get { return Rules.Flag("creatures", "hunters_by_day"); } }
        public double Random() { return _rng.NextDouble(); }

        public Seen NearestPlayer(Vec3 pos, double maxDist)
        {
            var best = new Seen();
            foreach (var p in Players.Values)
            {
                if (!p.Alive) continue;
                double d = Vec3.Distance(p.Pos, pos);
                if (d <= maxDist && (!best.Found || d < best.Dist)) best = new Seen { Id = p.Id, Pos = p.Pos, Dist = d };
            }
            return best;
        }

        public IReadOnlyList<Seen> LivingPlayers()
        {
            var list = new List<Seen>();
            foreach (var p in Players.Values) if (p.Alive) list.Add(new Seen { Id = p.Id, Pos = p.Pos });
            return list;
        }

        public Vec3? PlayerPos(int player)
        {
            PlayerRec p;
            return Players.TryGetValue(player, out p) ? p.Pos : (Vec3?)null;
        }

        public bool IsSprinting(int player)
        {
            PlayerRec p;
            return Players.TryGetValue(player, out p) && p.Speed > WalkSpeed * 1.25;
        }

        public bool HasBuff(int player, string buff)
        {
            PlayerRec p;
            return Players.TryGetValue(player, out p) && p.C.Buffs.ContainsKey(buff);
        }

        public void DamagePlayer(int player, double amount, Vec3 source, string kind)
        {
            PlayerRec p;
            if (!Players.TryGetValue(player, out p) || !p.Alive || amount <= 0) return;
            if (p.Iframes > 0 && kind != "fall" && kind != "condition") { Fx("evade", player, 0, 0, p.Pos); return; }
            if (kind != "fall" && kind != "condition") p.CombatAt = Time;
            if (p.Guard && (kind == "creature" || kind == "player"))
            {
                ItemDef held;
                double guardFrac = Items.TryGet(p.C.HeldKey, out held) && held.Raw.ContainsKey("guard")
                    ? MiniJson.Num(held.Raw, "guard", 0.3) : MiniJson.Num(Items.UnarmedRaw, "guard", 0.3);
                GuardResult g = CombatRules.Guard(amount, guardFrac, CombatRules.IsFacing(p.Pos, p.Facing, source), p.C.Stamina);
                amount = g.Damage;
                if (g.StaminaCost > 0)
                {
                    p.C.Stamina = Math.Max(0, p.C.Stamina - g.StaminaCost);
                    p.C.SinceSpend = 0;
                    Fx("blocked", player, 0, 0, p.Pos);
                }
                if (g.Broken)
                {
                    p.Guard = false;
                    p.Stagger = 0.8;
                    Fx("guard_break", player, 0, 0, p.Pos);
                }
            }
            bool died = p.C.Hurt(amount);
            Fx("hurt", player, 0, amount, source);
            if (died) Die(p);
        }

        public void ShovePlayer(int player, Vec3 velocity, double seconds)
        {
            PlayerRec p;
            if (Players.TryGetValue(player, out p)) p.Shove = velocity;
        }

        public void NoteHit(int player, string tag, double withinS, int count, string buff)
        {
            PlayerRec p;
            if (!Players.TryGetValue(player, out p)) return;
            List<double> times;
            if (!p.HitTimes.TryGetValue(tag, out times)) p.HitTimes[tag] = times = new List<double>();
            times.Add(Time);
            times.RemoveAll(t => Time - t > withinS);
            if (times.Count >= count)
            {
                times.Clear();
                p.C.AddBuff(buff);
            }
        }

        public void CreatureStrike(Vec3 origin, Vec3 forward, double reach, double arcDeg, double damage)
        {
            foreach (var p in new List<PlayerRec>(Players.Values))
                if (p.Alive && CombatRules.InArc(origin, forward, p.Chest, reach, arcDeg, 0.35))
                    DamagePlayer(p.Id, damage * DamageMult, origin, "creature");
        }

        public bool Carve(Vec3 centre, double radiusM, int power)
        {
            int r10 = (int)Math.Round(radiusM * M * 10);
            var r = Apply(EditOp.Sphere((int)Math.Floor(centre.X * M), (int)Math.Floor(centre.Y * M), (int)Math.Floor(centre.Z * M), Math.Max(r10, 5), 0, power));
            return r.Changed > 0;
        }

        public double GroundHeight(double x, double z)
        {
            int vx = (int)Math.Floor(x * M), vz = (int)Math.Floor(z * M);
            // Search down from a little above the generated height, so dug holes and builds count.
            int top = Field.HeightAt(vx, vz) + 16;
            for (int y = top; y > top - 64; y--)
                if (World.Blocks.Get(World.Get(vx, y, vz)).Collision) return (y + 1) / M;
            return Field.HeightAt(vx, vz) / M;
        }

        public bool ClearLine(Vec3 a, Vec3 b)
        {
            Vec3 d = b - a;
            double len = d.Length;
            if (len < 0.01) return true;
            VoxelHit hit = VoxelRaycast.Cast(World, a.X * M, a.Y * M, a.Z * M, d.X / len, d.Y / len, d.Z / len, (int)(len * M) + 1);
            return !hit.Hit || hit.Distance >= len * M - 0.5;
        }

        public Vec3? MothLight(Vec3 pos, double rangeM, double wardM)
        {
            Vec3? best = null;
            double bestD = rangeM;
            foreach (var s in Structures)
            {
                StructureDef def = Items.Structure(s.Key);
                if (def == null || !MiniJson.Bool(def.Raw, "draws_moths")) continue;
                double d = Vec3.Distance(s.Pos, pos);
                if (d < bestD && !NearStructure(s.Pos, "tether", wardM)) { bestD = d; best = s.Pos + new Vec3(0, 1, 0); }
            }
            return best;
        }

        public bool NearStructure(Vec3 pos, string station, double rangeM)
        {
            foreach (var s in Structures)
            {
                StructureDef def = Items.Structure(s.Key);
                if (def != null && (def.Station == station || s.Key == station) && Vec3.Distance(s.Pos, pos) <= rangeM) return true;
            }
            return false;
        }

        public int CountInState(string behavior, params string[] states)
        {
            int n = 0;
            foreach (var c in Creatures)
                if (c.Def.Behavior == behavior && Array.IndexOf(states, c.State) >= 0) n++;
            return n;
        }

        public void AlarmGroup(int group, Vec3 threat)
        {
            foreach (var c in Creatures)
                if (c.Group == group) Brains.Alarm(this, c, threat);
        }

        public void SpawnShot(int owner, Vec3 from, Vec3 velocity, double damage)
        {
            // Thrown shards land at once for now: whoever is near the line takes the hit.
            Vec3 dir = velocity.Normalized;
            foreach (var p in new List<PlayerRec>(Players.Values))
            {
                if (!p.Alive) continue;
                Vec3 to = p.Chest - from;
                double along = Vec3.Dot(to, dir);
                if (along > 0 && along < 20 && (to - dir * along).Length < 0.6 && ClearLine(from, p.Chest))
                    DamagePlayer(p.Id, damage * DamageMult, from, "creature");
            }
        }

        public void Event(int creature, string kind, double value)
        {
            Vec3 at = Vec3.Zero;
            foreach (var c in Creatures) if (c.Id == creature) at = c.Pos;
            Fx(kind, 0, creature, value, at);
        }

        public void RemoveQuietly(int creature)
        {
            Creatures.RemoveAll(c => c.Id == creature);
            _bodies.Remove(creature);
        }
    }
}
