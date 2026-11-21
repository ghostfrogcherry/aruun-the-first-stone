using System.Collections.Generic;
using Aruun.Core.Combat;
using Aruun.Core.Edits;
using Aruun.Core.People;
using Aruun.Core.Survival;
using Aruun.Core.Voxels;

namespace Aruun.Core.Play
{
    /// <summary>
    /// One player as the host keeps them: the saved character plus what only matters while
    /// playing (where they stand, guard, dodge, cooldowns). Positions are metres, feet at Pos.
    /// </summary>
    public sealed class PlayerRec
    {
        public int Id;
        public CharacterState C;
        public Vec3 Pos;
        /// <summary>Where the player looks, flat or not. Swings and guards use its flat part.</summary>
        public Vec3 Facing = Vec3.Forward;
        /// <summary>Ground speed in m/s, smoothed. Sprinting is judged from it.</summary>
        public double Speed;
        public bool Guard;
        /// <summary>Dev god mode: no damage, hunger or fatigue.</summary>
        public bool God;
        public double Iframes, Stagger;
        public double NextAttack;
        /// <summary>World time before which the player cannot speak again.</summary>
        public double NextWord;
        /// <summary>The conversation the player is in, or null.</summary>
        public Conversation Talk;
        /// <summary>World time of the last hit given or taken, for the "fighting" flag.</summary>
        public double CombatAt = -999;
        public double RespawnIn;
        /// <summary>The inscription table the player opened (voxel), or null.</summary>
        public int[] TableAt;
        /// <summary>World time until which the held item rings (resonant mining).</summary>
        public double ResonantUntil;
        /// <summary>World time before which an activated inscription cannot be used again.</summary>
        public double NextInscription;
        /// <summary>Partial durability mended so far, per bag slot (Oshen).</summary>
        internal readonly Dictionary<int, double> MendAcc = new Dictionary<int, double>();
        /// <summary>Set when the player woke up again; the Game layer moves the body there and clears it.</summary>
        public Vec3? RespawnAt;
        /// <summary>The way to face after going through a door (radians, 0 = north), or null.</summary>
        public double? ArriveYaw;
        /// <summary>A push from a creature (m/s) the Game layer adds to the body, then clears.</summary>
        public Vec3? Shove;
        /// <summary>The flags the last character tick ran with, for the HUD.</summary>
        public HashSet<string> Flags = new HashSet<string>();
        internal readonly Dictionary<string, List<double>> HitTimes = new Dictionary<string, List<double>>();
        internal double FlagTimer;
        internal HashSet<string> EnvFlags = new HashSet<string>();

        public bool Alive { get { return !C.Dead; } }
        /// <summary>Chest height, where swings come from and hits land.</summary>
        public Vec3 Chest { get { return Pos + new Vec3(0, 1.2, 0); } }
    }

    /// <summary>A plant or flint node standing in the world: wild ones come from the seed, crops are planted.</summary>
    public sealed class PlantRec
    {
        public int Id;
        public string Plant = "";
        /// <summary>The voxel it stands in.</summary>
        public int X, Y, Z;
        public double PlantedAt;
        public double Mult = 1;
        /// <summary>The wild node id, or "" for a crop someone planted.</summary>
        public string WildId = "";
        public Vec3 Pos { get { return new Vec3((X + 0.5) * WorldConfig.VoxelSize, Y * WorldConfig.VoxelSize, (Z + 0.5) * WorldConfig.VoxelSize); } }
    }

    /// <summary>A placed structure: a campfire or a Tether.</summary>
    public sealed class StructureRec
    {
        public int Id;
        public string Key = "";
        public Vec3 Pos;
        public int Owner;
    }

    /// <summary>Something for the Game layer to show: a hit, a swing, a death, a word of feedback.</summary>
    public struct HostEvent
    {
        /// <summary>swing, hit, hurt, blocked, guard_break, evade, die, creature_die, wake, glow, slam, windup, strike, harvest, craft, eat, place; and for inscriptions table, inscribe, erase, resonate_on, resonate, resonate_off, shockwave.</summary>
        public string Kind;
        /// <summary>The player it concerns, or 0.</summary>
        public int Player;
        /// <summary>The creature it concerns, or 0.</summary>
        public int Creature;
        public double Value;
        public Vec3 At;
        /// <summary>The block it concerns (resonate: the stone that shattered), or 0.</summary>
        public ushort Block;
    }

    /// <summary>A world edit the host made (digging, a slam, planting): the Game layer journals it and redraws the chunks.</summary>
    public struct HostEdit
    {
        public EditOp Op;
        public EditResult Result;
    }

    /// <summary>A person of the village standing in the world.</summary>
    public sealed class PersonRec
    {
        public int Id;
        public PersonDef Def;
        public Vec3 Pos, Home, Goal;
        public double Yaw;
        public double Wait;
        public bool Walking;
        /// <summary>Lives indoors (the library), on a floor rather than the ground.</summary>
        public bool Indoors;
        /// <summary>The player talking to them, or 0.</summary>
        public int TalkingTo;
        /// <summary>Which way they face when they stop (a seller faces their counter), or null for any.</summary>
        public double? HomeYaw;
    }

    /// <summary>What a person is saying to a player, and the answers the player can give.</summary>
    public sealed class Conversation
    {
        public int Person;
        public string Name = "";
        public string Say = "";
        /// <summary>The answers, in order; the last one always ends the talk.</summary>
        public readonly List<string> Choices = new List<string>();
        internal readonly List<ChoiceDef> Defs = new List<ChoiceDef>();
        /// <summary>Which page of the talk the player is on ("" for the first; "shop" for a shop's wares).</summary>
        internal string Menu = "";
    }

    /// <summary>What the player should do next in the echo ruin, and where.</summary>
    public struct Objective
    {
        public string Title;
        public string Text;
        /// <summary>Where it points, in metres; null when there is nowhere in particular.</summary>
        public Vec3? Target;
        public bool Done;
    }

    /// <summary>The answer to one tick of holding dig.</summary>
    public struct DigTick
    {
        public bool Refused, Broken;
        /// <summary>How worn the block is, 0 to 1.</summary>
        public double Fraction;
        public string Message;
    }
}
