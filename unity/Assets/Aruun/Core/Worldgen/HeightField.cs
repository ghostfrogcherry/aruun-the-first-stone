using System;

namespace Aruun.Core.Worldgen
{
    /// <summary>A tree the seed places in a 5 m cell.</summary>
    public struct TreeSpot
    {
        public bool Exists;
        public int X, Z, Base, Trunk, Radius;
        public bool Crimson;
    }

    /// <summary>
    /// The shape of the land from the world seed: ground height, mesas, how lush a place is,
    /// where plants and trees stand. Aruun is a living world: meadows and forests over red
    /// soil, lakes in the lowlands, red mesas where the rock breaks through.
    ///
    /// Everything here is integer maths (16.16 fixed point), so the host and every client get
    /// exactly the same world from the same seed on any machine. Keep it that way: no float,
    /// no System.Random.
    /// </summary>
    public sealed class HeightField
    {
        public const int SeaLevel = 14;
        public const int TreeCell = 20;    // one possible tree per 5 m x 5 m cell
        public const int TreeReach = 12;   // widest canopy radius, for chunk overlap tests
        const int One = 65536;

        public readonly int Seed;
        /// <summary>The world map the land follows, or null for the endless procedural land.</summary>
        public readonly WorldMap Map;

        public HeightField(int seed, WorldMap map = null) { Seed = seed; Map = map; }

        /// <summary>Ground height in voxels at column (x, z).</summary>
        public int HeightAt(int x, int z)
        {
            if (Map != null) return MapHeight(x, z, Blend(x, z));
            long h = 28L * One + 64L * Fractal(x, z, 400, 4, 100) + 3L * Value(x, z, 33, 200);
            // Terraced red mesas: flat tops and steep sides where the mask is high.
            int m = MesaMask(x, z);
            if (m > One / 4)
            {
                long lift = (long)(m - One / 4) * 6;          // 0..1 in 16.16 after clamping
                if (lift > One) lift = One;
                long liftVoxels = (lift * 56) >> 16;
                h += (liftVoxels / 8) * 8 * One;
            }
            return (int)(h >> 16);
        }

        public bool IsMesa(int x, int z)
        {
            if (Map != null)
            {
                int w = Blend(x, z).Mesas;
                return w > 0 && ((long)MesaMask(x, z) * w >> 16) > 20972;
            }
            return MesaMask(x, z) > 20972; // 0.32
        }

        /// <summary>0..65536: how overgrown the land is here (forest density, flowers).</summary>
        public int Lushness(int x, int z)
        {
            if (Map != null)
            {
                long l = ((long)Blend(x, z).Lush * (45875 + ((Value(x, z, 166, 400) * 19661L) >> 16))) >> 16; // 0.4..1
                return l < 0 ? 0 : l > One ? One : (int)l;
            }
            long v = (((long)Value(x, z, 166, 400) * 52429) >> 16) + 36045; // n * 0.8 + 0.55
            return v < 0 ? 0 : v > One ? One : (int)v;
        }

        /// <summary>Block key for the top voxel of a column of height h.</summary>
        public string SurfaceKey(int x, int z, int h)
        {
            if (Map != null) return MapSurface(x, z, h);
            if (h <= SeaLevel + 1) return h >= SeaLevel - 2 ? "dust_sand" : "red_soil"; // beaches, red lake beds
            if (IsMesa(x, z)) return "red_soil";
            return "grass";
        }

        /// <summary>Block key at height y in a column with ground height h.</summary>
        public static string LayerAt(int y, int h, string surface)
        {
            if (y > h) return y <= SeaLevel ? "water" : "air";
            if (y == h) return surface;
            if (y > h - 6) return "red_soil";
            if (y > h - 40) return "rust_rock";
            return "deep_stone";
        }

        /// <summary>Small plant on a column whose top block is `surface`, or null.</summary>
        public string FloraOn(int x, int z, string surface)
        {
            if (surface == "grass" || surface == "moss") return FloraAt(x, z);
            if (Map != null && surface == "snow")
            {
                MapBiome b = Map.BiomeAt(Warp(x, z, 0), Warp(x, z, 1));
                if (b.Flowers != null && Hash(x, z, 11) < 9830) return b.Flowers;
            }
            return null;
        }

