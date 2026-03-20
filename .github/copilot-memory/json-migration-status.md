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

## Test Status (2026-03-20, session-18)
- Shared.Tests: 778 passed
- Core.Tests: 1,738 passed
- Data.Tests: 203 passed
- Server.Tests: 350 passed (+19 from session-18: ContentExtendedEndpointTests — enemies, NPCs, quests, recipes, loot-tables, spells)

## What Was Done (session-18)
- `SeedItemsAsync` implemented in `DatabaseSeeder.cs`: 17 items covering all 6 ItemType values (consumable, crystal, gem, rune, essence, orb), with factory helpers `I()`, `ISt()`, `ITr()`
- `SeedEnchantmentsAsync` implemented in `DatabaseSeeder.cs`: 9 enchantments covering all 3 TargetSlot values (weapon ×4, armor ×3, any ×2), with factory helpers `EE()`, `ESt()`, `ETr()`
- `SeedContentRegistryAsync` updated to also register Items (`items/general`) and Enchantments (`items/enchantments`)
- `ContentExtendedEndpointTests.cs` added in `RealmUnbound.Server.Tests/Features/`: 19 integration tests for /enemies, /npcs, /quests, /recipes, /loot-tables, /spells

## What Remains (as of session-18)
- Remaining unrepresented ContentDbContext tables (Organizations, WorldLocations, Dialogues, ActorInstances, MaterialProperties, TraitDefinitions) — no repos needed until a feature handler requires them
- No content data is ever loaded from the filesystem — all DB-backed
