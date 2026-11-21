# Music (GG-T456)

(Sound effects: [SFX.md](SFX.md).)

How it plays: every battle stage (five waves) plays **one** looping track, drawn from its region's playlist.
Each region keeps a shuffle bag (`Meta/MusicPlaylist.cs`): no track repeats until the whole playlist has
played, and a refilled bag never opens with the track that just ended. A region with fewer than four tracks of
its own tops up from its `MusicBorrow` list (the closest-sounding tracks elsewhere). The Camp draws a new
peaceful track each time the party comes back from the field. Playlists live in `campaign.json`
(`HubMusic`, and `Music` / `MusicBorrow` per theme). Files are `Resources/Audio/Music/<Name>.ogg`.

Track changes crossfade (GG-T460, `App/Services/Audio.cs`): two music sources, the new track fading in while the
old one fades out over `Audio.CrossfadeSeconds` (1.2 s, unscaled time, equal-power curve), both under the
volume setting and the duck. `Music(null)` fades out. Switching back to a track still fading out turns it around
in place rather than restarting it; the battle pause holds only sound effects, so music keeps its place.

**Adding a track:** drop the file in `R:\Desktop\GridGameMusic` and run `python tools/import_music.py
"<file>=<GameName>"` (or `--all` for every new file there; names are derived from the file name when it is
ASCII). It trims silence (-55 dB both ends), normalises to -18 LUFS (two-pass loudnorm, TP -1.5, LRA 11), adds
20 ms de-click fades and writes Vorbis q6 at 44.1 kHz to `Resources/Audio/Music/<GameName>.ogg`. Duplicates are
caught by audio hash (decoded PCM, so a renamed or re-encoded copy still matches) against the batch and every
track already imported (`tools/music_sources.json`); an existing track is never overwritten without `--force`.
It then prints the playlists to list the track in. Add its name to the region's `Music` list (or `HubMusic`,
or a `MomentMusic` list). Once a region has four of its own, its `MusicBorrow` list stops being used.

## The first 19 tracks (2026-11-20)

Analysed with ffmpeg + numpy (tempo, key, loudness, dynamics, brightness, bass weight, energy arc) and
spectrograms. `Moonlit Conflict (1)` was a byte-identical copy of `Moonlit Conflict` and was dropped.

| Game name | Suno file | Region | Character |
|---|---|---|---|
| HearthsideCamp | Hub of Peace | Camp | soft, dark-toned (median 600 Hz), no highs, wide dynamics |
| EmbersAtRest | Hub of Peace (1) | Camp | same mood, a little brighter, gentle fade |
| MoorlandCharge | Grassland Charge (1) | Tithe Moors | light, airy, little bass, ~100 BPM |
| TitheDayGallop | Grassland Charge | Tithe Moors | the brisker take, ~150 BPM |
| OverThePass | Rising Theme (1) | Lantern Road | builds steadily, C major, heroic |
| LanternsRising | Rising Theme | Lantern Road | D minor, driving |
| MoonlitConflict | Moonlit Conflict | Lantern Road | dense, heavy low end, relentless |
| GlassDunes | Mineral Dark (1) | Shardfall | open, C major, mid-weight |
| Shardfall | Mineral Dark | Shardfall | D minor, flat-out, crystalline top |
| GroundedArks | Propulsive Groove | Arkgrave | steady groove |
| ArkgraveMarch | Propulsive Groove (1) | Arkgrave | the same groove, longer |
| AnchorPulse | Propulsive Pulse | Anchor Wastes | A minor, pulsing, builds to the end |
| WintersEdge | Winter's Edge | The Still | A minor, cold, measured |
| StillFrost | Winter's Edge (1) | The Still | sparser take |
| NeonSteelRush | Neon Steel Rush | Halcyon | electronic, heaviest bass in the set |
| HeartbeatOfTheBrave | 勇者の鼓動 ("the brave one's heartbeat") | The Free Holds | warm, folk-heroic, nearest to the moors tracks |
| NeonBreakbeat | Neon Breakbeat | The Underspire | breakbeat, short (62 s) |
| TheFinalSignal | 最終決戦の合図 ("signal of the final battle") | Choir's Cathedral | D minor, big, final-chapter |
| LastStandHorn | 最終決戦の合図 (1) | Morrow's Shadow | the brighter, heavier take |

