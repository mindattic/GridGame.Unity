using System;
using System.Linq;

namespace GridGame.Meta
{
    /// <summary>What the account wallet holds about one save it carried Diamonds in from (GG-T621).</summary>
    public sealed class CarriedSave
    {
        /// <summary>The most Diamonds any copy of this save held when it was carried in: the wallet already counts these,
        /// so a copy or an older backup of the same save only adds whatever it holds beyond them.</summary>
        public int Diamonds;
        /// <summary>Purchase records copied from it into the wallet's ledger.</summary>
        public int Purchases;
        /// <summary>Copies of this save carried in so far (the first load, then any copy or backup of it).</summary>
        public int Copies;
        public DateTime FirstUtc;
        public DateTime LastUtc;
    }

    public enum WalletMigrationOutcome
    {
        /// <summary>The save's Diamonds went into the wallet (all, or what it held beyond an earlier copy) and its bag
        /// was emptied of them. Write the wallet, then the save.</summary>
        Moved,
        /// <summary>The save was moved before: only its ledger was checked against the wallet's.</summary>
        AlreadyMoved,
        /// <summary>No save or no wallet. Nothing changed.</summary>
        Invalid,
        /// <summary>The wallet write failed, so the save was put back as it was (Diamonds in the bag, no marker). Try again
        /// on the next load; don't write the save as moved.</summary>
        Failed,
    }

    /// <summary>What <see cref="WalletMigration.Migrate"/> did.</summary>
    public sealed class WalletMigrationResult
    {
        public WalletMigrationOutcome Outcome;
        /// <summary>The save's identity (<see cref="WalletMigration.IdentityOf"/>).</summary>
        public string Identity;
        /// <summary>Diamonds taken out of the save's bag.</summary>
        public int Drained;
        /// <summary>Diamonds added to the wallet (less than <see cref="Drained"/> when another copy of this save was carried
        /// in first).</summary>
        public int Carried;
        /// <summary>Purchase records copied into the wallet's ledger (never paid again: their Diamonds were in the bag).</summary>
        public int Purchases;
        /// <summary>Refunds the save knew about that the wallet's copy of the record didn't (marked, nothing taken twice).</summary>
        public int Refunds;
        /// <summary>Diamonds found in the bag of a save that was already moved (something added them after the move). Left
        /// where they are and only reported: carrying them again could pay a rolled-back copy twice.</summary>
        public int Stray;

        /// <summary>Whether the wallet changed and has to be written.</summary>
        public bool WalletChanged => Outcome == WalletMigrationOutcome.Moved || Purchases > 0 || Refunds > 0;
    }

    /// <summary>
    /// Moves a save's Diamonds and purchase ledger into the account wallet (§25.18.1, GG-T621): the save-side half of
    /// "Diamonds belong to the store login, not the save" (GG-T567). Pure on the wallet and the save; <see cref="Run"/>
    /// adds the one safe write order (wallet first, the save only after the wallet held).
    /// <list type="bullet">
    /// <item>Never pays twice. A save is known by its identity (<see cref="IdentityOf"/>, from its creation time), which a
    /// copy in another slot or profile, a <c>.bak</c>, a cloud copy or an exported file all share. The wallet keeps, per
    /// identity, the most Diamonds any copy held when carried in, and a later copy adds only what it holds beyond that. The
    /// save is marked (<see cref="MovedFlagPrefix"/>) and a marked save is never carried again. Running it twice, or
    /// crashing between the wallet write and the save write, adds nothing the second time.</item>
    /// <item>Never loses Diamonds. Everything in the bag goes into the wallet unless the wallet already counts it from
    /// this same save; ledger records the wallet lacks are copied in, record by record (the Diamonds they granted are
    /// already in the bag, so a copied record adds none). A refund the save saw marks the wallet's record refunded, with
    /// what was clawed back, so it can't be clawed back a second time.</item>
    /// <item>Keeps the save readable for rollback. The save's own ledger is left exactly as it was, and the marker names
    /// how many Diamonds left the bag and for which wallet.</item>
    /// </list>
    /// </summary>
    public static class WalletMigration
    {
        /// <summary>The save flag that marks a moved save: <c>wallet-moved:&lt;Diamonds drained&gt;:&lt;wallet key&gt;</c>.
        /// A flag rather than a new save field, so no save version bump is needed and an older build still opens it.</summary>
        public const string MovedFlagPrefix = "wallet-moved:";

        /// <summary>The identity a save and every copy of it share: its creation time, to the tick.</summary>
        public static string IdentityOf(SaveData s) => s == null ? null : "save-" + s.CreatedUtc.Ticks.ToString("x");

        /// <summary>The save's move marker, or null when it hasn't been moved.</summary>
        public static string MovedFlag(SaveData s) => s?.Flags?.FirstOrDefault(f => f != null && f.StartsWith(MovedFlagPrefix, StringComparison.Ordinal));

        public static bool IsMoved(SaveData s) => MovedFlag(s) != null;

