using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Content;
using GridGame.Meta;
using NUnit.Framework;

namespace GridGame.Tests
{
    /// <summary>GG-T980 (epic GG-T963): gold Learn and stat requirements in a family world. A family-tree class's gold
    /// list is its start and signature grants (kit price) plus the general catalog less every family move, so a sibling's
    /// corner and the shared family nodes come only through the tree. A family node whose grant has Requires is Locked
    /// until the hero's skill hexagon stats meet it ("needs INT 9 (have 3)", the GG-T592 text; GG-T1058 reads
    /// <see cref="HeroBoard.BoardStats"/>, not level-up stat picks), unless it is one of the hero's start
    /// nodes or its own class's signature node; a sibling's signature node stays stat-gated while
    /// the family stat gates are gone with the talent trees (GG-T1081).</summary>
    public class FamilyLearnTests
    {
        /// <summary>Family "shadow": thief and ninja on one tree, each from its own corner, plus shared nodes; knight on a
        /// role tree whose class list holds a skill with a stat requirement; a tiny skill hexagon whose passive nodes
        /// (<see cref="Int3"/>, <see cref="Int6"/>, <see cref="Wis5"/>) are where a hero's Requires stats come from.</summary>
        private static ContentDb Fixture()
        {
            var db = new ContentDb();
            db.Families.Add(new FamilyDef { Id = "shadow", Name = "Shadow", Theme = "t", From = "AGI", To = "INT", Ring = 1 });
            db.Classes["thief"] = new ClassDef { Id = "thief", Name = "Thief", Team = Team.Hero, Family = "shadow", HomeTree = "shadow",
                DefaultLoadout = { "steal" }, StartNodes = { "t_root", "t_steal" }, Abilities = { "steal", "star_rain", "vanish" } };
            db.Classes["ninja"] = new ClassDef { Id = "ninja", Name = "Ninja", Team = Team.Hero, Family = "shadow", HomeTree = "shadow",
                DefaultLoadout = { "shuriken" }, StartNodes = { "n_root", "n_shuriken" } };
            db.Classes["knight"] = new ClassDef { Id = "knight", Name = "Knight", Team = Team.Hero, HomeTree = "guard",
                DefaultLoadout = { "cleave" }, Abilities = { "cleave", "shield_wall" } };
            foreach (var id in new[] { "steal", "cleave" })
                db.Abilities[id] = new AbilityDef { Id = id, Name = id, Kind = AbilityKind.Skill, CooldownTurns = 4 };
            db.Abilities["shuriken"] = new AbilityDef { Id = "shuriken", Name = "Shuriken", Kind = AbilityKind.Skill, CooldownTurns = 2,
                Requires = new Stats { Intelligence = 4 } };
            db.Abilities["vanish"] = new AbilityDef { Id = "vanish", Name = "Vanish", Kind = AbilityKind.Skill, CooldownTurns = 3, Universal = true };
            db.Abilities["shield_wall"] = new AbilityDef { Id = "shield_wall", Name = "Shield Wall", Kind = AbilityKind.Skill, CooldownTurns = 4,
                Requires = new Stats { Vitality = 6 } };
            db.Abilities["smoke"] = new AbilityDef { Id = "smoke", Name = "Smoke", Kind = AbilityKind.Spell, Cost = 3,
                Requires = new Stats { Agility = 9 } };
            db.Abilities["star_rain"] = new AbilityDef { Id = "star_rain", Name = "Star Rain", Kind = AbilityKind.Spell, Cost = 3,
                Requires = new Stats { Intelligence = 9 } };
            db.Abilities["shade"] = new AbilityDef { Id = "shade", Name = "Shade", Kind = AbilityKind.Spell, Cost = 2,
                Requires = new Stats { Wisdom = 5 } };
            db.Abilities["spark"] = new AbilityDef { Id = "spark", Name = "Spark", Kind = AbilityKind.Spell, Cost = 1 };
            db.SkillTrees["shadow"] = new SkillTreeDef
            {
                Id = "shadow",
                Name = "Shadow",
                Nodes =
                {
                    new SkillNodeDef { Id = "t_root", Name = "Quick Hands", Cost = 1, GrantsStat = new StatGrantDef { Stat = "Agility", Amount = 1 }, SignatureOf = "thief" },
                    new SkillNodeDef { Id = "t_steal", Name = "Steal", Cost = 1, Grants = "steal", SignatureOf = "thief", Prerequisites = { "t_root" } },
                    new SkillNodeDef { Id = "t_smoke", Name = "Smoke", Cost = 2, Grants = "smoke", SignatureOf = "thief", Prerequisites = { "t_steal" } },
                    new SkillNodeDef { Id = "n_root", Name = "Sharp Eye", Cost = 1, GrantsStat = new StatGrantDef { Stat = "Luck", Amount = 1 }, SignatureOf = "ninja" },
                    new SkillNodeDef { Id = "n_shuriken", Name = "Shuriken", Cost = 1, Grants = "shuriken", SignatureOf = "ninja", Prerequisites = { "n_root" } },
                    new SkillNodeDef { Id = "n_rain", Name = "Star Rain", Cost = 2, Grants = "star_rain", SignatureOf = "ninja" },
                    new SkillNodeDef { Id = "s_shade", Name = "Shade", Cost = 2, Grants = "shade" },
                    new SkillNodeDef { Id = "s_vanish", Name = "Vanish", Cost = 2, Grants = "vanish" },
                },
            };
            db.SkillTrees["guard"] = new SkillTreeDef
            {
                Id = "guard",
                Name = "Guardian",
                Nodes =
                {
                    new SkillNodeDef { Id = "g_cleave", Grants = "cleave" },
                    new SkillNodeDef { Id = "g_wall", Name = "Shield Wall", Cost = 1, Grants = "shield_wall" },
                },
            };
            // GG-T1067: Requires are measured against owned hexagon passives (LCK is not a board stat). No Heroes, so a
            // hero with no HexNodes owns nothing and has all-zero board stats.
            db.SkillHex = new SkillHexDef
            {
                Version = 1,
                Radius = 2,
                Costs = new SkillHexCostsDef { Passive = 1, Ability = 1 },
                Nodes =
                {
                    new SkillHexNodeDef { Q = 1, R = 0, Kind = SkillHexNodeKind.Passive, Stats = new Dictionary<string, int> { ["INT"] = 3 } },
                    new SkillHexNodeDef { Q = 2, R = 0, Kind = SkillHexNodeKind.Passive, Stats = new Dictionary<string, int> { ["INT"] = 6 } },
                    new SkillHexNodeDef { Q = 0, R = 1, Kind = SkillHexNodeKind.Passive, Stats = new Dictionary<string, int> { ["WIS"] = 5 } },
                },
            };
            return db;
        }

