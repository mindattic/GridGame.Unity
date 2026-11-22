using System.Linq;
using System.Collections.Generic;
using GridGame.Core.Content;
using GridGame.Meta;
using UnityEngine;

namespace GridGame.App
{
    /// <summary>
    /// Every texture the game draws: imported art by key (Resources) plus procedurally generated UI
    /// pieces (window gradient, cursor, orbs). Missing art never throws — it falls back to a
    /// generated placeholder and logs once, so a gap is visible instead of fatal.
    /// </summary>
    public static class Art
    {
        private static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
        private static readonly HashSet<string> warned = new HashSet<string>();

        public static Texture2D Load(string path)
        {
            if (string.IsNullOrEmpty(path)) return Placeholder;
            if (cache.TryGetValue(path, out var t) && t != null) return t;
            t = Resources.Load<Texture2D>(path);
            if (t == null)
            {
                if (warned.Add(path)) Debug.LogWarning($"[Art] missing texture '{path}'");
                t = Placeholder;
            }
            cache[path] = t;
            return t;
        }

        public static bool Exists(string path) => Resources.Load<Texture2D>(path) != null;

        /// <summary>Textures held by path (the `-leakprobe` line, GG-T375): bounded by the art on disk, never per battle.</summary>
        internal static int CachedTextures => cache.Count;

        private static readonly Dictionary<string, int> spellIndex = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);

        /// <summary>Called once content loads: every ability gets an ordinal, so every spell gets its own colour.</summary>
        public static void IndexSpells(IEnumerable<string> ids)
        {
            spellIndex.Clear();
            foreach (var id in ids.OrderBy(x => x, System.StringComparer.OrdinalIgnoreCase)) spellIndex[id] = spellIndex.Count;
        }

        /// <summary>Each spell's own colour (golden-ratio hue walk over the ability list) — its countdown ring and timeline frame.</summary>
        public static Color SpellColor(string abilityId)
        {
            int i = abilityId != null && spellIndex.TryGetValue(abilityId, out var k) ? k : 0;
            float hue = (0.03f + i * 0.61803398f) % 1f;
            return Color.HSVToRGB(hue, 0.72f, 1f);
        }

        /// <summary>A unit's one piece of art (full portrait). Every thumbnail is this texture framed by the class's
        /// Thumbnail (Ui.PortraitThumb, BIBLE §12.1.1); there are no separate thumb images.
        /// A class with no portrait yet shows the mannequin placeholder art (owner 2026-11-07, GG-T351).
        /// <para>GG-T532: the one resolver for hero variants. <paramref name="classId"/> reads as the version the player picked
        /// (<see cref="HeroLooks.Of"/>: an owned hero's <c>Variant</c>, or the look a wanderer on the board was given), so
        /// every view that draws a unit by class id (thumbnails, figures, cut-ins, dialogue, cards, level-up) shows the right
        /// art without knowing about variants. GG-T557: the file is the class's own data (<see cref="ClassDef.PortraitFile"/>:
        /// <c>Portrait</c>/<c>MalePortrait</c> stems + <c>PortraitVariant</c>, <c>acolyte-female-00</c>); a class without male
        /// art shows its default art.</para></summary>
        public static Texture2D Portrait(string classId) => Portrait(classId, HeroLooks.Of(classId));

        /// <summary>The portrait of <paramref name="classId"/> as <paramref name="v"/>, whoever owns it (the variant picker);
        /// <paramref name="variant"/> picks a numbered alternate (a costume), default the class's own.</summary>
        public static Texture2D Portrait(string classId, HeroVariant v, int? variant = null)
        {
            var def = Class(classId);
            bool male = v == HeroVariant.Male && HasMaleArt(classId);
            return PortraitByFile(def?.PortraitFile(male, variant), def, male);
        }

        /// <summary>Whether <paramref name="classId"/> is a class whose portrait file exists (dialogue, the death dissolve).</summary>
        public static bool HasPortrait(string classId)
        {
            var def = Class(classId);
            return def != null && Exists(PortraitFolder + def.PortraitFile());
        }

        private static ClassDef Class(string classId) =>
            string.IsNullOrEmpty(classId) || GameApp.I == null ? null : GameApp.I.Content?.TryClass(classId);

        /// <summary>The Resources folder of every portrait.</summary>
        public const string PortraitFolder = "Portraits/";

        private static readonly Dictionary<string, bool> maleArt = new Dictionary<string, bool>(System.StringComparer.OrdinalIgnoreCase);

