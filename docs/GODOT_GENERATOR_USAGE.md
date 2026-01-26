# Godot Generator Usage Guide

## ✅ Generator Registration Status

All generators are properly registered in `ServiceCollectionExtensions.cs` and will be available when you call:

```csharp
services.AddRealmEngineCore();  // Registers all 5 generators
```

## 📦 Available Generators

### 1. ItemGenerator
- **Registered:** ✅ Yes (in AddRealmEngineCore)
- **Command:** `GenerateItemCommand`
- **Data Location:** `RealmEngine.Data/Data/Json/items/`

### 2. EnemyGenerator
- **Registered:** ✅ Yes (in AddRealmEngineCore)
- **Command:** `GenerateEnemyCommand`
- **Data Location:** `RealmEngine.Data/Data/Json/enemies/`

### 3. NpcGenerator
- **Registered:** ✅ Yes (in AddRealmEngineCore)
- **Command:** `GenerateNPCCommand`
- **Data Location:** `RealmEngine.Data/Data/Json/npcs/`

### 4. AbilityGenerator
- **Registered:** ✅ Yes (in AddRealmEngineCore)
- **Command:** `GenerateAbilityCommand`
- **Data Location:** `RealmEngine.Data/Data/Json/abilities/`

### 5. CharacterClassGenerator
- **Registered:** ✅ Yes (in AddRealmEngineCore)
- **Queries:** `GetAvailableClassesQuery`, `GetClassDetailsQuery`
- **Data Location:** `RealmEngine.Data/Data/Json/classes/`

---

## 📁 JSON File Structure (IMPORTANT!)

### Items Structure
```
items/
├── weapons/catalog.json          ← Single file for ALL weapons
│   └── weapon_types {
│       ├── swords: { items: [...] }
│       ├── axes: { items: [...] }
│       ├── maces: { items: [...] }
│       └── ...
│   }
├── armor/catalog.json            ← Single file for ALL armor
│   └── armor_types {
│       ├── light: { items: [...] }
│       ├── medium: { items: [...] }
│       ├── heavy: { items: [...] }
│       └── shields: { items: [...] }
│   }
├── consumables/catalog.json
└── accessories/catalog.json
```

### Abilities Structure
```
abilities/
├── active/catalog.json           ← Active abilities
├── passive/catalog.json          ← Passive abilities
├── reactive/catalog.json         ← Reactive abilities
└── ultimate/catalog.json         ← Ultimate abilities
```

### Classes Structure
```
classes/
└── catalog.json                  ← Single file with all classes
    └── class_categories {
        ├── warriors: [...]
        ├── rogues: [...]
        ├── mages: [...]
        └── priests: [...]
    }
```

---

## ✅ Correct Usage Examples

### Generate Random Items

```csharp
// Generate 20 random weapons (swords, axes, maces, etc.)
var result = await _mediator.Send(new GenerateItemCommand
{
    Category = "weapons",     // Top-level folder
    Subcategory = null,       // Random from all weapon types
    Count = 20,
    Hydrate = true
});

// Generate 10 swords specifically
var result = await _mediator.Send(new GenerateItemCommand
{
    Category = "weapons",
    Subcategory = "swords",   // Specific weapon type
    Count = 10,
    Hydrate = true
});

// Generate random armor
var result = await _mediator.Send(new GenerateItemCommand
{
    Category = "armor",
    Subcategory = null,       // Random (light/medium/heavy/shields)
    Count = 15,
    Hydrate = true
});
```

### Generate Specific Item by Name

```csharp
var result = await _mediator.Send(new GenerateItemCommand
{
    Category = "weapons",
    ItemName = "longsword",   // Specific item name/slug
    Hydrate = true
});
```

### Generate Abilities

```csharp
// Generate 5 random active abilities
var result = await _mediator.Send(new GenerateAbilityCommand
{
    Category = "active",           // abilities/active/catalog.json
    Subcategory = "offensive",     // Type within active catalog
    Count = 5,
    Hydrate = true
});

// Generate specific ability
var result = await _mediator.Send(new GenerateAbilityCommand
{
    Category = "active",
    AbilityName = "fireball",      // Specific ability
    Hydrate = true
});

// Generate ultimate abilities
var result = await _mediator.Send(new GenerateAbilityCommand
{
    Category = "ultimate",         // abilities/ultimate/catalog.json
    Count = 3,
    Hydrate = true
});
```

