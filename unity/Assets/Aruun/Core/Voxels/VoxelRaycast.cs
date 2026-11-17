using System;

namespace Aruun.Core.Voxels
{
    public struct VoxelHit
    {
        public bool Hit;
        /// <summary>The solid cell that was hit.</summary>
        public int X, Y, Z;
        /// <summary>The empty cell just in front of it, where a placed block would go.</summary>
        public int PrevX, PrevY, PrevZ;
        /// <summary>Distance along the ray, in voxels.</summary>
        public double Distance;
    }

    /// <summary>
    /// Walks a ray through the voxel grid one cell at a time and stops at the first cell with
    /// collision. Water and plants are passed through. Used for aiming, and by the host to
    /// check what a player says they are pointing at.
    /// </summary>
    public static class VoxelRaycast
    {
        /// <summary>Origin and direction are in voxel units. The direction need not be normalised.</summary>
        public static VoxelHit Cast(VoxelWorld world, double ox, double oy, double oz, double dx, double dy, double dz, double maxDistance)
        {
            var hit = new VoxelHit();
            double len = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (len < 1e-9) return hit;
            dx /= len; dy /= len; dz /= len;

            int x = (int)Math.Floor(ox), y = (int)Math.Floor(oy), z = (int)Math.Floor(oz);
            int sx = dx > 0 ? 1 : -1, sy = dy > 0 ? 1 : -1, sz = dz > 0 ? 1 : -1;
            double tx = Boundary(ox, x, dx), ty = Boundary(oy, y, dy), tz = Boundary(oz, z, dz);
            double ddx = dx == 0 ? double.MaxValue : Math.Abs(1 / dx);
            double ddy = dy == 0 ? double.MaxValue : Math.Abs(1 / dy);
            double ddz = dz == 0 ? double.MaxValue : Math.Abs(1 / dz);

            int px = x, py = y, pz = z;
            double t = 0;
            while (t <= maxDistance)
            {
                if (world.Blocks.Get(world.Get(x, y, z)).Collision)
                {
                    hit.Hit = true;
                    hit.X = x; hit.Y = y; hit.Z = z;
                    hit.PrevX = px; hit.PrevY = py; hit.PrevZ = pz;
                    hit.Distance = t;
                    return hit;
                }
                px = x; py = y; pz = z;
                if (tx <= ty && tx <= tz) { t = tx; tx += ddx; x += sx; }
                else if (ty <= tz) { t = ty; ty += ddy; y += sy; }
                else { t = tz; tz += ddz; z += sz; }
            }
            return hit;
        }

        /// <summary>Distance along the ray to the first cell boundary on one axis.</summary>
        static double Boundary(double origin, int cell, double dir)
        {
            if (dir > 0) return (cell + 1 - origin) / dir;
            if (dir < 0) return (origin - cell) / -dir;
            return double.MaxValue;
        }
    }
}
