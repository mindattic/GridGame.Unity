---
layer: release
status: living
---
# GridGame.Unity — Release charter

> The one finish line this repo is working toward. Every open ticket in this repo sits under one of the epics
> below, and every epic is part of the milestone. Work that does not fit is out of scope until this milestone is
> reached (owner, 2026-11-27, MAT-T319).

## 1. What this is
A portrait-mobile tactics RPG (Terra Battle pincers on a Grandia II-style timeline, plus an FF-style Camp) for phone
players, built in Unity 6 on a deterministic, engine-free battle core.

## 2. The milestone: GridGame 1.0 on Google Play {#milestone}
- **Milestone:** MS-27, "GridGame 1.0: a polished, releasable game on Google Play"
- **Published means:** a signed Android App Bundle released to production on Google Play (staged rollout), found and
  installed from the Play Store.
- **The natural stopping point:** a new player installs the game, meets the MindAttic splash, creates their
  Commander, learns the board in five short drills, and plays the whole campaign to the ending through one coherent
  hub: the Commander story and regional lore told in crawls and beats, heroes met in scenes and bound by Trust and
  cohesion, abilities arriving steadily, heroes summoned on fair, disclosed terms, every screen on one kit, with
  complete audio and art, a lawful store and privacy story, and a build that survives a long session on a phone.
  The owner's own words: *"a polished, releasable game once the story has been established ... refine and balance
  everything that exists, cut or at least disable that which doesn't"* (RELEASE_SCOPE, GG-T392), extended by the
  owner's 2026-11-27 launch slices (GG-T1256).

This charter promotes and links the existing release docs rather than repeating them:
- [RELEASE_SCOPE.md](RELEASE_SCOPE.md): the core loop, what is kept, hidden (`Meta/Features.cs`) or cut, and the
  balance record (the postgame XP wall, decided 5b).
- [RELEASE_CHECKLIST.md](RELEASE_CHECKLIST.md): the release-candidate gates, the owner-only steps and the run order.
- [release/StoreReadiness.md](release/StoreReadiness.md): the store readiness list (GG-T1509).
- [RELEASE_BUILD.md](RELEASE_BUILD.md), [STORE_LISTING.md](STORE_LISTING.md), [legal/](legal/).

## 3. Done when (acceptance checks)
Each line is also a `tickets milestone check`.
1. build — `pwsh tools/build.ps1 -Release -Platform Android` writes `Build/AndroidRelease/GridGame.aab` with zero
   `error CS` (RELEASE_CHECKLIST gates 1-2).
2. harness — the release stability check, run once on the release candidate, records no crash, hang or memory drift
   in `docs/EndlessStability.md`.
3. exists — `docs/release/StoreReadiness.md` has every Google Play row done or accepted by the owner.
4. signoff — the owner installs the production build from Google Play and plays a fresh install from the splash
   through the drills into the campaign.

## 4. Epics, in priority order
| Priority | Epic (ticket) | What it delivers | Features |
|---|---|---|---|
| 900 | GG-T1258 Front door and onboarding | Splash to Camp for a new player | GG-T1316 Studio splash, legal and Main Menu (990), GG-T1336 Commander profile on first run (980), GG-T1351 Start Campaign and opening crawl (970), GG-T1369 Tutorial drills (960), GG-T1388 Onboarding skip, resume and graduation (950), GG-T1403 Front-door polish and close-out (940) |
| 800 | GG-T1270 Commander and world lore | The Commander story, told through canon, crawls and beats | GG-T1303 Commander canon and arc, incl. the battle-role spec (890), GG-T1387 Commander replaces the Warden (880), GG-T1474 Crawls and the story arc (860); GG-T1469 Regional lore landed during triage |
| 700 | GG-T1257 Forming the team: Trust and cohesion | Heroes met in scenes and bound by Trust and cohesion | GG-T1259 Bonds foundation, incl. the dialogue tree compiler (790), GG-T1264 Meeting the team (780), GG-T1261 Bonds in camp and conversation (770), GG-T1262 Bonds earned and paid off in battle (760), GG-T1265 Core cast bond content (750) |
| 600 | GG-T1289 Hub, progression, roster and monetization | One hub, steady abilities, fair Summon and Shop | GG-T1320 Hub root and quick return (690), GG-T1365 One look and feel (680), GG-T1393 Abilities keep coming (670), GG-T1410 Roster acquisition and Summon (660), GG-T1441 Shop, offers and fair play (650) |
| 500 | GG-T1476 Release and Google Play | Everything between the build and the Play Store | GG-T1479 Release pipeline, versioning and readiness list (590), GG-T1568 Audio and art complete (580), GG-T1483 Privacy, legal and player data (570), GG-T1484 Phone quality (560), GG-T1481 Google Play release (550), GG-T1569 Release stability check, run once at RC (540) |

