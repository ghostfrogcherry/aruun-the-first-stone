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

if errors:
    print("\n".join("ERROR " + e for e in errors))
    sys.exit(1)
print("content OK")
