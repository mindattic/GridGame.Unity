using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GridGame.Core.Content;
using GridGame.Meta;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace GridGame.App.Dev
{
    /// <summary>
    /// GG-T1801: puts a built scenario on screen. The save goes on slot -1 (Persist never writes it), the Hub is shown,
    /// and once it has actually landed (the Router.Changed wait <c>DemoMode.BeginRun</c> uses) the target opens: a battle
    /// on a synthetic stage that starts at the target wave (the automation <c>randombattle</c> technique), the real
    /// stage through <see cref="StageLauncher"/> with RealLaunch, a <see cref="Automation.StartScreens"/> screen, or the
    /// Hub itself for Camp on the road (until an overworld map screen exists). GG-T2129: then <see cref="ScenarioDef.Open"/>
    /// opens its state on that screen through <see cref="Openers"/>.
    /// </summary>
    public static class ScenarioLander
    {
        public const string SynthPrefix = "scenario-";
        /// <summary>How long an <see cref="ScenarioDef.Open"/> waits for its screen to take it (a battle's intro, a stage's beats).</summary>
        public const float OpenTimeoutSeconds = 60f;

        /// <summary>GG-T2129: each <see cref="ScenarioOpen.What"/> and how it opens on the screen on top, through that screen's
        /// own UI path. An opener returns false while the screen is not there or not ready yet, and is retried until it opens.
        /// Add a line here to open something new.</summary>
        public static readonly Dictionary<string, Func<GameScreen, IReadOnlyList<string>, bool>> Openers =
            new Dictionary<string, Func<GameScreen, IReadOnlyList<string>, bool>>(StringComparer.OrdinalIgnoreCase)
            {
                ["abilities"] = (screen, path) => screen is BattleScreen b && b.OpenAbilitiesAt(path),
                ["info"] = (screen, _) => screen is BattleScreen b && b.OpenExamine(),
            };

        public static ScenarioResult Land(ScenarioDef def)
        {
            var app = GameApp.I;
            var open = ScenarioOpen.Parse(def?.Open);
            if (open != null && !Openers.ContainsKey(open.What))
                throw new ArgumentException($"open: unknown '{open.What}' (one of {string.Join(", ", Openers.Keys)})");
            var result = ScenarioBuilder.Build(app.Content, def);
            ApplySettings(app, def.Settings);
            app.Save = result.Save;
            app.Slot = -1;
            var t = result.Target;

            void OnShown(GameScreen shown)
            {
                if (shown is not HubScreen) return;
                app.Router.Changed -= OnShown;
                if (t.Kind == ScenarioKind.Screen && Automation.StartScreens.TryGet(t.Screen, out var screen)) app.Router.Push(screen);
                else if (t.Kind == ScenarioKind.Battle && t.Drill) app.Router.Push(new BattleScreen(t.StageId));
                else if (t.Kind == ScenarioKind.Battle && def.RealLaunch) StageLauncher.Launch(t.StageId, false);
                else if (t.Kind == ScenarioKind.Battle) app.Router.Push(new BattleScreen(Synthesize(app.Content, app.Save, t, def.FullStage)));
                if (open != null) app.StartCoroutine(OpenWhenReady(app, open));
            }
            app.Router.Changed += OnShown;
            app.Router.ResetTo(new HubScreen());
            return result;
        }

        private static IEnumerator OpenWhenReady(GameApp app, ScenarioOpen open)
        {
            var opener = Openers[open.What];
            float end = Time.unscaledTime + OpenTimeoutSeconds;
            while (Time.unscaledTime < end)
            {
                if (!app.Router.Busy && app.Router.Current is GameScreen screen && opener(screen, open.Path)) yield break;
                yield return null;
            }
            Debug.LogWarning($"[Scenario] open '{open.What}' timed out on {app.Router.Current?.GetType().Name}");
        }

        /// <summary>A throwaway copy of the target stage in the in-memory ContentDb that opens on the target wave (and,
        /// with <paramref name="fullStage"/>, plays on through the rest), at the source stage's recommended level.</summary>
        public static string Synthesize(ContentDb c, SaveData s, ScenarioTarget t, bool fullStage)
        {
            foreach (var oldId in c.Stages.Keys.Where(k => k.StartsWith(SynthPrefix)).ToList()) c.Stages.Remove(oldId);
            var src = c.Stage(t.StageId);
            int wave = Mathf.Clamp(t.Wave, 0, Math.Max(0, src.Waves.Count - 1));
            var id = SynthPrefix + Guid.NewGuid().ToString("N").Substring(0, 8);
            c.Stages[id] = new StageDef
            {
                Id = id, Name = src.Name, Theme = src.Theme, Background = src.Background, Music = src.Music, IsTest = false,
                LevelOverride = Progression.StageRecommendedLevel(c, s, src), Blocks = src.Blocks, Rules = src.Rules,
                Waves = fullStage ? src.Waves.Skip(wave).ToList() : new List<WaveDef> { src.Waves[wave] },
            };
            return id;
        }

        private static void ApplySettings(GameApp app, Dictionary<string, JToken> settings)
        {
            if (settings == null || settings.Count == 0) return;
            var st = app.Settings;
            foreach (var kv in settings)
            {
                var v = kv.Value;
                bool on = v != null && (v.Type == JTokenType.Boolean ? (bool)v : v.ToString() == "1" || string.Equals(v.ToString(), "true", StringComparison.OrdinalIgnoreCase));
                switch (kv.Key.ToLowerInvariant())
                {
                    case "colorblind": st.ColorblindMode = on; break;
                    case "reducemotion": st.ReduceMotion = on; break;
                    case "dragtimer": st.DragTimer = on; break;
                    case "mutemusic": st.MuteMusic = on; break;
                    case "mutesfx": st.MuteSfx = on; break;
                    case "cutins": st.PincerCutIns = on; break;
                    case "confirm": st.ConfirmActions = on; break;
                    case "menutheme": st.MenuTheme = v?.ToString(); break;
                    case "hyphenate": st.Hyphenate = on; break;
                    case "textsize": st.TextScale = TextSize.Clamp(v != null && int.TryParse(v.ToString(), out var n) ? n : 100); break;
                    default: Debug.LogWarning($"[Scenario] unknown setting '{kv.Key}'"); break;
                }
            }
            app.ApplySettings();
        }

        /// <summary>A preset name (tools/scenarios/&lt;name&gt;.json under the project root), a path to a spec file, or a bare
        /// target ("first-boss", "screen:caravan") when neither file exists.</summary>
        public static ScenarioDef Load(string presetOrPath)
        {
            if (string.IsNullOrWhiteSpace(presetOrPath)) return new ScenarioDef();
            var arg = presetOrPath.Trim();
            if (arg.StartsWith("{")) return ScenarioDef.Parse(arg);
            if (File.Exists(arg)) return ScenarioDef.Parse(File.ReadAllText(arg));
            var root = Directory.GetParent(Application.dataPath)?.FullName;
            var preset = root == null ? null : Path.Combine(root, "tools", "scenarios", arg.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? arg : arg + ".json");
            if (preset != null && File.Exists(preset)) return ScenarioDef.Parse(File.ReadAllText(preset));
            return ScenarioDef.ForTarget(arg);
        }
    }

    /// <summary>`-scenario &lt;preset|path.json|target&gt;` (GG-T1801): boot straight into a scenario on the throwaway
    /// "Scenario" profile, never the owner's real profiles.</summary>
    public static class ScenarioMode
    {
        public const string Profile = "Scenario";
        public static string Arg => CommandLine.Value("-scenario");
        public static bool Active => !string.IsNullOrEmpty(Arg);

        public static void Start()
        {
            try
            {
                var r = ScenarioLander.Land(ScenarioLander.Load(Arg));
                Debug.Log("[Scenario] " + Newtonsoft.Json.JsonConvert.SerializeObject(r.Summary(GameApp.I.Content)));
            }
            catch (Exception e)
            {
                Debug.LogError($"[Scenario] -scenario {Arg}: {e.Message}");
                GameApp.I.Router.ResetTo(new TitleScreen());
            }
        }
    }
}
