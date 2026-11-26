using System;
using System.Collections;
using System.Collections.Generic;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Meta;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>
    /// The whole app is constructed here, in code, at startup — the scene file is an empty shell.
    /// Owns the services (content, saves, audio), the UI Toolkit panel, the adaptive stage
    /// (GG-LAW-8, bible §26) and the screen router.
    /// </summary>
    public sealed class GameApp : MonoBehaviour
    {
        /// <summary>GG-T694: the game's one current display name — title screen, credits and ProjectSettings'
        /// productName are meant to agree; a bare placeholder until the owner picks a real one, so the next
        /// rename is a one-line change here (plus ProjectSettings, which Unity can't read this from).</summary>
        public const string Title = "GridGame";

        /// <summary>The minimum stage: every screen is laid out to fit at least this (19.5:9 portrait).</summary>
        public const int DesignWidth = 1170;
        public const int DesignHeight = 2532;
        /// <summary>The widest (9:16, 2532*9/16) and tallest (9:23, 1170*23/9) shape the stage grows to fill edge to edge;
        /// beyond them (tablets, opened foldables, desktop windows) it is letterboxed at the nearest valid shape inside the
        /// blue mystic fog (GG-T706, owner 2026-11-23, bible §26.3; GG-T398's 3:4 fill is withdrawn).</summary>
        public const int MaxStageWidth = 1424;
        public const int MaxStageHeight = 2990;
        /// <summary>Design-pixel margin around a framed stage, so its outline is fully on screen.</summary>
        private const float FramePad = 36f;

        /// <summary>The live stage size in design pixels; never below DesignWidth×DesignHeight.</summary>
        public static float StageWidth { get; private set; } = DesignWidth;
        public static float StageHeight { get; private set; } = DesignHeight;
        /// <summary>Design px of the stage covered by the device's notch / home bar (§26.5).</summary>
        public static float SafeTop { get; private set; }
        public static float SafeBottom { get; private set; }
        /// <summary>Raised after the stage changes size (window resize, harness resize).</summary>
        public static event Action StageResized;

        public static GameApp I { get; private set; }

        public ContentDb Content { get; private set; }
        /// <summary>The active player profile's save slots (Profiles/&lt;Name&gt;/Saves, GG-T99).</summary>
        public SaveStore Saves { get; private set; }
        /// <summary>Cloud save (§15.2, GG-T608): Play Games Saved Games on Android; a no-op everywhere else.</summary>
        public CloudSaves Cloud { get; private set; } = new CloudSaves(null);
        /// <summary>The active player profile and its settings (bible §15.4, GG-T99).</summary>
        public ProfileSession Profile { get; private set; }
        /// <summary>The active profile's settings — the same whichever save slot is loaded, and on the title screen.</summary>
        public SettingsSave Settings => Profile?.Settings ?? fallbackSettings;
        private readonly SettingsSave fallbackSettings = new SettingsSave();
        public Audio Audio { get; private set; }
        /// <summary>GG-T456: which track plays: a shuffle bag per region, one draw per stage, and the Camp's draw.</summary>
        public Meta.MusicPlaylist Playlist { get; } = new Meta.MusicPlaylist(System.Environment.TickCount);
        public ScreenRouter Router { get; private set; }
        public VisualElement Stage { get; private set; }
        public VisualElement Overlay { get; private set; }
        public UIDocument Document { get; private set; }

        /// <summary>GG-T702: where the account wallet lives (beside the profiles); its wallet is bound as
        /// <see cref="DiamondExchange.Wallet"/>.</summary>
        public AccountWalletStore WalletStore { get; private set; }
        /// <summary>The store login the wallet belongs to: null (this device's wallet) until the store names one.</summary>
        public string WalletLogin { get; private set; }

        /// <summary>The save being played (null on the title screen).</summary>
        public SaveData Save { get; set; }
        public int Slot { get; set; } = -1;

        public List<string> ContentProblems { get; private set; } = new List<string>();
        public event Action<float> Ticked;

        private VisualElement letterbox;
        private VisualElement stageFrame;
        private MysticFog fog;
        private Vector2 lastScreen;
        private Rect lastSafe;
        private float lastDpi; // GG-T551: a density change alone (Android display size) moves the type floor
        /// <summary>Settings → UI scale and Thumb margin (GG-T1222), set by <see cref="ApplySettings"/>; the stage fit
        /// reruns when either differs from what it last laid out with.</summary>
        private int uiScale = 100, thumbMargin, lastUiScale = 100, lastThumbMargin;
        /// <summary>A faked notch / gesture bar in screen px (top, bottom), from <c>-safeArea</c> or the automation
        /// <c>safearea</c> endpoint (GG-T483); null = the device's own <see cref="Screen.safeArea"/>.</summary>
        private Vector2? fakeInsets;
        private bool fakeInsetsParsed;
        private float playClock;

        /// <summary>
        /// GG-T566: one number format for the whole app. Every thread (and this one) formats and parses with the invariant
        /// culture, so a device set to a comma-decimal or other locale no longer shows "1,5s" battle timers or regional digit
        /// grouping in the Diamond store. Runs as early as Unity allows, before any scene, save or content is touched.
        /// Dates the player sees are formatted explicitly (the save-slot list shows "yyyy-MM-dd HH:mm" local time).
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void UseInvariantCulture()
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            System.Globalization.CultureInfo.DefaultThreadCurrentCulture = inv;
            System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = inv;
            System.Globalization.CultureInfo.CurrentCulture = inv;
            System.Globalization.CultureInfo.CurrentUICulture = inv;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            UseInvariantCulture(); // again on the boot frame, in case anything reset the main thread's culture in between
            if (I != null) return;
            var go = new GameObject("GameApp");
            DontDestroyOnLoad(go);
            go.AddComponent<GameApp>();
        }

        private void Awake()
        {
            I = this;
            CrashReporter.Install(); // GG-T484: first, so a boot failure is caught too
            // GG-T1125: Unity.Shared's bootstraps (HeadlessBootstrap, ProfilesBootstrap) read --headless / -muted / MINDATTIC_* in any build, before this runs;
            // a release player without -enableDebugOptions undoes what they set (mute, run-in-background) so it ignores debug flags like every other one (GG-T566).
            if (!CommandLine.DebugOptionsAllowed)
            {
                global::Unity.Shared.Core.Profiles.SessionMuted = false;
                Application.runInBackground = false;
            }
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            projectQualityLevel = QualitySettings.GetQualityLevel();

            var cam = new GameObject("Camera").AddComponent<Camera>();
            cam.transform.SetParent(transform, false);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.cullingMask = 0;

            Content = LoadContent();
            Art.IndexSpells(Content.Abilities.Keys);
            OpenProfiles();
            OpenWallet(); // GG-T702: every slot moved into the wallet before the real-money store connects
            DiamondStores.Warm(); // (only when Features.DiamondStore is on)
            Cloud = CloudSaves.Create();
            Cloud.Start();
            Audio = Audio.Create(transform);

            if (CommandLine.Has("-allfeatures")) GridGame.Meta.Features.EnableAll(); // GG-T392: every hidden system back on
            BuildPanel();
            Router = new ScreenRouter(Stage.Q("screens"), Stage.Q("fade"));
            Router.Changed += VariantPicker.OnScreen; // GG-T532: a hero who joined is asked female/male at the next quiet screen
            TextFit.Install(Document.rootVisualElement); // GG-T305: every wrapped line and every overflowing one-liner
            ApplySettings(); // the profile's settings hold on the title screen too, before any save is loaded

            // GG-T775: `-turnMode <name>` plays that mode this run (in memory; settings.json is untouched). A save's New Game lock still wins.
            var turnModeArg = CommandLine.Value("-turnMode");
            if (turnModeArg != null)
            {
                if (TurnModeSetting.TryParseBuilt(turnModeArg, out var launchMode, out var turnModeError)) Settings.TurnMode = launchMode;
                else Debug.LogError($"[CommandLine] -turnMode ignored: {turnModeError}");
            }

            if (CommandLine.Has("-noDiagonalSlip")) BattleConfig.DiagonalSlipEnabled = false;
            var skill = CommandLine.Value("-skill");
            if (skill != null && float.TryParse(skill, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var skillLevel)) BotSkillProfile.Level = skillLevel;
            BotSkillProfile.TelemetryEnabled = CommandLine.Has("-telemetry");

            var start = CommandLine.Value("-start");
            if (TestMode.Active) TestMode.Start();
            else if (DemoMode.Active) DemoMode.Start();
            else if (!string.IsNullOrEmpty(start) && Automation.StartScreens.TryGet(start, out var startScreen)) Router.ResetTo(startScreen);
            else if (CommandLine.DebugOptionsAllowed && global::Unity.Shared.Core.Headless.IsActive) StartHeadlessSession(); // GG-T566: release needs -enableDebugOptions
            else Router.ResetTo(new TitleScreen());

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            Automation.AutomationServer.TryStart(this);
#endif
        }

        /// <summary>
        /// GG-T323: the shared <c>--headless</c> mode (Unity.Shared, MAU-T18) skips the title and the menus straight into a
        /// normal session: a fresh game with the starting party on a throwaway save (slot -1, never written), at the Hub,
        /// for a script to drive over automation. -testmode, -demo and -start still take precedence.
        /// </summary>
        private void StartHeadlessSession()
        {
            Save = Progression.NewGame(Content, "Headless");
            Slot = -1;
            Router.ResetTo(new HubScreen());
            Debug.Log("[GameApp] headless: new game on a throwaway save, at the Hub");
        }

        private ContentDb LoadContent()
        {
            var db = ContentLoader.Load(name => Resources.Load<TextAsset>("Content/" + name)?.text);
            ContentProblems = ContentLoader.Validate(db, file => Art.Exists(Art.PortraitFolder + file));
            foreach (var p in ContentProblems) Debug.LogWarning("[Content] " + p);
            Debug.Log($"[GameApp] content: {db.Classes.Count} classes, {db.Abilities.Count} abilities, {db.Items.Count} items, {db.Stages.Count} stages");
            return db;
        }

        private void BuildPanel()
        {
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.name = "GridGamePanel";
            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/GridGameTheme");
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(DesignWidth, DesignHeight);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.clearColor = true;
            settings.colorClearValue = Color.black;

            var docGo = new GameObject("UI");
            docGo.transform.SetParent(transform, false);
            docGo.SetActive(false);
            Document = docGo.AddComponent<UIDocument>();
            Document.panelSettings = settings;
            docGo.SetActive(true);

            var root = Document.rootVisualElement;
            root.styleSheets.Add(Resources.Load<StyleSheet>("UI/Theme"));
            root.style.backgroundColor = Color.black;
            Fonts.UseBody(root);

            letterbox = new VisualElement();
            letterbox.AddToClassList("fill");
            letterbox.AddToClassList("letterbox");
            root.Add(letterbox);

            fog = new MysticFog(); // GG-T706: fills the out-of-bounds area while the stage is letterboxed
            letterbox.Add(fog);

            stageFrame = new VisualElement { name = "stage-frame", pickingMode = PickingMode.Ignore };
            stageFrame.AddToClassList("stage-frame");
            letterbox.Add(stageFrame);

            Stage = new VisualElement { name = "stage" };
            Stage.AddToClassList("stage");
            letterbox.Add(Stage);

            var screens = new VisualElement { name = "screens" };
            screens.AddToClassList("fill");
            Stage.Add(screens);

            Overlay = new VisualElement { name = "overlay", pickingMode = PickingMode.Ignore };
            Overlay.AddToClassList("fill");
            Stage.Add(Overlay);

            var fade = new VisualElement { name = "fade", pickingMode = PickingMode.Ignore };
            fade.AddToClassList("fill");
            fade.AddToClassList("fade");
            Stage.Add(fade);

            FitStage();
#if DEVELOPMENT_BUILD
            if (CommandLine.Has("-traceInput"))
            {
                root.RegisterCallback<PointerDownEvent>(e => Debug.Log($"[Input] down at {e.position} target={(e.target as VisualElement)?.name}/{(e.target as VisualElement)?.GetType().Name} id={e.pointerId}"), TrickleDown.TrickleDown);
                root.RegisterCallback<PointerUpEvent>(e => Debug.Log($"[Input] up at {e.position} target={(e.target as VisualElement)?.name}/{(e.target as VisualElement)?.GetType().Name} id={e.pointerId}"), TrickleDown.TrickleDown);
                root.RegisterCallback<ClickEvent>(e =>
                {
                    var sb = new System.Text.StringBuilder();
                    for (var v = e.target as VisualElement; v != null; v = v.parent)
                        sb.Append($" > {v.GetType().Name}#{v.name}[{string.Join(".", v.GetClasses())}]{v.worldBound}");
                    Debug.Log("[Input] click path" + sb);
                }, TrickleDown.TrickleDown);
            }
#endif
        }

        /// <summary>Size of the stage for a screen aspect (width/height): never below the design size, grows the
        /// spare axis up to the widest (9:16) / tallest (9:23) supported shape, never scales non-uniformly (bible §26.3).</summary>
        public static Vector2 StageSizeFor(float screenAspect)
        {
            const float designAspect = DesignWidth / (float)DesignHeight;
            if (screenAspect >= designAspect)
                return new Vector2(Mathf.Clamp(Mathf.Round(DesignHeight * screenAspect), DesignWidth, MaxStageWidth), DesignHeight);
            return new Vector2(DesignWidth, Mathf.Clamp(Mathf.Round(DesignWidth / screenAspect), DesignHeight, MaxStageHeight));
        }

        /// <summary>Fit the stage inside the screen: every shape from 9:23 to 9:16 fills it edge to edge; shapes outside
        /// that range get the nearest supported stage, centered and framed in the mystic fog (GG-T706). Never stretched.</summary>
        private void FitStage()
        {
            var screen = new Vector2(Screen.width, Screen.height);
            var safeNow = SafeAreaPx();
            if (screen == lastScreen && safeNow == lastSafe && Screen.dpi == lastDpi && uiScale == lastUiScale
                && thumbMargin == lastThumbMargin && Stage.Q("screens") != null) return;
            lastScreen = screen;
            lastSafe = safeNow;
            lastDpi = Screen.dpi;
            lastUiScale = uiScale;
            lastThumbMargin = thumbMargin;
            float screenAspect = screen.x / Mathf.Max(1f, screen.y);
            var size = StageSizeFor(screenAspect);
            bool resized = size.x != StageWidth || size.y != StageHeight;
            StageWidth = size.x; StageHeight = size.y;

            // Out of the phone range the stage is framed; leave room so the whole outline is visible on every side.
            float exactW = DesignHeight * screenAspect, exactH = DesignWidth / screenAspect;
            // GG-T1222: a UI scale under 100% draws the stage smaller than the screen, framed in the fog like an off shape.
            bool framed = exactW > MaxStageWidth + 2f || exactH > MaxStageHeight + 2f || uiScale < 100;
            float pad = framed ? FramePad : 0f;
            var fit = new Vector2(size.x + pad * 2f, size.y + pad * 2f) * (100f / uiScale);

            // Scale by the limiting axis so the whole stage (and frame) is visible.
            bool widthLimits = screen.x / fit.x <= screen.y / fit.y;
            Document.panelSettings.referenceResolution = new Vector2Int((int)fit.x, (int)fit.y);
            Document.panelSettings.match = widthLimits ? 0f : 1f;
            float logicalW = widthLimits ? fit.x : fit.y * screenAspect;
            float logicalH = widthLimits ? fit.x / screenAspect : fit.y;
            // GG-T770: the one GUI text size from this scale, dpi and stage width (GG-T551 legibility floor included).
            GuiType.SetScreen(Document.rootVisualElement, widthLimits ? screen.x / fit.x : screen.y / fit.y, Screen.dpi, size.x);

            Stage.style.width = size.x; Stage.style.height = size.y;
            Stage.style.left = (logicalW - size.x) / 2f;
            Stage.style.top = (logicalH - size.y) / 2f;

            stageFrame.EnableInClassList("hidden", !framed);
            fog.SetActive(framed);
            const float gap = 10f;
            stageFrame.style.left = (logicalW - size.x) / 2f - gap;
            stageFrame.style.top = (logicalH - size.y) / 2f - gap;
            stageFrame.style.width = size.x + gap * 2f;
            stageFrame.style.height = size.y + gap * 2f;

            bool insetsChanged = ApplySafeArea(logicalW, logicalH, safeNow);
            // A new notch / gesture bar (a rotation to upside-down portrait, the automation endpoint) changes the usable
            // height Fit.H reads, so the current screen rebuilds as it does for a new stage size (GG-T483).
            if (resized || insetsChanged) StageResized?.Invoke();
        }

        /// <summary>The safe area in screen px (bottom-left origin, like <see cref="Screen.safeArea"/>): the device's own, or
        /// the faked insets when <c>-safeArea top,bottom</c> or the automation <c>safearea</c> endpoint set some (GG-T483).</summary>
        private Rect SafeAreaPx()
        {
            if (!fakeInsetsParsed)
            {
                fakeInsetsParsed = true;
                // -safeArea <topPx>,<bottomPx>: a notch / gesture bar on desktop, in window px.
                var ov = CommandLine.Value("-safeArea");
                if (!string.IsNullOrEmpty(ov))
                {
                    var parts = ov.Split(',');
                    if (parts.Length == 2 && float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var t)
                        && float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var b))
                        fakeInsets = new Vector2(Mathf.Max(0f, t), Mathf.Max(0f, b));
                }
            }
            if (fakeInsets is Vector2 f)
                return new Rect(0, f.y, Screen.width, Mathf.Max(1f, Screen.height - f.x - f.y));
            return Screen.safeArea;
        }

        /// <summary>Fakes a notch of <paramref name="topPx"/> and a gesture bar of <paramref name="bottomPx"/> (window px), or
        /// goes back to the device's own safe area when both are negative (automation <c>safearea</c>, GG-T483). Applies on
        /// the next frame.</summary>
        public void FakeSafeArea(float topPx, float bottomPx)
        {
            fakeInsetsParsed = true;
            fakeInsets = topPx < 0f && bottomPx < 0f ? (Vector2?)null : new Vector2(Mathf.Max(0f, topPx), Mathf.Max(0f, bottomPx));
        }

        /// <summary>Insets the screens where the safe area (notch / home bar) overlaps the stage (bible §26.5). Each screen's
        /// backdrop still bleeds under the insets (<see cref="GameScreen.BleedBackdrops"/>). True when the insets changed.</summary>
        private bool ApplySafeArea(float logicalW, float logicalH, Rect safe)
        {
            if (Screen.width <= 0 || Screen.height <= 0) return false;
            float k = logicalH / Screen.height;
            float stageTop = (logicalH - StageHeight) / 2f, stageBottom = stageTop + StageHeight;
            // GG-T1222: Settings → Thumb margin keeps extra room clear above the safe area's bottom edge (phones only).
            float safeTop = (Screen.height - safe.yMax) * k, safeBottom = logicalH - safe.yMin * k - thumbMargin;
            var screens = Stage.Q("screens");
            float top = Mathf.Round(Mathf.Max(0f, safeTop - stageTop)), bottom = Mathf.Round(Mathf.Max(0f, stageBottom - safeBottom));
            bool changed = top != SafeTop || bottom != SafeBottom;
            SafeTop = top;
            SafeBottom = bottom;
            screens.style.top = SafeTop;
            screens.style.bottom = SafeBottom;
            return changed;
        }

        private void Update()
        {
            FitStage();
            Tween.Advance();
            float dt = Tween.Dt;
            if (Save != null) { playClock += dt; if (playClock >= 1f) { Save.PlaySeconds += playClock; playClock = 0f; } }
            if (!Router.Preparing) Router.Current?.Tick(dt); // GG-T1205: a screen still loading is not mounted yet
            Ticked?.Invoke(dt);

            // The Input System reports Android's back button (and back gesture) as the Escape key.
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) HardwareBack();
        }

        // ── App lifecycle (GG-T480) ────────────────────────────────────────────────

        private bool suspended;
        private VisualElement quitVeil;

        /// <summary>Phones pause the game when it leaves the foreground; the desktop build keeps running unfocused (the
        /// harness drives it in the background) unless <c>-pauseOnFocusLoss</c> asks it to behave like a phone.</summary>
        private static bool PausesInBackground => Application.isMobilePlatform || CommandLine.Has("-pauseOnFocusLoss");

        /// <summary>Back with nowhere to go back to: on Android it asks to quit (a second Back cancels the question).</summary>
        private void HardwareBack()
        {
            if (quitVeil != null && quitVeil.parent != null) { quitVeil.RemoveFromHierarchy(); quitVeil = null; return; }
            if (ModalSwallowsBack()) return;
            if (Router.Back()) return;
            if (Application.platform != RuntimePlatform.Android) return;
            quitVeil = Confirm.Show("Quit the game? Your progress is saved.", "Quit", () => { Persist(); Application.Quit(); });
        }

        /// <summary>Modal overlays that play out on their own or need their own answer: while one is up, Back must not
        /// navigate the screen beneath it (a finishing Rested refill would then launch a battle from the wrong screen).</summary>
        private static readonly string[] BackBlockingOverlays =
            { TimedJobView.VeilName, VariantPicker.VeilName, "lucky-reveal", "summon-reveal" };

        /// <summary>True when an overlay took the Back press: a Confirm question is answered Cancel; a paywall job, a
        /// reveal, the look picker or a coach lesson (outside the battle, whose Back opens Pause) swallow it.</summary>
        private bool ModalSwallowsBack()
        {
            if (Overlay == null) return false;
            if (Confirm.CancelTop()) return true;
            foreach (var n in BackBlockingOverlays) if (Overlay.Q(n) != null) return true;
            if (Coach.Active && Overlay.Q("coach") != null && !(Router.Current is BattleScreen)) return true;
            return false;
        }

        /// <summary>Home button, an incoming call, the app switcher: the save is written first (Android may kill a
        /// backgrounded app without warning), then the battle stops on its pause menu and every sound holds.</summary>
        private void OnApplicationPause(bool paused)
        {
            if (paused) Suspend(true);
            else ResumeFromBackground();
        }

        /// <summary>Focus loss without a pause (the notification shade, a system dialog) holds the game too, without the
        /// disk write.</summary>
        private void OnApplicationFocus(bool focused)
        {
            if (!focused) Suspend(false);
            else ResumeFromBackground();
        }

        private void OnApplicationQuit() { Persist(); Cloud?.Flush(); }

        private void Suspend(bool flushSave)
        {
            if (flushSave && Save != null) { Save.PlaySeconds += playClock; playClock = 0f; Persist(); Cloud?.Flush(); }
            if (!PausesInBackground || suspended || Router == null) return;
            suspended = true;
            if (!Router.Preparing) Router.Current?.OnAppSuspended();
            AudioListener.pause = true; // music and every ringing effect hold mid-clip and carry on from there
        }

        /// <summary>Sound comes back; the battle stays on its pause menu until the player taps Resume.</summary>
        private void ResumeFromBackground()
        {
            if (!suspended) return;
            suspended = false;
            AudioListener.pause = false;
        }

        // ── Session helpers ─────────────────────────────────────────────────────────

        public void Persist()
        {
            // GG-T702: the wallet first. A save written while its wallet isn't could hold what a spend bought while the
            // wallet on disk still holds the Diamonds, so the save waits for the next try.
            if (!PersistWallet()) { if (Save != null && Slot >= 0) Toast("Couldn't save!"); return; }
            if (Save == null || Slot < 0) return;
            // SaveStore.Write never throws — it logs and reports failure via its return value (GG-T146).
            if (!Saves.Write(Slot, Save)) { Toast("Couldn't save!"); return; }
            Cloud?.Saved(Profile?.Name, Slot, Save); // GG-T608: uploaded at most once a minute, and on pause/quit
        }

        public void Toast(string text, float seconds = 1.8f)
        {
            StartCoroutine(ToastRoutine(text, seconds));
        }

        private IEnumerator ToastRoutine(string text, float seconds)
        {
            var t = Ui.Text(text, "toast shadow", Overlay);
            t.name = "toast";
            t.style.opacity = 0;
            yield return null;
            t.style.opacity = 1;
            yield return new WaitForSecondsRealtime(seconds);
            t.style.opacity = 0;
            yield return new WaitForSecondsRealtime(0.25f);
            t.RemoveFromHierarchy();
        }

        /// <summary>The quality level the player booted with, for <see cref="SettingsSave.QualityLevel"/> -1.</summary>
        private int projectQualityLevel;

        public void ApplySettings()
        {
            var st = Settings;
            Audio?.Apply(st);
            // GG-T1221 Graphics: quality level (-1 = the project's own), vsync, and the frame-rate cap (60 by default).
            int quality = st.QualityLevel >= 0 && st.QualityLevel < QualitySettings.names.Length ? st.QualityLevel : projectQualityLevel;
            if (QualitySettings.GetQualityLevel() != quality) QualitySettings.SetQualityLevel(quality, true);
            QualitySettings.vSyncCount = st.VSync ? 1 : 0;
            Application.targetFrameRate = GridGame.Meta.GameSettings.ClampFrameRateCap(st.FrameRateCap);
            Tween.ShakeScale = Mathf.Clamp01(st.ScreenShake);
            VfxPrefabLayer.Amount = Mathf.Clamp01(st.VfxAmount);
            Tween.Speed = Mathf.Clamp(st.BattleSpeed, 0.5f, 3f);
            // GG-T392: with the look presets hidden (Features.LookPresets off) every profile draws with the defaults.
            bool presets = GridGame.Meta.Features.LookPresets;
            Art.ApplyTheme(presets ? st.MenuTheme : GridGame.Meta.SettingsSave.DefaultMenuTheme);
            Ui.ApplyMenuTheme(Document?.rootVisualElement);
            Fonts.ApplyFont(presets ? st.FontName : GridGame.Meta.SettingsSave.DefaultFontName);
            TextFit.Configure(presets && st.Hyphenate);
            // GG-T495 accessibility: text size scales the one GUI size (GG-T770, --type-base), reduce motion for every shared tween.
            TextSize.Apply(Document?.rootVisualElement, st.TextScale);
            // GG-T1222: UI scale and the phone's thumb margin take effect in the next stage fit (FitStage).
            uiScale = GridGame.Meta.GameSettings.ClampUiScale(st.UiScale);
            thumbMargin = Application.isMobilePlatform ? GridGame.Meta.GameSettings.ClampThumbMargin(st.ThumbMargin) : 0;
            ApplyDisplay(st);
            Tween.Calm = st.ReduceMotion;
            // GG-T525: Colorblind palette swaps the team marks to a blue/orange pair: Palette for marks drawn in code
            // (timeline icons, cast rings), a root class for the board keylines in USS (Theme.uss .colorblind).
            Palette.Colorblind = st.ColorblindMode;
            Document?.rootVisualElement?.EnableInClassList("colorblind", st.ColorblindMode);
        }

        /// <summary>Display → Window mode and Resolution (GG-T1222), desktop players only: the window is left as launched
        /// until the player picks one, and is only reset when it differs from what they picked.</summary>
        private static void ApplyDisplay(GridGame.Meta.SettingsSave st)
        {
            if (Application.isMobilePlatform || Application.isEditor || Application.platform == RuntimePlatform.WebGLPlayer) return;
            bool sized = st.ResolutionWidth > 0 && st.ResolutionHeight > 0;
            if (st.FullScreen < 0 && !sized) return;
            var mode = st.FullScreen < 0 ? Screen.fullScreenMode : st.FullScreen == 1 ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            int w = sized ? st.ResolutionWidth : Screen.width, h = sized ? st.ResolutionHeight : Screen.height;
            if (mode != Screen.fullScreenMode || w != Screen.width || h != Screen.height) Screen.SetResolution(w, h, mode);
        }

        /// <summary>Writes the active profile's settings to its settings.json (a settings change, bible §15.2).</summary>
        public void SaveSettings()
        {
            if (Profile == null) return;
            if (!Profile.SaveSettings()) Toast("Couldn't save settings!");
            SaveStore.SyncFiles();
        }

        // ── Player profiles (bible §15.4, GG-T99) ──────────────────────────────────

        /// <summary>Name of the profile `-demo` / `-testmode` play under, in a throwaway folder outside the real profiles.</summary>
        public const string ThrowawayProfile = "Throwaway";

        /// <summary>
        /// Picks the Profiles folder and the profile, then points <see cref="Saves"/> at that profile's slots:
        /// `-saveRoot DIR` (harness/tests) keeps everything under DIR/Profiles; Unity.Shared's `-profiles-root DIR` /
        /// MINDATTIC_PROFILES_ROOT name the folder outright; `-demo` / `-testmode` use a throwaway folder under the
        /// temp cache. Only the real folder (persistentDataPath/Profiles) gets the one-time legacy migration.
        /// `-profile NAME` selects (or creates) that profile; otherwise the last one used.
        /// </summary>
        private void OpenProfiles()
        {
            var saveRoot = CommandLine.Value("-saveRoot");
            bool throwaway = DemoMode.Active || TestMode.Active;
            Unity.Shared.Core.ProfileStore store;
            if (!string.IsNullOrEmpty(saveRoot)) store = Unity.Shared.Core.Profiles.UseRoot(System.IO.Path.Combine(saveRoot, Unity.Shared.Core.ProfileStore.FolderName));
            else if (CommandLine.DebugOptionsAllowed && Unity.Shared.Core.Profiles.OverrideRoot() != null) store = Unity.Shared.Core.Profiles.UseDataPath(Application.persistentDataPath);
            else if (throwaway)
            {
                var dir = System.IO.Path.Combine(Application.temporaryCachePath, "ThrowawayProfiles");
                try { if (System.IO.Directory.Exists(dir)) System.IO.Directory.Delete(dir, true); }
                catch (Exception e) { Debug.LogWarning("[Profiles] couldn't clear the throwaway folder: " + e.Message); }
                store = Unity.Shared.Core.Profiles.UseRoot(dir);
            }
            else
            {
                // GG-T566: UseDataPath honours the -profiles-root / MINDATTIC_PROFILES_ROOT override itself; a release build
                // without -enableDebugOptions pins the real folder instead.
                store = CommandLine.DebugOptionsAllowed
                    ? Unity.Shared.Core.Profiles.UseDataPath(Application.persistentDataPath)
                    : Unity.Shared.Core.Profiles.UseRoot(System.IO.Path.Combine(Application.persistentDataPath, Unity.Shared.Core.ProfileStore.FolderName));
                try
                {
                    var m = LegacySaveMigration.Run(Application.persistentDataPath, store);
                    if (m.Ran) Debug.Log($"[Profiles] migrated legacy saves into '{Unity.Shared.Core.ProfileStore.DefaultName}': copied [{string.Join(", ", m.Copied)}], kept [{string.Join(", ", m.Skipped)}], settings from slot {m.SeededFromSlot + 1}, old folder now {m.MovedTo}");
                }
                catch (Exception e) { Debug.LogError("[Profiles] legacy save migration failed (the old Saves folder is untouched): " + e.Message); }
            }

            var requested = CommandLine.Value("-profile") ?? (throwaway ? ThrowawayProfile : null);
            try { Profile = new ProfileSession(store, requested); }
            catch (ArgumentException e)
            {
                Debug.LogError($"[Profiles] -profile \"{requested}\": {e.Message} Using the last profile instead.");
                Profile = new ProfileSession(store);
            }
            Saves = new SaveStore(Profile.SavesFolder);
            SaveStore.SyncFiles();
            Debug.Log($"[Profiles] profile '{Profile.Name}' ({store.Root})");
        }

        // ── Account wallet (§25.18.1, GG-T702) ─────────────────────────────────────

        /// <summary>Binds this device's account wallet and moves every slot of every profile into it (GG-T621's
        /// <see cref="WalletMigration"/>), before the real-money store connects, so a re-delivered order and every spender
        /// see one balance. A slot that won't read is left alone (it moves when it is loaded once it reads again).</summary>
        private void OpenWallet()
        {
            var profiles = Profile?.Store;
            if (profiles == null) return;
            try
            {
                WalletStore = AccountWalletStore.For(profiles);
                DiamondExchange.Wallet = WalletStore.Load(WalletLogin);
                DiamondExchange.WalletDirty = false;
                var folders = new List<string> { Saves.Root };
                foreach (var name in profiles.List()) folders.Add(System.IO.Path.Combine(profiles.Root, name, "Saves"));
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var folder in folders)
                {
                    if (!System.IO.Directory.Exists(folder) || !seen.Add(System.IO.Path.GetFullPath(folder))) continue;
                    var saves = string.Equals(System.IO.Path.GetFullPath(folder), System.IO.Path.GetFullPath(Saves.Root), StringComparison.OrdinalIgnoreCase) ? Saves : new SaveStore(folder);
                    for (int i = 0; i < SaveStore.Slots; i++)
                    {
                        if (!saves.Exists(i)) continue;
                        var s = saves.Load(i, Content);
                        if (s != null) MoveToWallet(saves, i, s, false);
                    }
                }
                Debug.Log($"[Wallet] {WalletStore.PathFor(WalletLogin)}: {DiamondExchange.Wallet.Diamonds} Diamonds, {DiamondExchange.Wallet.Granted.Count} purchases, {DiamondExchange.Wallet.Migrated.Count} saves carried in");
            }
            catch (Exception e) { Debug.LogError("[Wallet] couldn't open the account wallet: " + e.Message); }
        }

        /// <summary>Moves a save just loaded (or just made) into the account wallet: the wallet is written first, the save
        /// after (into <paramref name="slot"/> of the active profile, unless it is -1). A failed wallet write leaves the
        /// save as it was, spending from its bag, and it is tried again on the next load. Safe to call on every load.</summary>
        public WalletMigrationResult MoveToWallet(int slot, SaveData s) => MoveToWallet(Saves, slot, s, true);

        private WalletMigrationResult MoveToWallet(SaveStore saves, int slot, SaveData s, bool stamp)
        {
            if (WalletStore == null || DiamondExchange.Wallet == null || s == null) return null;
            if (!PersistWallet()) return null; // anything spent since the last write lands before the file is read back
            var r = WalletMigration.Run(WalletStore, WalletLogin, s, DateTime.UtcNow);
            if (r.Outcome != WalletMigrationOutcome.Failed && r.WalletChanged)
                DiamondExchange.Wallet = WalletStore.Load(WalletLogin);
            string where = $"{saves?.Root} slot {slot + 1}";
            switch (r.Outcome)
            {
                case WalletMigrationOutcome.Moved:
                    Debug.Log($"[Wallet] {where} moved in: {r.Drained} out of the bag, {r.Carried} carried, {r.Purchases} purchases");
                    if (slot >= 0 && saves != null && !saves.Write(slot, s, stamp)) Debug.LogWarning($"[Wallet] {where} moved but not rewritten; the wallet already counts it");
                    break;
                case WalletMigrationOutcome.Failed:
                    Debug.LogWarning($"[Wallet] {where}: the wallet couldn't be written; the save keeps its Diamonds for now");
                    break;
            }
            if (r.Stray > 0) Debug.LogWarning($"[Wallet] {where}: {r.Stray} Diamonds in the bag of a moved save, not carried");
            return r;
        }

        /// <summary>Writes the account wallet if a spend or grant changed it. True when it is on disk (or unchanged).</summary>
        public bool PersistWallet()
        {
            if (!DiamondExchange.WalletDirty || WalletStore == null || DiamondExchange.Wallet == null) return true;
            if (!WalletStore.Save(DiamondExchange.Wallet)) { Debug.LogError("[Wallet] write failed"); return false; }
            DiamondExchange.WalletDirty = false;
            return true;
        }

        /// <summary>Makes <paramref name="name"/> the active profile: its settings apply at once and its slots replace the old ones.</summary>
        public void SwitchProfile(string name)
        {
            Save = null; Slot = -1;
            Profile.Switch(name);
            ProfileChanged();
        }

        public void CreateProfile(string name) { Profile.Create(name); SaveStore.SyncFiles(); }

        public void RenameProfile(string name, string newName)
        {
            Profile.Rename(name, newName);
            ProfileChanged(); // the active profile's folder may have moved
        }

        public void DeleteProfile(string name)
        {
            Profile.Delete(name);
            ProfileChanged();
        }

        private void ProfileChanged()
        {
            Saves = new SaveStore(Profile.SavesFolder);
            SaveStore.SyncFiles();
            ApplySettings();
        }
    }

    /// <summary>
    /// The game's launch flags (-allfeatures, -start, -saveRoot, -profile, -testmode, -demo, -skill, -telemetry, -automation,
    /// -autobattle, ...). Every one of them is a debug/automation option. GG-T566 (owner: "in release mode the debug options
    /// have to be specified explicitly"): in a non-development player they are all ignored — <see cref="Has"/> is false and
    /// <see cref="Value"/> null — unless <c>-enableDebugOptions</c> is also on the command line (Android passes intent extras
    /// as command-line args, so a release APK could otherwise be launched into them). Editor and development builds are
    /// unchanged. One log line says when a release build ignored them.
    /// </summary>
    public static class CommandLine
    {
        public const string EnableDebugOptions = "-enableDebugOptions";
        private static bool? allowed;

        /// <summary>True in the Editor and development builds; in a release build only with <see cref="EnableDebugOptions"/>.</summary>
        public static bool DebugOptionsAllowed
        {
            get
            {
                if (allowed.HasValue) return allowed.Value;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                allowed = true;
#else
                allowed = RawHas(EnableDebugOptions);
                if (!allowed.Value)
                {
                    var flags = new List<string>();
                    foreach (var a in Environment.GetCommandLineArgs())
                        if (a.Length > 1 && a[0] == '-') flags.Add(a);
                    if (flags.Count > 0)
                        Debug.Log($"[CommandLine] release build: ignoring debug launch options ({string.Join(" ", flags)}); add {EnableDebugOptions} to use them");
                }
                else Debug.Log($"[CommandLine] release build: debug launch options enabled by {EnableDebugOptions}");
#endif
                return allowed.Value;
            }
        }

        public static string Value(string flag) => DebugOptionsAllowed ? RawValue(flag) : null;

        public static bool Has(string flag) => DebugOptionsAllowed && RawHas(flag);

        private static string RawValue(string flag)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }

        private static bool RawHas(string flag)
        {
            foreach (var a in Environment.GetCommandLineArgs())
                if (string.Equals(a, flag, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }

    /// <summary>
    /// `-demo`: boots straight into a scripted self-play loop (new game → battle, autoplayed → next
    /// stage → …, looping with a fresh save when the campaign ends or nothing is launchable) — this
    /// is the tour, the same flag name used for it in every MindAttic Unity project. The same name
    /// tools/harness.py campaign drives over HTTP. (The narrower, pre-existing meaning of "auto-resolve
    /// the current battle" was renamed to `-autobattle` — see BattleScreen.AutoPlay.)
    /// </summary>
    public static class DemoMode
    {
        public static bool Active => CommandLine.Has("-demo");
        /// <summary>`-demo -endless` (GG-T57): see <see cref="EndlessDemo"/>.</summary>
        public static bool Endless => Active && CommandLine.Has("-endless");

        public static void Start()
        {
            if (Endless) { EndlessDemo.Start(); return; }
            GameApp.I.Router.ResetTo(new TitleScreen());
            Restart();
        }

        /// <summary>Called from PostBattleScreen once the results are up: does whatever a human would click next.</summary>
        public static void AfterBattle(BattleReport report, string stageId)
        {
            if (Endless) { EndlessDemo.AfterBattle(report, stageId); return; }
            var app = GameApp.I;
            var c = app.Content;
            if (!report.Victory)
            {
                var theme = c.ThemeOf(stageId);
                string retryId = report.ExpeditionFailed && theme != null ? theme.Stages[0] : stageId;
                StageLauncher.Launch(retryId, true);
                return;
            }
            if (report.NextStageId != null) { StageLauncher.Launch(report.NextStageId, true); return; }
            var stages = c.CampaignStages();
            int idx = stages.FindIndex(s => s.Id == stageId);
            if (idx >= 0 && idx + 1 < stages.Count && Progression.IsStageUnlocked(app.Save, idx + 1) && Expeditions.CanLaunch(c, app.Save, stages[idx + 1].Id, out _))
            {
                StageLauncher.Launch(stages[idx + 1].Id, true);
                return;
            }
            Restart(); // campaign complete, or nothing left to launch — loop with a fresh save
        }

        /// <summary>Unlocks every class and shows the fighting-game-style select screen, which drives itself
        /// in demo mode: random tiles, so each run shows a different cross-section of the roster.</summary>
        private static void Restart()
        {
            GameApp.I.Router.Push(new CharacterSelectScreen(BeginRun, auto: true));
        }

        private static void BeginRun(List<string> party)
        {
            var app = GameApp.I;
            app.Save = Progression.NewGame(app.Content, "Demo", party);
            app.Slot = -1;
            // ResetTo fades out the still-visible CharacterSelectScreen before its mutate actually applies
            // (ScreenRouter.Go only mutates the stack synchronously when nothing is on screen yet), so pushing
            // the first stage immediately after would race it and land on top of the stale stack, then get wiped
            // out when the delayed Hub mutate finally runs. Wait for Hub to actually land before launching.
            void OnShown(GameScreen shown)
            {
                if (shown is not HubScreen) return;
                app.Router.Changed -= OnShown;
                var stages = app.Content.CampaignStages();
                if (stages.Count > 0) StageLauncher.Launch(stages[0].Id, false);
            }
            app.Router.Changed += OnShown;
            app.Router.ResetTo(new HubScreen());
        }
    }

    /// <summary>
    /// `-demo -endless` (GG-T57): an endless stream of fresh synthetic encounters with no Title, Hub, Camp or crawl —
    /// generate an encounter (<see cref="EncounterGenerator"/>, GG-T55) and a level-appropriate geared party for it
    /// (<see cref="EncounterParty"/>, GG-T56) → autoplayed battle → results (auto-advance) → the next encounter,
    /// forever. `-seed S` sets the base seed (default: the clock) and encounter i (0-based) uses seed S+i, so a run
    /// is reproducible encounter by encounter; `-count N` stops cleanly (quits) after N encounters. Every encounter
    /// runs on its own throwaway in-memory save on slot -1 (Persist never writes it, so no real slot is touched),
    /// and each synthetic `endless-` stage is pruned from the live ContentDb once it's done (the GG-T23 rule: a
    /// throwaway stage must never pile up in, or be borrowed back out of, the content).
    /// </summary>
    public static class EndlessDemo
    {
        /// <summary>Encounters finished (results reached).</summary>
        public static int Completed { get; private set; }
        private static int baseSeed;
        private static int? count;

        public static void Start()
        {
            var seedArg = CommandLine.Value("-seed");
            baseSeed = seedArg != null && int.TryParse(seedArg, out var s) ? s : Environment.TickCount;
            var countArg = CommandLine.Value("-count");
            count = countArg != null && int.TryParse(countArg, out var n) && n > 0 ? n : (int?)null;
            Completed = 0;
            Debug.Log($"[EndlessDemo] started: base seed {baseSeed}, {(count.HasValue ? count + " encounters" : "endless")}");
            Next();
        }

        /// <summary>Called from PostBattleScreen once the results are up.</summary>
        public static void AfterBattle(BattleReport report, string stageId)
        {
            Completed++;
            // GG-T61: an encounter the stall guard ended (EndlessStability) reports "stalled", not a defeat.
            Debug.Log($"[EndlessDemo] encounter {Completed} ({stageId}): {(EndlessStability.IsStalled(report) ? "stalled" : report.Victory ? "victory" : report.Escaped ? "escaped" : report.Retreated ? "retreated" : "defeat")}");
            if (count.HasValue && Completed >= count.Value) { Stop(); return; }
            Next();
        }

        private static void Next()
        {
            var app = GameApp.I;
            var c = app.Content;
            int seed = unchecked(baseSeed + Completed);
            // GG-T64: the same pure seed -> encounter build a headless Core replay uses (EndlessEncounter.Replay), so
            // `-demo -endless -seed S -count 1` reproduces exactly the encounter a record/report flagged with seed S.
            // Build prunes the previous encounter's stage first — safe now: its results screen reads name/theme from the report.
            var setup = EndlessEncounter.Build(c, seed, TurnModeSetting.Resolve(null, app.Settings)); // §2.13 (GG-T773): the profile's mode
            var stage = setup.Stage;

            // A fresh throwaway save per encounter: BattleFactory.Commit has somewhere to land (so the results screen
            // shows), nothing accumulates across encounters, and slot -1 means Persist never writes it.
            app.Save = Progression.NewGame(c, "Endless");
            app.Slot = -1;
            // The view's own randomness (the simulated cursor's overshoot/slip rolls) starts from the encounter seed too.
            UnityEngine.Random.InitState(seed);
            Debug.Log($"[EndlessDemo] encounter {Completed + 1} seed {seed}: {stage.Id} ({stage.Name}) Lv {stage.LevelOverride ?? 1} | party {string.Join(", ", setup.Party.ConvertAll(h => h.ClassId))}");
            // ResetTo, not Push: the stack never grows however long this runs. The autoplay bot's RNG is seeded from
            // the encounter seed (GG-T64), so its decisions match EndlessEncounter.Bot(seed) in a headless replay.
            app.Router.ResetTo(new BattleScreen(setup) { BotSeed = seed });
        }

        private static void Stop()
        {
            EndlessEncounter.Prune(GameApp.I.Content);
            Debug.Log($"[EndlessDemo] done: {Completed} encounters");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }

    /// <summary>
    /// `-testmode` (GG-T93, bot-only): self-plays the whole mechanical loop with no story, crawl, overworld
    /// or tutorial, to verify the game end to end — throwaway save on slot -1 (Persist never writes it) with
    /// the starting party → launch a stage → autoplayed battle → results → back to the Hub → CampRoutine
    /// (repair, Inn, potions, forge/equip, learn/assign) → launch again, forever. Unlike `-demo`, it visits
    /// camp and spends between every fight. Enemy level follows the party (Progression.StageRecommendedLevel)
    /// and stage choice lives in Meta (TestLoop.PickStage); this class only drives screens and logs one
    /// "[TestMode] cycle N | …" line per cycle (stage, party/enemy level, result, XP, gold, mats, camp actions).
    /// </summary>
    public static class TestMode
    {
        public static bool Active => CommandLine.Has("-testmode");
        /// <summary>Cycles finished (camp done and logged) — reported by the automation state dump.</summary>
        public static int Completed { get; private set; }
        public static string LastLine { get; private set; }
        private static TestCycle current;
        private static int losses;
        private static bool learned;

        public static void Start()
        {
            var app = GameApp.I;
            app.Save = TestLoop.NewSave(app.Content);
            app.Slot = -1;
            app.Settings.BattleSpeed = 3f; // the throwaway profile's settings, in memory only
            app.ApplySettings();
            Debug.Log($"[TestMode] started: throwaway save (slot -1), party {string.Join(", ", app.Save.Party)}, {app.Save.Gold}g");
            GoToCamp(runRoutine: false);
        }

        /// <summary>Called from PostBattleScreen once the results are up.</summary>
        public static void AfterBattle(BattleReport report, string stageId)
        {
            var app = GameApp.I;
            current?.Battle(app.Content, app.Save, report);
            losses = report.Victory ? 0 : losses + 1;
            GoToCamp(runRoutine: true);
        }

        private static void GoToCamp(bool runRoutine)
        {
            var app = GameApp.I;
            void OnShown(GameScreen shown)
            {
                if (shown is not HubScreen hub) return;
                app.Router.Changed -= OnShown;
                app.StartCoroutine(Camp(hub, runRoutine));
            }
            app.Router.Changed += OnShown;
            app.Router.ResetTo(new HubScreen());
        }

        private static IEnumerator Camp(HubScreen hub, bool runRoutine)
        {
            var app = GameApp.I;
            yield return new WaitForSecondsRealtime(1.0f);
            if (runRoutine && current != null)
            {
                current.RunCamp(app.Content, app.Save);
                LastLine = current.ToString();
                Completed++;
                Debug.Log(LastLine);
                learned |= TestLoop.LearnedAnAbility(current.Camp);
                if (!learned && Completed == TestLoop.CyclesThatMustLearnAnAbility)
                    Debug.LogWarning($"[TestMode] check failed: {Completed} cycles and camp never learned an ability (GG-T100)");
                if (app.Router.Current == hub) hub.Refresh();
                yield return new WaitForSecondsRealtime(1.5f);
            }
            if (app.Router.Current != hub) yield break; // something else took over the screen; don't fight it
            Launch();
        }

        private static void Launch()
        {
            var app = GameApp.I;
            var c = app.Content; var s = app.Save;
            string stageId = TestLoop.PickStage(c, s, losses);
            if (stageId != null && !Expeditions.CanLaunch(c, s, stageId, out var why))
            {
                Debug.Log($"[TestMode] can't launch {stageId} ({why}) — abandoning the expedition");
                Expeditions.Abandon(s);
                stageId = TestLoop.PickStage(c, s, losses);
            }
            if (stageId == null) { Debug.LogError("[TestMode] no launchable stage"); return; }
            current = TestCycle.Begin(c, s, stageId, Completed + 1);
            Debug.Log($"[TestMode] cycle {current.Number} launch {stageId} | party Lv {current.PartyLevel} | enemy Lv {current.EnemyMin}-{current.EnemyMax}");
            StageLauncher.Launch(stageId, false);
        }
    }
}
