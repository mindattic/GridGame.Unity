using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>
    /// GG-T846 (BIBLE §14.23, Shapes §11.6): the vector dressing on a painted region tile. The party's own hidden mark
    /// keeps a dotted frame in its owner's colour (dashed = there, but only to you). No life ring: a board tile carries
    /// no countdown (BIBLE §9.0, GG-T1817).
    /// </summary>
    public sealed class TileMarkFx : VisualElement
    {
        /// <summary>Share of the mark's life left (0..1); below 0 = the mark lasts until cleared. Only a hatching glob draws it.</summary>
        public float Life = -1f;
        /// <summary>GG-T895: the mark hatches when its life runs out (a slime glob): it is drawn as a wobbling blob of its
        /// state's colour that swells, and wobbles harder and quicker, as <see cref="Life"/> drains. <see cref="Calm"/>
        /// (Reduce Motion) holds it still, swelling only.</summary>
        public bool Glob, Calm;
        /// <summary>GG-T935: the mark is the party's standing wall (the Machinist's barricade): drawn as a braced crate,
        /// <see cref="Wear"/> (0..1, hits taken of its threshold) splitting its planks.</summary>
        public bool Crate;
        public float Wear;
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
            if (Glob && !hidden)
            {
                float grow = Life >= 0f ? 1f - Life : 0f;
                float radius = Mathf.Min(r.width, r.height) * (0.2f + 0.1f * grow);
                float wobble = Calm ? 0f : 0.05f + 0.1f * grow;
                float phase = Calm ? 0f : Tween.Clock * (3f + 6f * grow);
                var c = r.center + new Vector2(0f, radius * 0.15f);
                Shapes.Blob(p, c, radius, wobble, phase, ShapeStyle.Small.Tint(state.WithA(0.95f)).Outlined(ShapeStyle.Ink, 3f));
                // A wet shine on its upper left, riding the wobble.
                var shine = c + new Vector2(-radius * 0.35f, -radius * (0.4f + 0.5f * wobble * Mathf.Sin(phase)));
                Shapes.Dot(p, shine, radius * 0.18f, ShapeStyle.Small.Tint(Color.white.WithA(0.7f)).Outlined(Color.clear, 0f));
            }
            if (Crate && !hidden) DrawCrate(p, r, Wear);
        }

        /// <summary>The crate wood (GG-T935).</summary>
        public static readonly Color Wood = new Color(0.62f, 0.45f, 0.26f, 1f);

        /// <summary>GG-T935 (Shapes §11.6): a braced crate filling most of <paramref name="r"/>: a wood body with the ink
        /// outline, an inner frame and a cross brace (each a Shapes bar, ink-outlined); <paramref name="wear"/> (0..1) darkens
        /// the wood and splits the brace, so a wall about to give reads as one.</summary>
        public static void DrawCrate(Painter2D p, Rect r, float wear)
        {
            float pad = Mathf.Min(r.width, r.height) * 0.16f;
            var box = new Rect(r.x + pad, r.y + pad, r.width - 2f * pad, r.height - 2f * pad);
            var wood = Color.Lerp(Wood, new Color(0.3f, 0.2f, 0.12f, 1f), Mathf.Clamp01(wear) * 0.6f);
            var plank = Color.Lerp(wood, Color.white, 0.25f);
            Shapes.RoundedRect(p, box, 8f, ShapeStyle.Small.Tint(wood).Outlined(ShapeStyle.Ink, 3f), filled: true);
            var inner = new Rect(box.x + 9f, box.y + 9f, box.width - 18f, box.height - 18f);
            Shapes.RoundedRect(p, inner, 4f, ShapeStyle.Small.Tint(plank).Thick(5f).Outlined(ShapeStyle.Ink, 2f));
            var brace = ShapeStyle.Small.Tint(plank).Thick(7f).Outlined(ShapeStyle.Ink, 2f);
            var a = new Vector2(inner.xMin + 4f, inner.yMax - 4f);
            var b = new Vector2(inner.xMax - 4f, inner.yMin + 4f);
            if (wear < 0.5f) Shapes.Bar(p, a, b, brace);
            else
            {
                // Split: the brace snaps in the middle, each half sagging.
                var mid = (a + b) * 0.5f;
                Shapes.Bar(p, a, mid + new Vector2(-6f, 8f), brace);
                Shapes.Bar(p, mid + new Vector2(6f, -2f), b, brace);
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
