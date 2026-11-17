using System;
using System.Collections.Generic;
using Aruun.Core.Combat;
using Aruun.Core.Content;

namespace Aruun.Core.Creatures
{
    /// <summary>A creature kind from content/creatures/creatures.json. Metres, m/s and seconds.</summary>
    public sealed class CreatureDef
    {
        public string Key = "";
        public string Name = "";
        /// <summary>herd, hunter, skittish, defender, swarm or guardian.</summary>
        public string Behavior = "";
        public double Hp = 10, Poise = 10, Mass = 1;
        public double HeightM = 1, RadiusM = 0.5;
        public double WalkMps = 1.5, RunMps = 5, JumpM = 1, SenseM = 12;
        public bool Flies;
        public Dictionary<string, object> Raw = new Dictionary<string, object>();

        /// <summary>A sub-object such as "brain", "attack", "armour", or an empty one.</summary>
        public Dictionary<string, object> Part(string name)
        {
            object v;
            return Raw.TryGetValue(name, out v) && v is Dictionary<string, object> ? (Dictionary<string, object>)v : new Dictionary<string, object>();
        }

        /// <summary>A number in a sub-object, such as Num("brain", "alert_s").</summary>
        public double Num(string part, string key, double fallback = 0) { return MiniJson.Num(Part(part), key, fallback); }

        /// <summary>A number in a sub-sub-object, such as Num("attacks", "slam", "radius_m").</summary>
        public double Num(string part, string sub, string key, double fallback = 0)
        {
            object v;
            var p = Part(part);
            return p.TryGetValue(sub, out v) && v is Dictionary<string, object> ? MiniJson.Num((Dictionary<string, object>)v, key, fallback) : fallback;
        }

        /// <summary>The two numbers of a [low, high] pair, such as "fly_height_m".</summary>
        public static void Pair(Dictionary<string, object> o, string key, out double lo, out double hi)
        {
            var l = MiniJson.List(o, key);
            lo = l.Count > 0 && l[0] is double ? (double)l[0] : 0;
            hi = l.Count > 1 && l[1] is double ? (double)l[1] : lo;
        }

        public static List<CreatureDef> FromJson(string json)
        {
            var list = new List<CreatureDef>();
            foreach (var x in MiniJson.List(MiniJson.ParseObject(json), "creatures"))
            {
                var o = (Dictionary<string, object>)x;
                var d = new CreatureDef
                {
                    Key = MiniJson.Str(o, "key"), Behavior = MiniJson.Str(o, "behavior", "herd"),
                    Hp = MiniJson.Num(o, "hp", 10), Poise = MiniJson.Num(o, "poise", 10), Mass = MiniJson.Num(o, "mass", 1),
                    HeightM = MiniJson.Num(o, "height_m", 1), RadiusM = MiniJson.Num(o, "radius_m", 0.5),
                    WalkMps = MiniJson.Num(o, "walk_mps", 1.5), RunMps = MiniJson.Num(o, "run_mps", 5),
                    JumpM = MiniJson.Num(o, "jump_m", 1), SenseM = MiniJson.Num(o, "sense_m", 12),
                    Flies = MiniJson.Bool(o, "fly"), Raw = o,
                };
                d.Name = MiniJson.Str(o, "name", d.Key);
                if (Array.IndexOf(Brains.Behaviors, d.Behavior) < 0)
                    throw new FormatException("creatures.json: " + d.Key + " has unknown behavior '" + d.Behavior + "'");
                list.Add(d);
            }
            return list;
        }
    }

    /// <summary>Animation codes sent with every move. The Game layer maps them to clips.</summary>
    public enum Anim { Auto = -1, Idle = 0, Walk, Run, Windup, Stagger, Alert, Hop, Charge, Dive, Rest, Dormant, Slam }

