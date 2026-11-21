using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Content;
using GridGame.Meta;
using NUnit.Framework;

namespace GridGame.Tests
{
    /// <summary>GG-T538 (BIBLE §25.0.10), owner 2026-11-21: "some are progress gated, some are training gated, the usual."
    /// The Merchant's stock and the Forge and Alchemy recipe lists start small and grow with progress tiers (region clears)
    /// and the station's rank; nothing a player already has goes away.</summary>
    public class VendorTierTests
    {
        private static ContentDb Real => TestKit.Real;

        private static SaveData Cleared(int stages) => new SaveData { HighestClearedStage = stages - 1, Gold = 100000 };

        [Test]
        public void A_fresh_merchant_shows_a_small_shelf_with_the_tutorials_purchases()
        {
            var c = Real;
            var shelf = Shop.Stock(c, new SaveData());
            Assert.That(shelf.Count, Is.InRange(10, 22), string.Join(", ", shelf.Select(i => i.Id)));
            Assert.That(shelf.Count, Is.LessThan(Shop.Stock(c).Count / 3));
            foreach (var id in new[] { "healing_potion_basic", ContentLoader.EmptyBottle, "cons_purifying_salts", "cons_disarm_kit" })
                Assert.That(shelf, Does.Contain(c.Item(id)), id);
            Assert.That(shelf.Count(i => i.Slot == EquipmentSlot.Weapon), Is.GreaterThanOrEqualTo(3), "a starter weapon per weapon class");
        }

        [Test]
        public void The_shelf_grows_as_regions_are_cleared_and_every_item_is_stocked_by_the_last_tier()
        {
            var c = Real;
            int last = 0;
            for (int t = 1; t <= VendorTiers.Count; t++)
            {
                var s = Cleared(VendorTiers.ClearsFor(c, t));
                Assert.That(VendorTiers.ProgressTier(c, s), Is.EqualTo(t));
                int n = Shop.Stock(c, s).Count;
                Assert.That(n, Is.GreaterThanOrEqualTo(last), $"tier {t}");
                last = n;
            }
            Assert.That(last, Is.EqualTo(Shop.Stock(c).Count), "everything the Merchant sells is on the shelf by the last tier");
            Assert.That(VendorTiers.ProgressTier(c, Cleared(VendorTiers.ClearsFor(c, 2) - 1)), Is.EqualTo(1), "one stage short stays at tier 1");
        }

        [Test]
        public void An_item_off_the_shelf_cant_be_bought_and_tells_when_it_arrives()
        {
            var c = Real;
            var late = Shop.Stock(c).First(i => VendorTiers.ShopTier(i) > 1 && !VendorTiers.NeededCounter(c, new SaveData(), i));
            var s = new SaveData { Gold = 100000 };
            var r = Shop.Buy(c, s, new Dictionary<string, int> { [late.Id] = 1 });
            Assert.That(r.Ok, Is.False);
            Assert.That(r.Message, Does.Contain("after "));
            Assert.That(s.Count(late.Id), Is.EqualTo(0));
            Assert.That(s.Gold, Is.EqualTo(100000));
        }

        [Test]
        public void Something_once_bought_stays_on_the_shelf()
        {
            var c = Real;
            var late = Shop.Stock(c).First(i => VendorTiers.ShopTier(i) == 2 && i.MaxStack > 1);
            var s = Cleared(VendorTiers.ClearsFor(c, 2));
            Assert.That(Shop.Buy(c, s, new Dictionary<string, int> { [late.Id] = 1 }).Ok, Is.True);
            s.HighestClearedStage = -1; // a Test Mode reset or a migration oddity: what was bought still stays
            Assert.That(Shop.Stock(c, s), Does.Contain(late));
        }

        [Test]
        public void Test_mode_and_new_game_plus_see_every_tier()
        {
            var c = Real;
            Assert.That(Shop.Stock(c, new SaveData { TestMode = true }).Count, Is.EqualTo(Shop.Stock(c).Count));
            Assert.That(VendorTiers.ProgressTier(c, new SaveData { NewGamePlus = 1 }), Is.EqualTo(VendorTiers.Count));
        }

