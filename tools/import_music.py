"""Import music tracks into the game (GG-T461; the recipe from GG-T456).

Each source (any format ffmpeg reads: Suno's .m4a, .mp3, .wav ...) becomes
Assets/Gaiden/Resources/Audio/Music/<GameName>.ogg: leading/trailing silence trimmed at -55 dB, two-pass
loudness-normalised to -18 LUFS (TP -1.5, LRA 11; the level the rest of the music sits at), 20 ms de-click fades,
44.1 kHz, Vorbis q6. Unity's importer settings (Streaming, Vorbis) are forced by GaidenBuild.OnPreprocessAudio.

  python tools/import_music.py "Hub of Peace.m4a=HearthsideCamp"      one file, named
  python tools/import_music.py "Rising Theme.m4a"                     name derived: RisingTheme
  python tools/import_music.py --all                                  every new file in the source folder
  python tools/import_music.py --record "Hub of Peace.m4a=HearthsideCamp"   hash only (already converted)

A relative path that doesn't exist is looked up in the source folder (default R:\\Desktop\\GridGameMusic, or
--src). Duplicates are found by audio hash (the decoded PCM, so a re-encode or a renamed copy still matches):
against each other and against every track already imported (tools/music_sources.json). An existing game file
is never overwritten without --force. Afterwards it prints where to list the new tracks (campaign.json).
"""
import argparse, hashlib, json, os, re, subprocess, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DST = os.path.join(ROOT, "Assets", "Gaiden", "Resources", "Audio", "Music")
MANIFEST = os.path.join(ROOT, "tools", "music_sources.json")
CAMPAIGN = os.path.join(ROOT, "Assets", "Gaiden", "Resources", "Content", "campaign.json")
DEFAULT_SRC = r"R:\Desktop\GridGameMusic"
AUDIO_EXT = (".m4a", ".mp3", ".wav", ".flac", ".ogg", ".aac", ".opus", ".webm")
TRIM = ("silenceremove=start_periods=1:start_threshold=-55dB,areverse,"
        "silenceremove=start_periods=1:start_threshold=-55dB,areverse")
LOUD = "loudnorm=I=-18:TP=-1.5:LRA=11"


def run(args, **kw):
    return subprocess.run(args, capture_output=True, text=True, encoding="utf-8", errors="replace", **kw)


def audio_hash(path):
    """MD5 of the decoded audio (16-bit stereo 44.1 kHz PCM): identical sound hashes the same whatever the
    container, tags or file name."""
    h = hashlib.md5()
    p = subprocess.Popen(["ffmpeg", "-v", "error", "-i", path, "-vn", "-ac", "2", "-ar", "44100", "-f", "s16le", "-"],
                         stdout=subprocess.PIPE)
    for chunk in iter(lambda: p.stdout.read(1 << 20), b""):
        h.update(chunk)
    if p.wait() != 0:
        raise RuntimeError(f"ffmpeg could not decode {path}")
    return h.hexdigest()


def duration(path):
    return float(run(["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", path]).stdout)


def derive_name(path):
    """'Rising Theme (1).m4a' -> 'RisingTheme1'. Non-ASCII titles can't be derived: name them with FILE=Name."""
    stem = os.path.splitext(os.path.basename(path))[0]
    words = re.findall(r"[A-Za-z0-9]+", stem)
    name = "".join(w[:1].upper() + w[1:] for w in words)
    if not name or not name[0].isalpha() or any(ord(c) > 127 for c in stem):
        return None
    return name


def convert(src, name):
    meas = run(["ffmpeg", "-hide_banner", "-nostats", "-i", src, "-vn", "-af",
                TRIM + "," + LOUD + ":print_format=json", "-f", "null", "-"]).stderr
    found = re.search(r"\{[^{}]*\"input_i\"[^{}]*\}", meas, re.S)
    if not found:
        raise RuntimeError(f"loudness measurement failed for {src}")
    m = json.loads(found.group(0))
    ln = (f"{LOUD}:measured_I={m['input_i']}:measured_TP={m['input_tp']}:measured_LRA={m['input_lra']}:"
          f"measured_thresh={m['input_thresh']}:offset={m['target_offset']}:linear=true")
    tmp = os.path.join(DST, name + ".tmp.wav")
    out = os.path.join(DST, name + ".ogg")
    try:
        r = run(["ffmpeg", "-v", "error", "-y", "-i", src, "-vn", "-af", TRIM + "," + ln, "-ar", "44100", "-ac", "2",
                 tmp])
        if r.returncode != 0:
            raise RuntimeError(r.stderr.strip())
        d = duration(tmp)
        r = run(["ffmpeg", "-v", "error", "-y", "-i", tmp, "-af",
                 f"afade=t=in:d=0.02,afade=t=out:st={max(0.0, d - 0.02):.3f}:d=0.02",
                 "-c:a", "libvorbis", "-q:a", "6", out])
        if r.returncode != 0:
            raise RuntimeError(r.stderr.strip())
    finally:
        if os.path.exists(tmp):
            os.remove(tmp)
    return d


