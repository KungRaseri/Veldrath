---
kind: work-item
date: 2026-11-24
area: engine
card: t_b1af6084
title: "Crafting dispatches the skill award instead of duplicating it"
---

### Session-t_b1af6084 (2026-11-24) — The skill award has a dispatcher again

`AwardSkillXpHubCommand` had no server-side dispatcher at all after #269 retired the client-callable
`GameHub.AwardSkillXp` bridge. Crafting duplicated the award inline in `CraftItemHubCommandHandler
.AwardSkillExperience` — the same `Skill_{id}_XP` / `Skill_{id}_Rank` keys and the same `XpPerRank` rank size,
kept in step by a comment — and no Veldrath.Server combat path awards skill XP (engine-side combat uses
RealmEngine's own `AwardSkillXPCommand`, a different character model).

- **Option 1 (dispatch it from crafting), not option 2 (delete the command).** Deleting the command would have
  removed the only server-side way to award skill XP and still left two call sites hand-writing `XpPerRank`
  (`GetCharacterSkillsQuery`, `GameHub.CraftItem`), so the constant would have moved rather than the duplicate
  disappearing. Dispatching keeps one implementation and leaves `GameHub` untouched.
- **The craft handler owns the write.** `AwardSkillXpHubCommand` gained an `internal Character? LoadedCharacter`;
  when the crafting path sets it, the handler applies the award to that instance in memory and skips both the load
  and the `UpdateAsync`, because the craft already saves the attributes blob exactly once. Crafting sends the
  command after the output item is in the bag and folds `TotalXp`/`CurrentRank`/`RankedUp` into
  `CraftItemHubResult`, so a craft that crosses a rank still reports the rank-up it always did. The handler also
  refuses a supplied character whose `Id` is not the one the command names.
- **Deleted:** `CraftItemHubCommandHandler.AwardSkillExperience`, its private `SkillAward` struct and the
  `SkillXpSuffix` constant. Nothing else about the craft path changed: a recipe that trains nothing still awards
  nothing, and a craft with no XP still writes no character row.
- **Unchanged on purpose:** the `SkillXpGained` broadcast in `GameHub.CraftItem` stays — it is the client's only
  live skill-sheet update path and `web/client/src/lib/parity/hub-events.test.ts` requires a broadcast somewhere.
- **Pinned by tests.** `CraftItemHubCommandHandlerTests.Dispatches_the_award_and_writes_the_character_once` runs
  the real award handler behind a mocked mediator and asserts the command (character, `blacksmithing`, 15 XP), the
  resulting 110 XP / rank 1, and exactly one `UpdateAsync`; `Does_not_dispatch_an_award_when_the_recipe_trains
  _nothing` pins the no-award path. `GameHubTests` gains the caller-owned-write fact and the id-mismatch refusal.
  The existing award facts (accumulate, rank-up, refusals) and the corrupt-blob fact are untouched and still pass.
- **Measured.** `dotnet build Veldrath.Server.slnx` 0 warnings / 0 errors; `dotnet test
  Veldrath.Server.Tests` 1955 passed / 0 failed; `dotnet format Veldrath.Server.slnx --verify-no-changes` exit 0.
