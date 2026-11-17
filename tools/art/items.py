"""Starter items: tools, food, materials. Small models (6.25 cm voxels) for holding in the hand
and for rendering inventory icons. Tools stand upright with the origin at the grip and the
working edge facing forward (-z)."""
import math

from voxlib import Vox, flat, ramp, gradient, mix, _hash

FLINT = mix(flat("stone_1"), flat("stone_2"), 0.35, 1)
CORD = mix(flat("sandstone_3"), flat("sandstone_2"), 0.4, 2)


def _handle(v, top, wood=3, bottom=-4):
    v.box(-1, bottom, -1, 0, top, 0, ramp("wood", wood, 0.35, 4))


def flint_pick():
    v = Vox()
    _handle(v, 20)
    for z in range(-9, 8):          # curved head, pointed at both ends
        t = abs(z + 0.5) / 9
        y = 19 - int(round(3.5 * t * t))
        half = 1 if t < 0.75 else 0
        v.box(-1 if half else -1, y, z, 0 if half else -1 + 0, y + (1 if t < 0.6 else 0), z, FLINT)
    v.repaint(flat("stone_4"), lambda x, y, z: abs(z + 0.5) > 7.5)
    v.box(-2, 16, -2, 1, 18, 1, CORD, shell=True)
    return [("flint_pick", v)]


