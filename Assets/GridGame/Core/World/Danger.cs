using System;
using System.Collections.Generic;
using GridGame.Core.Content;

namespace GridGame.Core.World
{
    /// <summary>How the danger gauge rises and pulses (BIBLE 28.4).</summary>
    public sealed class DangerConfig
    {
        /// <summary>Gauge rise per art pixel travelled on an edge of Danger 1.</summary>
        public double BaseRisePerPx = 0.0004;

        public double NightMultiplier = 2.0;

        /// <summary>Rise multiplier per weather kind; a kind not listed is 1.</summary>
        public Dictionary<WeatherKind, double> WeatherMultipliers = new Dictionary<WeatherKind, double>
        {
            { WeatherKind.Storm, 2.0 },
            { WeatherKind.FogBank, 1.5 },
            { WeatherKind.Mist, 1.25 },
            { WeatherKind.Rain, 1.25 },
            { WeatherKind.Snow, 1.25 },
        };

        /// <summary>Seconds between marker pulses at an empty gauge and at a full one.</summary>
        public double PulseMax = 3.0;
        public double PulseMin = 0.5;

        /// <summary>The gauge level at which the ambush fires.</summary>
        public double AmbushAt = 1.0;

        /// <summary>How far below <see cref="AmbushAt"/> the seeded jitter may pull the threshold, as a fraction of it.</summary>
        public double Jitter = 0.1;

        public static DangerConfig Default() => new DangerConfig();

        public double WeatherMultiplierOf(WeatherKind weather) =>
            WeatherMultipliers != null && WeatherMultipliers.TryGetValue(weather, out var m) ? m : 1.0;
    }

    /// <summary>The gauge: Level 0..1, the seed, and the distance travelled, which together fix where the next ambush lands.</summary>
    public sealed class DangerState
    {
        public double Level;
        public int Seed;
        public double Travelled;
        /// <summary>Counts resets, so each stretch of road gets its own threshold.</summary>
        public int Epoch;
        double _anchor;

        public DangerState(int seed = 0)
        {
            Seed = seed;
        }

        /// <summary>The level at which this stretch's ambush fires: AmbushAt less a seeded jitter.</summary>
        public double Threshold(DangerConfig config)
        {
            config = config ?? DangerConfig.Default();
            double u = Unit(Hash(Seed, Epoch, (long)_anchor));
            return Math.Max(config.AmbushAt * (1.0 - config.Jitter * u), 0.0001);
        }

        /// <summary>Advance the gauge by <paramref name="distancePx"/> along <paramref name="edge"/>. Returns true when the
        /// gauge has reached the ambush threshold; the caller resolves the ambush and calls <see cref="Reset"/>.
        /// Nothing rises while <paramref name="suppressed"/> (inside a camp or woken waystone circle).</summary>
        public bool Rise(DangerConfig config, EdgeDef edge, double distancePx, bool isNight, WeatherKind weather, bool suppressed)
        {
            config = config ?? DangerConfig.Default();
            if (distancePx > 0) Travelled += distancePx;
            if (!suppressed && distancePx > 0 && edge != null && edge.Danger > 0)
            {
                double rate = config.BaseRisePerPx * edge.Danger * config.WeatherMultiplierOf(weather);
                if (isNight) rate *= config.NightMultiplier;
                Level = Math.Min(1.0, Level + rate * distancePx);
            }
            return Level >= Threshold(config);
        }

        /// <summary>Camp or arrival: the gauge empties and the next ambush gets a new threshold.</summary>
        public void Reset()
        {
            Level = 0;
            Epoch++;
            _anchor = Travelled;
        }

        /// <summary>Seconds between marker pulses: <see cref="DangerConfig.PulseMax"/> when empty, shrinking to PulseMin when full.</summary>
        public static double PulsePeriod(DangerConfig config, double level)
        {
            config = config ?? DangerConfig.Default();
            double t = Math.Min(Math.Max(level, 0), 1);
            return config.PulseMax + (config.PulseMin - config.PulseMax) * t;
        }

        static double Unit(uint h) => (h % 10000u) / 10000.0;

        static uint Hash(int seed, int epoch, long n)
        {
            uint h = 2166136261u;
            h = (h ^ (uint)seed) * 16777619u;
            h = (h ^ (uint)epoch) * 16777619u;
            h = (h ^ (uint)n) * 16777619u;
            h = (h ^ (uint)(n >> 32)) * 16777619u;
            h ^= h >> 15; h *= 2246822519u; h ^= h >> 13; h *= 3266489917u; h ^= h >> 16;
            return h;
        }
    }
}
