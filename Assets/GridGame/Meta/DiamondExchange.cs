using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Content;

namespace GridGame.Meta
{
    /// <summary>
    /// The premium currency's player-facing name (§25.18.1, GG-T568 then GG-T675; owner 2026-11-22: the real-money currency
    /// is "Mana Shard(s)", the stable form of mana that doesn't dissipate; it was "Aetherite Shard(s)", before that
    /// "Diamonds"). Every screen, toast and confirm reads its words from here. The internal ids keep the old words so saves
    /// and purchases still work: the item <c>mat_diamond</c>, the store product ids <c>diamonds_100</c> …, the glossary id
    /// <c>aetherite_shard</c>, the save fields and the <c>Diamond*</c> class names.
    /// </summary>
    public static class ShardText
    {
        /// <summary>The currency's stable wiki/glossary id.</summary>
        public const string Id = "aetherite_shard";
        public const string Name = "Mana Shard", Plural = "Mana Shards";
        /// <summary>The short form for tight labels (buttons, side windows, counters).</summary>
        public const string Short = "Shard", ShortPlural = "Shards";
        /// <summary>The real-money store's name (Camp row, screen title, every "not enough Shards" offer).</summary>
        public const string Store = "Shard Store";

        /// <summary>"1 Mana Shard", "250 Mana Shards".</summary>
        public static string Of(int n) => $"{n:N0} {(n == 1 ? Name : Plural)}";
        /// <summary>"1 Shard", "250 Shards".</summary>
        public static string ShortOf(int n) => $"{n:N0} {(n == 1 ? Short : ShortPlural)}";
    }

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

        /// <summary>GG-T702: the account wallet of the signed-in store login (<see cref="AccountWallet"/>, §25.18.1), bound by
        /// the App at boot. Every save moved into it (<see cref="WalletMigration.IsMoved"/>) reads, spends and earns its
        /// Diamonds here instead of in its bag; a save that isn't moved yet, or any save while none is bound (the tests, a
        /// sim), keeps using the bag.</summary>
        public static AccountWallet Wallet { get; set; }

        /// <summary>The bound wallet changed since it was last written. The App writes the wallet before the save (a save
        /// written without its wallet could hold what a spend bought while the wallet still holds the Diamonds) and clears
        /// this once the write held.</summary>
        public static bool WalletDirty { get; set; }

        /// <summary>The wallet <paramref name="s"/>'s Diamonds live in, or null when they're still in its bag.</summary>
        public static AccountWallet WalletOf(SaveData s) => Wallet != null && WalletMigration.IsMoved(s) ? Wallet : null;

        /// <summary>The Diamonds in the save's own bag, wallet or not (what <see cref="WalletMigration"/> drains).</summary>
        public static int Bag(SaveData s) => s?.Count(Crafting.DiamondId) ?? 0;

        /// <summary>The Diamonds this save can spend: its wallet's when it was moved into the bound one, else its bag's.</summary>
        public static int Have(SaveData s) => WalletOf(s) is AccountWallet w ? Math.Max(0, w.Diamonds) : Bag(s);

