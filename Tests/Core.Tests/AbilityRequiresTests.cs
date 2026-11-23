using System.Linq;
using GridGame.Core.Content;
using GridGame.Meta;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>GG-T590 (§25.6.5): every Learn-screen ability carries a stat-threshold <see cref="AbilityDef.Requires"/>,
    /// nothing else does, and the met/unmet helpers read it the way the Learn screen and bots will.</summary>
    public class AbilityRequiresTests
    {
        [Test]
        public void Every_learnable_hero_ability_has_a_requirement()
        {
            var missing = Real.Abilities.Values.Where(a => Abilities.IsHeroUsable(a) && !a.HasRequires).Select(a => a.Id).ToList();
            Assert.That(missing, Is.Empty, "hero-usable abilities with no Requires: " + string.Join(", ", missing));
        }

        [Test]
        public void Only_learnable_hero_abilities_carry_a_requirement()
        {
            var stray = Real.Abilities.Values.Where(a => !Abilities.IsHeroUsable(a) && a.Requires != null).Select(a => a.Id).ToList();
            Assert.That(stray, Is.Empty, "Requires on a passive, item or enemy ability: " + string.Join(", ", stray));
        }

        [Test]
        public void Requirements_are_whole_points_within_a_reachable_range()
        {
            foreach (var a in Real.Abilities.Values.Where(a => a.HasRequires))
                foreach (var (abbrev, _, min) in a.RequiredStats())
                {
                    Assert.That(min, Is.EqualTo((float)System.Math.Round(min)), $"{a.Id} {abbrev} is not a whole point");
                    Assert.That(min, Is.InRange(1f, 10f), $"{a.Id} {abbrev} {min}");
                }
        }

        [Test]
        public void The_trail_examples_are_authored_as_drawn()
        {
            Assert.That(Real.Ability("cleave").Requires.Strength, Is.EqualTo(3f));
            Assert.That(Real.Ability("fireball").Requires.Intelligence, Is.EqualTo(8f));
            Assert.That(Real.Ability("berserker_lastmanswinging").Requires.Strength, Is.EqualTo(8f));
        }

        [Test]
        public void Met_only_when_every_stat_reaches_its_minimum()
        {
            var a = new AbilityDef { Id = "x", Requires = new Stats { Strength = 5, Wisdom = 4 } };
            Assert.That(a.HasRequires, Is.True);
            Assert.That(a.RequiresMet(null), Is.False);
            Assert.That(a.RequiresMet(new Stats { Strength = 5, Wisdom = 3 }), Is.False);
            Assert.That(a.RequiresMet(new Stats { Strength = 5, Wisdom = 4 }), Is.True);
            Assert.That(a.RequiresMet(new Stats { Strength = 9, Wisdom = 9, Luck = 2 }), Is.True);

            var unmet = a.UnmetRequires(new Stats { Strength = 6, Wisdom = 1 });
            Assert.That(unmet.Count, Is.EqualTo(1));
            Assert.That(unmet[0].Abbrev, Is.EqualTo("WIS"));
            Assert.That(unmet[0].Stat, Is.EqualTo(nameof(Stats.Wisdom)));
            Assert.That(unmet[0].Min, Is.EqualTo(4f));
            Assert.That(unmet[0].Have, Is.EqualTo(1f));
        }

        [Test]
        public void No_requirement_is_always_met()
        {
            Assert.That(new AbilityDef().HasRequires, Is.False);
            Assert.That(new AbilityDef().RequiresMet(null), Is.True);
            Assert.That(new AbilityDef { Requires = new Stats() }.HasRequires, Is.False);
            Assert.That(new AbilityDef { Requires = new Stats() }.RequiresMet(new Stats()), Is.True);
        }
    }
}
