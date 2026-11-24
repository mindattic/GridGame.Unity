using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>
    /// One mana orb in the battle's orb row (GG-T673, owner 2026-11-22: "return the mana orbs line, doing things fills mana
    /// orbs which glow with bloom once they are active"). Drawn in code as a Manapunk glass bulb (GG-T688, BIBLE §11.5).
    /// GG-T730 (owner 2026-11-23: "empty mana orbs are like empty glass spheres; the mana dust effect that sucks in mana
    /// should fill the glass orb with glowing bloom fluid, then it should pulse quietly and breathe when it's full so you
    /// know you have a new ready to use mana orb"): an empty orb is clear glass (rim light, a specular highlight, a bounce
    /// reflection, the row's backdrop showing through); the orb forming next fills from the bottom with glowing mana fluid
    /// as <see cref="Charge"/> accrues (the charge motes fly into it), its surface swaying gently; a full orb's core breathes
    /// in step with its bloom (BattleScreen.BreatheOrbs). <see cref="Preview"/> marks an orb an aimed spell would spend (a
    /// pale pulsing rim); <see cref="Short"/> a red rim. Reduce Motion: no sway, no breathing, no flash, just the state.
    /// </summary>
    public sealed class ManaOrbView : VisualElement
    {
        public bool Filled;
        /// <summary>The next orb forming (the mana charge, §3.1.9), 0..1, drawn as the fluid level in its glass.</summary>
        public float Charge;
        public bool Preview;
        /// <summary>An empty orb an aimed spell still needs: a red rim, so "can't afford it" reads from the row.</summary>
        public bool Short;
        public bool ReduceMotion;

        /// <summary>The fluid level drawn (0..1), easing toward Filled ? 1 : Charge.</summary>
        private float level = -1f;
        private float flash, phase, lastCharge, hold;
        private bool wasFilled;

        public ManaOrbView()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
            schedule.Execute(Tick).Every(16);
        }

        /// <summary>An orb landed here: a bright flash that settles into the lit orb.</summary>
        public void Flash() { if (!ReduceMotion) flash = 1f; }

        private void Tick()
        {
            float dt = Tween.Dt;
            // A charge that rolls over into an orb drops to 0 here a beat before the mint lands and fills it (BattleScreen
            // CutIns then OrbAnim): hold the brimming glass through that gap instead of draining it and refilling.
            if (!Filled && !wasFilled && lastCharge >= 0.95f && Charge < lastCharge - 0.5f) hold = 1.2f;
            if (Filled || Charge > 0.005f) hold = 0f;
            hold = Mathf.Max(0f, hold - dt);
            lastCharge = Charge;
            wasFilled = Filled;

            float target = Filled ? 1f : hold > 0f ? Mathf.Max(level, 0f) : Mathf.Clamp01(Charge);
            if (level < 0f || ReduceMotion) level = target;
            else level += (target - level) * Mathf.Min(1f, dt * (target > level ? 6f : 9f));
            flash *= Mathf.Exp(-dt * 4f);
            if (!ReduceMotion) phase += dt * 3f;
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext ctx)
        {
            var rect = contentRect;
            float r = Mathf.Min(rect.width, rect.height) / 2f;
            if (r < 3f || float.IsNaN(r)) return;
            var c = rect.center;
            var p = ctx.painter2D;

            float collar = Mathf.Max(2f, r * 0.12f);
            float glass = r - collar;
            float full = Mathf.Clamp01((level - 0.98f) / 0.02f); // 1 once the glass is brimming
            // A full orb breathes, in step with its bloom behind it; static under Reduce Motion.
            float breathe = ReduceMotion ? 1f : 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / Timing.OrbBreathe);

            // Pewter collar, then clear glass: the backdrop shows through a faint tint, so an empty orb reads as a hollow
            // sphere rather than a dark bead.
            Disc(p, c, r, Palette.OrbCollar);
            Disc(p, c, glass, Palette.OrbGlass.WithA(0.55f));
            // The far wall's shading: a darker crescent up top, light gathering at the bottom like curved glass.
            Disc(p, c + new Vector2(0f, -0.18f) * glass, glass * 0.78f, Palette.OrbGlass.WithA(0.35f));

            if (level > 0.01f)
            {
                float fg = glass * 0.96f;
                // The surface sways a few degrees while the fluid is settling (not when brimming, not under Reduce Motion).
                float sway = ReduceMotion ? 0f : 4f * (1f - full) * Mathf.Sin(phase * 1.3f);
                var fluid = Color.Lerp(Palette.OrbLit, Palette.OrbLitHighlight, 0.15f * breathe * full);
                Segment(p, c, fg, level, sway, fluid);
                // A brighter core in the same fluid: the glowing heart of the mana.
                var core = Color.Lerp(Palette.OrbLit, Palette.OrbLitHighlight, 0.55f + 0.25f * full * breathe);
                float coreR = fg * (0.62f + 0.06f * full * breathe);
                float surfaceY = c.y + fg - 2f * fg * level;
                if (surfaceY < c.y + coreR) Segment(p, c + new Vector2(0f, 0.12f) * fg, coreR, LevelOf(surfaceY, c.y + 0.12f * fg, coreR), sway, core.WithA(0.85f));
                // The meniscus: a bright line where the fluid meets the glass, while it is still filling.
                if (full < 1f && level < 0.97f) Chord(p, c, fg, level, sway, Palette.OrbLitHighlight.WithA(0.7f * (1f - full)), Mathf.Max(1f, r * 0.05f));
                if (level > 0.5f) Disc(p, c + new Vector2(0.28f, 0.30f) * fg, fg * 0.2f, Palette.ManaGlint.WithA(0.5f * Mathf.InverseLerp(0.5f, 1f, level)));
            }

            // Glass rim light, brighter on a full orb as it breathes.
            Ring(p, c, glass - 0.5f, Mathf.Max(1f, r * 0.05f), Palette.OrbLitHighlight.WithA(0.45f + 0.35f * full * breathe));
            // The glass's own highlights, lit or not: a specular glint top-left and a small bounce reflection lower right.
            Disc(p, c + new Vector2(-0.34f, -0.38f) * glass, glass * 0.2f, Palette.White.WithA(level > 0.01f ? 0.8f : 0.55f));
            Disc(p, c + new Vector2(-0.12f, -0.52f) * glass, glass * 0.07f, Palette.White.WithA(0.45f));
            Disc(p, c + new Vector2(0.42f, 0.42f) * glass, glass * 0.08f, Palette.White.WithA(0.18f));

            if (flash > 0.02f) Disc(p, c, r, Palette.White.WithA(0.5f * flash));
            if (Preview && level > 0.5f)
            {
                float k = ReduceMotion ? 1f : 0.6f + 0.4f * Mathf.Sin(phase * 2f);
                Ring(p, c, r - 1f, Mathf.Max(2f, r * 0.14f), Palette.White.WithA(0.85f * k));
            }
            if (Short && !Filled) Ring(p, c, r - 1f, Mathf.Max(2f, r * 0.14f), Palette.Bad);
        }

        /// <summary>The level (0..1 of a circle of <paramref name="radius"/> at <paramref name="cy"/>) whose surface sits
        /// at <paramref name="surfaceY"/>.</summary>
        private static float LevelOf(float surfaceY, float cy, float radius) => Mathf.Clamp01((cy + radius - surfaceY) / (2f * radius));

        /// <summary>The fluid in a circle: the segment below a surface at <paramref name="lvl"/> of its height, tilted by
        /// <paramref name="tilt"/> degrees (the arc runs through the bottom, 90 degrees, and the chord closes it).</summary>
        private static void Segment(Painter2D p, Vector2 c, float radius, float lvl, float tilt, Color col)
        {
            if (lvl <= 0.005f) return;
            if (lvl >= 0.995f) { Disc(p, c, radius, col); return; }
            float a = Mathf.Asin(Mathf.Clamp(1f - 2f * lvl, -1f, 1f)) * Mathf.Rad2Deg;
            p.fillColor = col;
            p.BeginPath();
            p.Arc(c, radius, Angle.Degrees(a + tilt), Angle.Degrees(180f - a + tilt));
            p.ClosePath();
            p.Fill();
        }

        /// <summary>The fluid's surface line, matching <see cref="Segment"/>'s chord.</summary>
        private static void Chord(Painter2D p, Vector2 c, float radius, float lvl, float tilt, Color col, float width)
        {
            if (lvl <= 0.02f) return;
            float a = Mathf.Asin(Mathf.Clamp(1f - 2f * lvl, -1f, 1f)) * Mathf.Rad2Deg;
            float a0 = (a + tilt) * Mathf.Deg2Rad, a1 = (180f - a + tilt) * Mathf.Deg2Rad;
            p.strokeColor = col;
            p.lineWidth = width;
            p.lineCap = LineCap.Round;
            p.BeginPath();
            p.MoveTo(c + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * radius * 0.92f);
            p.LineTo(c + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * radius * 0.92f);
            p.Stroke();
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
    }
}
