using System;
using System.Collections.Generic;
using Aruun.Core.Combat;
using Aruun.Core.Content;
using Aruun.Core.Creatures;
using Aruun.Core.Edits;
using Aruun.Core.Inscription;
using Aruun.Core.Items;
using Aruun.Core.Language;
using Aruun.Core.Mining;
using Aruun.Core.Ruins;
using Aruun.Core.Voxels;

namespace Aruun.Core.Play
{
    /// <summary>
    /// Inscriptions: Avarra words cut into equipment at an inscription table, and what they do
    /// once cut. Everything here runs on the host, so in co-op the host decides; a client only
    /// asks (Inscribe, Erase, UseHeld) and is told what happened.
    ///
    /// The four kinds of inscription (see Core/Inscription):
    ///   passive        always on (Tahr: the item wears half as fast)
    ///   activated      right click with the item, costs stamina (Ulun on a pick: resonant mining;
    ///                  Ulun + Vuh on a hammer: a shockwave)
    ///   conditional    works by itself when something holds (Oshen: mends while you rest)
    ///   compositional  does nothing alone (Vuh on a hammer waits for a word to push)
    /// </summary>
    public sealed partial class Host
    {
        /// <summary>The inscription rules. Without them nothing can be inscribed.</summary>
        public InscriptionRules Inscriptions;

        /// <summary>How close a player must stay to the table they opened.</summary>
        public const double TableReachM = 4.5;

        // ===================================================================== the table

        /// <summary>E on an inscription table: the player is now working at it. The Game layer opens the inscription panel on the "table" event.</summary>
        bool OpenTable(PlayerRec p, Vec3 eye, Vec3 dir)
        {
            if (!World.Blocks.Has("inscription_table")) return false;
            VoxelHit hit = VoxelRaycast.Cast(World, eye.X * M, eye.Y * M, eye.Z * M, dir.X, dir.Y, dir.Z, ReachM * M);
            if (!hit.Hit || World.Get(hit.X, hit.Y, hit.Z) != World.Blocks.Id("inscription_table")) return false;
            p.TableAt = new[] { hit.X, hit.Y, hit.Z };
            Fx("table", p.Id, 0, 0, CellCentre(hit.X, hit.Y, hit.Z));
            if (Inscriptions == null) { Say(p.Id, "The table's channels are dry. (No inscription rules are loaded.)"); return true; }
            Say(p.Id, "An inscription table. Fine channels run across the worktop, cut to hold a word.");
            return true;
        }

        /// <summary>Is the player still at the table they opened?</summary>
        public bool AtTable(int id)
        {
            PlayerRec p;
            if (!Players.TryGetValue(id, out p) || !p.Alive || p.TableAt == null) return false;
            int[] t = p.TableAt;
            if (World.Get(t[0], t[1], t[2]) != World.Blocks.Id("inscription_table")) return false;
            return Vec3.Distance(p.Pos + new Vec3(0, 1, 0), CellCentre(t[0], t[1], t[2])) <= TableReachM;
        }

        /// <summary>
        /// "" when the player can cut `word` into the item in bag slot `slot` right now;
        /// otherwise why not, in plain words.
        /// </summary>
        public string CanInscribe(int id, int slot, string word)
        {
            PlayerRec p;
            if (!Players.TryGetValue(id, out p) || !p.Alive) return "no player";
            if (Inscriptions == null || Lexicon == null) return "no inscription rules are loaded";
            if (!AtTable(id)) return "you need to stand at an inscription table";
            var st = p.C.Inv[slot];
            if (!st.HasValue) return "pick an item first";
            ItemDef def;
            if (!Items.TryGet(st.Value.Key, out def)) return "unknown item";
            var current = new List<string>(st.Value.WordList);
            string why = Inscriber.WhyNot(Inscriptions, Lexicon, def, current, word, p.C.Lang);
            if (why.Length > 0) return why;
            foreach (var kv in Inscriptions.SlotCost(current.Count))
                if (p.C.Inv.Count(kv.Key) < kv.Value)
                    return "a second word needs " + kv.Value + " " + ItemName(kv.Key) + (kv.Value > 1 ? "s" : "") + " to bind it";
            return "";
        }

