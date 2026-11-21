"""
Generate male counterparts of every hero class's female portrait in Midjourney, download every candidate into one
folder and build a review gallery (GG-T535, umbrella GG-T532). Fire and forget: rerun it any time, finished classes are
skipped and jobs submitted by an interrupted run are downloaded, not resubmitted.

    python tools/male_variants.py                  # every Team:Hero class
    python tools/male_variants.py warden cleric    # just these
    python tools/male_variants.py --gallery        # only rebuild the gallery
    python tools/male_variants.py --redo cleric    # a fresh set for a class that already has one

How it works: Midjourney has no public API, so this drives its web app's own API (upload, submit-jobs, user-queue, the
CDN) from inside MindAttic.Browser's logged-in midjourney.com page over the DevTools protocol (Playwright
connect_over_cdp). Nothing is clicked. The Browser must be running with a debugging port; the script starts it so:

    set WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=--remote-debugging-port=9333
    C:\\Apps\\MindAttic.Browser\\MindAttic.Browser.exe https://www.midjourney.com/imagine

and stops with a message if Midjourney isn't logged in there.

Each class gets one Edit job per prompt in PROMPTS (the owner's own recipe: the portrait attached as the edit image,
"masculine male version of this exact character --ar 2:3", model v8.2), 4 images each, 896x1344.

Output (git-ignored), all candidates in one folder:
    _male_variants/<classId>_<variant><n>.png   e.g. cleric_a1.png .. cleric_a4.png, cleric_b1.png ..
    _male_variants/sources/<classId>.png        the image that was uploaded
    _male_variants/index.html                   gallery: the female portrait beside its candidates
    _male_variants/state.json, run.log
"""
import argparse
import base64
import html
import json
import os
import socket
import subprocess
import sys
import time
import urllib.request

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RES = os.path.join(ROOT, "Assets", "Gaiden", "Resources")
OUT = os.path.join(ROOT, "_male_variants")
BROWSER = r"C:\Apps\MindAttic.Browser\MindAttic.Browser.exe"
PORT = 9333
IMAGINE = "https://www.midjourney.com/imagine"
MAX_IN_FLIGHT = 3          # concurrent fast jobs a Standard plan allows
JOB_TIMEOUT = 20 * 60      # s before a job that never shows up is given up on
BACKGROUND = (246, 244, 240)

# a: the owner's prompt, verbatim (the Warden runs of 2026-11-21). b: a stronger push for a male build, since a: keeps
# the original silhouette very faithfully (skirts and all).
PROMPTS = {
    "a": "masculine male version of this exact character --ar 2:3",
    "b": "masculine male version of this exact character, a man with a broad-shouldered masculine build and a "
         "handsome male face, the same outfit, colours and weapon tailored for a man --ar 2:3",
}

JS_API = """async ([path, body]) => {
  const r = await fetch(path, body === null ? {headers: {'x-csrf-protection': '1'}}
    : {method: 'POST', headers: {'x-csrf-protection': '1', 'content-type': 'application/json'}, body: JSON.stringify(body)});
  return [r.status, await r.text()];
}"""
JS_UPLOAD = """async (b64) => {
  const bin = atob(b64); const a = new Uint8Array(bin.length);
  for (let i = 0; i < bin.length; i++) a[i] = bin.charCodeAt(i);
  const fd = new FormData(); fd.append('file', new Blob([a], {type: 'image/png'}), 'source.png');
  const r = await fetch('/api/storage-upload-file', {method: 'POST', body: fd, headers: {'x-csrf-protection': '1'}});
  return [r.status, await r.text()];
}"""
JS_DOWNLOAD = """async (u) => {
  const r = await fetch(u); if (!r.ok) return [r.status, ''];
  const b = new Uint8Array(await r.arrayBuffer()); let s = '';
  for (let i = 0; i < b.length; i += 32768) s += String.fromCharCode.apply(null, b.subarray(i, i + 32768));
  return [r.status, btoa(s)];
}"""


def log(msg):
    line = time.strftime("%H:%M:%S ") + msg
    print(line, flush=True)
    os.makedirs(OUT, exist_ok=True)
    with open(os.path.join(OUT, "run.log"), "a", encoding="utf-8") as f:
        f.write(line + "\n")


def heroes():
    classes = json.load(open(os.path.join(RES, "Content", "classes.json"), encoding="utf-8-sig"))
    seen, out = set(), []
    for c in classes:
        if c.get("Team") != "Hero":
            continue
        p = portrait_path(c)
        if p in seen:  # monstertamer draws wildchild's portrait
            continue
        seen.add(p)
        out.append(c)
    return out


