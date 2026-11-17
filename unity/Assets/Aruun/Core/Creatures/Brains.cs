using System;
using System.Collections.Generic;
using Aruun.Core.Combat;
using Aruun.Core.Content;

namespace Aruun.Core.Creatures
{
    /// <summary>
    /// How creatures think. Host only. Think returns the velocity a creature wants this step
    /// (3D for fliers) and may change its state. Every number comes from creatures.json; the
    /// design behind each brain is docs/design/mob_behaviours.md.
    ///
    ///  herd      grazes, the whole group bolts when one is hurt or you come close
    ///  hunter    hunts at night: chases, winds up, lunges; slinks away by day
    ///  skittish  reedhopper: freezes when it notices you, then the group flees in hops
    ///  defender  glassback: ignores you until crowded or hit, then charges in a line
    ///  swarm     duskmoth: drifts at night, circles open flame, dives at who sits by it
    ///  guardian  echo sentinel: dormant rubble that wakes and runs a fixed drill
    /// </summary>
    public static class Brains
    {
        public static readonly string[] Behaviors = { "herd", "hunter", "skittish", "defender", "swarm", "guardian" };

        public static Vec3 DirOf(double yaw) { return new Vec3(Math.Sin(yaw), 0, Math.Cos(yaw)); }
        public static double YawTo(Vec3 v) { return Math.Atan2(v.X, v.Z); }

        static double Clamp01(double v) { return v < 0 ? 0 : v > 1 ? 1 : v; }

        static double Wrap(double a)
        {
            while (a > Math.PI) a -= 2 * Math.PI;
            while (a < -Math.PI) a += 2 * Math.PI;
            return a;
        }

        static double LerpAngle(double from, double to, double t) { return from + Wrap(to - from) * t; }

        static double Range(ICreatureWorld w, double lo, double hi) { return lo + (hi - lo) * w.Random(); }

        public static Vec3 Think(ICreatureWorld w, Creature c, double dt)
        {
            c.T -= dt;
            c.Cd -= dt;
            c.Launched -= dt;
            c.Poise = Math.Min(c.Poise + dt * 4.0, c.Def.Poise);
            c.AnimForce = Anim.Auto;
            switch (c.Def.Behavior)
            {
                case "herd":
                case "hunter": return HerdOrHunter(w, c);
                case "skittish": return Skittish(w, c, dt);
                case "defender": return Defender(w, c, dt);
                case "swarm": return Swarm(w, c, dt);
                case "guardian": return Guardian(w, c, dt);
            }
            return Vec3.Zero;
        }

        static Vec3 Wander(ICreatureWorld w, Creature c, double minM, double maxM)
        {
            switch (c.State)
            {
                case "idle":
                    if (c.T <= 0)
                    {
                        double a = w.Random() * 2 * Math.PI;
                        c.Goal = c.Home + new Vec3(Math.Cos(a), 0, Math.Sin(a)) * Range(w, minM, maxM);
                        c.State = "wander";
                        c.T = Range(w, 3, 6);
                    }
                    break;
                case "wander":
                    Vec3 to = (c.Goal - c.Pos).Flat;
                    if (c.T <= 0 || to.Length < 0.5)
                    {
                        c.State = "idle";
                        c.T = Range(w, 2, 5);
                    }
                    else
                    {
                        c.Yaw = YawTo(to);
                        return to.Normalized * c.Def.WalkMps;
                    }
                    break;
            }
            return Vec3.Zero;
        }

        // ---- Redstrider (herd) and nightstalker (hunter)

