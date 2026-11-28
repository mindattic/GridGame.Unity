using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>
    /// GG-T1071: the one flip rule for anything that turns about its vertical (Y) axis on a flat UI: a timeline diamond
    /// spinning on a pushback, an actor tile card flipping (GG-T1072). Drawn orthographically: the width scales by
    /// |cos(angle)|, so at 90° the face is truly edge-on (about a pixel thick), and the face swaps exactly there:
    /// the front while cos(angle) ≥ 0 (facing the camera), the back only once it has turned past 90° (facing away).
    /// Because the swap lands on the edge-on frame, there is no visible pop.
    /// </summary>
    public static class CardFlip
    {
        /// <summary>True while the front faces the camera: cos(angle) ≥ 0, i.e. the angle (mod 360) is within
        /// [0, 90] or [270, 360). Exactly 90° (and 270°) is edge-on and still counts as the front.</summary>
        public static bool FrontFacing(float degrees)
        {
            float a = Mathf.Repeat(degrees, 360f);
            return a <= 90f || a >= 270f;
        }

        /// <summary>The projected width share, |cos(angle)|, floored at <paramref name="minScale"/> (pass 1 ÷ the drawn
        /// width in px to keep the edge-on frame one pixel thick rather than vanishing).</summary>
        public static float WidthScale(float degrees, float minScale = 0f) =>
            Mathf.Max(minScale, Mathf.Abs(Mathf.Cos(degrees * Mathf.Deg2Rad)));

        /// <summary>Both at once: which face shows and how wide it is drawn.</summary>
        public static (bool front, float widthScale) At(float degrees, float minScale = 0f) =>
            (FrontFacing(degrees), WidthScale(degrees, minScale));

        /// <summary>
        /// Applies the rule to a pair of face elements: shows <paramref name="back"/> (above the front) only while the
        /// angle faces away, and returns the width scale for the caller to fold into its own X scale (the host usually
        /// has a scale of its own, so this never writes one).
        /// </summary>
        public static float Apply(VisualElement back, float degrees, float minScale = 0f)
        {
            var (front, w) = At(degrees, minScale);
            if (back != null)
            {
                back.style.display = front ? DisplayStyle.None : DisplayStyle.Flex;
                if (!front) back.BringToFront();
            }
            return w;
        }

        private static Texture2D diamondBack;

        /// <summary>
        /// Placeholder back art for a timeline diamond (no owner art exists yet): a dark navy fill with a fine lattice and
        /// a nested inner frame and pip, so it reads as the reverse of a card, never a mirrored portrait. Drawn square;
        /// on a 45°-turned diamond frame the lattice shows as a diamond lattice. Real art is the owner's call.
        /// </summary>
        public static Texture2D DiamondBack => diamondBack != null ? diamondBack : diamondBack = MakeDiamondBack();

        private static Texture2D MakeDiamondBack()
        {
            const int s = 64;
            var t = new Texture2D(s, s, TextureFormat.RGBA32, false)
            { name = "diamond-back", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
            var baseC = Palette.Scene.CardBack;
            var lineC = Palette.Scene.CardLine;
            var trimC = Palette.Scene.CardTrim;
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    int edge = Mathf.Min(Mathf.Min(x, y), Mathf.Min(s - 1 - x, s - 1 - y));
                    var c = (x + y) % 8 == 0 || (x - y + s) % 8 == 0 ? lineC : baseC; // fine cross-hatch
                    if (edge >= 10 && edge <= 11) c = trimC;                       // inner frame
                    else if (edge >= 12 && edge <= 13) c = baseC;                  // a clear band inside it
                    if (edge >= 27) c = trimC;                                     // the centre pip
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            return t;
        }
    }
}
