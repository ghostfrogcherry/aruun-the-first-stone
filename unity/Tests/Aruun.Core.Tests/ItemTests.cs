using System;
using System.Collections.Generic;
using System.IO;
using Aruun.Core.Content;
using Aruun.Core.Items;
using NUnit.Framework;

namespace Aruun.Core.Tests
{
    public class ItemTests
    {
        static ItemRegistry Items()
        {
            return ItemRegistry.FromJson(File.ReadAllText(Path.Combine(TestContent.Root, "items", "items.json")), TestContent.Blocks());
        }

        static readonly string[] None = new string[0];

        [Test]
        public void TheRealItemsFileLoads()
        {
            ItemRegistry items = Items();
            Assert.That(items.Get("flint_pick").Wears, Is.True);
            Assert.That(items.Get("rust_rock").Kind, Is.EqualTo("block"), "dug blocks are items");
            Assert.That(items.Has("inscribed_stone"), Is.False, "unbreakable blocks are not items");
            Assert.That(items.Has("water"), Is.False);
            Assert.That(items.Recipe("flint_pick"), Is.Not.Null);
            Assert.That(items.StationStructure("fire").Key, Is.EqualTo("campfire"));
        }

        [Test]
        public void ItemsStackAndSpill()
        {
            ItemRegistry items = Items();
            var bag = new Inventory(items);
            Assert.That(bag.Add("flint", 150), Is.EqualTo(0));
            Assert.That(bag.Count("flint"), Is.EqualTo(150));
            Assert.That(bag[0].Value.Count, Is.EqualTo(99));
            Assert.That(bag[1].Value.Count, Is.EqualTo(51));
            Assert.That(bag.Add("flint_pick", 2), Is.EqualTo(0));
            Assert.That(bag[2].Value.Durability, Is.EqualTo(260), "tools start whole and do not stack");
            Assert.That(bag[3].Value.Key, Is.EqualTo("flint_pick"));
            Assert.That(bag.Add("no_such_thing", 3), Is.EqualTo(3));

            Assert.That(bag.Remove("flint", 60), Is.True);
            Assert.That(bag[0].Value.Count, Is.EqualTo(90), "the back of the bag empties first");
            Assert.That(bag[1].HasValue, Is.False);
            Assert.That(bag.Remove("flint", 1000), Is.False);
            Assert.That(bag.Count("flint"), Is.EqualTo(90));
        }

        [Test]
        public void AFullBagSaysHowManyDidNotFit()
        {
            var bag = new Inventory(Items());
            for (int i = 0; i < Inventory.Size; i++) bag.Add("flint_axe", 1);
            Assert.That(bag.Add("flint", 5), Is.EqualTo(5));
        }

        [Test]
        public void ToolsWearOutAndBreak()
        {
            var bag = new Inventory(Items());
            bag.Add("flint_axe", 1, 2);
            Assert.That(bag.Wear(0), Is.False);
            Assert.That(bag[0].Value.Durability, Is.EqualTo(1));
            Assert.That(bag.Wear(0), Is.True);
            Assert.That(bag[0].HasValue, Is.False);
        }

        [Test]
        public void MovingMergesOrSwaps()
        {
            var bag = new Inventory(Items());
            bag.Add("flint", 99);
            bag.Add("cord", 3);
            bag.Move(1, 5);
            Assert.That(bag.KeyAt(5), Is.EqualTo("cord"));
            bag.Add("flint", 10);  // fills slot 1 again
            Assert.That(bag.Move(1, 0), Is.False, "a full stack takes no more");
            bag.Remove("flint", 5);     // from slot 1, which keeps 5
            Assert.That(bag.Move(0, 1), Is.True, "part of a stack tops up another");
            Assert.That(bag[1].Value.Count, Is.EqualTo(99));
            Assert.That(bag[0].Value.Count, Is.EqualTo(5));
            Assert.That(bag.Count("flint"), Is.EqualTo(104));
            Assert.That(bag.Move(0, 5), Is.True);
            Assert.That(bag.KeyAt(0), Is.EqualTo("cord"), "different items swap");
        }

        [Test]
        public void TheBagSurvivesSavingAndLoading()
        {
            ItemRegistry items = Items();
            var bag = new Inventory(items);
            bag.Add("flint", 12);
            bag.Add("flint_pick", 1, 40);
            bag.Move(1, 7);
            var back = new Inventory(items);
            back.FromText(bag.ToText() + "9 removed_item 3\nrubbish\n");
            Assert.That(back.ToText(), Is.EqualTo(bag.ToText()));
            Assert.That(back[7].Value.Durability, Is.EqualTo(40));
        }

