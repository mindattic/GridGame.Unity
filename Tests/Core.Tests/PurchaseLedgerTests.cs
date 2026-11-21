using System;
using GridGame.Core.Content;
using GridGame.Meta;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>§25.18.1 (GG-T469): every real-money transaction pays out exactly once, refunds take back only what's left,
    /// and the ledger survives a save round trip and the v11 migration.</summary>
    public class PurchaseLedgerTests
    {
        private static readonly DateTime When = new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);

        private static SaveData Save() => Progression.NewGame(Real, "Ledger");

        [Test]
        public void A_transaction_is_granted_once_however_often_the_store_delivers_it()
        {
            var s = Save();
            Assert.That(PurchaseLedger.Grant(s, "tx-1", "diamonds_500", When, "GooglePlay", out int d), Is.EqualTo(GrantOutcome.Granted));
            Assert.That(d, Is.EqualTo(500));
            Assert.That(PurchaseLedger.Grant(s, "tx-1", "diamonds_500", When, "GooglePlay", out d), Is.EqualTo(GrantOutcome.AlreadyGranted));
            Assert.That(d, Is.EqualTo(0));
            Assert.That(s.Count(Crafting.DiamondId), Is.EqualTo(500));
            Assert.That(PurchaseLedger.Has(s, "tx-1"), Is.True);

            Assert.That(PurchaseLedger.Grant(s, "tx-2", "diamonds_100", When, "GooglePlay", out _), Is.EqualTo(GrantOutcome.Granted));
            Assert.That(s.Count(Crafting.DiamondId), Is.EqualTo(600));
            Assert.That(PurchaseLedger.TotalBought(s), Is.EqualTo(600));
        }

        [Test]
        public void Unknown_products_and_missing_ids_grant_nothing()
        {
            var s = Save();
            Assert.That(PurchaseLedger.Grant(s, "tx-x", "gems_9000", When, "AppleAppStore", out _), Is.EqualTo(GrantOutcome.UnknownProduct));
            Assert.That(PurchaseLedger.Grant(s, "", "diamonds_100", When, "AppleAppStore", out _), Is.EqualTo(GrantOutcome.Invalid));
            Assert.That(PurchaseLedger.Grant(null, "tx-y", "diamonds_100", When, "AppleAppStore", out _), Is.EqualTo(GrantOutcome.Invalid));
            Assert.That(s.Count(Crafting.DiamondId), Is.EqualTo(0));
            Assert.That(PurchaseLedger.Has(s, "tx-x"), Is.False);
        }

        [Test]
        public void Paid_diamonds_land_even_past_the_bag_cap()
        {
            var s = Save();
            s.AddItem(Crafting.DiamondId, DiamondExchange.Cap(Real));
            Assert.That(PurchaseLedger.Grant(s, "tx-cap", "diamonds_100", When, "GooglePlay", out _), Is.EqualTo(GrantOutcome.Granted));
            Assert.That(s.Count(Crafting.DiamondId), Is.EqualTo(DiamondExchange.Cap(Real) + 100));
        }

        [Test]
        public void A_refund_takes_back_only_what_is_left_and_only_once()
        {
            var s = Save();
            PurchaseLedger.Grant(s, "tx-r", "diamonds_1000", When, "GooglePlay", out _);
            s.AddItem(Crafting.DiamondId, -700); // spent most of it
            s.Gold = 5000;

            Assert.That(PurchaseLedger.Refund(s, "tx-r"), Is.EqualTo(300));
            Assert.That(s.Count(Crafting.DiamondId), Is.EqualTo(0));
            Assert.That(s.Gold, Is.EqualTo(5000), "a refund never takes gold");
            Assert.That(s.Purchases.Granted["tx-r"].Refunded, Is.True);
            Assert.That(s.Purchases.Granted["tx-r"].ClawedBack, Is.EqualTo(300));

            Assert.That(PurchaseLedger.Refund(s, "tx-r"), Is.EqualTo(0), "idempotent");
            Assert.That(PurchaseLedger.Refund(s, "never-seen"), Is.EqualTo(0));
            Assert.That(PurchaseLedger.TotalBought(s), Is.EqualTo(0));
            Assert.That(PurchaseLedger.Grant(s, "tx-r", "diamonds_1000", When, "GooglePlay", out _), Is.EqualTo(GrantOutcome.AlreadyGranted),
                "a refunded transaction is still in the ledger, so a re-delivery doesn't pay it again");
        }

        [Test]
        public void A_refund_named_by_product_reverses_the_latest_purchase_of_it()
        {
            var s = Save();
            PurchaseLedger.Grant(s, "old", "diamonds_100", When, "AppleAppStore", out _);
            PurchaseLedger.Grant(s, "new", "diamonds_100", When.AddDays(1), "AppleAppStore", out _);
            PurchaseLedger.Grant(s, "other", "diamonds_500", When.AddDays(2), "AppleAppStore", out _);

            Assert.That(PurchaseLedger.RefundLatest(s, "diamonds_100"), Is.EqualTo(100));
            Assert.That(s.Purchases.Granted["new"].Refunded, Is.True);
            Assert.That(s.Purchases.Granted["old"].Refunded, Is.False);
            Assert.That(PurchaseLedger.RefundLatest(s, "diamonds_100"), Is.EqualTo(100), "then the older one");
            Assert.That(PurchaseLedger.RefundLatest(s, "diamonds_100"), Is.EqualTo(0), "none left");
            Assert.That(s.Count(Crafting.DiamondId), Is.EqualTo(500));
        }

        [Test]
        public void The_ledger_survives_a_save_round_trip()
        {
            var s = Save();
            PurchaseLedger.Grant(s, "GPA.1234-5678", "diamonds_2500", When, "GooglePlay", out _);
            var back = SaveMigrations.Migrate(SaveFiles.Deserialize(SaveFiles.Serialize(s)), Real);
            Assert.That(PurchaseLedger.Has(back, "GPA.1234-5678"), Is.True);
            Assert.That(back.Purchases.Granted["GPA.1234-5678"].Diamonds, Is.EqualTo(2500));
            Assert.That(PurchaseLedger.Grant(back, "GPA.1234-5678", "diamonds_2500", When, "GooglePlay", out _), Is.EqualTo(GrantOutcome.AlreadyGranted));
        }

        [Test]
        public void A_v10_save_migrates_to_an_empty_ledger()
        {
            var s = Save();
            s.Version = 10;
            s.Purchases = null;
            var back = SaveMigrations.Migrate(s, Real);
            Assert.That(back.Version, Is.EqualTo(SaveData.CurrentVersion));
            Assert.That(back.Purchases, Is.Not.Null);
            Assert.That(back.Purchases.Granted, Is.Empty);
        }
    }
}
