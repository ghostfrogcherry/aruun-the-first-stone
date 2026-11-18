using System.Collections.Generic;
using Aruun.Core;
using Aruun.Core.Combat;
using Aruun.Core.Content;
using Aruun.Core.Inscription;
using Aruun.Core.Language;
using Aruun.Core.Play;
using Aruun.Core.Ruins;
using Aruun.Game.Art;
using UnityEngine;

namespace Aruun.Game
{
    /// <summary>
    /// How Avarra looks: the same glyph, built from the same cells, wherever a word appears. Carved
    /// glyphs are dark stone-cut cells (Deep Teal); they light (Resonance) only while something
    /// is answering, and when they light, they light cell by cell, like channels filling. No
    /// permanent glow, no enchantment shimmer (docs/WORLD_BIBLE.md, the visual language).
    ///
    /// Shows: the glyphs carved in the echo ruin and on the inscription table; the glyphs cut
    /// into an inscribed held item (a small view at the bottom right in first person); and the
    /// feedback of the words at work: grit from the struck stone and a ripple through it for
    /// resonant mining, a ring along the ground for a shockwave.
    /// </summary>
    public sealed class InscriptionView : MonoBehaviour
    {
        /// <summary>A quiet carved glyph (darker than the Deep Teal stone it is cut into) and active ancient language (Resonance).</summary>
        public static readonly Color Carved = new Color32(0x06, 0x0C, 0x0E, 255);   // the grooves of a quiet glyph: near black against the Deep Teal stone
        public static readonly Color Active = new Color32(0x62, 0xB8, 0xB1, 255);

        // ================================================================ glyph meshes

        static readonly Dictionary<string, Mesh> _meshes = new Dictionary<string, Mesh>();

        /// <summary>The cut cells of a glyph, in reading order (top row first).</summary>
        public static List<Vector2Int> Cells(string[] rows)
        {
            var cells = new List<Vector2Int>();
            for (int r = 0; r < rows.Length; r++)
                for (int c = 0; c < rows[r].Length; c++)
                    if (rows[r][c] == '#') cells.Add(new Vector2Int(c, r));
            return cells;
        }

        /// <summary>
        /// A glyph as a flat block of cubes in the local XY plane, centred, `cell` metres a
        /// side and `depth` deep along Z. Only the first `count` cells (-1: all). Cached.
        /// </summary>
        public static Mesh GlyphMesh(string[] rows, float cell, float depth, Color color, int count = -1)
        {
            string key = string.Join("/", rows) + "|" + cell + "|" + depth + "|" + ColorUtility.ToHtmlStringRGB(color) + "|" + count;
            Mesh m;
            if (_meshes.TryGetValue(key, out m)) return m;
            var cells = Cells(rows);
            if (count >= 0 && count < cells.Count) cells.RemoveRange(count, cells.Count - count);
            int size = rows.Length;
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var col = new List<Color32>();
            var tri = new List<int>();
            foreach (var c in cells)
            {
                var centre = new Vector3((c.x - (size - 1) * 0.5f) * cell, ((size - 1) * 0.5f - c.y) * cell, 0);
                AddCube(v, n, col, tri, centre, new Vector3(cell * 0.86f, cell * 0.86f, depth), color);
            }
            m = new Mesh { name = "glyph" };
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetColors(col);
            m.SetTriangles(tri, 0);
            m.RecalculateBounds();
            _meshes[key] = m;
            return m;
        }

        static readonly Vector3[] Faces = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };

