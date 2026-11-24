---
kind: work-item
date: 2026-11-24
area: engine
card: t_c1139655
title: "A shared kill ends every fight pointed at the enemy, and says so"
---

### Session-D7-t_c1139655 (2026-11-24) — A shared kill ends every fight pointed at the enemy, and says so

`CombatHelpers.DistributeRewardsAsync` ends a kill by calling `ICombatSessionStore.RemoveByEnemyIdAsync(enemy.Id)`.
A wilderness zone has one roster per zone scope, so several characters can hold a session pointed at one enemy row,
and that call removes *every* one of them. Only the killer's connection heard anything: its own `CombatTurn` carries
`EnemyDefeated`. Everyone else kept a combat panel for a fight the server had dropped, and the next `DefendAction`
was answered `Not in combat` — an `Error` the shell renders as a dismissible banner over a live panel. Measured on
the isolated stack: `tests/encounters.e2e.ts`'s death journey and `a1-the-droveway.e2e.ts` fight the same Drove Road
rows, and two consecutive full-suite runs lost the death journey this way while the character was still alive at
`21 / 105` health.

- **The released ids are data now.** `DistributeRewardsAsync` returns `CombatRewardDistribution(Rewards,
  ReleasedCharacterIds)` — the second list is `RemoveByEnemyIdAsync`'s return value, which the callers discarded. It
  includes a defender who never landed a hit and therefore earns no share, which is exactly the character the old
  code stranded.
- **Each handler reports them, and the hub sends one `CombatEnded` per released character.** `AttackEnemyHubResult`
  and `UseAbilityInCombatHubResult` carry `ReleasedCombatCharacterIds`; `GameHub.EndReleasedFightsAsync` addresses
  each id through `IPlayerSessionRepository.GetByCharacterIdAsync(...).ConnectionId`, the same route
  `AdvanceKillObjectivesAsync` uses for a party member, and skips the killer (its own turn already closes the panel).
  The reason is `shared kill`. A character with no player session is offline in the reconnect window: its session is
  gone with the enemy, so there is nothing to tell and nothing to resume.
- **`shared kill` is not a defeat, and the client no longer reads an unknown reason as one.** The old mapping was
  `reason === 'fled' ? 'fled' : 'defeat'`, so any new ending rendered as "Defeated / -0 XP · -0 gold". The reducer
  `outcomeFromCombatEndReason` now summarises only `defeated` and `fled` and returns `null` for anything else, and
  `session.svelte.ts` keeps whatever summary the player's own turns earned. The panel is unmounted as soon as
  `combat.active` goes false, so the honest end is a closed panel plus `Combat ended: shared kill.` in the log and
  the system channel.
- **The racing click is still possible and harmless.** A `Defend` in flight when someone else's blow lands is
  answered `Not in combat` and its banner stays until dismissed; the `CombatEnded` that follows closes the panel,
  which is the defect this card was about. Suppressing that one error would need the session end and the turned
  action to be ordered, which they are not.
- **Tests.** Handler: `Handle_Reports_Every_Session_The_Kill_Released` in both
  `AttackEnemyHubCommandHandlerTests` and `UseAbilityInCombatHubCommandHandlerTests` (a defender with no
  contribution is still in the released set). Hub: `AttackEnemy_On_A_Shared_Kill_Ends_The_Other_Fighters_Fight` and
  `UseAbilityInCombat_On_A_Shared_Kill_Ends_The_Other_Fighters_Fight` assert the other connection receives
  `{ CharacterId, Reason = "shared kill" }` and the caller receives none. Client: `session.test.ts`'s
  "closes a fight another character finished without calling it a death" pins `outcome === null`, `dead === false`
  and the log line; `reducers.test.ts` pins the reason mapping. E2E: a new deterministic spec in
  `encounters.e2e.ts` ("a kill by another character ends the fight here with a clean ending") — two clients, the
  second levelled through `POST /api/dev/experience` — kills the row the first is fighting and asserts its panel
  closes with no click and names `shared kill`.
- **The e2e death journey now tolerates an ending it did not cause.** Both specs share the roster, so the journey
  re-engages whatever is still standing instead of assuming one enemy will outlast it. A shared kill never heals, so
  the walk to 0 HP stays monotonic and the Respawn assertions still test what they were written for.
- **The specs that kill fight in their own zone group.** `a1-the-droveway.e2e.ts` asserts the drove road spawned at
  least two living rows, and a pool draw there rolls 2–4 rows (`ActorPoolResolver.MinCount`/`MaxCount`) — so when it
  rolls 2, one kill in the shared `zone:the-droveway_normal` roster drops it to one until the 2 s respawn, and a1 read
  that window (measured: pair run on a fresh stack, a1 red
  at a roster count of 1 (`roster.length` then, `ownGroup.length` now) while encounters passed; a1 alone green). A
  wilderness scope is `zone:{zoneId}_{mode}`, so the two encounters specs that kill now create **hardcore**
  characters: their roster is `zone:the-droveway_hardcore`, which no other spec touches, and the `_normal` roster a1
  reads is never depleted. The death journey stays `normal` (hardcore never respawns, and it is the respawn test) and
  removes no row, so sharing costs it nothing. Verified in the database after a pair run: `_hardcore` holds the
  killed rows, `_normal` still its two. One disclosure, since closed: a1's `readRoster` then filtered by location
  only, not zone group, so the hardcore rows counted towards its `>= 2`. It now counts `zone:the-droveway_normal` —
  the group its character joins — while the archetype and no-placement checks still run over the location-wide
  union, because what a row may be does not depend on the group that wrote it.
- **Negative control.** Commenting out only the `EndReleasedFightsAsync` call in `AttackEnemy` reddens exactly
  `AttackEnemy_On_A_Shared_Kill_Ends_The_Other_Fighters_Fight` (`Sequence contains no matching element` at the
  `CombatEnded` lookup) and leaves the other three shared-kill facts green.
