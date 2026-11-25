using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Content;

namespace GridGame.Core.Rules
{
    /// <summary>The skill hexagon at runtime (GG-T1023, docs/design/skill-hexagon/PLAN.md): lattice adjacency, the
    /// frontier a hero can take next, the cheapest route to a node, the SP a route costs and the stats a hero's owned
    /// passives add up to. Pure functions over a <see cref="SkillHexDef"/>; nothing here changes the board.
    /// Ability and Passive nodes can be taken; an Open slot has nothing on it yet, so it is never taken and no route
    /// passes through it.</summary>
    public sealed class SkillHexBoard
    {
        public readonly SkillHexDef Def;
        private readonly Dictionary<(int Q, int R), SkillHexNodeDef> nodes = new Dictionary<(int, int), SkillHexNodeDef>();
        private readonly Dictionary<string, (int Q, int R)> abilityAt = new Dictionary<string, (int, int)>(StringComparer.OrdinalIgnoreCase);

        public SkillHexBoard(SkillHexDef def)
        {
            Def = def ?? throw new ArgumentNullException(nameof(def));
            foreach (var n in def.Nodes ?? new List<SkillHexNodeDef>())
            {
                if (n == null || !SkillHexDef.OnLattice(n.Q, n.R, def.Radius)) continue;
                nodes[(n.Q, n.R)] = n;
                if (n.Kind == SkillHexNodeKind.Ability && !string.IsNullOrEmpty(n.Ability) && !abilityAt.ContainsKey(n.Ability))
                    abilityAt[n.Ability] = (n.Q, n.R);
            }
        }

        public int Radius => Def.Radius;
        public SkillHexCostsDef Costs => Def.Costs ?? new SkillHexCostsDef();

        /// <summary>Whether (q, r) is a point of this board's lattice.</summary>
        public bool Contains((int Q, int R) pt) => SkillHexDef.OnLattice(pt.Q, pt.R, Radius);

        /// <summary>The node at a point, or null when the point is off the lattice or the file left it out.</summary>
        public SkillHexNodeDef Node((int Q, int R) pt) => nodes.TryGetValue(pt, out var n) ? n : null;

        /// <summary>Where an ability sits, or null when it is not on the board.</summary>
        public (int Q, int R)? FindAbility(string abilityId) =>
            abilityId != null && abilityAt.TryGetValue(abilityId, out var pt) ? pt : ((int, int)?)null;

        /// <summary>A node a hero can own: an Ability or a Passive (not an Open slot, not a missing point).</summary>
        public bool IsTakeable((int Q, int R) pt)
        {
            var n = Node(pt);
            return n != null && (n.Kind == SkillHexNodeKind.Ability || n.Kind == SkillHexNodeKind.Passive);
        }

        /// <summary>The up to six lattice points next to a point, in <see cref="SkillHexDef.Directions"/> order.</summary>
        public IEnumerable<(int Q, int R)> Neighbors((int Q, int R) pt)
        {
            foreach (var d in SkillHexDef.Directions)
            {
                var nb = (pt.Q + d.Q, pt.R + d.R);
                if (Contains(nb)) yield return nb;
            }
        }

        /// <summary>Whether two points are one step apart.</summary>
        public static bool AreAdjacent((int Q, int R) a, (int Q, int R) b)
        {
            int dq = b.Q - a.Q, dr = b.R - a.R;
            foreach (var d in SkillHexDef.Directions)
                if (d.Q == dq && d.R == dr) return true;
            return false;
        }

        /// <summary>Steps between two points on the lattice.</summary>
        public static int Distance((int Q, int R) a, (int Q, int R) b)
        {
            int dq = a.Q - b.Q, dr = a.R - b.R;
            return (Math.Abs(dq) + Math.Abs(dr) + Math.Abs(dq + dr)) / 2;
        }

        /// <summary>The points a hero starts with: its class's Start path (empty when the class is not on the board).</summary>
        public HashSet<(int Q, int R)> StartOf(string classId)
        {
            var set = new HashSet<(int, int)>();
            var hero = (Def.Heroes ?? new List<SkillHexHeroDef>())
                .FirstOrDefault(h => h != null && string.Equals(h.Class, classId, StringComparison.OrdinalIgnoreCase));
            foreach (var c in hero?.Start ?? new List<int[]>())
                if (c != null && c.Length == 2 && Contains((c[0], c[1]))) set.Add((c[0], c[1]));
            return set;
        }

        /// <summary>Whether a hero owning <paramref name="owned"/> can take <paramref name="pt"/> next: a takeable node
        /// it does not own yet, next to one it does.</summary>
        public bool CanTake(IReadOnlyCollection<(int Q, int R)> owned, (int Q, int R) pt) =>
            !owned.Contains(pt) && IsTakeable(pt) && Neighbors(pt).Any(owned.Contains);

        /// <summary>Every node a hero can take next, sorted by (Q, R).</summary>
        public List<(int Q, int R)> Frontier(IReadOnlyCollection<(int Q, int R)> owned)
        {
            var result = new HashSet<(int, int)>();
            foreach (var pt in owned)
                foreach (var nb in Neighbors(pt))
                    if (!owned.Contains(nb) && IsTakeable(nb)) result.Add(nb);
            return result.OrderBy(p => p).ToList();
        }

        /// <summary>SP to take one node: <see cref="SkillHexCostsDef.Passive"/> or <see cref="SkillHexCostsDef.Ability"/>.</summary>
        public int Price((int Q, int R) pt)
        {
            var n = Node(pt);
            if (n == null) return 0;
            return n.Kind == SkillHexNodeKind.Ability ? Costs.Ability : n.Kind == SkillHexNodeKind.Passive ? Costs.Passive : 0;
        }

