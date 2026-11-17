using System;
using Aruun.Core.Content;

namespace Aruun.Core.Mining
{
    /// <summary>
    /// Block health and tool tiers. Pure functions the host uses to judge a dig.
    ///
    /// A block breaks when the tool is the right kind (any tool for "hand" blocks, an axe for
    /// wood, a pick for stone) and its dig power reaches the block's hardness. How long it takes
    /// is the block's health over the tool's dig speed, a little faster for each tier of power
    /// to spare. Using a tool of the wrong kind on a "hand" block digs at bare-hand speed.
    /// </summary>
    public static class DigRules
    {
        /// <summary>Health worn off per second with bare hands, or any tool on a block it does not fit.</summary>
        public const double HandSpeed = 2.0;
        /// <summary>Each tier of dig power above the block's hardness digs this much faster.</summary>
        public const double SpareTierBonus = 0.25;

        /// <summary>Can a dig of this kind and power break this block? Air and unbreakable blocks never break.</summary>
        public static bool CanBreak(BlockDef block, ToolClass tool, int power)
        {
            if (block.Id == 0 || block.Unbreakable || block.Hardness > power) return false;
            return tool == ToolClass.Any || block.Tool == ToolClass.Hand || block.Tool == tool;
        }

        public static bool CanBreak(BlockDef block, ToolDef tool)
        {
            return CanBreak(block, tool.Class, tool.Power);
        }

        /// <summary>Health worn off per second, or 0 when the tool cannot break the block at all.</summary>
        public static double Rate(BlockDef block, ToolDef tool)
        {
            if (!CanBreak(block, tool)) return 0;
            bool fits = block.Tool == tool.Class;
            double speed = fits ? tool.Speed : Math.Min(tool.Speed, HandSpeed);
            int spare = Math.Max(0, tool.Power - block.Hardness);
            return speed * (1.0 + SpareTierBonus * spare);
        }

        /// <summary>Seconds of steady digging to break the block, or infinity when it cannot be broken.</summary>
        public static double SecondsToBreak(BlockDef block, ToolDef tool)
        {
            double rate = Rate(block, tool);
            if (rate <= 0) return double.PositiveInfinity;
            return block.Health / rate;
        }

        /// <summary>A short line for the player when a dig is refused, or "" when it is allowed.</summary>
        public static string WhyNot(BlockDef block, ToolDef tool)
        {
            if (block.Id == 0) return "";
            if (block.Unbreakable) return block.Name + " cannot be broken";
            if (block.Tool != ToolClass.Hand && block.Tool != tool.Class)
                return block.Name + " needs " + (block.Tool == ToolClass.Axe ? "an axe" : "a pick");
            if (block.Hardness > tool.Power) return block.Name + " is too hard for " + tool.Name.ToLowerInvariant();
            return "";
        }
    }
}
