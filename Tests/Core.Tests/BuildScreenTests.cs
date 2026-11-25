using System.Linq;
using GridGame.Core.Content;
using GridGame.Meta;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>The Camp Status screen's Build-screen read models (GG-T48): known proficiencies and the one-line build
    /// summary, computed off a hero's existing Known list and its class. GG-T1098: the talent trees are retired, so the
    /// summary is the class name and its role word, with no tree investment.</summary>
    public class BuildScreenTests
    {
        private static ContentDb RoleDb()
        {
            var db = Mini();
            db.Classes["knight"].Role = "Tank";
            db.Classes["cleric"].Role = "Healer";
            return db;
        }

        [Test]
        public void KnownProficiencies_reports_only_the_ones_the_hero_has()
        {
            var c = RoleDb();
            c.Abilities["heavy_armor_proficiency"] = new AbilityDef { Id = "heavy_armor_proficiency", Name = "Heavy Armor Proficiency", Kind = AbilityKind.Passive };
            var withIt = new HeroSave { ClassId = "knight", Known = { "heavy_armor_proficiency" } };
            var withoutIt = new HeroSave { ClassId = "knight", Known = { } };
            Assert.That(Progression.KnownProficiencies(c, withIt), Is.EqualTo(new[] { "Heavy Armor Proficiency" }));
            Assert.That(Progression.KnownProficiencies(c, withoutIt), Is.Empty);
        }

        [Test]
        public void BuildSummary_names_the_class_and_its_role()
        {
            var c = RoleDb();
            var h = new HeroSave { ClassId = "knight", Known = { "shield_wall" } };
            Assert.That(Progression.BuildSummary(c, h), Is.EqualTo("Knight — a tank"));
            Assert.That(Progression.BuildSummary(c, new HeroSave { ClassId = "cleric" }), Is.EqualTo("Cleric — a healer"));
        }

        [Test]
        public void Every_real_hero_class_produces_a_build_summary_without_throwing()
        {
            var c = Real;
            foreach (var cls in c.Classes.Values.Where(x => x.Team == Team.Hero))
            {
                var h = Progression.NewHero(c, cls.Id);
                var summary = Progression.BuildSummary(c, h);
                Assert.That(summary, Does.StartWith(cls.Name + " — "));
            }
        }
    }
}
