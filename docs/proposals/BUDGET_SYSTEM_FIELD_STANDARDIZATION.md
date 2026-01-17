# Budget System Field Standardization

**Date:** January 16, 2026  
**Status:** ✅ APPROVED - Pure Formula-Based Approach  
**Decision:** Use `rarityWeight` everywhere with inverse cost formula

---

## Summary

All game data uses `rarityWeight` for both selection probability and cost calculation via inverse formula: **cost = numerator / rarityWeight**

- **Higher rarityWeight** = more common = cheaper
- **Lower rarityWeight** = more rare = more expensive
- **Materials** get optional `costScale` multiplier for refined/exotic variants

---

## Field Specification by File Type

### Materials (`materials/**/catalog.json`)
```json
{
  "name": "Iron",
  "rarityWeight": 60,      // REQUIRED: Selection weight (higher = more common)
  "costScale": 1.0         // OPTIONAL: Cost multiplier (defaults to 1.0)
}
```

### Catalogs (`items/**/catalog.json`, `abilities/**/catalog.json`, etc.)
```json
{
  "name": "Longsword",
  "rarityWeight": 40       // REQUIRED: Selection weight + cost calculation
}
```

### Names/Patterns (`items/**/names.json`, `abilities/**/names.json`)
```json
{
  "value": "Flaming",
  "rarityWeight": 30       // REQUIRED: Pattern weight + cost calculation
}
```

### Material Pools (`general/material-pools.json`)
```json
{
  "materialRef": "@materials/properties/metals:iron",
  "rarityWeight": 60       // REQUIRED: Pool selection weight
}
```

### Enemies (`enemies/**/catalog.json`)
```json
{
  "name": "Goblin Warrior",
  "rarityWeight": 50       // REQUIRED: Spawn weight
}
```

### NPCs (`npcs/**/catalog.json`)
```json
{
  "name": "Blacksmith",
  "rarityWeight": 40       // REQUIRED: Encounter weight
}
```

### Quests (`quests/**/catalog.json`)
```json
{
  "name": "Fetch Quest",
  "rarityWeight": 60       // REQUIRED: Quest selection weight
}
```

---

## Deprecated Fields (Remove These)

- ❌ `budgetCost` - Use rarityWeight with formula instead
- ❌ `selectionWeight` - Replaced by rarityWeight
- ❌ `rarity` - Calculated from rarityWeight on-demand (see Rarity Tier Calculation)

---

## Cost Formula Details

### Material Cost Formula
```
cost = (numerator / rarityWeight) × costScale

numerator = 6000 (recommended)
costScale = defaults to 1.0 if omitted

Example:
  Iron (rW=60, scale=1.0): (6000/60) × 1.0 = 100 budget
  Refined Mithril (rW=20, scale=1.5): (6000/20) × 1.5 = 450 budget
```

### Component/Enchantment Cost Formula
```
cost = numerator / rarityWeight

component numerator = 100
enchantment numerator = 130 (30% premium)

Example:
  Common prefix (rW=50): 100/50 = 2 budget
  Rare enchantment (rW=10): 130/10 = 13 budget
```

### When to Use costScale

**Use for:**
- Refined materials (processing premium)
- Exotic/imported materials (transportation costs)
- Guild-controlled materials (artificial scarcity)

**Don't use for:**
- Natural rarity (adjust rarityWeight)
- Power differences (use stats/traits)

### Rarity Tier Calculation

**No `rarity` field needed** - Calculate display tier from `rarityWeight`:

```csharp
public enum RarityTier
{
    Common,      // rarityWeight: 50-100
    Uncommon,    // rarityWeight: 30-49
    Rare,        // rarityWeight: 15-29
    Epic,        // rarityWeight: 5-14
    Legendary    // rarityWeight: 1-4
}

public static RarityTier GetRarityTier(int rarityWeight)
{
    return rarityWeight switch
    {
        >= 50 => RarityTier.Common,
        >= 30 => RarityTier.Uncommon,
        >= 15 => RarityTier.Rare,
        >= 5 => RarityTier.Epic,
        _ => RarityTier.Legendary
    };
}
```

**Benefits:**
- Single source of truth (only rarityWeight in data)
- No inconsistencies between rarity and rarityWeight
- Global tuning via range adjustments

---

## Implementation

### Phase 1: Update Configuration (budget-config.json)

```json
{
  "costFormulas": {
    "material": {
      "formula": "inverse_scaled",
      "numerator": 6000,
      "field": "rarityWeight",
      "scaleField": "costScale",
      "description": "Material cost = (6000 / rarityWeight) × costScale"
    },
    "component": {
      "formula": "inverse",
      "numerator": 100,
      "field": "rarityWeight",
      "description": "Component cost = 100 / rarityWeight"
    },
    "enchantment": {
      "formula": "inverse",
      "numerator": 130,
      "field": "rarityWeight",
      "description": "Enchantment cost = 130 / rarityWeight"
    }
  }
}
```

### Phase 2: Update Material Pools (material-pools.json)

Change `selectionWeight` → `rarityWeight`:

```json
{
  "pools": {
    "humanoid_low": {
      "metals": [
        {
          "materialRef": "@materials/properties/metals:iron",
          "rarityWeight": 60  // Changed from selectionWeight
        }
      ]
    }
  }
}
```

### Phase 3: Update Materials (metals/woods/leathers/catalog.json)

**Remove:** `budgetCost`, redundant `rarity` fields

**Keep/Add:**
- `rarityWeight` (required)
- `costScale` (optional, defaults to 1.0)

### Phase 4: Update BudgetCalculator.cs

