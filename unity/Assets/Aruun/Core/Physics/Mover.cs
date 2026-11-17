using System;
using Aruun.Core.Voxels;

namespace Aruun.Core.Physics
{
    /// <summary>A body that walks on voxels: an upright box, positions in voxel units, feet at Y.</summary>
    public struct Body
    {
        public double X, Y, Z;
        public double VX, VY, VZ;
        public bool Grounded;
    }

    /// <summary>
    /// Walking and falling against the voxel world: an axis-aligned box that slides along
    /// walls, lands on floors, and climbs low steps (the 0.25 m terrain steps) without jumping.
    /// Plain C#, so the host can run the same movement it checks clients against.
    /// All distances are voxels and all times seconds.
    /// </summary>
    public sealed class Mover
    {
        public double HalfWidth = 1.2;     // 0.6 m wide
        public double Height = 7.0;        // 1.75 m tall
        public double StepUp = 2.05;       // climbs 0.5 m ledges while walking
        public double Gravity = WorldConfig.Gravity * WorldConfig.VoxelsPerMeter;
        public double MaxFall = 50.0 * WorldConfig.VoxelsPerMeter;
        const double Skin = 1e-4;

        readonly VoxelWorld _world;

        public Mover(VoxelWorld world) { _world = world; }

        /// <summary>Jump speed (voxels/s) that lifts the feet by `metres` under this gravity.</summary>
        public double JumpSpeed(double metres)
        {
            return Math.Sqrt(2.0 * Gravity * metres * WorldConfig.VoxelsPerMeter);
        }

        /// <summary>Advances the body by dt with its velocity, gravity, collisions and step-up.</summary>
        public void Step(ref Body b, double dt)
        {
            b.VY -= Gravity * dt;
            if (b.VY < -MaxFall) b.VY = -MaxFall;

            // Small sub-steps keep every move under half a voxel, so nothing is skipped.
            double longest = Math.Max(Math.Abs(b.VX), Math.Max(Math.Abs(b.VY), Math.Abs(b.VZ))) * dt;
            int steps = Math.Max(1, (int)Math.Ceiling(longest / 0.45));
            double h = dt / steps;
            bool grounded = false;
            for (int i = 0; i < steps; i++)
            {
                if (!MoveY(ref b, b.VY * h)) { if (b.VY < 0) grounded = true; b.VY = 0; }
                bool canStep = grounded || b.Grounded;
                if (!MoveFlat(ref b, b.VX * h, 0, canStep)) b.VX = 0;
                if (!MoveFlat(ref b, 0, b.VZ * h, canStep)) b.VZ = 0;
            }
            // Standing still on the ground still counts as grounded.
            if (!grounded && b.VY <= 0 && Overlaps(b.X, b.Y - 0.02, b.Z)) grounded = true;
            b.Grounded = grounded;
        }

        /// <summary>Moves vertically; false if something stopped it.</summary>
        bool MoveY(ref Body b, double d)
        {
            if (d == 0) return true;
            double y = b.Y + d;
            if (!Overlaps(b.X, y, b.Z)) { b.Y = y; return true; }
            if (d < 0) b.Y = Math.Floor(y) + 1 + Skin;                   // stand on the voxel below
            else b.Y = Math.Floor(y + Height) - Height - Skin;          // head against the one above
            if (Overlaps(b.X, b.Y, b.Z)) b.Y = y - d;                    // could not settle: stay put
            return false;
        }

        /// <summary>Moves along X or Z; climbs a low step if allowed. False if blocked.</summary>
        bool MoveFlat(ref Body b, double dx, double dz, bool canStep)
        {
            if (dx == 0 && dz == 0) return true;
            double x = b.X + dx, z = b.Z + dz;
            if (!Overlaps(x, b.Y, z)) { b.X = x; b.Z = z; return true; }
            if (canStep)
            {
                // Find the lowest lift that clears the obstacle.
                for (double lift = 0.25; lift <= StepUp; lift += 0.25)
                {
                    if (Overlaps(b.X, b.Y + lift, b.Z)) break;             // no head room
                    if (!Overlaps(x, b.Y + lift, z))
                    {
                        double y = b.Y + lift;
                        // Drop back onto the step so we do not float above it.
                        double top = Math.Floor(y - Skin);
                        if (y - top > Skin && !Overlaps(x, top + Skin, z)) y = top + Skin;
                        b.X = x; b.Z = z; b.Y = y;
                        return true;
                    }
                }
            }
            // Slide flush against the wall.
            if (dx > 0) b.X = Math.Floor(x + HalfWidth) - HalfWidth - Skin;
            else if (dx < 0) b.X = Math.Floor(x - HalfWidth) + 1 + HalfWidth + Skin;
            if (dz > 0) b.Z = Math.Floor(z + HalfWidth) - HalfWidth - Skin;
            else if (dz < 0) b.Z = Math.Floor(z - HalfWidth) + 1 + HalfWidth + Skin;
            if (Overlaps(b.X, b.Y, b.Z)) { b.X = x - dx; b.Z = z - dz; }
            return false;
        }

        /// <summary>True if a body with its feet at (x, y, z) would be inside a solid voxel.</summary>
        public bool Overlaps(double x, double y, double z)
        {
            int x0 = (int)Math.Floor(x - HalfWidth), x1 = (int)Math.Floor(x + HalfWidth - Skin);
            int y0 = (int)Math.Floor(y), y1 = (int)Math.Floor(y + Height - Skin);
            int z0 = (int)Math.Floor(z - HalfWidth), z1 = (int)Math.Floor(z + HalfWidth - Skin);
            var blocks = _world.Blocks;
            for (int vy = y0; vy <= y1; vy++)
                for (int vz = z0; vz <= z1; vz++)
                    for (int vx = x0; vx <= x1; vx++)
                        if (blocks.Get(_world.Get(vx, vy, vz)).Collision) return true;
            return false;
        }
    }
}
