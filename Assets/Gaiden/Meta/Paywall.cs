using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Content;

namespace GridGame.Meta
{
    /// <summary>The paywall hooks. <c>LuckyDiamond</c> is retired (GG-T455): Diamonds come from the exchange
    /// (<see cref="DiamondExchange"/>) now; the value stays (saves store the kind) so an old save's pending job lands as a
    /// gold refund.</summary>
    public enum PaywallKind { Summon, LuckyDiamond, RestedRefill }

    /// <summary>A paid, timed job in progress (§25.18): gold is taken at the start, the effect lands when the time is up.
    /// Kept in the save so a long job survives a restart.</summary>
    public sealed class PaywallJob
    {
        public PaywallKind Kind;
        /// <summary>What the job is for (a hero class for Summon; free-form for the other kinds).</summary>
        public string Arg;
        public DateTime StartedUtc;
        public double Seconds;
        public int Paid;
        /// <summary>Diamonds spent instead of gold (the Summon Circle's random draw, GG-T343); refunded if it fizzles.</summary>
        public int Diamonds;
    }

    /// <summary>The paywall hooks (§25.18, GG-T232): every premium action prices and times itself here.</summary>
    public static class Paywall
    {
        /// <summary>Gold for a deliberate, chosen-hero Summon Circle recruit — the one hero a player fully controls,
        /// priced above the Diamond draw's blind convenience (GG-T427). A few stages' worth of grinding, not a whim.</summary>
        public const int SummonGoldPrice = 1500;
        /// <summary>Gold to skip the Rested wait on a fresh save (GG-T427); it grows by <see cref="RestedRefillGoldPerStage"/>
        /// per campaign stage cleared, so it stays about half to one stage's pay (measured ~210 g early, 400-600 g late)
        /// instead of shrinking to nothing. See <see cref="RestedPool.DiamondRefillPrice"/> for the Diamond side.</summary>
        public const int RestedRefillGoldPrice = 100;
        public const int RestedRefillGoldPerStage = 15;

        // One table, every hook's gold price and job length in one tunable spot (GG-T427 retired the 1 g / 1 s placeholders).
        private static readonly Dictionary<PaywallKind, (int gold, double seconds)> Table = new Dictionary<PaywallKind, (int, double)>
        {
            [PaywallKind.Summon] = (SummonGoldPrice, 1.0),
            [PaywallKind.RestedRefill] = (RestedRefillGoldPrice, 1.0),
        };

        /// <summary>A hook's gold price; 0 for a retired kind (LuckyDiamond, GG-T455), which can no longer start.</summary>
        public static int Cost(PaywallKind kind, SaveData s) =>
            !Table.TryGetValue(kind, out var row) ? 0
            : row.gold + (kind == PaywallKind.RestedRefill && s != null ? RestedRefillGoldPerStage * Math.Max(0, s.HighestClearedStage + 1) : 0);

        public static double Duration(PaywallKind kind, SaveData s) => Table.TryGetValue(kind, out var row) ? row.seconds : 0;

        public static bool Retired(PaywallKind kind) => !Table.ContainsKey(kind);

        /// <summary>"175 G" — the price tag every paywall button shows, plus "· ready in 2 min" when the job's wait is long
        /// enough to matter (GG-T519: a bare "· 1s" read as nothing a new player could decode).</summary>
        public static string Tag(PaywallKind kind, SaveData s) => $"{Cost(kind, s):N0} G" + WaitSuffix(Duration(kind, s));

        /// <summary>Waits shorter than this play out as a brief progress bar and go unmentioned in the price tag.</summary>
        public const double NotableWaitSeconds = 5;

        /// <summary>" · ready in 30s" / " · ready in 2 min" / " · ready in 3 h", or "" for a wait under
        /// <see cref="NotableWaitSeconds"/>.</summary>
        public static string WaitSuffix(double seconds) =>
            seconds < NotableWaitSeconds ? ""
            : seconds < 60 ? $" · ready in {seconds:0}s"
            : seconds < 3600 ? $" · ready in {Math.Ceiling(seconds / 60):0} min"
            : $" · ready in {seconds / 3600:0.#} h";

        public static PaywallJob Pending(SaveData s, PaywallKind kind) => s?.Jobs.FirstOrDefault(j => j.Kind == kind);

        /// <summary>Takes the gold and starts the clock. One job of each kind at a time.</summary>
        public static Result Start(SaveData s, PaywallKind kind, string arg, DateTime now, out PaywallJob job)
        {
            job = null;
            if (Retired(kind)) return Result.Fail($"{ShardText.Plural} come from the {ShardText.Store} now");
            if (Pending(s, kind) != null) return Result.Fail("Already under way");
            int cost = Cost(kind, s);
            if (s.Gold < cost) return Result.Fail("Not enough gold");
            s.Gold -= cost;
            job = new PaywallJob { Kind = kind, Arg = arg, StartedUtc = now, Seconds = Duration(kind, s), Paid = cost };
            s.Jobs.Add(job);
            return Result.Success();
        }

        public static double Progress(PaywallJob job, DateTime now)
        {
            if (job.Seconds <= 0) return 1;
            // The clock went backwards (device time changed): restart the wait from now, as RestedPool.Regen does, rather
            // than lock the job until real time catches up with the old start.
            if (now < job.StartedUtc) job.StartedUtc = now;
            return Math.Max(0, Math.Min(1, (now - job.StartedUtc).TotalSeconds / job.Seconds));
        }

        public static bool Ready(PaywallJob job, DateTime now) => Progress(job, now) >= 1;

        /// <summary>Lands a finished job's effect and removes it; refuses one still running.</summary>
        public static Result Finish(ContentDb c, SaveData s, PaywallJob job, DateTime now)
        {
            if (job == null || !s.Jobs.Contains(job)) return Result.Fail("No such job");
            if (!Ready(job, now)) return Result.Fail("Not done yet");
            s.Jobs.Remove(job);
            switch (job.Kind)
            {
                case PaywallKind.Summon:
                    var arrived = Summon.Arrive(c, s, job.Arg);
                    // A fizzled draw gives its whole Diamond price back as Diamonds, however it was paid (GG-T455: any gold
                    // part was exchanged into Diamonds on the way in).
                    if (!arrived.Ok && job.Diamonds > 0) s.AddItem(Crafting.DiamondId, job.Diamonds);
                    // A chosen-hero summon paid in gold alone (no Diamonds) gives its gold back when the hero can't come.
                    else if (!arrived.Ok && job.Paid > 0)
                    {
                        s.Gold += job.Paid;
                        return Result.Fail($"{arrived.Message ?? "The summon fizzled."} Refunded {job.Paid:N0} G.");
                    }
                    return arrived;
                case PaywallKind.RestedRefill: RestedPool.Fill(s, now); return Result.Success("The party is rested.");
                case PaywallKind.LuckyDiamond:
                    // Retired (GG-T455): an old save's pending one-Diamond purchase is refunded in full; the exchange
                    // sells Diamonds now, at a far better rate for the same gold.
                    s.Gold += job.Paid;
                    return Result.Success($"The old {ShardText.Short} order was refunded: {job.Paid:N0} G. {ShardText.Plural} come from the {ShardText.Store} now.");
                default: return Result.Success();
            }
        }

        /// <summary>Finishes every job whose time is up (e.g. ones left running when the app closed).</summary>
        public static List<Result> FinishReady(ContentDb c, SaveData s, DateTime now) =>
            s.Jobs.Where(j => Ready(j, now)).ToList().Select(j => Finish(c, s, j, now)).ToList();
    }
}
