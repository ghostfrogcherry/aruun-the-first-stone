using System;
using System.Collections.Generic;
using System.IO;
using Aruun.Core.Combat;
using Aruun.Core.Creatures;
using NUnit.Framework;

namespace Aruun.Core.Tests
{
    /// <summary>A flat, empty world with players standing where the test puts them.</summary>
    sealed class FakeWorld : ICreatureWorld
    {
        public double Time;
        public bool Night;
        public readonly Dictionary<int, Vec3> Players = new Dictionary<int, Vec3>();
        public readonly HashSet<int> Sprinting = new HashSet<int>();
        public readonly Dictionary<int, double> Damage = new Dictionary<int, double>();
        public readonly List<string> Events = new List<string>();
        public readonly List<Creature> Creatures = new List<Creature>();
        public Vec3? Fire;
        public int Shots, Carves, Shoves;
        readonly Random _rng = new Random(7);

        public double WorldTime { get { return Time; } }
        public bool IsNight { get { return Night; } }
        public double Gravity { get { return 9.81 * 0.38; } }
        public double DamageMult { get { return 1; } }
        public bool HuntersByDay { get { return false; } }
        public double Random() { return _rng.NextDouble(); }

        public Seen NearestPlayer(Vec3 pos, double maxDist)
        {
            var best = new Seen();
            foreach (var kv in Players)
            {
                double d = Vec3.Distance(kv.Value, pos);
                if (d <= maxDist && (!best.Found || d < best.Dist)) best = new Seen { Id = kv.Key, Pos = kv.Value, Dist = d };
            }
            return best;
        }

        public IReadOnlyList<Seen> LivingPlayers()
        {
            var l = new List<Seen>();
            foreach (var kv in Players) l.Add(new Seen { Id = kv.Key, Pos = kv.Value });
            return l;
        }

        public Vec3? PlayerPos(int player) { Vec3 p; return Players.TryGetValue(player, out p) ? p : (Vec3?)null; }
        public bool IsSprinting(int player) { return Sprinting.Contains(player); }
        public bool HasBuff(int player, string buff) { return false; }

        public void DamagePlayer(int player, double amount, Vec3 source, string kind)
        {
            double d;
            Damage.TryGetValue(player, out d);
            Damage[player] = d + amount;
        }

        public void ShovePlayer(int player, Vec3 velocity, double seconds) { Shoves++; }
        public void NoteHit(int player, string tag, double withinS, int count, string buff) { Events.Add("note " + tag); }

        public void CreatureStrike(Vec3 origin, Vec3 forward, double reach, double arcDeg, double damage)
        {
            foreach (var kv in Players)
                if (CombatRules.InArc(origin, forward, kv.Value, reach, arcDeg, 0.4)) DamagePlayer(kv.Key, damage, origin, "creature");
        }

        public bool Carve(Vec3 centre, double radiusM, int power) { Carves++; return false; }
        public double GroundHeight(double x, double z) { return 0; }
        public bool ClearLine(Vec3 a, Vec3 b) { return true; }
        public Vec3? MothLight(Vec3 pos, double rangeM, double wardM) { return Fire; }
        public bool NearStructure(Vec3 pos, string station, double rangeM) { return false; }

        public int CountInState(string behavior, params string[] states)
        {
            int n = 0;
            foreach (var c in Creatures)
                if (c.Def.Behavior == behavior && Array.IndexOf(states, c.State) >= 0) n++;
            return n;
        }

        public void AlarmGroup(int group, Vec3 threat)
        {
            foreach (var c in Creatures) if (c.Group == group) Brains.Alarm(this, c, threat);
        }

        public void SpawnShot(int owner, Vec3 from, Vec3 velocity, double damage) { Shots++; }
        public void Event(int creature, string kind, double value) { Events.Add(kind); }
        public void RemoveQuietly(int creature) { Events.Add("removed"); }

        /// <summary>Runs every creature for `seconds`, moving them by what they want.</summary>
        public void Run(double seconds, double dt = 0.05)
        {
            for (double t = 0; t < seconds; t += dt)
            {
                Time += dt;
                foreach (var c in Creatures.ToArray())
                {
                    if (c.State == "dead") continue;
                    Vec3 want = Brains.Think(this, c, dt);
                    if (!c.Def.Flies) want.Y = 0;
                    c.Pos = c.Pos + want * dt;
                    c.Grounded = true;
                }
            }
        }
    }

