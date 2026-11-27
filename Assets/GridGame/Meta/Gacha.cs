using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Content;

namespace GridGame.Meta
{
    /// <summary>
    /// The summon banners' rolls (BIBLE §25.10.3, GG-T1419): who a banner can bring (<see cref="Pool"/>), the chance of each
    /// (<see cref="Odds"/>, disclosed before purchase as GG-T503 requires, and the very list <see cref="Roll"/> draws from),
    /// a seeded roll with the Featured share and hard pity, and the record of each pull (<see cref="Record"/>: the pity
    /// counters per banner kind and the pull history in <see cref="SaveData.Summon"/>). Engine-free; bots and Test Mode
    /// never pull (callers refuse them, as <see cref="DiamondSummon.CanDraw"/> does).
    /// </summary>
    public static class Gacha
    {
        /// <summary>How many pulls the save keeps in <see cref="SummonState.History"/>; older ones roll off.</summary>
        public const int HistoryKeep = 200;

        /// <summary>What a duplicate is called on screen (GG-T1421); the owner may rename it ("Resonance", decision D7).</summary>
        public const string EchoName = "Echo";
        /// <summary>Skill points each Echo gives the hero it echoes.</summary>
        public const int EchoSp = 1;
        /// <summary>Echoes a hero can hold; a duplicate past this gives Ticket fragments instead.</summary>
        public const int EchoCap = 5;
        /// <summary>Summon Ticket fragments a duplicate gives once its hero is at <see cref="EchoCap"/>.</summary>
        public const int FragmentsPerSpareEcho = 2;
        /// <summary>Fragments that join into one Summon Ticket (<see cref="SummonState.Tickets"/>).</summary>
        public const int FragmentsPerTicket = 10;

        /// <summary>The always-open Standard banner, or null when the content has none.</summary>
        public static BannerDef Standard(ContentDb c) => c.BannerOrder.FirstOrDefault(b => b?.Kind == BannerKind.Standard);

        /// <summary>Every summonable hero (Acquire Summon, Wanderer or Starter; uncut, not lost), owned or not, in id order.</summary>
        public static List<string> Summonable(ContentDb c, SaveData s) =>
            c.Classes.Values
                .Where(d => d.Team == Team.Hero && !d.IsCut && !Permadeath.IsLost(s, d.Id)
                    && (d.Acquire == Acquire.Summon || d.Acquire == Acquire.Wanderer || d.Acquire == Acquire.Starter))
                .Select(d => d.Id).OrderBy(id => id, StringComparer.Ordinal).ToList();

        /// <summary>Every hero a pull on <paramref name="banner"/> can bring, owned or not: the Standard banner's is every
        /// summonable hero; a Featured banner's is its Featured heroes first, then the Standard pool without them.</summary>
        public static List<string> Pool(ContentDb c, SaveData s, BannerDef banner)
        {
            var all = Summonable(c, s);
            if (banner == null || banner.Kind == BannerKind.Standard) return all;
            var featured = Featured(banner, all);
            return featured.Concat(all.Where(id => !featured.Contains(id, StringComparer.OrdinalIgnoreCase))).ToList();
        }

        private static List<string> Featured(BannerDef banner, List<string> summonable) =>
            banner?.Kind == BannerKind.Featured
                ? banner.Featured.Where(id => summonable.Contains(id, StringComparer.OrdinalIgnoreCase)).ToList()
                : new List<string>();

        public static int NewHeroMiss(SaveData s, BannerKind kind) =>
            s.Summon?.NewHeroMiss != null && s.Summon.NewHeroMiss.TryGetValue(kind, out var n) ? n : 0;

        /// <summary>Which hard pity the next pull on <paramref name="banner"/> from <paramref name="pool"/> is under, if any.</summary>
        public static PityDue Due(SaveData s, BannerDef banner, List<string> pool)
        {
            if (banner == null) return PityDue.None;
            if (banner.Kind == BannerKind.Featured && banner.FeaturedPity > 0 && Featured(banner, pool).Count > 0
                && (s.Summon?.FeaturedMiss ?? 0) >= banner.FeaturedPity - 1) return PityDue.Featured;
            if (banner.NewHeroPity > 0 && NewHeroMiss(s, banner.Kind) >= banner.NewHeroPity - 1 && pool.Any(id => s.Hero(id) == null))
                return PityDue.NewHero;
            return PityDue.None;
        }

