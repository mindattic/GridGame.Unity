using System.Linq;
using System.Collections.Generic;
using GridGame.Core.Content;
using GridGame.Meta;
using UnityEngine;

namespace GridGame.App
{
    /// <summary>How long a cached texture lives (GG-T1203): <see cref="Art.BeginScope"/> tags every load made under it,
    /// <see cref="Art.ReleaseScope"/> drops them. Ordered most durable first.</summary>
    public enum ArtScope
    {
        /// <summary>Kept for the whole run (the UI chrome, the mannequin).</summary>
        Resident = 0,
        /// <summary>Kept for one stage or battle (its board, units, portraits).</summary>
        Stage = 1,
        /// <summary>Kept while one screen is open.</summary>
        Screen = 2,
    }

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
            if (cache.TryGetValue(path, out var t) && t != null) { Touch(path); return t; }
            t = Resources.Load<Texture2D>(path);
            if (t == null)
            {
                if (warned.Add(path)) Debug.LogWarning($"[Art] missing texture '{path}'");
                t = Placeholder;
            }
            Store(path, t);
            return t;
        }

        public static bool Exists(string path) => Resources.Load<Texture2D>(path) != null;

        /// <summary>Textures held by path (the `-leakprobe` line, GG-T375): bounded by the art on disk, never per battle.</summary>
        internal static int CachedTextures => cache.Count;

        // GG-T1193: the cache is an LRU with a cap. Evicting only drops our references (the cache, owner and figure-sprite
        // entries); Unity unloads a texture once nothing else holds it (Resources.UnloadUnusedAssets), so a texture still
        // drawn by a window or the board keeps living and is simply re-found by Resources.Load when asked for again.
        internal const int CacheCap = 400, CacheTrimTo = 320;
        private static readonly Dictionary<string, long> lastUsed = new Dictionary<string, long>();
        private static long useTick;

        private static void Touch(string path) { lastUsed[path] = ++useTick; Tag(path); }

        private static Texture2D Store(string path, Texture2D t)
        {
            cache[path] = t;
            Touch(path);
            if (cache.Count > CacheCap) TrimCache();
            return t;
        }

        /// <summary>The LRU backstop (GG-T1193): drops the least recently used unscoped entries first, then (only if scoped
        /// loads alone overflow the cap) the oldest Screen/Stage ones; Resident entries are never trimmed.</summary>
        private static void TrimCache()
        {
            var order = new List<KeyValuePair<string, long>>(cache.Count);
            foreach (var k in cache.Keys)
            {
                bool scoped = scopes.TryGetValue(k, out var sc);
                if (scoped && sc == ArtScope.Resident) continue;
                long u = lastUsed.TryGetValue(k, out var lu) ? lu : 0;
                order.Add(new KeyValuePair<string, long>(k, scoped ? u + long.MaxValue / 2 : u)); // unscoped sort first
            }
            order.Sort((a, b) => a.Value.CompareTo(b.Value));
            int drop = Mathf.Min(order.Count, cache.Count - CacheTrimTo);
            var dropped = new List<Texture2D>(drop);
            for (int i = 0; i < drop; i++)
            {
                string k = order[i].Key;
                if (cache.TryGetValue(k, out var tex) && tex != null) dropped.Add(tex);
                cache.Remove(k);
                lastUsed.Remove(k);
                scopes.Remove(k);
            }
            ForgetTextures(dropped);
        }

        // ── Texture scopes (GG-T1203) ──────────────────────────────────────────────
        // Every cached entry (a texture by path, a thumbnail, a backdrop set, and through its texture the owner and
        // figure-sprite entries) is tagged with the scope it was loaded under: the most durable scope that has asked for
        // it wins (Resident > Stage > Screen), so a Screen load of a texture a Stage already holds stays the Stage's.
        // ReleaseScope drops that scope's references and destroys the Sprites made for its textures; ReleaseAndCollect
        // then lets Unity unload whatever nothing else draws. A load made with no scope begun stays unscoped and is
        // bounded only by the LRU cap above.

        private static readonly Dictionary<string, ArtScope> scopes = new Dictionary<string, ArtScope>();
        private static readonly bool[] activeScopes = new bool[3];
        private const string ThumbKey = "thumb:", BackdropSetKey = "backdropset:";

        /// <summary>The scope new loads are tagged with: the narrowest begun scope (Screen, else Stage, else Resident),
        /// or null when none is begun (unscoped: LRU-bounded only).</summary>
        public static ArtScope? CurrentScope
        {
            get
            {
                for (int i = activeScopes.Length - 1; i >= 0; i--) if (activeScopes[i]) return (ArtScope)i;
                return null;
            }
        }

        /// <summary>Entries tagged with <paramref name="scope"/> (the leak probe and tests).</summary>
        internal static int CountInScope(ArtScope scope)
        {
            int n = 0;
            foreach (var s in scopes.Values) if (s == scope) n++;
            return n;
        }

        /// <summary>Starts <paramref name="scope"/>: whatever it held from its previous begin is released first (a new
        /// stage drops the last stage's art), then loads are tagged with it until it is released or a narrower scope begins.</summary>
        public static void BeginScope(ArtScope scope)
        {
            ReleaseScope(scope);
            activeScopes[(int)scope] = true;
        }

        /// <summary>Ends <paramref name="scope"/>: drops every cache, thumbnail, owner, figure-sprite and backdrop-set
        /// entry tagged with it and destroys those Sprites. The textures themselves are Unity's to unload
        /// (<see cref="ReleaseAndCollect"/>), so one still drawn elsewhere keeps living and is re-found on its next load.</summary>
        public static void ReleaseScope(ArtScope scope)
        {
            activeScopes[(int)scope] = false;
            var keys = new List<string>();
            foreach (var kv in scopes) if (kv.Value == scope) keys.Add(kv.Key);
            if (keys.Count == 0) return;
            var dropped = new List<Texture2D>();
            foreach (var k in keys)
            {
                scopes.Remove(k);
                if (k.StartsWith(ThumbKey, System.StringComparison.Ordinal))
                {
                    string file = k.Substring(ThumbKey.Length);
                    if (thumbs.TryGetValue(file, out var th) && th != null) dropped.Add(th);
                    thumbs.Remove(file);
                }
                else if (k.StartsWith(BackdropSetKey, System.StringComparison.Ordinal))
                    backdropSets.Remove(k.Substring(BackdropSetKey.Length));
                else
                {
                    if (cache.TryGetValue(k, out var tex) && tex != null) dropped.Add(tex);
                    cache.Remove(k);
                    lastUsed.Remove(k);
                }
            }
            ForgetTextures(dropped);
        }

        /// <summary>Coroutine-friendly: optionally releases <paramref name="release"/> first, then unloads every asset
        /// nothing references (Resources.UnloadUnusedAssets) and runs a full GC. <c>yield return Art.ReleaseAndCollect();</c></summary>
        public static System.Collections.IEnumerator ReleaseAndCollect(params ArtScope[] release)
        {
            if (release != null) foreach (var s in release) ReleaseScope(s);
            yield return Resources.UnloadUnusedAssets();
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            System.GC.Collect();
        }

        /// <summary>GG-T1204: loads every texture of <paramref name="set"/> into <see cref="ArtScope.Stage"/> through the
        /// same calls the battle draws with (portraits, figures, thumbnails, ability, item and status icons, the board tile
        /// and the backdrop set), so the keys are exactly Art's. Begin the Stage scope first (that releases the last
        /// stage's art); the scope is restored to what it was afterwards.</summary>
        public static void Preload(StageTextureSet set)
        {
            if (set == null) return;
            bool wasStage = activeScopes[(int)ArtScope.Stage], wasScreen = activeScopes[(int)ArtScope.Screen];
            activeScopes[(int)ArtScope.Stage] = true;
            activeScopes[(int)ArtScope.Screen] = false; // tag as Stage even if a screen is open
            try
            {
                var content = GameApp.I != null ? GameApp.I.Content : null;
                foreach (var c in set.Classes) { Portrait(c); PortraitFigure(c); Thumb(c); }
                foreach (var a in set.Abilities) { var def = content?.TryAbility(a); if (def != null) AbilityIcon(def); }
                foreach (var i in set.Items) ItemIcon(i);
                foreach (var s in set.Statuses) StatusIcon(s);
                if (!string.IsNullOrEmpty(set.Theme))
                {
                    BoardTile(set.Theme);
                    var key = BackgroundKey(set.Theme);
                    if (!string.IsNullOrEmpty(key) && Exists(key)) Load(key);
                }
            }
            finally
            {
                activeScopes[(int)ArtScope.Stage] = wasStage;
                activeScopes[(int)ArtScope.Screen] = wasScreen;
            }
        }

        /// <summary>Tags <paramref name="key"/> with the current scope, promoting it if that scope is more durable.</summary>
        private static void Tag(string key)
        {
            var cur = CurrentScope;
            if (cur == null) return;
            if (!scopes.TryGetValue(key, out var had) || cur.Value < had) scopes[key] = cur.Value;
        }

        /// <summary>Drops the per-texture entries (owner, figure sprite) of textures no remaining cache or thumbnail entry
        /// holds, destroying their Sprites. Generated and Resources textures are never destroyed here.</summary>
        private static void ForgetTextures(List<Texture2D> dropped)
        {
            if (dropped.Count == 0) return;
            var held = new HashSet<Texture2D>();
            foreach (var t in cache.Values) if (t != null) held.Add(t);
            foreach (var t in thumbs.Values) if (t != null) held.Add(t);
            foreach (var tex in dropped)
            {
                if (held.Contains(tex)) continue;
                owners.Remove(tex);
                if (figureSprites.TryGetValue(tex, out var s))
                {
                    figureSprites.Remove(tex);
                    if (s != null)
                    {
                        if (Application.isPlaying) Object.Destroy(s);
                        else Object.DestroyImmediate(s);
                    }
                }
            }
        }

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

        /// <summary>A unit's full portrait (1024 canvas). Small draws use its baked thumbnail instead (<see cref="Thumb(string)"/>,
        /// Ui.PortraitThumb, BIBLE §12.1.1, GG-T713); the portrait is for whole-figure and large views.
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
            if (def == null && IsOwnPortrait(classId)) return PortraitByFile(classId, null, false); // GG-T570: an NPC's own art
            bool male = v == HeroVariant.Male && HasMaleArt(classId);
            return PortraitByFile(def?.PortraitFile(male, variant), def, male);
        }

        /// <summary>The Resources folder of every baked thumbnail (tools/make_thumbs.py, GG-T719): one 256 px PNG per
        /// portrait, same file name, the class's head framing already cropped in, mipmapped.</summary>
        public const string ThumbFolder = "Thumbs/";

        private static readonly Dictionary<string, Texture2D> thumbs = new Dictionary<string, Texture2D>(System.StringComparer.OrdinalIgnoreCase);

        /// <summary>GG-T720: the baked thumbnail of the art <see cref="Portrait(string)"/> would draw for
        /// <paramref name="classId"/> (the picked variant's file, or an NPC's own Portraits/ file like <c>aetherist-00</c>),
        /// or null when that file has no thumbnail; the caller then falls back to the portrait through its Thumbnail
        /// framing (which also covers the mannequin). Every small actor draw goes through this (BIBLE §12.1.1).</summary>
        public static Texture2D Thumb(string classId) => Thumb(classId, HeroLooks.Of(classId));

        /// <summary>The thumbnail of <paramref name="classId"/> as <paramref name="v"/>; null when there is none.</summary>
        public static Texture2D Thumb(string classId, HeroVariant v)
        {
            if (string.IsNullOrEmpty(classId) || classId.StartsWith("$") || GameApp.I?.Content == null) return null;
            var def = Class(classId);
            // A class resolves its file as Portrait does; an id that is no class may name a Portraits/ file itself (GG-T570).
            string file = def != null ? def.PortraitFile(v == HeroVariant.Male && HasMaleArt(classId)) : classId;
            if (string.IsNullOrEmpty(file)) return null;
            Tag(ThumbKey + file);
            if (thumbs.TryGetValue(file, out var t)) return t;
            return thumbs[file] = Resources.Load<Texture2D>(ThumbFolder + file);
        }

        /// <summary>Whether <paramref name="classId"/> is a class whose portrait file exists (dialogue, the death dissolve),
        /// or (GG-T570) a Portraits/ file of its own that no class draws: a Camp NPC with art of its own, like the Aetherist
        /// (<c>aetherist-00</c>), drawn whole (no class, so no figure box).</summary>
        public static bool HasPortrait(string classId)
        {
            var def = Class(classId);
            return def != null ? Exists(PortraitFolder + def.PortraitFile()) : IsOwnPortrait(classId);
        }

        /// <summary>GG-T570: <paramref name="id"/> names a Portraits/ file directly (<c>aetherist-00</c>), not a class.</summary>
        private static bool IsOwnPortrait(string id) =>
            !string.IsNullOrEmpty(id) && !id.StartsWith("$") && GameApp.I?.Content != null && Exists(PortraitFolder + id);

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
                if (has) { Store(path, t); owners[t] = (def, true); } // it's drawn soon anyway
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
            if (cache.TryGetValue(path, out var t) && t != null) { Touch(path); return t; }
            t = string.IsNullOrEmpty(file) ? null : Resources.Load<Texture2D>(path);
            if (t == null)
            {
                if (warned.Add(path)) Debug.LogWarning($"[Art] missing portrait '{path}', using the mannequin");
                t = Load(PortraitFolder + MannequinPortrait);
                def = Class(MannequinClass); male = false;
            }
            if (t != null && def != null && !owners.ContainsKey(t)) owners[t] = (def, male);
            Store(path, t);
            return t;
        }

        /// <summary>The placeholder class, and its portrait file every class without art falls back to.</summary>
        public const string MannequinClass = "mannequin";
        public static readonly string MannequinPortrait = AssetNames.Of(MannequinClass);

        // ── The figure on a portrait (GG-T352) ─────────────────────────────────────
        // Owner 2026-11-07: "all characters are 1024x1024 with transparent backgrounds; that's the rule, that's what the
        // assets are." Thumbnail framings window that whole canvas (baked into Thumbs/ by tools/make_thumbs.py). Whole-figure views draw only the figure's
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
            if (cache.TryGetValue(path, out var t)) { Touch(path); return t; }
            return Store(path, Resources.Load<Texture2D>(path));
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
            if (cache.TryGetValue(path, out var t)) { Touch(path); return t; }
            return Store(path, Resources.Load<Texture2D>(path));
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
            Tag(BackdropSetKey + set);
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
            var t = Resources.Load<Texture2D>(BackgroundKey(theme, index));
            return t != null ? t : Load(Backdrops.Fallback);
        }

        /// <summary>The resource key <see cref="Background"/> loads (GG-T576: <see cref="LivingBackdrop"/> reads the
        /// backdrop's split from it).</summary>
        public static string BackgroundKey(string theme, int index = 0)
        {
            var content = GameApp.I != null ? GameApp.I.Content : null;
            return Backdrops.Resolve(theme, index, BackgroundSet, x => Backdrops.SetOf(content, x));
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

        /// <summary>The menu pointing-hand cursor (UI/cursor-00): "Hand cursor" by andreyin, CC0,
        /// https://opengameart.org/node/89568 (32x32, stored x4 nearest-neighbour at 128x128; credited in Credits.txt, GG-T772).
        /// Not generated by make_icons.py any more. The generated arrow if it's missing.</summary>
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
