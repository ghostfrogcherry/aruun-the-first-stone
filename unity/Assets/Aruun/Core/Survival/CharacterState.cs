using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Aruun.Core.Language;
using Aruun.Core.Content;
using Aruun.Core.Items;
using Aruun.Core.Mining;
using Aruun.Core.People;
using Aruun.Core.Skills;

namespace Aruun.Core.Survival
{
    /// <summary>
    /// Everything the host knows about one character: vitals, conditions, buffs, bag, known words
    /// and home Tether. Pure logic so it can be tested; the game feeds it flags and time.
    /// Saved per character, apart from the world, so a character can visit other worlds.
    /// </summary>
    public sealed class CharacterState
    {
        public string Guid = "";
        public string DisplayName = "Wanderer";
        public double Hp = 100;
        public double Stamina = 100;
        public Dictionary<string, double> Cond = new Dictionary<string, double>();
        /// <summary>Buff id to seconds left.</summary>
        public Dictionary<string, double> Buffs = new Dictionary<string, double>();
        public Inventory Inv;
        /// <summary>The hotbar slot in hand.</summary>
        public int Selected;
        public List<string> Words = new List<string>();
        /// <summary>What this character has seen, guessed and knows of Avarra. Words above mirrors its known words.</summary>
        public WordKnowledge Lang = new WordKnowledge();
        /// <summary>Where this character is in each quest.</summary>
        public QuestLog Quests = new QuestLog();
        /// <summary>Inscription compositions this character has found by cutting their words together (ids from inscriptions.json).</summary>
        public HashSet<string> Combos = new HashSet<string>();
        /// <summary>Skill tree nodes this character has learned, and points not yet spent.</summary>
        public HashSet<string> Skills = new HashSet<string>();
        public int SkillPoints;
        /// <summary>Points earned since the host last told the player. The host clears it.</summary>
        public int NewPoints;
        /// <summary>The discipline's tree, set by the host. Its learned nodes join the modifiers every tick.</summary>
        public SkillTree Tree;
        /// <summary>False for a save from before skill trees, so the host can back-pay its points once.</summary>
        public bool HasSkillSave = true;
        /// <summary>Home Tether position in voxels, if one is set.</summary>
        public int[] Tether;
        public bool Dead;
        /// <summary>Seconds left before a downed character dies (hardcore).</summary>
        public double Downed;
        public Mods Mods = new Mods();
        public List<ActiveBand> Bands = new List<ActiveBand>();
        public double SinceSpend = 99;
        public double SinceHurt = 99;

        readonly ItemRegistry _items;
        readonly Ruleset _rules;

        public CharacterState(ItemRegistry items, Ruleset rules)
        {
            _items = items;
            _rules = rules;
            Inv = new Inventory(items);
        }

        public Ruleset Rules { get { return _rules; } }

        /// <summary>A fresh castaway: start values, full vitals, the ruleset's kit and words.</summary>
        public void SetupNew()
        {
            Cond = Conditions.StartValues(_rules);
            Hp = _rules.Num("vitals", "hp", 100);
            Stamina = _rules.Num("vitals", "stamina", 100);
            Buffs.Clear();
            Inv = new Inventory(_items);
            foreach (var kv in _rules.StartingItems) Inv.Add(kv.Key, kv.Value);
            Lang = new WordKnowledge();
            Quests = new QuestLog();
            Skills.Clear();
            SkillPoints = 0;
            NewPoints = 0;
            HasSkillSave = true;
            foreach (var w in _rules.StartingWords) Learn(w);
            Dead = false;
            Downed = 0;
        }

        public double HpMax { get { return _rules.Num("vitals", "hp", 100) * Mods["hp_max_mult"]; } }
        public double StaminaMax { get { return _rules.Num("vitals", "stamina", 100) * Mods["stamina_max_mult"]; } }

        public string HeldKey { get { return Inv.KeyAt(Selected); } }
        public ToolDef HeldTool { get { return _items.Tools.Get(HeldKey); } }

        /// <summary>Flags granted by carried gear (a dust mask gives "masked").</summary>
        public void AddGearFlags(HashSet<string> flags)
        {
            for (int i = 0; i < Inventory.Size; i++)
            {
                var s = Inv[i];
                ItemDef def;
                if (!s.HasValue || !_items.TryGet(s.Value.Key, out def)) continue;
                string f = MiniJson.Str(def.Raw, "grants_flag");
                if (f.Length > 0) flags.Add(f);
            }
        }

