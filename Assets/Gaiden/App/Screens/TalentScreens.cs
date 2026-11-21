using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Content;
using GridGame.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    // ─────────────────────────────────────────────────────────────────────────────
    // Talents: a hero's skill trees as node graphs (§25.6.3, GG-T53)
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The Talents sub-tab of the Hero screen's Abilities tab (GG-T435; the Talents screen before it): the hero's home tree (and any off-tree it already has a node in) drawn as a node graph — one
    /// row per points-in-tree tier, prerequisite links drawn between cards — with Innate/Purchased/Available/Locked states,
    /// the skill-point counter, a detail panel for the tapped node (its tooltip) and confirm-to-spend.
    /// </summary>
    internal sealed class TalentsTab : HeroTab
    {
        private int tab;
        private string selected;
        private string popNode;
        public override string Id => "talents";
        public override void OnHeroChanged() => selected = null;

        private static readonly Color PurchasedColor = Palette.Gold;
        private static readonly Color InnateColor = Palette.Good;
        private static readonly Color AvailableColor = new Color(0.55f, 0.75f, 1f);
        private static readonly Color LockedColor = new Color(0.45f, 0.48f, 0.62f, 0.8f);

        public static Color StateColor(TalentState s) => s == TalentState.Purchased ? PurchasedColor
            : s == TalentState.Innate ? InnateColor : s == TalentState.Available ? AvailableColor : LockedColor;

        public static string StateLabel(TalentState s) => s == TalentState.Purchased ? "Learned"
            : s == TalentState.Innate ? "Known" : s == TalentState.Available ? "Available" : "Locked";

        public override void BuildBody(VisualElement body)
        {
            var h = Hero;
            var trees = Talents.Trees(C, h);

            // Point counter + respec.
            var head = Ui.Panel(body, null, false, "talent-head");
            var headRow = Ui.El("kit-footer", head);
            var counter = Ui.El("col grow", headRow);
            var pts = Ui.Heading($"{h.SkillPoints} skill point{(h.SkillPoints == 1 ? "" : "s")}", counter, h.SkillPoints > 0 ? "gold" : "dim");
            pts.name = "talent-points";
            int spent = Talents.PointsSpent(C, h);
            Ui.Caption($"Spent {spent} · earn more on level-up", counter, "dim");
            int cost = Talents.RespecCost(h);
            string closed = Talents.RespecClosed(S);
            var respec = Ui.Button($"Respec · {Ui.Gold(cost)}", () => RespecScreen.Ask(C, h,
                $"Pay {Ui.Gold(cost)} at the Inn to unlearn {C.Class(h.ClassId).Name}'s talents and refund {spent} skill point{(spent == 1 ? "" : "s")}?",
                r => Done(r), () => Talents.Respec(C, S, h), () => Talents.Respec(C, S, h, true)), "btn--small", headRow);
            respec.name = "talent-respec";
            Ui.SetEnabled(respec, closed == null && spent > 0 && S.Gold >= cost);
            if (closed != null) Ui.Para(closed, head, "dim");

            if (trees.Count == 0)
            {
                Ui.Empty("This hero has no skill tree.", Ui.Panel(body, null, true));
                return;
            }
            tab = Mathf.Clamp(tab, 0, trees.Count - 1);
            if (trees.Count > 1)
                Ui.Tabs(trees.Select(t => Talents.IsHome(C, h, t) ? t.Name + " (home)" : Talents.Opened(C, h, t) ? t.Name : t.Name + " (new)").ToArray(),
                    tab, i => { tab = i; selected = null; Refresh(); }, body);
            var tree = trees[tab];

            // §25.6.2 (GG-T51): an off-tree says what it costs; an unopened one also says whether a slot is free.
            string offNote = "";
            if (!Talents.IsHome(C, h, tree))
            {
                // GG-T135: the premium grows with distance on the archetype ring.
                int steps = Talents.Distance(C, h, tree);
                string away = steps == 1 ? "neighbour" : steps == TreeAdjacency.Unreachable ? "off the board" : $"{steps} steps away";
                offNote = $" · off-tree ({away}): +{Talents.Premium(C, h, tree)} pt a node, deeper tiers need more";
                if (!Talents.Opened(C, h, tree)) offNote += " · " + (Talents.WhyClosed(C, h, tree) ?? "buying a node opens it");
            }
            var list = Ui.ListPanel(body, $"{tree.Name} — {Talents.PointsIn(h, tree)} pts in tree{offNote}", "talent-tree");
            if (!string.IsNullOrEmpty(tree.Description)) GlossaryText.Create(tree.Description, "kit-text dim", list);
            var cards = Graph(tree, h, list);

            var sel = tree.Nodes.FirstOrDefault(n => n.Id == selected)
                ?? tree.Nodes.FirstOrDefault(n => Talents.State(C, h, tree, n) == TalentState.Available)
                ?? tree.Nodes.FirstOrDefault();
            if (sel != null)
            {
                if (cards.TryGetValue(sel.Id, out var selCard)) selCard.AddToClassList("talent-node--selected");
                Detail(tree, sel, h, body);
            }

            if (popNode != null && cards.TryGetValue(popNode, out var popped))
                App.StartCoroutine(Tween.Pop(popped, 1.12f, 0.35f));
            popNode = null;
        }

        /// <summary>Lays the tree out as rows by <see cref="SkillNodeDef.RequiredPointsInTree"/>, each row ordered by branch
        /// (a node follows its first prerequisite's root), and draws a link under every prerequisite → node pair.</summary>
        private Dictionary<string, VisualElement> Graph(SkillTreeDef tree, HeroSave h, VisualElement parent)
        {
            var graph = Ui.El("col", parent);
            graph.name = "talent-graph";
            var links = new TalentLinks();
            graph.Add(links);

            int RootIndex(SkillNodeDef n)
            {
                var cur = n;
                for (int guard = 0; guard < 32 && cur.Prerequisites.Count > 0; guard++)
                {
                    var p = tree.Nodes.FirstOrDefault(x => x.Id == cur.Prerequisites[0]);
                    if (p == null) break;
                    cur = p;
                }
                return tree.Nodes.IndexOf(cur);
            }

            var cards = new Dictionary<string, VisualElement>();
            foreach (var tier in tree.Nodes.GroupBy(n => n.RequiredPointsInTree).OrderBy(g => g.Key))
            {
                var row = Ui.El("row", graph);
                row.style.alignItems = Align.Center;
                row.style.marginBottom = Space.S32;
                int need = Talents.IsHome(C, h, tree) ? tier.Key : Talents.ScaledRequirement(tier.Key);
                var lbl = Ui.Caption(need == 0 ? "Start" : $"{need} pts", row, "dim");
                lbl.style.width = Size.ColumnL; lbl.style.unityTextAlign = TextAnchor.MiddleCenter;
                var cells = Ui.El("row grow", row);
                cells.style.justifyContent = Justify.SpaceAround; // cards shrink to share a wide tier rather than wrap
                foreach (var n in tier.OrderBy(RootIndex).ThenBy(x => tree.Nodes.IndexOf(x)))
                    cards[n.Id] = Card(tree, n, h, cells);
            }
            foreach (var n in tree.Nodes)
                foreach (var pre in n.Prerequisites)
                    if (cards.TryGetValue(pre, out var from) && cards.TryGetValue(n.Id, out var to))
                        links.Links.Add((from, to, Talents.Unlocked(h, tree, n) ? PurchasedColor : Talents.State(C, h, tree, n) == TalentState.Available ? AvailableColor : LockedColor));
            graph.RegisterCallback<GeometryChangedEvent>(_ => links.MarkDirtyRepaint());
            return cards;
        }

        private VisualElement Card(SkillTreeDef tree, SkillNodeDef n, HeroSave h, VisualElement parent)
        {
            var state = Talents.State(C, h, tree, n);
            var a = C.TryAbility(n.Grants);
            var card = Ui.El("col center talent-node", parent);
            card.name = "talent-" + n.Id;
            card.style.width = Size.Card; card.style.minHeight = Size.Thumb + Space.S32;
            card.style.paddingTop = card.style.paddingBottom = Space.S8;
            card.style.paddingLeft = card.style.paddingRight = Space.S8;
            card.style.borderTopLeftRadius = card.style.borderTopRightRadius = card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = 16;
            bool sel = n.Id == selected;
            float bw = sel ? 7 : state == TalentState.Available ? 5 : 3;
            card.style.borderTopWidth = card.style.borderBottomWidth = card.style.borderLeftWidth = card.style.borderRightWidth = bw;
            var bc = sel ? Color.white : StateColor(state);
            card.style.borderTopColor = card.style.borderBottomColor = card.style.borderLeftColor = card.style.borderRightColor = bc;
            card.style.backgroundColor = state == TalentState.Locked ? Palette.Ink.WithA(0.96f)
                : state == TalentState.Available ? new Color(0.07f, 0.12f, 0.34f, 0.97f) : Palette.Ink.WithA(0.97f);
            var icon = Art.AbilityIcon(a);
            if (icon != null)
            {
                var ic = Ui.El("menu-icon", card);
                ic.style.backgroundImage = new StyleBackground(icon);
                ic.style.marginRight = Space.S0;
                if (state == TalentState.Locked) ic.style.unityBackgroundImageTintColor = new Color(0.5f, 0.5f, 0.6f, 0.7f);
            }
            var name = Ui.Caption(n.Name ?? a?.Name ?? n.Id, card, state == TalentState.Locked ? "dim" : null);
            name.style.whiteSpace = WhiteSpace.Normal; name.style.unityTextAlign = TextAnchor.MiddleCenter;
            int cost = Talents.Cost(C, h, tree, n);
            string tag = state == TalentState.Purchased || state == TalentState.Innate ? "✔ " + StateLabel(state) : $"{cost} pt{(cost == 1 ? "" : "s")}";
            var tagLbl = Ui.Caption(tag, card);
            tagLbl.style.color = StateColor(state);
            card.AddToClassList("card-tap");
            if (a != null) InspectCard.Attach(card, b => InspectCard.Ability(b, a, $"{tag} · {tree.Name}"));
            Ui.Tappable(card, () => { selected = n.Id; Refresh(); }, "ui.select");
            return card;
        }

        /// <summary>The tapped node's tooltip: what it grants, its cost and gate, and the confirm-to-spend button.</summary>
        private void Detail(SkillTreeDef tree, SkillNodeDef n, HeroSave h, VisualElement body)
        {
            var state = Talents.State(C, h, tree, n);
            var a = C.TryAbility(n.Grants);
            var w = Ui.DetailPanel(body, "talent-detail");
            var top = Ui.El("row", w);
            top.style.alignItems = Align.Center;
            var icon = Art.AbilityIcon(a);
            if (icon != null) { var ic = Ui.El("menu-icon", top); ic.style.backgroundImage = new StyleBackground(icon); }
            var title = Ui.El("col grow", top);
            Ui.Heading(n.Name ?? a?.Name ?? n.Id, title);
            string kind = a == null ? "" : a.Kind == AbilityKind.Passive ? "Passive" : a.Kind.ToString();
            Ui.Caption($"{kind}{(a != null && a.Name != n.Name ? " · grants " + a.Name : "")}", title, "dim");
            var st = Ui.Caption(StateLabel(state), top);
            st.style.color = StateColor(state);
            GlossaryText.Create(a?.Description ?? n.Description ?? "", "kit-text dim", w);
            if (!string.IsNullOrEmpty(n.Description) && a?.Description != null && n.Description != a.Description)
                GlossaryText.Create(n.Description, "kit-text dim", w);
            var foot = Ui.El("kit-footer", w);
            int cost = Talents.Cost(C, h, tree, n);
            string pts = $"{cost} skill point{(cost == 1 ? "" : "s")}";
            bool offTree = !Talents.IsHome(C, h, tree);
            string premium = offTree && cost > n.Cost ? $" (off-tree: +{cost - n.Cost})" : "";
            string gate = state == TalentState.Locked ? Talents.WhyLocked(C, h, tree, n)
                : state == TalentState.Innate ? "Already in this hero's kit — no points needed"
                : state == TalentState.Purchased ? "Bought with skill points (Respec at the Inn refunds it)"
                : h.SkillPoints < cost ? $"Needs {pts}{premium} ({h.SkillPoints} now)"
                : $"Costs {pts}{premium}";
            Ui.Para(gate, foot, (state == TalentState.Locked || (state == TalentState.Available && h.SkillPoints < cost) ? "bad" : "dim") + " grow");
            if (state == TalentState.Available)
            {
                var hero = C.Class(h.ClassId).Name;
                bool opens = offTree && !Talents.Opened(C, h, tree);
                var learn = Ui.Button($"Learn · {cost} pt{(cost == 1 ? "" : "s")}", () => Confirm.Show(
                    $"Spend {pts} to teach {hero} {n.Name ?? a?.Name}?" + (opens ? $" This opens {tree.Name} as an off-tree." : ""), "Spend", () =>
                    {
                        var r = Talents.Unlock(C, S, h, tree.Id, n.Id);
                        if (r.Ok) { popNode = n.Id; selected = n.Id; }
                        Done(r);
                    }), "btn--primary btn--small", foot);
                learn.name = "talent-learn";
                Ui.SetEnabled(learn, h.SkillPoints >= cost);
            }
        }
    }

    /// <summary>Prerequisite links behind the talent cards: an elbow from the bottom of each prerequisite to the top of the
    /// node it opens, in the colour of how far along that link the hero is.</summary>
    public sealed class TalentLinks : VisualElement
    {
        public readonly List<(VisualElement from, VisualElement to, Color color)> Links = new List<(VisualElement, VisualElement, Color)>();

        public TalentLinks()
        {
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = style.top = style.right = style.bottom = Space.S0;
            generateVisualContent += Draw;
        }

        private void Draw(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            p.lineCap = LineCap.Round; p.lineJoin = LineJoin.Round;
            foreach (var (from, to, color) in Links)
            {
                var fb = from.worldBound; var tb = to.worldBound;
                if (fb.width <= 0 || tb.width <= 0) continue;
                var a = this.WorldToLocal(new Vector2(fb.center.x, fb.yMax));
                var b = this.WorldToLocal(new Vector2(tb.center.x, tb.yMin));
                float midY = (a.y + b.y) / 2f;
                p.strokeColor = new Color(0f, 0f, 0f, 0.5f); p.lineWidth = 10f;
                Path(p, a, b, midY);
                p.strokeColor = color; p.lineWidth = 5f;
                Path(p, a, b, midY);
            }
        }

        private static void Path(Painter2D p, Vector2 a, Vector2 b, float midY)
        {
            p.BeginPath();
            p.MoveTo(a);
            p.LineTo(new Vector2(a.x, midY));
            p.LineTo(new Vector2(b.x, midY));
            p.LineTo(b);
            p.Stroke();
        }
    }

    /// <summary>The Inn's Respec (§25.6.3, GG-T53): every hero with talent points spent, its price (scaling with level),
    /// and a confirm that refunds all of that hero's points.</summary>
    public sealed class RespecScreen : HubPage
    {
        public override string Id => "respec";
        protected override string Title => "Inn — Respec";
        protected override string Npc => CampNpcs.Innkeeper;

        protected override void BuildBody(VisualElement body)
        {
            var list = Ui.ListPanel(body, "Heroes", "respec-list");
            Ui.Para($"Rest a night and forget your training: every skill point a hero spent on talents comes back. " +
                $"Costs {Ui.Gold(Talents.RespecBaseGold)} + {Ui.Gold(Talents.RespecGoldPerLevel)} per level. " +
                "Abilities from a hero's starting kit or bought at Learn are kept.", list, "dim");
            string closed = Talents.RespecClosed(S);
            if (closed != null) Ui.Para(closed, list, "bad");
            foreach (var h in S.Roster.OrderByDescending(x => S.Party.Contains(x.ClassId)).ThenBy(x => x.ClassId))
            {
                var hero = h;
                int spent = Talents.PointsSpent(C, hero);
                int cost = Talents.RespecCost(hero);
                string name = C.TryClass(hero.ClassId)?.Name ?? hero.ClassId;
                string value = spent == 0 ? "No talents bought" : $"{spent} pts · {Ui.Gold(cost)}";
                bool ok = closed == null && spent > 0 && S.Gold >= cost;
                var row = Ui.MenuRow($"{name}  Lv {Progression.Level(hero)}", value, () => Ask(C, hero,
                    $"Pay {Ui.Gold(cost)} to unlearn {name}'s talents and refund {spent} skill point{(spent == 1 ? "" : "s")}?",
                    r => Done(r), () => Talents.Respec(C, S, hero), () => Talents.Respec(C, S, hero, true)), list, null, ok);
                row.name = "respec-" + hero.ClassId;
            }
        }

        /// <summary>
        /// GG-T566 (owner: "warn and ask user"): the respec confirm. When the respec would forget a proficiency the hero's
        /// equipped weapon/armor needs, the question lists those pieces and the yes is "Unequip and respec" (they go to the
        /// bag); Cancel leaves everything as it was. Otherwise it is the plain "Pay" confirm.
        /// </summary>
        public static void Ask(ContentDb c, HeroSave hero, string question, Action<Result> done, Func<Result> respec, Func<Result> respecUnequipping)
        {
            var lost = Talents.GearLostOnRespec(c, hero);
            if (lost.Count == 0) { Confirm.Show(question, "Pay", () => done(respec())); return; }
            string items = string.Join(", ", lost.Select(g => c.TryItem(g.ItemId)?.Name ?? g.ItemId));
            Confirm.Show($"{question}\n\nAfter the respec this hero can't use: {items}. They'll be unequipped and go to the bag.",
                "Unequip and respec", () => done(respecUnequipping()));
        }
    }
}
