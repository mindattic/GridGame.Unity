using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>
    /// One mana orb in the battle's orb row (GG-T673, owner 2026-11-22: "return the mana orbs line, doing things fills mana
    /// orbs which glow with bloom once they are active"). Drawn in code: empty, a dark navy glass bead; filled, indigo liquid
    /// with a violet core and the Somnolith art's red-pink glint (<see cref="Palette.ManaIndigo"/> and friends). The orb that
    /// is forming next carries <see cref="Charge"/> as an arc round its rim. <see cref="Preview"/> marks an orb an aimed
    /// spell would spend (a pale pulsing rim). The bloom is a separate element behind it (BattleScreen.BuildManaOrbs).
    /// Reduce Motion: no breathing and no flash, just the state.
    /// </summary>
    public sealed class ManaOrbView : VisualElement
    {
        public bool Filled;
        /// <summary>The next orb forming (the mana charge, §3.1.9), 0..1, drawn as an arc from the top.</summary>
        public float Charge;
        public bool Preview;
        /// <summary>An empty orb an aimed spell still needs: a red rim, so "can't afford it" reads from the row.</summary>
        public bool Short;
        public bool ReduceMotion;

        private float flash, phase, fill = -1f;

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
            float target = Filled ? 1f : 0f;
            fill = fill < 0f || ReduceMotion ? target : fill + (target - fill) * Mathf.Min(1f, dt * 9f);
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

            Disc(p, c, r, Palette.ManaDeep.WithA(0.92f));
            Ring(p, c, r - 1f, Mathf.Max(1.5f, r * 0.08f), Palette.ManaBloom.WithA(0.35f));
            if (fill > 0.01f)
            {
                float g = r * 0.82f * fill;
                Disc(p, c, g, Palette.ManaIndigo);
                Disc(p, c + new Vector2(0f, 0.12f) * g, g * 0.62f, Color.Lerp(Palette.ManaIndigo, Palette.ManaBloom, 0.6f));
                Disc(p, c + new Vector2(0.28f, 0.30f) * g, g * 0.22f, Palette.ManaGlint.WithA(0.55f * fill));
                Disc(p, c + new Vector2(-0.32f, -0.36f) * g, g * 0.16f, Palette.White.WithA(0.75f * fill));
            }
            if (flash > 0.02f) Disc(p, c, r, Palette.White.WithA(0.5f * flash));
            if (Preview && fill > 0.5f)
            {
                float k = ReduceMotion ? 1f : 0.6f + 0.4f * Mathf.Sin(phase * 2f);
                Ring(p, c, r - 1f, Mathf.Max(2f, r * 0.14f), Palette.White.WithA(0.85f * k));
            }
            if (Short && !Filled) Ring(p, c, r - 1f, Mathf.Max(2f, r * 0.14f), Palette.Bad);
            if (Charge > 0.005f && !Filled)
            {
                p.lineCap = LineCap.Round;
                p.strokeColor = Palette.ManaBloom;
                p.lineWidth = Mathf.Max(2f, r * 0.16f);
                p.BeginPath();
                p.Arc(c, r - p.lineWidth / 2f, Angle.Degrees(-90f), Angle.Degrees(-90f + 360f * Mathf.Clamp01(Charge)));
                p.Stroke();
            }
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
