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
- Materials are checked in full before any is consumed, and consumption is all-or-nothing on the in-memory bag
  (`CharacterBag.Take`), so a refused craft writes nothing and a partial craft cannot happen.
- The bag is the character's own `InventoryBlob` (`CharacterBag`), matching either the bare slug or the full
  reference on both sides. On success it adds `OutputQuantity` (min 1) of `CharacterBag.Slug(recipe.OutputItemReference)`
  and saves the blob. The engine's `IInventoryService` is not on this path: it writes
  `GameDbContext.InventoryRecords`, which nothing populates, so crafting through it read every material as unowned
  and stored the output where no client read it (fixed 2026-11-24, t_fd868f7e).
- Skill XP: when the recipe names a skill and `ExperienceGained > 0`, the handler dispatches
  `AwardSkillXpHubCommand` with the character it already loaded, so the award mutates the attributes blob in memory
  and the handler's single `UpdateAsync` writes one character row: the bag (`InventoryBlob`), the award
  (`Attributes`) and, when an award was made, `LastPlayedAt`. A failed award is logged and the craft still succeeds
  with `SkillXp = 0`; the bag is saved either way, because it changed.
- `CraftItemHubResult` carries `Success`, `ErrorMessage`, `RecipeSlug`, `RecipeName`, `ItemRef`,
  `OutputQuantity`, `ConsumedMaterials` (`ItemRef`, `Quantity`), `SkillId`, `SkillXp`, `SkillTotalXp`,
  `SkillRank`, `SkillRankedUp`. No gold field exists.
- Announcement: `GameHub.CraftItem` sends `ItemCrafted` to the zone group, or to the caller when it is not in a
  zone, with `{ CharacterId, RecipeSlug, RecipeName, ItemRef, OutputQuantity, ConsumedMaterials, SkillId,
  SkillXp }`. A second `SkillXpGained` announcement follows when the craft trained a skill. Both payloads are
  anonymous objects — there is no C# `ItemCraftedPayload` record. After the announcements the hub re-sends the
  caller's inventory snapshot and the craftable recipe list, so the new item and the spent materials show up
  without a manual refresh (t_fd868f7e).
- Web client: `ItemCraftedPayload` in `web/client/src/lib/game/types.ts`; `Session` subscribes with
  `hub.on('ItemCrafted')` in `web/client/src/lib/game/session.svelte.ts` and renders `craftedSummary(...)`
  (`web/client/src/lib/game/crafting.ts`) before refreshing the bag with `openInventory()`, so the sheet never
  shows materials the craft consumed. The craft itself is sent as `hub.invoke('CraftItem', recipeSlug)`.
