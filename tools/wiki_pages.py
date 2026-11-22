#!/usr/bin/env python3
"""GG-T627: emit static wiki pages (Markdown) from the wiki entries GG-T626 builds (BIBLE §33, tools/wiki_schema.py).

One page per entry at <out>/<kind>/<id>.md, an index per kind and a top index. Words come only from the applied Prose
texts (Content/Text) and game fields the entries point at; a text that isn't written yet shows a visible marker and is
reported, never filled in. Output is deterministic and goes to a git-ignored folder (default _wiki/).

    python tools/wiki_pages.py [--out _wiki] [--kind class] [-v]

Standard library only.
"""

from __future__ import annotations

import argparse
import os
import shutil
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import wiki_schema as ws  # noqa: E402

DEFAULT_OUT = os.path.join(ws.ROOT, "_wiki")
MARKER = ".wiki-generated"
PLURAL = {"ability": "Abilities", "class": "Classes", "enemy": "Enemies", "glossary": "Glossary", "lore": "Lore", "currency": "Currencies", "npc": "NPCs",
          "armorset": "Armor sets", "skilltree": "Skill trees"}


def plural(kind: str) -> str:
    return PLURAL.get(kind, kind.capitalize() + "s")


def _rel(from_dir: str, target: str) -> str:
    return os.path.relpath(target, from_dir).replace(os.sep, "/")


class Site:
    def __init__(self, entries: list, texts: dict, out: str):
        self.entries = entries
        self.texts = texts
        self.out = out
        self.by_id = {e["id"]: e for e in entries}
        self.missing = []  # (entry id, what)

    def page_path(self, e: dict) -> str:
        return os.path.join(self.out, e["kind"], e["id"] + ".md")

    def link(self, from_dir: str, eid: str) -> str:
        e = self.by_id.get(eid)
        if not e:
            return f"`{eid}`"
        return f"[{_md(e['title'])}]({_rel(from_dir, self.page_path(e))})"

    def words(self, e: dict, ref: dict) -> str:
        w = ws.resolve(ref, self.texts)
        if w:
            return str(w).strip()
        what = ref.get("text") or ref.get("gameField") or ref.get("canon") or "text"
        if ref.get("field"):
            what += " " + ref["field"]
        self.missing.append((e["id"], what))
        return f"*Not written yet* (`{what}`)"

    def value(self, from_dir: str, v) -> str:
        if isinstance(v, list):
            return ", ".join(self.value(from_dir, x) for x in v) or "—"
        if isinstance(v, dict):
            return ", ".join(f"{_md(str(k))} {self.value(from_dir, x)}" for k, x in v.items()) or "—"
        if isinstance(v, str) and v in self.by_id:
            return self.link(from_dir, v)
        return _md(str(v)).replace("\n", " ")

    def render(self, e: dict) -> str:
        d = os.path.dirname(self.page_path(e))
        label = {"npc": "NPC", "armorset": "Armor set", "skilltree": "Skill tree"}.get(e["kind"], e["kind"].capitalize())
        lines = [f"# {_md(e['title'])}", "", f"*{label}* · `{e['id']}`", ""]
        if e.get("art"):
            lines += [f"![{_md(e['title'])}]({_rel(d, os.path.join(ws.RESOURCES, e['art']))})", ""]
        summary = e.get("summary")
        if summary:
            lines += [self.words(e, summary), ""]
        if e.get("infobox"):
            lines += ["| | |", "|---|---|"]
            for label, v in e["infobox"].items():
                lines.append(f"| {_md(str(label))} | {self.value(d, v)} |")
            lines.append("")
        for b in e.get("body") or []:
            if b == summary:
                continue
            lines += [self.words(e, b), ""]
        unlinked = [x for x in e.get("links") or [] if x not in _infobox_ids(e)]
        if unlinked:
            lines += ["## See also", ""] + [f"- {self.link(d, x)}" for x in unlinked] + [""]
        src = e.get("source") or {}
        foot = f"Source: `{src.get('file')}`"
        if src.get("bible"):
            foot += f" · BIBLE {src['bible']}"
        lines += ["---", "", f"<sub>{foot} · {e['version'][:19]}</sub>", ""]
        return "\n".join(lines)

    def kind_index(self, kind: str, items: list) -> str:
        d = os.path.join(self.out, kind)
        lines = [f"# {plural(kind)}", "", f"[All kinds]({_rel(d, os.path.join(self.out, 'index.md'))})", ""]
        lines += [f"- {self.link(d, e['id'])}" for e in items]
        return "\n".join(lines) + "\n"

    def top_index(self, kinds: list) -> str:
        lines = ["# GridGame wiki", "", "Generated from the game's content and Prose texts (tools/wiki_pages.py). Do not edit.", ""]
        for kind, items in kinds:
            lines.append(f"- [{plural(kind)}]({kind}/index.md) ({len(items)})")
        return "\n".join(lines) + "\n"


def _infobox_ids(e: dict) -> set:
    out = set()
    for v in (e.get("infobox") or {}).values():
        for x in (v if isinstance(v, list) else [v]):
            if isinstance(x, str):
                out.add(x)
    return out


def _md(s: str) -> str:
    return s.replace("|", "\\|")


def _write(path: str, text: str):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


def _clean(out: str):
    """Clear a previous run, but only a folder this tool made."""
    if not os.path.isdir(out):
        return
    if not os.path.isfile(os.path.join(out, MARKER)) and os.listdir(out):
        raise SystemExit(f"{out} exists and wasn't made by wiki_pages.py; pick another --out")
    shutil.rmtree(out)


def generate(out: str = DEFAULT_OUT, kinds=None):
    schema = ws.load_schema()
    texts = ws.load_texts()
    every = ws.build_all(schema=schema, texts=texts)
    order = list(schema["kinds"])
    every.sort(key=lambda e: (order.index(e["kind"]), str(e["title"]).lower(), e["id"]))
    entries = [e for e in every if not kinds or e["kind"] in kinds]
    site = Site(every, texts, out)
    _clean(out)
    grouped = []
    for kind in order:
        items = [e for e in entries if e["kind"] == kind]
        if not items:
            continue
        grouped.append((kind, items))
        for e in items:
            _write(site.page_path(e), site.render(e))
        _write(os.path.join(out, kind, "index.md"), site.kind_index(kind, items))
    _write(os.path.join(out, "index.md"), site.top_index(grouped))
    _write(os.path.join(out, MARKER), "generated by tools/wiki_pages.py\n")
    return grouped, site.missing


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--out", default=DEFAULT_OUT, help="output folder (default _wiki/, git-ignored)")
    ap.add_argument("--kind", action="append", help="only these kinds (repeatable); links still resolve to every kind")
    ap.add_argument("-v", "--verbose", action="store_true", help="list every missing text")
    a = ap.parse_args(argv)
    grouped, missing = generate(os.path.abspath(a.out), a.kind)
    total = sum(len(i) for _, i in grouped)
    print("pages:", ", ".join(f"{k} {len(i)}" for k, i in grouped), f"(total {total}) -> {a.out}")
    print(f"{len(missing)} texts not written yet, on {len({m[0] for m in missing})} pages")
    for eid, what in (missing if a.verbose else missing[:20]):
        print(f"  {eid}: {what}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
