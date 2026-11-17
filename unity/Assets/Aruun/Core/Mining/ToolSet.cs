using System;
using System.Collections.Generic;
using Aruun.Core.Content;

namespace Aruun.Core.Mining
{
    /// <summary>How an item digs. Read from content/items/items.json ("unarmed" and items with dig_power).</summary>
    public sealed class ToolDef
    {
        public string Key = "";
        public string Name = "";
        public ToolClass Class = ToolClass.Hand;
        /// <summary>The hardest block this tool can break.</summary>
        public int Power = 1;
        /// <summary>Block health worn off per second when the tool fits the block.</summary>
        public double Speed = 2;
    }

    /// <summary>Every item that can dig, by key, plus bare hands.</summary>
    public sealed class ToolSet
    {
        public const string UnarmedKey = "unarmed";
        readonly Dictionary<string, ToolDef> _byKey = new Dictionary<string, ToolDef>();

        public ToolDef Unarmed { get { return _byKey[UnarmedKey]; } }

        /// <summary>The tool for an item key. Items that are not tools (and empty hands) dig like bare hands.</summary>
        public ToolDef Get(string itemKey)
        {
            ToolDef t;
            return itemKey != null && _byKey.TryGetValue(itemKey, out t) ? t : Unarmed;
        }

        public bool Has(string itemKey) { return _byKey.ContainsKey(itemKey); }
        public IEnumerable<ToolDef> All { get { return _byKey.Values; } }

        public static ToolSet FromJson(string itemsJson)
        {
            var root = MiniJson.ParseObject(itemsJson);
            var set = new ToolSet();
            object unarmed;
            if (!root.TryGetValue("unarmed", out unarmed) || !(unarmed is Dictionary<string, object>))
                throw new FormatException("items.json: needs an 'unarmed' object");
            var hands = Read((Dictionary<string, object>)unarmed, UnarmedKey);
            hands.Name = "Bare hands";
            set._byKey[UnarmedKey] = hands;
            foreach (var item in MiniJson.List(root, "items"))
            {
                var o = item as Dictionary<string, object>;
                if (o == null || !o.ContainsKey("dig_power")) continue;
                var t = Read(o, MiniJson.Str(o, "key"));
                set._byKey[t.Key] = t;
            }
            return set;
        }

        static ToolDef Read(Dictionary<string, object> o, string key)
        {
            var t = new ToolDef
            {
                Key = key,
                Name = MiniJson.Str(o, "name", key),
                Power = MiniJson.Int(o, "dig_power", 1),
                Speed = MiniJson.Num(o, "dig_speed", 2),
                Class = ToolClasses.Parse(MiniJson.Str(o, "tool_class"), ToolClass.Hand),
            };
            if (t.Class == ToolClass.Any) throw new FormatException("items.json: " + key + " cannot have tool_class 'any'");
            if (t.Speed <= 0) throw new FormatException("items.json: " + key + " needs a dig_speed above 0");
            return t;
        }
    }
}