        static void AddCube(List<Vector3> v, List<Vector3> n, List<Color32> col, List<int> tri, Vector3 centre, Vector3 size, Color color)
        {
            foreach (Vector3 f in Faces)
            {
                Vector3 a = f.x != 0 ? Vector3.up : Vector3.right;
                Vector3 b = Vector3.Cross(f, a);
                Vector3 h = Vector3.Scale(size, new Vector3(0.5f, 0.5f, 0.5f));
                Vector3 fc = centre + Vector3.Scale(f, h);
                Vector3 ha = Vector3.Scale(a, h), hb = Vector3.Scale(b, h);
                int start = v.Count;
                v.Add(fc - ha - hb); v.Add(fc + ha - hb); v.Add(fc + ha + hb); v.Add(fc - ha + hb);
                for (int i = 0; i < 4; i++) { n.Add(f); col.Add(color); }
                tri.Add(start); tri.Add(start + 1); tri.Add(start + 2);   // clockwise from outside: Unity's front faces
                tri.Add(start); tri.Add(start + 2); tri.Add(start + 3);
            }
        }

        internal static Mesh Bit(Color c) { return GlyphMesh(new[] { "#" }, 1f, 1f, c); }

        /// <summary>A carved glyph that can light up cell by cell.</summary>
        sealed class GlyphPlate
        {
            public Transform Root;
            public string[] Rows;
            public float Cell;
            public MeshFilter LitFilter;
            public int Lit = -1;
            public float LitUntil, LitStart;
            public bool Hold;

            public static GlyphPlate Make(string[] rows, float cell, Transform parent, string name)
            {
                var p = new GlyphPlate { Rows = rows, Cell = cell };
                p.Root = new GameObject(name).transform;
                p.Root.SetParent(parent, false);
                var carved = new GameObject("carved");
                carved.transform.SetParent(p.Root, false);
                carved.AddComponent<MeshFilter>().sharedMesh = GlyphMesh(rows, cell, cell * 0.5f, Carved);
                carved.AddComponent<MeshRenderer>().sharedMaterial = ModelLibrary.SolidMaterial;
                var lit = new GameObject("lit");
                lit.transform.SetParent(p.Root, false);
                lit.transform.localPosition = new Vector3(0, 0, -cell * 0.02f);
                p.LitFilter = lit.AddComponent<MeshFilter>();
                lit.AddComponent<MeshRenderer>().sharedMaterial = ModelLibrary.GlowMaterial;
                lit.SetActive(false);
                return p;
            }

            /// <summary>Lights the channels in sequence over `fill` seconds, then holds for `hold` seconds.</summary>
            public void Light(float fill, float hold)
            {
                if (Time.time > LitUntil) LitStart = Time.time - (Lit > 0 ? fill : 0);
                LitUntil = Mathf.Max(LitUntil, Time.time + fill + hold);
                _fill = fill;
            }

            float _fill = 0.6f;

            public void Update()
            {
                bool on = Hold || Time.time < LitUntil;
                int total = Cells(Rows).Count;
                int want = !on ? 0 : Mathf.Clamp(Mathf.CeilToInt((Time.time - LitStart) / Mathf.Max(0.01f, _fill) * total), 1, total);
                if (Hold && Time.time >= LitUntil) want = total;
                if (want == Lit) return;
                Lit = want;
                LitFilter.gameObject.SetActive(want > 0);
                if (want > 0) LitFilter.sharedMesh = GlyphMesh(Rows, Cell, Cell * 0.55f, Active, want);
            }
        }

        // ================================================================ state

        PlaySession _s;
        readonly List<KeyValuePair<RuinInscription, GlyphPlate>> _ruin = new List<KeyValuePair<RuinInscription, GlyphPlate>>();
        bool _ruinBuilt;
        Transform _held;
        string _heldKey = "", _heldWords = "";
        readonly List<GlyphPlate> _heldGlyphs = new List<GlyphPlate>();
        Light _heldLight;

        sealed class Bit3 { public Transform T; public Vector3 Vel; public float Life, Max; public bool Fall; public float Grow; }
        readonly List<Bit3> _bits = new List<Bit3>();
        readonly List<KeyValuePair<Light, float>> _flashes = new List<KeyValuePair<Light, float>>();
        Transform _fxRoot;

        void Start()
        {
            _s = GetComponent<PlaySession>();
            _fxRoot = new GameObject("Inscription effects").transform;
        }

