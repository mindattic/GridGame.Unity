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
    // ── Merchant (buy / sell with carts, §25.1) ──────────────────────────────────

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

    // ── Blacksmith: forge / upgrade / salvage / repair (§25.2) ───────────────────

    public sealed class BlacksmithScreen : HubPage
    {
        private const int RepairTab = 3;
        /// <summary>-1 until first built: then Repair if the workshop is still closed or his camp tip is due (GG-T175), else Forge.</summary>
        private int tab = -1;
        private RecipeDef selected;
        public override string Id => "blacksmith";
        protected override string Title => "Blacksmith";
        protected override string Backdrop => "cave";
        protected override string Subtitle => $"{Facilities.Current(C, S, CraftStation.Forge)} · {StationProgress.ShortText(S, StationKind.Blacksmith)}";
        protected override bool SubtitleInBody => true;
        protected override string Npc => CampNpcs.Blacksmith;
        /// <summary>The Forge tab has its own tip, the first forge (GG-T177), then the first armor once the weapon is made
        /// (GG-T178); Repair keeps the Blacksmith's.</summary>
        protected override string TipStation => tab != 0 ? Id : Forging.ArmorStep(S) ? Forging.ArmorTipStation : Forging.TipStation;

        protected override void BuildBody(VisualElement body)
        {
            // GG-T175 (§25.0.4): he joins to mend worn gear, so Repair is his only open tab at first; Forge, Upgrade and
            // Salvage are dimmed and say when they arrive.
            bool workshop = CampUnlocks.SmithWorkshopOpen(S);
            if (tab < 0) tab = workshop && (CampTips.Pending(S, Id) == null || !Repair.Pieces(C, S).Any()) ? 0 : RepairTab;
            if (!workshop) tab = RepairTab;
            var bar = Ui.Tabs(new[] { "Forge", "Upgrade", "Salvage", "Repair" }, tab, i =>
            {
                if (!workshop && i != RepairTab) { App.Toast(CampUnlocks.SmithWorkshopHint(App.Content)); App.Audio.Sfx("ui.error"); return; }
                tab = i; selected = null; Refresh();
            }, body);
            if (!workshop)
                for (int i = 0; i < bar.childCount; i++)
                    if (i != RepairTab) { bar[i].style.opacity = 0.4f; bar[i].AddToClassList("tab--locked"); }
            var list = Ui.ListPanel(body);
            var detail = Ui.DetailPanel(body);
            // GG-T177 (§25.0.6): until the first forge is made, the Forge opens on the starter weapon its tip walks.
            // GG-T178 (§25.0.7): once it's made (the starter still selected), it moves on to the first armor piece.
            if (tab == 0 && (selected == null || (Forging.IsStarter(selected) && Forging.Done(S))))
                selected = Forging.First(C, S)?.Recipe ?? Forging.FirstArmor(C, S)?.Recipe ?? selected;
            if (tab <= 1) RecipeList(list, detail, C, S, CraftStation.Forge, tab == 1, selected, r => selected = r, this);
            else if (tab == 2) SalvageList(list, detail);
            else RepairList(list, detail);
        }

        /// <summary>The station's recipe rows over its detail pane. <paramref name="onPick"/> keeps the page's own selection in
        /// step with the row the player taps, so a Craft/Brew (which rebuilds the page) stays on that recipe — and the list
        /// scrolls back to it — instead of jumping to the first or guided one.</summary>
        internal static void RecipeList(VisualElement list, VisualElement detail, ContentDb c, SaveData s, CraftStation station, bool upgrades, RecipeDef selected, Action<RecipeDef> onPick, HubPage page)
        {
            var recipes = Crafting.Recipes(c, station).Where(r => (r.BaseItem != null) == upgrades).ToList();
            if (station == CraftStation.Enchant) recipes = Crafting.Recipes(c, station).ToList();
            // Recipe discovery (§24.6.2, GG-T36): undiscovered recipes are hidden, except a "???" tease
            // for ones the player is exactly one input away from.
            // Armor-set recipes open after the first forge (GG-T178, §25.0.7).
            // GG-T538 (§25.0.10): the Forge and Alchemy lists also grow with progress (VendorTiers.RecipeOpen).
            var visible = recipes.Where(r => Forging.ArmorOpen(c, s, r) && VendorTiers.RecipeOpen(c, s, r) && (RecipeDiscovery.IsDiscovered(s, r) || RecipeDiscovery.OneAwayMissing(s, r) != null)).ToList();
            // GG-T432 (§25.12.2): the station's next facility level, bought right here (also a card on the Caravan screen).
            if (Facilities.Next(c, s, station) is CaravanLevel nextFacility)
            {
                var build = Ui.MenuRow($"Build {nextFacility.Name}", Ui.Gold(nextFacility.Cost), () => ConfirmFacility(c, s, station, page), list);
                build.name = "facility-build";
                build.KindIcon(RowKind.Station); // GG-T746 (§26.7)
            }
            // GG-T538: the station's next rank, in forges or brews ("Journeyman at 150 XP: 40/150, about 8 more brews").
            if (VendorTiers.Gated(recipes.FirstOrDefault()) && VendorTiers.NextRankLine(s, Crafting.StationFor(station)) is string rankLine)
                Ui.Note(rankLine, list).name = "rank-next";
            if (visible.Count == 0) Ui.Empty("No recipes.", list);
            RecipeDef sel = selected;
            // GG-T198: two discovered recipes can share a Result (the old Slime Gel brew and the new Empty Bottle
            // brew, GG-T176) and otherwise look identical in the list apart from price. When that happens, name
            // each row's key inputs so a player can tell them apart without opening both.
            var resultCounts = visible.Where(x => RecipeDiscovery.IsDiscovered(s, x)).GroupBy(x => x.Result)
                .ToDictionary(g => g.Key, g => g.Count());
            foreach (var r in visible)
            {
                var rec = r;
                VisualElement row;
                if (RecipeDiscovery.IsDiscovered(s, r))
                {
                    var result = c.Item(r.Result);
                    bool tierLocked = !Crafting.IsUnlocked(s, r), facilityLocked = !Facilities.Allows(c, s, r);
                    bool locked = tierLocked || facilityLocked;
                    bool can = !locked && Crafting.HasInputs(s, r) && s.Gold >= r.Gold;
                    string nameOverride = resultCounts[r.Result] > 1 ? $"{result.Name} — {InputSummary(c, r)}" : null;
                    string lockLabel = tierLocked ? $"🔒 {StationProgress.TierName(r.Tier)}" : $"🔒 {Facilities.NeededName(c, r)}";
                    row = ItemUi.Row(result, locked ? lockLabel : $"{r.Gold} G", () => { onPick?.Invoke(rec); ShowRecipe(detail, c, s, rec, page); }, list, true, false, nameOverride);
                    row.name = "recipe-" + r.Id;
                    if (!can) row.style.opacity = Dim.Row;
                }
                else
                {
                    var missing = RecipeDiscovery.OneAwayMissing(s, r);
                    var missingItem = c.TryItem(missing.Item);
                    row = Ui.MenuRow("???", $"Missing {missingItem?.Name ?? missing.Item}", () => { onPick?.Invoke(rec); ShowRecipe(detail, c, s, rec, page); }, list);
                    row.KindIcon(RowKind.Item); // GG-T717: an undiscovered result shows the Item emblem
                    row.style.opacity = 0.75f;
                }
                if (sel == r)
                {
                    Ui.Select(row);
                    var picked = row;
                    var scroll = list.GetFirstAncestorOfType<ScrollView>();
                    bool scrolled = false;
                    if (scroll != null)
                        scroll.RegisterCallback<GeometryChangedEvent>(_ => { if (!scrolled && picked.layout.height > 0) { scrolled = true; scroll.ScrollTo(picked); } });
                }
            }
            if (VendorTiers.Gated(recipes.FirstOrDefault())) ComingLater(list, detail, c, s, station, recipes, visible);
            if (sel != null && visible.Contains(sel)) ShowRecipe(detail, c, s, sel, page);
            else if (sel != null && !VendorTiers.RecipeOpen(c, s, sel)) ShowLocked(detail, c, s, sel, LockReason(c, s, sel));
            else if (sel != null) ShowRecipe(detail, c, s, sel, page);
            else Ui.Empty("Pick a recipe to see what it needs.", detail);
        }

        /// <summary>GG-T538 (§25.0.10): the next tier, shown locked at the foot of the list. Up to
        /// <see cref="VendorTiers.PreviewRows"/> recipes from the next rank ("🔒 Journeyman") and from the next progress tier
        /// ("🔒 The Lantern Road III"), with a count of the rest, so the player sees what is coming and what opens it.</summary>
        private static void ComingLater(VisualElement list, VisualElement detail, ContentDb c, SaveData s, CraftStation station, List<RecipeDef> recipes, List<RecipeDef> visible)
        {
            var kind = Crafting.StationFor(station);
            int rank = StationProgress.Level(s, kind), tier = VendorTiers.ProgressTier(c, s);
            var shown = new HashSet<RecipeDef>(visible);
            // Next rank: open on the road, hidden today only because the station's rank is short.
            var byRank = recipes.Where(r => !shown.Contains(r) && Forging.ArmorOpen(c, s, r) && VendorTiers.RecipeOpen(c, s, r) && r.Tier == rank + 1).ToList();
            // Next progress tier: the first tier above this save's with anything for this list.
            int next = recipes.Where(r => !shown.Contains(r) && Forging.ArmorOpen(c, s, r) && !VendorTiers.RecipeOpen(c, s, r))
                .Select(r => VendorTiers.RecipeTier(c, r)).Where(t => t > tier).DefaultIfEmpty(0).Min();
            var byRoad = next == 0 ? new List<RecipeDef>() : recipes.Where(r => !shown.Contains(r) && Forging.ArmorOpen(c, s, r) && !VendorTiers.RecipeOpen(c, s, r) && VendorTiers.RecipeTier(c, r) == next)
                .OrderBy(r => r.Tier).ThenBy(r => r.Gold).ToList();
            if (byRank.Count == 0 && byRoad.Count == 0) return;
            Ui.Section("Coming later", list).name = "recipes-later";
            void Rows(List<RecipeDef> set, string value, string more)
            {
                foreach (var r in set.Take(VendorTiers.PreviewRows))
                {
                    var rec = r;
                    var row = ItemUi.Row(c.Item(r.Result), value, () => ShowLocked(detail, c, s, rec, LockReason(c, s, rec)), list, true, false);
                    row.name = "recipe-locked-" + r.Id;
                    row.style.opacity = Dim.Row;
                }
                if (set.Count > VendorTiers.PreviewRows) Ui.Note($"+{set.Count - VendorTiers.PreviewRows} more {more}", list);
            }
            if (byRank.Count > 0) Rows(byRank, $"🔒 {StationProgress.TierName(rank + 1)}", $"at {StationProgress.TierName(rank + 1)}");
            if (byRoad.Count > 0)
            {
                string after = VendorTiers.OpensHint(c, next);
                Rows(byRoad, "🔒 " + after.Substring("after ".Length), after);
            }
        }

        /// <summary>Why a previewed recipe is locked: "Opens after The Lantern Road III", "Needs a Journeyman blacksmith (40/150 XP)", or both.</summary>
        private static string LockReason(ContentDb c, SaveData s, RecipeDef r)
        {
            var kind = Crafting.StationFor(r.Station);
            var parts = new List<string>();
            if (!VendorTiers.RecipeOpen(c, s, r)) parts.Add("Opens " + VendorTiers.OpensHint(c, VendorTiers.RecipeTier(c, r)));
            if (!Crafting.IsUnlocked(s, r))
                parts.Add($"Needs a {StationProgress.TierName(r.Tier)} {StationProgress.Id(kind)} (you are {StationProgress.LevelName(s, kind)}, {StationProgress.Xp(s, kind)} XP)");
            return string.Join(". ", parts);
        }

        /// <summary>A previewed recipe's detail: what it makes, why it is locked, and what goes in (GG-T538).</summary>
        private static void ShowLocked(VisualElement w, ContentDb c, SaveData s, RecipeDef r, string reason)
        {
            w.Clear();
            ItemUi.Detail(w, c.Item(r.Result), c);
            Ui.Para("🔒 " + reason, w, "bad");
            Ui.Section("Needs", w);
            if (r.BaseItem != null) Ui.Text($"{c.TryItem(r.BaseItem)?.Name ?? r.BaseItem} ×1", "kit-text dim", w);
            foreach (var ic in r.Inputs) Ui.Text($"{c.TryItem(ic.Item)?.Name ?? ic.Item} ×{ic.Count}", "kit-text dim", w);
            Ui.Para(Ui.Gold(r.Gold), w, "dim");
        }

        /// <summary>"Slime Gel" or "Empty Bottle + Wolf Pelt + Tattered Cloth" — the recipe's own inputs, joined,
        /// used to tell apart two recipes that share a Result (GG-T198).</summary>
        private static string InputSummary(ContentDb c, RecipeDef r) =>
            string.Join(" + ", r.Inputs.Select(ic => c.TryItem(ic.Item)?.Name ?? ic.Item));

        /// <summary>Asks, then builds the station's next facility level (GG-T432): a coin cue, then the forge's clang or
        /// the still's glug as the new equipment goes in.</summary>
        private static void ConfirmFacility(ContentDb c, SaveData s, CraftStation station, HubPage page)
        {
            var next = Facilities.Next(c, s, station);
            if (next == null) return;
            if (s.Gold < next.Cost) { GameApp.I.Toast($"{next.Name} costs {Ui.Gold(next.Cost)}"); GameApp.I.Audio.Sfx("ui.error"); return; }
            Confirm.Show($"Build the {next.Name} for {Ui.Gold(next.Cost)}? {next.Text}.", "Build", () =>
            {
                var res = Facilities.Build(c, s, station);
                GameApp.I.Toast(res.Message);
                GameApp.I.Audio.Sfx(res.Ok ? "ui.buy" : "ui.error");
                if (res.Ok)
                {
                    GameApp.I.Audio.Sfx(station == CraftStation.Forge ? "battle.clang" : "battle.glug");
                    GameApp.I.Persist();
                }
                page.Refresh();
            });
        }

        internal static void ShowRecipe(VisualElement w, ContentDb c, SaveData s, RecipeDef r, HubPage page)
        {
            w.Clear();
            if (!RecipeDiscovery.IsDiscovered(s, r))
            {
                Ui.Heading("??? — Undiscovered Recipe", w, "dim");
                Ui.Para("You sense you're close. Find the missing material to learn how this is made.", w, "dim");
                var undiscoveredNeed = w;
                if (r.BaseItem != null)
                {
                    var b = c.TryItem(r.BaseItem);
                    int have = s.Count(r.BaseItem);
                    Ui.Text($"{(have >= 1 ? "✔" : "✘")} {b?.Name ?? r.BaseItem} ×1   (have {have})", "kit-text " + (have >= 1 ? "good" : "bad"), undiscoveredNeed);
                }
                foreach (var ic in r.Inputs)
                {
                    var it = c.TryItem(ic.Item);
                    int have = s.Count(ic.Item);
                    Ui.Text($"{(have >= ic.Count ? "✔" : "✘")} {it?.Name ?? ic.Item} ×{ic.Count}   (have {have})", "kit-text " + (have >= ic.Count ? "good" : "bad"), undiscoveredNeed);
                }
                return;
            }
            var result = c.Item(r.Result);
            ItemUi.Detail(w, result, c);
            bool locked = !Crafting.CanMake(c, s, r);
            if (!Crafting.IsUnlocked(s, r))
            {
                var kind = Crafting.StationFor(r.Station);
                Ui.Para($"🔒 Requires {StationProgress.TierName(r.Tier)} ({StationProgress.Id(kind)} is {StationProgress.LevelName(s, kind)})", w, "bad");
            }
            if (!Facilities.Allows(c, s, r))
            {
                // GG-T432: shown locked, never hidden — the facility that works this material is named, with its price.
                Ui.Para($"🔒 Needs {Facilities.NeededName(c, r)} (you have the {Facilities.Current(c, s, r.Station)})", w, "bad");
                if (Facilities.Next(c, s, r.Station) is CaravanLevel next)
                {
                    var b = RowIcon.IconButton($"Build {next.Name} · {Ui.Gold(next.Cost)}", RowKind.Confirm, () => ConfirmFacility(c, s, r.Station, page), "btn--small", Ui.Actions(w), confirmTap: false);
                    b.name = "facility-build-detail";
                    Ui.SetEnabled(b, s.Gold >= next.Cost);
                }
            }
            Ui.Section("Needs", w);
            var need = w;
            if (r.BaseItem != null)
            {
                var b = c.TryItem(r.BaseItem);
                int have = s.Count(r.BaseItem);
                Ui.Text($"{(have >= 1 ? "✔" : "✘")} {b?.Name ?? r.BaseItem} ×1   (have {have})", "kit-text " + (have >= 1 ? "good" : "bad"), need);
            }
            foreach (var ic in r.Inputs)
            {
                var it = c.TryItem(ic.Item);
                int have = s.Count(ic.Item);
                Ui.Text($"{(have >= ic.Count ? "✔" : "✘")} {it?.Name ?? ic.Item} ×{ic.Count}   (have {have})", "kit-text " + (have >= ic.Count ? "good" : "bad"), need);
            }
            Ui.Para($"{(s.Gold >= r.Gold ? "✔" : "✘")} {Ui.Gold(r.Gold)}", need, s.Gold >= r.Gold ? "good" : "bad");
            if (r.Station == CraftStation.Forge)
                Ui.Para($"★ {OddsText.Percent(Crafting.MasterworkChance(c, s))} chance of a Masterwork result", need, "gold");
            if (Forging.IsGuided(s, r) && Forging.SuitedHero(c, s, result) is HeroSave suited)
                Ui.Para($"First forge: free, and the {c.TryClass(suited.ClassId)?.Name ?? suited.ClassId} equips it", need, "gold");
            if (Forging.IsGuidedArmor(c, s, r) && Forging.SuitedWearer(c, s, result) is HeroSave wearer)
                Ui.Para($"First armor: the {c.TryClass(wearer.ClassId)?.Name ?? wearer.ClassId} wears it", need, "gold");
            string verb = r.Station == CraftStation.Alchemy ? "Brew" : r.Station == CraftStation.Enchant ? "Enchant" : "Craft";
            var btn = RowIcon.IconButton(verb, RowKind.Confirm, () =>
            {
                // The guided first forge (GG-T177) and first armor (GG-T178) also equip what they make on the hero it suits.
                var res = Forging.Forge(c, s, r);
                GameApp.I.Toast(res.Message);
                GameApp.I.Audio.Sfx(res.Ok ? "ui.buy" : "ui.error");
                if (res.Ok) GameApp.I.Persist();
                page.Refresh();
            }, "btn--primary", Ui.Actions(w));
            btn.name = "craft";
            Ui.SetEnabled(btn, !locked && Crafting.HasInputs(s, r) && s.Gold >= r.Gold);
            LuckyCraftUi.Section(w, c, s, r, !locked && Crafting.HasInputs(s, r) && s.Gold >= r.Gold, page.Refresh);

            if (r.PotencyResult != null)
            {
                var greater = c.Item(r.PotencyResult);
                Ui.Section($"With a rarer reagent: {greater.Name}", w);
                var pneed = w;
                foreach (var ic in r.PotencyInputs)
                {
                    var it = c.TryItem(ic.Item);
                    int have = s.Count(ic.Item);
                    Ui.Text($"{(have >= ic.Count ? "✔" : "✘")} {it?.Name ?? ic.Item} ×{ic.Count}   (have {have})", "kit-text " + (have >= ic.Count ? "good" : "bad"), pneed);
                }
                var pbtn = RowIcon.IconButton($"{verb} {greater.Name}", RowKind.Confirm, () =>
                {
                    var res = Crafting.Craft(c, s, r, potency: true);
                    GameApp.I.Toast(res.Message);
                    GameApp.I.Audio.Sfx(res.Ok ? "ui.buy" : "ui.error");
                    if (res.Ok) GameApp.I.Persist();
                    page.Refresh();
                }, null, Ui.Actions(w));
                Ui.SetEnabled(pbtn, !locked && Crafting.HasPotencyInputs(s, r) && s.Gold >= r.Gold);
            }
        }

        private void SalvageList(VisualElement list, VisualElement detail)
        {
            var gear = S.Inventory.Where(kv => kv.Value > 0).Select(kv => C.TryItem(kv.Key)).Where(i => i != null && i.Type == ItemType.Equipment).ToList();
            var broken = S.Broken.Where(kv => kv.Value > 0).Select(kv => C.TryItem(kv.Key)).Where(i => i != null).ToList();
            if (gear.Count == 0 && broken.Count == 0) Ui.Empty("No spare gear in your bag.", list);
            Ui.Para("Break spare gear into materials — worn gear yields less; the worst copy goes first. Broken gear can only be salvaged.", detail, "dim");
            foreach (var it in gear)
            {
                var item = it;
                var yield = Crafting.SalvagePreview(C, S, item.Id);
                string y = yield.Count == 0 ? "—" : string.Join(", ", yield.Select(ic => $"{C.TryItem(ic.Item)?.Name}×{ic.Count}"));
                ItemUi.Row(item, $"×{S.Count(item.Id)}", () => Confirm.Show($"Salvage {item.Name} into {y}?", "Salvage", () => Done(Crafting.Salvage(C, S, item.Id))), list, yield.Count > 0, Equipment.IsMasterwork(S, item.Id));
            }
            foreach (var it in broken)
            {
                var item = it;
                var yield = Crafting.SalvageBrokenPreview(C, S, item.Id);
                string y = yield.Count == 0 ? "—" : string.Join(", ", yield.Select(ic => $"{C.TryItem(ic.Item)?.Name}×{ic.Count}"));
                var row = Ui.MenuRow($"Broken {item.Name}", $"×{S.BrokenCount(item.Id)}",
                    () => Confirm.Show($"Salvage Broken {item.Name} into {y}?", "Salvage", () => Done(Crafting.SalvageBroken(C, S, item.Id))), list, null, yield.Count > 0);
                row.KindIcon(RowKind.Item, Art.ItemIcon(item.Id)); // GG-T717
                row.Q<Label>("label").AddToClassList("dim");
            }
        }

        /// <summary>The Repair tab's Repair all button (GG-T544). Not "repair-…": the Blacksmith's tip rings the first
        /// control whose name starts with that.</summary>
        public const string RepairAllButton = "smith-repair-all";

        /// <summary>How much wear the Repair tab's price hint quotes (GG-T544): "3 G per 10 wear, 5 G after this repair".</summary>
        private const int HintWear = 10;

        private void RepairList(VisualElement list, VisualElement detail)
        {
            bool salvage = CampUnlocks.SmithWorkshopOpen(S); // "Salvage instead" waits for the Salvage tab (GG-T175)
            int threshold = (int)Math.Round(CampRoutine.RepairBelowFraction * 100);
            // GG-T544: one threshold for the camp routine and the player. Each repair compounds the piece's next price, so
            // the tab says so, Repair all mends only pieces worn below the threshold, and the rest say "Cheaper to wait".
            Ui.Para($"Each repair lowers the piece's max durability by 1 and makes its next repair cost 1.6× as much, so mend gear once it's below {threshold}%, not after every scratch."
                    + (salvage ? " An uneconomical repair offers salvage instead." : ""), detail, "dim");
            var due = Repair.DuePieces(C, S);
            int dueCost = due.Sum(p => Repair.Cost(C, S, p));
            var all = RowIcon.IconButton(due.Count == 0 ? "Nothing needs repair yet" : $"Repair all ({due.Count}) · {Ui.Gold(dueCost)}", RowKind.Confirm,
                () => Done(Repair.RepairAll(C, S), cue: "camp.repair"), "btn--primary", Ui.Actions(detail));
            all.name = RepairAllButton;
            Ui.SetEnabled(all, due.Any(p => Repair.Cost(C, S, p) <= S.Gold));
            bool any = false;
            int sound = 0, worn = 0;
            foreach (var p in Repair.Pieces(C, S))
            {
                any = true;
                var piece = p;
                int cost = Repair.Cost(C, S, p);
                if (cost <= 0) { sound++; continue; } // GG-T1865: full-durability pieces collapse into one row below
                worn++;
                bool bad = cost > 0 && Repair.IsUneconomical(p.Item, p.Durability, p.Repairs, p.Mastery);
                string name = (p.Mastery > 1f ? "★ " : "") + p.Item.Name;
                var row = Ui.MenuRow($"{C.Class(p.Hero.ClassId).Name} — {name}  {p.Durability}/{p.Max}", cost > 0 ? Ui.Gold(cost) : "OK",
                    () => Done(piece.Slot == EquipmentSlot.Weapon ? Repair.RepairWeapon(C, S, piece.Hero) : Repair.RepairArmor(C, S, piece.Hero, piece.Slot), cue: "camp.repair"),
                    list, null, cost > 0);
                row.name = p.RowName;
                row.KindIcon(RowKind.Item, Art.ItemIcon(p.Item.Id)); // GG-T746 (§26.7): the worn piece's icon, else the Item emblem
                if (bad) row.Q<Label>("value").AddToClassList("warn");
                if (cost > 0) RepairPriceNote(list, p);
                if (bad && salvage) SalvageInsteadRow(list, p.Hero, p.Slot, p.Item, p.Durability, cost);
            }
            string soundText = $"All gear sound ({sound} piece{(sound == 1 ? "" : "s")} at full durability)";
            if (worn > 0 && sound > 0) Ui.Note(soundText, list);
            if (worn > 0) return;
            // GG-T1883: nothing to mend, so the tab is a compact note that points at the work he can do instead.
            var state = Ui.EmptyState(list, any ? RowKind.Cleared : RowKind.Craft, any ? soundText : "Nobody has gear that wears out. Equip forged weapons first.", "smith-empty");
            if (!salvage) return;
            var next = Ui.Actions(state);
            if (any)
            {
                RowIcon.IconButton("Upgrade", RowKind.Craft, () => { tab = 1; selected = null; Refresh(); }, "btn--small", next);
                RowIcon.IconButton("Salvage", RowKind.Item, () => { tab = 2; selected = null; Refresh(); }, "btn--small", next);
            }
            else RowIcon.IconButton("Forge", RowKind.Craft, () => { tab = 0; selected = null; Refresh(); }, "btn--small", next);
        }

        /// <summary>GG-T544: the line under a worn piece's repair row — "Cheaper to wait" while it's above the threshold, then
        /// how the price grows: what 10 points of wear cost now, and after this repair (1.6x per repair, BIBLE §24.5).</summary>
        private void RepairPriceNote(VisualElement list, Repair.WornPiece p)
        {
            int now = Repair.WearCost(C, S, p.Item, HintWear, p.Repairs, p.Mastery);
            int next = Repair.WearCost(C, S, p.Item, HintWear, p.Repairs + 1, p.Mastery);
            bool wait = !Repair.Due(p.Item, p.Durability, p.Repairs, p.Mastery);
            var note = Ui.Note((wait ? $"Cheaper to wait ({Mathf.RoundToInt(p.Fraction * 100)}%) · " : "")
                               + $"{Ui.Gold(now)} per {HintWear} wear, {Ui.Gold(next)} after this repair", list);
            note.name = $"wear-note-{p.Hero.ClassId}-{p.Slot}"; // not "repair-…" (the tip rings that prefix)
        }

        /// <summary>The "repair is a ripoff — salvage it instead" offer, shown right under an uneconomical
        /// repair row so both outcomes (repair cost vs. salvage yield) sit side by side (GG-T42).</summary>
        private void SalvageInsteadRow(VisualElement list, HeroSave hero, EquipmentSlot slot, ItemDef item, int durability, int repairCost)
        {
            var yield = Crafting.SalvageYield(C, item, durability);
            string y = yield.Count == 0 ? "—" : string.Join(", ", yield.Select(ic => $"{C.TryItem(ic.Item)?.Name}×{ic.Count}"));
            var row = Ui.MenuRow("Salvage instead", y, () =>
                Confirm.Show($"Repair costs {Ui.Gold(repairCost)}. Scrap the {item.Name} instead for {y}? This destroys it.", "Salvage",
                    () => Done(Crafting.SalvageEquipped(C, S, hero, slot))), list);
            row.KindIcon(RowKind.Item, Art.ItemIcon(item.Id)); // GG-T746 (§26.7): the piece it scraps
            row.AddToClassList("kit-note"); // a sub-row of the repair above, indented under its label
            row.Q<Label>("value").AddToClassList("dim");
        }
    }

    // ── Alchemist: brew (§25.3) ───────────────────────────────────────────────────
    // GG-T119: enchanting moved to the Enchantress's own station, and resting is the Innkeeper's (the old Heal tab
    // was the Inn under another name, which would have let the Alchemist open it before the Innkeeper joined).

    public sealed class AlchemistScreen : HubPage
    {
        private RecipeDef selected;
        public override string Id => "alchemist";
        protected override string Title => "Alchemist";
        protected override string Backdrop => "swamp";
        protected override string Subtitle => $"{Facilities.Current(C, S, CraftStation.Alchemy)} · {StationProgress.HeaderText(S, StationKind.Alchemist)}";
        protected override bool SubtitleInBody => true;
        protected override string Npc => CampNpcs.Alchemist;

        protected override void BuildBody(VisualElement body)
        {
            // GG-T176 (§25.0.5): on the first visit his camp tip walks the first brew, so that bottle recipe opens selected.
            if (selected == null && CampTips.Pending(S, Id) != null) selected = Brewing.FirstBrew(C, S);
            StationBody(body, CraftStation.Alchemy, false, selected, r => selected = r, this);
        }

        /// <summary>A recipe list over a detail pane — the Alchemist's Brew and the Enchantress's Enchant share it.</summary>
        internal static void StationBody(VisualElement body, CraftStation station, bool upgrades, RecipeDef selected, Action<RecipeDef> onPick, HubPage page)
        {
            var list = Ui.ListPanel(body);
            var detail = Ui.DetailPanel(body);
            BlacksmithScreen.RecipeList(list, detail, GameApp.I.Content, GameApp.I.Save, station, upgrades, selected, onPick, page);
        }
    }

    // ── Enchantress: enchant (§25.3.4, GG-T119) ──────────────────────────────────

    /// <summary>The Enchantress's station: the Enchant recipes that used to be the Alchemist's second tab. Every
    /// enchant is Master tier and she has no ladder of her own to climb, so her recipes still unlock with the
    /// Alchemist's station level (her reagents are his brews); her header says how far off that is.</summary>
    public sealed class EnchantressScreen : HubPage
    {
        private RecipeDef selected;
        /// <summary>0 = Armor (GG-T179: bind a passive into a worn piece), 1 = Weapons (the elemental weapon enchants).</summary>
        private int tab;
        private ArmorEnchanting.Target piece;
        private ArmorEnchantDef enchant;
        public override string Id => "enchantress";
        protected override string Title => "Enchantress";
        protected override string Backdrop => "swamp";
        protected override string Subtitle => "Unlocks with the Alchemist: " + StationProgress.HeaderText(S, StationKind.Alchemist);
        protected override bool SubtitleInBody => true;
        protected override string Npc => CampNpcs.Enchantress;

        protected override void BuildBody(VisualElement body)
        {
            Ui.Tabs(new[] { "Armor", "Weapons" }, tab, i => { tab = i; Refresh(); }, body);
            if (tab == 1) { AlchemistScreen.StationBody(body, CraftStation.Enchant, true, selected, r => selected = r, this); return; }
            ArmorBody(body);
        }

        /// <summary>Armor enchants (GG-T179, §25.0.8): pick a worn piece, then an enchant; Enchant binds its passive into
        /// the piece. Until the first enchant is done, both start on what her camp tip walks (<see cref="ArmorEnchanting.First"/>).</summary>
        private void ArmorBody(VisualElement body)
        {
            var pieces = ArmorEnchanting.Pieces(C, S);
            var plan = ArmorEnchanting.First(C, S);
            if (plan != null && piece == null && enchant == null) { piece = plan.Target; enchant = plan.Enchant; }
            piece = piece != null ? pieces.FirstOrDefault(p => p.Hero == piece.Hero && p.Slot == piece.Slot) : null;
            piece ??= pieces.FirstOrDefault();

            // GG-T1873: worn armor and enchants share one scroll region filling the height over the detail pane.
            var plist = Ui.List(Ui.Panel(body, fill: true, name: "enchant-list")).contentContainer;
            Ui.Section("Worn armor", plist).name = "enchant-pieces";
            if (pieces.Count == 0)
                Ui.Empty("Nobody wears armor yet. Forge a piece at the Blacksmith, then bring it here.", plist);
            foreach (var t in pieces)
            {
                var target = t;
                string hero = C.TryClass(t.Hero.ClassId)?.Name ?? t.Hero.ClassId;
                var bound = ArmorEnchanting.PassiveOf(C, t.Piece.Enchant);
                var row = ItemUi.Row(t.Item, bound != null ? $"{hero} ✦ {bound.Name}" : hero, () => { piece = target; Refresh(); }, plist, true);
                row.name = t.RowName;
                if (piece != null && piece.Hero == t.Hero && piece.Slot == t.Slot) Ui.Select(row);
            }

            var elist = plist;
            Ui.Section("Enchants", elist);
            foreach (var e in ArmorEnchanting.All(C))
            {
                var ench = e;
                bool locked = !ArmorEnchanting.IsUnlocked(S, e);
                bool can = !locked && ArmorEnchanting.HasInputs(S, e) && ArmorEnchanting.CanAfford(C, S, e);
                var passive = C.TryAbility(e.Passive);
                var row = Ui.MenuRow(e.Name, locked ? $"🔒 {StationProgress.TierName(e.Tier)}" : Ui.Gold(ArmorEnchanting.Gold(C, S, e)),
                    () => { enchant = ench; Refresh(); }, elist);
                row.KindIcon(RowKind.Ability, passive != null ? Art.AbilityIcon(passive) : null); // GG-T746 (§26.7), else the Ability emblem
                row.name = ArmorEnchanting.EnchantRowPrefix + e.Id;
                if (!can) row.style.opacity = Dim.Row;
                if (enchant == e) Ui.Select(row);
            }

            var detail = Ui.DetailPanel(body);
            if (enchant == null) { Ui.Empty("Pick an enchant to see what it binds.", detail); return; }
            var p = C.TryAbility(enchant.Passive);
            Ui.Heading($"{enchant.Name} — {p?.Name ?? enchant.Passive}", detail);
            GlossaryText.Create(p?.Description ?? "", "kit-text dim", detail);
            Ui.Section("Needs", detail);
            foreach (var ic in enchant.Inputs)
            {
                int have = S.Count(ic.Item);
                Ui.Para($"{(have >= ic.Count ? "✔" : "✘")} {C.TryItem(ic.Item)?.Name ?? ic.Item} ×{ic.Count}   (have {have})", detail, have >= ic.Count ? "good" : "bad");
            }
            int gold = ArmorEnchanting.Gold(C, S, enchant);
            Ui.Para($"{(S.Gold >= gold ? "✔" : "✘")} {Ui.Gold(gold)}" + (gold == 0 && enchant.Gold > 0 ? "  (first rune free)" : gold > 0 && gold < enchant.Gold ? "  (Enchanter's Wagon)" : ""), detail, S.Gold >= gold ? "good" : "bad");
            bool unlocked = ArmorEnchanting.IsUnlocked(S, enchant);
            if (!unlocked)
                Ui.Para($"🔒 Requires {StationProgress.TierName(enchant.Tier)} (alchemist is {StationProgress.LevelName(S, StationKind.Alchemist)})", detail, "bad");
            if (piece != null)
            {
                string hero = C.TryClass(piece.Hero.ClassId)?.Name ?? piece.Hero.ClassId;
                var old = C.TryArmorEnchant(piece.Piece.Enchant);
                Ui.Para($"Into the {hero}'s {piece.Item.Name}" + (old != null && old != enchant ? $" (replaces {old.Name})" : ""), detail, "gold");
            }
            var btn = RowIcon.IconButton("Enchant", RowKind.Confirm, () => Done(ArmorEnchanting.Enchant(C, S, piece?.Hero, piece?.Slot ?? EquipmentSlot.Head, enchant)), "btn--primary", Ui.Actions(detail));
            btn.name = CampTips.CraftButton;
            Ui.SetEnabled(btn, piece != null && unlocked && ArmorEnchanting.HasInputs(S, enchant) && ArmorEnchanting.CanAfford(C, S, enchant) && piece.Piece.Enchant != enchant.Id);
        }
    }

    // ── Caravan upgrades (§25.12) ────────────────────────────────────────────────

    public sealed class CaravanScreen : HubPage
    {
        public override string Id => "caravan";
        protected override string Title => "Caravan";
        protected override string Backdrop => "desert";

        protected override void BuildBody(VisualElement body)
        {
            var head = Ui.Panel(body);
            Ui.Para("The caravan travels with the party — its wagons are the shops between battles. Upgrade it for the long road.", head);
            BarkLine(C, S, head);
            var list = Ui.ListPanel(body);
            foreach (var def in C.Caravan.Values.Where(x => Features.OffersCaravan(x.Id)))
            {
                var d = def;
                int lv = CaravanUpgrades.Level(S, d.Id);
                var next = CaravanUpgrades.NextLevel(C, S, d.Id);
                var card = Ui.Card(list, null, null, out var col, "caravan-" + d.Id);
                // GG-T746 (§26.7): the upgrade's pixel icon, else the Station emblem, leads the card.
                var ic = card.KindIcon(RowKind.Station, Art.ItemIcon(d.Icon));
                ic.style.width = ic.style.height = Kit.Thumb;
                ic.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
                var top = Ui.El("row", col);
                Ui.Name(d.Name, top, "grow");
                Ui.Caption($"Lv {lv}/{d.Levels.Count}", top, "gold");
                var crewBlurb = ContentDb.Blurb(C.CrewBlurbs, d.Id); // GG-T374: Prose "character:<crew>"
                if (lv > 0) Ui.Para("Now: " + d.Levels[lv - 1].Text, col, "good");
                if (next != null) Ui.Para("Next: " + next.Text, col);
                // GG-T1868: the keeper's lore and the full description are one tap (or a hold) away, not printed on every row.
                Func<VisualElement, bool> read = sheet =>
                {
                    Ui.Name(d.Name, sheet);
                    if (!string.IsNullOrEmpty(d.Crew)) Ui.Caption("Crew: " + CaravanUpgrades.CrewName(d), sheet, "gold");
                    if (!string.IsNullOrEmpty(crewBlurb)) Ui.Para(crewBlurb, sheet, "dim");
                    Ui.Para(d.Description, sheet, "dim");
                    return true;
                };
                InspectCard.Attach(ic, read, tap: true);
                InspectCard.Attach(top, read, tap: true);
                var b = RowIcon.IconButton(next == null ? "Maxed" : Ui.Gold(next.Cost), RowKind.Confirm, () => Confirm.Show($"Buy {d.Name} ({next.Text}) for {Ui.Gold(next.Cost)}?", "Buy", () => Done(CaravanUpgrades.Buy(C, S, d.Id), cue: "camp.caravan")), "btn--small", Ui.Actions(col), confirmTap: false);
                b.name = "caravan-buy-" + d.Id;
                Ui.SetEnabled(b, next != null && S.Gold >= next.Cost);
            }
        }

        private static int barkTurn = Environment.TickCount & 0xFFFF;

        /// <summary>One rotating crew bark (GG-T75, §25.12.1): a quoted line and who said it — flavor only, nothing to
        /// tap or answer. Each call advances the rotation, so every rebuild of Camp or the Caravan shows the next line.</summary>
        public static VisualElement BarkLine(ContentDb c, SaveData s, VisualElement parent)
        {
            var (crew, line) = CaravanUpgrades.Bark(c, s, barkTurn++);
            if (line == null) return null;
            var box = Ui.El("col", parent);
            box.name = "caravan-bark";
            Ui.Para($"\"{line}\"", box, "gold");
            Ui.Caption("— " + CaravanUpgrades.CrewName(crew), box, "dim");
            return box;
        }
    }

    // ── Summon Circle (§25.10) ───────────────────────────────────────────────────

    public sealed class SummonScreen : HubPage
    {
        public override string Id => "summon";
        protected override string Title => "Summon";
        protected override string Backdrop => "cityruins";

        /// <summary>The open tab: one per open banner (<see cref="Gacha.Open"/>), then Recruit.</summary>
        private int tab;

        /// <summary>The open Rates list's height in design px: about ten rows, the rest scrolls.</summary>
        private const float OddsListMaxHeight = 560;

        protected override void BuildBody(VisualElement body)
        {
            bool busy = Paywall.Pending(S, PaywallKind.Summon) != null;
            int starter = SummonTickets.Starter(S); // GG-T1604: the first visit's Tickets
            if (starter > 0) { App.Persist(); App.Toast($"The circle gives you {TicketText.Of(starter)} to begin with."); }
            var banners = Gacha.Open(C, DateTime.UtcNow);
            var names = banners.Select(TabName).Append("Recruit").ToArray();
            if (tab >= names.Length) tab = 0;
            Ui.Tabs(names, tab, i => { tab = i; Refresh(); }, body).name = "summon-tabs";
            if (tab < banners.Count) Banner(body, banners[tab], busy);
            else Recruit(body, busy);
        }

        private string TabName(BannerDef b) => b.Kind == BannerKind.Standard
            ? "Standard"
            : string.Join(" & ", b.Featured.Select(id => C.TryClass(id)?.Name ?? id));

        /// <summary>"3d 4h", "5h 12m", "9m 30s": the time left on a Featured banner.</summary>
        internal static string Left(TimeSpan t) =>
            t.TotalDays >= 1 ? $"{(int)t.TotalDays}d {t.Hours}h"
            : t.TotalHours >= 1 ? $"{(int)t.TotalHours}h {t.Minutes}m"
            : $"{Math.Max(0, t.Minutes)}m {Math.Max(0, t.Seconds)}s";

        /// <summary>A banner's tab (§25.10.3, GG-T1428): the featured portraits and the countdown to its close, the pity
        /// meters, the Rates list, the wallet, and the x1 and x<see cref="BannerDef.MultiCount"/> pulls naming what they take.</summary>
        private void Banner(VisualElement body, BannerDef b, bool busy)
        {
            var w = Ui.Panel(body, b.Name, false, "summon-banner-" + b.Id);
            // GG-T1789 (POL-C9): the banner window takes the column's spare height and spreads its blocks through it,
            // so a tall phone has no dead band under the pull buttons; it never shrinks, so an open Rates list still fits.
            w.style.flexGrow = 1;
            w.style.flexShrink = 0;
            w.style.justifyContent = Justify.SpaceBetween;
            var head = Ui.El("row", w);
            foreach (var id in b.Featured.Where(id => C.TryClass(id) != null)) Ui.Thumb(id, head, Kit.Thumb).name = "summon-featured-" + id;
            var col = Ui.El("col grow", head);
            int pool = Gacha.Pool(C, S, b).Count;
            Ui.Para(b.Kind == BannerKind.Featured
                ? $"{TabName(b)} answers {OddsText.Percent(b.FeaturedShare)} of the pulls here; the rest are any of {pool} heroes."
                : $"Any of {pool} heroes may answer, each as likely as the next. A hero you already have comes as an {Gacha.EchoName}.", col, "dim");
            var closes = Gacha.Closes(C, b, DateTime.UtcNow);
            var ends = Ui.Caption(closes is DateTime at ? "Ends in " + Left(at - DateTime.UtcNow) : "Always open", col, "gold");
            ends.name = "summon-ends";
            if (closes is DateTime close)
                ends.schedule.Execute(() =>
                {
                    var left = close - DateTime.UtcNow;
                    if (left <= TimeSpan.Zero) { Refresh(); return; }
                    ends.text = "Ends in " + Left(left);
                }).Every(1000);
            Pity(w, b);
            RatesRow(w, b);

            int diamonds = DiamondExchange.Have(S), tickets = SummonTickets.Have(S);
            var wallet = Ui.Actions(w);
            var img = wallet.KindIcon(RowKind.Diamond, Art.ItemIcon(Crafting.DiamondId)); // GG-T745 (§26.7)
            img.style.width = img.style.height = Size.IconS;
            img.style.marginLeft = Space.TargetHalfGap;
            img.style.marginRight = 0;
            Ui.Name($"{ShardText.ShortPlural}: {Ui.Count(diamonds)} · {TicketText.Plural}: {Ui.Count(tickets)}", wallet, "grow").style.marginLeft = Space.S8;
            var row = Ui.Actions(w);
            PullButton(row, b, 1, busy, "btn--small btn--primary").name = "summon-draw";
            PullButton(row, b, b.MultiCount, busy, "btn--small").name = "summon-draw-multi";
            if (!LootBoxPolicy.PaidAllowed) Ui.Para(LootBoxPolicy.OffLine, w, "dim").name = "summon-paid-off"; // GG-T1433
            else if (Gacha.Price(S, b, 1).shards > 0 && diamonds < b.PullShards)
            {
                Ui.Para($"Short of {ShardText.ShortPlural}, the rest is paid in gold at the exchange rate.", w, "dim");
                RowIcon.IconButton(ShardText.Store, RowKind.Diamond, DiamondStores.Open, "btn--small", Ui.Actions(w)).name = "summon-diamond-store"; // GG-T750
            }
        }

        /// <summary>The pity meters: pulls toward a guaranteed new hero and, on a Featured banner, a guaranteed featured one.</summary>
        private void Pity(VisualElement w, BannerDef b)
        {
            var box = Ui.El("col", w);
            box.name = "summon-pity";
            if (b.NewHeroPity > 0) Meter(box, "New hero", Gacha.NewHeroMiss(S, b.Kind), b.NewHeroPity, "summon-pity-new");
            if (b.Kind == BannerKind.Featured && b.FeaturedPity > 0) Meter(box, "Featured", S.Summon?.FeaturedMiss ?? 0, b.FeaturedPity, "summon-pity-featured");
            var due = Gacha.DueLine(Gacha.Due(S, b, Gacha.Pool(C, S, b)));
            if (due != null) Ui.Para(due, box, "good");
        }

        private static void Meter(VisualElement parent, string label, int miss, int pity, string name)
        {
            var row = Ui.El("row", parent);
            row.name = name;
            Ui.Caption(label, row);
            var g = Ui.Gauge(miss / (float)pity, row);
            g.style.flexGrow = 1;
            g.style.marginLeft = g.style.marginRight = Space.S8;
            int left = Math.Max(1, pity - miss);
            Ui.Caption(left == 1 ? "Next pull" : $"Within {left} pulls", row, "dim");
        }

        /// <summary>What a pull takes, as its button names it: "1 Summon Ticket", "1,000 Mana Shards", "40 Mana Shards + 360 G".</summary>
        private string PayText(int tickets, int shards)
        {
            var parts = new List<string>();
            if (tickets > 0) parts.Add(TicketText.Of(tickets));
            if (shards > 0)
            {
                var (qd, qg) = DiamondExchange.Quote(S, shards);
                if (qd > 0) parts.Add(ShardText.ShortOf(qd));
                if (qg > 0) parts.Add(Ui.Gold(qg));
            }
            return string.Join(" + ", parts);
        }

        private Button PullButton(VisualElement row, BannerDef b, int count, bool busy, string classes)
        {
            var (tickets, shards) = Gacha.Price(S, b, count);
            string pay = PayText(tickets, shards);
            var btn = RowIcon.IconButton($"x{count} · {pay}", RowKind.Confirm, () =>
            {
                var can = Gacha.CanPull(C, S, b, count);
                if (!can.Ok)
                {
                    if (shards > 0 && !DiamondExchange.CanPay(S, shards).Ok) DiamondStores.OfferStore(can.Message);
                    else { App.Toast(can.Message); App.Audio.Sfx("ui.error"); }
                    return;
                }
                Confirm.Show($"Pay {pay} for {(count == 1 ? "a pull" : $"{count} pulls")} on {b.Name}? {RatesLine(b, Gacha.Odds(C, S, b))}.", "Summon", () => Pull(b, count));
            }, classes, row, confirmTap: false);
            Ui.SetEnabled(btn, !busy && !S.TestMode && Gacha.Pool(C, S, b).Count > 0 && LootBoxPolicy.Allows(S, b, count).Ok);
            return btn;
        }

        /// <summary>Pays and starts the pull, then reveals every result in pull order (<see cref="SummonReveal"/>) and
        /// offers the new heroes' looks.</summary>
        private void Pull(BannerDef b, int count)
        {
            List<PullRecord> pulls = null;
            TimedJobView.Begin(now =>
            {
                var r = Gacha.Pull(C, S, b, count, now, out var job);
                if (r.Ok) pulls = S.Summon.History.Take(count).Reverse().ToList();
                return (r, job);
            }, count == 1 ? "The circle reaches out" : $"The circle reaches out {count} times", r =>
            {
                if (!r.Ok || pulls == null) { Refresh(); return; }
                SummonReveal.Show(pulls, b, () => VariantPicker.ShowPending(Refresh)); // GG-T532: then pick their look
            });
        }

        /// <summary>The next pull's odds in a line: the pity due, the featured share, or the even chance of each hero.</summary>
        private string RatesLine(BannerDef b, List<DrawOdds> odds)
        {
            var due = Gacha.DueLine(Gacha.Due(S, b, Gacha.Pool(C, S, b)));
            if (due != null) return due.TrimEnd('.');
            if (odds.Count == 0) return "No one is left to answer";
            if (b.Kind == BannerKind.Featured && b.Featured.Count > 0)
                return $"Featured: {OddsText.Percent(b.FeaturedShare)} together; each other hero {odds[odds.Count - 1].Label}";
            return $"Each of these {odds.Count} heroes: {odds[0].Label}";
        }

        /// <summary>The banner's odds, disclosed before purchase (GG-T503): one line (<c>summon-odds</c>) and a <b>Rates</b>
        /// button (<c>summon-odds-btn</c>) that opens the full list (<c>summon-odds-list</c>, a row <c>summon-odds-{classId}</c>
        /// per hero with its chance), all read from <see cref="Gacha.Odds"/>, the list the pull rolls from.</summary>
        private void RatesRow(VisualElement w, BannerDef b)
        {
            var odds = Gacha.Odds(C, S, b);
            var row = Ui.Actions(w);
            var line = Ui.Name(RatesLine(b, odds), row, "gold grow");
            line.name = "summon-odds";
            line.style.whiteSpace = WhiteSpace.Normal;
            // A bounded scroller: every row laid out in full pushed the pull buttons off a page that doesn't scroll (GG-T494).
            var list = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            list.touchScrollBehavior = ScrollView.TouchScrollBehavior.Clamped;
            list.style.maxHeight = OddsListMaxHeight;
            w.Add(list);
            list.name = "summon-odds-list";
            list.style.display = DisplayStyle.None;
            foreach (var o in odds)
            {
                var def = C.TryClass(o.ClassId);
                var r = Ui.El("row", list);
                r.name = $"summon-odds-{o.ClassId}";
                bool featured = b.Featured.Contains(o.ClassId, StringComparer.OrdinalIgnoreCase);
                Ui.Caption((def != null ? $"{def.Name} · {def.Role}" : o.ClassId) + (featured ? " · Featured" : ""), r, featured ? "gold grow" : "grow");
                Ui.Caption(o.Label, r, "dim");
            }
            Ui.Caption($"A hero you already have comes as an {Gacha.EchoName} (+{Gacha.EchoSp} SP).", list, "dim");
            Button toggle = null;
            toggle = RowIcon.IconButton("Rates", RowKind.Menu, () =>
            {
                bool open = list.style.display == DisplayStyle.None;
                list.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
                toggle.Q<Label>().text = open ? "Hide rates" : "Rates"; // GG-T750: the IconButton's label, not Button.text
            }, "btn--small", row);
            toggle.name = "summon-odds-btn";
            RowIcon.IconButton("History", RowKind.Menu, () => App.Router.Push(new SummonHistoryScreen()), "btn--small", row).name = "summon-history-btn";
        }

        /// <summary>The Recruit tab: the gold chosen summon, any hero from the pool by name.</summary>
        private void Recruit(VisualElement body, bool busy)
        {
            int price = Summon.Price(C, S);
            var tag = Paywall.Tag(PaywallKind.Summon, S);
            var list = Ui.ListPanel(body, "Heroes who answer", "summon-pool");
            Ui.Para($"Summon a hero to your side ({tag}). The circle takes a moment to answer.", list, "dim");
            foreach (var d in Summon.Pool(C, S).Where(d => Features.Offers(d.Id)))
            {
                var def = d;
                bool owned = S.Hero(d.Id) != null;
                Ui.Card(list, d.Id, d.Name, out var col);
                GlossaryText.Create(d.Card ?? d.Description ?? "", "kit-text dim", col);
                var b = RowIcon.IconButton(owned ? "Joined" : "Recruit", RowKind.Confirm, () => Confirm.Show($"Summon {def.Name} for {Ui.Gold(price)}?", "Summon", () =>
                    TimedJobView.Begin(now => (Summon.BeginRecruit(C, S, def.Id, now, out var job), job), $"Summoning {def.Name}", _ => VariantPicker.ShowPending(Refresh))), "btn--small", Ui.Actions(col), confirmTap: false); // GG-T532: then pick their look
                Ui.SetEnabled(b, !owned && !busy && S.Gold >= price);
            }
        }
    }

    /// <summary>The Summon history (§25.10.3, GG-T1430): the save's last <see cref="Gacha.HistoryKeep"/> banner pulls,
    /// newest first, each a card (<c>summon-history-{i}</c>) with the hero, what the pull gave, when, on which banner and
    /// what paid for it. Reached from a banner tab's <b>History</b> button.</summary>
    public sealed class SummonHistoryScreen : HubPage
    {
        public override string Id => "summon-history";
        protected override string Title => "Summon history";
        protected override string Backdrop => "cityruins";

        protected override void BuildBody(VisualElement body)
        {
            var pulls = S.Summon?.History ?? new List<PullRecord>();
            var list = Ui.ListPanel(body, pulls.Count == 0 ? "Pulls" : $"Last {pulls.Count} pulls", "summon-history");
            if (pulls.Count == 0)
            {
                var state = Ui.EmptyState(list, RowKind.Summon, "No pulls yet. Each banner pull is kept here, newest first.");
                RowIcon.IconButton("Summon", RowKind.Summon, () => App.Router.Pop(), "btn--small", Ui.Actions(state)).name = "summon-history-summon";
                return;
            }
            for (int i = 0; i < pulls.Count; i++)
            {
                var p = pulls[i];
                var def = C.TryClass(p.ClassId);
                var banner = C.TryBanner(p.BannerId);
                bool featured = SummonReveal.IsFeatured(banner, p);
                Ui.Card(list, def?.Id, def?.Name ?? p.ClassId, out var col, "summon-history-" + i);
                Ui.Caption(SummonReveal.Badge(p, featured), col, p.Result == PullResult.New ? "gold" : "good");
                string when = p.Utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
                string paid = string.IsNullOrEmpty(p.Paid) ? "" : " · " + p.Paid;
                Ui.Caption($"{when} · {banner?.Name ?? p.BannerId ?? "Standard"}{paid}", col, "dim");
            }
        }
    }

    /// <summary>A banner pull's reveal (GG-T343, GG-T1429): each result in pull order plays over time — the portrait swells
    /// out of the light, a <b>New</b> or <b>Echo</b> badge lands with its own cue, and a featured hero gets a gold flash
    /// and a bigger fanfare. A tap finishes the playing result, the next tap moves on. <b>Skip</b> (multi-pulls) and the
    /// end of a multi-pull open a summary grid of every result; Continue closes it. Under Reduce Motion each result
    /// fades in at full size with no swell, pop or flash. A new hero whose pull fizzled (refunded) isn't shown.</summary>
    internal static class SummonReveal
    {
        private const float PortraitSize = 420;

        public static void Show(IReadOnlyList<PullRecord> pulls, BannerDef banner, Action onClose)
        {
            var app = GameApp.I;
            var shown = pulls.Where(p => app.Content.TryClass(p.ClassId) != null && (p.Result != PullResult.New || app.Save.Hero(p.ClassId) != null)).ToList();
            if (shown.Count == 0) { onClose?.Invoke(); return; }
            bool calm = app.Settings.ReduceMotion;
            var dim = Palette.Ink.WithA(0.9f);
            var veil = Ui.El("fill veil col center", app.Overlay);
            veil.name = "summon-reveal";
            veil.pickingMode = PickingMode.Position;
            // The reveal's name and role are bare text, not a window: the plain veil left the Summon Circle's own text
            // showing through them. Dim the page almost out so the hero carries the moment.
            veil.style.backgroundColor = dim;
            var stage = Ui.El("col center", veil);
            stage.name = "summon-reveal-stage";
            int at = -1;
            bool ready = false, summary = false;
            Coroutine playing = null;
            Action settle = null;
            Button skip = null;
            if (shown.Count > 1)
            {
                skip = Ui.Button("Skip", () => Summary(), "btn--small", veil);
                skip.name = "summon-reveal-skip";
                skip.style.position = Position.Absolute;
                skip.style.top = skip.style.right = Space.S24;
                skip.RegisterCallback<ClickEvent>(e => e.StopPropagation());
            }

            veil.RegisterCallback<ClickEvent>(_ =>
            {
                if (summary) return;
                if (!ready) { Stop(); settle?.Invoke(); ready = true; return; }
                Next();
            });
            Next();

            void Stop()
            {
                if (playing != null) app.StopCoroutine(playing);
                playing = null;
            }

            void Close()
            {
                Stop();
                veil.RemoveFromHierarchy();
                onClose?.Invoke();
            }

            void Next()
            {
                if (++at >= shown.Count) { if (shown.Count > 1) Summary(); else Close(); return; }
                ready = false;
                stage.Clear();
                veil.style.backgroundColor = dim;
                var p = shown[at];
                var def = app.Content.TryClass(p.ClassId);
                bool featured = IsFeatured(banner, p);
                var thumb = Ui.Thumb(def.Id, stage, PortraitSize);
                thumb.name = "summon-reveal-hero";
                var badge = Ui.Text(Badge(p, featured), "h2 shadow " + (p.Result == PullResult.New ? "gold" : "good"), stage);
                badge.name = "summon-reveal-badge";
                badge.style.marginTop = Space.S16;
                var name = Ui.Text(def.Name, "title shadow" + (featured ? " gold" : ""), stage);
                name.style.marginTop = Space.S8;
                var role = Ui.Text(Line(def, p), "h2 shadow", stage);
                var tap = Ui.Text(shown.Count > 1 ? $"{at + 1} of {shown.Count} · Tap to continue" : "Tap to continue", "small dim", stage);
                tap.name = "summon-reveal-tap";
                tap.style.marginTop = Space.S48;
                settle = () =>
                {
                    thumb.style.scale = new Scale(Vector2.one);
                    thumb.style.opacity = badge.style.opacity = name.style.opacity = role.style.opacity = tap.style.opacity = 1f;
                    badge.style.scale = name.style.scale = new Scale(Vector2.one);
                    veil.style.backgroundColor = dim;
                };
                thumb.style.scale = new Scale(calm ? Vector2.one : Vector2.zero);
                thumb.style.opacity = calm ? 0f : 1f;
                badge.style.opacity = name.style.opacity = role.style.opacity = tap.style.opacity = 0f;
                playing = app.StartCoroutine(Play(p, featured, thumb, badge, name, role, tap));
            }

            System.Collections.IEnumerator Play(PullRecord p, bool featured, VisualElement thumb, Label badge, Label name, Label role, Label tap)
            {
                bool isNew = p.Result == PullResult.New;
                app.Audio.Sfx(isNew ? "camp.summon" : "ui.select", isNew ? 0.9f : 0.7f);
                if (calm) yield return Tween.Run(Timing.Quick, k => thumb.style.opacity = k);
                else
                {
                    float peak = featured ? 1.2f : 1.12f;
                    yield return Tween.Run(featured ? Timing.Emphasis : Timing.Standard,
                        k => thumb.style.scale = new Scale(Vector2.one * (k < 0.7f ? k / 0.7f * peak : peak - (peak - 1f) * (k - 0.7f) / 0.3f)), Tween.EaseOut);
                }
                thumb.style.scale = new Scale(Vector2.one);
                if (featured)
                {
                    app.Audio.Sfx("battle.victory");
                    app.Audio.Sfx("battle.warcry", 0.6f);
                    if (!calm) yield return Tween.Run(Timing.Dramatic, k => veil.style.backgroundColor = Color.Lerp(Palette.Gold.WithA(0.55f), dim, k), Tween.EaseOut);
                }
                else app.Audio.Sfx(isNew ? "battle.powerup" : "ui.diamond", isNew ? 0.7f : 0.6f);
                yield return Tween.Run(Timing.Quick, k => badge.style.opacity = name.style.opacity = role.style.opacity = k);
                if (!calm) yield return Tween.Pop(isNew ? name : badge, featured ? 1.3f : 1.15f, featured ? Timing.Standard : 0.2f);
                tap.style.opacity = 1f;
                playing = null;
                ready = true;
            }

            void Summary()
            {
                if (summary) return;
                summary = true;
                Stop();
                stage.Clear();
                veil.style.backgroundColor = dim;
                skip?.RemoveFromHierarchy();
                stage.name = "summon-reveal-summary";
                Ui.Text("The circle answered", "h2 shadow gold", stage).style.marginBottom = Space.S16;
                var grid = Ui.El("row", stage);
                grid.style.flexWrap = Wrap.Wrap;
                grid.style.justifyContent = Justify.Center;
                for (int i = 0; i < shown.Count; i++)
                {
                    var p = shown[i];
                    bool featured = IsFeatured(banner, p);
                    var cell = Ui.El("col center", grid);
                    cell.name = "summon-summary-" + i;
                    cell.style.marginLeft = cell.style.marginRight = cell.style.marginBottom = Space.S8;
                    var t = Ui.Thumb(p.ClassId, cell, Kit.Thumb);
                    if (featured)
                    {
                        t.style.borderTopWidth = t.style.borderBottomWidth = t.style.borderLeftWidth = t.style.borderRightWidth = 3;
                        t.style.borderTopColor = t.style.borderBottomColor = t.style.borderLeftColor = t.style.borderRightColor = Palette.Gold;
                    }
                    Ui.Caption(app.Content.TryClass(p.ClassId)?.Name ?? p.ClassId, cell, featured ? "gold" : null);
                    Ui.Caption(Badge(p, featured), cell, p.Result == PullResult.New ? "gold" : "dim");
                }
                var done = Ui.Button("Continue", Close, "btn--primary", Ui.Actions(stage));
                done.name = "summon-reveal-done";
                done.RegisterCallback<ClickEvent>(e => e.StopPropagation());
                if (!calm) app.StartCoroutine(Tween.Fade(grid, 0f, 1f, Timing.Standard));
            }
        }

        internal static bool IsFeatured(BannerDef banner, PullRecord p) =>
            banner?.Kind == BannerKind.Featured && banner.Featured.Contains(p.ClassId, StringComparer.OrdinalIgnoreCase);

        /// <summary>"New", "New · Featured", "Echo +1 SP", "+5 Ticket fragments".</summary>
        internal static string Badge(PullRecord p, bool featured) => p.Result switch
        {
            PullResult.New => featured ? "New · Featured" : "New",
            PullResult.Echo => $"{Gacha.EchoName} +{Gacha.EchoSp} SP",
            _ => $"+{p.Fragments} {TicketText.Fragments}",
        };

        private static string Line(ClassDef def, PullRecord p) =>
            p.Result == PullResult.New ? $"{def.Role} · answers the circle" : $"{def.Role} · answers again";
    }

    // ── The Crucible (§22.10) ────────────────────────────────────────────────────

    public sealed class CrucibleScreen : HubPage
    {
        public override string Id => "crucible";
        protected override string Title => "Crucible";
        protected override string Backdrop => HubScreen.ThemeFor(C, S);

        /// <summary>The Camp row's value: the save's best run in <paramref name="mode"/> (the mode the next run plays,
        /// GG-T945: records are kept per turn mode), or an invitation before the first one.</summary>
        public static string RecordText(SaveData s, TurnMode mode) => CampMenu.CrucibleRecord(s, mode);

        protected override void BuildBody(VisualElement body)
        {
            // GG-T454 (the menu kit): the rules and the record in one scrolling list, the team under it, and the primary action
            // in the footer at the bottom of the screen, where the thumb is (POL-C4); nothing can push Enter off a phone (GG-T439).
            var list = Ui.ListPanel(body, "The Crucible", "crucible-intro");
            Ui.Para("A horde that never ends: wave after wave, each one harder, until your team falls or you leave. Wounds, mana and items carry from wave to wave, with no camp between. " +
                    "Every wave you clear banks gold, and every coin, drop and point of experience earned is <b>kept</b>, even in defeat. The campaign doesn't move.", list);
            int start = Crucible.PartyLevel(S);
            Ui.Field("First wave", $"Lv {start}, +{Crucible.LevelStep} each wave", list);
            Ui.Field("Horde", $"{Crucible.FirstWaveEnemies}→{Crucible.MaxWaveEnemies} foes, elites from wave {BattleConfig.CrucibleFirstEliteWave + 1}", list);
            Ui.Field("Pay", $"{Ui.Gold(Crucible.WaveGold(0))}, +{BattleConfig.CrucibleWaveGoldPerWave} G a wave", list, "gold");
            var fams = Crucible.Families(C, S);
            Ui.Field("Foes", $"{fams.Count} famil{(fams.Count == 1 ? "y" : "ies")} met", list);
            if (fams.Count > 0)
            {
                var names = Ui.Para(string.Join(", ", fams.Select(f => f[0].Name)), list, "dim");
                names.style.display = DisplayStyle.None;
                Button toggle = null;
                toggle = Ui.Button("Show names", () =>
                {
                    bool show = names.style.display == DisplayStyle.None;
                    names.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                    toggle.text = show ? "Hide names" : "Show names";
                }, "btn--small", list);
                toggle.name = "crucible-foes-toggle";
            }
            var rec = Ui.El("col", list);
            rec.name = "crucible-record";
            Ui.Section("Record", rec);
            bool ran = S.Crucible.Runs > 0;
            var mode = TurnModeSetting.Resolve(S, App.Settings); // GG-T945: the record of the mode the next run plays
            Ui.Field("Best", ran ? $"{S.Crucible.BestFor(mode)} waves · {Ui.Gold(S.Crucible.BestGoldFor(mode))}" : "None yet", rec, "gold");
            Ui.Field("Runs", ran ? $"{S.Crucible.Runs} · {S.Crucible.TotalWaves} waves" : "0", rec);

            // GG-T430: you pick one team before the run (the active party, up to the party-size cap) and it fights
            // every wave; "Change team" swaps it on the Party page and comes straight back here.
            var team = Ui.Panel(body, $"Your team {S.Party.Count}/{Progression.MaxPartySize}", false, "crucible-team");
            // GG-T658: the faces share the row evenly (each cell grows from the avatar's width), with the edge equal to
            // the gap (row padding + cell margins = 24 each), instead of piling up at the left edge (POL-C8).
            var faces = Ui.El("row", team);
            faces.style.flexWrap = Wrap.Wrap;
            faces.style.paddingLeft = faces.style.paddingRight = Space.TargetHalfGap;
            foreach (var id in S.Party)
            {
                var h = S.Hero(id);
                if (h == null) continue;
                var cell = Ui.El("col", faces);
                cell.style.alignItems = Align.Center;
                cell.style.flexGrow = 1; cell.style.flexShrink = 0; cell.style.flexBasis = Kit.Avatar;
                Ui.Margin(cell, left: Space.TargetHalfGap, right: Space.TargetHalfGap);
                Ui.Thumb(id, cell, Kit.Avatar).style.marginRight = Space.S0;
                Ui.Caption($"Lv {Progression.Level(h)}", cell);
            }
            RowIcon.IconButton("Change team", RowKind.Hero, () => App.Router.Push(new PartyScreen()), "btn--small", Ui.Actions(team)).name = "crucible-team-change"; // GG-T750

            bool can = Crucible.CanLaunch(C, S, out var why);
            var foot = Ui.Footer(body, "crucible-footer");
            var costCol = Ui.El("col grow", foot);
            if (!can) Ui.Para(why, costCol, "bad");
            Ui.Caption($"Costs {RestedPool.LaunchCost} Rested: the energy for a run, up to {RestedPool.Max}, +1 every {(int)RestedPool.RegenEvery.TotalMinutes} min", costCol, "dim");
            var enter = RowIcon.IconButton("Enter the Crucible", RowKind.Enter, () => CrucibleLauncher.Launch(false), "btn--primary", foot);
            enter.name = "crucible-enter";
            Ui.SetEnabled(enter, can);
        }
    }

    // ── Bounty board (§22.4, GG-T431) ──────────────────────────────────────────────

    /// <summary>Hunts for marked targets: collect what was slain (with a claim cue), track what is hunted, and take a fresh
    /// poster. Each poster shows the target's portrait under the bounty skull, its title, its kind, the clue naming its
    /// stage, and the reward.</summary>
    public sealed class BountyScreen : HubPage
    {
        public override string Id => "bounties";
        protected override string Title => "Bounties";
        /// <summary>The Tome a just-claimed bounty paid (GG-T301), offered for teaching at the top of the board.</summary>
        private string claimedTome;

        protected override void BuildBody(VisualElement body)
        {
            if (C.TryItem(claimedTome) is ItemDef paid && S.Count(paid.Id) > 0)
            {
                var tw = Ui.Panel(body, $"Reward: {paid.Name}", false, "bounty-tome");
                TomeUi.Offer(tw, paid, r => { if (r.Ok) claimedTome = null; });
            }
            var list = Ui.ListPanel(body, "Wanted", "bounty-board");
            Ui.Para("Hunt a marked foe: play the stage its clue names, kill the one wearing the skull, then collect here.", list, "dim");
            var board = Bounties.Board(C, S);
            if (board.Count == 0) Ui.Empty("No one is posted yet. Clear more stages.", list);
            foreach (var b in board) Poster(b, list);
        }

        private void Poster(BountyDef b, VisualElement list)
        {
            var bounty = b;
            var target = C.TryClass(b.TargetClass);
            var reward = C.TryItem(b.RewardItem);
            bool slain = Bounties.IsSlain(S, b.Id), hunting = Bounties.IsTaken(S, b.Id);
            var card = Ui.Card(list, null, null, out var col, "poster-" + b.Id);
            var face = Ui.El(null, null);
            card.Insert(0, face);
            face.style.position = Position.Relative;
            if (target != null) Ui.Thumb(target.Id, face, Kit.Thumb);
            var skull = BountySkull.Make(Size.IconM, face);
            skull.style.position = Position.Absolute;
            skull.style.right = Space.S16; skull.style.top = -Space.S4;
            // GG-T1881: the poster's one action sits on its right edge beside the name, not on a row of its own.
            var acts = Ui.El("row", col);
            var title = Ui.Name(b.Name, acts);
            title.style.flexShrink = 1; title.style.minWidth = 0; title.style.whiteSpace = WhiteSpace.Normal;
            Ui.El("grow", acts);
            Ui.Caption($"{target?.Name ?? b.TargetClass} · tougher variant", col, "dim");
            Ui.Para(Bounties.Clue(C, b), col, "gold");
            int sp = Bounties.SkillPointsPaid(b);
            Ui.Para($"Reward {Ui.Gold(Bounties.RewardGold(C, b))}{(reward != null ? " + " + reward.Name : "")}{(sp > 0 ? $" + {sp} SP each" : "")}", col, "dim");
            TomeUi.Preview(col, reward); // GG-T301: what a Tome reward teaches
            if (slain)
            {
                var claim = RowIcon.IconButton("Claim", RowKind.Confirm, () => // GG-T750 (§26.7)
                {
                    claimedTome = reward?.Type == ItemType.Tome ? reward.Id : null;
                    var r = Bounties.Claim(C, S, bounty.Id);
                    Done(r);
                    if (r.Ok) App.Audio.Sfx("battle.pickup"); // the claim cue: the purse fills
                }, "btn--primary btn--small", acts);
                claim.name = "bounty-claim-" + b.Id;
            }
            else if (hunting)
            {
                var stop = RowIcon.IconButton("Hunting", RowKind.Close, () => Confirm.Show($"Stop hunting {bounty.Name}?", "Stop", () => Done(Bounties.Abandon(C, S, bounty.Id))), "btn--small", acts);
                stop.name = "bounty-abandon-" + b.Id;
            }
            else
            {
                var take = RowIcon.IconButton("Take", RowKind.Confirm, () => Done(Bounties.Take(C, S, bounty.Id)), "btn--small", acts);
                take.name = "bounty-" + b.Id;
            }
        }
    }

    // ── Bestiary ─────────────────────────────────────────────────────────────────

    public sealed class BestiaryScreen : HubPage
    {
        /// <summary>The tile behind an unseen creature's black silhouette: light enough that the shape reads against it.</summary>
        internal static readonly Color UnseenTile = new Color(0.45f, 0.50f, 0.60f, 1f);
        private string selected;
        public override string Id => "bestiary";
        protected override string Title => "Bestiary";

        /// <summary>The creature the detail shows (GG-T925: the Codex opens a linked foe here).</summary>
        internal string Selected { get => selected; set => selected = value; }

        protected override void BuildBody(VisualElement body) => BuildGrid(body);

        /// <summary>The grid and detail into <paramref name="body"/>: this screen's own body, and the Codex's Foes tab
        /// (GG-T925), which embeds it rather than copying it. <paramref name="onPick"/> hears each creature opened;
        /// <paramref name="isNew"/> puts the New badge on a seen creature's card until it is opened.</summary>
        internal void BuildGrid(VisualElement body, Action<string> onPick = null, Func<string, bool> isNew = null)
        {
            var enemies = C.Classes.Values.Where(c => c.Team == Team.Enemy).OrderBy(c => c.Name).ToList();
            int seen = enemies.Count(e => S.Bestiary.TryGetValue(e.Id, out var b) && b.Seen);
            var grid = Ui.ListPanel(body, $"Seen {seen}/{enemies.Count}", out var scroll, "bestiary-grid");
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            var detail = Ui.DetailPanel(body);
            foreach (var e in enemies)
            {
                var def = e;
                S.Bestiary.TryGetValue(e.Id, out var entry);
                var t = Ui.Thumb(e.Id, grid, 160, thumbOnly: true); // GG-T1210: 100+ cards, baked thumbs only
                t.name = "beast-" + e.Id;
                if (entry == null || !entry.Seen) { t.style.backgroundColor = UnseenTile; t.Q(Ui.ThumbArt).style.unityBackgroundImageTintColor = new Color(0, 0, 0, 0.92f); } // GG-T1889: a black silhouette on a light slate tile (over 4:1), not on the dark card
                else if (isNew != null && isNew(e.Id)) RowIcon.Badge(t, RowKind.New);
                t.AddToClassList("card-tap");
                Ui.Tappable(t, () => { selected = def.Id; Show(detail, def, entry); RowIcon.ClearBadge(t); onPick?.Invoke(def.Id); });
            }
            grid.style.flexDirection = FlexDirection.Column;
            grid.style.flexWrap = Wrap.NoWrap;
            var cells = grid.Children().ToList();
            int laidCols = -1;
            // Listen on the viewport, not the ScrollView: the vertical scroller appears only after the cells fill it,
            // shrinking the viewport without resizing the ScrollView, so a ScrollView listener never re-lays the grid.
            scroll.contentViewport.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                // The content viewport (inside the ScrollView's border, padding and scroller) is what the rows really get.
                laidCols = EvenGrid(grid, cells, scroll.contentViewport.layout.width, 160f, Space.S24, laidCols);
            });
            if (selected != null && C.TryClass(selected) is ClassDef sel) { Show(detail, sel, S.Bestiary.TryGetValue(selected, out var se) ? se : null); onPick?.Invoke(selected); }
            else Ui.Empty("Tap a creature.", detail);
        }

        /// <summary>Fits as many cells of about <paramref name="target"/> px as the width allows, then stretches them so the
        /// left, right and between-cell gutters are all exactly <paramref name="gap"/> (POL-C1/C8): no ragged right edge.
        /// Cells go into explicit row containers (never wrap), so every row has exactly the same count and edges.
        /// Returns the column count laid out; a call with the same count only resizes.</summary>
        private static int EvenGrid(VisualElement grid, List<VisualElement> cells, float width, float target, float gap, int laidCols)
        {
            float w = width - grid.resolvedStyle.paddingLeft - grid.resolvedStyle.paddingRight;
            if (float.IsNaN(w) || w <= target) return laidCols;
            int cols = Mathf.Max(1, Mathf.FloorToInt((w - gap) / (target + gap)));
            // Exact (unfloored) size: flooring dumped the remainder on the right edge, a ragged gutter (POL-C8).
            float size = (w - gap * (cols + 1)) / cols - 0.01f;
            if (cols != laidCols)
            {
                grid.Clear();
                VisualElement row = null;
                for (int i = 0; i < cells.Count; i++)
                {
                    if (i % cols == 0)
                    {
                        row = new VisualElement { name = "grid-row" };
                        row.style.flexDirection = FlexDirection.Row;
                        Ui.Pad(row, left: gap / 2f, right: gap / 2f);
                        Ui.Margin(row, top: i == 0 ? gap / 2f : 0);
                        grid.Add(row);
                    }
                    row.Add(cells[i]);
                }
                // Pad a short last row with invisible fillers so its cells stay the same size as every other row's.
                for (int i = cells.Count % cols; i > 0 && i < cols; i++)
                {
                    var filler = new VisualElement { name = "grid-filler", pickingMode = PickingMode.Ignore };
                    filler.style.visibility = Visibility.Hidden;
                    filler.style.flexBasis = 0; filler.style.flexGrow = 1; filler.style.flexShrink = 1;
                    Ui.Margin(filler, left: gap / 2f, right: gap / 2f);
                    row.Add(filler);
                }
            }
            foreach (var t in cells)
            {
                // Flex shares the row exactly (equal grow, zero basis): a fixed width computed here drifted wider than the row
                // by the cells' own borders and min sizes, leaving a ragged right edge. Height follows the computed size.
                t.style.width = StyleKeyword.Auto; t.style.minWidth = 0; t.style.maxWidth = StyleKeyword.None;
                t.style.flexBasis = 0; t.style.flexGrow = 1; t.style.flexShrink = 1;
                t.style.height = size;
                Ui.Margin(t, gap / 2f, gap / 2f, gap / 2f, gap / 2f);
            }
            return cols;
        }

        /// <summary>The detail panel (§25.11, GG-T290): name and description once Seen, then each knowledge row in the order
        /// defeats unlock it — Speed, HP and stats, elements, abilities — with a locked row reading "???" and a hint
        /// naming how many more defeats the next one takes.</summary>
        private void Show(VisualElement w, ClassDef d, BestiaryEntry e)
        {
            w.Clear();
            if (e == null || !e.Seen) { Ui.Heading("??? — not yet encountered", w, "dim"); return; }
            int tier = BestiaryKnowledge.Tier(e);
            var top = Ui.El("row", w);
            Ui.Heading(d.Name, top, "grow");
            Ui.Caption($"Defeated {e.Defeated}", top, "gold");
            if (!string.IsNullOrEmpty(d.Description)) GlossaryText.Create(d.Description, "kit-text", w);
            if (!string.IsNullOrEmpty(d.Lore) && d.Lore != d.Description) GlossaryText.Create(d.Lore, "kit-text dim", w);
            Fact(w, "beast-known", "Known", Known(tier));
            if (d.Heavy) GlossaryText.Create(InspectCard.HeavyLine, "kit-text gold", w).name = "beast-heavy"; // GG-T892: its keyword and counters, once seen
            if (d.DropTable != null && C.DropTables.TryGetValue(d.DropTable, out var drops) && drops.Entries.Count > 0)
            {
                var names = drops.Entries.OrderByDescending(x => x.Chance).Select(de => C.TryItem(de.Item)).Where(it => it != null)
                    .Select(it => $"<color=#{ColorUtility.ToHtmlStringRGB(Palette.Rarity(it.Rarity))}>{it.Name}</color>");
                Ui.Field("Drops", string.Join(",  ", names), w, null, "beast-drops").Q<Label>("value").enableRichText = true;
            }
            Fact(w, "beast-speed", "Speed", tier >= BestiaryKnowledge.Speed ? $"SPD {d.BaseStats.Speed:0}" : null);
            Fact(w, "beast-stats", "Stats", tier >= BestiaryKnowledge.Stats
                ? $"HP {Formulas.MaxHp(d.BaseStats, 1)} at Lv 1  ·  " + Ui.StatLine(Formulas.StatsAtLevel(d, 1)).Replace("\n", "  ") + $"  ·  EVA {Formulas.Evasion(Formulas.StatsAtLevel(d, 1)):0}%" : null);
            Fact(w, "beast-elements", "Elements", tier >= BestiaryKnowledge.Elements ? BestiaryKnowledge.ElementLine(d) : null);
            Fact(w, "beast-abilities", "Abilities", tier >= BestiaryKnowledge.Abilities ? "" : null);
            if (tier >= BestiaryKnowledge.Abilities)
            {
                var kit = Ui.KitTraits(C, d);
                if (kit.Length > 0) GlossaryText.Create(kit, "kit-text gold", w);
                // What it does standing in a pincer chain (GG-T307, §1.2.3): every foe has its own support, read at this tier.
                GlossaryText.Create(GridGame.Core.Rules.SupportText.Describe(C, d.Support, named: true), "kit-text gold", w).name = "beast-support";
                // Its abilities as chips: hold one to read it whole (GG-T252).
                var ids = d.Abilities.Concat(string.IsNullOrEmpty(d.ChargeAbility) ? Enumerable.Empty<string>() : new[] { d.ChargeAbility }).Distinct();
                var chips = Ui.El("row", w);
                chips.style.flexWrap = Wrap.Wrap;
                foreach (var id in ids)
                {
                    if (!(C.TryAbility(id) is AbilityDef ab) || string.IsNullOrEmpty(ab.Name)) continue;
                    var chip = Ui.El("row", chips);
                    chip.name = "beast-ability-" + ab.Id;
                    chip.style.alignItems = Align.Center;
                    chip.style.minHeight = Kit.Row;
                    Ui.Margin(chip, right: Space.S24);
                    var icon = Art.AbilityIcon(ab);
                    if (icon != null) { var ic = Ui.El("menu-icon", chip); ic.style.backgroundImage = new StyleBackground(icon); }
                    Ui.Name(ab.Name, chip);
                    InspectCard.Attach(chip, b => InspectCard.Ability(b, ab), tap: true); // GG-T450: chips have no other tap
                }
                Ui.Para(GridGame.Core.Rules.EnemyPlanner.Describe(d.Targeting != Targeting.Auto ? d.Targeting : d.BaseStats.Intelligence >= 14 ? Targeting.Weakest : d.Tags.Contains("Sentient") ? Targeting.Threat : Targeting.Closest), w, "warn");
                Ui.Para(GridGame.Core.Rules.EnemyPlanner.DescribeBrain(d.Brain), w);
            }
            var hint = BestiaryKnowledge.Hint(e);
            if (hint != null) Ui.Para(hint, w, "dim").name = "beast-hint";
        }

        /// <summary>How much of it the Bestiary knows, by the highest tier reached.</summary>
        private static string Known(int tier) =>
            tier >= BestiaryKnowledge.Abilities ? "Everything" : tier >= BestiaryKnowledge.Elements ? "Its weaknesses"
            : tier >= BestiaryKnowledge.Stats ? "Its HP and stats" : tier >= BestiaryKnowledge.Speed ? "Its speed" : "Its name and story";

        /// <summary>One knowledge row: its label, then the value, or "???" while <paramref name="value"/> is still locked (null).</summary>
        private static void Fact(VisualElement w, string name, string label, string value)
        {
            Ui.Field(label, value ?? "???", w, value == null ? "dim" : null, name);
        }
    }

    // ── Settings (§31) ───────────────────────────────────────────────────────────

    public sealed class SettingsScreen : HubPage
    {
        public override string Id => "settings";
        protected override string Title => "Settings";
        /// <summary>The active profile (GG-T99), in the header (GG-T454).</summary>
        protected override string Subtitle => $"Profile: {App.Profile?.Name}";
        protected override string SubtitleName => "settings-profile";

        /// <summary>The difficulty dial (§22.9, GG-T309): − / level / + (each a 132-px target), then one line naming what
        /// the level does and its reward multiplier. Saved at once; the next battle is built at the new level.</summary>
        private void DifficultyControl(SettingsSave st, VisualElement parent)
        {
            st.Difficulty ??= new DifficultySettings();
            int level = DifficultyTable.Clamp(st.Difficulty.Level);
            void Set(int to)
            {
                st.Difficulty.Level = Math.Max(DifficultyTable.Min, Math.Min(DifficultyTable.Max, to));
                App.SaveSettings(); Refresh();
            }
            var row = Ui.El("row", parent);
            row.name = "settings-difficulty";
            row.style.alignItems = Align.Center;
            row.style.minHeight = Kit.Row;
            Ui.Pad(row, left: Kit.Indent); // the label lines up with the menu rows' labels
            row.KindIcon(RowKind.Bounty); // GG-T750 (§26.7): lines up with the setting rows' icons
            Ui.Text("Difficulty", "menu-label grow", row);
            var n = Ui.Stepper(row, level.ToString(), () => Set(level - 1), () => Set(level + 1), out var minus, out var plus, "gold");
            minus.name = "difficulty-down";
            n.name = "difficulty-level";
            plus.name = "difficulty-up";
            Ui.SetEnabled(minus, level > DifficultyTable.Min);
            Ui.SetEnabled(plus, level < DifficultyTable.Max);
            Ui.Note(DifficultyTable.Describe(level) + " Applies from the next battle.", parent).name = "difficulty-text";
        }

        /// <summary>The turn mode (§2.13, GG-T774): the profile's <see cref="SettingsSave.TurnMode"/>, cycled like Battle
        /// speed through <see cref="TurnModes.Available"/> (only modes that are built), then one line explaining it. A save
        /// New Game locked to a mode keeps playing that one, and the line says so; changing the mode here releases that lock
        /// so the save follows Settings again (GG-T1216: switching modes to test them is never blocked).</summary>
        private void TurnModeControl(SettingsSave st, VisualElement parent)
        {
            var c = App.Content;
            var mode = TurnModes.IsBuilt(st.TurnMode) ? st.TurnMode : TurnModes.Available[0];
            var row = Ui.MenuRow("Turn mode", TurnModes.Name(c, mode), () =>
            {
                st.TurnMode = TurnModes.Next(mode);
                App.SaveSettings();
                if (App.Save?.TurnModeLock != null) { TurnModeSetting.Lock(App.Save, null); App.Persist(); }
                Refresh();
            }, parent);
            row.name = "settings-turn-mode";
            row.KindIcon(RowKind.NewGamePlus);
            string text = TurnModes.Describe(c, mode);
            if (App.Save?.TurnModeLock is TurnMode locked)
                text += $" This save is locked to {TurnModes.Name(c, locked)} until you change it here.";
            Ui.Note(text, parent).name = "settings-turn-mode-text";
        }

        /// <summary>A parent passed the gate this visit, so the limit cycles freely until Settings closes.</summary>
        private bool limitUnlocked;

        /// <summary>The monthly spending limit (§25.18.1, GG-T1559): the wallet's limit and this month's spend, cycled through
        /// <see cref="SpendLimits.Choices"/> behind the parental gate unless the wallet answered adult.</summary>
        private void SpendingLimitRow(AccountWallet wallet, VisualElement parent)
        {
            void Cycle()
            {
                wallet.MonthlyLimitCents = SpendLimits.Next(wallet.MonthlyLimitCents);
                DiamondExchange.WalletDirty = true;
                App.PersistWallet();
                Refresh();
            }
            string spent = SpendLimits.Money(SpendLimits.SpentCents(wallet, DateTime.UtcNow));
            var row = Ui.MenuRow("Spending limit", $"{SpendLimits.Describe(wallet)} · {spent} this month", () =>
            {
                if (limitUnlocked || !SpendLimits.LimitGated(wallet)) Cycle();
                else SpendGate.Parental(() => { limitUnlocked = true; Cycle(); });
            }, parent);
            row.name = "settings-spending-limit";
            row.KindIcon(RowKind.Shop);
        }

        /// <summary>Delete all game data (§15, GG-T1580): two questions, then <see cref="GameApp.DeleteAllData"/> erases this
        /// device's profiles, slots, settings and wallet, and the cloud copies, and the app starts over at the title.</summary>
        private void DeleteAllDataRow(VisualElement parent)
        {
            var row = Ui.MenuRow("Delete all game data", "Erase", () =>
                Confirm.Show("Delete all game data? Every profile, save slot and setting on this device is erased, and so are their cloud copies. This can't be undone.", "Continue", () =>
                    Confirm.Show($"Purchased {ShardText.Plural}, owned products and cosmetics are lost too, and are not refunded. Delete everything?", "Delete all", () =>
                        App.DeleteAllData(ok => App.Toast(ok ? "All game data deleted" : "Some game data couldn't be deleted")))), parent);
            row.name = "settings-delete-all-data";
            row.KindIcon(RowKind.Close);
        }

        /// <summary>Party levels the Developer section cycles through.</summary>
        private static readonly int[] DevLevels = { 1, 5, 10, 20, 30, 40, 50 };

        /// <summary>Settings &gt; Developer (GG-T1938): shown only once the version line was tapped seven times on this device.
        /// The Cheats switch adds the pause menu's cheat rows and the DEV badge; with it on, the save cheats below run the
        /// dev automation's own code paths (<see cref="Dev.DevCheats"/>) on the loaded save.</summary>
        private void DeveloperSection(VisualElement w, Action<string> section)
        {
            section("Developer");
            bool cheats = Dev.DevOptions.Cheats;
            var toggle = Ui.MenuRow("Cheats", cheats ? "ON" : "OFF", () => { Dev.DevOptions.SetCheats(!cheats); Refresh(); }, w);
            toggle.name = "settings-dev-cheats";
            toggle.KindIcon(RowKind.Settings);
            if (!cheats) { Ui.Note("Turn Cheats on for the pause menu's Kill all enemies, Win battle, Full heal, Fill mana and speeds up to ×8.", w); return; }
            var s = App.Save;
            if (s == null) { Ui.Note("Load a save to give gold, items, stages and levels.", w); return; }
            var c = App.Content;
            void Cheat(string label, string value, string name, Action act, string done)
            {
                var row = Ui.MenuRow(label, value, () => { act(); App.Persist(); App.Toast(done); Refresh(); }, w);
                row.name = name;
                row.KindIcon(RowKind.Settings);
            }
            Cheat("Give gold", "+10,000", "settings-dev-gold", () => s.Gold += 10000, "+10,000 gold");
            Cheat("Give items", "10 of each consumable", "settings-dev-items", () =>
            {
                foreach (var item in c.Items.Values.Where(i => i.Type == ItemType.Consumable)) Dev.DevCheats.GiveItem(s, item.Id, 10);
            }, "Consumables added");
            Cheat("Unlock all stages", $"{s.HighestClearedStage + 1}/{c.CampaignStages().Count} cleared", "settings-dev-stages",
                () => Dev.DevCheats.UnlockAllStages(c, s), "Every stage unlocked");
            Cheat("Unlock all stations", "", "settings-dev-stations", () => Dev.DevCheats.UnlockAllStations(s), "Every station unlocked");
            int level = s.Roster.Count > 0 ? s.Roster.Max(Progression.Level) : 1;
            int next = DevLevels.FirstOrDefault(l => l > level);
            if (next == 0) next = DevLevels[0];
            Cheat("Party level", $"{level} → {next}", "settings-dev-level", () => Dev.DevCheats.SetLevel(s.Roster, next), $"Party level {next}");
        }

        protected override void BuildBody(VisualElement body)
        {
            // The active profile's settings (GG-T99, §15.4): one set for every save slot, kept in its settings.json.
            var st = App.Settings;
            // GG-T454: one scrolling list (the rows, then How to play), so the last rows are never cut off a short phone.
            // GG-T1223: grouped into Gameplay, Graphics, Display, Sound, Accessibility and Data sections.
            var w = Ui.ListPanel(body, null, "settings-list");
            // GG-T750 (§26.7): every setting row leads with its own kind icon (GG-T1886), the save rows with the Save disk.
            VisualElement Row(RowKind kind, string label, string value, Action set, string name = null)
            {
                Action apply = () => { set(); App.ApplySettings(); App.SaveSettings(); Refresh(); };
                var row = Ui.MenuRow(label, value, apply, w).KindIcon(kind);
                if (name != null) row.name = name;
                return row;
            }
            void Toggle(RowKind kind, string label, bool value, Action<bool> set, string name = null) => Row(kind, label, value ? "ON" : "OFF", () => set(!value), name);
            void Cycle(RowKind kind, string label, float value, float[] opts, string fmt, Action<float> set, string name = null)
            {
                int i = Array.FindIndex(opts, o => Mathf.Abs(o - value) < 0.01f);
                Row(kind, label, string.Format(System.Globalization.CultureInfo.InvariantCulture, fmt, value), () => set(opts[(i + 1) % opts.Length]), name);
            }
            void CycleInt(RowKind kind, string label, int value, int[] opts, Func<int, string> text, Action<int> set, string name)
            {
                int i = Array.IndexOf(opts, value);
                Row(kind, label, text(value), () => set(opts[(i + 1) % opts.Length]), name);
            }
            void Section(string title) => Ui.Section(title, w).name = "settings-section-" + title.ToLowerInvariant();
            void CycleFont()
            {
                var names = Fonts.Presets.Select(p => p.Name).ToArray();
                int i = Array.IndexOf(names, st.FontName);
                st.FontName = names[(i + 1) % names.Length];
                App.ApplySettings(); App.SaveSettings(); Refresh();
            }
            // GG-T1222: Window mode and Resolution only apply to a desktop player, Thumb margin only to a phone.
            bool mobile = Application.isMobilePlatform;
            bool desktop = !mobile && Application.platform != RuntimePlatform.WebGLPlayer;

            Section("Gameplay");
            TurnModeControl(st, w); // GG-T774 (§2.13); first row so the modes can be picked from the title screen (GG-T1216)
            DifficultyControl(st, w); // GG-T309: the 1..10 dial
            Cycle(RowKind.Speed, "Battle speed", st.BattleSpeed, new[] { 0.5f, 1f, 1.5f, 2f, 3f }, "×{0:0.#}", v => st.BattleSpeed = v);
            Toggle(RowKind.Timer, "Drag timer (4 s)", st.DragTimer, v => st.DragTimer = v);
            Toggle(RowKind.Confirm, "Confirm before acting", st.ConfirmActions, v => st.ConfirmActions = v);
            Toggle(RowKind.Focus, "Pincer close-ups", st.PincerCutIns, v => st.PincerCutIns = v);

            // GG-T1221: quality (-1 = the project's own), frame-rate cap, vsync, shake and effects amount.
            Section("Graphics");
            var qualities = QualitySettings.names;
            CycleInt(RowKind.Diamond, "Quality", st.QualityLevel >= 0 && st.QualityLevel < qualities.Length ? st.QualityLevel : -1,
                Enumerable.Range(-1, qualities.Length + 1).ToArray(), q => q < 0 ? "Default" : qualities[q], v => st.QualityLevel = v, "settings-quality");
            CycleInt(RowKind.Gauge, "Frame rate cap", GameSettings.ClampFrameRateCap(st.FrameRateCap), new[] { 30, 60, -1 },
                f => f < 0 ? "Uncapped" : $"{f} fps", v => st.FrameRateCap = v, "settings-frame-cap");
            if (!mobile) Toggle(RowKind.Sync, "VSync", st.VSync, v => st.VSync = v, "settings-vsync"); // GG-T1240: mobile ignores vsync
            Cycle(RowKind.Shake, "Screen shake", st.ScreenShake, new[] { 0f, 0.5f, 1f }, "{0:0%}", v => st.ScreenShake = v, "settings-screen-shake");
            Cycle(RowKind.New, "Effects", st.VfxAmount, new[] { 0f, 0.5f, 1f }, "{0:0%}", v => st.VfxAmount = v, "settings-effects");

            Section("Display");
            if (desktop)
            {
                CycleInt(RowKind.Window, "Window mode", GameSettings.ClampFullScreen(st.FullScreen), new[] { -1, 0, 1 },
                    m => m < 0 ? "As launched" : m == 1 ? "Fullscreen" : "Windowed", v => st.FullScreen = v, "settings-window-mode");
                var sizes = new List<(int w, int h)> { (0, 0) };
                foreach (var r in UnityEngine.Screen.resolutions)
                    if (!sizes.Contains((r.width, r.height))) sizes.Add((r.width, r.height));
                // GG-T1238: a saved size missing from this monitor's list (other monitor, hand edit) is still applied by
                // ApplyDisplay, so list it rather than show "As launched" for it.
                var saved = (st.ResolutionWidth, st.ResolutionHeight);
                if (saved.Item1 > 0 && saved.Item2 > 0 && !sizes.Contains(saved)) sizes.Add(saved);
                int at = Math.Max(0, sizes.IndexOf(saved));
                Row(RowKind.Frame, "Resolution", at == 0 ? "As launched" : $"{sizes[at].w}×{sizes[at].h}", () =>
                {
                    var next = sizes[(at + 1) % sizes.Count];
                    st.ResolutionWidth = next.w; st.ResolutionHeight = next.h;
                }, "settings-resolution");
            }
            if (mobile)
                CycleInt(RowKind.Hand, "Thumb margin", GameSettings.ClampThumbMargin(st.ThumbMargin), new[] { 0, 24, 48, 72, GameSettings.MaxThumbMargin },
                    m => m == 0 ? "OFF" : $"{m} px", v => st.ThumbMargin = v, "settings-thumb-margin");
            CycleInt(RowKind.Scale, "UI scale", GameSettings.ClampUiScale(st.UiScale), new[] { 80, 90, 100 }, s => $"{s}%", v => st.UiScale = v, "settings-ui-scale");

            Section("Sound");
            Cycle(RowKind.Speaker, "Master volume", st.MasterVolume, new[] { 0f, 0.2f, 0.4f, 0.6f, 0.8f, 1f }, "{0:0%}", v => st.MasterVolume = v, "settings-master-volume");
            Cycle(RowKind.Note, "Music volume", st.MusicVolume, new[] { 0f, 0.2f, 0.4f, 0.6f, 0.8f, 1f }, "{0:0%}", v => st.MusicVolume = v);
            Cycle(RowKind.Wave, "Sound volume", st.SfxVolume, new[] { 0f, 0.25f, 0.5f, 0.7f, 0.85f, 1f }, "{0:0%}", v => st.SfxVolume = v);
            Toggle(RowKind.NoteOff, "Mute music", st.MuteMusic, v => st.MuteMusic = v);
            Toggle(RowKind.SpeakerOff, "Mute sound", st.MuteSfx, v => st.MuteSfx = v);
            Toggle(RowKind.WindowOff, "Mute when unfocused", st.MuteWhenUnfocused, v => st.MuteWhenUnfocused = v, "settings-mute-unfocused");

            Section("Accessibility");
            // GG-T495: text size, applied live to every screen (TextSize classes on the UI root).
            Row(RowKind.Text, "Text size", $"{TextSize.Clamp(st.TextScale)}%", () => st.TextScale = TextSize.Next(st.TextScale), "settings-text-size");
            Toggle(RowKind.Eye, "Colorblind palette", st.ColorblindMode, v => st.ColorblindMode = v);
            Toggle(RowKind.Pause, "Reduce motion", st.ReduceMotion, v => st.ReduceMotion = v);
            Toggle(RowKind.Timer, "Untimed turns", st.UntimedTurns, v => st.UntimedTurns = v, "settings-untimed-turns"); // GG-T1978 (§2.0.1)
            // GG-T392: the look presets are developer dials, hidden while Features.LookPresets is off (GameApp.ApplySettings
            // then draws with the default theme, font and word wrap).
            if (Features.LookPresets)
            {
                // GG-T407: every window theme (presets, the player's own, and the editor for gradient, texture and frame).
                var windows = Ui.MenuRow("Windows", Art.ThemeName, () => App.Router.Push(new WindowEditorScreen()), w);
                windows.name = "settings-windows";
                windows.KindIcon(RowKind.Window);
                Ui.MenuRow("Font", st.FontName, CycleFont, w).KindIcon(RowKind.Type);
                // GG-T305: how wrapped text breaks, applied live to every screen (TextFit).
                Row(RowKind.Lore, "Text wrapping", st.Hyphenate ? "Hyphenate" : "Word wrap", () => st.Hyphenate = !st.Hyphenate, "settings-text-wrap");
            }

            Section("Data");
            // GG-T607 (§15.2): move a slot to another device as a copyable code, no cloud account needed.
            var transfer = Ui.MenuRow("Transfer save", "Copy code", () => App.Router.Push(new SaveExportScreen(App.Slot >= 0 ? App.Slot : Math.Max(0, App.Saves?.Root == null ? 0 : SaveFiles.MostRecent(App.Saves.Root)))), w);
            transfer.name = "settings-transfer-save";
            transfer.KindIcon(RowKind.Save);
            var importRow = Ui.MenuRow("Import save", "Paste code", () => App.Router.Push(new SaveImportScreen()), w);
            importRow.name = "settings-import-save";
            importRow.KindIcon(RowKind.Save);
            if (Features.DiamondStore && DiamondExchange.Wallet != null) SpendingLimitRow(DiamondExchange.Wallet, w);
            DeleteAllDataRow(w);
            if (Dev.DevOptions.Unlocked) DeveloperSection(w, Section);
            // GG-T1330: version, legal links and third-party notices.
            var about = Ui.MenuRow("About", "", () => App.Router.Push(new AboutScreen()), w);
            about.name = "settings-about";
            about.KindIcon(RowKind.Codex);
            var version = Ui.Note("Version " + Legal.VersionLine(), w);
            version.name = "settings-version";
            Dev.DevOptions.CountTaps(version); // GG-T1938: seven taps unlock the Developer section
            var help = Ui.El("col", w);
            help.name = "settings-help";
            Ui.Section("How to play", help);
            var t = Ui.Para(
                "• Drag a hero tile by tile. Anyone in the way swaps into the tile you left.\n" +
                "• Trap enemies in a straight line between two heroes to PINCER them. Allies next to the attackers join in.\n" +
                "• Moving never hurts. Only pincers and spells deal damage.\n" +
                "• Time moves only when you act. Icons flow left; whoever reaches the left edge acts, and speed decides who gets there first.\n" +
                "• Pincer an enemy to push its icon back on the timeline.\n" +
                "• Every spell has its own mana bar. Pincers and every other mana gain fill every spell's bar, cheap spells faster. Casting empties only that spell's bar. Skills and items cost a turn.\n" +
                "• Tap a hero, a slot, a target, then Cast. Spells charge on the timeline before they land.\n" +
                "• Roles need each other: Tanks Taunt so enemies leave your casters alone, Healers mend (WIS), Mages hit hardest with spells (INT), Strikers swing Cleave and War Cry (STR).\n" +
                "• Fallen enemies sometimes drop a pickup. Walk a hero over it before it fades.\n" +
                "• GUARD skips to the next enemy turn, shields everyone and banks the unused time as mana.", help);
            if (S != null)
            {
                // GG-T539: one button brings back every lesson: the first battle's (on The Tithe Moors I), each battle system's
                // the next time it comes up, and every Camp and vendor lesson. It also turns lessons back on after Skip tutorial.
                var replay = RowIcon.IconButton("Replay tutorials", RowKind.NewGamePlus, () => { Coach.ResetAll(S); Done(Result.Success("Every lesson will show again")); }, "btn--small", Ui.Actions(help));
                replay.name = "replay-camp-tips";
                // GG-T1648: practice a drill from Settings; not from the pause menu, which opens Settings mid-battle.
                if (!App.Router.Stack.Any(x => x is BattleScreen))
                    RowIcon.IconButton("Play a drill", RowKind.Stage, () => App.Router.Push(new DrillPickerScreen()), "btn--small", replay.parent).name = "settings-play-drill";
            }
        }
    }

    // ── Aetherist: refine Dust into Shards (§25.18.1, GG-T570/624) ──────────────────

    /// <summary>The Aetherist's counter (GG-T624): buy Aetherite Dust with gold, refine it into Aetherite Shards. The
    /// only station whose two actions are both currency exchanges — no recipes, no gear.</summary>
    public sealed class AetheristScreen : HubPage
    {
        public override string Id => "aetherist";
        protected override string Title => "Manasmith";
        protected override string Backdrop => "desert";
        protected override string Npc => CampNpcs.Aetherist;

        private int buyAmount = 10;
        private int refineAmount = 1;

        protected override void BuildBody(VisualElement body)
        {
            int dust = DustEconomy.Have(S);
            int shards = DiamondExchange.Have(S);
            int maxBuy = DustEconomy.MaxBuy(C, S);
            int maxRefine = DustEconomy.MaxRefine(C, S);
            buyAmount = Math.Max(1, buyAmount);
            refineAmount = Math.Max(1, refineAmount);

            var bal = Ui.Panel(body, null, false, "aetherist-balance");
            Ui.Field(DustEconomy.DustName, dust.ToString("N0"), bal, null, "aetherist-dust-count");
            Ui.Field(ShardText.Plural, ShardText.ShortOf(shards), bal, null, "aetherist-shard-count");
            var greet = ShopBark(C, "aetherist/greet");
            if (greet != null) Ui.Para(greet, bal, "dim");

            var buy = Ui.Panel(body, "Buy Dust with gold", false, "aetherist-buy-panel");
            var pick = Ui.Actions(buy);
            pick.style.alignItems = Align.Center;
            var buyCount = Ui.Stepper(pick, buyAmount.ToString("N0"), () => { buyAmount = Math.Max(1, buyAmount - 1); Refresh(); }, () => { buyAmount += 1; Refresh(); }, out var buyMinus, out var buyPlus, "gold");
            buyMinus.name = "aetherist-buy-minus";
            buyCount.name = "aetherist-buy-amount";
            buyPlus.name = "aetherist-buy-plus";
            Ui.SetEnabled(buyMinus, buyAmount > 1);
            var maxBuyBtn = RowIcon.IconButton("Max", RowKind.Station, () => { buyAmount = Math.Max(1, maxBuy); Refresh(); }, "btn--small", pick);
            maxBuyBtn.name = "aetherist-buy-max";
            Ui.SetEnabled(maxBuyBtn, maxBuy > 0);
            Ui.Field("Costs", Ui.Gold(DustEconomy.GoldFor(buyAmount)), buy, S.Gold >= DustEconomy.GoldFor(buyAmount) ? null : "bad", "aetherist-buy-quote");
            var buyBtn = RowIcon.IconButton($"Buy {buyAmount:N0} {DustEconomy.DustName}", RowKind.Confirm, () => Done(DustEconomy.Buy(C, S, buyAmount), true, "ui.buy"), "btn--primary", buy);
            buyBtn.name = "aetherist-buy";
            Ui.SetEnabled(buyBtn, buyAmount <= maxBuy);

            var refine = Ui.Panel(body, "Refine into " + ShardText.Plural, false, "aetherist-refine-panel");
            var rpick = Ui.Actions(refine);
            rpick.style.alignItems = Align.Center;
            var refineCount = Ui.Stepper(rpick, refineAmount.ToString("N0"), () => { refineAmount = Math.Max(1, refineAmount - 1); Refresh(); }, () => { refineAmount += 1; Refresh(); }, out var refineMinus, out var refinePlus, "gold");
            refineMinus.name = "aetherist-refine-minus";
            refineCount.name = "aetherist-refine-amount";
            refinePlus.name = "aetherist-refine-plus";
            Ui.SetEnabled(refineMinus, refineAmount > 1);
            var maxRefineBtn = RowIcon.IconButton("Max", RowKind.Station, () => { refineAmount = Math.Max(1, maxRefine); Refresh(); }, "btn--small", rpick);
            maxRefineBtn.name = "aetherist-refine-max";
            Ui.SetEnabled(maxRefineBtn, maxRefine > 0);
            Ui.Field("Needs", $"{refineAmount * DustEconomy.DustPerShard:N0} {DustEconomy.DustName}", refine,
                dust >= refineAmount * DustEconomy.DustPerShard ? null : "bad", "aetherist-refine-quote");
            var refineBtn = RowIcon.IconButton($"Refine {ShardText.ShortOf(refineAmount)}", RowKind.Confirm, () =>
            {
                string occasion = dust < maxRefine * DustEconomy.DustPerShard ? "aetherist/short" : "aetherist/refine";
                var r = DustEconomy.Refine(C, S, refineAmount);
                var bark = r.Ok ? ShopBark(C, occasion) : null;
                Done(r, true, "ui.diamond");
                if (bark != null) App.Toast(bark, 2.6f);
            }, "btn--primary", refine);
            refineBtn.name = "aetherist-refine";
            Ui.SetEnabled(refineBtn, refineAmount <= maxRefine);


            Ui.Note($"{DustEconomy.DustPerShard} {DustEconomy.DustName} refines into 1 {ShardText.Short}. {DustEconomy.DustName} costs {DustEconomy.GoldPerDust} G a piece here — " +
                $"buying {ShardText.Plural} outright at the {ShardText.Store} is the better rate; Dust is the free-play path, earned in battle.", body);
        }

        /// <summary>A random line for this shop occasion, or null with nothing authored for it yet.</summary>
        private static string ShopBark(ContentDb c, string key) =>
            c != null && c.ShopBarks.TryGetValue(key, out var lines) && lines.Count > 0 ? lines[UnityEngine.Random.Range(0, lines.Count)] : null;
    }
}