        /// <summary>
        /// Advances the character by dt seconds under the given flags (day, night, exposed,
        /// sprinting...). Returns true when conditions brought health to zero.
        /// </summary>
        public bool Tick(HashSet<string> flags, double dt)
        {
            if (Dead) return false;
            SinceSpend += dt;
            SinceHurt += dt;
            var ended = new List<string>();
            var ids = new List<string>(Buffs.Keys);
            foreach (var id in ids)
            {
                Buffs[id] -= dt;
                if (Buffs[id] <= 0) ended.Add(id);
            }
            foreach (var id in ended) Buffs.Remove(id);
            AddGearFlags(flags);
            double debtBefore;
            bool hasDebt = Cond.TryGetValue("gravity_debt", out debtBefore);
            TickResult result = Conditions.Tick(Cond, _rules, flags, Buffs.Keys, dt);
            Mods = result.Mods;
            if (Tree != null) Tree.AddMods(this, Mods);
            // Skills change how fast debt is repaid: scale only the part that went down.
            if (hasDebt && Cond["gravity_debt"] < debtBefore)
                Cond["gravity_debt"] = Math.Max(0, debtBefore - (debtBefore - Cond["gravity_debt"]) * Mods["debt_repay_mult"]);
            Bands = result.Bands;
            if (flags.Contains("sprinting"))
            {
                Stamina = Math.Max(0, Stamina - _rules.Num("vitals", "sprint_cost", 8) * dt);
                SinceSpend = 0;
            }
            else if (SinceSpend >= _rules.Num("vitals", "stamina_regen_delay_s", 0.8))
                Stamina += _rules.Num("vitals", "stamina_regen", 24) * Mods["stamina_regen_mult"] * dt;
            Stamina = Math.Min(Stamina, StaminaMax);
            if (Downed > 0) return false;
            double regen = Mods["hp_regen"];
            if (SinceHurt >= _rules.Num("vitals", "hp_regen_delay_s", 6)) regen += _rules.Num("vitals", "hp_regen", 0.5);
            Hp = Math.Min(Hp + regen * dt, HpMax);
            Hp -= result.Damage;
            return Hp <= 0;
        }

        /// <summary>Spends stamina for an action. Any stamina left is enough, and the action can take you to zero.</summary>
        public bool Spend(double amount)
        {
            if (Stamina <= 0.5) return false;
            Stamina = Math.Max(0, Stamina - amount);
            SinceSpend = 0;
            return true;
        }

        /// <summary>Takes damage. Returns true if health reached zero.</summary>
        public bool Hurt(double amount)
        {
            if (Dead || amount <= 0) return false;
            Hp -= amount * Mods["damage_taken_mult"];
            SinceHurt = 0;
            return Hp <= 0;
        }

        /// <summary>Eats, drinks or applies an item. Returns false for items with no "use".</summary>
        public bool Use(string itemKey)
        {
            ItemDef def;
            object u;
            if (!_items.TryGet(itemKey, out def) || !def.Raw.TryGetValue("use", out u)) return false;
            var use = u as Dictionary<string, object>;
            if (use == null) return false;
            object c;
            if (use.TryGetValue("conditions", out c) && c is Dictionary<string, object>)
                foreach (var kv in (Dictionary<string, object>)c)
                    if (Cond.ContainsKey(kv.Key))
                        Cond[kv.Key] = Math.Max(0, Math.Min(100, Cond[kv.Key] + (double)kv.Value));
            string buff = MiniJson.Str(use, "buff");
            if (buff.Length > 0) AddBuff(buff);
            Hp = Math.Min(HpMax, Hp + MiniJson.Num(use, "heal"));
            return true;
        }

        public void AddBuff(string id)
        {
            BuffDef b;
            if (_rules.Buffs.TryGetValue(id, out b)) Buffs[id] = b.Duration;
        }

        /// <summary>Gravity debt: can this character take on `cost` more? The caller scales a spoken word's cost by Mods["debt_cost_mult"].</summary>
        public bool CanOwe(double cost)
        {
            double d;
            Cond.TryGetValue("gravity_debt", out d);
            return d + cost <= 100;
        }

        public void Owe(double cost)
        {
            if (!Cond.ContainsKey("gravity_debt")) return;
            Cond["gravity_debt"] = Math.Max(0, Math.Min(100, Cond["gravity_debt"] + cost));
        }

        /// <summary>Knows a word from now on.</summary>
        public void Learn(string word)
        {
            Lang.Teach(word);
            if (Words.Contains(word)) return;
            Words.Add(word);
            if (Tree != null) Award(Tree.Award("word"));
        }

        /// <summary>Earns skill points; the host tells the player at its next tick.</summary>
        public void Award(int points)
        {
            if (points <= 0) return;
            SkillPoints += points;
            NewPoints += points;
        }

        // ---- Saving. One "key value" pair per line, so a save is easy to read and diff.

