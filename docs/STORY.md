# GridGame Gaiden: story (a pointer, not a source)

**The story lives in Prose, not in this repo** (GG-T355, owner 2026-11-07: *"all dialog/conversations, descriptions,
screen crawls, everything is sourced from prose ... the Game handles gameplay, and Prose handles narrative"*).

- **Canon** (the premise, factions, places, characters and their relations) is the Prose universe **`gridgame`**
  ("GridGame") in the Prose narrative engine (`D:\Projects\MindAttic\Prose`): its UniverseCanon sections, staged in
  `Data/Prose/canon/`. The premise is the owner's *the Manalith* (2026-11-21, amended GG-T577): an ancient thing, not a
  moon, stopped long ago and hanging in the sky beside the Moon, now coming closer; its mana is the life in every
  person; the Warden, the Last Sleeper, wakes 1,200 years too late, with present-day heroes; the Hierarch is the villain.
  *Under Morrow* (v1 to v3) and *A War in Heaven* (v4) are RETIRED and kept in Prose as history.
- **Every word a player reads as story** (dialogue lines and choices, crawls, class/item/armor-set/skill-tree/NPC
  descriptions, quest briefs and turn-ins, glossary definitions, barks) is a Prose text. The game reads it from
  `Assets/Gaiden/Resources/Content/Text/*.json`, which the build pulls from Prose and nobody edits by hand.
- **To change the story,** draft in Prose (`prose --texts-draft gridgame <dir>`; agents only draft, RFC 0009) and the
  owner approves and publishes it there. The next build applies it.

How it flows, the key scheme and the commands: [PROSE.md](PROSE.md). The rules: [BIBLE §27.2](BIBLE.md). The old text of
this file is in git history (before GG-T355 Phase 2) and in Prose.
