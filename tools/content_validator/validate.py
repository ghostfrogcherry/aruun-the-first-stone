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
EFFECT_HANDLERS = {"raise_column"}  # keep in sync with scripts/language/interpreter.gd REGISTRY
# Names already used by Aruun canon; provisional words must not reuse them.
CANON_NAMES = {"keth", "keth-avar", "kethra", "vael", "vaari", "shaaru", "nhal", "oth", "avarra"}

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
    if w.get("effect") not in effects:
        err(f"lexicon: word '{wid}' uses unknown effect '{w.get('effect')}'")
    spoken = w.get("display", "").split(" ")[0].lower()
    if spoken in CANON_NAMES:
        err(f"lexicon: word '{wid}' display '{spoken}' collides with an Aruun canon name")
    block = w.get("params", {}).get("block")
    if block and block not in keys:
        err(f"lexicon: word '{wid}' places unknown block '{block}'")
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

if errors:
    print("\n".join("ERROR " + e for e in errors))
    sys.exit(1)
print("content OK")
