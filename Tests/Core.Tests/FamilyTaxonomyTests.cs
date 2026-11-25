using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Content;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>GG-T971 (BIBLE §25.6.6): the hero families. Every hero class names exactly one known family, no family is
    /// empty or a lone member without a reason, the ring runs 1..N, and FamilyContent.Validate catches each break.</summary>
    public class FamilyTaxonomyTests
    {
        private static List<string> FamilyProblems(ContentDb db)
        {
            var p = new List<string>();
            FamilyContent.Validate(db, p);
            return p;
        }

        [Test]
        public void Real_content_has_the_six_hex_families_and_every_hero_is_in_one()
        {
            var db = Real;
            Assert.That(db.FamiliesFileMissing, Is.False);
            Assert.That(db.Families.Count, Is.EqualTo(6));
            Assert.That(db.Families.OrderBy(f => f.Ring).Select(f => f.Id), Is.EqualTo(db.SkillHex.Families.Select(f => f.Id)), "GG-T1060: the hex families in ring order");
            Assert.That(FamilyProblems(db), Is.Empty);
            var heroes = db.Classes.Values.Where(c => c.Team == Team.Hero).ToList();
            Assert.That(heroes.Sum(c => 1), Is.EqualTo(db.Families.Sum(f => db.FamilyMembers(f.Id).Count)));
            foreach (var c in heroes) Assert.That(db.FamilyOf(c.Id), Is.Not.Null, c.Id);
            foreach (var c in db.Classes.Values.Where(c => c.Team != Team.Hero)) Assert.That(c.Family, Is.Null, c.Id);
        }

        [Test]
        public void The_owner_examples_share_a_family()
        {
            var db = Real;
            Assert.That(db.FamilyOf("thief").Id, Is.EqualTo(db.FamilyOf("blackninja").Id), "Thief and Ninja");
            Assert.That(db.FamilyOf("knight").Id, Is.EqualTo(db.FamilyOf("defender").Id), "Knight and Defender");
        }

        [Test]
        public void Ring_slots_run_one_to_n_and_every_family_has_two_members_or_more()
        {
            var db = Real;
            Assert.That(db.Families.Select(f => f.Ring).OrderBy(r => r), Is.EqualTo(Enumerable.Range(1, db.Families.Count)));
            foreach (var f in db.Families) Assert.That(db.FamilyMembers(f.Id).Count, Is.GreaterThanOrEqualTo(2), f.Id);
        }

        private static ContentDb Mini()
        {
            var db = new ContentDb();
            db.Classes["a"] = new ClassDef { Id = "a", Team = Team.Hero, Family = "f1" };
            db.Classes["b"] = new ClassDef { Id = "b", Team = Team.Hero, Family = "f1" };
            db.Classes["c"] = new ClassDef { Id = "c", Team = Team.Hero, Family = "f2" };
            db.Classes["d"] = new ClassDef { Id = "d", Team = Team.Hero, Family = "f2" };
            db.Classes["slime"] = new ClassDef { Id = "slime", Team = Team.Enemy };
            db.Families.Add(new FamilyDef { Id = "f1", Name = "One", Theme = "t", From = "STR", To = "VIT", Ring = 1 });
            db.Families.Add(new FamilyDef { Id = "f2", Name = "Two", Theme = "t", From = "AGI", To = "SPD", Ring = 2 });
            return db;
        }

        [Test]
        public void A_valid_fixture_is_clean_and_a_hand_built_db_without_families_is_skipped()
        {
            Assert.That(FamilyProblems(Mini()), Is.Empty);
            var bare = new ContentDb();
            bare.Classes["a"] = new ClassDef { Id = "a", Team = Team.Hero };
            Assert.That(FamilyProblems(bare), Is.Empty);
            bare.FamiliesFileMissing = true;
            Assert.That(FamilyProblems(bare).Single(), Does.Contain("families.json is missing"));
        }

        [Test]
        public void A_hero_without_a_family_or_with_an_unknown_one_is_reported()
        {
            var db = Mini();
            db.Classes["a"].Family = null;
            db.Classes["b"].Family = "nope";
            var p = FamilyProblems(db);
            Assert.That(p, Has.Some.Contains("class a: hero needs a Family"));
            Assert.That(p, Has.Some.Contains("class b: unknown family 'nope'"));
        }

        [Test]
        public void An_enemy_naming_a_family_is_reported()
        {
            var db = Mini();
            db.Classes["slime"].Family = "f1";
            Assert.That(FamilyProblems(db).Single(), Does.Contain("class slime: only a hero class names a Family"));
        }

        [Test]
        public void An_empty_family_and_an_unjustified_lone_member_are_reported()
        {
            var db = Mini();
            db.Families.Add(new FamilyDef { Id = "f3", Name = "Three", Theme = "t", From = "WIS", To = "INT", Ring = 3 });
            Assert.That(FamilyProblems(db).Single(), Does.Contain("family f3: no hero class belongs to it"));
            db.Classes["d"].Family = "f3";
            var p = FamilyProblems(db);
            Assert.That(p, Has.Some.Contains("family f2: only one member"));
            Assert.That(p, Has.Some.Contains("family f3: only one member"));
            db.Families[1].SoloReason = "signature";
            db.Families[2].SoloReason = "signature";
            Assert.That(FamilyProblems(db), Is.Empty);
        }

        [Test]
        public void Bad_ids_stats_and_ring_slots_are_reported()
        {
            var db = Mini();
            db.Families[1].From = "Str";
            db.Families[1].Ring = 1;
            db.Families.Add(new FamilyDef { Id = "f1", Name = "", Theme = "", From = "INT", To = "INT", Ring = 9 });
            var p = FamilyProblems(db);
            Assert.That(p, Has.Some.Contains("family f2: unknown From stat 'Str'"));
            Assert.That(p, Has.Some.Contains("family f2: Ring 1 already taken"));
            Assert.That(p, Has.Some.Contains("family f1: duplicate id"));
            Assert.That(p, Has.Some.Contains("family f1: needs a Name"));
            Assert.That(p, Has.Some.Contains("family f1: needs a Theme line"));
            Assert.That(p, Has.Some.Contains("family f1: From and To are the same stat"));
            Assert.That(p, Has.Some.Contains("family f1: Ring 9 outside 1..3"));
        }
    }
}
