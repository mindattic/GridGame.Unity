using System;
using System.Collections.Generic;
using GridGame.Core.Content;

namespace GridGame.Core.World
{
    /// <summary>A hand-crafted battle in the library (BIBLE 28.9): a stage tagged by region, biome, kind and base tier.</summary>
    public sealed class LibraryBattle
    {
        public string Id;
        public string Region;
        public string Biome;
        public BattleKind Kind;
        /// <summary>1..10.</summary>
        public int Tier;
        public StageDef Stage;
    }

    /// <summary>A library battle drawn for a trigger, with the Difficulty result's level offset and elite chance passed through.</summary>
    public sealed class PickedBattle
    {
        public LibraryBattle Battle;
        public int LevelOffset;
        public float EliteChance;
    }

    /// <summary>Picks a battle from the library by kind and tier band (GG-T1701, BIBLE 28.9).</summary>
    public static class BattlePicker
    {
        /// <summary>Most tiers the band widens by, each way, before it looks in other regions.</summary>
        public const int MaxWiden = 3;

        /// <summary>Same region and kind in the band, then the band widened a tier each way up to <see cref="MaxWiden"/>,
        /// then any region in the original band; null when none (the caller falls back to EncounterGenerator).
        /// Battles in <paramref name="recentlyPlayed"/> are skipped while an alternative exists.</summary>
        public static PickedBattle Pick(IReadOnlyList<LibraryBattle> library, string regionId, BattleKind kind,
            DifficultyResult diff, int seed, ISet<string> recentlyPlayed = null)
        {
            if (library == null || library.Count == 0) return null;
            var rng = new Rng(seed);

            for (int widen = 0; widen <= MaxWiden; widen++)
            {
                var found = PickFrom(library, regionId, kind, diff.TierMin - widen, diff.TierMax + widen, recentlyPlayed, rng);
                if (found != null) return Result(found, diff);
            }
            var any = PickFrom(library, null, kind, diff.TierMin, diff.TierMax, recentlyPlayed, rng);
            return any == null ? null : Result(any, diff);
        }

        static PickedBattle Result(LibraryBattle battle, DifficultyResult diff) =>
            new PickedBattle { Battle = battle, LevelOffset = diff.LevelOffset, EliteChance = diff.EliteChance };

        static LibraryBattle PickFrom(IReadOnlyList<LibraryBattle> library, string regionId, BattleKind kind,
            int tierMin, int tierMax, ISet<string> recent, Rng rng)
        {
            var fresh = new List<LibraryBattle>();
            var seen = new List<LibraryBattle>();
            foreach (var b in library)
            {
                if (b == null || b.Kind != kind || b.Tier < tierMin || b.Tier > tierMax) continue;
                if (regionId != null && !string.Equals(b.Region, regionId, StringComparison.OrdinalIgnoreCase)) continue;
                (recent != null && b.Id != null && recent.Contains(b.Id) ? seen : fresh).Add(b);
            }
            var pool = fresh.Count > 0 ? fresh : seen;
            if (pool.Count == 0) return null;
            pool.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return rng.Pick(pool);
        }
    }
}
