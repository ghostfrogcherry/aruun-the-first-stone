using System;
using System.Collections.Generic;
using Aruun.Core.Combat;
using Aruun.Core.Content;
using Aruun.Core.Ruins;
using Aruun.Core.Voxels;
using Aruun.Core.Worldgen;

namespace Aruun.Core.Town
{
    /// <summary>One building of the town: its footprint and floor, in world voxels.</summary>
    public sealed class VillageBuilding
    {
        public string Key = "";
        public int X, Z, Floor;
        public int MinX, MaxX, MinZ, MaxZ, Height;
    }

    /// <summary>A door into a place that is somewhere else (the college's library): E at one end takes you to the other.</summary>
    public sealed class Portal
    {
        public string Name = "";
        /// <summary>Where the door is, and where you arrive, in metres.</summary>
        public Vec3 At, To;
        /// <summary>The way you face on arrival (radians, 0 = north).</summary>
        public double ToYaw;
        public string Message = "";
    }

    /// <summary>
    /// The starting town at the Teshari Confluence, in the style of the architecture board
    /// (art/architecture in the project files): timber-framed houses on stone bases under blue
    /// tiled roofs, a fountain plaza with a blue crystal, lamp posts, market stalls, a bell
    /// tower and a ring wall with towers and gates. North of the wall stands the Grand College;
    /// its library is a separate hall high above the world, reached through the college door.
    /// A paved road runs south to the echo ruin.
    ///
    /// All of it is part of world generation: pure, the same for the same seed, stamped into
    /// each chunk. The game sets it on the generator before any chunk is made. Voxels, north +Z.
    /// </summary>
    public sealed class Village
    {
        public const int PlazaRadius = 48;
        public const int RoadHalf = 7;
        public const int WallRadius = 176;
        public const int WallHalf = 4;
        public const int WallHeight = 24;
        public const int GateHalf = 12;
        /// <summary>The college's middle, and the library's floor height (300 m up, far above everything).</summary>
        public const int CollegeZ = 340;
        public const int LibraryY = 1200;

        public readonly List<VillageBuilding> Buildings = new List<VillageBuilding>();
        /// <summary>Named spots (where people live), in metres. Y = 0 means "on the ground there".</summary>
        public readonly Dictionary<string, Vec3> Spots = new Dictionary<string, Vec3>();
        public readonly List<Portal> Portals = new List<Portal>();
        /// <summary>Warm lamps, in metres, for the Game layer to light.</summary>
        public readonly List<Vec3> Lamps = new List<Vec3>();
        /// <summary>Resonance crystals (the fountain, the spire, the library dais): a faint teal light each.</summary>
        public readonly List<Vec3> CrystalLights = new List<Vec3>();

        readonly HeightField _field;
        readonly Blueprint _bp;
        readonly ushort _paving, _wall, _moss, _air;
        readonly int _roadX0, _roadZ0, _roadX1, _roadZ1;
        int _collegeFloor, _terrace;

        /// <summary>The town for this world. contentRoot is kept for symmetry with other content loads.</summary>
        public static Village Load(string contentRoot, HeightField field, BlockRegistry blocks, EchoRuin ruin)
        {
            return new Village(field, blocks, ruin);
        }

        public Village(HeightField field, BlockRegistry blocks, EchoRuin ruin)
        {
            _field = field;
            _bp = new Blueprint(blocks);
            _paving = _bp.Id("paving");
            _wall = _bp.Id("stone_brick");
            _moss = _bp.Id("mossy_stone");
            _air = 0;
            _roadX0 = 0; _roadZ0 = -PlazaRadius + 4;
            _roadX1 = ruin != null ? ruin.Ox : 0; _roadZ1 = ruin != null ? ruin.Oz + 3 : -PlazaRadius;

            Fountain();
            House(-60, 44, true);
            House(60, 44, true);
            House(-84, -6, false);
            House(84, -6, false);
            House(-58, -62, true);
            House(58, -62, true);
            House(-104, 84, false);
            House(104, 84, false);
            BellTower(-34, 100);
            Stall(-30, -22, _bp.Id("awning_cloth"));
            Stall(30, -22, _bp.Id("banner_blue"));
            PlazaLamps();
            RoadLamps();
            WallTowers();
            College();
            Library();

            Spots["elder"] = M(0, 24, 0);
            Spots["cook"] = M(-30, -8, 0);
            Spots["watcher"] = M(-34, 80, 0);
            Spots["archivist"] = M(10, 346, LibraryY);
        }

        static Vec3 M(int x, int z, int y) { return new Vec3((x + 0.5) / WorldConfig.VoxelsPerMeter, y / (double)WorldConfig.VoxelsPerMeter, (z + 0.5) / WorldConfig.VoxelsPerMeter); }

        int Ground(int x, int z) { return Math.Max(_field.HeightAt(x, z), HeightField.SeaLevel + 1); }

        ushort B(string key) { return _bp.Id(key); }

