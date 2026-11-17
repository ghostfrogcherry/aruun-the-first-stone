using System.Collections.Generic;
using Aruun.Core.Items;
using Aruun.Core.Play;
using Aruun.Core.Survival;
using UnityEngine;

namespace Aruun.Game
{
    /// <summary>
    /// The survival HUD and the bag, drawn with Unity's immediate GUI so it needs no scene or
    /// assets: health and stamina, the Conditions you are in, the time of day, the hotbar
    /// (slots 1-9), feedback lines, dig progress, and with Tab the whole bag and crafting.
    /// A real styled UI replaces this once the look is settled.
    /// </summary>
    public sealed class PlayHud : MonoBehaviour
    {
        public static bool BagOpen { get; private set; }
        /// <summary>Set by the player each frame: how worn the block being dug is (0 when not digging).</summary>
        public static float DigFraction;
        /// <summary>Set by the player: what the crosshair is on ("Flint nodule", "Redstrider"), or "".</summary>
        public static string LookingAt = "";

        int _picked = -1;
        Vector2 _recipeScroll;
        float _hurtFlash;
        GUIStyle _small, _centre, _title;

        public static void SetBag(bool open)
        {
            BagOpen = open;
            Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = open;
        }

        void Update()
        {
            PlaySession s = PlaySession.Instance;
            if (s == null) return;
            foreach (HostEvent e in s.MyEvents) if (e.Kind == "hurt") _hurtFlash = 0.35f;
            _hurtFlash = Mathf.Max(0, _hurtFlash - Time.deltaTime);
        }

        void Styles()
        {
            if (_small != null) return;
            _small = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            _centre = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 12 };
            _title = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
        }

