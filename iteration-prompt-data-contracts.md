Continue iterating on `RealmEngine.Data` and `RealmUnbound.Contracts`, maintaining consistency with the completed move away from JSON file-based content toward PostgreSQL/EF Core as the single source of truth for all content.

Setup:
- Read `.github/copilot-memory/engine-codebase.md` and `.github/copilot-memory/json-migration-status.md` before writing any code. Cross-check those files AND the "Completed in previous sessions" section above so already-completed work is not re-attempted.
- Run `dotnet test RealmEngine.slnx` to establish the baseline. All work must finish with zero regressions against that baseline.

Completed in previous sessions (do not re-implement):
- `ISpeciesRepository` / `EfCoreSpeciesRepository` — done, registered, tested
- `IItemRepository` / `EfCoreItemRepository` — done, registered, tested
- `IEnchantmentRepository` / `EfCoreEnchantmentRepository` — done, registered, tested
- `INodeRepository` / `EfCoreNodeRepository` + `HarvestableNodeRecord` entity + `AddHarvestableNodes` migration — done, registered, tested
- `EfCoreBackgroundRepository` colon-split dead code removed — done
- XML doc comments on `ActorClassDto`, `CharacterDto`, `CreateCharacterRequest` — done
- Tests for `EfCoreBackgroundRepository`, `EfCoreEquipmentSetRepository`, `EfCoreCharacterClassRepository`, `EfCoreSpeciesRepository`, `EfCoreNamePatternRepository`, `EfCoreItemRepository`, `EfCoreEnchantmentRepository`, `EfCoreNodeRepository` — done

Goals — in priority order:
1. **Remaining Data repository test coverage** — these EfCore repositories still have zero tests in `RealmEngine.Data.Tests/`. Use the existing `TestContentDbContextFactory` (SQLite in-memory) pattern. For each: seed minimal data, cover `GetAllAsync`, `GetBySlug`/`GetByName`, any `GetByType` overload, and the not-found path. Missing repos:
   - `EfCoreArmorRepository`
   - `EfCoreWeaponRepository`
   - `EfCoreMaterialRepository`
   - `EfCoreSpellRepository`
   - `EfCoreQuestRepository`
   - `EfCoreNpcRepository`
   - `EfCoreEnemyRepository`
   - `EfCoreLootTableRepository`
   - `EfCoreHallOfFameRepository` (uses `GameDbContext`, not `ContentDbContext`)
2. **Core catalog feature handlers** — `ISpeciesRepository`, `IItemRepository`, and `IEnchantmentRepository` are registered in DI but no MediatR handler queries them. Add read-only query handlers in `RealmEngine.Core/Features/`:
   - `GetSpeciesQuery` → returns `IReadOnlyList<Species>` (optionally filtered by `TypeKey`)
   - `GetItemCatalogQuery` → returns `IReadOnlyList<Item>` (optionally filtered by `ItemType`)
   - `GetEnchantmentCatalogQuery` → returns `IReadOnlyList<Enchantment>` (optionally filtered by `TargetSlot`)
   For each: place in a sensibly named feature folder under `Core/Features/`, add FluentValidation validator if any input is present, and add handler tests in `RealmEngine.Core.Tests/`.
3. **Server endpoints for Items and Enchantments** — `ContentEndpoints.cs` already has dedicated `/api/content/species` endpoints. Add matching endpoints for:
   - `GET /api/content/items` — all items; optional `?type=` query string
   - `GET /api/content/items/{slug}` — single item by slug
   - `GET /api/content/enchantments` — all enchantments; optional `?targetSlot=` query string
   - `GET /api/content/enchantments/{slug}` — single enchantment by slug
   Map results through the existing `ContentSummaryDto` / `ContentDetailDto` pattern or add typed DTOs to `RealmUnbound.Contracts` if the existing generic shape is insufficient.
4. **DatabaseSeeder gaps** — `SeedItemsAsync` and `SeedEnchantmentsAsync` are absent; both tables are empty on a fresh DB. **Do not implement this goal yet.** The items and enchantments to seed need to be decided in a separate discussion before any code is written. Leave a `// TODO: SeedItemsAsync — pending content design discussion` comment in `DatabaseSeeder.cs` as a marker if helpful, but do not add any seeding logic.

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
