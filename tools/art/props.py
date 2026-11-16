"""Starter props. Model voxels are 6.25 cm (16 per metre); origin is ground centre."""
import math

from voxlib import Vox, flat, ramp, gradient, mix, _hash


def clay_jar():
    v = Vox()
    clay = ramp("rust", 5, 0.12, 1)
    v.cylinder(0, 0, 0, 1, 3.0, 3.6, clay)
    v.ellipsoid(0, 7.5, 0, 5.4, 6.6, 5.4, clay)
    v.cylinder(0, 0, 13, 15, 2.7, 2.7, clay)
    v.cylinder(0, 0, 16, 16, 3.7, 3.7, flat("rust_6"))
    # painted band: a pale zigzag between two dark lines
    v.repaint(flat("rust_3"), lambda x, y, z: y in (6, 10))
    v.repaint(flat("sandstone_4"), lambda x, y, z: y in (7, 8, 9) and (x + z + (y - 7)) % 4 < 2)
    v.cylinder(0, 0, 14, 16, 1.7, 1.7, None, carve=True)
    v.cylinder(0, 0, 13, 13, 1.7, 1.7, flat("ink_1"))
    for s in (-1, 1):  # handles
        v.line((s * 3.2, 14.5, 0.5), (s * 5.5, 13.5, 0.5), 0.5, flat("rust_4"))
        v.line((s * 5.5, 13.5, 0.5), (s * 5.2, 10.5, 0.5), 0.5, flat("rust_4"))
    return [("clay_jar", v)]


def clay_pots():
    v = Vox()
    for i, (cx, cz, r, h, lvl) in enumerate([(-5, -2, 3.6, 7, 5), (3, -4, 2.6, 5, 4), (4, 4, 4.2, 4, 6), (-3, 5, 2.2, 6, 5)]):
        m = ramp("rust", lvl, 0.25, 10 + i)
        v.ellipsoid(cx, h / 2 + 0.2, cz, r, h / 2 + 0.6, r, m, ymin=0)
        v.cylinder(cx, cz, h, h, r - 0.6, r - 0.6, flat("rust_6" if lvl < 6 else "sandstone_4"))
        v.cylinder(cx, cz, h, h, r - 1.5, r - 1.5, flat("ink_1"))
    # the wide bowl holds red berries
    v.cylinder(4, 4, 4, 4, 2.9, 2.9, mix(flat("crimson_4"), flat("crimson_2"), 0.35, 3))
    return [("clay_pots", v)]


def lamp_post():
    v = Vox()
    v.cylinder(0, 0, 0, 2, 3.6, 3.0, ramp("stone", 3, 0.4, 2))
    v.box(-1, 3, -1, 0, 42, 0, ramp("wood", 2, 0.4, 3))
    v.box(-2, 20, -2, 1, 21, 1, flat("bronze_2"))
    v.box(-2, 41, -2, 1, 42, 1, flat("bronze_2"))
    # curved arm reaching forward (-z)
    v.line((-0.5, 42, -1), (-0.5, 46, -5), 0.9, flat("wood_3"))
    v.line((-0.5, 46, -5), (-0.5, 45, -10), 0.9, flat("wood_3"))
    v.box(-1, 41, -10, 0, 44, -10 + 0, flat("bronze_1"))  # chain
    # lantern
    v.box(-3, 34, -12, 2, 34, -7, flat("bronze_2"))
    v.box(-3, 40, -12, 2, 40, -7, flat("bronze_2"))
    v.box(-2, 41, -11, 1, 41, -8, flat("bronze_3"))
    v.box(-2, 35, -11, 1, 39, -8, flat("glow_lamp"))
    for x in (-3, 2):
        for z in (-12, -7):
            v.box(x, 35, z, x, 39, z, flat("bronze_1"))
    return [("lamp_post", v)]


def tether_stone():
    """The stone a homestead is planted around: old stone, bronze band, a lit Avarra-style mark."""
    v = Vox()
    stone = ramp("stone", 4, 0.45, 5)
    for y in range(0, 27):
        t = y / 26
        hw = 4.6 - 1.4 * t + 0.5 * math.sin(y * 0.6)
        hd = 2.8 - 0.8 * t
        lean = int(round(0.06 * y))
        for x in range(-6, 6):
            for z in range(-4, 4):
                if abs(x + 0.5 - lean * 0.0) <= hw and abs(z + 0.5) <= hd:
                    if y > 23 and abs(x + 0.5) > hw - (y - 23) * 0.9:
                        continue
                    v.set(x, y, z, stone)
    v.repaint(flat("bronze_2"), lambda x, y, z: y in (5, 6))
    v.repaint(flat("bronze_3"), lambda x, y, z: y == 6 and (x + z) % 3 == 0)
    # glyph cut into the front face (z = -3 is the front)
    front = min(z for (_, _, z) in v.v)
    glyph = [(0, y) for y in range(9, 22)] + [(-1, 19), (-2, 20), (1, 16), (2, 17), (2, 18), (-1, 12), (-2, 12), (1, 10)]
    for gx, gy in glyph:
        for z in range(front, front + 2):
            if v.has(gx, gy, z) or v.has(gx, gy, z + 1):
                v.set(gx, gy, z + (0 if v.has(gx, gy, z) else 1), flat("glow_teal"))
                break
    # footing stones and a little moss
    for i in range(9):
        a = i * math.tau / 9 + 0.3
        r = 6.2 + (i % 3) * 0.6
        v.ellipsoid(math.cos(a) * r, 0.8, math.sin(a) * r * 0.8, 1.7, 1.5, 1.7, ramp("stone", 3, 0.5, 20 + i), ymin=0)
    v.repaint(ramp("green", 3, 0.5, 4), lambda x, y, z: y <= 3 and _hash(x, y, z, 9) < 0.12)
    return [("tether_stone", v)]


