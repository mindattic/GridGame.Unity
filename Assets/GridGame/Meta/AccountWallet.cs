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

        /// <summary>GG-T1447: Summon Tickets bought with real money. Never negative.</summary>
        public int Tickets;

        /// <summary>GG-T1447: the non-consumable products this login owns (<see cref="StoreCatalog"/> ids).</summary>
        public HashSet<string> Owned = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>GG-T1447: the cosmetic ids this login owns.</summary>
        public HashSet<string> Cosmetics = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>GG-T621: save identity (<see cref="WalletMigration.IdentityOf"/>) → what was carried in from that save,
        /// so a save, a copy of it or an older backup of it never pays its Diamonds into the wallet twice.</summary>
        public Dictionary<string, CarriedSave> Migrated = new Dictionary<string, CarriedSave>(StringComparer.Ordinal);

        public DateTime UpdatedUtc;

        /// <summary>GG-T1437: the age band asked once, at this login's first real-money purchase. Null until answered.</summary>
        public AgeBand? AgeBand;

        /// <summary>GG-T1437: the monthly spending limit in US cents set in Settings; null follows the age band's default
        /// (<see cref="SpendLimits.DefaultCents"/>), <see cref="SpendLimits.NoLimit"/> is none.</summary>
        public int? MonthlyLimitCents;

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
            Owned = Owned == null ? new HashSet<string>(StringComparer.Ordinal) : new HashSet<string>(Owned.Where(id => !string.IsNullOrEmpty(id)), StringComparer.Ordinal);
            Cosmetics = Cosmetics == null ? new HashSet<string>(StringComparer.Ordinal) : new HashSet<string>(Cosmetics.Where(id => !string.IsNullOrEmpty(id)), StringComparer.Ordinal);
            if (Diamonds < 0) Diamonds = 0;
            if (Tickets < 0) Tickets = 0;
            if (MonthlyLimitCents < SpendLimits.NoLimit) MonthlyLimitCents = SpendLimits.NoLimit;
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

        /// <summary>Grants <paramref name="productId"/>'s contents (<see cref="StoreCatalog"/>: Shards, Summon Tickets,
        /// cosmetics, ownership) for <paramref name="transactionId"/> unless this wallet granted it before. Paid contents
        /// ignore any cap or account limit: the money was taken, so they always land. A non-consumable the wallet already
        /// owns (a restore under a new transaction id) is recorded but grants nothing again (AlreadyGranted).
        /// <paramref name="diamonds"/> is the Shards added.</summary>
        public static GrantOutcome Grant(AccountWallet w, string transactionId, string productId, DateTime utc, string store, out int diamonds)
        {
            diamonds = 0;
            if (w == null || string.IsNullOrEmpty(transactionId)) return GrantOutcome.Invalid;
            w.Normalize();
            if (Has(w, transactionId)) return GrantOutcome.AlreadyGranted;
            var product = StoreCatalog.Of(productId);
            if (product == null) return GrantOutcome.UnknownProduct;
            var record = new PurchaseRecord { ProductId = productId, Utc = utc, Store = store };
            w.Granted[transactionId] = record;
            w.UpdatedUtc = utc;
            if (product.Type == ProductType.NonConsumable && StoreCatalog.Owns(w, product.Id)) return GrantOutcome.AlreadyGranted;
            w.Diamonds = Sum(w.Diamonds, product.Shards);
            w.Tickets = Sum(w.Tickets, product.Tickets);
            record.Diamonds = Math.Max(0, product.Shards);
            record.Tickets = Math.Max(0, product.Tickets);
            record.Cosmetics = product.Cosmetics.Where(c => !string.IsNullOrEmpty(c) && w.Cosmetics.Add(c)).ToArray();
            if (product.Type == ProductType.NonConsumable) record.Ownership = w.Owned.Add(product.Id);
            diamonds = record.Diamonds;
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

        /// <summary>A granted purchase was refunded: takes its Diamonds and Summon Tickets back, but only what's still in the
        /// wallet (never below zero, never touching any save's gold), and the cosmetics and ownership it granted.
        /// Idempotent; an unknown transaction does nothing. Returns the Diamonds taken.</summary>
        public static int Refund(AccountWallet w, string transactionId)
        {
            if (!Has(w, transactionId)) return 0;
            var r = w.Granted[transactionId];
            if (r.Refunded) return 0;
            w.Normalize();
            int take = Math.Min(Math.Max(0, r.Diamonds), Math.Max(0, w.Diamonds));
            w.Diamonds -= take;
            w.Tickets -= Math.Min(Math.Max(0, r.Tickets), w.Tickets);
            foreach (var c in r.Cosmetics ?? Array.Empty<string>()) w.Cosmetics.Remove(c);
            if (r.Ownership) w.Owned.Remove(r.ProductId);
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

    /// <summary>The age band a store login answered at its first real-money purchase (GG-T1437).</summary>
    public enum AgeBand
    {
        Under13,
        Teen,
        Adult,
    }

    /// <summary>
    /// Minors and spending (§25.18.1, GG-T1437): the age band is asked once per wallet before its first real-money purchase,
    /// each wallet has a monthly spending limit (minors get a default cap, editable in Settings behind a parental gate),
    /// and an under-13 passes the parental gate before every purchase. Pure rules; the App asks the questions.
    /// </summary>
    public static class SpendLimits
    {
        /// <summary>A <see cref="AccountWallet.MonthlyLimitCents"/> that means no limit.</summary>
        public const int NoLimit = -1;

        /// <summary>The default monthly cap for an under-13, in US cents.</summary>
        public static int Under13DefaultCents = 2000;

        /// <summary>The default monthly cap for a 13-to-17, in US cents.</summary>
        public static int TeenDefaultCents = 5000;

        /// <summary>The limits Settings cycles through: the age band's default (null), off, then caps, then none.</summary>
        public static readonly int?[] Choices = { null, 0, 1000, 2000, 5000, 10000, NoLimit };

        public static int DefaultCents(AgeBand? band) =>
            band == AgeBand.Under13 ? Under13DefaultCents : band == AgeBand.Teen ? TeenDefaultCents : NoLimit;

        /// <summary>The limit in force for <paramref name="w"/> in US cents, or <see cref="NoLimit"/>.</summary>
        public static int LimitCents(AccountWallet w) => w?.MonthlyLimitCents ?? DefaultCents(w?.AgeBand);

        /// <summary>Real money this wallet spent in <paramref name="utc"/>'s month, in US cents.</summary>
        public static int SpentCents(AccountWallet w, DateTime utc) => PurchaseLedger.MonthCents(w?.Granted?.Values, utc);

        public static bool NeedsAge(AccountWallet w) => w != null && w.AgeBand == null;

        /// <summary>An under-13's every purchase waits on the parental gate.</summary>
        public static bool NeedsParentalGate(AccountWallet w) => w?.AgeBand == AgeBand.Under13;

        /// <summary>Changing the limit in Settings waits on the parental gate unless the wallet answered adult.</summary>
        public static bool LimitGated(AccountWallet w) => w?.AgeBand != AgeBand.Adult;

        /// <summary>Why buying <paramref name="pack"/> now would break the monthly limit, or null when it fits.</summary>
        public static string Refusal(AccountWallet w, DiamondPack pack, DateTime utc) =>
            pack == null ? null : Refusal(w, pack.Cents, utc);

        /// <summary>Why buying the catalogue product <paramref name="product"/> now would break the monthly limit, or null
        /// when it fits.</summary>
        public static string Refusal(AccountWallet w, StoreProduct product, DateTime utc) =>
            product == null ? null : Refusal(w, product.Cents, utc);

        private static string Refusal(AccountWallet w, int cents, DateTime utc)
        {
            int limit = LimitCents(w);
            if (w == null || limit < 0) return null;
            int spent = SpentCents(w, utc);
            if ((long)spent + cents <= limit) return null;
            return limit == 0
                ? "Purchases are turned off. A parent can change the spending limit in Settings."
                : $"That would pass this month's spending limit of {Money(limit)} ({Money(Math.Max(0, limit - spent))} left). A parent can change it in Settings.";
        }

        /// <summary>The Settings choice after <paramref name="current"/> (an unlisted value goes back to the default).</summary>
        public static int? Next(int? current)
        {
            int i = Array.IndexOf(Choices, current);
            return Choices[(i + 1) % Choices.Length];
        }

        public static string Money(int cents) => cents < 0 ? "No limit" : $"${cents / 100}.{cents % 100:00}";

        /// <summary>The Settings row's value: "Default ($20.00)", "Off", "$50.00" or "No limit".</summary>
        public static string Describe(AccountWallet w)
        {
            if (w?.MonthlyLimitCents == null) return $"Default ({Money(DefaultCents(w?.AgeBand))})";
            return w.MonthlyLimitCents == 0 ? "Off" : Money(w.MonthlyLimitCents.Value);
        }

        public static string BandName(AgeBand band) =>
            band == AgeBand.Under13 ? "Under 13" : band == AgeBand.Teen ? "13 to 17" : "18 or older";

        /// <summary>A parental-gate sum a young child can't easily answer: <paramref name="a"/> × <paramref name="b"/>,
        /// with the right product among four shuffled choices.</summary>
        public static int[] GateQuestion(Random rng, out int a, out int b)
        {
            a = rng.Next(6, 10);
            b = rng.Next(3, 10);
            int p = a * b;
            var options = new[] { p, p + a, p - b, p + 10 };
            for (int i = options.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (options[i], options[j]) = (options[j], options[i]);
            }
            return options;
        }
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
                if (File.Exists(p))
                {
                    if (TryRead(p) != null) File.Copy(p, p + BackupSuffix, true);
                    else KeepCorrupt(p); // an unreadable file is set aside, never overwritten, so the balance can be recovered
                }
                AtomicFile.WriteAllText(p, Serialize(w));
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is JsonException) { return false; }
        }

        /// <summary>Renames an unreadable wallet file to <c>.corrupt</c> (a numbered name when one is already there).</summary>
        private static void KeepCorrupt(string p)
        {
            string dest = p + ".corrupt";
            for (int i = 1; File.Exists(dest); i++) dest = p + ".corrupt" + i;
            File.Move(p, dest);
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
