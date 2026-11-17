#!/usr/bin/env python3
"""Turn the painted Aruun map into the biome map the game generates terrain from.

    python3 tools/world/make_biome_map.py

Reads  content/world/aruun_map_art.png   (the painted map, 1448x1086)
Writes content/world/aruun_biomes.png    (one pixel per map cell, flat biome colours)
       docs/world/biomes_preview.png     (side by side, for checking)

The biome PNG is the real source of truth once made: anyone can repaint it in any paint
program using the exact colours in content/world/map.json. Re-running this script
overwrites it, so only run it again if the painted map changes.

Classification is by colour, with hand-drawn masks for the frame, legend, insets and
compass, and region hints where the painting's colours are ambiguous (volcanic Kharuun,
the Sael canyons, the Ilyari ice).
"""
import json
import os
import sys
from collections import Counter

from PIL import Image, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ART = os.path.join(ROOT, "content", "world", "aruun_map_art.png")
OUT = os.path.join(ROOT, "content", "world", "aruun_biomes.png")
PREVIEW = os.path.join(ROOT, "docs", "world", "biomes_preview.png")
MAP_JSON = os.path.join(ROOT, "content", "world", "map.json")

CELL = 4  # art pixels per map cell

# Areas of the painting that are decoration, not land (x0, y0, x1, y1), in art pixels.
MASKS = [
    (0, 0, 1448, 34), (0, 1052, 1448, 1086), (0, 0, 34, 1086), (1414, 0, 1448, 1086),
    (425, 0, 1025, 150),        # title cartouche
    (22, 22, 190, 205),         # Phobos inset
    (1262, 22, 1426, 205),      # Deimos inset
    (22, 655, 215, 840),        # compass rose
    (22, 835, 335, 1064),       # legend
    (505, 975, 945, 1064),      # scale bar
    (1035, 935, 1426, 1064),    # charter inscription and seal
]


def classify(r, g, b):
    mx, mn = max(r, g, b), min(r, g, b)
    sat = (mx - mn) / mx if mx else 0
    light = (r + g + b) / 3
    if light > 175 and sat < 0.22:
        return "glacial"
    if b > r + 12 and b >= g - 18:
        return "water"
    if b > g + 6 and r > g + 2:
        return "bloom"
    if g > r + 4 and g > b:
        return "forest" if light < 80 else "grassland"
    if r > 150 and g > 75 and b < 95 and r - g > 45:
        return "desert"
    if r > 95 and g < 75 and r - g > 45:
        return "volcanic"
    if abs(r - g) < 22 and g > b + 18:
        return "grassland"
    if r > g > b:
        return "highlands"
    return None


def region_hint(kind, cx, cy):
    """Region knowledge from the painting's labels, in art pixels."""
    def inside(ex, ey, rx, ry):
        return ((cx - ex) / rx) ** 2 + ((cy - ey) / ry) ** 2 <= 1
    # The north is open ocean except the green islands and the Teshari coast.
    if kind in ("highlands", "desert") and cy < 280 and not inside(700, 330, 330, 110):
        return "water"
    # Kharuun: volcanic highlands in the west.
    if kind in ("highlands", "desert", "volcanic") and inside(230, 500, 230, 300):
        return "volcanic" if kind == "volcanic" else "highlands"
    # Sael: cliff cities and canyons in the east.
    if kind in ("highlands", "desert") and inside(1060, 590, 300, 80):
        return "canyon"
    # Teshari river basins and the Nerethi islands are green lands with brown patches.
    if kind == "highlands" and (inside(700, 330, 330, 110) or (cx > 900 and cy < 420)):
        return "grassland"
    # The far south is ice and bloom.
    if kind in ("highlands", "desert") and cy > 770:
        return "glacial"
    return kind


