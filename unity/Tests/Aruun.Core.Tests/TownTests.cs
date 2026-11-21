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
            Assert.That(h.People.Count, Is.EqualTo(5));
            foreach (var r in h.People)
            {
                // The food seller works under her stall's awning (she counts as indoors); everyone else lives in the open.
                if (!r.Indoors && r.Def.Id != "mimmie") Assert.That(v.InBuilding(r.Pos), Is.False, r.Def.Id + " lives outdoors");
                int x = (int)System.Math.Floor(r.Pos.X * 4), z = (int)System.Math.Floor(r.Pos.Z * 4), y = (int)System.Math.Round(r.Pos.Y * 4);
                Assert.That(h.World.Blocks.Get(h.World.Get(x, y + 4, z)).Collision, Is.False, r.Def.Id + " has room to stand");
            }
        }

        static bool Solid(Host h, int x, int y, int z) { return h.World.Blocks.Get(h.World.Get(x, y, z)).Collision; }

        [Test]
        public void ARingWallWithGatesGoesAroundTheTown()
        {
            var h = NewHost(out var p);
            // Due west the wall is solid; due south the road goes through a gate.
            int r = Village.WallRadius + 1;
            int gw = System.Math.Max(h.World.Generator.Field.HeightAt(-r, 0), Worldgen.HeightField.SeaLevel);
            Assert.That(Solid(h, -r, gw + 8, 0), Is.True, "the west wall");
            Assert.That(Solid(h, -r, gw + Village.WallHeight - 1, 0), Is.True, "the west wall is tall");
            int gs = System.Math.Max(h.World.Generator.Field.HeightAt(0, -r), Worldgen.HeightField.SeaLevel);
            Assert.That(Solid(h, 0, gs + 4, -r), Is.False, "the south gate is open");
            Assert.That(Solid(h, 0, gs + 20, -r), Is.True, "with the wall over it");
            int towers = 0;
            foreach (var b in h.Village.Buildings) if (b.Key == "wall_tower") towers++;
            Assert.That(towers, Is.GreaterThanOrEqualTo(6));
            // Lamps are real lights in the game: enough to light the streets, few enough to stay fast.
            int lights = h.Village.Lamps.Count + h.Village.CrystalLights.Count;
            TestContext.Out.WriteLine("town lights: " + h.Village.Lamps.Count + " warm, " + h.Village.CrystalLights.Count + " crystal");
            Assert.That(lights, Is.InRange(10, 120));
        }

        [Test]
        public void TheCollegeDoorLeadsToTheLibraryAndBack()
        {
            var h = NewHost(out var p);
            Town.Portal into = null, outOf = null;
            foreach (var g in h.Village.Portals) { if (g.Name == "the Grand College") into = g; else outOf = g; }
            Assert.That(into, Is.Not.Null);
            Assert.That(outOf, Is.Not.Null);
            Assert.That(h.Village.InLibrary(into.At), Is.False);

            // Stand on the terrace in front of the great door and press E at it.
            Vec3 eye = into.At + new Vec3(0, 1.6, -2.5);
            p.Pos = eye - new Vec3(0, 1.6, 0);
            Assert.That(h.Interact(1, eye, new Vec3(0, 0, 1)), Is.True);
            Assert.That(h.Village.InLibrary(p.Pos), Is.True, "through the door");
            Assert.That(p.RespawnAt, Is.Not.Null, "the game moves the body");
            Assert.That(LastMessage(h), Does.Contain("library"));
            int x = (int)System.Math.Floor(p.Pos.X * 4), y = (int)System.Math.Floor(p.Pos.Y * 4), z = (int)System.Math.Floor(p.Pos.Z * 4);
            Assert.That(Solid(h, x, y - 1, z), Is.True, "a floor underfoot");
            for (int dy = 0; dy < 8; dy++) Assert.That(Solid(h, x, y + dy, z), Is.False, "headroom");

            // The archivist lives here, on the library floor.
            var a = h.Person("archivist");
            Assert.That(a, Is.Not.Null);
            Assert.That(h.Village.InLibrary(a.Pos), Is.True);
            Assert.That(System.Math.Abs(a.Pos.Y - p.Pos.Y), Is.LessThan(0.5), "on the same floor");
            Run(h, 10);
            Assert.That(System.Math.Abs(a.Pos.Y - p.Pos.Y), Is.LessThan(0.5), "and stays on it");
            Conversation c = Talk(h, p, "archivist");
            Assert.That(c.Say, Does.Contain("library"));
            h.EndTalk(1);

            // The way out leads back to the terrace.
            eye = outOf.At + new Vec3(0, 1.6, 2.5);
            Assert.That(h.Interact(1, eye, new Vec3(0, 0, -1)), Is.True);
            Assert.That(h.Village.InLibrary(p.Pos), Is.False);
            Assert.That(Vec3.FlatDistance(p.Pos, into.At), Is.LessThan(4), "in front of the college door");
        }

        [Test]
        public void MimmieLouKeepsAFoodStallInEveryTown()
        {
            var h = NewHost(out var p);
            int stalls = 0;
            foreach (var b in h.Village.Buildings) if (b.Key == "food_stall") stalls++;
            int sellers = 0;
            foreach (var r in h.People) if (r.Def.Id == "mimmie") sellers++;
            Assert.That(stalls, Is.GreaterThanOrEqualTo(1));
            Assert.That(sellers, Is.EqualTo(stalls), "one Mimmie-L'ou behind every food stall");
            PersonRec m = h.PersonNear("mimmie", p.Pos);
            Assert.That(m.Def.ShortName, Is.EqualTo("Mimmie-L'ou"));
            // Behind her counter, under the awning, with a lantern lit nearby.
            int mx = (int)System.Math.Floor(m.Pos.X * 4), mz = (int)System.Math.Floor(m.Pos.Z * 4), my = (int)System.Math.Round(m.Pos.Y * 4);
            bool awning = false;
            for (int dy = 8; dy <= 20; dy++) if (h.World.Get(mx, my + dy, mz) == h.World.Blocks.Id("awning_violet")) awning = true;
            Assert.That(awning, Is.True, "under the violet awning, not on it");
            bool lantern = false;
            foreach (var l in h.Village.Lamps) if (Vec3.FlatDistance(l, m.Pos) < 4) lantern = true;
            Assert.That(lantern, Is.True, "a lantern hangs at the stall");
            Run(h, 30);
            Assert.That(Vec3.FlatDistance(m.Pos, m.Home), Is.LessThan(1.0), "she stays behind her counter");
            Assert.That(m.HomeYaw, Is.Not.Null);
            for (int i = 0; i < 400 && m.Walking; i++) h.Tick(0.05);
            Assert.That(m.Walking, Is.False);
            Assert.That(m.Yaw, Is.EqualTo(m.HomeYaw.Value).Within(1e-9), "standing still, she faces her counter");
        }

        /// <summary>Takes away the food a castaway starts with, so the tests count only what they hand over.</summary>
        static void EmptyLarder(PlayerRec p)
        {
            foreach (string k in new[] { "redroot", "glasswort_leaf", "reed_stalk", "raw_meat" }) p.C.Inv.Remove(k, p.C.Inv.Count(k));
        }

        [Test]
        public void MimmieLouBartersFoodForIngredients()
        {
            var h = NewHost(out var p);
            EmptyLarder(p);
            Conversation c = Talk(h, p, "mimmie");
            Assert.That(c.Say, Does.Contain("cookin'"));
            int shop = c.Choices.FindIndex(t => t.Contains("selling"));
            Assert.That(shop, Is.GreaterThanOrEqualTo(0));
            c = h.Choose(1, shop);
            Assert.That(c.Choices.FindIndex(t => t.Contains("Grandmother")), Is.EqualTo(-1), "no heirloom soup before the cookbook quest");
            int stew = c.Choices.FindIndex(t => t.StartsWith("Redroot stew"));
            Assert.That(stew, Is.GreaterThanOrEqualTo(0));

            // Not enough: nothing changes hands, and she says what is missing.
            p.C.Inv.Add("redroot", 2);
            c = h.Choose(1, stew);
            Assert.That(c.Say, Does.Contain("1 more redroot").And.Contain("glasswort"));
            Assert.That(p.C.Inv.Count("redroot"), Is.EqualTo(2));
            Assert.That(p.C.Inv.Count("redroot_stew"), Is.EqualTo(0));

            // Enough: ingredients go, stew comes back, and the shop stays open for more.
            p.C.Inv.Add("redroot", 1);
            p.C.Inv.Add("glasswort_leaf", 1);
            c = h.Choose(1, stew);
            Assert.That(p.C.Inv.Count("redroot"), Is.EqualTo(0));
            Assert.That(p.C.Inv.Count("glasswort_leaf"), Is.EqualTo(0));
            Assert.That(p.C.Inv.Count("redroot_stew"), Is.EqualTo(2));
            Assert.That(c.Choices.FindIndex(t => t.StartsWith("Redroot stew")), Is.EqualTo(stew), "still in the shop");
            c = h.Choose(1, c.Choices.FindIndex(t => t == "Something else."));
            Assert.That(c.Say, Does.Contain("cookin'"), "back to the start");
        }

        [Test]
        public void TheIngredientQuestThenGrandmasCookbook()
        {
            var h = NewHost(out var p);
            EmptyLarder(p);
            Conversation c = Talk(h, p, "mimmie");
            c = h.Choose(1, c.Choices.FindIndex(t => t.Contains("help")));
            Assert.That(p.C.Quests.IsActive("side_ingredients"), Is.True);
            Assert.That(h.CurrentStage(p, "side_ingredients").Text, Does.Contain("redroot"));
            p.C.Inv.Add("redroot", 3);
            c = Talk(h, p, "mimmie");
            Assert.That(c.Say, Does.Contain("Three fresh redroot"), "still short of reeds");
            p.C.Inv.Add("reed_stalk", 2);
            c = Talk(h, p, "mimmie");
            c = h.Choose(1, 0);
            Assert.That(p.C.Quests.IsDone("side_ingredients"), Is.True);
            Assert.That(p.C.Inv.Count("redroot_stew"), Is.EqualTo(2));

            // Now she shows you Gran's cookbook.
            c = Talk(h, p, "mimmie");
            Assert.That(c.Say, Does.Contain("cookbook"));
            c = h.Choose(1, c.Choices.FindIndex(t => t.Contains("tomato")));
            Assert.That(p.C.Quests.Stage("side_cookbook"), Is.EqualTo(0));
            Assert.That(h.CurrentStage(p, "side_cookbook").Target, Is.EqualTo("person:archivist"));

            // The archivist, in the library, knows the word is not from Aruun.
            c = Talk(h, p, "archivist");
            Assert.That(c.Say, Does.Contain("Tomato"));
            c = h.Choose(1, 0);
            Assert.That(p.C.Quests.Stage("side_cookbook"), Is.EqualTo(1));

            // Without a tomato she can only wait.
            c = Talk(h, p, "mimmie");
            Assert.That(c.Say, Does.Contain("luck"));
            p.C.Inv.Add("earth_tomato", 1);
            c = Talk(h, p, "mimmie");
            c = h.Choose(1, 0);
            Assert.That(p.C.Quests.IsDone("side_cookbook"), Is.True);
            Assert.That(p.C.Inv.Count("earth_tomato"), Is.EqualTo(0));
            Assert.That(p.C.Inv.Count("heirloom_soup"), Is.EqualTo(3));

            // And Gran's soup is on the menu from now on.
            c = Talk(h, p, "mimmie");
            c = h.Choose(1, c.Choices.FindIndex(t => t.Contains("selling")));
            Assert.That(c.Choices.FindIndex(t => t.Contains("Grandmother")), Is.GreaterThanOrEqualTo(0));
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
            Assert.That(c.Say, Does.Contain("Nobody knows"));
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
            Assert.That(c.Say, Does.Contain("Nobody does"));
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

        [Test]
        public void TheOldSteadingCanBeClaimedWithATether()
        {
            var h = NewHost(out var p);
            Assert.That(h.Village.Buildings.Exists(b => b.Key == "steading"), Is.True);
            Vec3 plinth = h.Village.SteadingPlinth;
            int gx = (int)System.Math.Floor(plinth.X * 4), gz = (int)System.Math.Floor(plinth.Z * 4), gy = (int)System.Math.Round(plinth.Y * 4) - 1;
            Assert.That(h.World.Blocks.Get(h.World.Get(gx, gy, gz)).Collision, Is.True, "the plinth is solid");
            Assert.That(h.World.Get(gx, gy + 1, gz), Is.EqualTo(0), "with room for a Tether on top");

            // Dael offers the steading once the flint is brought.
            p.C.Quests.Set("side_watch", QuestLog.Done);
            Conversation c = Talk(h, p, "watcher");
            Assert.That(c.Choices.Count, Is.GreaterThan(0));
            h.Choose(1, 0);
            Assert.That(p.C.Quests.IsActive("side_steading"), Is.True);
            h.EndTalk(1);
            Assert.That(h.ObjectiveFor(1).Target.HasValue, Is.True, "the HUD points at the steading");

            p.Pos = plinth + new Vec3(0, 0, -1.5);
            p.C.Inv.Add("tether_stone", 1);
            for (int i = 0; i < Inventory.Size; i++) if (p.C.Inv.KeyAt(i) == "tether_stone") p.C.Selected = i;
            Assert.That(h.UseHeld(1, true, gx, gy, gz, gx, gy + 1, gz), Is.True);
            Assert.That(h.WorldFlags.Contains(Host.SteadingFlag), Is.True);
            Run(h, 0.2);
            Assert.That(h.CurrentStage(p, "side_steading").Id, Is.EqualTo("tell"));
            Talk(h, p, "watcher");
            h.Choose(1, 0);
            Assert.That(p.C.Quests.IsDone("side_steading"), Is.True);
            Assert.That(p.C.Inv.Count("stone_brick"), Is.EqualTo(160));
        }
    }
}
