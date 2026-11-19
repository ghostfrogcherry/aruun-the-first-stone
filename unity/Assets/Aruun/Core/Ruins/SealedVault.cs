using System;
using System.Collections.Generic;
using Aruun.Core.Combat;
using Aruun.Core.Content;
using Aruun.Core.Voxels;
using Aruun.Core.Worldgen;

namespace Aruun.Core.Ruins
{
    /// <summary>
    /// The sealed vault (placeholder name): the first discovery chain after the echo ruin.
    /// A rocky knoll east of the ruin, studded with singing quartz, with a door of dressed
    /// stone in its north face that no ordinary pick can break.
    ///
    ///   the knoll       singing quartz in its faces: a plain pick shatters it to dust, a
    ///                   ringing Ulun pick takes it whole. Three pieces tune the pick at an
    ///                   inscription table.
    ///   the door        dressed stone: only a tuned pick, ringing, can break it. Drai
    ///                   ("Split", proposed) is carved on the lintel above it.
    ///   the quartz hall a stair down, Drai again on the east wall (now you can guess it),
    ///                   two nightstalkers that lair here, and a 5 m ledge at the far end
    ///                   with a socket that answers Ahm: a stair of stone rises.
    ///   the seam gallery above the ledge: embersteel ore (placeholder) in the walls, too
    ///                   hard for flint, and an old worktable. Ulun and Drai cut together
    ///                   split stone along its seams, one tier harder than the pick could.
    ///                   The same answer opens a stair back up to the light.
    ///
    /// Generated with the world like the echo ruin: pure, the same for the same seed. The door
    /// is opened by digging and the lift by speaking, so both live in the edit journal.
    /// </summary>
    public sealed class SealedVault : RuinSite
    {
        /// <summary>Where the vault sits from the echo ruin's arch, in voxels.</summary>
        public const int EastOfRuin = 300, SouthOfRuin = 40;
        public const int StepsDown = 48;
        /// <summary>The ledge at the far end of the hall, in voxels (5 m).</summary>
        public const int LedgeHeight = 20;

        public const string FlagWoken = "vault_woken";
        public const string WordSplit = "word_fracture";

        readonly ushort _stone, _moss, _rock, _paving, _dressed, _quartz, _ore, _rune, _deep, _table, _seal, _grass;

        /// <summary>The hall's floor (first empty layer) and the gallery's, one ledge higher.</summary>
        public readonly int HallY, GalleryY;

        public Vec3 DoorM { get { return ToM(0, Ground + 6, -3); } }
        public Vec3 FrontM { get { return ToM(0, Ground + 1, -8); } }
        /// <summary>A cluster of singing quartz on the knoll's north face, the first one you see.</summary>
        public Vec3 QuartzM { get { return ToM(-18, Ground + 9, -1); } }
        public Vec3 HallM { get { return ToM(0, HallY, 116); } }
        public Vec3 LairM { get { return ToM(0, HallY, 128); } }
        public Vec3 LiftM { get { return Mechanism("lift").Point; } }
        public Vec3 GalleryM { get { return ToM(0, GalleryY, 166); } }
        public Vec3 TableM { get { return ToM(20.5, GalleryY + 4, 176.5); } }
        public Vec3 SeamM { get { return ToM(-28, GalleryY + 4, 162); } }
        public Vec3 ExitM { get; private set; }
        /// <summary>The singing quartz clusters, in metres (their middles).</summary>
        public readonly List<Vec3> Quartz = new List<Vec3>();

        /// <summary>The vault near this echo ruin, or null when the blocks it needs are not loaded.</summary>
        public static SealedVault Near(EchoRuin ruin, HeightField field, BlockRegistry blocks)
        {
            if (field == null || !blocks.Has("dressed_stone") || !blocks.Has("singing_quartz") || !blocks.Has("embersteel_ore")) return null;
            int x = ruin.Ox + EastOfRuin, z = ruin.Oz - SouthOfRuin;
            // Walk east until the knoll and the far end of the gallery both stand on dry land.
            for (int i = 0; i < 40; i++, x += 24)
                if (field.HeightAt(x, z) > HeightField.SeaLevel + 4 && field.HeightAt(x, z - 180) > HeightField.SeaLevel + 4) break;
            return new SealedVault(field, blocks, x, z);
        }

