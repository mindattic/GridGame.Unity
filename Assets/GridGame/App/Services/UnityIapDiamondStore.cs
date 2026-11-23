// GG-T468: compiled only when com.unity.purchasing is in the project (GridGame.App.asmdef's versionDefines sets GG_IAP), so
// the dotnet build of the Unity-generated csproj still passes before Unity has regenerated it with the package.
#if GG_IAP
using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Content;
using GridGame.Meta;
using UnityEngine;
using UnityEngine.Purchasing;

namespace GridGame.App
{
    /// <summary>
    /// The real-money Diamond store (§25.18.1, GG-T468): Unity IAP 5 over Google Play Billing on Android and StoreKit on
    /// iOS, selling the four consumable packs (<see cref="DiamondStore.Packs"/>, product ids <c>diamonds_100</c> …
    /// <c>diamonds_2500</c>). Release builds on Android/iOS use it (<see cref="DiamondStores.Current"/>); editor and
    /// development builds keep the free <see cref="DevDiamondStore"/>.
    ///
    /// The flow is the store's: <c>PurchaseProduct</c> → <c>OnPurchasePending</c> (paid, not yet finished) → grant the
    /// Diamonds and write the save → <c>ConfirmPurchase</c> (Google consumes, Apple finishes the transaction). The grant is
    /// saved before the confirm, so a crash in between re-delivers the order on the next launch instead of losing it.
    /// </summary>
    public sealed class UnityIapDiamondStore : IDiamondStore
    {
        private enum Link { Connecting, Connected, Lost }

        private StoreController store;
        private Link link = Link.Connecting;
        private string problem;
        private bool productsReady;

        /// <summary>The purchase the player started, answered once (by a grant, a deferral, a cancel or a failure).</summary>
        private string inFlightId;
        private Action<PurchaseResult> inFlightDone;
        private float inFlightSince;

        /// <summary>A purchase the store never answers (a disconnect mid-sheet, an order with no transaction id) stops
        /// locking the packs after this long; if it went through, its order arrives later and lands through
        /// <see cref="Delivered"/>.</summary>
        public const float InFlightTimeout = 120f;

        public event Action<PurchaseResult> Delivered;

        public UnityIapDiamondStore()
        {
            IapPump.Attach(this);
            Connect();
        }

        public bool Connected => link == Link.Connected && productsReady;

        // Recovery (the pump's Tick): a connect or product fetch that failed is retried with backoff, and unfinished
        // orders are fetched again while any is owed a confirm (Google refunds a purchase left unacknowledged for 3 days).
        private bool connecting;
        private bool productsFailed;
        private float retryIn;
        private float retryDelay = RetryMin;
        private const float RetryMin = 5f, RetryMax = 300f;
        /// <summary>An order was granted but couldn't be confirmed yet (save write failed, confirm failed, no transaction
        /// id, or the purchases fetch failed): fetch purchases again so it comes back through OnPurchasePending.</summary>
        private bool refetch;
        private float refetchIn;
        private const float RefetchEvery = 60f;

        /// <summary>The store screen opened: retry a failed connection now instead of waiting out the backoff.</summary>
        public void Nudge()
        {
            if (!Connected) retryIn = 0f;
        }

        /// <summary>Forgets the purchase in flight without answering it (the store screen timed it out).</summary>
        public void AbandonInFlight()
        {
            inFlightId = null;
            inFlightDone = null;
        }

        /// <summary>Answers the purchase in flight as still pending (it may yet go through) and frees the packs.</summary>
        private void ReleaseInFlight(string message)
        {
            if (inFlightId == null) return;
            var id = inFlightId;
            var done = inFlightDone;
            AbandonInFlight();
            done?.Invoke(PurchaseResult.Of(PurchaseKind.Pending, message, id));
        }