        /// <summary>Levels a pad for a footprint: solid below the floor, open air above it.</summary>
        void Pad(int x0, int z0, int x1, int z1, int floor, int height)
        {
            _bp.Box(x0, floor - 10, z0, x1, floor, z1, B("cobble"));
            _bp.Box(x0, floor + 1, z0, x1, floor + height, z1, _air);
        }

        /// <summary>A warm lamp. Amber light means people live here (visual guide); every lamp in town is warm.</summary>
        void Lamp(int x, int y, int z, bool indoors = false)
        {
            _bp.Box(x, y, z, x + 1, y + 1, z + 1, B("lamp_glow"));
            Lamps.Add(M(x + 1, z + 1, y + 1) + new Vec3(-0.125, 0, -0.125));
        }

        // ------------------------------------------------------------------ the plaza

        void Fountain()
        {
            int g = Ground(0, 0);
            // Clear the old waymark stone and level the fountain's footing.
            _bp.Box(-16, g - 6, -16, 15, g, 15, B("cobble"));
            _bp.Box(-16, g + 1, -16, 15, g + 24, 15, _air);
            _bp.Disc(0, g, 0, 16, 0, _paving);
            _bp.Disc(0, g + 1, 0, 14, 11, B("stone_brick"), 3);
            _bp.Disc(0, g + 4, 0, 14, 12, B("gold_trim"));
            _bp.Disc(0, g + 1, 0, 11, 0, B("water"), 2);
            _bp.Box(-3, g + 1, -3, 2, g + 7, 2, B("stone_brick"));
            _bp.Box(-4, g + 8, -4, 3, g + 8, 3, B("gold_trim"));
            _bp.Box(-1, g + 9, -1, 0, g + 18, 0, B("crystal_blue"));
            _bp.Box(-2, g + 11, -1, 1, g + 15, 0, B("crystal_blue"));
            _bp.Box(-1, g + 11, -2, 0, g + 15, 1, B("crystal_blue"));
            CrystalLights.Add(M(0, 0, g + 14));
        }

        void PlazaLamps()
        {
            for (int k = 0; k < 12; k++)
            {
                double a = k * Math.PI / 6 + Math.PI / 12;
                int x = (int)Math.Round(Math.Cos(a) * (PlazaRadius - 4)), z = (int)Math.Round(Math.Sin(a) * (PlazaRadius - 4));
                LampPost(x, z);
            }
        }

        void LampPost(int x, int z)
        {
            int g = Ground(x, z);
            _bp.Box(x - 1, g + 1, z - 1, x + 1, g + 1, z + 1, B("cobble"));
            _bp.Box(x, g + 2, z, x, g + 11, z, B("dark_timber"));
            _bp.Box(x - 1, g + 12, z - 1, x + 1, g + 12, z + 1, B("dark_timber"));
            Lamp(x, g + 13, z);
            _bp.Box(x - 1, g + 15, z - 1, x + 1, g + 15, z + 1, B("dark_timber"));
        }

        void RoadLamps()
        {
            for (int z = -PlazaRadius - 24; z > _roadZ1 + 16; z -= 40)
            {
                double t = (double)(z - _roadZ0) / (_roadZ1 - _roadZ0);
                int x = (int)Math.Round(_roadX0 + t * (_roadX1 - _roadX0));
                LampPost(x - RoadHalf - 3, z);
                LampPost(x + RoadHalf + 3, z);
            }
            for (int z = PlazaRadius + 24; z < CollegeZ - 80; z += 40)
            {
                LampPost(-RoadHalf - 3, z);
                LampPost(RoadHalf + 3, z);
            }
        }

        void Stall(int cx, int cz, ushort stripe)
        {
            int g = Ground(cx, cz);
            int x0 = cx - 8, x1 = cx + 7, z0 = cz - 5, z1 = cz + 4;
            foreach (int x in new[] { x0, x1 })
                foreach (int z in new[] { z0, z1 })
                    _bp.Box(x, g + 1, z, x, g + 11, z, B("dark_timber"));
            _bp.Box(x0 + 1, g + 1, z0 + 1, x1 - 1, g + 4, z0 + 2, B("plank"));
            // A striped awning, sloping down to the front.
            for (int x = x0 - 1; x <= x1 + 1; x++)
            {
                ushort b = ((x - x0) / 2) % 2 == 0 ? stripe : B("plaster");
                _bp.Box(x, g + 12, z0 - 1, x, g + 12, z0 + 2, b);
                _bp.Box(x, g + 13, z0 + 3, x, g + 13, z1 + 1, b);
            }
            Buildings.Add(new VillageBuilding { Key = "stall", X = cx, Z = cz, Floor = g, MinX = x0, MaxX = x1, MinZ = z0, MaxZ = z1, Height = 14 });
        }

        // ------------------------------------------------------------------ houses

