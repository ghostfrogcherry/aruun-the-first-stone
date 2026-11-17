using System.IO;
using Aruun.Core.Combat;
using Aruun.Core.Creatures;
using Aruun.Core.Farming;
using Aruun.Core.Items;
using Aruun.Core.Language;
using Aruun.Core.People;
using Aruun.Core.Play;
using Aruun.Core.Ruins;
using Aruun.Core.Survival;
using Aruun.Core.Town;
using Aruun.Core.Voxels;
using NUnit.Framework;

namespace Aruun.Core.Tests
{
    /// <summary>The Confluence village, its people and their quests, through the Host on the real map.</summary>
    public class TownTests
    {
        static Host NewHost(out PlayerRec p)
        {
            var blocks = TestContent.Blocks();
            var world = new VoxelWorld(20261116, blocks, TestContent.Map());
            world.Generator.Village = Village.Load(TestContent.Root, world.Generator.Field, blocks, world.Generator.Ruin);
            var items = ItemRegistry.FromJson(File.ReadAllText(Path.Combine(TestContent.Root, "items", "items.json")), blocks);
            var rules = Ruleset.Load(File.ReadAllText(Path.Combine(TestContent.Root, "survival", "rulesets.json")), "light");
            var plants = Plants.FromJson(File.ReadAllText(Path.Combine(TestContent.Root, "farming", "plants.json")));
            var creatures = CreatureDef.FromJson(File.ReadAllText(Path.Combine(TestContent.Root, "creatures", "creatures.json")));
            var host = new Host(world, items, rules, plants, creatures, 1) { SpawnCreatures = false };
            host.Lexicon = Lexicon.FromJson(File.ReadAllText(Path.Combine(TestContent.Root, "language", "lexicon.json")));
            host.LoadPeople(PersonDef.FromJson(File.ReadAllText(Path.Combine(TestContent.Root, "people", "people.json"))),
                QuestDef.FromJson(File.ReadAllText(Path.Combine(TestContent.Root, "quests", "quests.json"))));
            p = host.Join(1, new CharacterState(items, rules), new Vec3(6, host.GroundHeight(6, -6), -6), true);
            return host;
        }

        static void Run(Host h, double seconds) { for (double t = 0; t < seconds; t += 0.05) h.Tick(0.05); }

        static Conversation Talk(Host h, PlayerRec p, string who)
        {
            PersonRec r = h.Person(who);
            Assert.That(r, Is.Not.Null, who);
            p.Pos = r.Pos + new Vec3(0, 0, 1.5);
            return h.StartTalk(p, r);
        }

        static string LastMessage(Host h) { return h.Messages.Count > 0 ? h.Messages[h.Messages.Count - 1].Value : ""; }

        [Test]
        public void TheVillageStandsAroundTheConfluence()
        {
            var h = NewHost(out var p);
            Village v = h.Village;
            Assert.That(v.Buildings.Count, Is.GreaterThanOrEqualTo(6), "houses, huts and the tower");
            int paving = h.World.Blocks.Id("paving");
            int ground = h.World.Generator.Field.HeightAt(20, 20);
            if (ground > Worldgen.HeightField.SeaLevel) Assert.That(h.World.Get(20, ground, 20), Is.EqualTo(paving), "the plaza is paved");
            foreach (var b in v.Buildings)
            {
                int solid = 0;
                for (int y = b.Floor + 1; y < b.Floor + b.Height; y++)
                    for (int x = b.MinX; x <= b.MaxX; x += 3)
                        if (h.World.Blocks.Get(h.World.Get(x, y, b.Z)).Collision) solid++;
                Assert.That(solid, Is.GreaterThan(5), b.Key + " has walls");
                Assert.That(h.World.Blocks.Get(h.World.Get(b.X, b.Floor - 1, b.Z)).Collision, Is.True, b.Key + " stands on its pad");
            }
            Assert.That(h.People.Count, Is.EqualTo(3));
            foreach (var r in h.People)
            {
                Assert.That(v.InBuilding(r.Pos), Is.False, r.Def.Id + " lives outdoors");
                int x = (int)System.Math.Floor(r.Pos.X * 4), z = (int)System.Math.Floor(r.Pos.Z * 4), y = (int)System.Math.Round(r.Pos.Y * 4);
                Assert.That(h.World.Blocks.Get(h.World.Get(x, y + 4, z)).Collision, Is.False, r.Def.Id + " has room to stand");
            }
        }

        [Test]
        public void PeoplePotterAboutOnTheGround()
        {
            var h = NewHost(out var p);
            var elder = h.Person("elder");
            Run(h, 20);
            Assert.That(Vec3.FlatDistance(elder.Pos, elder.Home), Is.LessThanOrEqualTo(elder.Def.WanderM + 0.2));
            Assert.That(System.Math.Abs(elder.Pos.Y - elder.Home.Y), Is.LessThan(1.5), "walks on the ground");
        }

