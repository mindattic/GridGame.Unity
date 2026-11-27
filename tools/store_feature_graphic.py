"""Composes the Google Play feature graphic (GG-T1521).

Play wants a 1024x500 JPEG or 24-bit PNG with no alpha. This builds it from art the game already ships: a 1536x3072
battle backdrop from Assets/GridGame/Resources/Backgrounds, scaled to 1024 wide with a 500 px band cut out of it, and
a line of full-body hero portraits from Assets/GridGame/Resources/Portraits stood in front, the centre one largest and
the flanks stepping back (smaller, dimmer). No title text or logo goes on it, so it stays valid whatever name GG-T573
picks; Play draws the app name beside the graphic itself.

Each portrait is trimmed to its alpha bounding box, scaled to its height share of the canvas and stood on a line near
the bottom edge, over a soft dark shadow so it reads against bright skies. The bottom of the backdrop fades to near
black so the figures' feet have ground.

    python tools/store_feature_graphic.py                       # writes docs/store/feature-graphic-1024x500.png
    python tools/store_feature_graphic.py --backdrop still-00.jpg --band 0.4
    python tools/store_feature_graphic.py --heroes knight-female-00,ronin-male-00,red-mage-female-00
"""
import argparse
import sys
from pathlib import Path

from PIL import Image, ImageEnhance, ImageFilter

ROOT = Path(__file__).resolve().parent.parent
RES = ROOT / "Assets" / "GridGame" / "Resources"
OUT = ROOT / "docs" / "store" / "feature-graphic-1024x500.png"
W, H = 1024, 500

DEFAULT_BACKDROP = "shardfall-01.jpg"
# Left to right; the middle one is the lead and drawn largest.
DEFAULT_HEROES = "ronin-male-00,knight-female-00,red-mage-female-00,paladin-male-00,sword-master-female-00"


def load_backdrop(name, band, dim):
    img = Image.open(RES / "Backgrounds" / name).convert("RGB")
    scaled_h = round(img.height * W / img.width)
    img = img.resize((W, scaled_h), Image.LANCZOS)
    top = min(max(round(band * scaled_h - H / 2), 0), scaled_h - H)
    img = img.crop((0, top, W, top + H))
    img = ImageEnhance.Brightness(img).enhance(dim)
    # Fade the lower two-fifths to near black so the line of heroes stands on something.
    fade = Image.linear_gradient("L").resize((W, H))
    fade = fade.point(lambda v: max(0, (v - 150) * 255 // 105) * 0.85)
    return Image.composite(Image.new("RGB", (W, H), (8, 8, 14)), img, fade)


def load_hero(name, height):
    img = Image.open(RES / "Portraits" / (name + ".png")).convert("RGBA")
    img = img.crop(img.getchannel("A").getbbox())
    width = round(img.width * height / img.height)
    return img.resize((width, height), Image.LANCZOS)


def shadow_of(hero, blur):
    pad = blur * 3
    alpha = Image.new("L", (hero.width + pad * 2, hero.height + pad * 2), 0)
    alpha.paste(hero.getchannel("A"), (pad, pad))
    alpha = alpha.filter(ImageFilter.GaussianBlur(blur)).point(lambda v: v * 0.8)
    shadow = Image.new("RGBA", alpha.size, (0, 0, 0, 255))
    shadow.putalpha(alpha)
    return shadow, pad


def layer(img, pos):
    """img on a transparent canvas-sized layer, clipped at the edges."""
    out = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    out.paste(img, pos)
    return out


def compose(backdrop, heroes, band, dim, lead_height, step, spread, ground):
    canvas = load_backdrop(backdrop, band, dim).convert("RGBA")
    mid = len(heroes) // 2
    # Draw from the outside in so nearer (larger, central) figures overlap the ones behind them.
    order = sorted(range(len(heroes)), key=lambda i: -abs(i - mid))
    for i in order:
        rank = abs(i - mid)
        hero = load_hero(heroes[i], round(H * lead_height * step ** rank))
        if rank:
            rgb = ImageEnhance.Brightness(hero.convert("RGB")).enhance(1.0 - 0.12 * rank)
            rgb.putalpha(hero.getchannel("A"))
            hero = rgb
        cx = W / 2 + (i - mid) * spread * W
        x = round(cx - hero.width / 2)
        y = round(H * ground) - hero.height
        shadow, pad = shadow_of(hero, 10)
        canvas.alpha_composite(layer(shadow, (x - pad + 6, y - pad + 4)))
        canvas.alpha_composite(layer(hero, (x, y)))
    return canvas.convert("RGB")


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--backdrop", default=DEFAULT_BACKDROP, help="file in Resources/Backgrounds")
    ap.add_argument("--heroes", default=DEFAULT_HEROES, help="comma-separated Resources/Portraits names, left to right")
    ap.add_argument("--band", type=float, default=0.3, help="vertical centre of the backdrop band, 0 top to 1 bottom")
    ap.add_argument("--dim", type=float, default=0.9, help="backdrop brightness multiplier")
    ap.add_argument("--lead-height", type=float, default=0.92, help="centre hero's height as a share of the canvas")
    ap.add_argument("--step", type=float, default=0.86, help="height factor per place away from the centre")
    ap.add_argument("--spread", type=float, default=0.19, help="horizontal gap between hero centres, share of width")
    ap.add_argument("--ground", type=float, default=0.97, help="where feet stand, share of height from the top")
    ap.add_argument("--out", type=Path, default=OUT)
    args = ap.parse_args()

    heroes = [h.strip() for h in args.heroes.split(",") if h.strip()]
    missing = [h for h in heroes if not (RES / "Portraits" / (h + ".png")).exists()]
    if not (RES / "Backgrounds" / args.backdrop).exists():
        missing.append("Backgrounds/" + args.backdrop)
    if missing:
        print("missing art: " + ", ".join(missing), file=sys.stderr)
        return 1

    img = compose(args.backdrop, heroes, args.band, args.dim, args.lead_height, args.step, args.spread, args.ground)
    args.out.parent.mkdir(parents=True, exist_ok=True)
    img.save(args.out, "PNG", optimize=True)
    print(f"{args.out.relative_to(ROOT) if args.out.is_relative_to(ROOT) else args.out}: {img.width}x{img.height} {img.mode}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
