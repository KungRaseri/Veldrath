---
kind: work-item
date: 2026-10-25
area: content
title: "HUD rework: character sheet, world column, dialog overlays"
---

### Session-70 (2026-10-25) — HUD rework: character sheet, world column, dialog overlays

- **Layout is now sheet / world / social.** Top bar = identity + zone breadcrumb + connection dot +
  `Characters`. Left = the full character sheet (HP/MP, gold/XP, 8-slot equipment rack, bag list,
  menu). Center = map → node detail → `Local` (enemies + Search/Rest/Merchant/Leave zone); combat
  replaces that block. Right = chat + "Also here". `GameFooter.svelte` was deleted — it duplicated the
  header (connection, map, occupants).
- **Drawers became centered dialogs.** Quest log / Shop / Settings are Bits UI `Dialog`s styled by
  `.game-dialog` + `.game-dialog-backdrop` in `brand.css`; `GameOverlays.svelte` owns all three. There
  is no inventory overlay any more — the bag lives in the sheet, and `GameSession.showInventory` was
  removed (`InventoryLoaded.openOverlay` is still sent but ignored).
- **Bag actions are per row**: Equip (only when `slot` is present), Unequip, Sell (merchant only) and
  Discard, which is a two-step inline confirm → `DropItem` (permanently removes one unit; no ground
  drop exists yet).
- **`ItemRarity`/`ItemType` serialised as numbers.** The old bag read `rarity: 2`, so `rarityClass()`
  matched nothing and every item rendered muted, and `itemSubtitle()` printed `Rare · 2`. Fixed by
  sending the flat `InventoryItemView` projection with string `Rarity`/`Type` instead of the raw
  `Item` graph (which also dropped the socket/enchantment payload).
- **Equip needs no slot from the client.** `EquipItemHubRequest(string? Slot, string? ItemRef)`; the
  server derives the slot from the item type when `Slot` is omitted.
- **Tests**: `components.test.ts` rewritten around `GameHeader` + `LeftSidebar` (vitals, rack, bag
  badges/actions, menu) and the two dialogs; `types.test.ts` covers `rarityClass` / `itemSubtitle` /
  `isEquippable` / `EQUIPMENT_SLOTS`. Web: 69 unit tests + 22 e2e. Server: 677 tests.
- **Known gameplay gaps surfaced by this pass** (next candidates): no `UseItem` for consumables, no
  real ground drop, two-handed weapons do not clear `OffHand`, `Shoulders`/`Bracers`/`Gloves`/`Belt`
  have no slots, unspent attribute points can be shown but not allocated from the sheet.
