using System;

namespace Aruun.Core.Combat
{
    /// <summary>A plain 3D vector in metres, so combat and creature rules run without Unity. Y is up.</summary>
    public struct Vec3
    {
        public double X, Y, Z;

        public Vec3(double x, double y, double z) { X = x; Y = y; Z = z; }

        public static readonly Vec3 Zero = new Vec3(0, 0, 0);
        public static readonly Vec3 Up = new Vec3(0, 1, 0);
        /// <summary>Unity's forward, +Z.</summary>
        public static readonly Vec3 Forward = new Vec3(0, 0, 1);

        public double Length { get { return Math.Sqrt(X * X + Y * Y + Z * Z); } }
        public double FlatLength { get { return Math.Sqrt(X * X + Z * Z); } }
        public Vec3 Flat { get { return new Vec3(X, 0, Z); } }

        public Vec3 Normalized
        {
            get
            {
                double l = Length;
                return l > 1e-9 ? new Vec3(X / l, Y / l, Z / l) : Zero;
            }
        }

        public static Vec3 operator +(Vec3 a, Vec3 b) { return new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z); }
        public static Vec3 operator -(Vec3 a, Vec3 b) { return new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z); }
        public static Vec3 operator -(Vec3 a) { return new Vec3(-a.X, -a.Y, -a.Z); }
        public static Vec3 operator *(Vec3 a, double k) { return new Vec3(a.X * k, a.Y * k, a.Z * k); }
        public static Vec3 operator /(Vec3 a, double k) { return new Vec3(a.X / k, a.Y / k, a.Z / k); }

        public static double Dot(Vec3 a, Vec3 b) { return a.X * b.X + a.Y * b.Y + a.Z * b.Z; }
        public static double Distance(Vec3 a, Vec3 b) { return (a - b).Length; }
        public static double FlatDistance(Vec3 a, Vec3 b) { return (a - b).FlatLength; }

        /// <summary>Unsigned angle in radians between two directions on the ground plane.</summary>
        public static double FlatAngle(Vec3 a, Vec3 b)
        {
            double la = a.FlatLength, lb = b.FlatLength;
            if (la < 1e-9 || lb < 1e-9) return 0;
            double c = (a.X * b.X + a.Z * b.Z) / (la * lb);
            return Math.Acos(Math.Max(-1, Math.Min(1, c)));
        }

        public override string ToString() { return string.Format(System.Globalization.CultureInfo.InvariantCulture, "({0:0.##}, {1:0.##}, {2:0.##})", X, Y, Z); }
    }
}
