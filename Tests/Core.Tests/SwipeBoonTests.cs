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
    /// <summary>Swipe Boon (§7, GG-T866): the Thief lifts one boon (Empowered, Ward, Protection, Blurred, Regen) off an
    /// adjacent foe and wears it with the time it had left; the hero bot only wants it against a buffed foe.</summary>
    public class SwipeBoonTests
    {
        /// <summary>Mini plus a swipe skill, Empowered, and a boon that cannot be swiped.</summary>
        private static ContentDb WithSwipe()
        {
            var db = Mini();
            db.Abilities["swipe"] = new AbilityDef
            {
                Id = "swipe", Name = "Swipe Boon", Kind = AbilityKind.Skill, CooldownTurns = 3, Shape = TargetShape.SingleActor,
                Mode = TargetMode.PickActor, Filter = TargetFilter.EnemyOnly, AdjacentOnly = true, StealsBoon = true,
            };
            db.Buffs["empowered"] = new BuffDef { Id = "empowered", Name = "Empowered", Kind = BuffKind.Buff, Unit = DurationUnit.Turns, Duration = 3, OutgoingDamageMultiplier = 1.3f };
            db.Buffs["haloed"] = new BuffDef { Id = "haloed", Name = "Haloed", Kind = BuffKind.Buff, Unit = DurationUnit.Turns, Duration = 2 };
            return db;
        }

        /// <summary>The cleric (thief) at (2,5), a tank beside it at (3,5), a goblin far off at (6,0).</summary>
        private static (BattleState s, Unit thief, Unit tank, Unit goblin) Board(ContentDb db)
        {
            var (s, _) = Battle(db, Stage(("tank", 3, 5), ("goblin", 6, 0)), ("knight", 2, 3), ("cleric", 2, 5));
            var thief = Hero(s, "cleric");
            thief.Bar[0] = new AbilitySlot { AbilityId = "swipe" };
            var tank = s.Units.First(u => u.Def.Id == "tank" && u.Alive);
            var goblin = s.Units.First(u => u.Def.Id == "goblin" && u.Alive);
            return (s, thief, tank, goblin);
        }

        private static void Grant(Unit u, BuffDef d, int remaining) => u.Buffs.Add(new BuffInstance { Def = d, Remaining = remaining });

        private static List<BattleEvent> Use(BattleState s, Unit thief, Unit foe)
        {
            thief.ActedThisWindow = false;
            return BattleEngine.Apply(s, new UseSlotCommand { HeroId = thief.Id, Slot = 0, Target = foe.Pos });
        }

        [Test]
        public void Swipe_boon_ships_in_the_thiefs_kit_as_an_adjacent_foe_skill()
        {
            var a = Real.Abilities["thief_swipeboon"];
            Assert.That(a.StealsBoon, Is.True);
            Assert.That(a.Kind, Is.EqualTo(AbilityKind.Skill));
            Assert.That((a.Shape, a.Mode, a.Filter), Is.EqualTo((TargetShape.SingleActor, TargetMode.PickActor, TargetFilter.EnemyOnly)));
            Assert.That(a.AdjacentOnly, Is.True);
            Assert.That(Real.Classes["thief"].Abilities, Does.Contain("thief_swipeboon"));
            Assert.That(ContentLoader.Validate(Real).Where(p => p.Contains("thief_swipeboon")), Is.Empty);
            foreach (var id in SwipeBoon.Stealable)
                Assert.That(Real.Buffs.ContainsKey(id) && Real.Buffs[id].Kind == BuffKind.Buff, Is.True, $"{id} is a real boon");
        }

        [Test]
        public void One_boon_moves_onto_the_thief_with_its_remaining_duration()
        {
            var db = WithSwipe();
            var (s, thief, tank, _) = Board(db);
            Grant(tank, db.Buffs["protection"], 1);
            Grant(tank, db.Buffs["empowered"], 2);
            Grant(tank, db.Buffs["haloed"], 2);

            var ev = Use(s, thief, tank);

            Assert.That(tank.HasBuff("empowered"), Is.False, "the best boon is taken");
            Assert.That(tank.HasBuff("protection"), Is.True, "only one boon per swipe");
            Assert.That(tank.HasBuff("haloed"), Is.True, "a boon off the list stays");
            Assert.That(thief.HasBuff("empowered"), Is.True, "the thief wears it");
            Assert.That(thief.Buffs.Find(b => b.Def.Id == "empowered").Remaining, Is.GreaterThanOrEqualTo(2), "the time it had left comes along");
            var stolen = ev.OfType<BoonStolenEvent>().Single();
            Assert.That((stolen.CasterId, stolen.FromId, stolen.BuffId, stolen.Remaining), Is.EqualTo((thief.Id, tank.Id, "empowered", 2)));
            Assert.That(ev.OfType<BuffRemovedEvent>().Count(r => r.UnitId == tank.Id && r.Reason == "stolen"), Is.EqualTo(1));
            Assert.That(ev.OfType<BuffAppliedEvent>().Count(b => b.UnitId == thief.Id && b.BuffId == "empowered"), Is.EqualTo(1));
        }

        [Test]
        public void Of_two_of_the_same_the_one_with_more_time_left_goes()
        {
            var db = WithSwipe();
            var (_, _, tank, _) = Board(db);
            Grant(tank, db.Buffs["protection"], 1);
            Grant(tank, db.Buffs["protection"], 3);
            Assert.That(SwipeBoon.Pick(tank).Remaining, Is.EqualTo(3));
        }

        [Test]
        public void It_is_no_pick_on_an_unbuffed_or_distant_foe()
        {
            var db = WithSwipe();
            var (s, thief, tank, goblin) = Board(db);
            Grant(tank, db.Buffs["haloed"], 2);
            Assert.That(TargetResolver.IsValidPick(s, db.Abilities["swipe"], thief, tank.Pos), Is.False, "nothing it can swipe");
            Grant(goblin, db.Buffs["protection"], 2);
            Assert.That(TargetResolver.IsValidPick(s, db.Abilities["swipe"], thief, goblin.Pos), Is.False, "out of reach");
            Grant(tank, db.Buffs["protection"], 2);
            Assert.That(TargetResolver.IsValidPick(s, db.Abilities["swipe"], thief, tank.Pos), Is.True, "a buffed foe beside the thief");

            var (s2, thief2, tank2, _) = Board(WithSwipe());
            Use(s2, thief2, tank2);
            Assert.That(thief2.Buffs, Is.Empty, "refused: nothing stolen");
        }

        [Test]
        public void The_bot_values_it_only_against_a_buffed_foe()
        {
            var db = WithSwipe();
            var (s, thief, tank, goblin) = Board(db);
            Assert.That(SwipeBoon.Value(s, thief, tank), Is.EqualTo(0f), "an unbuffed foe is not worth the turn");
            Grant(tank, db.Buffs["haloed"], 2);
            Assert.That(SwipeBoon.Value(s, thief, tank), Is.EqualTo(0f), "nor one with only an unswipeable boon");
            Grant(goblin, db.Buffs["empowered"], 3);
            Assert.That(SwipeBoon.Value(s, thief, goblin), Is.EqualTo(0f), "nor one out of reach");

            Grant(tank, db.Buffs["protection"], 1);
            float guard = SwipeBoon.Value(s, thief, tank);
            Assert.That(guard, Is.GreaterThan(0f), "a guarded foe is");
            Grant(tank, db.Buffs["empowered"], 3);
            Assert.That(SwipeBoon.Value(s, thief, tank), Is.GreaterThan(guard), "an empowered one more so");
        }
    }
}
