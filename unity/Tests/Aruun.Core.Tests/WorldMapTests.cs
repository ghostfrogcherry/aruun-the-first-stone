using System;
using System.IO;
using Aruun.Core.Content;
using Aruun.Core.Voxels;
using Aruun.Core.Worldgen;
using NUnit.Framework;

namespace Aruun.Core.Tests
{
    public class WorldMapTests
    {
        static readonly WorldMap Map = TestContent.Map();

        [Test]
        public void SpawnIsTheConfluenceAtTheOrigin()
        {
            Assert.That(Map.Spawn.Key, Is.EqualTo("teshari_confluence"));
            Assert.That(Map.Spawn.X, Is.EqualTo(0));
            Assert.That(Map.Spawn.Z, Is.EqualTo(0));
            var f = new HeightField(1, Map);
            Assert.That(f.HeightAt(0, 0), Is.GreaterThan(HeightField.SeaLevel), "spawn should be dry land");
        }

        [Test]
        public void PlacesKeepTheirDirections()
        {
            // North is +Z, east is +X, whatever the seed.
            MapPlace teshbal = Map.Place("teshbal"), khar = Map.Place("khar_uln"), avarra = Map.Place("ruins_of_avarra");
            Assert.That(teshbal.Z, Is.GreaterThan(0), "Teshbal is north of the Confluence");
            Assert.That(teshbal.X, Is.GreaterThan(0), "Teshbal is east of the Confluence");
            Assert.That(khar.X, Is.LessThan(0), "Khar Uln is west");
            Assert.That(avarra.Z, Is.LessThan(0), "the Ruins of Avarra are south");
            Assert.That(Math.Abs(avarra.X), Is.LessThan(Map.Cell), "and straight south");
        }

        [Test]
        public void OlympusTowersAndTheNorthIsSea()
        {
            var f = new HeightField(5, Map);
            MapPeak olympus = null;
            foreach (var p in Map.Peaks) if (p.Key == "olympus_mons") olympus = p;
            Assert.That(olympus, Is.Not.Null);
            int rim = 0;
            for (int i = 0; i < 64; i++) rim = Math.Max(rim, f.HeightAt(olympus.X + olympus.Radius / 4 + i, olympus.Z));
            Assert.That(rim - HeightField.SeaLevel, Is.GreaterThan(120 * WorldConfig.VoxelsPerMeter), "crater rim above 120 m");
            Assert.That(f.HeightAt(olympus.X, olympus.Z), Is.LessThan(rim), "a crater at the summit");
            // Far north of the map is ocean.
            Assert.That(f.HeightAt(0, 95 * Map.Cell), Is.LessThan(HeightField.SeaLevel));
        }

        [Test]
        public void MapWorldIsDeterministicAndPinned()
        {
            var a = new HeightField(20261116, Map);
            var b = new HeightField(20261116, TestContent.Map());
            long sum = 17;
            for (int z = -60; z < 60; z++)
                for (int x = -60; x < 60; x++)
                {
                    int h = a.HeightAt(x * 97, z * 89);
                    Assert.That(h, Is.EqualTo(b.HeightAt(x * 97, z * 89)));
                    sum = unchecked(sum * 31 + h);
                }
            // If you change worldgen on purpose, update this and bump WorldConfig.GenerationVersion.
            Assert.That(sum, Is.EqualTo(PinnedMapHash), "map heights changed: got " + sum);
        }

        const long PinnedMapHash = -7720985634998013213;