        /// <summary>Adds (positive) or takes (negative, never below zero) Diamonds where this save keeps them: the bound
        /// wallet (marked dirty for the App to write first) or the bag. Callers check the balance and the room first.</summary>
        public static void Add(SaveData s, int diamonds)
        {
            if (s == null || diamonds == 0) return;
            var w = WalletOf(s);
            if (w == null) { s.AddItem(Crafting.DiamondId, diamonds); return; }
            if (diamonds > 0) AccountWallets.Add(w, diamonds);
            else w.Diamonds = Math.Max(0, Math.Max(0, w.Diamonds) + diamonds);
            w.UpdatedUtc = DateTime.UtcNow;
            WalletDirty = true;
        }

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
                return Result.Fail($"Not enough {ShardText.Plural}: {short_:N0} more, or {gold:N0} G to exchange");
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
            if (paid.diamonds > 0) Add(s, -paid.diamonds);
            s.Gold -= paid.gold;
            return Result.Success();
        }

        public static Result Pay(SaveData s, int price, int keepGold = 0) => Pay(s, price, out _, keepGold);

        /// <summary>"25 Shards (or 150 G)": the label every Shard-priced button shows (GG-T568: the short name fits a button).</summary>
        public static string Label(int price) => $"{ShardText.ShortOf(price)} (or {GoldFor(price):N0} G)";

        /// <summary>The most Diamonds the bag can hold (the item's MaxStack).</summary>
        public static int Cap(ContentDb c) => c?.TryItem(Crafting.DiamondId)?.MaxStack ?? int.MaxValue;

        /// <summary>Room left for Diamonds: the bag's cap, or (moved into the wallet, GG-T702) no cap at all.</summary>
        public static int Room(ContentDb c, SaveData s) => WalletOf(s) != null ? int.MaxValue - Have(s) : Math.Max(0, Cap(c) - Have(s));

        /// <summary>The most Diamonds this save's gold can buy right now (and the bag can hold).</summary>
        public static int MaxBuy(ContentDb c, SaveData s) => s == null ? 0 : Math.Min(Room(c, s), Math.Max(0, s.Gold) / GoldPerDiamond);

        /// <summary>Exchanges gold for <paramref name="diamonds"/> Diamonds, instantly, at <see cref="GoldPerDiamond"/>.</summary>
        public static Result Buy(ContentDb c, SaveData s, int diamonds)
        {
            if (s == null) return Result.Fail("No save");
            if (diamonds <= 0) return Result.Fail("Pick an amount");
            if (Room(c, s) < diamonds) return Result.Fail($"Can't hold that many {ShardText.Plural}");
            int gold = GoldFor(diamonds);
            if (s.Gold < gold) return Result.Fail($"Not enough gold: {gold:N0} G for {ShardText.Of(diamonds)}");
            s.Gold -= gold;
            Add(s, diamonds);
            return Result.Success($"Exchanged {gold:N0} G for {ShardText.Of(diamonds)}.");
        }

        /// <summary>Adds Diamonds bought with real money (or a refund), up to the bag's cap.</summary>
        public static Result Grant(ContentDb c, SaveData s, int diamonds)
        {
            if (s == null) return Result.Fail("No save");
            if (diamonds <= 0) return Result.Fail("Nothing to add");
            if (Room(c, s) < diamonds) return Result.Fail($"Can't hold that many {ShardText.Plural}");
            Add(s, diamonds);
            return Result.Success($"+{ShardText.Of(diamonds)}.");
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
        /// <summary>GG-T1574: buys any <see cref="StoreCatalog"/> product (a pack, the starter offer, the season pass, a
        /// cosmetic bundle). Refused, with nothing charged, when the account can't buy it again
        /// (<see cref="StoreCatalog.CanBuy"/>).</summary>
        void Purchase(ContentDb c, SaveData s, StoreProduct product, Action<PurchaseResult> done);
        /// <summary>The pack's price as the store shows it, in the player's currency ("₩1,400", "1,09 €"), or null when the
        /// store hasn't said (the screen falls back to <see cref="DiamondPack.Price"/>, the US-dollar list price). GG-T470.</summary>
        string PriceOf(DiamondPack pack);
        /// <summary>The product's localized store price, or null (the screen falls back to <see cref="StoreProduct.Price"/>).</summary>
        string PriceOf(StoreProduct product);
        /// <summary>A purchase that finished outside a <see cref="Purchase"/> call (the App toasts it and saves).</summary>
        event Action<PurchaseResult> Delivered;
        /// <summary>The platform offers a "Restore purchases" action (GG-T469: Apple's restore re-sends unfinished
        /// transactions; consumables already finished are never restorable on any store).</summary>
        bool CanRestore { get; }
        /// <summary>Asks the store to re-deliver anything unfinished; each one arrives through <see cref="Delivered"/>
        /// (granted once, by the ledger). <paramref name="done"/> says whether the store answered.</summary>
        void Restore(Action<PurchaseResult> done);
    }

    public static class DiamondStore
    {
        /// <summary>The packs on sale: a flat rate, no bonus tiers (owner: "low and honest").</summary>
        public static readonly IReadOnlyList<DiamondPack> Packs = new[]
        {
            DiamondPack.Of(100), DiamondPack.Of(500), DiamondPack.Of(1000), DiamondPack.Of(2500),
        };

        public const string NotConnected = "Store not connected yet";

        /// <summary>Whether <paramref name="productId"/> is one of the Shard packs (the only products a save's own ledger
        /// can book; everything else lands in the account wallet).</summary>
        public static bool IsPack(string productId) => Packs.Any(p => p.ProductId == productId);

        /// <summary>Why this account can't buy <paramref name="p"/>, or null when it can.</summary>
        public static string CantBuy(AccountWallet w, StoreProduct p) =>
            p == null ? "No such product"
            : StoreCatalog.CanBuy(w, p) ? null
            : p.Type == ProductType.NonConsumable ? $"{p.Name} is already yours."
            : $"{p.Name} is limited to {p.AccountLimit:N0} per account.";

        /// <summary>What a granted purchase added, for its toast: "+300 Mana Shards, +5 Summon Tickets, 1 new cosmetic",
        /// "Unit frames bundle unlocked".</summary>
        public static string Gains(StoreProduct p, PurchaseRecord r)
        {
            var parts = new List<string>();
            if (r != null && r.Diamonds > 0) parts.Add("+" + ShardText.Of(r.Diamonds));
            if (r != null && r.Tickets > 0) parts.Add($"+{r.Tickets:N0} Summon Ticket{(r.Tickets == 1 ? "" : "s")}");
            if (p?.Type == ProductType.NonConsumable) parts.Add($"{p.Name} unlocked");
            else if (r?.Cosmetics?.Length > 0) parts.Add(r.Cosmetics.Length == 1 ? "1 new cosmetic" : $"{r.Cosmetics.Length} new cosmetics");
            return parts.Count > 0 ? string.Join(", ", parts) : $"{p?.Name ?? "Purchase"} delivered";
        }
    }

    /// <summary>DEV / EDITOR ONLY: grants the pack at once, charges nothing. Never selected in a release build.</summary>
    public sealed class DevDiamondStore : IDiamondStore
    {
        public string Status => "Test store (dev build): purchases are free and land at once.";
        public bool Connected => true;
        public event Action<PurchaseResult> Delivered { add { } remove { } }
        public bool CanRestore => false;
        public void Restore(Action<PurchaseResult> done) => done?.Invoke(PurchaseResult.Of(PurchaseKind.Unavailable, "Nothing to restore in the test store."));
        public string PriceOf(DiamondPack pack) => null;
        public string PriceOf(StoreProduct product) => null;

        public void Purchase(ContentDb c, SaveData s, DiamondPack pack, Action<PurchaseResult> done)
        {
            var r = pack == null ? Result.Fail("No such pack") : DiamondExchange.Grant(c, s, pack.Diamonds);
            done?.Invoke(r.Ok
                ? PurchaseResult.Of(PurchaseKind.Granted, $"[Test store] +{ShardText.Of(pack.Diamonds)}, no charge.", pack.ProductId, pack.Diamonds)
                : PurchaseResult.Of(PurchaseKind.Failed, r.Message, pack?.ProductId));
        }

        /// <summary>A pack lands where the save keeps its Shards; any other product is granted into the bound account wallet
        /// under a dev transaction id, exactly as a paid order would be.</summary>
        public void Purchase(ContentDb c, SaveData s, StoreProduct product, Action<PurchaseResult> done)
        {
            var w = DiamondExchange.Wallet;
            if (DiamondStore.CantBuy(w, product) is string no) { done?.Invoke(PurchaseResult.Of(PurchaseKind.Unavailable, no, product?.Id)); return; }
            if (DiamondStore.IsPack(product.Id)) { Purchase(c, s, DiamondStore.Packs.First(p => p.ProductId == product.Id), done); return; }
            if (w == null) { done?.Invoke(PurchaseResult.Of(PurchaseKind.Unavailable, $"{product.Name} needs a store account wallet, and none is loaded.", product.Id)); return; }
            string tx = "dev-" + Guid.NewGuid().ToString("N");
            if (AccountWallets.Grant(w, tx, product.Id, DateTime.UtcNow, "Dev", out int diamonds) != GrantOutcome.Granted)
            {
                done?.Invoke(PurchaseResult.Of(PurchaseKind.Failed, $"{product.Name} couldn't be granted.", product.Id));
                return;
            }
            DiamondExchange.WalletDirty = true;
            done?.Invoke(PurchaseResult.Of(PurchaseKind.Granted, $"[Test store] {DiamondStore.Gains(product, w.Granted[tx])}, no charge.", product.Id, diamonds));
        }
    }

    /// <summary>A release build with no billing on its platform (desktop, WebGL, or the IAP package missing): shows the
    /// packs, sells nothing.</summary>
    public sealed class UnconnectedDiamondStore : IDiamondStore
    {
        public string Status => DiamondStore.NotConnected + ". " + ShardText.Plural + " can still be exchanged for gold.";
        public bool Connected => false;
        public event Action<PurchaseResult> Delivered { add { } remove { } }
        public bool CanRestore => false;
        public void Restore(Action<PurchaseResult> done) => done?.Invoke(PurchaseResult.Of(PurchaseKind.Unavailable, DiamondStore.NotConnected));
        public string PriceOf(DiamondPack pack) => null;
        public string PriceOf(StoreProduct product) => null;
        public void Purchase(ContentDb c, SaveData s, DiamondPack pack, Action<PurchaseResult> done) =>
            done?.Invoke(PurchaseResult.Of(PurchaseKind.Unavailable, DiamondStore.NotConnected, pack?.ProductId));
        public void Purchase(ContentDb c, SaveData s, StoreProduct product, Action<PurchaseResult> done) =>
            done?.Invoke(PurchaseResult.Of(PurchaseKind.Unavailable, DiamondStore.NotConnected, product?.Id));
    }
}
