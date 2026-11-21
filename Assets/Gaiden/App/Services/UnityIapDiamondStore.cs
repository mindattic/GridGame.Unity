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

        private void OnPurchasePending(PendingOrder order)
        {
            var id = ProductIdOf(order);
            var pack = DiamondStore.Packs.FirstOrDefault(p => p.ProductId == id);
            var app = GameApp.I;
            if (pack == null)
            {
                // Not one of ours (a retired product): finish it so the store stops re-delivering it.
                Debug.LogWarning("[IAP] unknown product in a pending order: " + id);
                store.ConfirmPurchase(order);
                return;
            }
            if (app?.Save == null) return; // no save loaded yet: left unfinished, re-delivered on the next fetch
            var r = DiamondExchange.Grant(app.Content, app.Save, pack.Diamonds);
            app.Persist();
            store.ConfirmPurchase(order);
            Answer(id, r.Ok
                ? PurchaseResult.Of(PurchaseKind.Granted, $"+{pack.Diamonds:N0} Diamonds. Thank you!", id, pack.Diamonds)
                : PurchaseResult.Of(PurchaseKind.Failed, r.Message, id));
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
