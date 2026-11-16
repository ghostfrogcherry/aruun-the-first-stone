"""Starter plants, after the person's "Flora of Mars" field guide (names are provisional).
Model voxels are 6.25 cm; origin is ground centre."""
import math

from voxlib import Vox, flat, ramp, gradient, mix, _hash
import palette as P


def _blade(v, x, z, angle, length, lean, mat_fn, droop=0.0, width=0.5):
    """A curved stem from the ground: leans outward more the higher it goes."""
    px, py, pz = float(x), 0.0, float(z)
    tilt = lean * 0.25
    for i in range(int(length)):
        t = i / max(1.0, length - 1)
        tilt = lean * (0.25 + 0.75 * t) + droop * t * t
        nx, ny, nz = px + math.cos(angle) * math.sin(tilt), py + math.cos(tilt), pz + math.sin(angle) * math.sin(tilt)
        v.line((px, py, pz), (nx, ny, nz), width, lambda a, b, c, t=t: mat_fn(t))
        px, py, pz = nx, ny, nz
    return px, py, pz


def _ramp_t(name, lo, hi):
    keys = P.RAMP_KEYS[name]
    return lambda t: keys[max(0, min(len(keys) - 1, int(round(lo + (hi - lo) * t)) - 1))]


GROWTH = (0.28, 0.52, 0.78, 1.0)   # size of crop stages 1 to 4


def vermillion_reed(stage=4):
    v = Vox()
    g = GROWTH[stage - 1]
    n = max(4, int(17 * g))
    for i in range(n):
        a = i * 2.399 + 0.4
        r = 0.6 + 3.2 * math.sqrt(i / n)
        length = max(4, int((20 + int(_hash(i, 0, 0, 1) * 16)) * g))
        _blade(v, math.cos(a) * r, math.sin(a) * r, a, length, 0.10 + 0.5 * (i / n), _ramp_t("crimson", 2, 5))
    return [("vermillion_reed", v)]


def dusk_blossom():
    v = Vox()
    # low rosette of dark leaves
    for i in range(6):
        a = i * math.tau / 6 + 0.2
        _blade(v, 0, 0, a, 8, 1.15, _ramp_t("violet", 1, 2), droop=0.5, width=0.9)
    _blade(v, 0, 0, 0.6, 15, 0.08, lambda t: "ink_2", width=0.5)
    cy = 17
    # cup of petals around a glowing heart
    for y in range(0, 6):
        r = 1.6 + 2.6 * math.sin(min(1.0, y / 4.0) * math.pi * 0.5) - (0.9 if y == 5 else 0)
        for i in range(40):
            a = i * math.tau / 40
            petal = int(a / math.tau * 6)
            key = ("dusk_1", "dusk_2")[(petal + (y > 3)) % 2] if y < 5 else "dusk_3"
            v.set(math.floor(math.cos(a) * r), cy + y - 2, math.floor(math.sin(a) * r), flat(key))
    v.ellipsoid(0, cy - 1.0, 0, 1.7, 1.4, 1.7, flat("dusk_1"))
    v.box(-1, cy, -1, 0, cy + 1, 0, flat("glow_violet"))
    return [("dusk_blossom", v)]


def glasswort(stage=4):
    v = Vox()
    g = GROWTH[stage - 1]
    spikes = [(0, 0, 0, 16, 0.02), (2.5, 1, 0.4, 11, 0.35), (-2.5, 0.5, 2.6, 12, 0.3), (0.5, -2.5, 4.6, 9, 0.45),
              (-1.5, 2.5, 1.7, 8, 0.5), (3, -2, 5.6, 7, 0.6), (-3.5, -2, 3.6, 6, 0.65)]
    for i, (x, z, a, length, lean) in enumerate(spikes[:max(2, int(round(len(spikes) * g)))]):
        length = max(3, int(round(length * g)))
        ex, ez = x + math.cos(a) * length * lean, z + math.sin(a) * length * lean
        v.line((x, 0, z), (ex, length, ez), 0.8 + 0.7 * g, gradient("water", 2, 4, 0, length, 0.2, i), 0.3)
        v.set(math.floor(ex), length, math.floor(ez), flat("sandstone_5"))
    v.repaint(flat("sandstone_5"), lambda x, y, z: y > 2 and _hash(x, y, z, 3) < 0.07)
    return [("glasswort", v)]


def floatbloom():
    v = Vox()
    for i in range(7):   # leaf rosette
        a = i * math.tau / 7
        _blade(v, 0, 0, a, 6, 1.25, _ramp_t("crimson", 1, 3), droop=0.4, width=0.9)
    stalks = [(0, 0, 0.0, 30, 0.03), (1, 1, 0.9, 22, 0.22), (-1, 1, 2.6, 25, 0.2), (0, -1, 4.4, 17, 0.3), (1, -1, 5.5, 12, 0.4)]
    for i, (x, z, a, length, lean) in enumerate(stalks):
        ex, ey, ez = _blade(v, x, z, a, length, lean, lambda t: "sandstone_2", width=0.5)
        r = 2.4 + (length % 5) * 0.28
        v.ellipsoid(round(ex), ey + r - 0.5, round(ez), r, r * 1.08, r,
                    gradient("sandstone", 3, 5, ey, ey + 2 * r, 0.1, i))
        v.set(round(ex) - 1, int(ey + 2 * r - 1), round(ez) - 1, flat("sandstone_5"))
    return [("floatbloom", v)]


