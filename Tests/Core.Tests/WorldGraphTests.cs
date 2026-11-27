using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Content;
using GridGame.Core.World;
using NUnit.Framework;

namespace GridGame.Tests
{
    /// <summary>Pathfinding over a region's graph (GG-T1676, BIBLE 28.2): cheapest route, locked roads, polylines, bridges.</summary>
    public class WorldGraphTests
    {
        static NodeDef Node(string id, float x, float y, NodeKind kind = NodeKind.Camp) => new NodeDef { Id = id, Kind = kind, X = x, Y = y };

        static EdgeDef Edge(string a, string b, float length = 0, string revealedBy = null, params float[] xy)
        {
            var e = new EdgeDef { A = a, B = b, Length = length, RevealedBy = revealedBy };
            for (int i = 0; i + 1 < xy.Length; i += 2) e.Points.Add(new WorldPoint { X = xy[i], Y = xy[i + 1] });
            return e;
        }

        static RegionDef Region(IEnumerable<NodeDef> nodes, IEnumerable<EdgeDef> edges)
        {
            var r = new RegionDef { Id = "test", Width = 1000, Height = 1000, Tiles = 256 };
            r.Nodes.AddRange(nodes);
            r.Edges.AddRange(edges);
            return r;
        }

        // A - B - D (short, 10 + 10) and A - C - D (long, 30 + 30).
        static RegionDef Diamond(string shortRevealedBy = null) => Region(
            new[] { Node("A", 0, 0), Node("B", 10, 0), Node("C", 0, 30), Node("D", 20, 0) },
            new[] { Edge("A", "B", 10, shortRevealedBy), Edge("B", "D", 10), Edge("A", "C", 30), Edge("C", "D", 30) });

        [Test]
        public void Takes_the_shorter_of_two_routes()
        {
            var route = new WorldGraph(Diamond()).Find("A", "D");
            Assert.That(route.Nodes, Is.EqualTo(new[] { "A", "B", "D" }));
            Assert.That(route.TotalLength, Is.EqualTo(20f));
            Assert.That(route.Edges.Length, Is.EqualTo(2));
        }

        [Test]
        public void A_locked_edge_forces_the_detour_until_its_waystone_wakes()
        {
            var region = Diamond("W");
            Assert.That(new WorldGraph(region).Find("A", "D").Nodes, Is.EqualTo(new[] { "A", "C", "D" }));
            var woken = new WorldGraph(region, WorldGraph.Unlocked(new[] { "W" }, null)).Find("A", "D");
            Assert.That(woken.Nodes, Is.EqualTo(new[] { "A", "B", "D" }));
            var cleared = new WorldGraph(region, WorldGraph.Unlocked(null, new[] { "w" })).Find("A", "D");
            Assert.That(cleared.Nodes, Is.EqualTo(new[] { "A", "B", "D" }));
        }

        [Test]
        public void Unreachable_or_unknown_nodes_give_null()
        {
            var region = Region(new[] { Node("A", 0, 0), Node("B", 10, 0), Node("Island", 500, 500) }, new[] { Edge("A", "B", 10) });
            var graph = new WorldGraph(region);
            Assert.That(graph.Find("A", "Island"), Is.Null);
            Assert.That(graph.Find("A", "Nowhere"), Is.Null);
            Assert.That(new WorldGraph(Region(new[] { Node("A", 0, 0), Node("B", 10, 0) }, new[] { Edge("A", "B", 10, "W") })).Find("A", "B"), Is.Null);
        }

        [Test]
        public void A_route_to_the_same_node_is_empty()
        {
            var route = new WorldGraph(Diamond()).Find("A", "A");
            Assert.That(route.Nodes, Is.EqualTo(new[] { "A" }));
            Assert.That(route.Edges, Is.Empty);
            Assert.That(route.TotalLength, Is.EqualTo(0f));
            Assert.That(route.Points.Length, Is.EqualTo(1));
        }

