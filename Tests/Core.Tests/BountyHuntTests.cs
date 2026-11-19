using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Meta;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>Bounty hunts (§22.4, GG-T431): marked, titled targets that roam one stage, spawn with a skull when their
    /// bounty is taken, and pay out at the board after they fall.</summary>
    public class BountyHuntTests
    {
        private static SaveData Opened(string name = "Hunter")
        {
            var s = Progression.NewGame(Real, name);
            s.HighestClearedStage = Real.CampaignStages().Count - 1; // every stage unlocked
            return s;
        }

        [Test]
        public void Every_bounty_names_a_real_foe_on_a_real_campaign_stage()
        {
            Assert.That(Real.Bounties.Count, Is.GreaterThanOrEqualTo(Bounties.BoardSize));
            foreach (var b in Real.Bounties.Values)
            {
                Assert.That(Real.TryClass(b.TargetClass)?.Team, Is.EqualTo(Team.Enemy), b.Id);
                Assert.That(Bounties.StageIndex(Real, b), Is.GreaterThanOrEqualTo(0), b.Id);
                Assert.That(b.Wave, Is.InRange(0, Real.Stage(b.Stage).Waves.Count - 1), b.Id);
                Assert.That(Bounties.RewardGold(Real, b), Is.GreaterThanOrEqualTo(Bounties.RewardGoldBase), b.Id);
            }
        }

        [Test]
        public void The_clue_names_the_stage_it_roams()
        {
            var b = Real.Bounties.Values.First(x => Real.Stage(x.Stage).Name == "The Tithe Moors II");
            Assert.That(Bounties.Clue(Real, b), Is.EqualTo("Seen wandering the Tithe Moors II"));
        }

        [Test]
        public void The_board_is_small_seeded_and_turns_over_with_battles()
        {
            var a = Opened("A");
            var b = Opened("B");
            b.CreatedUtc = a.CreatedUtc;
            var board = Bounties.Board(Real, a).Select(x => x.Id).ToList();
            Assert.That(board, Has.Count.EqualTo(Bounties.BoardSize));
            Assert.That(Bounties.Board(Real, b).Select(x => x.Id), Is.EqualTo(board), "same save seed, same board");

            var battle = BattleEngine.Create(Real, BattleFactory.Build(Real, a, Real.CampaignStages()[0].Id, 1), new List<BattleEvent>());
            for (int i = 0; i < Bounties.RefreshBattles; i++) Bounties.RecordBattle(Real, a, battle);
            Assert.That(a.Bounty.Refresh, Is.EqualTo(1));
        }

        [Test]
        public void A_fresh_game_offers_only_stages_it_has_unlocked()
        {
            var s = Progression.NewGame(Real, "Fresh");
            foreach (var b in Bounties.Board(Real, s))
                Assert.That(Progression.IsStageUnlocked(s, Bounties.StageIndex(Real, b)), Is.True, b.Id);
        }

        [Test]
        public void A_taken_bounty_spawns_its_marked_target_on_its_stage_and_nowhere_else()
        {
            var s = Opened();
            var b = Bounties.Offers(Real, s).First();
            Assert.That(Bounties.Take(Real, s, b.Id).Ok, Is.True);
            Assert.That(Bounties.Offers(Real, s).Any(x => x.Id == b.Id), Is.False);

            var setup = BattleFactory.Build(Real, s, b.Stage, 5);
            Assert.That(setup.Bounties.Select(m => (m.BountyId, m.ClassId, m.Title, m.Wave)), Is.EqualTo(new[] { (b.Id, b.TargetClass, b.Name, b.Wave) }));
            var other = Real.CampaignStages().First(st => st.Id != b.Stage && Bounties.OnStage(Real, s, st.Id).Count == 0);
            Assert.That(BattleFactory.Build(Real, s, other.Id, 5).Bounties, Is.Empty);
        }

        [Test]
        public void The_marked_target_is_titled_tougher_and_announced()
        {
            var db = Mini();
            var setup = new BattleSetup { Stage = Stage(("goblin", 0, 0)), Seed = 7, UnlockedSlots = 5 };
            setup.Party.Add(new HeroSpec { ClassId = "knight", Start = new Coord(5, 7) });
            setup.Bounties.Add(new BountyMark { BountyId = "b1", ClassId = "goblin", Title = "Grimjaw the Mauler", Wave = 0 });
            var ev = new List<BattleEvent>();
            var s = BattleEngine.Create(db, setup, ev);
            var plain = s.Units.First(u => u.Def.Id == "goblin" && !u.IsBountyTarget);
            var mark = s.Units.Single(u => u.IsBountyTarget);
            Assert.That(mark.Name, Is.EqualTo("Grimjaw the Mauler"));
            Assert.That(mark.MaxHp, Is.GreaterThan(plain.MaxHp));
            Assert.That(mark.Level, Is.EqualTo(plain.Level + BattleConfig.BountyLevelBonus));
            Assert.That(mark.LevelStats.Speed, Is.EqualTo(GridGame.Core.Rules.Formulas.StatsAtLevel(mark.Def, mark.Level).Speed), "the timeline reads the same");
            Assert.That(ev.OfType<BountyTargetEvent>().Single(e => !e.Slain).UnitId, Is.EqualTo(mark.Id));
            Assert.That(s.Bounties.Single().Spawned, Is.True);
        }

        [Test]
        public void A_slain_target_waits_at_the_board_then_pays_once()
        {
            var s = Opened();
            var b = Real.Bounties.Values.First(x => x.RewardItem != null);
            s.Bounty.Taken.Add(b.Id);
            var battle = BattleEngine.Create(Real, BattleFactory.Build(Real, s, b.Stage, 3), new List<BattleEvent>());
            battle.Bounties.Single().Slain = true; // it fell in the fight
            Assert.That(Bounties.RecordBattle(Real, s, battle), Does.Contain(b.Name));
            Assert.That(s.Bounty.Taken, Is.Empty);
            Assert.That(Bounties.Claimable(Real, s), Is.EqualTo(new[] { b }));
            Assert.That(Bounties.Board(Real, s).First(), Is.EqualTo(b), "collect first");

            int gold = s.Gold, items = s.Count(b.RewardItem);
            Assert.That(Bounties.Claim(Real, s, b.Id).Ok, Is.True);
            Assert.That(s.Gold, Is.EqualTo(gold + Bounties.RewardGold(Real, b)));
            Assert.That(s.Count(b.RewardItem), Is.EqualTo(items + 1));
            Assert.That(Bounties.Claim(Real, s, b.Id).Ok, Is.False);
            for (int r = 0; r < 20; r++) { s.Bounty.Refresh = r; Assert.That(Bounties.Board(Real, s), Does.Not.Contain(b), "a claimed bounty never posts again"); }
        }

        [Test]
        public void A_target_that_survives_stays_hunted()
        {
            var s = Opened();
            var b = Bounties.Offers(Real, s).First();
            Bounties.Take(Real, s, b.Id);
            var battle = BattleEngine.Create(Real, BattleFactory.Build(Real, s, b.Stage, 3), new List<BattleEvent>());
            Assert.That(Bounties.RecordBattle(Real, s, battle), Is.Null);
            Assert.That(s.Bounty.Taken, Is.EqualTo(new[] { b.Id }));
            Assert.That(Bounties.Abandon(Real, s, b.Id).Ok, Is.True);
            Assert.That(s.Bounty.Taken, Is.Empty);
        }

        [Test]
        public void An_old_saves_kill_contract_is_retired_and_the_board_stays_open()
        {
            var s = Progression.NewGame(Real, "Old");
            s.Bounty.ActiveId = "bounty_slime_culling";
            s.Bounty.Progress = 3;
            var back = SaveMigrations.Migrate(s);
            Assert.That(back.Bounty.ActiveId, Is.Null);
            Assert.That(back.Bounty.Progress, Is.Zero);
            Assert.That(back.Bounty.Taken, Is.Empty);
            Assert.That(CampUnlocks.BountiesOpen(back), Is.True);
        }

        [Test]
        public void The_camp_routine_never_works_the_board()
        {
            var s = Opened("Bot");
            CampNpcs.JoinAll(s);
            CampRoutine.Run(Real, s);
            Assert.That(s.Bounty.Taken, Is.Empty);
            Assert.That(s.Bounty.Claimed, Is.Empty);
        }
    }
}
