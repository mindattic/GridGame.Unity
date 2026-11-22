#!/usr/bin/env python3
"""GG-T626: the wiki entry schema's reader, entry builder and validator (BIBLE §33; schema in tools/wiki_schema.json).

One entry per thing, built only from the content JSON and the applied Prose texts (Content/Text). This module makes
entries (plain dicts, schema gridgame-wiki/1) and checks them; it renders nothing. GG-T627's page generator imports it:

    import wiki_schema as ws
    entries = ws.build_all()                  # every entry, every kind
    problems = ws.validate_all(entries)       # ["error: <id>: ...", "warn: <id>: ..."]

Run it to check the schema against the real data:  python tools/wiki_schema.py [--kind class] [--id class-knight] [-v]
Standard library only.
"""

from __future__ import annotations

import argparse
import glob
import hashlib
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import asset_names  # noqa: E402
import prose_keys as pk  # noqa: E402

ROOT = os.path.dirname(HERE)
SCHEMA_PATH = os.path.join(HERE, "wiki_schema.json")
RESOURCES = os.path.join(ROOT, "Assets", "Gaiden", "Resources")
CONTENT = os.path.join(RESOURCES, "Content")
CANON = os.path.join(ROOT, "Data", "Prose", "canon")
ENTRY_SCHEMA = "gridgame-wiki/1"
_TEMPLATE = re.compile(r"\{(\w+)\}")
# glossary LinkKind -> wiki kind (Buff and Stat have no entries of their own yet).
_GLOSSARY_LINK = {"Ability": "ability"}


def load_schema(path: str = SCHEMA_PATH) -> dict:
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def load_texts(content_dir: str = CONTENT) -> dict:
    """Every applied Prose text, key -> its gridgame-text/1 record (Content/Text/*.json)."""
    out = {}
    for path in glob.glob(os.path.join(content_dir, "Text", "*.json")):
        with open(path, encoding="utf-8-sig") as f:
            out.update(json.load(f).get("texts") or {})
    return out


def _content(content_dir: str) -> dict:
    def load(name):
        p = os.path.join(content_dir, name + ".json")
        return pk.load(p) if os.path.isfile(p) else []
    return {n: load(n) for n in ("classes", "abilities", "items", "armorsets", "skilltrees", "campaign", "stages",
                                 "regionrules", "quests", "factions", "glossary", "dialogues", "backdroplayers")}


def _npc_portrait_file(npc: str, content: dict) -> str:
    """The Portraits/ file a camp NPC shows: its dialogue speaker portrait, else the game's fallback (prose_map)."""
    import prose_map  # noqa: E402  (same resolution the Prose entities use)
    pic = prose_map.npc_portraits(content.get("dialogues") or []).get(npc, prose_map.NPC_PORTRAIT_FALLBACK.get(npc, npc))
    by_id = {c["Id"]: c for c in content.get("classes") or [] if isinstance(c, dict) and c.get("Id")}
    return asset_names.portrait_file(by_id[pic]) if pic in by_id else pic


def _jpg_ok(art: str) -> bool:
    """A .jpg backdrop is by design when it has no sky layer (Sky:false in backdroplayers.json, GG-T576)."""
    if not art.startswith("Backgrounds/") or not art.endswith(".jpg"):
        return False
    layers = _content(CONTENT).get("backdroplayers") or {}
    key = os.path.splitext(os.path.basename(art))[0]
    return isinstance(layers, dict) and key in layers and not layers[key].get("Sky")


def _fill(template, rec: dict, slug: str):
    """A schema template ("class:{Id}") with the record's fields; None when a field it names is empty."""
    if template is None:
        return None
    missing = False

    def sub(m):
        nonlocal missing
        name = m.group(1)
        v = slug if name == "slug" else rec.get(name)
        if v in (None, ""):
            missing = True
            return ""
        return str(v)
    out = _TEMPLATE.sub(sub, template)
    return None if missing else out


# ------------------------------------------------------------------------------------------------ sources

def records(kind: str, spec: dict, schema: dict, content: dict):
    """The records a kind is built from, as dicts with at least an Id."""
    src = spec["source"]
    if src == "registry":
        return list(schema.get("registry", {}).get(kind, []))
    if src == "prose_keys.NPCS":
        return [{"Id": k, "title": v, "PortraitFile": _npc_portrait_file(k, content)} for k, v in pk.NPCS.items()]
    if src == "campaign.json#Themes":
        camp = content.get("campaign") or {}
        return list(camp.get("Themes") or []) if isinstance(camp, dict) else []
    if src.startswith("Data/Prose/canon"):
        out = []
        for path in sorted(glob.glob(os.path.join(CANON, "*.md"))):
            name = re.sub(r"^\d+-", "", os.path.splitext(os.path.basename(path))[0])
            out.append({"Id": name, "file": os.path.relpath(path, ROOT).replace(os.sep, "/")})
        return out
    rows = content.get(os.path.splitext(src)[0]) or []
    filt = spec.get("filter") or {}
    keep = []
    for r in rows:
        if not isinstance(r, dict) or not r.get("Id"):
            continue
        ok = True
        for field, want in filt.items():
            allowed = want if isinstance(want, list) else [want]
            if r.get(field) not in allowed:
                ok = False
        if ok:
            keep.append(r)
    return keep


