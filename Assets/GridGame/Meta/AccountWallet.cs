using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Unity.Shared.Core;

namespace GridGame.Meta
{
    /// <summary>
    /// The account wallet (§25.18.1, GG-T567/GG-T620): the Diamonds bought or earned on this device for one store login,
    /// and the ledger of every real-money transaction granted to it. Every profile and every save slot on the device
    /// reads the same wallet, so a purchase belongs to the store login that paid for it, not to whichever save was loaded.
    /// It lives in its own file beside the profiles (<see cref="AccountWalletStore"/>), never inside a save.
    /// </summary>
    public sealed class AccountWallet
    {
        public const int CurrentVersion = 1;

        public int Version = CurrentVersion;

        /// <summary>The login key this wallet belongs to (<see cref="AccountWalletStore.KeyOf"/>): a hash of the store
        /// login, or <see cref="AccountWalletStore.DeviceKey"/> when the store hasn't named one.</summary>
        public string Login;

        /// <summary>The Diamonds in the wallet. Never negative.</summary>
        public int Diamonds;

        /// <summary>Store transaction id (Google Play's purchase token, Apple's transaction id) → what it granted.</summary>
        public Dictionary<string, PurchaseRecord> Granted = new Dictionary<string, PurchaseRecord>(StringComparer.Ordinal);

        /// <summary>GG-T621: save identity (<see cref="WalletMigration.IdentityOf"/>) → what was carried in from that save,
        /// so a save, a copy of it or an older backup of it never pays its Diamonds into the wallet twice.</summary>
        public Dictionary<string, CarriedSave> Migrated = new Dictionary<string, CarriedSave>(StringComparer.Ordinal);

        public DateTime UpdatedUtc;

        /// <summary>Fills in anything an older or hand-edited file left out.</summary>
        public AccountWallet Normalize()
        {
            Granted ??= new Dictionary<string, PurchaseRecord>(StringComparer.Ordinal);
            if (Granted.Comparer != StringComparer.Ordinal)
                Granted = new Dictionary<string, PurchaseRecord>(Granted, StringComparer.Ordinal);
            foreach (var k in Granted.Where(kv => kv.Value == null).Select(kv => kv.Key).ToList()) Granted.Remove(k);
            Migrated ??= new Dictionary<string, CarriedSave>(StringComparer.Ordinal);
            if (Migrated.Comparer != StringComparer.Ordinal)
                Migrated = new Dictionary<string, CarriedSave>(Migrated, StringComparer.Ordinal);
            foreach (var k in Migrated.Where(kv => kv.Value == null).Select(kv => kv.Key).ToList()) Migrated.Remove(k);
            if (Diamonds < 0) Diamonds = 0;
            if (Version < CurrentVersion) Version = CurrentVersion;
            return this;
        }
    }

    /// <summary>
    /// The wallet's rules (GG-T620), the account-level twin of <see cref="PurchaseLedger"/>: each transaction pays out
    /// exactly once per wallet, refunds take back only what's still in it (never below zero), and spends never overdraw.
    /// Pure: callers persist with <see cref="AccountWalletStore.Save"/> and confirm a store order only once that write held.
    /// </summary>
    public static class AccountWallets
    {
        public static bool Has(AccountWallet w, string transactionId) =>
            w?.Granted != null && !string.IsNullOrEmpty(transactionId) && w.Granted.ContainsKey(transactionId);

        /// <summary>Grants <paramref name="productId"/>'s Diamonds for <paramref name="transactionId"/> unless this wallet
        /// granted it before. Paid Diamonds ignore any cap: the money was taken, so they always land.</summary>
        public static GrantOutcome Grant(AccountWallet w, string transactionId, string productId, DateTime utc, string store, out int diamonds)
        {
            diamonds = 0;
            if (w == null || string.IsNullOrEmpty(transactionId)) return GrantOutcome.Invalid;
            w.Normalize();
            if (Has(w, transactionId)) return GrantOutcome.AlreadyGranted;
            var pack = PurchaseLedger.PackOf(productId);
            if (pack == null) return GrantOutcome.UnknownProduct;
            w.Diamonds = Sum(w.Diamonds, pack.Diamonds);
            w.Granted[transactionId] = new PurchaseRecord { ProductId = productId, Diamonds = pack.Diamonds, Utc = utc, Store = store };
            w.UpdatedUtc = utc;
            diamonds = pack.Diamonds;
            return GrantOutcome.Granted;
        }

        /// <summary>Adds Diamonds that came without a store transaction (bought with gold, carried over from a save).
        /// Returns what was added (0 for a non-positive amount).</summary>
        public static int Add(AccountWallet w, int diamonds)
        {
            if (w == null || diamonds <= 0) return 0;
            int before = Math.Max(0, w.Diamonds);
            w.Diamonds = Sum(before, diamonds);
            return w.Diamonds - before;
        }

        public static bool CanSpend(AccountWallet w, int diamonds) => w != null && diamonds >= 0 && w.Diamonds >= diamonds;

        /// <summary>Takes <paramref name="diamonds"/> out of the wallet, all or nothing. False (and nothing taken) when it
        /// holds fewer.</summary>
        public static bool Spend(AccountWallet w, int diamonds)
        {
            if (!CanSpend(w, diamonds)) return false;
            w.Diamonds -= diamonds;
            return true;
        }

        /// <summary>A granted purchase was refunded: takes its Diamonds back, but only what's still in the wallet (never
        /// below zero, never touching any save's gold). Idempotent; an unknown transaction does nothing. Returns the
        /// Diamonds taken.</summary>
        public static int Refund(AccountWallet w, string transactionId)
        {
            if (!Has(w, transactionId)) return 0;
            var r = w.Granted[transactionId];
            if (r.Refunded) return 0;
            int take = Math.Min(Math.Max(0, r.Diamonds), Math.Max(0, w.Diamonds));
            w.Diamonds -= take;
            r.Refunded = true;
            r.ClawedBack = take;
            return take;
        }