        [Test]
        public void Recipe_tiers_follow_their_materials_and_never_orphan()
        {
            var c = Real;
            foreach (var r in c.Recipes.Values.Where(VendorTiers.Gated))
            {
                int t = VendorTiers.RecipeTier(c, r);
                Assert.That(t, Is.InRange(1, VendorTiers.Count), r.Id);
                foreach (var ic in r.Inputs)
                    Assert.That(VendorTiers.ItemTier(c, ic.Item), Is.InRange(1, t), $"{r.Id}: {ic.Item} can be had by its tier");
                Assert.That(t, Is.GreaterThanOrEqualTo(VendorTiers.RarityTier(c.Item(r.Result).Rarity)), r.Id);
            }
        }

        [Test]
        public void The_guided_first_forge_armor_and_brew_sit_in_the_first_tier()
        {
            var c = Real;
            var s = Progression.NewGame(c, "T");
            foreach (var r in Forging.StarterRecipes(c)) Assert.That(VendorTiers.RecipeOpen(c, s, r), Is.True, r.Id);
            Assert.That(Forging.SetArmorRecipes(c).Any(r => r.Tier <= 1 && VendorTiers.RecipeOpen(c, s, r)), Is.True, "a first armor piece");
            Assert.That(VendorTiers.RecipeOpen(c, s, c.Recipes["recipe_bottle_healing"]), Is.True, "the first brew");
            int fresh = c.Recipes.Values.Count(r => r.Station == CraftStation.Forge && r.BaseItem == null && VendorTiers.RecipeOpen(c, s, r) && Crafting.IsUnlocked(s, r));
            Assert.That(fresh, Is.LessThan(Crafting.Recipes(c, CraftStation.Forge).Count(r => r.BaseItem == null) / 3), "a fresh Forge lists a short list");
        }

        [Test]
        public void A_crafted_or_learned_recipe_stays_listed_whatever_its_tier()
        {
            var c = Real;
            var s = new SaveData();
            var late = c.Recipes.Values.First(r => VendorTiers.Gated(r) && VendorTiers.RecipeTier(c, r) > 1);
            Assert.That(VendorTiers.RecipeOpen(c, s, late), Is.False);
            s.CraftedRecipes.Add(late.Id);
            Assert.That(VendorTiers.RecipeOpen(c, s, late), Is.True);
            var learned = c.Recipes.Values.Last(r => VendorTiers.Gated(r) && VendorTiers.RecipeTier(c, r) > 1);
            s.DiscoveredRecipes.Add(learned.Id);
            Assert.That(VendorTiers.RecipeOpen(c, s, learned), Is.True, "a scroll or a first-ever loot discovers it");
        }

        [Test]
        public void An_older_save_keeps_what_it_owns_and_gets_the_tiers_its_progress_reached()
        {
            var c = Real;
            var old = Progression.NewGame(c, "Old");
            old.Version = 12;
            old.HighestClearedStage = VendorTiers.ClearsFor(c, 3) - 1;
            var lateItem = Shop.Stock(c).First(i => VendorTiers.ShopTier(i) >= 5 && i.MaxStack > 1);
            old.AddItem(lateItem.Id, 1);
            var lateRecipe = c.Recipes.Values.First(r => r.Station == CraftStation.Forge && r.BaseItem == null && VendorTiers.RecipeTier(c, r) >= 5);
            old.AddItem(lateRecipe.Result, 1);
            SaveMigrations.Migrate(old, c);
            Assert.That(old.Version, Is.EqualTo(SaveData.CurrentVersion));
            Assert.That(VendorTiers.ProgressTier(c, old), Is.EqualTo(3));
            Assert.That(Shop.Stock(c, old), Does.Contain(lateItem), "an owned vendor item stays on the shelf");
            Assert.That(VendorTiers.RecipeOpen(c, old, lateRecipe), Is.True, "a recipe whose result it owns stays listed");
            Assert.That(old.Count(lateItem.Id), Is.EqualTo(1), "nothing owned is taken away");
        }

        [Test]
        public void The_next_rank_line_counts_crafts_to_go()
        {
            var s = new SaveData();
            s.StationXp["blacksmith"] = 40;
            Assert.That(VendorTiers.NextRankLine(s, StationKind.Blacksmith), Is.EqualTo("Journeyman at 150 XP: 40/150, about 8 more forges or repairs"));
            s.StationXp["alchemist"] = VendorTiers.RankXp[2];
            Assert.That(VendorTiers.NextRankLine(s, StationKind.Alchemist), Is.Null, "Master is the top rank");
        }
    }
}
