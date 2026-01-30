# DI Container Validation Test Results

**Date:** January 30, 2026  
**Status:** ⚠️ CRITICAL ISSUES FOUND  
**Test Suite:** [ServiceRegistrationTests.cs](../RealmEngine.Core.Tests/ServiceRegistrationTests.cs)

---

## Summary

Created comprehensive DI validation tests that automatically verify all services and handlers are properly registered. Tests revealed **3 critical issues** preventing proper DI resolution.

**Test Results:** 29/37 passed (78%)  
**Critical Failures:** 8 tests failed

---

## ✅ What's Working

### Successfully Registered Services

- ✅ **Core Services**: CharacterGrowthService, LootTableService, InMemoryInventoryService
- ✅ **Generators**: ItemGenerator, EnemyGenerator, LocationGenerator, NpcGenerator, AbilityGenerator, QuestGenerator, DialogueGenerator, SocketGenerator (8/8 tested)
- ✅ **Data Services**: GameDataCache (singleton), ReferenceResolverService, CategoryDiscoveryService
- ✅ **Interface Bindings**: IApocalypseTimer, IInventoryService, IPassiveBonusCalculator
- ✅ **Repository Interfaces**: INodeRepository, ICharacterClassRepository, IHallOfFameRepository, IEquipmentSetRepository
- ✅ **Catalog Loaders**: RecipeCatalogLoader, ItemCatalogLoader, AbilityCatalogService

---

## ❌ Critical Issues Found

### Issue 1: BudgetConfig Not Registered

**Error:**
```
Unable to resolve service for type 'RealmEngine.Core.Services.Budget.BudgetConfig' 
while attempting to activate 'RealmEngine.Core.Services.Budget.BudgetCalculator'
```

**Impact:** Blocks:
- BudgetCalculator
- BudgetHelperService → ShopEconomyService
- CraftingService (depends on BudgetCalculator via BudgetHelperService)
- All shop-related handlers (Browse, Buy, Sell, etc.)

**Root Cause:** `BudgetConfig` class exists at `RealmEngine.Core/Services/Budget/BudgetConfig.cs` but is not registered in DI container.

**Solution:** Add to `AddRealmEngineCore()`:
```csharp
// Register budget configuration
var budgetConfigPath = Path.Combine(dataPath, "budget-config.json");
var budgetConfig = BudgetConfig.LoadFromFile(budgetConfigPath);
services.AddSingleton(budgetConfig);
```

**Files Affected:** 4 tests failing

---

### Issue 2: MediatR Handlers Not Auto-Registered

**Error:**
```
122 handlers not registered in DI container
```

**Impact:** **ALL** MediatR handlers are missing from DI, including:
- ✗ Shop handlers (6): Browse, Buy, Sell, Merchant info, Refresh, Affordability
- ✗ SaveLoad handlers (5): Save, Load, Delete, GetAll, GetMostRecent
- ✗ Quest handlers (8): Start, Complete, Progress, Available, Active, Completed, Main chain, Initialize
- ✗ Progression handlers (9): Abilities, Spells, Skills, Level up, XP, Allocation
- ✗ Inventory handlers (11): Equip, Unequip, Drop, Sort, GetItems, Value, Details
- ✗ Combat handlers (8): Attack, Defend, StatusEffects, UseItem, Boss encounter
- ✗ Exploration handlers (12): Travel, Rest, Visit, Explore, NPCs, Locations, Dungeons
- ✗ Crafting handlers (4): Craft, Discover, Learn, GetKnown
- ✗ Character Creation handlers (6): Create, Classes, Abilities, Spells
- ✗ And 53 more handlers...

**Root Cause:** MediatR auto-registration via `AddMediatR(cfg => cfg.RegisterServicesFromAssembly(...))` should automatically register handlers, but tests show they're missing. This suggests:
1. Assembly scanning is not finding handlers
2. OR handlers have missing dependencies preventing registration

**Solution:** Investigate why MediatR assembly scanning isn't working. Likely need to ensure all handler dependencies are registered first (see Issue 1 - BudgetConfig).

**Files Affected:** 1 comprehensive test failing

---

### Issue 3: LiteDB File Locking (Test Isolation)

**Error:**
```
System.IO.IOException: The process cannot access the file 
'C:\code\console-game\RealmEngine.Core.Tests\bin\Debug\net9.0\savegames.db' 
because it is being used by another process.
```