def existing_names():
    return {os.path.splitext(f)[0] for f in os.listdir(DST) if not f.endswith(".meta") and not f.endswith(".wav")}


def load_manifest():
    if os.path.exists(MANIFEST):
        with open(MANIFEST, encoding="utf-8") as f:
            return json.load(f)
    return {}


def save_manifest(man):
    with open(MANIFEST, "w", encoding="utf-8", newline="\n") as f:
        json.dump(dict(sorted(man.items())), f, ensure_ascii=False, indent=2)
        f.write("\n")


def where_to_list(names):
    """Prints the playlists the new tracks can go in, with how many tracks each already has."""
    try:
        with open(CAMPAIGN, encoding="utf-8-sig") as f:
            camp = json.load(f)
    except (OSError, ValueError):
        camp = {}
    print("\nList the new track(s) in Assets/Gaiden/Resources/Content/campaign.json (see docs/MUSIC.md):")
    print(f"  HubMusic (the Camp)                 {len(camp.get('HubMusic', []))} tracks")
    for th in camp.get("Themes", []):
        own = th.get("Music", [])
        note = "" if len(own) >= 4 else "  (borrows until it has 4)"
        print(f"  Themes[{th.get('Id', '?')}].Music".ljust(38) + f"{len(own)} tracks{note}")
    for moment, tracks in (camp.get("MomentMusic") or {}).items():
        print(f"  MomentMusic.{moment}".ljust(38) + f"{len(tracks)} tracks")
    print("  new: " + ", ".join(f'"{n}"' for n in names))


def resolve(item, src_dir):
    path, _, name = item.partition("=")
    if not os.path.exists(path) and not os.path.isabs(path):
        path = os.path.join(src_dir, path)
    if not os.path.isfile(path):
        sys.exit(f"not found: {item}")
    name = name.strip()
    if name and not re.fullmatch(r"[A-Za-z][A-Za-z0-9]*", name):
        sys.exit(f"game name '{name}' must be PascalCase letters/digits")
    return path, name or derive_name(path), bool(name)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("items", nargs="*", help="FILE or FILE=GameName")
    ap.add_argument("--src", default=DEFAULT_SRC, help="folder relative FILEs are looked up in (and --all scans)")
    ap.add_argument("--all", action="store_true", help="import every audio file in --src not already imported")
    ap.add_argument("--force", action="store_true", help="overwrite an existing game track of the same name")
    ap.add_argument("--record", action="store_true", help="only record the source hashes (tracks already converted)")
    a = ap.parse_args()

    items = list(a.items)
    if a.all:
        if not os.path.isdir(a.src):
            sys.exit(f"no source folder {a.src}")
        items += sorted(f for f in os.listdir(a.src) if f.lower().endswith(AUDIO_EXT))
    if not items:
        ap.error("give FILE[=GameName] arguments or --all")

    man = load_manifest()
    by_hash = {v["audio_md5"]: k for k, v in man.items() if v.get("audio_md5")}
    have = existing_names()
    done, seen, problems = [], {}, 0

    for item in items:
        path, name, named = resolve(item, a.src)
        label = os.path.basename(path).encode("ascii", "replace").decode()
        print(f"{label}: hashing ...", flush=True)
        digest = audio_hash(path)
        if digest in seen:
            print(f"  DUPLICATE of {seen[digest]} in this batch: skipped")
            continue
        seen[digest] = label
        if digest in by_hash and by_hash[digest] != name:
            dup = "DUPLICATE: " if named else ""
            print(f"  {dup}already imported as {by_hash[digest]}: skipped")
            continue
        if digest in by_hash and not a.force and not a.record:
            print(f"  already imported as {name}: skipped (--force to re-convert)")
            continue
        if not name:
            print("  can't derive an ASCII game name: pass it as FILE=GameName")
            problems += 1
            continue
        if not a.record:
            if name in have and not a.force:
                print(f"  {name} already exists in Resources/Audio/Music: skipped (--force to overwrite)")
                problems += 1
                continue
            d = convert(path, name)
            print(f"  -> {name}.ogg  {d:.1f}s")
        man[name] = {"source": os.path.basename(path), "audio_md5": digest}
        by_hash[digest] = name
        have.add(name)
        done.append(name)

    if done:
        save_manifest(man)
        print(f"\n{'Recorded' if a.record else 'Imported'} {len(done)}; hashes in tools/music_sources.json")
        if not a.record:
            where_to_list(done)
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main())
