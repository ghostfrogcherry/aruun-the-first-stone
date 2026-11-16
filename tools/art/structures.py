"""Starter buildings and trees. These are stamped into the destructible terrain, so:
- voxels are terrain voxels (0.25 m), and
- every colour must be the colour of a block in content/blocks/blocks.json.
Origin is the centre of the floor; y = 0 is the floor layer and negative y is foundation."""
import math

from voxlib import Vox, flat, mix, _hash

# block key -> palette key. Keep in step with content/blocks/blocks.json (the validator checks).
BLOCK = {
    "red_soil": "rust_4", "rust_rock": "rust_3", "deep_stone": "stone_2", "moss": "green_3", "dust_sand": "sand_1",
    "ancient_stone": "stone_4", "inscribed_stone": "glow_teal", "timber": "wood_3", "grass": "green_4",
    "tall_grass": "green_5", "leaves": "green_2", "crimson_leaves": "crimson_3", "bark": "wood_2", "water": "water_3",
    "gold_flower": "cloth_mustard", "white_flower": "sandstone_5", "grinleaf": "herb_1",
    # appended by the art pipeline
    "sandstone": "sandstone_3", "sandstone_dark": "sandstone_2", "plaster": "sandstone_4", "clay_tile": "rust_5",
    "paving": "stone_5", "basalt": "stone_1", "plank": "wood_4", "thatch": "sand_2", "violet_leaves": "violet_3",
    "bronze": "bronze_2", "awning_cloth": "cloth_wine", "lamp_glow": "glow_lamp",
}


def B(key):
    return flat(BLOCK[key])


def _foundation(v, r, depth=8, key="sandstone_dark"):
    v.cylinder(0, 0, -depth, -1, r, r, B(key))


def vaari_dome_house():
    """Round riverside house: sandstone and plaster walls under a clay-tile dome. 7 m across."""
    v = Vox()
    R, H = 14, 15
    _foundation(v, R + 1)
    v.cylinder(0, 0, 0, 0, R + 1, R + 1, B("sandstone_dark"))          # plinth
    v.cylinder(0, 0, 0, 0, R - 2, R - 2, B("paving"))                   # floor
    v.cylinder(0, 0, 1, H, R, R, B("plaster"), shell=2.0)
    v.cylinder(0, 0, 1, 3, R, R, B("sandstone"), shell=2.0)
    v.cylinder(0, 0, H - 1, H, R + 0.6, R + 0.6, B("sandstone_dark"), shell=2.6)   # cornice
    v.repaint(B("sandstone"), lambda x, y, z: 3 < y < H - 1 and (math.atan2(z + 0.5, x + 0.5) * 8 / math.pi) % 4 < 0.5)  # pilasters
    v.ellipsoid(0, H + 0.5, 0, R + 0.2, 10.5, R + 0.2, B("clay_tile"), shell=1.6, ymin=H + 1)   # dome
    v.cylinder(0, 0, H + 10, H + 11, 2.2, 1.4, B("sandstone_dark"))
    v.box(-1, H + 12, -1, 0, H + 15, 0, B("bronze"))
    # door (front, -z) with a dark surround, and a lamp beside it
    for y in range(1, 12):
        half = 3.5 if y < 8 else math.sqrt(max(0.0, 3.5 ** 2 - (y - 7.5) ** 2))
        for x in range(-5, 5):
            for z in range(-R - 1, -R + 4):
                if abs(x + 0.5) <= half:
                    v.erase(x, y, z)
                elif abs(x + 0.5) <= half + 1.2 and v.has(x, y, z) and y < 13:
                    v.set(x, y, z, B("sandstone_dark"))
    v.box(-4, 0, -R - 2, 3, 0, -R - 1, B("paving"))                    # doorstep
    v.box(6, 9, -R - 1, 6, 9, -R - 1, B("bronze"))
    v.box(6, 8, -R - 1, 6, 8, -R - 1, B("lamp_glow"))
    for s in (-1, 1):                                                  # side windows with plank shutters
        for z in range(-2, 2):
            for y in range(7, 11):
                for x in range(R - 3, R + 1):
                    v.erase(s * x if s > 0 else -x - 1, y, z)
        xs = R if s > 0 else -R - 1
        v.box(xs, 6, -3, xs, 6, 2, B("sandstone_dark"))
        v.box(xs, 7, -3, xs, 10, -3, B("plank"))
        v.box(xs, 7, 2, xs, 10, 2, B("plank"))
    # a bench and shelf inside so it reads as lived in
    v.box(-9, 1, 6, 8, 2, 9, B("plank"))
    v.box(-9, 1, 9, 8, 1, 9, B("timber"))
    return v