        void LateUpdate()
        {
            if (_s == null || _s.Host == null) return;
            Host h = _s.Host;
            PlayerRec me = _s.Me;
            if (!_ruinBuilt) BuildRuinGlyphs(h);
            if (me != null)
            {
                foreach (HostEvent e in _s.MyEvents) OnEvent(h, e);
                SyncHeld(h, me);
            }
            bool atTable = h.AtTable(PlaySession.LocalId);
            foreach (var kv in _ruin)
            {
                kv.Value.Hold = atTable && kv.Key.Place == "ruin_table";
                kv.Value.Update();
            }
            foreach (var g in _heldGlyphs) g.Update();
            StepBits();
        }

        // ================================================================ the ruin's glyphs

        void BuildRuinGlyphs(Host h)
        {
            _ruinBuilt = true;
            EchoRuin r = h.Ruin;
            if (r == null || h.Lexicon == null) return;
            var root = new GameObject("Echo ruin glyphs").transform;
            foreach (RuinInscription ins in r.Inscriptions)
            {
                WordDef w = h.Lexicon.Word(ins.Word);
                if (w == null || w.Glyph.Length == 0) continue;
                // The carving faces whichever side of it is open.
                Vector3 c = PlaySession.ToUnity(ins.Point());
                Vector3 half = new Vector3(ins.X1 - ins.X0 + 1, ins.Y1 - ins.Y0 + 1, ins.Z1 - ins.Z0 + 1) * (0.5f * WorldConfig.VoxelSize);
                Vector3 normal = Vector3.zero;
                foreach (Vector3 f in Faces)
                {
                    Vector3 probe = c + Vector3.Scale(f, half) + f * (WorldConfig.VoxelSize * 0.5f);
                    int x = Mathf.FloorToInt(probe.x / WorldConfig.VoxelSize), y = Mathf.FloorToInt(probe.y / WorldConfig.VoxelSize), z = Mathf.FloorToInt(probe.z / WorldConfig.VoxelSize);
                    if (!h.World.Blocks.Get(h.World.Get(x, y, z)).Collision) { normal = f; break; }
                }
                if (normal == Vector3.zero) continue;
                float face = Mathf.Abs(Vector3.Dot(half, normal));
                var plate = GlyphPlate.Make(w.Glyph, 0.085f, root, "glyph " + ins.Place);
                plate.Root.position = c + normal * (face + 0.005f);
                plate.Root.rotation = Quaternion.LookRotation(-normal, Vector3.up);
                _ruin.Add(new KeyValuePair<RuinInscription, GlyphPlate>(ins, plate));
            }
        }

        // ================================================================ the held item

        void SyncHeld(Host h, PlayerRec me)
        {
            var st = me.C.Inv[me.C.Selected];
            string key = st.HasValue ? st.Value.Key : "", words = st.HasValue && st.Value.Inscribed ? st.Value.Words : "";
            DevPlayer player = GetComponent<DevPlayer>();
            bool show = words.Length > 0 && !me.C.Dead && (player == null || !player.ThirdPerson) && !PlayHud.BagOpen;
            if (key != _heldKey || words != _heldWords)
            {
                _heldKey = key;
                _heldWords = words;
                if (_held != null) Destroy(_held.gameObject);
                _held = null;
                _heldGlyphs.Clear();
                if (words.Length > 0) BuildHeld(h, key, st.Value.WordList);
            }
            if (_held == null) return;
            _held.gameObject.SetActive(show);
            bool ringing = h.Resonating(me);
            foreach (var g in _heldGlyphs) g.Hold = ringing;
            // A fine shiver while it rings: felt more than seen.
            Vector3 rest = new Vector3(0.34f, -0.36f, 0.7f);
            _held.localPosition = ringing ? rest + Random.insideUnitSphere * 0.004f : Vector3.Lerp(_held.localPosition, rest, Time.deltaTime * 10f);
            if (_heldLight != null)
            {
                _heldLight.enabled = ringing;
                _heldLight.intensity = 0.6f + 0.25f * Mathf.Sin(Time.time * 40f);
            }
        }

