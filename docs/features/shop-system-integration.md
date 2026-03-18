# Shop System Integration Guide

**Status**: ✅ 100% Complete (See [COMPLETED_WORK.md](../COMPLETED_WORK.md))

## Overview

The shop system enables players to buy and sell items with merchant NPCs. It uses a MediatR command/handler pattern for clean separation of concerns and supports dependency injection.

## Architecture

### Commands
- **BrowseShopCommand**: View merchant's inventory with pricing
- **BuyFromShopCommand**: Purchase items from merchant
- **SellToShopCommand**: Sell items to merchant

### Services
- **ShopEconomyService**: Handles pricing calculations, inventory management, and transactions
- **SaveGameService**: Manages game state persistence
- **ISaveGameService**: Interface for save game operations

## Dependency Injection Setup

All shop-related services (`ShopEconomyService`, `SaveGameService`, `ISaveGameService`) are registered automatically by `AddRealmEngineCore()`. The only service hosts need to provide is the persistence backend.

### Standard Setup (using engine DI extensions)

```csharp
using Microsoft.Extensions.DependencyInjection;
using RealmEngine.Core;

var services = new ServiceCollection();

// Register logging
services.AddLogging(builder => builder.AddConsole());

// Register all RealmEngine Core services (includes ShopEconomyService, SaveGameService, etc.)
// Use the default in-memory persistence for quick setup:
services.AddRealmEngineCore();

// Register MediatR handlers (shop command handlers are in RealmEngine.Core assembly)
services.AddRealmEngineMediatR();

var serviceProvider = services.BuildServiceProvider();
```

For PostgreSQL persistence:

```csharp
services.AddRealmEngineCore(p => p.UseNpgsql(connectionString));
services.AddRealmEngineMediatR();
```

### Minimal Test Setup

```csharp
using Moq;

var services = new ServiceCollection();
services.AddLogging();

// In-memory mode wires up all repositories automatically — no mocking needed for most tests.
services.AddRealmEngineCore();
services.AddRealmEngineMediatR();

var serviceProvider = services.BuildServiceProvider();
```

## Usage Examples

### Browse Shop Inventory

```csharp
using MediatR;
using RealmEngine.Core.Features.Shop.Commands;

// Get IMediator from service provider
var mediator = serviceProvider.GetRequiredService<IMediator>();

// Browse shop inventory
var browseCommand = new BrowseShopCommand("merchant-001");
var browseResult = await mediator.Send(browseCommand);

if (browseResult.Success)
{
    // Display core items (unlimited stock)
    foreach (var item in browseResult.CoreItems)
    {
        Console.WriteLine($"{item.Item.Name} - Buy: {item.BuyPrice}g");
    }

    // Display dynamic items (limited stock)
    foreach (var item in browseResult.DynamicItems)
    {
        Console.WriteLine($"{item.Item.Name} - Buy: {item.BuyPrice}g (Days left: {item.DaysRemaining})");
    }

    // Display player-sold items (resale)
    foreach (var item in browseResult.PlayerSoldItems)
    {
        Console.WriteLine($"{item.Item.Name} - Buy: {item.BuyPrice}g (Resale)");
    }
}
else
{
    Console.WriteLine($"Error: {browseResult.ErrorMessage}");
}
```

### Buy from Shop

```csharp
// Purchase an item
var buyCommand = new BuyFromShopCommand("merchant-001", "iron-sword-001");
var buyResult = await mediator.Send(buyCommand);

if (buyResult.Success)
{
    Console.WriteLine($"Purchased {buyResult.ItemPurchased.Name} for {buyResult.PriceCharged}g");
    Console.WriteLine($"Gold remaining: {buyResult.PlayerGoldRemaining}g");
}
else
{
    Console.WriteLine($"Error: {buyResult.ErrorMessage}");
}
```

### Sell to Shop

```csharp
// Sell an item to merchant
var sellCommand = new SellToShopCommand("merchant-001", "rusty-dagger-005");
var sellResult = await mediator.Send(sellCommand);

if (sellResult.Success)
{
    Console.WriteLine($"Sold {sellResult.ItemSold.Name} for {sellResult.PriceReceived}g");
    Console.WriteLine($"Gold remaining: {sellResult.PlayerGoldRemaining}g");
}
else
{
    Console.WriteLine($"Error: {sellResult.ErrorMessage}");
}
```

