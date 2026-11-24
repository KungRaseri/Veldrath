---
kind: work-item
date: 2026-10-22
area: engine
title: "Character creation equipment dropdowns fixed"
---

### Session-50 (2026-10-22) — Character creation equipment dropdowns fixed

- **Root cause (Avalonia wizard)**: `AvailableArmorTypes` / `AvailableWeaponTypes` are computed from `_equipmentCatalog`, but assigning the catalog after the async session load never raised `PropertyChanged`. The ComboBoxes were bound while the catalog was null and never refreshed, so both lists stayed empty.
- `CreateCharacterViewModel` now assigns the catalog through `SetEquipmentCatalog(...)`, which raises change notifications for both lists.
- **Second bug**: step 5 derived API slugs from display names (`"Light Armor"` → `light-armor`). It now maps display names back to `EquipmentTypeOptionDto.Slug` through the catalog (`light`, `sword`, …).
- `FakeCharacterCreationService` records `LastEquipmentPreferences` and supports a `BeginSessionGate` for async-notification tests.
- Tests: 3 new `CreateCharacterViewModelTests` (catalog populated, change notifications raised, canonical slugs sent). All 44 CreateCharacter tests pass.
- Note: the web/RCL wizard already bound slugs directly and re-renders after load, so it was unaffected.

---
