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

        /// <summary>"100 Diamonds (or 600 G)" for the button, plus the wait when it's long enough to matter (GG-T519).</summary>
        public static string Tag(SaveData s) => DiamondExchange.Label(Price) + Paywall.WaitSuffix(Paywall.Duration(PaywallKind.Summon, s));

        /// <summary>Who could come: the wanderer pool (unowned heroes with no other way in).</summary>
        public static List<string> Pool(ContentDb c, SaveData s) => Wanderers.Pool(c, s);

        /// <summary>The draw's odds, disclosed before purchase (GG-T503, App Store 3.1.1 / Google Play "Loot boxes"): every
        /// hero the next draw can bring and the chance of each. This is the list <see cref="Pick"/> draws from, one entry
        /// per hero, each equally likely, so what the Summon Circle shows is exactly what the draw does.</summary>
        public static List<DrawOdds> Odds(ContentDb c, SaveData s)
        {
            return Gacha.Odds(s, Gacha.Standard(c), Pool(c, s));
        }

        /// <summary>"Each of these 23 heroes: 1 in 23 (4.3%)" — the one-line disclosure beside the Draw button.</summary>
        public static string OddsLine(ContentDb c, SaveData s)
        {
            int n = Odds(c, s).Count;
            if (n == 0) return "No one is left to answer.";
            if (n == 1) return "The one hero left: certain (100%)";
            return $"Each of these {n} heroes: 1 in {n} ({OddsText.Percent(1.0 / n)})";
        }

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
            // Draws from the disclosed odds (GG-T503) through the banner roll (GG-T1419).
            return Gacha.Roll(Odds(c, s), Wanderers.SeedFor(s, "diamond-summon", s.Recruits));
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
            Gacha.Record(s, Gacha.Standard(c), Pool(c, s), pick, DiamondExchange.Label(Price), now);
            job = new PaywallJob { Kind = PaywallKind.Summon, Arg = pick, StartedUtc = now, Seconds = Paywall.Duration(PaywallKind.Summon, s), Diamonds = Price, Paid = split.gold };
            s.Jobs.Add(job);
            return Result.Success();
        }
    }

    /// <summary>One hero a draw can bring and its chance, about 1 in <see cref="OneIn"/> (GG-T503, GG-T1419).</summary>
    public readonly struct DrawOdds
    {
        public readonly string ClassId;
        public readonly double Chance;
        public DrawOdds(string classId, int oneIn) { ClassId = classId; Chance = oneIn > 0 ? 1.0 / oneIn : 0.0; }
        public DrawOdds(string classId, double chance) { ClassId = classId; Chance = chance; }
        public int OneIn => Chance > 0 ? (int)Math.Round(1.0 / Chance) : 0;
        /// <summary>"1 in 23 (4.3%)".</summary>
        public string Label => $"1 in {OneIn} ({OddsText.Percent(Chance)})";
    }

    /// <summary>How odds read on screen (GG-T503): a percentage to one decimal, culture-free ("4.3%", "35%").</summary>
    public static class OddsText
    {
        public static string Percent(double p) =>
            Math.Round(p * 100.0, 1).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "%";
    }
}
