using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Bot;
using GridGame.Core.Content;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>GG-T932: the hero bots (smart and casual share ScoreAbility) price a Firewall by
    /// <see cref="HeroBot.WallValue"/>: each wall tile on a foe's way in, behind a foe a ready shove can push into it or
    /// under a foe is worth something; a hero on it costs 8 (the wall burns the party too); nothing qualifying is worth 0.</summary>
    public class FirewallBotTests
    {
        private static ContentDb Db()
        {
            var db = Mini();
            db.Elements = Real.Elements;
            db.Abilities["firewall"] = Real.TryAbility("firewall");
            RegionRuleCatalog.Index(db, new List<RegionRuleDef> { Real.TryRegionRule("firewalls") });
            db.Abilities["shove"] = new AbilityDef
            {
                Id = "shove", Name = "shove", Kind = AbilityKind.Skill, BaseDamage = 1, Knockback = 1, AdjacentOnly = true, DamageType = DamageType.Physical,
                Shape = TargetShape.SingleActor, Mode = TargetMode.PickActor, Filter = TargetFilter.EnemyOnly,
            };
            return db;
        }

        /// <summary>The knight carries the Firewall in slot 0 and <paramref name="tool"/> (if any) in slot 1.</summary>
        private static BattleState Setup(StageDef stage, (int x, int y) knight, (int x, int y) cleric, string tool = null)
        {
            var setup = new BattleSetup { Stage = stage, Seed = 7, UnlockedSlots = 5 };
            var k = new HeroSpec { ClassId = "knight", Start = new Coord(knight.x, knight.y) };
            k.Bar.Add("firewall");
            if (tool != null) k.Bar.Add(tool);
            setup.Party.Add(k);
            setup.Party.Add(new HeroSpec { ClassId = "cleric", Start = new Coord(cleric.x, cleric.y) });
            var s = BattleEngine.Create(Db(), setup, new List<BattleEvent>());
            GrantMana(s, 4);
            return s;
        }

        private static float Wall(BattleState s, int x, int y) =>
            HeroBot.WallValue(s, s.Content.TryAbility("firewall"), Hero(s, "knight"), new Coord(x, y));

        [Test]
        public void A_wall_across_a_foes_way_in_is_worth_casting()
        {
            // Wolf (2,1), MoveRange 1: reach 2. The knight at (2,7) is its nearest hero (6 away).
            var s = Setup(Stage(("wolf", 2, 1)), (2, 7), (5, 7));
            Assert.That(Wall(s, 2, 3), Is.EqualTo(3f), "(2,3) lies on its straight walk, 2 steps out");
            Assert.That(Wall(s, 2, 4), Is.EqualTo(0f), "3 steps out: beyond its reach this turn");
            Assert.That(Wall(s, 0, 1), Is.EqualTo(0f), "beside it but off its way");
        }

        [Test]
        public void A_hero_on_the_wall_costs_more_than_it_buys()
        {
            // The cleric at (1,3) would stand in the row painted through (2,3): the wall burns the party too.
            var s = Setup(Stage(("wolf", 2, 1)), (2, 7), (1, 3));
            Assert.That(Wall(s, 2, 3), Is.LessThan(0f));
        }

        [Test]
        public void A_foe_already_on_the_wall_counts()
        {
            // The tank (MoveRange 0) sits on (1,3), in the row painted through (2,3); (2,3) is also one step on its way in.
            var s = Setup(Stage(("tank", 1, 3)), (2, 7), (5, 7));
            Assert.That(Wall(s, 2, 3), Is.EqualTo(6f));
        }

        [Test]
        public void The_tile_behind_a_foe_counts_only_with_a_ready_shove()
        {
            // Tank (2,5) beside the knight (2,6): the wall through (2,4) is behind it.
            var none = Setup(Stage(("tank", 2, 5)), (2, 6), (5, 7));
            Assert.That(Wall(none, 2, 4), Is.EqualTo(0f), "no shove ready: nothing qualifies, never cast superfluously");
            var s = Setup(Stage(("tank", 2, 5)), (2, 6), (5, 7), "shove");
            Assert.That(BattleEngine.GetSlotStatus(s, Hero(s, "knight").Id, 1), Is.EqualTo(SlotStatus.Ready));
            Assert.That(Wall(s, 2, 4), Is.EqualTo(5f), "the knight's shove sends the tank into it");
        }

        [Test]
        public void Only_a_visible_hazard_mark_counts()
        {
            var s = Setup(Stage(("wolf", 2, 1)), (2, 7), (5, 7), "shove");
            Assert.That(HeroBot.WallValue(s, s.Content.TryAbility("shove"), Hero(s, "knight"), new Coord(2, 3)), Is.EqualTo(0f));
        }
    }
}
