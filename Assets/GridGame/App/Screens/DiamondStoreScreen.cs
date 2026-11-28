using System;
using GridGame.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>Which real-money store this build talks to (§25.18.1, GG-T455), and the way into the Diamond Store from any
    /// "not enough Diamonds" moment.</summary>
    public static class DiamondStores
    {
        private static IDiamondStore current;

        /// <summary>Editor and development builds: the dev stub, which grants packs for free (clearly marked on screen).
        /// Release builds on Android and iOS: Unity IAP over Google Play Billing / StoreKit (GG-T468). Any other release
        /// build (desktop, WebGL): the placeholder that says "Store not connected yet".</summary>
        public static IDiamondStore Current => current ??= Choose();

        private static IDiamondStore Choose()
        {
            IDiamondStore s;
            if (Application.isEditor || Debug.isDebugBuild) s = new DevDiamondStore();
#if GG_IAP
            else if (Application.platform == RuntimePlatform.Android || Application.platform == RuntimePlatform.IPhonePlayer) s = new UnityIapDiamondStore();
#endif
            else s = new UnconnectedDiamondStore();
            s.Delivered += OnDelivered;
            return s;
        }

        /// <summary>Connects the release store at boot, so an order left unfinished last session lands as soon as a save
        /// is loaded rather than on the first visit to the Diamond Store. GG-T702: called by GameApp once every save is
        /// moved into the account wallet, never before.</summary>
        public static void Warm()
        {
            if (Features.DiamondStore) _ = Current;
        }

        /// <summary>A purchase finished outside the store screen's own call (approved later, or left unfinished by a
        /// crash): say so. The store has already granted it and written the save.</summary>
        private static void OnDelivered(PurchaseResult r)
        {
            if (r == null || GameApp.I == null) return;
            var current = GameApp.I.Router?.Current;
            var ds = current as DiamondStoreScreen ?? (current as ShopHubScreen)?.Shards;
            if (ds != null) { ds.Show(r); return; }
            GameApp.I.Toast(r.Message, 3f);
            GameApp.I.Audio?.Sfx(r.Ok ? "ui.diamond" : "ui.error");
        }

        /// <summary>The product id being bought (or "restore") while the store's sheet is up, null otherwise. Static so the
        /// lockout holds if the player leaves and comes back mid-purchase (GG-T470).</summary>
        public static string InFlight
        {
            get => inFlight;
            set { inFlight = value; InFlightSince = Time.realtimeSinceStartup; }
        }
        private static string inFlight;
        public static float InFlightSince { get; private set; }

        public static void Open() => GameApp.I.Router.PushOrReturn(new DiamondStoreScreen());

        /// <summary>A Diamond price couldn't be paid: say why, and offer the store.</summary>
        public static void OfferStore(string why)
        {
            GameApp.I.Audio.Sfx("ui.error");
            Confirm.Show($"{why}\nOpen the {ShardText.Store}?", ShardText.Store, Open);
        }
    }

    /// <summary>The age question and the parental gate (§25.18.1, GG-T1437/GG-T1559): modal veils on the overlay, named like
    /// <see cref="Confirm"/>'s so Android Back cancels them and a navigation clears them.</summary>
    public static class SpendGate
    {
        private static readonly System.Random Rng = new System.Random();

        private static (VisualElement veil, VisualElement window, Action cancel) Veil(string question, string name)
        {
            var w = Ui.Modal(question, null, name, false);
            var veil = w.parent;
            Action cancel = () => veil.RemoveFromHierarchy();
            return (veil, w, cancel);
        }

        /// <summary>Asks the wallet's age band once, stores and writes it, then runs <paramref name="then"/>. Cancel asks again
        /// next purchase.</summary>
        public static void AskAge(AccountWallet wallet, Action then)
        {
            var (veil, w, cancel) = Veil("Before your first purchase: how old are you?", "age-question");
            var row = Ui.El("col kit-gate", w);
            foreach (AgeBand band in Enum.GetValues(typeof(AgeBand)))
            {
                var b = band;
                RowIcon.IconButton(SpendLimits.BandName(b), RowKind.Enter, () =>
                {
                    veil.RemoveFromHierarchy();
                    wallet.AgeBand = b;
                    DiamondExchange.WalletDirty = true;
                    GameApp.I.PersistWallet();
                    then?.Invoke();
                }, "btn--primary", row).name = "age-" + b.ToString().ToLowerInvariant();
            }
            var foot = Ui.El("kit-footer kit-footer--end", w);
            RowIcon.IconButton("Cancel", RowKind.Close, cancel, null, foot).name = "confirm-no";
        }

        /// <summary>The parental gate: a multiplication a young child can't easily answer. The right product runs
        /// <paramref name="onPass"/>; a wrong one closes the gate and nothing happens.</summary>
        public static void Parental(Action onPass)
        {
            var options = SpendLimits.GateQuestion(Rng, out int a, out int b);
            int answer = a * b;
            var (veil, w, cancel) = Veil($"Ask a grown-up. What is {a} × {b}?", "parental-gate");
            var row = Ui.El("kit-footer kit-gate", w);
            foreach (var o in options)
            {
                int pick = o;
                RowIcon.IconButton(pick.ToString(), RowKind.Enter, () =>
                {
                    veil.RemoveFromHierarchy();
                    if (pick == answer) { onPass?.Invoke(); return; }
                    GameApp.I.Toast("That's not right. Ask a grown-up to help.", 2.5f);
                    GameApp.I.Audio?.Sfx("ui.error");
                }, null, row).name = "gate-answer-" + pick;
            }
            var foot = Ui.El("kit-footer kit-footer--end", w);
            RowIcon.IconButton("Cancel", RowKind.Close, cancel, null, foot).name = "confirm-no";
        }
    }

    /// <summary>
    /// The Diamond Store (§25.18.1, GG-T455): the balance, the gold exchange (pick an amount, see the gold, exchange at once)
    /// and the real-money packs at the flat 100 Diamonds = $1.00. Reached from Camp's Diamonds row and from every Diamond
    /// price the player can't pay.
    /// </summary>
    public sealed class DiamondStoreScreen : HubPage
    {
        public override string Id => "diamonds";
        protected override string Title => ShardText.Store; // GG-T568: Aetherite Shards, was Diamonds
        protected override string Backdrop => "cityruins";

        private int amount = 100;
        private static readonly int[] Steps = { -100, -10, 10, 100 };

        protected override void BuildBody(VisualElement page)
        {
            // GG-T1974: the balance, exchange and packs scroll; the footer stays under them, above the bottom nav.
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("list");
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.touchScrollBehavior = ScrollView.TouchScrollBehavior.Clamped;
            page.Add(scroll);
            var body = scroll.contentContainer;
            int have = DiamondExchange.Have(S);
            int max = DiamondExchange.MaxBuy(C, S);
            amount = Math.Max(1, amount);

            var bal = Ui.Panel(body, null, false, "diamond-balance");
            var head = Ui.El("row", bal);
            head.style.alignItems = Align.Center;
            var img = head.KindIcon(RowKind.Diamond);
            img.style.width = img.style.height = Size.Icon;
            img.AddToClassList("icon-gap");
            Ui.Heading(ShardText.Of(have), head, "gold").name = "diamond-count";
            Ui.Caption($"1 {ShardText.Short} = {DiamondExchange.GoldPerDiamond} G · {ShardText.ShortOf(DiamondExchange.DiamondsPerDollar)} = $1.00", bal, "dim");
            Ui.Para($"{ShardText.Plural} pay for a random summon, an instant Rested refill and a lucky craft. Anything priced in {ShardText.ShortPlural} can also be paid in gold at the same rate.", bal, "dim");

            var ex = Ui.Panel(body, "Exchange gold", false, "diamond-exchange-panel");
            var amt = Ui.Heading($"{amount:N0}", ex, "gold diamond-amount-box");
            amt.name = "diamond-amount";
            var pick = Ui.Actions(ex);
            pick.AddToClassList("diamond-steps");
            foreach (var step in Steps)
            {
                int d = step;
                string label = d < 0 ? $"−{-d}" : $"+{d}";
                Ui.Button(label, () => { amount = Math.Max(1, amount + d); Refresh(); }, "btn--small diamond-step", pick).name = d < 0 ? $"diamond-amount-minus{-d}" : $"diamond-amount-plus{d}";
            }
            var maxB = Ui.Button("Max", () => { amount = Math.Max(1, max); Refresh(); }, "btn--small diamond-step", pick);
            maxB.name = "diamond-amount-max";
            Ui.SetEnabled(maxB, max > 0);
            Ui.Field("Costs", Ui.Gold(DiamondExchange.GoldFor(amount)), ex, S.Gold >= DiamondExchange.GoldFor(amount) ? null : "bad", "diamond-quote");
            Ui.Field("You can buy", ShardText.ShortOf(max), ex, "dim");

            if (Features.DiamondStore)
            {
                var store = DiamondStores.Current;
                storeSig = Signature(store);
                var packs = Ui.Panel(body, "Buy " + ShardText.Plural, false, "diamond-packs");
                // GG-T470: one line says where the purchase stands: processing, the last answer (success, pending,
                // cancelled, failed), or the store being unavailable.
                var (state, text, tone) = StateLine(store);
                var line = Ui.Para(text, packs, tone);
                line.name = "diamond-store-status";
                line.AddToClassList("purchase-state--" + state);
                bool busy = DiamondStores.InFlight != null;
                bool open = store.Connected && !busy;
                bool localized = false;
                foreach (var p in DiamondStore.Packs)
                {
                    var pack = p;
                    var local = store.PriceOf(pack);
                    localized |= local != null;
                    var value = DiamondStores.InFlight == pack.ProductId ? "Processing" : local ?? pack.Price;
                    var row = Ui.MenuRow(ShardText.ShortOf(pack.Diamonds), value, () => Buy(store, pack), packs, null, open);
                    row.KindIcon(RowKind.Diamond);
                    row.name = "diamond-pack-" + pack.Diamonds;
                }
                if (store.CanRestore)
                {
                    var restore = Ui.MenuRow("Restore purchases", DiamondStores.InFlight == RestoreId ? "Checking" : "", () => Restore(store), packs, null, open);
                    restore.KindIcon(RowKind.NewGamePlus); // GG-T754 (§26.7)
                    restore.name = "diamond-restore";
                }
                Ui.Note(localized ? $"Prices are set by the store in your currency. Every pack is the same price per {ShardText.Short}."
                    : $"A flat rate: every pack is the same price per {ShardText.Short}.", packs);
            }

            var foot = Ui.Footer(page, "diamond-footer");
            Ui.Name($"{ShardText.ShortOf(amount)} for {Ui.Gold(DiamondExchange.GoldFor(amount))}", foot, "grow");
            var go = RowIcon.IconButton("Exchange", RowKind.Confirm, Exchange, "btn--primary", foot, confirmTap: false); // GG-T745
            go.name = "diamond-exchange";
            Ui.SetEnabled(go, amount <= max);
        }

        private void Exchange()
        {
            int n = amount;
            Confirm.Show($"Exchange {Ui.Gold(DiamondExchange.GoldFor(n))} for {ShardText.Of(n)}?", "Exchange", () =>
            {
                var r = DiamondExchange.Buy(C, S, n);
                App.Toast(r.Message ?? (r.Ok ? "Done" : "Can't do that"));
                App.Audio.Sfx(r.Ok ? "ui.diamond" : "ui.error");
                if (r.Ok) App.Persist();
                Refresh();
            });
        }

        private const string RestoreId = "restore";

        /// <summary>The last purchase answer this visit (shown on the status line until the next purchase).</summary>
        private PurchaseResult last;
        private string storeSig;
        private float sigClock;
        private bool nudged;

        private static string Signature(IDiamondStore s) =>
            $"{s.Connected}|{s.Status}|{s.PriceOf(DiamondStore.Packs[0])}|{DiamondStores.InFlight}";

        private (string state, string text, string tone) StateLine(IDiamondStore store)
        {
            if (DiamondStores.InFlight == RestoreId) return ("processing", "Checking your purchases", "warn");
            if (DiamondStores.InFlight != null) return ("processing", "Processing your purchase. The store will ask you to confirm. Keep the game open.", "warn");
            if (last != null)
                switch (last.Kind)
                {
                    case PurchaseKind.Granted: return ("success", last.Message, "good");
                    case PurchaseKind.Pending: return ("pending", last.Message, "warn");
                    case PurchaseKind.Cancelled: return ("cancelled", last.Message, "dim");
                    case PurchaseKind.Unavailable: return ("unavailable", last.Message, "bad");
                    default: return ("failure", last.Message, "bad");
                }
            return store.Connected ? ("ready", store.Status, "dim") : ("unavailable", store.Status, "bad");
        }

        /// <summary>The store connected, its prices arrived or it dropped: redraw (checked twice a second, never mid-purchase).</summary>
        public override void Tick(float dt)
        {
            base.Tick(dt);
            if (!Features.DiamondStore || (sigClock += dt) < 0.5f) return;
            sigClock = 0f;
            // A store that never answers mustn't lock the packs for the whole session: after two minutes, unlock and say
            // the purchase is still pending (if it went through, the Diamonds arrive on their own).
            if (!nudged)
            {
                nudged = true;
#if GG_IAP
                (DiamondStores.Current as UnityIapDiamondStore)?.Nudge(); // retry a failed store connection on open
#endif
            }
            if (DiamondStores.InFlight != null && Time.realtimeSinceStartup - DiamondStores.InFlightSince > 120f)
            {
                DiamondStores.InFlight = null;
#if GG_IAP
                // The store's own in-flight purchase must go too, or every later Buy is refused as "already in progress".
                (DiamondStores.Current as UnityIapDiamondStore)?.AbandonInFlight();
#endif
                Show(PurchaseResult.Of(PurchaseKind.Pending, $"The store hasn't answered yet. If the purchase went through, the {ShardText.ShortPlural} arrive on their own."));
                return;
            }
            var sig = Signature(DiamondStores.Current);
            if (sig != storeSig) Refresh();
        }

        private void Buy(IDiamondStore store, DiamondPack pack)
        {
            if (DiamondStores.InFlight != null) return; // GG-T470: no double taps while the store sheet is up
            if (!store.Connected)
            {
                Show(PurchaseResult.Of(PurchaseKind.Unavailable, store.Status, pack.ProductId));
                return;
            }
            // GG-T1559 (§25.18.1): the age band once per wallet, then the monthly limit, then an under-13's parental gate.
            var wallet = DiamondExchange.Wallet;
            if (SpendLimits.NeedsAge(wallet)) { SpendGate.AskAge(wallet, () => Buy(store, pack)); return; }
            var refusal = SpendLimits.Refusal(wallet, pack, DateTime.UtcNow);
            if (refusal != null)
            {
                Show(PurchaseResult.Of(PurchaseKind.Unavailable, refusal, pack.ProductId));
                return;
            }
            if (SpendLimits.NeedsParentalGate(wallet)) { SpendGate.Parental(() => Purchase(store, pack)); return; }
            Purchase(store, pack);
        }

        private void Purchase(IDiamondStore store, DiamondPack pack)
        {
            if (DiamondStores.InFlight != null) return;
            DiamondStores.InFlight = pack.ProductId;
            last = null;
            App.Audio.Sfx("ui.select");
            Refresh();
            store.Purchase(C, S, pack, r => { DiamondStores.InFlight = null; Show(r); });
        }

        private void Restore(IDiamondStore store)
        {
            if (DiamondStores.InFlight != null) return;
            DiamondStores.InFlight = RestoreId;
            last = null;
            Refresh();
            store.Restore(r => { DiamondStores.InFlight = null; Show(r); });
        }

        /// <summary>A purchase answer for this screen: the status line, a toast and a sound. Granted Diamonds are already in
        /// the save (the IAP store writes it before confirming); saving again covers the dev store.</summary>
        internal void Show(PurchaseResult r)
        {
            if (r == null) return;
            last = r;
            App.Toast(r.Message, r.Ok ? 2.2f : 3f);
            App.Audio.Sfx(r.Ok ? "ui.diamond" : r.Kind == PurchaseKind.Pending || r.Kind == PurchaseKind.Cancelled ? "ui.select" : "ui.error");
            if (r.Ok) App.Persist();
            var current = App.Router.Current;
            if (current == this || (TabHost != null && current == TabHost)) Refresh();
        }
    }
}
