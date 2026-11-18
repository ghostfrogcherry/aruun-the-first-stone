using System;
using System.Collections.Generic;
using Aruun.Core.Combat;
using Aruun.Core.Content;
using Aruun.Core.Creatures;
using Aruun.Core.People;
using Aruun.Core.Ruins;
using Aruun.Core.Town;
using Aruun.Core.Net;

namespace Aruun.Core.Play
{
    /// <summary>
    /// The village's people and the quests they give: who stands where, what they say, and
    /// how a conversation moves a quest on. Conversations are data (content/people), so new
    /// lines and quests need no code.
    /// </summary>
    public sealed partial class Host
    {
        public readonly List<PersonRec> People = new List<PersonRec>();
        public readonly Dictionary<string, QuestDef> Quests = new Dictionary<string, QuestDef>();
        /// <summary>How close you must be to talk to someone, in metres.</summary>
        public const double TalkReachM = 3.5;
        public const double PersonWalkMps = 1.1;

        int _nextPerson = 1;

        /// <summary>The village of this world, or null.</summary>
        public Village Village { get { return World.Generator.Village; } }

        /// <summary>Places the village's people at their spots and loads the quests.</summary>
        public void LoadPeople(IEnumerable<PersonDef> people, IEnumerable<QuestDef> quests)
        {
            foreach (var q in quests) Quests[q.Id] = q;
            if (Village == null) return;
            foreach (var d in people)
            {
                Vec3 spot;
                if (!Village.Spots.TryGetValue(d.Spot, out spot)) continue;
                // A spot with a height is indoors (the library): stand on the floor below it.
                bool indoors = spot.Y > 0;
                spot = new Vec3(spot.X, indoors ? FloorBelow(spot.X, spot.Y + 1, spot.Z) : GroundHeight(spot.X, spot.Z), spot.Z);
                People.Add(new PersonRec { Id = _nextPerson++, Def = d, Indoors = indoors, Pos = spot, Home = spot, Goal = spot, Yaw = Math.PI, Wait = 2 + _rng.NextDouble() * 3 });
            }
        }

        public PersonRec Person(string id)
        {
            foreach (var r in People) if (r.Def.Id == id) return r;
            return null;
        }

        PersonRec PersonById(int id)
        {
            foreach (var r in People) if (r.Id == id) return r;
            return null;
        }

        // ===================================================================== walking about

        void TickPeople(double dt)
        {
            foreach (var r in People)
            {
                if (r.TalkingTo != 0)
                {
                    PlayerRec p;
                    if (!Players.TryGetValue(r.TalkingTo, out p) || p.Talk == null || p.Talk.Person != r.Id || Vec3.Distance(p.Pos, r.Pos) > TalkReachM * 2)
                    {
                        if (p != null && p.Talk != null && p.Talk.Person == r.Id) p.Talk = null;
                        r.TalkingTo = 0;
                    }
                    else
                    {
                        r.Walking = false;
                        r.Yaw = Brains.YawTo((p.Pos - r.Pos).Flat);
                        continue;
                    }
                }
                if (!r.Walking)
                {
                    r.Wait -= dt;
                    if (r.Wait > 0) continue;
                    // By night everyone stays home; by day they potter about near it.
                    double a = _rng.NextDouble() * Math.PI * 2, d = IsNight ? 0 : _rng.NextDouble() * r.Def.WanderM;
                    r.Goal = r.Home + new Vec3(Math.Cos(a) * d, 0, Math.Sin(a) * d);
                    r.Walking = true;
                }
                Vec3 to = (r.Goal - r.Pos).Flat;
                double len = to.Length;
                if (len < 0.15)
                {
                    r.Walking = false;
                    r.Wait = 3 + _rng.NextDouble() * 5;
                    continue;
                }
                Vec3 step = to / len * Math.Min(len, PersonWalkMps * dt);
                Vec3 next = new Vec3(r.Pos.X + step.X, r.Pos.Y, r.Pos.Z + step.Z);
                if (r.Indoors)
                {
                    // Indoors there is furniture: walk round it (pick another goal) rather than over it.
                    if (Blocked(next)) { r.Walking = false; r.Wait = 1 + _rng.NextDouble() * 2; continue; }
                    r.Pos = new Vec3(next.X, FloorBelow(next.X, next.Y + 0.3, next.Z), next.Z);
                }
                else r.Pos = new Vec3(next.X, GroundHeight(next.X, next.Z), next.Z);
                r.Yaw = Brains.YawTo(to);
            }
        }

