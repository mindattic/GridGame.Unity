using System;
using System.Linq;
using GridGame.Core.World;
using NUnit.Framework;

namespace GridGame.Tests
{
    /// <summary>Camp circle and hunt spots (GG-T1695, 28.5).</summary>
    public class CampSiteTests
    {
        static readonly Func<double, double, bool> Land = (x, y) => true;

        static (double X, double Y)[] All(CampSite s) =>
            s.HuntSpots.Select(h => (h.X, h.Y)).Concat(s.ForageSpots.Select(f => (f.X, f.Y))).ToArray();

        [Test]
        public void Spots_are_inside_the_circle_and_on_walkable_ground()
        {
            Func<double, double, bool> walk = (x, y) => x >= 500;
            for (int seed = 0; seed < 20; seed++)
            {
                var s = CampSite.Pitch(500, 400, seed, walk);
                Assert.That(s.HuntSpots.Length + s.ForageSpots.Length, Is.GreaterThan(0));
                foreach (var (x, y) in All(s))
                {
                    Assert.That(s.Contains(x, y), Is.True);
                    Assert.That(walk(x, y), Is.True);
                }
            }
        }

        [Test]
        public void Full_counts_on_open_ground_and_hunt_spots_default_to_game_animals()
        {
            var cfg = new CampConfig();
            var s = CampSite.Pitch(0, 0, 3, Land, cfg);
            Assert.That(s.HuntSpots.Length, Is.EqualTo(cfg.HuntSpotCount));
            Assert.That(s.ForageSpots.Length, Is.EqualTo(cfg.ForageSpotCount));
            Assert.That(s.HuntSpots.All(h => h.Kind == HuntSpot.GameAnimals), Is.True);
            Assert.That(s.HuntSpots.Select(h => h.Id).Distinct().Count(), Is.EqualTo(s.HuntSpots.Length));
        }

        [Test]
        public void Spacing_is_respected()
        {
            var cfg = new CampConfig();
            for (int seed = 0; seed < 20; seed++)
            {
                var pts = All(CampSite.Pitch(0, 0, seed, Land, cfg));
                for (int i = 0; i < pts.Length; i++)
                    for (int j = i + 1; j < pts.Length; j++)
                        Assert.That(Math.Sqrt(Math.Pow(pts[i].X - pts[j].X, 2) + Math.Pow(pts[i].Y - pts[j].Y, 2)),
                            Is.GreaterThanOrEqualTo(cfg.MinSpotSpacing));
            }
        }

        [Test]
        public void Same_seed_same_spots_different_seed_different()
        {
            var a = CampSite.Pitch(10, 20, 7, Land);
            var b = CampSite.Pitch(10, 20, 7, Land);
            var c = CampSite.Pitch(10, 20, 8, Land);
            Assert.That(All(a), Is.EqualTo(All(b)));
            Assert.That(a.HuntSpots.Select(h => h.Id), Is.EqualTo(b.HuntSpots.Select(h => h.Id)));
            Assert.That(All(a), Is.Not.EqualTo(All(c)));
        }

        [Test]
        public void All_water_yields_no_spots_and_terminates()
        {
            var s = CampSite.Pitch(0, 0, 1, (x, y) => false);
            Assert.That(s.HuntSpots, Is.Empty);
            Assert.That(s.ForageSpots, Is.Empty);
        }

        [Test]
        public void Little_room_yields_fewer_spots()
        {
            var cfg = new CampConfig { CircleRadius = 30, MinSpotSpacing = 40, HuntSpotCount = 3, ForageSpotCount = 4 };
            var s = CampSite.Pitch(0, 0, 5, Land, cfg);
            Assert.That(s.HuntSpots.Length + s.ForageSpots.Length, Is.LessThan(7));
        }

        [Test]
        public void Contains_edge_cases()
        {
            var s = new CampSite { X = 10, Y = 10, Radius = 5 };
            Assert.That(s.Contains(10, 10), Is.True);
            Assert.That(s.Contains(15, 10), Is.True);
            Assert.That(s.Contains(10, 5), Is.True);
            Assert.That(s.Contains(15.01, 10), Is.False);
            Assert.That(s.Contains(14, 14), Is.False);
        }
    }
}
