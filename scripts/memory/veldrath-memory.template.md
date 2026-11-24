# Veldrath Server + Web Client — Memory Notes

> **Status**: this file is the **index** of the memory store, not a log. Every note lives in
> `work-items/` (one file per work item) or `areas/` (one file per stable topic). The table at
> the foot is generated from their YAML front matter, and this whole file is generated from
> `scripts/memory/veldrath-memory.template.md` — a hand edit is a failed build.

The Avalonia desktop client is gone — Veldrath is web-first, and the SvelteKit app in `web/` is the
only client. Notes written before that pivot mention `Veldrath.Client`, `GameClient.Core`,
`Razor`/`MudBlazor` or `/api/foundry-bridge/*`; they are historical session logs, not current
guidance.

## How to add a note

One file per work item, and only your own file — a new path cannot conflict with another branch:

1. Create `.github/agent-memory/work-items/<yyyy-mm-dd>-<card>-<slug>.md`. `<card>` is the
   `t_xxxxxxxx` id and is omitted when the work has none; the date is the day the work landed.
2. Start it with the front matter below and write the note underneath. The date in the file name
   and `date:` agree, or both say `undated`.
3. Regenerate the index with `node scripts/memory/memory-index.mjs --write` and commit both files.

```yaml
---
kind: work-item
date: 2026-11-23
area: content
card: t_d97538ea
pr: 244
title: "Progression is server-side; a level-up moves the health bar"
---
```

`area` is one of `content`, `engine`, `web`, `map`, `auth` or `infra`; `card` and `pr` are optional.
A stable topic that outgrows a session log becomes `areas/<topic>.md` with `kind: area`.

## Rules

- **One file per work item.** Never append to another card's file, and never append a
  `### Session-*` block to this one — the generator would erase it.
- **This file is generated.** `node scripts/memory/memory-index.mjs --check` compares the whole
  file byte-for-byte with what the template and the notes produce, so any hand edit — a preamble
  change, a table cell, or a note appended below the table — fails.
- **Counters have one source.** Where a number is asserted by a test, the note points at the test
  and does not restate the number — the seeded map graph is the worked example, and
  `RealmEngine.Data.Tests/Seeders/MapGraphSeederTests.cs` owns both the map/node/edge totals and the
  "every live zone is enterable through a door" list.
- **The other seven reference files are not split.** `engine-codebase.md`,
  `json-migration-status.md`, `foundry-content-schema.md`, `gap-analysis-process.md`,
  `auth-and-character-creation-plan.md`, `combat-loop-plan.md` and `world-lore-plan.md` are edited in
  place; only this index is generated.

## Index

Newest first. `—` in the Date column means the note carries no date of its own.

<!-- memory-index:begin -->
<!-- memory-index:end -->
