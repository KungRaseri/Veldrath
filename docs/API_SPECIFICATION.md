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
services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(AttackEnemyCommand).Assembly));
services.AddSingleton<IDataCache, GameDataCache>();
services.AddScoped<ISaveGameService, SaveGameService>();
services.AddScoped<ICharacterRepository, CharacterRepository>();
// ... register other services
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

### 2. Combat System
Turn-based combat with abilities, status effects, and tactical options.

**Commands:**
- `AttackEnemyCommand` - Execute basic or ability-based attacks
- `DefendActionCommand` - Raise defenses for damage reduction
- `FleeFromCombatCommand` - Attempt to escape from battle
- `UseCombatItemCommand` - Use consumable items during combat
- `ApplyStatusEffectCommand` - Apply buffs/debuffs
- `ProcessStatusEffectsCommand` - Process ongoing effects each turn
- `EncounterBossCommand` - Initiate boss battle

**Queries:**
- `GetCombatStateQuery` - Get current combat status
- `GetEnemyStatsQuery` - Retrieve enemy information
- `GetAvailableCombatActionsQuery` - List valid actions

**Key Properties:**
- `PlayerHealth`, `EnemyHealth`
- `Damage`, `CriticalHit`, `AttackType`
- `StatusEffects` (Poison, Burn, Stun, etc.)
- `TurnOrder`, `ActionQueue`

---

### 3. Inventory & Equipment ✅
Equipment, consumables, materials, weight limits, and gear management.

**Commands:**
- `AddItemToInventoryCommand` - Add item to player inventory
- `RemoveItemFromInventoryCommand` - Remove/drop items
- `MoveItemCommand` - Reorganize inventory slots
- `SortInventoryCommand` - Auto-sort by type/rarity/value
- `EquipItemCommand` - Equip weapons, armor, accessories
- `UnequipItemCommand` - Remove equipped items

**Queries:**
- `GetInventoryQuery` - Get full inventory with filters
- `GetEquippedItemsQuery` - List currently equipped gear
- `GetInventoryStatsQuery` - Weight, capacity, value totals
- `SearchInventoryQuery` - Find items by name/type/trait

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
- `GetActiveAbilitiesCommand` - List abilities ready to use

**Queries:**
- `GetLearnedAbilitiesQuery` - Show all learned abilities
- `GetAvailableAbilitiesQuery` - Abilities character can learn
- `GetAbilityCooldownsQuery` - View cooldown timers
- `GetPassiveBonusesQuery` - Calculate passive ability bonuses

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
- `IncreaseSkillLevelCommand` - Progress skill rank
- `AwardSkillXpCommand` - Grant skill experience
- `CheckSkillRequirementCommand` - Validate skill prerequisites

**Queries:**
- `GetSkillProgressQuery` - View skill level and XP
- `GetAllSkillsProgressQuery` - All skills overview
- `GetSkillBonusesQuery` - Calculate skill-based bonuses

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
- `ForgetSpellCommand` - Remove spell from memory

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

**Commands:**
- `ApplyStatusEffectCommand` - Apply effect to target
- `RemoveStatusEffectCommand` - Clear specific effect
- `ProcessStatusEffectsCommand` - Tick all active effects
- `ResistStatusEffectCommand` - Attempt to resist application

**Queries:**
- `GetActiveStatusEffectsQuery` - List current effects
- `GetStatusEffectDetailsQuery` - Effect information
- `GetResistancesQuery` - View resistances and immunities

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

### 8. Crafting System ✅
Recipe-based crafting with materials, tools, and skill requirements.

**Commands:**
- `CraftItemCommand` - Craft item from recipe and materials
- `LearnRecipeCommand` - Add recipe to known list
- `DiscoverRecipeCommand` - Unlock recipe through gameplay
- `RepairItemCommand` - Restore item durability

**Queries:**
- `GetKnownRecipesQuery` - List all learned recipes
- `GetCraftableItemsQuery` - Recipes with available materials
- `GetRecipeDetailsQuery` - View requirements and output
- `PreviewCraftingCostQuery` - Calculate material/gold cost

