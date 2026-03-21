# RealmEngine JSON → DB Migration Status

## What Was Done
- Deleted all `RealmEngine.Shared/Data/` JSON data model files (AbilityDataModels, ClassCatalogDataModels, EnemyNpcTraitDataModels, GameDataModels, ItemTraitDataModels, JsonHelpers, NpcCatalogDataModels, QuestCatalogDataModels, QuestDataModels, QuestObjectivesDataModels, QuestRewardsDataModels)
- Deleted `DataReferenceResolver.cs` and `PatternExecutor.cs` (old JSON-walk utilities)
- Renamed CatalogServices → DataServices (AbilityDataService, SkillDataService, SpellDataService, ItemDataService, RecipeDataService) — all now query EF Core
- Refactored `NameComposer` to use `NamePatternService` backed by DB (INamePatternRepository)
- `CategoryDiscoveryService` fully DB-backed
- New migration seeds budget-system configs into GameConfigs table

## Status: COMPLETE ✅

All Newtonsoft migration done as of 2026-03-16:
- A/B/C all done in a prior session (Core, BudgetConfigFactory, RarityConfigService, HarvestingConfigService, CharacterGrowthService — all using System.Text.Json)
- `EfCoreSaveGameRepository.cs` migrated to System.Text.Json with `PropertyNameCaseInsensitive = true`
- `background.cs` `[JsonProperty]` attributes removed
- Newtonsoft removed from: `RealmEngine.Core.csproj`, `RealmEngine.Shared.csproj`, `RealmEngine.Data.csproj`
- Newtonsoft remains in: `RealmUnbound.Server` (ServerSaveGameRepository), `RealmForge`, `RealmUnbound.Server.Tests` (test data fixtures)

## Test Status (session-20)
- Shared.Tests: 778 passed
- Core.Tests: **1,847 passed** (+109: 11 Goals-1/2 handlers + tests, 6 Goal-3 handlers + tests)
- Data.Tests: **227 passed** (+24: 6 Goal-3 EfCore repo test classes × 4 tests each)
- Server.Tests: 362 passed (unchanged)

## What Was Done (session-20)

### Goal 1+2 — 11 missing Core catalog query handlers
All 11 wired end-to-end (Core handler + unit tests):
`AbilityCatalog`, `EnemyCatalog`, `NpcCatalog`, `QuestCatalog`, `RecipeCatalog`, `LootTableCatalog`, `SpellCatalog`, `SkillCatalog`, `MaterialCatalog`, `WeaponCatalog`, `ArmorCatalog`

### Goal 3 — 6 new content tables wired end-to-end
Created: shared models, interfaces, EfCore repos, Program.cs registrations, ContentContracts.cs DTOs, ContentEndpoints.cs routes + handlers + mappers, Core handlers, Core.Tests, Data.Tests
- `Organization` / `WorldLocation` / `Dialogue` / `ActorInstance` / `MaterialProperty` / `TraitDefinition`
- All 12 server routes registered; TraitDefinition uses `/traits/{key}` (not slug)

## What Was Done (session-18)
- `SeedItemsAsync` implemented in `DatabaseSeeder.cs`: 17 items covering all 6 ItemType values (consumable, crystal, gem, rune, essence, orb), with factory helpers `I()`, `ISt()`, `ITr()`
- `SeedEnchantmentsAsync` implemented in `DatabaseSeeder.cs`: 9 enchantments covering all 3 TargetSlot values (weapon ×4, armor ×3, any ×2), with factory helpers `EE()`, `ESt()`, `ETr()`
- `SeedContentRegistryAsync` updated to also register Items (`items/general`) and Enchantments (`items/enchantments`)
- `ContentExtendedEndpointTests.cs` added in `RealmUnbound.Server.Tests/Features/`: 19 integration tests for /enemies, /npcs, /quests, /recipes, /loot-tables, /spells

## What Was Done (session-19)
- `WeaponDto`, `ArmorDto`, `MaterialDto` added to `RealmUnbound.Contracts/Content/ContentContracts.cs`
- `GET /api/content/weapons` (+slug), `GET /api/content/armors` (+slug), `GET /api/content/materials` (+slug) added to `ContentEndpoints.cs` — backed by `IWeaponRepository`, `IArmorRepository`, `IMaterialRepository` respectively
- `ContentEquipmentEndpointTests.cs` added in `RealmUnbound.Server.Tests/Features/`: 12 integration tests for /weapons, /armors, /materials

## What Remains (as of session-19)
- Remaining unrepresented ContentDbContext tables (Organizations, WorldLocations, Dialogues, ActorInstances, MaterialProperties, TraitDefinitions) — no repos needed until a feature handler requires them
- No content data is ever loaded from the filesystem — all DB-backed
