using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>Anchor Stance (§7, GG-T882): the Warden wears [Anchored] until its next action - Rooted, Heavy (the buff-based
    /// Heavy path), and every ally beside it is braced: no foe's shove, pull, swap or rule drag moves it, while its own side
    /// still can. The hero bot casts it only when a foe's displacing cast is queued at the Warden or an ally beside it.</summary>
    public class AnchorStanceTests
    {
        private const string Id = "warden_anchorstance";
        private const string Buff = "anchored";

        /// <summary>Mini plus the shipped stance and its buff, and a plain hook (a foe-targeting pull) for the knight.</summary>
        private static ContentDb Db()
        {
            var db = Mini();
            db.Abilities[Id] = Real.Ability(Id);
            db.Buffs[Buff] = Real.Buffs[Buff];
            db.Abilities["hook"] = new AbilityDef
            {
                Id = "hook", Name = "hook", Kind = AbilityKind.Skill, BaseDamage = 1, Pull = 2, DamageType = DamageType.Physical,
                Shape = TargetShape.SingleActor, Mode = TargetMode.PickActor, Filter = TargetFilter.EnemyOnly,
            };
            return db;
        }

        private static List<BattleEvent> Use(BattleState s, Unit hero, string ability, Coord target)
        {
            hero.Bar[0] = new AbilitySlot { AbilityId = ability };
            hero.ActedThisWindow = false;
            hero.Cooldowns.Clear();
            return BattleEngine.Apply(s, new UseSlotCommand { HeroId = hero.Id, Slot = 0, Target = target });
        }

        private static void Anchor(BattleState s, Unit u) =>
            u.Buffs.Add(new BuffInstance { Def = s.Content.Buffs[Buff], Remaining = 1, SourceId = u.Id });

        private static Unit Enemy(BattleState s, string cls) => s.Units.First(u => u.Def.Id == cls && u.Alive);

        [Test]
        public void Anchor_stance_ships_as_the_wardens_self_skill_with_a_bracing_buff()
        {
            var a = Real.Ability(Id);
            Assert.That(a.Kind, Is.EqualTo(AbilityKind.Skill));
            Assert.That((a.Shape, a.Mode), Is.EqualTo((TargetShape.Self, TargetMode.Auto)));
            Assert.That(a.ApplyBuff, Is.EqualTo(Buff));
            var b = Real.Buffs[Buff];
            Assert.That((b.Kind, b.Roots, b.Heavy, b.Braces, b.EndsOnAction), Is.EqualTo((BuffKind.Buff, true, true, true, true)));
            Assert.That(Real.Classes["warden"].Abilities, Does.Contain(Id));
            var problems = ContentLoader.Validate(Real).Where(p => p.Contains(Id) || p.Contains(Buff)).ToList();
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
            Assert.That(Real.TryTerm("Anchored"), Is.Not.Null, "the buff's name is a glossary term");
        }

        [Test]
        public void The_warden_is_rooted_and_heavy_until_its_next_action_then_walks_again()
        {
            var (s, _) = Battle(Db(), Stage(("tank", 0, 0)), ("knight", 2, 4), ("cleric", 5, 7));
            var k = Hero(s, "knight");
            Assert.That(k.IsHeavy, Is.False);
            k.Bar[0] = new AbilitySlot { AbilityId = Id };
            Turn(s, new UseSlotCommand { HeroId = k.Id, Slot = 0, Target = k.Pos });
            Assert.That((k.IsAnchoring, k.IsRooted, k.IsHeavy, k.HasHeavyBuff), Is.EqualTo((true, true, true, true)),
                "planted, and still planted after the foes' turn: it lasts until the Warden's next action, not a turn count");

            k.ActedThisWindow = false;
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = k.Id, Path = P((2, 3)) });
            Assert.That(ev.OfType<BuffRemovedEvent>().Any(r => r.UnitId == k.Id && r.BuffId == Buff && r.Reason == "acted"), Is.True);
            Assert.That(k.Pos, Is.EqualTo(new Coord(2, 3)), "the stance lifts as the next action starts, so the Warden can walk");
            Assert.That((k.IsAnchoring, k.IsRooted, k.IsHeavy), Is.EqualTo((false, false, false)));
            AssertInvariants(s);
        }

        [Test]
        public void A_unit_beside_an_anchoring_ally_is_braced_against_a_foes_pull()
        {
            // The rule reads both sides: here the wolf anchors and the tank beside it is the one the knight hooks.
            var (s, _) = Battle(Db(), Stage(("tank", 2, 0), ("wolf", 3, 0)), ("knight", 2, 4), ("cleric", 5, 7));
            var tank = Enemy(s, "tank");
            Anchor(s, Enemy(s, "wolf"));
            var ev = Use(s, Hero(s, "knight"), "hook", tank.Pos);
            Assert.That(tank.Pos, Is.EqualTo(new Coord(2, 0)), "braced: the hook can't tear it loose");
            Assert.That(ev.OfType<DisplacedEvent>().Any(d => d.UnitId == tank.Id), Is.False);
            Assert.That(ev.OfType<StepBlockedEvent>().Any(b => b.Reason == "braced"), Is.True);
            Assert.That(ev.OfType<AnnounceEvent>().Any(a => a.Text == "Braced!" && a.UnitId == tank.Id), Is.True);
        }

        [Test]
        public void An_ally_out_of_reach_of_the_stance_is_pulled_as_usual()
        {
            var (s, _) = Battle(Db(), Stage(("tank", 2, 0), ("wolf", 4, 0)), ("knight", 2, 4), ("cleric", 5, 7));
            var tank = Enemy(s, "tank");
            Anchor(s, Enemy(s, "wolf")); // two tiles off: not beside it
            Use(s, Hero(s, "knight"), "hook", tank.Pos);
            Assert.That(tank.Pos, Is.EqualTo(new Coord(2, 2)));
            AssertInvariants(s);
        }

        [Test]
        public void Only_a_foe_or_a_rule_is_stopped_never_the_braced_units_own_side()
        {
            var (s, _) = Battle(Db(), Stage(("tank", 0, 0)), ("knight", 2, 4), ("cleric", 2, 5));
            var k = Hero(s, "knight"); var c = Hero(s, "cleric"); var foe = Enemy(s, "tank");
            Assert.That(AnchorStance.BracedBy(s, c, foe), Is.Null, "nobody anchors yet");
            Anchor(s, k);
            Assert.That(AnchorStance.BracedBy(s, c, foe), Is.SameAs(k), "a foe's shove is stopped");
            Assert.That(AnchorStance.BracedBy(s, c, null), Is.SameAs(k), "so is a rule's drag (the anchor's heave)");
            Assert.That(AnchorStance.BracedBy(s, c, k), Is.Null, "an ally's own drag swap still moves it");
            Assert.That(AnchorStance.BracedBy(s, k, foe), Is.Null, "the Warden isn't braced by itself (its own Rooted holds it)");
            c.Pos = new Coord(4, 5);
            Assert.That(AnchorStance.BracedBy(s, c, foe), Is.Null, "no longer beside the Warden");
        }

        [Test]
        public void A_class_heavy_unit_and_a_buff_heavy_unit_read_the_same_and_floating_lifts_both()
        {
            var db = Db();
            db.Classes["tank"].Heavy = true;
            var (s, _) = Battle(db, Stage(("tank", 0, 0), ("wolf", 4, 0)), ("knight", 2, 4), ("cleric", 5, 7));
            var tank = Enemy(s, "tank"); var wolf = Enemy(s, "wolf");
            Anchor(s, wolf);
            Assert.That((tank.IsHeavy, wolf.IsHeavy, wolf.WontBudge), Is.EqualTo((true, true, true)));
            var floating = new BuffDef { Id = Unit.FloatingBuffId, Name = "Floating", Kind = BuffKind.Buff, Unit = DurationUnit.Turns, Duration = 3 };
            wolf.Buffs.Add(new BuffInstance { Def = floating, Remaining = 3 });
            Assert.That(wolf.IsHeavy, Is.False, "a floating unit is never Heavy, whatever made it so");
        }

        [Test]
        public void The_bot_values_the_stance_only_while_a_foes_hook_is_queued_at_the_warden_or_an_ally_beside_it()
        {
            var (s, _) = Battle(Db(), Stage(("wolf", 0, 0)), ("knight", 2, 4), ("cleric", 2, 5));
            var k = Hero(s, "knight"); var c = Hero(s, "cleric"); var wolf = Enemy(s, "wolf");
            Assert.That(AnchorStance.Is(s, s.Content.TryAbility(Id)), Is.True);
            Assert.That(AnchorStance.Is(s, s.Content.TryAbility("hook")), Is.False);
            Assert.That(AnchorStance.Value(s, k), Is.EqualTo(0f), "nothing queued: never cast for its own sake");

            var hook = new TimelineEntry { Kind = TimelineKind.Cast, OwnerId = wolf.Id, CasterTeam = Team.Enemy, AbilityId = "hook", TargetUnitId = c.Id, IsCharge = true };
            s.Timeline.Add(hook);
            Assert.That(AnchorStance.Value(s, k), Is.EqualTo(AnchorStance.SpareValue), "the hook is aimed at the cleric beside the Warden");

            c.Pos = new Coord(5, 5);
            Assert.That(AnchorStance.Value(s, k), Is.EqualTo(0f), "the cleric isn't beside the Warden: the stance can't hold it");

            hook.TargetUnitId = k.Id;
            Assert.That(AnchorStance.Value(s, k), Is.EqualTo(AnchorStance.SpareValue), "aimed at the Warden itself");
            Anchor(s, k);
            Assert.That(AnchorStance.Value(s, k), Is.EqualTo(0f), "already planted");
            k.Buffs.Clear();

            hook.AbilityId = "fireball";
            Assert.That(AnchorStance.Value(s, k), Is.EqualTo(0f), "a cast that moves nobody");
        }
    }
}