def campfire():
    v = Vox()
    for i in range(10):
        a = i * math.tau / 10
        v.ellipsoid(math.cos(a) * 6.5, 1.0, math.sin(a) * 6.5, 1.9, 1.7 + (i % 2) * 0.4, 1.9, ramp("stone", 3 + i % 2, 0.4, i), ymin=0)
    v.cylinder(0, 0, 0, 0, 4.6, 4.6, mix(flat("ink_1"), flat("stone_1"), 0.4, 1))
    for i in range(4):
        a = i * math.tau / 4 + 0.5
        v.line((math.cos(a) * 4.2, 1.0, math.sin(a) * 4.2), (-math.cos(a) * 1.0, 5.5, -math.sin(a) * 1.0), 0.95, ramp("wood", 1, 0.5, 30 + i))
    v.ellipsoid(0, 2.0, 0, 2.6, 2.2, 2.6, mix(flat("glow_ember"), flat("glow_lamp"), 0.35, 5), ymin=1)
    for (x, y, z) in ((0, 5, 0), (-1, 6, 0), (0, 7, -1), (0, 8, 0)):
        v.set(x, y, z, flat("glow_ember" if y < 7 else "glow_lamp"))
    return [("campfire", v)]


def supplies():
    v = Vox()
    plank = ramp("wood", 4, 0.5, 7)
    v.box(-9, 0, -6, 0, 9, 3, plank)
    v.repaint(flat("wood_2"), lambda x, y, z: x <= 0 and sum(c in e for c, e in ((x, (-9, 0)), (y, (0, 9)), (z, (-6, 3)))) >= 2)
    v.repaint(flat("wood_3"), lambda x, y, z: x <= 0 and y in (3, 6) and v.v[(x, y, z)] != "wood_2")
    v.box(-6, 10, -4, -3, 10, 1, flat("cloth_slate"))  # folded cloth on top
    v.box(-6, 11, -4, -3, 11, 0, flat("cloth_mustard"))
    sack = mix(flat("cloth_linen"), flat("sandstone_4"), 0.3, 4)
    v.ellipsoid(6, 4.5, -1, 4.6, 5.4, 4.2, sack, ymin=0)
    v.ellipsoid(6, 10.2, -1, 1.6, 1.4, 1.6, sack)
    v.repaint(flat("wood_2"), lambda x, y, z: x > 0 and y == 9)
    v.ellipsoid(4, 2.6, 6, 3.4, 3.2, 3.4, sack, ymin=0)
    return [("supplies", v)]


def bench():
    v = Vox()
    for x in (-10, 7):
        v.box(x, 0, -3, x + 2, 5, 2, ramp("stone", 4, 0.5, x))
    v.box(-13, 6, -4, 12, 7, 3, ramp("wood", 3, 0.5, 2))
    v.repaint(flat("wood_2"), lambda x, y, z: y >= 6 and z in (-2, 1))
    return [("bench", v)]


def waystone():
    """Road marker for the caravan routes."""
    v = Vox()
    v.box(-4, 0, -4, 3, 1, 3, ramp("stone", 3, 0.5, 1))
    for y in range(2, 20):
        r = 3 if y < 15 else max(0, 3 - (y - 15))
        v.box(-r, y, -r, r - 1, y, r - 1, ramp("sandstone", 3, 0.5, 2)) if r > 0 else None
    v.repaint(flat("sandstone_1"), lambda x, y, z: y in (2, 13))
    v.repaint(flat("bronze_2"), lambda x, y, z: y == 14)
    v.box(-1, 18, -1, 0, 20, 0, flat("bronze_3"))
    for y, xs in ((6, (-1, 0)), (7, (-2, 1)), (8, (-1, 0)), (9, (-1,)), (10, (-1, 0)), (11, (0,))):
        for x in xs:
            v.set(x, y, -3, flat("glow_teal"))
    return [("waystone", v)]


