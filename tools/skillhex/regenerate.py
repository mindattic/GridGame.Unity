"""Regenerate skillhex.json from content (the "Regenerate from content" tool).

This throws away hand edits to the board: it re-runs the layout generator (docs/design/skill-hexagon/generate.js)
over data.js in a headless browser and rewrites skillhex.json, then runs sync.py. Heroes with Status New and the
Note on any node or hero that still exists are carried over from the current file.
Normal page loads never run the generator; they draw skillhex.json as it is.

    python tools/skillhex/regenerate.py            # rewrite skillhex.json and skillhex.js
    python tools/skillhex/regenerate.py --check    # only report whether the generator still matches the file
"""
import json
import subprocess
import sys
from pathlib import Path

from playwright.sync_api import sync_playwright

PAGE = Path(__file__).resolve().parents[2] / "docs" / "design" / "skill-hexagon"
SRC = PAGE / "skillhex.json"


def generate():
    with sync_playwright() as p:
        try:
            b = p.chromium.launch()
        except Exception:
            b = p.chromium.launch(channel="msedge")
        pg = b.new_page()
        pg.set_content("<!doctype html><meta charset='utf-8'><body></body>")
        pg.add_script_tag(path=str(PAGE / "data.js"))
        pg.add_script_tag(path=str(PAGE / "generate.js"))
        data = pg.evaluate("window.SkillHexExport()")
        b.close()
    return data


def merge(new, old):
    notes = {(n["Q"], n["R"]): n["Note"] for n in old.get("Nodes", []) if n.get("Note")}
    for n in new["Nodes"]:
        if (n["Q"], n["R"]) in notes:
            n["Note"] = notes[(n["Q"], n["R"])]
    hnotes = {h["Class"]: h["Note"] for h in old.get("Heroes", []) if h.get("Note") and h.get("Class")}
    for h in new["Heroes"]:
        if h["Class"] in hnotes:
            h["Note"] = hnotes[h["Class"]]
    new["Heroes"] += [h for h in old.get("Heroes", []) if h.get("Status") == "New"]
    return new


def dump(data):
    # one node / hero per line keeps diffs and hand edits readable
    lines = ['{']
    keys = list(data.keys())
    for i, k in enumerate(keys):
        v = data[k]
        end = ',' if i < len(keys) - 1 else ''
        if isinstance(v, list) and k in ('Nodes', 'Heroes', 'Stats', 'Families'):
            lines.append(f'  "{k}": [')
            lines += ['    ' + json.dumps(x, ensure_ascii=False) + (',' if j < len(v) - 1 else '') for j, x in enumerate(v)]
            lines.append('  ]' + end)
        else:
            lines.append(f'  "{k}": ' + json.dumps(v, ensure_ascii=False) + end)
    lines.append('}')
    return '\n'.join(lines) + '\n'


def main():
    old = json.loads(SRC.read_text(encoding="utf-8")) if SRC.exists() else {}
    text = dump(merge(generate(), old))
    if "--check" in sys.argv:
        same = SRC.exists() and SRC.read_text(encoding="utf-8") == text
        print("skillhex.json matches the generator" if same else "skillhex.json differs from the generator (hand edits or content changes)")
        return 0 if same else 1
    SRC.write_text(text, encoding="utf-8", newline="\n")
    print(f"wrote {SRC}")
    return subprocess.call([sys.executable, str(Path(__file__).with_name("sync.py"))])


if __name__ == "__main__":
    sys.exit(main())
