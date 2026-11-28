using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Core.Rules;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>GG-T1816 (BIBLE §9.0): every hero ability says how it targets and who it targets.</summary>
    public class TargetTextTests
    {
        [Test]
        public void Every_hero_ability_says_how_and_who_it_targets()
        {
            var ids = Real.Classes.Values.Where(c => c.Team == Team.Hero).SelectMany(c => c.Abilities).Distinct().ToList();
            Assert.That(ids, Is.Not.Empty);
            var blank = ids.Select(id => Real.Ability(id)).Where(a => a != null)
                .Where(a => string.IsNullOrWhiteSpace(TargetText.How(a)) || string.IsNullOrWhiteSpace(TargetText.Who(a)))
                .Select(a => a.Id).ToList();
            Assert.That(blank, Is.Empty, string.Join(", ", blank));
        }

        [Test]
        public void Reach_shape_and_side_come_from_the_data()
        {
            var melee = new AbilityDef { Mode = TargetMode.PickActor, Shape = TargetShape.SingleActor, Filter = TargetFilter.EnemyOnly, AdjacentOnly = true };
            Assert.That(TargetText.How(melee), Does.Contain("[adjacent]"));
            Assert.That(TargetText.Who(melee), Is.EqualTo("One foe."));

            var nova = new AbilityDef { Mode = TargetMode.Auto, Shape = TargetShape.Square, Radius = 1, Filter = TargetFilter.AllyOnly };
            Assert.That(TargetText.How(nova), Does.Contain("3×3 square").And.Contain("9 tiles"));
            Assert.That(TargetText.Who(nova), Does.Contain("up to 9").And.Contain("you included"));

            Assert.That(TargetText.Who(new AbilityDef { Mode = TargetMode.Auto, Shape = TargetShape.AllEnemies }), Does.StartWith("Every foe"));
            Assert.That(TargetText.Cells(TargetShape.Row, 0), Is.EqualTo(BattleConfig.Cols));
        }
    }
}
