using System;
using System.Collections.Generic;
using Aruun.Core.Combat;
using Aruun.Core.Content;
using Aruun.Core.Edits;
using Aruun.Core.Voxels;
using Aruun.Core.Worldgen;

namespace Aruun.Core.Ruins
{
    /// <summary>A word carved into the ruin. Reading it counts as one sighting at this place.</summary>
    public sealed class RuinInscription
    {
        /// <summary>The place id the sighting counts for. Two different places make a guess.</summary>
        public string Place = "";
        public string Word = "";
        /// <summary>The carved cells, inclusive box in world voxels.</summary>
        public int X0, Y0, Z0, X1, Y1, Z1;

        /// <summary>The middle of the carving, in metres.</summary>
        public Vec3 Point()
        {
            return new Vec3((X0 + X1 + 1) * 0.5 / WorldConfig.VoxelsPerMeter, (Y0 + Y1 + 1) * 0.5 / WorldConfig.VoxelsPerMeter, (Z0 + Z1 + 1) * 0.5 / WorldConfig.VoxelsPerMeter);
        }

        public bool Contains(int x, int y, int z)
        {
            return x >= X0 - 1 && x <= X1 + 1 && y >= Y0 - 1 && y <= Y1 + 1 && z >= Z0 - 1 && z <= Z1 + 1;
        }
    }

    /// <summary>
    /// Something in the ruin that answers a spoken word: a sealed gate, a stair that rises, the
    /// heart. Whether it has answered is read from the world itself (the edits it made are in the
    /// journal), so a reload never loses it.
    /// </summary>
    public sealed class RuinMechanism
    {
        public string Id = "";
        public string Name = "";
        /// <summary>The action word it answers to.</summary>
        public string Word = "";
        /// <summary>Where to aim, in metres.</summary>
        public Vec3 Point;
        /// <summary>A world flag that must be set first ("" for none), and what is said if it is not.</summary>
        public string Needs = "";
        public string NotYet = "";
        public string Answer = "";
        /// <summary>The edits it makes, in order, when it answers.</summary>
        public readonly List<EditOp> Ops = new List<EditOp>();
        /// <summary>A cell that tells whether it has answered: done when that cell is solid (DoneSolid) or empty.</summary>
        public int CheckX, CheckY, CheckZ;
        public bool DoneSolid;
        /// <summary>A world flag set when it answers ("" for none).</summary>
        public string Sets = "";
    }

    /// <summary>
    /// The echo ruin: a buried shrine of the old builders on the road south from the Teshari
    /// Confluence toward the Ruins of Avarra. It is the demo's dungeon. Three halls under the
    /// ground, each keyed by an Avarra word you learn inside it, then the sentinel, the heart,
    /// and a stair back up to the light.
    ///
    ///   arch and stair down   (read Vuh, "Away", on the arch)
    ///   antechamber           (Vuh again, and Ahm, "Rise"; a sealed gate answers Vuh)
    ///   hall of the step      (Ahm again; a carved socket raises a stair up a 4 m ledge)
    ///   sentinel's court      (Sem, "Hither", on two pillars; the Echo Sentinel; the heart
    ///                          answers Sem once the sentinel falls, and opens the way out;
    ///                          the old builders' inscription table by the east wall, with
    ///                          Ulun, "Ring", cut into its face. Ulun is also on the hall of
    ///                          the step's east wall, so a careful reader can guess it.)
    ///   stair up              (back to the surface beyond the court)
    ///
    /// It is part of world generation: pure and thread safe, the same for the same seed, and
    /// stamped into each chunk it touches. Doors opened later are ordinary journaled edits.
    /// All layout numbers are in voxels (4 per metre). Local axes: u to the right, v forward
    /// (south, world -Z), y up.
    /// </summary>
    public sealed class EchoRuin
    {
        /// <summary>How far south of the spawn place the arch stands, in voxels (90 m).</summary>
        public const int DistanceSouth = 360;
        /// <summary>Steps down from the arch to the first hall, one voxel each.</summary>
        public const int StepsDown = 40;
        /// <summary>The ruin's footprint, for skipping chunks quickly (voxels, local).</summary>
        const int HalfWidth = 40, Length = 480;

