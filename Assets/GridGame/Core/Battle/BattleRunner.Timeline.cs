using System;
using System.Collections.Generic;
using GridGame.Core.Content;

namespace GridGame.Core.Battle
{
    internal sealed partial class BattleRunner
    {
        private int ticksSinceReport;

        /// <summary>GG-T308: an enemy turn resolved (or the window was spent) during this command — the party's window is over.</summary>
        private bool windowOver;

        /// <summary>Runs the shared clock forward <paramref name="ticks"/> ticks, resolving every arrival in order;
        /// <paramref name="stopAtEnemyTurn"/> ends the advance at the first enemy turn, hero turn (GG-T1083, HeroTimeline) or
        /// new wave — the window is over. Returns whether a hero's own turn icon arrived (only HeroTimeline gives one).</summary>
        private bool AdvanceClock(int ticks, bool stopAtEnemyTurn = false)
        {
            int wave = S.Wave;
            bool heroReached = false;
            for (int i = 0; i < ticks && !S.IsOver; i++)
            {
                Tick(out bool turnResolved, out bool heroTurn, stopAtHero: stopAtEnemyTurn);
                if (turnResolved) windowOver = true;
                if (heroTurn) heroReached = true;
                if (stopAtEnemyTurn && (turnResolved || heroTurn || S.Wave != wave)) break;
            }
            ReportAdvance();
            return heroReached;
        }

        /// <summary>Run the clock until an enemy turn has resolved, a hero's own turn came (GG-T1083), or nothing can ever arrive.</summary>
        private void AdvanceUntilEnemyTurn()
        {
            int wave = S.Wave;
            for (int i = 0; i < BattleConfig.MaxAdvanceTicks && !S.IsOver; i++)
            {
                Tick(out bool turnResolved, out bool heroTurn, stopAtHero: true);
                if (turnResolved) { windowOver = true; break; }
                if (heroTurn || S.Wave != wave) break;
            }
            ReportAdvance();
        }

        /// <summary>
        /// GG-T1083 (§2.13.3): HeroTimeline hands the clock to the next hero. Clears <see cref="BattleState.ActiveHeroId"/>,
        /// first resolves arrivals still pending at the current tick (a foe that tied a hero's icon and was held behind it),
        /// then ticks until a commanded hero's icon arrives and makes it the active hero. Enemy turns, casts, rule events
        /// and wave changes resolve on the way.
        /// </summary>
        private void AdvanceUntilHeroTurn()
        {
            S.ActiveHeroId = -1;
            if (S.IsOver) return;
            bool enemyTurn = false, heroTurn = false;
            ResolveArrivals(true, ref enemyTurn, ref heroTurn);
            if (enemyTurn) windowOver = true;
            for (int i = 0; !heroTurn && i < BattleConfig.MaxAdvanceTicks && !S.IsOver; i++)
            {
                Tick(out enemyTurn, out heroTurn, stopAtHero: true);
                if (enemyTurn) windowOver = true;
            }
            ReportAdvance();
        }

        /// <summary>
        /// GG-T308 (§2.0): the party's window is spent (every hero acted, or Guard gave it up) — the clock runs on to the
        /// next enemy turn; with no enemy icon able to arrive at all, the window is the fixed ActionTicks instead.
        /// </summary>
        private void SpendWindow()
        {
            if (BattleEngine.TurnTicks(S) > 0) AdvanceUntilEnemyTurn();
            else { AdvanceClock(BattleConfig.ActionTicks, stopAtEnemyTurn: true); windowOver = true; }
        }

        private void ReportAdvance()
        {
            if (ticksSinceReport <= 0) return;
            var e = new TimelineAdvancedEvent { Ticks = ticksSinceReport };
            foreach (var t in S.Timeline)
            {
                e.U[t.Id] = BattleEngine.BarU(S, t);
                if (BattleEngine.InRecovery(t)) e.Recover[t.Id] = (t.RecoverTicks, Math.Max(t.RecoverTicks, t.RecoverTotalTicks)); // GG-T319
            }
            Emit(e);
            ticksSinceReport = 0;
        }