        [Test]
        public void A_reversed_edge_is_walked_backwards_with_its_polyline_reversed()
        {
            var region = Region(
                new[] { Node("A", 0, 0), Node("B", 100, 0), Node("C", 100, 100) },
                new[] { Edge("A", "B", 0, null, 0, 0, 50, 20, 100, 0), Edge("C", "B", 0, null, 100, 100, 100, 0) });
            var route = new WorldGraph(region).Find("B", "A");
            Assert.That(route.Edges[0].Reversed, Is.True);
            Assert.That(route.Edges[0].From, Is.EqualTo("B"));
            Assert.That(route.Edges[0].To, Is.EqualTo("A"));
            Assert.That(route.Points.Select(p => (p.X, p.Y)), Is.EqualTo(new[] { (100f, 0f), (50f, 20f), (0f, 0f) }));

            var across = new WorldGraph(region).Find("A", "C");
            Assert.That(across.Edges.Select(e => e.Reversed), Is.EqualTo(new[] { false, true }));
            Assert.That(across.Points.Select(p => (p.X, p.Y)), Is.EqualTo(new[] { (0f, 0f), (50f, 20f), (100f, 0f), (100f, 100f) }));
            Assert.That(across.TotalLength, Is.EqualTo(across.Edges.Sum(e => WorldContent.LengthOf(region, e.Edge))).Within(1e-3f));
        }

        [Test]
        public void A_river_is_crossed_only_where_a_bridge_node_has_edges()
        {
            // West bank (W1, W2) and east bank (E1, E2); the only road across runs W2 - Bridge - E1.
            var region = Region(
                new[] { Node("W1", 0, 0), Node("W2", 100, 0), Node("Bridge", 200, 0, NodeKind.Bridge), Node("E1", 300, 0), Node("E2", 400, 0) },
                new[] { Edge("W1", "W2"), Edge("W2", "Bridge"), Edge("Bridge", "E1"), Edge("E1", "E2") });
            var route = new WorldGraph(region).Find("W1", "E2");
            Assert.That(route.Nodes, Is.EqualTo(new[] { "W1", "W2", "Bridge", "E1", "E2" }));

            region.Edges.RemoveAll(e => e.A == "Bridge" || e.B == "Bridge");
            Assert.That(new WorldGraph(region).Find("W1", "E2"), Is.Null);
        }

        [Test]
        public void Equal_routes_break_ties_by_edge_order_and_repeat_exactly()
        {
            var region = Region(
                new[] { Node("A", 0, 0), Node("B", 10, 0), Node("C", 10, 10), Node("D", 20, 0) },
                new[] { Edge("A", "B", 10), Edge("B", "D", 10), Edge("A", "C", 10), Edge("C", "D", 10) });
            var first = new WorldGraph(region).Find("A", "D");
            Assert.That(first.Nodes, Is.EqualTo(new[] { "A", "B", "D" }));
            for (int i = 0; i < 5; i++)
            {
                var again = new WorldGraph(region).Find("A", "D");
                Assert.That(again.Nodes, Is.EqualTo(first.Nodes));
                Assert.That(again.Edges.Select(e => e.Edge), Is.EqualTo(first.Edges.Select(e => e.Edge)));
                Assert.That(again.Points.Select(p => (p.X, p.Y)), Is.EqualTo(first.Points.Select(p => (p.X, p.Y))));
            }
        }

        [Test]
        public void A_weight_hook_changes_which_route_is_cheapest()
        {
            var region = Diamond();
            var route = new WorldGraph(region, null, e => (e.A == "A" && e.B == "B") ? 1000 : WorldContent.LengthOf(region, e)).Find("A", "D");
            Assert.That(route.Nodes, Is.EqualTo(new[] { "A", "C", "D" }));
            Assert.That(route.TotalLength, Is.EqualTo(60f));
        }
    }
}
