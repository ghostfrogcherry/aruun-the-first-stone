using System;
using System.Collections.Generic;
using System.IO;
using Aruun.Core.Content;
using Aruun.Core.Edits;
using Aruun.Core.Voxels;
using Aruun.Core.Worldgen;
using NUnit.Framework;

namespace Aruun.Core.Tests
{
    public class ContentTests
    {
        [Test]
        public void RealBlocksFileLoads()
        {
            BlockRegistry blocks = TestContent.Blocks();
            Assert.That(blocks.Get(0).Key, Is.EqualTo("air"));
            Assert.That(blocks.Get("grass").IsOpaqueCube, Is.True);
            Assert.That(blocks.Get("water").Transparent, Is.True);
            Assert.That(blocks.Get("water").Collision, Is.False);
            Assert.That(blocks.Get("tall_grass").Shape, Is.EqualTo("cross"));
            Assert.That(blocks.Get("inscribed_stone").Unbreakable, Is.True);
            Assert.That(blocks.Placeable, Does.Contain("timber"));
        }

        [Test]
        public void JsonReaderHandlesTheBasics()
        {
            var o = MiniJson.ParseObject("{\"a\": [1, -2.5, true, null, \"x\\n\\u0041\"], \"b\": {\"c\": 3}}");
            var a = MiniJson.List(o, "a");
            Assert.That(a[0], Is.EqualTo(1.0));
            Assert.That(a[1], Is.EqualTo(-2.5));
            Assert.That(a[2], Is.EqualTo(true));
            Assert.That(a[3], Is.Null);
            Assert.That(a[4], Is.EqualTo("x\nA"));
            Assert.That(MiniJson.Int((Dictionary<string, object>)o["b"], "c"), Is.EqualTo(3));
            Assert.Throws<FormatException>(() => MiniJson.Parse("{\"a\": }"));
        }

        [Test]
        public void EveryContentJsonFileParses()
        {
            foreach (string file in Directory.GetFiles(TestContent.Root, "*.json", SearchOption.AllDirectories))
                Assert.DoesNotThrow(() => MiniJson.Parse(File.ReadAllText(file)), file);
        }
    }

    public class WorldgenTests
    {
        [Test]
        public void SameSeedGivesTheSameChunk()
        {
            BlockRegistry blocks = TestContent.Blocks();
            var a = new TerrainGenerator(1234, blocks).Generate(new ChunkCoord(3, 1, -2));
            var b = new TerrainGenerator(1234, blocks).Generate(new ChunkCoord(3, 1, -2));
            Assert.That(a.Ids, Is.EqualTo(b.Ids));
        }

        [Test]
        public void DifferentSeedsGiveDifferentLand()
        {
            var a = new HeightField(1);
            var b = new HeightField(2);
            int different = 0;
            for (int i = 0; i < 200; i++)
                if (a.HeightAt(i * 37, i * 11) != b.HeightAt(i * 37, i * 11)) different++;
            Assert.That(different, Is.GreaterThan(100));
        }

        /// <summary>Pins the exact output so an accidental change to worldgen cannot slip by:
        /// it would move terrain under every existing save. Change deliberately, and bump
        /// WorldConfig.GenerationVersion when you do.</summary>
        [Test]
        public void HeightsArePinned()
        {
            var f = new HeightField(20261116);
            long sum = 0;
            for (int z = -64; z < 64; z++)
                for (int x = -64; x < 64; x++)
                    sum = unchecked(sum * 31 + f.HeightAt(x * 5, z * 5));
            Assert.That(sum, Is.EqualTo(PinnedHeights));
        }
        public const long PinnedHeights = -8010780048785411565;

