#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App.Automation
{
    /// <summary>
    /// GG-T1783: the WebGL build's name-tap bridge. A browser player has no HTTP automation endpoint (no sockets) and
    /// only a canvas, so a script (tools/webcheck.mjs) couldn't find a UI Toolkit element by name and had to tap
    /// hard-coded design coordinates. In a development WebGL build this adds an <see cref="AutomationServer"/> without
    /// a listener on its own GameObject, and GridGameWebBridge.jslib puts <c>window.gridgameAutomation.click(name)</c>
    /// on the page: it SendMessages <see cref="WebClick"/>, which does what the HTTP <c>click</c> op does
    /// (<c>Stage.Q(name)</c> → visible check → <see cref="DesignPoint"/> → <see cref="Tap"/> through the panel) and
    /// hands the same JSON reply back to the page's promise.
    /// </summary>
    public sealed partial class AutomationServer
    {
        /// <summary>The GameObject SendMessage targets; the jslib is told it at registration.</summary>
        public const string WebBridgeObject = "GridGameWebBridge";

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void GGWebBridge_Register(string objectName);
        [DllImport("__Internal")] private static extern void GGWebBridge_Reply(string id, string json);
#else
        private static void GGWebBridge_Register(string objectName) { }
        private static void GGWebBridge_Reply(string id, string json) { }
#endif

        /// <summary>Starts the bridge in a WebGL player only (the desktop and mobile players use <see cref="TryStart"/>).</summary>
        public static void TryStartWebBridge()
        {
            if (Application.platform != RuntimePlatform.WebGLPlayer) return;
            var go = new GameObject(WebBridgeObject);
            DontDestroyOnLoad(go);
            go.AddComponent<AutomationServer>().BeginWebBridge();
        }

        private void BeginWebBridge()
        {
            var root = GameApp.I.Document.rootVisualElement;
            root.RegisterCallback<PointerDownEvent>(Receipt, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerMoveEvent>(Receipt, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerUpEvent>(Receipt, TrickleDown.TrickleDown);
            GGWebBridge_Register(gameObject.name);
            Debug.Log("[Automation] WebGL name-tap bridge ready (window.gridgameAutomation)");
        }

        /// <summary>SendMessage entry point: <c>"&lt;request id&gt;|&lt;element name&gt;"</c>. Replies through the jslib with the
        /// HTTP click op's JSON: ok/target, or an error naming why nothing was tapped.</summary>
        public void WebClick(string arg)
        {
            var bar = arg?.IndexOf('|') ?? -1;
            var id = bar < 0 ? "" : arg.Substring(0, bar);
            var name = bar < 0 ? arg : arg.Substring(bar + 1);
            void Reply(object r) => GGWebBridge_Reply(id, JsonConvert.SerializeObject(r));
            try
            {
                var e = GameApp.I.Stage.Q(name);
                // The same visibility gate as the HTTP click op (GG-T16): Q(name) also finds hidden or zero-size matches.
                if (e == null || e.resolvedStyle.display == DisplayStyle.None || e.worldBound.width <= 0 || !Opaque(e))
                {
                    Reply(new { ok = false, error = $"no visible element named '{name}'" });
                    return;
                }
                var c = DesignPoint(e.worldBound.center);
                StartCoroutine(Tap(c.x, c.y, Reply));
            }
            catch (Exception ex) { Reply(new { ok = false, error = ex.Message }); }
        }
    }
}
#endif
