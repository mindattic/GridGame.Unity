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
    /// <summary>The Operative's Time Bomb (GG-T874, BIBLE §7): the spell plants a bomb on a foe that rides it as a queued
    /// hazard (a Follow schedule: its tiles move with the carrier) and blows 4 s of clock later for 16 Fire on the carrier
    /// and every unit beside it, heroes included; a carrier that falls drops it where it fell; the hero bot plants it on a
    /// foe with friends beside it or a shove away.</summary>
    public class TimeBombTests
    {
        /// <summary>Mini plus the real Time Bomb spell, its rule event and a plain 10-damage dart.</summary>
        private static ContentDb Db()
        {
            var db = Mini();
            db.Abilities["operative_timebomb"] = Real.TryAbility("operative_timebomb");
            RegionRuleCatalog.Index(db, new List<RegionRuleDef> { Real.TryRegionRule("timebombs") });
            db.Abilities["dart"] = new AbilityDef { Id = "dart", Name = "Dart", Kind = AbilityKind.Spell, Cost = 1, BaseDamage = 10, DamageType = DamageType.Arcane, Shape = TargetShape.SingleActor, Mode = TargetMode.PickActor, Filter = TargetFilter.EnemyOnly };
            return db;
        }

        private static Unit FoeAt(BattleState s, int x, int y) => s.UnitAt(new Coord(x, y));

        /// <summary>Two 200-HP tanks that never walk, at (2,2) and (5,2); the mage carries the bomb (slot 0) and a dart (slot 1).</summary>
        private static (BattleState s, Unit mage, Unit a, Unit b) Board(params (string cls, int x, int y)[] more)
        {
            var heroes = new List<(string cls, int x, int y)> { ("mage", 0, 7) };
            heroes.AddRange(more);
            var (s, _) = Battle(Db(), Stage(("tank", 2, 2), ("tank", 5, 2)), heroes.ToArray());
            var m = Hero(s, "mage");
            m.Bar[0] = new AbilitySlot { AbilityId = "operative_timebomb" };
            m.Bar[1] = new AbilitySlot { AbilityId = "dart" };
            return (s, m, FoeAt(s, 2, 2), FoeAt(s, 5, 2));
        }

        private static List<BattleEvent> Cast(BattleState s, Unit caster, int slot, Unit target)
        {
            GrantMana(s, 2);
            caster.ActedThisWindow = false; // one cast a window (GG-T308)
            return BattleEngine.Apply(s, new UseSlotCommand { HeroId = caster.Id, Slot = slot, Target = target.Pos });
        }

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

        private static TimelineEntry Bomb(BattleState s) => s.Timeline.SingleOrDefault(e => e.Kind == TimelineKind.Hazard && e.RuleEvent == "timebomb");

        /// <summary>The tiles a bomb on <paramref name="u"/> covers: its tile and the four beside it, on the board.</summary>
        private static List<Coord> Round(Unit u) =>
            new[] { u.Pos, u.Pos + Coord.Up, u.Pos + Coord.Down, u.Pos + Coord.Left, u.Pos + Coord.Right }.Where(BattleState.InBounds).ToList();

        // ── Content ──────────────────────────────────────────────────────────────────────────────

        [Test]
        public void The_time_bomb_ships_as_the_operatives_follow_schedule_spell()
        {
            var problems = ContentLoader.Validate(Real);
            var mine = problems.Where(p => p.Contains("timebomb") || p.Contains("operative_timebomb")).ToList();
            Assert.That(mine, Is.Empty, string.Join("\n", mine));
            var a = Real.TryAbility("operative_timebomb");
            Assert.That(a, Is.Not.Null);
            Assert.That(a.Kind, Is.EqualTo(AbilityKind.Spell));
            Assert.That(a.Mode, Is.EqualTo(TargetMode.PickActor));
            Assert.That(a.Filter, Is.EqualTo(TargetFilter.EnemyOnly));
            var fx = a.RuleEffects.Single();
            Assert.That(fx.Kind, Is.EqualTo(RuleEffectKind.Schedule));
            Assert.That(fx.Follow, Is.True);
            Assert.That(fx.Event, Is.EqualTo("timebomb"));
            Assert.That(fx.Radius, Is.EqualTo(1));
            Assert.That(Real.Classes["operative"].Abilities, Does.Contain("operative_timebomb"));
            var ev = Real.TryRuleEvent("timebomb");
            Assert.That(ev, Is.Not.Null);
            Assert.That(ev.Seconds, Is.EqualTo(4f));
            Assert.That(ev.Effects.Any(e => e.Kind == RuleEffectKind.Damage && e.Area == RuleArea.EventTiles && e.Amount > 0), Is.True);
        }

        // ── Plant, ride, blow ────────────────────────────────────────────────────────────────────

        [Test]
        public void Planting_queues_a_four_second_bomb_riding_the_foe()
        {
            var (s, m, a, _) = Board();
            var ev = Cast(s, m, 0, a);
            var q = ev.OfType<HazardQueuedEvent>().Single(x => x.EventId == "timebomb");
            Assert.That(q.CarrierId, Is.EqualTo(a.Id));
            Assert.That(q.Seconds, Is.EqualTo(4f).Within(0.01f));
            var bomb = Bomb(s);
            Assert.That(bomb, Is.Not.Null);
            Assert.That(bomb.CarrierId, Is.EqualTo(a.Id));
            Assert.That(bomb.HazardTiles, Is.EquivalentTo(Round(a)), "the carrier's tile and every tile beside it");
            Assert.That(bomb.RemainingTicks, Is.LessThanOrEqualTo(4 * BattleConfig.TicksPerSecond));
            AssertInvariants(s);
        }

        [Test]
        public void The_bomb_follows_its_carrier_when_it_is_moved()
        {
            var (s, m, a, _) = Board();
            Cast(s, m, 0, a);
            a.Pos = new Coord(3, 4); // shoved
            var ev = BattleEngine.Apply(s, new ShieldCommand());
            var bomb = Bomb(s);
            Assume.That(bomb, Is.Not.Null, "still ticking");
            Assert.That(bomb.HazardTiles, Is.EquivalentTo(Round(a)), "its tiles moved with the carrier");
            Assert.That(ev.OfType<HazardMovedEvent>().Any(x => x.EntryId == bomb.Id && x.CarrierId == a.Id && !x.Dropped), Is.True);
            AssertInvariants(s);
        }

        [Test]
        public void Shoved_into_a_crowd_it_blows_on_the_carrier_and_everyone_beside_it_heroes_included()
        {
            var (s, m, a, b) = Board(("knight", 4, 3));
            var knight = Hero(s, "knight");
            Cast(s, m, 0, a);
            a.Pos = new Coord(4, 2); // shoved beside the other tank and the knight
            var ev = GuardUntil(s, () => Bomb(s) == null);
            int landed = ev.FindIndex(x => x is HazardLandedEvent l && l.EventId == "timebomb");
            Assert.That(landed, Is.GreaterThanOrEqualTo(0), "it went off");
            var tiles = ((HazardLandedEvent)ev[landed]).Tiles;
            Assert.That(tiles, Does.Contain(new Coord(4, 2)).And.Contain(new Coord(5, 2)).And.Contain(new Coord(4, 3)));
            var hit = ev.Skip(landed + 1).OfType<DamagedEvent>().Where(d => d.Type == DamageType.Fire).Select(d => d.TargetId).ToList();
            Assert.That(hit, Does.Contain(a.Id), "the carrier");
            Assert.That(hit, Does.Contain(b.Id), "the foe it was shoved beside");
            Assert.That(hit, Does.Contain(knight.Id), "a hero beside it too");
            Assert.That(hit, Does.Not.Contain(m.Id), "the caster far off");
            AssertInvariants(s);
        }

        [Test]
        public void A_carrier_that_falls_drops_the_bomb_where_it_fell()
        {
            var (s, m, a, _) = Board();
            Cast(s, m, 0, a);
            var fell = a.Pos;
            a.Hp = 1;
            var ev = Cast(s, m, 1, a);
            Assert.That(a.Alive, Is.False);
            var bomb = Bomb(s);
            Assert.That(bomb, Is.Not.Null, "it still blows");
            Assert.That(bomb.CarrierId, Is.EqualTo(-1));
            Assert.That(bomb.HazardTiles, Does.Contain(fell));
            Assert.That(ev.OfType<HazardMovedEvent>().Any(x => x.Dropped), Is.True);
            AssertInvariants(s);
        }

        // ── The hero bot ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void The_bot_values_a_foe_with_friends_near_and_not_a_lone_one()
        {
            var a = Real.TryAbility("operative_timebomb");
            var (s, _) = Battle(Db(), Stage(("tank", 1, 1), ("tank", 3, 1), ("tank", 5, 5)), ("mage", 0, 7));
            float crowded = HeroBot.TimeBombValue(s, a, new Coord(1, 1)); // a friend a shove away (Manhattan 2)
            float alone = HeroBot.TimeBombValue(s, a, new Coord(5, 5));
            Assert.That(crowded, Is.GreaterThan(0f));
            Assert.That(alone, Is.EqualTo(0f));
            FoeAt(s, 3, 1).Pos = new Coord(2, 1); // now right beside it
            Assert.That(HeroBot.TimeBombValue(s, a, new Coord(1, 1)), Is.GreaterThan(crowded), "beside beats a shove away");
            Assert.That(HeroBot.TimeBombValue(s, Real.TryAbility("fieldeng_overdrive"), new Coord(1, 1)), Is.EqualTo(0f), "only a carried bomb");
        }
    }
}