        /// <summary>SP to take every node of a route (owned nodes in it cost nothing).</summary>
        public int SpCost(IEnumerable<(int Q, int R)> route, IReadOnlyCollection<(int Q, int R)> owned = null) =>
            route.Distinct().Where(pt => owned == null || !owned.Contains(pt)).Sum(Price);

        /// <summary>The planner's weight for stepping onto a node on the way to <paramref name="target"/>: a passive costs
        /// its price, the target and any ability in <paramref name="wanted"/> cost the ability price, and any other ability
        /// costs <see cref="SkillHexCostsDef.PlannerPassThroughAbility"/>, so routes go round abilities nobody asked for.</summary>
        public int StepWeight((int Q, int R) pt, (int Q, int R) target, ICollection<(int Q, int R)> wanted = null)
        {
            var n = Node(pt);
            if (n == null) return 0;
            if (n.Kind != SkillHexNodeKind.Ability) return Costs.Passive;
            return pt.Equals(target) || (wanted != null && wanted.Contains(pt)) ? Costs.Ability : Costs.PlannerPassThroughAbility;
        }

        /// <summary>The cheapest walk from what a hero owns to <paramref name="target"/>, by <see cref="StepWeight"/>: the
        /// nodes to take, in an order where each is next to one already owned or taken before it, ending with the target.
        /// Empty when the target is already owned; null when it can't be reached (not takeable, or nothing owned).
        /// Ties break the same way every time.</summary>
        public List<(int Q, int R)> CheapestRoute(IReadOnlyCollection<(int Q, int R)> owned, (int Q, int R) target,
            ICollection<(int Q, int R)> wanted = null)
        {
            if (owned.Contains(target)) return new List<(int, int)>();
            if (!IsTakeable(target) || owned.Count == 0) return null;

            var dist = new Dictionary<(int, int), int>();
            var prev = new Dictionary<(int, int), (int, int)>();
            var open = new SortedSet<(int D, int Q, int R)>();
            foreach (var pt in owned)
                if (Contains(pt) && dist.TryAdd(pt, 0)) open.Add((0, pt.Q, pt.R));

            while (open.Count > 0)
            {
                var cur = open.Min;
                open.Remove(cur);
                var at = (cur.Q, cur.R);
                if (at.Equals(target)) break;
                foreach (var nb in Neighbors(at))
                {
                    if (owned.Contains(nb) || !IsTakeable(nb)) continue;
                    int d = cur.D + StepWeight(nb, target, wanted);
                    if (dist.TryGetValue(nb, out var old))
                    {
                        if (d >= old) continue;
                        open.Remove((old, nb.Q, nb.R));
                    }
                    dist[nb] = d;
                    prev[nb] = at;
                    open.Add((d, nb.Q, nb.R));
                }
            }
            if (!dist.ContainsKey(target)) return null;

            var route = new List<(int Q, int R)>();
            for (var pt = target; !owned.Contains(pt); pt = prev[pt]) route.Add(pt);
            route.Reverse();
            return route;
        }

        /// <summary>The stats a hero's owned passives add up to, keyed by stat id (only stats with a bonus appear).</summary>
        public Dictionary<string, int> StatTotals(IEnumerable<(int Q, int R)> owned)
        {
            var totals = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var pt in owned.Distinct())
            {
                var n = Node(pt);
                if (n == null || n.Kind != SkillHexNodeKind.Passive || n.Stats == null) continue;
                foreach (var kv in n.Stats)
                    totals[kv.Key] = (totals.TryGetValue(kv.Key, out var v) ? v : 0) + kv.Value;
            }
            return totals;
        }

        /// <summary>The abilities a hero owns, in (Q, R) order.</summary>
        public List<string> OwnedAbilities(IEnumerable<(int Q, int R)> owned) =>
            owned.Distinct().OrderBy(p => p).Select(Node)
                .Where(n => n != null && n.Kind == SkillHexNodeKind.Ability && !string.IsNullOrEmpty(n.Ability))
                .Select(n => n.Ability).ToList();
    }

    /// <summary>The connected set of nodes one hero owns on a <see cref="SkillHexBoard"/>.</summary>
    public sealed class SkillHexOwned
    {
        private readonly HashSet<(int Q, int R)> nodes;

        public SkillHexOwned(IEnumerable<(int Q, int R)> nodes = null) => this.nodes = new HashSet<(int, int)>(nodes ?? Enumerable.Empty<(int, int)>());

        /// <summary>A hero's set as it joins: its class's Start path.</summary>
        public static SkillHexOwned Starting(SkillHexBoard board, string classId) => new SkillHexOwned(board.StartOf(classId));

        public IReadOnlyCollection<(int Q, int R)> Nodes => nodes;
        public int Count => nodes.Count;
        public bool Owns((int Q, int R) pt) => nodes.Contains(pt);

        /// <summary>Takes a node when <see cref="SkillHexBoard.CanTake"/> allows it (the first node of an empty set can
        /// be any takeable one); false and no change otherwise. SP is the caller's to charge.</summary>
        public bool Take(SkillHexBoard board, (int Q, int R) pt)
        {
            if (nodes.Count == 0 ? !board.IsTakeable(pt) : !board.CanTake(nodes, pt)) return false;
            nodes.Add(pt);
            return true;
        }

        /// <summary>Takes a whole route in order; stops at (and returns false on) the first node it can't take.</summary>
        public bool TakeRoute(SkillHexBoard board, IEnumerable<(int Q, int R)> route)
        {
            foreach (var pt in route)
                if (!Take(board, pt)) return false;
            return true;
        }
    }
}
