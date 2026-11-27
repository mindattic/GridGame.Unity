using System.Collections.Generic;
using GridGame.Core.World;
using NUnit.Framework;

namespace GridGame.Tests
{
    /// <summary>World weather and clouds (GG-T1689): seeded schedule, region tables, minimum spell, continuous drift.</summary>
    public class WorldWeatherTests
    {
        static List<WeatherKind> Schedule(int seed, string region, int spells)
        {
            var w = new WorldWeather(seed, region);
            var list = new List<WeatherKind>();
            for (int i = 0; i < spells; i++) list.Add(w.KindAt(i * 180L));
            return list;
        }

        [Test]
        public void Same_seed_gives_the_same_schedule_and_another_seed_differs()
        {
            Assert.That(Schedule(7, "greenvalley", 60), Is.EqualTo(Schedule(7, "greenvalley", 60)));
            Assert.That(Schedule(7, "greenvalley", 60), Is.Not.EqualTo(Schedule(8, "greenvalley", 60)));
        }

        [Test]
        public void Schedule_does_not_depend_on_how_time_was_advanced()
        {
            var a = new WorldWeather(3, "swamp");
            var b = new WorldWeather(3, "swamp");
            for (long m = 0; m <= 5000; m += 7) a.Update(m);
            a.Update(5000);
            b.Update(5000);
            Assert.That(a.Kind, Is.EqualTo(b.Kind));
        }

        [TestCase("greenvalley")]
        [TestCase("swamp")]
        [TestCase("lanternroad")]
        [TestCase("elsewhere")]
        public void Kinds_stay_within_the_regions_table(string region)
        {
            var w = new WorldWeather(11, region);
            var seen = new HashSet<WeatherKind>();
            for (int i = 0; i < 500; i++) seen.Add(w.KindAt(i * 180L));
            foreach (var kind in seen) Assert.That(w.Table.Contains(kind), Is.True, kind.ToString());
            Assert.That(seen.Count, Is.GreaterThan(1));
        }

        [Test]
        public void Region_tables_hold_their_defaults()
        {
            Assert.That(RegionWeatherTable.For("swamp").Contains(WeatherKind.Mist), Is.True);
            Assert.That(RegionWeatherTable.For("swamp").Contains(WeatherKind.Snow), Is.False);
            Assert.That(RegionWeatherTable.For("lanternroad").Contains(WeatherKind.Snow), Is.True);
            Assert.That(RegionWeatherTable.For("greenvalley").Contains(WeatherKind.FogBank), Is.True);
            Assert.That(RegionWeatherTable.For("other").Contains(WeatherKind.Storm), Is.False);
        }

        [Test]
        public void A_kind_lasts_at_least_its_minimum()
        {
            var config = new WorldWeatherConfig();
            var w = new WorldWeather(5, "greenvalley", config);
            var last = w.KindAt(0);
            long since = 0;
            for (long m = 1; m < 60000; m++)
            {
                var k = w.KindAt(m);
                if (k != last)
                {
                    Assert.That(m - since, Is.GreaterThanOrEqualTo(config.SpellMinutes));
                    last = k;
                    since = m;
                }
            }
        }

        [Test]
        public void Changed_fires_when_the_kind_changes()
        {
            var w = new WorldWeather(5, "greenvalley");
            int count = 0;
            w.Changed += (from, to) => { Assert.That(to, Is.Not.EqualTo(from)); count++; };
            for (long m = 0; m < 20000; m += 30) w.Update(m);
            Assert.That(count, Is.GreaterThan(0));
        }

        [Test]
        public void Clouds_drift_continuously_and_coverage_never_jumps()
        {
            var w = new WorldWeather(9, "swamp");
            w.Update(0);
            float offset = w.CloudOffset, coverage = w.CloudCoverage;
            for (long m = 1; m < 5000; m++)
            {
                w.Update(m);
                float step = w.CloudOffset - offset;
                if (step < 0) step += 1f;
                Assert.That(step, Is.EqualTo(1f / 600f).Within(1e-4f));
                Assert.That(System.Math.Abs(w.CloudCoverage - coverage), Is.LessThan(0.05f));
                Assert.That(w.CloudCoverage, Is.InRange(0f, 1f));
                offset = w.CloudOffset;
                coverage = w.CloudCoverage;
            }
        }
    }
}
