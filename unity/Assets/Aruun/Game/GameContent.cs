using System.IO;
using Aruun.Core.Content;
using UnityEngine;

namespace Aruun.Game
{
    /// <summary>
    /// Finds the content folder (JSON and .vox files). In the editor it is the repo's own
    /// content/ folder next to the Unity project, so artists edit one copy. A built game reads
    /// StreamingAssets/content, which the build step copies in.
    /// </summary>
    public static class GameContent
    {
        public static string Root
        {
            get
            {
                string built = Path.Combine(Application.streamingAssetsPath, "content");
                if (Directory.Exists(built)) return built;
                // <repo>/unity/Assets -> <repo>/content
                return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "content"));
            }
        }

        public static string ReadText(params string[] parts)
        {
            return File.ReadAllText(Path.Combine(Root, Path.Combine(parts)));
        }

        public static BlockRegistry LoadBlocks()
        {
            return BlockRegistry.FromJson(ReadText("blocks", "blocks.json"));
        }
    }
}
