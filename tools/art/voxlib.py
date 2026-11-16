"""Tiny voxel sculpting kit used by the asset generators.

Coordinates follow Godot: x right, y up, z towards the viewer. Things face -z.
A Vox is a sparse dict of (x, y, z) -> palette key such as "rust_3".
"""
import math
import struct

import palette as P


def _hash(x, y, z, seed=0):
    x, y, z = int(x * 16), int(y * 16), int(z * 16)
    h = (x * 374761393 + y * 668265263 + z * 2147483647 + seed * 1274126177) & 0xFFFFFFFF
    h = ((h ^ (h >> 13)) * 1274126177) & 0xFFFFFFFF
    return ((h ^ (h >> 16)) & 0xFFFF) / 65536.0


# --- Materials: functions (x, y, z) -> palette key ---------------------------

def flat(key):
    return lambda x, y, z: key


def ramp(name, level, jitter=0.3, seed=0):
    """A ramp colour with some voxels one step darker or lighter, for texture."""
    keys = P.RAMP_KEYS[name]

    def f(x, y, z):
        r = _hash(x, y, z, seed)
        d = -1 if r < jitter * 0.55 else (1 if r > 1.0 - jitter * 0.45 else 0)
        return keys[max(0, min(len(keys) - 1, level - 1 + d))]
    return f


def gradient(name, lo, hi, y0, y1, jitter=0.25, seed=0, axis=1):
    """Ramp level goes from lo (at y0) to hi (at y1) along an axis."""
    keys = P.RAMP_KEYS[name]

    def f(x, y, z):
        t = ((x, y, z)[axis] - y0) / max(1e-6, (y1 - y0))
        t = max(0.0, min(1.0, t))
        lv = lo + (hi - lo) * t + (_hash(x, y, z, seed) - 0.5) * 2.0 * jitter * 1.6
        return keys[max(0, min(len(keys) - 1, int(round(lv)) - 1))]
    return f


def mix(a, b, amount, seed=0):
    """Mostly material a, with a scattering of b."""
    return lambda x, y, z: (b if _hash(x, y, z, seed + 77) < amount else a)(x, y, z)


def _mat(m):
    return flat(m) if isinstance(m, str) else m


