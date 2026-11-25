using System.Linq;
using GridGame.Core;
using GridGame.Core.Content;
using GridGame.Meta;
using NUnit.Framework;

namespace GridGame.Tests
{
    /// <summary>GG-T978 (epic GG-T953): a new hero whose class's HomeTree is a family tree starts specced from its
    /// ClassDef.StartNodes: their grants are Known at creation, the nodes read Innate (stat nodes too), count toward the
    /// tree's points and are never refunded. Kit pricing and the Requires waiver cover start-node grants and the class's
    /// SignatureOf nodes; A role-tree class keeps the
    /// DefaultLoadout rules (GG-T388).</summary>
    public class StartingSpecTests
    {
        /// <summary>Family "shadow": thief and ninja on one tree, each from its own corner; knight on a role tree.</summary>
        private static ContentDb Fixture()
        {
            var db = new ContentDb();
            db.Families.Add(new FamilyDef { Id = "shadow", Name = "Shadow", Theme = "t", From = "AGI", To = "INT", Ring = 1 });
            db.Classes["thief"] = new ClassDef { Id = "thief", Name = "Thief", Team = Team.Hero, Family = "shadow", HomeTree = "shadow",
                DefaultLoadout = { "steal", "item:potion", "item:potion", "dash" }, StartNodes = { "t_root", "t_steal" } };
            db.Classes["ninja"] = new ClassDef { Id = "ninja", Name = "Ninja", Team = Team.Hero, Family = "shadow", HomeTree = "shadow",
                DefaultLoadout = { "shuriken" }, StartNodes = { "n_root", "n_shuriken" } };
            db.Classes["knight"] = new ClassDef { Id = "knight", Name = "Knight", Team = Team.Hero, HomeTree = "guard",
                DefaultLoadout = { "cleave", "taunt", "bulwark", "item:potion" } };
            foreach (var id in new[] { "steal", "shuriken", "vanish", "cleave", "taunt", "bulwark" })
                db.Abilities[id] = new AbilityDef { Id = id, Name = id, Kind = AbilityKind.Skill, CooldownTurns = 4 };
            db.Abilities["dash"] = new AbilityDef { Id = "dash", Name = "Dash", Kind = AbilityKind.Skill, Universal = true };
            db.Abilities["smoke"] = new AbilityDef { Id = "smoke", Name = "Smoke", Kind = AbilityKind.Spell, Cost = 3,
                Requires = new Stats { Agility = 9 } };
            db.Abilities["star_rain"] = new AbilityDef { Id = "star_rain", Name = "Star Rain", Kind = AbilityKind.Spell, Cost = 3,
                Requires = new Stats { Luck = 9 } };
            db.SkillTrees["shadow"] = new SkillTreeDef
            {
                Id = "shadow",
                Name = "Shadow",
                Nodes =
                {
                    new SkillNodeDef { Id = "t_root", Name = "Quick Hands", Cost = 1, GrantsStat = new StatGrantDef { Stat = "Agility", Amount = 1 }, Col = 0, Row = 0, SignatureOf = "thief" },
                    new SkillNodeDef { Id = "t_steal", Name = "Steal", Cost = 1, Grants = "steal", Col = 0, Row = 1, SignatureOf = "thief", Prerequisites = { "t_root" } },
                    new SkillNodeDef { Id = "t_smoke", Name = "Smoke", Cost = 2, Grants = "smoke", Col = 0, Row = 2, SignatureOf = "thief", Prerequisites = { "t_steal" } },
                    new SkillNodeDef { Id = "n_root", Name = "Sharp Eye", Cost = 1, GrantsStat = new StatGrantDef { Stat = "Luck", Amount = 1 }, Col = 4, Row = 0, SignatureOf = "ninja" },
                    new SkillNodeDef { Id = "n_shuriken", Name = "Shuriken", Cost = 1, Grants = "shuriken", Col = 4, Row = 1, SignatureOf = "ninja", Prerequisites = { "n_root" } },
                    new SkillNodeDef { Id = "n_rain", Name = "Star Rain", Cost = 2, Grants = "star_rain", Col = 4, Row = 2, SignatureOf = "ninja", Prerequisites = { "n_shuriken" } },
                    new SkillNodeDef { Id = "s_vanish", Name = "Vanish", Cost = 2, Grants = "vanish", Col = 2, Row = 2, Prerequisites = { "t_steal" } },
                },
            };
            db.SkillTrees["guard"] = new SkillTreeDef
            {
                Id = "guard",
                Name = "Guardian",
                Nodes =
                {
                    new SkillNodeDef { Id = "g_cleave", Grants = "cleave" },
                    new SkillNodeDef { Id = "g_taunt", Grants = "taunt" },
                    new SkillNodeDef { Id = "g_bulwark", Grants = "bulwark" },
                },
            };
            return db;
        }