        void BuildHeld(Host h, string key, string[] words)
        {
            _held = new GameObject("Held inscribed item").transform;
            _held.SetParent(transform, false);
            _held.localRotation = Quaternion.Euler(-12f, -28f, 8f);
            if (ModelLibrary.Has(key))
            {
                GameObject model = ModelLibrary.Instantiate(key, _held);
                model.transform.localScale = Vector3.one * 0.28f;
                model.transform.localPosition = new Vector3(0, -0.06f, 0.05f);
                foreach (var r in model.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            // The words sit in a row on a small inlaid plate, read left to right.
            for (int i = 0; i < words.Length; i++)
            {
                WordDef w = h.Lexicon != null ? h.Lexicon.Word(words[i]) : null;
                if (w == null || w.Glyph.Length == 0) continue;
                // An inlay of Deep Teal stone on the side of the tool, turned to face you, with the
                // word's glyph cut into it: the same shape as on the ruin wall.
                const float cell = 0.018f;
                var plate = GlyphPlate.Make(w.Glyph, cell, _held, "glyph " + w.Id);
                plate.Root.localRotation = Quaternion.Inverse(_held.localRotation);
                plate.Root.localPosition = new Vector3(-0.02f + i * 0.12f, 0.17f, -0.06f);   // up on the handle, in view
                var inlay = new GameObject("inlay");
                inlay.transform.SetParent(plate.Root, false);
                inlay.transform.localPosition = new Vector3(0, 0, cell * 0.45f);
                inlay.AddComponent<MeshFilter>().sharedMesh = GlyphMesh(new[] { "#" }, cell * 6.4f, cell * 0.5f, new Color32(0x31, 0x5E, 0x68, 255));
                inlay.AddComponent<MeshRenderer>().sharedMaterial = ModelLibrary.SolidMaterial;
                _heldGlyphs.Add(plate);
            }
            _heldLight = new GameObject("resonance").AddComponent<Light>();
            _heldLight.transform.SetParent(_held, false);
            _heldLight.type = LightType.Point;
            _heldLight.color = Active;
            _heldLight.range = 1.2f;
            _heldLight.shadows = LightShadows.None;
            _heldLight.enabled = false;
        }

        // ================================================================ feedback

        void OnEvent(Host h, HostEvent e)
        {
            switch (e.Kind)
            {
                case "read":
                    // The word becomes legible: the carving nearest the read lights in sequence.
                    GlyphPlate best = null;
                    float bestD = 2f;
                    foreach (var kv in _ruin)
                    {
                        float d = Vector3.Distance(PlaySession.ToUnity(kv.Key.Point()), PlaySession.ToUnity(e.At));
                        if (d < bestD) { bestD = d; best = kv.Value; }
                    }
                    if (best != null) best.Light(0.7f, 1.6f);
                    break;
                case "inscribe":
                    foreach (var kv in _ruin) if (kv.Key.Place == "ruin_table") kv.Value.Light(0.5f, 1.5f);
                    Flash(PlaySession.ToUnity(e.At) + Vector3.up * 0.3f, 1.4f, 2.5f, 0.7f);
                    break;
                case "resonate_on":
                    foreach (var g in _heldGlyphs) g.Light(0.35f, 0f);
                    break;
                case "resonate":
                    Resonate(h, e);
                    break;
                case "slam":
                    // A Fallhammer slam (a player's: creature slams carry the creature's id).
                    // Dust and grit thrown out from the impact, earth-coloured: weight, not light.
                    if (e.Creature == 0) Dust(PlaySession.ToUnity(e.At), (float)e.Value);
                    break;
                case "shockwave":
                    Ring(PlaySession.ToUnity(e.At) + Vector3.up * 0.15f, (float)e.Value, 40, 0.5f);
                    Flash(PlaySession.ToUnity(e.At) + Vector3.up, 1.6f, (float)e.Value + 2f, 0.35f);
                    foreach (var g in _heldGlyphs) g.Light(0.2f, 0.5f);
                    break;
            }
        }

        /// <summary>Resonant mining: grit from the struck stone, a ripple of light through it, a low flash. Small and precise.</summary>
        void Resonate(Host h, HostEvent e)
        {
            Vector3 at = PlaySession.ToUnity(e.At);
            var b = h.World.Blocks.Get(e.Block);
            Color stone = b.Id != 0 ? (Color)new Color32(b.R, b.G, b.B, 255) : new Color(0.6f, 0.4f, 0.3f);
            int n = Mathf.Clamp((int)e.Value / 2, 6, 18);
            for (int i = 0; i < n; i++)
            {
                Vector3 dir = Random.onUnitSphere;
                Spawn(Bit(stone), at + dir * 0.3f, dir * Random.Range(1.5f, 3.5f) + Vector3.up * 1.5f, Random.Range(0.5f, 0.9f), 0.06f, true, 0f);
            }
            Ring(at, 1.6f, 18, 0.35f);
            Flash(at, 1.2f, 4f, 0.3f);
        }

        static readonly Color DustColour = new Color32(0x80, 0x5d, 0x4b, 0xff);   // Ancient Stone
        static readonly Color GritColour = new Color32(0xd5, 0xb7, 0x7c, 0xff);   // Warm Sand

        void Dust(Vector3 at, float radius)
        {
            int count = Mathf.Clamp((int)(radius * 12), 16, 48);
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2 / count + Random.Range(-0.1f, 0.1f);
                Vector3 dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                Color c = (i & 1) == 0 ? DustColour : GritColour;
                Spawn(Bit(c), at + dir * 0.3f + Vector3.up * 0.1f, dir * (radius * Random.Range(1.6f, 2.4f)) + Vector3.up * Random.Range(1.5f, 3f),
                      Random.Range(0.45f, 0.8f), Random.Range(0.06f, 0.12f), true, 0f);
            }
        }

        void Ring(Vector3 at, float radius, int count, float seconds)
        {
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2 / count;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                Spawn(Bit(Active), at, dir * (radius / seconds), seconds, 0.05f, false, -0.04f / seconds);
            }
        }

