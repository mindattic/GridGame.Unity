using System;
using System.Collections.Generic;
using System.Linq;
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
        /// weak to Fire (×1.5)", "Immune to all but Lightning" for an element-locked foe (§3.3.1, GG-T2071), or "No elemental
        /// weakness or resistance".</summary>
        public static string ElementLine(ClassDef d)
        {
            var parts = new List<string>();
            var open = new List<DamageType>();
            foreach (DamageType t in Enum.GetValues(typeof(DamageType)))
                if (d?.Resistances == null || !d.Resistances.TryGetValue(t, out var m) || m > 0f) open.Add(t);
            bool locked = open.Count == 1;
            if (locked) parts.Add($"immune to all but {open[0]}");
            if (d?.Resistances != null)
                foreach (var kv in d.Resistances)
                {
                    if (Math.Abs(kv.Value - 1f) < 0.001f || (locked && kv.Value <= 0f)) continue;
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

    /// <summary>GG-T1975: the Bestiary's regions, read from content: each campaign theme, in campaign order, holds the enemy
    /// classes its stages spawn and its overworld roads ambush with (world.json encounter families), each foe in the first
    /// region that fields it. Foes no region fields (summons, hatchlings, Crucible-only) close the list under a null theme.</summary>
    public static class BestiaryRegions
    {
        public static List<(ThemeDef Theme, List<ClassDef> Foes)> Group(ContentDb c)
        {
            var placed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var list = new List<(ThemeDef, List<ClassDef>)>();
            void Take(string id, List<ClassDef> into)
            {
                if (c.TryClass(id) is ClassDef d && d.Team == Team.Enemy && placed.Add(d.Id)) into.Add(d);
            }
            foreach (var t in c.Campaign.Themes)
            {
                var foes = new List<ClassDef>();
                foreach (var st in t.Stages.Select(c.TryStage).Where(x => x != null))
                    foreach (var sp in st.Waves.SelectMany(w => w.Enemies)) Take(sp.Class, foes);
                foreach (var fam in c.Region(t.Id)?.Edges.SelectMany(e => e.Encounters ?? new List<string>()) ?? Enumerable.Empty<string>())
                    foreach (var g in EncounterCatalog.ForFamily(c, fam))
                        foreach (var sp in g.Enemies) Take(sp.Class, foes);
                if (foes.Count > 0) list.Add((t, foes.OrderBy(f => f.Name).ToList()));
            }
            var rest = c.Classes.Values.Where(d => d.Team == Team.Enemy && !placed.Contains(d.Id)).OrderBy(d => d.Name).ToList();
            if (rest.Count > 0) list.Add((null, rest));
            return list;
        }

        /// <summary>Records a cleared stage's foes as a played clear would: each seen, and defeated once per spawn (a
        /// generated mid-game save, so its Bestiary is not empty).</summary>
        public static void RecordStage(SaveData s, StageDef stage)
        {
            foreach (var sp in stage.Waves.SelectMany(w => w.Enemies))
            {
                if (string.IsNullOrEmpty(sp.Class)) continue;
                if (!s.Bestiary.TryGetValue(sp.Class, out var e)) { e = new BestiaryEntry(); s.Bestiary[sp.Class] = e; }
                e.Seen = true;
                e.Defeated++;
            }
        }
    }
}