        /// <summary>
        /// Cuts a word into the item in bag slot `slot`. A guessed word that takes becomes known:
        /// the table answers it. Returns "" when it worked, otherwise why not (also said to the player).
        /// </summary>
        public string Inscribe(int id, int slot, string word)
        {
            string why = CanInscribe(id, slot, word);
            PlayerRec p;
            Players.TryGetValue(id, out p);
            if (why.Length > 0) { if (p != null) Say(id, "Can't inscribe: " + why); return why; }
            var st = p.C.Inv[slot].Value;
            ItemDef def = Items.Get(st.Key);
            string cls = Inscriber.ClassOf(def);
            var current = new List<string>(st.WordList);
            foreach (var kv in Inscriptions.SlotCost(current.Count)) p.C.Inv.Remove(kv.Key, kv.Value);
            InscriptionRule formed = Inscriber.Forms(Inscriptions, current, word, cls);
            current.Add(word);
            p.C.Inv.SetWords(slot, string.Join("+", current.ToArray()));
            Fx("inscribe", id, 0, current.Count, CellCentre(p.TableAt[0], p.TableAt[1], p.TableAt[2]));

            string name = WordName(word);
            if (!p.C.Lang.Knows(word))
            {
                p.C.Lang.Confirm(word);
                p.C.Learn(word);
                Fx("learn", id, 0, 0, p.Chest);
                Say(id, "The glyph takes. The channels carry it into the " + def.Name.ToLowerInvariant() + ", and you understand: " + name + " means \"" + WordMeaning(word) + "\".");
            }
            else Say(id, "You cut " + name + " into the " + def.Name.ToLowerInvariant() + ".");

            if (formed != null)
            {
                bool first = p.C.Combos.Add(formed.Id);
                Say(id, (first ? "Something new: " : "") + JoinNames(formed.Words) + " work together. " + formed.Name + ". " + formed.Text);
            }
            else
            {
                InscriptionRule alone = Inscriptions.Word(word);
                if (alone != null && alone.Text.Length > 0) Say(id, alone.Text);
            }
            return "";
        }

        /// <summary>Grinds the last word off the item in bag slot `slot`, at a table. Whatever bound it is lost.</summary>
        public string Erase(int id, int slot)
        {
            PlayerRec p;
            if (!Players.TryGetValue(id, out p) || !p.Alive) return "no player";
            if (!AtTable(id)) { Say(id, "You need an inscription table to grind a word away."); return "not at a table"; }
            var st = p.C.Inv[slot];
            if (!st.HasValue || !st.Value.Inscribed) { Say(id, "That carries no words."); return "no words"; }
            var words = new List<string>(st.Value.WordList);
            string gone = words[words.Count - 1];
            words.RemoveAt(words.Count - 1);
            p.C.Inv.SetWords(slot, words.Count > 0 ? string.Join("+", words.ToArray()) : null);
            if (p.ResonantUntil > Time && slot == p.C.Selected) p.ResonantUntil = 0;
            Fx("erase", id, 0, words.Count, p.Chest);
            Say(id, "You grind " + WordName(gone) + " off the " + Items.Get(st.Value.Key).Name.ToLowerInvariant() + ".");
            return "";
        }

        /// <summary>What the inscription on the item in this slot does now.</summary>
        public Resolved InscriptionOf(PlayerRec p, int slot)
        {
            if (Inscriptions == null) return new Resolved();
            return Inscriber.Resolve(Inscriptions, Items, p.C.Inv[slot]);
        }

        /// <summary>Is the held item ringing (resonant mining active)?</summary>
        public bool Resonating(PlayerRec p)
        {
            return p.ResonantUntil > Time && InscriptionOf(p, p.C.Selected).WithEffect("resonant_mining") != null;
        }

        // ===================================================================== activated

        /// <summary>Right click with an inscribed tool or weapon: sounds its activated inscription. False when it has none.</summary>
        bool ActivateHeld(PlayerRec p)
        {
            Resolved res = InscriptionOf(p, p.C.Selected);
            InscriptionRule r = res.First(InscriptionType.Activated);
            if (r == null)
            {
                if (res.Waiting.Count > 0) Say(p.Id, JoinNames(res.Waiting.ToArray()) + " is cut here, but it waits for another word.");
                return false;
            }
            if (Time < p.NextInscription) { Say(p.Id, "It is still ringing from the last time."); return false; }
            if (!p.God && !p.C.Spend(r.Stamina)) { Say(p.Id, "You are too tired to make it ring."); return false; }
            p.NextInscription = Time + r.CooldownS;
            switch (r.Effect)
            {
                case "resonant_mining":
                    p.ResonantUntil = Time + r.DurationS;
                    Fx("resonate_on", p.Id, 0, r.DurationS, p.Chest);
                    Say(p.Id, "You strike the " + Items.Get(p.C.HeldKey).Name.ToLowerInvariant() + " and it rings. Stone will part for it while it rings.");
                    return true;
                case "shockwave":
                    Shockwave(p, r);
                    return true;
            }
            return false;
        }

