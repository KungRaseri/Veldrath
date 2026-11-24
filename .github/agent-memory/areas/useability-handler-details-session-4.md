---
kind: area
date: undated
area: engine
title: "UseAbility Handler Details (session-4)"
---

## UseAbility Handler Details (session-4)

- `DefaultManaCost = 10` (`internal const` on `UseAbilityHubCommandHandler`) deducted from `CurrentMana` per use
- Healing detected via `AbilityId.Contains("heal", OrdinalIgnoreCase)`
- Healing restores `Min(HealingAmount = 25, maxHealth - currentHealth)` HP
- Hub method `GameHub.UseAbility(string abilityId)` dispatches `UseAbilityHubCommand` and sends `AbilityUsed` with
  `{ CharacterId, AbilityId, ManaCost, RemainingMana, HealthRestored }` to the zone group, or to the caller when
  the character is not in a zone. The payload is an anonymous object — there is no C# `AbilityUsedPayload` record.
- Web client: `AbilityUsedPayload` in `web/client/src/lib/game/types.ts`; `Session` subscribes with
  `hub.on('AbilityUsed')` in `web/client/src/lib/game/session.svelte.ts` and applies it through
  `applyAbilityToStats` (`web/client/src/lib/game/reducers.ts`), which sets `currentMana` and adds
  `healthRestored` capped at `maxHealth`. The client sends `hub.invoke('UseAbility', abilityId)`.
