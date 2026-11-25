using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Content;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>GG-T977 (epic GG-T953): a SkillTreeDef whose Id is a families.json id is that family's shared tree. Nodes
    /// carry a layout cell (Col, Row) and an optional SignatureOf; a hero class lists the StartNodes it begins with.
    /// ContentLoader.ValidateFamilyTrees checks each rule, and the 7 role trees keep working beside it.</summary>
    public class FamilyTreeModelTests
    {
        private static List<string> Problems(ContentDb db)
        {
            var p = new List<string>();
            ContentLoader.ValidateFamilyTrees(db, p);
            return p;
        }

        /// <summary>Family "shadow" with thief and ninja, each starting on its own corner of one shared tree.</summary>
        private static ContentDb Fixture()
        {
            var db = new ContentDb();
            db.Families.Add(new FamilyDef { Id = "shadow", Name = "Shadow", Theme = "t", From = "AGI", To = "INT", Ring = 1 });
            db.Families.Add(new FamilyDef { Id = "vanguard", Name = "Vanguard", Theme = "t", From = "VIT", To = "STR", Ring = 2 });
            db.Classes["thief"] = new ClassDef { Id = "thief", Team = Team.Hero, Family = "shadow", HomeTree = "rogue",
                DefaultLoadout = { "steal", "item:potion", "dash" }, StartNodes = { "t_root", "t_steal" } };
            db.Classes["ninja"] = new ClassDef { Id = "ninja", Team = Team.Hero, Family = "shadow", HomeTree = "rogue",
                DefaultLoadout = { "shuriken" }, StartNodes = { "n_root", "n_shuriken" } };
            db.Classes["knight"] = new ClassDef { Id = "knight", Team = Team.Hero, Family = "vanguard", HomeTree = "rogue" };
            db.Classes["slime"] = new ClassDef { Id = "slime", Team = Team.Enemy };
            db.Abilities["steal"] = new AbilityDef { Id = "steal", Kind = AbilityKind.Skill };
            db.Abilities["shuriken"] = new AbilityDef { Id = "shuriken", Kind = AbilityKind.Skill };
            db.Abilities["dash"] = new AbilityDef { Id = "dash", Kind = AbilityKind.Skill, Universal = true };
            db.Abilities["vanish"] = new AbilityDef { Id = "vanish", Kind = AbilityKind.Skill };
            db.SkillTrees["shadow"] = new SkillTreeDef
            {
                Id = "shadow",
                Name = "Shadow",
                Nodes =
                {
                    new SkillNodeDef { Id = "t_root", GrantsStat = new StatGrantDef { Stat = "Agility", Amount = 1 }, Col = 0, Row = 0, SignatureOf = "thief" },
                    new SkillNodeDef { Id = "t_steal", Grants = "steal", Col = 0, Row = 1, SignatureOf = "thief", Prerequisites = { "t_root" } },
                    new SkillNodeDef { Id = "n_root", GrantsStat = new StatGrantDef { Stat = "Luck", Amount = 1 }, Col = 4, Row = 0, SignatureOf = "ninja" },
                    new SkillNodeDef { Id = "n_shuriken", Grants = "shuriken", Col = 4, Row = 1, SignatureOf = "ninja", Prerequisites = { "n_root" } },
                    // A shared node both siblings can converge on.
                    new SkillNodeDef { Id = "s_vanish", Grants = "vanish", Col = 2, Row = 2, Prerequisites = { "t_steal" } },
                },
            };
            // A role tree beside it: no layout, so every node sits on (0, 0) and that is fine.
            db.SkillTrees["rogue"] = new SkillTreeDef
            {
                Id = "rogue",
                Name = "Rogue",
                Nodes =
                {
                    new SkillNodeDef { Id = "r_a", Grants = "steal" },
                    new SkillNodeDef { Id = "r_b", Grants = "shuriken" },
                },
            };
            return db;
        }

        private static SkillNodeDef Node(ContentDb db, string id) => db.SkillTrees["shadow"].Nodes.Single(n => n.Id == id);

        [Test]
        public void A_valid_family_tree_beside_a_role_tree_is_clean()
        {
            Assert.That(Problems(Fixture()), Is.Empty);
        }

        [Test]
        public void Real_content_has_no_family_tree_problems()
        {
            Assert.That(Problems(Real), Is.Empty);
        }

        [Test]
        public void A_class_without_start_nodes_is_not_checked()
        {
            var db = Fixture();
            db.Classes["knight"].DefaultLoadout.Add("steal");
            Assert.That(Problems(db), Is.Empty);
        }

        [Test]
        public void A_start_node_outside_the_family_tree_is_reported()
        {
            var db = Fixture();
            db.Classes["thief"].StartNodes.Add("r_a"); // in the role tree, not the family tree
            db.Classes["ninja"].StartNodes.Add("nowhere");
            var p = Problems(db);
            Assert.That(p.Any(x => x.Contains("class thief") && x.Contains("'r_a' is not in family tree shadow")), Is.True, string.Join("\n", p));
            Assert.That(p.Any(x => x.Contains("class ninja") && x.Contains("'nowhere' is not in family tree shadow")), Is.True, string.Join("\n", p));
        }

        [Test]
        public void Start_nodes_without_a_family_tree_are_reported()
        {
            var db = Fixture();
            db.Classes["knight"].StartNodes.Add("t_root"); // vanguard has no tree yet
            var p = Problems(db);
            Assert.That(p.Any(x => x.Contains("class knight") && x.Contains("family tree 'vanguard'")), Is.True, string.Join("\n", p));
        }

        [Test]
        public void An_enemy_with_start_nodes_is_reported()
        {
            var db = Fixture();
            db.Classes["slime"].StartNodes.Add("t_root");
            Assert.That(Problems(db).Any(x => x.Contains("class slime") && x.Contains("only a hero class")), Is.True);
        }

        [Test]
        public void A_start_node_whose_prerequisite_is_not_a_start_node_is_reported()
        {
            var db = Fixture();
            db.Classes["thief"].StartNodes.Remove("t_root");
            var p = Problems(db);
            Assert.That(p.Any(x => x.Contains("class thief") && x.Contains("'t_steal' needs 't_root'")), Is.True, string.Join("\n", p));
        }

        [Test]
        public void A_loadout_ability_no_start_node_grants_is_reported_but_items_and_universals_are_not()
        {
            var db = Fixture();
            db.Classes["ninja"].DefaultLoadout.Add("vanish");
            var p = Problems(db);
            Assert.That(p.Any(x => x.Contains("class ninja") && x.Contains("loadout ability 'vanish'")), Is.True, string.Join("\n", p));
            Assert.That(p.Any(x => x.Contains("item:potion") || x.Contains("'dash'")), Is.False, string.Join("\n", p));
            Assert.That(p.Count(x => x.Contains("class thief")), Is.Zero, string.Join("\n", p));
        }

        [Test]
        public void Siblings_may_converge_on_the_same_node()
        {
            var db = Fixture();
            db.Classes["thief"].StartNodes.Add("s_vanish");
            db.Classes["ninja"].StartNodes.AddRange(new[] { "t_root", "t_steal", "s_vanish" });
            Assert.That(Problems(db), Is.Empty);
        }

        [Test]
        public void SignatureOf_must_name_a_member_of_the_family()
        {
            var db = Fixture();
            Node(db, "t_root").SignatureOf = "knight"; // a hero of another family
            Node(db, "n_root").SignatureOf = "slime";
            Node(db, "s_vanish").SignatureOf = "nobody";
            db.SkillTrees["rogue"].Nodes[0].SignatureOf = "thief"; // a role tree has no signatures
            var p = Problems(db);
            Assert.That(p.Any(x => x.Contains("node t_root") && x.Contains("'knight' is not a member of family shadow")), Is.True, string.Join("\n", p));
            Assert.That(p.Any(x => x.Contains("node n_root") && x.Contains("'slime'")), Is.True, string.Join("\n", p));
            Assert.That(p.Any(x => x.Contains("node s_vanish") && x.Contains("'nobody'")), Is.True, string.Join("\n", p));
            Assert.That(p.Any(x => x.Contains("tree rogue node r_a") && x.Contains("only for a family tree")), Is.True, string.Join("\n", p));
        }

        [Test]
        public void Layout_cells_are_unique_per_family_tree_and_not_negative()
        {
            var db = Fixture();
            Node(db, "s_vanish").Col = 4; Node(db, "s_vanish").Row = 1; // on n_shuriken's cell
            Node(db, "n_root").Col = -1;
            var p = Problems(db);
            Assert.That(p.Any(x => x.Contains("node s_vanish") && x.Contains("(4, 1) is already node n_shuriken")), Is.True, string.Join("\n", p));
            Assert.That(p.Any(x => x.Contains("node n_root") && x.Contains("can't be negative")), Is.True, string.Join("\n", p));
        }

        [Test]
        public void Node_ids_are_unique_across_family_and_role_trees()
        {
            var db = Fixture();
            db.SkillTrees["rogue"].Nodes.Add(new SkillNodeDef { Id = "t_steal", Grants = "steal" });
            var p = ContentLoader.Validate(db);
            Assert.That(p.Any(x => x.Contains("node id 't_steal' is already used in tree")), Is.True, string.Join("\n", p));
        }

        [Test]
        public void The_new_fields_read_from_json()
        {
            var cls = Newtonsoft.Json.JsonConvert.DeserializeObject<ClassDef>("{\"Id\":\"thief\",\"StartNodes\":[\"a\",\"b\"]}");
            Assert.That(cls.StartNodes, Is.EqualTo(new[] { "a", "b" }));
            var n = Newtonsoft.Json.JsonConvert.DeserializeObject<SkillNodeDef>("{\"Id\":\"a\",\"Col\":3,\"Row\":2,\"SignatureOf\":\"thief\"}");
            Assert.That((n.Col, n.Row, n.SignatureOf), Is.EqualTo((3, 2, "thief")));
            Assert.That(new ClassDef().StartNodes, Is.Empty);
        }
    }
}
