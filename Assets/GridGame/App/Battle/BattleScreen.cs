using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Bot;
using GridGame.Core.Content;
using GridGame.Core.Rules;
using GridGame.Meta;
using MindAttic.Media.Capture;
using MindAttic.Media.Capture.Unity;
using MindAttic.Telemetry;
using MindAttic.Telemetry.Unity;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>
    /// The battle. Layout follows the bible HUD rows (§9.0): top bar (hamburger · stage and wave · gold) · timeline ·
    /// 6×8 board · party mana strip over the action row [Abilities][Info][GUARD] · description panel to the safe bottom. The simulation (BattleState) is the only truth;
    /// this screen turns input into commands and plays the resulting events back.
    /// </summary>
    public sealed partial class BattleScreen : GameScreen
    {
        public override string Id => "battle";
        /// <summary>GG-T456: one track per stage, drawn once from the region's shuffle bag (Meta.MusicPlaylist) and kept
        /// while overlays (level-ups, dialogue) come and go over the battle. A Crucible run plays MomentMusic.crucible when
        /// campaign.json lists any (GG-T458), else any region's battle track.</summary>
        public override string Music => music ??= (InCrucible ? App.Playlist.Moment(App.Content.Campaign, MusicPlaylist.Crucible) : null)
            ?? App.Playlist.NextBattle(App.Content.Campaign, App.Content.ThemeOf(stageId)) ?? "crusade-00";
        private string music;
        private bool bossMusic;

        /// <summary>GG-T459: when a wave brings on a finale's rule-breaking boss (§14.9; a bounty target's rule-break
        /// doesn't count), the music crossfades to MomentMusic.boss, or .finalboss on the stage the Ending follows (the
        /// Hierarch), falling back to .boss and then to the stage's own track when those playlists are empty. Kept for
        /// the rest of the stage, overlays included.</summary>
        private void BossWaveMusic(WaveStartedEvent wv)
        {
            if (bossMusic || InCrucible) return;
            bool boss = false;
            foreach (var id in wv.Spawned)
                if (State.UnitById(id) is Unit u && u.RuleBreak != RuleBreak.None && u.BountyId == null) { boss = true; break; }
            if (!boss) return;
            bossMusic = true;
            var camp = App.Content.Campaign;
            string track = camp?.Ending?.Stage == stageId ? App.Playlist.Moment(camp, MusicPlaylist.FinalBoss) : null;
            track ??= App.Playlist.Moment(camp, MusicPlaylist.Boss);
            if (track == null) return;
            music = track;
            App.Audio.Music(track);
        }

        public BattleState State { get; private set; }
        private LocalSession session;
        /// <summary>The battle's session seam (GG-LAW-13): one local seat over the battle authority. Re-wrapped if the
        /// state is ever replaced, so commands always reach the battle on screen.</summary>
        private LocalSession Session => session != null && session.State == State ? session : (session = new LocalSession(State));
        public bool Busy => playing || dragging;
        public int SelectedHeroId { get; private set; } = -1;
        public int TargetingSlot { get; private set; } = -1;
        /// <summary>The picked anchor awaiting Confirm (owner direction 2026-10-11: select, confirm, act).</summary>
        public Coord? PendingTarget { get; private set; }
        public bool Confirming => PendingTarget.HasValue;
        private bool ConfirmActions => App.Settings.ConfirmActions;
        /// <summary>HeroBot plays this battle. Forced on by <c>-autobattle</c> or <c>-demo</c>
        /// (the tour, see GameApp.DemoMode); otherwise toggled from the pause menu or the automation
        /// "autoplay" endpoint.</summary>
        public bool AutoPlay { get; set; }

        private readonly string stageId;
        private readonly BattleSetup setupOverride;
        private BoardView board;

        /// <summary>The newest ShowBanner line (automation reads it); the line itself is a billboard row that fades.</summary>
        private string bannerText = "";

        private bool playing;
        private bool ended;
        private HeroBot autoBot;
        /// <summary>Strict-best shadow bot (BotSkillProfile demo mode): only instantiated when `-telemetry`
        /// and a below-100 skill are both active, to log the road not taken alongside the move actually played.</summary>
        private HeroBot perfectBot;

        // First-battles hint: after a pause, show the move the bot would make (§29 tutorial question → contextual hints).
        private float idle;
        private bool hintShown;
        /// <summary>The hint's or a tip's words (§9.2.1): the description panel's idle content, gone on input or after
        /// <see cref="AdviceSeconds"/>. The hint's board marks stay on the board.</summary>
        private string advice;
        private float adviceAt;
        private const float AdviceSeconds = 6f;
        /// <summary>The last refusal's reason (§9.0): the panel's state line until the next input.</summary>
        private string refusal;
        private const float HintDelay = 5f;
        private bool HintsEnabled => App.Save == null || App.Save.HighestClearedStage < 2;

        // drag state
        private bool pressing, dragging;
        /// <summary>True for the duration of <see cref="SimulatedHumanDrag"/> — pauses the real drag-timer
        /// countdown (Update()) so a slow, low-skill simulated drag can't be auto-committed out from under it.</summary>
        private bool simulatingDrag;
        private int dragHeroId = -1;
        private Vector2 pressPos;
        private Coord dragTile;
        private readonly List<Coord> dragPath = new List<Coord>();
        private MovePreview preview;
        private float dragTimeLeft;
        // diagonal slip gesture tracking (BattleScreen.Input.cs)
        private Vector2 lastPointerPos;
        private float lastPointerTime;

        private bool Colorblind => App.Settings.ColorblindMode;
        /// <summary>This battle is a Crucible run (§22.10): open-ended wave count, keep-everything retreat.</summary>
        private bool InCrucible => Crucible.IsStage(stageId);
        private bool ReduceMotion => App.Settings.ReduceMotion;

        // ─────────────────────────────────────────────────────────────────────────
        // Commands
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>Not while a drag is in progress either: a second finger tapping GUARD or a slot mid-drag would
        /// commit that instead and silently drop the drag, leaving the previewed units out of place (GG-T70).
        /// CommitDrag clears <c>dragging</c> before it commits, so the drag's own command still goes through. Nor while
        /// examining (GG-T1918): no battle time passes until Examine mode closes.</summary>
        public bool CanCommand() => !playing && !ended && !State.IsOver && !dragging && !Paused && !examining;

        /// <summary>Dev/automation helper: add orbs to every spell meter (cheat — development builds only call this).</summary>
        public void DevGiveMana(int n)
        {
            BattleEngine.GainMana(State, n * BattleConfig.ManaPointsPerOrb, "dev");
            SyncBar();
        }

        public string BannerText => bannerText;
        /// <summary>The hint or tip the description panel shows right now (§9.2.1), or null.</summary>
        public string AdviceText => advice;

        // GG-T358: the battle-start announcements (a wanderer, a guest) play in the opening beats and the billboard
        // drops each line after 1.6 s, so a poll that arrives a moment late misses them. Automation reads this
        // history of every banner and billboard line this battle has shown (newest last, capped) instead.
        private const int ShownLinesCap = 200;
        private readonly List<string> shownLines = new List<string>();
        public IReadOnlyList<string> ShownLines => shownLines;

        private void NoteShown(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            shownLines.Add(text);
            if (shownLines.Count > ShownLinesCap) shownLines.RemoveAt(0);
        }

        /// <summary>Dev/automation helper: put a buff on a unit and refresh its view.</summary>
        /// <summary>Dev/automation helper: put an ability ("poison") or item ("item:healing_potion_basic:3") in a hero's bar slot.</summary>
        public bool DevSetBar(int unitId, int slot, string entry)
        {
            var u = State.UnitById(unitId);
            if (u == null || !u.IsHero || slot < 0 || slot >= u.Bar.Count || string.IsNullOrEmpty(entry)) return false;
            if (entry.StartsWith("item:"))
            {
                var parts = entry.Split(':');
                int n = parts.Length > 2 && int.TryParse(parts[2], out var k) ? k : 3;
                u.Bar[slot] = new AbilitySlot { ItemId = parts[1], Charges = n, MaxCharges = n };
            }
            else u.Bar[slot] = new AbilitySlot { AbilityId = entry };
            SyncBar();
            return true;
        }

        /// <summary>Dev/automation helper: set a unit's HP (art review: make the next blow a kill).</summary>
        /// <summary>Dev/automation (GG-T241, GG-T342): the next player-launched battle holds this wanderer, skipping the 25% roll.</summary>
        public static string DevNextWanderer;

        /// <summary>Dev/automation (GG-T241): end the battle as a victory now and commit it like a real one, so story
        /// beats that hang off a win (the ending, interludes, rescues) can be reached without fighting every wave.</summary>
        public bool DevWin()
        {
            if (playing || ended || State.IsOver || App.Save == null || !CommitsToSave) return false;
            ended = true;
            State.Phase = BattlePhase.Victory;
            var rep = BattleFactory.Commit(App.Content, App.Save, State);
            App.Persist();
            PostBattleScreen.RouteFrom(rep, State.Stage.Id);
            return true;
        }

        public bool DevHp(int unitId, int hp)
        {
            var u = State.UnitById(unitId);
            if (u == null || !u.Alive) return false;
            u.Hp = Mathf.Clamp(hp, 1, u.MaxHp);
            SyncAll();
            return true;
        }

        /// <summary>Dev/automation helper: force a hero's weapon to a given durability (art/harness review — GG-T39).</summary>
        public bool DevWeaponDurability(int unitId, int durability)
        {
            var u = State.UnitById(unitId);
            if (u == null || !u.Alive || u.WeaponId == null) return false;
            u.WeaponDurability = Mathf.Max(0, durability);
            SyncAll();
            return true;
        }

        public bool DevBuff(int unitId, string buffId)
        {
            var u = State.UnitById(unitId);
            var def = App.Content.TryBuff(buffId);
            if (u == null || def == null) return false;
            u.Buffs.Add(new BuffInstance { Def = def, Remaining = def.Duration });
            SyncAll();
            return true;
        }

        /// <summary>Apply a command to the simulation and play its consequences. <paramref name="perfectCmd"/>:
        /// demo-mode only (BotSkillProfile) — the strict-best move this same decision point would have made at
        /// full skill, recorded alongside whatever imperfect <paramref name="cmd"/> was actually played.</summary>
        public void Commit(BattleCommand cmd, bool skipMovePrefix = false, BattleCommand perfectCmd = null)
        {
            if (!CanCommand()) return;
            ClearTapMove();
            bool wasTargeting = TargetingSlot >= 0;
            CancelTargeting();
            readSlot = -1;
            if (wasTargeting) { SyncBar(); panelUnitId = -1; SyncPanel(); }
            bool telemetryOn = TelemetryUnity.Current.Enabled;
            var before = recorder != null || telemetryOn ? BattleRecorder.Snapshot(State) : null;
            // GG-LAW-13: input never writes state; the command is stamped with this machine's seat and goes through the
            // session's one pipeline (LocalSession applies it this frame, exactly as BattleEngine.Apply always did).
            cmd.Player = Session.LocalPlayer;
            var events = Session.SubmitAndStep(cmd);
            recorder?.Add(before, cmd, events, State, perfectCmd, perfectCmd != null ? BotSkillProfile.Level : (float?)null);
            ObserveEncounterTelemetry(events); // GG-T59
            if (telemetryOn)
            {
                bool accepted = events.Exists(e => e is PlayerControlEvent || e is BattleEndedEvent);
                // Same type-labeled shape BattleRecorder writes to disk, so every event is self-describing (not just its raw fields).
                TelemetryUnity.RecordTurn(before, BattleRecorder.Command(cmd), accepted, BattleRecorder.Events(events), BattleRecorder.Snapshot(State));
            }
            if (AutoPlay) autoplayedTurns++;
            Debug.Log($"[Battle] {cmd} → {events.Count} events, clock {State.Clock}");
            TutorialAfterCommit(cmd, events);
            playRoutine = App.StartCoroutine(Play(events, skipMovePrefix));
        }

        private void OnSlot(int slot)
        {
            DropRefusal();
            ClearTapMove();
            if (!CanCommand()) { TapSlotCard(slot); return; } // GG-T450: looking is free, even mid-playback
            var hero = State.UnitById(SelectedHeroId);
            if (hero == null) return;
            if (TargetingSlot == slot) { CancelByPlayer(); return; }
            if (TargetingSlot >= 0) CancelTargeting();
            var status = BattleEngine.GetSlotStatus(State, hero.Id, slot);
            if (status != SlotStatus.Ready)
            {
                App.Audio.Sfx("ui.error");
                ShowBanner(StatusText(status));
                TapSlotCard(slot); // GG-T450: it can't be aimed, but the tap still reads it
                return;
            }
            var a = BattleEngine.SlotAbility(State, hero, slot);
            if (a == null) return;
            App.Audio.Sfx("ui.select");
            if (a.Mode == TargetMode.Auto)
            {
                if (!ConfirmActions || AutoPlay) { Commit(new UseSlotCommand { HeroId = hero.Id, Slot = slot, Target = hero.Pos }); return; }
                TargetingSlot = slot;
                SetPending(hero.Pos);
                SyncBar();
                return;
            }
            var targets = BattleEngine.ValidTargets(State, hero.Id, slot);
            if (targets.Count == 0) { ShowBanner("No valid target"); App.Audio.Sfx("ui.error"); TapSlotCard(slot); return; }
            TargetingSlot = slot;
            board.ClearMarks();
            foreach (var t in targets) board.Mark(t, Palette.Gold, 0.35f);
            SyncBar();
            SyncPanel();
        }

        private static string StatusText(SlotStatus s)
        {
            switch (s)
            {
                case SlotStatus.Unaffordable: return "Still charging";
                case SlotStatus.Cooldown: return "Recharging";
                case SlotStatus.OutOfCharges: return "None left";
                case SlotStatus.Silenced: return "Silenced!";
                case SlotStatus.CastCapReached: return "Too many spells!";
                case SlotStatus.Locked: return "Unlocks as you progress";
                case SlotStatus.CannotAct: return "Can't act";
                case SlotStatus.Acted: return "Already acted this turn";
                case SlotStatus.TooLittleTime: return $"Too little time — needs {BattleConfig.HeroActionSeconds:0.0}s"; // GG-T311
                case SlotStatus.NoTargets: return "No one to use it on"; // GG-T501: a revive with nobody fallen, an Encore with nothing to copy
                default: return "Empty slot";
            }
        }

        public void CancelTargeting()
        {
            if (TargetingSlot < 0) return;
            TargetingSlot = -1;
            PendingTarget = null;
            board.ClearMarks();
            ClearTargetGlows();
            ShowBanner("");
        }

        /// <summary>Player-facing cancel (the aim or confirm card's Cancel, Back): a soft "never mind" tone, then the Abilities
        /// sheet reopens at the move's detail page (GG-T1921). Not while the lessons run: their coach marks own the screen.</summary>
        public void CancelByPlayer()
        {
            if (TargetingSlot < 0) return;
            int slot = TargetingSlot;
            App.Audio.Sfx("ui.cancel", 0.8f);
            CancelTargeting();
            SyncBar();
            SyncPanel();
            if (!TutorialActive) OpenAbilities(slot);
        }

        /// <summary>Automation's <c>click name=slotN</c> (GG-T1922): the move on bar slot <paramref name="slot"/> through the
        /// Abilities sheet, opened at its detail page, then Perform / Cast / Use. A move that can't be committed now is read
        /// instead, and the move being aimed is cancelled, as a tap of its slot cell always did.</summary>
        public void TapSlot(int slot)
        {
            var hero = State.UnitById(SelectedHeroId);
            if (hero == null || TargetingSlot == slot || !CanCommand()) { OnSlot(slot); return; }
            OpenAbilities(slot);
            var go = sheetVeil?.Q("abilities-commit");
            if (go != null && go.enabledSelf) { CommitMove(hero, slot); return; }
            CloseAbilities(false);
            OnSlot(slot);
        }

        /// <summary>
        /// Lock a pick and ask for confirmation: the area and every unit it would touch light up and the description panel
        /// turns into the confirm card (on whom, the outcome, Cast / Cancel). Tapping another legal tile re-aims; tapping the same
        /// tile again (or Confirm) commits.
        /// </summary>
        private void SetPending(Coord anchor)
        {
            var hero = State.UnitById(SelectedHeroId);
            var a = hero == null || TargetingSlot < 0 ? null : BattleEngine.SlotAbility(State, hero, TargetingSlot);
            if (a == null) return;
            PendingTarget = anchor;
            RedrawTargeting();
            App.Audio.Sfx("ui.select", 0.8f);
            SyncPanel();
            var btn = panel.Q<Button>("confirm-action");
            if (btn != null) App.StartCoroutine(Tween.Pop(btn, 1.08f, 0.25f));
        }

        /// <summary>Commit the pending pick (Confirm button, or a second tap on the same tile).</summary>
        public void ConfirmPending()
        {
            if (!PendingTarget.HasValue || TargetingSlot < 0 || !CanCommand()) return;
            var hero = State.UnitById(SelectedHeroId);
            if (hero == null) return;
            App.Audio.Sfx("ui.confirm");
            var cmd = new UseSlotCommand { HeroId = hero.Id, Slot = TargetingSlot, Target = PendingTarget.Value };
            Commit(cmd);
        }

        private readonly HashSet<int> targetGlows = new HashSet<int>();

        private void ClearTargetGlows()
        {
            foreach (var id in targetGlows)
                if (board.Units.TryGetValue(id, out var v)) v.SetGlow(Palette.White, 0f);
            targetGlows.Clear();
            if (board.Units.TryGetValue(SelectedHeroId, out var sv)) sv.SetGlow(new Color(1f, 0.9f, 0.5f), 0.65f);
        }

        /// <summary>Legal anchors in gold; the pending pick's area (red harmful, green helpful, blue teleport) and its units glowing.</summary>
        private void RedrawTargeting()
        {
            var hero = State.UnitById(SelectedHeroId);
            var a = hero == null || TargetingSlot < 0 ? null : BattleEngine.SlotAbility(State, hero, TargetingSlot);
            board.ClearMarks();
            ClearTargetGlows();
            if (a == null) return;
            if (a.Mode != TargetMode.Auto)
                foreach (var vt in BattleEngine.ValidTargets(State, hero.Id, TargetingSlot)) board.Mark(vt, Palette.Gold, 0.25f);
            if (!PendingTarget.HasValue) return;
            var at = PendingTarget.Value;
            bool harmful = a.BaseDamage > 0 || a.Filter == TargetFilter.EnemyOnly;
            var color = a.IsTeleport ? Palette.Mana : harmful ? new Color(1f, 0.3f, 0.25f) : new Color(0.4f, 0.9f, 0.5f);
            var hits = TargetResolver.Collect(State, a, hero, at);
            if (a.Mode == TargetMode.Auto || a.Shape == TargetShape.Self || a.Shape == TargetShape.AllAllies || a.Shape == TargetShape.AllEnemies)
                foreach (var u in hits) foreach (var t in u.Tiles()) board.Mark(t, color, 0.42f);
            else foreach (var t in TargetResolver.Tiles(at, a.Shape, a.Radius, hero.Pos)) board.Mark(t, color, 0.42f); // GG-T933: Wall oriented from the caster
            foreach (var u in hits)
                if (board.Units.TryGetValue(u.Id, out var v)) { v.SetGlow(color, 0.9f); targetGlows.Add(u.Id); }
        }

        public override void Tick(float dt)
        {
            TickPrefabFx();
            SyncBossBar();
            if (Paused)
            {
                // GG-T126: the battle's clock is stopped — no watchdog, drag timer, hint, long-press or auto battle
                // runs, and the veil stays above anything a frozen beat already put on the Overlay.
                KeepPauseOnTop();
                if (PlaybackBeats != beatsAtPause)
                {
                    BeatsWhilePaused += PlaybackBeats - beatsAtPause;
                    Debug.LogError($"[Pause] {PlaybackBeats - beatsAtPause} playback beat(s) advanced while paused");
                    beatsAtPause = PlaybackBeats;
                }
                return;
            }
            battleTime += dt;
            // Watchdog: playback can never hold input hostage.
            if (playing && battleTime - playStarted > 20f)
            {
                Debug.LogError("[Battle] playback watchdog fired; resyncing");
                // Stop the stuck playback, or it would carry on beside the next command's Play (and route twice).
                if (playRoutine != null) { App.StopCoroutine(playRoutine); playRoutine = null; }
                // What the stopped Play's own epilogue would have reset.
                board.Lines.ClearLines();
                unplayedCasts.Clear();
                actingEntry = -1;
                pendingItemUser = -1; pendingItemId = null;
                ResetPincerBeats(); ResetRampage(); EndEnemyBeats(); // GG-T1233: tempo and trap drawing back to the state's own
                RefreshRecoverView();
                playing = false;
                SyncAll();
                if (State.IsOver && !ended) App.StartCoroutine(EndBattle()); // the stopped Play would have ended it
            }
            TickLinger(dt);
            TickHeroTurn(dt);
            if (dragging && !simulatingDrag && App.Settings.DragTimer && !Untimed)
            {
                dragTimeLeft -= dt;
                timerLabel.text = gapLabel != null && gapLabel.style.display == DisplayStyle.Flex ? "" : $"{Mathf.Max(0, dragTimeLeft):0.0}s"; // GG-T1912: the gap caption already shows the seconds
                if (dragTimeLeft <= 0f && !CoachRail && !coachCancelStep) CommitDrag(); // a coached drag is never forced (GG-T539)
            }
            else if (timerLabel != null) timerLabel.text = "";
            if (roundLabel != null) roundLabel.style.visibility = string.IsNullOrEmpty(timerLabel?.text) ? Visibility.Visible : Visibility.Hidden;
            if (advice != null && battleTime - adviceAt > AdviceSeconds) DropAdvice();

            TickBoardInspect();
            if (TutorialActive && !LessonWaiting && CurrentLesson.DoneOnInspect && InspectCard.IsOpen && (!CoachOn || (!Coach.Active && coachPopupFor == TutorialStep))) NextLesson();
            TickCoach();

            if (!AutoPlay && CanCommand() && !pressing && TargetingSlot < 0 && HintsEnabled && !Coach.Active)
            {
                idle += dt;
                if (idle > (TutorialDemoMove ? 0.8f : HintDelay) && !hintShown) ShowHint();
            }
            else ClearHint();

            if (AutoPlay && CanCommand() && !pressing)
            {
                autoBot ??= new HeroBot(BotSeed ?? Environment.TickCount);
                autoBot.SkillLevel = BotSkillProfile.Level;
                var cmd = autoBot.Decide(State);
                BattleCommand perfect = null;
                if (BotSkillProfile.TelemetryEnabled && BotSkillProfile.Level < 99.9f)
                {
                    perfectBot ??= new HeroBot(BotSeed ?? Environment.TickCount) { SkillLevel = 100f };
                    perfect = perfectBot.Decide(State);
                }
                if (cmd is MoveCommand m && m.Path.Count > 0) dragRoutine = App.StartCoroutine(SimulatedHumanDrag(m, perfect));
                else Commit(cmd, perfectCmd: perfect);
            }
        }

        /// <summary>
        /// Plays a bot move as a human-like simulated drag, driving the real pointer pipeline
        /// (<see cref="HandlePointerPosition"/>) instead of teleporting the hero tile-to-tile — so a diagonal
        /// slip succeeds or fails for the same kinematic reasons a real player's drag would, and the capture
        /// footage shows an actual hand-shaped path rather than a mechanical snap. The route is built from a
        /// cubic Bézier through every waypoint of <paramref name="m"/>'s path (Catmull-Rom-style tangents so
        /// corners carry velocity through them instead of stopping dead — a dead stop can never clear the
        /// diagonal-slip speed gate), timed by Fitts's Law and eased by a minimum-jerk profile, with
        /// physiological tremor and an occasional overshoot-then-correct, all scaled by BotSkillProfile.
        /// <paramref name="perfectCmd"/>: the strict-best move at this same decision (telemetry only, may be null).
        /// </summary>
        private IEnumerator SimulatedHumanDrag(MoveCommand m, BattleCommand perfectCmd)
        {
            if (!BeginSimulatedDrag(m.HeroId)) yield break;
            playing = true;
            simulatingDrag = true;
            playStarted = battleTime;

            var hero = State.UnitById(m.HeroId);
            var pts = new List<Vector2> { BoardView.CenterOf(hero.Pos) };
            foreach (var step in m.Path) pts.Add(BoardView.CenterOf(step));

            float skill01 = BotSkillProfile.Skill01;
            float totalLen = 0f;
            for (int i = 1; i < pts.Count; i++) totalLen += Vector2.Distance(pts[i - 1], pts[i]);
            float duration = Mathf.Max(0.08f, BotSkillProfile.FittsDuration(totalLen, BoardView.Tile));
            float tremor = BotSkillProfile.TremorPixels;
            bool willOvershoot = pts.Count > 0 && UnityEngine.Random.value < BotSkillProfile.OvershootChance;
            var overshootDir = pts.Count > 1 ? (pts[pts.Count - 1] - pts[pts.Count - 2]).normalized : Vector2.zero;
            var overshootPoint = pts[pts.Count - 1] + overshootDir * Mathf.Lerp(45f, 8f, skill01);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                float tau = BotSkillProfile.MinimumJerk(elapsed / duration);
                var basePos = EvaluateHumanPath(pts, tau, skill01);
                var jitter = new Vector2(Mathf.PerlinNoise(elapsed * 11f, 0.37f) - 0.5f, Mathf.PerlinNoise(0.91f, elapsed * 11f) - 0.5f) * tremor;
                HandlePointerPosition(basePos + jitter);
                if (!pressing) { playing = false; simulatingDrag = false; yield break; } // hero died / battle ended mid-drag
                yield return null;
                elapsed += Tween.Dt;
            }
            if (willOvershoot)
            {
                float ct = 0f; const float correctDuration = 0.1f;
                HandlePointerPosition(overshootPoint);
                while (ct < correctDuration)
                {
                    HandlePointerPosition(Vector2.Lerp(overshootPoint, pts[pts.Count - 1], ct / correctDuration));
                    yield return null;
                    ct += Tween.Dt;
                }
            }
            HandlePointerPosition(pts[pts.Count - 1]);
            playing = false;
            simulatingDrag = false;
            CommitDrag(perfectCmd);
        }

        /// <summary>
        /// A point at parametric position <paramref name="tau"/> (0-1) along a smoothed polyline through
        /// <paramref name="pts"/>: each interior waypoint is pulled toward the corner's bisector by an amount
        /// that grows with skill (a confident player cuts the corner close to a true diagonal; a clumsy one
        /// hugs the exact tile-to-tile route — see <see cref="BotSkillProfile.DiagonalAttemptChance"/>'s sibling
        /// reasoning), and consecutive segments share Catmull-Rom tangents so the path — and its velocity —
        /// stays continuous through every corner instead of stopping and restarting at each tile.
        /// </summary>
        private static Vector2 EvaluateHumanPath(List<Vector2> pts, float tau, float skill01)
        {
            if (pts.Count < 2) return pts.Count == 1 ? pts[0] : Vector2.zero;
            float cut = Mathf.Lerp(0.05f, 0.8f, skill01);
            var shaped = new List<Vector2> { pts[0] };
            for (int i = 1; i < pts.Count - 1; i++)
                shaped.Add(Vector2.Lerp(pts[i], Vector2.Lerp(pts[i - 1], pts[i + 1], 0.5f), cut));
            shaped.Add(pts[pts.Count - 1]);

            float[] segLen = new float[shaped.Count - 1];
            float total = 0f;
            for (int i = 0; i < segLen.Length; i++) { segLen[i] = Vector2.Distance(shaped[i], shaped[i + 1]); total += segLen[i]; }
            if (total <= 0f) return shaped[0];

            float target = Mathf.Clamp01(tau) * total, acc = 0f;
            int seg = 0;
            while (seg < segLen.Length - 1 && acc + segLen[seg] < target) { acc += segLen[seg]; seg++; }
            float localTau = segLen[seg] > 0f ? (target - acc) / segLen[seg] : 0f;

            var p0 = shaped[seg];
            var p3 = shaped[seg + 1];
            var prev = seg > 0 ? shaped[seg - 1] : p0 - (p3 - p0);
            var next = seg + 2 < shaped.Count ? shaped[seg + 2] : p3 + (p3 - p0);
            var p1 = p0 + (p3 - prev) / 6f;
            var p2 = p3 - (next - p0) / 6f;
            float u = 1f - localTau;
            return u * u * u * p0 + 3f * u * u * localTau * p1 + 3f * u * localTau * localTau * p2 + localTau * localTau * localTau * p3;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Selection / banner helpers
        // ─────────────────────────────────────────────────────────────────────────

        private void ShowHint()
        {
            hintShown = true;
            var decision = TutorialDemoMove ? (BattleCommand)new HeroBot(7).BestMove(State) ?? new HeroBot(7).Decide(State) : new HeroBot(7).Decide(State);
            if (decision is UseSlotCommand use)
            {
                var caster = State.UnitById(use.HeroId);
                var ab = caster == null ? null : BattleEngine.SlotAbility(State, caster, use.Slot);
                if (caster != null && ab != null)
                {
                    SelectHero(caster.Id);
                    if (abilitiesButton != null) App.StartCoroutine(Tween.Pop(abilitiesButton, 1.12f, 0.35f));
                    Advise($"Hint: {caster.Name} can use {ab.Name}");
                }
                return;
            }
            if (decision is ShieldCommand) { Advise("Hint: nothing good to do? Press GUARD"); return; }
            var cmd = decision as MoveCommand;
            if (cmd == null || cmd.Path.Count == 0) return;
            var pv = BattleEngine.PreviewMove(State, cmd.HeroId, cmd.Path);
            // The Moving lesson promises a glowing path even when no pincer is reachable yet (the opening board of
            // the first battle): demo the bot's best positioning move instead of falling back to a bare tip.
            if (pv.Pincers.Count == 0 && !TutorialDemoMove) { Advise("Tip: trap an enemy between two heroes"); return; }
            var hero = State.UnitById(cmd.HeroId);
            var dest = cmd.Path[cmd.Path.Count - 1];
            board.ClearMarks();
            board.Mark(dest, Palette.Gold, 0.5f);
            if (board.Units.TryGetValue(hero.Id, out var hv)) { hv.SetGlow(Palette.Gold, 0.95f); App.StartCoroutine(Tween.Pop(hv.Root, 1.12f, 0.35f)); }
            board.Lines.ClearLines();
            // GG-T736: one Shapes path arrow along the route, its head on the last step.
            var route = new List<Vector2> { BoardView.CenterOf(hero.Pos) };
            foreach (var step in cmd.Path) route.Add(BoardView.CenterOf(step));
            board.Lines.PathArrow(route, ShapeStyle.Indicator);
            Advise($"Hint: drag {hero.Name} to the glowing tile");
        }

        private void ClearHint()
        {
            idle = 0f;
            if (!hintShown) return;
            hintShown = false;
            DropAdvice();
            // While aiming the marks are the aim's own, but the hint's route lines are still the hint's: clear those.
            if (!dragging) board.Lines.ClearLines();
            if (TargetingSlot < 0 && !dragging) board.ClearMarks();
        }

        /// <summary>Puts a hint's or tip's words in the description panel (§9.2.1), never over the board.</summary>
        private void Advise(string text)
        {
            advice = text;
            adviceAt = battleTime;
            NoteShown(text);
            SyncPanel();
        }

        private void DropAdvice()
        {
            if (advice == null) return;
            advice = null;
            SyncPanel();
        }

        private void DropRefusal()
        {
            if (refusal == null) return;
            refusal = null;
            SyncPanel();
        }

        /// <summary>One wrapped line at the top of a description page (the refusal, the hint).</summary>
        private static void PanelLine(VisualElement page, string text, string classes, string name)
        {
            var l = Ui.Text(text, classes, page);
            l.name = name;
            l.style.whiteSpace = WhiteSpace.Normal;
            l.style.marginBottom = Space.S8;
        }

        public void SelectHero(int id)
        {
            if (SelectedHeroId == id)
            {
                // Re-touching the selected hero still dismisses an inspected enemy card (it hid the tutorial card).
                if (panelUnitId >= 0 || readSlot >= 0) { panelUnitId = -1; readSlot = -1; SyncPanel(); }
                return;
            }
            CancelTargeting();
            ClearTapMove();
            SelectedHeroId = id;
            panelUnitId = -1;
            foreach (var u in State.Units)
                if (board.Units.TryGetValue(u.Id, out var v) && u.Alive)
                    v.SetGlow(new Color(1f, 0.9f, 0.5f), u.Id == id ? 0.65f : 0f);
            SyncBar();
            SyncPanel();
        }

        /// <summary>Combat log (bible §9, GG-T391): every event line this battle goes to the info panel's Log page,
        /// newest at the bottom, the last <see cref="LogCap"/> kept. Nothing draws over the board any more.</summary>
        public void Feed(string text, Color color)
        {
            if (string.IsNullOrEmpty(text)) return;
            logLines.Add((text, color));
            if (logLines.Count > LogCap) logLines.RemoveAt(0);
            AddLogLine(text, color);
            if (logPage != null && logPage.resolvedStyle.display != DisplayStyle.None) ScrollLogToEnd();
        }

        /// <summary>
        /// An announcement (§9.0, GG-T1824): "PINCER!", a turn, a wave, a refusal, "VICTORY!". It joins the billboard as one
        /// of its fading lines on the board's top edge, never a row; repeating the newest line keeps it up instead of
        /// stacking a copy, and an empty text takes the banner lines down (the billboard's own lines run their course).
        /// </summary>
        private void ShowBanner(string text)
        {
            bannerText = text ?? "";
            if (bannerText.Length == 0) { ClearBannerLines(); return; }
            Billboard(bannerText, null, Palette.Text, banner: true);
        }
    }
}
