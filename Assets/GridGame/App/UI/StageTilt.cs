using GridGame.Core.Content;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GridGame.App
{
    /// <summary>
    /// GG-T682: how far the device is tilted away from however it is being held, for the layered stage's parallax
    /// (<see cref="LivingBackdrop"/>). Gravity from the accelerometer is low-passed (TiltSmoothSeconds) so the stage glides,
    /// and a much slower copy of it (TiltRecenterSeconds) is the held pose, so a phone lying flat, held upright or tipped in
    /// bed all settle back to the centre. The result is clamped to a unit circle at TiltDegrees. With no accelerometer (the
    /// editor, desktop) it is zero and the stage keeps only its idle sway. Read once per frame, shared by every backdrop.
    /// </summary>
    public static class StageTilt
    {
        /// <summary>Dev builds' automation can stand in for the accelerometer (gravity in device axes, g units); null =
        /// the real sensor.</summary>
        public static Vector3? Simulated;

        /// <summary>The last value <see cref="Read"/> returned (for the automation endpoint).</summary>
        public static Vector2 Last { get; private set; }

        private static int frame = -1;
        private static bool has;
        private static Vector3 smooth, rest;

        /// <summary>The tilt this frame: x toward the device's right edge, y toward its top edge, each -1..1.</summary>
        public static Vector2 Read(ParallaxDef p)
        {
            if (frame == Time.frameCount) return Last;
            frame = Time.frameCount;
            if (!Sample(out var g)) { has = false; return Last = Vector2.zero; }
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            if (!has) { smooth = rest = g; has = true; }
            smooth = Vector3.Lerp(smooth, g, 1f - Mathf.Exp(-dt / Mathf.Max(0.01f, p.TiltSmoothSeconds)));
            rest = Vector3.Lerp(rest, smooth, 1f - Mathf.Exp(-dt / Mathf.Max(0.01f, p.TiltRecenterSeconds)));
            var d = smooth - rest;
            var tilt = new Vector2(d.x, d.y) / Mathf.Sin(Mathf.Clamp(p.TiltDegrees, 1f, 60f) * Mathf.Deg2Rad);
            return Last = Vector2.ClampMagnitude(tilt, 1f);
        }

        private static bool Sample(out Vector3 g)
        {
            g = default;
            if (Simulated.HasValue) g = Simulated.Value;
            else
            {
                var acc = Accelerometer.current;
                if (acc == null) return false;
                if (!acc.enabled) InputSystem.EnableDevice(acc);
                g = acc.acceleration.ReadValue();
            }
            float m = g.magnitude;
            if (m < 0.3f) return false; // free fall or a sensor that hasn't reported yet: no usable gravity
            g /= m;
            return true;
        }
    }
}
