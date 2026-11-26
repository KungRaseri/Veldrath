---
kind: work-item
date: 2026-11-25
area: engine
card: t_a0369099
title: "A shared-zone roster is the caller's difficulty shard too, not just the caller's zone"
---

### Session t_a0369099 (2026-11-25) — The shared roster follows the difficulty shard

Follow-up to [`2026-11-25-t_1708ed7c-a-roster-is-the-callers-scope.md`](2026-11-25-t_1708ed7c-a-roster-is-the-callers-scope.md)
(PR #323), which scoped every `ZoneEntered` occupant list to the caller's **scope** but only along the run-vs-shared
dimension. A wilderness zone splits its shared scope by difficulty as well — `GameHub.ComputeZoneGroup` names the group
`zone:<zoneId>_<difficulty>` — and `GetOnSharedScopeAsync` did not know that dimension existed: it read
`ZoneId == zoneId && ZoneInstanceId == null`, so a normal-difficulty arrival in the Droveway was handed a roster naming
a hardcore character standing in the same zone on the other side of the split. A name disclosure, plus a durable ghost
in `session.occupants`: the other shard never sends `PlayerLeft` to this connection, so the entry survived until the
next `ZoneEntered`.

**What changed**

- `IPlayerSessionRepository.GetOnSharedScopeAsync(string zoneId)` → `GetOnSharedZoneAsync(string zoneId, string?
  difficultyMode)`. The second parameter is **required**, not defaulted, so a call site cannot skip the dimension the
  way the previous name let it. `null` means the zone's shared scope is not split by difficulty; a value means the
  roster is the caller's own shard. The XML now names both dimensions and states the invariant: the pair is the same
  pair `ZoneGroupName.ForZone` composes the scope name from.
- `PlayerSessionRepository.GetOnSharedZoneAsync` keeps the run filter and, when a difficulty is supplied, joins
  `Character` and requires `Character.DifficultyMode == difficultyMode`. `PlayerSession` stores no difficulty
  (`Veldrath.Server/Data/Entities/Character.cs:86` is the only record of it), so the join is the read. The whole-zone
  read `GetByZoneIdAsync` is untouched — the public counts still publish both shards' populations.
- `GameHub` gained `SharedScopeDifficulty(zoneType, difficultyMode)` — wilderness carries the mode, every other zone
  type is zone-wide — and `CurrentDifficultyMode()`, which replaces the inline `Context.Items["DifficultyMode"]`
  fallback `EnterZone` had and is now shared with `SyncArrivedZoneAsync`. Both roster reads (the `EnterZone` shared
  branch and the border-walk snapshot) pass `SharedScopeDifficulty(zone.Type, …)`.
- `ComputeZoneGroup` now delegates to `ZoneGroupName.ForZone(...)` instead of re-spelling `$"zone:{zoneId}_{mode}"`.
  The format was documented as composed in one place, and the roster read now depends on the same pair, so a second
  spelling of the rule was a second way for the name and the roster to disagree.
- Stable reference updated: `.github/agent-memory/areas/session-29-2026-04-08-region-map-architecture-playersession-migration.md`
  named the removed method and described only the run dimension in both places it discusses the scoped reads.

**Tests — 4 new cases, 1 strengthened.**

- `PlayerSessionRepositoryTests.GetOnSharedZoneAsync_ReadsOnlyTheCallersDifficultyShard_InAWildernessZone` — normal and
  hardcore sessions in `the-droveway`'s shared rooms; each shard read returns only its own session while
  `GetByZoneIdAsync` still counts both.
- `PlayerSessionRepositoryTests.GetOnSharedZoneAsync_SpansDifficulties_WhenTheZoneIsNotSplitByThem` — the `null` read
  keeps one roster for a town, so the fix cannot hide a player the caller can see.
- `GameHubZoneTests.EnterZone_Roster_In_A_Wilderness_Zone_Lists_Only_The_Callers_Difficulty_Shard` — the card's own
  reproduction: enter `the-droveway` as normal with a hardcore neighbour standing there; the caller joins
  `zone:the-droveway_normal` and the `ZoneEntered` roster names the normal resident and the caller, not the hardcore
  character.
- `GameHubZoneTests.EnterZone_Roster_In_A_Town_Lists_The_Other_Difficulty` — pins the wilderness-only half of the
  rule from the other side: a hardcore character in Crestfall is in `zone:crestfall` with the caller and must stay in
  the roster.
- `GameHubZoneTests.TravelToNode_Crossing_A_Border_Lists_Only_The_Shared_Scope` →
  `…_Lists_Only_The_Callers_Shared_Scope`: the border-walk case the review flagged as proving only "not the run". It
  now seeds a hardcore neighbour beside the resident and the mid-delve party, so it proves "my group" as well.

**Negative controls, one at a time on the working tree (each restored from a copy afterwards).**

- `SharedScopeDifficulty` always returns `null` (the hub stops passing the shard): 2 failed — the wilderness
  `EnterZone` case and the border-walk case — 2 passed (the repository cases, which call the read directly).
- The repository predicate dropped (difficulty ignored, run filter only): 3 failed — the repository shard case, the
  wilderness `EnterZone` case and the border-walk case — 1 passed (the town-spans case, which asserts the `null` path).
- `SharedScopeDifficulty` always returns the mode (towns filtered too): exactly 1 failed — the town roster case.

**Gates** — `dotnet build Veldrath.Server.slnx` (0 warnings, 0 errors), `dotnet test Veldrath.Server.slnx`
(6642 passed / 0 failed: Shared 1158, Core 2748, Data 697, Server 2039 — the four new cases are the difference),
`dotnet format Veldrath.Server.slnx --verify-no-changes` (exit 0), `node scripts/memory/memory-index.mjs --check`
and `node scripts/check-markdown-links.mjs` at the root.

**Reachability — this one is live on shipped content, unlike the run half of the same rule.** It needs no instanced
location: `the-droveway` is a seeded wilderness zone and every character has a `DifficultyMode`, so a normal and a
hardcore character standing in the same wilderness zone is ordinary play, and the leak was the roster a player sees on
arrival.

**Noticed and deliberately not changed.**

- `LeaveExpiredInstanceAsync` (the respawn wake for a run that was purged while the character was dead) still composes
  the shared group with `ZoneGroupName.ForZone(zoneId)` and no difficulty, so in a wilderness zone it would put a
  character in `zone:the-droveway` while their shard is `zone:the-droveway_normal`. Same class, different symptom —
  the character ends up in a group nobody else is in rather than being shown one they cannot reach. It is currently
  unreachable because no seeder sets `Traits.IsInstanced`, so it is filed as its own card rather than widened into
  this diff.
- `/who` still reads the whole zone. Already filed against PR #323 as its own card; not duplicated here.
