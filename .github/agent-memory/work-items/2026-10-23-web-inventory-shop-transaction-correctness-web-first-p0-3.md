---
kind: work-item
date: 2026-10-23
area: engine
title: "Web inventory/shop transaction correctness (web-first P0-3)"
---

### Session-54 (2026-10-23) — Web inventory/shop transaction correctness (web-first P0-3)

**Shop**
- RCL `ShopOverlay` now sends `BuyItem`/`SellItem` with the bare item slug. It previously sent `{ ItemSlug, Quantity }`, which cannot bind to the server's `BuyItem(string itemRef)` / `SellItem(string itemRef)`.
- `Game.razor` now requests `GetShopCatalog` after `ShopVisited`; the server sends both `Items` (Avalonia `ShopItemDto`) and `Catalog` (RCL `ShopItemEntry`: slug/name/rarity/price).
- Removed `ShopOverlay`'s dead `ShopCatalogUpdated` subscription — `Game.razor` owns the `ShopCatalog` handler.

**Transactions**
- `ItemPurchased`/`ItemSold`/`ItemDropped` RCL payloads now match the server fields; handlers update gold/messages only.
- Server sends a background `InventoryLoaded` snapshot after buy/sell/drop (best-effort; wrapped in `TrySendInventorySnapshotAsync`).
- `InventoryLoaded` now carries `OpenOverlay`; `EquipItem`, buy, sell, and drop snapshots set it false so the current overlay does not switch. Explicit `GetInventory` still opens the inventory panel.
- `InventoryOverlay` equipment panel uses `GetDisplayName()` (was `GetShortName()`, which rendered blank for catalog items without name components).
- Removed the dead `Use` button (`UseItem` hub method does not exist).

**Tests**
- New `WebInventoryInteractionTests` (6 tests): shield→OffHand equip payload, drop slug, no bag duplication of equipped gear, no Use button, buy slug, sell slug.
- Components tests **98 passing**; Core tests 51 passing; targeted server shop/transaction tests 29 passing.

---
