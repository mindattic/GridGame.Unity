using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Content;
using GridGame.Core.Rules;
using GridGame.Meta;
using NUnit.Framework;

namespace GridGame.Tests
{
    /// <summary>GG-T1076: <see cref="SkillHexProgress"/>'s Join, Buy and BuyRoute on a small hand-built radius-2 board
    /// (passives cost 1 SP, abilities 2, so a price is told apart from a node count).</summary>
    public class SkillHexProgressTests
    {
        private static readonly (int Q, int R)[] StartPath = { (-2, 0), (-2, 1), (-1, 0) };

        private static string AbilityAt(int q, int r) => $"a{q}_{r}";

        /// <summary>Radius 2: every slot an ability "a{Q}_{R}", every other point a +1 VIT passive. The "tester" class
        /// starts on (-2,0), (-2,1) (the a-2_1 ability) and (-1,0).</summary>
        private static ContentDb Db(int passive = 1, int ability = 2)
        {
            var def = new SkillHexDef
            {
                Version = 1, Radius = 2,
                Costs = new SkillHexCostsDef { Passive = passive, Ability = ability, PlannerPassThroughAbility = 4 },
            };
            for (int q = -2; q <= 2; q++)
                for (int r = -2; r <= 2; r++)
                {
                    if (!SkillHexDef.OnLattice(q, r, 2)) continue;
                    var n = new SkillHexNodeDef { Q = q, R = r };
                    if (SkillHexDef.IsSlot(q, r)) { n.Kind = SkillHexNodeKind.Ability; n.Ability = AbilityAt(q, r); }
                    else { n.Kind = SkillHexNodeKind.Passive; n.Stats = new Dictionary<string, int> { ["VIT"] = 1 }; }
                    def.Nodes.Add(n);
                }
            var hero = new SkillHexHeroDef { Class = "tester", Status = SkillHexHeroStatus.Placed };
            foreach (var pt in StartPath) hero.Start.Add(new[] { pt.Q, pt.R });
            def.Heroes.Add(hero);
            return new ContentDb { SkillHex = def };
        }

        private static HeroSave Hero(int sp = 0, string classId = "tester") => new HeroSave { ClassId = classId, SkillPoints = sp };

        private static HeroSave Joined(ContentDb db, int sp)
        {
            var h = Hero(sp);
            Assert.That(SkillHexProgress.Join(db, h).Ok, Is.True);
            return h;
        }

        private static string[] Keys(IEnumerable<(int Q, int R)> pts) => pts.Select(SkillHexProgress.Key).ToArray();

        // ---- Join ----

        [Test]
        public void Join_gives_the_start_path_free_and_learns_its_abilities()
        {
            var db = Db();
            var h = Hero(sp: 3);
            var r = SkillHexProgress.Join(db, h);
            Assert.That(r.Ok, Is.True, r.Message);
            Assert.That(h.HexNodes, Is.EquivalentTo(Keys(StartPath)));
            Assert.That(h.SkillPoints, Is.EqualTo(3), "joining costs nothing");
            Assert.That(h.Known, Is.EquivalentTo(new[] { AbilityAt(-2, 1) }), "the start path's ability is learned, its passives teach nothing");
            Assert.That(SkillHexProgress.Owned(h).Count, Is.EqualTo(StartPath.Length));
        }

        [Test]
        public void Join_fails_when_already_on_the_board()
        {
            var db = Db();
            var h = Joined(db, sp: 2);
            var r = SkillHexProgress.Join(db, h);
            Assert.That(r.Ok, Is.False);
            Assert.That(h.HexNodes, Is.EquivalentTo(Keys(StartPath)), "nothing added");
            Assert.That(h.SkillPoints, Is.EqualTo(2));

            var other = Hero();
            other.HexNodes.Add("0,0");
            Assert.That(SkillHexProgress.Join(db, other).Ok, Is.False, "any owned node means already on the board");
            Assert.That(other.HexNodes, Is.EqualTo(new[] { "0,0" }));
            Assert.That(other.Known, Is.Empty);
        }

        [Test]
        public void Join_fails_without_a_start_path_or_a_board()
        {
            var stranger = Hero(classId: "nobody");
            Assert.That(SkillHexProgress.Join(Db(), stranger).Ok, Is.False);
            Assert.That(stranger.HexNodes, Is.Empty);

            var h = Hero();
            Assert.That(SkillHexProgress.Join(new ContentDb(), h).Ok, Is.False, "no skillhex content");
            Assert.That(h.HexNodes, Is.Empty);
        }

        // ---- Buy ----

        [Test]
        public void Buy_takes_an_adjacent_passive_for_its_price()
        {
            var db = Db();
            var h = Joined(db, sp: 5);
            var r = SkillHexProgress.Buy(db, h, (0, -1)); // next to (-1, 0); a passive, 1 SP
            Assert.That(r.Ok, Is.True, r.Message);
            Assert.That(h.SkillPoints, Is.EqualTo(4));
            Assert.That(h.HexNodes, Does.Contain("0,-1"));
            Assert.That(h.Known, Is.EquivalentTo(new[] { AbilityAt(-2, 1) }), "a passive teaches nothing");
        }

