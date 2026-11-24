---
kind: work-item
date: 2026-10-23
area: auth
title: "First-login map, starting equipment, and map button fixed"
---

### Session-51 (2026-10-23) — First-login map, starting equipment, and map button fixed

**Map failed to load (GetMapGraph)**
- Root cause: `GameHub.GetMapGraph(string? mapId = null)` used an optional parameter. SignalR does not supply default values, so zero-argument invocations failed during binding ("Failed to invoke 'GetMapGraph' due to an error on the server") before the method body ran — no MediatR log entry appeared.
- Fix: the hub parameter is now required (`GetMapGraph(string? mapId)`); all callers send an explicit null or map id:
  - `MapHubCommands.RequestMapGraphAsync` always sends the argument.
  - Avalonia `GameViewModel` and `CharacterSelectViewModel` send `null` via `SendCommandAsync<object?>`.
- Server restart **and client rebuild** are required, since old clients still send zero arguments.

**Starting equipment not added**
- Server log showed `Loading equipment for class Warrior (Armor: , Weapons: )` followed by `Loaded 0 weapons and 0 armor items` and `No equipment found for class warrior`.
- Root cause: `EfCoreCharacterClassRepository.MapToModel` hardcoded `ArmorProficiency = []` and `WeaponProficiency = []`; `ActorClass` only stores playstyle traits.
- Fix: derive armor proficiencies (`light`, plus `medium`/`heavy` when `CanWearHeavy`, plus `shields` when `CanWearShield`) and weapon proficiencies (`simple`/`daggers` baseline, plus melee/ranged/spellcaster/stealth/dual-wield categories) from `ActorClassTraits`.
- `CreateCharacterHandler.SelectStartingEquipment` no longer returns early when no weapons match; it logs and still equips armor.

**Tests**
- New repository test: `GetById_Derives_Equipment_Proficiencies_From_Class_Traits`.
- Updated map-state test and RCL teleport test for the explicit null `GetMapGraph` argument.
- Full suite expected green; build clean.

---
