using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>GG-T827 (bible 9.1): a <see cref="DamagedEvent"/> carries what combat text reads by - the blow's
    /// overshoot past the HP left (<see cref="DamagedEvent.Overkill"/>) and the target's elemental multiplier as a word
    /// (<see cref="DamagedEvent.Effectiveness"/>).</summary>
    public class CombatTextEventTests
    {
        [Test]
        public void Effectiveness_reads_the_multiplier()
        {
            Assert.That(HitRead.Of(1f), Is.EqualTo(HitEffectiveness.Normal));
            Assert.That(HitRead.Of(1.5f), Is.EqualTo(HitEffectiveness.Weak));
            Assert.That(HitRead.Of(0.5f), Is.EqualTo(HitEffectiveness.Resist));
            Assert.That(HitRead.Of(0f), Is.EqualTo(HitEffectiveness.Immune));
            Assert.That(HitRead.Of(-1f), Is.EqualTo(HitEffectiveness.Immune));
        }

        [Test]
        public void Overkill_is_the_damage_past_the_hp_left()
        {
            Assert.That(HitRead.Overkill(30, 10), Is.EqualTo(20));
            Assert.That(HitRead.Overkill(10, 10), Is.EqualTo(0), "an exact kill spills nothing");
            Assert.That(HitRead.Overkill(5, 10), Is.EqualTo(0), "a blow that leaves HP spills nothing");
            Assert.That(HitRead.Overkill(0, 0), Is.EqualTo(0));
        }

        [Test]
        public void Effectiveness_reads_class_resistances_and_gear()
        {
            var db = Mini();
            db.Classes["tank"].Resistances[DamageType.Fire] = 2f;
            db.Classes["tank"].Resistances[DamageType.Ice] = 0.5f;
            db.Classes["tank"].Resistances[DamageType.Dark] = 0f;
            var (s, _) = Battle(db, Stage(("tank", 2, 0)), ("knight", 5, 7));
            var tank = At(s, 2, 0);
            Assert.That(HitRead.Of(tank, DamageType.Fire), Is.EqualTo(HitEffectiveness.Weak));
            Assert.That(HitRead.Of(tank, DamageType.Ice), Is.EqualTo(HitEffectiveness.Resist));
            Assert.That(HitRead.Of(tank, DamageType.Dark), Is.EqualTo(HitEffectiveness.Immune));
            Assert.That(HitRead.Of(tank, DamageType.Lightning), Is.EqualTo(HitEffectiveness.Normal));
            tank.GearResist[DamageType.Lightning] = 0.8f;
            Assert.That(HitRead.Of(tank, DamageType.Lightning), Is.EqualTo(HitEffectiveness.Resist), "gear counts too");
            Assert.That(HitRead.Of(null, DamageType.Fire), Is.EqualTo(HitEffectiveness.Normal));
        }

        private static DamagedEvent Fireball(ContentDb db, System.Action<Unit> prep = null)
        {
            var (s, _) = Battle(db, Stage(("tank", 2, 0)), ("knight", 5, 7), ("cleric", 4, 7));
            var k = Hero(s, "knight");
            var tank = At(s, 2, 0);
            prep?.Invoke(tank);
            k.Bar[0] = new AbilitySlot { AbilityId = "fireball" };
            GrantMana(s, 3);
            var ev = BattleEngine.Apply(s, new UseSlotCommand { HeroId = k.Id, Slot = 0, Target = new Coord(2, 0) });
            return ev.OfType<DamagedEvent>().First(d => d.TargetId == tank.Id && d.Hit != HitKind.Miss && d.SourceId == k.Id);
        }

        [Test]
        public void A_fire_hit_on_a_fire_weak_foe_reports_Weak()
        {
            var db = Mini();
            db.Classes["tank"].Resistances[DamageType.Fire] = 1.5f;
            var d = Fireball(db);
            Assert.That(d.Effectiveness, Is.EqualTo(HitEffectiveness.Weak), d.ToString());
            Assert.That(d.Overkill, Is.EqualTo(0), "the tank survives");
        }

        [Test]
        public void A_fire_hit_on_a_fire_resistant_foe_reports_Resist()
        {
            var db = Mini();
            db.Classes["tank"].Resistances[DamageType.Fire] = 0.5f;
            Assert.That(Fireball(db).Effectiveness, Is.EqualTo(HitEffectiveness.Resist));
        }

        [Test]
        public void A_plain_hit_reports_Normal()
        {
            Assert.That(Fireball(Mini()).Effectiveness, Is.EqualTo(HitEffectiveness.Normal));
        }

        [Test]
        public void A_killing_blow_past_the_hp_left_reports_its_overkill()
        {
            int hpLeft = 3;
            var d = Fireball(Mini(), tank => tank.Hp = hpLeft);
            Assert.That(d.HpAfter, Is.EqualTo(0), d.ToString());
            Assert.That(d.Amount, Is.GreaterThan(hpLeft));
            Assert.That(d.Overkill, Is.EqualTo(d.Amount - hpLeft));
            Assert.That(d.ToString(), Does.Contain("overkill="));
        }
    }
}
