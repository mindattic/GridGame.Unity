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
    /// <summary>Heavy keyword (GG-T891 Core, GG-T914 content; BIBLE 1.1.3, 14.2): a Heavy class is never displaced by anyone
    /// else unless it carries the "floating" buff.</summary>
    public class HeavyTests
    {
        private static readonly string[] HeavyIds =
        {
            "heavytrooper02", "mechaarmor00", "mechaarmor01", "mechaarmor02", "mountaintroll", "crystalbrute",
            "anchorhulk", "ironbarkgolem", "minotaur02", "treegolem00", "treegolem01", "treegolem02", "treegolem03", "treegolem04", "treegolem06",
        };

        private static ContentDb HeavyDb()
        {
            var db = Mini();
            db.Classes["bulwark"] = new ClassDef { Id = "bulwark", Name = "Bulwark", Team = Team.Enemy, Tags = { "Humanoid" }, Heavy = true,
                BaseStats = new Stats { Strength = 1, Vitality = 200, Agility = 10, Speed = 1, Intelligence = 10 }, MoveRange = 0 };
            db.Buffs["floating"] = new BuffDef { Id = "floating", Name = "Floating", Unit = DurationUnit.Turns, Duration = 3 };
            return db;
        }

        private static (BattleState s, Unit hero) WithSkill(AbilityDef a, StageDef stage)
        {
            var db = HeavyDb();
            a.Kind = AbilityKind.Skill;
            a.Name = a.Id;
            db.Abilities[a.Id] = a;
            var (s, _) = Battle(db, stage, ("knight", 2, 4), ("cleric", 5, 7));
            var k = Hero(s, "knight");
            k.Bar[0] = new AbilitySlot { AbilityId = a.Id };
            return (s, k);
        }

        private static List<BattleEvent> Use(BattleState s, Unit hero, Coord target) =>
            BattleEngine.Apply(s, new UseSlotCommand { HeroId = hero.Id, Slot = 0, Target = target });

        private static Unit Bulwark(BattleState s) => s.Units.Single(u => u.Def.Id == "bulwark");

        private static void Float(Unit u) =>
            u.Buffs.Add(new BuffInstance { Def = new BuffDef { Id = Unit.FloatingBuffId, Name = "Floating", Unit = DurationUnit.Turns, Duration = 3 }, Remaining = 3 });

        private static AbilityDef Shove(int knock, int pull, bool swap) => new AbilityDef
        {
            Id = "shove", BaseDamage = 1, Knockback = knock, Pull = pull, SwapsPlaces = swap, AdjacentOnly = knock > 0 || swap,
            Shape = TargetShape.SingleActor, Mode = TargetMode.PickActor, Filter = TargetFilter.EnemyOnly,
        };

        private static bool TooHeavy(List<BattleEvent> ev) => ev.OfType<AnnounceEvent>().Any(a => a.Text == "Too heavy!");

        [Test]
        public void The_listed_classes_are_Heavy_and_no_others_are()
        {
            var heavy = Real.Classes.Values.Where(c => c.Heavy).Select(c => c.Id).OrderBy(x => x).ToList();
            Assert.That(heavy, Is.EqualTo(HeavyIds.OrderBy(x => x).ToList()));
            foreach (var id in HeavyIds)
                Assert.That(Real.Classes[id].Footprint.W * Real.Classes[id].Footprint.H, Is.EqualTo(1), id + " is one tile");
        }

        [Test]
        public void A_drag_step_into_a_Heavy_unit_is_refused()
        {
            var (s, _) = Battle(HeavyDb(), Stage(("bulwark", 1, 1)), ("knight", 0, 1), ("cleric", 4, 4));
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = Hero(s, "knight").Id, Path = P((1, 1)) });
            Assert.That(ev.Any(e => e is StepBlockedEvent b && b.Reason == "heavy"), Is.True);
            Assert.That(Bulwark(s).Pos, Is.EqualTo(new Coord(1, 1)));
            Assert.That(Hero(s, "knight").Pos, Is.EqualTo(new Coord(0, 1)));
        }

        [Test]
        public void A_floating_Heavy_unit_is_swapped_by_a_drag_step()
        {
            var (s, _) = Battle(HeavyDb(), Stage(("bulwark", 1, 1)), ("knight", 0, 1), ("cleric", 4, 4));
            Float(Bulwark(s));
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = Hero(s, "knight").Id, Path = P((1, 1)) });
            Assert.That(ev.Any(e => e is StepBlockedEvent b && b.Reason == "heavy"), Is.False);
            Assert.That(Hero(s, "knight").Pos, Is.EqualTo(new Coord(1, 1)));
            Assert.That(Bulwark(s).Pos, Is.EqualTo(new Coord(0, 1)));
        }

        [Test]
        public void Knockback_fizzles_on_a_Heavy_target_and_works_once_it_floats()
        {
            var (s, k) = WithSkill(Shove(2, 0, false), Stage(("bulwark", 2, 3)));
            Assert.That(TooHeavy(Use(s, k, new Coord(2, 3))), Is.True);
            Assert.That(Bulwark(s).Pos, Is.EqualTo(new Coord(2, 3)));

            (s, k) = WithSkill(Shove(2, 0, false), Stage(("bulwark", 2, 3)));
            Float(Bulwark(s));
            Use(s, k, new Coord(2, 3));
            Assert.That(Bulwark(s).Pos, Is.EqualTo(new Coord(2, 1)));
        }

        [Test]
        public void Pull_fizzles_on_a_Heavy_target_and_works_once_it_floats()
        {
            var (s, k) = WithSkill(Shove(0, 2, false), Stage(("bulwark", 2, 0)));
            Assert.That(TooHeavy(Use(s, k, new Coord(2, 0))), Is.True);
            Assert.That(Bulwark(s).Pos, Is.EqualTo(new Coord(2, 0)));

            (s, k) = WithSkill(Shove(0, 2, false), Stage(("bulwark", 2, 0)));
            Float(Bulwark(s));
            Use(s, k, new Coord(2, 0));
            Assert.That(Bulwark(s).Pos, Is.EqualTo(new Coord(2, 2)));
        }

        [Test]
        public void Swap_fizzles_on_a_Heavy_target_and_works_once_it_floats()
        {
            var (s, k) = WithSkill(Shove(0, 0, true), Stage(("bulwark", 2, 3)));
            Assert.That(TooHeavy(Use(s, k, new Coord(2, 3))), Is.True);
            Assert.That(k.Pos, Is.EqualTo(new Coord(2, 4)));
            Assert.That(Bulwark(s).Pos, Is.EqualTo(new Coord(2, 3)));

            (s, k) = WithSkill(Shove(0, 0, true), Stage(("bulwark", 2, 3)));
            Float(Bulwark(s));
            Use(s, k, new Coord(2, 3));
            Assert.That(k.Pos, Is.EqualTo(new Coord(2, 3)));
            Assert.That(Bulwark(s).Pos, Is.EqualTo(new Coord(2, 4)));
        }

        [Test]
        public void A_rule_MoveUnits_skips_a_Heavy_unit()
        {
            var db = HeavyDb();
            var hook = new RuleHookDef { On = RuleHook.BattleStart };
            hook.Effects.Add(new RuleEffectDef { Kind = RuleEffectKind.MoveUnits, Units = RuleUnits.All, Team = RuleTeam.Enemy, Move = RuleMove.Down, Amount = 2 });
            var rule = new RegionRuleDef { Id = "heavy-rule" };
            rule.Hooks.Add(hook);
            RegionRuleCatalog.Index(db, new List<RegionRuleDef> { rule });
            var st = Stage(("bulwark", 1, 0), ("tank", 3, 0));
            st.Rules.Add("heavy-rule");
            var (s, _) = Battle(db, st, ("knight", 0, 7));
            Assert.That(Bulwark(s).Pos, Is.EqualTo(new Coord(1, 0)), "the Heavy unit stays put");
            Assert.That(s.Units.Single(u => u.Def.Id == "tank").Pos, Is.EqualTo(new Coord(3, 2)), "an ordinary one is dragged");
        }

        [Test]
        public void The_planner_never_swaps_with_a_Heavy_unit_unless_it_floats()
        {
            var (s, _) = Battle(HeavyDb(), Stage(("bulwark", 1, 1), ("tank", 3, 3)), ("knight", 0, 7));
            var knight = Hero(s, "knight");
            Assert.That(PincerPlanner.Swappable(Bulwark(s), knight), Is.False);
            Assert.That(PincerPlanner.Swappable(s.Units.Single(u => u.Def.Id == "tank"), knight), Is.True);
            Float(Bulwark(s));
            Assert.That(PincerPlanner.Swappable(Bulwark(s), knight), Is.True);
        }
    }
}
