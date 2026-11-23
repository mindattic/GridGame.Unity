"""GG-T448: play a fresh save the way a careful player would, logging the economy at every camp visit, to get a
real feel for progression and the buying/repairing/upgrading loop rather than guessing at prices in the abstract.

    python tools/playthrough_sim.py [RUN [MAX_BATTLES]] [--nocapture]
    # RUN defaults to "playthrough", MAX_BATTLES to 20. Needs the dev player built (tools/build.ps1) first.

Battles are autoplayed (the game's own bot, GG-T448's "economy" automation endpoint supplies the camp numbers);
camp decisions go through the real UI, the way a player's thumb would: forge/equip the first guided weapon and
armor piece once the Blacksmith's workshop opens (so gear actually wears and repair gets exercised), repair worn
gear, build the next facility once affordable with a cushion, buy potions, buy the cheapest useful Caravan
upgrade once affordable, and refill Rested through the real gate when it runs dry. Writes a JSONL log of every
camp visit and battle to artifacts/<RUN>/play.jsonl (gold, Rested, wear, party HP, every purchase/repair/build
decision), alongside a screenshot per camp and per result and (unless --nocapture) a recorded .mp4 per battle.

Not part of any automated suite — a manual tool for a played-through balance pass, invoked when asked, same as
tools/harness.py's other commands. Two real bugs surfaced and were fixed here while building it (2026-11-19):
  - tap_row(): a tab switch (Buy/Sell, Forge/Repair, …) rebuilds the list fresh; calling scrollto immediately
    after can race Unity's own layout pass, so ScrollView.ScrollTo computes against a not-yet-laid-out content
    height and silently fails to move — the next click then lands on the row's stale pre-scroll position, often
    far below the window ("tap at (1004,12774) hit nothing"). Retrying with a longer settle each time fixes it.
  - The battle-retry loop: a lost battle inside a multi-battle expedition restarts it from battle 1 by design
    (GG-T17, the same rule tools/harness.py's own campaign() already works around) — the camp's stage list no
    longer offers a direct entry into battle 2/3 once the defeat abandoned the expedition ("Expeditions start at
    battle 1"), so a retry has to tap the real "Retry" button on the post-battle screen itself, not go back to
    camp and re-pick a raw stage id.
"""
import json
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import harness as H
from harness import call

RUN = sys.argv[1] if len(sys.argv) > 1 and not sys.argv[1].startswith("-") else "playthrough"
MAX_BATTLES = int(sys.argv[2]) if len(sys.argv) > 2 and not sys.argv[2].startswith("-") else 20
CAPTURE = "--nocapture" not in sys.argv
# --resume: drive the player already running on this port (its save as it stands) instead of launching a fresh one,
# appending to the run's log. --keep: leave the player running at the end. Together they let a long campaign run as a
# chain of short foreground chunks (`--resume --keep`, MAX_BATTLES battles each), which the CLI's idle-time memory
# reaper never stops the way it stops a single hour-long background run (GG-T448).
RESUME = "--resume" in sys.argv
KEEP = "--keep" in sys.argv
ROOT = H.ROOT
LOG = os.path.join(ROOT, "artifacts", RUN, "play.jsonl")
CONTENT = os.path.join(ROOT, "Assets", "GridGame", "Resources", "Content")

_campaign = json.load(open(os.path.join(CONTENT, "campaign.json"), encoding="utf-8"))
_themes = _campaign["Themes"]
THEME_OF = {sid: (t.get("Id") or t.get("id")) for t in _themes for sid in (t.get("Stages") or t.get("stages"))}
# The game's own play order (ContentDb.CampaignStages: themes in order, stages in order) -- the index
# HighestClearedStage counts in. stages.json also holds test and synthetic stages, so its file order isn't it.
STAGE_IDS = [sid for t in _themes for sid in (t.get("Stages") or t.get("stages"))]


def log(kind, **kw):
    kw["kind"] = kind
    kw["t"] = round(time.time() - T0, 1)
    with open(LOG, "a", encoding="utf-8") as f:
        f.write(json.dumps(kw) + "\n")
    print(kind, json.dumps({k: v for k, v in kw.items() if k not in ("stock", "inventory", "caravan")})[:700], flush=True)


