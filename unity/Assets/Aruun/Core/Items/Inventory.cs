using System;
using System.Collections.Generic;

namespace Aruun.Core.Items
{
    /// <summary>
    /// One filled bag slot. Durability is -1 for items that never wear. Words is the Avarra
    /// inscription cut into this one item: word ids joined by '+', in the order they were cut,
    /// or null. Only single items (stack 1) carry words.
    /// </summary>
    public struct ItemStack
    {
        public string Key;
        public int Count;
        public int Durability;
        public string Words;

        public ItemStack(string key, int count, int durability = -1, string words = null)
        {
            Key = key;
            Count = count;
            Durability = durability;
            Words = string.IsNullOrEmpty(words) ? null : words;
        }

        /// <summary>The inscribed word ids, in order. Empty when the item carries none.</summary>
        public string[] WordList { get { return string.IsNullOrEmpty(Words) ? new string[0] : Words.Split('+'); } }

        public bool Inscribed { get { return !string.IsNullOrEmpty(Words); } }
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
        public int Add(string key, int n, int durability = -1, string words = null)
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
                _slots[i] = new ItemStack(key, put, def.Wears ? (durability >= 0 ? durability : def.Durability) : -1, def.Stack == 1 ? words : null);
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
                _slots[i] = left == 0 ? (ItemStack?)null : new ItemStack(key, left, s.Value.Durability, s.Value.Words);
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
            _slots[i] = left == 0 ? (ItemStack?)null : new ItemStack(s.Value.Key, left, s.Value.Durability, s.Value.Words);
            return new ItemStack(s.Value.Key, take, s.Value.Durability, s.Value.Words);
        }

        /// <summary>Moves slot a onto slot b: merges equal stacks of items that do not wear, otherwise swaps.</summary>
        public bool Move(int a, int b)
        {
            if (a == b || a < 0 || b < 0 || a >= Size || b >= Size || !_slots[a].HasValue) return false;
            var from = _slots[a].Value;
            var to = _slots[b];
            if (to.HasValue && to.Value.Key == from.Key && from.Durability < 0 && !from.Inscribed && !to.Value.Inscribed)
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
            _slots[i] = d <= 0 ? (ItemStack?)null : new ItemStack(s.Value.Key, s.Value.Count, d, s.Value.Words);
            return d <= 0;
        }

        /// <summary>Gives back up to `amount` durability to the item in slot i, never past new. Returns how much it mended.</summary>
        public int Mend(int i, int amount)
        {
            var s = this[i];
            ItemDef def;
            if (!s.HasValue || s.Value.Durability < 0 || amount <= 0 || !_items.TryGet(s.Value.Key, out def)) return 0;
            int d = Math.Min(def.Durability, s.Value.Durability + amount);
            _slots[i] = new ItemStack(s.Value.Key, s.Value.Count, d, s.Value.Words);
            return d - s.Value.Durability;
        }

        /// <summary>Puts this stack in slot i, whatever was there. Used when an item becomes another (tuning).</summary>
        public void Replace(int i, ItemStack? stack)
        {
            if (i < 0 || i >= Size) return;
            _slots[i] = stack.HasValue && stack.Value.Count > 0 ? stack : null;
        }

        /// <summary>Sets the words cut into the item in slot i (null or empty clears them). False unless it holds an item that never stacks.</summary>
        public bool SetWords(int i, string words)
        {
            var s = this[i];
            ItemDef def;
            if (!s.HasValue || s.Value.Count != 1 || !_items.TryGet(s.Value.Key, out def) || def.Stack != 1) return false;
            _slots[i] = new ItemStack(s.Value.Key, 1, s.Value.Durability, words);
            return true;
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
        /// One line per filled slot, "index key count [durability] [@word+word]". The character
        /// save and the network copy use this. Unknown items (removed from content) are dropped on
        /// load; words are kept as written (the host ignores ids it no longer knows).
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
                if (s.Inscribed) sb.Append(" @").Append(s.Words);
                sb.Append('\n');
            }
            return sb.ToString();
        }

        public void FromText(string text)
        {
            _slots = new ItemStack?[Size];
            foreach (var raw in text.Split('\n'))
            {
                var p = new List<string>(raw.Trim().Split(' '));
                string words = null;
                if (p.Count > 3 && p[p.Count - 1].StartsWith("@", StringComparison.Ordinal))
                {
                    words = p[p.Count - 1].Substring(1);
                    p.RemoveAt(p.Count - 1);
                }
                int i, n, d = -1;
                if (p.Count < 3 || !int.TryParse(p[0], out i) || !int.TryParse(p[2], out n)) continue;
                if (p.Count > 3 && !int.TryParse(p[3], out d)) continue;
                ItemDef def;
                if (i < 0 || i >= Size || n <= 0 || !_items.TryGet(p[1], out def)) continue;
                _slots[i] = new ItemStack(def.Key, Math.Min(n, def.Stack), def.Wears ? (d >= 0 ? d : def.Durability) : -1, def.Stack == 1 ? words : null);
            }
        }
    }
}