        internal void Tick(float dt)
        {
            if (inFlightId != null && Time.realtimeSinceStartup - inFlightSince > InFlightTimeout)
                ReleaseInFlight($"The store hasn't answered yet. If the purchase went through, the {ShardText.ShortPlural} arrive on their own.");

            if (waiting.Count > 0) DrainWaiting();

            if (!Connected && !connecting && (retryIn -= dt) <= 0f)
            {
                retryIn = retryDelay;
                retryDelay = Math.Min(RetryMax, retryDelay * 2f);
                if (store == null || link == Link.Lost) Connect();
                else if (link == Link.Connected && !productsReady && productsFailed) FetchProducts();
            }

            if (refetch && Connected && (refetchIn -= dt) <= 0f)
            {
                refetchIn = RefetchEvery;
                refetch = false; // set again by whatever still can't be confirmed
                store.FetchPurchases();
            }
        }

        private void NeedRefetch()
        {
            if (!refetch) refetchIn = RefetchEvery;
            refetch = true;
        }

        public string Status =>
            Connected ? "Purchases go through " + StoreName + "."
            : link == Link.Connecting ? "Connecting to " + StoreName + "…"
            : "Store unavailable" + (string.IsNullOrEmpty(problem) ? "" : ": " + problem) + ". " + ShardText.Plural + " can still be exchanged for gold.";

        private static string StoreName => Application.platform == RuntimePlatform.IPhonePlayer ? "the App Store" : "Google Play";

        /// <summary>Connects (or, after a failure or a disconnect, reconnects: the controller and its handlers are made once).</summary>
        private async void Connect()
        {
            if (connecting) return;
            connecting = true;
            link = Link.Connecting;
            try
            {
                if (store == null)
                {
                    var controller = UnityIAPServices.StoreController();
                    controller.OnStoreConnected += OnStoreConnected;
                    controller.OnStoreDisconnected += OnStoreDisconnected;
                    controller.OnProductsFetched += OnProductsFetched;
                    controller.OnProductsFetchFailed += f => { productsFailed = true; problem = f?.FailureReason; Debug.LogWarning("[IAP] products fetch failed: " + f?.FailureReason); };
                    controller.OnPurchasePending += OnPurchasePending;
                    controller.OnPurchaseConfirmed += OnPurchaseConfirmed;
                    controller.OnPurchaseFailed += OnPurchaseFailed;
                    controller.OnPurchaseDeferred += OnPurchaseDeferred;
                    controller.OnPurchasesFetched += o => Debug.Log($"[IAP] purchases fetched: {o?.PendingOrders?.Count ?? 0} unfinished, {o?.DeferredOrders?.Count ?? 0} deferred");
                    controller.OnPurchasesFetchFailed += f => { NeedRefetch(); Debug.LogWarning("[IAP] purchases fetch failed: " + f?.Message); };
                    // An order paid for but never finished (the app died before ConfirmPurchase) comes back through
                    // OnPurchasePending when purchases are fetched.
                    controller.ProcessPendingOrdersOnPurchasesFetched(true);
                    store = controller;
                }
                await store.Connect();
            }
            catch (Exception e)
            {
                link = Link.Lost;
                problem = e.Message;
                Debug.LogWarning("[IAP] connect failed (will retry): " + e);
            }
            finally { connecting = false; }
        }

        private void OnStoreDisconnected(StoreConnectionFailureDescription d)
        {
            link = Link.Lost;
            problem = d?.Message;
            Debug.LogWarning("[IAP] store disconnected (will retry): " + d?.Message);
            // The sheet's answer may never come now: free the packs. A purchase that did go through comes back as an
            // unfinished order once the store reconnects.
            ReleaseInFlight($"The connection to the store dropped. If the purchase went through, the {ShardText.ShortPlural} arrive on their own.");
        }

        private void FetchProducts()
        {
            productsFailed = false;
            store.FetchProducts(DiamondStore.Packs.Select(p => new ProductDefinition(p.ProductId, ProductType.Consumable)).ToList());
        }

        private void OnStoreConnected()
        {
            link = Link.Connected;
            problem = null;
            if (!revokeHooked && store.AppleStoreExtendedPurchaseService != null)
            {
                store.AppleStoreExtendedPurchaseService.OnEntitlementRevoked += OnRevoked;
                revokeHooked = true;
            }
            FetchProducts();
        }

        private void OnProductsFetched(List<Product> products)
        {
            productsReady = true;
            productsFailed = false;
            retryDelay = RetryMin;
            Debug.Log("[IAP] products: " + string.Join(", ", products.Select(p => $"{p.definition.id}={p.metadata?.localizedPriceString}")));
            store.FetchPurchases();
        }