def tap_row(name, tries=4, settle=0.5):
    """scrollto + click a row inside a list that was just rebuilt (a tab switch, a fresh screen). See the module
    docstring for why a bare scrollto+click can miss right after a rebuild, and why retrying with a longer
    settle each time is the fix."""
    r = None
    for i in range(tries):
        call("scrollto", name=name)
        time.sleep(settle + i * 0.3)
        r = call("click", name=name)
        if not H._tap_failed(r):
            return r
        time.sleep(0.3)
    return r


def wait_screen(names, timeout=15):
    end = time.time() + timeout
    while time.time() < end:
        sc = call("state")["screen"]
        if sc in names:
            return sc
        time.sleep(0.4)
    return call("state")["screen"]


def to_hub():
    for _ in range(6):
        sc = call("state")["screen"]
        if sc == "hub":
            return True
        if sc == "battle":  # a resumed chunk can land mid-battle: let autoplay finish it rather than back out of it
            H._play_battle("resumed-battle", timeout=600)
            time.sleep(1.2)
            H._skip_levelups(call("state"))
            continue
        if sc in ("crawl", "dialogue"):
            H.through_story(wait=1.0)
        elif sc == "levelup":
            H._skip_levelups(call("state"))
        elif sc == "postbattle":
            call("click", name="continue")  # GG-T513: the results button reads "To Camp" mid-expedition
            time.sleep(1.5)
        else:
            call("back")
            time.sleep(0.8)
    call("goto", screen="hub")
    time.sleep(1)
    return call("state")["screen"] == "hub"


def forge_starter(decisions):
    """Craft the game's guided first weapon/armor once the Blacksmith's workshop is open (GG-T177/178): this is what
    actually puts durability-bearing gear on a hero, so repair has something real to do later."""
    camp_d = call("camp")
    if not camp_d.get("workshop"):
        return
    for kind in ("firstForge", "firstArmor"):
        info = camp_d.get(kind)
        if not info or not info.get("ready"):
            continue
        if not H._goto("blacksmith"):
            continue
        call("click", name="tab-forge")
        time.sleep(0.6)
        recipe_names = call("names", prefix="recipe-")
        target = next((n for n in recipe_names if info["recipe"] in n), None)
        if not target:
            call("back")
            time.sleep(0.5)
            continue
        g = call("economy")["Gold"]
        tap_row(target)
        time.sleep(0.4)
        call("click", name="craft")
        time.sleep(0.8)
        decisions.append(f"forge {info['recipe']} ({kind}) for {info.get('hero')}: gold {g}->{call('economy')['Gold']}")
        call("back")
        time.sleep(0.6)
        camp_d = call("camp")


def forge_upgrades(econ, decisions, cap=4):
    """Craft affordable Forge recipes beyond the one guided starter piece (GG-T448): Crafting.Craft only ever adds
    the result to the bag -- it never equips anyone -- so without this step the Blacksmith stays visited but idle
    for the rest of the campaign and gear_up_party() below has nothing new to find. camp()["forge"] is already
    filtered to affordable + makeable + unlocked, so anything in it can be crafted right now; re-read fresh after
    each craft since gold (and the list) moves. Capped per visit so forging doesn't eat every potion/facility visit
    could otherwise afford too."""
    if "blacksmith" not in econ["npcs"] or not call("camp").get("workshop"):
        return
    if not H._goto("blacksmith"):
        return
    call("click", name="tab-forge")
    time.sleep(0.6)
    made = 0
    while made < cap:
        opts = call("camp").get("forge") or []
        if not opts:
            break
        r = opts[0]
        names = call("names", prefix=f"recipe-{r['Id']}")
        if not names:
            break
        g = call("economy")["Gold"]
        tr = tap_row(names[0])
        if H._tap_failed(tr):
            decisions.append(f"recipe-{r['Id']} tap failed: {tr}")
            break
        time.sleep(0.4)
        cr = call("click", name="craft")
        time.sleep(0.6)
        if H._tap_failed(cr):
            break
        g2 = call("economy")["Gold"]
        if g2 >= g:  # nothing spent: the craft didn't actually go through
            break
        decisions.append(f"forge {r['Id']} -> {r['Result']}: gold {g}->{g2}")
        made += 1
    call("back")
    time.sleep(0.6)


