using System.Collections.Generic;
using System.IO;
using System.Linq;
using GridGame.Core.Content;
using GridGame.Core.Rules;
using GridGame.Meta;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>GG-T1037 / GG-T1064: hero classes cut from the roster (<see cref="ClassDef.IsCut"/>) stay in the content for
    /// the Codex, but leave every pool, Validate keeps them out of the summon pool, guests and recruitables, and a save that
    /// holds one converts it to its <see cref="ClassDef.CutInto"/> hero on load.</summary>
    public class CutClassTests
    {
        /// <summary>A private copy of the real content (TestKit.Real is shared, so never mutated).</summary>
        private static ContentDb Copy() => ContentLoader.Load(name =>
        {
            var p = Path.Combine(ContentDir, name + ".json");
            return File.Exists(p) ? File.ReadAllText(p) : null;
        });

        private static List<string> CutProblems(ContentDb db) =>
            ContentLoader.Validate(db).Where(x => x.Contains("GG-T1037") || x.Contains("CutInto") || x.Contains("can be cut")).ToList();

        private static IEnumerable<string> CutIds(ContentDb db) => db.Classes.Values.Where(d => d.IsCut).Select(d => d.Id);

        private static HeroSave Hero(ContentDb c, string classId, int xp)
        {
            var h = Progression.NewHero(c, classId);
            h.TotalXp = xp;
            return h;
        }

        // ── content ──

        [Test]
        public void Every_listed_cut_class_is_cut_into_a_hero_in_play()
        {
            Assert.That(CutIds(Real), Is.Not.Empty);
            foreach (var kv in ContentLoader.CutClasses)
            {
                var d = Real.TryClass(kv.Key);
                if (d == null) continue;
                Assert.That(d.IsCut, Is.True, kv.Key);
                var into = Real.TryClass(d.CutInto);
                Assert.That(into, Is.Not.Null, kv.Key);
                Assert.That(into.Team, Is.EqualTo(Team.Hero));
                Assert.That(into.IsCut, Is.False);
            }
            Assert.That(CutProblems(Real), Is.Empty);
        }

        [Test]
        public void Validate_rejects_a_cut_class_in_the_summon_pool()
        {
            var db = Copy();
            var cut = CutIds(db).First();
            db.Classes[cut].Acquire = Acquire.Summon;
            Assert.That(CutProblems(db), Has.Some.Contains("Acquire Summon").And.Contains(cut));
        }

        [Test]
        public void Validate_rejects_a_CutInto_that_is_itself_cut()
        {
            var db = Copy();
            var cuts = CutIds(db).ToList();
            Assume.That(cuts.Count, Is.GreaterThanOrEqualTo(2));
            db.Classes[cuts[0]].CutInto = cuts[1];
            Assert.That(CutProblems(db), Has.Some.Contains($"class {cuts[0]}: CutInto"));
        }

        // ── pools ──

        [Test]
        public void Cut_classes_leave_the_wanderer_summon_and_story_pools()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Cut-pools");
            var cuts = CutIds(c).ToList();
            Assert.That(Wanderers.Pool(c, s).Intersect(cuts), Is.Empty);
            Assert.That(Summon.Pool(c, s).Select(d => d.Id).Intersect(cuts), Is.Empty);
            foreach (var cut in cuts)
                Assert.That(Summon.StoryPick(c, s, cut, null), Is.Not.EqualTo(cut), $"a story JoinHero never brings the cut {cut}");
            foreach (var role in Summon.StoryRoleOrder)
                Assert.That(cuts, Does.Not.Contain(Summon.StoryPick(c, s, null, role) ?? ""), role);
        }

        // ── save conversion ──

        [Test]
        public void A_cut_hero_becomes_its_CutInto_hero_with_its_experience_and_gear_in_the_bag()
        {
            var c = Real;
            var cut = c.Classes.Values.First(d => d.IsCut);
            var s = new SaveData();
            var weapon = c.Items.Values.First(i => i.Durability > 0).Id;
            var h = Hero(c, cut.Id, 5000);
            h.WeaponId = weapon;
            h.WeaponDurability = 1;
            s.Roster.Add(h);
            s.Party.Add(cut.Id);
            int level = Progression.Level(h);

            SaveMigrations.ConvertCutHeroes(s, c);

            Assert.That(s.Hero(cut.Id), Is.Null);
            var nh = s.Hero(cut.CutInto);
            Assert.That(nh, Is.Not.Null);
            Assert.That(nh.TotalXp, Is.EqualTo(5000));
            Assert.That(nh.SkillPoints, Is.EqualTo(Formulas.SkillPointsForLevelUp(1, level)), "every point it earned, unspent");
            Assert.That(nh.WeaponId, Is.Null);
            Assert.That(s.Count(weapon), Is.EqualTo(1), "its gear went to the bag");
            Assert.That(s.Party, Is.EqualTo(new[] { cut.CutInto }));
            Assert.That(Codex.IsUnlocked(s, CodexKinds.Character, cut.CutInto), Is.True);

            SaveMigrations.ConvertCutHeroes(s, c);
            Assert.That(s.Roster, Has.Count.EqualTo(1), "idempotent");
        }

        [Test]
        public void A_cut_hero_folds_into_an_owned_CutInto_hero()
        {
            var c = Real;
            var cut = c.Classes.Values.First(d => d.IsCut);
            var s = new SaveData();
            var into = Hero(c, cut.CutInto, 1000);
            s.Roster.Add(into);
            s.Roster.Add(Hero(c, cut.Id, 4000));
            s.Party.AddRange(new[] { cut.CutInto, cut.Id });
            int before = Progression.Level(into), sp = into.SkillPoints;

            SaveMigrations.ConvertCutHeroes(s, c);

            Assert.That(s.Roster, Has.Count.EqualTo(1));
            Assert.That(s.Roster[0], Is.SameAs(into));
            Assert.That(into.TotalXp, Is.EqualTo(5000));
            Assert.That(into.SkillPoints, Is.EqualTo(sp + Formulas.SkillPointsForLevelUp(before, Progression.Level(into))));
            Assert.That(s.Party, Is.EqualTo(new[] { cut.CutInto }), "no duplicate party slot");
        }

        [Test]
        public void Two_cut_heroes_into_the_same_hero_make_one()
        {
            var c = Real;
            var group = c.Classes.Values.Where(d => d.IsCut).GroupBy(d => d.CutInto).FirstOrDefault(g => g.Count() >= 2);
            Assume.That(group, Is.Not.Null);
            var s = new SaveData();
            foreach (var d in group) { s.Roster.Add(Hero(c, d.Id, 2000)); s.Party.Add(d.Id); }

            SaveMigrations.ConvertCutHeroes(s, c);

            Assert.That(s.Roster.Select(h => h.ClassId), Is.EqualTo(new[] { group.Key }));
            Assert.That(s.Roster[0].TotalXp, Is.EqualTo(2000 * group.Count()));
            Assert.That(s.Party, Is.EqualTo(new[] { group.Key }));
        }

        [Test]
        public void Migrate_converts_a_cut_hero_on_load()
        {
            var c = Real;
            var cut = c.Classes.Values.First(d => d.IsCut);
            var s = Progression.NewGame(c, "Cut-load");
            s.Roster.Add(Hero(c, cut.Id, 3000));
            var migrated = SaveMigrations.Migrate(s, c);
            Assert.That(migrated.Hero(cut.Id), Is.Null);
            Assert.That(migrated.Hero(cut.CutInto), Is.Not.Null);
        }
    }
}
