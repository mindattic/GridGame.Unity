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
    /// <summary>The Machinist's Barricade (GG-T934, BIBLE §7 and §14.23): it raises a <c>machinist_barricade</c> on an
    /// empty tile for 20 s. Nothing walks through it; a line charge stops at it, sparing whoever is behind, and bashes it;
    /// a foe ending its turn beside it bashes it too; the third hit knocks it down.</summary>
    public class BarricadeTests
    {
        private const string State = "machinist_barricade";

        /// <summary>Mini content plus the real Barricade, its rule and the Herd Gorer's Bull Rush.</summary>
        private static ContentDb Db()
        {
            var db = Mini();
            db.Abilities["machinist_barricade"] = Real.TryAbility("machinist_barricade");
            db.Abilities["charge_bull_rush"] = Real.TryAbility("charge_bull_rush");
            RegionRuleCatalog.Index(db, new List<RegionRuleDef> { Real.TryRegionRule("machinist-barricade") });
            return db;
        }

        /// <summary>The knight carries the Barricade in slot 0.</summary>
        private static BattleState Setup(ContentDb db, StageDef stage, (int x, int y) knight, (int x, int y) cleric)
        {
            var setup = new BattleSetup { Stage = stage, Seed = 7, UnlockedSlots = 5 };
            var k = new HeroSpec { ClassId = "knight", Start = new Coord(knight.x, knight.y) };
            k.Bar.Add("machinist_barricade");
            setup.Party.Add(k);
            setup.Party.Add(new HeroSpec { ClassId = "cleric", Start = new Coord(cleric.x, cleric.y) });
            return BattleEngine.Create(db, setup, new List<BattleEvent>());
        }

        private static List<BattleEvent> Raise(BattleState s, Coord at) =>
            BattleEngine.Apply(s, new UseSlotCommand { HeroId = Hero(s, "knight").Id, Slot = 0, Target = at });

        private static List<BattleEvent> GuardUntil(BattleState s, System.Func<bool> done, int max = 60)
        {
            var all = new List<BattleEvent>();
            for (int i = 0; i < max && !s.IsOver && !done(); i++)
            {
                all.AddRange(BattleEngine.Apply(s, new ShieldCommand()));
                AssertInvariants(s);
            }
            return all;
        }

        private static Unit Enemy(BattleState s, string cls) => s.Units.First(u => u.Def.Id == cls && u.Alive);

        // ── Content ──────────────────────────────────────────────────────────────────────────────

        [Test]
        public void The_barricade_content_loads_and_validates()
        {
            var problems = ContentLoader.Validate(Real);
            var mine = problems.Where(p => p.Contains("machinist_barricade") || p.Contains("machinist-barricade")).ToList();
            Assert.That(mine, Is.Empty, string.Join("\n", mine));
            var a = Real.TryAbility("machinist_barricade");
            Assert.That(a, Is.Not.Null);
            Assert.That(a.Kind, Is.EqualTo(AbilityKind.Skill));
            Assert.That(a.Mode, Is.EqualTo(TargetMode.PickTile));
            Assert.That(a.Filter, Is.EqualTo(TargetFilter.EmptyOnly));
            Assert.That(a.CooldownTurns, Is.EqualTo(3));
            Assert.That(a.RuleEffects.Any(fx => fx.Kind == RuleEffectKind.MarkTiles && fx.Area == RuleArea.Target && fx.State == State), Is.True);
            Assert.That(Real.Classes["machinist"].Abilities, Does.Contain("machinist_barricade"));
            var st = Real.TryTileState(State);
            Assert.That(st, Is.Not.Null);
            Assert.That(st.Blocks, Is.True);
            Assert.That(st.Pit, Is.False);
            Assert.That(st.Threshold, Is.EqualTo(3));
            Assert.That(st.Becomes, Is.Null, "the third hit clears it");
            Assert.That(st.LifeSeconds, Is.EqualTo(20f));
            Assert.That(Real.TileStateRule[State], Is.EqualTo("machinist-barricade"));
        }

        // ── Raising it, walking into it ──────────────────────────────────────────────────────────

        [Test]
        public void A_raised_barricade_is_the_partys_silent_wall_and_blocks_a_step()
        {
            var s = Setup(Db(), Stage(("tank", 5, 0)), (2, 5), (4, 7));
            var knight = Hero(s, "knight");
            var spot = new Coord(2, 4);
            var ev = Raise(s, spot);
            var m = s.Region.At(spot);
            Assert.That(m?.State, Is.EqualTo(State));
            Assert.That(m.OwnerTeam, Is.EqualTo(Team.Hero));
            Assert.That(m.Blocks, Is.True);
            Assert.That(m.LifeTicks, Is.EqualTo((int)System.Math.Round(20f * BattleConfig.TicksPerSecond)));
            Assert.That(s.Region.Rules, Does.Contain("machinist-barricade"));
            Assert.That(ev.OfType<RegionRuleEvent>().Any(), Is.False, "a hero-raised wall announces no rule");
            Assert.That(s.Walkable(spot), Is.False);
            Assert.That(EnemyPlanner.FootprintStepLegal(s, Enemy(s, "tank"), spot), Is.False, "no foe steps onto it");
            var cleric = Hero(s, "cleric");
            var from = cleric.Pos;
            BattleEngine.Apply(s, new MoveCommand { HeroId = knight.Id, Path = P((2, 4)) });
            Assert.That(knight.Pos, Is.Not.EqualTo(spot), "no hero walks through it either");
            Assert.That(s.Region.At(spot)?.State, Is.EqualTo(State));
            Assert.That(cleric.Pos, Is.EqualTo(from));
            AssertInvariants(s);
        }

        // ── Charges ──────────────────────────────────────────────────────────────────────────────

        [Test]
        public void A_charge_line_planned_after_the_barricade_ends_before_it()
        {
            var db = Db();
            db.Classes["witch"].ChargeShape = ChargeShape.Line;
            var s = Setup(db, Stage(("witch", 2, 0)), (2, 6), (5, 7));
            var wall = new Coord(2, 3);
            Raise(s, wall);
            Assume.That(s.Region.At(wall)?.State, Is.EqualTo(State));
            var line = EnemyPlanner.LineToward(Enemy(s, "witch"), Hero(s, "knight"), s);
            Assert.That(line, Is.EqualTo(new List<Coord> { new Coord(2, 1), new Coord(2, 2) }), "the line stops short of the wall");
        }

        [Test]
        public void The_bull_rush_stops_at_a_barricade_raised_on_its_line_and_spares_the_hero_behind()
        {
            var db = Db();
            db.Classes["witch"].ChargeShape = ChargeShape.Line;
            db.Classes["witch"].ChargeAbility = "charge_bull_rush";
            var s = Setup(db, Stage(("witch", 2, 0)), (2, 6), (2, 2));
            var witch = Enemy(s, "witch");
            var knight = Hero(s, "knight");
            var cleric = Hero(s, "cleric");
            s.TurnEntryOf(witch.Id).Ip = BattleConfig.BarLength - 1;
            var first = BattleEngine.Apply(s, new ShieldCommand());
            var q = first.OfType<CastQueuedEvent>().FirstOrDefault(e => e.AbilityId == "charge_bull_rush");
            Assume.That(q, Is.Not.Null, "the bull rush is queued");
            Assume.That(q.LineTiles, Does.Contain(new Coord(2, 6)), "its line runs down column 2 past the knight");
            var wall = new Coord(2, 4);
            var all = new List<BattleEvent>(Raise(s, wall));
            Assume.That(s.Region.At(wall)?.State, Is.EqualTo(State), "the knight walls the line between them");
            all.AddRange(GuardUntil(s, () => all.OfType<AbilityResolvedEvent>().Any(e => e.AbilityId == "charge_bull_rush"), 12));
            var resolved = all.OfType<AbilityResolvedEvent>().FirstOrDefault(e => e.AbilityId == "charge_bull_rush");
            Assert.That(resolved, Is.Not.Null, "the charge lands");
            Assert.That(resolved.Targets, Does.Not.Contain(knight.Id), "the hero behind the wall is spared");
            Assert.That(all.OfType<DamagedEvent>().Any(d => d.TargetId == knight.Id && d.SourceId == witch.Id), Is.False);
            Assert.That(resolved.Targets, Does.Contain(cleric.Id), "the hero in front of the wall is still on the line");
            Assert.That(resolved.Tiles, Does.Not.Contain(wall).And.Not.Contain(new Coord(2, 6)), "the run ends at the wall");
            Assert.That(all.OfType<AnnounceEvent>().Any(a => a.Text == "Blocked!"), Is.True);
            Assert.That(s.Region.At(wall)?.Counter, Is.EqualTo(1), "the charge is one hit on the barricade");
            AssertInvariants(s);
        }

        // ── Bashing and expiry ───────────────────────────────────────────────────────────────────

        [Test]
        public void Three_foe_turn_ends_beside_it_knock_it_down()
        {
            var s = Setup(Db(), Stage(("goblin", 3, 4), ("tank", 0, 0)), (0, 7), (5, 7));
            var spot = new Coord(2, 4);
            Raise(s, spot);
            var m = s.Region.At(spot);
            Assume.That(m?.State, Is.EqualTo(State));
            m.LifeTicks = 0; // only the hits take it down here
            var all = GuardUntil(s, () => s.Region.At(spot) == null);
            Assert.That(s.Region.At(spot), Is.Null, "the goblin's third turn-end beside it knocks it down");
            var changed = all.OfType<RegionTilesEvent>().Count(e => e.State == State && e.Change == "changed" && e.Tiles.Contains(spot));
            Assert.That(changed, Is.EqualTo(2), "two hits count up first");
            Assert.That(all.OfType<RegionTilesEvent>().Any(e => e.State == State && e.Change == "cleared" && e.Tiles.Contains(spot)), Is.True);
            Assert.That(s.Walkable(spot), Is.True, "the tile is open again");
            AssertInvariants(s);
        }

        [Test]
        public void A_hero_ending_its_turn_beside_it_does_not_bash_it()
        {
            var s = Setup(Db(), Stage(("tank", 5, 0)), (2, 5), (3, 4));
            var spot = new Coord(2, 4);
            Raise(s, spot);
            var m = s.Region.At(spot);
            Assume.That(m?.State, Is.EqualTo(State));
            m.LifeTicks = 0;
            GuardUntil(s, () => false, 6);
            Assert.That(s.Region.At(spot)?.Counter, Is.EqualTo(0), "only foes bash it");
        }

        [Test]
        public void The_barricade_falls_after_twenty_seconds()
        {
            var s = Setup(Db(), Stage(("tank", 5, 0)), (0, 7), (5, 7));
            var spot = new Coord(2, 4);
            Raise(s, spot);
            Assume.That(s.Region.At(spot)?.State, Is.EqualTo(State));
            long start = s.Clock;
            GuardUntil(s, () => s.Region.At(spot) == null, 400);
            Assert.That(s.Region.At(spot), Is.Null, "it crumbles when its 20 s run out");
            Assert.That(s.Clock - start, Is.GreaterThanOrEqualTo((long)(20f * BattleConfig.TicksPerSecond) - 1), "not before");
            Assert.That(s.Walkable(spot), Is.True);
            AssertInvariants(s);
        }
    }
}
