using System;
using System.IO;
using UnityEngine;

namespace Aruun.Game
{
    /// <summary>
    /// Remembers the last world played, for the title screen's Continue button. Kept in a file
    /// beside the saves rather than only in PlayerPrefs: the prefs file can move (it did once,
    /// to an "unknown/unknown" folder) and then Continue vanished although the saves were fine.
    /// Falls back to the old PlayerPrefs key, then to the most recently played world folder.
    /// </summary>
    public static class LastWorld
    {
        const string PrefsKey = "aruun.lastSeed";

        static string FilePath { get { return Path.Combine(Application.persistentDataPath, "worlds", "last.txt"); } }

        public static void Write(int seed)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(FilePath, seed.ToString());
            }
            catch (Exception e) { Debug.LogWarning("Could not remember the last world: " + e.Message); }
        }

        public static bool TryRead(out int seed)
        {
            seed = 0;
            try
            {
                if (File.Exists(FilePath) && int.TryParse(File.ReadAllText(FilePath).Trim(), out seed)) return true;
            }
            catch (Exception) { }
            if (PlayerPrefs.HasKey(PrefsKey)) { seed = PlayerPrefs.GetInt(PrefsKey); return true; }
            return TryNewestWorld(out seed);
        }

        /// <summary>The seed of the world folder (dev-SEED or survival-SEED) changed most recently.</summary>
        static bool TryNewestWorld(out int seed)
        {
            seed = 0;
            try
            {
                string dir = Path.Combine(Application.persistentDataPath, "worlds");
                if (!Directory.Exists(dir)) return false;
                DateTime newest = DateTime.MinValue;
                bool found = false;
                foreach (string d in Directory.GetDirectories(dir))
                {
                    string name = Path.GetFileName(d);
                    int dash = name.IndexOf('-');
                    int s;
                    if (dash < 0 || !int.TryParse(name.Substring(dash + 1), out s)) continue;
                    if (!name.StartsWith("dev-") && !name.StartsWith("survival-")) continue;
                    DateTime t = Directory.GetLastWriteTimeUtc(d);
                    foreach (string f in Directory.GetFiles(d)) { DateTime ft = File.GetLastWriteTimeUtc(f); if (ft > t) t = ft; }
                    if (t > newest) { newest = t; seed = s; found = true; }
                }
                return found;
            }
            catch (Exception) { return false; }
        }
    }
}
