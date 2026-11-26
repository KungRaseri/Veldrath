---
kind: work-item
date: 2026-11-25
area: engine
card: t_a3050703
title: "/who names the caller's scope, not the zone's population"
---

### Session t_a3050703 (2026-11-25) — `/who` reads the caller's own scope

Last of the three leaks the review of PR #323 filed. [`2026-11-25-t_1708ed7c-a-roster-is-the-callers-scope.md`](2026-11-25-t_1708ed7c-a-roster-is-the-callers-scope.md)
scoped every `ZoneEntered` occupant list, and [`2026-11-25-t_a0369099-a-roster-is-the-shard.md`](2026-11-25-t_a0369099-a-roster-is-the-shard.md)
added the difficulty dimension to the shared read — but the `who` case of `GameHub.HandleChatCommandAsync` still
called `_playerSessionRepo.GetByZoneIdAsync(zoneIdForWho)`, the whole zone's population. A shared-scope character
therefore had every party inside an instance run and every character in the other wilderness difficulty shard named
back to them by a chat command, none of whom are in a SignalR group the caller can see or chat with. The run half is
the same disclosure #323 fixed in the roster; the shard half is the same one `t_a0369099` fixed there.

**What changed**

- `GameHub.ReadOwnScopeAsync(string zoneId, ZoneType? zoneType)` — one read for every roster the hub shows a
  connection: the run's members while `Context.Items["CurrentZoneInstanceId"]` names a run
  (`GetByZoneInstanceAsync`), otherwise `GetOnSharedZoneAsync(zoneId, SharedScopeDifficulty(zoneType, CurrentDifficultyMode()))`.
  It is the body `SyncArrivedZoneAsync` already had inline, lifted out rather than copied, so the arrival snapshot
  and `/who` cannot drift: the pair that selects the shared scope is the pair that composed the group the
  connection joined.
- The `who` case now resolves the zone once (`_zoneRepo.GetByIdAsync`) and asks the helper for the caller's scope.
  The catalog read is there because the **zone type** is what declares the difficulty split; `ZoneGroupName` forbids
  reading it back out of the group name ("no business rule may decide which run it is in by reading the group
  name"), so the type comes from the catalog or not at all. `ZoneType?` is nullable for the one case that has no row:
  a zone the catalog does not know declares no split, so its shared scope is read whole and only the run dimension
  narrows the list. `/who` also runs once per invocation, not per tick, so the extra read is not a hot path.
- No payload, DTO, or client change: the command still answers with one `SystemMessage` of
  `Players here: a, b`. The `GetChatCommands` description ("List players in your zone") is unchanged on purpose — a
  run is inside the zone the character stands in, and the entry has to stay short.
- Stable reference corrected in place:
  `.github/agent-memory/areas/session-29-2026-04-08-region-map-architecture-playersession-migration.md` named `/who`
  as the remaining unscoped reader of `GetByZoneIdAsync`. The public `GET /api/zones` count is now the only
  deliberate whole-zone read.

**Tests — `GameHubChatTests`, 1 renamed and 3 new.**

- `Who_Lists_Every_Character_Session_In_The_Zone` → `Who_Lists_The_Shared_Scope_Of_The_Zone`: two shared sessions,
  unchanged expectation — the rename is because "every session in the zone" was the defect, not the intent.
- `Who_Does_Not_Name_A_Character_Inside_An_Instance_Run` — the card's reproduction: a shared resident and a delver
  whose session carries a run id, both in `the-droveway`. `/who` names the caller and the resident, not the delver.
  Membership is compared as a set (the DB order is not a contract).
- `Who_Inside_An_Instance_Run_Lists_The_Runs_Members_And_Not_The_Shared_Zone` — the other branch: caller and party
  member on one run id, a shared resident beside the door; the list is the run.
- `Who_In_A_Wilderness_Zone_Lists_The_Callers_Difficulty_Shard_Only` — normal and hardcore sessions in the
  Droveway's shared rooms; the caller's list is their own shard. This is the card's "take the shard filter from the
  same repository method" clause: the fix calls `GetOnSharedZoneAsync` and duplicates no predicate.

**Negative controls, one at a time in a detached worktree at the commit (`git worktree remove`d afterwards).**

- The `who` case reverted to `GetByZoneIdAsync` (the pre-fix shape): 3 failed / 50 passed — exactly the three new
  cases; the renamed shared-scope case passes, correctly, because every session in it is shared.
- `ReadOwnScopeAsync` passes `null` for the difficulty (the run dimension only, #323's behaviour): 2 failed — the new
  shard case and `GameHubZoneTests.TravelToNode_Crossing_A_Border_Lists_Only_The_Callers_Shared_Scope`, which proves
  the helper is still on the arrival path.
- `ReadOwnScopeAsync` ignores the run pointer and always reads the shared scope: 4 failed — the new run case plus the
  three arrival-snapshot cases that list a run's members. Same proof from the other side.

**Gates.** `dotnet build Veldrath.Server.slnx` (0 warnings, 0 errors); `dotnet test Veldrath.Server.slnx` (6648
passed / 0 failed: Shared 1158, Core 2748, Data 697, Server 2045 — the three new cases are the difference);
`dotnet format Veldrath.Server.slnx --verify-no-changes` (exit 0); `node scripts/memory/memory-index.mjs --check`;
`node scripts/check-markdown-links.mjs`.

**Reachability.** The run half needs an instanced location, which no seeder sets yet, so today it is latent. The shard
half is ordinary play: `the-droveway` is a seeded wilderness zone and every character has a `DifficultyMode`, so a
normal and a hardcore character standing in the same wilderness zone is exactly the state `/who` used to leak.

**Noticed and deliberately not changed.**

- `/w` (whisper) still resolves any online character by name, across every zone and run. That is what a whisper is;
  scoping it would be a product change, not a leak fix.
- `LeaveExpiredInstanceAsync` still composes the shared group without the difficulty suffix — already filed against
  the shard card as its own item, still unreachable because nothing seeds an instanced location.
- `GetByZoneIdAsync` remains the whole zone and keeps its "never use it for a roster" remark; `GET /api/zones` counts
  are the intentional caller.
