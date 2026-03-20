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

## Test Status (2026-03-20, session-14)
- Shared.Tests: 778 passed
- Core.Tests: 1,729 passed
- Data.Tests: 157 passed

## What Remains (as of session-14)
- No `IItemRepository` / `IEnchantmentRepository` / `INodeRepository` / `ISpeciesRepository` gaps remain \u2014 all implemented, registered, and tested
- EfCore repos still lacking tests: Armor, Enemy, LootTable, Material, Npc, Quest, Weapon, Spell (out of scope for this prompt iteration)
- No content data is ever loaded from the filesystem — all DB-backed
