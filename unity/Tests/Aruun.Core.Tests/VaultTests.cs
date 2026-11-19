using System;
using System.Collections.Generic;
using System.IO;
using Aruun.Core.Combat;
using Aruun.Core.Creatures;
using Aruun.Core.Farming;
using Aruun.Core.Inscription;
using Aruun.Core.Items;
using Aruun.Core.Language;
using Aruun.Core.Play;
using Aruun.Core.Ruins;
using Aruun.Core.Survival;
using Aruun.Core.Voxels;
using NUnit.Framework;

namespace Aruun.Core.Tests
{
    /// <summary>
    /// The sealed vault: the first discovery chain after the echo ruin, played through the Host
    /// on the real map from singing quartz to an embersteel pick, the way a player would.
    /// </summary>
    public class VaultTests
    {
        const int Seed = 20261116;

        static string Read(params string[] path) { return File.ReadAllText(Path.Combine(TestContent.Root, Path.Combine(path))); }

        static Host NewHost(out PlayerRec p)
        {
            var blocks = TestContent.Blocks();
            var world = new VoxelWorld(Seed, blocks, TestContent.Map());
            var items = ItemRegistry.FromJson(Read("items", "items.json"), blocks);
            var rules = Ruleset.Load(Read("survival", "rulesets.json"), "light");
            var host = new Host(world, items, rules, Plants.FromJson(Read("farming", "plants.json")), CreatureDef.FromJson(Read("creatures", "creatures.json")), 1) { SpawnCreatures = false };
            host.Lexicon = Lexicon.FromJson(Read("language", "lexicon.json"));
            host.Inscriptions = InscriptionRules.FromJson(Read("language", "inscriptions.json"));
            Assert.That(host.Vault, Is.Not.Null, "the map world has a sealed vault");
            p = host.Join(1, new CharacterState(items, rules), host.Vault.FrontM, true);
            p.God = true;   // no stamina or hunger in the way of the chain itself
            return host;
        }

        static string Last(Host h) { return h.Messages.Count > 0 ? h.Messages[h.Messages.Count - 1].Value : ""; }
        static bool AnyMessage(Host h, string part) { return h.Messages.Exists(m => m.Value.Contains(part)); }
        static int[] Cell(Vec3 m) { return new[] { (int)Math.Floor(m.X * 4), (int)Math.Floor(m.Y * 4), (int)Math.Floor(m.Z * 4) }; }
        static bool Solid(Host h, int x, int y, int z) { return h.World.Blocks.Get(h.World.Get(x, y, z)).Collision; }

        static int SlotOf(PlayerRec p, string key)
        {
            for (int i = 0; i < Inventory.Size; i++) if (p.C.Inv.KeyAt(i) == key) return i;
            return -1;
        }

        static void Ring(Host h, PlayerRec p)
        {
            h.Time += 5;
            Assert.That(h.UseHeld(p.Id, false, 0, 0, 0, 0, 0, 0), Is.True, Last(h));
        }

        static DigTick DigOut(Host h, PlayerRec p, int x, int y, int z)
        {
            DigTick t = default;
            for (int i = 0; i < 400 && !t.Broken && !t.Refused; i++) { t = h.Dig(p.Id, x, y, z, 0.05); h.Time += 0.05; }
            return t;
        }

        /// <summary>Digs every quartz voxel near this point, ringing first when asked.</summary>
        static void DigQuartzAt(Host h, PlayerRec p, Vec3 at, bool ring)
        {
            ushort q = h.World.Blocks.Id("singing_quartz");
            int[] c = Cell(at);
            p.Pos = at + new Vec3(0, -1, 0);
            for (int x = c[0] - 3; x <= c[0] + 3; x++)
                for (int y = c[1] - 3; y <= c[1] + 3; y++)
                    for (int z = c[2] - 3; z <= c[2] + 3; z++)
                        if (h.World.Get(x, y, z) == q)
                        {
                            if (ring) Ring(h, p);
                            DigOut(h, p, x, y, z);
                        }
        }

        /// <summary>Stands beside a table worktop and presses E on it.</summary>
        static void UseTable(Host h, PlayerRec p, Vec3 table, double floorY)
        {
            p.Pos = new Vec3(table.X - 1.6, floorY, table.Z);
            Vec3 eye = p.Pos + new Vec3(0, 1.6, 0);
            Assert.That(h.Interact(p.Id, eye, (table - new Vec3(0, 0.1, 0)) - eye), Is.True, Last(h));
            Assert.That(h.AtTable(p.Id), Is.True, "standing at the table");
        }