        // ===================================================================== talking

        /// <summary>The top of the first solid block at or below a height (metres), searching 4 m down.</summary>
        public double FloorBelow(double x, double y, double z)
        {
            int vx = (int)Math.Floor(x * M), vz = (int)Math.Floor(z * M), top = (int)Math.Floor(y * M);
            for (int vy = top; vy > top - 16; vy--)
                if (World.Blocks.Get(World.Get(vx, vy, vz)).Collision) return (vy + 1) / M;
            return y;
        }

        /// <summary>Is there something solid from knee to head height at a person's feet?</summary>
        bool Blocked(Vec3 feet)
        {
            int vx = (int)Math.Floor(feet.X * M), vz = (int)Math.Floor(feet.Z * M), vy = (int)Math.Floor(feet.Y * M);
            for (int dy = 2; dy <= 7; dy++)
                if (World.Blocks.Get(World.Get(vx, vy + dy, vz)).Collision) return true;
            return false;
        }

        /// <summary>How close you must be to a door to go through it, in metres.</summary>
        public const double PortalReachM = 3.5;

        /// <summary>Goes through a door (the college's, the library's way out) you are looking at.</summary>
        bool UsePortal(PlayerRec p, Vec3 eye, Vec3 dir)
        {
            if (Village == null) return false;
            foreach (var g in Village.Portals)
            {
                Vec3 to = g.At + new Vec3(0, 1.5, 0) - eye;
                double along = Vec3.Dot(to, dir);
                if (along < 0 || along > PortalReachM + 1) continue;
                if ((to - dir * along).Length > 1.8) continue;
                Enter(p, g);
                return true;
            }
            return false;
        }

        /// <summary>Moves a player through a door.</summary>
        public void Enter(PlayerRec p, Portal g)
        {
            if (p.Talk != null) EndTalk(p.Id);
            p.Pos = g.To;
            p.RespawnAt = g.To;
            p.ArriveYaw = g.ToYaw;
            Say(p.Id, g.Message);
            Fx("portal", p.Id, 0, 0, g.To);
        }

        bool TalkTo(PlayerRec p, Vec3 eye, Vec3 dir)
        {
            PersonRec best = null;
            double bestAlong = double.MaxValue;
            foreach (var r in People)
            {
                Vec3 to = r.Pos + new Vec3(0, 1.4, 0) - eye;
                double along = Vec3.Dot(to, dir);
                if (along < 0 || along > TalkReachM) continue;
                if ((to - dir * along).Length > 0.9) continue;
                if (along < bestAlong) { bestAlong = along; best = r; }
            }
            if (best == null) return false;
            StartTalk(p, best);
            return true;
        }

        /// <summary>Starts (or restarts) a conversation with a person.</summary>
        public Conversation StartTalk(PlayerRec p, PersonRec r)
        {
            r.TalkingTo = p.Id;
            p.Talk = Converse(p, r, null);
            return p.Talk;
        }

        /// <summary>The player picks answer `index` in their conversation. Returns the next one, or null when it ends.</summary>
        public Conversation Choose(int id, int index)
        {
            if (Link != null) { Link.Send("choose", Wire.Join(index)); PlayerRec me; return Players.TryGetValue(id, out me) ? me.Talk : null; }
            PlayerRec p;
            if (!Players.TryGetValue(id, out p) || p.Talk == null) return null;
            PersonRec r = PersonById(p.Talk.Person);
            if (r == null || index < 0 || index >= p.Talk.Choices.Count) return p.Talk;
            if (index >= p.Talk.Defs.Count)
            {
                // The last answer is always goodbye.
                EndTalk(id);
                return null;
            }
            ChoiceDef c = p.Talk.Defs[index];
            string reply = c.Reply;
            string failed = Act(p, c.Do);
            if (failed.Length > 0) reply = failed;
            if (reply.Length == 0 && c.Do.Count == 0) { EndTalk(id); return null; }
            p.Talk = Converse(p, r, reply.Length > 0 ? reply : null);
            return p.Talk;
        }