        private Product ProductOf(DiamondPack pack) => pack == null ? null : store?.GetProductById(pack.ProductId);

        /// <summary>GG-T470: the store's own localized price string, once products are fetched.</summary>
        public string PriceOf(DiamondPack pack)
        {
            if (!productsReady) return null;
            var s = ProductOf(pack)?.metadata?.localizedPriceString;
            return string.IsNullOrWhiteSpace(s) ? null : s;
        }
        public void Purchase(ContentDb c, SaveData s, DiamondPack pack, Action<PurchaseResult> done)
        {
            if (pack == null) { done?.Invoke(PurchaseResult.Of(PurchaseKind.Failed, "No such pack")); return; }
            if (!Connected) { done?.Invoke(PurchaseResult.Of(PurchaseKind.Unavailable, Status, pack.ProductId)); return; }
            if (inFlightId != null && Time.realtimeSinceStartup - inFlightSince > InFlightTimeout) AbandonInFlight();
            if (inFlightId != null) { done?.Invoke(PurchaseResult.Of(PurchaseKind.Failed, "A purchase is already in progress.", pack.ProductId)); return; }
            var product = ProductOf(pack);
            if (product == null || !product.availableToPurchase)
            {
                done?.Invoke(PurchaseResult.Of(PurchaseKind.Unavailable, $"{ShardText.Of(pack.Diamonds)} isn't on sale in this store right now.", pack.ProductId));
                return;
            }
            inFlightId = pack.ProductId;
            inFlightDone = done;
            inFlightSince = Time.realtimeSinceStartup;
            store.PurchaseProduct(product);
        }

        private void Answer(string productId, PurchaseResult r)
        {
            if (inFlightId != null && (productId == null || productId == inFlightId))
            {
                var done = inFlightDone;
                inFlightId = null;
                inFlightDone = null;
                done?.Invoke(r);
            }
            else Delivered?.Invoke(r);
        }

        private static string ProductIdOf(Order order) => order?.CartOrdered?.Items()?.FirstOrDefault()?.Product?.definition?.id;

        /// <summary>Paid orders that arrived with no save loaded (the title screen): granted to the first save loaded.</summary>
        private readonly List<PendingOrder> waiting = new List<PendingOrder>();

        private static string LedgerStore => Application.platform == RuntimePlatform.IPhonePlayer ? "AppleAppStore" : "GooglePlay";

        private static bool HasSave(GameApp app) => app != null && app.Save != null && app.Slot >= 0 && app.Saves != null;

        /// <summary>GG-T702: the account wallet is bound, so a paid order needs no save loaded: it lands in the wallet.</summary>
        private static bool HasWallet(GameApp app) => app != null && app.WalletStore != null && DiamondExchange.Wallet != null;

        private static bool CanGrant(GameApp app) => HasWallet(app) || HasSave(app);

        private void OnPurchasePending(PendingOrder order)
        {
            var app = GameApp.I;
            if (!CanGrant(app))
            {
                // GG-T469: nowhere to put the Diamonds yet. Hold the order (unfinished, so the store keeps it too) and grant
                // it the moment a save is loaded: the pump (IapPump) drains it every frame, whether or not the app object
                // existed when the order arrived.
                if (!waiting.Contains(order)) waiting.Add(order);
                return;
            }
            Fulfil(app, order);
        }

        private void DrainWaiting()
        {
            var app = GameApp.I;
            if (!CanGrant(app) || waiting.Count == 0) return;
            var batch = waiting.ToList();
            waiting.Clear();
            foreach (var o in batch) Fulfil(app, o);
        }

