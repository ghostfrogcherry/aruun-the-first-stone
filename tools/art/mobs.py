"""More creatures for the demo region. Same conventions as creatures.py: each builder returns
(parts, rig); things face -z, stand on the ground at the origin, 6.25 cm voxels.
DESIGN holds the behaviour sheet for each one; build.py copies it into models.json and
docs/design/mobs.md so designers and the gameplay code read the same thing."""
import math

from voxlib import Vox, flat, ramp, gradient, mix, _hash
from creatures import rot, bob

DESIGN = {
    "reedhopper": {
        "name": "Reedhopper", "role": "Passive prey. First food and hide for a new player.",
        "size": "0.6 m tall with ears up", "where": "Riverbanks and reed beds, in groups of 3 to 6, by day.",
        "behaviour": "Grazes and freezes when watched. Bolts in long low-gravity hops when you come within 6 m "
                     "or when one of the group is hit. Hides in vermillion reeds, where its red back disappears.",
        "attacks": "None.", "counter": "Approach crouched from downwind, or throw a spear. A Graviturge can pin one mid-hop.",
        "drops": "raw_meat, hide (small)", "alien_touch": "Four tiny forelimbs for feeding, two huge hind legs; feathery ear-gills.",
    },
    "glassback": {
        "name": "Glassback", "role": "Defensive tank. Dangerous only if provoked or cornered.",
        "size": "1.4 m long, 0.9 m tall", "where": "Rocky slopes and highland meadows, alone or in pairs.",
        "behaviour": "Slow six-legged grazer that grows glasswort-like crystal on its shell to store water. "
                     "Ignores you until hit or crowded for a few seconds, then lowers its head and charges in a straight line.",
        "attacks": "Charge (heavy knockback, long recovery). Shell spikes hurt anything that strikes its back.",
        "counter": "Sidestep the charge and hit the soft flank or belly while it recovers. Lifting it with gravity magick "
                   "flips it onto its back for a few seconds.",
        "drops": "glasswort_leaf, hide, raw_meat; crystal shards (future crafting)", "alien_touch": "All six legs kept; crystal garden on its back.",
    },
    "duskmoth": {
        "name": "Duskmoth", "role": "Night nuisance in swarms. Makes campfires and lamps a choice.",
        "size": "1.3 m wingspan", "where": "Forest edges and meadows from dusk to dawn, 3 to 8 together.",
        "behaviour": "Drifts on four wings and is drawn to light: campfires, lamps and glowing inscriptions. "
                     "Circles the light, then dives at whoever stands nearest. Scatters at dawn or if the light goes out.",
        "attacks": "Dive (light damage). Wing dust briefly blurs vision if three or more hit in a row.",
        "counter": "Fight in the dark, or lure them with a lamp placed away from camp. Fragile: one or two hits each.",
        "drops": "wing dust (future: lamp oil, dye), cord fibre", "alien_touch": "Four wings and two grasping legs; lamp-coloured eye spots that glow.",
    },
    "echo_sentinel": {
        "name": "Echo Sentinel", "role": "Ruin guardian and mini-boss. Teaches the gravity magick lesson before the real boss.",
        "size": "2.6 m tall, hovering", "where": "Ancient ruins only. Dormant as a pile of stones until someone enters its hall.",
        "behaviour": "Old-builder stonework held together by gravity magick around a lit core. Hovers, keeps its distance, "
                     "then closes to slam. Its stones fall apart for a moment after each slam and when it is staggered.",
        "attacks": "Fist slam (shockwave on the ground), thrown orbit stones, a pull that drags you in before a slam.",
        "counter": "The core is exposed from behind and whenever the stones part. Blocks are immune to weapons; "
                   "use the gravity word to drop a fist or pull stones away, then strike the core.",
        "drops": "ancient_stone, bronze fittings, an Avarra word fragment", "alien_touch": "Not alive: teal Avarra glow marks it as magick.",
    },
}


