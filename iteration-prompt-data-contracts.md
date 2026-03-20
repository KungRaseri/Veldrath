Continue iterating on `RealmEngine.Data` and `RealmUnbound.Contracts`, maintaining consistency with the completed move away from JSON file-based content toward PostgreSQL/EF Core as the single source of truth for all content.

Setup:
- Read `.github/copilot-memory/engine-codebase.md` and `.github/copilot-memory/json-migration-status.md` before writing any code. Cross-check those files so already-completed work is not re-attempted.
- Run `dotnet test RealmEngine.slnx` to establish the baseline. All work must finish with zero regressions against that baseline.

Goals — in priority order:
1. **Missing EF repositories** — several `ContentDbContext` entities have no `IXxxRepository` / `EfCoreXxxRepository` pair. Implement them end-to-end: interface in `RealmEngine.Core/Abstractions/`, EF Core implementation in `RealmEngine.Data/Repositories/`, registration in `RealmUnbound.Server/Program.cs`, and tests in `RealmEngine.Data.Tests/`. Priority: `ISpeciesRepository` (`SpeciesDto` already in Contracts, needed by character creation); `INodeRepository` + `EfCoreNodeRepository` (`InMemoryNodeRepository` is the spec — match its interface, then create the EF entity and migration); `IItemRepository` (general items not covered by weapon/armor repos); `IEnchantmentRepository` (referenced by `Item.EnchantmentIds`). For each: add a `GetAllAsync`, `GetBySlug`, and `GetByTypeAsync` as appropriate; use a `MapToModel(entity)` private static method; `AsNoTracking()` on all queries.
2. **Legacy format cleanup** — `EfCoreBackgroundRepository.GetBackgroundByIdAsync` splits on `:` to handle the old `"backgrounds/strength:soldier"` format. `Character.BackgroundId` now stores a bare slug, so that split-and-fallback is dead code. Remove it and query directly by slug. Add a test confirming the bare-slug path works.
3. **Contracts alignment** — add XML doc comments to `ActorClassDto.TypeKey` (holds the DB `TypeKey` column value, e.g. `"warriors"`; not a `@classes/...` prefix), `CharacterDto.ClassName` and `CreateCharacterRequest.ClassName` (stores the class `DisplayName`, e.g. `"Warrior"` — not a slug). Verify `SpeciesDto` field names align with the `Species` EF entity once `ISpeciesRepository` is implemented; adjust if they diverge.
4. **Data repository test coverage** — these EfCore repositories have zero tests in `RealmEngine.Data.Tests/`: `EfCoreBackgroundRepository`, `EfCoreEquipmentSetRepository`, `EfCoreNamePatternRepository`, `EfCoreCharacterClassRepository`. Use the `TestContentDbContextFactory` (SQLite in-memory) pattern already in the project. For each: seed minimal data per test, cover `GetAllAsync`, `GetBySlug`/`GetByName`, any `GetByType` overload, and the not-found path.

Process:
- Use the Explore subagent to run a gap analysis before writing any code. Cross-check `.github/copilot-memory/` so already-completed items are not re-reported.
- Fix missing repositories and legacy cleanup before writing tests for them — tests written against broken code have to be rewritten anyway.
- Every new repository interface must have full XML doc comments — CS1591 is a hard compile error in all non-test projects.
- After adding any `IXxxRepository`, verify `Program.cs` in `RealmUnbound.Server` registers the concrete implementation. Run `dotnet build RealmUnbound.Server` after every `Program.cs` change.
- New migrations go in `RealmEngine.Data/Migrations/` (ContentDbContext) or `RealmEngine.Data/Migrations/GameDb/` (GameDbContext). Never add a migration to `RealmUnbound.Server/Migrations/` unless it targets `ApplicationDbContext`.
- `CharacterGrowthService.GetClassMultipliers` accepts a `@classes/{typeKey}:{slug}` string because `ClassRef` values in the `growth-stats` GameConfig blob use that format — this is intentional config-schema design, not a migration gap. Do not change it.
- Run `dotnet build RealmEngine.slnx` and `dotnet test RealmEngine.slnx` after each batch of changes.

Wrap-up:
- If any non-obvious constraints, gotchas, or architectural decisions were discovered during the session, write them into `.github/copilot-memory/engine-codebase.md` or `.github/copilot-memory/json-migration-status.md` (edit directly using file tools). Only record things that would have caused wasted time if unknown at the start of a future session.
- Update `.github/copilot-memory/json-migration-status.md` with what was completed and what still remains.

Rules that must never be broken:
- Never suppress CS1591. Never add `NoWarn` entries to any `.csproj`.
- Never create breadcrumb or placeholder files — finish the work or don't create the file.
- Never apply `[Obsolete]` — always move forward with new implementations.
- Engine libraries (`Core`, `Shared`, `Data`) must remain UI-agnostic — no Avalonia, SignalR, or ASP.NET Core references in those projects.
- Never read content from the filesystem in `RealmEngine.Data` or `RealmEngine.Core`. All content comes from the database. If a test needs content data, seed it in the test setup.
- `Character.ClassName` (the server EF entity) stores the class `DisplayName` (e.g. `"Warrior"`). Never store a slug or `@classes/...` format in that field.
- `@classes/...` strings belong only inside the `growth-stats` `GameConfig` JSON blob. Do not introduce this format anywhere else.
