# Item Generation API

**Feature Module:** ItemGeneration  
**Location:** `RealmEngine.Core/Features/ItemGeneration/`  
**Purpose:** Generate random items with quantity control, budget management, and category filtering

---

## Overview

The Item Generation API provides CQRS commands and queries for generating items in bulk with fine-grained control over:

- **Quantity**: Generate 1 to 1000 items in a single call
- **Category**: Target specific categories or use "random" for variety
- **Budget**: Control item quality through min/max budget ranges
- **Generation Mode**: Simple catalog-based or full budget-based (materials + enchantments)

---

## Commands

### GenerateRandomItemsCommand

Generate random items from any category or across all categories.

**Use Cases:**
- Generate loot drops for chests
- Populate merchant inventories
- Create starter equipment packs
- Procedural reward generation

**Parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Quantity` | `int` | 1 | Number of items to generate (1-1000) |
| `Category` | `string?` | null | Category filter ("weapons/*", "armor", "random", null = all) |
| `MinBudget` | `int` | 10 | Minimum budget per item |
| `MaxBudget` | `int` | 50 | Maximum budget per item |
| `Hydrate` | `bool` | true | Resolve references and populate full details |
| `UseBudgetGeneration` | `bool` | true | Use budget-based generation with materials/enchantments |

**Example:**

```csharp
// Generate 20 random items from all categories
var result = await _mediator.Send(new GenerateRandomItemsCommand
{
    Quantity = 20,
    Category = "random",
    MinBudget = 15,
    MaxBudget = 40,
    UseBudgetGeneration = true
});

if (result.Success)
{
    Console.WriteLine($"Generated {result.ActualQuantity} items from {result.CategoriesUsed.Count} categories");
    foreach (var item in result.Items)
    {
        Console.WriteLine($"- {item.Name} ({item.Rarity})");
    }
}
```

**Result:**

```csharp
public class GenerateRandomItemsResult
{
    public bool Success { get; set; }
    public List<Item> Items { get; set; }
    public int RequestedQuantity { get; set; }
    public int ActualQuantity { get; } // Items.Count
    public string? ErrorMessage { get; set; }
    public List<string> CategoriesUsed { get; set; }
}
```

---

### GenerateItemsByCategoryCommand

Generate items from a specific category with quantity control.

**Use Cases:**
- Generate weapons for a weapon shop
- Create armor sets for a specific tier
- Batch-generate consumables for quests
- Generate specific item types for rewards

**Parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Category` | `string` | required | Item category path (e.g., "weapons/swords") |
| `Quantity` | `int` | 1 | Number of items to generate (1-1000) |
| `ItemName` | `string?` | null | Specific item name (if null, random from category) |
| `MinBudget` | `int` | 10 | Minimum budget per item (if UseBudgetGeneration=true) |
| `MaxBudget` | `int` | 50 | Maximum budget per item (if UseBudgetGeneration=true) |
| `Hydrate` | `bool` | true | Resolve references and populate full details |
| `UseBudgetGeneration` | `bool` | false | Use budget-based generation |

**Example:**

```csharp
// Generate 10 random swords
var result = await _mediator.Send(new GenerateItemsByCategoryCommand
{
    Category = "weapons/swords",
    Quantity = 10,
    UseBudgetGeneration = true,
    MinBudget = 20,
    MaxBudget = 50
});

if (result.Success)
{
    Console.WriteLine($"Generated {result.ActualQuantity} swords");
}
```

**Generate Specific Item (Multiple Copies):**

```csharp
// Generate 5 copies of "Iron Longsword" with random materials
var result = await _mediator.Send(new GenerateItemsByCategoryCommand
{
    Category = "weapons/swords",
    ItemName = "Iron Longsword",
    Quantity = 5,
    UseBudgetGeneration = true,
    MinBudget = 25,
    MaxBudget = 35
});
```

**Result:**

```csharp
public class GenerateItemsByCategoryResult
{
    public bool Success { get; set; }
    public List<Item> Items { get; set; }
    public string Category { get; set; }
    public int RequestedQuantity { get; set; }
    public int ActualQuantity { get; } // Items.Count
    public string? ErrorMessage { get; set; }
}
```

---

## Queries

### GetAvailableItemCategoriesQuery

Retrieve all available item categories with metadata.

**Use Cases:**
- Build dynamic UI category selectors
- Validate category inputs
- Display category statistics
- Filter categories by pattern

**Parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `FilterPattern` | `string?` | null | Filter pattern (e.g., "weapons/*" for all weapons) |
| `IncludeItemCounts` | `bool` | false | Include item count per category |

**Example:**

