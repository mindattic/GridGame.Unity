using System;
using System.Collections.Generic;
using GridGame.Core.Content;
using GridGame.Core.Rules;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    // ─────────────────────────────────────────────────────────────────────────────
    // Skill hexagon board view (GG-T1055)
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Draws a <see cref="SkillHexBoard"/> (ContentDb.SkillHex) with <c>generateVisualContent</c>, the way
    /// docs/design/skill-hexagon/app.js render() draws it: the hexagon of radius <see cref="BoardRadius"/> px with a corner
    /// pointing up, six faint stat wedges, the lattice links (ability spokes brighter), passives as small diamonds in their
    /// stat's colour, abilities as circles coloured by their wedge, Open slots as faint rings. The hero's trail sits on
    /// top: the Start path in hot, bought nodes in accent, a preview route in fg.
    /// It lives inside a <see cref="PanZoomView"/> (<see cref="Host"/>); a screen that rebuilds keeps one
    /// <see cref="PanZoomState"/> field and hands it to every new host so the pan and zoom survive the rebuild.
    /// A pointer is hit-tested to the nearest lattice point: <see cref="HoverChanged"/> while it moves over the board,
    /// <see cref="Tapped"/> on a click. A press that turned into a pan never reaches <see cref="Tapped"/>: the
    /// PanZoomView swallows the click after a drag before it trickles down to this element.
    /// </summary>
    public sealed class SkillHexView : VisualElement
    {
        /// <summary>Board radius in px (app.js R): the centre-to-corner distance of the outer lattice ring.</summary>
        public const float BoardRadius = 520f;
        /// <summary>Room around the hexagon for the corner labels.</summary>
        public const float Margin = 70f;

        // The design page's palette (docs/design/skill-hexagon/style.css).
        public static readonly Color Bg = Hex("#0d0f14");
        public static readonly Color PanelColor = Hex("#151821");
        public static readonly Color LineColor = Hex("#2a2f3d");
        public static readonly Color Fg = Hex("#e6e8ee");
        public static readonly Color Muted = Hex("#9097a8");
        public static readonly Color Accent = Hex("#7fd1ff");
        public static readonly Color Hot = Hex("#ffcf6b");

        private static readonly float Sqrt3 = Mathf.Sqrt(3f);

        public SkillHexBoard Board { get; }

        /// <summary>Raised when the pointer moves onto another lattice point, or off the board (null).</summary>
        public event Action<(int Q, int R)?> HoverChanged;
        /// <summary>Raised when a lattice point is tapped (not after a pan).</summary>
        public event Action<(int Q, int R)> Tapped;

        /// <summary>The lattice point under the pointer, or null.</summary>
        public (int Q, int R)? Hovered { get; private set; }

        private readonly float step;                  // px between neighbouring lattice points (R / Radius)
        private readonly Vector2 centre;              // the board's centre in this element's local space
        private readonly Vector2[] corners = new Vector2[6]; // unit vectors to the six corners, clockwise from the top
        private readonly Color[] statColors = new Color[6];
        private readonly Dictionary<string, Color> colorOf = new Dictionary<string, Color>(StringComparer.Ordinal);
        private readonly List<(int Q, int R)> points = new List<(int, int)>();

        private HashSet<(int Q, int R)> start = new HashSet<(int, int)>();
        private HashSet<(int Q, int R)> bought = new HashSet<(int, int)>();
        private HashSet<(int Q, int R)> preview = new HashSet<(int, int)>();
        private (int Q, int R)? selected;

        public SkillHexView(SkillHexBoard board)
        {
            Board = board ?? throw new ArgumentNullException(nameof(board));
            name = "skill-hex";
            int n = Mathf.Max(1, board.Radius);
            step = BoardRadius / n;
            float side = 2f * (BoardRadius + Margin);
            style.width = side;
            style.height = side;
            style.backgroundColor = Bg;
            pickingMode = PickingMode.Position;
            centre = new Vector2(side / 2f, side / 2f);

            for (int k = 0; k < 6; k++)
            {
                float a = (-90f + 60f * k) * Mathf.Deg2Rad;
                corners[k] = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            }
            var stats = board.Def.Stats ?? new List<SkillHexStatDef>();
            for (int k = 0; k < 6; k++)
            {
                var s = k < stats.Count ? stats[k] : null;
                statColors[k] = s != null ? Palette.Hex(s.Color, Muted) : Muted;
                if (s?.Id != null) colorOf[s.Id] = statColors[k];
            }
            for (int q = -n; q <= n; q++)
                for (int r = -n; r <= n; r++)
                    if (board.Contains((q, r))) points.Add((q, r));

            // Corner labels: the stat ids, just outside each corner.
            for (int k = 0; k < 6; k++)
            {
                var id = k < stats.Count ? stats[k]?.Id : null;
                if (string.IsNullOrEmpty(id)) continue;
                var at = centre + corners[k] * (BoardRadius + 40f);
                var label = new Label(id) { pickingMode = PickingMode.Ignore };
                label.style.position = Position.Absolute;
                label.style.left = at.x - 40f;
                label.style.top = at.y - 18f;
                label.style.width = 80f;
                label.style.height = 36f;
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
                label.style.fontSize = 26;
                label.style.unityFontStyleAndWeight = FontStyle.Bold;
                label.style.color = statColors[k];
                Add(label);
            }

            generateVisualContent += Draw;
            RegisterCallback<PointerMoveEvent>(e => SetHover(HitTest(e.localPosition)));
            RegisterCallback<PointerLeaveEvent>(_ => SetHover(null));
            RegisterCallback<ClickEvent>(e =>
            {
                var pt = HitTest(e.localPosition);
                if (pt.HasValue) Tapped?.Invoke(pt.Value);
            });
        }

        /// <summary>A PanZoomView over a new view of <paramref name="board"/>. Pass the screen's long-lived
        /// <paramref name="state"/> field so a rebuild keeps the player's pan and zoom.</summary>
        public static PanZoomView Host(SkillHexBoard board, PanZoomState state, out SkillHexView view)
        {
            var host = new PanZoomView(state) { MinScale = 0.25f };
            view = new SkillHexView(board);
            host.Add(view);
            return host;
        }

        /// <summary>The hero's trail: its Start path (hot) and the nodes it bought since (accent).</summary>
        public void SetOwned(IEnumerable<(int Q, int R)> startPath, IEnumerable<(int Q, int R)> boughtNodes)
        {
            start = new HashSet<(int, int)>(startPath ?? Array.Empty<(int, int)>());
            bought = new HashSet<(int, int)>(boughtNodes ?? Array.Empty<(int, int)>());
            bought.ExceptWith(start);
            MarkDirtyRepaint();
        }

        /// <summary>A route the player is looking at (e.g. <see cref="SkillHexBoard.CheapestRoute"/> to the hovered node),
        /// drawn in fg; null or empty clears it.</summary>
        public void SetPreview(IEnumerable<(int Q, int R)> route)
        {
            preview = new HashSet<(int, int)>(route ?? Array.Empty<(int, int)>());
            MarkDirtyRepaint();
        }

        /// <summary>Rings a node in hot (the one a detail panel is showing); null clears it.</summary>
        public void SetSelected((int Q, int R)? pt)
        {
            selected = pt;
            MarkDirtyRepaint();
        }

        public bool Owns((int Q, int R) pt) => start.Contains(pt) || bought.Contains(pt);

        // ── Geometry ────────────────────────────────────────────────────────────

        /// <summary>A lattice point's position in this element: x = (Q + R/2)·d, y = R·√3/2·d, turned -90° so a corner
        /// points up (app.js rot()), about the element's centre.</summary>
        public Vector2 PositionOf((int Q, int R) pt)
        {
            float x = (pt.Q + pt.R / 2f) * step, y = pt.R * Sqrt3 / 2f * step;
            return centre + new Vector2(y, -x);
        }

        /// <summary>The lattice point nearest a local position, or null when it is off the board or more than half a step
        /// from any point.</summary>
        public (int Q, int R)? HitTest(Vector2 local)
        {
            var v = local - centre;
            float x = -v.y, y = v.x; // undo the -90° turn
            float r = y / (Sqrt3 / 2f * step);
            float q = x / step - r / 2f;
            var pt = AxialRound(q, r);
            if (!Board.Contains(pt)) return null;
            return (PositionOf(pt) - local).sqrMagnitude <= step * step * 0.36f ? pt : ((int, int)?)null;
        }

        private static (int Q, int R) AxialRound(float q, float r)
        {
            float s = -q - r;
            int rq = Mathf.RoundToInt(q), rr = Mathf.RoundToInt(r), rs = Mathf.RoundToInt(s);
            float dq = Mathf.Abs(rq - q), dr = Mathf.Abs(rr - r), ds = Mathf.Abs(rs - s);
            if (dq > dr && dq > ds) rq = -rr - rs;
            else if (dr > ds) rr = -rq - rs;
            return (rq, rr);
        }

        /// <summary>Stat weights at a board-relative point: barycentric in its wedge (corner A, corner B, centre) — app.js
        /// weights(). Returns the wedge index and the A, B and centre weights.</summary>
        private (int K, float A, float B, float O) Weights(Vector2 v)
        {
            float ang = (Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg + 90f + 360f) % 360f;
            int k = Mathf.FloorToInt(ang / 60f) % 6;
            Vector2 ca = corners[k], cb = corners[(k + 1) % 6];
            float det = ca.x * cb.y - ca.y * cb.x;
            float x = v.x / BoardRadius, y = v.y / BoardRadius;
            float a = (x * cb.y - y * cb.x) / det, b = (ca.x * y - ca.y * x) / det;
            return (k, Mathf.Max(0f, a), Mathf.Max(0f, b), Mathf.Max(0f, 1f - a - b));
        }

        private Color AbilityColor(Vector2 pos)
        {
            var w = Weights(pos - centre);
            if (w.O > 0.8f) return Fg;
            return statColors[w.A >= w.B ? w.K : (w.K + 1) % 6];
        }

        private Color PassiveColor(SkillHexNodeDef node)
        {
            if (node?.Stats != null)
                foreach (var kv in node.Stats)
                    return colorOf.TryGetValue(kv.Key, out var c) ? c : Muted;
            return Muted;
        }

        private static bool IsAbilitySlot((int Q, int R) pt) => SkillHexDef.IsSlot(pt.Q, pt.R);

        // ── Drawing ─────────────────────────────────────────────────────────────

        private void Draw(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;

            // Wedges: stat-tinted triangles from the centre to each pair of corners, edged in the line colour.
            for (int k = 0; k < 6; k++)
            {
                var a = centre + corners[k] * BoardRadius;
                var b = centre + corners[(k + 1) % 6] * BoardRadius;
                p.fillColor = WithAlpha(statColors[k], 0.035f);
                p.BeginPath();
                p.MoveTo(centre); p.LineTo(a); p.LineTo(b); p.ClosePath();
                p.Fill();
                p.strokeColor = LineColor;
                p.lineWidth = 1f;
                p.Stroke();
            }

            // Lattice links, one stroked path per brightness: ability spokes, then passive-to-passive.
            StrokeLinks(p, true, WithAlpha(Muted, 0.13f));
            StrokeLinks(p, false, WithAlpha(Muted, 0.06f));

            // Trail: Start path in hot, bought nodes in accent (each link with a bought end), the preview in fg.
            var hot = new ShapeStyle { Width = 4f, Fill = WithAlpha(Hot, 0.9f) };
            var acc = new ShapeStyle { Width = 4f, Fill = WithAlpha(Accent, 0.9f) };
            var pv = new ShapeStyle { Width = 2f, Fill = WithAlpha(Fg, 0.9f) };
            foreach (var pt in start)
                foreach (var nb in Board.Neighbors(pt))
                    if (start.Contains(nb) && Less(pt, nb)) Shapes.Bar(p, PositionOf(pt), PositionOf(nb), hot);
            foreach (var pt in bought)
                foreach (var nb in Board.Neighbors(pt))
                    if ((bought.Contains(nb) && Less(pt, nb)) || start.Contains(nb)) Shapes.Bar(p, PositionOf(pt), PositionOf(nb), acc);
            foreach (var pt in preview)
                foreach (var nb in Board.Neighbors(pt))
                    if ((preview.Contains(nb) && Less(pt, nb)) || (!preview.Contains(nb) && Owns(nb)))
                        Shapes.Bar(p, PositionOf(pt), PositionOf(nb), pv);

            // Nodes.
            foreach (var pt in points)
            {
                var node = Board.Node(pt);
                var pos = PositionOf(pt);
                bool own = Owns(pt), inPreview = preview.Contains(pt);
                if (node == null) continue;
                switch (node.Kind)
                {
                    case SkillHexNodeKind.Ability:
                    {
                        var col = AbilityColor(pos);
                        var s = new ShapeStyle { Fill = own ? col : PanelColor, Line = inPreview ? Fg : col, Outline = own ? 2.5f : 2f };
                        Shapes.Dot(p, pos, own ? 8f : 6.5f, s);
                        break;
                    }
                    case SkillHexNodeKind.Passive:
                        DrawDiamond(p, pos, 2.6f * Mathf.Sqrt(2f), PassiveColor(node), own, inPreview);
                        break;
                    default: // Open slot (or a slot the file left without a kind): a faint ring.
                    {
                        if (own) Shapes.Dot(p, pos, 3f, new ShapeStyle { Fill = Hot });
                        var ring = new ShapeStyle { Width = 1f, Fill = !string.IsNullOrEmpty(node.Note) ? Accent : LineColor };
                        Shapes.Ring(p, pos, 3f, ring);
                        break;
                    }
                }
            }

            // Selection in hot, hover in faint fg.
            if (selected.HasValue && Board.Contains(selected.Value))
                Shapes.Ring(p, PositionOf(selected.Value), 12f, new ShapeStyle { Width = 2f, Fill = Hot });
            if (Hovered.HasValue && !Hovered.Equals(selected))
                Shapes.Ring(p, PositionOf(Hovered.Value), 11f, new ShapeStyle { Width = 1.5f, Fill = WithAlpha(Fg, 0.6f) });
        }

        private void StrokeLinks(Painter2D p, bool abilitySpokes, Color color)
        {
            p.strokeColor = color;
            p.lineWidth = 1f;
            p.BeginPath();
            foreach (var pt in points)
                foreach (var nb in Board.Neighbors(pt))
                {
                    if (!Less(pt, nb)) continue;
                    bool spoke = IsAbilitySlot(pt) || IsAbilitySlot(nb);
                    if (spoke != abilitySpokes) continue;
                    p.MoveTo(PositionOf(pt));
                    p.LineTo(PositionOf(nb));
                }
            p.Stroke();
        }

        /// <summary>A passive: a small diamond, filled in its stat colour when owned, otherwise only edged (faintly).</summary>
        private static void DrawDiamond(Painter2D p, Vector2 c, float r, Color col, bool own, bool inPreview)
        {
            p.BeginPath();
            p.MoveTo(c + new Vector2(0f, -r));
            p.LineTo(c + new Vector2(r, 0f));
            p.LineTo(c + new Vector2(0f, r));
            p.LineTo(c + new Vector2(-r, 0f));
            p.ClosePath();
            if (own)
            {
                p.fillColor = col;
                p.Fill();
            }
            p.strokeColor = inPreview ? Fg : own ? col : WithAlpha(col, 0.45f);
            p.lineWidth = 1f;
            p.Stroke();
        }

        private void SetHover((int Q, int R)? pt)
        {
            if (Nullable.Equals(pt, Hovered)) return;
            Hovered = pt;
            MarkDirtyRepaint();
            HoverChanged?.Invoke(pt);
        }

        private static bool Less((int Q, int R) a, (int Q, int R) b) => a.Q < b.Q || (a.Q == b.Q && a.R < b.R);

        private static Color WithAlpha(Color c, float a) { c.a *= a; return c; }

        private static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
    }
}
