using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>Float (§7, GG-T872): the Drifter's spell lays [Floating] on the picked unit, ally or foe (never redirected
    /// onto the caster); a floating Heavy foe can be moved; the hero bot floats a Heavy foe a ready mover can then set into
    /// a pincer, or a hero whose way in crosses ground hazards, and nothing else.</summary>
    public class FloatSpellTests
    {
        private const string Id = "drifter_float";

        /// <summary>Mini plus the shipped Float, Shove and Floating; the tank is Heavy; ground states for the hazard tests.</summary>
        private static ContentDb Db(bool heavyTank = true)
        {
            var db = Mini();
            db.Abilities[Id] = Real.Ability(Id);
            db.Abilities["shove"] = Real.Ability("shove");
            db.Buffs[Unit.FloatingBuffId] = Real.Buffs[Unit.FloatingBuffId];
            db.Classes["witch"].ChargeAbility = null;
            db.Classes["tank"].Heavy = heavyTank;
            db.TileStates["mine"] = new TileStateDef { Id = "mine", Hazard = true };
            db.TileStates["flames"] = new TileStateDef { Id = "flames", Hazard = true, Tall = true };
            db.TileStates["hole"] = new TileStateDef { Id = "hole", Blocks = true, Pit = true, FallDamage = 0.2f };
            return db;
        }

        private static Unit Enemy(BattleState s, string cls) => s.Units.First(u => u.Def.Id == cls && u.Alive);

        private static List<BattleEvent> Cast(BattleState s, Unit caster, Coord at)
        {
            caster.Bar[0] = new AbilitySlot { AbilityId = Id };
            GrantMana(s, s.Content.TryAbility(Id).Cost);
            caster.ActedThisWindow = false;
            caster.Cooldowns.Clear();
            return BattleEngine.Apply(s, new UseSlotCommand { HeroId = caster.Id, Slot = 0, Target = at });
        }

        private static void GiveShove(Unit h)
        {
            h.Bar[1] = new AbilitySlot { AbilityId = "shove" };
            h.Cooldowns.Clear();
        }

        private static void Paint(BattleState s, Coord c, string state)
        {
            var d = s.Content.TryTileState(state);
            s.Region.Tiles[c] = new TileMark { State = state, Blocks = d.Blocks, Hazard = d.Hazard };
        }

        [Test]
        public void Float_ships_as_the_drifters_any_target_floating_spell()
        {
            var a = Real.Ability(Id);
            Assert.That(a.Kind, Is.EqualTo(AbilityKind.Spell));
            Assert.That((a.Shape, a.Mode, a.Filter), Is.EqualTo((TargetShape.SingleActor, TargetMode.PickActor, TargetFilter.Any)));
            Assert.That(a.ApplyBuff, Is.EqualTo(Unit.FloatingBuffId));
            Assert.That(a.BaseDamage, Is.EqualTo(0));
            Assert.That(FloatSpell.Is(a), Is.True);
            Assert.That(FloatSpell.Is(Real.Ability("quicken")), Is.False);
            Assert.That(Real.Buffs[Unit.FloatingBuffId].Duration, Is.EqualTo(2), "floats for 2 turns");
            Assert.That(Real.Classes["drifter"].Abilities, Does.Contain(Id));
            var problems = ContentLoader.Validate(Real).Where(p => p.Contains(Id)).ToList();
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void Float_lifts_the_picked_foe_itself_and_a_floating_heavy_foe_can_be_shoved()
        {
            var (s, _) = Battle(Db(), Stage(("tank", 2, 3), ("witch", 5, 0)), ("knight", 2, 4), ("mage", 5, 7));
            var tank = Enemy(s, "tank");
            var mage = Hero(s, "mage");
            Assert.That(tank.IsHeavy, Is.True);
            var ev = Cast(s, mage, tank.Pos);
            Assert.That(tank.IsFloating, Is.True, "the foe floats");
            Assert.That(mage.IsFloating, Is.False, "the boon is not redirected onto the caster");
            Assert.That(tank.IsHeavy, Is.False, "Heavy no longer anchors it");
            Assert.That(tank.WontBudge, Is.False);
            Assert.That(ev.OfType<BuffAppliedEvent>().Any(b => b.UnitId == tank.Id && b.BuffId == Unit.FloatingBuffId), Is.True);
            AssertInvariants(s);
        }

        [Test]
        public void Float_on_a_hero_lets_it_walk_over_a_mine()
        {
            var (s, _) = Battle(Db(), Stage(("wolf", 2, 1)), ("knight", 2, 6), ("mage", 5, 7));
            Paint(s, new Coord(2, 5), "mine");
            var knight = Hero(s, "knight");
            Assert.That(RegionBoard.DangerAt(s, new Coord(2, 5), knight), Is.GreaterThan(0f));
            Cast(s, Hero(s, "mage"), knight.Pos);
            Assert.That(knight.IsFloating, Is.True);
            Assert.That(RegionBoard.DangerAt(s, new Coord(2, 5), knight), Is.EqualTo(0f), "it hovers over the mine");
        }

        /// <summary>Cleric above and knight below column 2; a Heavy tank stands one tile off the line at (3,2).</summary>
        private static BattleState OffLine(bool heavy = true)
        {
            var (s, _) = Battle(Db(heavy), Stage(("tank", 3, 2), ("witch", 5, 0)), ("knight", 2, 5), ("cleric", 2, 1), ("mage", 5, 7));
            return s;
        }

        [Test]
        public void The_bot_floats_a_heavy_foe_one_move_from_a_pincer_only_while_a_mover_is_ready()
        {
            var s = OffLine();
            var tank = Enemy(s, "tank");
            Assert.That(FloatSpell.FoeValue(s, tank), Is.EqualTo(0f), "no hero holds a shove: floating it buys nothing");
            GiveShove(Hero(s, "knight"));
            Assert.That(FloatSpell.MoverReady(s), Is.True);
            Assert.That(FloatSpell.FoeValue(s, tank), Is.GreaterThanOrEqualTo(FloatSpell.TrappedValue),
                "one tile left puts it beside the cleric, in the knight's next pincer");
            Assert.That(tank.Pos, Is.EqualTo(new Coord(3, 2)), "looking moves nothing");
            Assert.That(tank.IsFloating, Is.False, "looking lifts nothing");
            var mage = Hero(s, "mage");
            Assert.That(FloatSpell.Value(s, mage, tank), Is.EqualTo(FloatSpell.FoeValue(s, tank)));
        }

        [Test]
        public void The_bot_never_floats_a_foe_that_isnt_held_by_heavy()
        {
            var s = OffLine(heavy: false);
            GiveShove(Hero(s, "knight"));
            Assert.That(FloatSpell.FoeValue(s, Enemy(s, "tank")), Is.EqualTo(0f), "a light foe can be shoved already");
            Assert.That(FloatSpell.FoeValue(s, Enemy(s, "witch")), Is.EqualTo(0f));
        }

        [Test]
        public void The_bot_floats_a_hero_whose_way_in_crosses_a_mine_or_a_hole_but_not_tall_flames()
        {
            var (s, _) = Battle(Db(), Stage(("wolf", 2, 1)), ("knight", 2, 6), ("mage", 5, 7));
            var knight = Hero(s, "knight");
            Assert.That(FloatSpell.AllyValue(s, knight), Is.EqualTo(0f), "a clear way in");

            Paint(s, new Coord(2, 5), "mine");
            Assert.That(FloatSpell.AllyValue(s, knight), Is.EqualTo(FloatSpell.HazardStepValue));
            Assert.That(knight.IsFloating, Is.False, "looking lifts nothing");

            Paint(s, new Coord(2, 5), "hole");
            Assert.That(FloatSpell.AllyValue(s, knight), Is.EqualTo(FloatSpell.HoleStepValue));

            Paint(s, new Coord(2, 5), "flames");
            Assert.That(FloatSpell.AllyValue(s, knight), Is.EqualTo(0f), "a floater still burns in tall flames");

            s.Region.Tiles.Remove(new Coord(2, 5));
            Paint(s, new Coord(0, 6), "mine");
            Assert.That(FloatSpell.AllyValue(s, knight), Is.EqualTo(0f), "a mine off its way in is no reason");
        }
    }
}
