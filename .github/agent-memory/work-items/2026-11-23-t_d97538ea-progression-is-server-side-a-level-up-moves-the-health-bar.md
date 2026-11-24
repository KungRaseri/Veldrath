---
kind: work-item
date: 2026-11-23
area: content
card: t_d97538ea
title: "Progression is server-side; a level-up moves the health bar"
---

### Session-D5-t_d97538ea (2026-11-23) — Progression is server-side; a level-up moves the health bar

`GameHub.GainExperience` was an ordinary hub method, so any authenticated connection that had selected a character
could award itself any amount of experience; `GameHub.TakeDamage` did the same for damage. Neither had a web caller
(the parity allow-list recorded that), and neither had death handling — `TakeDamage` could zero HP with no
`CombatEnded` and no penalty. Both methods (and their request DTOs) are gone. The server-shape test
`GameHubTests.GameHub_Does_Not_Expose_ClientCallableProgression_Or_Damage_Methods` pins the absence.

- **The sanctioned preparation path is `POST /api/dev/experience`.** It dispatches `GainExperienceHubCommand` — the
  same command the quest turn-in uses — behind the same `Development`/`Test` gate as `/api/dev/teleport`. The A1
  Grevenmire and Drowning Pits specs prepare their character through it (`prepareJourneyCharacter`), so the journey
  still runs the real handler.
- **`GainExperienceHubCommandHandler` now recomputes the derived pools.** `CharacterProgression.AwardExperience`
  applies the rolling `Level × 100` formula and, when a level is crossed, rewrites `MaxHealth`/`MaxMana` in the
  attributes blob. The curve is the one creation encodes, resolved from the character's class row: `StartingHealth +
  HealthGrowth × (level − 1)` (warrior 120 + 12/level, mage 60 + 6/level, mana 20 + 2 and 100 + 10), with the engine
  formulas (`Constitution × 10 + level × 5`, `Wisdom × 5 + level × 3`) as the fallback when no class row resolves.
  Deriving from either formula alone was the round-1 review defect: creation writes the class pools but
  `POST /api/characters` writes the engine formulas, and the old formula lowered a mage's 100 mana to 51 and a
  warrior's 120 health to 110 on the first level-up. **The stored pool is always the floor** (`Math.Max`), so a
  level-up can never clamp a bar down; current pools are clamped, not refilled, so a level gained mid-fight raises
  the ceiling without healing the character. `EfCoreCharacterClassRepository.MapToModel` had to start projecting
  `Stats.HealthGrowth`/`ManaGrowth` into `Progression.StatGrowth` (it left `Progression` empty) for the curve to be
  reachable through the repository. Previously a level-6 character kept the level-1 120 HP bar.
- **The quest turn-in carries the event and the objective.** It broadcasts `ExperienceGained` (level, XP, and the
  re-derived pools) to the caller and advances `reach_level:` on a level-up, which is what the removed hub method
  used to do. The client's handler takes `newExperience` (not the old mismatched `newXP`) and applies the pools.
- **The dead-state class is closed at the other two doors.** `UseAbilityHubCommand` (a `heal` ability) and
  `RestAtLocationHubCommand` now refuse a character whose blob stores `CurrentHealth <= 0` — the pattern
  `UseItemHubCommand` settled on — while a missing key stays allowed for pre-vitals blobs. The admin `/sethealth`
  payload now matches the client's `DamageTaken` contract (`damage`/`currentHP`/`maxHP`/`isDead`), and the session
  sets `dead = payload.isDead`, so a GM heal clears the banner instead of leaving it stuck until a reload.
- **Known gap, filed separately:** combat rewards still write `character.Experience += share.Xp` in
  `CombatHelpers.DistributeRewardsAsync` without applying a level, so a kill can never level a character (the XP
  column grows, the level does not). That path needs the same `CharacterProgression` call plus the level/pool fields
  in the combat payload before it can be switched over without leaving the sheet stale.
- Tests: `GainExperienceHubCommandHandlerTests` (class-curve growth, the two no-loss facts, the engine fallback and the
  corrupt-blob rebuild), `DevEndpointTests.Experience_*` (the warrior curve value and the stored-mana floor on a real
  character), `HubHandlerGuardTests.UseAbility_RefusesADeadCharacter`/`Rest_RefusesADeadCharacter`,
  `session.test.ts` (re-derived pools), `session-events.test.ts` (death banner clears), and
  `hub-methods.test.ts` (allow-list no longer names the retired methods).