        public void EndTalk(int id)
        {
            if (Link != null) { Link.Send("endtalk", ""); PlayerRec me; if (Players.TryGetValue(id, out me)) me.Talk = null; return; }
            PlayerRec p;
            if (!Players.TryGetValue(id, out p) || p.Talk == null) return;
            PersonRec r = PersonById(p.Talk.Person);
            if (r != null && r.TalkingTo == id) r.TalkingTo = 0;
            p.Talk = null;
        }

        Conversation Converse(PlayerRec p, PersonRec r, string sayInstead)
        {
            var conv = new Conversation { Person = r.Id, Name = r.Def.ShortName };
            TalkDef t = null;
            foreach (var cand in r.Def.Talk) if (Holds(p, cand.When)) { t = cand; break; }
            conv.Say = sayInstead ?? (t != null ? t.Say : "...");
            if (t != null)
                foreach (var c in t.Choices)
                {
                    conv.Choices.Add(c.Text);
                    conv.Defs.Add(c);
                }
            conv.Choices.Add("Goodbye.");
            return conv;
        }

        /// <summary>Do a talk line's conditions hold for this player?</summary>
        bool Holds(PlayerRec p, Dictionary<string, object> when)
        {
            string quest = MiniJson.Str(when, "quest");
            if (quest.Length > 0)
            {
                QuestLog log = p.C.Quests;
                string state = MiniJson.Str(when, "state");
                if (state == "none" && log.Has(quest)) return false;
                if (state == "active" && !log.IsActive(quest)) return false;
                if (state == "done" && !log.IsDone(quest)) return false;
                string stage = MiniJson.Str(when, "stage");
                if (stage.Length > 0)
                {
                    QuestDef q;
                    if (!log.IsActive(quest) || !Quests.TryGetValue(quest, out q) || q.StageIndex(stage) != log.Stage(quest)) return false;
                }
                object ready;
                if (when.TryGetValue("ready", out ready) && ready is bool && (bool)ready != StageReady(p, quest)) return false;
            }
            string flag = MiniJson.Str(when, "flag");
            if (flag.Length > 0 && !WorldFlags.Contains(flag)) return false;
            return true;
        }

        /// <summary>Has the player got everything the quest's current stage asks for?</summary>
        public bool StageReady(PlayerRec p, string quest)
        {
            StageDef s = CurrentStage(p, quest);
            if (s == null) return false;
            foreach (var kv in s.Needs) if (p.C.Inv.Count(kv.Key) < kv.Value) return false;
            return s.DoneFlag.Length == 0 || WorldFlags.Contains(s.DoneFlag);
        }

        public StageDef CurrentStage(PlayerRec p, string quest)
        {
            QuestDef q;
            if (!Quests.TryGetValue(quest, out q) || !p.C.Quests.IsActive(quest)) return null;
            int i = p.C.Quests.Stage(quest);
            return i >= 0 && i < q.Stages.Count ? q.Stages[i] : null;
        }

        /// <summary>Runs a choice's actions. Returns what the person says if it could not be done, else "".</summary>
        string Act(PlayerRec p, Dictionary<string, object> act)
        {
            string start = MiniJson.Str(act, "start");
            if (start.Length > 0) StartQuest(p, start);
            string advance = MiniJson.Str(act, "advance");
            if (advance.Length > 0 && !Advance(p, advance))
            {
                StageDef s = CurrentStage(p, advance);
                if (s != null && s.Needs.Count > 0)
                {
                    var parts = new List<string>();
                    foreach (var kv in s.Needs) parts.Add(kv.Value + " " + (Items.Has(kv.Key) ? Items.Get(kv.Key).Name.ToLowerInvariant() : kv.Key));
                    return "You haven't got it all yet. I need " + string.Join(" and ", parts) + ".";
                }
                return "Not yet.";
            }
            return "";
        }

        public bool StartQuest(PlayerRec p, string quest)
        {
            QuestDef q;
            if (!Quests.TryGetValue(quest, out q) || p.C.Quests.Has(quest)) return false;
            p.C.Quests.Set(quest, 0);
            Fx("quest", p.Id, 0, 0, p.Pos);
            Say(p.Id, "New quest: " + q.Title + ". (L: journal)");
            AdvanceFlags(p);
            return true;
        }

