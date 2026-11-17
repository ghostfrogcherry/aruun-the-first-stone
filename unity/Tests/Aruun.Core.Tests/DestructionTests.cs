using System.Collections.Generic;
using Aruun.Core.Content;
using Aruun.Core.Destruction;
using Aruun.Core.Edits;
using Aruun.Core.Voxels;
using Aruun.Core.Worldgen;
using NUnit.Framework;

namespace Aruun.Core.Tests
{
    public class LoosePieceTests
    {
        const int S = 8;
        static int I(int x, int y, int z) { return x + z * S + y * S * S; }

        [Test]
        public void ASupportedColumnIsNotLoose()
        {
            var g = new bool[S * S * S];
            for (int y = 0; y < 5; y++) g[I(4, y, 4)] = true; // stands on the box floor
            Assert.That(LoosePieces.Find(g, S, S, S, 1000), Is.Empty);
        }

        [Test]
        public void AHangingBlockIsLoose()
        {
            var g = new bool[S * S * S];
            g[I(4, 4, 4)] = true;
            g[I(4, 5, 4)] = true;
            List<List<int>> pieces = LoosePieces.Find(g, S, S, S, 1000);
            Assert.That(pieces.Count, Is.EqualTo(1));
            Assert.That(pieces[0], Is.EquivalentTo(new[] { I(4, 4, 4), I(4, 5, 4) }));
        }

        [Test]
        public void TwoSeparatePiecesAreFoundSeparately()
        {
            var g = new bool[S * S * S];
            g[I(2, 3, 2)] = true;
            g[I(5, 5, 5)] = true;
            Assert.That(LoosePieces.Find(g, S, S, S, 1000).Count, Is.EqualTo(2));
        }

        [Test]
        public void AnOversizedPieceStaysPut()
        {
            var g = new bool[S * S * S];
            for (int x = 2; x < 6; x++) for (int z = 2; z < 6; z++) g[I(x, 4, z)] = true; // 16 cells
            Assert.That(LoosePieces.Find(g, S, S, S, 10), Is.Empty);
            Assert.That(LoosePieces.Find(g, S, S, S, 16).Count, Is.EqualTo(1));
        }

        [Test]
        public void CuttingATreeTrunkBringsTheTreeDown()
        {
            BlockRegistry blocks = TestContent.Blocks();
            var world = new VoxelWorld(42, blocks);
            HeightField f = world.Generator.Field;
            TreeSpot t = default(TreeSpot);
            // A tree standing alone: where canopies touch, a cut tree hangs in its neighbours.
            for (int c = 0; c < 200000 && !t.Exists; c++)
            {
                int cx = c % 400, cz = c / 400;
                t = f.TreeInCell(cx, cz);
                for (int dz = -1; dz <= 1 && t.Exists; dz++)
                    for (int dx = -1; dx <= 1; dx++)
                        if ((dx != 0 || dz != 0) && f.TreeInCell(cx + dx, cz + dz).Exists) t.Exists = false;
            }
            Assert.That(t.Exists, "the seed should grow a lone tree somewhere");
            int margin = t.Trunk + t.Radius + 6;

            // A dig in the ground beside the tree frees nothing, even though a few stray
            // leaves at the canopy's edge already hang in the air.
            world.Apply(EditOp.Box(t.X + 6, t.Base - 3, t.Z, t.X + 7, t.Base - 1, t.Z + 1, 0));
            Assert.That(LoosePieces.FreedBy(world, t.X + 6, t.Base - 3, t.Z, t.X + 7, t.Base - 1, t.Z + 1, margin, 100000), Is.Empty);

            // Saw through the trunk two voxels up.
            world.Apply(EditOp.Box(t.X - 1, t.Base + 2, t.Z - 1, t.X + 3, t.Base + 3, t.Z + 3, 0));
            List<List<PieceCell>> loose = LoosePieces.FreedBy(world, t.X - 1, t.Base + 2, t.Z - 1, t.X + 3, t.Base + 3, t.Z + 3, margin, 100000);
            Assert.That(loose.Count, Is.EqualTo(1), "the tree above the cut, in one piece");
            int bark = 0, leaves = 0;
            foreach (List<PieceCell> piece in loose)
                foreach (PieceCell c in piece)
                {
                    if (c.Block == blocks.Id("bark")) bark++;
                    if (c.Block == blocks.Id("leaves") || c.Block == blocks.Id("crimson_leaves")) leaves++;
                    Assert.That(world.Get(c.X, c.Y, c.Z), Is.EqualTo(c.Block), "cells carry the block that is really there");
                }
            Assert.That(bark, Is.GreaterThan(20), "the trunk above the cut");
            Assert.That(leaves, Is.GreaterThan(100), "and the canopy with it");
        }
    }

    public class CollapseTests
    {
        static List<PieceCell> Piece(params int[] xyz)
        {
            var list = new List<PieceCell>();
            for (int i = 0; i < xyz.Length; i += 3) list.Add(new PieceCell(xyz[i], xyz[i + 1], xyz[i + 2], 1));
            return list;
        }

        [Test]
        public void APieceLandsOnTheFloor()
        {
            // Floor at y = 0, piece at y = 10 and 11.
            Assert.That(Collapse.SettleDrop(Piece(0, 10, 0, 0, 11, 0), (x, y, z) => y > 0), Is.EqualTo(9));
        }

        [Test]
        public void APieceCatchesOnALedge()
        {
            // A wide piece; a ledge at (1, 6, 0) catches one end before the floor does.
            Assert.That(Collapse.SettleDrop(Piece(0, 10, 0, 1, 10, 0), (x, y, z) => y > 0 && !(x == 1 && y == 6)), Is.EqualTo(3));
        }

        [Test]
        public void AHollowPieceDoesNotBlockItself()
        {
            // An arch: its own lower cells must not count as obstacles for its upper cells.
            List<PieceCell> arch = Piece(0, 10, 0, 0, 11, 0, 1, 11, 0, 2, 11, 0, 2, 10, 0);
            Assert.That(Collapse.SettleDrop(arch, (x, y, z) => y > 0), Is.EqualTo(9));
        }

        [Test]
        public void ABottomlessFallIsReported()
        {
            Assert.That(Collapse.SettleDrop(Piece(0, 10, 0), (x, y, z) => true), Is.EqualTo(-1));
        }

        [Test]
        public void ImpactScalesWithSizeHeightAndGravity()
        {
            Assert.That(Collapse.ImpactDamage(100, 2), Is.EqualTo(0), "a short drop does nothing");
            Assert.That(Collapse.ImpactDamage(400, 20), Is.GreaterThan(Collapse.ImpactDamage(4, 20)));
            Assert.That(Collapse.ImpactDamage(100, 40), Is.GreaterThan(Collapse.ImpactDamage(100, 10)));
            Assert.That(Collapse.ImpactDamage(100, 20, WorldConfig.Gravity), Is.LessThan(Collapse.ImpactDamage(100, 20, WorldConfig.EarthGravity)),
                "the same fall is gentler on Aruun than in an Occlusion site");
            Assert.That(Collapse.ImpactDamage(1000000, 96), Is.EqualTo(400.0));
            // 2 m on Aruun takes about a second.
            Assert.That(Collapse.FallTime(8, WorldConfig.Gravity), Is.EqualTo(1.036).Within(0.01));
        }
    }
}
