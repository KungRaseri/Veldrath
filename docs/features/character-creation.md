# Character Creation

**Status**: ✅ Complete  
**Implementation**: `RealmEngine.Core/Features/CharacterCreation`

## Overview

Character creation initializes a new character with a class, optional background, starting equipment, starting location, and difficulty setting. All steps are orchestrated by a single `CreateCharacterCommand`.

## Selections

### Class

The class defines the character's core identity: base stats, hit die, primary attribute, starting abilities, and equipment proficiencies. A class object is loaded first and passed directly into the command.

```csharp
var classes = await mediator.Send(new GetCharacterClassesQuery());
var warrior = classes.First(c => c.Slug == "warrior");
```

### Background (Optional)

Backgrounds provide a character's origin story and grant flat attribute bonuses at creation. They are stored in the content database and managed through RealmForge.

| Field | Description |
|-------|-------------|
| `Slug` | Unique identifier (e.g., `"soldier"`, `"scholar"`) |
| `DisplayName` | Name shown to the player |
| `TypeKey` | Category tag (e.g., `"common"`, `"criminal"`, `"scholar"`) |
| `RarityWeight` | Frequency weight for random selection |

**Stats applied at creation:**

| Stat | Description |
|------|-------------|
| `StartingGold` | Gold the character begins with |
| `BonusStrength` | Flat STR bonus |
| `BonusDexterity` | Flat DEX bonus |
| `BonusIntelligence` | Flat INT bonus |
| `BonusConstitution` | Flat CON bonus |
| `StartingSkillBonus` | Slug of a skill that receives a starting rank bonus |
| `SkillBonusValue` | Magnitude of that skill bonus |

**Traits** (boolean classifiers):

| Trait | Description |
|-------|-------------|
| `Military` | Combat or military service |
| `Noble` | Aristocratic upbringing |
| `Criminal` | Underworld background |
| `Merchant` | Trade or commerce |
| `Scholar` | Academic study |
| `Religious` | Temple or faith |
| `Regional` | Region-specific culture |

### Difficulty (Optional)

Difficulty is stored on the save game and applies multipliers throughout gameplay. Defaults to `"Normal"` if not specified.

| Preset | Description |
|--------|-------------|
| `"Easy"` | Reduced enemy damage, generous economy |
| `"Normal"` | Balanced — standard experience |
| `"Hard"` | Tougher enemies, stricter economy, higher death penalties |
| `"Very Hard"` | Extreme modifiers, steep consequences |

### Starting Equipment (Optional)

Hints for preferred gear type are accepted and used to select starting items that match the class's proficiencies.

| Parameter | Example Values |
|-----------|---------------|
| `PreferredArmorType` | `"cloth"`, `"leather"`, `"mail"`, `"plate"` |
| `PreferredWeaponType` | `"sword"`, `"axe"`, `"bow"`, `"staff"` |
| `IncludeShield` | `true` / `false` |

If no preference is given, default starting equipment for the class is used.

### Starting Location (Optional)

A `StartingLocationId` places the character in a specific zone. If omitted, a suitable starting zone is selected automatically. Use `GetStartingLocationsQuery` to find available options, optionally filtered by background recommendations.

---

## Commands & Queries

### Create a Character

```csharp
var result = await mediator.Send(new CreateCharacterCommand
{
    CharacterName = "Aldric",
    CharacterClass = warrior,
    BackgroundId = "soldier",          // optional — slug or full ID
    DifficultyLevel = "Normal",        // optional, defaults to "Normal"
    StartingLocationId = "crossroads", // optional
    PreferredArmorType = "plate",      // optional
    PreferredWeaponType = "sword",     // optional
    IncludeShield = false              // optional
});

if (result.Success)
{
    var character = result.Character;
    // character has class stats, background bonuses, starting abilities, and equipment applied
}
```

### Query Available Backgrounds

```csharp
// All backgrounds
var backgrounds = await mediator.Send(new GetBackgroundsQuery());

// Specific background by slug
var bg = await mediator.Send(new GetBackgroundQuery("soldier"));
```

### Query Starting Locations

```csharp
// All starting-eligible locations
var locations = await mediator.Send(new GetStartingLocationsQuery());

// Filtered by background recommendations
var recommended = await mediator.Send(new GetStartingLocationsQuery(
    BackgroundId: "soldier",
    FilterByRecommended: true));
```

---

## Related Systems

- [Character System](character-system.md) — Core character model and stats
- [Progression System](progression-system.md) — Skills, abilities, and spells gained after creation
- [Difficulty System](difficulty-system.md) — Difficulty modifier details
- [Inventory System](inventory-system.md) — Starting equipment and item management
