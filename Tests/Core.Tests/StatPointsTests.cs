using System.Collections.Generic;
using GridGame.Core.Rules;
using GridGame.Meta;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>Level-up stat points (§15.1, GG-T591) are retired for the skill hexagon (GG-T1058): nothing is earned at
    /// any level, so nothing can be spent, and the camp routine spends nothing. Old picks stay in the save, untouched.</summary>
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
        public void No_level_earns_a_stat_point()
        {
            var (_, h) = Setup(1);
            foreach (int level in new[] { 1, 2, 7, 20 })
            {
                h.TotalXp = Formulas.TotalXpForLevel(level);
                Assert.That(Formulas.StatPointsForLevel(level), Is.Zero, $"level {level}");
                Assert.That(StatPoints.Earned(h), Is.Zero, $"level {level}");
                Assert.That(StatPoints.Unspent(h), Is.Zero, $"level {level}");
            }
            h.StatPicks = null; // a save from before GG-T591
            Assert.That(StatPoints.Unspent(h), Is.Zero);
        }

        [Test]
        public void Spend_fails_with_nothing_earned_and_leaves_skill_points_alone()
        {
            var (_, h) = Setup(12);
            int skill = h.SkillPoints;
            var r = StatPoints.Spend(h, "Intelligence");
            Assert.That(r.Ok, Is.False);
            Assert.That(r.Message, Is.EqualTo("No stat points to spend"));
            Assert.That(StatPoints.In(h, "Intelligence"), Is.Zero);
            Assert.That(StatPoints.Spend(h, "Charisma").Ok, Is.False, "an unknown stat");
            Assert.That(h.SkillPoints, Is.EqualTo(skill));
        }

        [Test]
        public void Old_picks_stay_in_the_save_with_nothing_unspent()
        {
            var (_, h) = Setup(7);
            h.StatPicks = new Dictionary<string, int> { ["Strength"] = 4 };
            Assert.That(StatPoints.In(h, "Strength"), Is.EqualTo(4));
            Assert.That(StatPoints.Unspent(h), Is.Zero);
            Assert.That(StatPoints.Spend(h, "Strength").Ok, Is.False);
            Assert.That(StatPoints.In(h, "Strength"), Is.EqualTo(4));
        }

        [Test]
        public void The_camp_routine_spends_nothing()
        {
            var (s, h) = Setup(5);
            Assert.That(StatPoints.AutoSpend(Real, s), Is.Empty);
            Assert.That(StatPoints.Spent(h), Is.Zero);
            CampRoutine.Run(Real, s);
            Assert.That(StatPoints.Spent(h), Is.Zero);
        }
    }
}
