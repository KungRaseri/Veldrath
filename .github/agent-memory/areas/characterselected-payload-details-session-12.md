---
kind: area
date: undated
area: auth
title: "CharacterSelected Payload Details (session-12)"
---

## CharacterSelected Payload Details (session-12)

- `GameHub.SelectCharacter` now deserializes `Character.Attributes` blob before broadcasting
- Broadcasting `CharacterSelected` includes: `Id, Name, ClassName, Level, Experience, CurrentZoneId, CurrentHealth, MaxHealth, CurrentMana, MaxMana, Gold, UnspentAttributePoints, SelectedAt`
- Defaults when blob empty: `MaxHealth = Level * 10`, `MaxMana = Level * 5`, `CurrentHealth = MaxHealth`, `CurrentMana = MaxMana`, `Gold = 0`, `UnspentAttributePoints = 0`
- Client: `CharacterSelectedPayload(Guid Id, string Name, string ClassName, int Level, long Experience, string CurrentZoneId, int CurrentHealth, int MaxHealth, int CurrentMana, int MaxMana, int Gold, int UnspentAttributePoints, DateTimeOffset SelectedAt)` internal record in `CharacterSelectViewModel.cs`
- Client subscription: `_characterSelectedSub` — 15th subscription total (14th active; `_characterStatusSub` declared but reserved for future online-status tracking)
- All 8 seeded values go to `_gameVm.SeedInitialStats(...)` — eliminates zero-HUD on login
