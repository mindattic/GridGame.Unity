# Gameplay trailer plan (GG-T488)

Target: one 45 s portrait (9:16, 1080x1920) cut, hard ceiling 60 s, built from `-capture video hq` footage of the
real player and the shipped music. This is the storyboard, the capture steps and the assembly recipe; the footage
itself is not captured yet (see Status).

## Status

- Prep done (this file). Footage not captured and nothing cut: the capture needs a player running for several
  minutes under the heavy-process lease, and the cut needs the clips watched shot by shot.
- Left to do: capture the shots below, pick the best take of each, run the assembly, drop the result at
  `docs/store/trailer/gridgame-trailer-45s.mp4` (size permitting; else keep it under `R:\MindAttic\Media\Capture` and link it).
- Raw footage from earlier runs already exists under `R:\MindAttic\Media\Capture\Unity.GridGame\<session>\*_Video.mp4`
  (the 2026-11-22 sessions are the newest) but predates the latest UI and icon work; prefer a fresh run.

## Music cue

One track end to end, trimmed and faded in the cut, nothing layered. Game audio from the capture is dropped
(`-an`); the music is the only sound, plus at most a 0.3 s sting from the SFX set on the title card.

| Beat | Time | Track | Why |
|---|---|---|---|
| Whole trailer | 0-45 s | `Resources/Audio/Music/neon-steel-rush-00.ogg` (heaviest bass, driving) or `lanterns-rising-00.ogg` (D minor, builds, heroic) | Both build and have a clear drive. Pick by ear; `lanterns-rising-00` is the default because it is the least genre-specific. |
| Fade | last 2 s | same | `afade=t=out` |

Start the track at its first strong downbeat (trim with `-ss`), and cut the picture on its beat; shot lengths
below are multiples of about 2 s so a 100-120 BPM track lands cuts on bars.

## Shot list (about 45 s)

All runs: `python tools/harness.py <cmd> --record` (adds `-capture video hq`), default window 585x1266 which is
1170x2532 design; scale to 1080x1920 in the cut (crop the safe area, do not stretch).

| # | Time | Shot | Source run | Notes |
|---|---|---|---|---|
| 1 | 0-3 s | Title screen, logo settles | `storyarc` start (title, `01-title`) | Fade in from black. |
| 2 | 3-8 s | Prologue crawl, then the Camp | `storyarc` (prologue, camp) | Show the story hook, not the whole crawl: 2 s of crawl, 3 s of Camp. |
| 3 | 8-16 s | A Moors battle: drag a hero, pincer, chain | `coach` run (tutorial battle) or `clips 5` | Wants a visible pincer and a 3+ chain; pick the clip where the board FX read best. |
| 4 | 16-22 s | Spell / ability cast with cut-in | `clips` | Slam cut-in is the bombastic beat; keep it unbroken. |
| 5 | 22-28 s | Boss intro banner and rule-break | `storyarc` part 2 (Tithe Moors III boss intro) | |
| 6 | 28-34 s | Timeline and hold-timer tension, a clutch heal/shove | `clips` | Optional; swap for another region's backdrop if the clip is weak. |
| 7 | 34-39 s | Region variety: 2 cuts of 2.5 s of other regions' battles | `campaign 6` | Shows different backdrops and foes. |
| 8 | 39-42 s | Level-up / loot / Camp forge | `fullloop 2` | |
| 9 | 42-45 s | Title card: logo, "GridGame", platform line, fade out | still from `docs/store/screenshots` or the title frame | Hold 2 s, music fades. |

No store-page text claims beyond what the game shows; no frames with debug UI, harness overlays or coach boxes.

## Capture steps

1. `orchestrator clis` must say the heavy-process lease is free; hold it for the whole capture (one player at a time).
2. `Build/Windows/GridGame.exe` must be current (`tools/build.ps1`); the harness launches it.
3. Run, one at a time, each with `--record`:
   `python tools/harness.py storyarc trailer-story` , `python tools/harness.py clips 8 --record`,
   `python tools/harness.py campaign trailer-camp --record`, `python tools/harness.py fullloop 2 --record`.
4. Sessions land in `R:\MindAttic\Media\Capture\GridGame.Unity\<UTC session>\*_Video.mp4`. Scrub each with ffplay and note
   in/out times per shot in the table above.

## Assembly (ffmpeg)

```
# per shot: trim, crop to 9:16, 30 fps, no audio
ffmpeg -ss IN -to OUT -i SESSION_Video.mp4 -an -vf "scale=1080:1920:force_original_aspect_ratio=increase,crop=1080:1920,fps=30" -c:v libx264 -crf 16 -preset slow shotNN.mp4
# list shots in shots.txt as: file 'shot01.mp4' ... then
ffmpeg -f concat -safe 0 -i shots.txt -c copy picture.mp4
# music: trim to the downbeat, fade out the last 2 s
ffmpeg -i Assets/GridGame/Resources/Audio/Music/lanterns-rising-00.ogg -ss MUSIC_IN -t 45 -af "afade=t=in:d=0.5,afade=t=out:st=43:d=2" music.m4a
ffmpeg -i picture.mp4 -i music.m4a -c:v copy -c:a aac -b:a 192k -shortest gridgame-trailer-45s.mp4
```

Check before landing: duration 30-60 s, `ffprobe` loudness near -16 LUFS (`ffmpeg -i f -af loudnorm=print_format=summary -f null -`),
no black frames mid-cut, no UI overlays from the harness.