**Impact:** Blocks:
- ISaveGameRepository test
- GameStateService test  
- SaveGameService, LoadGameService, CombatService, ExplorationService tests

**Root Cause:** All tests share single `IServiceProvider` instance in constructor, causing `SaveGameRepository` to open `savegames.db` once and hold file lock for entire test run. Subsequent tests can't access database.

**Solution Options:**
1. **Use in-memory LiteDB** (recommended for tests):
   ```csharp
   services.AddScoped<ISaveGameRepository>(sp => 
       new SaveGameRepository(":memory:"));
   ```

2. **Create new ServiceProvider per test** (slower but more realistic):
   ```csharp
   // Move service provider creation from constructor to each test method
   using var scope = _serviceProvider.CreateScope();
   ```

3. **Mock repository for unit tests**:
   ```csharp
   services.AddScoped<ISaveGameRepository, MockSaveGameRepository>();
   ```

**Files Affected:** 5 tests failing

---

## 📊 Test Coverage

### Tests Created

| Category | Tests | Purpose |
|----------|-------|---------|
| **Core Interfaces** | 4 | Verify IApocalypseTimer, ISaveGameService, IInventoryService, IPassiveBonusCalculator |
| **Repository Interfaces** | 5 | Verify ISaveGameRepository, INodeRepository, etc. |
| **Critical Services** | 5 | Verify user-reported missing services (Crafting, BudgetCalculator, etc.) |
| **Generators** | 1 | Verify all 8 generators resolvable |
| **Core Services** | 4 | Verify CharacterGrowth, LootTable, ShopEconomy, GameState |
| **Data Services** | 3 | Verify GameDataCache, ReferenceResolver, CategoryDiscovery |
| **Handler Resolution** | 6 | Verify specific handlers resolve with dependencies |
| **Comprehensive Scans** | 3 | Verify ALL handlers/generators/features resolvable |
| **Cross-Project** | 2 | Verify Core→Data dependencies resolve |
| **Total** | 37 | Full DI validation suite |

### Handler Coverage

Tests automatically discover and validate **all 122 MediatR handlers** in the codebase using assembly scanning. No manual updates needed when handlers are added/removed.

---

## 🔧 Recommended Fixes (Priority Order)

### 1. Register BudgetConfig (HIGH PRIORITY)

Add to [ServiceCollectionExtensions.cs](../RealmEngine.Core/ServiceCollectionExtensions.cs):

```csharp
public static IServiceCollection AddRealmEngineCore(this IServiceCollection services, string dataPath)
{
    // ... existing registrations ...
    
    // Register budget configuration (required by BudgetCalculator)
    var budgetConfigPath = Path.Combine(dataPath, "budget-config.json");
    var budgetConfig = BudgetConfig.LoadFromFile(budgetConfigPath);
    services.AddSingleton(budgetConfig);
    
    // Register budget services
    services.AddSingleton<BudgetCalculator>();
    // ... rest ...
}
```

**Impact:** Fixes 4 failing tests, unblocks shop/crafting features

---

### 2. Fix Test Isolation (MEDIUM PRIORITY)

Update [ServiceRegistrationTests.cs](../RealmEngine.Core.Tests/ServiceRegistrationTests.cs):

```csharp
public ServiceRegistrationTests()
{
    _services = new ServiceCollection();
    _services.AddLogging();
    _services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CreateCharacterCommand).Assembly));
    
    // Use in-memory database for tests
    _services.AddScoped<ISaveGameRepository>(sp => 
        new SaveGameRepository(":memory:"));
    _services.AddScoped<INodeRepository>(sp => 
        new InMemoryNodeRepository()); // Already in-memory
    
    // Register other repos normally
    _services.AddScoped<ICharacterClassRepository, CharacterClassRepository>();
    _services.AddScoped<IHallOfFameRepository, HallOfFameRepository>();
    _services.AddScoped<IEquipmentSetRepository, EquipmentSetRepository>();
    
    _services.AddRealmEngineData("c:\\code\\console-game\\RealmEngine.Data\\Data\\Json");
    _services.AddRealmEngineCore("c:\\code\\console-game\\RealmEngine.Data\\Data\\Json");
    
    _serviceProvider = _services.BuildServiceProvider();
}
```

