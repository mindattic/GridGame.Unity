using System;
using System.Linq;
using GridGame.Meta;
using NUnit.Framework;

namespace GridGame.Tests
{
    /// <summary>GG-T1447/GG-T1564: the real-money catalogue's account limits, and the account wallet granting and refunding
    /// by product: offer Tickets and cosmetics, non-consumables owned once, restores and refunds.</summary>
    public class StoreCatalogTests
    {
        private static readonly DateTime When = new DateTime(2026, 11, 23, 12, 0, 0, DateTimeKind.Utc);

        private static StoreProduct P(string id) => StoreCatalog.Of(id);

        [Test]
        public void Every_shard_pack_is_in_the_catalogue_and_ids_are_unique()
        {
            foreach (var pack in DiamondStore.Packs)
            {
                var p = P(pack.ProductId);
                Assert.That(p, Is.Not.Null, pack.ProductId);
                Assert.That(p.Type, Is.EqualTo(ProductType.Consumable));
                Assert.That(p.Shards, Is.EqualTo(pack.Diamonds));
                Assert.That(p.Cents, Is.EqualTo(pack.Cents));
            }
            Assert.That(StoreCatalog.Products.Select(p => p.Id).Distinct().Count(), Is.EqualTo(StoreCatalog.Products.Count));
            Assert.That(StoreCatalog.Of("gems_9000"), Is.Null);
            Assert.That(StoreCatalog.Of(null), Is.Null);
        }

        [Test]
        public void Shard_packs_have_no_limit_and_the_starter_pack_sells_once()
        {
            var w = new AccountWallet();
            var pack = P("diamonds_100");
            var starter = P(StoreCatalog.StarterPackId);
            Assert.That(StoreCatalog.Remaining(w, pack), Is.EqualTo(int.MaxValue));
            Assert.That(StoreCatalog.Remaining(w, starter), Is.EqualTo(1));
            Assert.That(StoreCatalog.Remaining(w, null), Is.EqualTo(0));
            Assert.That(StoreCatalog.CanBuy(w, null), Is.False);

            AccountWallets.Grant(w, "tx-a", "diamonds_100", When, "GooglePlay", out _);
            AccountWallets.Grant(w, "tx-b", "diamonds_100", When, "GooglePlay", out _);
            Assert.That(StoreCatalog.Bought(w, pack), Is.EqualTo(2));
            Assert.That(StoreCatalog.CanBuy(w, pack), Is.True);

            AccountWallets.Grant(w, "tx-s", StoreCatalog.StarterPackId, When, "GooglePlay", out _);
            Assert.That(StoreCatalog.Remaining(w, starter), Is.EqualTo(0));
            Assert.That(StoreCatalog.CanBuy(w, starter), Is.False);

            AccountWallets.Refund(w, "tx-s");
            Assert.That(StoreCatalog.Bought(w, starter), Is.EqualTo(0), "a refunded purchase no longer counts");
            Assert.That(StoreCatalog.CanBuy(w, starter), Is.True);
        }

        [Test]
        public void A_non_consumable_is_bought_at_most_once()
        {
            var w = new AccountWallet();
            var pass = P(StoreCatalog.SeasonPassS1Id);
            Assert.That(pass.Type, Is.EqualTo(ProductType.NonConsumable));
            Assert.That(StoreCatalog.Remaining(w, pass), Is.EqualTo(1));
            Assert.That(AccountWallets.Grant(w, "tx-p", pass.Id, When, "AppleAppStore", out _), Is.EqualTo(GrantOutcome.Granted));
            Assert.That(StoreCatalog.Owns(w, pass.Id), Is.True);
            Assert.That(StoreCatalog.Remaining(w, pass), Is.EqualTo(0));
            Assert.That(StoreCatalog.CanBuy(w, pass), Is.False);
        }

        [Test]
        public void The_starter_pack_grants_shards_tickets_and_its_cosmetic()
        {
            var w = new AccountWallet();
            Assert.That(AccountWallets.Grant(w, "tx-s", StoreCatalog.StarterPackId, When, "GooglePlay", out int shards), Is.EqualTo(GrantOutcome.Granted));
            Assert.That(shards, Is.EqualTo(300));
            Assert.That(w.Diamonds, Is.EqualTo(300));
            Assert.That(w.Tickets, Is.EqualTo(5));
            Assert.That(StoreCatalog.OwnsCosmetic(w, "frame_starter"), Is.True);
            Assert.That(StoreCatalog.Owns(w, StoreCatalog.StarterPackId), Is.False, "a consumable is never owned");
            var r = w.Granted["tx-s"];
            Assert.That(r.Tickets, Is.EqualTo(5));
            Assert.That(r.Cosmetics, Is.EqualTo(new[] { "frame_starter" }));
            Assert.That(r.Ownership, Is.False);

            Assert.That(AccountWallets.Refund(w, "tx-s"), Is.EqualTo(300));
            Assert.That(w.Diamonds, Is.EqualTo(0));
            Assert.That(w.Tickets, Is.EqualTo(0));
            Assert.That(StoreCatalog.OwnsCosmetic(w, "frame_starter"), Is.False);
        }

