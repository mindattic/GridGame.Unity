"""Files a fix ticket for each owner walkthrough note saved by the in-game Note button (GG-T1939).

The game (Developer options on) saves persistentDataPath/feedback/<timestamp>/{shot.png,note.json}. For each folder not yet
imported this files `tickets ticket new --repo GridGame.Unity --type fix`, copies the screenshot into the ticket's attachments,
adds a note with the screenshot and the note.json details, approves it, links it part-of GG-T1937 and writes
<folder>/imported.txt (the ticket id) so the note is never filed twice.

  python tools/import_feedback.py                 # the Windows player's feedback folder
  python tools/import_feedback.py --dir <path>    # any folder of note folders (e.g. one pulled from a phone)
  python tools/import_feedback.py --dry-run       # list what would be filed

From an Android device (package com.mindattic.gridgame):
  adb pull /sdcard/Android/data/com.mindattic.gridgame/files/feedback ./feedback-android
  python tools/import_feedback.py --dir ./feedback-android
"""
import argparse, json, os, shutil, subprocess, sys

PARENT = "GG-T1937"
REPO = "GridGame.Unity"
ATTACH_ROOT = r"D:\Projects\MindAttic\MindAttic.Tickets\data\attachments"
DEFAULT_DIR = os.path.join(os.path.expanduser("~"), "AppData", "LocalLow", "MindAttic", "GridGame", "feedback")
MARK = "imported.txt"


def tickets(*args, stdin=None):
    r = subprocess.run(["tickets", *args], input=stdin, capture_output=True, text=True, encoding="utf-8")
    if r.returncode != 0:
        raise RuntimeError(f"tickets {' '.join(args[:3])} failed: {(r.stderr or r.stdout).strip()}")
    return r.stdout.strip()


def details(note):
    lines = [f"- screen: {note.get('screen')}", f"- build: {note.get('version')}", f"- time: {note.get('time')}"]
    d = note.get("device") or {}
    lines.append(f"- device: {d.get('width')}x{d.get('height')} @ {d.get('dpi')} dpi, {d.get('model')} ({d.get('platform')})")
    b = note.get("battle")
    if b:
        lines.append(f"- battle: stage {b.get('stage')}, wave {b.get('wave')}/{b.get('waves')}, phase {b.get('phase')}")
        lines.append("- units: " + "; ".join(b.get("units") or []))
    return "\n".join(lines)


def file_note(folder, dry):
    with open(os.path.join(folder, "note.json"), encoding="utf-8") as f:
        note = json.load(f)
    comment = (note.get("comment") or "").strip()
    first = comment.splitlines()[0] if comment else "(no comment)"
    summary = f"{note.get('screen')}: {first} (owner walkthrough note)"
    if dry:
        print("would file:", summary)
        return
    tid = tickets("ticket", "new", "--repo", REPO, "--type", "fix", "--summary", summary, "--parent", PARENT).split()[0]
    body = [f"Owner walkthrough note {os.path.basename(folder)}", "", comment, ""]
    shot = os.path.join(folder, "shot.png")
    if os.path.exists(shot):
        dest = os.path.join(ATTACH_ROOT, tid)
        os.makedirs(dest, exist_ok=True)
        shutil.copy2(shot, os.path.join(dest, "shot.png"))
        body += [f"![screenshot](file:///{dest.replace(os.sep, '/')}/shot.png)", ""]
    body.append(details(note))
    tickets("ticket", "note", tid, "-", stdin="\n".join(body))
    tickets("ticket", "approve", tid)
    tickets("ticket", "link", tid, "part-of", PARENT)
    with open(os.path.join(folder, MARK), "w", encoding="utf-8") as f:
        f.write(tid + "\n")
    print(f"{os.path.basename(folder)} -> {tid}: {summary}")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--dir", default=DEFAULT_DIR, help="folder of note folders (default: the Windows player's feedback folder)")
    ap.add_argument("--dry-run", action="store_true")
    a = ap.parse_args()
    if not os.path.isdir(a.dir):
        sys.exit(f"no feedback folder at {a.dir}")
    new = [os.path.join(a.dir, n) for n in sorted(os.listdir(a.dir))
           if os.path.exists(os.path.join(a.dir, n, "note.json")) and not os.path.exists(os.path.join(a.dir, n, MARK))]
    if not new:
        print("no new notes")
    for folder in new:
        try:
            file_note(folder, a.dry_run)
        except Exception as e:
            print(f"{folder}: {e}", file=sys.stderr)


if __name__ == "__main__":
    main()
