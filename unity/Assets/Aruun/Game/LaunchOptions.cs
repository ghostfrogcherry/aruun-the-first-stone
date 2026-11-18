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
    ///   --host[=port]    open this world to friends (co-op, default port 27461)
    ///   --join=address[:port]   join a friend's world instead (skips the title screen)
    ///   --name=Name      your name in co-op
    /// </summary>
    public sealed class LaunchOptions
    {
        public int Seed = 20261116;
        public int ViewMeters = 192;
        public string ShotPath;
        public float ShotAfter = 10f;
        public float[] Pose;
        public bool Fly;
        /// <summary>--time=18.5 starts at that hour with the day paused (for screenshots); -1 means normal.</summary>
        public float Hour = -1f;
        /// <summary>Go straight into the world instead of showing the title screen.</summary>
        public bool SkipTitle;
        /// <summary>--join=address[:port]: join a friend's world instead of opening one.</summary>
        public string JoinAddress;

        public static LaunchOptions FromCommandLine()
        {
            var o = new LaunchOptions();
            foreach (string arg in Environment.GetCommandLineArgs())
            {
                int v;
                if (arg.StartsWith("--seed=") || arg.StartsWith("--shot=") || arg.StartsWith("--pos=") || arg == "--play" ||
                    arg.StartsWith("--gallery")) o.SkipTitle = true;
                if (arg == "--fly") o.Fly = true;
                // Co-op: host this world for friends, or join a friend's.
                if (arg == "--host") Coop.Hosting = true;
                else if (arg.StartsWith("--host=") && TryInt(arg.Substring(7), out v)) { Coop.Hosting = true; Coop.Port = v; }
                if (arg.StartsWith("--join=")) { o.JoinAddress = arg.Substring(7); o.SkipTitle = true; }
                if (arg.StartsWith("--name=")) Coop.Name = arg.Substring(7);
                float hour;
                if (arg.StartsWith("--time=") && float.TryParse(arg.Substring(7), NumberStyles.Float, CultureInfo.InvariantCulture, out hour))
                    o.Hour = Math.Max(0f, Math.Min(hour, 23.99f));
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
