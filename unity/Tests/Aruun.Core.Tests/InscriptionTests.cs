using System;
using System.Collections.Generic;
using System.IO;
using Aruun.Core.Combat;
using Aruun.Core.Creatures;
using Aruun.Core.Edits;
using Aruun.Core.Farming;
using Aruun.Core.Inscription;
using Aruun.Core.Items;
using Aruun.Core.Language;
using Aruun.Core.Play;
using Aruun.Core.Ruins;
using Aruun.Core.Survival;
using Aruun.Core.Voxels;
using NUnit.Framework;

namespace Aruun.Core.Tests
{
    /// <summary>
    /// Inscriptions: Avarra words cut into equipment. The vertical slice (find Ulun, use the
    /// inscription table, cut it into a pick, make it ring and mine) is played through the
    /// Host on the real map, like a player would. The grammar is tested on its own.
    /// </summary>
    public class InscriptionTests
    {
        const int Seed = 20261116;

        static string Read(params string[] path) { return File.ReadAllText(Path.Combine(TestContent.Root, Path.Combine(path))); }
        static Lexicon Lex() { return Lexicon.FromJson(Read("language", "lexicon.json")); }
        static InscriptionRules Rules() { return InscriptionRules.FromJson(Read("language", "inscriptions.json")); }
        static ItemRegistry Items() { return ItemRegistry.FromJson(Read("items", "items.json"), TestContent.Blocks()); }

        static Host NewHost(out PlayerRec p, out EchoRuin ruin)
        {
            var blocks = TestContent.Blocks();
            var world = new VoxelWorld(Seed, blocks, TestContent.Map());
            var items = ItemRegistry.FromJson(Read("items", "items.json"), blocks);
            var rules = Ruleset.Load(Read("survival", "rulesets.json"), "light");
            var host = new Host(world, items, rules, Plants.FromJson(Read("farming", "plants.json")), CreatureDef.FromJson(Read("creatures", "creatures.json")), 1) { SpawnCreatures = false };
            host.Lexicon = Lex();
            host.Inscriptions = Rules();
            ruin = host.Ruin;
            Assert.That(ruin, Is.Not.Null);
            p = host.Join(1, new CharacterState(items, rules), ruin.CourtM, true);
            host.SpawnPoint = ruin.CourtM;
            return host;
        }

        static RuinInscription Ins(EchoRuin r, string place) { return r.Inscriptions.Find(i => i.Place == place); }
        static string Last(Host h) { return h.Messages.Count > 0 ? h.Messages[h.Messages.Count - 1].Value : ""; }
        static bool AnyMessage(Host h, string part) { return h.Messages.Exists(m => m.Value.Contains(part)); }

        static int Give(PlayerRec p, string key)
        {
            var had = new bool[Inventory.Size];
            for (int i = 0; i < Inventory.Size; i++) had[i] = p.C.Inv[i].HasValue;
            p.C.Inv.Add(key, 1);
            for (int i = 0; i < Inventory.Size; i++) if (!had[i] && p.C.Inv.KeyAt(i) == key) return i;
            return -1;
        }

        /// <summary>Stand in front of the table and press E on its worktop.</summary>
        static void UseTable(Host h, PlayerRec p, EchoRuin r)
        {
            p.Pos = r.TableM + new Vec3(-1.6, -1.0 - 0.0, 0);
            p.Pos = new Vec3(p.Pos.X, r.CourtM.Y, p.Pos.Z);
            Vec3 eye = p.Pos + new Vec3(0, 1.6, 0);
            Assert.That(h.Interact(p.Id, eye, (r.TableM - new Vec3(0, 0.1, 0)) - eye), Is.True, Last(h));
            Assert.That(h.AtTable(p.Id), Is.True, "standing at the table");
        }

