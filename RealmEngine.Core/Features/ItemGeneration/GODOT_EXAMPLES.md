# Item Generation - Godot C# Integration Examples

This file shows how to use the new Item Generation API from Godot C#.

---

## Example 1: Generate Random Loot (20 Items)

```csharp
using Godot;
using MediatR;
using RealmEngine.Core.Features.ItemGeneration.Commands;

public partial class LootChest : Node3D
{
    private IMediator _mediator;

    public override void _Ready()
    {
        // Get RealmEngine mediator from singleton
        var realmEngine = GetNode<RealmEngineManager>("/root/RealmEngineManager");
        _mediator = realmEngine.Mediator;
    }

    public async void OnChestOpened()
    {
        GD.Print("Opening chest...");

        // Generate 20 random items
        var result = await _mediator.Send(new GenerateRandomItemsCommand
        {
            Quantity = 20,
            Category = "random",  // or null for all categories
            MinBudget = 15,
            MaxBudget = 45,
            UseBudgetGeneration = true  // Full materials/enchantments
        });

        if (result.Success)
        {
            GD.Print($"✅ Generated {result.ActualQuantity} items:");
            
            foreach (var item in result.Items)
            {
                GD.Print($"  - {item.Name} ({item.Rarity}) - {item.Price}g");
                
                // Spawn item in world
                SpawnItemPickup(item);
            }
        }
        else
        {
            GD.PrintErr($"❌ Failed to generate items: {result.ErrorMessage}");
        }
    }

    private void SpawnItemPickup(Item item)
    {
        // Your item spawning logic here
        // e.g., instantiate ItemPickup scene and set item data
    }
}
```

---

## Example 2: Generate Specific Category (Weapons Only)

```csharp
using Godot;
using MediatR;
using RealmEngine.Core.Features.ItemGeneration.Commands;

public partial class WeaponShop : Node
{
    private IMediator _mediator;
    private List<Item> _inventory = new();

    public override void _Ready()
    {
        var realmEngine = GetNode<RealmEngineManager>("/root/RealmEngineManager");
        _mediator = realmEngine.Mediator;
        
        // Generate shop inventory when ready
        GenerateInventory();
    }

    private async void GenerateInventory()
    {
        GD.Print("Generating weapon shop inventory...");

        // Generate 15 swords
        var swords = await _mediator.Send(new GenerateItemsByCategoryCommand
        {
            Category = "weapons/swords",
            Quantity = 15,
            MinBudget = 20,
            MaxBudget = 60,
            UseBudgetGeneration = true
        });

        // Generate 10 bows
        var bows = await _mediator.Send(new GenerateItemsByCategoryCommand
        {
            Category = "weapons/bows",
            Quantity = 10,
            MinBudget = 25,
            MaxBudget = 55,
            UseBudgetGeneration = true
        });

        if (swords.Success && bows.Success)
        {
            _inventory.AddRange(swords.Items);
            _inventory.AddRange(bows.Items);
            
            GD.Print($"✅ Shop stocked with {_inventory.Count} weapons");
            UpdateShopUI();
        }
        else
        {
            GD.PrintErr("❌ Failed to generate shop inventory");
        }
    }

    private void UpdateShopUI()
    {
        // Update your shop UI with _inventory items
    }
}
```

---

## Example 3: Simple Generation (No Materials/Enchantments)

```csharp
using Godot;
using MediatR;
using RealmEngine.Core.Features.ItemGeneration.Commands;

public partial class QuickLoot : Node
{
    public async Task<List<Item>> GenerateSimpleLoot(IMediator mediator, int quantity)
    {
        var result = await mediator.Send(new GenerateRandomItemsCommand
        {
            Quantity = quantity,
            Category = "random",
            UseBudgetGeneration = false,  // Simple mode - faster, no materials
            Hydrate = true
        });

        return result.Success ? result.Items : new List<Item>();
    }
}
```

---

## Example 4: Query Available Categories