        /// <summary>GG-T469: grant through the ledger (once per transaction id), write the save, and only then finish the
        /// order with the store. A failed write leaves the order unfinished: it comes back, and the in-memory ledger keeps
        /// this session from paying it twice.</summary>
        private void Fulfil(GameApp app, PendingOrder order)
        {
            var id = ProductIdOf(order);
            var tx = order?.Info?.TransactionID;
            if (string.IsNullOrEmpty(tx))
            {
                Debug.LogWarning("[IAP] pending order without a transaction id, left unfinished: " + id);
                NeedRefetch(); // it comes back with its id on a later fetch
                if (inFlightId != null && id == inFlightId)
                    ReleaseInFlight($"The store is still processing the purchase. The {ShardText.ShortPlural} arrive as soon as it clears.");
                return;
            }
            if (HasWallet(app)) { FulfilToWallet(app, order, id, tx); return; }
            if (GrantedElsewhere(app, tx))
            {
                Debug.Log($"[IAP] {tx} was already granted in another save slot; finishing it");
                store.ConfirmPurchase(order);
                Answer(id, PurchaseResult.Of(PurchaseKind.Granted, "That purchase was already delivered to another save.", id));
                return;
            }
            var outcome = PurchaseLedger.Grant(app.Save, tx, id, DateTime.UtcNow, LedgerStore, out int diamonds);
            switch (outcome)
            {
                case GrantOutcome.UnknownProduct:
                    // Not one of ours (a retired product): finish it so the store stops re-delivering it.
                    Debug.LogWarning("[IAP] unknown product in a pending order: " + id);
                    store.ConfirmPurchase(order);
                    Answer(id, PurchaseResult.Of(PurchaseKind.Failed, "That product isn't sold any more.", id));
                    return;
                case GrantOutcome.AlreadyGranted:
                    // Granted earlier this session; finish it only once that grant is on disk (an earlier write may have
                    // failed), else it waits for the next fetch.
                    if (!PurchaseLedger.FileHas(SaveFiles.PathFor(app.Saves.Root, app.Slot), tx) && !app.Saves.Write(app.Slot, app.Save))
                    {
                        Debug.LogWarning($"[IAP] {tx} re-delivered, granted but still not saved; leaving it unfinished");
                        NeedRefetch();
                        return;
                    }
                    Debug.Log($"[IAP] {tx} re-delivered; already granted, finishing it");
                    store.ConfirmPurchase(order);
                    Answer(id, PurchaseResult.Of(PurchaseKind.Granted, "That purchase was already delivered.", id));
                    return;
                case GrantOutcome.Invalid:
                    return;
            }
            if (!app.Saves.Write(app.Slot, app.Save))
            {
                Debug.LogWarning($"[IAP] save write failed after granting {tx}; leaving the order unfinished so it comes back");
                NeedRefetch();
                Answer(id, PurchaseResult.Of(PurchaseKind.Granted, $"+{ShardText.Of(diamonds)}, but the game couldn't save. Keep playing; it will be saved again.", id, diamonds));
                return;
            }
            store.ConfirmPurchase(order);
            Answer(id, PurchaseResult.Of(PurchaseKind.Granted, $"+{ShardText.Of(diamonds)}. Thank you!", id, diamonds));
        }

        /// <summary>GG-T702: grant into the account wallet (once per transaction id, across every profile and slot), write
        /// the wallet, and only then finish the order. A failed write leaves the order unfinished so it comes back; the
        /// in-memory wallet keeps this session from paying it twice.</summary>
        private void FulfilToWallet(GameApp app, PendingOrder order, string id, string tx)
        {
            var w = DiamondExchange.Wallet;
            if (AccountWallets.Has(w, tx))
            {
                if (!app.PersistWallet())
                {
                    Debug.LogWarning($"[IAP] {tx} re-delivered, granted but the wallet still isn't saved; leaving it unfinished");
                    NeedRefetch();
                    return;
                }
                Debug.Log($"[IAP] {tx} re-delivered; already in the wallet, finishing it");
                store.ConfirmPurchase(order);
                Answer(id, PurchaseResult.Of(PurchaseKind.Granted, "That purchase was already delivered.", id));
                return;
            }
            if (GrantedElsewhere(app, tx))
            {
                // A save that couldn't be moved into the wallet (it won't read) booked it: it isn't paid twice.
                Debug.Log($"[IAP] {tx} was already granted in a save slot; finishing it");
                store.ConfirmPurchase(order);
                Answer(id, PurchaseResult.Of(PurchaseKind.Granted, "That purchase was already delivered to another save.", id));
                return;
            }
            var outcome = AccountWallets.Grant(w, tx, id, DateTime.UtcNow, LedgerStore, out int diamonds);
            if (outcome == GrantOutcome.UnknownProduct)
            {
                Debug.LogWarning("[IAP] unknown product in a pending order: " + id);
                store.ConfirmPurchase(order);
                Answer(id, PurchaseResult.Of(PurchaseKind.Failed, "That product isn't sold any more.", id));
                return;
            }
            if (outcome != GrantOutcome.Granted) return;
            DiamondExchange.WalletDirty = true;
            if (!app.PersistWallet())
            {
                Debug.LogWarning($"[IAP] wallet write failed after granting {tx}; leaving the order unfinished so it comes back");
                NeedRefetch();
                Answer(id, PurchaseResult.Of(PurchaseKind.Granted, $"+{ShardText.Of(diamonds)}, but the game couldn't save. Keep playing; it will be saved again.", id, diamonds));
                return;
            }
            store.ConfirmPurchase(order);
            Answer(id, PurchaseResult.Of(PurchaseKind.Granted, $"+{ShardText.Of(diamonds)}. Thank you!", id, diamonds));
        }

