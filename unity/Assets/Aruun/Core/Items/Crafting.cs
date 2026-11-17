using System.Collections.Generic;

namespace Aruun.Core.Items
{
    /// <summary>
    /// Recipe rules. The host calls Craft with the real bag; clients call Check only to grey
    /// out buttons. Both return "" for yes, or a reason for the player.
    /// </summary>
    public static class Crafting
    {
        public const string Ok = "";

        /// <param name="stations">What the crafter stands near, such as "fire" or "tether".</param>
        public static string Check(ItemRegistry items, Recipe recipe, Inventory inv, ICollection<string> stations, bool knowsAWord)
        {
            if (recipe == null) return "unknown recipe";
            if (recipe.Station.Length > 0 && !stations.Contains(recipe.Station))
            {
                var s = items.StationStructure(recipe.Station);
                return "needs a " + (s != null ? s.Name.ToLowerInvariant() : recipe.Station) + " nearby";
            }
            if (recipe.NeedsWord && !knowsAWord) return "you must know a word of Avarra to inscribe it";
            if (!inv.HasAll(recipe.In)) return "missing materials";
            return Ok;
        }

        /// <summary>Uses up the inputs and adds the output. A full bag never eats the materials.</summary>
        public static string Craft(ItemRegistry items, Recipe recipe, Inventory inv, ICollection<string> stations, bool knowsAWord)
        {
            string why = Check(items, recipe, inv, stations, knowsAWord);
            if (why != Ok) return why;
            var trial = inv.Copy();
            foreach (var kv in recipe.In) trial.Remove(kv.Key, kv.Value);
            if (trial.Add(recipe.Out, recipe.Count) > 0) return "your bag is full";
            inv.CopyFrom(trial);
            return Ok;
        }
    }
}
