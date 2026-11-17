using System.IO;
using Aruun.Core.Combat;
using Aruun.Core.Farming;
using Aruun.Core.Items;
using Aruun.Core.Survival;
using NUnit.Framework;

namespace Aruun.Core.Tests
{
    public class CombatTests
    {
        static ItemRegistry Items()
        {
            return ItemRegistry.FromJson(File.ReadAllText(Path.Combine(TestContent.Root, "items", "items.json")), TestContent.Blocks());
        }

        static Ruleset Light()
        {
            return Ruleset.Load(File.ReadAllText(Path.Combine(TestContent.Root, "survival", "rulesets.json")), "light");
        }

        [Test]
        public void HeavyBlowsHitHarderAndCostMore()
        {
            ItemRegistry items = Items();
            var mods = new Mods();
            AttackProfile light = CombatRules.Profile(items, "fallhammer", false, mods);
            AttackProfile heavy = CombatRules.Profile(items, "fallhammer", true, mods);
            Assert.That(light.Damage, Is.EqualTo(30));
            Assert.That(heavy.Damage, Is.EqualTo(57).Within(1e-9));
            Assert.That(heavy.Stamina, Is.GreaterThan(light.Stamina));
            Assert.That(heavy.Windup, Is.GreaterThan(light.Windup));
            Assert.That(light.Reach, Is.EqualTo(2.3), "metres straight from the file");
            Assert.That(CombatRules.Profile(items, "redroot", false, mods).Damage, Is.EqualTo(5), "food hits like a fist");
            Assert.That(CombatRules.Profile(items, "", false, mods).Damage, Is.EqualTo(5));
        }

        [Test]
        public void ASwingHitsWhatIsInFront()
        {
            var o = Vec3.Zero;
            var fwd = Vec3.Forward;
            Assert.That(CombatRules.InArc(o, fwd, new Vec3(0, 0, 1.5), 2, 80), Is.True);
            Assert.That(CombatRules.InArc(o, fwd, new Vec3(0, 0, -1.5), 2, 80), Is.False, "behind");
            Assert.That(CombatRules.InArc(o, fwd, new Vec3(0, 0, 3), 2, 80), Is.False, "out of reach");
            Assert.That(CombatRules.InArc(o, fwd, new Vec3(1.5, 0, 0.5), 2, 80), Is.False, "too far to the side");
            Assert.That(CombatRules.InArc(o, fwd, new Vec3(1.5, 0, 0.5), 2, 80, 1.0), Is.True, "a big body is easier to hit");
            Assert.That(CombatRules.InArc(o, fwd, new Vec3(0, 3, 1), 2, 80), Is.False, "too high");
        }

        [Test]
        public void GuardingSoaksDamageUntilItBreaks()
        {
            GuardResult held = CombatRules.Guard(20, 0.6, true, 100);
            Assert.That(held.Damage, Is.EqualTo(8).Within(1e-9));
            Assert.That(held.StaminaCost, Is.EqualTo(22).Within(1e-9));
            Assert.That(held.Broken, Is.False);
            GuardResult broke = CombatRules.Guard(20, 0.6, true, 11);
            Assert.That(broke.Broken, Is.True);
            Assert.That(broke.Damage, Is.EqualTo(20 * (1 - 0.6 * 0.5)).Within(1e-9));
            Assert.That(CombatRules.Guard(20, 0.6, false, 100).Damage, Is.EqualTo(20), "a guard does nothing from behind");
            Assert.That(CombatRules.IsFacing(Vec3.Zero, Vec3.Forward, new Vec3(0, 0, 5)), Is.True);
            Assert.That(CombatRules.IsFacing(Vec3.Zero, Vec3.Forward, new Vec3(0, 0, -5)), Is.False);
        }

        [Test]
        public void LongFallsHurtAndDebtMakesThemWorse()
        {
            Ruleset rules = Light();
            Assert.That(CombatRules.FallDamage(6, rules), Is.EqualTo(0), "a short hop is safe");
            double hurt = CombatRules.FallDamage(12, rules);
            Assert.That(hurt, Is.EqualTo((12 - 7.0) * 6.5).Within(1e-9));
            Assert.That(CombatRules.FallDamage(12, rules, 100), Is.GreaterThan(hurt));
        }

        [Test]
        public void KnockbackPushesAwayAndUp()
        {
            Vec3 v = CombatRules.Knockback(Vec3.Zero, new Vec3(2, 0, 0), 4, 2);
            Assert.That(v.X, Is.EqualTo(2).Within(1e-9));
            Assert.That(v.Y, Is.GreaterThan(0));
            Assert.That(v.Z, Is.EqualTo(0).Within(1e-9));
        }
    }

    public class FarmingTests
    {
        static Plants Load()
        {
            return Plants.FromJson(File.ReadAllText(Path.Combine(TestContent.Root, "farming", "plants.json")));
        }

