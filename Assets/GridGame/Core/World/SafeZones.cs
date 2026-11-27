using System;
using System.Collections.Generic;
using GridGame.Core.Content;

namespace GridGame.Core.World
{
    /// <summary>The safe union of the camp circle and the woken waystone circles (GG-T1685, BIBLE 28.4, 28.8): danger
    /// does not rise inside it, and overlapping circles form one corridor. Distances are along a polyline in art pixels.</summary>
    public sealed class SafeZones
    {
        readonly List<double[]> circles = new List<double[]>();

        public int CircleCount => circles.Count;

        public SafeZones(CampSite camp, IEnumerable<WaystoneCircle> waystoneCircles)
        {
            if (camp != null && camp.Radius > 0) circles.Add(new[] { camp.X, camp.Y, camp.Radius });
            if (waystoneCircles != null)
                foreach (var c in waystoneCircles)
                    if (c.Radius > 0) circles.Add(new[] { (double)c.X, c.Y, c.Radius });
        }

        public static SafeZones From(CampSite camp, WaystoneState waystones)
        {
            return new SafeZones(camp, waystones?.Circles());
        }

        public bool Contains(double x, double y)
        {
            foreach (var c in circles)
            {
                double dx = x - c[0], dy = y - c[1];
                if (dx * dx + dy * dy <= c[2] * c[2]) return true;
            }
            return false;
        }

        public static double LengthOf(IList<WorldPoint> polyline)
        {
            double total = 0;
            if (polyline == null) return 0;
            for (int i = 1; i < polyline.Count; i++) total += Seg(polyline[i - 1], polyline[i]);
            return total;
        }

        /// <summary>The covered stretches along the polyline as [startDist, endDist] pairs, ascending and merged so
        /// overlapping or touching circles give one span.</summary>
        public List<double[]> SafeSpans(IList<WorldPoint> polyline)
        {
            var raw = new List<double[]>();
            if (polyline == null || circles.Count == 0) return raw;
            double offset = 0;
            for (int i = 1; i < polyline.Count; i++)
            {
                var a = polyline[i - 1];
                var b = polyline[i];
                double len = Seg(a, b);
                if (len > 0)
                {
                    foreach (var c in circles)
                        if (Clip(a, b, c, out double t0, out double t1))
                            raw.Add(new[] { offset + t0 * len, offset + t1 * len });
                }
                offset += len;
            }
            raw.Sort((p, q) => p[0].CompareTo(q[0]));
            var merged = new List<double[]>();
            foreach (var s in raw)
            {
                if (merged.Count > 0 && s[0] <= merged[merged.Count - 1][1] + 1e-6)
                    merged[merged.Count - 1][1] = Math.Max(merged[merged.Count - 1][1], s[1]);
                else
                    merged.Add(new[] { s[0], s[1] });
            }
            return merged;
        }

        public double CoveredDistance(IList<WorldPoint> polyline)
        {
            double sum = 0;
            foreach (var s in SafeSpans(polyline)) sum += s[1] - s[0];
            return sum;
        }

        /// <summary>Covered share of the polyline's length, 0..1; 0 for an empty or zero-length line.</summary>
        public double CoveredFraction(IList<WorldPoint> polyline)
        {
            double len = LengthOf(polyline);
            return len <= 0 ? 0 : Math.Min(1.0, CoveredDistance(polyline) / len);
        }

        /// <summary>True when every stretch of the route lies inside the union; a route with no length is safe only when its point is.</summary>
        public bool IsRouteFullySafe(IList<WorldPoint> polyline)
        {
            if (polyline == null || polyline.Count == 0) return false;
            double len = LengthOf(polyline);
            if (len <= 0) return Contains(polyline[0].X, polyline[0].Y);
            return len - CoveredDistance(polyline) <= 1e-6;
        }

        /// <summary>The uncovered distance of a step from <paramref name="d0"/> to <paramref name="d1"/> along the polyline:
        /// what <see cref="DangerState.Rise"/> should receive as its distance. The step is clamped to the line.</summary>
        public double UncoveredDistance(IList<WorldPoint> polyline, double d0, double d1)
        {
            return UncoveredDistance(SafeSpans(polyline), LengthOf(polyline), d0, d1);
        }

        /// <summary>As above for spans already computed, so a caller stepping every frame computes them once.</summary>
        public static double UncoveredDistance(IList<double[]> spans, double length, double d0, double d1)
        {
            double lo = Math.Max(0, Math.Min(d0, d1));
            double hi = Math.Min(length, Math.Max(d0, d1));
            if (hi <= lo) return 0;
            double covered = 0;
            foreach (var s in spans)
            {
                double a = Math.Max(lo, s[0]), b = Math.Min(hi, s[1]);
                if (b > a) covered += b - a;
            }
            return Math.Max(0, (hi - lo) - covered);
        }

        static double Seg(WorldPoint a, WorldPoint b)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        static bool Clip(WorldPoint a, WorldPoint b, double[] c, out double t0, out double t1)
        {
            t0 = t1 = 0;
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double fx = a.X - c[0], fy = a.Y - c[1];
            double qa = dx * dx + dy * dy;
            double qb = 2 * (fx * dx + fy * dy);
            double qc = fx * fx + fy * fy - c[2] * c[2];
            double disc = qb * qb - 4 * qa * qc;
            if (disc < 0) return false;
            double root = Math.Sqrt(disc);
            double lo = (-qb - root) / (2 * qa), hi = (-qb + root) / (2 * qa);
            t0 = Math.Max(0, lo);
            t1 = Math.Min(1, hi);
            return t1 >= t0;
        }
    }
}