### Query Character Classes

```csharp
// Get all available classes
var result = await _mediator.Send(new GetAvailableClassesQuery
{
    Hydrate = true
});

// Get classes by category (warriors, rogues, mages, priests)
var result = await _mediator.Send(new GetAvailableClassesQuery
{
    Category = "warriors",
    Hydrate = true
});

// Get specific class details
var result = await _mediator.Send(new GetClassDetailsQuery
{
    ClassName = "fighter",         // Class name or slug
    Hydrate = true
});
```

### Generate Enemies

```csharp
// Generate random enemies
var result = await _mediator.Send(new GenerateEnemyCommand
{
    Category = "humanoid",         // enemies/humanoid/catalog.json
    Count = 10,
    Hydrate = true
});
```

### Generate NPCs

```csharp
// Generate random NPCs
var result = await _mediator.Send(new GenerateNPCCommand
{
    Category = "merchants",        // npcs/merchants/catalog.json
    Count = 5,
    Hydrate = true
});
```

---

## 🚫 Common Mistakes

### ❌ WRONG: Trying to access nested paths directly
```csharp
// This will FAIL - no such file exists
var result = await _mediator.Send(new GenerateItemCommand
{
    Category = "weapons/swords",   // ❌ Wrong!
    Count = 10
});
```

### ✅ CORRECT: Use Category + Subcategory
```csharp
// This works - generator finds items/weapons/catalog.json
var result = await _mediator.Send(new GenerateItemCommand
{
    Category = "weapons",          // ✅ Top-level category
    Subcategory = "swords",        // ✅ Type within catalog
    Count = 10
});
```

---

## 📋 Available Categories Reference

### Item Categories
- `weapons` - Swords, axes, maces, daggers, staves, bows, crossbows, spears, fist-weapons
- `armor` - Light, medium, heavy, shields
- `consumables` - Potions, food, scrolls
- `accessories` - Rings, amulets, cloaks, belts
- `gems` - Blue, red, special
- `essences` - Fire, shadow, nature, etc.
- `crystals` - Life, mana
- `materials` - Bone, ceramic, chitin, cloth, etc.
- `orbs` - Combat, magic
- `runes` - Offensive, defensive

### Ability Categories
- `active` - Offensive, defensive, support, utility
- `passive` - Stat boosts, effects
- `reactive` - Triggered abilities
- `ultimate` - Powerful abilities

### Enemy Categories
- `humanoid` - Goblins, bandits, etc.
- `undead` - Skeletons, zombies, etc.
- `beast` - Wolves, bears, etc.
- `dragon` - Dragon types

### NPC Categories
- `merchants` - Shop vendors
- `guards` - City guards
- `commoners` - Regular NPCs

---

## 🔍 Troubleshooting

### "File not found: items/weapons/swords/catalog.json"

**Problem:** Your code is trying to access individual subcategory files.

**Solution:** Use the two-part path:
```csharp
Category = "weapons",      // File: items/weapons/catalog.json
Subcategory = "swords"     // Type within that catalog
```

### "No valid categories found"

**Problem:** Category name doesn't match any top-level folder.

**Solution:** Use exact category names from the reference above (all lowercase).

### "Query succeeded but returned no categories"

**Problem:** The query found the file but the structure doesn't match expectations.

**Solution:** Ensure you're using the correct command/query for the data type:
- Items → `GenerateItemCommand`
- Abilities → `GenerateAbilityCommand`
- Classes → `GetAvailableClassesQuery`

---

## 🎯 Summary

1. **Generators ARE registered** - No changes needed to backend DI
2. **File structure uses single catalogs** - Not nested subcategory files
3. **Use Category + Subcategory** - Let generators handle file paths
4. **Follow the examples above** - Correct patterns for all generator types

The backend is ready! Just update your Godot code to use the correct category structure. 🚀