```csharp
// Get all available categories
var result = await _mediator.Send(new GetAvailableItemCategoriesQuery
{
    IncludeItemCounts = true
});

if (result.Success)
{
    Console.WriteLine($"Found {result.TotalCategories} categories");
    foreach (var category in result.Categories)
    {
        Console.WriteLine($"- {category.DisplayName} ({category.Category}): {category.ItemCount} items");
    }
}
```

**Filter by Pattern:**

```csharp
// Get only weapon categories
var result = await _mediator.Send(new GetAvailableItemCategoriesQuery
{
    FilterPattern = "weapons/*",
    IncludeItemCounts = true
});
```

**Result:**

```csharp
public class GetAvailableItemCategoriesResult
{
    public bool Success { get; set; }
    public List<ItemCategoryInfo> Categories { get; set; }
    public int TotalCategories { get; }
    public string? ErrorMessage { get; set; }
}

public class ItemCategoryInfo
{
    public string Category { get; set; }          // "weapons/swords"
    public string DisplayName { get; set; }       // "Swords"
    public string? ParentCategory { get; set; }   // "weapons"
    public int? ItemCount { get; set; }           // 42 (if IncludeItemCounts=true)
    public bool HasNamesFile { get; set; }        // true (if names.json exists)
}
```

---

## Available Categories

The following categories are supported:

### Weapons
- `weapons/swords`
- `weapons/axes`
- `weapons/maces`
- `weapons/daggers`
- `weapons/staves`
- `weapons/bows`
- `weapons/crossbows`
- `weapons/spears`
- `weapons/fist-weapons`

### Armor
- `armor/light`
- `armor/medium`
- `armor/heavy`
- `armor/shields`

### Accessories
- `accessories/amulets`
- `accessories/rings`
- `accessories/cloaks`
- `accessories/belts`

### Consumables
- `consumables/potions`
- `consumables/food`
- `consumables/scrolls`

---

## Generation Modes

### Simple Catalog-Based Generation

**When to Use:**
- Testing and debugging
- Simple item selection without customization
- Performance-critical scenarios (faster)

**Characteristics:**
- No materials or enchantments applied
- Items use default stats from catalog
- Faster generation
- No budget constraints

**Example:**
```csharp
var result = await _mediator.Send(new GenerateRandomItemsCommand
{
    Quantity = 50,
    UseBudgetGeneration = false  // Simple mode
});
```

---

### Budget-Based Generation

**When to Use:**
- Production gameplay (loot, shops, rewards)
- Items need materials and enchantments
- Quality-based item generation

**Characteristics:**
- Materials baked into item (steel, iron, mithril, etc.)
- Enchantments baked into item (+Fire Damage, +Defense, etc.)
- Gem sockets added (player-customizable post-generation)
- Budget constraints control quality
- Slower generation (more complex)

**Example:**
```csharp
var result = await _mediator.Send(new GenerateRandomItemsCommand
{
    Quantity = 20,
    MinBudget = 25,
    MaxBudget = 60,
    UseBudgetGeneration = true  // Full enhancement system
});
```

**Budget Guidelines:**
- **10-20**: Common items, basic materials
- **20-40**: Uncommon items, decent materials
- **40-60**: Rare items, quality materials
- **60-80**: Epic items, exotic materials
- **80+**: Legendary items, premium materials/enchantments

---

## Integration Examples

### Godot C# Integration

```csharp
public class LootChest : Node3D
{
    private IMediator _mediator;
    
    public override void _Ready()
    {
        _mediator = GetNode<RealmEngineManager>("/root/RealmEngineManager").Mediator;
    }
    
    public async void OnChestOpened()
    {
        // Generate 5-10 random items for chest loot
        var quantity = Random.Shared.Next(5, 11);
        
        var result = await _mediator.Send(new GenerateRandomItemsCommand
        {
            Quantity = quantity,
            Category = "random",
            MinBudget = 15,
            MaxBudget = 45,
            UseBudgetGeneration = true
        });
        
        if (result.Success)
        {
            GD.Print($"Chest contains {result.ActualQuantity} items:");
            foreach (var item in result.Items)
            {
                GD.Print($"  - {item.Name} (Value: {item.Price} gold)");
                SpawnItemPickup(item);
            }
        }
    }
}
```

### Merchant Inventory Generation