def entry_id(kind: str, game_id: str, schema: dict) -> str:
    return _fill(schema["kinds"][kind]["id"], {"Id": game_id}, pk.slug(game_id))


def _art(spec: dict, rec: dict):
    a = spec.get("art")
    if not a:
        return None
    value = rec.get(a["field"])
    if not value:
        return None
    stem = value if a.get("isFile") else asset_names.of(value, a.get("variant", 0))
    for ext in (".png", ".jpg"):
        rel = f"{a['folder']}/{stem}{ext}"
        if os.path.isfile(os.path.join(RESOURCES, rel)):
            return rel
    return f"{a['folder']}/{stem}.png"  # declared but missing: validate() reports it


def _canon(path_rel: str, part: str) -> str:
    with open(os.path.join(ROOT, path_rel), encoding="utf-8") as f:
        text = f.read()
    paras = [p.strip() for p in re.split(r"\n\s*\n", text) if p.strip() and not p.lstrip().startswith("#")]
    return (paras[0] if paras else "") if part == "first paragraph" else text


def _title(spec, rec, slug, texts):
    t = spec["title"]
    if isinstance(t, dict):
        key = _fill(t["text"], rec, slug)
        return ((texts.get(key) or {}).get("payload") or {}).get(t["field"]) or rec.get("Id")
    if t == "the section's first heading":
        with open(os.path.join(ROOT, rec["file"]), encoding="utf-8") as f:
            for line in f:
                if line.startswith("#"):
                    return line.lstrip("#").strip()
        return rec["Id"]
    return rec.get(t) or rec.get("Id")


def _source_ref(src: dict, rec: dict, slug: str):
    """A summary/body source made concrete: {text, field} | {gameField, value} | {canon, field}."""
    if "text" in src:
        key = _fill(src["text"], rec, slug)
        field = _fill(src["field"], rec, slug)
        return {"text": key, "field": field} if key and field else None
    if "gameField" in src:
        return {"gameField": src["gameField"], "value": rec.get(src["gameField"])}
    if "canon" in src:
        return {"canon": rec["file"], "field": src["field"]}
    return None


def _links_of(value, link_kind, schema):
    vals = value if isinstance(value, list) else [value]
    out = []
    for v in vals:
        if isinstance(v, dict):
            v = v.get("Id") or v.get("Item")
        if isinstance(v, str) and v:
            out.append(entry_id(link_kind, v, schema))
    return out


def build_entry(kind: str, rec: dict, schema: dict, texts: dict) -> dict:
    spec = schema["kinds"][kind]
    game_id = rec["Id"]
    slug = pk.slug(game_id)
    summary = _source_ref(spec["summary"], rec, slug)
    body = [b for b in (_source_ref(s, rec, slug) for s in spec.get("body") or []) if b]
    infobox, links = {}, []
    for label, f in (spec.get("infobox") or {}).items():
        if isinstance(f, str):
            value = rec.get(f)
            if value not in (None, "", [], {}):
                infobox[label] = value
            continue
        value = rec.get(f["field"])
        if value in (None, "", [], {}):
            continue
        link_kind = f.get("link") or _GLOSSARY_LINK.get(rec.get(f.get("linkFromField", "")))
        if link_kind:
            ids = _links_of(value, link_kind, schema)
            infobox[label] = ids if isinstance(value, list) else ids[0] if ids else value
            links += ids
        else:
            infobox[label] = value
    if spec.get("bible") == "per registry entry":
        infobox["BIBLE"] = "§" + rec.get("bible", "")
    keys = sorted({s["text"] for s in [summary] + body if s and "text" in s})
    h = hashlib.sha256(json.dumps(rec, sort_keys=True, default=str).encode("utf-8"))
    for k in keys:
        h.update(((texts.get(k) or {}).get("hash") or "missing:" + k).encode("utf-8"))
    return {
        "schema": ENTRY_SCHEMA,
        "id": _fill(spec["id"], rec, slug),
        "kind": kind,
        "gameId": game_id,
        "title": _title(spec, rec, slug, texts),
        "summary": summary,
        "body": body,
        "infobox": infobox,
        "links": sorted(set(links)),
        "art": _art(spec, rec),
        "texts": keys,
        "source": {"file": spec["source"], "bible": rec.get("bible") if spec.get("bible") == "per registry entry" else spec.get("bible")},
        "version": "sha256:" + h.hexdigest(),
    }


