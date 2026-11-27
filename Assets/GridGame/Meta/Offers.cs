using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Content;

namespace GridGame.Meta
{
    /// <summary>
    /// The Shop's offers (GG-T1450, offers.json, docs/design/monetization): each sells for real money as a
    /// <see cref="StoreCatalog"/> product or for Shards, up to a per-account limit, once the save has cleared
    /// <see cref="OfferDef.ShowsAfter"/> campaign stages and until its end date. Claims are counted on the account wallet.
    /// Pure: callers persist the wallet with <see cref="AccountWalletStore.Save"/>.
    /// </summary>
    public static class Offers
    {
        public static bool ForMoney(OfferDef o) => o != null && !string.IsNullOrEmpty(o.ProductId);

        public static int Claimed(AccountWallet w, OfferDef o) =>
            o?.Id != null && w?.OfferClaims != null && w.OfferClaims.TryGetValue(o.Id, out var n) ? n : 0;

        /// <summary>How many more times this wallet may claim <paramref name="o"/> (<see cref="int.MaxValue"/> with no limit).</summary>
        public static int Remaining(AccountWallet w, OfferDef o)
        {
            if (o == null) return 0;
            int left = o.AccountLimit <= 0 ? int.MaxValue : Math.Max(0, o.AccountLimit - Claimed(w, o));
            return ForMoney(o) ? Math.Min(left, StoreCatalog.Remaining(w, StoreCatalog.Of(o.ProductId))) : left;
        }

        /// <summary>Whether <paramref name="o"/> shows in the Shop for this save and wallet at <paramref name="utc"/>.</summary>
        public static bool Available(OfferDef o, AccountWallet w, SaveData s, DateTime utc) =>
            o != null && s != null && (o.EndUtc == null || utc < o.EndUtc.Value)
            && (s.TestMode || s.HighestClearedStage + 1 >= o.ShowsAfter) && Remaining(w, o) > 0;

        /// <summary>Every offer in file order that <see cref="Available"/> shows.</summary>
        public static List<OfferDef> AvailableIn(ContentDb c, AccountWallet w, SaveData s, DateTime utc) =>
            c?.OfferOrder.Where(o => Available(o, w, s, utc)).ToList() ?? new List<OfferDef>();

        /// <summary>Buys the Shard offer <paramref name="o"/>: takes its price from the wallet and pays its contents, all
        /// or nothing. False (nothing changed) for a real-money offer, one not available, or a wallet short of Shards.</summary>
        public static bool BuyWithShards(OfferDef o, AccountWallet w, SaveData s, DateTime utc)
        {
            if (o == null || w == null || ForMoney(o) || o.ShardPrice <= 0 || !Available(o, w, s, utc)) return false;
            w.Normalize();
            if (!AccountWallets.Spend(w, o.ShardPrice)) return false;
            AccountWallets.Add(w, o.Shards);
            w.Tickets = (int)Math.Min(int.MaxValue, (long)w.Tickets + Math.Max(0, o.Tickets));
            foreach (var c in o.Cosmetics ?? new List<string>())
                if (!string.IsNullOrEmpty(c)) w.Cosmetics.Add(c);
            Claim(w, o, utc);
            return true;
        }

        /// <summary>A store transaction for <paramref name="productId"/> was granted (<see cref="AccountWallets.Grant"/>
        /// answered Granted, which paid the contents): counts the claim on the offer that sells it. Returns that offer, or
        /// null when no offer sells the product.</summary>
        public static OfferDef ClaimOnGrant(ContentDb c, AccountWallet w, string productId, DateTime utc)
        {
            if (c == null || w == null || string.IsNullOrEmpty(productId)) return null;
            var o = c.OfferOrder.FirstOrDefault(x => x != null && x.ProductId == productId);
            if (o == null) return null;
            w.Normalize();
            Claim(w, o, utc);
            return o;
        }

        private static void Claim(AccountWallet w, OfferDef o, DateTime utc)
        {
            w.OfferClaims[o.Id] = Claimed(w, o) + 1;
            w.UpdatedUtc = utc;
        }

        /// <summary>Every problem with offers.json against the store catalogue (empty = valid): each offer has a name and
        /// exactly one of a product id or a Shard price, a product id is a catalogue product whose contents it matches,
        /// and nothing is negative.</summary>
        public static List<string> Validate(ContentDb c)
        {
            var p = new List<string>();
            foreach (var o in c?.OfferOrder ?? new List<OfferDef>())
            {
                string at = $"offer {o.Id}";
                if (string.IsNullOrWhiteSpace(o.Name)) p.Add($"{at}: needs a Name");
                if (ForMoney(o) == o.ShardPrice > 0) p.Add($"{at}: needs exactly one of ProductId or ShardPrice");
                if (o.ShardPrice < 0 || o.Shards < 0 || o.Tickets < 0 || o.AccountLimit < 0 || o.ShowsAfter < 0) p.Add($"{at}: negative number");
                if (!ForMoney(o)) continue;
                var product = StoreCatalog.Of(o.ProductId);
                if (product == null) { p.Add($"{at}: unknown product '{o.ProductId}'"); continue; }
                if (product.Shards != o.Shards || product.Tickets != o.Tickets
                    || !product.Cosmetics.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual((o.Cosmetics ?? new List<string>()).OrderBy(x => x, StringComparer.Ordinal)))
                    p.Add($"{at}: contents differ from product '{o.ProductId}'");
            }
            return p;
        }
    }
}
