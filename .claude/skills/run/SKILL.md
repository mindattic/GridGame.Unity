---
name: run
description: Launch the already-published GridGame build (C:\Apps\GridGame\GridGame.exe) — the same exe the Desktop shortcut GridGame.lnk opens. Does not rebuild; use /deploy first for a fresh build.
---

When invoked:

In one tool call: if `C:\Apps\GridGame\GridGame.exe` is missing, say so and point at
`/deploy`; otherwise `Start-Process` it. Don't invoke the `unthrottle` skill yourself. A
self-invoked one does nothing (only a user-typed `/unthrottle` writes the context gate's marker).

This is the published build, not a dev/Editor run. For iterating on code, use `tools\test.ps1`,
`tools\build.ps1`, or the harness (`python tools\harness.py smoke|sweep|campaign`) per AGENTS.md
instead — this skill only ever opens the fixed `C:\Apps\GridGame\` copy.
