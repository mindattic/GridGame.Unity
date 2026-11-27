using System;
using System.Collections.Generic;

namespace GridGame.Meta
{
    /// <summary>How a hero joined, which sets the Trust they arrive with (§25.20, <see cref="BondsConfig"/>).</summary>
    public enum JoinRoute { Starting, StoryRecruit, Rapport, Hire }

    /// <summary>One bond a battle moved (GG-T1573): a hero's Trust (<see cref="Key"/> is its class id) or a pair's cohesion
    /// (<see cref="Key"/> is the <see cref="Bonds.PairKey"/>).</summary>
    public sealed class BondChange
    {
        public string Key;
        public int Before;
        public int After;
    }

    /// <summary>What a won battle did to the bonds (GG-T1573), for the results screen (GG-T1302).</summary>
    public sealed class BattleBonds
    {
        public List<BondChange> Trust = new List<BondChange>();
        public List<BondChange> Cohesion = new List<BondChange>();
    }

    /// <summary>Trust and cohesion (bible §25.20, GG-T1292): Trust is how far one hero trusts the Commander, cohesion how well
    /// two heroes work together. Both are 0-100, read in the tiers of <see cref="BondsConfig.TierFloors"/>, and never drop
    /// below the floor of the highest tier reached. Kept in the save (<see cref="SaveData.Trust"/>,
    /// <see cref="SaveData.Cohesion"/>), so New Game+ carries them with the roster. Never throws on a bad save.</summary>
    public static class Bonds
    {
        /// <summary>A hero's key in the bond tables: its class id, lower case.</summary>
        public static string Key(string classId) =>
            string.IsNullOrWhiteSpace(classId) ? null : classId.Trim().ToLowerInvariant();

        /// <summary>The key of the unordered pair a+b ("a|b", sorted), so Barbarian+Cleric is Cleric+Barbarian; null for a
        /// missing id or a hero paired with itself.</summary>
        public static string PairKey(string a, string b)
        {
            a = Key(a); b = Key(b);
            if (a == null || b == null || a == b) return null;
            return string.CompareOrdinal(a, b) < 0 ? a + "|" + b : b + "|" + a;
        }

        /// <summary>The tier (0..<see cref="BondsConfig.MaxTier"/>) a bond value reads as.</summary>
        public static int Tier(int value)
        {
            var floors = BondsConfig.TierFloors;
            for (int t = floors.Length - 1; t > 0; t--)
                if (value >= floors[t]) return t;
            return 0;
        }

        /// <summary>Where tier <paramref name="tier"/> begins.</summary>
        public static int Floor(int tier) =>
            BondsConfig.TierFloors[Math.Max(0, Math.Min(BondsConfig.MaxTier, tier))];

        // ── Who has bonds ──────────────────────────────────────────────────────────

        /// <summary>Heroes on the roster have Trust, except domesticated monsters and mercenaries. The Warden is an ordinary
        /// hero since the fixed lead was retired (GG-T1405), so it has Trust too.</summary>
        public static bool HasTrust(SaveData s, string classId) => HasCohesion(s, classId);

        /// <summary>Cohesion pairs are drawn from the same heroes as Trust.</summary>
        public static bool HasCohesion(SaveData s, string classId)
        {
            var h = s?.Roster == null || Key(classId) == null ? null : s.Hero(classId);
            return h != null && !h.Domesticated && !Mercenaries.IsMerc(s, classId);
        }

        // ── Trust ──────────────────────────────────────────────────────────────────

        public static int Trust(SaveData s, string classId) => Read(s?.Trust, Key(classId));

        public static int TrustTier(SaveData s, string classId) => Tier(Trust(s, classId));

        /// <summary>The highest Trust tier the hero has ever reached; Trust never drops below its floor.</summary>
        public static int TrustTierReached(SaveData s, string classId) =>
            Math.Max(Read(s?.TrustTierReached, Key(classId)), TrustTier(s, classId));

        /// <summary>Moves a hero's Trust by <paramref name="delta"/>, clamped to the range and the floor of the highest tier
        /// reached. A hero without Trust (<see cref="HasTrust"/>) is left alone. Returns the new Trust.</summary>
        public static int AddTrust(SaveData s, string classId, int delta)
        {
            if (!HasTrust(s, classId)) return 0;
            s.Trust ??= new Dictionary<string, int>();
            s.TrustTierReached ??= new Dictionary<string, int>();
            return Apply(s.Trust, s.TrustTierReached, Key(classId), delta);
        }

        /// <summary>The Trust a hero arrives with for <paramref name="route"/>.</summary>
        public static int Seed(JoinRoute route)
        {
            switch (route)
            {
                case JoinRoute.StoryRecruit: return BondsConfig.StoryRecruitSeed;
                case JoinRoute.Rapport: return BondsConfig.RapportSeed;
                case JoinRoute.Hire: return BondsConfig.HireSeed;
                default: return BondsConfig.StartingSeed;
            }
        }

        /// <summary>Sets a joining hero's Trust from its join route. Only the first join counts: a hero who already has a
        /// Trust entry (one who left and came back) keeps it. Returns the hero's Trust.</summary>
        public static int SeedTrust(SaveData s, string classId, JoinRoute route)
        {
            if (!HasTrust(s, classId)) return 0;
            if (s.Trust != null && s.Trust.ContainsKey(Key(classId))) return Trust(s, classId);
            s.Trust ??= new Dictionary<string, int>();
            s.Trust[Key(classId)] = BondsConfig.StartingSeed;
            return AddTrust(s, classId, Seed(route));
        }