**Key Properties:**
- `RecipeRef`, `RequiredMaterials`, `RequiredTools`
- `SkillRequirement`, `SuccessChance`
- `OutputItem`, `CraftingTime`

---

### 5. Harvesting System
Resource gathering from nodes (ores, trees, herbs) with tool requirements and skill progression.

**Commands:**
- `HarvestNodeCommand` - Extract resources from harvestable node
- `SpawnResourceNodeCommand` - Generate node at location
- `DepleteNodeCommand` - Mark node as exhausted

**Queries:**
- `GetNearbyNodesQuery` - Find harvestable nodes in area
- `InspectNodeQuery` - Preview node rewards and requirements
- `GetHarvestingStatsQuery` - View gathering statistics

**Key Properties:**
- `NodeId`, `NodeType` (OreVein, Tree, Herb, etc.)
- `ResourceYield`, `MaterialRef`, `RemainingUses`
- `MinToolTier`, `RequiredSkill`, `SkillXpReward`
- `IsHarvestable`, `IsDepleted`, `RespawnTime`

---

### 6. Enchanting System
Apply magical enchantments to items for stat bonuses.

**Commands:**
- `ApplyEnchantmentCommand` - Add enchantment to item
- `RemoveEnchantmentCommand` - Strip enchantment from item
- `AddEnchantmentSlotCommand` - Increase enchantment capacity

**Queries:**
- `GetAvailableEnchantsQuery` - List applicable enchantments
- `GetEnchantmentCostQuery` - Calculate application cost
- `PreviewEnchantmentQuery` - Show stat changes before applying

**Key Properties:**
- `EnchantmentRef`, `EnchantmentSlots`, `MaxSlots`
- `StatBonuses` (e.g., +10 Strength, +5% Crit)
- `ApplicationCost`, `RemovalCost`

---

### 11. Socketing System ✅
Insert gems/runes into socketed items for customization.

**Commands:**
- `SocketItemCommand` - Insert socketable into item
- `RemoveSocketedItemCommand` - Extract socketable
- `SocketMultipleItemsCommand` - Batch socket operation

**Queries:**
- `GetCompatibleSocketablesQuery` - List compatible gems/runes
- `GetSocketInfoQuery` - View item socket details
- `SocketPreviewQuery` - Preview stat changes
- `GetSocketCostQuery` - Calculate operation cost

**Key Properties:**
- `SocketSlots`, `FilledSockets`, `SocketType`
- `SocketableItem` (gem, rune, jewel)
- `SocketBonuses`, `SetBonuses`

---

### 12. Upgrading System ✅
Enhance items to higher tiers (+1, +2, +3, etc.) with increased stats.

**Commands:**
- `UpgradeItemCommand` - Increase item tier (+1, +2, etc.)
- `ResetUpgradeLevelCommand` - Reset to base stats

**Queries:**
- `GetUpgradeCostQuery` - Calculate materials/gold needed
- `GetUpgradeChanceQuery` - Success/failure probability
- `GetMaxUpgradeLevelQuery` - Max tier for item type

**Key Properties:**
- `UpgradeLevel` (+0, +1, +2, ... +10)
- `UpgradeMaterials`, `UpgradeCost`
- `SuccessChance`, `FailureConsequence`

---

### 13. Salvaging System ✅
Break down items into raw materials for crafting.

**Commands:**
- `SalvageItemCommand` - Dismantle item into components
- `SalvageMultipleItemsCommand` - Batch salvage

**Queries:**
- `GetSalvagePreviewQuery` - Preview materials from salvage
- `GetSalvageValueQuery` - Calculate material value

**Key Properties:**
- `SalvagedMaterials` (list), `SalvageYield`
- `ItemsLost`, `MaterialsGained`

---

### 14. Shop & Economy ✅
Buy/sell items with NPCs, dynamic pricing, and merchant inventories.

**Commands:**
- `BuyFromShopCommand` - Purchase item from merchant
- `SellToShopCommand` - Sell item to merchant
- `RefreshMerchantInventoryCommand` - Regenerate shop stock

