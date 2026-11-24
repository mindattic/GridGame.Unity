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
    /// <summary>Bramble Snare (§7, GG-T881): the Wild Child roots one foe for 2 turns; roots landing on a Line charger snap
    /// its charge off; the hero bot only wants it on a foe that would get away, a Line charger or a hooker.</summary>
    public class BrambleSnareTests
    {
        /// <summary>Mini plus the real Rooted, an instant snare (so the cast resolves on the spot) and a hook.</summary>
        private static ContentDb WithSnare()
        {
            var db = Mini();
            db.Buffs["rooted"] = Real.Buffs["rooted"];
            db.Abilities["snare"] = new AbilityDef
            {
                Id = "snare", Name = "Bramble Snare", Kind = AbilityKind.Skill, Shape = TargetShape.SingleActor,
                Mode = TargetMode.PickActor, Filter = TargetFilter.EnemyOnly, ApplyBuff = "rooted", ApplyBuffChance = 1f,
            };
            db.Abilities["hook"] = new AbilityDef
            {
                Id = "hook", Name = "Hook", Kind = AbilityKind.Skill, Shape = TargetShape.SingleActor,
                Mode = TargetMode.PickActor, Filter = TargetFilter.EnemyOnly, Pull = 2,
            };
            return db;
        }

        private static Unit Foe(BattleState s, string cls) => s.Units.First(u => u.Def.Id == cls && u.Alive);

        private static List<BattleEvent> Snare(BattleState s, Unit caster, Unit foe)
        {
            caster.Bar[0] = new AbilitySlot { AbilityId = "snare" };
            caster.ActedThisWindow = false;
            return BattleEngine.Apply(s, new UseSlotCommand { HeroId = caster.Id, Slot = 0, Target = foe.Pos });
        }

        private static TimelineEntry Charge(BattleState s, Unit witch, Unit at, ChargeShape shape)
        {
            var entry = new TimelineEntry
            {
                Id = s.NextEntryId++, Kind = TimelineKind.Cast, OwnerId = witch.Id, AbilityId = "charge_bolt", CasterTeam = Team.Enemy,
                IsCharge = true, ChargeShape = shape, TargetUnitId = at.Id, TargetTile = at.Pos, TotalCastTicks = 500, RemainingTicks = 500,
                LineTiles = shape == ChargeShape.Line ? EnemyPlanner.LineToward(witch, at, s) : null,
            };
            s.Timeline.Add(entry);
            return entry;
        }

        private static float BotScore(BattleState s, Unit caster, AbilityDef a, Coord anchor)
        {
            var m = typeof(GridGame.Core.Bot.HeroBot).GetMethod("ScoreAbility", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.That(m, Is.Not.Null, "HeroBot.ScoreAbility");
            return (float)m.Invoke(new GridGame.Core.Bot.HeroBot(1), new object[] { s, caster, a, anchor });
        }

        [Test]
        public void Bramble_snare_ships_in_the_wild_childs_kit_as_a_rooting_spell()
        {
            var a = Real.Abilities["wildchild_bramblesnare"];
            Assert.That(a.Kind, Is.EqualTo(AbilityKind.Spell));
            Assert.That((a.Shape, a.Mode, a.Filter), Is.EqualTo((TargetShape.SingleActor, TargetMode.PickActor, TargetFilter.EnemyOnly)));
            Assert.That(a.ApplyBuff, Is.EqualTo("rooted"));
            Assert.That(a.BaseDamage, Is.EqualTo(0));
            Assert.That(Real.Buffs["rooted"].Roots, Is.True);
            Assert.That(Real.Buffs["rooted"].Duration, Is.EqualTo(2), "held for 2 turns");
            Assert.That(Real.Classes["wildchild"].Abilities, Does.Contain("wildchild_bramblesnare"));
            Assert.That(ContentLoader.Validate(Real).Where(p => p.Contains("wildchild_bramblesnare") || p.Contains("rooted")), Is.Empty);
        }

        [Test]
        public void The_snare_roots_the_picked_foe()
        {
            var (s, _) = Battle(WithSnare(), Stage(("wolf", 4, 2), ("goblin", 6, 6)), ("knight", 2, 2), ("cleric", 2, 5));
            var wolf = Foe(s, "wolf");
            var ev = Snare(s, Hero(s, "knight"), wolf);
            Assert.That(wolf.IsRooted, Is.True);
            Assert.That(ev.OfType<BuffAppliedEvent>().Any(b => b.UnitId == wolf.Id && b.BuffId == "rooted"), Is.True);
            Assert.That(EnemyPlanner.PlanStep(s, wolf), Is.EqualTo(wolf.Pos), "held to its tile");
        }

        [Test]
        public void Roots_snap_a_line_charge_off_but_not_a_single_charge()
        {
            var db = WithSnare();
            var (s, _) = Battle(db, Stage(("witch", 0, 0), ("goblin", 6, 6)), ("knight", 4, 0), ("cleric", 2, 5));
            var witch = Foe(s, "witch");
            var line = Charge(s, witch, Hero(s, "knight"), ChargeShape.Line);
            var ev = Snare(s, Hero(s, "cleric"), witch);
            Assert.That(s.Timeline.Contains(line), Is.False, "the line charge is gone");
            var cc = ev.OfType<CastCancelledEvent>().Single(c => c.EntryId == line.Id);
            Assert.That((cc.CasterId, cc.Reason), Is.EqualTo((witch.Id, "rooted")));

            var (s2, _) = Battle(WithSnare(), Stage(("witch", 0, 0), ("goblin", 6, 6)), ("knight", 4, 0), ("cleric", 2, 5));
            var witch2 = Foe(s2, "witch");
            var bolt = Charge(s2, witch2, Hero(s2, "knight"), ChargeShape.Single);
            var ev2 = Snare(s2, Hero(s2, "cleric"), witch2);
            Assert.That(witch2.IsRooted, Is.True);
            Assert.That(s2.Timeline.Contains(bolt), Is.True, "a bolt from where it stands goes on");
            Assert.That(ev2.OfType<CastCancelledEvent>().Any(), Is.False);
        }

        [Test]
        public void The_bot_values_it_only_on_a_foe_that_would_get_away_or_charge_a_line()
        {
            var db = WithSnare();
            db.Classes["wolf"].Brain = Brain.Kiter;
            var (s, _) = Battle(db, Stage(("wolf", 4, 2), ("goblin", 6, 6), ("tank", 0, 7)), ("knight", 2, 2), ("cleric", 2, 5));
            var wolf = Foe(s, "wolf");
            var tank = Foe(s, "tank");
            Assert.That(BrambleSnare.Value(s, tank), Is.EqualTo(0f), "a foe that never moves isn't worth it");
            float kiter = BrambleSnare.Value(s, wolf);
            Assert.That(kiter, Is.GreaterThan(0f), "a Kiter keeping its distance is");
            wolf.Buffs.Add(new BuffInstance { Def = db.Buffs["rooted"], Remaining = 2 });
            Assert.That(BrambleSnare.Value(s, wolf), Is.EqualTo(0f), "nor one already rooted");

            var plain = WithSnare();
            var (p, _) = Battle(plain, Stage(("wolf", 4, 2), ("goblin", 6, 6)), ("knight", 2, 2), ("cleric", 2, 5));
            var hunter = Foe(p, "wolf");
            Assert.That(BrambleSnare.Value(p, hunter), Is.EqualTo(0f), "a hunter walking in to fight is coming anyway");
            hunter.MaxHp = 100; hunter.Hp = 10;
            Assert.That(BrambleSnare.Value(p, hunter), Is.GreaterThan(0f), "wounded, it would retreat");
        }

        [Test]
        public void The_bot_wants_a_line_charger_more_with_heroes_on_its_lane_and_a_hooker()
        {
            var db = WithSnare();
            var (s, _) = Battle(db, Stage(("witch", 0, 0), ("goblin", 6, 6)), ("knight", 4, 0), ("cleric", 2, 5));
            var witch = Foe(s, "witch");
            Assert.That(BrambleSnare.Value(s, witch), Is.EqualTo(0f), "a still witch going nowhere");
            Charge(s, witch, Hero(s, "knight"), ChargeShape.Line);
            Assert.That(BrambleSnare.Value(s, witch), Is.GreaterThan(6f), "a line charge with a hero on its lane");

            var hooks = WithSnare();
            hooks.Classes["wolf"].Abilities.Add("hook");
            var (h, _) = Battle(hooks, Stage(("wolf", 4, 2), ("goblin", 6, 6)), ("knight", 2, 2), ("cleric", 2, 5));
            Assert.That(BrambleSnare.Hooks(h, Foe(h, "wolf")), Is.True);
            Assert.That(BrambleSnare.Value(h, Foe(h, "wolf")), Is.GreaterThan(0f), "a hooker is held for the pincer");
        }

        [Test]
        public void The_hero_bot_scores_the_snare_on_a_kiter_above_a_foe_coming_anyway()
        {
            var db = WithSnare();
            db.Classes["wolf"].Brain = Brain.Kiter;
            var (s, _) = Battle(db, Stage(("wolf", 4, 2), ("goblin", 6, 6)), ("knight", 2, 2), ("cleric", 2, 5));
            var knight = Hero(s, "knight");
            var a = s.Content.Abilities["snare"];
            float onKiter = BotScore(s, knight, a, Foe(s, "wolf").Pos);
            float onGoblin = BotScore(s, knight, a, Foe(s, "goblin").Pos);
            Assert.That(onKiter, Is.GreaterThan(onGoblin), $"kiter {onKiter}, goblin {onGoblin}");
            Assert.That(onGoblin, Is.LessThanOrEqualTo(0f), "never cast for its own sake");
        }
    }
}
