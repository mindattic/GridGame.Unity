using System;
using System.Collections.Generic;
using GridGame.Core.Content;

namespace GridGame.Core.World
{
    /// <summary>One protective circle of a woken Mana Waystone, in art pixels (BIBLE 28.8).</summary>
    public readonly struct WaystoneCircle
    {
        public readonly string NodeId;
        public readonly float X, Y, Radius;

        public WaystoneCircle(string nodeId, float x, float y, float radius) { NodeId = nodeId; X = x; Y = y; Radius = radius; }

        public bool Contains(double x, double y)
        {
            double dx = x - X, dy = y - Y;
            return dx * dx + dy * dy <= (double)Radius * Radius;
        }
    }

    /// <summary>A region's Mana Waystone state (BIBLE 28.8, GG-T1692): which stones are woken and which guardian battles
    /// are cleared. A dormant stone shows only as a glint once the fog at it lifts; waking needs its guardian cleared and
    /// the party at the node; the woken stones' circles overlap into the safe corridors danger is suppressed by.</summary>
    public sealed class WaystoneState
    {
        public readonly RegionDef Region;
        public readonly HashSet<string> Woken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> GuardianCleared = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public WaystoneState(RegionDef region)
        {
            Region = region ?? throw new ArgumentNullException(nameof(region));
        }

        private NodeDef Stone(string nodeId)
        {
            var n = Region.TryNode(nodeId);
            return n != null && n.Kind == NodeKind.Waystone ? n : null;
        }

        public bool IsWoken(string nodeId) => nodeId != null && Woken.Contains(nodeId);

        public bool IsDormant(string nodeId) => Stone(nodeId) != null && !IsWoken(nodeId);

        /// <summary>A dormant stone glints once the fog at it lifts; a woken one is drawn as itself, not a glint.</summary>
        public bool IsGlintVisible(NodeDef node, Func<double, double, bool> isRevealed)
        {
            if (node == null || isRevealed == null || node.Kind != NodeKind.Waystone || IsWoken(node.Id)) return false;
            return isRevealed(node.X, node.Y);
        }

        public void ClearGuardian(string nodeId)
        {
            var n = Stone(nodeId);
            if (n != null) GuardianCleared.Add(n.Id);
        }

        public bool CanWake(string nodeId, string partyNodeId)
        {
            return IsDormant(nodeId) && GuardianCleared.Contains(nodeId)
                && partyNodeId != null && string.Equals(partyNodeId, nodeId, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Wakes the stone when <see cref="CanWake"/>; true when it is woken afterwards, so waking twice is harmless.</summary>
        public bool Wake(string nodeId, string partyNodeId)
        {
            if (IsWoken(nodeId)) return true;
            if (!CanWake(nodeId, partyNodeId)) return false;
            Woken.Add(Region.TryNode(nodeId).Id);
            return true;
        }

        public List<WaystoneCircle> Circles()
        {
            var list = new List<WaystoneCircle>();
            foreach (var n in Region.Nodes)
                if (n != null && n.Kind == NodeKind.Waystone && n.Radius > 0 && IsWoken(n.Id))
                    list.Add(new WaystoneCircle(n.Id, n.X, n.Y, n.Radius));
            return list;
        }

        /// <summary>True when the point is inside at least one woken circle (the union of the safe corridors).</summary>
        public bool InAnyCircle(double x, double y)
        {
            foreach (var c in Circles())
                if (c.Contains(x, y)) return true;
            return false;
        }
    }
}