    public class CreatureTests
    {
        static Dictionary<string, CreatureDef> Defs()
        {
            var d = new Dictionary<string, CreatureDef>();
            foreach (var c in CreatureDef.FromJson(File.ReadAllText(Path.Combine(TestContent.Root, "creatures", "creatures.json"))))
                d[c.Key] = c;
            return d;
        }

        static Creature Spawn(FakeWorld w, string key, Vec3 pos, int group = 1)
        {
            var c = new Creature(w.Creatures.Count + 1, Defs()[key], pos) { Group = group };
            w.Creatures.Add(c);
            return c;
        }

        [Test]
        public void EveryCreatureHasAKnownBrain()
        {
            Assert.That(Defs().Count, Is.GreaterThanOrEqualTo(6));
        }

        [Test]
        public void AReedhopperFreezesThenTheGroupBoltsInHops()
        {
            var w = new FakeWorld();
            Creature a = Spawn(w, "reedhopper", new Vec3(0, 0, 0));
            Creature b = Spawn(w, "reedhopper", new Vec3(1, 0, 0));
            w.Players[1] = new Vec3(0, 0, 9);
            w.Run(0.1);
            Assert.That(a.State, Is.EqualTo("alert"), "it freezes when it notices you");
            Assert.That(a.AnimForce, Is.EqualTo(Anim.Alert));
            w.Run(1.5);
            Assert.That(a.State, Is.EqualTo("flee"));
            Assert.That(b.State, Is.EqualTo("flee"), "the whole group goes");
            w.Run(2);
            Assert.That(a.Pos.Z, Is.LessThan(-2), "away from the player");
        }

        [Test]
        public void SprintingAtAReedhopperSpooksItAtOnce()
        {
            var w = new FakeWorld();
            Creature a = Spawn(w, "reedhopper", Vec3.Zero);
            w.Players[1] = new Vec3(0, 0, 9);
            w.Run(0.1);
            w.Sprinting.Add(1);
            w.Run(0.1);
            Assert.That(a.State, Is.EqualTo("flee"));
        }

        [Test]
        public void AGlassbackWarnsThenChargesWhenCrowded()
        {
            var w = new FakeWorld();
            Creature g = Spawn(w, "glassback", Vec3.Zero);
            w.Players[1] = new Vec3(0, 0, 2);
            w.Run(2.5);
            Assert.That(g.AnimForce, Is.EqualTo(Anim.Windup), "head down and stamping");
            Assert.That(g.State, Is.Not.EqualTo("charge"));
            w.Run(4);
            Assert.That(w.Events, Does.Contain("strike"), "it charged");
            Assert.That(w.Damage.ContainsKey(1), Is.True, "and hit the player who stood there");
        }

        [Test]
        public void AGlassbackLeftAloneIgnoresYou()
        {
            var w = new FakeWorld();
            Creature g = Spawn(w, "glassback", Vec3.Zero);
            w.Players[1] = new Vec3(0, 0, 8);
            w.Run(10);
            Assert.That(w.Damage.ContainsKey(1), Is.False);
            Assert.That(g.State, Is.AnyOf("idle", "wander"));
        }

        [Test]
        public void TheGlassbackShellHurtsBackButTheFlankIsSoft()
        {
            var w = new FakeWorld();
            Creature g = Spawn(w, "glassback", Vec3.Zero);   // faces +Z
            w.Players[1] = new Vec3(0, 0, 1.5);
            double front = Brains.Armour(w, g, 10, 1);
            Assert.That(front, Is.EqualTo(3).Within(1e-9));
            Assert.That(w.Damage[1], Is.EqualTo(4), "the shell cuts the attacker");
            w.Players[1] = new Vec3(1.5, 0, 0);
            Assert.That(Brains.Armour(w, g, 10, 1), Is.EqualTo(10));
            Assert.That(Brains.OnPull(w, g), Is.True);
            Assert.That(g.State, Is.EqualTo("flipped"));
            Assert.That(Brains.Armour(w, g, 10, 1), Is.EqualTo(15), "flipped on its back it is soft");
        }

