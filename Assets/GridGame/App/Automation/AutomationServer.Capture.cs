#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections.Specialized;
using System.Threading.Tasks;
using MindAttic.Media.Capture;
using MindAttic.Media.Capture.Unity;

namespace GridGame.App.Automation
{
    /// <summary>
    /// GG-T1005: the harness's hooks into MindAttic.Media.Capture while a <c>-capture</c> run is recording.
    /// "screenshot" drops a chapter marker named after its PNG; tap/click/drag add one with <c>mark=1</c>;
    /// "capture" drives the engine itself:
    /// <c>op=marker&amp;name=</c> | <c>raise&amp;event=&amp;k=v...</c> | <c>screenshot&amp;tag=</c> | <c>save-replay&amp;tag=</c> |
    /// <c>clip&amp;before=&amp;after=&amp;tag=</c> (seconds) | <c>status</c>. Each answers {ok, error?, folder?}; with capture
    /// off it answers {ok:false, error:"capture off"} at once, so a harness call never waits on it.
    /// </summary>
    public sealed partial class AutomationServer
    {
        private static bool Capturing => CaptureUnity.Options?.Enabled == true;

        /// <summary>A recording chapter for one harness step, only while capture runs.</summary>
        private static void CaptureMarker(string name)
        {
            if (Capturing) CaptureUnity.Marker(name);
        }

        private static void StepMarker(NameValueCollection q, string step)
        {
            if (q["mark"] == "1") CaptureMarker(step);
        }

        private object CaptureOp(NameValueCollection q, Action<object> reply)
        {
            var op = q["op"] ?? "status";
            var engine = CaptureUnity.Engine;
            if (!Capturing)
                return op == "status" ? new { ok = false, error = "capture off", enabled = false } : (object)new { ok = false, error = "capture off" };
            var tag = q["tag"];
            switch (op)
            {
                case "status":
                    return new { ok = true, enabled = engine.Enabled, folder = engine.SessionFolder, recording = engine.IsRecording, session = engine.SessionId };
                case "marker":
                    if (string.IsNullOrEmpty(q["name"])) return new { ok = false, error = "missing name" };
                    CaptureUnity.Marker(q["name"], CaptureDataOf(q, "op", "name"));
                    return new { ok = true, folder = engine.SessionFolder };
                case "raise":
                    if (string.IsNullOrEmpty(q["event"])) return new { ok = false, error = "missing event" };
                    CaptureUnity.Raise(q["event"], CaptureDataOf(q, "op", "event"));
                    return new { ok = true, folder = engine.SessionFolder };
                case "screenshot":
                    ReplyWhenDone(engine.ScreenshotAsync(tag ?? "harness"), reply);
                    return Pending;
                case "save-replay":
                    ReplyWhenDone(engine.SaveReplayAsync(CaptureUnity.Options.ReplayLength, TimeSpan.Zero, tag ?? "replay"), reply);
                    return Pending;
                case "clip":
                {
                    var before = TimeSpan.FromSeconds(Math.Max(0f, F(q["before"], 0f)));
                    var after = TimeSpan.FromSeconds(Math.Max(0f, F(q["after"], 0f)));
                    if (before + after <= TimeSpan.Zero) return new { ok = false, error = "clip needs before or after seconds" };
                    ReplyWhenDone(before > TimeSpan.Zero ? engine.SaveReplayAsync(before, after, tag ?? "clip") : engine.ClipAsync(after, tag ?? "clip"), reply);
                    return Pending;
                }
            }
            return new { ok = false, error = "unknown op " + op };
        }

        /// <summary>Every query key except <paramref name="skip"/> as marker/event data (mark=1 style flags included).</summary>
        private static CaptureData CaptureDataOf(NameValueCollection q, params string[] skip)
        {
            var data = new CaptureData();
            foreach (string key in q.AllKeys)
                if (key != null && Array.IndexOf(skip, key) < 0) data[key] = q[key];
            return data.Count == 0 ? null : data;
        }

        private static void ReplyWhenDone(Task<CaptureResult> task, Action<object> reply)
        {
            task.ContinueWith(t =>
            {
                if (t.IsFaulted || t.IsCanceled)
                {
                    reply(new { ok = false, error = t.Exception?.GetBaseException().Message ?? "capture cancelled", folder = CaptureUnity.Engine.SessionFolder });
                    return;
                }
                var r = t.Result;
                reply(new { ok = r.Succeeded, error = r.Error, folder = CaptureUnity.Engine.SessionFolder, file = r.PrimaryFile, files = r.Files });
            }, TaskScheduler.Default);
        }
    }
}
#endif
