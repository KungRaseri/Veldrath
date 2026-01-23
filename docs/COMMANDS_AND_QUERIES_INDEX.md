# RealmEngine Commands & Queries Reference

**Complete index of all MediatR commands and queries in RealmEngine**  
**Version:** 0.1.652.0  
**Last Updated:** January 23, 2026

---

## Quick Reference

| Feature | Commands | Queries | Total |
|---------|----------|---------|-------|
| Combat | 7 | 2 | 9 |
| Character Creation | 3 | 2 | 5 |
| Abilities | 2 | 2 | 4 |
| Spells | 2 | 4 | 6 |
| Skills | 2 | 2 | 4 |
| Inventory | 5 | 3 | 8 |
| Equipment | 1 | 1 | 2 |
| Crafting | 3 | 1 | 4 |
| Enchanting | 3 | 0 | 3 |
| Socketing | 3 | 4 | 7 |
| Upgrading | 1 | 0 | 1 |
| Salvaging | 1 | 0 | 1 |
| Shop | 4 | 2 | 6 |
| Quest | 4 | 4 | 8 |
| Exploration | 8 | 5 | 13 |
| Harvesting | 1 | 2 | 3 |
| Party | 3 | 1 | 4 |
| Reputation | 2 | 1 | 3 |
| Achievement | 2 | 1 | 3 |
| Death | 1 | 0 | 1 |
| Victory | 2 | 0 | 2 |
| SaveLoad | 3 | 2 | 5 |
| Item Generation | 2 | 1 | 3 |
| **TOTAL** | **60** | **33** | **93** |

---

## 1. Combat Commands & Queries

### Commands

```csharp
using RealmEngine.Core.Features.Combat.Commands.*;

// Attack enemy with ability
await mediator.Send(new AttackEnemyCommand
{
    CharacterName = "Player",
    EnemyId = "goblin-001",
    AbilityRef = "@abilities/active/offensive:basic-attack"
});

// Defend to reduce damage
await mediator.Send(new DefendActionCommand
{
    CharacterName = "Player"
});

// Flee from combat
await mediator.Send(new FleeFromCombatCommand
{
    CharacterName = "Player"
});

// Use item in combat (potion, scroll, etc.)
await mediator.Send(new UseCombatItemCommand
{
    CharacterName = "Player",
    ItemId = "healing-potion-001"
});

// Apply status effect (poison, burn, etc.)
await mediator.Send(new ApplyStatusEffectCommand
{
    TargetId = "enemy-001",
    EffectType = "Poison",
    Duration = 3,
    DamagePerTick = 5
});

// Process all status effects (tick damage, durations)
await mediator.Send(new ProcessStatusEffectsCommand
{
    CharacterName = "Player"
});

// Start boss encounter
await mediator.Send(new EncounterBossCommand
{
    BossId = "dragon-lord",
    LocationId = "dragon-lair"
});
```

### Queries

```csharp
using RealmEngine.Core.Features.Combat.Queries.*;

// Get current combat state
var state = await mediator.Send(new GetCombatStateQuery
{
    CharacterName = "Player"
});

// Get enemy information
var enemy = await mediator.Send(new GetEnemyInfoQuery
{
    EnemyId = "goblin-001"
});
```

---

## 2. Character Creation Commands & Queries

### Commands

```csharp
using RealmEngine.Core.Features.CharacterCreation.Commands.*;

// Create new character
await mediator.Send(new CreateCharacterCommand
{
    CharacterName = "Aragorn",
    ClassName = "Fighter",
    Attributes = new Dictionary<string, int>
    {
        ["Strength"] = 16,
        ["Dexterity"] = 14,
        ["Constitution"] = 15,
        ["Intelligence"] = 10,
        ["Wisdom"] = 12,
        ["Charisma"] = 13
    }
});

// Set starting abilities
await mediator.Send(new SetStartingAbilitiesCommand
{
    CharacterName = "Aragorn",
    AbilityRefs = new List<string>
    {
        "@abilities/active/offensive:basic-attack",
        "@abilities/passive/general:weapon-proficiency"
    }
});

// Set starting spells (for spellcasters)
await mediator.Send(new SetStartingSpellsCommand
{
    CharacterName = "Gandalf",
    SpellRefs = new List<string>
    {
        "@spells/arcane:magic-missile",
        "@spells/arcane:shield"
    }
});
```