        /// <summary>A refund reported by product only (Apple's revocation callback): refunds this wallet's most recent
        /// unrefunded purchase of it. Returns the Diamonds taken (0 when there is none).</summary>
        public static int RefundLatest(AccountWallet w, string productId)
        {
            var hit = w?.Granted?
                .Where(kv => kv.Value != null && kv.Value.ProductId == productId && !kv.Value.Refunded)
                .OrderByDescending(kv => kv.Value.Utc)
                .Select(kv => kv.Key)
                .FirstOrDefault();
            return hit == null ? 0 : Refund(w, hit);
        }

        /// <summary>Diamonds bought with real money into this wallet, refunds excluded.</summary>
        public static int TotalBought(AccountWallet w) =>
            w?.Granted?.Values.Where(r => r != null && !r.Refunded).Sum(r => r.Diamonds) ?? 0;

        private static int Sum(int a, int b) => (int)Math.Min(int.MaxValue, (long)Math.Max(0, a) + Math.Max(0, b));
    }

    /// <summary>
    /// Where wallets live (GG-T620): <c>Profiles/.account/wallet-&lt;key&gt;.json</c>, beside the profile folders and shared
    /// by all of them (the leading dot keeps <see cref="ProfileStore.List"/> from ever reading it as a profile). One file
    /// per store login, so each phone or login keeps its own Diamonds; the key is a hash, so no account name or email is
    /// ever written to disk. Writes are atomic with a rolling <c>.bak</c>, and a damaged file falls back to the backup
    /// instead of reading as an empty wallet.
    /// </summary>
    public sealed class AccountWalletStore
    {
        public const string FolderName = ".account";
        /// <summary>The key used until (or unless) the store names the signed-in login.</summary>
        public const string DeviceKey = "device";
        public const string BackupSuffix = ".bak";

        /// <summary>The folder the wallet files are in.</summary>
        public string Folder { get; }

        /// <summary>A store over <paramref name="profilesRoot"/>'s account folder (<see cref="ProfileStore.Root"/>).</summary>
        public AccountWalletStore(string profilesRoot)
        {
            if (string.IsNullOrWhiteSpace(profilesRoot)) throw new ArgumentException("A profiles folder is needed.", nameof(profilesRoot));
            Folder = Path.Combine(Path.GetFullPath(profilesRoot), FolderName);
        }

        public static AccountWalletStore For(ProfileStore profiles) =>
            new AccountWalletStore((profiles ?? throw new ArgumentNullException(nameof(profiles))).Root);

        /// <summary>The file key for a store login (a player id, account id or email): the first 16 hex digits of its
        /// SHA-256, so it is stable and filename-safe and reveals nothing; a blank login is <see cref="DeviceKey"/>.</summary>
        public static string KeyOf(string storeLogin)
        {
            if (string.IsNullOrWhiteSpace(storeLogin)) return DeviceKey;
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(storeLogin.Trim()));
                var sb = new StringBuilder(16);
                for (int i = 0; i < 8; i++) sb.Append(hash[i].ToString("x2"));
                return sb.ToString();
            }
        }

        /// <summary>Whether <paramref name="key"/> is already a file key (<see cref="DeviceKey"/> or 16 lowercase hex digits).</summary>
        public static bool IsKey(string key) =>
            key == DeviceKey || (key != null && key.Length == 16 && key.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')));

        public string PathFor(string storeLogin) => Path.Combine(Folder, "wallet-" + KeyOf(storeLogin) + ".json");

        /// <summary>Whether a wallet file (or its backup) exists for this login.</summary>
        public bool Exists(string storeLogin)
        {
            string p = PathFor(storeLogin);
            return File.Exists(p) || File.Exists(p + BackupSuffix);
        }

        /// <summary>The wallet for <paramref name="storeLogin"/> (null or blank: this device's): the file, else its backup
        /// when the file is missing or damaged, else a new empty wallet.</summary>
        public AccountWallet Load(string storeLogin = null)
        {
            string key = KeyOf(storeLogin), p = PathFor(storeLogin);
            var w = TryRead(p) ?? TryRead(p + BackupSuffix) ?? new AccountWallet();
            w.Login = key;
            return w.Normalize();
        }

        /// <summary>Writes the wallet under its <see cref="AccountWallet.Login"/> key, keeping the previous file as the
        /// backup. False when the write failed (the caller then leaves a store order unfinished so it comes back).</summary>
        public bool Save(AccountWallet w)
        {
            if (w == null) return false;
            try
            {
                w.Normalize();
                if (!IsKey(w.Login)) w.Login = KeyOf(w.Login);
                string p = Path.Combine(Folder, "wallet-" + w.Login + ".json");
                Directory.CreateDirectory(Folder);
                if (File.Exists(p) && TryRead(p) != null) File.Copy(p, p + BackupSuffix, true);
                AtomicFile.WriteAllText(p, Serialize(w));
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is JsonException) { return false; }
        }

        public static string Serialize(AccountWallet w) => JsonConvert.SerializeObject(w, Formatting.Indented, SaveFiles.Settings);

        public static AccountWallet Deserialize(string json) => JsonConvert.DeserializeObject<AccountWallet>(json, SaveFiles.Settings)?.Normalize();

        private static AccountWallet TryRead(string path)
        {
            try { return File.Exists(path) ? Deserialize(File.ReadAllText(path)) : null; }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is JsonException) { return null; }
        }
    }
}
