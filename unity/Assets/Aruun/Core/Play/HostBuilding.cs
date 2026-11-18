using System;
using System.Collections.Generic;
using Aruun.Core.Building;
using Aruun.Core.Combat;
using Aruun.Core.Edits;
using Aruun.Core.Items;
using Aruun.Core.Net;

namespace Aruun.Core.Play
{
    /// <summary>Shape building: the host checks a shape, takes the blocks it fills, and builds it.</summary>
    public sealed partial class Host
    {
        /// <summary>How far from the player either corner of a shape may be, in metres.</summary>
        public const double BuildReachM = 12;

        /// <summary>Empty cells the shape would fill: what it costs in the held block.</summary>
        public int ShapeCost(Shape s, int ax, int ay, int az, int bx, int by, int bz, ushort block)
        {
            int n = 0;
            foreach (long k in Shapes.Cells(Shapes.Ops(s, ax, ay, az, bx, by, bz, block)))
            {
                int x, y, z;
                Shapes.Unkey(k, out x, out y, out z);
                ushort old = World.Get(x, y, z);
                if (old != block && !World.Blocks.Get(old).Collision) n++;
            }
            return n;
        }

        /// <summary>
        /// Builds a shape from corner a to corner b in the held block. Fills only empty space,
        /// pays one block per voxel filled, and refuses (saying why) when the bag is short, a
        /// corner is out of reach, or someone stands inside. Returns "" or the reason.
        /// </summary>
        public string BuildShape(int id, Shape s, int ax, int ay, int az, int bx, int by, int bz)
        {
            if (Link != null) { Link.Send("build", Wire.Join((int)s, ax, ay, az, bx, by, bz)); return ""; }
            PlayerRec p;
            if (!Players.TryGetValue(id, out p) || !p.Alive) return "no player";
            if (!Enum.IsDefined(typeof(Shape), s)) return "no such shape";
            string why = BuildShapeWhy(p, s, ax, ay, az, bx, by, bz);
            if (why.Length > 0) Say(id, "Can't build that: " + why + ".");
            return why;
        }

        string BuildShapeWhy(PlayerRec p, Shape s, int ax, int ay, int az, int bx, int by, int bz)
        {
            ItemDef def;
            string key = p.C.HeldKey;
            if (key.Length == 0 || !Items.TryGet(key, out def) || def.Kind != "block") return "hold a block to build with";
            ushort block = World.Blocks.Id(def.Block);
            if (block == 0) return "that can't be built with";
            Shapes.Snap(s, ax, ay, az, ref bx, ref by, ref bz);
            Vec3 eye = p.Pos + new Vec3(0, 1.6, 0);
            if (Vec3.Distance(eye, CellCentre(ax, ay, az)) > BuildReachM || Vec3.Distance(eye, CellCentre(bx, by, bz)) > BuildReachM)
                return "a corner is too far away";
            List<EditOp> ops = Shapes.Ops(s, ax, ay, az, bx, by, bz, block);
            int cost = 0;
            foreach (long k in Shapes.Cells(ops))
            {
                int x, y, z;
                Shapes.Unkey(k, out x, out y, out z);
                ushort old = World.Get(x, y, z);
                if (old == block || World.Blocks.Get(old).Collision) continue;
                foreach (var other in Players.Values)
                    if (other.Alive && InBody(other.Pos, 0.3, 1.75, x, y, z)) return "someone is standing in the way";
                cost++;
            }
            if (cost == 0) return "there is no empty space there";
            int have = p.C.Inv.Count(key);
            if (have < cost) return "it needs " + cost + " " + def.Name.ToLowerInvariant() + " and you have " + have;
            int built = 0;
            foreach (var op in ops) built += Apply(op).Changed;
            if (built > 0) p.C.Inv.Remove(key, Math.Min(built, have));
            Fx("build", p.Id, 0, built, CellCentre(bx, by, bz));
            Say(p.Id, "Built: " + Shapes.Name(s).ToLowerInvariant() + ", " + built + " " + def.Name.ToLowerInvariant() + ".");
            return "";
        }

        /// <summary>World flag set when a Tether stands on the old steading's plinth.</summary>
        public const string SteadingFlag = "steading_claimed";

        /// <summary>A Tether set on the steading's plinth wakes the carving and claims the place.</summary>
        void ClaimSteading(PlayerRec p, Vec3 at)
        {
            if (Village == null || WorldFlags.Contains(SteadingFlag)) return;
            Vec3 plinth = Village.SteadingPlinth;
            if (Vec3.FlatDistance(at, plinth) > 0.6 || Math.Abs(at.Y - plinth.Y) > 0.4) return;
            WorldFlags.Add(SteadingFlag);
            Fx("glow", p.Id, 0, 0, plinth);
            Say(p.Id, "The carving round the plinth answers the Tether. The old steading is yours: mend its walls with shape building (C).");
            AdvanceQuestFlags();
        }
    }
}
