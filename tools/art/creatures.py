"""Starter creatures and people, built from separate parts so limbs can swing.

Each builder returns (parts, rig). parts: list of (name, Vox) in assembled coordinates
(facing -z, origin at ground centre, 6.25 cm voxels). rig: {"parts": {name: {"parent", "pivot"}},
"clips": {clip: [channels]}}. A channel rotates one part around one axis:
angle = offset + amp * sin(2 pi (freq * t + phase)), in degrees; or bobs it up and down ("bob", in voxels).
Names follow the working animal taxonomy in the Aruun alpha bible.
"""
import math

from voxlib import Vox, flat, ramp, gradient, mix, _hash


def rot(part, axis, amp, freq, phase=0.0, offset=0.0):
    return {"part": part, "axis": axis, "amp": amp, "freq": freq, "phase": phase, "offset": offset}


def bob(part, amp, freq, phase=0.0):
    return {"part": part, "bob": amp, "freq": freq, "phase": phase}


def _quadruped_walk(freq, swing, knee, legs=("fl", "fr", "bl", "br")):
    """Diagonal pairs move together. Upper legs swing, lower legs fold on the forward stroke."""
    out = []
    for leg, ph in zip(legs, (0.0, 0.5, 0.5, 0.0)):
        out.append(rot(f"leg_{leg}_upper", "x", swing, freq, ph))
        out.append(rot(f"leg_{leg}_lower", "x", knee, freq, ph + 0.22, knee * (1 if leg[0] == "b" else -1) * 0.6))
    return out


# --- Redstrider ---------------------------------------------------------------

