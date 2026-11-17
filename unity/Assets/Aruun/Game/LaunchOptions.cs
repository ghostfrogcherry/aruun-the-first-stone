using System;
using System.Globalization;

namespace Aruun.Game
{
    /// <summary>
    /// Command line flags, so the game can be driven from a terminal:
    ///   --seed=1234      world seed (default 20261116)
    ///   --view=160       view distance in metres
    ///   --shot=out.png   save a screenshot after --shot-after seconds (default 10), then quit
    ///   --pos=x,y,z,yaw,pitch   start the camera here (metres and degrees), flying
    ///   --fly            start flying instead of walking
    ///   --play           skip the title screen (also skipped by --seed, --shot, --pos, --gallery)
    /// More arrive with their features (--host, --join, --world).
    /// </summary>
    public sealed class LaunchOptions
    {
        public int Seed = 20261116;
        public int ViewMeters = 128;
        public string ShotPath;
        public float ShotAfter = 10f;
        public float[] Pose;
        public bool Fly;
        /// <summary>Go straight into the world instead of showing the title screen.</summary>
        public bool SkipTitle;

        public static LaunchOptions FromCommandLine()
        {
            var o = new LaunchOptions();
            foreach (string arg in Environment.GetCommandLineArgs())
            {
                int v;
                if (arg.StartsWith("--seed=") || arg.StartsWith("--shot=") || arg.StartsWith("--pos=") || arg == "--play" ||
                    arg.StartsWith("--gallery")) o.SkipTitle = true;
                if (arg == "--fly") o.Fly = true;
                if (arg.StartsWith("--seed=") && TryInt(arg.Substring(7), out v)) o.Seed = v;
                else if (arg.StartsWith("--seed=")) o.Seed = StableHash(arg.Substring(7)); // words work as seeds too
                else if (arg.StartsWith("--view=") && TryInt(arg.Substring(7), out v)) o.ViewMeters = Math.Max(32, Math.Min(v, 512));
                else if (arg.StartsWith("--shot=")) o.ShotPath = arg.Substring(7);
                else if (arg.StartsWith("--shot-after=") && TryInt(arg.Substring(13), out v)) o.ShotAfter = Math.Max(1, v);
                else if (arg.StartsWith("--pos="))
                {
                    string[] parts = arg.Substring(6).Split(',');
                    var pose = new float[parts.Length];
                    bool ok = parts.Length == 3 || parts.Length == 5;
                    for (int i = 0; ok && i < parts.Length; i++)
                        ok = float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out pose[i]);
                    if (ok) o.Pose = pose;
                }
            }
            return o;
        }

        static bool TryInt(string s, out int v)
        {
            return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v);
        }

        /// <summary>string.GetHashCode differs between runs and machines; this does not.</summary>
        public static int StableHash(string s)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (char c in s) { h ^= c; h *= 16777619; }
                return (int)h;
            }
        }
    }
}