        [Test]
        public void TheVaultIsAKnollEastOfTheRuinWithASealedDoor()
        {
            PlayerRec p;
            Host h = NewHost(out p);
            SealedVault v = h.Vault;
            Assert.That(v.Ox, Is.GreaterThan(h.Ruin.Ox + 200), "east of the echo ruin");
            Assert.That(Vec3.Distance(v.FrontM, Vec3.Zero), Is.GreaterThan(60), "outside the village");
            Assert.That(v.DoorOpen(h.World), Is.False, "the door starts sealed");
            int[] door = Cell(v.DoorM);
            Assert.That(h.World.Get(door[0], door[1], door[2] - 1), Is.EqualTo(h.World.Blocks.Id("dressed_stone")));
            Assert.That(v.Quartz.Count, Is.GreaterThanOrEqualTo(5));
            ushort q = h.World.Blocks.Id("singing_quartz");
            int[] qc = Cell(v.Quartz[0]);
            Assert.That(h.World.Get(qc[0], qc[1], qc[2]), Is.EqualTo(q), "quartz grows where the vault says");
            int[] hall = Cell(v.HallM);
            Assert.That(Solid(h, hall[0], hall[1] + 2, hall[2]), Is.False, "the hall is hollow");
            Assert.That(Solid(h, hall[0], hall[1] - 1, hall[2]), Is.True, "and has a floor");
            Assert.That(RuinSite.Done(h.World, v.Mechanism("lift")), Is.False, "the lift starts down");
            // The gallery is under the ground: it can't be reached from the surface before the lift.
            int[] g = Cell(v.GalleryM);
            int ceiling = (v.GalleryY + 17);
            int ground = int.MaxValue;
            for (int x = g[0] - 30; x <= g[0] + 30; x += 2)
                for (int z = g[2] - 25; z <= g[2] + 35; z += 2)
                    ground = Math.Min(ground, h.World.Generator.Field.HeightAt(x, z));
            TestContext.WriteLine("vault at " + v.Ox + "," + v.Oz + " ground " + v.Ground + ", gallery ceiling " + ceiling + " under ground " + ground);
            Assert.That(ceiling, Is.LessThanOrEqualTo(ground + 1));
        }