        public const string FlagSentinel = "sentinel_down";
        public const string FlagRemembered = "world_remembers";

        struct Box
        {
            public int X0, Y0, Z0, X1, Y1, Z1;
            public ushort Block;
        }

        readonly List<Box> _boxes = new List<Box>();
        readonly ushort _air, _stone, _paving, _lamp, _inscribed, _seal, _heart, _deep, _table;

        /// <summary>The arch, at ground level, in world voxels.</summary>
        public readonly int Ox, Oz, Ground;
        /// <summary>The first hall's floor: the first empty layer.</summary>
        public readonly int FloorY;
        /// <summary>The court's floor, one ledge higher.</summary>
        public readonly int CourtY;
        public readonly List<RuinInscription> Inscriptions = new List<RuinInscription>();
        public readonly List<RuinMechanism> Mechanisms = new List<RuinMechanism>();
        /// <summary>Glowing lamps, in metres, for the Game layer to light.</summary>
        public readonly List<Vec3> Lamps = new List<Vec3>();
        int _minX, _maxX, _minY, _maxY, _minZ, _maxZ;

        public Vec3 EntranceM { get { return ToM(0, Ground + 1, 4); } }
        public Vec3 GateM { get { return ToM(0, FloorY, 110); } }
        public Vec3 StepM { get { return ToM(0, FloorY, 150); } }
        public Vec3 SentinelHomeM { get { return ToM(0, CourtY, 236); } }
        public Vec3 CourtM { get { return ToM(0, CourtY, 240); } }
        public Vec3 HeartM { get { return ToM(0, CourtY + 26, 279); } }
        /// <summary>The top of the old builders' inscription table, by the court's east wall.</summary>
        public Vec3 TableM { get { return ToM(28.5, CourtY + 4, 238.5); } }
        public Vec3 ExitM { get; private set; }

        /// <summary>The ruin for this map, or null when the map has no spawn place to put it by.</summary>
        public static EchoRuin ForMap(HeightField field, BlockRegistry blocks)
        {
            if (field == null || field.Map == null || !blocks.Has("echo_seal")) return null;
            // The map puts the spawn place at the origin. Walk east until the arch is on dry land.
            int x = 0, z = -DistanceSouth;
            for (int i = 0; i < 40 && field.HeightAt(x, z) <= HeightField.SeaLevel + 4; i++) x += 24;
            return new EchoRuin(field, blocks, x, z);
        }

        public EchoRuin(HeightField field, BlockRegistry blocks, int ox, int oz)
        {
            _air = 0;
            _stone = blocks.Id("ancient_stone");
            _paving = blocks.Id("paving");
            _lamp = blocks.Id("lamp_glow");
            _inscribed = blocks.Id("inscribed_stone");
            _seal = blocks.Id("echo_seal");
            _heart = blocks.Id("echo_heart");
            _deep = blocks.Id("deep_stone");
            _table = blocks.Has("inscription_table") ? blocks.Id("inscription_table") : _stone;
            Ox = ox;
            Oz = oz;
            Ground = Math.Max(field.HeightAt(ox, oz), HeightField.SeaLevel + 1);
            FloorY = Ground + 1 - StepsDown;
            CourtY = FloorY + 16;
            _minX = _minY = _minZ = int.MaxValue;
            _maxX = _maxY = _maxZ = int.MinValue;
            Build(field);
        }

        // ------------------------------------------------------------------ coordinates

        int WX(int u) { return Ox + u; }
        int WZ(int v) { return Oz - v; }
        Vec3 ToM(double u, double y, double v) { return new Vec3((Ox + u + 0.5) / WorldConfig.VoxelsPerMeter, y / (double)WorldConfig.VoxelsPerMeter, (Oz - v + 0.5) / WorldConfig.VoxelsPerMeter); }

