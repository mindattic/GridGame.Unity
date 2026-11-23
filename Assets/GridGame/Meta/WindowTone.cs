using System;

namespace GridGame.Meta
{
    /// <summary>
    /// A window theme's display adjustment (GG-T563; owner 2026-11-21: "themes needs to support Brightness/Contrast/Gamma;
    /// so that they can be adjusted based on the devices on quirks when it comes to nits or w/e ... they all need to present
    /// themselves by default in the '0.5' level and be able to push the bars in either direction"). Each value is 0..1 and
    /// 0.5 is neutral: a theme at 0.5/0.5/0.5 looks exactly as it did before the adjustment existed.
    /// Engine-free, so <see cref="Map"/> is unit-tested; the app applies it to everything a theme paints
    /// (App/UI/WindowThemes.cs).
    /// </summary>
    public readonly struct WindowTone : IEquatable<WindowTone>
    {
        public const float Neutral = 0.5f;

        /// <summary>Brightness moves every channel by up to ±<see cref="BrightnessRange"/> (a quarter of full scale each way):
        /// enough to lift a dark theme on a dim panel or calm a bright one on a high-nit screen, never a white or black sheet.</summary>
        public const float BrightnessRange = 0.25f;
        /// <summary>Contrast scales around mid-gray by 2^((v−0.5)·2·log2(<see cref="ContrastMax"/>)): ×0.5 at 0, ×1 at 0.5,
        /// ×2 at 1. Exponential, so each half of the bar feels the same and 0 still keeps half the theme's shape.</summary>
        public const float ContrastMax = 2f;
        /// <summary>Gamma raises each channel to 2^((0.5−v)·2): exponent 2 at 0 (mid-tones darker), 1 at 0.5, 0.5 at 1
        /// (mid-tones lighter). Black and white stay put; only the mid-tones move.</summary>
        public const float GammaMax = 2f;

        /// <summary>The text guarantee (POLISH.md §A, WCAG AA body text) and the floor for dim captions (GG-T564: WCAG's
        /// minimum for large or secondary text).</summary>
        public const float TextContrast = 4.5f, CaptionContrast = 3f;

        public readonly float Brightness, Contrast, Gamma;

        public WindowTone(float brightness, float contrast, float gamma)
        {
            Brightness = Clamp01(brightness, Neutral);
            Contrast = Clamp01(contrast, Neutral);
            Gamma = Clamp01(gamma, Neutral);
        }

        public static WindowTone Default => new WindowTone(Neutral, Neutral, Neutral);

        /// <summary>True when every value is (within a slider step of) 0.5: the theme is painted exactly as authored.</summary>
        public bool IsNeutral => Math.Abs(Brightness - Neutral) < 0.001f && Math.Abs(Contrast - Neutral) < 0.001f && Math.Abs(Gamma - Neutral) < 0.001f;

        /// <summary>The additive offset brightness <paramref name="v"/> gives (−0.25..+0.25).</summary>
        public static float Offset(float v) => (Clamp01(v, Neutral) - Neutral) * 2f * BrightnessRange;
        /// <summary>The contrast factor <paramref name="v"/> gives (×0.5..×2).</summary>
        public static float Factor(float v) => (float)Math.Pow(ContrastMax, (Clamp01(v, Neutral) - Neutral) * 2f);
        /// <summary>The gamma exponent <paramref name="v"/> gives (2..0.5; higher v = lighter mid-tones).</summary>
        public static float Exponent(float v) => (float)Math.Pow(GammaMax, (Neutral - Clamp01(v, Neutral)) * 2f);

        /// <summary>One sRGB channel (0..1) through the adjustment: gamma, then contrast about mid-gray, then brightness,
        /// clamped to 0..1. The identity at 0.5/0.5/0.5.</summary>
        public float Map(float c)
        {
            if (IsNeutral) return c;
            float x = c <= 0f ? 0f : c >= 1f ? 1f : c;
            float g = Exponent(Gamma);
            if (g != 1f) x = (float)Math.Pow(x, g);
            x = (x - 0.5f) * Factor(Contrast) + 0.5f;
            x += Offset(Brightness);
            return x <= 0f ? 0f : x >= 1f ? 1f : x;
        }

        /// <summary>A #RRGGBB colour through <see cref="Map"/>; an unparsable one comes back unchanged.</summary>
        public string MapHex(string hex)
        {
            if (IsNeutral || !WindowThemeCatalog.TryHex(hex, out int v)) return hex;
            var self = this;
            int Ch(int shift) => (int)Math.Round(self.Map(((v >> shift) & 0xFF) / 255f) * 255f);
            return WindowThemeCatalog.Hex(Ch(16), Ch(8), Ch(0));
        }

        /// <summary>WCAG relative luminance of an sRGB colour (channels 0..1).</summary>
        public static float Luminance(float r, float g, float b)
        {
            static float Lin(float v) => v <= 0.04045f ? v / 12.92f : (float)Math.Pow((v + 0.055f) / 1.055f, 2.4f);
            return 0.2126f * Lin(r) + 0.7152f * Lin(g) + 0.0722f * Lin(b);
        }

        /// <summary>WCAG contrast ratio of two luminances (1..21).</summary>
        public static float Ratio(float la, float lb) => (Math.Max(la, lb) + 0.05f) / (Math.Min(la, lb) + 0.05f);

        /// <summary>The 4.5:1 text guarantee after an adjustment (POLISH.md §A, Tokens.OnWindow): <paramref name="text"/>
        /// keeps its hue and is lifted toward white until it clears 4.5:1 against <paramref name="window"/> (the window's
        /// lightest colour, as Tokens checks); when even white can't (a theme pushed very bright), it is pushed toward black
        /// instead. A colour that already clears it comes back unchanged. Channels 0..1.</summary>
        public static (float r, float g, float b) Readable((float r, float g, float b) text, (float r, float g, float b) window, float min = 4.5f)
        {
            float lw = Luminance(window.r, window.g, window.b);
            float Contrast((float r, float g, float b) c) => Ratio(Luminance(c.r, c.g, c.b), lw);
            if (Contrast(text) >= min) return text;
            (float r, float g, float b) Toward(float t)
            {
                var c = text;
                for (int i = 0; i < 40 && Contrast(c) < min; i++)
                    c = (c.r + (t - c.r) * 0.08f, c.g + (t - c.g) * 0.08f, c.b + (t - c.b) * 0.08f);
                return Contrast(c) >= min ? c : (t, t, t);
            }
            var up = Toward(1f);
            if (Contrast(up) >= min) return up;
            var down = Toward(0f);
            return Contrast(down) >= Contrast(up) ? down : up;
        }

        public bool Equals(WindowTone o) => Brightness == o.Brightness && Contrast == o.Contrast && Gamma == o.Gamma;
        public override bool Equals(object obj) => obj is WindowTone o && Equals(o);
        public override int GetHashCode() => (Brightness, Contrast, Gamma).GetHashCode();
        public override string ToString() => $"b{Brightness:0.00} c{Contrast:0.00} g{Gamma:0.00}";

        private static float Clamp01(float v, float fallback) => float.IsNaN(v) ? fallback : v < 0f ? 0f : v > 1f ? 1f : v;
    }

    /// <summary>A stored tone (settings.json <c>game.themeTones</c> for a preset, or a custom theme's own fields).</summary>
    public sealed class WindowToneDef
    {
        public float Brightness = WindowTone.Neutral, Contrast = WindowTone.Neutral, Gamma = WindowTone.Neutral;
        public WindowTone Tone => new WindowTone(Brightness, Contrast, Gamma);
        public static WindowToneDef From(WindowTone t) => new WindowToneDef { Brightness = t.Brightness, Contrast = t.Contrast, Gamma = t.Gamma };
    }
}