        static void Box(Rect r, Color c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        static void Bar(Rect r, float frac, Color fill, string label)
        {
            Box(r, new Color(0, 0, 0, 0.55f));
            Box(new Rect(r.x + 2, r.y + 2, (r.width - 4) * Mathf.Clamp01(frac), r.height - 4), fill);
            GUI.Label(new Rect(r.x + 6, r.y, r.width, r.height), label);
        }

        void OnGUI()
        {
            PlaySession s = PlaySession.Instance;
            if (s == null || s.Me == null) return;
            Styles();
            PlayerRec me = s.Me;
            CharacterState c = me.C;
            Host h = s.Host;
            float w = Screen.width, ht = Screen.height;

            if (_hurtFlash > 0) Box(new Rect(0, 0, w, ht), new Color(0.7f, 0f, 0f, _hurtFlash * 0.5f));

            // Vitals, bottom left.
            float x = 16, y = ht - 70;
            Bar(new Rect(x, y, 240, 22), (float)(c.Hp / c.HpMax), new Color(0.75f, 0.2f, 0.2f), "Health " + Mathf.CeilToInt((float)c.Hp));
            Bar(new Rect(x, y + 26, 240, 18), (float)(c.Stamina / c.StaminaMax), new Color(0.85f, 0.7f, 0.25f), "Stamina");
            if (me.Guard) GUI.Label(new Rect(x + 250, y + 24, 120, 22), "Guarding");

            // Conditions you are in, above the bars.
            float cx = x;
            foreach (ActiveBand b in c.Bands)
            {
                float bw = 12 + b.Name.Length * 7;
                Box(new Rect(cx, y - 28, bw, 22), b.Good ? new Color(0.15f, 0.45f, 0.25f, 0.8f) : new Color(0.55f, 0.2f, 0.15f, 0.85f));
                GUI.Label(new Rect(cx + 6, y - 27, bw, 22), b.Name);
                cx += bw + 4;
            }

            // Feedback lines.
            float ly = y - 56;
            for (int i = s.Log.Count - 1; i >= 0 && ly > ht * 0.45f; i--)
            {
                float age = Time.unscaledTime - s.Log[i].Value;
                if (age > 8) continue;
                GUI.color = new Color(1, 1, 1, Mathf.Clamp01(8 - age));
                GUI.Label(new Rect(x, ly, 560, 22), s.Log[i].Key);
                ly -= 20;
            }
            GUI.color = Color.white;

            // Time of day and the mode, top right.
            double tod = h.TimeOfDay;
            int hour = (int)(tod * 24), minute = (int)((tod * 24 - hour) * 60);
            string clock = "Day " + h.Day + "   " + hour.ToString("00") + ":" + minute.ToString("00") + (h.IsNight ? "   night" : "");
            string where = me.Flags.Contains("sheltered") ? "sheltered" : "in the open";
            if (me.Flags.Contains("near_heat")) where += ", by a fire";
            Box(new Rect(w - 266, 10, 256, 44), new Color(0, 0, 0, 0.45f));
            GUI.Label(new Rect(w - 258, 12, 250, 22), clock);
            GUI.Label(new Rect(w - 258, 30, 250, 22), (s.SurvivalOnly ? "Survival   " : "") + where);

            // Hotbar, bottom centre.
            float slot = 54, bx = w / 2 - slot * 4.5f, by = ht - slot - 12;
            for (int i = 0; i < 9; i++) DrawSlot(new Rect(bx + i * slot, by, slot - 4, slot - 4), h.Items, c.Inv, i, i == c.Selected, (i + 1).ToString());
            string held = c.HeldKey.Length > 0 && h.Items.Has(c.HeldKey) ? h.Items.Get(c.HeldKey).Name : "Bare hands";
            GUI.Label(new Rect(w / 2 - 150, by - 22, 300, 22), held, _centre);

            // Crosshair: what you look at and how far a dig has got.
            if (!BagOpen)
            {
                if (LookingAt.Length > 0) GUI.Label(new Rect(w / 2 - 150, ht / 2 + 14, 300, 20), LookingAt, _centre);
                if (DigFraction > 0) Bar(new Rect(w / 2 - 40, ht / 2 + 34, 80, 10), DigFraction, new Color(0.9f, 0.9f, 0.9f), "");
            }

            if (c.Dead)
            {
                Box(new Rect(0, 0, w, ht), new Color(0, 0, 0, 0.55f));
                GUI.Label(new Rect(0, ht / 2 - 20, w, 40), "You fall. You will wake shortly.", new GUIStyle(_title) { alignment = TextAnchor.MiddleCenter });
            }

            if (BagOpen) DrawBag(s, me);
        }

        void DrawSlot(Rect r, ItemRegistry items, Inventory inv, int i, bool selected, string label)
        {
            Box(r, selected ? new Color(0.95f, 0.85f, 0.45f, 0.9f) : new Color(0, 0, 0, 0.5f));
            Rect inner = new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4);
            Box(inner, new Color(0.12f, 0.12f, 0.14f, 0.85f));
            var st = inv[i];
            if (st.HasValue && items.Has(st.Value.Key))
            {
                ItemDef def = items.Get(st.Value.Key);
                Color col;
                if (!ColorUtility.TryParseHtmlString(def.Color, out col)) col = Color.gray;
                Box(new Rect(inner.x + 6, inner.y + 6, inner.width - 12, inner.height - 22), col);
                GUI.Label(new Rect(inner.x, inner.y + inner.height - 18, inner.width, 18), Short(def.Name), _centre);
                if (st.Value.Count > 1) GUI.Label(new Rect(inner.x + 2, inner.y, inner.width, 18), st.Value.Count.ToString(), _small);
            }
            if (label.Length > 0) GUI.Label(new Rect(r.x + r.width - 12, r.y, 12, 16), label, _small);
        }

        static string Short(string name) { return name.Length <= 9 ? name : name.Substring(0, 8) + "."; }