        /// <summary>A re-delivered order granted in another save slot, of this profile or any other, before its confirm
        /// reached the store. Read-only: each slot file (and its rolling backup) is only parsed for the transaction id, never
        /// loaded, migrated or repaired, so a damaged slot is never rewritten from here.</summary>
        private static bool GrantedElsewhere(GameApp app, string tx)
        {
            try
            {
                string current = Full(SaveFiles.PathFor(app.Saves.Root, app.Slot));
                var folders = new List<string> { app.Saves.Root };
                var profiles = app.Profile?.Store;
                if (profiles != null)
                    foreach (var name in profiles.List())
                        folders.Add(System.IO.Path.Combine(profiles.Root, name, "Saves"));
                foreach (var folder in folders.Select(Full).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if (!System.IO.Directory.Exists(folder)) continue;
                    for (int i = 0; i < SaveFiles.Slots; i++)
                    {
                        string p = SaveFiles.PathFor(folder, i);
                        if (string.Equals(Full(p), current, StringComparison.OrdinalIgnoreCase)) continue;
                        if (PurchaseLedger.FileHas(p, tx) || PurchaseLedger.FileHas(p + ".bak", tx)) return true;
                    }
                }
            }
            catch (Exception e) { Debug.LogWarning("[IAP] couldn't scan the other saves: " + e.Message); }
            return false;
        }

        private static string Full(string path) => System.IO.Path.GetFullPath(path);

        private bool revokeHooked;

        /// <summary>GG-T469: Apple reported a purchase refunded. Take back what's left of it (never gold, never below zero).
        /// Google reports refunds of consumed purchases only to a server (the Voided Purchases API), which this game doesn't
        /// have, so on Android a refund after delivery isn't clawed back.</summary>
        private void OnRevoked(string productId)
        {
            var app = GameApp.I;
            if (HasWallet(app))
            {
                // GG-T702: purchases are booked in the account wallet, so the refund comes out of it (what's left, never gold).
                int fromWallet = AccountWallets.RefundLatest(DiamondExchange.Wallet, productId);
                DiamondExchange.WalletDirty = true; // the record is marked refunded even when nothing was left to take
                app.PersistWallet();
                Debug.Log($"[IAP] entitlement revoked for {productId}: took back {fromWallet} Diamonds from the wallet");
                if (fromWallet > 0)
                    Delivered?.Invoke(PurchaseResult.Of(PurchaseKind.Failed, $"A refunded purchase was reversed: {ShardText.Of(fromWallet)} removed.", productId));
                return;
            }
            if (!HasSave(app)) return;
            int taken = PurchaseLedger.RefundLatest(app.Save, productId);
            Debug.Log($"[IAP] entitlement revoked for {productId}: took back {taken} Diamonds");
            if (taken <= 0) return;
            app.Persist();
            Delivered?.Invoke(PurchaseResult.Of(PurchaseKind.Failed, $"A refunded purchase was reversed: {ShardText.Of(taken)} removed.", productId));
        }

        public bool CanRestore => Connected && Application.platform == RuntimePlatform.IPhonePlayer;

