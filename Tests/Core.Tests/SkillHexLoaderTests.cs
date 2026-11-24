using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GridGame.Core.Content;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>GG-T1022: skillhex.json loads into <see cref="ContentDb.SkillHex"/> and <see cref="ContentLoader.ValidateSkillHex"/>
    /// holds it to tools/skillhex/validate.py's rules. Each broken fixture is the real board with one thing changed.</summary>
    public class SkillHexLoaderTests
    {
        private static JObject Board() => JObject.Parse(File.ReadAllText(Path.Combine(ContentDir, "skillhex.json")));

        /// <summary>A private ContentDb with the real classes, abilities and aliases (TestKit.Real is shared).</summary>
        private static ContentDb Db()
        {
            var db = new ContentDb();
            foreach (var kv in Real.Classes) db.Classes[kv.Key] = kv.Value;
            foreach (var kv in Real.Abilities) db.Abilities[kv.Key] = kv.Value;
            foreach (var kv in Real.AbilityAliases) db.AbilityAliases[kv.Key] = kv.Value;
            return db;
        }

        private static List<string> Problems(JObject board, ContentDb db = null)
        {
            var p = new List<string>();
            SkillHexContent.Validate(db ?? Db(), ContentLoader.ParseSkillHex(board.ToString()), p);
            return p;
        }

        private static JArray Nodes(JObject b) => (JArray)b["Nodes"];
        private static JArray Heroes(JObject b) => (JArray)b["Heroes"];
        private static JObject Node(JObject b, string kind) => Nodes(b).Children<JObject>().First(n => (string)n["Kind"] == kind);
        private static JObject Node(JObject b, int q, int r) =>
            Nodes(b).Children<JObject>().First(n => (int)n["Q"] == q && (int)n["R"] == r);
        private static JObject Placed(JObject b, int i = 0) =>
            Heroes(b).Children<JObject>().Where(h => (string)h["Status"] == "Placed").ElementAt(i);

        /// <summary>The node that holds a hero's Signature.</summary>
        private static JObject SignatureNode(JObject b, JObject hero) =>
            Nodes(b).Children<JObject>().First(n => (string)n["Ability"] == (string)hero["Signature"]);

        // ── the real board ──

        [Test]
        public void The_real_board_loads_and_validates_clean()
        {
            var hex = Real.SkillHex;
            Assert.That(hex, Is.Not.Null, "skillhex.json is in Resources/Content");
            Assert.That(hex.Radius, Is.GreaterThanOrEqualTo(2));
            int n = hex.Radius;
            Assert.That(hex.Nodes.Count, Is.EqualTo(3 * n * (n + 1) + 1), "every lattice point exactly once");
            Assert.That(hex.Stats.Select(s => s.Id), Is.EquivalentTo(SkillHexContent.StatIds));
            Assert.That(hex.Families, Has.Count.EqualTo(6));
            Assert.That(hex.Heroes, Is.Not.Empty);
            Assert.That(ContentLoader.Validate(Real).Where(x => x.StartsWith("skillhex")), Is.Empty);
        }

        [Test]
        public void A_missing_board_is_null_and_skips_its_checks()
        {
            var db = ContentLoader.Load(name =>
            {
                var path = Path.Combine(ContentDir, name + ".json");
                return name == "skillhex" || !File.Exists(path) ? null : File.ReadAllText(path);
            });
            Assert.That(db.SkillHex, Is.Null);
            var p = new List<string>();
            ContentLoader.ValidateSkillHex(db, p);
            Assert.That(p, Is.Empty);
        }

        [Test]
        public void The_untouched_fixture_is_clean()
        {
            Assert.That(Problems(Board()), Is.Empty);
        }

        [Test]
        public void An_unknown_kind_does_not_load()
        {
            var b = Board();
            Node(b, "Open")["Kind"] = "Bogus";
            Assert.Throws<FormatException>(() => ContentLoader.ParseSkillHex(b.ToString()));
        }

        // ── lattice ──

        [Test]
        public void Every_lattice_point_must_be_listed()
        {
            var b = Board();
            Node(b, "Passive").Remove();
            Assert.That(Problems(b), Has.Some.Contains("lattice point(s) missing"));
        }

        [Test]
        public void A_lattice_point_is_listed_once()
        {
            var b = Board();
            Nodes(b).Add(Node(b, "Passive").DeepClone());
            Assert.That(Problems(b), Has.Some.Contains("listed 2 times"));
        }

        [Test]
        public void A_node_outside_the_radius_is_rejected()
        {
            var b = Board();
            int n = (int)b["Radius"];
            Nodes(b).Add(new JObject { ["Q"] = n + 1, ["R"] = 0, ["Kind"] = "Passive", ["Stats"] = new JObject { ["VIT"] = 1 } });
            Assert.That(Problems(b), Has.Some.Contains("outside the radius"));
        }

        // ── slots and passives ──

        [Test]
        public void An_ability_or_open_node_sits_only_on_a_slot()
        {
            var b = Board();
            var passive = Node(b, "Passive");
            passive["Kind"] = "Open";
            passive.Remove("Stats");
            Assert.That(Problems(b), Has.Some.Contains("Open on a passive point"));
        }

        [Test]
        public void A_passive_never_sits_on_a_slot()
        {
            var b = Board();
            var open = Node(b, "Open");
            open["Kind"] = "Passive";
            open["Stats"] = new JObject { ["VIT"] = 1 };
            Assert.That(Problems(b), Has.Some.Contains("Passive on an ability slot"));
        }

        [Test]
        public void A_passive_names_one_of_the_six_stats()
        {
            var b = Board();
            Node(b, "Passive")["Stats"] = new JObject { ["LUCK"] = 1 };
            Assert.That(Problems(b), Has.Some.Contains("passive stat 'LUCK' is not one of the six Stats"));
        }

        [Test]
        public void A_passive_needs_stats()
        {
            var b = Board();
            Node(b, "Passive").Remove("Stats");
            Assert.That(Problems(b), Has.Some.Contains("Passive without Stats"));
        }

        // ── abilities ──

        [Test]
        public void A_placed_ability_must_exist()
        {
            var b = Board();
            var open = Node(b, "Open");
            open["Kind"] = "Ability";
            open["Ability"] = "no_such_ability";
            Assert.That(Problems(b), Has.Some.Contains("ability 'no_such_ability' is not in abilities.json"));
        }

        [Test]
        public void An_alias_stub_cannot_be_placed()
        {
            var b = Board();
            var db = Db();
            var live = db.Abilities.Keys.OrderBy(k => k).First();
            db.AbilityAliases["skillhex_test_alias"] = live;
            var open = Node(b, "Open");
            open["Kind"] = "Ability";
            open["Ability"] = "skillhex_test_alias";
            Assert.That(Problems(b, db), Has.Some.Contains("'skillhex_test_alias' is an alias stub"));
        }

        [Test]
        public void An_ability_is_placed_at_most_once()
        {
            var b = Board();
            var placed = (string)Node(b, "Ability")["Ability"];
            var open = Node(b, "Open");
            open["Kind"] = "Ability";
            open["Ability"] = placed;
            Assert.That(Problems(b), Has.Some.Contains($"ability '{placed}' placed 2 times"));
        }

        // ── heroes ──

        [Test]
        public void A_hero_class_must_exist()
        {
            var b = Board();
            Placed(b)["Class"] = "no_such_class";
            Assert.That(Problems(b), Has.Some.Contains("class 'no_such_class' is not in classes.json"));
        }

        [Test]
        public void Only_a_new_hero_may_lack_a_class()
        {
            var b = Board();
            Placed(b)["Class"] = null;
            Assert.That(Problems(b), Has.Some.Contains("Class is null but Status is Placed"));
        }

        [Test]
        public void A_new_hero_without_a_class_is_fine()
        {
            var b = Board();
            Heroes(b).Add(new JObject
            {
                ["Class"] = null, ["Name"] = "Someone New", ["Recipe"] = new JObject { ["VIT"] = 4 }, ["Status"] = "New",
                ["Signature"] = null, ["Start"] = new JArray(),
            });
            Assert.That(Problems(b), Is.Empty);
        }

        [Test]
        public void A_recipe_sums_to_four()
        {
            var b = Board();
            Placed(b)["Recipe"] = new JObject { ["VIT"] = 3, ["STR"] = 2 };
            Assert.That(Problems(b), Has.Some.Contains("Recipe sums to 5, not 4"));
        }

        [Test]
        public void A_start_path_is_connected()
        {
            var b = Board();
            var hero = Placed(b);
            var start = (JArray)hero["Start"];
            var cells = new HashSet<(int, int)>(start.Select(c => ((int)c[0], (int)c[1])));
            // A lattice point not touching any cell of the path.
            var far = Nodes(b).Children<JObject>().Select(x => ((int)x["Q"], (int)x["R"]))
                .First(pt => !cells.Contains(pt) && !SkillHexDef.Directions.Any(d => cells.Contains((pt.Item1 + d.Q, pt.Item2 + d.R))));
            start.Add(new JArray(far.Item1, far.Item2));
            Assert.That(Problems(b), Has.Some.Contains("Start is not connected"));
        }

        [Test]
        public void A_start_path_holds_its_signature()
        {
            var b = Board();
            var hero = Placed(b);
            var sig = SignatureNode(b, hero);
            var start = (JArray)hero["Start"];
            foreach (var c in start.Where(c => (int)c[0] == (int)sig["Q"] && (int)c[1] == (int)sig["R"]).ToList()) c.Remove();
            Assert.That(Problems(b), Has.Some.Contains($"Start does not hold its Signature '{hero["Signature"]}'"));
        }

        [Test]
        public void A_start_path_stays_on_the_lattice()
        {
            var b = Board();
            int n = (int)b["Radius"];
            ((JArray)Placed(b)["Start"]).Add(new JArray(n + 5, 0));
            Assert.That(Problems(b), Has.Some.Contains("Start points outside the lattice"));
        }

        [Test]
        public void A_signature_must_be_placed()
        {
            var b = Board();
            var hero = Placed(b);
            var node = SignatureNode(b, hero);
            node["Kind"] = "Open";
            node.Remove("Ability");
            Assert.That(Problems(b), Has.Some.Contains($"Signature '{hero["Signature"]}' is not placed on the board"));
        }

        [Test]
        public void Signatures_are_unique()
        {
            var b = Board();
            var sig = (string)Placed(b, 0)["Signature"];
            Placed(b, 1)["Signature"] = sig;
            Assert.That(Problems(b), Has.Some.Contains($"Signature '{sig}' shared by"));
        }

        [Test]
        public void A_hero_class_is_listed_once()
        {
            var b = Board();
            Heroes(b).Add(Placed(b).DeepClone());
            Assert.That(Problems(b), Has.Some.Contains($"hero class '{Placed(b)["Class"]}' listed 2 times"));
        }

        // ── stats and families ──

        [Test]
        public void There_are_six_stats()
        {
            var b = Board();
            ((JArray)b["Stats"]).RemoveAt(5);
            Assert.That(Problems(b), Has.Some.Contains("Stats: expected 6, found 5"));
        }

        [Test]
        public void A_stat_is_one_of_the_six()
        {
            var b = Board();
            b["Stats"][0]["Id"] = "LUCK";
            Assert.That(Problems(b), Has.Some.Contains("Stats: unknown stat 'LUCK'"));
        }

        [Test]
        public void Families_run_corner_to_corner_in_order()
        {
            var b = Board();
            var fams = (JArray)b["Families"];
            var first = fams[0];
            first.Remove();
            fams.Add(first);
            Assert.That(Problems(b), Has.Some.Contains("corner order needs"));
        }

        [Test]
        public void Family_ids_are_unique()
        {
            var b = Board();
            b["Families"][1]["Id"] = (string)b["Families"][0]["Id"];
            Assert.That(Problems(b), Has.Some.Contains("Families: duplicate ids"));
        }
    }
}
