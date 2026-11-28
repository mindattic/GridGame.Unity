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
    /// <summary>Pointer and text input routes: taps, clicks, drags, presses, typing, picking, scrolling and waiting for idle.</summary>
    public sealed partial class AutomationServer
    {
        private object InputRoutes(string path, System.Collections.Specialized.NameValueCollection q, Action<object> reply, GameApp app)
        {
            switch (path)
            {
                case "tap":
                    StepMarker(q, $"tap {q["x"]},{q["y"]}");
                    StartCoroutine(Tap(F(q["x"]), F(q["y"]), reply));
                    return Pending;
                case "drag":
                    StepMarker(q, "drag");
                    holdDrag = q["hold"] == "1";
                    StartCoroutine(Drag(ParsePoints(q["path"]), F(q["step"], 0.03f), reply));
                    return Pending;
                case "click":
                {
                    var e = app.Stage.Q(q["name"]);
                    // GG-T1922: the five slot cells are gone; "slotN" takes bar slot N through the Abilities sheet (BattleScreen.TapSlot).
                    if (e == null && app.Router.Current is BattleScreen sb && q["name"] is string sn && sn.Length == 5 && sn.StartsWith("slot") && char.IsDigit(sn[4]))
                    {
                        StepMarker(q, "click " + sn);
                        sb.TapSlot(sn[4] - '0');
                        return new { ok = true };
                    }
                    // Q(name) isn't filtered by visibility like clicktext's Query<>() is — without this,
                    // a hidden/zero-size/off-stage match (e.g. mid-transition, or a same-named element
                    // elsewhere not currently shown) taps nothing and still replies ok:true (GG-T16).
                    if (e == null || e.resolvedStyle.display == DisplayStyle.None || e.worldBound.width <= 0 || !Opaque(e))
                        return new { error = $"no visible element named '{q["name"]}'" };
                    var c = DesignPoint(e.worldBound.center);
                    StepMarker(q, "click " + q["name"]);
                    StartCoroutine(Tap(c.x, c.y, reply));
                    return Pending;
                }
                case "scrollto":
                {
                    // Scrolls the nearest ScrollView ancestor until the named element is in view, so a
                    // following click lands on it rather than on whatever sits at its off-viewport
                    // coordinates (GG-T110: most of the party-select grid starts below the fold).
                    var e = app.Stage.Q(q["name"]);
                    if (e == null) return new { error = $"no element named '{q["name"]}'" };
                    var sv = e.GetFirstAncestorOfType<ScrollView>();
                    if (sv == null) return new { ok = true, scrolled = false };
                    sv.ScrollTo(e);
                    return new { ok = true, scrolled = true };
                }
                case "clicktext":
                {
                    var target = q["text"];
                    // TextFit.SourceOf: the words as given, before GG-T305's line breaks or a marquee's hiding tag.
                    var label = app.Stage.Query<Label>().ToList().FirstOrDefault(l => TextFit.SourceOf(l) is string lt && lt.Contains(target) && l.resolvedStyle.display != DisplayStyle.None && l.worldBound.width > 0);
                    var btn = app.Stage.Query<Button>().ToList().FirstOrDefault(b => TextFit.SourceOf(b) is string bt && bt.Contains(target) && b.worldBound.width > 0);
                    VisualElement hit = (VisualElement)btn ?? label;
                    if (hit == null) return new { error = $"no visible text '{target}'" };
                    StartCoroutine(ScrollIntoViewThenTap(hit, reply));
                    return Pending;
                }
                case "pick":
                {
                    // Diagnostics for a tap that does nothing (GG-T16/GG-T95): what UI Toolkit would hit at a
                    // design point and whether the window has focus. Since GG-T121 automation input no longer
                    // depends on focus (it goes straight into the panel), but `focused` still tells a real
                    // player's dropped clicks apart from a game-side problem.
                    var panelPoint = ToPanel(F(q["x"]), F(q["y"]));
                    var hit = app.Stage.panel?.Pick(panelPoint);
                    var chain = new List<string>();
                    for (var e = hit; e != null; e = e.parent) chain.Add($"{e.GetType().Name}#{e.name}.{string.Join(".", e.GetClasses())}");
                    return new
                    {
                        chain, focused = Application.isFocused, hitsNothing = HitsNothing(hit), delivery = "panel",
                        screen = new[] { Screen.width, Screen.height }, panelPoint = panelPoint.ToString(),
                    };
                }
                case "type":
                {
                    // GG-T116: types into a named TextField the way a player would (the value-changed
                    // callbacks run, so a screen's own validation sees it), e.g. the New Profile name field.
                    var field = app.Stage.Q<TextField>(q["name"]);
                    if (field == null || field.resolvedStyle.display == DisplayStyle.None || field.worldBound.width <= 0)
                        return new { error = $"no visible text field named '{q["name"]}'" };
                    field.value = q["text"] ?? "";
                    return new { ok = true, value = field.value };
                }
                case "wait":
                    StartCoroutine(WaitIdle(F(q["ms"], 0) / 1000f, reply));
                    return Pending;
                case "press":
                    StartCoroutine(Press(F(q["x"]), F(q["y"]), F(q["ms"], 600) / 1000f, reply));
                    return Pending;
                case "release":
                {
                    var up = Send(lastHeld, PointerPhase.Up);
                    StartCoroutine(Frames(3, () => reply(
                        up.HitsNothing ? new { ok = false, error = "release hit nothing", target = up.Target }
                        : !up.Arrived ? new { ok = false, error = "release not delivered to the panel", target = up.Target }
                        : new { ok = true, error = (string)null, target = up.Target })));
                    return Pending;
                }
            }
            return Unrouted;
        }

        // ── Input injection (GG-T121: straight into the panel, independent of OS focus) ──

        private enum PointerPhase { Move, Down, Up }

        /// <summary>What one synthetic pointer event did: whether it reached the panel's dispatcher and what it targeted.</summary>
        private struct Delivery
        {
            public bool Arrived;
            public string Target;
            public bool HitsNothing;
        }

        private bool pointerDown;
        private Vector2 lastHeld;

        /// <summary>Design pixels → panel coordinates (the stage's own space scaled into the panel, origin top-left).</summary>
        private Vector2 ToPanel(float dx, float dy)
        {
            var stage = GameApp.I.Stage;
            return stage.worldBound.position + new Vector2(dx, dy) * (stage.worldBound.width / GameApp.StageWidth);
        }

        private Vector2 DesignPoint(Vector2 panelPoint)
        {
            var stage = GameApp.I.Stage;
            float k = GameApp.StageWidth / stage.worldBound.width;
            return (panelPoint - stage.worldBound.position) * k;
        }

        /// <summary>
        /// Nothing tappable under the point: off the panel, only the app's own scaffolding (root, letterbox,
        /// stage, the screens container) — every screen and widget lives below those — or the router's
        /// screen-transition fade, which swallows input while a screen is changing.
        /// </summary>
        private static bool HitsNothing(VisualElement hit)
        {
            if (hit == null) return true;
            var app = GameApp.I;
            return hit == app.Document.rootVisualElement || hit == app.Stage || hit == app.Stage.parent
                   || hit.name == "screens" || hit.name == "fade" || !app.Stage.Contains(hit);
        }

        private static string Describe(VisualElement e) => e == null ? null : $"{e.GetType().Name}#{e.name}";

        private void Receipt(EventBase e)
        {
            if (e != sentEvent) return;
            sentArrived = true;
            sentTarget = e.target as VisualElement;
        }

        /// <summary>
        /// Dispatches one mouse-type pointer event at a panel point through the panel's own event dispatcher —
        /// the same picking, pointer capture and ClickEvent synthesis real input goes through — and reports
        /// whether it arrived. Built from an IMGUI mouse Event, the way UI Toolkit's own event system builds them.
        /// </summary>
        private Delivery Send(Vector2 panelPoint, PointerPhase phase)
        {
            var panel = GameApp.I.Document.rootVisualElement.panel;
            var hit = panel?.Pick(panelPoint);
            var d = new Delivery { HitsNothing = HitsNothing(hit) };
            if (panel == null) return d;
            var type = phase == PointerPhase.Down ? EventType.MouseDown : phase == PointerPhase.Up ? EventType.MouseUp : pointerDown ? EventType.MouseDrag : EventType.MouseMove;
            var ev = new Event
            {
                type = type, mousePosition = panelPoint, button = 0, clickCount = phase == PointerPhase.Move ? 0 : 1,
                modifiers = EventModifiers.None, pointerType = UnityEngine.PointerType.Mouse,
            };
            EventBase evt = phase == PointerPhase.Down ? PointerDownEvent.GetPooled(ev)
                          : phase == PointerPhase.Up ? (EventBase)PointerUpEvent.GetPooled(ev)
                          : PointerMoveEvent.GetPooled(ev);
            sentEvent = evt; sentArrived = false; sentTarget = null;
            try { panel.visualTree.SendEvent(evt); }
            finally
            {
                // A pointer captured by an element (a Button between its press and release, the board during a
                // drag) gets its events straight at that element, so they may never pass the root listener —
                // but the dispatcher still gave the event a target. No receipt and no target = not delivered.
                d.Arrived = sentArrived || evt.target != null;
                d.Target = Describe(sentTarget ?? evt.target as VisualElement);
                sentEvent = null;
                evt.Dispose();
            }
            if (phase == PointerPhase.Down) pointerDown = true;
            else if (phase == PointerPhase.Up) pointerDown = false;
            return d;
        }

        private static object TapFailure(Delivery down, Delivery up, float x, float y)
        {
            if (down.HitsNothing)
                return new { ok = false, error = $"tap at ({x:0},{y:0}) hit nothing (off the stage, or only the stage background or a screen-transition fade is there)", x, y, target = down.Target };
            if (!down.Arrived || !up.Arrived)
                return new { ok = false, error = $"tap at ({x:0},{y:0}) not delivered to the panel (down={down.Arrived}, up={up.Arrived})", x, y };
            return null;
        }

        /// <summary>clicktext on a row below a ScrollView's fold (Camp's menu outgrew the stage, so Save &amp; Quit sits
        /// past y=2532): scroll it into view the way a player would, let layout settle, then tap its new centre
        /// (GG-T260). Elements outside any ScrollView tap straight away.</summary>
        private IEnumerator ScrollIntoViewThenTap(VisualElement hit, Action<object> reply)
        {
            var sv = hit.GetFirstAncestorOfType<ScrollView>();
            if (sv != null && !sv.contentViewport.worldBound.Contains(hit.worldBound.center))
            {
                sv.ScrollTo(hit);
                yield return null;
                yield return null;
            }
            var c = DesignPoint(hit.worldBound.center);
            yield return Tap(c.x, c.y, reply);
        }

        private IEnumerator Tap(float x, float y, Action<object> reply)
        {
            var p = ToPanel(x, y);
            Send(p, PointerPhase.Move); yield return null;
            var down = Send(p, PointerPhase.Down); yield return null; yield return null;
            var up = Send(p, PointerPhase.Up); yield return null; yield return null;
            reply(TapFailure(down, up, x, y) ?? new { ok = true, x, y, target = down.Target });
        }

        private static bool Opaque(VisualElement e)
        {
            for (var x = e; x != null; x = x.parent) if (x.resolvedStyle.opacity <= 0.01f) return false;
            return true;
        }

        private IEnumerator Frames(int n, Action done) { for (int i = 0; i < n; i++) yield return null; done(); }

        /// <summary>Press and hold at a design point for a while, then reply (button stays down until `release`).</summary>
        private IEnumerator Press(float x, float y, float seconds, Action<object> reply)
        {
            var p = ToPanel(x, y);
            lastHeld = p;
            Send(p, PointerPhase.Move); yield return null;
            var down = Send(p, PointerPhase.Down);
            if (down.HitsNothing) { reply(new { ok = false, error = $"press at ({x:0},{y:0}) hit nothing", target = down.Target }); yield break; }
            if (!down.Arrived) { reply(new { ok = false, error = "press not delivered to the panel" }); yield break; }
            yield return new WaitForSecondsRealtime(seconds);
            reply(new { ok = true, target = down.Target });
        }

        private IEnumerator Drag(List<Vector2> pts, float stepSeconds, Action<object> reply)
        {
            if (pts.Count == 0) { reply(new { ok = false, error = "empty path" }); yield break; }
            var start = ToPanel(pts[0].x, pts[0].y);
            Send(start, PointerPhase.Move); yield return null;
            var down = Send(start, PointerPhase.Down); yield return null;
            if (!down.Arrived || down.HitsNothing)
            {
                Send(start, PointerPhase.Up);
                holdDrag = false;
                reply(new { ok = false, error = down.HitsNothing ? $"drag start ({pts[0].x:0},{pts[0].y:0}) hit nothing" : "drag not delivered to the panel", target = down.Target });
                yield break;
            }
            int lost = 0;
            for (int i = 1; i < pts.Count; i++)
            {
                var a = pts[i - 1]; var b = pts[i];
                int n = Mathf.Max(2, Mathf.CeilToInt((b - a).magnitude / 30f));
                for (int k = 1; k <= n; k++)
                {
                    var p = Vector2.Lerp(a, b, k / (float)n);
                    if (!Send(ToPanel(p.x, p.y), PointerPhase.Move).Arrived) lost++;
                    yield return new WaitForSecondsRealtime(stepSeconds / n);
                }
            }
            var last = pts[pts.Count - 1];
            yield return null;
            lastHeld = ToPanel(last.x, last.y);
            if (lost > 0) { Send(lastHeld, PointerPhase.Up); holdDrag = false; reply(new { ok = false, error = $"{lost} drag moves not delivered to the panel" }); yield break; }
            if (holdDrag) { holdDrag = false; reply(new { ok = true, held = true, target = down.Target }); yield break; }
            var up = Send(lastHeld, PointerPhase.Up);
            yield return null; yield return null;
            reply(up.Arrived ? (object)new { ok = true, points = pts.Count, target = down.Target } : new { ok = false, error = "drag release not delivered to the panel" });
        }

        private IEnumerator WaitIdle(float minSeconds, Action<object> reply)
        {
            float start = Time.unscaledTime;
            yield return null;
            while (Time.unscaledTime - start < 30f)
            {
                bool busy = GameApp.I.Router.Current is BattleScreen b && b.Busy;
                if (!busy && Time.unscaledTime - start >= minSeconds) break;
                yield return null;
            }
            yield return new WaitForSecondsRealtime(0.25f);
            reply(StateDump());
        }
    }
}
#endif
