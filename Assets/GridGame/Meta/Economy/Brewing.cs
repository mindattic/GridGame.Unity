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
    // Bottle brewing (GG-T176, §25.0.5): an Empty Bottle from the Merchant plus looted monster trophies brews a
    // potion for less than the Merchant charges for it.
    // ─────────────────────────────────────────────────────────────────────────────

    public static class Brewing
    {
        public static bool IsBottleRecipe(RecipeDef r) =>
            r != null && r.Station == CraftStation.Alchemy && r.Inputs.Any(i => i.Item == ContentLoader.EmptyBottle);

        public static List<RecipeDef> BottleRecipes(ContentDb c) => Crafting.Recipes(c, CraftStation.Alchemy).Where(IsBottleRecipe).ToList();

        /// <summary>The bag holds every looted input of <paramref name="r"/> — everything but its Empty Bottles.</summary>
        public static bool HasLoot(SaveData s, RecipeDef r) =>
            s != null && r != null && r.Inputs.All(i => i.Item == ContentLoader.EmptyBottle || s.Count(i.Item) >= i.Count);

        /// <summary>The bottle recipe the Alchemist's camp tip walks through: one this save can brew right now, else
        /// one whose loot is already in the bag (only the bottle missing), else null.</summary>
        public static RecipeDef FirstBrew(ContentDb c, SaveData s)
        {
            if (s == null) return null;
            var open = BottleRecipes(c).Where(r => Crafting.CanMake(c, s, r) && VendorTiers.RecipeOpen(c, s, r)).ToList();
            return open.FirstOrDefault(r => Crafting.HasInputs(s, r) && s.Gold >= r.Gold) ?? open.FirstOrDefault(r => HasLoot(s, r));
        }

        /// <summary>Gold to brew <paramref name="r"/> with every input bought at the Merchant's price, plus its fee —
        /// always less than buying what it makes (<see cref="BuyCost"/>).</summary>
        public static int ShopCost(ContentDb c, RecipeDef r) => r.Gold + r.Inputs.Sum(i => (c.TryItem(i.Item)?.BaseCost ?? 0) * i.Count);

        /// <summary>What the recipe's output would cost bought outright.</summary>
        public static int BuyCost(ContentDb c, RecipeDef r) => (c.TryItem(r.Result)?.BaseCost ?? 0) * r.ResultCount;
    }
}