        /// <summary>One clock tick. <paramref name="heroTurn"/>: a commanded hero's own icon arrived (HeroTimeline);
        /// <paramref name="stopAtHero"/> leaves the tick's later arrivals pending right after it (GG-T1083).</summary>
        private void Tick(out bool enemyTurnResolved, out bool heroTurn, bool stopAtHero = false)
        {
            enemyTurnResolved = false;
            heroTurn = false;
            S.Clock++;
            ticksSinceReport++;
            // GG-T673: the orbs refill slowly with time on the battle clock (one charge step every few clock seconds).
            if (BattleConfig.RegenCharge > 0 && S.Clock % (BattleConfig.RegenIntervalSeconds * BattleConfig.TicksPerSecond) == 0)
                AddCharge(BattleConfig.RegenCharge, -1, "regen");

            if (S.PendingSpawns.Count > 0) { BringReinforcementsForward(); ResolvePendingSpawns(); CheckTilesStrict("reinforcements"); }
            if (S.IsOver) return;

            foreach (var e in S.Timeline)
            {
                if (e.Kind == TimelineKind.Hazard) { if (e.RemainingTicks > 0) e.RemainingTicks--; continue; } // GG-T359: no owner, never halted
                var owner = S.UnitById(e.OwnerId);
                if (owner == null || !owner.Alive) continue;
                if (e.Kind == TimelineKind.Turn)
                {
                    if (owner.IsTimelineHalted) continue;
                    if (e.RecoverTicks > 0) { e.RecoverTicks--; continue; } // off the bar, recovering (GG-T318)
                    if (e.StunTicks > 0) { e.StunTicks--; continue; }
                    if (e.Ip < BattleConfig.BarLength) e.Ip = Math.Min(BattleConfig.BarLength, e.Ip + BattleEngine.TurnRatePerTick(S, owner)); // one rate for everyone (GG-T318)
                }
                else
                {
                    if (owner.IsTimelineHalted) continue;
                    if (e.RemainingTicks > 0) e.RemainingTicks--;
                }
            }

            TickTimedBuffs(1);
            if (S.IsOver) return;
            FollowCarriers(); // GG-T874: a carried bomb keeps up with however its carrier moved since
            if (S.Region.Tiles.Count > 0) TickTileSight(); // GG-T843: fresh marks with VisibleSeconds hide from the other side
            if (S.Region.Tiles.Count > 0) TickRegionTiles(); // GG-T359: painted tiles with a lifetime fade
            if (S.IsOver) return;

            ResolveArrivals(stopAtHero, ref enemyTurnResolved, ref heroTurn);
        }