        /// <summary>
        /// A timber-framed house: a cobble base, plaster walls crossed by dark timber, warm
        /// windows, a door facing the plaza and a steep blue roof with a gold ridge and a chimney.
        /// wide: the long side runs east-west.
        /// </summary>
        void House(int cx, int cz, bool wide)
        {
            int w = wide ? 32 : 24, d = wide ? 24 : 32;
            int x0 = cx - w / 2, x1 = x0 + w - 1, z0 = cz - d / 2, z1 = z0 + d - 1;
            int f = Ground(cx, cz);
            const int H = 24;
            Pad(x0 - 3, z0 - 3, x1 + 3, z1 + 3, f, H + 30);
            ushort cobble = B("cobble"), plaster = B("plaster"), timber = B("dark_timber"), win = B("window_glow");
            _bp.Box(x0, f, z0, x1, f, z1, B("plank"));
            _bp.Walls(x0, f + 1, z0, x1, f + 4, z1, 1, cobble, _moss);
            _bp.Walls(x0, f + 5, z0, x1, f + H, z1, 1, plaster);
            // The frame: corner posts, two beams round the walls, uprights between the windows.
            foreach (int x in new[] { x0, x1 })
                foreach (int z in new[] { z0, z1 })
                    _bp.Box(x, f + 1, z, x, f + H, z, timber);
            _bp.Walls(x0, f + 12, z0, x1, f + 12, z1, 1, timber);
            _bp.Walls(x0, f + H, z0, x1, f + H, z1, 1, timber);
            for (int x = x0 + 8; x < x1; x += 8) { _bp.Box(x, f + 5, z0, x, f + H, z0, timber); _bp.Box(x, f + 5, z1, x, f + H, z1, timber); }
            for (int z = z0 + 8; z < z1; z += 8) { _bp.Box(x0, f + 5, z, x0, f + H, z, timber); _bp.Box(x1, f + 5, z, x1, f + H, z, timber); }
            // Windows, two storeys, between the uprights.
            for (int x = x0 + 3; x + 1 < x1 - 1; x += 8)
                foreach (int y in new[] { f + 6, f + 16 })
                {
                    _bp.Box(x + 1, y, z0, x + 2, y + 3, z0, win);
                    _bp.Box(x + 1, y, z1, x + 2, y + 3, z1, win);
                }
            for (int z = z0 + 3; z + 1 < z1 - 1; z += 8)
                foreach (int y in new[] { f + 6, f + 16 })
                {
                    _bp.Box(x0, y, z + 1, x0, y + 3, z + 2, win);
                    _bp.Box(x1, y, z + 1, x1, y + 3, z + 2, win);
                }
            // The door, on the side that faces the plaza, in a timber frame.
            int dx = -Math.Sign(cx), dz = -Math.Sign(cz);
            if (Math.Abs(cx) * d > Math.Abs(cz) * w)
            {
                int x = dx > 0 ? x1 : x0;
                _bp.Box(x, f + 1, cz - 3, x, f + 10, cz + 2, timber);
                _bp.Box(x, f + 1, cz - 2, x, f + 9, cz + 1, _air);
                Lamp(x + dx, f + 11, cz - 6);
            }
            else
            {
                int z = dz > 0 ? z1 : z0;
                _bp.Box(cx - 3, f + 1, z, cx + 2, f + 10, z, timber);
                _bp.Box(cx - 2, f + 1, z, cx + 1, f + 9, z, _air);
                Lamp(cx - 6, f + 11, z + dz);
            }
            ushort roof = ((cx / 8 + cz / 8) & 1) == 0 ? B("roof_blue") : B("roof_blue_dark");
            int top = _bp.Gable(x0, z0, x1, z1, f + H + 1, roof, B("gold_trim"), plaster);
            // A chimney through the roof at one end.
            int chx = wide ? x1 - 5 : cx + 4, chz = wide ? cz + 4 : z1 - 5;
            _bp.Box(chx, f + H - 4, chz, chx + 2, top + 3, chz + 2, cobble, _moss);
            Buildings.Add(new VillageBuilding { Key = "house", X = cx, Z = cz, Floor = f, MinX = x0 - 3, MaxX = x1 + 3, MinZ = z0 - 3, MaxZ = z1 + 3, Height = top - f });
        }