### Queries

```csharp
using RealmEngine.Core.Features.CharacterCreation.Queries.*;

// Get all available classes
var classes = await mediator.Send(new GetCharacterClassesQuery());

// Get specific class details
var fighter = await mediator.Send(new GetCharacterClassQuery
{
    ClassName = "Fighter"
});
```

---

## 3. Abilities Commands & Queries

### Commands

```csharp
using RealmEngine.Core.Features.Progression.Commands.*;

// Learn new ability
await mediator.Send(new LearnAbilityCommand
{
    CharacterName = "Player",
    AbilityRef = "@abilities/active/offensive:power-strike"
});

// Use ability
await mediator.Send(new UseAbilityCommand
{
    CharacterName = "Player",
    AbilityRef = "@abilities/active/offensive:power-strike",
    TargetId = "enemy-001"
});
```

### Queries

```csharp
using RealmEngine.Core.Features.Progression.Queries.*;

// Get abilities character can learn
var available = await mediator.Send(new GetAvailableAbilitiesQuery
{
    CharacterName = "Player"
});
```

---

## 4. Spells Commands & Queries

### Commands

```csharp
using RealmEngine.Core.Features.Progression.Commands.*;

// Learn spell
await mediator.Send(new LearnSpellCommand
{
    CharacterName = "Wizard",
    SpellRef = "@spells/arcane:fireball"
});

// Cast spell
await mediator.Send(new CastSpellCommand
{
    CharacterName = "Wizard",
    SpellRef = "@spells/arcane:fireball",
    TargetId = "enemy-001"
});
```

### Queries

```csharp
using RealmEngine.Core.Features.Progression.Queries.*;

// Get spells available for current level
var learnable = await mediator.Send(new GetLearnableSpellsQuery
{
    CharacterName = "Wizard"
});
```

---

## 5. Skills Commands & Queries

### Commands

```csharp
using RealmEngine.Core.Features.Progression.Commands.*;

// Award skill XP
await mediator.Send(new AwardSkillXPCommand
{
    CharacterName = "Player",
    SkillName = "Swordsmanship",
    XP = 50
});

// Set starting skills
await mediator.Send(new SetStartingSkillsCommand
{
    CharacterName = "Player",
    Skills = new Dictionary<string, int>
    {
        ["Swordsmanship"] = 10,
        ["Archery"] = 5,
        ["Stealth"] = 8
    }
});
```

### Queries

```csharp
using RealmEngine.Core.Features.Progression.Queries.*;

// Get skill progress
var skill = await mediator.Send(new GetSkillProgressQuery
{
    CharacterName = "Player",
    SkillName = "Swordsmanship"
});

// Get all skills
var allSkills = await mediator.Send(new GetAllSkillsProgressQuery
{
    CharacterName = "Player"
});
```

---

## 6. Inventory Commands & Queries

### Commands

```csharp
using RealmEngine.Core.Features.Inventory.Commands.*;

// Drop item
await mediator.Send(new DropItemCommand
{
    CharacterName = "Player",
    ItemId = "old-sword-001"
});

// Sort inventory
await mediator.Send(new SortInventoryCommand
{
    CharacterName = "Player",
    SortBy = "Rarity" // or "Type", "Name", "Value"
});

// Use item (consumable)
await mediator.Send(new UseItemCommand
{
    CharacterName = "Player",
    ItemId = "healing-potion-001"
});

// Equip item
await mediator.Send(new EquipItemCommand
{
    CharacterName = "Player",
    ItemId = "iron-sword-001"
});

// Unequip item
await mediator.Send(new UnequipItemCommand
{
    CharacterName = "Player",
    SlotName = "MainHand"
});
```

