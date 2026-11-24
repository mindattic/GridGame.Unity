using System.Collections.Generic;
using System.Linq;
using System.Text;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Bot;
using GridGame.Core.Content;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>
    /// GG-T781 (bible §2.13.1): <see cref="TurnMode.AnyHeroEveryTurn"/> through the <see cref="TurnRules"/> seam. Each
    /// party turn exactly one hero acts — any able hero, the same one again if you like — and that action ends the turn:
    /// the clock runs on to the next enemy arrival and the seconds it left are lost (only Guard banks time). The minimum
    /// action gap, forced Guard and Rally still apply, and a battle replays identically from its commands. GG-T782: the
    /// per-turn systems (cooldowns, Turns buffs, tempo, Protection, Frozen) count party turns, and the drag-timer time bank
    /// mints at most <see cref="BattleConfig.AnyHeroMaxTimeBankOrbs"/> per drop. GG-T783: pickups, corpses, region turn hooks
    /// and timed rule events, turn triggers, foes', tamed allies' and wanderers' own turns, recruits and the countdown badge
    /// count party turns, own turns or the clock, never hero actions.
    /// </summary>
    public class TurnModeAnyHeroTests
    {
        private static (BattleState s, List<BattleEvent> ev) Battle(ContentDb db, StageDef stage, params (string cls, int x, int y)[] heroes)
        {
            var setup = new BattleSetup { Stage = stage, Seed = 7, UnlockedSlots = 5, TurnMode = TurnMode.AnyHeroEveryTurn };
            foreach (var h in heroes) setup.Party.Add(new HeroSpec { ClassId = h.cls, Start = new Coord(h.x, h.y) });
            var ev = new List<BattleEvent>();
            return (BattleEngine.Create(db, setup, ev), ev);
        }

        private static (BattleState s, List<BattleEvent> ev) Trio() =>
            Battle(Mini(), Stage(("goblin", 5, 0)), ("knight", 0, 6), ("cleric", 2, 6), ("mage", 4, 6));

        private static List<BattleEvent> Step(BattleState s, Unit h, int dy) =>
            BattleEngine.Apply(s, new MoveCommand { HeroId = h.Id, Path = new List<Coord> { new Coord(h.Pos.X, h.Pos.Y + dy) }, HeldSeconds = BattleEngine.MinDragSeconds });

        private static bool NewTurn(List<BattleEvent> ev) => ev.OfType<PlayerControlEvent>().Any(p => p.NewWindow);

        [Test]
        public void Every_able_hero_may_act_at_the_start_of_a_turn()
        {
            var (s, _) = Trio();
            Assert.That(s.TurnMode, Is.EqualTo(TurnMode.AnyHeroEveryTurn));
            Assert.That(BattleEngine.HeroesLeftToAct(s).Count, Is.EqualTo(3));
            foreach (var h in s.Units.Where(u => u.IsHero))
            {
                Assert.That(BattleEngine.CanMove(s, h.Id), Is.True, h.Def.Id);
                Assert.That(TurnRules.HasActed(s, h), Is.False, h.Def.Id);
            }
        }

        [Test]
        public void One_action_ends_the_party_turn_and_the_clock_runs_to_the_next_enemy_arrival()
        {
            var (s, _) = Trio();
            var goblin = s.Units.First(u => u.Def.Id == "goblin");
            long before = s.Clock;
            int toArrival = BattleEngine.TurnTicks(s);
            Assert.That(toArrival, Is.GreaterThan(BattleEngine.NeedTicks(BattleEngine.MinDragSeconds)), "the opening gap fits a drag");

            var ev = Step(s, Hero(s, "knight"), -1);
            Assert.That(ev.OfType<AbilityRefusedEvent>(), Is.Empty);
            Assert.That(ev.OfType<TurnStartedEvent>().Select(t => t.UnitId), Does.Contain(goblin.Id), "the enemy acted in the same command");
            Assert.That(NewTurn(ev), Is.True, "one hero's action is the whole party turn");
            Assert.That(s.Clock - before, Is.GreaterThanOrEqualTo(toArrival), "the leftover seconds ran out on the clock");
            Assert.That(BattleEngine.HeroesLeftToAct(s).Count, Is.EqualTo(3), "a fresh turn: everyone may act again");
            Assert.That(s.Units.Any(u => u.ActedThisWindow), Is.False);
        }

        [Test]
        public void The_same_hero_may_act_twice_in_a_row()
        {
            var (s, _) = Trio();
            var knight = Hero(s, "knight");
            var first = Step(s, knight, -1);
            Assert.That(first.OfType<AbilityRefusedEvent>(), Is.Empty);
            Assert.That(NewTurn(first), Is.True);

            Assert.That(BattleEngine.CanMove(s, knight.Id), Is.True, "last turn's hero may be picked up again");
            var second = Step(s, knight, -1);
            Assert.That(second.OfType<AbilityRefusedEvent>(), Is.Empty);
            Assert.That(NewTurn(second), Is.True);
            Assert.That(knight.Pos, Is.EqualTo(new Coord(0, 4)));
            Assert.That(s.Results.ActionsTaken, Is.EqualTo(2));
        }

        [Test]
        public void A_second_action_in_one_turn_is_refused()
        {
            var (s, _) = Trio();
            var knight = Hero(s, "knight");
            var cleric = Hero(s, "cleric");
            // The knight has taken the party's one action and the turn hasn't turned over yet.
            TurnRules.MarkActed(s, knight);
            Assert.That(BattleEngine.HeroesLeftToAct(s), Is.Empty, "the turn is spent");
            foreach (var h in s.Units.Where(u => u.IsHero))
            {
                Assert.That(TurnRules.HasActed(s, h), Is.True, h.Def.Id + " reads as acted");
                Assert.That(BattleEngine.CanMove(s, h.Id), Is.False, h.Def.Id);
            }
            cleric.Bar[0] = new AbilitySlot { AbilityId = "heal" };
            Assert.That(BattleEngine.GetSlotStatus(s, cleric.Id, 0), Is.EqualTo(SlotStatus.Acted));
            Assert.That(TurnRules.EndsPartyTurn(s, new MoveCommand { HeroId = knight.Id }), Is.True);
            Assert.That(TurnRules.TooLittleTime(s), Is.False, "nobody is left to act, so it is no forced Guard");

            long clock = s.Clock;
            var other = Step(s, cleric, -1);
            Assert.That(other.OfType<AbilityRefusedEvent>().Single().Reason, Is.EqualTo("Already acted"));
            var again = Step(s, knight, -1);
            Assert.That(again.OfType<AbilityRefusedEvent>().Single().Reason, Is.EqualTo("Already acted"));
            Assert.That(s.Clock, Is.EqualTo(clock), "a refused command spends nothing");
            Assert.That(s.Results.ActionsTaken, Is.EqualTo(0));
        }

        [Test]
        public void Guard_banks_the_unused_turn_like_party_window()
        {
            var (s, _) = Trio();
            Assert.That(BattleEngine.GuardCharge(s), Is.EqualTo(BattleEngine.GuardChargeFor(BattleEngine.TurnWindowSeconds(s))));
            Assert.That(BattleEngine.GuardCharge(s, 1f), Is.EqualTo(BattleEngine.GuardChargeFor(BattleEngine.TurnWindowSeconds(s) - 1f)));
            var ev = BattleEngine.Apply(s, new ShieldCommand());
            Assert.That(NewTurn(ev), Is.True, "Guard gives the turn up");
        }

        [Test]
        public void Minimum_gap_forced_guard_and_rally_still_apply()
        {
            // MinActionTimeTests' lock: seven equal wolves evenly spread over one cycle, every gap too short to act in.
            var (s, _) = Battle(Mini(), Stage(("wolf", 0, 0), ("wolf", 1, 0), ("wolf", 2, 0), ("wolf", 3, 0), ("wolf", 4, 0), ("wolf", 5, 0), ("wolf", 0, 1)), ("knight", 5, 7), ("cleric", 3, 7));
            var w = s.Units.Where(u => u.Def.Id == "wolf" && u.Alive).OrderBy(u => u.Id).ToList();
            int rate = BattleEngine.TurnRatePerTick(s, w[0]);
            int lap = BattleEngine.NeedTicks(BattleConfig.RecoverBaseSeconds) + (BattleConfig.BarLength - BattleEngine.EntryIp(w[0]) + rate - 1) / rate;
            for (int i = 0; i < w.Count; i++) s.TurnEntryOf(w[i].Id).Ip = BattleConfig.BarLength - rate * ((i + 1) * lap / w.Count);
            Assert.That(BattleEngine.TooLittleTime(s), Is.True);

            var k = Hero(s, "knight");
            var refused = Step(s, k, -1);
            Assert.That(refused.OfType<AbilityRefusedEvent>().Single().Reason, Is.EqualTo("Too little time"));
            Assert.That(TurnRules.IsForcedGuard(s, new ShieldCommand()), Is.True);

            int guards = 0;
            while (BattleEngine.TooLittleTime(s) && guards < 12 && !s.IsOver) { BattleEngine.Apply(s, new ShieldCommand()); guards++; }
            Assert.That(BattleEngine.TooLittleTime(s), Is.False, "the rally opened a gap");
            Assert.That(s.Rally, Is.InRange(1, BattleConfig.RallyMaxStacks));

            var move = Step(s, k, -1);
            Assert.That(move.OfType<AbilityRefusedEvent>(), Is.Empty);
            Assert.That(s.Rally, Is.EqualTo(0), "a hero action ends the rally");
        }

        [Test]
        public void Heroes_act_first_in_every_wave()
        {
            var (s, _) = Battle(Mini(), StageWaves(new[] { ("goblin", 2, 3) }, new[] { ("goblin", 3, 0) }), ("knight", 2, 2), ("cleric", 2, 5));
            Assert.That(BattleEngine.ActionFits(s, BattleConfig.OpeningWindowSeconds), Is.True, "the opening window of wave 1");
            At(s, 2, 3).Hp = 1;
            // The cleric closes a pincer on the lone goblin: one action clears wave 1.
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = Hero(s, "cleric").Id, Path = P((2, 4)), HeldSeconds = BattleEngine.MinDragSeconds });
            Assert.That(ev.OfType<AbilityRefusedEvent>(), Is.Empty);
            Assert.That(s.IsOver, Is.False);
            Assert.That(s.Wave, Is.EqualTo(1), "wave 1 fell to the pincer");
            Assert.That(NewTurn(ev), Is.True, "clearing a wave opens a fresh turn");
            Assert.That(BattleEngine.HeroesLeftToAct(s).Count, Is.EqualTo(2));
            Assert.That(BattleEngine.ActionFits(s, BattleEngine.MinDragSeconds), Is.True, "the new wave opens with room to act");
        }

        // ── GG-T782: per-turn systems under one action per turn ──────────────────────────────────

        [Test]
        public void A_drop_banks_at_most_one_time_bank_orb()
        {
            // AiAndSystemsTests' PartyWindow case mints 3 for the same drop; here every drop is the turn's only action and
            // an enemy turn always follows, so the cap is the per-action share (§2.13.1, AnyHeroMaxTimeBankOrbs).
            var (s, _) = Battle(Mini(), Stage(("wolf", 0, 0)), ("knight", 5, 7), ("cleric", 4, 7));
            var wolf = s.Units.First(u => u.Def.Id == "wolf");
            s.TurnEntryOf(wolf.Id).Ip = BattleConfig.BarLength - BattleEngine.TurnRatePerTick(s, wolf) * 10; // a 1 s gap, room for a drag
            Assert.That(TurnRules.MaxTimeBankOrbs(s), Is.EqualTo(BattleConfig.AnyHeroMaxTimeBankOrbs));
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = Hero(s, "knight").Id, Path = P((5, 6)), TimeLeftSeconds = 3.9f, HeldSeconds = BattleEngine.MinDragSeconds });
            Assert.That(ev.OfType<AbilityRefusedEvent>(), Is.Empty);
            Assert.That(ev.OfType<OrbMintedEvent>().Count(o => o.Reason == "time bank"), Is.EqualTo(BattleConfig.AnyHeroMaxTimeBankOrbs));
            Assert.That(s.PendingTimeBankSeconds, Is.EqualTo(0f), "the remainder is used up at the handoff");
        }

        [Test]
        public void The_party_window_time_bank_cap_is_unchanged()
        {
            var (s, _) = TestKit.Battle(Mini(), Stage(("wolf", 0, 0)), ("knight", 5, 7));
            Assert.That(s.TurnMode, Is.EqualTo(TurnMode.PartyWindow));
            Assert.That(TurnRules.MaxTimeBankOrbs(s), Is.EqualTo(BattleConfig.MaxTimeBankOrbs));
        }

        [Test]
        public void Cooldowns_count_party_turns_even_for_a_hero_who_did_not_act()
        {
            var (s, _) = Trio();
            var knight = Hero(s, "knight");
            var cleric = Hero(s, "cleric");
            knight.Cooldowns["shield_bash"] = 2;
            Assert.That(NewTurn(Step(s, cleric, -1)), Is.True);
            Assert.That(knight.Cooldowns["shield_bash"], Is.EqualTo(1), "one party turn passed");
            Assert.That(NewTurn(Step(s, cleric, -1)), Is.True);
            Assert.That(knight.Cooldowns["shield_bash"], Is.EqualTo(0), "two party turns: ready again");
        }

        [Test]
        public void Turns_buffs_and_tempo_count_party_turns()
        {
            var (s, _) = Trio();
            var knight = Hero(s, "knight");
            var mage = Hero(s, "mage");
            var ward = new BuffDef { Id = "test_ward", Name = "Ward", Kind = BuffKind.Buff, Unit = DurationUnit.Turns, Duration = 2 };
            mage.Buffs.Add(new BuffInstance { Def = ward, Remaining = 2 });
            s.Tempo.Add(new TempoEffect { Source = "test", Amount = 1, TurnsLeft = 2, Fresh = false });

            Assert.That(NewTurn(Step(s, knight, -1)), Is.True);
            Assert.That(mage.Buffs.Single(b => b.Def.Id == "test_ward").Remaining, Is.EqualTo(1), "a bystander's buff ticks once per party turn");
            Assert.That(s.Tempo.Single(t => t.Source == "test").TurnsLeft, Is.EqualTo(1));

            Assert.That(NewTurn(Step(s, knight, -1)), Is.True);
            Assert.That(mage.HasBuff("test_ward"), Is.False, "2 Turns = two party turns");
            Assert.That(s.Tempo.Any(t => t.Source == "test"), Is.False);
        }

        [Test]
        public void Guard_protection_covers_the_enemy_turn_it_skips_and_then_expires()
        {
            var (s, _) = Trio();
            var goblin = s.Units.First(u => u.Def.Id == "goblin");
            var ev = BattleEngine.Apply(s, new ShieldCommand());
            Assert.That(ev.OfType<BuffAppliedEvent>().Count(b => b.BuffId == "protection"), Is.EqualTo(3), "every hero is protected");
            int turn = ev.FindIndex(e => e is TurnStartedEvent t && t.UnitId == goblin.Id);
            int expired = ev.FindIndex(e => e is BuffRemovedEvent r && r.BuffId == "protection" && r.Reason == "expired");
            Assert.That(turn, Is.GreaterThanOrEqualTo(0), "the enemy acted");
            Assert.That(expired, Is.GreaterThan(turn), "Protection held through the enemy turn and fell away at the boundary");
            Assert.That(s.Units.Where(u => u.IsHero).Any(h => h.HasBuff("protection")), Is.False);
        }

        [Test]
        public void A_frozen_foe_thaws_after_one_party_turn()
        {
            var (s, _) = Trio();
            var goblin = s.Units.First(u => u.Def.Id == "goblin");
            goblin.Buffs.Add(new BuffInstance { Def = s.Content.Buff("frozen"), Remaining = 1, Fresh = false });
            Assert.That(goblin.IsTimelineHalted, Is.True);
            Assert.That(NewTurn(Step(s, Hero(s, "knight"), -1)), Is.True);
            Assert.That(goblin.HasBuff("frozen"), Is.False, "Frozen 1 Turn = one party turn");
            Assert.That(goblin.HasBuff("wet"), Is.True, "and thaws into Wet as in PartyWindow");
        }

        [Test]
        public void Any_hero_every_turn_replays_identically()
        {
            var db = Real;
            var setup = new BattleSetup
            {
                Stage = db.StageOrder.First(st => !st.IsTest),
                Seed = 42,
                UnlockedSlots = 5,
                Party = ContentAndSoakTests.StarterParty(db, 3),
                TurnMode = TurnMode.AnyHeroEveryTurn,
            };
            var commands = new List<BattleCommand>();
            var played = new StringBuilder();
            int heroActions = 0;
            var final = BotRunner.Play(db, setup, new HeroBot(5), 120, (cmd, ev, st) =>
            {
                if (cmd != null) commands.Add(cmd);
                foreach (var e in ev) played.AppendLine(e.ToString());
                if (cmd == null || st.IsOver || ev.OfType<AbilityRefusedEvent>().Any()) return;
                // Every command that went through — a hero's action or a Guard — handed a fresh turn back.
                Assert.That(ev.OfType<PlayerControlEvent>().All(p => p.NewWindow), Is.True, $"{cmd} ended the party turn");
                Assert.That(st.Units.Any(u => u.ActedThisWindow), Is.False, "the turn boundary cleared the mark");
                if (BattleEngine.CommandHeroId(cmd) >= 0) heroActions++;
            });
            Assert.That(commands, Is.Not.Empty);
            Assert.That(heroActions, Is.GreaterThan(1), "the bot played heroes in this mode");

            var opening = new List<BattleEvent>();
            var s = BattleEngine.Create(db, setup, opening);
            var replayed = new StringBuilder();
            foreach (var e in opening) replayed.AppendLine(e.ToString());
            foreach (var cmd in commands) foreach (var e in BattleEngine.Apply(s, cmd)) replayed.AppendLine(e.ToString());
            Assert.That(replayed.ToString(), Is.EqualTo(played.ToString()));
            Assert.That(s.TurnMode, Is.EqualTo(TurnMode.AnyHeroEveryTurn));
            Assert.That(s.Clock, Is.EqualTo(final.Clock));
            Assert.That(s.Phase, Is.EqualTo(final.Phase));
            Assert.That(s.Results.ActionsTaken, Is.EqualTo(final.Results.ActionsTaken));
        }

        // ── GG-T783: turn- and window-counting rules under one action per turn ──────────────────

        /// <summary>A setup in <paramref name="mode"/> with <paramref name="heroes"/> (each with the given passives).</summary>
        private static BattleSetup Setup(StageDef stage, TurnMode mode, params (string cls, int x, int y, string[] passives)[] heroes)
        {
            var setup = new BattleSetup { Stage = stage, Seed = 7, UnlockedSlots = 5, TurnMode = mode };
            foreach (var h in heroes)
                setup.Party.Add(new HeroSpec { ClassId = h.cls, Start = new Coord(h.x, h.y), Passives = (h.passives ?? new string[0]).ToList() });
            return setup;
        }

        private static BattleState Create(ContentDb db, BattleSetup setup) => BattleEngine.Create(db, setup, new List<BattleEvent>());

        /// <summary>One hero action in either mode: in PartyWindow every other hero counts as acted (TestKit.Turn), so the
        /// same step is the whole party turn there too.</summary>
        private static List<BattleEvent> Act(BattleState s, Unit h, int dy)
        {
            var cmd = new MoveCommand { HeroId = h.Id, Path = new List<Coord> { new Coord(h.Pos.X, h.Pos.Y + dy) }, HeldSeconds = BattleEngine.MinDragSeconds };
            return s.TurnMode == TurnMode.PartyWindow ? Turn(s, cmd) : BattleEngine.Apply(s, cmd);
        }

        [Test]
        public void Pickups_age_one_step_per_party_turn()
        {
            var (s, _) = Trio();
            var knight = Hero(s, "knight");
            s.Pickups.Add(new Pickup { Id = s.NextPickupId++, PickupId = "pk", At = new Coord(5, 4), ActionsLeft = 2 });
            Assert.That(NewTurn(Step(s, knight, -1)), Is.True);
            Assert.That(s.Pickups.Single().ActionsLeft, Is.EqualTo(1), "one party turn");
            var ev = Step(s, knight, 1);
            Assert.That(NewTurn(ev), Is.True);
            Assert.That(ev.OfType<PickupExpiredEvent>().Count(), Is.EqualTo(1), "two party turns: it faded");
            Assert.That(s.Pickups, Is.Empty);
        }

        [Test]
        public void A_corpse_rots_one_step_per_party_turn()
        {
            var db = Mini();
            db.Classes["corpse"] = new ClassDef { Id = "corpse", Name = "Corpse", Team = Team.Enemy, Tags = { "Remains", "Corpse" }, BaseStats = new Stats { Vitality = 6 }, MoveRange = 0, Brain = Brain.Sentinel };
            db.Classes["goblin"].Remains = "corpse";
            db.Classes["goblin"].DropTable = null;
            var (s, _) = Battle(db, StageWaves(new[] { ("goblin", 2, 3) }, new[] { ("tank", 0, 0) }), ("knight", 1, 3), ("cleric", 3, 4));
            // SynergyTests' pincer: the cleric closes it on the goblin, which falls and leaves its body; wave 2 begins.
            var kill = BattleEngine.Apply(s, new MoveCommand { HeroId = Hero(s, "cleric").Id, Path = P((3, 3)), HeldSeconds = BattleEngine.MinDragSeconds });
            Assert.That(kill.OfType<AbilityRefusedEvent>(), Is.Empty);
            var corpse = s.Units.Single(u => u.Alive && u.IsCorpse);
            int left = corpse.DecayIn;
            Assume.That(left, Is.GreaterThan(1));

            var knight = Hero(s, "knight");
            int turns = 0;
            while (corpse.Alive && turns < 20 && !s.IsOver)
            {
                var ev = Step(s, knight, turns % 2 == 0 ? 1 : -1);
                Assert.That(ev.OfType<AbilityRefusedEvent>(), Is.Empty);
                Assert.That(NewTurn(ev), Is.True);
                turns++;
                if (corpse.Alive) Assert.That(corpse.DecayIn, Is.EqualTo(left - turns), "one step per party turn");
            }
            Assert.That(corpse.Alive, Is.False);
            Assert.That(turns, Is.EqualTo(left), "it rotted after as many party turns as it had left");
        }

        /// <summary>Mini content plus a rule modelled on the Drowned Arks' tide (§14.20.4): at BattleStart either (4,6) is
        /// flooded or a timed "rise" is scheduled to flood (5,7) after 6 s; a hero standing in the flood is soaked (Warm)
        /// at its TurnStart and its TurnEnd.</summary>
        private static (ContentDb db, StageDef stage) Flooded(bool timed)
        {
            var db = Mini();
            var rule = new RegionRuleDef
            {
                Id = "turn-flood",
                TileStates = { new TileStateDef { Id = "flood", Color = "#3B6FA0", Glyph = "~" } },
                Events =
                {
                    new RuleEventDef
                    {
                        Id = "rise", Seconds = 6f, Hazard = false,
                        Effects = { new RuleEffectDef { Kind = RuleEffectKind.MarkTiles, Area = RuleArea.EventTiles, State = "flood" } },
                    },
                },
            };
            var start = new RuleHookDef { On = RuleHook.BattleStart };
            start.Effects.Add(timed
                ? new RuleEffectDef { Kind = RuleEffectKind.Schedule, Area = RuleArea.Tiles, Tiles = { new Coord(5, 7) }, Event = "rise" }
                : new RuleEffectDef { Kind = RuleEffectKind.MarkTiles, Area = RuleArea.Tiles, Tiles = { new Coord(4, 6) }, State = "flood" });
            rule.Hooks.Add(start);
            foreach (var on in new[] { RuleHook.TurnStart, RuleHook.TurnEnd })
                rule.Hooks.Add(new RuleHookDef
                {
                    On = on, Team = RuleTeam.Hero, State = "flood",
                    Effects = { new RuleEffectDef { Kind = RuleEffectKind.ApplyBuff, Units = RuleUnits.Subject, Buff = "warm" } },
                });
            RegionRuleCatalog.Index(db, new List<RegionRuleDef> { rule });
            var stage = Stage(("goblin", 5, 0));
            stage.Rules.Add("turn-flood");
            return (db, stage);
        }

        /// <summary>How often the flood rule's hook <paramref name="hook"/> (1 TurnStart, 2 TurnEnd) has matched.</summary>
        private static int Runs(BattleState s, int hook) => s.Region.HookCounts.TryGetValue("turn-flood#" + hook, out int n) ? n : 0;

        [Test]
        public void A_heros_region_turn_hooks_run_once_per_party_turn_even_when_it_did_not_act()
        {
            var (db, stage) = Flooded(timed: false);
            var (s, _) = Battle(db, stage, ("knight", 0, 6), ("cleric", 2, 6), ("mage", 4, 6));
            Assert.That(s.Region.At(new Coord(4, 6))?.State, Is.EqualTo("flood"), "the mage stands in the flood");
            int starts = Runs(s, 1), ends = Runs(s, 2);
            var knight = Hero(s, "knight");
            var mage = Hero(s, "mage");
            for (int i = 1; i <= 2; i++)
            {
                var ev = Step(s, knight, i % 2 == 1 ? -1 : 1);
                Assert.That(NewTurn(ev), Is.True);
                Assert.That(Runs(s, 1), Is.EqualTo(starts + i), "the bystander's TurnStart in the water, once per party turn");
                Assert.That(Runs(s, 2), Is.EqualTo(ends + i), "and its TurnEnd");
                Assert.That(mage.HasBuff("warm"), Is.True, "soaked though it did not act");
            }
        }

        [Test]
        public void A_timed_rule_event_lands_after_the_same_party_turns_as_in_party_window()
        {
            // Rising water and the anchor heave are timeline events on the clock: a party turn still runs to the next enemy
            // arrival, so one action per turn sees them land when a full PartyWindow window would.
            (int turns, long clock) Play(TurnMode mode)
            {
                var (db, stage) = Flooded(timed: true);
                var s = Create(db, Setup(stage, mode, ("knight", 0, 6, null), ("cleric", 2, 6, null), ("mage", 4, 6, null)));
                var knight = Hero(s, "knight");
                for (int i = 1; i <= 30 && !s.IsOver; i++)
                {
                    var ev = Act(s, knight, i % 2 == 1 ? -1 : 1);
                    Assert.That(ev.OfType<AbilityRefusedEvent>(), Is.Empty, $"{mode} turn {i}");
                    Assert.That(NewTurn(ev), Is.True, $"{mode} turn {i}");
                    if (ev.OfType<HazardLandedEvent>().Any(h => h.EventId == "rise")) return (i, (long)s.Clock);
                }
                return (-1, (long)s.Clock);
            }
            var a = Play(TurnMode.AnyHeroEveryTurn);
            var w = Play(TurnMode.PartyWindow);
            Assert.That(a.turns, Is.GreaterThan(0), "the water rose");
            Assert.That(a, Is.EqualTo(w));
        }

        [Test]
        public void Turn_triggers_fire_for_every_hero_each_party_turn_and_action_end_only_for_the_actor()
        {
            var db = Mini();
            foreach (var (id, on) in new[] { ("t_start", TriggerOn.TurnStart), ("t_end", TriggerOn.TurnEnd), ("t_action", TriggerOn.ActionEnd) })
                db.Abilities[id] = new AbilityDef
                {
                    Id = id, Name = id, Kind = AbilityKind.Passive,
                    Trigger = new TriggerDef { On = on, Effects = { new TriggerEffect { Kind = TriggerEffectKind.ApplyBuff, Buff = "warm" } } },
                };
            var all = new[] { "t_start", "t_end", "t_action" };
            var s = Create(db, Setup(Stage(("goblin", 5, 0)), TurnMode.AnyHeroEveryTurn, ("knight", 0, 6, all), ("mage", 4, 6, all)));
            var knight = Hero(s, "knight");
            var mage = Hero(s, "mage");
            int Fired(List<BattleEvent> ev, Unit u, TriggerOn on) => ev.OfType<TriggeredEvent>().Count(t => t.UnitId == u.Id && t.On == on);

            var ev1 = Step(s, knight, -1);
            Assert.That(NewTurn(ev1), Is.True);
            foreach (var h in new[] { knight, mage })
            {
                Assert.That(Fired(ev1, h, TriggerOn.TurnStart), Is.EqualTo(1), h.Def.Id + ": one party turn, one TurnStart");
                Assert.That(Fired(ev1, h, TriggerOn.TurnEnd), Is.EqualTo(1), h.Def.Id + ": and one TurnEnd");
            }
            Assert.That(Fired(ev1, knight, TriggerOn.ActionEnd), Is.EqualTo(1), "the actor's action ended");
            Assert.That(Fired(ev1, mage, TriggerOn.ActionEnd), Is.EqualTo(0), "the bystander took no action");

            var ev2 = Step(s, mage, -1);
            Assert.That(NewTurn(ev2), Is.True);
            Assert.That(Fired(ev2, mage, TriggerOn.ActionEnd), Is.EqualTo(1));
            Assert.That(Fired(ev2, knight, TriggerOn.ActionEnd), Is.EqualTo(0));
            Assert.That(Fired(ev2, knight, TriggerOn.TurnStart), Is.EqualTo(1));
        }

        [Test]
        public void A_foe_counts_its_own_turns_not_hero_actions()
        {
            // What a rule break counts (TwinTurn, GravityStamp's every third turn) is the breaker's own TurnsTaken.
            var (s, _) = Trio();
            var goblin = s.Units.First(u => u.Def.Id == "goblin");
            int before = goblin.TurnsTaken, turns = 0;
            var knight = Hero(s, "knight");
            for (int i = 0; i < 3; i++)
                turns += Step(s, knight, i % 2 == 0 ? -1 : 1).OfType<TurnStartedEvent>().Count(t => t.UnitId == goblin.Id);
            Assert.That(turns, Is.GreaterThanOrEqualTo(3), "a lone foe arrives once per party turn");
            Assert.That(goblin.TurnsTaken - before, Is.EqualTo(turns));
        }

        [Test]
        public void A_tamed_ally_counts_its_own_turns_and_never_takes_the_party_action()
        {
            var (s, _) = Battle(Mini(), Stage(("tank", 5, 0), ("wolf", 0, 2)), ("knight", 0, 6), ("cleric", 2, 6), ("mage", 4, 6));
            var wolf = s.Units.First(u => u.Def.Id == "wolf");
            wolf.Team = Team.Hero; wolf.Tamed = true; wolf.TameTurnsLeft = 99; wolf.BondNeeded = 999;
            Assert.That(wolf.IsIndependent, Is.True);
            Assert.That(BattleEngine.HeroesLeftToAct(s), Does.Not.Contain(wolf), "not the party's to command");
            Assert.That(BattleEngine.HeroesLeftToAct(s).Count, Is.EqualTo(3));

            var knight = Hero(s, "knight");
            int grew = 0;
            for (int i = 0; i < 3; i++)
            {
                var ev = Step(s, knight, i % 2 == 0 ? -1 : 1);
                Assert.That(NewTurn(ev), Is.True);
                grew += ev.OfType<BondGrewEvent>().Count(b => b.UnitId == wolf.Id);
                Assert.That(wolf.ActedThisWindow, Is.False, "its own turns never mark the party's action");
                Assert.That(BattleEngine.HeroesLeftToAct(s).Count, Is.EqualTo(3), "the party's next turn is whole");
            }
            Assert.That(wolf.TameTurnsLeft, Is.EqualTo(99 - grew), "the tame counts the wolf's own turns, not the party's");
        }

        [Test]
        public void A_recruit_joins_with_the_talk_that_ends_the_turn_and_may_act_the_next()
        {
            var stage = Stage(("tank", 5, 0));
            stage.Waves[0].Recruitables.Add(new RecruitableDef { Class = "mage", X = 1, Y = 5 });
            var (s, _) = Battle(Mini(), stage, ("knight", 1, 6), ("cleric", 3, 6));
            var mage = s.Units.Single(u => u.Def.Id == "mage");
            Assert.That(mage.IsNeutral, Is.True);

            var talk = BattleEngine.Apply(s, new TalkCommand { HeroId = Hero(s, "knight").Id, TargetId = mage.Id });
            Assert.That(talk.OfType<RecruitJoinedEvent>().Count(), Is.EqualTo(1));
            Assert.That(NewTurn(talk), Is.True, "Talk is the turn's one action");
            Assert.That(mage.IsCommandable, Is.True);
            Assert.That(mage.ActedThisWindow, Is.False);
            Assert.That(BattleEngine.HeroesLeftToAct(s), Does.Contain(mage), "the recruit may act from the next party turn");

            var act = Step(s, mage, -1);
            Assert.That(act.OfType<AbilityRefusedEvent>(), Is.Empty);
            Assert.That(NewTurn(act), Is.True);
        }

        [Test]
        public void A_wanderer_is_independent_and_never_takes_the_party_action()
        {
            var setup = Setup(Stage(("tank", 5, 0)), TurnMode.AnyHeroEveryTurn, ("knight", 0, 6, null), ("cleric", 2, 6, null));
            setup.Wanderer = new WandererSpec { Class = "mage", Wave = 0 };
            var s = Create(Mini(), setup);
            var wanderer = s.Units.Single(u => u.Wanderer);
            Assert.That(wanderer.IsIndependent, Is.True);
            Assert.That(BattleEngine.CanMove(s, wanderer.Id), Is.False);
            Assert.That(BattleEngine.HeroesLeftToAct(s).Count, Is.EqualTo(2));

            int before = wanderer.TurnsTaken, turns = 0;
            var knight = Hero(s, "knight");
            for (int i = 0; i < 2; i++)
            {
                var ev = Step(s, knight, i % 2 == 0 ? -1 : 1);
                Assert.That(NewTurn(ev), Is.True);
                turns += ev.OfType<TurnStartedEvent>().Count(t => t.UnitId == wanderer.Id);
                Assert.That(wanderer.ActedThisWindow, Is.False);
                Assert.That(BattleEngine.HeroesLeftToAct(s).Count, Is.EqualTo(2), "both heroes may act again");
            }
            Assert.That(wanderer.TurnsTaken - before, Is.EqualTo(turns), "it counts its own turns");
        }

        [Test]
        public void The_countdown_badge_reads_the_same_in_both_modes()
        {
            BattleState Make(TurnMode mode) =>
                Create(Mini(), Setup(Stage(("goblin", 5, 0), ("wolf", 0, 0)), mode, ("knight", 0, 6, null), ("cleric", 2, 6, null), ("mage", 4, 6, null)));
            var a = Make(TurnMode.AnyHeroEveryTurn);
            var w = Make(TurnMode.PartyWindow);
            for (int i = 0; i < 3; i++)
            {
                foreach (var foe in a.Units.Where(u => u.Team == Team.Enemy && u.Alive))
                    Assert.That(TurnRules.CountdownLeft(a, foe.Id), Is.EqualTo(TurnRules.CountdownLeft(w, foe.Id)), $"{foe.Def.Id} after {i} turns");
                Act(a, Hero(a, "knight"), i % 2 == 0 ? -1 : 1);
                Act(w, Hero(w, "knight"), i % 2 == 0 ? -1 : 1);
            }
        }

        // ── GG-T785: the smart and casual bots under one action per turn ────────────────────────

        [Test]
        public void The_bot_planning_seam_counts_one_action_per_turn()
        {
            var (a, _) = Trio();
            var knight = Hero(a, "knight");
            var cleric = Hero(a, "cleric");
            Assert.That(BattleEngine.HeroesLeftToAct(a).Count, Is.EqualTo(3), "anyone may take the turn");
            Assert.That(TurnRules.HeroesLeftAfter(a, knight.Id), Is.EqualTo(0), "but nobody acts after the one who does");
            Assert.That(TurnRules.ActsLaterThisTurn(a, cleric), Is.False, "a set-up spans turns");

            var db = Mini();
            var w = Create(db, Setup(Stage(("goblin", 5, 0)), TurnMode.PartyWindow, ("knight", 0, 6, null), ("cleric", 2, 6, null), ("mage", 4, 6, null)));
            Assert.That(TurnRules.HeroesLeftAfter(w, Hero(w, "knight").Id), Is.EqualTo(2), "PartyWindow keeps its shared window");
            Assert.That(TurnRules.ActsLaterThisTurn(w, Hero(w, "cleric")), Is.True);
        }

        /// <summary>A campaign battle on the first stage, Gentle dial, in <paramref name="mode"/>.</summary>
        private static BattleSetup CampaignSetup(ContentDb db, int seed, TurnMode mode)
        {
            var save = GridGame.Meta.Progression.NewGame(db, "ModeA");
            return GridGame.Meta.BattleFactory.Build(db, save, db.CampaignStages()[0].Id, seed, 1, mode);
        }

        /// <summary>Plays <paramref name="setup"/> with <paramref name="bot"/>, checks every accepted command was the whole
        /// party turn, then replays its commands on a fresh battle and checks the events match.</summary>
        private static BattleState PlayAndReplay(ContentDb db, BattleSetup setup, HeroBot bot)
        {
            var commands = new List<BattleCommand>();
            var played = new StringBuilder();
            var final = BotRunner.Play(db, setup, bot, 3000, (cmd, ev, st) =>
            {
                if (cmd != null) commands.Add(cmd);
                foreach (var e in ev) played.AppendLine(e.ToString());
                if (cmd == null || st.IsOver || ev.OfType<AbilityRefusedEvent>().Any()) return;
                Assert.That(ev.OfType<PlayerControlEvent>().All(p => p.NewWindow), Is.True, $"{cmd} ended the party turn");
            });
            Assert.That(final.TurnMode, Is.EqualTo(TurnMode.AnyHeroEveryTurn));
            Assert.That(final.IsOver, Is.True, "the bot finished the stage (no soft-lock)");

            var opening = new List<BattleEvent>();
            var s = BattleEngine.Create(db, setup, opening);
            var replayed = new StringBuilder();
            foreach (var e in opening) replayed.AppendLine(e.ToString());
            foreach (var cmd in commands) foreach (var e in BattleEngine.Apply(s, cmd)) replayed.AppendLine(e.ToString());
            Assert.That(replayed.ToString(), Is.EqualTo(played.ToString()), "the battle replays identically");
            Assert.That(s.Phase, Is.EqualTo(final.Phase));
            Assert.That(s.Clock, Is.EqualTo(final.Clock));
            return final;
        }

        [Test]
        public void The_smart_bot_wins_a_stage_and_it_replays_identically()
        {
            var db = Real;
            var final = PlayAndReplay(db, CampaignSetup(db, 11, TurnMode.AnyHeroEveryTurn), new HeroBot(11));
            Assert.That(final.Phase, Is.EqualTo(BattlePhase.Victory));
        }

        [Test]
        public void The_casual_bot_finishes_a_stage_and_it_replays_identically()
        {
            var db = Real;
            var bot = new HeroBot(13, randomness: 0.5f);
            Assert.That(bot.TimelineAware, Is.False);
            PlayAndReplay(db, CampaignSetup(db, 13, TurnMode.AnyHeroEveryTurn), bot);
        }

        [Test]
        public void The_smart_bot_takes_one_action_per_turn_and_the_same_seed_decides_the_same()
        {
            var db = Real;
            var setup = CampaignSetup(db, 17, TurnMode.AnyHeroEveryTurn);
            var a = new List<string>();
            var b = new List<string>();
            BotRunner.Play(db, setup, new HeroBot(17), 200, (cmd, ev, st) => { if (cmd != null) a.Add(cmd.ToString()); });
            BotRunner.Play(db, setup, new HeroBot(17), 200, (cmd, ev, st) => { if (cmd != null) b.Add(cmd.ToString()); });
            Assert.That(a, Is.Not.Empty);
            Assert.That(b, Is.EqualTo(a), "the bot is deterministic for a seed");
        }
    }
}