        void BellTower(int cx, int cz)
        {
            int f = Ground(cx, cz);
            int x0 = cx - 12, x1 = cx + 11, z0 = cz - 12, z1 = cz + 11;
            const int H = 72;
            Pad(x0 - 2, z0 - 2, x1 + 2, z1 + 2, f, H + 50);
            ushort brick = B("stone_brick"), timber = B("dark_timber");
            _bp.Box(x0, f, z0, x1, f, z1, _paving);
            _bp.Walls(x0, f + 1, z0, x1, f + H, z1, 2, brick, _moss);
            _bp.Box(cx - 2, f + 1, z0, cx + 1, f + 10, z0 + 1, _air);
            _bp.Walls(x0 - 1, f + H + 1, z0 - 1, x1 + 1, f + H + 2, z1 + 1, 1, brick);
            _bp.Merlons(x0 - 1, z0 - 1, x1 + 1, z0 - 1, f + H + 3, brick);
            _bp.Merlons(x0 - 1, z1 + 1, x1 + 1, z1 + 1, f + H + 3, brick);
            _bp.Merlons(x0 - 1, z0 - 1, x0 - 1, z1 + 1, f + H + 3, brick);
            _bp.Merlons(x1 + 1, z0 - 1, x1 + 1, z1 + 1, f + H + 3, brick);
            // The belfry: open arches on each side under a blue pyramid roof.
            for (int s = 0; s < 4; s++)
            {
                int a = s % 2 == 0 ? cx - 4 : cz - 4;
                if (s == 0) _bp.Box(a, f + H - 16, z0, a + 7, f + H - 4, z0 + 1, _air);
                if (s == 1) _bp.Box(x1 - 1, f + H - 16, a, x1, f + H - 4, a + 7, _air);
                if (s == 2) _bp.Box(a, f + H - 16, z1 - 1, a + 7, f + H - 4, z1, _air);
                if (s == 3) _bp.Box(x0, f + H - 16, a, x0 + 1, f + H - 4, a + 7, _air);
            }
            _bp.Box(cx - 3, f + H - 14, cz - 3, cx + 2, f + H - 9, cz + 2, B("bronze"));
            _bp.Box(cx, f + H - 8, cz, cx, f + H, cz, timber);
            for (int y = f + 14; y < f + H - 20; y += 14)
            {
                _bp.Box(cx - 1, y, z0, cx, y + 3, z0, B("window_glow"));
                _bp.Box(x1, y, cz - 1, x1, y + 3, cz, B("window_glow"));
            }
            // A blue banner with a gold edge down the south face.
            _bp.Box(cx - 4, f + 28, z0 - 1, cx + 3, f + 52, z0 - 1, B("banner_blue"));
            _bp.Box(cx - 4, f + 53, z0 - 1, cx + 3, f + 53, z0 - 1, B("gold_trim"));
            _bp.Box(cx - 1, f + 38, z0 - 2, cx, f + 43, z0 - 2, B("gold_trim"));
            int top = _bp.Pyramid(x0 + 2, z0 + 2, x1 - 2, z1 - 2, f + H + 1, B("roof_blue"), B("gold_trim"), 0, 2);
            Lamp(cx - 6, f + 11, z0 - 1);
            Lamp(cx + 4, f + 11, z0 - 1);
            Buildings.Add(new VillageBuilding { Key = "bell_tower", X = cx, Z = cz, Floor = f, MinX = x0 - 2, MaxX = x1 + 2, MinZ = z0 - 2, MaxZ = z1 + 2, Height = top - f });
        }

        // ------------------------------------------------------------------ the walls

        /// <summary>The towers on the ring wall, between the gates.</summary>
        void WallTowers()
        {
            ushort brick = B("stone_brick");
            for (int k = 0; k < 8; k++)
            {
                double a = Math.PI / 8 + k * Math.PI / 4;
                int cx = (int)Math.Round(Math.Cos(a) * WallRadius), cz = (int)Math.Round(Math.Sin(a) * WallRadius);
                int g = Ground(cx, cz);
                int x0 = cx - 12, x1 = cx + 11, z0 = cz - 12, z1 = cz + 11;
                int top = g + WallHeight + 16;
                _bp.Box(x0, g - 8, z0, x1, top, z1, brick, _moss);
                _bp.Box(x0 + 2, top - 1, z0 + 2, x1 - 2, top, z1 - 2, _air);
                _bp.Merlons(x0, z0, x1, z0, top + 1, brick);
                _bp.Merlons(x0, z1, x1, z1, top + 1, brick);
                _bp.Merlons(x0, z0, x0, z1, top + 1, brick);
                _bp.Merlons(x1, z0, x1, z1, top + 1, brick);
                // A blue banner on the outward face.
                int ox = Math.Abs(Math.Cos(a)) > Math.Abs(Math.Sin(a)) ? (cx > 0 ? x1 + 1 : x0 - 1) : cx;
                int oz = Math.Abs(Math.Cos(a)) > Math.Abs(Math.Sin(a)) ? cz : (cz > 0 ? z1 + 1 : z0 - 1);
                if (ox == cx) _bp.Box(cx - 3, top - 22, oz, cx + 2, top - 6, oz, B("banner_blue"));
                else _bp.Box(ox, top - 22, cz - 3, ox, top - 6, cz + 2, B("banner_blue"));
                Lamp(cx, top + 1, cz);
                Buildings.Add(new VillageBuilding { Key = "wall_tower", X = cx, Z = cz, Floor = g, MinX = x0, MaxX = x1, MinZ = z0, MaxZ = z1, Height = top - g });
            }
        }

        /// <summary>Is this column part of the ring wall (and not a gate)?</summary>
        bool WallAt(int x, int z, out bool outer)
        {
            outer = false;
            int r2 = x * x + z * z;
            int lo = WallRadius - WallHalf, hi = WallRadius + WallHalf;
            if (r2 < lo * lo || r2 >= hi * hi) return false;
            outer = r2 >= WallRadius * WallRadius;
            return true;
        }

