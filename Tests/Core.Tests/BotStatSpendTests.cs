using System.Linq;
using GridGame.Core.Rules;
using GridGame.Meta;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>GG-T593 (§25.6.5): the bot's stat-spend heuristic picks the next class ability its <c>Requires</c> lock
    /// out, on-build first. Level-up stat points are retired (GG-T1058), so there is nothing for it to spend and the camp
    /// routine no longer calls it; Requires are measured against the hero's skill hexagon stats.</summary>
    public class BotStatSpendTests
    {
        private static (SaveData s, HeroSave h) Hero(string classId, int level)
        {
            var s = Progression.NewGame(Real, "BotStatSpend", new[] { classId });
            var h = s.Hero(classId);
            h.TotalXp = Formulas.TotalXpForLevel(level);
            return (s, h);
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
        public void Spend_has_no_points_to_spend_at_any_level()
        {
            var (s, h) = Hero("barbarian", 12);
            var first = BotStatSpend.Target(Real, s, h);
            var log = BotStatSpend.Spend(Real, s);
            Assert.That(log, Is.Empty);
            Assert.That(StatPoints.Spent(h), Is.Zero);
            Assert.That(BotStatSpend.Target(Real, s, h)?.Id, Is.EqualTo(first?.Id), "nothing was unlocked");
        }

        [Test]
        public void The_camp_routine_spends_no_stat_points()
        {
            var (s, h) = Hero("barbarian", 4);
            CampRoutine.Run(Real, s);
            Assert.That(StatPoints.Spent(h), Is.Zero);
            Assert.That(StatPoints.In(h, "Strength"), Is.Zero);
        }
    }
}
