---
kind: area
date: undated
area: engine
title: "Equipment Blob Schema (session-8)"
---

## Equipment Blob Schema (session-8)

The blob schema below is still current. This note used to end with the Avalonia client's `GameViewModel`
callback; that client was deleted in session-68 (2026-10-25), so the client half now names the web session
store.

- `Character.EquipmentBlob` separate `text` column, default `"{}"` — `Dictionary<string, string>` JSON (slot → item-ref slug). It ships with `20260416172040_InitialCreate`; there is no separate `AddEquipmentBlob` migration and no `Migrations/Application/` directory.
- Handler: `EquipItemHubCommandHandler` in [`Veldrath.Server/Features/Characters/EquipItemHubCommand.cs`](../../../Veldrath.Server/Features/Characters/EquipItemHubCommand.cs)
- Valid slots are the `EquipmentSlots` constants in [`InventoryItemView.cs`](../../../Veldrath.Server/Features/Characters/InventoryItemView.cs), compared case-insensitively: `MainHand`, `OffHand`, `Head`, `Shoulders`, `Chest`, `Bracers`, `Gloves`, `Belt`, `Legs`, `Feet`, `Ring`, `Amulet`
- Slot names are normalised to the canonical constant's casing (`ValidSlots.FirstOrDefault(...)`); an unknown slot fails with `Invalid equipment slot '<name>'.`
- With `Slot` omitted the handler derives it from the item's catalog type, and fails when the item is not equippable; a slot is required to unequip
- Pass `null` for `ItemRef` to unequip a slot
- A two-handed main-hand weapon empties the off hand and reports it in `ClearedSlots`; while it occupies the main hand, an off-hand equip is refused outright
- `GameHub.EquipItem` broadcasts `ItemEquipped` to the zone group (to the caller when not in a zone) with `{ CharacterId, Slot, ItemRef, AllEquippedItems, ClearedSlots, Equipped }`, then sends the refreshed private `InventoryLoaded` snapshot to the caller only, with `OpenOverlay = false`
- Hub method takes `EquipItemHubRequest(string? Slot, string? ItemRef)` in the `Veldrath.Server.Hubs` namespace
- Web client ([`session.svelte.ts`](../../../web/client/src/lib/game/session.svelte.ts)): `equipItem(itemRef)` invokes `EquipItem` with `{ itemRef }` alone — the server derives the slot — and `unequip(slot)` sends `{ slot, itemRef: null }`. The `ItemEquipped` subscriber replaces `equipped` from the payload and logs the unequip and each two-handed `clearedSlots` line
