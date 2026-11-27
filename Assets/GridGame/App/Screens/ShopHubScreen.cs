using System;
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
        private static readonly string[] TabNames = { "Merchant", "Shards", "Offers" };

        internal readonly ShopScreen Merchant = new ShopScreen();
        internal readonly DiamondStoreScreen Shards = new DiamondStoreScreen();
        private int tab = -1;

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

        private static void BuildOffers(VisualElement body)
        {
            var panel = Ui.Panel(body, "Offers", false, "offers-panel");
            Ui.Para("No offers right now. Check back soon.", panel, "dim");
        }

        /// <summary>The Shard Store's clock (store connection, prices, a stuck purchase) keeps running off its own tab.</summary>
        public override void Tick(float dt)
        {
            base.Tick(dt);
            if (tab != MerchantTab) Shards.Tick(dt);
        }
    }
}
