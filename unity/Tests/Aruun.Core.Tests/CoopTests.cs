using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Aruun.Core.Combat;
using Aruun.Core.Creatures;
using Aruun.Core.Farming;
using Aruun.Core.Items;
using Aruun.Core.Language;
using Aruun.Core.Net;
using Aruun.Core.People;
using Aruun.Core.Play;
using Aruun.Core.Survival;
using Aruun.Core.Town;
using Aruun.Core.Voxels;
using NUnit.Framework;

namespace Aruun.Core.Tests
{
    /// <summary>Two players in one world: a host and a friend over a real local TCP connection.</summary>
    public class CoopTests
    {
        const int Seed = 20261116;

        /// <summary>A whole game's host, the way the game builds one (world, town, people).</summary>
        static Host Build(int seed)
        {
            var blocks = TestContent.Blocks();
            var world = new VoxelWorld(seed, blocks, TestContent.Map());
            world.Generator.Village = Village.Load(TestContent.Root, world.Generator.Field, blocks, world.Generator.Ruin);
            var items = ItemRegistry.FromJson(File.ReadAllText(Path.Combine(TestContent.Root, "items", "items.json")), blocks);
            var rules = Ruleset.Load(File.ReadAllText(Path.Combine(TestContent.Root, "survival", "rulesets.json")), "light");
            var plants = Plants.FromJson(File.ReadAllText(Path.Combine(TestContent.Root, "farming", "plants.json")));
            var creatures = CreatureDef.FromJson(File.ReadAllText(Path.Combine(TestContent.Root, "creatures", "creatures.json")));
            var host = new Host(world, items, rules, plants, creatures, 1) { SpawnCreatures = false };
            host.Lexicon = Lexicon.FromJson(File.ReadAllText(Path.Combine(TestContent.Root, "language", "lexicon.json")));
            host.LoadPeople(PersonDef.FromJson(File.ReadAllText(Path.Combine(TestContent.Root, "people", "people.json"))),
                QuestDef.FromJson(File.ReadAllText(Path.Combine(TestContent.Root, "quests", "quests.json"))));
            host.Tree = Skills.SkillTree.FromJson(File.ReadAllText(Path.Combine(TestContent.Root, "skills", "graviturge.json")));
            host.SpawnPoint = new Vec3(6, host.GroundHeight(6, -6), -6);
            return host;
        }

        sealed class Pair : IDisposable
        {
            public Host Real, Mirror;
            public CoopServer Server;
            public CoopClient Client;
            public Welcome Welcome;
            public PlayerRec HostPlayer, Friend;

            public void Dispose() { Client?.Dispose(); Server?.Dispose(); }

            /// <summary>One frame on both machines, the way the Game layer runs them.</summary>
            public void Frame(double dt = 0.05)
            {
                Server.Pump(dt);
                Real.Tick(dt);
                Server.AfterTick(dt);
                Real.Edits.Clear(); Real.Events.Clear(); Real.Messages.Clear();
                Thread.Sleep(2);
                Client.Pump(dt);
            }

            /// <summary>Runs frames until `done` holds (or fails after `seconds` of real time).</summary>
            public void Until(Func<bool> done, double seconds, string what)
            {
                var until = DateTime.UtcNow.AddSeconds(seconds);
                while (!done())
                {
                    Assert.That(DateTime.UtcNow, Is.LessThan(until), "timed out waiting for: " + what);
                    Frame();
                }
            }
        }

        static Pair Connect(string friendCharacter = "")
        {
            var pair = new Pair { Real = Build(Seed) };
            pair.HostPlayer = pair.Real.Join(1, new CharacterState(pair.Real.Items, pair.Real.Rules), pair.Real.SpawnPoint, true);
            pair.Server = new CoopServer(pair.Real, Seed, null) { HostName = "Ana" };
            pair.Server.Start(0);

            // The friend connects first, then builds the world from what the host says.
            Welcome w = null;
            var t = new Thread(() => { pair.Client = CoopClient.Join("127.0.0.1", pair.Server.Port, "Bo", friendCharacter, out w); });
            t.Start();
            var until = DateTime.UtcNow.AddSeconds(10);
            while (t.IsAlive && DateTime.UtcNow < until) { pair.Server.Pump(0.01); Thread.Sleep(5); }
            Assert.That(t.IsAlive, Is.False, "the friend got an answer");
            pair.Welcome = w;
            pair.Mirror = Build(w.Seed);
            foreach (var op in w.Edits) pair.Mirror.World.Apply(op);
            var c = new CharacterState(pair.Mirror.Items, pair.Mirror.Rules);
            if (friendCharacter.Length > 0) c.FromText(friendCharacter);
            pair.Friend = pair.Mirror.Join(w.Id, c, w.Spawn, friendCharacter.Length == 0);
            pair.Client.Attach(pair.Mirror, w);
            return pair;
        }

