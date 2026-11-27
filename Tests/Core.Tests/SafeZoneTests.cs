using System.Collections.Generic;
using GridGame.Core.Content;
using GridGame.Core.World;
using NUnit.Framework;

namespace GridGame.Tests
{
    /// <summary>Safe circles (GG-T1685, 28.4, 28.8): camp and woken waystone circles form one safe union along a road.</summary>
    public class SafeZoneTests
    {
        static List<WorldPoint> Road(float x0, float x1) => new List<WorldPoint>
        {
            new WorldPoint { X = x0, Y = 0 }, new WorldPoint { X = x1, Y = 0 }
        };

        static WaystoneCircle Stone(string id, float x, float r) => new WaystoneCircle(id, x, 0, r);

        [Test]
        public void Contains_Is_The_Union_With_A_Gap()
        {
            var z = new SafeZones(null, new[] { Stone("a", 0, 10), Stone("b", 15, 10), Stone("c", 100, 10) });
            Assert.That(z.Contains(5, 0), Is.True);
            Assert.That(z.Contains(12, 0), Is.True);
            Assert.That(z.Contains(50, 0), Is.False);
            Assert.That(z.Contains(95, 0), Is.True);
        }

        [Test]
        public void Camp_Circle_Counts()
        {
            var z = new SafeZones(new CampSite { X = 0, Y = 0, Radius = 20 }, null);
            Assert.That(z.Contains(10, 10), Is.True);
            Assert.That(z.Contains(30, 0), Is.False);
        }

        [Test]
        public void Overlapping_Circles_Make_One_Span()
        {
            var z = new SafeZones(null, new[] { Stone("a", 0, 10), Stone("b", 15, 10) });
            var spans = z.SafeSpans(Road(-20, 40));
            Assert.That(spans.Count, Is.EqualTo(1));
            Assert.That(spans[0][0], Is.EqualTo(10).Within(1e-6));
            Assert.That(spans[0][1], Is.EqualTo(35).Within(1e-6));
        }

        [Test]
        public void Gap_Between_Circles_Leaves_Uncovered_Stretch()
        {
            var z = new SafeZones(null, new[] { Stone("a", 0, 10), Stone("b", 50, 10) });
            var road = Road(-10, 60);
            var spans = z.SafeSpans(road);
            Assert.That(spans.Count, Is.EqualTo(2));
            Assert.That(z.CoveredDistance(road), Is.EqualTo(40).Within(1e-6));
            Assert.That(z.CoveredFraction(road), Is.EqualTo(40.0 / 70).Within(1e-6));
            Assert.That(z.IsRouteFullySafe(road), Is.False);
        }

        [Test]
        public void Route_Inside_One_Corridor_Is_Fully_Safe()
        {
            var z = new SafeZones(null, new[] { Stone("a", 0, 10), Stone("b", 15, 10) });
            Assert.That(z.IsRouteFullySafe(Road(-5, 20)), Is.True);
            Assert.That(z.CoveredFraction(Road(-5, 20)), Is.EqualTo(1).Within(1e-6));
        }

        [Test]
        public void Uncovered_Distance_For_A_Step_Entering_A_Circle()
        {
            var z = new SafeZones(null, new[] { Stone("a", 50, 10) });
            var road = Road(0, 100);
            Assert.That(z.UncoveredDistance(road, 20, 45), Is.EqualTo(25).Within(1e-6));
            Assert.That(z.UncoveredDistance(road, 30, 60), Is.EqualTo(20).Within(1e-6));
            Assert.That(z.UncoveredDistance(road, 42, 58), Is.EqualTo(0).Within(1e-6));
            Assert.That(z.UncoveredDistance(road, 90, 150), Is.EqualTo(10).Within(1e-6));
        }

        [Test]
        public void Empty_Zones_Cover_Nothing()
        {
            var z = new SafeZones(null, null);
            var road = Road(0, 100);
            Assert.That(z.Contains(0, 0), Is.False);
            Assert.That(z.SafeSpans(road), Is.Empty);
            Assert.That(z.CoveredFraction(road), Is.EqualTo(0));
            Assert.That(z.IsRouteFullySafe(road), Is.False);
            Assert.That(z.UncoveredDistance(road, 10, 40), Is.EqualTo(30).Within(1e-6));
        }

        [Test]
        public void Spans_Run_Across_Polyline_Corners()
        {
            var z = new SafeZones(null, new[] { Stone("a", 10, 5) });
            var line = new List<WorldPoint>
            {
                new WorldPoint { X = 0, Y = 0 }, new WorldPoint { X = 10, Y = 0 }, new WorldPoint { X = 10, Y = 20 }
            };
            var spans = z.SafeSpans(line);
            Assert.That(spans.Count, Is.EqualTo(1));
            Assert.That(spans[0][0], Is.EqualTo(5).Within(1e-6));
            Assert.That(spans[0][1], Is.EqualTo(15).Within(1e-6));
        }
    }
}
