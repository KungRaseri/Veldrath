# RealmEngine TODO & API Coverage Report

**Date:** January 26, 2026  
**Generated:** Automated Analysis

---

## TODO Comments in Codebase

### 🔴 High Priority (User-Facing Features)

#### 1. GetStartingLocationsHandler - Location Loading
**File:** `RealmEngine.Core/Features/Exploration/Queries/GetStartingLocationsHandler.cs:33`  
**Code:** `// TODO: Implement location loading from catalog`  
**Priority:** HIGH  
**Status:** Deferred to Phase 2  
**Impact:** Character creation cannot select starting location  

**Current Behavior:**  
Returns hardcoded example locations instead of loading from `locations/catalog.json`

**Required Implementation:**
```csharp
// Load from GameDataCache
var catalogFile = _dataCache.GetFile("locations/catalog.json");
var catalog = JObject.Parse(catalogFile.JsonData.ToString());
var locationsArray = catalog["locations"] as JArray;

// Parse each location
foreach (var locationToken in locationsArray)
{
    var location = new Location
    {
        Id = locationToken["slug"]?.ToString(),
        Name = locationToken["name"]?.ToString(),
        Description = locationToken["description"]?.ToString(),
        // Map additional properties
    };
    locations.Add(location);
}
```

**Estimated Effort:** 2 hours  
**Blockers:** None (GameDataCache ready, JSON structure known)

---

#### 2. LevelUpHandler - Ability Unlocking
**File:** `RealmEngine.Core/Features/LevelUp/Commands/LevelUpHandler.cs:124`  
**Code:** `var unlockedAbilities = new List<string>(); // TODO: Query class abilities unlocked at this level`  
**Priority:** MEDIUM  
**Status:** Not started  
**Impact:** Players don't get new abilities when leveling up  

**Current Behavior:**  
LevelUp succeeds but returns empty `UnlockedAbilities` list

**Required Implementation:**
```csharp
// Query class catalog for abilities at this level
var characterClass = _classRepository.GetById(player.ClassId);
var classData = _dataCache.GetFile($"classes/{characterClass.Category}/catalog.json");
var catalog = JObject.Parse(classData.JsonData.ToString());

// Find abilities unlocked at newLevel
var unlockedAbilities = catalog["abilities"]
    ?.Where(a => a["requiredLevel"]?.Value<int>() == newLevel)
    ?.Select(a => a["name"]?.ToString())
    ?.ToList() ?? new List<string>();
```

**Estimated Effort:** 3 hours  
**Blockers:** Need classes catalog structure documentation

---

#### 3. PreviewLevelUpHandler - Ability Preview
**File:** `RealmEngine.Core/Features/LevelUp/Queries/PreviewLevelUpHandler.cs:94`  
**Code:** `var unlockedAbilities = new List<string>(); // TODO: Query class catalog for abilities unlocked at nextLevel`  
**Priority:** MEDIUM  
**Status:** Not started  
**Impact:** Players can't see what abilities they'll unlock before leveling  

**Current Behavior:**  
Preview shows stat increases but empty abilities list

**Required Implementation:**  
Same as LevelUpHandler (see above), but query for `nextLevel` instead of `newLevel`

