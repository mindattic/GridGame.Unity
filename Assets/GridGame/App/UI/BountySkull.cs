using UnityEngine;
using UnityEngine.UIElements;

namespace GridGame.App
{
    /// <summary>
    /// The bounty skull (§22.4, GG-T431): a dark red roundel with a white ☠ (the glyph the trap marks already use, so it is
    /// in the bundled fonts, GG-LAW-11). Worn by a marked target on the board and printed on the board's posters, so the
    /// player learns one sign for "tougher variant, reward at the board".
    /// </summary>
    internal static class BountySkull
    {
        public static readonly Color Fill = new Color(0.42f, 0.04f, 0.05f, 0.95f);

        public static VisualElement Make(float size, VisualElement parent = null)
        {
            var disc = new VisualElement { pickingMode = PickingMode.Ignore };
            disc.style.width = disc.style.height = size;
            disc.style.borderTopLeftRadius = disc.style.borderTopRightRadius = disc.style.borderBottomLeftRadius = disc.style.borderBottomRightRadius = size / 2f;
            disc.style.backgroundColor = Fill;
            disc.style.borderTopWidth = disc.style.borderBottomWidth = disc.style.borderLeftWidth = disc.style.borderRightWidth = 2;
            disc.style.borderTopColor = disc.style.borderBottomColor = disc.style.borderLeftColor = disc.style.borderRightColor = Palette.Bad;
            disc.style.alignItems = Align.Center;
            disc.style.justifyContent = Justify.Center;
            var glyph = new Label("☠") { pickingMode = PickingMode.Ignore };
            glyph.style.fontSize = size * 0.7f;
            glyph.style.color = Palette.White;
            glyph.style.unityTextAlign = TextAnchor.MiddleCenter;
            glyph.style.marginLeft = glyph.style.marginRight = glyph.style.marginTop = glyph.style.marginBottom = Space.S0;
            glyph.style.paddingLeft = glyph.style.paddingRight = glyph.style.paddingTop = glyph.style.paddingBottom = Space.S0;
            Fonts.UseHeavy(glyph);
            disc.Add(glyph);
            parent?.Add(disc);
            return disc;
        }
    }
}
