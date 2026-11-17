using System.IO;
using Aruun.Core.Combat;
using Aruun.Core.Creatures;
using Aruun.Core.Edits;
using Aruun.Core.Farming;
using Aruun.Core.Items;
using Aruun.Core.Play;
using Aruun.Core.Survival;
using Aruun.Core.Voxels;
using NUnit.Framework;

namespace Aruun.Core.Tests
{
    /// <summary>
    /// The whole survival loop through the Host, as the game plays it: dig, gather, craft,
    /// eat, fight, die and wake. Everything happens on a platform built high in the sky, so the
    /// terrain under it does not matter.
    /// </summary>
    public class HostTests
    {
        const int Floor = 400;   // voxel height of the test platform's top layer

        static Host NewHost(out PlayerRec p)
        {
            var blocks = TestContent.Blocks();
            var world = new VoxelWorld(20261116, blocks, TestContent.Map());
            var items = ItemRegistry.FromJson(File.ReadAllText(Path.Combine(TestContent.Root, "items", "items.json")), blocks);
            var rules = Ruleset.Load(File.ReadAllText(Path.Combine(TestContent.Root, "survival", "rulesets.json")), "light");
            var plants = Plants.FromJson(File.ReadAllText(Path.Combine(TestContent.Root, "farming", "plants.json")));
            var creatures = CreatureDef.FromJson(File.ReadAllText(Path.Combine(TestContent.Root, "creatures", "creatures.json")));
            var host = new Host(world, items, rules, plants, creatures, 1) { SpawnCreatures = false };
            // A 30 m square of soil, 1 m thick, in four boxes (one op is at most 64 voxels across).
            for (int bx = -60; bx < 60; bx += 60)
                for (int bz = -60; bz < 60; bz += 60)
                    world.Apply(EditOp.Box(bx, Floor - 3, bz, bx + 59, Floor, bz + 59, blocks.Id("red_soil")));
            p = host.Join(1, new CharacterState(items, rules), Feet(0, 0), true);
            host.SpawnPoint = Feet(0, 0);
            return host;
        }

        static Vec3 Feet(double x, double z) { return new Vec3(x, (Floor + 1) / 4.0, z); }

        static void Run(Host h, double seconds)
        {
            for (double t = 0; t < seconds; t += 0.05) h.Tick(0.05);
        }

        static DigTick DigUntilDone(Host h, int x, int y, int z, double maxSeconds)
        {
            DigTick r = default;
            for (double t = 0; t < maxSeconds; t += 0.05)
            {
                h.Time += 0.05;
                r = h.Dig(1, x, y, z, 0.05);
                if (r.Broken || r.Refused) return r;
            }
            return r;
        }

        [Test]
        public void BareHandsDigSoilAndKeepIt()
        {
            var h = NewHost(out var p);
            var r = DigUntilDone(h, 4, Floor, 4, 10);
            Assert.That(r.Broken, Is.True);
            Assert.That(h.World.Get(4, Floor, 4), Is.EqualTo(0));
            Assert.That(p.C.Inv.Count("red_soil"), Is.GreaterThan(0), "dug soil goes in the bag");
            Assert.That(h.Edits.Count, Is.EqualTo(1), "the bite is handed to the Game layer to journal and redraw");
        }

        [Test]
        public void TimberNeedsAnAxe()
        {
            var h = NewHost(out var p);
            h.World.Apply(EditOp.Set(6, Floor + 1, 6, h.World.Blocks.Id("timber")));
            var r = DigUntilDone(h, 6, Floor + 1, 6, 5);
            Assert.That(r.Refused, Is.True);
            Assert.That(r.Message, Does.Contain("axe"));
            p.C.Inv.Add("flint_axe", 1);
            p.C.Selected = SlotOf(p, "flint_axe");
            r = DigUntilDone(h, 6, Floor + 1, 6, 20);
            Assert.That(r.Broken, Is.True);
            Assert.That(p.C.Inv.Count("timber"), Is.GreaterThan(0));
        }

        [Test]
        public void PlaceTheBlocksYouDug()
        {
            var h = NewHost(out var p);
            p.C.Inv.Add("red_soil", 3);
            p.C.Selected = SlotOf(p, "red_soil");
            int before = p.C.Inv.Count("red_soil");
            Assert.That(h.UseHeld(1, true, 4, Floor, 4, 4, Floor + 1, 4), Is.True);
            Assert.That(h.World.Get(4, Floor + 1, 4), Is.EqualTo(h.World.Blocks.Id("red_soil")));
            Assert.That(p.C.Inv.Count("red_soil"), Is.EqualTo(before - 1));
            // Never inside your own body.
            Assert.That(h.UseHeld(1, true, 0, Floor, 0, 0, Floor + 1, 0), Is.False);
        }

        [Test]
        public void CraftCordThenAFlintPick()
        {
            var h = NewHost(out var p);
            p.C.Inv.Add("plant_fibre", 3);
            p.C.Inv.Add("flint", 2);
            p.C.Inv.Add("reed_stalk", 2);
            Assert.That(h.CanCraft(1, "flint_pick"), Is.EqualTo("missing materials"));
            Assert.That(h.Craft(1, "cord"), Is.EqualTo(""));
            Assert.That(h.Craft(1, "flint_pick"), Is.EqualTo(""));
            Assert.That(p.C.Inv.Count("flint_pick"), Is.EqualTo(1));
            Assert.That(h.CanCraft(1, "roast_redroot"), Does.Contain("nearby"), "cooking needs a fire");
        }