        bool Gate(int x, int z)
        {
            if (Math.Abs(x) <= GateHalf) return true;
            // Wherever a road crosses the wall there is a gate.
            return PavedRoad(x, z, GateHalf - RoadHalf);
        }

        // ------------------------------------------------------------------ the Grand College

        void College()
        {
            int cz = CollegeZ;
            int f = Ground(0, cz);
            for (int x = -64; x <= 64; x += 32)
                for (int z = cz - 60; z <= cz + 60; z += 30)
                    f = Math.Max(f, Ground(x, z) - 4);
            _collegeFloor = f;
            int t = f + 12;
            _terrace = t;
            ushort brick = B("stone_brick"), stone = B("college_stone"), gold = B("gold_trim"), banner = B("banner_blue"), roof = B("roof_blue"), roofD = B("roof_blue_dark"), glow = B("window_glow");
            // The terrace and the grand stair up to it from the north road.
            _bp.Box(-84, f - 12, cz - 52, 83, t, cz + 64, B("cobble"), _moss);
            _bp.Box(-84, t + 1, cz - 52, 83, t + 150, cz + 64, _air);
            _bp.Box(-80, t, cz - 48, 79, t, cz + 60, _paving);
            for (int i = 0; i < 12; i++)
            {
                int z = cz - 76 + 2 * i;
                _bp.Box(-16, f - 4, z, 15, f + 1 + i, z + 1, brick);
                _bp.Box(-16, f + 2 + i, z, 15, f + 40, z + 1, _air);
            }
            _bp.Box(-18, f - 4, cz - 76, -17, t + 3, cz - 53, brick);
            _bp.Box(16, f - 4, cz - 76, 17, t + 3, cz - 53, brick);
            for (int z = cz - 72; z <= cz - 56; z += 8) { Lamp(-18, f + 4 + (z - (cz - 76)) / 2, z); Lamp(16, f + 4 + (z - (cz - 76)) / 2, z); }

            // The main hall.
            int hx0 = -48, hx1 = 47, hz0 = cz - 20, hz1 = cz + 43;
            const int H = 64;
            _bp.Walls(hx0, t + 1, hz0, hx1, t + H, hz1, 2, stone);
            _bp.Walls(hx0 - 1, t + H, hz0 - 1, hx1 + 1, t + H + 1, hz1 + 1, 1, gold);
            _bp.Walls(hx0 - 1, t + 1, hz0 - 1, hx1 + 1, t + 6, hz1 + 1, 1, brick);
            _bp.Gable(hx0, hz0, hx1, hz1, t + H + 2, roofD, gold, stone, 3);
            // Tall blue windows along the sides and the back.
            for (int z = hz0 + 8; z < hz1 - 4; z += 10)
            {
                _bp.Box(hx0, t + 16, z, hx0 + 1, t + 44, z + 2, glow);
                _bp.Box(hx1 - 1, t + 16, z, hx1, t + 44, z + 2, glow);
            }
            for (int x = hx0 + 8; x < hx1 - 4; x += 12) _bp.Box(x, t + 16, hz1 - 1, x + 2, t + 44, hz1, glow);

            // The facade: the great door, its gold arch, banners.
            _bp.Box(-10, t + 1, hz0 - 1, 9, t + 32, hz0 - 1, gold);
            _bp.Box(-8, t + 1, hz0 - 1, 7, t + 30, hz0 + 1, B("college_door"));
            _bp.Box(-8, t + 31, hz0 - 1, 7, t + 31, hz0 - 1, gold);
            _bp.Box(-10, t + 36, hz0 - 2, 9, t + 60, hz0 - 2, banner);
            _bp.Box(-10, t + 61, hz0 - 2, 9, t + 61, hz0 - 2, gold);
            _bp.Disc(0, t + 48, hz0 - 3, 0, 0, gold);
            for (int y = 0; y <= 8; y++)
            {
                int half = 8 - Math.Abs(y - 4) * 2;
                _bp.Box(-half / 2 - 1, t + 44 + y, hz0 - 3, half / 2, t + 44 + y, hz0 - 3, B("rune_stone"));
            }
            foreach (int x in new[] { -40, -28, 22, 34 })
            {
                _bp.Box(x, t + 18, hz0 - 2, x + 5, t + 54, hz0 - 2, banner);
                _bp.Box(x, t + 55, hz0 - 2, x + 5, t + 55, hz0 - 2, gold);
                _bp.Box(x + 2, t + 36, hz0 - 3, x + 3, t + 39, hz0 - 3, B("rune_stone"));
            }
            Lamp(-14, t + 1, hz0 - 4, true);
            Lamp(12, t + 1, hz0 - 4, true);

            // Towers: two flanking the door, four at the corners, one spire with the crystal.
            Tower(-22, hz0 - 10, -13, hz0 - 1, t, 84, roof);
            Tower(12, hz0 - 10, 21, hz0 - 1, t, 84, roof);
            Tower(hx0 - 10, hz0 - 10, hx0 + 9, hz0 + 9, t, 96, roof);
            Tower(hx1 - 9, hz0 - 10, hx1 + 10, hz0 + 9, t, 96, roof);
            Tower(hx0 - 10, hz1 - 9, hx0 + 9, hz1 + 10, t, 104, roof);
            Tower(hx1 - 9, hz1 - 9, hx1 + 10, hz1 + 10, t, 104, roof);
            int spire = Tower(-12, cz + 8, 11, cz + 31, t, 132, roofD);
            _bp.Box(-1, spire, cz + 19, 0, spire + 16, cz + 20, B("crystal_blue"));
            CrystalLights.Add(M(0, cz + 20, spire + 10));

            Buildings.Add(new VillageBuilding { Key = "college", X = 0, Z = cz, Floor = t, MinX = -84, MaxX = 83, MinZ = cz - 52, MaxZ = cz + 64, Height = spire + 16 - t });
            Portals.Add(new Portal
            {
                Name = "the Grand College", At = M(0, hz0 - 1, t + 1), To = M(0, 310, LibraryY), ToYaw = 0,
                Message = "You step through the great door into the Grand College library.",
            });
        }

