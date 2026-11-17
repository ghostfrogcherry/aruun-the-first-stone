using System;
using System.Collections.Generic;
using Aruun.Core.Worldgen;

namespace Aruun.Core.Farming
{
    /// <summary>One wild plant or node the world places by itself.</summary>
    public struct WildNode
    {
        /// <summary>Stable id ("w:cx:cz:plant:i"), so the host only remembers which ones were harvested.</summary>
        public string Id;
        public string Plant;
        /// <summary>The voxel it stands in, just above the ground.</summary>
        public int X, Y, Z;
    }

    /// <summary>
    /// Where wild plants and flint nodes appear. Everything comes from the world seed through
    /// HeightField, so every machine and every session agrees and nothing is saved but harvests.
    /// Positions are in voxels; a cell is Plants.CellVoxels on a side (64 voxels, 16 m).
    /// </summary>
    public static class WildSpawns
    {
        /// <summary>
        /// Is column (x, z) the kind of place `where` asks for? shore: just above the waterline.
        /// mesa: on the red mesas. meadow: dry land off the mesas. land: anywhere dry.
        /// </summary>
        public static bool Fits(HeightField field, string where, int x, int z)
        {
            int h = field.HeightAt(x, z);
            int sea = HeightField.SeaLevel;
            switch (where)
            {
                case "shore": return h >= sea + 1 && h <= sea + 5;
                case "mesa": return h > sea + 1 && field.IsMesa(x, z);
                case "meadow": return h > sea + 1 && !field.IsMesa(x, z);
                case "land": return h > sea + 1;
                default: return false;
            }
        }

        /// <summary>The wild nodes of one cell, in a fixed order.</summary>
        public static List<WildNode> InCell(HeightField field, Plants plants, int cx, int cz)
        {
            var output = new List<WildNode>();
            int cell = plants.CellVoxels;
            int salt = 100;
            foreach (PlantDef p in plants.All)
            {
                salt += 40;
                if (p.WildPerCell <= 0) continue;
                int n = (int)Math.Floor(p.WildPerCell);
                // The fraction is a chance of one more, compared in whole numbers so it never drifts.
                int extra = (int)Math.Round((p.WildPerCell - n) * 65536);
                if (field.Hash(cx, cz, salt) < extra) n++;
                for (int i = 0; i < n; i++)
                {
                    // A few tries each, so sparse places like shores still get their plants.
                    for (int attempt = 0; attempt < 5; attempt++)
                    {
                        int s = salt + 1 + i * 6 + attempt;
                        int x = cx * cell + (int)(((long)field.Hash(cx, cz, s) * cell) >> 16);
                        int z = cz * cell + (int)(((long)field.Hash(cz, cx, s + 17) * cell) >> 16);
                        if (!Fits(field, p.WildWhere, x, z)) continue;
                        output.Add(new WildNode
                        {
                            Id = "w:" + cx + ":" + cz + ":" + p.Key + ":" + i,
                            Plant = p.Key, X = x, Y = field.HeightAt(x, z) + 1, Z = z,
                        });
                        break;
                    }
                }
            }
            return output;
        }

        /// <summary>The cell a voxel column is in (floor division, so negatives work).</summary>
        public static int CellOf(int v, int cellVoxels)
        {
            return v >= 0 ? v / cellVoxels : -((-v - 1) / cellVoxels) - 1;
        }
    }
}