        /// <summary>Resolves every arrival due at the current tick (GG-T1083: split out of <see cref="Tick"/> so
        /// <see cref="AdvanceUntilHeroTurn"/> can finish a tick a hero turn stopped). <paramref name="stopAtHero"/> stops
        /// right after a hero's own icon arrived.</summary>
        private void ResolveArrivals(bool stopAtHero, ref bool enemyTurnResolved, ref bool heroTurn)
        {
            // Arrivals this tick, resolved in arrival order: casts that hit 0, then rule hazards, then full turn icons; ties by id.
            for (int guard = 0; guard < 64 && !S.IsOver; guard++)
            {
                TimelineEntry next = null;
                foreach (var e in S.Timeline)
                {
                    bool arrived = e.Kind != TimelineKind.Turn ? e.RemainingTicks <= 0 : e.Ip >= BattleConfig.BarLength;
                    if (!arrived) continue;
                    // A halted (Frozen) owner holds its full icon until it thaws, same as the advance loop above skips it —
                    // a haste to a full bar or a freeze landing on the arrival tick must not let it act while frozen.
                    if (e.Kind != TimelineKind.Hazard) { var eo = S.UnitById(e.OwnerId); if (eo != null && eo.Alive && eo.IsTimelineHalted) continue; }
                    if (next == null || Earlier(e, next)) next = e;
                }
                if (next == null) break;
                ReportAdvance();
                BeginClutchAction(); // GG-T571: each arrival (a cast resolving, a turn, a rule event) is its own action
                if (next.Kind == TimelineKind.Cast) { ResolveCast(next); CheckTilesStrict("cast " + next.AbilityId); }
                else if (next.Kind == TimelineKind.Hazard) { ResolveHazard(next); CheckTilesStrict("rule event " + next.RuleEvent); }
                else if (S.UnitById(next.OwnerId) is Unit sleeper && sleeper.Alive && TurnRules.IsHeroIcon(S, sleeper) && sleeper.IsImmobile)
                    SkipHeroTurn(next, sleeper); // GG-T1100: Asleep/Immobile — the icon re-enters, nobody becomes active
                else if (S.UnitById(next.OwnerId) is Unit hero && hero.Alive && TurnRules.IsHeroIcon(S, hero))
                {
                    HeroIconArrived(next, hero);
                    heroTurn = true;
                    if (stopAtHero) break;
                }
                else
                {
                    next.Ip = 0;
                    var owner = S.UnitById(next.OwnerId);
                    recoverActorId = owner?.Id ?? -1;
                    turnRecoverSeconds = 0f;
                    // GG-T359: region rules' TurnStart hooks run as the icon arrives, before the unit acts.
                    if (owner != null && owner.Alive) RegionTurn(RuleHook.TurnStart, owner);
                    // A tamed or domesticated monster (GG-T303) acts on its own icon but doesn't end the party's window.
                    if (owner != null && owner.Alive && owner.IsIndependent) { RunAllyTurn(owner); CheckTilesStrict("ally turn of " + owner); }
                    else if (owner != null && owner.Alive) { RunEnemyTurn(owner); RuleBreakAfterTurn(owner); enemyTurnResolved = true; CheckTilesStrict("turn of " + owner); }
                    if (owner != null && owner.Alive && !S.IsOver) RegionTurn(RuleHook.TurnEnd, owner);
                    // GG-T809 (§2.13.3): a Frozen hero's icon never arrives, so its halt counts down on each foe turn too.
                    if (owner != null && !owner.IsIndependent && TurnRules.HeroesOnTimeline(S) && !S.IsOver)
                        foreach (var fh in S.LivingList(Team.Hero))
                            if (TurnRules.IsHeroIcon(S, fh) && fh.IsTimelineHalted) TickTurnBuffs(fh, true);
                    recoverActorId = -1;
                    // GG-T318: the turn is done, so its recovery cost is known. Off the bar, then back on at its Speed's entry.
                    if (owner != null && owner.Alive && S.Timeline.Contains(next))
                    {
                        // Pushback/haste landed on this icon during its own turn (a reactive OnAttackedDirect/beast_sense push,
                        // a haste): Ip was 0 at arrival, so stun is the push and any Ip is the haste. ScheduleReentry resets
                        // both, so carry them across or the PushedBack/Hastened event shown would be silently undone.
                        int carriedStun = Math.Max(0, next.StunTicks);
                        int carriedIp = Math.Max(0, next.Ip);
                        ScheduleReentry(next, owner, BattleConfig.RecoverBaseSeconds + turnRecoverSeconds, space: !owner.IsIndependent);
                        if (carriedStun > 0) next.StunTicks = carriedStun;
                        if (carriedIp > 0) RushTurn(next, owner, carriedIp / Math.Max(1, BattleEngine.TurnRatePerTick(S, owner)));
                    }
                }
            }
        }

        /// <summary>GG-T318: the unit whose turn is resolving, and the largest RecoverSeconds keyword it used this turn.</summary>
        private int recoverActorId = -1;
        private float turnRecoverSeconds;

        /// <summary>GG-T318: a RecoverSeconds keyword used by the unit whose turn is resolving adds to that turn's recovery.</summary>
        private void NoteRecover(Unit caster, AbilityDef a)
        {
            if (caster != null && a != null && caster.Id == recoverActorId && a.RecoverSeconds > turnRecoverSeconds)
                turnRecoverSeconds = a.RecoverSeconds;
        }