        /// <summary>A square college tower with a gold band, blue windows and a steep pyramid roof. Returns the roof's tip height.</summary>
        int Tower(int x0, int z0, int x1, int z1, int t, int h, ushort roof)
        {
            ushort stone = B("college_stone"), gold = B("gold_trim");
            _bp.Walls(x0, t + 1, z0, x1, t + h, z1, 2, stone);
            _bp.Walls(x0 - 1, t + h - 1, z0 - 1, x1 + 1, t + h, z1 + 1, 1, gold);
            int cx = (x0 + x1) / 2, cz = (z0 + z1) / 2;
            for (int y = t + 20; y < t + h - 12; y += 18)
            {
                _bp.Box(cx - 1, y, z0, cx, y + 8, z0, B("window_glow"));
                _bp.Box(x0, y, cz - 1, x0, y + 8, cz, B("window_glow"));
                _bp.Box(x1, y, cz - 1, x1, y + 8, cz, B("window_glow"));
            }
            return _bp.Pyramid(x0, z0, x1, z1, t + h + 1, roof, gold, 1, 3);
        }

        // ------------------------------------------------------------------ the library

        /// <summary>
        /// The library of the Grand College, after the reference art: a long dark hall of
        /// arches and bookshelves, galleries up both sides, rune circles glowing blue on the walls
        /// and a crystal on a railed dais in the middle. It is built 300 m up, closed on all
        /// sides, so it is a place of its own that only the college door leads to.
        /// </summary>
        void Library()
        {
            int y = LibraryY, x0 = -48, x1 = 47, z0 = 300, z1 = 399, top = y + 63;
            ushort stone = B("college_stone"), brick = B("stone_brick"), shelf = B("bookshelf"), timber = B("dark_timber"),
                gold = B("gold_trim"), rune = B("rune_stone"), plank = B("plank"), banner = B("banner_blue");
            _bp.Box(x0 - 3, y - 3, z0 - 3, x1 + 3, top + 3, z1 + 3, stone);
            _bp.Box(x0, y, z0, x1, top, z1, _air);
            _bp.Box(x0, y - 1, z0, x1, y - 1, z1, _paving);
            for (int z = z0 + 4; z < z1; z += 8) _bp.Box(-8, y - 1, z, 7, y - 1, z + 1, brick);

            // Shelves lining both long walls, below and above the galleries.
            for (int z = z0 + 2; z < z1 - 2; z += 16)
            {
                _bp.Box(x0, y, z, x0 + 1, y + 20, z + 11, shelf);
                _bp.Box(x1 - 1, y, z, x1, y + 20, z + 11, shelf);
                _bp.Box(x0, y + 25, z, x0 + 1, y + 44, z + 11, shelf);
                _bp.Box(x1 - 1, y + 25, z, x1, y + 44, z + 11, shelf);
            }
            // Galleries with timber railings, and a stair up to each.
            _bp.Box(x0, y + 24, z0 + 30, x0 + 15, y + 24, z1, plank);
            _bp.Box(x1 - 15, y + 24, z0 + 30, x1, y + 24, z1, plank);
            for (int z = z0 + 30; z <= z1; z += 3)
            {
                _bp.Box(x0 + 15, y + 25, z, x0 + 15, y + 28, z, timber);
                _bp.Box(x1 - 15, y + 25, z, x1 - 15, y + 28, z, timber);
            }
            _bp.Box(x0 + 15, y + 29, z0 + 30, x0 + 15, y + 29, z1, timber);
            _bp.Box(x1 - 15, y + 29, z0 + 30, x1 - 15, y + 29, z1, timber);
            for (int i = 0; i < 24; i++)
            {
                int z = z0 + 30 - 2 * (24 - i);
                if (z < z0) continue;
                _bp.Box(x0 + 2, y, z, x0 + 7, y + i, z + 1, brick);
                _bp.Box(x1 - 7, y, z, x1 - 2, y + i, z + 1, brick);
            }
            // Pillars down the hall, joined by gold beams under the vault.
            foreach (int x in new[] { -24, 20 })
                for (int z = z0 + 14; z < z1 - 6; z += 24)
                {
                    _bp.Box(x, y, z, x + 3, top, z + 3, stone);
                    _bp.Box(x - 1, y, z - 1, x + 4, y + 2, z + 4, brick);
                    _bp.Box(x + 1, y + 10, z - 1, x + 2, y + 22, z - 1, banner);
                    Lamp(x + 1, y + 3, z - 2, true);
                }
            _bp.Box(-24, top - 6, z0, -21, top - 6, z1, gold);
            _bp.Box(20, top - 6, z0, 23, top - 6, z1, gold);
            for (int z = z0 + 8; z < z1; z += 24) _bp.Box(x0, top - 4, z, x1, top - 3, z + 1, stone);

            // Rune circles: a great one on the far wall, smaller ones above the shelves.
            RuneCircle(0, y + 32, z1, 14, rune, gold, true);
            for (int z = z0 + 24; z < z1 - 8; z += 24)
            {
                RuneCircle(x0, y + 54, z, 6, rune, gold, false);
                RuneCircle(x1, y + 54, z, 6, rune, gold, false);
            }

            // The crystal on its dais.
            int cz = 350;
            _bp.Disc(0, y, cz, 10, 0, brick, 2);
            _bp.Disc(0, y + 2, cz, 9, 0, gold);
            _bp.Disc(0, y + 2, cz, 8, 0, B("water"));
            for (int k = 0; k < 24; k++)
            {
                double a = k * Math.PI / 12;
                if (k % 6 == 3) continue;
                int x = (int)Math.Round(Math.Cos(a) * 10), z = cz + (int)Math.Round(Math.Sin(a) * 10);
                _bp.Box(x, y + 2, z, x, y + 5, z, timber);
            }
            _bp.Disc(0, y + 2, cz, 4, 0, brick, 2);
            _bp.Disc(0, y + 4, cz, 3, 0, B("crystal_blue"), 18);
            _bp.Disc(0, y + 22, cz, 4, 0, gold);
            CrystalLights.Add(M(0, cz, y + 14));
            // Reading tables with lamps along the aisle.
            for (int z = z0 + 20; z < z1 - 10; z += 28)
                if (Math.Abs(z - cz) > 14)
                    foreach (int x in new[] { -12, 6 })
                    {
                        _bp.Box(x, y, z, x + 5, y + 3, z + 2, timber);
                        _bp.Box(x + 1, y + 4, z + 1, x + 1, y + 4, z + 1, B("lamp_glow"));
                        _bp.Box(x + 3, y + 4, z, x + 4, y + 4, z + 2, shelf);
                    }
            // The way out.
            _bp.Box(-6, y, z0 - 1, 5, y + 19, z0, B("college_door"));
            _bp.Box(-8, y + 20, z0, 7, y + 21, z0, gold);
            Lamp(-10, y + 4, z0 + 1, true);
            Lamp(8, y + 4, z0 + 1, true);
            for (int z = z0 + 40; z < z1; z += 40) { Lamp(x0 + 4, y + 25, z, true); Lamp(x1 - 5, y + 25, z, true); }

            Portals.Add(new Portal
            {
                Name = "the way out", At = M(0, z0 + 1, y), To = M(0, CollegeZ - 26, _terrace + 1), ToYaw = Math.PI,
                Message = "You step back out onto the college terrace.",
            });
        }