def portrait_path(c):
    return os.path.join(RES, "Portraits", (c.get("Portrait") or c["Id"]).lower() + ".png")


def upload_image(c):
    """The portrait cut to its figure on a 2:3 off-white canvas: Midjourney reads a transparent PNG's hidden colours, and
    a figure that fills the frame at the --ar the job asks for keeps its detail."""
    im = Image.open(portrait_path(c)).convert("RGBA")
    x0, y0, x1, y1 = im.getchannel("A").getbbox()
    fig = im.crop((x0, y0, x1, y1))
    pad = round(0.05 * fig.height)
    h = max(fig.height + 2 * pad, round((fig.width + 2 * pad) * 3 / 2))
    w = round(h * 2 / 3)
    canvas = Image.new("RGBA", (w, h), BACKGROUND + (255,))
    canvas.alpha_composite(fig, ((w - fig.width) // 2, (h - fig.height) // 2))
    os.makedirs(os.path.join(OUT, "sources"), exist_ok=True)
    path = os.path.join(OUT, "sources", c["Id"] + ".png")
    canvas.convert("RGB").save(path)
    return path


def candidates(cid):
    if not os.path.isdir(OUT):
        return []
    return sorted(f for f in os.listdir(OUT) if f.startswith(cid + "_") and f.endswith(".png")
                  and f[len(cid) + 1:-4][:1] in PROMPTS and f[len(cid) + 2:-4].isdigit())


def done(cid, v):
    return all(os.path.exists(os.path.join(OUT, f"{cid}_{v}{n}.png")) for n in range(1, 5))


# --------------------------------------------------------------------------------------------------------------------
# The browser


def port_open():
    with socket.socket() as s:
        s.settimeout(1)
        return s.connect_ex(("127.0.0.1", PORT)) == 0


def connect(pw):
    if not port_open():
        log("Starting MindAttic.Browser with a debugging port")
        env = dict(os.environ, WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=f"--remote-debugging-port={PORT}")
        subprocess.Popen([BROWSER, IMAGINE], env=env)
        for _ in range(60):
            time.sleep(1)
            if port_open():
                break
        else:
            sys.exit("MindAttic.Browser never opened its debugging port. Close every MindAttic.Browser window, rerun.")
        time.sleep(5)
    browser = pw.chromium.connect_over_cdp(f"http://127.0.0.1:{PORT}")
    pages = [p for c in browser.contexts for p in c.pages if "midjourney.com" in p.url]
    page = pages[0] if pages else browser.contexts[0].pages[0]
    user = {}
    page.on("request", lambda r: user.setdefault("id", r.url.split("user_id=")[1].split("&")[0])
            if "/api/imagine?" in r.url and "user_id=" in r.url else None)
    page.goto(IMAGINE, wait_until="domcontentloaded")
    for _ in range(30):
        page.wait_for_timeout(1000)
        if "id" in user or "/auth/" in page.url:
            break
    if "/auth/" in page.url or "id" not in user:
        sys.exit("Midjourney isn't logged in in MindAttic.Browser. Log in there (Continue with Google), then rerun.")
    return page, user["id"]


class Midjourney:
    def __init__(self, page, user_id):
        self.page, self.user_id, self.mode = page, user_id, "fast"

    def api(self, path, body=None):
        status, text = self.page.evaluate(JS_API, [path, body])
        try:
            return status, json.loads(text)
        except ValueError:
            return status, {"raw": text[:300]}

    def upload(self, path):
        status, text = self.page.evaluate(JS_UPLOAD, base64.b64encode(open(path, "rb").read()).decode())
        if status != 200:
            raise RuntimeError(f"upload {status}: {text[:200]}")
        return "https://cdn.midjourney.com/u/" + json.loads(text)["bucketPathname"]

    def submit(self, prompt, edit_url):
        body = {"f": {"mode": self.mode, "private": False}, "channelId": f"singleplayer_{self.user_id}",
                "metadata": {"isMobile": None, "imagePrompts": 0, "imageReferences": 0, "characterReferences": 0,
                             "depthReferences": 0, "lightboxOpen": None},
                "t": "edit_diffusion", "prompt": f"{prompt} --edit {edit_url} --v 8.2",
                "editorPageRequest": None, "parentJob": None, "imageWasEdited": None}
        status, d = self.api("/api/submit-jobs", body)
        ok = d.get("success") or []
        if status == 200 and ok:
            return ok[0]["job_id"], None
        return None, json.dumps(d.get("failure") or d)[:400]

    def queued(self):
        status, d = self.api("/api/user-queue")
        if status != 200:
            return None
        return {j.get("job_id") or j.get("id") for k in ("running", "waiting") for j in d.get(k, [])}

    def download(self, job_id, n):
        status, b64 = self.page.evaluate(JS_DOWNLOAD, f"https://cdn.midjourney.com/{job_id}/0_{n}.png")
        return base64.b64decode(b64) if status == 200 and b64 else None


# --------------------------------------------------------------------------------------------------------------------
# The run


def load_state():
    p = os.path.join(OUT, "state.json")
    return json.load(open(p, encoding="utf-8")) if os.path.exists(p) else {}


def save_state(state):
    json.dump(state, open(os.path.join(OUT, "state.json"), "w", encoding="utf-8"), indent=1)


def fetch_job(mj, key, job_id):
    cid, v = key.rsplit("_", 1)
    got = []
    for n in range(4):
        data = mj.download(job_id, n)
        if data is None:
            return False
        got.append(data)
    for n, data in enumerate(got, 1):
        open(os.path.join(OUT, f"{cid}_{v}{n}.png"), "wb").write(data)
    return True


def run(classes, redo):
    from playwright.sync_api import sync_playwright
    os.makedirs(OUT, exist_ok=True)
    state = load_state()
    if redo:
        for c in classes:
            for v in PROMPTS:
                for n in range(1, 5):
                    p = os.path.join(OUT, f"{c['Id']}_{v}{n}.png")
                    if os.path.exists(p):
                        os.replace(p, os.path.join(OUT, f"{c['Id']}_{v}{n}.old{int(time.time())}.png"))
                state.pop(f"{c['Id']}_{v}", None)
    todo = [(c, v) for c in classes for v in PROMPTS if not done(c["Id"], v)]
    log(f"{len(todo)} jobs to do over {len(classes)} classes ({len(PROMPTS)} prompts each)")
    with sync_playwright() as pw:
        page, user_id = connect(pw)
        mj = Midjourney(page, user_id)
        sources = {}
        flight = {}  # key -> (job_id, submitted_at)
        for c, v in todo:  # jobs an earlier run submitted: just collect them
            key = f"{c['Id']}_{v}"
            if key in state:
                flight[key] = (state[key], 0)
        pending = [(c, v) for c, v in todo if f"{c['Id']}_{v}" not in flight]
        failures = 0
        while pending or flight:
            while pending and len(flight) < MAX_IN_FLIGHT:
                c, v = pending[0]
                key = f"{c['Id']}_{v}"
                try:
                    if c["Id"] not in sources:
                        sources[c["Id"]] = mj.upload(upload_image(c))
                    job_id, err = mj.submit(PROMPTS[v], sources[c["Id"]])
                except Exception as e:  # a hung page call or a bad upload: try again after a pause
                    job_id, err = None, f"{type(e).__name__}: {e}"
                if job_id:
                    pending.pop(0)
                    flight[key] = (job_id, time.time())
                    state[key] = job_id
                    save_state(state)
                    log(f"submitted {key} ({c['Name']}) -> {job_id} [{mj.mode}]")
                    failures = 0
                    continue
                failures += 1
                log(f"submit {key} failed ({mj.mode}): {err}")
                low = (err or "").lower()
                if mj.mode == "fast" and any(w in low for w in ("fast", "hour", "credit", "subscri", "limit")):
                    mj.mode = "relax"
                    log("switching to relax mode")
                elif "queue" in low or "concurrent" in low or "too many" in low:
                    break  # full queue: wait for a slot
                elif failures >= 3:
                    pending.pop(0)
                    log(f"giving up on {key} after 3 failures")
                    failures = 0
                else:
                    time.sleep(20)
                break
            time.sleep(15)
            busy = mj.queued()
            if busy is None:
                continue
            for key, (job_id, t0) in list(flight.items()):
                if job_id in busy:
                    continue
                if fetch_job(mj, key, job_id):
                    del flight[key]
                    log(f"downloaded {key} (4 images)")
                    build_gallery(heroes())
                elif t0 and time.time() - t0 > JOB_TIMEOUT:
                    del flight[key]
                    state.pop(key, None)
                    save_state(state)
                    log(f"{key}: job {job_id} produced no images in {JOB_TIMEOUT // 60} min, will resubmit next run")
    path = build_gallery(heroes())
    missing = [f"{c['Id']}_{v}" for c in classes for v in PROMPTS if not done(c["Id"], v)]
    log(f"finished. gallery: {path}" + (f"; still missing (rerun to retry): {', '.join(missing)}" if missing else ""))


# --------------------------------------------------------------------------------------------------------------------
# The gallery


def build_gallery(classes):
    rows = []
    for c in classes:
        cid = c["Id"]
        src = os.path.relpath(portrait_path(c), OUT).replace("\\", "/")
        groups = []
        for v, prompt in PROMPTS.items():
            imgs = "".join(f'<a href="{f}" target="_blank"><img src="{f}" loading="lazy" title="{f}"><span>{f}</span></a>'
                           for f in (f"{cid}_{v}{n}.png" for n in range(1, 5)) if os.path.exists(os.path.join(OUT, f)))
            groups.append(f'<div class="set"><div class="tag">{v}: {html.escape(prompt.split(" --")[0])}</div>'
                          f'<div class="cands">{imgs or "<i>not generated yet</i>"}</div></div>')
        rows.append(f'<section><h2>{html.escape(c["Name"])} <small>{cid} · {html.escape(c.get("Role") or "")}</small></h2>'
                    f'<div class="row"><figure><img src="{src}" loading="lazy"><figcaption>female (in game)</figcaption>'
                    f'</figure><div class="sets">{"".join(groups)}</div></div></section>')
    n_done = sum(1 for c in classes if all(done(c["Id"], v) for v in PROMPTS))
    page = f"""<!doctype html><html><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Male Hero Variants</title><style>
:root{{--bg:#16161b;--panel:#202028;--ink:#ececf1;--dim:#9a9aa8;--line:#2f2f3a;--hi:#c8a2ff}}
body{{margin:0;background:var(--bg);color:var(--ink);font:14px/1.4 system-ui,sans-serif;padding:16px}}
h1{{font-size:20px;margin:0 0 4px}} p{{color:var(--dim);margin:0 0 16px}}
section{{background:var(--panel);border:1px solid var(--line);border-radius:8px;padding:12px;margin-bottom:12px}}
h2{{font-size:16px;margin:0 0 8px}} small{{color:var(--dim);font-weight:400}}
.row{{display:flex;gap:12px;align-items:flex-start}} figure{{margin:0;flex:none}}
figure img{{width:180px;height:270px;object-fit:contain;background:#3a3a46;border-radius:6px}}
figcaption,.tag{{color:var(--dim);font-size:12px}} .sets{{display:flex;flex-direction:column;gap:8px;min-width:0}}
.cands{{display:flex;flex-wrap:wrap;gap:8px}} .cands a{{color:var(--dim);font-size:11px;text-decoration:none;text-align:center}}
.cands img{{display:block;width:180px;height:270px;object-fit:cover;border-radius:6px;border:2px solid transparent}}
.cands a:hover img{{border-color:var(--hi)}}
@media(max-width:700px){{.row{{flex-direction:column}} figure img,.cands img{{width:120px;height:180px}}}}
</style></head><body><h1>Male hero variants</h1><p>{n_done} of {len(classes)} classes complete. Every candidate is a file
in this folder named &lt;class&gt;_&lt;set&gt;&lt;n&gt;.png; click one for full size.</p>
{''.join(rows)}</body></html>"""
    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, "index.html")
    open(path, "w", encoding="utf-8").write(page)
    return path


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("ids", nargs="*", help="class ids (default: every hero class)")
    ap.add_argument("--gallery", action="store_true", help="only rebuild the gallery")
    ap.add_argument("--redo", action="store_true", help="generate fresh sets for the given classes")
    args = ap.parse_args()
    classes = heroes()
    if args.ids:
        unknown = set(args.ids) - {c["Id"] for c in classes}
        if unknown:
            sys.exit(f"not hero classes: {', '.join(sorted(unknown))}")
        classes = [c for c in classes if c["Id"] in args.ids]
    if args.gallery:
        print(build_gallery(heroes()))
        return
    if os.name == "nt":  # keep the PC awake for this process only (ES_CONTINUOUS | ES_SYSTEM_REQUIRED); ends with it
        import ctypes
        ctypes.windll.kernel32.SetThreadExecutionState(0x80000000 | 0x00000001)
    stalls = 0
    while True:  # unattended: a dropped DevTools connection just reconnects and resumes from state.json
        before = sum(done(c["Id"], v) for c in classes for v in PROMPTS)
        try:
            run(classes, args.redo)
            return
        except SystemExit:
            raise
        except Exception as e:
            args.redo = False
            progressed = sum(done(c["Id"], v) for c in classes for v in PROMPTS) > before
            stalls = 0 if progressed else stalls + 1
            if stalls >= 5:
                log(f"run crashed 5 times in a row with no progress ({type(e).__name__}: {e}); stopping, rerun to resume")
                build_gallery(heroes())
                sys.exit(1)
            log(f"connection dropped ({type(e).__name__}); reconnecting in 15 s")
            time.sleep(15)

if __name__ == "__main__":
    main()