        // ── Cohesion ───────────────────────────────────────────────────────────────

        public static int Cohesion(SaveData s, string a, string b) => Read(s?.Cohesion, PairKey(a, b));

        public static int CohesionTier(SaveData s, string a, string b) => Tier(Cohesion(s, a, b));

        /// <summary>The highest cohesion tier the pair has ever reached; cohesion never drops below its floor.</summary>
        public static int CohesionTierReached(SaveData s, string a, string b) =>
            Math.Max(Read(s?.CohesionTierReached, PairKey(a, b)), CohesionTier(s, a, b));

        /// <summary>Moves a pair's cohesion by <paramref name="delta"/>, clamped like Trust. A pair where either hero has no
        /// cohesion (<see cref="HasCohesion"/>), or a hero paired with itself, is left alone. Returns the new cohesion.</summary>
        public static int AddCohesion(SaveData s, string a, string b, int delta)
        {
            var key = PairKey(a, b);
            if (key == null || !HasCohesion(s, a) || !HasCohesion(s, b)) return 0;
            s.Cohesion ??= new Dictionary<string, int>();
            s.CohesionTierReached ??= new Dictionary<string, int>();
            return Apply(s.Cohesion, s.CohesionTierReached, key, delta);
        }

        // ── Battle credits ─────────────────────────────────────────────────────────

        /// <summary>A won battle's bond credits (§25.20, GG-T1573): each pair in <paramref name="pairCredits"/> (keyed as
        /// <see cref="PairKey"/>) gains its credits, capped at <see cref="BondsConfig.PairCreditCapPerBattle"/>, as cohesion;
        /// each hero in <paramref name="fought"/>, standing or downed, gains <see cref="BondsConfig.VictoryTrust"/>. Heroes
        /// without bonds (<see cref="HasTrust"/>) are skipped. Returns each bond's before and after for the results screen.</summary>
        public static BattleBonds ApplyBattle(SaveData s, IReadOnlyDictionary<string, int> pairCredits, IEnumerable<string> fought)
        {
            var report = new BattleBonds();
            if (s == null) return report;
            var seen = new HashSet<string>();
            foreach (var id in fought ?? Array.Empty<string>())
            {
                var key = Key(id);
                if (key == null || !seen.Add(key) || !HasTrust(s, key)) continue;
                int before = Trust(s, key);
                report.Trust.Add(new BondChange { Key = key, Before = before, After = AddTrust(s, key, BondsConfig.VictoryTrust) });
            }
            if (pairCredits == null) return report;
            var pairs = new List<string>(pairCredits.Keys);
            pairs.Sort(string.CompareOrdinal);
            foreach (var pair in pairs)
            {
                int credits = Math.Min(pairCredits[pair], BondsConfig.PairCreditCapPerBattle);
                var ids = pair?.Split('|');
                if (credits <= 0 || ids == null || ids.Length != 2) continue;
                var key = PairKey(ids[0], ids[1]);
                if (key == null || !HasCohesion(s, ids[0]) || !HasCohesion(s, ids[1])) continue;
                int before = Cohesion(s, ids[0], ids[1]);
                report.Cohesion.Add(new BondChange { Key = key, Before = before, After = AddCohesion(s, ids[0], ids[1], credits) });
            }
            return report;
        }

        // ── Dialogue choices ───────────────────────────────────────────────────────

        /// <summary>A dialogue choice's bond change, held to <see cref="BondsConfig.ChoiceMin"/>..<see cref="BondsConfig.ChoiceMax"/>.</summary>
        public static int ClampChoice(int delta) => Math.Max(BondsConfig.ChoiceMin, Math.Min(BondsConfig.ChoiceMax, delta));

        /// <summary>True the first time this save pays the choice <paramref name="choiceId"/> of <paramref name="dialogueId"/>,
        /// false ever after (replays, New Game+): each bond-paying choice pays once per save.</summary>
        public static bool PayChoiceOnce(SaveData s, string dialogueId, string choiceId)
        {
            if (s == null || string.IsNullOrEmpty(dialogueId) || string.IsNullOrEmpty(choiceId)) return false;
            s.BondChoices ??= new HashSet<string>();
            return s.BondChoices.Add(dialogueId + "/" + choiceId);
        }

        // ── Shared rule ────────────────────────────────────────────────────────────

        private static int Read(Dictionary<string, int> table, string key) =>
            table != null && key != null && table.TryGetValue(key, out var v) ? Clamp(v) : 0;

        private static int Clamp(int v) => Math.Max(BondsConfig.Min, Math.Min(BondsConfig.Max, v));

        private static int Apply(Dictionary<string, int> values, Dictionary<string, int> reached, string key, int delta)
        {
            int old = Read(values, key);
            int peak = Math.Max(Read(reached, key), Tier(old));
            long raw = (long)old + delta;
            int next = (int)Math.Max(Floor(peak), Math.Min(BondsConfig.Max, raw));
            values[key] = next;
            peak = Math.Max(peak, Tier(next));
            if (peak > 0) reached[key] = peak;
            return next;
        }
    }
}