        [Test]
        public void CampfireLetsYouCookAndWarms()
        {
            var h = NewHost(out var p);
            p.C.Inv.Add("campfire_kit", 1);
            p.C.Selected = SlotOf(p, "campfire_kit");
            Assert.That(h.UseHeld(1, true, 4, Floor, 0, 4, Floor + 1, 0), Is.True);
            Assert.That(h.Structures.Count, Is.EqualTo(1));
            Assert.That(h.CanCraft(1, "roast_redroot"), Is.EqualTo(""), "standing next to the fire");
            Run(h, 1);
            Assert.That(p.Flags, Does.Contain("near_heat"));
        }

        [Test]
        public void EatingFeedsYou()
        {
            var h = NewHost(out var p);
            p.C.Cond["hunger"] = 60;
            p.C.Selected = SlotOf(p, "redroot");
            Assert.That(h.UseHeld(1, false, 0, 0, 0, 0, 0, 0), Is.True);
            Assert.That(p.C.Cond["hunger"], Is.LessThan(60));
        }

        [Test]
        public void WildFlintAppearsAndCanBeGathered()
        {
            var h = NewHost(out var p);
            // Stand on real ground at the spawn place so the seed's wild nodes are around.
            int gy = h.World.Generator.Field.HeightAt(0, 0);
            h.SetPose(1, new Vec3(0, (gy + 1) / 4.0, 0), Vec3.Forward, 0);
            Run(h, 1);
            Assert.That(h.PlantsInWorld.Count, Is.GreaterThan(0), "wild plants and flint around the spawn");
            PlantRec node = h.PlantsInWorld[0];
            Vec3 eye = node.Pos + new Vec3(0, 1.6, -1.5);
            h.SetPose(1, node.Pos + new Vec3(0, 0, -1.5), Vec3.Forward, 0);
            int itemsBefore = CountAll(p);
            Assert.That(h.Interact(1, eye, (node.Pos + new Vec3(0, 0.3, 0)) - eye), Is.True);
            Assert.That(CountAll(p), Is.GreaterThan(itemsBefore));
            Assert.That(h.PlantsInWorld.Contains(node), Is.False, "a wild node is gone once gathered");
        }

        [Test]
        public void SwingsKillACreatureAndGiveItsDrops()
        {
            var h = NewHost(out var p);
            p.C.Inv.Add("reed_spear", 1);
            p.C.Selected = SlotOf(p, "reed_spear");
            Creature c = h.SpawnCreature("redstrider", Feet(0, 1.4), 1);
            h.SetPose(1, Feet(0, 0), Vec3.Forward, 0);
            for (int i = 0; i < 60 && c.State != "dead"; i++)
            {
                c.Pos = Feet(0, 1.4);   // keep it in reach; it would rather run
                h.Attack(1, false);
                Run(h, 0.6);
                p.C.Stamina = p.C.StaminaMax;
            }
            Assert.That(c.State, Is.EqualTo("dead"));
            Assert.That(p.C.Inv.Count("raw_meat"), Is.GreaterThan(0));
        }

        [Test]
        public void GuardingTakesLessDamage()
        {
            var h = NewHost(out var p);
            h.SetPose(1, Feet(0, 0), Vec3.Forward, 0);
            double hp = p.C.Hp;
            h.DamagePlayer(1, 20, Feet(0, 2), "creature");
            double open = hp - p.C.Hp;
            p.C.Hp = hp;
            h.SetGuard(1, true);
            h.DamagePlayer(1, 20, Feet(0, 2), "creature");
            double guarded = hp - p.C.Hp;
            Assert.That(guarded, Is.LessThan(open));
        }

        [Test]
        public void DeathAndWakingAtTheSpawn()
        {
            var h = NewHost(out var p);
            h.SetPose(1, Feet(3, 3), Vec3.Forward, 0);
            h.DamagePlayer(1, 1000, Feet(3, 4), "creature");
            Assert.That(p.C.Dead, Is.True);
            Run(h, 6);
            Assert.That(p.C.Dead, Is.False);
            Assert.That(p.RespawnAt.HasValue, Is.True);
            Assert.That(Vec3.Distance(p.RespawnAt.Value, h.SpawnPoint), Is.LessThan(0.01));
        }

        [Test]
        public void CreaturesWalkOnTheGroundWithoutFallingThrough()
        {
            var h = NewHost(out var p);
            h.SetPose(1, Feet(-4, -4), Vec3.Forward, 0);
            Creature c = h.SpawnCreature("redstrider", Feet(3, 3) + new Vec3(0, 0.5, 0), 1);
            Run(h, 8);
            Assert.That(c.Pos.Y, Is.EqualTo((Floor + 1) / 4.0).Within(0.3), "it lands on the platform and stays on it");
        }

        [Test]
        public void HungerRisesOverTime()
        {
            var h = NewHost(out var p);
            double before = p.C.Cond["hunger"];
            Run(h, 60);
            Assert.That(p.C.Cond["hunger"], Is.GreaterThan(before));
        }

        static int SlotOf(PlayerRec p, string key)
        {
            for (int i = 0; i < Inventory.Size; i++) if (p.C.Inv.KeyAt(i) == key) return i;
            Assert.Fail("no " + key + " in the bag");
            return -1;
        }

        static int CountAll(PlayerRec p)
        {
            int n = 0;
            for (int i = 0; i < Inventory.Size; i++) { var s = p.C.Inv[i]; if (s.HasValue) n += s.Value.Count; }
            return n;
        }
    }
}
