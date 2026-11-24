using System;
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
    /// GG-T768 (bible §2.13): the <see cref="TurnRules"/> seam. PartyWindow through the seam is today's GG-T308/GG-T311
    /// rule exactly — every answer matches the pre-seam rule at every step of a played battle, and the battle replays
    /// identically from its commands — and a mode that isn't built yet refuses to run.
    /// </summary>
    public class TurnRulesTests
    {
        // ── The pre-seam rule (GG-T308/GG-T311, §2.0), kept verbatim as the oracle ────────────────────

        private static List<int> LegacyLeftToAct(BattleState s) =>
            s.Units.Where(u => u.Alive && u.IsCommandable && u.OnBoard && !u.IsImmobile && !u.ActedThisWindow).Select(u => u.Id).ToList();

        private static int LegacyTurnTicks(BattleState s)
        {
            int best = -1;
            foreach (var e in s.Timeline)
            {
                if (e.Kind != TimelineKind.Turn) continue;
                var o = s.UnitById(e.OwnerId);
                if (o == null || o.IsHero) continue;
                int t = BattleEngine.TicksUntilTurn(s, e.OwnerId);
                if (t > 0 && (best < 0 || t < best)) best = t;
            }
            return best;
        }

        private static bool LegacyFits(BattleState s, float seconds)
        {
            if (!(seconds > 0f)) return true;
            int gap = LegacyTurnTicks(s);
            return gap < 0 || gap >= BattleEngine.NeedTicks(seconds);
        }

        private static bool LegacyCanMove(BattleState s, Unit h) =>
            s.Phase == BattlePhase.PlayerInput && h.Alive && h.IsCommandable && !h.IsImmobile && !h.ActedThisWindow
            && LegacyFits(s, BattleEngine.MinDragSeconds);

        private static bool LegacyTooLittleTime(BattleState s)
        {
            if (s.Phase != BattlePhase.PlayerInput || LegacyTurnTicks(s) < 0) return false;
            if (LegacyLeftToAct(s).Count == 0) return false;
            return !LegacyFits(s, Math.Min(BattleEngine.MinDragSeconds, BattleConfig.HeroActionSeconds));
        }

        private static int LegacyGuardCharge(BattleState s)
        {
            int t = LegacyTurnTicks(s);
            float w = t < 0 ? BattleConfig.ActionTicks / (float)BattleConfig.TicksPerSecond : t / (float)BattleConfig.TicksPerSecond;
            return BattleEngine.GuardChargeFor(w);
        }

        private static void AssertSeamMatchesLegacy(BattleState s)
        {
            Assert.That(BattleEngine.TurnTicks(s), Is.EqualTo(LegacyTurnTicks(s)), "TurnTicks");
            Assert.That(BattleEngine.HeroesLeftToAct(s).Select(u => u.Id), Is.EqualTo(LegacyLeftToAct(s)), "HeroesLeftToAct");
            Assert.That(BattleEngine.TooLittleTime(s), Is.EqualTo(LegacyTooLittleTime(s)), "TooLittleTime");
            Assert.That(BattleEngine.GuardCharge(s), Is.EqualTo(LegacyGuardCharge(s)), "GuardCharge");
            foreach (var h in s.Units.Where(u => u.IsHero))
            {
                Assert.That(BattleEngine.CanMove(s, h.Id), Is.EqualTo(LegacyCanMove(s, h)), $"CanMove {h.Id}");
                Assert.That(TurnRules.HasActed(s, h), Is.EqualTo(h.ActedThisWindow), $"HasActed {h.Id}");
            }
            var use = new UseSlotCommand { HeroId = -1, Slot = 0 };
            Assert.That(BattleEngine.CommandTicks(s, use), Is.EqualTo(Math.Max(1, GridGame.Core.Rules.Formulas.SecondsToTicks(BattleConfig.HeroActionSeconds))), "CommandTicks use");
            Assert.That(BattleEngine.CommandTicks(s, new ShieldCommand()), Is.EqualTo(0), "CommandTicks guard");
        }

        private static BattleSetup Setup(ContentDb db, TurnMode mode) => new BattleSetup
        {
            Stage = db.StageOrder.First(st => !st.IsTest),
            Seed = 42,
            UnlockedSlots = 5,
            Party = ContentAndSoakTests.StarterParty(db, 3),
            TurnMode = mode,
        };

        [Test]
        public void Party_window_replays_identically_through_the_seam()
        {
            var db = Real;
            var setup = Setup(db, TurnMode.PartyWindow);
            var commands = new List<BattleCommand>();
            var played = new StringBuilder();
            int steps = 0;
            var final = BotRunner.Play(db, setup, new HeroBot(5), 120, (cmd, ev, st) =>
            {
                if (cmd != null) commands.Add(cmd);
                foreach (var e in ev) played.AppendLine(e.ToString());
                if (!st.IsOver) { AssertSeamMatchesLegacy(st); steps++; }
            });
            Assert.That(commands, Is.Not.Empty);
            Assert.That(steps, Is.GreaterThan(1), "the seam was compared over a played battle");

            // Replaying the same commands from the same setup gives the same events, clock and outcome.
            var opening = new List<BattleEvent>();
            var s = BattleEngine.Create(db, setup, opening);
            var replayed = new StringBuilder();
            foreach (var e in opening) replayed.AppendLine(e.ToString());
            foreach (var cmd in commands) foreach (var e in BattleEngine.Apply(s, cmd)) replayed.AppendLine(e.ToString());
            Assert.That(replayed.ToString(), Is.EqualTo(played.ToString()));
            Assert.That(s.Clock, Is.EqualTo(final.Clock));
            Assert.That(s.Phase, Is.EqualTo(final.Phase));
            Assert.That(s.Results.ActionsTaken, Is.EqualTo(final.Results.ActionsTaken));
        }

        [TestCase(TurnMode.UnmovedThisRound)]
        [TestCase(TurnMode.HeroTimeline)]
        public void Unbuilt_modes_refuse_through_the_seam(TurnMode mode)
        {
            var db = Real;
            var s = BattleEngine.Create(db, Setup(db, mode), new List<BattleEvent>());
            Assert.Throws<NotSupportedException>(() => TurnRules.TurnTicks(s));
            Assert.Throws<NotSupportedException>(() => TurnRules.HeroesLeftToAct(s));
            Assert.Throws<NotSupportedException>(() => TurnRules.EndsPartyTurn(s, new ShieldCommand()));
            Assert.Throws<NotSupportedException>(() => TurnRules.GuardCharge(s, 0f));
            Assert.Throws<NotSupportedException>(() => TurnRules.ResetTurn(s));
        }
    }
}
