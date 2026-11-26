using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>Gravity Well (§7, GG-T883): the Dark Templar's spell pulls every foe within 2 tiles of the picked tile one
    /// tile toward it, nearest first; Heavy foes resist unless they float, Rooted ones hold, heroes stay; the hero bot
    /// only casts it when it raises the trapped count of the party's next pincer.</summary>
    public class GravityWellTests
    {
        private const string Id = "darktemplar_gravitywell";

        /// <summary>Mini plus the shipped Gravity Well; the witch's charge is off to keep the board quiet.</summary>
        private static ContentDb Db()
        {
            var db = Mini();
            db.Abilities[Id] = Real.Ability(Id);
            db.Classes["witch"].ChargeAbility = null;
            return db;
        }

        private static Unit Enemy(BattleState s, string cls) => s.Units.First(u => u.Def.Id == cls && u.Alive);

        private static List<BattleEvent> Cast(BattleState s, Unit caster, Coord at)
        {
            caster.Bar[0] = new AbilitySlot { AbilityId = Id };
            s.Bank.Add(s.Content.TryAbility(Id).Cost);
            caster.ActedThisWindow = false; // one cast a window (GG-T308)
            caster.Cooldowns.Clear(); // these tests are about the pull, not its cooldown
            return BattleEngine.Apply(s, new UseSlotCommand { HeroId = caster.Id, Slot = 0, Target = at });
        }

        private static void Give(Unit u, BuffDef d) => u.Buffs.Add(new BuffInstance { Def = d, Remaining = d.Duration });

        [Test]
        public void Gravity_well_ships_as_the_dark_templars_pull_spell()
        {
            var a = Real.Ability(Id);
            Assert.That(a.Kind, Is.EqualTo(AbilityKind.Spell));
            Assert.That(a.Mode, Is.EqualTo(TargetMode.PickTile));
            Assert.That(a.BaseDamage, Is.EqualTo(0));
            var pull = GravityWell.Pull(a);
            Assert.That(pull, Is.Not.Null);
            Assert.That((pull.Kind, pull.Area, pull.Move, pull.Team, pull.Radius, pull.Amount),
                Is.EqualTo((RuleEffectKind.MoveUnits, RuleArea.Target, RuleMove.Toward, RuleTeam.Enemy, 2, 1)));
            Assert.That(a.Radius, Is.EqualTo(pull.Radius), "the picked shape shows the reach of the pull");
            Assert.That(Real.Classes["darktemplar"].Abilities, Does.Contain(Id));
            var problems = ContentLoader.Validate(Real).Where(p => p.Contains(Id)).ToList();
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
            Assert.That(GravityWell.Is(Real.Ability("blackwitch_shadowbolt")), Is.False);
        }

        [Test]
        public void Every_foe_in_reach_is_pulled_one_tile_in_nearest_first_and_heroes_stay()
        {
            // Column 2: the goblin (1 from the centre) moves first, so the tank behind it follows it in.
            var (s, _) = Battle(Db(), Stage(("goblin", 2, 1), ("tank", 2, 0), ("wolf", 4, 2), ("witch", 0, 0)), ("knight", 2, 4), ("mage", 5, 7));
            var gob = Enemy(s, "goblin"); var tank = Enemy(s, "tank"); var wolf = Enemy(s, "wolf"); var witch = Enemy(s, "witch");
            var knight = Hero(s, "knight");
            var ev = Cast(s, Hero(s, "mage"), new Coord(2, 2));

            Assert.That(gob.Pos, Is.EqualTo(new Coord(2, 2)), "onto the centre");
            Assert.That(tank.Pos, Is.EqualTo(new Coord(2, 1)), "into the room the goblin left");
            Assert.That(wolf.Pos, Is.EqualTo(new Coord(3, 2)));
            Assert.That(witch.Pos, Is.EqualTo(new Coord(0, 0)), "4 tiles off: out of reach");
            Assert.That(knight.Pos, Is.EqualTo(new Coord(2, 4)), "a hero in reach is not pulled");
            var moved = ev.OfType<DisplacedEvent>().Select(d => d.UnitId).ToList();
            Assert.That(moved, Is.EquivalentTo(new[] { gob.Id, tank.Id, wolf.Id }), "one step each");
            AssertInvariants(s);
        }

        [Test]
        public void A_heavy_foe_resists_unless_it_floats_and_a_rooted_one_holds()
        {
            var db = Db();
            db.Classes["goblin"].Heavy = true;
            var (s, _) = Battle(db, Stage(("goblin", 1, 2), ("wolf", 3, 3)), ("knight", 2, 6), ("mage", 5, 7));
            var gob = Enemy(s, "goblin"); var wolf = Enemy(s, "wolf");
            Give(wolf, new BuffDef { Id = "rooted", Name = "Rooted", Kind = BuffKind.Debuff, Unit = DurationUnit.Turns, Duration = 2, Roots = true });
            Cast(s, Hero(s, "mage"), new Coord(2, 2));
            Assert.That(gob.Pos, Is.EqualTo(new Coord(1, 2)), "Heavy: it won't budge");
            Assert.That(wolf.Pos, Is.EqualTo(new Coord(3, 3)), "Rooted: it holds");

            Give(gob, new BuffDef { Id = Unit.FloatingBuffId, Name = "Floating", Kind = BuffKind.Buff, Unit = DurationUnit.Turns, Duration = 3 });
            Cast(s, Hero(s, "mage"), new Coord(2, 2));
            Assert.That(gob.Pos, Is.EqualTo(new Coord(2, 2)), "a floating Heavy foe is pulled");
        }

        /// <summary>Knight below and cleric above column 2; a wolf at (1,3) and a tank at (3,2) stand off the line.</summary>
        private static BattleState Scattered()
        {
            var (s, _) = Battle(Db(), Stage(("wolf", 1, 3), ("tank", 3, 2)), ("knight", 2, 5), ("cleric", 2, 1));
            return s;
        }

        [Test]
        public void The_bot_values_a_well_that_clumps_foes_into_the_next_pincer_and_nothing_else()
        {
            var s = Scattered();
            var a = s.Content.TryAbility(Id);
            Assert.That(GravityWell.NextPincerTrapped(s), Is.EqualTo(0), "scattered: no drag traps anyone");
            Assert.That(GravityWell.Value(s, a, new Coord(2, 3)), Is.GreaterThanOrEqualTo(2 * GravityWell.TrappedValue),
                "both foes clump into column 2, between the cleric and the knight's next step");
            Assert.That(GravityWell.Value(s, a, new Coord(3, 1)), Is.EqualTo(0f), "a pull that sets no pincer up is superfluous");
            Assert.That(GravityWell.Value(s, a, new Coord(0, 7)), Is.EqualTo(0f), "nobody in reach");
            Assert.That(GravityWell.Value(s, Real.Ability("blackwitch_shadowbolt"), new Coord(2, 3)), Is.EqualTo(0f), "only a gravity well counts");
            Assert.That((Enemy(s, "wolf").Pos, Enemy(s, "tank").Pos), Is.EqualTo((new Coord(1, 3), new Coord(3, 2))), "looking moves nothing");

            var ev = Cast(s, Hero(s, "knight"), new Coord(2, 3));
            Assert.That((Enemy(s, "wolf").Pos, Enemy(s, "tank").Pos), Is.EqualTo((new Coord(2, 3), new Coord(2, 2))), "the cast does what the bot saw");
            Assert.That(GravityWell.NextPincerTrapped(s), Is.EqualTo(2));
            Assert.That(ev.OfType<AbilityResolvedEvent>().Any(r => r.AbilityId == Id), Is.True);
        }

        [Test]
        public void A_well_that_leaves_heavy_foes_where_they_stand_is_worth_less()
        {
            var light = Scattered();
            var db = Db();
            db.Classes["tank"].Heavy = true;
            var (heavy, _) = Battle(db, Stage(("wolf", 1, 3), ("tank", 3, 2)), ("knight", 2, 5), ("cleric", 2, 1));
            var a = db.Abilities[Id];
            Assert.That(GravityWell.Value(heavy, a, new Coord(2, 3)), Is.LessThan(GravityWell.Value(light, a, new Coord(2, 3))));
        }
    }
}
