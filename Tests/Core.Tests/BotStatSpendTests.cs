using System.Linq;
using GridGame.Core.Rules;
using GridGame.Meta;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>GG-T593 (§25.6.5): the camp routine spends level-up stat points toward the next class ability its
    /// <c>Requires</c> lock out, on-build first, and only falls back to the growth stat once nothing is locked.</summary>
    public class BotStatSpendTests
    {
        private static (SaveData s, HeroSave h) Hero(string classId, int level)
        {
            var s = Progression.NewGame(Real, "BotStatSpend", new[] { classId });
            var h = s.Hero(classId);
            h.TotalXp = Formulas.TotalXpForLevel(level);
            return (s, h); // the lead who always sets out stays level 1: no points of its own
        }

        [Test]
        public void A_target_is_a_locked_non_kit_ability_from_the_class_list()
        {
            var (s, h) = Hero("barbarian", 2);
            var t = BotStatSpend.Target(Real, s, h);
            Assert.That(t, Is.Not.Null);
            Assert.That(Real.Class("barbarian").Abilities, Does.Contain(t.Id));
            Assert.That(Abilities.IsKitMove(Real, h, t), Is.False, "kit moves are waived, never a target");
            Assert.That(Abilities.Unmet(Real, s, h, t), Is.Not.Empty);
        }

        [Test]
        public void On_build_abilities_come_before_off_build_ones()
        {
            var (s, h) = Hero("barbarian", 2);
            var affinity = BotStatSpend.KitAffinity(Real, Real.Class("barbarian"));
            Assert.That(affinity.Strength, Is.GreaterThan(0f));
            var t = BotStatSpend.Target(Real, s, h);
            Assert.That(t.RequiredStats().All(r => r.Stat == "Strength"), Is.True, $"target {t.Id} is off-build for a Barbarian");
            Assert.That(BotStatSpend.Pick(Real, s, h), Is.EqualTo("Strength"));
        }

        [Test]
        public void Spending_unlocks_the_target_and_logs_where_the_points_went()
        {
            var (s, h) = Hero("barbarian", 12);
            var first = BotStatSpend.Target(Real, s, h);
            var log = BotStatSpend.Spend(Real, s);
            Assert.That(StatPoints.Unspent(h), Is.Zero);
            Assert.That(Abilities.Unmet(Real, s, h, first), Is.Empty, $"{first.Id} still locked after 11 points");
            Assert.That(log, Has.Count.EqualTo(1));
            Assert.That(log[0], Does.StartWith("Barbarian put 11 stat points into"));
        }

        [Test]
        public void With_nothing_locked_the_point_goes_to_the_growth_stat()
        {
            var (s, h) = Hero("barbarian", 2);
            foreach (var n in Core.Content.StatGrantDef.Names) (h.StatPicks ??= new System.Collections.Generic.Dictionary<string, int>())[n] = 10;
            Assert.That(BotStatSpend.Target(Real, s, h), Is.Null);
            Assert.That(BotStatSpend.Pick(Real, s, h), Is.EqualTo(StatPoints.BotPick(Real, h)));
        }

        [Test]
        public void The_camp_routine_spends_stat_points_by_the_requires_heuristic()
        {
            var (s, h) = Hero("barbarian", 4);
            CampRoutine.Run(Real, s);
            Assert.That(StatPoints.Unspent(h), Is.Zero);
            Assert.That(StatPoints.In(h, "Strength"), Is.EqualTo(3));
        }
    }
}
