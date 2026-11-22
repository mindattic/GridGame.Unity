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
    /// GG-T670: behind the Somnolith runs the wake of debris it drags across the sky (<see cref="DebrisDef"/>), and now
    /// and then the Aetherain falls from it, glints of aether dust drifting down over the landscape
    /// (<see cref="AetherainDef"/>). Reduce Motion holds the wake still and skips the Aetherain.
    ///
    /// The plates cover the screen at the art's own aspect (cropping, never stretching, so 9:23 to 3:4 all fill), with a
    /// small margin so the sway never shows an edge. The bodies are placed in screen terms (see <see cref="SkyDef"/>), so
    /// the Somnolith stays in the visible sky at every aspect. A backdrop that isn't split (an interior, a rejected split)
    /// is drawn flat exactly as before.
    /// </summary>
    public sealed class LivingBackdrop
    {
        /// <summary>Marks a living backdrop's root so <see cref="GameScreen.BleedBackdrops"/> still bleeds it under the
        /// safe-area insets although it holds layer children (GG-T663).</summary>
        public const string RootClass = "living-backdrop";

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

        /// <summary>One piece of the Somnolith's wake (GG-T670). U0 is where it starts along the wake (0 at the body, 1 at
        /// the far end), Across its offset across the wake (-1..1), Jitter its size wobble; it is sized once at its largest
        /// and scaled down as it drifts away.</summary>
        private sealed class Debris
        {
            public VisualElement El;
            public float U0, Across, Jitter, Spin, Angle0, Opacity, Aspect;
            public bool Speck;
        }

        /// <summary>One falling glint of the Aetherain (GG-T670).</summary>
        private sealed class Mote
        {
            public VisualElement El;
            public float X0, Phase, Speed, Size, Drift, DriftHz, Twinkle, Seed;
        }

        private readonly VisualElement bg, sky, moon, som, fg;
        private readonly List<Shard> shards = new List<Shard>();
        private readonly List<Debris> debris = new List<Debris>();
        private readonly List<Mote> motes = new List<Mote>();
        /// <summary>Round pieces (specks, motes) are coloured, not textured, so the backdrop tint is applied to them by hand.</summary>
        private readonly List<(VisualElement el, Color c)> tinted = new List<(VisualElement, Color)>();
        private readonly DebrisDef wake;
        private readonly AetherainDef rain;
        private readonly SkyDef def;
        private readonly SkyRegionDef region;
        private readonly BackdropLayerDef layer;
        private readonly float aspect, moonAspect, somAspect, start;
        private readonly bool still, live;
        private Color tint = Color.white;
        private float w, h, skyBottom, refW;
        private float somX, somY, somR, rainTop;
        private Vector2 wakeDir, wakeAcross;

        private LivingBackdrop(VisualElement bg, string theme, Texture2D fgTex, Texture2D skyTex, BackdropLayerDef layer, SkyDef def, bool still)
        {
            this.bg = bg; this.layer = layer; this.def = def; this.still = still;
            bg.AddToClassList(RootClass);
            region = def.RegionFor(theme);
            aspect = (float)fgTex.width / fgTex.height;
            start = Time.realtimeSinceStartup;
            bg.style.backgroundImage = StyleKeyword.None;
            bg.style.overflow = Overflow.Hidden;

            sky = Layer(skyTex);
            var moonTex = Load(def.Moon?.Image);
            moon = moonTex != null ? Layer(moonTex) : null;
            moonAspect = moonTex != null ? (float)moonTex.height / moonTex.width : 1f;
            var rnd = new System.Random(); // cosmetic: a fresh wake and a fresh roll for the Aetherain every time
            wake = def.DebrisFor(region);
            BuildDebris(rnd); // behind the body: it sits in front of its own wake
            // GG-T679: the Manalith is the live 3D satellite (ManalithRig) when its model loads, else the flat art.
            if (!string.IsNullOrEmpty(def.Somnolith?.Model))
            {
                som = Layer(null);
                if (ManalithRig.Attach(som)) live = true;
                else { bg.Remove(som); som = null; }
            }
            if (som == null)
            {
                var somTex = Load(def.Somnolith?.Image);
                som = somTex != null ? Layer(somTex) : null;
                somAspect = somTex != null ? (float)somTex.height / somTex.width : 1f;
            }
            else somAspect = 1f;
            if (moon != null) moon.style.opacity = def.Moon.Opacity;
            if (som != null) som.style.opacity = def.Somnolith.Opacity;
            BuildShards();
            fg = Layer(fgTex);
            // The Aetherain falls in front of the landscape (still inside this backdrop, so behind every screen's content).
            rain = def.Aetherain;
            if (!still && rain != null && rnd.NextDouble() < def.AetherainChanceFor(region)) BuildAetherain(rnd);

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

        private VisualElement Dot(Color c)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore, usageHints = UsageHints.DynamicTransform };
            e.style.position = Position.Absolute;
            var round = Length.Percent(50);
            e.style.borderTopLeftRadius = round; e.style.borderTopRightRadius = round;
            e.style.borderBottomLeftRadius = round; e.style.borderBottomRightRadius = round;
            e.style.backgroundColor = c;
            bg.Add(e);
            tinted.Add((e, c));
            return e;
        }

        private static Color ParseColor(string hex, Color fallback) =>
            !string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out var c) ? c : fallback;

        /// <summary>GG-T674: one colour from a palette sampled from the Somnolith art (repeats weight it).</summary>
        private static Color PickColor(System.Collections.Generic.List<string> palette, System.Random rnd, Color fallback) =>
            palette == null || palette.Count == 0 ? fallback : ParseColor(palette[rnd.Next(palette.Count)], fallback);

        /// <summary>The Somnolith art's deep blue (GG-T674), for a palette that is missing.</summary>
        private static readonly Color SomnolithBlue = new Color(0.365f, 0.392f, 0.639f);

        private void BuildDebris(System.Random rnd)
        {
            var d = wake;
            if (d == null || d.Count <= 0) return;
            var images = def.Shards?.Images;
            int count = Mathf.Min(d.Count, DebrisDef.MaxCount);
            for (int i = 0; i < count; i++)
            {
                bool speck = images == null || images.Count == 0 || rnd.NextDouble() < d.SpeckShare;
                Texture2D tex = speck ? null : Load(images[rnd.Next(images.Count)]);
                if (!speck && tex == null) speck = true;
                // Two uniform draws averaged lean the offset toward the wake's centre line; the power leans pieces
                // toward the body, so the wake is densest where it leaves the Somnolith.
                float across = (float)(rnd.NextDouble() + rnd.NextDouble() - 1.0);
                var piece = new Debris
                {
                    El = speck ? Dot(PickColor(d.SpeckColors, rnd, SomnolithBlue)) : Layer(tex),
                    U0 = Mathf.Pow((float)rnd.NextDouble(), 1.6f),
                    Across = across,
                    Jitter = 0.6f + 0.6f * (float)rnd.NextDouble(),
                    Spin = (def.Shards?.Spin ?? 3f) * (float)(rnd.NextDouble() * 2 - 1),
                    Angle0 = (float)(rnd.NextDouble() * 360),
                    Opacity = d.Opacity * (speck ? 0.7f : 1f) * (0.6f + 0.4f * (float)rnd.NextDouble()),
                    Aspect = tex != null ? (float)tex.height / tex.width : 1f,
                    Speck = speck,
                };
                piece.El.style.opacity = 0f;
                debris.Add(piece);
            }
        }

        private void BuildAetherain(System.Random rnd)
        {
            int n = Mathf.Min(rain.Motes, AetherainDef.MaxMotes);
            for (int i = 0; i < n; i++)
            {
                var m = new Mote
                {
                    El = Dot(PickColor(rain.Colors, rnd, SomnolithBlue)),
                    X0 = (float)rnd.NextDouble(),
                    Phase = (float)rnd.NextDouble(),
                    Speed = (0.8f + 0.4f * (float)rnd.NextDouble()) / Mathf.Max(1f, rain.FallSeconds),
                    Size = Mathf.Lerp(rain.MinSize, rain.MaxSize, (float)rnd.NextDouble()),
                    Drift = 0.006f + 0.01f * (float)rnd.NextDouble(),
                    DriftHz = 0.08f + 0.12f * (float)rnd.NextDouble(),
                    Twinkle = 0.6f + 1.4f * (float)rnd.NextDouble(),
                    Seed = (float)(rnd.NextDouble() * 6.283),
                };
                m.El.style.opacity = 0f;
                motes.Add(m);
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
            // The live satellite fills FillHeight of its square frame, so the frame is enlarged to keep the body the
            // size the flat art was; it tumbles on its own, so no fixed rotation.
            if (som != null && live) Place(som, region.Size / ManalithRig.FillHeight, 1f, region.X, region.Y, 0f);
            else if (som != null) Place(som, region.Size, somAspect, region.X, region.Y, region.Rotation);
            foreach (var sh in shards)
            {
                float sw = sh.Size * refW;
                sh.El.style.width = sw; sh.El.style.height = sw * sh.Aspect;
                sh.El.style.left = Space.S0; sh.El.style.top = sh.Y * skyBottom - sw * sh.Aspect / 2f;
            }
            // The wake leaves the Somnolith's centre along Angle; each piece is sized once at its largest and scaled in Tick.
            somX = region.X * w; somY = region.Y * skyBottom; somR = region.Size * refW / 2f;
            if (wake != null)
            {
                float a = wake.Angle * Mathf.Deg2Rad;
                wakeDir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                wakeAcross = new Vector2(-wakeDir.y, wakeDir.x);
                foreach (var p in debris)
                {
                    float pw = (p.Speck ? wake.MinSize * 2.5f : wake.MaxSize) * p.Jitter * refW;
                    p.El.style.width = pw; p.El.style.height = pw * p.Aspect;
                    p.El.style.left = Space.S0; p.El.style.top = Space.S0;
                }
            }
            rainTop = Mathf.Max(0f, somY - somR);
            foreach (var m in motes)
            {
                float mw = m.Size * refW;
                m.El.style.width = mw; m.El.style.height = mw;
                m.El.style.left = Space.S0; m.El.style.top = Space.S0;
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
                foreach (var (el, c) in tinted) el.style.backgroundColor = new Color(c.r * tint.r, c.g * tint.g, c.b * tint.b, c.a);
            }
            float t = still ? 0f : Time.realtimeSinceStartup - start;
            float sway = still ? 0f : Mathf.Sin(2f * Mathf.PI * t / Mathf.Max(1f, def.Parallax.Seconds)) * def.Parallax.Amplitude * w;
            sky.style.translate = new Translate(sway * 0.12f, 0f);
            if (moon != null) moon.style.translate = new Translate(sway * 0.18f, 0f);
            float bob = still ? 0f : Mathf.Sin(t * 0.11f) * 0.004f * h;
            if (som != null)
                som.style.translate = new Translate(sway * 0.3f, bob);
            TickWake(t, sway * 0.3f, bob);
            TickAetherain(t, sway);
            foreach (var sh in shards)
            {
                float sw = sh.Size * refW, span = w + 2f * sw;
                float x = Mathf.Repeat(sh.X0 * span + t * sh.Speed * w, span) - sw;
                sh.El.style.translate = new Translate(x + sway * (0.4f + 0.4f * sh.Size / Mathf.Max(0.001f, def.Shards.MaxSize)), 0f);
                sh.El.style.rotate = new Rotate(sh.Angle0 + t * sh.Spin);
            }
            fg.style.translate = new Translate(sway, 0f);
        }

        /// <summary>The wake follows the Somnolith (its sway and bob); each piece drifts from the body to the far end and
        /// starts again near the body, shrinking and fading as it goes. Reduce Motion holds every piece where it began.</summary>
        private void TickWake(float t, float followX, float followY)
        {
            if (wake == null || debris.Count == 0) return;
            float length = wake.Length * w, spread = wake.Spread * w;
            float baseX = somX + followX, baseY = somY + followY;
            foreach (var p in debris)
            {
                float u = still ? p.U0 : Mathf.Repeat(p.U0 + t / Mathf.Max(1f, wake.DriftSeconds), 1f);
                float dist = somR * 0.8f + u * length; // starts at the body's rim, which is irregular, so a little inside
                float across = p.Across * spread * (0.25f + 0.75f * u);
                float x = baseX + wakeDir.x * dist + wakeAcross.x * across;
                float y = baseY + wakeDir.y * dist + wakeAcross.y * across;
                // Largest at the body, down to MinSize at the far end (specks start at 2.5x MinSize).
                float big = p.Speck ? wake.MinSize * 2.5f : wake.MaxSize;
                float k = Mathf.Lerp(1f, wake.MinSize / Mathf.Max(0.0001f, big), Mathf.Pow(u, 0.7f));
                float pw = big * p.Jitter * refW;
                p.El.style.translate = new Translate(x - pw / 2f, y - pw * p.Aspect / 2f);
                p.El.style.scale = new Scale(new Vector2(k, k));
                if (!p.Speck) p.El.style.rotate = new Rotate(p.Angle0 + t * p.Spin);
                float fadeIn = Mathf.Clamp01(u / 0.04f), fadeOut = Mathf.Pow(1f - u, 0.8f);
                p.El.style.opacity = p.Opacity * fadeIn * fadeOut;
            }
        }

        /// <summary>The Aetherain: glints fall from the wake's band to the bottom of the screen, drifting and twinkling.</summary>
        private void TickAetherain(float t, float sway)
        {
            if (motes.Count == 0) return;
            float fallTo = h;
            foreach (var m in motes)
            {
                float f = Mathf.Repeat(m.Phase + t * m.Speed, 1f);
                float mw = m.Size * refW;
                float y = Mathf.Lerp(rainTop, fallTo + mw, f);
                float x = m.X0 * w + Mathf.Sin(t * 2f * Mathf.PI * m.DriftHz + m.Seed) * m.Drift * w + sway;
                m.El.style.translate = new Translate(x - mw / 2f, y - mw / 2f);
                float twinkle = 0.55f + 0.45f * Mathf.Sin(t * m.Twinkle * 2f * Mathf.PI + m.Seed);
                float edge = Mathf.Clamp01(f / 0.08f) * Mathf.Clamp01((1f - f) / 0.08f);
                m.El.style.opacity = rain.Opacity * twinkle * edge;
            }
        }
    }
}