Regions with no music of their own yet (they borrow): The Bloom, The Drowned Arks, Deep Shardfall, Under the
Anchors.

## Moments (GG-T458)

The non-battle moments are playlists too: `campaign.json` `MomentMusic`, one shuffle bag each, drawn once per
screen. Until the owner's tracks land they hold the old stock tracks, so nothing changes; add a track to a
list to put it in rotation (replace the stock name to retire it).

| Key | Plays on | Today | Empty list means |
|---|---|---|---|
| `title` | Title screen | TellerOfTheTales | TellerOfTheTales |
| `story` | story crawls (prologue, chapters, ending) | TellerOfTheTales | TellerOfTheTales |
| `victory` | results after a win or a Crucible run | HeroicAge | HeroicAge |
| `defeat` | results after a loss | Heartbreaking | Heartbreaking |
| `levelup` | the level-up fanfare screen | HeroicAge | HeroicAge |
| `charselect` | New Campaign's party pick | MinstrelGuild | MinstrelGuild |
| `credits` | the end-of-story credits roll | HeroicAge | HeroicAge |
| `crucible` | a Crucible run's battle | (empty) | any region's battle track |
| `boss` | a stage-III finale's boss wave (GG-T459) | (empty) | the stage keeps its track |
| `finalboss` | the Hierarch's wave (GG-T459) | (empty) | the stage keeps its track |

Dialogue keeps whatever is playing (it ducks the music instead).

**Boss waves (GG-T459):** when a wave brings on a finale's rule-breaking boss (bible §14.9; a bounty target's
rule-break doesn't count), the battle crossfades to a `boss` track, or on the stage the `Ending` follows (the
Hierarch, `cathedral-03`) a `finalboss` track, falling back to `boss`. With both lists empty the stage keeps its
own track. The boss track then stays for the rest of the stage. A Crucible run never switches.

## Tracks still wanted, in priority order

Target: six per region (90 battle tracks) plus four to six for the Camp. Have 17 battle + 2 Camp.

1. **Regions with none (8):** two each for The Bloom, The Drowned Arks, Deep Shardfall, Under the Anchors.
2. **Regions with one (6):** one more each for Anchor Wastes, Halcyon, Choir's Cathedral, The Free Holds,
   The Underspire, Morrow's Shadow.
3. **Moments (7):** title theme, regional boss, final boss (the Hierarch), Crucible arena, story/dialogue
   underscore, victory/results, defeat. Each goes straight into its `MomentMusic` list; no code change needed.
4. **Depth:** the rest of the six per region (about 59 more) and 2–4 more Camp tracks.

## Suno prompts

Use Instrumental, and generate two takes per prompt (pick the better, or keep both as two tracks). For looping,
favour takes that end at full energy rather than with a long fade; the converter trims silence but cannot
undo a fade.

**The Bloom** (something fell from the sky and grew; alien, overgrown, uncanny)
- *Bloom Overgrowth*: `instrumental orchestral battle music, eerie alien wonder, shimmering bell textures, detuned
  synth pads over taut string ostinato, deep taiko hits, otherworldly choir oohs, 130 bpm, tense and beautiful,
  loopable, no fade out`
- *Spores on the Wind*: `instrumental fantasy battle theme, bioluminescent and strange, plucked harp and glassy
  synth arpeggios, pulsing low strings, tribal percussion, brass swells, D minor, 140 bpm, loopable, no fade out`

**The Drowned Arks** (grounded ships sinking under a rising tide)
- *Rising Tide*: `instrumental dark nautical battle music, low brass and cello ostinato, rolling timpani like waves,
  creaking wood percussion, wordless male choir, accordion drone, 120 bpm, heavy and relentless, loopable`
- *Hulls Under Water*: `instrumental swamp battle theme, murky and urgent, bowed double bass, muted brass stabs,
  water-drum percussion, dulcimer melody, minor key, 135 bpm, loopable, no fade out`