        void DrawBag(PlaySession s, PlayerRec me)
        {
            Host h = s.Host;
            Inventory inv = me.C.Inv;
            float w = Screen.width, ht = Screen.height;
            float slot = 58, cols = 8;
            float bagW = slot * cols + 24, bagH = slot * 4 + 70;
            float bx = w / 2 - (bagW + 380) / 2, by = ht / 2 - 220;
            Box(new Rect(bx, by, bagW, bagH), new Color(0.05f, 0.05f, 0.07f, 0.9f));
            GUI.Label(new Rect(bx + 12, by + 8, 300, 24), "Bag", _title);
            GUI.Label(new Rect(bx + 70, by + 12, 400, 22), "Click an item, then another slot to move it. The top row is your hotbar.", _small);
            Event e = Event.current;
            for (int i = 0; i < Inventory.Size; i++)
            {
                int row = i / 8, col = i % 8;
                var r = new Rect(bx + 12 + col * slot, by + 40 + row * slot, slot - 4, slot - 4);
                DrawSlot(r, h.Items, inv, i, i == _picked || (i == me.C.Selected && _picked < 0), row == 0 ? (i + 1).ToString() : "");
                if (e.type == EventType.MouseDown && r.Contains(e.mousePosition))
                {
                    if (_picked < 0) { if (inv[i].HasValue) _picked = i; }
                    else { inv.Move(_picked, i); _picked = -1; }
                    e.Use();
                }
                if (r.Contains(e.mousePosition) && inv[i].HasValue && h.Items.Has(inv[i].Value.Key))
                    GUI.Label(new Rect(bx + 12, by + bagH - 26, bagW, 22), Describe(h.Items.Get(inv[i].Value.Key)), _small);
            }

            // Crafting, to the right of the bag.
            float rx = bx + bagW + 8, rw = 372;
            Box(new Rect(rx, by, rw, 440), new Color(0.05f, 0.05f, 0.07f, 0.9f));
            GUI.Label(new Rect(rx + 12, by + 8, 300, 24), "Crafting", _title);
            var stations = h.StationsNear(me.Pos);
            GUI.Label(new Rect(rx + 110, by + 12, 250, 22), stations.Count > 0 ? "Near: " + string.Join(", ", stations) : "Some recipes need a fire or a Tether", _small);
            _recipeScroll = GUI.BeginScrollView(new Rect(rx + 8, by + 40, rw - 16, 392), _recipeScroll, new Rect(0, 0, rw - 36, h.Items.Recipes.Count * 48));
            float ry = 0;
            foreach (Recipe r in h.Items.Recipes)
            {
                string why = h.CanCraft(PlaySession.LocalId, r.Id);
                string outName = h.Items.Has(r.Out) ? h.Items.Get(r.Out).Name : r.Out;
                var parts = new List<string>();
                foreach (var kv in r.In)
                {
                    string n = h.Items.Has(kv.Key) ? h.Items.Get(kv.Key).Name : kv.Key;
                    parts.Add(kv.Value + " " + n + " (" + inv.Count(kv.Key) + ")");
                }
                GUI.enabled = why.Length == 0;
                if (GUI.Button(new Rect(0, ry, 90, 40), "Craft")) h.Craft(PlaySession.LocalId, r.Id);
                GUI.enabled = true;
                GUI.Label(new Rect(98, ry, rw - 130, 20), (r.Count > 1 ? r.Count + " " : "") + outName + (why.Length > 0 ? "   (" + why + ")" : ""));
                GUI.Label(new Rect(98, ry + 18, rw - 130, 22), string.Join(", ", parts), _small);
                ry += 48;
            }
            GUI.EndScrollView();
        }

        static string Describe(ItemDef def)
        {
            string kind = def.Kind == "food" ? "Food: right click to eat" :
                          def.Kind == "block" ? "Block: right click to place" :
                          def.Kind == "seed" ? "Seed: right click on soil to plant" :
                          def.Kind == "structure" ? "Right click on the ground to set it down" :
                          def.Kind == "tool" ? "Tool" : def.Kind == "weapon" ? "Weapon" : "Material";
            return def.Name + ". " + kind + ".";
        }
    }
}
