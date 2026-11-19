using System;
using Aruun.Core.Voxels;
using NUnit.Framework;

namespace Aruun.Core.Tests
{
    /// <summary>
    /// Chunk work runs hundreds of times a second while the player travels; garbage it makes
    /// turns into collection pauses (stutter). These pin the hot paths at (almost) none.
    /// </summary>
    public class AllocationTests
    {
        [Test]
        public void ChunkWorkMakesLittleGarbage()
        {
            var blocks = TestContent.Blocks();
            var world = new VoxelWorld(20261116, blocks, TestContent.Map());
            var f = world.Generator.Field;
            var mesher = new GreedyMesher(blocks);
            var mesh = new MeshData();
            var padded = new ushort[GreedyMesher.PaddedVolume];
            Func<int, ChunkCoord> at = i => new ChunkCoord(i, f.HeightAt(i * 32, 0) >> 5, 0);
            for (int i = 0; i < 3; i++) { world.FillPadded(at(100 + i), padded); mesher.Build(padded, mesh); }   // warm up

            long a = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 2000; i++) f.HeightAt(100 + i % 300, 9 + i / 300);
            long b = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 20; i++) mesher.Build(padded, mesh);
            long c = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 20; i++) world.Generator.Generate(new ChunkCoord(400 + i, 40, 0));   // open sky
            long d = GC.GetAllocatedBytesForCurrentThread();

            Assert.That(b - a, Is.LessThan(1024), "terrain height makes no garbage");
            Assert.That((c - b) / 20, Is.LessThan(1024), "meshing a chunk makes no garbage");
            Assert.That((d - c) / 20, Is.LessThan(1024), "an empty-sky chunk costs almost nothing");
        }
    }
}
