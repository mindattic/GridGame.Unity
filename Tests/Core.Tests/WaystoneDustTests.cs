using System.Collections.Generic;
using GridGame.Core.Content;
using GridGame.Core.World;
using NUnit.Framework;

namespace GridGame.Tests
{
    /// <summary>Mana Dust at woken waystones and the Manalith step (GG-T1693).</summary>
    public class WaystoneDustTests
    {
        static WaystoneDust Make()
        {
            var r = new RegionDef
            {
                Id = "moors", Width = 1000, Height = 1000,
                Nodes = new List<NodeDef>
                {
                    new NodeDef { Id = "a", Kind = NodeKind.Waystone, X = 100, Y = 100, Radius = 80 },
                    new NodeDef { Id = "b", Kind = NodeKind.Waystone, X = 800, Y = 800, Radius = 50 },
                },
            };
            return new WaystoneDust(new WaystoneState(r), new WaystoneDustConfig { DustPerHour = 2, CapPerStone = 10 });
        }

        static void WakeAt(WaystoneDust d, string id, double now)
        {
            d.State.ClearGuardian(id);
            Assert.That(d.Wake(id, id, now), Is.True);
        }

        [Test]
        public void Accrues_per_hour_from_the_wake()
        {
            var d = Make();
            WakeAt(d, "a", 600);
            Assert.That(d.Pending("a", 600), Is.EqualTo(0));
            Assert.That(d.Pending("a", 660), Is.EqualTo(2));
            Assert.That(d.Pending("a", 780), Is.EqualTo(6));
        }

        [Test]
        public void Pending_is_capped_per_stone()
        {
            var d = Make();
            WakeAt(d, "a", 0);
            Assert.That(d.Pending("a", 60 * 1000), Is.EqualTo(10));
            Assert.That(d.Collect("a", 60 * 1000), Is.EqualTo(10));
        }

        [Test]
        public void Collect_pays_and_resets_the_clock()
        {
            var d = Make();
            WakeAt(d, "a", 0);
            Assert.That(d.Collect("a", 180), Is.EqualTo(6));
            Assert.That(d.Collect("a", 180), Is.EqualTo(0));
            Assert.That(d.Collect("a", 240), Is.EqualTo(2));
        }

        [Test]
        public void Dormant_stones_accrue_nothing()
        {
            var d = Make();
            Assert.That(d.Pending("a", 100000), Is.EqualTo(0));
            Assert.That(d.Collect("a", 100000), Is.EqualTo(0));
            Assert.That(d.CollectAllInRange(100, 100, 100000), Is.EqualTo(0));
            d.State.ClearGuardian("a");
            Assert.That(d.Wake("a", "b", 0), Is.False);
            Assert.That(d.Pending("a", 100000), Is.EqualTo(0));
        }

        [Test]
        public void CollectAllInRange_takes_only_stones_whose_circle_holds_the_point()
        {
            var d = Make();
            WakeAt(d, "a", 0);
            WakeAt(d, "b", 0);
            Assert.That(d.CollectAllInRange(500, 500, 180), Is.EqualTo(0));
            Assert.That(d.CollectAllInRange(120, 100, 180), Is.EqualTo(6));
            Assert.That(d.Pending("b", 180), Is.EqualTo(6));
            Assert.That(d.CollectAllInRange(120, 100, 180), Is.EqualTo(0));
        }

        [Test]
        public void Manalith_shrinks_per_woken_stone_and_clamps_at_the_floor()
        {
            var cfg = new WaystoneDustConfig { ManalithStepPerStone = 0.01f, MinSizeFactor = 0.7f };
            Assert.That(WaystoneDust.ManalithSize(0.5f, 0, cfg), Is.EqualTo(0.5f).Within(1e-6));
            Assert.That(WaystoneDust.ManalithSize(0.5f, 10, cfg), Is.EqualTo(0.45f).Within(1e-6));
            Assert.That(WaystoneDust.ManalithSize(0.5f, 10, cfg), Is.LessThan(WaystoneDust.ManalithSize(0.5f, 5, cfg)));
            Assert.That(WaystoneDust.ManalithSize(0.5f, 1000, cfg), Is.EqualTo(0.35f).Within(1e-6));
            Assert.That(WaystoneDust.ManalithSize(0.5f, -3, cfg), Is.EqualTo(0.5f).Within(1e-6));
        }
    }
}