        /// <summary>Small plant on top of a grass column, or null for none.</summary>
        public string FloraAt(int x, int z)
        {
            int r = Hash(x, z, 11);
            if (Map != null)
            {
                MapBiome b = Map.BiomeAt(Warp(x, z, 0), Warp(x, z, 1));
                if (b.Flowers != null && r < 9830) return b.Flowers; // 15 %
            }
            long lush = Lushness(x, z);
            if (r < (lush * 262) >> 16) return "grinleaf";       // 0.4 %
            if (r < (lush * 1311) >> 16) return "gold_flower";   // 2 %
            if (r < (lush * 2294) >> 16) return "white_flower";  // 3.5 %
            if (r < (lush * 22938) >> 16) return "tall_grass";   // 35 %
            return null;
        }

        /// <summary>The tree in a cell, if the seed grows one there.</summary>
        public TreeSpot TreeInCell(int cx, int cz)
        {
            var t = new TreeSpot();
            int ox = cx * TreeCell, oz = cz * TreeCell;
            long chance = ((long)Lushness(ox, oz) * 55706) >> 16;                       // lush * 0.85
            if (Map != null) chance = (chance * Blend(ox, oz).Trees) >> 16;
            if (Hash(cx, cz, 21) > chance) return t;
            t.X = ox + 3 + ((Hash(cx, cz, 22) * (TreeCell - 6)) >> 16);
            t.Z = oz + 3 + ((Hash(cx, cz, 23) * (TreeCell - 6)) >> 16);
            int h = HeightAt(t.X, t.Z);
            if (h <= SeaLevel + 1 || IsMesa(t.X, t.Z)) return t;
            if (Map != null && MapSurface(t.X, t.Z, h) != "grass") return t;
            t.Exists = true;
            t.Base = h + 1;
            t.Trunk = 18 + ((Hash(cx, cz, 24) * 18) >> 16);
            t.Radius = 6 + ((Hash(cx, cz, 25) * 6) >> 16);
            t.Crimson = Hash(cx, cz, 26) < 19661; // 30 %
            return t;
        }

        /// <summary>Deterministic 0..65535 value for integer coordinates.</summary>
        public int Hash(int a, int b, int salt)
        {
            unchecked
            {
                uint h = (uint)(a * 73856093) ^ (uint)(b * 19349663) ^ (uint)(salt * 83492791) ^ (uint)Seed;
                h ^= h >> 13; h *= 1274126177u;
                h ^= h >> 16; h *= 2246822519u;
                h ^= h >> 15;
                return (int)(h & 0xFFFF);
            }
        }

        // ---- The world map ------------------------------------------------------------------

        /// <summary>Biome numbers blended between the four nearest map cells.</summary>
        public struct BiomeBlend
        {
            public int Base, Relief, Lush, Trees, Mesas, Canyons;
        }

        /// <summary>
        /// Column (x, z) moved by a slow wobble, so biome edges wander instead of following the
        /// square map cells.
        /// </summary>
        int Warp(int x, int z, int axis)
        {
            int shift = (Value(x, z, 240, 700 + axis) * 3 + Value(x, z, 61, 710 + axis)) / 1365; // about ±190 voxels
            return (axis == 0 ? x : z) + shift;
        }

        public BiomeBlend Blend(int x, int z)
        {
            int c = Map.Cell;
            int wx = Warp(x, z, 0), wz = Warp(x, z, 1);
            int gx = WorldMap.FloorDiv(wx, c), gz = WorldMap.FloorDiv(wz, c);
            long tx = (long)(wx - gx * c) * One / c, tz = (long)(wz - gz * c) * One / c;
            long sx = (tx * tx * (3L * One - 2 * tx)) >> 32;
            long sz = (tz * tz * (3L * One - 2 * tz)) >> 32;
            MapBiome a = Map.GridBiome(gx, gz), b = Map.GridBiome(gx + 1, gz);
            MapBiome d = Map.GridBiome(gx, gz + 1), e = Map.GridBiome(gx + 1, gz + 1);
            var r = new BiomeBlend();
            r.Base = Mix(a.Base, b.Base, d.Base, e.Base, sx, sz);
            r.Relief = Mix(a.Relief, b.Relief, d.Relief, e.Relief, sx, sz);
            r.Lush = Mix(a.Lush, b.Lush, d.Lush, e.Lush, sx, sz);
            r.Trees = Mix(a.Trees, b.Trees, d.Trees, e.Trees, sx, sz);
            r.Mesas = Mix(a.Mesas, b.Mesas, d.Mesas, e.Mesas, sx, sz);
            r.Canyons = Mix(a.Canyons, b.Canyons, d.Canyons, e.Canyons, sx, sz);
            return r;
        }

        static int Mix(int a, int b, int c, int d, long sx, long sz)
        {
            long top = a + (((long)(b - a) * sx) >> 16);
            long bottom = c + (((long)(d - c) * sx) >> 16);
            return (int)(top + (((bottom - top) * sz) >> 16));
        }

