using System;
using System.Collections.Generic;

namespace GridGame.Core.World
{
    /// <summary>The backdrop layer a battle draws over its scene for the weather it was fought in (BIBLE 28).</summary>
    public enum BattleOverlay { None, Rain, Snow, Mist, FogBank, Storm }

    /// <summary>A weather's battle rule: none, or the reach of ranged moves and attacks shrinks.</summary>
    public enum BattleRule { None, ReducedRange }

    /// <summary>What one kind of weather does to travel, danger and battle (GG-T1690).</summary>
    public readonly struct WeatherEffect
    {
        public readonly WeatherKind Kind;
        public readonly double TravelSpeedMultiplier;
        public readonly double DangerMultiplier;
        public readonly BattleOverlay Overlay;
        public readonly BattleRule Rule;
        public readonly int RangePenalty;

        public WeatherEffect(WeatherKind kind, double travelSpeedMultiplier, double dangerMultiplier, BattleOverlay overlay, BattleRule rule = BattleRule.None, int rangePenalty = 0)
        {
            Kind = kind;
            TravelSpeedMultiplier = travelSpeedMultiplier;
            DangerMultiplier = dangerMultiplier;
            Overlay = overlay;
            Rule = rule;
            RangePenalty = rangePenalty;
        }
    }

    /// <summary>The per-kind weather effect table, in C# as data.</summary>
    public static class WeatherEffects
    {
        static readonly Dictionary<WeatherKind, WeatherEffect> Table = new Dictionary<WeatherKind, WeatherEffect>
        {
            { WeatherKind.Clear, new WeatherEffect(WeatherKind.Clear, 1.0, 1.0, BattleOverlay.None) },
            { WeatherKind.Overcast, new WeatherEffect(WeatherKind.Overcast, 1.0, 1.0, BattleOverlay.None) },
            { WeatherKind.Rain, new WeatherEffect(WeatherKind.Rain, 0.9, 1.25, BattleOverlay.Rain) },
            { WeatherKind.FogBank, new WeatherEffect(WeatherKind.FogBank, 0.85, 1.5, BattleOverlay.FogBank, BattleRule.ReducedRange, 1) },
            { WeatherKind.Storm, new WeatherEffect(WeatherKind.Storm, 0.75, 2.0, BattleOverlay.Storm, BattleRule.ReducedRange, 1) },
            { WeatherKind.Snow, new WeatherEffect(WeatherKind.Snow, 0.7, 1.25, BattleOverlay.Snow) },
            { WeatherKind.Mist, new WeatherEffect(WeatherKind.Mist, 0.9, 1.25, BattleOverlay.Mist) },
        };

        public static WeatherEffect For(WeatherKind kind) =>
            Table.TryGetValue(kind, out var effect) ? effect : Table[WeatherKind.Clear];

        /// <summary>A travel speed multiplier that reads the weather's current kind each call; null weather is clear, 1.</summary>
        public static Func<double> TravelMultiplier(WorldWeather weather) =>
            () => weather == null ? 1.0 : For(weather.Kind).TravelSpeedMultiplier;

        /// <summary>Points the config's weather slot at this weather and returns the config.</summary>
        public static TravelConfig WithWeather(this TravelConfig config, WorldWeather weather)
        {
            config.WeatherMultiplier = TravelMultiplier(weather);
            return config;
        }
    }
}
