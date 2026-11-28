using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Content;
using GridGame.Meta;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>§25.10.3 (GG-T1424, GG-T1617): a x1 or x10 banner pull's payment, its in-batch pity and Echoes, and how its job lands.</summary>
    public class GachaPullTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 12, 2, 12, 0, 0, DateTimeKind.Utc);

        private static BannerDef Banner => Gacha.Standard(Real);

        /// <summary>A save with exactly <paramref name="tickets"/> earned Tickets, <paramref name="shards"/> Shards and <paramref name="gold"/> gold.</summary>
        private static SaveData Funded(string name, int tickets, int shards, int gold)
        {
            DiamondExchange.Wallet = null;
            var s = Progression.NewGame(Real, name);
            s.Summon ??= new SummonState();
            s.Summon.Tickets = tickets;
            DiamondExchange.Add(s, -DiamondExchange.Have(s));
            DiamondExchange.Add(s, shards);
            s.Gold = gold;
            return s;
        }

        [Test]
        public void A_banner_ships_its_multi_pull_count_and_only_x1_or_that_count_is_a_pull()
        {
            var banner = Banner;
            Assert.That(banner.MultiCount, Is.EqualTo(10), "banners.json");
            var s = Funded("P0", 100, 100000, 100000);
            Assert.That(Gacha.CanPull(Real, s, banner, 1).Ok, Is.True);
            Assert.That(Gacha.CanPull(Real, s, banner, banner.MultiCount).Ok, Is.True);
            Assert.That(Gacha.CanPull(Real, s, banner, 5).Ok, Is.False);
            Assert.That(Gacha.CanPull(Real, s, banner, 0).Ok, Is.False);
        }

        [Test]
        public void The_policy_hook_allows_every_pull_and_a_pull_asks_it_first()
        {
            var banner = Banner;
            var s = Funded("P1", 100, 0, 0);
            Assert.That(LootBoxPolicy.Allows(s, banner, 1).Ok, Is.True);
            Assert.That(LootBoxPolicy.Allows(s, banner, banner.MultiCount).Ok, Is.True);
            s.TestMode = true;
            Assert.That(Gacha.CanPull(Real, s, banner, 1).Ok, Is.False, "Test Mode refuses before anything is priced");
        }

        [Test]
        public void A_pull_is_paid_in_Tickets_first_and_takes_no_Shards_while_Tickets_last()
        {
            var banner = Banner;
            var s = Funded("P2", 3, 500, 1000);
            Assert.That(Gacha.Price(s, banner, 1), Is.EqualTo((banner.PullTickets, 0)));
            var r = Gacha.Pull(Real, s, banner, 1, T0, out var job);
            Assert.That(r.Ok, Is.True, r.Message);
            Assert.That(SummonTickets.Have(s), Is.EqualTo(3 - banner.PullTickets));
            Assert.That(DiamondExchange.Have(s), Is.EqualTo(500));
            Assert.That(s.Gold, Is.EqualTo(1000));
            Assert.That(job.Batch.Count, Is.EqualTo(1));
            Assert.That(job.TicketPulls, Is.EqualTo(1));
            Assert.That(job.Tickets, Is.EqualTo(banner.PullTickets));
            Assert.That(job.Diamonds, Is.EqualTo(0));
            Assert.That(s.Jobs, Does.Contain(job));
            Assert.That(s.Summon.History[0].Paid, Is.EqualTo(TicketText.Of(banner.PullTickets)));
        }

        [Test]
        public void A_x10_pays_Tickets_then_Shards_then_gold_for_the_shortfall()
        {
            var banner = Banner;
            int n = banner.MultiCount;
            var s = Funded("P3", 4, 100, DiamondExchange.GoldFor(500) + 7);
            var (tickets, shards) = Gacha.Price(s, banner, n);
            Assert.That(tickets, Is.EqualTo(4 * banner.PullTickets));
            Assert.That(shards, Is.EqualTo((n - 4) * banner.PullShards));

            var r = Gacha.Pull(Real, s, banner, n, T0, out var job);
            Assert.That(r.Ok, Is.True, r.Message);
            Assert.That(SummonTickets.Have(s), Is.EqualTo(0));
            Assert.That(DiamondExchange.Have(s), Is.EqualTo(0), "the Shards on hand go first");
            Assert.That(s.Gold, Is.EqualTo(7), "gold covers only what the Shards did not");
            Assert.That(job.Batch.Count, Is.EqualTo(n));
            Assert.That(job.TicketPulls, Is.EqualTo(4));
            Assert.That(job.Tickets, Is.EqualTo(tickets));
            Assert.That(job.Diamonds, Is.EqualTo(shards));
            Assert.That(job.Paid, Is.EqualTo(DiamondExchange.GoldFor(shards - 100)));
            Assert.That(s.Summon.History.Count, Is.EqualTo(n));
            Assert.That(s.Summon.Pulls[BannerKind.Standard], Is.EqualTo(n));
            Assert.That(s.Jobs.Count(j => j.Kind == PaywallKind.Summon), Is.EqualTo(1), "one job holds the batch");
        }

        [Test]
        public void A_refused_pull_takes_nothing()
        {
            var banner = Banner;
            int n = banner.MultiCount;
            var s = Funded("P4", 2, 50, DiamondExchange.GoldFor(banner.PullShards * (n - 2) - 50) - 1);
            int gold = s.Gold;
            var r = Gacha.Pull(Real, s, banner, n, T0, out var job);
            Assert.That(r.Ok, Is.False);
            Assert.That(job, Is.Null);
            Assert.That(SummonTickets.Have(s), Is.EqualTo(2));
            Assert.That(DiamondExchange.Have(s), Is.EqualTo(50));
            Assert.That(s.Gold, Is.EqualTo(gold));
            Assert.That(s.Jobs, Is.Empty);
            Assert.That(s.Summon.History, Is.Empty);
            Assert.That(s.Summon.Pulls, Is.Empty);

            Assert.That(Gacha.Pull(Real, s, banner, 5, T0, out job).Ok, Is.False, "a wrong count is refused too");
            Assert.That(SummonTickets.Have(s), Is.EqualTo(2));
        }

        [Test]
        public void A_second_pull_is_refused_while_a_summoning_is_under_way()
        {
            var banner = Banner;
            var s = Funded("P5", 5, 0, 0);
            Assert.That(Gacha.Pull(Real, s, banner, 1, T0, out _).Ok, Is.True);
            Assert.That(Gacha.Pull(Real, s, banner, 1, T0, out _).Ok, Is.False);
            Assert.That(SummonTickets.Have(s), Is.EqualTo(5 - banner.PullTickets));
        }

        [Test]
        public void A_repeat_inside_a_batch_is_recorded_as_an_Echo_without_touching_the_roster()
        {
            var s = Progression.NewGame(Real, "P7");
            var pool = new List<string> { "x_new", "y_new" };
            var banner = new BannerDef { Id = "std", Kind = BannerKind.Standard };
            var coming = new List<string>();
            var first = Gacha.Record(s, banner, "x_new", "1 Ticket", T0, coming);
            coming.Add("x_new");
            Assert.That(first.Result, Is.EqualTo(PullResult.New));
            var again = Gacha.Record(s, banner, "x_new", "1 Ticket", T0, coming);
            Assert.That(again.Result, Is.EqualTo(PullResult.Echo));
            Assert.That(s.Hero("x_new"), Is.Null, "nothing lands until the job finishes");
        }

        [Test]
        public void A_finished_batch_lands_in_order_a_repeat_becoming_an_Echo_after_the_hero_arrives()
        {
            var s = Progression.NewGame(Real, "P8");
            var fresh = Gacha.Summonable(Real, s).First(id => s.Hero(id) == null);
            var job = new PaywallJob
            {
                Kind = PaywallKind.Summon, Arg = "standard", Batch = new List<string> { fresh, fresh }, TicketPulls = 2,
                Tickets = 2, StartedUtc = T0, Seconds = 1,
            };
            s.Jobs.Add(job);
            int recruits = s.Recruits;
            var r = Paywall.Finish(Real, s, job, T0.AddDays(1));
            Assert.That(r.Ok, Is.True, r.Message);
            Assert.That(s.Hero(fresh), Is.Not.Null);
            Assert.That(s.Hero(fresh).Echoes, Is.EqualTo(1));
            Assert.That(s.Recruits, Is.EqualTo(recruits + 1));
            var lines = r.Message.Split('\n');
            Assert.That(lines.Length, Is.EqualTo(2));
            Assert.That(lines[0], Does.Contain("joins you"));
            Assert.That(lines[1], Does.Contain(Gacha.EchoName));
            Assert.That(s.Jobs, Does.Not.Contain(job));
        }

        [Test]
        public void A_fizzled_pull_refunds_its_own_price_Ticket_pulls_first()
        {
            var s = Funded("P9", 0, 0, 0);
            var job = new PaywallJob
            {
                Kind = PaywallKind.Summon, Arg = "standard", Batch = new List<string> { "ghost_a", "ghost_b", "ghost_c" },
                TicketPulls = 1, Tickets = 1, Diamonds = 200, StartedUtc = T0, Seconds = 1,
            };
            s.Jobs.Add(job);
            var r = Paywall.Finish(Real, s, job, T0.AddDays(1));
            Assert.That(r.Ok, Is.True);
            Assert.That(SummonTickets.Have(s), Is.EqualTo(1), "the first pull was paid in a Ticket");
            Assert.That(DiamondExchange.Have(s), Is.EqualTo(200), "the other two gave back their Shards, 100 each");
            Assert.That(r.Message.Split('\n').Length, Is.EqualTo(3));
        }
    }
}
