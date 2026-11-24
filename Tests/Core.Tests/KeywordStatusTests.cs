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

        // ── Rooted (GG-T856 data, GG-T912 engine, GG-T920 content and AI) ──

        private static ContentDb WithRoot()
        {
            var db = Mini();
            db.Buffs["rooted"] = Real.Buffs["rooted"];
            return db;
        }

        private static void Root(Unit u) =>
            u.Buffs.Add(new BuffInstance { Def = Real.Buffs["rooted"], Remaining = 2 });

        private static bool RootedBlock(System.Collections.Generic.List<BattleEvent> ev) =>
            ev.OfType<StepBlockedEvent>().Any(b => b.Reason == "rooted");

        private static (BattleState s, Unit k) WithMove(AbilityDef a, StageDef stage)
        {
            var db = WithRoot();
            a.Kind = AbilityKind.Skill; a.Name = a.Id;
            db.Abilities[a.Id] = a;
            var (s, _) = Battle(db, stage, ("knight", 2, 4), ("cleric", 5, 7));
            var k = Hero(s, "knight");
            k.Bar[0] = new AbilitySlot { AbilityId = a.Id };
            return (s, k);
        }

        private static System.Collections.Generic.List<BattleEvent> Use(BattleState s, Unit hero, Coord target) =>
            BattleEngine.Apply(s, new UseSlotCommand { HeroId = hero.Id, Slot = 0, Target = target });

        private static AbilityDef Shove(int knock, int pull, bool swap) => new AbilityDef
        {
            Id = "shove", BaseDamage = 1, Knockback = knock, Pull = pull, SwapsPlaces = swap, AdjacentOnly = knock > 0 || swap,
            Shape = TargetShape.SingleActor, Mode = TargetMode.PickActor, Filter = TargetFilter.EnemyOnly,
        };

        [Test]
        public void Rooted_is_a_two_turn_refreshing_debuff_with_a_glossary_entry()
        {
            var d = Real.Buffs["rooted"];
            Assert.That(d.Roots, Is.True);
            Assert.That(d.Immobile, Is.False, "rooted still acts");
            Assert.That(d.Kind, Is.EqualTo(BuffKind.Debuff));
            Assert.That(d.Duration, Is.EqualTo(2));
            Assert.That(d.Stacking, Is.EqualTo(BuffStacking.Refresh));
            Assert.That(Real.TryTerm("Rooted"), Is.Not.Null);
        }

        [Test]
        public void A_rooted_hero_cannot_take_a_drag_step_but_a_free_one_can()
        {
            var (s, _) = Battle(WithRoot(), Stage(("tank", 5, 0)), ("knight", 2, 4), ("cleric", 5, 7));
            var k = Hero(s, "knight");
            Root(k);
            Assert.That(k.IsRooted, Is.True);
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = k.Id, Path = P((2, 3)) });
            Assert.That(RootedBlock(ev), Is.True);
            Assert.That(k.Pos, Is.EqualTo(new Coord(2, 4)));
            k.Buffs.Clear();
            BattleEngine.Apply(s, new MoveCommand { HeroId = k.Id, Path = P((2, 3)) });
            Assert.That(k.Pos, Is.EqualTo(new Coord(2, 3)));
        }

        [Test]
        public void A_drag_into_a_rooted_occupant_is_refused_and_neither_unit_moves()
        {
            var (s, _) = Battle(WithRoot(), Stage(("tank", 2, 3)), ("knight", 2, 4), ("cleric", 5, 7));
            var tank = s.Units.First(u => u.Def.Id == "tank");
            Root(tank);
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = Hero(s, "knight").Id, Path = P((2, 3)) });
            Assert.That(RootedBlock(ev), Is.True);
            Assert.That(tank.Pos, Is.EqualTo(new Coord(2, 3)));
            Assert.That(Hero(s, "knight").Pos, Is.EqualTo(new Coord(2, 4)));
        }

        [Test]
        public void Knockback_pull_and_swap_leave_a_rooted_target_on_its_tile()
        {
            var (s, k) = WithMove(Shove(2, 0, false), Stage(("tank", 2, 3)));
            var tank = s.Units.First(u => u.Def.Id == "tank");
            Root(tank);
            Assert.That(RootedBlock(Use(s, k, new Coord(2, 3))), Is.True);
            Assert.That(tank.Pos, Is.EqualTo(new Coord(2, 3)), "knockback");

            (s, k) = WithMove(Shove(0, 2, false), Stage(("tank", 2, 0)));
            tank = s.Units.First(u => u.Def.Id == "tank");
            Root(tank);
            Assert.That(RootedBlock(Use(s, k, new Coord(2, 0))), Is.True);
            Assert.That(tank.Pos, Is.EqualTo(new Coord(2, 0)), "pull");

            (s, k) = WithMove(Shove(0, 0, true), Stage(("tank", 2, 3)));
            tank = s.Units.First(u => u.Def.Id == "tank");
            Root(tank);
            Assert.That(RootedBlock(Use(s, k, new Coord(2, 3))), Is.True);
            Assert.That(tank.Pos, Is.EqualTo(new Coord(2, 3)), "swap, target half");
            Assert.That(k.Pos, Is.EqualTo(new Coord(2, 4)));
        }

        [Test]
        public void A_rooted_caster_cannot_swap_step_back_or_teleport_away()
        {
            var (s, k) = WithMove(Shove(0, 0, true), Stage(("tank", 2, 3)));
            Root(k);
            Assert.That(RootedBlock(Use(s, k, new Coord(2, 3))), Is.True);
            Assert.That(k.Pos, Is.EqualTo(new Coord(2, 4)), "swap, caster half");

            (s, k) = WithMove(new AbilityDef { Id = "backstep", StepBack = 2, Shape = TargetShape.SingleActor, Mode = TargetMode.Auto, Filter = TargetFilter.Any }, Stage(("tank", 2, 2)));
            Root(k);
            Use(s, k, k.Pos);
            Assert.That(k.Pos, Is.EqualTo(new Coord(2, 4)), "step back");

            var (s2, _) = Battle(WithRoot(), Stage(("tank", 5, 0)), ("knight", 2, 4), ("cleric", 5, 7));
            var k2 = Hero(s2, "knight");
            k2.Bar[0] = new AbilitySlot { AbilityId = "teleport" };
            Root(k2);
            Assert.That(RootedBlock(Use(s2, k2, new Coord(0, 4))), Is.True);
            Assert.That(k2.Pos, Is.EqualTo(new Coord(2, 4)), "blink");
        }

        [Test]
        public void A_rooted_enemy_holds_its_tile_on_its_own_turn()
        {
            var db = WithRoot();
            db.Classes["wolf"].MoveRange = 2;
            var (s, _) = Battle(db, Stage(("wolf", 0, 0)), ("knight", 4, 6), ("cleric", 5, 7));
            var wolf = s.Units.First(u => u.Def.Id == "wolf");
            Root(wolf);
            s.TurnEntryOf(wolf.Id).Ip = BattleConfig.BarLength - 1;
            var ev = BattleEngine.Apply(s, new ShieldCommand());
            Assert.That(wolf.Pos, Is.EqualTo(new Coord(0, 0)));
            Assert.That(ev.OfType<SteppedEvent>().Any(e => e.UnitId == wolf.Id), Is.False);
        }

        [Test]
        public void The_planner_holds_a_rooted_foe_and_never_swaps_with_one()
        {
            var (s, _) = Battle(WithRoot(), Stage(("wolf", 1, 1), ("tank", 3, 3)), ("knight", 0, 7));
            var wolf = s.Units.First(u => u.Def.Id == "wolf");
            var tank = s.Units.First(u => u.Def.Id == "tank");
            Assert.That(PincerPlanner.Swappable(tank, Hero(s, "knight")), Is.True);
            Root(tank);
            Assert.That(PincerPlanner.Swappable(tank, Hero(s, "knight")), Is.False);
            Assert.That(EnemyPlanner.PlanStep(s, wolf), Is.Not.EqualTo(wolf.Pos), "free, it advances");
            Root(wolf);
            Assert.That(EnemyPlanner.PlanStep(s, wolf), Is.EqualTo(wolf.Pos));
            Assert.That(EnemyPlanner.PlanRetreat(s, wolf), Is.EqualTo(wolf.Pos));
        }
    }
}
