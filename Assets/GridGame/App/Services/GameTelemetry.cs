using System;
using System.IO;
using System.Linq;
using MindAttic.Telemetry;
using MindAttic.Telemetry.Unity;
using UnityEngine;

namespace GridGame.App
{
    /// <summary>
    /// GG-T548: where a MindAttic.Telemetry session may write, per platform. <see cref="TelemetryUnity.Begin"/> writes to
    /// <c>R:\Telemetry</c>, falling back to <c>Documents\MindAttic\Telemetry</c>: right on the dev desktop, but on Android
    /// (and iOS) neither exists, the fallback resolves to <c>/MindAttic/Telemetry</c> on a read-only root, and the library's
    /// Debug.LogError popped Unity's red Development Console over every battle start.
    ///
    /// Desktop players and the editor keep the library's own behaviour. Every other platform (phones, tablets) writes under
    /// <c>persistentDataPath/Telemetry</c> (on Android: <c>/sdcard/Android/data/&lt;package&gt;/files/Telemetry</c>,
    /// <c>adb pull</c>-able), keeps only the newest <see cref="MobileKeepSessions"/> session folders, and logs any failure as
    /// a plain line, never a red error. Nothing here throws: telemetry is dev-only and must never disturb play.
    /// </summary>
    public static class GameTelemetry
    {
        public const string GameId = "GridGameGaiden";
        /// <summary>Session folders kept on a device; older ones are deleted when a new session starts.</summary>
        public const int MobileKeepSessions = 20;

        /// <summary>True where the library's default output roots (a local drive / Documents) are real, writable places.</summary>
        public static bool IsDesktop =>
            Application.isEditor
            || Application.platform == RuntimePlatform.WindowsPlayer
            || Application.platform == RuntimePlatform.OSXPlayer
            || Application.platform == RuntimePlatform.LinuxPlayer;

        /// <summary>Starts a telemetry session for one unit of play. A no-op in release builds; never throws.</summary>
        public static void Begin(long? seed, string contentVersion, TelemetryData tags)
        {
            try
            {
                if (!TelemetryUnity.IsTelemetryBuild) return;
                if (IsDesktop) { TelemetryUnity.Begin(GameId, seed, contentVersion, tags); return; }

                var root = Path.Combine(Application.persistentDataPath, "Telemetry");
                var log = new QuietLog();
                Prune(Path.Combine(root, GameId), log);
                Telemetry.Begin(new TelemetryOptions
                {
                    OutputRoot = root,
                    FallbackRoot = root,
                    AppName = GameId,
                    AppVersion = Application.version,
                    Json = new NewtonsoftTelemetryJson(),
                    Log = log,
                }, GameId, seed, contentVersion, tags);
            }
            catch (Exception e) { Debug.Log("[Telemetry] off for this battle: " + e.Message); }
        }

        /// <summary>Deletes all but the newest session folders (names start with the UTC date, so they sort by age).</summary>
        private static void Prune(string appDir, ITelemetryLog log)
        {
            try
            {
                if (!Directory.Exists(appDir)) return;
                foreach (var old in Directory.GetDirectories(appDir).OrderByDescending(d => d, StringComparer.Ordinal).Skip(MobileKeepSessions - 1))
                    Directory.Delete(old, true);
            }
            catch (Exception e) { log.Log(TelemetryLogLevel.Warning, "Could not prune old telemetry sessions.", e); }
        }

        /// <summary>Telemetry's own warnings and errors as plain log lines: a dev-only recorder failing is not a game error,
        /// so it must not open the Development Console over play.</summary>
        private sealed class QuietLog : ITelemetryLog
        {
            public void Log(TelemetryLogLevel level, string message, Exception exception = null)
            {
                if (level == TelemetryLogLevel.Debug) return;
                if (level == TelemetryLogLevel.Info && !Debug.isDebugBuild) return;
                Debug.Log("[Telemetry] " + message + (exception == null ? "" : " (" + exception.GetType().Name + ": " + exception.Message + ")"));
            }
        }
    }
}
