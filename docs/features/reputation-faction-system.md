# Reputation & Faction System

**Status**: ✅ 100% Complete
**Implementation**: `RealmEngine.Core/Features/Reputation`
**Faction Data**: Seeded to the content database; manage factions in RealmForge
**Tests**: Integrated with all systems

## Overview

Player actions influence relationships with factions, affecting prices, quest availability, and faction hostility. Seven reputation levels from Hostile to Exalted provide gameplay consequences.

## Implementation Details

### Reputation Levels (7 Tiers)
1. **Hostile** (< -6000): Attacks on sight, no trade or quests
2. **Unfriendly** (-6000 to -3000): Limited trade, hostile interactions
3. **Neutral** (-500 to 500): Basic access, standard prices
4. **Friendly** (500 to 3000): 5% price discount, more quests
5. **Honored** (3000 to 6000): 10% price discount, special quests
6. **Revered** (6000 to 12000): 20% price discount, exclusive content
7. **Exalted** (12000+): 30% price discount, faction champion status

### Factions (Organized by Type)

**Trade Factions:**
- **Merchants Guild**: Trade association, allies with Craftsmen Guild and Nobility

**Labor Factions:**
- **Craftsmen Guild**: Artisans and tradespeople, allies with Merchants and Commoners

**Criminal Factions:**
- **Thieves Guild**: Underground network, enemies with City Guard and Merchants

**Military Factions:**
- **City Guard**: Law enforcement, allies with Nobility and Clergy
- **Military**: Armed forces, allies with Nobility
- **Fighters Guild**: Mercenaries, neutral stance

**Magical Factions:**
- **Mages Circle**: Arcane practitioners, allies with Scholars Guild

**Academic Factions:**
- **Scholars Guild**: Researchers, allies with Mages and Clergy

**Religious Factions:**
- **Clergy**: Religious organization, allies with City Guard and Nobility

**Social Factions:**
- **Commoners**: Common folk, allies with Craftsmen

**Political Factions:**
- **Nobility**: Aristocracy, allies with City Guard and Clergy

### Faction Benefits
- **Price Discounts**: 5-30% based on reputation level
- **Quest Access**: Higher reputation unlocks faction quests
- **Trade Access**: Some factions refuse trade when hostile
- **Ally/Enemy System**: Allied factions share reputation changes

## Services & Commands

### Services
- **ReputationService**: GetOrCreateReputation, GainReputation, LoseReputation, GetReputationLevel, CheckReputationRequirement, CanTrade, CanAcceptQuests, IsHostile, GetPriceDiscount, GetAllReputations
- **FactionDataService**: LoadFactions, GetFactionBySlug, GetFactionsByType

### Commands
- **GainReputationCommand**: Award reputation points with optional level change detection
- **LoseReputationCommand**: Remove reputation points with warnings
- **GetReputationQuery**: Get all or specific faction reputations with discount/access info

## Usage Example

```csharp
// Gain reputation from quest completion
var result = await mediator.Send(new GainReputationCommand 
{ 
    CharacterName = "PlayerHero",
    FactionId = "merchants-guild", 
    Amount = 500, 
    Reason = "Completed delivery quest",
    SaveGameId = saveId
});

if (result.Success && result.LevelChanged)
{
    DisplayNotification($"Reputation with {result.FactionName} increased to {result.NewLevel}!");
    if (result.NewLevel == ReputationLevel.Friendly)
    {
        DisplayNotification("You now receive a 5% discount at Merchants Guild shops!");
    }
}

// Check reputation before allowing quest
var repQuery = await mediator.Send(new GetReputationQuery
{
    CharacterName = "PlayerHero",
    FactionId = "thieves-guild",
    SaveGameId = saveId
});

var standing = repQuery.Reputations.FirstOrDefault();
if (standing != null)
{
    if (standing.IsHostile)
    {
        ShowMessage("The Thieves Guild won't talk to you!");
    }
    else if (standing.CanAcceptQuests)
    {
        ShowAvailableQuests("thieves-guild");
    }
    
    // Apply price discount in shop
    var discount = standing.PriceDiscount; // 0.0 to 0.30
    var finalPrice = basePrice * (1.0 - discount);
}

// Load faction data (FactionDataService is DI-injected, not manually constructed)
// It is registered as AddScoped<FactionDataService>() via AddRealmEngineCore()
var factionService = serviceProvider.GetRequiredService<FactionDataService>();
var allFactions = factionService.LoadFactions();
foreach (var faction in allFactions)
{
    DisplayFaction(faction.Name, faction.Type, faction.Description);
}
```

## Key Features

- **Meaningful Choices**: Actions have lasting consequences
- **Dynamic World**: Factions react to player behavior
- **Replayability**: Different faction paths encourage multiple playthroughs
- **Content Gating**: Exclusive content per faction
- **Moral Complexity**: No purely good or evil factions

## Related Systems

- [Quest System](quest-system.md) - Quest choices affect reputation
- [Exploration System](exploration-system.md) - Faction-controlled areas
- [Combat System](combat-system.md) - Attacking factions impacts reputation
