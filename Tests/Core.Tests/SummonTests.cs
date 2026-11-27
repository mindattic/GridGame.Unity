using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Meta;
using Newtonsoft.Json;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>§25.10.2 (GG-T343): the Summon Circle's random draw for Diamonds, beside the gold, deliberate summon.</summary>
    public class SummonTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc);

        /// <summary>A new save holding enough Diamonds for <paramref name="draws"/> draws and no gold, so nothing is
        /// exchanged unless a test gives it gold (GG-T455).</summary>
        private static SaveData WithDiamonds(string name, int draws)
        {
            var s = Progression.NewGame(Real, name);
            s.Gold = 0;
            if (draws > 0) s.AddItem(Crafting.DiamondId, draws * DiamondSummon.Price);
            return s;
        }

        [Test]
        public void A_draw_costs_its_diamond_price_and_no_gold_when_diamonds_cover_it()
        {
            var s = WithDiamonds("D1", 2);
            s.Gold = 500;
            Assert.That(DiamondSummon.Tag(s), Is.EqualTo($"{DiamondSummon.Price} Diamonds (or {DiamondExchange.GoldFor(DiamondSummon.Price):N0} G)")); // GG-T519: no bare "· 1s"
            Assert.That(DiamondSummon.Begin(Real, s, T0, out var job).Ok, Is.True);
            Assert.That(s.Count(Crafting.DiamondId), Is.EqualTo(DiamondSummon.Price));
            Assert.That(s.Gold, Is.EqualTo(500), "Diamonds, not gold");
            Assert.That(job.Kind, Is.EqualTo(PaywallKind.Summon));
            Assert.That(job.Diamonds, Is.EqualTo(DiamondSummon.Price));
            Assert.That(job.Paid, Is.EqualTo(0));
        }

        [Test]
        public void A_draw_with_no_diamonds_is_paid_in_gold_at_the_exchange_rate()
        {
            var s = WithDiamonds("D1g", 0);
            s.Gold = DiamondExchange.GoldFor(DiamondSummon.Price) + 7;
            Assert.That(DiamondSummon.Begin(Real, s, T0, out var job).Ok, Is.True);
            Assert.That(s.Gold, Is.EqualTo(7));
            Assert.That(job.Diamonds, Is.EqualTo(DiamondSummon.Price), "refunded as Diamonds if it fizzles");
            Assert.That(job.Paid, Is.EqualTo(DiamondExchange.GoldFor(DiamondSummon.Price)));
        }

        [Test]
        public void It_draws_from_the_wanderer_pool_and_the_hero_arrives_when_the_summoning_lands()
        {
            var s = WithDiamonds("D2", 1);
            var pool = Wanderers.Pool(Real, s);
            int roster = s.Roster.Count;
            Assert.That(DiamondSummon.Begin(Real, s, T0, out var job).Ok, Is.True);
            Assert.That(pool, Does.Contain(job.Arg));
            Assert.That(Summon.Ids(Real), Does.Not.Contain(job.Arg), "not one of the gold summons");
            Assert.That(Paywall.Finish(Real, s, job, T0.AddSeconds(0.5)).Ok, Is.False, "plays out over time");
            Assert.That(s.Hero(job.Arg), Is.Null);
            var r = Paywall.Finish(Real, s, job, T0.AddSeconds(1));
            Assert.That(r.Ok, Is.True);
            Assert.That(s.Hero(job.Arg), Is.Not.Null);
            Assert.That(s.Roster.Count, Is.EqualTo(roster + 1));
            Assert.That(Wanderers.Pool(Real, s), Does.Not.Contain(job.Arg), "owned heroes leave the pool");
        }

        [Test]
        public void The_pick_is_seeded_from_the_save()
        {
            var a = WithDiamonds("D3", 1); var b = WithDiamonds("D3", 1);
            b.CreatedUtc = a.CreatedUtc;
            var pick = DiamondSummon.Pick(Real, a);
            Assert.That(pick, Is.Not.Null);
            Assert.That(DiamondSummon.Pick(Real, b), Is.EqualTo(pick));
            Assert.That(DiamondSummon.Pick(Real, a), Is.EqualTo(pick), "asking again doesn't change it");
            Assert.That(DiamondSummon.Begin(Real, a, T0, out var job).Ok, Is.True);
            Assert.That(job.Arg, Is.EqualTo(pick), "the summoning brings the hero picked");
        }

        [Test]
        public void The_disclosed_odds_are_the_pool_the_draw_picks_from()
        {
            // GG-T503: what the Summon Circle shows before purchase is exactly what the draw does.
            var s = WithDiamonds("D3o", 1);
            var pool = DiamondSummon.Pool(Real, s);
            var odds = DiamondSummon.Odds(Real, s);
            Assert.That(pool.Count, Is.GreaterThan(1), "the real content has several wanderers");
            Assert.That(odds.Select(o => o.ClassId), Is.EqualTo(pool), "one entry per hero in the draw's pool, in order");
            Assert.That(odds.Select(o => o.ClassId).Distinct().Count(), Is.EqualTo(odds.Count));
            foreach (var o in odds)
            {
                Assert.That(o.OneIn, Is.EqualTo(pool.Count));
                Assert.That(o.Chance, Is.EqualTo(1.0 / pool.Count).Within(1e-12));
            }
            Assert.That(odds.Sum(o => o.Chance), Is.EqualTo(1.0).Within(1e-9), "the chances cover every outcome");
            Assert.That(odds.Select(o => o.ClassId), Does.Contain(DiamondSummon.Pick(Real, s)), "the pick is one of the listed heroes");
            Assert.That(DiamondSummon.OddsLine(Real, s), Is.EqualTo($"Each of these {pool.Count} heroes: 1 in {pool.Count} ({OddsText.Percent(1.0 / pool.Count)})"));
            Assert.That(DiamondSummon.Begin(Real, s, T0, out var job).Ok, Is.True);
            Assert.That(odds.Select(o => o.ClassId), Does.Contain(job.Arg), "the summoning brings a listed hero");
        }

        [Test]
        public void Every_seed_draws_only_listed_heroes_spread_across_the_pool()
        {
            var s = WithDiamonds("D3s", 0);
            var listed = DiamondSummon.Odds(Real, s).Select(o => o.ClassId).ToList();
            var seen = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < 400; i++)
            {
                s.Recruits = i;
                var pick = DiamondSummon.Pick(Real, s);
                Assert.That(listed, Does.Contain(pick));
                seen.Add(pick);
            }
            Assert.That(seen.Count, Is.GreaterThan(1), "not one fixed hero whatever the save");
        }

        [Test]
        public void Odds_read_as_a_culture_free_percentage()
        {
            Assert.That(OddsText.Percent(1.0 / 23), Is.EqualTo("4.3%"));
            Assert.That(OddsText.Percent(0.35f), Is.EqualTo("35%"));
            Assert.That(OddsText.Percent(1.0), Is.EqualTo("100%"));
            Assert.That(new DrawOdds("x", 23).Label, Is.EqualTo("1 in 23 (4.3%)"));
        }

        [Test]
        public void No_diamond_no_draw_and_one_summoning_at_a_time()
        {
            var none = WithDiamonds("D4", 0);
            none.Gold = DiamondExchange.GoldFor(DiamondSummon.Price) - 1;
            Assert.That(DiamondSummon.Begin(Real, none, T0, out _).Message, Does.Contain("Diamonds"));
            Assert.That(none.Jobs, Is.Empty);

            var s = WithDiamonds("D5", 3);
            Assert.That(DiamondSummon.Begin(Real, s, T0, out _).Ok, Is.True);
            Assert.That(DiamondSummon.Begin(Real, s, T0, out _).Ok, Is.False, "a summoning is under way");
            Assert.That(s.Count(Crafting.DiamondId), Is.EqualTo(2 * DiamondSummon.Price));
            var gold = Summon.Pool(Real, s).First(d => s.Hero(d.Id) == null);
            s.Gold = Paywall.SummonGoldPrice + 200;
            Assert.That(Summon.BeginRecruit(Real, s, gold.Id, T0, out _).Ok, Is.False, "the gold summon shares the circle");
        }

        [Test]
        public void A_fizzled_draw_gives_its_diamonds_back()
        {
            var s = WithDiamonds("D6", 1);
            Assert.That(DiamondSummon.Begin(Real, s, T0, out var job).Ok, Is.True);
            Summon.Join(Real, s, job.Arg); // they turned up some other way in the meantime
            Assert.That(Paywall.Finish(Real, s, job, T0.AddSeconds(1)).Ok, Is.False);
            Assert.That(s.Count(Crafting.DiamondId), Is.EqualTo(DiamondSummon.Price));
        }

        [Test]
        public void A_draw_left_running_survives_a_save_round_trip()
        {
            var s = WithDiamonds("D7", 1);
            Assert.That(DiamondSummon.Begin(Real, s, T0, out var job).Ok, Is.True);
            var json = JsonConvert.SerializeObject(s, ContentLoader.Settings);
            var back = SaveMigrations.Migrate(JsonConvert.DeserializeObject<SaveData>(json, ContentLoader.Settings));
            Assert.That(back.Jobs.Single().Diamonds, Is.EqualTo(DiamondSummon.Price));
            Assert.That(Paywall.FinishReady(Real, back, T0.AddMinutes(1)).Single().Ok, Is.True);
            Assert.That(back.Hero(job.Arg), Is.Not.Null);
        }

        [Test]
        public void Test_mode_and_the_camp_routine_never_spend_diamonds()
        {
            var t = WithDiamonds("D8", 2); t.TestMode = true;
            Assert.That(DiamondSummon.Begin(Real, t, T0, out _).Ok, Is.False);
            var s = WithDiamonds("D9", 5);
            s.Gold = 100000;
            for (int i = 0; i < 5; i++) CampRoutine.Run(Real, s);
            Assert.That(s.Count(Crafting.DiamondId), Is.EqualTo(5 * DiamondSummon.Price));
        }

        [Test]
        public void The_gold_summon_is_unchanged()
        {
            var s = WithDiamonds("D10", 1);
            var pick = Summon.Pool(Real, s).First(d => s.Hero(d.Id) == null);
            s.Gold = Paywall.SummonGoldPrice + 5;
            Assert.That(Summon.Recruit(Real, s, pick.Id).Ok, Is.True);
            Assert.That(s.Gold, Is.EqualTo(5));
            Assert.That(s.Count(Crafting.DiamondId), Is.EqualTo(DiamondSummon.Price));
        }

        // GG-T1608: Summon Tickets (§25.10.3, GG-T1422/T1604/T1607).

        [Test]
        public void The_starter_Tickets_are_given_once_per_save()
        {
            var s = WithDiamonds("T1", 0);
            Assert.That(SummonTickets.Starter(s), Is.EqualTo(SummonTickets.StarterTickets));
            Assert.That(SummonTickets.Starter(s), Is.EqualTo(0), "the second visit gives nothing");
            Assert.That(s.Summon.Tickets, Is.EqualTo(SummonTickets.StarterTickets));
            Assert.That(WithDiamonds("T1b", 0).Summon?.Tickets ?? 0, Is.EqualTo(0), "another save has its own");
        }

        [Test]
        public void A_first_clear_pays_one_and_a_chapter_stage_five_more()
        {
            var s = WithDiamonds("T2", 0);
            var chapter = Real.Campaign.Chapters.First(ch => ch?.Stage != null).Stage;
            var plain = Real.CampaignStages().Select(x => x.Id).First(id => Real.Campaign.Chapters.All(ch => ch?.Stage != id));
            Assert.That(SummonTickets.AfterFirstClear(Real, s, plain), Is.EqualTo(SummonTickets.PerFirstClear));
            Assert.That(SummonTickets.AfterFirstClear(Real, s, chapter), Is.EqualTo(SummonTickets.PerFirstClear + SummonTickets.PerChapter));
            Assert.That(s.Summon.Tickets, Is.EqualTo(2 * SummonTickets.PerFirstClear + SummonTickets.PerChapter));
        }

        [Test]
        public void Every_fifth_win_pays_a_Ticket()
        {
            var s = WithDiamonds("T3", 0);
            var paid = Enumerable.Range(0, 10).Select(_ => SummonTickets.AfterWin(s)).ToList();
            Assert.That(paid, Is.EqualTo(new[] { 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 }));
            Assert.That(s.Summon.Tickets, Is.EqualTo(2));
        }

        [Test]
        public void The_first_win_of_each_UTC_day_pays_one_Ticket()
        {
            var s = WithDiamonds("T4", 0);
            Assert.That(SummonTickets.DailyFirstWin(s, T0), Is.EqualTo(SummonTickets.PerDailyFirstWin));
            Assert.That(SummonTickets.DailyFirstWin(s, T0.AddHours(8)), Is.EqualTo(0), "same UTC day");
            Assert.That(SummonTickets.DailyFirstWin(s, T0.Date.AddDays(1)), Is.EqualTo(SummonTickets.PerDailyFirstWin), "next day");
            Assert.That(s.Summon.FirstWinDay, Is.EqualTo(T0.Date.AddDays(1)));
            Assert.That(s.Summon.Tickets, Is.EqualTo(2));
        }

        [Test]
        public void A_Crucible_best_pays_one_Ticket_per_five_wave_milestone_newly_crossed()
        {
            var s = WithDiamonds("T5", 0);
            Assert.That(SummonTickets.AfterCrucibleBest(s, 0, 4), Is.EqualTo(0));
            Assert.That(SummonTickets.AfterCrucibleBest(s, 4, 5), Is.EqualTo(1));
            Assert.That(SummonTickets.AfterCrucibleBest(s, 5, 9), Is.EqualTo(0), "no new milestone");
            Assert.That(SummonTickets.AfterCrucibleBest(s, 9, 21), Is.EqualTo(3), "10, 15 and 20");
            Assert.That(SummonTickets.AfterCrucibleBest(s, 21, 15), Is.EqualTo(0), "a lower best pays nothing");
            Assert.That(s.Summon.Tickets, Is.EqualTo(4));
        }

        [Test]
        public void A_quest_hand_in_pays_its_Tickets_and_the_reward_line_names_them()
        {
            var s = Progression.NewGame(Real, "T6");
            var q = Real.TryQuest("q_hold_the_ward");
            Assert.That(q.RewardTickets, Is.GreaterThan(0));
            Assert.That(Quests.RewardText(Real, q), Does.Contain(TicketText.Of(q.RewardTickets)));
            Quests.Start(Real, s, q.Id);
            var win = BattleEngine.Create(Real, BattleFactory.Build(Real, s, Real.Stage(q.Objective.Id), 7), new List<BattleEvent>());
            win.Phase = BattlePhase.Victory;
            BattleFactory.Commit(Real, s, win);
            int before = s.Summon?.Tickets ?? 0;
            var r = Quests.TurnIn(Real, s, q.Id);
            Assert.That(r.Ok, Is.True);
            Assert.That(s.Summon.Tickets, Is.EqualTo(before + q.RewardTickets));
            Assert.That(r.Message, Does.Contain(TicketText.Of(q.RewardTickets)));
        }

        [Test]
        public void A_campaign_win_reports_the_Tickets_it_earned()
        {
            var s = Progression.NewGame(Real, "T7");
            var stage = Real.CampaignStages()[0];
            var b = BattleEngine.Create(Real, BattleFactory.Build(Real, s, stage, 5), new List<BattleEvent>());
            b.Phase = BattlePhase.Victory;
            var rep = BattleFactory.Commit(Real, s, b);
            Assert.That(rep.FirstClear, Is.True);
            Assert.That(rep.TicketsEarned, Is.EqualTo(SummonTickets.PerFirstClear + SummonTickets.PerDailyFirstWin), "first clear, the day's first win, not the fifth win");
            Assert.That(s.Summon.Tickets, Is.EqualTo(rep.TicketsEarned));
        }

        [Test]
        public void A_draw_spends_an_earned_Ticket_then_a_wallet_Ticket_then_Shards()
        {
            var s = WithDiamonds("T8", 3);
            var wallet = new AccountWallet { Tickets = 1 };
            var oldWallet = DiamondExchange.Wallet;
            s.Flags.Add(WalletMigration.MovedFlagPrefix + "0:test");
            DiamondExchange.Wallet = wallet;
            try
            {
                Assert.That(WalletMigration.IsMoved(s), Is.True);
                SummonTickets.Earn(s, 1);
                Assert.That(SummonTickets.Have(s), Is.EqualTo(2));

                Assert.That(DiamondSummon.Begin(Real, s, T0, out var first).Ok, Is.True);
                Assert.That(first.Tickets, Is.EqualTo(1));
                Assert.That(s.Summon.Tickets, Is.EqualTo(0), "the earned one went first");
                Assert.That(wallet.Tickets, Is.EqualTo(1));
                Paywall.Finish(Real, s, first, T0.AddSeconds(1));

                Assert.That(DiamondSummon.Begin(Real, s, T0.AddHours(1), out var second).Ok, Is.True);
                Assert.That(second.Tickets, Is.EqualTo(1));
                Assert.That(wallet.Tickets, Is.EqualTo(0), "then the wallet's");
                Paywall.Finish(Real, s, second, T0.AddHours(1).AddSeconds(1));

                Assert.That(DiamondSummon.Begin(Real, s, T0.AddHours(2), out var third).Ok, Is.True);
                Assert.That(third.Tickets, Is.EqualTo(0));
                Assert.That(third.Diamonds, Is.EqualTo(DiamondSummon.Price), "then Shards");
            }
            finally { DiamondExchange.Wallet = oldWallet; }
        }

        [Test]
        public void A_fizzled_Ticket_draw_gives_the_Ticket_back_as_an_earned_one()
        {
            var s = WithDiamonds("T9", 0);
            SummonTickets.Earn(s, 1);
            Assert.That(DiamondSummon.Begin(Real, s, T0, out var job).Ok, Is.True);
            Assert.That(s.Summon.Tickets, Is.EqualTo(0));
            Summon.Join(Real, s, job.Arg);
            Assert.That(Paywall.Finish(Real, s, job, T0.AddSeconds(1)).Ok, Is.False);
            Assert.That(s.Summon.Tickets, Is.EqualTo(1));
            Assert.That(s.Count(Crafting.DiamondId), Is.EqualTo(0), "no Shards were taken");
        }
    }
}
