#!/usr/bin/env python3
"""GG-T387: lorem ipsum placeholders for every conversation (owner, 2026-11-16: "set up all the lorem ipsum for story
crawls and conversations"; the old story was flushed 2026-11-07 and is being reinvented in Prose).

    python tools/prose_lorem.py [--drafts <dir>] [--dry-run]
    prose --texts-draft gridgame <dir> --by claude --batch lorem-387     (drafts only; the owner approves, RFC 0009)

Every `dialogue:<id>` text (story beats, Camp talks, Advice quizzes, wanderer talks) is re-seeded from the game's own
dialogue records (tools/prose_seed.conversation: node ids, speakers, next, choice order and tags stay exactly as the
game plays them) with its spoken lines and choice labels replaced by lorem ipsum:

- a line keeps roughly its old length (the same number of words, 6 to 40), so the box lays out as before;
- a choice label is a short lorem phrase behind a marker the player can pick by ("A ·", "B ·", "C ·", in choice order).
  Nothing in a wanderer reply's words tells the winning one apart (fine for placeholders).

The text goes through prose_text.upsert_seed (a seed entry in Content/Text/conversations.json, never over a
Prose-applied text, plus a draft per text). Drafting those to Prose makes lorem the newest draft of every conversation,
so the build's prose-sync (published texts only) can never bring the retired words back: approval in Prose only ever
takes a text's newest draft. Deterministic: the same records give the same words. Dialogue crawls (crawl:dialogue/*)
are crawls, not conversations, and are left alone. Standard library only.

GG-T434: a quest with no text yet gets lorem journal text (brief and hand-in line; its title stays the game's
descriptive Name), and `--only-new` limits conversations to keys with no text yet (a new quest's Talk offer):

    python tools/prose_lorem.py --only-new --drafts <dir>
    prose --texts-draft gridgame <dir> --by claude --batch quests-434
"""

from __future__ import annotations

import argparse
import copy
import hashlib
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import prose_keys as K  # noqa: E402
import prose_seed  # noqa: E402
import prose_text  # noqa: E402

WORDS = (
    "lorem ipsum dolor sit amet consectetur adipiscing elit sed do eiusmod tempor incididunt ut labore et dolore magna "
    "aliqua ut enim ad minim veniam quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat "
    "duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur excepteur "
    "sint occaecat cupidatat non proident sunt in culpa qui officia deserunt mollit anim id est laborum sed ut "
    "perspiciatis unde omnis iste natus error sit voluptatem accusantium doloremque laudantium totam rem aperiam eaque "
    "ipsa quae ab illo inventore veritatis et quasi architecto beatae vitae dicta sunt explicabo nemo enim ipsam "
    "voluptatem quia voluptas sit aspernatur aut odit aut fugit"
).split()

MARKERS = "ABCDEFGH"


def _seed(*parts) -> int:
    return int(hashlib.sha256("|".join(str(p) for p in parts).encode("utf-8")).hexdigest()[:8], 16)


def lorem(n_words: int, *seed) -> str:
    """n_words of lorem ipsum, starting at a place fixed by seed, as sentences of 6 to 12 words."""
    at = _seed(*seed) % len(WORDS)
    words = [WORDS[(at + i) % len(WORDS)] for i in range(max(1, n_words))]
    out, sentence, cut = [], [], 6 + _seed("cut", *seed) % 7
    for w in words:
        sentence.append(w)
        if len(sentence) >= cut:
            out.append(" ".join(sentence).capitalize() + ".")
            sentence, cut = [], 6 + _seed("cut", len(out), *seed) % 7
    if sentence and out and len(sentence) < 4:  # no one-word tail sentence: it joins the last one
        out[-1] = out[-1][:-1] + " " + " ".join(sentence) + "."
    elif sentence:
        out.append(" ".join(sentence).capitalize() + ".")
    return " ".join(out)


def line_words(old: str) -> int:
    n = len((old or "").split())
    return min(40, max(6, n or 12))


