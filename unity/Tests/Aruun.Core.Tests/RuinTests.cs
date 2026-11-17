using System.Collections.Generic;
using System.IO;
using Aruun.Core.Combat;
using Aruun.Core.Creatures;
using Aruun.Core.Items;
using Aruun.Core.Language;
using Aruun.Core.Play;
using Aruun.Core.Ruins;
using Aruun.Core.Survival;
using Aruun.Core.Farming;
using Aruun.Core.Voxels;
using NUnit.Framework;

namespace Aruun.Core.Tests
{
    /// <summary>
    /// "The World Remembers", played through the Host on the real map: find the echo ruin,
    /// read its words, open the gate, raise the stair, fell the sentinel, wake the heart.
    /// </summary>
    public class RuinTests
    {
        const int Seed = 20261116;

        static Host NewHost(out PlayerRec p, out EchoRuin ruin)
        {
            var blocks = TestContent.Blocks();
            var world = new VoxelWorld(Seed, blocks, TestContent.Map());
            var items = ItemRegistry.FromJson(File.ReadAllText(Path.Combine(TestContent.Root, "items", "items.json")), blocks);
            var rules = Ruleset.Load(File.ReadAllText(Path.Combine(TestContent.Root, "survival", "rulesets.json")), "light");
            var plants = Plants.FromJson(File.ReadAllText(Path.Combine(TestContent.Root, "farming", "plants.json")));
            var creatures = CreatureDef.FromJson(File.ReadAllText(Path.Combine(TestContent.Root, "creatures", "creatures.json")));
            var host = new Host(world, items, rules, plants, creatures, 1) { SpawnCreatures = false };
            host.Lexicon = Lexicon.FromJson(File.ReadAllText(Path.Combine(TestContent.Root, "language", "lexicon.json")));
            ruin = host.Ruin;
            Assert.That(ruin, Is.Not.Null, "the map world has an echo ruin");
            p = host.Join(1, new CharacterState(items, rules), ruin.EntranceM, true);
            host.SpawnPoint = ruin.EntranceM;
            return host;
        }

        static Vec3 Eye(PlayerRec p) { return p.Pos + new Vec3(0, 1.6, 0); }

        /// <summary>Stands at `feet` and speaks one word toward `at`.</summary>
        static bool SpeakAt(Host h, PlayerRec p, Vec3 feet, Vec3 at, string word)
        {
            p.Pos = feet;
            h.Time += 5;   // past any cooldown
            return h.Speak(p.Id, new List<string> { word }, Eye(p), at - Eye(p));
        }

        static RuinInscription Ins(EchoRuin r, string place)
        {
            foreach (var i in r.Inscriptions) if (i.Place == place) return i;
            Assert.Fail("no inscription at " + place);
            return null;
        }

        static string LastMessage(Host h) { return h.Messages.Count > 0 ? h.Messages[h.Messages.Count - 1].Value : ""; }

        static bool Solid(Host h, int x, int y, int z) { return h.World.Blocks.Get(h.World.Get(x, y, z)).Collision; }

        [Test]
        public void TheRuinIsSouthOfTheConfluenceWithAWayDownAndAWayOut()
        {
            var h = NewHost(out var p, out var r);
            Assert.That(r.Oz, Is.LessThan(-300), "south of the spawn place");
            // The stair down: every step open with headroom, on a solid tread.
            for (int k = 0; k < EchoRuin.StepsDown; k++)
            {
                int y = r.Ground - k, z = r.Oz - (8 + 2 * k);
                Assert.That(Solid(h, r.Ox, y - 1, z), Is.True, "tread of step " + k);
                for (int dy = 0; dy < 8; dy++) Assert.That(Solid(h, r.Ox, y + dy, z), Is.False, "headroom of step " + k);
            }
            // The arch is open toward the north, where you come from.
            for (int dz = 1; dz <= 4; dz++) Assert.That(Solid(h, r.Ox, r.Ground + 2, r.Oz + dz), Is.False, "arch open to the north");
            // Sealed until spoken to.
            foreach (var m in r.Mechanisms) Assert.That(EchoRuin.Done(h.World, m), Is.False, m.Id + " starts closed");
            // The way out comes up to open air.
            Vec3 exit = r.ExitM;
            int ex = (int)System.Math.Floor(exit.X * 4), ey = (int)System.Math.Round(exit.Y * 4), ez = (int)System.Math.Floor(exit.Z * 4);
            Assert.That(Solid(h, ex, ey - 1, ez), Is.True, "exit floor");
            Assert.That(Solid(h, ex, ey + 1, ez), Is.False, "exit open");
            Assert.That(Solid(h, ex, ey + 1, ez - 3), Is.False, "the exit has no end wall");
            Assert.That(ey, Is.GreaterThanOrEqualTo(h.World.Generator.Field.HeightAt(ex, ez - 3) - 2), "the exit reaches the surface");
        }