**Deep Shardfall** (deep under the crystal desert)
- *Crystal Depths*: `instrumental desert battle music, middle-eastern scale, oud and ney flute lead, darbuka and
  frame drums, glassy crystal chimes, heavy low strings, E phrygian, 145 bpm, intense, loopable`
- *Shards Below*: `instrumental cavern battle theme, resonant crystal mallets, driving cello ostinato, war drums,
  distant horn calls, ominous and fast, 150 bpm, loopable, no fade out`

**Under the Anchors** (beneath the great anchor pylons; industrial, cold)
- *Beneath the Beams*: `instrumental industrial fantasy battle music, metallic percussion, hammered anvils, low
  synth pulse under orchestral strings, brass hits, cold and mechanical, 140 bpm, loopable`
- *Pylon Dark*: `instrumental dark orchestral battle theme, ticking clockwork rhythm, deep brass, choir stabs,
  sub bass drone, tense minor key, 128 bpm, loopable, no fade out`

**Anchor Wastes** (cold wastes, ice maulers, the Cyclops)
- *Frozen Anchors*: `instrumental orchestral battle music, icy and driving, staccato strings, glockenspiel and
  celesta sparkle over war drums, French horn melody, A minor, 145 bpm, loopable`

**Halcyon** (the ruined spire city; tech under old stone)
- *Spire Breach*: `instrumental synth-orchestral battle music, electric guitar chugs under soaring strings, pulsing
  synth bass, epic drums, heroic brass lead, D minor, 150 bpm, loopable, no fade out`

**The Choir's Cathedral** (the Choir, the Hierarch; the last chapter)
- *Hymn of Iron*: `instrumental epic cathedral battle music, pipe organ and full choir, pounding timpani, soaring
  strings, dark brass, latin-style chant without words, 140 bpm, grand and desperate, loopable`

**The Free Holds** (free farm-holds after the war; hopeful, rough)
- *Barricade Song*: `instrumental folk battle music, fiddle lead, acoustic guitar strumming, bodhran and snare,
  tin whistle counter-melody, hopeful and scrappy, G major, 150 bpm, loopable, no fade out`

**The Underspire** (under the spire; cracking floors)
- *Cracking Floor*: `instrumental dark electronic orchestral battle music, glitchy breakbeat drums, deep reese bass,
  staccato strings, distant bells, claustrophobic, 165 bpm, loopable`

**Morrow's Shadow** (the final postgame region)
- *Morrow's Edge*: `instrumental epic final-dungeon battle music, relentless strings and brass, choir, double-time
  war drums, ominous organ undertone, heroic melody breaking through, C minor, 155 bpm, loopable, no fade out`

**Moments**
- *Title (Tellers of the Tithe)*: `instrumental fantasy main theme, gentle solo piano opening into warm strings and
  french horn melody, hopeful and bittersweet, adventure overture, 90 bpm, loopable`
- *Boss (A Rule Broken)*: `instrumental JRPG-style boss battle music, aggressive string ostinato, distorted guitar
  and brass, choir hits, fast war drums, menacing minor key, 170 bpm, loopable, no fade out`
- *Final boss (The Hierarch)*: `instrumental epic final boss music, massive choir and pipe organ, full orchestra,
  rock drums and bass, tragic and furious, odd-meter sections, D minor, 160 bpm, loopable`
- *Crucible (The Arena)*: `instrumental arena battle music, brass fanfare motifs, driving rock drums, strings and
  electric bass, competitive and hype, 155 bpm, loopable, no fade out`
- *Story underscore (Around the Fire)*: `instrumental soft story underscore, acoustic guitar and warm strings,
  light harp, reflective and calm, no drums, 70 bpm, loopable`
- *Victory (Spoils)*: `instrumental short victory fanfare into a relaxed results loop, bright brass and strings,
  triumphant then easy, major key, 120 bpm, loopable`
- *Defeat (Fallen Banner)*: `instrumental sad defeat theme, solo piano and cello, slow and quiet, minor key,
  60 bpm, loopable`
- *Camp (more)*: `instrumental peaceful camp music, acoustic guitar, soft flute, light strings, crackling warmth,
  cozy and safe, no drums, 80 bpm, loopable`
