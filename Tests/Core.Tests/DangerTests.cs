using GridGame.Core.Content;
using GridGame.Core.World;
using NUnit.Framework;

namespace GridGame.Tests
{
    /// <summary>The danger gauge (GG-T1684, 28.4): rise, night and weather, suppression, ambush, reset, pulse.</summary>
    public class DangerTests
    {
        static readonly DangerConfig Cfg = DangerConfig.Default();

        static double LevelAfter(EdgeDef edge, double px, bool night = false, WeatherKind weather = WeatherKind.Clear, bool suppressed = false)
        {
            var s = new DangerState(7);
            s.Rise(Cfg, edge, px, night, weather, suppressed);
            return s.Level;
        }

        [Test]
        public void Rises_With_Distance_And_Edge_Danger()
        {
            var e1 = new EdgeDef { Danger = 1 };
            var e2 = new EdgeDef { Danger = 2 };
            Assert.That(LevelAfter(e1, 200), Is.GreaterThan(LevelAfter(e1, 100)));
            Assert.That(LevelAfter(e2, 100), Is.EqualTo(LevelAfter(e1, 200)).Within(1e-9));
            Assert.That(LevelAfter(new EdgeDef { Danger = 0 }, 500), Is.EqualTo(0));
        }

        [Test]
        public void Faster_At_Night_And_In_Storms()
        {
            var e = new EdgeDef { Danger = 1 };
            double day = LevelAfter(e, 100);
            Assert.That(LevelAfter(e, 100, night: true), Is.GreaterThan(day));
            Assert.That(LevelAfter(e, 100, weather: WeatherKind.Storm), Is.GreaterThan(day));
            Assert.That(LevelAfter(e, 100, weather: WeatherKind.FogBank), Is.GreaterThan(day));
        }

        [Test]
        public void No_Rise_When_Suppressed()
        {
            Assert.That(LevelAfter(new EdgeDef { Danger = 3 }, 1000, night: true, suppressed: true), Is.EqualTo(0));
        }

        [Test]
        public void Ambush_Fires_At_Full_And_Not_Before()
        {
            var e = new EdgeDef { Danger = 1 };
            var s = new DangerState(3);
            Assert.That(s.Rise(Cfg, e, 100, false, WeatherKind.Clear, false), Is.False);
            Assert.That(s.Rise(Cfg, e, 100000, false, WeatherKind.Clear, false), Is.True);
            Assert.That(s.Level, Is.LessThanOrEqualTo(1.0));
        }

        [Test]
        public void Ambush_Is_Deterministic_For_The_Seed()
        {
            var e = new EdgeDef { Danger = 1 };
            double Walk(int seed)
            {
                var s = new DangerState(seed);
                while (!s.Rise(Cfg, e, 10, false, WeatherKind.Clear, false)) { }
                return s.Travelled;
            }
            Assert.That(Walk(11), Is.EqualTo(Walk(11)));
            Assert.That(Walk(11), Is.LessThanOrEqualTo(1.0 / Cfg.BaseRisePerPx + 10));
            bool varies = false;
            for (int seed = 0; seed < 20 && !varies; seed++) varies = Walk(seed) != Walk(seed + 1);
            Assert.That(varies, Is.True);
        }

        [Test]
        public void Reset_Clears_The_Gauge()
        {
            var s = new DangerState(5);
            s.Rise(Cfg, new EdgeDef { Danger = 1 }, 100000, false, WeatherKind.Clear, false);
            s.Reset();
            Assert.That(s.Level, Is.EqualTo(0));
            Assert.That(s.Rise(Cfg, new EdgeDef { Danger = 1 }, 1, false, WeatherKind.Clear, false), Is.False);
        }

        [Test]
        public void Pulse_Period_Decreases_As_Level_Rises()
        {
            Assert.That(DangerState.PulsePeriod(Cfg, 0), Is.EqualTo(Cfg.PulseMax));
            Assert.That(DangerState.PulsePeriod(Cfg, 1), Is.EqualTo(Cfg.PulseMin));
            Assert.That(DangerState.PulsePeriod(Cfg, 0.7), Is.LessThan(DangerState.PulsePeriod(Cfg, 0.3)));
            Assert.That(DangerState.PulsePeriod(Cfg, 5), Is.EqualTo(Cfg.PulseMin));
        }
    }
}
