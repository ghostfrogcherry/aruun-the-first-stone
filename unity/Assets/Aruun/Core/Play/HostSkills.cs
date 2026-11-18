using System;
using System.Collections.Generic;
using Aruun.Core.Combat;
using Aruun.Core.Content;
using Aruun.Core.Items;
using Aruun.Core.Net;
using Aruun.Core.Skills;

namespace Aruun.Core.Play
{
    /// <summary>The Graviturge's skill tree and the Fallhammer's slam.</summary>
    public sealed partial class Host
    {
        /// <summary>The demo's discipline tree (content/skills/graviturge.json). Null leaves skills off.</summary>
        public SkillTree Tree;

        /// <summary>Gives a joining character the tree, its start points, and back-pay for a save made before trees existed.</summary>
        void SetUpSkills(PlayerRec p, bool fresh)
        {
            p.C.Tree = Tree;
            if (Tree == null || Link != null) return;
            if (fresh) { p.C.SkillPoints = Tree.StartPoints; p.C.NewPoints = 0; return; }
            if (p.C.HasSkillSave) return;
            int done = 0;
            foreach (var kv in p.C.Quests.All) if (kv.Value == Aruun.Core.People.QuestLog.Done) done++;
            p.C.SkillPoints = Tree.StartPoints + p.C.Words.Count * Tree.Award("word") + done * Tree.Award("quest");
            p.C.HasSkillSave = true;
        }

        /// <summary>Learns a node of the tree. Returns "" or why not (also said to the player).</summary>
        public string LearnSkill(int id, string node)
        {
            if (Link != null) { Link.Send("skill", Wire.Clean(node)); return ""; }
            PlayerRec p;
            if (!Players.TryGetValue(id, out p)) return "no player";
            if (Tree == null) return "no skill tree";
            string why = Tree.Learn(p.C, node);
            if (why.Length > 0) { Say(id, "Can't learn that: " + why + "."); return why; }
            Fx("skill", id, 0, 0, p.Chest);
            Say(id, "You learn " + Tree.Get(node).Name + ". " + Tree.Get(node).Text);
            return "";
        }

        /// <summary>Tells the player about points they just earned.</summary>
        void AnnouncePoints(PlayerRec p)
        {
            if (p.C.NewPoints <= 0) return;
            int n = p.C.NewPoints;
            p.C.NewPoints = 0;
            Fx("skill_point", p.Id, 0, n, p.Chest);
            Say(p.Id, "Your sense of down grows. " + (n == 1 ? "A skill point" : n + " skill points") + " to spend (P: skills).");
        }

        void AwardFor(PlayerRec p, string why)
        {
            if (Tree != null) p.C.Award(Tree.Award(why));
        }

        /// <summary>
        /// A heavy blow with a slamming weapon (the Fallhammer) also lands as a shockwave: every
        /// creature near the point of impact is struck and thrown outward. Skills widen and deepen it.
        /// </summary>
        void Slam(PlayerRec p, AttackProfile prof)
        {
            ItemDef def;
            if (!Items.TryGet(p.C.HeldKey, out def)) return;
            var slam = MiniJson.Obj(def.Raw, "slam");
            if (slam.Count == 0) return;
            double radius = MiniJson.Num(slam, "radius", 2) * p.C.Mods["slam_radius_mult"];
            Vec3 at = p.Pos + p.Facing.Flat.Normalized * (prof.Reach * 0.7);
            double dmg = prof.Damage * 0.5 * p.C.Mods["slam_damage_mult"];
            double debt = p.C.Mods["slam_debt"];
            if (debt > 0 && !p.God) p.C.Owe(debt);
            Fx("slam", p.Id, 0, radius, at);
            foreach (var c in Creatures.ToArray())
            {
                if (c.State == "dead") continue;
                double d = (c.Pos - at).FlatLength - c.Def.RadiusM;
                if (d > radius || Math.Abs(c.Pos.Y - at.Y) > radius) continue;
                double falloff = 1.0 - Math.Max(0, d) / (radius * 2);
                HitCreature(c, dmg * falloff, prof.Poise * 0.5, CombatRules.Knockback(at, c.Pos, prof.Knockback * 1.2, c.Def.Mass), p.Id);
            }
        }
    }
}