        static Vec3 HerdOrHunter(ICreatureWorld w, Creature c)
        {
            var d = c.Def;
            bool hunter = d.Behavior == "hunter";
            Seen target = w.NearestPlayer(c.Pos, d.SenseM);
            bool hunting = hunter && (w.IsNight || w.HuntersByDay);
            switch (c.State)
            {
                case "stagger":
                    c.AnimForce = Anim.Stagger;
                    if (c.T <= 0)
                    {
                        c.State = hunter ? "chase" : "flee";
                        c.T = 5;
                    }
                    return Vec3.Zero;
                case "idle":
                case "wander":
                    if (hunting && target.Found) { c.State = "chase"; return Vec3.Zero; }
                    if (c.State == "idle")
                    {
                        if (hunter && target.Found)
                        {
                            c.State = "flee";   // daylight: slink away
                            c.T = 6;
                            c.Threat = target.Pos;
                            return Vec3.Zero;
                        }
                        if (!hunter && target.Found && target.Dist < 4.0)
                        {
                            w.AlarmGroup(c.Group, target.Pos);
                            return Vec3.Zero;
                        }
                    }
                    return Wander(w, c, 3, 9);
                case "flee":
                    if (c.T <= 0)
                    {
                        c.State = "idle";
                        c.T = 2;
                        c.Home = c.Pos;
                    }
                    Vec3 away = (c.Pos - c.Threat).Flat;
                    Vec3 dir = away.Length > 0.025 ? away.Normalized : Vec3.Forward;
                    c.Yaw = YawTo(dir);
                    return dir * d.RunMps;
                case "chase":
                {
                    if (!target.Found || !hunting)
                    {
                        c.State = "idle";
                        c.T = 1;
                        c.Home = c.Pos;
                        return Vec3.Zero;
                    }
                    Vec3 to = (target.Pos - c.Pos).Flat;
                    double reach = d.Num("attack", "reach_m", 2);
                    if (to.Length < reach * 0.75 && Math.Abs(target.Pos.Y - c.Pos.Y) < reach)
                    {
                        if (c.Cd <= 0 && c.Grounded)
                        {
                            c.State = "windup";
                            c.T = d.Num("attack", "windup_s", 0.5);
                            c.Yaw = YawTo(to);
                            w.Event(c.Id, "windup", 0);
                        }
                        return Vec3.Zero;
                    }
                    c.Yaw = YawTo(to);
                    return to.Normalized * d.RunMps;
                }
                case "windup":
                    c.AnimForce = Anim.Windup;
                    if (c.T <= 0)
                    {
                        Vec3 fwd = c.Forward;
                        c.Vel = c.Vel + fwd * d.Num("attack", "lunge_mps", 4);
                        c.Launched = 0.25;
                        w.CreatureStrike(c.Pos + new Vec3(0, d.HeightM * 0.5, 0), fwd, d.Num("attack", "reach_m", 2),
                            d.Num("attack", "arc_deg", 70), d.Num("attack", "damage", 10) * w.DamageMult);
                        w.Event(c.Id, "strike", 0);
                        c.State = "recover";
                        c.T = 0.5;
                        c.Cd = d.Num("attack", "cooldown_s", 1.5);
                    }
                    return Vec3.Zero;
                case "recover":
                    if (c.T <= 0) c.State = "chase";
                    return Vec3.Zero;
            }
            c.State = "idle";
            return Vec3.Zero;
        }

        // ---- Reedhopper: freezes when it notices you, then the whole group bolts in hops

        static Vec3 Skittish(ICreatureWorld w, Creature c, double dt)
        {
            var d = c.Def;
            Seen target = w.NearestPlayer(c.Pos, d.SenseM);
            switch (c.State)
            {
                case "stagger":
                    c.AnimForce = Anim.Stagger;
                    if (c.T <= 0) StartFlee(w, c);
                    return Vec3.Zero;
                case "idle":
                case "wander":
                    if (target.Found)
                    {
                        c.State = "alert";
                        c.T = d.Num("brain", "alert_s", 1.2);
                        c.Yaw = YawTo(target.Pos - c.Pos);
                        return Vec3.Zero;
                    }
                    return Wander(w, c, 2, 7);
                case "alert":
                    c.AnimForce = Anim.Alert;
                    if (!target.Found)
                    {
                        if (c.T <= -2)
                        {
                            c.State = "idle";
                            c.T = 1;
                        }
                    }
                    else if (c.T <= 0 || target.Dist < d.Num("brain", "flee_within_m", 6) || w.IsSprinting(target.Id))
                    {
                        c.Threat = target.Pos;
                        w.AlarmGroup(c.Group, target.Pos);
                        StartFlee(w, c);
                    }
                    return Vec3.Zero;
                case "flee":
                {
                    if (!c.Fleeing) StartFlee(w, c);   // alarmed by a neighbour
                    Vec3 away = (c.Pos - c.Threat).Flat;
                    if (c.T <= 0 || away.Length > d.Num("brain", "flee_distance_m", 30))
                    {
                        c.State = "idle";
                        c.T = 2;
                        c.Home = c.Pos;
                        c.Fleeing = false;
                        return Vec3.Zero;
                    }
                    Vec3 dir = away.Length > 0.025 ? away.Normalized : c.Forward;
                    c.Yaw = YawTo(dir);
                    if (c.Grounded)
                    {
                        if (c.Pause > 0)
                        {
                            c.Pause -= dt;
                            c.AnimForce = Anim.Alert;
                            return Vec3.Zero;
                        }
                        // Leave the ground: one hop of the burst.
                        c.Vel = dir * d.RunMps + new Vec3(0, Math.Sqrt(2.0 * w.Gravity * d.JumpM), 0);
                        c.Hops++;
                        if (c.Hops >= (int)d.Num("brain", "hops_per_burst", 3))
                        {
                            c.Hops = 0;
                            c.Pause = d.Num("brain", "pause_between_bursts_s", 0.8);
                        }
                    }
                    c.AnimForce = Anim.Hop;
                    return dir * d.RunMps;
                }
            }
            c.State = "idle";
            return Vec3.Zero;
        }

