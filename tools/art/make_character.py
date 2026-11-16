#!/usr/bin/env python3
"""Small character maker: outfits a Vaari or a human from the Aruun palette and adds it to the game.

    python3 tools/art/make_character.py lyceum_warden --tunic stone:2 --skirt violet:2 --sash glow_teal --skin 4
    python3 tools/art/make_character.py scout --body human --tunic green:3 --skirt wood:2 --hair ink_1
    python3 tools/art/make_character.py --colours        # list what you can pick from

It writes content/models/creatures/<id>.vox (rigged: torso, head, arms, legs; clips idle,
walk, talk), an icon, and a preview picture, and records the choices in
tools/art/characters.json so rebuilding the art set keeps them. For anything beyond
colours (new body shapes, hats, tools in hand) open the .vox in MagicaVoxel and edit the
parts there: the game loads whatever you save.
"""
import argparse
import json
import pathlib
import subprocess
import sys

HERE = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import palette as P  # noqa: E402


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("id", nargs="?", help="model id, e.g. lyceum_warden")
    ap.add_argument("--tunic", default="dusk:2", help="ramp:level for the upper garment")
    ap.add_argument("--skirt", default="cloth:1", help="ramp:level for the lower garment")
    ap.add_argument("--sash", default="cloth_mustard", help="palette key for the sash")
    ap.add_argument("--skin", type=int, default=3, choices=(1, 2, 3, 4), help="hide ramp level, 1 dark to 4 pale")
    ap.add_argument("--body", default="vaari", choices=("vaari", "human"))
    ap.add_argument("--skin-key", default="hide_4", help="humans only: palette key for skin, e.g. hide_4, sandstone_3, wood_3")
    ap.add_argument("--hair", default="wood_1", help="humans only: palette key for hair")
    ap.add_argument("--seed", type=int, default=None, help="changes the cloth weave and ragged hem")
    ap.add_argument("--colours", action="store_true", help="list ramps and keys, then exit")
    args = ap.parse_args()

    if args.colours or not args.id:
        for ramp, keys in P.RAMP_KEYS.items():
            print(f"{ramp:10s} levels 1-{len(keys)}   " + "  ".join(f"{k} {P.HEX[k]}" for k in keys))
        return
    for value in (args.tunic, args.skirt):
        ramp, _, level = value.partition(":")
        if ramp not in P.RAMP_KEYS or not (1 <= int(level or 2) <= len(P.RAMP_KEYS[ramp])):
            sys.exit(f"'{value}' is not ramp:level. Run with --colours to see the choices.")
    if args.sash not in P.INDEX:
        sys.exit(f"'{args.sash}' is not a palette key. Run with --colours to see the choices.")

    path = HERE / "characters.json"
    data = json.loads(path.read_text())
    row = {"id": args.id, "tunic": args.tunic, "skirt": args.skirt, "sash": args.sash, "skin": args.skin,
           "body": args.body, "skin_key": args.skin_key, "hair": args.hair,
           "seed": args.seed if args.seed is not None else sum(map(ord, args.id)) % 97}
    data["characters"] = [c for c in data["characters"] if c["id"] != args.id] + [row]
    path.write_text(json.dumps(data, indent=2) + "\n")
    subprocess.check_call([sys.executable, str(HERE / "build.py")])

    import creatures
    import voxlib
    parts, _ = creatures.character(row)
    out = HERE.parents[1] / "docs/art" / f"character_{args.id}.png"
    voxlib.render([v for _, v in parts], scale=10).save(out)
    print(f"Made {args.id}. Preview: {out.relative_to(HERE.parents[1])}. In game: ModelLibrary.instantiate(\"{args.id}\")")


if __name__ == "__main__":
    main()
