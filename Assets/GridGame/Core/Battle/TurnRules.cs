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
    /// identically. <see cref="TurnMode.AnyHeroEveryTurn"/> (§2.13.1, GG-T781): one action by any able hero ends the
    /// party's turn. A mode whose feature hasn't landed throws <see cref="NotSupportedException"/>.
    /// <para>GG-T782 (§2.13.1): what counts turns keeps counting <b>party turns</b> in every mode — one per enemy arrival
    /// (or cleared wave), the same span of enemy time whoever acts in it — so Turns buffs and Protection, skill cooldowns,
    /// Frozen/Sleep and the other Turns statuses, party tempo, Rally, TurnStart/TurnEnd and the enemy support extra turn
    /// all run unchanged off <see cref="ResetTurn"/>'s boundary. Mana is spent and earned per action (a spell, a pincer),
    /// so only income measured per turn rather than per action needs a mode answer: <see cref="MaxTimeBankOrbs"/>.</para>
    /// <para>GG-T783 (§2.13.1): nothing else counts hero actions either, so nothing else asks the mode. Region rules'
    /// timed events (rising water, the anchor heave) follow the clock; a hero's region TurnStart/TurnEnd hooks and
    /// TurnStart/TurnEnd triggers fire for every commanded hero at each boundary (ActionEnd only for the actor); rule
    /// breaks, tames, bonds and a wanderer's turns count that unit's own icon; a recruit joins through Talk, the turn's
    /// action; the countdown badge counts other icons; pickups and corpses age at the boundary. Joins and partings still
    /// come through <see cref="ClearActed"/>.</para>
    /// <para>GG-T794 (§2.13.2): <see cref="TurnMode.UnmovedThisRound"/> is AnyHeroEveryTurn's one action per party turn,
    /// taken only by a hero who hasn't acted this <b>round</b> (<see cref="Unit.ActedThisRound"/>). At a turn boundary,
    /// once every able hero has acted, the round resets (<see cref="BattleState.Round"/> + 1, every mark cleared, the runner
    /// emits a <see cref="RoundStartedEvent"/>). Able = alive, commanded, on the board and not Immobile, so a hero who
    /// falls, or is Frozen or Asleep, is skipped and never holds the round up; it keeps its mark (a revived hero that
    /// already acted waits for the next round, one that hadn't may act). A hero joining mid-round (<see cref="ClearActed"/>)
    /// may act this round and the round waits for it. Guard uses no hero's place. A new wave resets the round (each wave
    /// opens with every hero free, like its opening window). The marks live on the units and the counter on the state, so
    /// Clone and replay carry them.</para>
    /// <para>GG-T795 (§2.13.2) per-turn systems under rounds, decided: a round is only who may act, never a unit of time.
    /// A party turn still spans one enemy arrival (or a cleared wave), so everything GG-T782 kept on party turns stays
    /// there and nothing counts rounds. Guard spends no hero's place (default no): it gives the turn up and the round waits.
    /// Guard's Protection (1 Turn, through the enemy turn it skips) and bank (<see cref="GuardCharge"/> on the whole window,
    /// capped) are unchanged. Mana: pincer and Guard charge and regen are unchanged; the time bank keeps AnyHeroEveryTurn's
    /// per-action cap (<see cref="MaxTimeBankOrbs"/>), every drop being the turn's only action. Turns buffs, Protection,
    /// skill cooldowns, Frozen/Sleep/Stun and party tempo tick at each party turn's boundary, for a hero waiting out the
    /// round too (a cooldown no longer than the party is ready by its next place: the round is the cadence). Rally: a forced
    /// Guard stacks it, uses no place, and any hero action resets it. The support extra turn is enemy-side and unchanged.
    /// So no code path asks the mode beyond <see cref="MaxTimeBankOrbs"/>; TurnModeRoundTests pin each answer.</para>
    /// <para>GG-T796 (§2.13.2) rounds and the GG-T783 rules, decided: none of them counts rounds, so none asks the mode.
    /// A hero's region TurnStart/TurnEnd hooks and TurnStart/TurnEnd triggers fire for every commanded hero once per party
    /// turn, whether or not it has its place yet (ActionEnd only for the actor); RoundStart is the one per-round hook
    /// (fired when <see cref="ResetTurn"/> starts a round). Timed rule events follow the clock and the countdown badge counts party turns,
    /// so both read as in PartyWindow. Rule breaks, tames, bonds and wanderers count their own turns; an independent ally,
    /// a foe or a summon is never able this round (<see cref="AbleThisRound"/> needs a commanded hero), so none takes a
    /// hero's place or holds the round up. A recruit joins through Talk, which uses the talker's place, and may act this
    /// round (<see cref="ClearActed"/>). Naming: a region <c>RuleHookDef.Every</c> counts hook matches, not rounds, so
    /// nothing is renamed. TurnModeRoundTests pin each answer.</para>
    /// <para>GG-T1102 (§2.13.3) HeroTimeline rate and per-turn decisions. Every hero turn is a party-turn boundary
    /// (<see cref="IsTurnBoundary"/>), but a hero counts <b>its own</b> turns like a foe (GG-T809): its Turns buffs, cooldowns,
    /// regen and TurnEnd/TurnStart run only as its own icon arrives. Sleep (GG-T1100): an Immobile, non-halted hero's arrived
    /// icon is a skipped turn — it re-enters, never becomes <see cref="BattleState.ActiveHeroId"/>, emits TurnSkipped and
    /// ticks its own Turns buffs and cooldowns; Frozen halts the icon and thaws on the shared cycle. Rates: Hasten,
    /// HastenSeconds and a buff's Tempo on a hero with an icon (<see cref="IsHeroIcon"/>) move only that hero's own icon
    /// (a rush; Slowed's TimelineSpeedMultiplier), never party tempo, which would count them twice (the hero sooner and
    /// every foe slower). Party tempo and Rally stay foe-only (<c>BattleEngine.EnemyTempoMultiplier</c>): a forced Guard
    /// stacks Rally and any hero action resets it, as before. A foe's Pushback lands on a hero's icon like any icon
    /// (<c>Pushback</c> has no hero skip): content's enemy users are the Spire Shover's Shove and an Echo of a hero's
    /// pushback skill; every enemy Hasten (Enrage, Chrono Rewind, Open Reservoir: Self; Charge Howl, Feed the Herd:
    /// AllyOnly) lands on foes. Mana: pincer charge and spells are per action, regen follows the clock, Guard's bank is
    /// unchanged, and the time bank keeps the per-action cap (<see cref="MaxTimeBankOrbs"/> =
    /// <c>AnyHeroMaxTimeBankOrbs</c>), one hero turn being one action. The support extra turn (<c>supportTurnOwner</c>,
    /// GG-T307) stays enemy-side and unchanged: a hero's Turns buff gained on its own turn already lasts until its own
    /// next arrival, so no hero needs one.</para>
    /// </summary>
    public static class TurnRules
    {
        /// <summary>Throws for a mode that isn't built yet (§2.13.3 lands with its own feature).</summary>
        private static void Require(BattleState s)
        {
            if (s.TurnMode != TurnMode.PartyWindow && s.TurnMode != TurnMode.AnyHeroEveryTurn && s.TurnMode != TurnMode.UnmovedThisRound
                && s.TurnMode != TurnMode.HeroTimeline)
                throw new NotSupportedException($"Turn mode {s.TurnMode} is not built yet (bible §2.13)");
        }

        /// <summary>§2.13.1 (GG-T781): one hero action per party turn — AnyHeroEveryTurn, and UnmovedThisRound (§2.13.2,
        /// GG-T794), which adds the round on top, and HeroTimeline (§2.13.3, GG-T1074), where the one action is the active
        /// hero's own turn.</summary>
        private static bool OneActionPerTurn(BattleState s) =>
            s.TurnMode == TurnMode.AnyHeroEveryTurn || s.TurnMode == TurnMode.UnmovedThisRound || s.TurnMode == TurnMode.HeroTimeline;

        /// <summary>GG-T1074 (§2.13.3): under HeroTimeline, is it some other hero's turn — <paramref name="u"/> is a
        /// commanded hero but not <see cref="BattleState.ActiveHeroId"/>? Always false in the other modes. The runner
        /// refuses such a command with "Not this hero's turn".</summary>
        public static bool NotThisHerosTurn(BattleState s, Unit u) => IsHeroIcon(s, u) && u.Id != s.ActiveHeroId;

        /// <summary>§2.13.2 (GG-T794): does this battle keep rounds (each able hero acts once per round)?</summary>
        private static bool KeepsRounds(BattleState s) => s.TurnMode == TurnMode.UnmovedThisRound;

        /// <summary>§2.13.2: can this unit take a place in the round — alive, commanded, on the board and not Immobile
        /// (Frozen, Asleep)? A unit that can't is skipped; the round never waits for it.</summary>
        private static bool AbleThisRound(Unit u) => u.Alive && u.IsCommandable && u.OnBoard && !u.IsImmobile;

        /// <summary>§2.13.2 (GG-T794): someone acted this round and every able hero has, so the round is over. Always false
        /// outside UnmovedThisRound.</summary>
        public static bool RoundComplete(BattleState s)
        {
            Require(s);
            if (!KeepsRounds(s)) return false;
            bool any = false;
            foreach (var u in s.Units)
            {
                if (u.ActedThisRound) any = true;
                else if (AbleThisRound(u)) return false;
            }
            return any;
        }

        /// <summary>§2.13.1: has any unit taken the party's one action this turn? The mark is the same per-unit
        /// <see cref="Unit.ActedThisWindow"/> PartyWindow uses (cleared at the turn boundary by <see cref="ResetTurn"/>),
        /// so no new state is kept or cloned and a replay reproduces it.</summary>
        private static bool PartyHasActed(BattleState s)
        {
            foreach (var u in s.Units) if (u.ActedThisWindow) return true;
            return false;
        }

        // ── Hero icons on the timeline (GG-T807, §2.13.3) ────────────────────────────────

        /// <summary>GG-T807 (§2.13.3): does each commanded hero ride the shared timeline on its own turn icon? Only
        /// <see cref="TurnMode.HeroTimeline"/>. It answers before that mode is playable (no <c>Require</c>): the icons
        /// are scheduled and race the foes' now, and handing a hero's arrival to the player lands with GG-T808.</summary>
        public static bool HeroesOnTimeline(BattleState s) => s.TurnMode == TurnMode.HeroTimeline;

        /// <summary>GG-T807: is this a commanded hero's own turn icon (never a tamed, domesticated or wanderer ally's,
        /// which act on their own AI in every mode)? False outside <see cref="TurnMode.HeroTimeline"/>.</summary>
        public static bool IsHeroIcon(BattleState s, Unit owner) => HeroesOnTimeline(s) && owner != null && owner.IsCommandable;

        // ── Records (GG-T945, §2.13.1, §22.10) ─────────────────────────────────────────────────────

        /// <summary>The mode a finished battle's records are kept under. Wave counts and hauls from different modes aren't
        /// comparable (one action per turn clears far fewer waves than a whole party window), so the Crucible keeps one
        /// record per mode (<c>CrucibleSave</c>) and asks this, never <see cref="BattleState.TurnMode"/>. Any mode answers,
        /// built or not: it only names the battle's own mode.</summary>
        public static TurnMode RecordMode(BattleState s) => s.TurnMode;

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
        /// window (GG-T308, one action per hero per window). AnyHeroEveryTurn: the party's one action is taken, by this
        /// hero or any other, so every hero reads as acted (greyed, refused "Already acted") until the next turn.</summary>
        public static bool HasActed(BattleState s, Unit hero)
        {
            Require(s);
            if (NotThisHerosTurn(s, hero)) return true; // GG-T1074: only the active hero has a turn
            if (!OneActionPerTurn(s)) return hero.ActedThisWindow;
            return PartyHasActed(s) || (KeepsRounds(s) && hero.ActedThisRound); // GG-T794: once per round
        }

        /// <summary>May this hero be picked up or use its bar now, time aside? PartyWindow: alive, commanded, not
        /// Immobile and not yet acted this window. AnyHeroEveryTurn: alive, commanded, not Immobile, and nobody has
        /// acted yet this turn — the same hero as last turn is fine. (The phase and the gap are the caller's, as before.)</summary>
        public static bool MayAct(BattleState s, Unit hero)
        {
            Require(s);
            return hero.Alive && hero.IsCommandable && !hero.IsImmobile && !HasActed(s, hero);
        }

        /// <summary>Heroes on the board who may still act in the party's turn. Empty = the turn is spent. PartyWindow:
        /// living, able heroes (not Immobile) who have not acted yet in this window. AnyHeroEveryTurn: every living, able
        /// hero until one of them acts, then nobody.</summary>
        public static List<Unit> HeroesLeftToAct(BattleState s)
        {
            Require(s);
            var list = new List<Unit>();
            if (OneActionPerTurn(s) && PartyHasActed(s)) return list;
            foreach (var u in s.Units)
                if (AbleThisRound(u) && !u.ActedThisWindow && !(KeepsRounds(s) && u.ActedThisRound) && !NotThisHerosTurn(s, u)) list.Add(u);
            return list;
        }

        /// <summary>GG-T785 (bot planning): heroes other than <paramref name="actorId"/> who may still act in the party's
        /// current turn once that hero has acted. PartyWindow: the rest of <see cref="HeroesLeftToAct"/>.
        /// AnyHeroEveryTurn: none — the actor's action is the whole turn, so a bot values a turn as one action.</summary>
        public static int HeroesLeftAfter(BattleState s, int actorId)
        {
            Require(s);
            if (OneActionPerTurn(s)) return 0;
            int n = 0;
            foreach (var h in HeroesLeftToAct(s)) if (h.Id != actorId) n++;
            return n;
        }

        /// <summary>GG-T785 (bot planning): can <paramref name="ally"/> still act before the foes move, after another hero's
        /// action this turn? PartyWindow: it has not acted yet this window (an ally still to act can close a set-up).
        /// AnyHeroEveryTurn: never — a set-up spans turns; the ally answers it next turn at the earliest.</summary>
        public static bool ActsLaterThisTurn(BattleState s, Unit ally)
        {
            Require(s);
            return !OneActionPerTurn(s) && !HasActed(s, ally);
        }

        /// <summary>GG-T787 (the battle view): is the party's turn one action by any one hero? PartyWindow: no — each
        /// hero wears a ✓ once it has acted and the banner names who is still to act. AnyHeroEveryTurn: yes — no ✓ marks
        /// (the turn is one action, not one per hero), the banner reads "Move any hero" with the seconds left, the gap
        /// lane and Guard call speak of that one action, and the selection stays on the hero who acted last.</summary>
        public static bool OneHeroPerTurn(BattleState s)
        {
            Require(s);
            return OneActionPerTurn(s);
        }

        /// <summary>GG-T800 (the battle view, §2.13.2): has <paramref name="hero"/> used its place in the current round?
        /// Only UnmovedThisRound keeps rounds, so always false elsewhere. The view dims such a hero with the acted check
        /// until the round resets and refuses its pickup with that reason.</summary>
        public static bool MovedThisRound(BattleState s, Unit hero)
        {
            Require(s);
            return KeepsRounds(s) && hero.ActedThisRound;
        }
        /// <summary>GG-T1091 (the battle view, §2.13.2): the round readout by the timeline, "Round 3 · 2 of 4 moved".
        /// Round = <see cref="BattleState.Round"/>; Moved = living commanded heroes that used their place this round;
        /// Of = Moved plus the heroes still able to take one (a fallen, Frozen or Asleep hero that hasn't moved isn't
        /// counted, since the round never waits for it). Null when the battle doesn't keep rounds, so the view shows nothing.</summary>
        public static (int Round, int Moved, int Of)? RoundProgress(BattleState s)
        {
            Require(s);
            if (!KeepsRounds(s)) return null;
            int moved = 0, left = 0;
            foreach (var u in s.Units)
            {
                if (u.ActedThisRound) { if (u.Alive && u.IsCommandable) moved++; }
                else if (AbleThisRound(u)) left++;
            }
            return (s.Round, moved, moved + left);
        }

        /// <summary>GG-T1095 (§2.13.2): does this battle keep rounds? Only UnmovedThisRound. Public for the tutorial's
        /// round-aware lesson copy and the post-battle round count; answers in any mode (it only reads the mode).</summary>
        public static bool HasRounds(BattleState s) => s != null && KeepsRounds(s);

        /// <summary>GG-T1095 (§2.13.2, the post-battle Battle length row): rounds the party played — every finished round
        /// (<see cref="BattleState.Round"/> - 1) plus the current one if any unit has used its place in it. 0 outside
        /// UnmovedThisRound, so the row shows no round count there.</summary>
        public static int RoundsPlayed(BattleState s)
        {
            if (!HasRounds(s)) return 0;
            int n = s.Round - 1;
            foreach (var u in s.Units) if (u.ActedThisRound) { n++; break; }
            return n;
        }

        // ── 2. Whether an action ends the party's turn ─────────────────────────────────────────────

        /// <summary>Records that <paramref name="hero"/> has taken its action (PartyWindow: acted this window;
        /// AnyHeroEveryTurn: it took the party's one action this turn).</summary>
        public static void MarkActed(BattleState s, Unit hero)
        {
            Require(s);
            hero.ActedThisWindow = true;
            if (KeepsRounds(s)) hero.ActedThisRound = true; // GG-T794: its place in the round is used
        }

        /// <summary>After <paramref name="cmd"/> resolved (and its hero was marked), is the party's turn spent — the clock
        /// runs on to the next enemy turn? PartyWindow: Guard gives the window up, or every able hero has acted.
        /// AnyHeroEveryTurn: always — Guard or any one hero's action is the whole turn. The seconds a hero's action leaves
        /// in the gap are <b>lost</b>, not banked (§2.13.1): the same as the last hero's leftover in PartyWindow, so only
        /// Guard turns unused time into mana charge (<see cref="GuardCharge"/>) and stays a real choice.</summary>
        public static bool EndsPartyTurn(BattleState s, BattleCommand cmd)
        {
            Require(s);
            return OneActionPerTurn(s) || cmd is ShieldCommand || HeroesLeftToAct(s).Count == 0;
        }

        // ── 3. What an action spends ───────────────────────────────────────────────────────────────

        /// <summary>
        /// What is left of the party's turn in clock ticks. PartyWindow: until the next enemy turn icon reaches the left
        /// edge (no pushback/hasten assumed); each hero action spends some of it and a Pushback or Slow lengthens it
        /// (GG-T308). -1 when no enemy icon can arrive (none left, or every one halted) — the hold then has no limit.
        /// HeroTimeline (GG-T1074, §2.13.3): until the next turn icon of either side other than the active hero's own (a
        /// foe's or another hero's), so the forced drop and the minimum-action gap (§2.0) read the same way.
        /// </summary>
        public static int TurnTicks(BattleState s)
        {
            Require(s);
            int best = -1;
            foreach (var e in s.Timeline)
            {
                if (e.Kind != TimelineKind.Turn) continue;
                var o = s.UnitById(e.OwnerId);
                if (o == null) continue;
                if (o.IsHero && !(IsHeroIcon(s, o) && o.Id != s.ActiveHeroId)) continue;
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

        /// <summary>Who Guard's Protection covers (§2.5). Every living hero, except under HeroTimeline (GG-T1075, §2.13.3),
        /// where Guard is the active hero's own turn: only that hero, banking the rest of its window
        /// (<see cref="GuardCharge"/>: time to the next other icon minus the time held).</summary>
        public static List<Unit> GuardProtects(BattleState s)
        {
            Require(s);
            if (!HeroesOnTimeline(s)) return s.LivingList(GridGame.Core.Content.Team.Hero);
            var list = new List<Unit>();
            if (s.UnitById(s.ActiveHeroId) is Unit active && active.Alive) list.Add(active);
            return list;
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

        /// <summary>
        /// Time bank (§3.1.8): the most whole orbs a drop's drag-timer remainder mints at the enemy handoff. PartyWindow:
        /// <see cref="BattleConfig.MaxTimeBankOrbs"/> (3), at most once per window, so shared by every hero's action in it.
        /// AnyHeroEveryTurn (GG-T782): every drop is the turn's only action and an enemy turn always follows it, so the
        /// same cap would pay 3 orbs on every action. It mints at most <see cref="BattleConfig.AnyHeroMaxTimeBankOrbs"/>
        /// (1), about PartyWindow's per-action share. Pincer, Guard and regen charge stay as they are: pincer charge and
        /// spells are both per action, Guard's cap already bounds the one turn it gives up, and regen follows the clock.
        /// UnmovedThisRound (GG-T795) takes the same per-action cap: one action per party turn there too.
        /// </summary>
        public static int MaxTimeBankOrbs(BattleState s)
        {
            Require(s);
            return OneActionPerTurn(s) ? BattleConfig.AnyHeroMaxTimeBankOrbs : BattleConfig.MaxTimeBankOrbs;
        }

        // ── 5. What a turn boundary resets ─────────────────────────────────────────────────────────

        /// <summary>Did this command cross a party-turn boundary (<see cref="PlayerControlEvent.NewWindow"/>)? PartyWindow:
        /// the window ran out (an enemy turn came) or a wave was cleared, which opens a fresh window.</summary>
        public static bool IsTurnBoundary(BattleState s, bool windowOver, bool waveChanged)
        {
            Require(s);
            if (HeroesOnTimeline(s)) return true; // GG-T1074: every hero turn is a boundary (the clock ran on to it)
            return windowOver || waveChanged;
        }

        /// <summary>A turn boundary: what it resets. PartyWindow: every unit may act again in the new window.</summary>
        /// <remarks>UnmovedThisRound (GG-T794): also, when every able hero has acted (<see cref="RoundComplete"/>), or a new
        /// wave began after someone acted this round, a new round starts: <see cref="BattleState.Round"/> + 1 and every
        /// <see cref="Unit.ActedThisRound"/> cleared. Returns true when it started one (the caller emits
        /// <see cref="RoundStartedEvent"/>); never in the other modes.</remarks>
        public static bool ResetTurn(BattleState s, bool waveChanged = false)
        {
            Require(s);
            foreach (var u in s.Units) u.ActedThisWindow = false;
            if (!KeepsRounds(s)) return false;
            bool anyActed = false;
            foreach (var u in s.Units) if (u.ActedThisRound) { anyActed = true; break; }
            if (!(waveChanged ? anyActed : RoundComplete(s))) return false;
            s.Round++;
            foreach (var u in s.Units) u.ActedThisRound = false;
            return true;
        }

        /// <summary>A unit joining the hero side mid-battle (tamed, a wanderer) or leaving it (a tame wears off) starts
        /// with no action spent. PartyWindow: its acted-this-window mark is cleared.</summary>
        public static void ClearActed(BattleState s, Unit u)
        {
            Require(s);
            u.ActedThisWindow = false;
            u.ActedThisRound = false; // GG-T794: a joiner may act this round, and the round waits for it
        }
    }
}
