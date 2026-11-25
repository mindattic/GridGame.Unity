using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Content;
using GridGame.Core.Rules;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>GG-T1023: <see cref="SkillHexBoard"/>'s adjacency, frontier, cheapest route, SP cost and stat totals,
    /// on a small hand-built radius-2 board and on the real skillhex.json.</summary>
    public class SkillHexRuntimeTests
    {
        /// <summary>Radius 2: every slot an ability "a{Q}{R}", every passive +1 VIT (plus +1 STR where Q is positive).</summary>
        private static SkillHexDef Small(int passive = 1, int ability = 1, int passThrough = 4)
        {
            var def = new SkillHexDef
            {
                Version = 1, Radius = 2,
                Costs = new SkillHexCostsDef { Passive = passive, Ability = ability, PlannerPassThroughAbility = passThrough },
            };
            for (int q = -2; q <= 2; q++)
                for (int r = -2; r <= 2; r++)
                {
                    if (!SkillHexDef.OnLattice(q, r, 2)) continue;
                    var n = new SkillHexNodeDef { Q = q, R = r };
                    if (SkillHexDef.IsSlot(q, r)) { n.Kind = SkillHexNodeKind.Ability; n.Ability = $"a{q}{r}"; }
                    else
                    {
                        n.Kind = SkillHexNodeKind.Passive;
                        n.Stats = new Dictionary<string, int> { ["VIT"] = 1 };
                        if (q > 0) n.Stats["STR"] = 1;
                    }
                    def.Nodes.Add(n);
                }
            def.Heroes.Add(new SkillHexHeroDef { Class = "tester", Status = SkillHexHeroStatus.Placed, Start = { new[] { -2, 0 }, new[] { -1, 0 } } });
            return def;
        }

        private static HashSet<(int Q, int R)> Set(params (int, int)[] pts) => new HashSet<(int Q, int R)>(pts);

        [Test]
        public void Neighbors_stay_on_the_lattice()
        {
            var b = new SkillHexBoard(Small());
            Assert.That(b.Neighbors((0, 0)).Count(), Is.EqualTo(6));
            Assert.That(b.Neighbors((2, 0)), Is.EquivalentTo(new[] { (1, 1), (1, 0), (2, -1) }));
            Assert.That(SkillHexBoard.AreAdjacent((0, 0), (1, -1)), Is.True);
            Assert.That(SkillHexBoard.AreAdjacent((0, 0), (1, 1)), Is.False);
            Assert.That(SkillHexBoard.Distance((-2, 0), (2, 0)), Is.EqualTo(4));
            Assert.That(SkillHexBoard.Distance((-1, -1), (1, 1)), Is.EqualTo(4));
        }

        [Test]
        public void Frontier_is_the_takeable_nodes_next_to_owned()
        {
            var def = Small();
            def.Nodes.First(n => n.Q == 1 && n.R == -1).Kind = SkillHexNodeKind.Open;
            def.Nodes.First(n => n.Q == 1 && n.R == -1).Stats = null;
            var b = new SkillHexBoard(def);
            var owned = Set((0, 0), (1, 0));
            var f = b.Frontier(owned);
            Assert.That(f, Does.Not.Contain((0, 0)).And.Not.Contain((1, 0)), "owned nodes are not frontier");
            Assert.That(f, Does.Not.Contain((1, -1)), "an Open slot is never taken");
            Assert.That(f, Is.EquivalentTo(new[] { (0, 1), (-1, 1), (-1, 0), (0, -1), (2, 0), (1, 1), (2, -1) }));
            Assert.That(f, Is.Ordered, "sorted by (Q, R)");
            Assert.That(b.CanTake(owned, (2, 0)), Is.True);
            Assert.That(b.CanTake(owned, (-2, 0)), Is.False, "not next to an owned node");
            Assert.That(b.Frontier(Set()), Is.Empty);
        }

        [Test]
        public void Cheapest_route_goes_round_an_unwanted_ability()
        {
            var b = new SkillHexBoard(Small());
            var owned = Set((-2, 0));
            var route = b.CheapestRoute(owned, (2, 0));
            Assert.That(route, Is.Not.Null);
            Assert.That(route.Last(), Is.EqualTo((2, 0)));
            Assert.That(route, Does.Not.Contain((0, 0)), "the straight line passes the a00 ability (weight 4)");
            Assert.That(route.Count, Is.EqualTo(5), "one step longer, all passives");
            Assert.That(route.All(pt => b.Node(pt).Kind == SkillHexNodeKind.Passive), Is.True);
            Assert.That(b.SpCost(route), Is.EqualTo(5));
        }

        [Test]
        public void A_wanted_ability_is_walked_through()
        {
            var b = new SkillHexBoard(Small());
            var route = b.CheapestRoute(Set((-2, 0)), (2, 0), Set((0, 0)));
            Assert.That(route, Is.EqualTo(new List<(int, int)> { (-1, 0), (0, 0), (1, 0), (2, 0) }));
        }

        [Test]
        public void A_cheap_pass_through_takes_the_straight_line()
        {
            var b = new SkillHexBoard(Small(passThrough: 1));
            Assert.That(b.CheapestRoute(Set((-2, 0)), (2, 0)), Has.Count.EqualTo(4).And.Contains((0, 0)));
        }

        [Test]
        public void Route_to_an_owned_or_unreachable_node()
        {
            var def = Small();
            def.Nodes.First(n => n.Q == 1 && n.R == 1).Kind = SkillHexNodeKind.Open;
            var b = new SkillHexBoard(def);
            Assert.That(b.CheapestRoute(Set((0, 0)), (0, 0)), Is.Empty, "already owned");
            Assert.That(b.CheapestRoute(Set((0, 0)), (1, 1)), Is.Null, "an Open slot can't be reached");
            Assert.That(b.CheapestRoute(Set(), (0, 1)), Is.Null, "nothing owned to walk from");
            Assert.That(b.CheapestRoute(Set((0, 0)), (5, 5)), Is.Null, "off the lattice");
        }

        [Test]
        public void Every_route_step_touches_what_is_owned_so_far()
        {
            var b = new SkillHexBoard(Small());
            var owned = new SkillHexOwned(new[] { (-2, 1) });
            var route = b.CheapestRoute(owned.Nodes, (2, -1));
            Assert.That(owned.TakeRoute(b, route), Is.True);
            Assert.That(owned.Owns((2, -1)), Is.True);
            Assert.That(SkillHexContent.Unreached(new HashSet<(int, int)>(owned.Nodes)), Is.Empty, "still connected");
        }

        [Test]
        public void Sp_cost_prices_passives_and_abilities_and_skips_owned()
        {
            var b = new SkillHexBoard(Small(passive: 1, ability: 3));
            var route = new List<(int, int)> { (-1, 0), (0, 0), (1, 0) };
            Assert.That(b.SpCost(route), Is.EqualTo(5));
            Assert.That(b.SpCost(route, Set((-1, 0))), Is.EqualTo(4));
            Assert.That(b.Price((0, 0)), Is.EqualTo(3));
            Assert.That(b.Price((1, 0)), Is.EqualTo(1));
        }

        [Test]
        public void Stat_totals_add_up_owned_passives_only()
        {
            var b = new SkillHexBoard(Small());
            var totals = b.StatTotals(new[] { (-1, 0), (1, 0), (2, 0), (0, 0), (0, 0) });
            Assert.That(totals["VIT"], Is.EqualTo(3));
            Assert.That(totals["STR"], Is.EqualTo(2));
            Assert.That(totals.ContainsKey("INT"), Is.False);
            Assert.That(b.StatTotals(new (int, int)[0]), Is.Empty);
            Assert.That(b.OwnedAbilities(new[] { (1, 0), (0, 0) }), Is.EqualTo(new[] { "a00" }));
        }

        [Test]
        public void Take_needs_an_adjacent_takeable_node()
        {
            var b = new SkillHexBoard(Small());
            var owned = SkillHexOwned.Starting(b, "tester");
            Assert.That(owned.Nodes, Is.EquivalentTo(new[] { (-2, 0), (-1, 0) }));
            Assert.That(owned.Take(b, (2, 0)), Is.False, "not adjacent");
            Assert.That(owned.Take(b, (0, 0)), Is.True);
            Assert.That(owned.Take(b, (0, 0)), Is.False, "already owned");
            Assert.That(owned.Count, Is.EqualTo(3));
            Assert.That(new SkillHexOwned().Take(b, (2, 0)), Is.True, "the first node of an empty set");
        }

        [Test]
        public void Real_board_every_placed_hero_can_reach_every_ability()
        {
            var hex = Real.SkillHex;
            Assert.That(hex, Is.Not.Null);
            var b = new SkillHexBoard(hex);
            var abilities = hex.Nodes.Where(n => n.Kind == SkillHexNodeKind.Ability).Select(n => (n.Q, n.R)).ToList();
            foreach (var hero in hex.Heroes.Where(h => h.Status == SkillHexHeroStatus.Placed).Take(5))
            {
                var start = b.StartOf(hero.Class);
                Assert.That(start, Is.Not.Empty, hero.Class);
                Assert.That(b.Frontier(start), Is.Not.Empty, hero.Class);
                Assert.That(b.FindAbility(hero.Signature), Is.Not.Null);
                Assert.That(start, Does.Contain(b.FindAbility(hero.Signature).Value), $"{hero.Class} starts with its Signature");
                foreach (var target in abilities)
                {
                    var route = b.CheapestRoute(start, target);
                    Assert.That(route, Is.Not.Null, $"{hero.Class} -> {target}");
                    var owned = new SkillHexOwned(start);
                    Assert.That(owned.TakeRoute(b, route), Is.True, $"{hero.Class} -> {target} walks");
                }
            }
        }
    }
}
