#!/usr/bin/env python3
"""Checks content/ for broken references, duplicate ids and malformed definitions.

Run from the repo root:  python3 tools/content_validator/validate.py
Exit code 1 if anything is wrong. Runs in CI without Godot.
"""
import json
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
CONTENT = ROOT / "content"
EFFECT_HANDLERS = {"raise_column", "force_push", "force_pull", "force_weigh"}  # keep in sync with Lexicon.Handlers in unity/Assets/Aruun/Core/Language/Lexicon.cs
# Names already used by Aruun canon; provisional words must not reuse them.
CANON_NAMES = {"keth", "keth-avar", "kethra", "vael", "vaari", "shaaru", "nhal", "oth", "avarra", "oru", "nereth", "sael", "ilyen"}

errors = []


def err(msg):
    errors.append(msg)


def load(path):
    try:
        return json.loads(path.read_text())
    except Exception as e:  # noqa: BLE001
        err(f"{path.relative_to(ROOT)}: invalid JSON ({e})")
        return {}


blocks = load(CONTENT / "blocks/blocks.json")
keys = {}
for i, b in enumerate(blocks.get("blocks", [])):
    if b.get("id") != i:
        err(f"blocks: '{b.get('key')}' has id {b.get('id')}, expected {i} (ids must be contiguous)")
    if b.get("key") in keys:
        err(f"blocks: duplicate key '{b.get('key')}'")
    keys[b.get("key")] = i
    if b.get("solid") and not re.fullmatch(r"#[0-9a-fA-F]{6}", b.get("color", "")):
        err(f"blocks: '{b.get('key')}' needs a #rrggbb color")
if keys.get("air") != 0:
    err("blocks: id 0 must be 'air'")
for k in blocks.get("placeable", []):
    if k not in keys:
        err(f"blocks: placeable '{k}' is not a block")

lex = load(CONTENT / "language/lexicon.json")
effects = {e["id"]: e for e in lex.get("effects", [])}
for eid in effects:
    if eid not in EFFECT_HANDLERS:
        err(f"lexicon: effect '{eid}' has no handler in the interpreter")
word_ids = set()
for w in lex.get("words", []):
    wid = w.get("id")
    if wid in word_ids:
        err(f"lexicon: duplicate word id '{wid}'")
    word_ids.add(wid)
    if w.get("speakable", True) and w.get("effect") not in effects:
        err(f"lexicon: word '{wid}' uses unknown effect '{w.get('effect')}'")
    glyph = w.get("glyph", [])
    if len(glyph) != 5 or any(not re.fullmatch(r"[#.]{5}", row) for row in glyph):
        err(f"lexicon: word '{wid}' needs a glyph of 5 rows of 5 '#' or '.'")
    spoken = w.get("display", "").split(" ")[0].lower()
    if spoken in CANON_NAMES:
        err(f"lexicon: word '{wid}' display '{spoken}' collides with an Aruun canon name")
    block = w.get("params", {}).get("block")
    if block and block not in keys:
        err(f"lexicon: word '{wid}' places unknown block '{block}'")
ins = load(CONTENT / "language/inscriptions.json")
items_doc = load(CONTENT / "items/items.json")
item_classes = {i.get("inscribe", {}).get("class") for i in items_doc.get("items", []) if i.get("inscribe")}
INSCRIPTION_TYPES = {"passive", "activated", "conditional", "compositional"}
for rule in ins.get("words", []) + ins.get("compositions", []):
    rid = rule.get("word") or rule.get("id")
    for wid in rule.get("words", [rule.get("word")]):
        if wid not in word_ids:
            err(f"inscriptions: '{rid}' uses unknown word '{wid}'")
    if rule.get("type") not in INSCRIPTION_TYPES:
        err(f"inscriptions: '{rid}' has unknown type '{rule.get('type')}'")
    for cls in rule.get("fits", []):
        if cls not in item_classes:
            err(f"inscriptions: '{rid}' fits '{cls}', which no item is")
for b in blocks.get("blocks", []):
    if b.get("inscription") and b["inscription"] not in word_ids:
        err(f"blocks: '{b['key']}' inscribes unknown word '{b['inscription']}'")

for path in (CONTENT / "structures").glob("*.json"):
    if path.name == "structures.json":
        continue
    s = load(path)
    seen = set()
    for v in s.get("voxels", []):
        if v[3] not in keys:
            err(f"{path.name}: unknown block '{v[3]}'")
            break
        if tuple(v[:3]) in seen:
            err(f"{path.name}: duplicate voxel at {v[:3]}")
            break
        seen.add(tuple(v[:3]))

# --- Art: palette, models, .vox structures -----------------------------------
import struct  # noqa: E402


