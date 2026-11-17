using System;
using System.IO;
using Aruun.Core.Content;

namespace Aruun.Core.Tests
{
    /// <summary>Finds the repo's real content files, so tests run against what the game ships.</summary>
    static class TestContent
    {
        public static string Root
        {
            get
            {
                var dir = new DirectoryInfo(AppContext.BaseDirectory);
                while (dir != null && !File.Exists(Path.Combine(dir.FullName, "content", "blocks", "blocks.json")))
                    dir = dir.Parent;
                if (dir == null) throw new DirectoryNotFoundException("Could not find the content folder above " + AppContext.BaseDirectory);
                return Path.Combine(dir.FullName, "content");
            }
        }

        public static BlockRegistry Blocks()
        {
            return BlockRegistry.FromJson(File.ReadAllText(Path.Combine(Root, "blocks", "blocks.json")));
        }

        public static Worldgen.WorldMap Map()
        {
            return Worldgen.WorldMap.FromText(File.ReadAllText(Path.Combine(Root, "world", "map.json")),
                                              File.ReadAllText(Path.Combine(Root, "world", "aruun_biomes.txt")));
        }
    }
}
