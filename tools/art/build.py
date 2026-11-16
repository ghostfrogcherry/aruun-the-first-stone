#!/usr/bin/env python3
"""Builds the Aruun starter art set from the generators in this folder.

    python3 tools/art/build.py            # write anything missing or unchanged-by-hand
    python3 tools/art/build.py --force    # overwrite everything, including hand-edited files
    python3 tools/art/build.py --no-images  # skip icons and preview sheets (no Pillow needed)

Outputs: content/art (palette), content/models (+ models.json), content/structures
(+ structures.json), content/icons, docs/art/preview_*.png.

A .vox that someone has edited by hand (in MagicaVoxel, say) is left alone: the manifest
remembers the hash of what the generator last wrote, and a file that no longer matches is
treated as hand-made from then on.
"""
import hashlib
import json
import pathlib
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import palette as P            # noqa: E402
import voxlib                  # noqa: E402
import props, flora, items, creatures, structures   # noqa: E402,E401

ROOT = pathlib.Path(__file__).resolve().parents[2]
MODELS = ROOT / "content/models"
STRUCTS = ROOT / "content/structures"
FORCE = "--force" in sys.argv
IMAGES = "--no-images" not in sys.argv
MODEL_VOXEL_M = 0.0625
TERRAIN_VOXEL_M = 0.25

CATEGORIES = [("props", props.PROPS), ("props", {"flint_nodule": items.flint_nodule}), ("flora", flora.FLORA),
              ("crops", flora.CROPS), ("items", items.ITEMS), ("creatures", creatures.CREATURES)]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def load_json(path):
    return json.loads(path.read_text()) if path.exists() else {}


def write_if_ours(path, old_entry, parts):
    """Writes the .vox unless it was edited by hand. Returns (hash, hand_edited)."""
    if path.exists() and not FORCE and old_entry and sha(path) != old_entry.get("generated_sha256"):
        print(f"  keeping hand-edited {path.relative_to(ROOT)}")
        return old_entry.get("generated_sha256"), True
    path.parent.mkdir(parents=True, exist_ok=True)
    voxlib.write_vox(path, parts)
    return sha(path), False


def size_of(parts):
    lo = [min(v.bounds()[0][i] for _, v in parts) for i in range(3)]
    hi = [max(v.bounds()[1][i] for _, v in parts) for i in range(3)]
    return [hi[i] - lo[i] + 1 for i in range(3)]


def doc_of(fn):
    return " ".join((getattr(fn, "__doc__", None) or "").split())


