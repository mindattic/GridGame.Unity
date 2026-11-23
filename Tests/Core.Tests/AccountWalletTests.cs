using System;
using System.IO;
using GridGame.Meta;
using NUnit.Framework;
using Unity.Shared.Core;

namespace GridGame.Tests
{
    /// <summary>GG-T620: the account wallet is one Diamond balance + purchase ledger per store login on the device, shared
    /// by every profile and slot, kept in its own file beside the profiles rather than in any save.</summary>
    public class AccountWalletTests
    {
        private static readonly DateTime When = new DateTime(2026, 11, 23, 12, 0, 0, DateTimeKind.Utc);
        private string root;

        [SetUp]
        public void MakeRoot()
        {
            root = Path.Combine(Path.GetTempPath(), "gg-wallet-" + Guid.NewGuid().ToString("N"), ProfileStore.FolderName);
            Directory.CreateDirectory(root);
        }

        [TearDown]
        public void DropRoot()
        {
            try { Directory.Delete(Path.GetDirectoryName(root), true); } catch (IOException) { }
        }

        [Test]
        public void A_transaction_is_granted_once_per_wallet()
        {
            var w = new AccountWallet();
            Assert.That(AccountWallets.Grant(w, "tx-1", "diamonds_500", When, "GooglePlay", out int d), Is.EqualTo(GrantOutcome.Granted));
            Assert.That(d, Is.EqualTo(500));
            Assert.That(AccountWallets.Grant(w, "tx-1", "diamonds_500", When, "GooglePlay", out d), Is.EqualTo(GrantOutcome.AlreadyGranted));
            Assert.That(d, Is.EqualTo(0));
            Assert.That(AccountWallets.Grant(w, "tx-2", "gems_9000", When, "GooglePlay", out _), Is.EqualTo(GrantOutcome.UnknownProduct));
            Assert.That(AccountWallets.Grant(w, "", "diamonds_100", When, "GooglePlay", out _), Is.EqualTo(GrantOutcome.Invalid));
            Assert.That(w.Diamonds, Is.EqualTo(500));
            Assert.That(AccountWallets.TotalBought(w), Is.EqualTo(500));
        }

        [Test]
        public void Spending_never_overdraws_and_refunds_take_back_only_what_is_left()
        {
            var w = new AccountWallet();
            AccountWallets.Grant(w, "tx-r", "diamonds_1000", When, "AppleAppStore", out _);
            Assert.That(AccountWallets.Spend(w, 1001), Is.False);
            Assert.That(w.Diamonds, Is.EqualTo(1000), "a refused spend takes nothing");
            Assert.That(AccountWallets.Spend(w, 700), Is.True);
            Assert.That(AccountWallets.Refund(w, "tx-r"), Is.EqualTo(300));
            Assert.That(w.Diamonds, Is.EqualTo(0));
            Assert.That(w.Granted["tx-r"].ClawedBack, Is.EqualTo(300));
            Assert.That(AccountWallets.Refund(w, "tx-r"), Is.EqualTo(0), "idempotent");
            Assert.That(AccountWallets.Grant(w, "tx-r", "diamonds_1000", When, "AppleAppStore", out _), Is.EqualTo(GrantOutcome.AlreadyGranted));
            Assert.That(AccountWallets.Add(w, 25), Is.EqualTo(25));
            Assert.That(AccountWallets.RefundLatest(w, "diamonds_1000"), Is.EqualTo(0), "nothing unrefunded left");
        }

        [Test]
        public void Every_profile_reads_the_same_wallet_and_each_login_has_its_own()
        {
            var profiles = new ProfileStore(root);
            profiles.Create("Alice");
            profiles.Create("Bob");
            var store = AccountWalletStore.For(profiles);

            var w = store.Load();
            AccountWallets.Grant(w, "tx-1", "diamonds_100", When, "GooglePlay", out _);
            Assert.That(store.Save(w), Is.True);

            Assert.That(profiles.List(), Is.EquivalentTo(new[] { "Alice", "Bob" }), "the account folder is never read as a profile");
            Assert.That(AccountWalletStore.For(new ProfileStore(root)).Load().Diamonds, Is.EqualTo(100));
            Assert.That(store.Load().Granted.ContainsKey("tx-1"), Is.True);

            var other = store.Load("someone@example.com");
            Assert.That(other.Diamonds, Is.EqualTo(0), "another store login starts empty");
            Assert.That(other.Login, Is.Not.EqualTo(AccountWalletStore.DeviceKey));
            Assert.That(store.PathFor("someone@example.com"), Does.Not.Contain("example"), "no login is written to disk");
            AccountWallets.Add(other, 7);
            Assert.That(store.Save(other), Is.True);
            Assert.That(store.Load("someone@example.com").Diamonds, Is.EqualTo(7));
            Assert.That(store.Load().Diamonds, Is.EqualTo(100));
        }

        [Test]
        public void A_damaged_wallet_falls_back_to_its_backup()
        {
            var store = new AccountWalletStore(root);
            var w = store.Load();
            AccountWallets.Add(w, 40);
            store.Save(w);
            AccountWallets.Add(w, 2);
            store.Save(w);
            File.WriteAllText(store.PathFor(null), "{ not json");
            Assert.That(store.Load().Diamonds, Is.EqualTo(40), "the previous good copy, not an empty wallet");
        }
    }
}