def redstrider():
    """Tall migratory herbivore of the wetlands and plains. About 2.6 m at the shoulder."""
    hide = gradient("hide", 3.4, 1.6, 30, 43, 0.15, 1)
    dark = ramp("hide", 1, 0.3, 2)
    body = Vox()
    body.ellipsoid(0, 37, -8, 5.4, 7.0, 8.5, hide)       # deep chest
    body.ellipsoid(0, 38, 2, 4.8, 5.6, 9.0, hide)        # barrel
    body.ellipsoid(0, 38, 10, 5.0, 6.2, 6.5, hide)       # hips
    body.repaint(flat("rust_3"), lambda x, y, z: y > 37 and _hash(x // 2, y // 2, z // 3, 5) < 0.16)
    for z in range(-14, -2):                              # wispy mane along the withers
        if _hash(0, 0, z, 3) < 0.75:
            h = 1 + int(_hash(1, 0, z, 3) * 3)
            body.box(-1, 44, z, 0, 44 + h, z, flat("ink_2"))

    neck = Vox()
    neck.line((0, 41, -13), (0, 63, -21), 3.4, hide, 2.1)
    for i in range(22):                                   # mane down the back of the neck
        t = i / 21
        y, z = 43 + 20 * t, -10.5 - 8 * t
        if _hash(i, 7, 0) < 0.8:
            neck.line((0, y, z), (0, y + 0.6, z + 2.4 + _hash(i, 8, 0) * 2), 0.5, flat("ink_2"))
    neck.repaint(gradient("hide", 3.2, 1.8, 41, 64, 0.3, 4), lambda x, y, z: neck.v[(x, y, z)] != "ink_2")

    head = Vox()
    head.ellipsoid(0, 65, -24, 2.6, 2.9, 3.6, ramp("hide", 2, 0.3, 6))
    head.line((0, 64.2, -26), (0, 62.6, -32), 2.2, ramp("hide", 2, 0.3, 6), 1.5)   # long muzzle
    head.box(-2, 61, -33, 1, 62, -32, flat("hide_1"))                             # nose
    for s in (-1, 1):
        x = -3 if s < 0 else 2
        head.set(x, 65, -26, flat("ink_1"))
        head.set(x, 66, -26, flat("ink_1"))
        head.line((s * 1.8, 67.5, -22), (s * 3.4, 71.5, -20.5), 0.8, flat("hide_1"))   # ear
    for i in range(6):                                    # wind-blown forelock
        head.line((0, 67.6, -24 + i * 0.7), (0, 69.5 + (i % 3), -21 + i * 1.3), 0.5, flat("ink_2"))

    tail = Vox()
    tail.line((0, 40, 15.5), (0, 33, 19), 1.4, dark, 0.9)
    tail.line((0, 33, 19), (0, 24, 20), 1.3, flat("ink_2"), 0.5)

    parts = [("body", body), ("neck", neck), ("head", head), ("tail", tail)]
    rig = {"body": {"parent": "", "pivot": [0, 37, 0]},
           "neck": {"parent": "body", "pivot": [0, 41, -12]},
           "head": {"parent": "neck", "pivot": [0, 63, -21]},
           "tail": {"parent": "body", "pivot": [0, 40, 15]}}
    for name, sx, z, knee_z in (("fl", -1, -10, -9), ("fr", 1, -10, -9), ("bl", -1, 10, 12), ("br", 1, 10, 12)):
        x = sx * 3.6
        upper, lower = Vox(), Vox()
        upper.line((x, 34, z), (x, 17, knee_z), 2.5, gradient("hide", 1.6, 2.6, 16, 34, 0.3, 8), 1.5)
        lower.line((x, 17, knee_z), (x, 2, z), 1.45, ramp("hide", 2, 0.3, 9), 1.15)
        lower.ellipsoid(x, 17, knee_z, 1.9, 1.9, 1.9, flat("hide_1"))             # knee knob
        cx = int(math.floor(x))
        lower.box(cx - 1, 0, z - 3, cx + 1, 1, z, flat("stone_1"))                # cloven hoof
        lower.erase(cx, 1, z - 3)
        lower.erase(cx, 0, z - 3)
        parts += [(f"leg_{name}_upper", upper), (f"leg_{name}_lower", lower)]
        rig[f"leg_{name}_upper"] = {"parent": "body", "pivot": [x, 34, z]}
        rig[f"leg_{name}_lower"] = {"parent": f"leg_{name}_upper", "pivot": [x, 17, knee_z]}

    clips = {
        "idle": [rot("neck", "x", 2.5, 0.16), rot("head", "x", 3.0, 0.16, 0.3), rot("head", "y", 6, 0.07),
                 rot("tail", "z", 14, 0.45), bob("body", 0.25, 0.25)],
        "walk": _quadruped_walk(0.8, 20, 16) + [rot("neck", "x", 3.5, 1.6), rot("head", "x", 3.0, 1.6, 0.5),
                                               rot("tail", "z", 10, 0.8), bob("body", 0.5, 1.6)],
        "graze": [rot("neck", "x", 4, 0.3, 0, -112), rot("head", "x", 5, 0.9, 0, 48), rot("tail", "z", 16, 0.5)],
    }
    return parts, {"parts": rig, "clips": clips}


# --- Nightstalker -------------------------------------------------------------

def nightstalker():
    """Twilight pursuit predator. Lean, striped, with small folded graspers under the chest."""
    def coat(x, y, z):
        if y < 16:
            return ramp("hide", 2, 0.3, 3)(x, y, z)
        stripe = math.sin(z * 0.85 + y * 0.55 + math.sin(y * 0.4) * 1.2) > 0.45 and y > 17
        return "rust_3" if stripe else ramp("ink", 2, 0.25, 2)(x, y, z)
    body = Vox()
    body.ellipsoid(0, 19, -8, 4.2, 5.6, 7.5, coat)        # deep chest
    body.ellipsoid(0, 20.5, 3, 3.3, 3.6, 8.5, coat)       # tucked waist
    body.ellipsoid(0, 20, 12, 3.9, 4.6, 5.5, coat)        # haunches
    body.line((0, 22, -13), (0, 22.5, -20), 2.9, coat, 2.3)   # neck, carried low and forward
    for s in (-1, 1):                                     # vestigial graspers
        body.line((s * 2.2, 14.5, -11), (s * 2.4, 12.5, -14), 0.5, flat("hide_2"))
        body.line((s * 2.4, 12.5, -14), (s * 2.0, 13.5, -16), 0.5, flat("hide_3"))

    head = Vox()
    head.ellipsoid(0, 22.5, -23.5, 2.8, 2.5, 3.8, coat)
    head.line((0, 21.6, -26), (0, 20.8, -31), 1.9, ramp("ink", 2, 0.2, 5), 1.2)
    head.box(-1, 20, -32, 0, 20, -32, flat("ink_1"))
    for s in (-1, 1):
        x = -3 if s < 0 else 2
        head.set(x, 23, -26, flat("glow_lamp"))
        head.line((s * 1.9, 24.5, -22.5), (s * 2.6, 30, -20.5), 1.0, flat("ink_1"), 0.5)   # tall ears
        head.set(int(math.floor(s * 2.3)), 26, -22, flat("hide_2"))

    tail = Vox()
    tail.line((0, 21, 16), (0, 15, 29), 1.5, coat, 1.0)
    tail.line((0, 15, 29), (0, 17.5, 41), 1.0, coat, 0.5)

    parts = [("body", body), ("head", head), ("tail", tail)]
    rig = {"body": {"parent": "", "pivot": [0, 19, 0]},
           "head": {"parent": "body", "pivot": [0, 22.5, -20]},
           "tail": {"parent": "body", "pivot": [0, 21, 16]}}
    for name, sx, z, mid, foot_z in (("fl", -1, -9, (9, -7.5), -9.5), ("fr", 1, -9, (9, -7.5), -9.5),
                                     ("bl", -1, 13, (9, 16.5), 13.5), ("br", 1, 13, (9, 16.5), 13.5)):
        x = sx * 3.1
        upper, lower = Vox(), Vox()
        upper.line((x, 18, z), (x, mid[0], mid[1]), 2.1 if z > 0 else 1.8, coat, 1.2)
        lower.line((x, mid[0], mid[1]), (x, 1.5, foot_z), 1.1, ramp("ink", 2, 0.25, 7), 0.9)
        cx = int(math.floor(x))
        lower.box(cx - 1, 0, int(foot_z) - 3, cx + 1, 1, int(foot_z), flat("ink_1"))
        lower.box(cx - 1, 0, int(foot_z) - 4, cx + 1, 0, int(foot_z) - 4, flat("sandstone_4") if False else flat("ink_1"))
        parts += [(f"leg_{name}_upper", upper), (f"leg_{name}_lower", lower)]
        rig[f"leg_{name}_upper"] = {"parent": "body", "pivot": [x, 18, z]}
        rig[f"leg_{name}_lower"] = {"parent": f"leg_{name}_upper", "pivot": [x, mid[0], mid[1]]}

    clips = {
        "idle": [rot("head", "y", 14, 0.11), rot("head", "x", 3, 0.3), rot("tail", "y", 16, 0.35),
                 rot("tail", "x", 5, 0.2), bob("body", 0.2, 0.3)],
        "walk": _quadruped_walk(1.0, 22, 18) + [rot("head", "x", 3, 2.0), rot("tail", "y", 12, 1.0), bob("body", 0.35, 2.0)],
        "run": _quadruped_walk(2.1, 38, 30) + [rot("body", "x", 4, 2.1), rot("head", "x", 5, 2.1, 0.5),
                                              rot("tail", "x", 12, 2.1, 0.25, 12), bob("body", 0.9, 2.1)],
    }
    return parts, {"parts": rig, "clips": clips}


# --- Mudback ------------------------------------------------------------------

def mudback():
    """Semi-aquatic marsh animal with feathery gill frills and webbed feet. About 2 m nose to tail."""
    skin = gradient("hide", 4.2, 1.8, 0.5, 9, 0.4, 2)

    def mottle(v):
        v.repaint(flat("rust_3"), lambda x, y, z: y >= 6 and _hash(x // 2, y, z // 2, 4) < 0.22)
    body = Vox()
    body.ellipsoid(0, 5.2, 1, 6.6, 4.4, 10.5, skin)
    mottle(body)
    for z in range(-6, 11):                               # low back ridge
        body.box(-1, 9, z, 0, 9 + (1 if z % 3 == 0 else 0), z, flat("hide_1"))

    head = Vox()
    head.ellipsoid(0, 5.4, -12.5, 5.6, 3.5, 5.6, skin)
    mottle(head)
    head.repaint(flat("hide_1"), lambda x, y, z: y == 4 and z < -12 and abs(x + 0.5) > 1.0)   # wide mouth line
    for s in (-1, 1):
        cx = s * 3.3
        head.ellipsoid(cx, 8.8, -13, 1.7, 1.6, 1.7, flat("hide_2"))
        ex = int(math.floor(cx + s * 0.6))
        head.box(ex, 9, -14, ex, 9, -13, flat("ink_1"))
        head.set(ex, 10, -14, flat("bronze_3"))

    parts = [("body", body), ("head", head)]
    rig = {"body": {"parent": "", "pivot": [0, 5, 0]},
           "head": {"parent": "body", "pivot": [0, 5, -8]}}
    for name, s in (("l", -1), ("r", 1)):                 # gill frills: three fans behind the jaw
        frill = Vox()
        for k, (ang, ln) in enumerate(((0.35, 6.5), (0.9, 7.5), (1.45, 6.0))):
            for w in (-1, 0, 1):
                tip = (s * (4.8 + math.sin(ang) * ln), 5.5 + w * 1.6 + k * 0.4, -8.5 + math.cos(ang) * ln * 0.9)
                frill.line((s * 4.8, 5.5 + w * 0.6, -8.5), tip, 0.5, gradient("crimson", 3, 5, 4, 9, 0.5, k))
        parts.append((f"frill_{name}", frill))
        rig[f"frill_{name}"] = {"parent": "head", "pivot": [s * 4.8, 5.5, -8.5]}

    tail = Vox()
    tail.line((0, 5, 10), (0, 3.2, 22), 3.4, skin, 1.6)
    tail.line((0, 3.2, 22), (0, 2.6, 31), 1.6, skin, 0.6)
    for z in range(11, 30):                               # tail fin
        h = int(round(2.8 * math.sin((z - 11) / 19 * math.pi)))
        top = max(y for (x, y, zz) in tail.v if zz == z)
        tail.box(-1, top + 1, z, 0, top + h, z, gradient("crimson", 2, 4, 5, 10, 0.4, 1))
    parts.append(("tail", tail))
    rig["tail"] = {"parent": "body", "pivot": [0, 5, 10]}

    for name, sx, z in (("fl", -1, -5), ("fr", 1, -5), ("bl", -1, 8), ("br", 1, 8)):
        leg = Vox()
        fz = z - 2 if z < 0 else z + 1
        leg.line((sx * 5.2, 4.0, z), (sx * 10, 4.4, fz), 1.7, ramp("hide", 2, 0.3, 6), 1.3)
        leg.line((sx * 10, 4.4, fz), (sx * 11, 1.2, fz - 1), 1.3, ramp("hide", 2, 0.3, 6), 1.0)
        for t in (-1, 0, 1):                              # webbed toes
            leg.line((sx * 11, 0.5, fz - 1), (sx * (12.5 + abs(t) * -0.6), 0.5, fz - 4.5 + abs(t) + t * 0.0), 0.5, flat("hide_3"))
            leg.line((sx * 11, 0.5, fz - 1), (sx * (11 + t * 2.2), 0.5, fz - 4.5), 0.5, flat("hide_3"))
        parts.append((f"leg_{name}", leg))
        rig[f"leg_{name}"] = {"parent": "body", "pivot": [sx * 5.2, 4.0, z]}

    def crawl(freq, amp):
        return [rot("leg_fl", "y", amp, freq, 0.0), rot("leg_br", "y", amp, freq, 0.0),
                rot("leg_fr", "y", amp, freq, 0.5), rot("leg_bl", "y", amp, freq, 0.5),
                rot("body", "y", amp * 0.25, freq, 0.25), rot("head", "y", amp * 0.25, freq, 0.75),
                rot("tail", "y", amp * 0.7, freq, 0.6)]
    clips = {
        "idle": [rot("frill_l", "y", 9, 0.5, 0, -6), rot("frill_r", "y", -9, 0.5, 0, 6), rot("tail", "y", 8, 0.18),
                 rot("head", "x", 2.5, 0.25), bob("body", 0.2, 0.4)],
        "walk": crawl(0.9, 24) + [rot("frill_l", "y", 6, 1.8), rot("frill_r", "y", -6, 1.8)],
        "swim": [rot("tail", "y", 30, 1.2), rot("body", "y", 7, 1.2, 0.35), rot("head", "y", 5, 1.2, 0.8),
                 rot("leg_fl", "y", 6, 1.2, 0, 50), rot("leg_fr", "y", 6, 1.2, 0.5, -50),
                 rot("leg_bl", "y", 6, 1.2, 0.5, 55), rot("leg_br", "y", 6, 1.2, 0, -55)],
    }
    return parts, {"parts": rig, "clips": clips}


# --- Skyblade -----------------------------------------------------------------

def skyblade():
    """Large canyon soarer that rides the updrafts. About 6 m from wingtip to wingtip."""
    Y = 8
    skin = gradient("hide", 3.6, 1.6, Y - 3, Y + 3, 0.3, 1)
    body = Vox()
    body.ellipsoid(0, Y, 0, 2.9, 3.0, 9.5, skin)
    body.ellipsoid(0, Y + 0.6, -5, 3.3, 3.2, 4.5, skin)                    # flight-muscle keel
    body.line((0, Y + 0.5, -8), (0, Y + 2, -13), 1.8, skin, 1.4)           # neck
    for s in (-1, 1):                                                       # tucked feet
        body.line((s * 1.6, Y - 3, 5), (s * 1.6, Y - 3.6, 9), 0.5, flat("hide_1"))

    head = Vox()
    head.ellipsoid(0, Y + 2.6, -15, 1.9, 1.9, 3.0, ramp("hide", 2, 0.3, 2))
    head.line((0, Y + 2.2, -17), (0, Y + 1.2, -29), 1.4, flat("sand_2"), 0.5)       # blade of a beak
    head.line((0, Y + 1.2, -17), (0, Y + 0.6, -27), 0.9, flat("wood_4"), 0.5)
    head.line((0, Y + 4.0, -14.5), (0, Y + 7.5, -7), 1.0, flat("rust_5"), 0.5)       # swept crest
    head.line((0, Y + 3.2, -13), (0, Y + 6.0, -7.5), 0.5, flat("violet_4"))
    for x in (-2, 1):
        head.set(x, Y + 3, -16, flat("ink_1"))

    tail = Vox()
    tail.line((0, Y, 9), (0, Y, 38), 1.2, ramp("hide", 2, 0.3, 3), 0.5)
    for z in range(36, 47):                                                 # leaf-shaped vane
        half = 3.6 * math.sin((z - 36) / 10 * math.pi) ** 0.7
        for x in range(-4, 4):
            if abs(x + 0.5) <= half:
                tail.set(x, Y, z, flat("rust_5" if abs(x + 0.5) > 1 else "rust_3"))

    parts = [("body", body), ("head", head), ("tail", tail)]
    rig = {"body": {"parent": "", "pivot": [0, Y, 0]},
           "head": {"parent": "body", "pivot": [0, Y + 2, -13]},
           "tail": {"parent": "body", "pivot": [0, Y, 9]}}

    JOINT, TIP = 22, 50

    def lead(ax):   # leading edge z at distance ax from the body: forward to the wrist, then swept back
        return -9 + (-3.0 * math.sin(ax / JOINT * math.pi * 0.5) if ax < JOINT else -3.0 + (ax - JOINT) * 0.42)

    def trail(ax):  # trailing edge z
        t = ax / TIP
        return 13 - 14.0 * t ** 1.7 + 1.2 * math.sin(ax * 0.55)             # scalloped between the fingers

    for name, s in (("l", -1), ("r", 1)):
        inner, outer = Vox(), Vox()
        for ax in range(2, TIP):
            x = ax if s > 0 else -ax - 1
            z0, z1 = lead(ax + 0.5), trail(ax + 0.5)
            target = inner if ax < JOINT else outer
            for z in range(int(math.floor(z0)), int(math.ceil(z1))):
                if z - z0 < 2.0:                                            # arm bone on the leading edge
                    key = "hide_1"
                    target.set(x, Y + 1, z, flat("hide_2")) if z - z0 < 1.0 and ax < TIP - 6 else None
                else:
                    u = (z - z0) / max(1.0, z1 - z0)
                    finger = any(abs((z - z0) - (ax - JOINT * 0.2) * k) < 0.6 for k in (0.35, 0.8)) and ax > 6
                    if finger:
                        key = "rust_3"
                    elif u > 0.78:
                        key = ("violet_4", "violet_3")[(ax + z) % 3 == 0]    # iridescent trailing edge
                    else:
                        key = gradient("rust", 6.2, 4.2, 0, 1, 0.5, 5, axis=1)(ax, u, z)
                target.set(x, Y, z, flat(key))
        parts += [(f"wing_{name}_inner", inner), (f"wing_{name}_outer", outer)]
        rig[f"wing_{name}_inner"] = {"parent": "body", "pivot": [s * 2.5, Y + 0.5, -4]}
        rig[f"wing_{name}_outer"] = {"parent": f"wing_{name}_inner", "pivot": [s * JOINT, Y + 0.5, -8]}

    def wings(freq, a_in, a_out, lift=0.0, fold=0.0):
        return [rot("wing_r_inner", "z", a_in, freq, 0.0, lift), rot("wing_l_inner", "z", -a_in, freq, 0.0, -lift),
                rot("wing_r_outer", "z", a_out, freq, -0.18, fold), rot("wing_l_outer", "z", -a_out, freq, -0.18, -fold)]
    clips = {
        "glide": wings(0.22, 4, 5, 6, -4) + [rot("tail", "y", 6, 0.3), rot("head", "y", 8, 0.13), rot("body", "z", 3, 0.1)],
        "flap": wings(1.15, 30, 24, 4, -6) + [bob("body", 1.6, 1.15, 0.5), rot("tail", "x", 5, 1.15, 0.3), rot("head", "x", 3, 1.15)],
    }
    return parts, {"parts": rig, "clips": clips}


# --- Vaari --------------------------------------------------------------------

def _vaari(tunic, skirt, sash, skin_level, seed):
    """Tall, light-framed person of Aruun (about 2.1 m). Look is exploratory, like the concept art."""
    skin = ramp("hide", skin_level, 0.25, seed)
    torso = Vox()
    for y in range(15, 27):                               # narrow chest that widens to the shoulders
        t = (y - 15) / 11
        hw, hd = 2.2 + 1.6 * t ** 1.5, 1.7 + 0.5 * t
        for x in range(-5, 5):
            for z in range(-3, 3):
                if (abs(x + 0.5) / hw) ** 2 + (abs(z + 0.5) / hd) ** 2 <= 1.0:
                    torso.set(x, y, z, ramp(*tunic, 0.3, seed + 1))
    for y in range(8, 16):                                # layered skirt with a ragged hem
        t = (16 - y) / 8
        hw, hd = 2.6 + 1.5 * t, 2.0 + 1.3 * t
        for x in range(-5, 5):
            for z in range(-4, 4):
                d = (abs(x + 0.5) / hw) ** 2 + (abs(z + 0.5) / hd) ** 2
                if d <= 1.0 and (d > 0.45 or y > 13) and not (y < 10 and _hash(x, 0, z, seed) < 0.3 + 0.2 * (9 - y)):
                    torso.set(x, y, z, ramp(*skirt, 0.35, seed + 2))
    torso.repaint(flat("bronze_2"), lambda x, y, z: y == 15)                       # belt
    torso.repaint(flat(sash), lambda x, y, z: 16 <= y <= 26 and abs((x + 0.5) * 1.25 + (y - 21)) < 1.3)   # sash
    torso.repaint(flat(sash), lambda x, y, z: 10 <= y <= 14 and -4 <= x <= -3 and z < 0)                   # its tail
    torso.set(-1, 15, min(z for (x, y, z) in torso.v if y == 15), flat("bronze_3"))
    torso.line((0, 26.5, 0), (0, 31, -0.6), 1.05, skin)                            # long neck
    torso.box(-1, 28, -1, 0, 28, 0, flat("bronze_3"))                               # neck ring

    head = Vox()
    head.ellipsoid(0, 32.8, -0.3, 1.9, 1.9, 2.7, skin)
    head.line((0, 33.4, 1.0), (0, 35.6, 5.0), 1.1, skin, 0.5)                       # back-swept crest
    head.line((0, 31.8, -2.2), (0, 31.4, -3.2), 0.9, skin)                          # short muzzle
    for x in (-2, 1):
        head.box(x, 33, -2, x, 33, -1, flat("ink_1"))                               # large side-set eyes
    head.repaint(flat("hide_1"), lambda x, y, z: z > 2 and y >= 35)

    parts = [("torso", torso), ("head", head)]
    rig = {"torso": {"parent": "", "pivot": [0, 16, 0]},
           "head": {"parent": "torso", "pivot": [0, 31, -0.5]}}
    for name, s in (("l", -1), ("r", 1)):
        arm = Vox()
        arm.ellipsoid(s * 4.0, 25.0, 0, 1.6, 1.6, 1.7, ramp(*tunic, 0.3, seed + 3))  # shoulder wrap
        arm.line((s * 4.2, 24, 0), (s * 4.8, 18.5, 0.3), 0.95, skin)
        arm.line((s * 4.8, 18.5, 0.3), (s * 4.6, 12.5, -0.8), 0.8, skin)
        cx = int(math.floor(s * 4.7))
        arm.box(cx - (1 if s > 0 else 0), 14, -1, cx + (0 if s > 0 else 1), 14, 0, flat("bronze_3"))   # bracelet
        leg = Vox()
        leg.line((s * 1.5, 15, 0), (s * 1.6, 8, -0.6), 1.25, skin, 1.0)
        leg.line((s * 1.6, 8, -0.6), (s * 1.6, 1.5, 0.4), 1.0, skin, 0.85)
        fx = int(math.floor(s * 1.6))
        leg.box(fx - (1 if s > 0 else 0), 0, -3, fx + (0 if s > 0 else 1), 0, 1, flat("wood_2"))        # sandal
        leg.box(fx - (1 if s > 0 else 0), 1, -1, fx + (0 if s > 0 else 1), 1, 1, flat("wood_2"))
        parts += [(f"arm_{name}", arm), (f"leg_{name}", leg)]
        rig[f"arm_{name}"] = {"parent": "torso", "pivot": [s * 4.0, 25, 0]}
        rig[f"leg_{name}"] = {"parent": "torso", "pivot": [s * 1.5, 15, 0]}

    clips = {
        "idle": [bob("torso", 0.2, 0.25), rot("head", "y", 10, 0.09), rot("head", "x", 2, 0.25, 0.2),
                 rot("arm_l", "x", 3, 0.25), rot("arm_r", "x", 3, 0.25, 0.5)],
        "walk": [rot("leg_l", "x", 26, 0.9), rot("leg_r", "x", 26, 0.9, 0.5), rot("arm_l", "x", 18, 0.9, 0.5),
                 rot("arm_r", "x", 18, 0.9), bob("torso", 0.4, 1.8), rot("head", "x", 2, 1.8)],
        "talk": [rot("arm_r", "x", 14, 0.7, 0, 55), rot("arm_r", "z", 6, 0.35, 0, 12), rot("arm_l", "x", 4, 0.35),
                 rot("head", "x", 4, 0.7, 0.2), rot("head", "y", 6, 0.2), bob("torso", 0.15, 0.35)],
    }
    return parts, {"parts": rig, "clips": clips}


def _human(tunic, trousers, sash, skin_key, hair_key, seed):
    """Earth-born human (about 1.75 m): shorter and sturdier than a Vaari. The stranded player."""
    skin = flat(skin_key)
    torso = Vox()
    torso.box(-4, 13, -2, 3, 23, 1, ramp(*tunic, 0.3, seed + 1))                   # chest and belly
    for (x, z) in ((-4, -2), (3, -2), (-4, 1), (3, 1)):
        torso.box(x, 13, z, x, 15, z, flat("ink_1"))
        torso.erase(x, 13, z), torso.erase(x, 14, z), torso.erase(x, 15, z)
    torso.box(-4, 12, -2, 3, 12, 1, flat("wood_2"))                                # belt
    torso.set(-1, 12, -2, flat("bronze_3"))
    torso.repaint(flat(sash), lambda x, y, z: 13 <= y <= 23 and abs((x + 0.5) * 1.1 - (y - 18)) < 1.2)   # strap
    torso.box(-1, 24, -1, 0, 24, 0, skin)                                          # neck
    torso.box(-3, 10, -2, 2, 11, 1, ramp(*trousers, 0.3, seed + 2))                # hips

    head = Vox()
    head.box(-3, 25, -3, 2, 30, 2, skin)
    for (x, z) in ((-3, -3), (2, -3), (-3, 2), (2, 2)):
        head.erase(x, 25, z), head.erase(x, 30, z)
    head.box(-3, 29, -3, 2, 31, 2, flat(hair_key))                                 # hair
    head.box(-3, 26, 0, 2, 28, 2, flat(hair_key))
    head.box(-2, 31, -2, 1, 31, 1, flat(hair_key))
    head.box(-2, 29, -3, 1, 29, -3, skin)                                          # forehead
    for x in (-2, 1):
        head.set(x, 28, -3, flat("ink_1"))
    head.set(-1, 26, -3, flat("hide_2")), head.set(0, 26, -3, flat("hide_2"))

    parts = [("torso", torso), ("head", head)]
    rig = {"torso": {"parent": "", "pivot": [0, 12, 0]},
           "head": {"parent": "torso", "pivot": [0, 25, 0]}}
    for name, s in (("l", -1), ("r", 1)):
        x0 = -6 if s < 0 else 4
        arm = Vox()
        arm.box(x0, 18, -1, x0 + 1, 23, 0, ramp(*tunic, 0.3, seed + 3))            # sleeve
        arm.box(x0, 12, -1, x0 + 1, 17, 0, skin)
        arm.box(x0, 15, -1, x0 + 1, 15, 0, flat("wood_2"))                         # wrist wrap
        lx = -3 if s < 0 else 0
        leg = Vox()
        leg.box(lx, 3, -1, lx + 2, 10, 1, ramp(*trousers, 0.3, seed + 4))
        leg.box(lx, 0, -2, lx + 2, 2, 1, flat("wood_1"))                           # boot
        leg.box(lx, 3, -1, lx + 2, 3, 1, flat("wood_2"))
        parts += [(f"arm_{name}", arm), (f"leg_{name}", leg)]
        rig[f"arm_{name}"] = {"parent": "torso", "pivot": [x0 + 1, 23, 0]}
        rig[f"leg_{name}"] = {"parent": "torso", "pivot": [lx + 1.5, 10, 0]}
    clips = {
        "idle": [bob("torso", 0.2, 0.28), rot("head", "y", 9, 0.1), rot("arm_l", "x", 3, 0.28), rot("arm_r", "x", 3, 0.28, 0.5)],
        "walk": [rot("leg_l", "x", 30, 1.1), rot("leg_r", "x", 30, 1.1, 0.5), rot("arm_l", "x", 24, 1.1, 0.5),
                 rot("arm_r", "x", 24, 1.1), bob("torso", 0.45, 2.2)],
        "run": [rot("leg_l", "x", 48, 1.9), rot("leg_r", "x", 48, 1.9, 0.5), rot("arm_l", "x", 40, 1.9, 0.5, 20),
                rot("arm_r", "x", 40, 1.9, 0, 20), rot("torso", "x", 2, 1.9, 0, -8), bob("torso", 0.8, 3.8)],
        "swing": [rot("arm_r", "x", 70, 1.6, 0, 60), rot("torso", "y", 12, 1.6, 0.1)],
    }
    return parts, {"parts": rig, "clips": clips}


def mimmie_lou():
    """Mimmie-L'ou, food seller: a cheerful human of Aruun (about 1.6 m). From Bruh's concept sheet:
    olive dress, cream apron with a violet leaf, curly brown hair in a bun, brown boots, wooden spoon."""
    seed = 77
    dress = mix(flat("green_3"), flat("green_1"), 0.1, seed)   # olive green #4f6b3a, not lime
    dress_dark = flat("green_1")
    apron = mix(flat("sandstone_5"), flat("cloth_linen"), 0.25, seed + 1)   # cream
    skin = flat("hide_4")
    hair = ramp("wood", 3, 0.5, seed + 2)
    hair_lite = flat("wood_4")

    torso = Vox()
    torso.box(-4, 14, -2, 3, 19, 1, dress)                                         # bodice
    for y in range(6, 14):                                                        # full skirt, flaring to the hem
        t = (14 - y) / 8
        hw, hd = 4.3 + 1.3 * t, 2.6 + 1.0 * t
        for x in range(-6, 6):
            for z in range(-4, 4):
                if (abs(x + 0.5) / hw) ** 2 + (abs(z + 0.5) / hd) ** 2 <= 1.0:
                    torso.set(x, y, z, dress)
    torso.repaint(dress_dark, lambda x, y, z: y == 6)                              # hem
    front = {}
    for (x, y, z) in list(torso.v):
        front[(x, y)] = min(front.get((x, y), 9), z)
    for (x, y), z in front.items():                                                # apron: bib and skirt panel
        if (7 <= y <= 13 and -3 <= x <= 2) or (14 <= y <= 18 and -2 <= x <= 1):
            torso.set(x, y, z, apron)
    torso.box(-4, 13, -3, 3, 13, -3, dress_dark)                                   # apron waist tie
    torso.box(-1, 13, 2, 0, 13, 3, flat("sandstone_5"))                            # its bow at the back
    torso.box(-2, 11, 3, -2, 12, 3, flat("sandstone_5")), torso.box(1, 11, 3, 1, 12, 3, flat("sandstone_5"))
    for (x, y) in ((-2, 19), (1, 19)):                                             # straps over the shoulders
        torso.box(x, y, -2, x, y, 1, apron)
    leaf = flat("violet_3")
    zf = front[(0, 10)]
    for (x, y) in ((-2, 8), (-1, 9), (-1, 10), (0, 11)):                        # the leaf emblem: a stem...
        torso.set(x, y, zf, flat("green_2"))
    for (x, y) in ((-3, 9), (-2, 10), (0, 10), (1, 11), (0, 12), (-2, 11), (1, 10)):   # ...with violet leaves
        torso.set(x, y, zf, leaf)
    torso.set(0, 9, zf, flat("violet_4")), torso.set(-1, 12, zf, flat("violet_4"))
    torso.box(-1, 20, -1, 0, 20, 0, skin)                                          # neck

    head = Vox()
    head.box(-4, 21, -3, 3, 28, 2, skin)                                           # a big, friendly head
    for (x, z) in ((-4, -3), (3, -3), (-4, 2), (3, 2)):
        head.erase(x, 21, z), head.erase(x, 28, z)
    head.set(-5, 24, 0, skin), head.set(4, 24, 0, skin)                            # ears
    for x in (-3, 1):                                                              # eyes, with a glint
        head.box(x, 24, -4, x + 1, 25, -4, flat("ink_1"))
        head.set(x + 1, 25, -4, flat("sandstone_5"))
    for x in (-4, 2):
        head.box(x, 23, -4, x + 1, 23, -4, flat("crimson_5"))                      # rosy cheeks
    head.box(-2, 22, -4, 1, 22, -4, flat("crimson_1"))                             # big open smile
    head.set(-1, 22, -4, flat("sandstone_5")), head.set(0, 22, -4, flat("sandstone_5"))
    head.box(-1, 21, -4, 0, 21, -4, flat("crimson_2"))
    head.box(-3, 26, -4, -2, 26, -4, flat("wood_1")), head.box(1, 26, -4, 2, 26, -4, flat("wood_1"))   # brows
    # Curly hair: a cap with lumpy curls, falling past the ears, and a bun at the back.
    for x in range(-5, 5):
        for y in range(22, 32):
            for z in range(-5, 5):
                if -4 <= x <= 3 and 21 <= y <= 28 and -3 <= z <= 2:
                    continue
                cx, cy, cz = x + 0.5, y - 26.5, z + 0.5
                r = (cx / 5.2) ** 2 + (max(cy, -1.0) / 5.6) ** 2 + (cz / 4.6) ** 2
                curl = _hash(x, y, z, seed) * 0.35
                if r > 1.0 + curl - 0.12:
                    continue
                if z < -3 and y < 28:                                             # keep the face clear
                    continue
                if z <= -3 and y < 30 and abs(cx) < 4 and _hash(x, y, z, seed + 3) < 0.45:
                    continue                                                      # ragged fringe
                if y < 24 and z < 0:
                    continue
                if y < 25 and abs(cx) > 4.6 and z < 1:
                    continue
                head.set(x, y, z, hair_lite if _hash(x, y, z, seed + 4) < 0.22 else hair)
    head.ellipsoid(0, 30, 3.6, 1.8, 1.8, 1.6, hair)                                # bun
    head.set(0, 31, 5, hair_lite)

    parts = [("torso", torso), ("head", head)]
    rig = {"torso": {"parent": "", "pivot": [0, 13, 0]},
           "head": {"parent": "torso", "pivot": [0, 21, 0]}}
    for name, s in (("l", -1), ("r", 1)):
        x0 = -6 if s < 0 else 4
        arm = Vox()
        arm.box(x0, 16, -1, x0 + 1, 19, 0, dress)                                  # puffed short sleeve
        arm.set(x0 + (0 if s < 0 else 1), 16, -2, dress), arm.set(x0 + (0 if s < 0 else 1), 16, 1, dress)
        arm.box(x0, 11, -1, x0 + 1, 15, 0, skin)
        arm.box(x0, 10, -1, x0 + 1, 10, 0, skin)
        if s > 0:                                                                  # her wooden spoon
            arm.line((x0 + 0.5, 10.5, -1.5), (x0 + 0.5, 8.5, -3.5), 0.5, flat("wood_4"))
            arm.ellipsoid(x0 + 0.5, 7.8, -4.2, 0.8, 1.0, 0.8, flat("wood_4"))
        lx = -3 if s < 0 else 0
        leg = Vox()
        leg.box(lx, 4, -1, lx + 2, 6, 0, skin)
        leg.box(lx, 0, -3, lx + 2, 3, 1, ramp("wood", 2, 0.2, seed + 5))           # chunky boots
        leg.box(lx, 3, -1, lx + 2, 3, 1, flat("wood_3"))                           # turned-down cuff
        leg.box(lx, 0, -3, lx + 2, 0, 1, flat("wood_1"))                           # sole
        parts += [(f"arm_{name}", arm), (f"leg_{name}", leg)]
        rig[f"arm_{name}"] = {"parent": "torso", "pivot": [x0 + 1, 19, 0]}
        rig[f"leg_{name}"] = {"parent": "torso", "pivot": [lx + 1.5, 6, 0]}
    clips = {
        "idle": [bob("torso", 0.25, 0.3), rot("head", "y", 8, 0.12), rot("head", "z", 4, 0.3),
                 rot("arm_l", "z", 3, 0.3, 0, -18), rot("arm_r", "x", 4, 0.3, 0.5, 10)],
        "walk": [rot("leg_l", "x", 28, 1.2), rot("leg_r", "x", 28, 1.2, 0.5), rot("arm_l", "x", 22, 1.2, 0.5),
                 rot("arm_r", "x", 22, 1.2), bob("torso", 0.5, 2.4), rot("head", "z", 3, 1.2)],
        "talk": [rot("arm_r", "x", 18, 0.9, 0, 60), rot("arm_r", "z", 8, 0.45, 0, 8), rot("arm_l", "z", 3, 0.3, 0, -18),
                 rot("head", "z", 6, 0.45), rot("head", "x", 4, 0.9, 0.2), bob("torso", 0.3, 0.9)],
        "cheer": [rot("arm_l", "z", 10, 1.4, 0, -150), rot("arm_r", "z", 10, 1.4, 0.5, 150), bob("torso", 0.8, 2.8),
                  rot("head", "x", 6, 1.4, 0, -6)],
    }
    return parts, {"parts": rig, "clips": clips}


def character(spec):
    if spec.get("body") == "human":
        def pair(value):
            name, _, level = value.partition(":")
            return (name, int(level or 2))
        return _human(pair(spec["tunic"]), pair(spec["skirt"]), spec["sash"], spec.get("skin_key", "hide_4"),
                      spec.get("hair", "wood_1"), int(spec.get("seed", 10)))
    return _character_vaari(spec)


def _character_vaari(spec):
    """Builds a Vaari from a row of tools/art/characters.json (see make_character.py)."""
    def split(value):
        name, _, level = value.partition(":")
        return (name, int(level or 2))
    return _vaari(split(spec["tunic"]), split(spec["skirt"]), spec["sash"], int(spec.get("skin", 3)), int(spec.get("seed", 10)))


def _load_characters():
    import json
    import pathlib
    rows = json.loads((pathlib.Path(__file__).parent / "characters.json").read_text())["characters"]
    return {row["id"]: (lambda row=row: character(row)) for row in rows}


CREATURES = {f.__name__: f for f in (redstrider, nightstalker, mudback, skyblade, mimmie_lou)}
CREATURES.update(_load_characters())
