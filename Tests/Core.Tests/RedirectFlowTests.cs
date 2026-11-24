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
    /// <summary>Redirect Flow (§7, GG-T887): the Monk lifts every debuff off an adjacent ally and pours it, with its
    /// remaining duration, into the foe beside the Monk; the hero bot only wants it for a damaging or disabling debuff.</summary>
    public class RedirectFlowTests
    {
        /// <summary>Mini plus a redirect skill, a harmless debuff and a silence.</summary>
        private static ContentDb WithRedirect()
        {
            var db = Mini();
            db.Abilities["redirect"] = new AbilityDef
            {
                Id = "redirect", Name = "Redirect Flow", Kind = AbilityKind.Skill, CooldownTurns = 3, Shape = TargetShape.SingleActor,
                Mode = TargetMode.PickActor, Filter = TargetFilter.AllyOnly, AdjacentOnly = true, RedirectsDebuffs = true,
            };
            db.Buffs["marked"] = new BuffDef { Id = "marked", Name = "Marked", Unit = DurationUnit.Turns, Duration = 2 };
            db.Buffs["hushed"] = new BuffDef { Id = "hushed", Name = "Hushed", Unit = DurationUnit.Turns, Duration = 2, BlocksSpells = true };
            return db;
        }

        /// <summary>The cleric (caster) at (2,5), the knight (ally) beside it at (2,4), a tank beside the cleric at (3,5) and
        /// a goblin far off.</summary>
        private static (BattleState s, Unit monk, Unit ally, Unit tank) Board(ContentDb db, bool foeInReach = true)
        {
            var enemies = foeInReach ? Stage(("tank", 3, 5), ("goblin", 6, 0)) : Stage(("goblin", 6, 0));
            var (s, _) = Battle(db, enemies, ("knight", 2, 4), ("cleric", 2, 5));
            var monk = Hero(s, "cleric");
            monk.Bar[0] = new AbilitySlot { AbilityId = "redirect" };
            var tank = s.Units.FirstOrDefault(u => u.Def.Id == "tank" && u.Alive);
            return (s, monk, Hero(s, "knight"), tank);
        }

        private static void Afflict(Unit u, BuffDef d, int remaining) => u.Buffs.Add(new BuffInstance { Def = d, Remaining = remaining });

        private static List<BattleEvent> Use(BattleState s, Unit monk, Unit ally)
        {
            monk.ActedThisWindow = false;
            return BattleEngine.Apply(s, new UseSlotCommand { HeroId = monk.Id, Slot = 0, Target = ally.Pos });
        }

        [Test]
        public void Redirect_flow_ships_in_the_monks_kit_as_an_adjacent_ally_skill()
        {
            var a = Real.Abilities["monk_redirectflow"];
            Assert.That(a.RedirectsDebuffs, Is.True);
            Assert.That(a.Kind, Is.EqualTo(AbilityKind.Skill));
            Assert.That((a.Shape, a.Mode, a.Filter), Is.EqualTo((TargetShape.SingleActor, TargetMode.PickActor, TargetFilter.AllyOnly)));
            Assert.That(a.AdjacentOnly, Is.True);
            Assert.That(Real.Classes["monk"].Abilities, Does.Contain("monk_redirectflow"));
            Assert.That(ContentLoader.Validate(Real).Where(p => p.Contains("monk_redirectflow")), Is.Empty);
        }

        [Test]
        public void Every_debuff_moves_onto_the_foe_with_its_remaining_duration()
        {
            var db = WithRedirect();
            var (s, monk, ally, tank) = Board(db);
            Afflict(ally, db.Buffs["burning"], 3);
            Afflict(ally, db.Buffs["hushed"], 1);
            Afflict(ally, db.Buffs["protection"], 1); // a buff stays where it is

            var ev = Use(s, monk, ally);

            Assert.That(ally.HasBuff("burning") || ally.HasBuff("hushed"), Is.False, "the ally is cleansed");
            Assert.That(ally.HasBuff("protection"), Is.True, "only debuffs flow");
            Assert.That(tank.HasBuff("burning") && tank.HasBuff("hushed"), Is.True, "the foe beside the monk takes them");
            Assert.That(tank.Buffs.Find(b => b.Def.Id == "burning").Remaining, Is.EqualTo(3), "the time it had left comes along");
            var moved = ev.OfType<DebuffsRedirectedEvent>().Single();
            Assert.That((moved.FromId, moved.ToId), Is.EqualTo((ally.Id, tank.Id)));
            Assert.That(moved.BuffIds, Is.EquivalentTo(new[] { "burning", "hushed" }));
            Assert.That(ev.OfType<BuffRemovedEvent>().Count(r => r.UnitId == ally.Id && r.Reason == "redirected"), Is.EqualTo(2));
            Assert.That(ev.OfType<BuffAppliedEvent>().Count(b => b.UnitId == tank.Id), Is.EqualTo(2), "a CombatText per moved status");
        }

        [Test]
        public void It_is_no_pick_without_a_foe_beside_the_monk_or_a_debuff_on_the_ally()
        {
            var db = WithRedirect();
            var (s, monk, ally, _) = Board(db, foeInReach: false);
            Afflict(ally, db.Buffs["burning"], 3);
            Assert.That(TargetResolver.IsValidPick(s, db.Abilities["redirect"], monk, ally.Pos), Is.False, "no foe in reach");
            Use(s, monk, ally);
            Assert.That(ally.HasBuff("burning"), Is.True, "refused: the ally keeps it");

            var (s2, monk2, ally2, _) = Board(WithRedirect());
            Assert.That(TargetResolver.IsValidPick(s2, s2.Content.Abilities["redirect"], monk2, ally2.Pos), Is.False, "nothing to move");
        }

        [Test]
        public void The_bot_values_it_only_for_a_damaging_or_disabling_debuff()
        {
            var db = WithRedirect();
            var (s, monk, ally, _) = Board(db);
            Afflict(ally, db.Buffs["marked"], 2);
            Assert.That(RedirectFlow.Value(s, monk, ally), Is.EqualTo(0f), "a harmless mark is not worth the turn");

            Afflict(ally, db.Buffs["burning"], 4);
            Assert.That(RedirectFlow.Value(s, monk, ally), Is.GreaterThan(10f), "burning with time left is");

            var (s2, monk2, ally2, _) = Board(WithRedirect(), foeInReach: false);
            Afflict(ally2, s2.Content.Buffs["burning"], 4);
            Assert.That(RedirectFlow.Value(s2, monk2, ally2), Is.EqualTo(0f), "no foe in reach");
        }
    }
}
