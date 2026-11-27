using System;
using System.Collections.Generic;

namespace GridGame.Core.World
{
    public enum WeatherKind { Clear, Overcast, Rain, FogBank, Storm, Snow, Mist }

    /// <summary>One kind a region's weather may take and how often (a relative weight).</summary>
    public readonly struct WeatherChoice
    {
        public readonly WeatherKind Kind;
        public readonly int Weight;

        public WeatherChoice(WeatherKind kind, int weight)
        {
            Kind = kind;
            Weight = weight;
        }
    }

    /// <summary>The kinds of weather a region can have (GG-T1689), by weight; defaults per region in C#.</summary>
    public sealed class RegionWeatherTable
    {
        public List<WeatherChoice> Choices = new List<WeatherChoice>();

        public static RegionWeatherTable For(string regionId)
        {
            switch (regionId)
            {
                case "greenvalley":
                    return Of((WeatherKind.Clear, 5), (WeatherKind.Overcast, 3), (WeatherKind.Rain, 2), (WeatherKind.FogBank, 1));
                case "swamp":
                    return Of((WeatherKind.Mist, 4), (WeatherKind.Rain, 3), (WeatherKind.Overcast, 3), (WeatherKind.Storm, 1));
                case "lanternroad":
                    return Of((WeatherKind.Clear, 4), (WeatherKind.Overcast, 3), (WeatherKind.Snow, 3), (WeatherKind.Storm, 1));
                default:
                    return Of((WeatherKind.Clear, 5), (WeatherKind.Overcast, 3), (WeatherKind.Rain, 2));
            }
        }

        static RegionWeatherTable Of(params (WeatherKind kind, int weight)[] choices)
        {
            var table = new RegionWeatherTable();
            foreach (var c in choices) table.Choices.Add(new WeatherChoice(c.kind, c.weight));
            return table;
        }

        public int TotalWeight
        {
            get
            {
                int total = 0;
                foreach (var c in Choices) total += Math.Max(c.Weight, 0);
                return total;
            }
        }

        public bool Contains(WeatherKind kind)
        {
            foreach (var c in Choices) if (c.Kind == kind && c.Weight > 0) return true;
            return false;
        }

        /// <summary>The kind at a roll of 0 up to (not including) <see cref="TotalWeight"/>.</summary>
        public WeatherKind Pick(int roll)
        {
            foreach (var c in Choices)
            {
                if (c.Weight <= 0) continue;
                if (roll < c.Weight) return c.Kind;
                roll -= c.Weight;
            }
            throw new InvalidOperationException("The weather table has no kind with a weight.");
        }
    }

    public sealed class WorldWeatherConfig
    {
        /// <summary>Weather is decided once per spell of this many world minutes, so a kind lasts at least this long.</summary>
        public int SpellMinutes = 180;

        /// <summary>Minutes at the start of a spell over which cloud coverage eases from the last spell's to this one's.</summary>
        public int BlendMinutes = 45;

        /// <summary>How far the clouds drift across the map layer's span per world minute (1 = one full span).</summary>
        public float DriftPerMinute = 1f / 600f;

        public static float CoverageOf(WeatherKind kind)
        {
            switch (kind)
            {
                case WeatherKind.Clear: return 0.15f;
                case WeatherKind.Overcast: return 0.7f;
                case WeatherKind.Rain: return 0.85f;
                case WeatherKind.FogBank: return 0.6f;
                case WeatherKind.Storm: return 1f;
                case WeatherKind.Snow: return 0.8f;
                case WeatherKind.Mist: return 0.5f;
                default: return 0.15f;
            }
        }
    }

    /// <summary>
    /// The overworld's weather and clouds (GG-T1689), ported from Unity.Rea.Overworld Core/Scenery/Weather.cs (Weather's mode and
    /// WeatherChanged, Clouds' drift) and SceneryData.cs. The original's per-frame particles and command plumbing are dropped:
    /// weather here is a pure function of a seed, the region and world minutes (the caller advances it with the
    /// <see cref="WorldClock"/>), and the clouds are a drift offset and a coverage the map layer reads.
    /// </summary>
    public sealed class WorldWeather
    {
        readonly int seed;
        readonly WorldWeatherConfig config;
        string regionId;
        RegionWeatherTable table;
        long minutes;