        /// <summary>A solid cube of rust rock standing in the court, for digging.</summary>
        static int[] RockCube(Host h, EchoRuin r, double eastM)
        {
            Vec3 at = r.CourtM + new Vec3(eastM, 0, -4);
            int x = (int)Math.Floor(at.X * 4), y = (int)Math.Floor(at.Y * 4), z = (int)Math.Floor(at.Z * 4);
            h.World.Apply(EditOp.Box(x - 3, y, z - 3, x + 3, y + 6, z + 3, h.World.Blocks.Id("rust_rock")));
            return new[] { x, y + 3, z - 3 };   // the middle of the face toward the player
        }

        static int CountBlock(Host h, int[] c, int radius, ushort id)
        {
            int n = 0;
            for (int x = c[0] - radius; x <= c[0] + radius; x++)
                for (int y = c[1] - radius; y <= c[1] + radius; y++)
                    for (int z = c[2] - radius; z <= c[2] + radius + 6; z++)
                        if (h.World.Get(x, y, z) == id) n++;
            return n;
        }

        static DigTick DigOut(Host h, int[] c)
        {
            DigTick t = default;
            for (int i = 0; i < 400 && !t.Broken && !t.Refused; i++) { t = h.Dig(1, c[0], c[1], c[2], 0.05); h.Time += 0.05; }
            return t;
        }

        // ================================================================ content

        [Test]
        public void TheShippedContentIsSound()
        {
            Lexicon lex = Lex();
            Assert.That(lex.Validate(), Is.Empty);
            foreach (var w in lex.Words) Assert.That(w.Glyph.Length, Is.EqualTo(Lexicon.GlyphSize), w.Id + " has a glyph");
            InscriptionRules rules = Rules();
            Assert.That(rules.Validate(lex, Items()), Is.Empty);
            // All four kinds are in use.
            var kinds = new HashSet<InscriptionType>();
            foreach (var r in rules.WordRules) kinds.Add(r.Type);
            Assert.That(kinds, Is.EquivalentTo(new[] { InscriptionType.Passive, InscriptionType.Activated, InscriptionType.Conditional, InscriptionType.Compositional }));
            Assert.That(lex.Word("word_sonic").Speakable, Is.False, "Ulun is cut, not spoken");
            Assert.That(lex.Word("word_push").Display, Is.EqualTo("Vuh"), "Vuh is canon, no placeholder label");
        }

        [Test]
        public void BadRulesAreCaught()
        {
            var rules = InscriptionRules.FromJson(@"{""words"": [
                {""word"": ""word_sonic"", ""type"": ""activated"", ""fits"": [""pick""], ""effect"": ""resonant_mining"", ""stamina"": 5, ""cooldown_s"": 1, ""duration_s"": 5, ""params"": {""dig_mult"": 50, ""vein_max"": 10, ""vein_radius_m"": 1}},
                {""word"": ""word_rise"", ""type"": ""passive"", ""fits"": [""pick""], ""effect"": ""rm_rf""}],
              ""compositions"": [{""id"": ""x"", ""words"": [""word_sonic"", ""nonsense""], ""type"": ""compositional"", ""fits"": [""pick""]}],
              ""effects"": [{""id"": ""resonant_mining"", ""max_dig_mult"": 4, ""max_vein_max"": 64, ""max_vein_radius_m"": 3, ""max_duration_s"": 30}]}");
            var problems = rules.Validate(Lex(), null);
            Assert.That(problems, Has.Some.Contains("dig_mult out of bounds"));
            Assert.That(problems, Has.Some.Contains("unknown effect 'rm_rf'"));
            Assert.That(problems, Has.Some.Contains("unknown word 'nonsense'"));
            Assert.That(problems, Has.Some.Contains("must say what the words make"));
            Assert.Throws<FormatException>(() => InscriptionRules.FromJson(@"{""words"": [{""word"": ""a"", ""type"": ""magic""}]}"));
        }

        // ================================================================ grammar

