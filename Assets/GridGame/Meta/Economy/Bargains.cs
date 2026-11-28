using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Core.Rules;

namespace GridGame.Meta
{
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
}