class Vox:
    def __init__(self):
        self.v = {}

    # -- basic ops
    def set(self, x, y, z, m):
        x, y, z = int(x), int(y), int(z)
        self.v[(x, y, z)] = _mat(m)(x, y, z)

    def erase(self, x, y, z):
        self.v.pop((int(x), int(y), int(z)), None)

    def has(self, x, y, z):
        return (x, y, z) in self.v

    def bounds(self):
        xs, ys, zs = zip(*self.v)
        return (min(xs), min(ys), min(zs)), (max(xs), max(ys), max(zs))

    def merge(self, other, dx=0, dy=0, dz=0):
        for (x, y, z), k in other.v.items():
            self.v[(x + dx, y + dy, z + dz)] = k
        return self

    def copy(self):
        o = Vox()
        o.v = dict(self.v)
        return o

    def mirror_x(self, about=0):
        """Returns a copy mirrored across the plane x = about (cells about-1 | about)."""
        o = Vox()
        for (x, y, z), k in self.v.items():
            o.v[(2 * about - 1 - x, y, z)] = k
        return o

    def rotated_y(self, quarter_turns=1):
        o = Vox()
        for (x, y, z), k in self.v.items():
            for _ in range(quarter_turns % 4):
                x, z = -z - 1, x
            o.v[(x, y, z)] = k
        return o

    def repaint(self, m, where=None):
        m = _mat(m)
        for p in list(self.v):
            if where is None or where(*p):
                self.v[p] = m(*p)

    def surface_cells(self):
        n6 = ((1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1))
        return [p for p in self.v if any((p[0] + a, p[1] + b, p[2] + c) not in self.v for a, b, c in n6)]

    def hollow(self):
        """Removes voxels that can never be seen (all six neighbours solid)."""
        keep = set(self.surface_cells())
        self.v = {p: k for p, k in self.v.items() if p in keep}
        return self

    # -- shapes (inclusive integer bounds)
    def box(self, x0, y0, z0, x1, y1, z1, m, shell=False):
        m = _mat(m)
        for x in range(x0, x1 + 1):
            for y in range(y0, y1 + 1):
                for z in range(z0, z1 + 1):
                    if shell and x0 < x < x1 and y0 < y < y1 and z0 < z < z1:
                        continue
                    self.v[(x, y, z)] = m(x, y, z)
        return self

    def carve_box(self, x0, y0, z0, x1, y1, z1):
        for x in range(x0, x1 + 1):
            for y in range(y0, y1 + 1):
                for z in range(z0, z1 + 1):
                    self.v.pop((x, y, z), None)
        return self

    def ellipsoid(self, cx, cy, cz, rx, ry, rz, m, shell=0.0, ymin=None, ymax=None, carve=False):
        """Centre in voxel units (may be x.5), radii in voxels. shell > 0 keeps a wall that thick."""
        m = _mat(m)
        for x in range(int(math.floor(cx - rx)), int(math.ceil(cx + rx)) + 1):
            for y in range(int(math.floor(cy - ry)), int(math.ceil(cy + ry)) + 1):
                if (ymin is not None and y < ymin) or (ymax is not None and y > ymax):
                    continue
                for z in range(int(math.floor(cz - rz)), int(math.ceil(cz + rz)) + 1):
                    px, py, pz = x + 0.5 - cx, y + 0.5 - cy, z + 0.5 - cz
                    d = (px / rx) ** 2 + (py / ry) ** 2 + (pz / rz) ** 2
                    if d > 1.0:
                        continue
                    if shell > 0.0:
                        d2 = (px / max(0.01, rx - shell)) ** 2 + (py / max(0.01, ry - shell)) ** 2 \
                            + (pz / max(0.01, rz - shell)) ** 2
                        if d2 <= 1.0:
                            continue
                    if carve:
                        self.v.pop((x, y, z), None)
                    else:
                        self.v[(x, y, z)] = m(x, y, z)
        return self

    def cylinder(self, cx, cz, y0, y1, r0, r1, m, shell=0.0, carve=False):
        """Vertical (tapered) cylinder: radius r0 at y0, r1 at y1."""
        m = _mat(m)
        for y in range(y0, y1 + 1):
            t = 0.0 if y1 == y0 else (y - y0) / (y1 - y0)
            r = r0 + (r1 - r0) * t
            for x in range(int(math.floor(cx - r)), int(math.ceil(cx + r)) + 1):
                for z in range(int(math.floor(cz - r)), int(math.ceil(cz + r)) + 1):
                    d = math.hypot(x + 0.5 - cx, z + 0.5 - cz)
                    if d > r or (shell > 0.0 and d <= r - shell):
                        continue
                    if carve:
                        self.v.pop((x, y, z), None)
                    else:
                        self.v[(x, y, z)] = m(x, y, z)
        return self

    def line(self, p0, p1, r0, m, r1=None):
        """A thick line (capsule) from p0 to p1; radius can taper from r0 to r1."""
        m = _mat(m)
        r1 = r0 if r1 is None else r1
        steps = int(max(abs(p1[0] - p0[0]), abs(p1[1] - p0[1]), abs(p1[2] - p0[2])) * 2) + 1
        for i in range(steps + 1):
            t = i / steps
            c = [p0[k] + (p1[k] - p0[k]) * t for k in range(3)]
            r = r0 + (r1 - r0) * t
            if r <= 0.5:
                x, y, z = int(math.floor(c[0])), int(math.floor(c[1])), int(math.floor(c[2]))
                self.v[(x, y, z)] = m(x, y, z)
                continue
            for x in range(int(math.floor(c[0] - r)), int(math.ceil(c[0] + r)) + 1):
                for y in range(int(math.floor(c[1] - r)), int(math.ceil(c[1] + r)) + 1):
                    for z in range(int(math.floor(c[2] - r)), int(math.ceil(c[2] + r)) + 1):
                        if (x + 0.5 - c[0]) ** 2 + (y + 0.5 - c[1]) ** 2 + (z + 0.5 - c[2]) ** 2 <= r * r:
                            self.v[(x, y, z)] = m(x, y, z)
        return self


# --- .vox writer (MagicaVoxel, with scene graph so parts open assembled) -----

def _chunk(cid, content=b"", children=b""):
    return cid + struct.pack("<ii", len(content), len(children)) + content + children


def _str(s):
    b = s.encode()
    return struct.pack("<i", len(b)) + b


def _dict(d):
    return struct.pack("<i", len(d)) + b"".join(_str(k) + _str(v) for k, v in d.items())


