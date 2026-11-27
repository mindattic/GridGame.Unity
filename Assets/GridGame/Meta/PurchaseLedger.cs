using System;
using System.Collections.Generic;
using System.Linq;

namespace GridGame.Meta
{
    /// <summary>The save's record of real-money purchases (§25.18.1, GG-T469): every store transaction id already granted.</summary>
    public sealed class PurchaseSave
    {
        /// <summary>Store transaction id (Google Play's purchase token, Apple's transaction id) → what it granted.</summary>
        public Dictionary<string, PurchaseRecord> Granted = new Dictionary<string, PurchaseRecord>(StringComparer.Ordinal);
    }

    public sealed class PurchaseRecord
    {
        public string ProductId;
        public int Diamonds;
        /// <summary>GG-T1447: Summon Tickets it granted.</summary>
        public int Tickets;
        /// <summary>GG-T1447: cosmetic ids it newly granted (taken back on a refund).</summary>
        public string[] Cosmetics;
        /// <summary>GG-T1447: it made the account the owner of its non-consumable product.</summary>
        public bool Ownership;
        public DateTime Utc;
        /// <summary>"GooglePlay", "AppleAppStore", … (informational).</summary>
        public string Store;
        /// <summary>The store reported the purchase refunded or revoked.</summary>
        public bool Refunded;
        /// <summary>Diamonds taken back for the refund (never more than the bag held then).</summary>
        public int ClawedBack;
    }

    public enum GrantOutcome
    {
        /// <summary>The Diamonds were added and the transaction recorded.</summary>
        Granted,
        /// <summary>This transaction was granted before: nothing added (the store re-delivered it). Finish it again.</summary>
        AlreadyGranted,
        /// <summary>Not one of <see cref="DiamondStore.Packs"/>. Nothing added.</summary>
        UnknownProduct,
        /// <summary>No save or no transaction id. Nothing added; leave the order unfinished so it comes back.</summary>
        Invalid,
    }

    /// <summary>
    /// Grants each real-money transaction exactly once (§25.18.1, GG-T469). A store re-delivers an order until the game
    /// finishes it, so a crash between the grant and the store's confirm, a deferred order approved later, or a second
    /// device all reach <see cref="Grant"/> with a transaction id it may have seen: the ledger in the save is what keeps
    /// them from paying out twice. The App writes the save after a grant and only then tells the store the order is done.
    /// </summary>
    public static class PurchaseLedger
    {
        private static PurchaseSave Book(SaveData s)
        {
            s.Purchases ??= new PurchaseSave();
            s.Purchases.Granted ??= new Dictionary<string, PurchaseRecord>(StringComparer.Ordinal);
            return s.Purchases;
        }

        public static bool Has(SaveData s, string transactionId) =>
            s != null && !string.IsNullOrEmpty(transactionId) && s.Purchases?.Granted != null && s.Purchases.Granted.ContainsKey(transactionId);

        /// <summary>Whether the save file at <paramref name="path"/> records <paramref name="transactionId"/> as granted. Strictly
        /// read-only: parses the JSON without deserializing, migrating or repairing anything, and never writes; a missing or
        /// unreadable file is simply false. Used to spot an order already granted in another slot or profile.</summary>
        public static bool FileHas(string path, string transactionId)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(transactionId)) return false;
            try
            {
                if (!System.IO.File.Exists(path)) return false;
                var root = Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText(path));
                return root["Purchases"]?["Granted"] is Newtonsoft.Json.Linq.JObject granted && granted[transactionId] != null;
            }
            catch (Exception) { return false; }
        }

        public static DiamondPack PackOf(string productId) => DiamondStore.Packs.FirstOrDefault(p => p.ProductId == productId);

        /// <summary>Grants <paramref name="productId"/>'s Diamonds for <paramref name="transactionId"/> unless it was granted
        /// before. Paid Diamonds ignore the bag's cap: the money was taken, so they always land.</summary>
        public static GrantOutcome Grant(SaveData s, string transactionId, string productId, DateTime utc, string store, out int diamonds)
        {
            diamonds = 0;
            if (s == null || string.IsNullOrEmpty(transactionId)) return GrantOutcome.Invalid;
            if (Has(s, transactionId)) return GrantOutcome.AlreadyGranted;
            var pack = PackOf(productId);
            if (pack == null) return GrantOutcome.UnknownProduct;
            s.AddItem(Crafting.DiamondId, pack.Diamonds);
            Book(s).Granted[transactionId] = new PurchaseRecord { ProductId = productId, Diamonds = pack.Diamonds, Utc = utc, Store = store };
            diamonds = pack.Diamonds;
            return GrantOutcome.Granted;
        }

        /// <summary>A granted purchase was refunded: takes its Diamonds back, but only what's still in the bag (never
        /// below zero, never touching gold: §25.18's forgiving paywall). Idempotent; an unknown transaction does nothing.
        /// Returns the Diamonds taken.</summary>
        public static int Refund(SaveData s, string transactionId)
        {
            if (!Has(s, transactionId)) return 0;
            var r = s.Purchases.Granted[transactionId];
            if (r.Refunded) return 0;
            int take = Math.Min(Math.Max(0, r.Diamonds), DiamondExchange.Have(s));
            if (take > 0) s.AddItem(Crafting.DiamondId, -take);
            r.Refunded = true;
            r.ClawedBack = take;
            return take;
        }

        /// <summary>A refund reported by product only (Apple's revocation callback names the product, not the transaction):
        /// refunds this save's most recent unrefunded purchase of it. Returns the Diamonds taken (0 when there is none).</summary>
        public static int RefundLatest(SaveData s, string productId)
        {
            var hit = s?.Purchases?.Granted?
                .Where(kv => kv.Value.ProductId == productId && !kv.Value.Refunded)
                .OrderByDescending(kv => kv.Value.Utc)
                .Select(kv => kv.Key)
                .FirstOrDefault();
            return hit == null ? 0 : Refund(s, hit);
        }

        /// <summary>Diamonds bought with real money on this save, refunds excluded.</summary>
        public static int TotalBought(SaveData s) =>
            s?.Purchases?.Granted?.Values.Where(r => !r.Refunded).Sum(r => r.Diamonds) ?? 0;

        /// <summary>GG-T1437: real money spent in <paramref name="utc"/>'s calendar month (UTC), in US cents at the list
        /// price, refunds excluded. Feeds the monthly spending limit (<see cref="SpendLimits"/>).</summary>
        public static int MonthCents(IEnumerable<PurchaseRecord> records, DateTime utc) =>
            records?.Where(r => r != null && !r.Refunded && r.Utc.Year == utc.Year && r.Utc.Month == utc.Month)
                .Sum(r => StoreCatalog.Of(r.ProductId)?.Cents ?? 0) ?? 0;
    }
}
