using System.Collections.Generic;
using System.IO;
using Aruun.Core.Items;
using Aruun.Core.Survival;
using NUnit.Framework;

namespace Aruun.Core.Tests
{
    public class SurvivalTests
    {
        static string RulesJson { get { return File.ReadAllText(Path.Combine(TestContent.Root, "survival", "rulesets.json")); } }

        static ItemRegistry Items()
        {
            return ItemRegistry.FromJson(File.ReadAllText(Path.Combine(TestContent.Root, "items", "items.json")), TestContent.Blocks());
        }

        static HashSet<string> Flags(params string[] f) { return new HashSet<string>(f); }

        [Test]
        public void HardcoreIsLightPlusItsChanges()
        {
            Ruleset light = Ruleset.Load(RulesJson, "light"), hard = Ruleset.Load(RulesJson, "hardcore");
            Assert.That(hard.Name, Is.EqualTo("Hardcore"));
            Assert.That(hard.Num("vitals", "hp"), Is.EqualTo(light.Num("vitals", "hp")), "inherited");
            Assert.That(hard.Num("vitals", "hp_regen"), Is.EqualTo(0.0), "overridden");
            Assert.That(hard.Condition("hunger").Bands.Count, Is.EqualTo(light.Condition("hunger").Bands.Count + 2), "bands_add appends");
            Assert.That(light.Condition("hunger").Bands.Count, Is.EqualTo(1), "merging never changes light");
            Assert.That(Ruleset.Load(RulesJson, "no_such").Id, Is.EqualTo("light"), "unknown falls back to the default");
            Assert.That(hard.Buffs.ContainsKey("warm_meal"), Is.True);
        }

        [Test]
        public void ColdRisesAtNightOutsideAndFallsByTheFire()
        {
            Ruleset rules = Ruleset.Load(RulesJson, "light");
            var values = Conditions.StartValues(rules);
            Conditions.Tick(values, rules, Flags("night", "exposed"), new string[0], 60);
            Assert.That(values["cold"], Is.EqualTo(14).Within(1e-9));
            TickResult r = Conditions.Tick(values, rules, Flags("night", "exposed"), new string[0], 180);
            Assert.That(values["cold"], Is.GreaterThanOrEqualTo(50));
            Assert.That(r.Bands.Exists(b => b.Condition == "cold" && !b.Good), Is.True, "chilled");
            Assert.That(r.Mods["stamina_regen_mult"], Is.LessThan(1.0));
            Conditions.Tick(values, rules, Flags("night", "exposed", "near_heat"), new string[0], 120);
            Assert.That(values["cold"], Is.EqualTo(0), "a fire warms you and severity never goes below 0");
        }

        [Test]
        public void BuffsAddFlagsModsAndDrift()
        {
            Ruleset rules = Ruleset.Load(RulesJson, "light");
            var values = Conditions.StartValues(rules);
            values["cold"] = 40;
            var flags = Flags("day");
            TickResult r = Conditions.Tick(values, rules, flags, new[] { "warm_meal", "calm" }, 60);
            Assert.That(flags, Does.Contain("calm"));
            Assert.That(values["cold"], Is.EqualTo(40 - 20 - 8).Within(1e-9), "day warming plus the warm meal");
            Assert.That(r.Mods["stamina_regen_mult"], Is.EqualTo(1.1 * 1.05 * 1.15 * 1.15).Within(1e-9), "meal, calm, well fed, hydrated");
        }

        [Test]
        public void HardcoreStarvationHurts()
        {
            Ruleset hard = Ruleset.Load(RulesJson, "hardcore");
            var values = Conditions.StartValues(hard);
            values["hunger"] = 95;
            TickResult r = Conditions.Tick(values, hard, Flags("day"), new string[0], 6);
            Assert.That(r.Damage, Is.EqualTo(1.0).Within(1e-9), "10 a minute for 6 seconds");
            Ruleset light = Ruleset.Load(RulesJson, "light");
            values = Conditions.StartValues(light);
            values["hunger"] = 95;
            Assert.That(Conditions.Tick(values, light, Flags("day"), new string[0], 6).Damage, Is.EqualTo(0), "light only rewards");
        }

