using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Aruun.Core.Edits
{
    /// <summary>
    /// The save file for a world's edits: a header naming the seed, then one op per line in
    /// the order they happened. Loading a world is generating it from the seed and replaying
    /// this file. A late joiner in co-op is sent the same lines.
    /// </summary>
    public static class Journal
    {
        public const int FormatVersion = 1;

        public static string Header(int seed)
        {
            return string.Format(CultureInfo.InvariantCulture, "aruun-journal {0} seed {1} gen {2}",
                FormatVersion, seed, WorldConfig.GenerationVersion);
        }

        public static void Write(TextWriter writer, int seed, IEnumerable<EditOp> ops)
        {
            writer.WriteLine(Header(seed));
            foreach (EditOp op in ops) writer.WriteLine(op.ToLine());
        }

        /// <summary>Adds one op to an open journal. Call Flush on the writer at safe points.</summary>
        public static void Append(TextWriter writer, EditOp op)
        {
            writer.WriteLine(op.ToLine());
        }

        /// <summary>
        /// Reads a journal. Lines that cannot be read are counted in `skipped` rather than
        /// stopping the load, so one bad line never costs a player their whole world.
        /// </summary>
        public static List<EditOp> Read(TextReader reader, out int seed, out int generation, out int skipped)
        {
            var ops = new List<EditOp>();
            seed = 0; generation = 0; skipped = 0;
            string header = reader.ReadLine();
            if (header == null) throw new FormatException("The journal is empty");
            string[] h = header.Split(' ');
            if (h.Length != 6 || h[0] != "aruun-journal" || h[2] != "seed" || h[4] != "gen" ||
                !int.TryParse(h[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out seed) ||
                !int.TryParse(h[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out generation))
                throw new FormatException("Not an Aruun journal: " + header);
            if (h[1] != FormatVersion.ToString(CultureInfo.InvariantCulture))
                throw new FormatException("Journal format " + h[1] + " is not supported by this version");
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.Length == 0) continue;
                EditOp op;
                if (EditOp.TryParse(line, out op)) ops.Add(op); else skipped++;
            }
            return ops;
        }
    }
}