def watch_tower():
    """Tapering round tower with a lookout gallery, like the ones over the river towns. 15 m tall."""
    v = Vox()
    R0, R1, H = 9.0, 6.5, 48
    _foundation(v, R0 + 1, 10)
    v.cylinder(0, 0, 0, 1, R0 + 1.5, R0 + 1, B("sandstone_dark"))
    v.cylinder(0, 0, 0, 0, R0 - 2, R0 - 2, B("paving"))
    v.cylinder(0, 0, 1, H, R0, R1, B("sandstone"), shell=2.0)
    v.repaint(B("sandstone_dark"), lambda x, y, z: y % 12 in (0, 1) and y > 1)       # string courses
    v.repaint(B("plaster"), lambda x, y, z: y % 12 > 2 and _hash(x // 3, y // 2, z // 3, 2) < 0.14)   # worn plaster
    for y in range(2, H):                                             # spiral stair inside the wall
        a = y * 0.26
        r = R0 + (R1 - R0) * y / H - 2.0
        for k in range(-3, 4):
            aa = a + k * 0.07
            for rr in (r - 0.3, r - 1.3, r - 2.3):
                v.set(math.floor(math.cos(aa) * rr), y, math.floor(math.sin(aa) * rr), B("plank"))
    for y0, ang in ((16, 1.2), (28, 3.4), (38, 5.2), (22, 4.6)):      # slit windows
        r = R0 + (R1 - R0) * y0 / H
        for y in range(y0, y0 + 5):
            for rr in (r - 2.5, r - 1.5, r - 0.5, r + 0.5):
                v.erase(math.floor(math.cos(ang) * rr), y, math.floor(math.sin(ang) * rr))
    for y in range(1, 11):                                            # door (front, -z)
        half = 2.5 if y < 8 else math.sqrt(max(0.0, 2.5 ** 2 - (y - 7.5) ** 2))
        for x in range(-4, 4):
            if abs(x + 0.5) <= half:
                for z in range(-int(R0) - 2, -int(R0) + 4):
                    v.erase(x, y, z)
    # gallery: overhanging floor, parapet with gaps, small tiled dome on posts
    G = H + 1
    v.cylinder(0, 0, G - 2, G, R1 + 1.0, R1 + 3.2, B("sandstone_dark"))
    v.cylinder(0, 0, G - 2, G, 2.6, 2.6, None, carve=True)            # stair opening
    v.cylinder(0, 0, G + 1, G + 3, R1 + 3.2, R1 + 3.2, B("sandstone"), shell=1.5)
    for (x, y, z) in list(v.v):
        if y == G + 3 and int((math.atan2(z + 0.5, x + 0.5) + math.pi) * 14 / math.pi) % 2 == 0:
            v.erase(x, y, z)
    for i in range(6):
        a = i * math.tau / 6 + 0.26
        px, pz = math.floor(math.cos(a) * (R1 + 0.5)), math.floor(math.sin(a) * (R1 + 0.5))
        v.box(px, G + 1, pz, px, G + 9, pz, B("timber"))
    v.ellipsoid(0, G + 9.5, 0, R1 + 2.4, 7.0, R1 + 2.4, B("clay_tile"), shell=1.5, ymin=G + 10)
    v.box(-1, G + 16, -1, 0, G + 21, 0, B("bronze"))
    v.box(-1, G + 8, -1, 0, G + 8, 0, B("bronze"))                    # hanging signal lamp
    v.box(-1, G + 6, -1, 0, G + 7, 0, B("lamp_glow"))
    return v


def reed_hut():
    """Small round hut with a reed-thatch cone roof: fishers and herders. 4.5 m across."""
    v = Vox()
    R, H = 9, 9
    _foundation(v, R, 6, "rust_rock")
    v.cylinder(0, 0, 0, 0, R, R, B("plank"))
    v.cylinder(0, 0, 1, H, R, R, B("thatch"), shell=1.5)
    for i in range(10):                                               # timber posts
        a = i * math.tau / 10 + 0.31
        px, pz = math.floor(math.cos(a) * (R - 0.4)), math.floor(math.sin(a) * (R - 0.4))
        v.box(px, 1, pz, px, H, pz, B("timber"))
    v.cylinder(0, 0, H, H, R, R, B("timber"), shell=1.5)
    for y in range(1, 9):                                             # door (front, -z)
        for x in range(-3, 3):
            for z in range(-R - 1, -R + 3):
                v.erase(x, y, z)
    for y in range(H + 1, H + 13):                                    # cone roof with a deep eave
        t = (y - H - 1) / 11
        r = (R + 3.5) * (1 - t) + 0.8 * t
        v.cylinder(0, 0, y, y, r, r, mix(B("thatch"), B("dust_sand"), 0.25, y), shell=2.2 if t < 0.8 else 0.0)
    v.cylinder(0, 0, H + 13, H + 14, 0.9, 0.9, B("timber"))
    return v


def ruin_arch():
    """A broken gateway of the old builders: two pillars, part of an arch, moss, fallen blocks."""
    v = Vox()
    old = mix(B("ancient_stone"), B("paving"), 0.18, 1)
    for s, top in ((-1, 26), (1, 20)):
        cx = s * 9
        v.box(cx - 3, -6, -3, cx + 2, 1, 2, old)                       # footing
        v.box(cx - 2, 2, -2, cx + 1, top, 1, old)
        v.box(cx - 3, top - 5, -3, cx + 2, top - 4, 2, old)            # capital
    for i in range(0, 34):                                            # arch springing from the left pillar
        a = math.pi - i / 40 * math.pi
        x, y = math.cos(a) * 9, 26 + math.sin(a) * 9
        if i < 26:
            v.box(math.floor(x) - 1, math.floor(y) - 1, -2, math.floor(x) + 1, math.floor(y) + 1, 1, old)
    v.box(11, 1, -9, 14, 3, -6, old)                                   # fallen stones
    v.box(5, 1, 5, 9, 2, 8, old)
    v.box(13, 1, 4, 15, 1, 6, old)
    v.repaint(B("moss"), lambda x, y, z: not v.has(x, y + 1, z) and _hash(x, y, z, 3) < 0.45)
    v.repaint(B("moss"), lambda x, y, z: y < 6 and _hash(x, y // 2, z, 4) < 0.2)
    v.box(-10, 12, -3, -9, 16, -3, B("inscribed_stone"))               # a mark still lit on the tall pillar
    return v


def _canopy(v, cx, cy, cz, rx, ry, rz, key, light_key, seed):
    v.ellipsoid(cx, cy, cz, rx, ry, rz, mix(B(key), B(light_key), 0.3, seed), shell=2.2)
    for p in [p for p in v.v if _hash(p[0], p[1], p[2], seed + 1) < 0.06 and v.v[p] in (BLOCK[key], BLOCK[light_key])]:
        v.erase(*p)                                                   # a few gaps so it is not a solid ball


def skyroot_tree():
    """Flat-topped savannah giant with wide surface roots and crimson leaves. 12 m tall."""
    v = Vox()
    v.cylinder(0, 0, -6, 30, 4.2, 2.6, B("bark"))
    for i in range(7):                                                # root mats spreading over the soil
        a = i * math.tau / 7 + 0.2
        v.line((math.cos(a) * 3, 5, math.sin(a) * 3), (math.cos(a) * 13, -2, math.sin(a) * 13), 2.0, B("bark"), 1.0)
    limbs = [(0.4, 15, 40), (1.7, 17, 43), (2.9, 14, 39), (4.1, 18, 42), (5.3, 13, 44)]
    for i, (a, reach, top) in enumerate(limbs):
        ex, ez = math.cos(a) * reach, math.sin(a) * reach
        v.line((0, 26, 0), (ex * 0.5, top - 6, ez * 0.5), 1.8, B("bark"), 1.3)
        v.line((ex * 0.5, top - 6, ez * 0.5), (ex, top - 1, ez), 1.3, B("bark"), 0.9)
        _canopy(v, ex, top + 1, ez, 11, 3.6, 11, "crimson_leaves", "awning_cloth", i)
    _canopy(v, 0, 46, 0, 12, 4.0, 12, "crimson_leaves", "awning_cloth", 9)
    return v


def violet_tree():
    """Crimson-wood forest tree: tall dark trunk, clumped violet crown. 11 m tall."""
    v = Vox()
    v.cylinder(0, 0, -5, 30, 3.0, 1.8, B("bark"))
    v.cylinder(0, 0, -5, 2, 4.2, 3.0, B("bark"))
    for i, (a, reach, y) in enumerate([(0.5, 8, 30), (2.2, 9, 33), (4.0, 7, 28), (5.4, 8, 36), (1.3, 3, 40)]):
        ex, ez = math.cos(a) * reach, math.sin(a) * reach
        v.line((0, y - 6, 0), (ex, y, ez), 1.2, B("bark"), 0.8)
        _canopy(v, ex, y + 3, ez, 7.5, 6.0, 7.5, "violet_leaves", "crimson_leaves", 20 + i)
    return v


def green_tree():
    """Broadleaf river tree with a round green crown. 8 m tall."""
    v = Vox()
    v.cylinder(0, 0, -5, 18, 2.6, 1.6, B("bark"))
    for i, (a, reach, y) in enumerate([(0.9, 5, 20), (3.0, 6, 22), (4.9, 5, 19)]):
        ex, ez = math.cos(a) * reach, math.sin(a) * reach
        v.line((0, y - 5, 0), (ex, y, ez), 1.1, B("bark"), 0.8)
        _canopy(v, ex, y + 3, ez, 7.5, 6.0, 7.5, "leaves", "grass", 40 + i)
    _canopy(v, 0, 27, 0, 8.5, 6.5, 8.5, "leaves", "grass", 50)
    return v


STRUCTURES = {f.__name__: f for f in (vaari_dome_house, watch_tower, reed_hut, ruin_arch, skyroot_tree, violet_tree, green_tree)}
