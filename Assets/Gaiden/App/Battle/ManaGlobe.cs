using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>
    /// The mana pool's globe (GG-T405, owner 2026-11-17: "a glass sphere filled with blue liquid that gets filled ... like
    /// Diablo or PoE2"): an action-RPG resource globe drawn in code with the UI Toolkit vector API, no art files. A heavy
    /// dark-metal bezel with four rivets holds dark glass; deep blue liquid fills it to <see cref="Target"/> (count / cap),
    /// darker at the bottom and luminous toward a glowing meniscus whose surface sloshes gently; translucent streaks drift
    /// through it (the churn); a soft inner glow, a curved reflection arc and a glint sit on the glass. Filling eases the
    /// level up with a ripple and a pulse of light; draining drops it with a slosh. <see cref="Preview"/> lightens the band
    /// at the top of the liquid that an aimed spell will drain. Reduce Motion: a still, flat surface and no drift.
    /// </summary>
    public sealed class ManaGlobe : VisualElement
    {
        /// <summary>The level to show, 0..1 (count / cap).</summary>
        public float Target;
        /// <summary>The share of the globe (0..1 of the cap) an aimed spell will drain, drawn lighter at the surface.</summary>
        public float Preview;
        public bool ReduceMotion;
        /// <summary>The next orb forming (the mana charge, §3.1.9), 0..1: a bright arc that runs clockwise round the bezel.</summary>
        public float Charge;

        private float level = -1f, phase, swirl, ripple, pulse, preview;
        private const int Samples = 40;

        private static readonly Color BezelDark = new Color(0.10f, 0.105f, 0.13f);
        private static readonly Color BezelMid = new Color(0.27f, 0.28f, 0.33f);
        private static readonly Color BezelLight = new Color(0.62f, 0.64f, 0.70f);
        private static readonly Color Rivet = new Color(0.50f, 0.52f, 0.58f);
        private static readonly Color Glass = new Color(0.025f, 0.035f, 0.08f, 0.96f);
        private static readonly Color Deep = new Color(0.02f, 0.07f, 0.30f);
        private static readonly Color Bright = new Color(0.16f, 0.45f, 1.00f);
        private static readonly Color Meniscus = new Color(0.62f, 0.90f, 1.00f);

        public ManaGlobe()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
            schedule.Execute(Tick).Every(16);
        }

        /// <summary>An orb landed: a ripple across the surface and a pulse of light.</summary>
        public void Fill() { ripple = 1f; pulse = 1f; }

        /// <summary>Orbs left: the surface drops with a slosh.</summary>
        public void Drain() { ripple = Mathf.Max(ripple, 0.8f); }

        private void Tick()
        {
            float dt = Tween.Dt;
            if (level < 0f || ReduceMotion) level = Target;
            else level += (Target - level) * Mathf.Min(1f, dt * 5f);
            preview += (Preview - preview) * (ReduceMotion ? 1f : Mathf.Min(1f, dt * 10f));
            if (!ReduceMotion) { phase += dt * 2.4f; swirl += dt * 0.35f; }
            ripple *= Mathf.Exp(-dt * 2.2f);
            pulse *= Mathf.Exp(-dt * 3.5f);
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext ctx)
        {
            var rect = contentRect;
            float r = Mathf.Min(rect.width, rect.height) / 2f;
            if (r < 4f || float.IsNaN(r)) return;
            var c = rect.center;
            float g = r * 0.80f; // the glass inside the bezel
            var p = ctx.painter2D;

            // Bezel: a dark metal ring, a lit outer lip, a shadowed inner lip and four rivets.
            Disc(p, c, r, BezelDark);
            Ring(p, c, r * 0.91f, r * 0.12f, BezelMid);
            Ring(p, c, r - 1.5f, 2.5f, BezelLight.WithA(0.75f));
            for (int i = 0; i < 4; i++)
            {
                float a = Mathf.PI / 4f + i * Mathf.PI / 2f;
                var at = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r * 0.90f;
                Disc(p, at, r * 0.055f, Rivet);
                Disc(p, at + new Vector2(-0.3f, -0.3f) * r * 0.03f, r * 0.022f, BezelLight);
            }

            // The mana charge: the next orb forming, as an arc round the bezel from the top.
            Ring(p, c, r * 0.91f, r * 0.07f, new Color(0f, 0f, 0f, 0.45f));
            if (Charge > 0.005f)
            {
                p.lineCap = LineCap.Round;
                p.strokeColor = new Color(0.40f, 0.75f, 1f, 0.95f);
                p.lineWidth = r * 0.07f;
                p.BeginPath();
                p.Arc(c, r * 0.91f, Angle.Degrees(-90f), Angle.Degrees(-90f + 360f * Mathf.Clamp01(Charge)));
                p.Stroke();
            }

            // Dark glass.
            Disc(p, c, g, Glass);

            // Liquid: slices from the deep bottom to the luminous top; only the top one carries the wave.
            float lv = Mathf.Clamp01(level < 0f ? Target : level);
            float amp = ReduceMotion || lv <= 0.001f ? 0f : g * (0.035f + 0.09f * ripple) * Mathf.Sin(Mathf.PI * Mathf.Clamp01(lv * 0.98f + 0.01f));
            float baseY = c.y + g - 2f * g * lv;
            float Surface(float x) => baseY + amp * Mathf.Sin(x / g * 5.2f + phase) + amp * 0.45f * Mathf.Sin(x / g * 9.1f - phase * 1.3f);
            float Bottom(float x) => c.y + g;
            if (lv > 0.001f)
            {
                const int Slices = 6;
                float depth = c.y + g - baseY;
                for (int s = 0; s < Slices; s++)
                {
                    float k0 = s / (float)Slices, k1 = (s + 1) / (float)Slices; // 0 = surface, 1 = bottom
                    var col = Color.Lerp(Bright, Deep, Mathf.Pow((k0 + k1) / 2f, 0.8f));
                    float y0 = baseY + depth * k0, y1 = baseY + depth * k1;
                    if (s == 0) Region(p, c, g, Surface, x => y1 + 1f, col);
                    else Region(p, c, g, x => y0, x => s == Slices - 1 ? c.y + g : y1 + 1f, col);
                }
                // The churn: translucent streaks drifting slowly through the liquid.
                for (int i = 0; i < 3; i++)
                {
                    float h = baseY + depth * (0.25f + 0.25f * i);
                    float sp = swirl * (1f + 0.4f * i) + i * 2.1f;
                    float w = g * (0.06f + 0.02f * i);
                    Region(p, c, g,
                        x => Mathf.Max(Surface(x), h + g * 0.10f * Mathf.Sin(x / g * (2.4f + i) + sp)),
                        x => Mathf.Max(Surface(x), h + w + g * 0.10f * Mathf.Sin(x / g * (2.4f + i) + sp + 0.6f)),
                        new Color(0.45f, 0.70f, 1f, 0.13f));
                }
                // The pending cost: a lighter band at the top that will drain.
                if (preview > 0.002f)
                {
                    float drop = 2f * g * Mathf.Min(preview, lv);
                    Region(p, c, g, Surface, x => Surface(x) + drop, new Color(0.70f, 0.88f, 1f, 0.45f));
                }
                // The glowing meniscus: a wide soft glow under a crisp bright line.
                Meniscus_(p, c, g, Surface, Meniscus.WithA(0.25f), g * 0.10f);
                Meniscus_(p, c, g, Surface, Meniscus, Mathf.Max(2f, g * 0.035f));
            }

            // Soft inner glow, plus a pulse of light when an orb lands.
            Ring(p, c, g - g * 0.05f, g * 0.10f, new Color(0.30f, 0.55f, 1f, 0.16f + 0.35f * pulse));
            if (pulse > 0.02f) Disc(p, c, g, new Color(0.45f, 0.75f, 1f, 0.28f * pulse));

            // Glass: the curved reflection arc and a glint, and the shadowed inner lip of the bezel.
            p.lineCap = LineCap.Round;
            p.strokeColor = new Color(1f, 1f, 1f, 0.40f);
            p.lineWidth = g * 0.09f;
            p.BeginPath();
            p.Arc(c, g * 0.78f, Angle.Degrees(198f), Angle.Degrees(252f));
            p.Stroke();
            p.strokeColor = new Color(1f, 1f, 1f, 0.12f);
            p.lineWidth = g * 0.05f;
            p.BeginPath();
            p.Arc(c, g * 0.80f, Angle.Degrees(20f), Angle.Degrees(60f));
            p.Stroke();
            Disc(p, c + new Vector2(-0.30f, -0.58f) * g, g * 0.075f, new Color(1f, 1f, 1f, 0.85f));
            Disc(p, c + new Vector2(-0.14f, -0.66f) * g, g * 0.032f, new Color(1f, 1f, 1f, 0.7f));
            Ring(p, c, g + 1f, Mathf.Max(2f, r * 0.03f), new Color(0f, 0f, 0f, 0.8f));
        }

        private static void Disc(Painter2D p, Vector2 c, float radius, Color col)
        {
            p.fillColor = col;
            p.BeginPath();
            p.Arc(c, radius, Angle.Degrees(0f), Angle.Degrees(360f));
            p.ClosePath();
            p.Fill();
        }

        private static void Ring(Painter2D p, Vector2 c, float radius, float width, Color col)
        {
            p.strokeColor = col;
            p.lineWidth = width;
            p.BeginPath();
            p.Arc(c, radius, Angle.Degrees(0f), Angle.Degrees(360f));
            p.ClosePath();
            p.Stroke();
        }

        /// <summary>Fills the part of the glass disc between two curves (top above bottom), clipped to the disc.</summary>
        private static void Region(Painter2D p, Vector2 c, float g, System.Func<float, float> top, System.Func<float, float> bottom, Color col)
        {
            var tops = new Vector2[Samples + 1];
            var bots = new Vector2[Samples + 1];
            bool any = false;
            for (int i = 0; i <= Samples; i++)
            {
                float x = -g + 2f * g * i / Samples;
                float h = Mathf.Sqrt(Mathf.Max(0f, g * g - x * x));
                float t = Mathf.Clamp(top(x), c.y - h, c.y + h);
                float b = Mathf.Clamp(bottom(x), c.y - h, c.y + h);
                if (b < t) b = t;
                if (b - t > 0.3f) any = true;
                tops[i] = new Vector2(c.x + x, t);
                bots[i] = new Vector2(c.x + x, b);
            }
            if (!any) return;
            p.fillColor = col;
            p.BeginPath();
            p.MoveTo(tops[0]);
            for (int i = 1; i <= Samples; i++) p.LineTo(tops[i]);
            for (int i = Samples; i >= 0; i--) p.LineTo(bots[i]);
            p.ClosePath();
            p.Fill();
        }

        /// <summary>Strokes the surface curve where it lies inside the glass.</summary>
        private static void Meniscus_(Painter2D p, Vector2 c, float g, System.Func<float, float> surface, Color col, float width)
        {
            p.strokeColor = col;
            p.lineWidth = width;
            p.lineCap = LineCap.Round;
            p.BeginPath();
            bool open = false;
            for (int i = 0; i <= Samples; i++)
            {
                float x = -g + 2f * g * i / Samples;
                float h = Mathf.Sqrt(Mathf.Max(0f, g * g - x * x)) - width / 2f;
                float y = surface(x);
                bool inside = h > 0f && y > c.y - h && y < c.y + h;
                var pt = new Vector2(c.x + x, y);
                if (inside) { if (open) p.LineTo(pt); else { p.MoveTo(pt); open = true; } }
                else open = false;
            }
            p.Stroke();
        }
    }
}
