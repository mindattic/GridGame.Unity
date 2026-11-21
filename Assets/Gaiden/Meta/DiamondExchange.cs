using System;
using System.Collections.Generic;
using GridGame.Core.Content;

namespace GridGame.Meta
{
    /// <summary>
    /// The Diamond exchange (§25.18.1, GG-T455). Owner, 2026-11-20: "Like guildwars gold and diamonds need an exchange rate so
    /// if something costs diamonds, it can be purchased with gold or with real money, right now lets make it 100 diamonds
    /// for $1.00 just to keep it low and honest". Every Diamond price is paid through <see cref="Pay"/>: Diamonds in the bag
    /// first, and any shortfall converted from gold at <see cref="GoldPerDiamond"/>, so anything priced in Diamonds can be
    /// bought with gold. Gold also buys Diamonds outright (<see cref="Buy"/>), and real money buys them through an
    /// <see cref="IDiamondStore"/> at <see cref="DiamondsPerDollar"/>. Every number here is the one tuning spot.
    /// </summary>
    public static class DiamondExchange
    {
        /// <summary>Gold for one Diamond. 100 Diamonds = 600 g, the old one-Diamond gold price, so a gold balance buys
        /// exactly what it did before the re-denomination.</summary>
        public const int GoldPerDiamond = 6;

        /// <summary>Diamonds per US dollar at the real-money store: flat, no bonus tiers (owner: "low and honest").</summary>
        public const int DiamondsPerDollar = 100;

        public static int Have(SaveData s) => s?.Count(Crafting.DiamondId) ?? 0;

        /// <summary>Gold worth of <paramref name="diamonds"/> Diamonds at the exchange rate.</summary>
        public static int GoldFor(int diamonds) => Math.Max(0, diamonds) * GoldPerDiamond;

        /// <summary>What paying <paramref name="price"/> Diamonds would take from this save: Diamonds first, then the
        /// shortfall in gold. Doesn't check that the gold is there (see <see cref="CanPay"/>).</summary>
        public static (int diamonds, int gold) Quote(SaveData s, int price)
        {
            price = Math.Max(0, price);
            int d = Math.Min(Have(s), price);
            return (d, GoldFor(price - d));
        }

        /// <summary>Whether the save can pay <paramref name="price"/> Diamonds, keeping <paramref name="keepGold"/> gold back
        /// for whatever else the same action costs (a lucky craft's recipe gold).</summary>
        public static Result CanPay(SaveData s, int price, int keepGold = 0)
        {
            if (s == null) return Result.Fail("No save");
            var (_, gold) = Quote(s, price);
            if (gold > 0 && s.Gold - Math.Max(0, keepGold) < gold)
            {
                int short_ = price - Have(s);
                return Result.Fail($"Not enough Diamonds: {short_} more, or {gold:N0} G to exchange");
            }
            return Result.Success();
        }

        /// <summary>Pays <paramref name="price"/> Diamonds: from the bag first, the rest exchanged from gold. Takes nothing
        /// when it fails.</summary>
        public static Result Pay(SaveData s, int price, out (int diamonds, int gold) paid, int keepGold = 0)
        {
            paid = (0, 0);
            var ok = CanPay(s, price, keepGold);
            if (!ok.Ok) return ok;
            paid = Quote(s, price);
            if (paid.diamonds > 0) s.AddItem(Crafting.DiamondId, -paid.diamonds);
            s.Gold -= paid.gold;
            return Result.Success();
        }

        public static Result Pay(SaveData s, int price, int keepGold = 0) => Pay(s, price, out _, keepGold);

        /// <summary>"25 Diamonds (or 150 G)": the label every Diamond-priced button shows.</summary>
        public static string Label(int price) => $"{price:N0} Diamond{(price == 1 ? "" : "s")} (or {GoldFor(price):N0} G)";

        /// <summary>The most Diamonds the bag can hold (the item's MaxStack).</summary>
        public static int Cap(ContentDb c) => c?.TryItem(Crafting.DiamondId)?.MaxStack ?? int.MaxValue;

        /// <summary>Room left in the bag for Diamonds.</summary>
        public static int Room(ContentDb c, SaveData s) => Math.Max(0, Cap(c) - Have(s));

        /// <summary>The most Diamonds this save's gold can buy right now (and the bag can hold).</summary>
        public static int MaxBuy(ContentDb c, SaveData s) => s == null ? 0 : Math.Min(Room(c, s), Math.Max(0, s.Gold) / GoldPerDiamond);

        /// <summary>Exchanges gold for <paramref name="diamonds"/> Diamonds, instantly, at <see cref="GoldPerDiamond"/>.</summary>
        public static Result Buy(ContentDb c, SaveData s, int diamonds)
        {
            if (s == null) return Result.Fail("No save");
            if (diamonds <= 0) return Result.Fail("Pick an amount");
            if (Room(c, s) < diamonds) return Result.Fail("Can't carry that many Diamonds");
            int gold = GoldFor(diamonds);
            if (s.Gold < gold) return Result.Fail($"Not enough gold: {gold:N0} G for {diamonds:N0} Diamonds");
            s.Gold -= gold;
            s.AddItem(Crafting.DiamondId, diamonds);
            return Result.Success($"Exchanged {gold:N0} G for {diamonds:N0} Diamonds.");
        }