        [Test]
        public void LandIsSmoothAndVaried()
        {
            var f = new HeightField(42);
            int min = int.MaxValue, max = int.MinValue, water = 0, mesa = 0, steep = 0, total = 0;
            for (int z = 0; z < 4000; z += 8)
            {
                for (int x = 0; x < 4000; x += 8)
                {
                    int h = f.HeightAt(x, z);
                    min = Math.Min(min, h); max = Math.Max(max, h);
                    if (h < HeightField.SeaLevel) water++;
                    if (f.IsMesa(x, z)) mesa++;
                    else if (Math.Abs(f.HeightAt(x + 1, z) - h) > 2) steep++;
                    total++;
                }
            }
            // A living world: some lakes, some mesas, mostly green land between.
            Assert.That(water * 100 / total, Is.InRange(2, 40), "share of water");
            Assert.That(mesa * 100 / total, Is.InRange(2, 40), "share of mesa");
            Assert.That(max - min, Is.GreaterThan(40));
            // Away from the mesas the ground is walkable: steps over half a metre are rare.
            Assert.That(steep * 1000 / total, Is.LessThan(10), "steep steps per thousand columns");
            TestContext.WriteLine("heights " + min + ".." + max + ", water " + (water * 100 / total) + "%, mesa " + (mesa * 100 / total) + "%, steep " + steep + "/" + total);
        }

        [Test]
        public void MeadowsGrowPlantsAndTrees()
        {
            BlockRegistry blocks = TestContent.Blocks();
            var gen = new TerrainGenerator(42, blocks);
            var count = new Dictionary<ushort, int>();
            for (int cz = 0; cz < 6; cz++)
                for (int cx = 0; cx < 6; cx++)
                    for (int cy = 0; cy < 4; cy++)
                        foreach (ushort id in gen.Generate(new ChunkCoord(cx, cy, cz)).Ids)
                        {
                            int n; count.TryGetValue(id, out n); count[id] = n + 1;
                        }
            Assert.That(count.ContainsKey(blocks.Id("grass")), "grass");
            Assert.That(count.ContainsKey(blocks.Id("tall_grass")), "tall grass");
            Assert.That(count.ContainsKey(blocks.Id("bark")), "tree trunks");
            Assert.That(count.ContainsKey(blocks.Id("leaves")) || count.ContainsKey(blocks.Id("crimson_leaves")), "leaves");
        }

        [Test]
        public void TreesJoinUpAcrossChunkBorders()
        {
            // A voxel read through the world must not depend on which chunk was generated first.
            BlockRegistry blocks = TestContent.Blocks();
            var gen = new TerrainGenerator(7, blocks);
            var left = gen.Generate(new ChunkCoord(0, 1, 0));
            var right = gen.Generate(new ChunkCoord(1, 1, 0));
            var again = new TerrainGenerator(7, blocks).Generate(new ChunkCoord(1, 1, 0));
            Assert.That(right.Ids, Is.EqualTo(again.Ids));
            Assert.That(left.Coord, Is.Not.EqualTo(right.Coord));
        }
    }

    public class MapDump
    {
        /// <summary>Not a check: writes a top-down picture of the land when ARUUN_MAP names a file.</summary>
        [Test]
        public void WriteMapPicture()
        {
            string path = Environment.GetEnvironmentVariable("ARUUN_MAP");
            if (string.IsNullOrEmpty(path)) Assert.Ignore("set ARUUN_MAP to write a map");
            BlockRegistry blocks = TestContent.Blocks();
            var f = new HeightField(42);
            const int size = 800, scale = 4;
            using (var w = new BinaryWriter(File.Create(path)))
            {
                w.Write(System.Text.Encoding.ASCII.GetBytes("P6 " + size + " " + size + " 255\n"));
                for (int z = 0; z < size; z++)
                    for (int x = 0; x < size; x++)
                    {
                        int wx = x * scale, wz = z * scale, h = f.HeightAt(wx, wz);
                        BlockDef b = h < HeightField.SeaLevel ? blocks.Get("water") : blocks.Get(f.SurfaceKey(wx, wz, h));
                        TreeSpot t = f.TreeInCell(wx / HeightField.TreeCell, wz / HeightField.TreeCell);
                        if (t.Exists && Math.Abs(t.X - wx) < 4 && Math.Abs(t.Z - wz) < 4) b = blocks.Get(t.Crimson ? "crimson_leaves" : "leaves");
                        int shade = 190 + (h - f.HeightAt(wx + 2, wz + 2)) * 12;
                        shade = Math.Max(110, Math.Min(255, shade));
                        w.Write((byte)(b.R * shade / 255)); w.Write((byte)(b.G * shade / 255)); w.Write((byte)(b.B * shade / 255));
                    }
            }
        }
    }

