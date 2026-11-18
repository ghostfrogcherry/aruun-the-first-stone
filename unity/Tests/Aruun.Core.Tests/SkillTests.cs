using System.IO;
using System.Linq;
using Aruun.Core.Combat;
using Aruun.Core.Creatures;
using Aruun.Core.Edits;
using Aruun.Core.Farming;
using Aruun.Core.Items;
using Aruun.Core.Language;
using Aruun.Core.Play;
using Aruun.Core.Skills;
using Aruun.Core.Survival;
using Aruun.Core.Voxels;
using NUnit.Framework;

namespace Aruun.Core.Tests
{
    /// <summary>The Graviturge tree (content/skills/graviturge.json): points, unlocks, keystones, saves, and the Fallhammer slam.</summary>
    public class SkillTests
    {
        const int Floor = 400;

        static SkillTree LoadTree() { return SkillTree.FromJson(File.ReadAllText(Path.Combine(TestContent.Root, "skills", "graviturge.json"))); }

        static Host NewHost(out PlayerRec p, out ItemRegistry items, out Ruleset rules)
        {
            var blocks = TestContent.Blocks();
            var world = new VoxelWorld(20261116, blocks, TestContent.Map());
            items = ItemRegistry.FromJson(File.ReadAllText(Path.Combine(TestContent.Root, "items", "items.json")), blocks);
            rules = Ruleset.Load(File.ReadAllText(Path.Combine(TestContent.Root, "survival", "rulesets.json")), "light");
            var plants = Plants.FromJson(File.ReadAllText(Path.Combine(TestContent.Root, "farming", "plants.json")));
            var creatures = CreatureDef.FromJson(File.ReadAllText(Path.Combine(TestContent.Root, "creatures", "creatures.json")));
            var host = new Host(world, items, rules, plants, creatures, 1) { SpawnCreatures = false };
            host.Lexicon = Lexicon.FromJson(File.ReadAllText(Path.Combine(TestContent.Root, "language", "lexicon.json")));
            host.Tree = LoadTree();
            for (int bx = -60; bx < 60; bx += 60)
                for (int bz = -60; bz < 60; bz += 60)
                    world.Apply(EditOp.Box(bx, Floor - 3, bz, bx + 59, Floor, bz + 59, blocks.Id("red_soil")));
            p = host.Join(1, new CharacterState(items, rules), Feet(0, 0), true);
            host.SpawnPoint = Feet(0, 0);
            return host;
        }

        static Vec3 Feet(double x, double z) { return new Vec3(x, (Floor + 1) / 4.0, z); }
        static void Run(Host h, double seconds) { for (double t = 0; t < seconds; t += 0.05) h.Tick(0.05); }

        [Test]
        public void TheTreeIsSmallWholeAndHasOneRoot()
        {
            SkillTree t = LoadTree();
            Assert.That(t.Validate(), Is.Empty);
            Assert.That(t.Nodes.Count, Is.InRange(10, 15), "the roadmap asks for a 10-15 node tree");
            Assert.That(t.Nodes.Count(n => n.Requires.Count == 0), Is.EqualTo(1));
            Assert.That(t.Nodes.Count(n => n.Keystone), Is.GreaterThanOrEqualTo(2));
            foreach (var n in t.Nodes) Assert.That(n.Mods, Is.Not.Empty, n.Id + " changes nothing");
        }

        [Test]
        public void PointsBuyNodesInOrderAndOnlyOneKeystone()
        {
            var h = NewHost(out var p, out _, out _);
            Assert.That(p.C.SkillPoints, Is.EqualTo(h.Tree.StartPoints));
            Assert.That(h.LearnSkill(1, "heavy_hand"), Does.Contain("first"));
            Assert.That(h.LearnSkill(1, "sense_of_down"), Is.Empty);
            Assert.That(h.LearnSkill(1, "heavy_hand"), Does.Contain("point"), "no points left");
            p.C.SkillPoints = 20;
            foreach (string id in new[] { "heavy_hand", "wide_fall", "deep_impact", "borrowed_weight", "patient_ledger", "light_tongue", "steady_breath" })
                Assert.That(h.LearnSkill(1, id), Is.Empty, id);
            Assert.That(h.LearnSkill(1, "deep_credit"), Does.Contain("keystone"));
            Assert.That(p.C.Skills, Does.Not.Contain("deep_credit"));
        }

        [Test]
        public void LearnedNodesChangeTheCharacter()
        {
            var h = NewHost(out var p, out _, out _);
            Run(h, 0.1);
            double hp = p.C.HpMax;
            p.C.SkillPoints = 10;
            foreach (string id in new[] { "sense_of_down", "soft_landing", "sure_footing", "rooted" }) h.LearnSkill(1, id);
            Run(h, 0.1);
            Assert.That(p.C.HpMax, Is.EqualTo(hp * 1.15).Within(1e-6));
            Assert.That(p.C.Mods["debt_cost_mult"], Is.EqualTo(0.9).Within(1e-9));
            Assert.That(p.C.Mods["fall_damage_mult"], Is.EqualTo(0.6).Within(1e-9));
        }