        /// <summary>"New-hero pity is due." and the like, for the line beside the odds; null when no pity is due.</summary>
        public static string DueLine(PityDue due) => due switch
        {
            PityDue.Featured => "Featured pity is due: this pull brings a featured hero.",
            PityDue.NewHero => "New-hero pity is due: this pull brings a hero you don't have.",
            _ => null,
        };

        /// <summary>The next pull's odds on <paramref name="banner"/> over <paramref name="pool"/> (its <see cref="Pool"/>, or a
        /// narrower list a caller draws from): one entry per hero who can come, in pool order, the chances summing to 1. Pity
        /// due → only the heroes it brings, evenly. Standard (or no banner) → every hero evenly. Featured →
        /// <see cref="BannerDef.FeaturedShare"/> split across the featured heroes, the rest a Standard pull.
        /// <see cref="Roll"/> draws from exactly this list.</summary>
        public static List<DrawOdds> Odds(SaveData s, BannerDef banner, List<string> pool)
        {
            var odds = new List<DrawOdds>(pool.Count);
            if (pool.Count == 0) return odds;
            var featured = Featured(banner, pool);
            switch (Due(s, banner, pool))
            {
                case PityDue.Featured:
                    foreach (var id in featured) odds.Add(new DrawOdds(id, 1.0 / featured.Count));
                    return odds;
                case PityDue.NewHero:
                    var unowned = pool.Where(id => s.Hero(id) == null).ToList();
                    foreach (var id in unowned) odds.Add(new DrawOdds(id, 1.0 / unowned.Count));
                    return odds;
            }
            var rest = pool.Where(id => !featured.Contains(id, StringComparer.OrdinalIgnoreCase)).ToList();
            if (featured.Count == 0 || rest.Count == 0)
            {
                foreach (var id in pool) odds.Add(new DrawOdds(id, 1.0 / pool.Count));
                return odds;
            }
            double share = Math.Min(1.0, Math.Max(0.0, banner.FeaturedShare));
            foreach (var id in featured) odds.Add(new DrawOdds(id, share / featured.Count));
            foreach (var id in rest) odds.Add(new DrawOdds(id, (1.0 - share) / rest.Count));
            return odds;
        }

        public static List<DrawOdds> Odds(ContentDb c, SaveData s, BannerDef banner) => Odds(s, banner, Pool(c, s, banner));

        /// <summary>One draw from <paramref name="odds"/> with <paramref name="seed"/>: a point in [0, 1) walked down the list.</summary>
        public static string Roll(List<DrawOdds> odds, int seed)
        {
            if (odds == null || odds.Count == 0) return null;
            double r = new Random(seed).NextDouble();
            foreach (var o in odds)
            {
                r -= o.Chance;
                if (r < 0) return o.ClassId;
            }
            return odds[odds.Count - 1].ClassId;
        }

        /// <summary>The hero the next pull on <paramref name="banner"/> brings: seeded from the save, the banner and the pulls
        /// already made on its kind, so asking again doesn't reroll.</summary>
        public static string Roll(ContentDb c, SaveData s, BannerDef banner) =>
            Roll(Odds(c, s, banner), SeedFor(s, banner));

        public static int SeedFor(SaveData s, BannerDef banner)
        {
            int pulls = banner != null && s.Summon?.Pulls != null && s.Summon.Pulls.TryGetValue(banner.Kind, out var n) ? n : 0;
            return Wanderers.SeedFor(s, "summon:" + (banner?.Id ?? "standard"), pulls);
        }

        /// <summary>Records a pull of <paramref name="classId"/> on <paramref name="banner"/> from <paramref name="pool"/>, before the
        /// hero lands: the kind's pull count, its new-hero pity (reset by a new hero, or once the pool has no one left to be
        /// new), the Featured pity (reset by a featured hero; it carries from one Featured banner to the next) and the
        /// history, newest first, capped at <see cref="HistoryKeep"/>. A hero already owned lands here and now as an
        /// <see cref="Echo"/>.</summary>
        public static PullRecord Record(SaveData s, BannerDef banner, List<string> pool, string classId, string paid, DateTime now)
        {
            s.Summon ??= new SummonState();
            var st = s.Summon;
            var kind = banner?.Kind ?? BannerKind.Standard;
            bool isNew = s.Hero(classId) == null;
            var result = PullResult.New;
            int fragments = 0;
            if (!isNew) result = Echo(s, classId, out fragments);
            st.Pulls[kind] = (st.Pulls.TryGetValue(kind, out var p) ? p : 0) + 1;
            bool noneLeft = pool.All(id => s.Hero(id) != null || string.Equals(id, classId, StringComparison.OrdinalIgnoreCase));
            st.NewHeroMiss[kind] = isNew || noneLeft ? 0 : NewHeroMiss(s, kind) + 1;
            if (kind == BannerKind.Featured)
                st.FeaturedMiss = banner.Featured.Contains(classId, StringComparer.OrdinalIgnoreCase) ? 0 : st.FeaturedMiss + 1;
            var rec = new PullRecord
            {
                Utc = now, BannerId = banner?.Id, ClassId = classId, Result = result, Fragments = fragments, Paid = paid,
                NewHeroMiss = st.NewHeroMiss[kind], FeaturedMiss = st.FeaturedMiss,
            };
            st.History.Insert(0, rec);
            if (st.History.Count > HistoryKeep) st.History.RemoveRange(HistoryKeep, st.History.Count - HistoryKeep);
            return rec;
        }

