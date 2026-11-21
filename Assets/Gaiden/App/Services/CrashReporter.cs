using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using MindAttic.Telemetry;
using MindAttic.Telemetry.Unity;
using Newtonsoft.Json;
using UnityEngine;

namespace GridGame.App
{
    /// <summary>
    /// GG-T484: every unhandled exception, in every build, is written to the device's own error log
    /// (<c>persistentDataPath/Diagnostics/errors.jsonl</c>, one JSON line each, rolled at 256 KB) and, while a
    /// MindAttic.Telemetry session is open (editor and development builds), recorded there too as an "exception" event.
    ///
    /// No personal data: a record is the time, the app version, the platform, OS and device model, the exception's
    /// type, message and stack — with the data folder, the user's home folder and the player-chosen profile name
    /// (Profiles/&lt;Name&gt;) scrubbed out of the text. Never the save, the device id, an account or an IP. Nothing
    /// leaves the device: release builds have no upload yet (docs/RELEASE_BUILD.md, "Crash and error reporting":
    /// native crashes and ANRs reach Google Play's Android vitals on their own; a remote sink for managed exceptions
    /// is the owner's pick). An exception repeated every frame is written a few times, then only counted.
    /// </summary>
    public static class CrashReporter
    {
        public const long MaxBytes = 256 * 1024;
        /// <summary>Records written per launch; past it exceptions are only counted.</summary>
        public const int MaxPerSession = 50;
        /// <summary>The same exception (type + first stack frame) is written this many times per launch at most.</summary>
        public const int MaxRepeats = 3;

        private static readonly object Gate = new object();
        private static readonly Dictionary<string, int> Seen = new Dictionary<string, int>();
        private static string folder, file, dataPath, platform, os, device, version;
        private static int written, dropped;
        private static bool installed;

        public static string LogFile => file;
        /// <summary>Exceptions seen this launch (written or not).</summary>
        public static int SessionCount { get; private set; }

        /// <summary>Hooks Unity's log and the runtime's last-chance handlers. Call once, first thing at boot.</summary>
        public static void Install()
        {
            if (installed) return;
            installed = true;
            try
            {
                dataPath = Application.persistentDataPath;
                folder = Path.Combine(dataPath, "Diagnostics");
                file = Path.Combine(folder, "errors.jsonl");
                platform = Application.platform.ToString();
                os = SystemInfo.operatingSystem;
                device = SystemInfo.deviceModel;
                version = Application.version;
                Directory.CreateDirectory(folder);
                if (File.Exists(file)) Debug.Log($"[Crash] error log holds {File.ReadAllLines(file).Length} record(s) from earlier runs ({file})");
            }
            catch (Exception e) { Debug.LogWarning("[Crash] no error log: " + e.Message); file = null; }
            Application.logMessageReceived += OnLog; // main thread: the telemetry session may be written from here
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                Record((e.ExceptionObject as Exception)?.GetType().Name ?? "UnhandledException",
                    e.ExceptionObject?.ToString() ?? "", "", e.IsTerminating ? "unhandled (terminating)" : "unhandled", false);
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, e) =>
                Record(e.Exception?.GetBaseException().GetType().Name ?? "TaskException", e.Exception?.GetBaseException().Message ?? "",
                    e.Exception?.ToString() ?? "", "unobserved task", false);
        }

        /// <summary>Unity logs every exception a frame, coroutine or callback threw (they don't crash the game) as
        /// LogType.Exception; those are what this catches in practice.</summary>
        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Exception) return;
            int colon = condition.IndexOf(':');
            string kind = colon > 0 ? condition.Substring(0, colon).Trim() : "Exception";
            string message = colon > 0 ? condition.Substring(colon + 1).Trim() : condition;
            Record(kind, message, stackTrace, "logged", true);
        }

        private static void Record(string kind, string message, string stack, string how, bool mainThread)
        {
            lock (Gate)
            {
                SessionCount++;
                string firstFrame = FirstLine(stack);
                string key = kind + "|" + firstFrame;
                Seen.TryGetValue(key, out var n);
                Seen[key] = n + 1;
                if (n >= MaxRepeats || written >= MaxPerSession) { dropped++; return; }
                written++;

                var record = new Dictionary<string, object>
                {
                    ["utc"] = DateTime.UtcNow.ToString("o"),
                    ["version"] = version,
                    ["platform"] = platform,
                    ["os"] = os,
                    ["device"] = device,
                    ["how"] = how,
                    ["type"] = kind,
                    ["message"] = Scrub(message, dataPath),
                    ["stack"] = Scrub(stack, dataPath),
                    ["repeat"] = n + 1,
                    ["droppedBefore"] = dropped,
                };
                Append(record);
                try
                {
                    if (mainThread && TelemetryUnity.Current.Enabled)
                    {
                        var data = new TelemetryData();
                        foreach (var kv in record) data[kv.Key] = kv.Value;
                        TelemetryUnity.Current.RecordEvent("exception", data);
                    }
                }
                catch (Exception) { /* telemetry is best-effort; never throw from a log handler */ }
            }
        }

        private static void Append(Dictionary<string, object> record)
        {
            if (file == null) return;
            try
            {
                if (File.Exists(file) && new FileInfo(file).Length > MaxBytes)
                {
                    var old = file + ".1";
                    if (File.Exists(old)) File.Delete(old);
                    File.Move(file, old);
                }
                File.AppendAllText(file, JsonConvert.SerializeObject(record) + "\n");
            }
            catch (Exception) { /* a full disk must not turn one exception into a loop of them */ }
        }

        private static string FirstLine(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            int nl = s.IndexOf('\n');
            return (nl < 0 ? s : s.Substring(0, nl)).Trim();
        }

        private static readonly Regex ProfileSegment = new Regex(@"(Profiles[\\/])[^\\/\r\n""']+", RegexOptions.Compiled);
        private static readonly Regex UserHome = new Regex(@"([A-Za-z]:[\\/]Users[\\/]|/Users/|/home/)[^\\/\r\n""']+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>Takes the personal bits out of exception text: the app's data folder (it holds the OS user name on
        /// desktop and the package path on phones), any user home folder, and the profile name in Profiles/&lt;Name&gt;.</summary>
        public static string Scrub(string text, string dataFolder)
        {
            if (string.IsNullOrEmpty(text)) return text ?? "";
            if (!string.IsNullOrEmpty(dataFolder))
            {
                text = text.Replace(dataFolder, "<data>");
                text = text.Replace(dataFolder.Replace('/', '\\'), "<data>");
            }
            text = UserHome.Replace(text, "$1<user>");
            text = ProfileSegment.Replace(text, "$1<profile>");
            return text;
        }
    }
}
