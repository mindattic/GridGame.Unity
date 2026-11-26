using System.Runtime.CompilerServices;
using GridGame.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>
    /// GG-T305 (owner: "perfect text wrapping on all things (hyphenated or word wrap as an option), and one-line things
    /// like buttons have a restarting marquee"). One pass over every TextElement on the panel, a few times a second:
    /// <list type="bullet">
    /// <item>Wrapped text (white-space: normal) is laid out by <see cref="LineBreaker"/> against its real width and font:
    /// no lone word on the last line, no single letter ending a line, an overlong word hyphenated instead of cut
    /// mid-glyph, and with Settings → Text wrapping → Hyphenate a word that would leave a ragged line breaks at a
    /// <see cref="Hyphenation"/> point. When the engine's own greedy wrap is already clean the text is left untouched;
    /// otherwise the label gets explicit line breaks, re-laid when its width, font size, the setting or its text changes
    /// (a text must hold still for one pass first, so a typewriter is never re-flowed mid-line).</item>
    /// <item>Single-line text (white-space: nowrap) that is wider than its box becomes a <see cref="Marquee"/>.</item>
    /// <item>A word in a GlossaryText row (GG-T300) wider than the row becomes a wrapped label, so it hyphenates too.</item>
    /// </list>
    /// </summary>
    public static class TextFit
    {
        /// <summary>Pass interval, ms.</summary>
        public const long Interval = 120;
        /// <summary>Lines are fitted this much narrower than the box, so the engine never re-wraps one of ours by a hair.</summary>
        private const float Slack = 2f;

        public static bool Hyphenate { get; private set; }
        private static int generation;

        private sealed class WrapState
        {
            public string Source, Output, Seen;
            public float Width, MaxLine, FontSize, ParentWidth;
            public int Generation = -1;
            public bool Pinned, ParentKnown;
            public StyleLength PrevMinWidth;
        }

        /// <summary>
        /// Our lines are measured to fit; but a label sized to its own text would then be exactly as wide as its longest
        /// line, and the engine re-wraps a line at exactly its own width by a rounding hair. So a re-laid label keeps a
        /// minimum width of its longest line plus <see cref="Slack"/> (never more than the width it was laid out in).
        /// </summary>
        private static void Unpin(TextElement t, WrapState st)
        {
            if (!st.Pinned) return;
            t.style.minWidth = st.PrevMinWidth;
            st.Pinned = false;
        }

        private static readonly ConditionalWeakTable<TextElement, WrapState> wraps = new ConditionalWeakTable<TextElement, WrapState>();

        public static void Install(VisualElement root)
        {
            if (root == null) return;
            root.schedule.Execute(() => Pass(root)).Every(Interval);
        }

        /// <summary>Applies the Text wrapping setting live (and a font change): every wrapped label is re-laid on the next pass.</summary>
        public static void Configure(bool hyphenate)
        {
            Hyphenate = hyphenate;
            generation++;
        }

        /// <summary>The words an element was given (before line breaks or a marquee's hiding tag), for automation and audits.</summary>
        public static string SourceOf(TextElement e)
        {
            if (e == null) return null;
            if (wraps.TryGetValue(e, out var st) && st.Output != null && e.text == st.Output) return st.Source;
            return Marquee.SourceOf(e);
        }

        public static void Pass(VisualElement root)
        {
            // GG-T711: a new screen shape moves the shrink floor; every shrunk label is fitted again.
            if (Mathf.Abs(TypeFloor.ShrinkFloor - lastFloor) > 0.01f) { lastFloor = TypeFloor.ShrinkFloor; generation++; }
            root.Query<TextElement>().ForEach(Fit);
        }

        private static void Fit(TextElement t)
        {
            if (t.panel == null) return;
            var p = t.parent;
            if (p == null || p.ClassListContains(Marquee.BoxClass) || t.ClassListContains("fit-off") || p is TextField || p.parent is TextField) return;
            var rs = t.resolvedStyle;
            if (rs.display == DisplayStyle.None) return;
            float w = t.contentRect.width;
            if (float.IsNaN(w) || w <= 1f) return;
            bool wrapped = rs.whiteSpace == WhiteSpace.Normal;
            // GG-T711: shrink to fit first; wrap (or, on one line, marquee / ellipsis) only below the floor.
            var glossary = GlossaryBox(t);
            if (glossary == null && Shrink(t, w, rs.fontSize, wrapped, out bool settled))
            {
                if (settled && !wrapped) Marquee.Check(t, w); // drops an overlay left from before the shrink
                return;
            }
            if (wrapped) Wrap(t, w, rs.fontSize);
            else if (t.ClassListContains(EllipsisClass)) { } // GG-T728: shrunk to the floor, then the engine's own ellipsis (no scrolling frame to catch mid-crawl)
            else if (!GlossaryWord(t, glossary)) Marquee.Check(t, w);
        }

        /// <summary>Opt-in class for one-line text that shrinks to the floor and then ends in an ellipsis rather than scrolling.</summary>
        public const string EllipsisClass = "ellipsis-only";

        // ---------------------------------------------------------------------------------------------------------
        // GG-T711 (owner 2026-11-23: "the sheer quantity of devices is mind boggling, the scaling of text to fit boxes is
        // important it should always try scaling text to fit content before it gives up and wraps"). Shrink to fit:
        // a text whose words don't fit its box on one line takes a smaller font (inline style.fontSize), down to
        // TypeFloor.ShrinkFloor; only when even the floor is too wide does it go back to its own size and wrap (wrapped
        // text) or marquee / ellipsize (one-line text). Paragraphs never shrink (see IsParagraph).
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Opt-out class: text that must wrap at its own size by design (lore, dialogue, a long description).</summary>
        public const string NoFitClass = "no-fit";
        /// <summary>Classes that are paragraph text by role (Ui.Para / Ui.Note / Ui.Empty) and keep wrapping.</summary>
        private static readonly string[] ParagraphClasses = { NoFitClass, "kit-text", "kit-note", "kit-empty" };
        /// <summary>Wrapped text of more words than this is a sentence: it wraps at its own size instead of shrinking.</summary>
        public const int ParagraphWords = 6;
        /// <summary>Font sizes are taken in steps of this many design px (rounded down, so the result always fits).</summary>
        private const float ShrinkStep = 0.5f;
        /// <summary>Most corrective steps after the first estimate, and most re-fits of one text, before it gives up.</summary>
        private const int MaxSteps = 6, MaxRefits = 12;

        private sealed class ShrinkState
        {
            public string Source;              // the words fitted
            public float Natural, Applied;     // the element's own size and the shrunk one
            public float ParentWidth;
            public bool ParentKnown, Active, Off, CapDriven; // CapDriven: shown at a peer's smaller size, not its own need
            public StyleLength PrevInline;     // the inline font size code had set, restored on release
            public int Generation, Steps, Refits;
            public string GaveUpSource;        // a text that doesn't fit even at the floor, for this width
            public float GaveUpWidth, GaveUpFloor;
        }

        private static readonly ConditionalWeakTable<TextElement, ShrinkState> shrinks = new ConditionalWeakTable<TextElement, ShrinkState>();
        private static float lastFloor = -1f;

        /// <summary>True when <paramref name="t"/> is paragraph text that wraps at its own size rather than shrinking.</summary>
        public static bool IsParagraph(TextElement t, string source)
        {
            foreach (var c in ParagraphClasses) if (t.ClassListContains(c)) return true;
            if (string.IsNullOrEmpty(source)) return false;
            int words = 0;
            bool inWord = false, inTag = false;
            foreach (char ch in source)
            {
                if (ch == '<') { inTag = true; continue; }
                if (inTag) { if (ch == '>') inTag = false; continue; }
                bool space = char.IsWhiteSpace(ch);
                if (!space && !inWord) words++;
                inWord = !space;
            }
            return words > ParagraphWords;
        }

        /// <summary>The font size, in design px, <paramref name="t"/> shows now (its shrunk size, or its own).</summary>
        public static float ShrunkSize(TextElement t) =>
            t != null && shrinks.TryGetValue(t, out var st) && st.Active ? st.Applied : float.NaN;

        private static void Release(TextElement t, ShrinkState st)
        {
            if (!st.Active) return;
            t.style.fontSize = st.PrevInline;
            st.Active = false;
        }

        /// <summary>
        /// One shrink-to-fit pass. Returns true when the element is (or is about to be) shown shrunk to one line, so the
        /// wrap / marquee fallbacks must leave it alone this pass; <paramref name="settled"/> is true once the layout
        /// shows the shrunk size. Returns false to fall through to wrapping / the marquee at the element's own size.
        /// </summary>
        private static bool Shrink(TextElement t, float w, float fontSize, bool wrapped, out bool settled)
        {
            settled = false;
            var st = shrinks.GetValue(t, _ => new ShrinkState());
            if (st.Off || t.ClassListContains(NoFitClass)) { Release(t, st); return false; }
            string source = SourceOf(t) ?? "";
            float pw = t.parent.contentRect.width;
            float floor = TypeFloor.ShrinkFloor;

            if (st.Active)
            {
                var inline = t.style.fontSize;
                if (inline.keyword != StyleKeyword.Undefined || Mathf.Abs(inline.value.value - st.Applied) > 0.01f)
                {
                    // Code sized this text itself while it was shrunk (a tween, a counter): it owns the size from now on.
                    st.Active = false;
                    st.Off = true;
                    return false;
                }
                bool caughtUp = Mathf.Abs(fontSize - st.Applied) < 0.1f;
                // The parent's width is read once the shrunk size has laid out, so a parent sized by this label (which the
                // shrink just narrowed) is the baseline rather than a change that would undo the shrink again.
                if (caughtUp && !st.ParentKnown) { st.ParentWidth = pw; st.ParentKnown = true; }
                float cap = PeerCap(t);
                bool stale = source != st.Source || st.Generation != generation
                             || (st.ParentKnown && Mathf.Abs(pw - st.ParentWidth) > 1f)
                             || (!float.IsNaN(cap) && cap < st.Applied - 0.01f)
                             || (st.CapDriven && (float.IsNaN(cap) || cap > st.Applied + 0.01f));
                if (stale)
                {
                    // New words, a new box, or a new type size: back to its own size, and fit again on the next pass
                    // (its natural size has to lay out before it can be measured).
                    Release(t, st);
                    if (++st.Refits > MaxRefits) st.Off = true;
                    return true;
                }
                if (!caughtUp) return true;
                if (Measure(t, source) <= w + 1f) { settled = true; return true; }
                // The linear estimate was a hair off (kerning, rounding): one more step down, a bounded number of times.
                float next = Mathf.Floor((st.Applied - ShrinkStep) / ShrinkStep) * ShrinkStep;
                if (st.Steps < MaxSteps && next >= floor)
                {
                    st.Steps++;
                    st.Applied = next;
                    t.style.fontSize = next;
                    st.ParentKnown = false;
                    return true;
                }
                // Even the floor is too wide: give up and fall back at its own size.
                GiveUp(st, source, w, floor);
                Release(t, st);
                return true;
            }

            if (source.Length == 0) return false;
            if (wrapped && IsParagraph(t, source)) return false;
            if (st.GaveUpSource == source && Mathf.Abs(st.GaveUpWidth - w) <= 1f && Mathf.Abs(st.GaveUpFloor - floor) < 0.01f) return false;
            if (fontSize <= floor + 0.01f) return false;
            float need = Measure(t, source);
            float peer = PeerCap(t);
            bool fits = need <= w + 0.5f || need <= 0f; // fits at its own size
            if (fits && (float.IsNaN(peer) || peer >= fontSize - 0.01f)) return false;
            // Text width scales with its font size, so one measurement gives the size that fits (less a hair of slack).
            float target = fits ? fontSize : Mathf.Floor(fontSize * (w - Slack) / need / ShrinkStep) * ShrinkStep;
            if (target < floor) { GiveUp(st, source, w, floor); return false; }
            // GG-T1157: rows of one list share one size, so a list never reads ragged: a peer already shrunk smaller sets it.
            bool capDriven = !float.IsNaN(peer) && peer < target - 0.01f;
            if (capDriven) target = peer;

            UndoWrap(t);
            st.PrevInline = t.style.fontSize;
            st.Natural = fontSize;
            st.Applied = target;
            st.Source = source;
            st.Generation = generation;
            st.Steps = 0;
            st.ParentKnown = false;
            st.Active = true;
            st.CapDriven = capDriven;
            st.GaveUpSource = null;
            t.style.fontSize = target;
            return true;
        }

        /// <summary>
        /// GG-T1157: the smallest size a shrunk peer of <paramref name="t"/> shows, or NaN. Peers are the same-role labels
        /// (same first class) in the siblings of <c>t</c>'s parent: the label of each menu row in a list, the name over each
        /// column of a row of cards. Each shrinks to its own need, but a list whose rows shrink by different amounts reads
        /// ragged, so every one of them takes the smallest.
        /// </summary>
        private static float PeerCap(TextElement t)
        {
            var row = t.parent;
            var list = row?.parent;
            if (list == null || list.childCount > 80) return float.NaN;
            string role = null;
            foreach (var c in t.GetClasses()) { role = c; break; }
            if (role == null) return float.NaN;
            float cap = float.NaN;
            foreach (var sib in list.Children())
            {
                if (sib == row) continue;
                foreach (var c in sib.Children())
                {
                    if (!(c is TextElement te) || te == t || !te.ClassListContains(role)) continue;
                    float s = ShrunkSize(te);
                    if (!float.IsNaN(s) && (float.IsNaN(cap) || s < cap)) cap = s;
                }
            }
            return cap;
        }

        private static void GiveUp(ShrinkState st, string source, float w, float floor)
        {
            st.GaveUpSource = source;
            st.GaveUpWidth = w;
            st.GaveUpFloor = floor;
        }

        /// <summary>Takes back GG-T305's explicit line breaks and width pin from a label that is about to shrink instead.</summary>
        private static void UndoWrap(TextElement t)
        {
            if (!wraps.TryGetValue(t, out var ws)) return;
            Unpin(t, ws);
            if (ws.Output != null && t.text == ws.Output && ws.Source != null && ws.Output != ws.Source)
                ((INotifyValueChanged<string>)t).SetValueWithoutNotify(ws.Source);
            wraps.Remove(t);
        }

        /// <summary>The GlossaryText row <paramref name="t"/> is a word of, or null.</summary>
        private static VisualElement GlossaryBox(TextElement t)
        {
            var box = t.parent;
            if (box != null && !box.ClassListContains(GlossaryText.UssClass)) box = box.parent;
            return box != null && box.ClassListContains(GlossaryText.UssClass) ? box : null;
        }

        /// <summary>A GlossaryText word wider than its whole row: let it wrap (and hyphenate) like any paragraph.</summary>
        private static bool GlossaryWord(TextElement t, VisualElement box)
        {
            if (box == null) return false;
            float row = box.contentRect.width;
            if (!float.IsNaN(row) && row > 1f && t.layout.width > row + 1f)
            {
                t.style.whiteSpace = WhiteSpace.Normal;
                t.style.flexShrink = 1f;
                t.style.maxWidth = Length.Percent(100);
            }
            return true;
        }

        private static float Measure(TextElement t, string s) =>
            t.MeasureTextSize(s ?? "", 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined).x;

        private static void Wrap(TextElement t, float w, float fontSize)
        {
            var st = wraps.GetValue(t, _ => new WrapState());
            string text = t.text ?? "";
            if (st.Output == null || text != st.Output)
            {
                // New text (first sight, or set from code): wait until it holds still for one pass.
                if (text != st.Seen) { st.Seen = text; return; }
                st.Source = text;
                st.Output = null;
            }
            float pw = t.parent.contentRect.width;
            // The parent's width is read one pass after a re-lay, so a parent sized by this label (which our pin just
            // narrowed) is the baseline rather than a change that would re-lay it again.
            if (st.Output != null && !st.ParentKnown) { st.ParentWidth = pw; st.ParentKnown = true; }
            bool stale = st.Output == null || st.Generation != generation || Mathf.Abs(st.FontSize - fontSize) > 0.1f
                         || w < st.MaxLine - 0.5f || w > st.Width + 1f || Mathf.Abs(pw - st.ParentWidth) > 1f;
            if (!stale) return;
            if (st.Pinned)
            {
                // Let it find its natural width again first; re-lay on the next pass.
                Unpin(t, st);
                st.Generation = -1;
                st.ParentKnown = true;
                st.ParentWidth = pw;
                return;
            }

            string output = st.Source;
            float maxLine = w; // untouched text: any narrowing re-checks it
            if (st.Source.Length > 0 && (st.Source.IndexOf(' ') >= 0 || Measure(t, st.Source) > w))
            {
                var r = LineBreaker.Break(st.Source, w - Slack, s => Measure(t, s), Hyphenate);
                if (r.Changed)
                {
                    output = string.Join("\n", r.Lines);
                    maxLine = 0f;
                    foreach (var line in r.Lines) maxLine = Mathf.Max(maxLine, Measure(t, line));
                }
            }
            st.Width = w;
            st.FontSize = fontSize;
            st.Generation = generation;
            st.MaxLine = maxLine;
            st.Output = output;
            st.Seen = output;
            st.ParentKnown = false;
            if (!ReferenceEquals(output, st.Source))
            {
                st.PrevMinWidth = t.style.minWidth;
                st.Pinned = true;
                t.style.minWidth = Mathf.Min(w, Mathf.Ceil(maxLine) + Slack);
            }
            if (t.text != output) ((INotifyValueChanged<string>)t).SetValueWithoutNotify(output);
        }
    }
}
