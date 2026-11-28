using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Core.Rules;
using GridGame.Meta;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>Magnetize (§7, GG-T878): the Tinkerer's spell drags every plated or Tech-tagged foe within 3 of the picked
    /// tile up to 2 tiles toward it, nearest first, and swings their plates to face it; Heavy foes resist unless Floating,
    /// Rooted ones hold, and both still turn their plates. HeroBot only pays for a pull that lines two or more up.</summary>
    public class MagnetizeTests
    {
        private const string Id = "tinkerer_magnetize";

        /// <summary>Mini plus the shipped Magnetize, a plated goblin, a Tech wolf (no plate), floating and rooted buffs.</summary>
        private static ContentDb WithMagnet(bool heavyGoblin = false)
        {
            var db = Mini();
            db.Abilities[Id] = Real.Ability(Id);
            var gob = db.Classes["goblin"];
            gob.ArmorPlate = 30;
            gob.DropTable = null;
            gob.Heavy = heavyGoblin;
            db.Classes["wolf"].Tags.Add(Magnetize.TechTag);
            db.Classes["wolf"].MoveRange = 0;
            db.Buffs["floating"] = new BuffDef { Id = "floating", Name = "Floating", Kind = BuffKind.Buff, Unit = DurationUnit.Turns, Duration = 3 };
            db.Buffs["rooted"] = new BuffDef { Id = "rooted", Name = "Rooted", Unit = DurationUnit.Turns, Duration = 3, Roots = true };
            return db;
        }

        private static List<BattleEvent> Cast(BattleState s, Unit caster, Coord tile)
        {
            caster.Bar[0] = new AbilitySlot { AbilityId = Id };
            GrantMana(s, s.Content.Abilities[Id].Cost);
            caster.ActedThisWindow = false; // one action a window (GG-T308); these tests are about the spell
            return BattleEngine.Apply(s, new UseSlotCommand { HeroId = caster.Id, Slot = 0, Target = tile });
        }

        /// <summary>The first facing the cast gave <paramref name="u"/> (an enemy turn after it may re-face it).</summary>
        private static Coord? SwungTo(List<BattleEvent> ev, Unit u)
        {
            int at = ev.FindIndex(e => e is MagnetizedEvent);
            for (int i = at + 1; i < ev.Count; i++)
                if (ev[i] is FacingChangedEvent f && f.UnitId == u.Id) return f.Facing;
            return null;
        }

        [Test]
        public void Magnetize_ships_as_the_tinkerers_instant_pick_tile_spell()
        {
            var a = Real.Ability(Id);
            Assert.That(a.Kind, Is.EqualTo(AbilityKind.Spell));
            Assert.That(a.Mode, Is.EqualTo(TargetMode.PickTile));
            Assert.That(a.Filter, Is.EqualTo(TargetFilter.EnemyOnly));
            Assert.That(a.Radius, Is.EqualTo(3));
            Assert.That(a.Magnetizes, Is.EqualTo(2));
            Assert.That(a.CastTime, Is.EqualTo(0f));
            Assert.That(Real.Classes["tinkerer"].Abilities, Does.Contain(Id));
            Assert.That(Abilities.HasEffect(a), Is.True);
            Assert.That(Abilities.MechanicKey(a), Does.Contain("magnetize"));
        }

        [Test]
        public void Two_plated_foes_are_pulled_into_a_line_on_the_tile_and_turn_their_plates_to_it()
        {
            var (s, _) = Battle(WithMagnet(), Stage(("goblin", 2, 0), ("goblin", 2, 5)), ("mage", 0, 7), ("knight", 5, 7));
            var gobs = s.Units.Where(u => u.Def.Id == "goblin").OrderBy(u => u.Pos.Y).ToList();
            Unit top = gobs[0], bottom = gobs[1];
            top.Facing = new Coord(1, 0);
            bottom.Facing = new Coord(1, 0);

            var ev = Cast(s, Hero(s, "mage"), new Coord(2, 3));

            var mz = ev.OfType<MagnetizedEvent>().Single();
            Assert.That(mz.UnitIds, Is.EqualTo(new[] { bottom.Id, top.Id }), "nearest the tile first (2 vs 3), then by id");
            Assert.That(bottom.Pos, Is.EqualTo(new Coord(2, 3)), "the nearer foe is dragged onto the tile");
            Assert.That(top.Pos, Is.EqualTo(new Coord(2, 2)), "the farther one stops 2 tiles on, right behind it");
            Assert.That(SwungTo(ev, top), Is.EqualTo(new Coord(0, 1)), "its plate swings down to face the tile");
            Assert.That(ev.OfType<DisplacedEvent>().Count(d => d.UnitId == top.Id), Is.EqualTo(2));
        }

        [Test]
        public void A_tech_foe_is_gripped_but_an_unplated_beast_is_not()
        {
            var (s, _) = Battle(WithMagnet(), Stage(("wolf", 0, 1), ("tank", 4, 1)), ("mage", 0, 7), ("knight", 5, 7));
            var wolf = s.Units.First(u => u.Def.Id == "wolf");
            var tank = s.Units.First(u => u.Def.Id == "tank");
            var a = s.Content.Abilities[Id];
            Assert.That(TargetResolver.IsValidPick(s, a, Hero(s, "mage"), new Coord(5, 1)), Is.False, "only the soft tank is near: nothing to grip");

            Cast(s, Hero(s, "mage"), new Coord(2, 1));

            Assert.That(wolf.Pos, Is.EqualTo(new Coord(2, 1)));
            Assert.That(tank.Pos, Is.EqualTo(new Coord(4, 1)), "no plate, no Tech tag: the field passes it by");
        }

        [Test]
        public void A_heavy_foe_resists_unless_floating_and_a_rooted_one_holds_but_both_turn_their_plates()
        {
            var (s, _) = Battle(WithMagnet(heavyGoblin: true), Stage(("goblin", 0, 2), ("goblin", 5, 2)), ("mage", 0, 7), ("knight", 5, 7));
            var left = s.Units.First(u => u.Def.Id == "goblin" && u.Pos.X == 0);
            var right = s.Units.First(u => u.Def.Id == "goblin" && u.Pos.X == 5);
            left.Facing = new Coord(0, 1);
            right.Facing = new Coord(0, 1);

            var ev = Cast(s, Hero(s, "mage"), new Coord(2, 2));

            Assert.That(left.Pos, Is.EqualTo(new Coord(0, 2)), "Heavy: too heavy to drag");
            Assert.That(right.Pos, Is.EqualTo(new Coord(5, 2)));
            Assert.That(ev.OfType<AnnounceEvent>().Count(e => e.Text == "Too heavy!"), Is.EqualTo(2));
            Assert.That(SwungTo(ev, left), Is.EqualTo(new Coord(1, 0)), "the plate still swings to the tile");
            Assert.That(SwungTo(ev, right), Is.EqualTo(new Coord(-1, 0)));

            // Floating lifts the Heavy one; a Rooted one holds and still turns.
            left.Buffs.Add(new BuffInstance { Def = s.Content.Buffs["floating"], Remaining = 3 });
            right.Buffs.Add(new BuffInstance { Def = s.Content.Buffs["floating"], Remaining = 3 });
            right.Buffs.Add(new BuffInstance { Def = s.Content.Buffs["rooted"], Remaining = 3 });
            right.Facing = new Coord(0, 1);
            var mage = Hero(s, "mage");
            mage.Cooldowns.Clear();

            ev = Cast(s, mage, new Coord(2, 2));

            Assert.That(left.Pos, Is.EqualTo(new Coord(2, 2)), "floating, it is dragged 2 tiles onto the tile");
            Assert.That(right.Pos, Is.EqualTo(new Coord(5, 2)), "rooted, it holds");
            Assert.That(ev.OfType<StepBlockedEvent>().Any(b => b.UnitId == right.Id && b.Reason == "rooted"), Is.True);
            Assert.That(SwungTo(ev, right), Is.EqualTo(new Coord(-1, 0)));
        }

        [Test]
        public void Herobot_values_a_pull_only_when_two_or_more_foes_would_line_up()
        {
            var db = WithMagnet();
            var a = db.Abilities[Id];
            var (s, _) = Battle(db, Stage(("goblin", 2, 0), ("goblin", 2, 5)), ("mage", 0, 7), ("knight", 5, 7));
            var mage = Hero(s, "mage");
            var before = s.Units.Select(u => (u.Id, u.Pos, u.Facing)).ToList();
            Assert.That(Magnetize.Value(s, mage, a, new Coord(2, 3)), Is.GreaterThan(0f), "the two line up in column 2");
            Assert.That(s.Units.Select(u => (u.Id, u.Pos, u.Facing)).ToList(), Is.EqualTo(before), "valuing it moves nobody");

            var (lone, _) = Battle(db, Stage(("goblin", 2, 1), ("tank", 4, 4)), ("mage", 0, 7), ("knight", 5, 7));
            Assert.That(Magnetize.Value(lone, Hero(lone, "mage"), a, new Coord(2, 3)), Is.EqualTo(0f), "a lone metal foe makes no line");

            var (lined, _) = Battle(db, Stage(("goblin", 2, 2), ("goblin", 2, 3)), ("mage", 0, 7), ("knight", 5, 7));
            Assert.That(Magnetize.Value(lined, Hero(lined, "mage"), a, new Coord(2, 3)), Is.EqualTo(0f), "the line already stands");
        }
    }
}
