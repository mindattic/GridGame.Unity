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
    /// <summary>GG-T935 (BIBLE §7, §14.23): the hero bots raise the Machinist's Barricade only where it matters
    /// (<see cref="HeroBot.BarricadeValue"/>): on a pending foe line charge between the charger and a hero (worth the trample
    /// spared; both bots), or (the smart bot only) on the tile a foe's planned walk ends on to close a pincer on a hero.
    /// Anywhere else it is worth nothing, so it is never raised for its own sake.</summary>
    public class BarricadeBotTests
    {
        private static ContentDb Db()
        {
            var db = Mini();
            db.Abilities["machinist_barricade"] = Real.TryAbility("machinist_barricade");
            db.Abilities["charge_bull_rush"] = Real.TryAbility("charge_bull_rush");
            db.Buffs["rooted"] = Real.Buffs["rooted"];
            RegionRuleCatalog.Index(db, new List<RegionRuleDef> { Real.TryRegionRule("machinist-barricade") });
            db.Classes["soldier"] = new ClassDef
            {
                Id = "soldier", Name = "Soldier", Team = Team.Enemy, Tags = { "Humanoid", "Sentient" }, MoveRange = 1,
                BaseStats = new Stats { Strength = 5, Vitality = 6, Agility = 10, Speed = 10, Intelligence = 10 },
            };
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

        /// <summary>A witch with the Bull Rush as a line charge at (2,0), the knight down its column at (2,6), the cleric off
        /// the line; the witch acts first and queues the rush down column 2.</summary>
        private static BattleState Charging(ContentDb db)
        {
            db.Classes["witch"].ChargeShape = ChargeShape.Line;
            db.Classes["witch"].ChargeAbility = "charge_bull_rush";
            var s = Setup(db, Stage(("witch", 2, 0)), (2, 6), (5, 7));
            var witch = s.Units.First(u => u.Def.Id == "witch" && u.Alive);
            s.TurnEntryOf(witch.Id).Ip = BattleConfig.BarLength - 1;
            var ev = BattleEngine.Apply(s, new ShieldCommand());
            var q = ev.OfType<CastQueuedEvent>().FirstOrDefault(e => e.AbilityId == "charge_bull_rush");
            Assume.That(q, Is.Not.Null, "the bull rush is queued");
            Assume.That(q.LineTiles, Does.Contain(new Coord(2, 6)), "its line runs down column 2 past the knight");
            return s;
        }

        private static void Root(Unit u) => u.Buffs.Add(new BuffInstance { Def = Real.Buffs["rooted"], Remaining = 2 });

        [Test]
        public void A_tile_on_a_charge_line_short_of_a_hero_is_worth_the_trample_spared()
        {
            var s = Charging(Db());
            float between = HeroBot.BarricadeValue(s, new Coord(2, 4));
            Assert.That(between, Is.GreaterThan(4f), "a wall between the charger and the knight spares the knight");
            Assert.That(HeroBot.BarricadeValue(s, new Coord(2, 7)), Is.EqualTo(0f), "beyond the knight: spares no one");
            Assert.That(HeroBot.BarricadeValue(s, new Coord(3, 4)), Is.EqualTo(0f), "off the line: nothing");
            Assert.That(HeroBot.BarricadeValue(s, new Coord(0, 4)), Is.EqualTo(0f), "an empty corner of the board: never for its own sake");
        }

        [Test]
        public void A_line_already_walled_short_of_the_tile_spares_nothing_more()
        {
            var s = Charging(Db());
            BattleEngine.Apply(s, new UseSlotCommand { HeroId = Hero(s, "knight").Id, Slot = 0, Target = new Coord(2, 2) });
            Assume.That(RegionBoard.StopsCharge(s, new Coord(2, 2)), Is.True, "the first wall stands");
            Assert.That(HeroBot.BarricadeValue(s, new Coord(2, 4)), Is.EqualTo(0f), "a second wall behind it buys nothing");
        }

        [Test]
        public void The_bot_walls_off_a_charge_aimed_at_a_hero_that_cannot_step_aside()
        {
            var s = Charging(Db());
            var knight = Hero(s, "knight");
            Root(knight);
            Root(Hero(s, "cleric"));
            Assume.That(BattleEngine.GetSlotStatus(s, knight.Id, 0), Is.EqualTo(SlotStatus.Ready));
            var cmd = new HeroBot(7).Decide(s);
            Assert.That(cmd, Is.InstanceOf<UseSlotCommand>(), cmd?.ToString());
            var use = (UseSlotCommand)cmd;
            Assert.That(use.HeroId, Is.EqualTo(knight.Id));
            Assert.That(use.Slot, Is.EqualTo(0));
            Assert.That(new[] { new Coord(2, 1), new Coord(2, 2), new Coord(2, 3), new Coord(2, 4), new Coord(2, 5) }, Does.Contain(use.Target),
                "the wall goes on the rush's line between the witch and the knight");
        }

        [Test]
        public void The_smart_bot_shuts_the_tile_a_foe_needs_to_close_a_pincer()
        {
            // Knight (2,3); a Sentient soldier already west of it at (1,3); a second soldier at (4,3), one step from the open
            // far end (3,3), which closes soldier-knight-soldier.
            var s = Setup(Db(), Stage(("soldier", 1, 3), ("soldier", 4, 3)), (2, 3), (5, 7));
            var ends = HeroBot.EnemyPincerEnds(s);
            Assume.That(ends.ContainsKey(new Coord(3, 3)), Is.True, "the far soldier plans to close the pincer at (3,3)");
            Assert.That(HeroBot.BarricadeValue(s, new Coord(3, 3), ends), Is.GreaterThan(6f), "the smart bot walls the pincer end");
            Assert.That(HeroBot.BarricadeValue(s, new Coord(3, 3)), Is.EqualTo(0f), "the casual bot reads only the charge");
            Assert.That(HeroBot.BarricadeValue(s, new Coord(2, 5), ends), Is.EqualTo(0f), "no pincer ends there");
        }

        [Test]
        public void Only_a_standing_wall_counts_as_a_barricade()
        {
            var s = Setup(Db(), Stage(("witch", 2, 0)), (2, 6), (5, 7));
            Assert.That(HeroBot.WallRaised(s, s.Content.TryAbility("machinist_barricade"))?.Id, Is.EqualTo("machinist_barricade"));
            Assert.That(HeroBot.WallRaised(s, s.Content.TryAbility("charge_bull_rush")), Is.Null);
        }
    }
}
