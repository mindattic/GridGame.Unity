using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using GridGame.Core.Battle;
using GridGame.Meta;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>
    /// GG-T61: keeps a `-demo -endless` run stable for hundreds of encounters in one process. Installs itself (like
    /// <see cref="EndlessCapture"/>) and does nothing outside `-demo -endless`.
    ///
    /// <para><b>Teardown.</b> Once the next encounter's battle is on screen, the previous <see cref="BattleScreen"/>
    /// is torn down completely (<see cref="BattleScreen.TearDownAfterEncounter"/>: its bots, its whole visual tree —
    /// board, FX layer, cut-in cards, timeline icons and their scheduled pulses — and the global pause flag), every
    /// sound effect still ringing is stopped (the SFX pool is fixed, never grown), and it is tracked by a weak
    /// reference so a screen something still holds on to shows up as a leak.</para>
    ///
    /// <para><b>Memory.</b> Every <see cref="SampleEvery"/> encounters (and at the start, as a baseline) it unloads
    /// unused assets, forces a full GC and logs one <c>[EndlessStability]</c> line — managed heap, Mono, Unity
    /// native allocated/reserved, graphics, process private bytes and working set, plus object counts (all Unity
    /// objects, textures, GameObjects, AudioSources, visual elements in the panel, Overlay children, torn-down
    /// battle screens still alive, prefab-FX camera rigs and RenderTextures) — and appends it to
    /// <c>endless-stability.csv</c> (in `-artifacts` or persistentDataPath). From the second warmed-up sample on, the
    /// line also carries the slope in MB per encounter of managed, native and private memory since encounter
    /// <see cref="SampleEvery"/>, and a closing <c>final</c> row is written at quit (GG-T587). A metric that rose at
    /// every one of the last <see cref="GrowthWindow"/> samples (and by a real amount) gets a <c>monotonic growth</c>
    /// warning.</para>
    ///
    /// <para><b>Stall guard.</b> An encounter that runs past <c>-stallTurns</c> actions (default
    /// <see cref="DefaultTurnCap"/>; one -demo battle has needed 412), <c>-stallSeconds</c> of real time (default
    /// <see cref="DefaultSecondsCap"/>), or sits idle — no action taken and nothing animating — for
    /// <see cref="FrozenSecondsCap"/> s is ended as <c>"stalled"</c> (<see cref="BattleScreen.EndStalled"/>: battle
    /// record saved, telemetry session closed with a "stalled" event/outcome) and the loop moves on to the next
    /// encounter instead of hanging.</para>
    ///
    /// <para><b>GG-T21.</b> To leave a trail if the process ever vanishes again, <c>endless-heartbeat.txt</c> is
    /// rewritten every few seconds with the encounter in flight and the latest memory figures, and unhandled
    /// exceptions, Unity's low-memory signal and the quit itself are logged.</para>
    /// </summary>
    public sealed class EndlessStability : MonoBehaviour
    {
        // GG-T588: the caps are battle tuning, so they live in BattleConfig with every other number.
        public const int DefaultTurnCap = BattleConfig.StallActionCap;
        public const float DefaultSecondsCap = BattleConfig.StallRealSecondsCap;
        public const float FrozenSecondsCap = BattleConfig.StallIdleSecondsCap;
        public const int SampleEvery = 10;
        public const int GrowthWindow = 5;
        private const float HeartbeatSeconds = 5f;

        private static EndlessStability instance;
        private static BattleReport stalledReport;

        /// <summary>True for the report the stall guard handed the endless loop (its result line says "stalled").</summary>
        public static bool IsStalled(BattleReport report) => report != null && ReferenceEquals(report, stalledReport);

        /// <summary>Encounters ended by the stall guard this run.</summary>
        public static int Stalls => instance != null ? instance.stalls : 0;

        private int turnCap;
        private float secondsCap;
        private ScreenRouter router;
        private BattleScreen current;
        private int currentIndex;
        private float currentStart, lastProgress;
        private int lastActions = -1;
        private int stalls;
        private int lastSampledAt = -1;
        private bool sampling;
        private readonly List<WeakReference> finished = new List<WeakReference>();
        private readonly List<double[]> samples = new List<double[]>();
        private string dir, csvPath, heartbeatPath;
        private float nextHeartbeat;
        private string lastSampleLine = "(none yet)";

        // GG-T375 `-leakprobe`: every screen shown and every battle state built, held weakly, counted after each encounter.
        private bool leakProbe;
        private readonly List<WeakReference> probeScreens = new List<WeakReference>();
        private readonly List<WeakReference> probeStates = new List<WeakReference>();

        private static readonly string[] Metrics =
        {
            "managedMB", "monoMB", "nativeAllocMB", "nativeReservedMB", "gfxMB", "privateMB", "workingSetMB",
            "unityObjects", "textures", "gameObjects", "audioSources", "visualElements", "overlayChildren", "liveOldScreens",
            "fxRigs", "renderTextures",
        };
        /// <summary>Growth below this (per metric, over the whole window) is noise, not a leak.</summary>
        private static readonly double[] MinGrowth = { 4, 4, 8, 8, 8, 32, 32, 50, 5, 1, 1, 50, 1, 1, 1, 1 };
        /// <summary>Metrics (indices into <see cref="Metrics"/>) whose overall slope per encounter is logged: managed,
        /// native allocated, process private bytes.</summary>
        private static readonly int[] SlopeMetrics = { 0, 2, 5 };
        /// <summary>The first sample the trend is measured from: encounter <see cref="SampleEvery"/>, once the first-load
        /// art/audio caches have warmed up (the encounter-0 baseline is before any battle was built).</summary>
        private int trendFrom = -1;
        private readonly List<int> sampleEncounters = new List<int>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (instance != null || !DemoMode.Endless) return;
            var go = new GameObject("GridGame.EndlessStability") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            instance = go.AddComponent<EndlessStability>();
        }

        private void Awake()
        {
            turnCap = int.TryParse(CommandLine.Value("-stallTurns"), out var t) && t > 0 ? t : DefaultTurnCap;
            secondsCap = float.TryParse(CommandLine.Value("-stallSeconds"), NumberStyles.Float, CultureInfo.InvariantCulture, out var s) && s > 0 ? s : DefaultSecondsCap;
            dir = CommandLine.Value("-artifacts") ?? Application.persistentDataPath;
            try
            {
                Directory.CreateDirectory(dir);
                csvPath = Path.Combine(dir, "endless-stability.csv");
                heartbeatPath = Path.Combine(dir, "endless-heartbeat.txt");
                File.WriteAllText(csvPath, "encounter,minutes,stalls," + string.Join(",", Metrics) + "\n");
            }
            catch (Exception e) { Debug.LogWarning("[EndlessStability] can't write to " + dir + ": " + e.Message); csvPath = heartbeatPath = null; }
            leakProbe = CommandLine.Has("-leakprobe");
            if (leakProbe) Debug.Log("[LeakProbe] on: after each encounter, a full GC and one [LeakProbe] line of live screens/battle states and the likely-culprit collections");
            Application.lowMemory += OnLowMemory;
            Application.quitting += OnQuitting;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandled;
            Debug.Log($"[EndlessStability] on: stall cap {turnCap} actions / {secondsCap:0}s real time / {FrozenSecondsCap:0}s idle; memory + object counts every {SampleEvery} encounters ({csvPath ?? "no csv"})");
        }

        private void OnDestroy()
        {
            Application.lowMemory -= OnLowMemory;
            Application.quitting -= OnQuitting;
            AppDomain.CurrentDomain.UnhandledException -= OnUnhandled;
            if (router != null) router.Changed -= OnScreenChanged;
        }

        private void Update()
        {
            if (router == null && GameApp.I != null && GameApp.I.Router != null)
            {
                router = GameApp.I.Router;
                router.Changed += OnScreenChanged;
                StartCoroutine(Sample(0)); // baseline
            }
            float now = Time.realtimeSinceStartup;
            if (now >= nextHeartbeat) { nextHeartbeat = now + HeartbeatSeconds; Heartbeat(); }
            if (current == null || router == null) return;
            if (ReferenceEquals(router.Current, current)) Guard(now);
            else lastProgress = now; // a screen pushed over the battle (a dialogue) isn't the battle sitting idle
        }

        // ── Encounter boundaries + teardown ──────────────────────────────────────────

        private void OnScreenChanged(GameScreen shown)
        {
            if (leakProbe && shown != null && !probeScreens.Exists(w => ReferenceEquals(w.Target, shown)))
            {
                probeScreens.Add(new WeakReference(shown));
                if (shown is BattleScreen b && b.State != null) probeStates.Add(new WeakReference(b.State));
            }
            if (shown is not BattleScreen battle || ReferenceEquals(battle, current)) return;
            if (current != null) Retire(current);
            current = battle;
            currentIndex = EndlessDemo.Completed + 1;
            currentStart = lastProgress = Time.realtimeSinceStartup;
            lastActions = -1;
            int done = EndlessDemo.Completed;
            if (done > 0 && done % SampleEvery == 0 && done != lastSampledAt && !sampling) StartCoroutine(Sample(done));
        }

        /// <summary>The previous encounter's battle is off screen for good: tear it down and watch that it's freed.</summary>
        private void Retire(BattleScreen old)
        {
            try { old.TearDownAfterEncounter(); }
            catch (Exception e) { Debug.LogWarning("[EndlessStability] teardown: " + e.Message); }
            GameApp.I?.Audio?.StopSfx();
            finished.Add(new WeakReference(old));
            if (finished.Count > 64) finished.RemoveAll(w => !w.IsAlive);
            if (leakProbe) StartCoroutine(Probe(EndlessDemo.Completed));
        }

        /// <summary>
        /// GG-T375 `-leakprobe`: one line per encounter, after a full GC, of what is still alive — screens by type (the
        /// one on screen and the battle just retired are expected; anything older is a leak), battle states, the capture
        /// engines EndlessCapture holds (it used to keep every restarted engine and its ~22 MB frame pool), and the
        /// bounded caches (Art by path, the SFX cue log, the BoardAudit tail, Overlay children).
        /// </summary>
        private IEnumerator Probe(int encounter)
        {
            yield return null; // let the teardown and the new screen's first frame settle
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            probeScreens.RemoveAll(w => !w.IsAlive);
            probeStates.RemoveAll(w => !w.IsAlive);
            var byType = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (var w in probeScreens)
            {
                var t = w.Target;
                if (t == null) continue;
                var n = t.GetType().Name;
                byType[n] = byType.TryGetValue(n, out var c) ? c + 1 : 1;
            }
            var app = GameApp.I;
            var screens = new StringBuilder();
            foreach (var kv in byType) screens.Append(screens.Length == 0 ? "" : ",").Append(kv.Key).Append('=').Append(kv.Value);
            Debug.Log($"[LeakProbe] encounter {encounter}: managedMB={GC.GetTotalMemory(false) / 1048576.0:0.0} " +
                      $"screens{{{screens}}} battleStates={probeStates.Count} " +
                      $"captureEngines={EndlessCapture.EnginesHeld} captureRestarts={EndlessCapture.Restarts} captureEncounters={EndlessCapture.EncountersHeld} " +
                      $"artTextures={Art.CachedTextures} artStage={Art.CountInScope(ArtScope.Stage)} artScreen={Art.CountInScope(ArtScope.Screen)} " +
                      $"textureMB={Texture.currentTextureMemory / 1048576.0:0.0} midBattleLoads={Art.MidBattleLoads} sfxCueLog={app?.Audio?.CueLog.Count ?? 0} boardAuditRecent={BoardAudit.Recent.Count} " +
                      $"overlayChildren={app?.Overlay?.childCount ?? 0} visualElements={CountElements(app?.Document != null ? app.Document.rootVisualElement : null)}");
        }

        // ── Stall guard ──────────────────────────────────────────────────────────────

        private void Guard(float now)
        {
            var state = current.State;
            if (state == null || state.IsOver || current.StabilityEnded) return;
            int actions = state.Results.ActionsTaken;
            if (actions != lastActions || current.StabilityBusy || Tween.Paused) { lastActions = actions; lastProgress = now; }
            float elapsed = now - currentStart;
            string reason =
                actions > turnCap ? $"turn cap: {actions} actions > {turnCap}" :
                elapsed > secondsCap ? $"real-time cap: {elapsed:0}s > {secondsCap:0}s" :
                now - lastProgress > FrozenSecondsCap ? $"idle {now - lastProgress:0}s with no action and nothing animating" :
                null;
            if (reason == null) return;
            var battle = current;
            if (!battle.EndStalled(reason, elapsed)) return;
            stalls++;
            Debug.LogWarning($"[EndlessStability] encounter {currentIndex} ({state.Stage.Id}) STALLED — {reason}; wave {state.Wave + 1}, clock {state.Results.TicksElapsed}. Ended as 'stalled' and recorded to telemetry; moving on.");
            var report = battle.StalledReport();
            stalledReport = report;
            DemoMode.AfterBattle(report, state.Stage.Id);
        }

        // ── Memory / object sampling ─────────────────────────────────────────────────

        private IEnumerator Sample(int encounter)
        {
            sampling = true;
            lastSampledAt = encounter;
            yield return Art.ReleaseAndCollect(); // GG-T1203: Art's one release-and-collect (UnloadUnusedAssets + full GC)
            if (!Record(encounter.ToString(CultureInfo.InvariantCulture), encounter)) { sampling = false; yield break; }
            CheckGrowth(encounter);
            sampling = false;
        }

        /// <summary>Measures (the caller has already collected), logs one line, appends one csv row and keeps the sample.
        /// <paramref name="label"/> is the csv's encounter column ("final" for the closing sample at quit).</summary>
        private bool Record(string label, int encounter)
        {
            double[] v;
            try { v = Measure(); }
            catch (Exception e) { Debug.LogWarning("[EndlessStability] sample failed: " + e.Message); return false; }
            samples.Add(v);
            sampleEncounters.Add(encounter);
            if (trendFrom < 0 && encounter >= SampleEvery) trendFrom = samples.Count - 1;
            var line = new StringBuilder($"[EndlessStability] encounter {label} @ {Time.realtimeSinceStartup / 60f:0.0} min, {stalls} stalled:");
            for (int i = 0; i < Metrics.Length; i++) line.Append(' ').Append(Metrics[i]).Append('=').Append(v[i].ToString(i < 7 ? "0.0" : "0", CultureInfo.InvariantCulture));
            var trend = Trend();
            if (trend != null) line.Append(" | ").Append(trend);
            lastSampleLine = line.ToString();
            Debug.Log(lastSampleLine);
            if (csvPath != null)
            {
                try
                {
                    var row = new StringBuilder();
                    row.Append(label).Append(',').Append((Time.realtimeSinceStartup / 60f).ToString("0.00", CultureInfo.InvariantCulture)).Append(',').Append(stalls);
                    foreach (var x in v) row.Append(',').Append(x.ToString("0.###", CultureInfo.InvariantCulture));
                    File.AppendAllText(csvPath, row.Append('\n').ToString());
                }
                catch (Exception e) { Debug.LogWarning("[EndlessStability] csv: " + e.Message); }
            }
            return true;
        }

        /// <summary>GG-T587: the overall slope since the warmed-up sample (encounter <see cref="SampleEvery"/>) of the
        /// managed heap, native allocations and private bytes, in MB per encounter — the number a leak hunt starts from
        /// (the GG-T375 leak read ~8-9 MB/encounter of managed heap). Null until there are two warmed-up samples.</summary>
        private string Trend()
        {
            int last = samples.Count - 1;
            if (trendFrom < 0 || last <= trendFrom) return null;
            int span = sampleEncounters[last] - sampleEncounters[trendFrom];
            if (span <= 0) return null;
            var sb = new StringBuilder($"trend since encounter {sampleEncounters[trendFrom]} (MB/encounter):");
            foreach (int m in SlopeMetrics)
                sb.Append(' ').Append(Metrics[m]).Append('=')
                  .Append(((samples[last][m] - samples[trendFrom][m]) / span).ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        private double[] Measure()
        {
            const double MB = 1024.0 * 1024.0;
            var app = GameApp.I;
            int liveOld = 0;
            foreach (var w in finished) if (w.IsAlive && !ReferenceEquals(w.Target, current)) liveOld++;
            ProcessMemory(out long priv, out long ws);
            return new double[]
            {
                GC.GetTotalMemory(false) / MB,
                Profiler.GetMonoUsedSizeLong() / MB,
                Profiler.GetTotalAllocatedMemoryLong() / MB,
                Profiler.GetTotalReservedMemoryLong() / MB,
                Profiler.GetAllocatedMemoryForGraphicsDriver() / MB,
                priv / MB,
                ws / MB,
                Resources.FindObjectsOfTypeAll<UnityEngine.Object>().Length,
                Resources.FindObjectsOfTypeAll<Texture>().Length,
                Resources.FindObjectsOfTypeAll<GameObject>().Length,
                Resources.FindObjectsOfTypeAll<AudioSource>().Length,
                CountElements(app?.Document != null ? app.Document.rootVisualElement : null),
                app?.Overlay?.childCount ?? 0,
                liveOld,
                CountFxRigs(),
                Resources.FindObjectsOfTypeAll<RenderTexture>().Length,
            };
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessMemoryCounters
        {
            public uint cb;
            public uint PageFaultCount;
            public UIntPtr PeakWorkingSetSize, WorkingSetSize, QuotaPeakPagedPoolUsage, QuotaPagedPoolUsage,
                QuotaPeakNonPagedPoolUsage, QuotaNonPagedPoolUsage, PagefileUsage, PeakPagefileUsage, PrivateUsage;
        }

        [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll", EntryPoint = "K32GetProcessMemoryInfo")]
        private static extern bool GetProcessMemoryInfo(IntPtr process, ref ProcessMemoryCounters counters, uint size);

        /// <summary>Process private bytes and working set. The player's System.Diagnostics.Process reports 0 for both
        /// (the first smoke run's baseline), so on Windows this asks the OS directly; 0 where neither works.</summary>
        private static void ProcessMemory(out long priv, out long ws)
        {
            priv = ws = 0;
            if (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor)
            {
                try
                {
                    var c = new ProcessMemoryCounters { cb = (uint)Marshal.SizeOf(typeof(ProcessMemoryCounters)) };
                    if (GetProcessMemoryInfo(GetCurrentProcess(), ref c, c.cb))
                    {
                        priv = (long)c.PrivateUsage.ToUInt64();
                        ws = (long)c.WorkingSetSize.ToUInt64();
                        if (priv > 0) return;
                    }
                }
                catch (Exception) { /* fall through */ }
            }
            try { using (var p = System.Diagnostics.Process.GetCurrentProcess()) { priv = p.PrivateMemorySize64; ws = p.WorkingSet64; } }
            catch { /* not available on every platform */ }
        }

        /// <summary>Prefab-FX camera rigs alive (each battle builds one and disposes it when it's hidden or torn down,
        /// GG-T586), so anything above 1 is a battle whose FX layer was never released.</summary>
        private static int CountFxRigs()
        {
            int n = 0;
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
                if (go.name == "VfxPrefabRig" && go.scene.IsValid()) n++;
            return n;
        }

        private static int CountElements(VisualElement root)
        {
            if (root == null) return 0;
            int n = 0;
            var stack = new Stack<VisualElement>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var e = stack.Pop();
                n++;
                for (int i = 0; i < e.hierarchy.childCount; i++) stack.Push(e.hierarchy[i]);
            }
            return n;
        }

        /// <summary>Warns for each metric that rose at every one of the last <see cref="GrowthWindow"/> samples.
        /// The baseline (encounter 0, before any battle was built) is left out: everything grows from it.</summary>
        private void CheckGrowth(int encounter)
        {
            int first = samples.Count - 1 - GrowthWindow;
            if (first < 1) return;
            for (int m = 0; m < Metrics.Length; m++)
            {
                bool rising = true;
                for (int i = first + 1; i < samples.Count && rising; i++) rising = samples[i][m] > samples[i - 1][m];
                double from = samples[first][m], to = samples[samples.Count - 1][m];
                if (!rising || to - from < Math.Max(MinGrowth[m], Math.Abs(from) * 0.02)) continue;
                Debug.LogWarning($"[EndlessStability] WARNING monotonic growth in {Metrics[m]}: rose at each of the last {GrowthWindow} samples ({SampleEvery * GrowthWindow} encounters, up to encounter {encounter}): {from:0.#} → {to:0.#}");
            }
        }

        // ── GG-T21 trail ─────────────────────────────────────────────────────────────

        private void Heartbeat()
        {
            if (heartbeatPath == null) return;
            try
            {
                var st = current?.State;
                File.WriteAllText(heartbeatPath,
                    $"time {DateTime.Now:yyyy-MM-dd HH:mm:ss}  up {Time.realtimeSinceStartup / 60f:0.0} min\n" +
                    $"encounters done {EndlessDemo.Completed}, stalled {stalls}\n" +
                    (st != null ? $"in flight: encounter {currentIndex} {st.Stage?.Id} wave {st.Wave + 1} actions {st.Results.ActionsTaken} clock {st.Results.TicksElapsed} for {Time.realtimeSinceStartup - currentStart:0}s\n" : "in flight: none\n") +
                    $"screen {router?.Current?.Id ?? "none"}\n" +
                    $"managed {GC.GetTotalMemory(false) / 1048576.0:0.0} MB, native {Profiler.GetTotalAllocatedMemoryLong() / 1048576.0:0.0} MB\n" +
                    $"last sample: {lastSampleLine}\n");
            }
            catch { /* best effort */ }
        }

        private void OnLowMemory() =>
            Debug.LogWarning($"[EndlessStability] Unity low-memory signal at encounter {currentIndex}: managed {GC.GetTotalMemory(false) / 1048576.0:0.0} MB, native {Profiler.GetTotalAllocatedMemoryLong() / 1048576.0:0.0} MB");

        private void OnUnhandled(object sender, UnhandledExceptionEventArgs e) =>
            Debug.LogError($"[EndlessStability] unhandled exception (terminating={e.IsTerminating}) at encounter {currentIndex}: {e.ExceptionObject}");

        private void OnQuitting()
        {
            // GG-T587: a closing sample, so a `-count N` run that stops between the every-10 samples still ends its csv
            // on its true last figures (no UnloadUnusedAssets here: it can't be awaited while quitting).
            if (!sampling && EndlessDemo.Completed > lastSampledAt)
            {
                try
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                    lastSampledAt = EndlessDemo.Completed;
                    Record("final", EndlessDemo.Completed);
                }
                catch (Exception e) { Debug.LogWarning("[EndlessStability] final sample: " + e.Message); }
            }
            Debug.Log($"[EndlessStability] quitting after {EndlessDemo.Completed} encounters, {stalls} stalled; last sample: {lastSampleLine}");
            Heartbeat();
        }
    }
}