        /// <summary>Adds Diamonds bought with real money (or a refund), up to the bag's cap.</summary>
        public static Result Grant(ContentDb c, SaveData s, int diamonds)
        {
            if (s == null) return Result.Fail("No save");
            if (diamonds <= 0) return Result.Fail("Nothing to add");
            if (Room(c, s) < diamonds) return Result.Fail("Can't carry that many Diamonds");
            s.AddItem(Crafting.DiamondId, diamonds);
            return Result.Success($"+{diamonds:N0} Diamonds.");
        }
    }

    /// <summary>One real-money Diamond pack. The price follows the flat rate (<see cref="DiamondExchange.DiamondsPerDollar"/>).</summary>
    public sealed class DiamondPack
    {
        public string ProductId;
        public int Diamonds;
        /// <summary>Price in US cents.</summary>
        public int Cents => (int)Math.Round(Diamonds * 100.0 / DiamondExchange.DiamondsPerDollar);
        public string Price => $"${Cents / 100}.{Cents % 100:00}";

        public static DiamondPack Of(int diamonds) => new DiamondPack { ProductId = $"diamonds_{diamonds}", Diamonds = diamonds };
    }

    /// <summary>How a real-money purchase ended (§25.18.1, GG-T468).</summary>
    public enum PurchaseKind
    {
        /// <summary>Paid and the Diamonds are in the bag.</summary>
        Granted,
        /// <summary>The store took the order but hasn't charged yet (a parent's approval, a cash payment still clearing).
        /// The Diamonds land on their own when it clears, even on a later launch.</summary>
        Pending,
        /// <summary>The player backed out of the store's sheet. Nothing charged.</summary>
        Cancelled,
        /// <summary>The store refused or failed (declined card, network). Nothing charged.</summary>
        Failed,
        /// <summary>No store to buy from (not connected, product missing, purchases disabled on the device).</summary>
        Unavailable,
    }

    /// <summary>One purchase's answer from an <see cref="IDiamondStore"/>.</summary>
    public sealed class PurchaseResult
    {
        public PurchaseKind Kind;
        public string Message;
        public string ProductId;
        /// <summary>Diamonds added to the bag (Granted only).</summary>
        public int Diamonds;

        public bool Ok => Kind == PurchaseKind.Granted;

        public static PurchaseResult Of(PurchaseKind kind, string message, string productId = null, int diamonds = 0) =>
            new PurchaseResult { Kind = kind, Message = message, ProductId = productId, Diamonds = diamonds };

        public Result ToResult() => Kind == PurchaseKind.Granted || Kind == PurchaseKind.Pending ? Result.Success(Message) : Result.Fail(Message);
    }

    /// <summary>
    /// Where real-money Diamonds come from (§25.18.1, GG-T455). The game talks only to this; the billing integration
    /// (Unity IAP over Google Play Billing / StoreKit, GG-T468) is an implementation wired in the App layer. Purchases are
    /// async in every real store, so the answer comes back through a callback, and a purchase can also complete outside any
    /// Purchase call (a deferred order approved later, an order left unfinished by a crash), which <see cref="Delivered"/>
    /// reports.
    /// </summary>
    public interface IDiamondStore
    {
        /// <summary>Shown on the store screen ("Test store: purchases are free", "Store not connected yet").</summary>
        string Status { get; }
        /// <summary>Whether a purchase can actually complete.</summary>
        bool Connected { get; }
        void Purchase(ContentDb c, SaveData s, DiamondPack pack, Action<PurchaseResult> done);
        /// <summary>A purchase that finished outside a <see cref="Purchase"/> call (the App toasts it and saves).</summary>
        event Action<PurchaseResult> Delivered;
    }

    public static class DiamondStore
    {
        /// <summary>The packs on sale: a flat rate, no bonus tiers (owner: "low and honest").</summary>
        public static readonly IReadOnlyList<DiamondPack> Packs = new[]
        {
            DiamondPack.Of(100), DiamondPack.Of(500), DiamondPack.Of(1000), DiamondPack.Of(2500),
        };

        public const string NotConnected = "Store not connected yet";
    }

    /// <summary>DEV / EDITOR ONLY: grants the pack at once, charges nothing. Never selected in a release build.</summary>
    public sealed class DevDiamondStore : IDiamondStore
    {
        public string Status => "Test store (dev build): purchases are free and land at once.";
        public bool Connected => true;
        public event Action<PurchaseResult> Delivered { add { } remove { } }

        public void Purchase(ContentDb c, SaveData s, DiamondPack pack, Action<PurchaseResult> done)
        {
            var r = pack == null ? Result.Fail("No such pack") : DiamondExchange.Grant(c, s, pack.Diamonds);
            done?.Invoke(r.Ok
                ? PurchaseResult.Of(PurchaseKind.Granted, $"[Test store] +{pack.Diamonds:N0} Diamonds, no charge.", pack.ProductId, pack.Diamonds)
                : PurchaseResult.Of(PurchaseKind.Failed, r.Message, pack?.ProductId));
        }
    }

    /// <summary>A release build with no billing on its platform (desktop, WebGL, or the IAP package missing): shows the
    /// packs, sells nothing.</summary>
    public sealed class UnconnectedDiamondStore : IDiamondStore
    {
        public string Status => DiamondStore.NotConnected + ". Diamonds can still be exchanged for gold.";
        public bool Connected => false;
        public event Action<PurchaseResult> Delivered { add { } remove { } }
        public void Purchase(ContentDb c, SaveData s, DiamondPack pack, Action<PurchaseResult> done) =>
            done?.Invoke(PurchaseResult.Of(PurchaseKind.Unavailable, DiamondStore.NotConnected, pack?.ProductId));
    }
}
