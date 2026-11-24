---
kind: work-item
date: 2026-10-25
area: content
title: "A2: safe respawn location, death sickness, loud failsafes"
---

### Session-85 (2026-10-25) — A2: safe respawn location, death sickness, loud failsafes

**Safe respawn is a content trait.** `ZoneLocationTraits.SafeRespawn` (bool?) on the content zone location, surfaced
as `ZoneLocationEntry.IsSafeRespawn`. No migration: `Traits` is configured `.ToJson("Traits")`, so adding a property
changes the JSON payload, not the schema — `dotnet ef migrations add` produced an **empty** migration, which was then
removed (it had also rewritten the snapshot with tooling churn: ProductVersion 10.0.9 → 10.0.13 and
`ToTable("X")` → `ToTable("X", (string)null)`; revert that, it is noise).

**Respawn flow.** `RespawnHubCommandHandler` resolves the character's `CurrentZoneId`, asks
`IZoneLocationRepository.GetByZoneIdAsync`, prefers the flagged safe location, else falls back to the zone's first
location, else stays put — and every degradation logs `Error` and returns a `Warning` the hub forwards as a
`SystemMessage`. It then persists `CurrentZoneLocationSlug` via `UpdateCurrentZoneLocationAsync` (note the name: there
is no `UpdateCurrentLocationAsync` on the interface).

**Death sickness** is a timed damage penalty in the attribute blob, not a status effect: the server never hydrates or
ticks status effects (`CombatCharacterHydrator` sets `ActiveStatusEffects = []`), so a "DB-authored power" would have
been decorative. `Death:DeathSicknessMinutes` (5) and `Death:DeathSicknessDamagePenaltyPercent` (25) write
`DeathSicknessUntil` (unix **minutes**, so it survives 2038) and `DeathSicknessPenalty`;
`DeathPenalties.ApplySicknessToDamage` reduces `CalculatePlayerDamage` while the window is open and never below 1.
A disabled window clears stale keys.

**Wire + client.** `CharacterRespawned` now carries `CurrentHealth`, `CurrentMana`, `LocationSlug`,
`LocationDisplayName`, `SicknessExpiresAt` (`CharacterRespawnedPayload` in `types.ts`, enforced by the contract test);
`GameSession` moves `session.location`, refreshes ground items and says "respawned at X … weakened until Y".

**Traps:** a `Mock.Of<IZoneLocationRepository>()` returns **null** for `GetByZoneIdAsync`, which NREs in the handler —
give test doubles a `ReturnsAsync([])` default. And `svelte/prefer-svelte-reactivity` rejects `new Date(...)` inside a
`.ts` file: use `SvelteDate` for the message timestamp.

**Tests:** 5 new respawn tests (safe location, fallback to first, no locations, sickness applied, stale sickness
cleared) + 2 sickness maths tests + 1 Vitest. Server 778 → **785**, full solution **3,930**, Vitest **179**,
typecheck/lint/format clean.

---