def export_palette():
    art = ROOT / "content/art"
    art.mkdir(parents=True, exist_ok=True)
    data = {"_comment": "Generated from tools/art/palette.py. Index is the MagicaVoxel palette index. Append only.",
            "colors": P.COLORS}
    (art / "palette.json").write_text(json.dumps(data, indent=1) + "\n")
    lines = ["GIMP Palette", "Name: Aruun", "Columns: 8", "#"]
    for c in P.COLORS:
        r, g, b = P.rgb(c["key"])
        lines.append(f"{r:3d} {g:3d} {b:3d}\t{c['key']}")
    (art / "aruun_palette.gpl").write_text("\n".join(lines) + "\n")
    if IMAGES:
        from PIL import Image
        strip = Image.new("RGBA", (256, 1))
        strip.putdata(P.rgba_table())
        strip.save(art / "aruun_palette.png")           # MagicaVoxel: drag onto the palette panel
        sw = 40
        chart = Image.new("RGB", (8 * sw, 8 * sw))
        for i, c in enumerate(P.COLORS):
            chart.paste(P.rgb(c["key"]), ((i % 8) * sw, (i // 8) * sw, (i % 8 + 1) * sw, (i // 8 + 1) * sw))
        (ROOT / "docs/art").mkdir(parents=True, exist_ok=True)
        chart.save(ROOT / "docs/art/palette.png")


def icon(parts, px=64):
    from PIL import Image
    w, h, d = size_of(parts)
    scale = max(2, min(12, int(3 * px / max(1, (w + d) + h))))
    img = voxlib.render([v for _, v in parts], scale=scale)
    img.thumbnail((px - 4, px - 4), Image.LANCZOS)
    out = Image.new("RGBA", (px, px), (0, 0, 0, 0))
    out.paste(img, ((px - img.width) // 2, (px - img.height) // 2), img)
    return out


def sheet(images, path, bg, width=1800):
    from PIL import Image
    rows, row, w = [], [], 0
    for im in images:
        if row and w + im.width > width:
            rows.append(row)
            row, w = [], 0
        row.append(im)
        w += im.width + 16
    rows.append(row)
    total_h = sum(max(i.height for i in r) + 16 for r in rows)
    out = Image.new("RGB", (width, total_h), bg)
    y = 0
    for r in rows:
        h = max(i.height for i in r)
        x = 8
        for im in r:
            out.paste(im, (x, y + h - im.height + 8), im)
            x += im.width + 16
        y += h + 16
    path.parent.mkdir(parents=True, exist_ok=True)
    out.save(path)


def main():
    export_palette()
    old = load_json(MODELS / "models.json").get("models", {})
    manifest = {}
    previews = {}
    for category, table in CATEGORIES:
        for model_id, fn in table.items():
            result = fn()
            parts, rig = result if isinstance(result, tuple) else (result, None)
            path = MODELS / category / f"{model_id}.vox"
            digest, hand = write_if_ours(path, old.get(model_id), parts)
            sx, sy, sz = size_of(parts)
            entry = {"file": f"{category}/{model_id}.vox", "category": category, "voxel_m": MODEL_VOXEL_M,
                     "size": [sx, sy, sz], "height_m": round(sy * MODEL_VOXEL_M, 2), "generated_sha256": digest}
            base = fn if not hasattr(fn, "__defaults__") or fn.__name__ != "<lambda>" else fn.__defaults__[0]
            if doc_of(base):
                entry["note"] = doc_of(base)
            if rig:
                entry["rig"] = rig
            if hand and model_id in old:
                entry = dict(old[model_id])
            manifest[model_id] = entry
            if IMAGES and not hand:
                (ROOT / "content/icons").mkdir(parents=True, exist_ok=True)
                icon(parts).save(ROOT / "content/icons" / f"{model_id}.png")
                scale = 6 if category in ("items", "crops", "flora") else 4
                previews.setdefault(category, []).append(voxlib.render([v for _, v in parts], scale=scale))
    for model_id, entry in old.items():      # imported and hand-made models are not ours to regenerate
        if model_id not in manifest and (MODELS / entry.get("file", "")).is_file():
            manifest[model_id] = entry
    (MODELS / "models.json").write_text(json.dumps({
        "_comment": "Generated by tools/art/build.py. Add hand-made models here too: id -> file, category, voxel_m.",
        "models": manifest}, indent=1) + "\n")

    old_s = load_json(STRUCTS / "structures.json").get("structures", {})
    smanifest = {}
    for sid, fn in structures.STRUCTURES.items():
        vox = fn()
        path = STRUCTS / f"{sid}.vox"
        digest, hand = write_if_ours(path, old_s.get(sid), [(sid, vox)])
        sx, sy, sz = size_of([(sid, vox)])
        smanifest[sid] = dict(old_s[sid]) if hand and sid in old_s else {
            "file": f"{sid}.vox", "voxel_m": TERRAIN_VOXEL_M, "size": [sx, sy, sz], "voxels": len(vox.v),
            "note": doc_of(fn), "generated_sha256": digest}
        if IMAGES and not hand:
            previews.setdefault("structures", []).append(voxlib.render([vox], scale=4))
    (STRUCTS / "structures.json").write_text(json.dumps({
        "_comment": "Structures stored as .vox, painted only in block colours. y = 0 is the floor layer.",
        "structures": smanifest}, indent=1) + "\n")

    if IMAGES:
        bg = (74, 92, 78)
        sheet(previews.get("props", []) + previews.get("flora", []), ROOT / "docs/art/preview_props.png", bg)
        sheet(previews.get("crops", []) + previews.get("items", []), ROOT / "docs/art/preview_items.png", bg)
        sheet(previews.get("creatures", []), ROOT / "docs/art/preview_creatures.png", bg)
        sheet(previews.get("structures", []), ROOT / "docs/art/preview_structures.png", (120, 150, 190))
    print(f"{len(manifest)} models, {len(smanifest)} structures, {len(P.COLORS)} colours")


if __name__ == "__main__":
    main()