        public SealedVault(HeightField field, BlockRegistry blocks, int ox, int oz)
            : base(ox, oz, Math.Max(field.HeightAt(ox, oz), HeightField.SeaLevel + 1))
        {
            _stone = blocks.Id("ancient_stone");
            _moss = blocks.Has("mossy_stone") ? blocks.Id("mossy_stone") : _stone;
            _rock = blocks.Id("rust_rock");
            _paving = blocks.Id("paving");
            _dressed = blocks.Id("dressed_stone");
            _quartz = blocks.Id("singing_quartz");
            _ore = blocks.Id("embersteel_ore");
            _rune = blocks.Has("rune_stone") ? blocks.Id("rune_stone") : _stone;
            _deep = blocks.Id("deep_stone");
            _table = blocks.Has("inscription_table") ? blocks.Id("inscription_table") : _stone;
            _seal = blocks.Id("echo_seal");
            _grass = blocks.Has("moss") ? blocks.Id("moss") : _paving;
            InscribedBlock = blocks.Id("inscribed_stone");
            LampBlock = blocks.Id("lamp_glow");
            HallY = Ground + 1 - StepsDown;
            GalleryY = HallY + LedgeHeight;
            Build(field);
        }

        void Cluster(int u, int y, int v, int du, int dv)
        {
            // A knot of crystal, 3 x 3 x 2, standing a voxel proud of the face it grows from.
            Put(u, y, v, u + (du == 0 ? 2 : du), y + 2, v + (dv == 0 ? 2 : dv), _quartz);
            Put(u + (du == 0 ? 1 : 0), y + 3, v + (dv == 0 ? 1 : 0), u + (du == 0 ? 1 : 0), y + 3, v + (dv == 0 ? 1 : 0), _quartz);
            Quartz.Add(ToM(u + 1, y + 1.5, v + 1));
        }