        /// <summary>Whether the class has male art (its <c>MalePortrait</c> is set and the file exists, GG-T532/GG-T557).
        /// The variant picker skips a class without it and the hero stays female.</summary>
        public static bool HasMaleArt(string classId)
        {
            if (string.IsNullOrEmpty(classId)) return false;
            if (maleArt.TryGetValue(classId, out var has)) return has;
            var def = Class(classId);
            if (def == null) return false; // content not loaded yet: ask again later
            has = false;
            if (def.HasMalePortrait)
            {
                string path = PortraitFolder + def.PortraitFile(true);
                var t = Resources.Load<Texture2D>(path);
                has = t != null;
                if (has) { cache[path] = t; owners[t] = (def, true); } // it's drawn soon anyway
            }
            return maleArt[classId] = has;
        }

        /// <summary>The thumbnail framing for <paramref name="classId"/> drawn as <paramref name="tex"/>: the class's
        /// <c>MaleThumbnail</c> on its male art (else its Thumbnail), its <c>Thumbnail</c> otherwise.</summary>
        public static Thumbnail ThumbnailFor(string classId, Texture2D tex)
        {
            var def = Class(classId);
            if (def == null) return null;
            return tex != null && owners.TryGetValue(tex, out var o) && o.male ? def.MaleThumbnail ?? def.Thumbnail : def.Thumbnail;
        }

        /// <summary>Which class (and which of its versions) a loaded portrait texture is: its figure box and framing come
        /// from that class. A file two classes share (Monster Tamer draws the Wild Child) keeps the first that loaded it.</summary>
        private static readonly Dictionary<Texture2D, (ClassDef def, bool male)> owners = new Dictionary<Texture2D, (ClassDef, bool)>();

        private static Texture2D PortraitByFile(string file, ClassDef def, bool male)
        {
            string path = PortraitFolder + file;
            if (cache.TryGetValue(path, out var t) && t != null) return t;
            t = string.IsNullOrEmpty(file) ? null : Resources.Load<Texture2D>(path);
            if (t == null)
            {
                if (warned.Add(path)) Debug.LogWarning($"[Art] missing portrait '{path}', using the mannequin");
                t = Load(PortraitFolder + MannequinPortrait);
                def = Class(MannequinClass); male = false;
            }
            if (t != null && def != null && !owners.ContainsKey(t)) owners[t] = (def, male);
            return cache[path] = t;
        }

        /// <summary>The placeholder class, and its portrait file every class without art falls back to.</summary>
        public const string MannequinClass = "mannequin";
        public static readonly string MannequinPortrait = AssetNames.Of(MannequinClass);

        // ── The figure on a portrait (GG-T352) ─────────────────────────────────────
        // Owner 2026-11-07: "all characters are 1024x1024 with transparent backgrounds; that's the rule, that's what the
        // assets are." Thumbnails frame that whole canvas (Ui.PortraitThumb). Whole-figure views draw only the figure's
        // box on it (ClassDef.Figure, the alpha bounds GridGame.Studio/tools/import_art.py writes), so a figure reads at the same size it
        // always did instead of shrinking into its canvas margins.

        /// <summary>The size whole-figure views measure a figure by: its box fitted inside this square, never enlarged
        /// (the size portraits were imported at before GG-T352, so a creature smaller than a person still draws smaller).</summary>
        public const float FigureBox = 640f;

        private static readonly Dictionary<Texture2D, Sprite> figureSprites = new Dictionary<Texture2D, Sprite>();

        /// <summary>The figure's box on a portrait texture, in texture pixels from the bottom-left (Unity's origin). It is
        /// the box of the class the texture belongs to, so the mannequin fallback (GG-T351) uses the mannequin's own box;
        /// a texture with no valid box is used whole.</summary>
        private static Rect FigureRect(Texture2D tex)
        {
            // GG-T532: a male portrait uses its class's MaleFigure, else the class's own Figure.
            ClassDef def = null; bool male = false;
            if (owners.TryGetValue(tex, out var o)) { def = o.def; male = o.male; }
            var f = def == null ? null : male ? def.MaleFigure ?? def.Figure : def.Figure;
            if (f == null || !f.IsValid) return new Rect(0, 0, tex.width, tex.height);
            float x0 = Mathf.Round(f.X * tex.width), x1 = Mathf.Min(tex.width, Mathf.Round((f.X + f.W) * tex.width));
            float y0 = Mathf.Round(f.Y * tex.height), y1 = Mathf.Min(tex.height, Mathf.Round((f.Y + f.H) * tex.height));
            if (x1 - x0 < 1f || y1 - y0 < 1f) return new Rect(0, 0, tex.width, tex.height);
            return new Rect(x0, tex.height - y1, x1 - x0, y1 - y0);
        }

