using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Core.Rules;
using GridGame.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    public sealed class ShopScreen : HubPage
    {
        private int tab;
        private string bargainSelected;
        private int buyCategory = -1;
        private readonly Dictionary<string, int> buyCart = new Dictionary<string, int>();
        private readonly Dictionary<string, int> sellCart = new Dictionary<string, int>();
        private ScrollView cartScroll;
        private float keepScroll;
        public override string Id => "shop";
        protected override string Title => "Merchant";
        protected override string Npc => CampNpcs.Merchant;

        protected override void BuildBody(VisualElement body)
        {
            Ui.Tabs(new[] { "Buy", "Sell", "Bargain" }, tab, i => { tab = i; bargainSelected = null; Refresh(); }, body);
            if (tab == 2) { BuildBargain(body); return; }
            bool selling = tab == 1;
            // GG-T538 (§25.0.10): the shelf grows with progress; a fresh Merchant shows the first stock tier only.
            var items = selling ? Shop.Sellable(C, S) : Shop.Stock(C, S).Where(i => Features.Offers(i.Id)).ToList();
            if (!selling)
            {
                // GG-T1161 (§25.1): one category at a time (about 110 rows in one list was a wall); the first non-empty one opens.
                var cats = items.Select(Shop.SectionOf).Distinct().OrderBy(c => c).ToList();
                if (cats.Count > 0)
                {
                    if (!cats.Contains(buyCategory)) buyCategory = cats[0];
                    var catTabs = Ui.Tabs(cats.Select(c => Shop.Sections[c]).ToArray(), cats.IndexOf(buyCategory), i => { buyCategory = cats[i]; keepScroll = 0f; Refresh(); }, body);
                    Ui.Margin(catTabs, top: Space.S24);
                    items = items.Where(i => Shop.SectionOf(i) == buyCategory).ToList();
                }
            }
            var list = Ui.ListPanel(body, null, out cartScroll);
            // A −/+ tap rebuilds the page: keep the list where the player had scrolled it, not back at the top.
            float restore = keepScroll; keepScroll = 0f;
            var sv = cartScroll;
            if (restore > 0f) sv.RegisterCallback<GeometryChangedEvent>(_ => { if (restore > 0f) { sv.scrollOffset = new Vector2(0f, restore); restore = 0f; } });
            var cart = selling ? sellCart : buyCart;
            if (!selling && Shop.NextStock(C, S) is var next && next.tier > 0)
            {
                // The next stock tier, signposted at the head of the list: "More stock after The Lantern Road III · +14 items".
                string when = VendorTiers.OpensHint(C, next.tier);
                var more = Ui.MenuRow($"More stock {when}", $"+{next.count} items", () => { App.Toast($"The Merchant restocks {when}"); }, list, null, false);
                more.KindIcon(RowKind.Locked); // GG-T750 (§26.7): stock not open yet
                more.name = "shop-next";
            }
            if (items.Count == 0) Ui.Empty(selling ? "Nothing to sell." : "Sold out.", list);
            var detail = Ui.DetailPanel(body);
            ItemUi.Detail(detail, null);
            int section = -1;
            foreach (var it in items)
            {
                var item = it;
                if (!selling && Shop.SectionOf(item) != section)
                {
                    section = Shop.SectionOf(item);
                    Ui.Section(Shop.Sections[section], list).name = "shop-section-" + Shop.Sections[section].ToLowerInvariant();
                }
                cart.TryGetValue(item.Id, out var q);
                int price = selling ? Shop.SellPrice(item) : item.BaseCost;
                int max = selling ? S.Count(item.Id) : Shop.MaxBuy(S, item);
                // The value is the price, with "own 2" in small type under it once you have some (GG-T454). GG-T515: one
                // line "250 G · own 15" was wide enough, beside the −/0/+ counter, to squeeze names onto three hyphenated
                // lines ("Bram- blehide Grips") and clip its own count; stacked, the value column is only as wide as the price.
                int own = S.Count(item.Id);
                var row = ItemUi.Row(item, $"{price} G", () => ItemUi.Detail(detail, item, C), list);
                if (own > 0)
                {
                    var v = row.Q<Label>("value");
                    var stack = Ui.El("col", row);
                    stack.name = "shop-value";
                    stack.style.flexShrink = 0f;
                    stack.style.alignItems = Align.FlexEnd;
                    stack.style.justifyContent = Justify.Center;
                    row.Insert(row.IndexOf(v), stack);
                    stack.Add(v);
                    var ownLabel = Ui.Text($"own {own}", "small dim", stack);
                    ownLabel.name = "shop-own";
                    ownLabel.style.whiteSpace = WhiteSpace.NoWrap;
                }
                Ui.Stepper(row, q.ToString(), () => { Step(cart, item.Id, -1, max); }, () => { Step(cart, item.Id, +1, max); }, out var minus, out var plus);
                plus.name = "cart-plus-" + item.Id;
                Ui.SetEnabled(minus, q > 0);
                Ui.SetEnabled(plus, q < max);
                // GG-T520: under a gear row, which party heroes can equip it and whom it upgrades.
                if (!selling && Shop.GearFitLine(C, S, item, out bool upgrade) is string fit)
                    Ui.Note(fit, list, upgrade ? "good" : "dim").name = "shop-fit-" + item.Id;
            }
            var fr = Ui.Footer(body, "shop-footer");
            int total = Shop.CartTotal(C, cart, selling);
            Ui.Heading(selling ? $"Receive: {Ui.Gold(total)}" : $"Pay: {Ui.Gold(total)}", fr, "grow " + (!selling && total > S.Gold ? "bad" : "gold"));
            var commit = RowIcon.IconButton(selling ? "Sell" : "Buy", RowKind.Confirm, () =>
            {
                var r = selling ? Shop.Sell(C, S, cart) : Shop.Buy(C, S, cart);
                if (r.Ok) cart.Clear();
                Done(r);
            }, "btn--primary", fr);
            commit.name = "shop-commit";
            Ui.SetEnabled(commit, total > 0 && (selling || total <= S.Gold));
        }

        private void Step(Dictionary<string, int> cart, string id, int d, int max)
        {
            cart.TryGetValue(id, out var q);
            q = Mathf.Clamp(q + d, 0, max);
            if (q == 0) cart.Remove(id); else cart[id] = q;
            keepScroll = cartScroll != null ? cartScroll.scrollOffset.y : 0f;
            Refresh();
        }

        // ── "Used / Bargain" shelf: rotating worn weapons/armor at a discount (US-246) ──

        private void BuildBargain(VisualElement body)
        {
            var list = Ui.ListPanel(body);
            var detail = Ui.DetailPanel(body);
            var offers = Bargains.Shelf(C, S);
            if (offers.Count == 0) Ui.Empty("The shelf is bare. Check back after your next battle.", list);
            foreach (var o in offers)
            {
                var offer = o;
                var row = ItemUi.Row(offer.Item, $"{offer.Price} G", () => { bargainSelected = offer.Item.Id; Refresh(); }, list);
                if (bargainSelected == offer.Item.Id) Ui.Select(row);
            }
            var sel = offers.FirstOrDefault(o => o.Item.Id == bargainSelected);
            if (sel != null) ShowBargain(detail, sel);
            else Ui.Empty("Pick a piece to see how worn it is.", detail);
        }

        private void ShowBargain(VisualElement w, BargainOffer offer)
        {
            ItemUi.Detail(w, offer.Item, C);
            Ui.Caption($"Durability {offer.Durability}/{offer.Item.Durability}", w, "dim");
            Ui.Gauge((float)offer.Durability / offer.Item.Durability, w, 320);
            Ui.Para("Used gear: cheap now, but it can shatter mid-battle before you get a chance to repair it.", w, "warn");
            bool owned = S.Count(offer.Item.Id) >= offer.Item.MaxStack;
            var btn = RowIcon.IconButton(owned ? "Already owned" : $"Buy for {Ui.Gold(offer.Price)}", RowKind.Confirm, () =>
            {
                bargainSelected = null;
                Done(Bargains.Buy(C, S, offer.Item.Id));
            }, "btn--primary", Ui.Actions(w));
            Ui.SetEnabled(btn, !owned && S.Gold >= offer.Price);
        }
    }
}
