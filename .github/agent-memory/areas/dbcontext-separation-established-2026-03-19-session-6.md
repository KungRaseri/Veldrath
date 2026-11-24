---
kind: area
date: 2026-03-19
area: engine
title: "DbContext Separation (established 2026-03-19 session-6)"
---

## DbContext Separation (established 2026-03-19 session-6)

> **See [`.github/instructions/ef-core-patterns.md`](../../../.github/instructions/ef-core-patterns.md) for the canonical DbContext separation rules, migration workflow, entity placement conventions, and now four DbContexts (Application, Game, Content, Editorial).**

Historical note: DbContext separation was established in session-6 when `GameDbContext` was extracted from `ApplicationDbContext` to fix Docker startup failures. `EditorialDbContext` was added in session-35 for patch notes, lore articles, and editorial announcements. Server operational entities (`ServerSaveGameRepository`, `ServerHallOfFameRepository`) inject `GameDbContext`, not `ApplicationDbContext`.


### ActorClassDto (breaking change, session-3)
- `ActorClassDto` ctor gained `HitDie` (int), `PrimaryStat` (string), `RarityWeight` (int)
- All `new ActorClassDto(slug, name, typeKey)` calls must be updated to include all 6 params

### AvailableClasses Test
- `CharacterSelectViewModel.LoadAsync` fires-and-forgets on construction; completes synchronously if `FakeContentService` returns `Task.FromResult`
- Fallback test: use `new FakeContentService { Classes = [] }` + `await Task.Delay(50)` before asserting
- Catalog-populated test: pass non-empty classes + same delay

### FakeServerConnectionService
- Tracks sent commands in `SentCommands` list (added session-3)
