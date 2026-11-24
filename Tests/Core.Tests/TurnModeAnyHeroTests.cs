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
    /// action gap, forced Guard and Rally still apply, and a battle replays identically from its commands.
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
    }
}