        [Test]
        public void Buy_an_ability_node_charges_the_ability_price_and_adds_it_to_Known()
        {
            var db = Db();
            var h = Joined(db, sp: 5);
            var r = SkillHexProgress.Buy(db, h, (0, 0)); // next to (-1, 0); an ability slot, 2 SP
            Assert.That(r.Ok, Is.True, r.Message);
            Assert.That(h.SkillPoints, Is.EqualTo(3));
            Assert.That(h.HexNodes, Does.Contain("0,0"));
            Assert.That(h.Known, Does.Contain(AbilityAt(0, 0)));
        }

        [Test]
        public void Buy_fails_for_a_node_not_next_to_one_owned()
        {
            var db = Db();
            var h = Joined(db, sp: 5);
            var r = SkillHexProgress.Buy(db, h, (2, 0));
            Assert.That(r.Ok, Is.False);
            Assert.That(h.SkillPoints, Is.EqualTo(5));
            Assert.That(h.HexNodes, Is.EquivalentTo(Keys(StartPath)));
        }

        [Test]
        public void Buy_fails_short_of_skill_points_and_changes_nothing()
        {
            var db = Db();
            var h = Joined(db, sp: 1);
            var r = SkillHexProgress.Buy(db, h, (0, 0)); // ability: 2 SP
            Assert.That(r.Ok, Is.False);
            Assert.That(h.SkillPoints, Is.EqualTo(1));
            Assert.That(h.HexNodes, Is.EquivalentTo(Keys(StartPath)));
            Assert.That(h.Known, Does.Not.Contain(AbilityAt(0, 0)));
        }

        [Test]
        public void Buy_fails_for_an_owned_node_or_before_joining()
        {
            var db = Db();
            var h = Joined(db, sp: 5);
            Assert.That(SkillHexProgress.Buy(db, h, (-1, 0)).Ok, Is.False, "already owned");
            Assert.That(h.SkillPoints, Is.EqualTo(5));

            var fresh = Hero(sp: 5);
            Assert.That(SkillHexProgress.Buy(db, fresh, (-2, 0)).Ok, Is.False, "not on the board yet");
            Assert.That(fresh.SkillPoints, Is.EqualTo(5));
            Assert.That(fresh.HexNodes, Is.Empty);
        }

        // ---- BuyRoute ----

        [Test]
        public void BuyRoute_charges_the_whole_route_SpCost()
        {
            var db = Db();
            var board = SkillHexProgress.Board(db);
            var target = (2, 0);
            var probe = Joined(db, sp: 0);
            var route = board.CheapestRoute(SkillHexProgress.Owned(probe).Nodes, target);
            Assert.That(route, Is.Not.Null.And.Not.Empty);
            int cost = board.SpCost(route, SkillHexProgress.Owned(probe).Nodes);
            Assert.That(cost, Is.GreaterThan(0));

            var h = Joined(db, sp: cost + 1);
            var r = SkillHexProgress.BuyRoute(db, h, target);
            Assert.That(r.Ok, Is.True, r.Message);
            Assert.That(h.SkillPoints, Is.EqualTo(1), "exactly SpCost spent");
            Assert.That(h.HexNodes, Is.EquivalentTo(Keys(StartPath).Concat(Keys(route))));
            var abilities = route.Select(pt => board.Node(pt)).Where(n => n.Kind == SkillHexNodeKind.Ability).Select(n => n.Ability);
            Assert.That(h.Known, Is.SupersetOf(abilities), "abilities on the route are learned");
        }

        [Test]
        public void BuyRoute_short_of_the_whole_cost_changes_nothing()
        {
            var db = Db();
            var board = SkillHexProgress.Board(db);
            var target = (2, 0);
            var h = Joined(db, sp: 0);
            int cost = board.SpCost(board.CheapestRoute(SkillHexProgress.Owned(h).Nodes, target), SkillHexProgress.Owned(h).Nodes);
            Assert.That(cost, Is.GreaterThan(1), "the route's first step alone would be affordable");
            h.SkillPoints = cost - 1;
            var knownBefore = h.Known.ToList();

            var r = SkillHexProgress.BuyRoute(db, h, target);
            Assert.That(r.Ok, Is.False);
            Assert.That(h.SkillPoints, Is.EqualTo(cost - 1));
            Assert.That(h.HexNodes, Is.EquivalentTo(Keys(StartPath)), "no partial route");
            Assert.That(h.Known, Is.EqualTo(knownBefore));
        }

        [Test]
        public void BuyRoute_fails_for_an_owned_or_unreachable_target()
        {
            var db = Db();
            var h = Joined(db, sp: 10);
            Assert.That(SkillHexProgress.BuyRoute(db, h, (-1, 0)).Ok, Is.False, "already owned");
            Assert.That(SkillHexProgress.BuyRoute(db, h, (5, 5)).Ok, Is.False, "off the board");
            Assert.That(h.SkillPoints, Is.EqualTo(10));
            Assert.That(h.HexNodes, Is.EquivalentTo(Keys(StartPath)));
        }
    }
}
