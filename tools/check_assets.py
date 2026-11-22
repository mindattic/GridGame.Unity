"""Fails if any sound cue or music track the code references has no bundled file (no silent events),
an ability, item or status has no pixel icon, or an art asset breaks the art standard outright
(docs/POLISH.md section H). Softer art-standard misses are printed as warnings and scored by tools/polish.py."""
import glob, json, os, re, sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import asset_names  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RESD = os.path.join(ROOT, "Assets", "Gaiden", "Resources")
SFX = os.path.join(RESD, "Audio", "Sfx")
MUSIC = os.path.join(RESD, "Audio", "Music")
# Cue key -> file, as App/Services/Audio.Cue resolves it (GG-T254, GG-T562): the key's words joined by hyphens plus
# variant -00 (battle.crucible_wave -> battle-crucible-wave-00), except these (the random-pick pools always have -00).
SPECIAL = {"ui.back": "ui-click-00", "battle.move": "battle-move-00", "battle.hit": "battle-hit-00"}


def cue_file(cue):
    return SPECIAL.get(cue) or asset_names.file(asset_names.stem(*cue.split(".")))


def campaign_tracks():
    """Every track campaign.json names (HubMusic, MomentMusic, each theme's Music and MusicBorrow)."""
    camp = json.load(open(os.path.join(RESD, "Content", "campaign.json"), encoding="utf-8-sig"))
    out = set(camp.get("HubMusic", []))
    for tracks in (camp.get("MomentMusic") or {}).values():
        out |= set(tracks)
    for th in camp.get("Themes", []):
        out |= set(th.get("Music", [])) | set(th.get("MusicBorrow", []))
    return out


def music_exists(track):
    return any(os.path.exists(os.path.join(MUSIC, track + ext)) for ext in (".ogg", ".mp3"))

# docs/POLISH.md section H thresholds.
PORTRAIT_CANVAS = 1024       # POL-H1, owner 2026-11-07 (GG-T352): "all characters are 1024x1024 with transparent
                             # backgrounds; that's the rule, that's what the assets are" - never trimmed or shrunk
FIGURE_BOX = 640             # POL-H1: the figure's box (classes.json "Figure") fitted in this square, never enlarged,
                             # is its drawn size (App Art.FigureSize): 640 = one person, smaller = a smaller creature
ICON_PX, ICON_GRID, ICON_COLORS = 88, 22, 9   # POL-H2: 22x22 pixel art at x4, 8 colours + outline
UI_ICON_PX = 64
BACKDROP_MIN_H = 1536         # POL-H3 (GG-T353): a 1:2 portrait backdrop at the art's own resolution, never downscaled
BACKDROP_MEAN = (0.25, 0.50) # POL-H3: mean luminance band shared by every backdrop
DIALOGUE_FIGURE_PX = 1200    # POL-H4: DialogueScreen.FigureHeight, the largest a portrait is drawn