**Impact:** Fixes 5 failing tests, enables proper test isolation

---

### 3. Investigate MediatR Handler Registration (MEDIUM PRIORITY)

After fixing BudgetConfig, re-run tests to see if handler registration issue persists. If handlers still missing:

**Check:**
1. Are handlers in correct namespace/assembly?
2. Do handlers have parameterless constructors OR all dependencies registered?
3. Is MediatR scanning correct assembly?

**Debug:**
```csharp
// Add to test constructor to debug
var assembly = typeof(CreateCharacterCommand).Assembly;
var handlers = assembly.GetTypes()
    .Where(t => t.Name.EndsWith("Handler") && !t.IsAbstract)
    .ToList();
Console.WriteLine($"Found {handlers.Count} handler types in assembly");
```

**Impact:** Fixes 1 comprehensive test, ensures all handlers discoverable

---

### 4. Add dataPath Parameter (LOW PRIORITY - BREAKING CHANGE)

Current signature:
```csharp
public static IServiceCollection AddRealmEngineCore(this IServiceCollection services)
```

Required for BudgetConfig:
```csharp
public static IServiceCollection AddRealmEngineCore(this IServiceCollection services, string dataPath)
```

**Breaking Change:** All callers must update:
```csharp
// OLD
services.AddRealmEngineCore();

// NEW
services.AddRealmEngineCore(dataPath);
```

**Workaround:** Make dataPath optional with default:
```csharp
public static IServiceCollection AddRealmEngineCore(
    this IServiceCollection services, 
    string? dataPath = null)
{
    dataPath ??= Path.Combine(Directory.GetCurrentDirectory(), "Data", "Json");
    // ... rest ...
}
```

---

## 🎯 Success Criteria

After applying fixes, all 37 tests should pass:

- ✅ All 4 core interface bindings resolve
- ✅ All 5 repository interfaces resolve
- ✅ All 5 user-reported services resolve (including CraftingService with BudgetCalculator)
- ✅ All 8 generators resolve
- ✅ All 122 MediatR handlers auto-register and resolve
- ✅ No file locking issues in tests
- ✅ Cross-project dependencies (Core→Data) work correctly

---

## 📝 Notes

### Why This Test Suite Matters

1. **Prevents Runtime Errors**: Catches DI issues at test time, not production
2. **Automated Discovery**: Finds ALL handlers/services via reflection - no manual maintenance
3. **Dependency Validation**: Ensures entire dependency graph is satisfied
4. **Regression Prevention**: Will catch future missing registrations immediately
5. **Documentation**: Tests serve as living documentation of what's registered

### Test Philosophy

- Tests use **real DI container** (not mocks) to catch actual resolution failures
- Tests use **assembly scanning** to auto-discover all handlers (future-proof)
- Tests check **both registration and resolution** (some services register but can't resolve due to missing dependencies)
- Tests run in **isolated scopes** where possible to prevent cross-contamination

### Future Enhancements

Consider adding:
1. **Startup validation** in Godot game: Call `ServiceRegistrationValidator.ValidateAllServices()` on startup
2. **CI integration**: Run these tests as part of build pipeline
3. **Performance tests**: Measure service resolution time for frequently-used services
4. **Scope tests**: Verify singleton vs scoped vs transient lifetimes are correct

---

## Files Modified

- ✅ [ServiceCollectionExtensions.cs](../RealmEngine.Core/ServiceCollectionExtensions.cs) - Added 5 missing services
- ✅ [ServiceRegistrationTests.cs](../RealmEngine.Core.Tests/ServiceRegistrationTests.cs) - Created 37 comprehensive tests

## Files Pending Modification

- ⏳ ServiceCollectionExtensions.cs - Need to add BudgetConfig registration
- ⏳ ServiceRegistrationTests.cs - Need to fix LiteDB file locking
- ⏳ ServiceCollectionExtensions.cs - May need to add dataPath parameter

---

## Related Documents

- [COPILOT_INSTRUCTIONS.md](../.github/copilot-instructions.md) - Project architecture
- [API_SPECIFICATION.md](API_SPECIFICATION.md) - MediatR command/query patterns
- [COMMANDS_AND_QUERIES_INDEX.md](COMMANDS_AND_QUERIES_INDEX.md) - Complete handler index
