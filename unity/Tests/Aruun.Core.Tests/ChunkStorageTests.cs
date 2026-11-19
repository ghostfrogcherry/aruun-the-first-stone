using System;
using Aruun.Core.Voxels;
using NUnit.Framework;

namespace Aruun.Core.Tests
{
    public class ChunkStorageTests
    {
        [Test]
        public void SmallStorageReadsBackEveryWrite()
        {
            var rng = new Random(7);
            var chunk = new VoxelChunk(new ChunkCoord(0, 0, 0));
            var truth = new ushort[WorldConfig.ChunkVolume];
            Assert.That(chunk.StorageBytes, Is.EqualTo(0), "a fresh chunk is all air and holds no array");
            // Few kinds first (palette), then more than 16 (full array).
            for (int step = 0; step < 40000; step++)
            {
                int i = rng.Next(truth.Length);
                ushort id = (ushort)(step < 20000 ? rng.Next(10) : rng.Next(40));
                chunk.Set(i, id);
                truth[i] = id;
                if (step == 19999) Assert.That(chunk.StorageBytes, Is.EqualTo(WorldConfig.ChunkVolume / 2));
            }
            Assert.That(chunk.StorageBytes, Is.EqualTo(WorldConfig.ChunkVolume * 2));
            Assert.That(chunk.ToArray(), Is.EqualTo(truth));
        }

        [Test]
        public void CompactKeepsEveryVoxel()
        {
            var full = new ushort[WorldConfig.ChunkVolume];
            var a = new VoxelChunk(new ChunkCoord(1, 2, 3), full);
            Assert.That(a.Compact().StorageBytes, Is.EqualTo(0), "all air needs no array");
            full[5] = 3; full[700] = 9; full[WorldConfig.ChunkVolume - 1] = 12;
            VoxelChunk small = a.Compact();
            Assert.That(small.StorageBytes, Is.EqualTo(WorldConfig.ChunkVolume / 2));
            Assert.That(small.ToArray(), Is.EqualTo(full));
            for (int i = 0; i < 20; i++) full[i * 100] = (ushort)(20 + i);
            VoxelChunk many = a.Compact();
            Assert.That(many.StorageBytes, Is.EqualTo(WorldConfig.ChunkVolume * 2));
            Assert.That(many.ToArray(), Is.EqualTo(full));
            full[0] = 77;   // the compact copy does not share the scratch array
            Assert.That(many.Get(0), Is.Not.EqualTo(77));
        }
    }
}