        [Test]
        public void SoftLandingTakesTheSameFallMoreGently()
        {
            var h = NewHost(out var p, out _, out _);
            double before = p.C.Hp;
            h.ReportFall(1, 16);
            double plain = before - p.C.Hp;
            Assert.That(plain, Is.GreaterThan(0));
            p.C.Hp = before;
            p.C.SkillPoints = 2;
            h.LearnSkill(1, "sense_of_down");
            h.LearnSkill(1, "soft_landing");
            Run(h, 0.05);
            before = p.C.Hp;
            h.ReportFall(1, 16);
            Assert.That(before - p.C.Hp, Is.EqualTo(plain * 0.6).Within(0.5));
        }

        [Test]
        public void PatientLedgerRepaysDebtFaster()
        {
            var h = NewHost(out var p, out _, out _);
            var other = h.Join(2, new CharacterState(h.Items, h.Rules), Feet(4, 0), true);
            p.C.SkillPoints = 2;
            h.LearnSkill(1, "sense_of_down");
            h.LearnSkill(1, "patient_ledger");
            p.C.Cond["gravity_debt"] = 60;
            other.C.Cond["gravity_debt"] = 60;
            Run(h, 30);
            double mine = 60 - p.C.Cond["gravity_debt"], theirs = 60 - other.C.Cond["gravity_debt"];
            Assert.That(theirs, Is.GreaterThan(1));
            Assert.That(mine / theirs, Is.EqualTo(1.3).Within(0.05));
        }

        [Test]
        public void LearningAWordEarnsAPointAndSaysSo()
        {
            var h = NewHost(out var p, out _, out _);
            int before = p.C.SkillPoints;
            string word = h.Lexicon.Words.First().Id;
            p.C.Learn(word);
            p.C.Learn(word);   // a word known already earns nothing
            Run(h, 0.05);
            Assert.That(p.C.SkillPoints, Is.EqualTo(before + h.Tree.Award("word")));
            Assert.That(h.Messages.Any(m => m.Key == 1 && m.Value.Contains("skill point")), Is.True);
        }

        [Test]
        public void SkillsSurviveASaveAndOldSavesAreBackPaid()
        {
            var h = NewHost(out var p, out var items, out var rules);
            p.C.SkillPoints = 3;
            h.LearnSkill(1, "sense_of_down");
            h.LearnSkill(1, "patient_ledger");
            string text = p.C.ToText();
            var copy = new CharacterState(items, rules);
            copy.FromText(text);
            Assert.That(copy.Skills, Is.EquivalentTo(new[] { "sense_of_down", "patient_ledger" }));
            Assert.That(copy.SkillPoints, Is.EqualTo(1));

            // A save from before skill trees: two words known, no skill lines.
            var old = new CharacterState(items, rules);
            old.FromText("name Old\nword " + h.Lexicon.Words.First().Id + "\nword " + h.Lexicon.Words.ElementAt(1).Id + "\n");
            var q = h.Join(3, old, Feet(2, 2), false);
            Assert.That(q.C.SkillPoints, Is.EqualTo(h.Tree.StartPoints + 2 * h.Tree.Award("word")));
            Assert.That(q.C.NewPoints, Is.EqualTo(0), "back-pay is quiet");
        }

        [Test]
        public void AFallhammerSlamThrowsCreaturesAroundTheImpact()
        {
            var h = NewHost(out var p, out _, out _);
            p.C.Inv.Add("fallhammer", 1);
            for (int i = 0; i < Inventory.Size; i++) if (p.C.Inv.KeyAt(i) == "fallhammer") p.C.Selected = i;
            h.SetPose(1, Feet(0, 0), Vec3.Forward, 0);
            Run(h, 0.05);
            // Beside the impact point but well outside the swing's arc and reach.
            Creature c = h.SpawnCreature("redstrider", Feet(2.6, 1.6), 1);
            double hp = c.Hp;
            Assert.That(h.Attack(1, true), Is.True);
            for (int i = 0; i < 12; i++) { c.Pos = Feet(2.6, 1.6); h.Tick(0.05); }   // keep it still; it would rather run
            Assert.That(c.Hp, Is.LessThan(hp), "the slam's shockwave reaches it");
            Assert.That(h.Events.Any(e => e.Kind == "slam" && e.Creature == 0), Is.True);
        }

        [Test]
        public void BorrowedWeightSlamsCostDebt()
        {
            var h = NewHost(out var p, out _, out _);
            p.C.Inv.Add("fallhammer", 1);
            for (int i = 0; i < Inventory.Size; i++) if (p.C.Inv.KeyAt(i) == "fallhammer") p.C.Selected = i;
            p.C.SkillPoints = 10;
            foreach (string id in new[] { "sense_of_down", "heavy_hand", "wide_fall", "deep_impact", "borrowed_weight" }) Assert.That(h.LearnSkill(1, id), Is.Empty);
            h.SetPose(1, Feet(0, 0), Vec3.Forward, 0);
            Run(h, 0.05);
            p.C.Cond["gravity_debt"] = 0;
            h.Attack(1, true);
            for (int i = 0; i < 10; i++) { h.Time += 0.05; h.Tick(0.0001); }
            Assert.That(p.C.Cond["gravity_debt"], Is.GreaterThan(7));
        }
    }
}
