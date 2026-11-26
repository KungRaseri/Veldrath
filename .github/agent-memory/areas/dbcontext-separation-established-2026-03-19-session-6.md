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

### Available Classes — the client test this covered is gone
- The `CharacterSelectViewModel.LoadAsync` / `FakeContentService` fire-and-forget gotcha (`Task.Delay(50)` after construction) went with the Avalonia client in session-68 (2026-10-25): neither type exists, and `FakeServerConnectionService` — the other client test fixture noted here — went with it.
- The path those tests exercised lives in the engine: `BeginCreationSessionHandler` asks `GetAvailableClassesQuery`, and both answers are pinned without any delay in `RealmEngine.Core.Tests/Features/CharacterCreation/` — `Queries/NewCharacterClassQueryHandlerTests.cs` (`Handle_ReturnsFailure_WhenNoClassesExist`) and `Commands/BeginCreationSessionHandlerTests.cs` (`Handle_ReturnsAvailableClasses_FromMediator`).