        [Test]
        public void CraftingUsesUpMaterials()
        {
            ItemRegistry items = Items();
            var bag = new Inventory(items);
            bag.Add("plant_fibre", 4);
            Assert.That(Crafting.Craft(items, items.Recipe("cord"), bag, None, false), Is.EqualTo(Crafting.Ok));
            Assert.That(bag.Count("cord"), Is.EqualTo(1));
            Assert.That(bag.Count("plant_fibre"), Is.EqualTo(1));
            Assert.That(Crafting.Craft(items, items.Recipe("cord"), bag, None, false), Is.EqualTo("missing materials"));
        }

        [Test]
        public void SomeRecipesNeedAFireOrAWord()
        {
            ItemRegistry items = Items();
            var bag = new Inventory(items);
            bag.Add("redroot", 1);
            Assert.That(Crafting.Craft(items, items.Recipe("roast_redroot"), bag, None, false), Is.EqualTo("needs a campfire nearby"));
            Assert.That(Crafting.Craft(items, items.Recipe("roast_redroot"), bag, new[] { "fire" }, false), Is.EqualTo(Crafting.Ok));
            Assert.That(bag.Count("roast_redroot"), Is.EqualTo(1));
            bag.Add("rust_rock", 16);
            bag.Add("flint", 2);
            Assert.That(Crafting.Check(items, items.Recipe("tether_stone"), bag, None, false), Does.Contain("word of Avarra"));
            Assert.That(Crafting.Check(items, items.Recipe("tether_stone"), bag, None, true), Is.EqualTo(Crafting.Ok));
        }

        [Test]
        public void AFullBagNeverEatsTheMaterials()
        {
            ItemRegistry items = Items();
            var bag = new Inventory(items);
            for (int i = 0; i < Inventory.Size - 1; i++) bag.Add("flint_axe", 1);
            bag.Add("bark", 2);
            // One bark is used, so its slot stays taken and the timber has nowhere to go.
            Assert.That(Crafting.Craft(items, items.Recipe("timber"), bag, None, false), Is.EqualTo("your bag is full"));
            Assert.That(bag.Count("bark"), Is.EqualTo(2));
            // With one bark the slot frees up and the timber fits.
            bag.Remove("bark", 1);
            Assert.That(Crafting.Craft(items, items.Recipe("timber"), bag, None, false), Is.EqualTo(Crafting.Ok));
            Assert.That(bag.Count("timber"), Is.EqualTo(2));
        }

        [Test]
        public void DigDropsFollowTheTable()
        {
            ItemRegistry items = Items();
            BlockRegistry blocks = items.Blocks;
            Func<double> always = () => 0.0, never = () => 0.999;
            Assert.That(items.RollDrops(blocks.Get("rust_rock"), never)[0].Key, Is.EqualTo("rust_rock"), "plain blocks give themselves");
            Assert.That(items.RollDrops(blocks.Get("grass"), never)[0].Key, Is.EqualTo("red_soil"));
            Assert.That(items.RollDrops(blocks.Get("tall_grass"), always)[0].Key, Is.EqualTo("plant_fibre"));
            Assert.That(items.RollDrops(blocks.Get("tall_grass"), never), Is.Empty);
            Assert.That(items.RollDrops(blocks.Get("gold_flower"), always), Is.Empty);
        }

        [Test]
        public void TheFirstPickCanBeMadeWithBareHands()
        {
            // Everything in the flint pick comes from things hands can gather: flint nodes,
            // reed stalks and plant fibre. If a recipe change breaks this, the game soft-locks.
            ItemRegistry items = Items();
            var handMade = new HashSet<string> { "flint", "reed_stalk", "plant_fibre", "red_soil", "moss", "dust_sand" };
            foreach (var r in items.Recipes)
                if (r.Station.Length == 0 && !r.NeedsWord)
                {
                    bool ok = true;
                    foreach (var kv in r.In) ok &= handMade.Contains(kv.Key);
                    if (ok) handMade.Add(r.Out);
                }
            // A second pass picks up recipes that use the first pass's outputs (cord).
            foreach (var r in items.Recipes)
                if (r.Station.Length == 0 && !r.NeedsWord)
                {
                    bool ok = true;
                    foreach (var kv in r.In) ok &= handMade.Contains(kv.Key);
                    if (ok) handMade.Add(r.Out);
                }
            Assert.That(handMade, Does.Contain("flint_pick"));
            Assert.That(handMade, Does.Contain("flint_axe"));
        }
    }
}