        void Build(HeightField field)
        {
            int g = Ground, h = HallY, a = GalleryY;
            var shells = new List<int[]>();
            var floors = new List<int[]>();
            var insides = new List<int[]>();
            Action<int, int, int, int, int, int> room = (u0, y0, v0, u1, y1, v1) =>
            {
                shells.Add(new[] { u0 - 2, y0 - 2, v0 - 2, u1 + 2, y1 + 2, v1 + 2 });
                floors.Add(new[] { u0, y0 - 1, v0, u1, y0 - 1, v1 });
                insides.Add(new[] { u0, y0, v0, u1, y1, v1 });
            };

            // The knoll: three stacked tiers of weathered rock, rooted well into the ground.
            Put(-28, g - 8, 0, 27, g + 16, 60, _rock, _moss);
            Put(-22, g + 17, 4, 21, g + 26, 52, _rock, _moss);
            Put(-14, g + 27, 10, 13, g + 34, 40, _rock, _moss);

            // The door's passage, then the stair down; the quartz hall; the gallery above its ledge.
            room(-5, g + 1, 0, 4, g + 14, 7);
            for (int k = 0; k < StepsDown; k++)
                room(-5, g - k, 8 + 2 * k, 4, g - k + 13, 9 + 2 * k);
            int stairEnd = 8 + 2 * StepsDown;          // 104
            room(-20, h, stairEnd, 19, h + 27, 165);  // the hall, 10 x 15.5 m, 7 m high; its far third is the ledge
            room(-28, a, 146, 27, a + 15, 197);       // the gallery, 14 x 13 m, 4 m high
            // The way out: a sealed stair from the gallery's far end up to the light.
            room(-5, a, 198, 4, a + 13, 203);
            int v = 204, floorY = a;
            for (int k = 0; k < 80; k++, v += 2)
            {
                floorY++;
                room(-5, floorY, v, 4, floorY + 13, v + 1);
                if (floorY > field.HeightAt(WX(0), WZ(v + 2)) + 1) break;
            }
            room(-5, floorY, v + 2, 4, floorY + 13, v + 7);
            ExitM = ToM(0, floorY, v + 6);
            insides.Add(new[] { -5, floorY, v + 8, 4, floorY + 13, v + 9 });

            foreach (var b in shells) Put(b[0], b[1], b[2], b[3], b[4], b[5], _stone, _moss);
            foreach (var b in floors) Put(b[0], b[1], b[2], b[3], b[4], b[5], _paving, _grass);
            foreach (var b in insides) Put(b[0], b[1], b[2], b[3], b[4], b[5], 0);

            // The door: a frame of old stone standing proud of the knoll, the dressed-stone slab
            // inside it, a band of carved channel above, and Drai cut into the lintel.
            Put(-9, g + 1, -2, 8, g + 19, -1, _stone, _moss);
            Put(-5, g + 1, -2, 4, g + 14, -1, _dressed);
            Put(-9, g + 18, -2, 8, g + 18, -2, _rune);
            Carve(-1, g + 15, -3, 0, g + 16, -3, "vault_lintel", WordSplit);
            Put(-9, g, -10, 8, g, -3, _paving, _grass);

            // Singing quartz in the knoll's faces: north (two, either side of the door), west,
            // east, and one up on the second tier.
            Cluster(-20, g + 7, -2, 0, 2);
            Cluster(15, g + 8, -2, 0, 2);
            Cluster(-30, g + 3, 20, 2, 0);
            Cluster(28, g + 4, 30, 2, 0);
            Cluster(-6, g + 17, 1, 0, 2);

            // The quartz hall: quartz grown through the walls, lamps, Drai on the east wall.
            Cluster(-22, h + 3, 116, 2, 0);
            Cluster(18, h + 6, 112, 2, 0);
            Carve(20, h + 6, 120, 20, h + 7, 121, "vault_hall", WordSplit);
            Lamp(-21, h + 12, 108); Lamp(19, h + 12, 108); Lamp(-21, h + 12, 128); Lamp(19, h + 12, 128);

            // The ledge: the far end of the hall is solid to the gallery's floor.
            Put(-20, h, 146, 19, a - 1, 165, _stone, _moss);
            Put(-20, h - 1, 146, 19, h - 1, 165, _deep);
            Put(-1, h + 4, 145, 0, h + 5, 145, _rune);
            var lift = new RuinMechanism
            {
                Id = "lift", Name = "the socket under the ledge", Word = "word_rise",
                Point = ToM(0, h + 5, 145),
                Answer = "The socket drinks the word. Stone climbs out of the floor in steps, up to the ledge, and far above something heavy grinds aside.",
                CheckX = WX(0), CheckY = h, CheckZ = WZ(108), DoneSolid = true,
                Gives = "word_fragment",
            };
            // Nineteen steps, the top one a voxel under the ledge, out across the hall floor.
            for (int j = 0; j < LedgeHeight - 1; j++)
                lift.Ops.Add(Op(-4, h, 144 - 2 * j, 3, h + LedgeHeight - 2 - j, 145 - 2 * j, _stone));
            lift.Ops.Add(Op(-5, a, 200, 4, a + 13, 201, 0));
            Mechanisms.Add(lift);

            // The gallery: seams of embersteel in deep stone along both long walls, lamps, and
            // an old worktable by the east wall.
            int[][] seams = { new[] { -30, 156, 2 }, new[] { -30, 176, 5 }, new[] { 28, 152, 3 }, new[] { 28, 186, 1 } };
            foreach (var s in seams)
            {
                int u = s[0], sv = s[1], sy = a + s[2];
                Put(u, sy - 2, sv - 2, u + 1, sy + 6, sv + 9, _deep);
                Put(u, sy, sv, u + 1, sy + 4, sv + 7, _ore);
            }
            Lamp(-29, a + 10, 150); Lamp(27, a + 10, 150); Lamp(-29, a + 10, 192); Lamp(27, a + 10, 192);
            Put(18, a, 175, 23, a + 2, 178, _stone);
            Put(18, a + 3, 175, 23, a + 3, 178, _table);
            Put(-5, a, 200, 4, a + 13, 201, _seal);
        }

        /// <summary>Has the dressed-stone door been broken through?</summary>
        public bool DoorOpen(VoxelWorld world)
        {
            return world.Get(WX(0), Ground + 5, WZ(-2)) == 0 && world.Get(WX(0), Ground + 5, WZ(-1)) == 0;
        }

        /// <summary>Is this point (metres) under the knoll, in the halls?</summary>
        public bool Inside(Vec3 p)
        {
            int x = (int)Math.Floor(p.X * WorldConfig.VoxelsPerMeter), y = (int)Math.Floor(p.Y * WorldConfig.VoxelsPerMeter), z = (int)Math.Floor(p.Z * WorldConfig.VoxelsPerMeter);
            return InBounds(x, y, z) && y < Ground - 2 && z <= WZ(8);
        }
    }
}