def write_vox(path, parts):
    """parts: list of (name, Vox) in shared ("assembled") coordinates."""
    models = b""
    nodes = []
    child_ids = []
    for i, (name, vox) in enumerate(parts):
        (x0, y0, z0), (x1, y1, z1) = vox.bounds()
        sx, sy, sz = x1 - x0 + 1, y1 - y0 + 1, z1 - z0 + 1
        assert max(sx, sy, sz) <= 256, f"{name}: larger than 256 voxels"
        cells = bytearray()
        for (x, y, z), key in sorted(vox.v.items()):
            # Godot (x, y, z) -> MagicaVoxel (x, depth, up)
            cells += bytes((x - x0, (sz - 1) - (z - z0), y - y0, P.INDEX[key]))
        models += _chunk(b"SIZE", struct.pack("<iii", sx, sz, sy))
        models += _chunk(b"XYZI", struct.pack("<i", len(vox.v)) + bytes(cells))
        tx = x0 + sx // 2
        ty = -z0 - (sz - 1) + sz // 2
        tz = y0 + sy // 2
        trn, shp = 2 + 2 * i, 3 + 2 * i
        child_ids.append(trn)
        nodes.append(_chunk(b"nTRN", struct.pack("<i", trn) + _dict({"_name": name})
                            + struct.pack("<iiii", shp, -1, 0, 1) + _dict({"_t": f"{tx} {ty} {tz}"})))
        nodes.append(_chunk(b"nSHP", struct.pack("<i", shp) + _dict({})
                            + struct.pack("<i", 1) + struct.pack("<i", i) + _dict({})))
    root = _chunk(b"nTRN", struct.pack("<i", 0) + _dict({}) + struct.pack("<iiii", 1, -1, -1, 1) + _dict({}))
    grp = _chunk(b"nGRP", struct.pack("<i", 1) + _dict({}) + struct.pack("<i", len(child_ids))
                 + b"".join(struct.pack("<i", c) for c in child_ids))
    layer = _chunk(b"LAYR", struct.pack("<i", 0) + _dict({"_name": "aruun"}) + struct.pack("<i", -1))
    rgba = _chunk(b"RGBA", b"".join(bytes(c) for c in P.rgba_table()))
    body = models + root + grp + b"".join(nodes) + layer + rgba
    with open(path, "wb") as f:
        f.write(b"VOX " + struct.pack("<i", 200) + _chunk(b"MAIN", b"", body))


# --- Isometric preview renderer ----------------------------------------------

def render(parts, scale=6, view="front", bg=None):
    """Draws parts (list of Vox, assembled coords) as an isometric image. Needs Pillow."""
    from PIL import Image, ImageDraw
    cells = {}
    for vox in parts:
        cells.update(vox.v)
    if view == "front":   # look at the -z (front) and +x sides
        cells = {(x, y, -z): k for (x, y, z), k in cells.items()}
    s = scale

    def proj(x, y, z):
        return ((x - z) * s, (x + z) * s * 0.5 - y * s)
    pts = [proj(x + a, y + b, z + c) for (x, y, z) in cells for a in (0, 1) for b in (0, 1) for c in (0, 1)]
    minx = min(p[0] for p in pts) - 4
    miny = min(p[1] for p in pts) - 4
    w = int(max(p[0] for p in pts) - minx + 5)
    h = int(max(p[1] for p in pts) - miny + 5)
    img = Image.new("RGBA", (w, h), bg or (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    def poly(points, colour):
        d.polygon([(px - minx, py - miny) for px, py in points], fill=colour)

    def shade(key, f, x, y, z):
        r, g, b = P.rgb(key)
        if P.COLORS[P.INDEX[key] - 1]["emissive"]:
            f = 1.0
        else:  # cheap ambient occlusion: darker when hemmed in from above
            f *= 1.0 - 0.07 * sum((x + a, y + 1, z + c) in cells for a, c in ((1, 0), (0, 1), (-1, 0), (0, -1)))
        return (min(255, int(r * f)), min(255, int(g * f)), min(255, int(b * f)), 255)

    for (x, y, z) in sorted(cells, key=lambda p: (p[0] + p[1] + p[2], p[1])):
        k = cells[(x, y, z)]
        if (x, y + 1, z) not in cells:
            poly([proj(x, y + 1, z), proj(x + 1, y + 1, z), proj(x + 1, y + 1, z + 1), proj(x, y + 1, z + 1)],
                 shade(k, 1.06, x, y, z))
        if (x + 1, y, z) not in cells:
            poly([proj(x + 1, y, z), proj(x + 1, y + 1, z), proj(x + 1, y + 1, z + 1), proj(x + 1, y, z + 1)],
                 shade(k, 0.66, x, y, z))
        if (x, y, z + 1) not in cells:
            poly([proj(x, y, z + 1), proj(x + 1, y, z + 1), proj(x + 1, y + 1, z + 1), proj(x, y + 1, z + 1)],
                 shade(k, 0.84, x, y, z))
    return img
