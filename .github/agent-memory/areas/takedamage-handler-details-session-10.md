---
kind: area
date: undated
area: engine
title: "TakeDamage Handler Details (session-10)"
---

## TakeDamage Handler Details (session-10)

- Blob keys: `CurrentHealth` (int), `MaxHealth` (int)
- `DamageAmount` must be > 0; zero/negative rejected with "must be positive" message
- Health clamped to 0 (never negative): `newHealth = Math.Max(0, currentHealth - damageAmount)`
- `IsDead = newHealth == 0`
- Default `MaxHealth = Level * 10` when blob missing `MaxHealth` key
- Default `CurrentHealth = MaxHealth` when blob missing `CurrentHealth` key
- Broadcasts `DamageTaken` payload: `{ CharacterId, DamageAmount, CurrentHealth, MaxHealth, IsDead, Source }`
- `DamageTakenPayload` internal record lives in `CharacterSelectViewModel.cs`
- Client callback: `GameViewModel.OnDamageTaken(int damageAmount, int currentHealth, int maxHealth, bool isDead)` → updates `CurrentHealth` property + AppendLog
- Hub sends: `TakeDamageCommand.Execute((damageAmount, source))` via `ReactiveCommand<(int, string?), Unit>`
