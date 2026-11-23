using GridGame.Core.Rules;
using GridGame.Meta;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>Level-up stat points (§15.1, GG-T591): one free point a level, spendable on any of the 8 stats, a pool
    /// apart from skill points.</summary>
    public class StatPointsTests
    {
        private static (SaveData s, HeroSave h) Setup(int level)
        {
            var s = Progression.NewGame(Real, "StatPoints");
            var h = s.Hero(s.Party[0]);
            h.TotalXp = Formulas.TotalXpForLevel(level);
            return (s, h);
        }

        [Test]
        public void Every_level_past_one_earns_one_stat_point_and_an_old_save_has_them_all_unspent()
        {
            var (_, h) = Setup(1);
            Assert.That(StatPoints.Earned(h), Is.Zero);
            h.TotalXp = Formulas.TotalXpForLevel(7);
            h.StatPicks = null; // a save from before GG-T591
            Assert.That(StatPoints.Earned(h), Is.EqualTo(6));
            Assert.That(StatPoints.Unspent(h), Is.EqualTo(6));
        }

        [Test]
        public void Spending_a_point_raises_that_stat_in_battle_and_leaves_skill_points_alone()
        {
            var (_, h) = Setup(4);
            int skill = h.SkillPoints;
            float before = Progression.Preview(Real, h).LevelStats.Intelligence;
            Assert.That(StatPoints.Spend(h, "Intelligence").Ok, Is.True);
            Assert.That(StatPoints.Spend(h, "Intelligence").Ok, Is.True);
            Assert.That(StatPoints.In(h, "Intelligence"), Is.EqualTo(2));
            Assert.That(StatPoints.Unspent(h), Is.EqualTo(1));
            Assert.That(h.SkillPoints, Is.EqualTo(skill), "separate pools");
            Assert.That(Progression.Preview(Real, h).LevelStats.Intelligence, Is.EqualTo(before + 2 * Formulas.StatPointValue));
        }

        [Test]
        public void No_point_can_be_spent_past_what_was_earned_or_on_an_unknown_stat()
        {
            var (_, h) = Setup(2);
            Assert.That(StatPoints.Spend(h, "Charisma").Ok, Is.False);
            Assert.That(StatPoints.Spend(h, "Luck").Ok, Is.True);
            Assert.That(StatPoints.Spend(h, "Luck").Ok, Is.False);
        }

        [Test]
        public void The_camp_routine_spends_every_point_on_the_class_growth_stat()
        {
            var (s, h) = Setup(5);
            StatPoints.AutoSpend(Real, s);
            Assert.That(StatPoints.Unspent(h), Is.Zero);
            Assert.That(StatPoints.In(h, StatPoints.BotPick(Real, h)), Is.EqualTo(4));
        }
    }
}
