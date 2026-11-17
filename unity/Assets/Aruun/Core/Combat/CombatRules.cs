using System;
using Aruun.Core.Content;
using Aruun.Core.Items;
using Aruun.Core.Survival;

namespace Aruun.Core.Combat
{
    /// <summary>The numbers of one swing.</summary>
    public struct AttackProfile
    {
        public double Damage, Stamina, Cooldown, Windup, Poise, Knockback, Reach, ArcDeg;
    }

    /// <summary>What a hit on a guarding defender does.</summary>
    public struct GuardResult
    {
        public double Damage;
        public double StaminaCost;
        public bool Broken;
    }

    /// <summary>
    /// Melee rules. Pure logic: the host calls these to decide every hit. Everything is in metres
    /// and seconds (1 Unity unit is 1 m, so no conversion), as the content files are.
    /// </summary>
    public static class CombatRules
    {
        public const double HeavyDamage = 1.9;
        public const double HeavyStamina = 1.8;
        public const double HeavyCooldown = 1.5;
        public const double HeavyPoise = 2.2;
        public const double LightWindup = 0.16;
        public const double HeavyWindup = 0.42;
        /// <summary>A guard blocks hits from roughly the front 160 degrees.</summary>
        public const double GuardArcDot = 0.2;
        public const double GuardStaminaPerDamage = 1.1;

        /// <summary>Weapon stats of the held item, or bare hands for items that are not weapons.</summary>
        public static AttackProfile Profile(ItemRegistry items, string heldKey, bool heavy, Mods mods)
        {
            ItemDef def;
            var stats = items.TryGet(heldKey, out def) && def.Raw.ContainsKey("damage") ? def.Raw : items.UnarmedRaw;
            double dmg = MiniJson.Num(stats, "damage", 5) * mods["damage_mult"];
            return new AttackProfile
            {
                Damage = dmg * (heavy ? HeavyDamage : 1.0),
                Stamina = MiniJson.Num(stats, "stamina", 6) * (heavy ? HeavyStamina : 1.0),
                Cooldown = MiniJson.Num(stats, "cooldown_s", 0.45) * (heavy ? HeavyCooldown : 1.0),
                Windup = heavy ? HeavyWindup : LightWindup,
                Poise = MiniJson.Num(stats, "poise", 6) * (heavy ? HeavyPoise : 1.0),
                Knockback = MiniJson.Num(stats, "knockback", 2) * (heavy ? 1.5 : 1.0),
                Reach = MiniJson.Num(stats, "reach_m", 1.6),
                ArcDeg = MiniJson.Num(stats, "arc_deg", 80),
            };
        }

        /// <summary>
        /// Is the target's centre inside the swing: within reach of origin and within the arc
        /// around forward on the ground plane? The arc widens by the angle the target's body covers.
        /// </summary>
        public static bool InArc(Vec3 origin, Vec3 forward, Vec3 target, double reach, double arcDeg, double targetRadius = 0)
        {
            Vec3 to = target - origin;
            if (Math.Abs(to.Y) > reach + targetRadius) return false;
            double dist = to.FlatLength;
            if (dist > reach + targetRadius) return false;
            if (dist <= targetRadius + 0.125) return true;   // standing inside it
            if (forward.FlatLength < 0.001) return true;
            double half = arcDeg * 0.5 * Math.PI / 180.0 + Math.Atan2(targetRadius, dist);
            return Vec3.FlatAngle(forward, to) <= half;
        }

        /// <summary>A hit on a guarding defender. A guard you cannot pay for breaks and lets the rest through.</summary>
        public static GuardResult Guard(double damage, double guardFrac, bool facing, double stamina)
        {
            if (!facing) return new GuardResult { Damage = damage };
            double cost = damage * GuardStaminaPerDamage;
            if (stamina < cost)
            {
                double paid = stamina / cost;
                return new GuardResult { Damage = damage * (1.0 - guardFrac * paid), StaminaCost = stamina, Broken = true };
            }
            return new GuardResult { Damage = damage * (1.0 - guardFrac), StaminaCost = cost };
        }

        public static bool IsFacing(Vec3 defenderPos, Vec3 defenderForward, Vec3 source)
        {
            Vec3 to = (source - defenderPos).Flat, f = defenderForward.Flat;
            if (to.Length < 0.01 || f.Length < 0.01) return true;
            return Vec3.Dot(f.Normalized, to.Normalized) >= GuardArcDot;
        }

        /// <summary>
        /// Damage from landing at `speed` m/s. Gravity debt is repaid in weight: you land harder
        /// while you owe (up to 1.5 times at full debt).
        /// </summary>
        public static double FallDamage(double speed, Ruleset rules, double gravityDebt = 0)
        {
            speed = Math.Max(0, Math.Min(100, speed)) * (1.0 + gravityDebt / 200.0);
            double over = speed - rules.Num("fall", "safe_mps", 9.0);
            return Math.Max(0, over) * rules.Num("fall", "damage_per_mps", 5.0);
        }

        /// <summary>Velocity kick away from `from`, with a little lift so the target leaves the ground.</summary>
        public static Vec3 Knockback(Vec3 from, Vec3 to, double strength, double mass)
        {
            Vec3 dir = (to - from).Flat;
            dir = dir.Length > 0.01 ? dir.Normalized : Vec3.Forward;
            return (dir * strength + Vec3.Up * (strength * 0.45)) / Math.Max(mass, 0.1);
        }
    }
}