        [Test]
        public void WordsResolveByItemAndCombination()
        {
            InscriptionRules rules = Rules();
            Resolved pick = Inscriber.Resolve(rules, new[] { "word_sonic" }, "pick");
            Assert.That(pick.Active.Count, Is.EqualTo(1));
            Assert.That(pick.Active[0].Type, Is.EqualTo(InscriptionType.Activated));
            Assert.That(pick.Active[0].Effect, Is.EqualTo("resonant_mining"));

            Resolved vuhAlone = Inscriber.Resolve(rules, new[] { "word_push" }, "hammer");
            Assert.That(vuhAlone.Any, Is.False, "a compositional word does nothing alone");
            Assert.That(vuhAlone.Waiting, Is.EquivalentTo(new[] { "word_push" }));

            // Order does not matter, and the pair is one effect, not two.
            foreach (var words in new[] { new[] { "word_sonic", "word_push" }, new[] { "word_push", "word_sonic" } })
            {
                Resolved hammer = Inscriber.Resolve(rules, words, "hammer");
                Assert.That(hammer.Active.Count, Is.EqualTo(1));
                Assert.That(hammer.Active[0].Id, Is.EqualTo("resonant_strike"));
                Assert.That(hammer.Waiting, Is.Empty);
            }
            Resolved ulunOnHammer = Inscriber.Resolve(rules, new[] { "word_sonic" }, "hammer");
            Assert.That(ulunOnHammer.Any, Is.False, "Ulun alone does not suit a hammer: it waits for Vuh");

            Resolved mending = Inscriber.Resolve(rules, new[] { "word_fortify", "word_reclaim" }, "axe");
            Assert.That(mending.Active[0].Id, Is.EqualTo("self_mending"));
            Assert.That(mending.Active[0].Type, Is.EqualTo(InscriptionType.Conditional));
            Assert.That(Inscriber.Resolve(rules, new[] { "word_fortify" }, "axe").Active[0].Type, Is.EqualTo(InscriptionType.Passive));

            Resolved junk = Inscriber.Resolve(rules, new[] { "System.IO.File.Delete", "word_sonic" }, "pick");
            Assert.That(junk.Active.Count, Is.EqualTo(1), "unknown ids do nothing");
            Assert.That(junk.Waiting, Is.EquivalentTo(new[] { "System.IO.File.Delete" }));
        }

        [Test]
        public void CompatibilityIsChecked()
        {
            InscriptionRules rules = Rules();
            Lexicon lex = Lex();
            ItemRegistry items = Items();
            var k = new WordKnowledge();
            foreach (var w in lex.Words) k.Teach(w.Id);
            Assert.That(Inscriber.WhyNot(rules, lex, items.Get("flint_pick"), new string[0], "word_sonic", k), Is.Empty);
            Assert.That(Inscriber.WhyNot(rules, lex, items.Get("flint_axe"), new string[0], "word_sonic", k), Does.Contain("does nothing for a axe"));
            Assert.That(Inscriber.WhyNot(rules, lex, items.Get("flint_pick"), new[] { "word_sonic" }, "word_fortify", k), Does.Contain("no room"));
            Assert.That(Inscriber.WhyNot(rules, lex, items.Get("fallhammer"), new[] { "word_sonic" }, "word_sonic", k), Does.Contain("already"));
            Assert.That(Inscriber.WhyNot(rules, lex, items.Get("cord"), new string[0], "word_sonic", k), Does.Contain("cannot carry"));
            Assert.That(Inscriber.WhyNot(rules, lex, items.Get("flint_pick"), new string[0], "word_rise", k), Does.Contain("does not take"));
            var fresh = new WordKnowledge();
            Assert.That(Inscriber.WhyNot(rules, lex, items.Get("flint_pick"), new string[0], "word_sonic", fresh), Does.Contain("never seen"));
            fresh.See("word_sonic", "a");
            Assert.That(Inscriber.WhyNot(rules, lex, items.Get("flint_pick"), new string[0], "word_sonic", fresh), Does.Contain("only once"));
            fresh.See("word_sonic", "b");
            Assert.That(Inscriber.WhyNot(rules, lex, items.Get("flint_pick"), new string[0], "word_sonic", fresh), Is.Empty, "a guessed word can be tried");
        }

        // ================================================================ the vertical slice

