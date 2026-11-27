using System.Collections.Generic;
using GridGame.Core.Content;
using UnityEngine;

namespace GridGame.App
{
    /// <summary>
    /// GG-T1774 (docs/research/sprite-recolor.md §8): a creature breed's art, baked once per (art file, breed). The art's
    /// region mask (<c>&lt;folder&gt;&lt;file&gt;-mask</c>: R body, G secondary, B keep-as-is) and the breed's ramps
    /// (breeds.json) go through the <c>BreedRecolor</c> shader into a RenderTexture, read back into a plain Texture2D, so
    /// UI Toolkit draws it exactly like the original art: no material on the board, no per-frame cost. Art with no mask,
    /// a plain breed, or no GPU (a -nographics run) draws as it is.
    /// </summary>
    public static class BreedArt
    {
        /// <summary>The mask's file name beside its art.</summary>
        public const string MaskSuffix = "-mask";
        /// <summary>Ramp textures are this many texels wide, dark to light.</summary>
        public const int RampWidth = 64;
        /// <summary>Baked textures kept; past it the least recently used is dropped (Unity unloads it once nothing draws it).</summary>
        internal const int CacheCap = 48;

        private static Material material;
        private static readonly Dictionary<string, Texture2D> baked = new Dictionary<string, Texture2D>();
        private static readonly LinkedList<string> order = new LinkedList<string>();
        private static readonly Dictionary<string, Texture2D> masks = new Dictionary<string, Texture2D>(System.StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Texture2D> ramps = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, bool> classHasMask = new Dictionary<string, bool>(System.StringComparer.OrdinalIgnoreCase);

        /// <summary>Whether <paramref name="classId"/>'s art has a breed mask (its portrait's or its thumbnail's), so its
        /// foes may roll a breed. <see cref="ContentDb.HasBreedMask"/> points here.</summary>
        public static bool ClassHasMask(ContentDb db, string classId)
        {
            if (string.IsNullOrEmpty(classId)) return false;
            if (classHasMask.TryGetValue(classId, out var has)) return has;
            var file = db?.TryClass(classId)?.PortraitFile();
            has = !string.IsNullOrEmpty(file) && (Mask(Art.ThumbFolder, file) != null || Mask(Art.PortraitFolder, file) != null);
            return classHasMask[classId] = has;
        }

        /// <summary><paramref name="art"/> (loaded from <paramref name="folder"/>, its name the file) recoloured as
        /// <paramref name="breedId"/>, or null when there is nothing to recolour: no breed or a plain one, no mask, no GPU.</summary>
        public static Texture2D Recolor(Texture2D art, string folder, string breedId)
        {
            if (art == null || string.IsNullOrEmpty(breedId)) return null;
            var breed = GameApp.I?.Content?.Breeds.TryGetValue(breedId, out var b) == true ? b : null;
            if (breed == null || breed.IsPlain) return null;
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return null;
            string key = folder + art.name + "|" + breed.Id;
            if (baked.TryGetValue(key, out var done) && done != null) { order.Remove(key); order.AddLast(key); return done; }
            var mask = Mask(folder, art.name);
            if (mask == null) return null;
            var mat = Material();
            if (mat == null) return null;
            var t = Bake(art, mask, breed, mat);
            if (t == null) return null;
            baked[key] = t;
            order.Remove(key);
            order.AddLast(key);
            while (order.Count > CacheCap) { baked.Remove(order.First.Value); order.RemoveFirst(); }
            return t;
        }

        private static Texture2D Mask(string folder, string file)
        {
            string path = folder + file + MaskSuffix;
            if (masks.TryGetValue(path, out var m)) return m;
            return masks[path] = Resources.Load<Texture2D>(path);
        }

        private static Material Material()
        {
            if (material != null) return material;
            var shader = Resources.Load<Shader>("Shaders/BreedRecolor");
            if (shader == null || !shader.isSupported) { Debug.LogWarning("[BreedArt] BreedRecolor shader missing or unsupported"); return null; }
            return material = new Material(shader) { name = "BreedRecolor", hideFlags = HideFlags.DontSave };
        }

        private static Texture2D Bake(Texture2D art, Texture2D mask, BreedDef breed, Material mat)
        {
            int w = art.width, h = art.height;
            var body = Ramp(breed.BodyRamp);
            var sec = breed.SecondaryRamp != null && breed.SecondaryRamp.Count > 0 ? Ramp(breed.SecondaryRamp) : body;
            mat.SetTexture("_MaskTex", mask);
            mat.SetTexture("_RampBody", body);
            mat.SetTexture("_RampSecondary", sec);
            mat.SetColor("_KeepTint", Art.HexColor(breed.KeepTint, Color.white));
            mat.SetFloat("_KeepMix", breed.KeepMix);
            mat.SetFloat("_LightLift", breed.LightLift);
            mat.SetFloat("_ChromaScale", breed.ChromaScale);
            mat.SetFloat("_Alpha", breed.Alpha);
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var prev = RenderTexture.active;
            try
            {
                Graphics.Blit(art, rt, mat);
                RenderTexture.active = rt;
                bool mips = art.mipmapCount > 1;
                var t = new Texture2D(w, h, TextureFormat.RGBA32, mips, false)
                {
                    name = art.name + "-" + breed.Id,
                    filterMode = art.filterMode,
                    wrapMode = TextureWrapMode.Clamp,
                };
                t.ReadPixels(new Rect(0, 0, w, h), 0, 0, mips);
                t.Apply(mips, true); // no CPU copy kept
                return t;
            }
            finally
            {
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        /// <summary>A breed ramp's <see cref="RampWidth"/> x 1 texture: the stops evenly spaced, dark to light, blended
        /// between. Cached by its stops.</summary>
        private static Texture2D Ramp(List<string> stops)
        {
            string key = string.Join(",", stops);
            if (ramps.TryGetValue(key, out var r) && r != null) return r;
            var cols = new List<Color>(stops.Count);
            foreach (var s in stops) cols.Add(Art.HexColor(s, Color.gray));
            if (cols.Count == 1) cols.Add(cols[0]);
            var px = new Color[RampWidth];
            for (int i = 0; i < RampWidth; i++)
            {
                float f = i / (float)(RampWidth - 1) * (cols.Count - 1);
                int a = Mathf.Min(cols.Count - 2, (int)f);
                px[i] = Color.Lerp(cols[a], cols[a + 1], f - a);
            }
            r = new Texture2D(RampWidth, 1, TextureFormat.RGBA32, false, false)
            {
                name = "breed-ramp", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave,
            };
            r.SetPixels(px);
            r.Apply(false, true);
            return ramps[key] = r;
        }
    }
}