### Queries

```csharp
using RealmEngine.Core.Features.Inventory.Queries.*;

// Get full inventory
var inventory = await mediator.Send(new GetInventoryQuery
{
    CharacterName = "Player"
});

// Get equipped items
var equipped = await mediator.Send(new GetEquippedItemsQuery
{
    CharacterName = "Player"
});

// Get item information
var item = await mediator.Send(new GetItemInfoQuery
{
    ItemId = "iron-sword-001"
});
```

---

## 7. Equipment Commands & Queries

```csharp
using RealmEngine.Core.Features.Equipment.Commands.*;
using RealmEngine.Core.Features.Equipment.Queries.*;

// Equip item (Equipment folder version)
await mediator.Send(new EquipItemCommand
{
    CharacterName = "Player",
    ItemId = "iron-sword-001"
});

// Get equipped gear (if separate from inventory query)
var gear = await mediator.Send(new GetEquippedGearQuery
{
    CharacterName = "Player"
});
```

---

## 8. Crafting Commands & Queries

### Commands

```csharp
using RealmEngine.Core.Features.Crafting.Commands.*;

// Craft item from recipe
await mediator.Send(new CraftItemCommand
{
    CharacterName = "Player",
    RecipeId = "iron-sword-recipe"
});

// Learn recipe
await mediator.Send(new LearnRecipeCommand
{
    CharacterName = "Player",
    RecipeId = "steel-sword-recipe"
});

// Discover recipe (through gameplay)
await mediator.Send(new DiscoverRecipeCommand
{
    CharacterName = "Player",
    RecipeId = "masterwork-sword-recipe"
});
```

### Queries

```csharp
using RealmEngine.Core.Features.Crafting.Queries.*;

// Get known recipes
var recipes = await mediator.Send(new GetKnownRecipesQuery
{
    CharacterName = "Player"
});
```

---

## 9. Enchanting Commands

```csharp
using RealmEngine.Core.Features.Enchanting.Commands.*;

// Apply enchantment to item
await mediator.Send(new ApplyEnchantmentCommand
{
    CharacterName = "Player",
    ItemId = "iron-sword-001",
    EnchantmentRef = "@enchantments:fire-damage"
});

// Remove enchantment
await mediator.Send(new RemoveEnchantmentCommand
{
    CharacterName = "Player",
    ItemId = "iron-sword-001",
    EnchantmentSlot = 0
});

// Add enchantment slot to item
await mediator.Send(new AddEnchantmentSlotCommand
{
    CharacterName = "Player",
    ItemId = "iron-sword-001"
});
```

---

## 10. Socketing Commands & Queries

### Commands

```csharp
using RealmEngine.Core.Features.Socketing.Commands.*;

// Socket gem into item
await mediator.Send(new SocketItemCommand
{
    CharacterName = "Player",
    ItemId = "iron-sword-001",
    SocketableItemId = "ruby-gem-001",
    SocketIndex = 0
});

// Remove socketed gem
await mediator.Send(new RemoveSocketedItemCommand
{
    CharacterName = "Player",
    ItemId = "iron-sword-001",
    SocketIndex = 0
});

// Socket multiple gems
await mediator.Send(new SocketMultipleItemsCommand
{
    CharacterName = "Player",
    Operations = new List<SocketOperation>
    {
        new() { ItemId = "sword-001", SocketableId = "ruby-001", Index = 0 },
        new() { ItemId = "armor-001", SocketableId = "sapphire-001", Index = 0 }
    }
});
```

### Queries

