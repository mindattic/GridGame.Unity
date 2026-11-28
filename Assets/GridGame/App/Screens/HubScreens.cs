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
            Fact(w, "beast-elements", "Elements", tier >= BestiaryKnowledge.Elements || d.RevealResistances ? BestiaryKnowledge.ElementLine(d) : null);
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
            Ui.Para(Coach.LoopLine, help).name = "settings-loop-line";
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
}
