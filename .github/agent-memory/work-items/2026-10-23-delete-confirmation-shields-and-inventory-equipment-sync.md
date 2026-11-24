---
kind: work-item
date: 2026-10-23
area: auth
title: "Delete confirmation, shields, and inventory/equipment sync"
---

### Session-52 (2026-10-23) — Delete confirmation, shields, and inventory/equipment sync

**Character deletion confirmation**
- Avalonia `CharacterSelectViewModel` now exposes `RequestDeleteCommand` / `CancelDeleteCommand`; `DeleteCommand` still performs the actual deletion.
- `CharacterEntryViewModel.IsPendingDelete` / `IsNotPendingDelete` drive the inline Delete → Confirm/Cancel buttons in `CharacterSelectView.axaml`. Only one entry can be pending at a time, and a failed delete clears the pending state.
- The RCL `CharacterSelect` already had an inline confirm flow.

**Shield granted when requested**
- `CreateCharacterHandler.SelectStartingEquipment` now filters shields out of the armor candidates (they previously could be equipped to Chest), detects shields via `TypeKey == "shield"` / `ItemType.Shield` / `ArmorType`, and synthesizes a `wooden-shield` fallback when the content catalog has no shield item.
- `EquipArmorToSlot` routes shields to `OffHand` and never to an armor slot.
- `FinalizeCreationSessionAsync` writes canonical slot names: `MainHand`, `OffHand`, `Head`, `Chest`, `Legs`, `Feet`, `Ring`, `Amulet` (Helmet→Head, Boots→Feet, Necklace→Amulet) so clients can display starter gear.

**Inventory duplication and equipment visibility**
- `GetInventoryHubCommand` now injects `IItemRepository`, filters equipped refs out of the bag list, and returns full `ItemDetails` plus a slot→`Item` `Equipped` map.
- `InventoryLoaded` payload shape is `{ CharacterId, Items, ItemDetails, Equipped, EquippedItems }`: `Equipped` is the slot→ref map (Avalonia) and `EquippedItems` is the slot→`Item` map (RCL). Keep those two names distinct — binding full item objects into the Avalonia string-ref map throws during SignalR deserialization and the inventory overlay never opens.
- `GetInventoryHubCommandResult` filtering means an equipped item is no longer listed in the bag; unequipping makes it reappear because the inventory blob still contains it.
- `GameHub.EquipItem` now sends a resolved `Equipped` map in `ItemEquipped` and follows it with a caller-only `InventoryLoaded` snapshot, removing the doubled-item view immediately after equipping.
- `CharacterSelected` now includes `Equipment` (slot→ref); Avalonia applies it in `SeedInitialStats` and the RCL seeds placeholder `GameState.EquippedItems`, so starter items appear equipped at login.
- `EfCoreItemRepository.MapToModel` now maps the model `ItemType` (weapon/shield/armor slot) from the persisted discriminator and category.
- Avalonia `InventoryItemViewModel.EquipSlot` routes Equip to the correct slot (shield→OffHand, helm→Head, etc.); the RCL `GuessSlot` now handles `ItemType.Shield` before the armor branch.

**Tests**
- New: 4 Avalonia delete-confirmation tests, 2 shield-granting tests, 2 server inventory-snapshot tests, 2 Avalonia inventory-slot tests, 1 EquipmentSelected-seeding test, 2 item-mapping tests.
- Full suite: **4,780 passing** across 13 projects; `Realm.Full.slnx` builds with 0 warnings / 0 errors.
- Runtime note: restart the server and rebuild the client; existing characters keep their existing blobs, so only newly created characters show the synthesized shield.

---