        public void Restore(Action<PurchaseResult> done)
        {
            if (!Connected) { done?.Invoke(PurchaseResult.Of(PurchaseKind.Unavailable, Status)); return; }
            // Google Play re-sends unfinished orders on every fetch; Apple's restore re-sends unfinished transactions.
            // Either way anything found arrives through OnPurchasePending and the ledger grants it at most once.
            store.FetchPurchases();
            store.RestoreTransactions((ok, error) => done?.Invoke(ok
                ? PurchaseResult.Of(PurchaseKind.Granted, "Purchases checked. Anything unfinished has been delivered.")
                : PurchaseResult.Of(PurchaseKind.Failed, "Couldn't check purchases" + (string.IsNullOrEmpty(error) ? "." : ": " + error))));
        }

        private void OnPurchaseConfirmed(Order order)
        {
            if (order is FailedOrder f)
            {
                // The grant is saved; only the store's finish failed. Fetch again later: the order comes back unfinished,
                // the ledger answers AlreadyGranted and it is confirmed again (an unacknowledged Google purchase is refunded
                // after three days).
                Debug.LogWarning($"[IAP] confirm failed for {ProductIdOf(order)}: {f.FailureReason} {f.Details}; will retry");
                NeedRefetch();
            }
        }

        private void OnPurchaseFailed(FailedOrder order)
        {
            var id = ProductIdOf(order);
            Debug.Log($"[IAP] purchase failed {id}: {order.FailureReason} {order.Details}");
            Answer(id, FailureOf(order.FailureReason, id));
        }

        private void OnPurchaseDeferred(DeferredOrder order)
        {
            var id = ProductIdOf(order);
            Answer(id, PurchaseResult.Of(PurchaseKind.Pending, $"Waiting for approval. The {ShardText.ShortPlural} arrive as soon as the purchase goes through.", id));
        }

        internal static PurchaseResult FailureOf(PurchaseFailureReason reason, string id)
        {
            switch (reason)
            {
                case PurchaseFailureReason.UserCancelled:
                    return PurchaseResult.Of(PurchaseKind.Cancelled, "Purchase cancelled. Nothing was charged.", id);
                case PurchaseFailureReason.PurchasingUnavailable:
                case PurchaseFailureReason.StoreNotConnected:
                case PurchaseFailureReason.ProductUnavailable:
                case PurchaseFailureReason.NotSupported:
                case PurchaseFailureReason.UserNotAuthenticated:
                    return PurchaseResult.Of(PurchaseKind.Unavailable, "The store isn't available right now. Nothing was charged.", id);
                case PurchaseFailureReason.ExistingPurchasePending:
                    return PurchaseResult.Of(PurchaseKind.Pending, $"An earlier purchase is still going through. Its {ShardText.ShortPlural} arrive when it clears.", id);
                case PurchaseFailureReason.PaymentDeclined:
                    return PurchaseResult.Of(PurchaseKind.Failed, "The payment was declined. Nothing was charged.", id);
                default:
                    return PurchaseResult.Of(PurchaseKind.Failed, $"The purchase didn't go through ({reason}). Nothing was charged.", id);
            }
        }
    }

    /// <summary>Drives <see cref="UnityIapDiamondStore.Tick"/> every frame from its own persistent object, so parked orders
    /// are granted once a save is loaded, failed connects are retried and owed confirms are fetched again, without
    /// depending on GameApp existing when the store was made (it can be warmed before the app object is up).</summary>
    internal sealed class IapPump : MonoBehaviour
    {
        private UnityIapDiamondStore owner;

        public static void Attach(UnityIapDiamondStore store)
        {
            try
            {
                var go = new GameObject("[IAP pump]") { hideFlags = HideFlags.HideAndDontSave };
                DontDestroyOnLoad(go);
                go.AddComponent<IapPump>().owner = store;
            }
            catch (Exception e) { Debug.LogWarning("[IAP] couldn't start the store pump: " + e.Message); }
        }

        private void Update()
        {
            try { owner?.Tick(Time.unscaledDeltaTime); }
            catch (Exception e) { Debug.LogWarning("[IAP] pump: " + e); }
        }
    }
}
#endif
