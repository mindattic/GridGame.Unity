using System;
using System.Collections.Generic;
using GridGame.Core.Content;
using GridGame.Core.World;
using NUnit.Framework;

namespace GridGame.Tests
{
    /// <summary>Where travel stops and how roadsigns move the caravan between regions (GG-T1682, BIBLE 28.1, 28.3): dusk and
    /// Continue, Interrupt, arrival, a stalled caravan, locked signs, unplaceable roads.</summary>
    public class TravelStopTests
    {
        static NodeDef Node(string id, float x, float y) => new NodeDef { Id = id, Kind = NodeKind.Camp, X = x, Y = y };

        static RegionDef Region(string id, IEnumerable<NodeDef> nodes, IEnumerable<EdgeDef> edges)
        {
            var r = new RegionDef { Id = id, Width = 3000, Height = 3000, Tiles = 256 };
            r.Nodes.AddRange(nodes);
            r.Edges.AddRange(edges);
            return r;
        }

        static RegionDef Long() => Region("test", new[] { Node("A", 0, 0), Node("B", 1000, 0) }, new[] { new EdgeDef { A = "A", B = "B" } });

        static RegionDef Bend() => Region("test",
            new[] { Node("A", 0, 0), Node("B", 100, 0), Node("C", 100, 100) },
            new[] { new EdgeDef { A = "A", B = "B" }, new EdgeDef { A = "B", B = "C" } });

        static Journey Walk(RegionDef region, string from, string to) => new Journey(new TravelState(new WorldGraph(region).Find(from, to)));

        static WorldClock At(int hour) => new WorldClock(new WorldTimeConfig { StartHour = hour, Periods = WorldTimeConfig.Default().Periods });

        static readonly TravelConfig OnePixel = new TravelConfig { PixelsPerMinute = 1 };

        static void AssertAt(WorldPoint p, float x, float y)
        {
            Assert.That(p.X, Is.EqualTo(x).Within(0.001f));
            Assert.That(p.Y, Is.EqualTo(y).Within(0.001f));
        }

        [Test]
        public void Travel_stops_at_dusk_with_the_camp_prompt()
        {
            var journey = Walk(Long(), "A", "B");
            var clock = At(17);

            var walked = journey.Walk(clock, 600, OnePixel);

            Assert.That(walked.Stop, Is.EqualTo(TravelStop.Dusk));
            Assert.That(walked.Message, Is.EqualTo(Journey.CampPrompt));
            Assert.That(walked.Step.Minutes, Is.EqualTo(60));
            Assert.That(walked.Step.Distance, Is.EqualTo(60).Within(1e-9));
            Assert.That(clock.Period.Name, Is.EqualTo("Dusk"));

            var again = journey.Walk(clock, 600, OnePixel);
            Assert.That(again.Stop, Is.EqualTo(TravelStop.Dusk));
            Assert.That(again.Step.Minutes, Is.EqualTo(0));
        }

        [Test]
        public void Continuing_into_the_night_walks_on_but_slower()
        {
            var journey = Walk(Long(), "A", "B");
            var clock = At(17);
            journey.Walk(clock, 600, OnePixel);

            journey.ContinueIntoNight = true;
            var walked = journey.Walk(clock, 180, OnePixel);

            Assert.That(walked.Stop, Is.EqualTo(TravelStop.Limit));
            Assert.That(walked.Step.Minutes, Is.EqualTo(180));
            Assert.That(clock.Period.Name, Is.EqualTo("Night"));
            // Two hours of dusk at full speed, then one of night at the night multiplier.
            Assert.That(walked.Step.Distance, Is.EqualTo(120 + 60 * OnePixel.NightMultiplier).Within(1e-9));
            Assert.That(walked.Step.Distance, Is.LessThan(180));
        }

        [Test]
        public void Interrupt_stops_mid_leg_where_the_caravan_is()
        {
            var journey = Walk(Bend(), "A", "C");
            var clock = At(8);
            long start = clock.Minutes;
            int asked = 0;

            var walked = journey.Walk(clock, 600, OnePixel, () => ++asked > 130);

            Assert.That(walked.Stop, Is.EqualTo(TravelStop.Interrupted));
            Assert.That(walked.Step.Minutes, Is.EqualTo(130));
            Assert.That(clock.Minutes - start, Is.EqualTo(130));
            Assert.That(walked.Step.Passed, Is.EqualTo(new[] { "B" }));
            Assert.That(journey.State.LegIndex, Is.EqualTo(1));
            Assert.That(journey.State.DistanceIntoLeg, Is.EqualTo(30).Within(1e-9));
            AssertAt(journey.State.Position, 100, 30);
            Assert.That(journey.State.Arrived, Is.False);
        }

