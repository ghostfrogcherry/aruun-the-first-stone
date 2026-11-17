using System;

namespace Aruun.Core.Mining
{
    /// <summary>
    /// What kind of tool a dig was made with, or what kind a block needs.
    ///
    /// On a block: Hand means anything breaks it, even bare hands (soil, sand, leaves, flowers).
    /// Axe blocks are wood and Pick blocks are stone and metal: only that kind of tool breaks them.
    ///
    /// On an edit: Any is the world's own edits (worldgen, admin, explosions, the journal of old
    /// saves) and ignores tool kinds; only dig power counts.
    /// </summary>
    public enum ToolClass : byte { Any = 0, Hand = 1, Axe = 2, Pick = 3 }

    public static class ToolClasses
    {
        public static ToolClass Parse(string text, ToolClass fallback)
        {
            switch (text)
            {
                case "": return fallback;
                case "any": return ToolClass.Any;
                case "hand": return ToolClass.Hand;
                case "axe": return ToolClass.Axe;
                case "pick": return ToolClass.Pick;
                default: throw new FormatException("Unknown tool kind '" + text + "' (use hand, axe or pick)");
            }
        }

        public static string Name(ToolClass c)
        {
            switch (c)
            {
                case ToolClass.Hand: return "hand";
                case ToolClass.Axe: return "axe";
                case ToolClass.Pick: return "pick";
                default: return "any";
            }
        }
    }
}
