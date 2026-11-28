using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>Tiny coroutine tweens on unscaled time, scaled by the battle-speed setting.</summary>
    public static class Tween
    {
        public static float Speed = 1f;
        /// <summary>
        /// Frame recorder (automation "record"): when &gt; 0 every animation advances exactly this much per rendered
        /// frame, however long the frame took, so a frame-by-frame capture shows every beat.
        /// </summary>
        public static float FixedStep;

        /// <summary>
        /// The game clock is stopped (the battle's pause menu, GG-T126). Every animation in the game advances by
        /// <see cref="Dt"/>, so while this is set each Tween.Run/Wait/Move, every hand-rolled Dt timer (coin bursts,
        /// cut-in streaks, the simulated drag, channel motes, the death dissolve) and <see cref="Clock"/>-driven pulse
        /// holds its exact position and carries on from there when it clears. The pause menu itself never tweens.
        /// </summary>
        public static bool Paused;

        /// <summary>Settings → Reduce motion (GG-T495), set by <c>GameApp.ApplySettings</c>: <see cref="Shake"/> holds still,
        /// <see cref="Pop"/> doesn't swell and <see cref="Move"/> takes at most <see cref="Timing.Quick"/>, everywhere —
        /// menus, celebrations and the battle alike (the battle's own effects already check the setting).</summary>
        public static bool Calm;

        /// <summary>Settings → Screen shake (GG-T1221), set by <c>GameApp.ApplySettings</c>: 0..1 scale on every
        /// <see cref="Shake"/>'s magnitude (1 = full, 0 = off).</summary>
        public static float ShakeScale = 1f;

        /// <summary>The pause menu's Instant turns cheat (GG-T1967), set by the battle's playback while it runs: every
        /// <see cref="Run"/> jumps straight to its end frame and every <see cref="Wait"/> returns at once.</summary>
        public static bool Instant;

        public static float Dt => Paused ? 0f : FixedStep > 0f ? FixedStep : Time.unscaledDeltaTime;

        /// <summary>Seconds of unpaused game time since launch, advanced once per frame by GameApp.Update — the
        /// pause-aware stand-in for Time.unscaledTime in looping ambient animations (pulses, bobs).</summary>
        public static float Clock { get; private set; }

        public static void Advance() => Clock += Dt;

        public static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
        public static float EaseInOut(float t) => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
        public static float Back(float t) { const float c1 = 1.70158f, c3 = c1 + 1f; return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f); }

        public static IEnumerator Run(float seconds, Action<float> step, Func<float, float> ease = null)
        {
            if (Instant) { step(1f); yield break; }
            float d = Mathf.Max(0.0001f, seconds / Mathf.Max(0.1f, Speed));
            float t = 0f;
            bool first = true;
            while (t < d)
            {
                // While paused the frame is held as-is: re-running a step at the same k would still re-roll anything
                // random in it (Shake's jitter) and visibly move a "frozen" frame (GG-T126).
                if (first || !Paused)
                {
                    float k = t / d;
                    step(ease != null ? ease(k) : k);
                }
                first = false;
                IdleFrameRate.Stir();
                yield return null;
                t += Dt;
            }
            step(1f);
        }

        public static IEnumerator Wait(float seconds)
        {
            if (Instant) yield break;
            float d = seconds / Mathf.Max(0.1f, Speed);
            float t = 0f;
            while (t < d) { yield return null; t += Dt; }
        }

        public static IEnumerator Move(VisualElement e, Vector2 from, Vector2 to, float seconds, Func<float, float> ease = null)
        {
            return Run(Calm ? Mathf.Min(seconds, Timing.Quick) : seconds, k =>
            {
                var p = Vector2.LerpUnclamped(from, to, k);
                e.style.left = p.x;
                e.style.top = p.y;
            }, Calm ? (Func<float, float>)EaseOut : ease ?? EaseOut); // no overshoot (Back) while calm
        }

        public static IEnumerator Fade(VisualElement e, float from, float to, float seconds)
        {
            return Run(seconds, k => e.style.opacity = Mathf.Lerp(from, to, k));
        }

        public static IEnumerator Shake(VisualElement e, float magnitude, float seconds)
        {
            magnitude = Calm ? 0f : magnitude * ShakeScale;
            return Run(seconds, k =>
            {
                float m = magnitude * (1f - k);
                e.style.translate = k >= 1f ? new Translate(0, 0) : new Translate(UnityEngine.Random.Range(-m, m), UnityEngine.Random.Range(-m, m));
            });
        }

        public static IEnumerator Pop(VisualElement e, float peak, float seconds)
        {
            if (Calm) peak = 1f;
            return Run(seconds, k =>
            {
                float s = 1f + (peak - 1f) * Ease.Arc(k);
                e.style.scale = new Scale(new Vector2(s, s));
            });
        }

        /// <summary>Runs several coroutines in parallel and waits for all.</summary>
        public static IEnumerator All(MonoBehaviour host, params IEnumerator[] routines)
        {
            int running = routines.Length;
            foreach (var r in routines) host.StartCoroutine(Track(r, () => running--));
            while (running > 0) yield return null;
        }

        private static IEnumerator Track(IEnumerator r, Action done)
        {
            yield return r;
            done();
        }
    }
}
