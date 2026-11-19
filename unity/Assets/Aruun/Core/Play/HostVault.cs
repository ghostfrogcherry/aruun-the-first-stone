using System;
using System.Collections.Generic;
using Aruun.Core.Combat;
using Aruun.Core.Content;
using Aruun.Core.Creatures;
using Aruun.Core.Edits;
using Aruun.Core.Inscription;
using Aruun.Core.Items;
using Aruun.Core.Language;
using Aruun.Core.Ruins;
using Aruun.Core.Voxels;

namespace Aruun.Core.Play
{
    /// <summary>
    /// The first discovery chain after the echo ruin: the sealed vault in the knoll east of it.
    /// Each step opens the next, and none of them is a key item:
    ///
    ///   ring the Ulun pick at singing quartz      it comes away whole (a plain strike shatters it)
    ///   tune the pick with quartz at a table      it now rings at the pitch of dressed stone
    ///   break the vault door while it rings       the quartz hall, Drai read a second time
    ///   speak Ahm at the socket under the ledge   a stair rises to the seam gallery, a way out opens
    ///   cut Drai beside Ulun                      seam splitting: one tier harder, whole veins
    ///   split the embersteel seams, smelt, forge  an embersteel pick that breaks the old stone anywhere
    /// </summary>
    public sealed partial class Host
    {
        /// <summary>How many singing quartz a tuning takes is in inscriptions.json; this is how many voxels of crystal give one piece.</summary>
        public const int QuartzVoxelsPerPiece = 6;
        /// <summary>How close a player must come to the quartz hall before its nightstalkers stir.</summary>
        public const double LairWakeM = 16;
        public const int LairPack = 2;

        /// <summary>The sealed vault of this world, or null.</summary>
        public SealedVault Vault { get { return Ruin != null ? Ruin.Vault : null; } }

        // ===================================================================== singing quartz

        /// <summary>
        /// Singing quartz gives nothing to a plain strike: it shatters. Struck while an Ulun
        /// inscription rings, it comes away whole.
        /// </summary>
        void HarvestQuartz(PlayerRec p, EditResult result)
        {
            if (!World.Blocks.Has("singing_quartz")) return;
            ushort quartz = World.Blocks.Id("singing_quartz");
            int n;
            if (!result.Removed.TryGetValue(quartz, out n) || n == 0) return;
            if (!Resonating(p))
            {
                bool hasUlun = false;
                for (int i = 0; i < Inventory.Size && !hasUlun; i++)
                {
                    var st = p.C.Inv[i];
                    hasUlun = st.HasValue && Array.IndexOf(st.Value.WordList, "word_sonic") >= 0;
                }
                Say(p.Id, hasUlun
                    ? "The quartz shatters into glittering dust. It hummed as you swung, the way your Ulun pick hums when it rings..."
                    : "The quartz shatters into glittering dust. It is too brittle for a plain strike.");
                Fx("shatter", p.Id, 0, n, p.Chest);
                return;
            }
            int pieces = Math.Max(1, (n + QuartzVoxelsPerPiece - 1) / QuartzVoxelsPerPiece);
            if (p.C.Inv.Add("singing_quartz", pieces) > 0) Say(p.Id, "Your bag is full");
            Say(p.Id, "The quartz sings with the pick and comes away whole: " + pieces + " singing quartz.");
        }

        // ===================================================================== tuning

        /// <summary>"" when the item in bag slot `slot` can be tuned at the table right now; otherwise why not.</summary>
        public string CanTune(int id, int slot)
        {
            PlayerRec p;
            if (!Players.TryGetValue(id, out p) || !p.Alive) return "no player";
            if (Inscriptions == null) return "no inscription rules are loaded";
            if (!AtTable(id)) return "you need to stand at an inscription table";
            var st = p.C.Inv[slot];
            if (!st.HasValue) return "pick an item first";
            Tuning t = Inscriptions.TuningFrom(st.Value.Key);
            if (t == null || !Items.Has(t.Into)) return ItemName(st.Value.Key) + " cannot be tuned";
            if (t.NeedsWord.Length > 0 && Array.IndexOf(st.Value.WordList, t.NeedsWord) < 0)
                return "it must carry " + WordName(t.NeedsWord) + " before it can be tuned";
            foreach (var kv in t.In)
                if (p.C.Inv.Count(kv.Key) < kv.Value)
                    return "tuning needs " + kv.Value + " " + ItemName(kv.Key);
            return "";
        }

        /// <summary>What the item in this slot would become if tuned, or null.</summary>
        public Tuning TuningFor(int id, int slot)
        {
            PlayerRec p;
            if (Inscriptions == null || !Players.TryGetValue(id, out p)) return null;
            var st = p.C.Inv[slot];
            return st.HasValue ? Inscriptions.TuningFrom(st.Value.Key) : null;
        }

        /// <summary>Tunes the item in bag slot `slot`: it becomes another item, keeping its words and wear. Returns "" when it worked.</summary>
        public string Tune(int id, int slot)
        {
            string why = CanTune(id, slot);
            PlayerRec p;
            Players.TryGetValue(id, out p);
            if (why.Length > 0) { if (p != null) Say(id, "Can't tune: " + why); return why; }
            var st = p.C.Inv[slot].Value;
            Tuning t = Inscriptions.TuningFrom(st.Key);
            foreach (var kv in t.In) p.C.Inv.Remove(kv.Key, kv.Value);
            p.C.Inv.Replace(slot, new ItemStack(t.Into, st.Count, st.Durability, st.Words));
            if (p.ResonantUntil > Time && slot == p.C.Selected) p.ResonantUntil = 0;
            Fx("inscribe", id, 0, st.WordList.Length, CellCentre(p.TableAt[0], p.TableAt[1], p.TableAt[2]));
            Say(id, t.Text);
            return "";
        }

