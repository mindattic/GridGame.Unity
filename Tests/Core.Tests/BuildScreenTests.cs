using System.Linq;
using GridGame.Core.Content;
using GridGame.Meta;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>The Camp Status screen's Build-screen read models (GG-T48): tree investment, known
    /// proficiencies and the one-line build summary — all computed off a hero's existing Known list,
    /// never a separate spend ledger (spending points against a tree is still follow-up work, §25.6).</summary>
    public class BuildScreenTests
    {
        private static ContentDb TreeDb()
        {
            var db = Mini();
            db.Classes["knight"].Role = "Tank";
            db.Classes["knight"].HomeTree = "guardian";
            db.Classes["cleric"].Role = "Healer";
            db.Classes["cleric"].HomeTree = "cleric";
            db.Abilities["shield_wall"] = new AbilityDef { Id = "shield_wall", Name = "Shield Wall", Kind = AbilityKind.Skill };
            db.Abilities["sanctuary"] = new AbilityDef { Id = "sanctuary", Name = "Sanctuary", Kind = AbilityKind.Spell };
            db.Abilities["bandage"] = new AbilityDef { Id = "bandage", Name = "Bandage", Kind = AbilityKind.Spell };
            db.SkillTrees["guardian"] = new SkillTreeDef
            {
                Id = "guardian",
                Name = "Guardian",
                Nodes =
                {
                    new SkillNodeDef { Id = "g1", Grants = "shield_wall", Cost = 3 },
                    new SkillNodeDef { Id = "g2", Grants = "fireball", Cost = 4 }, // not known by the knight below
                },
            };
            db.SkillTrees["cleric"] = new SkillTreeDef
            {
                Id = "cleric",
                Name = "Cleric",
                Nodes =
                {
                    new SkillNodeDef { Id = "c1", Grants = "sanctuary", Cost = 3 },
                    new SkillNodeDef { Id = "c2", Grants = "bandage", Cost = 2 },
                },
            };
            return db;
        }

        [Test]
        public void TreeInvestment_sums_cost_only_for_nodes_whose_grant_is_known()
        {
            var c = TreeDb();
            var h = new HeroSave { ClassId = "knight", Known = { "shield_wall" } }; // not fireball
            var trees = Progression.TreeInvestment(c, h);
            Assert.That(trees.Count, Is.EqualTo(1));
            Assert.That(trees[0].TreeId, Is.EqualTo("guardian"));
            Assert.That(trees[0].Points, Is.EqualTo(3));
        }

        [Test]
        public void TreeInvestment_counts_a_cross_tree_ability_toward_its_own_tree()
        {
            var c = TreeDb();
            // A knight (home tree guardian) who also knows a Cleric-tree spell (e.g. gold-Learned).
            var h = new HeroSave { ClassId = "knight", Known = { "shield_wall", "sanctuary" } };
            var trees = Progression.TreeInvestment(c, h);
            Assert.That(trees.Select(t => t.TreeId), Is.EquivalentTo(new[] { "guardian", "cleric" }));
            Assert.That(trees.First(t => t.TreeId == "guardian").Points, Is.EqualTo(3));
            Assert.That(trees.First(t => t.TreeId == "cleric").Points, Is.EqualTo(3));
        }

        [Test]
        public void TreeInvestment_is_empty_when_nothing_known_matches_any_node()
        {
            var c = TreeDb();
            var h = new HeroSave { ClassId = "knight", Known = { } };
            Assert.That(Progression.TreeInvestment(c, h), Is.Empty);
        }

        [Test]
        public void KnownProficiencies_reports_only_the_ones_the_hero_has()
        {
            var c = TreeDb();
            c.Abilities["heavy_armor_proficiency"] = new AbilityDef { Id = "heavy_armor_proficiency", Name = "Heavy Armor Proficiency", Kind = AbilityKind.Passive };
            var withIt = new HeroSave { ClassId = "knight", Known = { "heavy_armor_proficiency" } };
            var withoutIt = new HeroSave { ClassId = "knight", Known = { } };
            Assert.That(Progression.KnownProficiencies(c, withIt), Is.EqualTo(new[] { "Heavy Armor Proficiency" }));
            Assert.That(Progression.KnownProficiencies(c, withoutIt), Is.Empty);
        }

        [Test]
        public void BuildSummary_names_the_class_its_trees_and_its_role()
        {
            var c = TreeDb();
            var h = new HeroSave { ClassId = "knight", Known = { "shield_wall" } };
            var summary = Progression.BuildSummary(c, h);
            Assert.That(summary, Does.StartWith("Knight ·"));
            Assert.That(summary, Does.Contain("Guardian 3"));
            Assert.That(summary, Does.Contain("a tank"));
        }

        [Test]
        public void BuildSummary_adds_a_who_clause_once_a_second_tree_is_meaningfully_invested()
        {
            var c = TreeDb();
            // 3+ points in a second tree (cleric) earns the "who heals" tail; the tree's own branches
            // map one role each (§25.6.1), so Cleric's verb is "heals".
            var h = new HeroSave { ClassId = "knight", Known = { "shield_wall", "sanctuary" } };
            var summary = Progression.BuildSummary(c, h);
            Assert.That(summary, Does.Contain("a tank who heals"));
        }

        [Test]
        public void BuildSummary_has_no_who_clause_below_the_cross_tree_threshold()
        {
            var c = TreeDb();
            // Only 2 points in the cleric tree (below the 3-point flavor threshold): no "who heals" tail.
            var h = new HeroSave { ClassId = "knight", Known = { "shield_wall", "bandage" } };
            var summary = Progression.BuildSummary(c, h);
            Assert.That(summary, Does.Not.Contain("who"));
        }

        [Test]
        public void Every_real_hero_class_produces_a_build_summary_without_throwing()
        {
            var c = Real;
            foreach (var cls in c.Classes.Values.Where(x => x.Team == Team.Hero))
            {
                var h = Progression.NewHero(c, cls.Id);
                var summary = Progression.BuildSummary(c, h);
                Assert.That(summary, Does.StartWith(cls.Name + " ·"));
            }
        }

        // ── Family trees (GG-T979) ──────────────────────────────────────────────────────────────

        /// <summary>A Shadow-family thief (starts on t_root + t_steal) whose HomeTree still names the role tree "rogue",
        /// plus a Martial family tree to cross into.</summary>
        private static ContentDb FamilyDb()
        {
            var db = TreeDb();
            db.Families.Add(new FamilyDef { Id = "shadow", Name = "Shadow", Theme = "t", Primary = "Agility", Secondary = "Speed", Ring = 1 });
            db.Families.Add(new FamilyDef { Id = "martial", Name = "Martial", Theme = "t", Primary = "Vitality", Secondary = "Stamina", Ring = 2 });
            foreach (var id in new[] { "steal", "smoke", "guard_up", "pickpocket" })
                db.Abilities[id] = new AbilityDef { Id = id, Name = id, Kind = AbilityKind.Skill };
            db.SkillTrees["shadow"] = new SkillTreeDef
            {
                Id = "shadow",
                Name = "Shadow",
                Nodes =
                {
                    new SkillNodeDef { Id = "t_root", Cost = 1, GrantsStat = new StatGrantDef { Stat = "Agility", Amount = 1 }, SignatureOf = "thief" },
                    new SkillNodeDef { Id = "t_steal", Cost = 1, Grants = "steal", SignatureOf = "thief", Prerequisites = { "t_root" } },
                    new SkillNodeDef { Id = "t_smoke", Cost = 2, Grants = "smoke", SignatureOf = "thief", Prerequisites = { "t_steal" } },
                },
            };
            db.SkillTrees["martial"] = new SkillTreeDef { Id = "martial", Name = "Martial", Nodes = { new SkillNodeDef { Id = "v_guard", Cost = 3, Grants = "guard_up" } } };
            db.SkillTrees["rogue"] = new SkillTreeDef { Id = "rogue", Name = "Rogue", Nodes = { new SkillNodeDef { Id = "r_pick", Cost = 3, Grants = "pickpocket" } } };
            db.Classes["thief"] = new ClassDef { Id = "thief", Name = "Thief", Team = Team.Hero, Role = "Striker", Family = "shadow",
                HomeTree = "rogue", DefaultLoadout = { "steal" }, StartNodes = { "t_root", "t_steal" } };
            return db;
        }

        [Test]
        public void TreeInvestment_names_a_family_tree_and_counts_its_start_nodes()
        {
            var c = FamilyDb();
            var h = Progression.NewHero(c, "thief");
            var trees = Progression.TreeInvestment(c, h);
            Assert.That(trees.Count, Is.EqualTo(1));
            Assert.That(trees[0].TreeId, Is.EqualTo("shadow"));
            Assert.That(trees[0].TreeName, Is.EqualTo("Shadow family"));
            Assert.That(trees[0].Points, Is.EqualTo(2), "the stat start node counts too");
            Assert.That(Progression.TreeLabel(c, c.SkillTrees["guardian"]), Is.EqualTo("Guardian"), "a role tree keeps its own name");
        }

        [Test]
        public void BuildSummary_names_the_family_and_reads_its_tail_off_the_family_home()
        {
            var c = FamilyDb();
            var h = Progression.NewHero(c, "thief");
            Assert.That(Progression.BuildSummary(c, h), Is.EqualTo("Thief · Shadow family 2 — a striker"));

            h.Known.Add("smoke");
            h.Known.Add("guard_up");
            var summary = Progression.BuildSummary(c, h);
            Assert.That(summary, Does.StartWith("Thief · Shadow family 4 / Martial family 3"));
            Assert.That(summary, Does.EndWith("a striker who holds the line"), "another family's verb");
        }

        [Test]
        public void BuildSummary_treats_the_old_role_HomeTree_as_a_cross_tree_once_the_family_is_home()
        {
            var c = FamilyDb();
            var h = Progression.NewHero(c, "thief");
            h.Known.Add("smoke");
            h.Known.Add("pickpocket");
            var summary = Progression.BuildSummary(c, h);
            Assert.That(summary, Does.Contain("Shadow family 4 / Rogue 3"));
            Assert.That(summary, Does.EndWith("a striker who skirmishes"), "rogue is not home any more, so it earns the tail");
        }
    }
}
