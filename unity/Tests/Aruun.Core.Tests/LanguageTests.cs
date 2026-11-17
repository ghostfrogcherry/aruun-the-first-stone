using System.Collections.Generic;
using System.IO;
using Aruun.Core.Content;
using Aruun.Core.Edits;
using Aruun.Core.Language;
using Aruun.Core.Voxels;
using NUnit.Framework;

namespace Aruun.Core.Tests
{
    public class LanguageTests
    {
        static Lexicon RealLexicon()
        {
            return Lexicon.FromJson(File.ReadAllText(Path.Combine(TestContent.Root, "language", "lexicon.json")));
        }

        /// <summary>The real lexicon plus a thing word and two modifiers, to test grammar.</summary>
        const string Grammar = @"{
          ""words"": [
            {""id"": ""rise"", ""effect"": ""raise_column"", ""range_voxels"": 48, ""cooldown_s"": 1.5, ""debt"": 16,
             ""params"": {""height"": 4, ""radius"": 1, ""block"": ""rust_rock""}},
            {""id"": ""away"", ""effect"": ""force_push"", ""range_voxels"": 36, ""debt"": 14,
             ""params"": {""range_m"": 9, ""cone_deg"": 50, ""impulse_mps"": 13}},
            {""id"": ""stone"", ""role"": ""thing"", ""effect"": ""stone"", ""params"": {""block"": ""ancient_stone""}},
            {""id"": ""beast"", ""role"": ""thing"", ""effect"": ""creature""},
            {""id"": ""great"", ""role"": ""modifier"", ""scale"": 2, ""debt_scale"": 2.5},
            {""id"": ""bad"", ""role"": ""wobble""}
          ],
          ""effects"": [
            {""id"": ""raise_column"", ""targets"": ""solid_voxel"", ""max_height"": 24, ""max_radius"": 4},
            {""id"": ""force_push"", ""targets"": ""creatures"", ""max_range_m"": 14, ""max_impulse_mps"": 20}
          ]
        }";