        [Test]
        public void ReadingAWordTwiceInTwoPlacesLetsYouGuessIt()
        {
            var h = NewHost(out var p, out var r);
            var arch = Ins(r, "ruin_arch");
            Assert.That(arch.Word, Is.EqualTo("word_push"));
            // Read the arch through Interact, standing in the porch looking at the pillar.
            Vec3 at = arch.Point();
            Vec3 eye = at + new Vec3(1.5, 0, 0);
            Assert.That(h.Interact(1, eye, new Vec3(-1, 0, 0)), Is.True);
            Assert.That(p.C.Lang.Of("word_push"), Is.EqualTo(Familiarity.Seen));
            Assert.That(LastMessage(h), Does.Contain("Vuh"));
            // Once seen it can't be spoken.
            Assert.That(SpeakAt(h, p, r.GateM, r.Mechanism("gate").Point, "word_push"), Is.False);
            Assert.That(LastMessage(h), Does.Contain("only once"));
            // Reading the same place again teaches nothing.
            h.Read(p, arch);
            Assert.That(p.C.Lang.Of("word_push"), Is.EqualTo(Familiarity.Seen));
            h.Read(p, Ins(r, "ruin_antechamber_left"));
            Assert.That(p.C.Lang.Of("word_push"), Is.EqualTo(Familiarity.Guessed));
            Assert.That(LastMessage(h), Does.Contain("Away"));
        }

        [Test]
        public void TheWholeRuinFromGateToHeart()
        {
            var h = NewHost(out var p, out var r);
            var obj = h.ObjectiveFor(1);
            Assert.That(obj.Text, Does.Contain("Read"), "at the arch the quest says to read");

            // Gate: Vuh, read twice, spoken at the seal.
            h.Read(p, Ins(r, "ruin_arch"));
            h.Read(p, Ins(r, "ruin_antechamber_left"));
            Assert.That(h.ObjectiveFor(1).Text, Does.Contain("sealed gate"));
            var gate = r.Mechanism("gate");
            Assert.That(SpeakAt(h, p, r.GateM, gate.Point, "word_push"), Is.True, LastMessage(h));
            Assert.That(EchoRuin.Done(h.World, gate), Is.True);
            Assert.That(p.C.Lang.Knows("word_push"), Is.True, "the word answered, so now you know it");
            Assert.That(p.C.Words, Does.Contain("word_push"));
            Assert.That(h.Edits.Count, Is.GreaterThan(0), "the opening is journaled");

            // Stair: Ahm, read twice, spoken at the socket.
            h.Read(p, Ins(r, "ruin_antechamber_right"));
            h.Read(p, Ins(r, "ruin_step_hall"));
            var stair = r.Mechanism("stair");
            Assert.That(SpeakAt(h, p, r.StepM, stair.Point, "word_rise"), Is.True, LastMessage(h));
            Assert.That(EchoRuin.Done(h.World, stair), Is.True);
            Assert.That(p.C.Lang.Knows("word_rise"), Is.True);
            // The top step sits right under the ledge's lip.
            Assert.That(Solid(h, r.Ox, r.FloorY + 14, r.Oz - 162), Is.True);
            Assert.That(Solid(h, r.Ox, r.FloorY + 15, r.Oz - 162), Is.False);

            // Court: the sentinel appears when you come near.
            Assert.That(h.ObjectiveFor(1).Text, Does.Contain("Sentinel"));
            p.Pos = r.CourtM + new Vec3(0, 0, 6);
            for (int i = 0; i < 12; i++) h.Tick(0.05);
            Creature s = h.Sentinel;
            Assert.That(s, Is.Not.Null, "the sentinel takes its place");
            Assert.That(s.State, Is.EqualTo("dormant").Or.EqualTo("assemble"), "it starts as rubble");

            // The heart will not answer while it stands.
            h.Read(p, Ins(r, "ruin_court_west"));
            h.Read(p, Ins(r, "ruin_court_east"));
            var heart = r.Mechanism("heart");
            Vec3 below = r.CourtM + new Vec3(0, 0, -6);   // a few metres short of the far wall
            Assert.That(SpeakAt(h, p, below, heart.Point, "word_pull"), Is.False);
            Assert.That(LastMessage(h), Does.Contain("guardian"));
            Assert.That(EchoRuin.Done(h.World, heart), Is.False);

            // Fell it (its core is open after a stagger).
            s.State = "exposed";
            s.T = 3;
            h.HitCreature(s, 100000, 0, Vec3.Zero, 1);
            Assert.That(h.WorldFlags, Does.Contain(EchoRuin.FlagSentinel));
            Assert.That(h.ObjectiveFor(1).Text, Does.Contain("Heart"));

            // Now Sem wakes the heart, the way out opens, and the world remembers.
            Assert.That(SpeakAt(h, p, below, heart.Point, "word_pull"), Is.True, LastMessage(h));
            Assert.That(EchoRuin.Done(h.World, heart), Is.True);
            Assert.That(h.WorldFlags, Does.Contain(EchoRuin.FlagRemembered));
            Assert.That(Solid(h, r.Ox, r.CourtY + 1, r.Oz - 281), Is.False, "the seal behind the heart is gone");
            Assert.That(p.C.Lang.Knows("word_pull"), Is.True);
            Assert.That(p.C.Lang.Knows("word_weigh"), Is.True, "the heart teaches a word");
            bool heard = false;
            foreach (var m in h.Messages) heard |= m.Value.Contains("WORLD REMEMBERS");
            Assert.That(heard, Is.True);
            Assert.That(h.ObjectiveFor(1).Text, Does.Contain("light"));
            p.Pos = r.ExitM;
            Assert.That(h.ObjectiveFor(1).Done, Is.True);
        }