def read_vox(path):
    """Returns (palette hex by index, set of used palette indices, object names)."""
    data = path.read_bytes()
    if data[:4] != b"VOX ":
        err(f"{path.relative_to(ROOT)}: not a .vox file")
        return {}, set(), []
    pos, pal, used, names = 20, {}, set(), []
    while pos + 12 <= len(data):
        cid = data[pos:pos + 4]
        n, m = struct.unpack_from("<ii", data, pos + 4)
        body = data[pos + 12:pos + 12 + n]
        if cid == b"XYZI":
            used.update(body[7::4])
        elif cid == b"RGBA":
            pal = {i + 1: "#%02x%02x%02x" % tuple(body[i * 4:i * 4 + 3]) for i in range(255)}
        elif cid == b"nTRN":
            count = struct.unpack_from("<i", body, 4)[0]
            p = 8
            for _ in range(count):
                klen = struct.unpack_from("<i", body, p)[0]
                key = body[p + 4:p + 4 + klen]
                p += 4 + klen
                vlen = struct.unpack_from("<i", body, p)[0]
                if key == b"_name":
                    names.append(body[p + 4:p + 4 + vlen].decode())
                p += 4 + vlen
        pos += 12 + n + m
    return pal, used, names


palette = load(CONTENT / "art/palette.json").get("colors", [])
pal_hex = [c.get("hex", "").lower() for c in palette]
if len(set(pal_hex)) != len(pal_hex):
    err("palette: colours must be unique")
if [c.get("index") for c in palette] != list(range(1, len(palette) + 1)):
    err("palette: indices must run 1..n in order (append only)")

models = load(CONTENT / "models/models.json").get("models", {})
for mid, m in models.items():
    path = CONTENT / "models" / m.get("file", "")
    if not path.is_file():
        err(f"models: '{mid}' points at missing file {m.get('file')}")
        continue
    pal, used, names = read_vox(path)
    off = sorted({pal.get(i, "?") for i in used} - set(pal_hex))
    if off:
        err(f"models: '{mid}' uses colours outside the Aruun palette: {', '.join(off[:5])}")
    rig = m.get("rig")
    if rig:
        for part, d in rig.get("parts", {}).items():
            if part not in names:
                err(f"models: '{mid}' rig part '{part}' is not an object in the .vox")
            if d.get("parent") and d["parent"] not in rig["parts"]:
                err(f"models: '{mid}' part '{part}' has unknown parent '{d['parent']}'")
        for clip, channels in rig.get("clips", {}).items():
            for ch in channels:
                if ch.get("part") not in rig.get("parts", {}):
                    err(f"models: '{mid}' clip '{clip}' moves unknown part '{ch.get('part')}'")

block_hex = {b["color"].lower() for b in blocks.get("blocks", []) if b.get("solid") and b.get("color")}
# A block may name an older "paint" colour that still means it in .vox art.
block_hex |= {b["paint"].lower() for b in blocks.get("blocks", []) if b.get("solid") and b.get("paint")}
solid_colors = [b["color"].lower() for b in blocks.get("blocks", []) if b.get("solid") and b.get("color")]
if len(set(solid_colors)) != len(solid_colors):
    err("blocks: two blocks share a colour, so .vox structures cannot tell them apart")
vox_structures = load(CONTENT / "structures/structures.json").get("structures", {})
for sid, sdef in vox_structures.items():
    path = CONTENT / "structures" / sdef.get("file", "")
    if not path.is_file():
        err(f"structures: '{sid}' points at missing file {sdef.get('file')}")
        continue
    pal, used, _ = read_vox(path)
    off = sorted({pal.get(i, "?") for i in used} - block_hex)
    if off:
        err(f"structures: '{sid}' uses colours that are not block colours: {', '.join(off[:5])}")

# --- Survival content: items, recipes, structures, rulesets, plants, creatures ---
items_doc = load(CONTENT / "items/items.json")
block_keys = {b["key"] for b in blocks.get("blocks", [])}
# The game adds an item for every solid block that can be dug, so those count too.
item_keys = {i["key"] for i in items_doc.get("items", [])} | {
    b["key"] for b in blocks.get("blocks", []) if b.get("solid") and not b.get("fluid") and b.get("hardness", 0) < 99}