        /// <summary>A class's figure (its portrait cut to <see cref="ClassDef.Figure"/>) as a sprite for a
        /// <c>backgroundImage</c>: every whole-portrait view (dialogue, cut-ins, boss intro, level-up, cards, the death
        /// dissolve) draws this instead of the raw texture. Cached per texture; null only when there is no art at all.</summary>
        public static Sprite PortraitFigure(string classId) => FigureOf(Portrait(classId));

        /// <summary>The figure of <paramref name="classId"/> as <paramref name="v"/> (the variant picker, GG-T532).</summary>
        public static Sprite PortraitFigure(string classId, HeroVariant v) => FigureOf(Portrait(classId, v));

        private static Sprite FigureOf(Texture2D tex)
        {
            if (tex == null) return null;
            if (figureSprites.TryGetValue(tex, out var s) && s != null) return s;
            s = Sprite.Create(tex, FigureRect(tex), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            s.name = tex.name + ".figure";
            return figureSprites[tex] = s;
        }

        /// <summary>The figure's layout size in <see cref="FigureBox"/> units: its box fitted inside a 640 square, never
        /// enlarged. Width ÷ height is the figure's aspect; dialogue scales the height (640 = one person).</summary>
        public static Vector2 FigureSize(string classId)
        {
            var tex = Portrait(classId);
            if (tex == null) return new Vector2(FigureBox * 0.7f, FigureBox);
            var r = FigureRect(tex);
            // Measured on the 1024 canvas the box was found on, whatever size the texture was imported at.
            float px = 1024f / Mathf.Max(1, tex.width), w = r.width * px, h = r.height * px;
            float k = Mathf.Min(1f, FigureBox / Mathf.Max(w, h));
            return new Vector2(w * k, h * k);
        }
        /// <summary>The figure's real height in texture pixels: the most it can be drawn without upscaling (POL-H4).</summary>
        public static float FigureNativeHeight(string classId)
        {
            var tex = Portrait(classId);
            return tex == null ? float.MaxValue : FigureRect(tex).height;
        }

        /// <summary>A pixel icon under <paramref name="folder"/> by id, named by <see cref="AssetNames"/> (GG-T561); null when none.</summary>
        private static Texture2D Pixelled(string folder, string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            string path = AssetNames.Path(folder, id); // GG-T561: <folder>/<id words>-00
            if (cache.TryGetValue(path, out var t)) return t;
            return cache[path] = Resources.Load<Texture2D>(path);
        }

        /// <summary>An ability's pixel icon (AbilityIcons/&lt;id&gt;-00, GG-T561), else its legacy icon, else null.</summary>
        public static Texture2D AbilityIcon(AbilityDef a) => a == null ? null : Pixelled("AbilityIcons", a.Id) ?? (string.IsNullOrEmpty(a.Icon) ? null : Icon(a.Icon));

        /// <summary>An item's pixel icon (ItemIcons/&lt;id&gt;-00, GG-T561), or null.</summary>
        public static Texture2D ItemIcon(string itemId) => Pixelled("ItemIcons", itemId);

        /// <summary>Pixel-art status icon (tools/make_status_icons.py) for a buff id or "stunned"; null when none exists.</summary>
        public static Texture2D StatusIcon(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            string path = AssetNames.Path("StatusIcons", id);
            if (cache.TryGetValue(path, out var t)) return t;
            return cache[path] = Resources.Load<Texture2D>(path);
        }

        /// <summary>A theme's board tile: <c>Tiles/&lt;theme&gt;-00</c>, else its base theme's (a postgame theme with its own
        /// stages, like <c>greenvalley2</c>, keeps the moors' tile, GG-T346), else the plain tile.</summary>
        public static Texture2D BoardTile(string theme)
        {
            foreach (var t in new[] { theme, Backdrops.BaseTheme(theme) })
                if (!string.IsNullOrEmpty(t) && Exists(AssetNames.Path("Tiles", t))) return Load(AssetNames.Path("Tiles", t));
            return Load(AssetNames.Path("Tiles", "tile"));
        }
        /// <summary>A UI-group pixel icon (tools/icons.json "ui": cursor, pickups), or null.</summary>
        public static Texture2D UiIcon(string id) => Pixelled("UI", id);
        public static Texture2D Icon(string key) => string.IsNullOrEmpty(key) ? null : Load(AssetNames.Path("Icons", key));

        private static readonly Dictionary<string, BackdropSet> backdropSets = new Dictionary<string, BackdropSet>(System.StringComparer.OrdinalIgnoreCase);

        /// <summary>A background set as found in Resources (GG-T344: counted once, from <c>-00</c>, GG-T561).
        /// GG-T353: each probed image is unloaded again (a full-size backdrop is ~4.5 MB of texture), so counting a set
        /// doesn't keep all of it resident; the one shown is loaded by <see cref="Background"/>.</summary>
        public static BackdropSet BackgroundSet(string set)
        {
            if (string.IsNullOrEmpty(set)) return new BackdropSet(set, 0);
            if (!backdropSets.TryGetValue(set, out var s))
                backdropSets[set] = s = Backdrops.Probe(set, ProbeBackground);
            return s;
        }

        private static bool ProbeBackground(string key)
        {
            var t = Resources.Load<Texture2D>(key);
            if (t == null) return false;
            if (!cache.ContainsKey(key)) Resources.UnloadAsset(t);
            return true;
        }

        /// <summary>A theme's <paramref name="index"/>th backdrop from its campaign <c>Backgrounds</c> set (else the theme id),
        /// wrapped over the set's own count; a postgame theme with no art of its own borrows its base theme's
        /// (<see cref="Backdrops.Resolve"/>).</summary>
        public static Texture2D Background(string theme, int index = 0)
        {
            var content = GameApp.I != null ? GameApp.I.Content : null;
            var t = Resources.Load<Texture2D>(Backdrops.Resolve(theme, index, BackgroundSet, x => Backdrops.SetOf(content, x)));
            return t != null ? t : Load(Backdrops.Fallback);
        }

        // ── Generated pieces ────────────────────────────────────────────────────────

        private static Texture2D placeholder, window, windowDark, cursor, glow, orb;

        public static Texture2D Placeholder => placeholder ??= Solid(new Color(0.35f, 0.2f, 0.45f), "placeholder");

        /// <summary>The window body: the active theme's gradient (GG-T407, <see cref="WindowThemes"/>), baked at
        /// <see cref="WindowThemes.TexSize"/>² and stretched by each window; its texture overlay, frame and accent are
        /// applied by <c>Ui.Window</c> / <c>Ui.ApplyMenuTheme</c>.</summary>
        public static Texture2D Window => window ??= BakeWindow("window", 0f, 0f);
        /// <summary>The same gradient darkened (30 % at the top to 55 % at the bottom).</summary>
        public static Texture2D WindowDark => windowDark ??= BakeWindow("window-dark", 0.3f, 0.55f);

        private static Texture2D BakeWindow(string name, float darkTop, float darkBottom)
        {
            var t = WindowThemes.NewTexture(WindowThemes.TexSize, WindowThemes.TexSize, name);
            WindowThemes.Paint(t, WindowThemes.Current, darkTop, darkBottom);
            return t;
        }

        // ── Menu themes (owner 2026-10-11: "make multiple Themes ... updated in Settings and remembers it"; GG-T407:
        // "make the gradient totally editable ... many different themes to choose from") ─────────────────────────────
        // A theme is data now: Resources/UI/themes.json presets plus the profile's own (SettingsSave.CustomThemes), baked
        // by App/UI/WindowThemes.cs. These are the few things the rest of the game reads.

        /// <summary>The window border / highlight colour: the theme's own accent, else derived from its gradient by colour
        /// theory (the complement of the ends' hue midpoint, GG-T185).</summary>
        public static Color ThemeAccent => WindowThemes.Accent;
        /// <summary>The active theme's root USS class (its frame style's Theme.uss block), or null for Classic.</summary>
        public static string ThemeUssClass => WindowThemes.UssClass;
        public static string ThemeName => WindowThemes.Current.Name;

        /// <summary>Switches the active theme by name (a preset or a custom theme; an unknown name falls back to the default, Classic),
        /// regenerating any already-created window textures in place (same Texture2D instance, so every
        /// <c>VisualElement</c> already pointing at it updates with no rebuild). Call <c>Ui.ApplyMenuTheme</c> after.</summary>
        public static void ApplyTheme(string name) => ApplyTheme(WindowThemes.Resolve(name));

        /// <summary>Switches to <paramref name="theme"/> itself (the Window Editor's live draft).</summary>
        public static void ApplyTheme(GridGame.Meta.WindowThemeDef theme)
        {
            WindowThemes.SetCurrent(theme);
            if (window != null) WindowThemes.Paint(window, WindowThemes.Current);
            if (windowDark != null) WindowThemes.Paint(windowDark, WindowThemes.Current, 0.3f, 0.55f);
        }

        /// <summary>The pointing-glove cursor (GridGame.Studio/tools/make_icons.py, UI/cursor-00); the generated one if it's missing.</summary>
        public static Texture2D Cursor => cursor ??= (Resources.Load<Texture2D>(AssetNames.Path("UI", "cursor")) ?? MakeCursor());
        public static Texture2D Glow => glow ??= MakeGlow();
        public static Texture2D Orb => orb ??= MakeOrb();

        private static Texture2D NewTex(int w, int h, string name)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
        }

