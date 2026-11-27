using System;
using GridGame.Core.World;
using NUnit.Framework;

namespace GridGame.Tests
{
    /// <summary>Weather effects as data (GG-T1690): travel speed, danger, battle overlay and rule per kind.</summary>
    public class WeatherEffectsTests
    {
        [Test]
        public void Every_Kind_Has_An_Entry()
        {
            foreach (WeatherKind kind in Enum.GetValues(typeof(WeatherKind)))
            {
                var e = WeatherEffects.For(kind);
                Assert.That(e.Kind, Is.EqualTo(kind));
                Assert.That(e.TravelSpeedMultiplier, Is.GreaterThan(0));
                Assert.That(e.DangerMultiplier, Is.GreaterThanOrEqualTo(1.0));
            }
        }

        [Test]
        public void Storm_And_Snow_Slow_Travel()
        {
            Assert.That(WeatherEffects.For(WeatherKind.Storm).TravelSpeedMultiplier, Is.EqualTo(0.75));
            Assert.That(WeatherEffects.For(WeatherKind.Snow).TravelSpeedMultiplier, Is.EqualTo(0.7));
            Assert.That(WeatherEffects.For(WeatherKind.Rain).TravelSpeedMultiplier, Is.EqualTo(0.9));
            Assert.That(WeatherEffects.For(WeatherKind.FogBank).TravelSpeedMultiplier, Is.EqualTo(0.85));
            Assert.That(WeatherEffects.For(WeatherKind.Mist).TravelSpeedMultiplier, Is.EqualTo(0.9));
        }

        [Test]
        public void Clear_And_Overcast_Are_Neutral()
        {
            foreach (var kind in new[] { WeatherKind.Clear, WeatherKind.Overcast })
            {
                var e = WeatherEffects.For(kind);
                Assert.That(e.TravelSpeedMultiplier, Is.EqualTo(1.0));
                Assert.That(e.DangerMultiplier, Is.EqualTo(1.0));
                Assert.That(e.Overlay, Is.EqualTo(BattleOverlay.None));
                Assert.That(e.Rule, Is.EqualTo(BattleRule.None));
            }
        }

        [Test]
        public void Fog_And_Storm_Carry_The_Reduced_Range_Rule()
        {
            foreach (var kind in new[] { WeatherKind.FogBank, WeatherKind.Storm })
            {
                var e = WeatherEffects.For(kind);
                Assert.That(e.Rule, Is.EqualTo(BattleRule.ReducedRange));
                Assert.That(e.RangePenalty, Is.EqualTo(1));
            }
            foreach (var kind in new[] { WeatherKind.Clear, WeatherKind.Overcast, WeatherKind.Rain, WeatherKind.Snow, WeatherKind.Mist })
            {
                Assert.That(WeatherEffects.For(kind).Rule, Is.EqualTo(BattleRule.None));
                Assert.That(WeatherEffects.For(kind).RangePenalty, Is.EqualTo(0));
            }
        }

        [Test]
        public void Overlays_Match_Their_Kind()
        {
            Assert.That(WeatherEffects.For(WeatherKind.Rain).Overlay, Is.EqualTo(BattleOverlay.Rain));
            Assert.That(WeatherEffects.For(WeatherKind.Snow).Overlay, Is.EqualTo(BattleOverlay.Snow));
            Assert.That(WeatherEffects.For(WeatherKind.Mist).Overlay, Is.EqualTo(BattleOverlay.Mist));
            Assert.That(WeatherEffects.For(WeatherKind.FogBank).Overlay, Is.EqualTo(BattleOverlay.FogBank));
            Assert.That(WeatherEffects.For(WeatherKind.Storm).Overlay, Is.EqualTo(BattleOverlay.Storm));
        }

        [Test]
        public void Travel_Hook_Returns_The_Current_Weathers_Multiplier()
        {
            var weather = new WorldWeather(1, "greenvalley");
            var hook = WeatherEffects.TravelMultiplier(weather);
            Assert.That(hook(), Is.EqualTo(WeatherEffects.For(weather.Kind).TravelSpeedMultiplier));
        }

        [Test]
        public void Null_Weather_Hook_Is_One_And_Config_Speed_Follows_It()
        {
            Assert.That(WeatherEffects.TravelMultiplier(null)(), Is.EqualTo(1.0));
            var cfg = TravelConfig.Default().WithWeather(new WorldWeather(3, "lanternroad"));
            Assert.That(cfg.WeatherMultiplier, Is.Not.Null);
            Assert.That(cfg.SpeedAt(null), Is.EqualTo(cfg.PixelsPerMinute * cfg.WeatherMultiplier()).Within(1e-9));
        }
    }
}