def repair_worn(econ, decisions):
    worn = [w for w in econ["wear"] if w["Max"] > 0 and w["Durability"] < w["Max"] * 0.7 and w["cost"] > 0]
    if not worn or "blacksmith" not in econ["npcs"]:
        return
    if not H._goto("blacksmith"):
        return
    call("click", name="tab-repair")
    time.sleep(0.7)
    for w in sorted(worn, key=lambda w: w["Durability"] / w["Max"]):
        g = call("economy")["Gold"]
        if g < w["cost"]:
            decisions.append(f"can't afford repair {w['hero']} {w['slot']} {w['item']} ({w['cost']}g, have {g})")
            continue
        r = tap_row(f"repair-{w['hero']}-{w['slot']}")
        time.sleep(0.3)
        if H._tap_failed(r):
            decisions.append(f"repair-{w['hero']}-{w['slot']} tap failed: {r}")
            continue
        if call("names", prefix="confirm-"):
            call("clicktext", text="Repair")
            time.sleep(0.5)
        g2 = call("economy")["Gold"]
        decisions.append(f"repair {w['hero']} {w['slot']} {w['Durability']}/{w['Max']} for {g - g2}g (quoted {w['cost']})")
    call("back")
    time.sleep(0.6)


def gear_up_party(econ, decisions):
    """Equip every empty weapon/armor/jewelry slot on every party hero from the best owned, proficient item in the
    bag (GG-T448): without this, only the one hero the game's own tutorial walks through a guided forge ever gets
    anything equipped at all -- a scripted playthrough that stops there hits a difficulty wall that's an artifact of
    the script, not the game (confirmed: five of six heroes fought 22 stages bare-handed before this existed).
    Equipment.Candidates sorts by rarity descending, so "equip-ok-<id>" rows come back in best-first order and the
    first one is always the best the hero can currently wear."""
    equipped = econ.get("equipped") or {}
    party = [h["ClassId"] for h in econ["heroes"] if h["inParty"]]
    empties = [(hero, slot) for hero in party for slot, item in (equipped.get(hero) or {}).items() if item is None]
    if not empties or not H._goto("equip"):
        return
    current_hero = None
    for hero, slot in empties:
        if hero != current_hero:
            r = tap_row(f"hero-pick-{hero}", tries=3)
            if H._tap_failed(r):
                decisions.append(f"hero-pick-{hero} tap failed: {r}")
                continue
            current_hero = hero
        r = tap_row(f"equip-slot-{slot}", tries=3)
        if H._tap_failed(r):
            continue  # a slot with no candidates at all doesn't even get a bag; not worth logging every miss
        time.sleep(0.3)
        ok = call("names", prefix="equip-ok-")
        if not ok:
            continue
        best = ok[0]  # "names" returns DOM/document order, which is Candidates' own rarity-desc order
        tap_row(best, tries=3)  # preview
        time.sleep(0.3)
        r = tap_row(best, tries=3)  # confirm -- same row, same name, after the list's own rebuild
        if not H._tap_failed(r):
            decisions.append(f"equip {hero} {slot}: {best[len('equip-ok-'):]}")
    call("back")
    time.sleep(0.6)


def build_facilities(econ, decisions):
    for station, screen, tab in (("forge", "blacksmith", "tab-forge"), ("alembic", "alchemist", None)):
        c = next((x for x in econ["caravan"] if x["Id"] == station), None)
        if not c or not c["next"]:
            continue
        cost = c["next"]["Cost"]
        if econ["Gold"] >= cost * 2 and H._goto(screen):
            if tab:
                call("click", name=tab)
                time.sleep(0.6)
            g = call("economy")["Gold"]
            r = tap_row("facility-build")
            if H._tap_failed(r):
                decisions.append(f"facility-build tap failed: {r}")
                call("back")
                time.sleep(0.6)
                continue
            time.sleep(0.5)
            if call("names", prefix="confirm-"):
                call("clicktext", text="Build")
                time.sleep(0.8)
            decisions.append(f"build {c['next']['Name']} cost {cost}: gold {g}->{call('economy')['Gold']}")
            call("back")
            time.sleep(0.6)
            econ = call("economy")