        static void StartFlee(ICreatureWorld w, Creature c)
        {
            c.State = "flee";
            c.T = Range(w, 6, 9);
            c.Hops = 0;
            c.Pause = 0;
            c.Fleeing = true;
        }

        // ---- Glassback: grazes, warns, then charges in a straight line and pants after

        static Vec3 Defender(ICreatureWorld w, Creature c, double dt)
        {
            var d = c.Def;
            Seen target = w.NearestPlayer(c.Pos, d.Num("brain", "calm_beyond_m", 16));
            switch (c.State)
            {
                case "stagger":
                case "flipped":
                    c.AnimForce = Anim.Stagger;
                    if (c.T <= 0)
                    {
                        c.T = c.State == "flipped" ? 0.8 : 0.6;
                        c.State = "face";
                    }
                    return Vec3.Zero;
                case "idle":
                case "wander":
                    // Crowding: stand too close for too long and it has had enough.
                    if (target.Found && target.Dist < d.Num("brain", "crowd_within_m", 3))
                    {
                        c.Crowd += dt;
                        if (c.Crowd >= d.Num("brain", "crowd_provoke_s", 4)) Provoke(w, c);
                        else if (c.Crowd >= d.Num("brain", "crowd_warn_s", 2))
                        {
                            c.Yaw = LerpAngle(c.Yaw, YawTo(target.Pos - c.Pos), Clamp01(dt * 4));
                            c.AnimForce = Anim.Windup;   // head down, stamping
                            return Vec3.Zero;
                        }
                        if (c.State == "face") return Vec3.Zero;
                    }
                    else c.Crowd = Math.Max(c.Crowd - dt, 0);
                    return Wander(w, c, 2, 7);
                case "face":
                    if (!target.Found || w.WorldTime > c.CalmAt)
                    {
                        c.State = "idle";
                        c.T = 2;
                        c.Home = c.Pos;
                        c.Crowd = 0;
                        return Vec3.Zero;
                    }
                    c.Yaw = LerpAngle(c.Yaw, YawTo(target.Pos - c.Pos), Clamp01(dt * 5));
                    if (c.T <= 0 && c.Grounded)
                    {
                        c.State = "windup";
                        c.T = d.Num("attack", "windup_s", 1);
                        w.Event(c.Id, "windup", 0);
                    }
                    return Vec3.Zero;
                case "windup":
                    c.AnimForce = Anim.Windup;
                    if (target.Found) c.Yaw = LerpAngle(c.Yaw, YawTo(target.Pos - c.Pos), Clamp01(dt * 3));
                    if (c.T <= 0)
                    {
                        c.State = "charge";
                        c.T = d.Num("attack", "max_distance_m", 12) / d.Num("attack", "speed_mps", 6);
                        c.Struck.Clear();
                        c.Blocked = 0;
                        w.Event(c.Id, "strike", 0);
                    }
                    return Vec3.Zero;
                case "charge":
                {
                    c.AnimForce = Anim.Charge;
                    // It can barely steer, so a sidestep clears it.
                    if (target.Found)
                    {
                        double turn = d.Num("attack", "turn_rate_deg_s", 20) * Math.PI / 180.0 * dt;
                        c.Yaw += Math.Max(-turn, Math.Min(turn, Wrap(YawTo(target.Pos - c.Pos) - c.Yaw)));
                    }
                    Vec3 fwd = c.Forward;
                    double speed = d.Num("attack", "speed_mps", 6);
                    foreach (Seen p in w.LivingPlayers())
                    {
                        if (c.Struck.Contains(p.Id)) continue;
                        Vec3 to = p.Pos - c.Pos;
                        if (to.FlatLength < d.RadiusM + 0.55 && Math.Abs(to.Y) < 1.5 && Vec3.Dot(to.Flat, fwd) > -0.25)
                        {
                            c.Struck.Add(p.Id);
                            w.DamagePlayer(p.Id, d.Num("attack", "damage", 20) * w.DamageMult, c.Pos, "creature");
                            w.ShovePlayer(p.Id, fwd * d.Num("attack", "knockback_mps", 6), 0.35);
                        }
                    }
                    // Running into something: it breaks what is soft and is stunned either way.
                    if (c.Blocked > speed * 0.5)
                    {
                        Vec3 at = c.Pos + fwd * (d.RadiusM + 0.375) + new Vec3(0, d.HeightM * 0.5, 0);
                        bool broke = w.Carve(at, 0.55, (int)d.Num("attack", "breaks_power", 2));
                        c.State = "stun";
                        c.T = d.Num("attack", "wall_stun_s", 2.5);
                        w.Event(c.Id, broke ? "smash" : "hit", c.Hp / c.HpMax);
                        return Vec3.Zero;
                    }
                    if (c.T <= 0)
                    {
                        c.State = "recover";
                        c.T = d.Num("attack", "recover_s", 2);
                        return Vec3.Zero;
                    }
                    c.Vel = new Vec3(fwd.X * speed, c.Vel.Y, fwd.Z * speed);
                    return fwd * speed;
                }
                case "recover":
                case "stun":
                    c.AnimForce = c.State == "stun" ? Anim.Stagger : Anim.Idle;
                    if (c.T <= 0)
                    {
                        c.State = "face";
                        c.T = 0.4;
                    }
                    return Vec3.Zero;
            }
            c.State = "idle";
            return Vec3.Zero;
        }

