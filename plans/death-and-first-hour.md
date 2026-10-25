# Death Consequences and the First Hour (Milestone A)

> **Position in the roadmap:** D → C → **A** → B.
> Death consequences ride on D (respawn location and combat end state are world state) and C (a party wiping
> together is a different experience from a solo death).
> **Tracker:** epic #38 — A1 #44 (gold-first death cost), A2 #45 (safe respawn + sickness), A3 #46 (tutorial),
> A4 #47 (quest HUD), A5 #48 (objective kinds), A6 #49 (hardcore memorial + leaderboard).

---

## 1. What the code does today (verified)

| Behaviour | Where | Fact |
|---|---|---|
| Death cost | [`HandleDeathIfNeededAsync`](../Veldrath.Server/Features/Characters/Combat/CombatHelpers.cs:185) | **10% XP and 10% gold**, both at once, hardcoded |
| Hardcore death | same method | `DeletedAt` is set — the character is soft-deleted and cannot respawn |
| Respawn | [`RespawnHubCommandHandler`](../Veldrath.Server/Features/Characters/Combat/RespawnHubCommand.cs:56) | 25% of max HP, full mana, and the character **stays exactly where they died** |
| Death sickness / blessing | — | None. The deleted `RespawnService` had an unwired version |
| Tutorial | — | No `tutorial` identifier anywhere in the codebase. Session-72 only added `ResolveResumeNodeId` (resume at the last known node) |
| Quest objectives | [`QuestJournal.Advance`](../Veldrath.Server/Features/Quest/CharacterQuestEntry.cs:85) | Kinds `kill:`, `collect:`, `visit:` only |
| Objective key mismatch | [`GameHub`](../Veldrath.Server/Hubs/GameHub.cs:1949) | The kill path advances with `result.EnemyName` (display name) while `visit` uses a slug — authored `kill:<slug>` content would never progress |
| Quest UI | `GameOverlays.svelte` | Journal overlay exists; no during-play tracker, no completion toast |
| Hardcore surfaces | — | No memorial roster, no leaderboard |

## 2. Agreed direction

**Death should cost something meaningful but not punishing, and it must be legible to the player.**

### 2.1 Death cost — small and gold-first

- Take a **small share of carried gold** (default 10%, configurable, capped by an absolute maximum).
- **Only when the character carries essentially no gold** (below a configurable floor, default 10) take a
  small XP share instead (default 5%). **Never both.** The point is that the penalty always lands somewhere
  the player noticed, and a broke player is not double-punished.
- Numbers live in a validated options class (`ValidateOnStart`), so a missing or nonsensical value **fails
  loudly at startup** instead of silently applying a default.
- The client must be told exactly what was lost: the death broadcast gains the applied penalty
  (`goldLost`, `xpLost`) so the UI can show it rather than leaving the player to notice the difference.

### 2.2 Respawn location and death sickness

- Zones gain a designated safe respawn: `ZoneLocationTraits.SafeRespawn` on a content zone location (the traits
  block is a JSON column, so no content migration is needed). Respawn moves the character there instead of leaving
  them where they fell.
- **Failsafe:** a zone with no safe node logs `Error` naming the zone and falls back to the zone's entry node,
  with a client message. Degraded behaviour is allowed only when it is loud.
- Death sickness: **shipped as a timed damage penalty** (`Death:DeathSicknessMinutes`,
  `Death:DeathSicknessDamagePenaltyPercent`, stored as `DeathSicknessUntil` / `DeathSicknessPenalty` in the attribute
  blob). The original "DB-authored power with a `statusEffect` trait" is deferred: the server never hydrates or ticks
  status effects (`CombatCharacterHydrator` sets `ActiveStatusEffects = []`), so wiring a power would have been
  cosmetic. A working, visible penalty beats a decorative one.
- Leaving a corpse in a high-level location is the real cost; sickness only needs to discourage instant
  re-engagement.

### 2.3 Tutorial handoff and the quest HUD

