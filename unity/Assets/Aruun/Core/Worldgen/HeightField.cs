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

        public HeightField(int seed) { Seed = seed; }

        /// <summary>Ground height in voxels at column (x, z).</summary>
        public int HeightAt(int x, int z)
        {
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

        public bool IsMesa(int x, int z) { return MesaMask(x, z) > 20972; } // 0.32

        /// <summary>0..65536: how overgrown the land is here (forest density, flowers).</summary>
        public int Lushness(int x, int z)
        {
            long v = (((long)Value(x, z, 166, 400) * 52429) >> 16) + 36045; // n * 0.8 + 0.55
            return v < 0 ? 0 : v > One ? One : (int)v;
        }

        /// <summary>Block key for the top voxel of a column of height h.</summary>
        public string SurfaceKey(int x, int z, int h)
        {
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

        /// <summary>Small plant on top of a grass column, or null for none.</summary>
        public string FloraAt(int x, int z)
        {
            int r = Hash(x, z, 11);
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
            if (Hash(cx, cz, 21) > ((long)Lushness(ox, oz) * 55706) >> 16) return t; // lush * 0.85
            t.X = ox + 3 + ((Hash(cx, cz, 22) * (TreeCell - 6)) >> 16);
            t.Z = oz + 3 + ((Hash(cx, cz, 23) * (TreeCell - 6)) >> 16);
            int h = HeightAt(t.X, t.Z);
            if (h <= SeaLevel + 1 || IsMesa(t.X, t.Z)) return t;
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