        [Test]
        public void AKnownWordPushesCreatures()
        {
            var h = NewHost(out var p, out var r);
            p.C.Learn("word_push");
            Vec3 feet = r.CourtM + new Vec3(0, 0, 8);
            p.Pos = feet;
            var c = h.SpawnCreature("redstrider", feet + new Vec3(0, 0, -3), 1);
            Vec3 before = c.Vel;
            Assert.That(SpeakAt(h, p, feet, c.Pos + new Vec3(0, 0.8, 0), "word_push"), Is.True, LastMessage(h));
            Assert.That(c.Vel.Z, Is.LessThan(before.Z - 3), "pushed away from the speaker");
            Assert.That(p.C.Cond["gravity_debt"], Is.GreaterThan(0), "words cost gravity debt");
            // Speaking into empty air does nothing and teaches nothing.
            h.Time += 5;
            Assert.That(h.Speak(1, new List<string> { "word_push" }, Eye(p), new Vec3(0, 1, 0)), Is.False);
        }

        [Test]
        public void AGuessedWordWorksAnywhereAndIsLearned()
        {
            var h = NewHost(out var p, out var r);
            h.Read(p, Ins(r, "ruin_antechamber_right"));
            h.Read(p, Ins(r, "ruin_step_hall"));
            // Raise a column on the antechamber floor, nowhere near the socket.
            Vec3 feet = r.GateM + new Vec3(0, 0, 4);
            Vec3 floor = feet + new Vec3(2, -0.1, 0);
            Assert.That(SpeakAt(h, p, feet, floor, "word_rise"), Is.True, LastMessage(h));
            Assert.That(p.C.Lang.Knows("word_rise"), Is.True);
        }

        [Test]
        public void WordsAndSightingsAreSaved()
        {
            var h = NewHost(out var p, out var r);
            h.Read(p, Ins(r, "ruin_arch"));
            p.C.Learn("word_rise");
            string text = p.C.ToText();
            var again = new CharacterState(h.Items, h.Rules);
            again.FromText(text);
            Assert.That(again.Lang.Of("word_push"), Is.EqualTo(Familiarity.Seen));
            Assert.That(again.Lang.SeenAt("word_push", "ruin_arch"), Is.True);
            Assert.That(again.Lang.Knows("word_rise"), Is.True);
            again.Lang.See("word_push", "ruin_antechamber_left");
            Assert.That(again.Lang.Of("word_push"), Is.EqualTo(Familiarity.Guessed));
        }
    }
}
