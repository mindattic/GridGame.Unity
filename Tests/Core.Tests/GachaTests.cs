using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Content;
using GridGame.Meta;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>§25.10.3 (GG-T1419, GG-T1600): the summon banners' odds, roll and pull record.</summary>
    public class GachaTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 12, 2, 12, 0, 0, DateTimeKind.Utc);

        private static SaveData NewSave(string name) => Progression.NewGame(Real, name);

        private static BannerDef Standard() =>
            new BannerDef { Id = "std", Name = "Standard", Kind = BannerKind.Standard };

        private static BannerDef Featured(string id, float share, params string[] featured) =>
            new BannerDef { Id = id, Name = id, Kind = BannerKind.Featured, Featured = featured.ToList(), FeaturedShare = share };

        /// <summary>Two heroes the save owns (from its starting party).</summary>
        private static List<string> Owned(SaveData s)
        {
            Assert.That(s.Roster.Count, Is.GreaterThanOrEqualTo(2), "a new game starts with a party");
            return s.Roster.Take(2).Select(h => h.ClassId).ToList();
        }

        [Test]
        public void Odds_sum_to_one_and_the_roll_draws_only_listed_heroes()
        {
            var s = NewSave("G1");
            Assert.That(Real.BannerOrder, Is.Not.Empty, "banners.json ships banners");
            foreach (var banner in Real.BannerOrder)
            {
                var odds = Gacha.Odds(Real, s, banner);
                Assert.That(odds, Is.Not.Empty, banner.Id);
                Assert.That(odds.Sum(o => o.Chance), Is.EqualTo(1.0).Within(1e-9), banner.Id);
                Assert.That(odds.Select(o => o.ClassId), Is.EquivalentTo(Gacha.Pool(Real, s, banner)), "one entry per hero in the pool");
                var listed = new HashSet<string>(odds.Select(o => o.ClassId));
                var seen = new HashSet<string>();
                for (int seed = 0; seed < 300; seed++)
                {
                    var pick = Gacha.Roll(odds, seed);
                    Assert.That(listed, Does.Contain(pick), banner.Id);
                    seen.Add(pick);
                }
                Assert.That(seen.Count, Is.GreaterThan(1), "the roll spreads across the pool");
            }
            Assert.That(Gacha.Roll(new List<DrawOdds>(), 1), Is.Null);
        }

        [Test]
        public void A_featured_banner_splits_its_share_across_the_featured_heroes()
        {
            var s = NewSave("G2");
            var banner = Featured("fb", 0.5f, 0, "f1", "f2");
            var pool = new List<string> { "f1", "f2", "a", "b", "c", "d", "e" };
            var odds = Gacha.Odds(s, banner, pool);
            Assert.That(odds.Select(o => o.ClassId), Is.EqualTo(pool), "pool order");
            Assert.That(odds.Where(o => o.ClassId.StartsWith("f")).Select(o => o.Chance), Is.All.EqualTo(0.25).Within(1e-12));
            Assert.That(odds.Where(o => !o.ClassId.StartsWith("f")).Select(o => o.Chance), Is.All.EqualTo(0.1).Within(1e-12));
            Assert.That(odds.Sum(o => o.Chance), Is.EqualTo(1.0).Within(1e-9));

            var standard = Gacha.Odds(s, Standard(), pool);
            Assert.That(standard.Select(o => o.Chance), Is.All.EqualTo(1.0 / pool.Count).Within(1e-12), "Standard is even");
        }

        [Test]
        public void Record_counts_pulls_per_banner_kind()
        {
            var s = NewSave("G5");
            var owned = Owned(s);
            var std = Standard();
            var feat = Featured("fb", 0.5f, "f1");

            var r1 = Gacha.Record(s, std, owned[0], "100 Shards", T0);
            Gacha.Record(s, std, owned[1], "100 Shards", T0.AddMinutes(1));
            Assert.That(r1.Result, Is.EqualTo(PullResult.Echo));
            Assert.That(s.Summon.Pulls[BannerKind.Standard], Is.EqualTo(2));
            Assert.That(s.Summon.Pulls.ContainsKey(BannerKind.Featured), Is.False, "Standard pulls leave Featured alone");

            Gacha.Record(s, feat, owned[0], "100 Shards", T0.AddMinutes(2));
            Assert.That(s.Summon.Pulls[BannerKind.Featured], Is.EqualTo(1));
            Assert.That(s.Summon.Pulls[BannerKind.Standard], Is.EqualTo(2));

            var rNew = Gacha.Record(s, std, "x_new", "100 Shards", T0.AddMinutes(3));
            Assert.That(rNew.Result, Is.EqualTo(PullResult.New));

            int seedBefore = Gacha.SeedFor(s, std);
            Gacha.Record(s, std, owned[0], "100 Shards", T0.AddMinutes(4));
            Assert.That(Gacha.SeedFor(s, std), Is.Not.EqualTo(seedBefore), "the next roll is seeded from the kind's pull count");
        }

        [Test]
        public void History_is_newest_first_and_capped()
        {
            var s = NewSave("G6");
            var owned = Owned(s);
            var std = Standard();
            int total = Gacha.HistoryKeep + 5;
            for (int i = 0; i < total; i++) Gacha.Record(s, std, owned[i % 2], "100 Shards", T0.AddMinutes(i));
            Assert.That(Gacha.HistoryKeep, Is.EqualTo(200));
            Assert.That(s.Summon.History.Count, Is.EqualTo(Gacha.HistoryKeep));
            Assert.That(s.Summon.Pulls[BannerKind.Standard], Is.EqualTo(total), "the count keeps every pull");
            Assert.That(s.Summon.History[0].Utc, Is.EqualTo(T0.AddMinutes(total - 1)), "newest first");
            Assert.That(s.Summon.History[Gacha.HistoryKeep - 1].Utc, Is.EqualTo(T0.AddMinutes(total - Gacha.HistoryKeep)), "the oldest roll off");
            Assert.That(s.Summon.History.Select(h => h.Utc), Is.Ordered.Descending);
            Assert.That(s.Summon.History[0].BannerId, Is.EqualTo("std"));
            Assert.That(s.Summon.History[0].Paid, Is.EqualTo("100 Shards"));
        }

        [Test]
        public void A_v22_save_migrates_with_an_empty_summon_record()
        {
            var s = NewSave("G7");
            s.Version = 22;
            s.Summon = null;
            var back = SaveMigrations.Migrate(s);
            Assert.That(back.Version, Is.EqualTo(SaveData.CurrentVersion));
            Assert.That(back.Summon, Is.Not.Null);
            Assert.That(back.Summon.Pulls, Is.Empty);
            Assert.That(back.Summon.History, Is.Empty);

            var partial = NewSave("G7b");
            partial.Summon = new SummonState { Pulls = null, History = null };
            var fixedUp = SaveMigrations.Migrate(partial);
            Assert.That(fixedUp.Summon.Pulls, Is.Not.Null);
            Assert.That(fixedUp.Summon.History, Is.Not.Null);
        }

        [Test]
        public void An_owned_hero_pulled_again_is_an_echo_then_ticket_fragments()
        {
            var s = NewSave("G9");
            var owned = Owned(s);
            var std = Standard();
            var hero = s.Hero(owned[0]);
            int sp = hero.SkillPoints;
            for (int i = 1; i <= Gacha.EchoCap; i++)
            {
                var rec = Gacha.Record(s, std, owned[0], "1 Ticket", T0.AddMinutes(i));
                Assert.That(rec.Result, Is.EqualTo(PullResult.Echo));
                Assert.That(rec.Fragments, Is.EqualTo(0));
                Assert.That(hero.Echoes, Is.EqualTo(i));
                Assert.That(hero.SkillPoints, Is.EqualTo(sp + i * Gacha.EchoSp));
            }
            Assert.That(s.Roster.Count(h => h.ClassId == owned[0]), Is.EqualTo(1), "a duplicate never adds a second copy");

            int pullsPerTicket = Gacha.FragmentsPerTicket / Gacha.FragmentsPerSpareEcho;
            for (int i = 0; i < pullsPerTicket; i++)
            {
                var rec = Gacha.Record(s, std, owned[0], "1 Ticket", T0.AddHours(1).AddMinutes(i));
                Assert.That(rec.Result, Is.EqualTo(PullResult.Fragments));
                Assert.That(rec.Fragments, Is.EqualTo(Gacha.FragmentsPerSpareEcho));
            }
            Assert.That(hero.Echoes, Is.EqualTo(Gacha.EchoCap), "Echoes stop at the cap");
            Assert.That(hero.SkillPoints, Is.EqualTo(sp + Gacha.EchoCap * Gacha.EchoSp), "no SP past the cap");
            Assert.That(s.Summon.Tickets, Is.EqualTo(1), "the fragments join into a Ticket");
            Assert.That(s.Summon.Fragments, Is.EqualTo(0));

            Assert.That(s.Hero(owned[1]).Echoes, Is.EqualTo(0), "Echoes are per hero");
            Assert.That(Gacha.Record(s, std, "x_new", "1 Ticket", T0.AddDays(1)).Result,
                Is.EqualTo(PullResult.New));
        }

        [Test]
        public void A_diamond_draw_records_a_standard_pull()
        {
            var s = NewSave("G8");
            s.Gold = 0;
            s.AddItem(Crafting.DiamondId, DiamondSummon.Price);
            Assert.That(DiamondSummon.Begin(Real, s, T0, out var job).Ok, Is.True);
            Assert.That(s.Summon.History.Count, Is.EqualTo(1));
            var rec = s.Summon.History[0];
            Assert.That(rec.ClassId, Is.EqualTo(job.Arg));
            Assert.That(rec.BannerId, Is.EqualTo(Gacha.Standard(Real).Id));
            Assert.That(rec.Result, Is.EqualTo(PullResult.New), "the wanderer pool holds only heroes the save lacks");
            Assert.That(rec.Paid, Is.EqualTo(DiamondExchange.Label(DiamondSummon.Price)));
            Assert.That(rec.Utc, Is.EqualTo(T0));
            Assert.That(s.Summon.Pulls[BannerKind.Standard], Is.EqualTo(1));
            Assert.That(s.Summon.Pulls.ContainsKey(BannerKind.Featured), Is.False);
        }
    }
}