**Queries:**
- `GetMerchantInventoryQuery` - View merchant's wares
- `GetMerchantInfoQuery` - Merchant details and disposition
- `CheckAffordabilityQuery` - Verify player can afford item
- `GetPriceQuoteQuery` - Get buy/sell prices

**Key Properties:**
- `MerchantId`, `MerchantType`, `ShopInventory`
- `BuyPriceModifier`, `SellPriceModifier`
- `ReputationBonus`, `RefreshInterval`

---

### 15. Quest System ✅
Story quests, side quests, objectives, and rewards.

**Commands:**
- `AcceptQuestCommand` - Add quest to active list
- `AdvanceQuestObjectiveCommand` - Progress quest objective
- `CompleteQuestCommand` - Finalize quest and claim rewards
- `AbandonQuestCommand` - Drop active quest

**Queries:**
- `GetAvailableQuestsQuery` - List quests player can accept
- `GetActiveQuestsQuery` - Show current quests
- `GetCompletedQuestsQuery` - Quest history
- `GetMainQuestChainQuery` - Story progression quests
- `GetQuestDetailsQuery` - Full quest information

**Key Properties:**
- `QuestRef`, `QuestName`, `QuestType` (Main, Side, Daily)
- `Objectives`, `Rewards`, `Requirements`
- `QuestStatus` (Available, Active, Completed, Failed)

---

### 16. Progression System ✅
Level ups, skill trees, ability unlocks, and stat growth.

**Commands:**
- `GainExperienceCommand` - Award XP to character
- `LevelUpCommand` - Increase character level
- `AllocateAttributePointsCommand` - Spend points on attributes
- `RespecCharacterCommand` - Reset character progression

**Queries:**
- `GetCharacterProgressionQuery` - View all progression stats
- `GetNextLevelRequirementQuery` - XP needed for next level
- `GetAvailablePerksQuery` - Unlockable character perks

**Key Properties:**
- `Level`, `Experience`, `NextLevelXP`
- `AttributePoints`, `SkillPoints`, `AbilityPoints`
- `TotalXpEarned`, `DeathCount`, `PlayTime`

---

### 17. Reputation System ✅
Track relationships with factions, guilds, and NPCs.

**Commands:**
- `ModifyReputationCommand` - Change faction standing
- `JoinFactionCommand` - Become faction member
- `LeaveFactionCommand` - Leave faction

**Queries:**
- `GetReputationQuery` - View reputation with faction
- `GetAllFactionsQuery` - List all factions
- `GetFactionBenefitsQuery` - Perks for current standing

**Key Properties:**
- `FactionId`, `ReputationLevel` (Hostile, Neutral, Friendly, etc.)
- `ReputationPoints`, `ReputationTier`
- `FactionBenefits`, `FactionQuests`

---

### 14. Party System
Manage party members, formations, and group actions.

**Commands:**
- `Ad8. Party System ✅mand` - Recruit companion
- `RemovePartyMemberCommand` - Dismiss companion
- `ChangePartyLeaderCommand` - Switch active character
- `SetFormationCommand` - Arrange party positions

**Queries:**
- `GetPartyQuery` - List all party members
- `GetPartyStatsQuery` - Aggregate party statistics
- `GetPartyCapacityQuery` - Max party size

**Key Properties:**
- `PartyMembers` (list), `PartyLeader`, `MaxSize`
- `Formation`, `PartyBonuses`

---

### 15. Exploration System
Map traversal, location discovery, and fast travel.