        [Test]
        public void TheElderSendsYouToTheRuinAndRewardsYou()
        {
            var h = NewHost(out var p);
            Assert.That(h.ObjectiveFor(1).Text, Does.Contain("Teshwa"), "the HUD starts by pointing at the elder");
            Conversation c = Talk(h, p, "elder");
            Assert.That(c.Say, Does.Contain("humming"));
            Assert.That(c.Choices[c.Choices.Count - 1], Is.EqualTo("Goodbye."));
            // Ask about the builders first: a reply, no quest.
            c = h.Choose(1, 1);
            Assert.That(c.Say, Does.Contain("before the Vaari"));
            Assert.That(p.C.Quests.Has("main_remember"), Is.False);
            c = h.Choose(1, 0);
            Assert.That(p.C.Quests.IsActive("main_remember"), Is.True);
            Assert.That(c.Say, Does.Contain("road south"));
            Assert.That(h.ObjectiveFor(1).Target, Is.Not.Null);
            Assert.That(h.Choose(1, c.Choices.Count - 1), Is.Null, "goodbye ends it");
            Assert.That(p.Talk, Is.Null);

            // The heart falls (the ruin's own tests cover how): the quest moves on by itself.
            h.WorldFlags.Add(EchoRuin.FlagRemembered);
            Run(h, 1);
            Assert.That(h.CurrentStage(p, "main_remember").Id, Is.EqualTo("return"));
            Assert.That(h.ObjectiveFor(1).Text, Does.Contain("Tell Elder"));
            c = Talk(h, p, "elder");
            Assert.That(c.Say, Does.Contain("hum has stopped"));
            int bronze = p.C.Inv.Count("bronze_fitting");
            c = h.Choose(1, 0);
            Assert.That(p.C.Quests.IsDone("main_remember"), Is.True);
            Assert.That(p.C.Inv.Count("bronze_fitting"), Is.EqualTo(bronze + 2));
            Assert.That(c.Say, Does.Contain("starting again"));
        }

        [Test]
        public void WakingTheHeartFirstStillLetsYouTellTheElder()
        {
            var h = NewHost(out var p);
            h.WorldFlags.Add(EchoRuin.FlagRemembered);
            Conversation c = Talk(h, p, "elder");
            Assert.That(c.Say, Does.Contain("already"));
            c = h.Choose(1, 0);
            Assert.That(h.CurrentStage(p, "main_remember").Id, Is.EqualTo("return"), "the ruin stage is already done");
            h.Choose(1, 0);
            Assert.That(p.C.Quests.IsDone("main_remember"), Is.True);
        }

        [Test]
        public void TheCookWantsMeat()
        {
            var h = NewHost(out var p);
            Conversation c = Talk(h, p, "cook");
            h.Choose(1, 0);
            Assert.That(p.C.Quests.IsActive("side_hearth"), Is.True);
            c = Talk(h, p, "cook");
            Assert.That(c.Say, Does.Contain("Four cuts"), "not ready yet");
            p.C.Inv.Add("raw_meat", 5);
            c = Talk(h, p, "cook");
            Assert.That(c.Say, Does.Contain("brought it"));
            int roast = p.C.Inv.Count("roast_meat");
            h.Choose(1, 0);
            Assert.That(p.C.Quests.IsDone("side_hearth"), Is.True);
            Assert.That(p.C.Inv.Count("raw_meat"), Is.EqualTo(1), "four were handed over");
            Assert.That(p.C.Inv.Count("roast_meat"), Is.EqualTo(roast + 4));
            Assert.That(LastMessage(h), Does.Contain("Quest complete"));
        }

        [Test]
        public void YouTalkBySeeingSomeoneAndPressingE()
        {
            var h = NewHost(out var p);
            var w = h.Person("watcher");
            Vec3 eye = w.Pos + new Vec3(0, 1.5, 2);
            Assert.That(h.Interact(1, eye, new Vec3(0, 0, -1)), Is.True);
            Assert.That(p.Talk, Is.Not.Null);
            Assert.That(p.Talk.Name, Is.EqualTo("Dael"));
            Assert.That(w.TalkingTo, Is.EqualTo(1));
        }

        [Test]
        public void QuestsAreSaved()
        {
            var h = NewHost(out var p);
            Talk(h, p, "watcher");
            h.Choose(1, 0);
            p.C.Quests.Set("side_hearth", QuestLog.Done);
            var again = new CharacterState(h.Items, h.Rules);
            again.FromText(p.C.ToText());
            Assert.That(again.Quests.IsActive("side_watch"), Is.True);
            Assert.That(again.Quests.IsDone("side_hearth"), Is.True);
        }
    }
}
