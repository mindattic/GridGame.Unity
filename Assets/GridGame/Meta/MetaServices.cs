using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Core.Rules;

namespace GridGame.Meta
{
    public readonly struct Result
    {
        public readonly bool Ok;
        public readonly string Message;
        private Result(bool ok, string message) { Ok = ok; Message = message; }
        public static Result Success(string message = null) => new Result(true, message);
        public static Result Fail(string message) => new Result(false, message);
        public override string ToString() => (Ok ? "OK " : "FAIL ") + Message;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Using items at camp (potions on a hero, Tent on everyone)
    // ─────────────────────────────────────────────────────────────────────────────

    public static class Camp
    {
        public static bool UsableAtCamp(ItemDef i) => i != null && i.Type == ItemType.Consumable && i.BaseHealing > 0 && !i.Revives;

        public static bool HealsWholeParty(ItemDef i) => i != null && i.OutOfBattleOnly;

        public static Result Use(ContentDb c, SaveData s, string itemId, string heroId)
        {
            var item = c.TryItem(itemId);
            if (!UsableAtCamp(item)) return Result.Fail("Can't use that here");
            if (s.Count(itemId) < 1) return Result.Fail("None left");
            if (HealsWholeParty(item))
            {
                if (s.Roster.All(h => Inn.MissingHp(c, s, h) == 0)) return Result.Fail("Everyone is healthy");
                foreach (var h in s.Roster) h.Hp = 0;
                s.AddItem(itemId, -1);
                return Result.Success("The party is fully rested");
            }
            var hero = s.Hero(heroId);
            if (hero == null) return Result.Fail("Pick a hero");
            string heroName = c.TryClass(hero.ClassId)?.Name ?? hero.ClassId;
            int max = Progression.Preview(c, hero).MaxHp;
            if (hero.Hp <= 0 || hero.Hp >= max) return Result.Fail($"{heroName} is already healthy");
            int healed = Math.Min(max - hero.Hp, item.BaseHealing);
            hero.Hp += healed;
            if (hero.Hp >= max) hero.Hp = 0;
            s.AddItem(itemId, -1);
            return Result.Success($"{heroName} recovers {healed} HP");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Merchant (§25.1)
    // ─────────────────────────────────────────────────────────────────────────────

    public static class Shop
    {
        /// <summary>Everything the Merchant ever sells, every stock tier (the catalogue the content checks read).</summary>
        public static List<ItemDef> Stock(ContentDb c) =>
            c.Items.Values.Where(i => i.SoldByVendor && i.BaseCost > 0).OrderBy(i => i.Type).ThenBy(i => i.BaseCost).ThenBy(i => i.Name).ToList();

        /// <summary>GG-T538: this save's shelf, the stock tiers its progress has opened plus anything it bought before
        /// (<see cref="VendorTiers.OnShelf"/>).</summary>
        public static List<ItemDef> Stock(ContentDb c, SaveData s) => Stock(c).Where(i => VendorTiers.OnShelf(c, s, i)).ToList();

        /// <summary>GG-T538: the next stock tier this save hasn't opened, and how many items it brings (0 when every tier is open).</summary>
        public static (int tier, int count) NextStock(ContentDb c, SaveData s)
        {
            int now = VendorTiers.ProgressTier(c, s);
            for (int t = now + 1; t <= VendorTiers.Count; t++)
            {
                int n = Stock(c).Count(i => VendorTiers.ShopTier(i) == t && !VendorTiers.OnShelf(c, s, i));
                if (n > 0) return (t, n);
            }
            return (0, 0);
        }

        /// <summary>What the Merchant pays for one: <see cref="ItemDef.EffectiveSellValue"/> (half the base cost unless the item
        /// sets its own; an explicit SellValue of 0, e.g. Diamonds, means it can't be sold).</summary>
        public static int SellPrice(ItemDef i) => i == null ? 0 : Math.Max(0, i.EffectiveSellValue);

        /// <summary>The Merchant takes it: it has a price, isn't a quest item, and isn't marked unsellable (SellValue 0).</summary>
        public static bool CanSell(ItemDef i) => i != null && i.BaseCost > 0 && i.Type != ItemType.QuestItem && SellPrice(i) > 0;

        public static List<ItemDef> Sellable(ContentDb c, SaveData s) =>
            s.Inventory.Where(kv => kv.Value > 0).Select(kv => c.TryItem(kv.Key)).Where(CanSell)
                .OrderBy(i => i.Type).ThenBy(i => i.Name).ToList();

        public static int MaxBuy(SaveData s, ItemDef i) => Math.Max(0, i.MaxStack - s.Count(i.Id));

        public static int CartTotal(ContentDb c, Dictionary<string, int> cart, bool selling) =>
            cart.Sum(kv => { var i = c.TryItem(kv.Key); return i == null ? 0 : kv.Value * (selling ? SellPrice(i) : i.BaseCost); });

        public static Result Buy(ContentDb c, SaveData s, Dictionary<string, int> cart)
        {
            int total = CartTotal(c, cart, false);
            if (total <= 0) return Result.Fail("Nothing to buy");
            if (total > s.Gold) return Result.Fail("Not enough gold");
            foreach (var kv in cart)
            {
                var i = c.TryItem(kv.Key);
                if (i != null && kv.Value > 0 && !VendorTiers.OnShelf(c, s, i))
                    return Result.Fail($"{i.Name} isn't in stock yet ({VendorTiers.OpensHint(c, VendorTiers.ShopTier(i))})");
                if (i == null || kv.Value < 0 || kv.Value > MaxBuy(s, i)) return Result.Fail($"Can't carry more {i?.Name}");
            }
            s.Gold -= total;
            foreach (var kv in cart) { s.AddItem(kv.Key, kv.Value); if (kv.Value > 0) s.ShopBought?.Add(kv.Key); }
            return Result.Success($"Bought for {total}g");
        }

        public static Result Sell(ContentDb c, SaveData s, Dictionary<string, int> cart)
        {
            foreach (var kv in cart)
            {
                if (kv.Value < 0 || kv.Value > s.Count(kv.Key)) return Result.Fail("You don't have that many");
                if (kv.Value > 0 && !CanSell(c.TryItem(kv.Key))) return Result.Fail($"The Merchant won't buy {c.TryItem(kv.Key)?.Name ?? kv.Key}");
            }
            int total = CartTotal(c, cart, true);
            if (total <= 0) return Result.Fail("Nothing to sell");
            foreach (var kv in cart) s.AddItem(kv.Key, -kv.Value);
            s.Gold += total;
            return Result.Success($"Sold for {total}g");
        }

        // ── GG-T520: the Buy list in sections, each gear row naming who it fits and whom it upgrades ──

        /// <summary>The Buy list's sections, in the order they are listed (§25.1).</summary>
        public static readonly string[] Sections = { "Weapons", "Armor", "Jewelry", "Consumables", "Materials", "Other" };

        /// <summary>The index into <see cref="Sections"/> an item is listed under.</summary>
        public static int SectionOf(ItemDef i)
        {
            if (i == null) return Sections.Length - 1;
            switch (i.Type)
            {
                case ItemType.Equipment:
                    return i.Slot == EquipmentSlot.Weapon ? 0 : Array.IndexOf(BattleEngine.ArmorSlots, i.Slot) >= 0 ? 1 : 2;
                case ItemType.Consumable: return 3;
                case ItemType.CraftingMaterial: return 4;
                default: return 5;
            }
        }

        /// <summary>The slots a piece can go in: both of a ring's or an earring's pair, else its own slot.</summary>
        private static EquipmentSlot[] SlotsFor(ItemDef i) =>
            i.IsRing ? new[] { EquipmentSlot.Ring1, EquipmentSlot.Ring2 } :
            i.IsEarring ? new[] { EquipmentSlot.Earring1, EquipmentSlot.Earring2 } : new[] { i.Slot };

        /// <summary>Every party hero who can equip <paramref name="i"/> today (shape and proficiency, <see cref="Equipment.Fits(ContentDb, HeroSave, ItemDef, EquipmentSlot)"/>),
        /// in party order, and whether it is an upgrade for them: its gear score (<c>MetaHeuristics.Score</c>, the camp
        /// routine's and the Forge's measure) beats what they have in that slot, or in the weaker of a ring or earring pair
        /// (an empty slot scores 0). Empty for anything that isn't equipment.</summary>
        public static List<(HeroSave hero, bool upgrade)> GearFit(ContentDb c, SaveData s, ItemDef i)
        {
            var fits = new List<(HeroSave, bool)>();
            if (c == null || s == null || i == null || i.Type != ItemType.Equipment) return fits;
            float score = GridGame.Core.Bot.MetaHeuristics.Score(i);
            var slots = SlotsFor(i);
            foreach (var h in s.Party.Select(s.Hero))
            {
                if (h == null || !Equipment.Fits(c, h, i, slots[0])) continue;
                float worst = slots.Min(slot => GridGame.Core.Bot.MetaHeuristics.Score(c.TryItem(Equipment.Equipped(h, slot))));
                fits.Add((h, score > worst));
            }
            return fits;
        }

        /// <summary>The note under a gear row on the Buy list ("Upgrade for Warden · also fits Ranger"), or null for
        /// anything that isn't equipment; <paramref name="upgrade"/> is true when it beats some hero's current piece.</summary>
        public static string GearFitLine(ContentDb c, SaveData s, ItemDef i, out bool upgrade)
        {
            upgrade = false;
            if (i == null || i.Type != ItemType.Equipment) return null;
            var fits = GearFit(c, s, i);
            string Names(IEnumerable<(HeroSave hero, bool upgrade)> xs) => string.Join(", ", xs.Select(x => c.TryClass(x.hero.ClassId)?.Name ?? x.hero.ClassId));
            if (fits.Count == 0) return "No one in the party can use it yet";
            var ups = fits.Where(x => x.upgrade).ToList();
            var rest = fits.Where(x => !x.upgrade).ToList();
            upgrade = ups.Count > 0;
            if (upgrade) return $"Upgrade for {Names(ups)}" + (rest.Count > 0 ? $" · also fits {Names(rest)}" : "");
            bool everyone = fits.Count == s.Party.Select(s.Hero).Count(h => h != null);
            return (everyone && fits.Count > 1 ? "Fits everyone" : $"Fits {Names(fits)}") + ", not an upgrade";
        }
    }

    /// <summary>One rolled offer on the Merchant's "Used / Bargain" shelf (§25.1, US-246).</summary>
    public sealed class BargainOffer
    {
        public ItemDef Item;
        /// <summary>Discounted price (35-60% of <see cref="ItemDef.BaseCost"/>).</summary>
        public int Price;
        /// <summary>Starting durability the piece carries if bought (10-40% of <see cref="ItemDef.Durability"/>).</summary>
        public int Durability;
    }

    /// <summary>
    /// The Merchant's rotating "Used / Bargain" shelf (bible §25.1, US-246): cheap weapons and armor
    /// sold well below new price, but already worn down — a deliberate early-game gamble between
    /// cheap power now and a real chance of shattering mid-combat before it can be repaired.
    /// </summary>
    public static class Bargains
    {
        public const int ShelfSize = 4;
        public const float MinPriceFraction = 0.35f, MaxPriceFraction = 0.60f;
        public const float MinDurabilityFraction = 0.10f, MaxDurabilityFraction = 0.40f;

        private static bool Eligible(ItemDef i) => i.Type == ItemType.Equipment && i.Durability > 0 &&
            (i.Slot == EquipmentSlot.Weapon || Array.IndexOf(BattleEngine.ArmorSlots, i.Slot) >= 0);

        /// <summary>
        /// This rotation's stock. Deterministic from <see cref="SaveData.BargainRotation"/> alone, so it
        /// stays put across repeated calls (reopening the Merchant doesn't reroll it) and replays
        /// identically from the same save, but turns over on <see cref="Rotate"/>.
        /// </summary>
        public static List<BargainOffer> Shelf(ContentDb c, SaveData s)
        {
            var pool = c.Items.Values.Where(Eligible).OrderBy(i => i.Id, StringComparer.Ordinal).ToList();
            var offers = new List<BargainOffer>();
            if (pool.Count == 0) return offers;
            var rng = new Random(unchecked((int)((uint)s.BargainRotation * 747796405u + 2891336453u)));
            var remaining = Enumerable.Range(0, pool.Count).ToList();
            int take = Math.Min(ShelfSize, pool.Count);
            for (int i = 0; i < take; i++)
            {
                int pick = rng.Next(remaining.Count);
                var item = pool[remaining[pick]];
                remaining.RemoveAt(pick);
                float priceFrac = MinPriceFraction + (float)rng.NextDouble() * (MaxPriceFraction - MinPriceFraction);
                float durFrac = MinDurabilityFraction + (float)rng.NextDouble() * (MaxDurabilityFraction - MinDurabilityFraction);
                offers.Add(new BargainOffer
                {
                    Item = item,
                    Price = Math.Max(1, (int)Math.Round(item.BaseCost * priceFrac)),
                    Durability = Math.Max(1, (int)Math.Round(item.Durability * durFrac)),
                });
            }
            return offers;
        }

        public static Result Buy(ContentDb c, SaveData s, string itemId)
        {
            var offer = Shelf(c, s).FirstOrDefault(o => o.Item.Id == itemId);
            if (offer == null) return Result.Fail("Not on the shelf");
            if (s.Count(itemId) >= offer.Item.MaxStack) return Result.Fail("Can't carry more");
            if (s.Gold < offer.Price) return Result.Fail("Not enough gold");
            s.Gold -= offer.Price;
            s.AddItem(itemId, 1);
            // Marks the copy as worn on arrival so equipping it pulls this rolled durability, not factory-fresh.
            if (!s.Worn.TryGetValue(itemId, out var list)) s.Worn[itemId] = list = new List<WornGear>();
            list.Add(new WornGear { Durability = offer.Durability, Repairs = 0 });
            return Result.Success($"Bought worn {offer.Item.Name} for {offer.Price}g");
        }

        /// <summary>Turns the shelf over. Called once per finished battle, win or lose (<see cref="BattleFactory.Commit"/>).</summary>
        public static void Rotate(SaveData s) => s.BargainRotation++;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Station progression (§24.6.1, GG-T35): craft/brew/repair/salvage levels up the
    // station that did the work, unlocking the next tier of its own recipe ladder.
    // Recipe tiers only: the gold-bought facility levels of §25.12.2 (GG-T432) are a second, separate gate on
    // material tier (result rarity), checked by Crafting.CanMake; caravan stat upgrades never move station XP.
    // ─────────────────────────────────────────────────────────────────────────────

    public enum StationKind { Blacksmith, Alchemist }

    public static class StationProgress
    {
        public static readonly string[] LevelNames = { "Apprentice", "Journeyman", "Master" };
        /// <summary>Total XP needed to *reach* each level (index 0 = Apprentice, free). Tuned in <see cref="VendorTiers.RankXp"/> (GG-T538).</summary>
        private static int[] Thresholds => VendorTiers.RankXp;

        public static string Id(StationKind k) => k == StationKind.Blacksmith ? "blacksmith" : "alchemist";

        public static int Xp(SaveData s, StationKind k) => s.StationXp.TryGetValue(Id(k), out var n) ? n : 0;

        public static int Level(SaveData s, StationKind k)
        {
            int xp = Xp(s, k);
            int lvl = 1;
            for (int i = 1; i < Thresholds.Length; i++) if (xp >= Thresholds[i]) lvl = i + 1;
            return lvl;
        }

        public static string LevelName(SaveData s, StationKind k) => LevelNames[Level(s, k) - 1];
        public static string TierName(int tier) => LevelNames[Math.Max(1, Math.Min(tier, LevelNames.Length)) - 1];

        public static void Gain(SaveData s, StationKind k, int amount)
        {
            if (amount <= 0) return;
            s.StationXp[Id(k)] = Xp(s, k) + amount;
        }

        public static bool IsUnlocked(SaveData s, StationKind k, int tier) => Level(s, k) >= tier;

        /// <summary>The header's compact form: "Apprentice 40/150 XP", or just the rank once maxed.</summary>
        public static string ShortText(SaveData s, StationKind k)
        {
            int lvl = Level(s, k);
            return lvl >= LevelNames.Length ? LevelNames[lvl - 1] : $"{LevelNames[lvl - 1]} {Xp(s, k)}/{Thresholds[lvl]} XP";
        }

        /// <summary>"Journeyman — 40/450 XP to Master", or "Master — every tier unlocked" when maxed.</summary>
        public static string HeaderText(SaveData s, StationKind k)
        {
            int lvl = Level(s, k);
            if (lvl >= LevelNames.Length) return $"{LevelNames[lvl - 1]} — every recipe tier unlocked";
            return $"{LevelNames[lvl - 1]} — {Xp(s, k)}/{Thresholds[lvl]} XP to {LevelNames[lvl]}";
        }
    }

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

    // ─────────────────────────────────────────────────────────────────────────────
    // Recipe discovery (§24.6.2, GG-T36): recipes start hidden; a recipe is discovered once its
    // station reaches its tier (§24.6.1), or earlier by reading a recipe scroll or first-ever
    // pickup of the recipe's rarest input. Discovery only gates visibility — Crafting.Craft's own
    // gold/inputs/tier checks are unchanged, since a discovered-but-locked recipe already fails
    // IsUnlocked, and a tier-reached recipe is discovered by definition.
    // ─────────────────────────────────────────────────────────────────────────────

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

    // ─────────────────────────────────────────────────────────────────────────────
    // The first forge (GG-T177, §25.0.6): once the Blacksmith's workshop opens, the Forge offers starter weapon
    // recipes that cost no gold and take only early drops; the guided forge equips the result on the hero it suits.
    // ─────────────────────────────────────────────────────────────────────────────

    public static class Forging
    {
        /// <summary>Save flag set by the guided first forge.</summary>
        public const string FirstForgeFlag = "onboard:first-forge";
        /// <summary>The camp-tip key of the Blacksmith's Forge tab (his Repair tab's tip is "blacksmith").</summary>
        public const string TipStation = "blacksmith-forge";

        public sealed class Plan
        {
            public RecipeDef Recipe;
            /// <summary>What it makes: the starter weapon (<see cref="First"/>) or the set piece (<see cref="FirstArmor"/>).</summary>
            public ItemDef Weapon;
            /// <summary>The party hero it suits: the one who would equip it on forging.</summary>
            public HeroSave Hero;
            /// <summary>Every input is in the bag (it costs no gold), so Craft works now.</summary>
            public bool Ready;
        }

        public static bool IsStarter(RecipeDef r) => r != null && r.Starter && r.Station == CraftStation.Forge && r.BaseItem == null;

        public static List<RecipeDef> StarterRecipes(ContentDb c) => Crafting.Recipes(c, CraftStation.Forge).Where(IsStarter).ToList();

        /// <summary>The first forge is behind this save: it has been made, or the save is a bot's or a New Game+.</summary>
        public static bool Done(SaveData s) => s == null || s.TestMode || s.NewGamePlus > 0 || s.Flags.Contains(FirstForgeFlag);

        /// <summary>How well a weapon's stats match a hero's class: its stat bonuses weighted by the class's growth,
        /// per point — so a Wisdom lantern suits the Cleric and a Strength/Vitality club the Paladin.</summary>
        public static float Affinity(ContentDb c, HeroSave h, ItemDef item)
        {
            var g = c.TryClass(h.ClassId)?.Growth;
            var st = item?.Stats;
            if (g == null || st == null) return 0f;
            float total = st.Strength + st.Vitality + st.Agility + st.Speed + st.Intelligence + st.Wisdom + st.Luck;
            if (total <= 0f) return 0f;
            return (st.Strength * g.Strength + st.Vitality * g.Vitality + st.Agility * g.Agility + st.Speed * g.Speed
                    + st.Intelligence * g.Intelligence + st.Wisdom * g.Wisdom + st.Luck * g.Luck) / total;
        }

        /// <summary>The party hero a weapon suits: one who can wield it and would be better off with it than with what
        /// they hold now, the best stat match first (then the bigger gain, then party order); null when nobody gains.</summary>
        public static HeroSave SuitedHero(ContentDb c, SaveData s, ItemDef item)
        {
            if (c == null || s == null || item == null || item.Slot != EquipmentSlot.Weapon) return null;
            float score = GridGame.Core.Bot.MetaHeuristics.Score(item);
            return s.Party.Select(s.Hero)
                .Where(h => h != null && Equipment.Fits(c, h, item, EquipmentSlot.Weapon))
                .Select(h => (h, gain: score - GridGame.Core.Bot.MetaHeuristics.Score(c.TryItem(h.WeaponId))))
                .Where(x => x.gain > 0f)
                .OrderByDescending(x => Affinity(c, x.h, item)).ThenByDescending(x => x.gain)
                .Select(x => x.h).FirstOrDefault();
        }

        /// <summary>The starter recipe the Forge's camp tip walks and the Forge preselects, or null once the first forge
        /// is done: one that suits a party hero and can be carried, ready-to-forge ones first, then the best stat match,
        /// then the fewest materials still to find.</summary>
        public static Plan First(ContentDb c, SaveData s)
        {
            if (c == null || Done(s)) return null;
            return StarterRecipes(c)
                .Where(r => Crafting.CanMake(c, s, r) && VendorTiers.RecipeOpen(c, s, r) && c.TryItem(r.Result) is ItemDef w && s.Count(w.Id) + r.ResultCount <= w.MaxStack)
                .Select(r => { var w = c.Item(r.Result); return new Plan { Recipe = r, Weapon = w, Hero = SuitedHero(c, s, w), Ready = Crafting.HasInputs(s, r) && s.Gold >= r.Gold }; })
                .Where(p => p.Hero != null)
                .OrderByDescending(p => p.Ready).ThenByDescending(p => Affinity(c, p.Hero, p.Weapon))
                .ThenBy(p => p.Recipe.Inputs.Sum(i => Math.Max(0, i.Count - s.Count(i.Item))))
                .FirstOrDefault();
        }

        /// <summary>Forging <paramref name="r"/> is the guided first forge: it is a starter recipe and the first forge is still ahead.</summary>
        public static bool IsGuided(SaveData s, RecipeDef r) => IsStarter(r) && !Done(s);

        /// <summary>The guided first forge: makes the starter weapon and equips it on the hero it suits (when one gains
        /// from it), then marks the first forge done. Anything else is an ordinary <see cref="Crafting.Craft"/>.</summary>
        public static Result Forge(ContentDb c, SaveData s, RecipeDef r, bool lucky = false)
        {
            if (IsGuidedArmor(c, s, r)) return ForgeFirstArmor(c, s, r, lucky);
            if (!IsGuided(s, r)) return Crafting.Craft(c, s, r, lucky: lucky);
            var weapon = c.TryItem(r.Result);
            var hero = SuitedHero(c, s, weapon);
            var made = Crafting.Craft(c, s, r, lucky: lucky);
            if (!made.Ok) return made;
            s.Flags.Add(FirstForgeFlag);
            if (hero == null) return made;
            var equipped = Equipment.Equip(c, s, hero, EquipmentSlot.Weapon, weapon.Id);
            string name = c.TryClass(hero.ClassId)?.Name ?? hero.ClassId;
            return equipped.Ok ? Result.Success($"{made.Message}: the {name} equips it") : made;
        }

        // ── Onboarding step 5: forge armor (GG-T178, §25.0.7) ─────────────────────────────────────────────
        // The armor-set recipes (GG-T182) open at the Forge once the first weapon is made; the guided first armor
        // forges one set piece and equips it on the hero it suits.

        /// <summary>Save flag set by the guided first armor forge.</summary>
        public const string FirstArmorFlag = "onboard:first-armor";
        /// <summary>The camp-tip key of the Forge tab once the first weapon is made and the first armor is still ahead.</summary>
        public const string ArmorTipStation = "blacksmith-armor";
        /// <summary>Why a set piece can't be forged yet.</summary>
        public const string ArmorLockedHint = "Armor recipes open after your first forge";

        /// <summary>A Forge recipe for a piece of a named armor set (GG-T182).</summary>
        public static bool IsSetArmor(ContentDb c, RecipeDef r) =>
            c != null && r != null && r.Station == CraftStation.Forge && r.BaseItem == null && c.TryArmorSetOf(r.Result) != null;

        /// <summary>The armor-set recipes are open at the Forge: always, except for a set piece before the first forge.</summary>
        public static bool ArmorOpen(ContentDb c, SaveData s, RecipeDef r) => !IsSetArmor(c, r) || Done(s);

        public static List<RecipeDef> SetArmorRecipes(ContentDb c) => Crafting.Recipes(c, CraftStation.Forge).Where(r => IsSetArmor(c, r)).ToList();

        /// <summary>The first armor is behind this save: it has been forged, or the save is a bot's or a New Game+.</summary>
        public static bool ArmorDone(SaveData s) => s == null || s.TestMode || s.NewGamePlus > 0 || s.Flags.Contains(FirstArmorFlag);

        /// <summary>The first armor step is on: the first weapon is made and the first armor isn't.</summary>
        public static bool ArmorStep(SaveData s) => Done(s) && !ArmorDone(s);

        /// <summary>The party hero an armor piece suits: one who is proficient in its weight and better off with it than
        /// with what they wear in that slot, the best stat match first (then the bigger gain, then party order).</summary>
        public static HeroSave SuitedWearer(ContentDb c, SaveData s, ItemDef item)
        {
            if (c == null || s == null || item == null || Array.IndexOf(BattleEngine.ArmorSlots, item.Slot) < 0) return null;
            float score = GridGame.Core.Bot.MetaHeuristics.Score(item);
            return s.Party.Select(s.Hero)
                .Where(h => h != null && Equipment.Fits(c, h, item, item.Slot))
                .Select(h => (h, gain: score - GridGame.Core.Bot.MetaHeuristics.Score(c.TryItem(Equipment.Equipped(h, item.Slot)))))
                .Where(x => x.gain > 0f)
                .OrderByDescending(x => Affinity(c, x.h, item)).ThenByDescending(x => x.gain)
                .Select(x => x.h).FirstOrDefault();
        }

        /// <summary>The set piece the Forge's armor tip walks and the Forge preselects, or null outside the armor step:
        /// an open Apprentice set recipe that suits a party hero and can be carried, ready-to-forge ones first, then the
        /// best stat match, then the fewest materials still to find, then the cheapest.</summary>
        public static Plan FirstArmor(ContentDb c, SaveData s)
        {
            if (c == null || !ArmorStep(s)) return null;
            return SetArmorRecipes(c)
                .Where(r => r.Tier <= 1 && Crafting.CanMake(c, s, r) && VendorTiers.RecipeOpen(c, s, r) && c.TryItem(r.Result) is ItemDef a && s.Count(a.Id) + r.ResultCount <= a.MaxStack)
                .Select(r => { var a = c.Item(r.Result); return new Plan { Recipe = r, Weapon = a, Hero = SuitedWearer(c, s, a), Ready = Crafting.HasInputs(s, r) && s.Gold >= r.Gold }; })
                .Where(p => p.Hero != null)
                .OrderByDescending(p => p.Ready).ThenByDescending(p => Affinity(c, p.Hero, p.Weapon))
                .ThenBy(p => p.Recipe.Inputs.Sum(i => Math.Max(0, i.Count - s.Count(i.Item)))).ThenBy(p => p.Recipe.Gold)
                .FirstOrDefault();
        }

        /// <summary>Forging <paramref name="r"/> is the guided first armor: a set piece, during the armor step.</summary>
        public static bool IsGuidedArmor(ContentDb c, SaveData s, RecipeDef r) => IsSetArmor(c, r) && ArmorStep(s);

        /// <summary>The guided first armor: makes the set piece and equips it on the hero it suits (when one gains from
        /// it), then marks the first armor done.</summary>
        private static Result ForgeFirstArmor(ContentDb c, SaveData s, RecipeDef r, bool lucky)
        {
            var piece = c.TryItem(r.Result);
            var hero = SuitedWearer(c, s, piece);
            var made = Crafting.Craft(c, s, r, lucky: lucky);
            if (!made.Ok) return made;
            s.Flags.Add(FirstArmorFlag);
            if (hero == null) return made;
            var equipped = Equipment.Equip(c, s, hero, piece.Slot, piece.Id);
            string name = c.TryClass(hero.ClassId)?.Name ?? hero.ClassId;
            return equipped.Ok ? Result.Success($"{made.Message}: the {name} wears it") : made;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Armor enchants (GG-T179, §25.0.8): the Enchantress binds a passive (regeneration, poison ward, thorns…) into a
    // worn armor piece. Onboarding step 6: she joins once the first armor piece is forged, and the guided first
    // enchant (free of gold) binds one into it.
    // ─────────────────────────────────────────────────────────────────────────────

    public static class ArmorEnchanting
    {
        /// <summary>Save flag set by the first armor enchant.</summary>
        public const string FirstEnchantFlag = "onboard:first-enchant";

        /// <summary>One worn armor piece the Enchantress can work on.</summary>
        public sealed class Target
        {
            public HeroSave Hero;
            public EquipmentSlot Slot;
            public EquippedArmor Piece;
            public ItemDef Item;
            public string RowName => PieceRowPrefix + Hero.ClassId + "-" + Slot.ToString().ToLowerInvariant();
        }

        /// <summary>The guided first enchant: which enchant, into which piece, and whether it can be bound right now.</summary>
        public sealed class Plan
        {
            public ArmorEnchantDef Enchant;
            public Target Target;
            public bool Ready;
        }

        /// <summary>Row-name prefixes on the Enchantress's Armor tab (camp-tip and automation targets).</summary>
        public const string PieceRowPrefix = "piece-", EnchantRowPrefix = "aenchant-";

        public static IReadOnlyList<ArmorEnchantDef> All(ContentDb c) => c.ArmorEnchantOrder;

        /// <summary>Like every Enchantress recipe, an armor enchant's tier unlocks with the Alchemist's station level.</summary>
        public static bool IsUnlocked(SaveData s, ArmorEnchantDef e) => e != null && StationProgress.IsUnlocked(s, StationKind.Alchemist, e.Tier);

        public static bool HasInputs(SaveData s, ArmorEnchantDef e) => s != null && e != null && e.Inputs.All(i => s.Count(i.Item) >= i.Count);

        /// <summary>The first enchant is behind this save: it has been bound, or the save is a bot's or a New Game+.</summary>
        public static bool FirstDone(SaveData s) => s == null || s.TestMode || s.NewGamePlus > 0 || s.Flags.Contains(FirstEnchantFlag);

        /// <summary>Gold an enchant costs this save: nothing for the guided first one.</summary>
        public static int Gold(SaveData s, ArmorEnchantDef e) => e == null ? 0 : FirstDone(s) ? e.Gold : 0;

        /// <summary>Gold an enchant costs this save after the caravan's Enchanter's Wagon discount (GG-T75).</summary>
        public static int Gold(ContentDb c, SaveData s, ArmorEnchantDef e) => CaravanUpgrades.EnchantGold(c, s, Gold(s, e));

        public static bool CanAfford(SaveData s, ArmorEnchantDef e) => s != null && s.Gold >= Gold(s, e);

        public static bool CanAfford(ContentDb c, SaveData s, ArmorEnchantDef e) => s != null && s.Gold >= Gold(c, s, e);

        /// <summary>Every intact armor piece the roster wears, party heroes first.</summary>
        public static List<Target> Pieces(ContentDb c, SaveData s)
        {
            var list = new List<Target>();
            if (c == null || s == null) return list;
            var heroes = s.Party.Select(s.Hero).Where(h => h != null).Concat(s.Roster.Where(h => !s.Party.Contains(h.ClassId))).ToList();
            foreach (var h in heroes)
                foreach (var slot in BattleEngine.ArmorSlots)
                    if (h.Armor.TryGetValue(slot, out var piece) && piece?.ItemId != null && c.TryItem(piece.ItemId) is ItemDef item && item.Durability > 0)
                        list.Add(new Target { Hero = h, Slot = slot, Piece = piece, Item = item });
            return list;
        }

        public static Target Find(ContentDb c, SaveData s, string classId, EquipmentSlot slot) =>
            Pieces(c, s).FirstOrDefault(t => t.Hero.ClassId == classId && t.Slot == slot);

        /// <summary>The first enchant the Enchantress's camp tip walks and her Armor tab preselects, or null once it's done
        /// (or nobody wears armor yet): an unenchanted piece (a set piece first, as the guided first armor is), and an open
        /// enchant whose materials are in the bag first, then the fewest materials still to find, then file order.</summary>
        public static Plan First(ContentDb c, SaveData s)
        {
            if (c == null || FirstDone(s)) return null;
            var target = Pieces(c, s).Where(t => t.Piece.Enchant == null)
                .OrderByDescending(t => c.TryArmorSetOf(t.Item.Id) != null).FirstOrDefault();
            if (target == null) return null;
            var e = All(c).Select((x, i) => (x, i)).Where(p => IsUnlocked(s, p.x))
                .OrderByDescending(p => HasInputs(s, p.x))
                .ThenBy(p => p.x.Inputs.Sum(i => Math.Max(0, i.Count - s.Count(i.Item))))
                .ThenBy(p => p.i).Select(p => p.x).FirstOrDefault();
            return e == null ? null : new Plan { Enchant = e, Target = target, Ready = HasInputs(s, e) };
        }

        /// <summary>Binds <paramref name="e"/> into the armor <paramref name="h"/> wears in <paramref name="slot"/>, replacing
        /// any enchant it had. The piece keeps it through unequip and re-equip, and loses it if it breaks.</summary>
        public static Result Enchant(ContentDb c, SaveData s, HeroSave h, EquipmentSlot slot, ArmorEnchantDef e)
        {
            if (c == null || s == null || h == null || e == null) return Result.Fail("Nothing to enchant");
            if (!h.Armor.TryGetValue(slot, out var piece) || piece?.ItemId == null) return Result.Fail("No armor worn there");
            var item = c.TryItem(piece.ItemId);
            if (item == null || item.Durability <= 0) return Result.Fail("That can't hold an enchant");
            if (piece.Enchant == e.Id) return Result.Fail($"Already bound with {e.Name}");
            if (!IsUnlocked(s, e)) return Result.Fail($"Requires {StationProgress.TierName(e.Tier)}");
            if (!HasInputs(s, e)) return Result.Fail("Missing materials");
            int gold = Gold(c, s, e);
            if (s.Gold < gold) return Result.Fail("Not enough gold");
            s.Gold -= gold;
            foreach (var ic in e.Inputs) s.AddItem(ic.Item, -ic.Count);
            string replaced = c.TryArmorEnchant(piece.Enchant)?.Name;
            piece.Enchant = e.Id;
            StationProgress.Gain(s, StationKind.Alchemist, Crafting.CraftXp);
            s.Flags.Add(FirstEnchantFlag);
            string passive = c.TryAbility(e.Passive)?.Name ?? e.Name;
            string hero = c.TryClass(h.ClassId)?.Name ?? h.ClassId;
            return Result.Success($"{hero}'s {item.Name} now grants {passive}" + (replaced != null ? $" (replacing {replaced})" : ""));
        }

        /// <summary>The passive ability an enchant id grants, or null.</summary>
        public static AbilityDef PassiveOf(ContentDb c, string enchantId) => c?.TryAbility(c.TryArmorEnchant(enchantId)?.Passive);
    }

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

    // ─────────────────────────────────────────────────────────────────────────────
    // Repair (§24.5, §25.2)
    // ─────────────────────────────────────────────────────────────────────────────

    public static class Repair
    {
        public const int RepairXp = 8;

        /// <summary>A first repair's price per durability point, as a share of the piece's price per point; each later
        /// repair of the same piece costs 1.6x the last. GG-T448: a camp bot that forges and keeps its gear equipped
        /// spends 97-98% of a campaign's gold on repairs regardless of this rate (measured at 0.30: 17,246 earned, 16,758
        /// repaired; at 0.15: 18,152 earned, 17,865 repaired) — cheaper repairs buy more repairs before a piece crosses
        /// <see cref="IsUneconomical"/> rather than freeing up gold, since CampRoutine repairs every worn piece back to
        /// full every visit by design (facilities and the Summon Circle are deliberately left to repair's leftovers).
        /// Halved from 0.30 regardless, since it still gives facilities/gear a little more room (150→275, 3,937→4,981
        /// gold/run) — but a player who keeps every piece of gear repaired will still see almost no gold left over for
        /// anything else. Flagged in RELEASE_SCOPE as a watch item for real play, not something this rate alone fixes.
        /// GG-T540 fixed it at the routine instead: it repairs only below <see cref="CampRoutine.RepairBelowFraction"/>,
        /// which brought repairs to 19-28% of main-campaign spend (BIBLE §25.0.11).</summary>
        public const float RepairRate = 0.15f;

        /// <summary>The piece's durability ceiling — raised by <see cref="BattleConfig.MasterworkDurabilityMultiplier"/> for a Masterwork copy (GG-T46).</summary>
        public static int EffectiveMax(ItemDef item, int repairs, float mastery = 1f) =>
            Math.Max(1, (int)Math.Round(item.Durability * (mastery > 1f ? BattleConfig.MasterworkDurabilityMultiplier : 1f)) - repairs);

        /// <summary>Repair price after the caravan's Armory Wagon discount.</summary>
        public static int Cost(ContentDb c, SaveData s, ItemDef item, int current, int repairs, float mastery = 1f)
        {
            int baseCost = Cost(item, current, repairs, mastery);
            if (baseCost <= 0) return 0;
            return Math.Max(1, (int)Math.Ceiling(baseCost * (1f - CaravanUpgrades.Value(c, s, "armory"))));
        }

        public static int Cost(ItemDef item, int current, int repairs, float mastery = 1f)
        {
            if (item == null || item.Durability <= 0) return 0;
            int missing = Math.Max(0, EffectiveMax(item, repairs, mastery) - current);
            if (missing == 0) return 0;
            float perPoint = (float)item.BaseCost / item.Durability;
            float mult = RepairRate * (float)Math.Pow(1.6f, Math.Max(0, repairs));
            return Math.Max(1, (int)Math.Ceiling(missing * perPoint * mult));
        }

        public static bool IsUneconomical(ItemDef item, int current, int repairs, float mastery = 1f) => Cost(item, current, repairs, mastery) >= item.BaseCost;

        /// <summary>GG-T544: worn below <see cref="CampRoutine.RepairBelowFraction"/> of its ceiling, the point where the camp
        /// routine repairs it. Above it a repair is "cheaper to wait": every repair, however small, raises the piece's next
        /// repair price 1.6x, so mending scratches compounds the price. The Repair tab's Repair all takes only these.</summary>
        public static bool Due(ItemDef item, int durability, int repairs, float mastery = 1f) =>
            item != null && durability < EffectiveMax(item, repairs, mastery) * CampRoutine.RepairBelowFraction;

        /// <summary>GG-T544: what <paramref name="wear"/> points of wear cost to repair at the piece's current repair count —
        /// the Repair tab's "per 10 wear" price hint (pass repairs + 1 for the price after this repair).</summary>
        public static int WearCost(ContentDb c, SaveData s, ItemDef item, int wear, int repairs, float mastery = 1f) =>
            Cost(c, s, item, Math.Max(0, EffectiveMax(item, repairs, mastery) - wear), repairs, mastery);

        /// <summary>GG-T544: every equipped piece Repair all would mend (worn and <see cref="Due"/>), most worn first.</summary>
        public static List<WornPiece> DuePieces(ContentDb c, SaveData s) =>
            Pieces(c, s).Where(p => p.Durability < p.Max && Due(p.Item, p.Durability, p.Repairs, p.Mastery)).OrderBy(p => p.Fraction).ToList();

        /// <summary>The piece's repair price now, after the Armory Wagon discount.</summary>
        public static int Cost(ContentDb c, SaveData s, WornPiece p) => Cost(c, s, p.Item, p.Durability, p.Repairs, p.Mastery);

        /// <summary>GG-T544: the Repair tab's Repair all — mends every <see cref="DuePieces"/> piece the purse covers, most
        /// worn first, and leaves pieces above the threshold for later (they can still be repaired by hand).</summary>
        public static Result RepairAll(ContentDb c, SaveData s)
        {
            var due = DuePieces(c, s);
            if (due.Count == 0) return Result.Fail("Nothing needs repair yet");
            int done = 0, spent = 0;
            foreach (var p in due)
            {
                int cost = Cost(c, s, p);
                if (cost <= 0 || cost > s.Gold) continue;
                var r = p.Slot == EquipmentSlot.Weapon ? RepairWeapon(c, s, p.Hero) : RepairArmor(c, s, p.Hero, p.Slot);
                if (r.Ok) { done++; spent += cost; }
            }
            if (done == 0) return Result.Fail("Not enough gold");
            return Result.Success(done == 1 ? $"Repaired 1 piece for {spent:N0} G" : $"Repaired {done} pieces for {spent:N0} G");
        }

        /// <summary>One equipped piece that wears (weapon or armor) and how worn it is.</summary>
        public sealed class WornPiece
        {
            public HeroSave Hero;
            public EquipmentSlot Slot;
            public ItemDef Item;
            public int Durability, Max, Repairs;
            public float Mastery = 1f;
            public float Fraction => Max <= 0 ? 1f : (float)Durability / Max;
            /// <summary>The Repair tab row's element name ("repair-knight-Weapon") — what the Blacksmith's camp tip points at.</summary>
            public string RowName => RowNameFor(Hero.ClassId, Slot);
        }

        public static string RowNameFor(string classId, EquipmentSlot slot) => $"repair-{classId}-{slot}";

        /// <summary>Every equipped weapon and armor piece across the roster that has durability.</summary>
        public static IEnumerable<WornPiece> Pieces(ContentDb c, SaveData s)
        {
            if (s == null) yield break;
            foreach (var h in s.Roster)
            {
                var w = c.TryItem(h.WeaponId);
                if (w != null && w.Durability > 0)
                    yield return new WornPiece { Hero = h, Slot = EquipmentSlot.Weapon, Item = w, Durability = h.WeaponDurability, Max = EffectiveMax(w, h.WeaponRepairs, h.WeaponMastery), Repairs = h.WeaponRepairs, Mastery = h.WeaponMastery };
                foreach (var slot in BattleEngine.ArmorSlots)
                {
                    if (!h.Armor.TryGetValue(slot, out var piece)) continue;
                    var a = c.TryItem(piece.ItemId);
                    if (a != null && a.Durability > 0)
                        yield return new WornPiece { Hero = h, Slot = slot, Item = a, Durability = piece.Durability, Max = EffectiveMax(a, piece.Repairs, piece.Mastery), Repairs = piece.Repairs, Mastery = piece.Mastery };
                }
            }
        }

        /// <summary>The most worn equipped piece (lowest durability fraction), or null when nobody wears gear that wears.</summary>
        public static WornPiece MostWorn(ContentDb c, SaveData s) => Pieces(c, s).OrderBy(p => p.Fraction).FirstOrDefault();


        public static Result RepairWeapon(ContentDb c, SaveData s, HeroSave h)
        {
            var item = c.TryItem(h.WeaponId);
            if (item == null || item.Durability <= 0) return Result.Fail("Nothing to repair");
            int cost = Cost(c, s, item, h.WeaponDurability, h.WeaponRepairs, h.WeaponMastery);
            if (cost == 0) return Result.Fail("Already in top shape");
            if (s.Gold < cost) return Result.Fail("Not enough gold");
            s.Gold -= cost;
            h.WeaponRepairs++;
            h.WeaponDurability = EffectiveMax(item, h.WeaponRepairs, h.WeaponMastery);
            StationProgress.Gain(s, StationKind.Blacksmith, RepairXp);
            return Result.Success($"Repaired {item.Name}");
        }

        /// <summary>Repairs one equipped armor piece (Head/Torso/Legs/Gloves/Boots) — each slot has its own
        /// durability/repair count, so this takes which slot instead of assuming the single old Armor slot (GG-T89).</summary>
        public static Result RepairArmor(ContentDb c, SaveData s, HeroSave h, EquipmentSlot slot)
        {
            if (!h.Armor.TryGetValue(slot, out var piece)) return Result.Fail("Nothing equipped");
            var item = c.TryItem(piece.ItemId);
            if (item == null || item.Durability <= 0) return Result.Fail("Nothing to repair");
            int cost = Cost(c, s, item, piece.Durability, piece.Repairs, piece.Mastery);
            if (cost == 0) return Result.Fail("Already in top shape");
            if (s.Gold < cost) return Result.Fail("Not enough gold");
            s.Gold -= cost;
            piece.Repairs++;
            piece.Durability = EffectiveMax(item, piece.Repairs, piece.Mastery);
            StationProgress.Gain(s, StationKind.Blacksmith, RepairXp);
            return Result.Success($"Repaired {item.Name}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Inn / Heal party (§25.3)
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>GG-T1790: the Inn's gold heal is gone; camp rest (<see cref="CampRestService"/>) heals for free.</summary>
    public static class Inn
    {
        public static int MissingHp(ContentDb c, SaveData s, HeroSave h)
        {
            if (h.Hp <= 0) return 0;
            var max = Progression.Preview(c, h).MaxHp;
            return Math.Max(0, max - h.Hp);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Equip (§24.2, §25.4)
    // ─────────────────────────────────────────────────────────────────────────────

    public static class Equipment
    {
        /// <summary>The five jewelry slots, in the order §24.2's equip screen lists them (GG-T89).</summary>
        public static readonly EquipmentSlot[] JewelrySlots =
        {
            EquipmentSlot.Necklace, EquipmentSlot.Earring1, EquipmentSlot.Earring2, EquipmentSlot.Ring1, EquipmentSlot.Ring2,
        };

        private static bool IsArmorSlot(EquipmentSlot slot) => Array.IndexOf(BattleEngine.ArmorSlots, slot) >= 0;
        private static bool IsJewelrySlot(EquipmentSlot slot) => Array.IndexOf(JewelrySlots, slot) >= 0;

        public static string Equipped(HeroSave h, EquipmentSlot slot)
        {
            if (slot == EquipmentSlot.Weapon) return h.WeaponId;
            if (IsArmorSlot(slot)) return h.Armor.TryGetValue(slot, out var a) ? a.ItemId : null;
            if (IsJewelrySlot(slot)) return h.Jewelry.TryGetValue(slot, out var id) ? id : null;
            return null;
        }

        public static bool Fits(ItemDef item, EquipmentSlot slot)
        {
            if (item == null || item.Type != ItemType.Equipment) return false;
            if (slot == EquipmentSlot.Earring1 || slot == EquipmentSlot.Earring2) return item.IsEarring;
            if (slot == EquipmentSlot.Ring1 || slot == EquipmentSlot.Ring2) return item.IsRing;
            return item.Slot == slot;
        }

        /// <summary>GG-T52: shape fit (above) plus proficiency — refuses gear <paramref name="h"/> isn't
        /// trained to use. The shape-only overload still drives the Equip screen's bag listing (so a locked
        /// piece the hero owns stays visible with a lock badge instead of disappearing), and bot/shop code
        /// that should never pick gear it can't use calls this one instead.</summary>
        public static bool Fits(ContentDb c, HeroSave h, ItemDef item, EquipmentSlot slot) =>
            Fits(item, slot) && ProficiencyGate(c, h, item) == null;

        /// <summary>Null if <paramref name="h"/> can equip <paramref name="item"/> today; otherwise the name
        /// of the proficiency-unlock passive that would let them (GG-T52) — e.g. "Heavy Armor Proficiency".
        /// Only the Weapon and armor slots are ever gated; jewelry and consumables never are.</summary>
        public static string ProficiencyGate(ContentDb c, HeroSave h, ItemDef item)
        {
            if (item == null || item.Type != ItemType.Equipment) return null;
            if (item.Slot == EquipmentSlot.Weapon)
            {
                if (item.WeaponClass == WeaponClass.Simple) return null;
                if (Progression.EffectiveWeaponProficiencies(c, h).Contains(item.WeaponClass)) return null;
                var passiveId = item.WeaponClass == WeaponClass.Arcane ? "arcane_focus_proficiency" : "martial_weapons_proficiency";
                return c.TryAbility(passiveId)?.Name ?? passiveId;
            }
            if (IsArmorSlot(item.Slot))
            {
                if (item.ArmorWeight <= Progression.EffectiveArmorProficiency(c, h)) return null;
                return c.TryAbility("heavy_armor_proficiency")?.Name ?? "Heavy Armor Proficiency";
            }
            return null;
        }

        /// <summary>Everything in the bag that shape-fits this slot (locked gear included, GG-T52) — the Equip
        /// screen's bag listing. Pass <paramref name="h"/> to additionally drop anything the hero isn't
        /// proficient in (bot/shop shopping lists, which should never pick gear it can't wear).</summary>
        public static List<ItemDef> Candidates(ContentDb c, SaveData s, EquipmentSlot slot, HeroSave h = null) =>
            s.Inventory.Where(kv => kv.Value > 0).Select(kv => c.TryItem(kv.Key))
                .Where(i => Fits(i, slot) && (h == null || ProficiencyGate(c, h, i) == null))
                .OrderByDescending(i => i.Rarity).ThenBy(i => i.Name).ToList();

        /// <summary>True if the one owned (unequipped) copy of this item id is a Masterwork roll (§24.6.2, GG-T46).</summary>
        public static bool IsMasterwork(SaveData s, string itemId) =>
            itemId != null && s.Worn.TryGetValue(itemId, out var list) && list.Any(w => w.Mastery > 1f);

        public static Result Equip(ContentDb c, SaveData s, HeroSave h, EquipmentSlot slot, string itemId)
        {
            var item = c.TryItem(itemId);
            if (h != null && h.Domesticated) return Result.Fail("A tamed monster wears no gear"); // GG-T303
            if (!Fits(item, slot)) return Result.Fail("That doesn't go there");
            var gate = ProficiencyGate(c, h, item);
            if (gate != null) return Result.Fail($"Not proficient — needs {gate}");
            if (s.Count(itemId) < 1) return Result.Fail("You don't own that");
            Unequip(c, s, h, slot);
            if (IsJewelrySlot(slot))
            {
                s.AddItem(itemId, -1);
                h.Jewelry[slot] = itemId;
                return Result.Success($"Equipped {item.Name}");
            }
            // Fresh copies first; otherwise the best of the worn ones (per-instance durability, US-246).
            var copy = TakeCopy(s, item);
            s.AddItem(itemId, -1);
            if (slot == EquipmentSlot.Weapon) { h.WeaponId = itemId; h.WeaponDurability = copy.Durability; h.WeaponRepairs = copy.Repairs; h.WeaponMastery = copy.Mastery; }
            else h.Armor[slot] = new EquippedArmor { ItemId = itemId, Durability = copy.Durability, Repairs = copy.Repairs, Mastery = copy.Mastery, Enchant = copy.Enchant };
            return Result.Success(copy.Mastery > 1f ? $"Equipped ★ {item.Name}" : $"Equipped {item.Name}");
        }

        private static WornGear TakeCopy(SaveData s, ItemDef item)
        {
            s.Worn.TryGetValue(item.Id, out var worn);
            int fresh = s.Count(item.Id) - (worn?.Count ?? 0);
            // An enchanted copy (GG-T179) is put on before a fresh one, so taking a piece off and back on keeps its enchant.
            bool enchanted = worn != null && worn.Any(w => w.Enchant != null);
            if (!enchanted && (fresh > 0 || worn == null || worn.Count == 0)) return new WornGear { Durability = item.Durability, Repairs = 0 };
            worn.Sort((a, b) => { int e = (b.Enchant != null).CompareTo(a.Enchant != null); return e != 0 ? e : b.Durability.CompareTo(a.Durability); });
            var best = worn[0];
            worn.RemoveAt(0);
            if (worn.Count == 0) s.Worn.Remove(item.Id);
            return best;
        }

        /// <summary>Returns the piece to the bag, remembering its wear, repairs and Masterwork roll if it isn't factory-fresh.</summary>
        public static Result Unequip(ContentDb c, SaveData s, HeroSave h, EquipmentSlot slot)
        {
            var id = Equipped(h, slot);
            if (id == null) return Result.Fail("Nothing equipped");
            var item = c.TryItem(id);
            if (IsJewelrySlot(slot))
            {
                s.AddItem(id, 1);
                h.Jewelry.Remove(slot);
                return Result.Success("Unequipped");
            }
            int dur = 0, reps = 0; float mastery = 1f; string enchant = null;
            if (slot == EquipmentSlot.Weapon) { dur = h.WeaponDurability; reps = h.WeaponRepairs; mastery = h.WeaponMastery; }
            else if (h.Armor.TryGetValue(slot, out var a)) { dur = a.Durability; reps = a.Repairs; mastery = a.Mastery; enchant = a.Enchant; }
            s.AddItem(id, 1);
            if (item != null && item.Durability > 0 && (dur < item.Durability || reps > 0 || mastery > 1f || enchant != null))
            {
                if (!s.Worn.TryGetValue(id, out var list)) s.Worn[id] = list = new List<WornGear>();
                list.Add(new WornGear { Durability = dur, Repairs = reps, Mastery = mastery, Enchant = enchant });
            }
            if (slot == EquipmentSlot.Weapon) { h.WeaponId = null; h.WeaponDurability = 0; h.WeaponRepairs = 0; h.WeaponMastery = 1f; }
            else h.Armor.Remove(slot);
            return Result.Success("Unequipped");
        }

        /// <summary>Stat preview: the hero's effective stats if <paramref name="itemId"/> replaced the slot.</summary>
        public static (Stats now, Stats then) Compare(ContentDb c, HeroSave h, EquipmentSlot slot, string itemId)
        {
            var now = Progression.Preview(c, h).Effective;
            var copy = new HeroSave
            {
                ClassId = h.ClassId, TotalXp = h.TotalXp, StatPicks = h.StatPicks, // stat picks: same base on both sides (GG-T591)
                WeaponId = h.WeaponId, WeaponMastery = h.WeaponMastery,
                Armor = h.Armor.ToDictionary(kv => kv.Key, kv => new EquippedArmor { ItemId = kv.Value.ItemId, Mastery = kv.Value.Mastery }),
                Jewelry = new Dictionary<EquipmentSlot, string>(h.Jewelry),
            };
            if (slot == EquipmentSlot.Weapon) copy.WeaponId = itemId;
            else if (IsJewelrySlot(slot)) copy.Jewelry[slot] = itemId;
            else copy.Armor[slot] = new EquippedArmor { ItemId = itemId, Mastery = 1f };
            return (now, Progression.Preview(c, copy).Effective);
        }

        /// <summary>The armor sets this hero is wearing pieces of, most pieces first (GG-T182, Status/Equip screens).</summary>
        public static List<SetProgress> WornSets(ContentDb c, HeroSave h) =>
            ArmorSets.Progress(c, h.Armor.Values.Where(a => a?.ItemId != null).Select(a => a.ItemId));

        /// <summary>Set progress if <paramref name="itemId"/> (null = empty) replaced whatever is in <paramref name="slot"/>
        /// — the Equip screen's "3/5 → 4/5" preview.</summary>
        public static List<SetProgress> WornSetsWith(ContentDb c, HeroSave h, EquipmentSlot slot, string itemId)
        {
            var ids = h.Armor.Where(kv => kv.Key != slot && kv.Value?.ItemId != null).Select(kv => kv.Value.ItemId).ToList();
            if (itemId != null && IsArmorSlot(slot)) ids.Add(itemId);
            return ArmorSets.Progress(c, ids);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Abilities: learn + slot (§25.6)
    // ─────────────────────────────────────────────────────────────────────────────

    public static class Abilities
    {
        /// <summary>What a hero could learn: its class list plus the general spell catalog (hero spells only). GG-T1079: no
        /// longer sold for gold (abilities come from the skill board); kept as the catalog the ability audits check.
        /// GG-T1098: the family-tree paths are retired, so every class lists its <see cref="ClassDef.Abilities"/> and the whole catalog.</summary>
        public static List<AbilityDef> Learnable(ContentDb c, HeroSave h)
        {
            var def = c.TryClass(h.ClassId);
            var set = new List<AbilityDef>();
            if (h.Domesticated) return set; // a tamed monster fights with its own instincts, never a trainer's skills (GG-T303)
            var own = def == null ? new List<string>() : def.Abilities;
            foreach (var id in own)
            {
                var a = c.TryAbility(id);
                if (a != null && IsHeroUsable(a) && !h.Known.Contains(a.Id) && !set.Contains(a)) set.Add(a);
            }
            foreach (var a in c.Abilities.Values)
                if (IsHeroUsable(a) && !h.Known.Contains(a.Id) && !set.Contains(a) &&
                    (a.Kind == AbilityKind.Spell || a.Universal))
                    set.Add(a);
            // GG-T388: the hero's own kit moves first (the cheapest, and what it was built to do), then by price.
            var kit = Progression.KitAbilities(def);
            return set.OrderBy(a => kit.Contains(a.Id) ? 0 : 1).ThenBy(a => Price(a)).ThenBy(a => a.Name).ToList();
        }

        /// <summary>Boss-phase abilities only an enemy fires (an <c>OnEnterAbility</c>): free, so never learnable.</summary>
        private static readonly HashSet<string> EnemyOnly = new HashSet<string> { "enrage", "chrono_rewind", "lich_soul_harvest", "stampede_rush", "hierarch_open_reservoir" };

        public static bool IsHeroUsable(AbilityDef a) => a.Kind != AbilityKind.Passive &&
            (a.Kind == AbilityKind.Spell || a.Kind == AbilityKind.Skill) && !a.Id.StartsWith("charge_") && !EnemyOnly.Contains(a.Id);

        /// <summary>GG-T215: priced off <see cref="BattleConfig"/>'s training constants — a spell by its orb cost,
        /// a skill by its CooldownTurns (skills carry no orb cost, so cooldown is the power proxy), replacing the
        /// old flat 300G-for-every-skill price the RELEASE_SCOPE watch-list flagged.</summary>
        public static int Price(AbilityDef a) => a.Kind == AbilityKind.Skill
            ? BattleConfig.SkillTrainBasePrice + BattleConfig.SkillTrainPricePerCooldown * a.CooldownTurns
            : BattleConfig.SpellTrainBasePrice + BattleConfig.SpellTrainPricePerOrb * a.Cost;

        /// <summary>Is <paramref name="a"/> one of this hero's own kit moves (its class's default loadout)? Kit moves are waived
        /// from their Requires (<see cref="RequiresWaived"/>).</summary>
        public static bool IsKitMove(ContentDb c, HeroSave h, AbilityDef a)
        {
            if (a == null || h == null) return false;
            var def = c.TryClass(h.ClassId);
            return Progression.KitAbilities(def).Contains(a.Id);
        }

        /// <summary>Every hero-usable ability some hero could learn (GG-T1079: the gold Learn screen is gone): any spell, any universal skill, and
        /// every skill on a hero class's list (the union of <see cref="Learnable"/> over the hero classes).</summary>
        public static List<AbilityDef> LearnableByAnyHero(ContentDb c)
        {
            var onHeroLists = new HashSet<string>();
            foreach (var cls in c.Classes.Values)
                if (cls.Team == Team.Hero)
                    foreach (var id in cls.Abilities) onHeroLists.Add(id);
            return c.Abilities.Values
                .Where(a => IsHeroUsable(a) && (a.Kind == AbilityKind.Spell || a.Universal || onHeroLists.Contains(a.Id)))
                .OrderBy(a => a.Id).ToList();
        }

        /// <summary>
        /// GG-T183 reskin audit: the mechanic an ability plays, with its name, numbers (damage, heal, cost, cooldown,
        /// hasten amount, chance) and art stripped away. Two abilities with the same key are the same move with a
        /// different coat of paint. The key is: how it is paid and timed (skill on a cooldown, spell cast on the
        /// timeline, instant spell), its footprint (shape, radius where it changes the footprint, auto/pick, who it
        /// filters, adjacent-only), and each effect it carries — damage (Fire and Lightning separately, since they
        /// thaw/steam and shock/blind), heal or drain, the status it applies or clears, steal, orbs, revive,
        /// bonus-vs-tag, the movement it causes (teleport, knockback, pull, swap, step back) and the timeline change
        /// (hasten, delay). Other elements are resistance-only, so they count as the same mechanic.
        /// </summary>
        public static string MechanicKey(AbilityDef a)
        {
            string pay = a.Kind == AbilityKind.Spell ? (a.CastTime > 0f ? "cast" : "instant") : "skill";
            bool area = a.Shape == TargetShape.Square || a.Shape == TargetShape.Diamond || a.Shape == TargetShape.Cross;
            bool self = a.Shape == TargetShape.Self;
            bool all = a.Shape == TargetShape.AllAllies || a.Shape == TargetShape.AllEnemies;
            var k = new List<string>
            {
                pay,
                a.Shape + (area ? a.Radius.ToString() : ""),
                self || all ? "-" : a.Mode.ToString(),
                self || all ? "-" : a.Filter.ToString(),
                a.AdjacentOnly ? "adj" : "",
            };
            if (a.BaseDamage > 0) k.Add("dmg" + (a.DamageType == DamageType.Fire || a.DamageType == DamageType.Lightning ? ":" + a.DamageType : ""));
            if (a.BaseHeal > 0) k.Add(a.Filter == TargetFilter.EnemyOnly && !self ? "drain" : "heal");
            if (a.ApplyBuff != null) k.Add("buff:" + a.ApplyBuff);
            if (a.RemovesDebuffs) k.Add("cleanse");
            if (a.RemovesBuff != null) k.Add("cure:" + a.RemovesBuff);
            if (a.StealsMana) k.Add("steal");
            if (a.DrainsOrbs > 0) k.Add("siphon"); // GG-T332
            if (a.Tame > 0f) k.Add("tame"); // GG-T303
            if (a.GrantsOrbs > 0) k.Add("orbs");
            if (a.Revives) k.Add("revive");
            if (a.Escape) k.Add("escape");
            if (a.BonusVsTag != null) k.Add("vs:" + a.BonusVsTag);
            if (a.Vaults) k.Add("vault"); // GG-T868
            else if (a.IsTeleport) k.Add("teleport");
            if (a.Knockback > 0) k.Add("knockback");
            if (a.Pull > 0) k.Add("pull");
            if (a.Magnetizes > 0) k.Add("magnetize"); // GG-T878
            if (a.Vanishes) k.Add("vanish"); // GG-T861
            if (a.SwapsPlaces) k.Add("swap");
            if (a.StepBack > 0) k.Add("stepback");
            if (a.HastenU > 0f) k.Add("hasten");
            if (a.Pushback > 0f) k.Add("pushback");
            if (a.SwapsTurns) k.Add("countermarch"); // GG-T336
            if (a.Echoes) k.Add("echo"); // GG-T388
            if (a.RedirectsDebuffs) k.Add("redirect"); // GG-T887
            if (a.Afterimage > 0) k.Add("afterimage"); // GG-T867
            if (a.StealsBoon) k.Add("swipeboon"); // GG-T866
            return string.Join("|", k);
        }

        /// <summary>Does the ability do anything at all when it resolves? (GG-T183 found four that did nothing.)</summary>
        public static bool HasEffect(AbilityDef a) =>
            a.BaseDamage > 0 || a.BaseHeal > 0 || a.ApplyBuff != null || a.RemovesDebuffs || a.RemovesBuff != null || a.StealsMana || a.DrainsOrbs > 0 ||
            a.GrantsOrbs > 0 || a.Revives || a.Escape || a.IsTeleport || a.Knockback > 0 || a.Pull > 0 || a.Magnetizes > 0 || a.Vanishes || a.SwapsPlaces ||
            a.StepBack > 0 || a.HastenU > 0f || a.Pushback > 0f || a.Tame > 0f || a.SwapsTurns || a.Echoes || a.RedirectsDebuffs || a.StealsBoon || a.Afterimage > 0; // GG-T867: an Afterimage dashes; GG-T866: a Swipe Boon steals a boon; GG-T887: a Redirect Flow moves debuffs; GG-T388: an Echo repeats a move; GG-T303: a Tame turns a monster; GG-T336: a Countermarch reorders the foes

        /// <summary>GG-T592 (§25.6.5): is <paramref name="a"/> waived from its <see cref="AbilityDef.Requires"/> for this hero?
        /// A hero's own kit moves are (what its class was built to do: a fresh Red Mage is never locked out of its own
        /// Fireball), and so is the guided first skill
        /// (GG-T180) — GG-T1086: no longer, it is taught on the skill board by <see cref="Training.Teach"/>, and dropping it
        /// spares the Learn tab a Training.First route search per row.</summary>
        public static bool RequiresWaived(ContentDb c, SaveData s, HeroSave h, AbilityDef a) => IsKitMove(c, h, a);

        /// <summary>GG-T592: the stats this hero falls short on for <paramref name="a"/> (a display hint only, GG-T1031), measured against
        /// its skill hexagon stat totals (<see cref="HeroBoard.BoardStats"/>, GG-T1058; was its spent level-up stat points).
        /// Empty when it can be bought now (met or waived).</summary>
        public static List<(string Abbrev, string Stat, float Min, float Have)> Unmet(ContentDb c, SaveData s, HeroSave h, AbilityDef a)
        {
            if (a == null || h == null || !a.HasRequires || RequiresWaived(c, s, h, a)) return new List<(string, string, float, float)>();
            return a.UnmetRequires(HeroBoard.BoardStats(c, h));
        }

        /// <summary>A locked Learn entry's reason: "needs INT 8 (have 3)", or "needs VIT 5, WIS 4 (have 2, 0)". Null when met.</summary>
        public static string NeedsText(List<(string Abbrev, string Stat, float Min, float Have)> unmet) =>
            unmet == null || unmet.Count == 0 ? null
                : $"needs {string.Join(", ", unmet.Select(u => $"{u.Abbrev} {u.Min:0}"))} (have {string.Join(", ", unmet.Select(u => $"{u.Have:0}"))})";

        /// <summary>Reorder the bar (owner direction 2026-10-11: organise your abilities): swap two unlocked slots.</summary>
        public static Result Swap(SaveData s, HeroSave h, int a, int b)
        {
            int unlocked = Progression.UnlockedSlots(s);
            if (a < 0 || b < 0 || a >= unlocked || b >= unlocked) return Result.Fail("Slot locked");
            if (a == b) return Result.Fail("Same slot");
            while (h.Bar.Count < Progression.MaxBarSlots) h.Bar.Add(null);
            var t = h.Bar[a]; h.Bar[a] = h.Bar[b]; h.Bar[b] = t;
            return Result.Success("Moved");
        }

        public static Result Assign(ContentDb c, SaveData s, HeroSave h, int slot, string entry)
        {
            if (slot < 0 || slot >= Progression.MaxBarSlots) return Result.Fail("No such slot");
            if (h != null && h.Domesticated && entry != null) return Result.Fail("A tamed monster fights on instinct"); // GG-T303: no bar
            if (slot >= Progression.UnlockedSlots(s)) return Result.Fail("Slot locked");
            if (entry != null)
            {
                if (entry.StartsWith("item:"))
                {
                    var item = c.TryItem(entry.Substring(5));
                    if (item == null || item.Type != ItemType.Consumable) return Result.Fail("Not usable in battle");
                }
                else if (entry.StartsWith("weapon:"))
                {
                    var w = c.TryItem(entry.Substring(7));
                    if (w == null || w.Slot != EquipmentSlot.Weapon) return Result.Fail("Not a weapon");
                    if (s.Count(w.Id) < 1) return Result.Fail("You need a spare one in your bag");
                }
                else if (!HeroBoard.OwnsAbility(c, h, entry)) return Result.Fail("Not learned"); // known, or on an owned board node (GG-T1043)
                int dup = h.Bar.IndexOf(entry);
                if (dup >= 0 && dup != slot) h.Bar[dup] = null;
            }
            h.Bar[slot] = entry;
            return Result.Success();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Training (GG-T180, §25.0.9): onboarding step 7, the last of the chain. Once the first armor enchant is bound, the
    // Abilities screen opens on its Board sub-tab: a guided prompt trains one new skill (free) and slots it on the bar.
    // GG-T1085: the lesson is an ability node on the skill hexagon, bought (route and all) without spending skill points.
    // ─────────────────────────────────────────────────────────────────────────────

    public static class Training
    {
        /// <summary>Save flag set by the guided first skill; the onboarding chain is complete once it is set.</summary>
        public const string FirstSkillFlag = "onboard:first-skill";
        /// <summary>The Abilities screen's camp tip while this step is ahead (its plain "abilities" tip otherwise).</summary>
        public const string TipStation = "abilities-learn";

        /// <summary>The guided first skill: which hero learns which skill, and whether the bar has room for it.</summary>
        public sealed class Plan
        {
            public HeroSave Hero;
            public AbilityDef Skill;
            public int Slot;
            /// <summary>The ability node <see cref="Teach"/> buys (GG-T1085).</summary>
            public (int Q, int R) Node;
            /// <summary>The hero isn't on the board yet: <see cref="Teach"/> joins it on its class Start path first.</summary>
            public bool Join;
        }

        /// <summary>The first skill is behind this save: trained, or a bot's or New Game+ save.</summary>
        public static bool FirstDone(SaveData s) => s == null || s.TestMode || s.NewGamePlus > 0 || s.Flags.Contains(FirstSkillFlag);

        /// <summary>Step 7 is the current onboarding step: the first armor enchant is done (§25.0.8) and the first skill isn't.
        /// With the Enchanter hidden (<see cref="Features.Enchanter"/>, GG-T436) the enchant step is skipped and step 7
        /// follows the first armor (step 5) instead, so the chain never stalls on a station the player can't open.</summary>
        public static bool Step(SaveData s) => !FirstDone(s) &&
            (Features.Enchanter ? s.Flags.Contains(ArmorEnchanting.FirstEnchantFlag) : Forging.ArmorDone(s));

        /// <summary>Is <paramref name="a"/> right now the guided first skill (<see cref="First"/>'s pick, free, auto-slotted)?</summary>
        public static bool Guides(ContentDb c, SaveData s, AbilityDef a) => a != null && Step(s) && First(c, s)?.Skill == a;

        /// <summary>The bar slot a newly trained skill goes into: the first empty unlocked slot, else the last unlocked one.</summary>
        public static int SlotFor(SaveData s, HeroSave h)
        {
            int unlocked = Math.Min(Progression.UnlockedSlots(s), Progression.MaxBarSlots);
            if (h == null || unlocked <= 0) return -1;
            while (h.Bar.Count < Progression.MaxBarSlots) h.Bar.Add(null);
            for (int i = 0; i < unlocked; i++) if (h.Bar[i] == null) return i;
            return unlocked - 1;
        }

        /// <summary>The guided first skill (GG-T1085), or null outside step 7: for the party lead first (then the party,
        /// then the reserve), an ability node on the skill hexagon it can reach (joining its Start path first when it isn't
        /// on the board) whose ability it doesn't know: a frontier node when one exists, else the nearest by route; skills first.</summary>
        public static Plan First(ContentDb c, SaveData s)
        {
            if (c == null || !Step(s)) return null;
            var board = SkillHexProgress.Board(c);
            if (board == null) return null;
            var heroes = s.Party.Select(s.Hero).Where(h => h != null).Concat(s.Roster.Where(h => !s.Party.Contains(h.ClassId)));
            foreach (var h in heroes)
            {
                if (h.Domesticated) continue;
                bool join = (h.HexNodes?.Count ?? 0) == 0;
                IReadOnlyCollection<(int Q, int R)> owned = join ? board.StartOf(h.ClassId) : SkillHexProgress.Owned(h).Nodes;
                if (owned.Count == 0) continue;
                var pick = NodeFor(c, board, h, owned);
                if (pick == null) continue;
                return new Plan { Hero = h, Skill = c.TryAbility(board.Node(pick.Value).Ability), Node = pick.Value, Join = join, Slot = SlotFor(s, h) };
            }
            return null;
        }

        private static (int Q, int R)? NodeFor(ContentDb c, SkillHexBoard board, HeroSave h, IReadOnlyCollection<(int Q, int R)> owned)
        {
            bool Wanted((int Q, int R) pt)
            {
                var n = board.Node(pt);
                if (n == null || n.Kind != SkillHexNodeKind.Ability || string.IsNullOrEmpty(n.Ability) || owned.Contains(pt)) return false;
                var a = c.TryAbility(n.Ability);
                return a != null && !h.Known.Contains(a.Id) && !owned.Any(o => board.Node(o)?.Ability == a.Id);
            }
            int Rank((int Q, int R) pt) => c.TryAbility(board.Node(pt).Ability).Kind == AbilityKind.Skill ? 0 : 1;
            var near = board.Frontier(owned).Where(Wanted).OrderBy(Rank).ThenBy(p => p).ToList();
            if (near.Count > 0) return near[0];
            var far = board.Def.Nodes.Select(n => (n.Q, n.R)).Where(Wanted)
                .Select(pt => (pt, route: board.CheapestRoute(owned, pt))).Where(x => x.route != null)
                .OrderBy(x => x.route.Count).ThenBy(x => Rank(x.pt)).ThenBy(x => x.pt).ToList();
            return far.Count > 0 ? far[0].pt : ((int Q, int R)?)null;
        }

        /// <summary>Trains the guided first skill: Join if needed, buys the route to the node free (no SP), slots it via
        /// <see cref="SlotFor"/>, sets <see cref="FirstSkillFlag"/>.</summary>
        public static Result Teach(ContentDb c, SaveData s)
        {
            var plan = First(c, s);
            if (plan == null) return Result.Fail("Nothing to train");
            var h = plan.Hero;
            var board = SkillHexProgress.Board(c);
            if (plan.Join) { var j = SkillHexProgress.Join(c, h); if (!j.Ok) return j; }
            var route = board.CheapestRoute(SkillHexProgress.Owned(h).Nodes, plan.Node);
            if (route == null) return Result.Fail("Can't reach that");
            foreach (var pt in route)
            {
                int price = board.Price(pt);
                h.SkillPoints += price; // free: the price is granted, then spent
                var r = SkillHexProgress.Buy(c, h, pt);
                if (!r.Ok) { h.SkillPoints -= price; return r; }
            }
            Codex.Unlock(s, CodexKinds.Ability, plan.Skill.Id);
            int slot = SlotFor(s, h);
            if (slot >= 0) Abilities.Assign(c, s, h, slot, plan.Skill.Id);
            s.Flags.Add(FirstSkillFlag);
            string hero = c.TryClass(h.ClassId)?.Name ?? h.ClassId;
            return Result.Success(slot >= 0 ? $"{hero} learned {plan.Skill.Name}: it's on the battle bar, slot {slot + 1}" : $"{hero} learned {plan.Skill.Name}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Party & Summon Circle (§23.4, §25.5, §25.10)
    // ─────────────────────────────────────────────────────────────────────────────

    public static class Party
    {
        public static Result Toggle(SaveData s, string classId)
        {
            if (s.Hero(classId) == null) return Result.Fail("Not in roster");
            if (s.Party.Contains(classId))
            {
                if (s.Party.Count <= 1) return Result.Fail("Someone has to go");
                s.Party.Remove(classId);
                return Result.Success("Moved to reserve");
            }
            if (s.Party.Count >= Progression.MaxPartySize) return Result.Fail($"Party is full ({Progression.MaxPartySize})");
            s.Party.Add(classId);
            return Result.Success("Joined the party");
        }
    }

    /// <summary>
    /// The one rule for whether a unit can ever be lost for good (GG-T114). Owner rule: "no one dies permanently,
    /// that's lame" — in the normal game nobody is ever lost; heroes, guests, recruits and dialogue-joined heroes fall,
    /// get revived or recover, and come back. A planned opt-in Ironman mode will switch this on; every "unit is lost"
    /// decision (the guest record in <see cref="SaveData.LostGuests"/>, the Summon Circle pool, the guest exclusion in
    /// <c>BattleFactory.Build</c>, the battle's fall announcement via <c>BattleSetup.Permadeath</c>) reads it here,
    /// so Ironman only has to change <see cref="Enabled"/>.
    /// </summary>
    public static class Permadeath
    {
        /// <summary>Always false today — no Ironman mode exists yet (BIBLE §29.1).</summary>
        public static bool Enabled(SaveData s) => false;

        /// <summary>Whether this class has been lost for good. Off → never, even for a save that recorded a loss
        /// under the old "lost forever" guest rule: those guests come back.</summary>
        public static bool IsLost(SaveData s, string classId) => Enabled(s) && s.LostGuests.Contains(classId);
    }

    public static class Summon
    {
        /// <summary>A paywall hook (§25.18, GG-T232): <see cref="Paywall.Cost"/>, no longer rising per recruit.</summary>
        public static int Price(ContentDb c, SaveData s) => Paywall.Cost(PaywallKind.Summon, s);

        /// <summary>The Summon Circle's gold pool: every hero class whose <see cref="ClassDef.Acquire"/> is Summon (GG-T1416).</summary>
        public static List<string> Ids(ContentDb c) =>
            c.Classes.Values.Where(d => d.Team == Team.Hero && d.Acquire == Acquire.Summon).Select(d => d.Id).ToList();

        public static List<ClassDef> Pool(ContentDb c, SaveData s) =>
            c.Classes.Values.Where(d => d.Team == Team.Hero && d.Acquire == Acquire.Summon && !Permadeath.IsLost(s, d.Id)).ToList();

        private static Result CanRecruit(ContentDb c, SaveData s, string classId)
        {
            var def = c.TryClass(classId);
            if (def == null || def.Team != Team.Hero || def.Acquire != Acquire.Summon) return Result.Fail("Not summonable");
            if (Permadeath.IsLost(s, classId)) return Result.Fail("Fell in battle for good");
            if (s.Hero(classId) != null) return Result.Fail("Already in your roster");
            if (Paywall.Pending(s, PaywallKind.Summon) != null) return Result.Fail("A summoning is already under way");
            if (s.Gold < Price(c, s)) return Result.Fail("Not enough gold");
            return Result.Success();
        }

        /// <summary>The instant path (bots, tests): pays and the recruit arrives at once.</summary>
        public static Result Recruit(ContentDb c, SaveData s, string classId)
        {
            var ok = CanRecruit(c, s, classId);
            if (!ok.Ok) return ok;
            s.Gold -= Price(c, s);
            return Arrive(c, s, classId);
        }

        /// <summary>The player's path: pays and starts the summoning job; <see cref="Paywall.Finish"/> brings the hero.</summary>
        public static Result BeginRecruit(ContentDb c, SaveData s, string classId, DateTime now, out PaywallJob job)
        {
            job = null;
            var ok = CanRecruit(c, s, classId);
            return ok.Ok ? Paywall.Start(s, PaywallKind.Summon, classId, now, out job) : ok;
        }

        internal static Result Arrive(ContentDb c, SaveData s, string classId)
        {
            if (c.TryClass(classId) == null || s.Hero(classId) != null) return Result.Fail("The summoning fizzles");
            s.Recruits++;
            Join(c, s, classId);
            return Result.Success($"{c.Class(classId).Name} joins you!");
        }

        /// <summary>Adds a hero to the roster (and the party, if there's room) at the roster's average experience —
        /// shared by the Summon Circle and dialogue's RecruitHero effect (GG-T117). No-op if already owned. It arrives with
        /// the Trust of <paramref name="route"/>.</summary>
        public static HeroSave Join(ContentDb c, SaveData s, string classId, JoinRoute route = JoinRoute.Hire)
        {
            var existing = s.Hero(classId);
            if (existing != null) return existing;
            var h = Progression.NewHero(c, classId);
            int avg = s.Roster.Count == 0 ? 0 : (int)s.Roster.Average(x => x.TotalXp);
            h.TotalXp = avg; // recruits arrive at the roster's average experience
            h.SkillPoints += Formulas.SkillPointsForLevelUp(1, Progression.Level(h));
            Progression.TrainTo(c, h); // trained to the level they arrive at (GG-T388)
            s.Roster.Add(h);
            Codex.UnlockHero(s, h); // GG-T929
            if (s.Party.Count < Progression.MaxPartySize) s.Party.Add(h.ClassId);
            Bonds.Joined(s, h.ClassId, route);
            return h;
        }

        /// <summary>Who a story JoinHero (GG-T118) brings into a save: <paramref name="preferred"/> if it's a hero the
        /// player doesn't have; else an unowned hero of <paramref name="role"/>; else one of a role the roster lacks
        /// (<see cref="StoryRoleOrder"/>); else anyone unowned — or null once every hero is owned. The fallbacks skip
        /// the Summon Circle's pool and the stages' guests so those stay worth having; deterministic (name order) so
        /// a beat's speaker and its joiner always agree. <paramref name="exclude"/> holds heroes already promised to
        /// earlier JoinHero effects of the same dialogue.</summary>
        public static string StoryPick(ContentDb c, SaveData s, string preferred, string role, ICollection<string> exclude = null)
        {
            bool Free(ClassDef d) => d != null && d.Team == Team.Hero && !d.IsCut && s.Hero(d.Id) == null && !Permadeath.IsLost(s, d.Id) && (exclude == null || !exclude.Contains(d.Id));
            if (preferred != null && Free(c.TryClass(preferred))) return preferred;
            var pool = c.Classes.Values.Where(d => Free(d) && d.Acquire != Acquire.Summon && d.Acquire != Acquire.Guest)
                .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase).ThenBy(d => d.Id, StringComparer.Ordinal).ToList();
            if (role != null && pool.FirstOrDefault(d => d.Role == role) is ClassDef byRole) return byRole.Id;
            var have = new HashSet<string>(s.Roster.Select(h => c.TryClass(h.ClassId)?.Role).Where(r => r != null));
            foreach (var r in StoryRoleOrder)
                if (!have.Contains(r) && pool.FirstOrDefault(d => d.Role == r) is ClassDef missing) return missing.Id;
            return pool.FirstOrDefault()?.Id;
        }

        /// <summary>The order a JoinHero with no usable class or role fills gaps in the roster.</summary>
        public static readonly string[] StoryRoleOrder = { "Healer", "Tank", "Mage", "Support", "Striker", "Ranged" };
    }
}
