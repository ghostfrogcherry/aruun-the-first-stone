using System;
using System.Collections.Generic;

namespace Aruun.Core.Items
{
    /// <summary>One filled bag slot. Durability is -1 for items that never wear.</summary>
    public struct ItemStack
    {
        public string Key;
        public int Count;
        public int Durability;

        public ItemStack(string key, int count, int durability = -1)
        {
            Key = key;
            Count = count;
            Durability = durability;
        }
    }

    /// <summary>
    /// A character's bag: fixed slots, and the first Hotbar of them are the hotbar.
    /// Pure logic. The host owns the real one; clients hold a copy for display.
    /// </summary>
    public sealed class Inventory
    {
        public const int Size = 32;
        public const int Hotbar = 8;

        readonly ItemRegistry _items;
        ItemStack?[] _slots = new ItemStack?[Size];

        public Inventory(ItemRegistry items) { _items = items; }

        public ItemStack? this[int i] { get { return i >= 0 && i < Size ? _slots[i] : null; } }

        public string KeyAt(int i) { var s = this[i]; return s.HasValue ? s.Value.Key : ""; }

        /// <summary>Adds items, filling stacks first. Returns how many did not fit.</summary>
        public int Add(string key, int n, int durability = -1)
        {
            ItemDef def;
            if (!_items.TryGet(key, out def) || n <= 0) return n;
            if (def.Stack > 1)
            {
                for (int i = 0; i < Size && n > 0; i++)
                {
                    var s = _slots[i];
                    if (!s.HasValue || s.Value.Key != key || s.Value.Count >= def.Stack) continue;
                    int put = Math.Min(n, def.Stack - s.Value.Count);
                    _slots[i] = new ItemStack(key, s.Value.Count + put);
                    n -= put;
                }
            }
            for (int i = 0; i < Size && n > 0; i++)
            {
                if (_slots[i].HasValue) continue;
                int put = Math.Min(n, def.Stack);
                _slots[i] = new ItemStack(key, put, def.Wears ? (durability >= 0 ? durability : def.Durability) : -1);
                n -= put;
            }
            return n;
        }

        public int Count(string key)
        {
            int total = 0;
            foreach (var s in _slots)
                if (s.HasValue && s.Value.Key == key) total += s.Value.Count;
            return total;
        }

        public bool HasAll(IEnumerable<KeyValuePair<string, int>> needs)
        {
            foreach (var kv in needs)
                if (Count(kv.Key) < kv.Value) return false;
            return true;
        }

        /// <summary>Removes exactly n of key, or nothing. The back of the bag empties before the hotbar.</summary>
        public bool Remove(string key, int n)
        {
            if (Count(key) < n) return false;
            for (int i = Size - 1; i >= 0 && n > 0; i--)
            {
                var s = _slots[i];
                if (!s.HasValue || s.Value.Key != key) continue;
                int take = Math.Min(n, s.Value.Count);
                n -= take;
                int left = s.Value.Count - take;
                _slots[i] = left == 0 ? (ItemStack?)null : new ItemStack(key, left, s.Value.Durability);
            }
            return true;
        }

        /// <summary>Takes up to n from one slot (to drop or use). Returns what was taken, or null.</summary>
        public ItemStack? TakeSlot(int i, int n)
        {
            var s = this[i];
            if (!s.HasValue || n <= 0) return null;
            int take = Math.Min(n, s.Value.Count);
            int left = s.Value.Count - take;
            _slots[i] = left == 0 ? (ItemStack?)null : new ItemStack(s.Value.Key, left, s.Value.Durability);
            return new ItemStack(s.Value.Key, take, s.Value.Durability);
        }

        /// <summary>Moves slot a onto slot b: merges equal stacks of items that do not wear, otherwise swaps.</summary>
        public bool Move(int a, int b)
        {
            if (a == b || a < 0 || b < 0 || a >= Size || b >= Size || !_slots[a].HasValue) return false;
            var from = _slots[a].Value;
            var to = _slots[b];
            if (to.HasValue && to.Value.Key == from.Key && from.Durability < 0)
            {
                int stack = _items.Get(from.Key).Stack;
                int put = Math.Min(from.Count, stack - to.Value.Count);
                if (put <= 0) return false;
                _slots[b] = new ItemStack(from.Key, to.Value.Count + put);
                _slots[a] = from.Count == put ? (ItemStack?)null : new ItemStack(from.Key, from.Count - put);
                return true;
            }
            _slots[a] = to;
            _slots[b] = from;
            return true;
        }

        /// <summary>Wears the tool in slot i. Returns true if it broke and is gone.</summary>
        public bool Wear(int i, int amount = 1)
        {
            var s = this[i];
            if (!s.HasValue || s.Value.Durability < 0) return false;
            int d = s.Value.Durability - amount;
            _slots[i] = d <= 0 ? (ItemStack?)null : new ItemStack(s.Value.Key, s.Value.Count, d);
            return d <= 0;
        }

        public bool IsEmpty
        {
            get
            {
                foreach (var s in _slots) if (s.HasValue) return false;
                return true;
            }
        }

        /// <summary>Empties the bag (on death) and returns what was in it.</summary>
        public List<ItemStack> Clear()
        {
            var output = new List<ItemStack>();
            for (int i = 0; i < Size; i++)
            {
                if (_slots[i].HasValue) output.Add(_slots[i].Value);
                _slots[i] = null;
            }
            return output;
        }

        public Inventory Copy()
        {
            var c = new Inventory(_items);
            Array.Copy(_slots, c._slots, Size);
            return c;
        }

        /// <summary>Takes on another bag's contents (after a trial on a copy succeeds).</summary>
        public void CopyFrom(Inventory other) { Array.Copy(other._slots, _slots, Size); }

        /// <summary>
        /// One line per filled slot, "index key count [durability]". The character save and the
        /// network copy use this. Unknown items (removed from content) are dropped on load.
        /// </summary>
        public string ToText()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < Size; i++)
            {
                if (!_slots[i].HasValue) continue;
                var s = _slots[i].Value;
                sb.Append(i).Append(' ').Append(s.Key).Append(' ').Append(s.Count);
                if (s.Durability >= 0) sb.Append(' ').Append(s.Durability);
                sb.Append('\n');
            }
            return sb.ToString();
        }

        public void FromText(string text)
        {
            _slots = new ItemStack?[Size];
            foreach (var raw in text.Split('\n'))
            {
                var p = raw.Trim().Split(' ');
                int i, n, d = -1;
                if (p.Length < 3 || !int.TryParse(p[0], out i) || !int.TryParse(p[2], out n)) continue;
                if (p.Length > 3 && !int.TryParse(p[3], out d)) continue;
                ItemDef def;
                if (i < 0 || i >= Size || n <= 0 || !_items.TryGet(p[1], out def)) continue;
                _slots[i] = new ItemStack(def.Key, Math.Min(n, def.Stack), def.Wears ? (d >= 0 ? d : def.Durability) : -1);
            }
        }
    }
}
