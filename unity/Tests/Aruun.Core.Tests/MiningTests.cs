using System.Collections.Generic;
using System.IO;
using Aruun.Core.Content;
using Aruun.Core.Edits;
using Aruun.Core.Mining;
using Aruun.Core.Voxels;
using NUnit.Framework;

namespace Aruun.Core.Tests
{
    public class MiningTests
    {
        static ToolSet Tools()
        {
            return ToolSet.FromJson(File.ReadAllText(Path.Combine(TestContent.Root, "items", "items.json")));
        }

        [Test]
        public void ToolsLoadFromTheItemsFile()
        {
            ToolSet tools = Tools();
            Assert.That(tools.Unarmed.Class, Is.EqualTo(ToolClass.Hand));
            Assert.That(tools.Get("flint_axe").Class, Is.EqualTo(ToolClass.Axe));
            Assert.That(tools.Get("flint_pick").Class, Is.EqualTo(ToolClass.Pick));
            Assert.That(tools.Get("roast_meat"), Is.SameAs(tools.Unarmed), "food digs like bare hands");
            Assert.That(tools.Get(null), Is.SameAs(tools.Unarmed));
        }

        [Test]
        public void EveryBlockSaysWhatBreaksIt()
        {
            BlockRegistry blocks = TestContent.Blocks();
            Assert.That(blocks.Get("timber").Tool, Is.EqualTo(ToolClass.Axe));
            Assert.That(blocks.Get("bark").Tool, Is.EqualTo(ToolClass.Axe));
            Assert.That(blocks.Get("rust_rock").Tool, Is.EqualTo(ToolClass.Pick));
            Assert.That(blocks.Get("leaves").Tool, Is.EqualTo(ToolClass.Hand));
            for (int i = 1; i < blocks.Count; i++)
            {
                BlockDef b = blocks.Get(i);
                if (b.Solid && !b.Fluid && !b.Unbreakable) Assert.That(b.Health, Is.GreaterThan(0), b.Key + " needs health");
            }
        }

        [Test]
        public void YouCannotChopATreeWithBareHands()
        {
            BlockRegistry blocks = TestContent.Blocks();
            ToolSet tools = Tools();
            BlockDef timber = blocks.Get("timber"), rock = blocks.Get("rust_rock"), soil = blocks.Get("red_soil");

            Assert.That(DigRules.CanBreak(timber, tools.Unarmed), Is.False);
            Assert.That(DigRules.WhyNot(timber, tools.Unarmed), Does.Contain("needs an axe"));
            Assert.That(DigRules.CanBreak(timber, tools.Get("flint_axe")), Is.True);
            Assert.That(DigRules.CanBreak(timber, tools.Get("flint_pick")), Is.False, "a pick is the wrong tool for wood");

            Assert.That(DigRules.CanBreak(rock, tools.Unarmed), Is.False);
            Assert.That(DigRules.CanBreak(rock, tools.Get("flint_axe")), Is.False);
            Assert.That(DigRules.CanBreak(rock, tools.Get("flint_pick")), Is.True);
            Assert.That(DigRules.CanBreak(blocks.Get("basalt"), tools.Get("flint_pick")), Is.False, "basalt needs a better pick");
            Assert.That(DigRules.WhyNot(blocks.Get("basalt"), tools.Get("flint_pick")), Does.Contain("too hard"));
            Assert.That(DigRules.CanBreak(blocks.Get("inscribed_stone"), ToolClass.Any, 1000), Is.False);

            Assert.That(DigRules.CanBreak(soil, tools.Unarmed), Is.True, "soil comes up by hand");
            Assert.That(DigRules.CanBreak(blocks.Get("leaves"), tools.Unarmed), Is.True);
        }

        [Test]
        public void TheRightToolIsFaster()
        {
            BlockRegistry blocks = TestContent.Blocks();
            ToolSet tools = Tools();
            BlockDef soil = blocks.Get("red_soil"), timber = blocks.Get("timber");
            double hands = DigRules.SecondsToBreak(soil, tools.Unarmed);
            double withAxe = DigRules.SecondsToBreak(soil, tools.Get("flint_axe"));
            Assert.That(hands, Is.EqualTo(2.0).Within(1e-9), "red soil: health 4 at 2 a second");
            Assert.That(withAxe, Is.LessThanOrEqualTo(hands), "a tool never digs slower than hands");
            Assert.That(DigRules.SecondsToBreak(timber, tools.Unarmed), Is.EqualTo(double.PositiveInfinity));
            double chop = DigRules.SecondsToBreak(timber, tools.Get("flint_axe"));
            Assert.That(chop, Is.GreaterThan(0.5).And.LessThan(3.0), "a flint axe takes a trunk bite in a second or two");
            // Power to spare digs faster: a pick of power 3 on rock of hardness 2.
            BlockDef rock = blocks.Get("rust_rock");
            Assert.That(DigRules.Rate(rock, tools.Get("flint_pick")), Is.EqualTo(6 * 1.25).Within(1e-9));
        }

