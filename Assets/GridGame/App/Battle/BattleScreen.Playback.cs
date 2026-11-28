using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Meta;
using MindAttic.Media.Capture;
using MindAttic.Media.Capture.Unity;
using MindAttic.Telemetry.Unity;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    public sealed partial class BattleScreen
    {
        private int chainCount;

        /// <summary>Pincers played so far in the current event list (automation: "mid-chain").</summary>
        public int ChainCount => playing ? chainCount : 0;

        private float playStarted;

        /// <summary>
        /// Plays an event list back, one awaited beat each (bible: no instant anythings). The simulation
        /// already resolved everything; if any animation is skipped or interrupted, the final SyncAll
        /// snaps every view to the true state.
        /// </summary>
        private IEnumerator Play(List<BattleEvent> events, bool skipMovePrefix)
        {
            playing = true;
            playStarted = battleTime;
            chainCount = 0;
            // A cast queued in this action is already in the final state; it joins the bar only when its beat plays.
            unplayedCasts.Clear();
            foreach (var ev in events) if (ev is CastQueuedEvent q) unplayedCasts.Add(q.EntryId);
            BeginMeterPlayback(events); // GG-T1928: the party bar starts where this action found the pool
            BeginEnemyBeats(events); // GG-T839: traps and hazards drawn as of the beat now playing
            int i = 0;
            // The drag already showed the hero's walk live: the hero follows the finger and ApplyPreview slid every
            // displaced actor to where the whole walk leaves it. So the walk's own moves (the hero's steps, actors it
            // displaced) are not replayed; only their side effects are (traps, pickups, Sleep breaking). Every token
            // the walk touched is snapped to where the walk — not the whole command — leaves it: an enemy that moves
            // again later in this command (a drag can run a full enemy turn via AdvanceClock) starts that move from
            // its true post-walk tile, and no token is ever drawn on a tile another one holds at that moment, which a
            // partial replay (hero mid-walk, displaced actors already at their preview tiles) used to do (GG-T97).
            int walkHero = -1, walkEnd = 0;
            var walkAt = new Dictionary<int, Coord>();
            if (skipMovePrefix)
            {
                var touched = new HashSet<int>();
                for (walkEnd = 0; walkEnd < events.Count && !EndsWalk(events[walkEnd]); walkEnd++)
                {
                    switch (events[walkEnd])
                    {
                        case ActionBeganEvent ab when walkHero < 0: walkHero = ab.ActorId; touched.Add(ab.ActorId); break;
                        case SteppedEvent se when se.UnitId == walkHero: walkAt[se.UnitId] = se.To; break;
                        case DisplacedEvent de when de.ByUnitId == walkHero: touched.Add(de.UnitId); walkAt[de.UnitId] = de.To; break;
                        case StepBlockedEvent sb: touched.Add(sb.UnitId); break;
                    }
                }
                foreach (var id in touched)
                    if (board.Units.TryGetValue(id, out var v) && !v.Dead) board.Place(v, walkAt.TryGetValue(id, out var at) ? at : v.Tile);
            }
            for (; i < events.Count; i++)
            {
                // Within the already-shown walk, its moves are no-ops (their tokens already stand at the walk's end).
                bool shownLive = i < walkEnd && ((events[i] is SteppedEvent ws && ws.UnitId == walkHero) || (events[i] is DisplacedEvent wd && wd.ByUnitId == walkHero) || events[i] is StepBlockedEvent);
                if (shownLive) continue;
                // GG-T126: no beat starts while the pause menu is up (a beat's side effects — banners, tallies,
                // outlines — run the moment Animate is called, before its first yield).
                while (Tween.Paused || awayOnSettings) yield return null; // ...nor while "How to play" covers the battle
                PlaybackBeats++;
                // POL-E5: a pincer's hits land as Major; an ability's at its own tier (AbilityAnim); a new action resets.
                if (events[i] is ActionBeganEvent || events[i] is TurnStartedEvent) hitTier = VfxTier.Minor;
                else if (events[i] is PincerFormedEvent) hitTier = VfxTier.Major;
                // GG-T556: an ability's hits may carry its own attack type; a new action or a weapon strike clears it.
                if (events[i] is ActionBeganEvent || events[i] is TurnStartedEvent || events[i] is PincerFormedEvent || events[i] is AttackEvent) hitAbility = null;
                else if (events[i] is AbilityResolvedEvent rae) hitAbility = rae.AbilityId;
                // GG-T836: a new action's pincers start at normal tempo; a pincer picks its beat tempo before its cut-in.
                if (events[i] is ActionBeganEvent || events[i] is TurnStartedEvent) { ResetPincerBeats(); ResetRampage(); }
                else if (events[i] is PincerFormedEvent pfe) BeginPincerBeats(pfe);
                // GG-T839: a foe's action plays as beats: its intent as its turn starts (TurnAnim), the wind-up before the act.
                IEnumerator windUp = null;
                try { windUp = EnemyBeatBefore(events, i); }
                catch (System.Exception ex) { Debug.LogException(ex); }
                if (windUp != null) yield return Safe(windUp);
                IEnumerator anim = null;
                try { anim = Animate(events[i]); }
                catch (System.Exception e) { Debug.LogException(e); }
                if (anim != null) yield return Safe(anim);
                SyncDarkness();
                // GG-T97: once a beat ends no two tokens may be drawn on one tile. The one legitimate in-between is a
                // swap still finishing — the stepper has arrived and its partner's DisplacedEvent (or the amplifier's
                // shove) is the very next beat — so the check waits for that beat.
                bool swapPending = i + 1 < events.Count && (events[i + 1] is DisplacedEvent || events[i + 1] is AmplifierMovedEvent || events[i + 1] is PickupMovedEvent);
                // GG-T228: a unit token may sit on a pickup token only while the step that shoves the pickup is still
                // playing out (a snake's segments follow before its head's pickup goes behind the tail, a 2×2's lanes
                // shove in turn), so the unit-on-pickup check waits until no move beat is next.
                bool movePending = swapPending || (i + 1 < events.Count && (events[i + 1] is SteppedEvent || events[i + 1] is TeleportedEvent || events[i + 1] is VaultedEvent));
                if (!swapPending) BoardAudit.Beat(AuditTokens(false, unitsOnItems: !movePending), events[i]);
            }
            ResetPincerBeats(); // GG-T836: battle speed is the setting's again
            ResetRampage(); // GG-T898: no raging run outlives its playback
            EndEnemyBeats(); // GG-T839: the board draws the state's own traps and hazards again
            board.Lines.ClearLines();
            PruneGhosts();
            unplayedCasts.Clear();
            actingEntry = -1;
            pendingItemUser = -1; pendingItemId = null;
            // Before the safety-net snap: did playback leave every token where the state says it stands? A miss here
            // means a beat was skipped or played out of order (the snap below would hide it as a jump).
            BoardAudit.Settled(AuditTokens(true));
            RefreshRecoverView(); // GG-T319: the tray snaps to the true state with everything else
            shownPool = -1; // the party bar reads the true pool again
            SyncAll();
            BoardAudit.Synced(AuditTokens(true, rendered: true));
            while (Tween.Paused || awayOnSettings) yield return null;
            playing = false;
            if (lastAt != null) SyncSlot(lastAt); // GG-T1944: the slot reads YOU once playback is over
            if (State.IsOver && !ended) yield return EndBattle();
        }

        /// <summary>The first event after a hero's walk (MoveCommand's WalkPath): pincers, the clock, or the end of the action.</summary>
        private static bool EndsWalk(BattleEvent e) =>
            e is PincerFormedEvent || e is RespiteEvent || e is TimelineAdvancedEvent || e is TurnStartedEvent
            || e is WaveClearedEvent || e is BattleEndedEvent || e is PlayerControlEvent || e is AbilityRefusedEvent;

        private float Fx => ReduceMotion ? 0f : 1f;

        /// <summary>Runs a (possibly nested) animation; an exception ends that beat but never the playback.</summary>
        private static IEnumerator Safe(IEnumerator e)
        {
            while (true)
            {
                // GG-T126: a beat never steps while paused, even across a bare one-frame yield.
                while (Tween.Paused) yield return null;
                object cur;
                try
                {
                    if (!e.MoveNext()) yield break;
                    cur = e.Current;
                }
                catch (System.Exception ex) { Debug.LogException(ex); yield break; }
                if (cur is IEnumerator nested) yield return Safe(nested);
                else yield return cur;
            }
        }

        private IEnumerator Animate(BattleEvent e)
        {
            if (e is ActionBeganEvent began) lastActionLabel = began.Label; // what granted an orb (GG-T405)
            switch (e)
            {
                case ActionBeganEvent a when a.ActorId >= 0 && a.Label != "Move":
                    ShowBanner($"{State.UnitById(a.ActorId)?.Name}: {a.Label}");
                    return Tween.Wait(Timing.Quick);
                case ActionBeganEvent a when a.Label == "Shield":
                    ShowBanner("Shields up!");
                    App.Audio.Sfx("battle.cast");
                    return Tween.Wait(Timing.Standard);

                case SteppedEvent s when s.UnitId == rampageUnit && rampageStepsLeft > 0: return RampageStep(s); // GG-T898: the raging dash
                case RampagedEvent rp: return RampageAnim(rp); // GG-T898: the steam-train whistle
                case SteppedEvent s: return StepAnim(s.UnitId, s.To, 0.16f, "battle.move");
                case DisplacedEvent d: return StepAnim(d.UnitId, d.To, 0.14f, "battle.slide");
                case BurrowedEvent bw: return BurrowAnim(bw); // GG-T1159: a burrowed unit's token goes under with it
                case TeleportedEvent t: return TeleportAnim(t);
                case VaultedEvent vt: return VaultAnim(vt); // GG-T868: the Lancer's Pole Vault arc
                case SubstitutedEvent sb: return SubstitutionAnim(sb); // GG-T862: the White Ninja's log and smoke

                case PincerFormedEvent p: FlushTallies(); return PincerAnim(p);
                case PincerPhaseEvent pph: return PincerPhaseAnim(pph); // GG-T836
                case RespiteEvent rs: return RespiteAnim(rs);
                case AttackEvent at: return AttackAnim(at);
                case SupportActionEvent sa: return SupportAnim(sa);
                case DamagedEvent dm: return DamageAnim(dm);
                case HealedEvent h: return HealAnim(h);
                case DiedEvent d: EndGhost(d.UnitId, null); return DeathAnim(d);
                case LootEvent l: return LootAnim(l);
                case RemainsLeftEvent rl: return RemainsAnim(rl.RemainsId, rl.At, false);
                case GuestJoinedEvent gj: return GuestAnim(gj);
                case RecruitableAppearedEvent rap: return RecruitableAppearedAnim(rap);
                case RecruitOfferedEvent rof: return RecruitOfferedAnim(rof);
                case RecruitJoinedEvent rj: return RecruitJoinedAnim(rj);
                case TamedEvent te: return TamedAnim(te); // GG-T303
                case TameFailedEvent tf: return TameFailedAnim(tf);
                case BondGrewEvent bg: return BondAnim(bg);
                case TameEndedEvent tx: return TameEndedAnim(tx);
                case WandererJoinedEvent wj: return WandererAnim(wj); // GG-T342
                case TrustGrewEvent tg: return TrustAnim(tg);
                case RoseAgainEvent ra: return RiseAnim(ra);
                case AmplifierSpawnedEvent asp: return AmpSpawnAnim(asp);
                case AmplifierMovedEvent am: return AmpMoveAnim(am);
                case PickupMovedEvent pm: return PickupMoveAnim(pm);
                case AmplifierUsedEvent au: return AmpUsedAnim(au);
                case ManaGainedEvent mg: ManaLines(mg); return ManaGainAnim(mg);
                case SpellReadyEvent sr: return SpellReadyAnim(sr);
                case SpellManaSpentEvent sms: App.Audio.Sfx("battle.cast", 0.6f); SpellManaSpent(sms); return null;

                case TimelineAdvancedEvent ta: FlushTallies(); return TimelineAnim(ta);
                case PushedBackEvent pb: return PushAnim(pb);
                case HastenedEvent hs: return HastenAnim(hs);
                case TempoChangedEvent tc: return TempoAnim(tc);
                case TurnStartedEvent ts: FlushTallies(); return TurnAnim(ts);
                case TurnSkippedEvent sk: App.Audio.Sfx(sk.Reason == "asleep" ? "status.sleep" : "ui.cancel", sk.Reason == "asleep" ? 0.8f : 0.5f); return FloatText(sk.UnitId, CombatTextKind.StatusTick, sk.Reason == "asleep" ? "Zzz" : "Skipped", 0.35f);

                case CastQueuedEvent cq: return CastQueuedAnim(cq);
                case CastStaggeredEvent cs: return StaggerAnim(cs);
                case CastCancelledEvent cc: App.Audio.Sfx("battle.fizzle", 0.8f); castGhosts.Remove(cc.EntryId); SyncTimeline(null); board.Lines.ClearLines(); board.ClearMarks(); return FloatText(cc.CasterId, cc.Reason == "interrupted" || cc.Reason == "rooted" ? CombatTextKind.Debuff : CombatTextKind.Expire, cc.Reason == "interrupted" ? "Interrupted!" : cc.Reason == "rooted" ? "Snared!" : "Fizzled", 0.35f); // GG-T881: roots snap a Line charge
                case ClutchEvent cl: return ClutchAnim(cl);
                case AbilityResolvedEvent ar: FlushTallies(); return AbilityAnim(ar);
                case AbilityRefusedEvent rf: App.Audio.Sfx("ui.error"); ShowBanner(rf.Reason); refusal = rf.Reason; SyncPanel(); return null;
                case ItemUsedEvent iu: pendingItemUser = iu.UserId; pendingItemId = iu.ItemId; ShowBanner($"{App.Content.TryItem(iu.ItemId)?.Name} ({iu.ChargesLeft} left)"); return Tween.Wait(Timing.Instant);
                case PlateHitEvent ph: return PlateHitAnim(ph);
                case PlateBrokenEvent pbk: return PlateBrokenAnim(pbk);
                case PlateShatteredEvent pse: return PlateShatteredAnim(pse);
                case FacingChangedEvent fc: { var fu = State.UnitById(fc.UnitId); if (fu != null && board.Units.TryGetValue(fu.Id, out var fv)) { fv.SetPlate(fc.Facing, fu.PlateHp, fu.PlateMax); App.StartCoroutine(Tween.Pop(fv.Plate, 1.2f, 0.18f)); } return null; }
                case PickupDroppedEvent pd: return PickupDropAnim(pd);
                case PickupCollectedEvent pc: return PickupCollectAnim(pc);
                case PickupExpiredEvent pe: return PickupExpiredAnim(pe);

                case BuffAppliedEvent ba when ba.BuffId == "raging": return RageAnim(ba); // GG-T898: steam from the ears, RAGE
                case BuffAppliedEvent ba: return InflictAnim(ba);
                case BuffRemovedEvent br: RefreshUnit(br.UnitId); if (br.Reason == "steam") App.Audio.Sfx("element.steam", 0.8f); if (br.Reason != "steam") StatusGoneText(br); return br.Reason == "steam" ? FloatText(br.UnitId, CombatTextKind.Weak, "Steam!", 0.25f) : null;
                case BuffTickedEvent bt: RefreshUnit(bt.UnitId); StatusCountdownText(bt); return null;
                case AnnounceEvent an when an.Text == "Blocked!": wallBlocked = true; return null; // GG-T935: shown on the wall by its hit
                case AnnounceEvent an when an.Text == TooHeavyText: return TooHeavyAnim(an.UnitId, null); // GG-T892: a fizzled shove, pull or swap
                case StepBlockedEvent sbh when sbh.Reason == "heavy": return TooHeavyAnim(-1, sbh.At); // GG-T892: a bot's drag step refused
                case AnnounceEvent an: ShowBanner(an.Text); return Tween.Wait(Timing.Standard);
                case TriggeredEvent tr: return TriggeredAnim(tr);
                case DodgedEvent dg: return DodgeAnim(dg);
                case ResolveEvent rv: return ResolveAnim(rv);
                case UnitsLinkedEvent ul: return LinkedAnim(ul); // GG-T873: Curse Link's tether
                case LinkEchoEvent le: return LinkEchoAnim(le);
                case MagnetizedEvent mz: return MagnetizedAnim(mz); // GG-T878: Magnetize's arcs and hum
                case DebuffsRedirectedEvent dr: return RedirectAnim(dr); // GG-T887: Redirect Flow's chi stream
                case AfterimageLeftEvent al: return AfterimageLeftAnim(al); // GG-T867: the Speedster's dash and its afterimage
                case AfterimageFadedEvent af: return AfterimageFadedAnim(af);
                case BoonStolenEvent bs: return SwipeAnim(bs); // GG-T866: Swipe Boon's snatch
                case TrapPlacedEvent tp: board.SyncTraps(State); App.Audio.Sfx("battle.trap"); return FloatText(tp.ById, CombatTextKind.Debuff, "Snare!", 0.3f);
                case TrapTriggeredEvent tt: board.SyncTraps(State); App.Audio.Sfx("battle.trap"); return FloatText(tt.UnitId, CombatTextKind.Debuff, "Trap!", 0.25f);
                case TrapClearedEvent tc: board.SyncTraps(State); App.Audio.Sfx("battle.trap"); return FloatText(tc.ById, CombatTextKind.Support, tc.Into == null ? "Cleared!" : "Changed!", 0.25f); // GG-T371
                case RegionRuleEvent rgr: return RegionRuleAnim(rgr); // GG-T359
                case RegionTilesEvent rgt: return RegionTilesAnim(rgt);
                case TileRevealedEvent trv: return TileRevealedAnim(trv); // GG-T843
                case TileSabotagedEvent tsb: return SabotageAnim(tsb); // GG-T948: the tech gremlin turns a gadget
                case SquishedEvent sqe: return SquishedAnim(sqe); // GG-T846
                case RuneCircleEvent rce: return RuneCircleAnim(rce); // GG-T939: the Choir's circle closes
                case VanishedEvent vne: return VanishedAnim(vne); // GG-T861: Smoke Vanish's puff, callout and reappear flash
                case RegionAnchorEvent rga: return RegionAnchorAnim(rga);
                case RegionFlagEvent rgf: return RegionFlagAnim(rgf);
                case HazardQueuedEvent hzq: return HazardQueuedAnim(hzq);
                case HazardRetimedEvent hzr: return HazardRetimedAnim(hzr);
                case HazardLandedEvent hzl: return HazardLandedAnim(hzl);
                case BrokeThroughEvent btk: return BrokeThroughAnim(btk); // GG-T869
                case HazardMovedEvent hzm: HazardMoved(hzm); return null; // GG-T874: a Time Bomb rides its carrier
                case FellEvent fel: return FellAnim(fel); // GG-T368
                case WeaponWornEvent _: return null;
                case WeaponShatteredEvent ws: App.Audio.Sfx("battle.shatter"); return FloatText(ws.UnitId, CombatTextKind.Debuff, "SHATTER!", 0.5f, 1f);
                case ArmorBrokenEvent ab: App.Audio.Sfx("battle.shatter", 0.7f); return FloatText(ab.UnitId, CombatTextKind.Debuff, "Armor broken!", 0.4f, 0.5f);
                case WeaponSwappedEvent sw: App.Audio.Sfx("battle.dash"); SyncBar(); return FloatText(sw.UnitId, CombatTextKind.Support, "⭾ " + (App.Content.TryItem(sw.ToWeapon)?.Name ?? "Swap"), 0.3f);
                case BossPhaseEvent bp: return BossAnim(bp);
                case RuleBrokenEvent rb: return RuleBreakAnim(rb);
                case BountyTargetEvent bte: return BountyAnim(bte); // GG-T431
                case ComboChangedEvent cce: return ComboChangedAnim(cce); // GG-T851: the combo meter, off the beat
                case ComboEndedEvent cen: return ComboEndedAnim(cen);
                case WaveStartedEvent wv: TutorialWaveStarted(); return WaveAnim(wv);
                case RoundStartedEvent rs: return RoundAnim(rs); // GG-T1091 (§2.13.2)
                case ReinforcementsArrivedEvent ra: return ReinforcementsAnim(ra);
                case EggLaidEvent el: return EggLaidAnim(el);
                case EggHatchedEvent eh: return HatchAnim(eh);
                case WaveClearedEvent wc: FlushTallies(); return WaveClearedAnim(wc);
                case BattleEndedEvent be: lastEnded = be; ShowBanner(be.Victory ? "VICTORY!" : be.Escaped ? "ESCAPED" : be.Withdrawn == true ? "FALL BACK" : "DEFEAT"); App.Audio.Sfx(be.Victory ? "battle.victory" : be.Escaped || be.Withdrawn == true ? "battle.dash" : "battle.defeat"); return Tween.Wait(Timing.Cinematic);
                case RevivedEvent rv: return ReviveAnim(rv);
                case PlayerControlEvent pc: FlushTallies(); ShowBanner(""); SyncAll(); CastCountdownTexts(); BombCountdownTexts(); WindowContinues(pc.NewWindow); return null;
                case TileConflictEvent tc:
                    // The engine's last-resort net fired (GG-T97): a root-cause bug. Loud in every build's log; in a
                    // dev build also on screen, and counted in /state so the harness fails on it.
                    Debug.LogError($"[BattleRunner] {tc} — stage {State.Stage.Id}, wave {State.Wave}, clock {State.Clock}");
                    BoardAudit.Conflict(tc.ToString());
                    if (Debug.isDebugBuild) ShowBanner("TILE CONFLICT — see log");
                    return null;
            }
            return null;
        }

        /// <summary>
        /// GG-T97, the view half of "two actors never share a tile": the tiles the board currently draws tokens on.
        /// Always checks that no two live tokens overlap; with <paramref name="againstState"/> also that every living
        /// unit is drawn exactly where it stands (and nothing fallen is still drawn); with <paramref name="rendered"/>
        /// that each token's actual on-screen position is its tile (no tween left mid-way).
        /// </summary>
        /// <param name="unitsOnItems">Also check that no unit token is drawn on a pickup token's tile (GG-T228). Per beat
        /// only non-hero tokens count (a hero's collect beat can trail its step by a few beats); against the state every unit.</param>
        public List<string> AuditTokens(bool againstState, bool rendered = false, bool unitsOnItems = true)
        {
            var problems = new List<string>();
            var drawn = new Dictionary<Coord, int>();
            foreach (var kv in board.Units)
            {
                var v = kv.Value;
                if (v.Dead) continue;
                for (int dy = 0; dy < v.H; dy++)
                    for (int dx = 0; dx < v.W; dx++)
                    {
                        var t = new Coord(v.Tile.X + dx, v.Tile.Y + dy);
                        if (drawn.TryGetValue(t, out var other)) problems.Add($"tokens {other} and {kv.Key} both drawn on {t}");
                        else drawn[t] = kv.Key;
                    }
                if (rendered)
                {
                    var want = BoardView.PosOf(v.Tile);
                    float left = v.Root.style.left.value.value, top = v.Root.style.top.value.value;
                    if (Mathf.Abs(left - want.x) > 0.5f || Mathf.Abs(top - want.y) > 0.5f) problems.Add($"token {kv.Key} rendered at ({left:0},{top:0}), not on its tile {v.Tile}");
                }
            }
            if (againstState)
                foreach (var u in State.Units)
                {
                    bool live = board.Units.TryGetValue(u.Id, out var v) && !v.Dead;
                    if (u.Alive && !live) problems.Add($"{u} stands on {u.Pos} but isn't drawn");
                    else if (!u.Alive && live) problems.Add($"{u} has fallen but is still drawn on {v.Tile}");
                    else if (u.Alive && v.Tile != u.Pos) problems.Add($"{u} drawn on {v.Tile} but stands on {u.Pos}");
                }
            AuditItemTokens(problems, againstState);
            if (unitsOnItems)
                foreach (var kv in board.PickupViews)
                {
                    if (!(kv.Value.userData is Coord pat) || !drawn.TryGetValue(pat, out var unitId)) continue;
                    if (againstState || State.UnitById(unitId)?.IsHero != true) problems.Add($"unit {unitId} drawn on pickup {kv.Key} at {pat}");
                }
            return problems;
        }

        /// <summary>
        /// GG-T129, the view half of "at most one board item per tile, ever" (§1.1.3): no two item tokens (pickups, the
        /// Power Amplifier) drawn on one tile, and with <paramref name="againstState"/> no stale token — every drawn
        /// pickup is still on the board where the state has it, every pickup in the state is drawn, and the amplifier
        /// is drawn exactly where (and only while) it stands.
        /// </summary>
        private void AuditItemTokens(List<string> problems, bool againstState)
        {
            var items = new Dictionary<Coord, string>();
            void Drawn(Coord at, string what)
            {
                if (items.TryGetValue(at, out var other)) problems.Add($"items {other} and {what} both drawn on {at}");
                else items[at] = what;
            }
            foreach (var kv in board.PickupViews)
                if (kv.Value.userData is Coord pat) Drawn(pat, "pickup " + kv.Key);
            if (board.AmpTile.HasValue) Drawn(board.AmpTile.Value, "amplifier");
            if (!againstState) return;
            foreach (var kv in board.PickupViews)
            {
                var p = State.Pickups.FirstOrDefault(x => x.Id == kv.Key);
                if (p == null) problems.Add($"pickup {kv.Key} is gone but its token is still drawn");
                else if (kv.Value.userData is Coord pat && pat != p.At) problems.Add($"pickup {kv.Key} drawn on {pat} but lies on {p.At}");
            }
            foreach (var p in State.Pickups)
                if (!board.PickupViews.ContainsKey(p.Id)) problems.Add($"pickup {p.Id} lies on {p.At} but isn't drawn");
            if (board.AmpTile != State.Amplifier) problems.Add($"amplifier drawn on {board.AmpTile?.ToString() ?? "nothing"} but stands on {State.Amplifier?.ToString() ?? "nothing"}");
        }
    }
}
