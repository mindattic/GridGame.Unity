using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Content;

namespace GridGame.Meta
{
    /// <summary>
    /// The summon banners' rolls (BIBLE §25.10.3, GG-T1419): who a banner can bring (<see cref="Pool"/>), the chance of each
    /// (<see cref="Odds"/>, disclosed before purchase as GG-T503 requires, and the very list <see cref="Roll"/> draws from),
    /// a seeded roll with the Featured share, and the record of each pull (<see cref="Record"/>: the pull count
    /// per banner kind and the pull history in <see cref="SaveData.Summon"/>). Engine-free; bots and Test Mode
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

        /// <summary>The banners open at <paramref name="now"/>: each Featured banner in its window, then the Standard banner.</summary>
        public static List<BannerDef> Open(ContentDb c, DateTime now) =>
            c.BannerOrder.Where(b => b?.Kind == BannerKind.Featured && Closes(c, b, now) != null)
                .Concat(c.BannerOrder.Where(b => b?.Kind == BannerKind.Standard)).ToList();

        /// <summary>When the window of <paramref name="banner"/> open at <paramref name="now"/> closes (UTC); null when it is
        /// closed, or the always-open Standard banner. A rotation slot's windows repeat every full turn of the rotation.</summary>
        public static DateTime? Closes(ContentDb c, BannerDef banner, DateTime now)
        {
            if (banner == null || banner.Kind == BannerKind.Standard || !(banner.StartUtc is DateTime start) || now < start) return null;
            if (!banner.IsRotating) return banner.EndUtc is DateTime end && now < end ? end : (DateTime?)null;
            var slots = c.BannerOrder.Where(b => b != null && b.Kind == BannerKind.Featured && b.IsRotating && b.StartUtc != null)
                .OrderBy(b => b.Rotation).ToList();
            int turn = slots.Sum(b => b.Days);
            if (turn <= 0) return null;
            var first = slots[0].StartUtc.Value;
            double into = (now - first).TotalDays % turn, from = (start - first).TotalDays;
            if (into < from || into >= from + banner.Days) return null;
            return now.AddDays(from + banner.Days - into);
        }

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

        /// <summary>Whether the save has <paramref name="classId"/>, or it is already <paramref name="coming"/> in this batch.</summary>
        private static bool Has(SaveData s, string classId, IEnumerable<string> coming) =>
            s.Hero(classId) != null || (coming != null && coming.Contains(classId, StringComparer.OrdinalIgnoreCase));

