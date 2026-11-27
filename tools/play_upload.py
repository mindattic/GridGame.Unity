"""Upload the Android release bundle to Google Play through the Play Developer API (GG-T1514).

    python tools/play_upload.py --validate              # everything but the commit (internal track)
    python tools/play_upload.py                         # upload and commit to the internal testing track
    python tools/play_upload.py --track closed          # or: --track production --rollout 0.1

Steps: edits.insert, bundles.upload, tracks.update (release name v<version>+<code> and the release notes), then
edits.commit. --validate runs edits.validate instead of the commit and deletes the edit, so Play keeps nothing.

Needs the Google client libraries (pip install google-api-python-client google-auth) and the owner's service-account
JSON, found at $GG_PLAY_SERVICE_ACCOUNT, else at the path named in play-publisher.env (GG_PLAY_SERVICE_ACCOUNT=...,
git-ignored like android-signing.env), else --key. Never commit that file.

Build/AndroidRelease/release.json (tools/release.ps1) supplies version, build number and sha256; the upload is
refused when the sha256 does not match the .aab. Release notes come from its "notes" (text or {lang: text}) or from
--notes FILE, else Build/AndroidRelease/release-notes.txt; they are sent as en-US.
"""
import argparse
import hashlib
import json
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Build", "AndroidRelease")
PACKAGE = "com.mindattic.gridgame"
ENV_FILE = os.path.join(ROOT, "play-publisher.env")
ENV_VAR = "GG_PLAY_SERVICE_ACCOUNT"
SCOPE = "https://www.googleapis.com/auth/androidpublisher"
NOTES_LIMIT = 500  # Play's per-language release notes cap


def die(msg):
    print("play_upload: " + msg, file=sys.stderr)
    sys.exit(1)


def sha256_of(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def key_path(arg):
    if arg:
        return arg
    if os.environ.get(ENV_VAR):
        return os.environ[ENV_VAR]
    if os.path.isfile(ENV_FILE):
        with open(ENV_FILE, encoding="utf-8") as f:
            for line in f:
                line = line.strip()
                if line.startswith(ENV_VAR + "="):
                    return line.split("=", 1)[1].strip().strip('"')
    die("no service-account key: set %s, or %s in play-publisher.env, or pass --key" % (ENV_VAR, ENV_VAR))


def pick(d, *names):
    for n in names:
        if d.get(n) not in (None, ""):
            return d[n]
    return None


def load_release(aab, notes_file):
    rj = os.path.join(OUT, "release.json")
    if not os.path.isfile(rj):
        die("%s missing: run tools/release.ps1 first" % rj)
    with open(rj, encoding="utf-8") as f:
        rel = json.load(f)
    want = pick(rel, "sha256", "aabSha256", "Sha256")
    if not want:
        die("release.json has no sha256")
    if sha256_of(aab).lower() != str(want).lower():
        die("release.json sha256 does not match %s: rebuild with tools/release.ps1" % aab)
    version = pick(rel, "version", "Version")
    code = pick(rel, "buildNumber", "build", "versionCode", "Build")
    if not version or code is None:
        die("release.json needs version and build number")
    notes = None
    if notes_file:
        with open(notes_file, encoding="utf-8") as f:
            notes = f.read()
    else:
        notes = pick(rel, "notes", "releaseNotes", "ReleaseNotes")
        if isinstance(notes, dict):
            notes = pick(notes, "en-US", "en")
        if not notes:
            fallback = os.path.join(OUT, "release-notes.txt")
            if os.path.isfile(fallback):
                with open(fallback, encoding="utf-8") as f:
                    notes = f.read()
    if not notes or not str(notes).strip():
        die("no release notes: put them in release.json, release-notes.txt or --notes FILE")
    notes = str(notes).strip()
    if len(notes) > NOTES_LIMIT:
        die("release notes are %d characters; Play allows %d" % (len(notes), NOTES_LIMIT))
    return "v%s+%s" % (version, code), notes


def main():
    ap = argparse.ArgumentParser(description="Upload the release .aab to Google Play (GG-T1514).")
    ap.add_argument("--aab", default=os.path.join(OUT, "GridGame.aab"), help="bundle to upload")
    ap.add_argument("--track", default="internal", choices=["internal", "closed", "production"],
                    help="Play track (closed is Play's 'alpha'); default internal")
    ap.add_argument("--rollout", type=float, default=None, help="staged rollout fraction, e.g. 0.1 (production)")
    ap.add_argument("--notes", help="release notes text file (en-US)")
    ap.add_argument("--key", help="service-account JSON path (else $%s or play-publisher.env)" % ENV_VAR)
    ap.add_argument("--validate", action="store_true", help="do everything but commit; the edit is deleted")
    args = ap.parse_args()

    if args.rollout is not None and not 0 < args.rollout < 1:
        die("--rollout must be between 0 and 1 (leave it off for a full rollout)")
    if not os.path.isfile(args.aab):
        die("bundle not found: " + args.aab)
    name, notes = load_release(args.aab, args.notes)
    key = key_path(args.key)
    if not os.path.isfile(key):
        die("service-account key not found: " + key)

    try:
        from google.oauth2 import service_account
        from googleapiclient.discovery import build
        from googleapiclient.http import MediaFileUpload
    except ImportError:
        die("missing libraries: pip install google-api-python-client google-auth")

    creds = service_account.Credentials.from_service_account_file(key, scopes=[SCOPE])
    api = build("androidpublisher", "v3", credentials=creds, cache_discovery=False)
    edits = api.edits()
    track = {"closed": "alpha"}.get(args.track, args.track)

    edit_id = edits.insert(packageName=PACKAGE, body={}).execute()["id"]
    print("edit %s opened for %s" % (edit_id, PACKAGE))
    try:
        media = MediaFileUpload(args.aab, mimetype="application/octet-stream", resumable=True)
        bundle = edits.bundles().upload(packageName=PACKAGE, editId=edit_id, media_body=media).execute()
        code = bundle["versionCode"]
        print("bundle uploaded: versionCode %s" % code)
        release = {"name": name, "versionCodes": [str(code)],
                   "releaseNotes": [{"language": "en-US", "text": notes}]}
        if args.rollout is not None:
            release["status"] = "inProgress"
            release["userFraction"] = args.rollout
        else:
            release["status"] = "completed"
        edits.tracks().update(packageName=PACKAGE, editId=edit_id, track=track,
                              body={"track": track, "releases": [release]}).execute()
        print("track %s set to %s (%s)" % (args.track, name, release["status"]))
        if args.validate:
            edits.validate(packageName=PACKAGE, editId=edit_id).execute()
            print("validated; nothing committed")
            edits.delete(packageName=PACKAGE, editId=edit_id).execute()
        else:
            edits.commit(packageName=PACKAGE, editId=edit_id).execute()
            print("committed to %s" % args.track)
    except Exception as e:
        try:
            edits.delete(packageName=PACKAGE, editId=edit_id).execute()
        except Exception:
            pass
        die("%s: %s" % (type(e).__name__, e))


if __name__ == "__main__":
    main()
