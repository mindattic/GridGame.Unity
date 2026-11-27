# Endless stability: 300-encounter soak (GG-T589 / GG-T1191)

**Status: PARTIAL.** The run was stopped at 69 of 300 encounters (03:37, 2026-11-26) because the
orchestrator's work-unit budget said to stop, not because the game failed. GG-T1191 stays open for
a full rerun.

**Second attempt (2026-11-27 00:45, PARTIAL, 21 / 300).** Same command, same build. It was stopped at
01:17 after 21 encounters (about 1.5 min each) because the work-unit budget ran out, not because the
game failed. Free RAM was 4.1 GB at start and fell as low as 0.9 GB at 00:56-01:01 from other host load,
yet the game kept running. 21 / 21 victories, 0 crashes, hangs, stalls or relaunches, and no thrown
exceptions: the 145 "Exception" matches in Player.log are stack frames of the capture and telemetry
log helpers. 21 / 21 `records/*.json` parse, `saves` again holds only the throwaway profile's
`current.txt` and `settings.json`, `liveOldScreens` stays 0-1, and there are no growth warnings.

| enc | min | managedMB | nativeAllocMB | privateMB | workingSetMB | unityObjects | textures |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 0 | 0.1 | 14.1 | 49.7 | 375 | 219 | 1857 | 78 |
| 10 | 16.0 | 32.0 | 79.7 | 538 | 189 | 2131 | 212 |
| 20 | 28.6 | 50.9 | 82.1 | 556 | 236 | 2132 | 219 |

The outside sampler read private bytes of 513, 522, 523, 539, 554 and 570 MB at five-minute intervals.
That is the same climb of about 1.5 MB per encounter that the first run showed. Textures and Unity
objects are lower than in the first run (219 against 636 at enc 20) and almost flat. Only a full run,
which needs one uninterrupted ~6-8 h night window, can settle the flatness criterion.

**Third attempt (GG-T1253, 2026-11-27 01:52, PARTIAL, 7 / 300).** Same command, same build. Free RAM
was 6.2 GB at start. At about 02:00, Claude Code's background-shell memory reaper killed the harness
shell, and GridGame.exe went with it. 7 / 7 victories with no crash, hang or stall before the kill.
The outside sampler read working set / private bytes of 265 / 416 MB at 01:52 and 346 / 522 MB at
01:57, while free RAM fell from 5.5 to 4.4 GB. Player.log grew to 40 MB in 7 encounters (about
5.5 MB per encounter), so a full run would write about 1.6 GB of log. The next attempt needs a host
that is not under memory pressure from other CLIs. Run it from a plain terminal, outside a Claude
Code background shell, or with `CLAUDE_CODE_DISABLE_BG_SHELL_PRESSURE_REAP=1` set when Claude Code
starts.

## Run

- Command: `python tools/harness.py endless 300 --seed 61000 --speed 8 --headless --leakprobe --run gg-t589-endless300`
  (`--headless` is a real global harness flag (GG-T314). It makes the player launch with `-batchmode -nographics`.)
- Build: `Build/Windows/GridGame.exe` of 2026-11-23 (includes the GG-T375 capture-engine fix c433c23).
- Host: 32 GB. Free RAM was 5.7 GB at start and stayed between 4.6 and 5.4 GB. It never got near the
  1.5 GB floor during the run. After the stop, Claude Code reaped two of its idle watcher shells for
  host memory pressure; the game itself was not affected.
- Started 02:09:50 and stopped 03:37 (87 min, one launch). Encounters took 51-87 s each, about 75 s on
  average, so a full 300 would take about 6.2 h.

## Results (encounters 1-69)

| Criterion | Result |
|---|---|
| Encounters completed | 69 / 300 (66 victories, 3 defeats), seeds 61000-61068 |
| Crashes / hangs / relaunches | 0 / 0 / 0 (one launch) |
| Stall-guard "stalled" encounters | 0 |
| Exception lines in Player.log | 0 |
| `artifacts/<run>/saves` | No save slot written. It holds only `Profiles/current.txt` and `Profiles/Throwaway/settings.json`, the profile pointer and settings the throwaway profile creates. |
| `records/*.json` | 69 / 69 parse (Python `json.load`). Windows PowerShell 5.1's `ConvertFrom-Json` rejects all of them, which is a quirk of that reader, not corruption. |
| `liveOldScreens` | 0-1 every sample, as expected |
| Growth warnings | **Yes**, at encounter 60: `privateMB`, `workingSetMB`, `unityObjects`, `textures` |

## Memory curve (endless-stability.csv, a full GC plus UnloadUnusedAssets at every sample)

| enc | managedMB | nativeAllocMB | privateMB | workingSetMB | unityObjects | textures |
|---:|---:|---:|---:|---:|---:|---:|
| 0 | 16.1 | 59.5 | 395 | 240 | 1852 | 74 |
| 10 | 41.6 | 82.0 | 515 | 361 | 2477 | 480 |
| 20 | 45.6 | 83.0 | 533 | 380 | 2687 | 636 |
| 30 | 40.7 | 83.4 | 534 | 381 | 2761 | 695 |
| 40 | 52.7 | 86.3 | 552 | 398 | 2828 | 749 |
| 50 | 49.7 | 85.7 | 553 | 399 | 2843 | 763 |
| 60 | 54.0 | 85.8 | 587 | 428 | 2862 | 779 |

The outside sampler (GridGame.exe WorkingSet64 every 5 minutes) shows the same staircase: 355 MB
at encounter 4, 361 at 8-16, 380 at 20-29, 396-399 at 36-52, and 427-428 at 55-68.

- Managed heap and Unity native allocation are flat after encounter 10. Managed swings between 40
  and 54 MB (+0.25 MB per encounter, within GC noise), and native allocation sits at 82-86 MB.
- Private bytes and working set climb about 1.4 MB per encounter. They rise in steps, with plateaus
  in between.
- Texture and Unity-object counts keep rising, but more slowly over time: textures go up 156, 59,
  54, 14 and 16 per 10 encounters.

## Leak probe (one line per encounter)

- Screens alive: `BattleScreen=1-2`, which is the current battle plus the one just retired. No
  older screens are kept, and `battleStates` stays at 1-2.
- `captureEngines=1` and `captureRestarts=1` throughout, so the GG-T375 capture-engine leak stays fixed.
- `sfxCueLog=300` (capped), `boardAuditRecent=0`, `overlayChildren=1-6`, `visualElements=625-858`:
  all bounded.
- `artTextures` (the Art cache by path) went 294 → 407 → 445 → 494 → 507 → 520 → 524 at
  encounters 10-69. This is the one cache still growing. It matches the `textures` growth warning,
  and it slows as new seeds introduce fewer new sprites.

## Verdict so far

No crash, hang, stall, exception or save corruption appeared in 69 encounters. The monotonic-growth
warnings mean the memory-flatness criterion is **not met yet**. The likely cause is the unbounded Art
texture cache, plus the native and private memory those textures hold. It is filed as GG-T1193.
It may level off once every enemy and board sprite has been seen, but only the full 300-encounter
run can show that.

Evidence (not committed): `artifacts/gg-t589-endless300/` (player-1.log, endless-stability.csv,
records/) and `D:\Projects\MindAttic.worktrees\.scratch\GG-T1191\` (harness.log, workingset.csv).
