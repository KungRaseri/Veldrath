---
kind: area
date: undated
area: engine
title: "TakeDamage Handler Details (session-10)"
---

## TakeDamage Handler Details (session-10) — retired

> **Retired 2026-11-23.** Both halves are gone: `GameHub` no longer exposes `TakeDamage`, and
> `TakeDamageHubCommand` was deleted with it. `GameHubTests` pins the absence
> (`hubMethods.Should().NotContain("TakeDamage", "damage is applied by the combat handlers only")`), and
> `areas/wired-hub-mediatr-bridges.md` records the retirement. The Avalonia `GameViewModel.OnDamageTaken`
> callback and the `DamageTakenPayload` record it consumed died with the desktop client. The rest of this note
> now describes the path that actually applies damage.

- Damage is applied inside the combat handlers — `Veldrath.Server/Features/Characters/Combat/AttackEnemyHubCommand.cs`
  and the defend / flee / ability-in-combat variants — which subtract the enemy's hit from the player's
  `CurrentHealth` in the attributes blob and clamp at zero (`player.Health = Math.Max(0, player.Health - enemyDamage)`).
- The announcement is `CombatTurn`, sent to the caller only, and it carries the whole turn: `Action`,
  `PlayerDamage`, `EnemyRemainingHealth`, `EnemyDefeated`, `EnemyDamage`, `EnemyAbilityUsed`,
  `PlayerRemainingHealth`, `PlayerDefeated`, `PlayerHardcoreDeath`, `GoldLost`, `XpLost`, `XpEarned`,
  `GoldEarned`, the level/pool fields (`NewLevel`, `NewExperience`, `LeveledUp`, `MaxHealth`, `CurrentHealth`,
  `MaxMana`, `CurrentMana`, `UnspentAttributePoints`) and `ItemsDropped`. The web payload type is
  `CombatTurnPayload` in `web/client/src/lib/game/types.ts`.
- `DamageTaken` survives with exactly one sender: the GM `/sethealth` path in `Veldrath.Server/Hubs/GameHub.cs`.
  Its payload is `{ CharacterId, damage, currentHP, maxHP, isDead, Source = "Admin" }` — camelCase field names,
  unlike the rest of the hub — and the code comment there says so on purpose. The web `Session` still subscribes
  with `hub.on('DamageTaken')` and applies it through `applyDamageToStats`
  (`web/client/src/lib/game/reducers.ts`), which is what lets a GM heal clear the death banner without a reload.
- Do not re-add a client-callable damage method: damage is a combat consequence, not a client command.
