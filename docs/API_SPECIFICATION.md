# RealmEngine Backend - API Specification v0.1

**Target Framework:** .NET 9.0  
**Architecture:** CQRS (Command Query Responsibility Segregation) using MediatR  
**Purpose:** Game logic backend for Godot UI integration  
**Last Updated:** January 23, 2026

---

## Table of Contents

1. [Overview](#overview)
2. [Architecture](#architecture)
3. [Integration Pattern](#integration-pattern)
4. [Feature Modules](#feature-modules)
5. [Core Data Models](#core-data-models)
6. [Key Services](#key-services)
7. [Data Access](#data-access)
8. [Quick Start Guide](#quick-start-guide)

---

## Overview

RealmEngine is a **backend-only** game engine designed to handle all game logic, data management, and state persistence for RPG-style games. It contains **zero production UI code** - all user interface is handled by the Godot frontend.

### What's Included

- ✅ **25 Feature Modules**: Complete RPG systems including character, combat, magic, crafting, and progression
- ✅ **100+ Commands & Queries**: Full CQRS API via MediatR
- ✅ **Rich Data Models**: Character, Item, Quest, Enemy, SaveGame, HarvestableNode, etc.
- ✅ **211 JSON Data Files**: Items, enemies, abilities, spells, skills, materials, quests
- ✅ **581 Game Elements**: 383 abilities + 144 spells + 54 skills
- ✅ **Procedural Generation**: Items, NPCs, names, loot tables
- ✅ **Persistence Layer**: LiteDB-based save/load system

### What's NOT Included

- ❌ No UI/UX (handled by Godot)
- ❌ No input handling (handled by Godot)
- ❌ No rendering (handled by Godot)
- ❌ No audio playback (handled by Godot)

---

## Architecture

### CQRS Pattern with MediatR

All game operations use the **Command/Query** pattern:

- **Commands**: Mutate game state (e.g., `AttackEnemyCommand`, `BuyFromShopCommand`)
- **Queries**: Read game state (e.g., `GetInventoryQuery`, `GetActiveQuestsQuery`)

```csharp
// Example: Godot calls backend via IMediator
var result = await _mediator.Send(new AttackEnemyCommand 
{ 
    CharacterName = "Player1",
    EnemyId = "goblin-001",
    AbilityRef = "@abilities/active/offensive:basic-attack"
});

if (result.Success) 
{
    UpdateUI(result);
}
```

### Project Structure

```
RealmEngine.Core/          # Game logic, commands, handlers, generators
RealmEngine.Shared/        # Data models, interfaces, abstractions
RealmEngine.Data/          # Data access, repositories, JSON loading
```

---

## Integration Pattern

### Dependency Injection Setup

```csharp
// In Godot C# project (or any .NET consumer)
using RealmEngine.Core;

// 1. Register MediatR
services.AddMediatR(cfg => 
    cfg.RegisterServicesFromAssembly(typeof(AttackEnemyCommand).Assembly));

// 2. Register RealmEngine Data services (cache, repositories, reference resolver)
services.AddRealmEngineData("Data/Json");  // Path to JSON data folder

// 3. Register RealmEngine Core services (generators, validators)
services.AddRealmEngineCore();

// 4. Register additional services as needed
services.AddScoped<ISaveGameService, SaveGameService>();
services.AddScoped<ICharacterRepository, CharacterRepository>();
```

### Typical Workflow

```
Godot UI → Send Command/Query via IMediator → RealmEngine Backend → Response DTO → Update Godot UI
```

**Example Integration:**
```csharp
// 1. Player clicks "Attack" button in Godot
// 2. Godot sends command to backend
var result = await _mediator.Send(new AttackEnemyCommand 
{ 
    CharacterName = playerName,
    Action = CombatActionType.Attack 
});

// 3. Backend processes combat logic, returns result
// 4. Godot updates UI with result data
if (result.Success) 
{
    UpdateHealthBar(result.PlayerHealth, result.EnemyHealth);
    ShowDamageNumber(result.Damage);
    PlayAnimation(result.AttackType);
}
```

---

## Feature Modules

**Legend:**
- ✅ = Fully Implemented with Commands/Queries
- 📋 = Design Complete, Not Yet Implemented
- 🎮 = UI-Dependent Feature (Godot Implementation)

---

### 1. Character Creation ✅
Create and configure player characters with classes, attributes, and starting equipment.

**Commands:**
- `CreateCharacterCommand` - Create new character with class and starting gear
- `SelectClassCommand` - Choose character class during creation
- `AllocateAttributePointsCommand` - Distribute attribute points

**Queries:**
- `GetAvailableClassesQuery` - List all playable classes
- `GetClassDetailsQuery` - Get class stats, abilities, and equipment
- `PreviewCharacterStatsQuery` - Preview stats before finalizing

**Key Properties:** ✅
Turn-based combat with abilities, spell `Level`, `Experience`
- `Attributes` (Strength, Dexterity, Intelligence, etc.)
- `StartingAbilities`, `StartingEquipment`

---

### 2. Combat System ✅
Turn-based combat with abilities, status effects, and tactical options.

**Commands:**
- `AttackEnemyCommand` - Execute basic or ability-based attacks
- `DefendActionCommand` - Raise defenses for damage reduction
- `FleeFromCombatCommand` - Attempt to escape from battle
- `UseCombatItemCommand` - Use consumable items during combat
- `ApplyStatusEffectCommand` - Apply buffs/debuffs to targets
- `ProcessStatusEffectsCommand` - Process ongoing effects each turn
- `EncounterBossCommand` - Initiate boss battle

**Queries:**
- `GetCombatStateQuery` - Get current combat status and turn order
- `GetEnemyInfoQuery` - Retrieve enemy stats and abilities

**Key Properties:**
- `PlayerHealth`, `EnemyHealth`
- `Damage`, `CriticalHit`, `AttackType`
- `StatusEffects` (Poison, Burn, Stun, etc.)
- `TurnOrder`, `ActionQueue`

---

### 3. Inventory & Equipment ✅
Equipment, consumables, materials, weight limits, and gear management.

**Commands:**
- `EquipItemCommand` - Equip weapons, armor, accessories
- `UnequipItemCommand` - Remove equipped items
- `DropItemCommand` - Drop items from inventory
- `SortInventoryCommand` - Auto-sort by type/rarity/value
- `UseItemCommand` - Consume or activate items

**Queries:**
- `GetInventoryQuery` - Get full inventory with filters
- `GetEquippedItemsQuery` - List currently equipped gear
- `GetItemInfoQuery` - Get detailed item information

**Key Properties:**
- `Items` (collection), `MaxSlots`, `CurrentWeight`, `MaxWeight`
- `EquippedWeapon`, `EquippedArmor`, `EquippedAccessories`
- `EquipmentSlots` (Head, Chest, Legs, MainHand, OffHand, etc.)
- `Gold`, `TotalValue`, `SetBonuses`

---

### 4. Abilities System ✅
**383 total abilities** organized by activation type with passive, active, reactive, and ultimate powers.

**Commands:**
- `LearnAbilityCommand` - Learn new ability (class/level validation)
- `UseAbilityCommand` - Execute ability with cooldowns and costs

**Queries:**
- `GetAvailableAbilitiesQuery` - Abilities character can learn
- `GetLearnedAbilitiesQuery` - Show all learned abilities

**Ability Types:**
- **Active (177)**: Offensive, defensive, support, utility, control, summon, mobility
- **Passive (131)**: General, offensive, defensive, leadership, environmental
- **Reactive (63)**: On-hit, on-crit, on-damage-taken, on-kill triggers
- **Ultimate (12)**: Powerful signature abilities with long cooldowns

**Key Properties:**
- `A9. Harvesting System ✅Name`, `Tier` (1-5)
- `ManaCost`, `Cooldown`, `ActivationType`
- `Damage`, `Healing`, `Duration`, `Effects`
- `ClassRestrictions`, `LevelRequirement`

---

### 5. Skills System ✅
**54 total skills** organized into 5 categories with ranks 0-100 progression through use.

**Commands:**
- `AwardSkillXPCommand` - Grant skill experience
- `SetStartingSkillsCommand` - Initialize character skills

**Queries:**
- `GetSkillProgressQuery` - View skill level and XP
- `GetAllSkillsProgressQuery` - All skills overview

**Skill Categories:**
- **10. Enchanting System ✅)**: Athletics, Acrobatics, Stealth, Arcana, etc.
- **Weapon Skills (14)**: Light Blades, Heavy Weapons, Archery, etc.
- **Armor Skills (6)**: Light Armor, Medium Armor, Heavy Armor, Shields
- **Magic Skills (7)**: Arcane, Divine, Occult, Primal, Force Magic, etc.
- **Profession Skills (3)**: Crafting, Harvesting, Alchemy

**Key Properties:**
- `SkillName`, `SkillRank` (0-100), `SkillXP`
- `GoverningAttribute`, `BonusMultiplier`
- `UnlockLevel`, `UsageCount`

---

### 6. Magic & Spells System ✅
**144 total spells** across 4 magical traditions (Pathfinder 2e) with ranks 0-10.

**Commands:**
- `LearnSpellCommand` - Add spell to spellbook
- `CastSpellCommand` - Cast spell with mana cost and skill checks

**Queries:**
- `GetLearnableSpellsQuery` - Spells available for current level
- `GetKnownSpellsQuery` - List all learned spells
- `GetSpellDetailsQuery` - Full spell information
- `GetSpellCostQuery` - Calculate mana and material costs

**Magical Traditions:**
- **Arcane (36 spells)**: Study-based magic, raw power, force manipulation
- **Divine (36 spells)**: Faith-based magic, healing, holy power
- **Occult (36 spells)**: Mind magic, illusions, hidden knowledge
- **Primal (36 spells)**: Nature magic, elements, beasts

**Key Properties:**
- `SpellRef`, `SpellName`, `SpellRank` (0-10 Cantrip to Legendary)
- `ManaCost`, `CastTime`, `Range`, `Duration`
- `Tradition`, `RequiredSkills`, `ComponentCost`
- `Damage`, `Healing`, `StatusEffects`, `AreaOfEffect`

---

### 7. Status Effects System ✅
Temporary conditions affecting combat and exploration with damage-over-time, crowd control, and stat modifications.

**Note:** Status effects are applied via `ApplyStatusEffectCommand` and processed via `ProcessStatusEffectsCommand` (both in Combat feature).

**Commands:** (See Combat System)
- `ApplyStatusEffectCommand` - Apply effect to target
- `ProcessStatusEffectsCommand` - Tick all active effects

**Queries:** (Domain Service)
- Status effect queries handled by combat state inspection

**Effect Categories:**
- **Damage-Over-Time**: Poison, Burning, Bleeding, Frostbite
- **Crowd Control**: Stunned, Frozen, Slowed, Rooted, Silenced
- **Stat Modification**: Blessed, Cursed, Weakened, Strengthened
- **Environmental**: Burning Terrain, Poison Gas, Freezing Cold

**Key Properties:**
- `EffectType`, `EffectName`, `Duration`, `TickRate`
- `DamagePerTick`, `StatModifiers`, `IsStackable`
- `Resistances`, `Immunities`, `CureMethods`

---

### 8. Item Generation System ✅
**NEW** - Procedural item generation with quantity control, budget management, and category filtering.

**Commands:**
- `GenerateRandomItemsCommand` - Generate random items from any/all categories
- `GenerateItemsByCategoryCommand` - Generate items from specific category

**Queries:**
- `GetAvailableItemCategoriesQuery` - List all available item categories with metadata

**Generation Modes:**
- **Simple Catalog-Based**: Fast generation from catalog (no materials/enchantments)
- **Budget-Based**: Full enhancement system with materials, enchantments, and sockets

**Parameters:**
- `Quantity` (1-1000): Number of items to generate
- `Category`: Target category or "random" for variety
- `MinBudget`/`MaxBudget`: Control item quality (10-80+)
- `UseBudgetGeneration`: Toggle enhancement system

**Key Properties:**
- `RequestedQuantity`, `ActualQuantity`
- `Items` (generated items list)
- `CategoriesUsed`, `ErrorMessage`

**Supported Categories:**
- Weapons: swords, axes, maces, daggers, staves, bows, crossbows, spears, fist-weapons
- Armor: light, medium, heavy, shields
- Accessories: amulets, rings, cloaks, belts
- Consumables: potions, food, scrolls

**See:** [ItemGeneration/README.md](../RealmEngine.Core/Features/ItemGeneration/README.md) for detailed usage

---

### 9. Crafting System ✅
Recipe-based crafting with materials, tools, and skill requirements.

**Commands:**
- `CraftItemCommand` - Craft item from recipe and materials
- `LearnRecipeCommand` - Add recipe to known list
- `RepairItemCommand` - Restore item durability

**Queries:**
- `GetKnownRecipesQuery` - List all learned recipes

**Key Properties:**
- `RecipeRef`, `RequiredMaterials`, `RequiredTools`
- `SkillRequirement`, `SuccessChance`
- `OutputItem`, `CraftingTime`

---

### 10. Harvesting System ✅
Resource gathering from nodes (ores, trees, herbs) with tool requirements and skill progression.

**Commands:**
- `HarvestNodeCommand` - Extract resources from harvestable node

**Queries:**
- `GetNearbyNodesQuery` - Find harvestable nodes in area
- `InspectNodeQuery` - Preview node rewards and requirements

**Key Properties:**
- `NodeId`, `NodeType` (OreVein, Tree, Herb, etc.)
- `ResourceYield`, `MaterialRef`, `RemainingUses`
- `MinToolTier`, `RequiredSkill`, `SkillXpReward`
- `IsHarvestable`, `IsDepleted`, `RespawnTime`

---

### 11. Enchanting System ✅
Apply magical enchantments to items for stat bonuses.

**Commands:**
- `EnchantItemCommand` - Add enchantment to item
- `RemoveEnchantmentCommand` - Strip enchantment from item
- `TransferEnchantmentCommand` - Move enchantment between items

**Queries:** (Domain Service)
- Enchantment queries handled by item inspection services

**Key Properties:**
- `EnchantmentRef`, `EnchantmentSlots`, `MaxSlots`
- `StatBonuses` (e.g., +10 Strength, +5% Crit)
- `ApplicationCost`, `RemovalCost`

---

### 12. Socketing System ✅
Insert gems/runes into socketed items for customization.

**Commands:**
- `SocketItemCommand` - Insert socketable into item
- `UnsocketItemCommand` - Extract socketable
- `SocketMultipleItemsCommand` - Batch socket operation

**Queries:**
- `GetCompatibleSocketablesQuery` - List compatible gems/runes
- `GetSocketInfoQuery` - View item socket details
- `GetSocketPreviewQuery` - Preview stat changes before socketing
- `GetUnsocketCostQuery` - Calculate removal cost

**Key Properties:**
- `SocketSlots`, `FilledSockets`, `SocketType`
- `SocketableItem` (gem, rune, jewel)
- `SocketBonuses`, `SetBonuses`

---

### 13. Upgrading System ✅
Enhance items to higher tiers (+1, +2, +3, etc.) with increased stats.

**Commands:**
- `UpgradeItemCommand` - Increase item tier (+1, +2, etc.)

**Queries:** (Domain Service)
- Upgrade cost/chance calculations handled by ItemGenerator service

**Key Properties:**
- `UpgradeLevel` (+0, +1, +2, ... +10)
- `UpgradeMaterials`, `UpgradeCost`
- `SuccessChance`, `FailureConsequence`

---

### 14. Salvaging System ✅
Break down items into raw materials for crafting.

**Commands:**
- `SalvageItemCommand` - Dismantle item into components

**Queries:** (Domain Service)
- Salvage preview handled by ItemGenerator service

**Key Properties:**
- `SalvagedMaterials` (list), `SalvageYield`
- `ItemsLost`, `MaterialsGained`

---

### 15. Shop & Economy ✅
Buy/sell items with NPCs, dynamic pricing, and merchant inventories.

**Commands:**
- `BuyFromShopCommand` - Purchase item from merchant
- `SellToShopCommand` - Sell item to merchant
- `BrowseShopCommand` - View merchant inventory
- `RefreshMerchantInventoryCommand` - Regenerate shop stock

**Queries:**
- `GetMerchantInfoQuery` - Merchant details and disposition
- `CheckAffordabilityQuery` - Verify player can afford item

**Key Properties:**
- `MerchantId`, `MerchantType`, `ShopInventory`
- `BuyPriceModifier`, `SellPriceModifier`
- `ReputationBonus`, `RefreshInterval`

---

### 16. Quest System ✅
Story quests, side quests, objectives, and rewards.

**Commands:**
- `StartQuestCommand` - Add quest to active list
- `UpdateQuestProgressCommand` - Progress quest objectives
- `CompleteQuestCommand` - Finalize quest and claim rewards
- `SetStartingQuestsCommand` - Initialize starting quests

**Queries:**
- `GetAvailableQuestsQuery` - List quests player can accept
- `GetActiveQuestsQuery` - Show current quests
- `GetCompletedQuestsQuery` - Quest history
- `GetMainQuestChainQuery` - Story progression quests

**Key Properties:**
- `QuestRef`, `QuestName`, `QuestType` (Main, Side, Daily)
- `Objectives`, `Rewards`, `Requirements`
- `QuestStatus` (Available, Active, Completed, Failed)

---

### 17. Progression System ✅
Level ups, skill trees, ability unlocks, and stat growth.

**Commands:**
- `AllocateAttributePointsCommand` - Spend points on attributes
- `LevelUpCommand` - Increase character level
- `GainExperienceCommand` - Award XP to character
- `UnlockClassFeatureCommand` - Unlock class-specific features
- `RespecCharacterCommand` - Reset character progression
- `SetStartingAbilitiesCommand` - Initialize starting abilities

**Queries:**
- `GetCharacterProgressionQuery` - View all progression stats
- `GetNextLevelRequirementQuery` - XP needed for next level
- `GetAvailableClassFeaturesQuery` - List unlockable class features
- `GetRespecCostQuery` - Calculate respec cost

**Key Properties:**
- `Level`, `Experience`, `NextLevelXP`
- `AttributePoints`, `SkillPoints`, `AbilityPoints`
- `TotalXpEarned`, `DeathCount`, `PlayTime`

---

### 18. Reputation System ✅
Track relationships with factions, guilds, and NPCs.

**Commands:**
- `ModifyReputationCommand` - Change faction standing
- `JoinFactionCommand` - Become faction member

**Queries:**
- `GetReputationQuery` - View reputation with faction

**Key Properties:**
- `FactionId`, `ReputationLevel` (Hostile, Neutral, Friendly, etc.)
- `ReputationPoints`, `ReputationTier`
- `FactionBenefits`, `FactionQuests`

---

### 19. Party System ✅
Manage party members, formations, and group actions.

**Commands:**
- `AddPartyMemberCommand` - Recruit companion
- `RemovePartyMemberCommand` - Dismiss companion
- `SetPartyLeaderCommand` - Switch active character

**Queries:**
- `GetPartyMembersQuery` - List all party members

**Key Properties:**
- `PartyMembers` (list), `PartyLeader`, `MaxSize`
- `Formation`, `PartyBonuses`

---

### 20. Exploration System ✅
Map traversal, location discovery, and fast travel.

**Commands:**
- `MoveToLocationCommand` - Travel to new area
- `DiscoverLocationCommand` - Unlock map location
- `UnlockFastTravelCommand` - Enable fast travel point
- `FastTravelCommand` - Instant travel to discovered location
- `SearchLocationCommand` - Find hidden items/secrets
- `EnterDungeonCommand` - Enter instanced dungeon
- `ExitDungeonCommand` - Leave current dungeon
- `RestAtInnCommand` - Rest to restore health/mana

**Queries:**
- `GetCurrentLocationQuery` - Player's current position
- `GetDiscoveredLocationsQuery` - Unlocked map areas
- `GetFastTravelPointsQuery` - Available fast travel destinations
- `GetLocationDetailsQuery` - Get location information
- `GetNearbyLocationsQuery` - Adjacent explorable areas

**Domain Service (ExplorationService):**
- `ExploreAsync()` → `ExplorationResult` - Perform exploration at current location (60% combat, 40% peaceful)
- `GetAvailableLocations()` → `TravelResult` - Get locations available for travel
- `TravelToLocation(string locationName)` → `TravelResult` - Travel to specific location
- `RecoverDroppedItems(string location)` → `bool` - Recover items dropped at location
- `GetKnownLocationsAsync()` → `IReadOnlyList<Location>` - Get all discovered locations

**Result DTOs:**

*ExplorationResult*
- `Success` (bool) - Operation succeeded
- `CombatEncounter` (bool) - True if combat triggered (60% chance)
- `CurrentLocation` (string) - Current location name
- `XpGained` (int) - Experience reward (10-30 for peaceful)
- `GoldGained` (int) - Gold reward (5-25 for peaceful)
- `ItemFound` (Item?) - Optional item loot (30% chance)
- `LeveledUp` (bool) - Player leveled up from XP
- `NewLevel` (int?) - New level if leveled up
- `ErrorMessage` (string?) - Error details if failed

*TravelResult*
- `Success` (bool) - Operation succeeded
- `CurrentLocation` (string) - Current location name
- `AvailableLocations` (List<Location>) - Locations available for travel
- `DroppedItemsAtLocation` (List<Item>) - Items dropped at this location
- `ErrorMessage` (string?) - Error details if failed

**Key Properties:**
- `LocationId`, `LocationName`, `LocationType`
- `IsDiscovered`, `IsFastTravelPoint`
- `AdjacentLocations`, `RequiredLevel`

**Integration Example:**
```csharp
// Explore current location
var result = await _explorationService.ExploreAsync();

if (result.CombatEncounter)
{
    InitiateCombat(result.CurrentLocation);
}
else
{
    ShowRewards($"+{result.XpGained} XP, +{result.GoldGained} gold");
    if (result.ItemFound != null)
    {
        ShowItemFound(result.ItemFound);
    }
    if (result.LeveledUp)
    {
        ShowLevelUp(result.NewLevel.Value);
    }
}

// Get available travel destinations
var locations = await _explorationService.GetAvailableLocations();
var chosen = ShowLocationMenu(locations.AvailableLocations);

// Travel to chosen location
var travelResult = await _explorationService.TravelToLocation(chosen);
if (travelResult.DroppedItemsAtLocation.Any())
{
    if (ConfirmRecovery($"Found {travelResult.DroppedItemsAtLocation.Count} dropped items!"))
    {
        _explorationService.RecoverDroppedItems(travelResult.CurrentLocation);
    }
}
```

---

### 21. Achievement System ✅
Track player accomplishments and milestones.

**Commands:**
- `UnlockAchievementCommand` - Award achievement
- `TrackAchievementProgressCommand` - Update achievement progress

**Queries:**
- `GetUnlockedAchievementsQuery` - Earned achievements

**Key Properties:**
- `AchievementId`, `AchievementName`, `Description`
- `Progress`, `TotalRequired`, `IsCompleted`
- `Rewards`, `UnlockDate`

---

### 21. Death & Respawn System ✅
Handle character death, respawn mechanics, and difficulty-based penalties.

**Commands:**
- `HandlePlayerDeathCommand` - Process death, calculate penalties, handle permadeath
- `RespawnCommand` - Revive character at respawn location with stat restoration

**Queries:**
- `GetRespawnLocationQuery` - Get available respawn points (Hub Town + discovered safe zones)

**Respawn Mechanics:**
- **Location Selection**: Smart location based on death region (Hub Town default)
- **Safe Zones**: Discovered towns, villages, and sanctuaries become respawn points
- **Health/Mana Restoration**: 100% on Easy/Normal, 75% on Expert+
- **Cooldowns**: 0-5 seconds based on difficulty
- **Resurrection Sickness**: Debuff duration scales with difficulty and death count

**Death Penalties (Difficulty-Based):**
- **Easy**: 5% gold loss, 0% XP loss, no item drops
- **Normal**: 10% gold loss, 5% XP loss, no item drops
- **Hard**: 15% gold loss, 10% XP loss, 25% chance to drop items
- **Expert/Ironman**: 20% gold loss, 15% XP loss, 50% chance to drop items
- **Permadeath/Apocalypse**: Character deleted, Hall of Fame entry created

**Key Properties:**
- `IsDead`, `DeathCount`, `LastDeathTime`, `LastDeathLocation`
- `DeathPenalty` (gold/XP loss, item drops), `HallOfFameEntry` (for permadeath)
- `RespawnLocation`, `ResurrectionSicknessStacks`

**Integration Example:**
```csharp
// Get available respawn points
var locations = await _mediator.Send(new GetRespawnLocationQuery());
ShowRespawnMenu(locations.AvailableLocations);

// Respawn player at chosen location
var result = await _mediator.Send(new RespawnCommand 
{ 
    Player = currentCharacter,
    RespawnLocation = "Sanctuary of Light" // Optional, defaults to Hub Town
});
TeleportPlayer(result.RespawnLocation);
UpdateHealthBar(result.Health, currentCharacter.MaxHealth);
```

---

### 22. Difficulty System ✅
Adjustable challenge levels with enemy scaling, death penalties, and reward multipliers.

**Commands:**
- `SetDifficultyCommand` - Change difficulty level, initializes apocalypse timer if applicable

**Queries:**
- `GetDifficultySettingsQuery` - View current difficulty settings with all multipliers
- `GetAvailableDifficultiesQuery` - List all 7 difficulty modes with current selection

**Difficulty Levels (7 Total):**

| Difficulty | Enemy Dmg | Enemy HP | Player Dmg | Gold/XP | Death Penalty | Special |
|------------|-----------|----------|------------|---------|---------------|--------|
| **Easy** | 0.75× | 0.75× | 1.25× | 1.25× | 5% gold, 0% XP | Manual save enabled |
| **Normal** | 1.0× | 1.0× | 1.0× | 1.0× | 10% gold, 5% XP | Manual save enabled |
| **Hard** | 1.25× | 1.25× | 0.9× | 1.25× | 15% gold, 10% XP | Manual save enabled |
| **Expert** | 1.5× | 1.5× | 0.8× | 1.5× | 20% gold, 15% XP | Manual save enabled |
| **Ironman** | 1.75× | 1.75× | 0.75× | 1.75× | 20% gold, 15% XP | Auto-save only |
| **Permadeath** | 2.0× | 2.0× | 0.7× | 2.0× | 100% (character deleted) | Auto-save only, Hall of Fame |
| **Apocalypse** | 2.5× | 2.5× | 0.6× | 2.5× | 100% (character deleted) | 240-minute time limit, auto-save |

**Domain Service (DifficultyService):**
- `CalculatePlayerDamage()` - Adjust player damage based on difficulty
- `CalculateEnemyDamage()` - Adjust enemy damage based on difficulty
- `CalculateEnemyHealth()` - Scale enemy HP based on difficulty
- `CalculateGoldReward()` - Apply difficulty bonus to gold drops
- `CalculateXPReward()` - Apply difficulty bonus to XP gains
- `CalculateGoldLoss()` - Calculate gold penalty on death
- `CalculateXPLoss()` - Calculate XP penalty on death
- `CanManualSave()` - Check if manual saving is allowed
- `IsPermadeath()` - Check if difficulty has permadeath
- `IsApocalypseMode()` - Check if apocalypse timer is active

**Key Properties:**
- `Name`, `PlayerDamageMultiplier`, `EnemyDamageMultiplier`, `EnemyHealthMultiplier`
- `GoldRewardMultiplier`, `XPRewardMultiplier`
- `GoldLossPercentage`, `XPLossPercentage`, `ItemDropChance`
- `AutoSaveOnly`, `IsPermadeath`, `ApocalypseTimeLimitMinutes`

**Integration Example:**
```csharp
// Get available difficulties for menu
var result = await _mediator.Send(new GetAvailableDifficultiesQuery());
foreach (var difficulty in result.Difficulties) 
{
    AddMenuOption(difficulty.Name, difficulty.Description);
}

// Set difficulty and handle apocalypse mode
var setResult = await _mediator.Send(new SetDifficultyCommand { DifficultyName = "Hard" });
if (setResult.ApocalypseModeEnabled) 
{
    ShowTimerUI(setResult.ApocalypseTimeLimitMinutes.Value);
}

// Calculate adjusted values using DifficultyService
var difficulty = await _mediator.Send(new GetDifficultySettingsQuery());
var adjustedDamage = _difficultyService.CalculateEnemyDamage(baseDamage, difficulty.Settings);
var goldDrop = _difficultyService.CalculateGoldReward(baseGold, difficulty.Settings);
```

---

### 23. Victory System ✅
Game completion, new game plus, and post-game content.

**Commands:**
- `TriggerVictoryCommand` - Mark game as completed
- `StartNewGamePlusCommand` - Start NG+ with bonuses

**Queries:**
- `GetVictoryStatusQuery` - Check completion status
- `GetNewGamePlusBonusesQuery` - NG+ benefits

**Key Properties:**
- `IsGameCompleted`, `CompletionDate`
- `NewGamePlusLevel`, `CarryOverItems`

---

### 24. Save/Load System ✅
Persistent game state management with LiteDB.

**Commands:**
- `SaveGameCommand` - Persist current game state
- `LoadGameCommand` - Load saved game
- `DeleteSaveCommand` - Remove save file

**Queries:**
- `GetAllSavesQuery` - List all save files
- `GetSaveDetailsQuery` - Metadata for specific save
- `GetMostRecentSaveQuery` - Get most recently saved game

**Domain Service (LoadGameService):**
- `LoadGame(int saveId)` → `LoadGameResult` - Load saved game and restore apocalypse timer
- `GetAllSaves()` → `List<SaveGame>` - Get all available save games for menu display

**Result DTOs:**

*LoadGameResult*
- `Success` (bool) - Operation succeeded
- `SaveGame` (SaveGame?) - Loaded save game data
- `ApocalypseMode` (bool) - True if apocalypse mode is active
- `ApocalypseTimeExpired` (bool) - True if timer expired while away
- `ApocalypseRemainingMinutes` (int?) - Minutes remaining on apocalypse timer
- `ErrorMessage` (string?) - Error details if failed

**Key Properties:**
- `SaveId`, `SaveName`, `SaveDate`
- `Character` (full state), `Playtime`
- `Location`, `QuestProgress`, `InventorySnapshot`
- `ApocalypseMode`, `ApocalypseStartTime`, `ApocalypseBonusMinutes`

**Integration Example:**
```csharp
// Get available saves for menu
var saves = _loadGameService.GetAllSaves();
DisplaySaveMenu(saves);

// User selects save #3
var result = _loadGameService.LoadGame(selectedSaveId: 3);

if (result.Success)
{
    LoadCharacter(result.SaveGame.Character);
    
    if (result.ApocalypseMode)
    {
        if (result.ApocalypseTimeExpired)
        {
            ShowGameOver("Time ran out!");
        }
        else
        {
            ShowApocalypseTimer(result.ApocalypseRemainingMinutes.Value);
        }
    }
}
else
{
    ShowError(result.ErrorMessage);
}
```

---

### 25. Procedural Generation System ✅
CQRS wrappers for procedural content generation (items, enemies, NPCs, abilities).

**Commands:**
- `GenerateItemCommand` - Generate items by category or budget
- `GenerateEnemyCommand` - Generate enemies with optional level scaling
- `GenerateNPCCommand` - Generate NPCs by category with traits
- `GenerateAbilityCommand` - Generate abilities by category/subcategory

**Generation Modes:**

**Items:**
- **Budget-Based**: `BudgetRequest` with `MinBudget`/`MaxBudget`, `AllowedTypes`, `ForbiddenMaterials`
- **Category-Based**: Direct category like "weapons/swords", "armor/light-armor", "consumables/potions"
- **Hydration**: Optional full property resolution (default: true)

**Enemies:**
- **Category-Based**: "beasts", "undead", "humanoid", "elemental", "demons"
- **Level Scaling**: Optional level parameter applies scaling (MaxHealth × Level)
- **Hydration**: Optional full property resolution

**NPCs:**
- **Category-Based**: "merchants", "guards", "quest-givers", "trainers"
- **Traits**: Procedural personality traits, dialogue, inventory
- **Hydration**: Optional full property resolution

**Abilities:**
- **Category/Subcategory**: "active/offensive", "passive/defensive", "reactive/on-hit", "ultimate"
- **Specific or Random**: Generate by name or random selection
- **Count**: Generate multiple abilities in one call
- **Hydration**: Optional full property resolution

**Integration Example:**
```csharp
// Generate loot for chest (budget-based)
var lootResult = await _mediator.Send(new GenerateItemCommand 
{
    BudgetRequest = new BudgetItemRequest 
    {
        MinBudget = 500,
        MaxBudget = 1000,
        AllowedTypes = new[] { "weapons", "armor" },
        ForbiddenMaterials = new[] { "wood", "leather" }
    }
});
if (lootResult.Success) 
{
    AddToChest(lootResult.Item);
}

// Generate enemy encounter (level-scaled)
var enemyResult = await _mediator.Send(new GenerateEnemyCommand 
{
    Category = "undead",
    Level = playerLevel + 2,
    Hydrate = true
});
if (enemyResult.Success) 
{
    SpawnEnemy(enemyResult.Enemy);
}

// Generate merchant NPC
var npcResult = await _mediator.Send(new GenerateNPCCommand 
{
    Category = "merchants",
    Hydrate = true
});
if (npcResult.Success) 
{
    SpawnNPC(npcResult.NPC);
    PopulateMerchantInventory(npcResult.NPC);
}

// Generate starting abilities for class
var abilityResult = await _mediator.Send(new GenerateAbilityCommand 
{
    Category = "active",
    Subcategory = "offensive",
    Count = 3,
    Hydrate = true
});
if (abilityResult.Success) 
{
    foreach (var ability in abilityResult.Abilities)
    {
        LearnAbility(character, ability);
    }
}
```

**Key Properties:**
- **GenerateItemResult**: `Success`, `Item` (with stats, materials, enchantments), `ErrorMessage`
- **GenerateEnemyResult**: `Success`, `Enemy` (with abilities, stats, loot table), `ErrorMessage`
- **GenerateNPCResult**: `Success`, `NPC` (with traits, inventory, dialogue), `ErrorMessage`
- **GenerateAbilityResult**: `Success`, `Ability`, `Abilities` (list), `ErrorMessage`

---

### 26. New Game Plus 📋
Start new playthrough with bonuses and increased difficulty after completing the game.

**Commands:** (Planned)
- `StartNewGamePlusCommand` - Start NG+ with bonuses
- `IncrementNewGamePlusLevelCommand` - Increase NG+ tier

**Queries:** (Planned)
- `GetNewGamePlusBonusesQuery` - View NG+ benefits
- `GetCarryOverItemsQuery` - Items that transfer to NG+

**Key Properties:**
- `NewGamePlusLevel` (NG+, NG++, NG+++, etc.)
- `CarryOverItems`, `BonusAttributes`, `BonusSkillPoints`
- `EnemyScaling`, `LootRarity`, `GoldMultiplier`

---

## Unimplemented Features (Design Only)

The following features are **designed but not yet implemented** in the backend:

### Modding Support 📋
- JSON-based content modules
- Custom items, enemies, quests via data files
- Override system for base game content
- Future: C# scripting for behaviors

### Online & Community Features 📋
- Global leaderboards by difficulty/class
- Daily challenges with unique rewards
- Save sharing and build exports
- Seasonal events and competitions

### Quality of Life Enhancements 🎮
- Undo actions (UI-dependent)
- Tutorial system (UI-dependent)
- Keybind customization (UI-dependent)
- Most QoL features require Godot implementation

---

## Potential Feature Expansions

The following commands/queries could enhance existing features in future iterations:

### Combat Enhancements
- `SwitchTargetCommand` - Change combat target mid-fight
- `GetAvailableCombatActionsQuery` - List valid actions based on current state
- `PreviewAttackResultQuery` - Show estimated damage/hit chance before attacking
- `CounterAttackCommand` - Reactive combat mechanic

### Inventory Enhancements
- `TransferItemCommand` - Move items between characters/storage
- `SearchInventoryQuery` - Find items by name/type/trait/material
- `GetInventoryStatsQuery` - Calculate weight, capacity, total value
- `CompareItemsQuery` - Side-by-side stat comparison
- `FavoriteItemCommand` - Mark items as favorites to prevent accidental sale/salvage

### Crafting Enhancements
- `PreviewCraftingResultQuery` - Show item stats before crafting
- `GetCraftableItemsQuery` - Filter recipes by available materials
- `DiscoverRecipeCommand` - Unlock recipe through exploration/combat
- `MassProduceCommand` - Craft multiple items at once

### Progression Enhancements
- `GetAvailablePerksQuery` - Show unlockable character perks by level
- `PreviewLevelUpQuery` - Show stat gains before leveling
- `GetBuildSuggestionsQuery` - AI-recommended builds for playstyle

### Exploration Enhancements
- `GetAdjacentLocationsQuery` - Find connected areas from current location
- `GetLocationDangerLevelQuery` - Estimate area difficulty
- `SearchAreaCommand` - Find hidden items/secrets in location

### Enchanting Enhancements
- `PreviewEnchantmentQuery` - Show stat changes before applying
- `GetAvailableEnchantsQuery` - List applicable enchantments for item
- `GetEnchantmentCostQuery` - Calculate application cost

### Social/Economy Enhancements
- `GetPriceQuoteQuery` - Get buy/sell prices with reputation modifiers
- `BarterCommand` - Negotiate prices with merchants
- `GetFactionBenefitsQuery` - View perks for current reputation standing

**Note:** These are suggestions for future development. Implementing them would require coordination between backend logic and Godot UI.

---

## Core Data Models

### Character
```csharp
public class Character
{
    public string Name { get; set; }
    public string ClassName { get; set; }
    public int Level { get; set; }
    public int Experience { get; set; }
    public int Health { get; set; }
    public int MaxHealth { get; set; }
    public int Mana { get; set; }
    public int MaxMana { get; set; }
    public Dictionary<string, int> Attributes { get; set; }
    public Dictionary<string, int> Skills { get; set; }
    public List<string> LearnedAbilities { get; set; }
    public List<string> LearnedSpells { get; set; }
    public List<Item> Inventory { get; set; }
    public Dictionary<string, Item> EquippedItems { get; set; }
    public int Gold { get; set; }
    // ... 40+ additional properties
}
```

### Item
```csharp
public class Item
{
    public string Id { get; set; }
    public string Slug { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string? Lore { get; set; }
    public int Price { get; set; }
    public double Weight { get; set; }
    public ItemRarity Rarity { get; set; }
    public ItemType Type { get; set; }
    public Dictionary<string, int> StatModifiers { get; set; }
    public List<string> Traits { get; set; }
    public List<Socket> Sockets { get; set; }
    public List<Enchantment> Enchantments { get; set; }
    public MaterialComposition MaterialComposition { get; set; }
    public int UpgradeLevel { get; set; }
    // ... 50+ additional properties
}
```

### Quest
```csharp
public class Quest
{
    public string QuestRef { get; set; }
    public string QuestName { get; set; }
    public string Description { get; set; }
    public QuestType Type { get; set; }
    public QuestStatus Status { get; set; }
    public List<QuestObjective> Objectives { get; set; }
    public QuestRewards Rewards { get; set; }
    public Dictionary<string, int> Requirements { get; set; }
    public List<string> Prerequisites { get; set; }
    // ... 20+ additional properties
}
```

### Enemy
```csharp
public class Enemy
{
    public string EnemyId { get; set; }
    public string Name { get; set; }
    public EnemyType Type { get; set; }
    public int Level { get; set; }
    public int Health { get; set; }
    public int MaxHealth { get; set; }
    public Dictionary<string, int> Attributes { get; set; }
    public List<string> Abilities { get; set; }
    public LootTable Drops { get; set; }
    public int ExperienceReward { get; set; }
    public int GoldReward { get; set; }
    // ... 30+ additional properties
}
```

### SaveGame
```csharp
public class SaveGame
{
    public string SaveId { get; set; }
    public string SaveName { get; set; }
    public DateTime SaveDate { get; set; }
    public TimeSpan Playtime { get; set; }
    public Character Character { get; set; }
    public List<Quest> ActiveQuests { get; set; }
    public List<Quest> CompletedQuests { get; set; }
    public Dictionary<string, int> Reputation { get; set; }
    public List<string> VisitedLocations { get; set; }
    public List<string> UnlockedAchievements { get; set; }
    // ... 25+ additional properties
}
```

### HarvestableNode
```csharp
public class HarvestableNode
{
    public string NodeId { get; set; }
    public NodeType Type { get; set; } // OreVein, Tree, Herb, etc.
    public string LocationId { get; set; }
    public string LootTableRef { get; set; }
    public int RemainingUses { get; set; }
    public int MaxUses { get; set; }
    public int MinToolTier { get; set; }
    public string RequiredSkill { get; set; }
    public int SkillXpReward { get; set; }
    public DateTime? RespawnTime { get; set; }
    // ... 15+ additional properties
}
```

---

## Key Services

### ItemGenerator
Procedural item generation with materials, enchantments, and budgets.

```csharp
public interface IItemGenerator
{
    Task<Item> GenerateItemAsync(string itemType, int budget);
    Task<Item> GenerateItemByNameAsync(string itemName, int budget);
    Task<List<Item>> GenerateLootAsync(string lootTableRef, int quantity);
}
```

### LootTableService
Process loot tables for enemies, chests, and harvesting nodes.

```csharp
public interface ILootTableService
{
    Task<EnemyLootResult> RollEnemyDrops(string lootTableRef);
    Task<ChestLootResult> RollChestDrops(List<string> lootTableRefs, string mergeStrategy);
    HarvestLootResult RollHarvestingDrops(string lootTableRef, int baseYield, bool isCritical);
}
```

### ReferenceResolverService
Resolve JSON references (`@abilities/active/offensive:basic-attack`).

```csharp
public interface IReferenceResolverService
{
    Task<JToken?> ResolveReferenceAsync(string reference);
    Task<List<JToken>> ResolveMultipleReferencesAsync(List<string> references);
}
```

### SkillProgressionService
Handle skill XP, level ups, and unlocks.

```csharp
public interface ISkillProgressionService
{
    Task<SkillProgressResult> AwardSkillXP(Character character, string skillName, int xp);
    Task<bool> CheckSkillRequirement(Character character, string skillName, int requiredRank);
}
```

### GameDataCache
Centralized JSON data loading and caching.

```csharp
public interface IDataCache
{
    Task<JObject?> GetFileAsync(string relativePath);
    Task<List<JToken>> GetCatalogItemsAsync(string catalogPath);
    Task LoadAllDataAsync();
    void ClearCache();
}
```

---

## Data Access

### LiteDB Repositories

**ICharacterRepository**
```csharp
Task<Character?> GetByNameAsync(string name);
Task<List<Character>> GetAllAsync();
Task SaveAsync(Character character);
Task DeleteAsync(string name);
```

**ISaveGameService**
```csharp
Task<SaveGame?> LoadSaveAsync(string saveId);
Task<string> CreateSaveAsync(SaveGame save);
Task<List<SaveGame>> GetAllSavesAsync();
Task DeleteSaveAsync(string saveId);
```

**INodeRepository**
```csharp
Task<HarvestableNode?> GetNodeByIdAsync(string nodeId);
Task<List<HarvestableNode>> GetNodesByLocationAsync(string locationId);
Task SaveNodeAsync(HarvestableNode node);
Task DeleteNodeAsync(string nodeId);
```

**IInventoryService**
```csharp
Task AddItemAsync(string characterName, Item item);
Task<bool> RemoveItemAsync(string characterName, string itemId);
Task<List<Item>> GetInventoryAsync(string characterName);
Task<bool> ReduceItemDurabilityAsync(string characterName, string itemId, int amount);
```

---

## Quick Start Guide

### 1. Add Package Reference

```xml
<ItemGroup>
  <Reference Include="RealmEngine.Core">
    <HintPath>path\to\package\Libraries\RealmEngine.Core\RealmEngine.Core.dll</HintPath>
  </Reference>
  <Reference Include="RealmEngine.Shared">
    <HintPath>path\to\package\Libraries\RealmEngine.Shared\RealmEngine.Shared.dll</HintPath>
  </Reference>
  <Reference Include="RealmEngine.Data">
    <HintPath>path\to\package\Libraries\RealmEngine.Data\RealmEngine.Data.dll</HintPath>
  </Reference>
</ItemGroup>
```

### 2. Configure Services

```csharp
using MediatR;
using RealmEngine.Core;
using RealmEngine.Data;

var services = new ServiceCollection();

// Register MediatR (handles all Commands/Queries)
services.AddMediatR(cfg => 
    cfg.RegisterServicesFromAssembly(typeof(AttackEnemyCommand).Assembly));

// Register RealmEngine Data services (cache, repositories, reference resolver)
services.AddRealmEngineData("path/to/Data/Json");

// Register RealmEngine Core services (all generators including AbilityGenerator, CharacterClassGenerator)
services.AddRealmEngineCore();

// Register additional game services
services.AddScoped<ISaveGameService, SaveGameService>();
services.AddScoped<ICharacterRepository, CharacterRepository>();
services.AddScoped<INodeRepository, InMemoryNodeRepository>();

var serviceProvider = services.BuildServiceProvider();
```

### 3. Send Commands/Queries

```csharp
// Inject IMediator in your Godot C# scripts
public class PlayerController : Node
{
    private readonly IMediator _mediator;

    public PlayerController(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task AttackEnemy(string enemyId)
    {
        var result = await _mediator.Send(new AttackEnemyCommand
        {
            CharacterName = "Player",
            EnemyId = enemyId,
            AbilityRef = "@abilities/active/offensive:basic-attack"
        });

        if (result.Success)
        {
            GD.Print($"Dealt {result.Damage} damage!");
            UpdateHealthBar(result.EnemyHealth);
        }
    }

    public async Task GetInventory()
    {
        var result = await _mediator.Send(new GetInventoryQuery
        {
            CharacterName = "Player"
        });

        foreach (var item in result.Items)
        {
            GD.Print($"{item.Name} - {item.Rarity}");
        }
    }
}
```

### 4. Access JSON Data

```csharp
// Game data is located in: package/Data/Json/
// Example paths:
// - package/Data/Json/items/weapons/swords/catalog.json
// - package/Data/Json/abilities/active/offensive/catalog.json
// - package/Data/Json/enemies/humanoid/catalog.json

var dataCache = serviceProvider.GetRequiredService<IDataCache>();
await dataCache.LoadAllDataAsync();

// Data is now cached and accessible via ReferenceResolverService
var item = await resolverService.ResolveReferenceAsync("@items/weapons/swords:iron-longsword");
```

---

## Additional Resources

- **Package Manifest**: `package-manifest.json` - Complete file listing
- **Release Notes**: `release-notes.md` - Version history and changes
- **JSON Standards**: See `docs/standards/json/` in source repository
- **Game Design Document**: See `docs/GDD-Main.md` in source repository

---

**Questions or Issues?**  
This is a living specification. For integration support or bug reports, refer to the source repository documentation or contact the development team.
