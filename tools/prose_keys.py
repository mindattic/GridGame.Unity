#!/usr/bin/env python3
"""GG-T355: GridGame's side of the Prose conduit, the parts the mapper, the seed and the applier share
(docs/PROSE.md; Prose docs/CONDUIT.md, RFC 0017).

- Loading the content JSON (read only; never written here).
- Stable ids: entity ids (`class-<id>`, `place-<theme>`, ...) and text ids (`conversation-<dialogue>`, ...).
- The text KEY scheme: the handle the game will look each text up by (Phase 2's loader join).

    kind          key                              text id                              from
    conversation  dialogue:<dialogueId>            conversation-<dialogueId>            dialogues.json (every dialogue)
    crawl         crawl:opening/<n>                crawl-opening-<n>                    campaign.json Opening.Pages[n-1] (GG-T1363:
                                                                                        the paged opening, n from 1; the retired
                                                                                        Prologue's crawl:prologue is still read)
    crawl         crawl:ending                     crawl-ending                         campaign.json Ending.Crawl
    crawl         crawl:chapter/<chapterId>        crawl-chapter-<chapterId>            campaign.json Chapters[] (GG-T386: one after
                                                                                        every fifth stage; the per-theme crawls
                                                                                        crawl:<themeId> are retired)
    crawl         crawl:dialogue/<dialogueId>      crawl-dialogue-<dialogueId>          dialogues.json Crawl (interludes)
    description   class:<classId>                  description-class-<classId>          classes.json Description (short) / Lore (long)
                                                                                        (hero blurbs, enemy and Bestiary entries)
    description   item:<itemId>                    description-item-<itemId>            items.json Description (every item)
    description   armorset:<setId>                 description-armorset-<setId>         armorsets.json Description
    description   skilltree:<treeId>               description-skilltree-<treeId>       skilltrees.json Description
    description   skillnode:<nodeId>               description-skillnode-<nodeId>       skilltrees.json Nodes[].Description
    description   npc:<npcId>                      description-npc-<npcId>              the camp NPC's blurb (no gameplay field yet)
    description   place:<themeId>                  description-place-<themeId>          a campaign region (GG-T374: chapter select)
    description   place:<stageId>                  description-place-<stageId>          a campaign stage (not test stages)
    description   faction:<factionId>              description-faction-<factionId>      factions.json (wanderer talk, recruits)
    description   character:<crew>                 description-character-<crew>         caravan.json Crew (a named character with
                                                                                        no class; <crew> is its entity slug)
    description   character:commander              description-character-commander      the Commander (GG-T1591: the player, who
                                                                                        speaks story beats as $commander)
    quest         quest:<questId>                  quest-<questId>                      quests.json Name/Giver/Summary/TurnIn
    glossary      glossary:<glossaryId>            glossary-<glossaryId>                glossary.json Term/Aliases/Text
    bark          bark:caravan/<upgradeId>         bark-caravan-<upgradeId>             caravan.json Barks (the wagon's crew)
    bark          bark:merc/<classId>/pitch        bark-merc-<classId>-pitch            mercenaries.json Pitch
    bark          bark:merc/<classId>/renewal      bark-merc-<classId>-renewal          mercenaries.json Renewal
    bark          bark:bossphase/<classId>/<n>     bark-bossphase-<classId>-<n>         classes.json BossPhases[n].Announce
    bark          bark:surprise/<stageId>/<wave>   bark-surprise-<stageId>-<wave>       stages.json Waves[wave].SurpriseLine
    bark          bark:shop/<npcId>/<occasion>     bark-shop-<npcId>-<occasion>         SHOP_BARKS (GG-T570: the Aetherist's
                                                                                        refinery; DialogueDef.ShopBarks)
    description   codex:<kind>/<id>                description-codex-<kind>-<id>        codex.json Key "<kind>:<id>" (GG-T936: a
                                                                                        Codex entry's body, payload "long"; OPTIONAL,
                                                                                        the entry falls back to its record's words)

Stays in the game (UI and mechanics, not story): names (class, item, ability, theme, stage, quest-board labels;
Prose holds them as entity names, the game owns the id), ability Description and Trigger.Callout (mechanical floats),
class Card and Support.Label, item PassiveName/PassiveDescription (numbers), caravan Description/Levels[].Text,
pickups, bounties, encounters, the battle tutorial (BattleScreen.Tutorial.cs) and Credits.txt.

Keys keep the game's own ids verbatim (underscores and all); text and entity ids are those ids slugged
(lowercase, `_` -> `-`). A key exists exactly when the gameplay record it names exists, so `game_keys()` is also
how the applier tells an orphan (a text no gameplay record would look up) from a text to apply.

Standard library only.
"""

from __future__ import annotations

import json
import os
import re

CONTENT_DIR = os.path.join("Assets", "GridGame", "Resources", "Content")
UNIVERSE = "gridgame"