def art_findings():
    """[(rule, subject, ok, detail, hard)] for every portrait, icon and backdrop."""
    from PIL import Image, ImageStat
    out = []
    pdir = os.path.join(RESD, "Portraits")
    for f in sorted(glob.glob(os.path.join(pdir, "*.png"))):
        n = os.path.basename(f)[:-4]
        im = Image.open(f)
        w, h = im.size
        hard, soft = [], []
        if im.mode != "RGBA":
            hard.append(f"{im.mode}, not RGBA (no transparency)")
        if (w, h) != (PORTRAIT_CANVAS, PORTRAIT_CANVAS):
            hard.append(f"canvas {w}x{h}, not {PORTRAIT_CANVAS}x{PORTRAIT_CANVAS} (run GridGame.Studio/tools/import_art.py / portrait_cut.py --import)")
        bb = im.getchannel("A").getbbox() if im.mode == "RGBA" else None
        if not hard:
            clear = sum(im.getchannel("A").point(lambda v: 255 if v < 16 else 0).histogram()[255:]) / (w * h)
            if clear < 0.2:
                hard.append(f"only {clear:.0%} of the canvas is transparent (a solid background left in?)")
        if not asset_names.is_valid(n):
            hard.append("not named word-word-00 (tools/asset_names.py, GG-T557)")
        out.append(("POL-H1", f"Portraits/{n}.png", not (hard or soft), "; ".join(hard + soft), bool(hard)))
        if bb:
            bw, bh = bb[2] - bb[0], bb[3] - bb[1]
            # DialogueScreen caps a figure at its native height (GG-T437), so it is never drawn above its own pixels.
            drawn = min(bh, bh * min(1.0, FIGURE_BOX / max(bw, bh)) * DIALOGUE_FIGURE_PX / FIGURE_BOX)
            scale = drawn / bh
            out.append(("POL-H4", f"Portraits/{n}.png", scale <= 1.0,
                        f"drawn at {scale:.2f}x its native size in dialogue (a {bh} px figure at {drawn:.0f} px)", False))

    for folder, px in (("AbilityIcons", ICON_PX), ("ItemIcons", ICON_PX), ("StatusIcons", ICON_PX), ("Icons", UI_ICON_PX)):
        for f in sorted(glob.glob(os.path.join(RESD, folder, "*.png"))):
            im = Image.open(f).convert("RGBA")
            why, hard = [], False
            if im.size != (px, px):
                why.append(f"{im.size[0]}x{im.size[1]}, not {px}x{px}"); hard = True
            elif px == ICON_PX:
                small = im.resize((ICON_GRID, ICON_GRID), Image.NEAREST)
                if small.resize((px, px), Image.NEAREST).tobytes() != im.tobytes():
                    why.append(f"off the {ICON_GRID}x{ICON_GRID} pixel grid")
                cols = {p[:3] for p in small.get_flattened_data() if p[3] > 16} if hasattr(small, "get_flattened_data") \
                    else {p[:3] for p in small.getdata() if p[3] > 16}
                if len(cols) > ICON_COLORS:
                    why.append(f"{len(cols)} colours (max {ICON_COLORS})")
            if not asset_names.is_valid(os.path.basename(f)[:-4]):
                why.append("not named word-word-00 (tools/asset_names.py, GG-T561)"); hard = True
            out.append(("POL-H2", f"{folder}/{os.path.basename(f)}", not why, "; ".join(why), hard))

    for f in sorted(glob.glob(os.path.join(RESD, "Backgrounds", "*"))):
        if f.endswith(".meta"):
            continue
        im = Image.open(f)
        why, hard = [], False
        w, h = im.size
        if not f.lower().endswith((".jpg", ".png")) or w * 2 != h or h < BACKDROP_MIN_H:
            why.append(f"{os.path.splitext(f)[1]} {w}x{h}, not a 1:2 JPEG/PNG at least {BACKDROP_MIN_H} tall"); hard = True
        if not asset_names.is_valid(os.path.splitext(os.path.basename(f))[0]):
            why.append("not named <region-words>-NN (tools/asset_names.py, GG-T353/GG-T561)"); hard = True
        mean = ImageStat.Stat(im.convert("L")).mean[0] / 255
        if not BACKDROP_MEAN[0] <= mean <= BACKDROP_MEAN[1]:
            why.append(f"mean luminance {mean:.2f} outside {BACKDROP_MEAN[0]}-{BACKDROP_MEAN[1]}")
        out.append(("POL-H3", f"Backgrounds/{os.path.basename(f)}", not why, "; ".join(why), hard))
    return out


