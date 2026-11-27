using System;
using System.Collections.Generic;
using GridGame.Core.Content;

namespace GridGame.Core.World
{
    /// <summary>How fast the caravan moves (BIBLE 28.3). The defaults walk a typical 1000px leg of a ~3000px region map in
    /// about eleven daylight hours, most of a day.</summary>
    public sealed class TravelConfig
    {
        /// <summary>Art pixels per world minute by day.</summary>
        public double PixelsPerMinute = 1.5;

        /// <summary>Speed is multiplied by this while the clock is in <see cref="NightPeriod"/>.</summary>
        public double NightMultiplier = 0.5;

        public string NightPeriod = "Night";

        /// <summary>The weather's speed multiplier now (GG-T1690); null is clear weather, 1.</summary>
        public Func<double> WeatherMultiplier;

        public static TravelConfig Default() => new TravelConfig();

        /// <summary>Art pixels per minute at the clock's current period and weather; never negative.</summary>
        public double SpeedAt(WorldClock clock)
        {
            double speed = PixelsPerMinute;
            if (clock != null && string.Equals(clock.Period.Name, NightPeriod, StringComparison.OrdinalIgnoreCase)) speed *= NightMultiplier;
            if (WeatherMultiplier != null) speed *= WeatherMultiplier();
            return Math.Max(0, speed);
        }
    }

    /// <summary>The caravan on a <see cref="Route"/>: which leg it is on and how far along it, in the leg's travel length
    /// (<see cref="WorldContent.LengthOf"/>, so an authored <see cref="EdgeDef.Length"/> stretches or shrinks the walk along
    /// the drawn polyline).</summary>
    public sealed class TravelState
    {
        public readonly Route Route;
        public int LegIndex;
        public double DistanceIntoLeg;
        public bool Arrived;

        readonly WorldPoint[][] legPoints;
        readonly double[][] legSegments;
        readonly double[] legDrawn;
        readonly double[] legLength;

        public TravelState(Route route)
        {
            Route = route ?? throw new ArgumentNullException(nameof(route));
            var points = route.Points ?? new WorldPoint[0];
            var legs = route.Edges ?? new RouteLeg[0];
            legPoints = new WorldPoint[legs.Length][];
            legSegments = new double[legs.Length][];
            legDrawn = new double[legs.Length];
            legLength = new double[legs.Length];
            int start = 0;
            for (int i = 0; i < legs.Length; i++)
            {
                var edgePoints = legs[i].Edge.Points;
                int count = edgePoints != null && edgePoints.Count > 0 ? edgePoints.Count : 2;
                int first = Math.Min(start, Math.Max(points.Length - 1, 0));
                int last = Math.Min(first + count - 1, points.Length - 1);
                var pts = new List<WorldPoint>();
                for (int p = first; p <= last; p++) pts.Add(points[p]);
                legPoints[i] = pts.ToArray();
                legSegments[i] = new double[Math.Max(pts.Count - 1, 0)];
                for (int s = 1; s < pts.Count; s++)
                {
                    double d = Distance(pts[s - 1], pts[s]);
                    legSegments[i][s - 1] = d;
                    legDrawn[i] += d;
                }
                legLength[i] = legs[i].Edge.Length > 0 ? legs[i].Edge.Length : legDrawn[i];
                start = Math.Max(last, first);
            }
            Arrived = legs.Length == 0;
        }

        public int LegCount => legLength.Length;

        /// <summary>The whole route's travel length.</summary>
        public double TotalLength
        {
            get
            {
                double total = 0;
                foreach (var l in legLength) total += l;
                return total;
            }
        }

        public double LengthOfLeg(int leg) => legLength[leg];

        /// <summary>How far the caravan has come from the route's first node.</summary>
        public double DistanceTravelled
        {
            get
            {
                if (LegCount == 0) return 0;
                double d = 0;
                for (int i = 0; i < LegIndex; i++) d += legLength[i];
                return d + DistanceIntoLeg;
            }
        }

        public double RemainingLength => Math.Max(0, TotalLength - DistanceTravelled);

        /// <summary>The node the caravan is walking toward; the last node once it has arrived.</summary>
        public string NextNode
        {
            get
            {
                if (LegCount == 0) return Route.Nodes != null && Route.Nodes.Length > 0 ? Route.Nodes[0] : null;
                return Route.Edges[LegIndex].To;
            }
        }

