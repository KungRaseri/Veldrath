# Upgrading System

**Status**: ✅ Complete  
**Last Updated**: March 18, 2026

Upgrading improves an item's base stats by applying **essences**. Items progress from +1 through +10. The first five levels always succeed; levels +6–+10 carry increasing risk of downgrade on failure.

## Upgrade Levels and Stat Scaling

The stat multiplier at each level is: `1 + (level × 0.10) + (level² × 0.01)`

| Level | Stat multiplier | Notes |
|-------|----------------|-------|
| +0 (base) | ×1.00 | — |
| +1 | ×1.11 | Safe zone |
| +2 | ×1.24 | Safe zone |
| +3 | ×1.39 | Safe zone |
| +4 | ×1.56 | Safe zone |
| +5 | ×1.75 | Safe zone |
| +6 | ×1.96 | 95% success |
| +7 | ×2.19 | 85% success |
| +8 | ×2.44 | 75% success |
| +9 | ×2.71 | 60% success |
| +10 | ×3.00 | 50% success |

The maximum upgrade level is determined by item rarity (see `Item.GetMaxUpgradeLevel()`). A Common item cannot reach +10.

## Failure Mechanic

A failed upgrade drops the item by one level (minimum +0) and always consumes the essences. There is no way to avoid essence consumption on failure — the risk is intentional.

## Essence Requirements

Essences must match the item's category. Wrong-type essences are rejected before any roll.

| Item category | Required essence type |
|--------------|----------------------|
| Weapon, Off-hand | Weapon Essence |
| All armor pieces | Armor Essence |
| Ring, Necklace | Accessory Essence |

Essences come in four tiers. Higher levels require larger quantities and higher-tier essences:

| Target level | Required essences |
|-------------|-------------------|
| +1 | 1× Minor |
| +2 | 2× Minor |
| +3 | 3× Minor |
| +4 | 1× Greater + 2× Minor |
| +5 | 2× Greater |
| +6 | 3× Greater |
| +7 | 1× Superior + 3× Greater |
| +8 | 2× Superior |
| +9 | 3× Superior |
| +10 | 1× Perfect + 3× Superior |

## Performing an Upgrade

Send `UpgradeItemCommand` with the character, item, and the required essences. Essences are validated before the roll is made.

```
var result = await mediator.Send(new UpgradeItemCommand
{
    Character = character,
    Item = item,
    Essences = [greaterWeaponEssence, greaterWeaponEssence]  // targeting +5
});

result.Success          // bool — whether the level increased
result.NewUpgradeLevel  // int — current level after the attempt (may be lower on failure)
result.OldUpgradeLevel  // int — level before the attempt
result.SuccessRate      // double — probability that was used
result.EssencesConsumed // always true after validation passes
result.StatMultiplier   // double — stat multiplier at NewUpgradeLevel
```

## Queries

### Preview Before Upgrading
`GetUpgradePreviewQuery` returns full information about the next upgrade level without committing the operation. Use this to show players the risk and requirements before they spend essences.

```
var preview = await mediator.Send(new GetUpgradePreviewQuery(item));

preview.CanUpgrade              // bool
preview.NextLevelPreview        // UpgradePreviewInfo?
  .TargetLevel                  // int
  .SuccessRate                  // double
  .IsSafeZone                   // bool (+1–+5)
  .CurrentStatMultiplier        // double
  .ProjectedStatMultiplier      // double
  .StatIncreasePercent          // double — e.g. 24.0 for +24%
  .EssenceType                  // "Weapon", "Armor", or "Accessory"
  .RequiredEssenceTiers         // List<string> — e.g. ["Greater", "Greater"]

preview.RemainingLevels         // full table of all levels not yet reached
```

## Content Designer Notes

- Essence items are seeded in RealmForge under the **Items** catalog with names following the pattern `"{Type} Essence ({Tier})"`, e.g. `"Weapon Essence (Greater)"`. The handler matches by checking `Item.Name.Contains(essenceType)`.
- Essence drops are configured in loot tables and vendor inventories. Scaling the availability of higher-tier essences controls how quickly players can reach the +6–+10 risk zone.
- The `UpgradeService` (registered in DI) exposes all calculation methods (`CalculateSuccessRate`, `CalculateStatMultiplier`, `GetRequiredEssences`) and can be injected into any custom services that need upgrade information.

## Related Systems

- [Enchanting System](enchanting-system.md) — magical property application (separate from stat scaling)
- [Socketing System](socketing-system.md) — gem and rune insertion (separate enhancement path)
- [Crafting System](crafting-system.md) — materials and recipe ecosystem
- [Inventory System](inventory-system.md) — essences are stored and consumed from inventory