def reedhopper():
    """Small hopping grazer of the reed beds. Passive prey."""
    back = gradient("crimson", 4.4, 2.6, 4, 12, 0.25, 1)
    body = Vox()
    body.ellipsoid(0, 7.5, 1, 3.4, 3.6, 5.2, back)
    body.repaint(flat("sandstone_4"), lambda x, y, z: y <= 5)                           # pale belly
    body.ellipsoid(0, 8, 6.5, 1.3, 1.3, 1.3, flat("sandstone_5"))                       # tail tuft
    for s in (-1, 1):                                                                   # four tiny forelimbs
        for z in (-3, -1):
            body.line((s * 1.8, 5, z), (s * 2.2, 2.5, z - 1), 0.5, flat("hide_2"))
    head = Vox()
    head.ellipsoid(0, 10.5, -4.5, 2.5, 2.4, 2.8, back)
    head.box(-1, 9, -8, 0, 9, -7, flat("hide_3"))                                       # snout
    for x in (-3, 2):
        head.box(x, 11, -6, x, 11, -5, flat("ink_1"))
    parts = [("body", body), ("head", head)]
    rig = {"body": {"parent": "", "pivot": [0, 7, 1]}, "head": {"parent": "body", "pivot": [0, 9.5, -3]}}
    for name, s in (("l", -1), ("r", 1)):
        ear = Vox()
        for k in range(3):                                                              # feathery ear-gills
            ear.line((s * 1.4, 12.5, -3.5 + k * 0.2), (s * (2.2 + k * 0.9), 18 - k * 1.2, -2 + k * 1.3), 0.5,
                     gradient("crimson", 3, 5, 12, 18, 0.2, k))
        leg = Vox()
        leg.ellipsoid(s * 3.4, 6.5, 3, 1.6, 2.6, 2.8, ramp("crimson", 3, 0.3, 4))       # big haunch
        leg.line((s * 3.6, 5, 4), (s * 3.6, 2.2, 6.5), 1.0, flat("hide_2"))
        leg.line((s * 3.6, 2.2, 6.5), (s * 3.6, 0.5, 1.5), 0.9, flat("hide_2"))         # long foot
        parts += [(f"ear_{name}", ear), (f"leg_{name}", leg)]
        rig[f"ear_{name}"] = {"parent": "head", "pivot": [s * 1.4, 12.5, -3.5]}
        rig[f"leg_{name}"] = {"parent": "body", "pivot": [s * 3.4, 6.5, 3]}
    clips = {
        "idle": [rot("ear_l", "z", 8, 0.9, 0.0, 4), rot("ear_r", "z", -8, 0.9, 0.3, -4), rot("head", "x", 6, 0.35, 0, 8),
                 bob("body", 0.15, 0.6)],
        "alert": [rot("ear_l", "z", 2, 3.0, 0, -8), rot("ear_r", "z", -2, 3.0, 0, 8), rot("head", "x", 1, 0.5, 0, -14)],
        "hop": [rot("leg_l", "x", 42, 1.6), rot("leg_r", "x", 42, 1.6), rot("body", "x", 12, 1.6, 0.2),
                bob("body", 3.0, 1.6, 0.25), rot("ear_l", "x", 18, 1.6, 0.5, 20), rot("ear_r", "x", 18, 1.6, 0.5, 20)],
    }
    return parts, {"parts": rig, "clips": clips}