        [Test]
        public void TheWholeChainFromQuartzToEmbersteel()
        {
            PlayerRec p;
            Host h = NewHost(out p);
            SealedVault v = h.Vault;
            h.WorldFlags.Add(EchoRuin.FlagRemembered);   // the echo ruin is behind us
            p.C.Inv.Add("flint_pick", 1, 260, "word_sonic");
            p.C.Lang.Teach("word_sonic");
            p.C.Lang.Teach("word_rise");
            int pick = SlotOf(p, "flint_pick");
            p.C.Selected = pick;

            // 1. A plain strike shatters the quartz to nothing.
            DigQuartzAt(h, p, v.Quartz[1], false);
            Assert.That(p.C.Inv.Count("singing_quartz"), Is.EqualTo(0));
            Assert.That(AnyMessage(h, "shatters"), Is.True);

            // 2. Rung, it comes away whole.
            DigQuartzAt(h, p, v.Quartz[0], true);
            DigQuartzAt(h, p, v.Quartz[2], true);
            Assert.That(p.C.Inv.Count("singing_quartz"), Is.GreaterThanOrEqualTo(3), "rung quartz comes away whole");

            // 3. The door refuses an untuned pick, even ringing, and says what might.
            int[] door = Cell(v.DoorM);
            int dz = door[2] - 1;
            p.Pos = v.FrontM;
            Ring(h, p);
            DigTick refused = DigOut(h, p, door[0], door[1], dz);
            Assert.That(refused.Refused, Is.True);
            Assert.That(refused.Message, Does.Contain("tuned"));

            // 4. Tune it at the echo ruin's table: the pick becomes a tuned pick, keeping Ulun.
            int quartz = p.C.Inv.Count("singing_quartz");
            UseTable(h, p, h.Ruin.TableM, h.Ruin.CourtM.Y);
            Assert.That(h.CanTune(1, pick), Is.Empty);
            Assert.That(h.Tune(1, pick), Is.Empty, Last(h));
            Assert.That(p.C.Inv.KeyAt(pick), Is.EqualTo("tuned_pick"));
            Assert.That(p.C.Inv[pick].Value.Words, Is.EqualTo("word_sonic"));
            Assert.That(p.C.Inv.Count("singing_quartz"), Is.EqualTo(quartz - 3), "the tuning used up the quartz");

            // 5. Rung, the tuned pick breaks the dressed stone.
            p.Pos = v.FrontM;
            ushort dressed = h.World.Blocks.Id("dressed_stone");
            for (int round = 0; round < 40 && !v.DoorOpen(h.World); round++)
            {
                Ring(h, p);
                bool hit = false;
                for (int y = v.Ground + 1; y <= v.Ground + 14 && !hit; y++)
                    for (int x = door[0] - 5; x <= door[0] + 4 && !hit; x++)
                        for (int z = dz - 1; z <= dz && !hit; z++)
                            if (h.World.Get(x, y, z) == dressed) { Assert.That(DigOut(h, p, x, y, z).Broken, Is.True, Last(h)); hit = true; }
                if (!hit) break;
            }
            Assert.That(v.DoorOpen(h.World), Is.True, "the door is broken through");
            Assert.That(h.ObjectiveFor(1).Text, Does.Contain("Read the glyph"));

            // 6. Drai on the lintel and in the hall: a guess.
            foreach (var ins in v.Inscriptions) h.Read(p, ins);
            Assert.That(p.C.Lang.Of(SealedVault.WordSplit), Is.EqualTo(Familiarity.Guessed));

            // 7. Ahm at the socket: the stair rises, the way out opens, a word fragment drops.
            p.Pos = v.HallM;
            int fragments = p.C.Inv.Count("word_fragment");
            Vec3 eye = p.Pos + new Vec3(0, 1.6, 0);
            h.Time += 5;
            Assert.That(h.Speak(1, new List<string> { "word_rise" }, eye, v.LiftM - eye), Is.True, Last(h));
            Assert.That(RuinSite.Done(h.World, v.Mechanism("lift")), Is.True);
            Assert.That(p.C.Inv.Count("word_fragment"), Is.EqualTo(fragments + 1));
            Assert.That(h.ObjectiveFor(1).Text, Does.Contain("old worktable"));

            // 8. Cut Drai beside Ulun at the gallery's table: seam splitting.
            UseTable(h, p, v.TableM, v.GalleryY / 4.0);
            Assert.That(h.Inscribe(1, pick, SealedVault.WordSplit), Is.Empty, Last(h));
            Assert.That(p.C.Combos, Does.Contain("seam_splitting"));

            // 9. Flint scratches embersteel; split along its seams, it comes away.
            int[] seam = Cell(v.SeamM);
            ushort ore = h.World.Blocks.Id("embersteel_ore");
            int sx = seam[0] - 1;
            Assert.That(h.World.Get(sx, seam[1], seam[2]), Is.EqualTo(ore), "embersteel in the gallery wall");
            p.Pos = new Vec3((sx + 2.5) / 4.0, v.GalleryY / 4.0, seam[2] / 4.0);
            h.Time += 30;   // let the last ring fade
            Assert.That(DigOut(h, p, sx, seam[1], seam[2]).Refused, Is.True, "too hard for flint without its seams split");
            for (int i = 0; i < 6 && p.C.Inv.Count("embersteel_ore") < 6; i++)
            {
                Ring(h, p);
                bool hit = false;
                for (int y = seam[1] - 3; y <= seam[1] + 5 && !hit; y++)
                    for (int z = seam[2] - 6; z <= seam[2] + 6 && !hit; z++)
                        for (int x = sx - 1; x <= sx && !hit; x++)
                            if (h.World.Get(x, y, z) == ore) { Assert.That(DigOut(h, p, x, y, z).Broken, Is.True, Last(h)); hit = true; }
            }
            Assert.That(p.C.Inv.Count("embersteel_ore"), Is.GreaterThanOrEqualTo(6));

            // 10. Smelt and forge at a fire: the pick that breaks the old stone anywhere.
            h.Structures.Add(new StructureRec { Id = 99, Key = "campfire", Pos = p.Pos + new Vec3(1, 0, 0) });
            p.C.Inv.Add("timber", 2);
            p.C.Inv.Add("cord", 1);
            Assert.That(h.Craft(1, "embersteel_bar"), Is.Empty);
            Assert.That(h.Craft(1, "embersteel_bar"), Is.Empty);
            Assert.That(h.Craft(1, "embersteel_pick"), Is.Empty);
            Assert.That(h.VaultDone(p), Is.True);
            Assert.That(h.Items.Tools.Get("embersteel_pick").Power, Is.GreaterThanOrEqualTo(h.World.Blocks.Get(h.World.Blocks.Id("ancient_stone")).Hardness));
        }

        [Test]
        public void NightstalkersWakeInTheQuartzHall()
        {
            PlayerRec p;
            Host h = NewHost(out p);
            h.SpawnCreatures = true;
            p.Pos = h.Vault.HallM;
            for (int i = 0; i < 30; i++) h.Tick(0.1);
            Assert.That(h.WorldFlags, Does.Contain(SealedVault.FlagWoken));
            Assert.That(h.Creatures.FindAll(c => c.Def.Key == "nightstalker").Count, Is.GreaterThanOrEqualTo(Host.LairPack));
            for (int i = 0; i < 30; i++) h.Tick(0.1);
            Assert.That(h.Creatures.FindAll(c => c.Def.Key == "nightstalker" && Vec3.Distance(c.Pos, h.Vault.LairM) < 12).Count, Is.EqualTo(Host.LairPack), "they wake once");
        }
    }
}
