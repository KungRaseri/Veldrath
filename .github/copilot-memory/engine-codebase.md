# RealmEngine Codebase Notes

## Test Counts (as of 2026-03-20, session-19)
- RealmEngine.Core.Tests: **1,738 passing**
- RealmEngine.Shared.Tests: **778 passing**
- RealmEngine.Data.Tests: **203 passing**
- RealmUnbound.Client.Tests: **288 passing** (+7 from session-9)
- RealmUnbound.Server.Tests: **362 passing** (+12 from session-19: ContentEquipmentEndpointTests — weapons, armors, materials)

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

### Enemy / NPC Dual-Archetype Behavior
- `EfCoreEnemyRepository` and `EfCoreNpcRepository` **both** query `db.ActorArchetypes` without any hostility/role filter. A single seeded `ActorArchetype` row therefore appears in both `/api/content/enemies` and `/api/content/npcs` responses. This is by design — hostility is a trait flag, not a filter applied by the repos.

### SpellDto.School Is a Mapped Tradition, Not the Raw Entity School
- `EfCoreSpellRepository.ParseTradition(school)` maps entity granular schools to 4 broad traditions. `"fire"` → `MagicalTradition.Primal` → `SpellDto.School = "Primal"`. Other mappings: `"arcane"/"force"/"transmutation"` → `"Arcane"`, `"divine"/"holy"/"sacred"/"light"/"healing"` → `"Divine"`, `"shadow"/"occult"/"psychic"/"dark"/"void"` → `"Occult"`.

### QuestDto — Partially-Unmapped Fields
- `EfCoreQuestRepository.MapToModel` does **not** set `QuestType` or `Difficulty` on the shared `Quest` model. Those fields default to empty string. Only `Slug`, `Title` (= DisplayName), `DisplayName`, and `RarityWeight` are populated from the entity.

### WeaponDto / ArmorDto / MaterialDto (session-19)
- `WeaponDto(Slug, DisplayName, TypeKey, WeaponType, RarityWeight)` added to `RealmUnbound.Contracts/Content/ContentContracts.cs`
- `ArmorDto(Slug, DisplayName, TypeKey, ArmorType, RarityWeight)` added to same file — `ArmorType` is `Item.ArmorClass` from the shared model (mapped from `Armor.ArmorType` entity field)
- `MaterialDto(Slug, DisplayName, MaterialFamily, RarityWeight)` added to same file — `RarityWeight` is cast from `MaterialEntry.RarityWeight` (float) to int
- `GET /api/content/weapons`, `/api/content/weapons/{slug}` — backed by `IWeaponRepository`
- `GET /api/content/armors`, `/api/content/armors/{slug}` — backed by `IArmorRepository`
- `GET /api/content/materials`, `/api/content/materials/{slug}` — backed by `IMaterialRepository`
- All 6 routes are anonymous (no auth required)
- `ContentEquipmentEndpointTests.cs` — 12 integration tests in `RealmUnbound.Server.Tests/Features/`

### DatabaseSeeder — Items & Enchantments (session-18)
- `SeedItemsAsync` seeds 17 items (consumable×4, crystal×2, gem×3, rune×3, essence×3, orb×2) using `I()`, `ISt()`, `ITr()` factory helpers.
- `SeedEnchantmentsAsync` seeds 9 enchantments (weapon×4, armor×3, any×2) using `EE()`, `ESt()`, `ETr()` factory helpers.
- `SeedContentRegistryAsync` also registers Items (`items/general`) and Enchantments (`items/enchantments`).

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

### P3 Stubs — see [unbound-memory.md](unbound-memory.md) for full status

### RealmUnbound — Hub Architecture, Blob Schema, P3/P4 Status

> Full details in [unbound-memory.md](unbound-memory.md). Summary:

- Server hub commands follow the **blob pattern**: read `Character.Attributes` JSON, mutate, save. Never call Core handlers (type mismatch).
- Wired bridges: `GainExperience`, `RestAtLocation`, `AllocateAttributePoints`, `UseAbility`
- Last P3 stub open: `CharacterSelectViewModel.ServerUrl` hardcoded to `"http://localhost:8080"`
- P4 XML docs: `IPlayerAccountRepository` still missing; `IZoneRepository`/`IZoneSessionRepository` fixed session-4

