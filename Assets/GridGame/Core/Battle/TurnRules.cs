using System;
using System.Collections.Generic;
using GridGame.Core.Rules;

namespace GridGame.Core.Battle
{
    /// <summary>
    /// The turn-mode seam (bible §2.13, GG-T768): the <b>one</b> place a <see cref="TurnMode"/> answers
    /// (1) who may act now, (2) whether an action ends the party's turn, (3) what an action spends, (4) what Guard gives
    /// up and banks, and (5) what a turn boundary resets. <see cref="BattleEngine"/>, <see cref="BattleRunner"/>,
    /// <see cref="Recruitment"/> and the tame/wanderer joins ask it; nothing else reads <see cref="BattleState.TurnMode"/>.
    /// <see cref="TurnMode.PartyWindow"/> is today's GG-T308/GG-T311 rule exactly (§2.0, §2.13.0) and must replay
    /// identically; a mode whose feature hasn't landed throws <see cref="NotSupportedException"/>.
    /// </summary>
    public static class TurnRules
    {
        /// <summary>Throws for a mode that isn't built yet (§2.13.1–§2.13.3 land with their own features).</summary>
        private static void Require(BattleState s)
        {
            if (s.TurnMode != TurnMode.PartyWindow)
                throw new NotSupportedException($"Turn mode {s.TurnMode} is not built yet (bible §2.13)");
        }

        // ── Countdown badge (GG-T918, §2.10) ───────────────────────────────────────────────────────

        /// <summary>The countdown the badge shows for a foe: party turns until its pending cast lands, else until its turn
        /// (0 = at the end of your next turn); -1 with neither or Frozen. Counted in the party's own turns, so the same
        /// seam answers in every mode (it never reads who may act, only when the enemy side arrives).</summary>
        public static int CountdownLeft(BattleState s, int unitId)
        {
            int n = BattleEngine.ActionsUntilCast(s, unitId);
            return n >= 0 || s.CastEntryOf(unitId) != null ? n : BattleEngine.ActionsUntilTurn(s, unitId);
        }

        // ── 1. Who may act now ─────────────────────────────────────────────────────────────────────

        /// <summary>Has <paramref name="hero"/> used its action for the party's current turn? PartyWindow: it acted in this
        /// window (GG-T308, one action per hero per window).</summary>
        public static bool HasActed(BattleState s, Unit hero)
        {
            Require(s);
            return hero.ActedThisWindow;
        }

        /// <summary>May this hero be picked up or use its bar now, time aside? PartyWindow: alive, commanded, not
        /// Immobile and not yet acted this window. (The phase and the gap are the caller's, as before.)</summary>
        public static bool MayAct(BattleState s, Unit hero)
        {
            Require(s);
            return hero.Alive && hero.IsCommandable && !hero.IsImmobile && !hero.ActedThisWindow;
        }

        /// <summary>Heroes on the board who may still act in the party's turn. Empty = the turn is spent. PartyWindow:
        /// living, able heroes (not Immobile) who have not acted yet in this window.</summary>
        public static List<Unit> HeroesLeftToAct(BattleState s)
        {
            Require(s);
            var list = new List<Unit>();
            foreach (var u in s.Units)
                if (u.Alive && u.IsCommandable && u.OnBoard && !u.IsImmobile && !u.ActedThisWindow) list.Add(u);
            return list;
        }

        // ── 2. Whether an action ends the party's turn ─────────────────────────────────────────────

        /// <summary>Records that <paramref name="hero"/> has taken its action (PartyWindow: acted this window).</summary>
        public static void MarkActed(BattleState s, Unit hero)
        {
            Require(s);
            hero.ActedThisWindow = true;
        }

        /// <summary>After <paramref name="cmd"/> resolved (and its hero was marked), is the party's turn spent — the clock
        /// runs on to the next enemy turn? PartyWindow: Guard gives the window up, or every able hero has acted.</summary>
        public static bool EndsPartyTurn(BattleState s, BattleCommand cmd)
        {
            Require(s);
            return cmd is ShieldCommand || HeroesLeftToAct(s).Count == 0;
        }

        // ── 3. What an action spends ───────────────────────────────────────────────────────────────

