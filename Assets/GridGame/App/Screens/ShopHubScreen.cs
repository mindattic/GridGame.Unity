using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Content;
using GridGame.Meta;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>
    /// The Shop (GG-T1637): the nav Shop tab's one page, with the Merchant, the Shard Store and the Offers as tabs. The
    /// Merchant and the Shard Store are hosted, not copied: one instance of each lives as long as the hub, so carts, the
    /// exchange amount and the purchase status survive a tab switch. The Merchant's tab is locked until he joins.
    /// </summary>
    public sealed class ShopHubScreen : HubPage
    {
        private const int MerchantTab = 0, ShardsTab = 1, OffersTab = 2;
        private static readonly string[] TabNames = { "Merchant", ShardText.Plural, "Offers" };

        internal readonly ShopScreen Merchant = new ShopScreen();
        internal readonly DiamondStoreScreen Shards = new DiamondStoreScreen();
        private int tab = -1;

        /// <summary>The Shop opened on its Shard Store tab: the Camp's currency strip goes here (GG-T1963).</summary>
        internal static ShopHubScreen OnShards() => new ShopHubScreen { tab = ShardsTab };

        public override string Id => "shop-hub";
        protected override string Title => "Shop";
        protected override string Backdrop => tab == MerchantTab ? base.Backdrop : "cityruins";
        protected override string Npc => tab == MerchantTab ? CampNpcs.Merchant : null;
        protected override string TipStation => tab == MerchantTab ? Merchant.Id : tab == ShardsTab ? Shards.Id : Id;
        protected override Action ShardsTap => tab == ShardsTab ? null : () => Pick(ShardsTab);

        private bool MerchantOpen => CampUnlocks.NpcOpen(S, CampNpcs.Merchant);

        protected override void Build(VisualElement root)
        {
            if (tab < 0 || (tab == MerchantTab && !MerchantOpen)) tab = MerchantOpen ? MerchantTab : ShardsTab;
            base.Build(root);
        }

        protected override void BuildBody(VisualElement body)
        {
            Ui.Tabs(TabNames, tab, Pick, body, i => i == MerchantTab && !MerchantOpen,
                _ => App.Toast(CampUnlocks.JoinHint(C, CampNpcs.Merchant)));
            switch (tab)
            {
                case MerchantTab: Merchant.BuildAsTab(this, body); break;
                case ShardsTab: Shards.BuildAsTab(this, body); break;
                default: BuildOffers(body); break;
            }
        }

        private void Pick(int i)
        {
            if (i == tab) return;
            tab = i;
            Refresh();
        }

        /// <summary>GG-T1638: one card per offer on show, with its contents, each price it has, the claims left and Buy.</summary>
        private void BuildOffers(VisualElement body)
        {
            var w = DiamondExchange.Wallet;
            var offers = Offers.AvailableIn(C, w, S, DateTime.UtcNow);
            if (offers.Count == 0)
            {
                var panel = Ui.Panel(body, "Offers", false, "offers-panel");
                Ui.EmptyState(panel, RowKind.Diamond, EmptyLine(w), "offers-empty");
                return;
            }
            var store = DiamondStores.Current;
            bool busy = DiamondStores.InFlight != null;
            foreach (var offer in offers)
            {
                var o = offer;
                var card = Ui.Panel(body, o.Name, false, "offer-" + o.Id);
                ContentChips(o, card);
                var product = Offers.ForMoney(o) ? StoreCatalog.Of(o.ProductId) : null;
                if (product != null)
                    Ui.Field("Price", DiamondStores.InFlight == product.Id ? "Processing" : store.PriceOf(product) ?? product.Price, card, null, "offer-price-" + o.Id);
                if (o.ShardPrice > 0)
                    Ui.Field("Price", ShardText.ShortOf(o.ShardPrice), card, AccountWallets.CanSpend(w, o.ShardPrice) ? null : "bad", "offer-shard-price-" + o.Id);
                int left = Offers.Remaining(w, o);
                if (left != int.MaxValue) Ui.Field("Left", $"{left:N0}", card, "dim", "offer-left-" + o.Id);
                var act = Ui.Actions(card);
                var buy = RowIcon.IconButton("Buy", RowKind.Diamond, () => BuyOffer(o), "btn--primary", act);
                buy.name = "offer-buy-" + o.Id;
                Ui.SetEnabled(buy, product != null ? store.Connected && !busy : AccountWallets.CanSpend(w, o.ShardPrice));
            }
        }

        /// <summary>What the offer holds, one currency chip per kind (Shards, Summon Tickets, cosmetics).</summary>
        private static void ContentChips(OfferDef o, VisualElement card)
        {
            var chips = Ui.Actions(card);
            chips.name = "offer-contents-" + o.Id;
            if (o.Shards > 0) Ui.Chip(ShardText.Of(o.Shards), chips, RowKind.Diamond, tone: "gold");
            if (o.Tickets > 0) Ui.Chip(TicketText.Of(o.Tickets), chips, RowKind.Summon);
            int looks = o.Cosmetics?.Count ?? 0;
            if (looks > 0) Ui.Chip(looks == 1 ? "1 cosmetic" : $"{looks} cosmetics", chips, RowKind.Item);
        }

        private static string Contents(OfferDef o)
        {
            var parts = new List<string>();
            if (o.Shards > 0) parts.Add(ShardText.Of(o.Shards));
            if (o.Tickets > 0) parts.Add($"{o.Tickets:N0} Summon Ticket{(o.Tickets == 1 ? "" : "s")}");
            int looks = o.Cosmetics?.Count ?? 0;
            if (looks > 0) parts.Add(looks == 1 ? "1 cosmetic" : $"{looks} cosmetics");
            return string.Join(" · ", parts);
        }

        /// <summary>Why the tab is empty and when the next offer opens (the soonest one this save hasn't reached).</summary>
        private string EmptyLine(AccountWallet w)
        {
            var utc = DateTime.UtcNow;
            var next = C.OfferOrder
                .Where(o => o != null && (o.EndUtc == null || utc < o.EndUtc.Value) && Offers.Remaining(w, o) > 0
                    && S.HighestClearedStage + 1 < o.ShowsAfter)
                .OrderBy(o => o.ShowsAfter).FirstOrDefault();
            return next == null ? "No offers right now. Check back soon." : $"No offers yet. {CampUnlocks.MilestoneHint(C, next.ShowsAfter)}.";
        }

        private void BuyOffer(OfferDef o)
        {
            var w = DiamondExchange.Wallet;
            if (!Offers.ForMoney(o))
            {
                bool ok = Offers.BuyWithShards(o, w, S, DateTime.UtcNow);
                if (ok)
                {
                    DiamondExchange.WalletDirty = true;
                    App.PersistWallet();
                }
                App.Toast(ok ? $"{o.Name}: {Contents(o)}" : $"Not enough {ShardText.ShortPlural} for {o.Name}.", ok ? 2.2f : 3f);
                App.Audio.Sfx(ok ? "ui.diamond" : "ui.error");
                Refresh();
                return;
            }
            var store = DiamondStores.Current;
            var product = StoreCatalog.Of(o.ProductId);
            if (DiamondStores.InFlight != null || product == null) return;
            if (!store.Connected) { Answer(PurchaseResult.Of(PurchaseKind.Unavailable, store.Status, product.Id)); return; }
            // The Shard Store's gate (GG-T1559): the age band once per wallet, then the monthly limit, then the parental gate.
            if (SpendLimits.NeedsAge(w)) { SpendGate.AskAge(w, () => BuyOffer(o)); return; }
            var refusal = SpendLimits.Refusal(w, product, DateTime.UtcNow);
            if (refusal != null) { Answer(PurchaseResult.Of(PurchaseKind.Unavailable, refusal, product.Id)); return; }
            if (SpendLimits.NeedsParentalGate(w)) { SpendGate.Parental(() => Purchase(store, product)); return; }
            Purchase(store, product);
        }

        /// <summary>The store counts the offer's claim when it grants the product (Offers.ClaimOnGrant), so a deferred or
        /// restored purchase counts too.</summary>
        private void Purchase(IDiamondStore store, StoreProduct product)
        {
            if (DiamondStores.InFlight != null) return;
            DiamondStores.InFlight = product.Id;
            App.Audio.Sfx("ui.select");
            Refresh();
            store.Purchase(C, S, product, r => { DiamondStores.InFlight = null; Answer(r); });
        }

        private void Answer(PurchaseResult r)
        {
            if (r == null) return;
            App.Toast(r.Message, r.Ok ? 2.2f : 3f);
            App.Audio.Sfx(r.Ok ? "ui.diamond" : r.Kind == PurchaseKind.Pending || r.Kind == PurchaseKind.Cancelled ? "ui.select" : "ui.error");
            if (r.Ok && DiamondExchange.WalletDirty) App.PersistWallet();
            if (App.Router.Current == this) Refresh();
        }

        /// <summary>The Shard Store's clock (store connection, prices, a stuck purchase) keeps running off its own tab.</summary>
        public override void Tick(float dt)
        {
            base.Tick(dt);
            if (tab != MerchantTab) Shards.Tick(dt);
        }
    }
}
