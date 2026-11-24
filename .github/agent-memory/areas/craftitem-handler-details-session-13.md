---
kind: area
date: undated
area: engine
title: "CraftItem Handler Details (session-13)"
---

## CraftItem Handler Details (session-13)

> **Rewritten 2026-11-24.** This note used to claim an internal `DefaultCraftingCost = 50`, a 50-gold
> crafting fee, a "Not enough gold to craft" guard, and an `ItemCraftedPayload` record inside
> `CharacterSelectViewModel.cs`. None of that exists: `grep -rn "DefaultCraftingCost" --include=*.cs .`
> is empty, `CraftItemHubCommandHandler` never reads or writes the `Gold` key, and the Avalonia client
> that owned those view models is gone. Crafting costs materials and a skill rank, never gold.

- `CraftItemHubCommand(Guid CharacterId, string RecipeSlug) : IRequest<CraftItemHubResult>` in
  `Veldrath.Server/Features/Characters/CraftItemHubCommand.cs`. Hub method `GameHub.CraftItem(string recipeSlug)`.
- Checks, in order: blank slug, character exists, recipe exists, recipe has an `OutputItemReference`,
  `recipe.RequiredLevel <= character.Level`, required skill rank (`Skill_{skillId}_Rank` key in the attributes
  blob) `>= recipe.RequiredSkillLevel`, then every material's owned quantity.
- Materials are checked in full before any is consumed, and consumption compensates with a refund when a later
  removal or the output add fails — a partial craft cannot happen.
- On success it adds `OutputQuantity` (min 1) of `recipe.OutputItemReference` to the bag; the inventory service
  is keyed by character **name**, not id.
- Skill XP: when the recipe names a skill and `ExperienceGained > 0`, the handler dispatches
  `AwardSkillXpHubCommand` with the character it already loaded, so the award mutates the blob in memory and the
  handler's single `UpdateAsync` persists craft and award together. A failed award is logged and the craft still
  succeeds with `SkillXp = 0`.
- `CraftItemHubResult` carries `Success`, `ErrorMessage`, `RecipeSlug`, `RecipeName`, `ItemRef`,
  `OutputQuantity`, `ConsumedMaterials` (`ItemRef`, `Quantity`), `SkillId`, `SkillXp`, `SkillTotalXp`,
  `SkillRank`, `SkillRankedUp`. No gold field exists.
- Announcement: `GameHub.CraftItem` sends `ItemCrafted` to the zone group, or to the caller when it is not in a
  zone, with `{ CharacterId, RecipeSlug, RecipeName, ItemRef, OutputQuantity, ConsumedMaterials, SkillId,
  SkillXp }`. A second `SkillXpGained` announcement follows when the craft trained a skill. Both payloads are
  anonymous objects — there is no C# `ItemCraftedPayload` record.
- Web client: `ItemCraftedPayload` in `web/client/src/lib/game/types.ts`; `Session` subscribes with
  `hub.on('ItemCrafted')` in `web/client/src/lib/game/session.svelte.ts` and renders `craftedSummary(...)`
  (`web/client/src/lib/game/crafting.ts`) before refreshing the bag with `openInventory()`, so the sheet never
  shows materials the craft consumed. The craft itself is sent as `hub.invoke('CraftItem', recipeSlug)`.
