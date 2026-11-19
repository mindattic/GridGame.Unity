using System.Linq;
using GridGame.Core.Content;
using GridGame.Meta;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>GG-T432 (§25.12.2): gold-bought facility levels at the Forge and the Alchemist gate material tiers.</summary>
    public class FacilityTests
    {
        private static RecipeDef ForgeRecipe(ContentDb c, ItemRarity rarity) =>
            Crafting.Recipes(c, CraftStation.Forge).First(r => r.Tier == 1 && r.BaseItem == null && !Forging.IsSetArmor(c, r) && c.Item(r.Result).Rarity == rarity);

        private static SaveData Stocked(ContentDb c, RecipeDef r, int extraGold = 0)
        {
            var s = Progression.NewGame(c, "Facility");
            s.Flags.Add(Forging.FirstForgeFlag);
            s.Gold = r.Gold + extraGold;
            foreach (var ic in r.Inputs) s.AddItem(ic.Item, ic.Count);
            return s;
        }

        [Test]
        public void The_forge_and_the_still_each_have_a_rising_material_ladder_and_the_enchanter_none()
        {
            var c = Real;
            var forge = Facilities.Def(c, CraftStation.Forge);
            var still = Facilities.Def(c, CraftStation.Alchemy);
            Assert.That(forge, Is.Not.Null);
            Assert.That(still, Is.Not.Null);
            Assert.That(Facilities.Def(c, CraftStation.Enchant), Is.Null);
            Assert.That(forge.Levels.Select(l => l.Name), Is.EqualTo(new[] { "Iron Forge", "Steel Forge", "Mythril Forge" }));
            Assert.That(forge.BaseName, Is.EqualTo("Copper Forge"));
            foreach (var d in new[] { forge, still })
                for (int i = 1; i < d.Levels.Count; i++) Assert.That(d.Levels[i].Cost, Is.GreaterThan(d.Levels[i - 1].Cost), $"{d.Id} level {i + 1}");
            // Every Forge and Alchemy recipe lands somewhere on its ladder, and every rung has recipes behind it.
            foreach (var st in new[] { CraftStation.Forge, CraftStation.Alchemy })
            {
                var d = Facilities.Def(c, st);
                var needed = Crafting.Recipes(c, st).Select(r => Facilities.Required(c, r)).Distinct().ToList();
                for (int lv = 0; lv <= d.Levels.Count; lv++) Assert.That(needed, Does.Contain(lv), $"{st}: nothing needs level {lv}");
            }
        }

        [Test]
        public void A_rare_recipe_shows_needs_iron_forge_until_it_is_built()
        {
            var c = Real;
            var common = ForgeRecipe(c, ItemRarity.Common);
            var rare = ForgeRecipe(c, ItemRarity.Rare);
            var s = Stocked(c, common);
            Assert.That(Crafting.CanMake(c, s, common), Is.True, "the Copper Forge works common metal");
            Assert.That(Crafting.Craft(c, s, common).Ok, Is.True);

            s = Stocked(c, rare);
            Assert.That(Crafting.IsUnlocked(s, rare), Is.True, "the station's skill tier is not what blocks it");
            Assert.That(Crafting.CanMake(c, s, rare), Is.False);
            Assert.That(Facilities.NeededName(c, rare), Is.EqualTo("Iron Forge"));
            var refused = Crafting.Craft(c, s, rare);
            Assert.That(refused.Ok, Is.False);
            Assert.That(refused.Message, Is.EqualTo("Needs Iron Forge"));
            Assert.That(rare.Inputs.All(ic => s.Count(ic.Item) >= ic.Count), Is.True, "a refused craft consumes nothing");

            var cost = Facilities.Next(c, s, CraftStation.Forge).Cost;
            s.Gold += cost;
            Assert.That(Facilities.Build(c, s, CraftStation.Forge).Ok, Is.True);
            Assert.That(s.Gold, Is.EqualTo(rare.Gold), "the build cost exactly its price");
            Assert.That(Facilities.Current(c, s, CraftStation.Forge), Is.EqualTo("Iron Forge"));
            Assert.That(Crafting.Craft(c, s, rare).Ok, Is.True);
        }

        [Test]
        public void A_facility_costs_gold_and_stops_at_the_top()
        {
            var c = Real; var s = Progression.NewGame(c, "Facility2");
            s.Gold = 0;
            Assert.That(Facilities.Build(c, s, CraftStation.Alchemy).Ok, Is.False);
            s.Gold = 100000;
            var d = Facilities.Def(c, CraftStation.Alchemy);
            for (int i = 0; i < d.Levels.Count; i++) Assert.That(Facilities.Build(c, s, CraftStation.Alchemy).Ok, Is.True);
            Assert.That(Facilities.Next(c, s, CraftStation.Alchemy), Is.Null);
            Assert.That(Facilities.Build(c, s, CraftStation.Alchemy).Ok, Is.False);
            Assert.That(Crafting.Recipes(c, CraftStation.Alchemy).All(r => Facilities.Allows(c, s, r)), Is.True);
        }

        [Test]
        public void Prices_are_a_real_gold_sink_against_measured_stage_pay()
        {
            // Measured 2026-11-17: ~210-270 G a stage at Lv 1-6, 400-600 G from Lv 9, ~9,900 G for the main campaign.
            var c = Real;
            var forge = Facilities.Def(c, CraftStation.Forge).Levels;
            Assert.That(forge[0].Cost, Is.InRange(210, 600), "the first rung is one or two early stages");
            Assert.That(forge[forge.Count - 1].Cost, Is.GreaterThanOrEqualTo(4 * 500), "the top rung is several late stages");
            int all = Facilities.Def(c, CraftStation.Forge).Levels.Sum(l => l.Cost) + Facilities.Def(c, CraftStation.Alchemy).Levels.Sum(l => l.Cost);
            Assert.That(all, Is.LessThan(9900), "the whole ladder fits inside one campaign's pay");
        }

        [Test]
        public void Building_a_facility_does_not_open_the_caravan_row_early()
        {
            var s = new SaveData { HighestClearedStage = -1 };
            s.Caravan[CaravanUpgradeDef.ForgeFacilityId] = 1;
            Assert.That(CampUnlocks.CaravanOpen(s), Is.False);
            s.Caravan["armory"] = 1;
            Assert.That(CampUnlocks.CaravanOpen(s), Is.True);
        }

        [Test]
        public void An_older_save_keeps_the_facilities_its_station_skill_had_reached()
        {
            var apprentice = new SaveData { Version = 9 };
            SaveMigrations.Migrate(apprentice);
            Assert.That(CaravanUpgrades.Level(apprentice, CaravanUpgradeDef.ForgeFacilityId), Is.EqualTo(0));
            Assert.That(apprentice.Version, Is.EqualTo(SaveData.CurrentVersion));

            var veteran = new SaveData { Version = 9 };
            veteran.StationXp["blacksmith"] = 200; // Journeyman
            veteran.StationXp["alchemist"] = 9999; // Master
            veteran.Caravan["armory"] = 2;
            SaveMigrations.Migrate(veteran);
            Assert.That(CaravanUpgrades.Level(veteran, CaravanUpgradeDef.ForgeFacilityId), Is.EqualTo(1));
            Assert.That(CaravanUpgrades.Level(veteran, CaravanUpgradeDef.AlchemyFacilityId), Is.EqualTo(2));
            Assert.That(CaravanUpgrades.Level(veteran, "armory"), Is.EqualTo(2), "bought caravan upgrades are untouched");

            // A new save at the current version never gets a free facility from its station XP.
            var fresh = new SaveData();
            fresh.StationXp["blacksmith"] = 9999;
            SaveMigrations.Migrate(fresh);
            Assert.That(CaravanUpgrades.Level(fresh, CaravanUpgradeDef.ForgeFacilityId), Is.EqualTo(0));
        }

        [Test]
        public void Camp_routine_builds_the_next_facility_when_it_costs_half_the_purse_or_less()
        {
            var c = Real; var s = Progression.NewGame(c, "FacilityBot");
            foreach (var npc in new[] { CampNpcs.Blacksmith, CampNpcs.Alchemist }) s.Flags.Add("npc:" + npc);
            int forgeCost = Facilities.Next(c, s, CraftStation.Forge).Cost;
            s.Gold = forgeCost; // too dear: it would take the whole purse
            CampRoutine.Run(c, s);
            Assert.That(Facilities.Level(c, s, CraftStation.Forge), Is.EqualTo(0));

            s.Gold = 100000;
            CampRoutine.Run(c, s);
            Assert.That(Facilities.Level(c, s, CraftStation.Forge), Is.GreaterThanOrEqualTo(1));
            Assert.That(Facilities.Level(c, s, CraftStation.Alchemy), Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void A_station_without_its_keeper_is_never_built_by_the_bot()
        {
            var c = Real; var s = Progression.NewGame(c, "FacilityBot2");
            s.Gold = 100000;
            CampRoutine.Run(c, s);
            Assert.That(Facilities.Level(c, s, CraftStation.Forge), Is.EqualTo(0));
            Assert.That(Facilities.Level(c, s, CraftStation.Alchemy), Is.EqualTo(0));
        }
    }
}