        /// <summary>A rune circle in a wall: a glowing ring with a six-pointed star inside, framed in gold.</summary>
        void RuneCircle(int cx, int cy, int c, int r, ushort rune, ushort gold, bool onZ)
        {
            Action<int, int, ushort> put = (a, b, block) =>
            {
                if (onZ) _bp.Set(cx + a, cy + b, c, block);
                else _bp.Set(c, cy + b, cx + a, block);
            };
            int steps = r * 8;
            for (int i = 0; i < steps; i++)
            {
                double ang = i * 2 * Math.PI / steps;
                put((int)Math.Round(Math.Cos(ang) * r), (int)Math.Round(Math.Sin(ang) * r), rune);
                put((int)Math.Round(Math.Cos(ang) * (r + 1)), (int)Math.Round(Math.Sin(ang) * (r + 1)), gold);
            }
            // Two triangles.
            for (int t = 0; t < 2; t++)
                for (int k = 0; k < 3; k++)
                {
                    double a0 = Math.PI / 2 + t * Math.PI + k * 2 * Math.PI / 3, a1 = a0 + 2 * Math.PI / 3;
                    double ax = Math.Cos(a0) * (r - 1), ay = Math.Sin(a0) * (r - 1), bx = Math.Cos(a1) * (r - 1), by = Math.Sin(a1) * (r - 1);
                    int n = r * 2;
                    for (int s = 0; s <= n; s++)
                        put((int)Math.Round(ax + (bx - ax) * s / n), (int)Math.Round(ay + (by - ay) * s / n), rune);
                }
        }