def glassback():
    """Six-legged armoured grazer with a crystal garden on its shell. Charges when provoked."""
    shell = mix(ramp("stone", 3, 0.4, 1), ramp("hide", 1, 0.3, 2), 0.3, 3)
    body = Vox()
    body.ellipsoid(0, 8, 2, 8.2, 6.2, 11.5, shell, ymin=5)
    body.ellipsoid(0, 6.5, 2, 6.8, 2.6, 10.0, ramp("hide", 3, 0.3, 4))                   # soft underside
    body.repaint(flat("stone_2"), lambda x, y, z: y >= 8 and (z + 40) % 5 == 0)          # shell plates
    spikes = [(0, 3, 9), (-3, 0, 7), (3, 6, 8), (-4, 7, 6), (4, -2, 6), (-1, -5, 6), (1, 10, 5), (-5, 3, 4), (5, 3, 5), (0, -1, 5)]
    for i, (x, z, h) in enumerate(spikes):                                               # glasswort-like crystals
        top = max(y for (xx, y, zz) in body.v if abs(xx - x) <= 1 and abs(zz - z) <= 1)
        lean = (_hash(i, 1, 0) - 0.5) * 3
        body.line((x, top, z), (x + lean, top + h, z + lean * 0.5), 1.3, gradient("water", 2, 4, top, top + h, 0.2, i), 0.4)
        body.set(math.floor(x + lean), top + h, math.floor(z + lean * 0.5), flat("sandstone_5"))
    head = Vox()
    head.ellipsoid(0, 6.5, -11.5, 4.2, 3.4, 3.6, ramp("stone", 3, 0.4, 6))
    head.box(-3, 8, -13, 2, 9, -10, flat("stone_2"))                                     # brow plate for ramming
    for s in (-1, 1):
        x = -4 if s < 0 else 3
        head.set(x, 7, -13, flat("ink_1"))
        head.line((s * 2.5, 4.5, -14), (s * 3.2, 3, -17), 0.8, flat("bronze_3"), 0.5)    # digging tusks
    parts = [("body", body), ("head", head)]
    rig = {"body": {"parent": "", "pivot": [0, 7, 2]}, "head": {"parent": "body", "pivot": [0, 6.5, -9]}}
    for side, s in (("l", -1), ("r", 1)):
        for n, z in ((1, -5), (2, 2), (3, 9)):
            leg = Vox()
            leg.line((s * 6.5, 6, z), (s * 9.5, 4.5, z), 1.5, ramp("hide", 2, 0.3, 7), 1.2)
            leg.line((s * 9.5, 4.5, z), (s * 10, 0.8, z - 0.5), 1.3, ramp("hide", 1, 0.3, 8), 1.1)
            cx = int(math.floor(s * 10))
            leg.box(cx - 1, 0, z - 2, cx + 1, 0, z, flat("stone_1"))
            parts.append((f"leg_{side}{n}", leg))
            rig[f"leg_{side}{n}"] = {"parent": "body", "pivot": [s * 6.5, 6, z]}

    def gait(freq, amp):
        out = []
        for side, sign in (("l", 1), ("r", -1)):
            for n in (1, 2, 3):   # alternating tripod: l1, r2, l3 move together
                phase = 0.0 if (n % 2 == 1) == (side == "l") else 0.5
                out.append(rot(f"leg_{side}{n}", "y", amp * sign, freq, phase))
                out.append(rot(f"leg_{side}{n}", "z", amp * 0.35 * sign, freq, phase + 0.25))
        return out
    clips = {
        "idle": [rot("head", "x", 5, 0.2, 0, 10), rot("head", "y", 8, 0.11), bob("body", 0.2, 0.3)],
        "walk": gait(0.9, 16) + [rot("body", "z", 2, 0.9), bob("body", 0.25, 1.8), rot("head", "x", 3, 0.9, 0, 6)],
        "charge": gait(2.4, 26) + [rot("head", "x", 2, 2.4, 0, 22), rot("body", "x", 2, 2.4, 0, 6), bob("body", 0.6, 4.8)],
    }
    return parts, {"parts": rig, "clips": clips}


