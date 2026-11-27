using System;
using System.Collections.Generic;
using GridGame.Core.Content;

namespace GridGame.Core.World
{
    /// <summary>One edge walked in a <see cref="Route"/>; <see cref="Reversed"/> when it is walked from B to A.</summary>
    public readonly struct RouteLeg
    {
        public readonly EdgeDef Edge;
        public readonly bool Reversed;
        public RouteLeg(EdgeDef edge, bool reversed) { Edge = edge; Reversed = reversed; }
        public string From => Reversed ? Edge.B : Edge.A;
        public string To => Reversed ? Edge.A : Edge.B;
    }

    /// <summary>A walk through a region's graph (GG-T1676): the node ids visited, the edges taken in the direction taken,
    /// and the whole road as one polyline in art pixels.</summary>
    public sealed class Route
    {
        public string[] Nodes;
        public RouteLeg[] Edges;
        /// <summary>The sum of the edges' travel lengths (<see cref="WorldContent.LengthOf"/>).</summary>
        public float TotalLength;
        /// <summary>The sum of the edges' costs, which is what the route minimizes.</summary>
        public double TotalCost;
        public WorldPoint[] Points;
    }

    /// <summary>Dijkstra over a region's authored graph (BIBLE 28.2): roads are undirected, locked ones are skipped, and
    /// ties break by node order, then edge order, so the same input always gives the same route.</summary>
    public sealed class WorldGraph
    {
        readonly RegionDef region;
        readonly List<string> ids = new List<string>();
        readonly Dictionary<string, int> index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        readonly List<(int To, EdgeDef Edge, bool Reversed, double Cost)>[] adjacent;

        /// <summary>An edge is open when it has no RevealedBy, or its RevealedBy is a woken waystone or a cleared stage.</summary>
        public static Func<EdgeDef, bool> Unlocked(IEnumerable<string> wokenWaystones, IEnumerable<string> clearedStages)
        {
            var opened = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (wokenWaystones != null) foreach (var w in wokenWaystones) opened.Add(w);
            if (clearedStages != null) foreach (var s in clearedStages) opened.Add(s);
            return e => e.RevealedBy == null || opened.Contains(e.RevealedBy);
        }

        /// <param name="unlocked">Which edges can be walked; null means only those with no RevealedBy.</param>
        /// <param name="weight">An edge's cost; null means its length, <see cref="WorldContent.LengthOf"/>.</param>
        public WorldGraph(RegionDef region, Func<EdgeDef, bool> unlocked = null, Func<EdgeDef, double> weight = null)
        {
            this.region = region ?? throw new ArgumentNullException(nameof(region));
            unlocked = unlocked ?? (e => e.RevealedBy == null);
            weight = weight ?? (e => WorldContent.LengthOf(region, e));
            foreach (var n in region.Nodes) if (n != null && n.Id != null) Add(n.Id);
            foreach (var e in region.Edges)
            {
                if (e == null || e.A == null || e.B == null) continue;
                Add(e.A);
                Add(e.B);
            }
            adjacent = new List<(int, EdgeDef, bool, double)>[ids.Count];
            for (int i = 0; i < adjacent.Length; i++) adjacent[i] = new List<(int, EdgeDef, bool, double)>();
            foreach (var e in region.Edges)
            {
                if (e == null || e.A == null || e.B == null || !unlocked(e)) continue;
                double cost = Math.Max(0, weight(e));
                adjacent[index[e.A]].Add((index[e.B], e, false, cost));
                adjacent[index[e.B]].Add((index[e.A], e, true, cost));
            }
        }

        void Add(string id)
        {
            if (index.ContainsKey(id)) return;
            index[id] = ids.Count;
            ids.Add(id);
        }

        /// <summary>The cheapest route between two nodes over unlocked edges, or null when there is none or a node is unknown.
        /// From a node to itself is an empty route. Throws when a road on the route names a node the region doesn't have,
        /// since its polyline can't be placed (GG-T1682).</summary>
        public Route Find(string fromNodeId, string toNodeId)
        {
            if (fromNodeId == null || toNodeId == null) return null;
            if (!index.TryGetValue(fromNodeId, out int from) || !index.TryGetValue(toNodeId, out int to)) return null;

            int n = ids.Count;
            var dist = new double[n];
            var done = new bool[n];
            var prev = new int[n];
            var prevLeg = new (EdgeDef Edge, bool Reversed)[n];
            for (int i = 0; i < n; i++) { dist[i] = double.PositiveInfinity; prev[i] = -1; }
            dist[from] = 0;

            while (true)
            {
                int u = -1;
                for (int i = 0; i < n; i++)
                    if (!done[i] && !double.IsPositiveInfinity(dist[i]) && (u < 0 || dist[i] < dist[u])) u = i;
                if (u < 0 || u == to) break;
                done[u] = true;
                foreach (var (v, edge, reversed, cost) in adjacent[u])
                {
                    if (done[v]) continue;
                    double d = dist[u] + cost;
                    if (d < dist[v]) { dist[v] = d; prev[v] = u; prevLeg[v] = (edge, reversed); }
                }
            }
            if (double.IsPositiveInfinity(dist[to])) return null;

            var legs = new List<RouteLeg>();
            var nodes = new List<string>();
            for (int at = to; at != from; at = prev[at])
            {
                nodes.Add(ids[at]);
                legs.Add(new RouteLeg(prevLeg[at].Edge, prevLeg[at].Reversed));
            }
            nodes.Add(ids[from]);
            nodes.Reverse();
            legs.Reverse();

            float length = 0;
            var points = new List<WorldPoint>();
            foreach (var leg in legs)
            {
                foreach (var end in new[] { leg.Edge.A, leg.Edge.B })
                    if (region.TryNode(end) == null)
                        throw new InvalidOperationException($"Region '{region.Id}': the road {leg.Edge.A} - {leg.Edge.B} names node '{end}', which is not in the region.");
                length += WorldContent.LengthOf(region, leg.Edge);
                var pts = LegPoints(leg);
                for (int i = 0; i < pts.Count; i++)
                {
                    if (i == 0 && points.Count > 0) continue;
                    points.Add(pts[i]);
                }
            }
            if (legs.Count == 0)
            {
                var node = region.TryNode(ids[from]);
                if (node != null) points.Add(new WorldPoint { X = node.X, Y = node.Y });
            }
            return new Route { Nodes = nodes.ToArray(), Edges = legs.ToArray(), TotalLength = length, TotalCost = dist[to], Points = points.ToArray() };
        }

        List<WorldPoint> LegPoints(RouteLeg leg)
        {
            var pts = new List<WorldPoint>();
            if (leg.Edge.Points != null && leg.Edge.Points.Count > 0) pts.AddRange(leg.Edge.Points);
            else
            {
                var a = region.TryNode(leg.Edge.A);
                var b = region.TryNode(leg.Edge.B);
                if (a != null) pts.Add(new WorldPoint { X = a.X, Y = a.Y });
                if (b != null) pts.Add(new WorldPoint { X = b.X, Y = b.Y });
            }
            if (leg.Reversed) pts.Reverse();
            return pts;
        }
    }
}