```csharp
/// <summary>
/// Calculate the cost of a material using inverse formula with optional cost scale.
/// Formula: cost = (numerator / rarityWeight) × costScale
/// </summary>
public int CalculateMaterialCost(JToken material)
{
    var rarityWeight = GetIntProperty(material, "rarityWeight", 0);
    if (rarityWeight <= 0)
    {
        _logger.LogWarning("Material {MaterialName} has invalid rarityWeight: {Weight}", 
            GetStringProperty(material, "name"), rarityWeight);
        return 999999;
    }
    
    var numerator = _config.Formulas.Material.Numerator ?? 6000;
    var costScale = GetDoubleProperty(material, "costScale", 1.0);
    var cost = ((double)numerator / rarityWeight) * costScale;
    
    return (int)Math.Round(cost);
}

/// <summary>
/// Calculate the cost of a component using inverse formula.
/// Formula: cost = numerator / rarityWeight
/// </summary>
public int CalculateComponentCost(JToken component)
{
    var rarityWeight = GetIntProperty(component, "rarityWeight", 0);
    if (rarityWeight <= 0)
    {
        _logger.LogWarning("Component {ComponentName} has invalid rarityWeight: {Weight}", 
            GetStringProperty(component, "value"), rarityWeight);
        return 999999;
    }
    
    var numerator = _config.Formulas.Component.Numerator ?? 100;
    return (int)Math.Round((double)numerator / rarityWeight);
}

/// <summary>
/// Calculate the cost of an enchantment using inverse formula.
/// Formula: cost = numerator / rarityWeight
/// </summary>
public int CalculateEnchantmentCost(JToken enchantment)
{
    var rarityWeight = GetIntProperty(enchantment, "rarityWeight", 0);
    if (rarityWeight <= 0)
    {
        _logger.LogWarning("Enchantment {EnchantmentName} has invalid rarityWeight: {Weight}", 
            GetStringProperty(enchantment, "value"), rarityWeight);
        return 999999;
    }
    
    var numerator = _config.Formulas.Enchantment.Numerator ?? 130;
    return (int)Math.Round((double)numerator / rarityWeight);
}

private static double GetDoubleProperty(JToken token, string propertyName, double defaultValue)
{
    try
    {
        var value = token[propertyName];
        return value?.Value<double>() ?? defaultValue;
    }
    catch
    {
        return defaultValue;
    }
}
```

### Phase 5: Update MaterialPoolService.cs
field entirely (calculate from rarityWeight)
```csharp
// Use rarityAdd Rarity Calculation Utility

Create utility class in `RealmEngine.Shared`:

```csharp
// RarityCalculator.cs
public static class RarityCalculator
{
    public static RarityTier GetRarityTier(int rarityWeight)
    {
        return rarityWeight switch
        {
            >= 50 => RarityTier.Common,
            >= 30 => RarityTier.Uncommon,
            >= 15 => RarityTier.Rare,
            >= 5 => RarityTier.Epic,
            _ => RarityTier.Legendary
        };
    }
    
    public static string GetRarityColor(RarityTier tier)
    {
        return tier switch
        {
            RarityTier.Common => "#FFFFFF",
            RarityTier.Uncommon => "#1EFF00",
            RarityTier.Rare => "#0070DD",
            RarityTier.Epic => "#A335EE",
            RarityTier.Legendary => "#FF8000",
            _ => "#FFFFFF"
        };
    }, remove rarity)
- All `enemies/**/catalog.json` (remove rarity field)
- All `items/**/catalog.json` (remove rarity field)
- Verify all other catalogs/names already use rarityWeight

**Utility (new):**
- `RealmEngine.Shared/Utilities/RarityCalculator.cs` (calculate tier from weight)
```

### Phase 9: Update Documentation
- Update ITEM_ENHANCEMENT_SYSTEM.md
- Update JSON standards documents
- Document rarity tier range
    affordableMaterials.Add((
        Material: resolved,
        Cost: cost,
        Weight: materialRef.RarityWeight  // Changed from SelectionWeight
    ));
}
```

### Phase 6: Update Model Classes

```csharp
// MaterialReference.cs
public class MaterialReference
{
    [JsonProperty("materialRef")]
    public string MaterialRef { get; set; } = string.Empty;

    [JsonProperty("rarityWeight")]
    public int RarityWeight { get; set; }
}
```

### Phase 7: Update All JSON Files

**Search and replace across all data files:**
- Material pools: `selectionWeight` → `rarityWeight`
- Remove `budgetCost` from all materials
- Remove `rarity` if duplicating `rarityWeight`

### Phase 8: Update Documentation
- Update ITEM_ENHANCEMENT_SYSTEM.md
- Update JSON standards documents

---

## Validation Checklist

- [ ] Budget calculator uses `rarityWeight` for all cost calculations
- [ ] Material pools use `rarityWeight` (not selectionWeight)
- [ ] No code references `budgetCost` or `selectionWeight`
- [ ] All JSON files use `rarityWeight` consistently
- [ ] `costScale` defaults to 1.0 when omitted
- [ ] Test item generation with various budgets
- [ ] Verify cost progression feels balanced
- [ ] Godot integration works

---

## Files to Update

**Configuration:**
- `budget-config.json` (formulas)

**Code:**
- `BudgetCalculator.cs` (cost methods)
- `MaterialPoolService.cs` (pool selection)
- `MaterialReference.cs` (model)

**Data (bulk update):**
- `material-pools.json` (selectionWeight → rarityWeight)
- All `materials/**/catalog.json` (remove budgetCost/rarity)
- Verify all other catalogs/names already use rarityWeight
