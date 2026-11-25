---
kind: work-item
date: 2026-11-25
area: engine
card: t_0d9521bd
title: "An ability kill drops the same loot and clears the same room a melee kill does"
---

### Session t_0d9521bd (2026-11-25) — A kill is a kill, whichever blow landed

Follow-up to [`2026-11-24-t_83308434-an-ability-kill-credits-every-participant.md`](2026-11-24-t_83308434-an-ability-kill-credits-every-participant.md),
which gave the ability death path the *objective* credit a melee kill awards. Two more things a melee kill did
were still missing from `UseAbilityInCombatHubCommandHandler`, and both lived as private code inside
`AttackEnemyHubCommandHandler`:

- **Loot.** `RollLootAsync` was a private method on the melee handler; `UseAbilityInCombatHubResult` had no
  `ItemsDropped` field at all, so a fireball kill never rolled the archetype's table.
- **Room clear.** The `CountAliveAsync` → `ExtendClearedRespawnsAsync` → `RecordClearAsync` block and the
  `Cleared*`/`FirstClear*`/`UnlockedLocationSlugs` fields were also melee-only, and `GameHub.AttackEnemy` was the
  only place that broadcast `LocationCleared`.

**What changed**

- `CombatHelpers.RollLootAsync` and `CombatHelpers.ResolveRoomClearAsync` now own both, and both handlers call
  them from their defeated branch. `ResolveRoomClearAsync` returns a new internal `CombatRoomClear` record (run id,
  room slug, first-clear flags, unlocked slugs) so each handler projects the same five result fields from one
  object. The melee handler's private `RollLootAsync` is gone; its behaviour is unchanged.
- `UseAbilityInCombatHubResult` gained `ItemsDropped`, `ClearedInstanceId`, `ClearedLocationSlug`,
  `FirstClearOfRun`, `FirstClearForCharacter`, `UnlockedLocationSlugs`; the handler now takes `IZoneInstanceService`
  and `ILootTableRepository` (the melee handler already did).
- `GameHub` grew `BroadcastLocationClearedAsync`, which both `AttackEnemy` and `UseAbilityInCombat` call, so the
  `LocationCleared` payload plus the per-unlock `SystemMessage` cannot reach one death path and miss the other.
  `GameHub.UseAbilityInCombat`'s `CombatTurn` literal now sends `result.ItemsDropped`.

**The web payload: no `types.ts` edit was needed, and the card's premise was wrong in a useful way.**
`payload-contract.test.ts` derives `CombatTurnPayload` from the *union* of every `SendAsync("CombatTurn", { … })`
literal, and the melee literal already sent `ItemsDropped` — so adding it to the ability literal changed nothing in
the union, and the clear travels on `LocationCleared`, whose `LocationClearedPayload` the client already mirrors and
`session.svelte.ts` already handles. No field was invented to force an interface change. What the union *did* hide is
the defect class itself: **a variant that reports `EnemyDefeated` while a sibling variant sends a kill field passes
silently.** The suite now parses each literal separately (`readWirePayloadVariants`, with the union kept for the
existing assertions) and fails with `CombatTurn variant #N` for any variant that can report a kill but does not send
`ItemsDropped`. That guard is red on the pre-fix hub and green after.

**Client-visible consequence of the payload line.** `session.svelte.ts` re-reads the bag only when
`payload.itemsDropped` is non-empty, and `reducers.describeCombatTurn` names drops for any action, so the ability
turn's drop is both logged ("Found: …") and visible in the sheet with no client code change.

**Tests**

- `UseAbilityInCombatHubCommandHandlerTests.Handle_Drops_The_Same_Loot_A_Melee_Kill_Of_The_Same_Archetype_Drops` —
  one archetype, one guaranteed table, one fireball kill and one melee kill in the same test; the two
  `ItemsDropped` lists are equivalent and the caster's `InventoryBlob` carries the item.