        static Lexicon GrammarLexicon()
        {
            return Lexicon.FromJson(Grammar.Replace(@",
            {""id"": ""bad"", ""role"": ""wobble""}", ""));
        }

        static WordKnowledge KnowsAll(Lexicon lex)
        {
            var k = new WordKnowledge();
            foreach (WordDef w in lex.Words) k.Teach(w.Id);
            return k;
        }

        static SpeakResult Say(Lexicon lex, WordKnowledge k, params string[] words)
        {
            BlockRegistry blocks = TestContent.Blocks();
            return Interpreter.Speak(lex, k, words, 0, 0, 0, 1, 0, 0, 5, 0, 0, blocks.Id);
        }

        [Test]
        public void TheShippedLexiconIsSound()
        {
            Lexicon lex = RealLexicon();
            Assert.That(lex.Validate(), Is.Empty);
            Assert.That(lex.Word("word_rise").Role, Is.EqualTo(WordRole.Action), "old words default to actions");
        }

        [Test]
        public void BadContentIsCaught()
        {
            Assert.Throws<System.FormatException>(() => Lexicon.FromJson(Grammar), "unknown role");
            Lexicon lex = Lexicon.FromJson(File.ReadAllText(Path.Combine(TestContent.Root, "language", "lexicon.json"))
                .Replace("\"height\": 12", "\"height\": 999"));
            Assert.That(lex.Validate(), Has.Some.Contains("height out of bounds"));
        }

        [Test]
        public void YouCannotSpeakWhatYouDoNotKnow()
        {
            Lexicon lex = RealLexicon();
            var k = new WordKnowledge();
            Assert.That(Say(lex, k, "word_rise").Error, Is.EqualTo(SpeakError.NotLearned));
            Assert.That(Say(lex, k, "word_nonsense").Error, Is.EqualTo(SpeakError.UnknownWord));
            Assert.That(Say(lex, k).Error, Is.EqualTo(SpeakError.Empty));
        }

        [Test]
        public void LearningGoesSeenGuessedKnown()
        {
            var k = new WordKnowledge();
            Assert.That(k.Of("rise"), Is.EqualTo(Familiarity.Unseen));
            Assert.That(k.See("rise", "ruin_a"), Is.EqualTo(Familiarity.Seen));
            Assert.That(k.Confirm("rise"), Is.False, "one sighting is not enough to test a guess");
            Assert.That(k.See("rise", "ruin_a"), Is.EqualTo(Familiarity.Seen), "the same place teaches nothing new");
            Assert.That(k.See("rise", "ruin_b"), Is.EqualTo(Familiarity.Guessed));
            Assert.That(k.Knows("rise"), Is.False);
            Assert.That(k.Confirm("rise"), Is.True);
            Assert.That(k.Knows("rise"), Is.True);
            Assert.That(k.See("rise", "ruin_c"), Is.EqualTo(Familiarity.Known), "seeing it again never unlearns");
        }

        [Test]
        public void RiseRaisesAColumnOutOfTheGround()
        {
            Lexicon lex = RealLexicon();
            SpeakResult r = Say(lex, KnowsAll(lex), "word_rise");
            Assert.That(r.Ok, Is.True, r.Error.ToString());
            Assert.That(r.Edits.Count, Is.EqualTo(13), "radius 2 disc = 13 columns");
            Assert.That(r.Debt, Is.EqualTo(16));
            int cells = 0;
            foreach (EditOp op in r.Edits) op.ForEachCell((x, y, z) => cells++);
            Assert.That(cells, Is.EqualTo(13 * 12));
            Assert.That(r.Edits[0].Y, Is.EqualTo(1), "starts just above the target");
        }

        [Test]
        public void TheColumnStopsAtSolidGroundInTheWorld()
        {
            BlockRegistry blocks = TestContent.Blocks();
            Lexicon lex = RealLexicon();
            var world = new VoxelWorld(42, blocks);
            int h = world.Generator.Field.HeightAt(0, 0);
            SpeakResult r = Interpreter.Speak(lex, KnowsAll(lex), new[] { "word_rise" }, 0, h + 4, 0, 0, -1, 0, 0, h, 0, blocks.Id);
            foreach (EditOp op in r.Edits) world.Apply(op);
            Assert.That(world.Get(0, h, 0), Is.Not.EqualTo(blocks.Id("rust_rock")).Or.EqualTo(blocks.Id("rust_rock")), "ground untouched");
            Assert.That(world.Get(0, h + 12, 0), Is.EqualTo(blocks.Id("rust_rock")));
            Assert.That(world.Get(0, h + 13, 0), Is.Not.EqualTo(blocks.Id("rust_rock")));
        }

        [Test]
        public void TooFarAwayIsRefused()
        {
            BlockRegistry blocks = TestContent.Blocks();
            Lexicon lex = RealLexicon();
            SpeakResult r = Interpreter.Speak(lex, KnowsAll(lex), new[] { "word_rise" }, 0, 0, 0, 1, 0, 0, 500, 0, 0, blocks.Id);
            Assert.That(r.Error, Is.EqualTo(SpeakError.OutOfRange));
        }

        [Test]
        public void ForceWordsAimAndWorkInMetres()
        {
            Lexicon lex = RealLexicon();
            SpeakResult r = Say(lex, KnowsAll(lex), "word_push");
            Assert.That(r.Ok, Is.True);
            Assert.That(r.Forces[0].Kind, Is.EqualTo("push"));
            Assert.That(r.Forces[0].RangeM, Is.EqualTo(9));
            Assert.That(r.Forces[0].DirX, Is.EqualTo(1));
            BlockRegistry blocks = TestContent.Blocks();
            SpeakResult still = Interpreter.Speak(lex, KnowsAll(lex), new[] { "word_push" }, 0, 0, 0, 0, 0, 0, 0, 0, 0, blocks.Id);
            Assert.That(still.Error, Is.EqualTo(SpeakError.NoDirection));
        }

        [Test]
        public void WordsCombineIntoPhrases()
        {
            Lexicon lex = GrammarLexicon();
            Assert.That(lex.Validate(), Is.Empty);
            WordKnowledge k = KnowsAll(lex);
            BlockRegistry blocks = TestContent.Blocks();

            // "rise stone": the thing word picks the material.
            SpeakResult stone = Say(lex, k, "rise", "stone");
            Assert.That(stone.Ok, Is.True);
            Assert.That(stone.Edits[0].Block, Is.EqualTo(blocks.Id("ancient_stone")));

            // "great rise": stronger, and costs more. Word order does not matter.
            SpeakResult great = Say(lex, k, "great", "rise");
            Assert.That(great.Edits[0].Y2 - great.Edits[0].Y + 1, Is.EqualTo(8));
            Assert.That(great.Debt, Is.EqualTo(40));

            // Stacking modifiers can never break an effect's limits.
            SpeakResult greatest = Say(lex, k, "great", "great", "great", "great", "away");
            Assert.That(greatest.Forces[0].ImpulseMps, Is.LessThanOrEqualTo(20));
            Assert.That(greatest.Forces[0].RangeM, Is.LessThanOrEqualTo(14));
            Assert.That(greatest.Debt, Is.GreaterThan(great.Debt), "but it still costs");
        }

        [Test]
        public void BadGrammarIsRefused()
        {
            Lexicon lex = GrammarLexicon();
            WordKnowledge k = KnowsAll(lex);
            Assert.That(Say(lex, k, "great", "stone").Error, Is.EqualTo(SpeakError.NoAction));
            Assert.That(Say(lex, k, "rise", "away").Error, Is.EqualTo(SpeakError.TwoActions));
            Assert.That(Say(lex, k, "rise", "stone", "beast").Error, Is.EqualTo(SpeakError.TwoThings));
            SpeakResult wrong = Say(lex, k, "rise", "beast");
            Assert.That(wrong.Error, Is.EqualTo(SpeakError.WrongThing), "you cannot raise a column of beast");
            Assert.That(wrong.Word, Is.EqualTo("beast"));
            Assert.That(Say(lex, k, "away", "beast").Ok, Is.True);
        }
    }
}
