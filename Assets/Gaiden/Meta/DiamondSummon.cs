using System;
using System.Collections.Generic;
using GridGame.Core.Content;

namespace GridGame.Meta
{
    /// <summary>
    /// The Summon Circle's random draw for Diamonds (§25.10.2, GG-T343). Owner, 2026-11-07: "another way is to simply
    /// summon them randomly with Diamonds monetized currency". It costs <see cref="Price"/> Diamonds (<c>mat_diamond</c>),
    /// paid through <see cref="DiamondExchange.Pay(SaveData, int, int)"/> — Diamonds first, any shortfall in gold (GG-T455) — and draws one unowned hero from the same pool
    /// wanderers come from and go back to (<see cref="Wanderers.Pool"/>). The pick is seeded from the save and its recruit
    /// count, fixed when the summoning starts, and lands through the ordinary timed Summon job (<see cref="Paywall.Finish"/>),
    /// so it plays out over time with a reveal. The gold, deliberate summon is unchanged; bots never draw.
    /// </summary>
    public static class DiamondSummon
    {
        /// <summary>Diamonds per draw — the one tuning spot. 100 Diamonds = $1.00 = 600 g at the exchange (GG-T455), under
        /// half the 1,500 g chosen-hero summon since it's a blind draw.</summary>
        public const int Price = 100;

        /// <summary>"100 Diamonds (or 600 G) · 1s" for the button.</summary>
        public static string Tag(SaveData s) => $"{DiamondExchange.Label(Price)} · {Paywall.Duration(PaywallKind.Summon, s):0.#}s";

        /// <summary>Who could come: the wanderer pool (unowned heroes with no other way in).</summary>
        public static List<string> Pool(ContentDb c, SaveData s) => Wanderers.Pool(c, s);

        public static Result CanDraw(ContentDb c, SaveData s)
        {
            if (s.TestMode) return Result.Fail("Not in Test Mode");
            if (Pool(c, s).Count == 0) return Result.Fail("No one is left to answer");
            if (Paywall.Pending(s, PaywallKind.Summon) != null) return Result.Fail("A summoning is already under way");
            return DiamondExchange.CanPay(s, Price);
        }

        /// <summary>The hero the next draw brings for this save (deterministic: the save's creation time and its recruit count).</summary>
        public static string Pick(ContentDb c, SaveData s)
        {
            var pool = Pool(c, s);
            if (pool.Count == 0) return null;
            return pool[new Random(Wanderers.SeedFor(s, "diamond-summon", s.Recruits)).Next(pool.Count)];
        }

        /// <summary>Pays the Diamonds (gold for any shortfall) and starts the summoning; the hero arrives when
        /// <see cref="Paywall.Finish"/> lands it. The job records the whole price in Diamonds (refunded as Diamonds if it
        /// fizzles) and the gold part of the payment in <c>Paid</c>.</summary>
        public static Result Begin(ContentDb c, SaveData s, DateTime now, out PaywallJob job)
        {
            job = null;
            var ok = CanDraw(c, s);
            if (!ok.Ok) return ok;
            var pick = Pick(c, s);
            var paid = DiamondExchange.Pay(s, Price, out var split);
            if (!paid.Ok) return paid;
            job = new PaywallJob { Kind = PaywallKind.Summon, Arg = pick, StartedUtc = now, Seconds = Paywall.Duration(PaywallKind.Summon, s), Diamonds = Price, Paid = split.gold };
            s.Jobs.Add(job);
            return Result.Success();
        }
    }
}
