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
    // Caravan upgrades (§25.12): the long-run gold sink that changes how travel plays
    // ─────────────────────────────────────────────────────────────────────────────

    public static class CaravanUpgrades
    {
        public static int Level(SaveData s, string id) => s.Caravan.TryGetValue(id, out var n) ? n : 0;

        /// <summary>The current level's value, or <paramref name="none"/> when not bought.</summary>
        public static float Value(ContentDb c, SaveData s, string id, float none = 0f)
        {
            int lv = Level(s, id);
            if (lv <= 0 || !c.Caravan.TryGetValue(id, out var def) || def.Levels.Count == 0) return none;
            return def.Levels[Math.Min(lv, def.Levels.Count) - 1].Value;
        }

        public static CaravanLevel NextLevel(ContentDb c, SaveData s, string id) =>
            c.Caravan.TryGetValue(id, out var def) && Level(s, id) < def.Levels.Count ? def.Levels[Level(s, id)] : null;

        public static Result Buy(ContentDb c, SaveData s, string id)
        {
            if (!c.Caravan.TryGetValue(id, out var def)) return Result.Fail("Unknown upgrade");
            var next = NextLevel(c, s, id);
            if (next == null) return Result.Fail("Fully upgraded");
            if (s.Gold < next.Cost) return Result.Fail("Not enough gold");
            s.Gold -= next.Cost;
            s.Caravan[id] = Level(s, id) + 1;
            return Result.Success($"{def.Name}: {next.Text}");
        }

        /// <summary>Upgrade id of the Enchanter's Wagon: armor enchants cost less gold (GG-T75).</summary>
        public const string EnchanterWagon = "enchanter_wagon";
        /// <summary>Upgrade id of the Quartermaster's Ledger: salvage returns more materials (GG-T75).</summary>
        public const string QuartermasterLedger = "quartermaster_ledger";

        /// <summary>Gold after the Enchanter's Wagon discount (never below 1 for a priced enchant).</summary>
        public static int EnchantGold(ContentDb c, SaveData s, int gold)
        {
            if (c == null || s == null || gold <= 0) return gold;
            float off = Value(c, s, EnchanterWagon);
            return off <= 0f ? gold : Math.Max(1, (int)Math.Ceiling(gold * (1f - off)));
        }

        /// <summary>Salvage yield after the Quartermaster's Ledger: each material stack grows by its level's fraction (rounded half up).</summary>
        public static List<ItemCount> LedgerYield(ContentDb c, SaveData s, List<ItemCount> yield)
        {
            if (c == null || s == null || yield == null) return yield;
            float bonus = Value(c, s, QuartermasterLedger);
            if (bonus <= 0f) return yield;
            return yield.Select(ic => new ItemCount { Item = ic.Item, Count = ic.Count + (int)Math.Floor(ic.Count * bonus + 0.5f) }).ToList();
        }

        /// <summary>A rotating one-line crew bark (GG-T75): flavor only — no branching, no choices, nothing to answer.
        /// Drawn from the crews whose wagon this save has upgraded, or from every crew before the first upgrade.
        /// <paramref name="turn"/> picks the line, so callers rotate it by passing a counter.</summary>
        public static (CaravanUpgradeDef Crew, string Line) Bark(ContentDb c, SaveData s, int turn)
        {
            if (c == null) return (null, null);
            var crews = c.Caravan.Values.Where(d => d.Barks != null && d.Barks.Count > 0).ToList();
            var bought = s == null ? new List<CaravanUpgradeDef>() : crews.Where(d => Level(s, d.Id) > 0).ToList();
            var pool = (bought.Count > 0 ? bought : crews).SelectMany(d => d.Barks.Select(line => (d, line))).ToList();
            if (pool.Count == 0) return (null, null);
            int i = ((turn % pool.Count) + pool.Count) % pool.Count;
            return pool[i];
        }

        /// <summary>How a crew member is named on screen: "Brannoc, cask-keeper".</summary>
        public static string CrewName(CaravanUpgradeDef d) =>
            d == null || string.IsNullOrEmpty(d.Crew) ? "" : string.IsNullOrEmpty(d.CrewRole) ? d.Crew : $"{d.Crew}, {d.CrewRole}";
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Facility upgrades (§25.12.2, GG-T432): gold-bought station levels that gate material tiers
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Owner 2026-11-19: "you have to unlock the shops abilities to increase the value of things that can be built
    /// there … copper, iron, steel". Each crafting station (Forge, Alchemy; not Enchant) has a facility — a caravan
    /// upgrade in caravan.json (<see cref="CaravanUpgradeDef.IsFacility"/>), its levels kept in
    /// <see cref="SaveData.Caravan"/> like any other. A recipe needs the facility level whose material tier covers its
    /// result's rarity: the Copper Forge makes up to Uncommon, the Iron Forge Rare, the Steel Forge Epic, the Mythril
    /// Forge Legendary. A separate axis from the station's XP tier (§24.6.1, skill), which still gates
    /// <see cref="RecipeDef.Tier"/>; a recipe needs both.
    /// </summary>
    public static class Facilities
    {
        public static CaravanUpgradeDef Def(ContentDb c, CraftStation station)
        {
            var id = CaravanUpgradeDef.FacilityId(station);
            return c != null && id != null && c.Caravan.TryGetValue(id, out var d) && d.IsFacility ? d : null;
        }

        /// <summary>Whether a caravan upgrade id is a facility (by id alone, so a save can be checked without content).</summary>
        public static bool IsFacilityId(string id) => id == CaravanUpgradeDef.ForgeFacilityId || id == CaravanUpgradeDef.AlchemyFacilityId;

        /// <summary>The station's bought facility level: 0 is the base facility (Copper Forge, Clay Still).</summary>
        public static int Level(ContentDb c, SaveData s, CraftStation station)
        {
            var d = Def(c, station);
            return d == null || s == null ? 0 : Math.Min(CaravanUpgrades.Level(s, d.Id), d.Levels.Count);
        }

        /// <summary>The facility level a recipe needs: the first whose rarity covers its result's (0 = the base facility).</summary>
        public static int Required(ContentDb c, RecipeDef r)
        {
            var d = r == null ? null : Def(c, r.Station);
            var item = d == null ? null : c.TryItem(r.Result);
            if (item == null || item.Rarity <= d.BaseRarity.Value) return 0;
            for (int i = 0; i < d.Levels.Count; i++)
                if (d.Levels[i].Rarity is ItemRarity cap && item.Rarity <= cap) return i + 1;
            return d.Levels.Count;
        }

        public static bool Allows(ContentDb c, SaveData s, RecipeDef r) => Level(c, s, r.Station) >= Required(c, r);

        /// <summary>"Copper Forge" at level 0, "Iron Forge" at 1, …</summary>
        public static string Name(ContentDb c, CraftStation station, int level)
        {
            var d = Def(c, station);
            if (d == null) return "";
            return level <= 0 ? d.BaseName : d.Levels[Math.Min(level, d.Levels.Count) - 1].Name;
        }

        public static string Current(ContentDb c, SaveData s, CraftStation station) => Name(c, station, Level(c, s, station));

        /// <summary>The facility a recipe needs, by name: "Iron Forge".</summary>
        public static string NeededName(ContentDb c, RecipeDef r) => Name(c, r.Station, Required(c, r));

        /// <summary>The next facility level to buy, or null when it is fully built (or the station has none).</summary>
        public static CaravanLevel Next(ContentDb c, SaveData s, CraftStation station)
        {
            var d = Def(c, station);
            return d == null ? null : CaravanUpgrades.NextLevel(c, s, d.Id);
        }

        /// <summary>Buys the next facility level (the same purchase as its card on the Caravan screen).</summary>
        public static Result Build(ContentDb c, SaveData s, CraftStation station)
        {
            var d = Def(c, station);
            return d == null ? Result.Fail("No facility to build") : CaravanUpgrades.Buy(c, s, d.Id);
        }

        /// <summary>The station header's facility part: "Iron Forge — next: Steel Forge, 1,500 G" or "Mythril Forge".</summary>
        public static string HeaderText(ContentDb c, SaveData s, CraftStation station)
        {
            if (Def(c, station) == null) return "";
            var next = Next(c, s, station);
            string now = Current(c, s, station);
            return next == null ? now : $"{now} — next: {next.Name}, {next.Cost:N0} G";
        }

        /// <summary>Save v10 (GG-T432): a save from before facilities keeps roughly what its station skill had reached —
        /// a Journeyman station gets the first facility level, a Master the second (the Alchemist's whole ladder) —
        /// so a returning player's usual recipes don't vanish behind a paywall. Never lowers a level already bought.</summary>
        public static void Grandfather(SaveData s)
        {
            if (s == null) return;
            void Grant(string id, StationKind k)
            {
                int lv = StationProgress.Level(s, k) - 1;
                if (lv > CaravanUpgrades.Level(s, id)) s.Caravan[id] = lv;
            }
            Grant(CaravanUpgradeDef.ForgeFacilityId, StationKind.Blacksmith);
            Grant(CaravanUpgradeDef.AlchemyFacilityId, StationKind.Alchemist);
        }
    }
}
