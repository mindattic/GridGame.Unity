#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Meta;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App.Automation
{
    /// <summary>
    /// Development-build-only remote control so the game can be played and inspected by a script:
    /// a localhost HTTP endpoint (start the player with <c>-automation 7777</c>). Coordinates are
    /// design pixels (the live stage: 1170×2532 up to 1424 wide or 2730 tall, origin top-left).
    /// <para>
    /// Input (GG-T121): taps, drags and presses are dispatched straight into the UI Toolkit panel as
    /// pointer events — the same PointerDown/Move/Up (and the ClickEvent the panel synthesizes from them)
    /// that real input becomes, reaching the same handlers — rather than through an Input System virtual
    /// mouse. On desktop UI Toolkit's default event system drops all device input while the window is
    /// unfocused, so the old virtual-mouse path silently lost every tap whenever another player window on
    /// a shared desktop held focus, while still replying ok:true. The panel path doesn't care about OS
    /// focus, and every pointer reply now says what it hit: a tap that lands on nothing (off the stage, the
    /// bare stage background or a screen-transition fade) or whose events never reach the panel replies
    /// ok:false with an error.
    /// </para>
    /// </summary>
    public sealed partial class AutomationServer : MonoBehaviour
    {
        private HttpListener listener;
        private Thread thread;
        private readonly ConcurrentQueue<Action> mainThread = new ConcurrentQueue<Action>();
        private readonly List<string> logs = new List<string>();
        private string artifacts;
        private bool holdDrag;
        // Delivery receipts: a trickle-down listener on the panel root records each synthetic pointer
        // event as it is actually dispatched, so a reply can say whether (and where) it arrived.
        private EventBase sentEvent;
        private VisualElement sentTarget;
        private bool sentArrived;
        private Application.LogCallback logHandler;

        public static void TryStart(GameApp app)
        {
            var port = CommandLine.Value("-automation");
            if (string.IsNullOrEmpty(port)) return;
            var s = app.gameObject.AddComponent<AutomationServer>();
            s.Begin(int.Parse(port));
        }

        private void Begin(int port)
        {
            artifacts = CommandLine.Value("-artifacts") ?? Path.Combine(Application.persistentDataPath, "artifacts");
            Directory.CreateDirectory(artifacts);
            logHandler = (msg, stack, type) =>
            {
                if (type == LogType.Log && !msg.StartsWith("[")) return;
                lock (logs) { logs.Add($"{type}: {msg}" + (type == LogType.Exception || type == LogType.Error ? "\n" + stack : "")); if (logs.Count > 400) logs.RemoveAt(0); }
            };
            Application.logMessageReceivedThreaded += logHandler;
            Debug.developerConsoleEnabled = false;
            // The automation player must keep ticking (coroutines, tweens, layout) while another window
            // has focus; ProjectSettings already says so, this keeps it true whatever the build settings.
            Application.runInBackground = true;
            var root = GameApp.I.Document.rootVisualElement;
            root.RegisterCallback<PointerDownEvent>(Receipt, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerMoveEvent>(Receipt, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerUpEvent>(Receipt, TrickleDown.TrickleDown);
            listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            listener.Start();
            DisinheritListenerSockets();
            thread = new Thread(Serve) { IsBackground = true, Name = "Automation" };
            thread.Start();
            Debug.Log($"[Automation] listening on {port}, artifacts → {artifacts}");
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetHandleInformation(IntPtr handle, uint mask, uint flags);
#endif

        /// <summary>
        /// GG-T522: Windows sockets are inheritable by default, and MindAttic.Media.Capture starts its finalizer
        /// (capture.exe / ffmpeg) with handle inheritance when the player quits after a <c>-capture</c> run. The
        /// finalizer then holds this listener's 127.0.0.1:port socket in LISTENING under the dead player's PID
        /// for the whole export, and the next launch on that port fails to bind ("player did not start"). The
        /// HttpListener keeps its sockets private (Mono's EndPointManager → EndPointListener), so this finds them
        /// by reflection and clears HANDLE_FLAG_INHERIT on each. Best effort: if the internals ever change it
        /// logs and carries on, and tools/harness.py still falls back to a free port.
        /// </summary>
        private static void DisinheritListenerSockets()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            int cleared = 0;
            try
            {
                var seen = new HashSet<object>();
                void Walk(object o, int depth)
                {
                    if (o == null || depth > 4 || o is string || o.GetType().IsPrimitive || !seen.Add(o)) return;
                    if (o is System.Net.Sockets.Socket sock)
                    {
                        if (SetHandleInformation(sock.Handle, 1 /* HANDLE_FLAG_INHERIT */, 0)) cleared++;
                        return;
                    }
                    if (o is System.Collections.IDictionary dict)
                    {
                        foreach (var v in dict.Values) Walk(v, depth + 1);
                        return;
                    }
                    if (o is System.Collections.IEnumerable list)
                    {
                        foreach (var v in list) Walk(v, depth + 1);
                        return;
                    }
                    var t = o.GetType();
                    if (t.Namespace != "System.Net") return;
                    foreach (var f in t.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public))
                        if (!f.FieldType.IsPrimitive && f.FieldType != typeof(string)) Walk(f.GetValue(o), depth + 1);
                }
                // Older Mono names it EndPointManager, the corefx-based one HttpEndPointManager.
                foreach (var name in new[] { "System.Net.EndPointManager", "System.Net.HttpEndPointManager" })
                    if (typeof(HttpListener).Assembly.GetType(name) is Type mgr)
                    foreach (var f in mgr.GetFields(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public))
                        Walk(f.GetValue(null), 0);
            }
            catch (Exception e) { Debug.LogWarning($"[Automation] couldn't make the listener socket non-inheritable: {e.Message}"); }
            if (cleared == 0) Debug.LogWarning("[Automation] found no listener socket to make non-inheritable (GG-T522)");
            else Debug.Log($"[Automation] listener socket(s) non-inheritable: {cleared}");
#endif
        }

        private void OnDestroy()
        {
            if (logHandler != null) Application.logMessageReceivedThreaded -= logHandler;
            try { listener?.Stop(); } catch { }
        }

        private void Update()
        {
            while (mainThread.TryDequeue(out var a))
            {
                try { a(); } catch (Exception e) { Debug.LogException(e); }
            }
        }

        private void Serve()
        {
            while (listener.IsListening)
            {
                HttpListenerContext ctx;
                try { ctx = listener.GetContext(); }
                catch (Exception)
                {
                    // Expected when OnDestroy() calls listener.Stop() while GetContext() is blocked;
                    // anything else should not silently end the listener thread.
                    if (!listener.IsListening) return;
                    continue;
                }
                ThreadPool.QueueUserWorkItem(_ => Handle(ctx));
            }
        }

        private void Handle(HttpListenerContext ctx)
        {
            string body;
            int status = 200;
            try
            {
                var q = ctx.Request.QueryString;
                var path = ctx.Request.Url.AbsolutePath.Trim('/');
                var done = new ManualResetEventSlim();
                object result = null;
                Exception error = null;
                mainThread.Enqueue(() =>
                {
                    try
                    {
                        var r = Dispatch(path, q, v => { result = v; done.Set(); });
                        if (r != Pending) { result = r; done.Set(); }
                    }
                    catch (Exception e) { error = e; done.Set(); }
                });
                if (!done.Wait(TimeSpan.FromSeconds(120))) throw new TimeoutException("main thread did not answer");
                if (error != null) throw error;
                body = result as string ?? JsonConvert.SerializeObject(result, Formatting.Indented);
            }
            catch (Exception e)
            {
                status = 500;
                body = JsonConvert.SerializeObject(new { error = e.Message });
            }
            var bytes = Encoding.UTF8.GetBytes(body);
            try
            {
                ctx.Response.StatusCode = status;
                ctx.Response.ContentType = "application/json";
                ctx.Response.ContentLength64 = bytes.Length;
                ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
            }
            catch (Exception) { /* client disconnected mid-response; nothing more to do */ }
            finally { try { ctx.Response.OutputStream.Close(); } catch (Exception) { } }
        }

        private static readonly object Pending = new object();

        private static readonly object Unrouted = new object();

        private object Dispatch(string path, System.Collections.Specialized.NameValueCollection q, Action<object> reply)
        {
            var app = GameApp.I;
            var r = StateRoutes(path, q, reply, app);
            if (r == Unrouted) r = InputRoutes(path, q, reply, app);
            if (r == Unrouted) r = ScreensRoutes(path, q, reply, app);
            if (r == Unrouted) r = BattleRoutes(path, q, reply, app);
            if (r == Unrouted) r = DevRoutes(path, q, reply, app);
            return r != Unrouted ? r : new { error = "unknown endpoint " + path };
        }

        private static float F(string s, float def = 0f) => float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : def;

        private static List<Vector2> ParsePoints(string s)
        {
            var list = new List<Vector2>();
            if (string.IsNullOrEmpty(s)) return list;
            foreach (var part in s.Split(';'))
            {
                var xy = part.Split(',');
                if (xy.Length == 2) list.Add(new Vector2(F(xy[0]), F(xy[1])));
            }
            return list;
        }

        /// <summary>Strips any directory component/traversal from a request-supplied file name so it can't write or delete outside `artifacts`.</summary>
        private static string SafeName(string name, string fallback)
        {
            var n = Path.GetFileName(name);
            return string.IsNullOrEmpty(n) ? fallback : n;
        }

        private static List<Coord> ParseTiles(string s)
        {
            var list = new List<Coord>();
            foreach (var p in ParsePoints(s)) list.Add(new Coord((int)p.x, (int)p.y));
            return list;
        }
    }
}
#endif