        [Test]
        public void Arriving_at_a_roadsign_transfers_to_the_paired_region_and_arrival_node()
        {
            var moors = Region("moors", new[] { Node("A", 0, 0), Node("Sign", 200, 0) }, new[] { new EdgeDef { A = "A", B = "Sign" } });
            moors.Roadsigns.Add(new RoadsignDef { Id = "to-lantern", Node = "Sign", Leads = "lantern", Arrives = "to-moors" });
            var lantern = Region("lantern", new[] { Node("Gate", 50, 50), Node("Town", 400, 50) }, new[] { new EdgeDef { A = "Gate", B = "Town" } });
            lantern.Roadsigns.Add(new RoadsignDef { Id = "to-moors", Node = "Gate", Leads = "moors", Arrives = "to-lantern" });
            var world = new WorldDef();
            world.Regions.Add(moors);
            world.Regions.Add(lantern);

            var journey = Walk(moors, "A", "Sign");
            var walked = journey.Walk(At(8), 600, OnePixel);
            Assert.That(walked.Stop, Is.EqualTo(TravelStop.Arrived));
            Assert.That(walked.Step.ArrivedAt, Is.EqualTo("Sign"));

            var sign = Roadsigns.At(moors, walked.Step.ArrivedAt);
            Assert.That(sign, Is.Not.Null);
            var transfer = Roadsigns.Transfer(sign, world, s => false);

            Assert.That(transfer.Allowed, Is.True);
            Assert.That(transfer.Reason, Is.Null);
            Assert.That(transfer.Region, Is.EqualTo("lantern"));
            Assert.That(transfer.Node, Is.EqualTo("Gate"));
            Assert.That(transfer.Arrival.Leads, Is.EqualTo("moors"));
            Assert.That(Roadsigns.At(moors, "A"), Is.Null);
        }

        [Test]
        public void A_locked_roadsign_refuses_until_its_stage_is_cleared()
        {
            var lantern = Region("lantern", new[] { Node("Gate", 50, 50) }, new EdgeDef[0]);
            lantern.Roadsigns.Add(new RoadsignDef { Id = "to-moors", Node = "Gate", Leads = "moors", Arrives = "to-lantern" });
            var world = new WorldDef();
            world.Regions.Add(lantern);
            var sign = new RoadsignDef { Id = "to-lantern", Node = "Sign", Leads = "lantern", Arrives = "to-moors", RequiresStage = "moors3" };

            var locked = Roadsigns.Transfer(sign, world, s => false);
            Assert.That(locked.Allowed, Is.False);
            Assert.That(locked.Reason, Is.EqualTo(Roadsigns.LockedReason));
            Assert.That(locked.Reason, Is.EqualTo("the Commander can't read this road yet"));
            Assert.That(locked.Region, Is.Null);
            Assert.That(Roadsigns.Transfer(sign, world, null).Allowed, Is.False);

            var open = Roadsigns.Transfer(sign, world, s => s == "moors3");
            Assert.That(open.Allowed, Is.True);
            Assert.That(open.Node, Is.EqualTo("Gate"));
        }

        [Test]
        public void Zero_speed_stops_with_a_reason_instead_of_spending_the_clock()
        {
            var journey = Walk(Long(), "A", "B");
            var clock = At(8);
            long start = clock.Minutes;

            var walked = journey.Walk(clock, 600, new TravelConfig { PixelsPerMinute = 1, WeatherMultiplier = () => 0 });

            Assert.That(walked.Stop, Is.EqualTo(TravelStop.Stalled));
            Assert.That(walked.Message, Is.EqualTo(Journey.StalledReason));
            Assert.That(walked.Step.Minutes, Is.EqualTo(0));
            Assert.That(clock.Minutes, Is.EqualTo(start));
            Assert.That(journey.State.DistanceTravelled, Is.EqualTo(0));
        }

        [Test]
        public void A_road_naming_a_node_outside_the_region_fails_fast()
        {
            var region = Region("test", new[] { Node("A", 0, 0), Node("B", 100, 0) },
                new[] { new EdgeDef { A = "A", B = "B" }, new EdgeDef { A = "B", B = "Ghost" } });

            Assert.That(new WorldGraph(region).Find("A", "B"), Is.Not.Null);
            Assert.That(() => new WorldGraph(region).Find("A", "Ghost"), Throws.InstanceOf<InvalidOperationException>().With.Message.Contains("Ghost"));
        }
    }
}
