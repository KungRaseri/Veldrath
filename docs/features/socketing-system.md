# Socketing System

**Status**: ✅ Complete  
**Last Updated**: March 18, 2026

Sockets are special slots on weapons, armor, and accessories that accept gems, runes, crystals, and orbs. Each socketable item grants stat bonuses via the Traits system. Sockets within the same link group amplify one another when all slots are filled.

## Socket Types

| Type | Accepts | Example bonus |
|------|---------|---------------|
| `Gem` | Gems | Offensive stats (Attack Power, Crit) |
| `Rune` | Runes | Utility traits (Move Speed, Cooldown Reduction) |
| `Crystal` | Crystals | Defensive stats (Armor, Resistances) |
| `Orb` | Orbs | Magical stats (Spell Power, Mana) |

Socket type is fixed on the item at creation. A Gem socket only accepts Gem items; mixing types is not allowed.

## Link Groups

Some items have **linked sockets**. When all sockets in a link group are filled, an amplifying multiplier is applied to the traits contributed by every item in that group:

| Link size | Bonus multiplier |
|-----------|-----------------|
| 2-link | +10% |
| 3-link | +20% |
| 4-link | +30% |

A socket that is not part of any link group has `LinkGroup = -1`.

## Socketing Operations

### Socket an Item
Send `SocketItemCommand` with the target equipment ID, the socket index (0-based), and the socketable item. The item is validated for type compatibility and socket state (empty, not locked) before being placed.

```
var result = await mediator.Send(new SocketItemCommand(
    EquipmentItemId: item.Id,
    SocketIndex: 0,
    SocketableItem: gem));

result.Success          // bool
result.AppliedTraits    // Dictionary<string, TraitValue> — traits now active
result.IsLinked         // true if this socket is in a link group
result.LinkBonusMultiplier  // e.g. 1.2 if a 2-link just completed
```

### Socket Multiple Items (Batch)
`SocketMultipleItemsCommand` allows filling several sockets in one command, useful when equipping an item for the first time. Each slot is attempted independently; partial success is possible.

```
var result = await mediator.Send(new SocketMultipleItemsCommand(
    EquipmentItemId: item.Id,
    Operations: [
        new SocketOperation(0, gem),
        new SocketOperation(1, rune)
    ]));

result.SuccessCount         // int
result.FailureCount         // int
result.TotalAppliedTraits   // combined traits from all successful socketings
```

### Remove a Socketed Item
`RemoveSocketedItemCommand` extracts the item from a socket. Removal costs gold proportional to item value — query the cost first with `GetSocketCostQuery` (see below).

```
var result = await mediator.Send(new RemoveSocketedItemCommand(
    EquipmentItemId: item.Id,
    SocketIndex: 1,
    GoldCost: 150));

result.RemovedItem      // ISocketable — returned to player inventory
result.RemovedTraits    // traits that are no longer active
result.GoldPaid
```

## Queries

### Inspect Socket Layout
`GetSocketInfoQuery` returns a full breakdown of all sockets on an item, grouped by type, with link group information.

```
var info = await mediator.Send(new GetSocketInfoQuery(item.Id));

info.TotalSockets
info.FilledSockets
info.EmptySockets
info.SocketsByType          // Dictionary<SocketType, List<SocketDetailInfo>>
info.LinkedGroups           // List<LinkedSocketGroup>
```

`SocketInfoDto` is a flat-list equivalent for clients that prefer simple array access over dictionaries (every socket is in `Sockets`, link groups in `LinkGroups`, aggregate bonuses in `TotalBonuses`).

### Preview Before Socketing
`SocketPreviewQuery` validates and previews a socketing operation without committing it. Use this to show the player what will change before they confirm.

```
var preview = await mediator.Send(new SocketPreviewQuery(item.Id, socketIndex, gem));

preview.CanSocket           // bool — validation result
preview.TraitsToApply       // what will be added
preview.WouldActivateLink   // whether filling this slot completes a link group
preview.LinkBonusMultiplier // bonus that would activate
preview.Warnings            // non-blocking notices (e.g. replacing better gem)
```

### Compatible Items for a Socket
`GetCompatibleSocketablesQuery` returns all socketable items that fit a given socket type, with optional rarity filter and AI-suggested picks based on the character's build.

```
var options = await mediator.Send(new GetCompatibleSocketablesQuery(
    SocketType: SocketType.Gem,
    MinimumRarity: ItemRarity.Rare));

options.Items           // full list
options.SuggestedItems  // AI-curated subset
```

### Socket Cost
`GetSocketCostQuery` retrieves the gold cost for socketing, removal, or unlocking a locked socket, including any active modifiers such as reputation discounts.

```
var cost = await mediator.Send(new GetSocketCostQuery(
    EquipmentItemId: item.Id,
    CostType: SocketCostType.Remove,
    SocketIndex: 1));

cost.GoldCost       // final cost after modifiers
cost.CanAfford      // bool (if player gold was supplied)
cost.Modifiers      // List<CostModifier> for transparency
```

## Locked Sockets

A socket with `IsLocked = true` cannot be modified until it is unlocked via `RemoveSocketedItemCommand` with the `Unlock` cost type. Locked sockets appear on rare and higher rarity items as a balance mechanic against free respeccing.

## Content Designer Notes

- Socketable items (Gems, Runes, Crystals, Orbs) are seeded in RealmForge under the **Items** catalog with `SocketType` set to the appropriate value.
- The number and type of sockets on equipment are set in the item's `Sockets` list at creation time (via generators or direct DB entry).
- Link groups are configured per-item by assigning the same integer `LinkGroup` value to multiple socket entries.
- Socket counts per item are capped by the `SocketConfig` registered in DI (via `BudgetConfigFactory`).

## Related Systems

- [Crafting System](crafting-system.md) — crafted gear can be generated with sockets
- [Inventory System](inventory-system.md) — socketable items live in the player's inventory
- [Enchanting System](enchanting-system.md) — separate enhancement path using scrolls (not sockets)