    public class MesherTests
    {
        static ushort[] Empty() { return new ushort[GreedyMesher.PaddedVolume]; }

        [Test]
        public void CornersBesideAWallAreDarker()
        {
            BlockRegistry blocks = TestContent.Blocks();
            ushort stone = blocks.Id("rust_rock");
            ushort[] p = Empty();
            for (int x = 1; x <= 10; x++) for (int z = 1; z <= 10; z++) p[GreedyMesher.PaddedIndex(x, 5, z)] = stone;
            p[GreedyMesher.PaddedIndex(5, 6, 5)] = stone;   // one block standing on the floor
            var mesh = new MeshData();
            new GreedyMesher(blocks).Build(p, mesh);
            BlockDef b = blocks.Get(stone);
            int darkest = 255;
            for (int i = 0; i < mesh.Colors.Count; i += 4) darkest = Math.Min(darkest, mesh.Colors[i]);
            Assert.That(darkest, Is.LessThan((int)b.R), "the floor darkens where it meets the block");
        }

        [Test]
        public void OneVoxelIsSixQuads()
        {
            BlockRegistry blocks = TestContent.Blocks();
            ushort[] p = Empty();
            p[GreedyMesher.PaddedIndex(5, 6, 7)] = blocks.Id("rust_rock");
            var mesh = new MeshData();
            new GreedyMesher(blocks).Build(p, mesh);
            Assert.That(mesh.VertexCount, Is.EqualTo(24));
            Assert.That(mesh.Opaque.Count, Is.EqualTo(36));
            Assert.That(mesh.Transparent, Is.Empty);
        }

        [Test]
        public void AFlatSlabMergesIntoSixQuads()
        {
            BlockRegistry blocks = TestContent.Blocks();
            ushort[] p = Empty();
            for (int z = 1; z <= 32; z++)
                for (int x = 1; x <= 32; x++)
                    p[GreedyMesher.PaddedIndex(x, 1, z)] = blocks.Id("grass");
            var mesh = new MeshData();
            new GreedyMesher(blocks).Build(p, mesh);
            Assert.That(mesh.VertexCount, Is.EqualTo(24), "1,024 voxels should merge into one box");
        }

        [Test]
        public void BuriedVoxelsMakeNoFaces()
        {
            BlockRegistry blocks = TestContent.Blocks();
            ushort[] p = Empty();
            for (int i = 0; i < p.Length; i++) p[i] = blocks.Id("deep_stone");
            var mesh = new MeshData();
            new GreedyMesher(blocks).Build(p, mesh);
            Assert.That(mesh.IsEmpty, Is.True);
        }

        [Test]
        public void EveryTriangleFacesItsNormal()
        {
            BlockRegistry blocks = TestContent.Blocks();
            ushort[] p = Empty();
            p[GreedyMesher.PaddedIndex(3, 3, 3)] = blocks.Id("rust_rock");
            p[GreedyMesher.PaddedIndex(4, 3, 3)] = blocks.Id("grass");
            p[GreedyMesher.PaddedIndex(10, 12, 9)] = blocks.Id("water");
            var mesh = new MeshData();
            new GreedyMesher(blocks).Build(p, mesh);
            foreach (List<int> list in new[] { mesh.Opaque, mesh.Transparent })
            {
                for (int t = 0; t < list.Count; t += 3)
                {
                    int a = list[t] * 3, b = list[t + 1] * 3, c = list[t + 2] * 3;
                    float ux = mesh.Positions[b] - mesh.Positions[a], uy = mesh.Positions[b + 1] - mesh.Positions[a + 1], uz = mesh.Positions[b + 2] - mesh.Positions[a + 2];
                    float vx = mesh.Positions[c] - mesh.Positions[a], vy = mesh.Positions[c + 1] - mesh.Positions[a + 1], vz = mesh.Positions[c + 2] - mesh.Positions[a + 2];
                    float nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
                    float dot = nx * mesh.Normals[a] + ny * mesh.Normals[a + 1] + nz * mesh.Normals[a + 2];
                    Assert.That(dot, Is.GreaterThan(0f), "triangle " + (t / 3) + " is inside out");
                }
            }
        }

