# Enchanting System

**Status**: ✅ Complete  
**Last Updated**: March 18, 2026

Enchanting adds magical properties to existing gear using consumable **enchantment scrolls**. Unlike the socketing system (which inserts physical gems and runes), enchanting is a permanent, skill-based process with a success-rate mechanic — failure consumes the scroll without a benefit.

## Slots and Rarity Caps

Each item has two slot counts:

| Field | Meaning |
|-------|---------|
| `MaxPlayerEnchantments` | How many player-applied slots are currently unlocked (0–3) |
| `PlayerEnchantments` | The enchantments currently applied |

Items also have **inherent enchantments** (`Enchantments`) baked in at creation. These are always active and cannot be removed.

The **maximum possible** player enchantment slots is determined by item rarity:

| Rarity | Max player slots |
|--------|-----------------|
| Common | 1 |
| Uncommon | 1 |
| Rare | 2 |
| Epic | 3 |
| Legendary | 3 |

## Unlocking New Slots

Items start with 0 unlocked player slots. Use a **Socket Crystal** and `AddEnchantmentSlotCommand` to open each slot one at a time. Slot unlock requires:

| Slot | Enchanting skill required |
|------|--------------------------|
| 1st slot | 0 |
| 2nd slot | 25 |
| 3rd slot | 50 |

The crystal is always consumed. Unlocking past the rarity cap is blocked.

## Applying an Enchantment

Send `ApplyEnchantmentCommand` with the character, item, and an enchantment scroll. The scroll is always consumed regardless of outcome.

```
var result = await mediator.Send(new ApplyEnchantmentCommand
{
    Character = character,
    Item = item,
    EnchantmentScroll = scroll
});

result.Success              // bool
result.AppliedEnchantment   // Enchantment? (null on failure)
result.SuccessRate          // double — shown to player before attempt
result.ScrollConsumed       // always true after validation passes
```

### Success Rates

The success rate drops with each additional slot. The Enchanting skill partially compensates (+0.3% per rank, max 100%).

| Slot being filled | Base success rate |
|-------------------|------------------|
| 1st | 100% (guaranteed) |
| 2nd | 75% + skill × 0.3% |
| 3rd | 50% + skill × 0.3% |

Query `GetEnchantmentCostQuery` with `OperationType = ApplyEnchantment` to display the exact rate before the player commits.

## Removing an Enchantment

`RemoveEnchantmentCommand` removes a single player-applied enchantment by index. A **Removal Scroll** is required and is always consumed. The removed enchantment is destroyed — it is not returned to inventory.

```
var result = await mediator.Send(new RemoveEnchantmentCommand
{
    Character = character,
    Item = item,
    EnchantmentIndex = 0,   // zero-based index into PlayerEnchantments
    RemovalScroll = scroll
});

result.RemovedEnchantment   // Enchantment? — the destroyed enchantment
result.ScrollConsumed       // always true
```

## Queries

### View Item Enchantments
`GetEnchantmentsQuery` returns all player-applied and inherent enchantments on an item, plus slot availability and success rate information. Optionally pass the character's Enchanting skill rank to get accurate rate calculations.

```
var result = await mediator.Send(new GetEnchantmentsQuery(item, enchantingSkillRank: 45));

result.PlayerEnchantments   // List<EnchantmentSlotInfo> — applied by player
result.InherentEnchantments // List<EnchantmentSlotInfo> — baked in at creation
result.UnlockedSlots        // int
result.MaxPossibleSlots     // int — rarity cap
result.CanEnchant           // bool — has an open slot right now
result.RateSummary          // per-slot success rates
```

### Check Cost and Feasibility
`GetEnchantmentCostQuery` returns whether an operation is currently possible, what consumable is required, and (for `ApplyEnchantment`) the success rate.

```
var cost = await mediator.Send(new GetEnchantmentCostQuery(
    Item: item,
    OperationType: EnchantmentOperationType.ApplyEnchantment,
    EnchantingSkillRank: 45));

cost.IsPossible         // bool
cost.BlockedReason      // string — why it can't be done (empty if possible)
cost.SuccessRate        // double
cost.RequiredConsumable // "Enchantment Scroll", "Removal Scroll", or "Socket Crystal"
cost.RequiredSkill      // minimum Enchanting rank (for UnlockSlot operations)
```

## Enchantment Rarity Tiers

| Tier | `EnchantmentRarity` value |
|------|--------------------------|
| Minor | `Minor` |
| Lesser | `Lesser` |
| Greater | `Greater` |
| Superior | `Superior` |
| Legendary | `Legendary` |

Higher rarity enchantments provide stronger effects. Enchantment scrolls of each tier are crafted at the **Enchanting Altar** using appropriate reagents.

## Content Designer Notes

- Enchantment definitions are seeded in RealmForge under the **Enchantments** catalog. Each entry has a `Name`, `Description`, `Rarity`, and a `Traits` dictionary of stat bonuses.
- Enchantment scrolls are `Item` records with an `Enchantments` list containing exactly one enchantment entry.
- Socket crystals are plain consumable items — no special enchantment payload needed.
- The `EnchantingService` (registered in DI) contains all rate calculations and is available for injection into any custom handlers or UI services.

## Related Systems

- [Socketing System](socketing-system.md) — separate enhancement path using gems, runes, and orbs
- [Upgrading System](upgrading-system.md) — stat amplification via essences
- [Crafting System](crafting-system.md) — craft scrolls and crystals at the Enchanting Altar
- [Inventory System](inventory-system.md) — scrolls and crystals are stored as consumable items