        public string ToText()
        {
            var c = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append("guid ").Append(Guid).Append('\n');
            sb.Append("name ").Append(DisplayName.Replace('\n', ' ')).Append('\n');
            sb.Append("hp ").Append(Hp.ToString("R", c)).Append('\n');
            sb.Append("stamina ").Append(Stamina.ToString("R", c)).Append('\n');
            foreach (var kv in Cond) sb.Append("cond ").Append(kv.Key).Append(' ').Append(kv.Value.ToString("R", c)).Append('\n');
            foreach (var kv in Buffs) sb.Append("buff ").Append(kv.Key).Append(' ').Append(kv.Value.ToString("R", c)).Append('\n');
            sb.Append("selected ").Append(Selected).Append('\n');
            foreach (var w in Words) sb.Append("word ").Append(w).Append('\n');
            foreach (var kv in Quests.All) sb.Append("quest ").Append(kv.Key).Append(' ').Append(kv.Value).Append('\n');
            foreach (var kv in Lang.Places) foreach (var place in kv.Value) sb.Append("seen ").Append(kv.Key).Append(' ').Append(place).Append('\n');
            foreach (var combo in Combos) sb.Append("combo ").Append(combo).Append('\n');
            foreach (var skill in Skills) sb.Append("skill ").Append(skill).Append('\n');
            sb.Append("points ").Append(SkillPoints).Append('\n');
            if (Tether != null) sb.AppendFormat(c, "tether {0} {1} {2}\n", Tether[0], Tether[1], Tether[2]);
            foreach (var line in Inv.ToText().Split('\n'))
                if (line.Length > 0) sb.Append("slot ").Append(line).Append('\n');
            return sb.ToString();
        }

        /// <summary>Loads a save. Conditions or buffs the ruleset no longer has are dropped; new ones start fresh.</summary>
        public void FromText(string text)
        {
            var c = CultureInfo.InvariantCulture;
            Cond = Conditions.StartValues(_rules);
            Buffs.Clear();
            Words.Clear();
            Lang = new WordKnowledge();
            Quests = new QuestLog();
            Combos.Clear();
            Skills.Clear();
            SkillPoints = 0;
            NewPoints = 0;
            HasSkillSave = false;
            var seen = new List<string[]>();
            Tether = null;
            Hp = _rules.Num("vitals", "hp", 100);
            Stamina = _rules.Num("vitals", "stamina", 100);
            var slots = new StringBuilder();
            foreach (var raw in text.Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                int sp = line.IndexOf(' ');
                if (sp <= 0) continue;
                string key = line.Substring(0, sp), rest = line.Substring(sp + 1);
                string[] p = rest.Split(' ');
                double num;
                switch (key)
                {
                    case "guid": Guid = rest; break;
                    case "name": DisplayName = rest; break;
                    case "hp": if (double.TryParse(rest, NumberStyles.Float, c, out num)) Hp = num; break;
                    case "stamina": if (double.TryParse(rest, NumberStyles.Float, c, out num)) Stamina = num; break;
                    case "cond":
                        if (p.Length == 2 && Cond.ContainsKey(p[0]) && double.TryParse(p[1], NumberStyles.Float, c, out num))
                            Cond[p[0]] = Math.Max(0, Math.Min(100, num));
                        break;
                    case "buff":
                        if (p.Length == 2 && _rules.Buffs.ContainsKey(p[0]) && double.TryParse(p[1], NumberStyles.Float, c, out num))
                            Buffs[p[0]] = num;
                        break;
                    case "selected":
                        int sel;
                        if (int.TryParse(rest, out sel)) Selected = Math.Max(0, Math.Min(Inventory.Hotbar - 1, sel));
                        break;
                    case "word": if (!Words.Contains(rest)) Words.Add(rest); break;
                    case "seen": if (p.Length == 2) seen.Add(p); break;
                    case "quest":
                        int qs;
                        if (p.Length == 2 && int.TryParse(p[1], out qs)) Quests.Set(p[0], qs);
                        break;
                    case "combo": Combos.Add(rest); break;
                    case "skill": Skills.Add(rest); break;
                    case "points":
                        int pts;
                        if (int.TryParse(rest, out pts)) SkillPoints = Math.Max(0, pts);
                        HasSkillSave = true;
                        break;
                    case "tether":
                        int x, y, z;
                        if (p.Length == 3 && int.TryParse(p[0], out x) && int.TryParse(p[1], out y) && int.TryParse(p[2], out z))
                            Tether = new[] { x, y, z };
                        break;
                    case "slot": slots.Append(rest).Append('\n'); break;
                }
            }
            foreach (var sight in seen) Lang.See(sight[0], sight[1]);
            foreach (var w in Words) Lang.Teach(w);
            Inv = new Inventory(_items);
            Inv.FromText(slots.ToString());
            if (Hp <= 0) Hp = _rules.Num("vitals", "hp", 100) * 0.5;
            Dead = false;
            Downed = 0;
        }
    }
}
