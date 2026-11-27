using System;

namespace GridGame.Core.World
{
    /// <summary>A screen colour grade as plain numbers (GG-T1679): hue rotation in degrees, saturation, brightness and
    /// contrast as multipliers, then an overlay <see cref="TintR"/>/<see cref="TintG"/>/<see cref="TintB"/> (0..1) mixed in
    /// at <see cref="TintStrength"/> (0..1). The presentation reads these to drive a post-process or a sprite tint.</summary>
    public struct ColorGrade
    {
        public float HueShift;
        public float Saturation;
        public float Brightness;
        public float Contrast;
        public float TintR;
        public float TintG;
        public float TintB;
        public float TintStrength;

        public static ColorGrade Neutral => new ColorGrade
        {
            HueShift = 0f, Saturation = 1f, Brightness = 1f, Contrast = 1f,
            TintR = 1f, TintG = 1f, TintB = 1f, TintStrength = 0f,
        };

        public static ColorGrade Lerp(ColorGrade a, ColorGrade b, float t)
        {
            t = Clamp01(t);
            return new ColorGrade
            {
                HueShift = L(a.HueShift, b.HueShift, t),
                Saturation = L(a.Saturation, b.Saturation, t),
                Brightness = L(a.Brightness, b.Brightness, t),
                Contrast = L(a.Contrast, b.Contrast, t),
                TintR = L(a.TintR, b.TintR, t),
                TintG = L(a.TintG, b.TintG, t),
                TintB = L(a.TintB, b.TintB, t),
                TintStrength = L(a.TintStrength, b.TintStrength, t),
            };
        }

        /// <summary>Grades one colour (channels 0..1): hue rotation, saturation about luma, contrast about mid-grey,
        /// brightness, then the tint mixed in; the result is clamped to 0..1.</summary>
        public void ApplyToRgb(ref float r, ref float g, ref float b)
        {
            double rad = HueShift * Math.PI / 180.0;
            float cos = (float)Math.Cos(rad), sin = (float)Math.Sin(rad);
            float nr = r * (0.213f + cos * 0.787f - sin * 0.213f) + g * (0.715f - cos * 0.715f - sin * 0.715f) + b * (0.072f - cos * 0.072f + sin * 0.928f);
            float ng = r * (0.213f - cos * 0.213f + sin * 0.143f) + g * (0.715f + cos * 0.285f + sin * 0.140f) + b * (0.072f - cos * 0.072f - sin * 0.283f);
            float nb = r * (0.213f - cos * 0.213f - sin * 0.787f) + g * (0.715f - cos * 0.715f + sin * 0.715f) + b * (0.072f + cos * 0.928f + sin * 0.072f);

            float luma = 0.299f * nr + 0.587f * ng + 0.114f * nb;
            nr = luma + (nr - luma) * Saturation;
            ng = luma + (ng - luma) * Saturation;
            nb = luma + (nb - luma) * Saturation;

            nr = ((nr - 0.5f) * Contrast + 0.5f) * Brightness;
            ng = ((ng - 0.5f) * Contrast + 0.5f) * Brightness;
            nb = ((nb - 0.5f) * Contrast + 0.5f) * Brightness;

            float s = Clamp01(TintStrength);
            r = Clamp01(L(nr, TintR, s));
            g = Clamp01(L(ng, TintG, s));
            b = Clamp01(L(nb, TintB, s));
        }

        static float L(float a, float b, float t) => a + (b - a) * t;

        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
