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
    /// <see cref="HeroSave.HexNodes"/>, or its class Start path before it has joined). Hovering or tapping a node previews
    /// <see cref="SkillHexBoard.CheapestRoute"/> to it; the panel under the board shows that route's SP, the stats its
    /// passives add, the abilities it teaches and the hero's SP. Buy route pays with <see cref="HeroSave.SkillPoints"/>
    /// through <see cref="SkillHexProgress.BuyRoute"/> (joining the board first, free, when the hero has not yet).
    /// Keeps the id "talents" so `-start talents` and the harness's goto still land here.
    /// </summary>
    internal sealed class SkillHexTab : HeroTab
    {
        /// <summary>The detail window's height in design px: a full route preview without scrolling (GG-T1068).</summary>
        private const float DetailHeight = 460f;
        private readonly PanZoomState zoom = new PanZoomState();
        private (int Q, int R)? selected;

        public override string Id => "talents";
        public override void OnHeroChanged() => selected = null;

        public override void BuildBody(VisualElement body)
        {
            var h = Hero;
            var board = HeroBoard.Board(C);
            if (board == null || h == null)
            {
                Ui.Empty("There is no skill hexagon.", Ui.Panel(body, null, true));
                return;
            }
            var owned = HeroBoard.Owned(C, h).Nodes;
            var start = board.StartOf(h.ClassId);

            var head = Ui.Panel(body, null, false, "hex-head");
            Ui.Heading($"{h.SkillPoints} skill point{(h.SkillPoints == 1 ? "" : "s")}", head, h.SkillPoints > 0 ? "gold" : "dim").name = "hex-points";
            var have = StatLine(board.StatTotals(owned));
            Ui.Caption($"Owns {owned.Count} node{(owned.Count == 1 ? "" : "s")}{(have.Length > 0 ? " · " + have : "")} · earn 1 a level", head, "dim");

            var frame = Ui.Panel(body, null, true, "hex-board");
            var host = SkillHexView.Host(board, zoom, out var view);
            // GG-T1068: the board's own dark fills the whole viewport, not just the fitted square in the middle of it.
            host.style.backgroundColor = SkillHexView.Bg;
            frame.Add(host);
            view.SetOwned(start, owned);
            view.SetSelected(selected);

            var detail = Ui.DetailPanel(body, "hex-detail");
            // GG-T1068: one fixed height (a node's name, route, stats, learns and Buy route) so hovering or tapping
            // never resizes the board above it, which re-centred the hexagon under the pointer on every preview.
            body.Q("hex-detail").style.height = DetailHeight;
            void Show((int Q, int R)? pt)
            {
                detail.Clear();
                var route = pt.HasValue ? board.CheapestRoute(owned, pt.Value) : null;
                view.SetPreview(route);
                Detail(board, h, owned, pt, route, detail);
            }
            view.HoverChanged += pt => Show(pt ?? selected);
            view.Tapped += pt => { selected = pt; view.SetSelected(pt); Show(pt); };
            Show(selected);
        }

        /// <summary>What the shown node is, the route to it (SP, stats, abilities) and, for the tapped node, Buy route.</summary>
        private void Detail(SkillHexBoard board, HeroSave h, IReadOnlyCollection<(int Q, int R)> owned, (int Q, int R)? pt,
            List<(int Q, int R)> route, VisualElement w)
        {
            if (!pt.HasValue)
            {
                Ui.Para("Hover or tap a node to see the cheapest route to it.", w, "dim");
                return;
            }
            var node = board.Node(pt.Value);
            Ui.Heading(NodeName(node), w);
            if (route == null) { Ui.Para(node == null || node.Kind == SkillHexNodeKind.Open ? "An open slot: nothing to buy here yet." : "Can't reach that.", w, "dim"); return; }
            if (route.Count == 0) { Ui.Para("Owned.", w, "good"); return; }

            int cost = board.SpCost(route, owned);
            Ui.Para($"Route: {route.Count} node{(route.Count == 1 ? "" : "s")} · {cost} SP (you have {h.SkillPoints})", w, h.SkillPoints >= cost ? null : "bad").name = "hex-route-cost";
            var gains = StatLine(board.StatTotals(route));
            if (gains.Length > 0) Ui.Para("Stats: " + gains, w, "gold").name = "hex-route-stats";
            var abilities = board.OwnedAbilities(route).Select(id => C.TryAbility(id)?.Name ?? id).ToList();
            if (abilities.Count > 0) Ui.Para("Learns: " + string.Join(", ", abilities), w, "good").name = "hex-route-abilities";

            var foot = Ui.El("kit-footer", w);
            bool isSelected = pt.Equals(selected);
            Ui.Para(isSelected ? "" : "Tap it to buy.", foot, "dim grow");
            var target = pt.Value;
            var buy = RowIcon.IconButton($"Buy route · {cost} SP", RowKind.Confirm, () =>
            {
                if (h.HexNodes == null || h.HexNodes.Count == 0)
                {
                    var j = SkillHexProgress.Join(C, h);
                    if (!j.Ok) { Done(j); return; }
                }
                Done(SkillHexProgress.BuyRoute(C, h, target));
            }, "btn--primary btn--small", foot);
            buy.name = "hex-buy";
            Ui.SetEnabled(buy, isSelected && h.SkillPoints >= cost);
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
