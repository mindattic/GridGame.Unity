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
    /// <summary>GG-T2069 (bible §13.5): an unyielding plate takes every ordinary blow from its front for 0 and never
    /// dents; a ShattersPlate ability (Sunder) breaks any plate outright from any side, and the bot reaches for it.</summary>
    public class UnyieldingPlateTests
    {
        /// <summary>Mini with a goblin wearing an unyielding plate, a Sunder-style shatter skill, the shipped Shredded and
        /// a plain physical skill to compare.</summary>
        private static ContentDb Unyielding(bool unyielding = true, int plate = 40)
        {
            var db = Mini();
            var gob = db.Classes["goblin"];
            gob.ArmorPlate = plate;
            gob.PlateUnyielding = unyielding;
            gob.BaseStats.Vitality = 60;
            gob.DropTable = null;
            db.Buffs["shredded"] = Real.Buffs["shredded"];
            db.Abilities["sunder"] = new AbilityDef { Id = "sunder", Name = "Sunder", Kind = AbilityKind.Skill, CooldownTurns = 3, BaseDamage = 8, AdjacentOnly = true, ShattersPlate = true, DamageType = DamageType.Physical, Shape = TargetShape.SingleActor, Mode = TargetMode.PickActor, Filter = TargetFilter.EnemyOnly };
            db.Abilities["rend"] = new AbilityDef { Id = "rend", Name = "Rend", Kind = AbilityKind.Skill, CooldownTurns = 2, BaseDamage = 8, AdjacentOnly = true, ApplyBuff = "shredded", ApplyBuffChance = 1f, DamageType = DamageType.Physical, Shape = TargetShape.SingleActor, Mode = TargetMode.PickActor, Filter = TargetFilter.EnemyOnly };
            return db;
        }

        private static Unit Goblin(BattleState s) => s.Units.Single(u => u.Def.Id == "goblin");

        private static List<BattleEvent> Use(BattleState s, Unit hero, string ability, Unit target)
        {
            hero.Bar[0] = new AbilitySlot { AbilityId = ability };
            hero.ActedThisWindow = false;
            return BattleEngine.Apply(s, new UseSlotCommand { HeroId = hero.Id, Slot = 0, Target = target.Pos });
        }

        [Test]
        public void A_blow_on_the_unyielding_front_clangs_for_zero_and_never_dents_it()
        {
            var (s, _) = Battle(Unyielding(), Stage(("goblin", 2, 3), ("tank", 0, 0)), ("knight", 2, 4), ("cleric", 2, 0));
            var gob = Goblin(s);
            Assert.That(gob.Facing, Is.EqualTo(new Coord(0, 1)));
            int hp = gob.Hp, plate = gob.PlateHp;
            var knight = Hero(s, "knight");

            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = Hero(s, "cleric").Id, Path = P((2, 1), (2, 2)) });

            var clang = ev.OfType<PlateHitEvent>().Single(p => p.AttackerId == knight.Id);
            Assert.That(clang.Amount, Is.EqualTo(0));
            Assert.That(gob.PlateHp, Is.EqualTo(plate), "never dented");
            Assert.That(ev.Any(e => e is DamagedEvent d && d.TargetId == gob.Id && d.SourceId == knight.Id), Is.False, "the front takes nothing");
            Assert.That(gob.Hp, Is.LessThan(hp), "the cleric behind it still lands");
            Assert.That(ev.Any(e => e is PlateBrokenEvent), Is.False);
        }

        [Test]
        public void A_physical_skill_and_shredded_do_not_dent_an_unyielding_plate()
        {
            var (s, _) = Battle(Unyielding(), Stage(("goblin", 2, 3), ("tank", 0, 0)), ("knight", 2, 4), ("mage", 5, 7));
            var gob = Goblin(s);
            gob.Facing = new Coord(0, 1);
            int hp = gob.Hp, plate = gob.PlateHp;

            var ev = Use(s, Hero(s, "knight"), "rend", gob);

            Assert.That(gob.Hp, Is.EqualTo(hp));
            Assert.That(gob.PlateHp, Is.EqualTo(plate));
            Assert.That(ev.OfType<PlateHitEvent>().All(p => p.Amount == 0), Is.True);
        }

        [Test]
        public void Sunder_shatters_an_unyielding_plate_from_the_front_and_the_hit_lands_on_the_body()
        {
            var (s, _) = Battle(Unyielding(), Stage(("goblin", 2, 3), ("tank", 0, 0)), ("knight", 2, 4), ("mage", 5, 7));
            var gob = Goblin(s);
            gob.Facing = new Coord(0, 1);
            int hp = gob.Hp, plate = gob.PlateHp;
            var knight = Hero(s, "knight");

            var ev = Use(s, knight, "sunder", gob);

            var sh = ev.OfType<PlateShatteredEvent>().Single();
            Assert.That(sh.UnitId, Is.EqualTo(gob.Id));
            Assert.That(sh.AttackerId, Is.EqualTo(knight.Id));
            Assert.That(sh.AbilityId, Is.EqualTo("sunder"));
            Assert.That(sh.Left, Is.EqualTo(plate));
            Assert.That(ev.IndexOf(ev.OfType<PlateBrokenEvent>().Single()), Is.GreaterThan(ev.IndexOf(sh)));
            Assert.That(gob.HasPlate, Is.False);
            Assert.That(gob.Hp, Is.LessThan(hp), "the plate is gone, so the blow lands");
        }

        [Test]
        public void Sunder_shatters_an_ordinary_plate_from_the_flank_too()
        {
            var (s, _) = Battle(Unyielding(unyielding: false), Stage(("goblin", 2, 3), ("tank", 0, 0)), ("knight", 3, 3), ("mage", 5, 7));
            var gob = Goblin(s);
            gob.Facing = new Coord(0, 1);

            var ev = Use(s, Hero(s, "knight"), "sunder", gob);

            Assert.That(ev.OfType<PlateShatteredEvent>().Count(), Is.EqualTo(1));
            Assert.That(gob.HasPlate, Is.False);
        }

        [Test]
        public void The_bot_values_a_shatter_most_on_an_unyielding_plate()
        {
            Unit Foe(ContentDb db) { var (s, _) = Battle(db, Stage(("goblin", 2, 3)), ("knight", 2, 5)); return Goblin(s); }
            float hard = HeroBot.ShatterValue(Foe(Unyielding()));
            float plain = HeroBot.ShatterValue(Foe(Unyielding(unyielding: false)));
            float bare = HeroBot.ShatterValue(Foe(Unyielding(unyielding: false, plate: 0)));
            Assert.That(hard, Is.GreaterThan(plain));
            Assert.That(plain, Is.GreaterThan(bare));
            Assert.That(bare, Is.EqualTo(0f));
        }

        [Test]
        public void Validation_wants_a_plate_under_unyielding_and_a_foe_for_a_shatter()
        {
            var db = Unyielding(plate: 0);
            db.Abilities["sunder"].Filter = TargetFilter.AllyOnly;
            var p = ContentLoader.Validate(db).Where(x => x.Contains("GG-T2069")).ToList();
            Assert.That(p.Any(x => x.StartsWith("class goblin")), Is.True);
            Assert.That(p.Any(x => x.StartsWith("ability sunder")), Is.True);
            Assert.That(ContentLoader.Validate(Real).Where(x => x.Contains("GG-T2069")), Is.Empty);
        }
    }
}