        public WorldWeather(int seed, string regionId = null, WorldWeatherConfig config = null, RegionWeatherTable table = null)
        {
            this.seed = seed;
            this.config = config ?? new WorldWeatherConfig();
            if (this.config.SpellMinutes <= 0) throw new ArgumentOutOfRangeException(nameof(config));
            SetRegion(regionId, table);
            Kind = KindAt(0);
        }

        /// <summary>Raised when the kind changes while time advances: previous, new.</summary>
        public event Action<WeatherKind, WeatherKind> Changed;

        public string RegionId => regionId;
        public RegionWeatherTable Table => table;
        public WeatherKind Kind { get; private set; }
        public long Minutes => minutes;

        /// <summary>Moves to another region's table; the schedule is rolled afresh for it.</summary>
        public void SetRegion(string id, RegionWeatherTable newTable = null)
        {
            regionId = id ?? "";
            table = newTable ?? RegionWeatherTable.For(regionId);
            if (table.TotalWeight <= 0) throw new ArgumentException("The weather table has no weight.", nameof(newTable));
            Update(minutes);
        }

        /// <summary>Sets the world time (the clock's <see cref="WorldClock.Minutes"/>) and re-reads the weather there.</summary>
        public void Update(long worldMinutes)
        {
            minutes = Math.Max(worldMinutes, 0);
            var now = KindAt(minutes);
            if (now == Kind) return;
            var previous = Kind;
            Kind = now;
            Changed?.Invoke(previous, now);
        }

        long SpellAt(long m) => m / config.SpellMinutes;

        WeatherKind KindOfSpell(long spell)
        {
            var roll = (int)(Hash(seed, regionId, spell) % (uint)table.TotalWeight);
            return table.Pick(roll);
        }

        /// <summary>The weather at a world minute, from the seed alone.</summary>
        public WeatherKind KindAt(long worldMinutes) => KindOfSpell(SpellAt(Math.Max(worldMinutes, 0)));

        /// <summary>Minutes left before the current spell is over (the kind may then stay the same).</summary>
        public int MinutesToNextSpell => (int)(config.SpellMinutes - minutes % config.SpellMinutes);

        /// <summary>How far the clouds have drifted, 0 up to (not including) 1, wrapping; grows steadily with world time.</summary>
        public float CloudOffset
        {
            get
            {
                double phase = (Hash(seed, "clouds", 0) % 10000u) / 10000.0;
                double v = phase + minutes * (double)config.DriftPerMinute;
                return (float)(v - Math.Floor(v));
            }
        }

        /// <summary>How much of the sky the clouds cover, 0 to 1, easing between spells so it never jumps.</summary>
        public float CloudCoverage
        {
            get
            {
                long spell = SpellAt(minutes);
                float target = WorldWeatherConfig.CoverageOf(KindOfSpell(spell));
                int into = (int)(minutes % config.SpellMinutes);
                int blend = Math.Min(Math.Max(config.BlendMinutes, 0), config.SpellMinutes);
                if (spell == 0 || blend == 0 || into >= blend) return target;
                float from = WorldWeatherConfig.CoverageOf(KindOfSpell(spell - 1));
                return from + (target - from) * (into / (float)blend);
            }
        }

        static uint Hash(int seed, string text, long n)
        {
            uint h = 2166136261u;
            h = (h ^ (uint)seed) * 16777619u;
            foreach (var ch in text) h = (h ^ ch) * 16777619u;
            h = (h ^ (uint)n) * 16777619u;
            h = (h ^ (uint)(n >> 32)) * 16777619u;
            h ^= h >> 15; h *= 2246822519u; h ^= h >> 13; h *= 3266489917u; h ^= h >> 16;
            return h;
        }
    }
}
