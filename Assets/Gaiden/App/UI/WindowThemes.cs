using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>
    /// Data-driven menu window themes (GG-T407). The presets come from <c>Resources/UI/themes.json</c> (validated by
    /// <see cref="WindowThemeCatalog"/>; a bad one is logged and left out, and the game never runs without Granite), the
    /// player's own from <see cref="SettingsSave.CustomThemes"/>. A theme is:
    /// <list type="bullet">
    /// <item>a gradient baked into <see cref="Art.Window"/> (256² so it stays smooth when a window stretches it, with a
    /// sub-LSB dither against banding): <c>corners</c> (FF7-style four colours, bilinear), <c>linear</c> (2-4 stops at any
    /// angle) or <c>radial</c>;</item>
    /// <item>an optional texture overlay: a tile (procedural, or a PNG from Resources/UI/WindowTextures) repeated over an
    /// absolutely positioned, picking-ignored layer inside each window (<see cref="Dress"/>), so it stays crisp on any size
    /// and never touches layout, padding or hit-testing. UI Toolkit has no blend modes, so each mode is baked into the tile
    /// as black/white with alpha: multiply and screen are exact, overlay and soft-light (half strength) are the
    /// lighten-above / darken-below-mid-gray approximation, normal lays the tile's own colours on top;</item>
    /// <item>a frame: a Theme.uss block (<c>.theme-&lt;style&gt;</c>) for the shape of frames, tabs, buttons and header,
    /// optionally recoloured inline by 1-3 frame colours;</item>
    /// <item>optional default text and accent colours.</item>
    /// </list>
    /// </summary>
    public static class WindowThemes
    {
        public const string DefaultName = SettingsSave.DefaultMenuTheme;
        /// <summary>The window texture's size: fixed, so a theme switch repaints the same Texture2D in place.</summary>
        public const int TexSize = 256;
        public const string OverlayClass = "window-tex";
        public const string TextureFolder = "UI/WindowTextures/";

        private static List<WindowThemeDef> presets;

        /// <summary>The preset themes in file order (Granite first). Loaded once.</summary>
        public static IReadOnlyList<WindowThemeDef> Presets
        {
            get
            {
                if (presets != null) return presets;
                var asset = Resources.Load<TextAsset>("UI/themes");
                var errors = new List<string>();
                if (asset != null) presets = WindowThemeCatalog.Parse(asset.text, out errors);
                else
                {
                    presets = new List<WindowThemeDef>();
                    Debug.LogError("[WindowThemes] Resources/UI/themes.json is missing; using the built-in Granite only");
                }
                foreach (var e in errors) Debug.LogError("[WindowThemes] " + e);
                if (!presets.Any(t => t.Name == DefaultName)) presets.Insert(0, BuiltInGranite());
                return presets;
            }
        }

        /// <summary>The default look if themes.json can't be read (GG-T404's Granite).</summary>
        private static WindowThemeDef BuiltInGranite() => new WindowThemeDef
        {
            Name = DefaultName,
            Gradient = new WindowGradientDef { Mode = WindowThemeCatalog.Linear, Angle = 180f, Grain = 0.07f,
                Stops = new List<WindowStopDef> { new WindowStopDef(0f, "#8696A6"), new WindowStopDef(1f, "#242E3C") } },
            Frame = new WindowFrameDef { Style = "granite" },
        };

        public static bool IsPreset(string name) => Presets.Any(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));

        private static IEnumerable<WindowThemeDef> Customs => GameApp.I?.Settings?.CustomThemes ?? Enumerable.Empty<WindowThemeDef>();

        /// <summary>A theme by name: a preset, else one of the profile's custom themes, else Granite (an old save's
        /// theme name — Chrono, Classic, Slate … — is still a preset).</summary>
        public static WindowThemeDef Resolve(string name)
        {
            if (!string.IsNullOrEmpty(name))
            {
                var t = Presets.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))
                        ?? Customs.FirstOrDefault(x => x != null && string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
                if (t != null) return t;
            }
            return Presets.FirstOrDefault(x => x.Name == DefaultName) ?? Presets[0];
        }

        // ── The active theme ────────────────────────────────────────────────────────

        private static WindowThemeDef current;
        public static WindowThemeDef Current => current ??= Resolve(DefaultName);
        private static Color? accent;
        public static Color Accent => accent ??= Hex(Current.Accent, DerivedAccent(Current));
        /// <summary>The root USS class of the active frame style, or null for Classic (the base stylesheet).</summary>
        public static string UssClass => UssClassOf(Current.Frame?.Style);
        public static string UssClassOf(string style) => string.IsNullOrEmpty(style) || style == "classic" ? null : "theme-" + style;
        public static IEnumerable<string> AllUssClasses => WindowThemeCatalog.FrameStyles.Select(UssClassOf).Where(c => c != null);

        /// <summary>Makes <paramref name="t"/> the active theme (Art.ApplyTheme repaints the window textures).</summary>
        internal static void SetCurrent(WindowThemeDef t)
        {
            current = t ?? Resolve(DefaultName);
            accent = null;
        }

        public static Color Hex(string hex, Color fallback) =>
            WindowThemeCatalog.TryHex(hex, out int v) ? new Color32((byte)(v >> 16), (byte)(v >> 8), (byte)v, 255) : fallback;

        public static string HexOf(Color c)
        {
            Color32 k = c;
            return WindowThemeCatalog.Hex(k.r, k.g, k.b);
        }

        /// <summary>The theme's first and last gradient colours (corners: top-left and bottom-right).</summary>
        public static (Color a, Color b) Ends(WindowThemeDef t)
        {
            var g = t.Gradient;
            if (g.Mode == WindowThemeCatalog.Corners && g.Corners?.Length == 4) return (Hex(g.Corners[0], Color.gray), Hex(g.Corners[3], Color.black));
            if (g.Stops != null && g.Stops.Count > 0) return (Hex(g.Stops[0].Color, Color.gray), Hex(g.Stops[g.Stops.Count - 1].Color, Color.black));
            return (Color.gray, Color.black);
        }

        /// <summary>The accent derived by colour theory, never hand-picked (GG-T185): the complement of the circular
        /// hue-midpoint of the gradient's two ends, pushed toward full saturation and brightness.</summary>
        public static Color DerivedAccent(WindowThemeDef t)
        {
            var (a, b) = Ends(t);
            Color.RGBToHSV(a, out var ha, out var sa, out var va);
            Color.RGBToHSV(b, out var hb, out var sb, out var vb);
            float arc = Mathf.DeltaAngle(ha * 360f, hb * 360f) / 360f;
            float mid = Mathf.Repeat(ha + arc * 0.5f, 1f);
            float sat = Mathf.Clamp(Mathf.Max(sa, sb), 0.6f, 0.95f);
            float val = Mathf.Clamp(Mathf.Max(va, vb) * 1.05f, 0.8f, 1f);
            return Color.HSVToRGB(Mathf.Repeat(mid + 0.5f, 1f), sat, val);
        }

        /// <summary>Inline frame colours (top, left, right, bottom) for the active theme, or null when its USS block owns
        /// the frame. Classic with no colours keeps the old gradient-only look: the derived accent all round.</summary>
        public static Color[] FrameSides(WindowThemeDef t, Color accent)
        {
            var c = t.Frame?.Colors;
            if (c == null || c.Length == 0)
                return UssClassOf(t.Frame?.Style) == null ? new[] { accent, accent, accent, accent } : null;
            var k = c.Select(x => Hex(x, Color.white)).ToArray();
            switch (k.Length)
            {
                case 1: return new[] { k[0], k[0], k[0], k[0] };
                case 2: return new[] { k[0], k[0], k[1], k[1] };
                default: return new[] { k[0], k[1], k[1], k[2] };
            }
        }

        public static Color? TextColor => Current.Text != null ? Hex(Current.Text, Color.white) : (Color?)null;

        // ── Gradient baking ─────────────────────────────────────────────────────────

        /// <summary>Paints <paramref name="t"/>'s gradient into <paramref name="px"/> (row 0 = bottom, Unity's origin).
        /// <paramref name="darkTop"/>/<paramref name="darkBottom"/> darken toward black (Art.WindowDark).</summary>
        public static void Bake(WindowThemeDef t, Color32[] px, int w, int h, float darkTop = 0f, float darkBottom = 0f)
        {
            var g = t.Gradient ?? new WindowGradientDef();
            bool corners = g.Mode == WindowThemeCatalog.Corners && g.Corners?.Length == 4;
            Color[] cc = corners ? g.Corners.Select(x => Hex(x, Color.gray)).ToArray() : null;
            var stops = (g.Stops ?? new List<WindowStopDef>()).Where(s => s != null).Select(s => (at: Mathf.Clamp01(s.At), c: Hex(s.Color, Color.gray))).ToArray();
            if (!corners && stops.Length == 0) stops = new[] { (0f, Color.gray), (1f, Color.black) };
            bool radial = g.Mode == WindowThemeCatalog.Radial;
            float rad = g.Angle * Mathf.Deg2Rad;
            float dx = Mathf.Sin(rad), dy = -Mathf.Cos(rad); // CSS: 0 = up, 90 = right (y down)
            float len = Mathf.Abs(dx) + Mathf.Abs(dy);
            float cx = g.Center?.Length == 2 ? g.Center[0] : 0.5f, cy = g.Center?.Length == 2 ? g.Center[1] : 0.5f;
            float maxD = 0f;
            foreach (var (qx, qy) in new[] { (0f, 0f), (1f, 0f), (0f, 1f), (1f, 1f) })
                maxD = Mathf.Max(maxD, Mathf.Sqrt((qx - cx) * (qx - cx) + (qy - cy) * (qy - cy)));
            for (int y = 0; y < h; y++)
            {
                float v = 1f - (y + 0.5f) / h; // 0 = top
                float dark = Mathf.Lerp(darkTop, darkBottom, v);
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w;
                    Color c;
                    if (corners) c = Color.Lerp(Color.Lerp(cc[0], cc[1], u), Color.Lerp(cc[2], cc[3], u), v);
                    else
                    {
                        float s = radial
                            ? Mathf.Sqrt((u - cx) * (u - cx) + (v - cy) * (v - cy)) / Mathf.Max(0.001f, maxD)
                            : ((u - 0.5f) * dx + (v - 0.5f) * dy) / len + 0.5f;
                        c = Sample(stops, s);
                    }
                    if (g.Grain > 0f)
                    {
                        // GG-T404's mottle, on the same field it had on the old 96×128 texture: a broad mottle and a finer fleck.
                        float nx = u * 96f, ny = (1f - v) * 128f;
                        float n = (Mathf.PerlinNoise(nx * 0.09f + 3.1f, ny * 0.09f + 7.7f) - 0.5f) * 0.7f
                                + (Mathf.PerlinNoise(nx * 0.37f + 11.3f, ny * 0.37f + 1.9f) - 0.5f) * 0.3f;
                        float k = 1f + n * g.Grain * 2f;
                        c = new Color(c.r * k, c.g * k, c.b * k);
                    }
                    if (dark > 0f) c = Color.Lerp(c, Color.black, dark);
                    // Sub-LSB dither: a stretched gradient never shows 8-bit bands.
                    float d = (Hash(x, y, 7) - 0.5f) / 255f;
                    px[y * w + x] = new Color32(To8(c.r + d), To8(c.g + d), To8(c.b + d), 255);
                }
            }
        }

        private static byte To8(float f) => (byte)Mathf.Clamp(Mathf.RoundToInt(f * 255f), 0, 255);

        private static Color Sample((float at, Color c)[] stops, float s)
        {
            if (s <= stops[0].at) return stops[0].c;
            for (int i = 1; i < stops.Length; i++)
                if (s <= stops[i].at)
                {
                    float span = stops[i].at - stops[i - 1].at;
                    return span <= 0f ? stops[i].c : Color.Lerp(stops[i - 1].c, stops[i].c, (s - stops[i - 1].at) / span);
                }
            return stops[stops.Length - 1].c;
        }

        public static Texture2D NewTexture(int w, int h, string name, bool mips = false) =>
            new Texture2D(w, h, TextureFormat.RGBA32, mips)
            { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };

        /// <summary>Repaints <paramref name="tex"/> (any size) with <paramref name="t"/>'s gradient.</summary>
        public static void Paint(Texture2D tex, WindowThemeDef t, float darkTop = 0f, float darkBottom = 0f)
        {
            var px = new Color32[tex.width * tex.height];
            Bake(t, px, tex.width, tex.height, darkTop, darkBottom);
            tex.SetPixels32(px);
            tex.Apply();
        }

        private static readonly Dictionary<string, Texture2D> swatches = new Dictionary<string, Texture2D>();

        /// <summary>A small baked gradient of <paramref name="t"/> for the editor's preset tiles (cached by gradient).</summary>
        public static Texture2D Swatch(WindowThemeDef t)
        {
            string key = Newtonsoft.Json.JsonConvert.SerializeObject(t.Gradient, WindowThemeCatalog.Json);
            if (swatches.TryGetValue(key, out var tex) && tex != null) return tex;
            if (swatches.Count > 96)
            {
                foreach (var old in swatches.Values) if (old != null) UnityEngine.Object.Destroy(old);
                swatches.Clear();
            }
            tex = NewTexture(64, 64, "window-swatch");
            Paint(tex, t);
            return swatches[key] = tex;
        }

        // ── Texture overlays ────────────────────────────────────────────────────────

        /// <summary>Every texture id the editor offers: the procedural ones, then each PNG in Resources/UI/WindowTextures.</summary>
        public static IEnumerable<string> TextureIds =>
            WindowThemeCatalog.ProceduralTextures.Concat(Resources.LoadAll<Texture2D>(TextureFolder.TrimEnd('/'))
                .Select(t => WindowThemeCatalog.FilePrefix + t.name).Distinct().OrderBy(x => x));

        public static string TextureLabel(string id) =>
            string.IsNullOrEmpty(id) ? "None"
            : id.StartsWith(WindowThemeCatalog.FilePrefix) ? id.Substring(WindowThemeCatalog.FilePrefix.Length)
            : char.ToUpperInvariant(id[0]) + id.Substring(1);

        private static readonly Dictionary<string, Texture2D> tiles = new Dictionary<string, Texture2D>();

        /// <summary>The overlay for <paramref name="t"/>: its tile baked for the blend mode, the tile's size in design px,
        /// and the layer's opacity; null tex when the theme has no texture (or its PNG is missing).</summary>
        public static (Texture2D tex, float size, float opacity) Overlay(WindowThemeDef t)
        {
            var x = t?.Texture;
            if (x == null || string.IsNullOrEmpty(x.Id) || x.Opacity <= 0f) return (null, 0f, 0f);
            string blend = WindowThemeCatalog.Blends.Contains(x.Blend) ? x.Blend : "overlay";
            string bakeAs = blend == "soft-light" ? "overlay" : blend;
            string key = x.Id + "|" + bakeAs;
            if (!tiles.TryGetValue(key, out var tex))
            {
                tex = BakeTile(x.Id, bakeAs);
                tiles[key] = tex;
            }
            if (tex == null) return (null, 0f, 0f);
            float texel = TexelSize(x.Id);
            float op = Mathf.Clamp01(x.Opacity) * (blend == "soft-light" ? 0.5f : 1f);
            return (tex, tex.width * texel * Mathf.Clamp(x.Scale, 0.1f, 8f), op);
        }

        /// <summary>Design px per tile texel at scale 1: the natural size each pattern was drawn for.</summary>
        private static float TexelSize(string id)
        {
            switch (id)
            {
                case "marble": case "parchment": return 4f;
                case "brushed": case "stars": return 2f;
                case "linen": case "carbon": return 1.5f;
                default: return 1f;
            }
        }

        private static Texture2D BakeTile(string id, string blend)
        {
            float[] lum; float[] alpha = null; Color32[] src = null; int n;
            if (id.StartsWith(WindowThemeCatalog.FilePrefix))
            {
                var png = Resources.Load<Texture2D>(TextureFolder + id.Substring(WindowThemeCatalog.FilePrefix.Length));
                if (png == null) { Debug.LogWarning($"[WindowThemes] missing window texture '{id}'"); return null; }
                src = Readable(png);
                n = png.width;
                int hh = png.height;
                lum = new float[n * hh]; alpha = new float[n * hh];
                for (int i = 0; i < src.Length; i++)
                {
                    lum[i] = (0.299f * src[i].r + 0.587f * src[i].g + 0.114f * src[i].b) / 255f;
                    alpha[i] = src[i].a / 255f;
                }
                return Compose(lum, alpha, src, n, hh, blend, id);
            }
            n = 256;
            lum = Procedural(id, n);
            return Compose(lum, null, null, n, n, blend, id);
        }

        private static Texture2D Compose(float[] lum, float[] alpha, Color32[] src, int w, int h, string blend, string id)
        {
            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++)
            {
                float T = Mathf.Clamp01(lum[i]), a0 = alpha != null ? alpha[i] : 1f;
                switch (blend)
                {
                    case "multiply": px[i] = new Color32(0, 0, 0, To8((1f - T) * a0)); break;   // base × T
                    case "screen": px[i] = new Color32(255, 255, 255, To8(T * a0)); break;      // 1 − (1 − base)(1 − T)
                    case "normal":
                        px[i] = src != null ? new Color32(src[i].r, src[i].g, src[i].b, To8(a0)) : new Color32(To8(T), To8(T), To8(T), 255);
                        break;
                    default: // overlay: lighten above mid-gray, darken below
                        px[i] = T >= 0.5f ? new Color32(255, 255, 255, To8((2f * T - 1f) * a0)) : new Color32(0, 0, 0, To8((1f - 2f * T) * a0));
                        break;
                }
            }
            var tex = NewTexture(w, h, "window-tex-" + id + "-" + blend, mips: true);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }

        /// <summary>A PNG's pixels even when it wasn't imported Read/Write (no editor work): copied through the GPU.</summary>
        private static Color32[] Readable(Texture2D t)
        {
            if (t.isReadable) return t.GetPixels32();
            var rt = RenderTexture.GetTemporary(t.width, t.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var prev = RenderTexture.active;
            Graphics.Blit(t, rt);
            RenderTexture.active = rt;
            var copy = new Texture2D(t.width, t.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, t.width, t.height), 0, 0);
            copy.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            var px = copy.GetPixels32();
            UnityEngine.Object.Destroy(copy);
            return px;
        }

        // ── Procedural tiles (seamless; values 0..1, 0.5 = neutral for overlay) ──────

        private static float[] Procedural(string id, int n)
        {
            var T = new float[n * n];
            var rnd = new System.Random(id.GetHashCode() & 0x7fffffff);
            if (id == "stars")
            {
                for (int i = 0; i < T.Length; i++)
                {
                    int x = i % n, y = i / n;
                    float neb = Fbm(x / (float)n, y / (float)n, 3, 4, 41);
                    T[i] = Mathf.Max(0f, neb - 0.45f) * 0.5f;
                }
                for (int s = 0; s < 170; s++)
                {
                    float sx = (float)rnd.NextDouble() * n, sy = (float)rnd.NextDouble() * n;
                    float b = 0.45f + 0.55f * (float)Math.Pow(rnd.NextDouble(), 3), r = 0.6f + 1.2f * b;
                    for (int oy = -3; oy <= 3; oy++)
                        for (int ox = -3; ox <= 3; ox++)
                        {
                            int px = ((int)sx + ox + n) % n, py = ((int)sy + oy + n) % n;
                            float ddx = (int)sx + ox + 0.5f - sx, ddy = (int)sy + oy + 0.5f - sy;
                            float v = b * Mathf.Exp(-(ddx * ddx + ddy * ddy) / (r * r * 0.5f));
                            int k = py * n + px;
                            T[k] = Mathf.Max(T[k], v);
                        }
                }
                return T;
            }
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = x / (float)n, v = y / (float)n, t;
                    switch (id)
                    {
                        case "granite":
                        {
                            // Fine-grained stone (GG-T407 review: the first cut read as stretched camo blotches): a small
                            // crystal mottle, per-texel grain, and sparse light/dark flecks, drawn one texel per design px.
                            float f = Fbm(u, v, 32, 3, 3);
                            t = 0.5f + (f - 0.5f) * 0.7f + (Hash(x, y, 13) - 0.5f) * 0.3f;
                            float h = Hash(x, y, 11);
                            if (h > 0.97f) t += 0.25f; else if (h < 0.035f) t -= 0.25f;
                            break;
                        }
                        case "marble":
                        {
                            float f = Fbm(u, v, 4, 5, 5);
                            float vein = Mathf.Abs(Mathf.Sin(Mathf.PI * (u * 2f + v * 1f + f * 2.2f)));
                            t = 0.5f + 0.45f * Mathf.Pow(1f - vein, 9f) + (f - 0.5f) * 0.25f;
                            break;
                        }
                        case "parchment":
                        {
                            float blot = Fbm(u, v, 4, 4, 9);
                            float fib = Noise(u * 6f, v * 96f, 6, 96, 13);
                            t = 0.5f + (blot - 0.5f) * 0.9f + (fib - 0.5f) * 0.22f + (Hash(x, y, 17) - 0.5f) * 0.05f;
                            break;
                        }
                        case "linen":
                        {
                            bool over = (((x / 4) + (y / 4)) & 1) == 0;
                            float thread = over ? Mathf.Sin(Mathf.PI * (x % 4 + 0.5f) / 4f) : Mathf.Sin(Mathf.PI * (y % 4 + 0.5f) / 4f);
                            float slub = over ? Noise(u * 8f, v * 128f, 8, 128, 19) : Noise(u * 128f, v * 8f, 128, 8, 23);
                            t = 0.32f + 0.3f * thread + (slub - 0.5f) * 0.25f;
                            break;
                        }
                        case "brushed":
                        {
                            float s = Noise(u * 2f, v * 192f, 2, 192, 29) * 0.6f + Noise(u * 4f, v * 64f, 4, 64, 31) * 0.4f;
                            t = 0.5f + (s - 0.5f) * 0.9f + (Hash(x, y, 37) - 0.5f) * 0.06f;
                            break;
                        }
                        case "scanlines": t = (y % 8) < 3 ? 0.2f : 1f; break;
                        case "checker": t = (((x / 4) + (y / 4)) & 1) == 0 ? 0.35f : 0.65f; break;
                        case "noise": t = 0.5f + (Hash(x, y, 43) - 0.5f) * 0.8f; break;
                        case "carbon":
                        {
                            int cx = x / 8, cy = y / 8;
                            bool horiz = ((cx + cy) & 1) == 0;
                            float along = horiz ? (y % 8 + 0.5f) / 8f : (x % 8 + 0.5f) / 8f;
                            t = 0.22f + 0.55f * Mathf.Sin(Mathf.PI * along) * (horiz ? 1f : 0.7f);
                            break;
                        }
                        default: t = 0.5f; break;
                    }
                    T[y * n + x] = Mathf.Clamp01(t);
                }
            return T;
        }

        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        /// <summary>Value noise on a lattice that wraps every (px, py) cells, so a tile repeats with no seam.</summary>
        private static float Noise(float x, float y, int px, int py, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
            int ax = ((x0 % px) + px) % px, bx = (ax + 1) % px, ay = ((y0 % py) + py) % py, by = (ay + 1) % py;
            float a = Hash(ax, ay, seed), b = Hash(bx, ay, seed), c = Hash(ax, by, seed), d = Hash(bx, by, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        private static float Fbm(float u, float v, int freq, int octaves, int seed)
        {
            float sum = 0f, norm = 0f, amp = 0.5f;
            for (int o = 0; o < octaves; o++, freq *= 2, amp *= 0.5f)
            {
                sum += amp * Noise(u * freq, v * freq, freq, freq, seed + o * 101);
                norm += amp;
            }
            return sum / norm;
        }

        // ── Dressing windows ────────────────────────────────────────────────────────

        /// <summary>Puts the active theme's texture layer in <paramref name="window"/> (first child, absolute, inset to the
        /// border, the frame's inner radius, picking ignored) or takes it out when the theme has none.</summary>
        public static void Dress(VisualElement window) => DressWith(window, Current);

        public static void DressWith(VisualElement window, WindowThemeDef theme)
        {
            if (window == null) return;
            VisualElement layer = null;
            for (int i = 0; i < window.hierarchy.childCount; i++)
                if (window.hierarchy[i].ClassListContains(OverlayClass)) { layer = window.hierarchy[i]; break; }
            var (tex, size, opacity) = Overlay(theme);
            if (tex == null) { layer?.RemoveFromHierarchy(); return; }
            if (layer == null)
            {
                layer = new VisualElement { name = OverlayClass, pickingMode = PickingMode.Ignore };
                layer.AddToClassList(OverlayClass);
                layer.style.position = Position.Absolute;
                layer.style.left = layer.style.top = layer.style.right = layer.style.bottom = 0;
                layer.RegisterCallback<GeometryChangedEvent>(_ => SyncRadius(layer));
                window.hierarchy.Insert(0, layer);
            }
            else if (window.hierarchy.IndexOf(layer) != 0) { layer.RemoveFromHierarchy(); window.hierarchy.Insert(0, layer); }
            layer.style.backgroundImage = new StyleBackground(tex);
            layer.style.backgroundRepeat = new BackgroundRepeat(Repeat.Repeat, Repeat.Repeat);
            layer.style.backgroundSize = new BackgroundSize(size, size);
            layer.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Left);
            layer.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Top);
            layer.style.opacity = opacity;
            SyncRadius(layer);
            layer.schedule.Execute(() => SyncRadius(layer)).ExecuteLater(1);
        }

        /// <summary>The layer's corners follow the frame's inner curve (outer radius less the border).</summary>
        private static void SyncRadius(VisualElement layer)
        {
            var w = layer.hierarchy.parent;
            if (w == null) return;
            var s = w.resolvedStyle;
            layer.style.borderTopLeftRadius = Mathf.Max(0f, s.borderTopLeftRadius - Mathf.Max(s.borderTopWidth, s.borderLeftWidth));
            layer.style.borderTopRightRadius = Mathf.Max(0f, s.borderTopRightRadius - Mathf.Max(s.borderTopWidth, s.borderRightWidth));
            layer.style.borderBottomLeftRadius = Mathf.Max(0f, s.borderBottomLeftRadius - Mathf.Max(s.borderBottomWidth, s.borderLeftWidth));
            layer.style.borderBottomRightRadius = Mathf.Max(0f, s.borderBottomRightRadius - Mathf.Max(s.borderBottomWidth, s.borderRightWidth));
        }

        /// <summary>Dresses every <c>.window</c> under <paramref name="root"/>.</summary>
        public static void DressAll(VisualElement root) => root?.Query(className: "window").ForEach(Dress);
    }
}
