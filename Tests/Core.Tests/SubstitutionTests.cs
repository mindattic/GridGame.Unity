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
    /// <summary>The White Ninja's Substitution (GG-T862, BIBLE §7): once per battle, a foe's blow that would KO the ninja
    /// strikes a log instead; the ninja reappears on a random free tile beside an ally and the log stands one turn as an
    /// obstacle where it stood.</summary>
    public class SubstitutionTests
    {
        private const string Sub = "whiteninja_substitution";
        private const string Log = "substitution_log";

        /// <summary>Mini content plus the Substitution ability and the ninjadecoy rule, built here so the behaviour is
        /// checked without the full content set (the content test below checks the shipped rows match).</summary>
        private static ContentDb Db()
        {
            var db = Mini();
            db.Classes["wolf"].MoveRange = 0;
            db.Abilities[Sub] = new AbilityDef
            {
                Id = Sub, Name = "Substitution", Kind = AbilityKind.Reactive,
                Trigger = new TriggerDef
                {
                    On = TriggerOn.OnLethalBlow, OncePerBattle = true, Callout = "Substitution!",
                    Effects = { new TriggerEffect { Kind = TriggerEffectKind.Substitute, State = Log } },
                },
            };
            var rule = new RegionRuleDef { Id = "ninjadecoy", TileStates = { new TileStateDef { Id = Log, Blocks = true, LifeSeconds = 4f, Color = "#8b5a2b", Glyph = "▬" } } };
            RegionRuleCatalog.Index(db, new List<RegionRuleDef> { rule });
            return db;
        }

        /// <summary>A knight that knows Substitution, at 1 HP, under an immobile wolf; a cleric across the board unless
        /// <paramref name="alone"/>.</summary>
        private static (BattleState s, Unit knight, Unit wolf, List<BattleEvent> ev) Setup(ContentDb db, int seed = 3, bool alone = false, bool lowHp = true)
        {
            var setup = new BattleSetup { Stage = Stage(("wolf", 2, 2)), Seed = seed, UnlockedSlots = 2 };
            setup.Party.Add(new HeroSpec { ClassId = "knight", Start = new Coord(2, 3), Passives = { Sub } });
            if (!alone) setup.Party.Add(new HeroSpec { ClassId = "cleric", Start = new Coord(5, 7) });
            var all = new List<BattleEvent>();
            var s = BattleEngine.Create(db, setup, all);
            var k = Hero(s, "knight");
            if (lowHp) k.Hp = 1;
            return (s, k, s.Units.First(u => u.Def.Id == "wolf"), all);
        }

        /// <summary>Forces the wolf's turn until it has swung at the knight and landed (or a substitution fired).</summary>
        private static void BiteUntilLanded(BattleState s, Unit k, Unit wolf, List<BattleEvent> all, int turns = 8)
        {
            for (int i = 0; i < turns && wolf.Alive && k.Alive && !s.IsOver; i++)
            {
                if (all.OfType<SubstitutedEvent>().Any() || Landed(all, wolf, k)) return;
                s.TurnEntryOf(wolf.Id).Ip = BattleConfig.BarLength - 1;
                all.AddRange(BattleEngine.Apply(s, new ShieldCommand()));
                AssertInvariants(s);
            }
        }

        private static bool Landed(List<BattleEvent> ev, Unit wolf, Unit k) =>
            ev.OfType<DamagedEvent>().Any(d => d.SourceId == wolf.Id && d.TargetId == k.Id && d.Hit != HitKind.Miss && d.Amount > 0);

        [Test]
        public void A_lethal_blow_strikes_a_log_and_the_ninja_reappears_beside_an_ally()
        {
            var (s, k, wolf, ev) = Setup(Db());
            var cleric = Hero(s, "cleric");
            BiteUntilLanded(s, k, wolf, ev);
            var sub = ev.OfType<SubstitutedEvent>().SingleOrDefault();
            Assume.That(sub, Is.Not.Null, "the wolf's bite connected");

            Assert.That(k.Alive, Is.True, "the ninja lives");
            Assert.That(k.Hp, Is.EqualTo(1), "the log took the blow, not the ninja");
            Assert.That(Landed(ev, wolf, k), Is.False, "no bite damage reached the ninja");
            Assert.That(sub.UnitId, Is.EqualTo(k.Id));
            Assert.That(sub.AttackerId, Is.EqualTo(wolf.Id));
            Assert.That(sub.From, Is.EqualTo(new Coord(2, 3)));
            Assert.That(sub.To, Is.EqualTo(k.Pos));
            Assert.That(k.IsAdjacentTo(cleric), Is.True, "it reappears beside its ally");

            var log = s.Region.At(new Coord(2, 3));
            Assert.That(log, Is.Not.Null, "a log stands where the ninja stood");
            Assert.That(log.State, Is.EqualTo(Log));
            Assert.That(log.Blocks, Is.True, "the log is an obstacle");
            Assert.That(s.Walkable(new Coord(2, 3)), Is.False);

            int cue = ev.FindIndex(e => e is TriggeredEvent t && t.AbilityId == Sub);
            Assert.That(cue, Is.GreaterThanOrEqualTo(0), "the reactive fired");
            var tr = (TriggeredEvent)ev[cue];
            Assert.That(tr.Effect, Is.EqualTo(TriggerEffectKind.Substitute));
            Assert.That(tr.Callout, Is.EqualTo("Substitution!"));
            Assert.That(cue, Is.LessThan(ev.IndexOf(sub)), "the callout plays before the swap");
            Assert.That(ev.OfType<RegionTilesEvent>().Any(r => r.State == Log && r.Change == "marked" && r.Tiles.Contains(new Coord(2, 3))), Is.True);
        }

        [Test]
        public void It_works_once_per_battle()
        {
            var (s, k, wolf, ev) = Setup(Db());
            Assert.That(Triggers.SubstituteReady(s, k), Is.True, "ready before it is spent");
            BiteUntilLanded(s, k, wolf, ev);
            Assume.That(ev.OfType<SubstitutedEvent>().Count(), Is.EqualTo(1));
            Assert.That(Triggers.SubstituteReady(s, k), Is.False, "spent for the rest of the battle");

            // Put the ninja back under the wolf (the log cleared) and let it bite again: no second log this time.
            s.Region.Tiles.Clear();
            k.Pos = new Coord(2, 3);
            k.Hp = 1;
            int before = ev.Count;
            for (int i = 0; i < 8 && k.Alive && !s.IsOver; i++)
            {
                s.TurnEntryOf(wolf.Id).Ip = BattleConfig.BarLength - 1;
                ev.AddRange(BattleEngine.Apply(s, new ShieldCommand()));
            }
            Assume.That(ev.Skip(before).OfType<DamagedEvent>().Any(d => d.SourceId == wolf.Id && d.TargetId == k.Id && d.Hit != HitKind.Miss), Is.True);
            Assert.That(ev.OfType<SubstitutedEvent>().Count(), Is.EqualTo(1), "Substitution never fires twice");
            Assert.That(k.Alive, Is.False, "the second lethal bite fells it");
        }

        [Test]
        public void A_blow_that_would_not_KO_leaves_it_unspent()
        {
            var (s, k, wolf, ev) = Setup(Db(), lowHp: false);
            BiteUntilLanded(s, k, wolf, ev);
            Assume.That(Landed(ev, wolf, k), Is.True);
            Assume.That(k.Alive, Is.True);
            Assert.That(ev.OfType<SubstitutedEvent>().Any(), Is.False);
            Assert.That(k.Pos, Is.EqualTo(new Coord(2, 3)));
            Assert.That(Triggers.SubstituteReady(s, k), Is.True, "still up for the blow that matters");
        }

        [Test]
        public void With_no_ally_to_reappear_beside_it_does_not_fire()
        {
            var (s, k, wolf, ev) = Setup(Db(), alone: true);
            Assert.That(Triggers.SubstituteSpots(s, k), Is.Empty);
            Assert.That(Triggers.SubstituteReady(s, k), Is.False);
            BiteUntilLanded(s, k, wolf, ev);
            Assume.That(Landed(ev, wolf, k), Is.True);
            Assert.That(ev.OfType<SubstitutedEvent>().Any(), Is.False);
            Assert.That(k.Alive, Is.False);
        }

        [Test]
        public void The_spots_are_the_free_tiles_beside_allies_in_row_major_order()
        {
            var (s, k, _, _) = Setup(Db());
            var cleric = Hero(s, "cleric");
            var spots = Triggers.SubstituteSpots(s, k);
            Assert.That(spots, Is.Not.Empty);
            Assert.That(spots.All(c => c.Manhattan(cleric.Pos) == 1), Is.True, "every spot is beside the ally");
            Assert.That(spots.All(c => s.FreeForItem(c)), Is.True);
            Assert.That(spots.Contains(k.Pos), Is.False);
            Assert.That(spots, Is.EqualTo(spots.OrderBy(c => c.Y).ThenBy(c => c.X).ToList()), "row-major, so the Rng's pick replays");
        }

        [Test]
        public void The_log_clears_after_about_a_turn()
        {
            var (s, k, wolf, ev) = Setup(Db());
            BiteUntilLanded(s, k, wolf, ev);
            Assume.That(ev.OfType<SubstitutedEvent>().Any(), Is.True);
            for (int i = 0; i < 30 && s.Region.At(new Coord(2, 3)) != null && !s.IsOver; i++)
            {
                ev.AddRange(BattleEngine.Apply(s, new ShieldCommand()));
                AssertInvariants(s);
            }
            Assert.That(s.Region.At(new Coord(2, 3)), Is.Null, "the log is gone");
            Assert.That(ev.OfType<RegionTilesEvent>().Any(r => r.State == Log && r.Change == "cleared"), Is.True);
        }

        [Test]
        public void It_replays_identically_per_seed()
        {
            for (int seed = 1; seed <= 6; seed++)
            {
                var (s1, k1, w1, a) = Setup(Db(), seed);
                BiteUntilLanded(s1, k1, w1, a);
                var (s2, k2, w2, b) = Setup(Db(), seed);
                BiteUntilLanded(s2, k2, w2, b);
                Assert.That(b.Select(e => e.ToString()), Is.EqualTo(a.Select(e => e.ToString())), $"seed {seed}");
            }
        }

        [Test]
        public void Validation_keeps_Substitute_on_a_lethal_blow_with_a_blocking_decoy()
        {
            var db = Db();
            AbilityDef Make(TriggerOn on, string state) => new AbilityDef
            {
                Id = "x", Name = "x", Kind = AbilityKind.Reactive,
                Trigger = new TriggerDef { On = on, Effects = { new TriggerEffect { Kind = TriggerEffectKind.Substitute, State = state } } },
            };
            List<string> Problems(AbilityDef a) { var p = new List<string>(); ContentLoader.ValidateTrigger(db, a, p); return p; }
            Assert.That(Problems(Make(TriggerOn.OnLethalBlow, Log)), Is.Empty);
            Assert.That(Problems(Make(TriggerOn.OnDamaged, Log)), Has.Some.Contains("OnLethalBlow only"));
            Assert.That(Problems(Make(TriggerOn.OnLethalBlow, null)), Has.Some.Contains("needs a State"));
            Assert.That(Problems(Make(TriggerOn.OnLethalBlow, "nope")), Has.Some.Contains("unknown tile state"));
        }

        [Test]
        public void The_White_Ninja_ships_with_Substitution()
        {
            var problems = ContentLoader.Validate(Real).Where(p => p.Contains(Sub) || p.Contains(Log) || p.Contains("ninjadecoy")).ToList();
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
            var a = Real.TryAbility(Sub);
            Assert.That(a, Is.Not.Null);
            Assert.That(a.Kind, Is.EqualTo(AbilityKind.Reactive));
            Assert.That(a.Trigger.On, Is.EqualTo(TriggerOn.OnLethalBlow));
            Assert.That(a.Trigger.OncePerBattle, Is.True);
            Assert.That(a.Trigger.Effects.Single().Kind, Is.EqualTo(TriggerEffectKind.Substitute));
            Assert.That(a.Trigger.Effects.Single().State, Is.EqualTo(Log));
            Assert.That(Real.TryClass("whiteninja").Abilities, Does.Contain(Sub));
            var log = Real.TryTileState(Log);
            Assert.That(log, Is.Not.Null);
            Assert.That(log.Blocks, Is.True);
            Assert.That(log.LifeSeconds, Is.GreaterThan(0f).And.LessThanOrEqualTo(6f), "it stands about one turn");
            Assert.That(TriggerText.Describe(Real, a), Does.Contain("once per battle"));
        }
    }
}
