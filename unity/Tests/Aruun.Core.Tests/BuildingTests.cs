using System.IO;
using System.Linq;
using Aruun.Core.Building;
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
    /// <summary>Shape building: two corners and a shape fill empty space, paid for block by block.</summary>
    public class BuildingTests
    {
        const int Floor = 400;

        static Host NewHost(out PlayerRec p)
        {
            var blocks = TestContent.Blocks();
            var world = new VoxelWorld(20261116, blocks, TestContent.Map());
            var items = ItemRegistry.FromJson(File.ReadAllText(Path.Combine(TestContent.Root, "items", "items.json")), blocks);
            var rules = Ruleset.Load(File.ReadAllText(Path.Combine(TestContent.Root, "survival", "rulesets.json")), "light");
            var plants = Plants.FromJson(File.ReadAllText(Path.Combine(TestContent.Root, "farming", "plants.json")));
            var creatures = CreatureDef.FromJson(File.ReadAllText(Path.Combine(TestContent.Root, "creatures", "creatures.json")));
            var host = new Host(world, items, rules, plants, creatures, 1) { SpawnCreatures = false };
            for (int bx = -60; bx < 60; bx += 60)
                for (int bz = -60; bz < 60; bz += 60)
                    world.Apply(EditOp.Box(bx, Floor - 3, bz, bx + 59, Floor, bz + 59, blocks.Id("red_soil")));
            p = host.Join(1, new CharacterState(items, rules), new Vec3(0, (Floor + 1) / 4.0, 0), true);
            return host;
        }

        static void Hold(PlayerRec p, string key, int n)
        {
            p.C.Inv.Add(key, n);
            for (int i = 0; i < Inventory.Size; i++) if (p.C.Inv.KeyAt(i) == key) { p.C.Selected = i; return; }
            Assert.Fail("no " + key);
        }

        [Test]
        public void AWallStandsInOnePlaneAndCostsWhatItFills()
        {
            var h = NewHost(out var p);
            Hold(p, "stone_brick", 200);
            ushort brick = h.World.Blocks.Id("stone_brick");
            // Corners 12 cells apart along x and 3 along z: the wall keeps to x, at z = 8.
            Assert.That(h.ShapeCost(Shape.Wall, 4, Floor + 1, 8, 15, Floor + 8, 11, brick), Is.EqualTo(12 * 8));
            Assert.That(h.BuildShape(1, Shape.Wall, 4, Floor + 1, 8, 15, Floor + 8, 11), Is.Empty);
            Assert.That(h.World.Get(10, Floor + 4, 8), Is.EqualTo(brick));
            Assert.That(h.World.Get(10, Floor + 4, 9), Is.Not.EqualTo(brick), "one voxel thick");
            Assert.That(p.C.Inv.Count("stone_brick"), Is.EqualTo(200 - 96));
        }

        [Test]
        public void ShortOfBlocksBuildsNothing()
        {
            var h = NewHost(out var p);
            Hold(p, "stone_brick", 10);
            string why = h.BuildShape(1, Shape.Floor, 4, Floor + 1, 4, 9, Floor + 5, 9);
            Assert.That(why, Does.Contain("needs 36"));
            Assert.That(h.World.Get(5, Floor + 1, 5), Is.EqualTo(0));
            Assert.That(p.C.Inv.Count("stone_brick"), Is.EqualTo(10));
        }

        [Test]
        public void ShapesOnlyFillEmptySpaceAndChargeForThat()
        {
            var h = NewHost(out var p);
            Hold(p, "stone_brick", 500);
            ushort soil = h.World.Blocks.Id("red_soil");
            // Half of this box is inside the soil platform: only the air half is built and paid for.
            int before = p.C.Inv.Count("stone_brick");
            Assert.That(h.BuildShape(1, Shape.Box, 4, Floor - 1, 4, 7, Floor + 2, 7), Is.Empty);
            Assert.That(before - p.C.Inv.Count("stone_brick"), Is.EqualTo(4 * 4 * 2));
            Assert.That(h.World.Get(5, Floor, 5), Is.EqualTo(soil), "the soil stays");
        }

        [Test]
        public void ARoomIsHollow()
        {
            var h = NewHost(out var p);
            Hold(p, "stone_brick", 1000);
            ushort brick = h.World.Blocks.Id("stone_brick");
            Assert.That(h.BuildShape(1, Shape.Room, 4, Floor + 1, 4, 10, Floor + 6, 10), Is.Empty);
            Assert.That(h.World.Get(7, Floor + 3, 7), Is.EqualTo(0), "inside is empty");
            Assert.That(h.World.Get(4, Floor + 3, 7), Is.EqualTo(brick), "a wall");
            Assert.That(h.World.Get(7, Floor + 6, 7), Is.EqualTo(brick), "the roof");
            int shell = 7 * 6 * 7 - 5 * 4 * 5;
            Assert.That(p.C.Inv.Count("stone_brick"), Is.EqualTo(1000 - shell));
        }

        [Test]
        public void NoBuildingIntoABodyOrOutOfReach()
        {
            var h = NewHost(out var p);
            Hold(p, "stone_brick", 500);
            Assert.That(h.BuildShape(1, Shape.Box, -2, Floor + 1, -2, 2, Floor + 3, 2), Does.Contain("standing"));
            Assert.That(h.BuildShape(1, Shape.Line, 4, Floor + 1, 56, 4, Floor + 1, 58), Does.Contain("too far"));
        }

        [Test]
        public void SidesAreClampedAndLinesAreUnbroken()
        {
            int bx = 100, by = 0, bz = 0;
            Shapes.Snap(Shape.Box, 0, 0, 0, ref bx, ref by, ref bz);
            Assert.That(bx, Is.EqualTo(Shapes.MaxSide - 1));
            var line = Shapes.LineCells(0, 0, 0, 7, 3, -5);
            Assert.That(line.Count, Is.EqualTo(8));
            for (int i = 1; i < line.Count; i++)
                Assert.That(Enumerable.Range(0, 3).All(a => System.Math.Abs(line[i][a] - line[i - 1][a]) <= 1), Is.True);
            int x, y, z;
            Shapes.Unkey(Shapes.Key(-5, 401, -70000), out x, out y, out z);
            Assert.That(new[] { x, y, z }, Is.EqualTo(new[] { -5, 401, -70000 }));
        }
    }
}