### ActorClassDto Changed (2026-03-19)
- `ActorClassDto` in `RealmUnbound.Contracts` was updated to add `HitDie` (int), `PrimaryStat` (string), and `RarityWeight` (int) parameters
- This was a breaking change that broke `FakeServices.cs` and `CharacterSelectViewModelTests.cs` in Client.Tests
- Fix: update all `new ActorClassDto(slug, name, typeKey)` calls to `new ActorClassDto(slug, name, typeKey, hitDie, primaryStat, rarityWeight)`

### Session-17 Integration Test Notes
- `ContentTypedEndpointsFixture` seeds ActorClass, Species, Background, Skill, 2×Enchantment (different TargetSlot), Ability, Item into `ContentDbContext` — all routes under test resolve through the SQLite in-memory DB
- `Enchantment` in the fixture = `RealmEngine.Data.Entities.Enchantment` (not Shared.Models) — resolved unambiguously because the test file does NOT import `RealmEngine.Shared.Models`
- `EfCoreAbilityRepository.MapToModel` sets `Type = ParseAbilityType(entity.AbilityType, ...)` — seeding with `AbilityType = "active"` produces `AbilityTypeEnum.Offensive`; the endpoint DTO includes this as a string; tests do NOT assert the AbilityType string value
- `Item.TotalRarityWeight` is NOT set by `EfCoreItemRepository.MapToModel` (maps Slug/Name/TypeKey/Weight/Price/StackSize/IsStackable only) — `ItemDto.RarityWeight` will be 0 for all seeded items in tests; avoid asserting RarityWeight in item DTO tests
- `InMemoryHallOfFameRepository` already has a dedicated test file: `RealmEngine.Data.Tests/Repositories/InMemoryHallOfFameRepositoryTests.cs` — do NOT add it to `InMemoryStubRepositoryTests.cs`
- `InMemoryEquipmentSetRepository` is NOT a stub — it returns 5 hardcoded sets (Dragonborn, Shadow Assassin, Arcane Scholar, Iron Guardian, Stormcaller). Test against non-empty count.
- `ContentEndpoints.cs` intentionally uses `ContentDbContext` directly for `/classes`, `/species`, `/backgrounds`, `/skills` — those handlers map entity-specific fields (HitDie, PrimaryStat, GoverningAttribute, MaxRank) not present on shared models. This is by design, not a bug.
- `EfCoreMaterialRepository` and `EfCoreArmorRepository` / `EfCoreWeaponRepository` take only `ContentDbContext` (no logger param) — do NOT pass `NullLogger<T>.Instance`
- `HallOfFameEntry` is in `RealmEngine.Shared.Models`, NOT `RealmEngine.Data.Entities` — when using it in Data.Tests (which imports both namespaces), use alias: `using HallOfFameEntry = RealmEngine.Shared.Models.HallOfFameEntry;`
- `Enchantment` and `Item` exist in BOTH `RealmEngine.Data.Entities` and `RealmEngine.Shared.Models` — always qualify or alias when both namespaces are imported
- `ContentEndpoints.cs` enchantment endpoint uses `ContentDbContext` directly (entity has `TypeKey`); item endpoint uses `IItemRepository` (shared model has `TypeKey` mapped)
- `Item.TypeKey` on the shared model can be null — use `?? string.Empty` when mapping to `ItemDto.TypeKey`
- `GetSpeciesQuery` handler namespace is `RealmEngine.Core.Features.Species.Queries` (feature folder named `Species`)
- `EfCoreHallOfFameRepository` methods are synchronous (no async) — tests use `using var db` not `await using var db`

### AvailableClasses Test Gotcha
- `CharacterSelectViewModel.LoadAsync` fires-and-forgets on construction; `FakeContentService.GetClassesAsync()` completes synchronously (Task.FromResult)
- So `LoadAsync` replaces `AvailableClasses` before the test's first assertion if a non-empty `FakeContentService` is used
- Test for fallback list: pass `new FakeContentService { Classes = [] }` + `await Task.Delay(50)` to let fire-and-forget complete
- Test for catalog-populated list: pass classes with `Task.Delay(50)` wait

