using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GridGame.EditorTools
{
    /// <summary>
    /// Command-line entry points (Unity -batchmode -executeMethod GridGame.EditorTools.GaidenBuild.X).
    /// The one scene is an empty shell — GameApp builds everything at runtime — so this file is the
    /// only "scene authoring" the project has.
    /// </summary>
    public static class GaidenBuild
    {
        public const string ScenePath = "Assets/Gaiden/Scenes/Main.unity";

        [MenuItem("Gaiden/Ensure Main Scene")]
        public static void EnsureScene()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
                Debug.Log("[Build] created " + ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.bundleVersion = Version();
            AssetDatabase.SaveAssets();
        }

        private static bool CommandLine(string flag) => System.Array.IndexOf(System.Environment.GetCommandLineArgs(), flag) >= 0;

        private static string Version()
        {
            try { return File.ReadAllText("version.txt").Trim(); } catch { return "1"; }
        }

        [MenuItem("Gaiden/Build Windows (Development)")]
        public static void BuildWindowsDev() => Build(BuildTarget.StandaloneWindows64, "Build/Windows/GridGameGaiden.exe", true);

        public static void BuildWindowsRelease() => Build(BuildTarget.StandaloneWindows64, "Build/WindowsRelease/GridGameGaiden.exe", false);

        public const string AndroidPackage = "com.mindattic.gridgamegaiden";

        /// <summary>The development APK (ARM64 + x86_64, so the emulator runs it; tools/androidcheck.py). Debug-signed.
        /// <c>tools/build.ps1 -Platform Android -Dev</c>.</summary>
        public static void BuildAndroid()
        {
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, AndroidPackage);
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.X86_64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            EditorUserBuildSettings.buildAppBundle = false;
            // -vulkan: Vulkan-only test build (diagnostics); default keeps Unity's automatic API list.
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, !CommandLine("-vulkan"));
            if (CommandLine("-vulkan")) PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.Vulkan });
            Build(BuildTarget.Android, CommandLine("-vulkan") ? "Build/Android/GridGameGaiden-vulkan.apk" : "Build/Android/GridGameGaiden.apk", true);
        }

        /// <summary>
        /// GG-T478: the Google Play build — an Android App Bundle (.aab), IL2CPP, ARM64 only, no development flags,
        /// target SDK = the highest installed (Play's floor rises every year), version code bumped (see
        /// <see cref="AndroidVersionCode"/>). Signed with the upload keystore named by the environment
        /// (GG_ANDROID_KEYSTORE, GG_ANDROID_KEYSTORE_PASS, GG_ANDROID_KEY_ALIAS, GG_ANDROID_KEY_PASS — tools/build.ps1
        /// loads them from the git-ignored android-signing.env); with none, Unity's debug key signs it and the log says
        /// it can't go to Play. The keystore path is put back afterwards, so ProjectSettings never records a machine's
        /// key, and the passwords are never written anywhere (Unity keeps them in memory only). docs/RELEASE_BUILD.md.
        /// <c>tools/build.ps1 -Platform Android</c>.
        /// </summary>
        public static void BuildAndroidRelease()
        {
            var nt = UnityEditor.Build.NamedBuildTarget.Android;
            PlayerSettings.SetApplicationIdentifier(nt, AndroidPackage);
            PlayerSettings.SetScriptingBackend(nt, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetIl2CppCodeGeneration(nt, UnityEditor.Build.Il2CppCodeGeneration.OptimizeSpeed);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, true);
            int oldCode = PlayerSettings.Android.bundleVersionCode;
            PlayerSettings.Android.bundleVersionCode = AndroidVersionCode();
            EditorUserBuildSettings.buildAppBundle = true;
            EmbedAndroidSymbols();

            bool hadCustom = PlayerSettings.Android.useCustomKeystore;
            string oldStore = PlayerSettings.Android.keystoreName, oldAlias = PlayerSettings.Android.keyaliasName;
            var store = Environment.GetEnvironmentVariable("GG_ANDROID_KEYSTORE");
            bool signed = !string.IsNullOrEmpty(store) && File.Exists(store);
            try
            {
                if (signed)
                {
                    PlayerSettings.Android.useCustomKeystore = true;
                    PlayerSettings.Android.keystoreName = Path.GetFullPath(store);
                    PlayerSettings.Android.keystorePass = Environment.GetEnvironmentVariable("GG_ANDROID_KEYSTORE_PASS") ?? "";
                    PlayerSettings.Android.keyaliasName = Environment.GetEnvironmentVariable("GG_ANDROID_KEY_ALIAS") ?? "upload";
                    PlayerSettings.Android.keyaliasPass = Environment.GetEnvironmentVariable("GG_ANDROID_KEY_PASS")
                        ?? PlayerSettings.Android.keystorePass;
                    Debug.Log($"[Build] Android: signing with the upload key '{PlayerSettings.Android.keyaliasName}' from {Path.GetFileName(store)}");
                }
                else
                {
                    PlayerSettings.Android.useCustomKeystore = false;
                    Debug.LogWarning(string.IsNullOrEmpty(store)
                        ? "[Build] Android: no GG_ANDROID_KEYSTORE — the bundle is DEBUG-signed and Google Play will refuse it (docs/RELEASE_BUILD.md)"
                        : $"[Build] Android: GG_ANDROID_KEYSTORE '{store}' doesn't exist — the bundle is DEBUG-signed (docs/RELEASE_BUILD.md)");
                }
                Debug.Log($"[Build] Android: version {PlayerSettings.bundleVersion} ({PlayerSettings.Android.bundleVersionCode})");
                Build(BuildTarget.Android, "Build/AndroidRelease/GridGameGaiden.aab", false, exitWhenDone: false);
            }
            finally
            {
                PlayerSettings.Android.keystorePass = "";
                PlayerSettings.Android.keyaliasPass = "";
                PlayerSettings.Android.useCustomKeystore = hadCustom;
                if (signed) { PlayerSettings.Android.keystoreName = oldStore; PlayerSettings.Android.keyaliasName = oldAlias; }
                PlayerSettings.Android.bundleVersionCode = oldCode;
                EditorUserBuildSettings.buildAppBundle = false;
                AssetDatabase.SaveAssets();
            }
            if (Application.isBatchMode) EditorApplication.Exit(lastResult == BuildResult.Succeeded ? 0 : 1);
        }

        /// <summary>
        /// GG-T484: puts IL2CPP's native symbol table inside the .aab so Google Play's Android vitals shows readable
        /// crash stacks with nothing uploaded by hand (UnityEditor.Android.UserBuildSettings.DebugSymbols: level
        /// SymbolTable, format IncludeInBundle). Reached by reflection because the Android editor extension is only
        /// referenced while Android is the active target; a missing API only warns.
        /// </summary>
        private static void EmbedAndroidSymbols()
        {
            try
            {
                var t = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType("UnityEditor.Android.UserBuildSettings+DebugSymbols", false))
                    .FirstOrDefault(x => x != null);
                if (t == null) { Debug.LogWarning("[Build] Android: no DebugSymbols settings; bundle without native symbols"); return; }
                const System.Reflection.BindingFlags S = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static;
                var level = t.GetProperty("level", S);
                var format = t.GetProperty("format", S);
                level?.SetValue(null, Enum.Parse(level.PropertyType, "SymbolTable"));
                format?.SetValue(null, Enum.Parse(format.PropertyType, "IncludeInBundle"));
                Debug.Log($"[Build] Android: native symbols {level?.GetValue(null)} / {format?.GetValue(null)}");
            }
            catch (Exception e) { Debug.LogWarning("[Build] Android: couldn't set debug symbols (" + e.Message + ")"); }
        }

        /// <summary>
        /// GG-T478: Google Play refuses a bundle whose version code isn't higher than every one uploaded before. It is
        /// the number of commits on the checked-out history (git rev-list --count HEAD), which only grows on main, so
        /// every build of a newer commit outranks the last with nothing to remember or commit. GG_ANDROID_VERSION_CODE
        /// overrides it (a hotfix rebuilt from an older commit). The project's own code (ProjectSettings) is left alone.
        /// </summary>
        public static int AndroidVersionCode()
        {
            var env = Environment.GetEnvironmentVariable("GG_ANDROID_VERSION_CODE");
            if (int.TryParse(env, out var forced) && forced > 0) return forced;
            int count = 0;
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("git", "rev-list --count HEAD")
                {
                    UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true,
                    WorkingDirectory = Path.GetFullPath("."),
                };
                using var p = System.Diagnostics.Process.Start(psi);
                var text = p.StandardOutput.ReadToEnd();
                p.WaitForExit(30000);
                int.TryParse(text.Trim(), out count);
            }
            catch (Exception e) { Debug.LogWarning("[Build] git rev-list failed (" + e.Message + ")"); }
            if (count <= 0) throw new UnityEditor.Build.BuildFailedException("[Build] no version code: git rev-list failed and GG_ANDROID_VERSION_CODE isn't set");
            return count;
        }

        public static void BuildWebGL()
        {
            // Uncompressed so any static file server (and Playwright) can serve it without special headers.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.template = "APPLICATION:Minimal";
            PlayerSettings.defaultWebScreenWidth = 585;
            PlayerSettings.defaultWebScreenHeight = 1266;
            Build(BuildTarget.WebGL, "Build/WebGL", CommandLine("-dev"));
        }

        /// <summary>GG-T355: true once this editor session pulled Prose text for the build in progress.</summary>
        internal static bool ProseSynced;

        /// <summary>
        /// GG-T355 (docs/PROSE.md, "Build-time pull"): pull the latest published story text from Prose into
        /// Content/Text (tools/prose-sync.ps1: kit pull + kit apply) so a build ships what Prose has published.
        /// Never fails the build: the script warns and keeps the committed snapshot when the Hub is offline. Skipped
        /// with -skipProse on the command line or GG_SKIP_PROSE=1, and when tools/build.ps1 already ran it
        /// (GG_PROSE_SYNCED=1).
        /// </summary>
        public static void SyncProse()
        {
            if (ProseSynced) return;
            ProseSynced = true;
            if (CommandLine("-skipProse") || Environment.GetEnvironmentVariable("GG_SKIP_PROSE") == "1")
            {
                Debug.Log("[Build] Prose sync skipped: committed Content/Text snapshot");
                return;
            }
            if (Environment.GetEnvironmentVariable("GG_PROSE_SYNCED") == "1") return;
            var script = Path.GetFullPath("tools/prose-sync.ps1");
            if (!File.Exists(script)) { Debug.LogWarning("[Build] no tools/prose-sync.ps1; committed Content/Text snapshot"); return; }
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("powershell",
                    $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\"")
                {
                    UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
                    CreateNoWindow = true, WorkingDirectory = Path.GetFullPath("."),
                };
                using var p = System.Diagnostics.Process.Start(psi);
                var stdout = p.StandardOutput.ReadToEndAsync();
                var stderr = p.StandardError.ReadToEndAsync();
                if (!p.WaitForExit(5 * 60 * 1000))
                {
                    try { p.Kill(); } catch { /* already gone */ }
                    Debug.LogWarning("[Build] Prose sync timed out; committed Content/Text snapshot");
                    return;
                }
                Debug.Log("[Build] Prose sync:\n" + stdout.Result + stderr.Result);
                AssetDatabase.Refresh();
            }
            catch (Exception e) { Debug.LogWarning("[Build] Prose sync failed (" + e.Message + "); committed Content/Text snapshot"); }
        }

        private static BuildResult lastResult = BuildResult.Unknown;

        private static void Build(BuildTarget target, string output, bool development, bool exitWhenDone = true)
        {
            SyncProse();
            EnsureScene();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)) ?? ".");
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = target,
                options = development ? BuildOptions.Development | BuildOptions.AllowDebugging : BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            Debug.Log($"[Build] {s.result} {target} → {output} size={s.totalSize} errors={s.totalErrors} warnings={s.totalWarnings} time={s.totalTime}");
            lastResult = s.result;
            if (exitWhenDone && Application.isBatchMode) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }

        /// <summary>Force-reimport every Resources asset so GaidenImport's settings apply (run after adding art).</summary>
        public static void ReimportArt()
        {
            foreach (var guid in AssetDatabase.FindAssets("", new[] { "Assets/Gaiden/Resources" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.IsValidFolder(path)) continue;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            Debug.Log("[Build] reimported Resources art/audio");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>Compile-only check for CI/agents: succeeds when scripts compiled (we got here at all).</summary>
        public static void CompileCheck()
        {
            EnsureScene();
            Debug.Log("[Build] compile OK");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
    }

    /// <summary>
    /// GG-T355: a build started any other way (File > Build) still pulls Prose text first. Builds through
    /// <see cref="GaidenBuild"/> have already synced, so this does nothing for them.
    /// </summary>
    public sealed class GaidenProsePreBuild : UnityEditor.Build.IPreprocessBuildWithReport
    {
        public int callbackOrder => -100;
        public void OnPreprocessBuild(BuildReport report) => GaidenBuild.SyncProse();
    }

    /// <summary>Import settings by folder so no asset ever needs hand-tuning in the Inspector.</summary>
    public sealed class GaidenImport : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Gaiden/Resources/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            ti.sRGBTexture = true;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            // GG-T387: portraits are 1024 canvases (§12.1.2) and dialogue draws a person ~1.2x their pixels, so the cap must
            // never shrink one: 2048 imports every 1024 file pixel for pixel and leaves room for higher-resolution art.
            // No platform overrides (the .meta files carry none), so Android, iOS, WebGL and Standalone all use this.
            if (assetPath.Contains("/Portraits/")) ti.maxTextureSize = 2048;
            // GG-T353: backdrops keep the art's resolution (1536x3072 originals): 4096 so Unity never halves them.
            // Still CompressedHQ (BC7 / ASTC 4x4, ~1 byte per pixel, no mips): ~4.5 MB of texture memory each.
            else if (assetPath.Contains("/Backgrounds/")) ti.maxTextureSize = 4096;
            else if (assetPath.Contains("/Icons/")) { ti.maxTextureSize = 128; ti.textureCompression = TextureImporterCompression.Uncompressed; }
        }

        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/Gaiden/Resources/Audio/")) return;
            var ai = (AudioImporter)assetImporter;
            var s = ai.defaultSampleSettings;
            if (assetPath.Contains("/Music/"))
            {
                s.loadType = AudioClipLoadType.Streaming;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.6f;
            }
            else
            {
                s.loadType = AudioClipLoadType.DecompressOnLoad;
                s.compressionFormat = AudioCompressionFormat.ADPCM;
            }
            ai.defaultSampleSettings = s;
        }
    }
}