        /// <summary>Makes a defender turn on whoever is near, and stay angry for calm_after_s.</summary>
        public static void Provoke(ICreatureWorld w, Creature c)
        {
            c.CalmAt = w.WorldTime + c.Def.Num("brain", "calm_after_s", 12);
            if (c.State == "idle" || c.State == "wander")
            {
                c.State = "face";
                c.T = 0.5;
            }
        }

        // ---- Duskmoth: drifts, gathers at open flame, and dives at whoever sits by it

        static Vec3 Swarm(ICreatureWorld w, Creature c, double dt)
        {
            var d = c.Def;
            var brain = d.Part("brain");
            double walk = d.WalkMps;
            double ground = w.GroundHeight(c.Pos.X, c.Pos.Z) + 0.25;
            double diveLo, diveHi;
            CreatureDef.Pair(brain, "dive_every_s", out diveLo, out diveHi);
            if (!c.Airborne)
            {
                double lo, hi;
                CreatureDef.Pair(d.Raw, "fly_height_m", out lo, out hi);
                c.Alt = Range(w, lo, hi);
                c.Phase = w.Random() * 2 * Math.PI;
                c.Spin = w.Random() < 0.5 ? 1 : -1;
                c.Cd = Range(w, diveLo, diveHi);
                c.Airborne = true;
            }
            if (c.State == "stagger")
            {
                c.AnimForce = Anim.Stagger;
                if (c.T <= 0) c.State = "idle";
                return new Vec3(0, -walk, 0);
            }
            if (!w.IsNight)
            {
                // Dawn: the swarm lifts away and is gone.
                if (c.State != "leave")
                {
                    c.State = "leave";
                    c.T = 5;
                }
                if (c.T <= 0) w.RemoveQuietly(c.Id);
                return DirOf(c.Phase) * walk + new Vec3(0, walk, 0);
            }
            switch (c.State)
            {
                case "dive_windup":
                    c.AnimForce = Anim.Windup;
                    if (c.T <= 0)
                    {
                        c.State = "dive";
                        c.T = 2;
                    }
                    return Vec3.Zero;
                case "dive":
                {
                    c.AnimForce = Anim.Dive;
                    Vec3? p = w.PlayerPos(c.Prey);
                    if (p == null || c.T <= 0)
                    {
                        c.State = "low";
                        c.T = d.Num("attack", "low_after_dive_s", 0.8);
                        return Vec3.Zero;
                    }
                    Vec3 to = p.Value + new Vec3(0, 1.3, 0) - c.Pos;
                    if (to.Length < 1.3)
                    {
                        MothHit(w, c, c.Prey);
                        c.State = "low";
                        c.T = d.Num("attack", "low_after_dive_s", 0.8);
                        return Vec3.Zero;
                    }
                    c.Yaw = YawTo(to);
                    c.Vel = to.Normalized * d.Num("attack", "speed_mps", 9);
                    return c.Vel;
                }
                case "low":
                    if (c.T <= 0)
                    {
                        c.State = "idle";
                        c.Cd = Range(w, diveLo, diveHi);
                    }
                    return Vec3.Zero;
            }
            // Drifting, or circling a light.
            double ward = MiniJson.Num(brain, "tether_ward_m", 8);
            Vec3? light = w.MothLight(c.Pos, MiniJson.Num(brain, "sense_light_m", 30), ward);
            Vec3 centre = c.Home;
            double radius = 6;
            if (light != null)
            {
                centre = light.Value;
                double rlo, rhi;
                CreatureDef.Pair(brain, "orbit_radius_m", out rlo, out rhi);
                double f = c.Phase - Math.Floor(c.Phase);
                radius = rlo + (rhi - rlo) * f;
                if (c.Cd <= 0 && w.CountInState("swarm", "dive", "dive_windup") < (int)MiniJson.Num(brain, "max_divers", 2))
                {
                    Seen prey = w.NearestPlayer(centre, MiniJson.Num(brain, "dive_within_m_of_light", 6));
                    if (prey.Found && !w.HasBuff(prey.Id, MiniJson.Str(brain, "ignores_buff")) && !w.NearStructure(prey.Pos, "tether", ward))
                    {
                        c.State = "dive_windup";
                        c.T = d.Num("attack", "windup_s", 0.6);
                        c.Prey = prey.Id;
                        c.Yaw = YawTo(prey.Pos - c.Pos);
                        w.Event(c.Id, "windup", 0);
                        return Vec3.Zero;
                    }
                    c.Cd = 1;
                }
            }
            c.State = "idle";
            c.Phase += dt * 0.5 * c.Spin;
            Vec3 spot = centre + new Vec3(Math.Cos(c.Phase), 0, Math.Sin(c.Phase)) * radius;
            spot.Y = Math.Max(ground, centre.Y) + c.Alt + Math.Sin(c.Phase * 3) * 0.375;
            Vec3 toSpot = spot - c.Pos;
            if (toSpot.FlatLength > 0.125) c.Yaw = LerpAngle(c.Yaw, YawTo(toSpot), Clamp01(dt * 6));
            double speed = toSpot.Length > 8 ? d.RunMps : walk;
            return toSpot.Length < 0.25 ? toSpot * (speed / 0.25) : toSpot.Normalized * speed;
        }