        public WorldPoint Position => LegCount == 0 ? StartPoint() : PointOnLeg(LegIndex, DistanceIntoLeg);

        /// <summary>The point on the route's polyline at a travel distance from its first node, clamped to the route.</summary>
        public WorldPoint PositionAt(double distance)
        {
            if (LegCount == 0) return StartPoint();
            distance = Math.Max(0, distance);
            for (int i = 0; i < LegCount; i++)
            {
                if (distance <= legLength[i] || i == LegCount - 1) return PointOnLeg(i, Math.Min(distance, legLength[i]));
                distance -= legLength[i];
            }
            return PointOnLeg(LegCount - 1, legLength[LegCount - 1]);
        }

        WorldPoint StartPoint()
        {
            var pts = Route.Points;
            return pts != null && pts.Length > 0 ? new WorldPoint { X = pts[0].X, Y = pts[0].Y } : new WorldPoint();
        }

        WorldPoint PointOnLeg(int leg, double distance)
        {
            var pts = legPoints[leg];
            if (pts.Length == 0) return new WorldPoint();
            if (pts.Length == 1 || legDrawn[leg] <= 0) return new WorldPoint { X = pts[0].X, Y = pts[0].Y };
            double along = legLength[leg] > 0 ? distance / legLength[leg] * legDrawn[leg] : 0;
            along = Math.Min(Math.Max(along, 0), legDrawn[leg]);
            var segments = legSegments[leg];
            for (int s = 0; s < segments.Length; s++)
            {
                if (along > segments[s] && s < segments.Length - 1) { along -= segments[s]; continue; }
                double t = segments[s] > 0 ? Math.Min(along / segments[s], 1) : 1;
                var a = pts[s];
                var b = pts[s + 1];
                return new WorldPoint { X = (float)(a.X + (b.X - a.X) * t), Y = (float)(a.Y + (b.Y - a.Y) * t) };
            }
            var end = pts[pts.Length - 1];
            return new WorldPoint { X = end.X, Y = end.Y };
        }

        static double Distance(WorldPoint a, WorldPoint b)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }

    /// <summary>What one <see cref="Travel.Advance"/> did.</summary>
    public sealed class TravelStep
    {
        /// <summary>World minutes spent on the road; the clock moved on by this much.</summary>
        public long Minutes;
        /// <summary>Travel distance covered.</summary>
        public double Distance;
        /// <summary>Nodes passed through on the way, in order, not counting the destination.</summary>
        public List<string> Passed = new List<string>();
        /// <summary>The route's last node when this step reached it, else null.</summary>
        public string ArrivedAt;
    }

    /// <summary>Moves the caravan along its route by elapsed world time (BIBLE 28.3, GG-T1681). Speed is set minute by
    /// minute from the clock's period and the weather, so the same state, clock and config always give the same walk.
    /// Stopping at dusk, Interrupt and roadsigns are the caller's (GG-T1682).</summary>
    public static class Travel
    {
        /// <summary>Walks up to <paramref name="minutes"/> world minutes, advancing <paramref name="clock"/> by the whole
        /// minutes actually travelled (the minute in which the caravan arrives counts in full). Nothing moves once arrived.</summary>
        public static TravelStep Advance(TravelState state, WorldClock clock, long minutes, TravelConfig config = null)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (clock == null) throw new ArgumentNullException(nameof(clock));
            config = config ?? TravelConfig.Default();
            var step = new TravelStep();
            while (!state.Arrived && step.Minutes < minutes)
            {
                double move = config.SpeedAt(clock);
                clock.Advance(1);
                step.Minutes++;
                while (move > 0 && !state.Arrived)
                {
                    double left = state.LengthOfLeg(state.LegIndex) - state.DistanceIntoLeg;
                    if (move < left)
                    {
                        state.DistanceIntoLeg += move;
                        step.Distance += move;
                        break;
                    }
                    move -= left;
                    step.Distance += left;
                    if (state.LegIndex < state.LegCount - 1)
                    {
                        step.Passed.Add(state.Route.Edges[state.LegIndex].To);
                        state.LegIndex++;
                        state.DistanceIntoLeg = 0;
                    }
                    else
                    {
                        state.DistanceIntoLeg = state.LengthOfLeg(state.LegIndex);
                        state.Arrived = true;
                        step.ArrivedAt = state.NextNode;
                    }
                }
            }
            return step;
        }
    }
}