```csharp
using RealmEngine.Core.Features.Socketing.Queries.*;

// Get compatible socketables for item
var compatible = await mediator.Send(new GetCompatibleSocketablesQuery
{
    ItemId = "iron-sword-001"
});

// Get socket info for item
var sockets = await mediator.Send(new GetSocketInfoQuery
{
    ItemId = "iron-sword-001"
});

// Preview socketing effect
var preview = await mediator.Send(new SocketPreviewQuery
{
    ItemId = "iron-sword-001",
    SocketableItemId = "ruby-gem-001",
    SocketIndex = 0
});

// Get socket operation cost
var cost = await mediator.Send(new GetSocketCostQuery
{
    Operation = "Insert", // or "Remove"
    ItemId = "iron-sword-001"
});
```

---

## 11. Upgrading Commands

```csharp
using RealmEngine.Core.Features.Upgrading.Commands.*;

// Upgrade item to next tier (+1, +2, etc.)
await mediator.Send(new UpgradeItemCommand
{
    CharacterName = "Player",
    ItemId = "iron-sword-001"
});
```

---

## 12. Salvaging Commands

```csharp
using RealmEngine.Core.Features.Salvaging.Commands.*;

// Salvage item into materials
await mediator.Send(new SalvageItemCommand
{
    CharacterName = "Player",
    ItemId = "old-sword-001"
});
```

---

## 13. Shop Commands & Queries

### Commands

```csharp
using RealmEngine.Core.Features.Shop.Commands.*;

// Buy item from merchant
await mediator.Send(new BuyFromShopCommand
{
    CharacterName = "Player",
    MerchantId = "blacksmith-001",
    ItemId = "iron-sword-001"
});

// Sell item to merchant
await mediator.Send(new SellToShopCommand
{
    CharacterName = "Player",
    MerchantId = "blacksmith-001",
    ItemId = "old-sword-001"
});

// Browse shop (get inventory)
await mediator.Send(new BrowseShopCommand
{
    MerchantId = "blacksmith-001"
});

// Refresh merchant inventory
await mediator.Send(new RefreshMerchantInventoryCommand
{
    MerchantId = "blacksmith-001"
});
```

### Queries

```csharp
using RealmEngine.Core.Features.Shop.Queries.*;

// Get merchant information
var merchant = await mediator.Send(new GetMerchantInfoQuery
{
    MerchantId = "blacksmith-001"
});

// Check if player can afford item
var canAfford = await mediator.Send(new CheckAffordabilityQuery
{
    CharacterName = "Player",
    ItemPrice = 500
});
```

---

## 14. Quest Commands & Queries

### Commands

```csharp
using RealmEngine.Core.Features.Quest.Commands.*;

// Start quest
await mediator.Send(new StartQuestCommand
{
    CharacterName = "Player",
    QuestId = "slay-goblins"
});

// Update quest progress
await mediator.Send(new UpdateQuestProgressCommand
{
    CharacterName = "Player",
    QuestId = "slay-goblins",
    ObjectiveId = "kill-10-goblins",
    Progress = 5
});

// Complete quest
await mediator.Send(new CompleteQuestCommand
{
    CharacterName = "Player",
    QuestId = "slay-goblins"
});

// Set starting quests
await mediator.Send(new SetStartingQuestsCommand
{
    CharacterName = "Player",
    QuestIds = new List<string> { "tutorial-quest" }
});
```

### Queries

```csharp
using RealmEngine.Core.Features.Quest.Queries.*;

// Get available quests
var available = await mediator.Send(new GetAvailableQuestsQuery
{
    CharacterName = "Player"
});

// Get active quests
var active = await mediator.Send(new GetActiveQuestsQuery
{
    CharacterName = "Player"
});

// Get completed quests
var completed = await mediator.Send(new GetCompletedQuestsQuery
{
    CharacterName = "Player"
});

// Get main quest chain
var mainQuests = await mediator.Send(new GetMainQuestChainQuery
{
    CharacterName = "Player"
});
```

---

## 15. Exploration Commands & Queries

### Commands

