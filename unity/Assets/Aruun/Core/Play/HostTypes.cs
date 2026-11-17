using System.Collections.Generic;
using Aruun.Core.Combat;
using Aruun.Core.Edits;
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
        /// <summary>World time of the last hit given or taken, for the "fighting" flag.</summary>
        public double CombatAt = -999;
        public double RespawnIn;
        /// <summary>Set when the player woke up again; the Game layer moves the body there and clears it.</summary>
        public Vec3? RespawnAt;
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
        /// <summary>swing, hit, hurt, blocked, guard_break, evade, die, creature_die, wake, glow, slam, windup, strike, harvest, craft, eat, place.</summary>
        public string Kind;
        /// <summary>The player it concerns, or 0.</summary>
        public int Player;
        /// <summary>The creature it concerns, or 0.</summary>
        public int Creature;
        public double Value;
        public Vec3 At;
    }

    /// <summary>A world edit the host made (digging, a slam, planting): the Game layer journals it and redraws the chunks.</summary>
    public struct HostEdit
    {
        public EditOp Op;
        public EditResult Result;
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