def duskmoth():
    """Four-winged night flier drawn to light. Comes in swarms."""
    Y = 6
    body = Vox()
    body.ellipsoid(0, Y, 1, 1.9, 1.9, 4.6, mix(ramp("dusk", 1, 0.3, 1), flat("sandstone_4"), 0.25, 2))   # fuzzy body
    body.ellipsoid(0, Y + 0.4, -4, 1.7, 1.6, 1.8, flat("ink_2"))                                          # head
    for s in (-1, 1):
        body.set(-2 if s < 0 else 1, Y + 1, -5, flat("glow_lamp"))                                        # eyes
        body.line((s * 0.8, Y + 1.5, -5), (s * 3.2, Y + 4.5, -8), 0.5, flat("sandstone_3"))               # antennae
        body.line((s * 1.0, Y - 1.5, -1), (s * 1.6, Y - 4, -2.5), 0.5, flat("ink_1"))                     # grasping legs
    parts = [("body", body)]
    rig = {"body": {"parent": "", "pivot": [0, Y, 0]}}
    for name, s in (("l", -1), ("r", 1)):
        for pair, (z0, span, chord) in (("fore", (-3, 11, 7)), ("hind", (2, 8, 6))):
            wing = Vox()
            for ax in range(1, span + 1):
                x = ax if s > 0 else -ax - 1
                t = ax / span
                depth = chord * math.sin(min(1.0, t * 1.25) * math.pi) ** 0.6
                sweep = -2.5 * t if pair == "fore" else 2.5 * t
                for k in range(int(round(depth)) + 1):
                    z = int(round(z0 + sweep + k))
                    edge = k == 0 or k >= depth - 0.6 or ax == span
                    key = "dusk_1" if edge else ("violet_3" if (ax + k) % 4 == 0 else "dusk_2")
                    wing.set(x, Y, z, flat(key))
            cx = (span * 0.62) * s                                                                         # glowing eye spot
            ex, ez = int(math.floor(cx)), int(round(z0 + chord * 0.5 + (-1.5 if pair == "fore" else 1.5)))
            for (dx, dz) in ((0, 0), (1, 0), (0, 1), (1, 1)):
                wing.set(ex + dx, Y, ez + dz, flat("glow_lamp"))
            for (dx, dz) in ((-1, 0), (2, 0), (0, -1), (1, 2), (-1, 1), (2, 1), (0, 2), (1, -1)):
                wing.set(ex + dx, Y, ez + dz, flat("ink_1"))
            parts.append((f"wing_{name}_{pair}", wing))
            rig[f"wing_{name}_{pair}"] = {"parent": "body", "pivot": [s * 1.5, Y, z0 + 2]}

    def wings(freq, amp, lift):
        return [rot("wing_r_fore", "z", amp, freq, 0.0, lift), rot("wing_l_fore", "z", -amp, freq, 0.0, -lift),
                rot("wing_r_hind", "z", amp * 0.8, freq, -0.12, lift * 0.6), rot("wing_l_hind", "z", -amp * 0.8, freq, -0.12, -lift * 0.6)]
    clips = {
        "idle": wings(2.2, 38, 12) + [bob("body", 1.2, 1.1), rot("body", "x", 5, 0.55, 0, -12)],
        "dive": wings(4.0, 16, 38) + [rot("body", "x", 3, 2.0, 0, 28)],
        "rest": wings(0.3, 4, 62),
    }
    return parts, {"parts": rig, "clips": clips}