        void Spawn(Mesh mesh, Vector3 at, Vector3 vel, float life, float size, bool fall, float grow)
        {
            var go = new GameObject("bit");
            go.transform.SetParent(_fxRoot, false);
            go.transform.position = at;
            go.transform.localScale = Vector3.one * size;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = fall ? ModelLibrary.SolidMaterial : ModelLibrary.GlowMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _bits.Add(new Bit3 { T = go.transform, Vel = vel, Life = life, Max = life, Fall = fall, Grow = grow });
        }

        void Flash(Vector3 at, float intensity, float range, float seconds)
        {
            var l = new GameObject("flash").AddComponent<Light>();
            l.transform.SetParent(_fxRoot, false);
            l.transform.position = at;
            l.type = LightType.Point;
            l.color = Active;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            _flashes.Add(new KeyValuePair<Light, float>(l, seconds));
        }

        void StepBits()
        {
            float dt = Time.deltaTime;
            for (int i = _bits.Count - 1; i >= 0; i--)
            {
                Bit3 b = _bits[i];
                b.Life -= dt;
                if (b.Life <= 0) { Destroy(b.T.gameObject); _bits.RemoveAt(i); continue; }
                if (b.Fall) b.Vel += Vector3.down * ((float)WorldConfig.Gravity * dt);
                b.T.position += b.Vel * dt;
                b.T.localScale = Vector3.one * Mathf.Max(0.005f, b.T.localScale.x + b.Grow * dt);
            }
            for (int i = _flashes.Count - 1; i >= 0; i--)
            {
                Light l = _flashes[i].Key;
                float left = _flashes[i].Value - dt;
                if (left <= 0) { Destroy(l.gameObject); _flashes.RemoveAt(i); continue; }
                l.intensity *= Mathf.Clamp01(left / Mathf.Max(0.01f, _flashes[i].Value));
                _flashes[i] = new KeyValuePair<Light, float>(l, left);
            }
        }
    }
}
