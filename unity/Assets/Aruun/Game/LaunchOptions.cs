using System;
using System.Globalization;

namespace Aruun.Game
{
    /// <summary>
    /// Command line flags, so the game can be driven from a terminal:
    ///   --seed=1234      world seed (default 20261116)
    ///   --view=160       view distance in metres
    /// More arrive with their features (--host, --join, --world).
    /// </summary>
    public sealed class LaunchOptions
    {
        public int Seed = 20261116;
        public int ViewMeters = 128;

        public static LaunchOptions FromCommandLine()
        {
            var o = new LaunchOptions();
            foreach (string arg in Environment.GetCommandLineArgs())
            {
                int v;
                if (arg.StartsWith("--seed=") && TryInt(arg.Substring(7), out v)) o.Seed = v;
                else if (arg.StartsWith("--seed=")) o.Seed = StableHash(arg.Substring(7)); // words work as seeds too
                else if (arg.StartsWith("--view=") && TryInt(arg.Substring(7), out v)) o.ViewMeters = Math.Max(32, Math.Min(v, 512));
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