def flint_axe():
    v = Vox()
    _handle(v, 18)
    for z in range(-8, 2):          # wedge blade widening towards the edge
        t = (1 - z) / 9
        h = 2 + int(round(3.5 * t))
        v.box(-1, 15 - h // 2, z, 0, 15 + (h + 1) // 2, z, FLINT)
    v.repaint(flat("stone_4"), lambda x, y, z: z <= -8)
    v.repaint(flat("stone_3"), lambda x, y, z: z == -7 and y > 6)
    v.box(-2, 13, -1, 1, 17, 1, CORD, shell=True)
    return [("flint_axe", v)]


def reed_spear():
    v = Vox()
    v.box(0, -8, 0, 0, 26, 0, gradient("crimson", 1, 2, -8, 26, 0.4, 1))
    v.box(-1, 23, -1, 1, 25, 1, CORD, shell=True)
    for y in range(26, 33):
        half = 1 if y < 29 else 0
        v.box(-half, y, 0, half, y, 0, FLINT)
    v.set(0, 33, 0, flat("stone_4"))
    return [("reed_spear", v)]


def fallhammer():
    """Graviturge signature weapon: a heavy basalt head that hums with held weight."""
    v = Vox()
    v.box(-1, -5, -1, 0, 24, 0, ramp("wood", 1, 0.3, 2))
    v.box(-2, -6, -2, 1, -5, 1, flat("bronze_2"))
    for y in (2, 9):
        v.box(-2, y, -2, 1, y, 1, flat("bronze_2"), shell=True)
    v.box(-4, 19, -8, 3, 28, 7, ramp("stone", 1, 0.35, 5))
    for z in (-8, 7):               # chamfer the striking faces
        for (x, y) in ((-4, 19), (3, 19), (-4, 28), (3, 28)):
            v.erase(x, y, z)
    for z in (-6, 5):
        v.repaint(flat("bronze_2"), lambda x, y, zz, z=z: zz in (z, z) and y >= 19 and (x in (-4, 3) or y in (19, 28)))
    v.repaint(flat("glow_violet"), lambda x, y, z: y >= 19 and x in (-4, 3) and abs(z + 0.5) < 3 and (y in (23, 24) or abs(z + 0.5) < 1))
    v.repaint(flat("glow_teal"), lambda x, y, z: y == 28 and abs(z + 0.5) < 1 and abs(x + 0.5) < 2)
    return [("fallhammer", v)]


def dust_mask():
    v = Vox()
    v.ellipsoid(0, 0, 0, 5.2, 4.2, 4.0, mix(flat("cloth_linen"), flat("sandstone_4"), 0.3, 1), shell=1.3)
    v.v = {p: k for p, k in v.v.items() if p[2] < 0 and p[1] > -4}      # keep the front shell
    v.cylinder(0, 0, -1, 0, 1, 1, None, carve=True)
    for (x, y) in ((-1, -1), (0, -1), (-1, 0), (0, 0), (-2, -1), (1, -1), (-1, -2), (0, -2)):
        v.set(x, y, -5, flat("bronze_2"))                                 # filter disc
    v.set(-1, -1, -6, flat("bronze_1"))
    v.set(0, -1, -6, flat("bronze_1"))
    for s in (-1, 1):                                                     # straps
        v.line((s * 5, 0.5, -1), (s * 6.5, 1.5, 4), 0.5, flat("wood_2"))
    return [("dust_mask", v)]


def moss_bandage():
    v = Vox()
    for x in range(-4, 4):
        for y in range(0, 6):
            for z in range(-3, 3):
                d = math.hypot(y + 0.5 - 3, z + 0.5)
                if d <= 3:
                    v.set(x, y, z, flat("sandstone_5" if int(d * 1.5) % 2 == 0 else "cloth_linen"))
    v.repaint(ramp("green", 4, 0.6, 3), lambda x, y, z: -2 <= x <= 1 and math.hypot(y - 2.5, z + 0.5) > 2.0)
    v.box(4, 0, -2, 7, 0, 1, flat("cloth_linen"))                          # loose end
    return [("moss_bandage", v)]


def grinleaf_roll():
    v = Vox()
    v.line((-5, 0.5, 0.5), (4, 0.5, 0.5), 0.5, flat("sandstone_4"))
    v.box(-5, 0, -1, 4, 1, 0, mix(flat("sandstone_4"), flat("herb_1"), 0.25, 1))
    v.box(5, 0, -1, 5, 1, 0, flat("herb_1"))
    v.box(-6, 0, -1, -6, 1, 0, flat("glow_ember"))
    v.box(-7, 0, -1, -7, 1, 0, flat("stone_4"))
    return [("grinleaf_roll", v)]


def _root(v, colour):
    for y in range(0, 12):
        r = 0.5 + 2.6 * math.sin(min(1.0, (y + 1) / 9.0) * math.pi * 0.62)
        v.cylinder(0, 0, y, y, r, r, colour)


def redroot():
    v = Vox()
    _root(v, gradient("crimson", 2, 4, 0, 11, 0.3, 1))
    for i in range(4):
        a = i * math.tau / 4 + 0.5
        v.line((0, 11, 0), (math.cos(a) * 2.5, 15, math.sin(a) * 2.5), 0.5, flat("green_4" if i % 2 else "green_5"))
    return [("redroot", v)]


def roast_redroot():
    v = Vox()
    _root(v, mix(gradient("rust", 2, 4, 0, 11, 0.3, 1), flat("ink_1"), 0.12, 2))
    v.repaint(flat("rust_6"), lambda x, y, z: 4 <= y <= 6 and z < 0 and x == -1)     # split skin
    return [("roast_redroot", v)]


def _meat(v, lean, fat):
    v.ellipsoid(0, 3, 0, 4.2, 3.2, 3.4, lean, ymin=0)
    v.repaint(fat, lambda x, y, z: _hash(x // 2, y // 2, z // 2, 6) < 0.16)
    v.line((3, 3.5, 0.5), (7.5, 5, 0.5), 0.9, flat("sandstone_5"))
    v.ellipsoid(8, 5.3, 0.5, 1.4, 1.4, 1.6, flat("sandstone_5"))


def raw_meat():
    v = Vox()
    _meat(v, ramp("crimson", 4, 0.4, 1), flat("hide_4"))
    return [("raw_meat", v)]


def roast_meat():
    v = Vox()
    _meat(v, ramp("rust", 3, 0.5, 1), flat("wood_1"))
    return [("roast_meat", v)]


def glasswort_leaf():
    v = Vox()
    v.line((0, 0, 0), (1.5, 11, 0.5), 1.5, gradient("water", 2, 4, 0, 11, 0.2, 1), 0.4)
    v.set(1, 12, 0, flat("sandstone_5"))
    return [("glasswort_leaf", v)]


def flint():
    v = Vox()
    v.ellipsoid(0, 2, 0, 3.6, 2.6, 2.8, FLINT, ymin=0)
    v.carve_box(1, 3, -3, 4, 5, 3)
    v.repaint(flat("stone_4"), lambda x, y, z: x >= 0 and y == 2 and v.has(x, y, z) and not v.has(x, y + 1, z))   # fresh break
    v.repaint(flat("stone_5"), lambda x, y, z: x < -2 and _hash(x, y, z, 2) < 0.5)                                   # chalky rind
    return [("flint", v)]


def flint_nodule():
    """Harvest node: a chalky boulder with dark flint showing through."""
    v = Vox()
    v.ellipsoid(0, 4, 0, 8.5, 6.5, 7.0, ramp("stone", 5, 0.5, 1), ymin=0)
    v.ellipsoid(5, 2.5, 5, 4.5, 3.8, 4.2, ramp("stone", 5, 0.5, 2), ymin=0)
    v.repaint(FLINT, lambda x, y, z: math.sin(x * 0.7 + y * 1.3) + math.sin(z * 0.9 - y * 0.5) > 1.15)
    v.repaint(ramp("green", 3, 0.5, 4), lambda x, y, z: y < 2 and _hash(x, y, z, 5) < 0.25)
    return [("flint_nodule", v)]


def reed_stalk():
    v = Vox()
    for i, (x, z) in enumerate(((0, 0), (1, 1), (-1, 1))):
        v.box(x, 0, z, x, 20 - i * 2, z, gradient("crimson", 2, 5, 0, 20, 0.3, i))
    v.box(-2, 6, -1, 2, 7, 2, CORD, shell=True)
    return [("reed_stalk", v)]


def cord():
    v = Vox()
    for y in range(0, 3):
        v.cylinder(0, 0, y, y, 4.6 - (y == 2) * 0.6, 4.6 - (y == 2) * 0.6, CORD)
    v.cylinder(0, 0, 0, 2, 2.2, 2.2, None, carve=True)
    v.line((4, 0.5, 1), (7, 0.5, 4), 0.5, flat("sandstone_3"))
    return [("cord", v)]


def hide():
    v = Vox()
    skin = mix(ramp("hide", 2, 0.4, 1), flat("rust_3"), 0.15, 2)
    v.box(-6, 0, -4, 5, 1, 3, skin)
    v.box(-6, 2, -4, 5, 3, -1, skin)                                        # folded over
    v.repaint(flat("hide_4"), lambda x, y, z: y == 1 and z > -1)             # pale underside showing
    for (x, z) in ((-6, 3), (5, 3), (-6, -4), (5, -4)):
        v.erase(x, 0, z), v.erase(x, 1, z), v.erase(x, 2, z), v.erase(x, 3, z)
    return [("hide", v)]


def seeds():
    v = Vox()
    v.ellipsoid(0, 3, 0, 3.6, 3.6, 3.2, mix(flat("cloth_linen"), flat("sandstone_4"), 0.3, 1), ymin=0)
    v.box(-2, 6, -1, 1, 6, 0, flat("wood_2"))                                # tie
    v.box(-2, 7, -2, 1, 8, 1, flat("cloth_linen"))
    for i, (x, z) in enumerate(((5, 1), (6, -1), (4, 3), (7, 2), (5, -3))):
        v.set(x, 0, z, flat("wood_4" if i % 2 else "rust_4"))
    return [("seeds", v)]


def plant_fibre():
    v = Vox()
    for i, (x, z, top) in enumerate(((-2, 0, 15), (-1, 1, 17), (0, 0, 18), (1, 1, 16), (2, 0, 14), (0, -1, 16))):
        v.line((x, 0, z), (x * 2.2, top, z * 1.5), 0.5, gradient("green", 3, 5, 0, 18, 0.35, i))
    v.box(-2, 5, -1, 2, 6, 1, CORD, shell=True)
    return [("plant_fibre", v)]


def grinleaf_bud():
    v = Vox()
    v.box(0, 0, 0, 0, 3, 0, flat("green_2"))
    v.ellipsoid(0.5, 8, 0.5, 3.4, 5.2, 3.2, mix(ramp("green", 4, 0.5, 1), flat("herb_1"), 0.3, 2), ymin=3)
    v.repaint(flat("glow_ember"), lambda x, y, z: _hash(x, y, z, 7) < 0.1)        # orange hairs
    v.line((0, 4, 0), (-4, 6, 1), 0.5, flat("green_3"))
    v.line((0, 4, 0), (5, 6, -1), 0.5, flat("green_3"))
    return [("grinleaf_bud", v)]


def _seed_pile(colour, alt, n=7, seed=0):
    v = Vox()
    for i in range(n):
        a = i * 2.4 + seed
        r = 0 if i == 0 else 2.6 + (i % 2)
        x, z = int(round(math.cos(a) * r)), int(round(math.sin(a) * r))
        v.ellipsoid(x + 0.5, 1, z + 0.5, 1.4, 1.1, 1.0, flat(colour if i % 3 else alt), ymin=0)
    return v


def redroot_seed():
    return [("redroot_seed", _seed_pile("crimson_2", "crimson_4", seed=1))]


def grinleaf_seed():
    return [("grinleaf_seed", _seed_pile("wood_3", "green_2", seed=2))]


def glasswort_cutting():
    v = Vox()
    v.line((0, 0, 0), (0, 9, 0), 1.2, gradient("water", 2, 4, 0, 12, 0.2, 1), 0.7)
    v.line((0, 5, 0), (4, 11, 1), 0.9, gradient("water", 2, 4, 0, 12, 0.2, 2), 0.75)
    v.line((0, 4, 0), (-3, 8, -1), 0.8, gradient("water", 2, 4, 0, 12, 0.2, 3), 0.75)
    v.box(-1, 0, -1, 0, 0, 0, flat("sandstone_5"))                                 # cut end
    return [("glasswort_cutting", v)]


def reed_shoot():
    v = Vox()
    v.ellipsoid(0, 1, 0, 2.6, 2.2, 2.2, ramp("wood", 3, 0.4, 1), ymin=0)           # root ball
    for i, (x, z, top) in enumerate(((0, 0, 14), (1, 0, 10), (-1, 1, 8))):
        v.box(x, 2, z, x, top, z, gradient("crimson", 2, 5, 2, 14, 0.3, i))
    v.set(0, 15, 0, flat("crimson_5"))
    return [("reed_shoot", v)]


def campfire_kit():
    """Bundled firewood with a flint on top: the campfire before it is placed."""
    v = Vox()
    for i, (x, y) in enumerate(((-3, 0), (0, 0), (3, 0), (-1, 3), (2, 3))):
        v.box(x - 1, y, -7, x + 1, y + 2, 6, ramp("wood", 2 + i % 2, 0.4, i))
        v.box(x, y + 1, -7, x, y + 1, -7, flat("wood_4"))
        v.box(x, y + 1, 6, x, y + 1, 6, flat("wood_4"))
    for z in (-4, 3):
        v.box(-5, -1, z, 5, 6, z, CORD, shell=True)
    v.ellipsoid(0.5, 7.5, 0, 2.2, 1.6, 1.8, FLINT, ymin=6)
    return [("campfire_kit", v)]


def crystal_shard():
    """Glassback shell crystal."""
    v = Vox()
    for i, (x, z, h, r) in enumerate(((0, 0, 14, 2.0), (3, 1, 9, 1.5), (-2, 2, 7, 1.3), (1, -3, 6, 1.2))):
        v.cylinder(x + 0.5, z + 0.5, 0, h, r, 0.75, gradient("water", 1, 4, 0, 14, 0.2, i))
        v.set(x, h, z, flat("sandstone_5"))
    return [("crystal_shard", v)]


def wing_dust():
    """Duskmoth scales in a clay dish."""
    v = Vox()
    v.cylinder(0, 0, 0, 3, 3.6, 5.2, ramp("rust", 3, 0.3, 1))
    v.cylinder(0, 0, 1, 3, 2.8, 4.4, None, carve=True)
    v.cylinder(0, 0, 1, 2, 2.8, 3.6, mix(flat("dusk_2"), flat("violet_3"), 0.35, 3))
    v.repaint(flat("glow_lamp"), lambda x, y, z: y == 2 and _hash(x, y, z, 4) < 0.22)
    return [("wing_dust", v)]


def ancient_stone():
    """Dressed block from an old-builder ruin, with one inscribed line."""
    v = Vox()
    v.box(-5, 0, -4, 4, 7, 3, ramp("stone", 4, 0.45, 6))
    v.carve_box(3, 6, -4, 4, 7, -2)                                                # chipped corner
    v.repaint(ramp("green", 3, 0.5, 2), lambda x, y, z: y == 7 and _hash(x, y, z, 3) < 0.35)
    for z in (3, -4):                                                              # same line on both long faces
        for x in (-3, -2, 0, 1, 2):
            v.set(x, 4, z, flat("glow_teal"))
        v.set(-2, 3, z, flat("glow_teal")), v.set(1, 5, z, flat("glow_teal"))
    return [("ancient_stone", v)]


def bronze_fitting():
    """Old-builder clamp: a bronze band with two rivets."""
    v = Vox()
    v.box(-5, 0, -2, 4, 5, 1, ramp("bronze", 2, 0.4, 1), shell=True)
    v.carve_box(-4, 1, -2, 3, 4, 1)
    v.carve_box(-1, 5, -2, 0, 5, 1)                                                # the open gap
    for x in (-4, 3):
        v.box(x, 6, -1, x, 6, 0, flat("bronze_3"))
    v.repaint(flat("green_4"), lambda x, y, z: _hash(x, y, z, 9) < 0.12)           # verdigris
    return [("bronze_fitting", v)]


def word_fragment():
    """A shard of inscribed slate holding part of an Avarra word."""
    v = Vox()
    for y in range(0, 13):
        w = 5 - abs(y - 5) // 2 - (1 if y > 9 else 0)
        v.box(-w, y, 0, w - (y % 3 == 0), y, 1, ramp("stone", 2, 0.3, 8))
    for (x, y) in ((-2, 8), (-1, 8), (0, 8), (1, 8), (-1, 7), (-1, 6), (-1, 5), (1, 6), (1, 5), (2, 5), (0, 3), (1, 3), (-2, 3)):
        v.set(x, y, 1, flat("glow_teal")), v.set(x, y, 0, flat("glow_teal"))
    return [("word_fragment", v)]


ITEMS = {f.__name__: f for f in (flint_pick, flint_axe, reed_spear, fallhammer, dust_mask, moss_bandage, grinleaf_roll,
                                 redroot, roast_redroot, raw_meat, roast_meat, glasswort_leaf, flint, reed_stalk,
                                 cord, hide, seeds, plant_fibre, grinleaf_bud, redroot_seed, grinleaf_seed,
                                 glasswort_cutting, reed_shoot, campfire_kit, crystal_shard, wing_dust,
                                 ancient_stone, bronze_fitting, word_fragment)}