        static void MothHit(ICreatureWorld w, Creature c, int player)
        {
            var d = c.Def;
            w.DamagePlayer(player, d.Num("attack", "damage", 5) * w.DamageMult, c.Pos, "creature");
            // Enough scales in the face and the world blurs for a while.
            object a;
            if (!d.Part("attack").TryGetValue("applies", out a) || !(a is Dictionary<string, object>)) return;
            var applies = (Dictionary<string, object>)a;
            w.NoteHit(player, "moth", MiniJson.Num(applies, "within_s", 10), MiniJson.Int(applies, "after_hits", 3), MiniJson.Str(applies, "buff"));
        }

        // ---- Echo sentinel: a recording of a guard's drill. Throw, throw, slam, forever.

        static Vec3 Guardian(ICreatureWorld w, Creature c, double dt)
        {
            var d = c.Def;
            var brain = d.Part("brain");
            double frac = c.Hp / c.HpMax;
            bool frenzy = frac < MiniJson.Num(brain, "frenzy_below_hp", 0.25);
            if (c.State == "idle" || c.State == "wander") c.State = "dormant";
            if (c.State == "dormant")
            {
                c.AnimForce = Anim.Dormant;
                Seen near = w.NearestPlayer(c.Pos, MiniJson.Num(brain, "wake_within_m", 8));
                if (near.Found)
                {
                    c.State = "assemble";
                    c.T = MiniJson.Num(brain, "assemble_s", 2.5);
                    c.Step = 0;
                    c.Empty = 0;
                    int extra = Math.Max(w.LivingPlayers().Count - 1, 0);
                    c.HpMax = d.Hp * (1.0 + MiniJson.Num(d.Raw, "hp_per_extra_player_mult") * extra);
                    c.Hp = c.HpMax;
                    w.Event(c.Id, "wake", 1);
                }
                return Vec3.Zero;
            }
            // Everyone gone: settle back into rubble and mend.
            double leashM = MiniJson.Num(brain, "leash_m", 22);
            Seen target = w.NearestPlayer(c.Home, leashM);
            if (!target.Found)
            {
                c.Empty += dt;
                if (c.Empty >= MiniJson.Num(brain, "reset_after_empty_s", 10))
                {
                    c.State = "dormant";
                    c.Hp = c.HpMax = d.Hp;
                    c.Pos = c.Home;
                    c.Vel = Vec3.Zero;
                    w.Event(c.Id, "hit", 1);
                }
                return Vec3.Zero;
            }
            c.Empty = 0;
            Vec3 to = (target.Pos - c.Pos).Flat;
            switch (c.State)
            {
                case "assemble":
                    c.AnimForce = c.T > MiniJson.Num(brain, "assemble_s", 2.5) * 0.5 ? Anim.Dormant : Anim.Idle;
                    if (c.T <= 0)
                    {
                        c.State = "stalk";
                        c.T = 1.2;
                    }
                    return Vec3.Zero;
                case "stagger":
                case "exposed":
                    c.AnimForce = Anim.Stagger;
                    if (c.T <= 0)
                    {
                        c.State = "stalk";
                        c.T = MiniJson.Num(brain, "between_moves_s", 2) * 0.5;
                    }
                    return Vec3.Zero;
                case "stalk":
                {
                    c.Yaw = LerpAngle(c.Yaw, YawTo(to), Clamp01(dt * 4));
                    if (c.T <= 0 && c.Grounded)
                    {
                        string move = frenzy ? "slam" : PatternMove(brain, c.Step);
                        c.Step++;
                        if (move == "slam" && frac < MiniJson.Num(brain, "pull_below_hp", 0.6) && !frenzy)
                        {
                            c.State = "pull_windup";
                            c.T = d.Num("attacks", "pull", "windup_s", 1.2);
                            w.Event(c.Id, "glow", 0);
                        }
                        else
                        {
                            c.State = move + "_windup";
                            c.T = d.Num("attacks", move, "windup_s", 1);
                            w.Event(c.Id, "windup", 0);
                        }
                        return Vec3.Zero;
                    }
                    // Hold its distance: back off if crowded, close in if the player runs.
                    double keepLo, keepHi;
                    CreatureDef.Pair(brain, "keeps_distance_m", out keepLo, out keepHi);
                    double leash = (c.Pos - c.Home).FlatLength;
                    if (to.Length < keepLo && leash < leashM * 0.6) return -to.Normalized * d.WalkMps;
                    if (to.Length > keepHi && leash < leashM) return to.Normalized * d.WalkMps;
                    return Vec3.Zero;
                }
                case "throw_windup":
                    c.AnimForce = Anim.Windup;
                    c.Yaw = LerpAngle(c.Yaw, YawTo(to), Clamp01(dt * 6));
                    if (c.T <= 0)
                    {
                        Vec3 from = c.Pos + new Vec3(0, d.HeightM * 0.8, 0) + c.Forward * 1.0;
                        Vec3 aim = target.Pos + new Vec3(0, 1.2, 0) - from;
                        w.SpawnShot(c.Id, from, aim.Normalized * d.Num("attacks", "throw", "speed_mps", 10),
                            d.Num("attacks", "throw", "damage", 14) * w.DamageMult);
                        w.Event(c.Id, "strike", 0);
                        c.State = "stalk";
                        c.T = MiniJson.Num(brain, "between_moves_s", 2);
                    }
                    return Vec3.Zero;
                case "pull_windup":
                    c.AnimForce = Anim.Windup;
                    if (c.T <= 0)
                    {
                        c.State = "pull";
                        c.T = d.Num("attacks", "pull", "lasts_s", 1.5);
                        c.Tick = 0;
                    }
                    return Vec3.Zero;
                case "pull":
                    c.AnimForce = Anim.Windup;
                    c.Tick -= dt;
                    if (c.Tick <= 0)
                    {
                        c.Tick = 0.25;
                        Vec3 chest = c.Pos + new Vec3(0, d.HeightM * 0.6, 0);
                        double range = d.Num("attacks", "pull", "range_m", 10);
                        foreach (Seen p in w.LivingPlayers())
                        {
                            Vec3 pc = p.Pos + new Vec3(0, 1.2, 0);
                            Vec3 dd = chest - pc;
                            if (dd.Length < range && dd.Length > 2 && w.ClearLine(chest, pc))
                                w.ShovePlayer(p.Id, dd.Flat.Normalized * d.Num("attacks", "pull", "drag_mps", 5), 0.3);
                        }
                    }
                    if (c.T <= 0)
                    {
                        c.State = "slam_windup";
                        c.T = d.Num("attacks", "slam", "windup_s", 1.1);
                        w.Event(c.Id, "windup", 0);
                    }
                    return Vec3.Zero;
                case "slam_windup":
                    c.AnimForce = Anim.Slam;
                    if (c.T <= 0)
                    {
                        Slam(w, c);
                        c.State = "exposed";
                        c.T = frenzy ? d.Num("attacks", "slam", "frenzy_exposed_s", 3.5) : d.Num("attacks", "slam", "exposed_s", 2.5);
                        if (frenzy && c.Step % 2 == 1) c.T = 0.6;   // the first of a double slam: no rest yet
                    }
                    return Vec3.Zero;
            }
            c.State = "stalk";
            return Vec3.Zero;
        }

