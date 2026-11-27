using System;
using System.Collections.Generic;
using GridGame.Core.Content;

namespace GridGame.Meta
{
    /// <summary>
    /// Whether paid random items may be sold here (BIBLE §25.10.4, GG-T1433): from the storefront country, a Shard-priced
    /// summon pull and the lucky craft are refused where paid loot boxes are not allowed (<see cref="Blocked"/>, by default
    /// Belgium and the Netherlands). "Paid" is anything priced in Shards (bought with money, or with gold at the exchange)
    /// and Summon Tickets from the account wallet (bought); Summon Tickets earned in play (<see cref="SummonState.Tickets"/>)
    /// are not paid, so a pull paid wholly in them stays allowed everywhere. The country is the store's
    /// (<see cref="Country"/>, set at store connect by GG-T1435); unknown means allowed. Engine-free.
    /// </summary>
    public static class LootBoxPolicy
    {
        /// <summary>ISO 3166-1 alpha-2 storefront countries where paid random items are off by default.</summary>
        public static readonly string[] DefaultBlocked = { "BE", "NL" };

        /// <summary>Storefront countries where paid random items are off; starts as <see cref="DefaultBlocked"/>.</summary>
        public static readonly HashSet<string> Blocked = new HashSet<string>(DefaultBlocked, StringComparer.OrdinalIgnoreCase);

        private static string country;

        /// <summary>The storefront's ISO 3166-1 alpha-2 country ("BE"), upper-cased; null or empty while unknown.</summary>
        public static string Country
        {
            get => country;
            set => country = string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
        }

        /// <summary>Paid random items are sold in <see cref="Country"/>: it is unknown or not <see cref="Blocked"/>.</summary>
        public static bool PaidAllowed => country == null || !Blocked.Contains(country);

        /// <summary>Why paid pulls are off, for the Summon screen and a refused pull.</summary>
        public static string OffLine =>
            $"Paid summons aren't sold in your store's country ({country}). Pulls here take only {TicketText.Plural} earned in play.";

        /// <summary>Why the lucky craft is off, for the Forge and a refused craft.</summary>
        public static string LuckyOffLine => $"The lucky craft isn't sold in your store's country ({country}).";

        /// <summary>A x<paramref name="count"/> pull on <paramref name="banner"/>: allowed where paid pulls are, else only
        /// when earned Tickets pay for every pull in it (they are spent before bought ones, <see cref="SummonTickets.Spend"/>).</summary>
        public static Result Allows(SaveData s, BannerDef banner, int count)
        {
            if (PaidAllowed) return Result.Success();
            bool earned = banner != null && banner.AcceptsTickets && banner.PullTickets > 0
                && (s?.Summon?.Tickets ?? 0) >= count * banner.PullTickets;
            return earned ? Result.Success() : Result.Fail(OffLine);
        }

        /// <summary>The old one-Ticket random draw (<see cref="DiamondSummon"/>): as <see cref="Allows"/>, one earned Ticket.</summary>
        public static Result AllowsDraw(SaveData s) =>
            PaidAllowed || (s?.Summon?.Tickets ?? 0) > 0 ? Result.Success() : Result.Fail(OffLine);

        /// <summary>The lucky craft is always Shard-priced, so it is sold only where paid random items are.</summary>
        public static Result AllowsLuckyCraft() => PaidAllowed ? Result.Success() : Result.Fail(LuckyOffLine);
    }
}