        void Put(int u0, int y0, int v0, int u1, int y1, int v1, ushort block)
        {
            var b = new Box
            {
                X0 = WX(Math.Min(u0, u1)), X1 = WX(Math.Max(u0, u1)),
                Y0 = Math.Min(y0, y1), Y1 = Math.Max(y0, y1),
                Z0 = WZ(Math.Max(v0, v1)), Z1 = WZ(Math.Min(v0, v1)),
                Block = block,
            };
            _boxes.Add(b);
            _minX = Math.Min(_minX, b.X0); _maxX = Math.Max(_maxX, b.X1);
            _minY = Math.Min(_minY, b.Y0); _maxY = Math.Max(_maxY, b.Y1);
            _minZ = Math.Min(_minZ, b.Z0); _maxZ = Math.Max(_maxZ, b.Z1);
        }

        EditOp Op(int u0, int y0, int v0, int u1, int y1, int v1, ushort block)
        {
            return EditOp.Box(WX(Math.Min(u0, u1)), Math.Min(y0, y1), WZ(Math.Max(v0, v1)), WX(Math.Max(u0, u1)), Math.Max(y0, y1), WZ(Math.Min(v0, v1)), block, 98);
        }

        void Carve(int u0, int y0, int v0, int u1, int y1, int v1, string place, string word)
        {
            Put(u0, y0, v0, u1, y1, v1, _inscribed);
            Inscriptions.Add(new RuinInscription
            {
                Place = place, Word = word,
                X0 = WX(Math.Min(u0, u1)), X1 = WX(Math.Max(u0, u1)), Y0 = y0, Y1 = y1,
                Z0 = WZ(Math.Max(v0, v1)), Z1 = WZ(Math.Min(v0, v1)),
            });
        }

        void Lamp(int u, int y, int v)
        {
            Put(u, y, v, u + 1, y + 1, v + 1, _lamp);
            Lamps.Add(ToM(u + 0.5, y + 1, v + 0.5));
        }

        // ------------------------------------------------------------------ layout

