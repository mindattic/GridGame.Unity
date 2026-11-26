using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>
    /// GG-T1210: the gentle "handheld camera" drift of every framed actor thumbnail. Owner 2026-11-26: "make sure the
    /// portrait gets cropped 10% larger so the 'handheld camera wobble' can move every model a little without it being
    /// distracting." <see cref="Ui.PortraitThumb"/> draws the art with <see cref="Ui.ThumbMargin"/> spare on every side
    /// and calls <see cref="Track"/>; this slides the art (a px translate, no layout) by at most <see cref="Amplitude"/>
    /// of the visible side, well inside the margin, so no edge ever shows. Each thumb has its own random phase, so a grid
    /// doesn't sway in lockstep. One shared ticker drives every attached thumb (the hub and bestiary show 100+ at once):
    /// an element joins the list when it attaches to a panel and leaves when it detaches, and the ticker stops when the
    /// list is empty. Reduce Motion: every thumb rests at exactly the owner's framing.
    /// </summary>
    public static class ThumbWobble
    {
        /// <summary>Largest drift on each axis, as a fraction of the visible (host) side; below <see cref="Ui.ThumbMargin"/>.</summary>
        public const float Amplitude = 0.035f;
        /// <summary>Seconds per slow sway on each axis (two incommensurate sines each, so the path never visibly repeats).</summary>
        public const float PeriodX = 7.3f, PeriodY = 9.7f;

        private sealed class State
        {
            public float PhaseX, PhaseY;
            public bool On;
            public bool Listed;
        }

        private static readonly ConditionalWeakTable<VisualElement, State> states = new ConditionalWeakTable<VisualElement, State>();
        private static readonly List<VisualElement> live = new List<VisualElement>();
        private static bool running;
        private static int lastTickFrame = -1;
        private static bool calmApplied;

        /// <summary>Elements the ticker is currently moving (attached and tracked).</summary>
        public static int LiveCount => live.Count;

        private static bool ReduceMotion => GameApp.I?.Settings?.ReduceMotion == true;

        /// <summary>Lets <paramref name="art"/> drift (idempotent; a reused host keeps its phase).</summary>
        public static void Track(VisualElement art)
        {
            if (art == null) return;
            if (!states.TryGetValue(art, out var st))
            {
                st = new State { PhaseX = Random.value * Mathf.PI * 2f, PhaseY = Random.value * Mathf.PI * 2f };
                states.Add(art, st);
                art.RegisterCallback<AttachToPanelEvent>(OnAttach);
                art.RegisterCallback<DetachFromPanelEvent>(OnDetach);
            }
            st.On = true;
            if (art.panel != null) List(art, st);
        }

        /// <summary>Stops <paramref name="art"/> drifting and puts it back at rest (e.g. a host reused for unframed art).</summary>
        public static void Untrack(VisualElement art)
        {
            if (art == null || !states.TryGetValue(art, out var st)) return;
            st.On = false;
            Unlist(art, st);
            art.style.translate = StyleKeyword.Null;
        }

        private static void OnAttach(AttachToPanelEvent evt)
        {
            if (evt.target is VisualElement e && states.TryGetValue(e, out var st) && st.On) List(e, st);
        }

        private static void OnDetach(DetachFromPanelEvent evt)
        {
            if (evt.target is VisualElement e && states.TryGetValue(e, out var st)) Unlist(e, st);
        }

        private static void List(VisualElement e, State st)
        {
            if (!st.Listed) { st.Listed = true; live.Add(e); }
            // The ticker died with its host (a scene reload stops coroutines without finishing them): start it again.
            if (running && Time.frameCount - lastTickFrame > 2) running = false;
            if (!running && GameApp.I != null)
            {
                running = true;
                lastTickFrame = Time.frameCount;
                GameApp.I.StartCoroutine(Tick());
            }
        }

        private static void Unlist(VisualElement e, State st)
        {
            if (!st.Listed) return;
            st.Listed = false;
            live.Remove(e);
        }

        /// <summary>The drift of a thumb with these phases at <paramref name="t"/> seconds, in units of
        /// <see cref="Amplitude"/> (each axis within [-1, 1]).</summary>
        public static Vector2 Offset(float t, float phaseX, float phaseY)
        {
            const float tau = Mathf.PI * 2f;
            float x = 0.65f * Mathf.Sin(t * tau / PeriodX + phaseX) + 0.35f * Mathf.Sin(t * tau / (PeriodX * 0.41f) + phaseY * 1.7f);
            float y = 0.65f * Mathf.Sin(t * tau / PeriodY + phaseY) + 0.35f * Mathf.Sin(t * tau / (PeriodY * 0.37f) + phaseX * 1.3f);
            return new Vector2(x, y);
        }

        private static IEnumerator Tick()
        {
            while (live.Count > 0)
            {
                lastTickFrame = Time.frameCount;
                if (ReduceMotion)
                {
                    if (!calmApplied)
                    {
                        calmApplied = true;
                        foreach (var e in live) e.style.translate = StyleKeyword.Null;
                    }
                }
                else
                {
                    calmApplied = false;
                    float t = Time.unscaledTime;
                    for (int i = live.Count - 1; i >= 0; i--)
                    {
                        var e = live[i];
                        states.TryGetValue(e, out var st);
                        if (e.panel == null || st == null)
                        {
                            if (st != null) st.Listed = false;
                            live.RemoveAt(i);
                            continue;
                        }
                        // The visible box is the art's parent (the clip, inset 0 in the host).
                        var box = e.parent != null ? e.parent.layout : e.layout;
                        if (float.IsNaN(box.width) || box.width <= 0f) continue;
                        var o = Offset(t, st.PhaseX, st.PhaseY);
                        e.style.translate = new Translate(o.x * Amplitude * box.width, o.y * Amplitude * box.height);
                    }
                }
                yield return null;
            }
            running = false;
        }
    }
}