        [Test]
        public void DiscoverUlunInscribeAPickAndMineWithIt()
        {
            var h = NewHost(out var p, out var r);
            int pick = Give(p, "flint_pick");
            p.C.Selected = pick;

            // Ulun can't be cut before it is found, and it is never spoken.
            UseTable(h, p, r);
            Assert.That(h.Inscribe(1, pick, "word_sonic"), Does.Contain("never seen"));

            // After the heart, the quest points at the table.
            h.WorldFlags.Add(EchoRuin.FlagRemembered);
            Assert.That(h.ObjectiveFor(1).Title, Is.EqualTo("The Ringing Stone"));
            Assert.That(h.ObjectiveFor(1).Text, Does.Contain("glyph on its face"));

            // Discover: once on the hall of the step's east wall, once on the table's face.
            h.Read(p, Ins(r, "ruin_step_hall_east"));
            Assert.That(p.C.Lang.Of("word_sonic"), Is.EqualTo(Familiarity.Seen));
            h.Read(p, Ins(r, "ruin_table"));
            Assert.That(p.C.Lang.Of("word_sonic"), Is.EqualTo(Familiarity.Guessed));
            Assert.That(h.ObjectiveFor(1).Text, Does.Contain("cut Ulun into your pick"));
            Vec3 eye = p.Pos + new Vec3(0, 1.6, 0);
            h.Time += 5;
            Assert.That(h.Speak(1, new List<string> { "word_sonic" }, eye, new Vec3(1, 0, 0)), Is.False);
            Assert.That(Last(h), Does.Contain("not a word for the voice"));

            // Inscribe: the guessed word takes, so now it is known.
            UseTable(h, p, r);
            Assert.That(h.Inscribe(1, pick, "word_sonic"), Is.Empty, Last(h));
            Assert.That(p.C.Inv[pick].Value.Words, Is.EqualTo("word_sonic"));
            Assert.That(p.C.Lang.Knows("word_sonic"), Is.True, "the table answered the word");
            Assert.That(h.ObjectiveFor(1).Title, Is.Not.EqualTo("The Ringing Stone"), "done once something is inscribed");
            Assert.That(h.Events.Exists(e => e.Kind == "inscribe"), Is.True);

            // A plain pick digs one bite out of the rock.
            int[] plainCell = RockCube(h, r, -8);
            ushort rock = h.World.Blocks.Id("rust_rock");
            int before = CountBlock(h, plainCell, 6, rock);
            int plainSlot = Give(p, "flint_pick");
            p.C.Selected = plainSlot;
            p.Pos = new Vec3((plainCell[0] + 0.5) / 4.0, r.CourtM.Y, (plainCell[2] - 6) / 4.0);
            double plainStart = h.Time;
            Assert.That(DigOut(h, plainCell).Broken, Is.True);
            double plainSeconds = h.Time - plainStart;
            int plainRemoved = before - CountBlock(h, plainCell, 6, rock);

            // The inscribed pick, rung with a right click, digs faster and shatters the joined stone.
            int[] cell = RockCube(h, r, 8);
            before = CountBlock(h, cell, 6, rock);
            p.C.Selected = pick;
            p.Pos = new Vec3((cell[0] + 0.5) / 4.0, r.CourtM.Y, (cell[2] - 6) / 4.0);
            double stamina = p.C.Stamina;
            Assert.That(h.UseHeld(1, false, 0, 0, 0, 0, 0, 0), Is.True, Last(h));
            Assert.That(p.C.Stamina, Is.LessThan(stamina), "ringing costs stamina");
            Assert.That(h.Resonating(p), Is.True);
            Assert.That(h.UseHeld(1, false, 0, 0, 0, 0, 0, 0), Is.False, "it has a cooldown");
            double start = h.Time;
            int bagBefore = p.C.Inv.Count("rust_rock");
            Assert.That(DigOut(h, cell).Broken, Is.True);
            double seconds = h.Time - start;
            int removed = before - CountBlock(h, cell, 6, rock);
            Assert.That(seconds, Is.LessThan(plainSeconds * 0.6), "resonant digging is faster");
            Assert.That(removed, Is.GreaterThan(plainRemoved + 20), "the joined stone shatters with it");
            Assert.That(p.C.Inv.Count("rust_rock"), Is.GreaterThan(bagBefore), "and lands in the bag");
            Assert.That(h.Events.Exists(e => e.Kind == "resonate" && e.Value > 20), Is.True, "the Game layer is told, for the ripple");

            // The ringing fades.
            for (int i = 0; i < 300; i++) h.Tick(0.05);
            Assert.That(h.Resonating(p), Is.False);
            Assert.That(h.Events.Exists(e => e.Kind == "resonate_off") || !h.Resonating(p), Is.True);
        }

