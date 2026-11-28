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
    // Blacksmith & Alchemist crafting (§24.6, §25.2–25.3)
    // ─────────────────────────────────────────────────────────────────────────────

    public static class Crafting
    {
        public const int CraftXp = 15;
        public const int SalvageXp = 8;

        // ── Masterwork variants (§24.6.2, GG-T46) ────────────────────────────────
        /// <summary>Chance a Forge/Upgrade craft rolls Masterwork, before party Luck and station level…</summary>
        public const float MasterworkBaseChance = 0.05f;
        /// <summary>…plus this per point of the active party's combined Luck…</summary>
        public const float MasterworkChancePerLuck = 0.002f;
        /// <summary>…plus this per Blacksmith station level above Apprentice…</summary>
        public const float MasterworkChancePerStationLevel = 0.05f;
        /// <summary>…capped here.</summary>
        public const float MasterworkMaxChance = 0.35f;
        public const float MasterworkStatBonusMin = 0.10f;
        public const float MasterworkStatBonusMax = 0.20f;

        // ── Lucky craft (§24.6.5, GG-T235): a Diamond spent on a Forge craft ─────────────────────
        public const string DiamondId = "mat_diamond";
        /// <summary>Diamonds a lucky craft costs: 50 = $0.50 = 300 g at the exchange (GG-T455), paid through the exchange
        /// (Diamonds first, any shortfall in gold on top of the recipe's own gold).</summary>
        public const int LuckyDiamondCost = 50;
        /// <summary>A lucky craft adds this to the Masterwork chance…</summary>
        public const float LuckyChanceBonus = 0.40f;
        /// <summary>…and lifts the cap to this…</summary>
        public const float LuckyMaxChance = 0.75f;
        /// <summary>…and a lucky Masterwork's stat bonus never rolls below this.</summary>
        public const float LuckyStatBonusMin = 0.15f;

        private static int masterworkSeed = Environment.TickCount;

        public static StationKind StationFor(CraftStation station) => station == CraftStation.Forge ? StationKind.Blacksmith : StationKind.Alchemist;

        public static List<RecipeDef> Recipes(ContentDb c, CraftStation station) =>
            c.Recipes.Values.Where(r => r.Station == station && c.TryItem(r.Result) != null)
                .OrderBy(r => r.BaseItem != null).ThenBy(r => r.Gold).ThenBy(r => c.Item(r.Result).Name).ToList();

        public static bool HasInputs(SaveData s, RecipeDef r) => HasInputs(s, r, r.Inputs);

        private static bool HasInputs(SaveData s, RecipeDef r, List<ItemCount> inputs)
        {
            foreach (var ic in inputs) if (s.Count(ic.Item) < ic.Count) return false;
            if (r.BaseItem != null && s.Count(r.BaseItem) < 1) return false;
            return true;
        }

        public static bool IsUnlocked(SaveData s, RecipeDef r) => StationProgress.IsUnlocked(s, StationFor(r.Station), r.Tier);

        /// <summary>The recipe is open to craft: the station's skill tier (§24.6.1) and its facility's material tier
        /// (§25.12.2, GG-T432) both reach it. Inputs and gold are separate checks.</summary>
        public static bool CanMake(ContentDb c, SaveData s, RecipeDef r) => IsUnlocked(s, r) && Facilities.Allows(c, s, r);

        private static float PartyLuck(ContentDb c, SaveData s)
        {
            float luck = 0f;
            foreach (var id in s.Party)
            {
                var h = s.Hero(id);
                var def = h != null ? c.TryClass(h.ClassId) : null;
                if (def != null) luck += Formulas.StatsAtLevel(def, Progression.Level(h)).Luck;
            }
            return luck;
        }

        /// <summary>Chance this save's next Forge/Upgrade craft rolls a Masterwork result (party Luck + Blacksmith level).</summary>
        public static float MasterworkChance(ContentDb c, SaveData s, bool lucky = false)
        {
            int stationLevel = StationProgress.Level(s, StationKind.Blacksmith);
            float chance = MasterworkBaseChance + PartyLuck(c, s) * MasterworkChancePerLuck + (stationLevel - 1) * MasterworkChancePerStationLevel;
            if (lucky) return Math.Max(0f, Math.Min(LuckyMaxChance, chance + LuckyChanceBonus));
            return Math.Max(0f, Math.Min(MasterworkMaxChance, chance));
        }

        /// <summary>The lucky craft's odds, disclosed before purchase (GG-T503): this save's Masterwork chance without the
        /// Diamond and with it — <see cref="MasterworkChance"/>, the very function <see cref="Craft"/> rolls against.</summary>
        public static (float plain, float lucky) MasterworkOdds(ContentDb c, SaveData s) =>
            (MasterworkChance(c, s), MasterworkChance(c, s, lucky: true));

        /// <summary>"Masterwork chance: 15% without Shards, 55% with them" for the forge's lucky-craft window (GG-T568).</summary>
        public static string LuckyOddsLine(ContentDb c, SaveData s)
        {
            var (plain, lucky) = MasterworkOdds(c, s);
            return $"Masterwork chance: {OddsText.Percent(plain)} without {ShardText.ShortPlural}, {OddsText.Percent(lucky)} with them";
        }

        /// <summary>A lucky craft fits this recipe: a Forge recipe whose result can roll Masterwork.</summary>
        public static bool LuckyEligible(ContentDb c, RecipeDef r) =>
            r != null && r.Station == CraftStation.Forge && r.ResultCount == 1 && c.TryItem(r.Result) is ItemDef it && MasterworkEligible(it);

        /// <summary>A lucky craft is on offer: an eligible recipe, and <see cref="LuckyDiamondCost"/> Diamonds payable (in
        /// Diamonds, or gold through the exchange beyond the recipe's own gold), where paid random items are sold
        /// (<see cref="LootBoxPolicy.AllowsLuckyCraft"/>, GG-T1433).</summary>
        public static bool CanLucky(ContentDb c, SaveData s, RecipeDef r) =>
            LuckyEligible(c, r) && LootBoxPolicy.AllowsLuckyCraft().Ok && DiamondExchange.CanPay(s, LuckyDiamondCost, r.Gold).Ok;

        private static bool MasterworkEligible(ItemDef item) =>
            item.Type == ItemType.Equipment && item.Durability > 0 && (item.Slot == EquipmentSlot.Weapon || Array.IndexOf(BattleEngine.ArmorSlots, item.Slot) >= 0);

        /// <summary>Whether this recipe has a reagent-quality variant (GG-T45) and the player holds its
        /// higher-rarity inputs — e.g. a Pure Slime Gel in place of a plain one brews a Greater Healing Potion.</summary>
        public static bool HasPotencyInputs(SaveData s, RecipeDef r) => r.PotencyResult != null && HasInputs(s, r, r.PotencyInputs);

        public static Result Craft(ContentDb c, SaveData s, RecipeDef r, bool potency = false, Rng rng = null, bool lucky = false)
        {
            if (!IsUnlocked(s, r)) return Result.Fail($"Requires {StationProgress.TierName(r.Tier)}");
            if (!Facilities.Allows(c, s, r)) return Result.Fail($"Needs {Facilities.NeededName(c, r)}");
            if (!Forging.ArmorOpen(c, s, r)) return Result.Fail(Forging.ArmorLockedHint);
            if (potency && r.PotencyResult == null) return Result.Fail("No greater version");
            if (lucky && (potency || !LuckyEligible(c, r))) return Result.Fail("No lucky craft for this recipe");
            if (lucky && LootBoxPolicy.AllowsLuckyCraft() is var luckyAllowed && !luckyAllowed.Ok) return luckyAllowed;
            if (lucky && DiamondExchange.CanPay(s, LuckyDiamondCost, r.Gold) is var luckyPay && !luckyPay.Ok) return luckyPay;
            var inputs = potency ? r.PotencyInputs : r.Inputs;
            var resultId = potency ? r.PotencyResult : r.Result;
            var resultCount = potency ? r.PotencyResultCount : r.ResultCount;
            if (s.Gold < r.Gold) return Result.Fail("Not enough gold");
            if (!HasInputs(s, r, inputs)) return Result.Fail("Missing materials");
            var result = c.Item(resultId);
            if (s.Count(result.Id) + resultCount > result.MaxStack) return Result.Fail("Can't carry more");
            // The chance is fixed before anything changes (GG-T503): the craft's own station XP must not move the odds the
            // player was shown.
            float masterworkChance = MasterworkChance(c, s, lucky);
            if (lucky && !DiamondExchange.Pay(s, LuckyDiamondCost, r.Gold).Ok) return Result.Fail($"Not enough {ShardText.Plural} for a lucky craft");
            s.Gold -= r.Gold;
            foreach (var ic in inputs) s.AddItem(ic.Item, -ic.Count);
            if (r.BaseItem != null) s.AddItem(r.BaseItem, -1);
            s.AddItem(result.Id, resultCount);
            s.CraftedRecipes?.Add(r.Id); // GG-T538: a recipe once made stays in its station's list
            StationProgress.Gain(s, StationFor(r.Station), CraftXp);
            bool masterwork = false;
            if (!potency && r.Station == CraftStation.Forge && resultCount == 1 && MasterworkEligible(result))
            {
                rng ??= new Rng(masterworkSeed++);
                if (rng.Chance(masterworkChance))
                {
                    masterwork = true;
                    float mastery = rng.Range(1f + (lucky ? LuckyStatBonusMin : MasterworkStatBonusMin), 1f + MasterworkStatBonusMax);
                    int durability = (int)Math.Round(result.Durability * BattleConfig.MasterworkDurabilityMultiplier);
                    if (!s.Worn.TryGetValue(result.Id, out var list)) s.Worn[result.Id] = list = new List<WornGear>();
                    list.Add(new WornGear { Durability = durability, Repairs = 0, Mastery = mastery });
                }
            }
            if (lucky && !masterwork) return Result.Success($"Made {result.Name} — the {ShardText.ShortPlural}' luck didn't hold");
            return Result.Success(masterwork ? $"★ Masterwork {result.Name}!" : $"Made {result.Name}");
        }

        /// <summary>Junk..Legendary materials for any armor piece (Head/Torso/Legs/Gloves/Boots) without its own
        /// Forge recipe (GG-T89, extends GG-T42's fallback table to five slots instead of one).</summary>
        private static readonly (string Item, int Count)[][] ArmorFallbackTiers =
        {
            new[] { ("mat_tattered_cloth", 1) },
            new[] { ("mat_leather", 2), ("mat_cloth", 1) },
            new[] { ("mat_leather", 3), ("mat_arcane_dust", 1) },
            new[] { ("mat_troll_hide", 2), ("mat_leather", 2) },
            new[] { ("mat_demon_shard", 2), ("mat_troll_hide", 2) },
            new[] { ("mat_dragon_scale", 1), ("mat_demon_shard", 1) },
        };

        /// <summary>Junk..Legendary materials for any jewelry piece (Necklace/Earring/Ring) without its own recipe.</summary>
        private static readonly (string Item, int Count)[][] JewelryFallbackTiers =
        {
            new[] { ("mat_cracked_fang", 1) },
            new[] { ("mat_iron_ore", 1), ("mat_undead_bone", 1) },
            new[] { ("mat_arcane_dust", 2) },
            new[] { ("mat_naga_scale", 1), ("mat_arcane_dust", 1) },
            new[] { ("mat_ghost_ectoplasm", 2) },
            new[] { ("mat_dragon_scale", 1) },
        };

        /// <summary>Pieces without a base Forge recipe (upgrades, enchants, jewelry) fall back to a flat
        /// materials-by-type-and-rarity table, so every equipment piece is salvageable (GG-T42).</summary>
        private static readonly Dictionary<(EquipmentSlot Slot, ItemRarity Rarity), (string Item, int Count)[]> FallbackSalvage = BuildFallbackSalvage();

        private static Dictionary<(EquipmentSlot, ItemRarity), (string, int)[]> BuildFallbackSalvage()
        {
            var map = new Dictionary<(EquipmentSlot, ItemRarity), (string, int)[]>
            {
                [(EquipmentSlot.Weapon, ItemRarity.Junk)] = new[] { ("mat_broken_blade", 1) },
                [(EquipmentSlot.Weapon, ItemRarity.Common)] = new[] { ("mat_iron_ore", 2), ("mat_wood_plank", 1) },
                [(EquipmentSlot.Weapon, ItemRarity.Uncommon)] = new[] { ("mat_iron_ore", 3), ("mat_arcane_dust", 1) },
                [(EquipmentSlot.Weapon, ItemRarity.Rare)] = new[] { ("mat_iron_ore", 4), ("mat_arcane_dust", 2) },
                [(EquipmentSlot.Weapon, ItemRarity.Epic)] = new[] { ("mat_demon_shard", 2), ("mat_arcane_dust", 2) },
                [(EquipmentSlot.Weapon, ItemRarity.Legendary)] = new[] { ("mat_dragon_scale", 1), ("mat_demon_shard", 2) },
            };
            var rarities = (ItemRarity[])Enum.GetValues(typeof(ItemRarity)); // declared order: Junk..Legendary
            void Fill(EquipmentSlot slot, (string, int)[][] tiers)
            {
                for (int i = 0; i < rarities.Length && i < tiers.Length; i++) map[(slot, rarities[i])] = tiers[i];
            }
            foreach (var slot in BattleEngine.ArmorSlots) Fill(slot, ArmorFallbackTiers);
            // Only the canonical instance of each multi-slot jewelry category needs an entry (see the
            // bucket redirect in FullSalvageYield below) — Necklace has just the one slot either way.
            foreach (var slot in new[] { EquipmentSlot.Necklace, EquipmentSlot.Earring1, EquipmentSlot.Ring1 }) Fill(slot, JewelryFallbackTiers);
            return map;
        }

        private static List<ItemCount> FullSalvageYield(ContentDb c, ItemDef item)
        {
            var recipe = c.Recipes.Values.FirstOrDefault(r => r.Result == item.Id && r.Station == CraftStation.Forge && r.BaseItem == null);
            if (recipe != null)
                return recipe.Inputs.Select(ic => new ItemCount { Item = ic.Item, Count = Math.Max(1, ic.Count / 2) }).ToList();
            var bucket = item.Slot == EquipmentSlot.Earring2 ? EquipmentSlot.Earring1 : item.Slot == EquipmentSlot.Ring2 ? EquipmentSlot.Ring1 : item.Slot;
            if (!FallbackSalvage.TryGetValue((bucket, item.Rarity), out var fallback)) return new List<ItemCount>();
            return fallback.Select(f => new ItemCount { Item = f.Item, Count = f.Count }).ToList();
        }

        /// <summary>Salvage yield for a piece at <paramref name="durability"/> out of its max: a near-dead or
        /// Broken item returns proportionally less (floor), but never less than 1 of the cheapest input (GG-T42).</summary>
        public static List<ItemCount> SalvageYield(ContentDb c, ItemDef item, int durability)
        {
            var full = FullSalvageYield(c, item);
            if (full.Count == 0 || item.Durability <= 0) return full;
            float frac = Math.Max(0f, Math.Min(1f, (float)durability / item.Durability));
            var scaled = full.Select(ic => new ItemCount { Item = ic.Item, Count = (int)Math.Floor(ic.Count * frac) })
                .Where(ic => ic.Count > 0).ToList();
            if (scaled.Count == 0)
            {
                var cheapest = full.OrderBy(ic => c.TryItem(ic.Item)?.BaseCost ?? int.MaxValue).First();
                scaled.Add(new ItemCount { Item = cheapest.Item, Count = 1 });
            }
            return scaled;
        }

        /// <summary>Preview of what salvaging the bag's worst (most-worn) copy of this item would yield, without consuming it.</summary>
        public static List<ItemCount> SalvagePreview(ContentDb c, SaveData s, string itemId)
        {
            var item = c.TryItem(itemId);
            return item == null ? new List<ItemCount>() : SalvageYield(c, item, PeekWorstDurability(s, item));
        }

        private static int PeekWorstDurability(SaveData s, ItemDef item)
        {
            if (item.Durability <= 0) return item.Durability;
            return s.Worn.TryGetValue(item.Id, out var worn) && worn.Count > 0 ? worn.Min(w => w.Durability) : item.Durability;
        }

        /// <summary>Removes and returns the durability of the bag's single worst tracked copy, or the full
        /// durability if every copy is factory-fresh (so salvaging always eats the junkiest piece first).</summary>
        private static int TakeWorstDurability(SaveData s, ItemDef item)
        {
            if (item.Durability <= 0) return item.Durability;
            if (!s.Worn.TryGetValue(item.Id, out var worn) || worn.Count == 0) return item.Durability;
            int idx = 0;
            for (int i = 1; i < worn.Count; i++) if (worn[i].Durability < worn[idx].Durability) idx = i;
            int dur = worn[idx].Durability;
            worn.RemoveAt(idx);
            if (worn.Count == 0) s.Worn.Remove(item.Id);
            return dur;
        }

        /// <summary>Salvages one bag copy of <paramref name="itemId"/> — the worst (most-worn) tracked copy first.</summary>
        public static Result Salvage(ContentDb c, SaveData s, string itemId)
        {
            var item = c.TryItem(itemId);
            if (item == null || s.Count(itemId) < 1) return Result.Fail("Nothing to salvage");
            // Peek first: a failed salvage must not consume the copy's tracked wear (it would silently turn fresh).
            var yield = CaravanUpgrades.LedgerYield(c, s, SalvageYield(c, item, PeekWorstDurability(s, item)));
            if (yield.Count == 0) return Result.Fail("Can't be salvaged");
            TakeWorstDurability(s, item);
            s.AddItem(itemId, -1);
            foreach (var ic in yield) s.AddItem(ic.Item, ic.Count);
            StationProgress.Gain(s, StationKind.Blacksmith, SalvageXp);
            return Result.Success($"Salvaged {item.Name}");
        }

        /// <summary>Salvages a hero's currently equipped weapon/armor directly (the "Salvage instead" offer on
        /// an uneconomical repair, §25.2) — the piece is destroyed, not returned to the bag (GG-T42).</summary>
        public static Result SalvageEquipped(ContentDb c, SaveData s, HeroSave h, EquipmentSlot slot)
        {
            var itemId = Equipment.Equipped(h, slot);
            var item = itemId != null ? c.TryItem(itemId) : null;
            if (item == null) return Result.Fail("Nothing equipped");
            int durability = slot == EquipmentSlot.Weapon ? h.WeaponDurability : h.Armor.TryGetValue(slot, out var ap) ? ap.Durability : 0;
            var yield = CaravanUpgrades.LedgerYield(c, s, SalvageYield(c, item, durability));
            if (yield.Count == 0) return Result.Fail("Can't be salvaged");
            switch (slot)
            {
                case EquipmentSlot.Weapon: h.WeaponId = null; h.WeaponDurability = 0; h.WeaponRepairs = 0; h.WeaponMastery = 1f; break;
                case var armorSlot when Array.IndexOf(BattleEngine.ArmorSlots, armorSlot) >= 0: h.Armor.Remove(armorSlot); break;
                default: return Result.Fail("Can't be salvaged");
            }
            foreach (var ic in yield) s.AddItem(ic.Item, ic.Count);
            StationProgress.Gain(s, StationKind.Blacksmith, SalvageXp);
            return Result.Success($"Salvaged {item.Name}");
        }

        /// <summary>Preview of what salvaging one Broken fragment of <paramref name="itemId"/> would yield
        /// (durability 0 — the floor: 1 of the cheapest input) (GG-T41).</summary>
        public static List<ItemCount> SalvageBrokenPreview(ContentDb c, string itemId)
        {
            var item = c.TryItem(itemId);
            return item == null ? new List<ItemCount>() : SalvageYield(c, item, 0);
        }

        /// <summary><see cref="SalvageBrokenPreview(ContentDb, string)"/> with this save's Quartermaster's Ledger bonus (GG-T75).</summary>
        public static List<ItemCount> SalvageBrokenPreview(ContentDb c, SaveData s, string itemId) =>
            CaravanUpgrades.LedgerYield(c, s, SalvageBrokenPreview(c, itemId));

        /// <summary>Scraps one Broken fragment of <paramref name="itemId"/> for materials — Broken pieces
        /// can't be equipped or repaired, so salvage is their only use (GG-T41).</summary>
        public static Result SalvageBroken(ContentDb c, SaveData s, string itemId)
        {
            var item = c.TryItem(itemId);
            if (item == null || s.BrokenCount(itemId) < 1) return Result.Fail("Nothing to salvage");
            var yield = CaravanUpgrades.LedgerYield(c, s, SalvageYield(c, item, 0));
            if (yield.Count == 0) return Result.Fail("Can't be salvaged");
            s.AddBroken(itemId, -1);
            foreach (var ic in yield) s.AddItem(ic.Item, ic.Count);
            StationProgress.Gain(s, StationKind.Blacksmith, SalvageXp);
            return Result.Success($"Salvaged Broken {item.Name}");
        }
    }
}
