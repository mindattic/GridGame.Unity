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
    /// <summary>GG-T864: the Ripper's Flay (bible §7): a hit that lays Shredded on, chipping the plate from any side — the
    /// answer to plated and Heavy foes — and the HeroBot's reason to reach for it.</summary>
    public class FlayTests
    {
        /// <summary>Mini plus the shipped Flay and Shredded, a goblin with a thick plate and the body to survive the cut.</summary>
        private static ContentDb WithFlay(int plate = 200, float vit = 60f, bool heavy = false)
        {
            var db = Mini();
            db.Buffs["shredded"] = Real.Buffs["shredded"];
            db.Abilities["ripper_flay"] = Real.Ability("ripper_flay");
            var gob = db.Classes["goblin"];
            gob.ArmorPlate = plate;
            gob.BaseStats.Vitality = vit;
            gob.DropTable = null;
            gob.Heavy = heavy;
            return db;
        }

        private static List<BattleEvent> Flay(BattleState s, Unit hero, Unit target)
        {
            hero.Bar[0] = new AbilitySlot { AbilityId = "ripper_flay" };
            hero.ActedThisWindow = false; // one action a window (GG-T308); these tests are about the skill
            return BattleEngine.Apply(s, new UseSlotCommand { HeroId = hero.Id, Slot = 0, Target = target.Pos });
        }

        private static Unit Goblin(BattleState s) => s.Units.First(u => u.Def.Id == "goblin" && u.Alive);

        [Test]
        public void Flay_is_the_rippers_adjacent_physical_skill_that_shreds()
        {
            var a = Real.Ability("ripper_flay");
            Assert.That(a.Kind, Is.EqualTo(AbilityKind.Skill));
            Assert.That(a.DamageType, Is.EqualTo(DamageType.Physical));
            Assert.That(a.BaseDamage, Is.GreaterThan(0));
            Assert.That(a.AdjacentOnly, Is.True);
            Assert.That(a.ApplyBuff, Is.EqualTo("shredded"));
            Assert.That(a.ApplyBuffChance, Is.EqualTo(1f));
            Assert.That(Real.Buffs[a.ApplyBuff].PlateChip, Is.GreaterThan(0));
            Assert.That(Real.Classes["ripper"].Abilities, Does.Contain("ripper_flay"));
        }

        [Test]
        public void A_flay_the_plate_takes_head_on_still_shreds_and_chips_it()
        {
            var db = WithFlay();
            var (s, _) = Battle(db, Stage(("goblin", 2, 3), ("tank", 0, 0)), ("knight", 2, 4), ("mage", 5, 7));
            var gob = Goblin(s);
            gob.Facing = new Coord(0, 1); // its plate faces the knight below it
            var k = Hero(s, "knight");
            int hp = gob.Hp, plate = gob.PlateHp, chip = db.Buffs["shredded"].PlateChip;

            var ev = Flay(s, k, gob);

            Assert.That(gob.Hp, Is.EqualTo(hp), "the plate took the blow");
            Assert.That(gob.HasBuff("shredded"), Is.True, "but the flay still shreds");
            var hits = ev.OfType<PlateHitEvent>().Where(p => p.UnitId == gob.Id).ToList();
            Assert.That(hits.Count, Is.EqualTo(2), "the blow dents the plate, then Shredded bites it");
            Assert.That(hits.Last().Amount, Is.EqualTo(chip));
            Assert.That(gob.PlateHp, Is.EqualTo(plate - hits.Sum(p => p.Amount)));
        }

        [Test]
        public void A_flay_from_the_flank_cuts_the_body_and_bites_the_plate_too()
        {
            var db = WithFlay();
            var (s, _) = Battle(db, Stage(("goblin", 2, 3), ("tank", 0, 0)), ("knight", 3, 3), ("mage", 5, 7));
            var gob = Goblin(s);
            gob.Facing = new Coord(0, 1); // plate faces down; the knight stands at its side
            var k = Hero(s, "knight");
            int hp = gob.Hp, plate = gob.PlateHp, chip = db.Buffs["shredded"].PlateChip;

            var ev = Flay(s, k, gob);

            Assert.That(gob.Hp, Is.LessThan(hp));
            Assert.That(gob.HasBuff("shredded"), Is.True);
            Assert.That(gob.PlateHp, Is.EqualTo(plate - chip));
            Assert.That(ev.Any(e => e is BuffAppliedEvent b && b.UnitId == gob.Id && b.BuffId == "shredded"), Is.True);
        }

        [Test]
        public void The_bot_values_a_shred_on_plated_tough_and_heavy_foes_and_not_on_a_soft_fully_shredded_one()
        {
            var d = Real.Buffs["shredded"];
            Unit Foe(ContentDb db)
            {
                var (s, _) = Battle(db, Stage(("goblin", 2, 3)), ("knight", 2, 5));
                return Goblin(s);
            }
            float plated = HeroBot.ShredValue(Foe(WithFlay(plate: 30, vit: 10f)), d);
            float bare = HeroBot.ShredValue(Foe(WithFlay(plate: 0, vit: 10f)), d);
            float tough = HeroBot.ShredValue(Foe(WithFlay(plate: 0, vit: 60f)), d);
            float heavy = HeroBot.ShredValue(Foe(WithFlay(plate: 0, vit: 60f, heavy: true)), d);
            Assert.That(plated, Is.GreaterThan(bare), "a plate to bite");
            Assert.That(tough, Is.GreaterThan(bare), "high VIT");
            Assert.That(heavy, Is.GreaterThan(tough), "Heavy");

            var full = Foe(WithFlay(plate: 0, vit: 10f));
            for (int i = 0; i < d.MaxStacks; i++) full.Buffs.Add(new BuffInstance { Def = d, Remaining = d.Duration });
            Assert.That(HeroBot.ShredValue(full, d), Is.EqualTo(0f), "fully shredded and no plate: nothing left to gain");
        }
    }
}
