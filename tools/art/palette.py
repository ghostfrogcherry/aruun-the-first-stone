"""The Aruun master palette: 64 colours in named ramps (dark to light).

This file is the source of truth. `python3 tools/art/build.py` exports it to
content/art/ (JSON for the game, PNG for MagicaVoxel, GPL for Krita/Aseprite/GIMP).
Palette index = position in this list + 1 (index 0 is "empty" in MagicaVoxel).
Never reorder or remove entries: models store indices. Append instead.
"""

RAMPS = [
    # Ground: red soil and rock under the green.
    ("rust", ["#3c2219", "#5a2c20", "#7a3b2a", "#9a4a32", "#b85f3c", "#d07a4c"]),
    ("sand", ["#b9825a", "#cd9c6c", "#e0b98a"]),
    # Vaari masonry and plaster.
    ("sandstone", ["#8a6655", "#a98570", "#c9a78c", "#dcc3a8", "#ece4d2"]),
    ("stone", ["#2e2928", "#4a4240", "#6a625c", "#8c867c", "#b3aca0"]),
    ("ink", ["#130d10", "#38293c"]),
    ("wood", ["#3d2a1e", "#5a3f2c", "#6e4b32", "#8f6a45"]),
    # Iron-pigment plants: reds and purples instead of greens.
    ("crimson", ["#4a1420", "#7a1c2a", "#a8363a", "#cf4148", "#e86e5e"]),
    ("violet", ["#2e1b3d", "#4b2a5e", "#6d3d82", "#9058a6", "#b98bc8"]),
    # ...and plenty of green: Aruun is a flourishing world. Shared with the terrain blocks.
    ("green", ["#274a2c", "#3d7536", "#4f6b3a", "#5e8f3c", "#76a84a", "#a9cc6a"]),
    ("herb", ["#4e9a5a"]),
    ("water", ["#17414a", "#256b70", "#3a86a8", "#7fc9b9"]),
    ("dusk", ["#262a47", "#445781", "#899cab"]),
    # Living things and what they wear.
    ("hide", ["#4a2a2c", "#7b4a44", "#a8706a", "#cf9f94"]),
    ("cloth", ["#7c2f3e", "#3b6470", "#e3b23c", "#e6dcc4"]),  # wine, slate teal, mustard, linen
    ("bronze", ["#5c4326", "#9a7436", "#d2a857"]),
    # Light sources. Teal and violet are reserved for magick.
    ("glow", ["#3fb8a8", "#b06cff", "#ff8a2a", "#ffd27a"]),  # teal, violet, ember, lamp
]
GLOW_NAMES = ["teal", "violet", "ember", "lamp"]
CLOTH_NAMES = ["wine", "slate", "mustard", "linen"]

COLORS = []  # [{index, key, hex, ramp, emissive}]
for ramp, hexes in RAMPS:
    for i, h in enumerate(hexes):
        if ramp == "glow":
            key = "glow_" + GLOW_NAMES[i]
        elif ramp == "cloth":
            key = "cloth_" + CLOTH_NAMES[i]
        else:
            key = f"{ramp}_{i + 1}"
        COLORS.append({"index": len(COLORS) + 1, "key": key, "hex": h, "ramp": ramp,
                       "emissive": ramp == "glow"})
assert len(COLORS) == 64, len(COLORS)
assert len({c["hex"] for c in COLORS}) == 64, "palette colours must be unique"

INDEX = {c["key"]: c["index"] for c in COLORS}
HEX = {c["key"]: c["hex"] for c in COLORS}
RAMP_KEYS = {}
for c in COLORS:
    RAMP_KEYS.setdefault(c["ramp"], []).append(c["key"])


def rgb(key_or_index):
    c = COLORS[key_or_index - 1] if isinstance(key_or_index, int) else COLORS[INDEX[key_or_index] - 1]
    h = c["hex"]
    return int(h[1:3], 16), int(h[3:5], 16), int(h[5:7], 16)


def rgba_table():
    """256 RGBA entries as MagicaVoxel stores them (entry i is palette index i + 1)."""
    out = []
    for i in range(256):
        if i < len(COLORS):
            out.append(rgb(i + 1) + (255,))
        else:
            out.append((0, 0, 0, 255) if i < 255 else (0, 0, 0, 0))
    return out