        [Test]
        public void NightstalkersHuntAtNightAndSlinkAwayByDay()
        {
            var w = new FakeWorld { Night = false };
            Creature s = Spawn(w, "nightstalker", Vec3.Zero);
            w.Players[1] = new Vec3(0, 0, 10);
            w.Run(1);
            Assert.That(s.State, Is.EqualTo("flee"));
            Assert.That(s.Pos.Z, Is.LessThan(0));

            var night = new FakeWorld { Night = true };
            Creature h = Spawn(night, "nightstalker", Vec3.Zero);
            night.Players[1] = new Vec3(0, 0, 10);
            night.Run(5);
            Assert.That(night.Damage.ContainsKey(1), Is.True, "it closes in and strikes");
        }

        [Test]
        public void HurtingARedstriderSendsTheHerdRunning()
        {
            var w = new FakeWorld();
            Creature a = Spawn(w, "redstrider", Vec3.Zero, 3);
            Creature b = Spawn(w, "redstrider", new Vec3(2, 0, 0), 3);
            Creature other = Spawn(w, "redstrider", new Vec3(40, 0, 0), 4);
            w.Players[1] = new Vec3(0, 0, 2);
            Assert.That(Brains.Hurt(w, a, 5, 5, 1), Is.False);
            Assert.That(a.State, Is.EqualTo("flee"));
            Assert.That(b.State, Is.EqualTo("flee"));
            Assert.That(other.State, Is.EqualTo("idle"), "another herd is not alarmed");
            Assert.That(Brains.Hurt(w, a, 500, 0, 1), Is.True, "and it can die");
        }

        [Test]
        public void DuskmothsCircleTheFireDiveAndLeaveAtDawn()
        {
            var w = new FakeWorld { Night = true, Fire = new Vec3(0, 0, 0) };
            Creature m = Spawn(w, "duskmoth", new Vec3(10, 3, 0));
            w.Players[1] = new Vec3(2, 0, 0);
            w.Run(12);
            Assert.That(w.Damage.ContainsKey(1), Is.True, "it dived at the player by the fire");
            Assert.That(w.Events, Does.Contain("note moth"));
            w.Night = false;
            w.Run(6);
            Assert.That(w.Events, Does.Contain("removed"), "gone at dawn");
        }

        [Test]
        public void TheEchoSentinelWakesAndRunsItsDrill()
        {
            var w = new FakeWorld();
            Creature s = Spawn(w, "echo_sentinel", Vec3.Zero);
            Assert.That(s.State, Is.EqualTo("dormant"));
            Assert.That(Brains.Armour(w, s, 50, 1), Is.EqualTo(-1), "rubble cannot be hurt");
            w.Players[1] = new Vec3(0, 0, 20);
            w.Run(1);
            Assert.That(s.State, Is.EqualTo("dormant"), "too far to wake it");
            w.Players[1] = new Vec3(0, 0, 7);
            w.Players[2] = new Vec3(1, 0, 7);
            w.Run(0.1);
            Assert.That(s.State, Is.EqualTo("assemble"));
            Assert.That(s.HpMax, Is.EqualTo(320 * 1.6).Within(1e-9), "tougher with a second player");
            w.Run(12);
            Assert.That(w.Shots, Is.EqualTo(2), "throw, throw...");
            Assert.That(w.Events, Does.Contain("slam"), "...slam");
            Assert.That(w.Damage.ContainsKey(1), Is.False, "seven metres is outside the slam");
        }

        [Test]
        public void ThePullWordDragsTheSentinelsFistDown()
        {
            var w = new FakeWorld();
            Creature s = Spawn(w, "echo_sentinel", Vec3.Zero);
            s.State = "slam_windup";
            s.T = 1;
            Assert.That(Brains.OnPull(w, s), Is.True);
            Assert.That(s.State, Is.EqualTo("stagger"));
            w.Players[1] = new Vec3(0, 0, -2);   // behind it, but it is staggered
            Assert.That(Brains.Armour(w, s, 10, 1), Is.EqualTo(15), "the core is open while it reels");
        }

        [Test]
        public void TheSentinelResetsWhenEveryoneLeaves()
        {
            var w = new FakeWorld();
            Creature s = Spawn(w, "echo_sentinel", Vec3.Zero);
            w.Players[1] = new Vec3(0, 0, 7);
            w.Run(3);
            s.Hp = 10;
            w.Players.Clear();
            w.Run(11);
            Assert.That(s.State, Is.EqualTo("dormant"));
            Assert.That(s.Hp, Is.EqualTo(320));
        }
    }
}