        /// <summary>An owned hero pulled again: an Echo (+<see cref="EchoSp"/> SP to that hero) while it holds fewer than
        /// <see cref="EchoCap"/>, else <see cref="FragmentsPerSpareEcho"/> Ticket fragments, every
        /// <see cref="FragmentsPerTicket"/> of them joining into a Summon Ticket.</summary>
        public static PullResult Echo(SaveData s, string classId, out int fragments)
        {
            fragments = 0;
            var h = s.Hero(classId);
            if (h == null) return PullResult.New;
            if (h.Echoes < EchoCap)
            {
                h.Echoes++;
                h.SkillPoints += EchoSp;
                return PullResult.Echo;
            }
            s.Summon ??= new SummonState();
            fragments = FragmentsPerSpareEcho;
            s.Summon.Fragments += fragments;
            s.Summon.Tickets += s.Summon.Fragments / FragmentsPerTicket;
            s.Summon.Fragments %= FragmentsPerTicket;
            return PullResult.Fragments;
        }
    }

    public enum PityDue { None, NewHero, Featured }

    /// <summary>What a pull gave: a new hero, an Echo of an owned one, or Ticket fragments once its Echoes are full.</summary>
    public enum PullResult { New, Echo, Fragments }

    /// <summary>One pull in <see cref="SummonState.History"/>: when, which banner, who came, new or not, what paid for it,
    /// and both pity counters after it.</summary>
    public sealed class PullRecord
    {
        public DateTime Utc;
        public string BannerId;
        public string ClassId;
        public PullResult Result;
        /// <summary>Ticket fragments the pull gave (<see cref="PullResult.Fragments"/>), else 0.</summary>
        public int Fragments;
        public string Paid;
        public int NewHeroMiss;
        public int FeaturedMiss;
    }

    /// <summary>The save's summon record (GG-T1419, <see cref="Gacha"/>). Save v23: an older save starts with no pulls.</summary>
    public sealed class SummonState
    {
        /// <summary>Pulls made on each banner kind (seeds the next roll).</summary>
        public Dictionary<BannerKind, int> Pulls = new Dictionary<BannerKind, int>();
        /// <summary>Pulls in a row without a new hero, per banner kind (hard pity at <see cref="BannerDef.NewHeroPity"/>).</summary>
        public Dictionary<BannerKind, int> NewHeroMiss = new Dictionary<BannerKind, int>();
        /// <summary>Featured pulls in a row without a featured hero; carries to the next Featured banner.</summary>
        public int FeaturedMiss;
        /// <summary>The last <see cref="Gacha.HistoryKeep"/> pulls, newest first.</summary>
        public List<PullRecord> History = new List<PullRecord>();
        /// <summary>Ticket fragments not yet joined into a Ticket (under <see cref="Gacha.FragmentsPerTicket"/>; GG-T1421).</summary>
        public int Fragments;
        /// <summary>Summon Tickets earned in play, never bought (GG-T1421: joined fragments; GG-T1422: <see cref="SummonTickets"/>).</summary>
        public int Tickets;
        /// <summary>Battles won in any mode, counted toward <see cref="SummonTickets.WinsPerTicket"/> (GG-T1422).</summary>
        public int Wins;
        /// <summary>Set once the first Summon Circle visit has given <see cref="SummonTickets.StarterTickets"/> (GG-T1604).</summary>
        public bool StarterGiven;
        /// <summary>The UTC day whose first win paid <see cref="SummonTickets.PerDailyFirstWin"/> (GG-T1607); default before any.</summary>
        public DateTime FirstWinDay;
    }
}