        [Test]
        public void TheEarthsWeightComesBackInOcclusionSites()
        {
            Ruleset rules = Ruleset.Load(RulesJson, "light");
            var values = Conditions.StartValues(rules);
            TickResult r = Conditions.Tick(values, rules, Flags("day", "earth_gravity"), new string[0], 5);
            Assert.That(r.Mods["move_speed_mult"], Is.LessThan(1.0));
            Assert.That(r.Mods["debt_cost_mult"], Is.EqualTo(1.5));
        }

        [Test]
        public void AFreshCastawayGetsTheKitAndWords()
        {
            Ruleset rules = Ruleset.Load(RulesJson, "light");
            var c = new CharacterState(Items(), rules);
            c.SetupNew();
            Assert.That(c.Hp, Is.EqualTo(100));
            Assert.That(c.Inv.Count("redroot"), Is.EqualTo(3));
            Assert.That(c.Words, Does.Contain("word_push"));
            Assert.That(c.Cond["hunger"], Is.EqualTo(25));
            Assert.That(c.HeldTool.Key, Is.EqualTo("unarmed"), "redroot in hand digs like bare hands");
        }

        [Test]
        public void EatingFeedsAndWarms()
        {
            var c = new CharacterState(Items(), Ruleset.Load(RulesJson, "light"));
            c.SetupNew();
            c.Cond["hunger"] = 80;
            Assert.That(c.Use("roast_meat"), Is.True);
            Assert.That(c.Cond["hunger"], Is.EqualTo(30));
            Assert.That(c.Buffs.ContainsKey("warm_meal"), Is.True);
            Assert.That(c.Use("flint"), Is.False);
        }

        [Test]
        public void StaminaSpendsAndComesBack()
        {
            var c = new CharacterState(Items(), Ruleset.Load(RulesJson, "light"));
            c.SetupNew();
            Assert.That(c.Spend(150), Is.True, "any stamina left is enough");
            Assert.That(c.Stamina, Is.EqualTo(0));
            Assert.That(c.Spend(1), Is.False);
            c.Tick(Flags("day"), 0.5);
            Assert.That(c.Stamina, Is.EqualTo(0), "a short pause before it comes back");
            for (int i = 0; i < 10; i++) c.Tick(Flags("day"), 0.5);
            Assert.That(c.Stamina, Is.GreaterThan(50));
        }

        [Test]
        public void TheDustMaskKeepsDustOut()
        {
            ItemRegistry items = Items();
            Ruleset rules = Ruleset.Load(RulesJson, "light");
            var bare = new CharacterState(items, rules);
            var masked = new CharacterState(items, rules);
            bare.SetupNew();
            masked.SetupNew();
            masked.Inv.Add("dust_mask", 1);
            bare.Tick(Flags("day", "dusty", "exposed"), 30);
            masked.Tick(Flags("day", "dusty", "exposed"), 30);
            Assert.That(bare.Cond["dust"], Is.GreaterThan(0));
            Assert.That(masked.Cond["dust"], Is.EqualTo(0));
        }

        [Test]
        public void ACharacterSavesAndLoads()
        {
            ItemRegistry items = Items();
            Ruleset rules = Ruleset.Load(RulesJson, "light");
            var c = new CharacterState(items, rules) { Guid = "abc", DisplayName = "Ren Okafor" };
            c.SetupNew();
            c.Inv.Add("flint_pick", 1, 77);
            c.Selected = 3;
            c.Cond["cold"] = 33.5;
            c.AddBuff("warm_meal");
            c.Tether = new[] { 10, -4, 99 };
            c.Hp = 61.25;
            var back = new CharacterState(items, rules);
            back.FromText(c.ToText() + "cond retired_condition 50\nbuff no_buff 3\n");
            Assert.That(back.ToText(), Is.EqualTo(c.ToText()));
            Assert.That(back.DisplayName, Is.EqualTo("Ren Okafor"));
            Assert.That(back.Cond.ContainsKey("retired_condition"), Is.False);
        }

        [Test]
        public void DayAndNight()
        {
            Assert.That(DayClock.TimeOfDay(0, 1200), Is.EqualTo(0.3).Within(1e-9), "worlds start in the morning");
            Assert.That(DayClock.IsNight(DayClock.TimeOfDay(0, 1200)), Is.False);
            Assert.That(DayClock.IsNight(DayClock.TimeOfDay(600, 1200)), Is.True, "half a day later it is night");
            Assert.That(DayClock.DayNumber(0, 1200), Is.EqualTo(1));
            Assert.That(DayClock.DayNumber(1000, 1200), Is.EqualTo(2), "a new day starts at midnight");
        }
    }
}