        void Build(HeightField field)
        {
            int g = Ground, f = FloorY, c = CourtY;
            // The parts in three passes: stone shells, then floors, then the empty insides, so
            // halls that meet open into each other. Features go last.
            var shells = new List<int[]>();
            var floors = new List<int[]>();
            var insides = new List<int[]>();
            Action<int, int, int, int, int, int> room = (u0, y0, v0, u1, y1, v1) =>
            {
                shells.Add(new[] { u0 - 2, y0 - 2, v0 - 2, u1 + 2, y1 + 2, v1 + 2 });
                floors.Add(new[] { u0, y0 - 1, v0, u1, y0 - 1, v1 });
                insides.Add(new[] { u0, y0, v0, u1, y1, v1 });
            };

            // The porch under the arch, then the stair down: each step a voxel lower, two deep.
            room(-6, g + 1, 0, 5, g + 14, 7);
            for (int k = 0; k < StepsDown; k++)
                room(-6, g - k, 8 + 2 * k, 5, g - k + 13, 9 + 2 * k);
            int stairEnd = 8 + 2 * StepsDown;          // 88
            // Antechamber, 12 x 10 m, 6 m high.
            room(-24, f, stairEnd, 23, f + 23, 127);
            // The gate passage into the hall of the step.
            room(-6, f, 128, 5, f + 13, 131);
            // Hall of the step, 12 x 16 m, 9 m high; its far half is a 4 m ledge.
            room(-24, f, 132, 23, f + 35, 195);
            // Passage at ledge height into the court.
            room(-6, c, 196, 5, c + 13, 199);
            // The sentinel's court, 18 x 20 m, 8 m high.
            room(-36, c, 200, 35, c + 31, 279);
            // The way out, behind the heart's wall, then a stair up until it meets the open air.
            room(-6, c, 280, 5, c + 13, 285);
            int v = 286, floorY = c;
            for (int k = 0; k < 80; k++, v += 2)
            {
                floorY++;
                room(-6, floorY, v, 5, floorY + 13, v + 1);
                if (floorY > field.HeightAt(WX(0), WZ(v + 2)) + 1) break;
            }
            room(-6, floorY, v + 2, 5, floorY + 13, v + 7);
            ExitM = ToM(0, floorY, v + 6);
            // Both ends open to the outside: no end wall at the arch or at the way out.
            insides.Add(new[] { -6, g + 1, -2, 5, g + 14, -1 });
            insides.Add(new[] { -6, floorY, v + 8, 5, floorY + 13, v + 9 });

            foreach (var b in shells) Put(b[0], b[1], b[2], b[3], b[4], b[5], _stone);
            foreach (var b in floors) Put(b[0], b[1], b[2], b[3], b[4], b[5], _paving);
            foreach (var b in insides) Put(b[0], b[1], b[2], b[3], b[4], b[5], _air);

            // The arch over the porch: two pillars and a lintel, standing up out of the ground.
            Put(-10, g + 1, 0, -7, g + 20, 3, _stone);
            Put(6, g + 1, 0, 9, g + 20, 3, _stone);
            Put(-10, g + 17, 0, 9, g + 20, 3, _stone);
            Carve(-7, g + 6, 1, -7, g + 7, 2, "ruin_arch", "word_push");
            Lamp(-10, g + 21, 1);
            Lamp(8, g + 21, 1);

            // Antechamber: Vuh on the left wall, Ahm on the right, lamps, and the sealed gate.
            Carve(-25, f + 6, 104, -25, f + 7, 105, "ruin_antechamber_left", "word_push");
            Carve(24, f + 6, 104, 24, f + 7, 105, "ruin_antechamber_right", "word_rise");
            Lamp(-25, f + 12, 94); Lamp(23, f + 12, 94); Lamp(-25, f + 12, 118); Lamp(23, f + 12, 118);
            Put(-6, f, 129, 5, f + 13, 130, _seal);
            var gate = new RuinMechanism
            {
                Id = "gate", Name = "the sealed gate", Word = "word_push",
                Point = ToM(0, f + 7, 129),
                Answer = "The seal shudders at the word and slides away into the dark.",
                CheckX = WX(0), CheckY = f + 1, CheckZ = WZ(129), DoneSolid = false,
            };
            gate.Ops.Add(Op(-6, f, 129, 5, f + 13, 130, _air));
            Mechanisms.Add(gate);

            // Hall of the step: the ledge, Ahm by the socket, and the socket itself.
            Put(-24, f, 164, 23, f + 15, 195, _stone);
            Put(-24, f - 1, 164, 23, f - 1, 195, _deep);
            Carve(-25, f + 6, 150, -25, f + 7, 151, "ruin_step_hall", "word_rise");
            Carve(24, f + 6, 150, 24, f + 7, 151, "ruin_step_hall_east", "word_sonic");
            Put(-1, f + 4, 163, 0, f + 5, 163, _inscribed);
            Lamp(-25, f + 12, 140); Lamp(23, f + 12, 140); Lamp(-25, f + 26, 180); Lamp(23, f + 26, 180);
            var stair = new RuinMechanism
            {
                Id = "stair", Name = "the carved socket", Word = "word_rise",
                Point = ToM(0, f + 5, 163),
                Answer = "Stone rises from the floor in steps, up to the ledge.",
                CheckX = WX(0), CheckY = f, CheckZ = WZ(134), DoneSolid = true,
            };
            // Fifteen steps, the top one a voxel under the ledge, out across the hall floor.
            for (int j = 0; j < 15; j++)
                stair.Ops.Add(Op(-4, f, 162 - 2 * j, 3, f + 14 - j, 163 - 2 * j, _stone));
            Mechanisms.Add(stair);

            // The court: four pillars, Sem on two of them, the heart high on the far wall.
            Put(-24, c, 216, -17, c + 31, 223, _stone);
            Put(16, c, 216, 23, c + 31, 223, _stone);
            Put(-24, c, 256, -17, c + 31, 263, _stone);
            Put(16, c, 256, 23, c + 31, 263, _stone);
            Carve(-17, c + 6, 219, -17, c + 7, 220, "ruin_court_west", "word_pull");
            Carve(16, c + 6, 259, 16, c + 7, 260, "ruin_court_east", "word_pull");
            Lamp(-37, c + 14, 210); Lamp(35, c + 14, 210); Lamp(-37, c + 14, 270); Lamp(35, c + 14, 270);
            // The inscription table: a stone block 1.5 x 1 m with a worktop of old-builder metal,
            // and Ulun cut into the face you walk up to.
            Put(26, c, 237, 31, c + 2, 240, _stone);
            Put(26, c + 3, 237, 31, c + 3, 240, _table);
            Carve(26, c + 1, 238, 26, c + 2, 239, "ruin_table", "word_sonic");
            Put(-2, c + 24, 280, 1, c + 27, 281, _heart);
            Put(-6, c, 281, 5, c + 13, 282, _seal);
            var heart = new RuinMechanism
            {
                Id = "heart", Name = "the Echo Heart", Word = "word_pull",
                Point = ToM(0, c + 26, 280),
                Needs = FlagSentinel,
                NotYet = "The heart hums, but it will not stir while its guardian stands.",
                Answer = "The heart tears loose and falls. Far above, the ruin remembers: the way out opens.",
                CheckX = WX(0), CheckY = c + 25, CheckZ = WZ(280), DoneSolid = false,
                Sets = FlagRemembered,
            };
            heart.Ops.Add(Op(-2, c + 24, 280, 1, c + 27, 281, _air));
            heart.Ops.Add(Op(-6, c, 281, 5, c + 13, 282, _air));
            Mechanisms.Add(heart);
        }

