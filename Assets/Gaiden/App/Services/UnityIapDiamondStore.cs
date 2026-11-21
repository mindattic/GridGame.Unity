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

        public event Action<PurchaseResult> Delivered;

        public UnityIapDiamondStore() => Connect();

        public bool Connected => link == Link.Connected && productsReady;

        public string Status =>
            Connected ? "Purchases go through " + StoreName + "."
            : link == Link.Connecting ? "Connecting to " + StoreName + "…"
            : "Store unavailable" + (string.IsNullOrEmpty(problem) ? "" : ": " + problem) + ". Diamonds can still be exchanged for gold.";

        private static string StoreName => Application.platform == RuntimePlatform.IPhonePlayer ? "the App Store" : "Google Play";

        private async void Connect()
        {
            try
            {
                store = UnityIAPServices.StoreController();
                store.OnStoreConnected += OnStoreConnected;
                store.OnStoreDisconnected += d => { link = Link.Lost; problem = d?.Message; Debug.LogWarning("[IAP] store disconnected: " + d?.Message); };
                store.OnProductsFetched += OnProductsFetched;
                store.OnProductsFetchFailed += f => { problem = f?.FailureReason; Debug.LogWarning("[IAP] products fetch failed: " + f?.FailureReason); };
                store.OnPurchasePending += OnPurchasePending;
                store.OnPurchaseConfirmed += OnPurchaseConfirmed;
                store.OnPurchaseFailed += OnPurchaseFailed;
                store.OnPurchaseDeferred += OnPurchaseDeferred;
                store.OnPurchasesFetched += o => Debug.Log($"[IAP] purchases fetched: {o?.PendingOrders?.Count ?? 0} unfinished, {o?.DeferredOrders?.Count ?? 0} deferred");
                store.OnPurchasesFetchFailed += f => Debug.LogWarning("[IAP] purchases fetch failed: " + f?.Message);
                // An order paid for but never finished (the app died before ConfirmPurchase) comes back through
                // OnPurchasePending when purchases are fetched.
                store.ProcessPendingOrdersOnPurchasesFetched(true);
                await store.Connect();
            }
            catch (Exception e)
            {
                link = Link.Lost;
                problem = e.Message;
                Debug.LogWarning("[IAP] connect failed: " + e);
            }
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
            store.FetchProducts(DiamondStore.Packs.Select(p => new ProductDefinition(p.ProductId, ProductType.Consumable)).ToList());
        }

        private void OnProductsFetched(List<Product> products)
        {
            productsReady = true;
            Debug.Log("[IAP] products: " + string.Join(", ", products.Select(p => $"{p.definition.id}={p.metadata?.localizedPriceString}")));
            store.FetchPurchases();
        }

        private Product ProductOf(DiamondPack pack) => pack == null ? null : store?.GetProductById(pack.ProductId);

        public void Purchase(ContentDb c, SaveData s, DiamondPack pack, Action<PurchaseResult> done)
        {
            if (pack == null) { done?.Invoke(PurchaseResult.Of(PurchaseKind.Failed, "No such pack")); return; }
            if (!Connected) { done?.Invoke(PurchaseResult.Of(PurchaseKind.Unavailable, Status, pack.ProductId)); return; }
            if (inFlightId != null) { done?.Invoke(PurchaseResult.Of(PurchaseKind.Failed, "A purchase is already in progress.", pack.ProductId)); return; }
            var product = ProductOf(pack);
            if (product == null || !product.availableToPurchase)
            {
                done?.Invoke(PurchaseResult.Of(PurchaseKind.Unavailable, $"{pack.Diamonds:N0} Diamonds isn't on sale in this store right now.", pack.ProductId));
                return;
            }
            inFlightId = pack.ProductId;
            inFlightDone = done;
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
        private bool draining;

        private static string LedgerStore => Application.platform == RuntimePlatform.IPhonePlayer ? "AppleAppStore" : "GooglePlay";

        private static bool HasSave(GameApp app) => app != null && app.Save != null && app.Slot >= 0;

        private void OnPurchasePending(PendingOrder order)
        {
            var app = GameApp.I;
            if (!HasSave(app))
            {
                // GG-T469: nowhere to put the Diamonds yet. Hold the order (unfinished, so the store keeps it too) and grant
                // it the moment a save is loaded.
                if (!waiting.Contains(order)) waiting.Add(order);
                if (!draining && app != null) { app.Ticked += DrainWaiting; draining = true; }
                return;
            }
            Fulfil(app, order);
        }

        private void DrainWaiting(float dt)
        {
            var app = GameApp.I;
            if (!HasSave(app) || waiting.Count == 0) return;
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
                return;
            }
            if (GrantedInAnotherSlot(app, tx))
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
                Answer(id, PurchaseResult.Of(PurchaseKind.Granted, $"+{diamonds:N0} Diamonds, but the game couldn't save. Keep playing; it will be saved again.", id, diamonds));
                return;
            }
            store.ConfirmPurchase(order);
            Answer(id, PurchaseResult.Of(PurchaseKind.Granted, $"+{diamonds:N0} Diamonds. Thank you!", id, diamonds));
        }

        /// <summary>A re-delivered order granted in another slot of this profile before its confirm reached the store.</summary>
        private static bool GrantedInAnotherSlot(GameApp app, string tx)
        {
            for (int i = 0; i < SaveStore.Slots; i++)
            {
                if (i == app.Slot || !app.Saves.Exists(i)) continue;
                if (PurchaseLedger.Has(app.Saves.Load(i), tx)) return true;
            }
            return false;
        }

        private bool revokeHooked;

        /// <summary>GG-T469: Apple reported a purchase refunded. Take back what's left of it (never gold, never below zero).
        /// Google reports refunds of consumed purchases only to a server (the Voided Purchases API), which this game doesn't
        /// have, so on Android a refund after delivery isn't clawed back.</summary>
        private void OnRevoked(string productId)
        {
            var app = GameApp.I;
            if (!HasSave(app)) return;
            int taken = PurchaseLedger.RefundLatest(app.Save, productId);
            Debug.Log($"[IAP] entitlement revoked for {productId}: took back {taken} Diamonds");
            if (taken <= 0) return;
            app.Persist();
            Delivered?.Invoke(PurchaseResult.Of(PurchaseKind.Failed, $"A refunded purchase was reversed: {taken:N0} Diamonds removed.", productId));
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
            if (order is FailedOrder f) Debug.LogWarning($"[IAP] confirm failed for {ProductIdOf(order)}: {f.FailureReason} {f.Details}");
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
            Answer(id, PurchaseResult.Of(PurchaseKind.Pending, "Waiting for approval. The Diamonds arrive as soon as the purchase goes through.", id));
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
                    return PurchaseResult.Of(PurchaseKind.Pending, "An earlier purchase is still going through. Its Diamonds arrive when it clears.", id);
                case PurchaseFailureReason.PaymentDeclined:
                    return PurchaseResult.Of(PurchaseKind.Failed, "The payment was declined. Nothing was charged.", id);
                default:
                    return PurchaseResult.Of(PurchaseKind.Failed, $"The purchase didn't go through ({reason}). Nothing was charged.", id);
            }
        }
    }
}
#endif