        static string PatternMove(Dictionary<string, object> brain, int step)
        {
            var pattern = MiniJson.List(brain, "pattern");
            if (pattern.Count == 0) return "slam";
            return pattern[step % pattern.Count] as string ?? "slam";
        }

        static void Slam(ICreatureWorld w, Creature c)
        {
            var d = c.Def;
            double radius = d.Num("attacks", "slam", "radius_m", 3.5);
            w.Event(c.Id, "slam", radius);
            foreach (Seen p in w.LivingPlayers())
            {
                Vec3 to = p.Pos - c.Pos;
                // A shockwave along the ground: anyone in the air above it is missed.
                if (to.FlatLength <= radius && to.Y < 0.6 && to.Y > -0.5)
                {
                    w.DamagePlayer(p.Id, d.Num("attacks", "slam", "damage", 26) * w.DamageMult, c.Pos, "creature");
                    w.ShovePlayer(p.Id, to.Flat.Normalized * d.Num("attacks", "slam", "knockback_mps", 6), 0.3);
                }
            }
            w.Carve(c.Pos + c.Forward * 1.6 + new Vec3(0, -0.125, 0), 0.4, (int)d.Num("attacks", "slam", "breaks_power", 2));
        }

        // ---- Being hit

        /// <summary>
        /// Damage after armour, or -1 when it cannot be hurt at all (a dormant sentinel).
        /// May hurt the attacker back (a glassback's shell). `byPlayer` is 0 for non-player damage.
        /// </summary>
        public static double Armour(ICreatureWorld w, Creature c, double amount, int byPlayer)
        {
            var d = c.Def;
            if (d.Behavior == "guardian" && (c.State == "dormant" || c.State == "assemble" || c.State == "idle" || c.State == "wander"))
                return -1;
            if (amount <= 0 || byPlayer == 0 || !d.Raw.ContainsKey("armour")) return amount;
            Vec3? from = w.PlayerPos(byPlayer);
            if (from == null) return amount;
            double side = Vec3.Dot((from.Value - c.Pos).Flat.Normalized, c.Forward);   // 1 in front, -1 behind
            switch (d.Behavior)
            {
                case "defender":
                    if (c.State == "flipped") return amount * d.Num("armour", "flipped_damage_mult", 1.5);
                    if (Math.Abs(side) > 0.6)
                    {
                        // Head-on or from behind you hit the shell; the flank is soft.
                        w.DamagePlayer(byPlayer, d.Num("armour", "shell_reflect_damage", 4), c.Pos, "condition");
                        return amount * d.Num("armour", "shell_damage_mult", 0.3);
                    }
                    break;
                case "guardian":
                    if (c.State == "exposed" || c.State == "stagger") return amount * d.Num("armour", "core_exposed_damage_mult", 1.5);
                    if (side < -0.4) return amount * d.Num("armour", "core_back_damage_mult", 1);
                    return amount * d.Num("armour", "stone_damage_mult", 0);
            }
            return amount;
        }