        private static Texture2D Solid(Color c, string name)
        {
            var t = NewTex(4, 4, name);
            var px = new Color[16];
            for (int i = 0; i < px.Length; i++) px[i] = c;
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        private static Texture2D MakeCursor()
        {
            // A white right-pointing gauntlet-ish arrow with a dark outline, like the classic menu hand.
            const int s = 64;
            var t = NewTex(s, s, "cursor");
            var clear = new Color(0, 0, 0, 0);
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float fx = x / (float)s, fy = y / (float)s;
                    // arrow head
                    float head = fx > 0.45f ? (1f - fx) * 1.8f : 0f;
                    bool inHead = fx > 0.45f && Mathf.Abs(fy - 0.5f) < head * 0.5f;
                    bool inShaft = fx >= 0.08f && fx <= 0.5f && Mathf.Abs(fy - 0.5f) < 0.13f;
                    bool inCuff = fx >= 0.04f && fx <= 0.16f && Mathf.Abs(fy - 0.5f) < 0.22f;
                    bool inside = inHead || inShaft || inCuff;
                    bool edge = false;
                    if (!inside)
                    {
                        for (int k = 0; k < 8 && !edge; k++)
                        {
                            float ox = fx + Mathf.Cos(k * Mathf.PI / 4f) * 0.04f, oy = fy + Mathf.Sin(k * Mathf.PI / 4f) * 0.04f;
                            float h2 = ox > 0.45f ? (1f - ox) * 1.8f : 0f;
                            edge = (ox > 0.45f && Mathf.Abs(oy - 0.5f) < h2 * 0.5f) || (ox >= 0.08f && ox <= 0.5f && Mathf.Abs(oy - 0.5f) < 0.13f) || (ox >= 0.04f && ox <= 0.16f && Mathf.Abs(oy - 0.5f) < 0.22f);
                        }
                    }
                    var shade = Mathf.Lerp(1f, 0.78f, fy);
                    t.SetPixel(x, y, inside ? new Color(shade, shade, 1f, 1f) : edge ? new Color(0.05f, 0.05f, 0.15f, 1f) : clear);
                }
            t.Apply();
            return t;
        }