        private static SkillTreeDef Shadow(ContentDb db) => db.SkillTrees["shadow"];
        private static SkillNodeDef Node(ContentDb db, string id) => Shadow(db).Nodes.First(n => n.Id == id);
        private static SaveData Save() => new SaveData { TestMode = true, Gold = 10000 };

        private const string Int3 = "1,0", Int6 = "2,0", Wis5 = "0,1";

        /// <summary>A hero owning the given fixture hexagon nodes ("q,r").</summary>
        private static HeroSave Hero(ContentDb db, string classId, params string[] hexNodes)
        {
            var h = Progression.NewHero(db, classId);
            h.SkillPoints = 100;
            h.HexNodes = new List<string>(hexNodes);
            return h;
        }

        [Test]
        public void FamilyHero_GoldList_IsItsOwnCornerPlusTheGeneralCatalog()
        {
            var db = Fixture();
            var thief = Hero(db, "thief");
            var ids = Abilities.Learnable(db, thief).Select(a => a.Id).ToList();
            Assert.That(ids, Does.Contain("smoke"), "its own signature grant");
            Assert.That(ids, Does.Contain("spark"), "a general spell on no family tree");
            Assert.That(ids, Does.Not.Contain("star_rain"), "a sibling's signature comes only through the tree, even on the class list");
            Assert.That(ids, Does.Not.Contain("shade"), "a shared family spell comes only through the tree");
            Assert.That(ids, Does.Not.Contain("vanish"), "a Universal move on a family node comes only through the tree");
            Assert.That(ids, Does.Not.Contain("steal"), "a start grant is already known");
            Assert.That(ids.First(), Is.EqualTo("smoke"), "its own corner sorts first");
            Assert.That(Abilities.Price(db, Save(), thief, db.Abilities["smoke"]),
                Is.EqualTo(System.Math.Min(Abilities.KitPrice, Abilities.Price(db.Abilities["smoke"]))), "own signature at kit price");

            var ninjaIds = Abilities.Learnable(db, Hero(db, "ninja")).Select(a => a.Id).ToList();
            Assert.That(ninjaIds, Does.Contain("star_rain"));
            Assert.That(ninjaIds, Does.Not.Contain("smoke"));
        }

        [Test]
        public void FamilyHero_CannotGoldLearnASiblingsMove()
        {
            var db = Fixture();
            var s = Save();
            var thief = Hero(db, "thief", Int3, Int6);
            var r = Abilities.Learn(db, s, thief, "star_rain");
            Assert.That(r.Ok, Is.False);
            Assert.That(r.Message, Is.EqualTo("Can't learn that"));
            Assert.That(thief.Known, Does.Not.Contain("star_rain"));
            Assert.That(s.Gold, Is.EqualTo(10000));
            Assert.That(Abilities.Learn(db, s, thief, "smoke").Ok, Is.True, "its own signature is bought with gold, Requires waived");
        }

        [Test]
        public void RoleTreeHero_KeepsItsClassList_AndTheWholeCatalog()
        {
            var db = Fixture();
            var knight = Hero(db, "knight");
            var ids = Abilities.Learnable(db, knight).Select(a => a.Id).ToList();
            Assert.That(ids, Does.Contain("shield_wall"));
            Assert.That(ids, Does.Contain("smoke").And.Contain("star_rain").And.Contain("shade").And.Contain("spark").And.Contain("vanish"));
        }

    }
}
