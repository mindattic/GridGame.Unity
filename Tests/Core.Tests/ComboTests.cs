using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>GG-T911 (§1.2.8, GG-T849): the hero combo - supporter credit, variety vs halving, window close, enemy pincers, results.</summary>
    public class ComboTests
    {
        private static Unit Fake(int id, Team team, string cls) =>
            new Unit { Id = id, Team = team, Def = new ClassDef { Id = cls, Team = team }, Level = 1, W = 1, H = 1 };

        /// <summary>knight(1,3) and the cleric dropping on (3,3) pincer the target on (2,3); the mage on (1,4) supports with Crit.</summary>
        private static (BattleState s, List<BattleEvent> ev) Pincer(TurnMode mode, string target, bool viaTurn, int mageKnightTier = 0)
        {
            var db = Mini();
            db.Classes["mage"].Support = new SupportDef { Effect = SupportEffect.Crit, Magnitude = 0.3f, Range = 1, Label = "Shadow Edge" };
            var setup = new BattleSetup { Stage = Stage((target, 2, 3)), Seed = 7, UnlockedSlots = 5, TurnMode = mode };
            if (mageKnightTier > 0) setup.Cohesion["knight|mage"] = mageKnightTier;
            setup.Party.Add(new HeroSpec { ClassId = "knight", Start = new Coord(1, 3) });
            setup.Party.Add(new HeroSpec { ClassId = "cleric", Start = new Coord(3, 4) });
            setup.Party.Add(new HeroSpec { ClassId = "mage", Start = new Coord(1, 4) });
            var s = BattleEngine.Create(db, setup, new List<BattleEvent>());
            var cmd = new MoveCommand { HeroId = Hero(s, "cleric").Id, Path = P((3, 3)) };
            return (s, viaTurn ? Turn(s, cmd) : BattleEngine.Apply(s, cmd));
        }

        [Test]
        public void A_supporter_action_credits_the_supporter_not_the_attackers()
        {
            var (s, ev) = Pincer(TurnMode.PartyWindow, "tank", true);
            var ended = ev.OfType<ComboEndedEvent>().Single();
            var mage = ended.Breakdown.Single(b => b.UnitId == Hero(s, "mage").Id);
            Assert.That(mage.SupporterPoints, Is.GreaterThan(0));
            Assert.That(mage.AttackerPoints, Is.EqualTo(0));
            var knight = ended.Breakdown.Single(b => b.UnitId == Hero(s, "knight").Id);
            Assert.That(knight.AttackerPoints, Is.GreaterThan(0));
            Assert.That(knight.SupporterPoints, Is.EqualTo(0));
            Assert.That(ev.OfType<ComboChangedEvent>().Any(c => c.Kind == ComboKind.Support && c.ContributorId == mage.UnitId), Is.True);
        }

        [Test]
        public void A_bonded_supporter_earns_a_bond_credit_as_supporter_points()
        {
            var (s, ev) = Pincer(TurnMode.PartyWindow, "tank", false, mageKnightTier: 2);
            int mage = Hero(s, "mage").Id;
            var bond = ev.OfType<ComboChangedEvent>().Single(c => c.Kind == ComboKind.Bond);
            Assert.That(bond.ContributorId, Is.EqualTo(mage));
            Assert.That(bond.Label, Is.EqualTo("Bond 2"));
            Assert.That(bond.Points, Is.GreaterThanOrEqualTo(BattleConfig.ComboBondPoints));
            Assert.That(s.Combo.Shares.Single(x => x.UnitId == mage).AttackerPoints, Is.EqualTo(0));

            var (_, plain) = Pincer(TurnMode.PartyWindow, "tank", false);
            Assert.That(plain.OfType<ComboChangedEvent>().Any(c => c.Kind == ComboKind.Bond), Is.False);
        }

        [Test]
        public void A_repeated_bond_credit_halves_like_any_other()
        {
            var b = Fake(2, Team.Hero, "mage");
            var t = new ComboTracker();
            var first = t.Credit(b, ComboKind.Bond, "1", "Bond 3");
            var again = t.Credit(b, ComboKind.Bond, "1", "Bond 3");
            Assert.That(first.Points, Is.EqualTo(BattleConfig.ComboBondPoints));
            Assert.That(again.Points, Is.EqualTo(System.Math.Max(1, BattleConfig.ComboBondPoints / 2)));
            Assert.That(t.Shares.Single().SupporterPoints, Is.EqualTo(first.Points + again.Points));
        }

        [Test]
        public void Variety_outscores_the_same_unit_repeating_the_same_effect()
        {
            var a = Fake(1, Team.Hero, "knight"); var b = Fake(2, Team.Hero, "mage");
            var repeat = new ComboTracker();
            repeat.Credit(a, ComboKind.Pincer, "0", "x");
            repeat.Credit(a, ComboKind.Pincer, "0", "x");
            repeat.Credit(a, ComboKind.Pincer, "0", "x");
            var varied = new ComboTracker();
            varied.Credit(a, ComboKind.Pincer, "0", "x");
            varied.Credit(b, ComboKind.Support, "Crit", "x");
            varied.Credit(a, ComboKind.Status, "burning", "x");
            Assert.That(varied.Score, Is.GreaterThan(repeat.Score));
            // the first credit is base points; each repeat of the same unit/kind/effect halves
            Assert.That(repeat.Score, Is.EqualTo(BattleConfig.ComboPincerPoints + BattleConfig.ComboPincerPoints / 2 + BattleConfig.ComboPincerPoints / 4));
            var first = new ComboTracker().Credit(a, ComboKind.Pincer, "0", "x");
            Assert.That(first.Points, Is.EqualTo(BattleConfig.ComboPincerPoints), "no variety bonus on the opening credit");
        }

        [Test]
        public void A_new_contributor_and_kind_earn_their_bonuses()
        {
            var a = Fake(1, Team.Hero, "knight"); var b = Fake(2, Team.Hero, "mage");
            var t = new ComboTracker();
            t.Credit(a, ComboKind.Pincer, "0", "x");
            var ev = t.Credit(b, ComboKind.Support, "Crit", "x");
            Assert.That(ev.Points, Is.EqualTo(BattleConfig.ComboSupportPoints + BattleConfig.ComboNewContributorBonus
                + BattleConfig.ComboNewKindBonus + BattleConfig.ComboNewEffectBonus));
            Assert.That(t.Rank, Is.EqualTo(ComboTracker.RankFor(t.Score)));
        }

        [Test]
        public void Ranks_follow_the_configured_thresholds()
        {
            var th = BattleConfig.ComboRankThresholds;
            Assert.That(ComboTracker.RankFor(th[0] - 1), Is.EqualTo(ComboRank.None));
            for (int i = 0; i < th.Length; i++) Assert.That(ComboTracker.RankFor(th[i]), Is.EqualTo((ComboRank)(i + 1)));
            Assert.That(ComboTracker.RankFor(th[th.Length - 1]), Is.EqualTo(ComboRank.SSS));
        }

        [Test]
        public void Foes_never_feed_the_tracker()
        {
            var t = new ComboTracker();
            Assert.That(t.Credit(Fake(9, Team.Enemy, "goblin"), ComboKind.Pincer, "0", "x"), Is.Null);
            Assert.That(t.Active, Is.False);
        }

        [Test]
        public void An_enemy_pincer_emits_no_combo_events()
        {
            var db = Mini();
            db.Classes["knight"].BaseStats.Vitality = 500;
            var setup = new BattleSetup { Stage = Stage(("goblin", 1, 3), ("goblin", 3, 3), ("tank", 6, 6)), Seed = 7, UnlockedSlots = 5 };
            setup.Party.Add(new HeroSpec { ClassId = "knight", Start = new Coord(2, 3) });
            var s = BattleEngine.Create(db, setup, new List<BattleEvent>());
            var ev = Turn(s, new ShieldCommand());
            Assume.That(ev.OfType<PincerFormedEvent>().Any(p => p.Team == Team.Enemy), "the foes pincer the knight on their turn");
            Assert.That(ev.OfType<ComboChangedEvent>().Any(), Is.False);
            Assert.That(ev.OfType<ComboEndedEvent>().Any(), Is.False);
            Assert.That(s.Combo.Active, Is.False);
        }

        [Test]
        public void The_window_closes_at_the_turn_boundary_in_every_available_turn_mode()
        {
            foreach (var mode in TurnModes.Available)
            {
                var (s, ev) = Pincer(mode, "tank", true);
                Assert.That(ev.OfType<ComboEndedEvent>().Count(), Is.EqualTo(1), mode.ToString());
                Assert.That(s.Combo.Active, Is.False, mode + ": tracker reset");
                Assert.That(s.Combo.Score, Is.EqualTo(0), mode.ToString());
                Assert.That(s.Combo.Rank, Is.EqualTo(ComboRank.None), mode.ToString());
            }
        }

        [Test]
        public void The_window_closes_when_the_battle_ends()
        {
            var (s, ev) = Pincer(TurnMode.PartyWindow, "goblin", false);
            Assert.That(s.IsOver, Is.True);
            Assert.That(ev.OfType<BattleEndedEvent>().Any(), Is.True);
            Assert.That(ev.OfType<ComboEndedEvent>().Count(), Is.EqualTo(1));
            Assert.That(s.Combo.Active, Is.False);
        }

        [Test]
        public void Results_record_the_best_rank_and_each_class_attacker_and_supporter_points()
        {
            var (s, ev) = Pincer(TurnMode.PartyWindow, "tank", true);
            var ended = ev.OfType<ComboEndedEvent>().Single();
            Assert.That(s.Results.BestComboRank, Is.EqualTo(ended.Rank));
            Assert.That(s.Results.BestComboScore, Is.EqualTo(ended.Score));
            Assert.That(s.Results.ComboSupporterPoints["mage"], Is.GreaterThan(0));
            Assert.That(s.Results.ComboAttackerPoints["knight"], Is.GreaterThan(0));
            Assert.That(s.Results.ComboAttackerPoints.Values.Sum() + s.Results.ComboSupporterPoints.Values.Sum(), Is.EqualTo(ended.Score));
        }
    }
}