def echo_sentinel():
    """Old-builder stonework held aloft by gravity magick around a lit core. Guards the ruins."""
    old = mix(ramp("stone", 4, 0.4, 1), flat("stone_5"), 0.12, 2)

    def moss(v, seed):
        v.repaint(ramp("green", 3, 0.5, seed), lambda x, y, z: not v.has(x, y + 1, z) and _hash(x, y, z, seed) < 0.35)

    torso = Vox()
    for y in range(20, 34):                                                # a broad slab, wider at the shoulders
        half = 5 + (y - 20) * 0.28
        torso.box(-int(half), y, -4, int(half) - 1, y, 3, old)
    torso.carve_box(-3, 23, -1, 2, 30, 3)                                  # hollow chest, open at the back
    torso.box(-2, 24, -1, 1, 29, 2, flat("glow_teal"))                     # the core
    torso.box(-3, 26, -4, 2, 27, -4, flat("bronze_2"))                     # bronze band across the front
    for (x, y) in ((-1, 30), (0, 30), (-1, 22), (0, 22), (-2, 28), (1, 28), (-1, 25), (0, 25)):
        torso.set(x, y, -4, flat("glow_teal"))                             # inscription on the chest
    moss(torso, 3)
    for y in range(12, 19):                                                # loose keystone hanging below
        half = (y - 11) * 0.55
        torso.box(-int(half) - 1, y, -int(half) - 1, int(half), y, int(half), old)

    head = Vox()
    head.box(-3, 36, -3, 2, 41, 2, old)
    head.box(-4, 40, -4, 3, 41, 3, flat("stone_3"))                        # cap stone
    head.box(-2, 38, -3, 1, 38, -3, flat("glow_teal"))                     # single eye slit
    moss(head, 5)

    parts = [("torso", torso), ("head", head)]
    rig = {"torso": {"parent": "", "pivot": [0, 26, 0]}, "head": {"parent": "torso", "pivot": [0, 36, 0]}}
    for name, s in (("l", -1), ("r", 1)):
        x0 = -14 if s < 0 else 9
        arm = Vox()
        arm.box(x0 + 1, 26, -2, x0 + 3, 31, 1, old)                        # upper block, not touching the body
        arm.box(x0, 13, -3, x0 + 4, 22, 2, old)                            # great fist
        arm.box(x0, 17, -3, x0 + 4, 18, 2, flat("bronze_2"), shell=True)
        arm.box(x0 + 1, 15, -3, x0 + 3, 15, -3, flat("glow_teal"))
        moss(arm, 7 + s)
        parts.append((f"arm_{name}", arm))
        rig[f"arm_{name}"] = {"parent": "torso", "pivot": [x0 + 2.5, 31, 0]}
    orbit = Vox()
    for i in range(7):                                                     # ring of rubble circling the base
        a = i * math.tau / 7
        r = 9 + (i % 2) * 1.5
        size = 1.6 + (i % 3) * 0.5
        orbit.ellipsoid(math.cos(a) * r, 6 + (i % 3) * 1.5, math.sin(a) * r, size, size, size, old)
    parts.append(("orbit", orbit))
    rig["orbit"] = {"parent": "", "pivot": [0, 6, 0]}

    clips = {
        "idle": [bob("torso", 1.2, 0.35), bob("head", 0.5, 0.35, 0.3), bob("arm_l", 0.9, 0.35, 0.55), bob("arm_r", 0.9, 0.35, 0.7),
                 rot("orbit", "y", 180, 0.05), bob("orbit", 0.6, 0.5), rot("head", "y", 14, 0.12)],
        "walk": [bob("torso", 0.8, 0.6), rot("torso", "x", 2, 0.6, 0, 8), rot("arm_l", "x", 10, 0.6), rot("arm_r", "x", 10, 0.6, 0.5),
                 rot("orbit", "y", 180, 0.12), bob("head", 0.4, 0.6, 0.3)],
        "slam": [rot("arm_l", "x", 80, 0.7, 0, 70), rot("arm_r", "x", 80, 0.7, 0, 70), rot("torso", "x", 10, 0.7, 0.05, 6),
                 bob("torso", 1.5, 0.7, 0.5), rot("orbit", "y", 180, 0.3)],
        "dormant": [rot("arm_l", "z", 0.5, 0.1, 0, -6), rot("arm_r", "z", 0.5, 0.1, 0, 6), rot("head", "x", 0.5, 0.1, 0, 18)],
    }
    return parts, {"parts": rig, "clips": clips}


MOBS = {f.__name__: f for f in (reedhopper, glassback, duskmoth, echo_sentinel)}