        // ===================================================================== the lair

        void TickVault()
        {
            SealedVault v = Vault;
            if (v == null || WorldFlags.Contains(SealedVault.FlagWoken) || !CreatureDefs.ContainsKey("nightstalker")) return;
            foreach (var p in Players.Values)
            {
                if (!p.Alive || Vec3.Distance(p.Pos, v.HallM) > LairWakeM) continue;
                WorldFlags.Add(SealedVault.FlagWoken);
                if (!SpawnCreatures) return;
                for (int i = 0; i < LairPack; i++)
                {
                    Creature c = SpawnCreature("nightstalker", v.LairM + new Vec3(i * 2.5 - 1.25, 0, 0), 900);
                    if (c != null) c.Threat = p.Pos;
                }
                Say(p.Id, "Something moves at the far end of the hall. Eyes catch the lamplight.");
                return;
            }
        }

        // ===================================================================== what to do next

        bool CarriesWord(PlayerRec p, string word, out int slot)
        {
            for (int i = 0; i < Inventory.Size; i++)
            {
                var st = p.C.Inv[i];
                ItemDef def;
                if (st.HasValue && Items.TryGet(st.Value.Key, out def) && Inscriber.ClassOf(def) == "pick" && Array.IndexOf(st.Value.WordList, word) >= 0) { slot = i; return true; }
            }
            slot = -1;
            return false;
        }

        bool HasTunedPick(PlayerRec p)
        {
            for (int i = 0; i < Inventory.Size; i++)
            {
                var st = p.C.Inv[i];
                ItemDef def;
                if (st.HasValue && Items.TryGet(st.Value.Key, out def) && MiniJson.Str(def.Raw, "tuned_to").Length > 0) return true;
            }
            return false;
        }

        /// <summary>Has this player finished the vault's chain (forged a pick from its ore)?</summary>
        public bool VaultDone(PlayerRec p)
        {
            return Vault == null || p.C.Inv.Count("embersteel_pick") > 0;
        }

        /// <summary>The next step of the vault's chain for this player. False when there is none (no vault, or it is done).</summary>
        bool VaultObjective(PlayerRec p, ref Objective o)
        {
            SealedVault v = Vault;
            if (v == null || Inscriptions == null) return false;
            if (VaultDone(p)) return false;
            o.Done = false;
            o.Title = "The Singing Stone";
            WordKnowledge k = p.C.Lang;
            int slot;
            bool ulun = CarriesWord(p, "word_sonic", out slot), tuned = HasTunedPick(p);
            bool door = v.DoorOpen(World), lift = RuinSite.Done(World, v.Mechanism("lift"));
            Vec3 table = lift ? v.TableM : Ruin.TableM;
            if (!ulun && !tuned)
            {
                o.Text = "Cut " + WordName("word_sonic") + " into a pick at an inscription table.";
                o.Target = Ruin.TableM;
                return true;
            }
            if (!tuned && !door)
            {
                if (p.C.Inv.Count("singing_quartz") < 3)
                {
                    o.Text = "Singing quartz grows in the knoll east of the echo ruin. Make your pick ring (right click), then strike the quartz while it rings. (" + p.C.Inv.Count("singing_quartz") + " of 3)";
                    o.Target = v.QuartzM;
                    return true;
                }
                o.Text = "Take the singing quartz to an inscription table and tune your " + WordName("word_sonic") + " pick (E on the table).";
                o.Target = table;
                return true;
            }
            o.Title = "The Sealed Vault";
            if (!door)
            {
                o.Text = "Make the tuned pick ring, then break the dressed-stone door in the knoll's north face.";
                o.Target = v.DoorM;
                return true;
            }
            if (k.Of(SealedVault.WordSplit) < Familiarity.Guessed)
            {
                RuinInscription next = null;
                foreach (var i in v.Inscriptions) if (!k.SeenAt(i.Word, i.Place)) { next = i; break; }
                o.Text = k.Of(SealedVault.WordSplit) == Familiarity.Seen
                    ? "Find " + WordName(SealedVault.WordSplit) + " carved a second time, down in the quartz hall."
                    : "Read the glyph over the vault door (E), then the one in the hall below.";
                o.Target = next != null ? next.Point() : v.HallM;
                return true;
            }
            if (!lift)
            {
                o.Text = "Speak " + WordName("word_rise") + " (\"" + WordMeaning("word_rise") + "\") at the socket under the ledge, at the far end of the quartz hall.";
                o.Target = v.LiftM;
                return true;
            }
            if (!CarriesWord(p, SealedVault.WordSplit, out slot))
            {
                o.Text = "Cut " + WordName(SealedVault.WordSplit) + " into your tuned pick, beside " + WordName("word_sonic") + ", at the old worktable in the seam gallery.";
                o.Target = v.TableM;
                return true;
            }
            int ore = p.C.Inv.Count("embersteel_ore") + 3 * p.C.Inv.Count("embersteel_bar");
            if (ore < 6)
            {
                o.Text = "Make the pick ring and split the embersteel seams in the gallery walls. (" + ore + " of 6 ore)";
                o.Target = v.SeamM;
                return true;
            }
            o.Text = "At a campfire (Tab): smelt the ore into 2 embersteel bars, then forge an embersteel pick. It breaks the old stone anywhere.";
            o.Target = null;
            return true;
        }
    }
}