## 5. Out of scope (not before this milestone)
A new request that lands here is filed `proposed`, never approved automatically.
- **iOS and the App Store** (GG-T1482 and its tasks, iCloud save GG-T1127..T1129, GG-T479): needs a Mac and an Apple
  account; Google Play ships first, iOS is the next milestone.
- **Turn-mode experiments** (GG-T762, GG-T819..T822): the default PartyWindow ships; the other modes stay as they are.
- **Soaks, reruns and continuous loops** (GG-T61, T589, T1191, T1253, T1254, T276, the nightly): replaced by one
  release stability check at the release candidate. The GridGame.Nightly scheduler job stays off (Legion,
  2026-11-27, 8/8 "Off").
- **Season pass and cosmetics** (GG-T1452): new systems after launch.
- **The Commander in battle** (advice card and orders, GG-T1453): a new combat system; the hint stays as it is.
- **Duo supports** (GG-T1280, T1310, T1311) and the bond night balance run (GG-T1281, T1312): bonds ship with
  Trust tiers and bonded pincers/supports only.
- **Dialogue tree graph page** (GG-T1271, T1296): authoring tool nicety.
- **Hidden systems** stay hidden (RELEASE_SCOPE "The switch"): Enchantress, taming, Crucible/bounty switches as
  recorded there. Gambits, monster meat, spell augmentation (GG-T406): out.
- **Skill-hex bot soaks** (GG-T1048, T1189) and the trailer (GG-T488).
- An exhaustive bughunt (GG-T927): bugs found in kept code are filed one by one.

## 6. Current verified state
- Core loop playable end to end: run 8 finished the main campaign through the real UI, 24/24 main stages first try
  (RELEASE_SCOPE, GG-T448/GG-T452); the postgame XP wall answered with catch-up XP (GG-T474, 5b).
- Release gates in place: Windows and Android release builds (`tools/build.ps1`), release scope switches
  (`Meta/Features.cs`), debug-option opt-in (GG-T566), crash reporter (GG-T484), release lint (`tools/release_lint.py`).
- Front-door design page (`docs/design/front-door/`, GG-T1314, `3f106fec`), tutorial drills design (GG-T1315,
  `4202c771`), hub navigation design (GG-T1328, `ba7e10d9`), bonds design BIBLE 25.20 (GG-T1290, `5f3a804a`),
  Commander canon (GG-T1305, `2ff60201`), summon design (GG-T1413, `a240235a`), monetization page (GG-T1444,
  `d44e8e38`), StoreReadiness list (GG-T1509, `38183a80`), `tools/play_upload.py` (`82de2cf5`),
  `tools/check_aab.py` (`05a1d5f6`).
- Scoped texture loading (GG-T1202..T1207, `0e4f1b0d`, `2c99f3b5`).

## 7. Decision record
- 2026-11-27 — charter drafted from README, BIBLE, RELEASE_SCOPE, RELEASE_CHECKLIST, the GG-T1256 launch slices
  and the open tickets; Legion vote: Approve, 7 of 9 (plurality quorum reached, 9 of 9 answered); dissent: two voters wanted iOS/App Store kept in 1.0 (and one the Commander battle role); kept out, since iOS needs a Mac and the battle-role spec stays in scope.
- 2026-11-27 — GridGame.Nightly: Legion "Off" (8 of 9 voters answered, all Off): it stays disabled; tests and soaks
  run on an owner /test and once at the release candidate.
- Triage: 369 open → done 6, duplicate 2, wont-do 85 (incl. emptied containers merged into the kept features), kept 228 (plus this job ticket); 47 more were closed by other sessions landing their work during the triage. New: features GG-T1568 Audio and art complete and GG-T1569 Release stability check, task GG-T1570 (run the RC check once); GG-T596 reworded as the RC balance tune.
