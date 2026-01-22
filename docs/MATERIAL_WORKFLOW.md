# Material System Workflow

## Overview
The material system connects lootable inventory items to stat-providing properties used in crafting.

## Complete Workflow

### 1. Material Items (Loot Drops)
**Location**: `items/materials/[type]/catalog.json`

These are physical items players loot from enemies/chests:
- **Fabric Items**: `@items/materials/fabric:linen-cloth`
- **Ingot Items**: `@items/materials/ingot:iron-ingot`
- **Leather Items**: `@items/materials/leather:*`
- **Wood Items**: `@items/materials/wood:*`

**Example** (Linen Cloth):
```json
{
  "name": "Linen Cloth",
  "slug": "linen-cloth",
  "description": "Smooth, durable fabric woven from flax fibers.",
  "propertyRef": "@properties/materials/fabrics:linen",
  "stackSize": 100,
  "rarityWeight": 90,
  "baseValue": 3
}
```

### 2. Material Properties (Stat Bonuses)
**Location**: `properties/materials/[type]/catalog.json`

These define the stats applied to crafted items:
- **Fabric Properties**: `@properties/materials/fabrics:linen`
- **Metal Properties**: `@properties/materials/metals:iron`
- **Leather Properties**: `@properties/materials/leathers:*`
- **Wood Properties**: `@properties/materials/woods:*`

**Example** (Linen Property):
```json
{
  "slug": "linen",
  "name": "Linen",
  "attributes": {
    "intelligence": 6,
    "wisdom": 6,
    "charisma": 4
  },
  "traits": {
    "durability": 30,
    "weight": 0.4,
    "magicResistance": 8
  },
  "itemTypeTraits": {
    "armor": {
      "armorRating": 2,
      "value": 3
    }
  },
  "rarityWeight": 90
}
```

### 3. Craftable Items (Armor/Weapons)
**Location**: `items/armor/catalog.json`, `items/weapons/catalog.json`

These define which materials can be used:

**Example** (Cloth Tunic):
```json
{
  "name": "Cloth Tunic",
  "slug": "cloth-tunic",
  "armorClass": "light",
  "allowedMaterials": ["@properties/materials/fabrics:*"],
  "rarityWeight": 80
}
```

## Reference Chain

```
Player loots: Linen Cloth
    └── propertyRef → @properties/materials/fabrics:linen

Player crafts: Cloth Tunic (allowedMaterials: @properties/materials/fabrics:*)
    └── Material selected: linen property
    └── Stats applied: +6 int, +6 wis, +4 cha, etc.

Result: "Linen Cloth Tunic" with stat bonuses
```

## Key Points

1. **Material Items** have `propertyRef` pointing to properties
2. **Craftable Items** have `allowedMaterials` pointing to properties (with wildcards)
3. **Never** reference material items in `allowedMaterials` - always reference properties
4. Use wildcards (`*`) in `allowedMaterials` to allow any material of that type

## Correct vs Incorrect

### ✅ Correct
```json
{
  "allowedMaterials": ["@properties/materials/fabrics:*"]
}
```

### ❌ Incorrect
```json
{
  "allowedMaterials": ["@items/materials/fabric:*"]
}
```

### ❌ Incorrect
```json
{
  "allowedMaterials": ["@items/materials/fabric"]
}
```

## Material Types

- **Fabrics**: Light armor, cloth items
- **Metals**: Heavy armor, weapons, shields
- **Leathers**: Medium armor, light weapons
- **Woods**: Bows, staves, shields
- **Gems**: Jewelry, enchantments (future)

## Validation

All references are validated by:
- `CatalogJsonComplianceTests.References_Should_Use_Valid_Syntax`
- `ReferenceValidationTests.All_References_Should_Resolve_Successfully`

Ensure:
1. All material items have valid `propertyRef`
2. All craftable items reference `@properties/materials/[type]:*`
3. Property slugs match between items and properties