def buy_potions(econ, decisions):
    if "merchant" not in econ["npcs"]:
        return
    pots = [i for i in econ["stock"] if "potion" in i["Id"] and i["price"] <= max(1, econ["Gold"] * 0.15) and i["have"] < 3]
    if not pots or not H._goto("shop"):
        return
    call("click", name="tab-buy")
    time.sleep(0.6)
    p = min(pots, key=lambda i: i["price"])
    want = 3 - p["have"]
    g = call("economy")["Gold"]
    bought = 0
    for _ in range(want):
        r = tap_row(f"cart-plus-{p['Id']}")
        time.sleep(0.25)
        if H._tap_failed(r):
            decisions.append(f"cart-plus-{p['Id']} tap failed: {r}")
        else:
            bought += 1
    if bought == 0:
        decisions.append(f"buy {p['Id']}: every cart-plus tap failed, skipping commit")
        call("back")
        time.sleep(0.6)
        return
    call("click", name="shop-commit")
    time.sleep(0.8)
    decisions.append(f"buy {want}x {p['Id']} @{p['price']}: gold {g}->{call('economy')['Gold']}")
    call("back")
    time.sleep(0.6)


def buy_caravan(econ, decisions):
    opts = [c for c in econ["caravan"] if c["next"] and c["Id"] not in ("forge", "alembic", "enchanter_wagon")
            and c["next"]["Cost"] * 3 <= econ["Gold"]]
    if not opts or not H._goto("caravan"):
        return
    c = min(opts, key=lambda c: c["next"]["Cost"])
    g = call("economy")["Gold"]
    r = tap_row(f"caravan-buy-{c['Id']}")
    time.sleep(0.6)
    if H._tap_failed(r):
        decisions.append(f"caravan-buy-{c['Id']} tap failed: {r}")
    else:
        if call("names", prefix="confirm-"):
            call("clicktext", text="Buy")
            time.sleep(0.5)
        decisions.append(f"caravan {c['Id']} lv{c['level'] + 1} cost {c['next']['Cost']}: gold {g}->{call('economy')['Gold']}")
    call("back")
    time.sleep(0.6)


def next_stage(econ):
    cleared = econ["HighestClearedStage"]
    return STAGE_IDS[cleared + 1] if cleared + 1 < len(STAGE_IDS) else None


def launch_stage(sid, decisions):
    if not H._goto("stages"):
        call("clicktext", text="Adventure")
        time.sleep(0.8)
    theme = THEME_OF.get(sid)
    if call("names", prefix=f"expedition-{theme}"):
        call("click", name=f"expedition-{theme}")
        time.sleep(0.6)
    if not call("names", prefix=f"stage-{sid}"):
        if call("names", prefix="regions-back"):
            call("click", name="regions-back")
            time.sleep(0.5)
            call("click", name=f"expedition-{theme}")
            time.sleep(0.6)
    tap_row(f"stage-{sid}")
    time.sleep(1.0)
    texts = call("texts")
    if any("Refill Rested" in t for t in texts):
        econ = call("economy")
        decisions.append(f"Rested empty at stage {sid}: refill {econ['refill']['gold']}g (have {econ['Gold']}), diamonds {econ['diamonds']}")
        call("clicktext", text="Refill")
        time.sleep(3.5)
    sc = wait_screen(("battle", "crawl", "dialogue"), 12)
    if sc in ("crawl", "dialogue"):
        H.through_story(wait=1.2)
    return wait_screen(("battle",), 15) == "battle"