        /// <summary>The next pull's odds on <paramref name="banner"/> over <paramref name="pool"/> (its <see cref="Pool"/>, or a
        /// narrower list a caller draws from): one entry per hero who can come, in pool order, the chances summing to 1.
        /// Standard (or no banner) → every hero evenly. Featured →
        /// <see cref="BannerDef.FeaturedShare"/> split across the featured heroes, the rest a Standard pull.
        /// <see cref="Roll"/> draws from exactly this list.</summary>
        public static List<DrawOdds> Odds(SaveData s, BannerDef banner, List<string> pool)
        {
            var odds = new List<DrawOdds>(pool.Count);
            if (pool.Count == 0) return odds;
            var featured = Featured(banner, pool);
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
        /// hero lands: the kind's pull count and the
        /// history, newest first, capped at <see cref="HistoryKeep"/>. A hero already owned lands here and now as an
        /// <see cref="Echo"/>. In a multi-pull (<see cref="Pull"/>) <paramref name="coming"/> holds the heroes rolled before
        /// this one: they count as owned, and an Echo is only foreseen here; it lands with the job (<see cref="Land"/>).</summary>
        public static PullRecord Record(SaveData s, BannerDef banner, string classId, string paid, DateTime now, IList<string> coming = null)
        {
            s.Summon ??= new SummonState();
            var st = s.Summon;
            var kind = banner?.Kind ?? BannerKind.Standard;
            bool isNew = !Has(s, classId, coming);
            var result = PullResult.New;
            int fragments = 0;
            if (!isNew) result = coming == null ? Echo(s, classId, out fragments) : Foresee(s, classId, coming, out fragments);
            st.Pulls[kind] = (st.Pulls.TryGetValue(kind, out var p) ? p : 0) + 1;
            var rec = new PullRecord
            {
                Utc = now, BannerId = banner?.Id, ClassId = classId, Result = result, Fragments = fragments, Paid = paid,
            };
            st.History.Insert(0, rec);
            if (st.History.Count > HistoryKeep) st.History.RemoveRange(HistoryKeep, st.History.Count - HistoryKeep);
            return rec;
        }

        /// <summary>What <see cref="Echo"/> will give when a duplicate in a multi-pull lands: the hero's Echoes so far plus the
        /// copies of it <paramref name="coming"/> before (the first of them is the hero itself when it isn't owned yet).</summary>
        private static PullResult Foresee(SaveData s, string classId, IList<string> coming, out int fragments)
        {
            var h = s.Hero(classId);
            int echoes = (h?.Echoes ?? 0) + coming.Count(id => string.Equals(id, classId, StringComparison.OrdinalIgnoreCase)) - (h == null ? 1 : 0);
            fragments = echoes < EchoCap ? 0 : FragmentsPerSpareEcho;
            return echoes < EchoCap ? PullResult.Echo : PullResult.Fragments;
        }

        /// <summary>A pull's price on <paramref name="banner"/> for this save: whole pulls in Summon Tickets while it holds
        /// them (when the banner takes them), the rest in Shards (paid through <see cref="DiamondExchange"/>, any shortfall
        /// in gold).</summary>
        public static (int tickets, int shards) Price(SaveData s, BannerDef banner, int count)
        {
            int ticketPulls = banner.AcceptsTickets && banner.PullTickets > 0 ? Math.Min(count, SummonTickets.Have(s) / banner.PullTickets) : 0;
            return (ticketPulls * banner.PullTickets, (count - ticketPulls) * banner.PullShards);
        }

        public static Result CanPull(ContentDb c, SaveData s, BannerDef banner, int count)
        {
            if (s == null) return Result.Fail("No save");
            if (s.TestMode) return Result.Fail("Not in Test Mode");
            if (banner == null) return Result.Fail("No such banner");
            if (count != 1 && count != banner.MultiCount) return Result.Fail($"A pull is x1 or x{banner.MultiCount}");
            var allowed = LootBoxPolicy.Allows(s, banner, count);
            if (!allowed.Ok) return allowed;
            if (Pool(c, s, banner).Count == 0) return Result.Fail("No one is left to answer");
            if (Paywall.Pending(s, PaywallKind.Summon) != null) return Result.Fail("A summoning is already under way");
            var (_, shards) = Price(s, banner, count);
            return shards > 0 ? DiamondExchange.CanPay(s, shards) : Result.Success();
        }

        /// <summary>A x1 or x<see cref="BannerDef.MultiCount"/> pull on <paramref name="banner"/> (GG-T1424): pays Summon
        /// Tickets first (earned, then bought), then Shards, then the gold shortfall; rolls and records every pull now, in
        /// order, and starts one Summon job holding them all (<see cref="PaywallJob.Batch"/>). The heroes land in that order
        /// when <see cref="Paywall.Finish"/> runs (<see cref="Land"/>). Takes nothing when it fails.</summary>
        public static Result Pull(ContentDb c, SaveData s, BannerDef banner, int count, DateTime now, out PaywallJob job)
        {
            job = null;
            var ok = CanPull(c, s, banner, count);
            if (!ok.Ok) return ok;
            var (tickets, shards) = Price(s, banner, count);
            (int diamonds, int gold) split = (0, 0);
            if (shards > 0)
            {
                var paid = DiamondExchange.Pay(s, shards, out split);
                if (!paid.Ok) return paid;
            }
            for (int i = 0; i < tickets; i++) SummonTickets.Spend(s);
            int ticketPulls = tickets > 0 ? tickets / banner.PullTickets : 0;
            var pool = Pool(c, s, banner);
            var coming = new List<string>(count);
            for (int i = 0; i < count; i++)
            {
                var id = Roll(Odds(s, banner, pool), SeedFor(s, banner));
                Record(s, banner, id, i < ticketPulls ? TicketText.Of(banner.PullTickets) : DiamondExchange.Label(banner.PullShards), now, coming);
                coming.Add(id);
            }
            job = new PaywallJob
            {
                Kind = PaywallKind.Summon, Arg = banner.Id, Batch = coming, TicketPulls = ticketPulls, StartedUtc = now,
                Seconds = Paywall.Duration(PaywallKind.Summon, s), Diamonds = shards, Tickets = tickets, Paid = split.gold,
            };
            s.Jobs.Add(job);
            return Result.Success();
        }

        /// <summary>Lands a finished banner pull's heroes in pull order: a hero the save lacks arrives
        /// (<see cref="Summon.Arrive"/>), one it has becomes an <see cref="Echo"/>. A pull that fizzles gives back what paid
        /// for it: the ticket pulls come first, so Tickets for those, Shards for the rest.</summary>
        internal static Result Land(ContentDb c, SaveData s, PaywallJob job)
        {
            var lines = new List<string>();
            int shardPulls = job.Batch.Count - job.TicketPulls;
            for (int i = 0; i < job.Batch.Count; i++)
            {
                var id = job.Batch[i];
                string name = c.TryClass(id)?.Name ?? id;
                if (s.Hero(id) != null)
                {
                    lines.Add(Echo(s, id, out var fragments) == PullResult.Echo
                        ? $"{name}: {EchoName} (+{EchoSp} SP)"
                        : $"{name}: {fragments} {TicketText.Fragments}");
                    continue;
                }
                var arrived = Summon.Arrive(c, s, id);
                if (arrived.Ok) { lines.Add(arrived.Message); continue; }
                if (i < job.TicketPulls)
                {
                    int t = job.Tickets / Math.Max(1, job.TicketPulls);
                    SummonTickets.Earn(s, t);
                    lines.Add($"{arrived.Message}. Refunded {TicketText.Of(t)}.");
                }
                else
                {
                    int d = job.Diamonds / Math.Max(1, shardPulls);
                    DiamondExchange.Add(s, d);
                    lines.Add($"{arrived.Message}. Refunded {ShardText.Of(d)}.");
                }
            }
            return Result.Success(string.Join("\n", lines));
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

    /// <summary>What a pull gave: a new hero, an Echo of an owned one, or Ticket fragments once its Echoes are full.</summary>
    public enum PullResult { New, Echo, Fragments }

    /// <summary>One pull in <see cref="SummonState.History"/>: when, which banner, who came, new or not, and what paid for it.</summary>
    public sealed class PullRecord
    {
        public DateTime Utc;
        public string BannerId;
        public string ClassId;
        public PullResult Result;
        /// <summary>Ticket fragments the pull gave (<see cref="PullResult.Fragments"/>), else 0.</summary>
        public int Fragments;
        public string Paid;
    }

    /// <summary>The save's summon record (GG-T1419, <see cref="Gacha"/>). Save v23: an older save starts with no pulls.</summary>
    public sealed class SummonState
    {
        /// <summary>Pulls made on each banner kind (seeds the next roll).</summary>
        public Dictionary<BannerKind, int> Pulls = new Dictionary<BannerKind, int>();
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
