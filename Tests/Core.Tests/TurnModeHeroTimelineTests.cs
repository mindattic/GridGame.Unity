using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Core.Rules;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>
    /// GG-T1070 (bible §2.4, §2.13.3, core GG-T807): in <see cref="TurnMode.HeroTimeline"/> each commanded hero rides the
    /// shared timeline on its own turn icon. A faster hero enters further left (Speed against
    /// <see cref="Formulas.ReferenceHeroSpeed"/>), hero and foe icons race on the one clock, the heroes open each wave ahead
    /// of the foes, a same-tick tie goes to the hero, and a seeded run replays identically. Only the clock is driven
    /// (<see cref="BattleEngine.RunClock"/>): handing a hero's arrival to the player lands with GG-T808.
    /// </summary>
    public class TurnModeHeroTimelineTests
    {
        private static Stats Spd(float speed) =>
            new Stats { Strength = 20, Vitality = 10, Agility = 10, Speed = speed, Intelligence = 10 };

        private static ContentDb Db()
        {
            var db = Mini();
            db.Classes["sprinter"] = new ClassDef { Id = "sprinter", Name = "Sprinter", Team = Team.Hero, Tags = { "Humanoid" }, BaseStats = Spd(24) };
            db.Classes["plodder"] = new ClassDef { Id = "plodder", Name = "Plodder", Team = Team.Hero, Tags = { "Humanoid" }, BaseStats = Spd(8) };
            db.Classes["imp"] = new ClassDef { Id = "imp", Name = "Imp", Team = Team.Enemy, Tags = { "Beast" }, BaseStats = Spd(30), MoveRange = 0 };
            return db;
        }

        private static BattleSetup Setup(StageDef stage, int seed, params (string cls, int x, int y)[] heroes)
        {
            var setup = new BattleSetup { Stage = stage, Seed = seed, UnlockedSlots = 5, TurnMode = TurnMode.HeroTimeline };
            foreach (var h in heroes) setup.Party.Add(new HeroSpec { ClassId = h.cls, Start = new Coord(h.x, h.y) });
            return setup;
        }

        private static BattleState Create(StageDef stage, params (string cls, int x, int y)[] heroes) =>
            BattleEngine.Create(Db(), Setup(stage, 7, heroes), new List<BattleEvent>());

        private static Unit Foe(BattleState s) => s.Units.First(u => u.Alive && u.Team == Team.Enemy);

        private static int Ticks(BattleState s, Unit u) => BattleEngine.TicksUntilTurn(s, u.Id);

        [Test]
        public void A_faster_hero_enters_further_left_judged_against_the_hero_reference_speed()
        {
            var s = Create(Stage(("goblin", 5, 0)), ("sprinter", 0, 6), ("knight", 2, 6), ("plodder", 4, 6));
            Assert.That(TurnRules.HeroesOnTimeline(s), Is.True);
            var fast = Hero(s, "sprinter");
            var mid = Hero(s, "knight");
            var slow = Hero(s, "plodder");
            foreach (var h in new[] { fast, mid, slow })
            {
                Assert.That(TurnRules.IsHeroIcon(s, h), Is.True, h.Def.Id);
                Assert.That(s.TurnEntryOf(h.Id), Is.Not.Null, h.Def.Id + " has its own turn icon");
                float expected = BattleConfig.EntryBaseSeconds
                    * (float)Math.Pow(Formulas.ReferenceHeroSpeed(h.Level) / Math.Max(1f, h.Effective.Speed), BattleConfig.EntrySpeedExponent);
                expected = Math.Max(BattleConfig.EntryMinSeconds, Math.Min(BattleConfig.EntryMaxSeconds, expected));
                Assert.That(BattleEngine.EntrySeconds(h), Is.EqualTo(expected).Within(1e-4f), h.Def.Id + " is judged against heroes");
            }
            Assert.That(Formulas.ReferenceHeroSpeed(1), Is.EqualTo(12f));
            Assert.That(Formulas.ReferenceHeroSpeed(11), Is.EqualTo(19f).Within(1e-4f), "12 + 0.7 per level");

            // GG-T1083: the battle opens on the first hero's own turn — the fastest arrived first and is the active hero.
            Assert.That(s.ActiveHeroId, Is.EqualTo(fast.Id), "the fastest hero's icon arrived first");
            Assert.That(Ticks(s, mid), Is.LessThan(Ticks(s, slow)));
            Assert.That(BattleEngine.BarU(s, s.TurnEntryOf(mid.Id)), Is.LessThan(BattleEngine.BarU(s, s.TurnEntryOf(slow.Id))), "further left");
        }

        [Test]
        public void Hero_and_foe_icons_race_on_the_one_clock()
        {
            var s = Create(Stage(("goblin", 5, 0)), ("sprinter", 0, 6));
            var hero = Hero(s, "sprinter");
            var foe = Foe(s);
            // GG-T1083: the battle opened on the hero's turn, so its icon is already back on its cycle (recovery + entry).
            Assert.That(s.ActiveHeroId, Is.EqualTo(hero.Id));
            int cycle = Ticks(s, hero), tf = Ticks(s, foe);
            int k = Math.Min(cycle, tf) - 1;
            Assert.That(k, Is.GreaterThan(0));

            var ev = BattleEngine.RunClock(s, k);
            Assert.That(Ticks(s, hero), Is.EqualTo(cycle - k));
            Assert.That(Ticks(s, foe), Is.EqualTo(tf - k), "both icons moved the same ticks");
            Assert.That(ev.OfType<TurnStartedEvent>(), Is.Empty);

            var all = new List<BattleEvent>(BattleEngine.RunClock(s, Ticks(s, hero)));
            Assert.That(Ticks(s, hero), Is.EqualTo(cycle), "arrived: off the bar for RecoverBaseSeconds, then back at its Speed's entry");
            if (!all.OfType<TurnStartedEvent>().Any(t => t.UnitId == foe.Id)) all.AddRange(BattleEngine.RunClock(s, Ticks(s, foe)));
            Assert.That(all.OfType<TurnStartedEvent>().Select(t => t.UnitId), Does.Contain(foe.Id), "the foe's icon arrives on the same clock");
            Assert.That(all.OfType<TurnStartedEvent>().Select(t => t.UnitId), Does.Not.Contain(hero.Id), "a hero's arrival runs no foe turn");
        }

        [Test]
        public void Heroes_open_each_wave_ahead_of_the_foes()
        {
            var stage = StageWaves(new[] { ("imp", 5, 0) }, new[] { ("imp", 5, 1), ("imp", 3, 0) });
            var s = Create(stage, ("plodder", 0, 6), ("knight", 2, 6));
            var heroes = s.Units.Where(u => u.IsHero).ToList();
            // GG-T1083: wave 1 opened on the first hero's turn; the rest of the party still opens ahead of the foes.
            AssertHeroesOpen(s, heroes.Where(h => h.Id != s.ActiveHeroId).ToList(), "wave 1");

            // The wave-1 imp burns down on the clock alone; the next wave's opening reschedules every hero icon.
            var imp = Foe(s);
            imp.Hp = 1;
            imp.Buffs.Add(new BuffInstance { Def = s.Content.Buffs["burning"], Remaining = 5 });
            for (int i = 0; i < 200 && s.Wave == 0 && !s.IsOver; i++) BattleEngine.RunClock(s, 1);
            Assert.That(s.Wave, Is.EqualTo(1), "wave 2 began");
            AssertHeroesOpen(s, heroes, "wave 2");
            foreach (var h in heroes)
                Assert.That(Ticks(s, h), Is.EqualTo(BattleEngine.TicksUntilAct(s, s.TurnEntryOf(h.Id))), h.Def.Id);
        }

        private static void AssertHeroesOpen(BattleState s, List<Unit> heroes, string wave)
        {
            int lastHero = heroes.Max(h => Ticks(s, h));
            foreach (var foe in s.Units.Where(u => u.Alive && u.Team == Team.Enemy))
                Assert.That(Ticks(s, foe), Is.GreaterThanOrEqualTo(lastHero), wave + ": no foe opens before the last hero (a tie goes to the hero)");
        }

        [Test]
        public void On_a_same_tick_tie_the_hero_turn_resolves_before_the_foe_turn()
        {
            var s = Create(Stage(("goblin", 5, 0)), ("knight", 0, 6));
            var hero = Hero(s, "knight");
            var foe = Foe(s);
            var he = s.TurnEntryOf(hero.Id);
            var fe = s.TurnEntryOf(foe.Id);
            Assert.That(fe.Id, Is.LessThan(he.Id), "by entry id alone the foe would win the tie");

            // Line the foe's icon up with the hero's so both arrive on one tick.
            fe.Ip = he.Ip; fe.RecoverTicks = he.RecoverTicks; fe.RecoverTotalTicks = he.RecoverTotalTicks; fe.StunTicks = he.StunTicks;
            int t = Ticks(s, hero);
            Assert.That(Ticks(s, foe), Is.EqualTo(t));

            var runner = NewRunner(s);
            Assert.That(Earlier(runner, he, fe), Is.True, "hero turn before foe turn");
            Assert.That(Earlier(runner, fe, he), Is.False);
            Assert.That(ArrivalRank(TimelineKind.Cast), Is.LessThan(ArrivalRank(TimelineKind.Hazard)), "casts before hazards");
            Assert.That(ArrivalRank(TimelineKind.Hazard), Is.LessThan(ArrivalRank(TimelineKind.Turn)), "hazards before turns");

            var ev = BattleEngine.RunClock(s, t);
            Assert.That(ev.OfType<TurnStartedEvent>().Select(x => x.UnitId), Does.Contain(foe.Id), "the foe still took its turn on the tie tick");
            Assert.That(Ticks(s, hero), Is.EqualTo(t), "the hero's icon resolved on the same tick (back on the same cycle the battle opened with)");
        }

        [Test]
        public void Only_the_active_hero_may_act()
        {
            var s = Create(Stage(("goblin", 5, 0)), ("sprinter", 0, 6), ("plodder", 4, 6));
            var fast = Hero(s, "sprinter");
            var slow = Hero(s, "plodder");
            Assert.That(s.ActiveHeroId, Is.EqualTo(fast.Id), "the battle opens on the first hero's own turn");
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = slow.Id, Path = new List<Coord> { new Coord(4, 6), new Coord(4, 5) } });
            var refused = ev.OfType<AbilityRefusedEvent>().Single();
            Assert.That(refused.CasterId, Is.EqualTo(slow.Id));
            Assert.That(refused.Reason, Is.EqualTo("Not this hero's turn"));
            Assert.That(slow.Pos, Is.EqualTo(new Coord(4, 6)), "nothing happened");
        }

        [Test]
        public void An_action_runs_the_clock_on_to_the_next_hero_turn()
        {
            var s = Create(Stage(("goblin", 5, 0)), ("sprinter", 0, 6), ("plodder", 4, 6));
            Assert.That(s.ActiveHeroId, Is.Not.EqualTo(-1));
            var clock = s.Clock;
            int arrivedBefore = s.ActiveHeroId;
            BattleEngine.Apply(s, new ShieldCommand());
            Assert.That(s.IsOver, Is.False);
            Assert.That(s.Clock, Is.GreaterThan(clock), "the clock ran on");
            Assert.That(s.ActiveHeroId, Is.Not.EqualTo(-1), "it stopped on a hero's own turn");
            var active = s.UnitById(s.ActiveHeroId);
            Assert.That(active.IsHero && TurnRules.MayAct(s, active), Is.True, "the new active hero may act");
            foreach (var h in s.Units.Where(u => u.IsHero && u.Id != s.ActiveHeroId))
                Assert.That(TurnRules.NotThisHerosTurn(s, h), Is.True, h.Def.Id + " waits for its own icon");
            Assert.That(arrivedBefore, Is.Not.EqualTo(-1));
        }

        [Test]
        public void A_seeded_run_replays_identically()
        {
            string Run()
            {
                var stage = StageWaves(new[] { ("goblin", 5, 0), ("imp", 3, 0) }, new[] { ("imp", 5, 1) });
                var s = BattleEngine.Create(Db(), Setup(stage, 31, ("sprinter", 0, 6), ("knight", 2, 6), ("plodder", 4, 6)), new List<BattleEvent>());
                var sb = new StringBuilder();
                var arrivals = new Dictionary<int, int>();
                for (int i = 0; i < 400 && !s.IsOver; i++)
                {
                    var before = s.Units.Where(u => u.IsHero).ToDictionary(u => u.Id, u => Ticks(s, u));
                    foreach (var e in BattleEngine.RunClock(s, 1)) sb.Append(e.GetType().Name).Append(';');
                    foreach (var kv in before)
                        if (Ticks(s, s.UnitById(kv.Key)) > kv.Value) arrivals[kv.Key] = arrivals.TryGetValue(kv.Key, out var n) ? n + 1 : 1;
                    sb.Append('|').Append(s.Clock).Append(':');
                    foreach (var e in s.Timeline) sb.Append(e.Id).Append(',').Append(e.OwnerId).Append(',').Append(e.Ip).Append(',').Append(e.RecoverTicks).Append(',').Append(e.StunTicks).Append(' ');
                    foreach (var u in s.Units) sb.Append(u.Id).Append('=').Append(u.Hp).Append(' ');
                    sb.Append('\n');
                }
                Assert.That(arrivals.Count, Is.EqualTo(3), "every hero icon arrived at least once");
                return sb.ToString();
            }

            Assert.That(Run(), Is.EqualTo(Run()));
        }

        #region GG-T1097: the active hero's window, Guard and death (GG-T1075)

        /// <summary>Puts <paramref name="u"/>'s turn icon <paramref name="n"/> ticks from the left edge.</summary>
        private static void ArriveIn(BattleState s, Unit u, int n)
        {
            var e = s.TurnEntryOf(u.Id);
            e.RecoverTicks = 0; e.StunTicks = 0;
            e.Ip = BattleConfig.BarLength - BattleEngine.TurnRatePerTick(s, u) * n;
        }

        [Test]
        public void The_turn_is_a_fixed_four_seconds_whatever_icons_are_near_the_edge()
        {
            var s = Create(Stage(("goblin", 5, 0)), ("sprinter", 0, 6), ("plodder", 4, 6));
            var fast = Hero(s, "sprinter");
            Assert.That(s.ActiveHeroId, Is.EqualTo(fast.Id));
            int turn = Formulas.SecondsToTicks(BattleConfig.HeroTimelineTurnSeconds);
            Assert.That(turn, Is.EqualTo(40));
            Assert.That(TurnRules.TurnTicks(s), Is.EqualTo(turn));
            ArriveIn(s, fast, 1); // GG-T1171: neither the active hero's own icon,
            ArriveIn(s, Hero(s, "plodder"), 2); // another hero's,
            ArriveIn(s, Foe(s), 3); // nor a foe's shortens the frozen turn
            Assert.That(TurnRules.TurnTicks(s), Is.EqualTo(turn));
            Assert.That(BattleEngine.TurnWindowSeconds(s), Is.EqualTo(BattleConfig.HeroTimelineTurnSeconds).Within(1e-5f));
            Assert.That(TurnRules.TooLittleTime(s), Is.False);
        }

        [Test]
        public void No_icon_advances_during_the_action_and_the_clock_then_runs_to_the_next_hero_icon()
        {
            var s = Create(Stage(("goblin", 5, 0)), ("sprinter", 0, 6), ("plodder", 1, 6), ("knight", 2, 6),
                ("sprinter", 3, 6), ("plodder", 4, 6), ("knight", 5, 6));
            var active = s.UnitById(s.ActiveHeroId);
            Assert.That(active, Is.Not.Null);
            var waiting = s.Units.Where(u => u.IsHero && u.Id != active.Id).ToList();
            Assert.That(waiting.Count, Is.EqualTo(5));
            for (int i = 0; i < waiting.Count; i++) ArriveIn(s, waiting[i], i + 1); // a hero icon on each of the next ticks
            var foe = Foe(s);
            ArriveIn(s, foe, 15);
            Assert.That(TurnRules.SpendsClock(s, new UseSlotCommand { HeroId = active.Id }), Is.False);

            long clock = s.Clock;
            var dest = new Coord(active.Pos.X, active.Pos.Y - 1);
            var move = new MoveCommand { HeroId = active.Id, Path = new List<Coord> { active.Pos, dest }, HeldSeconds = 1f };
            Assert.That(TurnRules.SpendsClock(s, move), Is.False, "the action's time never runs the clock");
            var ev = BattleEngine.Apply(s, move);
            Assert.That(ev.OfType<AbilityRefusedEvent>(), Is.Empty);
            Assert.That(active.Pos, Is.EqualTo(dest));
            Assert.That(s.Clock - clock, Is.EqualTo(1), "only the run to the next hero icon moved the clock");
            Assert.That(s.ActiveHeroId, Is.EqualTo(waiting[0].Id), "the nearest waiting hero takes the next turn");
            Assert.That(Ticks(s, foe), Is.EqualTo(14), "the foe icon moved one tick, not the drag's 10");
        }

        [Test]
        public void A_forced_drop_spends_exactly_the_window()
        {
            var s = Create(Stage(("goblin", 5, 0)), ("sprinter", 0, 6), ("plodder", 4, 6));
            var fast = Hero(s, "sprinter");
            int window = TurnRules.TurnTicks(s);
            Assert.That(window, Is.GreaterThan(0));
            var move = new MoveCommand { HeroId = fast.Id, Path = P((0, 6), (0, 5)), HeldSeconds = 999f };
            Assert.That(BattleEngine.IsTimeUp(s, move.HeldSeconds), Is.True);
            Assert.That(TurnRules.CommandTicks(s, move), Is.EqualTo(window), "capped at the window");
            var ev = BattleEngine.Apply(s, move);
            Assert.That(ev.OfType<TurnTimeUpEvent>().Select(t => t.HeroId), Does.Contain(fast.Id));
        }

        [Test]
        public void A_foe_icon_a_tick_away_never_leaves_too_little_time()
        {
            var s = Create(Stage(("goblin", 5, 0)), ("sprinter", 0, 6), ("plodder", 4, 6));
            var fast = Hero(s, "sprinter");
            ArriveIn(s, Foe(s), 1);
            Assert.That(TurnRules.TooLittleTime(s), Is.False, "GG-T1171: the frozen turn always holds an action");
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = fast.Id, Path = P((0, 6), (0, 5)) });
            Assert.That(ev.OfType<AbilityRefusedEvent>(), Is.Empty);
            Assert.That(fast.Pos, Is.EqualTo(new Coord(0, 5)));
        }

        [Test]
        public void Guard_protects_only_the_active_hero_and_banks_the_rest_of_its_window()
        {
            var s = Create(Stage(("goblin", 5, 0)), ("sprinter", 0, 6), ("plodder", 4, 6));
            var fast = Hero(s, "sprinter");
            var slow = Hero(s, "plodder");
            Assert.That(TurnRules.GuardProtects(s).Select(u => u.Id), Is.EqualTo(new[] { fast.Id }));
            const float held = 0.25f;
            int expected = BattleEngine.GuardChargeFor(BattleEngine.TurnWindowSeconds(s) - held);
            Assert.That(expected, Is.GreaterThan(0));
            Assert.That(TurnRules.GuardCharge(s, held), Is.EqualTo(expected));
            int damped = BattleEngine.DampedCharge(s, expected);
            var ev = BattleEngine.Apply(s, new ShieldCommand { HeldSeconds = held });
            var prot = ev.OfType<BuffAppliedEvent>().Where(b => b.BuffId == BattleConfig.ShieldBuffId).Select(b => b.UnitId).ToList();
            Assert.That(prot, Does.Contain(fast.Id));
            Assert.That(prot, Does.Not.Contain(slow.Id), "the waiting hero isn't covered");
            Assert.That(ev.OfType<ManaGainedEvent>().First(m => m.Reason == "guard").Amount, Is.EqualTo(damped));
        }

        [Test]
        public void A_fallen_active_hero_leaves_the_timeline_and_clears_the_active_hero()
        {
            var s = Create(Stage(("goblin", 5, 0)), ("sprinter", 0, 6), ("plodder", 4, 6));
            var fast = Hero(s, "sprinter");
            Assert.That(s.ActiveHeroId, Is.EqualTo(fast.Id));
            RunnerType.GetMethod("Kill", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(NewRunner(s), new object[] { fast, null });
            Assert.That(fast.Alive, Is.False);
            Assert.That(s.Timeline.Any(e => e.OwnerId == fast.Id), Is.False, "its icon left the timeline");
            Assert.That(s.ActiveHeroId, Is.EqualTo(-1));
            Assert.That(TurnRules.GuardProtects(s), Is.Empty);
        }

        [Test]
        public void A_hero_that_falls_as_its_turn_opens_hands_the_turn_to_the_next_hero()
        {
            // GG-T1101: a region rule's TurnEnd hook bites the active hero as its own icon arrives (ReturnControl).
            var db = Db();
            var bite = new RuleHookDef { On = RuleHook.TurnEnd, Team = RuleTeam.Hero };
            bite.Effects.Add(new RuleEffectDef { Kind = RuleEffectKind.Damage, Units = RuleUnits.Subject, Amount = 1 });
            var rule = new RegionRuleDef { Id = "bite-rule" };
            rule.Hooks.Add(bite);
            RegionRuleCatalog.Index(db, new List<RegionRuleDef> { rule });
            var stage = Stage(("goblin", 5, 0));
            stage.Rules.Add("bite-rule");
            var s = BattleEngine.Create(db, Setup(stage, 7, ("sprinter", 0, 6), ("knight", 2, 6), ("plodder", 4, 6)), new List<BattleEvent>());
            var fast = Hero(s, "sprinter");
            var knight = Hero(s, "knight");
            Assert.That(s.ActiveHeroId, Is.EqualTo(fast.Id));

            knight.Hp = 1;
            ArriveIn(s, knight, 2);
            foreach (var u in s.Units.Where(u => u.Alive && u.Id != knight.Id && u.Id != fast.Id && s.TurnEntryOf(u.Id) != null))
                Assert.That(Ticks(s, u), Is.GreaterThan(2), u.Def.Id + " arrives after the knight");

            var ev = BattleEngine.Apply(s, new ShieldCommand());
            Assert.That(ev.OfType<DiedEvent>().Select(d => d.UnitId), Does.Contain(knight.Id), "the knight fell as its turn opened");
            Assert.That(knight.Alive, Is.False);
            Assert.That(s.IsOver, Is.False);
            Assert.That(s.ActiveHeroId, Is.GreaterThanOrEqualTo(0), "the advance resumed to another hero's own icon");
            Assert.That(s.ActiveHeroId, Is.Not.EqualTo(knight.Id));
            var next = s.UnitById(s.ActiveHeroId);
            Assert.That(next.IsHero && next.Alive, Is.True);
            Assert.That(TurnRules.MayAct(s, next), Is.True);
        }

        [Test]
        public void A_revived_hero_gets_its_icon_back_and_every_other_icon_stays_put()
        {
            var s = Create(Stage(("goblin", 5, 0)), ("sprinter", 0, 6), ("knight", 2, 6), ("plodder", 4, 6));
            var fast = Hero(s, "sprinter");
            var slow = Hero(s, "plodder");
            RunnerType.GetMethod("Kill", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(NewRunner(s), new object[] { slow, null });
            Assert.That(slow.Alive, Is.False);
            Assert.That(s.TurnEntryOf(slow.Id), Is.Null);
            var before = s.Timeline.ToDictionary(e => e.Id, e => (e.Ip, e.RecoverTicks, e.StunTicks));

            RunnerType.GetMethod("Revive", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(NewRunner(s), new object[] { fast, 5 });
            Assert.That(slow.Alive, Is.True, "revived mid-wave");
            var icon = s.TurnEntryOf(slow.Id);
            Assert.That(icon, Is.Not.Null, "its turn icon is back on the timeline");
            Assert.That(TurnRules.IsHeroIcon(s, slow), Is.True);
            foreach (var e in s.Timeline.Where(e => e != icon))
            {
                Assert.That(before.ContainsKey(e.Id), Is.True, "no other icon was made");
                Assert.That((e.Ip, e.RecoverTicks, e.StunTicks), Is.EqualTo(before[e.Id]), "icon " + e.Id + " stayed where it was");
            }
            Assert.That(s.Timeline.Count, Is.EqualTo(before.Count + 1));
            Assert.That(s.ActiveHeroId, Is.EqualTo(fast.Id), "the active hero's turn is untouched");
        }

        #endregion

        #region GG-T1104: region rules, joins and triggers on each hero's own turn (GG-T810)

        /// <summary>The Mode B flood rule (TurnModeRoundTests' GG-T1073 region) on <see cref="Db"/>: at BattleStart (0,6) and
        /// (4,6) are flooded; a hero standing in the flood is soaked (Warm) at its TurnStart and its TurnEnd.</summary>
        private static BattleState Flooded(params (string cls, int x, int y)[] heroes) => Flooded(false, false, heroes);

        /// <param name="timed">BattleStart also schedules the timed "rise" (6 s) to flood (5,7).</param>
        /// <param name="summon">A hero's TurnEnd in the flood also brings a goblin at (5,2), a rule summon.</param>
        private static BattleState Flooded(bool timed, bool summon, params (string cls, int x, int y)[] heroes)
        {
            var db = Db();
            var rule = new RegionRuleDef { Id = "turn-flood", TileStates = { new TileStateDef { Id = "flood", Color = "#3B6FA0", Glyph = "~" } } };
            rule.Events.Add(new RuleEventDef
            {
                Id = "rise", Seconds = 6f, Hazard = false,
                Effects = { new RuleEffectDef { Kind = RuleEffectKind.MarkTiles, Area = RuleArea.EventTiles, State = "flood" } },
            });
            var start = new RuleHookDef { On = RuleHook.BattleStart };
            start.Effects.Add(new RuleEffectDef { Kind = RuleEffectKind.MarkTiles, Area = RuleArea.Tiles, Tiles = { new Coord(0, 6), new Coord(4, 6) }, State = "flood" });
            if (timed) start.Effects.Add(new RuleEffectDef { Kind = RuleEffectKind.Schedule, Area = RuleArea.Tiles, Tiles = { new Coord(5, 7) }, Event = "rise" });
            rule.Hooks.Add(start);
            foreach (var on in new[] { RuleHook.TurnStart, RuleHook.TurnEnd })
            {
                var hook = new RuleHookDef
                {
                    On = on, Team = RuleTeam.Hero, State = "flood",
                    Effects = { new RuleEffectDef { Kind = RuleEffectKind.ApplyBuff, Units = RuleUnits.Subject, Buff = "warm" } },
                };
                if (summon && on == RuleHook.TurnEnd)
                    hook.Effects.Add(new RuleEffectDef { Kind = RuleEffectKind.SpawnUnit, Area = RuleArea.Tiles, Tiles = { new Coord(5, 2) }, Class = "goblin", Level = 1 });
                rule.Hooks.Add(hook);
            }
            RegionRuleCatalog.Index(db, new List<RegionRuleDef> { rule });
            var stage = Stage(("goblin", 5, 0));
            stage.Rules.Add("turn-flood");
            return BattleEngine.Create(db, Setup(stage, 7, heroes), new List<BattleEvent>());
        }

        /// <summary>How often the flood rule's hook <paramref name="hook"/> (1 TurnStart, 2 TurnEnd) has matched.</summary>
        private static int Runs(BattleState s, int hook) => s.Region.HookCounts.TryGetValue("turn-flood#" + hook, out int n) ? n : 0;

        /// <summary>The active hero steps one tile up or down (its whole turn, which ends with the next hero's arrival).</summary>
        private static List<BattleEvent> Step(BattleState s)
        {
            var h = s.UnitById(s.ActiveHeroId);
            int dy = h.Pos.Y >= 6 ? -1 : 1;
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = h.Id, Path = new List<Coord> { new Coord(h.Pos.X, h.Pos.Y + dy) }, HeldSeconds = BattleEngine.MinDragSeconds });
            Assert.That(ev.OfType<AbilityRefusedEvent>(), Is.Empty, h.Def.Id + " steps");
            return ev;
        }

        [Test]
        public void Region_turn_hooks_run_once_per_hero_turn_and_only_for_the_hero_whose_icon_arrived()
        {
            var s = Flooded(("sprinter", 0, 6), ("plodder", 4, 6));
            var heroes = s.Units.Where(u => u.IsHero).ToList();
            foreach (var h in heroes) Assert.That(s.Region.At(h.Pos)?.State, Is.EqualTo("flood"), h.Def.Id + " stands in the flood");
            for (int i = 0; i < 4 && !s.IsOver; i++)
            {
                int starts = Runs(s, 1), ends = Runs(s, 2);
                foreach (var h in heroes) h.Buffs.RemoveAll(b => b.Def.Id == "warm");
                BattleEngine.Apply(s, new ShieldCommand()); // Guard: nobody leaves the water
                Assert.That(s.ActiveHeroId, Is.GreaterThanOrEqualTo(0), $"turn {i}: a hero's own icon arrived");
                Assert.That(Runs(s, 1), Is.EqualTo(starts + 1), $"turn {i}: one TurnStart, the arriving hero's, though both stand in the flood");
                Assert.That(Runs(s, 2), Is.EqualTo(ends + 1), $"turn {i}: and one TurnEnd");
                foreach (var h in heroes)
                    Assert.That(h.HasBuff("warm"), Is.EqualTo(h.Id == s.ActiveHeroId), $"turn {i}: only {h.Def.Id}'s own turn soaks it");
            }
        }

        [Test]
        public void Turn_triggers_fire_only_on_a_heros_own_turn_and_action_end_only_for_the_actor()
        {
            var db = Db();
            foreach (var (id, on) in new[] { ("t_start", TriggerOn.TurnStart), ("t_end", TriggerOn.TurnEnd), ("t_action", TriggerOn.ActionEnd) })
                db.Abilities[id] = new AbilityDef
                {
                    Id = id, Name = id, Kind = AbilityKind.Passive,
                    Trigger = new TriggerDef { On = on, Effects = { new TriggerEffect { Kind = TriggerEffectKind.ApplyBuff, Buff = "warm" } } },
                };
            var setup = Setup(Stage(("goblin", 5, 0)), 7, ("sprinter", 0, 6), ("plodder", 4, 6));
            foreach (var p in setup.Party) p.Passives = new List<string> { "t_start", "t_end", "t_action" };
            var s = BattleEngine.Create(db, setup, new List<BattleEvent>());
            var heroes = s.Units.Where(u => u.IsHero).ToList();
            int Fired(List<BattleEvent> ev, Unit u, TriggerOn on) => ev.OfType<TriggeredEvent>().Count(t => t.UnitId == u.Id && t.On == on);

            for (int i = 0; i < 4 && !s.IsOver; i++)
            {
                int actor = s.ActiveHeroId;
                var ev = Step(s);
                int next = s.ActiveHeroId;
                Assert.That(next, Is.GreaterThanOrEqualTo(0));
                foreach (var h in heroes)
                {
                    int own = h.Id == next ? 1 : 0;
                    Assert.That(Fired(ev, h, TriggerOn.TurnStart), Is.EqualTo(own), $"turn {i} {h.Def.Id}: TurnStart only as its own icon arrives");
                    Assert.That(Fired(ev, h, TriggerOn.TurnEnd), Is.EqualTo(own), $"turn {i} {h.Def.Id}: and TurnEnd with it");
                    Assert.That(Fired(ev, h, TriggerOn.ActionEnd), Is.EqualTo(h.Id == actor ? 1 : 0), $"turn {i} {h.Def.Id}: ActionEnd only for the actor");
                }
            }
        }

        [Test]
        public void A_tamed_ally_and_a_wanderer_keep_their_own_icons_and_count_their_own_turns()
        {
            var setup = Setup(Stage(("tank", 5, 0), ("wolf", 0, 2)), 7, ("sprinter", 0, 6), ("plodder", 4, 6));
            setup.Wanderer = new WandererSpec { Class = "mage", Wave = 0 };
            var s = BattleEngine.Create(Db(), setup, new List<BattleEvent>());
            var wolf = s.Units.First(u => u.Def.Id == "wolf");
            wolf.Team = Team.Hero; wolf.Tamed = true; wolf.TameTurnsLeft = 99; wolf.BondNeeded = 999;
            TurnRules.ClearActed(s, wolf);
            var wanderer = s.Units.Single(u => u.Wanderer);
            var allies = new[] { wolf, wanderer };
            var icons = allies.ToDictionary(a => a.Id, a => s.TurnEntryOf(a.Id));
            var before = allies.ToDictionary(a => a.Id, a => a.TurnsTaken);
            var turns = allies.ToDictionary(a => a.Id, a => 0);
            foreach (var a in allies)
            {
                Assert.That(a.IsIndependent, Is.True, a.Def.Id);
                Assert.That(TurnRules.IsHeroIcon(s, a), Is.False, a.Def.Id + " is no commanded hero");
                Assert.That(icons[a.Id], Is.Not.Null, a.Def.Id + " rides the timeline on its own icon");
            }

            int grew = 0;
            for (int i = 0; i < 6 && !s.IsOver; i++)
            {
                var ev = BattleEngine.Apply(s, new ShieldCommand());
                grew += ev.OfType<BondGrewEvent>().Count(b => b.UnitId == wolf.Id);
                foreach (var a in allies)
                {
                    turns[a.Id] += ev.OfType<TurnStartedEvent>().Count(t => t.UnitId == a.Id);
                    Assert.That(s.ActiveHeroId, Is.Not.EqualTo(a.Id), a.Def.Id + " never holds a hero's turn");
                    if (a.Alive) Assert.That(s.TurnEntryOf(a.Id), Is.SameAs(icons[a.Id]), a.Def.Id + " keeps its own icon; no hero icon is made for it");
                }
            }
            foreach (var a in allies) Assert.That(a.TurnsTaken - before[a.Id], Is.EqualTo(turns[a.Id]), a.Def.Id + " counts its own turns");
            Assert.That(turns.Values.Sum(), Is.GreaterThan(0), "the allies took turns on the clock");
            Assert.That(wolf.TameTurnsLeft, Is.EqualTo(99 - grew), "the tame counts the wolf's own turns, not hero turns");
        }

        [Test]
        public void A_timed_rule_event_lands_on_the_clock_tick_it_was_scheduled_for_whatever_hero_turns_pass()
        {
            var s = Flooded(true, false, ("sprinter", 0, 6), ("plodder", 4, 6));
            var rise = s.Timeline.Single(e => e.Kind == TimelineKind.Hazard && e.RuleEvent == "rise");
            long due = s.Clock + BattleEngine.TicksUntilAct(s, rise);
            Assert.That(rise.TotalCastTicks, Is.EqualTo(6 * BattleConfig.TicksPerSecond), "scheduled 6 s out");
            Assert.That(due, Is.GreaterThan(s.Clock));

            int turns = 0;
            for (int i = 0; i < 30 && !s.IsOver; i++)
            {
                long before = s.Clock;
                var ev = BattleEngine.Apply(s, new ShieldCommand());
                turns++;
                if (ev.OfType<HazardLandedEvent>().Any(h => h.EventId == "rise"))
                {
                    Assert.That(before, Is.LessThan(due), "it had not landed before this hero turn");
                    Assert.That(s.Clock, Is.GreaterThanOrEqualTo(due), "it landed within the clock this hero turn ran");
                    Assert.That(s.Region.At(new Coord(5, 7))?.State, Is.EqualTo("flood"), "the water rose");
                    Assert.That(turns, Is.GreaterThan(1), "hero turns passed while it waited");
                    return;
                }
                Assert.That(s.Timeline, Does.Contain(rise), $"turn {i}: still pending");
                Assert.That(s.Clock + BattleEngine.TicksUntilAct(s, rise), Is.EqualTo(due), $"turn {i}: a hero turn neither hastens nor delays it");
            }
            Assert.Fail("the water never rose");
        }

        [Test]
        public void A_recruit_joins_on_its_own_hero_icon_and_every_other_icon_stays_put()
        {
            BattleState Recruiting()
            {
                var stage = Stage(("goblin", 5, 0));
                stage.Waves[0].Recruitables.Add(new RecruitableDef { Class = "mage", X = 1, Y = 5 });
                return BattleEngine.Create(Db(), Setup(stage, 7, ("sprinter", 1, 6), ("plodder", 4, 6)), new List<BattleEvent>());
            }

            // The join itself (DoTalk): ScheduleHeroIcons(onlyMissing) leaves every other icon where it is.
            var s = Recruiting();
            var fast = Hero(s, "sprinter");
            var mage = s.Units.Single(u => u.Def.Id == "mage");
            Assert.That(mage.IsNeutral, Is.True);
            Assert.That(s.ActiveHeroId, Is.EqualTo(fast.Id));
            var before = s.Timeline.Where(e => e.OwnerId != mage.Id).ToDictionary(e => e.Id, e => (e.Ip, e.RecoverTicks, e.StunTicks));
            bool joined = (bool)RunnerType.GetMethod("DoTalk", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(NewRunner(s), new object[] { new TalkCommand { HeroId = fast.Id, TargetId = mage.Id } });
            Assert.That(joined, Is.True);
            Assert.That(mage.IsCommandable, Is.True);
            var icon = s.TurnEntryOf(mage.Id);
            Assert.That(icon, Is.Not.Null, "the recruit rides the timeline on its own icon");
            Assert.That(TurnRules.IsHeroIcon(s, mage), Is.True, "a commanded hero's icon");
            foreach (var e in s.Timeline.Where(e => e != icon))
            {
                Assert.That(before.ContainsKey(e.Id), Is.True, "no other icon was made");
                Assert.That((e.Ip, e.RecoverTicks, e.StunTicks), Is.EqualTo(before[e.Id]), "icon " + e.Id + " stayed where it was");
            }
            Assert.That(s.ActiveHeroId, Is.EqualTo(fast.Id), "the talker's turn is untouched");

            // Through the command: Talk is the talker's turn, and the recruit then holds hero turns of its own.
            s = Recruiting();
            fast = Hero(s, "sprinter");
            mage = s.Units.Single(u => u.Def.Id == "mage");
            var talk = BattleEngine.Apply(s, new TalkCommand { HeroId = fast.Id, TargetId = mage.Id });
            Assert.That(talk.OfType<RecruitJoinedEvent>().Count(), Is.EqualTo(1));
            Assert.That(TurnRules.IsHeroIcon(s, mage), Is.True);
            Assert.That(s.TurnEntryOf(mage.Id), Is.Not.Null);
            bool held = s.ActiveHeroId == mage.Id;
            for (int i = 0; i < 8 && !held && !s.IsOver; i++)
            {
                BattleEngine.Apply(s, new ShieldCommand());
                held = s.ActiveHeroId == mage.Id;
            }
            Assert.That(held, Is.True, "the recruit's own icon arrived as a hero turn");
        }

        [Test]
        public void A_rule_summon_gets_a_foe_icon_and_never_holds_a_heros_turn()
        {
            var s = Flooded(false, true, ("sprinter", 0, 6), ("plodder", 4, 6));
            var summoned = new HashSet<int>();
            for (int i = 0; i < 6 && !s.IsOver; i++)
            {
                var ev = BattleEngine.Apply(s, new ShieldCommand());
                foreach (var id in ev.OfType<ReinforcementsArrivedEvent>().SelectMany(r => r.Spawned)) summoned.Add(id);
                Assert.That(summoned, Does.Not.Contain(s.ActiveHeroId), $"turn {i}: a summon never becomes the active hero");
                foreach (var id in summoned)
                {
                    var u = s.UnitById(id);
                    if (!u.Alive) continue;
                    Assert.That(u.Team, Is.EqualTo(Team.Enemy), u.Def.Id);
                    Assert.That(s.TurnEntryOf(u.Id), Is.Not.Null, u.Def.Id + " rides the timeline on its own icon");
                    Assert.That(TurnRules.IsHeroIcon(s, u), Is.False, u.Def.Id + " has a foe icon");
                }
            }
            Assert.That(summoned, Is.Not.Empty, "a hero's TurnEnd in the flood summoned a goblin");
        }

        [Test]
        public void A_wave_guest_gets_a_hero_icon_at_its_waves_opening()
        {
            var stage = StageWaves(new[] { ("imp", 5, 0) }, new[] { ("imp", 5, 1) });
            stage.Waves[1].Guests.Add(new SpawnDef { Class = "mage", X = 2, Y = 6 });
            var s = Create(stage, ("sprinter", 0, 6), ("plodder", 4, 6));
            Assert.That(s.Units.Any(u => u.Def.Id == "mage"), Is.False, "the guest waits for wave 2");

            var imp = Foe(s);
            imp.Hp = 1;
            imp.Buffs.Add(new BuffInstance { Def = s.Content.Buffs["burning"], Remaining = 5 });
            List<BattleEvent> opening = null;
            for (int i = 0; i < 200 && s.Wave == 0 && !s.IsOver; i++) opening = BattleEngine.RunClock(s, 1);
            Assert.That(s.Wave, Is.EqualTo(1), "wave 2 began");
            Assert.That(opening.OfType<GuestJoinedEvent>().Count(), Is.EqualTo(1), "the guest joined at the wave's opening");

            var guest = s.Units.Single(u => u.Def.Id == "mage");
            Assert.That(guest.IsGuest && guest.IsCommandable, Is.True);
            Assert.That(s.TurnEntryOf(guest.Id), Is.Not.Null, "it rides the timeline on its own icon");
            Assert.That(TurnRules.IsHeroIcon(s, guest), Is.True, "a commanded hero's icon");
            AssertHeroesOpen(s, s.Units.Where(u => u.IsHero && u.Alive).ToList(), "wave 2 with its guest");
        }

        [Test]
        public void The_countdown_badge_counts_a_hero_icon_arriving_before_the_foes()
        {
            var s = Create(Stage(("goblin", 5, 0)), ("sprinter", 0, 6), ("plodder", 4, 6));
            var fast = Hero(s, "sprinter");
            var slow = Hero(s, "plodder");
            var foe = Foe(s);
            Assert.That(s.CastEntryOf(foe.Id), Is.Null);
            ArriveIn(s, slow, 2);
            ArriveIn(s, foe, 5);
            ArriveIn(s, fast, 50);
            Assert.That(TurnRules.CountdownLeft(s, foe.Id), Is.EqualTo(1), "the plodder's hero turn comes first; the sprinter's after");
            Assert.That(BattleEngine.ActionsUntilTurn(s, foe.Id), Is.EqualTo(1));

            ArriveIn(s, slow, 6);
            Assert.That(TurnRules.CountdownLeft(s, foe.Id), Is.EqualTo(0), "0 = the foe's icon is the next to arrive");
        }

        #endregion

        #region GG-T1105: the enemy AI is blind to whose icon arrives next (GG-T812)

        /// <summary>GG-T1105, decided: foes don't prefer (or avoid) the hero whose icon arrives next. Targeting (taunt,
        /// personality, distance, HP, id), the step and the ranged pick read the board and HP, never
        /// <see cref="BattleState.ActiveHeroId"/>, so a foe's choices are the same whichever hero holds the turn.</summary>
        [Test]
        public void Enemy_targeting_ignores_whose_turn_it_is()
        {
            var s = Create(Stage(("goblin", 3, 2)), ("sprinter", 0, 6), ("knight", 3, 6), ("plodder", 5, 6));
            var foe = Foe(s);
            var target = EnemyPlanner.PickTarget(s, foe);
            var ranged = EnemyPlanner.RangedTarget(s, foe);
            var step = EnemyPlanner.PlanStep(s, foe);
            Assert.That(target, Is.Not.Null);
            int active = s.ActiveHeroId;
            foreach (int id in s.Units.Where(u => u.IsHero).Select(u => u.Id).Concat(new[] { -1 }))
            {
                s.ActiveHeroId = id;
                Assert.That(EnemyPlanner.PickTarget(s, foe), Is.SameAs(target), "target with hero " + id + " to act");
                Assert.That(EnemyPlanner.RangedTarget(s, foe), Is.SameAs(ranged), "ranged pick with hero " + id + " to act");
                Assert.That(EnemyPlanner.PlanStep(s, foe), Is.EqualTo(step), "step with hero " + id + " to act");
            }
            s.ActiveHeroId = active;
        }

        /// <summary>GG-T1105: a foe may push back the hero whose icon arrives next (no hero exception), and it never
        /// soft-locks: the push only adds bounded time, so that hero's icon still arrives.</summary>
        [Test]
        public void A_push_on_the_next_hero_only_delays_its_icon()
        {
            var s = Create(Stage(("goblin", 5, 0)), ("sprinter", 0, 6), ("knight", 2, 6), ("plodder", 4, 6));
            var next = s.Units.Where(u => u.IsHero && u.Id != s.ActiveHeroId).OrderBy(u => Ticks(s, u)).First();
            int before = Ticks(s, next);
            Assert.That(before, Is.GreaterThan(0));

            var runner = NewRunner(s);
            RunnerType.GetMethod("Pushback", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(runner, new object[] { next, 1f });
            int after = Ticks(s, next);
            Assert.That(after, Is.GreaterThan(before), "the push lands on the hero about to act");
            int cap = Formulas.SecondsToTicks(BattleConfig.PushbackMaxSeconds);
            Assert.That(after, Is.LessThanOrEqualTo(before + cap + BattleEngine.TurnRatePerTick(s, next) * cap),
                "the delay is bounded, so the icon still arrives");
        }

        #endregion

        #region GG-T1103: Sleep skips, own-turn counting, Frozen thaw and icon rate (GG-T1100, GG-T809, GG-T1102)

        /// <summary><see cref="Db"/> plus a five-turn "ward" (Turns), and tick-unit "slowed" (×0.5) and "hasted" (×2) icon rates.</summary>
        private static BattleState Warded(string foe, params (string cls, int x, int y)[] heroes)
        {
            var db = Db();
            db.Buffs["ward"] = new BuffDef { Id = "ward", Name = "Ward", Kind = BuffKind.Buff, Unit = DurationUnit.Turns, Duration = 5 };
            db.Buffs["slowed"] = new BuffDef { Id = "slowed", Name = "Slowed", Unit = DurationUnit.Ticks, Duration = 999, TimelineSpeedMultiplier = 0.5f };
            db.Buffs["hasted"] = new BuffDef { Id = "hasted", Name = "Hasted", Kind = BuffKind.Buff, Unit = DurationUnit.Ticks, Duration = 999, TimelineSpeedMultiplier = 2f };
            return BattleEngine.Create(db, Setup(Stage((foe, 5, 0)), 7, heroes), new List<BattleEvent>());
        }

        private static BuffInstance Give(BattleState s, Unit u, string id, int remaining)
        {
            var b = new BuffInstance { Def = s.Content.Buffs[id], Remaining = remaining, Fresh = false };
            u.Buffs.Add(b);
            return b;
        }

        [Test]
        public void A_slept_heros_arrived_icon_reenters_without_becoming_active_and_ticks_its_turn_buffs_and_cooldowns()
        {
            var s = Warded("imp", ("sprinter", 0, 6), ("plodder", 4, 6));
            var fast = Hero(s, "sprinter");
            var slow = Hero(s, "plodder");
            Assert.That(s.ActiveHeroId, Is.EqualTo(fast.Id));
            var sleep = Give(s, slow, "sleep", 3);
            var ward = Give(s, slow, "ward", 5);
            var fastWard = Give(s, fast, "ward", 5);
            slow.Cooldowns["x"] = 3;
            fast.Cooldowns["x"] = 3;
            ArriveIn(s, slow, 1);
            Assert.That(Ticks(s, Foe(s)), Is.GreaterThan(1));
            Assert.That(Ticks(s, fast), Is.GreaterThan(1));

            var ev = BattleEngine.RunClock(s, 1);
            Assert.That(ev.OfType<TurnSkippedEvent>().Single(t => t.UnitId == slow.Id).Reason, Is.EqualTo("asleep"));
            Assert.That(s.ActiveHeroId, Is.EqualTo(fast.Id), "the sleeper never became the active hero");
            Assert.That(TurnRules.NotThisHerosTurn(s, slow), Is.True);
            Assert.That(sleep.Remaining, Is.EqualTo(2), "Sleep itself counts the lost turn");
            Assert.That(ward.Remaining, Is.EqualTo(4), "its Turns buffs tick");
            Assert.That(slow.Cooldowns["x"], Is.EqualTo(2), "its cooldowns tick");
            Assert.That(fastWard.Remaining, Is.EqualTo(5), "another hero's buffs don't");
            Assert.That(fast.Cooldowns["x"], Is.EqualTo(3));
            Assert.That(s.TurnEntryOf(slow.Id), Is.Not.Null, "its icon stays on the timeline");
            Assert.That(Ticks(s, slow), Is.GreaterThan(1), "and re-entered on its cycle");
        }

        [Test]
        public void Only_the_active_hero_counts_its_turn_and_a_waiting_hero_ticks_only_a_halting_debuff()
        {
            var s = Warded("imp", ("sprinter", 0, 6), ("plodder", 4, 6));
            var fast = Hero(s, "sprinter");
            var slow = Hero(s, "plodder");
            Assert.That(s.ActiveHeroId, Is.EqualTo(fast.Id));
            var slowWard = Give(s, slow, "ward", 5);
            var fastWard = Give(s, fast, "ward", 5);
            slow.Cooldowns["x"] = 3;
            fast.Cooldowns["x"] = 3;
            ArriveIn(s, slow, 3);
            Assert.That(Ticks(s, Foe(s)), Is.GreaterThan(3), "the plodder's icon arrives first");
            Assert.That(Ticks(s, fast), Is.GreaterThan(3));

            BattleEngine.Apply(s, new ShieldCommand());
            Assert.That(s.ActiveHeroId, Is.EqualTo(slow.Id), "the plodder's own icon arrived");
            Assert.That(slowWard.Remaining, Is.EqualTo(4), "the active hero counts its own turn");
            Assert.That(slow.Cooldowns["x"], Is.EqualTo(2));
            Assert.That(fastWard.Remaining, Is.EqualTo(5), "the waiting hero's Turns buff holds");
            Assert.That(fast.Cooldowns["x"], Is.EqualTo(3), "and so do its cooldowns");

            // A halting debuff on the waiting hero does count down on another hero's turn.
            var frozen = Give(s, fast, "frozen", 2);
            ArriveIn(s, slow, 3);
            Assert.That(Ticks(s, Foe(s)), Is.GreaterThan(3));
            BattleEngine.Apply(s, new ShieldCommand());
            Assert.That(s.ActiveHeroId, Is.EqualTo(slow.Id));
            Assert.That(frozen.Remaining, Is.EqualTo(1), "Frozen ticked on the plodder's turn");
            Assert.That(fastWard.Remaining, Is.EqualTo(5), "the ward still held");
        }

        [Test]
        public void A_frozen_hero_thaws_on_foe_turns_while_its_icon_holds()
        {
            var s = Warded("imp", ("sprinter", 0, 6), ("plodder", 4, 6));
            var fast = Hero(s, "sprinter");
            var slow = Hero(s, "plodder");
            var foe = Foe(s);
            var frozen = Give(s, slow, "frozen", 2);
            var icon = s.TurnEntryOf(slow.Id);
            var held = (icon.Ip, icon.RecoverTicks, icon.StunTicks);

            for (int turn = 1; turn <= 2; turn++)
            {
                ArriveIn(s, foe, 1);
                Assert.That(Ticks(s, fast), Is.GreaterThan(1));
                var ev = BattleEngine.RunClock(s, 1);
                Assert.That(ev.OfType<TurnStartedEvent>().Select(t => t.UnitId), Does.Contain(foe.Id), $"foe turn {turn}");
                if (turn == 1)
                {
                    Assert.That(frozen.Remaining, Is.EqualTo(1), "the halt counted down on the foe's turn");
                    Assert.That((icon.Ip, icon.RecoverTicks, icon.StunTicks), Is.EqualTo(held), "the frozen icon held");
                }
            }
            Assert.That(slow.IsTimelineHalted, Is.False, "thawed after two foe turns");
            Assert.That(slow.HasBuff("frozen"), Is.False);
            var before = (icon.Ip, icon.RecoverTicks);
            BattleEngine.RunClock(s, 1);
            Assert.That((icon.Ip, icon.RecoverTicks), Is.Not.EqualTo(before), "its icon moves again");
        }

        [Test]
        public void Slow_and_haste_on_a_hero_change_its_own_icon_rate_and_leave_the_foes_alone()
        {
            var s = Warded("imp", ("sprinter", 0, 6), ("plodder", 4, 6));
            var slow = Hero(s, "plodder");
            var foe = Foe(s);
            int Rate(float m) => Math.Max(1, (int)Math.Round(BattleConfig.BaseIpPerTick * m));
            float tempo = BattleEngine.EnemyTempoMultiplier(s);
            int foeRate = BattleEngine.TurnRatePerTick(s, foe);
            ArriveIn(s, slow, 10);
            Assert.That(BattleEngine.TimelineScale(s, slow), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(BattleEngine.TurnRatePerTick(s, slow), Is.EqualTo(Rate(1f)));
            int plain = Ticks(s, slow);

            var slowed = Give(s, slow, "slowed", 999);
            Assert.That(BattleEngine.TimelineScale(s, slow), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(BattleEngine.TurnRatePerTick(s, slow), Is.EqualTo(Rate(0.5f)));
            Assert.That(Ticks(s, slow), Is.GreaterThan(plain), "Slow: its icon arrives later");
            slow.Buffs.Remove(slowed);

            Give(s, slow, "hasted", 999);
            Assert.That(BattleEngine.TimelineScale(s, slow), Is.EqualTo(2f).Within(1e-5f));
            Assert.That(BattleEngine.TurnRatePerTick(s, slow), Is.EqualTo(Rate(2f)));
            Assert.That(Ticks(s, slow), Is.LessThan(plain), "Haste: its icon arrives sooner");

            Assert.That(BattleEngine.EnemyTempoMultiplier(s), Is.EqualTo(tempo), "party tempo untouched");
            Assert.That(BattleEngine.TurnRatePerTick(s, foe), Is.EqualTo(foeRate), "the foe's icon rate untouched");
        }

        [Test]
        public void Hasten_on_a_hero_rushes_its_own_icon_and_leaves_party_tempo_alone()
        {
            // GG-T1102: under HeroTimeline the haste lands on the hero's icon alone; party tempo would count it twice.
            var s = Warded("imp", ("sprinter", 0, 6), ("plodder", 4, 6));
            var slow = Hero(s, "plodder");
            var foe = Foe(s);
            int net = s.NetTempo;
            float tempo = BattleEngine.EnemyTempoMultiplier(s);
            int foeTicks = Ticks(s, foe);
            int before = Ticks(s, slow);
            Assert.That(before, Is.GreaterThan(1));

            RunnerType.GetMethod("Hasten", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(NewRunner(s), new object[] { slow, 0.3f, null, 0 });
            Assert.That(Ticks(s, slow), Is.LessThan(before), "its own icon was rushed");
            Assert.That(s.NetTempo, Is.EqualTo(net), "no party tempo");
            Assert.That(BattleEngine.EnemyTempoMultiplier(s), Is.EqualTo(tempo));
            Assert.That(Ticks(s, foe), Is.EqualTo(foeTicks), "the foe's icon didn't slow");
        }

        #endregion

        // BattleRunner is internal and its tie rule private: read it by reflection rather than widen it for a test.
        private static readonly Type RunnerType = typeof(BattleEngine).Assembly.GetType("GridGame.Core.Battle.BattleRunner", throwOnError: true);

        private static object NewRunner(BattleState s) =>
            Activator.CreateInstance(RunnerType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new object[] { s, new List<BattleEvent>() }, null);

        private static bool Earlier(object runner, TimelineEntry a, TimelineEntry b) =>
            (bool)RunnerType.GetMethod("Earlier", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(runner, new object[] { a, b });

        private static int ArrivalRank(TimelineKind k) =>
            (int)RunnerType.GetMethod("ArrivalRank", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Invoke(null, new object[] { k });
    }
}
