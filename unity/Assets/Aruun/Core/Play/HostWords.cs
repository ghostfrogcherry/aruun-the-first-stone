using System;
using System.Collections.Generic;
using Aruun.Core.Combat;
using Aruun.Core.Creatures;
using Aruun.Core.Language;
using Aruun.Core.Ruins;
using Aruun.Core.Voxels;

namespace Aruun.Core.Play
{
    /// <summary>
    /// The Avarra words and the echo ruin: reading inscriptions, speaking phrases, the
    /// mechanisms that answer them, the Echo Sentinel and the quest that ties them together.
    ///
    /// How a word is learned: read it carved in one place (Seen), then in a second place
    /// (Guessed: you can try it), then speak it and see it work (Known). A word only seen once
    /// cannot be spoken yet.
    /// </summary>
    public sealed partial class Host
    {
        /// <summary>The words. Without it, nothing can be spoken.</summary>
        public Lexicon Lexicon;
        /// <summary>World facts that outlast a session (the sentinel fell, the world remembers). The Game layer saves them.</summary>
        public readonly HashSet<string> WorldFlags = new HashSet<string>();
        /// <summary>The echo ruin of this world, or null.</summary>
        public EchoRuin Ruin { get { return World.Generator.Ruin; } }

        /// <summary>How close a player must be to the court before the sentinel takes its place.</summary>
        public const double SentinelAppearM = 34;
        /// <summary>How far off-aim a mechanism still hears you, in metres.</summary>
        public const double MechanismAimM = 1.6;

        int _sentinel;

        /// <summary>A word's name without its "(placeholder)" note.</summary>
        public string WordName(string id)
        {
            WordDef w = Lexicon != null ? Lexicon.Word(id) : null;
            if (w == null) return id;
            string d = w.Display;
            int cut = d.IndexOf(" (", StringComparison.Ordinal);
            return cut > 0 ? d.Substring(0, cut) : d;
        }

        public string WordMeaning(string id)
        {
            WordDef w = Lexicon != null ? Lexicon.Word(id) : null;
            return w != null ? w.Meaning : "";
        }

        // ===================================================================== reading

        bool ReadInscription(PlayerRec p, Vec3 eye, Vec3 dir)
        {
            if (Ruin == null || Lexicon == null) return false;
            VoxelHit hit = VoxelRaycast.Cast(World, eye.X * M, eye.Y * M, eye.Z * M, dir.X, dir.Y, dir.Z, ReachM * M);
            if (!hit.Hit) return false;
            RuinInscription ins = Ruin.InscriptionAt(hit.X, hit.Y, hit.Z);
            if (ins == null || World.Get(hit.X, hit.Y, hit.Z) != World.Blocks.Id("inscribed_stone")) return false;
            Read(p, ins);
            return true;
        }

        /// <summary>The player reads one inscription: one sighting of its word at its place.</summary>
        public void Read(PlayerRec p, RuinInscription ins)
        {
            string name = WordName(ins.Word), meaning = WordMeaning(ins.Word);
            WordKnowledge k = p.C.Lang;
            Familiarity before = k.Of(ins.Word);
            bool again = k.SeenAt(ins.Word, ins.Place);
            Familiarity now = k.See(ins.Word, ins.Place);
            Fx("read", p.Id, 0, (int)now, ins.Point());
            if (now == Familiarity.Known)
                Say(p.Id, "Carved here: " + name + ". You know it: \"" + meaning + "\".");
            else if (again)
                Say(p.Id, "You have read this one before: " + name + ". Find it written somewhere else.");
            else if (now == Familiarity.Guessed && before < Familiarity.Guessed)
                Say(p.Id, name + " again, in a second place. Its meaning comes to you: \"" + meaning + "\"? Try speaking it (X).");
            else if (now == Familiarity.Guessed)
                Say(p.Id, name + " again. You think it means \"" + meaning + "\". Try speaking it (X).");
            else
                Say(p.Id, "Carved here: " + name + ". You don't know what it means yet. Find it written somewhere else.");
        }

        // ===================================================================== speaking

