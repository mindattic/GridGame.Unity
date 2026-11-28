"""
GG-T1802: show the owner a spot in the game without playing to it.

    python tools/scenario.py <preset|target> [--spec file.json] [--device iphone17pro] [--replace] [--build] [--no-show]

A preset is tools/scenarios/<name>.json; a target is first-boss, boss:N, boss:<class>, final-boss, stage:<id>,
region:<theme>, screen:<id> or overworld. --spec runs a spec file instead. The player is launched detached on a throwaway
"Scenario" profile, sized like the device with its safe area applied, and left open.
"""
import argparse, json, os, re, subprocess, sys, time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import harness

ROOT = harness.ROOT
# name: (width, height, safe-area top, safe-area bottom), all in device pixels.
DEVICES = {
    "iphone17pro": (1206, 2622, 186, 102),
    "iphone17promax": (1320, 2868, 186, 102),
    "pixel9": (1080, 2424, 110, 63),
    "ipad": (1640, 2360, 72, 60),
}


def work_area_height():
    try:
        import ctypes
        from ctypes import wintypes
        r = wintypes.RECT()
        ctypes.windll.user32.SystemParametersInfoW(0x30, 0, ctypes.byref(r), 0)
        return r.bottom - r.top
    except Exception:
        return 1392


def window_size(device):
    w, h, _, _ = DEVICES[device]
    k = min(1.0, (work_area_height() - 80) / h)
    return int(w * k) // 2 * 2, int(h * k) // 2 * 2


def newest_asset():
    newest = 0.0
    for base, _, files in os.walk(os.path.join(ROOT, "Assets")):
        for f in files:
            if not f.endswith(".meta"):
                newest = max(newest, os.path.getmtime(os.path.join(base, f)))
    return newest


def ensure_build(build):
    stale = not os.path.exists(harness.EXE) or os.path.getmtime(harness.EXE) < newest_asset()
    if not stale:
        print("build is up to date")
        return
    if not build:
        print("WARNING: Build/Windows/GridGame.exe is older than Assets/ (or missing); pass --build to rebuild", flush=True)
        return
    print("building the Windows dev player ...", flush=True)
    r = subprocess.run(["powershell", "-NoProfile", "-File", os.path.join(ROOT, "tools", "unity.ps1"),
                        "-Method", "GridGame.EditorTools.GridGameBuild.BuildWindowsDev"])
    if r.returncode:
        sys.exit(f"build failed ({r.returncode})")


def refuse_or_replace(replace):
    others = harness._other_players()
    if not others:
        return
    names = ", ".join(f"PID {pid}" + (f" (run {run})" if run else "") for pid, run in others)
    if not replace:
        sys.exit(f"a GridGame player is already running: {names}. One game instance at a time; pass --replace to close it.")
    try:
        if harness.alive():
            harness.call("quit", timeout=3)
    except Exception:
        pass
    time.sleep(2)
    for pid, _ in harness._other_players():
        subprocess.run(["taskkill", "/F", "/T", "/PID", str(pid)], capture_output=True)
    time.sleep(1)


def bring_to_front(pid):
    ps = f"(New-Object -ComObject WScript.Shell).AppActivate({pid}) | Out-Null"
    subprocess.run(["powershell", "-NoProfile", "-Command", ps], capture_output=True, timeout=30)


def read_summary(log, timeout=60):
    end = time.time() + timeout
    while time.time() < end:
        try:
            text = open(log, encoding="utf-8", errors="replace").read()
        except OSError:
            text = ""
        m = re.search(r"\[Scenario\] (\{.*\})", text)
        if m:
            return json.loads(m.group(1))
        failed = re.search(r"\[Scenario\] -scenario .*", text)
        if failed:
            sys.exit("scenario failed: " + failed.group(0))
        time.sleep(0.5)
    return None