```csharp
using Godot;
using MediatR;
using RealmEngine.Core.Features.ItemGeneration.Queries;

public partial class CategoryBrowser : Control
{
    private IMediator _mediator;

    public async void ShowAllCategories()
    {
        var result = await _mediator.Send(new GetAvailableItemCategoriesQuery
        {
            IncludeItemCounts = true
        });

        if (result.Success)
        {
            GD.Print($"Available categories: {result.TotalCategories}");
            
            foreach (var category in result.Categories)
            {
                GD.Print($"  - {category.DisplayName} ({category.Category})");
                GD.Print($"    Items: {category.ItemCount}, Has Names: {category.HasNamesFile}");
            }
        }
    }

    public async void ShowWeaponCategoriesOnly()
    {
        var result = await _mediator.Send(new GetAvailableItemCategoriesQuery
        {
            FilterPattern = "weapons/*",
            IncludeItemCounts = true
        });

        if (result.Success)
        {
            GD.Print($"Weapon categories: {result.TotalCategories}");
            
            foreach (var category in result.Categories)
            {
                GD.Print($"  - {category.DisplayName}: {category.ItemCount} items");
            }
        }
    }
}
```

---

## Example 5: Generate Specific Item Multiple Times

```csharp
using Godot;
using MediatR;
using RealmEngine.Core.Features.ItemGeneration.Commands;

public partial class QuestReward : Node
{
    public async Task<List<Item>> GenerateReward(IMediator mediator)
    {
        // Generate 3 copies of "Iron Longsword" with random materials/enchantments
        var result = await mediator.Send(new GenerateItemsByCategoryCommand
        {
            Category = "weapons/swords",
            ItemName = "Iron Longsword",
            Quantity = 3,
            MinBudget = 30,
            MaxBudget = 40,
            UseBudgetGeneration = true
        });

        if (result.Success)
        {
            GD.Print($"Quest reward: {result.ActualQuantity} Iron Longswords");
            return result.Items;
        }

        return new List<Item>();
    }
}
```

---

## Troubleshooting

### Issue: Only 9 items spawned instead of 20

**Cause**: Generation succeeded (20 items created) but spawning logic may have failed.

**Solution**: Check your item spawning code and add error handling:

```csharp
var result = await _mediator.Send(new GenerateRandomItemsCommand { Quantity = 20 });

if (result.Success)
{
    GD.Print($"Generated {result.ActualQuantity}/{result.RequestedQuantity} items");
    
    int spawned = 0;
    foreach (var item in result.Items)
    {
        try
        {
            SpawnItemPickup(item);
            spawned++;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Failed to spawn {item.Name}: {ex.Message}");
        }
    }
    
    GD.Print($"Spawned {spawned}/{result.Items.Count} items successfully");
}
```

### Issue: Generation returns 0 items

**Possible Causes:**
1. Invalid category name
2. Budget too low (items can't be generated)
3. Data cache not loaded

**Solution:**

```csharp
var result = await _mediator.Send(new GenerateRandomItemsCommand
{
    Quantity = 20,
    Category = "random",
    MinBudget = 15,
    MaxBudget = 50
});

if (!result.Success)
{
    GD.PrintErr($"Generation failed: {result.ErrorMessage}");
    GD.Print($"Categories used: {string.Join(", ", result.CategoriesUsed)}");
}
else if (result.ActualQuantity < result.RequestedQuantity)
{
    GD.PrintRich($"[color=yellow]Warning: Only generated {result.ActualQuantity}/{result.RequestedQuantity} items[/color]");
}
```

---

## Performance Tips

1. **Use Simple Mode for Testing:**
   ```csharp
   UseBudgetGeneration = false  // Faster, no materials/enchantments
   ```

2. **Batch Generation in Background:**
   ```csharp
   // Don't block UI - generate asynchronously
   Task.Run(async () => 
   {
       var items = await GenerateLargeInventory();
       CallDeferred("UpdateUI", items);
   });
   ```

3. **Limit Quantity for Responsive UI:**
   ```csharp
   Quantity = 50  // Good for realtime
   // vs
   Quantity = 500  // Use for background pre-generation
   ```

---

See [ItemGeneration/README.md](../RealmEngine.Core/Features/ItemGeneration/README.md) for full API documentation.