        [Test]
        public void AFriendJoinsAndBothSeeEachOther()
        {
            using (var pair = Connect())
            {
                Assert.That(pair.Welcome.Id, Is.EqualTo(CoopServer.FirstGuestId));
                Assert.That(pair.Welcome.Seed, Is.EqualTo(Seed));
                Assert.That(pair.Real.Players.ContainsKey(2), Is.True, "the host runs the friend's character");
                pair.Until(() => pair.Mirror.Players.ContainsKey(1), 5, "the host player in the friend's world");
                Assert.That(pair.Client.Names[1], Is.EqualTo("Ana"));
                Assert.That(pair.Server.GuestCount, Is.EqualTo(1));

                // The friend walks: the host sees it.
                Vec3 to = pair.Friend.Pos + new Vec3(3, 0, 2);
                pair.Mirror.SetPose(2, to, new Vec3(0, 0, 1), 0.05);
                pair.Until(() => Vec3.Distance(pair.Real.Players[2].Pos, to) < 0.01, 5, "the friend's move on the host");

                // The host walks: the friend sees it.
                Vec3 hostTo = pair.HostPlayer.Pos + new Vec3(-2, 0, 1);
                pair.Real.SetPose(1, hostTo, new Vec3(1, 0, 0), 0.05);
                pair.Until(() => Vec3.Distance(pair.Mirror.Players[1].Pos, hostTo) < 0.01, 5, "the host's move on the friend");

                // People are mirrored by id.
                Assert.That(pair.Mirror.People.Count, Is.EqualTo(pair.Real.People.Count));
            }
        }

        [Test]
        public void AFriendArrivesInFrontOfTheHostAndLearnsSkillsThere()
        {
            using (var pair = Connect())
            {
                Vec3 off = pair.Real.Players[2].Pos - pair.HostPlayer.Pos;
                Assert.That(off.FlatLength, Is.InRange(2.5, 5.0), "a few steps away, not shoulder to shoulder");
                Assert.That(off.Z, Is.GreaterThan(2), "in front of the host, who faces north");

                Assert.That(pair.Real.Players[2].C.SkillPoints, Is.EqualTo(pair.Real.Tree.StartPoints));
                pair.Mirror.LearnSkill(2, "sense_of_down");
                pair.Until(() => pair.Real.Players[2].C.Skills.Contains("sense_of_down"), 5, "the skill on the host");
                pair.Until(() => pair.Friend.C.Skills.Contains("sense_of_down"), 5, "the skill back on the friend's screen");
                Assert.That(pair.Real.Players[2].C.SkillPoints, Is.EqualTo(pair.Real.Tree.StartPoints - 1));
            }
        }

        [Test]
        public void AFriendDigsAndTheWorldChangesForBoth()
        {
            using (var pair = Connect())
            {
                pair.Until(() => pair.Mirror.Players.ContainsKey(1), 5, "first state");
                // Find diggable ground (grass or soil) by the spawn, outside the plaza.
                int tx = 0, ty = 0, tz = 0;
                bool found = false;
                for (int r = 0; r < 200 && !found; r += 4)
                    for (int dx = -r; dx <= r && !found; dx += 4)
                    {
                        int x = 120 + dx, z = -40 + r;
                        int y = pair.Real.World.Generator.Field.HeightAt(x, z);
                        string key = pair.Real.World.Blocks.Get(pair.Real.World.Get(x, y, z)).Key;
                        if (key == "grass" || key == "red_soil") { tx = x; ty = y; tz = z; found = true; }
                    }
                Assert.That(found, Is.True, "some grass near town");
                Vec3 stand = new Vec3((tx + 0.5) / 4.0 + 1, (ty + 1) / 4.0, (tz + 0.5) / 4.0);
                pair.Friend.Pos = stand;
                pair.Mirror.SetPose(2, stand, new Vec3(-1, 0, 0), 0.05);
                int before = pair.Real.Players[2].C.Inv.IsEmpty ? 0 : 1;
                pair.Until(() =>
                {
                    pair.Mirror.Dig(2, tx, ty, tz, 0.05);
                    return pair.Real.World.Get(tx, ty, tz) == 0;
                }, 10, "the block to break on the host");
                pair.Until(() => pair.Mirror.World.Get(tx, ty, tz) == 0, 5, "the edit in the friend's world");
                Assert.That(pair.Mirror.Edits.Count, Is.GreaterThan(0), "queued for the friend's Game layer to redraw");
                pair.Until(() => pair.Friend.C.Inv.Count("grass") + pair.Friend.C.Inv.Count("red_soil") + pair.Friend.C.Inv.Count("soil") > 0
                                 || pair.Client.CharacterText != null && pair.Client.CharacterText.Contains("slot"), 5, "the drop in the friend's bag");
            }
        }

