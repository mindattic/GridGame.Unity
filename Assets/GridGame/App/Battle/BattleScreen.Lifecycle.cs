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
    public sealed partial class BattleScreen
    {
        // MindAttic.Media.Capture: how the battle ended, set when BattleEndedEvent plays (BattleScreen.Playback.cs)
        private BattleEndedEvent lastEnded;

        public BattleScreen(string stageId) { this.stageId = stageId; }
        public BattleScreen(BattleSetup setup) { setupOverride = setup; stageId = setup.Stage.Id; }
        private BattleScreen(string stageId, bool practice) { this.stageId = stageId; this.practice = practice; }

        /// <summary>GG-T1647: a drill replayed for practice (Settings): its lessons always teach, and nothing is written to
        /// the save (no Bestiary, clear, seen lesson or onboarding step); it ends on the drill card, whose Back returns.</summary>
        public static BattleScreen Practice(string stageId) => new BattleScreen(stageId, true);

        private readonly bool practice;

        /// <summary>Seeds the autoplay bot's RNG (null = the clock). `-demo -endless` sets it to the encounter seed
        /// (GG-T64) so the bot decides exactly as <c>EndlessEncounter.Bot(seed)</c> does in a headless replay.</summary>
        public int? BotSeed { get; set; }

        /// <summary>The end of this battle is committed to App.Save and routed to the results screen. A battle built
        /// from an explicit setup normally just pops back instead — except under `-demo -endless` (GG-T57), whose
        /// setup is a generated party fought on a throwaway slot -1 save, and whose loop runs through the results.</summary>
        private bool CommitsToSave => !practice && (setupOverride == null || DemoMode.Endless);

        /// <summary>GG-T1647: a practice drill's end: its results card, with nothing committed.</summary>
        private void EndPractice()
        {
            var rep = Drills.Report(State);
            rep.Practice = true;
            PostBattleScreen.RouteFrom(rep, State.Stage.Id);
        }

        /// <summary>A still-open pause veil lives on the global Overlay (see OpenMenu), which outlives this screen's
        /// own root — remove it explicitly so it doesn't linger on top of whatever screen comes next.</summary>
        public override void OnHidden()
        {
            GameApp.StageResized -= OnStageResized;
            DisposePrefabFx();
            ReleaseStageArt(); // GG-T1205
            if (pauseVeil != null) { pauseVeil.RemoveFromHierarchy(); pauseVeil = null; ResumeBattle(); }
            CloseAbilities(false);
            awayOnSettings = false;
            // The billboard lives on the Overlay too; without this, each battle left one behind (lines from the
            // last ~1.6 s showing over the PostBattle screen) and they piled up battle after battle (GG-T70).
            billboard?.RemoveFromHierarchy();
            billboard = null;
            if (Coach.Active && (Coach.LessonId ?? "").StartsWith("battle-")) Coach.Cancel(); // GG-T539: a lesson leaves with its battle
        }

        protected override void Build(VisualElement root)
        {
            var initial = new List<BattleEvent>();
            // Build runs again on this same instance when the player backs out of "How to play" (the router
            // re-mounts the screen). Every per-build view cache must start empty, or the HUD keeps writing to
            // the previous build's detached elements (every slot read "Locked", timeline icons
            // gone, coin label blank) — and the once-per-battle work (tutorial, opening playback, capture
            // event) must not run a second time (GG-T70).
            bool firstBuild = !built;
            built = true;
            CloseAbilities(false);
            timelineIconViews.Clear();
            shownCoins = -1;
            if (!firstBuild)
            {
                // Back from "How to play": the aim, a press/drag/hold and an inspect press all belonged to the old board
                // (left set, CanCommand stayed false and the battle soft-locked); darkness re-hides on the fresh views.
                TargetingSlot = -1;
                PendingTarget = null;
                tapMoveTile = tapMovePress = null;
                tapMovePath = null;
                tapMovePreview = null;
                tapMoveHeroId = -1;
                targetGlows.Clear();
                EndHold();
                dragging = pressing = false;
                dragPath.Clear();
                preview = null;
                inspectUnitId = -1;
                inspectOpen = false;
                activePointerId = -1;
                hintShown = false;
                outcomeTags.Clear();
                darkHidden.Clear();
                awayOnSettings = false; // playback, held at its next beat while Settings was up, carries on
                if (pauseVeil != null) pauseVeil.style.display = DisplayStyle.Flex; // back from the pause menu's Settings (GG-T1909)
            }
            if (firstBuild)
            {
                if (State == null) { CreateState(); stageOwner = this; Art.BeginScope(ArtScope.Stage); Art.Preload(StageTextureSet.For(App.Content, setup)); } // mounted without the router's Prepare
                initial.AddRange(openingEvents); // the recorder holds openingEvents itself, so it is handed over, not cleared
                openingEvents = null;
                // Every battle is tagged with the dial and the multipliers it applied (§22.9, GG-T309).
                var diff = DifficultyTable.At(setup.Difficulty);
                GameTelemetry.Begin(setup.Seed, Application.version, // GG-T548: a writable folder on every platform, never throws
                    new TelemetryData
                    {
                        ["stage"] = State.Stage.Id, ["source"] = "player",
                        ["difficulty"] = setup.Difficulty,
                        ["diffEnemyHp"] = diff.EnemyHp, ["diffEnemyDamage"] = diff.EnemyDamage, ["diffEnemyLevel"] = diff.EnemyLevelOffset,
                        ["diffRewardGold"] = diff.RewardGold, ["diffRewardXp"] = diff.RewardXp, ["diffItemDropRate"] = diff.DropChance,
                    });
                BeginEncounterTelemetry(setup, initial, diff); // GG-T59
            }

            var bg = Ui.El("fill", root);
            LivingBackdrop.Show(bg, State.Stage.Theme, StageTextureSet.BattleBackdropIndex(stageId)); // Art wraps it over the theme's own count (GG-T344); staged sky (GG-T576)
            bg.style.unityBackgroundImageTintColor = Palette.Backdrop.Battle;
            shake = Ui.El("fill", root);

            BuildTopBar(shake);
            BuildTimeline(shake);

            boardHost = Ui.El("", shake);
            boardHost.style.position = Position.Absolute;
            boardHost.style.top = BoardTop;
            board = new BoardView(boardHost, State.Stage.Theme);
            board.SetScrim(Mathf.Max(App.Content.Sky?.BoardScrim ?? 0f, App.Content.Sky?.RegionFor(State.Stage.Theme)?.BoardScrim ?? 0f)); // GG-T665, GG-T1950
            CreatePrefabFx();
            board.Root.pickingMode = PickingMode.Position;
            // The pincer outline (board.Lines) stays inside the board, so it inherits every camera twist, zoom,
            // drift and screen shake and always hugs its tiles (GG-T128). Cut-in cards (ActionCard: SupporterPop,
            // chain pop-ups, caster slides) live in a stage-sized layer inside the board too, just BELOW the lines,
            // so a card popping in during a pincer never covers the outline (GG-T65's need), and both sit in this
            // screen's own tree, under the global Overlay, so nothing of the battle can draw over Pause (GG-T126).
            // Board order: tiles, units, marks, fx, cut-in cards, lines.
            cutInLayer = new VisualElement { name = "card-layer", pickingMode = PickingMode.Ignore };
            cutInLayer.style.position = Position.Absolute;
            cutInLayer.style.top = -BoardTop;
            board.Root.Add(cutInLayer);
            board.Lines.BringToFront();
            board.Root.RegisterCallback<PointerDownEvent>(OnPointerDown);
            board.Root.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            board.Root.RegisterCallback<PointerUpEvent>(OnPointerUp);
            board.Root.RegisterCallback<PointerCaptureOutEvent>(_ => { if (dragging) CommitDrag(); pressing = false; });

            BuildAnnouncementBand(shake);
            BuildAbilityBar(shake);
            BuildDescriptionPanel(shake);
            BuildTapArrow(shake);

            ApplyStageLayout();
            foreach (var u in State.Units) if (u.Alive) board.Add(u);
            SelectedHeroId = State.Units.FirstOrDefault(u => u.IsCommandable && u.Alive)?.Id ?? -1;
            if (firstBuild && State.Tutorial && Lessons.Length > 0) TutorialStep = 0;
            if (!firstBuild) SyncDarkness();
            SyncAll();
            if (!firstBuild) return;
            playRoutine = App.StartCoroutine(Play(initial, false));
            if (CommandLine.Has("-autobattle") || DemoMode.Active || TestMode.Active) AutoPlay = true;
            else if (TutorialStep < 0 && !practice && App.Save != null && App.Save.Flags.Add("hint:inspect"))
            {
                // GG-T252: a player who skipped the tutorial still learns the gesture once.
                App.Persist();
                panel.schedule.Execute(() => Advise("Tip: tap or hold any hero, foe or icon to read it; Abilities lists every move")).StartingIn(2500);
            }
            CaptureUnity.Raise("battle-start", new CaptureData { ["stage"] = State.Stage.Id });
        }

        public override void OnShown() { GameApp.StageResized -= OnStageResized; GameApp.StageResized += OnStageResized; }

        /// <summary>A programmatic leave (<see cref="LeaveBattle"/>) is under way: the next OnBack lets the router pop.</summary>
        private bool leaving;
        /// <summary>"How to play" pushed Settings over this battle (Push never calls OnHidden): playback holds at its next
        /// beat until the battle is shown again, so it can't end (and route) behind Settings.</summary>
        private bool awayOnSettings;
        /// <summary>The running event playback, so the watchdog can stop it rather than let a second Play run beside it.</summary>
        private Coroutine playRoutine;
        /// <summary>The autoplay bot's simulated drag in flight (GG-T586: stopped when the battle is torn down).</summary>
        private Coroutine dragRoutine;

        /// <summary>Pops this battle off the router. OnBack swallows Back (it opens the pause menu), so a plain
        /// Router.Back() from here did nothing; this one-shot flag lets exactly this pop through.</summary>
        private void LeaveBattle()
        {
            leaving = true;
            App.Router.Back();
        }

        public override bool OnBack()
        {
            if (leaving) { leaving = false; return true; }
            // Back/Escape on the open pause menu resumes, like its Resume row.
            if (pauseVeil != null) { closePause?.Invoke(); return false; }
            if (AbilitiesOpen) { SheetBack(); return false; }
            if (TargetingSlot >= 0) { CancelByPlayer(); return false; }
            if (TapMoving) { CancelTapMove(); return false; }
            OpenMenu();
            return false;
        }

        private VisualElement pauseVeil;
        private Action closePause;

        /// <summary>The pause menu is up: the battle's clock is stopped (GG-T126).</summary>
        public bool Paused => pauseVeil != null;
        /// <summary>Playback beats started so far this battle — the automation proof that none start while paused.</summary>
        public int PlaybackBeats { get; private set; }
        private int beatsAtPause;
        /// <summary>Beats that started while the menu was up (must stay 0; logged as an error when not).</summary>
        public int BeatsWhilePaused { get; private set; }
        /// <summary>Seconds of unpaused battle time; the playback watchdog measures against this, so a long pause
        /// can't make it fire on Resume.</summary>
        private float battleTime;

        /// <summary>
        /// Stops the battle's clock (GG-T126): Tween.Dt reads 0, so every playback beat, cut-in, outline, float, coin
        /// burst, dissolve, banner scroll and ambient pulse holds exactly where it is; the event player and Safe()
        /// stop stepping; the drag timer, hints and auto battle stop; ringing sound effects pause mid-clip. Resume
        /// carries on from the same frame.
        /// </summary>
        private void PauseBattle()
        {
            Tween.Paused = true;
            beatsAtPause = PlaybackBeats;
            App.Audio.HoldSfx(true);
        }

        private void ResumeBattle()
        {
            Tween.Paused = false;
            App.Audio.HoldSfx(false);
        }

        /// <summary>The pause veil must be the last child of the Overlay, above the full-screen pincer cut-in, the
        /// billboard, coin and orb flights — everything playback adds there or BringToFront()s (GG-T126).</summary>
        /// <summary>Automation: the open pause veil is the topmost Overlay child (nothing of the battle draws over it).</summary>
        public bool PauseOnTop => pauseVeil != null && pauseVeil.parent != null && pauseVeil.parent.IndexOf(pauseVeil) == pauseVeil.parent.childCount - 1;

        private void KeepPauseOnTop()
        {
            if (pauseVeil != null && pauseVeil.parent != null && pauseVeil.parent.IndexOf(pauseVeil) != pauseVeil.parent.childCount - 1)
                pauseVeil.BringToFront();
        }

        /// <summary>GG-T480: the phone went to the background (a call, the home button, the notification shade) — the
        /// battle stops on its pause menu, so coming back finds it exactly where it was, waiting for "Resume".</summary>
        public override void OnAppSuspended() => OpenMenu();

        private void OpenMenu()
        {
            // Escape/Back re-fires OnBack unconditionally (it doesn't know a veil is already up) — without
            // this guard, repeated presses before "Resume" stack another full veil on the global Overlay
            // each time, the same stale-overlay risk the board.Lines/Confirm.DismissStale comments warn about.
            if (ended || pauseVeil != null) return;
            var overlay = App.Overlay;
            var veil = pauseVeil = Ui.El("fill veil col center", overlay);
            veil.name = "pause-veil";
            veil.pickingMode = PickingMode.Position;
            PauseBattle();
            var w = Ui.Window("Paused", veil);
            w.style.width = Length.Percent(72);
            void Close() { veil.RemoveFromHierarchy(); pauseVeil = null; ResumeBattle(); }
            closePause = Close;
            Ui.MenuRow("Resume", "", Close, w).KindIcon(RowKind.Enter); // GG-T751 (§26.7)
            Ui.MenuRow(AutoPlay ? "Auto battle: ON" : "Auto battle: OFF", "", () => { AutoPlay = !AutoPlay; Close(); }, w).KindIcon(RowKind.Settings);
            Ui.MenuRow("Battle log", logLines.Count > 0 ? Ui.Count(logLines.Count) : "", () => ShowBattleLog(veil, w), w).KindIcon(RowKind.Menu);
            Ui.MenuRow("Settings", "", () => { veil.style.display = DisplayStyle.None; awayOnSettings = true; App.Router.Push(new SettingsScreen()); }, w).KindIcon(RowKind.Settings); // GG-T1909
            if (Dev.DevOptions.Enabled) Ui.MenuRow("Note", "", () => Dev.FeedbackNotes.Begin(veil), w).KindIcon(RowKind.Menu); // GG-T1939
            Ui.MenuRow("How to play", "", () => { Close(); awayOnSettings = true; App.Router.Push(new SettingsScreen()); }, w).KindIcon(RowKind.Bestiary);
            int retreatPct = (int)(BattleConfig.RetreatKeepFraction * 100);
            if (InCrucible)
                Ui.MenuRow("Leave the Crucible", "(keep all)", () => { Close();
                    Confirm.Show($"Leave the Crucible after {State.Wave} wave{(State.Wave == 1 ? "" : "s")}? You keep every coin, item and point of experience earned so far.", "Leave", Retreat); }, w).KindIcon(RowKind.Quit);
            else
                Ui.MenuRow("Retreat", $"(keep {retreatPct}%)", () => { Close();
                    Confirm.Show($"Retreat from battle? You'll keep {retreatPct}% of this fight's gold and loot — safer than risking a wipe.", "Retreat", Retreat); }, w).KindIcon(RowKind.Back);
            if (Dev.DevOptions.Cheats) DevCheatRows(w, Close); // GG-T1938
            Ui.Select(w[1]);
        }

        /// <summary>The pause menu's Battle log page (GG-T1825): the combat log (<see cref="logPage"/>, still named "feed")
        /// mounted in its own window over the veil, newest line at the bottom; Back returns to the menu.</summary>
        private void ShowBattleLog(VisualElement veil, VisualElement menu)
        {
            if (logPage == null) return;
            menu.style.display = DisplayStyle.None;
            var w = Ui.Window("Battle log", veil);
            w.name = "battle-log";
            w.style.width = Length.Percent(86);
            w.style.height = Length.Percent(70);
            logPage.style.flexGrow = 1;
            logPage.style.flexShrink = 1;
            logPage.style.display = DisplayStyle.Flex;
            w.Add(logPage);
            if (logLines.Count == 0) Ui.Text("Nothing has happened yet this battle.", "small dim", w);
            var back = Ui.MenuRow("Back", "", () =>
            {
                logPage.RemoveFromHierarchy();
                w.RemoveFromHierarchy();
                menu.style.display = DisplayStyle.Flex;
            }, w);
            back.KindIcon(RowKind.Back);
            back.name = "battle-log-back";
            ScrollLogToEnd();
        }

        // ── Battle records (§22.6): every battle is saved as JSON for balance analysis and future ML ──
        private BattleRecorder recorder;
        private int autoplayedTurns;
        public string LastRecordPath { get; private set; }

        private void SaveRecord()
        {
            if (recorder == null) return;
            try
            {
                var doc = recorder.Finish(State);
                if (autoplayedTurns > 0) doc["source"] = autoplayedTurns >= recorder.Turns ? "bot:autoplay" : "player+autoplay";
                var art = CommandLine.Value("-artifacts");
                var dir = CommandLine.Value("-records") ?? (art != null ? System.IO.Path.Combine(art, "records") : System.IO.Path.Combine(Application.persistentDataPath, "battle-records"));
                System.IO.Directory.CreateDirectory(dir);
                var file = System.IO.Path.Combine(dir, $"{DateTime.UtcNow:yyyyMMdd-HHmmss}_{State.Stage.Id}_{State.Phase}.json");
                System.IO.File.WriteAllText(file, recorder.ToJson());
                LastRecordPath = file;
                if (CommandLine.Value("-records") == null && art == null) PruneRecords(dir);
                Debug.Log($"[Battle] record saved: {file} ({recorder.Turns} turns)");
            }
            catch (Exception e) { Debug.LogWarning($"[Battle] could not save the battle record: {e.Message}"); }
            recorder = null;
        }

        /// <summary>The default records folder (persistentDataPath) keeps only the newest battles, so a player's device
        /// doesn't collect one JSON file per battle forever. Names start with a UTC timestamp, so name order is age order.</summary>
        private const int KeptRecords = 50;

        private static void PruneRecords(string dir)
        {
            try
            {
                var files = System.IO.Directory.GetFiles(dir, "*.json");
                if (files.Length <= KeptRecords) return;
                Array.Sort(files, StringComparer.Ordinal);
                for (int i = 0; i < files.Length - KeptRecords; i++) System.IO.File.Delete(files[i]);
            }
            catch (Exception e) { Debug.LogWarning($"[Battle] could not prune old battle records: {e.Message}"); }
        }

        private void Retreat()
        {
            // Retreating while an event list is still animating would tear down this screen out from under
            // coroutines that `App` (the persistent singleton, not this screen) is still running for it —
            // they'd keep playing sfx/vfx into whatever screen comes next. Same Busy gate as CanCommand().
            // The Confirm dialog outlives the pause menu, so it can be answered after the battle already ended
            // (autoplay or the last playback finishing behind it): EndBattle has set `ended` and is about to
            // Commit — retreating then would overwrite Victory/Defeat and Commit the battle twice (GG-T70).
            if (playing || ended || State.IsOver) return;
            ended = true;
            // The same end-of-battle bookkeeping EndBattle/EndStalled do (capture event, record, encounter telemetry, the
            // session's closing record), with the phase set first so the record and the encounter read "retreated".
            State.Phase = BattlePhase.Retreated;
            CaptureUnity.Raise("battle-end", new CaptureData { ["stage"] = State.Stage.Id, ["result"] = "retreated" });
            SaveRecord();
            RecordEncounterTelemetry();
            TelemetryUnity.Finish(new
            {
                stage = State.Stage.Id,
                result = "retreated",
                waveReached = State.Wave + 1,
                actions = State.Results.ActionsTaken,
                ticks = State.Results.TicksElapsed,
                coins = State.Results.Coins,
                xp = State.Results.Xp,
                heroesStanding = State.Units.Count(u => u.IsHero && u.Alive),
            });
            if (practice) EndPractice();
            else if (App.Save != null && CommitsToSave)
            {
                var rep = BattleFactory.Commit(App.Content, App.Save, State);
                App.Persist();
                PostBattleScreen.RouteFrom(rep, State.Stage.Id);
            }
            else LeaveBattle(); // Router.Back() alone was swallowed by OnBack (it opens the pause menu)
        }
    }
}
