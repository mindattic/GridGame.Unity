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
    /// <summary>GG-T884: the Blue Lion's Intimidate (bible §7): a roar that leaves every adjacent foe Frightened of the Lion,
    /// so on its next turn it backs straight away - into a firewall, out of a pincer round a hero - and the HeroBot's
    /// reason to roar (<see cref="Intimidate.Value"/>, scored on the tiles the fleeing foes reach).</summary>
    public class IntimidateTests
    {
        private static ContentDb WithRoar()
        {
            var db = Mini();
            db.Buffs["frightened"] = Real.Buffs["frightened"];
            db.Abilities["bluelion_intimidate"] = Real.Ability("bluelion_intimidate");
            db.Classes["wolf"].MoveRange = 2;
            db.Classes["wolf"].BaseStats.Vitality = 60;
            db.Classes["goblin"].MoveRange = 1;
            db.Classes["goblin"].DropTable = null;
            return db;
        }

        private static List<BattleEvent> Roar(BattleState s, Unit lion)
        {
            lion.Bar[0] = new AbilitySlot { AbilityId = "bluelion_intimidate" };
            lion.ActedThisWindow = false; // one action a window (GG-T308); these tests are about the skill
            return BattleEngine.Apply(s, new UseSlotCommand { HeroId = lion.Id, Slot = 0, Target = lion.Pos });
        }

        private static Unit Foe(BattleState s, string cls) => s.Units.First(u => u.Def.Id == cls && u.Alive && !u.IsHero);

        private static void Firewall(ContentDb db, BattleState s, Coord at)
        {
            s.Region.Rules.Add("firewalls");
            s.Region.Tiles[at] = new TileMark { State = "firewall", Rule = "firewalls", Hazard = true, OwnerTeam = Team.Hero };
        }

        private static ContentDb WithFirewalls()
        {
            var db = WithRoar();
            db.Elements = Real.Elements;
            RegionRuleCatalog.Index(db, new List<RegionRuleDef> { Real.TryRegionRule("firewalls") });
            return db;
        }

        [Test]
        public void Intimidate_is_the_blue_lions_adjacent_roar_that_frightens()
        {
            var a = Real.Ability("bluelion_intimidate");
            Assert.That(a.Kind, Is.EqualTo(AbilityKind.Skill));
            Assert.That(a.Shape, Is.EqualTo(TargetShape.Cross));
            Assert.That(a.Radius, Is.EqualTo(1));
            Assert.That(a.Mode, Is.EqualTo(TargetMode.Auto));
            Assert.That(a.Filter, Is.EqualTo(TargetFilter.EnemyOnly));
            Assert.That(a.BaseDamage, Is.EqualTo(0));
            Assert.That(Real.Buffs[a.ApplyBuff].Frightens, Is.True);
            Assert.That(Intimidate.Is(Real, a), Is.True);
            Assert.That(Real.Classes["bluelion"].Abilities, Does.Contain("bluelion_intimidate"));
            Assert.That(ContentLoader.Validate(Real).Where(p => p.Contains("bluelion_intimidate")), Is.Empty);
        }

        [Test]
        public void The_roar_frightens_adjacent_foes_of_the_lion_and_no_one_further()
        {
            var (s, _) = Battle(WithRoar(), Stage(("wolf", 2, 2), ("goblin", 5, 3)), ("knight", 2, 3), ("mage", 6, 7));
            var lion = Hero(s, "knight");
            var wolf = Foe(s, "wolf");
            var gob = Foe(s, "goblin");

            Roar(s, lion);

            Assert.That(wolf.IsFrightened, Is.True);
            Assert.That(wolf.FearSourceId, Is.EqualTo(lion.Id), "it fears the Lion");
            Assert.That(gob.IsFrightened, Is.False, "out of the roar's reach");
            Assert.That(lion.IsFrightened, Is.False);
        }

        [Test]
        public void A_boss_shrugs_the_roar_off()
        {
            var (s, _) = Battle(WithRoar(), Stage(("boss", 2, 1)), ("knight", 2, 3), ("mage", 6, 7));
            var boss = Foe(s, "boss");
            var ev = Roar(s, Hero(s, "knight"));
            Assert.That(ev.OfType<AnnounceEvent>().Any(a => a.Text == "Immune!" && a.UnitId == boss.Id), Is.True);
            Assert.That(boss.IsFrightened, Is.False);
        }

        [Test]
        public void A_frightened_foe_backs_into_the_firewall_behind_it_as_predicted()
        {
            var db = WithFirewalls();
            var (s, _) = Battle(db, Stage(("wolf", 2, 2)), ("knight", 2, 3), ("mage", 6, 7));
            Firewall(db, s, new Coord(2, 1));
            var lion = Hero(s, "knight");
            var wolf = Foe(s, "wolf");
            var predicted = Intimidate.FleePath(s, wolf, lion);
            Assert.That(predicted, Is.EqualTo(new List<Coord> { new Coord(2, 1), new Coord(2, 0) }), "straight back, through the fire");

            Roar(s, lion);
            Assert.That(wolf.IsFrightened, Is.True);
            int hp = wolf.Hp, lionHp = lion.Hp;
            s.TurnEntryOf(wolf.Id).Ip = BattleConfig.BarLength - 1;
            var ev = BattleEngine.Apply(s, new ShieldCommand());

            Assert.That(ev.OfType<SteppedEvent>().Any(e => e.UnitId == wolf.Id && e.To == new Coord(2, 1)), Is.True);
            Assert.That(wolf.Hp, Is.LessThan(hp), "the fire bites");
            Assert.That(wolf.Pos, Is.EqualTo(predicted.Last()), "the bot's prediction is where it ends");
            Assert.That(lion.Hp, Is.EqualTo(lionHp), "a fleeing foe doesn't attack");
        }

        [Test]
        public void The_bot_values_a_roar_that_drives_a_foe_into_fire_over_one_that_does_not()
        {
            var a = Real.Ability("bluelion_intimidate");
            var db = WithFirewalls();
            var (plain, _) = Battle(db, Stage(("wolf", 2, 2)), ("knight", 2, 3), ("mage", 6, 7));
            var (fire, _) = Battle(db, Stage(("wolf", 2, 2)), ("knight", 2, 3), ("mage", 6, 7));
            Firewall(db, fire, new Coord(2, 1));

            var lp = Hero(plain, "knight");
            var lf = Hero(fire, "knight");
            float vPlain = Intimidate.Value(plain, a, lp, lp.Pos);
            float vFire = Intimidate.Value(fire, a, lf, lf.Pos);

            Assert.That(vPlain, Is.GreaterThan(0f), "a foe beside the Lion loses its blow");
            Assert.That(vFire, Is.GreaterThan(vPlain), "the fire behind it is worth the roar");
            Assert.That(Foe(fire, "wolf").Pos, Is.EqualTo(new Coord(2, 2)), "pure: nothing moved");
        }

        [Test]
        public void The_bot_values_a_roar_that_breaks_a_pincer_round_the_lion()
        {
            var a = Real.Ability("bluelion_intimidate");
            var (s, _) = Battle(WithRoar(), Stage(("goblin", 1, 3), ("goblin", 3, 3)), ("knight", 2, 3), ("mage", 6, 7));
            var lion = Hero(s, "knight");
            Assert.That(PincerPlanner.TrappedAllies(s, Team.Hero, out _, null), Is.EqualTo(1), "two goblins pin the Lion");

            float v = Intimidate.Value(s, a, lion, lion.Pos);

            Assert.That(v, Is.GreaterThanOrEqualTo(Intimidate.FreedValue), "both back off a tile and the pincer opens");
            Assert.That(s.UnitAt(new Coord(1, 3)), Is.Not.Null);
            Assert.That(s.UnitAt(new Coord(3, 3)), Is.Not.Null, "pure: both goblins back where they were");
        }

        [Test]
        public void Nothing_to_scare_is_worth_nothing()
        {
            var a = Real.Ability("bluelion_intimidate");
            var (s, _) = Battle(WithRoar(), Stage(("wolf", 2, 0)), ("knight", 2, 5), ("mage", 6, 7));
            var lion = Hero(s, "knight");
            Assert.That(Intimidate.Value(s, a, lion, lion.Pos), Is.EqualTo(0f));

            var (s2, _) = Battle(WithRoar(), Stage(("boss", 2, 1)), ("knight", 2, 3), ("mage", 6, 7));
            var l2 = Hero(s2, "knight");
            Assert.That(Intimidate.Value(s2, a, l2, l2.Pos), Is.EqualTo(0f), "a boss doesn't scare");
        }
    }
}