ITEM_KINDS = {"material", "food", "seed", "tool", "weapon", "gear", "structure", "block"}
plants_doc = load(CONTENT / "farming/plants.json")
plant_keys = {p["key"] for p in plants_doc.get("plants", [])}
structure_keys = set(items_doc.get("structures", {}))
rules_doc = load(CONTENT / "survival/rulesets.json")
buff_keys = set(rules_doc.get("buffs", {}))
seen = set()
for it in items_doc.get("items", []):
    k = it.get("key", "?")
    if k in seen:
        err(f"items: '{k}' is defined twice")
    seen.add(k)
    if it.get("kind") not in ITEM_KINDS:
        err(f"items: '{k}' has unknown kind '{it.get('kind')}'")
    if it.get("kind") in ("tool", "weapon"):
        for f in ("damage", "reach_m", "arc_deg", "stamina", "cooldown_s", "poise", "knockback"):
            if f not in it:
                err(f"items: '{k}' is a {it['kind']} without '{f}'")
    if it.get("kind") == "seed" and it.get("plant") not in plant_keys:
        err(f"items: seed '{k}' grows unknown plant '{it.get('plant')}'")
    if it.get("kind") == "structure" and it.get("structure") not in structure_keys:
        err(f"items: '{k}' sets down unknown structure '{it.get('structure')}'")
    buff = it.get("use", {}).get("buff")
    if buff and buff not in buff_keys:
        err(f"items: '{k}' gives unknown buff '{buff}'")
for bk, drops in items_doc.get("block_drops", {}).items():
    if bk == "_comment":
        continue
    if bk not in block_keys:
        err(f"block_drops: unknown block '{bk}'")
    for d in drops:
        if d.get("item") not in item_keys:
            err(f"block_drops: '{bk}' drops unknown item '{d.get('item')}'")
stations = {v.get("station") for v in items_doc.get("structures", {}).values()}
seen = set()
for r in items_doc.get("recipes", []):
    rid = r.get("id", "?")
    if rid in seen:
        err(f"recipes: '{rid}' is defined twice")
    seen.add(rid)
    for k in list(r.get("in", {})) + [r.get("out")]:
        if k not in item_keys:
            err(f"recipes: '{rid}' uses unknown item '{k}'")
    if r.get("station") and r["station"] not in stations:
        err(f"recipes: '{rid}' needs station '{r['station']}' that no structure provides")
for p in plants_doc.get("plants", []):
    for y in p.get("yields", []):
        if y.get("item") not in item_keys:
            err(f"plants: '{p['key']}' yields unknown item '{y.get('item')}'")
    for g in p.get("ground", []):
        if g not in block_keys:
            err(f"plants: '{p['key']}' grows in unknown block '{g}'")
    if p.get("wild", {}).get("where", "land") not in ("shore", "mesa", "meadow", "land"):
        err(f"plants: '{p['key']}' has unknown wild place '{p['wild']['where']}'")
for c in load(CONTENT / "creatures/creatures.json").get("creatures", []):
    if c.get("behavior") not in ("herd", "hunter", "skittish", "defender", "swarm", "guardian"):
        err(f"creatures: '{c.get('key')}' has unknown behavior '{c.get('behavior')}'")
    if c.get("spawn", {}).get("when") not in ("day", "night", "never"):
        err(f"creatures: '{c.get('key')}' has unknown spawn time '{c.get('spawn', {}).get('when')}'")
    if c.get("spawn", {}).get("where", "land") not in ("shore", "mesa", "meadow", "land"):
        err(f"creatures: '{c.get('key')}' has unknown spawn place '{c['spawn'].get('where')}'")
    buff = c.get("attack", {}).get("applies", {}).get("buff")
    if buff and buff not in buff_keys:
        err(f"creatures: '{c.get('key')}' applies unknown buff '{buff}'")
    if c.get("behavior") in ("hunter", "defender", "swarm") and "attack" not in c:
        err(f"creatures: hunter '{c.get('key')}' has no attack")
    for d in c.get("drops", []):
        if d.get("item") not in item_keys:
            err(f"creatures: '{c.get('key')}' drops unknown item '{d.get('item')}'")
rulesets = rules_doc.get("rulesets", {})
if rules_doc.get("default") not in rulesets:
    err("rulesets: 'default' names a ruleset that does not exist")
for rid, r in rulesets.items():
    if "extends" in r and r["extends"] not in rulesets:
        err(f"rulesets: '{rid}' extends unknown ruleset '{r['extends']}'")
    for k in r.get("starting_items", {}):
        if k not in item_keys:
            err(f"rulesets: '{rid}' starts with unknown item '{k}'")
    for w in r.get("starting_words", []):
        if w not in {x["id"] for x in lex.get("words", [])}:
            err(f"rulesets: '{rid}' starts with unknown word '{w}'")
    for cid, c in r.get("conditions", {}).items():
        for b in c.get("bands", []) + c.get("bands_add", []):
            if not (0 <= b.get("from", -1) < b.get("to", -1) <= 101):
                err(f"rulesets: '{rid}' condition '{cid}' band '{b.get('name')}' has a bad range")

if errors:
    print("\n".join("ERROR " + e for e in errors))
    sys.exit(1)
print("content OK")
