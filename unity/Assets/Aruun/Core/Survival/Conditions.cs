using System;
using System.Collections.Generic;

namespace Aruun.Core.Survival
{
    /// <summary>Combined modifiers. Keys ending in _mult multiply (default 1); all others add (default 0).</summary>
    public sealed class Mods
    {
        readonly Dictionary<string, double> _values = new Dictionary<string, double>();

        public static bool IsMult(string key) { return key.EndsWith("_mult", StringComparison.Ordinal); }

        public double this[string key]
        {
            get
            {
                double v;
                return _values.TryGetValue(key, out v) ? v : (IsMult(key) ? 1.0 : 0.0);
            }
        }

        public void Add(Dictionary<string, double> mods)
        {
            foreach (var kv in mods)
            {
                double now = this[kv.Key];
                _values[kv.Key] = IsMult(kv.Key) ? now * kv.Value : now + kv.Value;
            }
        }

        public IEnumerable<string> Keys { get { return _values.Keys; } }
    }

    /// <summary>A band the character is in, for the HUD.</summary>
    public struct ActiveBand
    {
        public string Condition;
        public string Name;
        public bool Good;
    }

    public sealed class TickResult
    {
        public Mods Mods = new Mods();
        /// <summary>Health lost this tick.</summary>
        public double Damage;
        public List<ActiveBand> Bands = new List<ActiveBand>();
    }

    /// <summary>
    /// The Conditions system: hunger, thirst, cold, thin air, dust, fatigue and gravity debt are
    /// all one thing, a 0-100 severity that drifts with what is happening to you (flags) and
    /// applies effects while it sits in a band. Rulesets hold every number. Pure logic.
    /// </summary>
    public static class Conditions
    {
        public static Dictionary<string, double> StartValues(Ruleset rules)
        {
            var output = new Dictionary<string, double>();
            foreach (var c in rules.Conditions) output[c.Id] = c.Start;
            return output;
        }

        /// <summary>Per-minute drift of one condition under the given flags.</summary>
        public static double RateOf(ConditionDef def, ICollection<string> flags)
        {
            double total = 0;
            foreach (var r in def.Rates) if (r.On(flags)) total += r.PerMin;
            return total;
        }

        /// <summary>
        /// Advances `values` by dt seconds. Active buffs add their flags to `flags` (so pass a
        /// set you own), their mods and their extra drift.
        /// </summary>
        public static TickResult Tick(Dictionary<string, double> values, Ruleset rules, HashSet<string> flags, IEnumerable<string> buffs, double dt)
        {
            var result = new TickResult();
            var buffRates = new Dictionary<string, double>();
            foreach (var id in buffs)
            {
                BuffDef b;
                if (!rules.Buffs.TryGetValue(id, out b)) continue;
                foreach (var f in b.Flags) flags.Add(f);
                result.Mods.Add(b.Mods);
                foreach (var kv in b.Rates)
                {
                    double was;
                    buffRates.TryGetValue(kv.Key, out was);
                    buffRates[kv.Key] = was + kv.Value;
                }
            }
            foreach (var def in rules.Conditions)
            {
                double extra, now;
                buffRates.TryGetValue(def.Id, out extra);
                if (!values.TryGetValue(def.Id, out now)) now = def.Start;
                now = Clamp(now + (RateOf(def, flags) + extra) * dt / 60.0);
                values[def.Id] = now;
                foreach (var band in def.Bands)
                {
                    if (now < band.From || now >= band.To) continue;
                    result.Mods.Add(band.Mods);
                    result.Damage += band.DamagePerMin * dt / 60.0;
                    result.Bands.Add(new ActiveBand { Condition = def.Id, Name = band.Name, Good = band.Good });
                }
            }
            return result;
        }

        static double Clamp(double v) { return v < 0 ? 0 : v > 100 ? 100 : v; }
    }

    /// <summary>Time of day. 0 is midnight, 0.5 noon; worlds start in the morning.</summary>
    public static class DayClock
    {
        public static double TimeOfDay(double worldTime, double dayLength)
        {
            double t = worldTime / dayLength + 0.3;
            return t - Math.Floor(t);
        }

        public static bool IsNight(double tod) { return tod < 0.23 || tod > 0.79; }

        public static int DayNumber(double worldTime, double dayLength)
        {
            return (int)Math.Floor(worldTime / dayLength + 0.3) + 1;
        }
    }
}
