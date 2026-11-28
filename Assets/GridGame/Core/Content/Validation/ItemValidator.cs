using System;
using System.Collections.Generic;
using static GridGame.Core.Content.ContentLoader;

namespace GridGame.Core.Content
{
    /// <summary>The item, recipe, caravan facility and drop table checks. Part of <see cref="ContentLoader.Validate"/>.</summary>
    internal static class ItemValidator
    {
        internal static void Validate(ContentDb db, List<string> p)
        {
            void NeedItem(string id, string where) { if (id != null && !db.Items.ContainsKey(id)) p.Add($"{where}: unknown item '{id}'"); }
            // An alias (GG-T973) is reported by ValidateAbilityAliases with the id to use instead.
            void NeedAbility(string id, string where) { if (id != null && !db.Abilities.ContainsKey(id) && !db.AbilityAliases.ContainsKey(id)) p.Add($"{where}: unknown ability '{id}'"); }
            void NeedBuff(string id, string where) { if (id != null && !db.Buffs.ContainsKey(id)) p.Add($"{where}: unknown buff '{id}'"); }
            foreach (var i in db.Items.Values)
            {
                NeedAbility(i.OnUseAbility, $"item {i.Id}");
                if (!(i.EvasionBonus >= 0f && i.EvasionBonus <= 20f))
                    p.Add($"item {i.Id}: EvasionBonus {i.EvasionBonus} outside 0..20 (GG-T297)");
                NeedBuff(i.CuresBuff, $"item {i.Id} cures");
                NeedBuff(i.PincerApplyBuff, $"item {i.Id} pincer buff");
                if (i.Type == ItemType.RecipeScroll)
                {
                    if (i.TeachesRecipe == null) p.Add($"item {i.Id}: RecipeScroll needs TeachesRecipe");
                    else if (!db.Recipes.ContainsKey(i.TeachesRecipe)) p.Add($"item {i.Id}: unknown recipe '{i.TeachesRecipe}'");
                }
            }
            foreach (var r in db.Recipes.Values)
            {
                NeedItem(r.Result, $"recipe {r.Id}");
                NeedItem(r.BaseItem, $"recipe {r.Id} base");
                foreach (var ic in r.Inputs) NeedItem(ic.Item, $"recipe {r.Id}");
                NeedItem(r.PotencyResult, $"recipe {r.Id} potency");
                foreach (var ic in r.PotencyInputs) NeedItem(ic.Item, $"recipe {r.Id} potency");
                // GG-T177 (§25.0.6): a starter recipe is the guided first forge — always affordable, so no gold.
                if (r.Starter && (r.Station != CraftStation.Forge || r.BaseItem != null || r.Tier > 1 || r.Gold != 0 || r.ResultCount != 1
                                  || (db.Items.TryGetValue(r.Result ?? "", out var made) && made.Slot != EquipmentSlot.Weapon)))
                    p.Add($"recipe {r.Id}: a Starter recipe is an Apprentice Forge recipe for one weapon that costs no gold");
            }
            // GG-T432 (§25.12.2): a facility is a caravan upgrade whose levels name and gate a rising material tier.
            foreach (var u in db.Caravan.Values)
            {
                bool facilityId = u.Id == CaravanUpgradeDef.ForgeFacilityId || u.Id == CaravanUpgradeDef.AlchemyFacilityId;
                if (u.IsFacility != facilityId) { p.Add($"caravan {u.Id}: only '{CaravanUpgradeDef.ForgeFacilityId}' and '{CaravanUpgradeDef.AlchemyFacilityId}' are facilities, and each needs a BaseRarity"); continue; }
                if (!u.IsFacility) continue;
                if (string.IsNullOrEmpty(u.BaseName)) p.Add($"caravan {u.Id}: a facility needs a BaseName");
                var prev = u.BaseRarity.Value;
                for (int i = 0; i < u.Levels.Count; i++)
                {
                    var lv = u.Levels[i];
                    if (string.IsNullOrEmpty(lv.Name)) p.Add($"caravan {u.Id} level {i + 1}: a facility level needs a Name");
                    if (lv.Cost <= 0) p.Add($"caravan {u.Id} level {i + 1}: a facility level costs gold");
                    if (!lv.Rarity.HasValue || lv.Rarity.Value <= prev) p.Add($"caravan {u.Id} level {i + 1}: Rarity must rise above the level before it");
                    else prev = lv.Rarity.Value;
                }
            }
            foreach (var d in db.DropTables.Values)
                foreach (var e in d.Entries) NeedItem(e.Item, $"droptable {d.Id}");
        }
    }
}