def market_stall():
    v = Vox()
    post = ramp("wood", 2, 0.4, 1)
    for x in (-18, 16):
        v.box(x, 0, -11, x + 1, 30, -10, post)   # front posts (short)
        v.box(x, 0, 10, x + 1, 36, 11, post)     # back posts (tall)
    # sloped awning with a sag and stripes
    for x in range(-20, 20):
        for z in range(-14, 14):
            y = 31 + (z + 14) * 6.0 / 27.0 - 1.6 * math.sin((x + 20) / 39.0 * math.pi)
            key = "cloth_mustard" if (x + 20) % 10 < 2 else "cloth_wine"
            v.set(x, int(round(y)), z, mix(flat(key), flat("rust_2" if key == "cloth_wine" else "bronze_2"), 0.12, 6))
            if z == -14 and (x + 20) % 4 < 2:
                v.set(x, int(round(y)) - 1, z, flat(key))   # scalloped front edge
    # table
    v.box(-17, 12, -9, 16, 13, 2, ramp("wood", 4, 0.5, 2))
    for x in (-16, 14):
        for z in (-8, 1):
            v.box(x, 0, z, x + 1, 11, z, post)
    # goods: pots, red fruit, cloth bolts, a bronze dish
    for i, (cx, cz, r, h) in enumerate([(-13, -5, 2.4, 4), (-8, -3, 1.9, 5), (-12, 0, 1.6, 3)]):
        v.ellipsoid(cx, 14 + h / 2, cz, r, h / 2 + 0.6, r, ramp("rust", 5, 0.3, i), ymin=14)
    v.box(-4, 14, -7, 3, 14, -1, flat("wood_2"))
    for i in range(14):
        fx, fz = -4 + int(_hash(i, 1, 0) * 8), -7 + int(_hash(i, 2, 0) * 7)
        v.set(fx, 15, fz, flat("crimson_4" if i % 3 else "crimson_5"))
        if i % 4 == 0:
            v.set(fx, 16, fz, flat("crimson_3"))
    for i, key in enumerate(("cloth_slate", "cloth_linen", "cloth_mustard", "violet_3")):
        v.box(7, 14 + i * 2, -7, 14, 15 + i * 2, -2 - (i % 2), flat(key))
    v.cylinder(10, 0 - 0.5, 14, 14, 2.6, 2.6, flat("bronze_3"))
    # crate and jar under the table
    v.box(-12, 0, -6, -5, 6, 0, ramp("wood", 3, 0.5, 9))
    v.ellipsoid(8, 4, -3, 3.4, 4.6, 3.4, ramp("rust", 4, 0.3, 4), ymin=0)
    return [("market_stall", v)]


def river_skiff():
    v = Vox()
    plank = ramp("wood", 3, 0.22, 3)
    L, B, H = 30.0, 7.5, 8
    for y in range(0, H + 1):
        for z in range(-34, 34):
            t = abs(z + 0.5) / L
            if t >= 1.0:
                continue
            sheer = 4.5 * t ** 3                       # ends rise
            yy = y - sheer
            if yy < 0 or yy > H - 1 and t < 0.75:
                continue
            half = B * (1 - t ** 2.2) * (0.55 + 0.45 * min(1.0, (yy + 1) / (H - 1)))
            for x in range(-9, 9):
                d = abs(x + 0.5)
                if d <= half and (d > half - 1.2 or yy < 1.0):
                    v.set(x, y, z, plank)
    top = {}
    for (x, y, z) in v.v:
        top[(x, z)] = max(top.get((x, z), -1), y)
    for (x, z), y in top.items():   # darker gunwale
        if not v.has(x, y + 1, z) and (not v.has(x - 1, y, z) or not v.has(x + 1, y, z) or abs(z) > 27):
            v.set(x, y, z, flat("wood_1"))
    for z in (-16, -2, 12):          # thwarts
        for x in range(-8, 8):
            if any(v.has(xx, 5, z) for xx in range(x, 9)) and any(v.has(xx, 5, z) for xx in range(-9, x + 1)):
                v.box(x, 5, z, x, 5, z + 2, flat("wood_4"))
    v.line((-3.5, 6.5, -20), (4.5, 8.5, 22), 0.5, flat("wood_4"))        # pole
    v.ellipsoid(1.5, 3.2, 20, 3.4, 2.6, 3.6, mix(flat("cloth_linen"), flat("sandstone_4"), 0.3, 1), ymin=1)
    v.ellipsoid(-2, 3.0, -9, 2.4, 3.0, 2.4, ramp("rust", 5, 0.3, 2), ymin=1)
    return [("river_skiff", v)]


PROPS = {f.__name__: f for f in (clay_jar, clay_pots, lamp_post, tether_stone, campfire, supplies, bench,
                                 waystone, market_stall, river_skiff)}
