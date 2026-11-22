using System.Collections.Generic;
using GridGame.Core.Content;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>
    /// The staged, living backdrop (GG-T576; owner: "the Somnolith needs to be the same in every picture ... a living
    /// backdrop (imagine slowly moving shards of Somnolith being dragged across the sky)"). Back to front: the backdrop's
    /// sky plate (Resources/Skies), the ordinary Moon, the one canonical Somnolith (sized and placed per region from
    /// Content/sky.json, growing as the campaign goes on), its shards drifting across the sky, then the foreground plate
    /// (the backdrop with its sky cut out, under the backdrop's own resource key). A gentle idle sway moves the near layers
    /// more than the far ones. Reduce Motion keeps every layer still.
    ///
    /// The plates cover the screen at the art's own aspect (cropping, never stretching, so 9:23 to 3:4 all fill), with a
    /// small margin so the sway never shows an edge. The bodies are placed in screen terms (see <see cref="SkyDef"/>), so
    /// the Somnolith stays in the visible sky at every aspect. A backdrop that isn't split (an interior, a rejected split)
    /// is drawn flat exactly as before.
    /// </summary>
    public sealed class LivingBackdrop
    {
        /// <summary>Paints <paramref name="bg"/> (a full-screen element) with <paramref name="theme"/>'s
        /// <paramref name="index"/>th backdrop: living when it is split, the flat image otherwise. Set the element's
        /// tint after this as before; the layers pick it up.</summary>
        public static void Show(VisualElement bg, string theme, int index = 0)
        {
            var app = GameApp.I;
            var key = Art.BackgroundKey(theme, index);
            var tex = Art.Background(theme, index);
            var content = app != null ? app.Content : null;
            var stem = key.Substring(key.LastIndexOf('/') + 1);
            Texture2D skyTex = null;
            if (content != null && content.BackdropLayers.TryGetValue(stem, out var layer) && layer != null && layer.Sky)
                skyTex = Resources.Load<Texture2D>("Skies/" + stem);
            if (skyTex == null || tex == null)
            {
                bg.style.backgroundImage = new StyleBackground(tex);
                return;
            }
            new LivingBackdrop(bg, theme, tex, skyTex, content.BackdropLayers[stem], content.Sky,
                app.Settings != null && app.Settings.ReduceMotion);
        }

        private sealed class Shard
        {
            public VisualElement El;
            public float X0, Y, Size, Speed, Spin, Angle0, Opacity, Aspect;
        }

        private readonly VisualElement bg, sky, moon, som, fg;
        private readonly List<Shard> shards = new List<Shard>();
        private readonly SkyDef def;
        private readonly SkyRegionDef region;
        private readonly BackdropLayerDef layer;
        private readonly float aspect, moonAspect, somAspect, start;
        private readonly bool still;
        private Color tint = Color.white;
        private float w, h, skyBottom, refW;

        private LivingBackdrop(VisualElement bg, string theme, Texture2D fgTex, Texture2D skyTex, BackdropLayerDef layer, SkyDef def, bool still)
        {
            this.bg = bg; this.layer = layer; this.def = def; this.still = still;
            region = def.RegionFor(theme);
            aspect = (float)fgTex.width / fgTex.height;
            start = Time.realtimeSinceStartup;
            bg.style.backgroundImage = StyleKeyword.None;
            bg.style.overflow = Overflow.Hidden;

            sky = Layer(skyTex);
            var moonTex = Load(def.Moon?.Image);
            moon = moonTex != null ? Layer(moonTex) : null;
            moonAspect = moonTex != null ? (float)moonTex.height / moonTex.width : 1f;
            var somTex = Load(def.Somnolith?.Image);
            som = somTex != null ? Layer(somTex) : null;
            somAspect = somTex != null ? (float)somTex.height / somTex.width : 1f;
            if (moon != null) moon.style.opacity = def.Moon.Opacity;
            if (som != null) som.style.opacity = def.Somnolith.Opacity;
            BuildShards();
            fg = Layer(fgTex);

            bg.RegisterCallback<GeometryChangedEvent>(_ => Layout());
            if (!still) bg.schedule.Execute(Tick).Every(33);
        }

        private static Texture2D Load(string key) => string.IsNullOrEmpty(key) ? null : Resources.Load<Texture2D>(key);

        private VisualElement Layer(Texture2D tex)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore, usageHints = UsageHints.DynamicTransform };
            e.style.position = Position.Absolute;
            e.style.backgroundImage = new StyleBackground(tex);
            e.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            bg.Add(e);
            return e;
        }

        private void BuildShards()
        {
            var s = def.Shards;
            if (s == null || s.Count <= 0 || s.Images == null || s.Images.Count == 0) return;
            var rnd = new System.Random(); // cosmetic: a fresh drift every time
            for (int i = 0; i < s.Count; i++)
            {
                var tex = Load(s.Images[i % s.Images.Count]);
                if (tex == null) continue;
                float depth = (float)(rnd.NextDouble() * rnd.NextDouble()); // most shards far and small, a few near
                var sh = new Shard
                {
                    El = Layer(tex),
                    X0 = (float)rnd.NextDouble(),
                    Y = 0.08f + 0.8f * (float)rnd.NextDouble(),
                    Size = Mathf.Lerp(s.MinSize, s.MaxSize, depth),
                    Speed = (0.35f + 0.65f * depth) / Mathf.Max(1f, s.CrossSeconds),
                    Spin = s.Spin * (float)(rnd.NextDouble() * 2 - 1),
                    Angle0 = (float)(rnd.NextDouble() * 360),
                    Opacity = s.Opacity * (0.55f + 0.45f * depth),
                    Aspect = (float)tex.height / tex.width,
                };
                sh.El.style.opacity = sh.Opacity;
                shards.Add(sh);
            }
        }

        private void Layout()
        {
            w = bg.resolvedStyle.width; h = bg.resolvedStyle.height;
            if (float.IsNaN(w) || float.IsNaN(h) || w <= 0f || h <= 0f) return;
            float margin = still ? 0f : def.Parallax.Amplitude * 1.5f;
            float coverW = Mathf.Max(w, h * aspect) * (1f + 2f * margin), coverH = coverW / aspect;
            float left = (w - coverW) / 2f, top = (h - coverH) / 2f;
            foreach (var plate in new[] { sky, fg })
            {
                plate.style.left = left; plate.style.top = top;
                plate.style.width = coverW; plate.style.height = coverH;
            }
            // Where this backdrop's sky ends on screen; bodies sit in the sky above it.
            skyBottom = Mathf.Clamp(top + layer.SkyBottom * coverH, h * 0.15f, h);
            refW = Mathf.Min(w, 0.6f * h);
            if (moon != null) Place(moon, def.Moon.Size, moonAspect, def.Moon.X, def.Moon.Y, def.Moon.Rotation);
            if (som != null) Place(som, region.Size, somAspect, region.X, region.Y, region.Rotation);
            foreach (var sh in shards)
            {
                float sw = sh.Size * refW;
                sh.El.style.width = sw; sh.El.style.height = sw * sh.Aspect;
                sh.El.style.left = 0f; sh.El.style.top = sh.Y * skyBottom - sw * sh.Aspect / 2f;
            }
            Tick();
        }

        private void Place(VisualElement e, float size, float imgAspect, float x, float y, float rotation)
        {
            float bw = size * refW, bh = bw * imgAspect;
            e.style.width = bw; e.style.height = bh;
            e.style.left = x * w - bw / 2f; e.style.top = y * skyBottom - bh / 2f;
            e.style.rotate = new Rotate(rotation);
        }

        private void Tick()
        {
            if (w <= 0f) return;
            var t2 = bg.resolvedStyle.unityBackgroundImageTintColor;
            if (t2 != tint)
            {
                tint = t2;
                foreach (var child in bg.Children()) child.style.unityBackgroundImageTintColor = tint;
            }
            float t = still ? 0f : Time.realtimeSinceStartup - start;
            float sway = still ? 0f : Mathf.Sin(2f * Mathf.PI * t / Mathf.Max(1f, def.Parallax.Seconds)) * def.Parallax.Amplitude * w;
            sky.style.translate = new Translate(sway * 0.12f, 0f);
            if (moon != null) moon.style.translate = new Translate(sway * 0.18f, 0f);
            if (som != null)
                som.style.translate = new Translate(sway * 0.3f, still ? 0f : Mathf.Sin(t * 0.11f) * 0.004f * h);
            foreach (var sh in shards)
            {
                float sw = sh.Size * refW, span = w + 2f * sw;
                float x = Mathf.Repeat(sh.X0 * span + t * sh.Speed * w, span) - sw;
                sh.El.style.translate = new Translate(x + sway * (0.4f + 0.4f * sh.Size / Mathf.Max(0.001f, def.Shards.MaxSize)), 0f);
                sh.El.style.rotate = new Rotate(sh.Angle0 + t * sh.Spin);
            }
            fg.style.translate = new Translate(sway, 0f);
        }
    }
}