- `...Handle_Records_The_RoomClear_An_Ability_Kill_Completes` — the enemy carries a real run id, the room names a
  hidden chamber as its unlock key; asserts `ClearedInstanceId`/`ClearedLocationSlug`/both first-clear flags/the
  unlocked slug, plus the three persisted facts: the cleared-window extension, `ZoneInstanceLocationState.IsCleared`
  with `FirstClearedByCharacterId`, and the `CharacterUnlockedLocation` row.
- `...Handle_Reports_No_Drops_And_No_Clear_When_The_Ability_Does_Not_Kill` — a living enemy with a guaranteed-drop
  table: `ItemsDropped` empty, every `Cleared*` field null/false, no cleared window applied, no state row, no bag
  write.
- `GameHubTests.UseAbilityInCombat_Forwards_The_Kills_Loot_On_The_Turn`,
  `GameHubTests.UseAbilityInCombat_Broadcasts_LocationCleared_When_The_Kill_Clears_The_Room`,
  `GameHubTests.UseAbilityInCombat_Reports_No_Clear_When_The_Turn_Cleared_Nothing` — the hub half, against a mocked
  mediator.
- `payload-contract.test.ts` — the variant guard described above.
- The ability-test harness's `AddEnemy` now stamps `ZoneGroup`/`LocationSlug`/`Template` the way the EF store does.
  Without them the fake row stood "nowhere" (`""`/`""`), so `CountAliveAsync` could never see the room as cleared —
  the melee handler's tests have the same latent gap and are untouched.

**Negative controls, one at a time, restored after each.**

- Ability handler with the two helper calls replaced by an empty list / `null`: exactly
  `Handle_Drops_The_Same_Loot_A_Melee_Kill_Of_The_Same_Archetype_Drops` ("collection is empty") and
  `Handle_Records_The_RoomClear_An_Ability_Kill_Completes` ("ClearedInstanceId … found <null>") fail; 2 failed /
  8 passed. The non-kill test passes either way, by design.
- `GameHub` ability literal without `result.ItemsDropped`: the new parity guard fails with
  `expected [ 'CombatTurn variant #4' ] to deeply equal []`, and `GameHubTests.UseAbilityInCombat_Forwards_The_Kills_Loot_On_The_Turn`
  fails; the other 3 parity tests stay green.
- `GameHub` ability branch without the `BroadcastLocationClearedAsync` call: exactly
  `UseAbilityInCombat_Broadcasts_LocationCleared_When_The_Kill_Clears_The_Room` fails ("collection is empty");
  28 other `UseAbilityInCombat*` tests pass.

**Gates** — `dotnet build Veldrath.Server.slnx` (0 warnings, 0 errors);
`dotnet test Veldrath.Server.slnx` (Shared 1158, Core 2748, Data 697, Server 2000 — 6603 passed, 0 failed);
`dotnet format Veldrath.Server.slnx --verify-no-changes` (exit 0); `cd web` → `npm run typecheck` (0 errors,
0 warnings), `npm run lint` (Prettier + ESLint clean), `npm run test:unit --workspace client -- --run`
(107 files, 1593 tests); `node scripts/memory/memory-index.mjs --check`,
`node scripts/check-markdown-links.mjs`, `node scripts/check-content-names.mjs`,
`node scripts/check-e2e-manifest.mjs` (all exit 0).

**Noticed and deliberately not changed.** Both handlers still roll loot and record the clear *outside* the
`TryMarkDeadAsync` win, so a caller that loses the death race drops the table a second time — inherited melee
behaviour that this card mirrors rather than fixes, because `t_d22c80d2` owns the lost-race semantics and would
have to restructure the same branch. Noted on that card. Also unchanged: the clear still needs an
`EnemyInstance.ZoneInstanceId` to mint a run credit, and no seeder sets a location `IsInstanced`, so on shipped
content the loot half is live and the run-clear half stays code+tests (same reachability note as `t_c31b96fb`).