def describe(s):
    t = s["target"]
    if t.get("screen"):
        what = f"screen {t['screen']}"
    elif t.get("stage"):
        what = f"{t['kind']} {t['stage']}" + (f" wave {t['wave']}" if t.get("wave") else "")
    else:
        what = t["kind"]
    gear = "; ".join(f"{h}: {', '.join(g)}" for h, g in (s.get("gear") or {}).items()) or "none"
    reserve = f" (reserve: {', '.join(s['reserve'])})" if s.get("reserve") else ""
    caravan = f"open, level {s['caravan']}" if s.get("caravanOpen") else "not open"
    return "\n".join([
        f"target:  {what}",
        "party:   " + ", ".join(f"{p['hero']} L{p['level']}" for p in s["party"]) + reserve,
        f"gear:    {gear}",
        f"cleared: {s['cleared']} stages",
        f"caravan: {caravan}",
        f"gold:    {s['gold']}   food: {s['food']}" + ("   on the road" if s.get("onTheRoad") else ""),
    ])


def start(arg, device, run):
    harness._ensure_port()
    art = os.path.join(ROOT, "artifacts", run)
    saves = os.path.join(art, "saves")
    os.makedirs(saves, exist_ok=True)
    log = os.path.join(art, "player.log")
    if os.path.exists(log):
        os.remove(log)
    ww, wh = window_size(device)
    _, dh, top, bottom = DEVICES[device]
    cmd = [harness.EXE, "-automation", str(harness.PORT), "-artifacts", art, "-saveRoot", saves,
           "-screen-width", str(ww), "-screen-height", str(wh), "-screen-fullscreen", "0",
           "-logFile", log, "-scenario", arg]
    proc = subprocess.Popen(cmd, cwd=ROOT, close_fds=True,
                            creationflags=subprocess.DETACHED_PROCESS | subprocess.CREATE_NEW_PROCESS_GROUP)
    for _ in range(120):
        if harness.alive():
            break
        time.sleep(0.5)
    else:
        sys.exit("player did not start")
    harness.call("safearea", top=top, bottom=bottom, ref=dh)

    summary = read_summary(log)
    end = time.time() + 30
    screen = None
    while time.time() < end:
        try:
            screen = harness.call("state").get("screen")
        except Exception:
            screen = None
        if screen and screen not in ("splash", "title", "loading"):
            break
        time.sleep(0.5)
    time.sleep(2.5)
    return proc, summary, screen


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("scenario", nargs="?", help="a preset name or a target")
    ap.add_argument("--spec", help="a spec JSON file instead of a preset or target")
    ap.add_argument("--device", default="iphone17pro", choices=sorted(DEVICES))
    ap.add_argument("--replace", action="store_true", help="close a running GridGame player first")
    ap.add_argument("--build", action="store_true", help="rebuild the Windows dev player if it is stale")
    ap.add_argument("--no-show", action="store_true", help="skip the preview set")
    a = ap.parse_args()
    arg = os.path.abspath(a.spec) if a.spec else a.scenario
    if not arg:
        ap.error("give a preset, a target or --spec")
    name = re.sub(r"[^A-Za-z0-9_-]+", "-", os.path.splitext(os.path.basename(arg))[0] if a.spec else arg).strip("-")

    ensure_build(a.build)
    refuse_or_replace(a.replace)
    run = f"scenario-{name}"
    proc, summary, screen = start(arg, a.device, run)
    png = harness.shot(run)
    bring_to_front(proc.pid)

    print(f"{name} on {a.device} ({ww}x{wh}), screen: {screen}, player PID {proc.pid}, left open")
    if summary:
        print(describe(summary))
    else:
        print("(no [Scenario] summary in player.log)")
    print("screenshot:", png)
    if not a.no_show:
        subprocess.run(["powershell", "-NoProfile", "-File", r"D:\Projects\MindAttic\mindattic-agent-standard\preview.ps1",
                        "-Title", f"Scenario: {name}", "-Repo", "GridGame.Unity", "-File", png])


if __name__ == "__main__":
    main()
