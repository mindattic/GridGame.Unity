# Release checklist (GG-T1118)

What "release candidate" means for GridGame and where each gate lives. A candidate is ready when every gate in section 1 holds. Section 2 is steps only the owner can do; section 3 is the runs that wait for night. This page points at the sources and does not restate them: [BIBLE](BIBLE.md) section 8, [POLISH](POLISH.md), [RELEASE_SCOPE](RELEASE_SCOPE.md), [RELEASE_BUILD](RELEASE_BUILD.md) and [STORE_LISTING](STORE_LISTING.md).

Ground rules (the owner's):

- **Verification is build-only.** A gate is a build, a lint or a read of a doc. Agents do not run tests for a release check; the orchestrator runs the suite on main.
- **Soaks run only from 00:00 to 10:00** (section 3).
- **One device or game player at a time**, never in parallel (RAM).
- **Owner-only** steps are marked **(owner)** and are not agent work.

## 1. Gates

| # | Gate | How to check | Source |
|---|------|--------------|--------|
| 1 | Windows release build compiles | `pwsh tools/build.ps1 -Release`: zero `error CS` | [BIBLE 8](BIBLE.md#GG-§8) item 1 |
| 2 | Android release bundle builds | `pwsh tools/build.ps1 -Release -Platform Android` writes `Build/AndroidRelease/GridGame.aab`, zero `error CS` | [RELEASE_BUILD](RELEASE_BUILD.md) "Android (Google Play)", GG-T478 |
| 3 | BIBLE 8 quality bar | Every story in the candidate meets items 1-7 of BIBLE 8; anything short stays 🟡/⬜ ([GG-LAW-12](BIBLE.md#GG-LAW-12)) | [BIBLE 8](BIBLE.md#GG-§8) |
| 4 | Polish baseline holds | No dimension scores below `docs/polish/baseline.json` (recorded 2026-11-17); a drop fails the pass and gets a `fix` ticket | [GG-LAW-14](BIBLE.md#GG-LAW-14), [POLISH](POLISH.md) section 4, `tools/polish.py` |
| 5 | Release scope switches match the verdicts | `Assets/GridGame/Meta/Features.cs` has Crucible, bounties, caravan upgrades and barks, menu theme, font and wrap presets and the Camp Inn: Respec row off; compare against the verdict table | [RELEASE_SCOPE](RELEASE_SCOPE.md) "The switch" and "Verdicts", [BIBLE 8.1](BIBLE.md#81-release-scope-gg-t392-owner-2026-11-16), GG-T392 |
| 6 | Debug options need an explicit opt-in | A release build ignores `-allfeatures`, `-start`, `-saveRoot` and the other `App.CommandLine` flags unless launched with `-enableDebugOptions` | [BIBLE 8.1](BIBLE.md#81-release-scope-gg-t392-owner-2026-11-16), GG-T566 |
| 7 | Android size budget | `get-size total` on the bundle's `.apks` stays under 200 MB compressed download (not the .aab size) | [RELEASE_BUILD](RELEASE_BUILD.md) "Size budget", GG-T499 |
| 8 | Store copy is final | Name, subtitle, short and full description, keywords and ratings notes read right and the limits hold (80, 4000, 100 characters) | [STORE_LISTING](STORE_LISTING.md), GG-T489; the name itself is GG-T573 (owner) |
| 9 | Privacy policy has no open placeholders | No `[Owner: ...]` marker left in the policy; Data Safety and age-rating answers match it | `docs/legal/privacy-policy.md`, `docs/legal/play-data-safety.md`, `docs/legal/age-ratings.md`, GG-T472 |
| 10 | Crash reporter in place | `CrashReporter` writes C# exceptions to `<persistentDataPath>/Diagnostics/errors.jsonl`; native crashes show in Play Console Android vitals | `Assets/GridGame/App/Services/CrashReporter.cs`, [RELEASE_BUILD](RELEASE_BUILD.md) "Crash and error reporting", GG-T484 |
| 11 | Release content lint | `tools/release_lint.py` reports clean | GG-T1119 (in progress; this line holds once it lands) |
| 12 | Release player audit | The release player audit passes | GG-T1120 (in progress; this line holds once it lands) |

## 2. Owner-only

Every store item and its status, owner-only ones included: [release/StoreReadiness.md](release/StoreReadiness.md) (GG-T1509).

Each is already ticketed; this list only gathers them. None blocks the build gates above, but none of them can be closed by an agent.

- **(owner)** Choose the game's name: GG-T573. Then re-check gate 8.
- **(owner)** Store products setup: GG-T471.
- **(owner)** Google Play Console setup, including the Play Games app id for cloud save: GG-T1115 ([RELEASE_BUILD](RELEASE_BUILD.md) "Google Play Console (the owner)" and "Cloud save").
- **(owner)** iOS and Mac builds (needs a Mac and an Apple account): GG-T479 ([RELEASE_BUILD](RELEASE_BUILD.md) "Owner checklist").
- **(owner)** Gap art, generated and imported: GG-T492 ([ART_GAPS](ART_GAPS.md)).
- **(owner)** Missing music tracks: GG-T462 ([MUSIC](MUSIC.md)).
- **(owner)** Pick and buy the SFX library: GG-T463 ([SFX](SFX.md)).
- **(owner)** Privacy policy confirmations: the Unity Dashboard developer data choices and the target age group declared in Play Console, both marked in `docs/legal/privacy-policy.md` (GG-T472).
- **(owner)** Release phone check, one real phone, signed bundle ([RELEASE_BUILD](RELEASE_BUILD.md) "Testing a bundle on a phone"): GG-T1123, under GG-T493.
- **(owner)** Signing keystore, one time: [RELEASE_BUILD](RELEASE_BUILD.md) "Signing".

## 3. Night-only (00:00 to 10:00)

Start these only inside the window, one device or game player at a time, and finish or stop before 10:00.

- 300-encounter unattended soak: GG-T589.
- Polish re-baseline, the full recorded pass and new `docs/polish/baseline.json`: GG-T493 ([POLISH](POLISH.md) section 4, `python tools/polish.py`).
- Balance loop, `polish.py --balance` and triage of the lowest rows: GG-T276 (parked until the owner says "run the soaks").
- Any other multi-instance or recorded run, such as the nightly (`tools/nightly.ps1`).

## 4. Order

1. Gates 1-2 (build), then 5, 6 and 11-12 (cheap checks).
2. Gates 3, 4, 7, 8, 9, 10.
3. Night section, then the owner section.
4. Cut the candidate only when sections 1 and 3 are clean and every section 2 line is closed or accepted by the owner.
