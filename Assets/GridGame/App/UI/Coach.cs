using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>One coach mark (GG-T539): what to box, what the player must do there, and when it is done.</summary>
    public sealed class CoachStep
    {
        /// <summary>One line said next to the highlight ("Tap Heal", "Drag the Paladin along the dotted line").</summary>
        public string Caption;
        /// <summary>The element to box (resolved every frame, so a rebuilt screen is followed).</summary>
        public Func<VisualElement> Element;
        /// <summary>Or a box in panel (world) space: a board tile.</summary>
        public Func<Rect?> Area;
        /// <summary>A dotted path in panel space from the boxed actor to its destination, with an arrowhead at the end.</summary>
        public Func<List<Vector2>> Path;
        /// <summary>A second box (panel space) drawn at the destination of a move.</summary>
        public Func<Rect?> Goal;
        /// <summary>Done when this turns true. Null: a tap inside the box completes the step.</summary>
        public Func<bool> Done;
        /// <summary>GG-T542: with <see cref="Done"/> null, the step is also done when this turns true: the tapped control's
        /// effect landed (a Craft whose page rebuilt under the finger, so the release never read as a tap in the box).</summary>
        public Func<bool> Also;
        /// <summary>The step can still be done; when this turns false the lesson's marks stop (never a soft-lock).</summary>
        public Func<bool> Valid;
        /// <summary>Nothing real under the box: tapping it only moves on (a tour of the timeline, the mana globe).</summary>
        public bool Info;
        /// <summary>The caption docks at the safe bottom edge (the top, when the box is low) instead of beside the box: a
        /// move's route covers the board, and a caption beside it would hide the heroes it names.</summary>
        public bool Dock;
        /// <summary>Run when the step comes up and when it ends (the battle arms and disarms its drag rail here).</summary>
        public Action Enter, Exit;
    }

    /// <summary>One lesson: the popup that opens it, then its coach marks in order.</summary>
    public sealed class CoachLesson
    {
        /// <summary>The save flag suffix ("lesson:&lt;Id&gt;") and the lesson image's name (Resources/Tutorial/&lt;Id&gt;.png).</summary>
        public string Id;
        public string Kicker = "Lesson";
        public string Title, Text;
        /// <summary>Image resource override (default "Tutorial/" + Id; the file is &lt;folder&gt;/&lt;name&gt;-00, GG-T561); a missing
        /// image is simply left out.</summary>
        public string Image;
        public List<CoachStep> Steps = new List<CoachStep>();
        /// <summary>The popup was already shown (marks retried when the situation came up later): go straight to the steps.</summary>
        public bool SkipPopup;
        /// <summary>While true the coach hides and waits (the pause menu, battle playback).</summary>
        public Func<bool> Hold;
        /// <summary>Called once at the end: <see cref="CoachEnd"/> says how it ended.</summary>
        public Action<CoachEnd> Finished;
        /// <summary>Automation names for the popup card and its Next button (camp lessons keep the old camp-tip names).</summary>
        public string CardName = "coach-card", NextName = "coach-next", RingName = "coach-box";
    }

    public enum CoachEnd
    {
        /// <summary>Every step was done (or the lesson had none and Next was pressed).</summary>
        Completed,
        /// <summary>A step stopped being possible, or its target never appeared: the popup text stands, the game goes on.</summary>
        Aborted,
        /// <summary>Skip tutorial was confirmed: every lesson is off.</summary>
        TurnedOff,
        /// <summary>Torn down from outside (the screen left, autoplay took over).</summary>
        Cancelled,
    }

    /// <summary>
    /// Guided coach marks (GG-T539, owner 2026-11-21: "it shows the user what to do with highlights and arrows and dotted
    /// lines ... draw boxes around buttons and wait for the user to click that exact area"). A lesson opens with a blocking
    /// popup (title, a line or two, an image); after Next the stage dims except the boxed target, an arrow (or for a move, a
    /// dotted path) points at it, and only that area takes input until the player does the exact thing. Lives on the global
    /// Overlay so a lesson may walk across screens (Camp → Heroes → Equip). Never soft-locks: a target that doesn't appear in
    /// <see cref="MissingTimeout"/> seconds, or a step whose <see cref="CoachStep.Valid"/> turns false, ends the marks.
    /// </summary>
    public static class Coach
    {
        public const string OffFlag = "lessons:off", SeenPrefix = "lesson:";
        private const float MissingTimeout = 2.5f, BoxPad = 10f, ArrowGap = 28f;

        private static CoachLesson lesson;
        private static int stepIndex = -1;
        private static VisualElement layer, veil, card, box, catcher, caption;
        private static Label captionText;
        private static readonly VisualElement[] shades = new VisualElement[4];
        private static CoachCanvas canvas;
        private static float missing, pulseT, lastTick;
        private static bool tapped, downInHole, listening;
        private static Rect hole; // layer-local
        private static Rect? goal;
        private static List<Vector2> path;

        public static bool Active => lesson != null;

        /// <summary>A lesson image's Resources key: "Tutorial/hub-shop" reads Tutorial/hub-shop-00 (the asset naming rule,
        /// <see cref="GridGame.Core.Content.AssetNames"/>, GG-T561).</summary>
        internal static string ImageKey(string image)
        {
            if (string.IsNullOrEmpty(image)) return image;
            int slash = image.LastIndexOf('/');
            return slash < 0 ? GridGame.Core.Content.AssetNames.Of(image)
                : GridGame.Core.Content.AssetNames.Path(image.Substring(0, slash), image.Substring(slash + 1));
        }
        /// <summary>The marks are up (past the popup): input outside the box is refused.</summary>
        public static bool InSteps => lesson != null && stepIndex >= 0;
        public static CoachStep Step => InSteps && stepIndex < lesson.Steps.Count ? lesson.Steps[stepIndex] : null;
        public static string LessonId => lesson?.Id;

        /// <summary>Bots and harness runs see no blocking lessons unless the run asks for them (`-coach`, or the older
        /// `-campTips`); the battle tutorial then keeps its non-blocking Tutorial tab.</summary>
        public static bool Suppressed =>
            DemoMode.Active || TestMode.Active || CommandLine.Has("-autobattle") ||
            (CommandLine.Has("-automation") && !CommandLine.Has("-coach") && !CommandLine.Has("-campTips"));

        /// <summary>Lessons are off for this save: Skip tutorial was confirmed, or it's a bot save.</summary>
        public static bool Off(SaveData s) => s == null || s.TestMode || s.Flags.Contains(OffFlag);
        public static bool Seen(SaveData s, string id) => s != null && s.Flags.Contains(SeenPrefix + id);
        /// <summary>A lesson that should show now: coach marks on, lessons on, not seen yet, nothing else teaching.</summary>
        public static bool Pending(SaveData s, string id) => !Suppressed && !Off(s) && !Seen(s, id) && !Active;
        public static void MarkSeen(SaveData s, string id) { if (s != null && id != null) s.Flags.Add(SeenPrefix + id); }

        /// <summary>Settings › How to play › Replay tutorials: every lesson, the first battle's and Camp's, shows again.</summary>
        public static void ResetAll(SaveData s)
        {
            if (s == null) return;
            s.Flags.RemoveWhere(f => f.StartsWith(SeenPrefix) || f == OffFlag);
            s.TutorialDone = false;
            CampTips.ResetAll(s);
        }

        /// <summary>Skip tutorial, confirmed: every lesson off for this save, the first battle's and Camp's alike.</summary>
        public static void TurnOff(SaveData s)
        {
            if (s == null) return;
            s.Flags.Add(OffFlag);
            s.TutorialDone = true;
            foreach (var t in CampTips.All) CampTips.MarkSeen(s, t.Station);
        }

        /// <summary>Raised when Skip tutorial is confirmed (the battle drops its Tutorial tab).</summary>
        public static event Action TurnedOff;

        /// <summary>Opens <paramref name="l"/>. False when another lesson is running.</summary>
        public static bool Run(CoachLesson l)
        {
            // A lesson whose layer left the panel (its schedule stopped) can never finish on its own: clear it first.
            if (lesson != null && (layer == null || layer.panel == null)) End(CoachEnd.Cancelled);
            if (l == null || lesson != null || GameApp.I == null) return false;
            lesson = l;
            stepIndex = -1;
            if (!l.SkipPopup) InspectCard.Hide(); // a card left open from the last tap must not sit over the popup
            EnsureListening();
            Build();
            if (l.SkipPopup) BeginSteps(); else ShowPopup();
            return true;
        }

        /// <summary>Tears the current lesson down without marking it (autoplay took over, the screen went away).</summary>
        public static void Cancel() => End(CoachEnd.Cancelled);

        // ── Building ────────────────────────────────────────────────────────────────

        private static bool ReduceMotion => GameApp.I?.Settings?.ReduceMotion ?? false;

        private static void Build()
        {
            layer?.RemoveFromHierarchy();
            layer = new VisualElement { name = "coach", pickingMode = PickingMode.Ignore };
            layer.AddToClassList("fill");
            GameApp.I.Overlay.Add(layer);
            for (int i = 0; i < 4; i++)
            {
                var sh = new VisualElement { name = "coach-shade", pickingMode = PickingMode.Position };
                sh.style.position = Position.Absolute;
                sh.style.backgroundColor = new Color(0f, 0f, 0.04f, 0.62f);
                sh.style.display = DisplayStyle.None;
                // A tap outside the box does nothing but nudge the highlight.
                sh.RegisterCallback<PointerDownEvent>(e => { e.StopPropagation(); Nudge(); });
                layer.Add(sh);
                shades[i] = sh;
            }
            catcher = new VisualElement { name = "coach-catcher", pickingMode = PickingMode.Position };
            catcher.style.position = Position.Absolute;
            catcher.style.display = DisplayStyle.None;
            layer.Add(catcher);
            canvas = new CoachCanvas { name = "coach-canvas", pickingMode = PickingMode.Ignore };
            canvas.AddToClassList("fill");
            layer.Add(canvas);
            box = new VisualElement { name = lesson.RingName, pickingMode = PickingMode.Ignore };
            box.style.position = Position.Absolute;
            Border(box, 6f, Palette.Gold, 16f);
            box.style.display = DisplayStyle.None;
            layer.Add(box);
            layer.schedule.Execute(Tick).Every(16);
            lastTick = Time.unscaledTime;
        }

        private static void Border(VisualElement e, float w, Color c, float r)
        {
            e.style.borderTopWidth = e.style.borderBottomWidth = e.style.borderLeftWidth = e.style.borderRightWidth = w;
            e.style.borderTopColor = e.style.borderBottomColor = e.style.borderLeftColor = e.style.borderRightColor = c;
            e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = r;
        }

        private static void ShowPopup()
        {
            veil = new VisualElement { name = "coach-veil", pickingMode = PickingMode.Position };
            veil.AddToClassList("fill");
            veil.style.backgroundColor = new Color(0f, 0f, 0.04f, 0.66f);
            veil.style.justifyContent = Justify.Center;
            veil.style.paddingTop = GameApp.SafeTop + Space.S48;
            veil.style.paddingBottom = GameApp.SafeBottom + Space.S48;
            layer.Add(veil);
            card = Ui.Window(null, veil);
            card.name = lesson.CardName;
            Fit.Center(card);
            Ui.Caption(lesson.Kicker, card, "gold").name = "coach-kicker";
            var title = Ui.Heading(lesson.Title ?? "", card);
            title.name = "coach-title";
            title.style.whiteSpace = WhiteSpace.Normal;
            var tex = Resources.Load<Texture2D>(ImageKey(lesson.Image ?? "Tutorial/" + lesson.Id));
            if (tex != null)
            {
                var img = new VisualElement { name = "coach-image", pickingMode = PickingMode.Ignore };
                float w = Fit.Column - Kit.Gutter * 2f - Kit.PadX * 2f - Space.S16;
                float h = Mathf.Min(w * tex.height / Mathf.Max(1f, tex.width), Fit.H(30f));
                img.style.height = h;
                img.style.marginTop = img.style.marginBottom = Space.S16;
                img.style.backgroundImage = new StyleBackground(tex);
                img.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
                card.Add(img);
            }
            var text = Ui.Para(lesson.Text ?? "", card);
            text.name = "coach-text";
            text.style.whiteSpace = WhiteSpace.Normal;
            var row = Ui.El("kit-footer", card);
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.marginTop = Space.S24;
            Ui.Button("Skip tutorial", AskTurnOff, "btn--small", row).name = "coach-skip";
            Ui.Button("Next", BeginSteps, "btn--primary", row).name = lesson.NextName;
            // The first box shows under the popup already, so the popup reads as "this, here".
            PreviewFirstBox();
            GameApp.I.Audio?.Sfx("ui.select", 0.7f);
            if (!ReduceMotion)
            {
                veil.style.opacity = 0f;
                veil.schedule.Execute(() => { if (veil != null) veil.style.opacity = 1f; }).ExecuteLater(16);
            }
        }

        private static void PreviewFirstBox()
        {
            var first = lesson.Steps.FirstOrDefault();
            if (first == null || !Resolve(first, out var r)) return;
            Place(box, r, BoxPad);
            box.style.display = DisplayStyle.Flex;
        }

        private static void AskTurnOff()
        {
            Confirm.Show("Turn off every tutorial lesson? You can replay them from Settings › How to play.", "Turn off", () =>
            {
                var s = GameApp.I.Save;
                TurnOff(s);
                GameApp.I.Persist();
                End(CoachEnd.TurnedOff);
                TurnedOff?.Invoke();
            });
        }

        // ── Steps ───────────────────────────────────────────────────────────────────

        private static void BeginSteps()
        {
            if (lesson == null) return;
            veil?.RemoveFromHierarchy();
            veil = null; card = null;
            stepIndex = -1;
            NextStep();
        }

        private static void NextStep()
        {
            if (lesson == null) return;
            Step?.Exit?.Invoke();
            stepIndex++;
            // A step already done (the hero is already selected, the cast already confirmed) is passed straight away.
            while (stepIndex < lesson.Steps.Count)
            {
                var s = lesson.Steps[stepIndex];
                s.Enter?.Invoke();
                if (s.Done == null || !SafeTrue(s.Done)) break;
                s.Exit?.Invoke();
                stepIndex++;
            }
            if (stepIndex >= lesson.Steps.Count) { End(CoachEnd.Completed); return; }
            missing = 0f; tapped = false; downInHole = false;
            BuildCaption(Step);
            Tick();
            if (Step != null && GameApp.I != null) GameApp.I.Audio?.Sfx("ui.select", 0.45f);
        }

        private static bool SafeTrue(Func<bool> f)
        {
            try { return f(); }
            catch (Exception ex) { Debug.LogException(ex); return false; }
        }

        private static void BuildCaption(CoachStep s)
        {
            caption?.RemoveFromHierarchy();
            caption = Ui.Window(null, layer, "window--compact");
            caption.name = "coach-caption";
            caption.pickingMode = PickingMode.Position;
            caption.style.position = Position.Absolute;
            caption.style.left = caption.style.right = Fit.Inset + Fit.Edge;
            var row = Ui.El("row", caption);
            row.style.alignItems = Align.Center;
            captionText = Ui.Para(s.Caption ?? "", row);
            captionText.name = "coach-caption-text";
            captionText.style.whiteSpace = WhiteSpace.Normal;
            captionText.style.flexGrow = 1; captionText.style.flexShrink = 1;
            var skip = Ui.Button("Skip tutorial", AskTurnOff, "btn--small", row);
            skip.name = "coach-skip";
            skip.style.flexShrink = 0;
            skip.style.marginLeft = Space.S16;
        }

        private static bool Resolve(CoachStep s, out Rect world)
        {
            world = default;
            try
            {
                if (s.Area != null && s.Area() is Rect a) { world = a; return a.width > 0f; }
                var e = s.Element?.Invoke();
                if (e == null || e.panel == null || e.resolvedStyle.display == DisplayStyle.None) return false;
                for (var p = e; p != null; p = p.parent) if (p.resolvedStyle.display == DisplayStyle.None) return false;
                world = e.worldBound;
                // A target scrolled out of its list is brought into view first.
                var sv = e.GetFirstAncestorOfType<ScrollView>();
                if (sv != null && !sv.contentViewport.worldBound.Overlaps(world)) { sv.ScrollTo(e); return false; }
                if (sv != null) world = Clip(world, sv.contentViewport.worldBound);
                return world.width > 0f && world.height > 0f;
            }
            catch (Exception ex) { Debug.LogException(ex); return false; }
        }

        private static Rect Clip(Rect r, Rect to) =>
            Rect.MinMaxRect(Mathf.Max(r.xMin, to.xMin), Mathf.Max(r.yMin, to.yMin), Mathf.Min(r.xMax, to.xMax), Mathf.Min(r.yMax, to.yMax));

        private static bool Disabled(CoachStep s)
        {
            if (s.Area != null) return false;
            VisualElement e = null;
            try { e = s.Element?.Invoke(); } catch { }
            return e != null && (!e.enabledInHierarchy || e.ClassListContains("btn--disabled") || e.ClassListContains("menu-row--disabled"));
        }

        private static Rect ToLayer(Rect world)
        {
            var a = layer.WorldToLocal(world.min);
            var b = layer.WorldToLocal(world.max);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        private static void Place(VisualElement e, Rect worldRect, float pad)
        {
            var r = ToLayer(worldRect);
            e.style.left = r.xMin - pad; e.style.top = r.yMin - pad;
            e.style.width = r.width + pad * 2f; e.style.height = r.height + pad * 2f;
        }

        private static void Tick()
        {
            if (lesson == null || layer == null) return;
            float now = Time.unscaledTime, dt = Mathf.Clamp(now - lastTick, 0f, 0.1f);
            lastTick = now;
            bool hold = lesson.Hold != null && SafeTrue(lesson.Hold);
            layer.style.display = hold ? DisplayStyle.None : DisplayStyle.Flex;
            if (hold || !InSteps) return;
            // The marks stay on top of anything opened since (a tapped unit's card, a toast), but under a confirm dialog.
            var ov = layer.parent;
            if (ov != null && ov.IndexOf(layer) != ov.childCount - 1 && ov.Q("confirm-veil") == null) layer.BringToFront();
            var s = Step;
            if (s == null) return;
            if (s.Done != null && SafeTrue(s.Done)) { NextStep(); return; }
            if (s.Done == null && (tapped || (s.Also != null && SafeTrue(s.Also)))) { NextStep(); return; }
            if (s.Valid != null && !SafeTrue(s.Valid)) { End(CoachEnd.Aborted); return; }
            if (!Resolve(s, out var world))
            {
                missing += dt;
                foreach (var sh in shades) sh.style.display = DisplayStyle.None;
                box.style.display = catcher.style.display = DisplayStyle.None;
                caption.style.display = DisplayStyle.None;
                canvas.Blank();
                if (missing > MissingTimeout) End(CoachEnd.Aborted);
                return;
            }
            missing = 0f;
            caption.style.display = DisplayStyle.Flex;
            hole = ToLayer(world);
            var padded = new Rect(hole.xMin - BoxPad, hole.yMin - BoxPad, hole.width + BoxPad * 2f, hole.height + BoxPad * 2f);
            LayShades(padded);
            Place(box, world, BoxPad);
            box.style.display = DisplayStyle.Flex;
            // Nothing real to press (a tour stop) or the real control can't be pressed (too poor to buy): tap the box to go on.
            bool info = s.Info || (s.Done == null && Disabled(s));
            catcher.style.display = info ? DisplayStyle.Flex : DisplayStyle.None;
            if (info) Place(catcher, world, BoxPad);
            // Pulse the box (a static box under Reduce Motion).
            pulseT += dt;
            float a = ReduceMotion ? 1f : 0.65f + 0.35f * Mathf.Sin(pulseT * 5f);
            var c = Palette.Gold.WithA(a);
            box.style.borderTopColor = box.style.borderBottomColor = box.style.borderLeftColor = box.style.borderRightColor = c;

            path = null; goal = null;
            if (s.Path != null) { var p = s.Path(); if (p != null && p.Count >= 2) path = p.Select(v => layer.WorldToLocal(v)).ToList(); }
            if (s.Goal != null && s.Goal() is Rect g) goal = ToLayer(g);
            LayCaption(padded, s.Dock);
            canvas.Draw(path, goal, ArrowFromCaption(padded), ReduceMotion ? 0f : pulseT);
        }

        private static void LayShades(Rect r)
        {
            float w = layer.layout.width, h = layer.layout.height;
            if (float.IsNaN(w) || w <= 0f) { w = GameApp.StageWidth; h = GameApp.StageHeight; }
            float x0 = Mathf.Clamp(r.xMin, 0f, w), x1 = Mathf.Clamp(r.xMax, 0f, w), y0 = Mathf.Clamp(r.yMin, 0f, h), y1 = Mathf.Clamp(r.yMax, 0f, h);
            Set(shades[0], 0f, 0f, w, y0);            // above
            Set(shades[1], 0f, y1, w, h - y1);        // below
            Set(shades[2], 0f, y0, x0, y1 - y0);      // left
            Set(shades[3], x1, y0, w - x1, y1 - y0);  // right
            static void Set(VisualElement e, float x, float y, float ww, float hh)
            {
                e.style.display = ww > 0f && hh > 0f ? DisplayStyle.Flex : DisplayStyle.None;
                e.style.left = x; e.style.top = y; e.style.width = Mathf.Max(0f, ww); e.style.height = Mathf.Max(0f, hh);
            }
        }

        private static bool captionAbove;

        /// <summary>The caption sits on whichever side of the box has more room, inside the safe area.</summary>
        private static void LayCaption(Rect r, bool dock)
        {
            float h = layer.layout.height;
            if (float.IsNaN(h) || h <= 0f) h = GameApp.StageHeight;
            float top = GameApp.SafeTop + Space.S24, bottom = h - GameApp.SafeBottom - Space.S24;
            float ch = caption.resolvedStyle.height;
            if (float.IsNaN(ch) || ch <= 0f) ch = 200f;
            float room = 120f;
            // A move's dotted path runs below/above the box too; keep clear of the whole route.
            float yMin = r.yMin, yMax = r.yMax;
            if (path != null) foreach (var p in path) { yMin = Mathf.Min(yMin, p.y - 100f); yMax = Mathf.Max(yMax, p.y + 100f); }
            if (goal is Rect g) { yMin = Mathf.Min(yMin, g.yMin); yMax = Mathf.Max(yMax, g.yMax); }
            captionAbove = yMin - top > bottom - yMax;
            if (dock)
            {
                // Docked: below everything the mark covers when it fits there, else at the top.
                captionAbove = bottom - ch < yMax && top + ch < yMin;
                caption.style.top = captionAbove ? top : bottom - ch;
                return;
            }
            float y = captionAbove ? Mathf.Max(top, yMin - room - ch) : Mathf.Min(bottom - ch, yMax + room);
            caption.style.top = y;
        }

        private static (Vector2 from, Vector2 to)? ArrowFromCaption(Rect r)
        {
            if (path != null) return null; // the dotted path is the arrow
            float ct = caption.resolvedStyle.top, ch = caption.resolvedStyle.height;
            if (float.IsNaN(ct) || float.IsNaN(ch)) return null;
            float x = Mathf.Clamp(r.center.x, Fit.Inset + Fit.Edge + 60f, GameApp.StageWidth - Fit.Inset - Fit.Edge - 60f);
            var from = captionAbove ? new Vector2(x, ct + ch + 8f) : new Vector2(x, ct - 8f);
            var to = captionAbove ? new Vector2(r.center.x, r.yMin - ArrowGap * 0.3f) : new Vector2(r.center.x, r.yMax + ArrowGap * 0.3f);
            return (from - to).magnitude < 30f ? ((Vector2, Vector2)?)null : (from, to);
        }

        private static void Nudge()
        {
            GameApp.I?.Audio?.Sfx("ui.error", 0.35f);
            if (box == null || ReduceMotion) return;
            GameApp.I.StartCoroutine(Tween.Pop(box, 1.08f, 0.25f));
        }

        /// <summary>Automation (GG-T539): the lesson and its current mark, points mapped by <paramref name="toDesign"/> from panel space.</summary>
        public static object Describe(Func<Vector2, Vector2> toDesign)
        {
            if (lesson == null) return null;
            var s = Step;
            object box = null, goalBox = null;
            List<float[]> route = null;
            if (s != null && Resolve(s, out var world))
            {
                var a = toDesign(world.min); var b = toDesign(world.max);
                box = new[] { a.x, a.y, b.x, b.y };
                if (s.Goal != null && s.Goal() is Rect g) { var ga = toDesign(g.min); var gb = toDesign(g.max); goalBox = new[] { ga.x, ga.y, gb.x, gb.y }; }
                if (s.Path != null && s.Path() is List<Vector2> p) route = p.Select(v => { var d = toDesign(v); return new[] { d.x, d.y }; }).ToList();
            }
            return new
            {
                active = true, id = lesson.Id, title = lesson.Title, popup = !InSteps, step = stepIndex, steps = lesson.Steps.Count,
                caption = s?.Caption, info = s != null && s.Info, box, goal = goalBox, path = route,
            };
        }

        // ── Taps inside the box ─────────────────────────────────────────────────────

        private static void EnsureListening()
        {
            if (listening) return;
            var root = GameApp.I.Document.rootVisualElement;
            root.RegisterCallback<PointerDownEvent>(e => { downInHole = InSteps && InHole(e.position); }, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerUpEvent>(e =>
            {
                if (InSteps && downInHole && InHole(e.position))
                {
                    // Let the real control act on this release first; the step moves on next tick.
                    tapped = true;
                }
                downInHole = false;
            }, TrickleDown.TrickleDown);
            listening = true;
        }

        private static bool InHole(Vector2 panelPos)
        {
            if (layer == null || layer.panel == null) return false;
            var p = layer.WorldToLocal(panelPos);
            var r = new Rect(hole.xMin - BoxPad, hole.yMin - BoxPad, hole.width + BoxPad * 2f, hole.height + BoxPad * 2f);
            return r.Contains(p);
        }

        // ── Ending ──────────────────────────────────────────────────────────────────

        private static void End(CoachEnd how)
        {
            var l = lesson;
            if (l == null) return;
            Step?.Exit?.Invoke();
            lesson = null;
            stepIndex = -1;
            layer?.RemoveFromHierarchy();
            layer = veil = card = box = catcher = caption = null;
            canvas = null;
            path = null; goal = null;
            if (how == CoachEnd.Completed || how == CoachEnd.Aborted)
            {
                MarkSeen(GameApp.I?.Save, l.Id);
                GameApp.I?.Persist();
            }
            try { l.Finished?.Invoke(how); }
            catch (Exception ex) { Debug.LogException(ex); }
        }
    }

    /// <summary>Draws a move's dotted path with its arrowhead, the destination's dashed box and the caption's pointer.</summary>
    internal sealed class CoachCanvas : VisualElement
    {
        private List<Vector2> path;
        private Rect? goal;
        private (Vector2 from, Vector2 to)? arrow;
        private float t;

        public CoachCanvas() { generateVisualContent += Paint; }

        public void Draw(List<Vector2> p, Rect? g, (Vector2, Vector2)? a, float time)
        {
            path = p; goal = g; arrow = a; t = time;
            MarkDirtyRepaint();
        }

        public void Blank() { path = null; goal = null; arrow = null; MarkDirtyRepaint(); }

        private static readonly Color Ink = new Color(0f, 0f, 0.05f, 0.85f);

        private void Paint(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            var gold = Palette.Gold;
            if (goal is Rect g) DashedRect(p, new Rect(g.xMin - 6f, g.yMin - 6f, g.width + 12f, g.height + 12f), gold);
            if (path != null && path.Count >= 2)
            {
                // Dots march along the route (still under Reduce Motion), then an arrowhead on the destination.
                const float spacing = 34f;
                float total = 0f;
                for (int i = 1; i < path.Count; i++) total += Vector2.Distance(path[i - 1], path[i]);
                for (float d = 20f + (t * 60f) % spacing; d < total - 40f; d += spacing) Dot(p, PointAt(path, d), 9f, gold);
                var end = path[path.Count - 1];
                var prev = path[path.Count - 2];
                ArrowHead(p, end, (end - prev).normalized, 46f, gold);
            }
            if (arrow is (Vector2 from, Vector2 to))
            {
                var dir = (to - from).normalized;
                var shaftEnd = to - dir * 40f;
                p.lineCap = LineCap.Round;
                p.lineWidth = 16f; p.strokeColor = Ink;
                p.BeginPath(); p.MoveTo(from); p.LineTo(shaftEnd); p.Stroke();
                p.lineWidth = 10f; p.strokeColor = gold;
                p.BeginPath(); p.MoveTo(from); p.LineTo(shaftEnd); p.Stroke();
                ArrowHead(p, to, dir, 52f, gold);
            }
        }

        private static Vector2 PointAt(List<Vector2> pts, float d)
        {
            for (int i = 1; i < pts.Count; i++)
            {
                float len = Vector2.Distance(pts[i - 1], pts[i]);
                if (d <= len) return Vector2.Lerp(pts[i - 1], pts[i], len <= 0f ? 0f : d / len);
                d -= len;
            }
            return pts[pts.Count - 1];
        }

        private static void Dot(Painter2D p, Vector2 c, float r, Color col)
        {
            void Oct(float rr, Color cc)
            {
                p.fillColor = cc;
                p.BeginPath();
                for (int k = 0; k < 8; k++)
                {
                    float ang = k * Mathf.PI / 4f;
                    var v = c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rr;
                    if (k == 0) p.MoveTo(v); else p.LineTo(v);
                }
                p.ClosePath();
                p.Fill();
            }
            Oct(r + 4f, Ink);
            Oct(r, col);
        }

        private static void ArrowHead(Painter2D p, Vector2 tip, Vector2 dir, float size, Color col)
        {
            var side = new Vector2(-dir.y, dir.x);
            var a = tip - dir * size + side * size * 0.6f;
            var b = tip - dir * size - side * size * 0.6f;
            p.fillColor = Ink;
            p.BeginPath(); p.MoveTo(tip + dir * 6f); p.LineTo(a - dir * 4f + side * 5f); p.LineTo(b - dir * 4f - side * 5f); p.ClosePath(); p.Fill();
            p.fillColor = col;
            p.BeginPath(); p.MoveTo(tip); p.LineTo(a); p.LineTo(b); p.ClosePath(); p.Fill();
        }

        private static void DashedRect(Painter2D p, Rect r, Color col)
        {
            p.lineWidth = 6f; p.strokeColor = col; p.lineCap = LineCap.Butt;
            var pts = new[] { new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax) };
            const float dash = 22f, gap = 14f;
            for (int i = 0; i < 4; i++)
            {
                var a = pts[i]; var b = pts[(i + 1) % 4];
                float len = Vector2.Distance(a, b);
                var dir = (b - a) / Mathf.Max(1f, len);
                for (float d = 0f; d < len; d += dash + gap)
                {
                    p.BeginPath(); p.MoveTo(a + dir * d); p.LineTo(a + dir * Mathf.Min(len, d + dash)); p.Stroke();
                }
            }
        }
    }
}