        /// <summary>
        /// Applies a hit after armour: health, poise and the creature's reaction. Returns true
        /// when it died (the host then drops loot and removes it).
        /// </summary>
        public static bool Hurt(ICreatureWorld w, Creature c, double amount, double poiseDamage, int byPlayer)
        {
            if (amount < 0) return false;
            c.Hp -= amount;
            c.Poise -= poiseDamage;
            if (byPlayer != 0)
            {
                Vec3? p = w.PlayerPos(byPlayer);
                if (p != null) c.Threat = p.Value;
            }
            if (c.Hp <= 0)
            {
                c.State = "dead";
                return true;
            }
            bool broke = c.Poise <= 0;
            if (broke)
            {
                c.Poise = c.Def.Poise;
                c.State = "stagger";
                c.T = 0.7;
            }
            switch (c.Def.Behavior)
            {
                case "herd":
                    if (!broke) w.AlarmGroup(c.Group, c.Threat);
                    break;
                case "hunter":
                    if (!broke && (c.State == "idle" || c.State == "wander")) c.State = "chase";
                    break;
                case "skittish":
                    w.AlarmGroup(c.Group, c.Threat);
                    if (!broke) StartFlee(w, c);
                    break;
                case "defender":
                    Provoke(w, c);
                    break;
                case "swarm":
                    if (!broke)
                    {
                        c.State = "low";
                        c.T = 0.5;
                    }
                    break;
                case "guardian":
                    if (broke) c.T = c.Def.Num("brain", "stagger_s", 3);
                    break;
            }
            w.Event(c.Id, "hit", Math.Max(c.Hp, 0) / c.HpMax);
            return false;
        }

