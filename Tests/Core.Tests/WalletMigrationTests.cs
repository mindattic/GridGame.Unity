using System;
using System.IO;
using GridGame.Meta;
using NUnit.Framework;
using Unity.Shared.Core;

namespace GridGame.Tests
{
    /// <summary>GG-T621: a save's Diamonds and purchase ledger move into the account wallet once, never twice (not on a
    /// second run, a copy of the save, an older backup of it, or a crash between the two writes), never lose a Diamond,
    /// and leave the save's own ledger as it was for a rollback.</summary>
    public class WalletMigrationTests
    {
        private static readonly DateTime When = new DateTime(2026, 11, 23, 12, 0, 0, DateTimeKind.Utc);
        private string root;

        [SetUp]
        public void MakeRoot()
        {
            root = Path.Combine(Path.GetTempPath(), "gg-walletmove-" + Guid.NewGuid().ToString("N"), ProfileStore.FolderName);
            Directory.CreateDirectory(root);
        }

        [TearDown]
        public void DropRoot()
        {
            try { Directory.Delete(Path.GetDirectoryName(root), true); } catch (IOException) { }
        }

        private static SaveData SaveWith(int diamonds, DateTime created, params string[] txs)
        {
            var s = new SaveData { CreatedUtc = created };
            foreach (var tx in txs) PurchaseLedger.Grant(s, tx, "diamonds_100", When, "GooglePlay", out _);
            int extra = diamonds - DiamondExchange.Have(s);
            if (extra != 0) s.AddItem(Crafting.DiamondId, extra);
            return s;
        }

        private static SaveData Copy(SaveData s) => SaveFiles.Deserialize(SaveFiles.Serialize(s));

        [Test]
        public void A_save_moves_once_and_keeps_its_ledger()
        {
            var w = new AccountWallet();
            var s = SaveWith(250, When.AddDays(-3), "tx-1");

            var r = WalletMigration.Migrate(w, s, When);
            Assert.That(r.Outcome, Is.EqualTo(WalletMigrationOutcome.Moved));
            Assert.That(r.Drained, Is.EqualTo(250));
            Assert.That(r.Carried, Is.EqualTo(250));
            Assert.That(r.Purchases, Is.EqualTo(1));
            Assert.That(w.Diamonds, Is.EqualTo(250), "the ledger's Diamonds were in the bag: not paid again");
            Assert.That(w.Granted.ContainsKey("tx-1"), Is.True);
            Assert.That(DiamondExchange.Have(s), Is.EqualTo(0));
            Assert.That(s.Purchases.Granted.ContainsKey("tx-1"), Is.True, "the save's ledger stays for a rollback");
            Assert.That(WalletMigration.DrainedOf(s), Is.EqualTo(250));

            var again = WalletMigration.Migrate(w, s, When);
            Assert.That(again.Outcome, Is.EqualTo(WalletMigrationOutcome.AlreadyMoved));
            Assert.That(again.WalletChanged, Is.False);
            Assert.That(w.Diamonds, Is.EqualTo(250));
            Assert.That(AccountWallets.Grant(w, "tx-1", "diamonds_100", When, "GooglePlay", out _), Is.EqualTo(GrantOutcome.AlreadyGranted));
        }

        [Test]
        public void A_copy_or_old_backup_of_the_same_save_adds_only_what_it_holds_beyond_the_first()
        {
            var w = new AccountWallet();
            var created = When.AddDays(-10);
            var backup = SaveWith(600, created, "tx-1");
            var current = Copy(backup);
            current.AddItem(Crafting.DiamondId, -100); // spent 100 after the backup was written

            WalletMigration.Migrate(w, current, When);
            Assert.That(w.Diamonds, Is.EqualTo(500));
            var r = WalletMigration.Migrate(w, backup, When);
            Assert.That(r.Outcome, Is.EqualTo(WalletMigrationOutcome.Moved));
            Assert.That(r.Carried, Is.EqualTo(100), "only the excess: never the same Diamonds twice");
            Assert.That(w.Diamonds, Is.EqualTo(600));
            Assert.That(w.Granted.Count, Is.EqualTo(1));

            var other = SaveWith(40, When.AddDays(-1));
            WalletMigration.Migrate(w, other, When);
            Assert.That(w.Diamonds, Is.EqualTo(640), "a different save adds all of its own");
        }

        [Test]
        public void A_crash_between_the_wallet_write_and_the_save_write_pays_nothing_twice()
        {
            var store = new AccountWalletStore(root);
            var onDisk = SaveWith(300, When.AddDays(-2), "tx-9");
            var loaded = Copy(onDisk);

            var r = WalletMigration.Run(store, null, loaded, When);
            Assert.That(r.Outcome, Is.EqualTo(WalletMigrationOutcome.Moved));
            Assert.That(store.Load().Diamonds, Is.EqualTo(300));
            // The save write never happened: the unmarked file loads again.
            var r2 = WalletMigration.Run(store, null, Copy(onDisk), When);
            Assert.That(r2.Outcome, Is.EqualTo(WalletMigrationOutcome.Moved));
            Assert.That(r2.Carried, Is.EqualTo(0));
            Assert.That(store.Load().Diamonds, Is.EqualTo(300));
        }

        [Test]
        public void A_failed_wallet_write_puts_the_save_back()
        {
            var blocker = Path.Combine(root, AccountWalletStore.FolderName);
            File.WriteAllText(blocker, "not a folder"); // the wallet folder can't be made
            var store = new AccountWalletStore(root);
            var s = SaveWith(120, When.AddDays(-2), "tx-2");

            var r = WalletMigration.Run(store, null, s, When);
            Assert.That(r.Outcome, Is.EqualTo(WalletMigrationOutcome.Failed));
            Assert.That(DiamondExchange.Have(s), Is.EqualTo(120));
            Assert.That(WalletMigration.IsMoved(s), Is.False);
        }

        [Test]
        public void A_refund_the_save_saw_is_not_clawed_back_again()
        {
            var w = new AccountWallet();
            var s = SaveWith(100, When.AddDays(-2), "tx-r");
            AccountWallets.Grant(w, "tx-r", "diamonds_100", When, "GooglePlay", out _); // the wallet already knew the order
            PurchaseLedger.Refund(s, "tx-r");

            var r = WalletMigration.Migrate(w, s, When);
            Assert.That(r.Refunds, Is.EqualTo(1));
            Assert.That(w.Granted["tx-r"].Refunded, Is.True);
            Assert.That(AccountWallets.Refund(w, "tx-r"), Is.EqualTo(0));
        }

        [Test]
        public void The_wallet_remembers_what_it_carried_across_a_reload()
        {
            var store = new AccountWalletStore(root);
            var s = SaveWith(75, When.AddDays(-4));
            WalletMigration.Run(store, null, Copy(s), When);
            var w = store.Load();
            Assert.That(w.Migrated[WalletMigration.IdentityOf(s)].Diamonds, Is.EqualTo(75));
            Assert.That(WalletMigration.Migrate(w, s, When).Carried, Is.EqualTo(0));
        }
    }
}