```csharp
using RealmEngine.Core.Features.Exploration.Commands.*;

// Travel to location
await mediator.Send(new TravelToLocationCommand
{
    CharacterName = "Player",
    LocationId = "forest-clearing"
});

// Explore current location
await mediator.Send(new ExploreLocationCommand
{
    CharacterName = "Player"
});

// Rest at location
await mediator.Send(new RestCommand
{
    CharacterName = "Player",
    Hours = 8
});

// Rest at inn (paid)
await mediator.Send(new RestAtInnCommand
{
    CharacterName = "Player",
    InnQuality = "Standard" // or "Poor", "Luxury"
});

// Enter shop
await mediator.Send(new EnterShopLocationCommand
{
    CharacterName = "Player",
    LocationId = "blacksmith-shop"
});

// Encounter NPC
await mediator.Send(new EncounterNPCCommand
{
    CharacterName = "Player",
    NPCId = "mysterious-stranger"
});

// Generate enemy for location
await mediator.Send(new GenerateEnemyForLocationCommand
{
    LocationId = "dark-forest",
    EnemyLevel = 5
});
```

### Queries

```csharp
using RealmEngine.Core.Features.Exploration.Queries.*;

// Get current location
var location = await mediator.Send(new GetCurrentLocationQuery
{
    CharacterName = "Player"
});

// Get known locations
var locations = await mediator.Send(new GetKnownLocationsQuery
{
    CharacterName = "Player"
});

// Get location details
var info = await mediator.Send(new GetLocationInfoQuery
{
    LocationId = "forest-clearing"
});

// Get NPCs at location
var npcs = await mediator.Send(new GetNPCsAtLocationQuery
{
    LocationId = "town-square"
});

// Get spawn information
var spawns = await mediator.Send(new GetLocationSpawnInfoQuery
{
    LocationId = "goblin-camp"
});
```

---

## 16. Harvesting Commands & Queries

### Commands

```csharp
using RealmEngine.Core.Features.Harvesting.Commands.*;

// Harvest node
await mediator.Send(new HarvestNodeCommand
{
    CharacterName = "Player",
    NodeId = "iron-ore-vein-001"
});
```

### Queries

```csharp
using RealmEngine.Core.Features.Harvesting.Queries.*;

// Get harvestable nodes nearby
var nodes = await mediator.Send(new GetHarvestableNodesQuery
{
    LocationId = "mining-area",
    Radius = 100
});

// Inspect node
var nodeInfo = await mediator.Send(new InspectNodeQuery
{
    NodeId = "iron-ore-vein-001"
});
```

---

## 17. Party Commands & Queries

### Commands

```csharp
using RealmEngine.Core.Features.Party.Commands.*;

// Recruit NPC to party
await mediator.Send(new RecruitNPCCommand
{
    CharacterName = "Player",
    NPCId = "companion-warrior"
});

// Dismiss party member
await mediator.Send(new DismissPartyMemberCommand
{
    CharacterName = "Player",
    PartyMemberId = "companion-warrior"
});

// Handle party turn (combat)
await mediator.Send(new HandlePartyTurnCommand
{
    PartyId = "player-party"
});
```

### Queries

```csharp
using RealmEngine.Core.Features.Party.Queries.*;

// Get party members
var party = await mediator.Send(new GetPartyQuery
{
    CharacterName = "Player"
});
```

---

## 18. Reputation Commands & Queries

### Commands

```csharp
using RealmEngine.Core.Features.Reputation.Commands.*;

// Gain reputation
await mediator.Send(new GainReputationCommand
{
    CharacterName = "Player",
    FactionId = "thieves-guild",
    Amount = 50
});

// Lose reputation
await mediator.Send(new LoseReputationCommand
{
    CharacterName = "Player",
    FactionId = "city-guard",
    Amount = 25
});
```

### Queries

```csharp
using RealmEngine.Core.Features.Reputation.Queries.*;

// Get reputation with faction
var rep = await mediator.Send(new GetReputationQuery
{
    CharacterName = "Player",
    FactionId = "thieves-guild"
});
```

---

## 19. Achievement Commands & Queries

### Commands