        /// <summary>
        /// The player speaks a phrase (word ids) from `eye` looking along `dir` (metres). It may
        /// wake a mechanism, raise stone, or push, pull or weigh down creatures. Returns true if
        /// anything answered. The reason it didn't is said to the player.
        /// </summary>
        public bool Speak(int id, IList<string> phrase, Vec3 eye, Vec3 dir)
        {
            PlayerRec p;
            if (!Players.TryGetValue(id, out p) || !p.Alive) return false;
            if (Lexicon == null) { Say(id, "No words are loaded."); return false; }
            if (phrase == null || phrase.Count == 0) { Say(id, "Pick words to say first (number keys), then speak."); return false; }
            if (Time < p.NextWord) { Say(id, "Your voice is still ringing. Wait a moment."); return false; }
            dir = dir.Normalized;
            WordKnowledge k = p.C.Lang;
            WordDef action = null;
            foreach (string w in phrase)
            {
                WordDef def = Lexicon.Word(w);
                if (def == null) { Say(id, "That is not a word anyone remembers."); return false; }
                Familiarity f = k.Of(w);
                if (f == Familiarity.Unseen) { Say(id, "You have never seen that word."); return false; }
                if (f == Familiarity.Seen) { Say(id, "You have seen " + WordName(w) + " only once. Find it written in a second place before you try it."); return false; }
                if (def.Role == WordRole.Action && action == null) action = def;
            }
            if (action == null) { Say(id, "A phrase needs a word that does something."); return false; }

            // Is a mechanism of the ruin in front of you?
            RuinMechanism mech = AimedMechanism(eye, dir, Math.Max(action.RangeVoxels / M, 6));
            if (mech != null)
            {
                if (mech.Word != action.Id)
                {
                    bool allKnown = true;
                    foreach (string w in phrase) allKnown &= k.Knows(w);
                    if (!allKnown) { Say(id, Cap(mech.Name) + " does not answer to " + WordName(action.Id) + "."); return false; }
                }
                else return Answer(p, mech, phrase, action);
            }

            // Otherwise the words act on the world. A guessed word can be tried: if it works, you know it.
            var trying = new WordKnowledge();
            foreach (string w in phrase) trying.Teach(w);
            double reach = action.RangeVoxels + WorldConfig.ServerReachSlack;
            VoxelHit hit = VoxelRaycast.Cast(World, eye.X * M, eye.Y * M, eye.Z * M, dir.X, dir.Y, dir.Z, reach);
            int tx = hit.Hit ? hit.X : (int)Math.Floor((eye.X + dir.X * reach) * M);
            int ty = hit.Hit ? hit.Y : (int)Math.Floor((eye.Y + dir.Y * reach) * M);
            int tz = hit.Hit ? hit.Z : (int)Math.Floor((eye.Z + dir.Z * reach) * M);
            SpeakResult r = Interpreter.Speak(Lexicon, trying, phrase, eye.X * M, eye.Y * M, eye.Z * M, dir.X, dir.Y, dir.Z,
                tx, ty, tz, key => World.Blocks.Has(key) ? World.Blocks.Id(key) : (ushort)0);
            if (!r.Ok) { Say(id, SpeakErrorText(r)); return false; }
            double cost = r.Debt * p.C.Mods["debt_cost_mult"];
            if (!p.God && !p.C.CanOwe(cost)) { Say(id, "The weight of the word is more than you can carry. Rest a while."); return false; }

            int changed = 0;
            foreach (var op in r.Edits) changed += Apply(op).Changed;
            int moved = 0;
            foreach (var f in r.Forces) moved += Force(p, f);
            if (!p.God) p.C.Owe(cost);
            p.NextWord = Time + r.CooldownSeconds;
            Fx("speak", id, 0, changed + moved, eye);
            if (changed + moved == 0)
            {
                Say(id, WordName(action.Id) + " leaves your mouth, and nothing answers." + (r.Forces.Count > 0 ? " Aim at a creature." : ""));
                return false;
            }
            LearnFrom(p, phrase);
            return true;
        }

        bool Answer(PlayerRec p, RuinMechanism m, IList<string> phrase, WordDef action)
        {
            if (m.Needs.Length > 0 && !WorldFlags.Contains(m.Needs))
            {
                Fx("glow", p.Id, 0, 0, m.Point);
                Say(p.Id, m.NotYet);
                p.NextWord = Time + 1;
                return false;
            }
            foreach (var op in m.Ops) Apply(op);
            if (m.Sets.Length > 0) WorldFlags.Add(m.Sets);
            if (!p.God) p.C.Owe(action.Debt * p.C.Mods["debt_cost_mult"] * 0.5);
            p.NextWord = Time + 1;
            Fx("answer", p.Id, 0, 1, m.Point);
            Say(p.Id, m.Answer);
            LearnFrom(p, phrase);
            if (m.Sets == EchoRuin.FlagRemembered) Remembered(p);
            return true;
        }

        void LearnFrom(PlayerRec p, IList<string> phrase)
        {
            foreach (string w in phrase)
            {
                if (p.C.Lang.Knows(w)) continue;
                p.C.Lang.Confirm(w);
                p.C.Learn(w);
                Fx("learn", p.Id, 0, 0, p.Chest);
                Say(p.Id, "The word answered. You know " + WordName(w) + " now: \"" + WordMeaning(w) + "\".");
            }
        }