        [Test]
        public void AFriendTalksToTheElder()
        {
            using (var pair = Connect())
            {
                pair.Until(() => pair.Mirror.Players.ContainsKey(1), 5, "first state");
                PersonRec elder = pair.Real.Person("elder");
                Vec3 feet = elder.Pos + new Vec3(0, 0, 2);
                pair.Friend.Pos = feet;
                pair.Mirror.SetPose(2, feet, new Vec3(0, 0, -1), 0.05);
                pair.Frame();
                pair.Mirror.Interact(2, elder.Pos + new Vec3(0, 1.5, 2), new Vec3(0, 0, -1));
                pair.Until(() => pair.Friend.Talk != null, 5, "the elder's words on the friend's screen");
                Assert.That(pair.Friend.Talk.Name, Does.Contain("Teshwa"));
                Assert.That(pair.Friend.Talk.Choices[pair.Friend.Talk.Choices.Count - 1], Is.EqualTo("Goodbye."));
                pair.Mirror.Choose(2, 0);
                pair.Until(() => pair.Friend.C.Quests.IsActive("main_remember"), 5, "the quest in the friend's log");
                Assert.That(pair.HostPlayer.C.Quests.Has("main_remember"), Is.False, "quests are per character");
                pair.Mirror.EndTalk(2);
                pair.Until(() => pair.Real.Players[2].Talk == null, 5, "the talk ends on the host");
            }
        }

        [Test]
        public void AFriendKeepsTheirOwnCharacter()
        {
            var blocks = TestContent.Blocks();
            var items = ItemRegistry.FromJson(File.ReadAllText(Path.Combine(TestContent.Root, "items", "items.json")), blocks);
            var rules = Ruleset.Load(File.ReadAllText(Path.Combine(TestContent.Root, "survival", "rulesets.json")), "light");
            var mine = new CharacterState(items, rules);
            mine.SetupNew();
            mine.Inv.Add("flint", 7);
            mine.Quests.Set("side_watch", 0);
            using (var pair = Connect(mine.ToText()))
            {
                Assert.That(pair.Real.Players[2].C.Inv.Count("flint"), Is.EqualTo(mine.Inv.Count("flint")));
                Assert.That(pair.Real.Players[2].C.Quests.IsActive("side_watch"), Is.True);
            }
        }

        [Test]
        public void WhenAFriendLeavesTheHostLetsGo()
        {
            var pair = Connect();
            pair.Until(() => pair.Mirror.Players.ContainsKey(1), 5, "first state");
            pair.Client.Dispose();
            var until = DateTime.UtcNow.AddSeconds(5);
            while (pair.Real.Players.ContainsKey(2) && DateTime.UtcNow < until) { pair.Server.Pump(0.05); Thread.Sleep(10); }
            Assert.That(pair.Real.Players.ContainsKey(2), Is.False);
            Assert.That(pair.Server.GuestCount, Is.EqualTo(0));
            pair.Server.Dispose();
        }

        [Test]
        public void FramesSurviveTabsNewlinesAndUnicode()
        {
            string text = "line one\nline\ttwo \\ back\nÄrûn";
            Assert.That(Wire.Unpack(Wire.Pack(text)), Is.EqualTo(text));
            var ms = new MemoryStream();
            Frames.Write(ms, "state", "T\t1.5\nM\tHellø");
            Frames.Write(ms, "bye", "");
            ms.Position = 0;
            Frame a = Frames.Read(ms), b = Frames.Read(ms);
            Assert.That(a.Kind, Is.EqualTo("state"));
            Assert.That(a.Lines, Is.EqualTo(new[] { "T\t1.5", "M\tHellø" }));
            Assert.That(b.Kind, Is.EqualTo("bye"));
            Assert.That(Frames.Read(ms), Is.Null, "end of stream");
        }
    }
}
