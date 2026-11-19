using System.IO;

namespace Aruun.Game
{
    /// <summary>
    /// Writes a save file so a crash or power cut mid-write can never leave it half written:
    /// the text goes to a temporary file first, which then replaces the old one in one step.
    /// The previous version is kept as a .bak beside it.
    /// </summary>
    public static class SafeFile
    {
        public static void WriteAllText(string path, string text)
        {
            string tmp = path + ".tmp";
            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var w = new StreamWriter(fs))
            {
                w.Write(text);
                w.Flush();
                fs.Flush(true);
            }
            if (File.Exists(path)) File.Replace(tmp, path, path + ".bak");
            else File.Move(tmp, path);
        }
    }
}
