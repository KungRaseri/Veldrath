# Weight Terminology Consolidation Proposal

**Date:** January 15, 2026  
**Status:** 🔴 Draft - Awaiting Decision  
**Motivation:** Multiple overlapping weight/rarity terms cause confusion and maintenance issues

---

## Problem Statement

The codebase currently uses **4 overlapping terms** for item/component weighting:

| Term | Current Usage | Purpose | Inverse? |
|------|---------------|---------|----------|
| **`rarity`** | Numeric 0-100 value | Display tier (Common/Rare/Epic) | N/A |
| **`rarityWeight`** | Catalog items, components | Weighted random selection | ❌ No (higher = more common) |
| **`selectionWeight`** | Budget system, material pools | Budget cost calculation | ❌ No (higher = cheaper) |
| **`budgetCost`** | Materials only | Direct cost value | ✅ Yes (higher = more expensive) |

### Current Confusion Points

1. **`rarityWeight` vs `selectionWeight`**: Are they the same? Different systems use different names
2. **Inverse semantics**: `budgetCost` is inverse (higher = rarer), but weights are direct (higher = commoner)
3. **Fallback logic**: We just added code treating them as equivalent when they may not be
4. **No clear standard**: Different JSON files use different terms

### Example of Current Mess

```json
// catalog.json - uses rarityWeight
{
  "name": "Iron Sword",
  "rarityWeight": 50  // Higher = more common
}

// material-pools.json - uses selectionWeight  
{
  "materialRef": "@materials/metals:iron",
  "selectionWeight": 60  // Higher = more common
}

// materials/catalog.json - uses budgetCost
{
  "name": "Iron",
  "budgetCost": 10  // Higher = more expensive (INVERSE!)
}

// enchantments/names.json - uses rarityWeight
{
  "value": "of Fire",
  "rarityWeight": 30  // Higher = more common
}
```

**Code fallback (problematic):**
```csharp
// BudgetCalculator.cs - treats them as equivalent
var selectionWeight = component["selectionWeight"] ?? component["rarityWeight"];
```

---

## Proposed Solution: Clear Semantic Separation

### Option A: Consolidate to Single Weight System ⭐ RECOMMENDED

**Define clear semantic boundaries:**

| Field Name | JSON Files | Purpose | Formula | Semantics |
|------------|-----------|---------|---------|-----------|
| **`rarityWeight`** | catalog.json, names.json (components) | Weighted random selection | `P = 100 / rarityWeight` | Higher = RARER = less common |
| **`budgetCost`** | materials catalog only | Direct budget deduction | `cost = budgetCost` | Higher = MORE EXPENSIVE |
| **`rarity`** | Computed field (output) | Display tier | Threshold map | Common/Uncommon/Rare/Epic/Legendary |

**Key Changes:**