```csharp
public class MerchantShop
{
    private IMediator _mediator;
    
    public async Task<List<Item>> GenerateMerchantInventory(string merchantType)
    {
        var items = new List<Item>();
        
        switch (merchantType)
        {
            case "Blacksmith":
                // Generate 10 weapons + 10 armor pieces
                var weapons = await _mediator.Send(new GenerateItemsByCategoryCommand
                {
                    Category = "weapons/*",
                    Quantity = 10,
                    UseBudgetGeneration = true,
                    MinBudget = 20,
                    MaxBudget = 60
                });
                
                var armor = await _mediator.Send(new GenerateRandomItemsCommand
                {
                    Quantity = 10,
                    Category = "armor",
                    MinBudget = 25,
                    MaxBudget = 55,
                    UseBudgetGeneration = true
                });
                
                items.AddRange(weapons.Items);
                items.AddRange(armor.Items);
                break;
                
            case "Alchemist":
                // Generate 20 potions and scrolls
                var consumables = await _mediator.Send(new GenerateRandomItemsCommand
                {
                    Quantity = 20,
                    Category = "consumables",
                    MinBudget = 10,
                    MaxBudget = 30,
                    UseBudgetGeneration = false // Simple potions
                });
                
                items.AddRange(consumables.Items);
                break;
        }
        
        return items;
    }
}
```

### Quest Reward Generation

```csharp
public class QuestRewardService
{
    private IMediator _mediator;
    
    public async Task<List<Item>> GenerateQuestReward(int questLevel, string rewardType)
    {
        // Scale budget with quest level
        var minBudget = 10 + (questLevel * 5);
        var maxBudget = 30 + (questLevel * 10);
        
        var result = await _mediator.Send(new GenerateRandomItemsCommand
        {
            Quantity = rewardType == "major" ? 3 : 1,
            Category = "random",
            MinBudget = minBudget,
            MaxBudget = maxBudget,
            UseBudgetGeneration = true
        });
        
        return result.Items;
    }
}
```

---

## Error Handling

All commands/queries return a `Success` boolean and optional `ErrorMessage`:

```csharp
var result = await _mediator.Send(new GenerateRandomItemsCommand
{
    Quantity = 10
});

if (!result.Success)
{
    _logger.LogError("Item generation failed: {Error}", result.ErrorMessage);
    // Handle error (e.g., show message to player, use fallback items)
}
else
{
    // Use generated items
    ProcessItems(result.Items);
}
```

**Common Errors:**
- `"Quantity must be greater than 0"` - Invalid quantity
- `"Quantity must not exceed 1000"` - Too many items requested
- `"No valid categories found for 'xyz'"` - Invalid category filter
- `"Category is required"` - Missing required category parameter

---

## Performance Considerations

### Quantity Limits
- **Maximum**: 1000 items per request (enforced)
- **Recommended**: 10-100 items per call for responsive UI
- **Batch Processing**: For >100 items, consider splitting into multiple calls

### Generation Speed
- **Simple Mode**: ~0.5-1ms per item (fast)
- **Budget Mode**: ~2-5ms per item (slower, but richer items)

### Memory Usage
- Each item: ~2-5KB in memory
- 100 items: ~500KB
- 1000 items: ~5MB

### Optimization Tips
1. Use `Hydrate = false` if full item details aren't needed immediately
2. Use simple mode (`UseBudgetGeneration = false`) for large batches
3. Cache generated items for merchants/loot tables
4. Generate items asynchronously to avoid blocking UI

---

## Testing

```csharp
[Fact]
public async Task GenerateRandomItems_Should_Return_Requested_Quantity()
{
    // Arrange
    var command = new GenerateRandomItemsCommand
    {
        Quantity = 20,
        Category = "weapons/swords",
        UseBudgetGeneration = false // Faster for tests
    };
    
    // Act
    var result = await _mediator.Send(command);
    
    // Assert
    result.Success.Should().BeTrue();
    result.ActualQuantity.Should().Be(20);
    result.Items.Should().AllBeOfType<Item>();
}
```

---

## Migration Guide

### Old API (ItemGenerator Direct Usage)

```csharp
// OLD: Direct ItemGenerator usage
var items = await _itemGenerator.GenerateItemsAsync("weapons/swords", 10);
```

### New API (MediatR Commands)

```csharp
// NEW: CQRS command via MediatR
var result = await _mediator.Send(new GenerateItemsByCategoryCommand
{
    Category = "weapons/swords",
    Quantity = 10
});

var items = result.Items;
```

**Benefits of New API:**
- Consistent error handling
- Better logging and metrics
- Quantity control (1-1000)
- Budget management
- Category filtering
- Testability via MediatR

---

## Future Enhancements

**Planned Features:**
- Item generation by level range
- Item generation by rarity tier
- Bulk generation with progress callbacks
- Item set generation (matching items)
- Weighted category selection
- Item generation profiles (presets)

---

**Questions or Issues?**  
See [API_SPECIFICATION.md](../../../docs/API_SPECIFICATION.md) for complete API documentation.