## Merchant Setup

### Creating a Merchant NPC

Merchants must have specific traits in their NPC definition:

```csharp
using RealmEngine.Shared.Models;

var merchant = new NPC
{
    NpcId = "merchant-001",
    Name = "Tharin the Blacksmith",
    Gold = 5000, // Starting gold
    Traits = new Dictionary<string, string>
    {
        ["isMerchant"] = "true",
        ["shopType"] = "general", // or "weapons", "armor", "potions", etc.
        ["shopInventoryType"] = "hybrid" // "core-only", "dynamic-only", or "hybrid"
    }
};

// Add to save game
saveGame.KnownNPCs.Add(merchant);
```

### Shop Types

- **general**: Sells common items, tools, and basic equipment
- **weapons**: Specializes in weapons and combat gear
- **armor**: Focuses on protective equipment
- **potions**: Sells consumables and crafting materials
- **magic**: Arcane items, spell scrolls, and enchanted gear

### Inventory Types

- **core-only**: Only sells fixed merchant inventory (unlimited stock)
- **dynamic-only**: Only sells procedurally generated items (limited stock)
- **hybrid**: Sells both core and dynamic items (recommended)

## Pricing System

The shop economy uses a dynamic pricing model:

- **Buy Price** (player buying from merchant): Base price × merchant markup (usually 1.5x)
- **Sell Price** (player selling to merchant): Base price × merchant buyback rate (usually 0.5x)

Factors affecting pricing:
- Item rarity
- Merchant type (specialists offer better prices in their category)
- Player reputation (future feature)

## Error Handling

### Common Errors

1. **"Merchant not found"**: `merchantId` doesn't exist in `SaveGame.KnownNPCs`
2. **"NPC is not a merchant"**: NPC is missing `isMerchant=true` trait
3. **"Insufficient gold"**: Player doesn't have enough gold to buy
4. **"Item not found in inventory"**: Trying to sell item not owned
5. **"Cannot sell equipped item"**: Item is currently equipped (unequip first)
6. **"Merchant cannot afford this purchase"**: Merchant has insufficient gold

All errors are returned in the result's `ErrorMessage` property with `Success=false`.

## Testing

See `ShopIntegrationTests.cs` for comprehensive examples covering:

- ✅ Browsing shop inventory
- ✅ Buying items with sufficient gold
- ✅ Handling insufficient gold
- ✅ Selling items to merchants
- ✅ Preventing sale of equipped items
- ✅ Gold tracking across transactions
- ✅ Player-sold item resale system
- ✅ Price calculation validation

## Integration with Game Systems

### NPC Encounters

When player interacts with NPC, check for merchant trait:

```csharp
if (npc.Traits.TryGetValue("isMerchant", out var isMerchantValue) 
    && isMerchantValue == "true")
{
    // Show "Trade" option in dialogue menu
    // On selection, open shop UI using BrowseShopCommand
}
```

### Exploration System

Add shop menu option when merchant is encountered:

```csharp
var options = new List<string> { "Talk", "Leave" };

if (currentNpc.Traits.ContainsKey("isMerchant"))
{
    options.Insert(1, "Trade");
}

var choice = ShowMenu("What would you like to do?", options);
if (choice == "Trade")
{
    await OpenShop(currentNpc.NpcId);
}
```

## Future Enhancements

Planned features for the shop system:

- [ ] Haggling/negotiation system
- [ ] Bulk buy/sell discounts
- [ ] Merchant reputation system
- [ ] Special merchant quests
- [ ] Black market merchants (stolen goods)
- [ ] Item repair services
- [ ] Enchantment services
- [ ] Crafting commission system

## Dependencies

- **MediatR**: Command/query pattern
- **Microsoft.Extensions.DependencyInjection**: Service registration
- **Microsoft.Extensions.Logging**: Diagnostic logging
- **RealmEngine.Shared**: Game models (SaveGame, NPC, Item)
- **RealmEngine.Core**: Core services and abstractions

## Related Documentation

- [Inventory System](./inventory-system.md)
