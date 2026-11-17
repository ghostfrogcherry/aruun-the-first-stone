using Aruun.Core.Content;
using Aruun.Core.Edits;
using Aruun.Core.Physics;
using Aruun.Core.Voxels;
using NUnit.Framework;

namespace Aruun.Core.Tests
{
    public class MoverTests
    {
        // A test floor high above the land, so terrain never gets in the way.
        const int Floor = 3000;

        static VoxelWorld FlatWorld(out Mover mover)
        {
            BlockRegistry blocks = TestContent.Blocks();
            var world = new VoxelWorld(1, blocks);
            world.Apply(EditOp.Box(-40, Floor, -40, 40, Floor, 40, blocks.Id("ancient_stone")));
            mover = new Mover(world);
            return world;
        }

        static Body Standing(double x = 0, double z = 0)
        {
            return new Body { X = x + 0.5, Y = Floor + 1.0001, Z = z + 0.5 };
        }

        static void Run(Mover m, ref Body b, double seconds)
        {
            for (double t = 0; t < seconds; t += 1.0 / 60) m.Step(ref b, 1.0 / 60);
        }

        [Test]
        public void FallsAndLandsOnTheFloor()
        {
            Mover m;
            FlatWorld(out m);
            var b = new Body { X = 0.5, Y = Floor + 40, Z = 0.5 };
            Run(m, ref b, 6);
            Assert.That(b.Grounded, Is.True);
            Assert.That(b.Y, Is.EqualTo(Floor + 1).Within(0.01));
        }

        [Test]
        public void WalksAndStopsAtAWall()
        {
            Mover m;
            VoxelWorld w = FlatWorld(out m);
            w.Apply(EditOp.Box(10, Floor + 1, -10, 10, Floor + 12, 10, w.Blocks.Id("ancient_stone")));
            Body b = Standing();
            b.VX = 16;
            for (int i = 0; i < 120; i++) { b.VX = 16; m.Step(ref b, 1.0 / 60); }
            Assert.That(b.X + m.HalfWidth, Is.EqualTo(10).Within(0.01), "flush against the wall");
            Assert.That(b.Y, Is.EqualTo(Floor + 1).Within(0.01), "did not climb a tall wall");
        }

        [Test]
        public void ClimbsLowStepsWithoutJumping()
        {
            Mover m;
            VoxelWorld w = FlatWorld(out m);
            ushort stone = w.Blocks.Id("ancient_stone");
            w.Apply(EditOp.Box(6, Floor + 1, -10, 30, Floor + 1, 10, stone));
            w.Apply(EditOp.Box(12, Floor + 2, -10, 30, Floor + 3, 10, stone));
            Body b = Standing();
            for (int i = 0; i < 90; i++) { b.VX = 16; m.Step(ref b, 1.0 / 60); }
            Assert.That(b.X, Is.GreaterThan(20));
            Assert.That(b.Y, Is.EqualTo(Floor + 4).Within(0.01));
        }

        [Test]
        public void JumpReachesItsHeightAtLowGravity()
        {
            Mover m;
            FlatWorld(out m);
            Body b = Standing();
            m.Step(ref b, 1.0 / 60);
            Assert.That(b.Grounded, Is.True);
            b.VY = m.JumpSpeed(1.2);
            double top = 0;
            for (int i = 0; i < 300; i++) { m.Step(ref b, 1.0 / 120); top = System.Math.Max(top, b.Y - Floor - 1); }
            Assert.That(top / WorldConfig.VoxelsPerMeter, Is.EqualTo(1.2).Within(0.05));
            Assert.That(b.Grounded, Is.True, "landed again");
        }
    }
}
