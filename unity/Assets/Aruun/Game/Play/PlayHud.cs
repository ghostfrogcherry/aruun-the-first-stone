using System.Collections.Generic;
using Aruun.Core.Combat;
using Aruun.Core.Items;
using Aruun.Core.Language;
using Aruun.Core.People;
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

        /// <summary>The quest journal (L) is open.</summary>
        public static bool JournalOpen;

        /// <summary>The speak panel (X) is open and number keys pick words.</summary>
        public static bool Speaking { get; private set; }
        /// <summary>The words picked so far, in order.</summary>
        public static readonly List<string> Phrase = new List<string>();
        /// <summary>The last phrase spoken; the middle mouse button says it again.</summary>
        public static readonly List<string> LastPhrase = new List<string>();

        public static void OpenSpeak() { Speaking = true; Phrase.Clear(); }
        public static void CloseSpeak() { Speaking = false; Phrase.Clear(); }
        public static void DropWord() { if (Phrase.Count > 0) Phrase.RemoveAt(Phrase.Count - 1); }

        /// <summary>The words this character has at least seen, in lexicon order: what the number keys pick from.</summary>
        public static List<string> WordsFor(Host h, PlayerRec me)
        {
            var list = new List<string>();
            if (h.Lexicon == null) return list;
            foreach (WordDef w in h.Lexicon.Words)
                if (me.C.Lang.Of(w.Id) >= Familiarity.Seen) list.Add(w.Id);
            return list;
        }

        public static void PickWord(int index, Host h, PlayerRec me)
        {
            List<string> words = WordsFor(h, me);
            if (index < 0 || index >= words.Count || Phrase.Count >= 4) return;
            Phrase.Add(words[index]);
        }

        int _picked = -1;
        Vector2 _recipeScroll;
        float _hurtFlash;
        GUIStyle _small, _centre, _title, _tiny, _tinyCentre, _bold;

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
            _tinyCentre = new GUIStyle(GUI.skin.label) { fontSize = 10, alignment = TextAnchor.MiddleCenter };
            _bold = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
            _tiny = new GUIStyle(GUI.skin.label) { fontSize = 10, alignment = TextAnchor.LowerCenter, clipping = TextClipping.Overflow, padding = new RectOffset(0, 0, 0, 1) };
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

            DrawObjective(h, me, w);
            double debt;
            if (c.Cond.TryGetValue("gravity_debt", out debt) && debt > 0.5)
                Bar(new Rect(x, y - 2 - 14, 240, 12), (float)(debt / 100), new Color(0.35f, 0.75f, 0.75f), "");
            if (Speaking) DrawSpeak(h, me, w, by);
            DrawNames(h, me);
            if (me.Talk != null) DrawTalk(me.Talk, w, by);
            if (JournalOpen) DrawJournal(h, me, w, ht);

            if (c.Dead)
            {
                Box(new Rect(0, 0, w, ht), new Color(0, 0, 0, 0.55f));
                GUI.Label(new Rect(0, ht / 2 - 20, w, 40), "You fall. You will wake shortly.", new GUIStyle(_title) { alignment = TextAnchor.MiddleCenter });
            }

            if (BagOpen) DrawBag(s, me);
        }

        /// <summary>Names over the heads of people nearby.</summary>
        void DrawNames(Host h, PlayerRec me)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            foreach (PersonRec r in h.People)
            {
                if (Vec3.Distance(r.Pos, me.Pos) > 14) continue;
                Vector3 sp = cam.WorldToScreenPoint(PlaySession.ToUnity(r.Pos) + Vector3.up * 2.5f);
                if (sp.z <= 0) continue;
                float y = Screen.height - sp.y;
                GUI.Label(new Rect(sp.x - 100, y - 20, 200, 20), r.Def.ShortName, _centre);
                GUI.color = new Color(1, 1, 1, 0.7f);
                GUI.Label(new Rect(sp.x - 100, y - 4, 200, 18), r.Def.Role, _tinyCentre);
                GUI.color = Color.white;
            }
        }

        /// <summary>The conversation box: what they say, and numbered answers.</summary>
        void DrawTalk(Conversation c, float w, float hotbarY)
        {
            float pw = Mathf.Min(620, w - 40), lineH = 22;
            float textH = _small.CalcHeight(new GUIContent(c.Say), pw - 24) + 8;
            float ph = 40 + textH + c.Choices.Count * lineH + 8;
            float px = w / 2 - pw / 2, py = hotbarY - 30 - ph;
            Box(new Rect(px, py, pw, ph), new Color(0.06f, 0.05f, 0.03f, 0.88f));
            GUI.Label(new Rect(px + 12, py + 6, pw - 24, 24), c.Name, _title);
            GUI.Label(new Rect(px + 12, py + 32, pw - 24, textH), c.Say, _small);
            float ly = py + 36 + textH;
            for (int i = 0; i < c.Choices.Count && i < 9; i++)
            {
                GUI.Label(new Rect(px + 20, ly, pw - 32, lineH), (i + 1) + "   " + c.Choices[i], _small);
                ly += lineH;
            }
        }

        /// <summary>The quest journal: every quest you have, where you are in it, and what is done.</summary>
        void DrawJournal(Host h, PlayerRec me, float w, float ht)
        {
            float pw = 460, px = w / 2 - pw / 2, py = 60, ph = ht - 200;
            Box(new Rect(px, py, pw, ph), new Color(0.05f, 0.04f, 0.03f, 0.9f));
            GUI.Label(new Rect(px + 14, py + 8, pw - 28, 24), "Journal   (L closes)", _title);
            float y = py + 40;
            bool any = false;
            foreach (bool done in new[] { false, true })
                foreach (QuestDef q in h.Quests.Values)
                {
                    if (done ? !me.C.Quests.IsDone(q.Id) : !me.C.Quests.IsActive(q.Id)) continue;
                    any = true;
                    GUI.color = done ? new Color(1, 1, 1, 0.5f) : Color.white;
                    GUI.Label(new Rect(px + 14, y, pw - 28, 22), (q.Main ? "★ " : "") + q.Title + (done ? "   (done)" : ""), _bold);
                    y += 22;
                    if (!done)
                    {
                        StageDef s = h.CurrentStage(me, q.Id);
                        string text = s != null ? s.Text : "";
                        float th = _small.CalcHeight(new GUIContent(text), pw - 40);
                        GUI.Label(new Rect(px + 26, y, pw - 40, th), text, _small);
                        y += th + 8;
                    }
                    GUI.color = Color.white;
                }
            if (!any) GUI.Label(new Rect(px + 14, y, pw - 28, 40), "No quests yet. Talk to people in the village (E).", _small);
        }

        /// <summary>The quest line, top right under the clock: what to do and which way it is.</summary>
        void DrawObjective(Host h, PlayerRec me, float w)
        {
            // Survival-only has no story to follow: the ruin is still there to find.
            if (PlaySession.Instance != null && PlaySession.Instance.SurvivalOnly) return;
            Objective o = h.ObjectiveFor(me.Id);
            if (o.Done) return;
            string where = "";
            if (o.Target.HasValue)
            {
                Vec3 to = o.Target.Value - me.Pos;
                double flat = to.FlatLength;
                where = flat < 3 ? "here" : Mathf.RoundToInt((float)flat) + " m " + Bearing(me.Facing, to);
                if (to.Y < -3) where += ", below";
                else if (to.Y > 3) where += ", above";
            }
            float bw = 300;
            Box(new Rect(w - bw - 10, 60, bw, where.Length > 0 ? 82 : 64), new Color(0, 0, 0, 0.45f));
            GUI.Label(new Rect(w - bw - 2, 62, bw - 12, 22), o.Title, _title);
            GUI.Label(new Rect(w - bw - 2, 84, bw - 12, 40), o.Text, _small);
            if (where.Length > 0) GUI.Label(new Rect(w - bw - 2, 120, bw - 12, 20), where, _small);
        }

        /// <summary>Where a direction lies relative to where you look: ahead, left, behind-right...</summary>
        static string Bearing(Vec3 facing, Vec3 to)
        {
            Vec3 f = facing.Flat, t = to.Flat;
            if (f.Length < 1e-6 || t.Length < 1e-6) return "";
            double a = Mathf.Atan2((float)(f.X * t.Z - f.Z * t.X), (float)(f.X * t.X + f.Z * t.Z)) * Mathf.Rad2Deg;   // + is to the left
            string[] names = { "ahead", "ahead-left", "left", "behind-left", "behind", "behind-right", "right", "ahead-right" };
            int i = Mathf.RoundToInt((float)(a / 45.0));
            return names[((i % 8) + 8) % 8];
        }

        /// <summary>The speak panel: the words you have seen, how well you know them, and the phrase so far.</summary>
        void DrawSpeak(Host h, PlayerRec me, float w, float hotbarY)
        {
            List<string> words = WordsFor(h, me);
            float pw = 380, lineH = 20, ph = 70 + Mathf.Max(1, words.Count) * lineH;
            float px = w / 2 - pw / 2, py = hotbarY - 34 - ph;
            Box(new Rect(px, py, pw, ph), new Color(0.02f, 0.08f, 0.09f, 0.82f));
            GUI.Label(new Rect(px + 10, py + 4, pw - 20, 22), "Speak Avarra", _title);
            float ly = py + 28;
            if (words.Count == 0) { GUI.Label(new Rect(px + 10, ly, pw - 20, lineH), "You know no words yet. Read the carvings in old ruins (E).", _small); ly += lineH; }
            for (int i = 0; i < words.Count && i < 9; i++)
            {
                Familiarity f = me.C.Lang.Of(words[i]);
                string meaning = f == Familiarity.Known ? h.WordMeaning(words[i])
                    : f == Familiarity.Guessed ? h.WordMeaning(words[i]) + "?  (try it)"
                    : "?  (seen once: find it again)";
                GUI.color = f == Familiarity.Seen ? new Color(1, 1, 1, 0.55f) : Color.white;
                GUI.Label(new Rect(px + 10, ly, pw - 20, lineH), (i + 1) + "   " + h.WordName(words[i]) + "   " + meaning, _small);
                GUI.color = Color.white;
                ly += lineH;
            }
            var said = new List<string>();
            foreach (string id in Phrase) said.Add(h.WordName(id));
            GUI.Label(new Rect(px + 10, ly + 2, pw - 20, lineH), "Say: " + (said.Count > 0 ? string.Join(" ", said.ToArray()) : "..."), _small);
            GUI.Label(new Rect(px + 10, ly + 20, pw - 20, lineH), "Numbers pick, Enter or X speaks at the crosshair, Backspace undoes, Esc closes", _small);
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
                Box(new Rect(inner.x + 8, inner.y + 5, inner.width - 16, inner.height - 20), col);
                GUI.Label(new Rect(inner.x - 4, inner.y + inner.height - 15, inner.width + 8, 14), Short(def.Name), _tiny);
                if (st.Value.Count > 1) GUI.Label(new Rect(inner.x + 2, inner.y, inner.width, 18), st.Value.Count.ToString(), _small);
            }
            if (label.Length > 0) GUI.Label(new Rect(r.x + r.width - 12, r.y, 12, 16), label, _small);
        }

        static string Short(string name)
        {
            int cut = name.IndexOf(" (", System.StringComparison.Ordinal);
            if (cut > 0) name = name.Substring(0, cut);
            return name.Length <= 10 ? name : name.Substring(0, 9) + ".";
        }

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
