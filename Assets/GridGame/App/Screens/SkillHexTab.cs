using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Content;
using GridGame.Core.Rules;
using GridGame.Meta;
using UnityEngine.UIElements;

namespace GridGame.App
{
    // ─────────────────────────────────────────────────────────────────────────────
    // Board: the hero's skill hexagon (GG-T1056)
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The Abilities tab's Board sub-tab (in place of the old Talents skill trees): the skill hexagon in a
    /// <see cref="SkillHexView"/>, the hero's owned nodes drawn as its trail (<see cref="HeroBoard.Owned"/>: its saved
    /// <see cref="HeroSave.HexNodes"/>, or its class Start path before it has joined). Tapping a node previews
    /// <see cref="SkillHexBoard.CheapestRoute"/> to it (hovering draws the route only); the panel under the board explains
    /// the node and shows that route's SP, the stats its passives add, the abilities it teaches and the hero's SP. Buy
    /// route pays with <see cref="HeroSave.SkillPoints"/> through <see cref="SkillHexProgress.BuyRoute"/> (joining the board first, free, when the hero has not yet).
    /// Keeps the id "talents" so `-start talents` and the harness's goto still land here.
    /// </summary>
    internal sealed class SkillHexTab : HeroTab
    {
        /// <summary>The board's floor in design px: on the shortest stages the detail window gives way first, so the hexagon
        /// stays big enough to tap (GG-T1113).</summary>
        private const float BoardMinHeight = 280f;
        /// <summary>The detail window's floor in design px: room for the node's name and the pinned Buy route row (GG-T1113).</summary>
        private const float DetailMinHeight = 150f;
        private readonly PanZoomState zoom = new PanZoomState();
        private (int Q, int R)? selected;

        public override string Id => "talents";
        public override void OnHeroChanged() => selected = null;
        /// <summary>Opens with this node tapped, its route previewed and Buy route ready (GG-T1404: a Learn button's next ability).</summary>
        public void Select((int Q, int R)? pt) => selected = pt;

        public override void BuildBody(VisualElement body)
        {
            var h = Hero;
            var board = HeroBoard.Board(C);
            if (board == null || h == null)
            {
                Ui.Empty("There are no Talents.", Ui.Panel(body, null, true));
                return;
            }
            var owned = HeroBoard.Owned(C, h).Nodes;
            var start = board.StartOf(h.ClassId);

            var head = Ui.Panel(body, null, false, "hex-head");
            var top = Ui.El("row", head);
            top.style.alignItems = Align.Center;
            var points = Ui.Heading($"{h.SkillPoints} skill point{(h.SkillPoints == 1 ? "" : "s")}", top, h.SkillPoints > 0 ? "gold" : "dim");
            points.name = "hex-points";
            points.style.flexGrow = 1; points.style.flexShrink = 1; points.style.minWidth = 0;
            // GG-T2085: the Inn's Respec opens from here, off the hub's mode banner, while the caravan is camped.
            if (CampMenu.Build(C, S, App.Settings).Any(d => d.Id == CampMenu.Respec))
            {
                var respec = RowIcon.IconButton("Respec", RowKind.Station, () => App.Router.Push(new RespecScreen()), "btn--small", top, false, Art.UiIcon("hub-respec"));
                respec.name = "hex-respec";
                respec.style.marginLeft = Space.S16;
            }
            var have = StatLine(board.StatTotals(owned));
            int bought = SkillHexProgress.Refundable(C, h).Count;
            Ui.Caption($"Owns {owned.Count} node{(owned.Count == 1 ? "" : "s")} ({bought} bought){(have.Length > 0 ? " · " + have : "")} · earn 1 a level", head, "dim");

            var frame = Ui.Panel(body, null, true, "hex-board");
            var host = SkillHexView.Host(board, zoom, out var view);
            // GG-T1068: the board's own dark fills the whole viewport, not just the fitted square in the middle of it.
            host.style.backgroundColor = SkillHexView.Bg;
            // GG-T1875: the board takes the column's free height and the whole hexagon fits it at rest.
            host.FitAll = true;
            frame.Add(host);
            frame.style.minHeight = BoardMinHeight;
            view.SetOwned(start, owned);
            view.SetSelected(selected);

            var detail = Ui.DetailPanel(body, "hex-detail");
            // GG-T1875: the window is as tall as what it shows, so the board keeps the rest; only a tap changes it (hover
            // draws the route alone), so the board never resizes under the pointer (GG-T1068).
            var detailWindow = body.Q("hex-detail");
            // GG-T1113: on short stages the window shrinks (its USS cap is 45% of the column) down to a floor that still
            // shows the pinned Buy route row; the route, stats and learns lines scroll inside it instead of overflowing.
            detailWindow.style.minHeight = DetailMinHeight;
            detailWindow.style.overflow = Overflow.Hidden;
            var scroll = detailWindow.Q<ScrollView>();
            if (scroll != null) { scroll.style.flexGrow = 1; scroll.style.flexShrink = 1; scroll.style.minHeight = 0; }
            // The Buy route row sits under the scroll, outside it, so a long route never scrolls it off the stage.
            var foot = Ui.El("kit-footer", detailWindow);
            foot.name = "hex-foot";
            foot.style.flexShrink = 0;
            void Show((int Q, int R)? pt)
            {
                detail.Clear();
                foot.Clear();
                var route = pt.HasValue ? board.CheapestRoute(owned, pt.Value) : null;
                view.SetPreview(route);
                Detail(board, h, owned, pt, route, detail, foot);
                foot.style.display = foot.childCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }
            view.HoverChanged += pt =>
            {
                var at = pt ?? selected;
                view.SetPreview(at.HasValue ? board.CheapestRoute(owned, at.Value) : null);
            };
            // GG-T1971: select, then confirm. A tap selects the node (pressed in, its route and Buy route shown); Buy route
            // is the confirm, so it takes one tap rather than a second select of its own.
            view.Tapped += pt =>
            {
                if (!pt.Equals(selected)) GameApp.I?.Audio?.Sfx("ui.select");
                selected = pt;
                view.SetSelected(pt);
                Show(pt);
            };
            Show(selected);
        }

