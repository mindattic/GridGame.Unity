using GridGame.Core.World;
using NUnit.Framework;

namespace GridGame.Tests
{
    /// <summary>Predator encroachment odds (GG-T1698): inside less than outside, night more than day, Wilds depth, determinism, family table.</summary>
    public class PredatorTests
    {
        [Test]
        public void InsideCircleIsLessLikelyThanOutside()
        {
            Assert.That(Predators.Chance(true, 0, false), Is.LessThan(Predators.Chance(false, 0, false)));
        }

        [Test]
        public void NightIsMoreLikelyThanDay()
        {
            Assert.That(Predators.Chance(false, 50, true), Is.GreaterThan(Predators.Chance(false, 50, false)));
            Assert.That(Predators.Chance(true, 0, true), Is.GreaterThan(Predators.Chance(true, 0, false)));
        }

        [Test]
        public void DeeperInTheWildsRisesUpToTheCap()
        {
            float near = Predators.Chance(false, 50, false);
            float far = Predators.Chance(false, 200, false);
            Assert.That(far, Is.GreaterThan(near));
            Assert.That(Predators.Chance(false, 1000000, true), Is.EqualTo(Predators.Default.MaxChance));
            Assert.That(Predators.Chance(false, 1000000, false), Is.EqualTo(Predators.Default.MaxChance));
        }

        [Test]
        public void RollIsDeterministicForSeedAndHunt()
        {
            for (int i = 0; i < 50; i++)
            {
                var a = Predators.Roll(7, "hunt" + i, false, 300, true, "swamp");
                var b = Predators.Roll(7, "hunt" + i, false, 300, true, "swamp");
                Assert.That(a.HasValue, Is.EqualTo(b.HasValue));
                if (a.HasValue)
                {
                    Assert.That(a.Value.Family, Is.EqualTo(b.Value.Family));
                    Assert.That(a.Value.Count, Is.EqualTo(b.Value.Count));
                    Assert.That(a.Value.Wave, Is.EqualTo(b.Value.Wave));
                }
            }
        }

        [Test]
        public void RollJoinsInWaveTwoOrThreeAndFollowsOdds()
        {
            int joins = 0;
            for (int i = 0; i < 400; i++)
            {
                var j = Predators.Roll(3, "h" + i, false, 1000000, true, "lanternroad");
                if (!j.HasValue) continue;
                joins++;
                Assert.That(j.Value.Wave, Is.InRange(2, 3));
                Assert.That(j.Value.Count, Is.GreaterThanOrEqualTo(1));
                Assert.That(j.Value.Family, Is.AnyOf("wolves", "bats"));
            }
            Assert.That(joins, Is.GreaterThan(300));

            int safe = 0;
            for (int i = 0; i < 400; i++) if (Predators.Roll(3, "h" + i, true, 0, false, "greenvalley").HasValue) safe++;
            Assert.That(safe, Is.LessThan(80));
        }

        [Test]
        public void RegionFamilyTableFallsBackToWolves()
        {
            var c = new PredatorConfig();
            Assert.That(c.FamiliesFor("swamp")[0].Family, Is.EqualTo("lurkers"));
            Assert.That(c.FamiliesFor("SWAMP").Length, Is.EqualTo(2));
            Assert.That(c.FamiliesFor("lanternroad").Length, Is.EqualTo(2));
            Assert.That(c.FamiliesFor("greenvalley")[0].Family, Is.EqualTo("wolves"));
            Assert.That(c.FamiliesFor("unknown")[0].Family, Is.EqualTo("wolves"));
            Assert.That(c.FamiliesFor(null)[0].Family, Is.EqualTo("wolves"));
        }
    }
}