        int MapHeight(int x, int z, BiomeBlend bl)
        {
            long h = (long)(SeaLevel + bl.Base) * One
                   + (long)bl.Relief * Fractal(x, z, 400, 4, 100)
                   + 3L * Value(x, z, 33, 200);
            if (bl.Mesas > 0)
            {
                long m = ((long)MesaMask(x, z) * bl.Mesas) >> 16;
                if (m > One / 4)
                {
                    long lift = (m - One / 4) * 6;
                    if (lift > One) lift = One;
                    long liftVoxels = (lift * 56) >> 16;
                    h += (liftVoxels / 8) * 8 * One;
                }
            }
            if (bl.Canyons > 0)
            {
                // Winding gorges where a slow noise crosses zero.
                int n = Fractal(x, z, 360, 2, 500);
                int a = n < 0 ? -n : n;
                const int width = 5900; // 0.09
                if (a < width)
                {
                    long cut = (long)(width - a) * 96 / width;   // up to 24 m deep
                    h -= ((cut * bl.Canyons) >> 16) * One;
                }
            }
            foreach (MapPeak p in Map.Peaks)
            {
                long dx = x - p.X, dz = z - p.Z;
                long d2 = dx * dx + dz * dz, r = p.Radius;
                if (d2 >= r * r) continue;
                long t = (Isqrt(d2) << 16) / r;                       // 0 at the summit, 1 at the foot
                long s = One - t;
                long cone = (s * s >> 16) * (3L * One - 2 * s) >> 16;  // smoothstep: wide gentle shield
                long lift = p.Height * cone;
                if (t < p.Crater)
                {
                    long u = (t << 16) / p.Crater;                    // 0 centre, 1 rim
                    long dip = (One - ((u * u * u) >> 32)) * (p.Height / 3);
                    lift -= dip;
                }
                h += lift;
            }
            return (int)(h >> 16);
        }

        string MapSurface(int x, int z, int h)
        {
            MapBiome b = Map.BiomeAt(Warp(x, z, 0), Warp(x, z, 1));
            if (b.Surface == "snow") return "snow";
            if (h >= SeaLevel + 520 && b.Surface != "basalt") return "snow";     // above 130 m
            if (h <= SeaLevel + 1) return h >= SeaLevel - 2 || b.Base < 0 ? "dust_sand" : "red_soil";
            if (IsMesa(x, z)) return "red_soil";
            return b.Surface;
        }

        /// <summary>True where the sea freezes over (glacial and bloom-field water).</summary>
        public bool Frozen(int x, int z)
        {
            return Map != null && Map.BiomeAt(Warp(x, z, 0), Warp(x, z, 1)).Surface == "snow";
        }

        static long Isqrt(long v)
        {
            if (v <= 0) return 0;
            long r = (long)Math.Sqrt(v);                // exact for these sizes after the fix-ups
            while (r * r > v) r--;
            while ((r + 1) * (r + 1) <= v) r++;
            return r;
        }

        int MesaMask(int x, int z) { return Fractal(x, z, 250, 2, 300); }

        /// <summary>Smooth value noise in -65536..65536 with features about `period` voxels wide.</summary>
        int Value(int x, int z, int period, int salt)
        {
            if (period < 1) period = 1;
            int cx = FloorDiv(x, period), cz = FloorDiv(z, period);
            long tx = (long)(x - cx * period) * One / period;
            long tz = (long)(z - cz * period) * One / period;
            long sx = (tx * tx * (3L * One - 2 * tx)) >> 32;
            long sz = (tz * tz * (3L * One - 2 * tz)) >> 32;
            long a = Hash(cx, cz, salt), b = Hash(cx + 1, cz, salt);
            long c = Hash(cx, cz + 1, salt), d = Hash(cx + 1, cz + 1, salt);
            long top = a + (((b - a) * sx) >> 16);
            long bottom = c + (((d - c) * sx) >> 16);
            long v = top + (((bottom - top) * sz) >> 16);
            return (int)(v * 2 - One);
        }

        /// <summary>Several layers of value noise, each half the size and strength of the last.</summary>
        int Fractal(int x, int z, int period, int octaves, int salt)
        {
            long sum = 0, total = 0;
            for (int o = 0; o < octaves; o++)
            {
                sum += Value(x, z, period >> o, salt + o) >> o;
                total += One >> o;
            }
            return (int)(sum * One / total);
        }

        static int FloorDiv(int a, int b)
        {
            int q = a / b;
            return (a % b != 0 && (a < 0) != (b < 0)) ? q - 1 : q;
        }
    }
}