def lorem_conversation(doc: dict) -> dict:
    """The conversation doc with every line and choice label replaced; ids, speakers, next and tags untouched."""
    doc = copy.deepcopy(doc)
    key = doc["key"]
    for node in doc["payload"].get("nodes") or []:
        node["line"] = lorem(line_words(node.get("line")), key, node.get("id"))
        for i, ch in enumerate(node.get("choices") or []):
            mark = MARKERS[i] if i < len(MARKERS) else str(i + 1)
            ch["text"] = f"{mark} · " + lorem(3 + _seed("n", key, node.get("id"), i) % 3, key, node.get("id"), i).rstrip(".")
    return doc


def build(content: dict, known: set) -> list:
    return [lorem_conversation(prose_seed.conversation(d, known)) for d in content.get("dialogues") or [] if d.get("Id")]


def lorem_quest(q: dict, known: set) -> dict:
    """GG-T434: a quest's journal text as lorem ipsum. The title is the game's own descriptive Name ("Iron for the
    Forge"), kept so the story writer can read the quest's shape; the brief (about 30 words) and the hand-in line
    (about 18) are placeholders. Giver and about follow prose_seed's quest seed."""
    key = "quest:" + q["Id"]
    npc = K.npc_id_for(q.get("Giver"))
    giver = K.character_entity(npc) if npc else ("$party" if q.get("Giver") == "Party" else (q.get("Giver") or ""))
    p = {"title": q.get("Name") or q["Id"], "brief": lorem(30, key, "brief")}
    if giver:
        p["giver"] = giver
    p["complete"] = lorem(18, key, "complete")
    about = [giver, K.place_entity(q.get("Theme") or "")]
    obj = q.get("Objective") or {}
    if obj.get("Kind") in ("Defeat", "Recruit") and obj.get("Id"):
        about.append(K.class_entity(obj["Id"]))
    if obj.get("Hero"):
        about.append(K.class_entity(obj["Hero"]))
    return prose_seed.text("quest", key, p, [a for a in about if a in known])


def build_quests(content: dict, known: set) -> list:
    return [lorem_quest(q, known) for q in content.get("quests") or [] if q.get("Id")]


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description="Lorem ipsum placeholders for every GridGame conversation (GG-T387).")
    ap.add_argument("--content", default=K.CONTENT_DIR)
    ap.add_argument("--text", default=prose_text.TEXT_DIR, help="Content/Text directory to write the seeds into")
    ap.add_argument("--drafts", help="also write each text as a prose-text/1 draft here, for prose --texts-draft")
    ap.add_argument("--universe", default=os.path.join("Data", "Prose", "gridgame.universe.json"))
    ap.add_argument("--only-new", action="store_true",
                    help="GG-T434: only conversations whose key has no text yet (a new quest's offer); the rest are untouched")
    ap.add_argument("--dry-run", action="store_true")
    args = ap.parse_args(argv)
    content = prose_text.load_content_with_text(args.content, args.text)
    known = {e["id"] for e in K.load(args.universe).get("entities") or []} if os.path.isfile(args.universe) else set()
    held = prose_text.load_texts(args.text)
    docs = build(content, known)
    if args.only_new:
        docs = [d for d in docs if d["key"] not in held]
    # GG-T434: a new quest (no text yet) gets lorem journal text too; a quest that already has words keeps them.
    quests = [d for d in build_quests(content, known) if d["key"] not in held]
    keys = K.game_keys(content)
    for d in docs:
        assert keys.get(d["key"]) == d["kind"] == "conversation", d["key"]
    for d in quests:
        assert keys.get(d["key"]) == d["kind"] == "quest", d["key"]
    if args.dry_run:
        print(f"prose_lorem (dry run): {len(docs)} conversations, {len(quests)} new quests")
        return 0
    counts = prose_text.upsert_seed(docs + quests, args.text, args.drafts)
    print(f"prose_lorem: {counts['seeded']} texts seeded with lorem ipsum ({len(docs)} conversations, {len(quests)} new "
          f"quests), {counts['kept']} Prose-applied kept{' (drafts in ' + args.drafts + ')' if args.drafts else ''}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