        [Test]
        public void CropsGrowFromWhenTheyWerePlanted()
        {
            Plants plants = Load();
            PlantDef redroot = plants.Get("redroot");
            Assert.That(Plants.Stage(redroot, 100, 100), Is.EqualTo(0));
            Assert.That(Plants.Stage(redroot, 100, 100 + 90), Is.EqualTo(1));
            Assert.That(Plants.IsRipe(redroot, 100, 100 + 269), Is.False);
            Assert.That(Plants.IsRipe(redroot, 100, 100 + 270), Is.True);
            Assert.That(Plants.IsRipe(redroot, 100, 100 + 180, 1.5), Is.True, "a Tether makes it grow faster");
            Assert.That(Plants.Stage(redroot, 0, 1e9), Is.EqualTo(3), "never past ripe");
        }

        [Test]
        public void CropsNeedTheRightGround()
        {
            Plants plants = Load();
            Assert.That(Plants.CanPlant(plants.Get("redroot"), "red_soil"), Is.True);
            Assert.That(Plants.CanPlant(plants.Get("redroot"), "rust_rock"), Is.False);
            Assert.That(Plants.CanPlant(plants.Get("glasswort"), "rust_rock"), Is.True);
        }

        [Test]
        public void HarvestingYieldsAndRegrows()
        {
            Plants plants = Load();
            PlantDef reed = plants.Get("vermillion_reed");
            var got = Plants.Harvest(reed, (lo, hi) => hi);
            Assert.That(got[0].Key, Is.EqualTo("reed_stalk"));
            Assert.That(got[0].Value, Is.EqualTo(4));
            Assert.That(Plants.Harvest(reed, (lo, hi) => lo).Count, Is.EqualTo(1), "a zero roll gives nothing");
            double? again = Plants.RegrowTime(reed, 1000);
            Assert.That(Plants.Stage(reed, again.Value, 1000), Is.EqualTo(1), "back to stage one");
            Assert.That(Plants.RegrowTime(plants.Get("redroot"), 1000), Is.Null, "redroot is pulled up");
        }

        [Test]
        public void EverySeedGrowsARealPlantAndEveryYieldIsAnItem()
        {
            Plants plants = Load();
            ItemRegistry items = ItemRegistry.FromJson(File.ReadAllText(Path.Combine(TestContent.Root, "items", "items.json")), TestContent.Blocks());
            foreach (PlantDef p in plants.All)
                foreach (Yield y in p.Yields)
                    Assert.That(items.Has(y.Item), Is.True, p.Key + " yields unknown " + y.Item);
            ItemDef seed = items.Get("redroot_seed");
            Assert.That(plants.Get((string)seed.Raw["plant"]), Is.Not.Null);
            Assert.That(plants.Get("flint_nodule").Node, Is.True);
        }
    }
}

namespace Aruun.Core.Tests
{
    using System.Collections.Generic;
    using Aruun.Core.Worldgen;

    public class WildSpawnTests
    {
        static Plants Load()
        {
            return Plants.FromJson(File.ReadAllText(Path.Combine(TestContent.Root, "farming", "plants.json")));
        }

        [Test]
        public void TheSameSeedGivesTheSameWildNodes()
        {
            Plants plants = Load();
            var a = WildSpawns.InCell(new HeightField(42), plants, 3, -2);
            var b = WildSpawns.InCell(new HeightField(42), plants, 3, -2);
            Assert.That(b.Count, Is.EqualTo(a.Count));
            for (int i = 0; i < a.Count; i++)
            {
                Assert.That(b[i].Id, Is.EqualTo(a[i].Id));
                Assert.That(b[i].X, Is.EqualTo(a[i].X));
            }
        }

        [Test]
        public void WildNodesSitOnTheGroundInTheirOwnCellAndPlace()
        {
            Plants plants = Load();
            var field = new HeightField(42);
            int flint = 0, total = 0;
            var ids = new HashSet<string>();
            for (int cx = -6; cx < 6; cx++)
                for (int cz = -6; cz < 6; cz++)
                    foreach (WildNode n in WildSpawns.InCell(field, plants, cx, cz))
                    {
                        total++;
                        if (n.Plant == "flint_nodule") flint++;
                        Assert.That(ids.Add(n.Id), Is.True, "ids are unique");
                        Assert.That(WildSpawns.CellOf(n.X, plants.CellVoxels), Is.EqualTo(cx));
                        Assert.That(WildSpawns.CellOf(n.Z, plants.CellVoxels), Is.EqualTo(cz));
                        Assert.That(n.Y, Is.EqualTo(field.HeightAt(n.X, n.Z) + 1));
                        Assert.That(WildSpawns.Fits(field, plants.Get(n.Plant).WildWhere, n.X, n.Z), Is.True);
                    }
            Assert.That(flint, Is.GreaterThan(40), "flint is easy to find, since the first tools need it");
            Assert.That(total, Is.GreaterThan(flint));
        }

        [Test]
        public void CellsWorkForNegativeCoordinates()
        {
            Assert.That(WildSpawns.CellOf(-1, 64), Is.EqualTo(-1));
            Assert.That(WildSpawns.CellOf(-64, 64), Is.EqualTo(-1));
            Assert.That(WildSpawns.CellOf(-65, 64), Is.EqualTo(-2));
            Assert.That(WildSpawns.CellOf(63, 64), Is.EqualTo(0));
        }
    }
}
