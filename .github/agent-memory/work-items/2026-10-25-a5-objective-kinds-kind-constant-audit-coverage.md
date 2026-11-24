---
kind: work-item
date: 2026-10-25
area: web
title: "A5: objective kinds, kind constant, audit coverage"
---

### Session-88 (2026-10-25) — A5: objective kinds, kind constant, audit coverage

**`QuestJournal.Advance` was already prefix-generic** (`kind:` + exact key, case-insensitive), so adding a kind is
*documentation + one call site + audit coverage*, not a matcher change. That is now written down where it belongs:
`QuestObjectiveKinds` (kill, collect, visit, craft, reach_level, talk, deliver) with XML docs on every constant, and
`Advance`'s parameter doc points at it.

**Call sites added:**
- `craft:` — `CraftItem` advances with `CraftItemHubResult.ItemCrafted` after a successful craft.
- `reach_level:` — the quest turn-in's `GainExperienceHubCommand` award advances **both** `NewLevel` and
  `LeveledUpTo` on a level-up, so content naming either value completes. (The client-callable hub method that used to
  do this was retired 2026-11-23; the turn-in carries the advance now.)
- `talk:` — new hub method `TalkToNpc(string npcSlug)` (validates the slug is non-empty, then advances
  `talk:<npcSlug>`). There was no dialogue interaction surface at all; this is the one call site, and the client will
  use it once NPC dialogue lands.
- **`deliver:` is reserved, loudly.** A hand-over needs inventory-removal semantics and a recipient model that do not
  exist, so instead of a fake call site the content audit now reports rule `unsupported-quest-kind` for any
  `deliver:` objective. Authored content can therefore never sit there silently never progressing.

**Audit widened** (`ContentReferenceRules.AuditQuestObjectives`): `craft:` validates against item slugs,
`reach_level:` must be a positive number, `deliver:` is reported as reserved. `talk:` is deliberately **not**
validated yet — there is no NPC catalog passed into the rules; the switch carries a comment saying so.

**Kill-key shim:** the dual slug+name advance already exists from C3; the name path now logs a `Debug` line naming the
legacy key so migration is visible without spamming Info.

**Tests:** new `QuestObjectiveKindTests` (8 theory rows across the new kinds + name-vs-slug mismatch, plus
completed-quest immunity) and `QuestObjectiveAuditTests` (craft known/unknown, reach_level number, deliver reserved).
Server 790 → **803**, full solution **3,948**, format clean.

---