        /// <summary>GLSL-style smoothstep (Unity's Mathf.SmoothStep is an interpolator, not an edge function).</summary>
        private static float Edge(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        private static Texture2D MakeGlow()
        {
            const int s = 64;
            var t = NewTex(s, s, "glow");
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = (x - s / 2f + 0.5f) / (s / 2f), dy = (y - s / 2f + 0.5f) / (s / 2f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    // Soft ring that stays bright out to the frame edge (≈0.75) then falls off.
                    float a = d < 0.7f ? 1f : 1f - Edge(0.7f, 1f, d);
                    t.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            t.Apply();
            return t;
        }

        private static Texture2D MakeOrb()
        {
            const int s = 64;
            var t = NewTex(s, s, "orb");
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = (x - s / 2f + 0.5f) / (s / 2f), dy = (y - s / 2f + 0.5f) / (s / 2f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > 1f) { t.SetPixel(x, y, new Color(0, 0, 0, 0)); continue; }
                    float hl = Mathf.Clamp01(1f - Mathf.Sqrt((dx + 0.35f) * (dx + 0.35f) + (dy - 0.35f) * (dy - 0.35f)) * 1.6f);
                    float rim = Edge(0.8f, 1f, d);
                    float v = Mathf.Lerp(0.85f, 0.55f, rim) + hl * 0.5f;
                    float a = 1f - Edge(0.94f, 1f, d);
                    t.SetPixel(x, y, new Color(v, v, v, a));
                }
            t.Apply();
            return t;
        }

        /// <summary>The one mana orb color (generic mana, AMENDMENTS 2026-10-10).</summary>
        public static readonly Color Mana = new Color(0.42f, 0.78f, 1f);

        public static Color HexColor(string hex, Color fallback)
        {
            if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex.StartsWith("#") ? hex : "#" + hex, out var c)) return c;
            return fallback;
        }
    }
}
