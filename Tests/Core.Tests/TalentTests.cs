using System.Linq;
using GridGame.Core.Content;
using GridGame.Meta;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>Spending skill points on tree nodes and the Inn's Respec (§25.6.3, GG-T53).</summary>
    public class TalentTests
    {
        private static (ContentDb c, SaveData s, HeroSave h, SkillTreeDef t) Setup()
        {
            var c = Real;
            var s = Progression.NewGame(c, "Talents");
            CampNpcs.JoinAll(s);
            var h = s.Hero(s.Party[0]);
            return (c, s, h, Talents.HomeTree(c, h));
        }

        [Test]
        public void Home_tree_comes_first_and_starting_kit_nodes_are_innate()
        {
            var (c, _, h, t) = Setup();
            Assert.That(Talents.Trees(c, h).First(), Is.SameAs(t));
            foreach (var n in t.Nodes.Where(n => h.Known.Contains(n.Grants)))
                Assert.That(Talents.State(h, t, n), Is.EqualTo(TalentState.Innate), n.Id);
            Assert.That(Talents.PointsSpent(c, h), Is.Zero);
        }

        [Test]
        public void Unlock_spends_points_grants_the_ability_and_respects_gates()
        {
            var (c, s, h, t) = Setup();
            var open = t.Nodes.First(n => Talents.State(h, t, n) == TalentState.Available);
            h.SkillPoints = 0;
            Assert.That(Talents.Unlock(c, s, h, t.Id, open.Id).Ok, Is.False, "no points");
            h.SkillPoints = 50;
            Assert.That(Talents.Unlock(c, s, h, t.Id, open.Id).Ok, Is.True);
            Assert.That(h.SkillPoints, Is.EqualTo(50 - open.Cost));
            Assert.That(h.Known, Does.Contain(open.Grants));
            Assert.That(Talents.State(h, t, open), Is.EqualTo(TalentState.Purchased));
            Assert.That(Talents.Unlock(c, s, h, t.Id, open.Id).Ok, Is.False, "twice");

            var locked = t.Nodes.FirstOrDefault(n => Talents.State(h, t, n) == TalentState.Locked);
            if (locked != null)
            {
                int before = h.SkillPoints;
                Assert.That(Talents.Unlock(c, s, h, t.Id, locked.Id).Ok, Is.False);
                Assert.That(h.SkillPoints, Is.EqualTo(before));
            }
        }

        [Test]
        public void Respec_refunds_bought_points_for_gold_that_scales_with_level_and_keeps_the_starting_kit()
        {
            var (c, s, h, t) = Setup();
            var kit = h.Known.ToList();
            h.SkillPoints = 50;
            var open = t.Nodes.First(n => Talents.State(h, t, n) == TalentState.Available);
            Assert.That(Talents.Unlock(c, s, h, t.Id, open.Id).Ok, Is.True);
            h.Bar[0] = open.Grants;

            s.Gold = 0;
            Assert.That(Talents.Respec(c, s, h).Ok, Is.False, "can't afford");
            int cost = Talents.RespecCost(h);
            s.Gold = cost;
            Assert.That(Talents.Respec(c, s, h).Ok, Is.True);
            Assert.That(s.Gold, Is.Zero);
            Assert.That(h.SkillPoints, Is.EqualTo(50));
            Assert.That(h.TreeNodes, Is.Empty);
            Assert.That(h.Known, Is.EquivalentTo(kit));
            Assert.That(h.Bar[0], Is.Null);
            Assert.That(Talents.Respec(c, s, h).Ok, Is.False, "nothing to refund");

            int low = Talents.RespecCost(h);
            h.TotalXp += 100000;
            Assert.That(Talents.RespecCost(h), Is.GreaterThan(low));
        }

        // ── Cross-tree specialization (§25.6.2, GG-T51) ─────────────────────────────────────────────

        /// <summary>Off-trees the hero has no node in at all (not even a Known one), nearest on the adjacency ring first
        /// (GG-T135), then by name — so the GG-T51 tests below open the home tree's neighbours, which pay the flat premium.</summary>
        private static SkillTreeDef[] Untouched(ContentDb c, HeroSave h, SkillTreeDef home) =>
            c.SkillTrees.Values.Where(t => t != home && !t.Nodes.Any(n => Talents.Unlocked(h, t, n)))
                .OrderBy(t => TreeAdjacency.Distance(home.Id, t.Id)).ThenBy(t => t.Name).ToArray();

        /// <summary>Buys home-tree nodes the way the camp routine would until the home tree holds <paramref name="target"/> points.</summary>
        private static void InvestHome(ContentDb c, SaveData s, HeroSave h, SkillTreeDef home, int target)
        {
            for (int guard = 0; guard < 100 && Talents.PointsIn(h, home) < target; guard++)
            {
                var next = CampRoutine.NextTalent(c, h, home);
                Assert.That(next, Is.Not.Null, $"home tree ran out of nodes below {target} pts");
                Assert.That(Talents.Unlock(c, s, h, home.Id, next.Id).Ok, Is.True, next.Id);
            }
            Assert.That(Talents.PointsIn(h, home), Is.GreaterThanOrEqualTo(target));
        }

        private static SkillNodeDef StartNode(SkillTreeDef t) =>
            t.Nodes.First(n => n.RequiredPointsInTree == 0 && n.Prerequisites.Count == 0);

        [Test]
        public void A_second_tree_opens_only_once_the_home_tree_has_enough_points()
        {
            var (c, s, h, home) = Setup();
            h.SkillPoints = 500;
            var off = Untouched(c, h, home);
            Assume.That(off.Length, Is.GreaterThanOrEqualTo(1));
            var tree = off[0];
            var node = StartNode(tree);
            int threshold = TalentConfig.OpenTreeAt[0];

            if (Talents.PointsIn(h, home) < threshold)
            {
                Assert.That(Talents.OffTreeSlots(c, h), Is.Zero);
                Assert.That(Talents.Trees(c, h), Does.Not.Contain(tree), "a closed tree isn't offered");
                Assert.That(Talents.State(c, h, tree, node), Is.EqualTo(TalentState.Locked));
                Assert.That(Talents.WhyLocked(c, h, tree, node), Does.Contain($"{threshold} pts in {home.Name}"));
                int before = h.SkillPoints;
                Assert.That(Talents.Unlock(c, s, h, tree.Id, node.Id).Ok, Is.False);
                Assert.That(h.SkillPoints, Is.EqualTo(before), "a refused buy costs nothing");
                Assert.That(h.TreeNodes, Has.None.StartsWith(tree.Id + "/"));
            }

            InvestHome(c, s, h, home, threshold);
            Assert.That(Talents.OffTreeSlots(c, h), Is.EqualTo(1));
            Assert.That(Talents.Trees(c, h), Does.Contain(tree), "a free slot offers every other tree");
            Assert.That(Talents.State(c, h, tree, node), Is.EqualTo(TalentState.Available));
            Assert.That(Talents.Opened(c, h, tree), Is.False);
            Assert.That(Talents.Unlock(c, s, h, tree.Id, node.Id).Ok, Is.True);
            Assert.That(Talents.Opened(c, h, tree), Is.True, "the first node bought opens the tree");
            Assert.That(Talents.OpenedOffTrees(c, h), Is.EqualTo(new[] { tree }));
        }

        [Test]
        public void Off_tree_nodes_cost_the_premium_and_home_nodes_do_not()
        {
            var (c, s, h, home) = Setup();
            h.SkillPoints = 500;
            var tree = Untouched(c, h, home).First();
            foreach (var n in home.Nodes) Assert.That(Talents.Cost(c, h, home, n), Is.EqualTo(n.Cost), n.Id);
            foreach (var n in tree.Nodes) Assert.That(Talents.Cost(c, h, tree, n), Is.EqualTo(n.Cost + TalentConfig.OffTreePremium), n.Id);

            InvestHome(c, s, h, home, TalentConfig.OpenTreeAt[0]);
            var node = StartNode(tree);
            int before = h.SkillPoints;
            Assert.That(Talents.Unlock(c, s, h, tree.Id, node.Id).Ok, Is.True);
            Assert.That(h.SkillPoints, Is.EqualTo(before - node.Cost - TalentConfig.OffTreePremium));
            Assert.That(Talents.PointsIn(h, tree), Is.EqualTo(node.Cost), "the premium is a price, not points in the tree");

            var another = tree.Nodes.First(n => n != node && Talents.State(c, h, tree, n) == TalentState.Available);
            h.SkillPoints = another.Cost; // enough at the home price, one short at the off-tree price
            var r = Talents.Unlock(c, s, h, tree.Id, another.Id);
            Assert.That(r.Ok, Is.False);
            Assert.That(r.Message, Is.EqualTo("Not enough skill points"));
            Assert.That(h.SkillPoints, Is.EqualTo(another.Cost));
        }

        [Test]
        public void Deeper_off_tree_tiers_need_more_points_in_that_tree()
        {
            var (c, s, h, home) = Setup();
            h.SkillPoints = 500;
            var tree = Untouched(c, h, home).First();
            foreach (var n in home.Nodes)
                Assert.That(Talents.Required(c, h, home, n), Is.EqualTo(n.RequiredPointsInTree), n.Id);
            foreach (var n in tree.Nodes)
            {
                int scaled = Talents.Required(c, h, tree, n);
                if (n.RequiredPointsInTree == 0) Assert.That(scaled, Is.Zero, n.Id);
                else Assert.That(scaled, Is.GreaterThan(n.RequiredPointsInTree), n.Id);
            }

            InvestHome(c, s, h, home, TalentConfig.OpenTreeAt[0]);
            // The shallowest gated node with no prerequisites (a branch Opener): its home-tree gate is met before its
            // off-tree gate is, so there is a stretch where it would be open at home but is still locked here.
            var opener = tree.Nodes.Where(n => n.RequiredPointsInTree > 0 && n.Prerequisites.Count == 0)
                .OrderBy(n => n.RequiredPointsInTree).First();
            int need = Talents.Required(c, h, tree, opener);
            bool sawScaledGate = false;
            foreach (var start in tree.Nodes.Where(n => n.RequiredPointsInTree == 0 && n.Prerequisites.Count == 0))
            {
                Assert.That(Talents.Unlock(c, s, h, tree.Id, start.Id).Ok, Is.True, start.Id);
                int have = Talents.PointsIn(h, tree);
                if (have >= opener.RequiredPointsInTree && have < need)
                {
                    sawScaledGate = true;
                    Assert.That(Talents.State(h, tree, opener), Is.EqualTo(TalentState.Available), "met by the tree's own gate");
                    Assert.That(Talents.State(c, h, tree, opener), Is.EqualTo(TalentState.Locked), "but not by the off-tree gate");
                    Assert.That(Talents.WhyLocked(c, h, tree, opener), Does.Contain($"Needs {need} pts in {tree.Name}"));
                }
                if (have >= need) break;
            }
            Assert.That(sawScaledGate, Is.True, "the start tier should land between the home and off-tree gates");
            Assert.That(Talents.PointsIn(h, tree), Is.GreaterThanOrEqualTo(need));
            Assert.That(Talents.State(c, h, tree, opener), Is.EqualTo(TalentState.Available));
        }

        [Test]
        public void A_third_tree_needs_the_next_threshold_and_there_is_no_fourth()
        {
            var (c, s, h, home) = Setup();
            h.SkillPoints = 1000;
            var off = Untouched(c, h, home);
            Assume.That(off.Length, Is.GreaterThanOrEqualTo(3));
            Assume.That(TalentConfig.OpenTreeAt.Length, Is.EqualTo(2));

            InvestHome(c, s, h, home, TalentConfig.OpenTreeAt[0]);
            Assert.That(Talents.Unlock(c, s, h, off[0].Id, StartNode(off[0]).Id).Ok, Is.True);
            if (Talents.PointsIn(h, home) < TalentConfig.OpenTreeAt[1])
            {
                Assert.That(Talents.State(c, h, off[1], StartNode(off[1])), Is.EqualTo(TalentState.Locked), "one off-tree per slot");
                Assert.That(Talents.WhyClosed(c, h, off[1]), Does.Contain("third tree"));
                Assert.That(Talents.Trees(c, h), Does.Not.Contain(off[1]));
                Assert.That(Talents.State(c, h, off[0], StartNode(off[0])), Is.EqualTo(TalentState.Purchased), "the opened tree stays open");
            }

            InvestHome(c, s, h, home, TalentConfig.OpenTreeAt[1]);
            Assert.That(Talents.OffTreeSlots(c, h), Is.EqualTo(2));
            Assert.That(Talents.Unlock(c, s, h, off[1].Id, StartNode(off[1]).Id).Ok, Is.True);
            Assert.That(Talents.OpenedOffTrees(c, h).Count, Is.EqualTo(2));

            var fourth = Talents.Unlock(c, s, h, off[2].Id, StartNode(off[2]).Id);
            Assert.That(fourth.Ok, Is.False, "two off-trees is the cap");
            Assert.That(fourth.Message, Does.Contain("Already specialized in 2 other trees"));
            Assert.That(Talents.Trees(c, h), Does.Contain(off[0]).And.Contain(off[1]).And.Not.Contain(off[2]));
        }

        [Test]
        public void Respec_refunds_the_off_tree_premium_and_closes_the_off_tree()
        {
            var (c, s, h, home) = Setup();
            h.SkillPoints = 500;
            var tree = Untouched(c, h, home).First();
            InvestHome(c, s, h, home, TalentConfig.OpenTreeAt[0]);
            Assert.That(Talents.Unlock(c, s, h, tree.Id, StartNode(tree).Id).Ok, Is.True);
            Assert.That(h.SkillPoints + Talents.PointsSpent(c, h), Is.EqualTo(500), "spent + banked = earned, premium included");

            s.Gold = Talents.RespecCost(h);
            Assert.That(Talents.Respec(c, s, h).Ok, Is.True);
            Assert.That(h.SkillPoints, Is.EqualTo(500));
            Assert.That(Talents.Opened(c, h, tree), Is.False);
            Assert.That(Talents.OpenedOffTrees(c, h), Is.Empty);
        }

        // ── Archetype adjacency (§25.6.2.2, GG-T135) ────────────────────────────────────────────────

        [Test]
        public void Every_skill_tree_sits_once_on_the_adjacency_ring()
        {
            var c = Real;
            var ring = TreeAdjacency.Ring;
            Assert.That(ring.Distinct().Count(), Is.EqualTo(ring.Length), "no tree twice");
            Assert.That(ring, Is.EquivalentTo(c.SkillTrees.Values.Select(t => t.Id)), "the ring is exactly the content's trees");
        }

        [Test]
        public void Ring_distance_routes_a_warrior_to_mage_through_guardian()
        {
            Assert.That(TreeAdjacency.Adjacent("warrior", "guardian"), Is.True);
            Assert.That(TreeAdjacency.Adjacent("guardian", "cleric"), Is.True, "the ring wraps");
            Assert.That(TreeAdjacency.Distance("warrior", "warrior"), Is.Zero);
            Assert.That(TreeAdjacency.Distance("warrior", "mage"), Is.EqualTo(3), "Warrior - Guardian - Cleric - Mage");
            Assert.That(TreeAdjacency.DistanceFrom(new[] { "warrior", "guardian" }, "mage"), Is.EqualTo(2), "nearest held tree counts");
            Assert.That(TreeAdjacency.DistanceFrom(new[] { "warrior", "guardian", "cleric" }, "mage"), Is.EqualTo(1));
            Assert.That(TreeAdjacency.Distance("warrior", "nowhere"), Is.EqualTo(TreeAdjacency.Unreachable));

            Assert.That(TreeAdjacency.ExtraPremium(1), Is.Zero, "a neighbour pays only GG-T51's flat premium");
            Assert.That(TreeAdjacency.ExtraPremium(2), Is.EqualTo(TreeAdjacency.PremiumPerExtraStep));

            Assert.That(TreeAdjacency.WhyTooFar(new[] { "warrior" }, "mage"), Does.Contain("open guardian first"));
            Assert.That(TreeAdjacency.Stepping(new[] { "warrior" }, "mage"), Is.EqualTo(new[] { "guardian" }));
            Assert.That(TreeAdjacency.WhyTooFar(new[] { "warrior", "guardian" }, "mage"), Is.Null);
        }

        [Test]
        public void A_distant_tree_must_be_routed_to_and_costs_more_per_step()
        {
            var (c, s, h, home) = Setup();
            h.SkillPoints = 2000;
            Assume.That(TalentConfig.OpenTreeAt.Length, Is.GreaterThanOrEqualTo(2));
            Assume.That(TreeAdjacency.MaxOpenDistance, Is.EqualTo(2));
            var untouched = Untouched(c, h, home);
            var far = untouched.FirstOrDefault(t => TreeAdjacency.Distance(home.Id, t.Id) == 3);
            Assume.That(far, Is.Not.Null, "the 7-ring has trees 3 steps from every home");
            var via = untouched.First(t => TreeAdjacency.Adjacent(home.Id, t.Id) && TreeAdjacency.Distance(t.Id, far.Id) == 2);

            InvestHome(c, s, h, home, TalentConfig.OpenTreeAt[0]);
            var farStart = StartNode(far);
            Assert.That(Talents.State(c, h, far, farStart), Is.EqualTo(TalentState.Locked), "3 steps: too far to open directly");
            Assert.That(Talents.WhyClosed(c, h, far), Does.Contain("3 steps away").And.Contain(via.Name));
            int before = h.SkillPoints;
            Assert.That(Talents.Unlock(c, s, h, far.Id, farStart.Id).Ok, Is.False);
            Assert.That(h.SkillPoints, Is.EqualTo(before));

            Assert.That(Talents.Premium(c, h, via), Is.EqualTo(TalentConfig.OffTreePremium), "a neighbour pays the flat premium");
            Assert.That(Talents.Unlock(c, s, h, via.Id, StartNode(via).Id).Ok, Is.True);

            InvestHome(c, s, h, home, TalentConfig.OpenTreeAt[1]);
            Assert.That(Talents.Distance(c, h, far), Is.EqualTo(2), "the stepping stone brought it closer");
            Assert.That(Talents.WhyClosed(c, h, far), Is.Null);
            int premium = Talents.Premium(c, h, far);
            Assert.That(premium, Is.EqualTo(TalentConfig.OffTreePremium + TreeAdjacency.PremiumPerExtraStep), "two steps cost one more a node");
            before = h.SkillPoints;
            Assert.That(Talents.Unlock(c, s, h, far.Id, farStart.Id).Ok, Is.True);
            Assert.That(h.SkillPoints, Is.EqualTo(before - farStart.Cost - premium));
            Assert.That(h.SkillPoints + Talents.PointsSpent(c, h), Is.EqualTo(2000), "Respec would refund what was paid");
        }

        [Test]
        public void Respec_needs_the_Innkeeper()
        {
            var c = Real;
            var s = Progression.NewGame(c, "NoInn");
            var h = s.Hero(s.Party[0]);
            var t = Talents.HomeTree(c, h);
            h.SkillPoints = 50;
            var open = t.Nodes.First(n => Talents.State(h, t, n) == TalentState.Available);
            Assert.That(Talents.Unlock(c, s, h, t.Id, open.Id).Ok, Is.True);
            s.Gold = 100000;
            if (!CampUnlocks.NpcOpen(s, CampNpcs.Innkeeper))
                Assert.That(Talents.Respec(c, s, h).Ok, Is.False);
        }

        // ── Family trees (GG-T979, epic GG-T953) ─────────────────────────────────────────────────────

        /// <summary>Six families on a ring (listed out of ring order on purpose), each with its own tree, plus one role tree.
        /// The thief is a Shadow (Ring 4) whose HomeTree still names the role tree "rogue": its family tree is home anyway.
        /// Every family tree has a free root, a 4-point gate node and ten 2-point filler nodes to invest in.</summary>
        private static ContentDb FamilyDb()
        {
            var c = new ContentDb();
            var families = new[] { ("wild", 5), ("vanguard", 1), ("shadow", 4), ("blade", 2), ("machina", 6), ("brawn", 3) };
            foreach (var (id, ring) in families)
            {
                string name = char.ToUpperInvariant(id[0]) + id.Substring(1);
                c.Families.Add(new FamilyDef { Id = id, Name = name, Theme = "t", Primary = "Strength", Secondary = "Vitality", Ring = ring });
                var t = new SkillTreeDef { Id = id, Name = name };
                t.Nodes.Add(FamilyNode(c, id + "_root", 1, 0));
                t.Nodes.Add(FamilyNode(c, id + "_gate", 1, 4));
                for (int i = 0; i < 10; i++) t.Nodes.Add(FamilyNode(c, $"{id}_fill{i}", 2, 0));
                c.SkillTrees[id] = t;
            }
            c.SkillTrees["rogue"] = new SkillTreeDef { Id = "rogue", Name = "Rogue", Nodes = { FamilyNode(c, "rogue_root", 1, 0) } };
            c.Abilities["steal"] = new AbilityDef { Id = "steal", Name = "Steal", Kind = AbilityKind.Skill };
            var shadow = c.SkillTrees["shadow"];
            shadow.Nodes.Add(new SkillNodeDef { Id = "t_root", Name = "Quick Hands", Cost = 1, SignatureOf = "thief",
                GrantsStat = new StatGrantDef { Stat = "Agility", Amount = 1 } });
            shadow.Nodes.Add(new SkillNodeDef { Id = "t_steal", Name = "Steal", Cost = 1, Grants = "steal", SignatureOf = "thief", Prerequisites = { "t_root" } });
            c.Classes["thief"] = new ClassDef { Id = "thief", Name = "Thief", Team = Team.Hero, Role = "Striker", Family = "shadow",
                HomeTree = "rogue", DefaultLoadout = { "steal" }, StartNodes = { "t_root", "t_steal" } };
            return c;
        }

        private static SkillNodeDef FamilyNode(ContentDb c, string id, int cost, int required)
        {
            c.Abilities[id] = new AbilityDef { Id = id, Name = id, Kind = AbilityKind.Passive };
            return new SkillNodeDef { Id = id, Name = id, Cost = cost, Grants = id, RequiredPointsInTree = required };
        }

        private static (ContentDb c, SaveData s, HeroSave h, SkillTreeDef home) FamilySetup()
        {
            var c = FamilyDb();
            var s = new SaveData { TestMode = true, Gold = 100000 };
            CampNpcs.JoinAll(s);
            var h = Progression.NewHero(c, "thief");
            h.SkillPoints = 2000;
            return (c, s, h, c.SkillTrees["shadow"]);
        }

        /// <summary>Buys the home tree's open fillers until it counts <paramref name="target"/> points (start nodes included).</summary>
        private static void InvestFamily(ContentDb c, SaveData s, HeroSave h, SkillTreeDef home, int target)
        {
            for (int guard = 0; guard < 100 && Talents.PointsIn(c, h, home) < target; guard++)
            {
                var next = home.Nodes.First(n => n.Id.Contains("_fill") && Talents.State(c, h, home, n) == TalentState.Available);
                Assert.That(Talents.Unlock(c, s, h, home.Id, next.Id).Ok, Is.True, next.Id);
            }
            Assert.That(Talents.PointsIn(c, h, home), Is.GreaterThanOrEqualTo(target));
        }

        [Test]
        public void The_family_tree_is_home_even_when_HomeTree_names_a_role_tree()
        {
            var (c, _, h, shadow) = FamilySetup();
            var def = c.Class("thief");
            Assert.That(Progression.FamilyTree(c, def), Is.SameAs(shadow), "the tree named by Family, as ContentLoader.ValidateFamilyTrees reads it");
            Assert.That(Talents.HomeTree(c, h), Is.SameAs(shadow));
            Assert.That(Talents.IsHome(c, h, shadow), Is.True);
            Assert.That(Talents.IsHome(c, h, c.SkillTrees["rogue"]), Is.False, "the old role tree is an off-tree now");
            Assert.That(Talents.Trees(c, h).First(), Is.SameAs(shadow));
            Assert.That(h.Known, Does.Contain("steal"), "the start grant came from the family tree");
            Assert.That(Talents.State(c, h, shadow, shadow.Nodes.First(n => n.Id == "t_root")), Is.EqualTo(TalentState.Innate));
            Assert.That(Talents.PointsIn(c, h, shadow), Is.EqualTo(2), "both start nodes count");
            Assert.That(Talents.Premium(c, h, shadow), Is.Zero);
            Assert.That(Talents.Required(c, h, shadow, shadow.Nodes.First(n => n.Id == "shadow_gate")), Is.EqualTo(4), "home gates are unscaled");
        }

        [Test]
        public void The_family_board_follows_families_json_ring_order()
        {
            var c = FamilyDb();
            Assert.That(TreeAdjacency.FamilyRing(c), Is.EqualTo(new[] { "vanguard", "blade", "brawn", "shadow", "wild", "machina" }));
            Assert.That(TreeAdjacency.BoardFor(c, "shadow"), Is.EqualTo(TreeAdjacency.FamilyRing(c)));
            Assert.That(TreeAdjacency.BoardFor(c, "rogue"), Is.SameAs(TreeAdjacency.Ring), "a role home keeps the role ring");
            var board = TreeAdjacency.FamilyRing(c);
            Assert.That(TreeAdjacency.Adjacent("shadow", "brawn", board), Is.True);
            Assert.That(TreeAdjacency.Adjacent("machina", "vanguard", board), Is.True, "the ring wraps");
            Assert.That(TreeAdjacency.Distance("shadow", "vanguard", board), Is.EqualTo(3));
            Assert.That(TreeAdjacency.Distance("shadow", "rogue", board), Is.EqualTo(TreeAdjacency.Unreachable), "a role tree is off the family board");

            var real = Real;
            var ring = TreeAdjacency.FamilyRing(real);
            Assert.That(ring, Is.EqualTo(real.Families.OrderBy(f => f.Ring).Select(f => f.Id).ToArray()));
            Assert.That(ring.Distinct().Count(), Is.EqualTo(real.Families.Count), "every family once");
        }

        [Test]
        public void ExtraEdges_bridge_the_family_board_too()
        {
            var c = FamilyDb();
            var board = TreeAdjacency.FamilyRing(c);
            var saved = TreeAdjacency.ExtraEdges;
            try
            {
                TreeAdjacency.ExtraEdges = new System.Collections.Generic.List<(string A, string B)> { ("shadow", "vanguard") };
                Assert.That(TreeAdjacency.Distance("shadow", "vanguard", board), Is.EqualTo(1));
            }
            finally { TreeAdjacency.ExtraEdges = saved; }
        }

        [Test]
        public void Off_family_slots_premium_and_scaled_gates_work_as_off_trees_do()
        {
            var (c, s, h, shadow) = FamilySetup();
            Assume.That(TalentConfig.OpenTreeAt, Is.EqualTo(new[] { 10, 20 }));
            Assume.That(TreeAdjacency.MaxOpenDistance, Is.EqualTo(2));
            var brawn = c.SkillTrees["brawn"];       // neighbour (Ring 3)
            var machina = c.SkillTrees["machina"];   // 2 steps (Ring 6)
            var vanguard = c.SkillTrees["vanguard"]; // 3 steps (Ring 1)
            var rogue = c.SkillTrees["rogue"];       // the old role tree, off the family board

            Assert.That(Talents.OffTreeSlots(c, h), Is.Zero);
            Assert.That(Talents.WhyClosed(c, h, brawn), Does.Contain("needs 10 pts in Shadow (2 now)"), "start nodes count toward the slot");
            Assert.That(Talents.Unlock(c, s, h, "brawn", "brawn_root").Ok, Is.False);

            InvestFamily(c, s, h, shadow, TalentConfig.OpenTreeAt[0]);
            Assert.That(Talents.OffTreeSlots(c, h), Is.EqualTo(1));
            Assert.That(Talents.Distance(c, h, brawn), Is.EqualTo(1));
            Assert.That(Talents.Premium(c, h, brawn), Is.EqualTo(TalentConfig.OffTreePremium));
            Assert.That(Talents.Required(c, h, brawn, brawn.Nodes.First(n => n.Id == "brawn_gate")), Is.EqualTo(Talents.ScaledRequirement(4)));
            Assert.That(Talents.WhyClosed(c, h, vanguard), Does.Contain("Vanguard is 3 steps away").And.Contain("Brawn or Wild"));
            Assert.That(Talents.Distance(c, h, machina), Is.EqualTo(2));
            Assert.That(Talents.Premium(c, h, machina), Is.EqualTo(TalentConfig.OffTreePremium + TreeAdjacency.PremiumPerExtraStep));
            Assert.That(Talents.Distance(c, h, rogue), Is.EqualTo(TreeAdjacency.Unreachable));
            Assert.That(Talents.Premium(c, h, rogue), Is.EqualTo(TalentConfig.OffTreePremium), "off the board pays the flat premium only");

            int before = h.SkillPoints;
            Assert.That(Talents.Unlock(c, s, h, "brawn", "brawn_root").Ok, Is.True);
            Assert.That(h.SkillPoints, Is.EqualTo(before - 1 - TalentConfig.OffTreePremium));
            Assert.That(Talents.OpenedOffTrees(c, h), Is.EqualTo(new[] { brawn }));
            Assert.That(Talents.HeldTreeIds(c, h), Is.EqualTo(new[] { "shadow", "brawn" }));
            Assert.That(Talents.WhyClosed(c, h, machina), Does.Contain("needs 20 pts in Shadow"), "the second slot waits for 20");

            InvestFamily(c, s, h, shadow, TalentConfig.OpenTreeAt[1]);
            Assert.That(Talents.Distance(c, h, vanguard), Is.EqualTo(2), "Brawn is a stepping stone to Vanguard");
            Assert.That(Talents.WhyClosed(c, h, vanguard), Is.Null);
            Assert.That(Talents.Unlock(c, s, h, "vanguard", "vanguard_root").Ok, Is.True);
            Assert.That(Talents.WhyClosed(c, h, machina), Does.Contain("Already specialized in 2 other trees"));
            Assert.That(h.SkillPoints + Talents.PointsSpent(c, h), Is.EqualTo(2000));
        }

        [Test]
        public void Respec_on_a_family_tree_refunds_what_was_bought_and_never_the_start_nodes()
        {
            var (c, s, h, shadow) = FamilySetup();
            InvestFamily(c, s, h, shadow, TalentConfig.OpenTreeAt[0]);
            Assert.That(Talents.Unlock(c, s, h, "brawn", "brawn_root").Ok, Is.True);
            Assert.That(Talents.PointsSpent(c, h), Is.EqualTo(2000 - h.SkillPoints));

            Assert.That(Talents.Respec(c, s, h).Ok, Is.True);
            Assert.That(h.SkillPoints, Is.EqualTo(2000));
            Assert.That(Talents.PointsSpent(c, h), Is.Zero);
            Assert.That(h.Known, Does.Contain("steal"), "a start grant is never forgotten");
            Assert.That(h.Known, Does.Not.Contain("brawn_root"));
            Assert.That(Talents.PointsIn(c, h, shadow), Is.EqualTo(2), "the start nodes still count");
            foreach (var id in new[] { "t_root", "t_steal" })
                Assert.That(Talents.State(c, h, shadow, shadow.Nodes.First(n => n.Id == id)), Is.EqualTo(TalentState.Innate), id);
            Assert.That(Talents.OpenedOffTrees(c, h), Is.Empty);
        }
    }
}
