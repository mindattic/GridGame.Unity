using System;
using System.Collections.Generic;
using System.Linq;

namespace GridGame.Meta
{
    /// <summary>How a store sells a product: a consumable can be bought again and again, a non-consumable is owned once
    /// and restores on a new device.</summary>
    public enum ProductType
    {
        Consumable,
        NonConsumable,
    }

    /// <summary>One real-money product (GG-T1447): its store id, type, what it grants and how many times one account may
    /// buy it. Prices are in US cents at the list price; the store shows its own localized price.</summary>
    public sealed class StoreProduct
    {
        public string Id;
        public string Name;
        public ProductType Type;
        /// <summary>Price in US cents.</summary>
        public int Cents;
        /// <summary>Shards (Diamonds) paid into the account wallet.</summary>
        public int Shards;
        /// <summary>Summon Tickets paid into the account wallet.</summary>
        public int Tickets;
        /// <summary>Cosmetic ids the account owns once it is granted.</summary>
        public string[] Cosmetics = Array.Empty<string>();
        /// <summary>How many times one account may buy it; 0 is no limit. A non-consumable is always 1.</summary>
        public int AccountLimit;

        public string Price => $"${Cents / 100}.{Cents % 100:00}";

        /// <summary>The flat-rate Shard pack as a catalogue product.</summary>
        public static StoreProduct Of(DiamondPack pack) => new StoreProduct
        {
            Id = pack.ProductId,
            Name = ShardText.Of(pack.Diamonds),
            Type = ProductType.Consumable,
            Cents = pack.Cents,
            Shards = pack.Diamonds,
        };
    }

    /// <summary>
    /// Every real-money product (GG-T1446/GG-T1447, docs/design/monetization): the Shard packs at the flat rate, the starter
    /// offer, the season pass and the cosmetic bundles. <see cref="AccountWallets.Grant"/> pays each transaction out from
    /// here exactly once. Offer, pass and cosmetic prices are placeholders until the owner sets them (GG-T1445).
    /// </summary>
    public static class StoreCatalog
    {
        public const string StarterPackId = "starter_pack";
        public const string SeasonPassS1Id = "season_pass_s1";

        public static readonly IReadOnlyList<StoreProduct> Products = DiamondStore.Packs.Select(StoreProduct.Of).Concat(new[]
        {
            new StoreProduct
            {
                Id = StarterPackId, Name = "Starter pack", Type = ProductType.Consumable, Cents = 299,
                Shards = 300, Tickets = 5, Cosmetics = new[] { "frame_starter" }, AccountLimit = 1,
            },
            new StoreProduct
            {
                Id = SeasonPassS1Id, Name = "Season pass premium, season 1", Type = ProductType.NonConsumable, Cents = 499, AccountLimit = 1,
            },
            new StoreProduct
            {
                Id = "cosmetic_frames_1", Name = "Unit frames bundle", Type = ProductType.NonConsumable, Cents = 199, AccountLimit = 1,
                Cosmetics = new[] { "frame_set1_1", "frame_set1_2", "frame_set1_3" },
            },
            new StoreProduct
            {
                Id = "cosmetic_themes_1", Name = "Window themes bundle", Type = ProductType.NonConsumable, Cents = 299, AccountLimit = 1,
                Cosmetics = new[] { "theme_set1_1", "theme_set1_2" },
            },
            new StoreProduct
            {
                Id = "cosmetic_backdrops_1", Name = "Hub backdrops bundle", Type = ProductType.NonConsumable, Cents = 199, AccountLimit = 1,
                Cosmetics = new[] { "backdrop_set1_1", "backdrop_set1_2" },
            },
        }).ToArray();

        public static StoreProduct Of(string productId) =>
            string.IsNullOrEmpty(productId) ? null : Products.FirstOrDefault(p => p.Id == productId);

        /// <summary>How many of <paramref name="p"/> this wallet bought, refunds excluded.</summary>
        public static int Bought(AccountWallet w, StoreProduct p) =>
            p == null ? 0 : w?.Granted?.Values.Count(r => r != null && !r.Refunded && r.ProductId == p.Id) ?? 0;

        /// <summary>How many more of <paramref name="p"/> this wallet may buy (<see cref="int.MaxValue"/> with no limit).</summary>
        public static int Remaining(AccountWallet w, StoreProduct p)
        {
            if (p == null) return 0;
            int limit = p.Type == ProductType.NonConsumable ? 1 : p.AccountLimit;
            if (limit <= 0) return int.MaxValue;
            if (p.Type == ProductType.NonConsumable && Owns(w, p.Id)) return 0;
            return Math.Max(0, limit - Bought(w, p));
        }

        public static bool CanBuy(AccountWallet w, StoreProduct p) => Remaining(w, p) > 0;

        /// <summary>Whether this wallet owns the non-consumable <paramref name="productId"/>.</summary>
        public static bool Owns(AccountWallet w, string productId) =>
            w?.Owned != null && !string.IsNullOrEmpty(productId) && w.Owned.Contains(productId);

        /// <summary>Whether this wallet owns the cosmetic <paramref name="cosmeticId"/>.</summary>
        public static bool OwnsCosmetic(AccountWallet w, string cosmeticId) =>
            w?.Cosmetics != null && !string.IsNullOrEmpty(cosmeticId) && w.Cosmetics.Contains(cosmeticId);
    }
}