        [Test]
        public void Refunded_tickets_take_back_only_what_is_left()
        {
            var w = new AccountWallet();
            AccountWallets.Grant(w, "tx-s", StoreCatalog.StarterPackId, When, "GooglePlay", out _);
            w.Tickets = 2;
            AccountWallets.Refund(w, "tx-s");
            Assert.That(w.Tickets, Is.EqualTo(0), "never below zero");
        }

        [Test]
        public void A_restore_under_a_new_transaction_id_is_recorded_but_grants_nothing()
        {
            var w = new AccountWallet();
            Assert.That(AccountWallets.Grant(w, "tx-1", "cosmetic_frames_1", When, "AppleAppStore", out _), Is.EqualTo(GrantOutcome.Granted));
            Assert.That(w.Cosmetics, Has.Count.EqualTo(3));

            Assert.That(AccountWallets.Grant(w, "tx-2", "cosmetic_frames_1", When.AddDays(1), "AppleAppStore", out int shards), Is.EqualTo(GrantOutcome.AlreadyGranted));
            Assert.That(shards, Is.EqualTo(0));
            Assert.That(AccountWallets.Has(w, "tx-2"), Is.True, "the restore is recorded so it is not granted again");
            var r = w.Granted["tx-2"];
            Assert.That(r.Ownership, Is.False);
            Assert.That(r.Cosmetics ?? Array.Empty<string>(), Is.Empty);
            Assert.That(w.Cosmetics, Has.Count.EqualTo(3));
            Assert.That(w.Owned, Has.Count.EqualTo(1));

            AccountWallets.Refund(w, "tx-2");
            Assert.That(StoreCatalog.Owns(w, "cosmetic_frames_1"), Is.True, "refunding the restore takes nothing it never granted");
            Assert.That(w.Cosmetics, Has.Count.EqualTo(3));
        }

        [Test]
        public void A_refund_removes_ownership_and_only_the_cosmetics_that_transaction_added()
        {
            var w = new AccountWallet();
            w.Cosmetics.Add("frame_ember");
            AccountWallets.Grant(w, "tx-f", "cosmetic_frames_1", When, "GooglePlay", out _);
            AccountWallets.Grant(w, "tx-t", "cosmetic_themes_1", When, "GooglePlay", out _);
            Assert.That(w.Granted["tx-f"].Cosmetics, Is.EquivalentTo(new[] { "frame_tidal", "frame_verdant" }), "an owned cosmetic is not counted as added");
            Assert.That(w.Granted["tx-f"].Ownership, Is.True);

            AccountWallets.Refund(w, "tx-f");
            Assert.That(StoreCatalog.Owns(w, "cosmetic_frames_1"), Is.False);
            Assert.That(StoreCatalog.OwnsCosmetic(w, "frame_ember"), Is.True, "owned before the purchase, so kept");
            Assert.That(StoreCatalog.OwnsCosmetic(w, "frame_tidal"), Is.False);
            Assert.That(StoreCatalog.OwnsCosmetic(w, "frame_verdant"), Is.False);
            Assert.That(StoreCatalog.Owns(w, "cosmetic_themes_1"), Is.True, "another product's ownership is untouched");
            Assert.That(StoreCatalog.OwnsCosmetic(w, "theme_aurora"), Is.True);
            Assert.That(StoreCatalog.CanBuy(w, P("cosmetic_frames_1")), Is.True);
        }

        [Test]
        public void The_monthly_limit_prices_any_product_from_the_catalogue()
        {
            var w = new AccountWallet { MonthlyLimitCents = 500 };
            AccountWallets.Grant(w, "tx-s", StoreCatalog.StarterPackId, When, "GooglePlay", out _);
            Assert.That(SpendLimits.SpentCents(w, When), Is.EqualTo(299));
            Assert.That(SpendLimits.Refusal(w, P(StoreCatalog.SeasonPassS1Id), When), Is.Not.Null);
            Assert.That(SpendLimits.Refusal(w, P("cosmetic_frames_1"), When), Is.Null);
            Assert.That(SpendLimits.Refusal(w, (StoreProduct)null, When), Is.Null);
            Assert.That(SpendLimits.Refusal(w, P(StoreCatalog.SeasonPassS1Id), When.AddMonths(1)), Is.Null, "a new month starts fresh");
            w.MonthlyLimitCents = 0;
            Assert.That(SpendLimits.Refusal(w, P("cosmetic_frames_1"), When), Does.StartWith("Purchases are turned off"));
        }
    }
}