        /// <summary>Resonant strike: everything near you is thrown back and hurt.</summary>
        void Shockwave(PlayerRec p, InscriptionRule r)
        {
            double radius = r.Num("radius_m"), impulse = r.Num("impulse_mps"), damage = r.Num("damage");
            int hits = 0;
            foreach (var c in Creatures.ToArray())
            {
                if (c.State == "dead") continue;
                Vec3 to = c.Pos - p.Pos;
                double d = to.Length;
                if (d > radius + c.Def.RadiusM) continue;
                double falloff = 1 - 0.5 * Math.Min(1, d / Math.Max(0.1, radius));
                Vec3 away = to.Flat.Length > 0.01 ? to.Flat.Normalized : p.Facing.Flat.Normalized;
                double give = 1 / Math.Sqrt(Math.Max(1, c.Def.Mass));
                HitCreature(c, damage * falloff, damage, away * (impulse * falloff * give) + new Vec3(0, 3 * give, 0), p.Id);
                c.Threat = p.Pos;
                hits++;
            }
            if (hits > 0) p.CombatAt = Time;
            Fx("shockwave", p.Id, 0, radius, p.Pos);
            Say(p.Id, hits > 0 ? "The hammer rings and the ground answers: " + hits + " thrown back." : "The hammer rings and the ground shivers around you.");
            WearHeld(p);
        }

        // ===================================================================== digging

        /// <summary>The tool as it digs now: faster while it rings.</summary>
        ToolDef DiggingTool(PlayerRec p, BlockDef block = null)
        {
            ToolDef t = p.C.HeldTool;
            if (!Resonating(p)) return t;
            InscriptionRule r = InscriptionOf(p, p.C.Selected).WithEffect("resonant_mining");
            int power = t.Power + (int)r.Num("power_bonus");
            // A tuned tool, ringing, breaks the one stone it is tuned to, however hard.
            ItemDef held;
            if (block != null && !block.Unbreakable && Items.TryGet(p.C.HeldKey, out held) && block.Key.Length > 0 && MiniJson.Str(held.Raw, "tuned_to") == block.Key)
                power = Math.Max(power, block.Hardness);
            return new ToolDef { Key = t.Key, Name = t.Name, Class = t.Class, Power = power, Speed = t.Speed * r.Num("dig_mult", 1) };
        }

        /// <summary>
        /// Resonant mining: the stone joined to a block that just broke shatters with it, up to
        /// the inscription's limits. Only stone a pick may break, of the same kind as the block
        /// that broke, within reach of the strike. Returns how many voxels shattered.
        /// </summary>
        int Shatter(PlayerRec p, int x, int y, int z, ushort block, ToolDef tool, EditResult drops)
        {
            InscriptionRule r = InscriptionOf(p, p.C.Selected).WithEffect("resonant_mining");
            BlockDef def = World.Blocks.Get(block);
            if (r == null || block == 0 || def.Tool != ToolClass.Pick || !DigRules.CanBreak(def, tool)) return 0;
            int max = (int)r.Num("vein_max");
            double radius = r.Num("vein_radius_m") * M, r2 = radius * radius;
            // Walk outward through the joined stone (and the hole the strike just made), nearest first.
            var seen = new HashSet<long>();
            var queue = new Queue<int[]>();
            var vein = new List<int[]>();
            queue.Enqueue(new[] { x, y, z });
            seen.Add(Pack(x, y, z));
            int[][] steps = { new[] { 1, 0, 0 }, new[] { -1, 0, 0 }, new[] { 0, 1, 0 }, new[] { 0, -1, 0 }, new[] { 0, 0, 1 }, new[] { 0, 0, -1 } };
            while (queue.Count > 0 && vein.Count < max)
            {
                int[] c = queue.Dequeue();
                foreach (var s in steps)
                {
                    int nx = c[0] + s[0], ny = c[1] + s[1], nz = c[2] + s[2];
                    double dx = nx - x, dy = ny - y, dz = nz - z;
                    if (dx * dx + dy * dy + dz * dz > r2 || !seen.Add(Pack(nx, ny, nz))) continue;
                    ushort here = World.Get(nx, ny, nz);
                    if (here == block)
                    {
                        if (vein.Count >= max) break;
                        vein.Add(new[] { nx, ny, nz });
                        queue.Enqueue(new[] { nx, ny, nz });
                    }
                    else if (here == 0 && dx * dx + dy * dy + dz * dz <= (BiteRadius10 / 10.0 + 1) * (BiteRadius10 / 10.0 + 1))
                        queue.Enqueue(new[] { nx, ny, nz });   // through the fresh hole
                }
            }
            ToolClass cls = tool.Class;
            int n = 0;
            foreach (var c in vein)
            {
                EditResult e = Apply(EditOp.Set(c[0], c[1], c[2], 0, tool.Power).WithTool(cls));
                n += e.Changed;
                foreach (var kv in e.Removed)
                {
                    int had;
                    drops.Removed.TryGetValue(kv.Key, out had);
                    drops.Removed[kv.Key] = had + kv.Value;
                }
            }
            if (n > 0) Events.Add(new HostEvent { Kind = "resonate", Player = p.Id, Value = n, At = CellCentre(x, y, z), Block = block });
            return n;
        }