```csharp
using RealmEngine.Core.Features.Achievement.Commands.*;

// Unlock achievement
await mediator.Send(new UnlockAchievementCommand
{
    CharacterName = "Player",
    AchievementId = "first-quest-complete"
});

// Check achievement progress
await mediator.Send(new CheckAchievementProgressCommand
{
    CharacterName = "Player",
    AchievementId = "kill-100-goblins"
});
```

### Queries

```csharp
using RealmEngine.Core.Features.Achievement.Queries.*;

// Get unlocked achievements
var achievements = await mediator.Send(new GetUnlockedAchievementsQuery
{
    CharacterName = "Player"
});
```

---

## 20. Death Commands

```csharp
using RealmEngine.Core.Features.Death.Commands.*;

// Handle player death
await mediator.Send(new HandlePlayerDeathCommand
{
    CharacterName = "Player",
    KilledBy = "dragon-001"
});
```

---

## 21. Victory Commands

```csharp
using RealmEngine.Core.Features.Victory.Commands.*;

// Trigger victory (game completion)
await mediator.Send(new TriggerVictoryCommand
{
    CharacterName = "Player"
});

// Start New Game Plus
await mediator.Send(new StartNewGamePlusCommand
{
    CharacterName = "Player",
    NewGamePlusLevel = 1
});
```

---

## 22. SaveLoad Commands & Queries

### Commands

```csharp
using RealmEngine.Core.Features.SaveLoad.Commands.*;

// Save game
await mediator.Send(new SaveGameCommand
{
    CharacterName = "Player",
    SaveName = "Before Dragon Fight"
});

// Load game
await mediator.Send(new LoadGameCommand
{
    SaveId = "save-001"
});

// Delete save
await mediator.Send(new DeleteSaveCommand
{
    SaveId = "save-001"
});
```

### Queries

```csharp
using RealmEngine.Core.Features.SaveLoad.Queries.*;

// Get all saves
var saves = await mediator.Send(new GetAllSavesQuery());

// Get most recent save
var recent = await mediator.Send(new GetMostRecentSaveQuery());
```

---

## 23. Item Generation Commands & Queries

### Commands

```csharp
using RealmEngine.Core.Features.ItemGeneration.Commands.*;

// Generate random items
var result = await mediator.Send(new GenerateRandomItemsCommand
{
    Quantity = 20,
    Category = "random", // or "weapons/*", "armor", etc.
    MinBudget = 15,
    MaxBudget = 45,
    UseBudgetGeneration = true
});

// Generate items by category
var items = await mediator.Send(new GenerateItemsByCategoryCommand
{
    Category = "weapons/swords",
    Quantity = 10,
    MinBudget = 20,
    MaxBudget = 50,
    UseBudgetGeneration = true
});
```

### Queries

```csharp
using RealmEngine.Core.Features.ItemGeneration.Queries.*;

// Get available item categories
var categories = await mediator.Send(new GetAvailableItemCategoriesQuery
{
    FilterPattern = "weapons/*", // optional
    IncludeItemCounts = true
});
```

---

## Result Pattern

All commands and queries return a Result object:

```csharp
public class SomeResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    // ... additional properties
}
```

### Example Usage

```csharp
var result = await mediator.Send(new AttackEnemyCommand { ... });

if (result.Success)
{
    GD.Print($"Attack hit for {result.Damage} damage!");
    // Process success
}
else
{
    GD.PrintErr($"Attack failed: {result.ErrorMessage}");
    // Handle error
}
```

---

## Service Registration

All handlers are auto-registered with MediatR:

```csharp
services.AddMediatR(cfg => 
    cfg.RegisterServicesFromAssembly(typeof(AttackEnemyCommand).Assembly));
```

---

**Total Available:** 60 Commands, 33 Queries = 93 handlers  
**Status:** ✅ All functional and tested  
**Documentation:** See [API_SPECIFICATION.md](API_SPECIFICATION.md) for detailed API reference
