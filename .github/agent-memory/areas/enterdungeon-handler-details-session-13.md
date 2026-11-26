---
kind: area
date: undated
area: engine
title: "EnterDungeon Handler Details (session-13)"
---

## EnterDungeon Handler Details (session-13)

- Uses `IZoneRepository.GetByIdAsync(dungeonSlug)` — `Zone.Id` IS the slug (e.g. `"dungeon-grotto"`).
  **`IDungeonRepository` does NOT exist** (grep confirms); earlier iteration prompt notes were fiction.
- `IZoneRepository` is registered in `Program.cs` alongside `IZoneLocationRepository` — no new DI registration needed.
- Validates, in order: empty slug → "Dungeon slug cannot be empty"; unknown zone → `"Dungeon '{slug}' not found"`;
  `zone.Type != ZoneType.Dungeon` → `"'{name}' is not a dungeon"`.
- The handler then resolves the dungeon's entrance location (the safe-respawn room — the lobby — falling back to
  the first non-`dungeons` location) and delegates the actual move to `EnterZoneLocationHubCommand`. A zone with no
  locations at all fails with `"{name} has no entrance location"`; the arrival result's own error is passed through.
- `EnterDungeonHubResult`: `Success`, `ErrorMessage`, `DungeonId` (the zone id), `LocationSlug` (the entrance),
  `RecommendedLevel`, `YourLevel`, `LevelWarning` (the advisory wording for an under-levelled arrival).
- `GameHub.EnterDungeon(string dungeonSlug)` sends `DungeonEntered` **to the caller** with
  `{ CharacterId, DungeonId, DungeonSlug, LocationSlug, RecommendedLevel, YourLevel, LevelWarning }`, and sends
  `PlayerEntered` to the others in the zone group. `DungeonSlug` is the slug the client asked for;
  `DungeonId` is the zone the handler resolved.
- The payloads are anonymous objects — there is no C# `DungeonEnteredPayload` record, and nothing in
  `web/client/src/lib/game/` subscribes to `DungeonEntered` yet. `web/client/src/lib/parity/hub-events.test.ts`
  records it as deferred ("dungeon UI does not exist yet"), so a web consumer is the missing half, not the server.
- Handler tests build their own zone and entrance locations through the two repository abstractions
  (`Veldrath.Server.Tests/Features/EnterDungeonHubCommandHandlerTests.cs`); they do not depend on a seeded
  dungeon row.
