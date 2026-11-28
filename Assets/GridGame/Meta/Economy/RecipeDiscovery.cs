using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Core.Rules;

namespace GridGame.Meta
{
    // ─────────────────────────────────────────────────────────────────────────────
    // Recipe discovery (§24.6.2, GG-T36): recipes start hidden; a recipe is discovered once its
    // station reaches its tier (§24.6.1), or earlier by reading a recipe scroll or first-ever
    // pickup of the recipe's rarest input. Discovery only gates visibility — Crafting.Craft's own
    // gold/inputs/tier checks are unchanged, since a discovered-but-locked recipe already fails
    // IsUnlocked, and a tier-reached recipe is discovered by definition.
    // ─────────────────────────────────────────────────────────────────────────────

    public static class RecipeDiscovery
    {
        public static bool IsDiscovered(SaveData s, RecipeDef r) =>
            r != null && (Crafting.IsUnlocked(s, r) || s.DiscoveredRecipes.Contains(r.Id));

        public static void Discover(SaveData s, RecipeDef r) { if (r != null) s.DiscoveredRecipes.Add(r.Id); }

        /// <summary>The input material with the highest rarity (ties broken by cost, then id) — first-ever
        /// pickup of this item discovers the recipe.</summary>
        public static ItemDef RarestInput(ContentDb c, RecipeDef r)
        {
            ItemDef rarest = null;
            foreach (var ic in r.Inputs)
            {
                var item = c.TryItem(ic.Item);
                if (item == null) continue;
                if (rarest == null || item.Rarity > rarest.Rarity ||
                    (item.Rarity == rarest.Rarity && (item.BaseCost > rarest.BaseCost ||
                        (item.BaseCost == rarest.BaseCost && string.CompareOrdinal(item.Id, rarest.Id) < 0))))
                    rarest = item;
            }
            return rarest;
        }

        /// <summary>Call when the player's first-ever copy of an item lands in the bag (battle loot) —
        /// discovers every still-hidden recipe whose rarest input is that item.</summary>
        public static void OnItemAcquired(ContentDb c, SaveData s, string itemId)
        {
            foreach (var r in c.Recipes.Values)
            {
                if (IsDiscovered(s, r)) continue;
                if (RarestInput(c, r)?.Id == itemId) Discover(s, r);
            }
        }

        /// <summary>Reads a recipe scroll: learns its recipe outright (even if still tier-locked) and consumes it.</summary>
        public static Result Learn(ContentDb c, SaveData s, string scrollItemId)
        {
            var item = c.TryItem(scrollItemId);
            if (item == null || item.Type != ItemType.RecipeScroll) return Result.Fail("Can't be read");
            if (s.Count(scrollItemId) < 1) return Result.Fail("None left");
            var recipe = c.TryRecipe(item.TeachesRecipe);
            if (recipe == null) return Result.Fail("This page leads nowhere");
            bool already = IsDiscovered(s, recipe);
            s.AddItem(scrollItemId, -1);
            Discover(s, recipe);
            var result = c.TryItem(recipe.Result);
            return Result.Success(already ? $"You already knew how to make {result?.Name ?? recipe.Result}" : $"Learned: {result?.Name ?? recipe.Result}");
        }

        /// <summary>Not discovered, but missing exactly one item (an input, or the base item an
        /// upgrade/enchant consumes) — the Forge/Brew list's "???" tease. Null when discovered, already
        /// craftable, or still missing more than one thing.</summary>
        public static ItemCount OneAwayMissing(SaveData s, RecipeDef r)
        {
            if (IsDiscovered(s, r)) return null;
            ItemCount missing = null;
            if (r.BaseItem != null && s.Count(r.BaseItem) < 1) missing = new ItemCount { Item = r.BaseItem, Count = 1 };
            foreach (var ic in r.Inputs)
            {
                if (s.Count(ic.Item) < ic.Count)
                {
                    if (missing != null) return null;
                    missing = ic;
                }
            }
            return missing;
        }
    }
}