        /// <summary>The heart fell: everyone in the world hears it, and the one who woke it learns a word.</summary>
        void Remembered(PlayerRec p)
        {
            foreach (var other in Players.Values) Say(other.Id, "THE WORLD REMEMBERS.");
            if (Lexicon.Has("word_weigh") && !p.C.Lang.Knows("word_weigh"))
            {
                p.C.Learn("word_weigh");
                Say(p.Id, "The heart's echo leaves a word in you: " + WordName("word_weigh") + ", \"" + WordMeaning("word_weigh") + "\".");
            }
            if (Items.Has("word_fragment")) p.C.Inv.Add("word_fragment", 1);
        }

        RuinMechanism AimedMechanism(Vec3 eye, Vec3 dir, double rangeM)
        {
            if (Ruin == null) return null;
            RuinMechanism best = null;
            double bestAlong = double.MaxValue;
            foreach (var m in Ruin.Mechanisms)
            {
                if (EchoRuin.Done(World, m)) continue;
                Vec3 to = m.Point - eye;
                double along = Vec3.Dot(to, dir);
                if (along < 0 || along > rangeM) continue;
                double off = (to - dir * along).Length;
                // Close up, a looser aim is enough.
                if (off > MechanismAimM + along * 0.08) continue;
                if (along < bestAlong) { bestAlong = along; best = m; }
            }
            return best;
        }

        /// <summary>Pushes, pulls or weighs down creatures. Returns how many it moved.</summary>
        int Force(PlayerRec p, ForceEffect f)
        {
            var origin = new Vec3(f.OriginX, f.OriginY, f.OriginZ);
            var dir = new Vec3(f.DirX, f.DirY, f.DirZ);
            double cone = f.Kind == "push" ? f.ConeDeg : 18;
            double cosCone = Math.Cos(cone * Math.PI / 180);
            var hits = new List<Creature>();
            foreach (var c in Creatures)
            {
                if (c.State == "dead") continue;
                Vec3 to = c.Pos + new Vec3(0, c.Def.HeightM * 0.5, 0) - origin;
                double d = to.Length;
                if (d > f.RangeM + c.Def.RadiusM || d < 0.01) continue;
                if (Vec3.Dot(to / d, dir) < cosCone) continue;
                hits.Add(c);
            }
            // Pull and weigh take hold of one creature, the nearest; push sweeps the whole cone.
            if (f.Kind != "push" && hits.Count > 1)
            {
                hits.Sort((a, b) => Vec3.Distance(a.Pos, origin).CompareTo(Vec3.Distance(b.Pos, origin)));
                hits.RemoveRange(1, hits.Count - 1);
            }
            foreach (var c in hits)
            {
                double give = 1 / Math.Sqrt(Math.Max(1, c.Def.Mass));
                Vec3 flatTo = (c.Pos - origin).Flat;
                Vec3 away = flatTo.Length > 0.01 ? flatTo.Normalized : dir.Flat.Normalized;
                switch (f.Kind)
                {
                    case "push":
                        c.Vel = c.Vel + away * (f.ImpulseMps * give) + new Vec3(0, 2.5 * give, 0);
                        c.Launched = 0.6;
                        if (c.Def.Behavior == "guardian" && c.State == "slam_windup") { c.State = "stagger"; c.T = 1.0; }
                        break;
                    case "pull":
                        if (!Brains.OnPull(this, c))
                        {
                            c.Vel = c.Vel - away * (f.ImpulseMps * give * 0.7) + new Vec3(0, 1.5 * give, 0);
                            c.Launched = 0.6;
                        }
                        break;
                    case "weigh":
                        c.Vel = new Vec3(c.Vel.X * 0.2, -f.ImpulseMps, c.Vel.Z * 0.2);
                        if (c.Def.Behavior != "guardian")
                        {
                            c.State = "stagger";
                            c.T = f.DurationS;
                        }
                        break;
                }
                c.Threat = p.Pos;
                Fx("force", p.Id, c.Id, 0, c.Pos);
            }
            if (hits.Count > 0) p.CombatAt = Time;
            return hits.Count;
        }

        string SpeakErrorText(SpeakResult r)
        {
            switch (r.Error)
            {
                case SpeakError.OutOfRange: return "Too far. " + WordName(r.Word) + " reaches only so far: aim at ground nearer to you.";
                case SpeakError.TwoActions: return "Only one word in a phrase can do something.";
                case SpeakError.TwoThings: return "Name only one thing in a phrase.";
                case SpeakError.WrongThing: return WordName(r.Word) + " is not something that word can act on.";
                case SpeakError.NoDirection: return "Look somewhere first.";
                case SpeakError.NoAction: return "A phrase needs a word that does something.";
                default: return "The words don't fit together.";
            }
        }

        static string Cap(string s) { return s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s.Substring(1); }

        // ===================================================================== the ruin