1. **Eliminate `selectionWeight`** - Use `rarityWeight` everywhere
2. **Standardize semantics**: `rarityWeight` ALWAYS means "higher = rarer = less common"
3. **Keep `budgetCost`** separate for materials (it's fundamentally different - direct cost, not probability)
4. **Update formulas** to use consistent `rarityWeight`

**Migration:**

```json
// BEFORE: material-pools.json
{
  "materialRef": "@materials/metals:iron",
  "selectionWeight": 60  // Common material
}

// AFTER: material-pools.json  
{
  "materialRef": "@materials/metals:iron",
  "rarityWeight": 60  // Higher number = less common (consistent with components)
}
```

**Code Update:**
```csharp
// BudgetCalculator.cs
public int CalculateComponentCost(JToken component)
{
    var rarityWeight = GetIntProperty(component, "rarityWeight", 0);
    if (rarityWeight <= 0)
    {
        _logger.LogWarning("Component {Name} missing rarityWeight", GetName(component));
        return 999999;
    }
    
    // Inverse formula: rarer components cost more budget
    var numerator = _config.Formulas.Component.Numerator ?? 100;
    return (int)Math.Round((double)numerator / rarityWeight);
}

public int CalculateMaterialCost(JToken material)
{
    // Materials use DIRECT cost (different system)
    var budgetCost = GetIntProperty(material, "budgetCost", 0);
    if (budgetCost <= 0)
    {
        _logger.LogWarning("Material {Name} missing budgetCost", GetName(material));
        return 999999;
    }
    return budgetCost;
}
```

---

### Option B: Distinct Systems (Keep Separate)

**Keep separate terms but DOCUMENT the difference:**

| Field Name | Purpose | Semantics | Used In |
|------------|---------|-----------|---------|
| **`rarityWeight`** | Random selection probability | Higher = LESS common | Catalogs (abilities, items, enemies) |
| **`selectionWeight`** | Budget affordability | Higher = MORE affordable | Components, enchantments in budget system |
| **`budgetCost`** | Material cost | Higher = MORE expensive | Materials only |

**Pros:**
- No migration needed
- Systems stay independent
- Budget system can have different semantics

**Cons:**
- Still confusing (two different "weight" meanings)
- Fallback code is unclear
- Hard to maintain

---

### Option C: Rename Everything (Nuclear Option)

**Eliminate ambiguity with explicit names:**

| Old Name | New Name | Purpose |
|----------|----------|---------|
| `rarityWeight` | `dropWeight` | Weighted random selection (higher = more common) |
| `selectionWeight` | `affordabilityScore` | Budget affordability (higher = cheaper) |
| `budgetCost` | `budgetCost` | Material cost (keep as-is) |
| `rarity` | `rarityTier` | Display tier (Common/Rare/etc.) |

**Pros:**
- Maximally explicit
- No semantic confusion

**Cons:**
- HUGE migration (192 JSON files, 50+ code files)
- Breaking change to JSON standards
- Breaks existing Godot integration

---

## Recommended Decision Tree

```
┌─────────────────────────────────────────────────────┐
│ Do rarityWeight and selectionWeight serve            │
│ fundamentally different purposes?                    │
└────────────┬────────────────────────────────────────┘
             │
      ┌──────┴──────┐
      NO            YES
      │              │
      ▼              ▼
  OPTION A      OPTION B
  Consolidate   Document
  to rarity-    distinct
  Weight        semantics
```

### My Recommendation: **Option A**

**Reasoning:**

1. **Both represent "how rare is this thing"** - same conceptual purpose
2. **Inverse formula works the same way** - `cost = numerator / weight`
3. **Simpler mental model** - one weight system, one cost system (budgetCost)
4. **Easier to maintain** - no confusion about which to use when

**Implementation Plan:**

1. ✅ Decide on Option A
2. Update all JSON files: `selectionWeight` → `rarityWeight`
3. Update budget system code to use `rarityWeight` only
4. Remove fallback logic (no longer needed)
5. Update documentation
6. Rebuild package
7. Test in Godot

---

## Questions for Decision

1. **Are `rarityWeight` and `selectionWeight` conceptually the same?**
   - If YES → Option A (consolidate)
   - If NO → Option B (document difference)

2. **Should weight semantics be inverted (higher = rarer)?**
   - Current: higher weight = more common
   - Alternative: higher weight = rarer (matches "rarity" naming)

3. **Should `budgetCost` be renamed for clarity?**
   - Current name is clear
   - Alternative: `materialBudgetCost` (more explicit)

4. **Do we keep the fallback logic?**
   - If consolidating → remove fallback
   - If keeping separate → document when to use which

---

## Impact Analysis

### Files to Update (if Option A chosen)

**JSON Files (~15 files):**
- `general/material-pools.json` - Change `selectionWeight` → `rarityWeight`
- All materials catalogs (if any have `selectionWeight`)

**C# Code Files (~8 files):**
- `BudgetCalculator.cs` - Remove fallback, use `rarityWeight` only
- `BudgetItemGenerationService.cs` - Update component selection
- `MaterialPoolService.cs` - Use `rarityWeight` for pool selection
- `MaterialReference.cs` - Rename property
- Tests for above

**Documentation:**
- Update budget system docs
- Update JSON standards
- Update CATALOG_JSON_STANDARD.md

### Risk Assessment

- **Low Risk**: Mostly internal to budget system
- **Medium Risk**: Requires package rebuild and Godot retesting
- **High Risk**: If JSON structure mismatches cause runtime errors

---

## Next Steps

**Please decide:**

1. Which option? (A, B, or C)
2. Should weight semantics be inverted?
3. Timeline for implementation?

Once decided, I can implement the changes immediately.