        // ------------------------------------------------------------------ queries

        /// <summary>Is this column a road (the plaza does not count)?</summary>
        bool PavedRoad(int x, int z, int extra = 0)
        {
            // North: straight to the college stair.
            if (Math.Abs(x) <= RoadHalf + extra && z >= PlazaRadius - 4 && z <= CollegeZ - 76) return true;
            double dx = _roadX1 - _roadX0, dz = _roadZ1 - _roadZ0;
            double len2 = dx * dx + dz * dz;
            if (len2 < 1) return false;
            double t = ((x - _roadX0) * dx + (z - _roadZ0) * dz) / len2;
            if (t < 0 || t > 1) return false;
            double px = _roadX0 + t * dx - x, pz = _roadZ0 + t * dz - z;
            int half = RoadHalf + extra;
            return px * px + pz * pz <= half * half;
        }

        /// <summary>Is this column paved: the plaza or a road?</summary>
        public bool Paved(int x, int z)
        {
            return x * x + z * z <= PlazaRadius * PlazaRadius || PavedRoad(x, z);
        }

        /// <summary>Is this point (metres) inside a building's footprint?</summary>
        public bool InBuilding(Vec3 p)
        {
            int x = (int)Math.Floor(p.X * WorldConfig.VoxelsPerMeter), z = (int)Math.Floor(p.Z * WorldConfig.VoxelsPerMeter);
            foreach (var b in Buildings) if (x >= b.MinX && x <= b.MaxX && z >= b.MinZ && z <= b.MaxZ) return true;
            return false;
        }

        /// <summary>Is this point (metres) in the library, the place behind the college door?</summary>
        public bool InLibrary(Vec3 p) { return p.Y * WorldConfig.VoxelsPerMeter > LibraryY - 40; }

        // ------------------------------------------------------------------ worldgen

        /// <summary>Writes the town into a freshly generated chunk: roads and the ring wall follow the ground; the buildings come from the blueprint.</summary>
        public void Stamp(VoxelChunk chunk, BlockRegistry blocks)
        {
            const int n = WorldConfig.ChunkSize;
            int ox = chunk.Coord.OriginX, oy = chunk.Coord.OriginY, oz = chunk.Coord.OriginZ;
            int reach = WallRadius + WallHalf + 2;
            bool nearTown = ox <= reach && ox + n > -reach && oz <= Math.Max(reach, CollegeZ - 60) && oz + n > Math.Min(-reach, _roadZ1 - 4);
            bool nearRoad = ox <= Math.Max(_roadX0, _roadX1) + RoadHalf && ox + n > Math.Min(_roadX0, _roadX1) - RoadHalf;
            if ((nearTown || nearRoad) && oy < 600)
            {
                for (int z = 0; z < n; z++)
                    for (int x = 0; x < n; x++)
                    {
                        int wx = ox + x, wz = oz + z;
                        bool paved = Paved(wx, wz);
                        bool outer;
                        bool wall = WallAt(wx, wz, out outer);
                        if (!paved && !wall) continue;
                        int h = _field.HeightAt(wx, wz);
                        if (wall)
                        {
                            bool gate = Gate(wx, wz);
                            int g = Math.Max(h, HeightField.SeaLevel);
                            // The wall's top: a walk on the inner half, merlons on the outer half.
                            int topY = g + WallHeight;
                            int ang = (int)Math.Floor((Math.Atan2(wz, wx) + Math.PI) * WallRadius / 2);
                            if (outer && (ang & 1) == 0) topY += 3;
                            int fromY = gate ? g + 18 : g - 6;
                            for (int wy = Math.Max(fromY, oy); wy <= Math.Min(topY, oy + n - 1); wy++)
                            {
                                ushort id = Blueprint.Hash(wx, wy, wz) % 6 == 0 ? _moss : _wall;
                                if (gate && wy == g + 18) id = blocks.Id("gold_trim");
                                chunk.Set(x, wy - oy, z, id);
                            }
                            if (gate)
                                for (int wy = Math.Max(g + 1, oy); wy <= Math.Min(g + 17, oy + n - 1); wy++) chunk.Set(x, wy - oy, z, _air);
                        }
                        if (paved && h > HeightField.SeaLevel)
                        {
                            int y = h - oy;
                            if (y >= 0 && y < n) chunk.Set(x, y, z, _paving);
                            if (!wall)
                                for (int dy = 1; dy <= 24; dy++)
                                {
                                    int yy = y + dy;
                                    if (yy >= 0 && yy < n) chunk.Set(x, yy, z, _air);
                                }
                        }
                    }
            }
            _bp.Stamp(chunk);
        }
    }
}
