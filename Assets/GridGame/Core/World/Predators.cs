using System;
using System.Collections.Generic;

namespace GridGame.Core.World
{
    /// <summary>A predator family that can join a hunt in a region (BIBLE 28.5).</summary>
    public readonly struct PredatorFamily
    {
        public readonly string Family;
        public readonly int MinCount, MaxCount;

        public PredatorFamily(string family, int minCount, int maxCount) { Family = family; MinCount = minCount; MaxCount = maxCount; }
    }

    /// <summary>Predators that join a hunt in progress: what, how many, and in which wave.</summary>
    public readonly struct PredatorJoin
    {
        public readonly string Family;
        public readonly int Count;
        public readonly int Wave;

        public PredatorJoin(string family, int count, int wave) { Family = family; Count = count; Wave = wave; }
    }

    /// <summary>Predator encroachment numbers as data (GG-T1698, BIBLE 28.5).</summary>
    public sealed class PredatorConfig
    {
        public float InsideCircleChance = 0.05f;
        public float OutsideChance = 0.35f;
        public float NightMultiplier = 1.8f;

        /// <summary>Extra chance per <see cref="WildsBonusUnit"/> of distance beyond the circle edge.</summary>
        public float WildsBonusPerUnit = 0.08f;
        public float WildsBonusUnit = 100f;

        public float MaxChance = 0.9f;

        public int EarliestWave = 2;
        public int LatestWave = 3;

        public PredatorFamily[] DefaultFamilies = { new PredatorFamily("wolves", 2, 3) };

        public Dictionary<string, PredatorFamily[]> RegionFamilies = new Dictionary<string, PredatorFamily[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["greenvalley"] = new[] { new PredatorFamily("wolves", 2, 3) },
            ["swamp"] = new[] { new PredatorFamily("lurkers", 1, 2), new PredatorFamily("marsh shamblers", 1, 2) },
            ["lanternroad"] = new[] { new PredatorFamily("wolves", 2, 3), new PredatorFamily("bats", 2, 4) },
        };

        public PredatorFamily[] FamiliesFor(string regionId)
        {
            if (regionId != null && RegionFamilies.TryGetValue(regionId, out var f) && f != null && f.Length > 0) return f;
            return DefaultFamilies;
        }
    }

    /// <summary>Whether predators join a hunt, deterministic from the seed and the hunt id (BIBLE 28.5, GG-T1698).</summary>
    public static class Predators
    {
        public static readonly PredatorConfig Default = new PredatorConfig();

        /// <summary>Chance in [0, MaxChance] that predators join. distanceOutside is how far past the circle edge the hunt is.</summary>
        public static float Chance(bool insideCircle, float distanceOutside, bool isNight, PredatorConfig config = null)
        {
            var c = config ?? Default;
            float p = insideCircle
                ? c.InsideCircleChance
                : c.OutsideChance + c.WildsBonusPerUnit * Math.Max(0f, distanceOutside) / Math.Max(1e-3f, c.WildsBonusUnit);
            if (isNight) p *= c.NightMultiplier;
            return Math.Max(0f, Math.Min(c.MaxChance, p));
        }

        public static PredatorJoin? Roll(int seed, string huntId, bool insideCircle, float distanceOutside, bool isNight,
            string regionId, PredatorConfig config = null)
        {
            var c = config ?? Default;
            double chance = Chance(insideCircle, distanceOutside, isNight, c);
            if (Unit(Hash(seed, huntId, "join")) >= chance) return null;

            var families = c.FamiliesFor(regionId);
            var fam = families[(int)(Hash(seed, huntId, "family") % (uint)families.Length)];
            int lo = Math.Max(1, fam.MinCount), hi = Math.Max(lo, fam.MaxCount);
            int count = lo + (int)(Hash(seed, huntId, "count") % (uint)(hi - lo + 1));
            int wLo = c.EarliestWave, wHi = Math.Max(wLo, c.LatestWave);
            int wave = wLo + (int)(Hash(seed, huntId, "wave") % (uint)(wHi - wLo + 1));
            return new PredatorJoin(fam.Family, count, wave);
        }

        static double Unit(uint h) => h / 4294967296.0;

        static uint Hash(int seed, string huntId, string salt)
        {
            uint h = 2166136261u;
            h = (h ^ (uint)seed) * 16777619u;
            foreach (var ch in huntId ?? "") h = (h ^ ch) * 16777619u;
            foreach (var ch in salt) h = (h ^ ch) * 16777619u;
            h ^= h >> 15; h *= 2246822519u; h ^= h >> 13; h *= 3266489917u; h ^= h >> 16;
            return h;
        }
    }
}