        private static int SecondsAsTicks(float seconds) => (int)Math.Round(Math.Max(0f, seconds) * BattleConfig.TicksPerSecond);

        /// <summary>
        /// GG-T318 (§2.12): puts a turn icon back on its cycle. It sits off the bar for <paramref name="recoverSeconds"/>
        /// (± the difficulty row's jitter, never acting sooner than <paramref name="floorTicks"/> from now), then re-enters
        /// at its Speed's entry (<see cref="BattleEngine.EntryIp"/>). With <paramref name="space"/>, the spacer
        /// (<see cref="TimelineSpacing"/>) may hold it longer so its turn lands clear of every other foe's. Entries in
        /// <paramref name="unscheduled"/> are not counted as others yet.
        /// </summary>
        private void ScheduleReentry(TimelineEntry e, Unit u, float recoverSeconds, bool space, int floorTicks = 0, ISet<TimelineEntry> unscheduled = null)
        {
            e.Ip = BattleEngine.EntryIp(u);
            e.StunTicks = 0;
            e.RecoverTicks = 0;
            int travel = Math.Max(0, BattleEngine.TicksUntilAct(S, e, ignoreHalt: true));
            var row = BattleEngine.SpacingRow(S);
            int recover = SecondsAsTicks(recoverSeconds);
            int jitter = SecondsAsTicks(row.JitterSeconds);
            if (jitter > 0) recover += S.Rng.Range(-jitter, jitter + 1);
            recover = Math.Max(0, Math.Max(recover, floorTicks - travel));
            if (space && row.MinGapSeconds > 0f)
            {
                var others = new List<int>();
                foreach (var o in S.Timeline)
                {
                    if (o == e || (unscheduled != null && unscheduled.Contains(o))) continue;
                    var ou = S.UnitById(o.OwnerId);
                    if (ou == null || !ou.Alive || ou.Team != Team.Enemy || ou.IsIndependent) continue;
                    if (o.Kind == TimelineKind.Cast && !o.IsCharge) continue;
                    others.Add(BattleEngine.TicksUntilAct(S, o));
                }
                recover += TimelineSpacing.Delay(recover + travel, others, SecondsAsTicks(row.MinGapSeconds), SecondsAsTicks(row.MaxSpreadSeconds));
            }
            e.RecoverTicks = recover;
            e.RecoverTotalTicks = recover;
        }

        /// <summary>
        /// GG-T807 (§2.13.3): a commanded hero's own icon reached the left edge. Until GG-T808 hands that hero to the player,
        /// the icon is reset and put back on its cycle: off the bar for <see cref="BattleConfig.RecoverBaseSeconds"/>, then
        /// re-entering at its Speed's entry (<see cref="BattleEngine.EntrySeconds"/>, judged against
        /// <see cref="GridGame.Core.Rules.Formulas.ReferenceHeroSpeed"/>). It never ends the party's window and runs no TurnStart/TurnEnd hooks
        /// (those stay at party-turn boundaries, §2.13.1). Hero icons are not spaced: <see cref="TimelineSpacing"/> keeps
        /// foes' turns apart for the player's sake, and a hero's own turn is the player's, so it races the foes unspaced.
        /// </summary>
        private void HeroIconArrived(TimelineEntry e, Unit hero)
        {
            e.Ip = 0;
            S.ActiveHeroId = hero.Id; // GG-T1074: this hero alone may act now (TurnRules.NotThisHerosTurn)
            ScheduleReentry(e, hero, BattleConfig.RecoverBaseSeconds, space: false);
        }

