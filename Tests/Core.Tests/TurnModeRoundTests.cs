using System.Collections.Generic;
using System.Linq;
using System.Text;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Bot;
using GridGame.Core.Content;
using GridGame.Core.Rules;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>
    /// GG-T1066 (bible §2.13.2, rules GG-T794): <see cref="TurnMode.UnmovedThisRound"/> through the <see cref="TurnRules"/>
    /// seam. Each party turn one hero acts, and only a hero that hasn't acted this round (<see cref="Unit.ActedThisRound"/>);
    /// once every able hero has acted the round resets (<see cref="BattleState.Round"/> + 1, a
    /// <see cref="RoundStartedEvent"/>). Fallen, Frozen and Asleep heroes are skipped, a revived hero keeps its mark, a
    /// joiner may act this round, Guard uses no place, a new wave resets the round, Clone carries it all and a bot battle
    /// replays identically. GG-T1073 (GG-T796): region turn hooks and TurnStart/TurnEnd triggers run once per party turn
    /// for every hero (ActionEnd only for the actor), timed rule events and the countdown badge read as in PartyWindow, and
    /// foes, tames, wanderers and summons count their own turns and never take a hero's place in the round.
    /// </summary>
    public class TurnModeRoundTests
    {
        private static (BattleState s, List<BattleEvent> ev) Battle(ContentDb db, StageDef stage, params (string cls, int x, int y)[] heroes)
        {
            var setup = new BattleSetup { Stage = stage, Seed = 7, UnlockedSlots = 5, TurnMode = TurnMode.UnmovedThisRound };
            foreach (var h in heroes) setup.Party.Add(new HeroSpec { ClassId = h.cls, Start = new Coord(h.x, h.y) });
            var ev = new List<BattleEvent>();
            return (BattleEngine.Create(db, setup, ev), ev);
        }

        private static (BattleState s, List<BattleEvent> ev) Trio() =>
            Battle(Mini(), Stage(("goblin", 5, 0)), ("knight", 0, 6), ("cleric", 2, 6), ("mage", 4, 6));

        private static List<BattleEvent> Step(BattleState s, Unit h, int dy) =>
            BattleEngine.Apply(s, new MoveCommand { HeroId = h.Id, Path = new List<Coord> { new Coord(h.Pos.X, h.Pos.Y + dy) }, HeldSeconds = BattleEngine.MinDragSeconds });

        private static bool NewTurn(List<BattleEvent> ev) => ev.OfType<PlayerControlEvent>().Any(p => p.NewWindow);

        private static List<RoundStartedEvent> Rounds(List<BattleEvent> ev) => ev.OfType<RoundStartedEvent>().ToList();

        /// <summary>A hero's step that went through and ended the party's turn.</summary>
        private static List<BattleEvent> Acts(BattleState s, Unit h, int dy = -1)
        {
            var ev = Step(s, h, dy);
            Assert.That(ev.OfType<AbilityRefusedEvent>(), Is.Empty, h.Def.Id + " acts");
            Assert.That(NewTurn(ev), Is.True, h.Def.Id + "'s action is the party's turn");
            return ev;
        }

        private static void Refused(BattleState s, Unit h)
        {
            long clock = s.Clock;
            int actions = s.Results.ActionsTaken;
            var ev = Step(s, h, -1);
            Assert.That(ev.OfType<AbilityRefusedEvent>().Single().Reason, Is.EqualTo("Already acted"), h.Def.Id);
            Assert.That(s.Clock, Is.EqualTo(clock), "a refused command spends nothing");
            Assert.That(s.Results.ActionsTaken, Is.EqualTo(actions));
        }

        private static void Kill(Unit u) { u.Hp = 0; u.OnBoard = false; }

        private static void Revive(Unit u) { u.Hp = 5; u.OnBoard = true; }

        [Test]
        public void The_mode_is_built_and_every_hero_may_act_in_round_one()
        {
            var (s, _) = Trio();
            Assert.That(s.TurnMode, Is.EqualTo(TurnMode.UnmovedThisRound));
            Assert.That(s.Round, Is.EqualTo(1));
            Assert.That(TurnRules.OneHeroPerTurn(s), Is.True);
            Assert.That(TurnRules.RoundComplete(s), Is.False, "nobody has acted yet");
            Assert.That(BattleEngine.HeroesLeftToAct(s).Count, Is.EqualTo(3));
            foreach (var h in s.Units.Where(u => u.IsHero))
            {
                Assert.That(BattleEngine.CanMove(s, h.Id), Is.True, h.Def.Id);
                Assert.That(TurnRules.HasActed(s, h), Is.False, h.Def.Id);
                Assert.That(h.ActedThisRound, Is.False, h.Def.Id);
            }
        }

        [Test]
        public void A_moved_hero_is_refused_until_the_round_resets()
        {
            var (s, _) = Trio();
            var knight = Hero(s, "knight");
            var cleric = Hero(s, "cleric");
            var mage = Hero(s, "mage");

            var first = Acts(s, knight);
            Assert.That(Rounds(first), Is.Empty, "two heroes still to act");
            Assert.That(knight.ActedThisRound, Is.True);
            Assert.That(knight.ActedThisWindow, Is.False, "the turn boundary cleared the window mark, not the round's");
            Assert.That(TurnRules.HasActed(s, knight), Is.True);
            Assert.That(BattleEngine.CanMove(s, knight.Id), Is.False);
            Assert.That(BattleEngine.HeroesLeftToAct(s), Is.EquivalentTo(new[] { cleric, mage }));
            Refused(s, knight);

            Acts(s, cleric);
            Assert.That(s.Round, Is.EqualTo(1));
            Refused(s, knight);
            Refused(s, cleric);
            Assert.That(BattleEngine.HeroesLeftToAct(s), Is.EquivalentTo(new[] { mage }));

            Acts(s, mage);
            Assert.That(s.Round, Is.EqualTo(2), "every hero acted, a new round");
            Assert.That(BattleEngine.CanMove(s, knight.Id), Is.True, "free again");
            Acts(s, knight);
        }

        [Test]
        public void The_round_resets_when_the_last_able_hero_acts()
        {
            var (s, _) = Trio();
            var knight = Hero(s, "knight");
            Assert.That(Rounds(Acts(s, knight)), Is.Empty);
            Assert.That(Rounds(Acts(s, Hero(s, "cleric"))), Is.Empty);
            Assert.That(s.Round, Is.EqualTo(1));

            var last = Acts(s, Hero(s, "mage"));
            Assert.That(Rounds(last).Single().Round, Is.EqualTo(2));
            Assert.That(s.Round, Is.EqualTo(2));
            Assert.That(s.Units.Any(u => u.ActedThisRound), Is.False, "every mark cleared");
            Assert.That(BattleEngine.HeroesLeftToAct(s).Count, Is.EqualTo(3));
            Assert.That(TurnRules.RoundComplete(s), Is.False);

            // A second round runs the same way.
            Assert.That(Rounds(Acts(s, knight, 1)), Is.Empty);
            Assert.That(Rounds(Acts(s, Hero(s, "cleric"), 1)), Is.Empty);
            Assert.That(Rounds(Acts(s, Hero(s, "mage"), 1)).Single().Round, Is.EqualTo(3));
        }

        [TestCase("fallen")]
        [TestCase("frozen")]
        [TestCase("sleep")]
        public void A_fallen_frozen_or_asleep_hero_is_skipped(string why)
        {
            var (s, _) = Trio();
            var mage = Hero(s, "mage");
            if (why == "fallen") Kill(mage);
            else
            {
                mage.Buffs.Add(new BuffInstance { Def = s.Content.Buff(why), Remaining = 9, Fresh = false });
                Assume.That(mage.IsImmobile, Is.True, why + " holds the mage");
            }
            Assert.That(BattleEngine.HeroesLeftToAct(s), Does.Not.Contain(mage));

            Assert.That(Rounds(Acts(s, Hero(s, "knight"))), Is.Empty);
            var last = Acts(s, Hero(s, "cleric"));
            Assert.That(Rounds(last).Single().Round, Is.EqualTo(2), "the round did not wait for the " + why + " mage");
            Assert.That(mage.ActedThisRound, Is.False);
        }

        [Test]
        public void A_revived_hero_keeps_its_mark()
        {
            var (s, _) = Trio();
            var knight = Hero(s, "knight");
            var cleric = Hero(s, "cleric");
            var mage = Hero(s, "mage");

            Kill(cleric); // falls before it acted
            Acts(s, knight);
            Assert.That(knight.ActedThisRound, Is.True);
            Kill(knight);
            Revive(cleric);
            Assert.That(Rounds(Acts(s, mage)), Is.Empty, "the revived cleric hasn't acted, so the round waits for it");
            Assert.That(s.Round, Is.EqualTo(1));

            Revive(knight);
            Assert.That(knight.ActedThisRound, Is.True, "revived, it keeps its mark");
            Assert.That(TurnRules.HasActed(s, knight), Is.True);
            Assert.That(BattleEngine.CanMove(s, knight.Id), Is.False);
            Refused(s, knight);
            Assert.That(BattleEngine.CanMove(s, cleric.Id), Is.True, "a hero that fell before acting may act once revived");

            var last = Acts(s, cleric);
            Assert.That(Rounds(last).Single().Round, Is.EqualTo(2));
            Assert.That(BattleEngine.CanMove(s, knight.Id), Is.True, "the next round frees it");
        }

        [Test]
        public void A_joiner_may_act_this_round_and_the_round_waits_for_it()
        {
            var stage = Stage(("tank", 5, 0));
            stage.Waves[0].Recruitables.Add(new RecruitableDef { Class = "mage", X = 1, Y = 5 });
            var (s, _) = Battle(Mini(), stage, ("knight", 1, 6), ("cleric", 3, 6));
            var knight = Hero(s, "knight");
            var cleric = Hero(s, "cleric");
            var mage = s.Units.Single(u => u.Def.Id == "mage");
            Assert.That(mage.IsNeutral, Is.True);

            var talk = BattleEngine.Apply(s, new TalkCommand { HeroId = knight.Id, TargetId = mage.Id });
            Assert.That(talk.OfType<RecruitJoinedEvent>().Count(), Is.EqualTo(1));
            Assert.That(NewTurn(talk), Is.True, "Talk is the turn's one action");
            Assert.That(knight.ActedThisRound, Is.True, "the talker used its place");
            Assert.That(mage.IsCommandable, Is.True);
            Assert.That(mage.ActedThisRound, Is.False);
            Assert.That(BattleEngine.HeroesLeftToAct(s), Is.EquivalentTo(new[] { cleric, mage }));

            var c = Acts(s, cleric);
            Assert.That(Rounds(c), Is.Empty, "the joiner still has its place this round");
            Assert.That(s.Round, Is.EqualTo(1));

            var m = Acts(s, mage);
            Assert.That(Rounds(m).Single().Round, Is.EqualTo(2));

            // ClearActed itself: a unit joining (or rejoining) mid-round is free this round.
            mage.ActedThisRound = true;
            TurnRules.ClearActed(s, mage);
            Assert.That(mage.ActedThisRound, Is.False);
            Assert.That(mage.ActedThisWindow, Is.False);
        }

        [Test]
        public void Guard_uses_no_place_in_the_round()
        {
            var (s, _) = Trio();
            var knight = Hero(s, "knight");
            var cleric = Hero(s, "cleric");
            var mage = Hero(s, "mage");

            var opening = BattleEngine.Apply(s, new ShieldCommand());
            Assert.That(NewTurn(opening), Is.True, "Guard gives the turn up");
            Assert.That(Rounds(opening), Is.Empty);
            Assert.That(s.Units.Any(u => u.ActedThisRound), Is.False, "nobody's place was used");

            Acts(s, knight);
            var guard = BattleEngine.Apply(s, new ShieldCommand());
            Assert.That(NewTurn(guard), Is.True);
            Assert.That(Rounds(guard), Is.Empty);
            Assert.That(s.Round, Is.EqualTo(1));
            Assert.That(knight.ActedThisRound, Is.True, "Guard does not reset the round");
            Assert.That(BattleEngine.HeroesLeftToAct(s), Is.EquivalentTo(new[] { cleric, mage }));
            Refused(s, knight);
        }

        [Test]
        public void A_new_wave_resets_the_round()
        {
            var (s, _) = Battle(Mini(), StageWaves(new[] { ("goblin", 2, 3) }, new[] { ("goblin", 3, 0) }), ("knight", 2, 2), ("cleric", 2, 5));
            var knight = Hero(s, "knight");
            var cleric = Hero(s, "cleric");
            // The knight has had its place this round (an earlier party turn, without moving it off the pincer line).
            TurnRules.MarkActed(s, knight);
            Assert.That(TurnRules.ResetTurn(s), Is.False, "the cleric is still to act, so no new round");
            Assert.That(knight.ActedThisRound, Is.True);
            Assert.That(BattleEngine.CanMove(s, knight.Id), Is.False);

            At(s, 2, 3).Hp = 1;
            // The cleric closes a pincer on the lone goblin: wave 1 falls and wave 2 begins.
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = cleric.Id, Path = P((2, 4)), HeldSeconds = BattleEngine.MinDragSeconds });
            Assert.That(ev.OfType<AbilityRefusedEvent>(), Is.Empty);
            Assert.That(s.IsOver, Is.False);
            Assert.That(s.Wave, Is.EqualTo(1), "wave 1 fell to the pincer");
            Assert.That(NewTurn(ev), Is.True);
            Assert.That(Rounds(ev).Single().Round, Is.EqualTo(2), "a new wave opens a new round");
            Assert.That(s.Units.Any(u => u.ActedThisRound), Is.False);
            Assert.That(BattleEngine.HeroesLeftToAct(s), Is.EquivalentTo(new[] { knight, cleric }), "each wave opens with every hero free");
        }

        [Test]
        public void A_new_wave_with_nobody_acted_this_round_keeps_the_round()
        {
            var (s, _) = Trio();
            Assert.That(TurnRules.ResetTurn(s, waveChanged: true), Is.False, "nobody acted: the round is already fresh");
            Assert.That(s.Round, Is.EqualTo(1));
        }

        [Test]
        public void Clone_carries_the_round_and_the_marks()
        {
            var (s, _) = Trio();
            Acts(s, Hero(s, "knight"));
            Acts(s, Hero(s, "cleric"));
            Acts(s, Hero(s, "mage"));
            Assert.That(s.Round, Is.EqualTo(2));
            Acts(s, Hero(s, "cleric"), 1);

            var c = s.Clone();
            Assert.That(c.Round, Is.EqualTo(2));
            Assert.That(c.TurnMode, Is.EqualTo(TurnMode.UnmovedThisRound));
            foreach (var u in s.Units)
                Assert.That(c.UnitById(u.Id).ActedThisRound, Is.EqualTo(u.ActedThisRound), u.Def.Id);
            Assert.That(TurnRules.HasActed(c, Hero(c, "cleric")), Is.True);
            Assert.That(BattleEngine.HeroesLeftToAct(c).Select(u => u.Id), Is.EqualTo(BattleEngine.HeroesLeftToAct(s).Select(u => u.Id)));

            // The copy plays on alone: finishing its round leaves the original where it was.
            Acts(c, Hero(c, "knight"), 1);
            Acts(c, Hero(c, "mage"), 1);
            Assert.That(c.Round, Is.EqualTo(3));
            Assert.That(s.Round, Is.EqualTo(2));
            Assert.That(Hero(s, "cleric").ActedThisRound, Is.True);
            Assert.That(Hero(s, "knight").ActedThisRound, Is.False);
        }

        [Test]
        public void Other_modes_never_keep_rounds()
        {
            foreach (var mode in new[] { TurnMode.PartyWindow, TurnMode.AnyHeroEveryTurn })
            {
                var setup = new BattleSetup { Stage = Stage(("goblin", 5, 0)), Seed = 7, UnlockedSlots = 5, TurnMode = mode };
                setup.Party.Add(new HeroSpec { ClassId = "knight", Start = new Coord(0, 6) });
                var s = BattleEngine.Create(Mini(), setup, new List<BattleEvent>());
                var knight = Hero(s, "knight");
                TurnRules.MarkActed(s, knight);
                Assert.That(knight.ActedThisRound, Is.False, mode + " marks no round");
                Assert.That(TurnRules.RoundComplete(s), Is.False, mode.ToString());
                Assert.That(TurnRules.ResetTurn(s, waveChanged: true), Is.False, mode.ToString());
                Assert.That(s.Round, Is.EqualTo(1), mode.ToString());
            }
        }

        [Test]
        public void A_bot_battle_replays_identically()
        {
            var db = Real;
            var setup = new BattleSetup
            {
                Stage = db.StageOrder.First(st => !st.IsTest),
                Seed = 42,
                UnlockedSlots = 5,
                Party = ContentAndSoakTests.StarterParty(db, 3),
                TurnMode = TurnMode.UnmovedThisRound,
            };
            var commands = new List<BattleCommand>();
            var played = new StringBuilder();
            int heroActions = 0, rounds = 0, lastRound = 1;
            var final = BotRunner.Play(db, setup, new HeroBot(5), 120, (cmd, ev, st) =>
            {
                if (cmd != null) commands.Add(cmd);
                foreach (var e in ev) played.AppendLine(e.ToString());
                foreach (var r in ev.OfType<RoundStartedEvent>())
                {
                    Assert.That(r.Round, Is.EqualTo(lastRound + 1), "rounds count up one at a time");
                    lastRound = r.Round;
                    rounds++;
                }
                if (cmd == null || st.IsOver || ev.OfType<AbilityRefusedEvent>().Any()) return;
                Assert.That(ev.OfType<PlayerControlEvent>().All(p => p.NewWindow), Is.True, $"{cmd} ended the party turn");
                Assert.That(st.Units.Any(u => u.ActedThisWindow), Is.False, "the turn boundary cleared the window mark");
                if (BattleEngine.CommandHeroId(cmd) >= 0) heroActions++;
            });
            Assert.That(commands, Is.Not.Empty);
            Assert.That(heroActions, Is.GreaterThan(1), "the bot played heroes in this mode");
            Assert.That(final.Round, Is.EqualTo(lastRound));

            var opening = new List<BattleEvent>();
            var s = BattleEngine.Create(db, setup, opening);
            var replayed = new StringBuilder();
            foreach (var e in opening) replayed.AppendLine(e.ToString());
            foreach (var cmd in commands) foreach (var e in BattleEngine.Apply(s, cmd)) replayed.AppendLine(e.ToString());
            Assert.That(replayed.ToString(), Is.EqualTo(played.ToString()));
            Assert.That(s.TurnMode, Is.EqualTo(TurnMode.UnmovedThisRound));
            Assert.That(s.Round, Is.EqualTo(final.Round));
            Assert.That(s.Units.Select(u => u.ActedThisRound), Is.EqualTo(final.Units.Select(u => u.ActedThisRound)));
            Assert.That(s.Clock, Is.EqualTo(final.Clock));
            Assert.That(s.Phase, Is.EqualTo(final.Phase));
            Assert.That(s.Results.ActionsTaken, Is.EqualTo(final.Results.ActionsTaken));
        }

        // ── GG-T798: bots plan the round and never strand it ────────────────────────────────────────

        /// <summary>Plays the first campaign stage in UnmovedThisRound with <paramref name="bot"/> to the end (no round is
        /// stranded: mid-round the bot Guards at most <see cref="HeroBot.RoundGuardLimit"/> times by choice), then replays it.</summary>
        private static BattleState PlayRoundStage(ContentDb db, int seed, HeroBot bot)
        {
            var save = GridGame.Meta.Progression.NewGame(db, "ModeB");
            var setup = GridGame.Meta.BattleFactory.Build(db, save, db.CampaignStages()[0].Id, seed, 1, TurnMode.UnmovedThisRound);
            var commands = new List<BattleCommand>();
            var played = new StringBuilder();
            var final = BotRunner.Play(db, setup, bot, 3000, (cmd, ev, st) =>
            {
                if (cmd != null) commands.Add(cmd);
                foreach (var e in ev) played.AppendLine(e.ToString());
            });
            Assert.That(final.IsOver, Is.True, "the bot finished the stage (no stranded round)");
            Assert.That(final.TurnMode, Is.EqualTo(TurnMode.UnmovedThisRound));

            var opening = new List<BattleEvent>();
            var s = BattleEngine.Create(db, setup, opening);
            var replayed = new StringBuilder();
            foreach (var e in opening) replayed.AppendLine(e.ToString());
            foreach (var cmd in commands) foreach (var e in BattleEngine.Apply(s, cmd)) replayed.AppendLine(e.ToString());
            Assert.That(replayed.ToString(), Is.EqualTo(played.ToString()), "the battle replays identically");
            Assert.That(s.Round, Is.EqualTo(final.Round));
            Assert.That(s.Clock, Is.EqualTo(final.Clock));
            return final;
        }

        [Test]
        public void The_smart_bot_wins_a_stage_and_it_replays_identically()
        {
            var final = PlayRoundStage(Real, 11, new HeroBot(11));
            Assert.That(final.Phase, Is.EqualTo(BattlePhase.Victory));
        }

        [Test]
        public void The_casual_bot_finishes_a_stage_and_it_replays_identically()
        {
            var bot = new HeroBot(13, randomness: 0.5f);
            Assert.That(bot.TimelineAware, Is.False);
            PlayRoundStage(Real, 13, bot);
        }

        [Test]
        public void The_same_seed_decides_the_same_rotation()
        {
            var db = Real;
            var save = GridGame.Meta.Progression.NewGame(db, "ModeB");
            var setup = GridGame.Meta.BattleFactory.Build(db, save, db.CampaignStages()[0].Id, 17, 1, TurnMode.UnmovedThisRound);
            var a = new List<string>();
            var b = new List<string>();
            BotRunner.Play(db, setup, new HeroBot(17), 200, (cmd, ev, st) => { if (cmd != null) a.Add(cmd.ToString()); });
            BotRunner.Play(db, setup, new HeroBot(17), 200, (cmd, ev, st) => { if (cmd != null) b.Add(cmd.ToString()); });
            Assert.That(a, Is.Not.Empty);
            Assert.That(b, Is.EqualTo(a), "the bot is deterministic for a seed");
        }

        /// <summary>GG-T798, decided: the enemy AI does not notice who has moved this round. Targeting (personality, taunt)
        /// and charges read the board and HP, never <see cref="Unit.ActedThisRound"/>, so a foe's pick is the same whether
        /// or not its target has taken its place (a moved hero is no softer a target: it can still be pincered by allies
        /// and acts again next round).</summary>
        [Test]
        public void Enemy_targeting_ignores_who_has_moved_this_round()
        {
            var (s, _) = Trio();
            var goblin = s.LivingList(Team.Enemy).First();
            var before = GridGame.Core.Rules.EnemyPlanner.PickTarget(s, goblin);
            foreach (var h in s.LivingList(Team.Hero)) h.ActedThisRound = h != before;
            Assert.That(GridGame.Core.Rules.EnemyPlanner.PickTarget(s, goblin), Is.SameAs(before));
            foreach (var h in s.LivingList(Team.Hero)) h.ActedThisRound = h == before;
            Assert.That(GridGame.Core.Rules.EnemyPlanner.PickTarget(s, goblin), Is.SameAs(before));
        }

        // ── GG-T1090 (GG-T1087): the smart bot's save-vs-spend round planner ────────────────────────

        /// <summary>The knight (1,3) and the cleric (4,3) flank the goblin (2,3)'s row: the cleric's one step to (3,3)
        /// closes a pincer (Complete), while the mage in the far corner (5,7) is more than <see cref="PincerPlanner.MaxSearchSteps"/>
        /// from any tile beside the goblin (Kind None).</summary>
        private static BattleState SaveBoard(TurnMode mode)
        {
            var setup = new BattleSetup { Stage = Stage(("goblin", 2, 3)), Seed = 7, UnlockedSlots = 5, TurnMode = mode };
            foreach (var (cls, x, y) in new[] { ("knight", 1, 3), ("cleric", 4, 3), ("mage", 5, 7) })
                setup.Party.Add(new HeroSpec { ClassId = cls, Start = new Coord(x, y) });
            var s = BattleEngine.Create(Mini(), setup, new List<BattleEvent>());
            Assume.That(PincerPlanner.Plan(s, Hero(s, "cleric"), PincerPlanner.MaxSearchSteps).Kind, Is.EqualTo(PincerPlanKind.Complete), "the cleric has pincer prospects");
            Assume.That(PincerPlanner.Plan(s, Hero(s, "mage"), PincerPlanner.MaxSearchSteps).Kind, Is.EqualTo(PincerPlanKind.None), "the mage has none");
            return s;
        }

        /// <summary>Runs HeroBot.SaveForPincers on a hand-made candidate list: a cleric shuffle (10), a mage action (5) and
        /// the cleric's real pincer (12, index 2). Returns the scores after the round planner has charged them.</summary>
        private static float[] Saved(BattleState s)
        {
            var cleric = Hero(s, "cleric");
            var mage = Hero(s, "mage");
            var candidates = new List<(BattleCommand cmd, float score)>
            {
                (new MoveCommand { HeroId = cleric.Id, Path = P((4, 4)) }, 10f),
                (new MoveCommand { HeroId = mage.Id, Path = P((5, 6)) }, 5f),
                (new MoveCommand { HeroId = cleric.Id, Path = P((3, 3)) }, 12f),
            };
            var m = typeof(HeroBot).GetMethod("SaveForPincers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.That(m, Is.Not.Null, "HeroBot.SaveForPincers");
            m.Invoke(null, new object[] { s, candidates, new HashSet<int> { 2 } });
            return candidates.Select(c => c.score).ToArray();
        }

        [Test]
        public void Mid_round_the_planner_saves_the_hero_with_prospects_and_spends_the_one_without()
        {
            var s = SaveBoard(TurnMode.UnmovedThisRound);
            TurnRules.MarkActed(s, Hero(s, "knight"));
            Assert.That(Hero(s, "knight").ActedThisRound, Is.True, "mid-round");
            Assert.That(TurnRules.HeroesLeftToAct(s).Count, Is.EqualTo(2));

            var after = Saved(s);
            Assert.That(after[0], Is.EqualTo(10f - HeroBot.RoundSaveCost), "the saved cleric's non-pincer action is charged");
            Assert.That(after[1], Is.EqualTo(5f), "the mage (no prospects) is never charged");
            Assert.That(after[2], Is.EqualTo(12f), "a real pincer is never charged");
            Assert.That(after[1], Is.GreaterThan(after[0]), "short of the pincer, the mage spends the turn");
        }

        [Test]
        public void Mid_round_the_smart_bot_still_takes_the_saved_heros_real_pincer()
        {
            var s = SaveBoard(TurnMode.UnmovedThisRound);
            var cleric = Hero(s, "cleric");
            TurnRules.MarkActed(s, Hero(s, "knight"));
            var cmd = new HeroBot(7).Decide(s);
            Assert.That(cmd, Is.InstanceOf<MoveCommand>(), cmd?.ToString());
            var move = (MoveCommand)cmd;
            Assert.That(move.HeroId, Is.EqualTo(cleric.Id), "the saved hero spends its place on the pincer");
            Assert.That(BattleEngine.PreviewMove(s, move.HeroId, move.Path).Pincers, Is.Not.Empty, "a real pincer");
        }

        [Test]
        public void At_round_start_nothing_is_charged()
        {
            var s = SaveBoard(TurnMode.UnmovedThisRound);
            Assert.That(s.Units.Any(u => u.ActedThisRound), Is.False);
            Assert.That(Saved(s), Is.EqualTo(new[] { 10f, 5f, 12f }));
        }

        [Test]
        public void With_one_hero_left_unmoved_nothing_is_charged()
        {
            var s = SaveBoard(TurnMode.UnmovedThisRound);
            TurnRules.MarkActed(s, Hero(s, "knight"));
            TurnRules.MarkActed(s, Hero(s, "mage"));
            Assert.That(TurnRules.HeroesLeftToAct(s), Is.EquivalentTo(new[] { Hero(s, "cleric") }), "the round's last unmoved hero");
            Assert.That(Saved(s), Is.EqualTo(new[] { 10f, 5f, 12f }));
        }

        [TestCase(TurnMode.PartyWindow)]
        [TestCase(TurnMode.AnyHeroEveryTurn)]
        public void Other_modes_decide_as_before(TurnMode mode)
        {
            var s = SaveBoard(mode);
            TurnRules.MarkActed(s, Hero(s, "knight"));
            Assert.That(s.Units.Any(u => u.ActedThisRound), Is.False, mode + " never sets ActedThisRound");
            Assert.That(Saved(s), Is.EqualTo(new[] { 10f, 5f, 12f }), mode + ": no round save");
        }

        // ── GG-T795: per-turn systems under rounds — they count party turns, not rounds ─────────────

        [Test]
        public void A_drop_banks_at_most_one_time_bank_orb()
        {
            // One action per party turn, as in AnyHeroEveryTurn, so the per-action cap applies (§2.13.2).
            var (s, _) = Battle(Mini(), Stage(("wolf", 0, 0)), ("knight", 5, 7), ("cleric", 4, 7));
            var wolf = s.Units.First(u => u.Def.Id == "wolf");
            s.TurnEntryOf(wolf.Id).Ip = BattleConfig.BarLength - BattleEngine.TurnRatePerTick(s, wolf) * 10; // a 1 s gap, room for a drag
            Assert.That(TurnRules.MaxTimeBankOrbs(s), Is.EqualTo(BattleConfig.AnyHeroMaxTimeBankOrbs));
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = Hero(s, "knight").Id, Path = P((5, 6)), TimeLeftSeconds = 3.9f, HeldSeconds = BattleEngine.MinDragSeconds });
            Assert.That(ev.OfType<AbilityRefusedEvent>(), Is.Empty);
            Assert.That(ev.OfType<OrbMintedEvent>().Count(o => o.Reason == "time bank"), Is.EqualTo(BattleConfig.AnyHeroMaxTimeBankOrbs));
        }

        [Test]
        public void Cooldowns_count_party_turns_for_a_hero_waiting_out_the_round()
        {
            var (s, _) = Trio();
            var knight = Hero(s, "knight");
            Acts(s, knight); // the knight's place this round is used
            knight.Cooldowns["shield_bash"] = 2;
            Acts(s, Hero(s, "cleric"));
            Assert.That(knight.Cooldowns["shield_bash"], Is.EqualTo(1), "one party turn passed while the knight waited");
            var ev = Acts(s, Hero(s, "mage"));
            Assert.That(Rounds(ev).Count, Is.EqualTo(1), "the mage closed the round");
            Assert.That(knight.Cooldowns["shield_bash"], Is.EqualTo(0), "two party turns: ready as the knight's place returns");
        }

        [Test]
        public void Turns_buffs_and_tempo_count_party_turns_not_rounds()
        {
            var (s, _) = Trio();
            var mage = Hero(s, "mage");
            var ward = new BuffDef { Id = "test_ward", Name = "Ward", Kind = BuffKind.Buff, Unit = DurationUnit.Turns, Duration = 2 };
            mage.Buffs.Add(new BuffInstance { Def = ward, Remaining = 2 });
            s.Tempo.Add(new TempoEffect { Source = "test", Amount = 1, TurnsLeft = 2, Fresh = false });

            Acts(s, Hero(s, "knight"));
            Assert.That(mage.Buffs.Single(b => b.Def.Id == "test_ward").Remaining, Is.EqualTo(1), "a buff ticks once per party turn");
            Assert.That(s.Tempo.Single(t => t.Source == "test").TurnsLeft, Is.EqualTo(1));

            Acts(s, Hero(s, "cleric"));
            Assert.That(s.Round, Is.EqualTo(1), "still the first round");
            Assert.That(mage.HasBuff("test_ward"), Is.False, "2 Turns = two party turns, inside one round");
            Assert.That(s.Tempo.Any(t => t.Source == "test"), Is.False);
        }

        [Test]
        public void Guard_protects_and_banks_as_in_the_other_modes_and_spends_no_place()
        {
            var (s, _) = Trio();
            var goblin = s.Units.First(u => u.Def.Id == "goblin");
            int charge = TurnRules.GuardCharge(s, 0f);
            Assert.That(charge, Is.EqualTo(BattleEngine.GuardChargeFor(BattleEngine.TurnWindowSeconds(s))), "the bank is the whole window, as in PartyWindow");
            var ev = BattleEngine.Apply(s, new ShieldCommand());
            Assert.That(ev.OfType<BuffAppliedEvent>().Count(b => b.BuffId == "protection"), Is.EqualTo(3), "every hero is protected");
            int turn = ev.FindIndex(e => e is TurnStartedEvent t && t.UnitId == goblin.Id);
            int expired = ev.FindIndex(e => e is BuffRemovedEvent r && r.BuffId == "protection" && r.Reason == "expired");
            Assert.That(turn, Is.GreaterThanOrEqualTo(0), "the enemy acted");
            Assert.That(expired, Is.GreaterThan(turn), "Protection held through the enemy turn and fell away at the boundary");
            Assert.That(s.Units.Any(u => u.ActedThisRound), Is.False, "Guard used no hero's place in the round");
        }

        [Test]
        public void A_frozen_foe_thaws_after_one_party_turn()
        {
            var (s, _) = Trio();
            var goblin = s.Units.First(u => u.Def.Id == "goblin");
            goblin.Buffs.Add(new BuffInstance { Def = s.Content.Buff("frozen"), Remaining = 1, Fresh = false });
            Assert.That(goblin.IsTimelineHalted, Is.True);
            Acts(s, Hero(s, "knight"));
            Assert.That(goblin.HasBuff("frozen"), Is.False, "Frozen 1 Turn = one party turn, not one round");
        }

        // ── GG-T796: the RoundStart trigger ─────────────────────────────────────────────────────

        [Test]
        public void Round_start_triggers_fire_for_every_hero_once_per_round_after_turn_start()
        {
            var db = Mini();
            foreach (var (id, on) in new[] { ("t_start", TriggerOn.TurnStart), ("t_round", TriggerOn.RoundStart) })
                db.Abilities[id] = new AbilityDef
                {
                    Id = id, Name = id, Kind = AbilityKind.Passive,
                    Trigger = new TriggerDef { On = on, Effects = { new TriggerEffect { Kind = TriggerEffectKind.ApplyBuff, Buff = "warm" } } },
                };
            var setup = new BattleSetup { Stage = Stage(("goblin", 5, 0)), Seed = 7, UnlockedSlots = 5, TurnMode = TurnMode.UnmovedThisRound };
            foreach (var (cls, x) in new[] { ("knight", 0), ("mage", 4) })
                setup.Party.Add(new HeroSpec { ClassId = cls, Start = new Coord(x, 6), Passives = new List<string> { "t_start", "t_round" } });
            var s = BattleEngine.Create(db, setup, new List<BattleEvent>());
            var knight = Hero(s, "knight");
            var mage = Hero(s, "mage");
            int Fired(List<BattleEvent> ev, Unit u, TriggerOn on) => ev.OfType<TriggeredEvent>().Count(t => t.UnitId == u.Id && t.On == on);

            var first = Acts(s, knight);
            Assert.That(Rounds(first), Is.Empty);
            Assert.That(Fired(first, knight, TriggerOn.TurnStart), Is.EqualTo(1));
            Assert.That(Fired(first, knight, TriggerOn.RoundStart) + Fired(first, mage, TriggerOn.RoundStart), Is.EqualTo(0), "no new round yet");

            var last = Acts(s, mage);
            Assert.That(Rounds(last).Single().Round, Is.EqualTo(2));
            foreach (var h in new[] { knight, mage })
            {
                Assert.That(Fired(last, h, TriggerOn.RoundStart), Is.EqualTo(1), h.Def.Id + ": one RoundStart for the new round");
                int turn = last.FindIndex(e => e is TriggeredEvent t && t.UnitId == h.Id && t.On == TriggerOn.TurnStart);
                int round = last.FindIndex(e => e is TriggeredEvent t && t.UnitId == h.Id && t.On == TriggerOn.RoundStart);
                Assert.That(round, Is.GreaterThan(turn), h.Def.Id + ": RoundStart after the party turn's TurnStart");
            }
        }

        // ── GG-T1073 (GG-T796): region rules, joins, triggers and the badge under rounds ──────────

        /// <summary>A setup in <paramref name="mode"/> with <paramref name="heroes"/> (each with the given passives).</summary>
        private static BattleSetup Setup(StageDef stage, TurnMode mode, params (string cls, int x, int y, string[] passives)[] heroes)
        {
            var setup = new BattleSetup { Stage = stage, Seed = 7, UnlockedSlots = 5, TurnMode = mode };
            foreach (var h in heroes)
                setup.Party.Add(new HeroSpec { ClassId = h.cls, Start = new Coord(h.x, h.y), Passives = (h.passives ?? new string[0]).ToList() });
            return setup;
        }

        private static BattleState Create(ContentDb db, BattleSetup setup) => BattleEngine.Create(db, setup, new List<BattleEvent>());

        /// <summary>Party turn <paramref name="i"/> (0-based) of a round robin over <paramref name="order"/>: each hero in
        /// turn, so every round is one pass, each stepping up on its first pass and back down on its second. In
        /// PartyWindow every other hero counts as acted (TestKit.Turn), so the same step is the whole party turn there.</summary>
        private static List<BattleEvent> RoundRobin(BattleState s, int i, params string[] order)
        {
            var h = Hero(s, order[i % order.Length]);
            int dy = (i / order.Length) % 2 == 0 ? -1 : 1;
            var cmd = new MoveCommand { HeroId = h.Id, Path = new List<Coord> { new Coord(h.Pos.X, h.Pos.Y + dy) }, HeldSeconds = BattleEngine.MinDragSeconds };
            var ev = s.TurnMode == TurnMode.PartyWindow ? Turn(s, cmd) : BattleEngine.Apply(s, cmd);
            Assert.That(ev.OfType<AbilityRefusedEvent>(), Is.Empty, $"{s.TurnMode} turn {i}: {h.Def.Id} acts");
            Assert.That(NewTurn(ev), Is.True, $"{s.TurnMode} turn {i} is a party turn");
            return ev;
        }

        private static readonly string[] Three = { "knight", "cleric", "mage" };

        /// <summary>TurnModeAnyHeroTests' flood rule (the Drowned Arks' tide, §14.20.4): at BattleStart (4,6) and (4,5) are
        /// flooded, or a timed "rise" is scheduled to flood (5,7) after 6 s; a hero standing in the flood is soaked (Warm)
        /// at its TurnStart and its TurnEnd. <paramref name="summon"/>: a hero's TurnEnd in the flood also brings a goblin
        /// at (5,2), a rule summon joining mid-round.</summary>
        private static (ContentDb db, StageDef stage) Flooded(bool timed, bool summon = false)
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
                : new RuleEffectDef { Kind = RuleEffectKind.MarkTiles, Area = RuleArea.Tiles, Tiles = { new Coord(4, 6), new Coord(4, 5) }, State = "flood" });
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
            return (db, stage);
        }

        /// <summary>How often the flood rule's hook <paramref name="hook"/> (1 TurnStart, 2 TurnEnd) has matched.</summary>
        private static int Runs(BattleState s, int hook) => s.Region.HookCounts.TryGetValue("turn-flood#" + hook, out int n) ? n : 0;

        [Test]
        public void A_heros_region_turn_hooks_run_once_per_party_turn_not_once_per_round()
        {
            var (db, stage) = Flooded(timed: false);
            var (s, _) = Battle(db, stage, ("knight", 0, 6), ("cleric", 2, 6), ("mage", 4, 6));
            Assert.That(s.Region.At(new Coord(4, 6))?.State, Is.EqualTo("flood"), "the mage stands in the flood");
            int starts = Runs(s, 1), ends = Runs(s, 2);
            var mage = Hero(s, "mage");
            for (int i = 0; i < 6; i++)
            {
                RoundRobin(s, i, Three);
                Assert.That(s.Region.At(mage.Pos)?.State, Is.EqualTo("flood"), "the mage only wades between flooded tiles");
                Assert.That(Runs(s, 1), Is.EqualTo(starts + i + 1), "the mage's TurnStart in the water, once per party turn");
                Assert.That(Runs(s, 2), Is.EqualTo(ends + i + 1), "and its TurnEnd, whoever acted");
                Assert.That(mage.HasBuff("warm"), Is.True, "soaked whether or not it has its place yet");
            }
            Assert.That(s.Round, Is.EqualTo(3), "six party turns were two rounds, and the hooks counted the turns");
        }

        [Test]
        public void A_timed_rule_event_lands_after_the_same_party_turns_as_in_party_window()
        {
            // Rising water and the anchor heave are timeline events on the clock: a party turn still runs to the next enemy
            // arrival whoever takes it, so rounds see them land when a full PartyWindow window would.
            (int turns, long clock) Play(TurnMode mode)
            {
                var (db, stage) = Flooded(timed: true);
                var s = Create(db, Setup(stage, mode, ("knight", 0, 6, null), ("cleric", 2, 6, null), ("mage", 4, 6, null)));
                for (int i = 0; i < 30 && !s.IsOver; i++)
                    if (RoundRobin(s, i, Three).OfType<HazardLandedEvent>().Any(h => h.EventId == "rise")) return (i + 1, (long)s.Clock);
                return (-1, (long)s.Clock);
            }
            var r = Play(TurnMode.UnmovedThisRound);
            var w = Play(TurnMode.PartyWindow);
            Assert.That(r.turns, Is.GreaterThan(0), "the water rose");
            Assert.That(r, Is.EqualTo(w));
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
            var s = Create(db, Setup(Stage(("goblin", 5, 0)), TurnMode.UnmovedThisRound, ("knight", 0, 6, all), ("mage", 4, 6, all)));
            var knight = Hero(s, "knight");
            var mage = Hero(s, "mage");
            int Fired(List<BattleEvent> ev, Unit u, TriggerOn on) => ev.OfType<TriggeredEvent>().Count(t => t.UnitId == u.Id && t.On == on);

            for (int i = 0; i < 4; i++)
            {
                var actor = i % 2 == 0 ? knight : mage;
                var other = i % 2 == 0 ? mage : knight;
                var ev = RoundRobin(s, i, "knight", "mage");
                foreach (var h in new[] { knight, mage })
                {
                    Assert.That(Fired(ev, h, TriggerOn.TurnStart), Is.EqualTo(1), $"turn {i} {h.Def.Id}: one party turn, one TurnStart");
                    Assert.That(Fired(ev, h, TriggerOn.TurnEnd), Is.EqualTo(1), $"turn {i} {h.Def.Id}: and one TurnEnd");
                }
                Assert.That(Fired(ev, actor, TriggerOn.ActionEnd), Is.EqualTo(1), $"turn {i}: the actor's action ended");
                Assert.That(Fired(ev, other, TriggerOn.ActionEnd), Is.EqualTo(0), $"turn {i}: the hero waiting took no action");
            }
            Assert.That(s.Round, Is.EqualTo(3), "four party turns, two rounds: the triggers counted turns");
        }

        [Test]
        public void A_foe_counts_its_own_turns_not_hero_actions_or_rounds()
        {
            // What a rule break counts (TwinTurn, GravityStamp's every third turn) is the breaker's own TurnsTaken.
            var (s, _) = Trio();
            var goblin = s.Units.First(u => u.Def.Id == "goblin");
            int before = goblin.TurnsTaken, turns = 0;
            for (int i = 0; i < 4; i++)
                turns += RoundRobin(s, i, Three).OfType<TurnStartedEvent>().Count(t => t.UnitId == goblin.Id);
            Assert.That(turns, Is.GreaterThanOrEqualTo(4), "a lone foe arrives once per party turn, not once per round");
            Assert.That(goblin.TurnsTaken - before, Is.EqualTo(turns));
            Assert.That(goblin.ActedThisRound, Is.False, "a foe never holds a place in the round");
        }

        [Test]
        public void A_tamed_ally_counts_its_own_turns_and_never_takes_a_place_in_the_round()
        {
            var (s, _) = Battle(Mini(), Stage(("tank", 5, 0), ("wolf", 0, 2)), ("knight", 0, 6), ("cleric", 2, 6), ("mage", 4, 6));
            var wolf = s.Units.First(u => u.Def.Id == "wolf");
            wolf.Team = Team.Hero; wolf.Tamed = true; wolf.TameTurnsLeft = 99; wolf.BondNeeded = 999;
            TurnRules.ClearActed(s, wolf);
            Assert.That(wolf.IsIndependent, Is.True);
            Assert.That(BattleEngine.HeroesLeftToAct(s), Does.Not.Contain(wolf), "not the party's to command");
            Assert.That(BattleEngine.HeroesLeftToAct(s).Count, Is.EqualTo(3));

            int grew = 0;
            for (int i = 0; i < 3; i++)
            {
                var ev = RoundRobin(s, i, Three);
                grew += ev.OfType<BondGrewEvent>().Count(b => b.UnitId == wolf.Id);
                Assert.That(wolf.ActedThisRound, Is.False, "its own turns never use a place in the round");
                Assert.That(BattleEngine.HeroesLeftToAct(s), Does.Not.Contain(wolf));
                if (i < 2) Assert.That(BattleEngine.HeroesLeftToAct(s).Count, Is.EqualTo(2 - i), "the round waits only for heroes");
            }
            Assert.That(s.Round, Is.EqualTo(2), "three hero actions were the whole round: the wolf held nothing up");
            Assert.That(BattleEngine.HeroesLeftToAct(s).Count, Is.EqualTo(3));
            Assert.That(wolf.TameTurnsLeft, Is.EqualTo(99 - grew), "the tame counts the wolf's own turns, not the party's or rounds");
        }

        [Test]
        public void A_wanderer_is_independent_and_never_takes_a_place_in_the_round()
        {
            var setup = Setup(Stage(("tank", 5, 0)), TurnMode.UnmovedThisRound, ("knight", 0, 6, null), ("cleric", 2, 6, null));
            setup.Wanderer = new WandererSpec { Class = "mage", Wave = 0 };
            var s = Create(Mini(), setup);
            var wanderer = s.Units.Single(u => u.Wanderer);
            Assert.That(wanderer.IsIndependent, Is.True);
            Assert.That(BattleEngine.CanMove(s, wanderer.Id), Is.False);
            Assert.That(BattleEngine.HeroesLeftToAct(s).Count, Is.EqualTo(2));

            int before = wanderer.TurnsTaken, turns = 0;
            for (int i = 0; i < 4; i++)
            {
                var ev = RoundRobin(s, i, "knight", "cleric");
                turns += ev.OfType<TurnStartedEvent>().Count(t => t.UnitId == wanderer.Id);
                Assert.That(wanderer.ActedThisRound, Is.False);
                Assert.That(BattleEngine.HeroesLeftToAct(s), Does.Not.Contain(wanderer));
            }
            Assert.That(s.Round, Is.EqualTo(3), "two heroes, two rounds in four turns: the wanderer held nothing up");
            Assert.That(wanderer.TurnsTaken - before, Is.EqualTo(turns), "it counts its own turns");
        }

        [Test]
        public void A_unit_summoned_mid_round_never_holds_the_round_up()
        {
            var (db, stage) = Flooded(timed: false, summon: true);
            var (s, _) = Battle(db, stage, ("knight", 0, 6), ("cleric", 2, 6), ("mage", 4, 6));
            var knight = Hero(s, "knight");
            var cleric = Hero(s, "cleric");
            var mage = Hero(s, "mage");
            int foes = s.Units.Count(u => u.Alive && u.Team == Team.Enemy);

            var first = RoundRobin(s, 0, Three);
            var summoned = first.OfType<ReinforcementsArrivedEvent>().SelectMany(r => r.Spawned).Select(s.UnitById).ToList();
            Assert.That(summoned, Is.Not.Empty, "the mage's TurnEnd in the flood summoned a goblin");
            Assert.That(s.Units.Count(u => u.Alive && u.Team == Team.Enemy), Is.GreaterThan(foes));
            foreach (var u in summoned) Assert.That(u.ActedThisRound, Is.False, "it joins with no action spent");
            Assert.That(BattleEngine.HeroesLeftToAct(s), Is.EquivalentTo(new[] { cleric, mage }), "the round still waits only for its heroes");
            Assert.That(s.Round, Is.EqualTo(1));

            RoundRobin(s, 1, Three);
            var last = RoundRobin(s, 2, Three);
            Assert.That(Rounds(last).Single().Round, Is.EqualTo(2), "the summons never took a hero's place");
            Assert.That(BattleEngine.HeroesLeftToAct(s), Is.EquivalentTo(new[] { knight, cleric, mage }));
        }

        [Test]
        public void The_countdown_badge_reads_the_same_as_party_window()
        {
            BattleState Make(TurnMode mode) =>
                Create(Mini(), Setup(Stage(("goblin", 5, 0), ("wolf", 0, 0)), mode, ("knight", 0, 6, null), ("cleric", 2, 6, null), ("mage", 4, 6, null)));
            var r = Make(TurnMode.UnmovedThisRound);
            var w = Make(TurnMode.PartyWindow);
            for (int i = 0; i < 4; i++)
            {
                foreach (var foe in r.Units.Where(u => u.Team == Team.Enemy && u.Alive))
                    Assert.That(TurnRules.CountdownLeft(r, foe.Id), Is.EqualTo(TurnRules.CountdownLeft(w, foe.Id)), $"{foe.Def.Id} after {i} turns");
                RoundRobin(r, i, Three);
                RoundRobin(w, i, Three);
            }
        }
    }
}
