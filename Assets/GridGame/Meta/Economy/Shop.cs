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
}