    /// <summary>
    /// The host's record of one living creature. The brains read and change it; the Game layer
    /// moves the body (gravity, collision) and writes back Pos, Vel, Grounded and Blocked.
    /// </summary>
    public sealed class Creature
    {
        public int Id;
        public CreatureDef Def;
        public Vec3 Pos, Vel, Home, Goal, Threat;
        /// <summary>Facing, radians. 0 faces +Z (Unity's forward); the direction is (sin, 0, cos).</summary>
        public double Yaw;
        public string State = "idle";
        /// <summary>The state timer, counting down.</summary>
        public double T;
        /// <summary>Attack cooldown, counting down.</summary>
        public double Cd;
        public double Hp, HpMax, Poise;
        public bool Grounded = true;
        /// <summary>How much of the wanted speed the body could not move last step (it ran into something).</summary>
        public double Blocked;
        /// <summary>Creatures spawned together share a group and flee together.</summary>
        public int Group;
        public Anim AnimForce = Anim.Auto;

        // Brain memory.
        public bool Fleeing;
        public int Hops;
        public double Pause;
        public double Crowd;
        public double CalmAt;
        public HashSet<int> Struck = new HashSet<int>();
        public bool Airborne;   // swarm set up
        public double Alt, Phase, Spin = 1;
        public int Prey;
        public int Step;
        public double Empty;
        public double Tick;
        /// <summary>Seconds of a lunge still pushing it, so ground friction does not eat it.</summary>
        public double Launched;

        public Creature(int id, CreatureDef def, Vec3 pos)
        {
            Id = id;
            Def = def;
            Pos = Home = Goal = Threat = pos;
            Hp = HpMax = def.Hp;
            Poise = def.Poise;
            if (def.Behavior == "guardian") State = "dormant";
        }

        public Vec3 Forward { get { return Brains.DirOf(Yaw); } }
    }

    /// <summary>A player as the brains see them.</summary>
    public struct Seen
    {
        public int Id;
        public Vec3 Pos;
        public double Dist;
        public bool Found { get { return Id != 0; } }
    }

    /// <summary>
    /// Everything a brain may ask of or do to the world. The host's creature system implements
    /// it; tests use a small fake. Positions are in metres.
    /// </summary>
    public interface ICreatureWorld
    {
        double WorldTime { get; }
        bool IsNight { get; }
        double Gravity { get; }
        /// <summary>The ruleset's creatures.damage_mult.</summary>
        double DamageMult { get; }
        /// <summary>The ruleset's creatures.hunters_by_day.</summary>
        bool HuntersByDay { get; }
        /// <summary>A number in [0, 1) from the host's seeded random.</summary>
        double Random();
        Seen NearestPlayer(Vec3 pos, double maxDist);
        IReadOnlyList<Seen> LivingPlayers();
        Vec3? PlayerPos(int player);
        bool IsSprinting(int player);
        bool HasBuff(int player, string buff);
        void DamagePlayer(int player, double amount, Vec3 source, string kind);
        void ShovePlayer(int player, Vec3 velocity, double seconds);
        /// <summary>Counts hits of one kind on a player; after `count` within `withinS` seconds the buff is applied.</summary>
        void NoteHit(int player, string tag, double withinS, int count, string buff);
        /// <summary>A creature's melee swing (hunters), judged by the combat rules against every player.</summary>
        void CreatureStrike(Vec3 origin, Vec3 forward, double reach, double arcDeg, double damage);
        /// <summary>Breaks blocks in a ball. Returns true if anything broke.</summary>
        bool Carve(Vec3 centre, double radiusM, int power);
        double GroundHeight(double x, double z);
        bool ClearLine(Vec3 a, Vec3 b);
        /// <summary>The nearest open flame a moth would circle, not within `wardM` of a Tether.</summary>
        Vec3? MothLight(Vec3 pos, double rangeM, double wardM);
        bool NearStructure(Vec3 pos, string station, double rangeM);
        /// <summary>How many creatures of this behavior are in one of these states.</summary>
        int CountInState(string behavior, params string[] states);
        /// <summary>Sets every creature of this group fleeing from the threat.</summary>
        void AlarmGroup(int group, Vec3 threat);
        void SpawnShot(int owner, Vec3 from, Vec3 velocity, double damage);
        /// <summary>Tells clients something to show: windup, strike, wake, glow, slam, weigh, hit.</summary>
        void Event(int creature, string kind, double value);
        void RemoveQuietly(int creature);
    }
}
