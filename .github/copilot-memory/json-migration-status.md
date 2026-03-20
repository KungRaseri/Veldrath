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

## Test Status (2026-03-20, session-17)
- Shared.Tests: 778 passed
- Core.Tests: 1,738 passed
- Data.Tests: 203 passed
- Server.Tests: 331 passed (+39 from session-17)

## What Remains (as of session-17)
- `ContentTypedEndpointTests.cs` added in session-17: 39 integration tests for /classes, /species, /backgrounds, /skills, /enchantments (+ slot filter), /abilities, /items (+ type filter)
- **Deferred**: `SeedItemsAsync` and `SeedEnchantmentsAsync` in `DatabaseSeeder.cs` — pending content design discussion
- Remaining unrepresented ContentDbContext tables (Organizations, WorldLocations, Dialogues, ActorInstances, MaterialProperties, TraitDefinitions) — no repos needed until a feature handler requires them
- No content data is ever loaded from the filesystem — all DB-backed
