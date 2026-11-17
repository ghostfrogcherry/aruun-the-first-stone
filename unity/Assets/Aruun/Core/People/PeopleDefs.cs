using System;
using System.Collections.Generic;
using Aruun.Core.Content;

namespace Aruun.Core.People
{
    /// <summary>One thing a person can say back: a line, what it does, and what they answer.</summary>
    public sealed class ChoiceDef
    {
        public string Text = "";
        /// <summary>Actions: {"start": quest} or {"advance": quest}.</summary>
        public Dictionary<string, object> Do = new Dictionary<string, object>();
        /// <summary>What the person answers ("" to just carry on).</summary>
        public string Reply = "";
    }

    /// <summary>One thing a person may say, when its conditions hold. The first that holds is used.</summary>
    public sealed class TalkDef
    {
        /// <summary>Conditions: quest, state (none, active, done), stage, ready (the stage's needs are in the bag), flag.</summary>
        public Dictionary<string, object> When = new Dictionary<string, object>();
        public string Say = "";
        public readonly List<ChoiceDef> Choices = new List<ChoiceDef>();
    }

    /// <summary>A person of the village, from content/people/people.json.</summary>
    public sealed class PersonDef
    {
        public string Id = "";
        public string Name = "";
        public string Role = "";
        /// <summary>The model id (content/models) and the village spot they live at.</summary>
        public string Model = "";
        public string Spot = "";
        public double WanderM = 2;
        public readonly List<TalkDef> Talk = new List<TalkDef>();

        /// <summary>The name without its "(placeholder)" note.</summary>
        public string ShortName
        {
            get { int cut = Name.IndexOf(" (", StringComparison.Ordinal); return cut > 0 ? Name.Substring(0, cut) : Name; }
        }

        public static List<PersonDef> FromJson(string json)
        {
            var list = new List<PersonDef>();
            foreach (object o in MiniJson.List(MiniJson.ParseObject(json), "people"))
            {
                var p = o as Dictionary<string, object>;
                if (p == null) continue;
                var d = new PersonDef
                {
                    Id = MiniJson.Str(p, "id"), Name = MiniJson.Str(p, "name"), Role = MiniJson.Str(p, "role"),
                    Model = MiniJson.Str(p, "model"), Spot = MiniJson.Str(p, "spot"), WanderM = MiniJson.Num(p, "wander_m", 2),
                };
                foreach (object to in MiniJson.List(p, "talk"))
                {
                    var t = to as Dictionary<string, object>;
                    if (t == null) continue;
                    var td = new TalkDef { When = MiniJson.Obj(t, "when"), Say = MiniJson.Str(t, "say") };
                    foreach (object co in MiniJson.List(t, "choices"))
                    {
                        var c = co as Dictionary<string, object>;
                        if (c == null) continue;
                        td.Choices.Add(new ChoiceDef { Text = MiniJson.Str(c, "text"), Do = MiniJson.Obj(c, "do"), Reply = MiniJson.Str(c, "reply") });
                    }
                    d.Talk.Add(td);
                }
                list.Add(d);
            }
            return list;
        }
    }

    /// <summary>One step of a quest: what to do, where it points, and what finishes it.</summary>
    public sealed class StageDef
    {
        public string Id = "";
        public string Text = "";
        /// <summary>Where it points: "person:id", "ruin", or "".</summary>
        public string Target = "";
        /// <summary>Items handed over to finish it (taken from the bag when it is advanced).</summary>
        public readonly Dictionary<string, int> Needs = new Dictionary<string, int>();
        /// <summary>A world flag that finishes it on its own ("" for none).</summary>
        public string DoneFlag = "";
    }

    /// <summary>A quest, from content/quests/quests.json.</summary>
    public sealed class QuestDef
    {
        public string Id = "";
        public string Title = "";
        /// <summary>The main story quest: the HUD follows it first.</summary>
        public bool Main;
        public string Giver = "";
        public readonly List<StageDef> Stages = new List<StageDef>();
        public readonly Dictionary<string, int> RewardItems = new Dictionary<string, int>();
        public string RewardWord = "";

        public int StageIndex(string id)
        {
            for (int i = 0; i < Stages.Count; i++) if (Stages[i].Id == id) return i;
            return -1;
        }

        public static List<QuestDef> FromJson(string json)
        {
            var list = new List<QuestDef>();
            foreach (object o in MiniJson.List(MiniJson.ParseObject(json), "quests"))
            {
                var q = o as Dictionary<string, object>;
                if (q == null) continue;
                var d = new QuestDef { Id = MiniJson.Str(q, "id"), Title = MiniJson.Str(q, "title"), Main = MiniJson.Bool(q, "main"), Giver = MiniJson.Str(q, "giver") };
                foreach (object so in MiniJson.List(q, "stages"))
                {
                    var s = so as Dictionary<string, object>;
                    if (s == null) continue;
                    var st = new StageDef
                    {
                        Id = MiniJson.Str(s, "id"), Text = MiniJson.Str(s, "text"), Target = MiniJson.Str(s, "target"),
                        DoneFlag = MiniJson.Str(MiniJson.Obj(s, "done_when"), "flag"),
                    };
                    foreach (var kv in MiniJson.Obj(s, "needs")) st.Needs[kv.Key] = Convert.ToInt32(kv.Value);
                    d.Stages.Add(st);
                }
                var reward = MiniJson.Obj(q, "reward");
                foreach (var kv in MiniJson.Obj(reward, "items")) d.RewardItems[kv.Key] = Convert.ToInt32(kv.Value);
                d.RewardWord = MiniJson.Str(reward, "word");
                if (d.Stages.Count == 0) throw new FormatException("quests.json: quest '" + d.Id + "' has no stages");
                list.Add(d);
            }
            return list;
        }
    }

    /// <summary>
    /// Where a character is in each quest: the index of the stage they are on, or Done.
    /// Saved with the character as "quest id stage" lines.
    /// </summary>
    public sealed class QuestLog
    {
        public const int Done = 1000;
        readonly Dictionary<string, int> _at = new Dictionary<string, int>();

        public bool Has(string quest) { return _at.ContainsKey(quest); }
        public bool IsDone(string quest) { int s; return _at.TryGetValue(quest, out s) && s >= Done; }
        public bool IsActive(string quest) { int s; return _at.TryGetValue(quest, out s) && s < Done; }
        /// <summary>The stage index, -1 if not started, Done if finished.</summary>
        public int Stage(string quest) { int s; return _at.TryGetValue(quest, out s) ? s : -1; }
        public void Set(string quest, int stage) { _at[quest] = stage; }
        public IEnumerable<KeyValuePair<string, int>> All { get { return _at; } }
    }
}
