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
    /// 6×8 board · icon-only ability bar with GUARD at its right end · description panel to the safe bottom. The simulation (BattleState) is the only truth;
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
        public override bool RebuildOnResize => false; // ApplyStageLayout re-lays the HUD out in place

        // Layout (design pixels). Vertical positions are fixed from the top; horizontal ones follow the live stage
        // width (bible §26.3) so a wider phone centres the board and stretches the timeline instead of leaving a gap.
        // §9.0 (GG-T1822): fixed rows, nothing stacked between them: the top bar, the timeline, the board a gutter under
        // the timeline frame (the old banner row is gone, so the board gains its 117 px), the ability bar, the description panel.
        private const float BoardTop = TimelineY + TimelineFrameHeight / 2f + Space.S8;
        private static float BoardLeft => (GameApp.StageWidth - BoardView.Tile * BattleConfig.Cols) / 2f;
        // The timeline's geometry lives with it in BattleScreen.Timeline.cs (GG-T287).

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
        private readonly List<VisualElement> slotViews = new List<VisualElement>();
        /// <summary>The description panel's page (§9.0): the selected unit, its skills and the ability/confirm card draw here
        /// (a ScrollView, so Add/Clear reach its content).</summary>
        private VisualElement panel;
        /// <summary>Where the announcement overlay sits (§9.0, GG-T1824): no row of its own, an empty anchor on the board's
        /// top edge that the billboard reads its place from; it takes no input.</summary>
        private VisualElement bannerViewport;
        // Row heights (§9.0, GG-T1894): the top bar hugs the safe top and every gap between rows is tightened, so the description
        // panel gets the height back (the board keeps its 180-px tiles). The icon-only slots need no room for a name.
        private const float TopBarTop = Space.S8, TopBarHeight = 110f, BarHeight = 140f;
        private const float BannerTop = BoardTop + Space.S8;
        /// <summary>One announcement line's height; the billboard stacks up to <see cref="BillboardMaxLines"/> of them.</summary>
        private const float BannerViewportHeight = Size.Target - Space.S24;
        // The HUD rows under the board, top to bottom, a gutter apart (§9.0): the ability bar (five icon-only slots), then
        // the description panel, which stretches to the safe bottom and so alone absorbs a taller stage (§26.3).
        private static float BoardBottom => BoardTop + BoardView.Tile * BattleConfig.Rows;
        private static float BarTop => BoardBottom + Space.S24; // 24 px from the board's tiles, also targets (GG-T656, POL-C3)
        // GG-T706 (owner 2026-11-23): the ability bar is always the row under the board. GG-T523's side columns beside the
        // board on a 2:3 / 3:4 stage are gone: those shapes are now letterboxed at 9:16 (bible §26.3), so no stage is wide
        // enough for them.
        /// <summary>The description panel starts 16 px under the ability row (GG-T1894).</summary>
        private static float DescriptionTop => BarTop + BarHeight + Space.S16;
        private Label waveLabel, coinLabel, timerLabel;
        /// <summary>The newest ShowBanner line (automation reads it); the line itself is a billboard row that fades.</summary>
        private string bannerText = "";
        private VisualElement shake;
        private VisualElement boardHost;
        /// <summary>Stage-sized layer inside the board, under board.Lines, that holds cut-in cards (GG-T128).</summary>
        private VisualElement cutInLayer;
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
            awayOnSettings = false;
            // The billboard lives on the Overlay too; without this, each battle left one behind (lines from the
            // last ~1.6 s showing over the PostBattle screen) and they piled up battle after battle (GG-T70).
            billboard?.RemoveFromHierarchy();
            billboard = null;
            if (Coach.Active && (Coach.LessonId ?? "").StartsWith("battle-")) Coach.Cancel(); // GG-T539: a lesson leaves with its battle
        }

        private bool Colorblind => App.Settings.ColorblindMode;
        /// <summary>This battle is a Crucible run (§22.10): open-ended wave count, keep-everything retreat.</summary>
        private bool InCrucible => Crucible.IsStage(stageId);
        private bool ReduceMotion => App.Settings.ReduceMotion;

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
            slotViews.Clear();
            timelineIconViews.Clear();
            shownCoins = -1;
            if (!firstBuild)
            {
                // Back from "How to play": the aim, a press/drag/hold and an inspect press all belonged to the old board
                // (left set, CanCommand stayed false and the battle soft-locked); darkness re-hides on the fresh views.
                TargetingSlot = -1;
                PendingTarget = null;
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
            bg.style.unityBackgroundImageTintColor = new Color(0.78f, 0.78f, 0.86f);
            shake = Ui.El("fill", root);

            BuildTopBar(shake);
            BuildTimeline(shake);

            boardHost = Ui.El("", shake);
            boardHost.style.position = Position.Absolute;
            boardHost.style.top = BoardTop;
            board = new BoardView(boardHost, State.Stage.Theme);
            board.SetScrim(App.Content.Sky?.RegionFor(State.Stage.Theme)?.BoardScrim ?? 0f); // GG-T665
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
                panel.schedule.Execute(() => Advise("Tip: tap or hold any hero, foe, slot or icon to read it")).StartingIn(2500);
            }
            CaptureUnity.Raise("battle-start", new CaptureData { ["stage"] = State.Stage.Id });
        }

        // ─────────────────────────────────────────────────────────────────────────
        // HUD construction
        // ─────────────────────────────────────────────────────────────────────────

        private void BuildTopBar(VisualElement root)
        {
            var bar = Ui.El("row", root);
            bar.style.position = Position.Absolute;
            bar.style.left = Fit.Edge; bar.style.right = Fit.Edge; bar.style.top = TopBarTop; bar.style.height = TopBarHeight;
            RowIcon.HamburgerButton(OpenMenu, bar); // GG-T754: icon-only pause
            // §9.0 row 1: the stage name and "Wave n/m" small on one line between the hamburger and the gold; the name
            // shrinks to the floor, then scrolls (GG-T1143), the wave never does.
            var mid = Ui.El("row grow", bar);
            mid.style.alignItems = Align.Center; mid.style.justifyContent = Justify.Center;
            mid.style.minWidth = Space.S0;
            mid.style.marginLeft = mid.style.marginRight = Space.S16;
            var stageName = Ui.Marquee(Ui.Text(State.Stage.Name, "small shadow", mid), grow: false);
            stageName.style.flexShrink = 1; stageName.style.minWidth = Space.S0; stageName.style.unityTextAlign = TextAnchor.MiddleCenter;
            var sep = Ui.Text("·", "small shadow", mid);
            sep.style.flexShrink = 0; sep.style.marginLeft = sep.style.marginRight = Space.S8;
            // Full-colour, not .dim: the LivingBackdrop's drifting shards pass behind the top bar, and dim grey "1/5"
            // vanished against a pale shard so the label read as just "Wave" (GG-T666).
            waveLabel = Ui.Text("", "small shadow", mid);
            waveLabel.style.flexShrink = 0;
            coinLabel = Ui.Text("", "h2 gold shadow", bar);
            coinLabel.style.width = Fit.W(17f); coinLabel.style.unityTextAlign = TextAnchor.MiddleRight;
        }

        /// <summary>The announcement anchor (§9.0, GG-T1824): an empty, input-free box on the board's top edge where the
        /// billboard's fading lines stack. The drag timer's seconds sit in the timeline row beside the NOW diamond, above the
        /// track, so nothing of the drag covers the board.</summary>
        private void BuildAnnouncementBand(VisualElement root)
        {
            bannerViewport = Ui.El("", root);
            bannerViewport.name = "banner-viewport";
            bannerViewport.pickingMode = PickingMode.Ignore;
            bannerViewport.style.position = Position.Absolute;
            bannerViewport.style.left = bannerViewport.style.right = Fit.Edge; bannerViewport.style.top = BannerTop; bannerViewport.style.height = BannerViewportHeight;
            timerLabel = Ui.Text("", "h2 shadow warn", root);
            timerLabel.name = "drag-timer";
            Ui.Scrim(timerLabel); // GG-T1898
            timerLabel.pickingMode = PickingMode.Ignore;
            timerLabel.style.position = Position.Absolute;
            timerLabel.style.left = Fit.Edge + SlotSpan + Space.S8;
            timerLabel.style.top = TimelineY - TimelineFrameHeight / 2f; timerLabel.style.height = TrackY - IconSpan / 2f;
            timerLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
        }

        /// <summary>Re-applies the positions that depend on the stage width (build time and on resize).</summary>
        private void ApplyStageLayout()
        {
            if (boardHost == null) return;
            boardHost.style.left = BoardLeft;
            cutInLayer.style.left = -BoardLeft;
            // The rows under the board (ability bar, description panel) keep to the centred content column, so on a
            // tablet they don't stretch past usefulness; the board centres and the timeline spans the stage (GG-T398).
            float inset = Fit.Inset;
            LayoutAbilityBar(inset);
            if (Root?.Q("description-panel") is VisualElement desc)
            {
                desc.style.left = desc.style.right = inset + Fit.Edge;
                desc.style.top = DescriptionTop;
            }
            cutInLayer.style.width = GameApp.StageWidth; cutInLayer.style.height = GameApp.StageHeight;
            LayoutTimelineFrame();
        }

        private void OnStageResized()
        {
            ApplyStageLayout();
            SyncAll();
        }

        public override void OnShown() { GameApp.StageResized -= OnStageResized; GameApp.StageResized += OnStageResized; }

        /// <summary>The ability row (named "abilitybar") under the board: five skill cells, then GUARD.</summary>
        private VisualElement abilityBar;

        /// <summary>Lays the ability cells out as one row under the board, inside the HUD column.</summary>
        private void LayoutAbilityBar(float inset)
        {
            if (abilityBar == null) return;
            var bar = abilityBar;
            bar.style.top = BarTop; bar.style.height = BarHeight;
            // Row padding + each cell's 12-px margin: slots sit 24 px apart (POL-C3), the row's edge equals that gap
            // (POL-C8), and the cells still line up with the description panel at inset + Fit.Edge (GG-T651, GG-T656).
            bar.style.left = bar.style.right = inset + Fit.Edge - 2 * Space.TargetHalfGap;
            bar.style.paddingLeft = bar.style.paddingRight = Space.TargetHalfGap;
            foreach (var cell in bar.Children()) LayCell(cell);
        }

        /// <summary>A cell's share of the row: an even split, a gutter either side.</summary>
        private static void LayCell(VisualElement cell)
        {
            cell.style.flexGrow = 1;
            cell.style.flexBasis = 0;
            cell.style.height = BarHeight;
            cell.style.marginLeft = cell.style.marginRight = Space.TargetHalfGap;
            cell.style.marginTop = cell.style.marginBottom = Space.S0;
        }

        private void BuildAbilityBar(VisualElement root)
        {
            var bar = abilityBar = Ui.El("row", root);
            bar.name = "abilitybar";
            bar.style.position = Position.Absolute;
            // Five skill slots and GUARD fill the row evenly (owner 2026-11-21: "the 5 abilities should perfectly fit
            // that row"; GG-T1904 adds GUARD as the sixth); even gutters on the space scale, and the
            // cells, not the gaps, take a wider stage (POL-C1/C8). Laid out by LayoutAbilityBar.
            bar.style.top = BarTop; bar.style.height = BarHeight; // left/right: ApplyStageLayout (the HUD column)
            bar.style.justifyContent = Justify.FlexStart;
            for (int i = 0; i < 5; i++)
            {
                int idx = i;
                var slot = HudCell(bar);
                slot.name = "slot" + i;
                slot.AddToClassList("slot");
                Ui.Pressable(slot);
                slot.RegisterCallback<ClickEvent>(_ => OnSlot(idx));
                // GG-T1819: icon-only (owner: long names "truncated or ..." can't work as 5 buttons); the full name is
                // in the description panel.
                Ui.AbilitySlot(slot);
                slotViews.Add(slot);
            }
            BuildGuardCell(bar);
        }

        /// <summary>The GUARD cell for <see cref="SyncGuardCall"/>'s gold call and Guard's mana motes (<see cref="MintFrom"/>).</summary>
        private VisualElement guardButton;
        /// <summary>The GUARD cell's resting edge: pewter on the dark window, apart from the skills' plain frames.</summary>
        private static readonly Color GuardEdge = Palette.PewterLight;

        /// <summary>GG-T1904 (owner 2026-11-28, §9.0): GUARD at the right end of the bottom action row, in thumb reach, built
        /// apart from the slot cells so they can be swapped out (GG-T1914). Its Protection shield fills the cell; the GUARD
        /// caption shows only while it fits whole.</summary>
        private void BuildGuardCell(VisualElement bar)
        {
            var cell = guardButton = HudCell(bar);
            cell.name = "shield";
            cell.AddToClassList("guard");
            cell.style.backgroundImage = new StyleBackground(Art.WindowDark);
            cell.style.borderTopColor = cell.style.borderBottomColor = cell.style.borderLeftColor = cell.style.borderRightColor = GuardEdge;
            Ui.Pressable(cell);
            cell.RegisterCallback<ClickEvent>(_ => { if (CanCommand()) Commit(new ShieldCommand()); });
            var icon = Ui.El(null, cell); icon.name = "icon";
            icon.pickingMode = PickingMode.Ignore;
            icon.style.position = Position.Absolute;
            icon.style.left = icon.style.top = icon.style.right = Space.S16;
            icon.style.backgroundImage = new StyleBackground(Art.StatusIcon("protection"));
            icon.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            var caption = Ui.Text("GUARD", "tiny shadow", cell);
            caption.name = "guard-label";
            caption.pickingMode = PickingMode.Ignore;
            caption.style.position = Position.Absolute;
            caption.style.left = caption.style.right = Space.S0;
            caption.style.bottom = Space.S8;
            caption.style.unityTextAlign = TextAnchor.LowerCenter;
            caption.style.whiteSpace = WhiteSpace.NoWrap;
            Fonts.UseHeavy(caption);
            cell.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                var need = caption.MeasureTextSize(caption.text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined);
                bool fits = need.x <= cell.contentRect.width - 2 * Space.S8 && need.y <= cell.contentRect.height / 3f;
                caption.style.display = fits ? DisplayStyle.Flex : DisplayStyle.None;
                icon.style.bottom = fits ? Space.S8 + need.y : Space.S16;
            });
        }

        /// <summary>One cell of the ability row: a window-framed column that takes an even share of the row.</summary>
        private static VisualElement HudCell(VisualElement bar)
        {
            var cell = Ui.El("col center", bar);
            cell.style.flexGrow = 1; cell.style.flexBasis = 0; cell.style.height = BarHeight;
            cell.style.marginLeft = cell.style.marginRight = Space.S8;
            cell.style.borderTopLeftRadius = cell.style.borderTopRightRadius = cell.style.borderBottomLeftRadius = cell.style.borderBottomRightRadius = 18;
            cell.style.borderTopWidth = cell.style.borderBottomWidth = cell.style.borderLeftWidth = cell.style.borderRightWidth = 4;
            cell.style.backgroundImage = new StyleBackground(Art.Window);
            return cell;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Sync views ← state (the safety net after every playback)
        // ─────────────────────────────────────────────────────────────────────────

        public void SyncAll()
        {
            SyncTempo();
            foreach (var u in State.Units)
            {
                if (!board.Units.TryGetValue(u.Id, out var v))
                {
                    if (!u.Alive) continue;
                    v = board.Add(u);
                }
                if (!u.Alive)
                {
                    if (!v.Dead) { v.Root.RemoveFromHierarchy(); v.Dead = true; }
                    continue;
                }
                if (v.Dead) { board.UnitLayer.Add(v.Root); v.Dead = false; }
                board.Place(v, u.Pos);
                v.Root.style.opacity = 1; v.Root.style.scale = new Scale(Vector2.one); v.Root.style.translate = new Translate(0, 0);
                v.SetHp(u.Hp, u.MaxHp);
                v.SetBuffs(u, Colorblind, State.TurnEntryOf(u.Id)?.StunTicks > 0);
                v.SetPlate(u.Facing, u.PlateHp, u.PlateMax);
                v.SetActed(u.IsHero && (TurnRules.OneHeroPerTurn(State) ? TurnRules.MovedThisRound(State, u) : TurnRules.HasActed(State, u))); // GG-T308, §2.13: acted this party turn, as the mode says; GG-T787: no ✓ when the turn is one hero's action; GG-T800: under rounds the ✓ holds until the round resets
                if (u.IsHero)
                {
                    var weaponDef = u.WeaponId != null ? App.Content.TryItem(u.WeaponId) : null;
                    bool hasWeapon = weaponDef != null && weaponDef.Durability > 0;
                    float wMax = hasWeapon ? weaponDef.Durability * (u.WeaponMastery > 1f ? BattleConfig.MasterworkDurabilityMultiplier : 1f) : 1f;
                    float wFrac = hasWeapon ? u.WeaponDurability / wMax : 1f;
                    // Five armor pieces condense to one gear icon: any equipped at all, worst-condition fraction (GG-T89).
                    bool hasArmor = u.Gear.Count > 0;
                    float aFrac = 1f;
                    foreach (var piece in u.Gear.Values)
                    {
                        var def = App.Content.TryItem(piece.ItemId);
                        if (def == null || def.Durability <= 0) continue;
                        float max = def.Durability * (piece.Mastery > 1f ? BattleConfig.MasterworkDurabilityMultiplier : 1f);
                        float frac = max > 0f ? piece.Durability / max : 1f;
                        if (frac < aFrac) aFrac = frac;
                    }
                    v.SetGear(hasWeapon, wFrac, hasWeapon && u.WeaponDurability == 1, hasArmor, aFrac, !ReduceMotion);
                }
                else v.SetGear(false, 0f, false, false, 0f, !ReduceMotion);
                bool sel = u.Id == SelectedHeroId;
                var aura = AuraFor(u);
                if (sel) v.SetGlow(new Color(1f, 0.9f, 0.5f), 0.65f);
                else if (aura.a > 0f && !ReduceMotion) v.SetGlow(aura, 0.45f);
                else v.SetGlow(Color.white, 0f);
            }
            board.SyncTraps(State);
            board.SyncRegion(State); // GG-T359
            board.SyncBlocks(State.Blocks);
            board.SyncAmplifier(State.Amplifier);
            board.SyncPickups(State);
            SyncTimeline(null);
            SyncBar();
            SyncPanel();
            int wave = Math.Min(State.Wave + 1, State.Stage.Waves.Count);
            waveLabel.text = InCrucible ? $"Crucible wave {wave}" : $"Wave {wave}/{State.Stage.Waves.Count}";
            if (shownCoins < 0) SyncCoins();
        }

        /// <summary>Linger effect (bible §6.2 stage 4): units carrying a strong status glow its color.</summary>
        private static Color AuraFor(Unit u)
        {
            foreach (var b in u.Buffs)
                switch (b.Def.Id)
                {
                    case "burning": return new Color(1f, 0.45f, 0.1f, 1f);
                    case "frozen": return new Color(0.6f, 0.9f, 1f, 1f);
                    case "poisoned": return new Color(0.5f, 1f, 0.3f, 1f);
                    case "regen": return Palette.Good;
                    case "sleep": return new Color(0.7f, 0.7f, 1f, 1f);
                    case "taunting": return new Color(1f, 0.3f, 0.2f, 1f);
                    case "empowered": return Palette.Buff;
                }
            return new Color(0, 0, 0, 0);
        }

        /// <summary>The effect that stops this unit's clock or turn, if any: buffs that halt/immobilise, else pushback stun.</summary>
        private static (string icon, string letter, Color color, string name)? StatusOf(Unit u, TimelineEntry turn)
        {
            foreach (var b in u.Buffs)
                if (b.Def.HaltsTimeline || b.Def.Immobile)
                    return (b.Def.Id, string.IsNullOrEmpty(b.Def.Letter) ? b.Def.Name.Substring(0, 1) : b.Def.Letter, Art.HexColor(b.Def.Color, new Color(0.6f, 0.85f, 1f)), b.Def.Name);
            if (turn != null && turn.StunTicks > 0) return ("stunned", "!", Palette.Gold, "Stunned");
            return null;
        }

        // ── Pending casts ──────────────────────────────────────────────────────

        private Dictionary<int, float> lastAt;
        private int castCycle;

        /// <summary>
        /// A cast time shows only on the caster's timeline entry (BIBLE §9.0, GG-T1817, owner 2026-11-27): nothing is drawn on
        /// board tiles for a pending cast. This keeps each queued hero cast's ghost at its place on the bar.
        /// </summary>
        private void SyncCastGhosts(Dictionary<int, float> at)
        {
            foreach (var g in castGhosts.Values)
            {
                if (at != null && at.TryGetValue(g.EntryId, out var u)) g.U = u;
                else if (State.Timeline.FirstOrDefault(x => x.Id == g.EntryId) is TimelineEntry e) g.U = BattleEngine.BarU(State, e);
            }
        }

        /// <summary>The shared 1.2 s beat: overlapping timeline circles and status slots each show their next item.</summary>
        private void CycleBeat()
        {
            if (Paused) return; // the 1.2 s beat is game time too (GG-T126)
            castCycle++;
            if (State == null || board == null || lastAt == null) return;
            SyncCastGhosts(lastAt);
            LayoutIcons(lastAt);
            SyncTimelineStatus();
            foreach (var v in board.Units.Values) v.NextStatus();
        }

        /// <summary>Dev hook (automation): queue a pending enemy cast on a unit so its timeline entry can be checked.</summary>
        public int DevQueueCast(int targetId, string abilityId, int ticks)
        {
            var target = State.UnitById(targetId);
            var caster = State.Units.FirstOrDefault(u => u.Alive && !u.IsHero);
            if (target == null || caster == null || App.Content.TryAbility(abilityId) == null) return -1;
            State.Timeline.Add(new TimelineEntry { Id = State.NextEntryId++, Kind = TimelineKind.Cast, OwnerId = caster.Id, AbilityId = abilityId, CasterTeam = Team.Enemy, TargetUnitId = target.Id, TargetTile = target.Pos, TotalCastTicks = ticks, RemainingTicks = ticks, IsCharge = true });
            SyncAll();
            return caster.Id;
        }

        private static void SetBorder(VisualElement e, float radius, float width, Color c)
        {
            e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = radius;
            e.style.borderTopWidth = e.style.borderBottomWidth = e.style.borderLeftWidth = e.style.borderRightWidth = width;
            e.style.borderTopColor = e.style.borderBottomColor = e.style.borderLeftColor = e.style.borderRightColor = c;
        }

        private void SyncBar()
        {
            var hero = State.UnitById(SelectedHeroId);
            for (int i = 0; i < slotViews.Count; i++)
            {
                var v = slotViews[i];
                var status = hero == null ? SlotStatus.CannotAct : BattleEngine.GetSlotStatus(State, hero.Id, i);
                var border = Palette.Text.WithA(0.75f);
                float alpha = 1f, iconAlpha = 1f;
                if (hero == null || i >= hero.UnlockedSlots)
                {
                    Ui.SetAbilitySlot(v, null, state: hero == null ? "" : "Locked");
                    alpha = 0.35f;
                }
                else if (i >= hero.Bar.Count || hero.Bar[i].IsEmpty)
                {
                    Ui.SetAbilitySlot(v, null, state: "—");
                    alpha = 0.5f;
                }
                else
                {
                    var sl = hero.Bar[i];
                    var a = BattleEngine.SlotAbility(State, hero, i);
                    bool silenced = status == SlotStatus.Silenced, charging = false;
                    if (sl.IsWeapon)
                    {
                        border = new Color(0.8f, 0.8f, 0.9f);
                        Ui.SetAbilitySlot(v, Art.ItemIcon(sl.WeaponId) ?? Art.Orb, silenced: silenced);
                    }
                    else if (sl.IsItem)
                    {
                        border = new Color(0.86f, 0.62f, 0.36f);
                        Ui.SetAbilitySlot(v, Art.ItemIcon(sl.ItemId) ?? Art.AbilityIcon(a) ?? Art.Orb, count: sl.Charges, silenced: silenced);
                    }
                    else if (a != null)
                    {
                        float sweep = 0f, mana = -1f; int left = 0;
                        if (a.Kind == AbilityKind.Skill)
                        {
                            border = new Color(0.4f, 0.9f, 0.5f);
                            if (status == SlotStatus.Cooldown && hero.Cooldowns.TryGetValue(a.Id, out left) && left > 0)
                                sweep = (float)left / Mathf.Max(left, a.CooldownTurns);
                        }
                        else
                        {
                            border = Palette.Mana;
                            // GG-T1820 (§3.1.0): the spell's own meter, as far as playback has filled it.
                            int full = State.ManaFull(hero, i);
                            if (full > 0) mana = ShownMana(hero.Id, i) / (float)full;
                        }
                        charging = mana >= 0f && mana < 1f;
                        Ui.SetAbilitySlot(v, Art.AbilityIcon(a) ?? Art.Orb, sweep, left, silenced: silenced, mana: mana, ready: mana >= 1f && !silenced);
                    }
                    else Ui.SetAbilitySlot(v, null, silenced: silenced);
                    if (silenced) border = new Color(1f, 0.25f, 0.25f);
                    // Charging replaces "unaffordable": the icon dims and its bar stays bright.
                    if (charging && (status == SlotStatus.Ready || status == SlotStatus.Unaffordable)) iconAlpha = 0.45f;
                    else if (status != SlotStatus.Ready) alpha = 0.45f;
                }
                bool targeting = i == TargetingSlot;
                if (targeting) border = Palette.Gold;
                v.EnableInClassList("slot--targeting", targeting);
                v.style.scale = StyleKeyword.Null; // scale belongs to the stylesheet (press / targeting states)
                v.style.borderTopColor = v.style.borderBottomColor = v.style.borderLeftColor = v.style.borderRightColor = border;
                v.style.opacity = alpha;
                v.Q("icon").style.opacity = iconAlpha;
            }
        }

        private int panelUnitId = -1;

        private void SyncPanel()
        {
            panel.Clear();
            var u = State.UnitById(panelUnitId >= 0 ? panelUnitId : SelectedHeroId);
            if (u == null || !u.Alive) u = State.UnitById(SelectedHeroId);
            if (refusal != null) PanelLine(panel, refusal, "small warn", "refusal");
            if (TargetingSlot >= 0 && State.UnitById(SelectedHeroId) is Unit caster)
            {
                var ab = BattleEngine.SlotAbility(State, caster, TargetingSlot);
                if (ab != null) { AbilityInfo(caster, TargetingSlot, ab); return; }
            }
            if (advice != null) PanelLine(panel, advice, "small gold", "advice");
            // A slot read without aiming (a hold, or a tap on one that can't aim) stays in the panel until a unit is read or
            // another hero is selected (GG-T1823).
            if (panelUnitId >= 0 || readSlotHero != SelectedHeroId) readSlot = -1;
            if (readSlot >= 0 && State.UnitById(SelectedHeroId) is Unit reader && BattleEngine.SlotAbility(State, reader, readSlot) is AbilityDef read)
            {
                InspectCard.Ability(panel, read, SlotState(reader, readSlot, read));
                return;
            }
            // A running lesson is the panel's content (§9.0, GG-T1827) until an aim, a held slot or a unit the player reads needs it.
            if (TutorialActive && panelUnitId < 0)
            {
                TutorialCard(panel);
                return;
            }
            if (u == null) return;
            // Idle (§9.0): one line for the selected hero, then its card and every skill's readout.
            if (u.Id == SelectedHeroId && u.IsHero && u.IsCommandable)
            {
                var idle = Ui.Text($"{u.Name}: tap a skill to aim it · hold one to read it · drag {u.Name} to move", "small gold", panel);
                idle.name = "idle-prompt";
                idle.style.whiteSpace = WhiteSpace.Normal;
                idle.style.marginBottom = Space.S8;
            }
            var row = Ui.El("row", panel);
            row.style.alignItems = Align.FlexStart;
            Ui.Thumb(u.Def.Id, row, Size.Thumb).style.marginRight = Space.S16;
            var col = Ui.El("col grow", row);
            col.style.flexShrink = 1;
            var top = Ui.El("row", col);
            top.style.alignItems = Align.Center;
            var nm = Ui.Text(u.Name, "h2 grow", top);
            nm.style.flexShrink = 1;
            if (u.IsIndependent && !u.Wanderer) Ui.RoleBadge(Ui.TamedRole, top); // GG-T303
            else if (u.Wanderer && !string.IsNullOrEmpty(u.Def.Role)) Ui.RoleBadge(u.Def.Role, top); // a wanderer is a hero class (GG-T342)
            else if (u.IsHero && !string.IsNullOrEmpty(u.Def.Role)) Ui.RoleBadge(u.Def.Role, top);
            Ui.Text($"Lv {u.Level}", "h2 gold", top);
            // What the Bestiary has learned from defeating its class gates the numbers (§25.11, GG-T290).
            int know = Knowledge(u);
            // FF-style plain numbers, no bar (owner 2026-11-21): "HEALTH"/"AETHER" are the same width, so those labels
            // line up instead of "HP"/"MP". A hero's MANA is how many of its spell meters are full (§3.1.0).
            var hpRow = Ui.El("row", col);
            Ui.Text("HEALTH", "tiny dim", hpRow).style.marginRight = Space.S8;
            Ui.Text(know >= BestiaryKnowledge.Stats ? $"{u.Hp}/{u.MaxHp}" : "???", "small", hpRow);
            if (u.IsHero)
            {
                var aetherRow = Ui.El("row", col);
                Ui.Text("MANA", "tiny dim", aetherRow).style.marginRight = Space.S8;
                int spells = 0, full = 0;
                for (int i = 0; i < u.Bar.Count; i++)
                {
                    int need = State.ManaFull(u, i);
                    if (need <= 0) continue;
                    spells++;
                    if (State.ManaIn(u.Id, i) >= need) full++;
                }
                Ui.Text(spells == 0 ? "no spells" : $"{full}/{spells} spells full", "small", aetherRow);
            }
            if (know >= BestiaryKnowledge.Stats) Ui.Text($"{Ui.StatLine(u.Effective)}  ·  EVA {GridGame.Core.Rules.Formulas.Evasion(u):0}%", "tiny dim", col); // GG-T297
            else if (know >= BestiaryKnowledge.Speed) Ui.Text($"SPD {u.Effective.Speed:0}  ·  defeat more to learn the rest", "tiny dim", col);
            if (!u.IsHero && know >= BestiaryKnowledge.Elements) Ui.Text(BestiaryKnowledge.ElementLine(u.Def), "tiny dim", col);
            if (!u.IsHero)
            {
                int n = BattleEngine.ActionsUntilTurn(State, u.Id);
                var cast = State.CastEntryOf(u.Id);
                Ui.Text(cast != null ? $"Charging {App.Content.TryAbility(cast.AbilityId)?.Name}!" : n < 0 ? "Frozen in time" : n == 0 ? "Acts after your next move!" : $"Acts in {n} moves", "small warn", col);
                if (know >= BestiaryKnowledge.Abilities)
                {
                    var mind = Ui.Text($"{EnemyPlanner.DescribeBrain(u.Def.Brain)} · {EnemyPlanner.Describe(EnemyPlanner.TargetingOf(u)).ToLowerInvariant()}", "tiny dim", col);
                    mind.name = "enemy-brain";
                    mind.style.whiteSpace = WhiteSpace.Normal;
                }
                if (u.HasPlate) { var pl = Ui.Text($"Armor plate {u.PlateHp}/{u.PlateMax} on its {SideName(u.Facing)} — strike from the sides, break it head-on, or use spells", "tiny", col); pl.style.whiteSpace = WhiteSpace.Normal; pl.style.color = Palette.Info; }
                if (u.IsCorpse) { var co = Ui.Text($"The body of a {App.Content.TryClass(u.CorpseOf)?.Name ?? "foe"} — blocks the tile, rots in {u.DecayIn} actions", "tiny dim", col); co.style.whiteSpace = WhiteSpace.Normal; }
                var kit = Ui.KitTraits(App.Content, u.Def);
                if (kit.Length > 0) { var kt = GlossaryText.Create(kit, "tiny gold", col); kt.name = "enemy-kit"; kt.style.whiteSpace = WhiteSpace.Normal; }
            }
            // What the flashing status slot on the tile means, one line per effect.
            var statusTurn = State.TurnEntryOf(u.Id);
            if (statusTurn != null && statusTurn.StunTicks > 0) StatusRow(col, "stunned", "!", Palette.Gold, "Stunned", "knocked back on the timeline — its turn icon is held");
            foreach (var b in u.Buffs)
            {
                var pip = StatusRow(col, b.Def.Id, string.IsNullOrEmpty(b.Def.Letter) ? b.Def.Name.Substring(0, 1) : b.Def.Letter,
                    Art.HexColor(b.Def.Color, b.Def.Kind == BuffKind.Buff ? Palette.Mana : new Color(0.8f, 0.4f, 0.9f)),
                    b.Def.Name, $"{Ui.BuffEffect(b.Def)} ({b.Remaining} {(b.Def.Unit == GridGame.Core.Content.DurationUnit.Turns ? "turns" : "s")} left)");
                AttachStatusInspect(pip, b.Def, b.Remaining);
            }
            if (u.IsHero && u.IsCommandable) SkillReadout(u);
        }

        private static string SideName(Coord f) => f.Y > 0 ? "front (facing down)" : f.Y < 0 ? "front (facing up)" : f.X > 0 ? "front (facing right)" : "front (facing left)";

        private static VisualElement StatusRow(VisualElement parent, string icon, string letter, Color color, string name, string what)
        {
            var row = Ui.El("row", parent);
            var pip = Ui.StatusPip(icon, letter, color, 40, row);
            pip.style.marginRight = Space.S8;
            var t = GlossaryText.Create(GameApp.I?.Content?.TryTerm(name) != null ? $"[{name}] — {what}" : $"{name} — {what}", "tiny", row);
            t.style.whiteSpace = WhiteSpace.Normal; t.style.flexShrink = 1;
            return pip;
        }

        /// <summary>
        /// The aimed ability in the description panel (§9.0, GG-T1823). While aiming: "Name: choose a target"; once a target
        /// is picked, the confirm card (on whom, the outcome preview, Cast / Cancel) takes the top. Under either, the full
        /// card: what it is, its cost, how and who it targets, its numbers and description, and the slot's live state.
        /// </summary>
        private void AbilityInfo(Unit hero, int slot, AbilityDef a)
        {
            bool confirm = PendingTarget.HasValue && TargetingSlot >= 0;
            var row = Ui.El("row", panel);
            row.name = "aim-card";
            row.style.alignItems = Align.FlexStart;
            row.style.marginBottom = Space.S16;
            var col = Ui.El("col grow", row);
            col.style.flexShrink = 1;
            if (!confirm)
            {
                var ask = Ui.Text($"{a.Name}: choose a target", "small gold", col);
                ask.name = "aim-prompt";
                ask.style.whiteSpace = WhiteSpace.Normal;
                Ui.Text("Tap a highlighted tile · tap the slot again to cancel", "tiny dim", col).style.whiteSpace = WhiteSpace.Normal;
            }
            else
            {
                var hits = TargetResolver.Collect(State, a, hero, PendingTarget.Value);
                string who = hits.Count == 0 ? (a.IsTeleport ? "Teleport to the marked tile" : "No one in the area")
                    : hits.Count == 1 ? (hits[0] == hero ? $"On {hero.Name} (self)" : $"On {hits[0].Name}")
                    : $"On {string.Join(", ", hits.Take(3).Select(h => h.Name))}{(hits.Count > 3 ? $" +{hits.Count - 3}" : "")}";
                var summary = Ui.Text(who, "small", col);
                summary.name = "confirm-summary";
                summary.style.whiteSpace = WhiteSpace.Normal;
                OutcomeLines(col, a);
                // Cast / Cancel side by side at the top of the card (GG-T391), so the two never scroll out of view.
                var btns = Ui.El("row", row);
                btns.style.flexShrink = 0;
                btns.style.marginLeft = Space.S16;
                string verb = a.Kind == AbilityKind.Item ? "Use" : a.Kind == AbilityKind.Spell ? "Cast" : "Go";
                var ok = RowIcon.IconButton(verb, RowKind.Confirm, ConfirmPending, "btn--primary", btns);
                ok.name = "confirm-action";
                var cancel = RowIcon.IconButton("Cancel", RowKind.Close, CancelByPlayer, "btn--small", btns);
                cancel.name = "confirm-cancel";
            }
            InspectCard.Ability(panel, a, SlotState(hero, slot, a));
        }

        // ─────────────────────────────────────────────────────────────────────────
        // The description panel (§9.0, GG-T1822): from a gutter under the ability bar to the safe bottom
        // ─────────────────────────────────────────────────────────────────────────

        private ScrollView logPage;
        /// <summary>Every combat line this battle (newest last, capped), so the Log survives a rebuild.</summary>
        private const int LogCap = 200;
        private readonly List<(string text, Color color)> logLines = new List<(string, Color)>();

        /// <summary>The panel holds one page: the aimed or read ability, a running lesson, or the unit. The Party · Lore · Log tab strip
        /// and window are gone (owner 2026-11-27): the Log page is built and filled off the layout and shown by the pause
        /// menu's Battle log (GG-T1825); a unit's lore lives in the hub's Bestiary and hero sheet (GG-T1826).</summary>
        private void BuildDescriptionPanel(VisualElement root)
        {
            var host = Ui.El("col description-panel", root);
            host.name = "description-panel";
            host.style.position = Position.Absolute;
            host.style.top = DescriptionTop; host.style.bottom = Space.S16; // left/right/top: ApplyStageLayout (the HUD column)
            var win = Ui.Window(null, host, "window--compact description-panel__window");
            win.name = "description-window";
            panel = InfoPage(win, "description");
            // The combat log keeps the old feed's name, so automation that looks for "feed" finds it on the pause menu's Battle log.
            logPage = InfoPage(null, "feed");
            foreach (var (text, color) in logLines) AddLogLine(text, color);
        }

        private static ScrollView InfoPage(VisualElement win, string name)
        {
            var sv = new ScrollView(ScrollViewMode.Vertical) { name = name };
            sv.AddToClassList("list");
            sv.AddToClassList("description-panel__page");
            sv.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            sv.touchScrollBehavior = ScrollView.TouchScrollBehavior.Clamped;
            win?.Add(sv);
            return sv;
        }

        /// <summary>The Party page's skill readout under a hero's card: each bar slot's icon, name, cost or recharge,
        /// and its description. Holding a row reads it in full, like holding the slot.</summary>
        private void SkillReadout(Unit hero)
        {
            if (hero == null || !hero.IsHero) return;
            var head = Ui.Text("Skills", "small gold", panel);
            head.style.marginTop = Space.S8;
            for (int i = 0; i < hero.Bar.Count && i < slotViews.Count; i++)
            {
                if (i >= hero.UnlockedSlots || hero.Bar[i].IsEmpty) continue;
                var sl = hero.Bar[i];
                var a = BattleEngine.SlotAbility(State, hero, i);
                if (a == null) continue;
                var row = Ui.El("row", panel);
                row.name = "skill" + i;
                row.style.alignItems = Align.FlexStart;
                row.style.marginBottom = Space.S8;
                var icon = Ui.El("menu-icon", row);
                icon.style.width = icon.style.height = Size.IconM;
                icon.style.marginRight = Space.S16;
                var tex = sl.IsItem ? Art.ItemIcon(sl.ItemId) : sl.IsWeapon ? Art.ItemIcon(sl.WeaponId) : null;
                icon.style.backgroundImage = new StyleBackground(tex ?? Art.AbilityIcon(a) ?? Art.Orb);
                var col = Ui.El("col grow", row);
                col.style.flexShrink = 1;
                var line = Ui.El("row", col);
                line.style.alignItems = Align.Center;
                var nm = Ui.Text(sl.IsWeapon ? App.Content.TryItem(sl.WeaponId)?.Name ?? a.Name : sl.IsItem ? App.Content.TryItem(sl.ItemId)?.Name ?? a.Name : a.Name, "small", line);
                nm.style.flexShrink = 1;
                if (a.Kind != AbilityKind.Skill && a.Cost > 0) Ui.CostPips(a.Cost, line, Size.Pip).style.marginLeft = Space.S8;
                int cd = hero.Cooldowns.TryGetValue(a.Id, out var c) ? c : 0;
                string when = sl.IsItem ? $"Item · {sl.Charges}/{sl.MaxCharges} left"
                    : a.Kind == AbilityKind.Skill ? (cd > 0 ? $"Skill · ready in {cd} turns" : a.CooldownTurns > 0 ? $"Skill · recharge {a.CooldownTurns} turns" : "Skill")
                    : a.CastTime > 0 ? $"Spell · charges {a.CastTime:0.#}s" : "Instant";
                var w = Ui.Text(when, "small gold", line);
                w.style.marginLeft = Space.S16;
                w.style.flexShrink = 0;
                if (!string.IsNullOrEmpty(a.Description))
                {
                    var d = GlossaryText.Create(a.Description, "small dim", col);
                    d.style.whiteSpace = WhiteSpace.Normal;
                }
                var ab = a;
                int idx = i;
                InspectCard.Attach(row, body => InspectCard.Ability(body, ab, SlotState(hero, idx, ab)));
            }
        }

        private void AddLogLine(string text, Color color)
        {
            if (logPage == null) return;
            var l = Ui.Text(text, "small", logPage);
            // GG-T525: a line's colour (a team's, an element's, mana blue) is lifted until it reads at 4.5:1 on the window.
            l.style.color = Palette.OnWindow(color);
            l.style.whiteSpace = WhiteSpace.Normal;
            l.style.marginBottom = Space.S4;
            while (logPage.contentContainer.childCount > LogCap) logPage.contentContainer.RemoveAt(0);
        }

        /// <summary>Newest at the bottom: pin the log to its end once the new line has been laid out.</summary>
        private void ScrollLogToEnd()
        {
            if (logPage == null) return;
            logPage.schedule.Execute(() => logPage.scrollOffset = new Vector2(0f, Mathf.Max(0f, logPage.contentContainer.layout.height - logPage.contentViewport.layout.height)));
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Commands
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>Not while a drag is in progress either: a second finger tapping GUARD or a slot mid-drag would
        /// commit that instead and silently drop the drag, leaving the previewed units out of place (GG-T70).
        /// CommitDrag clears <c>dragging</c> before it commits, so the drag's own command still goes through.</summary>
        public bool CanCommand() => !playing && !ended && !State.IsOver && !dragging && !Paused;

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

        /// <summary>Player-facing cancel (the confirm card's Cancel, a slot re-tap, Back): a soft "never mind" tone.</summary>
        public void CancelByPlayer()
        {
            if (TargetingSlot < 0) return;
            App.Audio.Sfx("ui.cancel", 0.8f);
            CancelTargeting();
            SyncBar();
            SyncPanel();
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
                if (board.Units.TryGetValue(id, out var v)) v.SetGlow(Color.white, 0f);
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
            if (TargetingSlot >= 0) { CancelByPlayer(); return false; }
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
            Ui.MenuRow("How to play", "", () => { Close(); awayOnSettings = true; App.Router.Push(new SettingsScreen()); }, w).KindIcon(RowKind.Bestiary);
            int retreatPct = (int)(BattleConfig.RetreatKeepFraction * 100);
            if (InCrucible)
                Ui.MenuRow("Leave the Crucible", "(keep all)", () => { Close();
                    Confirm.Show($"Leave the Crucible after {State.Wave} wave{(State.Wave == 1 ? "" : "s")}? You keep every coin, item and point of experience earned so far.", "Leave", Retreat); }, w).KindIcon(RowKind.Quit);
            else
                Ui.MenuRow("Retreat", $"(keep {retreatPct}%)", () => { Close();
                    Confirm.Show($"Retreat from battle? You'll keep {retreatPct}% of this fight's gold and loot — safer than risking a wipe.", "Retreat", Retreat); }, w).KindIcon(RowKind.Back);
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

        public override void Tick(float dt)
        {
            TickPrefabFx();
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
            if (dragging && !simulatingDrag && App.Settings.DragTimer)
            {
                dragTimeLeft -= dt;
                timerLabel.text = $"{Mathf.Max(0, dragTimeLeft):0.0}s";
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
                    if (use.Slot < slotViews.Count) App.StartCoroutine(Tween.Pop(slotViews[use.Slot], 1.12f, 0.35f));
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
