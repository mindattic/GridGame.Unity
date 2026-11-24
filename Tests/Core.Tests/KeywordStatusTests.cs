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

        // ── Rooted, part 2 (GG-T930): rule drags, holes, rule breaks, enemy walks and brains, planner and bot ──

        /// <summary>Rooted for long enough to outlast every turn a test guards through.</summary>
        private static void RootLong(Unit u) =>
            u.Buffs.Add(new BuffInstance { Def = Real.Buffs["rooted"], Remaining = 20 });

        [Test]
        public void A_rule_MoveUnits_skips_a_rooted_unit()
        {
            var db = WithRoot();
            var hook = new RuleHookDef { On = RuleHook.TurnStart };
            hook.Effects.Add(new RuleEffectDef { Kind = RuleEffectKind.MoveUnits, Units = RuleUnits.All, Team = RuleTeam.Enemy, Move = RuleMove.Down, Amount = 1 });
            var rule = new RegionRuleDef { Id = "root-rule" };
            rule.Hooks.Add(hook);
            RegionRuleCatalog.Index(db, new System.Collections.Generic.List<RegionRuleDef> { rule });
            var st = Stage(("tank", 1, 0), ("goblin", 4, 0));
            st.Rules.Add("root-rule");
            var (s, _) = Battle(db, st, ("knight", 0, 7), ("cleric", 5, 7));
            var tank = Enemy(s, "tank");
            var gob = Enemy(s, "goblin");
            var tankAt = tank.Pos;
            var gobAt = gob.Pos;
            RootLong(tank);
            var ev = BattleEngine.Apply(s, new ShieldCommand());
            Assert.That(tank.Pos, Is.EqualTo(tankAt), "the rule drag can't tear a rooted unit loose");
            Assert.That(ev.OfType<StepBlockedEvent>().Any(b => b.UnitId == tank.Id && b.Reason == "rooted"), Is.True);
            Assert.That(gob.Pos, Is.Not.EqualTo(gobAt), "a free one is dragged");
            AssertInvariants(s);
        }

        [Test]
        public void On_a_full_board_a_faller_never_swaps_with_a_rooted_unit()
        {
            // CrackingFloorTests' full board: a crumbling tile under the knight, the cleric beside it, the tank far off.
            var db = WithRoot();
            var paint = new RuleEffectDef { Kind = RuleEffectKind.MarkTiles, Area = RuleArea.Tiles, State = "crumbling" };
            paint.Tiles.Add(new Coord(0, 7));
            var rule = new RegionRuleDef
            {
                Id = "floor",
                TileStates =
                {
                    new TileStateDef { Id = "crumbling", OnMark = { new RuleEffectDef { Kind = RuleEffectKind.Schedule, Area = RuleArea.Self, Event = "cavein" } } },
                    new TileStateDef { Id = "hole", Blocks = true, Pit = true, FallDamage = 0.2f, LifeSeconds = 2f },
                },
                Events =
                {
                    new RuleEventDef
                    {
                        Id = "cavein", Seconds = 1f,
                        Effects =
                        {
                            new RuleEffectDef { Kind = RuleEffectKind.MarkTiles, Area = RuleArea.EventTiles, State = "hole" },
                            new RuleEffectDef { Kind = RuleEffectKind.ClearTiles, Area = RuleArea.EventTiles, State = "crumbling" },
                        },
                    },
                },
                Hooks = { new RuleHookDef { On = RuleHook.BattleStart, Effects = { paint } } },
            };
            RegionRuleCatalog.Index(db, new System.Collections.Generic.List<RegionRuleDef> { rule });
            var stage = Stage(("tank", 5, 0));
            stage.Rules.Add("floor");
            for (int y = 0; y < BattleConfig.Rows; y++)
                for (int x = 0; x < BattleConfig.Cols; x++)
                {
                    var c = new Coord(x, y);
                    if (c != new Coord(0, 7) && c != new Coord(1, 7) && c != new Coord(5, 0)) stage.Blocks.Add(c);
                }
            var (s, _) = Battle(db, stage, ("knight", 0, 7), ("cleric", 1, 7));
            var cleric = Hero(s, "cleric");
            RootLong(cleric);
            var all = new System.Collections.Generic.List<BattleEvent>();
            for (int i = 0; i < 40 && !s.IsOver && s.Timeline.Any(e => e.Kind == TimelineKind.Hazard); i++)
                all.AddRange(BattleEngine.Apply(s, new ShieldCommand()));
            var fell = all.OfType<FellEvent>().Single();
            Assert.That(fell.SwappedId, Is.Not.EqualTo(cleric.Id), "the rooted cleric is passed over");
            Assert.That(cleric.Pos, Is.EqualTo(new Coord(1, 7)));
            AssertInvariants(s);
        }

        private static StageDef MireBreaker(params (string cls, int x, int y)[] enemies)
        {
            var st = Stage(enemies);
            st.Waves[0].Enemies[0].RuleBreak = RuleBreak.MireSwap;
            return st;
        }

        private static void GuardTurns(BattleState s, Unit u, int turns)
        {
            for (int i = 0; i < 60 && !s.IsOver && u.TurnsTaken < turns; i++) BattleEngine.Apply(s, new ShieldCommand());
            Assert.That(u.TurnsTaken, Is.GreaterThanOrEqualTo(turns), $"{u} never reached turn {turns}");
        }

        [Test]
        public void MireSwap_holds_when_the_boss_or_its_only_partner_is_rooted()
        {
            // RuleBreakTests.MireSwap_trades_places_with_an_ally_when_a_hero_corners_it, with one side rooted.
            var (s, _) = Battle(WithRoot(), MireBreaker(("goblin", 2, 2), ("tank", 0, 0)), ("knight", 2, 3), ("cleric", 5, 7));
            var boss = s.Units.First(u => u.RuleBreak != RuleBreak.None);
            var ally = Enemy(s, "tank");
            RootLong(boss);
            GuardTurns(s, boss, 1);
            Assert.That(boss.Pos, Is.EqualTo(new Coord(2, 2)), "a rooted boss can't swap out");
            Assert.That(ally.Pos, Is.Not.EqualTo(new Coord(2, 2)));

            (s, _) = Battle(WithRoot(), MireBreaker(("goblin", 2, 2), ("tank", 0, 0)), ("knight", 2, 3), ("cleric", 5, 7));
            boss = s.Units.First(u => u.RuleBreak != RuleBreak.None);
            ally = Enemy(s, "tank");
            RootLong(ally);
            GuardTurns(s, boss, 1);
            Assert.That(boss.Pos, Is.EqualTo(new Coord(2, 2)), "its only partner is rooted: nobody to trade with");
            Assert.That(ally.Pos, Is.EqualTo(new Coord(0, 0)));
            AssertInvariants(s);
        }

        /// <summary>PincerPlannerTests.SwapBoard: knight (2,3), a Sentient soldier at (1,3) west of it, a wolf at (3,3) east
        /// of it, and a Sentient soldier mover at (3,2) above the wolf that completes the pincer by swapping with the wolf;
        /// the mover acts next.</summary>
        private static (BattleState s, Unit mover, Unit wolf) SwapBoard()
        {
            var db = WithRoot();
            var st = new Stats { Strength = 5, Vitality = 6, Agility = 10, Speed = 10, Intelligence = 10 };
            db.Classes["soldier"] = new ClassDef { Id = "soldier", Name = "Soldier", Team = Team.Enemy, Tags = { "Humanoid", "Sentient" }, BaseStats = st, MoveRange = 1 };
            var setup = new BattleSetup { Stage = Stage(("soldier", 1, 3), ("wolf", 3, 3), ("soldier", 3, 2)), Seed = 7, UnlockedSlots = 5 };
            setup.Party.Add(new HeroSpec { ClassId = "knight", Start = new Coord(2, 3) });
            setup.Party.Add(new HeroSpec { ClassId = "cleric", Start = new Coord(5, 7) });
            var s = BattleEngine.Create(db, setup, new System.Collections.Generic.List<BattleEvent>());
            var mover = s.Units.Single(u => u.Alive && u.Pos == new Coord(3, 2));
            var wolf = s.Units.Single(u => u.Alive && u.Pos == new Coord(3, 3));
            foreach (var e in s.Timeline.Where(e => e.Kind == TimelineKind.Turn)) e.Ip = 0;
            s.TurnEntryOf(mover.Id).Ip = BattleConfig.BarLength - 1;
            return (s, mover, wolf);
        }

        [Test]
        public void A_pincer_walk_never_swaps_with_a_rooted_occupant()
        {
            var (s, mover, wolf) = SwapBoard();
            RootLong(wolf);
            var ev = BattleEngine.Apply(s, new ShieldCommand());
            Assert.That(ev.OfType<DisplacedEvent>().Any(d => d.UnitId == wolf.Id), Is.False, "the rooted wolf is not swapped");
            Assert.That(wolf.Pos, Is.EqualTo(new Coord(3, 3)));
            Assert.That(mover.Pos, Is.Not.EqualTo(new Coord(3, 3)));
            AssertInvariants(s);
        }

        [Test]
        public void A_rooted_pincerer_plans_no_walk_and_stays_put()
        {
            var (s, mover, wolf) = SwapBoard();
            var free = EnemyPlanner.PlanPincer(s, mover);
            Assert.That(free, Is.Not.Null);
            Assert.That(free.Path, Is.Not.Empty, "free, it walks (and swaps) to complete the pincer");
            RootLong(mover);
            var plan = EnemyPlanner.PlanPincer(s, mover);
            Assert.That(plan == null || plan.Path.Count == 0, Is.True, "rooted, it pincers from where it stands: " + plan);
            var ev = BattleEngine.Apply(s, new ShieldCommand());
            Assert.That(mover.Pos, Is.EqualTo(new Coord(3, 2)));
            Assert.That(wolf.Pos, Is.EqualTo(new Coord(3, 3)));
            Assert.That(ev.OfType<DisplacedEvent>().Any(d => d.ByUnitId == mover.Id), Is.False);
            AssertInvariants(s);
        }

        [Test]
        public void A_rooted_hopper_does_not_leap()
        {
            var db = WithRoot();
            db.Classes["wolf"].Brain = Brain.Hopper;
            db.Classes["wolf"].MoveRange = 1;
            var (s, _) = Battle(db, Stage(("wolf", 2, 0), ("tank", 2, 1)), ("knight", 2, 4), ("cleric", 5, 7));
            var w = Enemy(s, "wolf");
            Assert.That(EnemyPlanner.PlanStep(s, w), Is.EqualTo(new Coord(2, 2)), "free, it would leap over the tank");
            RootLong(w);
            Assert.That(EnemyPlanner.PlanStep(s, w), Is.EqualTo(w.Pos));
            s.TurnEntryOf(w.Id).Ip = BattleConfig.BarLength - 1;
            var ev = BattleEngine.Apply(s, new ShieldCommand());
            Assert.That(w.Pos, Is.EqualTo(new Coord(2, 0)));
            Assert.That(ev.OfType<SteppedEvent>().Any(e => e.UnitId == w.Id), Is.False);
        }

        [TestCase(Brain.Blinker, 1)]
        [TestCase(Brain.Burrower, 2)]
        public void A_rooted_blinker_or_burrower_cannot_vanish_beside_its_prey(Brain brain, int turnsTaken)
        {
            var db = WithRoot();
            db.Classes["wolf"].Brain = brain;
            db.Classes["wolf"].MoveRange = 1;
            db.Classes["wolf"].BaseStats.Vitality = 40;
            db.Classes["wolf"].ChargeAbility = null;
            var (s, _) = Battle(db, Stage(("wolf", 0, 0)), ("knight", 4, 6), ("cleric", 5, 7));
            var w = Enemy(s, "wolf");
            RootLong(w);
            w.TurnsTaken = turnsTaken; // its next turn is a blink / burrow turn
            s.TurnEntryOf(w.Id).Ip = BattleConfig.BarLength - 1;
            var ev = BattleEngine.Apply(s, new ShieldCommand());
            Assert.That(ev.OfType<TeleportedEvent>().Any(t => t.UnitId == w.Id), Is.False);
            Assert.That(RootedBlock(ev), Is.True);
            Assert.That(w.Pos, Is.EqualTo(new Coord(0, 0)));
            AssertInvariants(s);
        }

        [Test]
        public void A_big_body_never_shoves_a_rooted_hero_aside()
        {
            // AiAndSystemsTests.Two_by_two_boss_shoves_a_hero_into_the_tile_it_vacates, with the knight rooted.
            var db = WithRoot();
            db.Classes["boss"].MoveRange = 1;
            var (s, _) = Battle(db, Stage(("boss", 0, 0)), ("knight", 2, 1), ("cleric", 5, 7));
            var boss = Enemy(s, "boss");
            var knight = Hero(s, "knight");
            RootLong(knight);
            s.TurnEntryOf(boss.Id).Ip = BattleConfig.BarLength - 1;
            var ev = BattleEngine.Apply(s, new ShieldCommand());
            Assert.That(knight.Pos, Is.EqualTo(new Coord(2, 1)), "a rooted hero holds the body off");
            Assert.That(ev.OfType<DisplacedEvent>().Any(d => d.UnitId == knight.Id), Is.False);
            Assert.That(boss.Occupies(new Coord(2, 1)), Is.False);
            AssertInvariants(s);
        }

        [Test]
        public void A_rooted_line_charger_does_not_plan_its_charge()
        {
            var db = WithRoot();
            db.Classes["witch"].ChargeShape = ChargeShape.Line;
            var (s, _) = Battle(db, Stage(("witch", 0, 0)), ("knight", 4, 0), ("cleric", 5, 7));
            var witch = Enemy(s, "witch");
            Assert.That(EnemyPlanner.PlanCast(s, witch, false), Is.Not.Null, "free, it charges down the line");
            Root(witch);
            Assert.That(EnemyPlanner.PlanCast(s, witch, false), Is.Null, "a line charge carries the caster; rooted, it can't");
        }

        private static float BotScore(BattleState s, Unit caster, AbilityDef a, Coord anchor)
        {
            var m = typeof(GridGame.Core.Bot.HeroBot).GetMethod("ScoreAbility", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.That(m, Is.Not.Null, "HeroBot.ScoreAbility");
            return (float)m.Invoke(new GridGame.Core.Bot.HeroBot(1), new object[] { s, caster, a, anchor });
        }

        [TestCase(2, 0, false)]
        [TestCase(0, 2, false)]
        [TestCase(0, 0, true)]
        public void The_bot_does_not_value_knockback_pull_or_swap_on_a_rooted_foe(int knock, int pull, bool swap)
        {
            var (s, k) = WithMove(Shove(knock, pull, swap), Stage(("wolf", 2, 3)));
            var wolf = Enemy(s, "wolf");
            var a = s.Content.Abilities["shove"];
            float free = BotScore(s, k, a, wolf.Pos);
            Root(wolf);
            float rooted = BotScore(s, k, a, wolf.Pos);
            Assert.That(rooted, Is.LessThan(free), $"free {free}, rooted {rooted}");
        }

        // ── Frightened (GG-T916 engine, GG-T917 content) ──

        private static ContentDb WithFear()
        {
            var db = Mini();
            db.Buffs["frightened"] = Real.Buffs["frightened"];
            db.Abilities["scare"] = new AbilityDef { Id = "scare", Name = "Scare", Kind = AbilityKind.Spell, Cost = 1, ApplyBuff = "frightened", Shape = TargetShape.SingleActor, Mode = TargetMode.PickActor, Filter = TargetFilter.EnemyOnly };
            return db;
        }

        private static void Frighten(Unit u, Unit source) =>
            u.Buffs.Add(new BuffInstance { Def = Real.Buffs["frightened"], Remaining = 2, SourceId = source.Id });

        private static System.Collections.Generic.List<BattleEvent> Scare(BattleState s, Unit caster, Unit target)
        {
            s.Bank.Add(1);
            caster.ActedThisWindow = false;
            caster.Bar[0] = new AbilitySlot { AbilityId = "scare" };
            return BattleEngine.Apply(s, new UseSlotCommand { HeroId = caster.Id, Slot = 0, Target = target.Pos });
        }

        private static bool Immune(System.Collections.Generic.List<BattleEvent> ev, Unit u) =>
            ev.OfType<AnnounceEvent>().Any(a => a.Text == "Immune!" && a.UnitId == u.Id);

        [Test]
        public void Frightened_is_a_two_turn_debuff_with_a_glossary_entry()
        {
            var d = Real.Buffs["frightened"];
            Assert.That(d.Frightens, Is.True);
            Assert.That(d.Kind, Is.EqualTo(BuffKind.Debuff));
            Assert.That(d.Unit, Is.EqualTo(DurationUnit.Turns));
            Assert.That(d.Duration, Is.EqualTo(2));
            Assert.That(d.Immobile, Is.False, "it runs, it isn't frozen");
            Assert.That(Real.TryTerm("Frightened"), Is.Not.Null);
            Assert.That(Real.TryTerm("Frighten"), Is.Not.Null);
            Assert.That(ContentLoader.Validate(Real).Where(p => p.Contains("frightened")), Is.Empty);
        }

        [Test]
        public void Validate_rejects_a_frightens_buff_that_is_not_a_debuff()
        {
            var db = Mini();
            db.Buffs["brave_fear"] = new BuffDef { Id = "brave_fear", Name = "Brave Fear", Kind = BuffKind.Buff, Unit = DurationUnit.Turns, Duration = 2, Frightens = true };
            var problems = string.Join("\n", ContentLoader.Validate(db));
            Assert.That(problems, Does.Contain("buff brave_fear: Frightens must be a Debuff"));
        }

        [Test]
        public void A_frightened_foe_flees_its_move_range_directly_away_then_the_fear_is_spent()
        {
            var db = WithFear();
            db.Classes["wolf"].MoveRange = 2;
            var (s, _) = Battle(db, Stage(("wolf", 2, 2)), ("knight", 2, 4), ("cleric", 5, 7));
            var wolf = Enemy(s, "wolf");
            var k = Hero(s, "knight");
            Frighten(wolf, k);
            Assert.That(wolf.IsFrightened, Is.True);
            Assert.That(wolf.FearSourceId, Is.EqualTo(k.Id));
            int hp = k.Hp;
            s.TurnEntryOf(wolf.Id).Ip = BattleConfig.BarLength - 1;
            var ev = BattleEngine.Apply(s, new ShieldCommand());

            Assert.That(wolf.Pos, Is.EqualTo(new Coord(2, 0)), "two steps straight up, away from the knight below");
            Assert.That(ev.OfType<SteppedEvent>().Count(e => e.UnitId == wolf.Id), Is.EqualTo(2));
            Assert.That(ev.OfType<AnnounceEvent>().Any(a => a.Text == "Wolf flees!"), Is.True);
            Assert.That(wolf.IsFrightened, Is.False, "the fear is spent on the flight");
            Assert.That(k.Hp, Is.EqualTo(hp), "a fleeing foe doesn't attack");
        }

        [Test]
        public void A_fleeing_foe_runs_straight_across_a_hazard()
        {
            var db = WithFear();
            db.Elements = Real.Elements;
            RegionRuleCatalog.Index(db, new System.Collections.Generic.List<RegionRuleDef> { Real.TryRegionRule("firewalls") });
            db.Classes["wolf"].MoveRange = 2;
            db.Classes["wolf"].BaseStats.Vitality = 60;
            var (s, _) = Battle(db, Stage(("wolf", 2, 2)), ("knight", 2, 4), ("cleric", 5, 7));
            s.Region.Rules.Add("firewalls");
            s.Region.Tiles[new Coord(2, 1)] = new TileMark { State = "firewall", Rule = "firewalls", Hazard = true, OwnerTeam = Team.Hero };
            var wolf = Enemy(s, "wolf");
            Frighten(wolf, Hero(s, "knight"));
            int hp = wolf.Hp;
            s.TurnEntryOf(wolf.Id).Ip = BattleConfig.BarLength - 1;
            var ev = BattleEngine.Apply(s, new ShieldCommand());

            Assert.That(ev.OfType<SteppedEvent>().Any(e => e.UnitId == wolf.Id && e.To == new Coord(2, 1)), Is.True, "panic doesn't pick its footing");
            Assert.That(wolf.Hp, Is.LessThan(hp), "and the fire bites");
            Assert.That(wolf.Pos, Is.EqualTo(new Coord(2, 0)));
        }

        [Test]
        public void Bosses_and_heavy_foes_are_immune_to_fear()
        {
            var (s, _) = Battle(WithFear(), Stage(("boss", 2, 1)), ("knight", 2, 5), ("mage", 4, 7));
            var boss = Enemy(s, "boss");
            var ev = Scare(s, Hero(s, "mage"), boss);
            Assert.That(Immune(ev, boss), Is.True);
            Assert.That(boss.IsFrightened, Is.False);

            var db = WithFear();
            db.Classes["tank"].Heavy = true;
            var (s2, _) = Battle(db, Stage(("tank", 2, 2)), ("knight", 2, 5), ("mage", 4, 7));
            var tank = Enemy(s2, "tank");
            Assert.That(tank.IsHeavy, Is.True);
            ev = Scare(s2, Hero(s2, "mage"), tank);
            Assert.That(Immune(ev, tank), Is.True);
            Assert.That(tank.IsFrightened, Is.False);

            var (s3, _) = Battle(WithFear(), Stage(("tank", 2, 2)), ("knight", 2, 5), ("mage", 4, 7));
            var plain = Enemy(s3, "tank");
            var m = Hero(s3, "mage");
            ev = Scare(s3, m, plain);
            Assert.That(Immune(ev, plain), Is.False);
            Assert.That(plain.IsFrightened, Is.True, "an ordinary foe scares");
            Assert.That(plain.FearSourceId, Is.EqualTo(m.Id), "and fears its caster");
        }

        [Test]
        public void A_frightened_hero_cannot_end_a_drag_beside_what_it_fears()
        {
            var (s, _) = Battle(WithFear(), Stage(("goblin", 2, 2)), ("knight", 2, 5), ("cleric", 5, 7));
            var k = Hero(s, "knight");
            var gob = Enemy(s, "goblin");
            Frighten(k, gob);
            Assert.That(k.IsFrightened, Is.True);

            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = k.Id, Path = P((2, 4), (2, 3)) });
            Assert.That(ev.OfType<AbilityRefusedEvent>().Any(r => r.CasterId == k.Id && r.Reason == "Too frightened"), Is.True);
            Assert.That(k.Pos, Is.EqualTo(new Coord(2, 5)), "it won't go up beside the goblin");

            ev = BattleEngine.Apply(s, new MoveCommand { HeroId = k.Id, Path = P((2, 4)) });
            Assert.That(ev.OfType<AbilityRefusedEvent>().Any(r => r.Reason == "Too frightened"), Is.False);
            Assert.That(k.Pos, Is.EqualTo(new Coord(2, 4)), "a drag that stops short of it is fine");
        }
    }
}