        static long Pack(int x, int y, int z) { return ((long)(x & 0x1FFFFF) << 42) | ((long)(y & 0x1FFFFF) << 21) | (long)(z & 0x1FFFFF); }

        // ===================================================================== passive and conditional

        /// <summary>Wears the held item once, less often when Tahr holds it fast. Says so when it breaks.</summary>
        void WearHeld(PlayerRec p)
        {
            if (!Rules.Durability) return;
            InscriptionRule f = InscriptionOf(p, p.C.Selected).WithEffect("fortify");
            if (f != null && Random() >= f.Num("wear_mult", 1)) return;
            string name = p.C.HeldKey.Length > 0 && Items.Has(p.C.HeldKey) ? Items.Get(p.C.HeldKey).Name.ToLowerInvariant() : "tool";
            if (p.C.Inv.Wear(p.C.Selected)) Say(p.Id, "Your " + name + " breaks");
        }

        /// <summary>Conditional inscriptions: Oshen mends what it is cut into while the player rests.</summary>
        void TickInscriptions(PlayerRec p, double dt)
        {
            if (Inscriptions == null) return;
            if (p.ResonantUntil > 0 && p.ResonantUntil <= Time)
            {
                p.ResonantUntil = 0;
                Fx("resonate_off", p.Id, 0, 0, p.Chest);
            }
            for (int i = 0; i < Inventory.Size; i++)
            {
                var st = p.C.Inv[i];
                if (!st.HasValue || !st.Value.Inscribed || st.Value.Durability < 0) continue;
                double rate = 0;
                foreach (var r in InscriptionOf(p, i).Active)
                {
                    if (r.Effect != "reclaim") continue;
                    bool holds = r.When == "always" || (r.When == "resting" && p.Flags.Contains("resting"));
                    if (holds) rate += r.Num("mend_per_min") / 60.0;
                }
                if (rate <= 0) continue;
                double acc;
                p.MendAcc.TryGetValue(i, out acc);
                acc += rate * dt;
                int whole = (int)acc;
                if (whole > 0)
                {
                    p.C.Inv.Mend(i, whole);
                    acc -= whole;
                }
                p.MendAcc[i] = acc;
            }
        }

        // ===================================================================== helpers

        string ItemName(string key) { return Items.Has(key) ? Items.Get(key).Name.ToLowerInvariant() : key; }

        string JoinNames(string[] words)
        {
            var names = new List<string>();
            foreach (string w in words) names.Add(WordName(w));
            return string.Join(" and ", names.ToArray());
        }

        /// <summary>The next step for someone who has woken the heart and not yet cut a word into anything.</summary>
        bool InscriptionObjective(PlayerRec p, ref Objective o)
        {
            if (Ruin == null || Inscriptions == null || !World.Blocks.Has("inscription_table")) return false;
            bool inscribed = false;
            for (int i = 0; i < Inventory.Size && !inscribed; i++) { var st = p.C.Inv[i]; inscribed = st.HasValue && st.Value.Inscribed; }
            if (inscribed) return false;
            o.Title = "The Ringing Stone";
            o.Done = false;
            Familiarity f = p.C.Lang.Of("word_sonic");
            if (f < Familiarity.Guessed)
            {
                RuinInscription next = NextUnread(p.C.Lang, "word_sonic");
                o.Text = f == Familiarity.Seen
                    ? "Find " + WordName("word_sonic") + " carved in a second place in the ruin."
                    : "An old worktable stands by the court's east wall. Read the glyph on its face (E).";
                o.Target = next != null ? next.Point() : Ruin.TableM;
                return true;
            }
            bool hasPick = false;
            for (int i = 0; i < Inventory.Size && !hasPick; i++)
            {
                var st = p.C.Inv[i];
                ItemDef def;
                hasPick = st.HasValue && Items.TryGet(st.Value.Key, out def) && Inscriber.ClassOf(def) == "pick";
            }
            o.Text = hasPick
                ? "Use the inscription table (E) and cut " + WordName("word_sonic") + " into your pick."
                : "Make a flint pick (Tab), then cut " + WordName("word_sonic") + " into it at the inscription table.";
            o.Target = Ruin.TableM;
            return true;
        }
    }
}