        /// <summary>
        /// What is left of the party's turn in clock ticks. PartyWindow: until the next enemy turn icon reaches the left
        /// edge (no pushback/hasten assumed); each hero action spends some of it and a Pushback or Slow lengthens it
        /// (GG-T308). -1 when no enemy icon can arrive (none left, or every one halted) — the hold then has no limit.
        /// </summary>
        public static int TurnTicks(BattleState s)
        {
            Require(s);
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

        /// <summary>
        /// Clock ticks a hero command spends from the turn. PartyWindow: a drag the share of the window it was held (never
        /// under the minimum drag, never over the window), an Ability/Item/Spell/Talk the fixed
        /// <see cref="BattleConfig.HeroActionSeconds"/>; Guard spends the rest of the window instead (0 here).
        /// </summary>
        public static int CommandTicks(BattleState s, BattleCommand cmd)
        {
            Require(s);
            switch (cmd)
            {
                case MoveCommand m: // GG-T311: never less than the minimum drag (pickup + a tile), never more than the window
                {
                    int t = Math.Max(BattleEngine.HoldTicks(s, m.HeldSeconds), BattleEngine.NeedTicks(BattleEngine.MinDragSeconds));
                    int turn = TurnTicks(s);
                    return turn > 0 ? Math.Min(turn, t) : t;
                }
                case UseSlotCommand _:
                case TalkCommand _:
                    return Math.Max(1, Formulas.SecondsToTicks(BattleConfig.HeroActionSeconds));
                default: return 0;
            }
        }

        /// <summary>Does the clock run on by <see cref="CommandTicks"/> after this command resolves? PartyWindow: every
        /// command but Guard (Guard spends the rest of the window through the turn end instead).</summary>
        public static bool SpendsClock(BattleState s, BattleCommand cmd)
        {
            Require(s);
            return !(cmd is ShieldCommand);
        }

        // ── 4. What Guard gives up and banks ───────────────────────────────────────────────────────

        /// <summary>
        /// GG-T311: heroes are left to act but the gap is too short for any of their actions (the cheapest is a minimum
        /// drag or a skill's use time) — the party can only Guard, giving the turn up, and the enemy acts.
        /// </summary>
        public static bool TooLittleTime(BattleState s)
        {
            Require(s);
            if (s.Phase != BattlePhase.PlayerInput || TurnTicks(s) < 0) return false;
            if (HeroesLeftToAct(s).Count == 0) return false;
            return !BattleEngine.ActionFits(s, Math.Min(BattleEngine.MinDragSeconds, BattleConfig.HeroActionSeconds));
        }

        /// <summary>Is this command a forced Guard — a sacrifice, not a choice — that rallies the party (GG-T311)?
        /// Measured before the command resolves.</summary>
        public static bool IsForcedGuard(BattleState s, BattleCommand cmd)
        {
            Require(s);
            return cmd is ShieldCommand && TooLittleTime(s);
        }

        /// <summary>
        /// Guard bank (GG-T189, §2.5): the mana charge a Guard pressed after <paramref name="heldSeconds"/> earns.
        /// PartyWindow: the window minus the time already used, at <see cref="BattleConfig.GuardChargePerSecond"/>, capped
        /// at <see cref="BattleConfig.GuardMaxCharge"/>; with no enemy icon able to arrive the window is the fixed
        /// <see cref="BattleConfig.ActionTicks"/> it would spend.
        /// </summary>
        public static int GuardCharge(BattleState s, float heldSeconds)
        {
            Require(s);
            float w = BattleEngine.TurnWindowSeconds(s);
            if (w < 0f) w = BattleConfig.ActionTicks / (float)BattleConfig.TicksPerSecond;
            return BattleEngine.GuardChargeFor(w - Math.Max(0f, heldSeconds));
        }

        // ── 5. What a turn boundary resets ─────────────────────────────────────────────────────────

        /// <summary>Did this command cross a party-turn boundary (<see cref="PlayerControlEvent.NewWindow"/>)? PartyWindow:
        /// the window ran out (an enemy turn came) or a wave was cleared, which opens a fresh window.</summary>
        public static bool IsTurnBoundary(BattleState s, bool windowOver, bool waveChanged)
        {
            Require(s);
            return windowOver || waveChanged;
        }

        /// <summary>A turn boundary: what it resets. PartyWindow: every unit may act again in the new window.</summary>
        public static void ResetTurn(BattleState s)
        {
            Require(s);
            foreach (var u in s.Units) u.ActedThisWindow = false;
        }

        /// <summary>A unit joining the hero side mid-battle (tamed, a wanderer) or leaving it (a tame wears off) starts
        /// with no action spent. PartyWindow: its acted-this-window mark is cleared.</summary>
        public static void ClearActed(BattleState s, Unit u)
        {
            Require(s);
            u.ActedThisWindow = false;
        }
    }
}