        [Test]
        public void HoldingDigWearsTheBlockDown()
        {
            BlockRegistry blocks = TestContent.Blocks();
            ToolSet tools = Tools();
            var dig = new DigProgress();
            BlockDef soil = blocks.Get("red_soil");
            double t = 0;
            DigProgress.HitResult r = default(DigProgress.HitResult);
            int ticks = 0;
            do
            {
                t += 0.1;
                r = dig.Hit(1, 5, 5, 5, soil, tools.Unarmed, 0.1, t);
                ticks++;
            } while (r.Outcome == DigProgress.Outcome.Working && ticks < 100);
            Assert.That(r.Outcome, Is.EqualTo(DigProgress.Outcome.Broken));
            Assert.That(ticks, Is.InRange(19, 21), "two seconds of digging at ten ticks a second");

            // Looking away starts over.
            dig.Hit(1, 5, 5, 5, soil, tools.Unarmed, 1.0, 10);
            Assert.That(dig.FractionFor(1, blocks), Is.EqualTo(0.5).Within(1e-9));
            DigProgress.HitResult moved = dig.Hit(1, 6, 5, 5, soil, tools.Unarmed, 0.1, 10.1);
            Assert.That(moved.Fraction, Is.LessThan(0.1));

            // Damage left alone fades.
            DigProgress.HitResult late = dig.Hit(1, 6, 5, 5, soil, tools.Unarmed, 0.1, 20);
            Assert.That(late.Fraction, Is.LessThan(0.1));

            // Two players dig separately.
            dig.Hit(2, 0, 0, 0, soil, tools.Unarmed, 1.0, 30);
            Assert.That(dig.FractionFor(2, blocks), Is.EqualTo(0.5).Within(1e-9));

            // The wrong tool is refused with a reason.
            DigProgress.HitResult no = dig.Hit(1, 0, 0, 0, blocks.Get("timber"), tools.Unarmed, 0.1, 31);
            Assert.That(no.Outcome, Is.EqualTo(DigProgress.Outcome.Refused));
            Assert.That(no.Message, Does.Contain("axe"));
        }

        [Test]
        public void AnAxeBiteTakesTheTrunkAndLeavesTheStone()
        {
            BlockRegistry blocks = TestContent.Blocks();
            var world = new VoxelWorld(42, blocks);
            ushort timber = blocks.Id("timber"), rock = blocks.Id("rust_rock");
            const int y = 600;   // high in the empty sky, so only what we build is there
            world.Apply(EditOp.Box(0, y, 0, 0, y + 4, 0, timber));
            world.Apply(EditOp.Box(1, y, 0, 1, y + 4, 0, rock));
            EditResult r = world.Apply(EditOp.Sphere(0, y + 2, 0, 24, 0, 2).WithTool(ToolClass.Axe));
            Assert.That(r.Removed.ContainsKey(timber), Is.True);
            Assert.That(r.Removed.ContainsKey(rock), Is.False, "an axe does not cut stone");
            EditResult hands = world.Apply(EditOp.Box(0, y, 0, 1, y + 4, 0, 0, 1).WithTool(ToolClass.Hand));
            Assert.That(hands.Changed, Is.EqualTo(0), "bare hands break neither");
            EditResult pick = world.Apply(EditOp.Box(1, y, 0, 1, y + 4, 0, 0, 3).WithTool(ToolClass.Pick));
            Assert.That(pick.Removed[rock], Is.EqualTo(5));
        }

        [Test]
        public void ToolsSurviveTheJournalAndOldLinesStillRead()
        {
            var ops = new List<EditOp>
            {
                EditOp.Sphere(1, 2, 3, 24, 0, 2).WithTool(ToolClass.Axe),
                EditOp.Set(4, 5, 6, 0, 1).WithTool(ToolClass.Hand),
                EditOp.Box(0, 0, 0, 2, 2, 2, 0, 98),
            };
            Assert.That(ops[0].ToLine(), Is.EqualTo("sphere 1 2 3 24 0 2 axe"));
            Assert.That(ops[2].ToLine(), Is.EqualTo("box 0 0 0 2 2 2 0 98"), "world edits keep the old line");
            foreach (EditOp op in ops)
            {
                EditOp back;
                Assert.That(EditOp.TryParse(op.ToLine(), out back), Is.True, op.ToLine());
                Assert.That(back.Tool, Is.EqualTo(op.Tool));
                Assert.That(back.ToLine(), Is.EqualTo(op.ToLine()));
            }
            EditOp bad;
            Assert.That(EditOp.TryParse("set 1 2 3 0 1 spoon", out bad), Is.False);
        }
    }
}
