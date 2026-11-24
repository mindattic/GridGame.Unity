using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Core.Rules;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>Keyword statuses (§8.1): what each one does on the board, against the shipped buffs.json numbers.</summary>
    public class KeywordStatusTests
    {
        private static Unit Enemy(BattleState s, string cls) => s.Units.First(u => u.Def.Id == cls && u.Alive);

        /// <summary>Mini plus the real Shredded and a 1-damage Fire dart that applies it (spells go around a plate, so the
        /// only plate damage is the debuff's own chip).</summary>
        private static ContentDb WithShred()
        {
            var db = Mini();
            db.Buffs["shredded"] = Real.Buffs["shredded"];
            db.Abilities["shred_dart"] = new AbilityDef { Id = "shred_dart", Name = "Shred Dart", Kind = AbilityKind.Spell, Cost = 1, BaseDamage = 1, DamageType = DamageType.Fire, ApplyBuff = "shredded", Shape = TargetShape.SingleActor, Mode = TargetMode.PickActor, Filter = TargetFilter.EnemyOnly };
            return db;
        }

        private static System.Collections.Generic.List<BattleEvent> Shred(BattleState s, Unit caster, Unit target)
        {
            s.Bank.Add(1);
            caster.ActedThisWindow = false; // one cast a window (GG-T308); these tests are about the status
            return BattleEngine.Apply(s, new UseSlotCommand { HeroId = caster.Id, Slot = 0, Target = target.Pos });
        }

        [Test]
        public void Shredded_is_a_three_stack_physical_debuff_with_a_glossary_entry()
        {
            var d = Real.Buffs["shredded"];
            Assert.That(d.Kind, Is.EqualTo(BuffKind.Debuff));
            Assert.That(d.Stacking, Is.EqualTo(BuffStacking.Stack));
            Assert.That(d.MaxStacks, Is.EqualTo(3));
            Assert.That(d.PhysicalDamageTakenBonus, Is.EqualTo(0.10f).Within(1e-4));
            Assert.That(d.PlateChip, Is.GreaterThan(0));
            Assert.That(Real.TryTerm("Shredded"), Is.Not.Null);
            Assert.That(Real.TryTerm("Shred"), Is.Not.Null);
        }

        [Test]
        public void Each_shredded_stack_adds_ten_percent_physical_damage_taken_up_to_three()
        {
            var (s, _) = Battle(WithShred(), Stage(("tank", 2, 2)), ("knight", 5, 7), ("mage", 4, 7));
            var m = Hero(s, "mage");
            m.Bar[0] = new AbilitySlot { AbilityId = "shred_dart" };
            var tank = Enemy(s, "tank");
            var knight = Hero(s, "knight");
            float clean = Formulas.ExpectedPhysical(knight, tank, 10f);
            Assert.That(Formulas.TakenMultiplier(tank, DamageType.Physical), Is.EqualTo(1f));

            Shred(s, m, tank);
            Assert.That(Formulas.TakenMultiplier(tank, DamageType.Physical), Is.EqualTo(1.1f).Within(1e-4));
            for (int i = 0; i < 3; i++) Shred(s, m, tank);

            Assert.That(tank.Buffs.Count(b => b.Def.Id == "shredded"), Is.EqualTo(3), "it stacks to 3, no further");
            Assert.That(Formulas.TakenMultiplier(tank, DamageType.Physical), Is.EqualTo(1.3f).Within(1e-4));
            Assert.That(Formulas.TakenMultiplier(tank, DamageType.Fire), Is.EqualTo(1f), "only physical damage");
            Assert.That(Formulas.ExpectedPhysical(knight, tank, 10f) / clean, Is.EqualTo(1.3f).Within(1e-3));
        }

        [Test]
        public void Shredded_chips_the_plate_from_behind_where_a_blow_never_could()
        {
            var db = WithShred();
            db.Classes["goblin"].ArmorPlate = 40;
            db.Classes["goblin"].BaseStats.Vitality = 60;
            db.Classes["goblin"].DropTable = null;
            // The goblin faces the knight below it; the mage casts from behind.
            var (s, _) = Battle(db, Stage(("goblin", 2, 3), ("tank", 0, 0)), ("knight", 2, 4), ("mage", 2, 0));
            var gob = Enemy(s, "goblin");
            Assert.That(gob.Facing, Is.EqualTo(new Coord(0, 1)));
            var m = Hero(s, "mage");
            m.Bar[0] = new AbilitySlot { AbilityId = "shred_dart" };
            int chip = db.Buffs["shredded"].PlateChip, before = gob.PlateHp;
            Assert.That(before, Is.GreaterThan(chip));

            var ev = Shred(s, m, gob);
            Assert.That(ev.Any(e => e is PlateHitEvent ph && ph.UnitId == gob.Id && ph.Amount == chip && ph.AttackerId == m.Id), Is.True);
            Assert.That(gob.PlateHp, Is.EqualTo(before - chip));

            gob.PlateHp = 1; // the last sliver: the next shred breaks it off
            var ev2 = Shred(s, m, gob);
            Assert.That(ev2.Any(e => e is PlateBrokenEvent pb && pb.UnitId == gob.Id), Is.True);
            Assert.That(gob.HasPlate, Is.False);
        }
    }
}