def main():
    code = "".join(open(p, encoding="utf-8").read() for p in glob.glob(os.path.join(ROOT, "Assets", "Gaiden", "App", "**", "*.cs"), recursive=True))
    cues = set(re.findall(r'Sfx\("([^"]+)"', code))
    # (NB: a bare "A and B or []" here would silently evaluate to [] whenever A itself found nothing,
    # even though B is the pattern that actually matters — never leave that construct for the next edit.)
    cues |= set(c for pair in re.findall(r'Sfx\([^;]*?"([a-z.]+)"\s*:\s*"([a-z.]+)"', code) for c in pair)
    for m in re.findall(r'case DamageType\.\w+: return "([a-z._]+)";', code): cues.add(m)
    cues |= set(re.findall(r'StepAnim\([^;]*?"([a-z._]+)"\)', code))
    missing = [c for c in sorted(cues) if not os.path.exists(os.path.join(SFX, cue_file(c) + ".ogg"))]
    # Tracks: every fallback a screen's Music names in code (?? "crusade-00") plus every campaign.json playlist entry.
    tracks = set(re.findall(r'Music => [^;]*?"([a-z0-9-]+)"', code)) | set(re.findall(r'\?\? "([a-z0-9]+(?:-[a-z0-9]+)*-\d\d)"', code))
    tracks |= campaign_tracks()
    missing += [f"music:{t}" for t in sorted(tracks) if not music_exists(t)]
    # GG-T562: every audio file follows the asset naming rule (word-word-00.ext).
    for folder in (SFX, MUSIC):
        missing += [f"badly named (want word-word-00): {os.path.basename(folder)}/{f}" for f in sorted(os.listdir(folder))
                    if not f.endswith(".meta") and not asset_names.is_valid(os.path.splitext(f)[0])]

    def _ids(name): return [x["Id"] for x in json.load(open(os.path.join(RESD, "Content", name), encoding="utf-8"))]
    for folder, ids in (("AbilityIcons", _ids("abilities.json")), ("ItemIcons", _ids("items.json")), ("StatusIcons", _ids("buffs.json") + ["stunned"])):
        missing += [f"icon:{folder}/{asset_names.of(i)} (run GridGame.Studio/tools/make_icons.py)" for i in ids if not os.path.exists(os.path.join(RESD, folder, asset_names.of(i) + ".png"))]

    # GG-T561: every image under Resources follows the asset naming rule (word-word-00.ext).
    for folder in ("Tiles", "Tutorial", "UI", "Backgrounds"):
        for f in sorted(glob.glob(os.path.join(RESD, folder, "**", "*.*"), recursive=True)):
            if f.lower().endswith((".png", ".jpg")) and not asset_names.is_valid(os.path.splitext(os.path.basename(f))[0]):
                missing.append(f"badly named (want word-word-00): {os.path.relpath(f, RESD)}")

    # GG-T557: every class's portrait (and male art, when it names one) resolves to a file, by the one naming rule.
    for c in json.load(open(os.path.join(RESD, "Content", "classes.json"), encoding="utf-8-sig")):
        for male in ((False, True) if c.get("MalePortrait") else (False,)):
            f = asset_names.portrait_file(c, male=male)
            if not os.path.exists(os.path.join(RESD, "Portraits", f + ".png")):
                missing.append(f"portrait:{c['Id']}{' (male)' if male else ''} -> Portraits/{f}.png")

    # GG-T556: attack-type hit sounds are optional slots (Audio falls back to battle-hit-00..06), so a missing one warns.
    at = json.load(open(os.path.join(RESD, "Content", "attacktypes.json"), encoding="utf-8"))
    slots = [f for t in at["Types"] for f in t["Sfx"]]
    pending = [f for f in slots if not os.path.exists(os.path.join(SFX, f + ".ogg"))]
    if pending:
        print(f"sfx warning: {len(pending)} of {len(slots)} attack-type hit sounds still missing "
              f"(Content/attacktypes.json, docs/AUDIO_ATTACK_TYPES.md); those types play the generic hits")

    findings = art_findings()
    broken = [f"{s}: {d}" for r, s, ok, d, hard in findings if hard]
    soft = [(r, s, d) for r, s, ok, d, hard in findings if not ok and not hard and r != "POL-H4"]
    upscaled = sum(1 for r, s, ok, d, hard in findings if r == "POL-H4" and not ok)
    for r, s, d in soft:
        print(f"art warning {r if r != 'warn' else '(composition)'} {s}: {d}")
    if upscaled:
        print(f"art warning POL-H4: {upscaled} portraits are drawn above native size in dialogue "
              f"(a {FIGURE_BOX}-unit figure is {DIALOGUE_FIGURE_PX} px tall)")
    if missing or broken:
        print("MISSING ASSETS:", ", ".join(missing + broken)); sys.exit(1)
    print(f"asset check OK ({len(cues)} cues, {len(tracks)} tracks, pixel icons, "
          f"{len(soft)} art warnings, see docs/POLISH.md section H)")


if __name__ == "__main__":
    main()
