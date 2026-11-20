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
        /// Release builds: the placeholder that says "Store not connected yet" until a real billing integration (Unity IAP,
        /// Google Play, Apple) is wired here.</summary>
        public static IDiamondStore Current =>
            current ??= Application.isEditor || Debug.isDebugBuild ? new DevDiamondStore() : (IDiamondStore)new UnconnectedDiamondStore();

        public static void Open() => GameApp.I.Router.Push(new DiamondStoreScreen());

        /// <summary>A Diamond price couldn't be paid: say why, and offer the store.</summary>
        public static void OfferStore(string why)
        {
            GameApp.I.Audio.Sfx("ui.error");
            Confirm.Show($"{why}\nOpen the Diamond Store?", "Diamond Store", Open);
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
        protected override string Title => "Diamond Store";
        protected override string Backdrop => "cityruins";

        private int amount = 100;
        private static readonly int[] Steps = { -100, -10, 10, 100 };

        protected override void BuildBody(VisualElement body)
        {
            int have = DiamondExchange.Have(S);
            int max = DiamondExchange.MaxBuy(C, S);
            amount = Math.Max(1, amount);

            var bal = Ui.Panel(body, null, false, "diamond-balance");
            var head = Ui.El("row", bal);
            head.style.alignItems = Align.Center;
            var icon = Art.ItemIcon(Crafting.DiamondId);
            if (icon != null)
            {
                var img = Ui.El(null, head);
                img.style.width = img.style.height = Size.Icon;
                img.style.marginRight = Space.S16;
                img.style.backgroundImage = new StyleBackground(icon);
            }
            Ui.Heading($"{have:N0} Diamonds", head, "gold").name = "diamond-count";
            Ui.Caption($"1 Diamond = {DiamondExchange.GoldPerDiamond} G · {DiamondExchange.DiamondsPerDollar} Diamonds = $1.00", bal, "dim");
            Ui.Para("Diamonds pay for a random summon, an instant Rested refill and a lucky craft. Anything priced in Diamonds can also be paid in gold at the same rate.", bal, "dim");

            var ex = Ui.Panel(body, "Exchange gold", false, "diamond-exchange-panel");
            var pick = Ui.Actions(ex);
            pick.style.alignItems = Align.Center;
            foreach (var step in Steps)
            {
                int d = step;
                if (d > 0) continue;
                Ui.Button($"−{-d}", () => { amount = Math.Max(1, amount + d); Refresh(); }, "btn--small", pick).name = $"diamond-amount-minus{-d}";
            }
            var amt = Ui.Name($"{amount:N0}", pick, "gold");
            amt.name = "diamond-amount";
            foreach (var step in Steps)
            {
                int d = step;
                if (d < 0) continue;
                Ui.Button($"+{d}", () => { amount += d; Refresh(); }, "btn--small", pick).name = $"diamond-amount-plus{d}";
            }
            var maxB = Ui.Button("Max", () => { amount = Math.Max(1, max); Refresh(); }, "btn--small", pick);
            maxB.name = "diamond-amount-max";
            Ui.SetEnabled(maxB, max > 0);
            Ui.Field("Costs", Ui.Gold(DiamondExchange.GoldFor(amount)), ex, S.Gold >= DiamondExchange.GoldFor(amount) ? null : "bad", "diamond-quote");
            Ui.Field("You can buy", $"{max:N0} Diamonds", ex, "dim");

            if (Features.DiamondStore)
            {
                var store = DiamondStores.Current;
                var packs = Ui.ListPanel(body, "Buy Diamonds", "diamond-packs");
                Ui.Para(store.Status, packs, store.Connected ? "dim" : "bad").name = "diamond-store-status";
                foreach (var p in DiamondStore.Packs)
                {
                    var pack = p;
                    var row = Ui.MenuRow($"{pack.Diamonds:N0} Diamonds", pack.Price, () => Buy(store, pack), packs, icon);
                    row.name = "diamond-pack-" + pack.Diamonds;
                }
                Ui.Note("A flat rate: every pack is the same price per Diamond.", packs);
            }

            var foot = Ui.Footer(body, "diamond-footer");
            Ui.Name($"{amount:N0} Diamonds for {Ui.Gold(DiamondExchange.GoldFor(amount))}", foot, "grow");
            var go = Ui.Button("Exchange", Exchange, "btn--primary", foot);
            go.name = "diamond-exchange";
            Ui.SetEnabled(go, amount <= max);
        }

        private void Exchange()
        {
            int n = amount;
            Confirm.Show($"Exchange {Ui.Gold(DiamondExchange.GoldFor(n))} for {n:N0} Diamonds?", "Exchange", () => Done(DiamondExchange.Buy(C, S, n)));
        }

        private void Buy(IDiamondStore store, DiamondPack pack)
        {
            if (!store.Connected)
            {
                App.Toast(store.Status);
                App.Audio.Sfx("ui.error");
                return;
            }
            store.Purchase(C, S, pack, r => Done(r));
        }
    }
}
