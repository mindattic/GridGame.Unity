using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Content;
using GridGame.Core.Rules;
using GridGame.Meta;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>GG-T1033 (docs/design/skill-hexagon/PLAN.md, Saves): a save from before the skill hexagon stands each hero
    /// on the board when it loads: its class Start path plus the cheapest route to every ability it knows, free; spent
    /// talent points come back as SP; cut classes convert first (GG-T1064) and aliases resolve first (GG-T973).</summary>
    public class HexBoardMigrationTests
    {
        private static SkillHexBoard Board => HeroBoard.Board(Real);

        /// <summary>A save as a v17 build wrote it: serialized, Version set back to 17.</summary>
        private static SaveData V17(SaveData s)
        {
            var json = JObject.Parse(SaveFiles.Serialize(s));
            json[nameof(SaveData.Version)] = 17;
            foreach (var h in (JArray)json[nameof(SaveData.Roster)])
                ((JObject)h).Remove(nameof(HeroSave.HexNodes));
            return SaveFiles.Deserialize(json.ToString());
        }

        private static HashSet<(int Q, int R)> Owned(HeroSave h) => new HashSet<(int Q, int R)>(SkillHexProgress.Owned(h).Nodes);

        /// <summary>Every owned node is reachable from the Start path through owned nodes.</summary>
        private static void AssertConnected(HeroSave h)
        {
            var owned = Owned(h);
            var start = Board.StartOf(h.ClassId);
            var seen = new HashSet<(int Q, int R)>(start.Where(owned.Contains));
            var queue = new Queue<(int Q, int R)>(seen);
            while (queue.Count > 0)
                foreach (var nb in Board.Neighbors(queue.Dequeue()))
                    if (owned.Contains(nb) && seen.Add(nb)) queue.Enqueue(nb);
            Assert.That(seen, Is.EquivalentTo(owned), $"{h.ClassId}: every owned node joins the Start path");
        }

        /// <summary>A hero class on the board with an ability node off its Start path.</summary>
        private static (string ClassId, string Ability) HeroWithFarAbility()
        {
            foreach (var hero in Real.SkillHex.Heroes.OrderBy(x => x.Class))
            {
                var def = Real.TryClass(hero.Class);
                if (def == null || def.IsCut || def.Team != Team.Hero) continue;
                var start = Board.StartOf(hero.Class);
                if (start.Count == 0) continue;
                var far = Real.SkillHex.Nodes
                    .Where(n => n.Kind == SkillHexNodeKind.Ability && !string.IsNullOrEmpty(n.Ability) && Real.TryAbility(n.Ability) != null)
                    .Where(n => !start.Contains((n.Q, n.R)))
                    .OrderBy(n => SkillHexBoard.Distance((n.Q, n.R), start.First()))
                    .FirstOrDefault();
                if (far != null) return (hero.Class, far.Ability);
            }
            Assert.Fail("no hero on the board with an ability off its Start path");
            return default;
        }

        [Test]
        public void A_v17_hero_gets_its_start_path_and_a_route_to_every_known_ability()
        {
            var (cls, ability) = HeroWithFarAbility();
            var s = Progression.NewGame(Real, "Hex-v17");
            var h = s.Hero(cls) ?? Progression.NewHero(Real, cls);
            if (s.Hero(cls) == null) s.Roster.Add(h);
            if (!h.Known.Contains(ability)) h.Known.Add(ability);

            var back = SaveMigrations.Migrate(V17(s), Real);
            Assert.That(back.Version, Is.EqualTo(SaveData.CurrentVersion));
            var m = back.Hero(cls);
            var owned = Owned(m);
            Assert.That(Board.StartOf(cls).All(owned.Contains), Is.True, "the Start path is owned");
            foreach (var id in m.Known)
            {
                var at = Board.FindAbility(id);
                if (at.HasValue) Assert.That(owned.Contains(at.Value), Is.True, $"{cls}: known {id} is owned on the board");
            }
            Assert.That(owned.Contains(Board.FindAbility(ability).Value), Is.True);
            foreach (var pt in owned)
            {
                var n = Board.Node(pt);
                if (n?.Kind == SkillHexNodeKind.Ability) Assert.That(m.Known, Does.Contain(n.Ability), "an owned ability node is known");
            }
            AssertConnected(m);
            Assert.That(m.HexNodes, Is.Unique);
        }

        [Test]
        public void Spent_talent_points_come_back_as_skill_points_and_the_tree_is_cleared()
        {
            var (cls, _) = HeroWithFarAbility();
            var tree = Real.SkillTrees.Values.OrderBy(t => t.Id).First(t => t.Nodes.Count >= 2);
            var s = Progression.NewGame(Real, "Hex-talents");
            var h = s.Hero(cls) ?? Progression.NewHero(Real, cls);
            if (s.Hero(cls) == null) s.Roster.Add(h);
            int sp = h.SkillPoints;
            string paidKey = Talents.Key(tree, tree.Nodes[0]), oldKey = Talents.Key(tree, tree.Nodes[1]);
            h.TreeNodes = new List<string> { paidKey, oldKey };
            h.TreeNodeCosts = new Dictionary<string, int> { [paidKey] = 7 };
            int expected = 7 + Talents.Cost(Real, h, tree, tree.Nodes[1]);

            var back = SaveMigrations.Migrate(V17(s), Real);
            var m = back.Hero(cls);
            Assert.That(m.TreeNodes, Is.Empty);
            Assert.That(m.TreeNodeCosts ?? new Dictionary<string, int>(), Is.Empty);
            Assert.That(m.SkillPoints, Is.EqualTo(sp + expected), "what was paid comes back; an unrecorded node refunds its current cost");
        }

        [Test]
        public void The_board_step_runs_once()
        {
            var (cls, _) = HeroWithFarAbility();
            var s = Progression.NewGame(Real, "Hex-once");
            var h = s.Hero(cls) ?? Progression.NewHero(Real, cls);
            if (s.Hero(cls) == null) s.Roster.Add(h);
            var back = SaveMigrations.Migrate(V17(s), Real);
            var m = back.Hero(cls);
            int sp = m.SkillPoints;
            var nodes = m.HexNodes.ToList();
            var again = SaveMigrations.Migrate(SaveFiles.Deserialize(SaveFiles.Serialize(back)), Real).Hero(cls);
            Assert.That(again.HexNodes, Is.EqualTo(nodes));
            Assert.That(again.SkillPoints, Is.EqualTo(sp));
        }

        [Test]
        public void A_v17_cut_hero_converts_then_stands_on_its_new_heros_board()
        {
            var cut = Real.Classes.Values.Where(d => d.IsCut).OrderBy(d => d.Id).First();
            var s = Progression.NewGame(Real, "Hex-cut");
            var h = Progression.NewHero(Real, cut.Id);
            h.TotalXp = 3000;
            s.Roster.Add(h);

            var back = SaveMigrations.Migrate(V17(s), Real);
            Assert.That(back.Hero(cut.Id), Is.Null);
            var m = back.Hero(cut.CutInto);
            Assert.That(m, Is.Not.Null);
            var start = Board.StartOf(m.ClassId);
            Assume.That(start, Is.Not.Empty, $"{m.ClassId} has a Start path");
            Assert.That(start.All(Owned(m).Contains), Is.True);
            AssertConnected(m);
        }

        [Test]
        public void A_v17_merged_ability_resolves_and_its_survivor_is_routed()
        {
            var alias = Real.AbilityAliases.OrderBy(kv => kv.Key).FirstOrDefault(kv => Board.FindAbility(kv.Value).HasValue);
            Assume.That(alias.Key, Is.Not.Null, "the content has a merged ability whose survivor is on the board");
            var (cls, _) = HeroWithFarAbility();
            var s = Progression.NewGame(Real, "Hex-alias");
            var h = s.Hero(cls) ?? Progression.NewHero(Real, cls);
            if (s.Hero(cls) == null) s.Roster.Add(h);
            h.Known.Add(alias.Key);

            var m = SaveMigrations.Migrate(V17(s), Real).Hero(cls);
            Assert.That(m.Known, Does.Not.Contain(alias.Key));
            Assert.That(m.Known, Does.Contain(alias.Value));
            Assert.That(Owned(m).Contains(Board.FindAbility(alias.Value).Value), Is.True);
            AssertConnected(m);
        }

        [Test]
        public void A_hero_off_the_board_keeps_its_refund_and_gets_no_nodes()
        {
            var s = new SaveData();
            var tree = Real.SkillTrees.Values.OrderBy(t => t.Id).First(t => t.Nodes.Count >= 1);
            var h = new HeroSave { ClassId = "not_a_board_class", SkillPoints = 2 };
            string key = Talents.Key(tree, tree.Nodes[0]);
            h.TreeNodes = new List<string> { key };
            h.TreeNodeCosts = new Dictionary<string, int> { [key] = 4 };
            s.Roster.Add(h);
            SaveMigrations.PlaceOnBoard(s, Real);
            Assert.That(h.SkillPoints, Is.EqualTo(6));
            Assert.That(h.TreeNodes, Is.Empty);
            Assert.That(h.HexNodes, Is.Empty);
        }
    }
}