- Zone content gains an `IsTutorialStart` marker (`ZoneLocationTraits.IsTutorialStart`); the tutorial quest slug is
  configured in `Tutorial:TutorialQuestSlug`. A character who has not completed the tutorial spawns at that node with
  the quest auto-accepted; `TutorialStarted` in the attributes blob marks the handoff as done, and **completion is
  read from the journal** (the tutorial quest reaching `Completed`) rather than duplicated into `TutorialCompleted`.
- **Failsafe:** no tutorial node authored → log `Error`, start at the normal entry node, tell the client.
- Quest HUD tracker: a compact sidebar listing active quests with the nearest incomplete objective, derived
  client-side from `QuestLogReceived` (no new wire event), plus a completion toast on `QuestTurnedIn`.
  Objective completion inside a quest is detected by diffing the progressing snapshot, which avoids inventing
  a per-tick event.

### 2.4 Objective kinds beyond kill/collect/visit

Add `talk:<npcSlug>`, `deliver:<itemSlug>`, `craft:<itemSlug>`, `reach_level:<n>`, each with exactly one
server call site: dialogue interaction, deliver-on-hand-over, craft success, level-up. Along the way, fix the
kill key so objectives match `ArchetypeSlug` — and accept the legacy display-name form with a one-time
`Debug` log, so existing authored content keeps working during the transition.

`Advance` stays a single prefix-matching function; the kind list becomes a documented constant rather than an
implicit convention.

### 2.5 Hardcore memorial and leaderboard

- Keep the soft delete. Add `GET /api/characters/memorial` (the account's soft-deleted hardcore characters)
  and `GET /api/leaderboard/hardcore` (global, ordered by level then XP, with the death date).
- Web: a memorial section on the roster/account page and a leaderboard page. No new tables — `DeletedAt` and
  `DifficultyMode` already carry the data.
- Hardcore characters never respawn; the memorial is where they are remembered.

## 3. Phases

| Phase | Work | Done when |
|---|---|---|
| **A1** | Gold-first death cost behind validated options; death broadcast carries `goldLost`/`xpLost`; client shows it | Dying with gold costs only gold; dying broke costs only XP; the player sees the exact loss |
| **A2** | Safe respawn node (DB-authored) + death-sickness power + failsafe paths | Dying returns the character to the zone's safe node with sickness; a zone without a safe node logs `Error` and uses the entry node |
| **A3** | Tutorial start node + auto-accepted tutorial quest + `TutorialCompleted` flag | A new character lands on the tutorial node with the guided quest; a missing tutorial logs `Error` and continues normally |
| **A4** | Quest HUD tracker + turn-in toast + objective-completion diffing | Active quests are visible during play; completing an objective or turning in a quest is announced without a reload |
| **A5** | Objective kinds `talk`/`deliver`/`craft`/`reach_level`; kill key fixed to the archetype slug with a legacy shim | A dialogue, a craft and a level-up advance their objectives; `kill:<slug>` content progresses |
| **A6** | Memorial roster + hardcore leaderboard (API + web) | A player can see their fallen hardcore characters and the global hardcore ranking |

## 4. Acceptance criteria

1. **Penalty correctness:** three cases (gold only, no gold, gold below the floor) each produce exactly one
   penalty, reported to the client, and nothing when the character has neither gold nor XP.
2. **Loud degradation:** delete the safe-respawn flag and the death-sickness power in turn; each produces an
   `Error` log naming the missing content and a client message, and the player is never stuck dead.
3. **Tutorial isolation:** a character that completed the tutorial never re-enters the tutorial start node.
4. **Objective coverage:** one test per new kind asserting the journal advances only for the matching key.
5. **Hardcore:** a soft-deleted hardcore character appears in the account's memorial and the global board, and
   cannot be respawned.

## 5. Explicitly deferred

- Resurrection items/skills, corpse runs, durability loss on death.
- Party-wipe mechanics (revisit once C is live).
- Any change to the hardcore soft-delete policy itself.
