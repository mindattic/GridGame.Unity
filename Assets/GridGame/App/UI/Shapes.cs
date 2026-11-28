using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>
    /// How a <see cref="Shapes"/> shape looks (GG-T735): body thickness, outline thickness, arrowhead size and dash pattern,
    /// plus the body tint (white by default) and the outline colour (near-black). Use a preset (<see cref="Indicator"/>,
    /// <see cref="Tutorial"/>, <see cref="Small"/>) and adjust it with <see cref="Tint"/>/<see cref="Thick"/>/<see cref="Dashes"/>
    /// so every arrow, ring and box on screen shares the same stroke and head proportions.
    /// </summary>
    public struct ShapeStyle
    {
        /// <summary>Body thickness in px: an arrow's shaft, a ring's band, a frame's stroke, a dot's diameter.</summary>
        public float Width;
        /// <summary>Outline thickness in px, drawn wholly outside the body.</summary>
        public float Outline;
        /// <summary>Arrowhead length (tip to base) and full width at the base, in px.</summary>
        public float HeadLength, HeadWidth;
        /// <summary>Dash pattern for the dashed shapes: each dash's centreline length (its round caps add
        /// <see cref="Width"/>) and the visible gap between dashes (px). A dash of 0 makes round dots of <see cref="Width"/> diameter.</summary>
        public float Dash, Gap;
        /// <summary>Body colour (white unless tinted) and outline colour.</summary>
        public Color Fill, Line;

        public static readonly Color Ink = new Color(0f, 0f, 0.05f, 0.9f);

        /// <summary>On-board pointers and target arrows.</summary>
        public static ShapeStyle Indicator => new ShapeStyle
        { Width = 8f, Outline = 3f, HeadLength = 34f, HeadWidth = 42f, Dash = 18f, Gap = 12f, Fill = Color.white, Line = Ink };

        /// <summary>Coach marks: the caption's pointer, a move's route, the destination box.</summary>
        public static ShapeStyle Tutorial => new ShapeStyle
        { Width = 10f, Outline = 3f, HeadLength = 46f, HeadWidth = 58f, Dash = 22f, Gap = 14f, Fill = Color.white, Line = Ink };

        /// <summary>Inline glyph-sized marks (chevrons on rows, small rings).</summary>
        public static ShapeStyle Small => new ShapeStyle
        { Width = 4f, Outline = 2f, HeadLength = 14f, HeadWidth = 16f, Dash = 8f, Gap = 6f, Fill = Color.white, Line = Ink };

        public ShapeStyle Tint(Color c) { var s = this; s.Fill = c; return s; }
        public ShapeStyle Outlined(Color c, float width) { var s = this; s.Line = c; s.Outline = width; return s; }
        public ShapeStyle Thick(float width) { var s = this; s.Width = width; return s; }
        public ShapeStyle Dashes(float dash, float gap) { var s = this; s.Dash = dash; s.Gap = gap; return s; }
        public ShapeStyle Head(float length, float width) { var s = this; s.HeadLength = length; s.HeadWidth = width; return s; }
    }

    /// <summary>
    /// Vector shapes for Painter2D (GG-T735), one API for every arrow, chevron, ring and box drawn in a
    /// <c>generateVisualContent</c> callback. Each shape is built from math as closed polygons (a shaft is its centreline
    /// offset by half the width with mitred joins, the arrowhead is part of the same outline, ends are round caps), then
    /// painted in two passes: the outline (stroked at twice <see cref="ShapeStyle.Outline"/>, so the fill covers its inner
    /// half and the outline sits wholly outside) and the body fill on top. Pieces of one shape (dashes, ring loops) share
    /// both passes, so their outlines never cut across each other's bodies.
    /// </summary>
    public static class Shapes
    {
        private const int CapSegments = 8, CircleSegments = 40, BezierSegments = 36, CornerSegments = 6;
        private const float DefaultBow = 0.18f, DefaultCorner = 14f;

        // ── Arrows ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// An arrow from <paramref name="from"/> (round tail) to its tip at <paramref name="to"/> (GG-T1833): straight when the
        /// two share a row or column (a right angle's single leg), otherwise a cubic Bezier bowed by <paramref name="bow"/>
        /// (share of the span, to the left of travel; negative bows right). Always a filled, outlined body, never a stroke.
        /// </summary>
        public static void Arrow(Painter2D p, Vector2 from, Vector2 to, ShapeStyle s, float bow = DefaultBow)
        {
            if (Aligned(from, to)) PathArrow(p, new[] { from, to }, s);
            else BezierArrow(p, from, to, s, bow);
        }

        /// <summary>A dashed straight arrow; <paramref name="phase"/> (px) slides the dashes toward the tip (marching).</summary>
        public static void DashedArrow(Painter2D p, Vector2 from, Vector2 to, ShapeStyle s, float phase = 0f) =>
            DashedArrow(p, new[] { from, to }, s, phase);

        /// <summary>
        /// A right-angle arrow (GG-T1833): straight when the ends share a row or column, else an L (horizontal leg first unless
        /// <paramref name="verticalFirst"/>) with its corner rounded by <paramref name="corner"/> px. <paramref name="insetStart"/>
        /// and <paramref name="insetEnd"/> trim px off the tail and tip along the path (to stop short of tokens).
        /// </summary>
        public static void OrthoArrow(Painter2D p, Vector2 from, Vector2 to, ShapeStyle s, bool verticalFirst = false,
            float corner = DefaultCorner, float insetStart = 0f, float insetEnd = 0f)
        {
            var path = new List<Vector2> { from };
            if (!Aligned(from, to)) path.Add(verticalFirst ? new Vector2(from.x, to.y) : new Vector2(to.x, from.y));
            path.Add(to);
            if (insetStart > 0f || insetEnd > 0f) path = Sub(path, insetStart, Length(path) - insetEnd);
            PathArrow(p, RoundCorners(path, corner), s);
        }

        /// <summary>A solid arrow along a right-angle polyline (a board route) with each corner rounded by <paramref name="corner"/> px.</summary>
        public static void OrthoPathArrow(Painter2D p, IList<Vector2> path, ShapeStyle s, float corner = DefaultCorner) =>
            PathArrow(p, RoundCorners(Clean(path), corner), s);

        /// <summary>
        /// A cubic Bezier arrow b0 → b3 (GG-T1833) with control points b1, b2: the shaft is the sampled curve offset along its
        /// normal on both sides (a filled ribbon), the head lies along the end tangent, and one outline runs round the lot.
        /// </summary>
        public static void BezierArrow(Painter2D p, Vector2 b0, Vector2 b1, Vector2 b2, Vector2 b3, ShapeStyle s, bool dashed = false, float phase = 0f)
        {
            var pts = new Vector2[BezierSegments + 1];
            for (int i = 0; i <= BezierSegments; i++)
            {
                float t = i / (float)BezierSegments, u = 1f - t;
                pts[i] = u * u * u * b0 + 3f * u * u * t * b1 + 3f * u * t * t * b2 + t * t * t * b3;
            }
            if (dashed) DashedArrow(p, pts, s, phase); else PathArrow(p, pts, s);
        }

        /// <summary>A cubic Bezier arrow with b1, b2 picked for a perpendicular bow: both a third of the way in from their
        /// ends and pushed <paramref name="bow"/> × span to the left of travel (negative bows right).</summary>
        public static void BezierArrow(Painter2D p, Vector2 from, Vector2 to, ShapeStyle s, float bow = DefaultBow, bool dashed = false, float phase = 0f)
        {
            var span = to - from;
            var off = Perp(Dir(from, to, Vector2.right)) * (span.magnitude * bow);
            BezierArrow(p, from, from + span / 3f + off, from + span * (2f / 3f) + off, to, s, dashed, phase);
        }

        /// <summary>A solid arrow along a polyline: one outline from the round tail, along the mitred shaft, around the head.</summary>
        public static void PathArrow(Painter2D p, IList<Vector2> path, ShapeStyle s)
        {
            var pts = Clean(path);
            if (pts.Count < 2) return;
            float total = Length(pts), head = Mathf.Min(s.HeadLength, total);
            var tip = pts[pts.Count - 1];
            var shaft = Sub(pts, 0f, total - head);
            var baseP = shaft[shaft.Count - 1];
            var dir = Dir(baseP, tip, shaft.Count >= 2 ? Dir(shaft[shaft.Count - 2], baseP, Vector2.right) : Vector2.right);
            var n = Perp(dir);
            float hw = s.Width * 0.5f, hh = Mathf.Max(hw, s.HeadWidth * 0.5f);
            var poly = new List<Vector2>();
            if (shaft.Count >= 2 && total - head > 0.5f)
            {
                var (left, right) = Offsets(shaft, hw);
                // Square the shaft's last offsets onto the head's base so the head joins it cleanly.
                left[left.Count - 1] = baseP + n * hw;
                right[right.Count - 1] = baseP - n * hw;
                StartCap(poly, shaft[0], Dir(shaft[0], shaft[1], dir), hw);
                poly.AddRange(left);
                poly.Add(baseP + n * hh); poly.Add(tip); poly.Add(baseP - n * hh);
                for (int i = right.Count - 1; i >= 0; i--) poly.Add(right[i]);
            }
            else
            {
                poly.Add(baseP + n * hh); poly.Add(tip); poly.Add(baseP - n * hh);
            }
            Paint(p, s, poly);
        }

        /// <summary>A dashed arrow along a polyline: round-capped dashes (or dots, with <see cref="ShapeStyle.Dash"/> 0)
        /// up to a solid head. <paramref name="phase"/> marches the dashes toward the tip.</summary>
        public static void DashedArrow(Painter2D p, IList<Vector2> path, ShapeStyle s, float phase = 0f)
        {
            var pts = Clean(path);
            if (pts.Count < 2) return;
            float total = Length(pts), head = Mathf.Min(s.HeadLength, total);
            var tip = pts[pts.Count - 1];
            var baseP = PointAt(pts, total - head);
            var dir = Dir(baseP, tip, Vector2.right);
            var n = Perp(dir);
            float hw = s.Width * 0.5f, hh = Mathf.Max(hw, s.HeadWidth * 0.5f);
            var polys = Dashes(pts, s, phase, total - head - hw - s.Gap * 0.5f);
            polys.Add(new List<Vector2> { baseP + n * hh, tip, baseP - n * hh });
            Paint(p, s, polys);
        }

        // ── Marks ───────────────────────────────────────────────────────────────────

        /// <summary>A chevron (">" pointing along <paramref name="dir"/>) centred on <paramref name="center"/>;
        /// <paramref name="size"/> is its arm span across and half its depth along.</summary>
        public static void Chevron(Painter2D p, Vector2 center, Vector2 dir, float size, ShapeStyle s)
        {
            dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.right;
            var n = Perp(dir);
            float depth = size * 0.5f;
            var line = new List<Vector2> { center - dir * depth * 0.5f + n * size * 0.5f, center + dir * depth * 0.5f, center - dir * depth * 0.5f - n * size * 0.5f };
            Paint(p, s, Band(line, s.Width * 0.5f));
        }

        /// <summary>A round-capped bar (an arrow with no head) from <paramref name="from"/> to <paramref name="to"/>: a link (GG-T736).</summary>
        public static void Bar(Painter2D p, Vector2 from, Vector2 to, ShapeStyle s) =>
            Paint(p, s, Band(new List<Vector2> { from, to }, s.Width * 0.5f));

        /// <summary>
        /// A round-capped stroke along a polyline with mitred corners (GG-T737): a connector, a progress trace. With
        /// <paramref name="closed"/> the polyline is a loop: no caps, every corner mitred, a hole in the middle.
        /// </summary>
        public static void Line(Painter2D p, IList<Vector2> path, ShapeStyle s, bool closed = false)
        {
            var pts = Clean(path);
            if (closed && pts.Count > 2 && (pts[0] - pts[pts.Count - 1]).sqrMagnitude <= 0.01f) pts.RemoveAt(pts.Count - 1);
            if (!closed || pts.Count < 3) { if (pts.Count > 0) Paint(p, s, Band(pts, s.Width * 0.5f)); return; }
            // Offset the loop with its wrap-around neighbours so every vertex, the first included, is mitred.
            var wrap = new List<Vector2>(pts.Count + 2) { pts[pts.Count - 1] };
            wrap.AddRange(pts);
            wrap.Add(pts[0]);
            var (left, right) = Offsets(wrap, s.Width * 0.5f);
            var outer = left.GetRange(1, pts.Count);
            var inner = right.GetRange(1, pts.Count);
            inner.Reverse();
            Paint(p, s, new List<List<Vector2>> { outer, inner }, FillRule.OddEven);
        }

        /// <summary>A filled dot (outlined disc) of <paramref name="radius"/>.</summary>
        public static void Dot(Painter2D p, Vector2 center, float radius, ShapeStyle s) => Paint(p, s, Circle(center, radius));

        /// <summary>GG-T895: a wobbling blob (outlined, like <see cref="Dot"/>): a disc of <paramref name="radius"/> whose rim
        /// ripples in slow lobes by <paramref name="wobble"/> (share of the radius) at <paramref name="phase"/> (radians), its
        /// lower half squashed flat as if resting on the floor. A wobble of 0 is a plain squashed disc.</summary>
        public static void Blob(Painter2D p, Vector2 center, float radius, float wobble, float phase, ShapeStyle s)
        {
            var pts = new List<Vector2>(CircleSegments);
            for (int k = 0; k < CircleSegments; k++)
            {
                float a = 2f * Mathf.PI * k / CircleSegments;
                float r = radius * (1f + wobble * (Mathf.Sin(3f * a + phase) * 0.7f + Mathf.Sin(5f * a - phase * 1.3f) * 0.3f));
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                if (d.y > 0f) d.y *= 0.72f;
                pts.Add(center + d * r);
            }
            Paint(p, s, pts);
        }

        /// <summary>A ring (band of <see cref="ShapeStyle.Width"/>) of centreline <paramref name="radius"/>; a partial ring
        /// sweeps <paramref name="sweepDeg"/> clockwise (screen space) from <paramref name="startDeg"/> (0 = right) with round ends.</summary>
        public static void Ring(Painter2D p, Vector2 center, float radius, ShapeStyle s, float startDeg = 0f, float sweepDeg = 360f)
        {
            float hw = s.Width * 0.5f;
            if (Mathf.Abs(sweepDeg) >= 359.9f)
            {
                var outer = Circle(center, radius + hw);
                var inner = Circle(center, Mathf.Max(0f, radius - hw));
                inner.Reverse();
                Paint(p, s, new List<List<Vector2>> { outer, inner }, FillRule.OddEven);
                return;
            }
            int segs = Mathf.Max(4, Mathf.CeilToInt(CircleSegments * Mathf.Abs(sweepDeg) / 360f));
            var arc = new List<Vector2>(segs + 1);
            for (int i = 0; i <= segs; i++)
            {
                float a = (startDeg + sweepDeg * i / segs) * Mathf.Deg2Rad;
                arc.Add(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
            }
            Paint(p, s, Band(arc, hw));
        }

        /// <summary>GG-T1819: a filled wedge of <paramref name="radius"/> sweeping <paramref name="sweepDeg"/> clockwise (screen
        /// space) from <paramref name="startDeg"/> (0 = right, −90 = 12 o'clock); a full sweep is a disc. The cooldown sweep.</summary>
        public static void Pie(Painter2D p, Vector2 center, float radius, ShapeStyle s, float startDeg = -90f, float sweepDeg = 360f)
        {
            if (Mathf.Abs(sweepDeg) >= 359.9f) { Paint(p, s, Circle(center, radius)); return; }
            if (Mathf.Abs(sweepDeg) < 0.5f) return;
            int segs = Mathf.Max(2, Mathf.CeilToInt(CircleSegments * Mathf.Abs(sweepDeg) / 360f));
            var poly = new List<Vector2>(segs + 2) { center };
            for (int i = 0; i <= segs; i++)
            {
                float a = (startDeg + sweepDeg * i / segs) * Mathf.Deg2Rad;
                poly.Add(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
            }
            Paint(p, s, poly);
        }

        /// <summary>
        /// A rounded rectangle. <paramref name="filled"/>: a solid outlined body. Otherwise a frame whose stroke
        /// (<see cref="ShapeStyle.Width"/>) is centred on the rect's edge; <paramref name="dashed"/> breaks it into dashes,
        /// and <paramref name="portion"/> &lt; 1 draws only that fraction of the perimeter, clockwise from
        /// <paramref name="start"/> (a fraction of the perimeter from the top-left corner's end): a progress frame.
        /// </summary>
        public static void RoundedRect(Painter2D p, Rect r, float radius, ShapeStyle s, bool filled = false, bool dashed = false,
            float start = 0f, float portion = 1f)
        {
            radius = Mathf.Clamp(radius, 0f, Mathf.Min(r.width, r.height) * 0.5f);
            float hw = s.Width * 0.5f;
            if (filled) { Paint(p, s, Perimeter(r, radius)); return; }
            portion = Mathf.Clamp01(portion);
            if (portion <= 0f) return;
            if (!dashed && portion >= 1f)
            {
                var outer = Perimeter(Inflate(r, hw), radius + hw);
                var inner = Perimeter(Inflate(r, -hw), Mathf.Max(0f, radius - hw));
                inner.Reverse();
                Paint(p, s, new List<List<Vector2>> { outer, inner }, FillRule.OddEven);
                return;
            }
            var loop = Perimeter(r, radius);
            loop.Add(loop[0]);
            float total = Length(loop), from = Mathf.Repeat(start, 1f) * total, len = portion * total;
            // Walk the closed perimeter twice so a run that wraps past the start stays one piece.
            var twice = new List<Vector2>(loop);
            twice.AddRange(loop.GetRange(1, loop.Count - 1));
            var run = Sub(twice, from, from + len);
            if (!dashed) { Paint(p, s, Band(run, hw)); return; }
            Paint(p, s, Dashes(run, s, 0f, Length(run)));
        }

        // ── Painting ────────────────────────────────────────────────────────────────

        private static void Paint(Painter2D p, ShapeStyle s, List<Vector2> poly) =>
            Paint(p, s, new List<List<Vector2>> { poly });

        /// <summary>Outline pass (every piece), then one fill over every piece.</summary>
        private static void Paint(Painter2D p, ShapeStyle s, List<List<Vector2>> polys, FillRule rule = FillRule.NonZero)
        {
            if (polys.Count == 0) return;
            if (s.Outline > 0f && s.Line.a > 0f)
            {
                p.lineJoin = LineJoin.Miter;
                p.miterLimit = 4f;
                p.lineWidth = s.Outline * 2f;
                p.strokeColor = s.Line;
                foreach (var poly in polys)
                {
                    if (poly.Count < 3) continue;
                    p.BeginPath();
                    Trace(p, poly);
                    p.Stroke();
                }
            }
            p.fillColor = s.Fill;
            p.BeginPath();
            foreach (var poly in polys) if (poly.Count >= 3) Trace(p, poly);
            p.Fill(rule);
        }

        private static void Trace(Painter2D p, List<Vector2> poly)
        {
            p.MoveTo(poly[0]);
            for (int i = 1; i < poly.Count; i++) p.LineTo(poly[i]);
            p.ClosePath();
        }

        // ── Geometry ────────────────────────────────────────────────────────────────

        /// <summary>Dashes along <paramref name="pts"/> up to distance <paramref name="until"/>: capsules, or discs for a 0 dash.</summary>
        private static List<List<Vector2>> Dashes(List<Vector2> pts, ShapeStyle s, float phase, float until)
        {
            var polys = new List<List<Vector2>>();
            // Gap is the visible space between round caps, so the period is dash + width + gap.
            float hw = s.Width * 0.5f, dash = Mathf.Max(0f, s.Dash), period = Mathf.Max(1f, dash + s.Width + Mathf.Max(0f, s.Gap));
            for (float d = hw + Mathf.Repeat(phase, period); d <= until; d += period)
            {
                if (dash <= 0f) { polys.Add(Circle(PointAt(pts, d), hw)); continue; }
                float e = Mathf.Min(d + dash, until);
                var seg = Sub(pts, d, e);
                polys.Add(seg.Count >= 2 && e - d > 0.5f ? Band(seg, hw) : Circle(PointAt(pts, d), hw));
            }
            return polys;
        }

        /// <summary>A closed outline around a polyline: mitred sides, round caps at both ends.</summary>
        private static List<Vector2> Band(List<Vector2> line, float hw)
        {
            line = Clean(line);
            var poly = new List<Vector2>();
            if (line.Count < 2) { if (line.Count == 1) poly.AddRange(Circle(line[0], hw)); return poly; }
            var (left, right) = Offsets(line, hw);
            StartCap(poly, line[0], Dir(line[0], line[1], Vector2.right), hw);
            poly.AddRange(left);
            EndCap(poly, line[line.Count - 1], Dir(line[line.Count - 2], line[line.Count - 1], Vector2.right), hw);
            for (int i = right.Count - 1; i >= 0; i--) poly.Add(right[i]);
            return poly;
        }

        /// <summary>The polyline offset by ±<paramref name="hw"/>, mitred at every inner vertex (the mitre clamped at 4×).</summary>
        private static (List<Vector2> left, List<Vector2> right) Offsets(List<Vector2> line, float hw)
        {
            var left = new List<Vector2>(line.Count);
            var right = new List<Vector2>(line.Count);
            for (int i = 0; i < line.Count; i++)
            {
                Vector2 m;
                float len = hw;
                if (i == 0) m = Perp(Dir(line[0], line[1], Vector2.right));
                else if (i == line.Count - 1) m = Perp(Dir(line[i - 1], line[i], Vector2.right));
                else
                {
                    var n0 = Perp(Dir(line[i - 1], line[i], Vector2.right));
                    var n1 = Perp(Dir(line[i], line[i + 1], Vector2.right));
                    m = n0 + n1;
                    m = m.sqrMagnitude < 1e-6f ? n0 : m.normalized;
                    len = hw / Mathf.Max(0.25f, Vector2.Dot(m, n0));
                }
                left.Add(line[i] + m * len);
                right.Add(line[i] - m * len);
            }
            return (left, right);
        }

        /// <summary>Half circle around the start: from the right side, behind the start, to the left side.</summary>
        private static void StartCap(List<Vector2> poly, Vector2 c, Vector2 dir, float hw)
        {
            var n = Perp(dir);
            for (int k = 0; k <= CapSegments; k++)
            {
                float a = Mathf.PI * k / CapSegments;
                poly.Add(c + (-n * Mathf.Cos(a) - dir * Mathf.Sin(a)) * hw);
            }
        }

        /// <summary>Half circle around the end: from the left side, past the end, to the right side.</summary>
        private static void EndCap(List<Vector2> poly, Vector2 c, Vector2 dir, float hw)
        {
            var n = Perp(dir);
            for (int k = 0; k <= CapSegments; k++)
            {
                float a = Mathf.PI * k / CapSegments;
                poly.Add(c + (n * Mathf.Cos(a) + dir * Mathf.Sin(a)) * hw);
            }
        }

        private static List<Vector2> Circle(Vector2 c, float r)
        {
            int segs = r < 12f ? 16 : CircleSegments;
            var pts = new List<Vector2>(segs);
            for (int k = 0; k < segs; k++)
            {
                float a = 2f * Mathf.PI * k / segs;
                pts.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
            }
            return pts;
        }

        /// <summary>A rounded rect's outline, clockwise (screen space) from the end of the top-left corner.</summary>
        private static List<Vector2> Perimeter(Rect r, float radius)
        {
            var pts = new List<Vector2>();
            if (radius <= 0.01f)
            {
                pts.Add(new Vector2(r.xMin, r.yMin)); pts.Add(new Vector2(r.xMax, r.yMin));
                pts.Add(new Vector2(r.xMax, r.yMax)); pts.Add(new Vector2(r.xMin, r.yMax));
                return pts;
            }
            const int segs = 6;
            void Corner(Vector2 c, float a0)
            {
                for (int k = 0; k <= segs; k++)
                {
                    float a = (a0 + 90f * k / segs) * Mathf.Deg2Rad;
                    pts.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
                }
            }
            Corner(new Vector2(r.xMax - radius, r.yMin + radius), -90f);
            Corner(new Vector2(r.xMax - radius, r.yMax - radius), 0f);
            Corner(new Vector2(r.xMin + radius, r.yMax - radius), 90f);
            Corner(new Vector2(r.xMin + radius, r.yMin + radius), 180f);
            // Start where the top-left corner ends (the top edge's left end).
            var first = pts[pts.Count - 1];
            pts.RemoveAt(pts.Count - 1);
            pts.Insert(0, first);
            return pts;
        }

        private static Rect Inflate(Rect r, float by) =>
            new Rect(r.xMin - by, r.yMin - by, Mathf.Max(0f, r.width + by * 2f), Mathf.Max(0f, r.height + by * 2f));

        /// <summary>True when the two points share a row or column, so one straight leg joins them at a right angle.</summary>
        private static bool Aligned(Vector2 a, Vector2 b) => Mathf.Abs(a.x - b.x) < 0.5f || Mathf.Abs(a.y - b.y) < 0.5f;

        /// <summary>The polyline with each inner corner replaced by a circular fillet of up to <paramref name="radius"/> px
        /// (shrunk to fit half of each neighbouring leg); straight-through vertices are left as they are.</summary>
        private static List<Vector2> RoundCorners(List<Vector2> pts, float radius)
        {
            if (pts.Count < 3 || radius <= 0f) return pts;
            var o = new List<Vector2> { pts[0] };
            for (int i = 1; i < pts.Count - 1; i++)
            {
                Vector2 a = pts[i - 1], v = pts[i], b = pts[i + 1];
                var d0 = Dir(a, v, Vector2.right);
                var d1 = Dir(v, b, Vector2.right);
                float turn = Vector2.Angle(d0, d1) * Mathf.Deg2Rad;
                if (turn < 0.05f) { o.Add(v); continue; }
                float tan = Mathf.Tan(turn * 0.5f);
                float cut = Mathf.Min(radius * tan, Vector2.Distance(a, v) * 0.5f, Vector2.Distance(v, b) * 0.5f);
                var p0 = v - d0 * cut;
                var p1 = v + d1 * cut;
                // A quadratic through the corner is close enough to the circular fillet at these sizes.
                for (int k = 0; k <= CornerSegments; k++)
                {
                    float t = k / (float)CornerSegments, u = 1f - t;
                    o.Add(u * u * p0 + 2f * u * t * v + t * t * p1);
                }
            }
            o.Add(pts[pts.Count - 1]);
            return o;
        }

        private static Vector2 Perp(Vector2 d) => new Vector2(-d.y, d.x);

        private static Vector2 Dir(Vector2 a, Vector2 b, Vector2 fallback)
        {
            var d = b - a;
            return d.sqrMagnitude > 1e-6f ? d.normalized : fallback;
        }

        /// <summary>The points with repeats (zero-length segments) dropped.</summary>
        private static List<Vector2> Clean(IList<Vector2> pts)
        {
            var o = new List<Vector2>(pts?.Count ?? 0);
            if (pts == null) return o;
            foreach (var v in pts) if (o.Count == 0 || (v - o[o.Count - 1]).sqrMagnitude > 0.01f) o.Add(v);
            return o;
        }

        private static float Length(List<Vector2> pts)
        {
            float t = 0f;
            for (int i = 1; i < pts.Count; i++) t += Vector2.Distance(pts[i - 1], pts[i]);
            return t;
        }

        /// <summary>The point <paramref name="d"/> px along the polyline (clamped to its ends).</summary>
        public static Vector2 PointAt(IList<Vector2> pts, float d)
        {
            if (pts == null || pts.Count == 0) return default;
            if (d <= 0f) return pts[0];
            for (int i = 1; i < pts.Count; i++)
            {
                float len = Vector2.Distance(pts[i - 1], pts[i]);
                if (d <= len) return Vector2.Lerp(pts[i - 1], pts[i], len <= 0f ? 0f : d / len);
                d -= len;
            }
            return pts[pts.Count - 1];
        }

        /// <summary>The piece of the polyline between distances <paramref name="d0"/> and <paramref name="d1"/>.</summary>
        private static List<Vector2> Sub(List<Vector2> pts, float d0, float d1)
        {
            var o = new List<Vector2> { PointAt(pts, d0) };
            float acc = 0f;
            for (int i = 1; i < pts.Count; i++)
            {
                acc += Vector2.Distance(pts[i - 1], pts[i]);
                if (acc > d0 && acc < d1) o.Add(pts[i]);
                if (acc >= d1) break;
            }
            o.Add(PointAt(pts, Mathf.Max(d0, d1)));
            return Clean(o);
        }
    }
}