        /// <summary>What the shown node is, the route to it (SP, stats, abilities) and, for the tapped node, Buy route.</summary>
        private void Detail(SkillHexBoard board, HeroSave h, IReadOnlyCollection<(int Q, int R)> owned, (int Q, int R)? pt,
            List<(int Q, int R)> route, VisualElement w, VisualElement foot)
        {
            if (!pt.HasValue)
            {
                Ui.Para("Tap a node to see the cheapest route to it, then tap Buy route.", w, "dim");
                return;
            }
            var node = board.Node(pt.Value);
            bool isAbility = node != null && node.Kind == SkillHexNodeKind.Ability && C.TryAbility(node.Ability) != null;
            // GG-T1803: an ability node explains the whole skill with the battle's own card; the card carries the name.
            if (isAbility) InspectCard.Ability(w, C.TryAbility(node.Ability), null, C.TryClass(h.ClassId));
            else Ui.Heading(NodeName(node), w).style.whiteSpace = WhiteSpace.Normal;
            if (node != null && node.Kind == SkillHexNodeKind.Passive) Explain(node, w);
            else if (node != null && node.Kind == SkillHexNodeKind.Open) Ui.Para("An open slot: it adds nothing, but the trail runs through it to the nodes beyond.", w, "dim");
            if (route == null) { if (node == null || node.Kind != SkillHexNodeKind.Open) Ui.Para("Can't reach that.", w, "dim"); return; }
            if (route.Count == 0) { Ui.Para("Owned", w, "good").name = "hex-owned"; return; }

            int cost = board.SpCost(route, owned);
            Ui.Para($"Route: {route.Count} node{(route.Count == 1 ? "" : "s")} · {cost} SP (you have {h.SkillPoints})", w, h.SkillPoints >= cost ? null : "bad").name = "hex-route-cost";
            var gains = StatLine(board.StatTotals(route));
            if (gains.Length > 0) Ui.Para("Stats: " + gains, w, "gold").name = "hex-route-stats";
            var abilities = board.OwnedAbilities(route).Select(id => C.TryAbility(id)?.Name ?? id).ToList();
            if (abilities.Count > 0) Ui.Para("Learns: " + string.Join(", ", abilities), w, "good").name = "hex-route-abilities";

            bool isSelected = pt.Equals(selected);
            Ui.El("grow", foot);
            var target = pt.Value;
            var buy = RowIcon.IconButton($"Buy route · {cost} SP", RowKind.Confirm, () =>
            {
                if (h.HexNodes == null || h.HexNodes.Count == 0)
                {
                    var j = SkillHexProgress.Join(C, h);
                    if (!j.Ok) { Done(j); return; }
                }
                Done(SkillHexProgress.BuyRoute(C, h, target));
            }, "btn--primary btn--small", foot, confirmTap: false);
            buy.name = "hex-buy";
            Ui.SetEnabled(buy, isSelected && h.SkillPoints >= cost);
        }

        /// <summary>A passive node's stats, one per line with what each does (GG-T1803).</summary>
        private void Explain(SkillHexNodeDef n, VisualElement w)
        {
            Ui.Para("A passive: it permanently raises these stats while owned.", w, "dim");
            foreach (var kv in (n.Stats ?? new Dictionary<string, int>()).OrderBy(kv => kv.Key))
            {
                var term = C.TryTerm(kv.Key);
                string val = $"{(kv.Value >= 0 ? "+" : "")}{kv.Value}";
                var t = GlossaryText.Create(term != null ? $"[{kv.Key}] {val} — {term.Text}" : $"{kv.Key} {val}", "kit-text", w);
                t.name = "hex-stat-line";
            }
        }

        private string NodeName(SkillHexNodeDef n)
        {
            if (n == null) return "Empty";
            switch (n.Kind)
            {
                case SkillHexNodeKind.Ability: return C.TryAbility(n.Ability)?.Name ?? n.Ability;
                case SkillHexNodeKind.Passive: return "Passive · " + StatLine(n.Stats ?? new Dictionary<string, int>());
                default: return "Open slot";
            }
        }

        private static string StatLine(Dictionary<string, int> totals) =>
            string.Join(" · ", totals.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key} {(kv.Value >= 0 ? "+" : "")}{kv.Value}"));
    }
}
