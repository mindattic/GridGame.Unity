using System.Collections.Generic;
using GridGame.Core.Content;
using GridGame.Core.World;
using NUnit.Framework;

namespace GridGame.Tests
{
    /// <summary>The caravan walking a route by world time (GG-T1681, BIBLE 28.3): polylines, legs, direction, night, arrival.</summary>
    public class TravelTests
    {
        static NodeDef Node(string id, float x, float y) => new NodeDef { Id = id, Kind = NodeKind.Camp, X = x, Y = y };

        static EdgeDef Edge(string a, string b, params float[] xy)
        {
            var e = new EdgeDef { A = a, B = b };
            for (int i = 0; i + 1 < xy.Length; i += 2) e.Points.Add(new WorldPoint { X = xy[i], Y = xy[i + 1] });
            return e;
        }

        static RegionDef Region(IEnumerable<NodeDef> nodes, IEnumerable<EdgeDef> edges)
        {
            var r = new RegionDef { Id = "test", Width = 3000, Height = 3000, Tiles = 256 };
            r.Nodes.AddRange(nodes);
            r.Edges.AddRange(edges);
            return r;
        }

        static TravelState Walk(RegionDef region, string from, string to) => new TravelState(new WorldGraph(region).Find(from, to));

        static readonly TravelConfig OnePixel = new TravelConfig { PixelsPerMinute = 1 };

        static RegionDef Straight() => Region(new[] { Node("A", 0, 0), Node("B", 300, 0) }, new[] { Edge("A", "B") });

        // A to B along an L: right 100, then down 100.
        static RegionDef Bent() => Region(new[] { Node("A", 0, 0), Node("B", 100, 100) }, new[] { Edge("A", "B", 0, 0, 100, 0, 100, 100) });

        static void AssertAt(WorldPoint p, float x, float y)
        {
            Assert.That(p.X, Is.EqualTo(x).Within(0.001f));
            Assert.That(p.Y, Is.EqualTo(y).Within(0.001f));
        }

        [Test]
        public void Constant_speed_along_a_straight_road()
        {
            var state = Walk(Straight(), "A", "B");
            var clock = new WorldClock();
            long start = clock.Minutes;

            var step = Travel.Advance(state, clock, 100);

            Assert.That(step.Minutes, Is.EqualTo(100));
            Assert.That(clock.Minutes - start, Is.EqualTo(100));
            Assert.That(step.Distance, Is.EqualTo(150).Within(1e-9));
            AssertAt(state.Position, 150, 0);
            Assert.That(state.RemainingLength, Is.EqualTo(150).Within(1e-9));
            Assert.That(state.NextNode, Is.EqualTo("B"));
            Assert.That(state.Arrived, Is.False);
        }

        [Test]
        public void Constant_speed_around_a_bend()
        {
            var state = Walk(Bent(), "A", "B");
            var clock = new WorldClock();

            Travel.Advance(state, clock, 50, OnePixel);
            AssertAt(state.Position, 50, 0);
            Travel.Advance(state, clock, 100, OnePixel);
            AssertAt(state.Position, 100, 50);
            AssertAt(state.PositionAt(100), 100, 0);
            Assert.That(state.DistanceTravelled, Is.EqualTo(150).Within(1e-9));
        }

        [Test]
        public void Crosses_from_one_leg_to_the_next()
        {
            var region = Region(new[] { Node("A", 0, 0), Node("B", 100, 0), Node("C", 100, 100) }, new[] { Edge("A", "B"), Edge("B", "C") });
            var state = Walk(region, "A", "C");

            var step = Travel.Advance(state, new WorldClock(), 150, OnePixel);

            Assert.That(step.Passed, Is.EqualTo(new[] { "B" }));
            Assert.That(step.ArrivedAt, Is.Null);
            Assert.That(state.LegIndex, Is.EqualTo(1));
            Assert.That(state.DistanceIntoLeg, Is.EqualTo(50).Within(1e-9));
            Assert.That(state.NextNode, Is.EqualTo("C"));
            AssertAt(state.Position, 100, 50);
        }

        [Test]
        public void A_reversed_leg_walks_its_polyline_backwards()
        {
            var state = Walk(Bent(), "B", "A");
            Assert.That(state.Route.Edges[0].Reversed, Is.True);

            Travel.Advance(state, new WorldClock(), 50, OnePixel);

            AssertAt(state.Position, 100, 50);
            Assert.That(state.NextNode, Is.EqualTo("A"));
        }

        [Test]
        public void Night_is_slower_than_day_for_the_same_minutes()
        {
            var day = Walk(Straight(), "A", "B");
            var night = Walk(Straight(), "A", "B");
            var dayClock = new WorldClock(new WorldTimeConfig { StartHour = 9, Periods = WorldTimeConfig.Default().Periods });
            var nightClock = new WorldClock(new WorldTimeConfig { StartHour = 22, Periods = WorldTimeConfig.Default().Periods });
            Assert.That(nightClock.Period.Name, Is.EqualTo("Night"));

            var byDay = Travel.Advance(day, dayClock, 60);
            var byNight = Travel.Advance(night, nightClock, 60);

            Assert.That(byNight.Minutes, Is.EqualTo(byDay.Minutes));
            Assert.That(byNight.Distance, Is.LessThan(byDay.Distance));
            Assert.That(byNight.Distance, Is.EqualTo(byDay.Distance * TravelConfig.Default().NightMultiplier).Within(1e-9));
        }

        [Test]
        public void The_weather_hook_scales_speed()
        {
            var state = Walk(Straight(), "A", "B");
            var config = new TravelConfig { PixelsPerMinute = 1, WeatherMultiplier = () => 0.5 };

            var step = Travel.Advance(state, new WorldClock(), 100, config);

            Assert.That(step.Distance, Is.EqualTo(50).Within(1e-9));
        }

        [Test]
        public void Arrives_at_the_end_and_clamps_past_it()
        {
            var state = Walk(Straight(), "A", "B");
            var clock = new WorldClock();
            long start = clock.Minutes;

            var step = Travel.Advance(state, clock, 1000, OnePixel);

            Assert.That(step.ArrivedAt, Is.EqualTo("B"));
            Assert.That(state.Arrived, Is.True);
            Assert.That(step.Minutes, Is.EqualTo(300));
            Assert.That(clock.Minutes - start, Is.EqualTo(300));
            Assert.That(step.Distance, Is.EqualTo(300).Within(1e-9));
            AssertAt(state.Position, 300, 0);
            Assert.That(state.RemainingLength, Is.EqualTo(0));
            AssertAt(state.PositionAt(5000), 300, 0);

            var again = Travel.Advance(state, clock, 60, OnePixel);
            Assert.That(again.Minutes, Is.EqualTo(0));
            Assert.That(again.ArrivedAt, Is.Null);
            Assert.That(clock.Minutes - start, Is.EqualTo(300));
        }
    }
}