### ISpeciesRepository (added session-13)
- `ISpeciesRepository` interface in `RealmEngine.Shared/Abstractions/`
- `EfCoreSpeciesRepository` in `RealmEngine.Data/Repositories/`
- `RealmEngine.Shared/Models/Species.cs` shared model (Slug, DisplayName, TypeKey, RarityWeight)
- Registered in `RealmUnbound.Server/Program.cs` as `AddScoped<ISpeciesRepository, EfCoreSpeciesRepository>()`
- DTO: `SpeciesDto` in `RealmUnbound.Contracts` — browse-list projection only (no stats/traits)

### EfCoreBackgroundRepository Legacy Cleanup (session-13)
- Removed dead-code `:` split in `GetBackgroundByIdAsync` — now queries directly by bare slug
- Removed `GetBackgroundByIdAsync_StripsPrefixedSlug` test (was testing dead code)

### CharacterContracts.cs XML Docs (session-13)
- Added full XML `<summary>` + `<param>` docs to `CreateCharacterRequest` and `CharacterDto`
- `ClassName` param doc states: stores the class display name (e.g. "Warrior", "Mage")

### EfCoreEquipmentSetRepository Tests (session-13)
- All 3 public methods tested (synchronous — no async): `GetAll`, `GetById`, `GetByName`
- No logger constructor arg — just `new EfCoreEquipmentSetRepository(db)`

### EfCoreCharacterClassRepository Notes
- `MapToModel` hardcodes `IsSubclass = false` and `ParentClassId = null` for all rows
- `GetClassesByType(classType)` filters by `Id.StartsWith("{classType}:")` — Id format = `"{typeKey}:{displayName}"`
- Uses sync-over-async pattern with `SemaphoreSlim` cache — test each method on a fresh `ContentDbContext` (separate `CreateDbContext()` call) to avoid cache bleed between tests
- Implements `IDisposable` — call `repo.Dispose()` in tests if creating multiple repos on same db

### EfCoreNamePatternRepository Location
- Lives in `RealmEngine.Core/Repositories/` (NOT in `RealmEngine.Data/Repositories/`)
- Tests in `RealmEngine.Core.Tests/Repositories/EfCoreNamePatternRepositoryTests.cs`

### IItemRepository (added session-14)
- Interface in `RealmEngine.Shared/Abstractions/`, impl `EfCoreItemRepository` in `RealmEngine.Data/Repositories/`
- Uses `ContentDbContext.Items` — entity `Item` has `ItemType` (not TypeKey) for category filtering
- Methods: `GetAllAsync`, `GetBySlugAsync`, `GetByTypeAsync(itemType)` — itemType is lowercased before query
- MapToModel maps: Slug, Name, TypeKey, Weight (float→double), Price (Value), StackSize, IsStackable

### IEnchantmentRepository (added session-14)
- Interface in `RealmEngine.Shared/Abstractions/`, impl `EfCoreEnchantmentRepository` in `RealmEngine.Data/Repositories/`
- Uses `ContentDbContext.Enchantments` — entity `Enchantment` has `TargetSlot` column
- Methods: `GetAllAsync`, `GetBySlugAsync`, `GetByTargetSlotAsync(targetSlot)` — lowercased before query

### INodeRepository / EfCoreNodeRepository (added session-14)
- Interface `INodeRepository` already existed in `RealmEngine.Shared/Abstractions/` (8 methods, all mutating + querying)
- EF entity `HarvestableNodeRecord` in `RealmEngine.Data/Entities/HarvestableNodeRecord.cs` — PK = `NodeId` (string)
- `EfCoreNodeRepository` uses **`GameDbContext`** (not ContentDbContext) — nodes are runtime game state, not catalog content
- Migration `AddHarvestableNodes` in `RealmEngine.Data/Migrations/GameDb/` (generated with dotnet ef)
- `GetNearbyNodesAsync` ignores `radius` (same as InMemory version) — real spatial queries would need PostGIS
- `SaveNodeAsync` is an upsert: insert if NodeId not present, update mutable fields if found
- Tests are in `RealmEngine.Data.Tests/Repositories/EfCoreGameRepositoryTests.cs`

### ActorClassDto XML Docs (session-14)
- Added full `<param>` docs for all 6 positional parameters of `ActorClassDto`
- TypeKey doc: "DB category key for this class family (e.g. \"warriors\", \"casters\"). Not a content-reference prefix."