        void TickRuin()
        {
            if (Ruin == null || WorldFlags.Contains(EchoRuin.FlagSentinel)) return;
            if (!CreatureDefs.ContainsKey("echo_sentinel")) return;
            foreach (var c in Creatures) if (c.Id == _sentinel && c.State != "dead") return;
            Vec3 court = Ruin.CourtM;
            foreach (var p in Players.Values)
            {
                if (!p.Alive || Vec3.Distance(p.Pos, court) > SentinelAppearM) continue;
                Creature s = SpawnCreature("echo_sentinel", Ruin.SentinelHomeM, 0);
                if (s == null) return;
                s.Yaw = Math.PI;   // facing back toward the way in
                _sentinel = s.Id;
                return;
            }
        }

        void OnCreatureKilled(Creature c, int byPlayer)
        {
            if (c.Def.Key != "echo_sentinel" || Ruin == null) return;
            WorldFlags.Add(EchoRuin.FlagSentinel);
            foreach (var p in Players.Values)
                if (Vec3.Distance(p.Pos, c.Pos) < 60) Say(p.Id, "The sentinel breaks apart. High on the far wall, the heart flickers. Its word is Sem.");
        }

        /// <summary>The living Echo Sentinel, or null.</summary>
        public Creature Sentinel
        {
            get
            {
                foreach (var c in Creatures) if (c.Id == _sentinel && c.State != "dead") return c;
                return null;
            }
        }

        /// <summary>What this player should do next in the echo ruin itself.</summary>
        Objective RuinObjective(int id)
        {
            var o = new Objective { Title = "The World Remembers" };
            PlayerRec p;
            if (Ruin == null || !Players.TryGetValue(id, out p)) { o.Done = true; return o; }
            WordKnowledge k = p.C.Lang;
            RuinMechanism gate = Ruin.Mechanism("gate"), stair = Ruin.Mechanism("stair"), heart = Ruin.Mechanism("heart");
            if (EchoRuin.Done(World, heart) || WorldFlags.Contains(EchoRuin.FlagRemembered))
            {
                bool out_ = Vec3.Distance(p.Pos, Ruin.ExitM) < 8 || (!Ruin.Inside(p.Pos) && Vec3.Distance(p.Pos, Ruin.CourtM) > 40);
                o.Done = out_;
                o.Text = out_ ? "The world remembers. Explore, and use your words." : "Climb the stair behind the heart, back to the light.";
                o.Target = out_ ? (Vec3?)null : Ruin.ExitM;
                return o;
            }
            if (!EchoRuin.Done(World, gate))
            {
                if (k.Of(gate.Word) < Familiarity.Guessed)
                {
                    RuinInscription next = NextUnread(k, gate.Word);
                    bool far = Vec3.Distance(p.Pos, Ruin.EntranceM) > 20 && !Ruin.Inside(p.Pos);
                    o.Text = far ? "Find the echo ruin, south of the Confluence." : "Read the carved words (E) in the ruin.";
                    o.Target = far ? Ruin.EntranceM : (next != null ? next.Point() : Ruin.GateM);
                    return o;
                }
                o.Text = "Speak " + WordName(gate.Word) + " (\"" + WordMeaning(gate.Word) + "\") at the sealed gate: X, pick it, Enter.";
                o.Target = gate.Point;
                return o;
            }
            if (!EchoRuin.Done(World, stair) && !k.Knows(stair.Word))
            {
                if (k.Of(stair.Word) < Familiarity.Guessed)
                {
                    RuinInscription next = NextUnread(k, stair.Word);
                    o.Text = "Find " + WordName(stair.Word) + " carved in a second place.";
                    o.Target = next != null ? next.Point() : Ruin.StepM;
                    return o;
                }
                o.Text = "Speak " + WordName(stair.Word) + " (\"" + WordMeaning(stair.Word) + "\") at the carved socket under the ledge.";
                o.Target = stair.Point;
                return o;
            }
            if (!WorldFlags.Contains(EchoRuin.FlagSentinel))
            {
                string hint = k.Of("word_pull") >= Familiarity.Guessed
                    ? " Strike its back. When it raises its fist, speak " + WordName("word_pull") + " to drag it down."
                    : " Strike it from behind, and read the pillars.";
                o.Text = "Defeat the Echo Sentinel." + hint;
                o.Target = Sentinel != null ? Sentinel.Pos : Ruin.CourtM;
                return o;
            }
            o.Text = k.Of(heart.Word) >= Familiarity.Guessed
                ? "Speak " + WordName(heart.Word) + " at the Echo Heart, high on the far wall."
                : "Read the pillars (E), then wake the Echo Heart on the far wall.";
            o.Target = heart.Point;
            return o;
        }

        RuinInscription NextUnread(WordKnowledge k, string word)
        {
            foreach (var i in Ruin.Inscriptions)
                if (i.Word == word && !k.SeenAt(word, i.Place)) return i;
            return null;
        }
    }
}
