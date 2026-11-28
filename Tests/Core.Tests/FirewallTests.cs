using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Core.Rules;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>The Firewall (GG-T931, BIBLE §7 and §14.23): a Spell that paints regionrules <c>firewalls</c> state
    /// <c>firewall</c> on a <see cref="TargetShape.Wall"/>, three tiles across the line from the caster. Whoever enters it
    /// (a hero too: Affects Any) takes 6 Fire and Burning; ending a turn on it, 4 Fire and Burning again. Ice douses it, the
    /// cleanse set clears it, and it reads as danger so foes steer round it.</summary>
    public class FirewallTests
    {
        /// <summary>Mini content plus the real element table, firewalls rule and Firewall spell, a one-tile shove, an ice
        /// bomb and the Purifying Salts' cleanse.</summary>
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
            db.Abilities["icebomb"] = new AbilityDef
            {
                Id = "icebomb", Name = "icebomb", Kind = AbilityKind.Skill, BaseDamage = 5, DamageType = DamageType.Ice, Shape = TargetShape.SingleTile, Mode = TargetMode.PickTile, Filter = TargetFilter.Any,
            };
            db.Abilities["salts"] = new AbilityDef
            {
                Id = "salts", Name = "salts", Kind = AbilityKind.Skill, Shape = TargetShape.Square, Radius = 1, Mode = TargetMode.PickTile, Filter = TargetFilter.Any, TileReaction = "cleanse",
            };
            return db;
        }

        /// <summary>The knight carries the Firewall in slot 0 and <paramref name="tool"/> (if any) in slot 1; the bank pays.</summary>
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

        private static List<BattleEvent> GuardUntil(BattleState s, System.Func<bool> done, int max = 20)
        {
            var all = new List<BattleEvent>();
            for (int i = 0; i < max && !s.IsOver && !done(); i++)
            {
                all.AddRange(BattleEngine.Apply(s, new ShieldCommand()));
                AssertInvariants(s);
            }
            return all;
        }

        private static List<BattleEvent> Cast(BattleState s, Coord at) =>
            BattleEngine.Apply(s, new UseSlotCommand { HeroId = Hero(s, "knight").Id, Slot = 0, Target = at });

        /// <summary>The knight's tool, once its slot is ready again after the cast.</summary>
        private static List<BattleEvent> UseTool(BattleState s, Coord at)
        {
            var knight = Hero(s, "knight");
            GuardUntil(s, () => BattleEngine.GetSlotStatus(s, knight.Id, 1) == SlotStatus.Ready, 10);
            return BattleEngine.Apply(s, new UseSlotCommand { HeroId = knight.Id, Slot = 1, Target = at });
        }

        private static string StateAt(BattleState s, int x, int y) => s.Region.At(new Coord(x, y))?.State;

        private static Coord[] C(params (int x, int y)[] tiles) => tiles.Select(t => new Coord(t.x, t.y)).ToArray();

        // ── Content ──────────────────────────────────────────────────────────────────────────────

        [Test]
        public void The_firewall_content_loads_and_validates()
        {
            var problems = ContentLoader.Validate(Real);
            var mine = problems.Where(p => p.Contains("firewall")).ToList();
            Assert.That(mine, Is.Empty, string.Join("\n", mine));
            var a = Real.TryAbility("firewall");
            Assert.That(a, Is.Not.Null);
            Assert.That(a.Kind, Is.EqualTo(AbilityKind.Spell));
            Assert.That(a.Mode, Is.EqualTo(TargetMode.PickTile));
            Assert.That(a.Filter, Is.EqualTo(TargetFilter.EmptyOnly));
            Assert.That(a.Shape, Is.EqualTo(TargetShape.Wall));
            Assert.That(a.Radius, Is.EqualTo(1));
            Assert.That(a.DamageType, Is.EqualTo(DamageType.Fire));
            Assert.That(a.RuleEffects.Any(fx => fx.Kind == RuleEffectKind.MarkTiles && fx.Area == RuleArea.EventTiles && fx.State == "firewall"), Is.True);
            Assert.That(Real.Classes["redmage"].Abilities, Does.Contain("firewall"));
            var st = Real.TryTileState("firewall");
            Assert.That(st, Is.Not.Null);
            Assert.That(st.Hazard, Is.True);
            Assert.That(st.Element, Is.EqualTo(DamageType.Fire));
            Assert.That(st.Affects, Is.EqualTo(TileAffects.Any));
            Assert.That(st.LifeSeconds, Is.EqualTo(12f));
            Assert.That(Real.TileStateRule["firewall"], Is.EqualTo("firewalls"));
            var ice = Real.Elements.Elements.Single(e => e.Type == DamageType.Ice);
            Assert.That(ice.Tiles.Any(t => t.On == "firewall" && t.Kind == TileReactionKind.Clear), Is.True, "Ice douses it");
            var cleanse = Real.Elements.Reactions.Single(r => r.Id == "cleanse");
            Assert.That(cleanse.Tiles.Any(t => t.On == "firewall" && t.Kind == TileReactionKind.Clear), Is.True, "the Salts clear it");
        }

        // ── Shape ────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void A_wall_lies_across_the_line_from_the_caster()
        {
            var anchor = new Coord(3, 4);
            Assert.That(TargetResolver.Tiles(anchor, TargetShape.Wall, 1, new Coord(3, 7)), Is.EquivalentTo(C((2, 4), (3, 4), (4, 4))), "cast from below: a row");
            Assert.That(TargetResolver.Tiles(anchor, TargetShape.Wall, 1, new Coord(3, 1)), Is.EquivalentTo(C((2, 4), (3, 4), (4, 4))), "cast from above: a row");
            Assert.That(TargetResolver.Tiles(anchor, TargetShape.Wall, 1, new Coord(0, 4)), Is.EquivalentTo(C((3, 3), (3, 4), (3, 5))), "cast from the side: a column");
            Assert.That(TargetResolver.Tiles(anchor, TargetShape.Wall, 1, new Coord(5, 5)), Is.EquivalentTo(C((3, 3), (3, 4), (3, 5))), "mostly sideways: a column");
            Assert.That(TargetResolver.Tiles(anchor, TargetShape.Wall, 1, new Coord(5, 6)), Is.EquivalentTo(C((2, 4), (3, 4), (4, 4))), "a diagonal: a row");
            Assert.That(TargetResolver.Tiles(anchor, TargetShape.Wall, 1), Is.EquivalentTo(C((2, 4), (3, 4), (4, 4))), "no caster: a row");
            Assert.That(TargetResolver.Tiles(new Coord(0, 4), TargetShape.Wall, 1, new Coord(0, 7)), Is.EquivalentTo(C((0, 4), (1, 4))), "clipped at the edge");
        }

        [Test]
        public void Casting_paints_the_party_s_wall_in_front_of_the_caster()
        {
            var s = Setup(Stage(("tank", 5, 0)), (2, 7), (5, 7));
            var ev = Cast(s, new Coord(2, 4));
            Assert.That(ev.OfType<AbilityRefusedEvent>().Any(), Is.False, string.Join(" | ", ev));
            foreach (var c in C((1, 4), (2, 4), (3, 4)))
            {
                var m = s.Region.At(c);
                Assert.That(m?.State, Is.EqualTo("firewall"), $"{c} is in the wall");
                Assert.That(m.OwnerTeam, Is.EqualTo(Team.Hero));
            }
            Assert.That(s.Region.At(new Coord(2, 3)), Is.Null);
            Assert.That(s.Region.At(new Coord(2, 5)), Is.Null);
            Assert.That(ev.OfType<AnnounceEvent>().Any(a => a.Text == "Missed!"), Is.False, "a board-only spell misses nobody");
            Assert.That(ev.OfType<RegionRuleEvent>().Any(), Is.False, "a hero-laid wall announces no rule");

            s = Setup(Stage(("tank", 5, 0)), (5, 4), (5, 7));
            Cast(s, new Coord(2, 4));
            Assert.That(new[] { StateAt(s, 2, 3), StateAt(s, 2, 4), StateAt(s, 2, 5) }, Is.All.EqualTo("firewall"), "cast from the side: a column");
            Assert.That(StateAt(s, 1, 4), Is.Null);
            AssertInvariants(s);
        }

        // ── Burning ──────────────────────────────────────────────────────────────────────────────

        [Test]
        public void A_foe_shoved_into_the_wall_burns()
        {
            var s = Setup(Stage(("tank", 2, 5)), (2, 6), (5, 7), "shove");
            var tank = s.Units.Single(u => u.Def.Id == "tank");
            Cast(s, new Coord(2, 4));
            Assert.That(StateAt(s, 2, 4), Is.EqualTo("firewall"));
            Assert.That(tank.HasBuff("burning"), Is.False);
            UseTool(s, tank.Pos);
            Assert.That(tank.Pos, Is.EqualTo(new Coord(2, 4)), "shoved into the wall");
            Assert.That(tank.HasBuff("burning"), Is.True, "stepping in sets it burning");
            AssertInvariants(s);
        }

        [Test]
        public void A_hero_walking_into_the_party_s_own_wall_burns_too()
        {
            var s = Setup(Stage(("tank", 5, 0)), (2, 7), (1, 5));
            var cleric = Hero(s, "cleric");
            Cast(s, new Coord(2, 4));
            Assert.That(StateAt(s, 1, 4), Is.EqualTo("firewall"));
            Assert.That(RegionBoard.DangerAt(s, new Coord(1, 4), cleric), Is.GreaterThan(0f), "Affects Any: the party reads it as danger too");
            int hp = cleric.Hp;
            BattleEngine.Apply(s, new MoveCommand { HeroId = cleric.Id, Path = P((1, 4)) });
            Assert.That(cleric.Pos, Is.EqualTo(new Coord(1, 4)));
            Assert.That(cleric.HasBuff("burning"), Is.True);
            Assert.That(!cleric.Alive || cleric.Hp < hp, Is.True, "6 Fire on the way in");
            AssertInvariants(s);
        }

        [Test]
        public void Ending_a_turn_in_the_wall_burns()
        {
            // The wall is painted under the cleric (EmptyOnly checks only the anchor): it never stepped in, so the burn is the
            // TurnEnd hook's.
            var s = Setup(Stage(("tank", 5, 0)), (2, 7), (1, 4));
            var cleric = Hero(s, "cleric");
            int hp = cleric.Hp;
            Cast(s, new Coord(2, 4));
            Assert.That(StateAt(s, 1, 4), Is.EqualTo("firewall"));
            GuardUntil(s, () => cleric.HasBuff("burning") || !cleric.Alive, 10);
            Assert.That(!cleric.Alive || cleric.HasBuff("burning"), Is.True, "a turn ended on the wall: 4 Fire and Burning");
            Assert.That(!cleric.Alive || cleric.Hp < hp, Is.True);
        }

        // ── Counters and bots ────────────────────────────────────────────────────────────────────

        [Test]
        public void Ice_douses_the_wall_and_the_salts_clear_it()
        {
            var s = Setup(Stage(("tank", 5, 0)), (2, 7), (5, 7), "icebomb");
            Cast(s, new Coord(2, 4));
            UseTool(s, new Coord(2, 4));
            Assert.That(StateAt(s, 2, 4), Is.Null, "Ice douses the tile it hits");
            Assert.That(StateAt(s, 1, 4), Is.EqualTo("firewall"), "and only that tile");

            s = Setup(Stage(("tank", 5, 0)), (2, 7), (5, 7), "salts");
            Cast(s, new Coord(2, 4));
            UseTool(s, new Coord(2, 4));
            Assert.That(new[] { StateAt(s, 1, 4), StateAt(s, 2, 4), StateAt(s, 3, 4) }, Is.All.Null, "the cleanse set clears the wall");
        }

        [Test]
        public void Foes_read_the_wall_as_danger()
        {
            var s = Setup(Stage(("wolf", 2, 1)), (2, 7), (5, 7));
            var wolf = s.Units.Single(u => u.Def.Id == "wolf");
            Cast(s, new Coord(2, 4));
            Assert.That(RegionBoard.DangerAt(s, new Coord(2, 4), wolf), Is.GreaterThan(0f), "EnemyPlanner steers round it");
            Assert.That(RegionBoard.DangerAt(s, new Coord(2, 3), wolf), Is.EqualTo(0f));
        }
    }
}