**Estimated Effort:** 1 hour (reuse LevelUpHandler logic)  
**Blockers:** None (depends on #2 above)

---

### 🟡 Medium Priority (Internal/Technical Debt)

#### 4. LevelUpService - Move Data to JSON
**File:** `RealmEngine.Core/Services/LevelUpService.cs:170`  
**Code:** `/// TODO: Move to JSON data files and load via IDataService`  
**Priority:** LOW  
**Status:** Not started  
**Impact:** Hardcoded XP tables make balance changes difficult  

**Current Behavior:**  
XP requirements and stat gains are hardcoded in `LevelUpService.cs`

**Required Implementation:**
1. Create `progression/experience-tables.json`
2. Create `progression/stat-gain-tables.json`
3. Update LevelUpService to load from GameDataCache
4. Remove hardcoded values

**Estimated Effort:** 4 hours  
**Blockers:** Need to define JSON structure for progression tables

---

### 🟢 Low Priority (Test/Documentation)

#### 5. ItemGeneratorTests - Material Traits
**File:** `RealmEngine.Core.Tests/Generators/ItemGeneratorTests.cs:227`  
**Code:** `// Note: Material.Traits population is a TODO in budget generation`  
**Priority:** LOW  
**Status:** Known limitation  
**Impact:** Generated items don't have material-specific traits  

**Current Behavior:**  
Test acknowledges that Material.Traits are not populated during budget-based generation

**Resolution:**  
Not a bug - trait population is handled in a separate enhancement phase

**Action:** Document this as expected behavior

---

## Summary Statistics

| Priority | Count | Status |
|----------|-------|--------|
| HIGH | 1 | Deferred (Phase 2) |
| MEDIUM | 2 | Not Started |
| LOW | 2 | Backlog |
| **TOTAL** | **5** | |

---

## API Coverage Analysis

### Character Creation Domain

#### Commands
- ✅ `CreateCharacterCommand` - Create new character
- ✅ `InitializeStartingAbilitiesCommand` - Set starting abilities
- ✅ `InitializeStartingSpellsCommand` - Set starting spells

#### Queries
- ✅ `GetClassesQuery` - List available classes
- ✅ `GetBackgroundsQuery` - List character backgrounds (Phase 1)
- ✅ `GetEquipmentForClassQuery` - Get equipment by proficiency (Phase 3)
- ⚠️ `GetStartingLocationsQuery` - List starting locations (Phase 2 - TODO #1)

**Coverage:** 85% (6/7 implemented)

---

### Combat Domain

#### Commands
- ✅ `ProcessStatusEffectsCommand` - Apply DoT/buffs/debuffs
- ✅ `PartyCombatTurnCommand` - Execute party member actions

#### Queries
- ❌ `GetCombatStateQuery` - Get current battle state
- ❌ `GetAvailableActionsQuery` - List valid actions for turn

**Coverage:** 50% (2/4 implemented)

**Missing APIs:**
```csharp
// Needed for Godot combat UI
public record GetCombatStateQuery : IRequest<CombatStateResult>;
public record GetAvailableActionsQuery(string CharacterId) : IRequest<List<CombatAction>>;
```

---

### Inventory Domain

#### Commands
- ✅ `EquipItemCommand` - Equip weapon/armor
- ❌ `UnequipItemCommand` - Remove equipped item
- ❌ `AddItemToInventoryCommand` - Add item to bag
- ❌ `RemoveItemFromInventoryCommand` - Remove/drop item
- ❌ `TransferItemCommand` - Move item between characters
- ❌ `SortInventoryCommand` - Organize inventory

#### Queries
- ❌ `GetInventoryQuery` - Get character inventory
- ❌ `GetEquippedItemsQuery` - Get equipped gear

**Coverage:** 12% (1/8 implemented)

**Critical Missing APIs:**
```csharp
public record GetInventoryQuery(string CharacterName) : IRequest<InventoryResult>;
public record AddItemCommand(string CharacterName, Item Item) : IRequest<AddItemResult>;
public record RemoveItemCommand(string CharacterName, string ItemId) : IRequest<RemoveItemResult>;
```

---

### Item Enhancement Domain

#### Enchanting
- ✅ `ApplyEnchantmentCommand` - Add enchantment
- ✅ `RemoveEnchantmentCommand` - Remove enchantment
- ✅ `AddEnchantmentSlotCommand` - Add enchantment slot

#### Socketing
- ✅ `SocketItemCommand` - Socket single gem
- ✅ `SocketMultipleItemsCommand` - Socket multiple gems
- ✅ `RemoveSocketedItemCommand` - Remove socketed gem

#### Upgrading
- ✅ `UpgradeItemCommand` - Upgrade item quality/level

#### Salvaging
- ✅ `SalvageItemCommand` - Break down item for materials

#### Crafting
- ✅ `LearnRecipeCommand` - Learn crafting recipe
- ✅ `DiscoverRecipeCommand` - Auto-learn recipe

**Coverage:** 100% (10/10 implemented) ✨

---

### Exploration Domain

#### Queries
- ⚠️ `GetStartingLocationsQuery` - Get spawn points (Phase 2 - TODO #1)
- ❌ `GetLocationDetailsQuery` - Get location info
- ❌ `GetNearbyLocationsQuery` - Get connected areas
- ❌ `GetLocationSpawnInfoQuery` - Get enemy/loot spawn data

#### Commands
- ❌ `TravelToLocationCommand` - Move to new area
- ❌ `ExploreLocationCommand` - Discover location features

**Coverage:** 0% (0/6 implemented)

**Critical Missing APIs:**
```csharp
public record GetLocationDetailsQuery(string LocationId) : IRequest<LocationResult>;
public record TravelToLocationCommand(string CharacterName, string LocationId) : IRequest<TravelResult>;
```

---

### Quest Domain

#### Commands
- ✅ `StartQuestCommand` - Accept quest
- ✅ `UpdateQuestProgressCommand` - Update objective progress
- ✅ `CompleteQuestCommand` - Turn in completed quest
- ✅ `InitializeStartingQuestsCommand` - Set starting quests

#### Queries
- ❌ `GetAvailableQuestsQuery` - List available quests
- ❌ `GetActiveQuestsQuery` - List in-progress quests
- ❌ `GetQuestDetailsQuery` - Get quest objectives/rewards

**Coverage:** 57% (4/7 implemented)

**Missing APIs:**
```csharp
public record GetAvailableQuestsQuery(string CharacterName, string LocationId) : IRequest<List<Quest>>;
public record GetActiveQuestsQuery(string CharacterName) : IRequest<List<Quest>>;
```

---

### Shop/Trading Domain

#### Commands
- ❌ `BuyItemCommand` - Purchase from merchant
- ❌ `SellItemCommand` - Sell to merchant
- ❌ `RepairItemCommand` - Pay for repairs

#### Queries
- ❌ `GetShopInventoryQuery` - List merchant goods
- ❌ `GetSellPriceQuery` - Get item sell value
- ❌ `GetRepairCostQuery` - Calculate repair cost

**Coverage:** 0% (0/6 implemented) ❌

**Critical Missing APIs:**
```csharp
public record BuyItemCommand(string CharacterName, string MerchantId, string ItemId) : IRequest<BuyResult>;
public record SellItemCommand(string CharacterName, string MerchantId, string ItemId) : IRequest<SellResult>;
public record GetShopInventoryQuery(string MerchantId) : IRequest<List<Item>>;
```

---

### Level/Progression Domain

#### Commands
- ✅ `GainExperienceCommand` - Award XP
- ⚠️ `LevelUpCommand` - Level up character (TODO #2: missing ability unlocks)
- ✅ `AllocateAttributePointsCommand` - Spend stat points

#### Queries
- ⚠️ `PreviewLevelUpQuery` - Preview next level (TODO #3: missing ability preview)

**Coverage:** 75% (3/4 implemented, 2 with TODOs)

---

### Party Management Domain

#### Commands
- ✅ `RecruitNPCCommand` - Add party member
- ✅ `DismissPartyMemberCommand` - Remove party member

#### Queries
- ❌ `GetPartyMembersQuery` - List current party
- ❌ `GetRecruitableNPCsQuery` - List available companions

**Coverage:** 50% (2/4 implemented)

**Missing APIs:**
```csharp
public record GetPartyMembersQuery(string CharacterName) : IRequest<List<PartyMember>>;
public record GetRecruitableNPCsQuery(string LocationId) : IRequest<List<NPC>>;
```

---

### Save/Load Domain

#### Commands
- ❌ `SaveGameCommand` - Save current game state
- ❌ `LoadGameCommand` - Load saved game
- ❌ `DeleteSaveCommand` - Delete save file

#### Queries
- ❌ `GetSaveGamesQuery` - List all saves
- ❌ `GetSaveInfoQuery` - Get save metadata

**Coverage:** 0% (0/5 implemented) ❌

**Critical Missing APIs:**
```csharp
public record SaveGameCommand(string CharacterName, string SaveName) : IRequest<SaveResult>;
public record LoadGameCommand(string SaveId) : IRequest<Character>;
public record GetSaveGamesQuery() : IRequest<List<SaveInfo>>;
```

---

### Reputation Domain

#### Commands
- ✅ `GainReputationCommand` - Increase faction standing
- ✅ `LoseReputationCommand` - Decrease faction standing

#### Queries
- ❌ `GetReputationStatusQuery` - Get all faction standings

**Coverage:** 67% (2/3 implemented)

---

### Achievement Domain

#### Commands
- ✅ `UnlockAchievementCommand` - Grant achievement
- ✅ `CheckAchievementProgressCommand` - Evaluate triggers

#### Queries
- ❌ `GetAchievementsQuery` - List all achievements
- ❌ `GetUnlockedAchievementsQuery` - List earned achievements

**Coverage:** 50% (2/4 implemented)

---

### Item Generation Domain

#### Commands
- ✅ `GenerateRandomItemsCommand` - Generate loot
- ✅ `GenerateItemsByCategoryCommand` - Generate specific types
- ✅ `GenerateAbilityCommand` - Generate ability

**Coverage:** 100% (3/3 implemented) ✨

---

### Victory/New Game+ Domain

#### Commands
- ✅ `TriggerVictoryCommand` - End game
- ✅ `StartNewGamePlusCommand` - Start NG+

**Coverage:** 100% (2/2 implemented) ✨

---

## Overall API Coverage

| Domain | Implemented | Total | Coverage | Status |
|--------|------------|-------|----------|--------|
| Character Creation | 6 | 7 | 86% | ⚠️ Good |
| Combat | 2 | 4 | 50% | 🔴 Needs Work |
| Inventory | 1 | 8 | 13% | 🔴 Critical Gap |
| Item Enhancement | 10 | 10 | 100% | ✅ Complete |
| Exploration | 0 | 6 | 0% | 🔴 Not Started |
| Quests | 4 | 7 | 57% | ⚠️ Needs Work |
| Shop/Trading | 0 | 6 | 0% | 🔴 Critical Gap |
| Level/Progression | 3 | 4 | 75% | ⚠️ Good |
| Party Management | 2 | 4 | 50% | ⚠️ Needs Work |
| Save/Load | 0 | 5 | 0% | 🔴 Critical Gap |
| Reputation | 2 | 3 | 67% | ⚠️ Good |
| Achievement | 2 | 4 | 50% | ⚠️ Needs Work |
| Item Generation | 3 | 3 | 100% | ✅ Complete |
| Victory/NG+ | 2 | 2 | 100% | ✅ Complete |
| **TOTAL** | **37** | **73** | **51%** | ⚠️ **Half Complete** |

---

## Critical Missing APIs for Game Functionality

### 🔴 Must-Have (Blocks Basic Gameplay)

1. **Inventory Management** (Priority: CRITICAL)
   - GetInventoryQuery
   - AddItemToInventoryCommand
   - RemoveItemFromInventoryCommand

2. **Save/Load System** (Priority: CRITICAL)
   - SaveGameCommand
   - LoadGameCommand
   - GetSaveGamesQuery

3. **Shop System** (Priority: HIGH)
   - BuyItemCommand
   - SellItemCommand
   - GetShopInventoryQuery

4. **Combat Queries** (Priority: HIGH)
   - GetCombatStateQuery
   - GetAvailableActionsQuery

5. **Exploration** (Priority: HIGH)
   - GetLocationDetailsQuery
   - TravelToLocationCommand

### 🟡 Should-Have (Enhances Experience)

6. **Quest Queries** (Priority: MEDIUM)
   - GetAvailableQuestsQuery
   - GetActiveQuestsQuery

7. **Party Queries** (Priority: MEDIUM)
   - GetPartyMembersQuery
   - GetRecruitableNPCsQuery

8. **Starting Locations** (Priority: MEDIUM)
   - Complete GetStartingLocationsHandler (TODO #1)

---

## Recommendations

### Immediate Actions (Next Sprint)

1. **Fix GetStartingLocationsHandler** (TODO #1)
   - Unblocks Phase 2 character creation
   - Estimated: 2 hours

2. **Implement Inventory CQRS APIs**
   - GetInventoryQuery
   - AddItemToInventoryCommand
   - RemoveItemFromInventoryCommand
   - Estimated: 8 hours

3. **Implement Save/Load CQRS APIs**
   - SaveGameCommand (with LiteDB)
   - LoadGameCommand
   - GetSaveGamesQuery
   - Estimated: 12 hours

### Short-Term Goals (Next 2 Sprints)

4. **Complete Shop System**
   - BuyItemCommand
   - SellItemCommand
   - GetShopInventoryQuery
   - Estimated: 10 hours

5. **Add Combat State Queries**
   - GetCombatStateQuery
   - GetAvailableActionsQuery
   - Estimated: 6 hours

6. **Complete Exploration APIs**
   - GetLocationDetailsQuery
   - TravelToLocationCommand
   - Estimated: 8 hours

### Long-Term Improvements

7. **Complete Ability Unlocking** (TODO #2, #3)
   - Update LevelUpHandler
   - Update PreviewLevelUpHandler
   - Estimated: 4 hours

8. **Move Progression Data to JSON** (TODO #4)
   - Create XP/stat JSON files
   - Update LevelUpService
   - Estimated: 4 hours

9. **Add Missing Quest/Party/Achievement Queries**
   - Estimated: 12 hours total

---

## Conclusion

The RealmEngine has **strong foundation** in item enhancement and generation systems (100% complete), but has **critical gaps** in core gameplay systems:

- ❌ **0% Coverage**: Inventory, Shop, Save/Load, Exploration
- ⚠️ **50-75% Coverage**: Combat, Quests, Party, Level/Progression
- ✅ **100% Coverage**: Item Enhancement, Item Generation, Victory/NG+

**Overall Status:** 51% API coverage - **functionally incomplete** for full game experience.

**Priority Focus:** Implement Inventory, Save/Load, and Shop systems before adding more character creation features. These are blocking Godot UI development.
