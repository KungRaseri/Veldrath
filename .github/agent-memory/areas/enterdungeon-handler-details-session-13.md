---
kind: area
date: undated
area: engine
title: "EnterDungeon Handler Details (session-13)"
---

## EnterDungeon Handler Details (session-13)

- Uses `IZoneRepository.GetByIdAsync(dungeonSlug)` — `Zone.Id` IS the slug (e.g., `"dungeon-grotto"`)
- **`IDungeonRepository` does NOT exist** — earlier iteration prompt note was a fiction; use `IZoneRepository`
- `IZoneRepository` is already registered in `Program.cs` — no new DI registration needed
- Validates `zone.Type == ZoneType.Dungeon`; fails with `"'{name}' is not a dungeon"` for non-dungeon zones
- Fails with `"Dungeon '{slug}' not found"` for missing zone; fails on empty slug
- Returns `zone.Id` as `DungeonId` in result
- Broadcasts `DungeonEntered` payload: `{ CharacterId, DungeonId, DungeonSlug }`
- `DungeonEnteredPayload` internal record lives in `CharacterSelectViewModel.cs`
- Client callback: `GameViewModel.OnDungeonEntered(string dungeonId, string dungeonSlug)` → `AppendLog`
- Seeded dungeon available in `TestDbContextFactory` (SQLite + `EnsureCreated`): **REPLACED in session-14 — use `"verdant-barrow"` (Verdant Barrow, ZoneType.Dungeon)**
