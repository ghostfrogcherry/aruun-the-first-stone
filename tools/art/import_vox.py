#!/usr/bin/env python3
"""Brings a .vox made elsewhere (a free pack, a friend's MagicaVoxel file) into the game.

    python3 tools/art/import_vox.py ~/Downloads/chest.vox --id old_chest --category props \\
        --credit "Voxel Dungeon Set by austin-io" --license CC0

It copies the file to content/models/<category>/<id>.vox, snaps every colour to the nearest
colour of the Aruun palette so it sits with the rest of the art (skip with --keep-colours),
and lists it in content/models/models.json so ModelLibrary.instantiate("<id>") works.
Credits go to docs/art/THIRD_PARTY.md. Only import files whose licence allows it.

Scale: --voxel-m is the real size of one voxel. Props and creatures use 0.0625 (1/16 m).
A chunky model drawn at low resolution may want 0.125.
"""
import argparse
import json
import pathlib
import struct
import sys

HERE = pathlib.Path(__file__).resolve().parent
ROOT = HERE.parents[1]
sys.path.insert(0, str(HERE))
import palette as P  # noqa: E402

CATEGORIES = ("props", "flora", "crops", "items", "creatures")


def nearest(r, g, b, targets):
    # "redmean" distance: cheap and close enough to how eyes judge colour difference
    def dist(t):
        rm = (r + t[0]) / 2
        return (2 + rm / 256) * (r - t[0]) ** 2 + 4 * (g - t[1]) ** 2 + (2 + (255 - rm) / 256) * (b - t[2]) ** 2
    return min(targets, key=dist)


def snap_palette(data):
    """Rewrites the RGBA chunk in place; voxels keep their indices."""
    targets = [P.rgb(c["key"]) for c in P.COLORS if not c["emissive"]]   # never turn things into lamps by accident
    pos = 20
    while pos + 12 <= len(data):
        cid = bytes(data[pos:pos + 4])
        n, m = struct.unpack_from("<ii", data, pos + 4)
        if cid == b"RGBA":
            for i in range(255):
                o = pos + 12 + i * 4
                data[o:o + 3] = bytes(nearest(data[o], data[o + 1], data[o + 2], targets))
            return True
        pos += 12 + n + m
    return False


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("file")
    ap.add_argument("--id", required=True)
    ap.add_argument("--category", default="props", choices=CATEGORIES)
    ap.add_argument("--voxel-m", type=float, default=0.0625)
    ap.add_argument("--credit", default="", help="author and pack name")
    ap.add_argument("--license", default="", help="e.g. CC0, CC BY 4.0")
    ap.add_argument("--keep-colours", action="store_true")
    args = ap.parse_args()

    data = bytearray(pathlib.Path(args.file).expanduser().read_bytes())
    if data[:4] != b"VOX ":
        sys.exit("That is not a MagicaVoxel .vox file.")
    if not args.keep_colours and not snap_palette(data):
        sys.exit("The file has no palette to recolour (very old .vox). Open and re-save it in MagicaVoxel first.")
    out = ROOT / "content/models" / args.category / f"{args.id}.vox"
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_bytes(data)

    manifest_path = ROOT / "content/models/models.json"
    manifest = json.loads(manifest_path.read_text())
    entry = {"file": f"{args.category}/{args.id}.vox", "category": args.category, "voxel_m": args.voxel_m,
             "imported": True}
    if args.credit:
        entry["credit"] = args.credit
    if args.license:
        entry["license"] = args.license
    manifest["models"][args.id] = entry
    manifest_path.write_text(json.dumps(manifest, indent=1) + "\n")

    if args.credit or args.license:
        credits = ROOT / "docs/art/THIRD_PARTY.md"
        text = credits.read_text() if credits.exists() else "# Third-party art\n\n| Model | Credit | Licence |\n| --- | --- | --- |\n"
        text = "".join(line for line in text.splitlines(True) if not line.startswith(f"| {args.id} |"))
        credits.write_text(text + f"| {args.id} | {args.credit or '-'} | {args.license or '-'} |\n")
    print(f"Imported {args.id}. In game: ModelLibrary.instantiate(\"{args.id}\"). See it in scenes/art/gallery.tscn.")


if __name__ == "__main__":
    main()