        private static SkillTreeDef Shadow(ContentDb db) => db.SkillTrees["shadow"];
        private static SkillNodeDef Node(ContentDb db, string id) => Shadow(db).Nodes.First(n => n.Id == id);
        private static SaveData Save() => new SaveData { TestMode = true, Gold = 10000 };

        [Test]
        public void FamilyHero_KnowsItsStartGrants_AndBarsTheLoadoutInOrder()
        {
            var db = Fixture();
            var h = Progression.NewHero(db, "thief");
            Assert.That(Progression.IsFamilySpec(db, db.Class("thief")), Is.True);
            Assert.That(h.Known, Is.EquivalentTo(new[] { "steal", "dash" }), "start grant plus the Universal loadout move");
            Assert.That(h.Bar.Take(4), Is.EqualTo(new[] { "steal", "item:potion", "item:potion", "dash" }), "the loadout in its authored order");
            Assert.That(h.Known, Does.Not.Contain("smoke"), "a signature node past the start is not learned");
            Assert.That(Progression.NewHero(db, "ninja").Known, Is.EquivalentTo(new[] { "shuriken" }), "siblings start apart");
        }

        [Test]
        public void RoleTreeClass_KeepsTheDefaultLoadoutRules()
        {
            var db = Fixture();
            var def = db.Class("knight");
            Assert.That(Progression.IsFamilySpec(db, def), Is.False);
            Assert.That(Progression.StarterKit(db, def), Is.EqualTo(Progression.StarterKit(def)));
            Assert.That(Progression.KitAbilities(db, def), Is.EqualTo(Progression.KitAbilities(def)));
            var h = Progression.NewHero(db, "knight");
            Assert.That(h.Known, Is.EqualTo(new[] { "cleave", "bulwark" }));
            Assert.That(h.Bar.Take(3), Is.EqualTo(new[] { "cleave", "bulwark", "item:potion" }));
        }

        [Test]
        public void StartGrantsAndSignatureNodes_AreKitMoves_AndWaivedFromRequires()
        {
            var db = Fixture();
            var s = Save();
            var thief = Progression.NewHero(db, "thief");
            var smoke = db.Abilities["smoke"];
            var rain = db.Abilities["star_rain"];
            Assert.That(Abilities.IsKitMove(db, thief, db.Abilities["steal"]), Is.True, "a start grant is a kit move");
            Assert.That(Abilities.IsKitMove(db, thief, smoke), Is.True, "its own signature node is a kit move");
            Assert.That(Abilities.IsKitMove(db, thief, rain), Is.False, "a sibling's signature is not");
            Assert.That(Abilities.RequiresWaived(db, s, thief, smoke), Is.True);
            Assert.That(Abilities.Unmet(db, s, thief, smoke), Is.Empty);
            Assert.That(Abilities.RequiresWaived(db, s, thief, rain), Is.False);
            Assert.That(Abilities.Unmet(db, s, thief, rain), Is.Not.Empty);
        }

        [Test]
        public void TrainTo_AddsNothingPastTheStart_OnAFamilyTree()
        {
            var db = Fixture();
            var h = Progression.NewHero(db, "thief");
            h.TotalXp = 1_000_000;
            var before = h.Known.ToList();
            Progression.TrainTo(db, h);
            Assert.That(h.Known, Is.EqualTo(before));
        }
    }
}