        [Test]
        public void AStrangerToTheTableCannotInscribe()
        {
            var h = NewHost(out var p, out var r);
            int pick = Give(p, "flint_pick");
            p.C.Learn("word_sonic");
            Assert.That(h.Inscribe(1, pick, "word_sonic"), Does.Contain("inscription table"));
            UseTable(h, p, r);
            p.Pos = r.CourtM + new Vec3(0, 0, -12);
            Assert.That(h.Inscribe(1, pick, "word_sonic"), Does.Contain("inscription table"), "walking away closes it");
        }

        // ================================================================ the second item

        [Test]
        public void UlunAndVuhMakeAResonantHammer()
        {
            var h = NewHost(out var p, out var r);
            int hammer = Give(p, "fallhammer");
            p.C.Selected = hammer;
            p.C.Learn("word_sonic");
            p.C.Learn("word_push");
            UseTable(h, p, r);

            Assert.That(h.Inscribe(1, hammer, "word_push"), Is.Empty, Last(h));
            Assert.That(h.UseHeld(1, false, 0, 0, 0, 0, 0, 0), Is.False, "Vuh alone waits");
            Assert.That(Last(h), Does.Contain("waits"));

            Assert.That(h.Inscribe(1, hammer, "word_sonic"), Does.Contain("word fragment"), "a second word costs a fragment");
            p.C.Inv.Add("word_fragment", 1);
            Assert.That(h.Inscribe(1, hammer, "word_sonic"), Is.Empty, Last(h));
            Assert.That(p.C.Inv.Count("word_fragment"), Is.EqualTo(0));
            Assert.That(p.C.Combos.Contains("resonant_strike"), Is.True, "found a composition");
            Assert.That(AnyMessage(h, "Resonant strike"), Is.True);

            // Right click: a shockwave throws back and hurts the creature beside you.
            Vec3 at = p.Pos + new Vec3(2, 0, 0);
            Creature c = h.SpawnCreature("redstrider", new Vec3(at.X, h.GroundHeight(at.X, at.Z), at.Z), 5);
            Assert.That(c, Is.Not.Null);
            c.Pos = new Vec3(at.X, p.Pos.Y, at.Z);
            double hp = c.Hp;
            Assert.That(h.UseHeld(1, false, 0, 0, 0, 0, 0, 0), Is.True, Last(h));
            Assert.That(c.Hp, Is.LessThan(hp), "it hurts");
            Assert.That(c.Vel.X, Is.GreaterThan(0), "and throws it away from you");
            Assert.That(h.Events.Exists(e => e.Kind == "shockwave"), Is.True);

            // Removal: grind the last word off and the composition is gone.
            Assert.That(h.Erase(1, hammer), Is.Empty);
            Assert.That(p.C.Inv[hammer].Value.Words, Is.EqualTo("word_push"));
            Assert.That(h.InscriptionOf(p, hammer).Any, Is.False);
            Assert.That(h.Erase(1, hammer), Is.Empty);
            Assert.That(p.C.Inv[hammer].Value.Inscribed, Is.False);
        }

        // ================================================================ passive and conditional