        // ------------------------------------------------------------------ queries

        public RuinMechanism Mechanism(string id)
        {
            foreach (var m in Mechanisms) if (m.Id == id) return m;
            return null;
        }

        /// <summary>Has this mechanism already answered, judging by the world?</summary>
        public static bool Done(VoxelWorld world, RuinMechanism m)
        {
            bool solid = world.Blocks.Get(world.Get(m.CheckX, m.CheckY, m.CheckZ)).Collision;
            return solid == m.DoneSolid;
        }

        public RuinInscription InscriptionAt(int x, int y, int z)
        {
            foreach (var i in Inscriptions) if (i.Contains(x, y, z)) return i;
            return null;
        }

        /// <summary>Is this point (metres) inside the ruin's halls, roughly?</summary>
        public bool Inside(Vec3 p)
        {
            int x = (int)Math.Floor(p.X * WorldConfig.VoxelsPerMeter), y = (int)Math.Floor(p.Y * WorldConfig.VoxelsPerMeter), z = (int)Math.Floor(p.Z * WorldConfig.VoxelsPerMeter);
            return x >= _minX && x <= _maxX && y >= _minY && y <= _maxY && z >= _minZ && z <= _maxZ && y < Ground - 4;
        }

        // ------------------------------------------------------------------ worldgen

        /// <summary>Writes the ruin's parts into a freshly generated chunk, in order.</summary>
        public void Stamp(VoxelChunk chunk)
        {
            const int n = WorldConfig.ChunkSize;
            int ox = chunk.Coord.OriginX, oy = chunk.Coord.OriginY, oz = chunk.Coord.OriginZ;
            if (ox > _maxX || ox + n <= _minX || oy > _maxY || oy + n <= _minY || oz > _maxZ || oz + n <= _minZ) return;
            foreach (var b in _boxes)
            {
                int x0 = Math.Max(b.X0, ox), x1 = Math.Min(b.X1, ox + n - 1);
                int y0 = Math.Max(b.Y0, oy), y1 = Math.Min(b.Y1, oy + n - 1);
                int z0 = Math.Max(b.Z0, oz), z1 = Math.Min(b.Z1, oz + n - 1);
                if (x0 > x1 || y0 > y1 || z0 > z1) continue;
                for (int y = y0; y <= y1; y++)
                    for (int z = z0; z <= z1; z++)
                        for (int x = x0; x <= x1; x++)
                            chunk.Set(x - ox, y - oy, z - oz, b.Block);
            }
        }
    }
}