        /// <summary>
        /// GG-T1100 (§2.13.3): an Immobile (Asleep), non-halted hero's own icon arrived under HeroTimeline. Like a foe's
        /// skipped turn (RunEnemyTurn's Immobile branch) the turn is lost but still counts: its Turns buffs (Sleep itself
        /// included) and skill cooldowns tick, then the icon re-enters at its Speed's entry. It never becomes
        /// <see cref="BattleState.ActiveHeroId"/> and the clock runs on to the next hero's own icon.
        /// </summary>
        private void SkipHeroTurn(TimelineEntry e, Unit hero)
        {
            e.Ip = 0;
            Emit(new TurnSkippedEvent { UnitId = hero.Id, Reason = hero.HasBuff("sleep") ? "asleep" : "immobile" });
            TickTurnBuffs(hero, false);
            if (hero.Alive)
            {
                var keys = new List<string>(hero.Cooldowns.Keys);
                foreach (var k in keys)
                    if (hero.Cooldowns[k] > 0) hero.Cooldowns[k]--;
            }
            CheckEnd();
            if (hero.Alive && S.Timeline.Contains(e)) ScheduleReentry(e, hero, BattleConfig.RecoverBaseSeconds, space: false);
        }

        /// <summary>
        /// GG-T807 (§2.13.3): in <see cref="TurnMode.HeroTimeline"/> every commanded hero on the board without a turn icon
        /// (the wave's opening, a guest who just joined, a hero revived since) gets one, entering at its Speed's entry.
        /// Returns the latest tick any hero icon arrives at (-1 with none), so the wave's foes can open behind it.
        /// </summary>
        /// <remarks>GG-T1075: <paramref name="onlyMissing"/> (a revive mid-wave) gives icons only to heroes without one
        /// and leaves every other icon where it is.</remarks>
        private int ScheduleHeroIcons(bool onlyMissing = false)
        {
            if (!TurnRules.HeroesOnTimeline(S)) return -1;
            int last = -1;
            foreach (var u in S.Units)
            {
                if (!u.Alive || !u.OnBoard || !TurnRules.IsHeroIcon(S, u)) continue;
                var e = S.TurnEntryOf(u.Id);
                if (onlyMissing && e != null) continue;
                if (e == null) { e = new TimelineEntry { Id = S.NextEntryId++, Kind = TimelineKind.Turn, OwnerId = u.Id }; S.Timeline.Add(e); }
                ScheduleReentry(e, u, 0f, space: false);
                last = Math.Max(last, BattleEngine.TicksUntilAct(S, e, ignoreHalt: true));
            }
            return last;
        }

        private bool Earlier(TimelineEntry a, TimelineEntry b)
        {
            if (a.Kind != b.Kind) return ArrivalRank(a.Kind) < ArrivalRank(b.Kind);
            // GG-T807 (§2.4, §2.13.3): on the same tick a hero's turn beats a foe's (the party opens and wins ties, as
            // in every mode); otherwise the earlier-made icon. Only HeroTimeline gives heroes turn icons, so other modes
            // are unchanged.
            if (a.Kind == TimelineKind.Turn)
            {
                bool ha = TurnRules.IsHeroIcon(S, S.UnitById(a.OwnerId)), hb = TurnRules.IsHeroIcon(S, S.UnitById(b.OwnerId));
                if (ha != hb) return ha;
            }
            return a.Id < b.Id;
        }

        /// <summary>Tie order on one tick: casts, then rule hazards (GG-T359), then turns (a hero's before a foe's, GG-T807).</summary>
        internal static int ArrivalRank(TimelineKind k) => k == TimelineKind.Cast ? 0 : k == TimelineKind.Hazard ? 1 : 2;

        /// <summary>GG-T807: runs the shared clock <paramref name="ticks"/> ticks forward with no command (the
        /// <see cref="BattleEngine.RunClock"/> hook).</summary>
        internal void RunClock(int ticks) => AdvanceClock(ticks);

        /// <summary>Time-banked orbs (§3.1.8): drag-timer seconds left at the last drop convert to orbs at the enemy handoff.</summary>
        private void MintTimeBank()
        {
            if (S.PendingTimeBankSeconds <= 0f) return;
            int n = Math.Min(TurnRules.MaxTimeBankOrbs(S), (int)(S.PendingTimeBankSeconds / BattleConfig.SecondsPerTimeBankOrb)); // §2.13: the mode's cap
            S.PendingTimeBankSeconds = 0f;
            if (n <= 0) return;
            for (int i = 0; i < n; i++) MintOrb(-1, "time bank");
            Announce($"Banked {n} mana");
        }
    }
}
