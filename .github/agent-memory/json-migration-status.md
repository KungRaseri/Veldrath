# RealmEngine JSON → DB Migration Status

> **Status**: Reviewed and current as of Session-39 (2026-06-27).

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
- Newtonsoft remains in: `Veldrath.Server` (ServerSaveGameRepository), `RealmForge`, `Veldrath.Server.Tests` (test data fixtures)

## Session History

Multiple sessions (18–23) completed the JSON→DB migration incrementally:
- Session 18: Item/enchantment seeding (17 items, 9 enchantments), 19 content integration tests
- Session 19: Weapon/Armor/Material DTOs + endpoints (12 integration tests)
- Session 20: 11 catalog query handlers + 6 new content tables wired end-to-end (Organization, WorldLocation, Dialogue, ActorInstance, MaterialProperty, TraitDefinition)
- Session 21: Weapon/Armor → Item consolidation (clean break: `/api/content/items?type=weapon|armor`)
- Session 23: Database seeder completion (LootTables, Quests, WorldLocations, ActorInstances, TraitDefinitions), Power unification bug fixes

Final test count: all passing with zero failures across all projects.

> See git history for per-session code-level details.

## What Remains
- All `ContentDbContext` tables now have baseline seed data
- No known open gaps in seeding
- Potential future work: seeder tests (unit tests verifying seeder idempotency); seeded data expansion


