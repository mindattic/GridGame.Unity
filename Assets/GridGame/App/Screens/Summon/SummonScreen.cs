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
    public sealed class SummonScreen : HubPage
    {
        public override string Id => "summon";
        protected override string Title => "Recruit";
        protected override string Backdrop => "cityruins";

        /// <summary>The open tab: one per open banner (<see cref="Gacha.Open"/>), then Recruit.</summary>
        private int tab;

        /// <summary>The open Rates list's height in design px: about ten rows, the rest scrolls.</summary>
        private const float OddsListMaxHeight = 560;

        /// <summary>The Recruit tab's Summon | Mercenaries switch (GG-T1964), atop both pages; a page not open yet is
        /// locked with its unlock hint.</summary>
        internal static void RecruitTabs(VisualElement body, int active)
        {
            var app = GameApp.I;
            Func<int, string> hint = i => CampUnlocks.MilestoneHint(app.Content, i == 0 ? CampUnlocks.SummonAfter : CampUnlocks.BountiesAfter);
            Ui.Tabs(new[] { "Summon", "Mercenaries" }, active, i => { if (i != active) app.Router.Replace(i == 0 ? new SummonScreen() : (GameScreen)new MercenaryScreen()); }, body,
                i => i == 0 ? !CampUnlocks.SummonOpen(app.Save) : !Mercenaries.Open(app.Save), i => app.Toast(hint(i))).name = "recruit-tabs";
        }

        protected override void BuildBody(VisualElement body)
        {
            RecruitTabs(body, 0);
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

        /// <summary>A banner's tab (§25.10.3, GG-T1428): the featured portraits and the countdown to its close, the
        /// Rates list, the wallet, and the x1 and x<see cref="BannerDef.MultiCount"/> pulls naming what they take.</summary>
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
            RatesRow(w, b);

            int diamonds = DiamondExchange.Have(S), tickets = SummonTickets.Have(S);
            var wallet = Ui.Actions(w);
            var img = wallet.KindIcon(RowKind.Diamond, Art.ItemIcon(Crafting.DiamondId)); // GG-T745 (§26.7)
            img.style.width = img.style.height = Size.IconS;
            img.style.marginLeft = Space.TargetHalfGap;
            img.style.marginRight = Space.S0;
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

        /// <summary>The next pull's odds in a line: the featured share, or the even chance of each hero.</summary>
        private string RatesLine(BannerDef b, List<DrawOdds> odds)
        {
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
}
