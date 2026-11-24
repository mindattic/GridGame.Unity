using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>GG-T997: a direct (single-target) enemy charge with Pull (Hook Line) hauls its victim toward the caster,
    /// and Heavy, Rooted and Anchor-Stance-braced heroes hold.</summary>
    public class DirectChargePullTests
    {
        private static ContentDb Db()
        {
            var db = Mini();
            db.Buffs["rooted"] = Real.Buffs["rooted"];
            db.Buffs["anchored"] = Real.Buffs["anchored"];
            db.Abilities["charge_hook"] = new AbilityDef
            {
                Id = "charge_hook", Name = "Hook", Kind = AbilityKind.Spell, BaseDamage = 1, Pull = 2, DamageType = DamageType.Physical,
                Shape = TargetShape.SingleActor, Mode = TargetMode.PickActor, Filter = TargetFilter.EnemyOnly,
            };
            return db;
        }

        /// <summary>Queue the hook at <paramref name="at"/> from the wolf, about to land, and play until it resolves.</summary>
        private static List<BattleEvent> Hook(BattleState s, Unit at)
        {
            var wolf = s.Units.First(u => u.Def.Id == "wolf" && u.Alive);
            s.Timeline.Add(new TimelineEntry
            {
                Id = s.NextEntryId++, Kind = TimelineKind.Cast, OwnerId = wolf.Id, AbilityId = "charge_hook", CasterTeam = Team.Enemy,
                IsCharge = true, ChargeShape = ChargeShape.Single, TargetUnitId = at.Id, TargetTile = at.Pos, TotalCastTicks = 1, RemainingTicks = 1,
            });
            var all = new List<BattleEvent>();
            for (int i = 0; i < 40 && !all.OfType<AbilityResolvedEvent>().Any(e => e.AbilityId == "charge_hook"); i++)
                all.AddRange(BattleEngine.Apply(s, new ShieldCommand()));
            return all;
        }

        private static (BattleState s, Unit wolf, Unit knight) Setup()
        {
            var (s, _) = Battle(Db(), Stage(("wolf", 2, 0)), ("knight", 2, 4), ("cleric", 5, 7));
            return (s, s.Units.First(u => u.Def.Id == "wolf"), Hero(s, "knight"));
        }

        [Test]
        public void A_direct_charge_with_pull_hauls_the_hero_toward_the_caster()
        {
            var (s, wolf, knight) = Setup();
            var ev = Hook(s, knight);
            Assume.That(ev.OfType<AbilityResolvedEvent>().Any(e => e.AbilityId == "charge_hook"), Is.True);
            Assert.That(ev.OfType<DisplacedEvent>().Any(d => d.UnitId == knight.Id && d.ByUnitId == wolf.Id), Is.True);
            Assert.That(knight.Pos.Y, Is.LessThan(4), "dragged up the column toward the wolf");
            AssertInvariants(s);
        }

        [Test]
        public void A_rooted_or_heavy_hero_holds()
        {
            var (s, _, knight) = Setup();
            knight.Buffs.Add(new BuffInstance { Def = s.Content.Buffs["rooted"], Remaining = 3 });
            var ev = Hook(s, knight);
            Assert.That(ev.OfType<DisplacedEvent>().Any(d => d.UnitId == knight.Id), Is.False);
            Assert.That(knight.Pos, Is.EqualTo(new Coord(2, 4)));

            var (s2, _, k2) = Setup();
            k2.Buffs.Add(new BuffInstance { Def = new BuffDef { Id = "weighty", Name = "Weighty", Kind = BuffKind.Buff, Heavy = true, Unit = DurationUnit.Turns, Duration = 3 }, Remaining = 3 });
            Assume.That(k2.IsHeavy, Is.True);
            var ev2 = Hook(s2, k2);
            Assert.That(ev2.OfType<DisplacedEvent>().Any(d => d.UnitId == k2.Id), Is.False);
            Assert.That(ev2.OfType<AnnounceEvent>().Any(a => a.Text == "Too heavy!"), Is.True);
        }

        [Test]
        public void A_hero_braced_by_an_anchoring_ally_holds()
        {
            var (s, _, knight) = Setup();
            var cleric = Hero(s, "cleric");
            cleric.Pos = new Coord(3, 4);
            cleric.Buffs.Add(new BuffInstance { Def = s.Content.Buffs["anchored"], Remaining = 1, SourceId = cleric.Id });
            var ev = Hook(s, knight);
            Assert.That(ev.OfType<DisplacedEvent>().Any(d => d.UnitId == knight.Id), Is.False);
            Assert.That(ev.OfType<AnnounceEvent>().Any(a => a.Text == "Braced!" && a.UnitId == knight.Id), Is.True);
        }
    }
}
