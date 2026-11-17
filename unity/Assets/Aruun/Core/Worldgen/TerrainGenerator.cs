using Aruun.Core.Content;
using Aruun.Core.Voxels;

namespace Aruun.Core.Worldgen
{
    /// <summary>
    /// Fills chunks from the world seed. Pure and thread safe: the same seed and chunk always
    /// give the same voxels, and nothing is shared between calls except read-only data.
    /// </summary>
    public sealed class TerrainGenerator
    {
        public readonly HeightField Field;
        readonly BlockRegistry _blocks;
        readonly ushort _air, _tallGrass, _bark, _leaves, _crimsonLeaves, _water, _redSoil, _rustRock, _deepStone;

        public TerrainGenerator(int seed, BlockRegistry blocks)
        {
            Field = new HeightField(seed);
            _blocks = blocks;
            _air = blocks.Id("air");
            _tallGrass = blocks.Id("tall_grass");
            _bark = blocks.Id("bark");
            _leaves = blocks.Id("leaves");
            _crimsonLeaves = blocks.Id("crimson_leaves");
            _water = blocks.Id("water");
            _redSoil = blocks.Id("red_soil");
            _rustRock = blocks.Id("rust_rock");
            _deepStone = blocks.Id("deep_stone");
        }

        public VoxelChunk Generate(ChunkCoord coord)
        {
            var chunk = new VoxelChunk(coord);
            const int n = WorldConfig.ChunkSize;
            int ox = coord.OriginX, oy = coord.OriginY, oz = coord.OriginZ;
            for (int z = 0; z < n; z++)
            {
                for (int x = 0; x < n; x++)
                {
                    int wx = ox + x, wz = oz + z;
                    int h = Field.HeightAt(wx, wz);
                    int top = h + 1 > HeightField.SeaLevel ? h + 1 : HeightField.SeaLevel;
                    if (top < oy) continue; // this whole column of the chunk is air
                    string surface = Field.SurfaceKey(wx, wz, h);
                    ushort surfaceId = _blocks.Id(surface);
                    for (int y = 0; y < n; y++)
                    {
                        int wy = oy + y;
                        if (wy > top) break;
                        ushort id = LayerId(wy, h, surfaceId);
                        if (wy == h + 1 && id == _air && surface == "grass")
                        {
                            string plant = Field.FloraAt(wx, wz);
                            if (plant != null) id = _blocks.Id(plant);
                        }
                        if (id != _air) chunk.Set(x, y, z, id);
                    }
                }
            }
            ApplyTrees(chunk);
            return chunk;
        }

        /// <summary>
        /// Trees are placed per 5 m cell, so any chunk can rebuild the parts of every tree that
        /// reach into it without knowing about its neighbours.
        /// </summary>
        void ApplyTrees(VoxelChunk chunk)
        {
            const int n = WorldConfig.ChunkSize;
            const int reach = HeightField.TreeReach + 3;
            const int cell = HeightField.TreeCell;
            int ox = chunk.Coord.OriginX, oy = chunk.Coord.OriginY, oz = chunk.Coord.OriginZ;
            for (int cz = Floor(oz - reach, cell); cz <= Floor(oz + n + reach, cell); cz++)
            {
                for (int cx = Floor(ox - reach, cell); cx <= Floor(ox + n + reach, cell); cx++)
                {
                    TreeSpot t = Field.TreeInCell(cx, cz);
                    if (!t.Exists) continue;
                    int top = t.Base + t.Trunk, r = t.Radius;
                    if (t.Base > oy + n || top + r < oy) continue;
                    ushort leaf = t.Crimson ? _crimsonLeaves : _leaves;
                    int thick = t.Trunk < 28 ? 2 : 3;
                    int x0 = Max(t.X - r - ox, 0), x1 = Min(t.X + r + thick - ox, n);
                    int y0 = Max(t.Base - oy, 0), y1 = Min(top + r - oy, n);
                    int z0 = Max(t.Z - r - oz, 0), z1 = Min(t.Z + r + thick - oz, n);
                    for (int y = y0; y < y1; y++)
                    for (int z = z0; z < z1; z++)
                    for (int x = x0; x < x1; x++)
                    {
                        ushort cur = chunk.Get(x, y, z);
                        if (cur != _air && cur != _tallGrass) continue;
                        int wx = ox + x, wy = oy + y, wz = oz + z;
                        bool inTrunk = wx >= t.X && wx < t.X + thick && wz >= t.Z && wz < t.Z + thick && wy < top;
                        if (inTrunk) { chunk.Set(x, y, z, _bark); continue; }
                        if (wy < top - r / 2) continue;
                        // Canopy: a squashed sphere with a ragged edge. Distances are doubled
                        // so voxel centres stay whole numbers.
                        long dx = 2 * wx + 1 - (2 * t.X + thick);
                        long dy = (2 * wy + 1 - 2 * top) * 7 / 5;
                        long dz = 2 * wz + 1 - (2 * t.Z + thick);
                        long factor = 52429 + ((Field.Hash(wx * 7 + wy, wz, 31) * 16384L) >> 16); // 0.8..1.05
                        long edge = 2L * r * factor;                                             // 16.16
                        if (((dx * dx + dy * dy + dz * dz) << 32) < edge * edge) chunk.Set(x, y, z, leaf);
                    }
                }
            }
        }

        /// <summary>Same rule as HeightField.LayerAt, with ids looked up once.</summary>
        ushort LayerId(int y, int h, ushort surfaceId)
        {
            if (y > h) return y <= HeightField.SeaLevel ? _water : _air;
            if (y == h) return surfaceId;
            if (y > h - 6) return _redSoil;
            if (y > h - 40) return _rustRock;
            return _deepStone;
        }

        static int Floor(int a, int b) { int q = a / b; return (a % b != 0 && a < 0) ? q - 1 : q; }
        static int Max(int a, int b) { return a > b ? a : b; }
        static int Min(int a, int b) { return a < b ? a : b; }
    }
}