        /// <summary>Finishes the current stage if its needs are met (taking the items). Returns false if they are not.</summary>
        public bool Advance(PlayerRec p, string quest)
        {
            QuestDef q;
            if (!Quests.TryGetValue(quest, out q) || !StageReady(p, quest)) return false;
            StageDef s = CurrentStage(p, quest);
            foreach (var kv in s.Needs) p.C.Inv.Remove(kv.Key, kv.Value);
            int next = p.C.Quests.Stage(quest) + 1;
            if (next >= q.Stages.Count) Complete(p, q);
            else
            {
                p.C.Quests.Set(quest, next);
                Say(p.Id, q.Title + ": " + q.Stages[next].Text);
                AdvanceFlags(p);
            }
            return true;
        }

        void Complete(PlayerRec p, QuestDef q)
        {
            p.C.Quests.Set(q.Id, QuestLog.Done);
            AwardFor(p, "quest");
            var parts = new List<string>();
            foreach (var kv in q.RewardItems)
            {
                if (!Items.Has(kv.Key)) continue;
                p.C.Inv.Add(kv.Key, kv.Value);
                parts.Add(kv.Value + " " + Items.Get(kv.Key).Name.ToLowerInvariant());
            }
            if (q.RewardWord.Length > 0 && Lexicon != null && Lexicon.Has(q.RewardWord) && !p.C.Lang.Knows(q.RewardWord))
            {
                p.C.Learn(q.RewardWord);
                parts.Add("the word " + WordName(q.RewardWord));
            }
            Fx("quest_done", p.Id, 0, 0, p.Pos);
            Say(p.Id, "Quest complete: " + q.Title + (parts.Count > 0 ? ". You receive " + string.Join(", ", parts) + "." : "."));
        }

        /// <summary>Stages finished by a world flag move on by themselves.</summary>
        void AdvanceFlags(PlayerRec p)
        {
            foreach (var q in Quests.Values)
            {
                for (int guard = 0; guard < 8; guard++)
                {
                    StageDef s = CurrentStage(p, q.Id);
                    if (s == null || s.DoneFlag.Length == 0 || s.Needs.Count > 0 || !WorldFlags.Contains(s.DoneFlag)) break;
                    Advance(p, q.Id);
                }
            }
        }

        void AdvanceQuestFlags()
        {
            foreach (var p in Players.Values) AdvanceFlags(p);
        }

        // ===================================================================== what to do next

        /// <summary>
        /// What this player should do next and where: the main quest first, then any side
        /// quest. Inside the echo ruin the ruin's own steps lead.
        /// </summary>
        public Objective ObjectiveFor(int id)
        {
            PlayerRec p;
            if (!Players.TryGetValue(id, out p)) return new Objective { Done = true };
            QuestDef main = null;
            foreach (var q in Quests.Values) if (q.Main) { main = q; break; }
            if (main == null) return RuinObjective(id);
            QuestLog log = p.C.Quests;
            bool inRuin = Ruin != null && (Ruin.Inside(p.Pos) || Vec3.Distance(p.Pos, Ruin.EntranceM) < 20);

            if (!log.Has(main.Id))
            {
                if (inRuin && !WorldFlags.Contains(EchoRuin.FlagRemembered)) return RuinObjective(id);
                PersonRec giver = Person(main.Giver);
                return new Objective
                {
                    Title = main.Title,
                    Text = giver != null ? "Talk to " + giver.Def.ShortName + " in the Confluence plaza (E)." : main.Stages[0].Text,
                    Target = giver != null ? (Vec3?)giver.Pos : null,
                };
            }
            if (log.IsActive(main.Id))
            {
                StageDef s = CurrentStage(p, main.Id);
                if (s.Target == "ruin") return RuinObjective(id);
                return ForStage(main, s);
            }
            foreach (var q in Quests.Values)
                if (log.IsActive(q.Id)) return ForStage(q, CurrentStage(p, q.Id));
            return new Objective { Title = main.Title, Done = true };
        }

        Objective ForStage(QuestDef q, StageDef s)
        {
            var o = new Objective { Title = q.Title, Text = s.Text };
            if (s.Target.StartsWith("person:", StringComparison.Ordinal))
            {
                PersonRec r = Person(s.Target.Substring(7));
                if (r != null) o.Target = r.Pos;
            }
            else if (s.Target == "ruin" && Ruin != null) o.Target = Ruin.EntranceM;
            return o;
        }
    }
}
