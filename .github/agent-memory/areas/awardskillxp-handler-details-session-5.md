---
kind: area
date: undated
area: engine
title: "AwardSkillXp Handler Details (session-5)"
---

## AwardSkillXp Handler Details (session-5)

> Corrected 2026-11-24 (t_4085cec4). Session-5 described a client-callable hub bridge and an Avalonia payload
> record; both are retired. The mechanism below is the one on `development`.

- Blob keys: `Skill_{SkillId}_XP` (int) and `Skill_{SkillId}_Rank` (int) in the character's attributes JSON blob
- `XpPerRank = 100` — an `internal const int` on `AwardSkillXpHubCommandHandler`. `GetCharacterSkillsQuery` and
  `GameHub.CraftItem` read that constant instead of restating `100`
- `newRank = newXp / XpPerRank`; `rankedUp = newRank > previousRank`, where `previousRank` is the stored
  `Skill_{SkillId}_Rank` value — not a rank re-derived from the accumulated XP
- **No client-callable hub method.** `AwardSkillXpHubCommand` has exactly one production caller,
  `CraftItemHubCommandHandler` (`Veldrath.Server/Features/Characters/CraftItemHubCommand.cs`). The
  `GameHub.AwardSkillXp` bridge was retired 2026-11-24 (#269), and
  `GameHubTests.GameHub_Does_Not_Expose_ClientCallableProgression_Or_Damage_Methods` pins its absence
- **The craft handler owns the character-row write.** Crafting passes the character it already loaded through
  `internal Character? LoadedCharacter`, so the award handler applies the award to that instance in memory and
  skips both its own load and its own `UpdateAsync`. `CraftItemHubCommandHandler.Handle` then makes the one
  character-row `UpdateAsync` after the dispatch, which writes that row once: `InventoryBlob` (materials taken,
  output added — the bag the client reads) and `Attributes` (the award), plus `LastPlayedAt` when an award was
  made. It is one row in one save, and there is no transaction spanning anything else. The craft does not go
  through `IInventoryService`: `EfCoreInventoryService` writes `GameDbContext.InventoryRecords`, a table no
  server path populates, so the old craft path demanded materials the player was carrying and put its output
  where no client could see it (fixed 2026-11-24, t_fd868f7e)
- A supplied `LoadedCharacter` whose `Id` is not the command's `CharacterId` is refused
- Without `LoadedCharacter` the handler loads the character and saves it itself, so the command still works
  standalone — but nothing in production dispatches it that way (the server tests call the handler directly)
- Broadcast: `GameHub.CraftItem` sends `SkillXpGained` to the character's zone group (to the caller when it is in
  none) after the award, with `{ CharacterId, SkillId, TotalXp, CurrentRank, RankedUp, XpIntoRank, XpToNextRank }`.
  `XpIntoRank = TotalXp % XpPerRank`; `XpToNextRank = XpPerRank`, a constant rank size rather than the XP left in
  the current rank
- That broadcast is sent only when `CraftItemHubResult.SkillXp > 0` and `SkillId` is non-empty, so a recipe that
  trains nothing — or an award that failed — announces no `SkillXpGained`
- Client side: `web/client/src/lib/game/session.svelte.ts` subscribes `hub.on('SkillXpGained', …)` and applies it
  in `applySkillXp`; the payload type is `SkillXpGainedPayload` in `web/client/src/lib/game/types.ts`. The
  Avalonia payload records session-5 listed went with the desktop client