        /// <summary>Sets a creature fleeing because its group was alarmed (the host calls this from AlarmGroup).</summary>
        public static void Alarm(ICreatureWorld w, Creature c, Vec3 threat)
        {
            if (c.State == "dead" || c.State == "stagger") return;
            c.State = "flee";
            c.T = Range(w, 5, 8);
            c.Threat = threat;
            if (c.Def.Behavior == "skittish") StartFlee(w, c);
        }

        /// <summary>A pull word took hold of it. Returns true if the creature reacted in its own way.</summary>
        public static bool OnPull(ICreatureWorld w, Creature c)
        {
            switch (c.Def.Behavior)
            {
                case "defender":
                    if (c.State != "flipped")
                    {
                        c.State = "flipped";
                        c.T = c.Def.Num("brain", "flip_s", 4);
                        c.Vel = new Vec3(c.Vel.X, 3.0, c.Vel.Z);
                        c.CalmAt = w.WorldTime + c.Def.Num("brain", "calm_after_s", 12);
                        w.Event(c.Id, "weigh", 0);
                    }
                    return true;
                case "guardian":
                    if (c.State == "slam_windup")
                    {
                        // The raised fist is dragged down: the slam never lands.
                        c.State = "stagger";
                        c.T = c.Def.Num("brain", "stagger_s", 3);
                        w.Event(c.Id, "weigh", 0);
                    }
                    return true;
            }
            return false;
        }
    }
}
