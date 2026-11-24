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
    /// <summary>Tree golem Root Grasp (§14.24, GG-T904): a tree golem's landed blow roots the hero for 1 turn, its Root Snare
    /// support rolls Rooted instead of Slowed, and a pincer-planning foe prefers the set-up that pins a Rooted hero.</summary>
    public class RootGraspTests
    {
        private static readonly string[] Golems = { "treegolem00", "treegolem01", "treegolem02", "treegolem03", "treegolem04", "treegolem06" };

        [Test]
        public void Every_tree_golem_roots_on_hit_for_one_turn_and_root_snare_rolls_rooted()
        {
            var c = Real;
            Assert.That(c.TryBuff("rooted")?.Roots, Is.True);
            foreach (var id in Golems)
            {
                var d = c.Classes[id];
                Assert.That((d.OnHitBuff, d.OnHitChance, d.OnHitTurns), Is.EqualTo(("rooted", 1f, 1)), id);
            }
            var snare = c.Classes["treegolem06"].Support;
            Assert.That(snare.Label, Is.EqualTo("Root Snare"));
            Assert.That((snare.Effect, snare.Buff), Is.EqualTo((SupportEffect.Status, "rooted")), "a Rooted chance, no longer Slowed");
            Assert.That(snare.Magnitude, Is.GreaterThan(0f).And.LessThan(1f), "a chance, not a certainty");
            var problems = ContentLoader.Validate(c).Where(p => p.Contains("treegolem")).ToList();
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void On_hit_turns_must_name_a_buff_and_never_go_negative()
        {
            var db = Mini();
            db.Classes["wolf"].OnHitTurns = 1;
            Assert.That(ContentLoader.Validate(db).Any(p => p.Contains("class wolf: OnHitTurns")), Is.True, "no OnHitBuff to time");
            db.Classes["goblin"].OnHitTurns = -1;
            Assert.That(ContentLoader.Validate(db).Any(p => p.Contains("class goblin: OnHitTurns")), Is.True, "negative");
        }

        /// <summary>A rooting wolf (MoveRange 0) beside the knight gets up to four turns; every rooted application it lands.</summary>
        private static (Unit knight, List<BuffAppliedEvent> roots) Bite(int onHitTurns)
        {
            var db = Mini();
            db.Buffs["rooted"] = Real.TryBuff("rooted");
            var w = db.Classes["wolf"];
            w.MoveRange = 0;
            w.BaseStats.Luck = 0;
            w.OnHitBuff = "rooted";
            w.OnHitChance = 1f;
            w.OnHitTurns = onHitTurns;
            var setup = new BattleSetup { Stage = Stage(("wolf", 2, 2)), Seed = 3, UnlockedSlots = 2 };
            setup.Party.Add(new HeroSpec { ClassId = "knight", Start = new Coord(2, 3) });
            setup.Party.Add(new HeroSpec { ClassId = "cleric", Start = new Coord(5, 7) });
            var all = new List<BattleEvent>();
            var s = BattleEngine.Create(db, setup, all);
            var k = Hero(s, "knight");
            var wolf = s.Units.First(u => u.Def.Id == "wolf");
            for (int i = 0; i < 4 && wolf.Alive && k.Alive && !all.OfType<BuffAppliedEvent>().Any(b => b.BuffId == "rooted"); i++)
            {
                s.TurnEntryOf(wolf.Id).Ip = BattleConfig.BarLength - 1;
                all.AddRange(BattleEngine.Apply(s, new ShieldCommand()));
            }
            return (k, all.OfType<BuffAppliedEvent>().Where(b => b.BuffId == "rooted").ToList());
        }

        [Test]
        public void A_landed_blow_roots_the_hero_for_its_on_hit_turns()
        {
            var (k, roots) = Bite(1);
            Assert.That(roots, Is.Not.Empty, "the blow landed and rooted");
            Assert.That(roots[0].UnitId, Is.EqualTo(k.Id));
            Assert.That(roots[0].Duration, Is.EqualTo(1), "Root Grasp: one turn, not the buff's own two");

            var (_, plain) = Bite(0);
            Assert.That(plain, Is.Not.Empty);
            Assert.That(plain[0].Duration, Is.EqualTo(Real.TryBuff("rooted").Duration), "OnHitTurns 0: the buff's own duration");
        }

        /// <summary>Column 2: knight (2,1), the planning soldier (2,3), cleric (2,5). One step north sets up the knight
        /// (far end (2,0), the soldier at (1,0) closes it); one step south the cleric (far end (2,6), the soldier at (1,6)).</summary>
        private static BattleState TwoSetups()
        {
            var db = Mini();
            db.Buffs["rooted"] = Real.TryBuff("rooted");
            var st = new Stats { Strength = 5, Vitality = 6, Agility = 10, Speed = 10, Intelligence = 10 };
            db.Classes["soldier"] = new ClassDef { Id = "soldier", Name = "Soldier", Team = Team.Enemy, Tags = { "Humanoid", "Sentient" }, BaseStats = st, MoveRange = 1 };
            var setup = new BattleSetup { Stage = Stage(("soldier", 2, 3), ("soldier", 1, 0), ("soldier", 1, 6)), Seed = 7, UnlockedSlots = 5 };
            setup.Party.Add(new HeroSpec { ClassId = "knight", Start = new Coord(2, 1) });
            setup.Party.Add(new HeroSpec { ClassId = "cleric", Start = new Coord(2, 5) });
            return BattleEngine.Create(db, setup, new List<BattleEvent>());
        }

        private static void Root(BattleState s, Unit u)
        {
            var d = s.Content.TryBuff("rooted");
            u.Buffs.Add(new BuffInstance { Def = d, Remaining = 1 });
        }

        [TestCase("knight", 2, 2)]
        [TestCase("cleric", 2, 4)]
        public void A_foe_prefers_the_set_up_that_pins_a_rooted_hero(string rooted, int x, int y)
        {
            var s = TwoSetups();
            var mover = At(s, 2, 3);
            var plain = PincerPlanner.Plan(s, mover, 1);
            Assert.That(plain.Kind, Is.EqualTo(PincerPlanKind.Setup), plain.ToString());
            Assert.That(plain.Score, Is.EqualTo(PincerPlanner.SetupBase + PincerPlanner.SetupPerOpponent - PincerPlanner.StepCost).Within(0.01f),
                "control: either set-up is worth the same while nobody is rooted");

            Root(s, Hero(s, rooted));
            var plan = PincerPlanner.Plan(s, mover, 1);
            Assert.That(plan.Kind, Is.EqualTo(PincerPlanKind.Setup), plan.ToString());
            Assert.That(plan.End, Is.EqualTo(new Coord(x, y)), $"it lines up on the rooted {rooted}");
            Assert.That(plan.Score, Is.EqualTo(plain.Score + PincerPlanner.PinnedOpponentBonus).Within(0.01f));
            Assert.That(EnemyPlanner.PlanPincer(s, mover)?.End, Is.EqualTo(new Coord(x, y)), "the enemy turn takes the same walk");
            Assert.That(mover.Pos, Is.EqualTo(new Coord(2, 3)), "planning only looks");
        }

        [Test]
        public void A_pinned_set_up_never_outbids_a_pincer_it_can_close_now()
        {
            Assert.That(PincerPlanner.SetupBase + PincerPlanner.SetupPerOpponent + PincerPlanner.PinnedOpponentBonus,
                Is.LessThan(PincerPlanner.OpponentValue));
        }

        [Test]
        public void A_hero_planner_gets_no_pin_bonus()
        {
            // The knight looks from (2,2): the soldier at (2,3) is its line, the cleric can close (2,4). Rooting the soldier
            // changes nothing - the pin bonus is the foes' (§14.1.3 hero set-ups are unchanged).
            var s = TwoSetups();
            var knight = Hero(s, "knight");
            var home = knight.Pos;
            knight.Pos = new Coord(2, 2);
            try
            {
                float before = PincerPlanner.SetupValue(s, knight);
                Assert.That(before, Is.EqualTo(PincerPlanner.SetupBase + PincerPlanner.SetupPerOpponent).Within(0.01f));
                Root(s, At(s, 2, 3));
                Assert.That(PincerPlanner.SetupValue(s, knight), Is.EqualTo(before).Within(0.01f));
            }
            finally { knight.Pos = home; }
        }
    }
}
