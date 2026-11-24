---
kind: area
date: undated
area: engine
title: "Character Attributes JSON Blob Schema"
---

## Character Attributes JSON Blob Schema

- `UnspentAttributePoints`: int — granted by a level-up (`CharacterProgression.AwardExperience`) and spent via `AllocateAttributePoints`
- `Strength`, `Dexterity`, etc.: int — core stats
- `Gold`: int — currency; deducted by `RestAtLocation` (default cost: 10); modified directly by `AddGold`
- `CurrentHealth`, `MaxHealth`: int — rewritten by a level-up (`CharacterProgression`); restored to max by `RestAtLocation`
- `CurrentMana`, `MaxMana`: int — rewritten by a level-up (`CharacterProgression`); restored to max by `RestAtLocation`
- Defaults when absent: `MaxHealth = Level * 10`, `MaxMana = Level * 5`
- Handler key constants are `internal const string KeyXxx` on the handler class
- **DO NOT store string-valued data in Attributes blob** — it's deserialized as `Dictionary<string, int>`; mixing types causes `JsonException` and data loss

## Derived pools are the class curve, not an attribute formula

`CharacterProgression.AwardExperience` writes `MaxHealth`/`MaxMana` on a level-up from the class row creation encoded
(`StartingHealth + HealthGrowth × (level − 1)`, same for mana: warrior 120 + 12/level, mage 60 + 6/level), falling back
to the engine's `Constitution × 10 + Level × 5` / `Wisdom × 5 + Level × 3` only when no class row resolves. The stored
pool is always the floor, and the current pools are clamped to the new ceiling but never refilled.

`AllocateAttributePoints` writes the attribute and the unspent balance and deliberately leaves the pools alone. **That
is a recorded decision, not a bug** — the next reader should not "fix" it without changing the pool model:

- At the character's current level the class curve returns the value the blob already holds, so calling the level-up
  recompute from the allocation path would be a no-op. Only the no-class-row fallback would pay the engine's
  `Constitution × 10`, which would make one point worth 10 health for a character whose class row is missing and 0 for
  everyone else — the split-brain the class curve exists to avoid.
- The attributes take effect where combat reads them: `Strength` (player damage), `Constitution` (incoming-damage
  defence in `CombatHelpers.CalculateEnemyDamage`) and `Intelligence` (magic damage in `UseAbilityInCombatHubCommand`).
  `Wisdom`, `Dexterity` and `Charisma` have no server-side reader yet, so a point there is inert until a rule consumes it.
- The client's health bar is the stored `MaxHealth` from the `CharacterSelected` payload, so allocating `Constitution`
  does not move it.

`Veldrath.Server.Tests/Features/AllocateAttributePointsHubCommandTests.cs` pins the behaviour: the only keys an
allocation changes are the allocated attribute and `UnspentAttributePoints`.
