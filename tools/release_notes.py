"""Release notes (GG-T1512): writes docs/release/CHANGELOG.md and the store "What is new" text from the commits since the
last v* tag (all commits when none is tagged yet).

    python tools/release_notes.py              # writes the three files
    python tools/release_notes.py --print      # prints them, writes nothing

Outputs, under docs/release/:
  CHANGELOG.md          the version.txt version, the date and one bullet per commit, newest first
  whatsnew-play.txt     Google Play "What is new", at most 500 characters
  whatsnew-appstore.txt App Store "What's New", at most 4000 characters

The ticket ID at the end of a subject is dropped; a subject's text before its first colon (the area) is kept as the
lead, and only the first sentence is used so the store text stays short.
"""
import datetime
import os
import re
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "docs", "release")
PLAY_MAX = 500
APPSTORE_MAX = 4000
TICKET = re.compile(r"\s*\(?\b[A-Z]{2,}-T\d+\b\)?\s*$")
SKIP = re.compile(r"^(?:Merge\b|Revert\b)", re.I)


def git(*args):
    return subprocess.run(["git", *args], cwd=ROOT, capture_output=True, text=True, encoding="utf-8",
                          check=True).stdout.strip()


def last_tag():
    try:
        return git("describe", "--tags", "--abbrev=0", "--match", "v*")
    except subprocess.CalledProcessError:
        return ""


def subjects(tag):
    rng = f"{tag}..HEAD" if tag else "HEAD"
    out = []
    for line in git("log", "--no-merges", "--format=%s", rng).splitlines():
        s = TICKET.sub("", line).strip()
        if s and not SKIP.match(s):
            out.append(s)
    return out


def short(subject):
    """First sentence of a subject, so one commit is one short store line."""
    m = re.search(r"(?<=[a-z0-9\)])\.\s", subject)
    s = subject[:m.start()] if m else subject
    return s.rstrip(".")


def fit(lines, limit, width=110):
    """Bullets, newest first, each cut to width at a word boundary, while they fit in limit characters."""
    text = ""
    for ln in lines:
        if len(ln) > width:
            ln = ln[:width].rsplit(" ", 1)[0].rstrip(",;:(") + "..."
        nxt = text + (chr(10) if text else "") + "- " + ln
        if len(nxt) > limit:
            break
        text = nxt
    return text


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    version = open(os.path.join(ROOT, "version.txt"), encoding="utf-8").read().strip()
    tag = last_tag()
    subs = subjects(tag)
    lines = [short(s) for s in subs]
    since = f"since {tag}" if tag else "since the first commit"
    changelog = f"# Changelog\n\n## {version} - {datetime.date.today().isoformat()}\n\nChanges {since}.\n\n" + \
        "".join(f"- {s}\n" for s in subs)
    play = fit(lines, PLAY_MAX)
    appstore = fit(lines, APPSTORE_MAX)
    files = {"CHANGELOG.md": changelog, "whatsnew-play.txt": play + "\n", "whatsnew-appstore.txt": appstore + "\n"}
    assert len(play) <= PLAY_MAX and len(appstore) <= APPSTORE_MAX
    if "--print" in sys.argv:
        for name, text in files.items():
            print(f"=== {name} ({len(text)})\n{text}")
        return
    os.makedirs(OUT, exist_ok=True)
    for name, text in files.items():
        with open(os.path.join(OUT, name), "w", encoding="utf-8", newline="\n") as f:
            f.write(text)
    print(f"{version}: {len(subs)} commits {since}; play {len(play)}/{PLAY_MAX}, app store {len(appstore)}/{APPSTORE_MAX}")


if __name__ == "__main__":
    main()