def build_all(content_dir: str = CONTENT, schema: dict = None, texts: dict = None, kinds=None) -> list:
    schema = schema or load_schema()
    texts = load_texts(content_dir) if texts is None else texts
    content = _content(content_dir)
    out = []
    for kind, spec in schema["kinds"].items():
        if kinds and kind not in kinds:
            continue
        for rec in records(kind, spec, schema, content):
            out.append(build_entry(kind, rec, schema, texts))
    return out


# ------------------------------------------------------------------------------------------------ validation

def resolve(ref: dict, texts: dict):
    """A summary/body source's words, or None when it doesn't resolve."""
    if not ref:
        return None
    if "text" in ref:
        return ((texts.get(ref["text"]) or {}).get("payload") or {}).get(ref["field"]) or None
    if "gameField" in ref:
        return ref.get("value") or None
    if "canon" in ref:
        return _canon(ref["canon"], ref["field"]) or None
    return None


def validate(entry: dict, schema: dict, texts: dict, known_ids: set) -> list:
    """Problems with one entry: 'error: ...' breaks a page, 'warn: ...' is missing polish (art, a long text)."""
    p = []
    eid = entry.get("id") or "?"
    for f in schema["entry"]["required"]:
        if f not in entry:
            p.append(f"error: {eid}: missing field {f}")
    if entry.get("schema") != ENTRY_SCHEMA:
        p.append(f"error: {eid}: schema is not {ENTRY_SCHEMA}")
    kind = entry.get("kind")
    if kind not in schema["kinds"]:
        p.append(f"error: {eid}: unknown kind {kind}")
        return p
    if not re.match(schema["idPattern"], eid):
        p.append(f"error: {eid}: id doesn't match {schema['idPattern']}")
    if eid != entry_id(kind, entry.get("gameId", ""), schema):
        p.append(f"error: {eid}: id isn't the {kind} template of gameId {entry.get('gameId')}")
    if not entry.get("title"):
        p.append(f"error: {eid}: no title")
    if not resolve(entry.get("summary"), texts):
        what = (entry.get("summary") or {}).get("text") or (entry.get("summary") or {}).get("gameField") or "summary"
        p.append(f"error: {eid}: summary doesn't resolve ({what})")
    for b in entry.get("body") or []:
        if not resolve(b, texts):
            p.append(f"warn: {eid}: body source has no words yet ({b.get('text') or b.get('gameField') or b.get('canon')}"
                     f"{' ' + b['field'] if b.get('field') else ''})")
    for k in entry.get("texts") or []:
        if k not in texts:
            p.append(f"error: {eid}: text key {k} isn't in Content/Text")
    for link in entry.get("links") or []:
        if link not in known_ids:
            p.append(f"warn: {eid}: links to {link}, which has no entry")
    art = entry.get("art")
    if art:
        if not os.path.isfile(os.path.join(RESOURCES, art)):
            p.append(f"warn: {eid}: art {art} isn't on disk")
        elif not art.endswith(".png") and not _jpg_ok(art):
            p.append(f"warn: {eid}: art {art} isn't a .png")
    return p


def validate_all(entries: list, schema: dict = None, texts: dict = None) -> list:
    schema = schema or load_schema()
    texts = load_texts() if texts is None else texts
    ids = [e["id"] for e in entries]
    known = set(ids)
    problems = []
    seen = set()
    for e in entries:
        if e["id"] in seen:
            problems.append(f"error: {e['id']}: two entries share this id")
        seen.add(e["id"])
        problems += validate(e, schema, texts, known)
    return problems


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--kind", action="append", help="only these kinds (repeatable)")
    ap.add_argument("--id", action="append", help="print these entries in full")
    ap.add_argument("-v", "--verbose", action="store_true", help="list every problem, not just counts")
    a = ap.parse_args(argv)
    schema = load_schema()
    texts = load_texts()
    every = build_all(schema=schema, texts=texts)  # links resolve against every kind
    entries = [e for e in every if not a.kind or e["kind"] in a.kind]
    for e in every:
        if a.id and e["id"] in a.id:
            print(json.dumps(e, indent=2, ensure_ascii=False, default=str))
    known = {e["id"] for e in every}
    problems = []
    seen = set()
    for e in entries:
        if e["id"] in seen:
            problems.append(f"error: {e['id']}: two entries share this id")
        seen.add(e["id"])
        problems += validate(e, schema, texts, known)
    by_kind = {}
    for e in entries:
        by_kind[e["kind"]] = by_kind.get(e["kind"], 0) + 1
    print("entries:", ", ".join(f"{k} {n}" for k, n in by_kind.items()), f"(total {len(entries)})")
    errors = [x for x in problems if x.startswith("error")]
    warns = [x for x in problems if x.startswith("warn")]
    print(f"{len(errors)} errors, {len(warns)} warnings")
    for x in (problems if a.verbose else errors[:40]):
        print("  " + x)
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
