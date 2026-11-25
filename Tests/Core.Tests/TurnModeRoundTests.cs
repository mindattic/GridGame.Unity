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
    /// GG-T1066 (bible §2.13.2, rules GG-T794): <see cref="TurnMode.UnmovedThisRound"/> through the <see cref="TurnRules"/>
    /// seam. Each party turn one hero acts, and only a hero that hasn't acted this round (<see cref="Unit.ActedThisRound"/>);
    /// once every able hero has acted the round resets (<see cref="BattleState.Round"/> + 1, a
    /// <see cref="RoundStartedEvent"/>). Fallen, Frozen and Asleep heroes are skipped, a revived hero keeps its mark, a
    /// joiner may act this round, Guard uses no place, a new wave resets the round, Clone carries it all and a bot battle
    /// replays identically.
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
    }
}