def main():
    global T0
    T0 = time.time()
    os.makedirs(os.path.dirname(LOG), exist_ok=True)
    if RESUME:
        if not H.alive():
            print("--resume: no player answering on this port")
            sys.exit(1)
    else:
        H.launch(RUN, fresh=True, extra=(("-capture", "video", "hq") if CAPTURE else ()) + ("-rested",))
        open(LOG, "w").close()
        H._wait_screen("title", 20)
        time.sleep(0.8)
        H._tap_until(lambda: call("clicktext", text="New Game"), lambda: any("Slot 1" in t for t in call("texts")), tries=4)
        H._tap_until(lambda: call("clicktext", text="Slot 1"), lambda: call("state")["screen"] == "charselect", tries=4, wait=1.0)
        if not H.pick_party():
            print("party pick failed")
            sys.exit(1)
    if KEEP:
        H._launched = False  # harness's atexit hook only quits a player this process launched
    call("speed", x=3)
    first = 0
    if RESUME and os.path.exists(LOG):
        first = sum(1 for l in open(LOG, encoding="utf-8") if '"kind": "camp"' in l)
    for n in range(first, first + MAX_BATTLES):
        to_hub()
        econ = call("economy")
        decisions = []
        forge_starter(decisions)
        econ = call("economy")
        forge_upgrades(econ, decisions)
        econ = call("economy")
        gear_up_party(econ, decisions)
        econ = call("economy")
        repair_worn(econ, decisions)
        econ = call("economy")
        build_facilities(econ, decisions)
        econ = call("economy")
        buy_potions(econ, decisions)
        econ = call("economy")
        buy_caravan(econ, decisions)
        econ = call("economy")
        sid = next_stage(econ)
        log("camp", n=n, gold=econ["Gold"], diamonds=econ["diamonds"], rested=econ["rested"], refill=econ["refill"], cleared=econ["HighestClearedStage"],
            next=sid, heroes=[(h["ClassId"], h["level"], h["hp"], h["max"], h["WeaponId"]) for h in econ["heroes"] if h["inParty"]],
            roster=len(econ["heroes"]), wear=[(w["hero"], w["slot"], w["Durability"], w["Max"], w["cost"]) for w in econ["wear"]],
            npcs=econ["npcs"], caravan=[(c["Id"], c["level"]) for c in econ["caravan"]], decisions=decisions)
        H.shot(f"camp-{n:02d}")
        if sid is None:
            log("done", reason="campaign finished")
            break
        if not launch_stage(sid, decisions):
            log("stuck", where="launch", stage=sid, screen=call("state")["screen"], texts=call("texts")[:20])
            break
        t = time.time()
        try:
            outcome, st = H._play_battle(f"b{n:02d}-{sid}", timeout=600)
        except Exception as e:  # a one-off automation 500 (seen once in playthrough5): end the chunk, the player lives on
            log("stuck", where="battle-http", stage=sid, error=str(e)[:200])
            break
        time.sleep(1.2)
        H._skip_levelups(call("state"))
        won = not call("names", prefix="retry") and not any(tx == "Retry" for tx in call("texts"))
        # A lost battle inside a multi-battle expedition restarts it from battle 1 by design (GG-T17, the same rule
        # tools/harness.py's own campaign() already works around): the camp's stage list no longer offers a direct
        # entry into stage 2/3 once the expedition's defeat abandoned it ("Expeditions start at battle 1"), so a
        # retry must tap the real "Retry" button on THIS screen, not go back to camp and re-pick a raw stage id.
        attempt = 0
        while not won and attempt < 3:
            attempt += 1
            r = call("clicktext", text="Retry")
            if isinstance(r, dict) and r.get("error"):
                won = True  # no Retry button after all: it was actually a win
                break
            time.sleep(1.5)
            for _ in range(20):
                if call("state")["screen"] == "battle":
                    break
                time.sleep(0.2)
            outcome, st = H._play_battle(f"b{n:02d}-{sid}-retry{attempt}", timeout=600)
            time.sleep(1.2)
            H._skip_levelups(call("state"))
            won = not call("names", prefix="retry") and not any(tx == "Retry" for tx in call("texts"))
        res = call("state").get("save", {})
        log("battle", n=n, stage=sid, outcome=outcome, won=won, retries=attempt, secs=round(time.time() - t), gold=res.get("Gold"), levels=res.get("levels"), hp=res.get("hp"))
        H.shot(f"result-{n:02d}")
        if outcome != "ended" or not won:
            log("stuck", where="battle", reason="DIFFICULTY_WALL" if outcome == "ended" else outcome, stage=sid)
            break
    errs = [l[:300] for l in call("logs") if l.startswith(("Error", "Exception")) and "first mouse" not in l]
    log("end", errors=errs[:10])
    if KEEP:
        return
    try:
        call("quit", timeout=3)
    except Exception:
        pass


if __name__ == "__main__":
    main()
