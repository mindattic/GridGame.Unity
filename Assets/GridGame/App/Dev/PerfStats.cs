using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace GridGame.App.Dev
{
    /// <summary>
    /// GG-T2101: the BIBLE §30 budget, measured. Samples every frame's time, the GC allocation in it, texture memory and the
    /// UI Toolkit update cost; the automation <c>perf</c> route reads a window of them (reset with <c>perf?reset=1</c>) and the
    /// Settings &gt; Developer &gt; Perf HUD switch shows a live corner readout. The HUD lives in this device's PlayerPrefs and
    /// starts off. GC alloc, texture memory and the UI markers come from profiler counters a development build records; a
    /// release build falls back to <see cref="Texture.currentTextureMemory"/> and the managed heap's growth.
    /// </summary>
    public static class PerfStats
    {
        private const string HudKey = "gridgame.dev.perfhud";
        /// <summary>The longest window the perf route keeps (ten minutes at 60 fps).</summary>
        private const int WindowCap = 36000;
        private const float HudRefreshSeconds = 0.25f;
        private static readonly string[] UiMarkers =
            { "UIElements.UpdateStyle", "UIElements.UpdateLayout", "UIElements.UpdateRenderData", "UIElements.UpdateAnimation" };

        private static ProfilerRecorder gcAlloc, texMemory, mainThread, systemUsed;
        private static ProfilerRecorder[] ui;
        private static readonly List<float> frameMs = new List<float>(WindowCap);
        private static readonly List<float> uiMs = new List<float>(WindowCap);
        private static long windowGc, lastHeap;
        private static int gcFrames;
        private static float windowStart, hudNext;
        private static float hudMs, hudGcKb;
        private static int hudFrames;
        private static Label hud;

        /// <summary>Seconds from engine start to the first screen past the splash (0 until then).</summary>
        public static float ColdStartSeconds { get; private set; }

        public static bool HudOn => DevOptions.Unlocked && PlayerPrefs.GetInt(HudKey, 0) == 1;

        public static void SetHud(bool on)
        {
            PlayerPrefs.SetInt(HudKey, on ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>Called once when the app's stage exists: starts the counters and samples every tick.</summary>
        public static void Install(GameApp app)
        {
            gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            texMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Texture Memory");
            systemUsed = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");
            mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread");
            ui = new ProfilerRecorder[UiMarkers.Length];
            for (int i = 0; i < UiMarkers.Length; i++) ui[i] = ProfilerRecorder.StartNew(ProfilerCategory.Gui, UiMarkers[i]);
            lastHeap = GC.GetTotalMemory(false);
            Reset();
            app.Ticked += _ => Sample(app);
        }

        /// <summary>Starts a fresh measuring window.</summary>
        public static void Reset()
        {
            frameMs.Clear();
            uiMs.Clear();
            windowGc = 0;
            gcFrames = 0;
            windowStart = Time.realtimeSinceStartup;
        }

        private static void Sample(GameApp app)
        {
            float ms = Time.unscaledDeltaTime * 1000f;
            long gc = FrameGcBytes();
            if (frameMs.Count < WindowCap)
            {
                frameMs.Add(ms);
                if (UiValid) uiMs.Add(UiFrameMs());
                windowGc += gc;
                if (gc > 0) gcFrames++;
            }
            if (ColdStartSeconds <= 0f && app.Router.Current != null && app.Router.Current.Id != "splash" && !app.Router.Busy)
                ColdStartSeconds = Time.realtimeSinceStartup;
            Hud(app, ms, gc);
        }

        private static long FrameGcBytes()
        {
            if (gcAlloc.Valid) return gcAlloc.LastValue;
            long heap = GC.GetTotalMemory(false);
            long grew = heap - lastHeap;
            lastHeap = heap;
            return grew > 0 ? grew : 0;
        }

        private static bool UiValid => ui != null && ui.Length > 0 && ui[0].Valid;

        private static float UiFrameMs()
        {
            long ns = 0;
            foreach (var r in ui) if (r.Valid) ns += r.LastValue;
            return ns / 1e6f;
        }

        private static long TextureBytes => texMemory.Valid && texMemory.LastValue > 0 ? texMemory.LastValue : (long)Texture.currentTextureMemory;

        private static float Mb(long bytes) => Mathf.Round(bytes / 1048576f * 10f) / 10f;

        private static float Ms(float v) => Mathf.Round(v * 100f) / 100f;

        private static float Pct(int part, int n) => n > 0 ? Mathf.Round(part * 1000f / n) / 10f : 0f;

        private static float Percentile(List<float> sorted, float p) =>
            sorted.Count == 0 ? 0f : sorted[Mathf.Clamp(Mathf.CeilToInt(p * sorted.Count) - 1, 0, sorted.Count - 1)];

        /// <summary>The window since the last <see cref="Reset"/>, for the automation <c>perf</c> route.</summary>
        public static Dictionary<string, object> Snapshot()
        {
            var sorted = new List<float>(frameMs);
            sorted.Sort();
            float total = 0f; int over60 = 0, over30 = 0;
            foreach (var v in sorted) { total += v; if (v > 1000f / 60f + 0.5f) over60++; if (v > 1000f / 30f + 0.5f) over30++; }
            int n = sorted.Count;
            var d = new Dictionary<string, object>
            {
                ["frames"] = n,
                ["seconds"] = Ms(Time.realtimeSinceStartup - windowStart),
                ["fps_avg"] = n > 0 ? Mathf.Round(n * 1000f / total * 10f) / 10f : 0f,
                ["frame_ms"] = new Dictionary<string, float>
                {
                    ["avg"] = n > 0 ? Ms(total / n) : 0f, ["p50"] = Ms(Percentile(sorted, 0.5f)), ["p95"] = Ms(Percentile(sorted, 0.95f)),
                    ["p99"] = Ms(Percentile(sorted, 0.99f)), ["max"] = n > 0 ? Ms(sorted[n - 1]) : 0f,
                },
                ["over_16ms_pct"] = Pct(over60, n),
                ["over_33ms_pct"] = Pct(over30, n),
                ["gc_source"] = gcAlloc.Valid ? "profiler" : "heap-growth",
                ["gc_alloc_kb_total"] = Mathf.Round(windowGc / 102.4f) / 10f,
                ["gc_alloc_bytes_per_frame"] = n > 0 ? windowGc / n : 0,
                ["gc_alloc_frames_pct"] = Pct(gcFrames, n),
                ["texture_mb"] = Mb(TextureBytes),
                ["allocated_mb"] = Mb(Profiler.GetTotalAllocatedMemoryLong()),
                ["reserved_mb"] = Mb(Profiler.GetTotalReservedMemoryLong()),
                ["mono_heap_mb"] = Mb(Profiler.GetMonoUsedSizeLong()),
                ["cold_start_s"] = Ms(ColdStartSeconds),
                ["target_fps"] = Application.targetFrameRate,
                ["render_interval"] = OnDemandRendering.renderFrameInterval,
                ["dev_build"] = Debug.isDebugBuild,
            };
            if (systemUsed.Valid) d["system_used_mb"] = Mb(systemUsed.LastValue);
            if (mainThread.Valid) d["main_thread_ms_last"] = Ms(mainThread.LastValue / 1e6f);
            if (uiMs.Count > 0)
            {
                var u = new List<float>(uiMs);
                u.Sort();
                float sum = 0f; foreach (var v in u) sum += v;
                d["ui_ms"] = new Dictionary<string, float> { ["avg"] = Ms(sum / u.Count), ["p95"] = Ms(Percentile(u, 0.95f)), ["max"] = Ms(u[u.Count - 1]) };
            }
            return d;
        }

        private static void Hud(GameApp app, float ms, long gc)
        {
            if (!HudOn)
            {
                if (hud != null) { hud.RemoveFromHierarchy(); hud = null; }
                return;
            }
            hudMs += ms; hudGcKb += gc / 1024f; hudFrames++;
            if (hud == null)
            {
                hud = Ui.Text("", "tiny", app.Stage);
                hud.name = "perf-hud";
                hud.pickingMode = PickingMode.Ignore;
                hud.style.position = Position.Absolute;
                hud.style.left = Space.S8; hud.style.bottom = Space.S8;
                hud.style.paddingLeft = Space.TargetHalfGap; hud.style.paddingRight = Space.TargetHalfGap;
                hud.style.backgroundColor = new Color(0f, 0f, 0f, 0.6f);
                hud.style.color = Color.white;
                hud.style.borderTopLeftRadius = hud.style.borderTopRightRadius = Radius.R8;
                hud.style.borderBottomLeftRadius = hud.style.borderBottomRightRadius = Radius.R8;
                hudNext = 0f;
            }
            hud.BringToFront();
            if (Time.unscaledTime < hudNext) return;
            hudNext = Time.unscaledTime + HudRefreshSeconds;
            float avg = hudMs / Mathf.Max(1, hudFrames);
            string uiPart = UiValid ? $"  UI {UiFrameMs():0.0} ms" : "";
            hud.text = $"{1000f / Mathf.Max(0.01f, avg):0} fps  {avg:0.0} ms{uiPart}\nGC {hudGcKb / Mathf.Max(1, hudFrames):0.0} KB/f  Tex {Mb(TextureBytes):0} MB";
            hudMs = 0f; hudGcKb = 0f; hudFrames = 0;
        }
    }
}
