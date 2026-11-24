using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>
    /// GG-T846 (BIBLE §14.23, Shapes §11.6): the vector dressing on a painted region tile. A timed mark shows a life ring
    /// in its state's colour, its lit arc draining clockwise from 12 o'clock as its LifeSeconds run out (over a dark ink
    /// track); the party's own hidden mark keeps a dotted frame in its owner's colour (dashed = there, but only to you).
    /// </summary>
    public sealed class TileMarkFx : VisualElement
    {
        /// <summary>Share of the mark's life left (0..1); below 0 = no ring (the mark lasts until cleared).</summary>
        public float Life = -1f;
        private readonly Color owner, state;
        private readonly bool hidden;

        public TileMarkFx(Color owner, Color state, bool hidden)
        {
            this.owner = owner; this.state = state; this.hidden = hidden;
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = style.top = style.right = style.bottom = 0f;
            generateVisualContent += Draw;
        }

        private void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect;
            if (r.width <= 0f || r.height <= 0f) return;
            var p = ctx.painter2D;
            if (hidden)
            {
                var st = ShapeStyle.Small.Tint(owner.WithA(0.9f)).Thick(5f).Dashes(0f, 10f);
                var inner = new Rect(r.x + 10f, r.y + 10f, r.width - 20f, r.height - 20f);
                Shapes.RoundedRect(p, inner, 14f, st, dashed: true);
            }
            if (Life >= 0f)
            {
                var st = ShapeStyle.Small.Thick(6f);
                float radius = Mathf.Min(r.width, r.height) * 0.38f;
                Shapes.Ring(p, r.center, radius, st.Tint(ShapeStyle.Ink.WithA(0.35f)));
                if (Life > 0f) Shapes.Ring(p, r.center, radius, st.Tint(Color.Lerp(state, Color.white, 0.25f).WithA(hidden ? 0.5f : 0.95f)), -90f, 360f * Life);
            }
        }
    }

    /// <summary>GG-T846: a squished mark's splat: blobs of its colour burst outward from the tile centre and fade
    /// (Reduce Motion: one blob fades in place). It removes itself when done.</summary>
    public sealed class TileSplat : VisualElement
    {
        private const float Seconds = 0.5f;
        private readonly Color color;
        private readonly bool calm;
        private readonly float t0;
        private readonly Vector2[] dirs;
        private readonly float[] sizes;

        public TileSplat(Color color, bool calm)
        {
            this.color = color; this.calm = calm;
            t0 = Tween.Clock;
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.width = style.height = BoardView.Tile;
            int n = calm ? 0 : 7;
            dirs = new Vector2[n]; sizes = new float[n];
            for (int i = 0; i < n; i++)
            {
                float a = (i / (float)n + Random.Range(-0.05f, 0.05f)) * Mathf.PI * 2f;
                dirs[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(0.6f, 1f);
                sizes[i] = Random.Range(10f, 20f);
            }
            generateVisualContent += Draw;
            schedule.Execute(() => { if (Tween.Clock - t0 >= Seconds) RemoveFromHierarchy(); else MarkDirtyRepaint(); }).Every(16).Until(() => parent == null);
        }

        private void Draw(MeshGenerationContext ctx)
        {
            float k = Mathf.Clamp01((Tween.Clock - t0) / Seconds);
            var c = contentRect.center;
            var p = ctx.painter2D;
            float fade = 1f - k * k;
            var st = ShapeStyle.Small.Tint(color.WithA(0.9f * fade)).Outlined(ShapeStyle.Ink.WithA(0.9f * fade), 2f);
            // The flattened body where the mark was, then the flung drops.
            Shapes.Dot(p, c, BoardView.Tile * (calm ? 0.28f : Mathf.Lerp(0.18f, 0.34f, Mathf.Sqrt(k))), st);
            float reach = BoardView.Tile * 0.5f * (1f - (1f - k) * (1f - k));
            for (int i = 0; i < dirs.Length; i++) Shapes.Dot(p, c + dirs[i] * reach, sizes[i] * (1f - 0.5f * k), st);
        }
    }
}
