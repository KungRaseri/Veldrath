# RealmEngine Codebase Notes

## Test Counts (as of 2026-03-16)
- RealmEngine.Core.Tests: 1,283 passing
- RealmEngine.Shared.Tests: 690 passing
- RealmEngine.Data.Tests: 119 passing

## Key Model Facts
- `Location` has 4 required properties: `Id`, `Name`, `Description`, `Type`
- `Background` uses `Slug` (not `Id`) as its identifier
- `Enemy.Prefixes` and `Enemy.Suffixes` are `List<NameComponent>`, not `List<string>`
- `SaveGame` uses `PlayerName` (not `CharacterName`)
- `NameComponent` has `Token` (semantic key) and `Value` (display text) properties
- `Character.GainExperience()` auto-levels at `Level * 100` XP
- `InventoryRecord` now has nullable `Durability` column (added migration `AddInventoryRecordDurability`)

## Positional Records (must use positional ctor, not object initializer)
- `RestAtInnCommand(string LocationId, string CharacterName, int Cost = 10)`
- `VisitShopCommand(string LocationId, string CharacterName)`
- `RefreshMerchantInventoryCommand(string MerchantId)`
- `GetBackgroundQuery(string BackgroundId)`
- `GetBackgroundsQuery(string? FilterByAttribute = null)`

## IInventoryService Interface
Methods: `AddItemsAsync`, `AddItemAsync`, `HasInventorySpaceAsync`, `GetItemCountAsync`, `RemoveItemAsync`, `ReduceItemDurabilityAsync`
- `InMemoryInventoryService`: in-memory dict; tracks durabilities in `_durabilities` dict (default 100)
- `EfCoreInventoryService`: uses `GameDbContext.InventoryRecords` table

## Handler Behaviors
- `EncounterBossCommandHandler.GenerateBossTitle`: uses `Prefixes.Select(p => p.Value)` for title (bug was fixed — was using `string.Join(" ", boss.Prefixes)` which called ToString())
- `ExploreLocationCommandHandler`: uses `Random.Shared.Next(100)` for 60% combat / 40% peaceful; not injectable
- `HarvestNodeCommandHandler`: calls `_inventoryService.ReduceItemDurabilityAsync` after calculating `durabilityLoss`

## Testing Gotchas

### Castle.DynamicProxy / Moq Constraints
- Types used as generic type arguments in `Mock<IFoo<T>>` must be `public`. `file` or `internal` test types used as generic args will cause a Castle proxy error at runtime.
- `EnemyGenerator(IEnemyRepository, ILogger<EnemyGenerator>)` has no parameterless constructor — cannot use `Mock.Of<EnemyGenerator>()`. Construct a real instance: `new EnemyGenerator(Mock.Of<IEnemyRepository>(), NullLogger<EnemyGenerator>.Instance)`.

### MediatR ValidationBehavior
- `ValidationBehavior` runs validators via `Task.WhenAll` but passes the **same** `ValidationContext` to all of them. Failure messages from one validator appear in every other validator's result set. Testing multi-validator failure aggregation: use **one validator with multiple failing rules**, not multiple separate validators.

### Model Required Members
- `Location` has four `required` members: `Id`, `Name`, `Description`, `Type`. All four must be set in object initializers or tests will not compile (CS9035).
- `DungeonRoom` requires `Id` and `Type`.
- `DungeonInstance` requires `Id`, `LocationId`, and `Name`.

### Item Upgrade Levels
- `Item.GetMaxUpgradeLevel()`: Common=5, Uncommon=5, Rare=7, Epic=9, Legendary=10.
- Tests for upgrade levels > 5 must use `ItemRarity.Legendary` (max=10) or the test will hit an empty remaining-levels list.

### Static State in Handlers
- `EnterDungeonHandler._activeDungeons` is `private static Dictionary<string, DungeonInstance>`. Access via reflection for test setup. Use a file-scoped `ActiveDungeonScope` helper that injects and cleans up the entry in Dispose.

### File Deletion in PowerShell
- `Remove-Item` may be blocked by shell policy. Use `[System.IO.File]::Delete("absolute\path")` as a reliable alternative.

### Enum Confusion
- `RarityTier` enum is in `RealmEngine.Shared.Models`. Separate from `ItemRarity` (used on items). Don't confuse the two.

### ShopEconomyService / Shop Handler Tests
- Pass `null!` for the DbContextFactory: `new ItemDataService(null!, NullLogger<ItemDataService>.Instance)` is safe because `LoadCatalog` catches the resulting exception and returns `[]`.
- Pre-populate a merchant's inventory via `shopSvc.GetOrCreateInventory(merchant)` then mutate `inventory.CoreItems`, `inventory.DynamicItems`, or `inventory.PlayerSoldItems` directly before passing to the handler under test.
- `CalculateSellPrice` for a Common item with `Price=100` and no modifier traits returns exactly 100 (quality multiplier = 100/100 = 1.0).

### BudgetHelperService / BudgetCalculator Tests
- `new BudgetCalculator(new BudgetConfig(), NullLogger<BudgetCalculator>.Instance)` works directly — no DI required.
- Default `BudgetConfig` values: `EnemyLevelMultiplier=5.0`, `BossMultiplier=2.5`, `EliteMultiplier=1.5`. So `CalculateBaseBudget(10)` = 50.

### Services Requiring EF Core InMemory (cannot unit-test without DB)
- `ItemDataService` — calls `_dbFactory.CreateDbContext()` in `LoadCatalog()`
- `CategoryDiscoveryService` — queries 11+ EF Core DbSets in `Initialize()`
- `MaterialPoolService` — calls `_dbFactory.CreateDbContextAsync()` in every public method
- `BudgetItemGenerationService` — depends on `MaterialPoolService` (transitively EF Core)

## Known Open Items (as of March 2026)

### RealmEngine Core Abstractions
- `IGameStateService` interface extracted to `RealmEngine.Core/Abstractions/IGameStateService.cs`
- All 12 handlers/services (Exploration, Death/Respawn) depend on `IGameStateService` (not concrete `GameStateService`)
- `GameStateService` depends on `ISaveGameService` (not concrete `SaveGameService`)
- All test mocks use `Mock<IGameStateService>`; `FakeGameStateService` implements `IGameStateService` directly

### P3 Stubs (incomplete implementations)
1. **MainMenuViewModel.SettingsCommand** — empty TODO body (`RealmUnbound.Client/ViewModels/MainMenuViewModel.cs` ~line 51)
2. **CharacterSelectViewModel.AvailableClasses** — hardcoded list; should call `HttpContentService.GetClassesAsync()`
3. **GameHub Hub→MediatR bridge** — `SelectCharacter`, `EnterZone`, `LeaveZone` do not dispatch to MediatR yet

### P4 XML Doc Gaps (will cause CS1591 build errors)
- `RealmUnbound.Contracts`: all Auth, Characters, Zones, Foundry, Content DTOs missing `<summary>`
- `RealmUnbound.Server` repo interfaces: `IZoneRepository`, `IZoneSessionRepository`, `IPlayerAccountRepository`, `ICharacterRepository` — missing method summaries
- `RealmUnbound.Client` service interfaces: `IHubConnectionFactory`, `HttpContentService`, `INavigationService`, `HttpResponseHelper` — missing docs