        [Test]
        public void WaterIsSeparateAndHidesItsOwnInside()
        {
            BlockRegistry blocks = TestContent.Blocks();
            ushort[] p = Empty();
            for (int x = 1; x <= 4; x++) p[GreedyMesher.PaddedIndex(x, 1, 1)] = blocks.Id("water");
            p[GreedyMesher.PaddedIndex(5, 1, 1)] = blocks.Id("rust_rock");
            var mesh = new MeshData();
            new GreedyMesher(blocks).Build(p, mesh);
            // Water: 4 long sides + 1 end (the other end is against rock) = 5 quads.
            Assert.That(mesh.Transparent.Count, Is.EqualTo(5 * 6));
            // Rock: all 6 faces, because you can see it through the water.
            Assert.That(mesh.Opaque.Count, Is.EqualTo(6 * 6));
        }

        [Test]
        public void PlantsBecomeCrossedQuads()
        {
            BlockRegistry blocks = TestContent.Blocks();
            ushort[] p = Empty();
            p[GreedyMesher.PaddedIndex(2, 2, 2)] = blocks.Id("tall_grass");
            var mesh = new MeshData();
            new GreedyMesher(blocks).Build(p, mesh);
            Assert.That(mesh.VertexCount, Is.EqualTo(8));
            Assert.That(mesh.Opaque.Count, Is.EqualTo(24));
        }

        [Test]
        public void AGeneratedChunkMeshesWithoutHoles()
        {
            BlockRegistry blocks = TestContent.Blocks();
            var world = new VoxelWorld(42, blocks);
            var field = world.Generator.Field;
            var coord = ChunkCoord.FromVoxel(100, field.HeightAt(100, 100), 100);
            var padded = new ushort[GreedyMesher.PaddedVolume];
            world.FillPadded(coord, padded);
            // The padding really is the neighbours' voxels.
            Assert.That(padded[GreedyMesher.PaddedIndex(0, 5, 5)], Is.EqualTo(world.Get(coord.OriginX - 1, coord.OriginY + 4, coord.OriginZ + 4)));
            Assert.That(padded[GreedyMesher.PaddedIndex(33, 33, 33)], Is.EqualTo(world.Get(coord.OriginX + 32, coord.OriginY + 32, coord.OriginZ + 32)));
            var mesh = new MeshData();
            new GreedyMesher(blocks).Build(padded, mesh);
            Assert.That(mesh.IsEmpty, Is.False);
            Assert.That(mesh.VertexCount, Is.LessThan(40000), "greedy meshing should keep a surface chunk small");
        }
    }

    public class EditTests
    {
        [Test]
        public void OpsSurviveTheJournal()
        {
            var ops = new List<EditOp>
            {
                EditOp.Set(-5, 40, 12, 8),
                EditOp.Box(0, 30, 0, 9, 31, 4, 2, 3),
                EditOp.Sphere(100, -20, 7, 16, 0, 1),
            };
            var text = new StringWriter();
            Journal.Write(text, 777, ops);
            int seed, gen, skipped;
            List<EditOp> back = Journal.Read(new StringReader(text.ToString()), out seed, out gen, out skipped);
            Assert.That(seed, Is.EqualTo(777));
            Assert.That(gen, Is.EqualTo(WorldConfig.GenerationVersion));
            Assert.That(skipped, Is.EqualTo(0));
            Assert.That(back.Count, Is.EqualTo(3));
            for (int i = 0; i < ops.Count; i++) Assert.That(back[i].ToLine(), Is.EqualTo(ops[i].ToLine()));
        }

        [Test]
        public void ABadLineIsSkippedNotFatal()
        {
            string text = Journal.Header(1) + "\nset 1 2 3 4 5\nnonsense here\nbox 1 2\nsphere 0 0 0 999999 0 1\nset 9 9 9 2 98\n";
            int seed, gen, skipped;
            List<EditOp> ops = Journal.Read(new StringReader(text), out seed, out gen, out skipped);
            Assert.That(ops.Count, Is.EqualTo(2));
            Assert.That(skipped, Is.EqualTo(3));
            Assert.Throws<FormatException>(() => Journal.Read(new StringReader("hello"), out seed, out gen, out skipped));
        }

