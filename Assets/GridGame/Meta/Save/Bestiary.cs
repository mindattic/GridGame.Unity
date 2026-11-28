using System;
using System.Collections.Generic;
using GridGame.Core.Content;

namespace GridGame.Meta
{
    /// <summary>What the save knows about one enemy class (§25.11). How much the Bestiary and the battle inspect
    /// reveal is <see cref="BestiaryKnowledge.Tier"/>, earned by <see cref="Defeated"/>.</summary>
    public sealed class BestiaryEntry
    {
        public bool Seen;
        /// <summary>Retired with the Scan ability (GG-T290). Never set any more; an older save that scanned this enemy
        /// keeps at least the stats tier through <see cref="BestiaryKnowledge.Tier"/>.</summary>
        public bool Scanned;
        public int Defeated;
    }

    /// <summary>The Bestiary's knowledge tiers (§25.11, GG-T290): the more of an enemy the party defeats, the more it
    /// learns. Seen gives its name and description; then Speed, then HP and every base stat, then its elemental
    /// weaknesses and resistances, and last its abilities and how it picks targets.</summary>
    public static class BestiaryKnowledge
    {
        public const int None = 0, Description = 1, Speed = 2, Stats = 3, Elements = 4, Abilities = 5;
        public const int DefeatsForSpeed = 1, DefeatsForStats = 3, DefeatsForElements = 6, DefeatsForAbilities = 10;

        /// <summary>The highest tier <paramref name="e"/> has reached. A pre-GG-T290 Scanned entry counts as at least Stats.</summary>
        public static int Tier(BestiaryEntry e)
        {
            if (e == null) return None;
            int d = Math.Max(0, e.Defeated);
            int t = d >= DefeatsForAbilities ? Abilities : d >= DefeatsForElements ? Elements : d >= DefeatsForStats ? Stats
                : d >= DefeatsForSpeed ? Speed : e.Seen ? Description : None;
            if (e.Scanned && t < Stats) t = Stats;
            return t;
        }

        public static int Tier(SaveData s, string classId) =>
            s?.Bestiary != null && classId != null && s.Bestiary.TryGetValue(classId, out var e) ? Tier(e) : None;

        public static bool Knows(BestiaryEntry e, int tier) => Tier(e) >= tier;
        public static bool Knows(SaveData s, string classId, int tier) => Tier(s, classId) >= tier;

        /// <summary>The defeat count that unlocks <paramref name="tier"/> (0 for Description and below).</summary>
        public static int DefeatsFor(int tier) =>
            tier >= Abilities ? DefeatsForAbilities : tier == Elements ? DefeatsForElements : tier == Stats ? DefeatsForStats : tier == Speed ? DefeatsForSpeed : 0;

        /// <summary>The next tier still to learn, or -1 when everything is known.</summary>
        public static int NextTier(BestiaryEntry e)
        {
            int t = Tier(e);
            return t >= Abilities ? -1 : Math.Max(Speed, t + 1);
        }

        /// <summary>Defeats still needed for the next tier (0 when everything is known).</summary>
        public static int DefeatsToNext(BestiaryEntry e)
        {
            int next = NextTier(e);
            return next < 0 ? 0 : Math.Max(0, DefeatsFor(next) - Math.Max(0, e?.Defeated ?? 0));
        }

        /// <summary>What a tier reveals, as the Bestiary's "Defeat N more to learn …" phrase.</summary>
        public static string Label(int tier) => tier switch
        {
            Speed => "its speed",
            Stats => "its HP and stats",
            Elements => "its weaknesses",
            Abilities => "its abilities",
            _ => "about it",
        };

        /// <summary>Its elemental weaknesses and resistances from the class's multipliers, e.g. "Immune to Poison ·
        /// weak to Fire (×1.5)", or "No elemental weakness or resistance".</summary>
        public static string ElementLine(ClassDef d)
        {
            var parts = new List<string>();
            if (d?.Resistances != null)
                foreach (var kv in d.Resistances)
                {
                    if (Math.Abs(kv.Value - 1f) < 0.001f) continue;
                    parts.Add(kv.Value <= 0f ? $"immune to {kv.Key}"
                        : kv.Value > 1f ? $"weak to {kv.Key} (×{kv.Value:0.##})" : $"resists {kv.Key} (×{kv.Value:0.##})");
                }
            if (parts.Count == 0) return "No elemental weakness or resistance";
            var s = string.Join(" · ", parts);
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }

        /// <summary>"Defeat 2 more to learn its HP and stats", or null when everything is known.</summary>
        public static string Hint(BestiaryEntry e)
        {
            int next = NextTier(e);
            if (next < 0) return null;
            int n = DefeatsToNext(e);
            return $"Defeat {n} more to learn {Label(next)}.";
        }
    }
}
