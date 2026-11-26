---
kind: work-item
date: 2026-10-25
area: content
title: "Gameplay gaps closed: items, ground drops, quests, attributes, abilities"
---

### Session-71 (2026-10-25) — Gameplay gaps closed: items, ground drops, quests, attributes, abilities

- **The bag loads on entry.** `selectCharacter` now requests `GetInventory` after `CharacterSelected`
  and stores `attributes` from that payload, so the sheet renders vitals, rack and bag without a
  refresh. `showInventory` is gone; the sheet is the only inventory surface.
- **`InventoryItemView` grew** to `Effect`/`EffectPower`, `IsTwoHanded`, `ArmorClass`/`ArmorType`/
  `WeaponType`, `DamageMin`/`DamageMax`, `RequirementsLevel`/`Requirements`, `Bonuses`. That feeds the
  item detail dialog (damage, armour, durability, effect, requirements, bonuses, value, stack).
- **`UseItem` command + hub** (`ItemUsed`): `heal`, `heal_overtime`, `restore`, `cure_poison` consume
  one unit, edit the Attributes blob, and the hub then refreshes the bag. Unknown effects fail loudly.
- **Two-handed weapons** own both hands: equipping one frees `OffHand` (`EquipItemHubResult.ClearedSlots`
  → `ItemEquipped.ClearedSlots`), and off-hand items are rejected while a two-handed main hand is worn.
- **Twelve equipment slots**: `Shoulders`, `Bracers`, `Gloves`, `Belt` joined the eight in
  `EquipmentSlots` + `ValidSlots` + `EQUIPMENT_SLOTS` (web order: MainHand, OffHand, Head, Shoulders,
  Chest, Bracers, Gloves, Belt, Legs, Feet, Ring, Amulet).
- **GroundItems table** (zone + location keyed, quantity, durability, dropped-by) with commands
  `GetGroundItems` / `DropItemToGround` / `PickUpGroundItem` and the `GroundItemsChanged` broadcast.
  Drop is *not* destroy: `DropItem` still permanently removes. The client refreshes the ground list on
  every `LocationEntered`.
- **Quests are real now**: `Character.QuestBlob` journal, `QuestJournal.Advance(kind, key)`, and
  `AcceptQuest` / `AbandonQuest` / `TurnInQuest`. Objective keys are `kill:<enemy>`, `collect:<item>`,
  `visit:<location>`; the hub advances them from `AttackEnemy`/`UseAbilityInCombat` (`EnemyName` was
  added to both results), `BuyItem`, `PickUpGroundItem` and `NavigateToLocation`. Turn-in grants gold/XP
  via `AddGoldHubCommand` / `GainExperienceHubCommand` so level-ups stay in one place. The old
  save-game-backed quest log is gone (server characters never had a save game).
- **`GameHub` payload gotcha for the contract test**: `payload-contract.test.ts` only reads
  `Name = value` properties and inline `new { ... }` payloads. Member shorthands (`result.Items`) and
  `new SomeRecord(...)` are invisible to it — write explicit `Name = ...` assignments or inline
  anonymous objects, otherwise the guard reports "not on the wire".
- **Attributes + abilities**: the sheet's attribute strip opens a dialog that accumulates points and
  submits capitalised keys to `AllocateAttributePoints`; `AttributePointsAllocated` updates the values.
  The abilities dialog lists `learnedAbilities` and casts them out of combat via `UseAbility`.
- **Search results** are kept in `lastSearch` and rendered in the Local panel; a discovery that matches
  a reachable node offers travel.
- Verification: 693 server tests, 79 web unit tests, 22 Playwright specs, dotnet format clean.