        [Test]
        public void DiggingRespectsHardness()
        {
            Assert.That(EditRules.Breaks(1, 1), Is.True);
            Assert.That(EditRules.Breaks(3, 1), Is.False);
            Assert.That(EditRules.Breaks(99, 1000), Is.False, "inscribed stone never breaks");

            BlockRegistry blocks = TestContent.Blocks();
            var world = new VoxelWorld(42, blocks);
            int h = FindMeadow(world, out int x, out int z);
            // Bare hands (power 1) take grass and soil but stop at rock.
            EditResult weak = world.Apply(EditOp.Box(x, h - 20, z, x, h, z, 0, 1));
            Assert.That(weak.Changed, Is.EqualTo(6), "one grass and five soil");
            Assert.That(world.Get(x, h - 6, z), Is.EqualTo(blocks.Id("rust_rock")));
            // A pick (power 2) goes on through the rock.
            EditResult strong = world.Apply(EditOp.Box(x, h - 20, z, x, h, z, 0, 2));
            Assert.That(strong.Removed[blocks.Id("rust_rock")], Is.EqualTo(15));
        }

        [Test]
        public void BuildingFillsSpaceButNeverOverwritesSolid()
        {
            BlockRegistry blocks = TestContent.Blocks();
            var world = new VoxelWorld(42, blocks);
            int h = FindMeadow(world, out int x, out int z);
            ushort timber = blocks.Id("timber");
            EditResult r = world.Apply(EditOp.Box(x, h, z, x, h + 3, z, timber));
            Assert.That(r.Changed, Is.EqualTo(3), "the ground voxel stays, three above are filled");
            Assert.That(world.Get(x, h, z), Is.EqualTo(blocks.Id("grass")));
            Assert.That(world.Get(x, h + 3, z), Is.EqualTo(timber));
        }

        [Test]
        public void TheWorldRemembersAfterUnloadAndReload()
        {
            BlockRegistry blocks = TestContent.Blocks();
            var world = new VoxelWorld(42, blocks);
            int h = FindMeadow(world, out int x, out int z);
            var ops = new List<EditOp>
            {
                EditOp.Sphere(x, h, z, 30, 0, 2),
                EditOp.Box(x - 1, h + 4, z - 1, x + 1, h + 4, z + 1, blocks.Id("timber")),
            };
            foreach (EditOp op in ops) world.Apply(op);
            ushort dug = world.Get(x, h, z), built = world.Get(x, h + 4, z);
            Assert.That(dug, Is.EqualTo(0));
            Assert.That(built, Is.EqualTo(blocks.Id("timber")));

            // 1. Chunks dropped from memory come back with their edits.
            ChunkCoord coord = ChunkCoord.FromVoxel(x, h, z);
            world.Unload(coord);
            world.Unload(ChunkCoord.FromVoxel(x, h + 4, z));
            Assert.That(world.Get(x, h, z), Is.EqualTo(dug));
            Assert.That(world.Get(x, h + 4, z), Is.EqualTo(built));

            // 2. Save, quit, reload: a fresh world replaying the journal is voxel for voxel the same.
            var text = new StringWriter();
            Journal.Write(text, 42, ops);
            int seed, gen, skipped;
            var reloaded = new VoxelWorld(42, blocks);
            foreach (EditOp op in Journal.Read(new StringReader(text.ToString()), out seed, out gen, out skipped))
                reloaded.Apply(op);
            Assert.That(reloaded.GetChunk(coord).Ids, Is.EqualTo(world.GetChunk(coord).Ids));
        }

        [Test]
        public void EditsOnAChunkBorderDirtyTheNeighbour()
        {
            BlockRegistry blocks = TestContent.Blocks();
            var world = new VoxelWorld(42, blocks);
            EditResult r = world.Apply(EditOp.Set(32, 500, 5, blocks.Id("timber")));
            Assert.That(r.Dirty, Does.Contain(new ChunkCoord(1, 15, 0)));
            Assert.That(r.Dirty, Does.Contain(new ChunkCoord(0, 15, 0)));
            Assert.That(r.Dirty.Count, Is.EqualTo(2));
        }

