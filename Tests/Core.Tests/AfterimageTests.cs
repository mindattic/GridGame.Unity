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
    /// <summary>Afterimage (§1.2.1, §7, GG-T867): the Speedster dashes in a straight line and leaves an afterimage that is a
    /// pincer end of its own for the rest of the action and draws foes' attacks until its next turn, then fades.</summary>
    public class AfterimageTests
    {
        private static ContentDb WithAfterimage()
        {
            var db = Mini();
            db.Abilities["afterimage"] = new AbilityDef
            {
                Id = "afterimage", Name = "Afterimage", Kind = AbilityKind.Skill, CooldownTurns = 3, Shape = TargetShape.SingleTile,
                Mode = TargetMode.PickTile, Filter = TargetFilter.EmptyOnly, Afterimage = 3,
            };
            return db;
        }

        private static Unit Speedster(BattleState s)
        {
            var k = Hero(s, "knight");
            k.Bar[0] = new AbilitySlot { AbilityId = "afterimage" };
            return k;
        }

        private static List<BattleEvent> Dash(BattleState s, Unit u, int x, int y)
        {
            u.ActedThisWindow = false;
            return BattleEngine.Apply(s, new UseSlotCommand { HeroId = u.Id, Slot = 0, Target = new Coord(x, y) });
        }

        [Test]
        public void Afterimage_ships_in_the_speedsters_kit_as_a_dash()
        {
            var a = Real.Abilities["speedster_afterimage"];
            Assert.That(a.Afterimage, Is.GreaterThan(0));
            Assert.That(a.Kind, Is.EqualTo(AbilityKind.Skill));
            Assert.That((a.Mode, a.Filter), Is.EqualTo((TargetMode.PickTile, TargetFilter.EmptyOnly)));
            Assert.That(Real.Classes["speedster"].Abilities, Does.Contain("speedster_afterimage"));
            Assert.That(ContentLoader.Validate(Real).Where(p => p.Contains("speedster_afterimage")), Is.Empty);
        }

        [Test]
        public void Only_a_straight_dash_within_reach_onto_an_empty_tile_is_a_pick()
        {
            var db = WithAfterimage();
            var (s, _) = Battle(db, Stage(("tank", 2, 3)), ("knight", 1, 3), ("cleric", 5, 7));
            var k = Speedster(s);
            var a = db.Abilities["afterimage"];
            Assert.That(TargetResolver.IsValidPick(s, a, k, new Coord(3, 3)), Is.True, "straight past the foe");
            Assert.That(TargetResolver.IsValidPick(s, a, k, new Coord(1, 0)), Is.True, "three tiles up");
            Assert.That(TargetResolver.IsValidPick(s, a, k, new Coord(5, 3)), Is.False, "four tiles is out of reach");
            Assert.That(TargetResolver.IsValidPick(s, a, k, new Coord(2, 4)), Is.False, "never diagonal");
            Assert.That(TargetResolver.IsValidPick(s, a, k, new Coord(2, 3)), Is.False, "never onto a unit");
        }

        [Test]
        public void A_dash_past_a_lone_foe_pincers_it_between_the_speedster_and_its_afterimage()
        {
            var db = WithAfterimage();
            var (s, _) = Battle(db, Stage(("tank", 2, 3)), ("knight", 1, 3), ("cleric", 5, 7));
            var k = Speedster(s);
            var tank = s.Units.Single(u => u.Def.Id == "tank");
            int hp = tank.Hp;

            var ev = Dash(s, k, 3, 3);

            Assert.That(k.Pos, Is.EqualTo(new Coord(3, 3)));
            Assert.That(ev.OfType<AfterimageLeftEvent>().Single().From, Is.EqualTo(new Coord(1, 3)));
            var p = ev.OfType<PincerFormedEvent>().Single();
            Assert.That((p.Attacker1, p.Attacker2), Is.EqualTo((k.Id, k.Id)), "pincers from one hero");
            Assert.That(p.Afterimage1 || p.Afterimage2, Is.True);
            Assert.That(new[] { p.Attacker1At.At, p.Attacker2At.At }, Is.EquivalentTo(new[] { new Coord(1, 3), new Coord(3, 3) }));
            Assert.That(p.GeometryError(), Is.Null);
            Assert.That(tank.Hp, Is.LessThan(hp));
            Assert.That(ev.OfType<ComboChangedEvent>().Any(c => c.ContributorId == k.Id && c.Label == "Afterimage"), Is.True, "the afterimage's credit is the Speedster's");

            // The action is over: the afterimage still stands (to draw foes) but no longer closes pincers.
            Assert.That(k.AfterimageAt, Is.EqualTo(new Coord(1, 3)));
            Assert.That(k.AfterimagePincers, Is.False);
            Assert.That(PincerDetector.Detect(s, Team.Hero, k).Any(x => x.AAfterimage.HasValue || x.BAfterimage.HasValue), Is.False);
        }

        [Test]
        public void The_afterimage_closes_a_line_with_another_hero()
        {
            var db = WithAfterimage();
            var (s, _) = Battle(db, Stage(("tank", 1, 1), ("tank", 1, 2)), ("knight", 1, 3), ("cleric", 1, 0));
            var k = Speedster(s);

            var ev = Dash(s, k, 3, 3);

            var p = ev.OfType<PincerFormedEvent>().Single();
            Assert.That(new[] { p.Attacker1, p.Attacker2 }, Is.EquivalentTo(new[] { k.Id, Hero(s, "cleric").Id }));
            Assert.That(p.Opponents.Count, Is.EqualTo(2));
            var ghostEnd = p.Afterimage1 ? p.Attacker1At : p.Attacker2At;
            Assert.That(ghostEnd.At, Is.EqualTo(new Coord(1, 3)));
        }

        [Test]
        public void A_foe_in_reach_strikes_the_afterimage_instead_of_a_hero()
        {
            var db = WithAfterimage();
            var (s, _) = Battle(db, Stage(("wolf", 2, 3)), ("knight", 0, 0), ("cleric", 5, 7));
            var k = Speedster(s);
            k.AfterimageAt = new Coord(2, 5);
            var wolf = s.Units.Single(u => u.Def.Id == "wolf");
            ActsAfterAction(s, wolf);
            int kHp = k.Hp, cHp = Hero(s, "cleric").Hp;

            var ev = Turn(s, new MoveCommand { HeroId = Hero(s, "cleric").Id, Path = P((5, 6)) });

            var faded = ev.OfType<AfterimageFadedEvent>().First();
            Assert.That((faded.Reason, faded.ById, faded.At), Is.EqualTo(("struck", wolf.Id, new Coord(2, 5))));
            Assert.That(wolf.Pos.Manhattan(new Coord(2, 5)), Is.EqualTo(1));
            Assert.That(k.AfterimageAt, Is.Null);
            Assert.That((k.Hp, Hero(s, "cleric").Hp), Is.EqualTo((kHp, cHp)), "the blow was spent on the afterimage");
        }

        [Test]
        public void The_afterimage_fades_when_the_party_turn_comes_round()
        {
            var db = WithAfterimage();
            var (s, _) = Battle(db, Stage(("tank", 5, 0)), ("knight", 0, 0), ("cleric", 5, 7));
            var k = Speedster(s);
            k.AfterimageAt = new Coord(0, 4);

            var ev = Turn(s, new MoveCommand { HeroId = Hero(s, "cleric").Id, Path = P((5, 6)) });

            Assert.That(ev.OfType<AfterimageFadedEvent>().Single().Reason, Is.EqualTo("faded"));
            Assert.That(k.AfterimageAt, Is.Null);
        }

        [Test]
        public void The_bot_values_only_a_dash_whose_afterimage_closes_a_pincer()
        {
            var db = WithAfterimage();
            var (s, _) = Battle(db, Stage(("tank", 2, 3)), ("knight", 1, 3), ("cleric", 5, 7));
            var k = Speedster(s);
            Assert.That(Afterimage.Value(s, k, new Coord(3, 3)), Is.GreaterThan(0f), "past the foe");
            Assert.That(Afterimage.Value(s, k, new Coord(1, 5)), Is.EqualTo(0f), "a dash that pincers nothing is superfluous");
            Assert.That(k.Pos, Is.EqualTo(new Coord(1, 3)), "valuing a dash never moves anyone");
        }
    }
}