        [Test]
        public void GlacialSeaFreezesAndBloomsGrow()
        {
            BlockRegistry blocks = TestContent.Blocks();
            var gen = new TerrainGenerator(3, blocks, Map);
            HeightField f = gen.Field;
            int ice = 0, bloom = 0;
            ushort iceId = blocks.Id("ice"), bloomId = blocks.Id("violet_bloom");
            // Walk the south of the map, a few chunks at a time.
            for (int i = 0; i < 400 && (ice == 0 || bloom == 0); i++)
            {
                int x = (i * 37 % 200 - 100) * Map.Cell / 2, z = -(110 + i % 50) * Map.Cell;
                int h = f.HeightAt(x, z);
                int y = h < HeightField.SeaLevel ? HeightField.SeaLevel : h + 1;
                VoxelChunk c = gen.Generate(ChunkCoord.FromVoxel(x, y, z));
                for (int k = 0; k < WorldConfig.ChunkVolume; k++)
                {
                    ushort v = c.Get(k & 31, (k >> 10) & 31, (k >> 5) & 31);
                    if (v == iceId) ice++;
                    if (v == bloomId) bloom++;
                }
            }
            Assert.That(ice, Is.GreaterThan(0), "some frozen sea in the far south");
            Assert.That(bloom, Is.GreaterThan(0), "violet blooms in the bloom fields");
        }

        [Test]
        public void BeyondTheMapTheSeedTakesOver()
        {
            var mapped = new HeightField(77, Map);
            var wild = new HeightField(77);
            // Far east of the map's edge the land is the seed's own, not ocean forever.
            int x = (Map.Width - Map.SpawnPx + 40) * Map.Cell;
            int same = 0, land = 0;
            for (int i = 0; i < 200; i++)
            {
                int z = (i - 100) * 97;
                if (mapped.HeightAt(x, z) == wild.HeightAt(x, z)) same++;
                if (mapped.HeightAt(x, z) > HeightField.SeaLevel) land++;
            }
            Assert.That(same, Is.EqualTo(200));
            Assert.That(land, Is.GreaterThan(50), "there is land out there");
            Assert.That(mapped.Wildness(0, 0), Is.EqualTo(0));
        }

        [Test]
        public void EveryPlaceHasAMarker()
        {
            BlockRegistry blocks = TestContent.Blocks();
            var world = new VoxelWorld(9, blocks, Map);
            MapPlace p = Map.Place("teshbal");
            int g = Math.Max(world.Generator.Field.HeightAt(p.X, p.Z), HeightField.SeaLevel);
            Assert.That(world.Get(p.X, g + 5, p.Z), Is.EqualTo(blocks.Id("ancient_stone")));
        }

        /// <summary>Not a check: writes the whole map world as a picture when ARUUN_WORLDMAP names a file.</summary>
        [Test]
        public void WriteWorldPicture()
        {
            string path = Environment.GetEnvironmentVariable("ARUUN_WORLDMAP");
            if (string.IsNullOrEmpty(path)) Assert.Ignore("set ARUUN_WORLDMAP to write a picture");
            BlockRegistry blocks = TestContent.Blocks();
            var f = new HeightField(42, Map);
            // The whole map plus 60 cells of the wild lands around it, two pixels per cell.
            const int margin = 60;
            int step = Map.Cell / 2, w = (Map.Width + 2 * margin) * 2, h = (Map.Height + 2 * margin) * 2;
            int x0 = -(Map.SpawnPx + margin) * Map.Cell, z0 = (Map.SpawnPy + margin) * Map.Cell;
            using (var o = new BinaryWriter(File.Create(path)))
            {
                o.Write(System.Text.Encoding.ASCII.GetBytes("P6 " + w + " " + h + " 255\n"));
                for (int j = 0; j < h; j++)
                    for (int i = 0; i < w; i++)
                    {
                        int wx = x0 + i * step, wz = z0 - j * step, y = f.HeightAt(wx, wz);
                        BlockDef b = y < HeightField.SeaLevel ? blocks.Get(f.Frozen(wx, wz) ? "ice" : "water") : blocks.Get(f.SurfaceKey(wx, wz, y));
                        int shade = 190 + (y - f.HeightAt(wx + 8, wz - 8)) * 6;
                        if (y < HeightField.SeaLevel) shade = 255 + Math.Max(-120, (y - HeightField.SeaLevel) * 2);
                        shade = Math.Max(90, Math.Min(255, shade));
                        o.Write((byte)(b.R * shade / 255)); o.Write((byte)(b.G * shade / 255)); o.Write((byte)(b.B * shade / 255));
                    }
            }
        }
    }
}