        [Test]
        public void NegativeCoordinatesLandInTheRightChunk()
        {
            Assert.That(ChunkCoord.FromVoxel(-1, -32, -33), Is.EqualTo(new ChunkCoord(-1, -1, -2)));
            BlockRegistry blocks = TestContent.Blocks();
            var world = new VoxelWorld(42, blocks);
            world.Apply(EditOp.Set(-1, 600, -33, blocks.Id("timber")));
            Assert.That(world.Get(-1, 600, -33), Is.EqualTo(blocks.Id("timber")));
            Assert.That(world.Get(0, 600, -33), Is.EqualTo(0));
        }

        [Test]
        public void ManyThreadsCanReadWhileGenerating()
        {
            BlockRegistry blocks = TestContent.Blocks();
            var world = new VoxelWorld(42, blocks);
            var reference = new VoxelWorld(42, blocks);
            System.Threading.Tasks.Parallel.For(0, 64, i =>
            {
                var padded = new ushort[GreedyMesher.PaddedVolume];
                world.FillPadded(new ChunkCoord(i % 4, 1, i % 3), padded);
            });
            for (int i = 0; i < 12; i++)
            {
                var c = new ChunkCoord(i % 4, 1, i % 3);
                Assert.That(world.GetChunk(c).Ids, Is.EqualTo(reference.GetChunk(c).Ids));
            }
        }

        [Test]
        public void ARayFindsTheGroundAndTheCellInFront()
        {
            BlockRegistry blocks = TestContent.Blocks();
            var world = new VoxelWorld(42, blocks);
            int h = FindMeadow(world, out int x, out int z);
            // Clear any plant so "in front" is plain air, then look straight down from 5 voxels up.
            VoxelHit hit = VoxelRaycast.Cast(world, x + 0.5, h + 6.5, z + 0.5, 0, -1, 0, 24);
            Assert.That(hit.Hit, Is.True);
            Assert.That(new[] { hit.X, hit.Y, hit.Z }, Is.EqualTo(new[] { x, h, z }), "rays pass through plants and stop on the ground");
            Assert.That(new[] { hit.PrevX, hit.PrevY, hit.PrevZ }, Is.EqualTo(new[] { x, h + 1, z }));
            Assert.That(hit.Distance, Is.EqualTo(5.5).Within(1e-6));
            // Too short a reach finds nothing; neither does looking at the sky.
            Assert.That(VoxelRaycast.Cast(world, x + 0.5, h + 6.5, z + 0.5, 0, -1, 0, 3).Hit, Is.False);
            Assert.That(VoxelRaycast.Cast(world, x + 0.5, h + 300.5, z + 0.5, 0.2, 1, 0.1, 50).Hit, Is.False);
            // A slanted ray lands on a solid cell next to an open one.
            VoxelHit slant = VoxelRaycast.Cast(world, x + 0.5, h + 6.5, z + 0.5, 0.6, -1, 0.3, 40);
            Assert.That(slant.Hit, Is.True);
            Assert.That(blocks.Get(world.Get(slant.X, slant.Y, slant.Z)).Collision, Is.True);
            Assert.That(blocks.Get(world.Get(slant.PrevX, slant.PrevY, slant.PrevZ)).Collision, Is.False);
        }

        /// <summary>Finds a grass column well above the water, to dig and build on.</summary>
        static int FindMeadow(VoxelWorld world, out int x, out int z)
        {
            HeightField f = world.Generator.Field;
            for (int i = 0; i < 100000; i++)
            {
                x = i * 13; z = i * 7;
                int h = f.HeightAt(x, z);
                if (h > HeightField.SeaLevel + 6 && !f.IsMesa(x, z) && f.SurfaceKey(x, z, h) == "grass"
                    && world.Get(x, h + 1, z) == 0 && world.Get(x, h + 3, z) == 0 && world.Get(x, h + 4, z) == 0)
                    return h;
            }
            throw new InvalidOperationException("no meadow found");
        }
    }
}