# Camp NPCs: the game's npc id (Talk trigger "npc:<id>", JoinNpc) -> the name dialogues and quests use for them.
NPCS = {
    "innkeeper": "Innkeeper",
    "blacksmith": "Blacksmith",
    "alchemist": "Alchemist",
    "enchantress": "Enchantress",
    "merchant": "Lantern Merchant",
    "aetherist": "Manasmith",  # GG-T570: the retired hero class, now the refinery's keeper; named Manasmith by GG-T675 (id kept)
}
NPC_BY_NAME = {v: k for k, v in NPCS.items()}

# GG-T1591: the Commander (the player) as a Prose character; story beats speak as $commander.
COMMANDER = "commander"

# GG-T570: a Camp shop keeper's bark occasions, "<npcId>/<occasion>" -> key bark:shop/<npcId>/<occasion>. Mirrors the
# game's DialogueDef.ShopBarks.
SHOP_BARKS = ("aetherist/greet", "aetherist/refine", "aetherist/short", "aetherist/leave")

# Choice tags the game's dialogue logic keys on (prose.config.json "tags"):
#   right-answer  the answer the game rewards: a wanderer reply that wins its exchange in the game of wits (Wit +1,
#                 Wanderers.RightAnswers, GG-T387) or the Advice-quiz answer that earns a skill point (GrantSkillPoint).
#   conditional   a choice shown only when its gameplay Conditions hold (gold, a flag, the roster).
TAGS = ("right-answer", "conditional")

_SLUG_BAD = re.compile(r"[^a-z0-9]+")


# GG-T936: keys whose text may not exist yet (a Codex body falls back to its record's own words). Mirrors the game's
# ProseText.IsOptional: game_keys lists them (so an applied text is not an orphan), but a missing one is not reported.
OPTIONAL_PREFIXES = ("codex:",)


def is_optional(key: str) -> bool:
    return key.startswith(OPTIONAL_PREFIXES)


def codex_text_key(codex_key: str) -> str:
    """codex.json Key '<kind>:<id>' -> its body's text key 'codex:<kind>/<id>' (CodexDef.TextKey)."""
    kind, _, rest = codex_key.strip().partition(":")
    return f"codex:{kind}/{rest}" if rest else "codex:" + codex_key.strip()


def slug(s: str) -> str:
    """A stable id piece: lowercase, every run of other characters one '-', no edge dashes."""
    return _SLUG_BAD.sub("-", (s or "").lower()).strip("-")


def load(path: str):
    with open(path, encoding="utf-8-sig") as f:
        return json.load(f)


def load_content(content_dir: str = CONTENT_DIR) -> dict:
    """The content files the conduit reads, by name ('classes', 'dialogues', ...). A missing file is an empty list
    (campaign: an empty object), so a partial fixture still loads."""
    out = {}
    for name in ("classes", "factions", "campaign", "stages", "dialogues", "quests", "glossary", "caravan",
                 "mercenaries", "items", "armorsets", "skilltrees", "regionrules", "codex"):
        path = os.path.join(content_dir, name + ".json")
        if os.path.isfile(path):
            out[name] = load(path)
        else:
            out[name] = {} if name == "campaign" else []
    return out


# ------------------------------------------------------------------------------------------------ ids

def class_entity(class_id: str) -> str:
    return "class-" + slug(class_id)


def faction_entity(faction_id: str) -> str:
    return "faction-" + slug(faction_id)


def place_entity(theme_or_stage: str) -> str:
    return "place-" + slug(theme_or_stage)


def character_entity(name_or_id: str) -> str:
    return "character-" + slug(name_or_id)


def concept_entity(glossary_id: str) -> str:
    return "concept-" + slug(glossary_id)


def item_entity(item_id: str) -> str:
    return "item-" + slug(item_id)


def npc_id_for(name_or_id: str):
    """The camp NPC id for a speaker/giver name ('Lantern Merchant') or id ('merchant', 'smith'), else None."""
    if not name_or_id:
        return None
    if name_or_id in NPC_BY_NAME:
        return NPC_BY_NAME[name_or_id]
    if name_or_id in NPCS:
        return name_or_id
    if name_or_id == "smith":
        return "blacksmith"
    return None


# ------------------------------------------------------------------------------------------------ keys

