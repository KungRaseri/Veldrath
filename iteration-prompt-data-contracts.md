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
- Tests for `EfCoreArmorRepository`, `EfCoreWeaponRepository`, `EfCoreMaterialRepository`, `EfCoreSpellRepository`, `EfCoreQuestRepository`, `EfCoreNpcRepository`, `EfCoreEnemyRepository`, `EfCoreLootTableRepository`, `EfCoreHallOfFameRepository` — done (session-15)
- `GetSpeciesQuery`, `GetItemCatalogQuery`, `GetEnchantmentCatalogQuery` Core handlers with tests — done (session-15)
- `GET /api/content/items` (+slug), `GET /api/content/enchantments` (+slug) server endpoints — done (session-15)
- `ItemDto`, `EnchantmentDto` added to `RealmUnbound.Contracts` — done (session-15)
- Tests for `InMemoryArmorRepository`, `InMemoryWeaponRepository`, `InMemoryMaterialRepository`, `InMemoryEquipmentSetRepository` stubs — done (session-16)

Goals — in priority order:
1. **DatabaseSeeder for Items and Enchantments** — implement `SeedItemsAsync` and `SeedEnchantmentsAsync` in `DatabaseSeeder.cs` once a content design discussion has been had. TODO comments are already in place as markers. **Do not implement until the items/enchantments catalog content is decided.**
2. **Integration tests for typed content endpoints** — `ContentEndpoints.cs` now has 29 typed routes (abilities, enemies, npcs, quests, recipes, loot-tables, spells, classes, species, backgrounds, skills, items, enchantments). None have integration test coverage in `RealmUnbound.Server.Tests`. Consider adding `WebApplicationFactory`-based integration tests for the most important routes.
3. **Add repositories for unrepresented ContentDbContext tables** — the following `DbSet<T>` tables in `ContentDbContext` have no corresponding repository interface. Add them when needed by a feature handler:
   - `Organizations` → `IOrganizationRepository`
   - `WorldLocations` → `IWorldLocationRepository`
   - `Dialogues` → `IDialogueRepository`
   - `ActorInstances` → `IActorInstanceRepository`
   - `MaterialProperties` → `IMaterialPropertyRepository`
   - `TraitDefinitions` → `ITraitDefinitionRepository`

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
