using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core.World;
using NUnit.Framework;

namespace GridGame.Tests
{
    /// <summary>Seeded Wild events (GG-T1696, 28.5).</summary>
    public class WildEventTests
    {
        static readonly Func<double, double, bool> Land = (x, y) => true;
        static readonly WildBounds Bounds = new WildBounds(0, 0, 2000, 2000);
        static readonly CampSite Camp = new CampSite { X = 1000, Y = 1000, Radius = 120 };

        static Dictionary<WildEventKind, IList<string>> Cands(params WildEventKind[] kinds) =>
            kinds.ToDictionary(k => k, k => (IList<string>)new List<string> { k + "-a", k + "-b" });

        static Dictionary<WildEventKind, IList<string>> All() =>
            Cands(WildEventKind.RecruitHero, WildEventKind.WildBoss, WildEventKind.Treasure, WildEventKind.Bounty);

        static Func<double, double, bool> OutsideCamp(params (double X, double Y, double R)[] extra) => (x, y) =>
        {
            if (Camp.Contains(x, y)) return false;
            foreach (var c in extra)
                if ((x - c.X) * (x - c.X) + (y - c.Y) * (y - c.Y) <= c.R * c.R) return false;
            return true;
        };

        [Test]
        public void Never_inside_the_camp_or_a_waystone_circle()
        {
            var stone = (X: 1300.0, Y: 1000.0, R: 150.0);
            var outside = OutsideCamp(stone);
            for (int seed = 0; seed < 30; seed++)
            {
                var list = WildEvents.Spawn(seed, Camp, Land, outside, All(), Bounds);
                Assert.That(list, Is.Not.Empty);
                foreach (var e in list)
                {
                    Assert.That(Camp.Contains(e.X, e.Y), Is.False);
                    Assert.That((e.X - stone.X) * (e.X - stone.X) + (e.Y - stone.Y) * (e.Y - stone.Y), Is.GreaterThan(stone.R * stone.R));
                }
            }
        }

        [Test]
        public void Only_on_walkable_ground()
        {
            Func<double, double, bool> walk = (x, y) => y >= 1000;
            for (int seed = 0; seed < 20; seed++)
                foreach (var e in WildEvents.Spawn(seed, Camp, walk, OutsideCamp(), All(), Bounds))
                    Assert.That(walk(e.X, e.Y), Is.True);
        }

        [Test]
        public void Same_seed_same_events()
        {
            var a = WildEvents.Spawn(7, Camp, Land, OutsideCamp(), All(), Bounds);
            var b = WildEvents.Spawn(7, Camp, Land, OutsideCamp(), All(), Bounds);
            Assert.That(a.Select(e => (e.Id, e.Kind, e.X, e.Y, e.Payload)), Is.EqualTo(b.Select(e => (e.Id, e.Kind, e.X, e.Y, e.Payload))));
        }

        [Test]
        public void A_kind_with_no_candidates_never_appears()
        {
            var cands = Cands(WildEventKind.Treasure, WildEventKind.Bounty);
            cands[WildEventKind.RecruitHero] = new List<string>();
            for (int seed = 0; seed < 30; seed++)
                foreach (var e in WildEvents.Spawn(seed, Camp, Land, OutsideCamp(), cands, Bounds))
                    Assert.That(e.Kind, Is.Not.EqualTo(WildEventKind.RecruitHero).And.Not.EqualTo(WildEventKind.WildBoss));
        }

        [Test]
        public void No_candidates_at_all_yields_nothing()
        {
            Assert.That(WildEvents.Spawn(1, Camp, Land, OutsideCamp(), new Dictionary<WildEventKind, IList<string>>(), Bounds), Is.Empty);
        }

        [Test]
        public void Payload_comes_from_the_kind_candidates()
        {
            foreach (var e in WildEvents.Spawn(3, Camp, Land, OutsideCamp(), All(), Bounds))
                Assert.That(e.Payload, Does.StartWith(e.Kind.ToString()));
        }

        [Test]
        public void Count_stays_within_the_configured_range_and_bounds()
        {
            var cfg = new WildConfig { MinCount = 3, MaxCount = 4 };
            for (int seed = 0; seed < 20; seed++)
            {
                var list = WildEvents.Spawn(seed, Camp, Land, OutsideCamp(), All(), Bounds, cfg);
                Assert.That(list.Count, Is.InRange(0, 4));
                foreach (var e in list) Assert.That(Bounds.Contains(e.X, e.Y), Is.True);
            }
        }

        [Test]
        public void Events_keep_the_minimum_spacing()
        {
            var cfg = new WildConfig { MinCount = 8, MaxCount = 8, MinSpacing = 80 };
            for (int seed = 0; seed < 20; seed++)
            {
                var list = WildEvents.Spawn(seed, Camp, Land, OutsideCamp(), All(), Bounds, cfg);
                for (int i = 0; i < list.Count; i++)
                    for (int j = i + 1; j < list.Count; j++)
                    {
                        double dx = list[i].X - list[j].X, dy = list[i].Y - list[j].Y;
                        Assert.That(Math.Sqrt(dx * dx + dy * dy), Is.GreaterThanOrEqualTo(cfg.MinSpacing));
                    }
            }
        }

        [Test]
        public void Fog_hides_events()
        {
            var list = WildEvents.Spawn(5, Camp, Land, OutsideCamp(), All(), Bounds);
            Assert.That(list, Is.Not.Empty);
            Assert.That(WildEvents.Visible(list, (x, y) => false), Is.Empty);
            Assert.That(WildEvents.Visible(list, (x, y) => true).Count, Is.EqualTo(list.Count));
            var half = WildEvents.Visible(list, (x, y) => x >= 1000);
            Assert.That(half.All(e => e.X >= 1000), Is.True);
        }
    }
}