def game_keys(content: dict) -> dict:
    """Every text key the gameplay data defines -> its kind. A text with any other key is an orphan."""
    keys = {}
    for d in content.get("dialogues") or []:
        if d.get("Id"):
            keys["dialogue:" + d["Id"]] = "conversation"
            if d.get("HasCrawl") or d.get("Crawl"):  # Phase 2: the record keeps only the HasCrawl marker
                keys["crawl:dialogue/" + d["Id"]] = "crawl"
    camp = content.get("campaign") or {}
    if isinstance(camp, dict):
        if isinstance(camp.get("Prologue"), dict):
            keys["crawl:prologue"] = "crawl"
        if isinstance(camp.get("Opening"), dict):  # GG-T1363: one key per page
            for n in range(1, len(camp["Opening"].get("Pages") or []) + 1):
                keys[f"crawl:opening/{n}"] = "crawl"
        if isinstance(camp.get("Ending"), dict):
            keys["crawl:ending"] = "crawl"
        for ch in camp.get("Chapters") or []:  # GG-T386 (replaces the retired crawl:<themeId>)
            if isinstance(ch, dict) and ch.get("Id"):
                keys["crawl:chapter/" + ch["Id"]] = "crawl"
    for c in content.get("classes") or []:
        if c.get("Id"):
            keys["class:" + c["Id"]] = "description"
            for n, ph in enumerate(c.get("BossPhases") or []):
                if isinstance(ph, dict):  # every boss phase announces itself
                    keys[f"bark:bossphase/{c['Id']}/{n}"] = "bark"
    for i in content.get("items") or []:
        if isinstance(i, dict) and i.get("Id"):
            keys["item:" + i["Id"]] = "description"
    for a in content.get("armorsets") or []:
        if a.get("Id"):
            keys["armorset:" + a["Id"]] = "description"
    for t in content.get("skilltrees") or []:
        if t.get("Id"):
            keys["skilltree:" + t["Id"]] = "description"
            for n in t.get("Nodes") or []:
                if n.get("Id"):
                    keys["skillnode:" + n["Id"]] = "description"
    for npc in NPCS:
        keys["npc:" + npc] = "description"
    for b in SHOP_BARKS:  # GG-T570
        keys["bark:shop/" + b] = "bark"
    for key in description_keys(content).values():  # GG-T374: places, factions, crew
        keys[key] = "description"
    for r in content.get("regionrules") or []:  # GG-T359: a region rule's name and description
        if isinstance(r, dict) and r.get("Id"):
            keys["rule:" + r["Id"]] = "description"
    for s in content.get("stages") or []:
        for w, wave in enumerate(s.get("Waves") or []):
            if s.get("Id") and isinstance(wave, dict) and (wave.get("SurpriseSpeaker") or wave.get("SurpriseLine")):
                keys[f"bark:surprise/{s['Id']}/{w}"] = "bark"
    for q in content.get("quests") or []:
        if q.get("Id"):
            keys["quest:" + q["Id"]] = "quest"
    for g in content.get("glossary") or []:
        if g.get("Id"):
            keys["glossary:" + g["Id"]] = "glossary"
    for u in content.get("caravan") or []:
        if u.get("Id") and (u.get("Crew") or u.get("Barks")):  # a crewed wagon has barks
            keys["bark:caravan/" + u["Id"]] = "bark"
    for m in content.get("mercenaries") or []:
        if m.get("Class"):
            keys[f"bark:merc/{m['Class']}/pitch"] = "bark"
            keys[f"bark:merc/{m['Class']}/renewal"] = "bark"
    for e in content.get("codex") or []:  # GG-T936: optional Codex bodies
        if isinstance(e, dict) and (e.get("Key") or "").strip():
            keys[codex_text_key(e["Key"])] = "description"
    return keys


def campaign_stage_ids(content: dict) -> list:
    """(theme id, stage id) for every campaign stage the mapper emits as a place: in a theme's Stages, defined in
    stages.json, not a test stage. Campaign order."""
    camp = content.get("campaign") or {}
    stages = {s["Id"]: s for s in content.get("stages") or [] if isinstance(s, dict) and s.get("Id")}
    out = []
    for t in (camp.get("Themes") or []) if isinstance(camp, dict) else []:
        for sid in t.get("Stages") or []:
            if t.get("Id") and sid in stages and not stages[sid].get("IsTest"):
                out.append((t["Id"], sid))
    return out


def description_keys(content: dict) -> dict:
    """GG-T374: the description key of every place, faction and named character entity (entity id -> key), so each
    one the mapper emits has a text the player can look up (Prose's TEXT-OBLIGATION [undescribed]). Camp NPCs keep
    their npc:<id> key; classes, items and armor sets have their own."""
    out = {}
    camp = content.get("campaign") or {}
    for t in (camp.get("Themes") or []) if isinstance(camp, dict) else []:
        if t.get("Id"):
            out[place_entity(t["Id"])] = "place:" + t["Id"]
    for _, sid in campaign_stage_ids(content):
        out.setdefault(place_entity(sid), "place:" + sid)
    for f in content.get("factions") or []:
        if isinstance(f, dict) and f.get("Id"):
            out[faction_entity(f["Id"])] = "faction:" + f["Id"]
    for npc in NPCS:
        out[character_entity(npc)] = "npc:" + npc
    for u in content.get("caravan") or []:
        if isinstance(u, dict) and u.get("Crew"):
            out.setdefault(character_entity(u["Crew"]), "character:" + slug(u["Crew"]))
    out[character_entity(COMMANDER)] = "character:" + COMMANDER  # GG-T1591
    return out


def text_id(kind: str, key: str) -> str:
    """The text id for a key: the kind, then the key's game id(s) slugged (class texts keep 'class')."""
    scheme, _, rest = key.partition(":")
    if kind == "conversation":
        return "conversation-" + slug(rest)
    if kind == "description":
        return f"description-{slug(scheme)}-{slug(rest)}"
    return f"{kind}-{slug(rest)}"
