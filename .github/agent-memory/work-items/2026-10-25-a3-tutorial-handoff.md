---
kind: work-item
date: 2026-10-25
area: content
title: "A3: tutorial handoff"
---

### Session-86 (2026-10-25) — A3: tutorial handoff

**Where the tutorial lives.** The start location is content (`ZoneLocationTraits.IsTutorialStart`, surfaced as
`ZoneLocationEntry.IsTutorialStart`; another JSON-column property, so again **no migration**). The *quest* comes from
options (`Tutorial:TutorialQuestSlug`, default `first-steps`) because finding a quest by a content flag would need a
new `IQuestRepository` method plus every implementation and fake; a configured slug is equivalent and loud when the
content is missing.

**Two-state design, deliberately:**
- `TutorialStarted` in the attribute blob marks "already handed to the tutorial", so logging in again never re-places
  or re-accepts.
- Completion is **derived from the journal** (the tutorial quest entry reaching `Completed`) instead of being copied
  into `TutorialCompleted`. One source of truth, no drift — the plan's blob flag is intentionally not used.

**Flow.** `PrepareTutorialHubCommandHandler` (Features/Quest) runs from `SelectCharacter` **after** the map-position
block: skip if the journal entry is completed, skip if `TutorialStarted`, find the location flagged
`IsTutorialStart` (via `GetAllAsync`), accept the quest through the existing `AcceptQuestHubCommand` so the journal is
written once, then set `CurrentZoneId` + `CurrentZoneLocationSlug` + `TutorialStarted` in one save. Missing start
location or a failed accept → `Error` log + `Warning` the hub forwards as `SystemMessage`, and the character keeps the
normal starting point.

**Hub detail:** `SelectCharacter` treats a **null** handoff result as a loud failure (Error log) rather than an NRE —
which is also what keeps every existing hub test working, since they run with `Mock.Of<ISender>()`.

**Tests:** `PrepareTutorialHubCommandTests` (5: places + accepts, skips completed, skips started, no start location,
quest missing). Server 785 → **790**, full solution **3,935**, format clean.

**Trap:** `Characters.QuestBlob` is NOT NULL — test seeders must default it to `string.Empty`, and
`Character.CurrentZoneId` already defaults to `crestfall`, so "did not move" assertions must compare against the
seeded value, not against empty.

---
