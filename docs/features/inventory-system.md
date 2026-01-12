# Inventory System

**Status**: ✅ 100% Complete (with Item Stacking) (See [COMPLETED_WORK.md](../COMPLETED_WORK.md))

## Overview

The Inventory System manages item storage, equipment, consumables, and procedural item generation with rarity tiers and trait systems.

## Core Components

### Item Management
- **Storage**: Limited inventory capacity (20 slots)
- **Equipment Slots**: Weapon, armor, shield, accessory
- **Sorting**: Organize by name, type, or rarity

### Item Categories
- **Weapons**: Melee and ranged with varying damage types
- **Armor**: Light to heavy protective gear
- **Shields**: Defensive equipment increasing block chance
- **Consumables**: Potions and scrolls for healing/buffs
- **Accessories**: Rings, amulets providing passive bonuses

### Rarity System
Five tiers: Common, Uncommon, Rare, Epic, Legendary affecting stats and drop rates.

### Trait System
Items possess traits providing offensive, defensive, or utility bonuses with procedural trait assignment.

### Procedural Generation
- **Name Generation**: Composable patterns for unlimited variety
- **Stat Generation**: Scales with rarity and level
- **Trait Assignment**: Based on item type and rarity pools

## Key Features

- **Capacity Management**: Strategic choices about what to carry
- **Equipment Optimization**: 13 equipment slots for build customization
- **Procedural Variety**: Virtually unlimited item combinations
- **Data-Driven**: JSON v4.0 specification for content expansion
- **Query APIs**: Complete MediatR-based API layer for UI integration

## Query APIs (MediatR)

### GetPlayerInventoryQuery
Retrieves player inventory with filtering, sorting, and summary statistics.

**Parameters:**
- `ItemTypeFilter` (string?) - Filter by item type (e.g., "Weapon", "Helmet")
- `RarityFilter` (string?) - Filter by rarity (e.g., "Legendary", "Common")
- `MinValue` (int?) - Minimum item value filter
- `MaxValue` (int?) - Maximum item value filter
- `SortBy` (string?) - Sort field ("name", "value", "rarity", "type")
- `SortDescending` (bool) - Sort direction

**Returns:**
- `List<InventoryItemInfo>` - Filtered/sorted items with equipped status
- `InventorySummary` - Aggregate statistics
  - TotalItems, UniqueItems, TotalValue, EquippedItems
  - ItemsByType (Dictionary<string, int>)
  - ItemsByRarity (Dictionary<string, int>)

### GetEquippedItemsQuery
Retrieves complete equipment loadout with aggregated statistics.

**Returns:**
- `EquipmentLoadout` - All 13 equipment slots
  - MainHand, OffHand, Helmet, Shoulders, Chest, Bracers, Gloves, Belt, Legs, Boots, Necklace, Ring1, Ring2
- `EquipmentStats` - Aggregated bonuses
  - TotalValue, TotalAttackBonus, TotalDefenseBonus
  - TotalSockets, FilledSockets
  - SetBonuses (Dictionary<string, int>)

### CheckItemEquippedQuery
Checks if a specific item is equipped and in which slot.

**Parameters:**
- `ItemId` (string) - Item identifier to check

**Returns:**
- `IsEquipped` (bool) - Whether item is equipped
- `EquipSlot` (string?) - Slot name if equipped (e.g., "MainHand", "Ring1")

### GetInventoryValueQuery
Calculates total inventory value with wealth categorization.

**Parameters:**
- `IncludeEquipped` (bool) - Whether to include equipped items (default: true)

**Returns:**
- `TotalValue` (int) - Combined value of all items
- `EquippedValue` (int) - Value of equipped items
- `UnequippedValue` (int) - Value of unequipped items
- `MostValuableItemPrice` (int) - Highest item value
- `MostValuableItemName` (string) - Name of most valuable item
- `WealthCategory` (enum) - Pauper, Common, Comfortable, Wealthy, Rich, Noble

## Usage Example

```csharp
// Get filtered inventory
var query = new GetPlayerInventoryQuery 
{ 
    ItemTypeFilter = "Weapon",
    RarityFilter = "Legendary",
    SortBy = "value",
    SortDescending = true
};
var result = await mediator.Send(query);

if (result.Success)
{
    foreach (var item in result.Items)
    {
        Console.WriteLine($"{item.Name} - {item.Value}g {(item.IsEquipped ? "[EQUIPPED]" : "")}");
    }
    Console.WriteLine($"Total legendary weapons: {result.Items.Count}");
    Console.WriteLine($"Combined value: {result.Items.Sum(i => i.TotalValue)}g");
}

// Get equipment stats
var equipQuery = new GetEquippedItemsQuery();
var equipResult = await mediator.Send(equipQuery);

if (equipResult.Success)
{
    Console.WriteLine($"Total equipped: {equipResult.Stats.TotalEquippedItems}");
    Console.WriteLine($"Attack bonus: +{equipResult.Stats.TotalAttackBonus}");
    Console.WriteLine($"Defense bonus: +{equipResult.Stats.TotalDefenseBonus}");
    Console.WriteLine($"Equipment value: {equipResult.Stats.TotalValue}g");
}

// Check wealth category
var valueQuery = new GetInventoryValueQuery();
var valueResult = await mediator.Send(valueQuery);

if (valueResult.Success)
{
    Console.WriteLine($"Total wealth: {valueResult.TotalValue}g");
    Console.WriteLine($"Status: {valueResult.WealthCategory}");
    Console.WriteLine($"Most valuable: {valueResult.MostValuableItemName} ({valueResult.MostValuableItemPrice}g)");
}
```

## Related Systems

- [Combat System](combat-system.md) - Equipment affects combat effectiveness
- [Crafting System](crafting-system.md) - Materials and crafted items
- [Exploration System](exploration-system.md) - Loot distribution by location