def sulfur_lily():
    v = Vox()
    for i in range(5):
        a = i * math.tau / 5 + 0.3
        _blade(v, 0, 0, a, 9, 0.9, _ramp_t("green", 1, 3), droop=0.5, width=0.5)
    _blade(v, 0, 0, 1.0, 13, 0.06, lambda t: "green_2", width=0.5)
    cy = 13
    for i in range(6):   # six petals that open and curl back
        a = i * math.tau / 6
        px, py, pz = 0.0, float(cy), 0.0
        for s in range(9):
            t = s / 8
            tilt = 0.5 + 1.5 * t
            nx, ny, nz = px + math.cos(a) * math.sin(tilt), py + math.cos(tilt), pz + math.sin(a) * math.sin(tilt)
            key = ("rust_6", "cloth_mustard", "bronze_3", "bronze_3")[min(3, int(t * 4))]
            v.line((px, py, pz), (nx, ny, nz), 0.95 if 0.2 < t < 0.8 else 0.5, flat(key))
            px, py, pz = nx, ny, nz
    v.box(-1, cy, -1, 0, cy + 2, 0, flat("rust_5"))
    for (x, z) in ((-1, -1), (0, 0)):
        v.set(x, cy + 3, z, flat("rust_3"))
    return [("sulfur_lily", v)]


def canyon_fan():
    v = Vox()
    leaves = [(0.3, 22, 0.75), (1.6, 19, 0.9), (2.9, 23, 0.7), (4.2, 18, 0.95), (5.4, 20, 0.85)]
    for i, (a, length, lean) in enumerate(leaves):
        px, py, pz = 0.0, 0.0, 0.0
        side = (-math.sin(a), math.cos(a))
        for s in range(length):
            t = s / (length - 1)
            tilt = lean * (0.2 + 0.8 * t) + 0.5 * t * t
            nx, ny, nz = px + math.cos(a) * math.sin(tilt), py + math.cos(tilt), pz + math.sin(a) * math.sin(tilt)
            half = 0.5 + 5.0 * math.sin(min(1.0, t * 1.15) * math.pi) ** 0.8
            for w in range(-int(half), int(half) + 1):
                cup = abs(w) * 0.22        # leaf edges curl up a little
                key = "crimson_3" if w == 0 else ("violet_4" if abs(w) >= half - 1 else ("violet_2", "violet_3")[(s + abs(w)) % 5 > 1])
                v.set(math.floor(nx + side[0] * w), math.floor(ny + cup), math.floor(nz + side[1] * w), flat(key))
            px, py, pz = nx, ny, nz
    return [("canyon_fan", v)]


def grinleaf(stage=4):
    """The quiet-field crop (oumessa): a bushy green herb that flushes red when ripe. Placeholder name."""
    v = Vox()
    g = GROWTH[stage - 1]
    height = max(5, int(24 * g))
    _blade(v, 0, 0, 0.3, height, 0.04, lambda t: "green_3", width=0.9 if stage > 2 else 0.5)
    for tier, y in enumerate(range(max(2, int(5 * g)), height - 1, 4)):
        size = (9 - tier * 1.3) * (0.45 + 0.55 * g)
        for i in range(4):
            a = i * math.tau / 4 + tier * 0.8
            # each branch ends in a hand of narrow leaflets
            bx, bz = math.cos(a) * size * 0.45, math.sin(a) * size * 0.45
            v.line((0, y, 0), (bx, y + 2, bz), 0.5, flat("green_3"))
            for j in (-0.7, -0.35, 0.0, 0.35, 0.7):
                ln = size * (1.0 - abs(j) * 0.55)
                tip = (bx + math.cos(a + j) * ln, y + 2 - ln * 0.35, bz + math.sin(a + j) * ln)
                v.line((bx, y + 2, bz), tip, 0.5, flat(("herb_1", "green_5")[(i + tier) % 2] if j else "green_5"))
    if stage >= 3:   # flowering top, red when ripe
        bud = mix(mix(flat("crimson_3"), flat("crimson_4"), 0.4, 1), flat("sandstone_5"), 0.12, 2) if stage == 4 else flat("green_6")
        for y in range(height - 4, height + 4):
            r = 2.4 - abs(y - (height - 1)) * 0.42
            v.cylinder(0, 0, y, y, max(0.6, r), max(0.6, r), bud)
    return [("grinleaf", v)]


def redroot(stage=4):
    """Placeholder food crop: a fat red root under a rosette of green leaves."""
    v = Vox()
    g = GROWTH[stage - 1]
    n = 3 + stage
    for i in range(n):
        a = i * math.tau / n + 0.4
        _blade(v, 0, 0, a, max(3, int(13 * g)), 0.75, lambda t: ("crimson_2", "green_4", "green_5", "green_5")[min(3, int(t * 4))],
               droop=0.55, width=0.9 if stage > 1 else 0.5)
    if stage >= 2:   # the root's shoulder pushes out of the soil as it swells
        r = 0.8 + 1.1 * (stage - 1)
        v.ellipsoid(0, 0.4 * (stage - 1), 0, r, 1.0 + 0.7 * (stage - 1), r, gradient("crimson", 2, 4, 0, 4, 0.3, 3), ymin=0)
    return [("redroot", v)]


FLORA = {f.__name__: f for f in (dusk_blossom, floatbloom, sulfur_lily, canyon_fan)}
# Farmable or harvestable plants come in four growth stages: <id>_s1 (sprout) to <id>_s4 (ripe).
CROP_KINDS = (vermillion_reed, glasswort, redroot, grinleaf)
CROPS = {f"{f.__name__}_s{s}": (lambda f=f, s=s: f(s)) for f in CROP_KINDS for s in (1, 2, 3, 4)}