        /// <summary>Diamonds the marker says left the bag when the save was moved (0 when not moved).</summary>
        public static int DrainedOf(SaveData s)
        {
            var f = MovedFlag(s);
            if (f == null) return 0;
            var parts = f.Substring(MovedFlagPrefix.Length).Split(':');
            return int.TryParse(parts[0], out int n) ? Math.Max(0, n) : 0;
        }

        /// <summary>Moves <paramref name="s"/>'s Diamonds and ledger into <paramref name="w"/> in memory. Idempotent: a moved
        /// save only has its ledger compared again. The caller persists the wallet first and the save after (see
        /// <see cref="Run"/>); on a failed wallet write <see cref="Undo"/> puts the save back.</summary>
        public static WalletMigrationResult Migrate(AccountWallet w, SaveData s, DateTime utc)
        {
            var r = new WalletMigrationResult { Identity = IdentityOf(s) };
            if (w == null || s == null) { r.Outcome = WalletMigrationOutcome.Invalid; return r; }
            w.Normalize();
            s.Flags ??= new System.Collections.Generic.HashSet<string>();

            MergeLedger(w, s, r);

            if (IsMoved(s))
            {
                r.Outcome = WalletMigrationOutcome.AlreadyMoved;
                r.Stray = DiamondExchange.Bag(s);
                if (r.Purchases > 0 && r.Identity != null && w.Migrated.TryGetValue(r.Identity, out var known)) known.Purchases += r.Purchases;
                if (r.WalletChanged) w.UpdatedUtc = utc;
                return r;
            }

            int bag = Math.Max(0, DiamondExchange.Bag(s));
            w.Migrated.TryGetValue(r.Identity, out var before);
            int counted = before?.Diamonds ?? 0;
            r.Carried = AccountWallets.Add(w, Math.Max(0, bag - counted));
            var entry = before ?? new CarriedSave { FirstUtc = utc };
            entry.Diamonds = Math.Max(counted, bag);
            entry.Purchases += r.Purchases;
            entry.Copies++;
            entry.LastUtc = utc;
            w.Migrated[r.Identity] = entry;
            w.UpdatedUtc = utc;

            if (bag > 0) s.AddItem(Crafting.DiamondId, -bag);
            r.Drained = bag;
            s.Flags.Add(MovedFlagPrefix + bag + ":" + (w.Login ?? AccountWalletStore.DeviceKey));
            r.Outcome = WalletMigrationOutcome.Moved;
            return r;
        }

        /// <summary>Puts a save back as it was before <see cref="Migrate"/> moved it (its Diamonds back in the bag, no
        /// marker), for when the wallet couldn't be written. The save's ledger was never changed, so nothing else to undo.</summary>
        public static void Undo(SaveData s, WalletMigrationResult r)
        {
            if (s == null || r == null || r.Outcome != WalletMigrationOutcome.Moved) return;
            var f = MovedFlag(s);
            if (f != null) s.Flags.Remove(f);
            if (r.Drained > 0) s.AddItem(Crafting.DiamondId, r.Drained);
        }

        /// <summary>The whole migration for one loaded save: loads <paramref name="storeLogin"/>'s wallet, moves the save
        /// into it and writes the wallet. When that write fails the save is put back (<see cref="WalletMigrationOutcome.Failed"/>)
        /// and nothing is lost; otherwise the caller writes the save next (a crash before that is harmless: the wallet
        /// already counts this save's Diamonds, so the unmarked file on disk adds nothing when it is loaded again).</summary>
        public static WalletMigrationResult Run(AccountWalletStore store, string storeLogin, SaveData s, DateTime utc)
        {
            if (store == null || s == null) return new WalletMigrationResult { Outcome = WalletMigrationOutcome.Invalid, Identity = IdentityOf(s) };
            var w = store.Load(storeLogin);
            var r = Migrate(w, s, utc);
            if (!r.WalletChanged || store.Save(w)) return r;
            Undo(s, r);
            r.Outcome = WalletMigrationOutcome.Failed;
            return r;
        }

        /// <summary>Copies the save's ledger records the wallet lacks (cloned, never paid again) and carries a refund the
        /// save saw onto a wallet record that hadn't seen it.</summary>
        private static void MergeLedger(AccountWallet w, SaveData s, WalletMigrationResult r)
        {
            var granted = s.Purchases?.Granted;
            if (granted == null) return;
            foreach (var kv in granted)
            {
                if (string.IsNullOrEmpty(kv.Key) || kv.Value == null) continue;
                var rec = kv.Value;
                if (!w.Granted.TryGetValue(kv.Key, out var have))
                {
                    w.Granted[kv.Key] = new PurchaseRecord
                    {
                        ProductId = rec.ProductId, Diamonds = rec.Diamonds, Utc = rec.Utc, Store = rec.Store,
                        Refunded = rec.Refunded, ClawedBack = rec.ClawedBack,
                    };
                    r.Purchases++;
                }
                else if (rec.Refunded && !have.Refunded)
                {
                    have.Refunded = true;
                    have.ClawedBack = rec.ClawedBack;
                    r.Refunds++;
                }
            }
        }
    }
}
