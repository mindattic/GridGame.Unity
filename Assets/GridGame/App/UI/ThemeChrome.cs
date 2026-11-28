using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using GridGame.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>
    /// GG-T564: Brightness/Contrast/Gamma for the parts of a theme Theme.uss paints (its <c>.theme-&lt;style&gt;</c> block,
    /// or the base rules for Classic): window frames with no frame colours of their own, plain windows, buttons, tabs, the
    /// header, toasts, selected rows and cards. The stylesheet stays the one place those colours are written: when the
    /// active theme is tuned, a hidden probe of each element kind (and its pressed / hover / selected states) is laid
    /// under the stage, its resolved colours are read back, run through <see cref="WindowTone"/> and set inline on the
    /// real elements, and re-set as their state changes. Dim caption text gets a readable floor (3:1 against the tuned
    /// window, <see cref="WindowTone.CaptionContrast"/>). At 0.5/0.5/0.5 nothing is set inline: the stylesheet look,
    /// pseudo-states and all, is untouched.
    /// </summary>
    public static class ThemeChrome
    {
        /// <summary>The element kinds probed, most specific last within a kind. A key matches an element that carries
        /// every class in it; the match with the most classes wins.</summary>
        private static readonly string[] Kinds =
        {
            "window", "window window--plain",
            "btn", "btn btn--primary", "btn btn--danger",
            "tab", "tab tab--active",
            "header", "toast", "stage-frame", "thumb", "role-badge",
            "menu-row menu-row--selected", "card", "card card--selected",
        };

        /// <summary>States probed on top of each kind (a class, or a pseudo-state for hover).</summary>
        private const string Pressed = "is-pressed", Hover = ":hover";

        private sealed class Swatch
        {
            public Color Bg, Top, Left, Right, Bottom, Text;
        }

        private sealed class Owned
        {
            public string Key, Style;
            public WindowTone Tone;
            public bool Bg, Border, Text, Hooked;
        }

        private static readonly Dictionary<string, Dictionary<string, Swatch>> probed = new Dictionary<string, Dictionary<string, Swatch>>();
        private static readonly ConditionalWeakTable<VisualElement, Owned> owned = new ConditionalWeakTable<VisualElement, Owned>();
        private static readonly List<WeakReference<VisualElement>> touched = new List<WeakReference<VisualElement>>();
        private static IVisualElementScheduledItem sweep;
        private static bool probing;

        private static string StyleKey => WindowThemes.UssClass ?? "classic";

        /// <summary>The active theme changed (Art.ApplyTheme): re-tone the chrome once Ui.ApplyMenuTheme has put the new
        /// frame style's class on the root, and keep sweeping new elements while the theme is tuned.</summary>
        internal static void ThemeChanged()
        {
            var stage = GameApp.I != null ? GameApp.I.Stage : null;
            if (stage == null) return;
            stage.schedule.Execute(() => Run(stage));
            if (WindowThemes.Current.Tone.IsNeutral) { sweep?.Pause(); return; }
            // New elements (a screen build, a popup, a toast) are picked up by a light sweep while the theme is tuned.
            if (sweep == null) sweep = stage.schedule.Execute(() => Run(stage)).Every(200);
            else sweep.Resume();
        }

        private static readonly string[] SweepClasses = { "window", "btn", "tab", "header", "toast", "stage-frame", "thumb", "role-badge", "menu-row", "card" };

        private static void Run(VisualElement stage)
        {
            if (probing) return;
            var tone = WindowThemes.Current.Tone;
            if (tone.IsNeutral) { ClearAll(); return; }
            if (!probed.TryGetValue(StyleKey, out var table)) { Probe(stage); return; }
            var root = GameApp.I?.Document != null ? GameApp.I.Document.rootVisualElement : stage;
            foreach (var cls in SweepClasses)
                root.Query(className: cls).ForEach(e => Apply(e, table, tone));
            var floor = CaptionFloor(table);
            root.Query(className: "dim").ForEach(e => ApplyDim(e, floor));
        }

        // ── Probing the stylesheet ──────────────────────────────────────────────────

        private static PropertyInfo pseudoStates;
        private static object hoverState;

        private static void Probe(VisualElement stage)
        {
            if (probing) return;
            probing = true;
            string style = StyleKey;
            var box = new VisualElement { name = "theme-chrome-probe", pickingMode = PickingMode.Ignore };
            box.style.position = Position.Absolute;
            box.style.left = Fit.Offstage; box.style.top = Space.S0;
            box.style.visibility = Visibility.Hidden;
            // A window under the stage, so rules scoped to windows (.window .dim) resolve as they do on screen.
            var host = new VisualElement();
            host.AddToClassList("window");
            box.Add(host);
            var probes = new List<(string key, VisualElement e)>();
            void Add(string key, string classes, bool hover)
            {
                var e = new VisualElement { pickingMode = PickingMode.Ignore };
                foreach (var c in classes.Split(' ')) e.AddToClassList(c);
                if (hover) SetHover(e);
                host.Add(e);
                probes.Add((key, e));
            }
            foreach (var k in Kinds)
            {
                Add(k, k, false);
                Add(k + " " + Pressed, k + " " + Pressed, false);
                if (HoverSupported) Add(k + " " + Hover, k, true);
            }
            var dim = new Label("probe");
            dim.AddToClassList("dim");
            host.Add(dim);
            probes.Add(("dim", dim));
            stage.Add(box);
            // Styles resolve on the next panel update; read them after it.
            box.schedule.Execute(() =>
            {
                var table = new Dictionary<string, Swatch>();
                foreach (var (key, e) in probes)
                {
                    var r = e.resolvedStyle;
                    table[key] = new Swatch { Bg = r.backgroundColor, Top = r.borderTopColor, Left = r.borderLeftColor, Right = r.borderRightColor, Bottom = r.borderBottomColor, Text = r.color };
                }
                box.RemoveFromHierarchy();
                probed[style] = table;
                probing = false;
                Run(stage);
            }).ExecuteLater(50);
        }

        private static bool HoverSupported
        {
            get
            {
                if (pseudoStates != null) return hoverState != null;
                try
                {
                    pseudoStates = typeof(VisualElement).GetProperty("pseudoStates", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (pseudoStates != null && pseudoStates.PropertyType.IsEnum)
                        hoverState = Enum.Parse(pseudoStates.PropertyType, "Hover");
                }
                catch (Exception) { hoverState = null; }
                return hoverState != null;
            }
        }

        private static void SetHover(VisualElement e)
        {
            try { pseudoStates.SetValue(e, hoverState); } catch (Exception) { /* hover just isn't probed */ }
        }

        // ── Applying ────────────────────────────────────────────────────────────────

        /// <summary>The probe key for <paramref name="e"/> in its current state, or null when it isn't chrome.</summary>
        private static string KeyOf(VisualElement e, Dictionary<string, Swatch> table, out string baseKey)
        {
            baseKey = null;
            int best = 0;
            foreach (var k in Kinds)
            {
                var cs = k.Split(' ');
                if (cs.Length > best && cs.All(e.ClassListContains)) { best = cs.Length; baseKey = k; }
            }
            if (baseKey == null) return null;
            if (e.ClassListContains(Pressed) && table.ContainsKey(baseKey + " " + Pressed)) return baseKey + " " + Pressed;
            if (owned.TryGetValue(e, out var o) && o.Hooked && hovered.Contains(e) && table.ContainsKey(baseKey + " " + Hover)) return baseKey + " " + Hover;
            return baseKey;
        }

        private static readonly HashSet<VisualElement> hovered = new HashSet<VisualElement>();

        private static void Apply(VisualElement e, Dictionary<string, Swatch> table, WindowTone tone)
        {
            string key = KeyOf(e, table, out var baseKey);
            if (key == null || !table.TryGetValue(key, out var s)) return;
            // A window whose theme sets frame colours (or Classic's accent) is already tinted inline by Ui.TintWindow.
            bool window = baseKey == "window" || baseKey == "window window--plain";
            bool frameInline = window && WindowThemes.FrameSides(WindowThemes.Current, Art.ThemeAccent) != null;
            var o = owned.GetValue(e, _ => new Owned());
            if (o.Key == key && o.Style == StyleKey && o.Tone.Equals(tone) && Fresh(e, o)) return;
            if (!o.Bg && !o.Border && !o.Text) touched.Add(new WeakReference<VisualElement>(e));
            o.Key = key;
            o.Style = StyleKey;
            o.Tone = tone;
            if (frameInline) o.Border = false; // Ui.TintWindow owns this frame
            // A window's body is the baked gradient image (already tuned); a plain window's is a colour. Never overwrite a
            // colour a screen set inline itself.
            bool doBg = baseKey != "window";
            if (doBg && (o.Bg || e.style.backgroundColor.keyword == StyleKeyword.Null))
            {
                e.style.backgroundColor = WindowThemes.Map(tone, s.Bg);
                o.Bg = true;
            }
            if (!frameInline && (o.Border || e.style.borderTopColor.keyword == StyleKeyword.Null))
            {
                e.style.borderTopColor = WindowThemes.Map(tone, s.Top);
                e.style.borderLeftColor = WindowThemes.Map(tone, s.Left);
                e.style.borderRightColor = WindowThemes.Map(tone, s.Right);
                e.style.borderBottomColor = WindowThemes.Map(tone, s.Bottom);
                o.Border = true;
            }
            if (!o.Hooked)
            {
                o.Hooked = true;
                void Later() => e.schedule.Execute(() => Reapply(e));
                e.RegisterCallback<PointerDownEvent>(_ => Later(), TrickleDown.TrickleDown);
                e.RegisterCallback<PointerUpEvent>(_ => Later(), TrickleDown.TrickleDown);
                e.RegisterCallback<PointerCancelEvent>(_ => Later());
                e.RegisterCallback<PointerCaptureOutEvent>(_ => Later());
                e.RegisterCallback<PointerEnterEvent>(_ => { hovered.Add(e); Later(); });
                e.RegisterCallback<PointerLeaveEvent>(_ => { hovered.Remove(e); Later(); });
                e.RegisterCallback<DetachFromPanelEvent>(_ => hovered.Remove(e));
            }
        }

        /// <summary>Inline values still the ones this set (a TintWindow or screen rebuild clears them).</summary>
        private static bool Fresh(VisualElement e, Owned o) =>
            (!o.Bg || e.style.backgroundColor.keyword != StyleKeyword.Null) && (!o.Border || e.style.borderTopColor.keyword != StyleKeyword.Null);

        private static void Reapply(VisualElement e)
        {
            var tone = WindowThemes.Current.Tone;
            if (!owned.TryGetValue(e, out var o)) return;
            if (tone.IsNeutral || !probed.TryGetValue(StyleKey, out var table)) { Clear(e, o); return; }
            o.Key = null; // force
            Apply(e, table, tone);
        }

        // ── Dim captions ────────────────────────────────────────────────────────────

        /// <summary>The dim caption colour for the tuned theme: the stylesheet's own dim colour, held to 3:1 against the
        /// window's lightest tuned colour; null when it already clears it.</summary>
        private static Color? CaptionFloor(Dictionary<string, Swatch> table)
        {
            if (!table.TryGetValue("dim", out var d)) return null;
            var c = d.Text;
            var bg = WindowThemes.Lightest(WindowThemes.Current);
            var (r, g, b) = WindowTone.Readable((c.r, c.g, c.b), (bg.r, bg.g, bg.b), WindowTone.CaptionContrast);
            var k = c; (k.r, k.g, k.b) = (r, g, b);
            return k == c ? (Color?)null : k;
        }

        private static void ApplyDim(VisualElement e, Color? floor)
        {
            var o = owned.GetValue(e, _ => new Owned());
            if (!o.Text && e.style.color.keyword != StyleKeyword.Null) return; // a screen's own colour
            if (floor.HasValue)
            {
                if (!o.Text) touched.Add(new WeakReference<VisualElement>(e));
                e.style.color = floor.Value;
                o.Text = true;
            }
            else if (o.Text) { e.style.color = StyleKeyword.Null; o.Text = false; }
        }

        // ── Back to the stylesheet ──────────────────────────────────────────────────

        private static void Clear(VisualElement e, Owned o)
        {
            if (o.Bg) e.style.backgroundColor = StyleKeyword.Null;
            if (o.Border)
            {
                // A window whose (new) theme has its own frame colours keeps them (Ui.TintWindow's), else the stylesheet's.
                var sides = e.ClassListContains("window") ? WindowThemes.FrameSides(WindowThemes.Current, Art.ThemeAccent) : null;
                StyleColor C(int i) => sides == null ? new StyleColor(StyleKeyword.Null) : new StyleColor(sides[i]);
                e.style.borderTopColor = C(0); e.style.borderLeftColor = C(1); e.style.borderRightColor = C(2); e.style.borderBottomColor = C(3);
            }
            if (o.Text) e.style.color = StyleKeyword.Null;
            o.Bg = o.Border = o.Text = false;
            o.Key = null;
        }

        private static void ClearAll()
        {
            foreach (var w in touched)
                if (w.TryGetTarget(out var e) && owned.TryGetValue(e, out var o)) Clear(e, o);
            touched.Clear();
            // A window theme with its own frame colours gets them back from Ui.TintWindow on the next ApplyMenuTheme.
        }
    }
}