        [Test]
        public void TahrHoldsFastAndOshenMendsAtRest()
        {
            var h = NewHost(out var p, out var r);
            h.Rules.Durability = true;   // tools only wear in the hardcore ruleset
            p.God = true;                // a long wait underground; keep the castaway fed
            int axe = Give(p, "flint_axe");
            int plain = Give(p, "flint_axe");
            p.C.Learn("word_fortify");
            p.C.Learn("word_reclaim");
            UseTable(h, p, r);
            Assert.That(h.Inscribe(1, axe, "word_fortify"), Is.Empty, Last(h));
            int full = h.Items.Get("flint_axe").Durability;

            // Passive: two hundred swings' worth of wear, about half of it lands.
            for (int i = 0; i < 200; i++)
            {
                p.C.Selected = axe; InvokeWear(h, p);
                p.C.Selected = plain; InvokeWear(h, p);
            }
            int wornFortified = full - p.C.Inv[axe].Value.Durability;
            int wornPlain = full - p.C.Inv[plain].Value.Durability;
            Assert.That(wornPlain, Is.EqualTo(200));
            Assert.That(wornFortified, Is.InRange(60, 140), "Tahr halves the wear");

            // Conditional: Oshen on a pick mends only while the player rests.
            int pick = Give(p, "flint_pick");
            Assert.That(h.Inscribe(1, pick, "word_reclaim"), Is.Empty, Last(h));
            p.C.Inv.Wear(pick, 50);
            int worn = p.C.Inv[pick].Value.Durability;
            for (int i = 0; i < 20 * 30; i++) h.Tick(0.05);     // 30 s, not resting
            Assert.That(p.C.Inv[pick].Value.Durability, Is.EqualTo(worn));
            h.Structures.Add(new StructureRec { Id = 99, Key = "campfire", Pos = p.Pos + new Vec3(1, 0, 0) });
            p.FlagTimer = 0;
            for (int i = 0; i < 20 * 60; i++) { h.SetPose(1, p.Pos, p.Facing, 0.05); h.Tick(0.05); }
            Assert.That(p.Flags.Contains("resting"), Is.True, string.Join(",", p.Flags));
            Assert.That(p.C.Inv[pick].Value.Durability, Is.GreaterThan(worn + 5), "a minute by the fire mends it");
        }

        static void InvokeWear(Host h, PlayerRec p)
        {
            // Wear the held item the way a dig or a hit does.
            typeof(Host).GetMethod("WearHeld", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(h, new object[] { p });
        }

        // ================================================================ saving

        [Test]
        public void InscriptionsAreSaved()
        {
            var h = NewHost(out var p, out var r);
            int hammer = Give(p, "fallhammer");
            p.C.Inv.SetWords(hammer, "word_push+word_sonic");
            p.C.Inv.Wear(hammer, 7);
            p.C.Combos.Add("resonant_strike");
            int cord = Give(p, "cord");
            Assert.That(p.C.Inv.SetWords(cord, "word_sonic"), Is.False, "things that stack never carry words");

            var again = new CharacterState(h.Items, h.Rules);
            again.FromText(p.C.ToText());
            ItemStack s = again.Inv[hammer].Value;
            Assert.That(s.Key, Is.EqualTo("fallhammer"));
            Assert.That(s.Words, Is.EqualTo("word_push+word_sonic"));
            Assert.That(s.Durability, Is.EqualTo(h.Items.Get("fallhammer").Durability - 7));
            Assert.That(again.Combos, Does.Contain("resonant_strike"));
            Assert.That(again.Inv[cord].Value.Inscribed, Is.False);

            // Moving keeps the words with the item; an old save line without words still loads.
            again.Inv.Move(hammer, 20);
            Assert.That(again.Inv[20].Value.Words, Is.EqualTo("word_push+word_sonic"));
            var inv = new Inventory(h.Items);
            inv.FromText("3 flint_pick 1 120\n4 flint_pick 1 @word_sonic\n");
            Assert.That(inv[3].Value.Durability, Is.EqualTo(120));
            Assert.That(inv[3].Value.Inscribed, Is.False);
            Assert.That(inv[4].Value.Words, Is.EqualTo("word_sonic"));
        }
    }
}
