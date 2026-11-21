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
using MindAttic.Capture;
using MindAttic.Capture.Unity;
using MindAttic.Telemetry;
using MindAttic.Telemetry.Unity;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>
    /// The battle. Layout follows the bible HUD rows (§9): top bar · timeline + shield · action banner ·
    /// 6×8 board · ability bar with the mana pool at its right end (GG-T405) · Party · Lore · Log tabs. The simulation (BattleState) is the only truth;
    /// this screen turns input into commands and plays the resulting events back.
    /// </summary>
    public sealed partial class BattleScreen : GameScreen
    {
        public override string Id => "battle";
        /// <summary>GG-T456: one track per stage, drawn once from the region's shuffle bag (Meta.MusicPlaylist) and kept
        /// while overlays (level-ups, dialogue) come and go over the battle. A Crucible run plays MomentMusic.crucible when
        /// campaign.json lists any (GG-T458), else any region's battle track.</summary>
        public override string Music => music ??= (InCrucible ? App.Playlist.Moment(App.Content.Campaign, MusicPlaylist.Crucible) : null)
            ?? App.Playlist.NextBattle(App.Content.Campaign, App.Content.ThemeOf(stageId)) ?? "Crusade";
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
        private const float BoardTop = 450f;
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
        /// <summary>The mana pool (GG-T405): one widget at the ability row's right end instead of a row of 12 orbs: an
        /// orb with the spendable count large, the cap small beneath it, the next orb's charge as a thin gauge, and a
        /// "-N" ghost while a spell is aimed. Named "orbs" (the old row's name) so automation still finds it.</summary>
        private VisualElement pool;
        private ManaGlobe poolOrb;
        private Label poolCount, poolCap, poolGhost;
        /// <summary>How many orbs the pool currently shows (GG-T366). Mid-playback it lags State.Bank, which already
        /// holds the action's final count: each mint ticks it up as its orb lands and each spend ticks it down, so the
        /// pool never shows an orb before its fly-in or after it was spent.</summary>
        private int shownOrbs;
        /// <summary>The Party page of the info tabs (GG-T391): the selected unit, its skills, the ability/confirm card and
        /// the tutorial lesson all draw here (a ScrollView, so Add/Clear reach its content).</summary>
        private VisualElement panel;
        private VisualElement bannerViewport;
        private Coroutine bannerScrollRoutine;
        /// <summary>The banner band (GG-T391, GG-T405): a gutter under the timeline frame to a gutter above the board, so
        /// nothing in it (banner, billboard, drag timer) can touch either. Two lines of the stepped-down banner font fit.</summary>
        private const float BannerTop = TimelineY + TimelineFrameHeight / 2f + Space.S8, TopBarHeight = 110f;
        private const float BannerViewportHeight = BoardTop - Space.S8 - BannerTop;
        /// <summary>Room the drag-timer seconds take at the banner row's right end; the banner keeps the same inset on both
        /// sides so it stays centred and never runs under the timer (GG-T391).</summary>
        private const float TimerReserve = Size.Label + Space.S24;
        // The HUD rows under the board, top to bottom, a gutter apart (GG-T391, GG-T405): the ability bar (five slots and
        // the mana pool), then the info tabs, which stretch to the safe bottom and so alone absorb a taller stage (§26.3).
        private static float BoardBottom => BoardTop + BoardView.Tile * BattleConfig.Rows;
        private static float BarTop => BoardBottom + Space.S16;
        /// <summary>GG-T523: on a stage wide enough (2:3, 3:4 tablets) the ability bar leaves the row under the board and
        /// stands beside it as two columns, slots 1-3 on the left and 4-5 + the mana pool on the right, so the info tabs
        /// get the bar's 196 px and the tutorial card room to breathe. A column must still fit a cell of
        /// <see cref="SideBarMinWidth"/> (the slot height + its gutters) between the stage edge and the board.</summary>
        private const float SideBarMinWidth = Size.Slot + Space.S32;
        private static float SideBarWidth => BoardLeft - Space.S16 - Fit.Edge;
        private static bool SideBars => SideBarWidth >= SideBarMinWidth;
        /// <summary>A side-column cell: a little taller than the row's (its width is the column's, well over a slot).</summary>
        private const float SideCellHeight = Size.Slot + Space.S32;
        /// <summary>The info tabs start a target gap (POL-C3, 24) under whatever is above them: the ability row, or the
        /// board itself when the bar stands beside it.</summary>
        private static float InfoTabsTop => (SideBars ? BoardBottom : BarTop + Size.Slot) + Space.S24;
        private Label banner, waveLabel, coinLabel, timerLabel;
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
        // MindAttic.Capture: how the battle ended, set when BattleEndedEvent plays (BattleScreen.Playback.cs)
        private BattleEndedEvent lastEnded;

        public BattleScreen(string stageId) { this.stageId = stageId; }
        public BattleScreen(BattleSetup setup) { setupOverride = setup; stageId = setup.Stage.Id; }

        /// <summary>Seeds the autoplay bot's RNG (null = the clock). `-demo -endless` sets it to the encounter seed
        /// (GG-T64) so the bot decides exactly as <c>EndlessEncounter.Bot(seed)</c> does in a headless replay.</summary>
        public int? BotSeed { get; set; }

        /// <summary>The end of this battle is committed to App.Save and routed to the results screen. A battle built
        /// from an explicit setup normally just pops back instead — except under `-demo -endless` (GG-T57), whose
        /// setup is a generated party fought on a throwaway slot -1 save, and whose loop runs through the results.</summary>
        private bool CommitsToSave => setupOverride == null || DemoMode.Endless;

        /// <summary>A still-open pause veil lives on the global Overlay (see OpenMenu), which outlives this screen's
        /// own root — remove it explicitly so it doesn't linger on top of whatever screen comes next.</summary>
        public override void OnHidden()
        {
            GameApp.StageResized -= OnStageResized;
            DisposePrefabFx();
            if (pauseVeil != null) { pauseVeil.RemoveFromHierarchy(); pauseVeil = null; ResumeBattle(); }
            // The billboard lives on the Overlay too; without this, each battle left one behind (lines from the
            // last ~1.6 s showing over the PostBattle screen) and they piled up battle after battle (GG-T70).
            billboard?.RemoveFromHierarchy();
            billboard = null;
            if (Coach.Active && (Coach.LessonId ?? "").StartsWith("battle-")) Coach.Cancel(); // GG-T539: a lesson leaves with its battle
            mintTagHost?.RemoveFromHierarchy(); // GG-T405: the globe's source tags live on the Overlay too
            mintTagHost = null;
            mintTags.Clear();
            // Build() rebuilds `banner` fresh when the player backs out of "How to play" onto this same
            // instance (see the Build() comment); an in-flight scroll left running would keep animating
            // whatever `banner` now points to using timing measured against the old one (GG-T70).
            if (bannerScrollRoutine != null) { App.StopCoroutine(bannerScrollRoutine); bannerScrollRoutine = null; }
        }

        private bool Colorblind => App.Settings.ColorblindMode;
        /// <summary>This battle is a Crucible run (§22.10): open-ended wave count, keep-everything retreat.</summary>
        private bool InCrucible => Crucible.IsStage(stageId);
        /// <summary>The campaign's first stage (the tutorial battle, The Tithe Moors I).</summary>
        private bool IsFirstStage => App.Content.CampaignStages().FirstOrDefault()?.Id == stageId;
        /// <summary>GG-T542: the tutorial battle's fixed seed: the same board, the same foes' choices, every time.</summary>
        private const int TutorialSeed = 542;
        private bool ReduceMotion => App.Settings.ReduceMotion;

        protected override void Build(VisualElement root)
        {
            var initial = new List<BattleEvent>();
            // Build runs again on this same instance when the player backs out of "How to play" (the router
            // re-mounts the screen). Every per-build view cache must start empty, or the HUD keeps writing to
            // the previous build's detached elements (every slot read "Locked", orbs empty, timeline icons
            // gone, coin label blank) — and the once-per-battle work (tutorial, opening playback, capture
            // event) must not run a second time (GG-T70).
            bool firstBuild = State == null;
            slotViews.Clear();
            pool = null;
            timelineIconViews.Clear();
            shownCoins = -1;
            if (firstBuild)
            {
                // GG-T539: Settings › How to play › Replay tutorials brings the lessons back on the first stage too.
                bool tutorial = setupOverride == null && !InCrucible && App.Save != null && !App.Save.TutorialDone && (App.Save.HighestClearedStage < 0 || IsFirstStage)
                    && !CommandLine.Has("-autobattle") && !DemoMode.Active && !TestMode.Active;
                // A Crucible run is seeded by the save (§22.10) so the same save replays the same gauntlet; the tutorial battle
                // always plays the same (GG-T542), so its lessons' set pieces land the same way every time.
                int seed = tutorial ? TutorialSeed : InCrucible && App.Save != null ? Crucible.SeedFor(App.Save) : Environment.TickCount;
                var setup = setupOverride ?? BattleFactory.Build(App.Content, App.Save, stageId, seed, App.Settings?.Difficulty?.Level ?? DifficultyTable.Default);
                setup.Tutorial = tutorial;
                if (setupOverride == null && !tutorial) Wanderers.Apply(App.Content, App.Save, setup); // GG-T342: maybe a wanderer to win over
                if (setupOverride == null && DevNextWanderer != null) { setup.Wanderer = new WandererSpec { Class = DevNextWanderer, Wave = 0 }; DevNextWanderer = null; }
                State = BattleEngine.Create(App.Content, setup, initial);
                recorder = new BattleRecorder(State, setup, "player", initial);
                // Every battle is tagged with the dial and the multipliers it applied (§22.9, GG-T309).
                var diff = DifficultyTable.At(setup.Difficulty);
                TelemetryUnity.Begin("GridGameGaiden", seed: setup.Seed, contentVersion: Application.version,
                    tags: new TelemetryData
                    {
                        ["stage"] = State.Stage.Id, ["source"] = "player",
                        ["difficulty"] = setup.Difficulty,
                        ["diffEnemyHp"] = diff.EnemyHp, ["diffEnemyDamage"] = diff.EnemyDamage, ["diffEnemyLevel"] = diff.EnemyLevelOffset,
                        ["diffRewardGold"] = diff.RewardGold, ["diffRewardXp"] = diff.RewardXp, ["diffItemDropRate"] = diff.DropChance,
                    });
                BeginEncounterTelemetry(setup, initial, diff); // GG-T59
            }

            var bg = Ui.El("fill", root);
            bg.style.backgroundImage = new StyleBackground(Art.Background(State.Stage.Theme, Math.Abs(stageId.GetHashCode() % 4096))); // Art wraps it over the theme's own count (GG-T344)
            bg.style.unityBackgroundImageTintColor = new Color(0.78f, 0.78f, 0.86f);
            shake = Ui.El("fill", root);

            BuildTopBar(shake);
            BuildTimeline(shake);
            bannerViewport = Ui.El("", shake);
            bannerViewport.name = "banner-viewport";
            bannerViewport.style.position = Position.Absolute;
            bannerViewport.style.left = TimerReserve; bannerViewport.style.right = TimerReserve; bannerViewport.style.top = BannerTop; bannerViewport.style.height = BannerViewportHeight;
            bannerViewport.style.overflow = Overflow.Hidden;
            banner = Ui.Text("", "h2 shadow", bannerViewport);
            banner.style.position = Position.Absolute;
            banner.style.left = Space.S0; banner.style.right = Space.S0; banner.style.top = Space.S0;
            banner.style.unityTextAlign = TextAnchor.UpperCenter;
            banner.style.whiteSpace = WhiteSpace.Normal;

            boardHost = Ui.El("", shake);
            boardHost.style.position = Position.Absolute;
            boardHost.style.top = BoardTop;
            board = new BoardView(boardHost, State.Stage.Theme);
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

            BuildAbilityBar(shake);
            BuildInfoTabs(shake);

            ApplyStageLayout();
            foreach (var u in State.Units) if (u.Alive) board.Add(u);
            SelectedHeroId = State.Units.FirstOrDefault(u => u.IsCommandable && u.Alive)?.Id ?? -1;
            if (firstBuild && State.Tutorial) TutorialStep = 0;
            SyncAll();
            if (!firstBuild) return;
            App.StartCoroutine(Play(initial, false));
            if (CommandLine.Has("-autobattle") || DemoMode.Active || TestMode.Active) AutoPlay = true;
            else if (TutorialStep < 0 && App.Save != null && App.Save.Flags.Add("hint:inspect"))
            {
                // GG-T252: a player who skipped the tutorial still learns the gesture once.
                App.Persist();
                panel.schedule.Execute(() => ShowBanner("Tip: tap or hold any hero, foe, slot or icon to read it")).StartingIn(2500);
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
            bar.style.left = Fit.Edge; bar.style.right = Fit.Edge; bar.style.top = Space.S32; bar.style.height = TopBarHeight;
            Ui.Button("II", OpenMenu, "btn--small btn--icon", bar).name = "menu";
            var mid = Ui.El("col grow", bar);
            mid.style.alignItems = Align.Center;
            Ui.Text(State.Stage.Name, "h2 shadow", mid);
            waveLabel = Ui.Text("", "small shadow dim", mid);
            coinLabel = Ui.Text("", "h2 gold shadow", bar);
            coinLabel.style.width = Fit.W(17f); coinLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            timerLabel = Ui.Text("", "h1 shadow warn", root);
            timerLabel.style.position = Position.Absolute;
            // The drag timer keeps to the banner band (GG-T405): its own height, centred in it, never over the timeline or board.
            timerLabel.style.right = Space.S48; timerLabel.style.top = BannerTop; timerLabel.style.height = BannerViewportHeight;
            timerLabel.style.unityTextAlign = TextAnchor.MiddleRight;
        }

        /// <summary>Re-applies the positions that depend on the stage width (build time and on resize).</summary>
        private void ApplyStageLayout()
        {
            if (boardHost == null) return;
            boardHost.style.left = BoardLeft;
            cutInLayer.style.left = -BoardLeft;
            // The rows under the board (ability bar + mana pool, info tabs) keep to the centred content column, so on a
            // tablet they don't stretch past usefulness; the board centres and the timeline spans the stage (GG-T398).
            float inset = Fit.Inset;
            LayoutAbilityBar(inset);
            if (Root?.Q("info-tabs") is VisualElement tabs)
            {
                tabs.style.left = tabs.style.right = inset + Fit.Edge;
                tabs.style.top = InfoTabsTop;
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

        /// <summary>The ability row (named "abilitybar"; on a wide stage the right-hand column) and, on a wide stage only,
        /// the left-hand column that takes slots 1-3 (GG-T523).</summary>
        private VisualElement abilityBar, abilityBarLeft;

        /// <summary>Lays the ability cells out as one row under the board, or as two columns beside it when the stage is
        /// wide enough (<see cref="SideBars"/>, GG-T523), moving slots 1-3 between the row and the left column.</summary>
        private void LayoutAbilityBar(float inset)
        {
            if (abilityBar == null || abilityBarLeft == null) return;
            bool side = SideBars;
            var bar = abilityBar;
            var left = abilityBarLeft;
            if (side)
            {
                for (int i = 0; i < 3 && i < slotViews.Count; i++) if (slotViews[i].parent != left) left.Add(slotViews[i]);
                float w = SideBarWidth;
                foreach (var col in new[] { left, bar })
                {
                    col.style.flexDirection = FlexDirection.Column;
                    col.style.justifyContent = Justify.FlexEnd; // bottom-aligned with the board, nearest the thumbs
                    col.style.alignItems = Align.Stretch;
                    col.style.top = BoardTop; col.style.height = BoardView.Tile * BattleConfig.Rows;
                    col.style.width = w;
                }
                left.style.left = Fit.Edge; left.style.right = StyleKeyword.Auto;
                bar.style.right = Fit.Edge; bar.style.left = StyleKeyword.Auto;
                left.style.display = DisplayStyle.Flex;
            }
            else
            {
                for (int i = Mathf.Min(3, slotViews.Count) - 1; i >= 0; i--) if (slotViews[i].parent != bar) bar.Insert(0, slotViews[i]);
                bar.style.flexDirection = FlexDirection.Row;
                bar.style.justifyContent = Justify.FlexStart;
                bar.style.alignItems = StyleKeyword.Null;
                bar.style.top = BarTop; bar.style.height = Size.Slot;
                bar.style.width = StyleKeyword.Auto;
                bar.style.left = bar.style.right = inset + Space.S16;
                left.style.display = DisplayStyle.None;
            }
            foreach (var cell in bar.Children().Concat(left.Children())) LayCell(cell, side);
        }

        /// <summary>A cell's share of its row (an even split, a gutter either side) or its place in a column (full width, a
        /// fixed height, a gutter above and below).</summary>
        private static void LayCell(VisualElement cell, bool column)
        {
            cell.style.flexGrow = column ? 0 : 1;
            cell.style.flexBasis = column ? (StyleLength)StyleKeyword.Auto : 0;
            cell.style.height = column ? SideCellHeight : Size.Slot;
            cell.style.marginLeft = cell.style.marginRight = column ? Space.S0 : Space.S8;
            cell.style.marginTop = cell.style.marginBottom = column ? Space.S8 : Space.S0;
        }

        private void BuildAbilityBar(VisualElement root)
        {
            abilityBarLeft = Ui.El("col", root);
            abilityBarLeft.name = "abilitybar-left";
            abilityBarLeft.style.position = Position.Absolute;
            abilityBarLeft.style.display = DisplayStyle.None;
            var bar = abilityBar = Ui.El("row", root);
            bar.name = "abilitybar";
            bar.style.position = Position.Absolute;
            // Five slots and the mana pool share the row evenly (GG-T405); even gutters on the space scale, and the
            // cells, not the gaps, take a wider stage (POL-C1/C8). Row or columns: LayoutAbilityBar (GG-T523).
            bar.style.top = BarTop; bar.style.height = Size.Slot; // left/right: ApplyStageLayout (the HUD column)
            bar.style.justifyContent = Justify.FlexStart;
            for (int i = 0; i < 5; i++)
            {
                int idx = i;
                var slot = HudCell(bar);
                slot.name = "slot" + i;
                slot.AddToClassList("slot");
                Ui.Pressable(slot);
                AttachSlotInspect(slot, idx);
                slot.RegisterCallback<ClickEvent>(_ => OnSlot(idx));
                var icon = Ui.El("menu-icon", slot); icon.name = "icon"; icon.style.marginRight = Space.S0; icon.style.width = icon.style.height = Size.IconL;
                var name = Ui.Text("", "tiny", slot); name.name = "name"; name.style.unityTextAlign = TextAnchor.MiddleCenter; name.style.whiteSpace = WhiteSpace.Normal; name.style.maxWidth = Length.Percent(94);
                var cost = Ui.El("row", slot); cost.name = "cost"; cost.style.height = Size.Pip;
                slotViews.Add(slot);
            }
            BuildManaPool(bar);
        }

        /// <summary>One cell of the ability row: a window-framed column that takes an even share of the row.</summary>
        private static VisualElement HudCell(VisualElement bar)
        {
            var cell = Ui.El("col center", bar);
            cell.style.flexGrow = 1; cell.style.flexBasis = 0; cell.style.height = Size.Slot;
            cell.style.marginLeft = cell.style.marginRight = Space.S8;
            cell.style.borderTopLeftRadius = cell.style.borderTopRightRadius = cell.style.borderBottomLeftRadius = cell.style.borderBottomRightRadius = 18;
            cell.style.borderTopWidth = cell.style.borderBottomWidth = cell.style.borderLeftWidth = cell.style.borderRightWidth = 4;
            cell.style.backgroundImage = new StyleBackground(Art.Window);
            return cell;
        }

        /// <summary>The mana pool (owner 2026-11-17, GG-T405: "a single 'mana pool' on the far right ... instead of keeping
        /// an entire row of 12 orbs, that way the manapool can grow but always represent a growing number"; then "a glass
        /// sphere filled with blue liquid", "like Diablo or PoE2"): the ability row's sixth cell. A <see cref="ManaGlobe"/>
        /// fills to count / cap with the count in outlined white and "MANA" on it, the cap small beneath, gold when full;
        /// the ring round its bezel is the next orb forming (the mana charge, §3.1.9); an aimed spell lightens the band it
        /// will drain and shows a "-N" ghost. Mints fly into it from their source, spends fly out (BattleScreen.Playback).
        /// Holding it explains it (ManaPoolCard).</summary>
        private void BuildManaPool(VisualElement bar)
        {
            pool = HudCell(bar);
            pool.name = "orbs";
            pool.AddToClassList("mana-pool");
            pool.style.borderTopColor = pool.style.borderBottomColor = pool.style.borderLeftColor = pool.style.borderRightColor = Palette.Mana;
            pool.pickingMode = PickingMode.Position; // hold to read what it is and what fills it
            InspectCard.Attach(pool, ManaPoolCard, tap: true); // GG-T450: its tap does nothing else, so it reads
            poolOrb = new ManaGlobe { name = "orb0", ReduceMotion = ReduceMotion }; // the globe (the old row's first slot name)
            pool.Add(poolOrb);
            poolOrb.style.width = poolOrb.style.height = Size.Thumb;
            poolOrb.style.alignItems = Align.Center; poolOrb.style.justifyContent = Justify.Center;
            // The count in outlined white over the liquid, "MANA" small beneath it, so it says what it is.
            poolCount = Ui.Text("0", "mana-pool__count", poolOrb);
            poolCount.name = "orbs-count";
            poolCount.pickingMode = PickingMode.Ignore;
            poolCount.style.fontSize = TypeScale.H2; Fonts.UseHeavy(poolCount);
            poolCount.style.unityTextAlign = TextAnchor.MiddleCenter;
            poolCount.style.unityTextOutlineWidth = Size.Hairline; poolCount.style.unityTextOutlineColor = Palette.Ink;
            var word = Ui.Text("MANA", "", poolOrb);
            word.name = "orbs-label";
            word.pickingMode = PickingMode.Ignore;
            word.style.fontSize = TypeScale.Badge; Fonts.UseHeavy(word);
            word.style.color = Palette.Info;
            word.style.unityTextOutlineWidth = Size.Hairline; word.style.unityTextOutlineColor = Palette.Ink;
            word.style.marginTop = -Space.S8;
            poolCap = Ui.Text("", "shadow", pool);
            poolCap.name = "orbs-cap";
            poolCap.pickingMode = PickingMode.Ignore;
            poolCap.style.fontSize = TypeScale.Small;
            poolCap.style.unityTextAlign = TextAnchor.MiddleCenter;
            poolGhost = Ui.Text("", "h3 shadow mana-pool__ghost", pool);
            poolGhost.name = "orbs-cost";
            poolGhost.pickingMode = PickingMode.Ignore;
            poolGhost.style.position = Position.Absolute;
            poolGhost.style.top = Space.S4; poolGhost.style.right = Space.S4;
            poolGhost.style.fontSize = TypeScale.Body; Fonts.UseHeavy(poolGhost);
            poolGhost.style.opacity = 0f;
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
                v.SetActed(u.IsHero && u.ActedThisWindow); // GG-T308: acted this window
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
            SyncOrbs();
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

        // ── Cast countdowns on the target's frame ──────────────────────────────

        private Dictionary<int, float> lastAt;
        private int castCycle;

        /// <summary>
        /// Cast time shows on the caster (owner direction 2026-10-10): every pending cast traces a radial countdown
        /// around its caster's frame in the spell's colour; its target carries the spell chip, so "who casts what on
        /// whom" reads on the board. A unit with several pending casts (or targeted by several) cycles every 1.2 s.
        /// </summary>
        private void SyncCastRings(Dictionary<int, float> at)
        {
            var casting = new Dictionary<int, List<(Color color, float left)>>();
            var targeted = new Dictionary<int, List<(Color color, AbilityDef a)>>();
            var rides = State.Timeline.Where(e => e.Kind == TimelineKind.Cast)
                .Select(e => (e.Id, e.OwnerId, e.AbilityId, e.TargetUnitId, e.TargetTile, U: BattleEngine.BarU(State, e), Span: CastSpanU(e.TotalCastTicks + e.AccumulatedDelayTicks))).ToList();
            foreach (var g in castGhosts.Values)
                if (rides.All(r => r.Id != g.EntryId)) rides.Add((g.EntryId, g.CasterId, g.AbilityId, g.TargetUnitId, g.TargetTile, g.U, g.Span));
            foreach (var e in rides)
            {
                // GG-T525: under Colorblind palette a cast ring says whose spell it is (blue hero / orange foe) instead of
                // its element's hue, which several deficiencies can't tell apart.
                var color = Colorblind && State.UnitById(e.OwnerId) is Unit caster ? Palette.TeamCast(caster.Team == Team.Hero) : Art.SpellColor(e.AbilityId);
                var a = App.Content.TryAbility(e.AbilityId);
                float u = at != null && at.TryGetValue(e.Id, out var x) ? x : e.U;
                if (castGhosts.TryGetValue(e.Id, out var ghost)) ghost.U = u;
                if (!casting.TryGetValue(e.OwnerId, out var cl)) casting[e.OwnerId] = cl = new List<(Color, float)>();
                cl.Add((color, Mathf.Clamp01(u / Mathf.Max(0.0001f, e.Span)))); // the share of the cast still to run
                int target = e.TargetUnitId;
                if (State.UnitById(target) is not Unit tu || !tu.Alive) target = State.UnitAt(e.TargetTile)?.Id ?? -1;
                if (target < 0) continue;
                if (!targeted.TryGetValue(target, out var tl)) targeted[target] = tl = new List<(Color, AbilityDef)>();
                tl.Add((color, a));
            }
            foreach (var kv in board.Units)
            {
                if (casting.TryGetValue(kv.Key, out var cl)) { var (c, left) = cl[castCycle % cl.Count]; kv.Value.SetCastRing(c, left); kv.Value.SetCastBar(c, 1f - left); }
                else { kv.Value.SetCastRing(null, 0f); kv.Value.SetCastBar(null, 0f); }
                if (targeted.TryGetValue(kv.Key, out var tl))
                {
                    var (c, a) = tl[castCycle % tl.Count];
                    kv.Value.SetCastChip(c, Art.AbilityIcon(a), a?.Name);
                }
                else kv.Value.SetCastChip(null, null, null);
            }
        }

        /// <summary>The shared 1.2 s beat: cast rings, overlapping timeline circles and status slots each show their next item.</summary>
        private void CycleCastRings()
        {
            if (Paused) return; // the 1.2 s beat is game time too (GG-T126)
            castCycle++;
            if (State == null || board == null || lastAt == null) return;
            SyncCastRings(lastAt);
            LayoutIcons(lastAt);
            SyncTimelineStatus();
            foreach (var v in board.Units.Values) v.NextStatus();
        }

        /// <summary>Dev hook (automation): queue a pending enemy cast on a unit so the countdown ring can be checked.</summary>
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

        /// <param name="upTo">Mid-playback (GG-T112): show only this many orbs, so orbs minted in one action tick the pool
        /// up one by one as each lands instead of all at once; -1 shows the bank as it is. Also used by
        /// <see cref="OrbsSpentEvent"/> playback (GG-T130) to show the count right after that spend, not the command's
        /// end-of-action <see cref="BattleState.Bank"/> count, which may already include orbs the same action mints later.</param>
        private void SyncOrbs(int upTo = -1)
        {
            int filled = upTo >= 0 ? Mathf.Min(upTo, State.Bank.Count) : State.Bank.Count;
            shownOrbs = filled; // GG-T366: the pool shows what has landed so far, not the final count
            shownCharge = State.ManaCharge;
            if (pool == null) return;
            if (!playing) SetChargeBubble(State.ManaCharge / (float)BattleConfig.ManaChargePerOrb);
            ShowPoolCount(filled);
            SyncPoolPreview();
        }

        /// <summary>Holding the mana globe (GG-T405, owner: "it should be obvious what it is and what is filling it up"):
        /// what the pool is, and every way it fills, as the rules have them (BattleRunner.MintOrb and AddCharge callers).</summary>
        private bool ManaPoolCard(VisualElement body)
        {
            int cap = State.Bank.Capacity;
            return InspectCard.Explain(body, Art.Orb, "Mana pool", $"{State.Bank.Count}/{cap} orbs", Palette.Mana,
                "The party's shared mana. Every spell is paid in orbs from here; skills and items cost none. It holds " + cap + " orbs; an orb earned while it is full is lost.",
                new[]
                {
                    "Pincers fill the ring round the globe by the HP they take (fewer heroes and longer chains fill it faster); a full ring becomes an orb.",
                    "Critical hits, interrupting a foe's charge, the power amplifier and Mana supporters add to the ring too.",
                    "Guard trades the turn time you give up for charge (10 per second, up to 40).",
                    "A quick move banks the drag timer's spare seconds as orbs at the next foe's turn.",
                    "Mana pickups on the board, Mana Potions and Ethers, Steal and Mug, and Meditate or Aether Pour give whole orbs.",
                    "Some gear starts the battle with orbs, an expedition carries orbs over, and some regions' rules grant them.",
                },
                "Kills alone earn nothing: it is the pincer's damage that fills the ring. The Log tab lists every orb and where it came from.");
        }

        /// <summary>The pool's numbers: the globe's level and the count on it, and the cap beneath, gold when full.</summary>
        private void ShowPoolCount(int count)
        {
            if (pool == null) return;
            int cap = State.Bank.Capacity;
            poolCount.text = count.ToString();
            poolCap.text = "/" + cap;
            bool full = count >= cap;
            poolOrb.Target = cap > 0 ? Mathf.Clamp01(count / (float)cap) : 0f;
            poolCount.style.color = count > 0 ? Palette.White : Palette.TextDim;
            poolCap.style.color = full ? Palette.Gold : Palette.TextSub;
            pool.EnableInClassList("mana-pool--full", full);
        }

        /// <summary>The pending-cost preview (GG-T405): while a spell is aimed the count dims and a "-N" ghost fades in over
        /// the pool's corner (red if the bank can't pay it); both fade back when the aim ends.</summary>
        private void SyncPoolPreview()
        {
            if (pool == null) return;
            int cost = 0;
            var hero = State.UnitById(SelectedHeroId);
            if (TargetingSlot >= 0 && hero != null && TargetingSlot < hero.Bar.Count)
            {
                var sl = hero.Bar[TargetingSlot];
                var a = BattleEngine.SlotAbility(State, hero, TargetingSlot);
                if (a != null && a.Kind == AbilityKind.Spell && !sl.IsItem && !sl.IsWeapon) cost = a.Cost;
            }
            if (cost > 0)
            {
                poolGhost.text = "-" + cost;
                poolGhost.style.color = cost <= shownOrbs ? Palette.Gold : Palette.Bad;
            }
            poolGhost.style.opacity = cost > 0 ? 1f : 0f;
            poolOrb.Preview = cost > 0 && State.Bank.Capacity > 0 ? Mathf.Min(cost, shownOrbs) / (float)State.Bank.Capacity : 0f;
            poolCount.style.opacity = cost > 0 ? 0.55f : 1f;
        }

        private void SyncBar()
        {
            var hero = State.UnitById(SelectedHeroId);
            for (int i = 0; i < slotViews.Count; i++)
            {
                var v = slotViews[i];
                var icon = v.Q("icon");
                var name = v.Q<Label>("name");
                var cost = v.Q("cost");
                cost.Clear();
                icon.style.backgroundImage = StyleKeyword.None;
                var status = hero == null ? SlotStatus.CannotAct : BattleEngine.GetSlotStatus(State, hero.Id, i);
                var border = Palette.Text.WithA(0.75f);
                float alpha = 1f;
                if (hero == null || i >= hero.UnlockedSlots)
                {
                    name.text = hero == null ? "" : "Locked";
                    alpha = 0.35f;
                }
                else if (i >= hero.Bar.Count || hero.Bar[i].IsEmpty)
                {
                    name.text = "—";
                    alpha = 0.5f;
                }
                else
                {
                    var sl = hero.Bar[i];
                    var a = BattleEngine.SlotAbility(State, hero, i);
                    if (sl.IsWeapon)
                    {
                        var w = App.Content.TryItem(sl.WeaponId);
                        name.text = $"⭾ {w?.Name}";
                        border = new Color(0.8f, 0.8f, 0.9f);
                        icon.style.backgroundImage = new StyleBackground(Art.ItemIcon(sl.WeaponId) ?? Art.Orb);
                    }
                    else if (sl.IsItem)
                    {
                        var item = App.Content.TryItem(sl.ItemId);
                        name.text = $"{item?.Name}\n{sl.Charges}/{sl.MaxCharges}";
                        border = new Color(0.86f, 0.62f, 0.36f);
                        icon.style.backgroundImage = new StyleBackground(Art.ItemIcon(sl.ItemId) ?? Art.AbilityIcon(a) ?? Art.Orb);
                    }
                    else if (a != null)
                    {
                        name.text = a.Name;
                        icon.style.backgroundImage = new StyleBackground(Art.AbilityIcon(a) ?? Art.Orb);
                        if (a.Kind == AbilityKind.Skill)
                        {
                            border = new Color(0.4f, 0.9f, 0.5f);
                            if (status == SlotStatus.Cooldown) name.text = $"{a.Name}\nCD {hero.Cooldowns[a.Id]}";
                        }
                        else
                        {
                            border = Palette.Mana;
                            Ui.CostPips(a.Cost, cost, 30);
                        }
                    }
                    if (status == SlotStatus.Silenced) { border = new Color(1f, 0.25f, 0.25f); name.text += "\nSilenced"; }
                    if (status != SlotStatus.Ready) alpha = 0.45f;
                }
                bool targeting = i == TargetingSlot;
                if (targeting) border = Palette.Gold;
                v.EnableInClassList("slot--targeting", targeting);
                v.style.scale = StyleKeyword.Null; // scale belongs to the stylesheet (press / targeting states)
                v.style.borderTopColor = v.style.borderBottomColor = v.style.borderLeftColor = v.style.borderRightColor = border;
                v.style.opacity = alpha;
                // A one-word name ("Taunt") never hyphen-breaks across lines; two-word names still wrap (GG-T408).
                name.style.whiteSpace = (name.text ?? "").IndexOf(' ') < 0 ? WhiteSpace.NoWrap : WhiteSpace.Normal;
            }
            SyncPoolPreview(); // the aimed spell's "-N" ghost follows the bar's targeting state (GG-T405)
        }

        private int panelUnitId = -1;

        private void SyncPanel()
        {
            panel.Clear();
            ApplyInfoTab();
            var u = State.UnitById(panelUnitId >= 0 ? panelUnitId : SelectedHeroId);
            if (u == null || !u.Alive) u = State.UnitById(SelectedHeroId);
            SyncLore(u);
            // The lessons live in their own Tutorial tab (GG-T449, owner: "built into the tabs at the bottom, not another
            // popup over the top of the tabs"): Party keeps showing the unit, and a new lesson brings its tab forward once.
            tutorialPage?.Clear();
            if (TutorialActive && tutorialPage != null)
            {
                if (shownLesson != TutorialStep) { shownLesson = TutorialStep; if (TargetingSlot < 0 && infoTab != InfoTabTutorial) ShowInfoTab(InfoTabTutorial); }
                TutorialCard(tutorialPage);
                // Its Next/Skip sit side by side at the top, so the lesson's buttons never scroll out of the short page.
                var tb = tutorialPage.Q("tutorial-skip")?.parent;
                if (tb != null) { tb.style.flexDirection = FlexDirection.Row; tb.style.alignSelf = Align.FlexStart; }
            }
            else if (infoTab == InfoTabTutorial) ShowInfoTab(InfoTabParty);
            if (TargetingSlot >= 0 && State.UnitById(SelectedHeroId) is Unit caster)
            {
                var ab = BattleEngine.SlotAbility(State, caster, TargetingSlot);
                if (ab != null) { AbilityInfo(ab); return; }
            }
            if (u == null) return;
            var row = Ui.El("row", panel);
            row.style.alignItems = Align.FlexStart;
            Ui.Thumb(u.Def.Id, row, Size.Thumb).style.marginRight = Space.S16;
            var col = Ui.El("col grow", row);
            col.style.flexShrink = 1;
            var top = Ui.El("row", col);
            top.style.alignItems = Align.Center;
            var nm = Ui.Text(u.Name, "h2 grow", top);
            nm.style.flexShrink = 1;
            if (u.IsIndependent && !u.Wanderer) Ui.RoleBadge(Ui.TamedRole, top, 26); // GG-T303
            else if (u.Wanderer && !string.IsNullOrEmpty(u.Def.Role)) Ui.RoleBadge(u.Def.Role, top, 26); // a wanderer is a hero class (GG-T342)
            else if (u.IsHero && !string.IsNullOrEmpty(u.Def.Role)) Ui.RoleBadge(u.Def.Role, top, 26);
            Ui.Text($"Lv {u.Level}", "h2 gold", top);
            // What the Bestiary has learned from defeating its class gates the numbers (§25.11, GG-T290).
            int know = Knowledge(u);
            var hpRow = Ui.El("row", col);
            Ui.Gauge(u.HpFraction, hpRow, 460);
            Ui.Text(know >= BestiaryKnowledge.Stats ? $"  {u.Hp}/{u.MaxHp}" : "  ???", "small", hpRow);
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
        /// The ability card in the actor panel (fits its ~260 design px). While aiming it explains the ability; once a
        /// target is picked it becomes the confirm card: what, on whom, then Cast / Cancel stacked on the right.
        /// </summary>
        private void AbilityInfo(AbilityDef a)
        {
            bool confirm = PendingTarget.HasValue && TargetingSlot >= 0;
            var row = Ui.El("row", panel);
            row.style.alignItems = Align.Center;
            var icon = Ui.El("menu-icon", row);
            icon.style.width = icon.style.height = Size.Avatar;
            var itemTex = a.Id != null && a.Id.StartsWith("item:") ? Art.ItemIcon(a.Id.Substring(5)) : null;
            icon.style.backgroundImage = new StyleBackground(itemTex ?? Art.AbilityIcon(a) ?? Art.Orb);
            var col = Ui.El("col grow", row);
            col.style.flexShrink = 1;
            var top = Ui.El("row", col);
            top.style.alignItems = Align.Center;
            Ui.Text(a.Name, "h2", top);
            if (a.Cost > 0) Ui.CostPips(a.Cost, top, 32).style.marginLeft = Space.S16;
            string timing = a.Kind == AbilityKind.Skill ? (a.CooldownTurns > 0 ? $"Skill · recharges in {a.CooldownTurns} turns" : "Skill")
                : a.Kind == AbilityKind.Item ? "Item · acts at once"
                : a.CastTime > 0 ? $"Charges {a.CastTime:0.#}s on the timeline" : "Instant";
            Ui.Text(timing, "tiny gold", col);
            if (!confirm)
            {
                var d = GlossaryText.Create(a.Description ?? "", "tiny", col);
                d.style.whiteSpace = WhiteSpace.Normal;
                Ui.Text("Tap a highlighted tile · tap the slot again to cancel", "tiny dim", col);
                return;
            }
            var hero = State.UnitById(SelectedHeroId);
            var hits = hero == null ? new List<Unit>() : TargetResolver.Collect(State, a, hero, PendingTarget.Value);
            string who = hits.Count == 0 ? (a.IsTeleport ? "Teleport to the marked tile" : "No one in the area")
                : hits.Count == 1 ? (hits[0] == hero ? $"On {hero.Name} (self)" : $"On {hits[0].Name}")
                : $"On {string.Join(", ", hits.Take(3).Select(h => h.Name))}{(hits.Count > 3 ? $" +{hits.Count - 3}" : "")}";
            var summary = Ui.Text(who, "small", col);
            summary.name = "confirm-summary";
            summary.style.whiteSpace = WhiteSpace.Normal;
            OutcomeLines(col, a);
            // Cast / Cancel side by side at the top of the card (GG-T391): the Party page is one tap target tall at the
            // shortest stage, so the two never stack out of view.
            row.style.alignItems = Align.FlexStart;
            var btns = Ui.El("row", row);
            btns.style.flexShrink = 0;
            btns.style.marginLeft = Space.S16;
            string verb = a.Kind == AbilityKind.Item ? "Use" : a.Kind == AbilityKind.Spell ? "Cast" : "Go";
            var ok = Ui.Button(verb, ConfirmPending, "btn--primary", btns);
            ok.name = "confirm-action";
            var cancel = Ui.Button("Cancel", CancelByPlayer, "btn--small", btns);
            cancel.name = "confirm-cancel";
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Info tabs (GG-T391): one tabbed panel under the orbs — Party · Lore · Log
        // ─────────────────────────────────────────────────────────────────────────

        private const int InfoTabParty = 0, InfoTabLore = 1, InfoTabLog = 2, InfoTabTutorial = 3;
        private static readonly string[] InfoTabNames = { "Party", "Lore", "Log", "Tutorial" };
        /// <summary>The tab the player picked; it lasts the whole battle (the instance outlives a rebuild).</summary>
        private int infoTab = InfoTabParty;
        private VisualElement infoTabStrip;
        private readonly VisualElement[] infoPages = new VisualElement[4];
        private ScrollView lorePage, logPage, tutorialPage;
        /// <summary>The tutorial lesson last shown, so a new one brings the Party page forward exactly once.</summary>
        private int shownLesson = -1;
        /// <summary>Every combat line this battle (newest last, capped), so the Log survives a rebuild.</summary>
        private const int LogCap = 200;
        private readonly List<(string text, Color color)> logLines = new List<(string, Color)>();

        private void BuildInfoTabs(VisualElement root)
        {
            var host = Ui.El("col info-tabs", root);
            host.name = "info-tabs";
            host.style.position = Position.Absolute;
            host.style.top = InfoTabsTop; host.style.bottom = Space.S24; // left/right/top: ApplyStageLayout (the HUD column)
            // A thin drawn strip (GG-T405: 60 px, under half a target) in a full-size hit area (GG-T524): each tab is a
            // 132-px-tall transparent target with its 60-px face at the bottom, flush on the window.
            infoTabStrip = Ui.El("tabs tabs--thin", host);
            for (int i = 0; i < InfoTabNames.Length; i++)
            {
                int idx = i;
                var hit = Ui.El("info-tab", infoTabStrip);
                hit.name = "tab-" + InfoTabNames[i].ToLowerInvariant();
                hit.pickingMode = PickingMode.Position;
                var face = Ui.Text(InfoTabNames[i], "tab" + (i == infoTab ? " tab--active" : ""), hit);
                face.pickingMode = PickingMode.Ignore;
                Ui.Tappable(hit, () => ShowInfoTab(idx));
            }
            var win = Ui.Window(null, host, "window--compact info-tabs__window");
            win.name = "info-panel";
            panel = infoPages[InfoTabParty] = InfoPage(win, "party");
            lorePage = InfoPage(win, "lore");
            infoPages[InfoTabLore] = lorePage;
            // The combat log keeps the old feed's name, so automation that looks for "feed" still finds it.
            logPage = InfoPage(win, "feed");
            infoPages[InfoTabLog] = logPage;
            tutorialPage = InfoPage(win, "tutorial");
            infoPages[InfoTabTutorial] = tutorialPage;
            foreach (var (text, color) in logLines) AddLogLine(text, color);
            ApplyInfoTab();
        }

        private static ScrollView InfoPage(VisualElement win, string name)
        {
            var sv = new ScrollView(ScrollViewMode.Vertical) { name = name };
            sv.AddToClassList("list");
            sv.AddToClassList("info-tabs__page");
            sv.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            sv.touchScrollBehavior = ScrollView.TouchScrollBehavior.Clamped;
            win.Add(sv);
            return sv;
        }

        private void ShowInfoTab(int i)
        {
            infoTab = Mathf.Clamp(i, InfoTabParty, TutorialActive ? InfoTabTutorial : InfoTabLog);
            ApplyInfoTab();
        }

        /// <summary>Shows the picked page. While an ability is aimed the Party page (its card and Cast / Cancel) shows
        /// whatever tab was picked, and the pick comes back when the aim ends.</summary>
        private void ApplyInfoTab()
        {
            if (infoTabStrip == null) return;
            int shown = TargetingSlot >= 0 ? InfoTabParty : infoTab;
            for (int i = 0; i < infoPages.Length; i++)
                if (infoPages[i] != null) infoPages[i].style.display = i == shown ? DisplayStyle.Flex : DisplayStyle.None;
            int k = 0;
            foreach (var t in infoTabStrip.Children())
            {
                bool on = k++ == shown;
                // The Tutorial tab exists only while lessons are running (GG-T449).
                if (k - 1 == InfoTabTutorial) t.style.display = TutorialActive ? DisplayStyle.Flex : DisplayStyle.None;
                // t is the tab's hit area (GG-T524); its drawn face is its one child.
                if (t.childCount == 0) continue;
                var face = t[0];
                face.EnableInClassList("tab--active", on);
                if (on) Fonts.UseHeavy(face); else Fonts.UseBody(face);
            }
            if (shown == InfoTabLog) ScrollLogToEnd();
        }

        /// <summary>The Lore page: the unit's Prose description and lore (ClassDef.Description / Lore, filled from
        /// Content/Text by ProseText) and, for a foe, what the Bestiary knows of it.</summary>
        private void SyncLore(Unit u)
        {
            if (lorePage == null) return;
            lorePage.Clear();
            if (u == null) return;
            var top = Ui.El("row", lorePage);
            top.style.alignItems = Align.Center;
            var nm = Ui.Text(u.Name, "h2 grow", top);
            nm.style.flexShrink = 1;
            if (!string.IsNullOrEmpty(u.Def.Role)) Ui.Text(u.Def.Role, "small gold", top);
            bool foe = !u.IsHero && !u.IsIndependent;
            var entry = foe && App.Save?.Bestiary != null && App.Save.Bestiary.TryGetValue(u.Def.Id, out var be) ? be : null;
            int know = Knowledge(u);
            bool seen = !foe || know >= BestiaryKnowledge.Description || (entry != null && entry.Seen);
            if (!seen) LoreText("Not yet in the Bestiary. Its story is learned by meeting it.", "small dim");
            else
            {
                if (!string.IsNullOrEmpty(u.Def.Description)) LoreText(u.Def.Description, "small");
                if (!string.IsNullOrEmpty(u.Def.Lore) && u.Def.Lore != u.Def.Description) LoreText(u.Def.Lore, "small dim");
            }
            if (!foe) return;
            var head = Ui.Text("Bestiary", "small gold", lorePage);
            head.style.marginTop = Space.S8;
            int defeated = entry?.Defeated ?? 0;
            string knows = know >= BestiaryKnowledge.Abilities ? "everything" : know >= BestiaryKnowledge.Elements ? "its weaknesses"
                : know >= BestiaryKnowledge.Stats ? "its HP and stats" : know >= BestiaryKnowledge.Speed ? "its speed" : "its name";
            LoreText($"Defeated {defeated} · known: {knows}", "small");
            if (know >= BestiaryKnowledge.Elements) LoreText(BestiaryKnowledge.ElementLine(u.Def), "small dim");
            int more = BestiaryKnowledge.DefeatsToNext(entry);
            int next = BestiaryKnowledge.NextTier(entry);
            if (next >= 0 && more > 0) LoreText($"Defeat {more} more to learn {BestiaryKnowledge.Label(next)}.", "small dim");
        }

        private void LoreText(string text, string classes)
        {
            var t = GlossaryText.Create(text, classes, lorePage);
            t.style.whiteSpace = WhiteSpace.Normal;
            t.style.marginBottom = Space.S8;
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
                InspectCard.Attach(row, body => InspectCard.Ability(body, ab, SlotNote(hero, ab)));
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

        /// <summary>Dev/automation helper: add orbs to the bank (cheat — development builds only call this).</summary>
        public void DevGiveMana(int n, int charge = -1)
        {
            State.Bank.Add(n);
            // Dev only: a charge past one orb is allowed on purpose, so the next charge gain rolls several orbs at
            // once (tools/harness.py orbs, GG-T112).
            if (charge >= 0) State.ManaCharge = Mathf.Clamp(charge, 0, 10 * BattleConfig.ManaChargePerOrb);
            SyncOrbs();
            SyncBar();
        }

        public string BannerText => banner?.text;

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
            App.StartCoroutine(Play(events, skipMovePrefix));
        }

        private void OnSlot(int slot)
        {
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
            ShowBanner($"{a.Name}: choose a target");
            SyncBar();
            SyncPanel();
        }

        private static string StatusText(SlotStatus s)
        {
            switch (s)
            {
                case SlotStatus.Unaffordable: return "Not enough mana";
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
            SyncPoolPreview();
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
        /// Lock a pick and ask for confirmation: the area and every unit it would touch light up, the banner names the
        /// deal, and the actor panel turns into the confirm card. Tapping another legal tile re-aims; tapping the same
        /// tile again (or Confirm) commits.
        /// </summary>
        private void SetPending(Coord anchor)
        {
            var hero = State.UnitById(SelectedHeroId);
            var a = hero == null || TargetingSlot < 0 ? null : BattleEngine.SlotAbility(State, hero, TargetingSlot);
            if (a == null) return;
            PendingTarget = anchor;
            RedrawTargeting();
            var hits = TargetResolver.Collect(State, a, hero, anchor);
            string who = hits.Count == 0 ? "" : hits.Count == 1 ? $" on {(hits[0] == hero ? "self" : hits[0].Name)}" : $" on {hits.Count} targets";
            ShowBanner($"{a.Name}{who}? Confirm below");
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
            else foreach (var t in TargetResolver.Tiles(at, a.Shape, a.Radius)) board.Mark(t, color, 0.42f);
            foreach (var u in hits)
                if (board.Units.TryGetValue(u.Id, out var v)) { v.SetGlow(color, 0.9f); targetGlows.Add(u.Id); }
        }

        public override bool OnBack()
        {
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
        /// <summary>Bank slot each landed orb flew to, in playback order (last 24) — the automation proof that orbs
        /// minted together fan into consecutive slots (GG-T112).</summary>
        public readonly List<int> OrbSlotsFlown = new List<int>();
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
            Ui.MenuRow("Resume", "", Close, w);
            Ui.MenuRow(AutoPlay ? "Auto battle: ON" : "Auto battle: OFF", "", () => { AutoPlay = !AutoPlay; Close(); }, w);
            Ui.MenuRow("How to play", "", () => { Close(); App.Router.Push(new SettingsScreen()); }, w);
            int retreatPct = (int)(BattleConfig.RetreatKeepFraction * 100);
            if (InCrucible)
                Ui.MenuRow("Leave the Crucible", "(keep all)", () => { Close();
                    Confirm.Show($"Leave the Crucible after {State.Wave} wave{(State.Wave == 1 ? "" : "s")}? You keep every coin, item and point of experience earned so far.", "Leave", Retreat); }, w);
            else
                Ui.MenuRow("Retreat", $"(keep {retreatPct}%)", () => { Close();
                    Confirm.Show($"Retreat from battle? You'll keep {retreatPct}% of this fight's gold and loot — safer than risking a wipe.", "Retreat", Retreat); }, w);
            Ui.Select(w[1]);
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
                Debug.Log($"[Battle] record saved: {file} ({recorder.Turns} turns)");
            }
            catch (Exception e) { Debug.LogWarning($"[Battle] could not save the battle record: {e.Message}"); }
            recorder = null;
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
            SaveRecord();
            if (App.Save != null && CommitsToSave)
            {
                State.Phase = BattlePhase.Retreated;
                var rep = BattleFactory.Commit(App.Content, App.Save, State);
                App.Persist();
                PostBattleScreen.RouteFrom(rep, State.Stage.Id);
            }
            else App.Router.Back();
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
                playing = false;
                SyncAll();
            }
            TickLinger(dt);
            if (dragging && !simulatingDrag && App.Settings.DragTimer)
            {
                dragTimeLeft -= dt;
                timerLabel.text = $"{Mathf.Max(0, dragTimeLeft):0.0}";
                if (dragTimeLeft <= 0f && !CoachRail && !coachCancelStep) CommitDrag(); // a coached drag is never forced (GG-T539)
            }
            else if (timerLabel != null) timerLabel.text = "";

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
                if (cmd is MoveCommand m && m.Path.Count > 0) App.StartCoroutine(SimulatedHumanDrag(m, perfect));
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
                    ShowBanner($"Hint: {caster.Name} can use {ab.Name}");
                }
                return;
            }
            if (decision is ShieldCommand) { ShowBanner("Hint: nothing good to do? Press GUARD"); return; }
            var cmd = decision as MoveCommand;
            if (cmd == null || cmd.Path.Count == 0) return;
            var pv = BattleEngine.PreviewMove(State, cmd.HeroId, cmd.Path);
            // The Moving lesson promises a glowing path even when no pincer is reachable yet (the opening board of
            // the first battle): demo the bot's best positioning move instead of falling back to a bare tip.
            if (pv.Pincers.Count == 0 && !TutorialDemoMove) { ShowBanner("Tip: trap an enemy between two heroes"); return; }
            var hero = State.UnitById(cmd.HeroId);
            var dest = cmd.Path[cmd.Path.Count - 1];
            board.ClearMarks();
            board.Mark(dest, Palette.Gold, 0.5f);
            if (board.Units.TryGetValue(hero.Id, out var hv)) { hv.SetGlow(Palette.Gold, 0.95f); App.StartCoroutine(Tween.Pop(hv.Root, 1.12f, 0.35f)); }
            board.Lines.ClearLines();
            var from = BoardView.CenterOf(hero.Pos);
            foreach (var step in cmd.Path) { var to = BoardView.CenterOf(step); board.Lines.Add(from, to, Palette.Gold.WithA(0.55f), 10f); from = to; }
            ShowBanner($"Hint: drag {hero.Name} to the glowing tile");
        }

        private void ClearHint()
        {
            idle = 0f;
            if (!hintShown) return;
            hintShown = false;
            if (TargetingSlot < 0 && !dragging) { board.ClearMarks(); board.Lines.ClearLines(); }
        }

        public void SelectHero(int id)
        {
            if (SelectedHeroId == id)
            {
                // Re-touching the selected hero still dismisses an inspected enemy card (it hid the tutorial card).
                if (panelUnitId >= 0) { panelUnitId = -1; SyncPanel(); }
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
        /// Action/status banner in the band between the timeline frame and the board (GG-T405: it never touches either).
        /// Text that runs long steps its font down (H3 → Body → Small) while it still fits one line, then wraps at the
        /// smallest size, and if two lines still don't fit the band it's shown, paused, then slowly scrolled so the whole
        /// message gets read. It eases in over Timing.Quick.
        /// </summary>
        private void ShowBanner(string text)
        {
            if (banner == null) return;
            text ??= "";
            banner.text = text;
            NoteShown(text);
            banner.style.fontSize = BannerSizes[0];
            banner.style.top = Space.S0;
            if (bannerScrollRoutine != null) { App.StopCoroutine(bannerScrollRoutine); bannerScrollRoutine = null; }
            if (text.Length > 0) bannerScrollRoutine = App.StartCoroutine(FitBanner());
        }

        private static readonly float[] BannerSizes = { TypeScale.H3, TypeScale.Body, TypeScale.Small };

        private IEnumerator FitBanner()
        {
            banner.style.opacity = 0f;
            // Step down while the text wraps past one line; a frame per size so resolvedStyle.height is current.
            for (int i = 0; i < BannerSizes.Length; i++)
            {
                banner.style.fontSize = BannerSizes[i];
                yield return null;
                if (banner.resolvedStyle.height < BannerSizes[i] * 1.7f) break; // one line at this size
            }
            float content = banner.resolvedStyle.height;
            App.StartCoroutine(Tween.Run(Timing.Quick, k => { if (banner != null) banner.style.opacity = k; }));
            if (content <= BannerViewportHeight)
            {
                banner.style.top = (BannerViewportHeight - content) / 2f;
                bannerScrollRoutine = null;
                yield break;
            }
            float travel = content - BannerViewportHeight;
            yield return Tween.Wait(Timing.Cinematic); // let the reader start before it moves
            yield return Tween.Run(Mathf.Clamp(travel / 40f, 0.8f, 4f), k => banner.style.top = -travel * k);
            yield return Tween.Wait(Timing.Dramatic); // hold on the tail before the next message replaces it
            bannerScrollRoutine = null;
        }
    }
}
