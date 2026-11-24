---
kind: area
date: undated
area: engine
title: "CraftItem Handler Details (session-13)"
---

## CraftItem Handler Details (session-13)

- `DefaultCraftingCost = 50` — `internal const int` on `CraftItemHubCommandHandler`
- Blob key: `"Gold"` (same as `AddGold` / `RestAtLocation` handlers)
- Guard: if `currentGold < 50` → fails with `"Not enough gold to craft this item"`
- On success: `newGold = currentGold - 50`; saves character; returns `RecipeSlug` as `ItemCrafted`
- Broadcasts `ItemCrafted` payload: `{ CharacterId, RecipeSlug, GoldSpent, RemainingGold }`
- `ItemCraftedPayload` internal record lives in `CharacterSelectViewModel.cs`
- Client callback: `GameViewModel.OnItemCrafted(string recipeSlug, int goldSpent, int remainingGold)` → updates `Gold = remainingGold` + `AppendLog`
- **`DefaultCraftingCost` is `internal` — tests must hardcode `50` (not reference the constant directly)**