def cells_to_text(png_path):
    """Write aruun_biomes.txt next to the PNG: one character per cell (biome id in base 36),
    one line per row. The game reads this file, so it needs no image decoder."""
    meta = json.load(open(os.path.join(os.path.dirname(png_path), "map.json")))
    by_rgb = {tuple(int(b["color"][i:i + 2], 16) for i in (1, 3, 5)): b["id"] for b in meta["biomes"]}
    img = Image.open(png_path).convert("RGB")
    lines = []
    for y in range(img.height):
        row = []
        for x in range(img.width):
            rgb = img.getpixel((x, y))
            if rgb not in by_rgb:
                raise SystemExit(f"pixel {x},{y} colour {rgb} is not a biome colour in map.json")
            row.append("0123456789abcdefghijklmnopqrstuvwxyz"[by_rgb[rgb]])
        lines.append("".join(row))
    with open(os.path.splitext(png_path)[0] + ".txt", "w") as f:
        f.write("\n".join(lines) + "\n")


def main():
    with open(MAP_JSON) as f:
        spec = json.load(f)
    colors = {b["key"]: tuple(int(b["color"][i:i + 2], 16) for i in (1, 3, 5)) for b in spec["biomes"]}

    art = Image.open(ART).convert("RGB")
    w, h = art.size
    gw, gh = w // CELL, h // CELL
    small = art.filter(ImageFilter.GaussianBlur(3)).resize((gw, gh), Image.BOX)

    grid = [[None] * gw for _ in range(gh)]
    for y in range(gh):
        for x in range(gw):
            ax, ay = x * CELL + CELL // 2, y * CELL + CELL // 2
            if any(x0 <= ax < x1 and y0 <= ay < y1 for x0, y0, x1, y1 in MASKS):
                continue
            k = classify(*small.getpixel((x, y)))
            grid[y][x] = region_hint(k, ax, ay) if k else None

    # Mode filter to drop labels, routes and brush noise.
    for _ in range(2):
        out = [[None] * gw for _ in range(gh)]
        for y in range(gh):
            for x in range(gw):
                c = Counter(grid[yy][xx] for yy in range(max(0, y - 2), min(gh, y + 3))
                            for xx in range(max(0, x - 2), min(gw, x + 3)) if grid[yy][xx])
                out[y][x] = c.most_common(1)[0][0] if c else None
        grid = out

    # Fill masked and unknown cells from their neighbours.
    while any(v is None for row in grid for v in row):
        out = [row[:] for row in grid]
        for y in range(gh):
            for x in range(gw):
                if grid[y][x] is None:
                    c = Counter(grid[yy][xx] for yy in range(max(0, y - 1), min(gh, y + 2))
                                for xx in range(max(0, x - 1), min(gw, x + 2)) if grid[yy][xx])
                    if c:
                        out[y][x] = c.most_common(1)[0][0]
        if out == grid:
            for row in out:
                for i, v in enumerate(row):
                    if v is None:
                        row[i] = "deep_ocean"
        grid = out

    # Water near land is shallow sea; the rest is deep ocean.
    land = [[grid[y][x] != "water" for x in range(gw)] for y in range(gh)]
    dist = [[0 if land[y][x] else 99 for x in range(gw)] for y in range(gh)]
    for _ in range(6):
        for y in range(gh):
            for x in range(gw):
                if dist[y][x]:
                    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                        xx, yy = x + dx, y + dy
                        if 0 <= xx < gw and 0 <= yy < gh:
                            dist[y][x] = min(dist[y][x], dist[yy][xx] + 1)
    for y in range(gh):
        for x in range(gw):
            if grid[y][x] == "water":
                grid[y][x] = "shallow_sea" if dist[y][x] <= 4 else "deep_ocean"
            elif grid[y][x] in ("grassland", "forest", "desert") and dist[y][x] == 1:
                grid[y][x] = "coast"

    out = Image.new("RGB", (gw, gh))
    for y in range(gh):
        for x in range(gw):
            out.putpixel((x, y), colors[grid[y][x]])
    out.save(OUT)

    os.makedirs(os.path.dirname(PREVIEW), exist_ok=True)
    pv = Image.new("RGB", (w * 2 // 2 + w // 2 * 2, h // 2 * 1))
    half = art.resize((w // 2, h // 2))
    pv = Image.new("RGB", (w, h // 2))
    pv.paste(half, (0, 0))
    pv.paste(out.resize((w // 2, h // 2), Image.NEAREST), (w // 2, 0))
    pv.save(PREVIEW)
    cells_to_text(OUT)
    counts = Counter(v for row in grid for v in row)
    print("wrote", OUT, f"{gw}x{gh}", dict(counts))


if __name__ == "__main__":
    sys.exit(main())