**Commands:**
- `Mo9. Exploration System ✅- Travel to new area
- `DiscoverLocationCommand` - Unlock map location
- `UnlockFastTravelCommand` - Enable fast travel point
- `SearchAreaCommand` - Find hidden items/secrets

**Queries:**
- `GetCurrentLocationQuery` - Player's current position
- `GetDiscoveredLocationsQuery` - Unlocked map areas
- `GetFastTravelPointsQuery` - Available fast travel destinations

**Key Properties:**
- `LocationId`, `LocationName`, `LocationType`
- `IsDiscovered`, `IsFastTravelPoint`
- `AdjacentLocations`, `RequiredLevel`

---

### 16. Achievement System
Track player accomplishments and milestones.

**Commands:**
- `U20. Achievement System ✅d` - Award achievement
- `TrackProgressCommand` - Update achievement progress

**Queries:**
- `GetUnlockedAchievementsQuery` - Earned achievements
- `GetAchievementProgressQuery` - Progress on specific achievement
- `GetAllAchievementsQuery` - Full achievement list

**Key Properties:**
- `AchievementId`, `AchievementName`, `Description`
- `Progress`, `TotalRequired`, `IsCompleted`
- `Rewards`, `UnlockDate`

---

### 21. Death System ✅
Handle character death, respawn, and penalties.

**Commands:**
- `ProcessDeathCommand` - Handle death consequences
- `RespawnCommand` - Revive character at checkpoint
- `ApplyDeathPenaltyCommand` - XP loss, item drops, etc.

**Queries:**
- `GetDeathPenaltyQuery` - Calculate death consequences
- `GetRespawnLocationQuery` - Determine spawn point

**Key Properties:**
- `IsDead`, `DeathCount`, `LastDeathTime`
- `DeathPenalty` (XP loss, durability loss, etc.)
- `RespawnPoint`, `SoulLocation`

---

### 22. Difficulty System ✅
Adjustable challenge levels with enemy scaling, death penalties, and reward multipliers.

**Commands:**
- `SetDifficultyCommand` - Change difficulty level
- `ApplyDifficultyModifiersCommand` - Apply scaling to enemies

**Queries:**
- `GetDifficultySettingsQuery` - View current difficulty
- `GetAvailableDifficultiesQuery` - List all difficulty modes
- `GetDifficultyModifiersQuery` - View scaling multipliers

**Difficulty Modes:**
- **Casual**: 0.75× enemy strength, no death penalty, story focus
- **Normal**: 1.0× enemy strength, moderate penalties, standard rewards
- **Hard**: 1.5× enemy strength, harsher penalties, +50% rewards
- **Nightmare**: 2.0× enemy strength, permadeath, +100% rewards
- **Apocalypse**: 3.0× enemy strength, permadeath + time pressure, +200% rewards

**Key Properties:**
- `DifficultyLevel`, `EnemyHealthMultiplier`, `EnemyDamageMultiplier`
- `DeathPenaltyType`, `RewardMultiplier`
- `IsPermadeathEnabled`, `TimePressureEnabled`

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
- `CreateBackupCommand` - Backup current save

**Queries:**
- `GetAllSavesQuery` - List all save files
- `GetMostRecentSaveQuery` - Latest save
- `GetSaveDetailsQuery` - Metadata for specific save

**Key Properties:**
- `SaveId`, `SaveName`, `SaveDate`
- `Character` (full state), `Playtime`
- `Location`, `QuestProgress`, `InventorySnapshot`

---

### 25. New Game Plus ✅
Start new playthrough with bonuses and increased difficulty after completing the game.

**Commands:**
- `StartNewGamePlusCommand` - Start NG+ with bonuses
- `IncrementNewGamePlusLevelCommand` - Increase NG+ tier

**Queries:**
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

// Register MediatR
services.AddMediatR(cfg => 
    cfg.RegisterServicesFromAssembly(typeof(AttackEnemyCommand).Assembly));

// Register data services
services.AddSingleton<IDataCache, GameDataCache>(sp => 
    new GameDataCache("path/to/Data/Json", sp.GetRequiredService<ILogger<GameDataCache>>()));
services.AddScoped<ISaveGameService, SaveGameService>();
services.AddScoped<ICharacterRepository, CharacterRepository>();
services.AddScoped<INodeRepository, InMemoryNodeRepository>();

// Register game services
services.AddScoped<IItemGenerator, ItemGenerator>();
services.AddScoped<ILootTableService, LootTableService>();
services.AddScoped<IReferenceResolverService, ReferenceResolverService>();

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
