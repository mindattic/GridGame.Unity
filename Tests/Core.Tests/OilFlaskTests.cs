using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Bot;
using GridGame.Core.Content;
using GridGame.Core.Rules;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>The Alchemist's Oil Flask (GG-T886, BIBLE §7 and §14.23): a Skill that paints regionrules <c>oilslicks</c>
    /// state <c>oil</c> on a Plus around an empty tile. A foe that enters it is Slowed (the party treads it freely); Fire turns
    /// it into a <c>firewall</c> (elements.json Fire <c>Tiles</c>), so the Alchemist and the Red Mage combo. The hero bots
    /// price it by <see cref="OilFlask.Value"/> while another hero has a ready slot that lights it.</summary>
    public class OilFlaskTests
    {
        /// <summary>Mini content plus the real element table, the oil and firewall rules, the Oil Flask, the Firewall, a
        /// one-tile shove and a fire bomb.</summary>
        private static ContentDb Db()
        {
            var db = Mini();
            db.Elements = Real.Elements;
            db.Buffs["slowed"] = Real.TryBuff("slowed");
            db.Abilities["alchemist_oilflask"] = Real.TryAbility("alchemist_oilflask");
            db.Abilities["firewall"] = Real.TryAbility("firewall");
            RegionRuleCatalog.Index(db, new List<RegionRuleDef> { Real.TryRegionRule("oilslicks"), Real.TryRegionRule("firewalls") });
            db.Abilities["shove"] = new AbilityDef
            {
                Id = "shove", Name = "shove", Kind = AbilityKind.Skill, BaseDamage = 1, Knockback = 1, AdjacentOnly = true, DamageType = DamageType.Physical,
                Shape = TargetShape.SingleActor, Mode = TargetMode.PickActor, Filter = TargetFilter.EnemyOnly,
            };
            db.Abilities["firebomb"] = new AbilityDef
            {
                Id = "firebomb", Name = "firebomb", Kind = AbilityKind.Skill, BaseDamage = 5, DamageType = DamageType.Fire, Shape = TargetShape.SingleTile, Mode = TargetMode.PickTile, Filter = TargetFilter.Any,
            };
            return db;
        }

        /// <summary>The knight carries the Oil Flask in slot 0 and <paramref name="tool"/> (if any) in slot 1; the cleric
        /// carries <paramref name="clericTool"/> (if any); the bank pays.</summary>
        private static BattleState Setup(StageDef stage, (int x, int y) knight, (int x, int y) cleric, string tool = null, string clericTool = null)
        {
            var setup = new BattleSetup { Stage = stage, Seed = 7, UnlockedSlots = 5 };
            var k = new HeroSpec { ClassId = "knight", Start = new Coord(knight.x, knight.y) };
            k.Bar.Add("alchemist_oilflask");
            if (tool != null) k.Bar.Add(tool);
            setup.Party.Add(k);
            var c = new HeroSpec { ClassId = "cleric", Start = new Coord(cleric.x, cleric.y) };
            if (clericTool != null) c.Bar.Add(clericTool);
            setup.Party.Add(c);
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

        private static List<BattleEvent> Throw(BattleState s, Coord at) =>
            BattleEngine.Apply(s, new UseSlotCommand { HeroId = Hero(s, "knight").Id, Slot = 0, Target = at });

        /// <summary>The knight's tool, once its slot is ready again after the throw.</summary>
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
        public void The_oil_flask_content_loads_and_validates()
        {
            var problems = ContentLoader.Validate(Real);
            var mine = problems.Where(p => p.Contains("oil")).ToList();
            Assert.That(mine, Is.Empty, string.Join("\n", mine));
            var a = Real.TryAbility("alchemist_oilflask");
            Assert.That(a, Is.Not.Null);
            Assert.That(a.Kind, Is.EqualTo(AbilityKind.Skill));
            Assert.That(a.Mode, Is.EqualTo(TargetMode.PickTile));
            Assert.That(a.Filter, Is.EqualTo(TargetFilter.EmptyOnly));
            Assert.That(a.Shape, Is.EqualTo(TargetShape.Plus));
            Assert.That(a.Radius, Is.EqualTo(1));
            Assert.That(a.BaseDamage, Is.EqualTo(0));
            Assert.That(a.RuleEffects.Any(fx => fx.Kind == RuleEffectKind.MarkTiles && fx.Area == RuleArea.EventTiles && fx.State == "oil"), Is.True);
            Assert.That(Real.Classes["alchemist"].Abilities, Does.Contain("alchemist_oilflask"));
            var st = Real.TryTileState("oil");
            Assert.That(st, Is.Not.Null);
            Assert.That(st.Hazard, Is.True);
            Assert.That(st.Element, Is.Null);
            Assert.That(st.Affects, Is.EqualTo(TileAffects.FoesOfOwner));
            Assert.That(st.LifeSeconds, Is.EqualTo(15f));
            Assert.That(Real.TileStateRule["oil"], Is.EqualTo("oilslicks"));
            var fire = Real.Elements.Elements.Single(e => e.Type == DamageType.Fire);
            Assert.That(fire.Tiles.Any(t => t.On == "oil" && t.Kind == TileReactionKind.Becomes && t.Into == "firewall"), Is.True, "Fire lights the oil");
            var cleanse = Real.Elements.Reactions.Single(r => r.Id == "cleanse");
            Assert.That(cleanse.Tiles.Any(t => t.On == "oil" && t.Kind == TileReactionKind.Clear), Is.True, "the Salts clear it");
            var k = OilFlask.Kindling(Real, a);
            Assert.That(k, Is.Not.Null);
            Assert.That(k.Value.Element, Is.EqualTo(DamageType.Fire));
            Assert.That(k.Value.Becomes.Id, Is.EqualTo("firewall"));
            Assert.That(OilFlask.Is(Real, Real.TryAbility("firewall")), Is.False, "the wall itself is no kindling");
        }

        // ── Painting and slowing ─────────────────────────────────────────────────────────────────

        [Test]
        public void A_throw_splashes_the_party_s_oil_over_a_plus()
        {
            var s = Setup(Stage(("tank", 5, 0)), (2, 7), (5, 7));
            var ev = Throw(s, new Coord(2, 4));
            Assert.That(ev.OfType<AbilityRefusedEvent>().Any(), Is.False, string.Join(" | ", ev));
            foreach (var c in C((2, 4), (1, 4), (3, 4), (2, 3), (2, 5)))
            {
                var m = s.Region.At(c);
                Assert.That(m?.State, Is.EqualTo("oil"), $"{c} is in the pool");
                Assert.That(m.OwnerTeam, Is.EqualTo(Team.Hero));
            }
            Assert.That(StateAt(s, 1, 3), Is.Null);
            Assert.That(StateAt(s, 2, 6), Is.Null);
            Assert.That(ev.OfType<AnnounceEvent>().Any(a => a.Text == "Missed!"), Is.False, "a board-only throw misses nobody");
            Assert.That(ev.OfType<RegionRuleEvent>().Any(), Is.False, "a hero-laid slick announces no rule");
            AssertInvariants(s);
        }

        [Test]
        public void A_foe_shoved_into_the_oil_is_slowed()
        {
            // Tank (2,5) beside the knight (2,6); the pool around (2,3) covers (2,4), right behind it.
            var s = Setup(Stage(("tank", 2, 5)), (2, 6), (5, 7), "shove");
            var tank = s.Units.Single(u => u.Def.Id == "tank");
            Throw(s, new Coord(2, 3));
            Assert.That(StateAt(s, 2, 4), Is.EqualTo("oil"));
            Assert.That(tank.HasBuff("slowed"), Is.False);
            UseTool(s, tank.Pos);
            Assert.That(tank.Pos, Is.EqualTo(new Coord(2, 4)), "shoved into the oil");
            Assert.That(tank.HasBuff("slowed"), Is.True, "stepping in slows it");
            AssertInvariants(s);
        }

        [Test]
        public void The_party_treads_its_own_oil_freely()
        {
            var s = Setup(Stage(("tank", 5, 0)), (2, 7), (1, 5));
            var cleric = Hero(s, "cleric");
            Throw(s, new Coord(2, 4));
            Assert.That(StateAt(s, 1, 4), Is.EqualTo("oil"));
            BattleEngine.Apply(s, new MoveCommand { HeroId = cleric.Id, Path = P((1, 4)) });
            Assert.That(cleric.Pos, Is.EqualTo(new Coord(1, 4)));
            Assert.That(cleric.HasBuff("slowed"), Is.False, "FoesOfOwner: the party's oil slows only foes");
            Assert.That(RegionBoard.DangerAt(s, new Coord(1, 4), s.Units.Single(u => u.Def.Id == "tank")), Is.GreaterThan(0f), "foes steer round it");
        }

        // ── Fire lights it ───────────────────────────────────────────────────────────────────────

        [Test]
        public void A_fire_hit_on_the_oil_turns_it_into_a_firewall()
        {
            var s = Setup(Stage(("tank", 5, 0)), (2, 7), (5, 7), "firebomb");
            Throw(s, new Coord(2, 4));
            var ev = UseTool(s, new Coord(2, 4));
            Assert.That(ev.OfType<AbilityRefusedEvent>().Any(), Is.False, "a pick on an oil tile is legal: Fire reacts with it");
            var m = s.Region.At(new Coord(2, 4));
            Assert.That(m?.State, Is.EqualTo("firewall"), "the hit tile catches");
            Assert.That(m.OwnerTeam, Is.EqualTo(Team.Hero));
            Assert.That(StateAt(s, 1, 4), Is.EqualTo("oil"), "only the tiles the Fire covers");
            AssertInvariants(s);
        }

        [Test]
        public void A_firewall_cast_over_the_oil_lights_it()
        {
            // The Red Mage combo: the wall (a row through (2,4), cast from below) lands on the pool's middle row.
            var s = Setup(Stage(("tank", 5, 0)), (2, 7), (5, 7), "firewall");
            Throw(s, new Coord(2, 4));
            UseTool(s, new Coord(2, 4));
            Assert.That(new[] { StateAt(s, 1, 4), StateAt(s, 2, 4), StateAt(s, 3, 4) }, Is.All.EqualTo("firewall"));
            Assert.That(StateAt(s, 2, 3), Is.EqualTo("oil"), "the oil off the wall's line stays");
            Assert.That(StateAt(s, 2, 5), Is.EqualTo("oil"));
            AssertInvariants(s);
        }

        // ── Bot ──────────────────────────────────────────────────────────────────────────────────

        private static float Value(BattleState s, int x, int y) =>
            OilFlask.Value(s, s.Content.TryAbility("alchemist_oilflask"), Hero(s, "knight"), new Coord(x, y));

        [Test]
        public void The_bot_throws_where_a_ready_fire_ally_can_light_it_under_foes()
        {
            // Wolf (2,2): the pool around (2,4) reaches (2,3), beside it.
            var none = Setup(Stage(("wolf", 2, 2)), (2, 7), (5, 7));
            Assert.That(Value(none, 2, 4), Is.EqualTo(0f), "no ally to light it: no combo value");
            var s = Setup(Stage(("wolf", 2, 2)), (2, 7), (5, 7), clericTool: "firebomb");
            Assert.That(OilFlask.Lighter(s, Hero(s, "knight"), DamageType.Fire), Is.SameAs(Hero(s, "cleric")));
            Assert.That(Value(s, 2, 4), Is.EqualTo(OilFlask.LightValue));
            Assert.That(Value(s, 5, 5), Is.EqualTo(0f), "far from every foe");
        }

        [Test]
        public void A_hero_inside_the_pool_costs_more_than_it_buys()
        {
            // The cleric at (1,4) would stand in oil that burns everyone once lit.
            var s = Setup(Stage(("wolf", 2, 2)), (2, 7), (1, 4), clericTool: "firebomb");
            Assert.That(Value(s, 2, 4), Is.LessThan(0f));
        }

        [Test]
        public void The_oil_on_a_foes_way_in_is_worth_a_throw_by_itself()
        {
            // WallValue prices any visible hazard paint on a foe's way: the oil slows the wolf's approach.
            var s = Setup(Stage(("wolf", 2, 1)), (2, 7), (5, 7));
            Assert.That(HeroBot.WallValue(s, s.Content.TryAbility("alchemist_oilflask"), Hero(s, "knight"), new Coord(2, 3)), Is.GreaterThan(0f));
        }
    }
}
